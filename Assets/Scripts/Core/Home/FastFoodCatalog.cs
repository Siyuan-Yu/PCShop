using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
namespace PCShopSell.Core.Home
{
    public sealed class FastFoodItem
    {
        public string Id { get; }
        public string Name { get; }
        public Money Price { get; }
        public int Hunger { get; }
        public int Nutrition { get; }
        public int Mood { get; }
        public int Minutes { get; }
        public bool Drink { get; }
        public double WeightKg { get; }
        public int ShelfDays => Drink ? 7 : 1;
        private static string Signed(int n) => n > 0 ? "+" + n : n.ToString();
        internal FastFoodItem(string id, string name, long yuan, int hunger, int nutrition, int mood, int minutes, double kg, bool drink = false)
        { Id = id; Name = name; Price = Money.FromYuan(yuan); Hunger = hunger; Nutrition = nutrition; Mood = mood; Minutes = minutes; Drink = drink; WeightKg = kg; }
        public string Effects => $"饱腹 +{Hunger} · 营养 {Signed(Nutrition)} · 心情 {Signed(Mood)}";
    }

    public static class FastFoodCatalog
    {
        public static readonly IReadOnlyList<FastFoodItem> All = Array.AsReadOnly(new[]
        {
            new FastFoodItem("burger", "鸡腿汉堡", 18, 45, -1, 5, 10, .25),
            new FastFoodItem("chicken", "香酥炸鸡", 16, 35, -2, 6, 10, .3),
            new FastFoodItem("fries", "薯条", 8, 18, -2, 4, 5, .15),
            new FastFoodItem("cola", "可乐", 6, 2, -3, 8, 2, .33, true),
            new FastFoodItem("rice", "鸡肉蔬菜饭", 20, 65, 3, 3, 15, .5),
            new FastFoodItem("combo", "汉堡炸鸡套餐（含可乐）", 32, 90, -4, 10, 20, .9)
        });

        public static FastFoodItem Packed(string id) => id != null && id.StartsWith("fast:") ? All.FirstOrDefault(i => "fast:" + i.Id == id) : null;
    }
}
