using System;
using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 手动走路（WASD）。街道是沿路网的一条条"走廊"（商业街两侧人行道宽，小路窄），
    /// 玩家可以在走廊里朝任何方向自由走，碰到走廊边缘就贴着边滑，不会走进楼里或草地。
    /// 时间按走过的路程折算：压缩连接路上画面走一米等于游戏里好几米。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>走到离某地点入口这么近（画面米）就能按 F 进去。</summary>
        /// <summary>离门口这么近才能按 F 进门（站到店门前的人行道上）。</summary>
        public const double EnterRadius = 3.0;

        /// <summary>y=0 那条路边上的门面离路中线多远：商业街路宽，门面更靠外。</summary>
        public const double LinkFrontY = 4.5, ShoppingFrontY = 8.0;

        public static double FrontYAt(double x) => x >= ShoppingStreetMinX && x <= ShoppingStreetMaxX ? ShoppingFrontY : LinkFrontY;

        /// <summary>地点的门口（画面坐标）：路边的店在门面正中，城中村的家门在小路右手边。</summary>
        public MapPoint DoorOf(Place p)
        {
            MapPoint n = City.ViewPosition(p.NodeId);
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
            if (kind != VehicleKind.Walk || Math.Abs(n.Y) >= 0.1) return n;
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

        private struct Corridor
        {
            public MapPoint A, B;
            public double HalfWidth;
            public double Scale; // 游戏米 / 画面米
        }

        private List<Corridor> _corridors;
        private MapPoint? _free;
        private bool _steering;
        private double _steerX, _steerY;
        private double _meterCarry;
        private bool _manualMoving;

        /// <summary>现在不是正好站在某个路口上（手动走到了路中间）。</summary>
        public bool OffNode => _free.HasValue;

        /// <summary>玩家正在自己走（WASD）或在自动前往的路上。</summary>
        public bool IsWalking => Trip != null || _manualMoving;

        /// <summary>最近一次移动的朝向（画面坐标，单位向量）。</summary>
        public double HeadingX { get; private set; }

        public double HeadingY { get; private set; } = 1;

        /// <summary>打车由司机开，不能自己走；其余出行方式都可以。</summary>
        public bool CanSteer => !IsGameOver && Vehicle != VehicleKind.Taxi;

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
                        HalfWidth = IsShoppingStreet(a, b) ? MainStreetHalfWidth : IsMainRoad(a, b) ? LinkRoadHalfWidth : SideRoadHalfWidth,
                        Scale = view < 1e-6 ? 1 : e.Length / view,
                    });
                }
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

        /// <summary>把想去的点限制进走廊里：在走廊内就原样，否则贴着最近走廊的边。scale 是那里的"游戏米/画面米"。</summary>
        private MapPoint Constrain(MapPoint want, out double scale)
        {
            scale = 1;
            double nearestDist = double.MaxValue;
            double bestOver = double.MaxValue;
            MapPoint clamped = want;
            bool inside = false;
            foreach (Corridor c in Corridors())
            {
                MapPoint q = ClosestOnSegment(c.A, c.B, want);
                double d = Dist(q, want);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    scale = c.Scale;
                }
                double over = d - c.HalfWidth;
                if (over <= 0) inside = true;
                else if (over < bestOver)
                {
                    bestOver = over;
                    double k = c.HalfWidth / d;
                    clamped = new MapPoint(q.X + (want.X - q.X) * k, q.Y + (want.Y - q.Y) * k);
                }
            }
            return inside ? want : clamped;
        }

        private string NearestNodeTo(MapPoint p)
        {
            string best = PlayerNodeId;
            double bd = double.MaxValue;
            foreach (WorldNode n in City.Map.Nodes)
            {
                double d = Dist(p, City.ViewPosition(n.Id));
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
            MapPoint pos = Trip.Position;
            Trip = null;
            _free = pos;
            PlayerNodeId = NearestNodeTo(pos);
        }

        /// <summary>按方向走一步。返回是否真的走动了；minutes 是这一步折算出的游戏分钟。</summary>
        private bool StepManual(double scaledSeconds, out int minutes)
        {
            minutes = 0;
            VehicleSpec v = CurrentVehicle;
            double budget = v.VisualSpeed * scaledSeconds;
            MapPoint from = PlayerPosition;
            MapPoint want = new MapPoint(from.X + _steerX * budget, from.Y + _steerY * budget);
            MapPoint res = Constrain(want, out double scale);
            double moved = Dist(from, res);
            if (moved < 1e-6) return false;

            _free = res;
            PlayerNodeId = NearestNodeTo(res);
            HeadingX = (res.X - from.X) / moved;
            HeadingY = (res.Y - from.Y) / moved;

            _meterCarry += moved * scale;
            int due = (int)Math.Floor(_meterCarry / v.SpeedPerMinute + 1e-9);
            if (due > 0)
            {
                _meterCarry -= due * v.SpeedPerMinute;
                minutes = due;
                AdvanceClock(due);
            }
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
                double bestDist = EnterRadius;
                foreach (Place pl in City.Places)
                {
                    double d = Dist(p, DoorOf(pl));
                    if (d < bestDist)
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
