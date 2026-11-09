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
        /// <summary>只截图：在家打开居家工作台（大屏），四个页签 + 经营子页 + 生活托管。</summary>
        public static void HomeWorkbenchViewFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/home-workbench"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(300000); s.DebugSetHomeWorkbench(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Frame(0.05f);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Set(string f, int v) => t.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, v);
            Check(s.CanUseHomeWorkbench, "在家且接入大屏后可用");
            Frame(0.05f); _game.SnapCamera(); Shot("00-home-launcher", 1920, 1080);
            string[] names = { "overview", "business", "invest", "contacts" };
            for (int tab = 0; tab < 4; tab++)
            {
                ui.CloseAllWindows(); Set("_workbenchTab", tab); Set("_workbenchBusinessTab", 0);
                ui.OpenHomeWorkbench(); Frame(0); _game.SnapCamera(); Shot("0" + (tab + 1) + "-" + names[tab], 1920, 1080);
            }
            string[] sub = { "report", "shops", "staff", "orders", "assets" };
            for (int b = 1; b < 5; b++)
            {
                ui.CloseAllWindows(); Set("_workbenchTab", 1); Set("_workbenchBusinessTab", b);
                ui.OpenHomeWorkbench(); Frame(0); _game.SnapCamera(); Shot("business-" + sub[b], 1920, 1080);
            }
            ui.CloseAllWindows(); Set("_workbenchTab", 0); ui.OpenHomeWorkbench(); Frame(0);
            t.GetMethod("OpenHomeAutomation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null); Frame(0); _game.SnapCamera(); Shot("05-automation", 1920, 1080);
            Log("HOME WORKBENCH VIEW COMPLETE");
        });
    }
}