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
            s.CurrentShop.Fridge.Food.RiceKg=portions*.25;
            s.CurrentShop.Fridge.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=portions,ExpireDay=s.Clock.Today.TotalDays+2});
        }
        [TestCase("fridge")]
        [TestCase("warehouse")]
        [TestCase("stock")]
        public void HungryRestaurantOwnerEatsLocalIngredientsWithoutSaleOrCashCharge(string source)
        {
            using(var s=Make("restaurant"))
            {
                var shop=s.CurrentShop;var state=s.SpecialBusiness(shop.Place.Id);state.NextVisit=long.MaxValue;
                if(source=="stock")
                {shop.Sim.Stock.ForceAddGoods("produce:rice",1);shop.Sim.Stock.ForceAddGoods("produce:egg",2);}
                else
                {
                    var bag=source=="fridge"?shop.Fridge:shop.Locker;
                    bag.Food.RiceKg=.5;bag.Food.Ingredient.Add(new FoodBatch{Id="egg",Name="鸡蛋",Count=2,ExpireDay=30});
                }
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
                shop.Fridge.Food.RiceKg=.1;s.Backpack.Food.Packed.Clear();s.Player.Hunger=20;s.AdvanceClock(31);
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
        [Test] public void ClearingTableDoesNotSettleTheMealAndCounterChargesOnlyOnce()
        {
            using(var s=Make("restaurant"))
            {
                IngredientsFor(s);var order=Order(s);var balance=s.Ledger.Balance;
                Assert.True(s.StartRestaurantCooking(order.Id));s.Clock.Advance(8);
                Assert.True(s.PlateRestaurantOrder(order.Id));Assert.True(s.ServeRestaurantOrder(order.Id));Assert.False(s.ServeRestaurantOrder(order.Id));
                Assert.AreEqual(balance,s.Ledger.Balance);s.Clock.Advance(15);
                Assert.AreEqual(DiningStage.Checkout,order.Stage);Assert.AreEqual(balance,s.Ledger.Balance);
                Assert.False(s.CheckoutRestaurantCustomer(order.Id));
                Assert.True(s.ClearRestaurantOrder(order.Id));Assert.True(order.TableCleared);
                Assert.AreEqual(balance,s.Ledger.Balance);
                Until(s,()=>s.RestaurantCheckoutCustomer==order);
                Assert.True(s.CheckoutRestaurantCustomer(order.Id));Assert.False(s.CheckoutRestaurantCustomer(order.Id));
                Assert.AreEqual(balance+Money.FromYuan(18),s.Ledger.Balance);Assert.IsEmpty(s.SpecialBusiness("pc").Orders);
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
                    using(var again=Reload(loaded))
                    {
                        var balance=again.Ledger.Balance;
                        Until(again,()=>again.RestaurantCheckoutCustomer?.Id==order.Id);
                        Assert.AreEqual(balance,again.Ledger.Balance);
                        Assert.True(again.CheckoutRestaurantCustomer(order.Id));
                        using(var paid=Reload(again))
                        {
                            Assert.False(paid.CheckoutRestaurantCustomer(order.Id));
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
                    var refused=Seller(s);ui.OpenDock();Button("DeclineUsed_"+refused.Id).onClick.Invoke();
                    Assert.IsEmpty(s.SpecialBusiness("pc").Used);
                    var item=Seller(s);Assert.True(s.OfferUsedPrice(item.Id,40));Assert.True(s.InspectUsed(item.Id));ui.RebuildDock();
                    Button("ListUsed_"+item.Id).onClick.Invoke();Assert.True(item.Listed);Assert.IsNull(Button("ListUsed_"+item.Id));
                    ui.DebugSelectTab(1);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedStock_"+item.Id));
                    ui.OpenShelfWindow(2);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedIllustration_"+item.Id));
                    ui.CloseAllWindows();WaitForUsedBuyer(s);s.PlayerShop.Checkout(s.PlayerShop.Queue.Single().Id);ui.OpenDock();ui.DebugSelectTab(0);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="UsedSold_"+item.Id));
                    Assert.IsNull(Button("ListUsed_"+item.Id));
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
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
                Until(s,()=>s.RestaurantCheckoutCustomer==order);
                Assert.AreEqual(balance,s.Ledger.Balance);
                Assert.True(s.CheckoutRestaurantCustomer(order.Id));
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
                    Until(s,()=>s.RestaurantCheckoutCustomer==order);
                    Assert.True(s.CheckoutRestaurantCustomer(order.Id));Assert.AreEqual(1,s.SpecialBusiness("pc").Paid);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test] public void PlayerCanFinishServingAndClearingAfterRestaurantCloses()
        {
            using(var s=Make("restaurant"))
            {
                var order=Order(s);order.Stage=DiningStage.Ready;s.PlayerShop.ForcedClosed=true;
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Serve,7,.9));Until(s,()=>order.Stage==DiningStage.Eating);
                Until(s,()=>s.RestaurantCheckoutCustomer==order);Assert.True(s.CheckoutRestaurantCustomer(order.Id));
                Assert.True(s.RequestRestaurantWork(order.Id,RestaurantJob.Clear,7,.9));Until(s,()=>!s.SpecialBusiness("pc").Orders.Contains(order));
                Assert.True(s.PlayerShop.ForcedClosed);
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
                    Assert.True(texts.Any(text=>text.Contains(kind=="restaurant"?"厨房大米":"每日收购预算")));
                    if(kind=="restaurant")
                    {
                        s.CurrentShop.Locker.Food.RiceKg=1;
                        var chill=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b=>b.name=="ChillKitchenIngredients");
                        Assert.True(chill.interactable);chill.onClick.Invoke();
                        Assert.AreEqual(1,s.CurrentShop.Fridge.Food.RiceKg);Assert.Zero(s.CurrentShop.Locker.Food.RiceKg);
                    }
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
                s.CurrentShop.Locker.Food.RiceKg=1;
                s.CurrentShop.Locker.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=1,ExpireDay=16});
                s.PlayerShop.Stock.ForceAddGoods("produce:veg",2,17);
                Assert.True(s.ChillKitchenIngredients());
                Assert.AreEqual(1,s.CurrentShop.Fridge.Food.RiceKg);Assert.AreEqual(3,s.CurrentShop.Fridge.Food.Ingredient.Sum(b=>b.Count));
                Assert.IsEmpty(s.CurrentShop.Locker.Food.Ingredient);Assert.Zero(s.PlayerShop.Stock.GoodsCount("produce:veg"));
                Assert.True(s.CurrentShop.Fridge.Food.Ingredient.All(b=>b.Chilled));Assert.False(s.ChillKitchenIngredients());
            }
        }
        [Test] public void KitchenColdStorageWorksWhileClosedForRenovation()
        {
            using(var s=Make("restaurant"))
            {
                Assert.True(s.BeginRenovation());Assert.True(s.CanOpenFridge);Assert.False(s.SpecialOpen(s.CurrentShop,s.Clock.Now));
                s.CurrentShop.Locker.Food.RiceKg=1;
                s.CurrentShop.Locker.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=2,ExpireDay=16});
                Assert.True(s.ChillKitchenIngredients(),"装修歇业不应该阻止使用已摆放的冰箱整理食材");
                Assert.AreEqual(1,s.CurrentShop.Fridge.Food.RiceKg);Assert.AreEqual(2,s.CurrentShop.Fridge.Food.Ingredient.Sum(b=>b.Count));
                Assert.True(s.CurrentShop.Fridge.Food.Ingredient.All(b=>b.Chilled));
                Assert.True(s.IsRenovating);Assert.True(s.PlayerShop.ForcedClosed);Assert.False(s.SpecialOpen(s.CurrentShop,s.Clock.Now));
            }
        }
        [Test] public void KitchenColdStorageExplainsMissingFridgeAndRejectsRemoteUse()
        {
            using(var s=Make("restaurant"))
            {
                s.CurrentShop.Locker.Food.RiceKg=1;
                s.CurrentShop.Layout.RemoveAll(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Fridge);
                StringAssert.Contains("本店还没有摆放冰箱",s.KitchenColdStorageBlocker);
                Assert.False(s.ChillKitchenIngredients());Assert.AreEqual(1,s.CurrentShop.Locker.Food.RiceKg);
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Fridge,X=1,Z=2});
                Assert.IsNull(s.KitchenColdStorageBlocker);
                GameSessionTests.Teleport(s,s.Home.Id);
                StringAssert.Contains("请到自己的餐馆",s.KitchenColdStorageBlocker);
                Assert.False(s.ChillKitchenIngredients());Assert.AreEqual(1,s.CurrentShop.Locker.Food.RiceKg);
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
