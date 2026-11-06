using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<VehicleKind> _owned = new List<VehicleKind>();
        private readonly List<string> _passed = new List<string>();

        /// <summary>当前出行方式。载具永远跟着玩家。</summary>
        public VehicleKind Vehicle { get; private set; } = VehicleKind.Walk;

        private int _impoundedTricycles, _impoundedEBikes;
        public bool TricycleImpounded => _impoundedTricycles > 0;

        public bool Owns(VehicleKind kind) => kind == VehicleKind.Walk || kind == VehicleKind.Taxi || _owned.Contains(kind);

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
            VehicleSpec v = Vehicles.Get(kind);
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

        /// <summary>出发去某个地点，一直走到它门口的人行道上（到了再进门）。</summary>
        public bool GoTo(Place place) => GoTo(place.NodeId, StandOf(place));

        /// <summary>出发去某节点（路上可以随时改目的地，从下一个路口接着走）。打车先付车费。</summary>
        public bool GoTo(string targetNode) => GoTo(targetNode, null);

        private bool GoTo(string targetNode, MapPoint? end)
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
                return true;
            }

            var planned = PlanTrip(targetNode, end, out double routeMeters);
            if (planned == null) return Fail("到不了那里。");
            VehicleSpec v = CurrentVehicle;
            if (v.Kind == VehicleKind.Taxi)
            {
                int blocked = BlockedDistrictOnTrip(planned);
                if(blocked>0)return Fail("通往"+DistrictName(blocked)+"的道路尚未开放，出租车暂不发车，也不收取车费。");
                Money fare = Vehicles.TaxiFare(routeMeters);
                if (!Ledger.TryDebit(Clock.Today, "交通", fare, "打车")) return Fail($"打车要 {fare}，钱不够。");
                Say($"打车 {fare}");
            }

            if (CurrentPlace != null) Leave();
            Trip = planned;
            ClearEdge();
            return true;
        }

        private Trip PlanTrip(string targetNode, MapPoint? end, out double meters)
        {
            meters = 0;
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
                scale = City.TravelDistanceScale;
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
                scale = City.TravelDistanceScale;
                from = PlayerNodeId;
            }

            PathResult path = Pathfinder.FindPath(City.Map, from, targetNode);
            if (!path.Found) return null;
            var vehicle = CurrentVehicle;
            var trip = new Trip(City, start, scale, path.NodeIds, vehicle, n => LaneOf(n, vehicle.Kind), end);
            meters = trip.TotalGameMeters;
            return trip;
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
            double remaining = CityTravelTiming.ScaledSecondsMeters(Trip.Vehicle, scaledSeconds, Time.SecondsPerMinuteAt1x);
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
            if (Random.Chance(PoliceSystem.ImpoundChance))
            {
                ImpoundVehicle(caught);
                Vehicle = VehicleKind.Walk;
                ApplyCargoLimit();
                if (Trip != null) Trip.ChangeVehicle(Vehicles.Walk);
                body += $"\n{name}被扣了！货只能自己扛（走得慢）。去车行交 {PoliceSystem.RedeemFee} 可以赎回。";
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
            SyncGarage();var v=_garage.FirstOrDefault(g=>MotorCatalog.Get(g.ModelId).Kind==kind);
            return v!=null&&SellMotor(v.Id);
        }

        private void ImpoundVehicle(VehicleKind kind,string vehicleId=null)
        {
            SyncGarage();
            var motor=vehicleId==null?_garage.FirstOrDefault(v=>v.Commuter&&MotorCatalog.Get(v.ModelId).Kind==kind):_garage.Find(v=>v.Id==vehicleId);
            if(motor==null||motor.Impounded)return;
            motor.Impounded=true;
            if (kind == VehicleKind.EBike) _impoundedEBikes++;
            else if (kind == VehicleKind.Tricycle) _impoundedTricycles++;
        }

        public bool RedeemVehicle(VehicleKind kind)
        {
            if (ImpoundedVehicleCount(kind) <= 0) return false;
            if (CurrentPlace == null || CurrentPlace.Kind != PlaceKind.Dealer) return Fail("要在车行办理。");
            string name = Vehicles.Get(kind).Name;
            if (!Ledger.TryDebit(Clock.Today, "罚款", PoliceSystem.RedeemFee, "赎回" + name)) return Fail("钱不够赎车。");
            if (kind == VehicleKind.EBike) _impoundedEBikes = System.Math.Max(0, _impoundedEBikes - 1);
            else if (kind == VehicleKind.Tricycle) _impoundedTricycles = System.Math.Max(0, _impoundedTricycles - 1);
            var motor=_garage.FirstOrDefault(v=>v.Impounded&&MotorCatalog.Get(v.ModelId).Kind==kind);if(motor!=null)motor.Impounded=false;
            Say(name + "赎回来了。");
            return true;
        }

        public bool RedeemTricycle() => RedeemVehicle(VehicleKind.Tricycle);
    }
}
