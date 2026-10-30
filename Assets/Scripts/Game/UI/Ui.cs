using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>用代码搭 uGUI 的小工具。灰盒阶段字体用系统微软雅黑。</summary>
    public static class Ui
    {
        public static readonly Color PanelBg = Tone("202E2D");
        public static readonly Color PanelBg2 = Tone("2B3D3A");
        public static readonly Color Accent = Tone("DDC18B");
        public static readonly Color ButtonBg = Tone("374945");
        public static readonly Color ButtonHot = Tone("547469");
        public static readonly Color Warn = Tone("A65E49");
        public static readonly Color Good = Tone("A9C9A9");
        public static readonly Color Dim = Tone("ABB5A8");
        public static readonly Color TextColor = Tone("F1E9D7");
        private static Color Tone(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        private static Sprite _card;
        private static Sprite Card
        {
            get
            {
                if (_card != null) return _card;
                const int n = 64; const float radius = 10;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Enamel card", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (32 - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (32 - radius), 0);
                    pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
                }
                texture.SetPixels(pixels); texture.Apply();
                return _card = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            }
        }

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
            if (name == "Button" || name == "Panel" || name == "ShopDock" || name == "Toast" || name == "Bubble" || name == "QuestTracker" || name == "Portrait")
            {
                img.sprite = Card; img.type = Image.Type.Sliced;
                if (name != "Bubble")
                {
                    var line = rt.gameObject.AddComponent<Outline>();
                    line.effectColor = new Color(0.8f, 0.76f, 0.6f, name == "Button" ? 0.10f : 0.22f);
                    line.effectDistance = new Vector2(0.65f, -0.65f);
                }
            }
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
            cb.highlightedColor = new Color(1.15f, 1.13f, 1.08f);
            cb.fadeDuration = 0.12f;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = cb;
            b.interactable = interactable;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            Text t = Label(img.transform, text, size, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(6);
            if (!interactable) t.color = Dim;
            return b;
        }

        /// <summary>给任意界面元素挂鼠标悬停说明（悬停约半秒后在鼠标旁边弹出）。文字每次悬停时现取，所以能显示当前状态。</summary>
        public static T Tip<T>(this T c, Func<string> text) where T : Component
        {
            if (c == null) return c;
            Graphic g = c.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
            var t = c.GetComponent<TipTarget>() ?? c.gameObject.AddComponent<TipTarget>();
            t.Text = text;
            return c;
        }

        public static T Tip<T>(this T c, string text) where T : Component => c.Tip(() => text);

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
        /// <summary>记下 root 里每个滚动列表滚到哪了（重建前调用）。</summary>
        public static List<Vector2> SaveScroll(Transform root)
        {
            var l = new List<Vector2>();
            foreach (ScrollRect sr in root.GetComponentsInChildren<ScrollRect>())
                l.Add(sr.content != null ? sr.content.anchoredPosition : Vector2.zero);
            return l;
        }

        /// <summary>重建后把滚动列表按顺序滚回原来的位置，买东西时列表不会跳回顶上。</summary>
        public static void RestoreScroll(RectTransform layoutRoot, Transform root, List<Vector2> saved)
        {
            if (saved == null || saved.Count == 0) return;
            ScrollRect[] now = root.GetComponentsInChildren<ScrollRect>();
            if (now.Length == 0) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
            for (int i = 0; i < now.Length && i < saved.Count; i++)
            {
                if (now[i].content == null) continue;
                now[i].StopMovement();
                now[i].content.anchoredPosition = saved[i];
            }
        }

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
