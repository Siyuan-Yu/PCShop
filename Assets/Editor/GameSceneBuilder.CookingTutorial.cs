using System.IO;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
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
        // 画布尺寸在第一次渲染后才更新，所以先渲一次热身再存图，免得暗区位置按旧尺寸算。
        private static void Snap(string name) { Shot(name, 1920, 1080); Shot(name, 1920, 1080); }

        /// <summary>只截图：开局做饭强制引导（暗区 + 电饭锅高亮 + 步骤卡 + 锅内高亮按钮 + 出门被拦）。</summary>
        public static void CookingTutorialFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/cooking-tutorial"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            SaveStore.DirectoryOverride = Path.GetFullPath("work/cooking-tutorial-saves");
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            var ui = _game.Ui;
            Frame(0.05f); _game.SnapCamera();
            Check(s.OpeningMealTutorialPending && s.CurrentPlace == s.Home, "新游戏在家，做饭引导待完成");
            ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f);
            Snap("00-arrival-background");
            ClickCooking(null, "OpeningMealIntroNext"); Frame(0.05f);
            Check(ui.OpeningMealFocusTarget == "Movement", "背景后先教移动，不显示饥饿或煮饭");
            ui.OpenCookerWindow(); Check(!ui.AnyModalOpen, "移动前不能直接打开电饭锅跳过");
            Snap("00b-wasd-movement");
            s.Time.SetPaused(false);
            for (int i = 0; i < 30 && ui.OpeningMovementPending; i++) { _game.MoveInResidence(Vector3.left, .1f); Frame(.01f); }
            Check(!ui.OpeningMovementPending && ui.OpeningMealFocusTarget == "HungerStory", "人物实际移动后才出现饥饿故事");
            Snap("00c-after-walking-hungry");
            ClickCooking(null, "OpeningMealIntroNext"); Frame(.05f); s.Time.SetPaused(true);
            Check(!ui.OpeningMealPreludePending && ui.OpeningMealFocusTarget == "CookerWorld", "饥饿故事后开始电饭锅引导");
            var inputBlock = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).FirstOrDefault(b => b.name == "OpeningMealInputBlock");
            Check(inputBlock != null && inputBlock.image.raycastTarget, "教学遮罩拦截顶栏和底栏点击");
            inputBlock.onClick.Invoke();
            Check(ui.OpeningMealGuideVisible, "步骤卡 + 暗区可见");
            Snap("01-step1-dim-cooker");

            _game.LeaveHome(); Frame(0.05f);
            Check(s.CurrentPlace == s.Home, "没做饭，点出门被拦下");
            Snap("02-leave-blocked");

            AssertCookingOnly();
            ui.OpenSystemPanel();
            Check(!ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "先做饭"), "Esc 菜单正常开放");
            ui.OpenSettingsMenu();
            Check(!ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "先做饭"), "系统设置子窗口正常开放");
            ui.CloseAllWindows();
            ui.OpenCookerWindow(); Frame(0.05f); Snap("03-cooker-start");
            AssertCookingSpotlight("OpeningMealRice");
            ClickCooking("0.5 千克"); Frame(0.05f); Snap("03b-recipe-spotlight"); AssertCookingSpotlight("OpeningMealRecipe");
            ClickCooking("白饭"); Frame(0.05f); Snap("03c-start-spotlight"); AssertCookingSpotlight("OpeningMealStart");
            ClickCooking(null, "OpeningMealStart");
            Check(s.OpeningMealStep == 1, "通过实际按钮开始焖饭"); ui.RebuildWindows(); Frame(0.05f); Snap("04-cooker-wait");
            ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f); Snap("05-step2-cooking");

            AssertCookingOnly();
            ui.OpenCookerWindow(); ClickCooking(null, "OpeningMealWait"); ui.CloseAllWindows(); Frame(0.05f);
            Check(s.OpeningMealStep == 2, "饭好了进入第 3 步");
            AssertCookingOnly();
            Snap("06-step3-ready");
            ui.OpenCookerWindow(); Frame(0.05f); Snap("07-cooker-eat");
            AssertCookingSpotlight("OpeningMealEat");
            ClickCooking(null, "OpeningMealEat"); Frame(0.05f);
            Check(s.OpeningMealStep == 3 && s.OpeningMealTutorialPending, "吃一碗后仍需打包");
            Snap("07b-pack-spotlight"); AssertCookingSpotlight("OpeningMealPackAll");
            ClickCooking(null, "OpeningMealPackAll"); Frame(0.05f);
            Check(!s.OpeningMealTutorialPending && s.Pantry.CookedBowls == 0 && s.Backpack.Lunchboxes > 0, "全部装好后才完成");
            Check(ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text.Contains("放进背包的食物会在饭点或饿了时自动吃")), "自动吃饭说明弹窗可见");
            Snap("07c-auto-eat-message"); ui.CloseAllWindows(); Frame(0.05f); Frame(0.05f);
            Check(!ui.OpeningMealGuideVisible && s.LeaveBlocker == null, "吃过饭后引导消失、可以出门");
            Snap("08-done");
            ui.OpenBedWindow(); Check(ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "睡觉" || t.text.Contains("睡到")), "完成后床交互恢复");
            ui.CloseAllWindows();
            Log("COOKING TUTORIAL COMPLETE");
        });

        private static void AssertCookingSpotlight(string targetName)
        {
            var ui = _game.Ui;
            Check(ui.OpeningMealGuideVisible && ui.OpeningMealFocusTarget == targetName, "暗幕高亮跟随当前步骤 " + targetName);
            var canvas = ui.Canvases.First(c => c.name == "UI_Tutorial");
            Check(canvas.sortingOrder > 30, "教学覆盖锅内窗口");
            Check(!ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Transform>()).Any(t => t.name == "OpeningMealBanner"), "锅内没有嵌入教学文字");
            var root = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Transform>()).First(t => t.name == "Window_电饭锅");
            foreach (var b in root.GetComponentsInChildren<Button>())
                if (b.interactable) Check(b.transform.GetComponentsInParent<Transform>().Any(t => t.name == targetName), "只有高亮区域按钮可操作");
            var target = root.GetComponentsInChildren<RectTransform>().First(t => t.name == targetName);
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var modal = target.GetComponentInParent<Canvas>();
            var point = RectTransformUtility.WorldToScreenPoint(modal.worldCamera, (corners[0] + corners[2]) * .5f);
            var eventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            var data = new UnityEngine.EventSystems.PointerEventData(eventSystem) { position = point };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            // Edit-mode screenshot harness has no active input module; exercise the actual canvas raycasters directly.
            canvas.GetComponent<GraphicRaycaster>().Raycast(data, hits);
            Check(hits.Count == 0, "教学遮罩射线穿过高亮洞口");
            modal.GetComponent<GraphicRaycaster>().Raycast(data, hits);
            Check(hits.Any(hit => hit.gameObject.transform.IsChildOf(target)), "高亮区域实际控件接收射线");
            data.position = new Vector2(20, 20); hits.Clear(); canvas.GetComponent<GraphicRaycaster>().Raycast(data, hits);
            Check(hits.Any(hit => hit.gameObject.name.StartsWith("GuideDim")), "暗区实际拦截点击");
        }

        private static void ClickCooking(string label, string name = null)
        {
            var b = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).First(b => name != null ? b.name == name : b.GetComponentInChildren<Text>()?.text == label);
            Check(b.interactable, "教学按钮可用 " + (label ?? name)); b.onClick.Invoke();
        }
        private static void AssertCookingOnly()
        {
            var ui = _game.Ui; var s = _game.Session;
            long now = s.Clock.Now.TotalMinutes; var cash = s.Ledger.Balance; double energy = s.Player.Energy;
            foreach (var id in new[] { "bed", "wardrobe", "storage", "seat", "door", "repair", "fridge" })
            {
                _game.HomeInteract(id);
                Check(!ui.AnyModalOpen && s.Clock.Now.TotalMinutes == now && s.Player.Energy == energy && s.Ledger.Balance == cash && s.CurrentPlace == s.Home, "拦截屋内其他交互 " + id);
            }
            foreach (System.Action open in new System.Action[] { ui.OpenBedWindow, ui.OpenWardrobeWindow, ui.OpenStorageWindow, ui.OpenBusinessPanel, ui.ToggleBackpack })
            {
                open();
                Check(ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "先做饭"), "直接入口与快捷键统一提示先做饭");
                ui.CloseAllWindows();
            }
            foreach (var key in new[] { KeyCode.M, KeyCode.P, KeyCode.C, KeyCode.J, KeyCode.H, KeyCode.T })
            {
                Check(ui.TryHotkey(key) && !ui.AnyModalOpen, "快捷键仅提示先做饭 " + key);
            }
            Check(s.Clock.Now.TotalMinutes == now && s.Ledger.Balance == cash && s.Player.Energy == energy && s.CurrentPlace == s.Home, "被拦截的操作不改变资金时间位置或精力");
        }
    }
}
