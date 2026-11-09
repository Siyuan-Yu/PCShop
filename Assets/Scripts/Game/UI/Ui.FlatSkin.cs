using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 全游戏统一的平面深蓝皮肤：方角、1px 描边、深海军蓝底、金色强调。
    /// 底色来自 PanelBg / PanelBg2 / ButtonBg，这里只负责描边和少量专用色。
    /// </summary>
    public static partial class Ui
    {
        public static readonly Color FlatWindow = PanelBg;
        public static readonly Color FlatCard = PanelBg2;
        public static readonly Color FlatButton = ButtonBg;
        public static readonly Color FlatCardEdge = Tone("37496A");
        public static readonly Color FlatButtonEdge = Tone("3D516B");
        public static readonly Color FlatHotEdge = Tone("C9A14F");
        public static readonly Color FlatWell = Tone("101A2C");
        public static readonly Color FlatWellEdge = Tone("4A5B75");
        public static readonly Color FlatHeader = PanelBg2;
        public static readonly Color FlatBand = Tone("18243A");
        public static readonly Color FlatRow = Tone("2A3C58");
        public static readonly Color FlatInk = Tone("E6DCC3");
        public static readonly Color FlatUp = Tone("6FCF8E");
        public static readonly Color FlatDown = Tone("E0705A");

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .004f && Mathf.Abs(a.g - b.g) < .004f && Mathf.Abs(a.b - b.b) < .004f && Mathf.Abs(a.a - b.a) < .004f;

        private static bool IsFlatSkinned(string name) =>
            name == "Button" || name == "Panel" || name == "ShopDock" || name == "Toast" || name == "Bubble" || name == "QuestTracker"
            || name == "Portrait" || name == "TopBar" || name == "BottomBar" || name == "TitleBar" || name == "Choice" || name == "Input";

        private static void SkinFlat(Image image, string name, Color color)
        {
            if (name == "Choice" || name == "Input") image.color = FlatWell;
            if (name == "TitleBar") return;
            bool window = name == "Panel" && Same(color, PanelBg);
            FlatBorder(image, FlatEdge(color, name), window ? 2 : 1);
        }

        private static Color FlatEdge(Color color, string name)
        {
            if (name == "Choice" || name == "Input") return FlatWellEdge;
            if (name != "Button" && name != "Panel") return FlatCardEdge;
            if (Same(color, ButtonHot) || Same(color, Accent)) return FlatHotEdge;
            if (Same(color, Warn)) return Tone("D9775A");
            if (Same(color, PanelBg)) return ButtonHot;
            if (Same(color, PanelBg2)) return FlatCardEdge;
            return FlatButtonEdge;
        }

        /// <summary>给任意图形加方角描边。</summary>
        public static void FlatBorder(Graphic graphic, Color edge, float width = 1)
        {
            var line = graphic.GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>();
            line.effectColor = edge;
            line.effectDistance = new Vector2(width, -width);
            line.useGraphicAlpha = false;
        }

        /// <summary>平面卡片：底色 + 细描边。</summary>
        public static Image FlatPanel(Transform parent, string name, Color? fill = null, Color? edge = null)
        {
            var image = Panel(parent, name, fill ?? FlatCard);
            FlatBorder(image, edge ?? FlatCardEdge);
            return image;
        }
    }
}
