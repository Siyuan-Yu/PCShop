using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Home
{
    /// <summary>送人的礼品（烟、酒、水果……）：在烟酒茶行 / 农贸市场买，或者网购；背包里有才能送。</summary>
    public sealed class GiftDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Money Price { get; }
        public double WeightKg { get; }

        /// <summary>在哪一类店里有得卖（地点的 Business）。</summary>
        public string SoldAt { get; }

        public GiftDef(string id, string name, string description, long priceYuan, double weightKg, string soldAt)
        {
            Id = id;
            Name = name;
            Description = description;
            Price = Money.FromYuan(priceYuan);
            WeightKg = weightKg;
            SoldAt = soldAt;
        }
    }

    public static class Gifts
    {
        public const string Cig = "cig";
        public const string CigHi = "cig_hi";
        public const string Fruit = "fruit";
        public const string Wine = "wine";
        public const string Drink = "drink";
        public const string Snack = "snack";
        public const string Baijiu = "baijiu";

        public const string TobaccoBusiness = "烟酒";
        public const string FarmBusiness = "农贸";

        public static readonly IReadOnlyList<GiftDef> All = new[]
        {
            new GiftDef(Cig, "一条好烟", "递给熟人的硬通货。也能拿来向档口老板打听行情（一次一条）。", 100, 0.2, TobaccoBusiness),
            new GiftDef(CigHi, "一条名烟", "更体面的烟，老档口老板就认这个。也能拿来打听行情。", 150, 0.2, TobaccoBusiness),
            new GiftDef(Wine, "两瓶好酒", "两瓶像样的酒，有点沉。", 200, 1.6, TobaccoBusiness),
            new GiftDef(Baijiu, "两瓶白酒", "实在人喝的酒，有点沉。", 160, 1.6, TobaccoBusiness),
            new GiftDef(Drink, "一箱饮料", "一整箱，很沉。", 100, 6.0, TobaccoBusiness),
            new GiftDef(Snack, "一盒点心", "精致的点心盒。", 120, 0.8, TobaccoBusiness),
            new GiftDef(Fruit, "一箱水果", "农贸市场的时令水果，一箱也不轻。", 120, 3.0, FarmBusiness),
        };

        public static GiftDef Get(string id) => All.FirstOrDefault(g => g.Id == id);

        public static bool IsCigarette(string id) => id == Cig || id == CigHi;

        public static IEnumerable<GiftDef> SoldAtBusiness(string business) => All.Where(g => g.SoldAt == business);
    }

    /// <summary>
    /// 出门背着的背包：有承重（和载具的载货一起算，1 格 = 2 公斤）。
    /// 装着刚买的米和菜（回家自动放进厨房）、送人的礼品；进货的配件在载具的 <c>Cargo</c> 里，两边合计受承重限制。
    /// </summary>
    public sealed class Backpack
    {
        /// <summary>每格货折合多少公斤（配件和背包里的东西共用一个承重）。</summary>
        public const double KgPerSpace = 2.0;

        public const double RiceKgPerJin = 0.5;

        /// <summary>刚买、还没带回家的米和食材。</summary>
        public readonly Pantry Food = new Pantry { RiceJin = 0 };

        private readonly Dictionary<string, int> _gifts = new Dictionary<string, int>();

        public IEnumerable<KeyValuePair<string, int>> GiftLines => _gifts.Where(kv => kv.Value > 0);

        public int GiftCount(string id) => _gifts.TryGetValue(id, out int n) ? n : 0;

        public int CigaretteCount => GiftCount(Gifts.Cig) + GiftCount(Gifts.CigHi);

        public void AddGift(string id, int count)
        {
            if (count <= 0 || Gifts.Get(id) == null) return;
            _gifts[id] = GiftCount(id) + count;
        }

        public bool TakeGift(string id)
        {
            int have = GiftCount(id);
            if (have <= 0) return false;
            if (have == 1) _gifts.Remove(id);
            else _gifts[id] = have - 1;
            return true;
        }

        /// <summary>拿走一条烟（先用便宜的）。</summary>
        public bool TakeCigarette() => TakeGift(Gifts.Cig) || TakeGift(Gifts.CigHi);

        public static double IngredientKg(string id)
        {
            switch (id)
            {
                case "veg": return 0.5;
                case "egg": return 0.3;
                case "tofu": return 0.4;
                case "pork": return 0.5;
                default: return 0.4;
            }
        }

        public double WeightKg =>
            Food.RiceJin * RiceKgPerJin
            + Food.Ingredient.Sum(b => b.Count * IngredientKg(b.Id))
            + _gifts.Sum(kv => kv.Value * (Gifts.Get(kv.Key)?.WeightKg ?? 0));

        /// <summary>折合成“格”（和载具的载货容量同一单位）。</summary>
        public double Space => WeightKg / KgPerSpace;

        public bool IsEmpty => Food.RiceJin <= 0 && Food.Ingredient.Count == 0 && !GiftLines.Any();

        public void ClearGifts() => _gifts.Clear();
    }
}
