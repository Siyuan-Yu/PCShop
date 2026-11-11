using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Home
{
    /// <summary>
    /// 家具按「哪一种店会用到它」归类。家具城与进货员界面都用这份归类做筛选，
    /// 以后新增店型或专用设施只要改这里，界面自动跟上。
    /// </summary>
    public static class FurnitureShops
    {
        /// <summary>玩家真能开的每一种店。花店、生鲜店、食品店尚未开放，故不计入。</summary>
        public static readonly string[] AllKinds = RetailShopTypes.All;

        /// <summary>只给某几种店用的专用设施；返回空数组表示这是通用设施。</summary>
        public static string[] Dedicated(FurnitureDef item)
        {
            if (item == null) return Array.Empty<string>();
            if (item.Id == "restaurant_stove" || item.Id == "restaurant_table") return new[] { "restaurant" };
            if (item.Id == "used_buydesk" || item.Id == "used_display") return new[] { "secondhand" };
            if (PetShopCatalog.HouseCapacity(item.Id) > 0 || PetShopCatalog.IsToyFixture(item.Id))
                return new[] { PetShopCatalog.ShopKind };
            if (item.Id == FurnitureCatalog.ShopBench || item.Id == FurnitureCatalog.ShopDisplay)
                return new[] { "computer" };
            return Array.Empty<string>();
        }

        /// <summary>
        /// 这件家具的「适用店铺」取值。货架、收银台、冰箱、装饰这类没有专属归属的设施，
        /// 归到每一种店名下，这样按任意店型筛选都不会把它们漏掉。
        /// </summary>
        public static string[] For(FurnitureDef item)
        {
            var dedicated = Dedicated(item);
            return (dedicated.Length > 0 ? dedicated : AllKinds)
                .Select(RetailShopTypes.Name).ToArray();
        }

        /// <summary>货架、收银台、冰箱、装饰这类每家店都用得上的设施。</summary>
        public static bool IsUniversal(FurnitureDef item) => Dedicated(item).Length == 0;

        /// <summary>家具城第一层的店型标签（不含「全部」，由界面自己加）。</summary>
        public static IReadOnlyList<string> Tabs() =>
            AllKinds.Select(RetailShopTypes.Name).ToArray();
    }
}
