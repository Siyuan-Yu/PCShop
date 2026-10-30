using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Inventory
{
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
    }

    public static class ShelfKinds
    {
        /// <summary>每个货架能放多少公斤（只算重量，不算体积；散件是盒装的一半重）。</summary>
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
        };

        public static IEnumerable<ShelfKind> PartKinds => All.Where(k => k.IsParts);

        public static IEnumerable<ShelfKind> GoodsKinds => All.Where(k => k.IsGoods);

        public static ShelfKind Get(string id) => id == null ? null : All.FirstOrDefault(k => k.Id == id);

        public static ShelfKind ForCategory(PartCategory c) => All.First(k => k.IsParts && k.Category == c);

        public static ShelfKind ForGift(string giftId) => All.FirstOrDefault(k => k.IsGoods && k.GiftIds.Contains(giftId));

        public static string NameOf(string id) => Get(id)?.Name ?? "未设置";
    }

    /// <summary>某一类货在货架上摆了哪些（受货架总载重限制）。</summary>
    public sealed class ShelfFill
    {
        public ShelfKind Kind { get; }
        public double CapacityKg { get; }
        public double UsedKg { get; internal set; }
        public List<StockLine> Parts { get; } = new List<StockLine>();
        public List<KeyValuePair<string, int>> Goods { get; } = new List<KeyValuePair<string, int>>();

        public ShelfFill(ShelfKind kind, double capacityKg)
        {
            Kind = kind;
            CapacityKg = capacityKg;
        }

        public int Total => Parts.Sum(l => l.Count) + Goods.Sum(g => g.Value);

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
    /// 后仓的货怎么摆上货架：货架只摆一种货，装满为止（按重量）；同一类里各个型号轮流各摆一件，
    /// 这样摆出来的货是多样的。顾客只能买货架上的东西。
    /// </summary>
    public static class ShelfPlanner
    {
        /// <summary>一件配件多重（公斤）：盒装 2 公斤，散件减半，显卡、电源机箱再乘 2。</summary>
        public static double PartKg(PartDef part, bool boxed) => Stock.SpaceOf(part, boxed, 1) * Backpack.KgPerSpace;

        /// <summary>一件杂货多重（公斤，最少 0.2）。</summary>
        public static double GoodsKg(string giftId) => Math.Max(0.2, Gifts.Get(giftId)?.WeightKg ?? 1.0);

        public static ShelfFill Fill(Stock stock, ShelfKind kind, double capacityKg)
        {
            var fill = new ShelfFill(kind, capacityKg);
            if (kind == null || capacityKg <= 0) return fill;

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

            double used = 0;
            if (double.IsPositiveInfinity(capacityKg))
            {
                foreach (Entry e in entries)
                {
                    e.Taken = e.Left;
                    used += e.Kg * e.Left;
                }
            }
            else
            {
                bool progress = true;
                while (progress)
                {
                    progress = false;
                    foreach (Entry e in entries)
                    {
                        if (e.Taken >= e.Left || used + e.Kg > capacityKg + 1e-9) continue;
                        e.Taken++;
                        used += e.Kg;
                        progress = true;
                    }
                }
            }

            fill.UsedKg = used;
            foreach (Entry e in entries)
            {
                if (e.Taken <= 0) continue;
                if (e.Part != null) fill.Parts.Add(new StockLine(e.Part, e.Boxed, e.Taken));
                else fill.Goods.Add(new KeyValuePair<string, int>(e.Gift, e.Taken));
            }
            return fill;
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
