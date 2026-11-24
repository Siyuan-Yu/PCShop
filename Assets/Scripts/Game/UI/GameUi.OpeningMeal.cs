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
        public IReadOnlyList<Button> PassThroughButtons { get; set; }
        public RectTransform PassThroughArea { get; set; }
        public RectTransform PassThroughViewport { get; set; }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (PassThrough == null) return base.IsRaycastLocationValid(screenPoint, eventCamera);
            var canvas = PassThrough.targetGraphic.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (PassThroughButtons != null)
            {
                foreach (var button in PassThroughButtons)
                {
                    if (button == null || !button.IsInteractable() || !button.gameObject.activeInHierarchy) continue;
                    var buttonRect = (RectTransform)button.transform;
                    if (RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPoint, camera)
                        && (PassThroughViewport == null || !buttonRect.IsChildOf(PassThroughViewport)
                            || RectTransformUtility.RectangleContainsScreenPoint(PassThroughViewport, screenPoint, camera)))
                        return false;
                }
                return true;
            }
            var rect = PassThroughArea != null ? PassThroughArea : (RectTransform)PassThrough.transform;
            return !RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera)
                || (PassThroughViewport != null
                    && !RectTransformUtility.RectangleContainsScreenPoint(PassThroughViewport, screenPoint, camera));
        }
    }

    internal sealed class RecruitmentGuideOutline : MaskableGraphic
    {
        internal readonly List<Rect> Buttons = new List<Rect>();

        internal void Refresh(Color edgeColor)
        {
            color = edgeColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var surface = rectTransform.rect;
            const float edge = 3;
            foreach (var button in Buttons)
            {
                Quad(button.xMin, button.yMax, button.xMax, button.yMax + edge, color);
                Quad(button.xMin, button.yMin - edge, button.xMax, button.yMin, color);
                Quad(button.xMin - edge, button.yMin - edge, button.xMin, button.yMax + edge, color);
                Quad(button.xMax, button.yMin - edge, button.xMax + edge, button.yMax + edge, color);
            }

            void Quad(float left, float bottom, float right, float top, Color tint)
            {
                if (right <= left || top <= bottom) return;
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(left + surface.xMin, bottom + surface.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(left + surface.xMin, top + surface.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right + surface.xMin, top + surface.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right + surface.xMin, bottom + surface.yMin), tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
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
        private const double OpeningMealRiceKg = 1.5;
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
            RestoreOpeningMealButtons();
            _openingMealCooker = w;
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
            ? "等饭自然煮好，再打开电饭锅吃一碗、打包剩饭。"
            : S.OpeningMealStep >= 2 ? (S.OpeningMealEaten ? "已经吃过饭了，打开电饭锅，把剩饭全部装进背包。" : "饭好了，打开电饭锅，先吃一碗。")
            : "走近高亮的电饭锅，按 F，选好米量和做法后开始焖饭。";

        private void BuildOpeningMealReminder(Window w)
        {
            Para(w.Content, "先完成这一顿饭，再做其他事情。", 22, Ui.Accent);
            Para(w.Content, OpeningMealNextAction, 19);
            Para(w.Content, "可以继续走路、调整镜头；Esc 仍可保存、设置或退出。", 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, "知道了，先做饭", w.Close, 19), -1, 46);
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
        private RectTransform _guideRoot, _guideHole, _guideArrow, _guideCard, _openingHungerIllustration;
        private readonly Image[] _guideDim = new Image[4];
        private readonly Image[] _guideFrame = new Image[4];
        private Text _guideTitle, _guideBody, _guideFoot;

        private static readonly Color GuideDimColor = new Color(.10f, .15f, .11f, .78f);
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

            var arrow = Ui.Label(_guideRoot, "▼ 电饭锅", 26, TextAnchor.LowerCenter, Ui.ChromeGold);
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

            _guideTitle = Ui.Label(_guideCard, "", 24, TextAnchor.MiddleLeft, Ui.Accent);
            _guideTitle.fontStyle = FontStyle.Bold; _guideTitle.raycastTarget = false;
            _guideTitle.rectTransform.Anchor(0, 1, 1, 1, 22, -48, 20, 12);
            _guideBody = Ui.Label(_guideCard, "", 20, TextAnchor.UpperLeft);
            _guideBody.raycastTarget = false;
            _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, 44, 22, 56);
            _guideFoot = Ui.Label(_guideCard, "", 17, TextAnchor.MiddleLeft, Ui.Warn);
            _guideFoot.raycastTarget = false;
            _guideFoot.rectTransform.Anchor(0, 0, 1, 0, 22, 10, 22, -38);
            if (Resources.Load<Texture2D>("EventCg/opening_hunger") is Texture2D hungerArt)
            {
                _openingHungerIllustration = Ui.Rect(_guideCard, "OpeningHungerIllustration");
                EventCgImage(_openingHungerIllustration, "RiceCooker", hungerArt);
                _openingHungerIllustration.gameObject.SetActive(false);
            }
            _openingMealIntroNext = Ui.Button(_guideCard, "先找点吃的", () =>
            {
                S.Flags.Add(OpeningMealIntroPending ? OpeningMealIntroSeen : OpeningHungerSeen);
                UpdateOpeningMealGuide();
            }, 19, Ui.FlatRow);
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

        private Button _mealDeliveryGuideButton;
        private Window _openingRenovationConfirmWindow;
        private Button _openingRenovationStartButton, _openingRenovationConfirmButton;
        private Window _openingShelfChooser;
        private Button _openingShelfCategoryButton, _openingShelfKindOption;
        private readonly Dictionary<int, Button> _openingCounterButtons = new Dictionary<int, Button>();
        private System.Action _refreshOpeningRestaurantTables;
        private (int id, RestaurantJob job)? _openingCounterPendingAction, _openingCounterRejectedAction;
        private Window _openingStaffLessonWindow;
        private Button _openingStaffDockControl, _openingStaffLessonDone, _openingStaffFirstApplicant;
        private Button _openingStaffRecruitmentNotice, _openingStaffPurchaseButton;
        private RectTransform _openingStaffCandidates;
        private RectTransform _firstUseGuideRoot, _firstUseGuideCard;
        private Text _firstUseGuideText, _firstUseGuideArrow;
        private readonly Image[] _firstUseGuideFrame = new Image[4];
        private readonly Image[] _firstUseGuideDim = new Image[4];
        private RecruitmentGuideOutline _recruitmentOutline;
        private OpeningGuideInputBlock _firstUseInputBlock;
        private Button _requiredGuideTarget;
        private Navigation _requiredGuideNavigation;
        private RectTransform _requiredGuideArea;
        public bool RequiredTutorialControlVisible => _requiredGuideTarget != null;

        private void UpdateFirstUseGuide()
        {
            bool meal = S.MealDeliveryTutorialPaused;
            bool scooter = S.ScooterTutorialPaused;
            bool bank = !scooter && S.BankTutorialPaused;
            bool canShow = !MainMenuOpen && !S.IsGameOver && !S.IsWorking && !S.Racing && !_ctrl.Placing
                && !_ctrl.PetFocusActive && !PopupOpen && !QuestAnnouncementBlocksGuide && S.Trip == null
                && (_guideRoot == null || !_guideRoot.gameObject.activeSelf);
            if (meal && canShow && !OpeningMealRestricted && _mealDeliveryTutorialWindow?.Closed != false)
            {
                OpenMealDeliverySettings(null);
                Canvas.ForceUpdateCanvases();
            }
            Button target = null;
            RectTransform targetArea = null;
            string text = null;
            Window top = AnyModalOpen ? _windows[_windows.Count - 1] : null;
            bool recruiting = top != null && top == _openingRecruitWindow;
            bool showLesson = false;
            bool counterLesson = false;
            bool mallTravelLesson = false;
            bool petIntakeLesson = canShow && top == null && _ctrl.InInterior && !_ctrl.CheckoutPending
                && !_ctrl.OpeningPetIntakeInstructionFollowed && S.OpeningPetIntakeLessonAvailable
                && S.OpeningStaffStep == OpeningStaffLesson.None;
            if (meal)
            {
                if (top != null && top == _mealDeliveryTutorialWindow) target = _mealDeliveryGuideButton;
                text = "点「开启自动订餐」继续\n缺饭时按选定餐档下单";
            }
            else if (scooter && top == null)
            {
                target = _vehicleButtons.FirstOrDefault(entry => entry.kind == VehicleKind.EBike).b;
                text = "教学暂停 · 点「小电驴」\n切换出行方式后继续";
            }
            else if (bank)
            {
                if (top == null && canShow)
                {
                    OpenBank(S.CurrentPlace);
                    Canvas.ForceUpdateCanvases();
                    top = _bankWindow;
                }
                if (top != null && top == _stockWindow && _stockAtBank)
                {
                    target = _securitiesLessonButton;
                    showLesson = true;
                    text = "看完说明，点「已了解，继续开店」";
                }
                else if (top != null && top == _bankWindow)
                {
                    if (S.BankBorrowTutorialPending)
                    {
                        showLesson = _bankBorrowLessonButton != null;
                        target = showLesson ? _bankBorrowLessonButton : _bankBorrowTab;
                        text = showLesson ? "看完借款说明，点「已了解借款」" : "教学暂停 · 点「借款」";
                    }
                    else
                    {
                        target = _bankSecuritiesTab;
                        text = "教学暂停 · 点「证券交易」";
                    }
                }
            }
            else if (canShow && top == null && !PropertyContractOpen && _propertyContractPopups.Count == 0
                && _scheduledPropertyPopups.Count == 0 && S.OpeningMallTravelPending)
            {
                bool focusChanged = !ReferenceEquals(_questActionButton, _requiredGuideTarget);
                if (focusChanged || !_questPanel.gameObject.activeSelf || _questSeen != "mall")
                    UpdateQuestTracker();
                if (_questPanel.gameObject.activeSelf && _questSeen == "mall" && _questActionText.text == OpeningMallTravelLabel)
                {
                    if (focusChanged) Canvas.ForceUpdateCanvases();
                    target = _questActionButton;
                    text = "点「前往家具城」\n去买齐开店设施";
                    mallTravelLesson = true;
                }
            }
            else if (canShow && S.OpeningRenovationStep is var renovation && renovation != OpeningRenovationLesson.None)
            {
                (target, text) = OpeningRenovationGuideTarget(renovation, top);
            }
            else if (canShow && S.OpeningShelfTutorialPaused)
            {
                (target, text) = OpeningShelfGuideTarget(top);
            }
            else if (canShow && _ctrl.InInterior && (top == null || top == _openingUsedDeskWindow)
                && S.OpeningUsedDeskLessonItem() is UsedItem usedItem && !_ctrl.CheckoutPending)
            {
                (target, text) = OpeningUsedDeskGuideTarget(usedItem, top);
                showLesson = top != null;
                counterLesson = true;
            }
            else if (canShow && top == null && _ctrl.InInterior && (petIntakeLesson || S.OpeningCounterLessonAvailable))
            {
                (target, text) = OpeningCounterGuideTarget(petIntakeLesson);
                counterLesson = true;
            }
            else if (canShow && S.OpeningStaffStep is OpeningStaffLesson staffing && staffing != OpeningStaffLesson.None)
            {
                if (top == null)
                {
                    bool open = DockOpen;
                    if (!open) OpenDock();
                    if (staffing == OpeningStaffLesson.StaffTab)
                    {
                        target = _tabButtons.FirstOrDefault(entry => entry.tab == ShopTab.Staff).b;
                        text = "教学暂停 · 点「员工」\n看看这家店需要哪些岗位";
                    }
                    else
                    {
                        if (_tab != ShopTab.Staff) SelectTab(ShopTab.Staff);
                        if (staffing == OpeningStaffLesson.Hire)
                        {
                            target = _openingStaffFirstApplicant;
                            targetArea = _openingStaffCandidates;
                            text = "选一位应聘者\n点「聘用并安排工作」";
                        }
                        else if (staffing == OpeningStaffLesson.PostRecruitment)
                        {
                            target = _openingStaffRecruitmentNotice;
                            text = "在员工页点「贴招聘告示」\n首批应聘者明天 09:00 到店";
                        }
                        else
                        {
                            bool purchase = staffing == OpeningStaffLesson.Purchase;
                            target = purchase ? _openingStaffPurchaseButton : _openingStaffDockControl;
                            showLesson = purchase;
                            text = purchase ? "在进货员这一行\n点「安排进货」，设置采购清单"
                                : "点「查看本店岗位」\n了解每个人负责的工作";
                        }
                        if (!ReferenceEquals(target, _requiredGuideTarget)) RevealFirstUseDockControl(target);
                    }
                    if (!open || !ReferenceEquals(target, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                }
                else if (top == _openingStaffLessonWindow && staffing == OpeningStaffLesson.Roles)
                {
                    target = _openingStaffLessonDone;
                    showLesson = true;
                    text = "看完本店分工后，点「已了解分工」";
                }
                else if (recruiting && staffing == OpeningStaffLesson.Hire)
                {
                    target = _openingRecruitTarget;
                    targetArea = _openingRecruitRoleArea;
                    showLesson = true;
                    text = _openingRecruitInstruction;
                    if (!ReferenceEquals(target, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                }
            }
            else if (canShow && S.VenueSponsorGuidePaused)
            {
                (target, text) = VenueSponsorGuideTarget(top);
                top = AnyModalOpen ? _windows[_windows.Count - 1] : null;
            }
            bool show = canShow && target != null && target.IsInteractable() && target.gameObject.activeInHierarchy
                && (top == null || target.transform.IsChildOf(top.Root));
            S.FocusOpeningMallTutorial(show && mallTravelLesson);
            S.FocusOpeningCounterTutorial(show && counterLesson);
            var allowedButtons = recruiting ? _openingRecruitChoices : null;
            SetRequiredGuideTarget(show ? target : null, show ? targetArea : null, allowedButtons);
            if (_firstUseGuideRoot != null) _firstUseGuideRoot.gameObject.SetActive(show);
            if (!show || _tutorialCanvas == null) return;
            if (_firstUseGuideRoot == null)
            {
                _firstUseGuideRoot = Ui.Rect(_tutorialCanvas.transform, "FirstUseGuide").Fill();
                _firstUseInputBlock = Ui.Rect(_firstUseGuideRoot, "FirstUseInputBlock").gameObject.AddComponent<OpeningGuideInputBlock>();
                _firstUseInputBlock.color = Color.clear;
                _firstUseInputBlock.rectTransform.Fill();
                for (int i = 0; i < _firstUseGuideFrame.Length; i++)
                {
                    var dim = _firstUseGuideDim[i] = Ui.Panel(_firstUseGuideRoot, "FirstUseDim" + i, GuideDimColor);
                    dim.raycastTarget = false;
                    dim.rectTransform.anchorMin = dim.rectTransform.anchorMax = Vector2.zero;
                    dim.rectTransform.pivot = Vector2.zero;
                }
                for (int i = 0; i < _firstUseGuideFrame.Length; i++)
                {
                    var frame = _firstUseGuideFrame[i] = Ui.Panel(_firstUseGuideRoot, "FirstUseFrame" + i, Ui.FlatHotEdge);
                    frame.raycastTarget = false;
                    frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = Vector2.zero;
                    frame.rectTransform.pivot = Vector2.zero;
                }
                _recruitmentOutline = Ui.Rect(_firstUseGuideRoot, "RecruitmentSpotlight").Fill().gameObject
                    .AddComponent<RecruitmentGuideOutline>();
                _recruitmentOutline.raycastTarget = false;
                var card = Ui.FlatPanel(_firstUseGuideRoot, "FirstUseHint", Ui.PanelBg, Ui.FlatHotEdge);
                card.raycastTarget = false;
                _firstUseGuideCard = card.rectTransform;
                _firstUseGuideCard.anchorMin = _firstUseGuideCard.anchorMax = Vector2.zero;
                _firstUseGuideCard.pivot = Vector2.zero;
                _firstUseGuideText = Ui.Label(_firstUseGuideCard, "", 19, TextAnchor.MiddleCenter, Ui.Accent);
                _firstUseGuideText.raycastTarget = false;
                _firstUseGuideText.rectTransform.Fill(10);
                _firstUseGuideArrow = Ui.Label(_firstUseGuideRoot, "▼", 25, TextAnchor.MiddleCenter, Ui.ChromeGold);
                _firstUseGuideArrow.raycastTarget = false;
                var arrow = _firstUseGuideArrow.rectTransform;
                arrow.anchorMin = arrow.anchorMax = Vector2.zero;
                arrow.pivot = Vector2.zero;
            }
            _firstUseGuideRoot.SetAsLastSibling();
            _firstUseInputBlock.PassThrough = target;
            _firstUseInputBlock.PassThroughButtons = allowedButtons;
            var groupScroll = targetArea != null ? targetArea.GetComponentInParent<ScrollRect>() : null;
            var viewport = groupScroll != null ? groupScroll.viewport : null;
            _firstUseInputBlock.PassThroughArea = targetArea;
            _firstUseInputBlock.PassThroughViewport = viewport;
            if (viewport != null)
            {
                var group = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, targetArea);
                float topGap = viewport.rect.yMax - group.max.y;
                float bottomGap = viewport.rect.yMin - group.min.y;
                float offset = Mathf.Clamp(0, Mathf.Min(topGap, bottomGap), Mathf.Max(topGap, bottomGap));
                float overflow = groupScroll.content.rect.height - viewport.rect.height;
                if (overflow > 0 && Mathf.Abs(offset) > .5f)
                {
                    groupScroll.StopMovement();
                    groupScroll.verticalNormalizedPosition = Mathf.Clamp01(groupScroll.verticalNormalizedPosition - offset / overflow);
                }
            }
            var size = ((RectTransform)_tutorialCanvas.transform).rect.size;
            Rect hole = GuideTargetBounds(targetArea != null ? targetArea : (RectTransform)target.transform, size, 5);
            Rect viewportBounds = default;
            if (viewport != null)
            {
                viewportBounds = GuideTargetBounds(viewport, size, 0);
                hole = IntersectGuideBounds(hole, viewportBounds);
            }
            Color color = Ui.FlatHotEdge;
            color.a = .6f + .4f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4));
            _recruitmentOutline.gameObject.SetActive(recruiting);
            foreach (var frame in _firstUseGuideFrame) frame.gameObject.SetActive(!recruiting);
            if (recruiting)
            {
                var buttons = _recruitmentOutline.Buttons;
                buttons.Clear();
                foreach (var choice in allowedButtons)
                {
                    var bounds = GuideTargetBounds((RectTransform)choice.transform, size, .75f);
                    if (viewport != null && choice.transform.IsChildOf(viewport)) bounds = IntersectGuideBounds(bounds, viewportBounds);
                    if (bounds.width > 0 && bounds.height > 0) buttons.Add(bounds);
                }
                _recruitmentOutline.Refresh(color);
            }
            // Keep the lesson text legible while its confirmation button is highlighted.
            Rect spotlight = showLesson
                ? GuideTargetBounds(recruiting ? (RectTransform)top.Fitter.transform
                    : top != null && top == _openingUsedDeskWindow ? (RectTransform)top.Content.parent
                    : (RectTransform)target.transform.parent, size, 6)
                : hole;
            GuideRect(_firstUseGuideDim[0].rectTransform, 0, spotlight.yMax, size.x, size.y - spotlight.yMax);
            GuideRect(_firstUseGuideDim[1].rectTransform, 0, 0, size.x, spotlight.yMin);
            GuideRect(_firstUseGuideDim[2].rectTransform, 0, spotlight.yMin, spotlight.xMin, spotlight.height);
            GuideRect(_firstUseGuideDim[3].rectTransform, spotlight.xMax, spotlight.yMin, size.x - spotlight.xMax, spotlight.height);
            const float edge = 3;
            GuideRect(_firstUseGuideFrame[0].rectTransform, hole.xMin, hole.yMax, hole.width, edge);
            GuideRect(_firstUseGuideFrame[1].rectTransform, hole.xMin, hole.yMin - edge, hole.width, edge);
            GuideRect(_firstUseGuideFrame[2].rectTransform, hole.xMin - edge, hole.yMin - edge, edge, hole.height + edge * 2);
            GuideRect(_firstUseGuideFrame[3].rectTransform, hole.xMax, hole.yMin - edge, edge, hole.height + edge * 2);
            foreach (var frame in _firstUseGuideFrame) frame.color = color;
            float width = Mathf.Min(330, size.x - 24);
            float x = Mathf.Clamp(hole.center.x - width * .5f, 12, size.x - width - 12);
            bool above = !showLesson && hole.yMax + 116 < size.y;
            float y = above ? hole.yMax + 28 : hole.yMin - 104;
            if (recruiting)
            {
                above = spotlight.yMax + 116 < size.y;
                y = above ? spotlight.yMax + 20 : spotlight.yMin - 100;
            }
            GuideRect(_firstUseGuideCard, x, Mathf.Clamp(y, 12, size.y - 92), width, 80);
            _firstUseGuideText.text = text;
            bool arrowAbove = recruiting || above;
            _firstUseGuideArrow.text = arrowAbove ? "▼" : "▲";
            Rect arrowTarget = recruiting ? GuideTargetBounds((RectTransform)target.transform, size, .75f) : hole;
            GuideRect(_firstUseGuideArrow.rectTransform, arrowTarget.center.x - 16,
                arrowAbove ? arrowTarget.yMax + 2 : arrowTarget.yMin - 28, 32, 26);
        }

        private static Rect IntersectGuideBounds(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));

        private (Button target, string text) OpeningRenovationGuideTarget(OpeningRenovationLesson step, Window top)
        {
            if (top != null)
                return top == _openingRenovationConfirmWindow
                    ? (_openingRenovationConfirmButton, "确认歇业后进入装修\n今天可以摆放开店设施") : (null, null);

            if (step == OpeningRenovationLesson.Travel)
            {
                if (!_questPanel.gameObject.activeSelf || _questSeen != "place" || _questActionText.text != "前往店铺")
                    UpdateQuestTracker();
                if (_questSeen != "place" || _questActionText.text != "前往店铺") return (null, null);
                if (!ReferenceEquals(_questActionButton, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                return (_questActionButton, "家具已买齐\n点「前往店铺」出发");
            }

            if (!DockOpen) OpenDock();
            if (step == OpeningRenovationLesson.RenovationTab)
            {
                var tab = _tabButtons.FirstOrDefault(entry => entry.tab == ShopTab.Renovate).b;
                if (!ReferenceEquals(tab, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                return (tab, "已经到店了\n点「装修」查看开业设施清单");
            }

            if (_tab != ShopTab.Renovate || _openingRenovationStartButton == null || !_openingRenovationStartButton.gameObject.activeInHierarchy)
                SelectTab(ShopTab.Renovate);
            RevealFirstUseDockControl(_openingRenovationStartButton);
            string label = _openingRenovationStartButton?.GetComponentInChildren<Text>().text;
            return (_openingRenovationStartButton, $"点「{label}」\n进入摆放模式，布置开店设施");
        }

        private (Button target, string text) OpeningShelfGuideTarget(Window top)
        {
            if (top != null)
            {
                if (top != _openingShelfChooser) return (null, null);
                string name = S.OpeningShelfTutorialKind()?.Name;
                string explanation = S.RetailKind == "restaurant" ? "堂食顾客可以加购货架上的饮料" : "后仓有货时会自动摆上货架";
                return (_openingShelfKindOption, $"点「{name}」设置这架货架\n{explanation}");
            }

            if (!DockOpen) OpenDock();
            if (!S.OpeningShelfStockVisited)
            {
                var inventory = _tabButtons.FirstOrDefault(entry => entry.tab == ShopTab.Stock).b;
                if (!ReferenceEquals(inventory, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                return (inventory, "教学暂停 · 点「库存」\n设置店里的第一架货架");
            }

            if (_tab != ShopTab.Stock || _openingShelfCategoryButton == null || !_openingShelfCategoryButton.gameObject.activeInHierarchy)
                SelectTab(ShopTab.Stock);
            RevealFirstUseDockControl(_openingShelfCategoryButton);
            string label = S.ShelfKindAt(S.OpeningShelfTutorialIndex())?.Name ?? "选择摆什么";
            return (_openingShelfCategoryButton, $"点「{label}」\n选择货架上要摆的商品品类");
        }

        private (Button target, string text) OpeningCounterGuideTarget(bool petIntake)
        {
            if (_ctrl.CheckoutPending) return (null, null);
            if (DockOpen && (ChoiceDropdown.OpenInside(_dock) || ListScrollbar.PressedInside(_dock))) return (null, null);
            bool restaurant = S.RetailKind == "restaurant";
            if (!restaurant && (!_ctrl.CanRequestCheckout || (!petIntake && _ctrl.OpeningCheckoutInstructionFollowed))) return (null, null);
            var (order, job) = restaurant ? S.OpeningRestaurantCounterAction() : default;
            if (order != null && _openingCounterPendingAction is { } pending)
            {
                // The actor is idle again. If the same job is still offered, let the player adjust the layout or retry freely.
                if (pending == (order.Id, job)) _openingCounterRejectedAction = pending;
                _openingCounterPendingAction = null;
            }
            var queue = S.PlayerShop.Queue;
            if (!petIntake && (restaurant ? order == null || _openingCounterRejectedAction == (order.Id, job) : queue.Count == 0))
                return (null, null);

            if (!S.OpeningCounterTabVisited)
            {
                if (!DockOpen) OpenDock();
                var tab = _tabButtons.FirstOrDefault(entry => entry.tab == ShopTab.Counter).b;
                if (!ReferenceEquals(tab, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                string prompt = petIntake ? "有主人带宠物来寄养\n点「柜台」查看送养订单"
                    : restaurant ? "客人已经下单\n点「柜台」查看餐桌订单" : "有顾客在排队\n点「柜台」查看待结账顾客";
                return (tab, prompt);
            }

            if (!DockOpen) OpenDock();
            if (_tab != ShopTab.Counter) SelectTab(ShopTab.Counter);
            if (petIntake)
            {
                if (_openingPetIntakeButton == null || !ReferenceEquals(_openingPetIntakeButton, _requiredGuideTarget))
                    RefreshDock();
                RevealFirstUseDockControl(_openingPetIntakeButton);
                return (_openingPetIntakeButton, $"点「{PetIntakeLabel}」\n角色会到收银台办理寄养");
            }

            int key = restaurant ? order.Table : queue[0].Id;
            if (restaurant && (!_openingCounterButtons.TryGetValue(key, out var current) || !ReferenceEquals(current, _requiredGuideTarget)))
                _refreshOpeningRestaurantTables?.Invoke();
            if (!_openingCounterButtons.TryGetValue(key, out var action) || !action.IsInteractable())
            {
                RefreshDock();
                if (!_openingCounterButtons.TryGetValue(key, out action) || !action.IsInteractable()) return (null, null);
            }
            RevealFirstUseDockControl(action);
            if (!restaurant) return (action, "点「结账」\n角色会到收银台交货、收款");

            string caption = action.GetComponentInChildren<Text>().text;
            string hint = job switch
            {
                RestaurantJob.Cook => "角色会到灶台备料并下锅",
                RestaurantJob.Serve => "把做好的菜送到餐桌",
                RestaurantJob.Clear when order.Stage == DiningStage.Burnt => "清锅后可以重新备料",
                _ => "客人吃完了，收桌并收款"
            };
            return (action, $"点「{caption}」 · {order.Table + 1}号桌\n{hint}");
        }

        private (Button target, string text) OpeningUsedDeskGuideTarget(UsedItem item, Window top)
        {
            if (top != null)
            {
                string instruction = !item.Owned ? $"核对卖家要价 ¥{item.Asking:N0}，点「出价收购」"
                    : !item.Inspected ? "点「检查状态」，查看成色和参考售价"
                    : "核对参考售价，点「确认售价并上架」";
                return (_openingUsedDeskAction, instruction);
            }

            if (DockOpen && (ChoiceDropdown.OpenInside(_dock) || ListScrollbar.PressedInside(_dock))) return (null, null);
            if (!DockOpen) OpenDock();
            if (!S.OpeningCounterTabVisited)
            {
                var tab = _tabButtons.FirstOrDefault(entry => entry.tab == ShopTab.Counter).b;
                if (!ReferenceEquals(tab, _requiredGuideTarget)) Canvas.ForceUpdateCanvases();
                return (tab, item.Owned ? "还有旧物待处理\n点「柜台」继续" : "有卖家带旧物来了\n点「柜台」查看收购情况");
            }

            if (_tab != ShopTab.Counter) SelectTab(ShopTab.Counter);
            RevealFirstUseDockControl(_openingUsedDeskButton);
            return (_openingUsedDeskButton, "点「打开收购鉴定台」\n在这里收购、鉴定并上架旧物");
        }

        private void RevealFirstUseDockControl(Button button)
        {
            if (button == null || ReferenceEquals(button, _requiredGuideTarget)) return;
            Canvas.ForceUpdateCanvases();
            for (Transform parent = button.transform.parent; parent != null && parent != _dock; parent = parent.parent)
            {
                if (!parent.TryGetComponent<ScrollRect>(out var scroll)) continue;
                var visible = scroll.viewport.rect;
                float overflow = scroll.content.rect.height - visible.height;
                if (overflow <= 0) continue;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                float offset = bounds.min.y < visible.yMin ? visible.yMin - bounds.min.y + 8
                    : Mathf.Min(0, visible.yMax - bounds.max.y);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - offset / overflow);
            }
        }

        private void SetRequiredGuideTarget(Button target, RectTransform area, IReadOnlyList<Button> allowedButtons)
        {
            if (!ReferenceEquals(_requiredGuideTarget, target) || !ReferenceEquals(_requiredGuideArea, area))
            {
                if (_requiredGuideTarget != null && _requiredGuideArea == null) _requiredGuideTarget.navigation = _requiredGuideNavigation;
                _requiredGuideTarget = target;
                _requiredGuideArea = area;
                if (target != null && area == null)
                {
                    _requiredGuideNavigation = target.navigation;
                    target.navigation = new Navigation { mode = Navigation.Mode.None };
                }
            }
            if (target == null) return;
            var events = EventSystem.current;
            if (events != null && !events.alreadySelecting && events.currentSelectedGameObject != target.gameObject)
            {
                var selected = events.currentSelectedGameObject;
                if (selected != null && (allowedButtons != null
                    ? allowedButtons.Any(button => button.gameObject == selected)
                    : area != null && selected.transform.IsChildOf(area))) return;
                events.SetSelectedGameObject(target.gameObject);
            }
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
            // Waiting uses the ordinary cooker UI and world clock. A teaching
            // spotlight would obscure the close button and the time controls.
            if(step==1)
            {
                RestoreOpeningMealButtons();
                _openingMealFocus=null;
                _guideRoot.gameObject.SetActive(false);
                return;
            }
            _guideRoot.gameObject.SetActive(show);
            if (!show) { if (step < 0) RestoreOpeningMealButtons(); return; }
            _guideCard.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var canvasRt = (RectTransform)_tutorialCanvas.transform;
            Vector2 size = canvasRt.rect.size;
            _openingMealInputBlock.gameObject.SetActive(!cookerOpen);
            foreach (var dim in _guideDim) dim.raycastTarget = cookerOpen;
            _openingMealIntroNext.gameObject.SetActive(intro);
            _guideArrow.gameObject.SetActive(!intro && !cookerOpen);
            _guideCard.anchorMin = _guideCard.anchorMax = Vector2.zero;
            _guideCard.pivot = Vector2.zero;
            _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, intro ? 78 : 66, 22, 62);
            bool compactCookerGuide = cookerOpen && (step == 0 || step == 2);
            _guideFoot.gameObject.SetActive(!intro && !movement && !compactCookerGuide);
            bool showHungerIllustration = intro && hunger && _openingHungerIllustration != null;
            if (_openingHungerIllustration != null)
                _openingHungerIllustration.gameObject.SetActive(showHungerIllustration);
            Rect hole = new Rect(); bool hasHole = false;
            (string title, string body) text = OpeningMealGuideText(step);
            if (intro)
            {
                if (showHungerIllustration)
                {
                    float width = Mathf.Min(720, size.x - 32);
                    float textAndControlsHeight = width < 620 ? 292 : 250;
                    float illustrationHeight = Mathf.Clamp(size.y - 32 - textAndControlsHeight, 0, Mathf.Max(0, width - 44) * 9 / 16);
                    float height = textAndControlsHeight + illustrationHeight;
                    GuideRect(_guideCard, (size.x - width) * .5f, (size.y - height) * .5f, width, height);
                    _openingHungerIllustration.Anchor(0, 1, 1, 1, 22, -56 - illustrationHeight, 22, 56);
                    _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, 78, 22, 72 + illustrationHeight);
                }
                else GuideRect(_guideCard, (size.x - 620) * .5f, (size.y - 340) * .5f, 620, 340);
                _openingMealFocus = background ? "ArrivalStory" : "HungerStory";
                _openingMealIntroNext.GetComponentInChildren<Text>().text = background ? "看看房间" : "找点吃的";
                text = background
                    ? ("换个地方，重新开始", $"公司裁员，你失去了上一份工作。\n你带着不多的行李来到{Ui.Emphasize("城郊")}，暂时租下了这间老旧的小屋。\n现在手头还有 {Ui.Emphasize(S.Ledger.Balance.ToString())}，往后的日子得慢慢打算。\n先放下行李，看看这个暂时属于自己的落脚处吧。")
                    : ("肚子有点饿了", $"在房间里走了几步，你才发觉自己一路奔波，还没有好好吃过东西。\n肚子饿得直叫。桌上放着{Ui.Emphasize("电饭锅")}，家里也还有大米。\n先别急着出门，给自己做顿热饭吧。");
            }
            else if (movement)
            {
                _openingMealFocus = "Movement";
                GuideRect(_guideCard, 30, 180, 440, 116);
                _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, 10, 22, 62);
                _guideArrow.gameObject.SetActive(false);
                Vector3 sp = _ctrl.Cam.WorldToScreenPoint(_ctrl.HomeRoom.OwnerWorld + Vector3.up * .8f);
                if (sp.z > 0) { var c = GuidePoint(canvasRt, sp, size); hole = new Rect(c - new Vector2(280, 190), new Vector2(560, 380)); hasHole = true; }
                text = ("先在房间里走走", $"按 {Ui.Emphasize("W / A / S / D")} 移动，也可以用 {Ui.Emphasize("方向键")}。");
            }
            else if (cookerOpen)
            {
                string targetName = step == 2 ? "OpeningMealEat" : step == 3 ? "OpeningMealPackAll" :
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
                    var settings = _guideBody.GetGenerationSettings(new Vector2(width - 44, 0));
                    float bodyHeight = _guideBody.cachedTextGeneratorForLayout.GetPreferredHeight(text.body, settings) / _guideBody.pixelsPerUnit;
                    float height = Mathf.Ceil(bodyHeight) + (compactCookerGuide ? 82 : 128);
                    if (compactCookerGuide)
                        _guideBody.rectTransform.Anchor(0, 0, 1, 1, 22, 18, 22, 62);
                    GuideRect(_guideCard, x, Mathf.Clamp(hole.center.y - height * .5f, 80, size.y - height - 50), width, height);
                }
                if (!compactCookerGuide)
                    _guideFoot.text = $"只需点击高亮区域 · {Ui.Emphasize("Esc")} 可暂时关闭窗口";
            }
            else
            {
                _openingMealFocus = "CookerWorld";
                Vector3 sp = _ctrl.HomeRoom.CookerWorld is Vector3 cooker ? _ctrl.Cam.WorldToScreenPoint(cooker + Vector3.down * .45f) : new Vector3(0, 0, -1);
                if (sp.z > 0) { Vector2 c = GuidePoint(canvasRt, sp, size); hole = new Rect(c - GuideHoleSize * .5f, GuideHoleSize); hasHole = true; }
                GuideRect(_guideCard, (size.x - 880) * .5f, 150, 880, string.IsNullOrEmpty(text.body) ? 96 : 154);
                _guideFoot.text = $"按 {Ui.Emphasize("WASD / 方向键")} 移动，走近后按 {Ui.Emphasize("F")} 打开。";
            }
            DrawGuideSpotlight(size, hasHole ? (Rect?)hole : null);
            _guideTitle.text = text.title; _guideBody.text = text.body;
        }

        private bool UpdateOpeningAgencyGuide()
        {
            bool show = S.OpeningAgencyGuidePending && !MainMenuOpen && !AnyModalOpen && !QuestAnnouncementBlocksGuide;
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
            if (step == 2) return ("先吃一碗", $"点 {Ui.Emphasize("「现在吃一碗」")}。");
            if (step == 3) return ("带上剩饭", $"点 {Ui.Emphasize("「全部装进背包」")}。饭点或饿了会自动吃。");
            if (_openingMealCookStage == 0) return ("选米量", "大米会从背包和家中后仓取用，开焖时扣除。");
            if (_openingMealCookStage == 1) return ("选做法", $"选 {Ui.Emphasize("白饭")} 就可以，灰色选项表示食材不足。");
            return ("开始焖饭", $"开焖后约 {Ui.Emphasize($"{PCShopSell.Core.Home.Pantry.CookMinutes} 分钟")}煮好。");
        }

        private Camera TutorialCam => _tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _tutorialCanvas.worldCamera;

        private (string title, string body) OpeningMealGuideText(int step)
        {
            switch (step)
            {
                case 0:
                    return ("打开电饭锅", "");
                case 2:
                    return ("饭好了，先吃一碗", $"打开电饭锅，点 {Ui.Emphasize("「现在吃一碗」")}。");
                default:
                    return ("带上剩饭", $"打开电饭锅，点 {Ui.Emphasize("「全部装进背包」")}。");
            }
        }

    }
}
