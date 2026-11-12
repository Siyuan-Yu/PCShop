using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;

namespace PCShopSell.Core.Home
{
    /// <summary>
    /// 家具城与进货员按各店型的开店设施及专用设备筛选。
    /// </summary>
    public static class FurnitureShops
    {
        /// <summary>玩家真能开的每一种店。花店、生鲜店、食品店尚未开放，故不计入。</summary>
        public static readonly string[] AllKinds = RetailShopTypes.All;
        private static readonly IReadOnlyDictionary<string, string[]> OpeningRequirements =
            AllKinds.ToDictionary(kind => kind, GameSession.OpeningFurniture);

        /// <summary>开店基础款排在前面，之后是可替代的规格和专用设备。无关家具返回 -1。</summary>
        public static int ShopOrder(string kind, FurnitureDef item)
        {
            if (item == null || kind == null || !OpeningRequirements.TryGetValue(kind, out var required)) return -1;
            int exact = Array.IndexOf(required, item.Id);
            if (exact >= 0) return exact;
            int substitute = Array.FindIndex(required, id => GameSession.MatchesOpeningFurniture(id, item));
            if (substitute >= 0) return required.Length + substitute;

            bool useful = kind switch
            {
                PetShopCatalog.ShopKind => PetShopCatalog.IsToyFixture(item.Id),
                "computer" => item.Id == FurnitureCatalog.ShopBench || item.Id == FurnitureCatalog.ShopDisplay,
                "models" => item.Id == FurnitureCatalog.ShopDisplay,
                "clothing" => item.Id == FurnitureCatalog.ShopDisplay || item.Id == FurnitureCatalog.FittingMirror,
                _ => false,
            };
            return useful ? required.Length * 2 : -1;
        }

        public static string[] For(FurnitureDef item) =>
            AllKinds.Where(kind => ShopOrder(kind, item) >= 0).Select(RetailShopTypes.Name).ToArray();

        public static bool IsUniversal(FurnitureDef item) => AllKinds.All(kind => ShopOrder(kind, item) >= 0);

        /// <summary>家具城第一层的店型标签（不含「全部」，由界面自己加）。</summary>
        public static IReadOnlyList<string> Tabs() =>
            AllKinds.Select(RetailShopTypes.Name).ToArray();
    }
}
