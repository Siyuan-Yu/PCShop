using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly HashSet<VehicleKind> _owned = new HashSet<VehicleKind>();
        private readonly List<string> _passed = new List<string>();

        /// <summary>当前出行方式。载具永远跟着玩家。</summary>
        public VehicleKind Vehicle { get; private set; } = VehicleKind.Walk;

        public bool TricycleImpounded { get; private set; }

        public bool Owns(VehicleKind kind) => kind == VehicleKind.Walk || kind == VehicleKind.Taxi || _owned.Contains(kind);

        public bool CanUse(VehicleKind kind)
        {
            if (!Owns(kind)) return false;
            if (kind == VehicleKind.Tricycle && TricycleImpounded) return false;
            return true;
        }

        /// <summary>换出行方式（路上不能换）。随身的货放不下时不能换成更小的。</summary>
        public bool SetVehicle(VehicleKind kind)
        {
            if (kind == Vehicle) return true;
            if (Trip != null) return Fail("在路上不能换交通方式。");
            if (!Owns(kind)) return Fail($"你还没有{Vehicles.Get(kind).Name}。");
            if (kind == VehicleKind.Tricycle && TricycleImpounded) return Fail("三轮车被交警扣了，去车行交 ¥500 赎回。");
            VehicleSpec v = Vehicles.Get(kind);
            if (CarriedKg > v.CargoCapacity * Backpack.KgPerSpace + 1e-9)
                return Fail($"身上的东西太重（{CarriedKg:0.#} 公斤），{v.Name}最多带 {v.CargoCapacity * Backpack.KgPerSpace:0.#} 公斤。");
            Vehicle = kind;
            ApplyCargoLimit();
            return true;
        }

        /// <summary>估算去某节点的路程（游戏米）和耗时（分钟）。</summary>
        public bool Estimate(string targetNode, out double meters, out int minutes, out Money fare)
        {
            meters = 0;
            minutes = 0;
            fare = Money.Zero;
            string from = Trip != null ? Trip.NextNode : PlayerNodeId;
            PathResult path = Pathfinder.FindPath(City.Map, from, targetNode);
            if (!path.Found) return false;
            meters = path.Distance;
            VehicleSpec v = CurrentVehicle;
            minutes = (int)System.Math.Ceiling(meters / v.SpeedPerMinute);
            if (v.Kind == VehicleKind.Taxi) fare = Vehicles.TaxiFare(meters);
            return true;
        }

        /// <summary>出发去某个地点，一直走到它门口的人行道上（到了再进门）。</summary>
        public bool GoTo(Place place) => GoTo(place.NodeId, StandOf(place));

        /// <summary>出发去某节点（路上可以随时改目的地，从下一个路口接着走）。打车先付车费。</summary>
        public bool GoTo(string targetNode) => GoTo(targetNode, null);

        private bool GoTo(string targetNode, MapPoint? end)
        {
            if (IsGameOver) return false;
            if (!City.Map.HasNode(targetNode)) return false;
            string leaveBlock = LeaveBlocker;
            if (leaveBlock != null) return Fail(leaveBlock);
            bool insideHere = CurrentPlace != null && CurrentPlace.NodeId == PlayerNodeId && (!end.HasValue || Dist(StandOf(CurrentPlace), end.Value) < 1e-6);
            if (Trip == null && targetNode == PlayerNodeId && (insideHere || (end.HasValue ? Dist(PlayerPosition, end.Value) < 1e-6 : !OffNode)))
            {
                if (CurrentPlace != null) Leave();
                return true;
            }

            MapPoint start;
            double scale;
            string from;
            if (Trip != null)
            {
                start = Trip.Position;
                scale = Trip.CurrentGameScale;
                from = Trip.NextNode;
            }
            else if (CurrentPlace != null && CurrentPlace.NodeId == PlayerNodeId)
            {
                start = StandOf(CurrentPlace);
                scale = 1;
                from = PlayerNodeId;
            }
            else if (OffNode)
            {
                start = PlayerPosition;
                Constrain(start, out scale);
                from = PlayerNodeId;
            }
            else
            {
                start = City.ViewPosition(PlayerNodeId);
                scale = 1;
                from = PlayerNodeId;
            }

            PathResult path = Pathfinder.FindPath(City.Map, from, targetNode);
            if (!path.Found) return Fail("到不了那里。");

            VehicleSpec v = CurrentVehicle;
            if (v.Kind == VehicleKind.Taxi)
            {
                Money fare = Vehicles.TaxiFare(path.Distance);
                if (!Ledger.TryDebit(Clock.Today, "交通", fare, "打车")) return Fail($"打车要 {fare}，钱不够。");
                Say($"打车 {fare}");
            }

            if (CurrentPlace != null) Leave();
            VehicleKind kind = v.Kind;
            Trip = new Trip(City, start, scale, path.NodeIds, v, n => LaneOf(n, kind), end);
            ClearEdge();
            return true;
        }

        /// <summary>取消出行，停在最近经过的节点。</summary>
        public void StopTrip()
        {
            Trip = null;
        }

        private int StepTrip(double scaledSeconds)
        {
            _passed.Clear();
            int minutes = Trip.Step(Trip.Vehicle.VisualSpeed * scaledSeconds, _passed);
            foreach (string n in _passed) PlayerNodeId = n;
            bool arrived = Trip.Arrived;
            string target = Trip.TargetNode;
            if (arrived)
            {
                MapPoint end = Trip.Position;
                PlayerNodeId = target;
                Trip = null;
                if (Dist(end, City.ViewPosition(target)) > 1e-3) _free = end;
            }
            if (minutes > 0) AdvanceClock(minutes);
            if (arrived && !IsGameOver && CurrentPlace == null && PlayerNodeId == target) TripArrived?.Invoke(target);
            return minutes;
        }

        private void StepPolice(double scaledSeconds)
        {
            bool visible = CurrentPlace == null && CurrentVehicle.Illegal;
            if (Police.Step(scaledSeconds, Clock.MinuteOfDay, PlayerPosition, visible))
                OnCaughtByPolice();
        }

        private void OnCaughtByPolice()
        {
            Ledger.ForceDebit(Clock.Today, "罚款", PoliceSystem.Fine, "三轮车违规上路");
            string body = $"三轮车不能在城里上路，罚款 {PoliceSystem.Fine}。";
            if (Random.Chance(PoliceSystem.ImpoundChance))
            {
                TricycleImpounded = true;
                Vehicle = VehicleKind.Walk;
                ApplyCargoLimit();
                if (Trip != null) Trip.ChangeVehicle(Vehicles.Walk);
                body += $"\n三轮车被扣了！货只能自己扛（走得慢）。去车行交 {PoliceSystem.RedeemFee} 可以赎回。";
            }
            Show("被交警抓到了", body);
        }

        // ---------- 地铁 ----------

        /// <summary>在地铁站里坐到另一个站。</summary>
        public bool TakeMetro()
        {
            if (CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Station) return Fail("要先进地铁站。");
            Place other = null;
            foreach (Place p in City.OfKind(PlaceKind.Station))
            {
                if (p != CurrentPlace) other = p;
            }
            if (other == null) return Fail("这条线只有一个站。");
            if (Clock.MinuteOfDay < 6 * 60 || Clock.MinuteOfDay >= 23 * 60) return Fail("地铁运营时间 6:00–23:00。");
            if (!Ledger.TryDebit(Clock.Today, "交通", MetroFare, "地铁")) return Fail("连地铁票都买不起了。");
            AdvanceClock(MetroMinutes);
            if (IsGameOver || CurrentPlace == Home) return true;
            PlayerNodeId = other.NodeId;
            ClearEdge();
            CurrentPlace = other;
            Say($"坐地铁到了{other.Name}（{MetroMinutes} 分钟，{MetroFare}）");
            return true;
        }

        // ---------- 车行 ----------

        public bool BuyVehicle(VehicleKind kind)
        {
            if (CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Dealer) return Fail("要在车行买。");
            if (!IsDealerOpen) return Fail("车行 9:00–19:00 营业。");
            VehicleSpec v = Vehicles.Get(kind);
            if (!v.Purchasable) return false;
            if (_owned.Contains(kind)) return Fail($"已经有{v.Name}了。");
            if (!Ledger.TryDebit(Clock.Today, "载具", v.Price, v.Name)) return Fail($"{v.Name}要 {v.Price}，钱不够。");
            _owned.Add(kind);
            Show("买到了" + v.Name, kind == VehicleKind.Tricycle
                ? "能拉很多货。但三轮车在城里不能上路——巡逻交警看到就会追，追上罚款，还可能扣车。"
                : kind == VehicleKind.Car ? "商务出行，拥有后魅力 +2；日养护费 ¥40（含油费、保险）。"
                : kind == VehicleKind.Van ? "合法运货，容量 80 格；日养护费 ¥60（含油费、保险）。" : "比走路快三倍，能带一点货。");
            return true;
        }

        public bool SellVehicle(VehicleKind kind)
        {
            if (IsGameOver || CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Dealer || !IsDealerOpen || !_owned.Contains(kind)) return false;
            if (kind == VehicleKind.Tricycle && TricycleImpounded) return Fail("先赎回扣押的车再出售。");
            if (Vehicle == kind && !SetVehicle(VehicleKind.Walk)) return false;
            _owned.Remove(kind);
            Ledger.Credit(Clock.Today, "载具出售", Vehicles.Get(kind).Price.Scale(.6), Vehicles.Get(kind).Name);
            return true;
        }

        public bool RedeemTricycle()
        {
            if (!TricycleImpounded) return false;
            if (CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Dealer) return Fail("要在车行办理。");
            if (!Ledger.TryDebit(Clock.Today, "罚款", PoliceSystem.RedeemFee, "赎回三轮车")) return Fail("钱不够赎车。");
            TricycleImpounded = false;
            Say("三轮车赎回来了。");
            return true;
        }
    }
}
