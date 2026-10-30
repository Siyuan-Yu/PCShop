using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum OfficerState
    {
        OffDuty,
        Patrol,
        Chase,
        Cooldown,
    }

    /// <summary>
    /// 交警：沿巡逻路线来回走，前方有扇形视野。看到骑三轮车的玩家就追，追上罚款，可能扣车。
    /// 坐标为画面坐标，按真实秒推进（这是全游戏唯一的实时追逐）。
    /// </summary>
    public sealed class Officer
    {
        public const double PatrolSpeed = 2;
        public const double ChaseSpeed = 8.25;
        public const double SightRange = 13;
        public const double SightHalfAngleDeg = 40;
        public const double CatchDistance = 1.3;
        public const double GiveUpDistance = 30;
        public const double MaxChaseSeconds = 7;
        public const double CooldownSeconds = 20;

        private readonly List<MapPoint> _route = new List<MapPoint>();
        private int _leg;
        private bool _forward = true;
        private double _legProgress;
        private double _timer;

        public string RouteId { get; }
        public OfficerState State { get; internal set; } = OfficerState.OffDuty;
        public MapPoint Position { get; private set; }
        public double DirX { get; private set; } = 1;
        public double DirY { get; private set; }

        /// <summary>今天是否出勤（每天随机）。</summary>
        public bool OnDutyToday { get; set; }

        internal Officer(CityLayout city, PatrolRoute route)
        {
            RouteId = route.Id;
            foreach (string n in route.NodeIds) _route.Add(city.ViewPosition(n));
            Position = _route[0];
        }

        /// <summary>真实秒推进。返回 true 表示这一步追上了玩家。</summary>
        internal bool Step(double dt, bool activeHours, MapPoint player, bool playerVisible)
        {
            if (!OnDutyToday || !activeHours)
            {
                State = OfficerState.OffDuty;
                return false;
            }
            if (State == OfficerState.OffDuty) State = OfficerState.Patrol;

            switch (State)
            {
                case OfficerState.Patrol:
                    Patrol(dt);
                    if (playerVisible && CanSee(player))
                    {
                        State = OfficerState.Chase;
                        _timer = 0;
                    }
                    return false;

                case OfficerState.Chase:
                    if (!playerVisible)
                    {
                        StartCooldown();
                        return false;
                    }
                    _timer += dt;
                    double dx = player.X - Position.X, dy = player.Y - Position.Y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d <= CatchDistance)
                    {
                        StartCooldown();
                        return true;
                    }
                    if (d > GiveUpDistance || _timer > MaxChaseSeconds)
                    {
                        StartCooldown();
                        return false;
                    }
                    double stepLen = Math.Min(d, ChaseSpeed * dt);
                    DirX = dx / d;
                    DirY = dy / d;
                    Position = new MapPoint(Position.X + DirX * stepLen, Position.Y + DirY * stepLen);
                    if (d - stepLen <= CatchDistance)
                    {
                        StartCooldown();
                        return true;
                    }
                    return false;

                case OfficerState.Cooldown:
                    _timer -= dt;
                    ReturnToRoute(dt);
                    if (_timer <= 0) State = OfficerState.Patrol;
                    return false;
            }
            return false;
        }

        /// <summary>玩家是否在视野扇形内。</summary>
        public bool CanSee(MapPoint p)
        {
            double dx = p.X - Position.X, dy = p.Y - Position.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > SightRange) return false;
            if (d < 1e-6) return true;
            double cos = (dx * DirX + dy * DirY) / d;
            return cos >= Math.Cos(SightHalfAngleDeg * Math.PI / 180.0);
        }

        internal void ResetToStart()
        {
            _leg = 0;
            _forward = true;
            _legProgress = 0;
            Position = _route[0];
            State = OfficerState.OffDuty;
        }

        private void StartCooldown()
        {
            State = OfficerState.Cooldown;
            _timer = CooldownSeconds;
        }

        private void Patrol(double dt)
        {
            double remaining = PatrolSpeed * dt;
            int guard = 0;
            while (remaining > 0 && guard++ < 16)
            {
                int from = _forward ? _leg : _leg + 1;
                int to = _forward ? _leg + 1 : _leg;
                MapPoint a = _route[from], b = _route[to];
                double len = Trip.Dist(a, b);
                double left = len - _legProgress;
                if (remaining < left)
                {
                    _legProgress += remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= left;
                    _legProgress = 0;
                    if (_forward)
                    {
                        if (_leg + 1 >= _route.Count - 1) _forward = false;
                        else _leg++;
                    }
                    else
                    {
                        if (_leg == 0) _forward = true;
                        else _leg--;
                    }
                }
                from = _forward ? _leg : _leg + 1;
                to = _forward ? _leg + 1 : _leg;
                a = _route[from];
                b = _route[to];
                len = Trip.Dist(a, b);
                double t = len < 1e-6 ? 0 : _legProgress / len;
                Position = new MapPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                if (len > 1e-6)
                {
                    DirX = (b.X - a.X) / len;
                    DirY = (b.Y - a.Y) / len;
                }
            }
        }

        private void ReturnToRoute(double dt)
        {
            int from = _forward ? _leg : _leg + 1;
            int to = _forward ? _leg + 1 : _leg;
            MapPoint a = _route[from], b = _route[to];
            double len = Trip.Dist(a, b);
            double t = len < 1e-6 ? 0 : _legProgress / len;
            var target = new MapPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            double dx = target.X - Position.X, dy = target.Y - Position.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.05) return;
            double s = Math.Min(d, PatrolSpeed * 1.5 * dt);
            DirX = dx / d;
            DirY = dy / d;
            Position = new MapPoint(Position.X + DirX * s, Position.Y + DirY * s);
        }
    }

    public sealed class PoliceSystem
    {
        public const int ActiveFromMinute = 8 * 60;
        public const int ActiveToMinute = 20 * 60;
        public const double DutyChance = 0.6;
        public static readonly Money Fine = Money.FromYuan(200);
        public static readonly Money RedeemFee = Money.FromYuan(500);
        public const double ImpoundChance = 0.3;

        private readonly List<Officer> _officers = new List<Officer>();

        public IReadOnlyList<Officer> Officers => _officers;

        internal PoliceSystem(CityLayout city)
        {
            foreach (PatrolRoute r in city.Patrols) _officers.Add(new Officer(city, r));
        }

        internal void RollDuty(GameRandom rng)
        {
            foreach (Officer o in _officers)
            {
                o.OnDutyToday = rng.Chance(DutyChance);
                o.ResetToStart();
            }
        }

        public bool AnyChasing
        {
            get
            {
                foreach (Officer o in _officers)
                {
                    if (o.State == OfficerState.Chase) return true;
                }
                return false;
            }
        }

        /// <summary>返回是否有人追上了玩家。</summary>
        internal bool Step(double dt, int minuteOfDay, MapPoint player, bool playerVisible)
        {
            bool active = minuteOfDay >= ActiveFromMinute && minuteOfDay < ActiveToMinute;
            bool caught = false;
            foreach (Officer o in _officers)
            {
                if (o.Step(dt, active, player, playerVisible)) caught = true;
            }
            return caught;
        }
    }
}
