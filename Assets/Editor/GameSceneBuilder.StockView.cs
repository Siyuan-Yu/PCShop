using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        /// <summary>只截图：真实证券窗口各页 + 居家工作台投资页（同一状态下对比）。</summary>
        public static void StockViewFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/stock-view"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(2000000); s.SetSmartphone(true); s.DebugSetHomeWorkbench(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            for (int i = 0; i < 12; i++) s.AdvanceClock(24 * 60);   // 攒出几天报价历史
            Frame(0.05f);
            int lot = GameSession.StockLotSize;
            var ids = CityInvestments.All.Select(d => d.Id).ToArray();
            Log("TRADE " + s.TradeCityInvestment(ids[0], 3 * lot, true, true) + " " + s.TradeCityInvestment(ids[3], 2 * lot, true, true) + " " + s.TradeCityInvestment(ids[9], lot, true, true));
            Log("SHORT " + s.OpenStockShort(ids[5], lot, true));
            var ui = _game.Ui; var t = typeof(GameUi);
            void Set(string f, object v) => t.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, v);
            void Page(int p) { var e = t.GetNestedType("StockPage", BindingFlags.NonPublic); Set("_stockPage", System.Enum.ToObject(e, p)); }
            void Shot2(string name) { Frame(0); _game.SnapCamera(); _game.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()?.GetType(); var extra = _game.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (extra != null) extra.renderPostProcessing = false; Shot(name, 1920, 1080); }
            ui.OpenCityInvestments(); Page(1); Set("_stockShortMode", false); Set("_stockExpanded", null); ui.OpenCityInvestments(); Shot2("01-exchange-spot");
            Set("_stockExpanded", ids[0]); ui.OpenCityInvestments(); Shot2("02-exchange-expanded");
            Set("_stockCandles", true); ui.OpenCityInvestments(); Shot2("03-expanded-candles");
            Set("_stockCandles", false); Set("_stockExpanded", null); Set("_stockShortMode", true); ui.OpenCityInvestments(); Shot2("04-exchange-short");
            Set("_stockShortMode", false); Page(0); ui.OpenCityInvestments(); Shot2("05-charts");
            Page(2); ui.OpenCityInvestments(); Shot2("06-news");
            ui.CloseAllWindows(); Set("_stockExpanded", null); Page(1);
            void Tab(int n) { t.GetField("_workbenchTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n); }
            Tab(2); ui.OpenHomeWorkbench(); Shot2("07-workbench-invest");
            Set("_stockExpanded", ids[0]); ui.RebuildWindows(); Shot2("08-workbench-invest-expanded");
            Set("_stockExpanded", null); Page(0); ui.RebuildWindows(); Shot2("09-workbench-invest-chart");
            Page(2); ui.RebuildWindows(); Shot2("10-workbench-invest-news"); Page(1);
            Tab(0); ui.RebuildWindows(); Shot2("11-workbench-overview");
            Tab(1); ui.RebuildWindows(); Shot2("12-workbench-business");
            Log("STOCK VIEW COMPLETE");
        });

        /// <summary>只截图：居家工作台总览（今日待办）与经营各页（街区分组、逐店展开），带多店、借款、做空的固定场景。</summary>
        public static void WorkbenchPagesFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/workbench-pages"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(false));
            Teleport(s, "agency");
            foreach (var listing in s.Listings.Where(l => l.OpenDay <= s.Clock.Today.TotalDays && l.PlaceId.StartsWith("shop")).GroupBy(l => s.City.GetPlace(l.PlaceId).StreetId).Select(g => g.First()).Take(3).ToArray())
                Log("LEASE " + listing.PlaceId + " " + s.SignLease(listing.PlaceId, true));
            for (int i = 0; i < 6; i++) s.AdvanceClock(24 * 60);
            int lot = GameSession.StockLotSize; var ids = CityInvestments.All.Select(d => d.Id).ToArray();
            Log("TRADE " + s.TradeCityInvestment(ids[0], 3 * lot, true, true) + " SHORT " + s.OpenStockShort(ids[5], lot, true));
            Log("LOAN " + s.BorrowAppLoan(50000, 1));
            s.DebugSetHomeWorkbench(true); Teleport(s, s.Home.Id);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Tab(int n) => t.GetField("_workbenchTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n);
            void Biz(int n) => t.GetField("_workbenchBusinessTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n);
            void Shot2(string name) { Frame(0); _game.SnapCamera(); _game.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()?.GetType(); var extra = _game.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (extra != null) extra.renderPostProcessing = false; Shot(name, 1920, 1080); }
            Tab(0); ui.OpenHomeWorkbench(); Shot2("01-overview");
            Tab(1); for (int i = 0; i < 5; i++) { Biz(i); ui.RebuildWindows(); Shot2("02-business-" + i); }
            { var stocked = s.MyShops.FirstOrDefault(x => x.Sim.Stock.Lines().Any() || x.Sim.Stock.GoodsLines().Any()); if (stocked != null) { t.GetField("_workbenchShop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, stocked.Place.Id); Biz(1); ui.RebuildWindows(); Shot2("02-business-1b"); } }
            { Tab(1); Biz(0); t.GetField("_workbenchPeriod", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, 2); t.GetField("_workbenchReportShop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, s.MyShops[0].Place.Id);
              { var id0 = s.MyShops[0].Place.Id; var today = s.Clock.Today; var attr = PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "销售");
                s.Ledger.Credit(today, "零售", Money.FromYuan(1280), "柜台销售 · 猫粮 ×8", attr); s.Ledger.Credit(today, "零售", Money.FromYuan(560), "柜台销售 · 猫砂 ×4", attr);
                s.Ledger.TryDebit(today, "进货", Money.FromYuan(900), "批发市场进货", PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "进货"));
                s.Ledger.TryDebit(today, "工资", Money.FromYuan(240), "店员日薪", PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "工资")); }
              Log("LEDGER shop entries " + s.Ledger.Entries.Count(e => e.ShopId != null) + "/" + s.Ledger.Entries.Count); ui.RebuildWindows(); Shot2("02-business-0b"); }
            Tab(2); ui.RebuildWindows(); Shot2("02-invest");
            Tab(3); ui.RebuildWindows(); Shot2("03-contacts");
            Log("WORKBENCH PAGES COMPLETE");
        });
    }
}