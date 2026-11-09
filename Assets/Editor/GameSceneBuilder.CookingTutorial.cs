using System.IO;
using System.Linq;
using PCShopSell.Core.Home;
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
        // 画布尺寸在第一次渲染后才更新，所以先渲一次热身再存图，免得暗区位置按旧尺寸算。
        private static void Snap(string name) { Shot(name, 1920, 1080); Shot(name, 1920, 1080); }

        /// <summary>只截图：开局做饭强制引导（暗区 + 电饭锅高亮 + 步骤卡 + 锅内高亮按钮 + 出门被拦）。</summary>
        public static void CookingTutorialFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/cooking-tutorial"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            var ui = _game.Ui;
            Frame(0.05f); _game.SnapCamera();
            Check(s.OpeningMealTutorialPending && s.CurrentPlace == s.Home, "新游戏在家，做饭引导待完成");
            ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f);
            Check(ui.OpeningMealGuideVisible, "步骤卡 + 暗区可见");
            Snap("01-step1-dim-cooker");

            _game.LeaveHome(); Frame(0.05f);
            Check(s.CurrentPlace == s.Home, "没做饭，点出门被拦下");
            Snap("02-leave-blocked");

            ui.OpenCookerWindow(); Frame(0.05f); Snap("03-cooker-start");
            Check(s.CookRice(0.5, null), "开始焖饭"); ui.RebuildWindows(); Frame(0.05f); Snap("04-cooker-wait");
            ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f); Snap("05-step2-cooking");

            s.AdvanceClock(Pantry.CookMinutes); Frame(0.05f);
            Check(s.OpeningMealStep == 2, "饭好了进入第 3 步");
            Snap("06-step3-ready");
            ui.OpenCookerWindow(); Frame(0.05f); Snap("07-cooker-eat");
            Check(s.EatBowlAtHome(), "吃饭"); ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f);
            Check(!ui.OpeningMealGuideVisible && s.LeaveBlocker == null, "吃过饭后引导消失、可以出门");
            Snap("08-done");
            Log("COOKING TUTORIAL COMPLETE");
        });
    }
}
