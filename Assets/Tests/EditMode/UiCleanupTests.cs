using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class UiCleanupTests
    {
        [Test] public void HudCanBeConstructedBeforeSessionStarts()
        {
            var root = new GameObject("HudStartupTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            var previous = UnityEngine.EventSystems.EventSystem.current;
            GameUi ui = null;
            try
            {
                Assert.IsNull(controller.Session);
                ui = new GameUi(controller);
                var brand = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>(true))
                    .Single(b => b.name == "HudBrand");
                Assert.True(brand.gameObject.activeSelf);
            }
            finally
            {
                if (ui != null)
                    foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
            }
        }

        private sealed class Rig : IDisposable
        {
            public readonly GameSession S;
            public readonly GameUi Ui;
            private readonly GameObject _root;
            private readonly UnityEngine.EventSystems.EventSystem _previous;
            public Rig(GameSession session = null)
            {
                S = session ?? GameSessionTests.Make(5000000, 600);
                S.EventsEnabled = false; S.Clock.Restore(new GameDate(15), 600); S.PlayerShop.Reputation = 100;
                GameSessionTests.Teleport(S, S.PlayerShopPlace.Id);
                _root = new GameObject("UiCleanupTest"); _root.SetActive(false);
                var controller = _root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller, S);
                typeof(GameController).GetProperty("City").SetValue(controller, S.City);
                _previous = UnityEngine.EventSystems.EventSystem.current;
                Ui = new GameUi(controller);
            }
            public T[] Components<T>() where T : Component => Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<T>()).ToArray();
            public string Text => string.Join("\n", Components<Text>().Select(t => t.text));
            public Button Button(string name) => Components<Button>().Single(b => b.name == name);
            public Transform Workspace(string group) => Components<Transform>().Single(t => t.name == "Workspace_" + group);
            public void ClickCaption(string caption) => Components<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == caption).onClick.Invoke();
            public object Call(string method, params object[] args) => typeof(GameUi).GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Ui, args);
            public void RentSecond()
            {
                S.DebugAddListing("tea", 0, 60, Money.FromYuan(1000)); GameSessionTests.Teleport(S, "agency");
                Assert.True(S.SignLease("tea", true)); GameSessionTests.Teleport(S, "tea");
            }
            public void Dispose()
            {
                Ui.CloseAllWindows(); foreach (var canvas in Ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                UnityEngine.Object.DestroyImmediate(_root);
                if (_previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                S.Dispose();
            }
        }

        [Test]
        public void BusinessPagesReuseOneWindowAndReleaseTheirSinglePauseHold()
        {
            using (var r = new Rig())
            {
                Assert.False(r.S.Time.IsHeld);
                r.Ui.OpenBusinessPanel(); var window = r.Workspace("business");
                r.Ui.OpenCityProcurement(); Assert.AreSame(window, r.Workspace("business"));
                r.Ui.OpenCityProperties(); Assert.AreSame(window, r.Workspace("business"));
                r.Ui.OpenCityInvestments(); Assert.AreSame(window, r.Workspace("business"));
                Assert.AreEqual(1, r.Components<Transform>().Count(t => t.name == "Workspace_business"));
                Assert.True(r.S.Time.IsHeld);
                Assert.True(r.Ui.CloseTopWindow()); Assert.False(r.S.Time.IsHeld);
            }
        }

        [Test]
        public void BrandWindowIsIndependentFromBusinessNavigation()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenBrandBusiness();
                var brand = r.Workspace("brands");
                Assert.False(brand.GetComponentsInChildren<Button>(true).Any(b => b.name.StartsWith("BusinessNav_")));
                Assert.IsEmpty(r.Components<InputField>());
                StringAssert.DoesNotContain("创建电脑店品牌", r.Text);
                r.ClickCaption("＋ 创建品牌");
                Assert.IsEmpty(r.Components<InputField>());
                StringAssert.Contains("创建电脑店品牌", r.Text);
                Assert.False(r.Button("BrandCreate_computer").interactable);
                r.ClickCaption("收起行业资格");
                Assert.IsEmpty(r.Components<InputField>());
                r.Ui.OpenBrandBusiness();
                Assert.AreSame(brand, r.Workspace("brands"));
                Assert.True(r.Ui.CloseTopWindow());
                Assert.False(r.S.Time.IsHeld);
                r.Ui.OpenBusinessPanel();
                Assert.False(r.Components<Button>().Any(b => b.name == "BusinessNav_brands"));
                Assert.True(r.Ui.CloseTopWindow());
                Assert.False(r.S.Time.IsHeld);
            }
        }

        [Test]
        public void BrandOverviewExpandsStoresAndOpensTheAmbassadorMarket()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var session = GameSession.CreateCityLifePreset(city, "brand_ready_two");
            Assert.True(session.FoundBrand("computer", "卫星", "value"));
            Assert.True(session.FoundBrand("clothing", "衣坊", "quality"));
            using (var r = new Rig(session))
            {
                r.Ui.OpenBrandBusiness();
                StringAssert.Contains("近7天营收", r.Text);
                StringAssert.Contains("近7天支出", r.Text);
                StringAssert.Contains("知名度", r.Text);
                r.Button("BrandRename_brand:computer").onClick.Invoke();
                StringAssert.Contains("50 → 25", r.Text);
                r.ClickCaption("取消");
                Assert.AreEqual("卫星", session.BrandBusiness.Brands.First().Name);
                r.Button("BrandRename_brand:computer").onClick.Invoke();
                r.Components<InputField>().Single().text = "新卫星";
                r.ClickCaption("确认改名");
                StringAssert.Contains("新卫星 · 电脑店", r.Text);
                Assert.AreEqual(25, session.BrandBusiness.Brands.First().Reputation);
                Assert.False(r.Text.Contains("调整门店归属"));
                r.Button("BrandExpand_brand:computer").onClick.Invoke();
                StringAssert.Contains("调整门店归属", r.Text);
                r.Call("OpenBrandShopMembership", "brand:computer");
                r.ClickCaption("返回品牌总览");
                r.Components<Button>().First(b => b.GetComponentInChildren<Text>()?.text == "查看品牌代言人").onClick.Invoke();
                StringAssert.DoesNotContain("短期推广", r.Text);
                r.ClickCaption("前往代言人市场");
                StringAssert.Contains("衣见文化", r.Text);
                Assert.True(r.Components<RawImage>().Any(p => p.name == "KolPortrait_momo" && p.texture != null));
                Assert.True(r.Components<RawImage>().Any(p => p.name == "KolPortrait_ake" && p.texture != null));
            }
        }

        [Test]
        public void BrandCreationAndMcnContractFollowSeparateDialogs()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using (var r = new Rig(GameSession.CreateCityLifePreset(city, "brand_ready_two")))
            {
                r.Ui.OpenBrandBusiness();
                r.ClickCaption("＋ 创建品牌");
                Assert.IsEmpty(r.Components<InputField>());
                r.Button("BrandCreate_computer").onClick.Invoke();
                r.Components<InputField>().Single().text = "星桥电脑";
                r.ClickCaption("实惠定位");
                Assert.AreEqual("星桥电脑", r.Components<InputField>().Single().text);
                r.Button("BrandConfirmCreation").onClick.Invoke();
                Assert.AreEqual("value", r.S.BrandBusiness.Brands.Single().Position);
                Assert.True(r.S.FoundBrand("clothing", "星桥服装", "value"));
                r.Ui.CloseAllWindows();

                typeof(GameSession).GetProperty("CurrentPlace").SetValue(r.S, city.GetPlace(GameSession.CommunityMcnId));
                r.S.Flags.Remove(GameSession.MetFlag("momo"));
                var person = r.S.CityPersonState("momo");
                person.Route = null;
                person.LocationPlaceId = person.HomePlaceId;
                r.Ui.OpenCommunityMcn();
                StringAssert.DoesNotContain("星桥电脑", r.Text);
                StringAssert.DoesNotContain("人事洽谈", r.Text);
                r.Button("McnIntroduce_momo").onClick.Invoke();
                r.ClickCaption("确定办理");
                r.Button("McnContact_momo").onClick.Invoke();
                r.ClickCaption("谈品牌代言与直播");
                StringAssert.Contains("合约期限", r.Text);
                StringAssert.Contains("直播频率", r.Text);
                StringAssert.DoesNotContain("表达心意", r.Text);
                r.ClickCaption("60天");
                r.ClickCaption("隔天");
                r.Button("KolConfirmSign_momo").onClick.Invoke();
                var contract = r.S.ActiveKolAmbassador("momo");
                Assert.IsNull(contract.BrandId);
                Assert.AreEqual(2, contract.FrequencyDays);
                r.Ui.CloseAllWindows();
                r.Ui.OpenBrandBusiness();
                r.ClickCaption("代言人（KOL）");
                StringAssert.Contains("我的代言人", r.Text);
                StringAssert.DoesNotContain("星桥电脑  ·  知名度", r.Text);
                StringAssert.Contains("已一次付清", r.Text);
                StringAssert.Contains("每场直播费", r.Text);
                var brands = r.Components<Dropdown>().Single(d => d.name == "KolBrandChoice_" + contract.Id);
                CollectionAssert.AreEqual(new[] { "未分配", "星桥电脑", "星桥服装" }, brands.options.Select(o => o.text));
                brands.value = 1;
                r.Ui.RebuildWindows();
                var save = r.Button("KolSaveSchedule_" + contract.Id);
                Assert.True(save.interactable);
                save.onClick.Invoke();
                var configured = r.S.ActiveKolAmbassador("momo");
                Assert.AreEqual("brand:computer", configured.BrandId);
                Assert.AreEqual(2, configured.FrequencyDays);
                Assert.True(configured.Enabled);

                var switchBrand = r.Components<Dropdown>().Single(d => d.name == "KolBrandChoice_" + contract.Id);
                switchBrand.value = 2;
                r.Ui.RebuildWindows();
                r.Button("KolSaveSchedule_" + contract.Id).onClick.Invoke();
                Assert.AreEqual(0, r.S.BrandKolCount("brand:computer"));
                Assert.AreEqual(1, r.S.BrandKolCount("brand:clothing"));
                using (var loaded = GameSession.Load(city, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(r.S.CreateSave()))))
                {
                    Assert.AreEqual(1, loaded.KolStreaming.Contracts.Length);
                    Assert.AreEqual("brand:clothing", loaded.BrandBusiness.Deals.Single(d => !d.Ended).BrandId);
                    Assert.AreEqual(configured.FeeCents, loaded.ActiveKolAmbassador("momo").FeeCents);
                    Assert.AreEqual(configured.SessionFeeCents, loaded.ActiveKolAmbassador("momo").SessionFeeCents);
                }
            }
        }

        [Test]
        public void ProcurementUsesCurrentBranchAndBackKeepsSearchAndDestination()
        {
            using (var r = new Rig())
            {
                r.RentSecond(); r.Ui.OpenCityProcurement();
                string title = r.S.ShopTitle(r.S.CurrentShop);
                StringAssert.Contains(title, r.Components<Text>().Single(t => t.name == "ProcurementDestination").text);
                var input = r.Components<InputField>().Single(); input.onEndEdit.Invoke("显卡"); r.Ui.RebuildWindows();
                r.Ui.OpenCityProperties(); r.Button("WorkspaceBack").onClick.Invoke();
                Assert.AreEqual("显卡", r.Components<InputField>().Single().text);
                StringAssert.Contains(title, r.Components<Text>().Single(t => t.name == "ProcurementDestination").text);
                r.Ui.OpenCityProcurementFor(r.S.MainShop);
                StringAssert.Contains(r.S.ShopTitle(r.S.MainShop), r.Components<Text>().Single(t => t.name == "ProcurementDestination").text);
            }
        }

        [Test]
        public void OperatingTrendFiltersShopAndExcludesPersonalFinancingAndInternalTransfers()
        {
            var ledger = new Ledger(Money.FromYuan(5000)); var day = new GameDate(0);
            ledger.Credit(day, "零售", Money.FromYuan(120), attribution: LedgerAttribution.ForShop("a"));
            ledger.Credit(day, "零售", Money.FromYuan(900), attribution: LedgerAttribution.ForShop("b"));
            ledger.TryDebit(day, "进货", Money.FromYuan(40), attribution: LedgerAttribution.ForShop("a"));
            ledger.TryDebit(day, "生活", Money.FromYuan(300), attribution: new LedgerAttribution(LedgerBusinessKind.Personal));
            ledger.Credit(day, "贷款", Money.FromYuan(10000), attribution: new LedgerAttribution(LedgerBusinessKind.Financing));
            ledger.Credit(day, "转回", Money.FromYuan(700), attribution: new LedgerAttribution(LedgerBusinessKind.InternalTransfer));
            var one = FinanceReport.OperatingDaily(ledger, day, 14, "a").Single();
            Assert.AreEqual(120, one.Revenue.Yuan); Assert.AreEqual(40, one.Expense.Yuan); Assert.AreEqual(80, one.CashChange.Yuan);
            var all = FinanceReport.OperatingDaily(ledger, day, 14).Single();
            Assert.AreEqual(1020, all.Revenue.Yuan); Assert.AreEqual(980, all.CashChange.Yuan);
            Assert.AreEqual(6, ledger.Entries.Count, "读取趋势不写账");
        }

        [Test]
        public void StoreTrendNamesSelectedBranchAndKeepsOnlyItsOperatingSales()
        {
            using (var r = new Rig())
            {
                r.RentSecond(); var shop = r.S.CurrentShop;
                r.S.Ledger.Credit(r.S.Clock.Today, "零售", Money.FromYuan(123), attribution: LedgerAttribution.ForShop(shop.Place.Id));
                r.Ui.OpenReportWindow();
                StringAssert.Contains("经营趋势 · " + r.S.ShopTitle(shop), r.Text);
                StringAssert.Contains("123", r.Text);
                StringAssert.DoesNotContain("含退押金", r.Text);
            }
        }

        [TestCase("restaurant")] [TestCase("secondhand")]
        public void SpecialStoreCounterPricingAndRecordsHaveDistinctControls(string kind)
        {
            using (var r = new Rig())
            {
                Assert.True(r.S.SetShopMainCategory(kind)); r.Ui.OpenDock(); r.Ui.DebugSelectTab(0);
                if (kind == "restaurant") Assert.True(r.Components<Button>().Any(b => b.name == "ChillKitchenIngredients"));
                else StringAssert.Contains("每日收购预算", r.Text);
                r.Ui.DebugSelectTab(2);
                Assert.False(r.Components<Button>().Any(b => b.name == "ChillKitchenIngredients"));
                StringAssert.DoesNotContain("每日收购预算", r.Text);
                if (kind == "restaurant") Assert.NotNull(r.Button("SaveMenuMarkup"));
                r.Ui.DebugSelectTab(3);
                Assert.False(r.Components<Button>().Any(b => b.name == "SaveMenuMarkup" || b.name == "ChillKitchenIngredients"));
                StringAssert.DoesNotContain("每日收购预算", r.Text);
                StringAssert.Contains(kind == "restaurant" ? "当前桌单" : "收购与售出记录", r.Text);
            }
        }

        [Test]
        public void GlobalPriceSliderOnlyCommitsOnExplicitConfirmation()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenDock(); r.Ui.DebugSelectTab(2); double original = r.S.PlayerShop.Markup;
                r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider").value = 1.75f;
                Assert.AreEqual(original, r.S.PlayerShop.Markup);
                r.Button("Save_GlobalMarkupSlider").onClick.Invoke(); Assert.AreEqual(1.75, r.S.PlayerShop.Markup, 1e-9);
            }
        }

        [Test]
        public void MenuPriceSliderOnlyCommitsOnExplicitConfirmation()
        {
            using (var r = new Rig())
            {
                Assert.True(r.S.SetShopMainCategory("restaurant")); var state = r.S.SpecialBusiness("pc");
                r.Ui.OpenDock(); r.Ui.DebugSelectTab(2); double original = state.MenuMarkup;
                r.Components<Slider>().Single(s => s.name == "MenuMarkupSlider").value = 1.7f;
                Assert.AreEqual(original, state.MenuMarkup);
                r.Button("SaveMenuMarkup").onClick.Invoke(); Assert.AreEqual(1.7, state.MenuMarkup, 1e-9);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CalendarAndActivityLinksShareOneWindowAndBothRemainReachable(bool smartphone)
        {
            using (var r = new Rig())
            {
                r.S.SetSmartphone(smartphone);
                r.Ui.OpenSchedulePanel(); var window = r.Workspace("calendar");
                Assert.NotNull(r.Button("CalendarActivities")); r.Button("CalendarActivities").onClick.Invoke();
                Assert.AreSame(window, r.Workspace("calendar")); r.Button("CalendarPersonal").onClick.Invoke();
                Assert.AreSame(window, r.Workspace("calendar")); StringAssert.Contains(r.S.HasCalendar ? "我的安排 · 到期前提醒" : "笔记本", r.Text);
                r.Ui.CloseAllWindows(); Assert.False(r.S.Time.IsHeld);
            }
        }

        [Test]
        public void MarketAndPeopleNavigationNeverStackDuplicateRootWindows()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenMarketPanel(); var market = r.Workspace("market");
                r.Button("MarketTab_1").onClick.Invoke(); r.Button("MarketTab_2").onClick.Invoke(); r.Button("MarketTab_3").onClick.Invoke();
                Assert.AreSame(market, r.Workspace("market"));
                StringAssert.DoesNotContain("市场变化与原创模型", r.Text);
                r.Ui.CloseAllWindows(); r.Ui.OpenContactsPanel(); var people = r.Workspace("people");
                r.Button("PeopleTab_relations").onClick.Invoke(); r.Button("PeopleTab_people").onClick.Invoke();
                Assert.AreSame(people, r.Workspace("people"));
                r.Ui.CloseAllWindows(); Assert.False(r.S.Time.IsHeld);
            }
        }

        [Test]
        public void ClosingLocationMenuStaysInside_LeavingIsAnExplicitAction()
        {
            using (var r = new Rig())
            {
                GameSessionTests.Teleport(r.S, "agency"); var place = r.S.CurrentPlace;
                void Open() => r.Call("OpenPlaceWindow", place, new Action<Window, Place>((w, p) => { }), 640f, 520f, true, true);
                Open(); Assert.True(r.Ui.CloseTopWindow()); Assert.AreSame(place, r.S.CurrentPlace);
                Open(); r.Button("LeavePlace").onClick.Invoke(); Assert.IsNull(r.S.CurrentPlace); Assert.False(r.S.Time.IsHeld);
            }
        }

        [Test]
        public void HomeWorkbenchButtonExplainsMissingEquipmentAndLocation()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenMyPanel(); StringAssert.Contains("需购买家庭经营大屏", r.Button("MyHomeWorkbench").GetComponentInChildren<Text>().text);
                r.S.HomeBox.AddFurniture(GameSession.HomeWorkbenchId, 1); r.Ui.RebuildWindows();
                StringAssert.Contains("回家后可用", r.Button("MyHomeWorkbench").GetComponentInChildren<Text>().text);
                GameSessionTests.Teleport(r.S, r.S.Home.Id); r.Ui.RebuildWindows(); Assert.True(r.Button("MyHomeWorkbench").interactable);
            }
        }

        [Test]
        public void PersonalPageStartsWithEssentialStatsAndMovesInventoryIntoItsOwnTab()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenMyPanel(); StringAssert.Contains("饱腹", r.Text);
                StringAssert.DoesNotContain("未摆放的家具", r.Text); StringAssert.DoesNotContain("没有冰箱，放不久", r.Text);
                r.Button("MyTab_3").onClick.Invoke(); StringAssert.Contains("在途包裹", r.Text);
            }
        }

        [Test]
        public void GoodsIntakeFeedbackSeparatesDisplayedQuantityFromWarehouseRemainder()
        {
            using (var r = new Rig())
            {
                r.S.PlayerShop.Stock.Clear(); r.S.CurrentShop.Layout.Clear();
                r.S.CurrentShop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.Shelf });
                Assert.True(r.S.SetShelfKind(0, "smoke")); r.S.Backpack.AddGift(Gifts.Cig, 25);
                r.Call("StoreRetailGoods", Gifts.Cig, 25);
                Assert.AreEqual(25, r.S.PlayerShop.Stock.GoodsCount(Gifts.Cig)); Assert.AreEqual(20, r.S.PlayerShop.OnShelfGoods(Gifts.Cig));
                StringAssert.Contains("本品陈列 20 件 · 后仓 5 件", r.Text);
            }
        }

        [Test]
        public void ParkShortcutOpensParkSectionInsteadOfFinanceOverview()
        {
            using (var r = new Rig())
            {
                r.Call("OpenBusinessSection", 6, null);
                Assert.NotNull(r.Button("BusinessSection_6"));
                Assert.False(r.Workspace("business").GetComponentsInChildren<Button>().Any(b => b.name == "ProcurementDestination"));
                StringAssert.DoesNotContain("统计日期：", r.Text);
            }
        }

        [Test]
        public void PropertyShortcutFiltersTheActualSelectedUnit()
        {
            using (var r = new Rig())
            {
                r.RentSecond(); r.Ui.OpenCityProperty(r.S.CurrentShop.Place);
                var cards = r.Workspace("business").GetComponentsInChildren<RectTransform>().Where(t => t.name == "BusinessCard").ToArray();
                Assert.AreEqual(1, cards.Length);
                StringAssert.Contains(r.S.CityPropertyAddress(r.S.CurrentShop.Place), string.Join("\n", cards[0].GetComponentsInChildren<Text>().Select(t => t.text)));
                r.ClickCaption("查看全部物业");
                Assert.Greater(r.Workspace("business").GetComponentsInChildren<RectTransform>().Count(t => t.name == "BusinessCard"), 1);
            }
        }

        [Test]
        public void PropertyAgencyUsesTheResponsibleDistrictInsteadOfFirstAgency()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var session = new GameSession(city, 7, Money.FromYuan(5000000), GameTimestamp.From(new GameDate(15), 600), startWithShop: true);
            using (var r = new Rig(session))
            {
                var first = city.FirstOfKind(PlaceKind.Agency);
                var property = city.OfKind(PlaceKind.Shop).First(p => r.S.AgencyFor(p) != first);
                var destination = (Place)r.Call("PreferredAgency", property);
                Assert.AreNotEqual(first.Id, destination.Id);
                Assert.True(r.S.AgencyServes(destination, property));
            }
        }

        [Test]
        public void PriceDraftSurvivesAViewRebuildWithoutChangingTheActualPrice()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenDock(); r.Ui.DebugSelectTab(2); double original = r.S.PlayerShop.Markup;
                r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider").value = 2.2f;
                r.Call("RebuildDock");
                Assert.AreEqual(original, r.S.PlayerShop.Markup);
                Assert.AreEqual(2.2, r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider").value, 1e-5);
                r.Button("Save_GlobalMarkupSlider").onClick.Invoke(); Assert.AreEqual(2.2, r.S.PlayerShop.Markup, 1e-9);
            }
        }

        [Test]
        public void UnifiedProcurementKeepsBudgetControlsAndOnlySavesOnConfirmation()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenCityProcurement(); r.Button("ProcurementBudget").onClick.Invoke();
                long old = r.S.RestockDailyBudgetYuan;
                r.Button("QuantityPlus_dailyBudget").onClick.Invoke(); r.Ui.RebuildWindows();
                Assert.AreEqual(old, r.S.RestockDailyBudgetYuan);
                r.Button("SaveProcurementBudget").onClick.Invoke(); Assert.AreEqual(old + 100, r.S.RestockDailyBudgetYuan);
            }
        }

        [Test]
        public void HouseMoveShowsFullCostAndDoesNotChargeBeforeConfirmation()
        {
            using (var r = new Rig())
            {
                GameSessionTests.Teleport(r.S, "agency"); r.Call("OpenCityResidence");
                Money before = r.S.Ledger.Balance; r.ClickCaption("改善住宅…");
                Assert.AreEqual(before, r.S.Ledger.Balance); Assert.AreEqual(0, r.S.HomeTier);
                StringAssert.Contains("搬家费 ¥500", r.Text);
                r.ClickCaption("确认搬家"); Assert.AreEqual(1, r.S.HomeTier);
                Assert.AreEqual(r.S.HomeTierRentQuote(1) + Money.FromYuan(500), before - r.S.Ledger.Balance);
            }
        }

        [Test]
        public void UnsavedPriceFromOneBranchCannotLeakIntoAnotherBranch()
        {
            using (var r = new Rig())
            {
                r.Ui.OpenDock(); r.Ui.DebugSelectTab(2);
                r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider").value = 2.5f;
                r.RentSecond(); r.Ui.OpenDock(); r.Ui.DebugSelectTab(2);
                Assert.AreEqual(r.S.PlayerShop.Markup, r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider").value, 1e-5);
                Assert.False(r.Button("Save_GlobalMarkupSlider").interactable);
            }
        }
    }
}
