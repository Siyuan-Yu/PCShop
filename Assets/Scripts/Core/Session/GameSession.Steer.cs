using System;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 手动走路（WASD）。玩家沿着街道路网走：在路口按方向键选下一条路，在路中间可以停下，也可以掉头。
    /// 时间照旧按走过的路程折算；停下不动时按倍速走。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>走到离某地点入口这么近（画面米）就能按 E 进去。</summary>
        public const double EnterRadius = 3.0;

        private const double TurnMinDot = 0.35;
        private const double KeepGoingMinDot = 0.3;

        private string _edgeA, _edgeB;
        private double _edgeT, _edgeLen, _edgeGameScale = 1;
        private bool _steering;
        private double _steerX, _steerY;
        private double _meterCarry;
        private bool _manualMoving;

        /// <summary>现在是否不在路口、停在两个路口之间。</summary>
        public bool OffNode => _edgeA != null;

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

        public MapPoint PositionOnEdge
        {
            get
            {
                MapPoint a = City.ViewPosition(_edgeA), b = City.ViewPosition(_edgeB);
                return new MapPoint(a.X + (b.X - a.X) * _edgeT, a.Y + (b.Y - a.Y) * _edgeT);
            }
        }

        private string NearestEnd() => _edgeT < 0.5 ? _edgeA : _edgeB;

        private void ClearEdge()
        {
            _edgeA = _edgeB = null;
            _edgeT = 0;
            _edgeGameScale = 1;
        }

        private double GameEdgeLength(string a, string b)
        {
            foreach (WorldEdge e in City.Map.NeighborsOf(a))
                if (e.To == b) return e.Length;
            return -1;
        }

        private static double Dist(MapPoint a, MapPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void BeginEdge(string from, string to, double t)
        {
            _edgeA = from;
            _edgeB = to;
            _edgeLen = Dist(City.ViewPosition(from), City.ViewPosition(to));
            double game = GameEdgeLength(from, to);
            _edgeGameScale = _edgeLen < 1e-6 || game < 0 ? 1 : game / _edgeLen;
            _edgeT = t;
        }

        /// <summary>手动接管：把正在进行的自动前往转成"在某条路上"的状态。</summary>
        private void BreakTripToEdge()
        {
            Trip trip = Trip;
            MapPoint pos = trip.Position;
            string behind = PlayerNodeId, ahead = trip.NextNode;
            Trip = null;
            if (behind != ahead && GameEdgeLength(behind, ahead) >= 0)
            {
                BeginEdge(behind, ahead, 0);
                _edgeT = _edgeLen < 1e-6 ? 0 : Math.Max(0, Math.Min(1, Dist(City.ViewPosition(behind), pos) / _edgeLen));
                return;
            }
            PlayerNodeId = ahead;
            ClearEdge();
        }

        private string PickEdge(string node)
        {
            MapPoint a = City.ViewPosition(node);
            string best = null;
            double bestDot = TurnMinDot;
            foreach (WorldEdge e in City.Map.NeighborsOf(node))
            {
                MapPoint b = City.ViewPosition(e.To);
                double len = Dist(a, b);
                if (len < 1e-6) continue;
                double dot = ((b.X - a.X) * _steerX + (b.Y - a.Y) * _steerY) / len;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = e.To;
                }
            }
            return best;
        }

        /// <summary>按方向走一步。返回是否真的走动了；minutes 是这一步折算出的游戏分钟。</summary>
        private bool StepManual(double scaledSeconds, out int minutes)
        {
            minutes = 0;
            VehicleSpec v = CurrentVehicle;
            double budget = v.VisualSpeed * scaledSeconds;
            double moved = 0, meters = 0;
            while (budget > 1e-9)
            {
                if (_edgeA == null)
                {
                    string next = PickEdge(PlayerNodeId);
                    if (next == null) break;
                    BeginEdge(PlayerNodeId, next, 0);
                }
                MapPoint a = City.ViewPosition(_edgeA), b = City.ViewPosition(_edgeB);
                if (_edgeLen < 1e-6)
                {
                    PlayerNodeId = _edgeB;
                    ClearEdge();
                    continue;
                }
                double ex = (b.X - a.X) / _edgeLen, ey = (b.Y - a.Y) / _edgeLen;
                double dot = ex * _steerX + ey * _steerY;
                int sign = dot > KeepGoingMinDot ? 1 : dot < -KeepGoingMinDot ? -1 : 0;
                if (sign == 0) break;

                double pos = _edgeT * _edgeLen;
                double room = sign > 0 ? _edgeLen - pos : pos;
                double m = Math.Min(budget, room);
                pos += sign * m;
                budget -= m;
                moved += m;
                meters += m * _edgeGameScale;
                _edgeT = pos / _edgeLen;
                HeadingX = sign * ex;
                HeadingY = sign * ey;
                if (room - m <= 1e-9)
                {
                    PlayerNodeId = sign > 0 ? _edgeB : _edgeA;
                    ClearEdge();
                }
            }

            if (moved <= 1e-9) return false;
            _meterCarry += meters;
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
                    double d = Dist(p, City.ViewPosition(pl.NodeId));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = pl;
                    }
                }
                return best;
            }
        }

        /// <summary>进入附近的地点（会先走到入口）。</summary>
        public bool EnterNearby()
        {
            Place p = NearbyPlace;
            if (p == null) return Fail("附近没有能进的地方。走到门口再按 E。");
            PlayerNodeId = p.NodeId;
            ClearEdge();
            return Enter(p);
        }
    }
}
