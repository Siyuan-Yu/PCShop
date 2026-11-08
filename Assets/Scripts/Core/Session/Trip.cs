using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 一次出行。画面位置按交通方式移动，世界时钟由 GameSession 统一推进。
    /// 剩余时间和移动按实际路径米数与统一车辆步调估算，画面仅沿相同路段插值。
    /// </summary>
    public sealed class Trip
    {
        private readonly List<MapPoint> _points = new List<MapPoint>();
        private readonly List<string> _nodeAt = new List<string>();
        private readonly List<double> _viewLen = new List<double>();
        private readonly List<double> _gameLen = new List<double>();
        private readonly string _firstRoadOtherEnd;
        private int _segment;
        private double _segmentProgress;
        private double _viewMetersDone;
        internal CitySavedTrip SaveCityTrip() => new CitySavedTrip {xs=_points.ConvertAll(p=>p.X).ToArray(),ys=_points.ConvertAll(p=>p.Y).ToArray(),
            viewLengths=_viewLen.ToArray(),gameLengths=_gameLen.ToArray(),nodeAt=_nodeAt.ToArray(),target=TargetNode,segment=_segment,
            vehicle=(int)Vehicle.Kind,progress=_segmentProgress,viewDone=_viewMetersDone,firstRoadOtherEnd=_firstRoadOtherEnd};
        private Trip(CitySavedTrip d, CityLayout city)
        {
            TargetNode=d.target;Vehicle=Vehicles.Get((VehicleKind)d.vehicle);
            for(int i=0;i<d.xs.Length;i++)_points.Add(new MapPoint(d.xs[i],d.ys[i]));
            _viewLen.AddRange(d.viewLengths.Select(v=>Math.Max(1e-8,v)));
            // Recalibrate saved routes without changing their displayed position/progress.
            if (city.HasUniformRoadScale)
                _gameLen.AddRange(_viewLen.Select(length => length * city.TravelDistanceScale));
            else _gameLen.AddRange(d.gameLengths);
            _nodeAt.AddRange(d.nodeAt);
            _firstRoadOtherEnd = d.firstRoadOtherEnd;
            _segment=Math.Max(0,Math.Min(d.segment,_viewLen.Count));_segmentProgress=Arrived?0:Math.Min(_viewLen[_segment],Math.Max(0,d.progress));_viewMetersDone=Math.Max(0,d.viewDone);
            TotalViewMeters=_viewLen.Sum();TotalGameMeters=_gameLen.Sum();UpdatePosition();UpdateDirection();
        }
        internal static Trip RestoreCityTrip(CitySavedTrip d,CityLayout city)
        {
            if(d==null||!city.Map.HasNode(d.target)||d.xs==null||d.ys==null||d.xs.Length<1||d.xs.Length!=d.ys.Length
                ||d.viewLengths?.Length!=d.xs.Length-1||d.gameLengths?.Length!=d.xs.Length-1||d.nodeAt?.Length!=d.xs.Length)return null;
            if (double.IsNaN(d.progress) || double.IsInfinity(d.progress) || double.IsNaN(d.viewDone) || double.IsInfinity(d.viewDone)
                || d.xs.Any(v=>double.IsNaN(v)||double.IsInfinity(v)) || d.ys.Any(v=>double.IsNaN(v)||double.IsInfinity(v))
                || d.viewLengths.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<0) || d.gameLengths.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<0)) return null;
            return new Trip(d, city);
        }

        public VehicleSpec Vehicle { get; internal set; }
        public string TargetNode { get; }
        public double TotalGameMeters { get; }
        public double TotalViewMeters { get; }
        public MapPoint Position { get; private set; }
        public double DirX { get; private set; }
        public double DirY { get; private set; } = 1;
        public bool Arrived => _segment >= _viewLen.Count;

        /// <summary>当前路线接下来要经过的节点，最后一段进门时返回门前的路网节点。</summary>
        public string NextNode => Arrived || string.IsNullOrEmpty(_nodeAt[_segment + 1]) ? TargetNode : _nodeAt[_segment + 1];

        // An in-progress reroute starts between nodes. Keep the opposite end of that road
        // available if the player changes destination again before reaching a junction.
        internal string PreviousRoadNode
        {
            get
            {
                if (Arrived) return null;
                string node = _nodeAt[_segment];
                if (!string.IsNullOrEmpty(node)) return node;
                return _segment == 0 && !string.IsNullOrEmpty(_firstRoadOtherEnd) ? _firstRoadOtherEnd : null;
            }
        }

        public IReadOnlyList<MapPoint> Points => _points;

        /// <param name="start">起点的画面坐标（可以在两节点之间）。</param>
        /// <param name="startGameScale">起点到第一个节点这段的"游戏米/画面米"比例。</param>
        /// <param name="pointOf">路过每个节点时实际走的点（步行走人行道，不走路中间）；null 就走节点本身。</param>
        /// <param name="end">最后再走到这里（目的地门口）；null 就停在最后一个节点。</param>
        /// <param name="firstRoadOtherEnd">若在一段路中间起步，这段路上与 path[0] 相对的另一端。</param>
        internal Trip(CityLayout city, MapPoint start, double startGameScale, IReadOnlyList<string> path, VehicleSpec vehicle,
            Func<string, MapPoint> pointOf = null, MapPoint? end = null, string firstRoadOtherEnd = null)
        {
            Vehicle = vehicle;
            TargetNode = path[path.Count - 1];
            _firstRoadOtherEnd = firstRoadOtherEnd;
            _points.Add(start);
            _nodeAt.Add(null);

            for (int i = 0; i < path.Count; i++)
            {
                MapPoint p = pointOf != null ? pointOf(path[i]) : city.ViewPosition(path[i]);
                MapPoint prev = _points[_points.Count - 1];
                double view = Dist(prev, p);
                double game;
                if (city.HasUniformRoadScale) game = view * city.TravelDistanceScale;
                else if (i == 0) game = view * startGameScale;
                else game = CityTravelTiming.EdgeMeters(city, path[i - 1], path[i]);

                if (i == 0 && view < 1e-6)
                {
                    _nodeAt[0] = path[0];
                    continue;
                }
                _points.Add(p);
                _nodeAt.Add(path[i]);
                _viewLen.Add(Math.Max(1e-8, view));
                _gameLen.Add(game);
            }

            if (end.HasValue)
            {
                double view = Dist(_points[_points.Count - 1], end.Value);
                if (view > 1e-6)
                {
                    _points.Add(end.Value);
                    _nodeAt.Add(null);
                    _viewLen.Add(view);
                    _gameLen.Add(view * city.TravelDistanceScale);
                }
            }

            double tv = 0, tg = 0;
            for (int i = 0; i < _viewLen.Count; i++)
            {
                tv += _viewLen[i];
                tg += _gameLen[i];
            }
            TotalViewMeters = tv;
            TotalGameMeters = tg;
            Position = start;
            UpdateDirection();
        }

        /// <summary>当前这段路的"游戏米/画面米"比例。</summary>
        public double CurrentGameScale
        {
            get
            {
                if (Arrived) return 1;
                double v = _viewLen[_segment];
                return v < 1e-12 ? 1 : _gameLen[_segment] / v;
            }
        }

        /// <summary>
        /// 按真实路径距离推进。不推进时钟；经过的节点放进 passed。
        /// </summary>
        internal void Step(double gameMeters, List<string> passed)
        {
            double remaining = gameMeters;
            while (remaining > 0 && !Arrived)
            {
                double viewLeft = Math.Max(0, _viewLen[_segment] - _segmentProgress);
                double gameScale = CurrentGameScale;
                double segLeft = viewLeft * gameScale;
                if (remaining < segLeft)
                {
                    double advanced = remaining / gameScale;
                    _segmentProgress += advanced;
                    _viewMetersDone += advanced;
                    remaining = 0;
                }
                else
                {
                    remaining -= segLeft;
                    _viewMetersDone += viewLeft;
                    _segment++;
                    _segmentProgress = 0;
                    if (_nodeAt[_segment] != null) passed.Add(_nodeAt[_segment]);
                }
            }
            UpdatePosition();

        }

        /// <summary>换交通方式只改变后续移动速度，不重算已流逝的世界时间。</summary>
        internal void ChangeVehicle(VehicleSpec v) => Vehicle = v;

        public double RemainingGameMeters
        {
            get
            {
                if (Arrived) return 0;
                double value = Math.Max(0, _viewLen[_segment] - _segmentProgress) * CurrentGameScale;
                for (int i = _segment + 1; i < _gameLen.Count; i++) value += _gameLen[i];
                return value;
            }
        }
        public int EstimateRemainingMinutes(double secondsPerMinuteAt1x) => CityTravelTiming.PlayerMinutes(RemainingGameMeters, Vehicle);
        public int EstimatedMinutes => EstimateRemainingMinutes(TimeController.DefaultSecondsPerMinute);

        private void UpdatePosition()
        {
            if (Arrived)
            {
                Position = _points[_points.Count - 1];
                return;
            }
            MapPoint a = _points[_segment];
            MapPoint b = _points[_segment + 1];
            double len = _viewLen[_segment];
            double t = len < 1e-6 ? 1 : _segmentProgress / len;
            Position = new MapPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            UpdateDirection();
        }

        private void UpdateDirection()
        {
            if (Arrived || _points.Count < 2) return;
            MapPoint a = _points[_segment];
            MapPoint b = _points[_segment + 1];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-6) return;
            DirX = dx / l;
            DirY = dy / l;
        }

        internal static double Dist(MapPoint a, MapPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double EdgeLength(WorldMap map, string a, string b)
        {
            foreach (WorldEdge e in map.NeighborsOf(a))
            {
                if (e.To == b) return e.Length;
            }
            throw new ArgumentException($"没有边：{a} - {b}");
        }
    }
}
