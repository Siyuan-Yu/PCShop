using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Home
{
    /// <summary>农贸市场卖的原始食材，可长期保存。</summary>
    public sealed class IngredientDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Unit { get; }
        public Money Price { get; }

        /// <summary>焖进饭里，每碗多顶多少饱。</summary>
        public double HungerBonus { get; }

        /// <summary>每碗多恢复多少健康。</summary>
        public double HealthBonus { get; }

        public double WeightKg => Backpack.IngredientKg(Id);
        public Money PricePerKg => Price.Scale(1 / WeightKg);
        public string DishName { get; }

        public IngredientDef(string id, string name, string unit, long priceYuan, double hungerBonus, double healthBonus, string dish)
        {
            Id = id;
            Name = name;
            Unit = unit;
            Price = Money.FromYuan(priceYuan);
            HungerBonus = hungerBonus;
            HealthBonus = healthBonus;
            DishName = dish;
        }
    }

    public static class Ingredients
    {
        public static readonly IReadOnlyList<IngredientDef> All = new[]
        {
            new IngredientDef("veg", "青菜", "千克", 3, 6, 0.4, "青菜焖饭"),
            new IngredientDef("egg", "鸡蛋", "千克", 2, 9, 0.3, "鸡蛋焖饭"),
            new IngredientDef("tofu", "豆腐", "千克", 3, 8, 0.3, "豆腐焖饭"),
            new IngredientDef("pork", "五花肉", "千克", 12, 14, 0.2, "肉末焖饭"),
        };

        public static IngredientDef Get(string id) => All.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>一批食材或成品。只有成品有过期日。</summary>
    public sealed class FoodBatch
    {
        public const int NoExpiry = -1;

        public string Id;
        public string Name;
        public double Hunger;
        public double Health;
        public int Count;
        public int ExpireDay;
        public bool Chilled;
        public int ChilledDays;

        public static double WeightOf(string id) => FastFoodCatalog.Packed(id)?.WeightKg ?? Backpack.LunchboxKg;

        public static long ExpiryMinute(int day) => ((long)day + 1) * 1440;
        public long MinutesLeft(long now) => ExpireDay == NoExpiry ? long.MaxValue : Math.Max(0, ExpiryMinute(ExpireDay) - now);

        public FoodBatch Clone() => new FoodBatch { Id = Id, Name = Name, Hunger = Hunger, Health = Health, Count = Count, ExpireDay = ExpireDay, Chilled = Chilled, ChilledDays = ChilledDays };
    }

    /// <summary>电饭锅里正在焖的一锅：到点才好，好了才能取。</summary>
    public sealed class CookJob
    {
        public FoodBatch Batch;
        public long ReadyMinute;
    }

    /// <summary>家里的粮食：米、食材、电饭锅里焖着 / 焖好的饭、打包带走的饭盒。</summary>
    public sealed class Pantry
    {
        public const double StartRiceKg = 20;
        public const int BowlsPerKg = 8;
        public const double MinCookRiceKg = 0.25;
        public const double BaseBowlHunger = 22;
        public const int CookMinutes = 40;
        public const int EatMinutes = 10;
        public static readonly Money RicePricePerKg = Money.FromYuan(6);

        public double RiceKg = StartRiceKg;
        public readonly List<FoodBatch> Ingredient = new List<FoodBatch>();
        public readonly List<FoodBatch> Cooked = new List<FoodBatch>();
        public readonly List<FoodBatch> Packed = new List<FoodBatch>();

        public int IngredientCount(string id) => Ingredient.Where(b => b.Id == id).Sum(b => b.Count);

        /// <summary>电饭锅里正在焖的饭（没有就是 null）。</summary>
        public CookJob Cooking;

        /// <summary>锅里正在焖，或者焖好的饭还没取完：这时不能再焖新的一锅。</summary>
        public bool PotBusy => Cooking != null || CookedBowls > 0;

        public int CookedBowls => Cooked.Sum(b => b.Count);

        public int PackedBowls => Packed.Sum(b => b.Count);

        public void AddIngredient(string id, int count)
        {
            IngredientDef d = Ingredients.Get(id);
            if (d == null || count <= 0) return;
            Ingredient.Add(new FoodBatch { Id = id, Name = d.Name, Count = count, ExpireDay = FoodBatch.NoExpiry });
        }

        public static int IngredientUnitsForRice(double kg) => (int)Math.Ceiling(kg * 2 - 1e-9);

        public bool StartCook(double kg, string ingredientId, long nowMinute, out string error) =>
            StartCook(kg, ingredientId, nowMinute, new[] { this }, out error);

        /// <summary>检查整锅用量后，按给定来源的顺序扣除原料并开始焖饭。</summary>
        internal bool StartCook(double kg, string ingredientId, long nowMinute, IEnumerable<Pantry> supplies, out string error)
        {
            error = null;
            if (Cooking != null) { error = "锅里正在焖饭。"; return false; }
            if (CookedBowls > 0) { error = "锅里还有焖好的饭，先吃掉或者装进饭盒。"; return false; }
            if (double.IsNaN(kg) || double.IsInfinity(kg) || kg < MinCookRiceKg - 1e-9) { error = "至少放 0.25 千克米。"; return false; }
            if (supplies.Sum(source => source.RiceKg) + 1e-9 < kg) { error = "可用大米不够。"; return false; }
            IngredientDef ing = ingredientId == null ? null : Ingredients.Get(ingredientId);
            if (ingredientId != null && ing == null) { error = "电饭锅不能用这种配菜。"; return false; }
            int need = ing == null ? 0 : IngredientUnitsForRice(kg);
            if (ing != null && supplies.Sum(source => source.IngredientCount(ingredientId)) < need)
            { error = $"{ing.Name}不够（要 {need * ing.WeightKg:0.##} 千克）。"; return false; }

            double riceLeft = kg;
            foreach (Pantry source in supplies)
            {
                double take = Math.Min(riceLeft, source.RiceKg);
                source.RiceKg -= take;
                riceLeft -= take;
                if (need > 0) need = Remove(source.Ingredient, ingredientId, need);
                if (riceLeft <= 0 && need == 0) break;
            }
            int bowls = Math.Max(1, (int)Math.Round(kg * BowlsPerKg));
            Cooking = new CookJob
            {
                ReadyMinute = nowMinute + CookMinutes,
                Batch = new FoodBatch
                {
                    Id = ing?.Id ?? "rice",
                    Name = ing?.DishName ?? "白米饭",
                    Hunger = BaseBowlHunger + (ing?.HungerBonus ?? 0),
                    Health = ing?.HealthBonus ?? 0,
                    Count = bowls,
                },
            };
            return true;
        }

        /// <summary>还要焖多少分钟（没在焖返回 0）。</summary>
        public long CookMinutesLeft(long nowMinute) => Cooking == null ? 0 : Math.Max(0, Cooking.ReadyMinute - nowMinute);

        /// <summary>到点了就把这一锅放进“做好的饭”，返回这一批；还没好返回 null。做好的饭放不过第二天。</summary>
        public FoodBatch FinishCook(long nowMinute)
        {
            if (Cooking == null || nowMinute < Cooking.ReadyMinute) return null;
            FoodBatch b = Cooking.Batch;
            b.ExpireDay = (int)(Cooking.ReadyMinute / (24 * 60)) + 1;
            Cooked.Add(b);
            Cooking = null;
            return b;
        }

        private static int Remove(List<FoodBatch> list, string id, int count)
        {
            foreach (FoodBatch b in list.Where(x => x.Id == id).ToList())
            {
                int take = Math.Min(count, b.Count);
                b.Count -= take;
                count -= take;
                if (b.Count == 0) list.Remove(b);
                if (count == 0) break;
            }
            return count;
        }

        /// <summary>从某个列表里吃一碗（先吃快过期的）。返回这一碗的批次，没有则为 null。</summary>
        public FoodBatch TakeBowl(List<FoodBatch> from, string id = null)
        {
            FoodBatch b = from.Where(x => x.Count > 0 && (id == null || x.Id == id)).OrderBy(x => FastFoodCatalog.Packed(x.Id)?.Drink == true ? 1 : 0).ThenBy(x => x.ExpireDay).FirstOrDefault();
            if (b == null) return null;
            b.Count--;
            var eaten = b.Clone();
            eaten.Count = 1;
            if (b.Count == 0) from.Remove(b);
            return eaten;
        }

        /// <summary>把做好的饭装进饭盒带走。</summary>
        public int Pack(int bowls)
        {
            int moved = 0;
            while (moved < bowls)
            {
                FoodBatch b = TakeBowl(Cooked);
                if (b == null) break;
                Packed.Add(b);
                moved++;
            }
            return moved;
        }

        /// <summary>清理超过保质期的成品，返回清理明细。</summary>
        public List<string> Spoil(int today)
        {
            var lost = new List<string>();
            foreach (List<FoodBatch> list in new[] { Cooked, Packed })
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    FoodBatch b = list[i];
                    if (b.ExpireDay >= today) continue;
                    lost.Add($"{b.Name}×{b.Count}");
                    list.RemoveAt(i);
                }
            }
            return lost;
        }
    }
}
