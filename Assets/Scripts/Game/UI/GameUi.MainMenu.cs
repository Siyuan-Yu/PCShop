using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
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
        private Texture2D _mainMenuLogo;
        private const float MenuLeft = .034f;
        private const float MenuButtonWidth = .24f;
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
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = _mainMenuCover.width / (float)_mainMenuCover.height;
            }

            var awning = Ui.Rect(w.Content, "MenuAwning").Anchor(MenuLeft, .896f, .373f, .95f);
            var green = new Color32(91, 122, 94, 255);
            var cream = new Color32(239, 230, 207, 255);
            const int stripeCount = 21;
            for (int i = 0; i < stripeCount; i++)
            {
                var stripe = Ui.Panel(awning, "AwningStripe", i % 2 == 0 ? green : cream);
                stripe.rectTransform.Anchor(i / (float)stripeCount, 0, (i + 1f) / stripeCount, 1);
                stripe.raycastTarget = false;
                Ui.StyleAwningStripe(stripe);
            }

            _mainMenuLogo ??= Resources.Load<Texture2D>("Menu/PetShopLogo");
            var logo = Ui.Rect(w.Content, "MenuLogo").Anchor(MenuLeft, .75f, .384f, .89f);
            if (_mainMenuLogo != null)
            {
                var graphic = Ui.Rect(logo, "LogoArtwork").Fill().gameObject.AddComponent<RawImage>();
                graphic.texture = _mainMenuLogo;
                graphic.raycastTarget = false;
                var fit = graphic.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = _mainMenuLogo.width / (float)_mainMenuLogo.height;
            }
            else
            {
                var title = Ui.Label(logo, L("game_title"), 144, TextAnchor.MiddleLeft, Ui.ChromeInk);
                title.fontStyle = FontStyle.Bold;
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 32;
                title.resizeTextMaxSize = 144;
                title.rectTransform.Fill();
            }
            var subtitle = Ui.Label(w.Content, L("tagline"), 35, TextAnchor.MiddleLeft, Ui.ChromeInk);
            subtitle.fontStyle = FontStyle.Bold;
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 24;
            subtitle.resizeTextMaxSize = 35;
            subtitle.rectTransform.Anchor(MenuLeft + .017f, .696f, .389f, .741f);
            if (GameSession.FirstChapterTrial)
                Ui.Label(w.Content, L("trial_edition"), 17, TextAnchor.MiddleLeft, Ui.ChromeGold)
                    .rectTransform.Anchor(MenuLeft, .652f, .389f, .684f);

            var latest = SaveStore.LatestReadable();
            bool hasSaves = latest != null || SaveStore.Exists;
            bool canResume = _canResumeMainMenu || latest != null;
            if (latest != null)
            {
                var record = Ui.Panel(w.Content, "MenuSaveSummary", Ui.HudTile);
                Ui.StyleMenuPlate(record, Ui.Accent);
                record.rectTransform.Anchor(MenuLeft, .618f, .377f, .678f);
                var stripe = Ui.Panel(record.transform, "SummaryStripe", Ui.ButtonHot);
                Ui.Round(stripe);
                stripe.raycastTarget = false;
                stripe.rectTransform.Anchor(0, 0, 0, 1, 16, 10, -28, 10);
                var date = new GameDate(latest.Day);
                var summary = Ui.Label(record.transform, string.Format(L("menu_save_summary"),
                    date.Year, date.Month, date.Day, Money.FromCents(latest.BalanceCents), latest.ShopCount), 27);
                summary.fontStyle = FontStyle.Bold;
                summary.resizeTextForBestFit = true;
                summary.resizeTextMinSize = 20;
                summary.resizeTextMaxSize = 27;
                summary.rectTransform.Anchor(0, 0, 1, 1, 44, 5, 12, 5);
            }

            var actions = Ui.Rect(w.Content, "MenuActions").Fill();
            if (canResume)
                Place(.41f, "resume", () =>
                {
                    if (_canResumeMainMenu) w.Close();
                    else if (!_ctrl.TryLoadGame(latest.Slot, latest.Automatic)) OpenMenuMessage("load_failed", "load_failed_body");
                }, UiIcon.Play, true);
            Place(canResume ? .52f : .41f, canResume || hasSaves ? "new_game" : "start", () =>
            {
                if (_canResumeMainMenu) ConfirmMenuAction("new_game", "new_confirm", "new_game", _ctrl.NewGame);
                else _ctrl.NewGame();
            }, UiIcon.Shop, !canResume);
            Place(canResume ? .63f : .575f, "load", () => OpenLoadMenu(_canResumeMainMenu), UiIcon.Folder, enabled: hasSaves);
            Place(.74f, "settings", OpenSettingsMenu, UiIcon.Settings);

            var secondary = Ui.Row(actions, 86, 14).Anchor(MenuLeft, .066f, .356f, .142f);
            Ui.Size(MainMenuAction(secondary, "help", OpenHelpMenu, UiIcon.Help, compact: true), 0, -1, 1);
            Ui.Size(MainMenuAction(secondary, "about", OpenAboutMenu, UiIcon.About, compact: true), 0, -1, 1);
            Ui.Size(MainMenuAction(secondary, "quit", OpenQuitMenu, UiIcon.Door, compact: true), 0, -1, 1.20f);

            void Place(float y, string key, Action action, UiIcon icon, bool primary = false, bool enabled = true)
            {
                var control = MainMenuAction(actions, key, action, icon, primary, enabled);
                control.image.rectTransform.Anchor(MenuLeft, 1 - y - .09f, MenuLeft + MenuButtonWidth, 1 - y);
            }
        }

        private static Button MainMenuAction(Transform parent, string key, Action click, UiIcon icon,
            bool emphasized = false, bool interactable = true, bool compact = false)
        {
            int size = compact ? 31 : 46;
            bool danger = key == "quit";
            Color fill = danger ? Ui.ButtonDanger : emphasized ? Ui.ButtonHot : Ui.HudTile;
            var button = Ui.Button(parent, L(key), click, size, fill, interactable, exclusiveEmphasis: false);
            var edge = danger ? Ui.Warn : interactable ? Ui.FlatHotEdge : Ui.FlatButtonEdge;
            Ui.StyleMenuPlate(button.image, edge);
            var caption = button.GetComponentInChildren<Text>();
            caption.fontStyle = FontStyle.Bold;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 24;
            caption.resizeTextMaxSize = size;

            var artwork = Ui.Artwork(button.transform, icon);
            caption.alignment = artwork == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            float captionLeft = artwork == null ? 12 : compact ? 66 : 140;
            caption.rectTransform.Anchor(0, 0, 1, 1, captionLeft, 4, emphasized ? 64 : 12, 4);
            if (artwork != null)
            {
                Ui.UseCaptionIcon(button, artwork);
                if (icon == UiIcon.Play)
                {
                    var tile = Ui.Panel(button.transform, "PlayIconTile", Ui.Chrome);
                    Ui.StyleMenuPlate(tile, Ui.FlatHotEdge);
                    tile.raycastTarget = false;
                    tile.rectTransform.Anchor(0, .5f, 0, .5f, 28, -28, -98, -28);
                    artwork.transform.SetParent(tile.transform, false);
                    artwork.rectTransform.Anchor(.5f, .5f, .5f, .5f, -22, -22, -22, -22);
                }
                else
                {
                    int iconSize = compact ? 44 : 76;
                    float inset = compact ? 13 : 25;
                    artwork.rectTransform.Anchor(0, .5f, 0, .5f,
                        inset, -iconSize * .5f, -inset - iconSize, -iconSize * .5f);
                }
            }
            if (emphasized)
                Ui.Label(button.transform, "›", 55, TextAnchor.MiddleCenter, Color.white)
                    .rectTransform.Anchor(1, 0, 1, 1, -65, 1, 16, 1);
            return button;
        }

        public void OpenSettingsMenu()
        {
            var display=GameDisplaySettings.Current();
            OpenWindow(L("settings"), 840, 850, w =>
            {
                MenuTitle(w, "settings");
                BuildDisplayOptions(w,display);
                SectionBand(w.Content, L("language"));
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
                SectionBand(w.Content, L("audio"));
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
            }, onClosed: PlayerPrefs.Save, fitContent: true);
        }

        public void OpenHelpMenu() => OpenWindow(L("help"), 900, 680, w =>
        {
            MenuTitle(w, "help");
            HelpCard("help_goal", "help_goal_body", UiIcon.Shop);
            HelpCard("controls", "controls_body", UiIcon.Settings);
            HelpCard("help_save", "help_save_body", UiIcon.Save);

            void HelpCard(string title, string copy, UiIcon key)
            {
                var card = Ui.FlatPanel(w.Content, "Help_" + title, Ui.ButtonBg).rectTransform;
                var columns = card.gameObject.AddComponent<HorizontalLayoutGroup>();
                columns.padding = new RectOffset(14, 16, 9, 10);
                columns.spacing = 14;
                columns.childControlWidth = columns.childControlHeight = true;
                columns.childForceExpandWidth = columns.childForceExpandHeight = false;
                if (Ui.Artwork(card, key) is { } artwork) Ui.Size(artwork, 56, 56);
                var text = Ui.Size(Ui.Rect(card, "HelpText"), 0, flexWidth: 1);
                Ui.VList(text, 4, 0);
                Line(text, L(title), 23);
                Para(text, L(copy), 20);
            }
        }, fitContent: true);

        public void OpenAboutMenu() => OpenWindow(L("about"), 760, 480, w =>
        {
            MenuTitle(w, "about");
            Line(w.Content, L("game_title"), 27, Ui.Accent);
            Para(w.Content, L("about_body"), 20);
            Para(w.Content, L("development"), 17, Ui.Dim);
        }, fitContent: true);

        private void OpenMenuMessage(string title, string body) => OpenWindow(L(title), 640, 350, w =>
        {
            MenuTitle(w, title); Para(w.Content, L(body), 19);
        }, allowDuringOpeningMeal: true, fitContent: true);

        private void ConfirmMenuAction(string title, string body, string action, Action confirmed, string bodyArgument = null)
        {
            OpenWindow(L(title), 660, 340, w =>
            {
                MenuTitle(w, title);
                Para(w.Content, bodyArgument == null ? L(body) : MenuLocalization.Format(body, bodyArgument), 20);
                var buttons = Ui.Row(w.Content, 50);
                Ui.Size(Ui.Button(buttons, L("cancel"), w.Close, 20, Ui.ButtonSecondary), -1, -1, 1);
                Ui.Size(Ui.Button(buttons, L(action), () => { w.Close(); confirmed(); }, 20, Ui.ButtonDanger), -1, -1, 1);
            }, allowDuringOpeningMeal: MainMenuOpen, fitContent: true);
        }

        private static void SectionBand(Transform parent, string title)
        {
            var band = Ui.Size(Ui.Panel(parent, "SectionBand", Ui.Chrome), -1, 37);
            Ui.Label(band.transform, "<b>" + title + "</b>", 21).rectTransform.Fill(8);
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
