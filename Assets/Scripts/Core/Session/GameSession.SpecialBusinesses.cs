using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum UsedCondition { Good, Flawed, Replica, Broken }
    public enum DiningStage { Waiting, Cooking, Ready, Eating, Completed, Left, Burnt, Checkout }
    [Serializable] public sealed class UsedItem
    {
        public int Id, Product, Personality, Statement, SellerId;
        public UsedCondition Condition, Claim;
        public long Asking, Minimum, Cost, Price, ExpiresAt;
        public long SalePrice,SoldAt;
        public bool Owned, Inspected, Listed, Sold;
        public UsedItem Copy() => (UsedItem)MemberwiseClone();
    }
    [Serializable] public sealed class DiningOrder
    {
        public int Id, Recipe, Table;
        public int Stove=-1;
        public DiningStage Stage;
        public bool TableCleared;
        public bool NeedsClearing => !TableCleared && (Stage == DiningStage.Checkout || Stage == DiningStage.Completed
            || Stage == DiningStage.Left || Stage == DiningStage.Burnt);
        public long Arrived, Started, ReadyAt, BurnAt, EatUntil, CheckoutAt, Price;
        public DiningOrder Copy() => (DiningOrder)MemberwiseClone();
    }
    [Serializable] public sealed partial class SpecialBusinessState
    {
        public string ShopId;
        public int NextId=1, BudgetDay=-1;
        public long NextVisit, Revenue, SpentToday;
        public int Paid, Lost;
        public int RestaurantArrivalDay = -1, RestaurantArrivalsToday;
        public int RestaurantVisitDay = -1;
        public List<ShopVisitDetail> RestaurantCompletedVisits = new List<ShopVisitDetail>();
        public int RecipeMask=7;
        public bool AutoPurchasePaused;
        public long DailyBudget=500, MaxOffer=200;
        public double Risk=.5, MenuMarkup=1;
        public List<UsedItem> Used=new List<UsedItem>();
        public List<DiningOrder> Orders=new List<DiningOrder>();
        public SpecialBusinessState Copy()
        {
            var copy=(SpecialBusinessState)MemberwiseClone();
            copy.RestaurantCompletedVisits = RestaurantCompletedVisits?.ToList() ?? new List<ShopVisitDetail>();
            copy.Used=Used.Select(i=>i.Copy()).ToList();copy.Orders=Orders.Select(o=>o.Copy()).ToList();copy.Workers=(Workers??new List<RestaurantWorker>()).Select(w=>w.Copy()).ToList();return copy;
        }
    }
    [Serializable] public sealed class SavedFixedSalePrice {public string ShopId,Key;public long Price;}
    public sealed partial class SaveData { public SpecialBusinessState[] specialBusinesses; public SavedFixedSalePrice[] fixedSalePrices; }
    public sealed partial class GameSession
    {
        private readonly Dictionary<string,SpecialBusinessState> _specialBusinesses=new Dictionary<string,SpecialBusinessState>();
        public static readonly string[] UsedNames={"旧台灯","旧音箱","旧腕表","旧装饰摆件"};
        private static readonly long[] UsedValues={120,260,450,180};
        public static readonly string[] SellerNames={"王先生","陈先生","张女士","李女士","赵先生","周女士","刘先生","孙女士","许先生","林女士","吴先生","黄女士"};
        public static readonly string[] SellerPersonalities={"说话直来直去","很会推销","态度谨慎"};
        private static readonly string[] SellerStatements={"一直是自己用的，情况我跟你说清楚。","朋友送的，我也不太懂这些。","搬家急着出，你看着给个价。","闲置好一阵了，平时保管得挺好。"};
        public static readonly string[] RecipeNames={"青菜炒饭","鸡蛋炒饭","肉末炒饭"};
        public static readonly string[] RecipeIngredients={"veg","egg","pork"};
        public static readonly long[] RecipePrices={18,22,28};
        public static string ConditionName(UsedCondition c)=>c==UsedCondition.Good?"好货":c==UsedCondition.Flawed?"瑕疵品":c==UsedCondition.Replica?"仿造品":"完全损坏";
        public static long UsedValue(UsedItem i)=>(long)Math.Round(UsedValues[Math.Max(0,Math.Min(3,i.Product))]*(i.Condition==UsedCondition.Good?1:i.Condition==UsedCondition.Flawed?.55:i.Condition==UsedCondition.Replica?.22:.03));
        public static string SellerSpeech(UsedItem i)=>SellerStatements[Math.Max(0,Math.Min(3,i.Statement))]+" 我说的状态是："+ConditionName(i.Claim)+"。";
        public bool IsSpecialBusiness(OwnedShop shop)=>shop!=null&&(shop.RetailKind=="restaurant"||shop.RetailKind=="secondhand");
        public SpecialBusinessState SpecialBusiness(string shopId)
        {
            if(!_specialBusinesses.TryGetValue(shopId,out var state))_specialBusinesses[shopId]=state=new SpecialBusinessState{ShopId=shopId,NextVisit=Clock.Now.TotalMinutes+12};
            return state;
        }
        public bool SpecialBusinessBusy(OwnedShop shop)
        {
            if(!_specialBusinesses.TryGetValue(shop.Place.Id,out var s))return false;
            return s.Used.Any(i=>!i.Sold)||s.Orders.Any(o=>o.Stage!=DiningStage.Completed&&o.Stage!=DiningStage.Left);
        }
        private static int Furnished(OwnedShop shop,string id)=>shop.Layout.Count(p=>p.Id==id);
        public string SpecialFitGap(OwnedShop shop)
        {
            if(shop.RetailKind=="restaurant")
            {
                List<string> missing = null;
                foreach (string id in RestaurantOpeningFurniture)
                    if (!OpeningFurniturePlaced(shop, id))
                        (missing ??= new List<string>()).Add(OpeningFurnitureName(id));
                return missing == null ? null : string.Join("、", missing);
            }
            if(shop.RetailKind=="secondhand")
            {
                var missing=new List<string>();
                foreach(string id in new[]{"used_buydesk","used_display",FurnitureCatalog.ShopCounter})
                    if(Furnished(shop,id)<1)missing.Add(FurnitureCatalog.Get(id).Name);
                if(!shop.Layout.Any(p=>FurnitureCatalog.Get(p.Id)?.Fixture==FixtureKind.Shelf))missing.Add("货架（设置为二手转售品）");
                return missing.Count==0?null:string.Join("、",missing);
            }
            return null;
        }
        private bool SpecialPlayer(OwnedShop shop)=>shop!=null&&InPlayerShop&&CurrentShop==shop&&!_sleeping&&!IsWorking&&!CityActivityExclusive&&!IsRenovating&&!IsGameOver;
        private Employee[] SpecialWorkers(OwnedShop shop,EmployeeRole role,GameTimestamp now)=>ShopStaff(shop,role).Where(e=>e.OnDuty(now)&&CityEmployeeAvailable(e,shop)).ToArray();
        public bool SpecialOpen(OwnedShop shop, GameTimestamp now) => SpecialOpen(shop, now, SpecialPlayer(shop));
        private bool SpecialOpen(OwnedShop shop, GameTimestamp now, bool playerPresent) =>
            shop.Rented && !IsGameOver && !shop.Sim.ForcedClosed && ShopPowerTraffic(shop, now) > 0
            && shop.Sim.InHours(now) && SpecialFitGap(shop) == null
            && (playerPresent || SpecialWorkers(shop, EmployeeRole.Chef, now).Length > 0
                || SpecialWorkers(shop, EmployeeRole.Waiter, now).Length > 0
                || shop.RetailKind == "secondhand" && (SpecialWorkers(shop, EmployeeRole.Appraiser, now).Length > 0
                    || SpecialWorkers(shop, EmployeeRole.Cashier, now).Length > 0));
        private void TickSpecialBusinesses(GameTimestamp from,GameTimestamp to)
        {
            foreach(var shop in MyShops.Where(IsSpecialBusiness))
            {
                shop.Sim.TradingEnabled=shop.RetailKind=="secondhand";
                var state=SpecialBusiness(shop.Place.Id);
                // Minute stepping preserves cooking, waiting and settlement order during sleep/fast-forward.
                for(long minute=from.TotalMinutes+1;minute<=to.TotalMinutes;minute++)
                {
                    var now=GameTimestamp.From(new GameDate((int)(minute/1440)),(int)(minute%1440));
                    bool open=SpecialOpen(shop,now);
                    if(shop.RetailKind=="restaurant")TickRestaurant(shop,state,now,open);
                    else TickUsedShop(shop,state,now,open);
                }
            }
        }
        private UsedItem CreateSeller(SpecialBusinessState state,long now)
        {
            var item=new UsedItem{Id=state.NextId++,Product=Random.Next(0,4),Personality=Random.Next(0,3),Statement=Random.Next(0,4),ExpiresAt=now+90};
            item.SellerId=Random.Next(0,SellerNames.Length);item.Personality=item.SellerId%SellerPersonalities.Length;
            int roll=Random.Next(0,100);item.Condition=roll<45?UsedCondition.Good:roll<73?UsedCondition.Flawed:roll<90?UsedCondition.Replica:UsedCondition.Broken;
            bool truthful=Random.Chance(item.Personality==0?.8:item.Personality==1?.35:.65);
            item.Claim=truthful?item.Condition:(UsedCondition)(((int)item.Condition+Random.Next(1,4))%4);
            double factor=item.Claim==UsedCondition.Good?1:item.Claim==UsedCondition.Flawed?.6:item.Claim==UsedCondition.Replica?.3:.08;
            item.Asking=Math.Max(1,(long)(UsedValues[item.Product]*factor*Random.Range(.55,.85)));
            item.Minimum=Math.Max(1,(long)(item.Asking*Random.Range(.65,.95)));return item;
        }
        private bool BuyUsed(OwnedShop shop,SpecialBusinessState state,UsedItem item,long bid,long at=-1)
        {
            long minute=at<0?Clock.Now.TotalMinutes:at;
            if(item==null||item.Owned||item.ExpiresAt<minute||bid<1||bid>1000000)return false;
            if(state.BudgetDay!=(int)(minute/1440)){state.BudgetDay=(int)(minute/1440);state.SpentToday=0;}
            if(bid<item.Minimum)return Fail("卖家没有接受这个报价，可提高报价或放弃。");
            if(state.Used.Count(i=>i.Owned&&!i.Sold)>=20)return Fail("最多暂存20件旧物，请先卖出或清理库存。");
            var payment=Money.FromYuan(bid);
            if(!Ledger.TryDebit(Clock.Today,"进货",payment,"二手收购 "+UsedNames[item.Product],LedgerAttribution.ForShop(shop.Place.Id,"used:"+item.Id+":buy","采购")))return Fail("现金不足。");
            item.Owned=true;item.Cost=bid;state.SpentToday+=bid;Say("已收购"+UsedNames[item.Product]+"，支付 ¥"+bid+"。请鉴定后确认售价上架。");return true;
        }
        public bool OfferUsedPrice(int id,long bid)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="secondhand"||!SpecialOpen(CurrentShop,Clock.Now))return Fail("请在营业中的二手店收购台操作。");
            var s=SpecialBusiness(CurrentShop.Place.Id);return BuyUsed(CurrentShop,s,s.Used.FirstOrDefault(i=>i.Id==id),bid);
        }
        public bool DeclineUsedSeller(int id)
        {
            if(!SpecialPlayer(CurrentShop))return false;var s=SpecialBusiness(CurrentShop.Place.Id);
            bool removed=s.Used.RemoveAll(i=>i.Id==id&&!i.Owned)>0;
            if(removed)Say("已拒收，卖家带走物品，没有扣款。");return removed;
        }
        public bool InspectUsed(int id)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="secondhand")return false;
            var item=SpecialBusiness(CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&i.Owned&&!i.Sold);
            if(item==null||item.Inspected)return false;item.Inspected=true;item.Price=UsedValue(item);
            Say("鉴定结果："+UsedNames[item.Product]+" · "+ConditionName(item.Condition)+"，参考价 ¥"+item.Price+"。");return true;
        }
        public bool ListUsedItem(int id, long price) =>
            ListUsedItem(id, price, out var blocker) || Fail(blocker);

        public bool ListUsedItem(int id, long price, out string blocker)
        {
            var shop = CurrentShop;
            if (!SpecialPlayer(shop) || shop.RetailKind != "secondhand")
            {
                blocker = InPlayerShop && shop.RetailKind == "secondhand" && IsRenovating
                    ? "本店今日歇业装修。请等明天恢复营业后再上架。"
                    : "请在自己的二手旧物店内上架。";
                return false;
            }
            var item = SpecialBusiness(shop.Place.Id).Used.FirstOrDefault(i => i.Id == id);
            blocker = SetUsedListing(shop, item, price);
            if (blocker != null) return false;
            Say(UsedNames[item.Product] + "已按 ¥" + price + " 上架到二手转售品货架，等待顾客挑选、结账。");
            return true;
        }
        public bool DiscardUsed(int id)
        {
            if(!SpecialPlayer(CurrentShop))return false;
            if(UsedReserved(CurrentShop,id))return Fail("顾客正在结账，不能丢弃这件商品。");
            return SpecialBusiness(CurrentShop.Place.Id).Used.RemoveAll(i=>i.Id==id&&i.Owned&&!i.Sold)>0;
        }
        public static double AppraiserAccuracy(int intelligence)=>.3+.006*Math.Max(0,Math.Min(100,intelligence));
        private void TickUsedShop(OwnedShop shop,SpecialBusinessState state,GameTimestamp now,bool open)
        {
            long minute=now.TotalMinutes;
            if(state.BudgetDay!=now.Date.TotalDays){state.BudgetDay=now.Date.TotalDays;state.SpentToday=0;}
            state.Used.RemoveAll(i=>!i.Owned&&i.ExpiresAt<minute);
            if(!open)return;
            if(minute>=state.NextVisit)
            {
                state.NextVisit=minute+(long)Math.Ceiling(SpecialVisitInterval(shop,45)/ShopPowerTraffic(shop,now));
                if(!state.Used.Any(i=>!i.Owned)&&state.Used.Count(i=>i.Owned&&!i.Sold)<20)state.Used.Add(CreateSeller(state,minute));
            }
            if(state.AutoPurchasePaused)return;
            var worker=SpecialWorkers(shop,EmployeeRole.Appraiser,now).OrderByDescending(e=>e.Intelligence).FirstOrDefault();
            if(worker==null)return;
            // Each seller is considered once: staff cannot reroll a judgement every minute.
            var seller=state.Used.FirstOrDefault(i=>!i.Owned&&i.ExpiresAt>minute);
            if(seller!=null)
            {
                UsedCondition guess=Random.Chance(AppraiserAccuracy(worker.Intelligence))?seller.Condition:seller.Claim;
                var estimate=seller.Copy();estimate.Condition=guess;
                long bid=Math.Min(state.MaxOffer,(long)(UsedValue(estimate)*(.35+.35*state.Risk)));
                bid=Math.Min(bid,Math.Max(0,state.DailyBudget-state.SpentToday));
                if(bid<seller.Minimum||!BuyUsed(shop,state,seller,bid,minute))state.Used.Remove(seller);
            }
            foreach(var item in state.Used.Where(i=>i.Owned&&!i.Sold).ToArray())
            {
                item.Inspected=true;
                if(!item.Listed&&shop.Sim.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).UsedKg+UsedWeight(item)<=shop.Sim.ShelfCapacityOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand"))
                    && SetUsedListing(shop, item, Math.Max(1, UsedValue(item))) is string blocker)
                    Fail(blocker);
            }
            if(state.Used.Count>80)state.Used.RemoveAll(i=>i.Sold);
        }
        private static bool RestaurantIngredientsOnly(OwnedShop shop) => shop?.RetailKind == "restaurant";
        private static bool IsRawIngredientProduct(string id) => id == "produce:rice" || Gifts.Get(id)?.IngredientId != null;
        private static bool IsRawIngredientDelivery(CourierItem item) =>
            item.Kind is CourierItemKind.Rice or CourierItemKind.Ingredient
            || (item.Kind is CourierItemKind.Goods or CourierItemKind.Gift && IsRawIngredientProduct(item.Id));

        private static string RestaurantFridgeRequired(OwnedShop shop) =>
            RestaurantIngredientsOnly(shop) && FridgeCapacityOf(shop.Layout) <= 0
                ? "餐馆要先摆放冰箱才能收食材" : null;

        private static double RestaurantFridgeRoom(OwnedShop shop) =>
            Math.Max(0, FridgeCapacityOf(shop.Layout) - shop.Fridge.WeightKg);

        private string RestaurantIngredientDeliveryBlocker(OwnedShop shop, double kg)
        {
            if (kg <= 1e-9 || !RestaurantIngredientsOnly(shop)) return null;
            if (RestaurantFridgeRequired(shop) is string missing) return missing;
            string shopId = shop.Place.Id;
            double incoming = _couriers.Where(run => run.ToShop && DeliveryShop(run.ShopId) == shop)
                .Sum(run => run.Items.Where(IsRawIngredientDelivery).Sum(CourierItemKg))
                + _employeeTrips.Where(trip => !trip.Delivered && DeliveryShop(trip.ShopId) == shop)
                    .Sum(trip => trip.Items.Select(PurchaseCourierItem).Where(IsRawIngredientDelivery).Sum(CourierItemKg))
                + _parcels.Where(parcel => parcel.ToShop && parcel.ShopId == shopId).Sum(parcel => parcel.Kind == ParcelKind.Rice
                    ? RiceBagKg * parcel.Count : parcel.Kind == ParcelKind.Gift && IsRawIngredientProduct(parcel.Id)
                        ? Gifts.Get(parcel.Id).WeightKg * parcel.Count : 0);
            double room = Math.Max(0, RestaurantFridgeRoom(shop) - incoming);
            return kg <= room + 1e-9 ? null : $"冰箱扣除在途食材后还能放 {room:0.##} 千克，本次需要 {kg:0.##} 千克";
        }

        private string UnpackRestaurantIngredient(OwnedShop shop, CourierItem item)
        {
            double room = RestaurantFridgeRoom(shop);
            string Destination(double overflow) => overflow <= 1e-9 ? "已放入冰箱"
                : room <= 1e-9 ? "冰箱未摆放或已满，已送回家中后仓" : "冰箱装不下的部分已送回家中后仓";
            if (item.Kind == CourierItemKind.Rice)
            {
                double fit = Math.Min(room, item.Kg);
                shop.Fridge.Food.RiceKg += fit;
                HomeBox.Food.RiceKg += item.Kg - fit;
                return $"大米 {item.Kg:0.##} 千克（{Destination(item.Kg - fit)}）";
            }
            var product = Gifts.Get(item.Id);
            int count = item.Count;
            int fits = count <= 0 ? 0 : Math.Min(count, (int)Math.Floor(room / (CourierItemKg(item) / count) + 1e-9));
            string name = item.Kind == CourierItemKind.Ingredient ? Ingredients.Get(item.Id)?.Name ?? item.Id : product.Name;
            void Add(Backpack bag, int units, bool chilled)
            {
                if (units <= 0) return;
                if (item.Kind == CourierItemKind.Ingredient)
                    bag.Food.Ingredient.Add(new FoodBatch { Id = item.Id, Name = name, Count = units, ExpireDay = FoodBatch.NoExpiry, Chilled = chilled });
                else AddRetailToBag(bag, product, units, FoodBatch.NoExpiry, chilled);
            }
            Add(shop.Fridge, fits, true);
            Add(HomeBox, count - fits, false);
            return $"{name}×{count}（{Destination(count - fits)}）";
        }

        private void RefrigerateExistingRestaurantIngredients(OwnedShop shop)
        {
            if (!RestaurantIngredientsOnly(shop)) return;
            var fridge = shop.Fridge;
            double capacity = FridgeCapacityOf(shop.Layout);
            if (capacity <= 0) return;
            foreach(var bag in new[]{shop.Locker,shop.Temp})
            {
                double rice=Math.Min(bag.Food.RiceKg,Math.Max(0,capacity-fridge.WeightKg));
                if(rice>1e-9){bag.Food.RiceKg-=rice;fridge.Food.RiceKg+=rice;}
                foreach(var batch in bag.Food.Ingredient.ToArray())
                {
                    double unit=Backpack.IngredientKg(batch.Id);int fit=Math.Min(batch.Count,(int)Math.Floor(Math.Max(0,capacity-fridge.WeightKg)/unit+1e-9));
                    if(fit<1)continue;var copy=batch.Clone();copy.Count=fit;fridge.Food.Ingredient.Add(copy);batch.Count-=fit;if(batch.Count==0)bag.Food.Ingredient.Remove(batch);
                }
            }
            foreach(var line in shop.Sim.Stock.GoodsLines().ToArray())
            {
                if (!IsRawIngredientProduct(line.Key)) continue;
                var product=Gifts.Get(line.Key);
                int fit=Math.Min(line.Value,(int)Math.Floor(Math.Max(0,capacity-fridge.WeightKg)/product.WeightKg+1e-9));
                for(int n=0;n<fit;n++)
                {
                    int expiry=shop.Sim.Stock.GoodsExpiry(product.Id);bool chilled=shop.Sim.Stock.GoodsChilled(product.Id);
                    if(!shop.Sim.Stock.TryRemoveGoods(product.Id,1))break;
                    AddRetailToBag(fridge,product,1,expiry,chilled);
                }
            }
            Chill(fridge);
        }
        public int KitchenIngredientCount(OwnedShop shop,string id) => shop.Fridge.Food.IngredientCount(id);
        public double KitchenRice(OwnedShop shop) => shop.Fridge.Food.RiceKg;
        private int[] RestaurantMealChoices()
        {
            var shop=CurrentShop;
            if(!SpecialPlayer(shop)||shop.RetailKind!="restaurant"
                ||Furnished(shop,"restaurant_stove")==0||KitchenRice(shop)<.25-1e-9)return Array.Empty<int>();
            return Enumerable.Range(0,RecipeNames.Length).Where(i=>KitchenIngredientCount(shop,RecipeIngredients[i])>0).ToArray();
        }
        private bool EatRestaurantMeal(string meal)
        {
            var choices=RestaurantMealChoices();
            if(choices.Length==0)return false;
            int recipe=choices[Random.Next(0,choices.Length)];
            if(!ConsumeRecipe(CurrentShop,recipe))return false;
            double gain=Math.Min(MealGain,100-Player.Hunger);
            Player.Hunger+=gain;
            ApplyDiet(MealNutrition(RecipeIngredients[recipe]),MealMood(RecipeIngredients[recipe]));
            _lastAutomaticMealMinute=Clock.Now.TotalMinutes;
            ApplyShelfCapacity();
            Say($"{meal}在自己的餐厅吃了{RecipeNames[recipe]}，消耗本店大米 0.25 千克和{Ingredients.Get(RecipeIngredients[recipe]).Name} 1 份，饱腹 +{gain:0.#}。");
            return true;
        }
        private bool ConsumeRecipe(OwnedShop shop,int recipe)
        {
            string id=RecipeIngredients[recipe];
            if(KitchenRice(shop)<.25-1e-9||KitchenIngredientCount(shop,id)<1)return false;
            shop.Fridge.Food.RiceKg = Math.Max(0, shop.Fridge.Food.RiceKg - .25);
            Pantry.TakeBowl(shop.Fridge.Food.Ingredient, id);
            return true;
        }
        private bool StartRestaurantOrder(OwnedShop shop,SpecialBusinessState state,DiningOrder order,long minute,bool automatic=false,int stove=-1)
        {
            if(order==null||order.Stage!=DiningStage.Waiting||SpecialFitGap(shop)!=null)return false;
            if(state.Orders.Count(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt)>=Furnished(shop,"restaurant_stove"))return automatic?false:Fail("锅位正在使用。");
            var used=state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt).Select(o=>o.Stove).ToArray();
            if(stove<0)stove=Enumerable.Range(0,Furnished(shop,"restaurant_stove")).First(i=>!used.Contains(i));
            if(used.Contains(stove))return false;
            if(!ConsumeRecipe(shop,order.Recipe))return automatic?false:Fail("冰箱缺少食材：需要0.25千克大米和1份"+Ingredients.Get(RecipeIngredients[order.Recipe]).Name+"。");
            order.Stove=stove;order.Stage=DiningStage.Cooking;order.Started=minute;order.ReadyAt=minute+8;order.BurnAt=minute+38;return true;
        }
        public bool StartRestaurantCooking(int id)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant"||!SpecialOpen(CurrentShop,Clock.Now))return false;
            var s=SpecialBusiness(CurrentShop.Place.Id);return StartRestaurantOrder(CurrentShop,s,s.Orders.FirstOrDefault(o=>o.Id==id),Clock.Now.TotalMinutes);
        }
        public bool PlateRestaurantOrder(int id)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant")return false;
            var o=SpecialBusiness(CurrentShop.Place.Id).Orders.FirstOrDefault(x=>x.Id==id);
            if(!CanTakeOffHeat(o,Clock.Now.TotalMinutes))return Fail("还没熟或已经焦糊。");
            o.Stage=DiningStage.Ready;return true;
        }
        public bool ServeRestaurantOrder(int id)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant")return false;
            var o=SpecialBusiness(CurrentShop.Place.Id).Orders.FirstOrDefault(x=>x.Id==id);
            if(!JobMatches(RestaurantJob.Serve,o,Clock.Now.TotalMinutes))return false;
            o.Stage=DiningStage.Eating;o.EatUntil=Clock.Now.TotalMinutes+15;return true;
        }
        public bool ClearRestaurantOrder(int id)
        {
            var shop = CurrentShop;
            if (!SpecialPlayer(shop) || shop.RetailKind != "restaurant") return false;
            var state = SpecialBusiness(shop.Place.Id);
            if (state.Workers.Any(worker => worker.Busy && worker.OrderId == id)) return false;
            return ClearRestaurantOrder(shop, state, state.Orders.FirstOrDefault(order => order.Id == id), Clock.Now);
        }
        private bool ClearRestaurantOrder(OwnedShop shop,SpecialBusinessState state,DiningOrder order,GameTimestamp now)
        {
            if(order?.NeedsClearing!=true)return false;
            if(order.Stage==DiningStage.Burnt&&now.TotalMinutes-order.Arrived<100){order.Stage=DiningStage.Waiting;order.Stove=-1;return true;}
            if (order.Stage == DiningStage.Checkout)
            {
                if (Furnished(shop, FurnitureCatalog.ShopCounter) == 0) return false;
                SettleRestaurantTable(shop, state, order, now);
            }
            return state.Orders.Remove(order);
        }

        private const int RestaurantCheckoutPatienceMinutes = 60;

        public int RestaurantTablesAwaitingPayment(OwnedShop shop) => shop?.RetailKind=="restaurant"
            && _specialBusinesses.TryGetValue(shop.Place.Id,out var state)
                ? state.Orders.Count(o=>o.Stage==DiningStage.Checkout) : 0;

        public int RestaurantArrivalsToday(OwnedShop shop) =>
            _specialBusinesses.TryGetValue(shop.Place.Id, out var state) && state.RestaurantArrivalDay == Clock.Today.TotalDays
                ? state.RestaurantArrivalsToday : 0;

        public int RestaurantLossesToday(OwnedShop shop) =>
            _specialBusinesses.TryGetValue(shop.Place.Id, out var state) && state.RestaurantVisitDay == Clock.Today.TotalDays
                ? state.RestaurantCompletedVisits.Count(visit => visit.CountedAsLost) : 0;

        public IEnumerable<ShopVisitDetail> RestaurantVisitsToday(OwnedShop shop, bool lossesOnly)
        {
            if (!_specialBusinesses.TryGetValue(shop.Place.Id, out var state)) yield break;
            int day = Clock.Today.TotalDays;
            long dayStart = (long)day * GameTimestamp.MinutesPerDay;
            if (state.RestaurantVisitDay == day)
                foreach (var visit in state.RestaurantCompletedVisits)
                    if (lossesOnly ? visit.CountedAsLost : visit.ArrivalMinute >= dayStart)
                        yield return visit;
            if (lossesOnly) yield break;
            foreach (var order in state.Orders)
            {
                if (order.Arrived < dayStart || order.Stage == DiningStage.Left || order.Stage == DiningStage.Completed) continue;
                yield return new ShopVisitDetail
                {
                    CustomerId = order.Id,
                    ArrivalMinute = order.Arrived,
                    StatusText = (order.Stage switch
                    {
                        DiningStage.Waiting => "已点餐",
                        DiningStage.Cooking => "烹饪中",
                        DiningStage.Ready => "等待上菜",
                        DiningStage.Eating => "用餐中",
                        DiningStage.Checkout => "等待收桌收款",
                        DiningStage.Burnt => "等待重做",
                        _ => "在店"
                    }) + " · " + RecipeNames[order.Recipe]
                };
            }
        }

        private static void RecordRestaurantVisit(SpecialBusinessState state, GameTimestamp now, ShopVisitDetail visit)
        {
            if (state.RestaurantVisitDay != now.Date.TotalDays)
            {
                state.RestaurantCompletedVisits.Clear();
                state.RestaurantVisitDay = now.Date.TotalDays;
            }
            state.RestaurantCompletedVisits.Add(visit);
            if (visit.CountedAsLost) state.Lost++;
        }

        private static void RecordRestaurantLoss(SpecialBusinessState state, GameTimestamp now, int id, long arrived, LeaveReason reason) =>
            RecordRestaurantVisit(state, now, new ShopVisitDetail
            {
                CustomerId = id, ArrivalMinute = arrived, State = CustomerState.Left,
                CountedAsLost = true, Reason = reason
            });

        private static string RestaurantSaleId(OwnedShop shop, DiningOrder order) =>
            $"meal:{shop.Place.Id}:{order.Arrived}:{order.Id}";

        private void SettleRestaurantTable(OwnedShop shop,SpecialBusinessState state,DiningOrder order,GameTimestamp now)
        {
            Ledger.Credit(now.Date,"餐饮",Money.FromYuan(order.Price),RecipeNames[order.Recipe]+"堂食",LedgerAttribution.ForShop(shop.Place.Id, RestaurantSaleId(shop, order) + ":paid", "销售"));
            RecordAggregateReceipt(shop, "meal:" + order.Id, RecipeNames[order.Recipe], Money.FromYuan(order.Price), now.Date.TotalDays);
            state.Revenue+=order.Price;state.Paid++;order.Stage=DiningStage.Completed;
            shop.Sim.Reputation+=ShopSimulation.SaleReputationGain;
            shop.Sim.AdjustShopReputation(.12);
            var drink = SellRestaurantDrink(shop, state, order, now);
            RecordRestaurantVisit(state, now, new ShopVisitDetail
            {
                CustomerId = order.Id, ArrivalMinute = order.Arrived, State = CustomerState.Paid,
                ProductName = drink.Name == null ? RecipeNames[order.Recipe] : RecipeNames[order.Recipe] + "、" + drink.Name,
                PaidCents = Money.FromYuan(order.Price).Cents + drink.Price.Cents
            });
        }
        private void TickRestaurant(OwnedShop shop,SpecialBusinessState state,GameTimestamp now,bool open)
        {
            long minute=now.TotalMinutes;
            foreach(var o in state.Orders.ToArray())
            {
                if(o.Stage==DiningStage.Cooking&&minute>=o.BurnAt)o.Stage=DiningStage.Burnt;
                if((o.Stage==DiningStage.Waiting||o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Ready||o.Stage==DiningStage.Burnt)&&minute-o.Arrived>=100)
                {
                    o.Stage=DiningStage.Left;
                    RecordRestaurantLoss(state, now, o.Id, o.Arrived, LeaveReason.MealWaitTimeout);
                    shop.Sim.AdjustShopReputation(-.3);
                }
                if(o.Stage==DiningStage.Eating&&minute>=o.EatUntil)
                {
                    o.Stage=DiningStage.Checkout;
                    o.CheckoutAt=minute;
                }
                if(o.Stage==DiningStage.Checkout&&minute>=o.CheckoutAt+RestaurantCheckoutPatienceMinutes)
                {
                    o.Stage=DiningStage.Left;
                    RecordRestaurantLoss(state, now, o.Id, o.Arrived, LeaveReason.Impatient);
                    shop.Sim.AdjustShopReputation(-.3);
                    if(o.TableCleared)state.Orders.Remove(o);
                }
            }
            TickRestaurantWorkers(shop,state,now,open);
            if(!open||minute<state.NextVisit)return;
            state.NextVisit=minute+(long)Math.Ceiling(SpecialVisitInterval(shop,RestaurantVisitMinutes)/ShopPowerTraffic(shop,now));
            TrySeatRestaurantGuest(shop, state, now);
        }

        private bool TrySeatRestaurantGuest(OwnedShop shop, SpecialBusinessState state, GameTimestamp now)
        {
            long minute = now.TotalMinutes;
            int tables=Math.Min(RestaurantTableLimit(shop.Sim.Size), RestaurantTablesPlaced(shop));
            int table=0;
            while(table<tables&&state.Orders.Any(o=>!o.TableCleared&&o.Table==table))table++;
            if(table>=tables)return false;
            if (state.RestaurantArrivalDay != now.Date.TotalDays)
            {
                state.RestaurantArrivalDay = now.Date.TotalDays;
                state.RestaurantArrivalsToday = 0;
            }
            state.RestaurantArrivalsToday++;
            if (!Random.Chance(shop.Sim.MarkupPurchaseIntent(state.MenuMarkup)))
            {
                RecordRestaurantLoss(state, now, state.NextId++, minute, LeaveReason.TooExpensive);
                return true;
            }
            var recipes=Enumerable.Range(0,RecipeNames.Length).Where(i=>(state.RecipeMask&(1<<i))!=0).ToArray();
            if (recipes.Length == 0)
            {
                RecordRestaurantLoss(state, now, state.NextId++, minute, LeaveReason.MenuUnavailable);
                return true;
            }
            int recipe=recipes[Random.Next(0,recipes.Length)];
            state.Orders.Add(new DiningOrder{Id=state.NextId++,Recipe=recipe,Table=table,Stage=DiningStage.Waiting,Arrived=minute,Price=(long)Math.Round(RecipePrices[recipe]*state.MenuMarkup)});
            return true;
        }
        private void MoveSpecialBusinessState(string from,string to)
        {
            if(from==to||!_specialBusinesses.TryGetValue(from,out var state))return;
            _specialBusinesses.Remove(from);state.ShopId=to;_specialBusinesses[to]=state;
        }
        private void SaveSpecialBusinesses(SaveData d)
        {
            d.specialBusinesses=_specialBusinesses.Values.Select(s=>s.Copy()).ToArray();
            d.fixedSalePrices=MyShops.SelectMany(shop=>shop.Sim.FixedSalePrices.Select(p=>new SavedFixedSalePrice{ShopId=shop.Place.Id,Key=p.Key,Price=p.Value})).ToArray();
        }
        private void LoadSpecialBusinesses(SaveData d)
        {
            _specialBusinesses.Clear();
            foreach(var price in d.fixedSalePrices??Array.Empty<SavedFixedSalePrice>())if(price!=null)ShopAt(price.ShopId)?.Sim.RestoreFixedSalePrice(price.Key,price.Price);
            foreach(var saved in d.specialBusinesses??Array.Empty<SpecialBusinessState>())
                if(saved!=null&&ShopAt(saved.ShopId)!=null)
                {
                    saved.Used=saved.Used??new List<UsedItem>();saved.Orders=saved.Orders??new List<DiningOrder>();saved.Workers=saved.Workers??new List<RestaurantWorker>();
                    var state=saved.Copy();var used=new HashSet<int>();
                    state.Orders.RemoveAll(order => order.TableCleared);
                    foreach (var order in state.Orders.Where(o => o.Stage == DiningStage.Checkout))
                        order.CheckoutAt = Math.Min(order.CheckoutAt, Clock.Now.TotalMinutes);
                    foreach (var worker in state.Workers.Where(w => w.Station == FurnitureCatalog.LegacyRestaurantPass && w.Job == RestaurantJob.Plate && w.Phase == 1))
                    {
                        var order = state.Orders.FirstOrDefault(o => o.Id == worker.OrderId);
                        if (order?.Stage == DiningStage.Cooking) order.Stage = DiningStage.Ready;
                    }
                    state.Workers.RemoveAll(w => w.Station == FurnitureCatalog.LegacyRestaurantPass || w.Station == FurnitureCatalog.LegacyRestaurantPrep);
                    foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt))
                    {if(order.Stove<0||used.Contains(order.Stove))order.Stove=Enumerable.Range(0,state.Orders.Count+1).First(i=>!used.Contains(i));used.Add(order.Stove);}
                    _specialBusinesses[saved.ShopId]=state;
                }
            foreach(var shop in MyShops.Where(IsSpecialBusiness))shop.Sim.TradingEnabled=shop.RetailKind=="secondhand";
        }
    }
}
