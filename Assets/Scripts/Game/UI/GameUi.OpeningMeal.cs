using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    internal sealed class OpeningGuideInputBlock : Image
    {
        public Button PassThrough { get; set; }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (PassThrough == null) return base.IsRaycastLocationValid(screenPoint, eventCamera);
            var canvas = PassThrough.image.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return !RectTransformUtility.RectangleContainsScreenPoint(PassThrough.image.rectTransform, screenPoint, camera);
        }
    }

    /// <summary>
    /// 开局做饭强制引导：整个屋子压暗，只留电饭锅一块高亮；下面一张步骤卡说清楚该怎么做。
    /// 做完（吃了饭或装了饭盒）之前，核心层会拦住出门（GameSession.LeaveBlocker）。
    /// </summary>
    public sealed partial class GameUi
    {
        private Canvas _tutorialCanvas;
        private OpeningGuideInputBlock _openingMealInputBlock;
        private Button _openingMealIntroNext;
        private Window _openingMealCooker;
        private GameSession _openingMealSession;
        private Navigation _questActionNavigation;
        private int _openingMealCookStage;
        private string _openingMealFocus;
        private bool _openingMealIntroHeld;
        private readonly Dictionary<Button, bool> _openingMealButtons = new Dictionary<Button, bool>();
        private const string OpeningMealIntroSeen = "tutorial:opening-meal-intro-seen";
        private bool OpeningMealIntroPending => S.OpeningMealTutorialPending && S.OpeningMealStep == 0 && !S.Flags.Contains(OpeningMealIntroSeen);
        private const string OpeningMoveDone = "tutorial:opening-movement-done";
        private const string OpeningHungerSeen = "tutorial:opening-hunger-seen";
        private float _openingWalked;
        public bool OpeningMovementPending => S.OpeningMealTutorialPending && S.OpeningMealStep == 0
            && !S.Flags.Contains(OpeningMoveDone);
        private bool OpeningHungerPending => S.OpeningMealTutorialPending && S.OpeningMealStep == 0
            && S.Flags.Contains(OpeningMoveDone) && !S.Flags.Contains(OpeningHungerSeen);
        public bool OpeningMealPreludePending => OpeningMealIntroPending || OpeningMovementPending || OpeningHungerPending;
        public string OpeningMealFocusTarget => _openingMealFocus;

        public void RecordOpeningWalk(float actualDistance)
        {
            if (!OpeningMovementPending || OpeningMealIntroPending || !_ctrl.InHome || actualDistance <= 0) return;
            _openingWalked += actualDistance;
            if (_openingWalked >= 1.2f) S.Flags.Add(OpeningMoveDone);
        }

        private void BindOpeningMealCooker(Window w)
        {
            if (!S.OpeningMealTutorialPending) return;
            _openingMealCooker = w;
            _openingMealButtons.Clear();
            foreach (var b in w.Root.GetComponentsInChildren<Button>()) _openingMealButtons[b] = b.interactable;
        }

        private void RestoreOpeningMealButtons()
        {
            foreach (var pair in _openingMealButtons) if (pair.Key != null) pair.Key.interactable = pair.Value;
            _openingMealButtons.Clear();
        }
        public bool OpeningMealRestricted => S != null && S.OpeningMealTutorialPending && !MainMenuOpen && !S.IsGameOver;

        public void RemindOpeningMeal() => Toast(OpeningMealPreludePending
            ? OpeningMealIntroPending ? "先看看自己的处境，点击“看看房间”继续。"
                : OpeningMovementPending ? "先按 WASD 或方向键，在房间里实际走动几步。"
                : "肚子饿了，点击“找点吃的”后再做饭。"
            : "先做饭。" + OpeningMealNextAction);

        private string OpeningMealNextAction => S.OpeningMealStep == 1
            ? "打开电饭锅，点“等到饭好”，再吃一碗并打包剩饭。"
            : S.OpeningMealStep >= 2 ? (S.OpeningMealEaten ? "已经吃过饭了，打开电饭锅，把剩饭全部装进背包。" : "饭好了，打开电饭锅，先吃一碗。")
            : "走近高亮的电饭锅，按 F，选好米量和做法后开始焖饭。";

        private void BuildOpeningMealReminder(Window w)
        {
            Para(w.Content, "先完成这一顿饭，再做其他事情。", 22, Ui.Accent);
            Para(w.Content, OpeningMealNextAction, 19);
            Para(w.Content, "可以继续走路、调整镜头；Esc 仍可保存、设置或退出。", 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, "知道了，先做饭", w.Close, 19, Ui.ButtonHot), -1, 46);
        }

        private void OpeningMealOverlayClick()
        {
            if (S.OpeningAgencyGuidePending) return;
            if (OpeningMealPreludePending) { RemindOpeningMeal(); return; }
            // Keep the existing cooking quest shortcut usable through the teaching overlay.
            var questCanvas = _questActionButton?.GetComponentInParent<Canvas>();
            var camera = questCanvas != null && questCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? questCanvas.worldCamera : null;
            if (_questActionButton != null && _questActionButton.interactable && S.CurrentQuest()?.Id == "first_meal"
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_questActionButton.transform, Input.mousePosition, camera))
                _questAction?.Invoke();
            else RemindOpeningMeal();
        }
        private RectTransform _guideRoot, _guideHole, _guideArrow, _guideCard;
        private readonly Image[] _guideDim = new Image[4];
        private readonly Image[] _guideFrame = new Image[4];
        private Text _guideTitle, _guideBody, _guideFoot;
        private readonly Image[] _guideDots = new Image[3];

        private static readonly Color GuideDimColor = new Color(0.02f, 0.04f, 0.08f, 0.78f);
        private static readonly Vector2 GuideHoleSize = new Vector2(260, 230);

        private const float GuideFrameWidth = 4;

        private void BuildOpeningMealGuide()
        {
            _tutorialCanvas = Ui.CreateCanvas("UI_Tutorial", 40);
            _guideRoot = Ui.Rect(_tutorialCanvas.transform, "OpeningMealGuide");
            _guideRoot.Anchor(0, 0, 1, 1, 0, 0, 0, 0);
            var inputBlock = _openingMealInputBlock = Ui.Rect(_guideRoot, "OpeningMealInputBlock").gameObject.AddComponent<OpeningGuideInputBlock>();
            inputBlock.color = Color.clear;
            inputBlock.rectTransform.Fill();
            var blockerButton = inputBlock.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = inputBlock;
            blockerButton.navigation = new Navigation { mode = Navigation.Mode.None };
            blockerButton.onClick.AddListener(OpeningMealOverlayClick);
            string[] dimNames = { "GuideDimTop", "GuideDimBottom", "GuideDimLeft", "GuideDimRight" };
            for (int i = 0; i < 4; i++)
            {
                _guideDim[i] = Ui.Panel(_guideRoot, dimNames[i], GuideDimColor);
                _guideDim[i].raycastTarget = false;
                _guideDim[i].gameObject.AddComponent<Button>().onClick.AddListener(() => Toast("请先点击高亮的区域，完成当前这一步。"));
                var r = _guideDim[i].rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
            }
            for (int i = 0; i < 4; i++)
            {
                _guideFrame[i] = Ui.Panel(_guideRoot, "GuideFrame" + i, Ui.FlatHotEdge);
                _guideFrame[i].raycastTarget = false;
                var r = _guideFrame[i].rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
            }
            _guideHole = Ui.Rect(_guideRoot, "GuideHole");
            _guideHole.anchorMin = _guideHole.anchorMax = Vector2.zero; _guideHole.pivot = Vector2.zero;

            var arrow = Ui.Label(_guideRoot, "▼ 电饭锅", 26, TextAnchor.LowerCenter, Ui.Accent);
            arrow.fontStyle = FontStyle.Bold; arrow.raycastTarget = false;
            arrow.horizontalOverflow = HorizontalWrapMode.Overflow; arrow.verticalOverflow = VerticalWrapMode.Overflow;
            arrow.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            _guideArrow = arrow.rectTransform;
            _guideArrow.anchorMin = _guideArrow.anchorMax = Vector2.zero; _guideArrow.pivot = new Vector2(0.5f, 0);
            _guideArrow.sizeDelta = new Vector2(240, 36);

            var card = Ui.FlatPanel(_guideRoot, "GuideCard", Ui.FlatBand, Ui.FlatHotEdge);
            card.raycastTarget = false;
            _guideCard = card.rectTransform;
            _guideCard.anchorMin = _guideCard.anchorMax = new Vector2(0.5f, 0); _guideCard.pivot = new Vector2(0.5f, 0);
            _guideCard.sizeDelta = new Vector2(880, 220);
            _guideCard.anchoredPosition = new Vector2(0, 150);
            Ui.FlatBorder(card, Ui.FlatHotEdge, 2);

            for (int i = 0; i < 3; i++)
            {
                _guideDots[i] = Ui.Panel(_guideCard, "GuideDot" + i, Ui.FlatWellEdge);
                _guideDots[i].raycastTarget = false;
                _guideDots[i].rectTransform.Anchor(0, 1, 0, 1, 22 + i * 26, -34, -(22 + i * 26 + 18), 16);
            }
            _guideTitle = Ui.Label(_guideCard, "", 24, TextAnchor.MiddleLeft, Ui.Accent);
            _guideTitle.fontStyle = FontStyle.Bold; _guideTitle.raycastTarget = false;
            _guideTitle.rectTransform.Anchor(0, 1, 1, 1, 100, -44, 20, 12);
            _guideBody = Ui.Label(_guideCard, "", 20, TextAnchor.UpperLeft);
            _guideBody.raycastTarget = false;
            _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, 44, 22, 56);
            _guideFoot = Ui.Label(_guideCard, "", 17, TextAnchor.MiddleLeft, new Color32(0xE0, 0x9A, 0x82, 255));
            _guideFoot.raycastTarget = false;
            _guideFoot.rectTransform.Anchor(0, 0, 1, 0, 22, 10, 22, -38);
            _openingMealIntroNext = Ui.Button(_guideCard, "先找点吃的", () =>
            {
                S.Flags.Add(OpeningMealIntroPending ? OpeningMealIntroSeen : OpeningHungerSeen);
                UpdateOpeningMealGuide();
            }, 19, Ui.ButtonHot);
            _openingMealIntroNext.name = "OpeningMealIntroNext";
            ((RectTransform)_openingMealIntroNext.transform).Anchor(0, 0, 1, 0, 22, 16, 22, -62);
            _openingMealIntroNext.gameObject.SetActive(false);
            _guideRoot.gameObject.SetActive(false);
        }

        public bool OpeningMealGuideVisible => _guideRoot != null && _guideRoot.gameObject.activeSelf && _openingMealInputBlock.PassThrough == null;

        private static void GuideRect(RectTransform r, float x, float y, float w, float h)
        {
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        private void UpdateOpeningMealGuide()
        {
            if (_tutorialCanvas == null) return;
            if (!ReferenceEquals(_openingMealSession, S))
            {
                SetOpeningAgencyGuideVisible(false);
                RestoreOpeningMealButtons();
                _openingMealSession = S;
                _openingMealIntroHeld = false;
                _openingMealCookStage = 0;
                _openingWalked = 0;
                _openingMealCooker = null;
            }
            if (UpdateOpeningAgencyGuide()) return;
            int step = S.OpeningMealStep;
            bool cookerOpen = _openingMealCooker != null && !_openingMealCooker.Closed
                && _windows.Count > 0 && _windows[_windows.Count - 1] == _openingMealCooker;
            bool background = OpeningMealIntroPending;
            bool movement = !background && OpeningMovementPending;
            bool hunger = !background && !movement && OpeningHungerPending;
            bool intro = (background || hunger) && !AnyModalOpen;
            bool show = step >= 0 && _ctrl.InHome && !MainMenuOpen && (!AnyModalOpen || cookerOpen)
                && !S.IsGameOver && !_ctrl.Placing && !PopupOpen;
            if (show && intro && !_openingMealIntroHeld) { S.Time.Hold(); _openingMealIntroHeld = true; }
            else if ((!show || !intro) && _openingMealIntroHeld) { S.Time.Release(); _openingMealIntroHeld = false; }
            _guideRoot.gameObject.SetActive(show);
            if (!show) { if (step < 0) RestoreOpeningMealButtons(); return; }
            _guideCard.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var canvasRt = (RectTransform)_tutorialCanvas.transform;
            Vector2 size = canvasRt.rect.size;
            _openingMealInputBlock.gameObject.SetActive(!cookerOpen);
            foreach (var dim in _guideDim) dim.raycastTarget = cookerOpen;
            _openingMealIntroNext.gameObject.SetActive(intro);
            foreach (var dot in _guideDots) dot.gameObject.SetActive(!intro && !cookerOpen);
            _guideArrow.gameObject.SetActive(!intro && !cookerOpen);
            _guideCard.anchorMin = _guideCard.anchorMax = Vector2.zero;
            _guideCard.pivot = Vector2.zero;
            _guideTitle.rectTransform.Anchor(0, 1, 1, 1, cookerOpen || intro ? 22 : 100, -48, 20, 12);
            _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, intro ? 78 : 66, 22, 62);
            _guideFoot.gameObject.SetActive(!intro);
            Rect hole = new Rect(); bool hasHole = false;
            (string title, string body) text = OpeningMealGuideText(step);
            if (intro)
            {
                GuideRect(_guideCard, (size.x - 620) * .5f, (size.y - 340) * .5f, 620, 340);
                _openingMealFocus = background ? "ArrivalStory" : "HungerStory";
                _openingMealIntroNext.GetComponentInChildren<Text>().text = background ? "看看房间" : "找点吃的";
                text = background
                    ? ("换个地方，重新开始", $"公司裁员，你失去了上一份工作。\n你带着不多的行李来到城郊，暂时租下了这间老旧的小屋。\n现在手头还有 <b>{S.Ledger.Balance}</b>，往后的日子得慢慢打算。\n先放下行李，看看这个暂时属于自己的落脚处吧。")
                    : ("肚子有点饿了", "在房间里走了几步，你才发觉自己一路奔波，还没有好好吃过东西。\n肚子饿得直叫。桌上放着电饭锅，家里也还有大米。\n先别急着出门，给自己做顿热饭吧。");
            }
            else if (movement)
            {
                _openingMealFocus = "Movement";
                GuideRect(_guideCard, 30, 180, 440, 250);
                _guideTitle.rectTransform.Anchor(0, 1, 1, 1, 22, -48, 20, 12);
                _guideArrow.gameObject.SetActive(false);
                foreach (var dot in _guideDots) dot.gameObject.SetActive(false);
                Vector3 sp = _ctrl.Cam.WorldToScreenPoint(_ctrl.HomeRoom.OwnerWorld + Vector3.up * .8f);
                if (sp.z > 0) { var c = GuidePoint(canvasRt, sp, size); hole = new Rect(c - new Vector2(280, 190), new Vector2(560, 380)); hasHole = true; }
                text = ("先在房间里走走", "按 <b>W / A / S / D</b> 移动，也可以用 <b>方向键</b>。\n试着离开门边，往房间里走几步。\n人物真正走动后，故事会继续。");
                _guideFoot.text = "不用点击物品 · 先熟悉移动";
            }
            else if (cookerOpen)
            {
                string targetName = step == 1 ? "OpeningMealWait" : step == 2 ? "OpeningMealEat" : step == 3 ? "OpeningMealPackAll" :
                    _openingMealCookStage == 0 ? "OpeningMealRice" : _openingMealCookStage == 1 ? "OpeningMealRecipe" : "OpeningMealStart";
                var target = _openingMealCooker.Root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == targetName);
                if (_openingMealFocus != targetName)
                { _openingMealFocus = targetName; EventSystem.current?.SetSelectedGameObject(null); }
                foreach (var pair in _openingMealButtons)
                    if (pair.Key != null) pair.Key.interactable = pair.Value && target != null && pair.Key.transform.IsChildOf(target);
                if (target != null)
                {
                    var corners = new Vector3[4]; target.GetWorldCorners(corners);
                    var sourceCanvas = target.GetComponentInParent<Canvas>();
                    var sourceCam = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
                    Vector2 low = GuidePoint(canvasRt, RectTransformUtility.WorldToScreenPoint(sourceCam, corners[0]), size);
                    Vector2 high = GuidePoint(canvasRt, RectTransformUtility.WorldToScreenPoint(sourceCam, corners[2]), size);
                    hole = Rect.MinMaxRect(low.x - 6, low.y - 6, high.x + 6, high.y + 6); hasHole = true;
                    text = OpeningMealControlText(step);
                    // Fit beside the whole cooker window, keeping the explanation off its controls.
                    var panel = (RectTransform)_openingMealCooker.TitleText.transform.parent.parent;
                    panel.GetWorldCorners(corners);
                    float panelRight = GuidePoint(canvasRt, RectTransformUtility.WorldToScreenPoint(sourceCam, corners[2]), size).x;
                    float panelLeft = GuidePoint(canvasRt, RectTransformUtility.WorldToScreenPoint(sourceCam, corners[0]), size).x;
                    float width = Mathf.Min(400, Mathf.Max(size.x - panelRight - 36, panelLeft - 36));
                    width = Mathf.Max(260, width);
                    float x = size.x - panelRight - 36 >= width ? panelRight + 18 : Mathf.Max(12, panelLeft - width - 18);
                    GuideRect(_guideCard, x, Mathf.Clamp(hole.center.y - 135, 80, size.y - 320), width, 270);
                }
                _guideFoot.text = "只需点击高亮区域 · Esc 可暂时关闭窗口";
            }
            else
            {
                _openingMealFocus = "CookerWorld";
                Vector3 sp = _ctrl.HomeRoom.CookerWorld is Vector3 cooker ? _ctrl.Cam.WorldToScreenPoint(cooker + Vector3.down * .45f) : new Vector3(0, 0, -1);
                if (sp.z > 0) { Vector2 c = GuidePoint(canvasRt, sp, size); hole = new Rect(c - GuideHoleSize * .5f, GuideHoleSize); hasHole = true; }
                GuideRect(_guideCard, (size.x - 880) * .5f, 150, 880, 220);
                _guideFoot.text = "先做饭再做其他事 · WASD / 方向键走路，F 交互；Esc 打开系统菜单。";
            }
            DrawGuideSpotlight(size, hasHole ? (Rect?)hole : null);
            _guideTitle.text = text.title; _guideBody.text = text.body;
            for (int i = 0; i < 3; i++) _guideDots[i].color = i < step ? Ui.FlatUp : i == step ? Ui.FlatHotEdge : Ui.FlatWellEdge;
        }

        private bool UpdateOpeningAgencyGuide()
        {
            bool show = S.OpeningAgencyGuidePending && !MainMenuOpen && !AnyModalOpen;
            if (show && _openingMealInputBlock.PassThrough == null)
            {
                _questHidden = false;
                UpdateQuestTracker();
                show = !AnyModalOpen && _questActionButton.gameObject.activeInHierarchy;
            }
            SetOpeningAgencyGuideVisible(show);
            if (!show) return false;

            _guideRoot.gameObject.SetActive(true);
            _openingMealInputBlock.gameObject.SetActive(true);
            _guideCard.gameObject.SetActive(false);
            _guideArrow.gameObject.SetActive(false);
            foreach (var dim in _guideDim) dim.raycastTarget = false;
            var size = ((RectTransform)_tutorialCanvas.transform).rect.size;
            DrawGuideSpotlight(size, GuideTargetBounds(_questPanel.rectTransform, size, 6));
            return true;
        }

        private void SetOpeningAgencyGuideVisible(bool show)
        {
            if (show == (_openingMealInputBlock.PassThrough != null)) return;
            if (show)
            {
                _openingMealInputBlock.PassThrough = _questActionButton;
                _questActionNavigation = _questActionButton.navigation;
                _questActionButton.navigation = new Navigation { mode = Navigation.Mode.None };
                EventSystem.current?.SetSelectedGameObject(null);
                _openingMealSession.Time.Hold();
            }
            else
            {
                _openingMealInputBlock.PassThrough = null;
                _questActionButton.navigation = _questActionNavigation;
                _openingMealSession.Time.Release();
            }
        }

        private readonly Vector3[] _guideTargetCorners = new Vector3[4];

        private Rect GuideTargetBounds(RectTransform target, Vector2 size, float padding)
        {
            target.GetWorldCorners(_guideTargetCorners);
            var canvas = (RectTransform)_tutorialCanvas.transform;
            var sourceCanvas = target.GetComponentInParent<Canvas>();
            var sourceCam = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
            Vector2 low = GuidePoint(canvas, RectTransformUtility.WorldToScreenPoint(sourceCam, _guideTargetCorners[0]), size);
            Vector2 high = GuidePoint(canvas, RectTransformUtility.WorldToScreenPoint(sourceCam, _guideTargetCorners[2]), size);
            return Rect.MinMaxRect(low.x - padding, low.y - padding, high.x + padding, high.y + padding);
        }

        private void DrawGuideSpotlight(Vector2 size, Rect? spotlight)
        {
            if (spotlight is Rect hole)
            {
                float x0 = Mathf.Clamp(hole.xMin, 0, size.x), x1 = Mathf.Clamp(hole.xMax, 0, size.x);
                float y0 = Mathf.Clamp(hole.yMin, 0, size.y), y1 = Mathf.Clamp(hole.yMax, 0, size.y);
                GuideRect(_guideHole, x0, y0, x1 - x0, y1 - y0);
                GuideRect(_guideDim[0].rectTransform, 0, y1, size.x, size.y - y1);
                GuideRect(_guideDim[1].rectTransform, 0, 0, size.x, y0);
                GuideRect(_guideDim[2].rectTransform, 0, y0, x0, y1 - y0);
                GuideRect(_guideDim[3].rectTransform, x1, y0, size.x - x1, y1 - y0);
                GuideRect(_guideFrame[0].rectTransform, x0 - 4, y1, x1 - x0 + 8, 4);
                GuideRect(_guideFrame[1].rectTransform, x0 - 4, y0 - 4, x1 - x0 + 8, 4);
                GuideRect(_guideFrame[2].rectTransform, x0 - 4, y0, 4, y1 - y0);
                GuideRect(_guideFrame[3].rectTransform, x1, y0, 4, y1 - y0);
                _guideArrow.anchoredPosition = new Vector2((x0 + x1) * .5f, y1 + 10);
            }
            else
            {
                GuideRect(_guideDim[0].rectTransform, 0, 0, size.x, size.y);
                for (int i = 1; i < 4; i++) GuideRect(_guideDim[i].rectTransform, 0, 0, 0, 0);
                foreach (var f in _guideFrame) GuideRect(f.rectTransform, 0, 0, 0, 0);
                _guideArrow.gameObject.SetActive(false);
            }
        }

        private Vector2 GuidePoint(RectTransform canvas, Vector2 screen, Vector2 size)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, TutorialCam, out var local);
            return local + size * .5f;
        }

        private (string title, string body) OpeningMealControlText(int step)
        {
            if (step == 1) return ("第 4 步 · 等饭焖好", $"饭正在焖，还剩约 <b>{S.Pantry.CookMinutesLeft(S.Clock.Now.TotalMinutes)} 分钟</b>。\n点高亮的 <b>“等到饭好”</b>，推进到这锅饭煮好。\n食材已经下锅，不需要再放一次。");
            if (step == 2) return ("第 5 步 · 先吃一碗", "饭煮好了，先照顾好自己。\n点 <b>“现在吃一碗”</b>，恢复饱腹。\n吃完后，再把剩饭带上。");
            if (step == 3) return ("第 6 步 · 带上剩饭", "肚子不饿了，带上剩饭作午饭。\n点 <b>“全部装进背包”</b>。\n饭点或饿了会自动吃，不用手动操作。");
            if (_openingMealCookStage == 0) return ("第 1 步 · 选米量", $"点击高亮区域里的米量。\n第一次推荐 <b>0.5 千克</b>，大约能做 <b>{PCShopSell.Core.Home.Pantry.BowlsPerKg / 2} 碗饭</b>。\n大米自动从背包和家中后仓取用，现在选择不会扣食材。");
            if (_openingMealCookStage == 1) return ("第 2 步 · 选做法", "点击高亮区域里的做法。\n先学做 <b>白饭</b> 就可以，不需要额外配菜。\n有食材时也能选择焖饭；灰色选项表示食材不足。");
            return ("第 3 步 · 开始焖饭", $"确认选好 <b>{_cookKg:0.##} 千克大米</b>和做法后，点击高亮的 <b>“开始焖饭”</b>。\n这时才会消耗所选食材，约 <b>{PCShopSell.Core.Home.Pantry.CookMinutes} 分钟</b>后做好。");
        }

        private Camera TutorialCam => _tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _tutorialCanvas.worldCamera;

        private (string title, string body) OpeningMealGuideText(int step)
        {
            switch (step)
            {
                case 0:
                    return ("第 1 步 · 认识电饭锅，选米与做法",
                        "用 <b>WASD / 方向键</b> 走到高亮处，出现电饭锅提示后按 <b>F</b>。\n在锅里依次点 <b>米量（推荐 0.5 千克）→ 白饭或有食材的做法 → 开始焖饭</b>。\n食材自动从背包和家中后仓取用，不用先搬东西。");
                case 1:
                    long left = S.Pantry.Cooking != null ? S.Pantry.CookMinutesLeft(S.Clock.Now.TotalMinutes) : 0;
                    return ("第 2 步 · 等饭焖好",
                        $"饭正在焖，还要约 <b>{left}</b> 分钟，焖好之前锅盖打不开。\n在电饭锅窗口点 <b>“等到饭好”</b>，只推进剩余时间，不会提前获得熟饭。\n也可以关窗等候；这段教学先不做其他事情。");
                case 2:
                    return ("饭好了 · 先吃一碗",
                        "饭好了！打开电饭锅，点 <b>“现在吃一碗”</b>，消耗一碗并恢复饱腹。\n吃完后还要把剩饭全部装进背包，留作午饭。");
                default: return ("吃饱了 · 带上剩饭", "再次打开电饭锅，点击 <b>“全部装进背包”</b>。\n带上饭盒，之后饭点或饿了会自动吃。装完后就可以出门了。");
            }
        }

    }
}
