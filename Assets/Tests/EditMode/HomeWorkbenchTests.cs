using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class HomeWorkbenchTests
    {
        private static GameSession Ready(int minute=10*60)
        {
            var s=HomeTests.Make(200000,minute);s.EventsEnabled=false;GameSessionTests.Teleport(s,s.Home.Id);
            s.HomeBox.AddFurniture(GameSession.HomeWorkbenchId,1);return s;
        }
        [Test] public void LargeScreenCanBePurchasedDeliveredAndUnlocksOnlyAtHomeWithoutInstallation()
        {
            using(var s=HomeTests.Make(200000,10*60))
            {
                s.EventsEnabled=false;Assert.False(s.OwnsHomeWorkbench);
                var def=FurnitureCatalog.Get(GameSession.HomeWorkbenchId);Assert.AreEqual(28000,def.Price.Yuan);Assert.AreEqual(160,def.WeightKg);
                GameSessionTests.Teleport(s,"mall");var before=s.Ledger.Balance;
                Assert.True(s.CartAddFurniture(def.Id));Assert.AreEqual(before,s.Ledger.Balance);Assert.AreEqual(160,s.CartTruckKg);
                Assert.True(s.Checkout(false));Assert.Less(s.Ledger.Balance.Cents,before.Cents);Assert.False(s.OwnsHomeWorkbench);
                s.AdvanceClock(180);GameSessionTests.Teleport(s,s.Home.Id);
                Assert.True(s.OwnsHomeWorkbench);Assert.True(s.CanUseHomeWorkbench);Assert.AreEqual(0,s.HomeTier);
                Assert.False(s.HomeLayout.Any(p=>p.Id==def.Id));
                GameSessionTests.Teleport(s,"pc");Assert.False(s.CanUseHomeWorkbench);Assert.False(s.SetWorkbenchMarkup("pc",1.3));
            }
        }
        [Test] public void RemotePricingAndHoursUseTheSelectedShopAndOnlyDisplayedGoods()
        {
            using(var s=Ready())
            {
                var shop=s.MainShop;shop.Sim.Stock.Clear();var product=Gifts.All.First(g=>ClothingProducts.IsClothing(g.Id));
                shop.Sim.Stock.ForceAddGoods(product.Id,4);shop.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>());
                Assert.False(s.SetWorkbenchGoodsPrice(shop.Place.Id,product.Id,product.Price.Yuan));
                shop.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{ShelfKinds.ForGift(product.Id).Id,60}});
                var place=s.CurrentPlace;var focus=s.CurrentShop;long time=s.Clock.Now.TotalMinutes;
                Assert.True(s.SetWorkbenchGoodsPrice(shop.Place.Id,product.Id,product.Price.Yuan+1));
                Assert.AreEqual(product.Price.Yuan+1,shop.Sim.GoodsPriceOf(product.Id).Yuan);
                Assert.True(s.SetWorkbenchHours(shop.Place.Id,21,7,false));Assert.AreEqual(21*60,shop.Sim.OpenMinute);
                Assert.False(s.SetWorkbenchHours(shop.Place.Id,9,9,false));Assert.False(s.SetWorkbenchPartPrice(shop.Place.Id,"invalid",true,100));
                Assert.True(s.SetWorkbenchMarkup(shop.Place.Id,1.3));Assert.False(s.SetWorkbenchMarkup(shop.Place.Id,double.NaN));
                Assert.AreEqual(place,s.CurrentPlace);Assert.AreSame(focus,s.CurrentShop);Assert.AreEqual(time,s.Clock.Now.TotalMinutes);
                Assert.AreEqual(4,shop.Sim.Stock.GoodsCount(product.Id));
            }
        }
        [Test] public void StoredMealsAreConsumedWithoutDuplicateDeliveryOrCharges()
        {
            using(var s=Ready(11*60+59))
            {
                s.Backpack.Food.Packed.Clear();s.Pantry.Cooked.Clear();s.HomeFridge.Food.Packed.Clear();
                s.HomeBox.Food.Packed.Add(new FoodBatch{Id="rice",Name="饭",Count=5,Hunger=30,ExpireDay=5});
                s.Player.Hunger=40;Assert.True(s.SetHomeAutomation(new HomeAutomationSettings{Meals=true,Takeaway=true,Supplies=true}));
                var cash=s.Ledger.Balance;s.AdvanceClock(1);int remaining=s.HomeBox.Food.Packed.Sum(b=>b.Count);
                Assert.Less(remaining,5);Assert.Greater(s.Player.Hunger,60);Assert.AreEqual(cash,s.Ledger.Balance);
                s.AdvanceClock(1);Assert.AreEqual(remaining,s.HomeBox.Food.Packed.Sum(b=>b.Count));
            }
        }
        [Test] public void TakeawayDailyBudgetAndAlertDeduplicationSurviveSaveLoad()
        {
            using(var s=Ready(11*60+59))
            {
                s.Backpack.Food.Packed.Clear();s.Pantry.Cooked.Clear();s.HomeBox.Food.Packed.Clear();s.Player.Hunger=50;
                Assert.True(s.SetHomeAutomation(new HomeAutomationSettings{Takeaway=true,DailyMealBudget=25}));
                var cash=s.Ledger.Balance;s.AdvanceClock(1);Assert.AreEqual(cash-Money.FromYuan(25),s.Ledger.Balance);Assert.AreEqual(25,s.WorkbenchMealSpent);
                s.Clock.Restore(s.Clock.Today,18*60+29);s.Player.Hunger=50;int notices=0;s.Popup+=(a,b)=>notices++;
                s.AdvanceClock(1);Assert.AreEqual(cash-Money.FromYuan(25),s.Ledger.Balance);Assert.AreEqual(1,notices);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.True(loaded.CanUseHomeWorkbench);Assert.AreEqual(25,loaded.WorkbenchMealSpent);
                    loaded.Clock.Restore(loaded.Clock.Today,18*60+29);loaded.Player.Hunger=40;int repeated=0;loaded.Popup+=(a,b)=>repeated++;
                    loaded.AdvanceClock(1);Assert.AreEqual(cash-Money.FromYuan(25),loaded.Ledger.Balance);Assert.AreEqual(0,repeated);
                }
            }
        }
        [Test] public void AutomaticSleepAdvancesClockAndSuppliesConsumeOncePerDay()
        {
            using(var s=Ready(15*60+59))
            {
                s.HomeBox.AddGift(Gifts.Fruit,2);Assert.True(s.SetHomeAutomation(new HomeAutomationSettings{Supplies=true,Sleep=true}));
                s.AdvanceClock(1);Assert.AreEqual(1,s.HomeBox.GiftCount(Gifts.Fruit));
                using(var loaded=GameSession.Load(s.City,s.CreateSave()))
                {
                    loaded.Clock.Restore(loaded.Clock.Today,15*60+59);loaded.AdvanceClock(1);Assert.AreEqual(1,loaded.HomeBox.GiftCount(Gifts.Fruit));
                    loaded.Clock.Restore(loaded.Clock.Today,22*60+59);loaded.Player.Hunger=90;loaded.AdvanceClock(1);
                    Assert.AreEqual(7*60,loaded.Clock.MinuteOfDay);Assert.GreaterOrEqual(loaded.SleepHoursLast24,7.99);
                }
            }
        }
        [Test] public void AutomaticSleepWaitsForPendingDecisionAndResumesDuringTheRestWindow()
        {
            using(var s=Ready(22*60+59))
            {
                Assert.True(s.SetHomeAutomation(new HomeAutomationSettings{Sleep=true}));s.RaiseEvent("flyer");
                s.AdvanceClock(1);Assert.AreEqual(23*60,s.Clock.MinuteOfDay);Assert.NotNull(s.PendingEvent);
                Assert.True(s.ResolveEvent(1));s.AdvanceClock(1);Assert.AreEqual(7*60,s.Clock.MinuteOfDay);Assert.Greater(s.SleepHoursLast24,7.9);
            }
        }
        [Test] public void AutomationRequiresOwnershipHomeAndExistingFacilities()
        {
            using(var s=Ready(22*60+59))
            {
                Assert.True(s.SetHomeAutomation(new HomeAutomationSettings{Sleep=true,Clothing=true}));
                var layout=(System.Collections.Generic.List<PlacedFurniture>)s.HomeLayout;layout.RemoveAll(p=>FurnitureCatalog.Get(p.Id).Role==FurnitureRole.Bed||FurnitureCatalog.Get(p.Id).Role==FurnitureRole.Wardrobe);
                s.AdvanceClock(1);Assert.AreEqual(23*60,s.Clock.MinuteOfDay);Assert.True(s.WorkbenchAlerts.Any(a=>a.Key=="bed"));Assert.True(s.WorkbenchAlerts.Any(a=>a.Key=="wardrobe"));
                GameSessionTests.Teleport(s,"pc");Assert.False(s.SetHomeAutomation(new HomeAutomationSettings()));
            }
        }
        [Test] public void PriceHistoryRecordsRealDaysAndPersonalCashSeparatesFinancingAndInvestment()
        {
            using(var s=Ready())
            {
                Assert.IsNull(s.WorkbenchPriceChange("computer"));int day=s.Clock.Today.TotalDays;
                s.Clock.Restore(new GameDate(day+1),10*60);s.RecordWorkbenchPrices();Assert.AreEqual(2,s.WorkbenchPrices("computer").Length);
                s.RecordWorkbenchPrices();Assert.AreEqual(2,s.WorkbenchPrices("computer").Length);
                s.Ledger.Credit(s.Clock.Today,"收入",Money.FromYuan(100),"工作");
                s.Ledger.Credit(s.Clock.Today,"贷款",Money.FromYuan(1000),"贷款",new LedgerAttribution(LedgerBusinessKind.Financing));
                s.Ledger.Credit(s.Clock.Today,"投资",Money.FromYuan(500),"卖出",new LedgerAttribution(LedgerBusinessKind.AssetInvestment));
                var flow=s.WorkbenchPersonalCash(s.Clock.Today,s.Clock.Today);Assert.AreEqual(100,flow.income.Yuan);
                using(var loaded=GameSession.Load(s.City,s.CreateSave()))Assert.AreEqual(2,loaded.WorkbenchPrices("computer").Length);
            }
        }
        [Test] public void WorkbenchProvidesInvestmentAndChatConnectionWithoutGrantingAPhone()
        {
            using(var s=new GameSession(PCShopSell.Core.World.CityLayout.FromData(GameSessionTests.Layout()),7,Money.FromYuan(200000),GameTimestamp.From(new GameDate(0),600),false,false))
            {
                s.EventsEnabled=false;s.HomeBox.AddFurniture(GameSession.HomeWorkbenchId,1);
                Assert.False(s.HasSmartphone);Assert.True(s.CanUseHomeWorkbench);Assert.True(s.HasChatApp);
                var cash=s.Ledger.Balance;Assert.True(s.TradeCityInvestment("hardware",1,true));Assert.AreEqual(1,s.CityInvestmentUnits("hardware"));Assert.Less(s.Ledger.Balance.Cents,cash.Cents);
                GameSessionTests.Teleport(s,"pc");cash=s.Ledger.Balance;Assert.False(s.TradeCityInvestment("hardware",1,true));Assert.AreEqual(cash,s.Ledger.Balance);Assert.False(s.HasSmartphone);Assert.False(s.HasChatApp);
            }
        }
        [Test] public void OwningAnUnconfiguredScreenDoesNotDisableExistingMealDelivery()
        {
            using(var s=Ready(11*60+59))
            {
                s.Backpack.Food.Packed.Clear();s.Pantry.Cooked.Clear();s.Player.Hunger=50;s.ToggleMealDelivery();
                var cash=s.Ledger.Balance;s.AdvanceClock(2);Assert.AreEqual(cash-Money.FromYuan(25),s.Ledger.Balance);
            }
        }
        [Test] public void RemoteActionsOnASecondShopNeverChangeTheManagedShopOrPlayerLocation()
        {
            using(var s=Ready())
            {
                var main=s.MainShop;s.PlayerShop.Reputation=80;s.DebugAddListing("tea",0,60,Money.FromYuan(3000));GameSessionTests.Teleport(s,"agency");Assert.True(s.SignLease("tea",true));
                var second=s.ShopAt("tea");GameSessionTests.Teleport(s,s.Home.Id);
                var focus=s.CurrentShop;var part=PartCatalog.Get("cpu0");second.Sim.Stock.ForceAdd(part,true,1);second.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"cpu",60}});
                var unchanged=main.Sim.PriceOf(part,true);long next=second.Sim.PriceOf(part,true).Yuan+1;
                Assert.True(s.SetWorkbenchPartPrice(second.Place.Id,part.Id,true,next));Assert.AreEqual(next,second.Sim.PriceOf(part,true).Yuan);Assert.AreEqual(unchanged,main.Sim.PriceOf(part,true));
                Assert.True(s.SetWorkbenchHours(second.Place.Id,10,20,false));Assert.AreEqual(10*60,second.Sim.OpenMinute);Assert.AreEqual(s.Home,s.CurrentPlace);Assert.AreSame(focus,s.CurrentShop);
                s.HomeBox.TakeFurniture(GameSession.HomeWorkbenchId,1);Assert.False(s.CanUseHomeWorkbench);Assert.False(s.SetWorkbenchPartPrice(second.Place.Id,part.Id,true,next+1));
            }
        }
        [Test] public void CheatButtonOpensAndClosesWorkbenchWithoutBuyingOrRemovingFurniture()
        {
            using(var s=HomeTests.Make(200000,600))
            {
                s.EventsEnabled=false;GameSessionTests.Teleport(s,s.Home.Id);var cash=s.Ledger.Balance;
                var root=new GameObject("WorkbenchCheatTest");root.SetActive(false);var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                void Cheats()=>typeof(GameUi).GetMethod("OpenCheatPanel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);
                Button Toggle()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name=="CheatHomeWorkbench");
                try
                {
                    Assert.False(s.OwnsHomeWorkbench);Assert.False(s.CanUseHomeWorkbench);Cheats();Toggle().onClick.Invoke();
                    Assert.True(s.DebugHomeWorkbenchEnabled);Assert.True(s.CanUseHomeWorkbench);Assert.False(s.OwnsHomeWorkbench);Assert.False(s.Time.IsHeld);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="WorkbenchTab_0"));
                    Cheats();StringAssert.Contains("关闭大屏",Toggle().GetComponentInChildren<Text>().text);Toggle().onClick.Invoke();
                    Assert.False(s.DebugHomeWorkbenchEnabled);Assert.False(s.CanUseHomeWorkbench);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="WorkbenchTab_0"));
                    ui.CloseAllWindows();Assert.False(s.Time.IsHeld);Assert.AreEqual(cash,s.Ledger.Balance);
                    s.HomeBox.AddFurniture(GameSession.HomeWorkbenchId,1);s.DebugSetHomeWorkbench(true);Cheats();Toggle().onClick.Invoke();
                    Assert.False(s.DebugHomeWorkbenchEnabled);Assert.True(s.CanUseHomeWorkbench);Assert.AreEqual(1,s.HomeBox.FurnitureCount(GameSession.HomeWorkbenchId));
                }
                finally{ui.CloseAllWindows();foreach(var c in ui.Canvases)UnityEngine.Object.DestroyImmediate(c.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }
        [Test] public void WorkbenchHasFourRealPagesAndRemotePriceConfirmationChangesActualShop()
        {
            using(var s=Ready())
            {
                var product=Gifts.All.First(g=>ClothingProducts.IsClothing(g.Id));s.PlayerShop.Stock.Clear();s.PlayerShop.Stock.ForceAddGoods(product.Id,3);
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{ShelfKinds.ForGift(product.Id).Id,60}});
                var root=new GameObject("HomeWorkbenchUiTest");root.SetActive(false);var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                Button Button(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name==name);
                try
                {
                    ui.OpenHomeWorkbench();Assert.False(s.Time.IsHeld);Assert.AreEqual(4,ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Count(b=>b.name.StartsWith("WorkbenchTab_")));
                    Assert.IsFalse(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name.StartsWith("WorkbenchBuyers_")));
                    Button("WorkbenchTab_1").onClick.Invoke();ui.RebuildWindows();Button("WorkbenchBusiness_1").onClick.Invoke();ui.RebuildWindows();
                    var before=s.PlayerShop.GoodsPriceOf(product.Id);Button("WorkbenchGoods_"+product.Id).onClick.Invoke();
                    Button("PricePlus_goods_"+product.Id).onClick.Invoke();Assert.AreEqual(before,s.PlayerShop.GoodsPriceOf(product.Id));
                    Button("ConfirmPrice_goods_"+product.Id).onClick.Invoke();Assert.AreEqual(before.Yuan+1,s.PlayerShop.GoodsPriceOf(product.Id).Yuan);
                    ui.CloseTopWindow();s.SetSmartphone(true);Button("WorkbenchTab_2").onClick.Invoke();ui.RebuildWindows();
                    // 投资页直接内嵌手机证券的交易所：不打开新窗口、不暂停时间。
                    Assert.NotNull(Button("StockQuickBuy_hardware"));Assert.False(s.Time.IsHeld);
                    if(s.StockTradeBlocker("hardware",1,"buy")==null)
                    {
                        var tradeCash=s.Ledger.Balance;long held=s.CityInvestmentUnits("hardware");
                        Button("StockQuickBuy_hardware").onClick.Invoke();
                        Assert.AreEqual(held+1,s.CityInvestmentUnits("hardware"));Assert.Less(s.Ledger.Balance.Cents,tradeCash.Cents);
                    }
                    Button("WorkbenchTab_3").onClick.Invoke();ui.RebuildWindows();Assert.NotNull(Button("WorkbenchContacts"));
                    Button("WorkbenchAutomation").onClick.Invoke();Assert.NotNull(Button("Automation_Sleep"));Button("Automation_Sleep").onClick.Invoke();Assert.True(s.HomeAutomation.Sleep);
                    Assert.AreEqual(s.Home,s.CurrentPlace);
                }
                finally{ui.CloseAllWindows();foreach(var c in ui.Canvases)UnityEngine.Object.DestroyImmediate(c.gameObject);UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }
    }
}
