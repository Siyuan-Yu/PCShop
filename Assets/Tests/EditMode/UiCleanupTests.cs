using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
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
        public void PhoneAppsUseTheSameCenteredFrameFromDesktopAndStore()
        {
            using var rig = new Rig();
            rig.S.SetSmartphone(true);

            void CheckAppFrame(string buttonName)
            {
                rig.Button(buttonName).onClick.Invoke();
                var panel = rig.Components<RectTransform>().Last(rect => rect.name == "Panel"
                    && (rect.parent.name.StartsWith("Window_") || rect.parent.name.StartsWith("Workspace_")));
                Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(1500, 880)), buttonName);
                Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero), buttonName);
                Assert.That(rig.S.Time.IsHeld, Is.EqualTo(buttonName != "PhoneApp_live" && buttonName != "PhoneDownload_live"), buttonName);
            }

            foreach (var app in PhoneApps.All)
            {
                rig.Ui.OpenPhone();
                CheckAppFrame("PhoneApp_" + app.Id);
                rig.Ui.CloseAllWindows();
                if (app.Id == PhoneApps.Store) continue;
                rig.Ui.OpenPhoneStore();
                CheckAppFrame("PhoneDownload_" + app.Id);
                rig.Ui.CloseAllWindows();
            }
        }

        [Test]
        public void RacingAndNoticeHudShortcutsOpenTheirPanels()
        {
            using (var r = new Rig())
            {
                if (GameSession.FirstChapterTrial)
                    Assert.False(r.Ui.TryHotkey(KeyCode.R));
                else
                {
                    var racing = r.Button("HudRacing");
                    Assert.AreEqual("赛车 R", racing.GetComponentInChildren<Text>().text);
                    Assert.NotNull(racing.transform.Find("NavigationIcon").GetComponent<Image>().sprite);
                    Assert.True(r.Ui.TryHotkey(KeyCode.R));
                    Assert.True(r.Components<Transform>().Any(t => t.name == "Window_湖区赛车"));
                    Assert.False(r.Ui.TryHotkey(KeyCode.N));
                    Assert.True(r.Ui.CloseTopWindow());
                }
                Assert.True(r.Ui.TryHotkey(KeyCode.N));
                Assert.True(r.Components<Transform>().Any(t => t.name == "Window_通知记录（本次会话）"));
            }
        }

        [Test]
        public void FurnitureFilterRoutesSharedCounterToMatchingShopAtCheckout()
        {
            using (var r = new Rig(HomeTests.Make(200000, 10 * 60)))
            {
                r.S.MainShop.RetailKind = PetShopCatalog.ShopKind;
                r.S.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(r.S, "agency");
                Assert.True(r.S.SignLease("tea", true, "secondhand"));
                var branch = r.S.ShopAt("tea");

                GameSessionTests.Teleport(r.S, "mall");
                r.S.SetCourierDest(r.S.MainShop);
                Assert.True(r.S.CartAddFurniture(FurnitureCatalog.Shelf));
                Assert.True(r.S.Checkout(false));
                GameSessionTests.Teleport(r.S, "mall");
                Assert.AreSame(r.S.MainShop, r.S.LastPurchaseDeliveryShop);

                r.Ui.OnEnteredPlace(r.S.CurrentPlace);
                r.Button("ShopTab_二手旧物店").onClick.Invoke();
                r.Button("QuantityPlus_furn:" + FurnitureCatalog.ShopCounter).onClick.Invoke();
                r.Ui.RebuildWindows();
                StringAssert.Contains(r.S.ShopTitle(branch) + "  ▼", r.Text);

                r.Button("ShopTab_全部").onClick.Invoke();
                StringAssert.Contains(r.S.ShopTitle(branch) + "  ▼", r.Text,
                    "Browsing another tab should not reroute the counter already in the cart.");
                r.Button("CartCheckout").onClick.Invoke();
                Assert.AreEqual(branch.Place.Id, r.S.Couriers.Last().ShopId);
            }
        }

        [Test]
        public void TruckItemFilterUsesTheSameTypeForBackpackAndStockAndKeepsTheLoad()
        {
            using (var r = new Rig())
            {
                const string highFood = "pet:high:catfood";
                string carried = "bag|gift:" + PetShopCatalog.CatFood;
                string stocked = "shop|goods:" + PetShopCatalog.CatFood;
                string premium = "shop|goods:" + highFood;
                r.S.Backpack.AddGift(PetShopCatalog.CatFood, 1);
                r.S.PlayerShop.Stock.ForceAddGoods(PetShopCatalog.CatFood, 2);
                r.S.PlayerShop.Stock.ForceAddGoods(highFood, 1);
                r.Ui.OpenTruckWindow();

                Dropdown Filter() => r.Components<Dropdown>().Single(dropdown => dropdown.name == "TruckItemCategory");
                Transform Item(string key) => r.Components<Transform>().SingleOrDefault(t => t.name == "TruckLoad_" + key);
                void Choose(string label)
                {
                    var filter = Filter();
                    int index = filter.options.FindIndex(option => option.text == label);
                    Assert.GreaterOrEqual(index, 0);
                    filter.value = index;
                }

                Assert.NotNull(Item(premium));
                Choose("基础宠物用品");
                Assert.NotNull(Item(carried));
                Assert.NotNull(Item(stocked));
                Assert.IsNull(Item(premium));
                Item(carried).GetComponentInChildren<Button>().onClick.Invoke();
                r.Ui.RebuildWindows();
                Assert.AreEqual("基础宠物用品", Filter().captionText.text);
                Assert.IsNull(Item(carried), "The only carried unit is already on the loading list.");

                Choose("高档宠物用品");
                Assert.NotNull(Item(premium));
                Assert.IsNull(Item(stocked));
                Assert.AreEqual(1, r.S.TruckAmount(carried));
                Assert.True(r.Components<Transform>().Any(t => t.name.StartsWith("CartLine_" + carried + "_to_")));
                Choose("全部物品");
                Assert.NotNull(Item(stocked));
                Assert.NotNull(Item(premium));
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
        public void QuickStockTradeShowsFailuresAndConfirmsBeforeUsingReserve()
        {
            using (var r = new Rig())
            {
                r.S.InstallApp(PhoneApps.Securities);
                r.S.RestockReserveYuan = r.S.Ledger.Balance.Cents / 100;
                r.Ui.OpenCityInvestments();
                var cash = r.S.Ledger.Balance;

                r.Button("StockQuickSell_city_chip").onClick.Invoke();
                var feedback = r.Components<Text>().Single(t => t.name == "StockTradeFeedback");
                StringAssert.Contains("卖出数量超过实际持股", feedback.text);
                Assert.AreEqual(cash, r.S.Ledger.Balance);

                r.Button("StockQuickBuy_city_chip").onClick.Invoke();
                StringAssert.Contains("买入会动用预留资金", r.Text);
                Assert.AreEqual(cash, r.S.Ledger.Balance);
                Assert.Zero(r.S.CityInvestmentUnits("city_chip"));

                r.Button("StockReserveConfirm").onClick.Invoke();
                Assert.AreEqual(GameSession.StockLotSize, r.S.CityInvestmentUnits("city_chip"));
                StringAssert.Contains("已完成", r.Components<Text>().Single(t => t.name == "StockTradeFeedback").text);
            }
        }

        [Test]
        public void QuickStockBulkOrdersUseAllAffordableLotsAndLiquidateThePosition()
        {
            using var r = new Rig();
            var s = r.S;
            const string id = "city_chip";
            s.SetSmartphone(true);
            var cost = s.CityInvestmentTradeTotal(id, 200, buy: true);
            var cash = s.CityInvestmentTradeTotal(id, 300, buy: true) - Money.FromCents(1);
            s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - cash);
            s.RestockReserveYuan = cash.Cents / 100;
            r.Ui.OpenCityInvestments();

            Assert.That(r.Components<InputField>(), Has.Length.EqualTo(1));
            Assert.That(r.Components<Transform>().Any(item => item.name == "StockQuote_hardware"), Is.False);
            Assert.That(r.Button("StockQuickSellAll_" + id).interactable, Is.False);
            r.Button("StockQuickBuyMax_" + id).onClick.Invoke();
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash));
            r.Button("StockReserveConfirm").onClick.Invoke();
            Assert.That(s.CityInvestmentUnits(id), Is.EqualTo(200));
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash - cost));
            Assert.That(r.Button("StockQuickBuyMax_" + id).interactable, Is.False);

            var proceeds = s.CityInvestmentTradeTotal(id, 200, buy: false);
            r.Button("StockQuickSellAll_" + id).onClick.Invoke();
            Assert.That(s.CityInvestmentUnits(id), Is.Zero);
            Assert.That(s.CityInvestmentCost(id), Is.EqualTo(Money.Zero));
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash - cost + proceeds));
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
        public void AmbassadorActionsAppearOnlyAfterUnlockingTheCommunity()
        {
            if (GameSession.FirstChapterTrial) Assert.Ignore("需要验证社区解锁前后。");
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var session = GameSession.CreateCityLifePreset(city, "brand_ready_two");
            Assert.True(session.FoundBrand("computer", "测试品牌", "value"));
            using var r = new Rig(session);
            const string singer = "life_singer";
            session.Meet(singer, 21);
            var district = new DistrictProgress();
            typeof(GameSession).GetField("_district", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, district);
            bool HasButton(string caption) => r.Components<Button>().Any(b => b.GetComponentInChildren<Text>()?.text == caption);

            r.Ui.OpenKolManagement();
            Assert.That(r.Ui.AnyModalOpen, Is.False);
            r.Ui.OpenBrandBusiness();
            Assert.That(HasButton("代言人（KOL）"), Is.False);
            Assert.That(HasButton("查看品牌代言人"), Is.False);
            r.Ui.CloseAllWindows();
            r.Ui.OpenPersonDialogue(singer, remote: true);
            Assert.That(r.Ui.AnyModalOpen, Is.True);
            StringAssert.Contains(">KOL<", r.Text);
            Assert.That(HasButton("聊聊"), Is.True);
            Assert.That(HasButton("谈品牌代言与直播"), Is.False);
            var cash = session.Ledger.Balance;
            Assert.That(session.KolSigningBlocker(singer, 90, 1, remote: true), Does.Contain("解锁社区片区"));
            Assert.That(session.SignKolAmbassador(singer, 90, 1, remote: true), Is.False);
            Assert.That(session.Ledger.Balance, Is.EqualTo(cash));
            Assert.That(session.ActiveKolAmbassador(singer), Is.Null);

            district.UnlockedTier = 1;
            r.Ui.RebuildWindows();
            r.ClickCaption("谈品牌代言与直播");
            var confirm = r.Button("KolConfirmSign_" + singer);
            Assert.That(confirm.interactable, Is.True);
            confirm.onClick.Invoke();
            Assert.That(session.ActiveKolAmbassador(singer), Is.Not.Null);
            r.Ui.CloseAllWindows();
            r.Ui.OpenBrandBusiness();
            Assert.That(HasButton("代言人（KOL）"), Is.True);
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
                r.RentSecond(); r.Ui.OpenCityProcurementFor(r.S.CurrentShop);
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
                if (kind == "restaurant") Assert.False(r.Components<Button>().Any(b => b.name == "ChillKitchenIngredients"));
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
        public void UsedDelegationIsAlwaysVisibleAndCanBeEditedOnTheCounter()
        {
            using var r = new Rig();
            Assert.True(r.S.SetShopMainCategory("secondhand"));
            var state = r.S.SpecialBusiness(r.S.CurrentShop.Place.Id);
            r.Ui.OpenDock();
            r.Ui.DebugSelectTab(0);

            var budget = r.Components<InputField>().Single(input => input.name == "UsedDailyBudgetInput");
            var offer = r.Components<InputField>().Single(input => input.name == "UsedMaxOfferInput");
            Assert.AreSame(budget.transform.parent.parent.parent, offer.transform.parent.parent.parent);
            Assert.False(r.Ui.AnyModalOpen);
            Assert.False(r.Button("UsedDelegationToggle").interactable);
            Assert.False(r.Components<Button>().Any(button => button.GetComponentInChildren<Text>()?.text == "代管设置"));
            budget.text = "1200";
            budget.onEndEdit.Invoke(budget.text);
            offer.text = "350";
            offer.onEndEdit.Invoke(offer.text);
            r.Button("UsedRisk_积极").onClick.Invoke();

            Assert.AreEqual(1200, state.DailyBudget);
            Assert.AreEqual(350, state.MaxOffer);
            Assert.AreEqual(.8, state.Risk);
            Assert.AreEqual(Ui.ButtonHot, r.Button("UsedRisk_积极").image.color);
            Assert.False(r.Ui.AnyModalOpen);
            Assert.False(r.S.Time.IsHeld);
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
        public void PricingKeepsItsButtonsDuringStockUpdatesAndPriceConfirmation()
        {
            using var r = new Rig();
            var shop = r.S.PlayerShop;
            var part = PartCatalog.Get("cpu0");
            var other = PartCatalog.Get("cpu1");
            shop.Stock.Clear();
            shop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double> { { "cpu", 60 } });
            shop.Stock.ForceAdd(part, true, 2);
            r.Ui.OpenDock();
            r.Ui.DebugSelectTab(2);
            var slider = r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider");
            var save = r.Button("Save_GlobalMarkupSlider");
            var edit = r.Button("EditPrice_cpu0_boxed");
            double original = shop.Markup;
            slider.value = 1.75f;

            shop.Stock.ForceAddGoods(Gifts.Soda, 1);
            r.Call("RefreshDock");
            Assert.That(r.Button("Save_GlobalMarkupSlider"), Is.SameAs(save));
            Assert.That(r.Button("EditPrice_cpu0_boxed"), Is.SameAs(edit));

            Assert.That(shop.Stock.TryRemove(part, true, 1), Is.True);
            shop.Stock.ForceAdd(other, true, 1);
            r.Call("RefreshDock");
            Assert.That(r.Components<Slider>().Single(s => s.name == "GlobalMarkupSlider"), Is.SameAs(slider));
            Assert.That(r.Button("EditPrice_cpu0_boxed"), Is.SameAs(edit));
            Assert.That(r.Button("EditPrice_cpu1_boxed"), Is.Not.Null);
            Assert.That(edit.transform.parent.GetComponentsInChildren<Text>().Any(label => label.text.Contains("已陈列 1 件")), Is.True);
            Assert.That(slider.value, Is.EqualTo(1.75f));
            Assert.That(shop.Markup, Is.EqualTo(original));
            Assert.That(save.interactable, Is.True);

            save.onClick.Invoke();
            Assert.That(shop.Markup, Is.EqualTo(1.75).Within(1e-9));
            Assert.That(r.Button("Save_GlobalMarkupSlider"), Is.SameAs(save));
            Assert.That(save.interactable, Is.False);
            Assert.That(shop.Stock.TryRemove(other, true, 1), Is.True);
            r.Call("RefreshDock");
            Assert.That(r.Components<Button>().Any(button => button.name == "EditPrice_cpu1_boxed"), Is.False);
            Assert.That(r.Button("EditPrice_cpu0_boxed"), Is.SameAs(edit));

            long price = shop.PriceOf(part, true).Yuan;
            edit.onClick.Invoke();
            r.Button("PricePlus_cpu0_boxed").onClick.Invoke();
            r.Button("ConfirmPrice_cpu0_boxed").onClick.Invoke();
            Assert.That(shop.PriceOf(part, true).Yuan, Is.EqualTo(price + 1));
            Assert.That(r.Button("EditPrice_cpu0_boxed"), Is.SameAs(edit));
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
        public void ScheduleUsesMonthGridWithSmartphoneAndNotebookWithoutIt(bool smartphone)
        {
            using (var r = new Rig())
            {
                r.S.SetSmartphone(smartphone);
                if (smartphone)
                {
                    Assert.True(r.S.OrderOnline(ParcelKind.Rice, "rice"));
                    Assert.True(r.S.OrderOnline(ParcelKind.Rice, "rice"));
                }
                r.Ui.OpenSchedulePanel(); var window = r.Workspace("calendar");
                Assert.False(window.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<Text>().text == "城市活动"));
                Assert.False(window.GetComponentsInChildren<Button>().Any(b => b.name == "CalendarWeather"));
                if (smartphone)
                {
                    int today = r.S.Clock.Today.TotalDays;
                    int due = r.S.Parcels[0].DueDay;
                    Assert.AreEqual(GameDate.DaysPerMonth, window.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("CalendarDay_")));
                    var captions = r.Button("CalendarDay_" + due).GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
                    CollectionAssert.Contains(captions, "快递到货");
                    CollectionAssert.Contains(captions, "+1");
                    r.Button("CalendarDay_" + due).onClick.Invoke();
                    StringAssert.Contains("2 项安排", r.Text);
                    StringAssert.Contains("快递到家：大米", r.Text);
                    r.Button("CalendarNextMonth").onClick.Invoke();
                    Assert.NotNull(r.Button("CalendarDay_" + GameDate.DaysPerMonth));
                    r.Button("CalendarToday").onClick.Invoke();
                    StringAssert.Contains("今", r.Button("CalendarDay_" + today).GetComponentInChildren<Text>().text);
                }
                else
                {
                    Assert.False(window.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("CalendarDay_")));
                    StringAssert.Contains("笔记本", r.Text);
                }
                r.Ui.OpenSchedulePanel();
                Assert.AreSame(window, r.Workspace("calendar"));
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
                r.Ui.OpenMyPanel(); StringAssert.Contains("到米家购买居家办公台", r.Button("MyHomeWorkbench").GetComponentInChildren<Text>().text);
                r.S.HomeBox.AddFurniture(GameSession.HomeWorkbenchId, 1); r.Ui.RebuildWindows();
                StringAssert.Contains("需要中型及以上住宅安装", r.Button("MyHomeWorkbench").GetComponentInChildren<Text>().text);
                HomeWorkbenchTests.MoveAndInstallWorkbench(r.S);
                GameSessionTests.Teleport(r.S, r.S.MainShop.Place.Id); r.Ui.RebuildWindows();
                StringAssert.Contains("回家后可用", r.Button("MyHomeWorkbench").GetComponentInChildren<Text>().text);
                GameSessionTests.Teleport(r.S, r.S.Home.Id); r.Ui.RebuildWindows(); Assert.True(r.Button("MyHomeWorkbench").interactable);
            }
        }

        [Test]
        public void MiHomeStoreShowsUpgradeBlockersAndSellsCheckoutEquipmentWithLowReputation()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var r = new Rig(new GameSession(fixture.City, 7, Money.FromYuan(2_000_000), fixture.Clock.Now));
            r.S.PlayerShop.Reputation = 2;
            var place = r.S.City.Places.Single(p => p.Business == GameSession.PhoneBusiness);
            Assert.AreEqual("米家", place.Name);
            GameSessionTests.Teleport(r.S, place.Id);
            r.Ui.OnEnteredPlace(place);
            const string plus = "QuantityPlus_upgrade:selfcheck";
            Assert.IsFalse(r.Button(plus).interactable);
            StringAssert.Contains("需要先买：开通收款码", r.Text);
            Assert.True(r.S.BuyUpgrade(PCShopSell.Core.Shop.ShopUpgrades.Qr));
            r.Ui.RebuildWindows();
            Assert.IsTrue(r.Button(plus).interactable);
            var hold = r.Button(plus).GetComponent<HoldRepeat>();
            hold.Commit(1);
            r.Ui.RebuildWindows();
            r.Button("CartCheckout").onClick.Invoke();
            Assert.True(r.S.HasUpgrade(PCShopSell.Core.Shop.ShopUpgrades.SelfCheckout));
            Assert.IsTrue(r.Button("QuantityPlus_furn:" + GameSession.HomeWorkbenchId).interactable);
            StringAssert.Contains(Money.FromYuan(1_080_000).ToString(), r.Text);
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
        public void RetailQuantityAcceptsWeightAndKeepsTheStepButtonsAndOpeningHours()
        {
            using var r = new Rig(HomeTests.Make(5000000, 600));
            GameSessionTests.Teleport(r.S, "farm");
            r.Ui.OnEnteredPlace(r.S.CurrentPlace);
            var field = r.Components<InputField>().Single(f => f.name == "QuantityValue_rice");
            Assert.That(field.contentType, Is.EqualTo(InputField.ContentType.DecimalNumber));
            var cash = r.S.Ledger.Balance;

            field.onEndEdit.Invoke("1.38");
            Assert.That(r.S.CartAmount("rice"), Is.EqualTo(1.5));
            Assert.That(field.text, Is.EqualTo("1.5"));
            r.Button("QuantityPlus_rice").onClick.Invoke();
            Assert.That(r.S.CartAmount("rice"), Is.EqualTo(1.75));
            r.Button("QuantityMinus_rice").onClick.Invoke();
            Assert.That(r.S.CartAmount("rice"), Is.EqualTo(1.5));
            field.onEndEdit.Invoke("");
            Assert.That(field.text, Is.EqualTo("1.5"));

            r.S.Clock.Restore(r.S.Clock.Today, 23 * 60);
            r.Ui.RebuildWindows();
            field = r.Components<InputField>().Single(f => f.name == "QuantityValue_rice");
            field.onEndEdit.Invoke("12");
            Assert.That(r.S.CartAmount("rice"), Is.EqualTo(1.5));
            field.onEndEdit.Invoke("0");
            Assert.That(r.S.CartAmount("rice"), Is.Zero);
            Assert.That(r.S.Ledger.Balance, Is.EqualTo(cash));
        }

        [Test]
        public void OnlineOrdersUseTypedQuantitiesForPartsAndParcels()
        {
            using var r = new Rig();
            r.S.SetSmartphone(true);
            r.S.InstallApp(PhoneApps.Shop);
            r.Ui.OpenWholesaleApp();
            r.Components<InputField>().Single(f => f.name == "QuantityValue_wholesale_cpu0").onEndEdit.Invoke("8");
            Assert.That(r.S.Online.Pending, Is.Empty);
            r.Ui.RebuildWindows();
            r.Button("OnlineOrder").onClick.Invoke();
            Assert.That(r.S.Online.Pending.Single().Lines.Single().Count, Is.EqualTo(8));

            r.ClickCaption("大米");
            var parcel = r.Components<InputField>().Single(f => f.name == "QuantityValue_parcel_rice");
            parcel.onEndEdit.Invoke("2");
            r.Ui.RebuildWindows();
            // Clicking the existing order button after editing must use the new amount.
            r.Components<InputField>().Single(f => f.name == "QuantityValue_parcel_rice").onEndEdit.Invoke("3");
            r.Button("OnlineOrder").onClick.Invoke();
            Assert.That(r.S.Parcels.Single().Count, Is.EqualTo(3));
        }

        [Test]
        public void EmployeePurchaseAcceptsTypedCountAndKeepsMaximumMode()
        {
            using var r = new Rig();
            var buyer = new Employee { Name = "进货员", DailyWageYuan = 150 };
            r.S.MainShop.Buyer = buyer;
            r.Ui.OpenEmployeePurchase(buyer.Id);
            var field = r.Components<InputField>().First(f => f.name.StartsWith("QuantityValue_"));
            string key = field.name.Substring("QuantityValue_".Length);
            var offer = r.S.EmployeePurchaseCatalog().Single(item => item.Key == key);
            int count = r.S.PurchaseMinimum(offer) + 1;
            field.onEndEdit.Invoke(count.ToString());
            Assert.That(field.text, Is.EqualTo(count.ToString()));
            Assert.That(buyer.PurchasePlan.Items, Is.Empty);
            r.Button("PurchaseDone").onClick.Invoke();
            Assert.That(buyer.PurchasePlan.Items.Single().Count, Is.EqualTo(count));
            Assert.That(buyer.PurchasePlan.Items.Single().BuyMaximum, Is.False);

            r.Ui.OpenEmployeePurchase(buyer.Id);
            r.Button("PurchaseMaximum_" + key).onClick.Invoke();
            r.Button("PurchaseDone").onClick.Invoke();
            Assert.That(buyer.PurchasePlan.Items.Single().BuyMaximum, Is.True);
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
        public void WalletStationKeepsTimeRunningAndEnablesHandoffAfterWaiting()
        {
            using var r = new Rig();
            r.S.RaiseEvent("wallet");
            Assert.That(r.S.ResolveEvent(0), Is.True);
            var station = r.S.WalletReturnStation;
            GameSessionTests.Teleport(r.S, station.Id);
            r.S.Time.SetPaused(true);
            r.Ui.OnEnteredPlace(station);
            Assert.That(r.S.Time.IsHeld, Is.False);
            Assert.That(r.Button("WalletReturnDesk").interactable, Is.False);
            r.S.Time.SecondsPerMinuteAt1x = .5;
            r.Button("WalletResumeWait").onClick.Invoke();
            Assert.That(r.S.Time.IsRunning, Is.True);
            for (int i = 0; i < 30; i++) r.S.Tick(.25);
            r.Ui.RebuildWindows();
            var handoff = r.Button("WalletReturnDesk");
            Assert.That(handoff.interactable, Is.True);
            handoff.onClick.Invoke();
            Assert.That(r.S.WalletReturnStation, Is.Null);
            Assert.That(r.S.Time.IsHeld, Is.True);
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
