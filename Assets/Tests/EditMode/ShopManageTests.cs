using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using PCShopSell.Core.Home;

namespace PCShopSell.Tests
{
    /// <summary>按配件定价、自动补货、经营报表。</summary>
    public class ShopManageTests
    {
        private static GameSession Make(bool phone = true, long cash = 20000)
        {
            return new GameSession(PCShopSell.Core.World.CityLayout.FromData(GameSessionTests.Layout()), 7,
                Money.FromYuan(cash), GameTimestamp.From(new GameDate(3), 8 * 60), false, phone);
        }

        [TestCase("computer")]
        [TestCase("clothing")]
        [TestCase("models")]
        [TestCase("flowers")]
        [TestCase("produce")]
        [TestCase("food")]
        public void StockAndPricingUseActualGoodsForTheCurrentBusiness(string kind)
        {
            using(var s=Make())
            {
                GameSessionTests.Teleport(s,"pc");
                Assert.IsTrue(s.SetShopMainCategory(kind));
                var shop=s.PlayerShop;shop.Settings.BaseCustomersPerHour=0;shop.Stock.Clear();
                var product=Gifts.All.First(g=>RetailShopTypes.Sells(kind,g.Id));
                var part=PartCatalog.Get("cpu0");
                shop.Stock.ForceAddGoods(product.Id,3);shop.Stock.ForceAdd(part,true,2);
                var root=new GameObject("ActualShopGoodsRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existingEventSystem=UnityEngine.EventSystems.EventSystem.current;
                var ui=new PCShopSell.Game.UI.GameUi(controller);
                Transform[] Named(string prefix)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith(prefix)).ToArray();
                UnityEngine.UI.Button Button(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b=>b.name==name);
                try
                {
                    ui.OpenDock();ui.DebugSelectTab(2);
                    Assert.AreEqual(2,Named("Pricing_").Length,"只展示实际持有的商品，混售不受主售限制");
                    Assert.AreEqual(1,Named("Pricing_goods_"+product.Id).Length);
                    Assert.AreEqual(1,Named("Pricing_cpu0").Length);
                    string IntentSummary()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Text>()).Single(t=>t.name=="PricingPurchaseIntentSummary").text;
                    string ExpectedIntent()=>((shop.GoodsPurchaseIntentAtPrice(product.Id,shop.GoodsPriceOf(product.Id).Yuan)*3
                        +shop.PartPurchaseIntentAtPrice(part,true,shop.PriceOf(part,true).Yuan)*2)/5*100).ToString("0")+"%";
                    StringAssert.Contains("购买意愿",IntentSummary());StringAssert.Contains(ExpectedIntent(),IntentSummary());
                    var slider=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Slider>()).Single(t=>t.name=="GlobalMarkupSlider");
                    slider.value=1.4f;StringAssert.Contains(ExpectedIntent(),IntentSummary());
                    var before=shop.GoodsPriceOf(product.Id);
                    Button("EditPrice_goods_"+product.Id).onClick.Invoke();
                    Button("PricePlus_goods_"+product.Id).onClick.Invoke();
                    Assert.AreEqual(before,shop.GoodsPriceOf(product.Id),"预览不能提前修改售价");
                    Button("ConfirmPrice_goods_"+product.Id).onClick.Invoke();
                    Assert.AreEqual(before.Yuan+1,shop.GoodsPriceOf(product.Id).Yuan);
                    StringAssert.Contains(ExpectedIntent(),IntentSummary(),"单独改价后顶部参考值也必须更新");
                    ui.CloseAllWindows();ui.OpenDock();ui.DebugSelectTab(1);
                    Assert.AreEqual(1,Named("StockGoods_"+product.Id).Length);
                    Assert.AreEqual(1,Named("StockPart_cpu0").Length,"实际配件在任何主售店都可管理");
                    Assert.AreEqual(2,shop.Stock.CountAll(part));Assert.AreEqual(3,shop.Stock.GoodsCount(product.Id));
                    Assert.IsTrue(shop.Stock.TryRemoveGoods(product.Id,3));
                    typeof(PCShopSell.Game.UI.GameUi).GetMethod("RefreshDock",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,null);
                    Assert.AreEqual(0,Named("StockGoods_"+product.Id).Length,"售罄后库存列表应刷新");
                    ui.DebugSelectTab(2);
                    Assert.AreEqual(1,Named("Pricing_").Length,"售罄商品不能继续显示在调价页");
                    int shelf=s.CurrentShop.Layout.Count;
                    s.CurrentShop.Layout.Add(new PCShopSell.Core.Home.PlacedFurniture{Id=FurnitureCatalog.Shelf});
                    foreach(string group in new[]{"服装","电脑配件","模型手办","零售商品","全部"})
                    {
                        ui.OpenShelfKindChooser(shelf);
                        Button("ShelfGroup_"+group).onClick.Invoke();
                        var expected=PCShopSell.Core.Inventory.ShelfKinds.All.Where(k=>group=="全部" || k.BrowseGroup==group).Select(k=>"ShelfKind_"+k.Id).ToArray();
                        CollectionAssert.AreEquivalent(expected,Named("ShelfKind_").Select(t=>t.name));
                        Button(expected.First()).onClick.Invoke();
                        Assert.AreEqual(expected.First().Substring("ShelfKind_".Length),s.ShelfKindAt(shelf).Id);
                    }
                }
                finally
                {
                    ui.CloseAllWindows();
                    foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existingEventSystem==null && UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void PartMarkup_OverridesGlobal_AndSaves()
        {
            GameSession s = Make();
            PartDef gpu = PartCatalog.Get("gpu1"), ram = PartCatalog.Get("ram0");
            s.SetMarkup(1.2);
            Money ramGlobal = s.PlayerShop.PriceOf(ram, true);
            s.PlayerShop.SetPartMarkup(gpu, 1.5);
            Assert.AreEqual(1.5, s.PlayerShop.MarkupOf(gpu), 1e-9);
            Assert.AreEqual(1.2, s.PlayerShop.MarkupOf(ram), 1e-9, "没单独定的跟全局");
            Assert.AreEqual(ramGlobal, s.PlayerShop.PriceOf(ram, true));
            Assert.Greater(s.PlayerShop.PriceOf(gpu, true).Cents, (s.Market.PriceOf(gpu) * 1).Cents * 1.5, "单独 1.5 倍，盒装再贵 12%");

            s.SetMarkup(1.0);
            Assert.AreEqual(1.5, s.PlayerShop.MarkupOf(gpu), 1e-9, "全局变了，单独定价不变");

            s.PlayerShop.SetPartMarkup(gpu, 9.0);
            Assert.AreEqual(1.6, s.PlayerShop.MarkupOf(gpu), 1e-9, "上限 1.6");

            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(1.6, l.PlayerShop.MarkupOf(PartCatalog.Get("gpu1")), 1e-9);
            Assert.IsFalse(l.PlayerShop.HasPartMarkup(PartCatalog.Get("ram0")));

            s.PlayerShop.SetPartMarkup(gpu, null);
            Assert.AreEqual(1.0, s.PlayerShop.MarkupOf(gpu), 1e-9, "恢复跟随全局");
        }

        [Test]
        public void FinanceReport_SumsRevenueAndExpensePerDay()
        {
            var ledger = new Ledger(Money.FromYuan(1000));
            var d5 = new GameDate(5);
            ledger.Credit(d5, FinanceReport.RetailCategory, Money.FromYuan(300));
            ledger.Credit(d5, FinanceReport.BuildCategory, Money.FromYuan(200));
            ledger.Credit(d5, "其他", Money.FromYuan(99));
            ledger.ForceDebit(d5, "进货", Money.FromYuan(120));
            ledger.Credit(new GameDate(7), FinanceReport.RetailCategory, Money.FromYuan(50));
            ledger.Credit(new GameDate(1), FinanceReport.RetailCategory, Money.FromYuan(999));

            var days = FinanceReport.Daily(ledger, new GameDate(7), 3);
            Assert.AreEqual(3, days.Count);
            Assert.AreEqual(5, days[0].Date.TotalDays);
            Assert.AreEqual(Money.FromYuan(500), days[0].Revenue, "只统计零售和装机");
            Assert.AreEqual(Money.FromYuan(120), days[0].Expense);
            Assert.AreEqual(Money.Zero, days[1].Revenue);
            Assert.AreEqual(Money.FromYuan(50), days[2].Revenue);

            Assert.AreEqual(2, FinanceReport.Daily(ledger, new GameDate(1), 5).Count, "不画游戏开始以前的日子");
        }

        [Test]
        public void AutoRestock_OrdersWhenLow_AndRespectsPhoneAndCash()
        {
            GameSession s = Make();
            PartDef ram = PartCatalog.Get("ram1");
            Assert.IsTrue(s.UninstallApp(PhoneApps.Wholesale));
            s.SetRestockTarget(ram.Id, 10);
            Assert.AreEqual(10, s.RestockTarget(ram.Id));

            s.RunAutoRestock();
            Assert.AreEqual(0, s.Online.Pending.Count, "没装 1788 不能自动下单");
            Assert.IsNotNull(s.LastRestockNote);

            Assert.IsTrue(s.InstallApp(PhoneApps.Wholesale));
            Money before = s.Ledger.Balance;
            s.RunAutoRestock();
            Assert.AreEqual(1, s.Online.Pending.Count, "库存 0 < 40%，应该下单");
            Assert.AreEqual(10, s.PendingOnlineCount(ram.Id), "补到目标");
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);

            s.RunAutoRestock();
            Assert.AreEqual(1, s.Online.Pending.Count, "在途也算库存，不重复下单");

            s.SetRestockTarget(ram.Id, 0);
            Assert.AreEqual(0, s.RestockTarget(ram.Id));
        }

        [Test]
        public void AutoRestock_SkipsWhenCashWouldRunLow_AndDailyTriggerAt9()
        {
            GameSession s = Make(true, 300);
            s.SetRestockTarget("cpu1", 10);
            s.RunAutoRestock();
            Assert.AreEqual(0, s.Online.Pending.Count, "钱不够留 500 的底");

            GameSession r = Make();
            r.SetRestockTarget("ssd1", 6);
            r.AdvanceClock(60 + 5);
            Assert.AreEqual(1, r.Online.Pending.Count, "8:00 出发，推进过 9:00 自动补货");

            GameSession l = GameSession.Load(r.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(r.CreateSave())));
            Assert.AreEqual(6, l.RestockTarget("ssd1"));
        }
    }
}
