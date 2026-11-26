using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>街坊小店的米灰窗口、深色常驻栏与共用材质。</summary>
    public static partial class Ui
    {
        public static Color FlatWindow => PanelBg;
        public static Color FlatCard => PanelBg2;
        public static Color FlatButton => ButtonBg;
        public static Color FlatHeader => PanelBg2;
        public static Color FlatInk => TextColor;
        public static Color FlatUp => Good;
        public static Color FlatDown => Warn;
        public static readonly Color FlatCardEdge = Tone("C2B8AB");
        public static readonly Color FlatButtonEdge = Tone("B2A696");
        public static readonly Color FlatHotEdge = Tone("8D4834");
        public static readonly Color FlatWell = Tone("F7F3EC");
        public static readonly Color FlatWellEdge = Tone("ACA194");
        public static readonly Color FlatBand = Tone("E8E2D8");
        public static readonly Color FlatRow = Tone("E5EBDD");
        public static readonly Color SelectionEdge = Tone("A8624B");
        internal static readonly Color PaperStock = Tone("FCF8EE");
        internal static readonly Color NoteStock = Tone("F9F3E2");
        internal static readonly Color Clipboard = Tone("A79A86");
        internal static readonly Color WoodBoard = Tone("B7AA97");
        internal static readonly Color WoodBack = Tone("DDD3C4");
        internal static readonly Color ShelfChannel = Tone("858479");
        public static readonly Color Chrome = Tone("3A3733");
        public static readonly Color ChromeWell = Tone("2B2825");
        public static readonly Color ChromeButton = Tone("4B443D");
        public static readonly Color ChromeEdge = Tone("7A7268");
        public static readonly Color ChromeInk = Tone("FFF8EA");
        public static readonly Color ChromeDim = Tone("D6CCBF");
        public static readonly Color ChromeGold = Tone("E0BB73");
        public static readonly Color ChromeGood = Tone("B7D3A8");
        public static readonly Color ChromeWarn = Tone("F3AA93");
        internal static readonly Color HudTile = Tone("EDE4D5");
        public static readonly Color SuccessSurface = Tone("E3ECDE");
        public static readonly Color PendingSurface = Tone("EEE3CA");
        public static readonly Color InfoInk = Tone("4B6474");
        public static readonly Color InfoSurface = Tone("E1E8EB");
        public static readonly Color WorldShade = Tone("26221E7A");

        public static Color SelectedButton(bool selected) => selected ? FlatRow : ButtonBg;
        internal static Color TabFill(bool selected) => selected ? Chrome : ButtonBg;

        public static Button Tab(Transform parent, string title, System.Action select, bool selected, float width = 124)
            => Size(Button(parent, title, select, 17, TabFill(selected)), width, 34, 0);

        public static void SetButtonFill(Button button, Color fill)
        {
            if (button is FlatStyledButton styled)
                styled.ApplyFill(fill);
        }

        internal static Color SurfaceBehind(Transform parent)
        {
            for (var node = parent; node != null; node = node.parent)
            {
                if (node.TryGetComponent<Image>(out var image) && image.color.a >= .58f)
                    return image.color;
            }
            return PanelBg;
        }

        private static bool Dark(Color color) => color.r * .2126f + color.g * .7152f + color.b * .0722f < .43f;

        internal static Color ReadableInk(Color ink, Color surface)
        {
            if (surface == ButtonHot && (ink == TextColor || ink == Dim)) return Color.white;
            if (surface == ButtonSecondary && (ink == TextColor || ink == Dim)) return Tone("201C18");
            if (!Dark(surface)) return ink;
            if (ink == TextColor) return ChromeInk;
            if (ink == Dim) return ChromeDim;
            if (ink == Accent) return ChromeGold;
            if (ink == Good) return ChromeGood;
            if (ink == Warn) return ChromeWarn;
            return ink;
        }

        private static Color ControlFill(Color color, bool dark)
        {
            if (color == Accent) return ButtonHot;
            if (color == Warn) return dark ? Warn : WarningSurface;
            if (!dark) return color;
            if (color == ButtonBg || color == PanelBg2) return ChromeButton;
            if (color == FlatWell) return ChromeWell;
            if (color == FlatRow) return Tone("50634F");
            return color;
        }

        private static bool PlateColor(Color color) => color == PanelBg || color == PanelBg2 || color == FlatBand
            || color == Chrome || color == ChromeWell || color == ChromeButton || color == WarningSurface
            || color == PendingSurface || color == SuccessSurface || color == InfoSurface;

        private static void SkinSurface(Image image, string name, Color color)
        {
            if (name == "TitleBar" || name.StartsWith("Window_", System.StringComparison.Ordinal)) return;
            if (name == "Choice" || name == "Input")
                image.color = ControlFill(FlatWell, Dark(SurfaceBehind(image.transform.parent)));
            if (name == "Button" || name == "Choice" || name == "Input" || name == "Panel"
                || name == "Portrait" || name == "Toast" || name == "Bubble" || PlateColor(color))
            {
                Round(image);
                if (name == "TopBar" || name == "BottomBar") FlatBorder(image, ChromeEdge);
            }
        }

        internal static void Round(Image image)
        {
            image.sprite = RoundedPlate;
            image.type = Image.Type.Sliced;
        }

        private static Color SurfaceEdge(Color color, string name)
        {
            if (color.a <= .01f) return Color.clear;
            if (color == ButtonHot) return FlatHotEdge;
            if (color == FlatRow) return SelectionEdge;
            if (color == ButtonSecondary) return Good;
            if (color == WarningSurface || color == Warn) return Tone("C98569");
            if (Dark(color)) return ChromeEdge;
            if (name == "Choice" || name == "Input" || color == FlatWell) return FlatWellEdge;
            if (PlateColor(color) || name == "Panel") return FlatCardEdge;
            return FlatButtonEdge;
        }

        /// <summary>给任意图形加方角描边。</summary>
        public static void FlatBorder(Graphic graphic, Color edge, float width = 1)
        {
            var line = graphic.GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>();
            line.effectColor = edge;
            line.effectDistance = new Vector2(width, -width);
            line.useGraphicAlpha = graphic is Image;
        }

        /// <summary>平面卡片：底色 + 细描边。</summary>
        public static Image FlatPanel(Transform parent, string name, Color? fill = null, Color? edge = null)
        {
            var image = Panel(parent, name, fill ?? FlatCard);
            Round(image);
            FlatBorder(image, edge ?? FlatCardEdge);
            return image;
        }

        private static Sprite _paperSurface, _receiptSurface, _woodSurface;

        internal static Image PaperPanel(Transform parent, string name, Color? fill = null, bool lifted = false, bool torn = false)
        {
            var image = Panel(parent, name, fill ?? PaperStock);
            ApplyPaper(image, lifted, torn);
            return image;
        }

        internal static void ApplyPaper(Image image, bool lifted = false, bool torn = false)
        {
            if (torn)
            {
                if (_receiptSurface == null) _receiptSurface = CreateSurfaceTexture(false, true);
                image.sprite = _receiptSurface;
            }
            else
            {
                if (_paperSurface == null) _paperSurface = CreateSurfaceTexture(false);
                image.sprite = _paperSurface;
            }
            image.type = Image.Type.Simple;
            FlatBorder(image, FlatWellEdge, .65f);
            if (!lifted) return;
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Tone("37302636");
            shadow.effectDistance = new Vector2(1, -3);
        }

        internal static Image WoodPanel(Transform parent, string name, Color fill)
        {
            var image = Panel(parent, name, fill);
            if (_woodSurface == null) _woodSurface = CreateSurfaceTexture(true);
            image.sprite = _woodSurface;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        internal static void Trim(RectTransform parent, string name, Color color, float inset, bool top = false)
        {
            float edge = top ? 1 : 0;
            var trim = Panel(parent, name, color);
            trim.raycastTarget = false;
            trim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            trim.rectTransform.Anchor(0, edge, 1, edge, 0,
                top ? -inset - 1 : inset, 0, top ? inset : -inset - 1);
        }

        private static Sprite CreateSurfaceTexture(bool wood, bool torn = false)
        {
            int width = wood ? 256 : 192;
            int height = wood ? 96 : 192;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = wood ? "Neighborhood pale wood" : torn ? "Neighborhood receipt" : "Neighborhood paper",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width * height];
            var random = new System.Random(wood ? 582 : 147);
            for (int y = 0; y < height; y++)
            {
                int grain = wood ? random.Next(-8, 6) : 0;
                for (int x = 0; x < width; x++)
                {
                    float line = wood ? Mathf.Sin(y * 1.31f + Mathf.Sin(x * .026f + y * .19f) * .8f) * 4 : 0;
                    byte tone = (byte)Mathf.Clamp(248 + grain + Mathf.RoundToInt(line) + random.Next(-3, 4), 0, 255);
                    byte alpha = torn && y < 2 * Mathf.Abs(x % 8 - 4) ? (byte)0 : (byte)255;
                    pixels[y * width + x] = new Color32(tone, tone, tone, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        internal static void UseCaptionInk(Button button, Color ink)
        {
            if (button is FlatStyledButton styled) styled.SetInk(ink);
        }

        internal static void UseCaptionIcon(Button button, Image icon)
        {
            if (button is FlatStyledButton styled) styled.SetIcon(icon);
        }

        private sealed class FlatStyledButton : Button
        {
            private Text _caption;
            private Image _icon;
            private Outline _edge;
            private bool _captionDimmed;
            private bool _darkParent;
            private Color? _fixedInk;
            private Color _enabledInk;
            private Color _disabledInk;
            private Color? _requestedFill;

            public override void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left) PlayClick();
                base.OnPointerClick(eventData);
            }

            public override void OnSubmit(BaseEventData eventData)
            {
                PlayClick();
                base.OnSubmit(eventData);
            }

            private void PlayClick()
            {
                if (IsActive() && IsInteractable() && !TryGetComponent<HoldRepeat>(out _)) SoundFx.Play(Sfx.Click);
            }

            public void SetCaption(Text caption, Color fill)
            {
                _caption = caption;
                _darkParent = Dark(SurfaceBehind(transform.parent));
                ApplyFill(fill);
            }

            public void ApplyFill(Color fill)
            {
                Color face = ControlFill(fill, _darkParent);
                if (_requestedFill == fill && image.color == face) return;
                _requestedFill = fill;
                image.color = face;
                if (fill == ButtonHot || fill == ButtonSecondary || fill == Accent || fill == FlatRow || fill == Warn || fill == Chrome)
                {
                    var edge = _darkParent && fill == FlatRow ? ChromeGood : SurfaceEdge(face, "Button");
                    FlatBorder(image, edge);
                    _edge = GetComponent<Outline>();
                    _edge.enabled = true;
                }
                else if (_edge != null) _edge.enabled = false;
                Color under = face.a < .58f && _darkParent ? Chrome : face;
                Color ink = fill == Warn ? Warn : fill == FlatRow ? Good : TextColor;
                _enabledInk = _fixedInk ?? ReadableInk(ink, under);
                _disabledInk = ReadableInk(Dim, under);
                UpdateInk();
                _caption.fontStyle = fill == FlatRow || fill == ButtonHot || fill == Accent || fill == Chrome
                    ? FontStyle.Bold : FontStyle.Normal;
            }

            public void SetInk(Color ink)
            {
                _fixedInk = ink;
                _enabledInk = ink;
                UpdateInk();
            }

            public void SetIcon(Image icon)
            {
                _icon = icon;
                UpdateInk();
            }

            private void UpdateInk()
            {
                _caption.color = _captionDimmed ? _disabledInk : _enabledInk;
                if (_icon != null) _icon.color = new Color(1, 1, 1, _captionDimmed ? .48f : 1f);
            }

            protected override void DoStateTransition(SelectionState state, bool instant)
            {
                base.DoStateTransition(state, instant);
                if (_caption == null) return;
                bool dimmed = state == SelectionState.Disabled;
                if (dimmed == _captionDimmed) return;
                if (dimmed) _enabledInk = _caption.color;
                _captionDimmed = dimmed;
                UpdateInk();
            }
        }
    }
}
