using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class UsedMotor
    {
        public string ShopId,Source;public GarageVehicle Vehicle;public long Cost,ListPrice,Offer;public int OfferUntil,ListedDay;public bool Inspected;
        public UsedMotor Copy(){var c=(UsedMotor)MemberwiseClone();c.Vehicle=Vehicle.Copy();return c;}
    }
    public sealed class MotorLead {public string Id,PersonId,ModelId;public long Price;public double Condition;}
    public sealed partial class GameSession
    {
        private readonly List<UsedMotor> _usedMotors=new List<UsedMotor>();
        private readonly HashSet<string> _boughtMotorLeads=new HashSet<string>();
        public int UsedMotorCapacity(string shopId)=>City.GetPlace(shopId).Size==PCShopSell.Core.Shop.ShopSize.Small?2:City.GetPlace(shopId).Size==PCShopSell.Core.Shop.ShopSize.Medium?4:6;
        public IReadOnlyList<UsedMotor> UsedMotors=>_usedMotors.Select(s=>s.Copy()).ToArray();
        public Money UsedMotorAssetValue=>Money.FromYuan(_usedMotors.Sum(s=>(long)(MotorCatalog.Get(s.Vehicle.ModelId).Price*.6*s.Vehicle.Condition/100)));
        public IEnumerable<MotorLead> MotorLeads()
        {
            int week=Clock.Today.TotalDays/7;
            foreach(var t in RaceTracks.All.Where(t=>IsMet(t.Npc)))
            {
                string id=t.Id+":"+week;if(_boughtMotorLeads.Contains(id))continue;
                var models=MotorCatalog.All.Where(m=>MotorCatalog.ClassOf(m.Kind)==2&&m.Price<=new[]{100000,600000,1500000,4000000}[t.Tier]).ToArray();
                var m=models[(week+t.Tier)%models.Length];double condition=55+(week*13+t.Tier*7)%40;
                yield return new MotorLead{Id=id,PersonId=t.Npc,ModelId=m.Id,Condition=condition,Price=(long)(m.Price*(.48+condition*.002)-m.Price*Math.Min(.06,Relation(t.Npc)*.0006))};
            }
        }
        private bool AtUsedDealer(string shopId)=>!IsGameOver&&!Racing&&CurrentPlace?.Id==shopId&&VentureAt(shopId)?.Kind=="cars"&&!VentureAt(shopId).Paused&&Clock.MinuteOfDay>=540&&Clock.MinuteOfDay<1140;
        public bool BuyMotorLead(string shopId,string leadId)
        {
            if(!AtUsedDealer(shopId))return Fail("请到自己的二手车行，在 9:00–19:00 营业时办理。");
            var lead=MotorLeads().FirstOrDefault(l=>l.Id==leadId);if(lead==null||_usedMotors.Count(s=>s.ShopId==shopId)>=UsedMotorCapacity(shopId))return Fail("线索已失效或展位已满。");
            if(!Ledger.TryDebit(Clock.Today,"二手车收购",Money.FromYuan(lead.Price),MotorCatalog.Get(lead.ModelId).Name))return Fail("收车资金不足。");
            var car=NewMotor(lead.ModelId);car.Condition=lead.Condition;
            _usedMotors.Add(new UsedMotor{ShopId=shopId,Source=lead.PersonId,Vehicle=car,Cost=lead.Price});_boughtMotorLeads.Add(lead.Id);AdvanceClock(30);return true;
        }
        public bool ConsignMotor(string shopId,string id)
        {
            SyncGarage();var v=_garage.Find(c=>c.Id==id);
            if(!AtUsedDealer(shopId)||v==null||MotorCatalog.ClassOf(MotorCatalog.Get(v.ModelId).Kind)!=2||!MotorAvailable(v)||_usedMotors.Count(s=>s.ShopId==shopId)>=UsedMotorCapacity(shopId))return false;
            var m=MotorCatalog.Get(v.ModelId);if(Vehicle==m.Kind&&!SetVehicle(VehicleKind.Walk))return false;
            _owned.Remove(m.Kind);_garage.Remove(v);_usedMotors.Add(new UsedMotor{ShopId=shopId,Source="自有车辆",Vehicle=v,Cost=(long)(m.Price*.6*v.Condition/100),Inspected=true});return true;
        }
        public bool InspectUsedMotor(string id)
        {
            var s=_usedMotors.Find(c=>c.Vehicle.Id==id);if(s==null||!AtUsedDealer(s.ShopId)||s.Inspected)return false;
            if(!Ledger.TryDebit(Clock.Today,"二手车检测",Money.FromYuan(300),"车况与底盘检查"))return false;
            s.Cost+=300;s.Inspected=true;AdvanceClock(30);return true;
        }
        public bool RepairUsedMotor(string id)
        {
            var s=_usedMotors.Find(c=>c.Vehicle.Id==id);if(s==null||!s.Inspected||!AtUsedDealer(s.ShopId)||s.Vehicle.Condition>=100)return false;
            long cost=MotorRepairPrice(s.Vehicle);if(!Ledger.TryDebit(Clock.Today,"二手车整备",Money.FromYuan(cost),MotorCatalog.Get(s.Vehicle.ModelId).Name))return false;
            s.Cost+=cost;s.Vehicle.Condition=100;s.Offer=0;AdvanceClock(60);return true;
        }
        public bool ListUsedMotor(string id,long price)
        {
            var s=_usedMotors.Find(c=>c.Vehicle.Id==id);if(s==null||!AtUsedDealer(s.ShopId)||!s.Inspected||price<1000||price>MotorCatalog.Get(s.Vehicle.ModelId).Price*3)return false;
            s.ListPrice=price;s.ListedDay=Clock.Today.TotalDays;s.Offer=0;return true;
        }
        private void SettleUsedMotors(ManagedVenture v,GameDate today,long fixedCost)
        {
            long paid=Math.Min(v.ReserveCents,fixedCost);v.ReserveCents-=paid;
            if(fixedCost>paid)Ledger.ForceDebit(today,"车行固定费",Money.FromCents(fixedCost-paid),"租金与店员工资");
            v.LastSalesCents=0;v.LastCostsCents=fixedCost;v.TotalOperatingCents-=fixedCost;
            foreach(var s in _usedMotors.Where(c=>c.ShopId==v.PlaceId&&c.OfferUntil<today.TotalDays))s.Offer=0;
            if(v.Paused||today.TotalDays%3!=v.OpenDay%3)return;
            var car=_usedMotors.Where(c=>c.ShopId==v.PlaceId&&c.ListPrice>0&&c.ListedDay<today.TotalDays&&c.Offer==0).OrderBy(c=>c.ListedDay).FirstOrDefault();if(car==null)return;
            long value=(long)(MotorCatalog.Get(car.Vehicle.ModelId).Price*(.45+.004*car.Vehicle.Condition));
            // High asking prices have a real chance of producing no buyer at all.
            if(car.ListPrice>value*1.4||!Random.Chance(Math.Min(.85,(double)value/car.ListPrice*.65)))return;
            car.Offer=Math.Min(car.ListPrice,(long)(value*Random.Range(.92,1.08)));car.OfferUntil=today.TotalDays+2;
            Say("二手车行收到 "+MotorCatalog.Get(car.Vehicle.ModelId).Name+" 的买家报价 ¥"+car.Offer.ToString("N0")+"，请在两天内到店交付。");
        }
        public bool AcceptMotorBuyer(string id)
        {
            var c=_usedMotors.Find(s=>s.Vehicle.Id==id);if(c==null||!AtUsedDealer(c.ShopId)||c.Offer<=0||c.OfferUntil<Clock.Today.TotalDays)return false;
            var v=VentureAt(c.ShopId);long profit=c.Offer-c.Cost;
            Ledger.Credit(Clock.Today,"二手车销售",Money.FromYuan(c.Offer),MotorCatalog.Get(c.Vehicle.ModelId).Name+" · 毛利 ¥"+profit.ToString("N0"));
            v.LastSalesCents+=c.Offer*100;v.TotalOperatingCents+=profit*100;_usedMotors.Remove(c);if(CityPerson(c.Source)!=null)AddRelation(c.Source,2);AdvanceClock(30);return true;
        }
        public bool WithdrawUsedMotor(string id)
        {
            var c=_usedMotors.Find(s=>s.Vehicle.Id==id);if(c==null||!AtUsedDealer(c.ShopId))return false;
            SyncGarage();_usedMotors.Remove(c);_garage.Add(c.Vehicle);_owned.Add(MotorCatalog.Get(c.Vehicle.ModelId).Kind);return true;
        }
    }
}
