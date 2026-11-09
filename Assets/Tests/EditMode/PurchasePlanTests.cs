using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class PurchasePlanTests
    {
        private static GameSession Make()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s = new GameSession(city, 7, Money.FromYuan(500000), GameTimestamp.From(new GameDate(0), 9 * 60));
            s.EventsEnabled = false;
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.MainShop.Buyer = new Employee { Name = "陈启明", DailyWageYuan = 150, CheckoutMinutes = 3 };
            CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
            return s;
        }
        private static EmployeePurchasePlan Mixed(GameSession s)
        {
            var offers = s.EmployeePurchaseCatalog();
            return new EmployeePurchasePlan
            {
                BudgetYuan = 1, Transport = VehicleKind.Taxi,
                Items = new[] { offers.First(o => o.Id == "cpu0" && o.Kind == PurchaseGoodsKind.Part).Line(1),
                    offers.First(o => o.Id == "ram0" && o.Kind == PurchaseGoodsKind.Part).Line(1),
                    offers.First(o => o.Id == Gifts.Drink && o.Kind == PurchaseGoodsKind.Gift).Line(10) }
            };
        }
        [Test]
        public void BrowseSourceSurvivesCompletingAndSavingWithoutChangingPurchaseLines()
        {
            using(var s=Make())
            {
                var worker=s.MainShop.Buyer;
                var plan=Mixed(s);
                Assert.IsTrue(s.ConfigureEmployeePurchase(worker.Id,plan));
                var source=s.EmployeePurchaseCatalog().First(o=>ClothingProducts.IsClothing(o.Id)).Source.Id;
                long cash=s.Ledger.Balance.Cents;
                Assert.IsTrue(s.RememberEmployeePurchaseSource(worker.Id,source));
                var draft=worker.PurchasePlan.Copy();
                s.PrepareEmployeePurchaseDraft(draft);
                Assert.AreEqual(source,s.EmployeePurchaseBrowseSource(draft));
                Assert.IsTrue(s.CompleteEmployeePurchase(worker.Id,draft));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.AreEqual(source,loaded.EmployeePurchaseBrowseSource(loaded.MainShop.Buyer.PurchasePlan));
                    CollectionAssert.AreEqual(plan.Items.Select(GameSession.PurchaseKey),loaded.MainShop.Buyer.PurchasePlan.Items.Select(GameSession.PurchaseKey));
                    Assert.AreEqual(cash,loaded.Ledger.Balance.Cents);
                }
                Assert.IsFalse(s.RememberEmployeePurchaseSource(worker.Id,"missing-source"));
                Assert.AreEqual(source,s.EmployeePurchaseBrowseSource(worker.PurchasePlan));
                var second=new Employee{Name="另一位采购员"};
                s.MainShop.Buyer=second;
                Assert.IsNull(s.EmployeePurchaseBrowseSource(second.PurchasePlan));
                Assert.IsTrue(s.RememberEmployeePurchaseSource(second.Id,null));
                Assert.AreEqual(source,s.EmployeePurchaseBrowseSource(worker.PurchasePlan));
            }
        }
        [Test]
        public void ExplicitAllSourcesSurvivesSavingAndOverridesSingleSupplierInference()
        {
            using(var s=Make())
            {
                var offer=s.EmployeePurchaseCatalog().First();
                var plan=new EmployeePurchasePlan{Items=new[]{offer.Line(2)}};
                Assert.AreEqual(offer.Source.Id,s.EmployeePurchaseBrowseSource(plan));
                Assert.IsTrue(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id,plan));
                Assert.IsTrue(s.RememberEmployeePurchaseSource(s.MainShop.Buyer.Id,null));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.IsTrue(loaded.MainShop.Buyer.PurchasePlan.BrowseSourceSelected);
                    Assert.IsNull(loaded.EmployeePurchaseBrowseSource(loaded.MainShop.Buyer.PurchasePlan));
                    Assert.AreEqual(2,loaded.MainShop.Buyer.PurchasePlan.Items.Single().Count);
                }
                var legacy=new EmployeePurchasePlan{SourceId=offer.Source.Id,Items=new[]{new PurchaseLine{PartId=offer.Id,Kind=offer.Kind,Count=1}}};
                s.PrepareEmployeePurchaseDraft(legacy);
                Assert.AreEqual(offer.Source.Id,s.EmployeePurchaseBrowseSource(legacy));
                Assert.IsNull(s.EmployeePurchaseBrowseSource(Mixed(s)));
                Assert.IsNull(s.EmployeePurchaseBrowseSource(new EmployeePurchasePlan{BrowseSourceSelected=true,BrowseSourceId="missing-source"}));
            }
        }
        [Test]
        public void EmployeePurchaseWindowReopensAtTheLastBrowsedSource()
        {
            using(var s=Make())
            {
                var root=new GameObject("PurchaseBrowseRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existingEventSystem=UnityEngine.EventSystems.EventSystem.current;
                var ui=new PCShopSell.Game.UI.GameUi(controller);
                UnityEngine.UI.Button Button(string caption)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>())
                    .Single(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text==caption);
                UnityEngine.UI.Button[] CategoryButtons()=>ui.Canvases.Select(c=>c.transform.Find("Window_商品分类"))
                    .Single(t=>t!=null).GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Where(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text!="关闭").ToArray();
                void AssertFixedLayout()
                {
                    var window=ui.Canvases.SelectMany(c=>c.transform.Cast<Transform>()).Single(t=>t.name.StartsWith("Window_定期采购"));
                    var panel=(RectTransform)window.Find("Panel");
                    var body=(RectTransform)panel.Find("FixedContent");
                    var footer=(RectTransform)panel.Find("FixedContent/BuyerSplit/BuyerSide");
                    var budget=footer.GetComponentsInChildren<RectTransform>().First(t=>t.name=="EmployeePurchaseBudget");
                    Assert.IsNotNull(body,"外层应为固定布局");
                    Assert.IsNull(body.GetComponent<UnityEngine.UI.ContentSizeFitter>());
                    var scrolls=window.GetComponentsInChildren<UnityEngine.UI.ScrollRect>();
                    Assert.AreEqual(2,scrolls.Length,"只有商品目录和已选清单可以滚动");
                    var canvas=window.GetComponentInParent<Canvas>();
                    canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;
                    canvas.renderMode=RenderMode.WorldSpace;
                    Rect BoundsIn(RectTransform parent,RectTransform target)
                    {
                        var corners=new Vector3[4];target.GetWorldCorners(corners);
                        var points=corners.Select(p=>parent.InverseTransformPoint(p)).ToArray();
                        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                    }
                    foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1024,768)})
                    {
                        var viewport=(RectTransform)canvas.transform;viewport.sizeDelta=size;
                        panel.GetComponent<PCShopSell.Game.UI.WindowFit>().Fit();
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);Canvas.ForceUpdateCanvases();
                        foreach(var target in new[]{budget,(RectTransform)footer.Find("PurchaseTransport"),(RectTransform)footer.Find("PurchaseActions")})
                        {
                            var rect=BoundsIn(viewport,target);
                            Assert.GreaterOrEqual(rect.yMin,viewport.rect.yMin-.1f,"预算及操作必须在画面内："+size);
                            Assert.LessOrEqual(rect.yMax,viewport.rect.yMax+.1f);
                            var local=BoundsIn(footer,target);
                            Assert.GreaterOrEqual(local.yMin,footer.rect.yMin-.1f);
                            Assert.LessOrEqual(local.yMax,footer.rect.yMax+.1f);
                        }
                        foreach(RectTransform child in body)
                        {
                            var rect=BoundsIn(body,child);
                            Assert.GreaterOrEqual(rect.yMin,body.rect.yMin-.1f,"外层内容不能溢出："+child.name);
                            Assert.LessOrEqual(rect.yMax,body.rect.yMax+.1f);
                        }
                        var before=BoundsIn(viewport,budget);
                        foreach(var scroll in scrolls)scroll.verticalNormalizedPosition=0;
                        Canvas.ForceUpdateCanvases();
                        Assert.AreEqual(before,BoundsIn(viewport,budget),"滚动商品列表不能带动预算");
                        Assert.GreaterOrEqual(panel.Find("FixedContent/BuyerSplit/BuyerCatalog/PurchaseCatalog").GetComponent<RectTransform>().rect.height,160);
                        Assert.GreaterOrEqual(panel.Find("FixedContent/BuyerSplit/BuyerSide/PurchaseSelected").GetComponent<RectTransform>().rect.height,100);
                    }
                }
                try
                {
                    var source=s.City.GetPlace("clothing_wholesale");
                    var plan=Mixed(s);
                    Assert.IsTrue(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id,plan));
                    ui.OpenEmployeePurchase(s.MainShop.Buyer.Id);
                    AssertFixedLayout();
                    Button("全部货源  ▼").onClick.Invoke();
                    Button(source.Name).onClick.Invoke();
                    Button("全部 ▾").onClick.Invoke();
                    var clothingCategories=new[]{"全部"}.Concat(s.EmployeePurchaseCatalog().Where(o=>o.Source.Id==source.Id).Select(o=>o.Category)).Distinct().ToArray();
                    CollectionAssert.AreEquivalent(clothingCategories,CategoryButtons().Select(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text));
                    string clothingCategory=clothingCategories.First(c=>c!="全部");
                    CategoryButtons().Single(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text==clothingCategory).onClick.Invoke();
                    Assert.IsNotNull(Button(source.Name+"  ▼"),"点击商品分类不能把已选货源重置为全部");
                    var electronics=s.EmployeePurchaseCatalog().First(o=>o.Kind==PurchaseGoodsKind.Part).Source;
                    Button(source.Name+"  ▼").onClick.Invoke();
                    Button(electronics.Name).onClick.Invoke();
                    Assert.IsNotNull(Button("全部 ▾"),"切换到不包含旧分类的货源时，分类应回到全部");
                    Button("全部 ▾").onClick.Invoke();
                    CollectionAssert.AreEquivalent(new[]{"全部"}.Concat(s.EmployeePurchaseCatalog().Where(o=>o.Source.Id==electronics.Id).Select(o=>o.Category)).Distinct(),CategoryButtons().Select(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text));
                    Assert.IsTrue(ui.CloseTopWindow());
                    Button(electronics.Name+"  ▼").onClick.Invoke();
                    Button("全部货源").onClick.Invoke();
                    Button("全部 ▾").onClick.Invoke();
                    CollectionAssert.AreEquivalent(new[]{"全部"}.Concat(s.EmployeePurchaseCatalog().Select(o=>o.Category)).Distinct(),CategoryButtons().Select(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text));
                    Assert.IsTrue(ui.CloseTopWindow());
                    Button("全部货源  ▼").onClick.Invoke();
                    Button(source.Name).onClick.Invoke();
                    ui.CloseAllWindows();
                    ui.OpenEmployeePurchase(s.MainShop.Buyer.Id);
                    Assert.IsNotNull(Button(source.Name+"  ▼"));
                    Button("完成").onClick.Invoke();
                    CollectionAssert.AreEqual(plan.Items.Select(GameSession.PurchaseKey),s.MainShop.Buyer.PurchasePlan.Items.Select(GameSession.PurchaseKey),"筛选不能改动已选采购商品及货源");
                    ui.OpenEmployeePurchase(s.MainShop.Buyer.Id);
                    Assert.IsNotNull(Button(source.Name+"  ▼"),"完成后删除草稿，也必须恢复上次的浏览货源");
                    AssertFixedLayout();
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
        public void MixedSources_AutoCostIgnoresOldBudget_AndShipsAllItemsExactlyOnceAfterJson()
        {
            using (var s = Make())
            {
                string id = s.MainShop.Buyer.Id;
                var plan = Mixed(s);
                Assert.AreEqual(3, plan.Items.Select(x => x.SourceId).Distinct().Count());
                Assert.True(s.ConfigureEmployeePurchase(id, plan));
                var q = s.QuoteEmployeePurchase(id);
                Assert.IsNull(q.Error, q.Error);
                Assert.Greater(q.Total.Cents, 100, "旧存档的一元预算不能拦住实际采购");
                var cash = s.Ledger.Balance;
                int cpu = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0"));
                int ram = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0"));
                int drinks = s.MainShop.Sim.Stock.GoodsCount(Gifts.Drink);
                Assert.True(s.DispatchEmployeePurchase(id));
                Assert.AreEqual(cash - q.Total, s.Ledger.Balance);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    Assert.AreEqual(3, trip.Items.Select(x => x.SourceId).Distinct().Count());
                    loaded.AdvanceClock((int)(Math.Max(trip.DeliveryAt, trip.ReturnAt) - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.AreEqual(cpu + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.AreEqual(ram + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0")));
                    Assert.AreEqual(drinks + 10, loaded.MainShop.Sim.Stock.GoodsCount(Gifts.Drink));
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count);
                    var paid = loaded.Ledger.Balance;
                    loaded.AdvanceClock(5);
                    Assert.AreEqual(paid, loaded.Ledger.Balance);
                    Assert.AreEqual(drinks + 10, loaded.MainShop.Sim.Stock.GoodsCount(Gifts.Drink));
                }
            }
        }

        [Test]
        public void Complete_EnablesDailyPlanAndRepeatsTheWholeListOnAnotherDay()
        {
            using (var s = Make())
            {
                var buyer = s.MainShop.Buyer;
                Assert.True(s.CompleteEmployeePurchase(buyer.Id, Mixed(s)));
                Assert.True(buyer.PurchasePlan.Daily);
                var cash = s.Ledger.Balance;
                s.AdvanceClock(1);
                Assert.Zero(s.EmployeePurchaseTrips.Count, "完成当天只保存，不出发");
                Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.False(s.DispatchEmployeePurchase(buyer.Id), "不能绕过次日生效规则");
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59); s.AdvanceClock(2);
                var first = s.EmployeePurchaseTrips.Single();
                Assert.AreEqual(3, first.Items.Length);
                s.AdvanceClock((int)(Math.Max(first.DeliveryAt, first.ReturnAt) - s.Clock.Now.TotalMinutes + 1));
                s.AdvanceClock(5);
                Assert.Zero(s.EmployeePurchaseTrips.Count, "同一天只自动尝试一次");
                s.Clock.Restore(new GameDate(2), 8 * 60 + 59);
                s.AdvanceClock(2);
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length, "次日仍按整份多源清单采购");
                Assert.True(s.CompleteEmployeePurchase(buyer.Id, new EmployeePurchasePlan()));
                Assert.False(buyer.PurchasePlan.Daily, "清空后完成应停止自动采购，已出发的货不受影响");
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length);
            }
        }

        [Test]
        public void CatalogIncludesAllExistingPhysicalGoods_AndFurnitureFoodReachStorage()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                foreach (PurchaseGoodsKind kind in Enum.GetValues(typeof(PurchaseGoodsKind))) Assert.True(offers.Any(o => o.Kind == kind), kind.ToString());
                var food = offers.First(o => o.Kind == PurchaseGoodsKind.Ingredient);
                var rice = offers.First(o => o.Kind == PurchaseGoodsKind.Rice);
                var furniture = offers.First(o => o.Kind == PurchaseGoodsKind.Furniture && o.Id == FurnitureCatalog.HomeBox);
                var plan = new EmployeePurchasePlan { Transport = VehicleKind.Taxi, Items = new[] { food.Line(2), rice.Line(3), furniture.Line(1) } };
                Assert.True(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id, plan));
                var quote = s.QuoteEmployeePurchase(s.MainShop.Buyer.Id);
                Assert.IsNull(quote.Error, quote.Error);
                Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                var trip = s.EmployeePurchaseTrips.Single();
                s.AdvanceClock((int)(Math.Max(trip.ReturnAt, trip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
                Assert.AreEqual(2, s.MainShop.Locker.Food.IngredientCount(food.Id) + s.MainShop.Temp.Food.IngredientCount(food.Id));
                Assert.AreEqual(1.5, s.MainShop.Locker.Food.RiceKg + s.MainShop.Temp.Food.RiceKg);
                Assert.AreEqual(1, s.MainShop.Locker.FurnitureCount(furniture.Id) + s.MainShop.Temp.FurnitureCount(furniture.Id));
                Assert.Zero(s.EmployeePurchaseTrips.Count);
            }
        }

        [Test]
        public void SwitchingBrowseSourceDoesNotLoseItemsOrModifyAnActiveTrip()
        {
            using (var s = Make())
            {
                var plan = Mixed(s);
                Assert.True(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id, plan));
                Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                var draft = s.MainShop.Buyer.PurchasePlan.Copy();
                Assert.Zero(s.ChangePurchaseSource(draft, plan.Items[0].SourceId));
                Assert.AreEqual(3, draft.Items.Length);
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length);
                draft.Items = draft.Items.Take(1).ToArray();
                Assert.True(s.CompleteEmployeePurchase(s.MainShop.Buyer.Id, draft));
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length, "保存新清单不能改动已出发的采购");
                Assert.AreEqual(1, s.MainShop.Buyer.PurchasePlan.Items.Length);
            }
        }

        private static void Wallet(GameSession s, Money amount)
        { s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - amount, "设置可用现金"); }

        [Test] public void PartialCash_BuysInListOrderAndReturnsChangeOnceAfterSave()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                var cheap = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                var dear = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu1");
                var plan = new EmployeePurchasePlan { Transport = VehicleKind.Walk, Items = new[] { cheap.Line(5), dear.Line(5) } };
                var id = s.MainShop.Buyer.Id; Assert.True(s.ConfigureEmployeePurchase(id, plan));
                var full = s.QuoteEmployeePurchase(id);
                Money wallet = cheap.Price * 2 + full.Travel + full.Freight + Money.FromYuan(1);
                var reserve=s.GetCityCashReserve(s.RestockReserveDays).Total;
                Wallet(s, reserve+wallet); var q = s.QuoteEmployeePurchase(id);
                Assert.IsNull(q.Error); Assert.AreEqual(wallet, q.Budget); Assert.Less(q.Total.Cents, q.RequiredTotal.Cents);
                Assert.True(s.DispatchEmployeePurchase(id)); Assert.AreEqual(reserve+Money.FromYuan(1), s.Ledger.Balance);
                var trip = s.EmployeePurchaseTrips.Single(); Assert.AreEqual(1, trip.Items.Length);
                Assert.AreEqual("cpu0", trip.Items[0].PartId); Assert.AreEqual(2, trip.Items[0].Count);
                Assert.AreEqual(0, trip.ChangeCents,"unused cash now stays in the shared wallet rather than being issued then refunded");
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled = false;
                    var copy = loaded.EmployeePurchaseTrips.Single(); Assert.AreEqual(0, copy.ChangeCents);
                    loaded.AdvanceClock((int)(System.Math.Max(copy.ReturnAt, copy.DeliveryAt) - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.AreEqual(reserve+Money.FromYuan(1), loaded.Ledger.Balance);
                    loaded.AdvanceClock(1); Assert.AreEqual(reserve+Money.FromYuan(1), loaded.Ledger.Balance);
                }
            }
        }
        [Test] public void PartialCash_SkipsUnaffordableEarlyItemButStillBuysLaterCheapItem()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                var dear = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu2");
                var cheap = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                var id = s.MainShop.Buyer.Id;
                Assert.True(s.ConfigureEmployeePurchase(id, new EmployeePurchasePlan { Transport = VehicleKind.Walk, Items = new[] { dear.Line(1), cheap.Line(1) } }));
                var full = s.QuoteEmployeePurchase(id);
                var reserve=s.GetCityCashReserve(s.RestockReserveDays).Total;
                Wallet(s, reserve+cheap.Price + full.Travel + full.Freight);
                Assert.True(s.DispatchEmployeePurchase(id));
                var items = s.EmployeePurchaseTrips.Single().Items;
                Assert.AreEqual(1, items.Length); Assert.AreEqual("cpu0", items[0].PartId);
                Assert.GreaterOrEqual(s.Ledger.Balance.Cents,reserve.Cents);
            }
        }
        [Test] public void Complete_NextDayEligibilityAndOrderSurviveJsonSave()
        {
            using (var s = Make())
            {
                var plan = Mixed(s); plan.Items = plan.Items.Reverse().ToArray();
                var id = s.MainShop.Buyer.Id; var cash = s.Ledger.Balance;
                Assert.True(s.CompleteEmployeePurchase(id, plan));
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled = false; loaded.AdvanceClock(1);
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count); Assert.AreEqual(cash, loaded.Ledger.Balance);
                    Assert.AreEqual(1, loaded.MainShop.Buyer.PurchasePlan.FirstDay);
                    CollectionAssert.AreEqual(plan.Items.Select(x => x.PartId), loaded.MainShop.Buyer.PurchasePlan.Items.Select(x => x.PartId));
                    loaded.Clock.Restore(new GameDate(1), 8 * 60 + 59); loaded.AdvanceClock(2);
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    CollectionAssert.AreEqual(plan.Items.Select(x => x.PartId), trip.Items.Select(x => x.PartId));
                }
            }
        }
        [Test] public void ThreeDayFrequency_RepeatsAndSurvivesSaveWithoutDailyPurchases()
        {
            using (var s = Make())
            {
                var plan = Mixed(s); plan.IntervalDays = 3; var id = s.MainShop.Buyer.Id;
                Assert.True(s.CompleteEmployeePurchase(id, plan));
                s.Clock.Restore(new GameDate(1), 9 * 60); s.AdvanceClock(1);Assert.Zero(s.EmployeePurchaseTrips.Count,"周二不采购");
                s.Clock.Restore(new GameDate(2), 9 * 60); s.AdvanceClock(1);
                var trip = s.EmployeePurchaseTrips.Single();
                s.AdvanceClock((int)(System.Math.Max(trip.ReturnAt, trip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled=false;Assert.AreEqual(PurchaseFrequency.MondayWednesdayFriday,loaded.MainShop.Buyer.PurchasePlan.Schedule);
                    Assert.AreEqual(4,loaded.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                    loaded.Clock.Restore(new GameDate(3),9*60);loaded.AdvanceClock(1);Assert.Zero(loaded.EmployeePurchaseTrips.Count,"周四不采购");
                    loaded.Clock.Restore(new GameDate(4),9*60);loaded.AdvanceClock(1);Assert.AreEqual(1,loaded.EmployeePurchaseTrips.Count,"周五再次采购");
                    Assert.AreEqual(7,loaded.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                }
            }
        }
        [Test] public void MondayWednesdayFriday_FailedAttemptWaitsUntilNextScheduledDay()
        {
            using(var s=Make())
            {
                var plan=Mixed(s);plan.IntervalDays=3;Assert.True(s.CompleteEmployeePurchase(s.MainShop.Buyer.Id,plan));
                Wallet(s,Money.FromYuan(1));s.Clock.Restore(new GameDate(2),9*60);s.AdvanceClock(1);
                Assert.Zero(s.EmployeePurchaseTrips.Count);Assert.AreEqual(4,s.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                s.DebugAddCash(500000);s.Clock.Restore(new GameDate(3),9*60);s.AdvanceClock(1);Assert.Zero(s.EmployeePurchaseTrips.Count);
                s.Clock.Restore(new GameDate(4),9*60);s.AdvanceClock(1);Assert.AreEqual(1,s.EmployeePurchaseTrips.Count);
            }
        }
        [Test] public void LegacyFrequency_ZeroMeansDailyAndCopyKeepsCadence()
        {
            var plan = new EmployeePurchasePlan { IntervalDays = 0, FirstDay = 1, LastDay = 1 };
            Assert.AreEqual(1, plan.EveryDays); Assert.AreEqual(2, plan.NextPurchaseDay);
            Assert.AreEqual(1, plan.Copy().IntervalDays);
            plan.IntervalDays = 3; Assert.AreEqual(2, plan.Copy().NextPurchaseDay);
        }
        [Test]
        public void PostingAnotherAdRetainsPreviousCandidates_AndAddsMoreTomorrow()
        {
            using (var s = GameSessionTests.Make(50000, 9 * 60))
            {
                s.EventsEnabled = false;
                GameSessionTests.Teleport(s, "pc");
                Assert.True(s.PostJobAd());
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59); s.AdvanceClock(2);
                var originals = s.Candidates.Select(x => x.Id).ToArray();
                Assert.AreEqual(2, originals.Length);
                Assert.True(s.PostJobAd());
                Assert.AreEqual(2, s.Candidates.Count, "重贴告示不能删除原候选人");
                s.Clock.Restore(new GameDate(2), 8 * 60 + 59); s.AdvanceClock(2);
                Assert.AreEqual(4, s.Candidates.Count);
                Assert.True(originals.All(id => s.Candidates.Any(x => x.Id == id)));
            }
        }
    }
}
