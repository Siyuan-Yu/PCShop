using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
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
            var ids=kind=="restaurant"?new[]{"restaurant_prep","restaurant_stove","restaurant_pass","restaurant_table",FurnitureCatalog.Fridge}:
                kind=="secondhand"?new[]{"used_buydesk","used_display"}:new string[0];
            for(int i=0;i<ids.Length;i++)s.CurrentShop.Layout.Add(new PlacedFurniture{Id=ids[i],X=1+i,Z=2});
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
            s.CurrentShop.Fridge.Food.RiceKg=portions*.25;
            s.CurrentShop.Fridge.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=portions,ExpireDay=s.Clock.Today.TotalDays+2});
        }
        private static UsedItem Seller(GameSession s,UsedCondition condition=UsedCondition.Good)
        {
            var state=s.SpecialBusiness(s.CurrentShop.Place.Id);state.NextVisit=long.MaxValue;
            var item=new UsedItem{Id=state.NextId++,Product=0,Condition=condition,Claim=UsedCondition.Good,Asking=80,Minimum=40,ExpiresAt=s.Clock.Now.TotalMinutes+90};state.Used.Add(item);return item;
        }
        [Test] public void OnlyPlannedNewTypesAreOffered_AndLegacyTypesStillLoad()
        {
            CollectionAssert.AreEquivalent(new[]{"computer","models","clothing","restaurant","secondhand"},RetailShopTypes.All);
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
        [Test] public void HardwareReleaseAndDiscountAreCalendarBasedAndSurviveLoad()
        {
            using(var s=GameSessionTests.Make())
            {
                var old=PartCatalog.Get("cpu0");var newer=PartCatalog.Get("g1:cpu0");
                Assert.False(IndustryCycles.Available(newer,29));Assert.True(IndustryCycles.Available(newer,30));
                var before=s.Market.PriceOf(old);s.Clock.Restore(new GameDate(30),600);
                Assert.Less(s.Market.PriceOf(old).Cents,before.Cents);
                using(var loaded=Reload(s))Assert.AreEqual(s.Market.PriceOf(old),loaded.Market.PriceOf(old));
                Assert.AreEqual(.55,IndustryCycles.HardwareFactor(old,90));
                Assert.IsNull(PartCatalog.CheckCompatibility(PartCatalog.BuildList(0).Select(p=>PartCatalog.Get("g1:"+p.Id)).ToArray()));
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
        [Test] public void RestaurantRequiresKitchenNotRetailShelfAndCashCounter()
        {
            using(var s=Make("restaurant"))
            {
                Assert.IsNull(s.SpecialFitGap(s.CurrentShop));Assert.True(s.ShopFitted);
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
        [Test] public void CompleteRestaurantFlowChargesCustomerOnceAfterEatingAndRequiresClearingTable()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);var balance=s.Ledger.Balance;
                Assert.True(s.StartRestaurantCooking(order.Id));s.Clock.Advance(8);
                Assert.True(s.PlateRestaurantOrder(order.Id));Assert.True(s.ServeRestaurantOrder(order.Id));Assert.False(s.ServeRestaurantOrder(order.Id));
                Assert.AreEqual(balance,s.Ledger.Balance);s.Clock.Advance(15);
                Assert.AreEqual(DiningStage.Completed,order.Stage);Assert.AreEqual(balance+Money.FromYuan(18),s.Ledger.Balance);
                s.Clock.Advance(2);Assert.AreEqual(balance+Money.FromYuan(18),s.Ledger.Balance);
                Assert.True(s.ClearRestaurantOrder(order.Id));Assert.IsEmpty(s.SpecialBusiness("pc").Orders);
            }
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
                    using(var again=Reload(loaded)){var balance=again.Ledger.Balance;again.Clock.Advance(2);Assert.AreEqual(balance,again.Ledger.Balance);}
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
        [Test] public void UsedSellingRequiresDisplayCapacityAndRetainsLossRisk()
        {
            using(var s=Make("secondhand"))
            {
                for(int n=0;n<5;n++)
                {
                    var item=Seller(s,UsedCondition.Broken);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.True(s.InspectUsed(item.Id));
                    Assert.Less(GameSession.UsedValue(item),item.Cost);
                    Assert.AreEqual(n<4,s.ListUsedItem(item.Id,GameSession.UsedValue(item)));
                }
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
                var state=s.SpecialBusiness("pc");state.AutoPurchase=true;state.DailyBudget=40;state.MaxOffer=40;
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
                GameSessionTests.Teleport(s,"flat");s.AdvanceClock(30);
                Assert.AreEqual(1,s.SpecialBusiness("pc").Paid);Assert.AreEqual(18,s.SpecialBusiness("pc").Revenue);Assert.Zero(s.KitchenIngredientCount(s.MainShop,"veg"));
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
        [TestCase("restaurant")][TestCase("secondhand")]
        public void SpecialCounterBuildsActualControlsAndSpecialFurnitureHasVisibleModels(string kind)
        {
            using(var s=Make(kind))
            {
                var root=new GameObject("SpecialCounterRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(controller);
                try
                {
                    ui.OpenDock();
                    var texts=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Text>()).Select(text=>text.text).ToArray();
                    Assert.True(texts.Any(text=>text.Contains(kind=="restaurant"?"厨房大米":"每日收购预算")));
                    foreach(var furniture in s.CurrentShop.Layout)
                    {
                        var model=PCShopSell.Game.Runtime.FurnitureVisuals.Build(root.transform,FurnitureCatalog.Get(furniture.Id),false);
                        Assert.Greater(model.transform.childCount,1);
                    }
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }

        [Test] public void KitchenColdStorageMovesActualPurchasedBatchesWithoutDuplication()
        {
            using(var s=Make("restaurant"))
            {
                s.CurrentShop.Locker.Food.RiceKg=1;
                s.CurrentShop.Locker.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=1,ExpireDay=16});
                s.PlayerShop.Stock.ForceAddGoods("produce:veg",2,17);
                Assert.True(s.ChillKitchenIngredients());
                Assert.AreEqual(1,s.CurrentShop.Fridge.Food.RiceKg);Assert.AreEqual(3,s.CurrentShop.Fridge.Food.Ingredient.Sum(b=>b.Count));
                Assert.IsEmpty(s.CurrentShop.Locker.Food.Ingredient);Assert.Zero(s.PlayerShop.Stock.GoodsCount("produce:veg"));
                Assert.True(s.CurrentShop.Fridge.Food.Ingredient.All(b=>b.Chilled));Assert.False(s.ChillKitchenIngredients());
            }
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

        [Test] public void IngredientAndWholesalePackagingCannotDoubleSpendTheSameSupplyInOneEmployeeTrip()
        {
            using(var s=HomeTests.Make(500000,600))
            {
                s.EventsEnabled=false;var buyer=new Employee{Name="采购员",DailyWageYuan=150,CheckoutMinutes=3};s.MainShop.Buyer=buyer;CityTestPeople.AtAssignedWork(s,buyer);
                var offers=s.EmployeePurchaseCatalog();var retail=offers.First(o=>o.Source.Id=="farm"&&o.Kind==PurchaseGoodsKind.Ingredient&&o.Id=="veg");
                var wholesale=offers.First(o=>o.Source.Id=="farm"&&o.Kind==PurchaseGoodsKind.Gift&&o.Id=="produce:veg");
                int limit=s.PurchaseLimit(retail);var plan=new EmployeePurchasePlan{Transport=VehicleKind.Taxi,Items=new[]{retail.Line(limit),wholesale.Line(limit)}};
                Assert.True(s.ConfigureEmployeePurchase(buyer.Id,plan));Assert.True(s.DispatchEmployeePurchase(buyer.Id));
                Assert.AreEqual(limit,s.EmployeePurchaseTrips.Single().Items.Sum(i=>i.Count));Assert.Zero(s.CitySupplierRemaining("farm","produce:veg"));
            }
        }

        [Test] public void MaximumSelectionIsStoredAsIntentRatherThanTodaysSnapshot()
        {
            var line=new PurchaseLine{Kind=PurchaseGoodsKind.Part,SourceId="stall",PartId="cpu0",Count=40,BuyMaximum=true};
            var copy=line.Copy();Assert.True(copy.BuyMaximum);copy.Count=12;Assert.AreEqual(40,line.Count);
            var json=JsonUtility.ToJson(new EmployeePurchasePlan{Items=new[]{line}});Assert.True(JsonUtility.FromJson<EmployeePurchasePlan>(json).Items.Single().BuyMaximum);
        }
    }
}
