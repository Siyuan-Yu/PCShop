using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Inventory
{
    public readonly struct StockLine
    {
        public PartDef Part { get; }
        public bool Boxed { get; }
        public int Count { get; }

        public StockLine(PartDef part, bool boxed, int count)
        {
            Part = part;
            Boxed = boxed;
            Count = count;
        }
    }

    /// <summary>
    /// 有容量限制的库存（店铺货架、载具货箱）。容量单位为"格"：
    /// 盒装占 1 格，散件占 0.4 格，再乘配件的大小倍率（显卡、电源机箱为 2）。
    /// </summary>
    public sealed class Stock
    {
        public const double BoxedSpace = 1.0;
        public const double LooseSpace = 0.4;
        private const double Epsilon = 1e-9;

        private readonly Dictionary<string, int> _boxed = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _loose = new Dictionary<string, int>();

        public double Capacity { get; set; }

        public Stock(double capacity)
        {
            Capacity = capacity;
        }

        public static double SpaceOf(PartDef part, bool boxed, int count)
        {
            return count * (boxed ? BoxedSpace : LooseSpace) * part.SizeFactor;
        }

        public double UsedSpace
        {
            get
            {
                double used = 0;
                foreach (var kv in _boxed) used += SpaceOf(PartCatalog.Get(kv.Key), true, kv.Value);
                foreach (var kv in _loose) used += SpaceOf(PartCatalog.Get(kv.Key), false, kv.Value);
                return used;
            }
        }

        public double FreeSpace => Math.Max(0, Capacity - UsedSpace);

        public bool IsEmpty => _boxed.Count == 0 && _loose.Count == 0;

        public int Count(PartDef part, bool boxed)
        {
            Dictionary<string, int> d = boxed ? _boxed : _loose;
            return d.TryGetValue(part.Id, out int n) ? n : 0;
        }

        public int CountAll(PartDef part) => Count(part, true) + Count(part, false);

        public bool CanAdd(PartDef part, bool boxed, int count)
        {
            return count > 0 && SpaceOf(part, boxed, count) <= FreeSpace + Epsilon;
        }

        /// <summary>放得下多少件。</summary>
        public int MaxAddable(PartDef part, bool boxed)
        {
            double per = SpaceOf(part, boxed, 1);
            return (int)Math.Floor((FreeSpace + Epsilon) / per);
        }

        public bool TryAdd(PartDef part, bool boxed, int count)
        {
            if (!CanAdd(part, boxed, count)) return false;
            Dictionary<string, int> d = boxed ? _boxed : _loose;
            d[part.Id] = Count(part, boxed) + count;
            return true;
        }

        /// <summary>不检查容量地放入（读档用）。</summary>
        public void ForceAdd(PartDef part, bool boxed, int count)
        {
            if (count <= 0) return;
            Dictionary<string, int> d = boxed ? _boxed : _loose;
            d[part.Id] = Count(part, boxed) + count;
        }

        public bool TryRemove(PartDef part, bool boxed, int count)
        {
            int have = Count(part, boxed);
            if (count <= 0 || have < count) return false;
            Dictionary<string, int> d = boxed ? _boxed : _loose;
            if (have == count) d.Remove(part.Id);
            else d[part.Id] = have - count;
            return true;
        }

        /// <summary>拿走一件（优先散件），返回拿的是不是盒装；没有则返回 null。</summary>
        public bool? TakeOne(PartDef part)
        {
            if (TryRemove(part, false, 1)) return false;
            if (TryRemove(part, true, 1)) return true;
            return null;
        }

        /// <summary>拆包装：盒装变散件，不可逆。</summary>
        public bool Unbox(PartDef part, int count)
        {
            if (!TryRemove(part, true, count)) return false;
            ForceAdd(part, false, count);
            return true;
        }

        public List<StockLine> Lines()
        {
            var list = new List<StockLine>();
            foreach (PartDef p in PartCatalog.All)
            {
                int b = Count(p, true);
                int l = Count(p, false);
                if (b > 0) list.Add(new StockLine(p, true, b));
                if (l > 0) list.Add(new StockLine(p, false, l));
            }
            return list;
        }

        public void Clear()
        {
            _boxed.Clear();
            _loose.Clear();
        }
    }
}
