using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // Baselines only; actual capacity belongs to the merchant + exact product pair.
        public static int SupplierDailyLimit(string productId) => ModelProducts.IsModel(productId)?20:ClothingProducts.IsClothing(productId)?60:
            productId!=null&&(productId.StartsWith("produce:")||productId.StartsWith("fast:"))?80:40;
        public int SupplierCapacity(string sourceId,string productId)
        {
            uint hash=2166136261;
            foreach(char ch in (sourceId??"")+"|"+(productId??""))hash=unchecked((hash^ch)*16777619);
            int basis=PartCatalog.Exists(productId)?20:productId!=null&&productId.StartsWith("kind:4:")?10:SupplierDailyLimit(productId);
            return Math.Max(5,Math.Min(100,(int)Math.Round(basis*new[]{.5,.75,1.0,1.25}[hash%4])));
        }
        private static string PhysicalProductId(PurchaseOffer offer)=>offer.Kind==PurchaseGoodsKind.Ingredient?"produce:"+offer.Id:
            offer.Kind==PurchaseGoodsKind.Rice?"produce:rice":offer.Kind==PurchaseGoodsKind.Gift||offer.Kind==PurchaseGoodsKind.Part?offer.Id:"kind:"+(int)offer.Kind+":"+offer.Id;
        private static string PhysicalProductId(PurchaseLine line)=>line.Kind==PurchaseGoodsKind.Ingredient?"produce:"+line.PartId:line.Kind==PurchaseGoodsKind.Rice?"produce:rice":line.Kind==PurchaseGoodsKind.Gift||line.Kind==PurchaseGoodsKind.Part?line.PartId:"kind:"+(int)line.Kind+":"+line.PartId;
        private static string PhysicalSupplyKey(PurchaseOffer offer)=>offer.Source.Id+"|"+PhysicalProductId(offer);
        public int PurchaseLimit(PurchaseOffer offer)=>SupplierCapacity(offer.Source.Id,PhysicalProductId(offer));
        public int PurchaseRemaining(PurchaseOffer offer)=>offer.Kind==PurchaseGoodsKind.Part?StallRemaining(offer.Source,PartCatalog.Get(offer.Id)):
            offer.Kind==PurchaseGoodsKind.Gift||offer.Kind==PurchaseGoodsKind.Ingredient||offer.Kind==PurchaseGoodsKind.Rice?CitySupplierRemaining(offer.Source.Id,PhysicalProductId(offer)):
            Math.Max(0,PurchaseLimit(offer)-(_citySupplierPurchases.TryGetValue(SupplierCounterKey(offer.Source.Id,PhysicalProductId(offer)),out var used)?used:0));
        private void RecordOtherSupply(PurchaseOffer offer,int count)=>RecordCitySupplierPurchase(offer.Source.Id,PhysicalProductId(offer),count);
        private static bool SupplierClosesOnWeekend(Place source)=>source.Kind==PlaceKind.Wholesaler||source.Id=="model_market"||source.Id=="clothing_wholesale";
        private static bool SourceWorksOnWeekday(PurchaseOffer offer,int weekday)=>!SupplierClosesOnWeekend(offer.Source)||weekday<5;
        private bool SupplierPlanHasDay(EmployeePurchasePlan plan,int day)
        {
            if((plan.Items?.Length??0)==0)return true;
            return plan.Items.Any(line=>{
                string source=line.SourceId??plan.SourceId;
                if(source==null)source=EmployeePurchaseOffer(line,plan.SourceId)?.Source.Id;
                if(source==null||!City.HasPlace(source))return false;
                return (!SupplierClosesOnWeekend(City.GetPlace(source))||day%7<5)&&(ReservationCount(plan,line,day%7)>0||line.BuyMaximum);
            });
        }
        public int NextEmployeePurchaseDay(string employeeId,EmployeePurchasePlan draft=null,int? earliest=null)
        {
            var plan=draft??FindEmployee(employeeId)?.Person.PurchasePlan;if(plan==null)return Clock.Today.TotalDays;
            int next=plan.NextOnOrAfter(earliest??Clock.Today.TotalDays);
            for(int attempt=0;attempt<14;attempt++){if(SupplierPlanHasDay(plan,next))return next;next=plan.NextOnOrAfter(next+1);}
            return -1;
        }
        public string EmployeePurchaseNextText(string employeeId)
        {
            int next=NextEmployeePurchaseDay(employeeId);return next<0?"暂无可执行配额，请调整清单":"下次最早第"+(next+1)+"天上班时尝试";
        }
        private static int ReservationCount(EmployeePurchasePlan plan,PurchaseLine line,int weekday)
        {
            if(!plan.MatchesWeekday(weekday))return 0;
            return line.ReservedByWeekday!=null&&line.ReservedByWeekday.Length==7?Math.Max(0,line.ReservedByWeekday[weekday]):Math.Max(0,line.Count);
        }
        private int OtherSupplierReservations(string employeeId,PurchaseOffer offer,int weekday,int actualDay=-1)
        {
            string key=PhysicalSupplyKey(offer);int total=0;
            foreach(var worker in Employees.Where(e=>e.Role==EmployeeRole.Buyer&&e.Person.Id!=employeeId))
            {
                var plan=worker.Person.PurchasePlan;
                if(plan?.Daily!=true||!plan.MatchesWeekday(weekday))continue;
                // Actual spending is already deducted from physical stock. Do not reserve it a second time.
                if(actualDay>=0&&(actualDay<plan.FirstDay||plan.LastDay>=actualDay))continue;
                foreach(var line in plan.Items??Array.Empty<PurchaseLine>())
                {
                    string source=line.SourceId??plan.SourceId;
                    if(source==null)source=EmployeePurchaseOffer(line,plan.SourceId)?.Source.Id;
                    if(source+"|"+PhysicalProductId(line)==key&&SourceWorksOnWeekday(offer,weekday))total+=ReservationCount(plan,line,weekday);
                }
            }
            return total;
        }
        public int SupplierPlanHeadroom(string employeeId,EmployeePurchasePlan draft,PurchaseOffer offer,int weekday)
        {
            if(!draft.MatchesWeekday(weekday)||!SourceWorksOnWeekday(offer,weekday))return 0;
            int used=OtherSupplierReservations(employeeId,offer,weekday);
            foreach(var line in draft.Items??Array.Empty<PurchaseLine>())
            {
                if(PurchaseKey(line)==offer.Key)continue;
                string source=line.SourceId??draft.SourceId;
                if(source==null)source=EmployeePurchaseOffer(line,draft.SourceId)?.Source.Id;
                if(source+"|"+PhysicalProductId(line)==PhysicalSupplyKey(offer))used+=Math.Max(0,line.Count);
            }
            return Math.Max(0,PurchaseLimit(offer)-used);
        }
        public int SupplierPlanMaximum(string employeeId,EmployeePurchasePlan draft,PurchaseOffer offer,bool allDays)
        {
            var days=Enumerable.Range(0,7).Where(d=>draft.MatchesWeekday(d)&&SourceWorksOnWeekday(offer,d)).ToArray();
            if(days.Length==0)return 0;
            return allDays?days.Min(d=>SupplierPlanHeadroom(employeeId,draft,offer,d)):days.Max(d=>SupplierPlanHeadroom(employeeId,draft,offer,d));
        }
        public string SupplierAllocationText(string employeeId,EmployeePurchasePlan draft,PurchaseOffer offer)
        {
            string[] names={"一","二","三","四","五","六","日"};
            return "计划可分配："+string.Join(" / ",Enumerable.Range(0,7).Where(d=>draft.MatchesWeekday(d)&&SourceWorksOnWeekday(offer,d)).Select(d=>"周"+names[d]+" "+SupplierPlanHeadroom(employeeId,draft,offer,d)));
        }
        private string AllocateSupplierPlan(string employeeId,EmployeePurchasePlan plan,bool migrate=false)
        {
            var used=new Dictionary<string,int>();string[] names={"一","二","三","四","五","六","日"};
            foreach(var line in plan.Items.OrderBy(i=>i.BuyMaximum?1:0))
            {
                var offer=EmployeePurchaseOffer(line,plan.SourceId);if(offer==null)return "采购货源已不可用";
                line.ReservedByWeekday=new int[7];
                for(int day=0;day<7;day++)
                {
                    if(!plan.MatchesWeekday(day)||!SourceWorksOnWeekday(offer,day))continue;
                    string key=PhysicalSupplyKey(offer)+"|"+day;
                    used.TryGetValue(key,out int assigned);
                    int free=Math.Max(0,PurchaseLimit(offer)-OtherSupplierReservations(employeeId,offer,day)-assigned);
                    int amount=line.BuyMaximum?free:Math.Min(free,line.Count);
                    if(!migrate&&!line.BuyMaximum&&line.Count>free)return offer.Source.Name+"的"+offer.Name+"周"+names[day]+"仅剩 "+free+" 份计划额度，请减少数量或更换货源/星期。";
                    if(amount<PurchaseMinimum(offer))amount=0;
                    line.ReservedByWeekday[day]=amount;used[key]=assigned+amount;
                }
                if(line.BuyMaximum)line.Count=Math.Max(1,line.ReservedByWeekday.Max());
                if(!migrate&&line.ReservedByWeekday.All(n=>n==0))return offer.Name+"没有可分配的采购日或剩余量不足起订量。";
            }
            return null;
        }
        private int EmployeeSupplyAvailable(string employeeId,PurchaseOffer offer,int day)=>Math.Max(0,PurchaseRemaining(offer)-OtherSupplierReservations(employeeId,offer,day%7,day));
        private void LoadWeeklySupplierPlans()
        {
            var legacy=Employees.Where(e=>e.Role==EmployeeRole.Buyer&&e.Person.PurchasePlan?.Daily==true&&
                (e.Person.PurchasePlan.Items??Array.Empty<PurchaseLine>()).Any(i=>i.ReservedByWeekday==null||i.ReservedByWeekday.Length!=7))
                .OrderBy(e=>e.Shop.Place.Id,StringComparer.Ordinal).ThenBy(e=>e.Person.Id,StringComparer.Ordinal).ToArray();
            foreach(var worker in legacy)worker.Person.PurchasePlan.Daily=false;
            foreach(var worker in legacy)
            {
                var plan=worker.Person.PurchasePlan;var schedule=plan.Schedule;plan.HasWeeklySchedule=true;plan.Frequency=schedule;
                PrepareEmployeePurchaseDraft(plan);AllocateSupplierPlan(worker.Person.Id,plan,true);plan.Daily=true;
            }
        }
    }
}
