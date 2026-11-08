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
    public enum DiningStage { Waiting, Cooking, Ready, Eating, Completed, Left, Burnt }
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
        public long Arrived, Started, ReadyAt, BurnAt, EatUntil, Price;
        public DiningOrder Copy() => (DiningOrder)MemberwiseClone();
    }
    [Serializable] public sealed partial class SpecialBusinessState
    {
        public string ShopId;
        public int NextId=1, BudgetDay=-1;
        public long NextVisit, Revenue, SpentToday;
        public int Paid, Lost;
        public int RestaurantArrivalDay = -1, RestaurantArrivalsToday;
        public int RecipeMask=7;
        public bool AutoPurchase;
        public long DailyBudget=500, MaxOffer=200;
        public double Risk=.5, MenuMarkup=1;
        public List<UsedItem> Used=new List<UsedItem>();
        public List<DiningOrder> Orders=new List<DiningOrder>();
        public SpecialBusinessState Copy()
        {
            var copy=(SpecialBusinessState)MemberwiseClone();
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
                var missing=new List<string>();
                foreach(var id in new[]{"restaurant_prep","restaurant_stove","restaurant_pass","restaurant_table"})
                    if(Furnished(shop,id)==0)missing.Add(FurnitureCatalog.Get(id).Name);
                if(!shop.Layout.Any(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Fridge))missing.Add("冰箱");
                return missing.Count==0?null:string.Join("、",missing);
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
            shop.Rented && !IsGameOver && !shop.Sim.ForcedClosed && shop.Sim.InHours(now) && SpecialFitGap(shop) == null
            && (playerPresent || SpecialWorkers(shop, EmployeeRole.Chef, now).Length > 0
                || SpecialWorkers(shop, EmployeeRole.Waiter, now).Length > 0 || SpecialWorkers(shop, EmployeeRole.Appraiser, now).Length > 0
                || SpecialWorkers(shop, EmployeeRole.Cashier, now).Length > 0);
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
        public bool ListUsedItem(int id,long price)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="secondhand"||price<1||price>1000000)return false;
            var s=SpecialBusiness(CurrentShop.Place.Id);var item=s.Used.FirstOrDefault(i=>i.Id==id&&i.Owned&&i.Inspected&&!i.Sold);
            if(!SetUsedListing(CurrentShop,item,price))return false;
            Say(UsedNames[item.Product]+"已按 ¥"+price+" 上架到二手转售品货架，等待顾客挑选、结账。");return true;
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
                state.NextVisit=minute+SpecialVisitInterval(shop,45);
                if(!state.Used.Any(i=>!i.Owned)&&state.Used.Count(i=>i.Owned&&!i.Sold)<20)state.Used.Add(CreateSeller(state,minute));
            }
            if(!state.AutoPurchase)return;
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
                if(!item.Listed&&shop.Sim.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).UsedKg+UsedWeight(item)<=shop.Sim.ShelfCapacityOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")))
                    SetUsedListing(shop,item,Math.Max(1,UsedValue(item)));
            }
            if(state.Used.Count>80)state.Used.RemoveAll(i=>i.Sold);
        }
        public string KitchenColdStorageBlocker
        {
            get
            {
                if(!InPlayerShop||CurrentShop.RetailKind!="restaurant")return "请到自己的餐馆里整理食材。";
                if(!CanOpenFridge)return "本店还没有摆放冰箱，请先在装修页摆好冰箱。";
                if(IsGameOver)return "当前游戏已经结束，不能整理食材。";
                if(_sleeping)return "请先结束休息，再整理食材。";
                if(IsWorking)return "正在打工，请收工后再整理食材。";
                if(CityActivityExclusive)return "正在参加活动，请结束活动后再整理食材。";
                return null;
            }
        }
        public bool ChillKitchenIngredients()
        {
            // Stocking an installed fridge is preparation, including while the restaurant is closed for renovation.
            string blocker=KitchenColdStorageBlocker;if(blocker!=null)return Fail(blocker);
            var shop=CurrentShop;var fridge=shop.Fridge;double capacity=CurrentFridgeLimitKg;int moved=0;
            foreach(var bag in new[]{shop.Locker,shop.Temp})
            {
                double rice=Math.Min(bag.Food.RiceKg,Math.Max(0,capacity-fridge.WeightKg));
                if(rice>1e-9){bag.Food.RiceKg-=rice;fridge.Food.RiceKg+=rice;moved++;}
                foreach(var batch in bag.Food.Ingredient.ToArray())
                {
                    double unit=Backpack.IngredientKg(batch.Id);int fit=Math.Min(batch.Count,(int)Math.Floor(Math.Max(0,capacity-fridge.WeightKg)/unit+1e-9));
                    if(fit<1)continue;var copy=batch.Clone();copy.Count=fit;fridge.Food.Ingredient.Add(copy);batch.Count-=fit;if(batch.Count==0)bag.Food.Ingredient.Remove(batch);moved+=fit;
                }
            }
            foreach(var line in shop.Sim.Stock.GoodsLines().ToArray())
            {
                var product=Gifts.Get(line.Key);if(product==null||(product.IngredientId==null&&product.Id!="produce:rice"))continue;
                int fit=Math.Min(line.Value,(int)Math.Floor(Math.Max(0,capacity-fridge.WeightKg)/product.WeightKg+1e-9));
                for(int n=0;n<fit;n++)
                {
                    int expiry=shop.Sim.Stock.GoodsExpiry(product.Id);bool chilled=shop.Sim.Stock.GoodsChilled(product.Id);
                    if(!shop.Sim.Stock.TryRemoveGoods(product.Id,1))break;
                    AddRetailToBag(fridge,product,1,expiry,chilled);moved++;
                }
            }
            Chill(fridge);ApplyShelfCapacity();Say(moved>0?"已将店内可容纳的食材放入冰箱，冰箱内永久保存。":"没有可转入的食材，或冰箱已满。");return moved>0;
        }
        private IEnumerable<Backpack> KitchenBags(OwnedShop shop)=>new[]{shop.Fridge,shop.Locker,shop.Temp};
        public int KitchenIngredientCount(OwnedShop shop,string id)=>KitchenBags(shop).Sum(b=>b.Food.Ingredient.Where(f=>f.Id==id).Sum(f=>f.Count))+shop.Sim.Stock.GoodsCount("produce:"+id);
        public double KitchenRice(OwnedShop shop)=>KitchenBags(shop).Sum(b=>b.Food.RiceKg)+shop.Sim.Stock.GoodsCount("produce:rice")*.5;
        private int[] RestaurantMealChoices()
        {
            var shop=CurrentShop;
            if(!SpecialPlayer(shop)||shop.RetailKind!="restaurant"||Furnished(shop,"restaurant_prep")==0
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
            double need=.25;
            foreach(var bag in KitchenBags(shop)){double take=Math.Min(need,bag.Food.RiceKg);bag.Food.RiceKg-=take;need-=take;}
            if(need>1e-9){shop.Sim.Stock.TryRemoveGoods("produce:rice",1);shop.Fridge.Food.RiceKg+=.5-need;}
            foreach(var bag in KitchenBags(shop))
            {
                var batch=bag.Food.Ingredient.FirstOrDefault(b=>b.Id==id&&b.Count>0);
                if(batch==null)continue;if(--batch.Count==0)bag.Food.Ingredient.Remove(batch);return true;
            }
            shop.Sim.Stock.TryRemoveGoods("produce:"+id,1);return true;
        }
        private bool StartRestaurantOrder(OwnedShop shop,SpecialBusinessState state,DiningOrder order,long minute,bool automatic=false,int stove=-1)
        {
            if(order==null||order.Stage!=DiningStage.Waiting||SpecialFitGap(shop)!=null)return false;
            if(state.Orders.Count(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt)>=Furnished(shop,"restaurant_stove"))return automatic?false:Fail("锅位正在使用。");
            var used=state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt).Select(o=>o.Stove).ToArray();
            if(stove<0)stove=Enumerable.Range(0,Furnished(shop,"restaurant_stove")).First(i=>!used.Contains(i));
            if(used.Contains(stove))return false;
            if(!ConsumeRecipe(shop,order.Recipe))return automatic?false:Fail("缺少食材：需要0.25千克大米和1份"+Ingredients.Get(RecipeIngredients[order.Recipe]).Name+"，请补入店内库存或冰箱。");
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
            if(o==null||o.Stage!=DiningStage.Cooking||Clock.Now.TotalMinutes<o.ReadyAt||Clock.Now.TotalMinutes>=o.BurnAt)return Fail("还没熟或已经焦糊。");
            o.Stage=DiningStage.Ready;return true;
        }
        public bool ServeRestaurantOrder(int id)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant")return false;
            var o=SpecialBusiness(CurrentShop.Place.Id).Orders.FirstOrDefault(x=>x.Id==id);
            if(o==null||o.Stage!=DiningStage.Ready)return false;o.Stage=DiningStage.Eating;o.EatUntil=Clock.Now.TotalMinutes+15;return true;
        }
        public bool ClearRestaurantOrder(int id)
        {
            if(!SpecialPlayer(CurrentShop))return false;var s=SpecialBusiness(CurrentShop.Place.Id);var o=s.Orders.FirstOrDefault(x=>x.Id==id);
            if(o==null)return false;
            if(o.Stage==DiningStage.Burnt&&Clock.Now.TotalMinutes-o.Arrived<100){o.Stage=DiningStage.Waiting;o.Stove=-1;return true;}
            if(o.Stage==DiningStage.Completed||o.Stage==DiningStage.Left||o.Stage==DiningStage.Burnt)return s.Orders.Remove(o);return false;
        }
        private void TickRestaurant(OwnedShop shop,SpecialBusinessState state,GameTimestamp now,bool open)
        {
            long minute=now.TotalMinutes;
            foreach(var o in state.Orders.ToArray())
            {
                if(o.Stage==DiningStage.Cooking&&minute>=o.BurnAt)o.Stage=DiningStage.Burnt;
                if((o.Stage==DiningStage.Waiting||o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Ready||o.Stage==DiningStage.Burnt)&&minute-o.Arrived>=100)
                {
                    o.Stage=DiningStage.Left;state.Lost++;
                    shop.Sim.AdjustShopReputation(-.3);
                }
                if(o.Stage==DiningStage.Eating&&minute>=o.EatUntil)
                {
                    Ledger.Credit(now.Date,"餐饮",Money.FromYuan(o.Price),RecipeNames[o.Recipe]+"堂食",LedgerAttribution.ForShop(shop.Place.Id,"meal:"+o.Id+":paid","销售"));
                    state.Revenue+=o.Price;state.Paid++;o.Stage=DiningStage.Completed;
                    shop.Sim.Reputation += ShopSimulation.SaleReputationGain;
                    shop.Sim.AdjustShopReputation(.12);
                    SellRestaurantDrink(shop,state,o,now);
                }
            }
            TickRestaurantWorkers(shop,state,now,open);
            if(!open||minute<state.NextVisit)return;
            state.NextVisit=minute+SpecialVisitInterval(shop,RestaurantVisitMinutes);
            int tables=Furnished(shop,"restaurant_table");
            if (state.Orders.Count >= tables) return;
            if (state.RestaurantArrivalDay != now.Date.TotalDays)
            {
                state.RestaurantArrivalDay = now.Date.TotalDays;
                state.RestaurantArrivalsToday = 0;
            }
            state.RestaurantArrivalsToday++;
            if (!Random.Chance(shop.Sim.MarkupPurchaseIntent(state.MenuMarkup))) return;
            int table=Enumerable.Range(0,tables).FirstOrDefault(index=>!state.Orders.Any(o=>o.Table==index));
            var recipes=Enumerable.Range(0,RecipeNames.Length).Where(i=>(state.RecipeMask&(1<<i))!=0).ToArray();
            if(recipes.Length==0)return;
            int recipe=recipes[Random.Next(0,recipes.Length)];
            state.Orders.Add(new DiningOrder{Id=state.NextId++,Recipe=recipe,Table=table,Stage=DiningStage.Waiting,Arrived=minute,Price=(long)Math.Round(RecipePrices[recipe]*state.MenuMarkup)});
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
                    foreach(var order in state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt))
                    {if(order.Stove<0||used.Contains(order.Stove))order.Stove=Enumerable.Range(0,state.Orders.Count+1).First(i=>!used.Contains(i));used.Add(order.Stove);}
                    _specialBusinesses[saved.ShopId]=state;
                }
            foreach(var shop in MyShops.Where(IsSpecialBusiness))shop.Sim.TradingEnabled=shop.RetailKind=="secondhand";
        }
    }
}
