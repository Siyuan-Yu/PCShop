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
        /// <summary>只截图：平面皮肤下常见窗口和 HUD 一览，用来审视哪些界面还能简化。</summary>
        public static void UiAuditFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-audit"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Snap2(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Call(string method) { var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null); if (m == null) { Log("NO METHOD " + method); return; } ui.CloseAllWindows(); m.Invoke(ui, null); Frame(0.05f); }

            var shop = s.MyShops.FirstOrDefault();
            if (shop != null) { Teleport(s, shop.Place.Id); Frame(0.1f); Snap2("01-shop-interior"); }
            ui.CloseAllWindows(); s.Leave(); Frame(0.1f); Snap2("02-street");
            foreach (var m in new[]
            {
                ("OpenBackpackPanel", "03-backpack"), ("OpenEmployeesPanel", "04-employees"), ("OpenMyPanel", "05-my"), ("OpenBusinessPanel", "06-business"),
                ("OpenQuests", "07-quests"), ("OpenPhone", "08-phone"), ("OpenLedger", "09-ledger"), ("OpenCityProcurement", "10-procurement"),
                ("OpenCityProperties", "11-properties"), ("OpenBrandOverview", "12-brand"), ("OpenSchedulePanel", "13-calendar"), ("OpenChatPanel", "14-chat"),
            })
            { Call(m.Item1); Snap2(m.Item2); }
            ui.CloseAllWindows();
            (typeof(GameUi).GetField("_hudMore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui) as GameObject)?.SetActive(true); s.Leave(); Frame(0.1f); Snap2("15-hud-more");
            Log("UI AUDIT COMPLETE");
        });
    }
}
