using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 开局做饭强制引导：整个屋子压暗，只留电饭锅一块高亮；下面一张步骤卡说清楚该怎么做。
    /// 做完（吃了饭或装了饭盒）之前，核心层会拦住出门（GameSession.LeaveBlocker）。
    /// </summary>
    public sealed partial class GameUi
    {
        private Canvas _tutorialCanvas;
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
            _tutorialCanvas = Ui.CreateCanvas("UI_Tutorial", 22);
            _guideRoot = Ui.Rect(_tutorialCanvas.transform, "OpeningMealGuide");
            _guideRoot.Anchor(0, 0, 1, 1, 0, 0, 0, 0);
            string[] dimNames = { "GuideDimTop", "GuideDimBottom", "GuideDimLeft", "GuideDimRight" };
            for (int i = 0; i < 4; i++)
            {
                _guideDim[i] = Ui.Panel(_guideRoot, dimNames[i], GuideDimColor);
                _guideDim[i].raycastTarget = false;
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
            _guideCard.sizeDelta = new Vector2(820, 190);
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
            _guideRoot.gameObject.SetActive(false);
        }

        public bool OpeningMealGuideVisible => _guideRoot != null && _guideRoot.gameObject.activeSelf;

        private static void GuideRect(RectTransform r, float x, float y, float w, float h)
        {
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        private void UpdateOpeningMealGuide()
        {
            if (_tutorialCanvas == null) return;
            int step = S.OpeningMealStep;
            bool show = step >= 0 && _ctrl.InHome && !MainMenuOpen && !AnyModalOpen && !S.IsGameOver && !_ctrl.Placing && !PopupOpen;
            if (_guideRoot.gameObject.activeSelf != show) _guideRoot.gameObject.SetActive(show);
            if (!show) return;

            var canvasRt = (RectTransform)_tutorialCanvas.transform;
            Vector2 size = canvasRt.rect.size;
            float t = Time.unscaledTime;

            bool hasHole = false;
            if (_ctrl.HomeRoom.CookerWorld is Vector3 cooker)
            {
                Vector3 sp = _ctrl.Cam.WorldToScreenPoint(cooker + Vector3.down * 0.45f);
                if (sp.z > 0)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, TutorialCam, out Vector2 local);
                    Vector2 c = local + size * 0.5f;
                    Vector2 hs = GuideHoleSize;
                    float x0 = Mathf.Clamp(c.x - hs.x * .5f, 0, size.x), x1 = Mathf.Clamp(c.x + hs.x * .5f, 0, size.x);
                    float y0 = Mathf.Clamp(c.y - hs.y * .5f, 0, size.y), y1 = Mathf.Clamp(c.y + hs.y * .5f, 0, size.y);
                    GuideRect(_guideDim[0].rectTransform, 0, y1, size.x, size.y - y1);   // 上
                    GuideRect(_guideDim[1].rectTransform, 0, 0, size.x, y0);              // 下
                    GuideRect(_guideDim[2].rectTransform, 0, y0, x0, y1 - y0);            // 左
                    GuideRect(_guideDim[3].rectTransform, x1, y0, size.x - x1, y1 - y0);  // 右
                    float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 5f));
                    var edge = new Color(Ui.FlatHotEdge.r, Ui.FlatHotEdge.g, Ui.FlatHotEdge.b, pulse);
                    GuideRect(_guideFrame[0].rectTransform, x0 - GuideFrameWidth, y1, x1 - x0 + GuideFrameWidth * 2, GuideFrameWidth);
                    GuideRect(_guideFrame[1].rectTransform, x0 - GuideFrameWidth, y0 - GuideFrameWidth, x1 - x0 + GuideFrameWidth * 2, GuideFrameWidth);
                    GuideRect(_guideFrame[2].rectTransform, x0 - GuideFrameWidth, y0, GuideFrameWidth, y1 - y0);
                    GuideRect(_guideFrame[3].rectTransform, x1, y0, GuideFrameWidth, y1 - y0);
                    foreach (var f in _guideFrame) f.color = edge;
                    _guideArrow.anchoredPosition = new Vector2((x0 + x1) * .5f, y1 + 6 + Mathf.Abs(Mathf.Sin(t * 4f)) * 14f);
                    hasHole = true;
                }
            }
            if (!hasHole)
            {
                // 屋里没有电饭锅 / 看不到：整屏轻压暗，不留洞。
                GuideRect(_guideDim[0].rectTransform, 0, 0, size.x, size.y);
                for (int i = 1; i < 4; i++) GuideRect(_guideDim[i].rectTransform, 0, 0, 0, 0);
                foreach (var f in _guideFrame) GuideRect(f.rectTransform, 0, 0, 0, 0);
            }
            _guideArrow.gameObject.SetActive(hasHole);

            (string title, string body) = OpeningMealGuideText(step);
            _guideTitle.text = title;
            _guideBody.text = body;
            _guideFoot.text = "做完这一顿之前不能出门：走到门边也会被拦下。";
            for (int i = 0; i < 3; i++)
                _guideDots[i].color = i < step ? Ui.FlatUp : i == step ? Ui.FlatHotEdge : Ui.FlatWellEdge;
        }

        private Camera TutorialCam => _tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _tutorialCanvas.worldCamera;

        private (string title, string body) OpeningMealGuideText(int step)
        {
            switch (step)
            {
                case 0:
                    return ("第 1 步 · 去电饭锅那里焖饭",
                        "走到高亮的电饭锅旁边（WASD 或方向键），按 <b>F</b> 打开它。\n选好米量，点 <b>“开始焖饭”</b>。");
                case 1:
                    long left = S.Pantry.Cooking != null ? S.Pantry.CookMinutesLeft(S.Clock.Now.TotalMinutes) : 0;
                    return ("第 2 步 · 等饭焖好",
                        $"饭正在焖，还要约 <b>{left}</b> 分钟，焖好之前锅盖打不开。\n再次打开电饭锅，点 <b>“等到饭好”</b> 就能直接把时间快进到饭好。");
                default:
                    return ("第 3 步 · 吃饭",
                        "饭好了！再次打开电饭锅，点 <b>“现在吃一碗”</b>。\n也可以先把饭装进背包当午饭——装了饭盒同样算完成。");
            }
        }

        /// <summary>电饭锅窗口顶部的引导条，并给当前该点的按钮套上金色高亮框。</summary>
        private void OpeningMealWindowGuide(Window w, int step)
        {
            (string title, string body) = OpeningMealGuideText(step);
            var banner = Ui.FlatPanel(w.Content, "OpeningMealBanner", Ui.FlatBand, Ui.FlatHotEdge);
            banner.raycastTarget = false;
            var layout = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 8, 8); layout.spacing = 2;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var head = Ui.Label(banner.transform, title, 19, TextAnchor.MiddleLeft, Ui.Accent); head.fontStyle = FontStyle.Bold; head.raycastTarget = false;
            Ui.Size(head, -1, 28);
            var text = Ui.Label(banner.transform, body, 16, TextAnchor.UpperLeft); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            Ui.Size(text, -1, 46);
            Ui.Size(banner, -1, 98, 0);
        }

        private static void GuideHighlight(Button b)
        {
            if (b == null) return;
            Ui.FlatBorder(b.targetGraphic, Ui.FlatHotEdge, 3);
        }
    }
}
