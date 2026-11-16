using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class ShopCoreGameplayTests
    {
        private static GameSession Make(string kind)
        {
            var s=GameSessionTests.Make(500000,600);s.EventsEnabled=false;
            s.Clock.Restore(new GameDate(15),600);GameSessionTests.Teleport(s,"pc");
            Assert.True(s.SetShopMainCategory(kind));
            s.CurrentShop.Layout.Clear();
            var ids = s.IsSpecialBusiness(s.CurrentShop) ? GameSession.OpeningFurniture(kind) : Array.Empty<string>();
            for(int i=0;i<ids.Length;i++)s.CurrentShop.Layout.Add(new PlacedFurniture{Id=ids[i],X=1+i,Z=2});
            if(kind=="secondhand")Assert.True(s.SetShelfKind(2,"secondhand"));
            typeof(GameSession).GetMethod("UpdateShopFit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(s,null);
            s.CurrentShop.Sim.ForcedClosed=false;
            return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static DiningOrder Order(GameSession s,int id=1)
        {
            var state=s.SpecialBusiness(s.CurrentShop.Place.Id);state.NextVisit=long.MaxValue;
            var order=new DiningOrder{Id=id,Recipe=0,Table=0,Stage=DiningStage.Waiting,Arrived=s.Clock.Now.TotalMinutes,Price=18};state.Orders.Add(order);return order;
        }
        private static void IngredientsFor(GameSession s,int portions=1)
        {
            s.CurrentShop.Locker.Food.RiceKg=portions*.25;
            s.CurrentShop.Fridge.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=portions,ExpireDay=s.Clock.Today.TotalDays+2});
        }
        [Test]
        public void HungryRestaurantOwnerEatsLocalIngredientsWithoutSaleOrCashCharge()
        {
            using(var s=Make("restaurant"))
            {
                var shop=s.CurrentShop;var state=s.SpecialBusiness(shop.Place.Id);state.NextVisit=long.MaxValue;
                shop.Locker.Food.RiceKg=.5;
                shop.Fridge.Food.Ingredient.Add(new FoodBatch{Id="egg",Name="鸡蛋",Count=2,ExpireDay=30});
                var cash=s.Ledger.Balance;s.Player.Hunger=20;
                s.AdvanceClock(1);
                Assert.Greater(s.Player.Hunger,79);Assert.AreEqual(.25,s.KitchenRice(shop),1e-6);
                Assert.AreEqual(1,s.KitchenIngredientCount(shop,"egg"));Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.Zero(state.Paid);Assert.Zero(state.Revenue);Assert.IsEmpty(state.Orders);
                s.AdvanceClock(1);Assert.AreEqual(.25,s.KitchenRice(shop),1e-6,"不能每分钟重复吃饭");
                using(var loaded=Reload(s))
                {
                    loaded.Player.Hunger=20;loaded.AdvanceClock(1);
                    Assert.AreEqual(.25,loaded.KitchenRice(loaded.CurrentShop),1e-6,"读档保留用餐间隔");
                }
            }
        }
        [Test]
        public void RestaurantLunchUsesOneAvailableRecipeAndNeverConsumesAnIncompleteMeal()
        {
            using(var s=Make("restaurant"))
            {
                var shop=s.CurrentShop;s.SpecialBusiness(shop.Place.Id).NextVisit=long.MaxValue;
                IngredientsFor(s,2);
                shop.Fridge.Food.Ingredient.Add(new FoodBatch{Id="egg",Name="鸡蛋",Count=2,ExpireDay=30});
                s.Player.Hunger=50;s.Clock.Restore(s.Clock.Today,719);s.AdvanceClock(2);
                Assert.AreEqual(100,s.Player.Hunger,1e-6);
                Assert.AreEqual(.25,s.KitchenRice(shop),1e-6);
                Assert.AreEqual(3,s.KitchenIngredientCount(shop,"veg")+s.KitchenIngredientCount(shop,"egg"));
                shop.Locker.Food.RiceKg=.1;s.Backpack.Food.Packed.Clear();s.Player.Hunger=20;s.AdvanceClock(31);
                Assert.Less(s.Player.Hunger,20);Assert.AreEqual(.1,s.KitchenRice(shop),1e-6);
                Assert.AreEqual(3,s.KitchenIngredientCount(shop,"veg")+s.KitchenIngredientCount(shop,"egg"));
            }
        }
        [Test]
        public void RestaurantOwnerDoesNotEatRestaurantStockRemotely()
        {
            using(var s=Make("restaurant"))
            {
                var shop=s.CurrentShop;IngredientsFor(s,2);
                GameSessionTests.Teleport(s,s.Home.Id);s.Player.Hunger=20;s.AdvanceClock(1);
                Assert.AreEqual(.5,s.KitchenRice(shop),1e-6);Assert.AreEqual(2,s.KitchenIngredientCount(shop,"veg"));
            }
        }
        private static UsedItem Seller(GameSession s,UsedCondition condition=UsedCondition.Good)
        {
            var state=s.SpecialBusiness(s.CurrentShop.Place.Id);state.NextVisit=long.MaxValue;
            var item=new UsedItem{Id=state.NextId++,Product=0,Condition=condition,Claim=UsedCondition.Good,Asking=80,Minimum=40,ExpiresAt=s.Clock.Now.TotalMinutes+90};state.Used.Add(item);return item;
        }
        [Test] public void OnlyPlannedNewTypesAreOffered_AndLegacyTypesStillLoad()
        {
            CollectionAssert.AreEquivalent(new[]{"computer","models","clothing","restaurant","secondhand","pet"},RetailShopTypes.All);
            foreach(string legacy in new[]{"food","flowers","produce"})Assert.AreEqual(legacy,RetailShopTypes.Valid(legacy));
        }
        [TestCase("model:harbor:deck",20)]
        [TestCase("produce:veg",80)]
        [TestCase("fast:rice",80)]
        [TestCase("flower_rose",40)]
        public void WholesaleCapsAreFiniteAndShared(string product,int cap)
        {
            using(var s=GameSessionTests.Make())
            {
                cap=s.SupplierCapacity("source",product);Assert.AreEqual(cap,s.CitySupplierRemaining("source",product));
                s.RecordCitySupplierPurchase("source",product,cap-1);Assert.AreEqual(1,s.CitySupplierRemaining("source",product));
                s.RecordCitySupplierPurchase("source",product,1);Assert.Zero(s.CitySupplierRemaining("source",product));
                using(var loaded=Reload(s)){Assert.Zero(loaded.CitySupplierRemaining("source",product));loaded.Clock.Restore(new GameDate(1),600);Assert.AreEqual(cap,loaded.CitySupplierRemaining("source",product));}
            }
        }
        [Test] public void ModelHeatHasRunupPeakAndCrashWhileClothingIsStableBetweenRareEvents()
        {
            Assert.AreEqual(1,IndustryCycles.ModelFactor("harbor",0));
            Assert.Greater(IndustryCycles.ModelFactor("harbor",16),IndustryCycles.ModelFactor("harbor",10));
            Assert.Less(IndustryCycles.ModelFactor("harbor",32),1);
            Assert.AreNotEqual(IndustryCycles.ModelFactor("harbor",16),IndustryCycles.ModelFactor("garden",16));
            Assert.AreEqual(1,IndustryCycles.ClothingFactor(30));Assert.AreEqual(1,IndustryCycles.ClothingFactor(31));
            Assert.AreEqual(1.35,IndustryCycles.ClothingFactor(70));Assert.AreEqual(1.5,IndustryCycles.ClothingFactor(250));Assert.AreEqual(1,IndustryCycles.ClothingFactor(280));
        }
        [Test] public void IndustryPricingUsesSameBasisForQuotePreviewAndConfirmation()
        {
            using(var s=Make("models"))
            {
                var model=ModelProducts.Regular.First();var sim=s.PlayerShop;
                s.Clock.Restore(new GameDate(16),600);
                var range=sim.GoodsPriceRange(model.Id);
                Assert.True(sim.SetGoodsSalePrice(model.Id,range.max));
                Assert.AreEqual(range.max,sim.GoodsPriceOf(model.Id).Yuan);
                Assert.That(sim.GoodsPurchaseIntentAtPrice(model.Id,range.max),Is.InRange(0,1));
            }
        }
        [Test] public void RestaurantRequiresKitchenAndCashCounterWithoutRetailShelf()
        {
            using(var s=Make("restaurant"))
            {
                Assert.IsNull(s.SpecialFitGap(s.CurrentShop));Assert.True(s.ShopFitted);
                var counter = s.CurrentShop.Layout.Single(p => p.Id == FurnitureCatalog.ShopCounter);
                s.CurrentShop.Layout.Remove(counter);
                Assert.AreEqual("收银台", s.SpecialFitGap(s.CurrentShop));
                Assert.False(s.ShopFitted);
                Assert.False(s.SpecialOpen(s.CurrentShop, s.Clock.Now));
                s.CurrentShop.Layout.Add(counter);
                s.CurrentShop.Layout.RemoveAll(p=>p.Id=="restaurant_stove");Assert.AreEqual("餐馆炒锅灶台",s.SpecialFitGap(s.CurrentShop));Assert.False(s.ShopFitted);
            }
        }
        [Test] public void CookingConsumesRealIngredientsOnceAndRequiresFreeStove()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s,2);var first=Order(s);var second=Order(s,2);
                Assert.True(s.StartRestaurantCooking(first.Id));Assert.False(s.StartRestaurantCooking(first.Id));Assert.False(s.StartRestaurantCooking(second.Id));
                Assert.AreEqual(.25,s.KitchenRice(s.CurrentShop));Assert.AreEqual(1,s.KitchenIngredientCount(s.CurrentShop,"veg"));
                Assert.False(s.PlateRestaurantOrder(first.Id));Assert.False(s.ServeRestaurantOrder(first.Id));
            }
        }
        [Test] public void ClearingTableRequiresInstalledCounterAndChargesOnlyOnce()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);
                var state = s.SpecialBusiness("pc");
                state.RecipeMask = 1;
                state.NextFishOrder = long.MaxValue;
                Until(s, () => state.Orders.Count > 0);
                var order = state.Orders.Single();
                Assert.AreEqual(36, order.Price);
                double standing = s.PlayerShop.Reputation;
                state.NextVisit = long.MaxValue;
                state.MenuMarkup = 1.7;
                var balance = s.Ledger.Balance;
                Assert.True(s.StartRestaurantCooking(order.Id));s.Clock.Advance(8);
                Assert.True(s.PlateRestaurantOrder(order.Id));Assert.True(s.ServeRestaurantOrder(order.Id));Assert.False(s.ServeRestaurantOrder(order.Id));
                Assert.AreEqual(balance,s.Ledger.Balance);s.Clock.Advance(15);
                Assert.AreEqual(DiningStage.Checkout,order.Stage);Assert.AreEqual(balance,s.Ledger.Balance);
                var counter = s.CurrentShop.Layout.Single(p => p.Id == FurnitureCatalog.ShopCounter);
                s.CurrentShop.Layout.Remove(counter);
                Assert.False(s.ClearRestaurantOrder(order.Id));
                Assert.AreEqual(balance,s.Ledger.Balance);
                s.CurrentShop.Layout.Add(counter);
                Assert.True(s.ClearRestaurantOrder(order.Id));Assert.False(s.ClearRestaurantOrder(order.Id));
                Assert.AreEqual(balance+Money.FromYuan(36),s.Ledger.Balance);Assert.IsEmpty(state.Orders);
                Assert.AreEqual(standing, s.PlayerShop.Reputation);
            }
        }
        [Test]
        public void FishPreorderUsesRealIngredientsAndAwardsStandingOnceWhenDelivered()
        {
            using var s = Make("restaurant");
            var state = s.SpecialBusiness("pc");
            state.RecipeMask = 1;
            state.MenuMarkup = 1.2;
            s.AdvanceClock(20);
            var order = state.Orders.Single(o => o.IsFishOrder);
            Assert.AreEqual(-1, order.Table);
            Assert.AreEqual(7 * GameTimestamp.MinutesPerDay, order.DueAt - order.Arrived);
            Assert.AreEqual(2 * (43 + order.Fish.DinnerSurcharge), order.Price);
            IngredientsFor(s);
            Assert.False(s.StartRestaurantCooking(order.Id));
            Assert.AreEqual(.25, s.KitchenRice(s.CurrentShop));
            Assert.AreEqual(1, s.KitchenIngredientCount(s.CurrentShop, "veg"));
            s.Backpack.Food.AddIngredient(order.FishId, 1);
            Assert.True(s.MoveFridgeFood("ing:" + order.FishId, 1, true));
            Assert.True(s.StartRestaurantCooking(order.Id));
            Assert.Zero(s.KitchenIngredientCount(s.CurrentShop, order.FishId));
            Assert.False(s.HandInRestaurantFishOrder(order.Id));
            s.AdvanceClock(8);
            Assert.True(s.PlateRestaurantOrder(order.Id));
            Assert.False(s.ServeRestaurantOrder(order.Id));
            using var loaded = Reload(s);
            var restored = loaded.SpecialBusiness("pc").Orders.Single(o => o.IsFishOrder);
            Assert.AreEqual(order.FishId, restored.FishId);
            Assert.AreEqual(order.DueAt, restored.DueAt);
            Assert.AreEqual(order.Price, restored.Price);
            loaded.Clock.Restore(loaded.Clock.Today, 22 * 60 + 10);
            loaded.AdvanceClock(1);
            Assert.AreEqual(DiningStage.Ready, restored.Stage);
            var cash = loaded.Ledger.Balance;
            double reputation = loaded.PlayerShop.Reputation;
            Assert.True(loaded.HandInRestaurantFishOrder(order.Id));
            Assert.False(loaded.HandInRestaurantFishOrder(order.Id));
            Assert.AreEqual(cash + Money.FromYuan(order.Price), loaded.Ledger.Balance);
            Assert.AreEqual(reputation + order.ReputationReward, loaded.PlayerShop.Reputation, .000001);
        }

        [Test]
        public void LegacySaveDoublesPendingFishQuotesOnce()
        {
            using var s = Make("restaurant");
            var state = s.SpecialBusiness("pc");
            long now = s.Clock.Now.TotalMinutes;
            state.Orders.Add(new DiningOrder { Id = 1, FishId = "fish_mandarin", Table = -1, Stage = DiningStage.Waiting, DueAt = now + 800, Price = 581 });
            state.Orders.Add(new DiningOrder { Id = 2, FishId = "fish_mandarin", Table = -1, Stage = DiningStage.Ready, DueAt = now + 900, Price = 244 });
            state.Orders.Add(new DiningOrder { Id = 3, Table = 0, Stage = DiningStage.Waiting, Price = 56 });
            state.Revenue = 1200;
            var save = s.CreateSave();
            save.restaurantFishOrderPriceVersion = 0;

            using var restored = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
            var restoredState = restored.SpecialBusiness("pc");
            CollectionAssert.AreEqual(new long[] { 1162, 488, 56 }, restoredState.Orders.Select(o => o.Price));
            Assert.AreEqual(1200, restoredState.Revenue);
            Assert.AreEqual(581, save.specialBusinesses.Single(x => x.ShopId == "pc").Orders[0].Price);
            using var reloaded = Reload(restored);
            CollectionAssert.AreEqual(new long[] { 1162, 488, 56 }, reloaded.SpecialBusiness("pc").Orders.Select(o => o.Price));
        }

        [Test]
        public void FishPreordersOfferOneOrTwoPerOpenDayEvenWhenDeclinedAndReloaded()
        {
            using var s = Make("restaurant");
            var firstDay = s.Clock.Today;

            int DeclineOffer(GameSession session, GameDate day, int minute)
            {
                var state = session.SpecialBusiness("pc");
                session.Clock.Restore(day, minute);
                state.NextVisit = session.Clock.Now.TotalMinutes + 1;
                session.AdvanceClock(1);
                var order = state.Orders.SingleOrDefault(o => o.IsFishOrder);
                if (order == null) return 0;
                Assert.True(session.DeclineRestaurantFishOrder(order.Id));
                return 1;
            }

            Assert.AreEqual(1, DeclineOffer(s, firstDay, 600));
            long nextOffer = s.SpecialBusiness("pc").NextFishOrder;
            using var loaded = Reload(s);
            Assert.AreEqual(nextOffer, loaded.SpecialBusiness("pc").NextFishOrder);
            Assert.AreEqual(0, DeclineOffer(loaded, firstDay, 601));
            Assert.AreEqual(nextOffer / GameTimestamp.MinutesPerDay == firstDay.TotalDays ? 1 : 0,
                DeclineOffer(loaded, firstDay, 850));
            Assert.AreEqual(0, DeclineOffer(loaded, firstDay, 1110));

            for (int day = 1; day <= 6; day++)
            {
                var date = firstDay.AddDays(day);
                Assert.AreEqual(1, DeclineOffer(loaded, date, 600));
                Assert.That(DeclineOffer(loaded, date, 850), Is.InRange(0, 1));
                Assert.AreEqual(0, DeclineOffer(loaded, date, 1110));
            }
        }

        [Test]
        public void FishPreordersStopAtTwoAndFreeASlotAtTheDeadline()
        {
            using var s = Make("restaurant");
            var state = s.SpecialBusiness("pc");
            void CheckMorning(GameDate day)
            {
                s.Clock.Restore(day, 600);
                state.NextVisit = s.Clock.Now.TotalMinutes + 1;
                s.AdvanceClock(1);
            }
            var day = s.Clock.Today;
            CheckMorning(day);
            var first = state.Orders.Single(o => o.IsFishOrder);
            CheckMorning(day.AddDays(2));
            CheckMorning(day.AddDays(4));
            Assert.AreEqual(2, state.Orders.Count(o => o.IsFishOrder));
            Assert.Contains(first, state.Orders);
            CheckMorning(day.AddDays(7));
            Assert.False(state.Orders.Contains(first));
            Assert.AreEqual(2, state.Orders.Count(o => o.IsFishOrder));
            Assert.False(s.HandInRestaurantFishOrder(first.Id));
            Assert.AreEqual(0, s.PlayerShop.Reputation);
        }
        [Test] public void FoodBurnsIfIgnoredAndRemakingNeedsAnotherPortion()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);Assert.True(s.StartRestaurantCooking(order.Id));s.Clock.Advance(38);
                Assert.AreEqual(DiningStage.Burnt,order.Stage);Assert.False(s.PlateRestaurantOrder(order.Id));Assert.True(s.ClearRestaurantOrder(order.Id));
                Assert.False(s.StartRestaurantCooking(order.Id));Assert.Zero(s.SpecialBusiness("pc").Revenue);
            }
        }
        [Test] public void HungryWaitingCustomerLeavesWithoutIncome()
        {
            using(var s=Make("restaurant")){var order=Order(s);s.Clock.Advance(100);Assert.AreEqual(DiningStage.Left,order.Stage);Assert.Zero(s.SpecialBusiness("pc").Revenue);}
        }
        [Test] public void InFlightCookingSurvivesJsonWithoutRefreshingFireOrDuplicatingPayment()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);Assert.True(s.StartRestaurantCooking(order.Id));s.Clock.Advance(4);
                using(var loaded=Reload(s))
                {
                    var restored=loaded.SpecialBusiness("pc").Orders.Single();Assert.AreEqual(order.ReadyAt,restored.ReadyAt);Assert.AreEqual(order.BurnAt,restored.BurnAt);
                    loaded.Clock.Advance(4);Assert.True(loaded.PlateRestaurantOrder(order.Id));Assert.True(loaded.ServeRestaurantOrder(order.Id));loaded.Clock.Advance(15);
                    using(var again=Reload(loaded))
                    {
                        var balance=again.Ledger.Balance;
                        Until(again,()=>again.SpecialBusiness("pc").Orders.Any(o => o.Id == order.Id && o.Stage == DiningStage.Checkout));
                        Assert.AreEqual(balance,again.Ledger.Balance);
                        Assert.True(again.ClearRestaurantOrder(order.Id));
                        using(var paid=Reload(again))
                        {
                            Assert.False(paid.ClearRestaurantOrder(order.Id));
                            Assert.AreEqual(balance+Money.FromYuan(order.Price),paid.Ledger.Balance);
                        }
                    }
                }
            }
        }
        [TestCase(UsedCondition.Good)] [TestCase(UsedCondition.Flawed)] [TestCase(UsedCondition.Replica)] [TestCase(UsedCondition.Broken)]
        public void UsedGoodsCanOnlyBeInspectedAfterPurchase_ConditionNeverRerolls(UsedCondition condition)
        {
            using(var s=Make("secondhand"))
            {
                var item=Seller(s,condition);var balance=s.Ledger.Balance;
                Assert.False(s.InspectUsed(item.Id));Assert.False(s.OfferUsedPrice(item.Id,1));Assert.AreEqual(balance,s.Ledger.Balance);
                Assert.True(s.OfferUsedPrice(item.Id,40));Assert.False(s.OfferUsedPrice(item.Id,40));Assert.AreEqual(balance-Money.FromYuan(40),s.Ledger.Balance);
                Assert.False(s.ListUsedItem(item.Id,100));Assert.True(s.InspectUsed(item.Id));Assert.False(s.InspectUsed(item.Id));
                Assert.AreEqual(condition,item.Condition);
                using(var loaded=Reload(s)){var restored=loaded.SpecialBusiness("pc").Used.Single();Assert.AreEqual(condition,restored.Condition);Assert.True(restored.Inspected);Assert.AreEqual(40,restored.Cost);}
            }
        }
        [Test] public void UsedSellingRequiresShelfWeightCapacityAndRetainsLossRisk()
        {
            using(var s=Make("secondhand"))
            {
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"secondhand",4.8}});
                for(int n=0;n<5;n++)
                {
                    var item=Seller(s,UsedCondition.Broken);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.True(s.InspectUsed(item.Id));
                    Assert.Less(GameSession.UsedValue(item),item.Cost);
                    Assert.AreEqual(n<4,s.ListUsedItem(item.Id,GameSession.UsedValue(item)));
                }
            }
        }
        private static UsedItem ListedUsed(GameSession s)
        {
            var item=Seller(s);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.True(s.InspectUsed(item.Id));
            Assert.True(s.ListUsedItem(item.Id,100));return item;
        }
        private static void WaitForUsedBuyer(GameSession s)
        {
            var sim=s.PlayerShop;sim.Settings.BaseCustomersPerHour=600;
            sim.Settings.MinBrowseMinutes=1;sim.Settings.MaxBrowseMinutes=1;sim.Settings.CashCarryChance=1;
            sim.Settings.PatienceMinutes=1000;sim.CustomerCanBrowse=c=>true;
            for(int n=0;n<90&&sim.Queue.Count==0;n++)s.AdvanceClock(1);
            Assert.That(sim.Queue.Count,Is.EqualTo(1),"顾客应从二手货架拿货并排队结账");
        }
        [TestCase(false)][TestCase(true)]
        public void UsedShelfReservesUniqueItemAndOnlyCheckoutPaysOnce(bool reload)
        {
            using(var initial=Make("secondhand"))
            {
                var item=ListedUsed(initial);var cash=initial.Ledger.Balance;
                Assert.AreEqual(1,initial.PlayerShop.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).Total);
                WaitForUsedBuyer(initial);
                using(var loaded=reload?Reload(initial):null)
                {
                    var s=loaded??initial;var sim=s.PlayerShop;var state=s.SpecialBusiness("pc");
                    var held=state.Used.Single(i=>i.Id==item.Id);var customer=sim.Queue.Single();
                    Assert.AreEqual(item.Id,customer.ResaleId);Assert.AreEqual(cash,s.Ledger.Balance);
                    Assert.False(held.Sold);Assert.Zero(state.Paid);
                    Assert.Zero(sim.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).Total);
                    Assert.False(s.ListUsedItem(item.Id,200));Assert.False(s.UnlistUsedItem(item.Id));Assert.False(s.DiscardUsed(item.Id));
                    s.AdvanceClock(8);Assert.AreEqual(1,sim.Queue.Count,"唯一物品不能同时被多名顾客拿走");
                    Assert.GreaterOrEqual(sim.Checkout(customer.Id),0);
                    Assert.True(held.Sold);Assert.False(held.Listed);Assert.AreEqual(100,held.SalePrice);
                    Assert.AreEqual(cash+Money.FromYuan(100),s.Ledger.Balance);Assert.AreEqual(1,state.Paid);
                    Assert.AreEqual(100,state.Revenue);Assert.AreEqual(-1,sim.Checkout(customer.Id));
                    Assert.AreEqual(0, sim.Reputation, "普通旧物成交只改变门店指标");
                    Assert.AreEqual(cash+Money.FromYuan(100),s.Ledger.Balance);
                }
            }
        }
        [Test] public void AbandonedUsedPurchaseReturnsToShelfAndNeverPays()
        {
            using(var s=Make("secondhand"))
            {
                var item=ListedUsed(s);var cash=s.Ledger.Balance;WaitForUsedBuyer(s);
                s.PlayerShop.ClearCustomers();
                Assert.False(item.Sold);Assert.True(item.Listed);Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.AreEqual(1,s.PlayerShop.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).Total);
                Assert.True(s.UnlistUsedItem(item.Id));Assert.Zero(s.PlayerShop.ShelfFillOf(PCShopSell.Core.Inventory.ShelfKinds.Get("secondhand")).Total);
            }
        }
        [TestCase(UsedCondition.Good, 10000, 1)]
        [TestCase(UsedCondition.Replica, 150, 0)]
        public void ArtifactReputationIsGrantedOnceAtPaidCheckoutAfterReload(UsedCondition condition, long expectedPrice, double expectedReputation)
        {
            using var first = Make("secondhand");
            var item = Seller(first, condition);
            item.Product = GameSession.ArtifactProduct;
            item.Minimum = 2300;
            item.Asking = 2700;
            Assert.False(first.OfferUsedPrice(item.Id, 2000));
            Assert.True(first.OfferUsedPrice(item.Id, 2700));
            Assert.True(first.InspectUsed(item.Id));
            Assert.AreEqual(expectedPrice, item.Price);
            Assert.AreEqual(expectedReputation, GameSession.UsedSaleReputation(item));
            Assert.True(first.ListUsedItem(item.Id, item.Price));
            WaitForUsedBuyer(first);
            Assert.Zero(first.PlayerShop.Reputation);

            using var loaded = Reload(first);
            var customer = loaded.PlayerShop.Queue.Single();
            Assert.AreEqual(item.Id, customer.ResaleId);
            var cash = loaded.Ledger.Balance;
            Assert.GreaterOrEqual(loaded.PlayerShop.Checkout(customer.Id), 0);
            Assert.AreEqual(cash + Money.FromYuan(expectedPrice), loaded.Ledger.Balance);
            Assert.AreEqual(expectedReputation, loaded.PlayerShop.Reputation, 1e-9);
            Assert.Less(loaded.PlayerShop.Checkout(customer.Id), 0);
            using var sold = Reload(loaded);
            Assert.AreEqual(expectedReputation, sold.PlayerShop.Reputation, 1e-9);
            Assert.True(sold.SpecialBusiness("pc").Used.Single(i => i.Id == item.Id).Sold);
        }

        [Test]
        public void ArtifactSellersAreRareAndBothAuthenticityOutcomesRequireHighOffers()
        {
            using var s = Make("secondhand");
            var state = s.SpecialBusiness("pc");
            var createSeller = typeof(GameSession).GetMethod("CreateSeller", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var arguments = new object[] { state, s.Clock.Now.TotalMinutes };
            var artifacts = Enumerable.Range(0, 20000).Select(_ => (UsedItem)createSeller.Invoke(s, arguments))
                .Where(item => item.Product == GameSession.ArtifactProduct).ToArray();
            Assert.That(artifacts.Length / 20000.0, Is.InRange(.016, .024));
            Assert.That(artifacts.Count(item => item.Condition == UsedCondition.Good) / (double)artifacts.Length,
                Is.InRange(.06, .14));
            Assert.True(artifacts.All(item => item.Minimum > 2000 && item.Asking > item.Minimum));
        }

        [TestCase(95, 0)]
        [TestCase(96, .2)]
        public void PetWeeklyPaymentAwardsReputationOnlyAboveNinetyFiveAndCannotRepeat(int condition, double expected)
        {
            using var original = Make(PetShopCatalog.ShopKind);
            var ownerId = original.CityPeople.First().PersonId;
            var save = original.CreateSave();
            save.petBoarding = new[] { new PetBoardingState { ShopId = "pc", Stays = new[] {
                new PetStay { Id = 1, OwnerId = ownerId, Name = "团子", Weeks = 2, Condition = condition,
                    NextVisitAt = original.Clock.Now.TotalMinutes, LastConditionDay = original.Clock.Today.TotalDays }
            } } };
            using var loaded = GameSession.Load(original.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
            GameSessionTests.Teleport(loaded, "pc");
            var owner = loaded.CityPersonState(ownerId);
            owner.Route = null;
            owner.LocationPlaceId = "pc";
            owner.Task = CityPersonTask.Shopping;
            owner.TaskLabel = "到店办理宠物代养";
            Assert.True(loaded.SettlePetStay(1));
            Assert.AreEqual(expected, loaded.PlayerShop.Reputation, 1e-9);
            Assert.False(loaded.SettlePetStay(1));
            using var reloaded = Reload(loaded);
            Assert.AreEqual(expected, reloaded.PlayerShop.Reputation, 1e-9);
            Assert.AreEqual(1, reloaded.PetGuests(reloaded.MainShop).Single().PaidWeeks);
        }

        [Test] public void UsedShopNeedsRegularShelfAndCounterAndCanRejectSellerWithoutCharge()
        {
            using(var s=Make("secondhand"))
            {
                var item=Seller(s);var cash=s.Ledger.Balance;Assert.True(s.DeclineUsedSeller(item.Id));
                Assert.IsEmpty(s.SpecialBusiness("pc").Used);Assert.AreEqual(cash,s.Ledger.Balance);
                item=Seller(s);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.True(s.InspectUsed(item.Id));
                Assert.True(s.SetShelfKind(2,"drink"));Assert.False(s.ListUsedItem(item.Id,100));
                s.CurrentShop.Layout.RemoveAll(p=>p.Id==FurnitureCatalog.Shelf);Assert.IsNotNull(s.SpecialFitGap(s.CurrentShop));
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf,X=2,Z=3});
                s.CurrentShop.Layout.RemoveAll(p=>p.Id==FurnitureCatalog.ShopCounter);Assert.IsNotNull(s.SpecialFitGap(s.CurrentShop));
            }
        }
        [Test] public void UsedCounterMovesConfirmedItemsToIllustratedShelfAndShowsSoldReceipt()
        {
            using(var s=Make("secondhand"))
            {
                var root=new GameObject("UsedResaleUi");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(controller);
                UnityEngine.UI.Button Button(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).SingleOrDefault(b=>b.name==name);
                try
                {
                    var refused=Seller(s);ui.OpenDock();ui.OpenUsedDesk();Button("DeclineUsed_"+refused.Id).onClick.Invoke();
                    Assert.IsEmpty(s.SpecialBusiness("pc").Used);
                    Assert.True(ui.CloseTopWindow());
                    var item=Seller(s);
                    var nextSeller=Seller(s);
                    ui.OpenUsedDesk();
                    Vector3[] Corners(string name)
                    {
                        Canvas.ForceUpdateCanvases();
                        var corners = new Vector3[4];
                        Button(name).image.rectTransform.GetWorldCorners(corners);
                        Assert.Greater(Vector3.Distance(corners[0], corners[2]), 0);
                        return corners;
                    }
                    var buyPosition = Corners("BuyUsed_" + item.Id);
                    Button("BuyUsed_"+item.Id).onClick.Invoke();
                    Assert.True(item.Owned);
                    CollectionAssert.AreEqual(buyPosition, Corners("InspectUsed_" + item.Id));
                    Button("InspectUsed_"+item.Id).onClick.Invoke();
                    Assert.True(item.Inspected);
                    CollectionAssert.AreEqual(buyPosition, Corners("ListUsed_" + item.Id));
                    Assert.True(s.SetShelfKind(2,"drink"));
                    Button("ListUsed_"+item.Id).onClick.Invoke();
                    var warning = ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>())
                        .Single(t=>t.name=="Window_暂时无法上架");
                    Assert.True(warning.GetComponentsInChildren<UnityEngine.UI.Text>()
                        .Any(t=>t.text.Contains("二手转售品") && t.text.Contains("按 F")));
                    Assert.False(item.Listed);
                    Assert.True(ui.CloseTopWindow());
                    Assert.True(s.SetShelfKind(2,"secondhand"));
                    Button("ListUsed_"+item.Id).onClick.Invoke();Assert.True(item.Listed);Assert.IsNull(Button("ListUsed_"+item.Id));
                    CollectionAssert.AreEqual(buyPosition, Corners("BuyUsed_" + nextSeller.Id));
                    Assert.True(ui.CloseTopWindow());
                    ui.DebugSelectTab(2);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedStock_"+item.Id));
                    ui.OpenShelfWindow(2);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedIllustration_"+item.Id));
                    ui.CloseAllWindows();WaitForUsedBuyer(s);s.PlayerShop.Checkout(s.PlayerShop.Queue.Single().Id);ui.OpenShelfWindow(2);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedSold_"+item.Id));
                    Assert.IsNull(Button("ListUsed_"+item.Id));
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }

        [Test]
        public void UsedSellersQueueUpToFiveAndWaitEightHoursThroughSaveAndClosing()
        {
            using var s = Make("secondhand");
            s.PlayerShop.Settings.BaseCustomersPerHour = 0;
            var state = s.SpecialBusiness("pc");
            long firstArrival = s.Clock.Now.TotalMinutes + 1;
            for (int visit = 0; visit < GameSession.UsedSellerQueueLimit + 1; visit++)
            {
                state.NextVisit = s.Clock.Now.TotalMinutes + 1;
                s.AdvanceClock(1);
            }
            Assert.AreEqual(5, state.Used.Count);
            Assert.AreEqual(firstArrival + 8 * 60, state.Used[0].ExpiresAt);
            state.NextVisit = s.Clock.Now.TotalMinutes + 1;
            s.PlayerShop.SetHours(8 * 60, s.Clock.MinuteOfDay);
            s.AdvanceClock(1);
            Assert.True(s.ShopFinishingService(s.CurrentShop));
            Assert.True(s.OfferUsedPrice(state.Used[2].Id, state.Used[2].Asking));
            s.AdvanceClock(1);
            Assert.AreEqual(4, state.Used.Count(item => item.WaitingForPurchase));

            using var loaded = Reload(s);
            var waiting = loaded.SpecialBusiness("pc").Used.Where(item => item.WaitingForPurchase).ToArray();
            CollectionAssert.AreEqual(state.Used.Where(item => item.WaitingForPurchase).Select(item => item.Id), waiting.Select(item => item.Id));
            loaded.AdvanceClock((int)(waiting[0].ExpiresAt - loaded.Clock.Now.TotalMinutes) - 1);
            Assert.Contains(waiting[0], loaded.SpecialBusiness("pc").Used);
            loaded.AdvanceClock(1);
            Assert.False(loaded.SpecialBusiness("pc").Used.Contains(waiting[0]));
            Assert.Contains(waiting[1], loaded.SpecialBusiness("pc").Used);
        }
        [Test] public void ListedUsedItemCannotSellWithoutOrdinaryCustomerCheckout()
        {
            using(var s=Make("secondhand"))
            {
                var item=ListedUsed(s);s.PlayerShop.Settings.BaseCustomersPerHour=0;var cash=s.Ledger.Balance;
                s.AdvanceClock(180);Assert.False(item.Sold);Assert.True(item.Listed);
                Assert.Zero(s.SpecialBusiness("pc").Paid);Assert.AreEqual(cash,s.Ledger.Balance);
            }
        }
        [Test] public void UsedBuyerVisiblyWalksFromStreetToShelfThenHoldsGoodsAtCheckout()
        {
            using(var s=Make("secondhand"))
            {
                var item=ListedUsed(s);var host=new GameObject("UsedBuyerRoute");
                try
                {
                    var view=new PCShopSell.Game.Runtime.ShopInteriorView(host.transform,s.PlayerShop.Size);
                    view.SetActive(true);view.Update(s,0,0);
                    Assert.True(host.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="LampShade"),"二手货架显示旧物模型");
                    WaitForUsedBuyer(s);s.PlayerShop.Settings.BaseCustomersPerHour=0;
                    var buyer=s.PlayerShop.Queue.Single();buyer.PersonId=null; // Ambient visitor: exercise the indoor route without a city commute.
                    var visitors=(System.Collections.IDictionary)typeof(PCShopSell.Game.Runtime.ShopInteriorView).GetField("_visitors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);
                    for(int n=0;n<1800;n++)
                    {
                        view.Update(s,.1f,.1f);
                        if(visitors.Contains(buyer.Id)&&(int)visitors[buyer.Id].GetType().GetField("Phase").GetValue(visitors[buyer.Id])==2)break;
                    }
                    Assert.True(visitors.Contains(buyer.Id));var figure=visitors[buyer.Id];
                    Assert.AreEqual(2,(int)figure.GetType().GetField("Phase").GetValue(figure),"顾客实际走到货架取货后进入排队状态");
                    Assert.NotNull(figure.GetType().GetField("Held").GetValue(figure));Assert.False(item.Sold);
                    s.PlayerShop.Checkout(buyer.Id);view.Update(s,.1f,.1f);Assert.True(item.Sold);
                    Assert.False(visitors.Contains(buyer.Id));
                }
                finally{UnityEngine.Object.DestroyImmediate(host);}
            }
        }
        [Test] public void AppraiserRoleAndIntelligencePersistThroughTransferAndSave()
        {
            using(var s=Make("secondhand"))
            {
                var worker=new Employee{Name="聪明收购员",Intelligence=94,DailyWageYuan=160,CheckoutMinutes=3};s.CurrentShop.Employee=worker;
                Assert.True(s.ArrangeEmployeeWork(worker.Id,"pc",EmployeeRole.Appraiser));Assert.AreEqual(1,s.Employees.Count);
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(EmployeeRole.Appraiser,loaded.FindEmployee(worker.Id).Role);Assert.AreEqual(94,loaded.FindEmployee(worker.Id).Person.Intelligence);
                    Assert.True(loaded.ArrangeEmployeeWork(worker.Id,"pc",EmployeeRole.Cashier));Assert.AreEqual(1,loaded.Employees.Count);
                }
                Assert.Greater(GameSession.AppraiserAccuracy(94),GameSession.AppraiserAccuracy(30));Assert.Less(GameSession.AppraiserAccuracy(100),1);
            }
        }

        [Test]
        public void AppraiserAndSellerStandOnOppositeSidesOfTheMovedPurchaseDesk()
        {
            using var s = Make("secondhand");
            var worker = new Employee { Name = "收购师", DailyWageYuan = 130 };
            s.CurrentShop.Employee = worker;
            Assert.True(s.ArrangeEmployeeWork(worker.Id, "pc", EmployeeRole.Appraiser));
            CityTestPeople.AtAssignedWork(s, worker);
            var seller = Seller(s);
            var nextSeller = Seller(s);
            var desk = s.CurrentShop.Layout.Single(furniture => furniture.Id == "used_buydesk");
            var root = new GameObject("UsedDeskPositions");
            try
            {
                var view = new PCShopSell.Game.Runtime.ShopInteriorView(root.transform, s.PlayerShop.Size);
                void Walk()
                {
                    for (int i = 0; i < 60; i++) view.Update(s, .1f, .1f);
                }
                void CheckPositions(Vector3 back)
                {
                    Walk();
                    var figures = root.GetComponentsInChildren<Transform>();
                    var appraiser = figures.Single(t => t.name == "ShopEmployee_" + worker.Id);
                    var visitor = figures.Single(t => t.name == "SpecialCustomer_seller:" + seller.Id);
                    var center = new Vector3((float)desk.X, 0, (float)desk.Z);
                    Assert.That(Vector3.Dot(appraiser.localPosition - center, back), Is.InRange(.5f, 1.1f));
                    Assert.That(Vector3.Dot(visitor.localPosition - center, back), Is.InRange(-1.1f, -.5f));
                    Assert.Greater(Vector3.Dot(appraiser.localRotation * Vector3.forward,
                        (center - appraiser.localPosition).normalized), .99f);
                    var waiting = figures.Single(t => t.name == "SpecialCustomer_seller:" + nextSeller.Id);
                    Assert.That(Vector3.Distance(visitor.localPosition, waiting.localPosition), Is.GreaterThan(.85f));
                    Assert.That(waiting.localPosition.z, Is.GreaterThan(0));
                }

                desk.X = 2.5;
                desk.Z = 4.2;
                CheckPositions(Vector3.forward);
                desk.X = 4;
                desk.Rot = 1;
                CheckPositions(Vector3.right);

                var newSeller = Seller(s);
                view.Update(s, 0, 0);
                var newcomer = root.GetComponentsInChildren<Transform>().Single(t => t.name == "SpecialCustomer_seller:" + newSeller.Id);
                Assert.Less(newcomer.localPosition.z, 0);
                Assert.True(s.DeclineUsedSeller(seller.Id));
                Walk();
                var queue = root.GetComponentsInChildren<Transform>();
                Assert.False(queue.Any(t => t.name == "SpecialCustomer_seller:" + seller.Id));
                Assert.Greater(newcomer.localPosition.z, 0);
                var head = queue.Single(t => t.name == "SpecialCustomer_seller:" + nextSeller.Id);
                Assert.That(head.localPosition.x - (float)desk.X, Is.InRange(-1.1f, -.5f));

                for (int i = 2; i < GameSession.UsedSellerQueueLimit; i++) Seller(s);
                Walk();
                var positions = root.GetComponentsInChildren<Transform>()
                    .Where(t => t.name.StartsWith("SpecialCustomer_seller:")).Select(t => t.localPosition).ToArray();
                Assert.AreEqual(GameSession.UsedSellerQueueLimit, positions.Length);
                Assert.True(positions.All(position => position.z > 0));
                for (int i = 0; i < positions.Length; i++)
                    for (int j = i + 1; j < positions.Length; j++)
                        Assert.Greater(Vector3.Distance(positions[i], positions[j]), .85f);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void BusySpecialBusinessCannotBeChangedIntoRetailAndLoseOrdersOrUniqueGoods()
        {
            using(var s=Make("secondhand")){var item=Seller(s);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.False(s.SetShopMainCategory("computer"));Assert.AreEqual("secondhand",s.RetailKind);}
            using(var s=Make("restaurant")){Order(s);Assert.False(s.SetShopMainCategory("computer"));}
        }
        [Test] public void SavedMaximumPlanResolvesRemainingStockAtDispatchAndSharesItWithOtherBuyers()
        {
            using(var s=HomeTests.Make(500000,600))
            {
                s.EventsEnabled=false;
                var buyer=new Employee{Name="最大采购员",DailyWageYuan=150,CheckoutMinutes=3};s.MainShop.Buyer=buyer;CityTestPeople.AtAssignedWork(s,buyer);
                var offer=s.EmployeePurchaseCatalog().First(o=>o.Kind==PurchaseGoodsKind.Part&&o.Id=="cpu0");
                var item=offer.Line(40);item.BuyMaximum=true;
                Assert.True(s.ConfigureEmployeePurchase(buyer.Id,new EmployeePurchasePlan{Items=new[]{item},Transport=VehicleKind.Taxi}));
                int available=s.PurchaseRemaining(offer);
                Assert.Greater(available,0);Assert.LessOrEqual(available,40);
                Assert.True(s.DispatchEmployeePurchase(buyer.Id));
                Assert.Zero(s.PurchaseRemaining(offer));Assert.AreEqual(available,s.EmployeePurchaseTrips.Single().Items.Single().Count);
                Assert.True(buyer.PurchasePlan.Items.Single().BuyMaximum);
                using(var loaded=Reload(s)){Assert.True(loaded.MainShop.Buyer.PurchasePlan.Items.Single().BuyMaximum);Assert.Zero(loaded.PurchaseRemaining(loaded.EmployeePurchaseOffer(item)));}
            }
        }
        [Test] public void AppraiserHonorsBudgetAndOnlyWorksWhileActuallyOnDuty()
        {
            using(var s=Make("secondhand"))
            {
                var worker=new Employee{Name="收购员",Intelligence=90,DailyWageYuan=150,CheckoutMinutes=3};s.CurrentShop.Employee=worker;
                Assert.True(s.ArrangeEmployeeWork(worker.Id,"pc",EmployeeRole.Appraiser));CityTestPeople.AtAssignedWork(s,worker);
                var state=s.SpecialBusiness("pc");state.DailyBudget=40;state.MaxOffer=40;
                var item=Seller(s);s.AdvanceClock(1);Assert.True(item.Owned);Assert.AreEqual(40,state.SpentToday);Assert.True(item.Inspected);
                var second=Seller(s);s.AdvanceClock(1);Assert.False(second.Owned);Assert.AreEqual(40,state.SpentToday);
            }
        }
        [Test] public void ChefAndWaiterCompleteRealOrdersInBackgroundWithoutPlayerClicking()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);
                var chef=new Employee{Name="厨师",CheckoutMinutes=3,DailyWageYuan=150};s.CurrentShop.Employee=chef;
                Assert.True(s.ArrangeEmployeeWork(chef.Id,"pc",EmployeeRole.Chef));CityTestPeople.AtAssignedWork(s,chef);
                var waiter=new Employee{Name="服务员",CheckoutMinutes=3,DailyWageYuan=130};s.CurrentShop.Employee=waiter;
                Assert.True(s.ArrangeEmployeeWork(waiter.Id,"pc",EmployeeRole.Waiter));CityTestPeople.AtAssignedWork(s,waiter);
                GameSessionTests.Teleport(s,"flat");s.AdvanceClock(65);
                Assert.AreEqual(1,s.SpecialBusiness("pc").Paid);Assert.AreEqual(18,s.SpecialBusiness("pc").Revenue);Assert.Zero(s.KitchenIngredientCount(s.MainShop,"veg"));
            }
        }
        private static void Until(GameSession s,Func<bool> done,int minutes=80)
        {
            for(int i=0;i<minutes&&!done();i++)s.AdvanceClock(1);
            Assert.True(done(),"任务应沿真实路线完成，而不是永久等待");
        }
        [Test] public void PlayerRestaurantWorkWalksAndPreparesBeforeConsumingIngredients()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s,2);var order=Order(s);long start=s.Clock.Now.TotalMinutes;
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Cook,7,.9));
                var worker=s.RestaurantActor("pc",GameSession.RestaurantPlayer);double x=worker.X;
                Assert.AreEqual(DiningStage.Waiting,order.Stage);Assert.AreEqual(.5,s.KitchenRice(s.CurrentShop));
                Assert.False(s.RequestRestaurantWork(order.Id,RestaurantJob.Cook,7,.9));
                s.AdvanceClock(1);Assert.AreNotEqual(x,worker.X);Assert.AreEqual(DiningStage.Waiting,order.Stage);
                Until(s,()=>order.Stage==DiningStage.Cooking);
                Assert.Greater(order.Started,start+1);Assert.AreEqual(.25,s.KitchenRice(s.CurrentShop));
                Assert.AreEqual(1,s.KitchenIngredientCount(s.CurrentShop,"veg"));
                s.AdvanceClock(2);Assert.AreEqual(.25,s.KitchenRice(s.CurrentShop));
            }
        }
        [Test] public void ServingPicksUpFromStoveThenDeliversToTable()
        {
            using(var s=Make("restaurant"))
            {
                var order=Order(s);order.Stage=DiningStage.Cooking;order.Stove=0;
                order.ReadyAt=s.Clock.Now.TotalMinutes;order.BurnAt=order.ReadyAt+30;
                var balance=s.Ledger.Balance;
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Serve,7,.9));
                Assert.AreEqual(DiningStage.Cooking,order.Stage);
                Assert.AreEqual("restaurant_stove",s.RestaurantActor("pc",GameSession.RestaurantPlayer).Station);
                Until(s,()=>s.RestaurantActor("pc",GameSession.RestaurantPlayer).Carrying);
                Assert.AreEqual(DiningStage.Ready,order.Stage);Assert.AreEqual(balance,s.Ledger.Balance);
                Assert.AreEqual("restaurant_table",s.RestaurantActor("pc",GameSession.RestaurantPlayer).Station);
                Until(s,()=>order.Stage==DiningStage.Checkout);
                Assert.AreEqual(balance,s.Ledger.Balance);
                Assert.True(s.ClearRestaurantOrder(order.Id));
                Assert.AreEqual(DiningStage.Completed,order.Stage);Assert.AreEqual(balance+Money.FromYuan(order.Price),s.Ledger.Balance);
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Clear,7,.9));Assert.Contains(order,s.SpecialBusiness("pc").Orders);
                Until(s,()=>!s.SpecialBusiness("pc").Orders.Contains(order));
                Assert.AreEqual(balance+Money.FromYuan(order.Price),s.Ledger.Balance);
            }
        }
        [Test] public void RestaurantWorkCanCancelAndSurvivesLoadWithoutInstantActions()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Cook,7,.9));
                s.AdvanceClock(1);s.CancelPlayerRestaurantWork();s.AdvanceClock(5);
                Assert.AreEqual(DiningStage.Waiting,order.Stage);Assert.AreEqual(.25,s.KitchenRice(s.CurrentShop));
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Cook,7,.9));s.AdvanceClock(1);
                using(var loaded=Reload(s))
                {
                    var restored=loaded.SpecialBusiness("pc").Orders.Single();Assert.AreEqual(DiningStage.Waiting,restored.Stage);
                    Assert.True(loaded.RestaurantActor("pc",GameSession.RestaurantPlayer).Busy);
                    Until(loaded,()=>restored.Stage==DiningStage.Cooking);Assert.Zero(loaded.KitchenRice(loaded.CurrentShop));
                    Assert.AreEqual(DiningStage.Waiting,order.Stage,"存档必须深拷贝在途任务与订单");
                }
            }
        }
        [Test] public void StaffPhysicallyWalkAndOneOrderCannotBeClaimedTwice()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);
                var chef=new Employee{Name="厨师",DailyWageYuan=150};s.CurrentShop.Employee=chef;
                Assert.True(s.ArrangeEmployeeWork(chef.Id,"pc",EmployeeRole.Chef));CityTestPeople.AtAssignedWork(s,chef);
                s.AdvanceClock(1);var worker=s.RestaurantActor("pc",chef.Id);Assert.True(worker.Busy);
                Assert.AreEqual(DiningStage.Waiting,order.Stage);Assert.False(s.RequestRestaurantWork(order.Id,RestaurantJob.Cook,7,.9));
                double x=worker.X;s.AdvanceClock(1);Assert.AreNotEqual(x,worker.X);Assert.AreEqual(DiningStage.Waiting,order.Stage);
                Until(s,()=>order.Stage==DiningStage.Cooking);Assert.Zero(s.KitchenRice(s.CurrentShop));
                var copy=s.CreateSave();s.AdvanceClock(1);
                Assert.AreEqual(1,s.SpecialBusiness("pc").Workers.Count(w=>w.ActorId==chef.Id));
            }
        }
        [Test] public void RestaurantButtonQueuesActualOwnerMovementAndPausingFreezesIt()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);var host=new GameObject("RestaurantMovementUi");host.SetActive(false);
                var controller=host.AddComponent<PCShopSell.Game.GameController>();
                var view=new PCShopSell.Game.Runtime.ShopInteriorView(host.transform,s.PlayerShop.Size);
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                typeof(PCShopSell.Game.GameController).GetProperty("Interior").SetValue(controller,view);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(controller);
                typeof(PCShopSell.Game.GameController).GetProperty("Ui").SetValue(controller,ui);
                try
                {
                    view.SetActive(true);view.Update(s,0,0);ui.OpenDock();var at=view.OwnerWorld;
                    s.CityTravelRequested+=intent=>{};Assert.True(s.BeginCityTravel(s.Home.Id));Assert.NotNull(s.CityIntent);
                    ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b=>b.GetComponentInChildren<UnityEngine.UI.Text>()?.text=="备料投锅").onClick.Invoke();
                    Assert.IsNull(s.CityIntent,"店内任务应接管此前的出门导航");
                    Assert.AreEqual(DiningStage.Waiting,order.Stage);
                    s.Time.SetPaused(true);s.Tick(.2);view.Update(s,0,.2f);Assert.AreEqual(at,view.OwnerWorld);
                    s.Time.SetSpeed(1);s.Tick(.2);view.Update(s,0,.2f);Assert.AreNotEqual(at,view.OwnerWorld);
                    Until(s,()=>order.Stage==DiningStage.Cooking);view.Update(s,0,0);
                    Assert.Less(Vector3.Distance(view.OwnerWorld-PCShopSell.Game.Runtime.ShopInteriorView.Origin,new Vector3(2,0,2)),1.3f);
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(host);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }
        [Test] public void WaiterModelFollowsWorkRouteAndCarriesMealBeforeCustomerEats()
        {
            using(var s=Make("restaurant"))
            {
                var order=Order(s);order.Stage=DiningStage.Ready;
                var waiter=new Employee{Name="服务员",DailyWageYuan=130};s.CurrentShop.Employee=waiter;
                Assert.True(s.ArrangeEmployeeWork(waiter.Id,"pc",EmployeeRole.Waiter));CityTestPeople.AtAssignedWork(s,waiter);
                var root=new GameObject("RestaurantWorkerVisual");
                try
                {
                    var view=new PCShopSell.Game.Runtime.ShopInteriorView(root.transform,s.PlayerShop.Size);view.Update(s,0,0);
                    var figure=root.GetComponentsInChildren<PCShopSell.Game.Runtime.CityPersonMarker>().Single(m=>m.name=="ShopEmployee_"+waiter.Id).gameObject;
                    s.AdvanceClock(1);view.Update(s,0,0);var start=figure.transform.localPosition;
                    s.AdvanceClock(1);view.Update(s,0,0);Assert.AreNotEqual(start,figure.transform.localPosition);
                    Until(s,()=>s.RestaurantActor("pc",waiter.Id).Carrying);view.Update(s,0,0);
                    Assert.True(figure.transform.Find("RestaurantCarriedDish").gameObject.activeSelf);Assert.AreEqual(DiningStage.Ready,order.Stage);
                    Until(s,()=>order.Stage==DiningStage.Eating);view.Update(s,0,0);
                    Assert.False(figure.transform.Find("RestaurantCarriedDish").gameObject.activeSelf);
                    Assert.NotNull(root.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="SpecialGoods_meal:"+order.Id));
                    Until(s,()=>order.TableCleared);
                    Assert.AreEqual(DiningStage.Checkout,order.Stage);Assert.Zero(s.SpecialBusiness("pc").Paid);
                    Until(s,()=>order.Stage==DiningStage.Checkout);
                    Assert.True(s.ClearRestaurantOrder(order.Id));Assert.AreEqual(1,s.SpecialBusiness("pc").Paid);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test]
        public void TwoChefsUseSeparateStandsAndFaceTheirOwnStovesWhenCooking()
        {
            using var s = Make("restaurant");
            var shop = s.CurrentShop;
            var stoves = new[]
            {
                shop.Layout.Single(p => p.Id == "restaurant_stove"),
                new PlacedFurniture { Id = "restaurant_stove", X = 5.5, Z = 3.5 }
            };
            shop.Layout.Add(stoves[1]);
            shop.Layout.Add(new PlacedFurniture { Id = "restaurant_table", X = 6.5, Z = 5 });
            var chefs = new[] { new Employee { Name = "厨师甲", DailyWageYuan = 150 }, new Employee { Name = "厨师乙", DailyWageYuan = 150 } };
            foreach (var chef in chefs)
            {
                shop.AdditionalEmployees.Add(new AdditionalShopEmployee { Role = EmployeeRole.Chef, Person = chef });
                CityTestPeople.AtAssignedWork(s, chef);
            }
            var state = s.SpecialBusiness(shop.Place.Id);
            state.NextVisit = long.MaxValue;
            foreach (var chef in chefs)
                state.Workers.Add(new RestaurantWorker { ActorId = chef.Id, X = 5, Z = .9 });
            s.AdvanceClock(1);
            var workers = chefs.Select(chef => s.RestaurantActor(shop.Place.Id, chef.Id)).ToArray();
            Assert.That(Distance(workers[0].X, workers[0].Z, workers[1].X, workers[1].Z), Is.GreaterThanOrEqualTo(.85));

            IngredientsFor(s, 2);
            var orders = new[] { Order(s), Order(s, 2) };
            orders[1].Table = 1;
            s.AdvanceClock(1);
            Assert.That(workers.All(w => w.Job == RestaurantJob.Cook), Is.True);
            Assert.That(workers.Select(w => w.Stove), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(Distance(workers[0].PathX.Last(), workers[0].PathZ.Last(), workers[1].PathX.Last(), workers[1].PathZ.Last()),
                Is.GreaterThanOrEqualTo(.85));
            var root = new GameObject("TwoRestaurantWorkers");
            try
            {
                var view = new ShopInteriorView(root.transform, s.PlayerShop.Size);
                var sawCookPose = new bool[2];
                for (int minute = 0; minute < 20 && !sawCookPose.All(seen => seen); minute++)
                {
                    view.Update(s, 0, 0);
                    var figures = chefs.Select(chef => root.GetComponentsInChildren<FigureMotion>()
                        .Single(f => f.name == "ShopEmployee_" + chef.Id)).ToArray();
                    Assert.That(Vector3.Distance(figures[0].transform.position, figures[1].transform.position),
                        Is.GreaterThanOrEqualTo(.84f));
                    for (int i = 0; i < chefs.Length; i++)
                    {
                        if (workers[i].Job != RestaurantJob.Cook || workers[i].OperateUntil < 0) continue;
                        var figure = figures[i];
                        var stove = stoves[workers[i].Stove];
                        var toStove = new Vector3((float)stove.X, 0, (float)stove.Z) - figure.transform.localPosition;
                        Assert.That(Vector3.Dot(figure.transform.forward, toStove.normalized), Is.GreaterThan(.99));
                        Assert.That(figure.WorkAction, Is.EqualTo(FigureWorkAction.Cook));
                        sawCookPose[i] = true;
                    }
                    s.AdvanceClock(1);
                }
                Assert.That(sawCookPose, Is.All.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            static double Distance(double x, double z, double otherX, double otherZ) =>
                Math.Sqrt(Math.Pow(x - otherX, 2) + Math.Pow(z - otherZ, 2));
        }
        [TestCase(true)]
        [TestCase(false)]
        public void RestaurantAtClosingFinishesSeatedCustomersOnlyWhileAttended(bool ownerStays)
        {
            using var s = Make("restaurant");
            s.Clock.Restore(s.Clock.Today, 21 * 60 + 59);
            IngredientsFor(s);
            var order = Order(s);
            var state = s.SpecialBusiness("pc");
            state.NextVisit = s.Clock.Now.TotalMinutes + 1;
            var cash = s.Ledger.Balance;
            if (!ownerStays) s.Leave();

            s.AdvanceClock(2);
            Assert.False(s.SpecialOpen(s.MainShop, s.Clock.Now));
            Assert.AreEqual(ownerStays, s.ShopFinishingService(s.MainShop));
            Assert.AreEqual(1, state.Orders.Count, "打烊后不能接新客");
            if (!ownerStays)
            {
                Assert.AreEqual(DiningStage.Left, order.Stage);
                Assert.AreEqual(cash, s.Ledger.Balance);
                return;
            }

            Assert.True(s.StartRestaurantCooking(order.Id));
            Until(s, () => s.Clock.Now.TotalMinutes >= order.ReadyAt);
            Assert.True(s.PlateRestaurantOrder(order.Id));
            Assert.True(s.ServeRestaurantOrder(order.Id));
            Until(s, () => order.Stage == DiningStage.Checkout);
            Assert.True(s.ClearRestaurantOrder(order.Id));
            Assert.AreEqual(cash + Money.FromYuan(18), s.Ledger.Balance);
            Assert.IsEmpty(state.Orders);
            Assert.False(s.ShopFinishingService(s.MainShop));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void QueuedRetailCustomerCanPayAfterClosingOnlyWhileAttended(bool ownerStays)
        {
            using var s = Make("secondhand");
            ListedUsed(s);
            WaitForUsedBuyer(s);
            var customer = s.PlayerShop.Queue.Single();
            var cash = s.Ledger.Balance;
            int arrivals = s.PlayerShop.TodayCustomers;
            s.PlayerShop.SetHours(8 * 60, s.Clock.MinuteOfDay);
            if (!ownerStays) s.Leave();

            s.AdvanceClock(2);
            Assert.AreEqual(arrivals, s.PlayerShop.TodayCustomers);
            Assert.AreEqual(ownerStays, s.ShopFinishingService(s.MainShop));
            if (ownerStays)
            {
                Assert.True(s.Checkout(customer.Id));
                Assert.AreEqual(cash + Money.FromYuan(100), s.Ledger.Balance);
            }
            else
            {
                Assert.IsEmpty(s.PlayerShop.Queue);
                Assert.AreEqual(PCShopSell.Core.Shop.LeaveReason.ShopClosed, customer.Reason);
                Assert.AreEqual(cash, s.Ledger.Balance);
            }
        }
        [Test] public void PlayerConfirmedPricesDoNotChangeWhenSeriesHeatOrTechnologyChanges()
        {
            using(var s=Make("models"))
            {
                var product=ModelProducts.Regular.First();long chosen=s.PlayerShop.GoodsPriceOf(product.Id).Yuan;
                Assert.True(s.PlayerShop.SetGoodsSalePrice(product.Id,chosen));
                var part=PartCatalog.Get("cpu0");long partPrice=s.PlayerShop.PriceOf(part,true).Yuan;Assert.True(s.PlayerShop.SetPartSalePrice(part,true,partPrice));
                s.Clock.Restore(new GameDate(90),600);Assert.AreEqual(chosen,s.PlayerShop.GoodsPriceOf(product.Id).Yuan);Assert.AreEqual(partPrice,s.PlayerShop.PriceOf(part,true).Yuan);
                using(var loaded=Reload(s)){Assert.AreEqual(chosen,loaded.PlayerShop.GoodsPriceOf(product.Id).Yuan);Assert.AreEqual(partPrice,loaded.PlayerShop.PriceOf(part,true).Yuan);}
            }
        }
        [TestCase("restaurant",false)][TestCase("restaurant",true)][TestCase("secondhand",false)]
        public void SpecialCounterBuildsActualControlsAndSpecialFurnitureHasVisibleModels(string kind,bool renovating)
        {
            using(var s=Make(kind))
            {
                if(renovating)Assert.True(s.BeginRenovation());
                var root=new GameObject("SpecialCounterRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(controller);
                try
                {
                    ui.OpenDock();
                    var texts=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Text>()).Select(text=>text.text).ToArray();
                    Assert.True(texts.Any(text=>text.Contains(kind=="restaurant"?"打开冰箱":"每日收购预算")));
                    foreach(var furniture in s.CurrentShop.Layout)
                    {
                        var model=PCShopSell.Game.Runtime.FurnitureVisuals.Build(root.transform,FurnitureCatalog.Get(furniture.Id),false);
                        Assert.Greater(model.GetComponentsInChildren<MeshRenderer>().Length,1);
                    }
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }

        [Test] public void KitchenColdStorageMovesActualPurchasedBatchesWithoutDuplication()
        {
            using(var s=Make("restaurant"))
            {
                s.CurrentShop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopBox, X = 1, Z = 3 });
                s.Backpack.Food.RiceKg=1;
                s.Backpack.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=1,ExpireDay=16});
                s.Backpack.AddGift("produce:veg",2);
                Assert.True(s.StoreBackpackInWarehouse());
                Assert.AreEqual(1,s.KitchenRice(s.CurrentShop));Assert.AreEqual(0,s.CurrentShop.Fridge.Food.RiceKg);
                Assert.AreEqual(3,s.CurrentShop.Fridge.Food.Ingredient.Sum(b=>b.Count));
                Assert.IsEmpty(s.Backpack.Food.Ingredient);Assert.Zero(s.Backpack.GiftCount("produce:veg"));
                Assert.True(s.CurrentShop.Fridge.Food.Ingredient.All(b=>b.Chilled));Assert.False(s.StoreBackpackInWarehouse());
            }
        }
        [Test] public void KitchenColdStorageWorksWhileClosedForRenovation()
        {
            using(var s=Make("restaurant"))
            {
                Assert.True(s.BeginRenovation());Assert.True(s.CanOpenFridge);Assert.False(s.SpecialOpen(s.CurrentShop,s.Clock.Now));
                s.CurrentShop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopBox, X = 1, Z = 3 });
                s.Backpack.Food.RiceKg=1;
                s.Backpack.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=2,ExpireDay=16});
                Assert.True(s.StoreBackpackInWarehouse(),"装修歇业不应该阻止使用已摆放的冰箱整理食材");
                Assert.AreEqual(1,s.KitchenRice(s.CurrentShop));Assert.AreEqual(0,s.CurrentShop.Fridge.Food.RiceKg);
                Assert.AreEqual(2,s.CurrentShop.Fridge.Food.Ingredient.Sum(b=>b.Count));
                Assert.True(s.CurrentShop.Fridge.Food.Ingredient.All(b=>b.Chilled));
                Assert.True(s.IsRenovating);Assert.True(s.PlayerShop.ForcedClosed);Assert.False(s.SpecialOpen(s.CurrentShop,s.Clock.Now));
            }
        }
        [Test] public void KitchenCanStoreRiceWhileOtherIngredientsWaitForAFridge()
        {
            using(var s=Make("restaurant"))
            {
                s.CurrentShop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopBox, X = 1, Z = 3 });
                s.Backpack.Food.RiceKg=1;
                s.Backpack.Food.Ingredient.Add(new FoodBatch { Id = "veg", Count = 2 });
                s.CurrentShop.Layout.RemoveAll(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Fridge);
                Assert.True(s.StoreBackpackInWarehouse());
                Assert.AreEqual(1,s.KitchenRice(s.CurrentShop));
                Assert.Zero(s.Backpack.Food.RiceKg);
                Assert.AreEqual(2,s.Backpack.Food.IngredientCount("veg"));
                Assert.False(s.StoreBackpackInWarehouse());
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Fridge,X=1,Z=2});
                Assert.True(s.StoreBackpackInWarehouse());
                Assert.Zero(s.CurrentShop.Fridge.Food.RiceKg);
                Assert.AreEqual(2,s.KitchenIngredientCount(s.CurrentShop,"veg"));
                Assert.Zero(s.Backpack.Food.IngredientCount("veg"));
            }
        }

        [Test]
        public void RestaurantRiceMovesByHandWithoutUsingFridgeSpace()
        {
            using var s = Make("restaurant");
            var shop = s.CurrentShop;
            shop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopBox, X = 1, Z = 3 });
            s.Backpack.Food.RiceKg = 2.5;
            Assert.False(s.MoveFridgeFood("rice", 1, true));
            Assert.True(s.StoreInBox("rice"));
            Assert.AreEqual(1, s.KitchenRice(shop));
            Assert.True(s.UnloadBackpack());
            Assert.AreEqual(2.5, s.KitchenRice(shop));
            shop.Temp.Food.RiceKg = .5;
            Assert.True(s.MoveTemp("rice", 1, "box"));
            Assert.AreEqual(3, s.KitchenRice(shop));
            Assert.True(s.TakeFromBox("rice"));
            Assert.AreEqual(2, s.KitchenRice(shop));
            Assert.AreEqual(1, s.Backpack.Food.RiceKg);
            Assert.AreEqual(2, s.GoodsAvailableToList("produce:rice"));
            Assert.True(s.ListGoods("produce:rice", 2));
            Assert.AreEqual(3, s.KitchenRice(shop));
            Assert.Zero(s.GoodsAvailableToList("produce:rice"));
            Assert.Zero(shop.Sim.Stock.GoodsCount("produce:rice"));
            Assert.Zero(shop.Fridge.WeightKg);
        }

        [Test]
        public void LegacyRestaurantRiceMovesIntoItsOwnWarehouseOnlyOnce()
        {
            using var s = Make("restaurant");
            var shop = s.CurrentShop;
            shop.Locker.Food.RiceKg = 9;
            shop.Fridge.Food.RiceKg = 121.5;
            shop.Temp.Food.RiceKg = .25;
            shop.Sim.Stock.ForceAddGoods("produce:rice", 5);
            shop.Fridge.Food.Ingredient.Add(new FoodBatch { Id = "veg", Count = 2, ExpireDay = FoodBatch.NoExpiry });
            s.HomeFridge.Food.RiceKg = 2;
            using var loaded = Reload(s);
            using var reloaded = Reload(loaded);
            foreach (var session in new[] { loaded, reloaded })
            {
                Assert.AreEqual(133.25, session.KitchenRice(session.MainShop));
                Assert.Zero(session.MainShop.Fridge.Food.RiceKg);
                Assert.Zero(session.MainShop.Temp.Food.RiceKg);
                Assert.Zero(session.MainShop.Sim.Stock.GoodsCount("produce:rice"));
                Assert.AreEqual(2, session.KitchenIngredientCount(session.MainShop, "veg"));
                Assert.AreEqual(2, session.HomeFridge.Food.RiceKg);
            }
        }

        [Test]
        public void OnlineRiceReachesRestaurantWithoutAFridge()
        {
            using var s = Make("restaurant");
            var shop = s.CurrentShop;
            shop.Layout.RemoveAll(p => FurnitureCatalog.Get(p.Id)?.Role == FurnitureRole.Fridge);
            Assert.That(s.CanOpenFridge, Is.False);
            Assert.True(s.OrderOnline(ParcelKind.Rice, "rice", 2, shop));
            Assert.True(s.OrderOnline(ParcelKind.Gift, "produce:rice", 3, shop));
            Assert.False(s.OrderOnline(ParcelKind.Gift, "produce:veg", 1, shop));
            s.Clock.Advance(GameSession.DeliveryDays * 1440);
            Assert.AreEqual(11.5, s.KitchenRice(shop));
            Assert.Zero(shop.Fridge.Food.RiceKg);
        }
        [Test] public void PausingAllRecipesStopsNewOrdersAndOpenStatusDoesNotEnableRetailSimulation()
        {
            using(var s=Make("restaurant"))
            {
                Assert.True(s.SpecialOpen(s.CurrentShop,s.Clock.Now));Assert.True(s.PlayerShop.IsOpen);Assert.False(s.PlayerShop.IsOpenAt(s.Clock.Now));
                var state=s.SpecialBusiness("pc");state.RecipeMask=0;state.NextVisit=s.Clock.Now.TotalMinutes;
                s.AdvanceClock(30);Assert.IsEmpty(state.Orders);
            }
        }

        [Test] public void FormerFarmOrderMergesWithWholesaleEntryWhenLoading()
        {
            using var s = HomeTests.Make(500000, 600);
            s.EventsEnabled = false;
            var buyer = new Employee { Name = "采购员", DailyWageYuan = 150, CheckoutMinutes = 3 };
            s.MainShop.Buyer = buyer;
            buyer.PurchasePlan = new EmployeePurchasePlan
            {
                Items = new[] {
                    new PurchaseLine { SourceId = "farm", Kind = PurchaseGoodsKind.Ingredient, PartId = "veg", Count = 7 },
                    new PurchaseLine { SourceId = "farm", Kind = PurchaseGoodsKind.Gift, PartId = "produce:veg", Count = 10 },
                }
            };
            using var loaded = Reload(s);
            var item = loaded.MainShop.Buyer.PurchasePlan.Items.Single();
            Assert.AreEqual(PurchaseGoodsKind.Gift, item.Kind);
            Assert.AreEqual("farm", item.SourceId);
            Assert.AreEqual("produce:veg", item.PartId);
            Assert.AreEqual(17, item.Count);
        }

        [Test] public void MaximumSelectionIsStoredAsIntentRatherThanTodaysSnapshot()
        {
            var line=new PurchaseLine{Kind=PurchaseGoodsKind.Part,SourceId="stall",PartId="cpu0",Count=40,BuyMaximum=true};
            var copy=line.Copy();Assert.True(copy.BuyMaximum);copy.Count=12;Assert.AreEqual(40,line.Count);
            var json=JsonUtility.ToJson(new EmployeePurchasePlan{Items=new[]{line}});Assert.True(JsonUtility.FromJson<EmployeePurchasePlan>(json).Items.Single().BuyMaximum);
        }
    }
}
