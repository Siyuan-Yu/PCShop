using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Catalog;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static double PartUnitKg(PartDef part, bool boxed = true) => Stock.SpaceOf(part, boxed, 1) * Backpack.KgPerSpace;
        // Let the horizontal layout report the height of the wrapped product text.
        // Fixed row heights combined with Text overflow used to paint over the next product.
        private static Text ProductText(Transform parent, string text, int size = 18,
            TextAnchor align = TextAnchor.UpperLeft, Color? color = null)
        {
            var label = Ui.Label(parent, text.Replace("<size=13>", "<size=14>"), size, align, color);
            label.lineSpacing = 1.15f;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var row = parent.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                var element = parent.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.preferredHeight = -1;
                    element.minHeight = Mathf.Max(88, element.minHeight);
                    element.flexibleHeight = 0;
                }
                row.padding.top = Mathf.Max(10, row.padding.top);
                row.padding.bottom = Mathf.Max(10, row.padding.bottom);
                row.spacing = Mathf.Max(10, row.spacing);
            }
            return label;
        }

        private static string ProductLabel(string name, double kg, string detail = null, string unit = "每件") =>
            name + "\n<size=13><color=#A8B0BA>" + unit + " " + kg.ToString("0.###") + " 千克</color></size>"
            + (string.IsNullOrEmpty(detail) ? "" : "\n<size=13><color=#A8B0BA>" + detail + "</color></size>");
    }
}
