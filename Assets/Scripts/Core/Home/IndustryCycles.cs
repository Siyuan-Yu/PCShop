using System;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Home
{
    // Calendar-derived rules are reproducible without rolling new outcomes on load.
    public static class IndustryCycles
    {
        public static int ModelPhase(string series, int day)
        {
            int index = Array.IndexOf(ModelProducts.SeriesIds, series);
            return ((day + Math.Max(0,index) * 7) % 40 + 40) % 40;
        }
        public static double ModelFactor(string series, int day)
        {
            int phase = ModelPhase(series,day);
            return phase < 8 ? 1 : phase < 14 ? 1.25 : phase < 22 ? 1.9 : phase < 28 ? 1.35 : .65;
        }
        public static string ModelNews(string series, int day)
        {
            int phase = ModelPhase(series,day);
            string title = ModelProducts.SeriesName(series);
            return title + (phase < 8 ? "：新作预告，播出后可能带动热度" : phase < 14 ? "：新作开播，讨论升温" : phase < 22 ? "：角色走红，相关模型需求旺盛" : phase < 28 ? "：作品完结，热度开始回落" : "：话题退潮，库存折价出清");
        }
        public static double ClothingFactor(int day)
        {
            int annual = ((day % 360) + 360) % 360;
            // Two long supply events per year; ordinary days stay stable. Baseline is catalog reference price.
            return annual >= 70 && annual < 90 ? 1.35 : annual >= 250 && annual < 280 ? 1.5 : 1;
        }
        public static double WholesaleMarketFactor(string business, int day)
        {
            if (string.IsNullOrEmpty(business) || day <= 0) return 1;
            int phase = 0;
            foreach (char c in business) phase = (phase * 31 + c) % 113;
            return 1 + .019 * Math.Sin(day * .91 + phase) + .011 * Math.Sin(day * .39 + phase * .21);
        }

        public static double GoodsFactor(GiftDef product, int day)
        {
            if (product == null) return 1;
            if (ModelProducts.IsModel(product.Id)) return ModelFactor(product.ModelSeries, day);
            double weekly = WholesaleMarketFactor(product.SoldAt, day);
            return ClothingProducts.IsClothing(product.Id) ? ClothingFactor(day) * weekly : weekly;
        }
    }
}
