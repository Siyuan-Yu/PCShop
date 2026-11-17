using System;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _mainMenu;
        private bool _canResumeMainMenu;
        private Texture2D _mainMenuCover;
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
            foreach (var canvas in new[] { _hudCanvas, _labelCanvas, _dockCanvas, _toastCanvas, _invitationCanvas, _tutorialCanvas, _tipCanvas })
                if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        public void ShowMainMenu(bool canResume = false)
        {
            CloseAllWindows();
            _canResumeMainMenu = canResume;
            SetGameplayUiVisible(false);
            _mainMenu = OpenWindow(L("game_title"), 1920, 1080, BuildMainMenu, true, false, () =>
            {
                _mainMenu = null;
                SetGameplayUiVisible(true);
            }, scrollContent: false, allowDuringOpeningMeal: true, showTitleBar: false, fullScreen: true);
        }

        internal Window ShowTrialWorldLoading() => OpenWindow(L("loading_trial_world"), 1920, 1080, w =>
        {
            Ui.Label(w.Content, L("loading_trial_world"), 32, TextAnchor.MiddleCenter, Ui.Accent)
                .rectTransform.Fill();
        }, holds: false, closable: false, scrollContent: false, allowDuringOpeningMeal: true,
            showTitleBar: false, fullScreen: true);

        private void BuildMainMenu(Window w)
        {
            var cover = Ui.Rect(w.Content, "MenuCover").Fill();
            cover.gameObject.AddComponent<RectMask2D>();
            if (_mainMenuCover == null) _mainMenuCover = Resources.Load<Texture2D>("Menu/PetShopMenuCover");
            if (_mainMenuCover != null)
            {
                var art = Ui.Rect(cover, "CoverArt").Fill().gameObject.AddComponent<RawImage>();
                art.texture = _mainMenuCover;
                art.raycastTarget = false;
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectRatio = _mainMenuCover.width / (float)_mainMenuCover.height;
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }

            var coverTitle = Ui.Label(w.Content, L("game_title"), 132, TextAnchor.MiddleLeft, new Color(1f, .94f, .80f));
            coverTitle.fontStyle = FontStyle.Bold;
            coverTitle.resizeTextForBestFit = true;
            coverTitle.resizeTextMinSize = 30;
            coverTitle.resizeTextMaxSize = 132;
            coverTitle.rectTransform.Anchor(.048f, .682f, .445f, .84f);
            var shadow = coverTitle.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .03f, .07f, .88f);
            shadow.effectDistance = new Vector2(3, -4);
            var subtitle = Ui.Label(w.Content, L("tagline"), 27, TextAnchor.MiddleLeft, new Color(1f, .86f, .69f));
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 16;
            subtitle.resizeTextMaxSize = 27;
            subtitle.rectTransform.Anchor(.052f, .631f, .44f, .685f);
            if (GameSession.FirstChapterTrial)
                Ui.Label(w.Content, L("trial_edition"), 17, TextAnchor.MiddleLeft, Ui.ChromeGold)
                    .rectTransform.Anchor(.052f, .596f, .44f, .631f);

            var actions = Ui.Rect(w.Content, "MenuActions").Anchor(.048f, .12f, .222f, _canResumeMainMenu ? .59f : .556f);
            Ui.VList(actions, 14, 0);
            bool hasSaves = SaveStore.Exists;
            if (_canResumeMainMenu)
                Ui.Size(MainMenuAction(actions, "resume", w.Close, emphasized: true), -1, 66);
            Ui.Size(MainMenuAction(actions, hasSaves || _canResumeMainMenu ? "new_game" : "start", () =>
            {
                if (_canResumeMainMenu) ConfirmMenuAction("new_game", "new_confirm", "new_game", _ctrl.NewGame);
                else _ctrl.NewGame();
            }, emphasized: !_canResumeMainMenu && !hasSaves), -1, 66);
            Ui.Size(MainMenuAction(actions, "load", () => OpenLoadMenu(_canResumeMainMenu),
                emphasized: !_canResumeMainMenu && hasSaves, interactable: hasSaves), -1, 66);
            Ui.Size(MainMenuAction(actions, "settings", OpenSettingsMenu), -1, 66);
            var links = Ui.Row(actions, 42, 8);
            Ui.Size(MainMenuAction(links, "help", OpenHelpMenu, link: true), 0, -1, 1);
            Ui.Size(MainMenuAction(links, "about", OpenAboutMenu, link: true), 0, -1, 1);
            Ui.Size(MainMenuAction(actions, "quit", OpenQuitMenu, link: true), -1, 40);
        }

        private static Button MainMenuAction(Transform parent, string key, Action click,
            bool emphasized = false, bool interactable = true, bool link = false)
        {
            int size = link ? 25 : 31;
            Color fill = emphasized ? Ui.ButtonHot : link ? Color.clear : Ui.ChromeButton;
            var button = Ui.Button(parent, L(key), click, size, fill, interactable);
            var caption = button.GetComponentInChildren<Text>();
            caption.alignment = TextAnchor.MiddleLeft;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 16;
            caption.resizeTextMaxSize = size;
            caption.rectTransform.Anchor(0, 0, 1, 1, link ? 12 : 32, 0, 12);
            var feedback = button.colors;
            feedback.highlightedColor = new Color(1.17f, 1.17f, 1.17f);
            button.colors = feedback;
            return button;
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
                    }, 21, Ui.SelectedButton(MenuLocalization.Current == chosen)), -1, -1, 1);
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
        }, allowDuringOpeningMeal: true);

        private void ConfirmMenuAction(string title, string body, string action, Action confirmed, string bodyArgument = null)
        {
            OpenWindow(L(title), 660, 340, w =>
            {
                MenuTitle(w, title);
                Para(w.Content, bodyArgument == null ? L(body) : MenuLocalization.Format(body, bodyArgument), 20);
                var buttons = Ui.Row(w.Content, 50);
                Ui.Size(Ui.Button(buttons, L("cancel"), w.Close, 20), -1, -1, 1);
                Ui.Size(Ui.Button(buttons, L(action), () => { w.Close(); confirmed(); }, 20, Ui.Warn), -1, -1, 1);
            }, allowDuringOpeningMeal: MainMenuOpen);
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
