using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static double PartUnitKg(PartDef part, bool boxed = true) => Stock.SpaceOf(part, boxed, 1) * Backpack.KgPerSpace;
        private static string ProductLabel(string name, double kg, string detail = null, string unit = "每件") =>
            name + "\n<size=13><color=#A8B0BA>" + unit + " " + kg.ToString("0.###") + " 千克</color></size>"
            + (string.IsNullOrEmpty(detail) ? "" : "\n<size=13><color=#A8B0BA>" + detail + "</color></size>");
    }
}
