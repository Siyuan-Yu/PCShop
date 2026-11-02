using System.Collections.Generic;
using System.Linq;
namespace PCShopSell.Core.Home
{
    public static class RetailProducts
    {
        public static readonly IReadOnlyList<GiftDef> All = Create();
        private static GiftDef[] Create()
        {
            var list = new List<GiftDef>
            {
                new GiftDef("flower_rose", "玫瑰花束", "鲜花批发，适合花店陈列。", 38, 0.4, "花店"),
                new GiftDef("flower_lily", "百合花束", "花店常备鲜花。", 48, 0.5, "花店"),
                new GiftDef("flower_sun", "向日葵花束", "明亮的向日葵花束。", 28, 0.4, "花店"),
                new GiftDef("flower_pot", "绿植盆栽", "适合家居和办公桌的小盆栽。", 32, 1.2, "花店"),
                new GiftDef("produce:rice", "大米（0.5千克/袋）", "零售装大米，可放进厨房使用。", 3, 0.5, "农贸"),
            };
            foreach (var ingredient in Ingredients.All)
                list.Add(new GiftDef("produce:" + ingredient.Id, ingredient.Name, "农贸批发，可零售或带回家做饭。",
                    ingredient.Price.Yuan, ingredient.WeightKg, "农贸") { IngredientId = ingredient.Id, ShelfDays = ingredient.ShelfDays });
            foreach (var meal in FastFoodCatalog.All)
                list.Add(new GiftDef("fast:" + meal.Id, meal.Name, "食品批发，外带食品可放背包、冰箱或上架零售。",
                    meal.Price.Yuan, meal.WeightKg, "快餐") { MealId = meal.Id, ShelfDays = meal.ShelfDays });
            list.AddRange(ModelProducts.All);
            list.AddRange(ClothingProducts.All);
            return list.ToArray();
        }
        public static bool IsFood(GiftDef item) => item != null && (item.IngredientId != null || item.MealId != null || item.Id == "produce:rice");
        public static bool IsDrink(string id) => id == Gifts.Soda || id == Gifts.Drink;
    }
    public static class RetailShopTypes
    {
        public static readonly string[] All = { "computer", "models", "clothing", "secondhand", "restaurant" };
        public static string Valid(string kind) => All.Contains(kind) || kind == "flowers" || kind == "produce" || kind == "food" ? kind : "computer";
        public static string Name(string kind) => kind == "restaurant" ? "餐馆" : kind == "secondhand" ? "二手旧物店" : kind == "clothing" ? "服装店" : kind == "models" ? "模型手办店" : kind == "flowers" ? "花店" : kind == "produce" ? "生鲜店" : kind == "food" ? "食品店" : "电脑店";
        public static string Description(string kind) => kind == "restaurant" ? "食材备料、投锅看火候、出锅上菜；需要加工台、灶台、传菜台、餐桌和冰箱，可安排厨师与服务员。" : kind == "secondhand" ? "听卖家说辞、出价收购，买后鉴定好货/瑕疵/仿造/损坏，再按件定价出售；高智力收购师可代管。" : kind == "clothing" ? "服装四档选品；通用货架或展示台按品类陈列，从服装批发及1788进货。季节仅影响挑货倾向，不改变采购目标。" : kind == "flowers" ? "鲜花、花束与盆栽；从花店批发进货。"
            : kind == "produce" ? "大米、蔬菜、鸡蛋、豆腐、肉类和水果；从农贸市场进货。"
            : kind == "food" ? "汉堡、炸鸡、薯条、便当、点心和饮料；从快餐店和烟酒茶行进货。"
            : kind == "models" ? "四个原创系列、12款现货模型；常规款从潮玩工坊批发，限定款需在漫展到场采购。按系列设置货架，顾客按兴趣和预算挑选。" : "电脑配件、整机及少量附带杂货。";
        // Main-category affinity only: this must not restrict listing or shelf configuration.
        public static bool Sells(string kind, string id)
        {
            if (kind == "computer") return true;
            if(kind=="restaurant"||kind=="secondhand")return false;
            if (kind == "clothing") return ClothingProducts.IsClothing(id);
            if (kind == "models") return ModelProducts.IsModel(id);
            if (kind == "flowers") return id != null && id.StartsWith("flower_");
            if (kind == "produce") return id != null && (id.StartsWith("produce:") || id == Gifts.Fruit);
            return id != null && (id.StartsWith("fast:") || id == Gifts.Soda || id == Gifts.Drink || id == Gifts.Snack);
        }
        public static string GoodsKind(string id)=>new[]{"clothing","models","flowers","produce","food"}.FirstOrDefault(k=>Sells(k,id))??"retail";
    }
}
