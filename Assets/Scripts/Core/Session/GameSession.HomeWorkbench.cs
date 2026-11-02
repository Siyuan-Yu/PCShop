using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class HomeAutomationSettings
    {
        public bool Meals, Takeaway, Sleep, Clothing, Supplies;
        public int SleepHour=23, WakeHour=7, MealTier, DailyMealBudget=60;
        public string SupplyId=Gifts.Fruit;
    }
    [Serializable]
    public sealed class WorkbenchPriceDay { public int Day; public string Kind; public double Price; }
    [Serializable]
    public sealed class WorkbenchAlert { public string Key, Text; }
    public sealed partial class SaveData
    {
        public int homeWorkbenchVersion;
        public HomeAutomationSettings homeAutomation;
        public WorkbenchPriceDay[] workbenchPrices;
        public WorkbenchAlert[] workbenchAlerts;
        public int workbenchSleepDay=-1, workbenchSupplyDay=-1, workbenchMealDay=-1, workbenchMealSpent;
        public long workbenchOutfitAt=-1;
        public bool workbenchAutomationConfigured;
    }
    public sealed partial class GameSession
    {
        public const string HomeWorkbenchId="home_workbench";
        public HomeAutomationSettings HomeAutomation { get; private set; }=new HomeAutomationSettings();
        private readonly List<WorkbenchPriceDay> _workbenchPrices=new List<WorkbenchPriceDay>();
        private readonly Dictionary<string,string> _workbenchAlerts=new Dictionary<string,string>();
        private long _workbenchPriceAt=-1, _workbenchOutfitAt=-1;
        private int _workbenchSleepDay=-1,_workbenchSupplyDay=-1,_workbenchMealDay=-1,_workbenchMealSpent;
        private bool _workbenchSleepPending;
        private bool _workbenchAutomationConfigured;
        public bool OwnsHomeWorkbench=>FurnitureInStock(HomeWorkbenchId)>0 || HomeBox.FurnitureCount(HomeWorkbenchId)>0
            || HomeTemp.FurnitureCount(HomeWorkbenchId)>0 || Backpack.FurnitureCount(HomeWorkbenchId)>0 || HomeLayout.Any(p=>p.Id==HomeWorkbenchId);
        public bool CanUseHomeWorkbench=>!IsGameOver && CurrentPlace==Home && (OwnsHomeWorkbench||DebugHomeWorkbenchEnabled) && !IsWorking && !CityActivityExclusive && Trip==null;
        public IReadOnlyList<WorkbenchAlert> WorkbenchAlerts=>_workbenchAlerts.Select(p=>new WorkbenchAlert{Key=p.Key,Text=p.Value}).ToArray();
        public int WorkbenchMealSpent=>_workbenchMealDay==Clock.Today.TotalDays?_workbenchMealSpent:0;
        public int WorkbenchMealsAvailable=>(HomeHas(FurnitureRole.Cooker)?Pantry.CookedBowls:0)+(HomeHas(FurnitureRole.Fridge)?HomeFridge.Food.Packed.Where(b=>b.Count>0&&FastFoodCatalog.Packed(b.Id)?.Drink!=true).Sum(b=>b.Count):0)
            +(HomeHas(FurnitureRole.Storage)?HomeBox.Food.Packed.Where(b=>b.Count>0&&FastFoodCatalog.Packed(b.Id)?.Drink!=true).Sum(b=>b.Count):0)
            +Backpack.Food.Packed.Where(b=>b.Count>0&&FastFoodCatalog.Packed(b.Id)?.Drink!=true).Sum(b=>b.Count);
        public bool SetHomeAutomation(HomeAutomationSettings settings)
        {
            if(!CanUseHomeWorkbench || settings==null)return Fail("在家拥有家庭经营大屏后才能设置生活托管。");
            if(settings.SleepHour<0||settings.SleepHour>23||settings.WakeHour<0||settings.WakeHour>23||settings.SleepHour==settings.WakeHour
                ||settings.MealTier<0||settings.MealTier>=DeliveryMeals.Count||settings.DailyMealBudget<0||settings.DailyMealBudget>100000
                ||!IsEdibleGift(settings.SupplyId))return Fail("请检查托管时间、餐档、预算和消耗品。");
            HomeAutomation=CopyAutomation(settings);_workbenchAutomationConfigured=true;
            if(!settings.Meals)_workbenchAlerts.Remove("food");if(!settings.Sleep)_workbenchAlerts.Remove("bed");
            if(!settings.Clothing)_workbenchAlerts.Remove("wardrobe");if(!settings.Supplies)_workbenchAlerts.Remove("supply");
            if(!settings.Takeaway||settings.DailyMealBudget-WorkbenchMealSpent>=DeliveryMeals[settings.MealTier].Price&&Ledger.CanAfford(Money.FromYuan(DeliveryMeals[settings.MealTier].Price)))_workbenchAlerts.Remove("meal");
            return true;
        }
        private static HomeAutomationSettings CopyAutomation(HomeAutomationSettings a)=>new HomeAutomationSettings{Meals=a.Meals,Takeaway=a.Takeaway,Sleep=a.Sleep,Clothing=a.Clothing,Supplies=a.Supplies,
            SleepHour=a.SleepHour,WakeHour=a.WakeHour,MealTier=a.MealTier,DailyMealBudget=a.DailyMealBudget,SupplyId=a.SupplyId};
        private void WorkbenchAlertState(string key,string message,bool active)
        {
            if(!active){_workbenchAlerts.Remove(key);return;}
            if(_workbenchAlerts.ContainsKey(key))return;
            _workbenchAlerts[key]=message;Popup?.Invoke("生活托管提醒",message);
        }
        public void RecordWorkbenchPrices()
        {
            int day=Clock.Today.TotalDays;
            foreach(string kind in RetailShopTypes.All)
            {
                double price;
                if(kind=="computer")price=PartCatalog.All.Average(p=>Market.PriceOf(p).Cents*.84);
                else
                {
                    var products=Gifts.All.Where(g=>RetailShopTypes.Sells(kind,g.Id)&&!g.LimitedEdition).ToArray();
                    if(products.Length==0)continue;
                    price=products.Average(g=>(double)CityRetailWholesalePrice(City.Places.FirstOrDefault(p=>p.Business==g.SoldAt),g).Cents);
                }
                var sample=_workbenchPrices.FirstOrDefault(p=>p.Day==day&&p.Kind==kind);
                if(sample==null)_workbenchPrices.Add(new WorkbenchPriceDay{Day=day,Kind=kind,Price=price});else sample.Price=price;
            }
            _workbenchPrices.RemoveAll(p=>p.Day<day-30);_workbenchPriceAt=Clock.Now.TotalMinutes;
        }
        public WorkbenchPriceDay[] WorkbenchPrices(string kind)=>_workbenchPrices.Where(p=>p.Kind==kind).OrderBy(p=>p.Day)
            .Select(p=>new WorkbenchPriceDay{Day=p.Day,Kind=p.Kind,Price=p.Price}).ToArray();
        public double? WorkbenchPriceChange(string kind,int days=7)
        {
            var history=WorkbenchPrices(kind);if(history.Length<2)return null;
            var latest=history.Last();var first=history.FirstOrDefault(p=>p.Day>=latest.Day-days);
            return first==null||first.Day==latest.Day||first.Price<=0?(double?)null:(latest.Price/first.Price-1)*100;
        }
        public (Money income,Money expense) WorkbenchPersonalCash(GameDate from,GameDate to)
        {
            var entries=Ledger.Entries.Where(e=>e.Date>=from&&e.Date<=to && e.BusinessKind!=LedgerBusinessKind.InternalTransfer
                && e.BusinessKind!=LedgerBusinessKind.AssetInvestment && e.BusinessKind!=LedgerBusinessKind.Financing);
            return (Money.FromCents(entries.Where(e=>e.Amount.IsPositive).Sum(e=>e.Amount.Cents)),Money.FromCents(-entries.Where(e=>e.Amount.IsNegative).Sum(e=>e.Amount.Cents)));
        }
        public bool SetWorkbenchHours(string shopId,int openHour,int closeHour,bool allDay)
        {
            var shop=ShopAt(shopId);if(!CanUseHomeWorkbench||shop?.Rented!=true)return Fail("请选择自己的营业门店。");
            if(openHour<0||openHour>23||closeHour<0||closeHour>23||!allDay&&openHour==closeHour)return Fail("开关门请选择不同整点。");
            shop.Sim.SetHours(allDay?0:openHour*60,allDay?1440:closeHour*60);return true;
        }
        public bool SetWorkbenchMarkup(string shopId,double markup)
        {
            var shop=ShopAt(shopId);if(!CanUseHomeWorkbench||shop?.Rented!=true||double.IsNaN(markup)||double.IsInfinity(markup)||markup<.9||markup>1.6)return false;
            shop.Sim.Markup=markup;return true;
        }
        public bool SetWorkbenchGoodsPrice(string shopId,string id,long yuan)
        {
            var shop=ShopAt(shopId);return CanUseHomeWorkbench&&shop?.Rented==true&&shop.Sim.OnShelfGoods(id)>0&&shop.Sim.SetGoodsSalePrice(id,yuan);
        }
        public bool SetWorkbenchPartPrice(string shopId,string id,bool boxed,long yuan)
        {
            if(!PartCatalog.Exists(id))return false;
            var shop=ShopAt(shopId);var part=PartCatalog.Get(id);
            return CanUseHomeWorkbench&&shop?.Rented==true&&part!=null&&shop.Sim.ShelfFillOf(part.Category).Parts.Any(l=>l.Part.Id==id&&l.Boxed==boxed&&l.Count>0)
                &&shop.Sim.SetPartSalePrice(part,boxed,yuan);
        }
        private void TickHomeWorkbench(GameTimestamp from,GameTimestamp to)
        {
            if(_workbenchPriceAt<0||to.TotalMinutes-_workbenchPriceAt>=30||from.Date!=to.Date)RecordWorkbenchPrices();
            if(!CanUseHomeWorkbench||_sleeping)return;
            var a=HomeAutomation;int day=to.Date.TotalDays;
            if((a.Meals||a.Takeaway)&&(Player.Hunger<25||CrossesWorkbench(from,to,12*60)||CrossesWorkbench(from,to,18*60+30)))EatMeal("托管用餐");
            WorkbenchAlertState("food","家中可用饭食不足两餐，请补给或开启自动点饭。",a.Meals&&WorkbenchMealsAvailable<2);
            WorkbenchAlertState("wardrobe","自动换衣需要在家中摆放衣柜。",a.Clothing&&!HomeHas(FurnitureRole.Wardrobe));
            if(a.Clothing&&HomeHas(FurnitureRole.Wardrobe)&&to.TotalMinutes/180!=_workbenchOutfitAt)
            { _workbenchOutfitAt=to.TotalMinutes/180;EquipForCurrentWeather(); }
            if(a.Supplies&&CrossesWorkbench(from,to,16*60)&&_workbenchSupplyDay!=day)
            {
                _workbenchSupplyDay=day;
                bool accessible=HomeHas(FurnitureRole.Storage)&&HomeBox.GiftCount(a.SupplyId)>0;
                WorkbenchAlertState("supply","自动取用的家中消耗品不足，或尚未摆放纸箱。",!accessible);
                if(accessible)
                {
                    HomeBox.TakeGift(a.SupplyId);
                    ApplyDiet(a.SupplyId==Gifts.Soda?-3:a.SupplyId==Gifts.Fruit?5:1,a.SupplyId==Gifts.Soda?8:a.SupplyId==Gifts.Fruit?2:4);
                    Player.Hunger=Math.Min(100,Player.Hunger+(a.SupplyId==Gifts.Soda?2:a.SupplyId==Gifts.Fruit?8:10));
                    Say("生活托管使用了"+Gifts.Get(a.SupplyId).Name+"。");
                }
            }
            if(!a.Supplies||HomeHas(FurnitureRole.Storage)&&HomeBox.GiftCount(a.SupplyId)>0)_workbenchAlerts.Remove("supply");
            int sleepMinute=a.SleepHour*60,wakeMinute=a.WakeHour*60;
            bool restWindow=sleepMinute<wakeMinute?to.MinuteOfDay>=sleepMinute&&to.MinuteOfDay<wakeMinute:to.MinuteOfDay>=sleepMinute||to.MinuteOfDay<wakeMinute;
            int restDay=day-(sleepMinute>wakeMinute&&to.MinuteOfDay<wakeMinute?1:0);
            if(a.Sleep&&restWindow&&_workbenchSleepDay!=restDay)
            {
                WorkbenchAlertState("bed","自动睡觉需要家中有床。",!HomeHas(FurnitureRole.Bed));
                if(HomeHas(FurnitureRole.Bed)&&PendingEvent==null){_workbenchSleepDay=restDay;_workbenchSleepPending=true;}
            }
            if(!a.Sleep||HomeHas(FurnitureRole.Bed))_workbenchAlerts.Remove("bed");
        }
        private static bool CrossesWorkbench(GameTimestamp from,GameTimestamp to,int minute)
        {
            long mark=(long)from.Date.TotalDays*1440+minute;
            if(mark<=from.TotalMinutes)mark+=1440;
            return mark<=to.TotalMinutes;
        }
        private void ProcessWorkbenchSleep()
        {
            if(!_workbenchSleepPending)return;_workbenchSleepPending=false;
            if(CanUseHomeWorkbench&&!_sleeping&&PendingEvent==null&&HomeAutomation.Sleep&&HomeHas(FurnitureRole.Bed))SleepUntil(HomeAutomation.WakeHour*60);
        }
        private bool EatWorkbenchMeal(string meal)
        {
            if(!CanUseHomeWorkbench||!_workbenchAutomationConfigured)return false;
            var a=HomeAutomation;
            if(a.Meals)
            {
                List<FoodBatch> food=Pantry.CookedBowls>0&&HomeHas(FurnitureRole.Cooker)?Pantry.Cooked:
                    HomeHas(FurnitureRole.Fridge)&&HasMeal(HomeFridge.Food.Packed)?HomeFridge.Food.Packed:
                    a.Supplies&&HomeHas(FurnitureRole.Storage)&&HasMeal(HomeBox.Food.Packed)?HomeBox.Food.Packed:
                    HasMeal(Backpack.Food.Packed)?Backpack.Food.Packed:null;
                if(food!=null)
                {
                    Player.Hunger=Math.Min(100,Player.Hunger+EatFrom(food));_lastAutomaticMealMinute=Clock.Now.TotalMinutes;ApplyCargoLimit();
                    _workbenchAlerts.Remove("meal");Say("生活托管 · "+meal+"已使用已有饭食。");return true;
                }
            }
            if(!a.Takeaway)return true;
            int day=Clock.Today.TotalDays;if(_workbenchMealDay!=day){_workbenchMealDay=day;_workbenchMealSpent=0;}
            var delivery=DeliveryMeals[a.MealTier];
            if(_workbenchMealSpent+delivery.Price>a.DailyMealBudget)
            {WorkbenchAlertState("meal","自动点饭达到每日预算，请调整预算或补充食物。",true);return true;}
            if(!Ledger.TryDebit(Clock.Today,"餐饮",Money.FromYuan(delivery.Price),"生活托管 · "+meal+" · "+delivery.Name))
            {WorkbenchAlertState("meal","余额不足，自动点饭未扣款，请补充食物或现金。",true);return true;}
            _workbenchMealSpent+=delivery.Price;_lastAutomaticMealMinute=Clock.Now.TotalMinutes;
            Player.Hunger=Math.Min(100,Player.Hunger+delivery.Hunger);Player.Health=Math.Max(0,Math.Min(100,Player.Health+delivery.Health));
            ApplyDiet(0,delivery.Mood);Player.Energy=Math.Min(Player.MaxEnergy,Player.Energy+delivery.Energy);
            _workbenchAlerts.Remove("meal");Say("生活托管 · "+delivery.Name+"已送达，花费 ¥"+delivery.Price+"。");return true;
        }
        private void SaveHomeWorkbench(SaveData d)
        {
            d.homeWorkbenchVersion=1;d.homeAutomation=CopyAutomation(HomeAutomation);d.workbenchPrices=_workbenchPrices.ToArray();
            d.workbenchAutomationConfigured=_workbenchAutomationConfigured;
            d.workbenchAlerts=WorkbenchAlerts.ToArray();d.workbenchSleepDay=_workbenchSleepDay;d.workbenchSupplyDay=_workbenchSupplyDay;
            d.workbenchMealDay=_workbenchMealDay;d.workbenchMealSpent=_workbenchMealSpent;d.workbenchOutfitAt=_workbenchOutfitAt;
        }
        private void LoadHomeWorkbench(SaveData d)
        {
            HomeAutomation=d.homeAutomation==null?new HomeAutomationSettings():CopyAutomation(d.homeAutomation);
            _workbenchAutomationConfigured=d.workbenchAutomationConfigured;
            HomeAutomation.SleepHour=Math.Max(0,Math.Min(23,HomeAutomation.SleepHour));HomeAutomation.WakeHour=Math.Max(0,Math.Min(23,HomeAutomation.WakeHour));
            HomeAutomation.MealTier=Math.Max(0,Math.Min(DeliveryMeals.Count-1,HomeAutomation.MealTier));HomeAutomation.DailyMealBudget=Math.Max(0,Math.Min(100000,HomeAutomation.DailyMealBudget));
            if(HomeAutomation.SleepHour==HomeAutomation.WakeHour)HomeAutomation.Sleep=false;
            if(!IsEdibleGift(HomeAutomation.SupplyId))HomeAutomation.SupplyId=Gifts.Fruit;
            _workbenchPrices.Clear();foreach(var p in d.workbenchPrices??Array.Empty<WorkbenchPriceDay>())
                if(p!=null&&p.Price>0&&!double.IsInfinity(p.Price)&&!double.IsNaN(p.Price)&&RetailShopTypes.All.Contains(p.Kind)&&p.Day>=Clock.Today.TotalDays-30&&p.Day<=Clock.Today.TotalDays)
                    _workbenchPrices.Add(new WorkbenchPriceDay{Day=p.Day,Kind=p.Kind,Price=p.Price});
            _workbenchAlerts.Clear();foreach(var a in d.workbenchAlerts??Array.Empty<WorkbenchAlert>())if(a?.Key!=null&&a.Text!=null)_workbenchAlerts[a.Key]=a.Text;
            _workbenchSleepDay=d.homeWorkbenchVersion>0?d.workbenchSleepDay:-1;_workbenchSupplyDay=d.homeWorkbenchVersion>0?d.workbenchSupplyDay:-1;
            _workbenchMealDay=d.homeWorkbenchVersion>0?d.workbenchMealDay:-1;_workbenchMealSpent=Math.Max(0,d.workbenchMealSpent);_workbenchOutfitAt=d.homeWorkbenchVersion>0?d.workbenchOutfitAt:-1;
            if(_workbenchPrices.Count==0)RecordWorkbenchPrices();
        }
    }
}
