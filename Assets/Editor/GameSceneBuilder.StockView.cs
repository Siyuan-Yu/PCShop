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
        public static void StockViewFromBatch() => RunSmokeBatch(CaptureStockViews);

        private static void CaptureStockViews()
        {
            _outDir = Path.GetFullPath("docs/previews/stock-view"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(2000000); s.SetSmartphone(true); s.DebugSetHomeWorkbench(true);
            s.DebugRentStarterShop(false);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            for (int i = 0; i < 12; i++) { Feed(s); s.AdvanceClock(24 * 60); } // 真实报价历史
            while (!s.StockMarketOpen) s.AdvanceClock(1440);
            if (s.Clock.MinuteOfDay < 600) s.AdvanceClock(600-s.Clock.MinuteOfDay);
            Frame(0.05f);
            int lot = GameSession.StockLotSize;
            var ids = CityInvestments.All.Select(d => d.Id).ToArray();
            Log("TRADE " + s.TradeCityInvestment(ids[0], 3 * lot, true, true) + " " + s.TradeCityInvestment(ids[3], 2 * lot, true, true) + " " + s.TradeCityInvestment(ids[9], lot, true, true));
            Log("SHORT " + s.OpenStockShort(ids[5], lot, true));
            var ui = _game.Ui; var t = typeof(GameUi);
            void Set(string f, object v) => t.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, v);
            void Page(int p) { var e = t.GetNestedType("StockPage", BindingFlags.NonPublic); Set("_stockPage", System.Enum.ToObject(e, p)); }
            void Shot2(string name) { Frame(0); _game.SnapCamera(); PrepareStockPreviewLayout(); Shot(name, 1920, 1080); }
            ui.OpenCityInvestments(); Page(1); Set("_stockShortMode", false); Set("_stockExpanded", null); ui.OpenCityInvestments(); Shot2("01-exchange-spot");
            Set("_stockExpanded", ids[0]); ui.OpenCityInvestments();
            ((System.Collections.Generic.Dictionary<string,long>)t.GetField("_stockSpotDraft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui))[ids[0]]=200;
            ((System.Collections.Generic.Dictionary<string,long>)t.GetField("_stockSpotDraft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui))[ids[9]]=-lot;
            ui.RebuildWindows(); Shot2("02-exchange-expanded");
            Set("_stockCandles", true); ui.RebuildWindows(); Shot2("03-expanded-candles");
            Set("_stockCandles", false); Set("_stockExpanded", ids[5]); Set("_stockShortMode", true); ui.RebuildWindows(); Shot2("04-exchange-short");
            Set("_stockShortMode", false); Page(0); ui.RebuildWindows(); Shot2("05-charts");
            Page(2); ui.RebuildWindows(); Shot2("06-news");
            Page(1);ui.RebuildWindows();
            var stockWindow=(Window)t.GetField("_stockWindow",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            t.GetMethod("OpenStockOrderTools",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{stockWindow,ids[0],false});Shot2("13-quick-order");ui.CloseTopWindow();
            t.GetMethod("OpenStockMargin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{stockWindow,ids[5]});Shot2("14-margin");ui.CloseTopWindow();
            t.GetMethod("OpenStockFees",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);Shot2("15-fees");ui.CloseTopWindow();
            t.GetMethod("OpenStockTradeHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{ids[0],s.StockExecutions(ids[0],0,false)});Shot2("16-executions");ui.CloseTopWindow();
            Teleport(s,s.City.Places.First(p=>p.Business==GameSession.BankBusiness).Id);DrainStockPreviewNotices();
            t.GetMethod("OpenBankSecurities",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);Shot2("17-bank-exchange");
            ui.CloseAllWindows(); Set("_stockExpanded", null); Page(1);
            Teleport(s, s.Home.Id);
            DrainStockPreviewNotices();
            void Tab(int n) { t.GetField("_workbenchTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n); }
            Tab(2); ui.OpenHomeWorkbench(); Shot2("07-workbench-invest");
            Set("_stockExpanded", ids[0]); ui.RebuildWindows(); Shot2("08-workbench-invest-expanded");
            Set("_stockExpanded", null); Page(0); ui.RebuildWindows(); Shot2("09-workbench-invest-chart");
            Page(2); ui.RebuildWindows(); Shot2("10-workbench-invest-news"); Page(1);
            Tab(0); ui.RebuildWindows(); Shot2("11-workbench-overview");
            Tab(1); ui.RebuildWindows(); Shot2("12-workbench-business");
            Log("STOCK VIEW COMPLETE");
        }

        /// <summary>只截图：居家工作台总览（今日待办）与经营各页（街区分组、逐店展开），带多店、借款、做空的固定场景。</summary>
        public static void WorkbenchPagesFromBatch() => RunSmokeBatch(CaptureWorkbenchPages);

        public static void StockAndWorkbenchFromBatch() => RunSmokeBatch(() => { CaptureStockViews(); CaptureWorkbenchPages(); });

        private static void CaptureWorkbenchPages()
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
            for (int i = 0; i < 6; i++) { Feed(s); s.AdvanceClock(24 * 60); }
            while (!s.StockMarketOpen) s.AdvanceClock(1440);
            if(s.Clock.MinuteOfDay<600)s.AdvanceClock(600-s.Clock.MinuteOfDay);
            int lot = GameSession.StockLotSize; var ids = CityInvestments.All.Select(d => d.Id).ToArray();
            Log("TRADE " + s.TradeCityInvestment(ids[0], 3 * lot, true, true) + " SHORT " + s.OpenStockShort(ids[5], lot, true));
            Log("LOAN " + BorrowForPreview(s, 50000, 1));
            s.DebugSetHomeWorkbench(true); Teleport(s, s.Home.Id);
            DrainStockPreviewNotices();
            var ui = _game.Ui; var t = typeof(GameUi);
            void Tab(int n) => t.GetField("_workbenchTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n);
            void Biz(int n) => t.GetField("_workbenchBusinessTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, n);
            void Shot2(string name) { Frame(0); _game.SnapCamera(); PrepareStockPreviewLayout(); Shot(name, 1920, 1080); }
            Tab(0); ui.OpenHomeWorkbench(); Shot2("01-overview");
            Tab(1); t.GetField("_workbenchShop",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui, ""); ui.RebuildWindows(); Shot2("02-shops-overview");
            t.GetField("_workbenchShop",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,s.MyShops[0].Place.Id);ui.RebuildWindows();Shot2("02-shop-detail");
            { Tab(1); Biz(0); t.GetField("_workbenchPeriod", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, 2); t.GetField("_workbenchReportShop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, s.MyShops[0].Place.Id);
              { var id0 = s.MyShops[0].Place.Id; var today = s.Clock.Today; var attr = PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "销售");
                s.Ledger.Credit(today, "零售", Money.FromYuan(1280), "柜台销售 · 猫粮 ×8", attr); s.Ledger.Credit(today, "零售", Money.FromYuan(560), "柜台销售 · 猫砂 ×4", attr);
                s.Ledger.TryDebit(today, "进货", Money.FromYuan(900), "批发市场进货", PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "进货"));
                s.Ledger.TryDebit(today, "工资", Money.FromYuan(240), "店员日薪", PCShopSell.Core.Finance.LedgerAttribution.ForShop(id0, null, "工资")); }
              Log("LEDGER shop entries " + s.Ledger.Entries.Count(e => e.ShopId != null) + "/" + s.Ledger.Entries.Count); t.GetField("_workbenchShop",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui, "");ui.RebuildWindows(); Shot2("02-shops-overview-ledger"); }
            Tab(2); ui.RebuildWindows(); Shot2("02-invest");
            t.GetField("_workbenchPeriod", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui,1);
            Tab(3); ui.RebuildWindows(); Shot2("03-finance");
            Tab(4); ui.RebuildWindows(); Shot2("04-contacts");
            Log("WORKBENCH PAGES COMPLETE");
        }

        private static bool BorrowForPreview(GameSession session, long yuan, int months, bool atBank = false)
        {
            var application = session.BeginLoanApplication(yuan, months, atBank);
            if (application == null) return false;
            while (application.NextGesture is LoanGesture gesture)
                session.PerformLoanGesture(application, gesture);
            return session.BorrowAppLoan(application);
        }

        private static void DrainStockPreviewNotices()
        {
            // 快进产生的缴租通知先逐一看完，避免覆盖本次要复拍的界面。
            for(int i=0;i<24;i++){_game.Ui.CloseAllWindows();Frame(0);}
            _game.Ui.CloseAllWindows();
        }

        private static void PrepareStockPreviewLayout()
        {
            // EditMode 不运行 LateUpdate，显式执行正式列表的显隐刷新，和游玩时一致。
            Canvas.ForceUpdateCanvases();
            foreach(var scroll in _game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.ScrollRect>()))
                scroll.GetType().GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(scroll,null);
            Canvas.ForceUpdateCanvases();
        }
    }
}
