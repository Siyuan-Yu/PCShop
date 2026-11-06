using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class RegionalGarageLease
    {
        public string PlaceId;
        public int OwnedSpaces,RentedSpaces,NextRentDay;
        public int Capacity => OwnedSpaces+RentedSpaces;
        public RegionalGarageLease Copy() => (RegionalGarageLease)MemberwiseClone();
    }

    public sealed class RegionalGarage
    {
        public string Id, TrackId, Name;
        public int Tier;
        public long RentYuan => new long[] { 50, 150, 400, 1000 }[Tier];
        public long PriceYuan => new long[] { 5000, 15000, 40000, 100000 }[Tier];
    }

    public static class RegionalGarages
    {
        public static readonly RegionalGarage[] All = RaceTracks.All.Select(t => new RegionalGarage
        { Id = "garage_" + t.Tier, TrackId = t.Id, Tier = t.Tier, Name = GameSession.DistrictName(t.Tier) + "赛场车库" }).ToArray();
        public static RegionalGarage Get(string id) => All.FirstOrDefault(g => g.Id == id);
        public static RegionalGarage ForTrack(string id) => All.FirstOrDefault(g => g.TrackId == id);
    }

    public sealed partial class GameSession
    {
        private readonly List<RegionalGarageLease> _regionalGarages = new List<RegionalGarageLease>();
        public IReadOnlyList<RegionalGarageLease> GarageLeases => _regionalGarages.Select(g => g.Copy()).ToArray();
        public RegionalGarageLease GarageLease(string id) => _regionalGarages.FirstOrDefault(g => g.PlaceId == id)?.Copy();
        public bool HasGarage(string id) => GarageCapacity(id)>0;
        public int GarageCapacity(string id) => _regionalGarages.FirstOrDefault(g=>g.PlaceId==id)?.Capacity??0;
        public int ParkedMotorCount(string id) => _garage.Count(v => MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind)&&!v.Commuter && v.GarageId == id);
        public int GarageFreeSpaces(string id) => Math.Max(0,GarageCapacity(id)-ParkedMotorCount(id));
        public int ReturnableGarageSpaces(string id) => Math.Min(GarageLease(id)?.RentedSpaces??0,GarageFreeSpaces(id));
        public Money GaragePropertyValue => Money.FromYuan(_regionalGarages.Sum(g => RegionalGarages.Get(g.PlaceId).PriceYuan*g.OwnedSpaces));
        public Money MonthlyGarageRent => Money.FromYuan(_regionalGarages.Sum(g => RegionalGarages.Get(g.PlaceId).RentYuan*g.RentedSpaces));
        public Money GarageSpaceQuote(string id,bool buy,int count)
        {
            var g=RegionalGarages.Get(id);if(g==null||count<=0)return Money.Zero;
            var price=Money.FromYuan((buy?g.PriceYuan:g.RentYuan)*(long)count);var lease=GarageLease(id);
            return buy||lease==null||lease.RentedSpaces==0?price:price.Scale(Math.Max(1,lease.NextRentDay-Clock.Today.TotalDays)/30.0);
        }

        public string GarageContractBlocker(string id)
        {
            var g = RegionalGarages.Get(id);
            if (g == null || !City.HasPlace(id)) return "车库不存在";
            if (IsGameOver || Racing || IsWorking || Trip != null) return "请先结束当前活动";
            if (DistrictAccessTier < g.Tier) return "先解锁" + DistrictName(g.Tier) + "片区";
            if (CurrentPlace?.Id != id) return "请到车库建筑旁按 F 办理";
            return null;
        }

        public bool AcquireGarage(string id, bool buy,int count=1)
        {
            string why = GarageContractBlocker(id); if (why != null) return Fail(why);
            var g = RegionalGarages.Get(id); var lease = _regionalGarages.FirstOrDefault(x => x.PlaceId == id);
            if(count<=0||(long)count+(lease?.Capacity??0)>int.MaxValue)return Fail("请输入有效的车位数量");
            var price = GarageSpaceQuote(id,buy,count);
            var attribution = buy ? LedgerAttribution.ForAsset(id, transactionKind: "车位购置")
                : new LedgerAttribution(LedgerBusinessKind.Personal, propertyId: id, transactionKind: "车位租金");
            if (!Ledger.TryDebit(Clock.Today, buy ? "购买车位" : "车位租金", price, g.Name + " · "+count+"个车位"+(buy ? "全款购买" : "本期租金"), attribution)) return Fail("现金不足");
            if (lease == null) { lease = new RegionalGarageLease { PlaceId = id }; _regionalGarages.Add(lease); }
            if(buy)lease.OwnedSpaces+=count;
            else {if(lease.RentedSpaces==0)lease.NextRentDay=Clock.Today.TotalDays+30;lease.RentedSpaces+=count;}
            Say(g.Name+"已"+(buy?"购买":"租用")+count+"个车位；现有"+lease.Capacity+"位，需要时可继续增加。"); return true;
        }

        public bool EndGarageLease(string id,int count=0)
        {
            string why = GarageContractBlocker(id); if (why != null) return Fail(why);
            var lease = _regionalGarages.FirstOrDefault(g => g.PlaceId == id);
            if (lease == null || lease.RentedSpaces==0) return false;
            if(count==0)count=ReturnableGarageSpaces(id);
            if(count<=0||count>ReturnableGarageSpaces(id))return Fail("只能退租空闲车位，请先转移车辆或增加自有车位");
            lease.RentedSpaces-=count;if(lease.Capacity==0)_regionalGarages.Remove(lease);
            Say("已退租"+count+"个空闲车位，已付租金不退。");return true;
        }

        private void SettleGarageRent(GameDate today)
        {
            foreach (var lease in _regionalGarages.Where(g => g.RentedSpaces>0))
                while (today.TotalDays >= lease.NextRentDay)
                {
                    var g = RegionalGarages.Get(lease.PlaceId);
                    Ledger.ForceDebit(today, "车位租金", Money.FromYuan(g.RentYuan*lease.RentedSpaces), g.Name + " · "+lease.RentedSpaces+"个车位续租30天",
                        new LedgerAttribution(LedgerBusinessKind.Personal, propertyId: g.Id, transactionKind: "车位租金"));
                    lease.NextRentDay += 30;
                }
        }
        private Money GarageRentDueWithin(int days) => Money.FromYuan(_regionalGarages.Where(g => g.RentedSpaces>0)
            .Sum(g => Enumerable.Range(1, days).Count(d => Clock.Today.TotalDays + d >= g.NextRentDay && (Clock.Today.TotalDays + d - g.NextRentDay) % 30 == 0) * RegionalGarages.Get(g.PlaceId).RentYuan*g.RentedSpaces));

        public GarageVehicle CommuteMotor(VehicleKind kind)
        { SyncGarage(); return kind==VehicleKind.Van?null:_garage.FirstOrDefault(v => v.Commuter && MotorCatalog.Get(v.ModelId).Kind == kind)?.Copy(); }
        private bool MotorBusy(GarageVehicle v) => v != null && (_raceVehicle == v.Id || _employeeTrips.Any(t => !t.Returned && t.VehicleId == v.Id));
        private bool MotorUsable(GarageVehicle v) => v != null && !v.Impounded && !MotorBusy(v);
        private GarageVehicle EmployeeTransportMotor(VehicleKind kind)
        {
            SyncGarage();
            return _garage.Where(v => MotorCatalog.Get(v.ModelId).Kind == kind && MotorUsable(v) && !(v.Commuter && Vehicle == kind))
                .OrderBy(v => v.Commuter).FirstOrDefault();
        }
        public string MotorLocationText(GarageVehicle v)
        {
            if (v == null) return "暂无车辆";
            string place = MotorCatalog.Get(v.ModelId).Kind==VehicleKind.Van?"员工进货专用 · 免车位":v.Commuter ? "通勤车 · 不占车位" : RegionalGarages.Get(v.GarageId)?.Name ?? "待入库";
            return place + (v.Impounded ? " · 被扣押" : MotorBusy(v) ? " · 使用中" : "");
        }

        private string MotorSpaceBlocker(string garageId)
        {
            var g = RegionalGarages.Get(garageId);
            if (g == null || !HasGarage(garageId)) return "请先在目标片区车库租用或购买车位";
            if (DistrictAccessTier < g.Tier) return "先解锁" + DistrictName(g.Tier) + "片区";
            return GarageFreeSpaces(garageId) <= 0 ? "已持有车位已满，请到车库增加车位或先移走车辆" : null;
        }

        public string MotorStorageBlocker(string modelId, bool forCommute, string garageId)
        {
            var model = MotorCatalog.Get(modelId); if (model == null) return "车型不存在";
            if (IsGameOver || Racing || Trip != null || IsWorking) return "请先结束当前活动";
            if(model.Kind==VehicleKind.Van)return null;
            SyncGarage();
            var old = _garage.FirstOrDefault(v => v.Commuter && MotorCatalog.Get(v.ModelId).Kind == model.Kind);
            if (forCommute && old == null) return null;
            if (forCommute && !MotorUsable(old)) return "原通勤车正在使用或被扣押，不能替换";
            return MotorSpaceBlocker(garageId);
        }

        private void PlaceAcquiredMotor(GarageVehicle v, bool forCommute, string garageId)
        {
            var model = MotorCatalog.Get(v.ModelId);
            if(model.Kind==VehicleKind.Van){v.Commuter=false;v.GarageId=null;v.Impounded=false;_garage.Add(v);_owned.Add(model.Kind);return;}
            var old = _garage.FirstOrDefault(x => x.Commuter && MotorCatalog.Get(x.ModelId).Kind == model.Kind);
            if (forCommute && old != null) { old.Commuter = false; old.GarageId = garageId; }
            v.Commuter = forCommute; v.GarageId = forCommute ? null : garageId; v.Impounded = false;
            _garage.Add(v); _owned.Add(model.Kind);
        }

        public bool SetMotorCommuter(string id)
        {
            SyncGarage(); var v = _garage.Find(x => x.Id == id);
            if (v == null || v.Commuter) return false;
            if(!MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind))return Fail("货车仅供员工进货，不可设为通勤车。");
            string why = GarageContractBlocker(v.GarageId); if (why != null) return Fail(why);
            if (!MotorUsable(v)) return Fail("车辆正在使用或被扣押");
            var kind = MotorCatalog.Get(v.ModelId).Kind;
            var old = _garage.FirstOrDefault(x => x.Commuter && MotorCatalog.Get(x.ModelId).Kind == kind);
            if (old != null && !MotorUsable(old)) return Fail("原通勤车正在使用或被扣押");
            if (old != null) { old.Commuter = false; old.GarageId = v.GarageId; }
            v.Commuter = true; v.GarageId = null;
            Say(MotorCatalog.Get(v.ModelId).Name + "已设为" + Vehicles.Get(kind).Name + "通勤车，不再用于比赛或练习。"); return true;
        }

        public bool ParkCommuteMotor(string id, string garageId)
        {
            SyncGarage(); var v = _garage.Find(x => x.Id == id);
            string why = GarageContractBlocker(garageId) ?? MotorSpaceBlocker(garageId); if (why != null) return Fail(why);
            if (v == null || !v.Commuter || !MotorUsable(v)) return Fail("通勤车正在使用或被扣押，不能入库");
            var kind = MotorCatalog.Get(v.ModelId).Kind;
            if (Vehicle == kind && !SetVehicle(VehicleKind.Walk)) return false;
            v.Commuter = false; v.GarageId = garageId;
            Say(MotorCatalog.Get(v.ModelId).Name + "已入库；" + Vehicles.Get(kind).Name + "暂不再提供通勤。"); return true;
        }

        public Money MotorTransferFee(string motorId, string targetId)
        {
            var v = _garage.FirstOrDefault(x => x.Id == motorId); var source = RegionalGarages.Get(v?.GarageId); var target = RegionalGarages.Get(targetId);
            return source == null || target == null ? Money.Zero : Money.FromYuan(100 * (1 + Math.Abs(source.Tier - target.Tier)));
        }
        public string MotorTransferBlocker(string motorId, string targetId)
        {
            SyncGarage(); var v = _garage.Find(x => x.Id == motorId); var target = RegionalGarages.Get(targetId);
            if (target == null || IsGameOver || Racing || IsWorking || Trip != null) return "当前不能调车";
            if(v!=null&&MotorCatalog.Get(v.ModelId).Kind==VehicleKind.Van)return "货车仅供员工进货，不属于赛车车库车辆";
            if (CurrentPlace?.Id != targetId && CurrentPlace?.Id != target.TrackId) return "请到目标车库或对应赛场办理调入";
            if (v == null || v.Commuter) return "通勤车需先在车库改为入库车辆";
            if (v.GarageId == targetId) return "车辆已经在本区车库";
            if (!MotorUsable(v)) return "车辆正在使用或被扣押，不能调运";
            if (!HasGarage(v.GarageId)) return "车辆尚未存放在有效车库";
            string why = MotorSpaceBlocker(targetId); if (why != null) return why;
            if (!Ledger.CanAfford(MotorTransferFee(motorId, targetId))) return "调运现金不足";
            return null;
        }
        public bool TransferMotor(string motorId, string targetId)
        {
            string why = MotorTransferBlocker(motorId, targetId); if (why != null) return Fail(why);
            var v = _garage.Find(x => x.Id == motorId); var fee = MotorTransferFee(motorId, targetId);
            if (!Ledger.TryDebit(Clock.Today, "车辆调运", fee, RegionalGarages.Get(v.GarageId).Name + "→" + RegionalGarages.Get(targetId).Name,
                new LedgerAttribution(LedgerBusinessKind.Personal, propertyId: targetId, transactionKind: "车辆调运"))) return false;
            v.GarageId = targetId; Say(MotorCatalog.Get(v.ModelId).Name + "已调入" + RegionalGarages.Get(targetId).Name + "，支付" + fee + "。"); return true;
        }
    }
}
