using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class PriceDay
    {
        public int Day;
        public double[] Prices;
        public PriceDay Copy() => new PriceDay { Day = Day, Prices = (double[])Prices.Clone() };
    }

    public sealed partial class GameSession
    {
        public static readonly string[] PriceGroups = PartCatalog.Categories.Select(PartCatalog.CategoryName)
            .Concat(new[] { "米与食材", "烟酒饮料与礼品", "家具与货架", "快餐", "载具", "智能手机", "服装" }).ToArray();
        private readonly List<PriceDay> _priceDays = new List<PriceDay>();
        private int _nextPriceReportDay;
        public IReadOnlyList<PriceDay> PriceDays => _priceDays;
        public bool WeeklyPricesSubscribed => !Flags.Contains("prices:unsubscribed");
        public bool WeeklyPricesPending { get; private set; }
        public void ToggleWeeklyPrices()
        {
            if (!Flags.Remove("prices:unsubscribed")) Flags.Add("prices:unsubscribed");
            WeeklyPricesPending = false;
        }
        public void DismissWeeklyPrices() => WeeklyPricesPending = false;

        private void RecordPriceDay()
        {
            int day = Clock.Today.TotalDays;
            if (_priceDays.Any(p => p.Day == day)) return;
            var prices = PartCatalog.Categories.Select(c => PartCatalog.All.Where(p => p.Category == c)
                .Average(p => (double)Market.PriceOf(p).Cents)).ToList();
            prices.Add(Ingredients.All.Select(i => (double)i.Price.Cents).Concat(new[] { (double)Pantry.RicePricePerKg.Cents }).Average());
            prices.Add(Gifts.All.Average(g => (double)g.Price.Cents));
            prices.Add(FurnitureCatalog.All.Where(f => !f.Starter).Average(f => (double)f.Price.Cents));
            prices.Add(FastFoodCatalog.All.Average(f => (double)f.Price.Cents));
            prices.Add(Vehicles.All.Where(v => v.Purchasable).Average(v => (double)v.Price.Cents));
            prices.Add(PhonePrice.Cents);
            prices.Add(Outfits.All.Where(o => o.Price.IsPositive).Average(o => (double)o.Price.Cents));
            _priceDays.Add(new PriceDay { Day = day, Prices = prices.ToArray() });
            if (_nextPriceReportDay <= 0) _nextPriceReportDay = day + 7;
            if (day < _nextPriceReportDay) return;
            _nextPriceReportDay = day + 7;
            if (WeeklyPricesSubscribed)
            {
                WeeklyPricesPending = true;
                Notice?.Invoke("本周物价简报已出，可在“行情 → 每周物价简报”查看。");
            }
        }

        private void RestorePriceReports(SaveData data)
        {
            _priceDays.Clear();
            foreach (var p in (data.priceDays ?? Array.Empty<PriceDay>()).Where(p => p != null).OrderBy(p => p.Day))
                if (p.Day >= 0 && p.Day <= Clock.Today.TotalDays && p.Prices != null && p.Prices.Length == PriceGroups.Length
                    && p.Prices.All(v => v > 0 && !double.IsNaN(v) && !double.IsInfinity(v)) && !_priceDays.Any(d => d.Day == p.Day))
                    _priceDays.Add(p.Copy());
            _nextPriceReportDay = data.nextPriceReportDay > 0 ? data.nextPriceReportDay : Clock.Today.TotalDays + 7;
            WeeklyPricesPending = data.weeklyPricesPending && WeeklyPricesSubscribed;
            RecordPriceDay();
        }
    }
}
