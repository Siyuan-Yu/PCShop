using System;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _mainMenu;
        private bool _canResumeMainMenu;
        public bool MainMenuOpen => _mainMenu != null && !_mainMenu.Closed;
        private static string L(string key) => MenuLocalization.Get(key);

        private void MenuTitle(Window window, string key)
        {
            window.TitleText.text = L(key);
            window.TitleText.font = Ui.Font;
            var close = window.TitleText.transform.parent.GetComponentInChildren<Button>();
            if (close != null)
            {
                close.GetComponentInChildren<Text>().text = L("close");
                close.GetComponentInChildren<Text>().font = Ui.Font;
            }
        }
        private void SetGameplayUiVisible(bool visible)
        {
            foreach (var canvas in new[] { _hudCanvas, _labelCanvas, _dockCanvas, _toastCanvas, _tipCanvas })
                if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        public void ShowMainMenu(bool canResume = false)
        {
            CloseAllWindows();
            _canResumeMainMenu = canResume;
            SetGameplayUiVisible(false);
            _mainMenu = OpenWindow(L("game_title"), 1320, 850, BuildMainMenu, true, false, () =>
            {
                _mainMenu = null;
                SetGameplayUiVisible(true);
            }, allowDuringOpeningMeal: true);
        }

        private void BuildMainMenu(Window w)
        {
            MenuTitle(w, "game_title");
            var body = Ui.Row(w.Content, 680, 36);
            var hero = Ui.Rect(body, "MainMenuHero");
            Ui.Size(hero, -1, 680, 1);
            Ui.VList(hero, 20, 18);
            Ui.Size(Ui.Label(hero, "PC SHOP", 60, TextAnchor.MiddleLeft, Ui.Accent), -1, 105);
            Ui.Size(Ui.Label(hero, L("tagline"), 32, TextAnchor.MiddleLeft), -1, 100);
            Para(hero, L("story"), 21, Ui.Dim);
            var screen = Ui.Panel(hero, "MenuComputer", Ui.PanelBg2);
            Ui.Size(screen, -1, 200);
            var inset = Ui.Panel(screen.transform, "Monitor", new Color(.07f, .13f, .13f));
            inset.rectTransform.Anchor(.12f, .15f, .88f, .85f);
            Ui.Label(inset.transform, "> PET SHOP\n" + L("computer_line"), 25, TextAnchor.MiddleCenter, Ui.Good).rectTransform.Fill(16);
            Ui.Size(Ui.Label(hero, L("development"), 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 36);

            var actions = Ui.Panel(body, "MenuActions", Ui.PanelBg2).rectTransform;
            Ui.Size(actions, 470, 680);
            Ui.VList(actions, 12, 24);
            Ui.Size(Ui.Label(actions, L("main_menu"), 25, TextAnchor.MiddleLeft, Ui.Accent), -1, 50);
            if (_canResumeMainMenu) Ui.Size(Ui.Button(actions, L("resume"), () => _mainMenu.Close(), 22, Ui.ButtonHot), -1, 54);
            Ui.Size(Ui.Button(actions, L("start"), () =>
            {
                if (_canResumeMainMenu) ConfirmMenuAction("new_game", "new_confirm", "start", _ctrl.NewGame);
                else _ctrl.NewGame();
            }, 22, Ui.ButtonHot), -1, 54);
            Ui.Size(Ui.Button(actions, L("load"), () => OpenLoadMenu(false), 22, Ui.ButtonBg, SaveStore.Exists), -1, 54);
            Ui.Size(Ui.Button(actions, L("settings"), OpenSettingsMenu, 22), -1, 54);
            Ui.Size(Ui.Button(actions, L("about"), OpenAboutMenu, 22), -1, 54);
            Ui.Size(Ui.Button(actions, L("help"), OpenHelpMenu, 22), -1, 54);
            Ui.Size(Ui.Button(actions, L("quit"), OpenQuitMenu, 22, Ui.Warn), -1, 54);
            Para(actions, SaveStore.Exists ? L("save_available") : L("no_saves"), 16, Ui.Dim);
            Ui.Size(Ui.Label(actions, L("language_hint"), 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 35);
        }

        public void OpenSettingsMenu()
        {
            var display=GameDisplaySettings.Current();
            OpenWindow(L("settings"), 840, 850, w =>
            {
                MenuTitle(w, "settings");
                BuildDisplayOptions(w,display);
                Line(w.Content, L("language"), 23, Ui.Accent);
                Para(w.Content, L("language_saved"), 17, Ui.Dim);
                var languages = Ui.Row(w.Content, 55, 10);
                foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                {
                    var chosen = language;
                    string nativeName = chosen == GameLanguage.Chinese ? "简体中文" : chosen == GameLanguage.Japanese ? "日本語" : "English";
                    Ui.Size(Ui.Button(languages, nativeName + (MenuLocalization.Current == chosen ? " ✓" : ""), () =>
                    {
                        MenuLocalization.Current = chosen;
                        Ui.InvalidateFont();
                        foreach (var window in _windows.ToArray()) window.RequestRebuild();
                    }, 21, MenuLocalization.Current == chosen ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                Line(w.Content, L("audio"), 23, Ui.Accent);
                Ui.Size(Ui.Button(w.Content, SoundFx.Muted ? L("sound_off") : L("sound_on"), () =>
                {
                    SoundFx.Muted = !SoundFx.Muted; PlayerPrefs.Save(); w.Rebuild();
                }, 20), -1, 48);
                var valueText = Ui.Label(w.Content, MenuLocalization.Format("volume", Math.Round(SoundFx.Volume * 100)), 19);
                Ui.Size(valueText, -1, 35);
                Ui.Size(Ui.Slider(w.Content, 0, 1, SoundFx.Volume, value =>
                {
                    SoundFx.Volume = value;
                    valueText.text = MenuLocalization.Format("volume", Math.Round(value * 100));
                }), -1, 40);
                Para(w.Content, L("settings_saved"), 16, Ui.Dim);
            }, onClosed: PlayerPrefs.Save);
        }

        public void OpenLoadMenu(bool confirmProgress = true)
        {
            OpenWindow(L("load"), 820, 570, w =>
            {
                MenuTitle(w, "load");
                Para(w.Content, L("load_description"), 18);
                foreach (bool automatic in new[] { false, true })
                {
                    bool slot = automatic;
                    var info = SaveStore.InspectSlot(slot);
                    Line(w.Content, L(slot ? "auto_save" : "manual_save"), 22, Ui.Accent);
                    Para(w.Content, !info.Exists ? L("empty_slot") : info.SavedAt != null
                        ? MenuLocalization.Format("save_time", info.SavedAt) : L("backup_available"), 17, Ui.Dim);
                    Ui.Size(Ui.Button(w.Content, L(slot ? "load_auto" : "load_manual"), () =>
                    {
                        void Load()
                        {
                            if (!_ctrl.TryLoadGame(slot)) OpenMenuMessage("load_failed", "load_failed_body");
                        }
                        if (confirmProgress) ConfirmMenuAction("load", "load_confirm", "load", Load);
                        else Load();
                    }, 20, Ui.ButtonHot, info.Exists), -1, 48);
                }
            });
        }

        public void OpenHelpMenu() => OpenWindow(L("help"), 900, 680, w =>
        {
            MenuTitle(w, "help");
            Line(w.Content, L("help_goal"), 24, Ui.Accent);
            Para(w.Content, L("help_goal_body"), 19);
            Line(w.Content, L("controls"), 23, Ui.Accent);
            Para(w.Content, L("controls_body"), 18);
            Line(w.Content, L("help_save"), 23, Ui.Accent);
            Para(w.Content, L("help_save_body"), 18);
        });

        public void OpenAboutMenu() => OpenWindow(L("about"), 760, 480, w =>
        {
            MenuTitle(w, "about");
            Line(w.Content, L("game_title"), 27, Ui.Accent);
            Para(w.Content, L("about_body"), 20);
            Para(w.Content, L("development"), 17, Ui.Dim);
        });

        private void OpenMenuMessage(string title, string body) => OpenWindow(L(title), 640, 350, w =>
        {
            MenuTitle(w, title); Para(w.Content, L(body), 19);
        });

        private void ConfirmMenuAction(string title, string body, string action, Action confirmed)
        {
            OpenWindow(L(title), 660, 340, w =>
            {
                MenuTitle(w, title); Para(w.Content, L(body), 20);
                var buttons = Ui.Row(w.Content, 50);
                Ui.Size(Ui.Button(buttons, L("cancel"), w.Close, 20), -1, -1, 1);
                Ui.Size(Ui.Button(buttons, L(action), () => { w.Close(); confirmed(); }, 20, Ui.Warn), -1, -1, 1);
            });
        }

        public void OpenQuitMenu() => ConfirmMenuAction("quit", (!MainMenuOpen || _canResumeMainMenu) ? "quit_unsaved" : "quit_confirm", "quit", () =>
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
}
