using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<string> _passed = new List<string>();

        /// <summary>当前出行方式。载具永远跟着玩家。</summary>
        public VehicleKind Vehicle { get; private set; } = VehicleKind.Walk;
        public VehicleSpec CurrentVehicle => CommuteVehicle(Vehicle);
        public VehicleSpec CommuteVehicle(VehicleKind kind) =>
            MotorCatalog.CommuteSpec(CommuteMotorModelId(kind)) ?? Vehicles.Get(kind);

        public bool TricycleImpounded => ImpoundedVehicleCount(VehicleKind.Tricycle) > 0;

        public bool Owns(VehicleKind kind) => kind == VehicleKind.Walk || kind == VehicleKind.Taxi || VehicleCount(kind) > 0;

        public bool CanUse(VehicleKind kind)
        {
            if(kind==VehicleKind.Van)return false;
            if (!Owns(kind)) return false;
            if (kind != VehicleKind.Walk && kind != VehicleKind.Taxi && !MotorUsable(CommuteMotor(kind))) return false;
            return true;
        }

        /// <summary>换出行方式（路上不能换）。随身的货放不下时不能换成更小的。</summary>
        public bool SetVehicle(VehicleKind kind)
        {
            if(kind==VehicleKind.Van)return Fail("货车仅供员工进货，不能作为玩家通勤车。");
            if (kind == Vehicle) return true;
            if (Trip != null) return Fail("在路上不能换交通方式。");
            if (!Owns(kind)) return Fail($"你还没有{Vehicles.Get(kind).Name}。");
            if (!CanUse(kind)) return Fail("没有可用的" + Vehicles.Get(kind).Name + "通勤车；请到车库设置，或等待员工归还、办理赎车。");
            VehicleSpec v = CommuteVehicle(kind);
            if (CarriedKg > v.CargoCapacity * Backpack.KgPerSpace + 1e-9)
                return Fail($"身上的东西太重（{CarriedKg:0.##} 千克），{v.Name}最多带 {v.CargoCapacity * Backpack.KgPerSpace:0.##} 千克。");
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
            var planned = PlanTrip(targetNode, null, out meters);
            if (planned == null) return false;
            minutes = planned.EstimateRemainingMinutes(Time.SecondsPerMinuteAt1x);
            if (CurrentVehicle.Kind == VehicleKind.Taxi) fare = Vehicles.TaxiFare(meters);
            return true;
        }

        public string CityDestinationTravelBlocker(Place target, out int minutes, out Money fare)
        {
            minutes = 0;
            fare = Money.Zero;
            if (target == null) return "请选择一个地点";
            if (CurrentPlace == target) return "已在此处";
            if (IsGameOver) return "当前无法出发";
            if (CityActivityExclusive) return "请先结束当前活动";
            if (LeaveBlocker is string leave) return leave;
            if (DistrictAccessBlocker(target) is string district) return district;
            if (RacingGarageEntryBlocker(target.Id) is string garageBlock) return garageBlock;

            var route = PlanTrip(target.NodeId, StandOf(target), out double meters);
            if (route == null) return "当前路线不可达";
            int blocked = BlockedDistrictOnTrip(route);
            if (blocked > 0) return "路线经过尚未开放的" + DistrictName(blocked) + "片区";

            minutes = Math.Max(1, route.EstimateRemainingMinutes(Time.SecondsPerMinuteAt1x));
            if (Vehicle != VehicleKind.Taxi) return null;
            fare = Vehicles.TaxiFare(meters);
            return Ledger.CanAfford(fare) ? null : $"打车需 {fare}，现金不足";
        }

        /// <summary>出发去某个地点，一直走到它门口的人行道上（到了再进门）。</summary>
        public bool GoTo(Place place) => GoTo(place.NodeId, StandOf(place));

        /// <summary>出发去某节点。途中改目的地时，从当前位置重新选路。打车先付车费。</summary>
        public bool GoTo(string targetNode) => GoTo(targetNode, null);

        private bool GoTo(string targetNode, MapPoint? end, bool requireUnlockedRoute = false)
        {
            if (IsWorking) return Fail("正在打工，收工后才能出行。");
            if (IsGameOver) return false;
            if (!City.Map.HasNode(targetNode)) return false;
            string leaveBlock = LeaveBlocker;
            if (leaveBlock != null) return Fail(leaveBlock);
            bool insideHere = CurrentPlace != null && CurrentPlace.NodeId == PlayerNodeId && (!end.HasValue || Dist(StandOf(CurrentPlace), end.Value) < 1e-6);
            if (Trip == null && targetNode == PlayerNodeId && (insideHere || (end.HasValue ? Dist(PlayerPosition, end.Value) < 1e-6 : !OffNode)))
            {
                if (CurrentPlace != null) Leave();
                Time.SetPaused(false);
                return true;
            }

            var planned = PlanTrip(targetNode, end, out double routeMeters);
            if (planned == null) return Fail("到不了那里。");
            VehicleSpec v = CurrentVehicle;
            if (requireUnlockedRoute || v.Kind == VehicleKind.Taxi)
            {
                int blocked = BlockedDistrictOnTrip(planned);
                if (blocked > 0) return Fail(v.Kind == VehicleKind.Taxi
                    ? "通往" + DistrictName(blocked) + "的道路尚未开放，出租车暂不发车，也不收取车费。"
                    : "沿途通往" + DistrictName(blocked) + "的道路尚未开放。");
            }
            if (v.Kind == VehicleKind.Taxi)
            {
                Money fare = Vehicles.TaxiFare(routeMeters);
                if (!Ledger.TryDebit(Clock.Today, "交通", fare, "打车")) return Fail($"打车要 {fare}，钱不够。");
                Say($"打车 {fare}");
            }

            if (CurrentPlace != null) Leave();
            Trip = planned;
            ClearEdge();
            Time.SetPaused(false);
            return true;
        }

        private Trip PlanTrip(string targetNode, MapPoint? end, out double meters)
        {
            meters = 0;
            MapPoint start;
            double scale;
            string from;
            string otherEnd = null;
            var vehicle = CurrentVehicle;
            if (Trip != null)
            {
                start = Trip.Position;
                scale = Trip.CurrentGameScale;
                from = Trip.NextNode;
                otherEnd = Trip.PreviousRoadNode;
                // Older rerouted trips only saved the road endpoint they were heading toward.
                if (otherEnd == null && City.Map.HasEdge(PlayerNodeId,from))
                {
                    var a = LaneOf(PlayerNodeId,vehicle.Kind);
                    var b = LaneOf(from,vehicle.Kind);
                    if (Dist(start,ClosestOnSegment(a,b,start)) < 1e-6) otherEnd = PlayerNodeId;
                }
            }
            else if (CurrentPlace != null && CurrentPlace.NodeId == PlayerNodeId)
            {
                start = StandOf(CurrentPlace);
                scale = City.TravelDistanceScale;
                from = PlayerNodeId;
            }
            else if (OffNode)
            {
                start = PlayerPosition;
                scale = NearestCorridorScale(start);
                from = PlayerNodeId;
            }
            else
            {
                start = City.ViewPosition(PlayerNodeId);
                scale = City.TravelDistanceScale;
                from = PlayerNodeId;
            }

            bool twoEnds = City.Map.HasEdge(from,otherEnd);
            var trip = BuildTrip(from,twoEnds ? otherEnd : null);
            if (twoEnds)
            {
                var reverse = BuildTrip(otherEnd,from);
                if (reverse != null && (trip == null || reverse.TotalGameMeters < trip.TotalGameMeters - 1e-6))
                    trip = reverse;
            }
            meters = trip?.TotalGameMeters ?? 0;
            return trip;

            Trip BuildTrip(string firstNode,string oppositeNode)
            {
                PathResult path = Pathfinder.FindPath(City.Map,firstNode,targetNode);
                return path.Found
                    ? new Trip(City,start,scale,path.NodeIds,vehicle,n=>LaneOf(n,vehicle.Kind),end,oppositeNode)
                    : null;
            }
        }

        /// <summary>取消出行，停在最近经过的节点。</summary>
        public void StopTrip()
        {
            if(Trip!=null) {var pos=Trip.Position;Trip=null;_free=pos;PlayerNodeId=City.NearestNode(pos.X,pos.Y);}
            CancelCityTravel();
        }

        private string StepTrip(double scaledSeconds)
        {
            _passed.Clear();
            double remaining = CityTravelTiming.PlayerScaledSecondsMeters(Trip.Vehicle, scaledSeconds, Time.SecondsPerMinuteAt1x);
            // Check each short segment so a fast vehicle cannot jump across a curved checkpoint road.
            while (remaining > 1e-8 && !Trip.Arrived)
            {
                var before = Trip.Position;
                double distance = Math.Min(remaining, .5 * Trip.CurrentGameScale);
                Trip.Step(distance, _passed); remaining -= distance;
                if (TryDistrictRoadStop(before, Trip.Position, out MapPoint stop, out int tier))
                {
                    Trip = null; _free = stop; PlayerNodeId = City.NearestNode(stop.X, stop.Y);
                    CancelCityTravel(); NotifyDistrictRoadStop(tier); return null;
                }
            }
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
            return arrived ? target : null;
        }

        private void StepPolice(double scaledSeconds)
        {
            bool visible = CurrentPlace == null && CurrentVehicle.Illegal;
            if (Police.Step(scaledSeconds, Clock.MinuteOfDay, PlayerPosition, visible))
                OnCaughtByPolice();
        }

        private void OnCaughtByPolice()
        {
            var caught = Vehicle;
            string name = Vehicles.Get(caught).Name;
            Ledger.ForceDebit(Clock.Today, "罚款", PoliceSystem.Fine, name + "违规上路");
            string body = $"{name}不能在城里上路，罚款 {PoliceSystem.Fine}。";
            if (Random.Chance(PoliceSystem.ImpoundChance) && ImpoundVehicle(caught))
            {
                Vehicle = VehicleKind.Walk;
                ApplyCargoLimit();
                if (Trip != null) Trip.ChangeVehicle(Vehicles.Walk);
                body += $"\n这台{name}已被扣押，满 {VehicleImpoundDays} 天免费归还。也可提前去车行交 {PoliceSystem.RedeemFee} 领回。";
            }
            Show("被交警抓到了", body);
        }

        // ---------- 地铁 ----------

        /// <summary>在地铁站里坐到另一个站。</summary>
        public bool TakeMetro() => TakeMetro(City.OfKind(PlaceKind.Station).FirstOrDefault(p=>p!=CurrentPlace)?.Id);
        public bool TakeMetro(string stationId)
        {
            if(CurrentPlace==null||CurrentPlace.Kind!=PlaceKind.Station)return Fail("要先进地铁站。");
            var origin=CurrentPlace;
            if(!City.HasPlace(stationId))return Fail("目的站不存在。");
            var destination=City.GetPlace(stationId);
            if(destination.Kind!=PlaceKind.Station||destination==origin)return Fail("请选择其他地铁站。");
            string districtBlock = DistrictAccessBlocker(destination);
            if(districtBlock!=null)return Fail(districtBlock);
            if(Clock.MinuteOfDay<360||Clock.MinuteOfDay>=1380)return Fail("地铁运营时间 6:00–23:00。");
            if(!Ledger.TryDebit(Clock.Today,"交通",MetroFare,origin.Name+"→"+destination.Name))return Fail("连地铁票都买不起了。");
            CancelCityTravel();Trip=null;
            Time.SetPaused(false);
            AdvanceClock(MetroMinutes);
            if(IsGameOver||CurrentPlace!=origin)return true;
            PlayerNodeId=destination.NodeId;ClearEdge();CurrentPlace=destination;
            Say("坐地铁到了"+destination.Name+"（"+MetroMinutes+"分钟，"+MetroFare+"）");return true;
        }

        // ---------- 车行 ----------

        public bool BuyVehicle(VehicleKind kind,bool notify=true)
        {
            return Vehicles.Get(kind).Purchasable && BuyMotor(MotorCatalog.Legacy(kind).Id);
        }

        public bool SellVehicle(VehicleKind kind)
        {
            var v=_garage.FirstOrDefault(g=>MotorCatalog.Get(g.ModelId).Kind==kind);
            return v!=null&&SellMotor(v.Id);
        }

        public const int VehicleImpoundDays = 3;

        public string VehicleReturnHint(VehicleKind kind)
        {
            var impounded = _garage.Where(v => v.Impounded && MotorCatalog.Get(v.ModelId).Kind == kind).ToArray();
            if (impounded.Length == 0) return null;
            long minutes = Math.Max(0, impounded.Min(v => v.ImpoundReleaseAt) - Clock.Now.TotalMinutes);
            long hours = (minutes + 59) / 60;
            string remaining = hours >= 24 ? $"{hours / 24} 天 {hours % 24} 小时" : hours > 0 ? $"{hours} 小时" : "片刻";
            return (impounded.Length > 1 ? "最早归还的一台" : Vehicles.Get(kind).Name)
                + $"还需 {remaining} 自动归还，也可到车行提前领回。";
        }

        private void UpdateImpoundedVehicles(long now)
        {
            foreach (var motor in _garage.Where(v => v.Impounded && v.ImpoundReleaseAt > 0
                && v.ImpoundReleaseAt <= now))
            {
                motor.Impounded = false;
                motor.ImpoundReleaseAt = 0;
                var model = MotorCatalog.Get(motor.ModelId);
                QueueCustomerFeedback("车辆已归还", $"{model.Name}的 {VehicleImpoundDays} 天扣押期已满，车辆已免费归还。"
                    + (motor.Commuter ? $"\n点击左下角「{Vehicles.Get(model.Kind).Name}」即可切换出行方式。" : ""));
            }
        }

        private bool ImpoundVehicle(VehicleKind kind, string vehicleId = null)
        {
            var motor = _garage.FirstOrDefault(v => MotorCatalog.Get(v.ModelId).Kind == kind
                && (vehicleId == null ? v.Commuter : v.Id == vehicleId));
            if (motor == null || motor.Impounded) return false;
            motor.Impounded = true;
            motor.ImpoundReleaseAt = Clock.Now.TotalMinutes + VehicleImpoundDays * GameTimestamp.MinutesPerDay;
            return true;
        }

        public bool RedeemVehicle(VehicleKind kind)
        {
            var motor = _garage.Where(v => v.Impounded && MotorCatalog.Get(v.ModelId).Kind == kind)
                .OrderBy(v => v.ImpoundReleaseAt).FirstOrDefault();
            if (motor == null) return false;
            if (CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Dealer) return Fail("要在车行办理。");
            string name = MotorCatalog.Get(motor.ModelId).Name;
            if (!Ledger.TryDebit(Clock.Today, "罚款", PoliceSystem.RedeemFee, "赎回" + name)) return Fail("钱不够赎车。");

            motor.Impounded = false;
            motor.ImpoundReleaseAt = 0;
            Say(name + "赎回来了。");
            return true;
        }

        public bool RedeemTricycle() => RedeemVehicle(VehicleKind.Tricycle);
    }
}
