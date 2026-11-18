using System;
using System.Collections.Generic;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Home
{
    public static class PetShopCatalog
    {
        public const string ShopKind = "pet";
        public const string SupplierId = "pet_wholesale", SupplierBusiness = "宠物批发";
        public const string CatFood = "pet:catfood", DogFood = "pet:dogfood";
        public const string BasicLitter = "pet:litter";
        public const string Wand = "pet:wand", Ball = "pet:ball";
        public const string SmallHouse = "pet_house_small", MediumHouse = "pet_house_medium", LargeHouse = "pet_house_large";
        public const string WandFixture = "pet_wand_fixture", BallFixture = "pet_ball_fixture";

        private static GiftDef Product(string id, string name, string description, long price, double weight, string business) =>
            new GiftDef(id, name, description, price, weight, business, retailPriceFactor: 1.5, wholesalePriceFactor: 1.5);

        public static readonly IReadOnlyList<GiftDef> Products = new[]
        {
            Product(CatFood, "基础猫粮", "可上架，也可供代养区喂猫。", 38, 1.2, SupplierBusiness),
            Product(DogFood, "基础狗粮", "可上架，也可供代养区喂狗。", 42, 1.6, SupplierBusiness),
            Product(BasicLitter, "膨润土猫砂", "可上架，也可供代养区更换猫砂。", 32, 2.5, SupplierBusiness),
            Product("pet:treat", "宠物零食", "猫狗用小包装零食。", 16, .22, SupplierBusiness),
            Product(Wand, "逗猫棒", "可上架零售的猫玩具。", 19, .15, SupplierBusiness),
            Product(Ball, "宠物软球", "可上架零售的狗玩具。", 16, .12, SupplierBusiness),
            Product("pet:leash", "牵引绳", "出门遛狗常用的配件。", 29, .35, SupplierBusiness),
            Product("pet:mid:catfood", "优选猫粮", "喂养时比基础主粮多提升一点状态。", 88, 1.2, SupplierBusiness),
            Product("pet:mid:dogfood", "优选狗粮", "喂养时比基础主粮多提升一点状态。", 98, 1.6, SupplierBusiness),
            Product("pet:mid:litter", "豆腐猫砂", "中档猫砂，轻便易补充。", 65, 1.8, SupplierBusiness),
            Product("pet:mid:bowl", "陶瓷慢食碗", "适合日常使用的中档配件。", 79, .6, SupplierBusiness),
            Product("pet:mid:brush", "软针梳毛刷", "日常护理用品。", 56, .23, SupplierBusiness),
            Product("pet:high:catfood", "鲜肉冻干猫粮", "高档主粮，喂养时状态提升最高。", 198, 1.2, SupplierBusiness),
            Product("pet:high:dogfood", "鲜肉冻干狗粮", "高档主粮，喂养时状态提升最高。", 228, 1.6, SupplierBusiness),
            Product("pet:high:litter", "低尘混合猫砂", "高档低尘猫砂。", 119, 2.1, SupplierBusiness),
            Product("pet:high:carrier", "透气宠物外出包", "带猫狗外出的高档配件。", 299, 1.3, SupplierBusiness),
            Product("pet:high:bed", "厚垫宠物窝", "高档睡垫，可上架零售。", 249, 2.2, SupplierBusiness),
        };

        public static bool IsProduct(string id) => id != null && id.StartsWith("pet:", StringComparison.Ordinal);
        public static bool IsFood(string id) => IsProduct(id) && (id.EndsWith(":catfood",StringComparison.Ordinal) || id.EndsWith(":dogfood",StringComparison.Ordinal));
        public static bool IsDogFood(string id) => IsFood(id) && id.EndsWith(":dogfood",StringComparison.Ordinal);
        public static bool IsLitter(string id) => IsProduct(id) && id.EndsWith(":litter",StringComparison.Ordinal);
        public static int Tier(string id) => id.StartsWith("pet:high:",StringComparison.Ordinal) ? 2 : id.StartsWith("pet:mid:",StringComparison.Ordinal) ? 1 : 0;
        public static int HouseCapacity(string id) => id == SmallHouse ? 4 : id == MediumHouse ? 8 : id == LargeHouse ? 12 : 0;
        public static int MaximumCapacity(ShopSize size) => size == ShopSize.Large ? 12 : size == ShopSize.Medium ? 8 : 4;
        public const int ToyTierCount = 3;

        public static string ToyFixture(bool dog, int tier)
        {
            string id = dog ? BallFixture : WandFixture;
            return tier == 0 ? id : id + (tier == 1 ? "_mid" : "_high");
        }

        public static int ToyTier(string id) => id switch
        {
            WandFixture or BallFixture => 0,
            WandFixture + "_mid" or BallFixture + "_mid" => 1,
            WandFixture + "_high" or BallFixture + "_high" => 2,
            _ => -1,
        };

        public static bool IsToyFixture(string id) => ToyTier(id) >= 0;
        public static bool IsDogToy(string id) => id is BallFixture or BallFixture + "_mid" or BallFixture + "_high";
        public static string ToyTierName(int tier) => tier == 2 ? "高档" : tier == 1 ? "中档" : "低档";
        public static int ToyHappinessLimit(string id) => ToyTier(id) switch { 2 => 100, 1 => 90, _ => 75 };
    }
}
