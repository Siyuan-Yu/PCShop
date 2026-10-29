using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Catalog
{
    public enum PartCategory
    {
        Cpu,
        Motherboard,
        Memory,
        Gpu,
        Storage,
        PowerCase,
    }

    /// <summary>配件定义。品牌与型号全部虚构。</summary>
    public sealed class PartDef
    {
        public string Id { get; }
        public string Name { get; }
        public PartCategory Category { get; }

        /// <summary>0 入门 / 1 主流 / 2 高端。</summary>
        public int Tier { get; }

        public Money BasePrice { get; }

        /// <summary>CPU 与主板的接口；其他配件为空。</summary>
        public string Socket { get; }

        /// <summary>CPU、显卡为功耗；电源机箱套装为额定功率。</summary>
        public int Watts { get; }

        /// <summary>占用空间倍率：显卡、电源机箱套装为 2。</summary>
        public double SizeFactor { get; }

        public PartDef(string id, string name, PartCategory category, int tier, long basePriceYuan,
            string socket = null, int watts = 0, double sizeFactor = 1.0)
        {
            Id = id;
            Name = name;
            Category = category;
            Tier = tier;
            BasePrice = Money.FromYuan(basePriceYuan);
            Socket = socket;
            Watts = watts;
            SizeFactor = sizeFactor;
        }
    }

    public static class PartCatalog
    {
        public const int TierCount = 3;

        private static readonly List<PartDef> _all = new List<PartDef>
        {
            new PartDef("cpu0", "锐芯 R3-3100", PartCategory.Cpu, 0, 599, "AX4", 65),
            new PartDef("cpu1", "锐芯 R5-5600", PartCategory.Cpu, 1, 1099, "AX4", 95),
            new PartDef("cpu2", "英擎 i7-K70", PartCategory.Cpu, 2, 2299, "LG7", 125),

            new PartDef("mb0", "鹰嘴 A320M", PartCategory.Motherboard, 0, 399, "AX4"),
            new PartDef("mb1", "鹰嘴 B550 战神", PartCategory.Motherboard, 1, 799, "AX4"),
            new PartDef("mb2", "禾田 Z790 旗舰", PartCategory.Motherboard, 2, 1599, "LG7"),

            new PartDef("ram0", "白鲸 DDR4 8G", PartCategory.Memory, 0, 149),
            new PartDef("ram1", "白鲸 DDR4 16G", PartCategory.Memory, 1, 279),
            new PartDef("ram2", "雷霆 DDR5 32G", PartCategory.Memory, 2, 599),

            new PartDef("gpu0", "星耀 GT 550", PartCategory.Gpu, 0, 899, null, 75, 2.0),
            new PartDef("gpu1", "星耀 RX 660", PartCategory.Gpu, 1, 1999, null, 170, 2.0),
            new PartDef("gpu2", "星耀 RTX 880", PartCategory.Gpu, 2, 4999, null, 320, 2.0),

            new PartDef("ssd0", "闪存侠 256G", PartCategory.Storage, 0, 169),
            new PartDef("ssd1", "闪存侠 512G", PartCategory.Storage, 1, 299),
            new PartDef("ssd2", "闪存侠 1TB", PartCategory.Storage, 2, 549),

            new PartDef("psu0", "铁甲 400W 电源机箱套装", PartCategory.PowerCase, 0, 299, null, 400, 2.0),
            new PartDef("psu1", "铁甲 550W 电源机箱套装", PartCategory.PowerCase, 1, 459, null, 550, 2.0),
            new PartDef("psu2", "铁甲 750W 电源机箱套装", PartCategory.PowerCase, 2, 799, null, 750, 2.0),
        };

        private static readonly Dictionary<string, PartDef> _byId = BuildIndex();

        public static IReadOnlyList<PartDef> All => _all;

        public static readonly PartCategory[] Categories =
        {
            PartCategory.Cpu, PartCategory.Motherboard, PartCategory.Memory,
            PartCategory.Gpu, PartCategory.Storage, PartCategory.PowerCase,
        };

        public static PartDef Get(string id)
        {
            if (id == null || !_byId.TryGetValue(id, out PartDef p))
                throw new KeyNotFoundException($"配件不存在：{id}");
            return p;
        }

        public static bool Exists(string id) => id != null && _byId.ContainsKey(id);

        public static PartDef Find(PartCategory category, int tier)
        {
            foreach (PartDef p in _all)
            {
                if (p.Category == category && p.Tier == tier) return p;
            }
            throw new KeyNotFoundException($"没有 {category} 档次 {tier} 的配件。");
        }

        public static IEnumerable<PartDef> InCategory(PartCategory category)
        {
            foreach (PartDef p in _all)
            {
                if (p.Category == category) yield return p;
            }
        }

        /// <summary>装一台指定档次的整机需要的配件（每个品类一件）。</summary>
        public static List<PartDef> BuildList(int tier)
        {
            var list = new List<PartDef>();
            foreach (PartCategory c in Categories) list.Add(Find(c, tier));
            return list;
        }

        public static string CategoryName(PartCategory c)
        {
            switch (c)
            {
                case PartCategory.Cpu: return "CPU";
                case PartCategory.Motherboard: return "主板";
                case PartCategory.Memory: return "内存";
                case PartCategory.Gpu: return "显卡";
                case PartCategory.Storage: return "硬盘";
                case PartCategory.PowerCase: return "电源机箱";
                default: return c.ToString();
            }
        }

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 0: return "入门";
                case 1: return "主流";
                default: return "高端";
            }
        }

        /// <summary>
        /// 检查一组配件能否装成整机：每个品类各一件，CPU 与主板接口一致，电源功率够用。
        /// 返回不兼容原因；兼容时返回 null。
        /// </summary>
        public static string CheckCompatibility(IReadOnlyList<PartDef> parts)
        {
            PartDef cpu = null, mb = null, gpu = null, psu = null;
            var seen = new HashSet<PartCategory>();
            foreach (PartDef p in parts)
            {
                if (!seen.Add(p.Category)) return $"重复的{CategoryName(p.Category)}";
                if (p.Category == PartCategory.Cpu) cpu = p;
                if (p.Category == PartCategory.Motherboard) mb = p;
                if (p.Category == PartCategory.Gpu) gpu = p;
                if (p.Category == PartCategory.PowerCase) psu = p;
            }
            foreach (PartCategory c in Categories)
            {
                if (!seen.Contains(c)) return $"缺少{CategoryName(c)}";
            }
            if (cpu.Socket != mb.Socket) return $"CPU 接口 {cpu.Socket} 与主板 {mb.Socket} 不匹配";
            int need = cpu.Watts + gpu.Watts + 100;
            if (psu.Watts < need) return $"电源功率不足（需要 {need}W，只有 {psu.Watts}W）";
            return null;
        }

        private static Dictionary<string, PartDef> BuildIndex()
        {
            var d = new Dictionary<string, PartDef>();
            foreach (PartDef p in _all) d.Add(p.Id, p);
            return d;
        }
    }
}
