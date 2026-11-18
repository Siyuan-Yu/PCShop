using System;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Home
{
    public readonly struct CommodityPriceEvent
    {
        private const int ImpactDays = 5;
        private const int HoldDays = 3;
        private const int RecoveryDays = 19;
        private const int Duration = ImpactDays + HoldDays + RecoveryDays;

        public readonly string Key, Name, Headline;
        public readonly int StartDay;
        private readonly double _dailyMove;

        internal CommodityPriceEvent(string key, string name, string headline, int startDay, double dailyMove)
        {
            Key = key;
            Name = name;
            Headline = headline;
            StartDay = startDay;
            _dailyMove = dailyMove;
        }

        public bool IsActive(int day) => Key != null && day >= StartDay && day < StartDay + Duration;
        public bool IsVolatile(int day) => Key != null && day >= StartDay && day < StartDay + ImpactDays;

        internal double Factor(int day)
        {
            int age = day - StartDay;
            double exposure = age < ImpactDays ? age + 1
                : ImpactDays * (1 - Math.Max(0, age - ImpactDays - HoldDays + 1) / (double)RecoveryDays);
            return Math.Pow(1 + _dailyMove, exposure);
        }
    }

    // Calendar-derived rules are reproducible without rolling new outcomes on load.
    public static class IndustryCycles
    {
        private const int EventInterval = 90;
        private const int EarliestEventDay = 72;
        private static readonly (string key, string name, string shortage, string surplus)[] EventMarkets =
        {
            (nameof(PartCategory.Gpu), "显卡", "矿场集中补货，渠道库存趋紧", "多家矿场集中出清设备，显卡涌入市场"),
            (nameof(PartCategory.Memory), "内存", "内存主要产线停工检修，现货供应收紧", "服务器厂商取消一批采购，内存转入现货市场"),
            (nameof(PartCategory.Storage), "硬盘", "大型数据中心同时扩容，硬盘交付吃紧", "硬盘厂商集中清理积压库存"),
            (Gifts.FarmBusiness, "米与食材", "连日暴雨阻断部分农产运输，农贸到货减少", "周边几个产区集中到货，农贸市场腾库促销"),
            (ClothingProducts.Business, "服装", "面料供应紧张，成衣批发商陆续提价", "多家成衣厂集中清理积压库存"),
            ("harbor", "晨湾航行模型", "新作引发抢购，工坊的现货快卖完了", "工坊提前开放补货，囤积的现货集中流入市场"),
            ("rainbot", "雨巷守望模型", "联展造成人气暴涨，渠道开始抢货", "巡展订单缩减，渠道收到大批退回的现货"),
            ("garden", "苔园来客模型", "新角色意外走红，模型订单激增", "工坊加印提前完成，多批现货同时到仓"),
            ("starlight", "星灯工坊模型", "展览登上热搜，模型补货跟不上订单", "多家经销商撤单，工坊低价放出一批库存")
        };

        public static CommodityPriceEvent CommodityEventOn(int day, ulong seed)
        {
            if (day < EarliestEventDay) return default;
            int cycle = day / EventInterval;
            var current = EventInCycle(cycle, seed);
            if (current.IsActive(day)) return current;
            if (cycle == 0) return default;
            var previous = EventInCycle(cycle - 1, seed);
            return previous.IsActive(day) ? previous : default;
        }

        private static CommodityPriceEvent EventInCycle(int cycle, ulong seed)
        {
            ulong roll = unchecked(seed + ((ulong)cycle + 1) * 0x9E3779B97F4A7C15UL);
            roll = unchecked((roll ^ (roll >> 30)) * 0xBF58476D1CE4E5B9UL);
            roll = unchecked((roll ^ (roll >> 27)) * 0x94D049BB133111EBUL);
            roll ^= roll >> 31;
            var market = EventMarkets[(int)(roll % (ulong)EventMarkets.Length)];
            int day = cycle * EventInterval + EarliestEventDay
                + (int)((roll >> 8) % (EventInterval - EarliestEventDay));
            bool shortage = (roll & (1UL << 20)) != 0;
            double move = .075 + ((roll >> 24) % 21) * .001;
            return new CommodityPriceEvent(market.key, market.name, shortage ? market.shortage : market.surplus,
                day, shortage ? move : -move);
        }

        internal static double CommodityEventFactor(string key, int day, ulong seed)
        {
            var news = CommodityEventOn(day, seed);
            return news.Key != null && news.Key == key ? news.Factor(day) : 1;
        }

        public static int ModelPhase(string series, int day)
        {
            int index = Array.IndexOf(ModelProducts.SeriesIds, series);
            return ((day + Math.Max(0,index) * 7) % 40 + 40) % 40;
        }
        public static double ModelFactor(string series, int day, ulong seed = 0)
        {
            int phase = ModelPhase(series,day);
            double baseline = phase < 8 ? 1 : phase < 14 ? Trend(phase, 8, 14, 1, 1.25)
                : phase < 19 ? Trend(phase, 14, 19, 1.25, 1.9)
                : phase < 22 ? 1.9 : phase < 28 ? Trend(phase, 22, 28, 1.9, 1.35)
                : phase < 34 ? Trend(phase, 28, 34, 1.35, .65)
                : Trend(phase, 34, 40, .65, 1);
            return baseline * CommodityEventFactor(series, day, seed);
        }

        private static double Trend(int day, int fromDay, int toDay, double from, double to)
        {
            double t = (day - fromDay) / (double)(toDay - fromDay);
            return from + (to - from) * t * t * (3 - 2 * t);
        }
        public static string ModelNews(string series, int day)
        {
            int phase = ModelPhase(series,day);
            string title = ModelProducts.SeriesName(series);
            return title + (phase < 8 ? "：新作预告，播出后可能带动热度" : phase < 14 ? "：新作开播，讨论升温" : phase < 22 ? "：角色走红，相关模型需求旺盛" : phase < 28 ? "：作品完结，热度开始回落" : phase < 34 ? "：话题退潮，库存折价出清" : "：库存逐渐消化，档口开始恢复常规报价");
        }
        public static double WholesaleMarketFactor(string business, int day, ulong seed = 0) =>
            OrdinaryFactor(business, day) * CommodityEventFactor(business, day, seed);

        internal static double OrdinaryFactor(string market, int day)
        {
            if (string.IsNullOrEmpty(market) || day <= 0) return 1;
            int phase = 0;
            foreach (char c in market) phase = (phase * 31 + c) % 113;
            double variation = .073 * Math.Sin(day * .22 + phase)
                + .024 * Math.Sin(day * .45 + phase * .21);
            return 1 + Math.Min(1, day / 7.0) * variation;
        }

        public static double GoodsFactor(GiftDef product, int day, ulong seed = 0)
        {
            if (product == null) return 1;
            if (ModelProducts.IsModel(product.Id)) return ModelFactor(product.ModelSeries, day, seed);
            return WholesaleMarketFactor(product.SoldAt, day, seed);
        }
    }
}
