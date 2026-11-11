using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        /// <summary>第三批界面普查：日常高频窗口，逐个判断文字冗长、重复信息与空状态。</summary>
        public static void UiAudit3FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-audit3"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Snap(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Call(string method)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var m = t.GetMethod(method, flags, null, Type.EmptyTypes, null);
                object[] args = null;
                if (m == null)
                {
                    // 带可选参数的方法（如 OpenQuarterlyExpo(string id = null)）
                    m = t.GetMethods(flags).FirstOrDefault(x => x.Name == method && x.GetParameters().All(p => p.IsOptional));
                    if (m != null) args = new object[m.GetParameters().Length];
                }
                if (m == null) { Log("NO METHOD " + method); return; }
                ui.CloseAllWindows();
                try { m.Invoke(ui, args); Frame(0.05f); }
                catch (Exception e) { Log("FAIL " + method + " :: " + (e.InnerException ?? e).Message); }
            }

            foreach (var m in new[]
            {
                ("OpenReportWindow", "41-report"), ("OpenShopBuyers", "42-buyers"), ("OpenRelationsPanel", "43-relations"),
                ("OpenCityPeople", "44-people"), ("OpenCityMap", "45-map"), ("OpenCityWardrobe", "46-wardrobe"),
                ("OpenCityClimate", "47-climate"), ("OpenBenchWindow", "48-bench"), ("OpenIndustryNews", "49-news"),
                ("OpenCityModelCatalog", "50-models"), ("OpenMotorWorkshop", "51-vehicle"), ("OpenRacingDestinations", "52-racing"),
                ("OpenPetOrders", "53-pets"), ("OpenProcurementBudget", "54-budget"), ("OpenQuarterlyExpo", "55-expo-hall"),
                ("OpenExpoReception", "56-expo-signup"), ("OpenCityResidence", "57-residence"), ("OpenContactsPanel", "58-contacts"),
                ("OpenSpecialBusinessPanel", "59-special"), ("OpenStockFees", "60-stock-fees"), ("OpenMotorGarage", "61-garage"),
                ("OpenNoticeHistory", "62-notice"), ("OpenCityFinanceOverview", "63-finance"),
            })
            { Call(m.Item1); Snap(m.Item2); }
            ui.CloseAllWindows();
            Log("UI AUDIT 3 COMPLETE");
        });
    }
}
