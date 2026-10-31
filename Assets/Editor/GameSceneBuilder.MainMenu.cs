using System;
using System.IO;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void MainMenuFromBatch() => RunSmokeBatch(SmokeMainMenu);

        private static void SmokeMainMenu()
        {
            bool hadLanguage = PlayerPrefs.HasKey(MenuLocalization.PreferenceKey);
            string oldLanguage = PlayerPrefs.GetString(MenuLocalization.PreferenceKey);
            string oldDirectory = SaveStore.DirectoryOverride;
            try
            {
                SaveStore.DirectoryOverride = Path.Combine(oldDirectory, "menu-" + Guid.NewGuid().ToString("N"));
                MenuLocalization.Current = GameLanguage.Chinese;
                PCShopSell.Game.UI.Ui.InvalidateFont();
                _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
                Directory.CreateDirectory(_outDir);
                ShaderUtil.allowAsyncCompilation = false;
                UrpSetup.Ensure();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = BuildRig();
                _game = new GameObject("Game").AddComponent<GameController>();
                _game.Bootstrap(true, 7);
                _game.Ui.QuestPopups = false;
                foreach (var canvas in _game.Ui.Canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1;
                }
                Check(_game.Ui.MainMenuOpen && _game.Session.Time.IsHeld, "真实启动进入主菜单，游戏时间暂停");
                long time = _game.Session.Clock.Now.TotalMinutes;
                Frame(2);
                Check(_game.Session.Clock.Now.TotalMinutes == time, "主菜单浏览不消耗经营时间");
                Check(GuideButton("加载存档") != null && !GuideButton("加载存档").interactable, "无存档时加载入口禁用");
                Shot("vs1_menu_01_zh");
                Shot("vs1_menu_02_zh_720", 1280, 720);
                ClickGuideButton("设置");
                Shot("vs1_menu_03_settings_zh");
                ClickGuideButton("English"); Frame(0);
                Check(MenuLocalization.Current == GameLanguage.English, "语言切换立即写入偏好");
                Check(GuideButton("Close") != null, "设置窗口关闭按钮同步英文");
                Shot("vs1_menu_04_settings_en");
                _game.Ui.CloseTopWindow();
                Check(_game.Ui.MainMenuOpen && _game.Session.Time.IsHeld, "关闭设置回主菜单，保留暂停");
                Shot("vs1_menu_05_en");
                ClickGuideButton("Help"); Shot("vs1_menu_06_help_en"); _game.Ui.CloseTopWindow();
                ClickGuideButton("About"); Shot("vs1_menu_07_about_en"); _game.Ui.CloseTopWindow();
                ClickGuideButton("Settings"); ClickGuideButton("日本語"); Frame(0);
                Shot("vs1_menu_08_settings_ja");
                _game.Ui.CloseTopWindow();
                Check(GuideButton("ゲーム開始") != null, "主菜单按钮即时更新为日文");
                Shot("vs1_menu_09_ja");
                Shot("vs1_menu_10_ja_720", 1280, 720);
                ClickGuideButton("ヘルプ"); Shot("vs1_menu_11_help_ja"); _game.Ui.CloseTopWindow();

                SaveStore.Save(_game.Session);
                SaveStore.Save(_game.Session, true);
                _game.Ui.RebuildWindows();
                ClickGuideButton("セーブデータをロード");
                Check(GuideButton("手動セーブをロード").interactable && GuideButton("オートセーブをロード").interactable,
                    "加载页面分别识别手动与自动存档");
                Shot("vs1_menu_12_load_ja");
                ClickGuideButton("オートセーブをロード");
                Check(!_game.Ui.MainMenuOpen && !_game.Session.Time.IsHeld, "加载成功进入游戏并释放菜单暂停");
                Check(MenuLocalization.Current == GameLanguage.Japanese, "读档不覆盖语言设置");
                _game.Ui.ShowMainMenu(true);
                ClickGuideButton("ゲームを再開");
                Check(!_game.Ui.MainMenuOpen && !_game.Session.Time.IsHeld, "主菜单可继续本次游戏");
                _game.Ui.ShowMainMenu(true);
                var previous = _game.Session;
                ClickGuideButton("ゲーム開始"); ClickGuideButton("キャンセル");
                Check(ReferenceEquals(previous, _game.Session), "取消新游戏保留当前进度");
                ClickGuideButton("ゲーム開始"); ClickGuideButton("ゲーム開始");
                Check(!ReferenceEquals(previous, _game.Session) && !_game.Ui.MainMenuOpen, "确认开始创建新局，不覆盖存档文件");
                Shot("vs1_menu_13_intro_ja");
                ClickGuideButton("経営を始める");
                _game.Session.EventsEnabled = false;
                Check(!_game.Session.Time.IsHeld, "开始经营后没有菜单暂停锁残留");
                _game.Ui.OpenSystemPanel();
                Shot("vs1_menu_14_pause_ja");
                ClickGuideButton("タイトルに戻る");
                ClickGuideButton("ゲームを終了");
                Shot("vs1_menu_15_quit_ja");
                ClickGuideButton("キャンセル");
                Check(_game.Ui.MainMenuOpen, "退出取消回到主菜单");

                _game.Ui.CloseAllWindows();
                SaveStore.DirectoryOverride = Path.Combine(oldDirectory, "menu-corrupt-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveStore.DirectoryOverride);
                File.WriteAllText(SaveStore.PathOf, "invalid json");
                _game.Ui.ShowMainMenu();
                _game.Ui.OpenLoadMenu(false);
                ClickGuideButton("手動セーブをロード");
                Check(_game.Ui.MainMenuOpen && _game.Session.Time.IsHeld, "存档损坏保留主菜单与当前会话");
                Shot("vs1_menu_16_load_error_ja");
                Check(File.ReadAllText(SaveStore.PathOf) == "invalid json", "加载失败不覆盖或删除损坏文件");
                Log("main menu three-language flow complete");
            }
            finally
            {
                SaveStore.DirectoryOverride = oldDirectory;
                if (hadLanguage) PlayerPrefs.SetString(MenuLocalization.PreferenceKey, oldLanguage);
                else PlayerPrefs.DeleteKey(MenuLocalization.PreferenceKey);
                PlayerPrefs.Save();
                PCShopSell.Game.UI.Ui.InvalidateFont();
            }
        }
    }
}
