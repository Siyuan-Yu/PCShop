using System;
using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 手动走路（WASD）。街道是沿路网的一条条"走廊"（商业街两侧人行道宽，小路窄），
    /// 玩家可以在走廊里朝任何方向自由走，碰到走廊边缘就贴着边滑，不会走进楼里或草地。
    /// 移动只更新位置，世界时间由统一时钟推进，不随道路压缩比例变化。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>离门口这么近才能按 F 进门（站到店门前的人行道上）。</summary>
        public const double EnterRadius = 3.0;

        // Motorsport entrances open onto wider roads and are commonly approached by vehicle.
        private static double DoorEnterRadius(Place place) =>
            place.Kind == PlaceKind.Garage || place.Business == "racing" || place.Business == "motor" ? 9.0 : EnterRadius;

        /// <summary>y=0 那条路边上的门面离路中线多远：商业街路宽，门面更靠外。</summary>
        public const double LinkFrontY = 4.5, ShoppingFrontY = 8.0;

        public static double FrontYAt(double x) => x >= ShoppingStreetMinX && x <= ShoppingStreetMaxX ? ShoppingFrontY : LinkFrontY;

        /// <summary>地点的门口（画面坐标）：路边的店在门面正中，城中村的家门在小路右手边。</summary>
        public MapPoint DoorOf(Place p)
        {
            MapPoint n = City.ViewPosition(p.NodeId);
            if (p.ExplicitEntrance) return new MapPoint(n.X + p.EntranceX, n.Y + p.EntranceY);
            if (Math.Abs(n.Y) < 0.1) return new MapPoint(n.X, FrontYAt(n.X));
            return p.Kind == PlaceKind.Home ? new MapPoint(n.X + 2, n.Y) : n;
        }

        /// <summary>自动前往时站的门前位置：门口往路这边退半米，正好在人行道上。</summary>
        public MapPoint StandOf(Place p)
        {
            MapPoint door = DoorOf(p), n = City.ViewPosition(p.NodeId);
            double d = Dist(door, n);
            if (d < 1e-6) return door;
            double k = Math.Min(DoorStandBack, d) / d;
            return new MapPoint(door.X + (n.X - door.X) * k, door.Y + (n.Y - door.Y) * k);
        }

        private const double DoorStandBack = 0.5;

        /// <summary>步行的人行道离门面多远：y=0 那条路上自动走沿北侧（店门那侧）人行道走。</summary>
        public const double SidewalkInset = 0.8;

        /// <summary>自动前往经过某个节点时实际走的点：步行走人行道，其余交通方式走马路。</summary>
        private MapPoint LaneOf(string nodeId, VehicleKind kind)
        {
            MapPoint n = City.ViewPosition(nodeId);
            if (City.LayoutVersion>=7 || kind != VehicleKind.Walk || Math.Abs(n.Y) >= 0.1) return n;
            return new MapPoint(n.X, FrontYAt(n.X) - SidewalkInset);
        }

        /// <summary>商业街走廊半宽（含两侧人行道）。商业街的路是连接路的两倍宽。</summary>
        public const double MainStreetHalfWidth = 7.7;

        /// <summary>去电子市场的连接路、市场门前（也在 y=0 那条线上）的走廊半宽。</summary>
        public const double LinkRoadHalfWidth = 4.2;

        /// <summary>商业街的 x 范围（画面坐标）：这一段 y=0 的路按宽马路处理；最后一家店到城中村路口那一小段还是窄路。</summary>
        public const double ShoppingStreetMinX = -100.5, ShoppingStreetMaxX = 100;

        public static bool IsMainRoad(MapPoint a, MapPoint b) => Math.Abs(a.Y) < 0.1 && Math.Abs(b.Y) < 0.1;

        public static bool IsShoppingStreet(MapPoint a, MapPoint b) =>
            IsMainRoad(a, b) && Math.Min(a.X, b.X) >= ShoppingStreetMinX && Math.Max(a.X, b.X) <= ShoppingStreetMaxX;

        /// <summary>城中村等小路的走廊半宽。</summary>
        public const double SideRoadHalfWidth = 2.0;

        // One continuous street surface: lane and paving share the same movement area.
        public const double CityPavingWidth = 1.4;
        public static double CityRoadWidth(MapPoint a, MapPoint b)
        {
            bool horizontal=Math.Abs(a.Y-b.Y)<.01;
            return horizontal?(Math.Abs(a.Y/135-Math.Round(a.Y/135))<.001?8:5):7;
        }

        private struct Corridor
        {
            public MapPoint A, B;
            public double HalfWidth;
            public bool Footpath;
            public double Scale; // 游戏米 / 画面米
        }

        private readonly struct ResidenceFootprint
        {
            private const double Clearance = .12;
            private readonly MapPoint _door;
            private readonly double _cos, _sin, _halfWidth, _back, _reach;

            public ResidenceFootprint(Place place, MapPoint door)
            {
                _door = door;
                double yaw = place.Heading * Math.PI / 180;
                _cos = Math.Cos(yaw);
                _sin = Math.Sin(yaw);
                _halfWidth = place.ExteriorWidth / 2 + Clearance;
                _back = place.ExteriorDepth + Clearance;
                _reach = _halfWidth + _back;
            }

            // The building starts at its door plane and extends along Unity's local +Z.
            private MapPoint ToLocal(MapPoint point)
            {
                double x = point.X - _door.X, y = point.Y - _door.Y;
                return new MapPoint(_cos * x - _sin * y, _sin * x + _cos * y);
            }

            public MapPoint MoveOutside(MapPoint point)
            {
                var local = ToLocal(point);
                double side = _halfWidth - Math.Abs(local.X);
                double front = local.Y + Clearance;
                double back = _back - local.Y;
                if (side <= 0 || front <= 0 || back <= 0) return point;

                double width = local.X, depth = local.Y;
                if (side < Math.Min(front, back)) width = (width < 0 ? -1 : 1) * (_halfWidth + .001);
                else depth = front <= back ? -Clearance - .001 : _back + .001;
                return new MapPoint(_door.X + _cos * width + _sin * depth, _door.Y - _sin * width + _cos * depth);
            }

            public double FirstHit(MapPoint from, MapPoint to, out MapPoint normal)
            {
                normal = default;
                if (Math.Min(from.X, to.X) > _door.X + _reach || Math.Max(from.X, to.X) < _door.X - _reach
                    || Math.Min(from.Y, to.Y) > _door.Y + _reach || Math.Max(from.Y, to.Y) < _door.Y - _reach)
                    return 1;
                var start = ToLocal(from);
                var target = ToLocal(to);
                double dx = target.X - start.X, dy = target.Y - start.Y;
                double side = FaceHit(start.X, dx, -_halfWidth, _halfWidth, start.Y, dy, -Clearance, _back);
                double end = FaceHit(start.Y, dy, -Clearance, _back, start.X, dx, -_halfWidth, _halfWidth);
                normal = side < end ? new MapPoint(_cos, -_sin) : new MapPoint(_sin, _cos);
                return Math.Min(side, end);
            }

            private static double FaceHit(double position, double motion, double min, double max,
                double across, double acrossMotion, double acrossMin, double acrossMax)
            {
                if (Math.Abs(motion) < 1e-9) return 1;
                double time = ((motion > 0 ? min : max) - position) / motion;
                if (time < 0 || time >= 1) return 1;
                double crossing = across + time * acrossMotion;
                return crossing >= acrossMin && crossing <= acrossMax ? time : 1;
            }
        }

        private List<Corridor> _corridors;
        private List<ResidenceFootprint> _residenceFootprints;
        private MapPoint? _free;
        private bool _steering;
        private double _steerX, _steerY;
        private bool _manualMoving;

        /// <summary>现在不是正好站在某个路口上（手动走到了路中间）。</summary>
        public bool OffNode => _free.HasValue;

        /// <summary>玩家正在自己走（WASD）或在自动前往的路上。</summary>
        public bool IsWalking => Trip != null || _manualMoving;

        /// <summary>最近一次移动的朝向（画面坐标，单位向量）。</summary>
        public double HeadingX { get; private set; }

        public double HeadingY { get; private set; } = 1;

        /// <summary>打车由司机开，不能自己走；其余出行方式都可以。</summary>
        public bool CanSteer => !IsWorking && !IsGameOver && !CityActivityExclusive && Vehicle != VehicleKind.Taxi;

        /// <summary>每帧由输入层告诉我想往哪个方向走（画面坐标，不用归一化）。传 (0,0) 表示松手。</summary>
        public void SetSteer(double dx, double dy)
        {
            double l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-6)
            {
                _steering = false;
                return;
            }
            _steering = true;
            CancelCityTravel();
            _steerX = dx / l;
            _steerY = dy / l;
        }

        private bool SteerActive => _steering && CanSteer && CurrentPlace == null;

        private void ClearEdge()
        {
            _free = null;
        }

        // ---------- 走廊 ----------

        private List<Corridor> Corridors()
        {
            if (_corridors != null) return _corridors;
            _corridors = new List<Corridor>();
            var done = new HashSet<string>();
            foreach (WorldNode n in City.Map.Nodes)
            {
                foreach (WorldEdge e in City.Map.NeighborsOf(n.Id))
                {
                    string key = string.CompareOrdinal(n.Id, e.To) < 0 ? n.Id + "|" + e.To : e.To + "|" + n.Id;
                    if (!done.Add(key)) continue;
                    MapPoint a = City.ViewPosition(n.Id), b = City.ViewPosition(e.To);
                    double view = Dist(a, b);
                    _corridors.Add(new Corridor
                    {
                        A = a,
                        B = b,
                        HalfWidth = City.LayoutVersion>=7 ? CityRoadWidth(a,b)/2+CityPavingWidth
                            : IsShoppingStreet(a, b) ? MainStreetHalfWidth : IsMainRoad(a, b) ? LinkRoadHalfWidth : SideRoadHalfWidth,
                        Scale = view < 1e-6 ? 1 : e.Length / view,
                    });
                }
            }
            foreach(var place in City.PublicPlaces)
                if(place.ExplicitEntrance)_corridors.Add(new Corridor { A=City.ViewPosition(place.NodeId), B=DoorOf(place), HalfWidth=1.6, Scale=City.TravelDistanceScale });
            if (City.IsLakeCity && City.HasPlace("home") && City.GetPlace("home").HousingForm == ResidenceForm.LaneHouse)
            {
                var origin = DoorOf(City.GetPlace("home"));
                foreach (var path in OldQuarterLanes.Paths)
                    _corridors.Add(new Corridor { A = OldQuarterLanes.ToWorld(origin,path.x1,path.z1),
                        B = OldQuarterLanes.ToWorld(origin,path.x2,path.z2), HalfWidth = path.width/2,
                        Scale = City.TravelDistanceScale, Footpath = true });
            }
            return _corridors;
        }

        private static double Dist(MapPoint a, MapPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static MapPoint ClosestOnSegment(MapPoint a, MapPoint b, MapPoint p)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double l2 = dx * dx + dy * dy;
            if (l2 < 1e-12) return a;
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2;
            t = Math.Max(0, Math.Min(1, t));
            return new MapPoint(a.X + dx * t, a.Y + dy * t);
        }

        private double NearestCorridorScale(MapPoint point)
        {
            double scale = 1;
            double nearestDist = double.MaxValue;
            foreach (Corridor c in Corridors())
            {
                if (c.Footpath && Vehicle != VehicleKind.Walk && Vehicle != VehicleKind.EBike) continue;
                MapPoint q = ClosestOnSegment(c.A, c.B, point);
                double dx = point.X - q.X, dy = point.Y - q.Y;
                double distance = dx * dx + dy * dy;
                if (distance >= nearestDist) continue;
                nearestDist = distance;
                scale = c.Scale;
            }
            return scale;
        }

        /// <summary>把想去的点限制进走廊里：在走廊内就原样，否则贴着最近走廊的边。</summary>
        private MapPoint Constrain(MapPoint want)
        {
            double bestOver = double.MaxValue;
            MapPoint clamped = want;
            foreach (Corridor c in Corridors())
            {
                if (c.Footpath && Vehicle != VehicleKind.Walk && Vehicle != VehicleKind.EBike) continue;
                MapPoint q = ClosestOnSegment(c.A, c.B, want);
                double dx = want.X - q.X, dy = want.Y - q.Y;
                double distanceSquared = dx * dx + dy * dy;
                if (distanceSquared <= c.HalfWidth * c.HalfWidth) return want;
                double d = Math.Sqrt(distanceSquared);
                double over = d - c.HalfWidth;
                if (over < bestOver)
                {
                    bestOver = over;
                    double k = c.HalfWidth / d;
                    clamped = new MapPoint(q.X + dx * k, q.Y + dy * k);
                }
            }
            return clamped;
        }

        private List<ResidenceFootprint> ResidenceFootprints()
        {
            if (_residenceFootprints != null) return _residenceFootprints;
            _residenceFootprints = new List<ResidenceFootprint>();
            if (City.LayoutVersion >= 7)
                foreach (var place in City.OfKind(PlaceKind.Home))
                    if (place.ExplicitEntrance && place.Width > 0 && place.Depth > 0)
                        _residenceFootprints.Add(new ResidenceFootprint(place, DoorOf(place)));
            return _residenceFootprints;
        }

        private MapPoint OutsideResidenceWalls(MapPoint point)
        {
            foreach (var footprint in ResidenceFootprints()) point = footprint.MoveOutside(point);
            return point;
        }

        private MapPoint StopAtResidenceWall(MapPoint from, MapPoint to, out MapPoint normal)
        {
            double first = 1;
            normal = default;
            foreach (var footprint in ResidenceFootprints())
            {
                double hit = footprint.FirstHit(from, to, out var wallNormal);
                if (hit >= first) continue;
                first = hit;
                normal = wallNormal;
            }
            if (first >= 1) return to;
            double safe = Math.Max(0, first - .0001);
            return new MapPoint(from.X + (to.X - from.X) * safe, from.Y + (to.Y - from.Y) * safe);
        }

        private MapPoint SlideAlongResidenceWall(MapPoint from, MapPoint to)
        {
            var stop = StopAtResidenceWall(from, to, out var normal);
            if (normal.X == 0 && normal.Y == 0) return stop;
            double dx = to.X - stop.X, dy = to.Y - stop.Y;
            double againstWall = dx * normal.X + dy * normal.Y;
            var slide = Constrain(new MapPoint(stop.X + dx - againstWall * normal.X, stop.Y + dy - againstWall * normal.Y));
            return StopAtResidenceWall(stop, slide, out _);
        }

        private string NearestNodeTo(MapPoint p)
        {
            string best = PlayerNodeId;
            double bd = double.MaxValue;
            foreach (WorldNode n in City.Map.Nodes)
            {
                double dx = p.X - n.X / City.DistanceScale, dy = p.Y - n.Y / City.DistanceScale;
                double d = dx * dx + dy * dy;
                if (d < bd)
                {
                    bd = d;
                    best = n.Id;
                }
            }
            return best;
        }

        /// <summary>手动接管：把正在进行的自动前往转成"站在原地"的自由状态。</summary>
        private void BreakTripToEdge()
        {
            CancelCityTravel();
            MapPoint pos = Trip.Position;
            Trip = null;
            _free = pos;
            PlayerNodeId = NearestNodeTo(pos);
        }

        /// <summary>按方向走一步。返回是否真的走动了；不推进世界时间。</summary>
        private bool StepManual(double scaledSeconds)
        {
            VehicleSpec v = CurrentVehicle;
            MapPoint from = PlayerPosition, res = OutsideResidenceWalls(from);
            double budget = CityTravelTiming.PlayerScaledSecondsMeters(v, scaledSeconds, Time.SecondsPerMinuteAt1x);
            // Small spatial steps prevent 8x movement from crossing a wall or skipping an entire short road.
            for (int step = 0; budget > 1e-8 && step < 4096; step++)
            {
                double scale = Math.Max(1e-6, NearestCorridorScale(new MapPoint(res.X + _steerX * .01, res.Y + _steerY * .01)));
                double view = Math.Min(.75, budget / scale);
                MapPoint next = Constrain(new MapPoint(res.X + _steerX * view, res.Y + _steerY * view));
                next = SlideAlongResidenceWall(res, next);
                if (TryDistrictRoadStop(res, next, out MapPoint gateStop, out int gateTier))
                {
                    res = gateStop; _steering = false; NotifyDistrictRoadStop(gateTier); break;
                }
                double advanced = Dist(res, next);
                if (advanced < 1e-8) break;
                // A blocked component must not turn into extra tangent speed. Spend the intended
                // directional step, so diagonal wall sliding keeps the same physical pace.
                budget -= view * scale; res = next;
            }
            double moved = Dist(from, res);
            if (moved < 1e-6) return false;

            _free = res;
            PlayerNodeId = NearestNodeTo(res);
            HeadingX = (res.X - from.X) / moved;
            HeadingY = (res.Y - from.Y) / moved;

            return true;
        }

        // ---------- 进门 ----------

        /// <summary>站在入口附近（不用正好站在路口）的地点。</summary>
        public Place NearbyPlace
        {
            get
            {
                if (CurrentPlace != null || Trip != null || IsGameOver) return null;
                MapPoint p = PlayerPosition;
                Place best = null;
                double bestDist = double.PositiveInfinity;
                foreach (Place pl in City.PublicPlaces)
                {
                    double d = Dist(p, DoorOf(pl));
                    if (d < bestDist && d <= DoorEnterRadius(pl))
                    {
                        bestDist = d;
                        best = pl;
                    }
                }
                return best;
            }
        }

        /// <summary>进入附近的地点（会先走到入口）。进不去（比如打烊）时人留在门口原地，不挪位置。</summary>
        public bool EnterNearby()
        {
            Place p = NearbyPlace;
            if (p == null) return Fail("附近没有能进的地方。走到门口再按 F。");
            string closed = ClosedReason(p);
            if (closed != null) return Fail(closed);
            PlayerNodeId = p.NodeId;
            ClearEdge();
            return Enter(p);
        }
    }
}
