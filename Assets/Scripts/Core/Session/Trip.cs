using System;
using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 一次出行。画面上按固定画面速度移动；时钟按这段路的游戏内路程 ÷ 交通速度同步推进。
    /// 被压缩的连接路（边的实际长度大于画面长度）在画面上走得快、但时间照算。
    /// </summary>
    public sealed class Trip
    {
        private readonly List<MapPoint> _points = new List<MapPoint>();
        private readonly List<string> _nodeAt = new List<string>();
        private readonly List<double> _viewLen = new List<double>();
        private readonly List<double> _gameLen = new List<double>();
        private int _segment;
        private double _segmentProgress;
        private double _gameMetersDone;
        private double _minutesOwed;

        public VehicleSpec Vehicle { get; internal set; }
        public string TargetNode { get; }
        public double TotalGameMeters { get; }
        public double TotalViewMeters { get; }
        public MapPoint Position { get; private set; }
        public double DirX { get; private set; }
        public double DirY { get; private set; } = 1;
        public bool Arrived => _segment >= _viewLen.Count;

        /// <summary>下一个要经过的节点（重新选目的地时从这里接着走）。</summary>
        public string NextNode => Arrived ? TargetNode : _nodeAt[_segment + 1];

        public IReadOnlyList<MapPoint> Points => _points;

        /// <param name="start">起点的画面坐标（可以在两节点之间）。</param>
        /// <param name="startGameScale">起点到第一个节点这段的"游戏米/画面米"比例。</param>
        internal Trip(CityLayout city, MapPoint start, double startGameScale, IReadOnlyList<string> path, VehicleSpec vehicle)
        {
            Vehicle = vehicle;
            TargetNode = path[path.Count - 1];
            _points.Add(start);
            _nodeAt.Add(null);

            for (int i = 0; i < path.Count; i++)
            {
                MapPoint p = city.ViewPosition(path[i]);
                MapPoint prev = _points[_points.Count - 1];
                double view = Dist(prev, p);
                double game;
                if (i == 0) game = view * startGameScale;
                else game = EdgeLength(city.Map, path[i - 1], path[i]);

                if (i == 0 && view < 1e-6)
                {
                    _nodeAt[0] = path[0];
                    continue;
                }
                _points.Add(p);
                _nodeAt.Add(path[i]);
                _viewLen.Add(view);
                _gameLen.Add(game);
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
                return v < 1e-6 ? 1 : _gameLen[_segment] / v;
            }
        }

        /// <summary>
        /// 按画面距离推进。返回这一步应推进的整分钟数；经过的节点放进 passed。
        /// </summary>
        internal int Step(double viewMeters, List<string> passed)
        {
            double remaining = viewMeters;
            while (remaining > 0 && !Arrived)
            {
                double segLeft = _viewLen[_segment] - _segmentProgress;
                double scale = CurrentGameScale;
                if (remaining < segLeft)
                {
                    _segmentProgress += remaining;
                    _gameMetersDone += remaining * scale;
                    remaining = 0;
                }
                else
                {
                    remaining -= segLeft;
                    _gameMetersDone += segLeft * scale;
                    _segment++;
                    _segmentProgress = 0;
                    if (_nodeAt[_segment] != null) passed.Add(_nodeAt[_segment]);
                }
            }
            UpdatePosition();

            double totalMinutes = _gameMetersDone / Vehicle.SpeedPerMinute;
            if (Arrived) totalMinutes = Math.Ceiling(totalMinutes - 1e-9);
            int due = (int)Math.Floor(totalMinutes - _minutesOwed + 1e-9);
            if (due < 0) due = 0;
            _minutesOwed += due;
            return due;
        }

        /// <summary>中途换交通方式（例如三轮车被扣）：已走过的部分按原速度算完，剩下的按新速度。</summary>
        internal void ChangeVehicle(VehicleSpec v)
        {
            double doneMinutes = _gameMetersDone / Vehicle.SpeedPerMinute;
            Vehicle = v;
            _gameMetersDone = doneMinutes * v.SpeedPerMinute;
        }

        public int EstimatedMinutes => (int)Math.Ceiling(TotalGameMeters / Vehicle.SpeedPerMinute);

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
