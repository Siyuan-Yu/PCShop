using System.IO;
using System.Linq;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ExpoAcceptanceFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/expo-acceptance"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7);
            _game.Ui.QuestPopups = false; _game.Session.Time.SetPaused(true);
            long originalTime = _game.Session.Clock.Now.TotalMinutes, originalCash = _game.Session.Ledger.Balance.Cents;
            void Click(string name)
            {
                var button = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).Single(b => b.name == name);
                button.onClick.Invoke();
            }
            void Capture(string name)
            {
                foreach (var canvas in _game.Ui.Canvases)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
                Frame(0); _game.SnapCamera(); Shot(name, 1280, 900);
            }
            _game.Ui.OpenExpoAcceptance(); Capture("start-window");
            Click("CreateBrandExpoPreset");
            Check(_game.Session.IsBrandExpoAcceptance && _game.Session.DistrictAccessTier == 1, "实际按钮创建独立社区展会档");
            Capture("prepared-window");
            Click("OpenAcceptanceBrandExpo"); Capture("registration");
            var s = _game.Session;
            Check(s.RegisterBrandExpo("acceptance-expo", s.MainShop.Place.Id, "cpu0", "buyers"), "真实报名扣款交样");
            _game.Ui.CloseAllWindows(); _game.Ui.OpenExpoAcceptance(); Click("AdvanceAcceptanceExpo");
            Check(s.Clock.MinuteOfDay == 540 && s.BrandBusiness.Exhibits.Single().Stage == 0, "推进开幕但不代做接待");
            Capture("opening-window");
            Click("ReturnFromBrandExpoAcceptance");
            Check(!_game.Session.IsCityLifeTest && _game.Session.Clock.Now.TotalMinutes == originalTime && _game.Session.Ledger.Balance.Cents == originalCash,
                "返回原进度，时间与资金不被测试档改写");
            Log("EXPO ACCEPTANCE GRAPHICS COMPLETE");
        });
    }
}
