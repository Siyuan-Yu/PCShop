using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Inventory
{
    public sealed class ResaleOffer
    {
        public int Id,Product;
        public string Name;
        public double Kg;
        public long Price,ReferencePrice;
    }
    /// <summary>货架能摆的一种货：6 类配件，或一组杂货（烟、酒、饮料、点心、水果）。</summary>
    public sealed class ShelfKind
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsParts { get; }
        public PartCategory Category { get; }
        public IReadOnlyList<string> GiftIds { get; }

        public ShelfKind(string id, string name, PartCategory category)
        {
            Id = id;
            Name = name;
            IsParts = true;
            Category = category;
            GiftIds = Array.Empty<string>();
        }

        public ShelfKind(string id, string name, params string[] giftIds)
        {
            Id = id;
            Name = name;
            IsParts = false;
            GiftIds = giftIds;
        }

        public bool IsGoods => !IsParts;
        public string BrowseGroup => IsParts?"电脑配件":Id.StartsWith("clothing_")?"服装":Id.StartsWith("model_")?"模型手办":Id.StartsWith("pet_")?"宠物用品":"零售商品";
    }

    public static class ShelfKinds
    {
        /// <summary>每个货架能放多少千克（只算重量，不算体积；散件是盒装的一半重）。</summary>
        public const double CapacityKg = 60;

        public static readonly IReadOnlyList<ShelfKind> All = new[]
        {
            new ShelfKind("gpu", "显卡", PartCategory.Gpu),
            new ShelfKind("cpu", "处理器", PartCategory.Cpu),
            new ShelfKind("memory", "内存", PartCategory.Memory),
            new ShelfKind("storage", "硬盘", PartCategory.Storage),
            new ShelfKind("powercase", "电源机箱", PartCategory.PowerCase),
            new ShelfKind("motherboard", "主板", PartCategory.Motherboard),
            new ShelfKind("smoke", "烟", Gifts.Cig, Gifts.CigHi),
            new ShelfKind("liquor", "酒", Gifts.Wine, Gifts.Baijiu),
            new ShelfKind("drink", "饮料", Gifts.Drink, Gifts.Soda),
            new ShelfKind("snack", "点心", Gifts.Snack),
            new ShelfKind("fruit", "水果", Gifts.Fruit),
            new ShelfKind("secondhand", "二手转售品", Array.Empty<string>()),
            new ShelfKind("flowers", "鲜花与盆栽", RetailProducts.All.Where(g => g.Id.StartsWith("flower_")).Select(g => g.Id).ToArray()),
            new ShelfKind("pet_supplies", "基础宠物用品", PetShopCatalog.Products.Where(g => PetShopCatalog.Tier(g.Id)==0).Select(g => g.Id).ToArray()),
            new ShelfKind("pet_supplies_mid", "中档宠物用品", PetShopCatalog.Products.Where(g => PetShopCatalog.Tier(g.Id)==1).Select(g => g.Id).ToArray()),
            new ShelfKind("pet_supplies_high", "高档宠物用品", PetShopCatalog.Products.Where(g => PetShopCatalog.Tier(g.Id)==2).Select(g => g.Id).ToArray()),
            new ShelfKind("produce", "米与生鲜", RetailProducts.All.Where(g => g.Id.StartsWith("produce:")).Select(g => g.Id).ToArray()),
            new ShelfKind("meals", "外带食品", RetailProducts.All.Where(g => g.MealId != null).Select(g => g.Id).ToArray()),
            new ShelfKind("clothing_0", "平价服装", ClothingProducts.All.Where(g => ClothingProducts.Outfit(g.Id).Tier == 0).Select(g => g.Id).ToArray()),
            new ShelfKind("clothing_1", "品质服装", ClothingProducts.All.Where(g => ClothingProducts.Outfit(g.Id).Tier == 1).Select(g => g.Id).ToArray()),
            new ShelfKind("clothing_2", "精品服装", ClothingProducts.All.Where(g => ClothingProducts.Outfit(g.Id).Tier == 2).Select(g => g.Id).ToArray()),
            new ShelfKind("clothing_3", "奢侈服装", ClothingProducts.All.Where(g => ClothingProducts.Outfit(g.Id).Tier == 3).Select(g => g.Id).ToArray()),
            new ShelfKind("model_harbor", "晨湾航行模型", ModelProducts.All.Where(g => g.ModelSeries == "harbor").Select(g => g.Id).ToArray()),
            new ShelfKind("model_rainbot", "雨巷守望手办", ModelProducts.All.Where(g => g.ModelSeries == "rainbot").Select(g => g.Id).ToArray()),
            new ShelfKind("model_garden", "苔园来客模型", ModelProducts.All.Where(g => g.ModelSeries == "garden").Select(g => g.Id).ToArray()),
            new ShelfKind("model_starlight", "星灯工坊模型", ModelProducts.All.Where(g => g.ModelSeries == "starlight").Select(g => g.Id).ToArray()),
        };

        public static IEnumerable<ShelfKind> PartKinds => All.Where(k => k.IsParts);

        public static IEnumerable<ShelfKind> GoodsKinds => All.Where(k => k.IsGoods);

        public static ShelfKind Get(string id) => id == null ? null : All.FirstOrDefault(k => k.Id == id);

        public static ShelfKind ForCategory(PartCategory c) => All.First(k => k.IsParts && k.Category == c);

        public static ShelfKind ForGift(string giftId) => All.FirstOrDefault(k => k.IsGoods && k.GiftIds.Contains(giftId));

        public static string NameOf(string id) => Get(id)?.Name ?? "未设置";
    }

    /// <summary>某一类货在货架上摆了哪些（同时受总承重和总件数限制）。</summary>
    public sealed class ShelfFill
    {
        public ShelfKind Kind { get; }
        public double CapacityKg { get; }
        public int CapacityItems { get; }
        public double UsedKg { get; internal set; }
        public List<StockLine> Parts { get; } = new List<StockLine>();
        public List<KeyValuePair<string, int>> Goods { get; } = new List<KeyValuePair<string, int>>();
        public List<ResaleOffer> Resale { get; } = new List<ResaleOffer>();

        public ShelfFill(ShelfKind kind, double capacityKg, int capacityItems = int.MaxValue)
        {
            Kind = kind;
            CapacityKg = capacityKg;
            CapacityItems = Math.Max(0, capacityItems);
        }

        public int Total => Parts.Sum(l => l.Count) + Goods.Sum(g => g.Value)+Resale.Count;
        internal bool WeightBlocked { get; set; }
        public bool Full => CapacityItems > 0 && CapacityKg > 0 && (WeightBlocked || Total >= CapacityItems || UsedKg >= CapacityKg - 1e-9);

        public int PartCount(PartDef part, bool boxed)
        {
            foreach (StockLine l in Parts)
                if (l.Part.Id == part.Id && l.Boxed == boxed) return l.Count;
            return 0;
        }

        public int PartCount(PartDef part) => PartCount(part, true) + PartCount(part, false);

        public int PartsTotal(bool boxed) => Parts.Where(l => l.Boxed == boxed).Sum(l => l.Count);

        public int GoodsCount(string giftId)
        {
            foreach (var kv in Goods)
                if (kv.Key == giftId) return kv.Value;
            return 0;
        }
    }

    /// <summary>
    /// 后仓的货怎么摆上货架：货架只摆一种货，装满为止（同时限制重量与件数）；同一类里各个型号轮流各摆一件，
    /// 这样摆出来的货是多样的。顾客只能买货架上的东西。
    /// </summary>
    public static class ShelfPlanner
    {
        /// <summary>一件配件多重（千克）：盒装 2 千克，散件减半，显卡、电源机箱再乘 2。</summary>
        public static double PartKg(PartDef part, bool boxed) => Stock.SpaceOf(part, boxed, 1) * Backpack.KgPerSpace;

        /// <summary>一件杂货多重（千克，最少 0.2）。</summary>
        public static double GoodsKg(string giftId) => Math.Max(0.2, Gifts.Get(giftId)?.WeightKg ?? 1.0);

        public static ShelfFill Fill(Stock stock, ShelfKind kind, double capacityKg, int maxItems = int.MaxValue)
        {
            var fill = new ShelfFill(kind, capacityKg, maxItems);
            if (capacityKg>0 && maxItems>0) FillShelves(stock,kind,new[]{fill});
            return fill;
        }

        internal static void FillShelves(Stock stock, ShelfKind kind, IReadOnlyList<ShelfFill> shelves)
        {
            if (kind == null || shelves.Count == 0) return;
            var entries = new List<Entry>();
            if (kind.IsParts)
            {
                foreach (PartDef p in PartCatalog.InCategory(kind.Category).OrderBy(x => x.Tier))
                    foreach (bool boxed in new[] { true, false })
                    {
                        int n = stock.Count(p, boxed);
                        if (n > 0) entries.Add(new Entry { Part = p, Boxed = boxed, Left = n, Kg = PartKg(p, boxed) });
                    }
            }
            else
            {
                foreach (string id in kind.GiftIds)
                {
                    int n = stock.GoodsCount(id);
                    if (n > 0) entries.Add(new Entry { Gift = id, Left = n, Kg = GoodsKg(id) });
                }
            }

            foreach (var fill in shelves)
            {
                double used = 0;
                int taken = 0;
                bool progress = true;
                while (progress && taken < fill.CapacityItems)
                {
                    progress = false;
                    foreach (Entry e in entries)
                    {
                        if (taken >= fill.CapacityItems || e.Left == 0 || used + e.Kg > fill.CapacityKg + 1e-9) continue;
                        int add = double.IsPositiveInfinity(fill.CapacityKg) ? Math.Min(e.Left,fill.CapacityItems-taken) : 1;
                        e.Taken += add;
                        e.Left -= add;
                        taken += add;
                        used += e.Kg * add;
                        progress = true;
                    }
                }
                fill.UsedKg = used;
                fill.WeightBlocked = taken>0 && entries.Any(e=>e.Left>0 && used+e.Kg>fill.CapacityKg+1e-9);
                foreach (Entry e in entries)
                {
                    if (e.Taken == 0) continue;
                    if (e.Part != null) fill.Parts.Add(new StockLine(e.Part, e.Boxed, e.Taken));
                    else fill.Goods.Add(new KeyValuePair<string, int>(e.Gift, e.Taken));
                    e.Taken = 0;
                }
            }
        }

        private sealed class Entry
        {
            public PartDef Part;
            public bool Boxed;
            public string Gift;
            public int Left;
            public int Taken;
            public double Kg;
        }
    }
}
