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
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class UiCleanupTests
    {
        [Test]
        public void SharedButtonSkinKeepsFillBorderAndCaptionInSync()
        {
            var root = new GameObject("ButtonSkinTest");
            try
            {
                var button = Ui.Button(root.transform, "测试", () => { });
                var caption = button.GetComponentInChildren<Text>();
                var border = button.GetComponent<Outline>();
                Assert.AreEqual(Ui.ButtonBg, button.image.color);
                Assert.AreEqual(Ui.FlatButtonEdge, border.effectColor);
                Assert.AreEqual(Ui.TextColor, caption.color);

                Ui.SetButtonFill(button, Ui.SelectedButton(true));
                Assert.AreEqual(Ui.FlatRow, button.image.color);
                Assert.AreEqual(Ui.FlatHotEdge, border.effectColor);
                Assert.AreEqual(Ui.Accent, caption.color);

                button.interactable = false;
                Assert.AreEqual(Ui.Dim, caption.color);
                button.interactable = true;
                Assert.AreEqual(Ui.Accent, caption.color);

                Ui.SetButtonFill(button, Ui.SelectedButton(false));
                Assert.AreEqual(Ui.ButtonBg, button.image.color);
                Assert.AreEqual(Ui.FlatButtonEdge, border.effectColor);
                Assert.AreEqual(Ui.TextColor, caption.color);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

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
        public void SpaceIsReservedFromUiSubmitWhileEnterAndControllerStillSubmit()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").Single();
            using var inputManager = new SerializedObject(settings);
            var axes = inputManager.FindProperty("m_Axes");
            var keys = Enumerable.Range(0, axes.arraySize).Select(axes.GetArrayElementAtIndex)
                .Where(axis => axis.FindPropertyRelative("m_Name").stringValue == "Submit")
                .SelectMany(axis => new[]
                {
                    axis.FindPropertyRelative("positiveButton").stringValue,
                    axis.FindPropertyRelative("altPositiveButton").stringValue
                }).ToArray();
            CollectionAssert.DoesNotContain(keys, "space");
            CollectionAssert.IsSubsetOf(new[] { "return", "enter", "joystick button 0" }, keys);
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator OpeningEmployeePurchaseClearsTheOpenerSelection()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            using (var r = new Rig())
            {
                r.S.CurrentShop.Buyer = new Employee { Name = "乔禾", DailyWageYuan = 120 };
                r.Ui.OpenDock();
                r.Ui.DebugSelectTab(5);
                yield return null;
                var arrange = r.Components<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == "安排进货");
                var events = EventSystem.current;
                Assert.NotNull(events);
                events.SetSelectedGameObject(arrange.gameObject);
                Assert.AreSame(arrange.gameObject, events.currentSelectedGameObject);
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);

                Assert.IsTrue(r.S.Time.IsHeld);
                Assert.IsNull(events.currentSelectedGameObject);
                Assert.AreEqual(1, r.Components<Transform>().Count(t => t.name.StartsWith("Window_定期采购清单")));
                Assert.IsTrue(r.Ui.CloseTopWindow());
                Assert.IsFalse(r.S.Time.IsHeld);
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        [Test]
        public void CompactHudFitsTwoRowsAndPriceButtonSharesItsHotkey()
        {
            using var rig = new Rig();
            var top = (RectTransform)rig.Components<Transform>().Single(t => t.name == "TopBar");
            var canvas = top.GetComponentInParent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var viewport = (RectTransform)canvas.transform;
            var row = (RectTransform)top.Find("Row");
            var shortcuts = (RectTransform)row.Find("HudShortcuts");
            var daily = (RectTransform)shortcuts.Find("HudShortcutRows/HudDailyShortcuts");
            var business = (RectTransform)shortcuts.Find("HudShortcutRows/HudBusinessShortcuts");
            var purchase = (RectTransform)rig.Button("HudProcurement").transform;
            var price = (RectTransform)rig.Button("HudPrices").transform;
            var menu = (RectTransform)rig.Button("HudMenu").transform;
            var timeControls = (RectTransform)row.Find("HudTimeControls");
            var speeds = TimeController.Speeds.Select(speed => rig.Button("HudSpeed_" + speed)).ToArray();
            var pause = rig.Button("HudPauseTime");
            Assert.AreSame(business, price.parent);
            Assert.AreEqual(purchase.GetSiblingIndex() + 1, price.GetSiblingIndex());
            Assert.AreEqual("物价 H", price.GetComponentInChildren<Text>().text);
            Assert.AreEqual(Ui.ButtonHot, rig.Button("ShortcutGo_0").image.color);

            speeds[0].onClick.Invoke();
            Assert.AreEqual(TimeController.Speeds[0], rig.S.Time.Speed);
            pause.onClick.Invoke();
            Assert.True(rig.S.Time.IsUserPaused);
            rig.Call("RefreshHudTimeControls");
            Assert.AreEqual("继续", pause.GetComponentInChildren<Text>().text);
            Assert.AreEqual(Ui.FlatRow, speeds[0].image.color);
            rig.S.Time.Hold();
            rig.Call("RefreshHudTimeControls");
            Assert.AreEqual("窗口暂停", pause.GetComponentInChildren<Text>().text);

            Rect Bounds(RectTransform target)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                Vector3 min = top.InverseTransformPoint(corners[0]);
                Vector3 max = top.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            foreach (int width in new[] { 1920, 1280, 1024 })
            {
                viewport.sizeDelta = new Vector2(width, 1080);
                rig.Call("FitHudTopRow");
                LayoutRebuilder.ForceRebuildLayoutImmediate(row);
                Assert.AreEqual(64, top.rect.height, .1);
                Rect upper = Bounds(daily), lower = Bounds(business), menuRect = Bounds(menu);
                Assert.Greater(upper.yMin, lower.yMax);
                Assert.AreEqual(upper.yMax, menuRect.yMax, .1);
                Assert.AreEqual(lower.yMin, menuRect.yMin, .1);
                Assert.LessOrEqual(menuRect.xMax, top.rect.xMax);
                Assert.That(menuRect.yMin, Is.GreaterThanOrEqualTo(top.rect.yMin));
                Assert.That(menuRect.yMax, Is.LessThanOrEqualTo(top.rect.yMax));
                Assert.That(Bounds(price).width, Is.GreaterThan(0));
                if (width == 1920) Assert.AreEqual(.365, Bounds(shortcuts).width / width, .005);

                var firstSpeedRect = (RectTransform)speeds[0].transform;
                foreach (var button in speeds)
                {
                    var rect = (RectTransform)button.transform;
                    var caption = button.GetComponentInChildren<Text>();
                    Assert.AreEqual(firstSpeedRect.rect.width, rect.rect.width, .1);
                    Assert.AreEqual(Bounds(firstSpeedRect).center.y, Bounds(rect).center.y, .1);
                    Assert.AreEqual(HorizontalWrapMode.Overflow, caption.horizontalOverflow);
                    Assert.That(caption.preferredWidth, Is.LessThanOrEqualTo(caption.rectTransform.rect.width + 1));
                }
                Assert.That(Bounds((RectTransform)speeds.Last().transform).xMax, Is.LessThan(Bounds(timeControls).xMax));
            }
            rig.S.Time.Release();

            rig.Button("HudPrices").onClick.Invoke();
            Assert.That(rig.Workspace("market").GetComponentsInChildren<Text>().Select(label => label.text), Does.Contain("价格走势"));
            rig.Ui.CloseAllWindows();
            Assert.True(rig.Ui.TryHotkey(KeyCode.H));
            Assert.NotNull(rig.Workspace("market"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SoloParkWalkHasFreeRoutesAndNoPurchaseOrConversationControls(int route)
        {
            using var rig = new Rig(DistrictProgressionTests.Make());
            var park = rig.S.City.GetPlace("life_hike");
            GameSessionTests.Teleport(rig.S, park.Id);
            rig.Ui.OpenCityVenue(park);
            var routes = rig.Components<Button>().Where(b => b.GetComponentInChildren<Text>()?.text == "选这条路线").ToArray();
            Assert.AreEqual(2, routes.Length);
            StringAssert.Contains("公园步道", rig.Text);
            Money before = rig.S.Ledger.Balance;

            routes[route].onClick.Invoke();
            var walk = rig.S.RunningCityActivity;
            Assert.NotNull(walk, rig.S.VenueServiceBlocker("hike", route));
            Assert.AreEqual(route, walk.ServiceChoice);
            Assert.AreEqual(0, walk.EntryCostCents);
            Assert.AreEqual(before, rig.S.Ledger.Balance);
            rig.Ui.UpdateCityLifeHud(1);
            StringAssert.Contains("散步详情", rig.Text);
            rig.Ui.OpenCityVenue(park);
            StringAssert.Contains("散步路线", rig.Text);
            Assert.That(rig.Text, Does.Not.Contain("本次消费").And.Not.Contain("打开场所菜单")
                .And.Not.Contain("打开菜单 / 服务").And.Not.Contain("聊生活").And.Not.Contain("分享经历"));
            rig.Ui.CloseAllWindows();
            rig.S.AdvanceClock(walk.Duration);
            Assert.AreEqual(CityActivityPhase.Completed, walk.State, walk.EndReason);
            rig.Ui.OpenCityActivitySettlement(walk.Id);
            Assert.That(rig.Text, Does.Not.Contain("本次支出").And.Not.Contain("参考费用"));
        }

        [TestCase("life_court")]
        [TestCase("life_court_mid")]
        [TestCase("life_fitness")]
        [TestCase("life_golf")]
        public void SportVenueKeepsRosterBesideCompactPassOptions(string venueId)
        {
            using var rig = new Rig(DistrictProgressionTests.Make());
            var venue = rig.S.City.GetPlace(venueId);
            GameSessionTests.Teleport(rig.S, venueId);
            rig.Ui.OpenCityVenue(venue);
            var roster = rig.Components<Transform>().Single(t => t.name == "People");
            var goods = roster.parent.Find("Goods");
            Assert.NotNull(goods);
            var passes = goods.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("SportPass_")).ToArray();
            Assert.AreEqual(4, passes.Length);
            Assert.That(passes.All(button => button.GetComponent<LayoutElement>().preferredHeight == 36));
            string text = string.Join("\n", goods.GetComponentsInChildren<Text>().Select(label => label.text));
            StringAssert.Contains("全部用完结算 2 小时", text);
            StringAssert.Contains("全部用完结算 6 小时", text);
        }

        [Test]
        public void RacingQuestAndMenuOnlyOfferTravelWhenEligibleAndGarageShowsRegistrationFirst()
        {
            using var world = DistrictProgressionTests.Make();
            using var rig = new Rig(new GameSession(world.City, 7, Money.FromYuan(99999), world.Clock.Now));
            foreach (var quest in Quests.All.TakeWhile(q => q.Id != "racing_enrollment"))
                rig.S.Flags.Add("tutorial:" + quest.Id);
            Assert.That(rig.S.CurrentQuest()?.Id, Is.EqualTo("racing_enrollment"));
            Assert.That(rig.S.BuyEventScratchTicket(), Is.True);
            rig.S.Flags.Add("life:library-invited");

            Transform CheckQuest()
            {
                var window = rig.Components<Transform>().Single(t => t.name == "Window_当前任务");
                string text = string.Join("\n", window.GetComponentsInChildren<Text>().Select(label => label.text));
                StringAssert.Contains("当前任务", text);
                StringAssert.Contains("办理赛车登记", text);
                Assert.That(text, Does.Not.Contain("经营待办").And.Not.Contain("社区生活"));
                return window;
            }

            rig.Ui.OpenQuests();
            var blocked = CheckQuest();
            StringAssert.Contains("现金需达到", rig.Ui.QuestTrackerText);
            Assert.That(blocked.GetComponentsInChildren<Button>().Any(button =>
                button.GetComponentInChildren<Text>()?.text == "前往城郊赛场车库"), Is.False);
            rig.Ui.CloseAllWindows();
            rig.Ui.OpenRacingDestinations();
            StringAssert.Contains("现金需达到", rig.Text);
            Assert.That(rig.Components<Button>().Any(button =>
                button.GetComponentInChildren<Text>()?.text == "前往城郊赛场车库"), Is.False);
            rig.Ui.CloseAllWindows();

            rig.S.DebugAddCash(1000);
            rig.Ui.OpenQuestWindow();
            CheckQuest().GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<Text>()?.text == "前往城郊赛场车库").onClick.Invoke();
            var garage = rig.S.City.GetPlace(GameSession.StarterRaceGarageId);
            Assert.That(rig.S.Trip?.TargetNode, Is.EqualTo(garage.NodeId));
            Assert.That(rig.S.CityIntent.EnterOnArrival, Is.False);

            rig.S.StopTrip();
            rig.S.ArriveAtNode(garage.NodeId);
            Assert.That(rig.S.Enter(garage), Is.True);
            rig.Ui.OnEnteredPlace(garage);
            StringAssert.Contains("车库负责人 · 赛车登记", rig.Text);
            Assert.That(rig.S.RacingUnlocked, Is.False);
            Assert.That(rig.Components<RectTransform>().Any(rect => rect.name == "GarageParking"), Is.False);
            rig.ClickCaption("办理赛车登记");
            Assert.That(rig.S.RacingUnlocked, Is.True);
            Assert.That(rig.Components<RectTransform>().Any(rect => rect.name == "GarageParking"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScheduleShowsEventActionsWithoutTheRemovedShortcuts(bool smartphone)
        {
            using var rig = new Rig(DistrictProgressionTests.Make());
            rig.S.SetSmartphone(smartphone);
            rig.Ui.OpenSchedulePanel();
            Assert.That(rig.Text, Does.Not.Contain("经营待办").And.Not.Contain("社区生活"));
            rig.Ui.CloseAllWindows();

            Assert.That(rig.S.BuyEventScratchTicket(), Is.True);
            rig.S.Flags.Add("life:library-invited");
            rig.Ui.OpenSchedulePanel();
            var calendar = rig.Workspace("calendar");
            var captions = calendar.GetComponentsInChildren<Button>()
                .Select(button => button.GetComponentInChildren<Text>()?.text).ToArray();
            Assert.That(captions, Has.None.EqualTo("经营待办").And.None.EqualTo("社区生活").And.None.EqualTo("社区生活待办"));
            rig.ClickCaption("图书");
            StringAssert.Contains("社区图书角", rig.Text);
            rig.ClickCaption("刮开");
            Assert.That(rig.Components<RectTransform>().Any(rect => rect.name == "ScratchTicketSurface"), Is.True);
        }

        [Test]
        public void FurnitureBuyback_UsesSelectedShopAndShowsArrivalWithoutClosingWindow()
        {
            using var rig = new Rig(HomeTests.Make(500000, 600));
            rig.RentSecond();
            var second = rig.S.CurrentShop;
            string id = FurnitureCatalog.FridgeSmall;
            second.Locker.AddFurniture(id, 2);
            rig.S.MainShop.Locker.AddFurniture(FurnitureCatalog.RackHeavy, 1);
            GameSessionTests.Teleport(rig.S, "mall");
            rig.Ui.OnEnteredPlace(rig.S.CurrentPlace);
            rig.Button("OpenFurnitureBuyback").onClick.Invoke();
            Assert.That(rig.S.Time.IsHeld, Is.True);
            var windows = (System.Collections.Generic.List<Window>)typeof(GameUi)
                .GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig.Ui);
            var buyback = windows.Last();
            void UpdateBuybackWindow()
            {
                const BindingFlags state = BindingFlags.Instance | BindingFlags.NonPublic;
                ((Action)typeof(Window).GetField("RefreshContent", state).GetValue(buyback))?.Invoke();
                if ((bool)typeof(Window).GetField("RebuildPending", state).GetValue(buyback))
                    typeof(Window).GetMethod("ApplyPendingRebuild", state).Invoke(buyback, null);
            }
            Assert.That(rig.Button("ConfirmFurnitureBuyback").interactable, Is.False);
            var picker = rig.Components<Dropdown>().Single(control => control.name == "FurnitureBuybackShop");
            picker.value = rig.S.MyShops.ToList().IndexOf(second);
            UpdateBuybackWindow();
            Assert.That(rig.Components<InputField>().Any(control => control.name == "QuantityValue_Buyback_" + FurnitureCatalog.RackHeavy), Is.False);
            rig.Components<InputField>().Single(control => control.name == "QuantityValue_Buyback_" + id).onEndEdit.Invoke("2");
            UpdateBuybackWindow();
            var confirm = rig.Button("ConfirmFurnitureBuyback");
            Assert.That(confirm.interactable, Is.True);
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
            UpdateBuybackWindow();
            var order = rig.S.FurnitureBuybacks.Single();
            Assert.That(order.ShopId, Is.EqualTo(second.Place.Id));
            Assert.That(order.Count, Is.EqualTo(2));
            Assert.That(second.Locker.FurnitureCount(id), Is.Zero);
            Assert.That(rig.S.MainShop.Locker.FurnitureCount(FurnitureCatalog.RackHeavy), Is.EqualTo(1));
            Assert.That(rig.S.Time.IsHeld, Is.False);
            StringAssert.Contains("货车在途", rig.Text);
            StringAssert.Contains("1 辆面包车 · 56 千克", rig.Text);

            rig.S.Clock.Advance((int)(order.DueMinute - rig.S.Clock.Now.TotalMinutes));
            UpdateBuybackWindow();
            StringAssert.Contains("回收已完成", rig.Text);
            StringAssert.Contains("回收款 · 已到账", rig.Text);
            rig.Button("ConfirmFurnitureBuyback").onClick.Invoke();
            UpdateBuybackWindow();
            Assert.That(rig.S.Time.IsHeld, Is.True);
            buyback.Close();
            Assert.That(rig.S.Time.IsHeld, Is.True);
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
        public void PhoneDeliveryOrdersAndConfiguresAutomaticMealsIndependently()
        {
            using var rig = new Rig();
            var session = rig.S;
            session.SetSmartphone(true);
            session.Player.Hunger = 40;
            var cash = session.Ledger.Balance;
            var order = GameSession.DeliveryMeals[2];

            rig.Ui.OpenPhone();
            rig.Button("PhoneApp_" + PhoneApps.FoodDelivery).onClick.Invoke();
            rig.Button("MealDeliveryAutoTab").onClick.Invoke();
            rig.Button("MealDeliveryAutoTier_1").onClick.Invoke();
            Assert.AreEqual(1, session.MealDeliveryTier);
            Assert.False(session.MealDeliveryEnabled);
            Assert.AreEqual(cash, session.Ledger.Balance);

            rig.Button("MealDeliveryOrderTab").onClick.Invoke();
            rig.Button("MealDeliveryOrder_2").onClick.Invoke();
            Assert.AreEqual(cash - Money.FromYuan(order.Price), session.Ledger.Balance);
            Assert.AreEqual(100, session.Player.Hunger);
            Assert.False(session.MealDeliveryEnabled);
            Assert.AreEqual(1, session.MealDeliveryTier);
            StringAssert.Contains(order.Name, rig.Components<Text>().Single(t => t.name == "MealDeliveryReceipt").text);
            Assert.False(rig.Button("MealDeliveryOrder_2").interactable);

            rig.Button("MealDeliveryAutoTab").onClick.Invoke();
            rig.Button("MealDeliveryAutoToggle").onClick.Invoke();
            Assert.True(session.MealDeliveryEnabled);
            Assert.AreEqual(cash - Money.FromYuan(order.Price), session.Ledger.Balance);
            using var loaded = GameSession.Load(session.City, session.CreateSave());
            Assert.True(loaded.MealDeliveryEnabled);
            Assert.AreEqual(1, loaded.MealDeliveryTier);
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
                    Assert.NotNull(racing.GetComponentInChildren<HorizontalLayoutGroup>().transform.Find("NavigationIcon").GetComponent<Image>().sprite);
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
        public void FurnitureMallKeepsVariantsTogetherAcrossShopTabs()
        {
            using var r = new Rig(HomeTests.Make(200000, 10 * 60));
            GameSessionTests.Teleport(r.S, "mall");
            r.Ui.OnEnteredPlace(r.S.CurrentPlace);
            const string fieldPrefix = "QuantityValue_furn:";

            void AssertAdjacent(params string[] ids)
            {
                var rows = r.Components<InputField>().Where(input => input.name.StartsWith(fieldPrefix))
                    .Select(input => input.name.Substring(fieldPrefix.Length)).ToArray();
                int first = Array.IndexOf(rows, ids[0]);
                Assert.That(first, Is.GreaterThanOrEqualTo(0), ids[0]);
                CollectionAssert.AreEqual(ids, rows.Skip(first).Take(ids.Length).ToArray());
            }

            var shelves = new[] { FurnitureCatalog.Shelf, FurnitureCatalog.RackStrong, FurnitureCatalog.RackHeavy };
            var fridges = new[] { FurnitureCatalog.FridgeSmall, FurnitureCatalog.FridgeFamily, FurnitureCatalog.FridgeSide, FurnitureCatalog.FridgePremium };
            r.Button("ShopTab_全部").onClick.Invoke();
            AssertAdjacent(shelves);
            AssertAdjacent(fridges);
            AssertAdjacent("aircon", "aircon_large");
            r.Button("ShopTab_facility_电器").onClick.Invoke();
            AssertAdjacent(fridges);
            r.Button("ShopTab_facility_非电器").onClick.Invoke();
            AssertAdjacent(shelves);
            r.Button("ShopTab_facility_全部").onClick.Invoke();
            foreach (var kind in FurnitureShops.AllKinds)
            {
                r.Button("ShopTab_" + RetailShopTypes.Name(kind)).onClick.Invoke();
                if (kind == "restaurant") AssertAdjacent(fridges);
                else AssertAdjacent(shelves);
                if (kind == PetShopCatalog.ShopKind)
                {
                    AssertAdjacent(PetShopCatalog.SmallHouse, PetShopCatalog.MediumHouse, PetShopCatalog.LargeHouse);
                    AssertAdjacent(PetShopCatalog.ToyFixture(false, 0), PetShopCatalog.ToyFixture(false, 1), PetShopCatalog.ToyFixture(false, 2));
                    AssertAdjacent(PetShopCatalog.ToyFixture(true, 0), PetShopCatalog.ToyFixture(true, 1), PetShopCatalog.ToyFixture(true, 2));
                }
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
                Assert.AreEqual(Ui.ButtonHot, r.Button("CourierDestination").image.color);

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
                Assert.AreEqual(Ui.ButtonHot, r.Button("CourierDestination").image.color);

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
        public void StockDraftCyclesLotsAndConfirmsBeforeUsingReserve()
        {
            using (var r = new Rig())
            {
                r.S.InstallApp(PhoneApps.Securities);
                r.S.RestockReserveYuan = r.S.Ledger.Balance.Cents / 100;
                r.Ui.OpenCityInvestments();
                var cash = r.S.Ledger.Balance;

                Assert.False(r.Button("StockDraftMinus_city_chip").interactable);
                foreach (var lots in new[] { 5, 25, 100, 1 })
                {
                    var cycle = r.Button("StockLotCycle");
                    cycle.onClick.Invoke();
                    Assert.AreEqual($"×{lots} 手", cycle.GetComponentInChildren<Text>().text);
                    r.Button("StockDraftPlus_city_chip").onClick.Invoke();
                    var draft = r.Components<Text>().Single(t => t.name == "StockDraft_city_chip");
                    Assert.AreEqual($"+{lots * GameSession.StockLotSize:N0} 股", draft.text);
                    r.Button("StockDraftMinus_city_chip").onClick.Invoke();
                    Assert.AreEqual("—", draft.text);
                }
                r.Button("StockDraftPlus_city_chip").onClick.Invoke();
                r.Button("StockPage_Charts").onClick.Invoke();
                r.Button("StockChartSeries_city_chip").onClick.Invoke();
                Assert.That(r.Button("StockChartSeries_city_chip").transform.Find("SeriesHidden"), Is.Not.Null);
                r.Button("StockPage_Exchange").onClick.Invoke();
                Assert.That(r.Components<Text>().Single(t => t.name == "StockDraft_city_chip").text, Is.EqualTo("+100 股"));
                Assert.AreEqual(cash, r.S.Ledger.Balance);
                r.Button("StockBatchExecute").onClick.Invoke();
                StringAssert.Contains("成交会动用预留资金", r.Text);
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

            Text Amount(string name) => r.Components<Text>().Single(text => text.name == name);
            Assert.AreEqual(cash.ToString(), Amount("StockAvailableCash").text);
            Assert.AreEqual(Money.Zero.ToString(), Amount("StockBatchNet").text);
            foreach (string amountName in new[] { "StockAvailableCash", "StockBatchNet" })
            {
                Assert.AreEqual(FontStyle.Bold, Amount(amountName).fontStyle);
                Assert.GreaterOrEqual(Amount(amountName).fontSize, 28);
            }
            Assert.IsEmpty(r.Components<InputField>());
            Assert.That(r.Components<Transform>().Any(item => item.name == "StockQuote_hardware"), Is.False);
            r.Button("StockDraftMore_" + id).onClick.Invoke();
            Assert.That(r.Components<InputField>(), Has.Length.EqualTo(1));
            Assert.That(r.Button("StockQuickSellAll_" + id).interactable, Is.False);
            r.Button("StockQuickBuyMax_" + id).onClick.Invoke();
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash));
            Assert.AreEqual((Money.Zero - cost).ToString(), Amount("StockBatchNet").text);
            Assert.AreEqual(Ui.FlatDown, Amount("StockBatchNet").color);
            r.Button("StockBatchExecute").onClick.Invoke();
            r.Button("StockReserveConfirm").onClick.Invoke();
            Assert.That(s.CityInvestmentUnits(id), Is.EqualTo(200));
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash - cost));
            Assert.AreEqual((cash - cost).ToString(), Amount("StockAvailableCash").text);
            var proceeds = s.CityInvestmentTradeTotal(id, 200, buy: false);
            r.Button("StockDraftMore_" + id).onClick.Invoke();
            Assert.That(r.Button("StockQuickBuyMax_" + id).interactable, Is.False);
            r.Button("StockQuickSellAll_" + id).onClick.Invoke();
            Assert.That(s.CityInvestmentUnits(id), Is.EqualTo(200));
            Assert.AreEqual("+" + proceeds, Amount("StockBatchNet").text);
            Assert.AreEqual(Ui.Good, Amount("StockBatchNet").color);
            r.Button("StockBatchExecute").onClick.Invoke();
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
        public void SpecialStoreCounterPricingAndOrdersOrRecordsHaveDistinctControls(string kind)
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
                if (kind == "restaurant")
                {
                    var state = r.S.SpecialBusiness(r.S.CurrentShop.Place.Id);
                    state.Paid = 334;
                    state.Revenue = 15092;
                    state.Lost = 89;
                    state.Orders.Add(new DiningOrder { Id = 1, Table = 0, Recipe = 0, Price = 36, Stage = DiningStage.Waiting });
                    state.Orders.Add(new DiningOrder { Id = 2, Table = 1, Recipe = 0, Price = 36, Stage = DiningStage.Left });
                    state.Orders.Add(new DiningOrder { Id = 3, Table = 2, Recipe = 0, Price = 36, Stage = DiningStage.Completed, TableCleared = true });
                }
                r.ClickCaption(kind == "restaurant" ? "订单" : "记录");
                Assert.False(r.Components<Button>().Any(b => b.name == "SaveMenuMarkup" || b.name == "ChillKitchenIngredients"));
                StringAssert.DoesNotContain("每日收购预算", r.Text);
                if (kind == "restaurant")
                {
                    StringAssert.Contains("当前桌单 2 单", r.Text);
                    StringAssert.Contains("1号桌 · 青菜炒饭 · ¥36 · 待备料", r.Text);
                    StringAssert.Contains("2号桌 · 青菜炒饭 · ¥36 · 顾客已离开 · 待收桌", r.Text);
                    StringAssert.DoesNotContain("3号桌", r.Text);
                    StringAssert.DoesNotContain("累计成交", r.Text);
                    StringAssert.DoesNotContain("营业收入", r.Text);
                    StringAssert.DoesNotContain("累计流失", r.Text);
                    r.ClickCaption("返回柜台处理桌单");
                    Assert.NotNull(r.Button("ShopMainCategory"));
                }
                else
                {
                    StringAssert.Contains("收购与售出记录", r.Text);
                    StringAssert.Contains("累计成交", r.Text);
                }
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
            Assert.AreEqual(Ui.FlatRow, r.Button("UsedRisk_积极").image.color);
            Assert.False(r.Ui.AnyModalOpen);
            Assert.False(r.S.Time.IsHeld);
        }

        [Test]
        public void UsedDeskButtonShowsAWarningDotWhileSellersAreWaiting()
        {
            using var r = new Rig();
            Assert.True(r.S.SetShopMainCategory("secondhand"));
            var shop = r.S.CurrentShop;
            shop.Layout.Clear();
            var furniture = GameSession.OpeningFurniture("secondhand");
            for (int i = 0; i < furniture.Length; i++)
                shop.Layout.Add(new PlacedFurniture { Id = furniture[i], X = i + 1, Z = 2 });
            shop.Sim.ForcedClosed = false;
            var state = r.S.SpecialBusiness(shop.Place.Id);
            state.NextVisit = long.MaxValue;
            r.Ui.OpenDock();
            r.Ui.DebugSelectTab(0);

            Transform WaitingDot() => r.Button("OpenUsedDesk").transform.Find("UsedSellerWaitingDot");
            Assert.AreEqual(Ui.ButtonHot, r.Button("OpenUsedDesk").image.color);
            Assert.IsNull(WaitingDot());

            var seller = new UsedItem { Id = 1, Minimum = 20, ExpiresAt = r.S.Clock.Now.TotalMinutes + 60 };
            state.Used.Add(seller);
            r.Call("RefreshDock");
            Assert.IsNotNull(WaitingDot());
            Assert.False(WaitingDot().GetComponent<Text>().raycastTarget);

            Assert.True(r.S.OfferUsedPrice(seller.Id, 40));
            r.Call("RefreshDock");
            Assert.IsNull(WaitingDot());
            StringAssert.Contains("待鉴定 1", r.Text);

            state.Used.Add(new UsedItem { Id = 2, ExpiresAt = seller.ExpiresAt });
            r.Call("RefreshDock");
            Assert.IsNotNull(WaitingDot());
            Assert.True(r.S.DeclineUsedSeller(2));
            r.Call("RefreshDock");
            Assert.IsNull(WaitingDot());

            state.Used.Add(new UsedItem { Id = 3, ExpiresAt = r.S.Clock.Now.TotalMinutes + 1 });
            r.Call("RefreshDock");
            Assert.IsNotNull(WaitingDot());
            r.S.AdvanceClock(2);
            r.Call("RefreshDock");
            Assert.IsNull(WaitingDot());
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
        public void DeferredWindowRefreshKeepsThePressedButtonAndHoverDoesNotChangeItsTint()
        {
            using var r = new Rig();
            var window = r.Ui.OpenWindow("刷新测试", 440, 280, w =>
                Ui.Button(w.Content, "测试操作", null).name = "RefreshTestAction");
            var action = r.Button("RefreshTestAction");
            Assert.AreEqual(action.colors.normalColor, action.colors.highlightedColor);
            Assert.AreEqual(0, action.colors.fadeDuration);
            Assert.AreNotEqual(action.colors.normalColor, action.colors.pressedColor);
            Assert.AreNotEqual(action.colors.normalColor, action.colors.disabledColor);

            var canRefresh = typeof(Window).GetMethod("CanApplyPendingRebuild", BindingFlags.Instance | BindingFlags.NonPublic);
            window.RequestRebuild();
            Assert.False((bool)canRefresh.Invoke(window, new object[] { true }));
            Assert.AreSame(action, r.Button("RefreshTestAction"));
            Assert.True((bool)canRefresh.Invoke(window, new object[] { false }));
            typeof(Window).GetMethod("ApplyPendingRebuild", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            Assert.AreNotSame(action, r.Button("RefreshTestAction"));
        }

        [Test]
        public void FishChoicesAndPreordersShowTheirEffectsAndDeadline()
        {
            using var r = new Rig(HomeTests.Make());
            GameSessionTests.Teleport(r.S, r.S.Home.Id);
            var fish = FishingCatalog.Get("fish_mandarin");
            r.S.Backpack.Food.AddIngredient(fish.Id, 1);
            r.Ui.OpenCookerWindow();
            var choice = r.Components<Dropdown>().Single(d => d.name == "CookFishChoice");
            StringAssert.Contains("鳜鱼", choice.options[1].text);
            choice.value = 1;
            StringAssert.Contains("白米饭 + 鳜鱼", r.Text);
            StringAssert.Contains("本锅用 鳜鱼 1 条", r.Text);
            Assert.AreEqual(1, r.S.Backpack.Food.IngredientCount(fish.Id));
            r.Ui.CloseAllWindows();

            GameSessionTests.Teleport(r.S, "pc");
            Assert.True(r.S.SetShopMainCategory("restaurant"));
            var due = r.S.Clock.Now.AddMinutes(7 * GameTimestamp.MinutesPerDay);
            r.S.SpecialBusiness("pc").Orders.Add(new DiningOrder
            {
                Id = 1, Recipe = 0, Table = -1, FishId = fish.Id, DueAt = due.TotalMinutes,
                Stage = DiningStage.Waiting, Price = 2 * (36 + fish.DinnerSurcharge)
            });
            r.Ui.OpenDock();
            r.Ui.DebugSelectTab(3);
            StringAssert.Contains("鱼餐预订 1 / 2", r.Text);
            StringAssert.Contains("青菜炒饭 + 鳜鱼", r.Text);
            StringAssert.Contains($"¥{2 * (36 + fish.DinnerSurcharge):N0}", r.Text);
            StringAssert.Contains("交付名望 +0.65", r.Text);
            StringAssert.Contains("交期 " + due, r.Text);
        }

        [Test]
        public void MenuPriceSliderOnlyCommitsOnExplicitConfirmation()
        {
            using (var r = new Rig())
            {
                Assert.True(r.S.SetShopMainCategory("restaurant")); var state = r.S.SpecialBusiness("pc");
                r.Ui.OpenDock(); r.Ui.DebugSelectTab(2); double original = state.MenuMarkup;
                StringAssert.Contains("青菜炒饭 · ¥36", r.Text);
                StringAssert.Contains("鸡蛋炒饭 · ¥44", r.Text);
                StringAssert.Contains("肉末炒饭 · ¥56", r.Text);
                var slider = r.Components<Slider>().Single(s => s.name == "MenuMarkupSlider");
                var save = r.Button("SaveMenuMarkup");
                slider.value = 1.7f;
                r.S.PlayerShop.Stock.ForceAddGoods(Gifts.Soda, 1);
                r.Call("RefreshDock");
                Assert.AreSame(slider, r.Components<Slider>().Single(s => s.name == "MenuMarkupSlider"));
                Assert.AreSame(save, r.Button("SaveMenuMarkup"));
                Assert.True(save.interactable);
                Assert.AreEqual(original, state.MenuMarkup);
                save.onClick.Invoke(); Assert.AreEqual(1.7, state.MenuMarkup, 1e-9);
                Assert.AreSame(save, r.Button("SaveMenuMarkup"));
                Assert.False(save.interactable);
                StringAssert.Contains("青菜炒饭 · ¥61", r.Text);
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
        public void MarketOpensCurrentPriceReportAndCanBrowseHistoryAndGroups()
        {
            using var r = new Rig();
            var clock = r.S.Clock;
            clock.Restore(new GameDate(0), 600);
            Assert.True(r.Ui.TryHotkey(KeyCode.H));
            StringAssert.Contains("本期只有一份报价", r.Text);
            Assert.False(r.Button("EconomicPrevPeriod").interactable);
            r.Ui.CloseAllWindows();

            for (int day = 1; day <= 8; day++)
            {
                clock.Restore(new GameDate(day - 1), GameTimestamp.MinutesPerDay - 1);
                clock.Advance(1);
            }
            int food = Array.IndexOf(GameSession.PriceGroups, "米与食材");
            r.S.PriceDays.Last().Prices[food] = r.S.PriceDays.Single(day => day.Day == 6).Prices[food] * 1.12;
            r.Button("HudPrices").onClick.Invoke();
            var market = r.Workspace("market");
            string Range() => market.GetComponentsInChildren<Text>().Single(label => label.name == "EconomicPeriodRange").text;
            StringAssert.Contains("进行中", Range());
            StringAssert.Contains("最新报价", r.Text);
            StringAssert.Contains("+12.00%", r.Text);
            StringAssert.Contains("住宅与商铺", r.Text);
            StringAssert.DoesNotContain("币圈热度", r.Text);
            Assert.True(market.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "PriceTrend"));
            Assert.True(r.S.EconomicReportPending);
            Assert.False(r.Button("EconomicNextPeriod").interactable);

            r.Button("EconomicPriceGroup_" + food).onClick.Invoke();
            StringAssert.Contains("返回综合走势", r.Text);
            r.Button("EconomicPrevPeriod").onClick.Invoke();
            StringAssert.DoesNotContain("进行中", Range());
            StringAssert.Contains("期末报价", r.Text);
            r.Button("MarketTab_1").onClick.Invoke();
            StringAssert.Contains("硬件新闻", r.Text);
            StringAssert.Contains("小道消息", r.Text);
            r.Button("MarketTab_0").onClick.Invoke();
            Assert.AreSame(market, r.Workspace("market"));
            StringAssert.DoesNotContain("进行中", Range());
            r.Button("EconomicNextPeriod").onClick.Invoke();
            StringAssert.Contains("进行中", Range());
            r.ClickCaption("返回综合走势");
            StringAssert.DoesNotContain("返回综合走势", r.Text);

            r.Button("MarketOpenEconomicReport").onClick.Invoke();
            StringAssert.Contains("现金收支", r.Text);
            r.ClickCaption("物价涨跌");
            var report = r.Components<Transform>().Single(t => t.name == "Window_经济简报");
            Assert.True(report.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == "PriceTrend"));
            StringAssert.DoesNotContain("进行中", report.GetComponentsInChildren<Text>().Single(label => label.name == "EconomicPeriodRange").text);
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
        public void PersonalPanelKeepsTheSameWindowAndSharesPhoneContacts()
        {
            using (var r = new Rig())
            {
                r.S.Meet(Npcs.Zhou, 40);
                r.Ui.OpenMyPanel(); StringAssert.Contains("饱腹", r.Text);
                Assert.That(r.Components<RawImage>().Any(image => image.name == "MyPortrait"), Is.True);
                var window = r.Workspace("personal");
                var size = ((RectTransform)window.Find("Panel")).sizeDelta;
                var currentPlace = r.S.CurrentPlace;
                for (int i = 0; i < 5; i++)
                {
                    r.Button("MyTab_" + i).onClick.Invoke();
                    Assert.AreSame(window, r.Workspace("personal"));
                    Assert.AreEqual(size, ((RectTransform)window.Find("Panel")).sizeDelta);
                    Assert.IsFalse(r.Components<Transform>().Any(item => item.name == "Workspace_people"));
                    StringAssert.DoesNotContain("物品与包裹", r.Text);
                    StringAssert.DoesNotContain("摆放家中家具", r.Text);
                    StringAssert.DoesNotContain("去中介管理", r.Text);
                }
                r.Button("MyTab_2").onClick.Invoke();
                r.Button("MyLeaseDetails").onClick.Invoke();
                StringAssert.Contains("下次扣租", r.Text);
                Assert.AreSame(currentPlace, r.S.CurrentPlace);
                Assert.IsNull(r.S.Trip);

                string[] ContactActions() => r.Components<Button>().Select(button => button.GetComponentInChildren<Text>()?.text)
                    .Where(text => text == "人物资料" || text == "发消息" || text == "联系司机").ToArray();
                r.Button("MyTab_3").onClick.Invoke();
                var personalActions = ContactActions();
                CollectionAssert.AreEqual(new[] { "联系司机", "人物资料", "发消息" }, personalActions);
                r.Ui.CloseTopWindow();
                r.Ui.OpenContactsPanel();
                CollectionAssert.AreEqual(personalActions, ContactActions());
            }
        }

        [Test]
        public void HomeDockListsOnlyFurnitureInStorageAndOpensItsOwnResidenceTab()
        {
            using var r = new Rig();
            void Refresh() => r.Call("RefreshHomeDockVisibility", false);
            bool Visible(string name) => r.Components<RectTransform>().Any(item => item.name == name);
            Refresh();
            Assert.False(Visible("HomeDockToggle"));
            GameSessionTests.Teleport(r.S, r.S.Home.Id);
            Refresh();
            Assert.True(Visible("HomeDock"));
            r.Button("HomeDockToggle").onClick.Invoke();
            Assert.False(Visible("HomeDock"));
            r.Button("HomeDockToggle").onClick.Invoke();
            Assert.True(Visible("HomeDock"));
            Assert.NotNull(r.Button("HomeDockBeginPlacement"));

            string id = r.S.HomeLayout[0].Id;
            int count = r.S.HomeLayout.Count;
            int stored = r.S.FurnitureToPlace(false, id);
            Assert.AreEqual(0, stored);
            Assert.False(Visible("HomeDockPlace_" + id));
            StringAssert.DoesNotContain("已摆放", r.Text);
            StringAssert.DoesNotContain("收回", r.Text);
            Assert.True(r.S.StoreFurniture(false, 0));
            r.Call("RefreshHomeDock", false);
            Assert.AreEqual(count - 1, r.S.HomeLayout.Count);
            Assert.AreEqual(stored + 1, r.S.FurnitureToPlace(false, id));
            Assert.NotNull(r.Button("HomeDockPlace_" + id));
            Assert.False(r.S.Time.IsHeld);
            r.Button("HomeDockResidence").onClick.Invoke();
            StringAssert.Contains("我的租约与产权", r.Text);
            Assert.AreSame(r.S.Home, r.S.CurrentPlace);
            r.Ui.CloseTopWindow();
            GameSessionTests.Teleport(r.S, r.S.MainShop.Place.Id);
            Refresh();
            Assert.False(Visible("HomeDock"));
            Assert.False(Visible("HomeDockToggle"));
        }

        [Test]
        public void RestaurantStockShowsRiceInWarehouseAndFridgeDoesNotOfferToStoreIt()
        {
            using var r = new Rig();
            Assert.True(r.S.SetShopMainCategory("restaurant"));
            var shop = r.S.CurrentShop;
            if (!r.S.HasWarehouseHere) shop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopBox });
            if (!r.S.CanOpenFridge) shop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.FridgeSmall });
            PackTests.EmptyBag(r.S);
            r.S.Backpack.Food.RiceKg = 1;
            shop.Locker.Food.RiceKg = 121.5;
            shop.Fridge.Food.Ingredient.Add(new FoodBatch { Id = "tofu", Count = 1, ExpireDay = FoodBatch.NoExpiry });
            r.Ui.OpenDock();
            r.Ui.DebugSelectTab(1);
            var warehouseRice = r.Components<RectTransform>().Single(row => row.name == "WarehouseItem_rice");
            Assert.That(warehouseRice.GetComponentsInChildren<Text>().Any(label => label.text.Contains("121.5")), Is.True);
            Assert.That(r.Components<RectTransform>().Any(row => row.name == "FridgeItem_rice"), Is.False);

            r.Ui.OpenFridgeWindow();
            var fridge = r.Components<RectTransform>().Single(panel => panel.name.StartsWith("Window_冰箱 ·"));
            Assert.That(fridge.GetComponentsInChildren<Button>().Any(button =>
                button.GetComponentInChildren<Text>()?.text == "全部存入"), Is.False);
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
        public void OpeningFacilityTaskOnlyShowsRemainingItemsAndUsesOutlinedAction()
        {
            var layout = GameSessionTests.Layout();
            layout.places = layout.places.Append(new LayoutPlaceData
            {
                id = "mall", name = "宜居家具城", kind = "shop", node = "dl", business = GameSession.MallBusiness,
            }).ToArray();
            var session = new GameSession(CityLayout.FromData(layout), 7, Money.FromYuan(30000),
                GameTimestamp.From(new GameDate(0), 10 * 60), startWithShop: false);
            using var rig = new Rig(session);
            rig.Ui.QuestPopups = false;
            GameSessionTests.Teleport(session, "agency");
            Assert.True(session.SignRetailLease("pc", false, "restaurant"));
            rig.Ui.DebugRefreshQuests();
            var action = rig.Button("QuestPrimaryAction");
            Assert.AreEqual(Ui.FlatRow, action.image.color);
            Assert.AreEqual(Ui.FlatHotEdge, action.GetComponent<Outline>().effectColor);
            StringAssert.Contains("已备 0/4 项", rig.Ui.QuestTrackerText);

            session.DebugGiveFurniture(FurnitureCatalog.ShopCounter, 1);
            rig.Ui.OpenQuestWindow();
            var guide = rig.Components<Transform>().Single(item => item.name == "Window_当前任务");
            string guideText = string.Join("\n", guide.GetComponentsInChildren<Text>().Select(item => item.text));
            StringAssert.Contains("已备 1/4 项", guideText);
            StringAssert.Contains("餐馆炒锅灶台", guideText);
            StringAssert.DoesNotContain("收银台", guideText);
            rig.Ui.CloseTopWindow();

            foreach (string id in GameSession.OpeningFurniture("restaurant").Where(id => id != FurnitureCatalog.ShopCounter))
                session.DebugGiveFurniture(id, 1);
            session.CurrentShop.Layout.Add(new PlacedFurniture { Id = FurnitureCatalog.ShopCounter });
            rig.Ui.DebugRefreshQuests();
            Assert.AreEqual("place", session.CurrentQuest().Id);
            StringAssert.Contains("已摆 1/4 项", rig.Ui.QuestTrackerText);
            StringAssert.Contains("在店里摆放餐馆炒锅灶台", rig.Ui.QuestTrackerText);
            StringAssert.DoesNotContain("收银台", rig.Ui.QuestTrackerText);
            StringAssert.DoesNotContain("收银台", session.CurrentQuest().Hint);
        }

        [Test]
        public void FarmMarketOpensWholesaleAndAcceptsExactBagCounts()
        {
            using var r = new Rig(HomeTests.Make(5000000, 600));
            GameSessionTests.Teleport(r.S, "farm");
            r.Ui.OnEnteredPlace(r.S.CurrentPlace);
            const string key = "wholesale:produce:rice";
            var field = r.Components<InputField>().Single(f => f.name == "QuantityValue_" + key);
            Assert.That(field.contentType, Is.EqualTo(InputField.ContentType.IntegerNumber));
            var captions = r.Components<Text>().Select(text => text.text).ToArray();
            Assert.That(captions.Any(text => text.Contains("批发采购")), Is.True);
            Assert.That(captions.Any(text => text.Contains("切回零售")), Is.False);
            var cash = r.S.Ledger.Balance;

            field.onEndEdit.Invoke("5");
            Assert.That(r.S.CartAmount(key), Is.EqualTo(5));
            Assert.That(r.S.CartGoods, Is.EqualTo(Money.FromCents(975)));
            r.Button("QuantityPlus_" + key).onClick.Invoke();
            Assert.That(r.S.CartAmount(key), Is.EqualTo(6));
            r.Button("QuantityMinus_" + key).onClick.Invoke();
            Assert.That(r.S.CartAmount(key), Is.EqualTo(5));
            Assert.That(r.S.Ledger.Balance, Is.EqualTo(cash));

            var windows = (System.Collections.Generic.List<Window>)typeof(GameUi)
                .GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(r.Ui);
            var apply = typeof(Window).GetMethod("ApplyPendingRebuild", BindingFlags.Instance | BindingFlags.NonPublic);
            apply.Invoke(windows.Last(), null);
            Assert.AreSame(field, r.Components<InputField>().Single(f => f.name == "QuantityValue_" + key),
                "更新购物车时保留左侧商品输入框");
            Assert.That(r.Text, Does.Contain("货款 ¥9.75"));
            r.Button("QuantityMax_" + key).onClick.Invoke();
            Assert.AreEqual(r.S.CitySupplierRemaining("farm", "produce:rice"), r.S.CartAmount(key));
            field.onEndEdit.Invoke("5");

            string[] FishFields() => r.Components<InputField>()
                .Select(input => input.name).Where(name => name.StartsWith("QuantityValue_wholesale:produce:fish_"))
                .ToArray();
            var availableFish = FishFields();
            Assert.AreEqual(5, availableFish.Length);
            r.Button("ShopTab_鱼类").onClick.Invoke();
            CollectionAssert.AreEquivalent(availableFish, r.Components<InputField>().Select(input => input.name));
            Assert.AreEqual(5, r.S.CartAmount(key), "切换鱼类页签时保留已选的大米");
            r.Button("ShopTab_米与生鲜").onClick.Invoke();
            Assert.IsEmpty(FishFields());
            Assert.AreEqual("5", r.Components<InputField>().Single(input => input.name == "QuantityValue_" + key).text);

            r.Button("ShopTab_饮料").onClick.Invoke();
            string sodaKey = "wholesale:" + Gifts.Soda;
            var drinks = r.Components<InputField>();
            CollectionAssert.AreEquivalent(new[] { "QuantityValue_" + sodaKey, "QuantityValue_wholesale:" + Gifts.Drink },
                drinks.Select(input => input.name));
            drinks.Single(input => input.name == "QuantityValue_" + sodaKey).onEndEdit.Invoke("10");
            Assert.AreEqual(10, r.S.CartAmount(sodaKey));
            Assert.AreEqual(5, r.S.CartAmount(key));
            Assert.That(r.Text, Does.Contain("/瓶").And.Contain("/箱"));
            r.Button("ShopTab_米与生鲜").onClick.Invoke();

            r.S.Clock.Restore(r.S.Clock.Today, 23 * 60);
            r.Ui.RebuildWindows();
            field = r.Components<InputField>().Single(f => f.name == "QuantityValue_" + key);
            field.onEndEdit.Invoke("12");
            Assert.That(r.S.CartAmount(key), Is.EqualTo(5));
            Assert.NotNull(r.S.CheckoutBlocker(false));
        }

        [Test]
        public void OnlineOrdersUseTypedQuantitiesForPartsAndParcels()
        {
            using var r = new Rig();
            r.S.SetSmartphone(true);
            r.S.InstallApp(PhoneApps.Shop);
            r.Ui.OpenWholesaleApp();
            var delivery = r.Components<Dropdown>().Single(dropdown => dropdown.name == "OnlineDeliveryDestination");
            Assert.AreEqual(Ui.ButtonHot, delivery.targetGraphic.color);
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

        [TestCase(false)]
        [TestCase(true)]
        public void InsuranceTermsReturnToTheSameOfferAndPurchaseOnlyChargesOnce(bool renew)
        {
            using var r = new Rig();
            if (renew)
            {
                Assert.That(r.S.BuyHospitalPolicy(), Is.True);
                var nearExpiry = new GameTimestamp(r.S.HospitalPolicyUntil - 10 * 1440);
                r.S.Clock.Restore(nearExpiry.Date, nearExpiry.MinuteOfDay);
            }
            long oldExpiry = r.S.HospitalPolicyUntil;
            long now = r.S.Clock.Now.TotalMinutes;
            Money cash = r.S.Ledger.Balance;
            int premiums = r.S.Ledger.Entries.Count(entry => entry.Category == "住院保险");
            r.S.RaiseEvent("money_insurance_agent");
            var offer = r.S.PendingEvent;
            Assert.That(offer, Is.Not.Null);
            int terms = offer.Choices.FindIndex(choice => choice.Text == "查看条款");
            Assert.That(terms, Is.GreaterThanOrEqualTo(0));
            Assert.That(r.S.ResolveEvent(terms), Is.False, "查看条款不能结算事件");
            r.Ui.ShowEvent(offer);

            string purchase = renew ? "续保一年 · ¥3,000" : "投保一年 · ¥3,000";
            for (int i = 0; i < 2; i++)
            {
                r.ClickCaption("查看条款");
                StringAssert.Contains("报销80%", r.Text);
                StringAssert.DoesNotContain(purchase, r.Text);
                Assert.That(r.S.PendingEvent, Is.SameAs(offer));
                Assert.That(r.S.Ledger.Balance, Is.EqualTo(cash));
                Assert.That(r.S.HospitalPolicyUntil, Is.EqualTo(oldExpiry));
                Assert.That(r.S.Time.IsHeld, Is.True);
                r.ClickCaption("返回上一页");
                StringAssert.Contains(purchase, r.Text);
                StringAssert.Contains("暂不购买", r.Text);
                Assert.That(r.S.PendingEvent, Is.SameAs(offer));
            }

            r.ClickCaption(purchase);
            Assert.That(r.S.PendingEvent, Is.Null);
            Assert.That(r.S.Ledger.Balance, Is.EqualTo(cash - Money.FromYuan(3000)));
            Assert.That(r.S.Ledger.Entries.Count(entry => entry.Category == "住院保险"), Is.EqualTo(premiums + 1));
            Assert.That(r.S.HospitalPolicyUntil, Is.EqualTo(Math.Max(now, oldExpiry) + GameDate.DaysPerYear * 1440L));
            Assert.That(r.Ui.AnyModalOpen, Is.False);
        }

        [Test]
        public void WalletStationKeepsTimeRunningAndEnablesHandoffAfterWaiting()
        {
            using var r = new Rig();
            r.S.EventRaised += r.Ui.ShowEvent;
            r.S.CityPlaceEntered += r.Ui.OnEnteredPlace;
            r.S.RaiseEvent("wallet");
            r.ClickCaption("送到地铁站服务台");
            var station = r.S.WalletReturnStation;
            Assert.That(r.Ui.AnyModalOpen, Is.False);
            Assert.That(r.S.Trip?.TargetNode, Is.EqualTo(station.NodeId));
            Assert.That(r.S.CurrentPlace, Is.Not.EqualTo(station));
            for (int frame = 0; frame < 2000 && r.S.CurrentPlace != station; frame++) r.S.Tick(.05);
            Assert.That(r.S.CurrentPlace, Is.EqualTo(station));
            Assert.That(r.S.WalletWaitLeft, Is.EqualTo(30));
            r.S.Time.SetPaused(true);
            r.Ui.RebuildWindows();
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

        [TestCase(false)]
        [TestCase(true)]
        public void WalletTravelCanResumeFromEitherScheduleView(bool smartphone)
        {
            using var r = new Rig();
            r.S.SetSmartphone(smartphone);
            r.S.RaiseEvent("wallet");
            Assert.That(r.S.ResolveEvent(0), Is.True);
            var station = r.S.WalletReturnStation;
            r.S.StopTrip();
            Assert.That(r.S.CityIntent, Is.Null);
            r.Ui.OpenSchedulePanel();
            r.ClickCaption("前往地铁站");
            Assert.That(r.S.CityIntent?.TargetPlaceId, Is.EqualTo(station.Id));
            Assert.That(r.S.Trip?.TargetNode, Is.EqualTo(station.NodeId));
            Assert.That(r.S.CurrentPlace, Is.Not.EqualTo(station));
            Assert.That(r.S.WalletWaitLeft, Is.EqualTo(30));
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
