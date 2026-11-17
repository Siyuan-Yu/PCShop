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
        private static readonly string[] OpeningFurnitureFamilies =
            { FurnitureCatalog.Shelf, FurnitureCatalog.FridgeSmall, PetShopCatalog.SmallHouse };

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
                "restaurant" => GameSession.MatchesOpeningFurniture(FurnitureCatalog.Shelf, item),
                _ => false,
            };
            return useful ? required.Length * 2 : -1;
        }

        public static string[] For(FurnitureDef item) =>
            AllKinds.Where(kind => ShopOrder(kind, item) >= 0).Select(RetailShopTypes.Name).ToArray();

        public static bool IsUniversal(FurnitureDef item) => AllKinds.All(kind => ShopOrder(kind, item) >= 0);

        // Keep each family's original position in the catalogue, with its variants ordered by price.
        public static IEnumerable<T> GroupPurchaseVariants<T>(IEnumerable<T> items, Func<T, FurnitureDef> getFurniture) =>
            items.GroupBy(item => PurchaseFamily(getFurniture(item)))
                .SelectMany(family => family.OrderBy(item => getFurniture(item)?.Price.Cents ?? 0));

        private static string PurchaseFamily(FurnitureDef item)
        {
            if (item == null) return null;
            foreach (string family in OpeningFurnitureFamilies)
                if (GameSession.MatchesOpeningFurniture(family, item)) return family;
            if (PetShopCatalog.IsToyFixture(item.Id)) return PetShopCatalog.ToyFixture(PetShopCatalog.IsDogToy(item.Id), 0);
            if (item.Role == FurnitureRole.Bed) return FurnitureCatalog.BedBoard;
            if (item.Role == FurnitureRole.Wardrobe && item.Id != FurnitureCatalog.FittingMirror) return FurnitureCatalog.WardrobeCloth;
            if (item.Role == FurnitureRole.Storage) return FurnitureCatalog.HomeBox;
            return item.Id switch
            {
                "aircon_large" => "aircon",
                "shop_sofa" => "sofa",
                "shop_plant" => "plant",
                "shop_poster" => "poster",
                _ => item.Id,
            };
        }

        /// <summary>家具城第一层的店型标签（不含「全部」，由界面自己加）。</summary>
        public static IReadOnlyList<string> Tabs() =>
            AllKinds.Select(RetailShopTypes.Name).ToArray();
    }
}
