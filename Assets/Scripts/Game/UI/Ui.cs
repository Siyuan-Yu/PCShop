using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>用代码搭 uGUI 的小工具。灰盒阶段字体用系统微软雅黑。</summary>
    public static class Ui
    {
        public static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.94f);
        public static readonly Color PanelBg2 = new Color(0.15f, 0.17f, 0.21f, 0.96f);
        public static readonly Color Accent = new Color(0.18f, 0.75f, 0.70f);
        public static readonly Color ButtonBg = new Color(0.24f, 0.27f, 0.33f);
        public static readonly Color ButtonHot = new Color(0.20f, 0.55f, 0.52f);
        public static readonly Color Warn = new Color(0.95f, 0.45f, 0.30f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color Dim = new Color(0.65f, 0.68f, 0.72f);
        public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.96f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "PingFang SC", "Arial" }, 20);
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Anchor(this RectTransform rt, float minX, float minY, float maxX, float maxY,
            float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0) => rt.Anchor(0, 0, 1, 1, pad, pad, pad, pad);

        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size = 20, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            RectTransform rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button Button(Transform parent, string text, Action onClick, int size = 18, Color? bg = null, bool interactable = true)
        {
            Image img = Panel(parent, "Button", bg ?? ButtonBg);
            var b = img.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = cb;
            b.interactable = interactable;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            Text t = Label(img.transform, text, size, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(4);
            if (!interactable) t.color = Dim;
            return b;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing = 6, int pad = 10, bool expandHeight = false)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = expandHeight;
            return v;
        }

        public static RectTransform Row(Transform parent, float height, float spacing = 6)
        {
            RectTransform rt = Rect(parent, "Row");
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            Size(rt, -1, height).GetComponent<LayoutElement>().flexibleHeight = 0;
            return rt;
        }

        public static T Size<T>(T c, float width = -1, float height = -1, float flexWidth = -1) where T : Component
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0)
            {
                le.preferredHeight = height;
                le.minHeight = height;
            }
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            return c;
        }

        /// <summary>带竖向滚动的列表，返回放内容的容器（自动撑高）。</summary>
        public static RectTransform Scroll(Transform parent, float height = -1)
        {
            Image bg = Panel(parent, "Scroll", new Color(0, 0, 0, 0.18f));
            if (height >= 0) Size(bg, -1, height);
            var sr = bg.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.scrollSensitivity = 30;
            sr.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = Rect(bg.transform, "Viewport").Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            VList(content, 4, 6);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = content;
            return content;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            RectTransform rt = Rect(parent, "Slider");
            Size(rt, -1, 28, 1);
            Image bg = Panel(rt, "Bg", new Color(0.2f, 0.22f, 0.26f));
            bg.rectTransform.Anchor(0, 0.35f, 1, 0.65f);
            RectTransform fillArea = Rect(rt, "FillArea").Anchor(0, 0.35f, 1, 0.65f, 8, 0, 8, 0);
            Image fill = Panel(fillArea, "Fill", Accent);
            fill.rectTransform.Fill();
            RectTransform handleArea = Rect(rt, "HandleArea").Anchor(0, 0, 1, 1, 8, 0, 8, 0);
            Image handle = Panel(handleArea, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(18, 0);
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        /// <summary>横向进度条（0~1）。</summary>
        public static Image Bar(Transform parent, float value, Color color, float width = 120, float height = 12)
        {
            Image bg = Panel(parent, "Bar", new Color(0.2f, 0.2f, 0.24f));
            Size(bg, width, height);
            Image fill = Panel(bg.transform, "Fill", color);
            fill.rectTransform.Anchor(0, 0, Mathf.Clamp01(value), 1);
            return fill;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Kill.It(c);
            }
        }

        public static string Hm(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

        public static string WeekdayName(PCShopSell.Core.Time.GameWeekday d)
        {
            string[] n = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            return n[(int)d];
        }
    }
}
