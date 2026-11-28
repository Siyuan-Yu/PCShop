using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void UiAlignFromBatch() => RunSmokeBatch(() => CaptureAlignedUi(false));
        public static void MenuCoverFromBatch() => RunSmokeBatch(() => CaptureAlignedUi(true));

        private static void CaptureAlignedUi(bool menuOnly)
        {
            _outDir = Path.GetFullPath(Environment.GetEnvironmentVariable("PCSHOP_UI_ALIGN_CAPTURE") ?? "work/ui-align/capture");
            Directory.CreateDirectory(_outDir);
            SaveStore.DirectoryOverride = Path.Combine(Path.GetFullPath("work"), "ui-align-saves-" + Guid.NewGuid().ToString("N"));
            MenuLocalization.Current = GameLanguage.Chinese;
            Ui.InvalidateFont();
            ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 11);
            var session = _game.Session;
            var ui = _game.Ui;
            ui.QuestPopups = false;
            session.EventsEnabled = false;
            session.Time.SetPaused(true);
            foreach (var canvas in ui.Canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }

            ui.ShowMainMenu();
            Snap("menu-no-save");
            Shot("menu-no-save-720", 1280, 720);
            ui.CloseAllWindows();
            session.DebugAddCash(3000000);
            session.SetSmartphone(true);
            session.DebugRentStarterShop(true);
            Feed(session);
            var recordPrice = typeof(GameSession).GetMethod("RecordPriceDay", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int day = 1; day <= 3; day++)
            {
                session.Clock.Restore(new GameDate(day), 8 * 60);
                recordPrice.Invoke(session, new object[] { false });
            }
            session.Ledger.Credit(session.Clock.Today, "门店零售", Money.FromYuan(1260), "电脑配件");
            session.Ledger.TryDebit(session.Clock.Today, "进货", Money.FromYuan(480), "电脑配件");
            Frame(0);
            SaveStore.Save(session);
            ui.ShowMainMenu();
            Snap("menu-save");
            Shot("menu-save-720", 1280, 720);
            Capture("menu-load", () => ui.OpenLoadMenu(false), keepMain: true);
            Capture("menu-settings", ui.OpenSettingsMenu, keepMain: true);
            Capture("menu-help", ui.OpenHelpMenu, keepMain: true);
            Capture("menu-about", ui.OpenAboutMenu, keepMain: true);
            Capture("menu-quit", ui.OpenQuitMenu, keepMain: true);
            if (menuOnly) return;
            ui.CloseAllWindows();
            session.Leave();
            Frame(0);
            ui.DebugRefreshQuests();
            for (int step = 0; step < 4; step++) Frame(.12f);
            Snap("00-hud-street");
            Capture("01-phone", ui.OpenPhone);
            Capture("03-backpack", ui.OpenBackpackPanel);
            Capture("04-my-panel", ui.OpenMyPanel);
            Capture("05-relations", ui.OpenRelationsPanel);
            Capture("07-quests", ui.OpenQuests);
            Capture("10-schedule", ui.OpenSchedulePanel);
            Capture("14-market-prices", ui.OpenMarketPanel);
            Capture("18-brand", () => Call("OpenBrandOverview"));
            Capture("20-finance", ui.OpenCityFinanceOverview);
            Capture("22-ledger", () => Call("OpenLedger"));
            Capture("23-procurement", ui.OpenCityProcurement);
            Capture("41-employees", ui.OpenEmployeesPanel);
            Capture("42-fleet", ui.OpenFleetPanel);
            session.Backpack.AddFurniture(FurnitureCatalog.FridgeSmall, 1);
            Capture("43-truck", ui.OpenTruckWindow);
            Capture("60-system-menu", ui.OpenSystemPanel);
            Capture("61-settings", ui.OpenSettingsMenu);
            Capture("62-save-menu", ui.OpenSaveMenu);
            Capture("63-help", ui.OpenHelpMenu);
            Capture("65-destination", () => Call("OpenCustomDestinationPicker"));
            Capture("68-notice", ui.OpenNoticeHistory);

            ui.CloseAllWindows();
            var shop = session.MyShops.FirstOrDefault();
            if (shop != null)
            {
                Teleport(session, shop.Place.Id);
                Frame(0);
                session.Clock.Restore(new GameDate(Math.Max(session.Clock.Today.TotalDays, session.PlayerShop.Settings.BuildOrdersStartDay)),
                    session.Clock.MinuteOfDay);
                var now = session.Clock.Now;
                session.PlayerShop.RestoreOrders(new[]
                {
                    new BuildOrder(1, 1, session.PlayerShop.BuildPrice(1), now, now.AddMinutes(1440))
                }, 2);
                ui.OpenDock();
                ShopTab("30-shop-counter", 0);
                ShopTab("31-shop-orders", 3);
                ShopTab("32-shop-renovate", 4);
                ShopTab("33-shop-staff", 5);
                Capture("36-shop-management", ui.OpenStoreManagement);
                Capture("37-shop-display", ui.OpenDisplayWindow);
                Capture("38-shop-report", ui.OpenReportWindow);
                var shelves = session.ShelfIndexes();
                if (shelves.Count > 0) Capture("39-shop-shelf", () => ui.OpenShelfWindow(shelves[0]));
                session.PlayerShop.RestoreOrders(Array.Empty<BuildOrder>(), 2);
                Capture("56-home-bench", ui.OpenBenchWindow);
            }

            ui.CloseAllWindows();
            Teleport(session, session.Home.Id);
            session.DebugSetHomeWorkbench(true);
            string fridgeId = FurnitureCatalog.FridgeSmall;
            session.HomeBox.AddFurniture(fridgeId, 1);
            var room = session.CurrentResidenceRoom;
            var fridge = FurnitureCatalog.Get(fridgeId);
            var fridges = from x in Enumerable.Range(1, (int)(room.Width / RoomPlanner.Grid))
                          from z in Enumerable.Range(1, (int)(room.Depth / RoomPlanner.Grid))
                          let candidate = RoomPlanner.Snap(room, fridge, x * RoomPlanner.Grid, z * RoomPlanner.Grid, 0)
                          where session.PlacementError(false, fridge, candidate, -1) == null
                          select candidate;
            var fridgePosition = fridges.FirstOrDefault();
            if (fridgePosition != null) session.PlaceFromStock(false, fridgeId, fridgePosition);
            Frame(0);
            Capture("50-home-bed", ui.OpenBedWindow);
            Capture("51-home-cooker", ui.OpenCookerWindow);
            Capture("52-home-fridge", ui.OpenFridgeWindow);
            Capture("53-home-workbench", ui.OpenHomeWorkbench);
            Capture("54-home-wardrobe", ui.OpenWardrobeWindow);
            Capture("55-home-repair", ui.OpenRepairWindow);
            Capture("stock", ui.OpenCityInvestments);
            CapturePlace("80-dealer", PlaceKind.Dealer);
            CapturePlace("81-garage", PlaceKind.Garage);
            CapturePlace("82-station", PlaceKind.Station);
            ui.CloseAllWindows();
            session.Leave();
            OpenGardenForStockPreview(session);
            Capture("18-brand-unlocked", () => Call("OpenBrandOverview"));

            void Call(string method) => typeof(GameUi).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).Invoke(ui, null);

            void Capture(string name, Action open, bool keepMain = false)
            {
                if (keepMain)
                {
                    if (!ui.MainMenuOpen) ui.ShowMainMenu();
                    else if (name != "menu-load") ui.CloseTopWindow();
                }
                else ui.CloseModalWindows();
                open();
                Snap(name);
            }

            void ShopTab(string name, int index)
            {
                ui.CloseModalWindows();
                ui.OpenDock();
                ui.DebugSelectTab(index);
                Snap(name);
            }

            void CapturePlace(string name, PlaceKind kind)
            {
                var place = session.City.Places.FirstOrDefault(p => p.Kind == kind);
                if (place == null) return;
                ui.CloseAllWindows();
                Teleport(session, place.Id);
                ui.OnEnteredPlace(place);
                Snap(name);
            }

            void Snap(string name)
            {
                Frame(.12f);
                _game.SnapCamera();
                PrepareStockPreviewLayout();
                Shot(name);
            }
        }
    }
}
