using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void UiCleanupFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-cleanup"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.DebugAddCash(5000000);
            Check(s.DebugRentStarterShop(computerScenario: true), "准备主店"); s.Clock.Restore(new GameDate(15), 600); s.PlayerShop.Reputation = 100;
            s.Time.SetPaused(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            var main = s.MainShop.Place;
            var second = s.City.OfKind(PlaceKind.Shop).First(p => p != main && !s.IsDedicatedVenue(p) && s.AgencyFor(p) == s.AgencyFor(main));
            s.DebugAddListing(second.Id, 15, 60, Money.FromYuan(1000)); Teleport(s, s.AgencyFor(second).Id);
            Check(s.SignRetailLease(second.Id, true, "computer"), "准备第二家自营店"); Teleport(s, second.Id); Frame(0);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenBusinessPanel(); Frame(0);
            Shot("business_workspace"); Shot("business_workspace_720", 1280, 720);
            _game.Ui.OpenCityProcurement(); Frame(0);
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Single(t => t.name == "ProcurementDestination").text.Contains(s.ShopTitle(s.CurrentShop)), "采购默认当前分店");
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Transform>()).Count(t => t.name == "Workspace_business") == 1, "经营采购只用一个主窗口");
            Shot("branch_procurement"); Shot("branch_procurement_720", 1280, 720);
            _game.Ui.OpenCityProperty(second); Frame(0); Shot("selected_property_720", 1280, 720);
            _game.Ui.CloseAllWindows(); Teleport(s, main.Id);
            Check(s.SetShopMainCategory("restaurant"), "主店转为餐馆");
            var shop = s.CurrentShop; shop.Layout.Clear();
            string[] kitchen = { "restaurant_prep", "restaurant_stove", "restaurant_pass", FurnitureCatalog.Fridge };
            for (int i = 0; i < kitchen.Length; i++) shop.Layout.Add(new PlacedFurniture { Id = kitchen[i], X = 1 + i * 1.25, Z = 4.8 });
            shop.Layout.Add(new PlacedFurniture { Id = "restaurant_table", X = 2, Z = 2 });
            shop.Layout.Add(new PlacedFurniture { Id = "restaurant_table", X = 4, Z = 2 });
            shop.Fridge.Food.RiceKg = 1; shop.Fridge.Food.AddIngredient("veg", 3);
            var state = s.SpecialBusiness(main.Id); state.NextVisit = long.MaxValue;
            state.Orders.Add(new DiningOrder { Id = state.NextId++, Recipe = 0, Table = 0, Stage = DiningStage.Cooking, ReadyAt = s.Clock.Now.TotalMinutes, BurnAt = s.Clock.Now.TotalMinutes + 5, Arrived = s.Clock.Now.TotalMinutes - 8, Price = 18 });
            state.Orders.Add(new DiningOrder { Id = state.NextId++, Recipe = 1, Table = 1, Stage = DiningStage.Waiting, Arrived = s.Clock.Now.TotalMinutes, Price = 22 });
            _game.Ui.OnEnteredPlace(s.CurrentPlace); _game.Ui.OpenDock(); _game.Ui.DebugSelectTab(0); Frame(0);
            Shot("restaurant_counter"); Shot("restaurant_counter_720", 1280, 720);
            _game.Ui.DebugSelectTab(2); Frame(0);
            Check(!_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).Any(b => b.name == "ChillKitchenIngredients"), "定价页没有柜台操作");
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).Any(b => b.name == "SaveMenuMarkup"), "菜单价格需要确认保存");
            Shot("restaurant_pricing"); Shot("restaurant_pricing_720", 1280, 720);
            _game.Ui.DebugSelectTab(3); Frame(0); Shot("restaurant_records_720", 1280, 720);
            state.Orders.Clear(); Check(s.SetShopMainCategory("secondhand"), "主店转为二手店");
            state.Used.Add(new UsedItem { Id = state.NextId++, Product = 1, Owned = true, Inspected = true, Condition = UsedCondition.Good, Cost = 70, Price = 130 });
            _game.Ui.DebugSelectTab(2); Frame(0); Shot("used_pricing_720", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenMyPanel(); Frame(0); Shot("personal_overview_720", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenSchedulePanel(); Frame(0); Shot("unified_calendar_720", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenMarketPanel(); Frame(0); Shot("market_workspace_720", 1280, 720);
            _game.Ui.CloseAllWindows(); Teleport(s, s.AgencyFor(main).Id); _game.Ui.OnEnteredPlace(s.CurrentPlace);
            typeof(GameUi).GetField("_agencySelectedId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_game.Ui, main.Id);
            _game.Ui.RebuildWindows(); Frame(0); Shot("agency_reduced_labels_720", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenContactsPanel(); Frame(0); Shot("people_workspace_720",1280,720);
            _game.Ui.CloseAllWindows();
            typeof(GameUi).GetMethod("OpenCityResidence",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_game.Ui,null);
            var cash=s.Ledger.Balance;
            _game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.GetComponentInChildren<Text>()?.text=="改善住宅…").onClick.Invoke();
            Check(s.Ledger.Balance==cash,"搬家预览不扣款");Frame(0);Shot("housing_cost_preview_720",1280,720);
            Log("UI CLEANUP GRAPHICS COMPLETE");
        });
    }
}
