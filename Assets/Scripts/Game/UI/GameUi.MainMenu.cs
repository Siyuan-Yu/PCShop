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
        private const float MenuLeft = .054f;
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

        private Text _worldLoadingStatus, _worldLoadingProgressText;
        private RectTransform _worldLoadingFill;
        private float _worldLoadingProgress, _worldLoadingStarted, _worldLoadingNextRefresh;

        internal void SetWorldLoadingProgress(float progress)
        {
            _worldLoadingProgress=Mathf.Max(_worldLoadingProgress,Mathf.Clamp01(progress));
            if(_worldLoadingFill!=null)_worldLoadingFill.anchorMax=new Vector2(_worldLoadingProgress,1);
            RefreshWorldLoading(true);
        }
        internal void RefreshWorldLoading(bool force=false)
        {
            if(_worldLoadingProgressText==null||(!force&&Time.realtimeSinceStartup<_worldLoadingNextRefresh))return;
            _worldLoadingNextRefresh=Time.realtimeSinceStartup+.2f;
            int seconds=Mathf.FloorToInt(Time.realtimeSinceStartup-_worldLoadingStarted);
            _worldLoadingProgressText.text=$"{Mathf.FloorToInt(_worldLoadingProgress*100)}%  ·  {string.Format(L("loading_elapsed"),seconds)}";
        }

        internal void SetWorldLoadingStage(string key)
        {
            if (_worldLoadingStatus != null) _worldLoadingStatus.text = L(key);
        }

        internal Window ShowWorldLoading() => OpenWindow(L("loading_world"), 1920, 1080, w =>
        {
            w.PreserveDuringTransition=true;
            // Intro windows and tutorial canvases may be created while the session
            // starts. Keep the opaque loading cover above all of them until ready.
            var canvas=w.Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting=true;canvas.sortingOrder=1000;
            w.Root.gameObject.AddComponent<GraphicRaycaster>();
            _worldLoadingProgress=0;
            _worldLoadingStarted=Time.realtimeSinceStartup;
            Ui.Label(w.Content, L("loading_world"), 36, TextAnchor.MiddleCenter, Ui.ChromeInk)
                .rectTransform.Anchor(.1f, .50f, .9f, .60f);
            _worldLoadingStatus = Ui.Label(w.Content, L("loading_ground"), 24, TextAnchor.MiddleCenter, Ui.ChromeDim);
            _worldLoadingStatus.rectTransform.Anchor(.1f, .44f, .9f, .51f);
            var track=Ui.Panel(w.Content,"WorldLoadingTrack",Ui.ChromeDim);
            track.raycastTarget=false;
            track.rectTransform.Anchor(.30f,.415f,.70f,.427f);
            var fill=Ui.Panel(track.transform,"WorldLoadingFill",Ui.ChromeGold);
            fill.raycastTarget=false;
            _worldLoadingFill=fill.rectTransform;
            _worldLoadingFill.Anchor(0,0,0,1);
            _worldLoadingProgressText=Ui.Label(w.Content,"",22,TextAnchor.MiddleCenter,Ui.ChromeDim);
            _worldLoadingProgressText.rectTransform.Anchor(.20f,.35f,.80f,.40f);
            Ui.Label(w.Content,L("loading_background_hint"),20,TextAnchor.MiddleCenter,Ui.ChromeDim)
                .rectTransform.Anchor(.15f,.29f,.85f,.34f);
            RefreshWorldLoading(true);
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

            var coverTitle = Ui.Label(w.Content, L("game_title"), 132, TextAnchor.MiddleLeft, Ui.ChromeInk);
            coverTitle.fontStyle = FontStyle.Bold;
            coverTitle.resizeTextForBestFit = true;
            coverTitle.resizeTextMinSize = 30;
            coverTitle.resizeTextMaxSize = 132;
            coverTitle.rectTransform.Anchor(MenuLeft, .682f, .445f, .84f);
            var shadow = coverTitle.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .03f, .07f, .88f);
            shadow.effectDistance = new Vector2(3, -4);
            var subtitle = Ui.Label(w.Content, L("tagline"), 27, TextAnchor.MiddleLeft, Ui.ChromeGold);
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 16;
            subtitle.resizeTextMaxSize = 27;
            subtitle.rectTransform.Anchor(MenuLeft, .631f, .44f, .685f);
            if (GameSession.FirstChapterTrial)
                Ui.Label(w.Content, L("trial_edition"), 17, TextAnchor.MiddleLeft, Ui.ChromeGold)
                    .rectTransform.Anchor(MenuLeft, .596f, .44f, .631f);

            var actions = Ui.Rect(w.Content, "MenuActions").Anchor(MenuLeft, .12f, MenuLeft + .25f,
                GameSession.FirstChapterTrial ? .567f : .589f);
            Ui.VList(actions, 15, 0);
            const float actionHeight = 76f;
            bool hasSaves = SaveStore.Exists;
            if (_canResumeMainMenu)
                Ui.Size(MainMenuAction(actions, "resume", w.Close, emphasized: true), -1, actionHeight);
            Ui.Size(MainMenuAction(actions, hasSaves || _canResumeMainMenu ? "new_game" : "start", () =>
            {
                if (_canResumeMainMenu) ConfirmMenuAction("new_game", "new_confirm", "new_game", _ctrl.NewGame);
                else _ctrl.NewGame();
            }, emphasized: !_canResumeMainMenu && !hasSaves), -1, actionHeight);
            Ui.Size(MainMenuAction(actions, "load", () => OpenLoadMenu(_canResumeMainMenu),
                emphasized: !_canResumeMainMenu && hasSaves, interactable: hasSaves), -1, actionHeight);
            Ui.Size(MainMenuAction(actions, "settings", OpenSettingsMenu), -1, actionHeight);
            var secondary = Ui.Row(actions, 68, 15);
            Ui.Size(MainMenuAction(secondary, "help", OpenHelpMenu, compact: true), 0, -1, 1);
            Ui.Size(MainMenuAction(secondary, "about", OpenAboutMenu, compact: true), 0, -1, 1);
            Ui.Size(MainMenuAction(secondary, "quit", OpenQuitMenu, compact: true), 0, -1, 1);
        }

        private static Button MainMenuAction(Transform parent, string key, Action click,
            bool emphasized = false, bool interactable = true, bool compact = false)
        {
            int size = compact ? 23 : 31;
            Color fill = key == "quit" ? Ui.ButtonDanger : emphasized ? Ui.ButtonHot : Ui.ButtonSecondary;
            var button = Ui.Button(parent, L(key), click, size, fill, interactable);
            var caption = button.GetComponentInChildren<Text>();
            caption.alignment = compact ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = compact ? 13 : 16;
            caption.resizeTextMaxSize = size;
            caption.rectTransform.Anchor(0, 0, 1, 1, compact ? 8 : 26, 0, emphasized ? 56 : 8);
            if (emphasized)
                Ui.Label(button.transform, "›", 38, TextAnchor.MiddleCenter, Ui.TextColor)
                    .rectTransform.Anchor(1, 0, 1, 1, -52, 0, 14);
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
                Ui.Size(Ui.Button(buttons, L("cancel"), w.Close, 20, Ui.ButtonSecondary), -1, -1, 1);
                Ui.Size(Ui.Button(buttons, L(action), () => { w.Close(); confirmed(); }, 20, Ui.ButtonDanger), -1, -1, 1);
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
