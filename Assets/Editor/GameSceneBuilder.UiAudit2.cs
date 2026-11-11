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
        /// <summary>第二批界面一览：目标、经济简报、居家工作台、履历、证券等，继续审视冗长文字与无用信息。</summary>
        public static void UiAudit2FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-audit2"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Snap(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Call(string method) { var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null); if (m == null) { Log("NO METHOD " + method); return; } ui.CloseAllWindows(); m.Invoke(ui, null); Frame(0.05f); }

            foreach (var m in new[]
            {
                ("OpenGoalsPanel", "21-goals"), ("OpenEconomicReport", "22-report"), ("OpenHomeWorkbench", "23-workbench"),
                ("OpenCityStories", "24-stories"), ("OpenCityInvestments", "25-investments"), ("OpenRetailTypeChooser", "26-retail-kinds"),
                ("OpenDisplayWindow", "27-display"), ("OpenFleetPanel", "28-fleet"), ("OpenDistrictStory", "29-district-story"),
                ("OpenCityLife", "30-city-life"),
            })
            { Call(m.Item1); Snap(m.Item2); }
            ui.CloseAllWindows();
            Log("UI AUDIT 2 COMPLETE");
        });
    }
}
