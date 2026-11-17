using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>暖米色窗口、墨绿灰常驻栏，以及共享的控件颜色和形状。</summary>
    public static partial class Ui
    {
        public static Color FlatWindow => PanelBg;
        public static Color FlatCard => PanelBg2;
        public static Color FlatButton => ButtonBg;
        public static Color FlatHeader => PanelBg2;
        public static Color FlatInk => TextColor;
        public static Color FlatUp => Good;
        public static Color FlatDown => Warn;
        public static readonly Color FlatCardEdge = Tone("DED3BE");
        public static readonly Color FlatButtonEdge = Tone("D5C8AE");
        public static readonly Color FlatHotEdge = Tone("B4884A");
        public static readonly Color FlatWell = Tone("FFF9EC");
        public static readonly Color FlatWellEdge = Tone("D3C4A8");
        public static readonly Color FlatBand = Tone("EFE7D7");
        public static readonly Color FlatRow = Tone("EED5A6");
        public static readonly Color Chrome = Tone("292F2C");
        public static readonly Color ChromeWell = Tone("1D2421");
        public static readonly Color ChromeButton = Tone("3A4339");
        public static readonly Color ChromeEdge = Tone("575B4B");
        public static readonly Color ChromeInk = Tone("FFF4DE");
        public static readonly Color ChromeDim = Tone("CDC5B1");
        public static readonly Color ChromeGold = Tone("F2CE82");
        public static readonly Color ChromeGood = Tone("B7D3A8");
        public static readonly Color ChromeWarn = Tone("F3AA93");
        public static readonly Color SuccessSurface = Tone("E3ECDE");
        public static readonly Color PendingSurface = Tone("F3E4C6");
        public static readonly Color InfoInk = Tone("42667A");
        public static readonly Color InfoSurface = Tone("E3EBE9");
        public static readonly Color WorldShade = Tone("1A251C87");

        public static Color SelectedButton(bool selected) => selected ? FlatRow : ButtonBg;

        public static Button Tab(Transform parent, string title, System.Action select, bool selected, float width = 124)
            => Size(Button(parent, title, select, 17, SelectedButton(selected)), width, 34, 0);

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
            if (color == FlatRow) return Tone("514832");
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
            if (color == ButtonHot || color == FlatRow) return FlatHotEdge;
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
                if (fill == ButtonHot || fill == Accent || fill == FlatRow || fill == Warn)
                {
                    var edge = _darkParent && fill == FlatRow ? FlatHotEdge : SurfaceEdge(face, "Button");
                    FlatBorder(image, edge);
                    _edge = GetComponent<Outline>();
                    _edge.enabled = true;
                }
                else if (_edge != null) _edge.enabled = false;
                Color under = face.a < .58f && _darkParent ? Chrome : face;
                Color ink = fill == Warn ? Warn : fill == FlatRow ? Accent : TextColor;
                _enabledInk = _fixedInk ?? ReadableInk(ink, under);
                _disabledInk = ReadableInk(Dim, under);
                UpdateInk();
                _caption.fontStyle = fill == FlatRow || fill == ButtonHot || fill == Accent
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
                if (_icon != null) _icon.color = _caption.color;
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
