using System;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Home
{
    // Calendar-derived rules are reproducible without rolling new outcomes on load.
    public static class IndustryCycles
    {
        public static int Generation(string id) => id != null && id.StartsWith("g2:") ? 2 : id != null && id.StartsWith("g1:") ? 1 : 0;
        public static int ReleaseDay(int generation) => generation == 0 ? 0 : generation == 1 ? 30 : 90;
        public static int CurrentGeneration(int day) => day >= 90 ? 2 : day >= 30 ? 1 : 0;
        public static bool Available(PartDef part, int day) => day >= ReleaseDay(Generation(part.Id));
        public static double HardwareFactor(PartDef part, int day)
        {
            int age = CurrentGeneration(day) - Generation(part.Id);
            return age <= 0 ? 1 : age == 1 ? .78 : .55;
        }
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
        public static double GoodsFactor(GiftDef product,int day) => product == null ? 1
            : ModelProducts.IsModel(product.Id) ? ModelFactor(product.ModelSeries,day)
            : ClothingProducts.IsClothing(product.Id) ? ClothingFactor(day) : 1;
    }
}
