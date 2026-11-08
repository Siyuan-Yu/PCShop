using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum EconomicReportCadence { Weekly, Biweekly, Monthly }

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
            .Concat(new[] { "米与食材", "烟酒饮料与礼品", "家具与货架", "快餐", "载具", "智能手机", "服装", "住宅售价", "住宅周租", "商铺售价", "商铺周租" }).ToArray();
        public static int PropertyPriceGroupStart => PriceGroups.Length - 4;
        private readonly List<PriceDay> _priceDays = new List<PriceDay>();
        private const string EconomicReportDisabled = "economy-report:disabled";
        public IReadOnlyList<PriceDay> PriceDays => _priceDays;
        public EconomicReportCadence ReportCadence { get; private set; }
        public bool EconomicReportAutomatic => !Flags.Contains(EconomicReportDisabled);
        public bool EconomicReportPending { get; private set; }
        public int EconomicReportPeriodDays => ReportCadence == EconomicReportCadence.Monthly ? GameDate.DaysPerMonth
            : ReportCadence == EconomicReportCadence.Biweekly ? 2 * GameDate.DaysPerWeek : GameDate.DaysPerWeek;
        public int EconomicReportAnchorDay => ReportCadence == EconomicReportCadence.Monthly ? 0
            : _priceDays.Count > 0 ? _priceDays[0].Day : Clock.Today.TotalDays;

        public void SetEconomicReportCadence(EconomicReportCadence? cadence)
        {
            if (cadence.HasValue)
            {
                ReportCadence = cadence.Value;
                Flags.Remove(EconomicReportDisabled);
            }
            else Flags.Add(EconomicReportDisabled);
            EconomicReportPending = false;
        }
        public void DismissEconomicReport() => EconomicReportPending = false;

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
            prices.AddRange(_propertyPriceFactors.Select(v => v * 10000));
            _priceDays.Add(new PriceDay { Day = day, Prices = prices.ToArray() });
            int elapsed = day - EconomicReportAnchorDay;
            if (EconomicReportAutomatic && _priceDays.Count > 1 && elapsed > 0 && elapsed % EconomicReportPeriodDays == 0)
            {
                EconomicReportPending = true;
                Notice?.Invoke("新一期经济简报已出，可在“行情 → 经济简报”查看收支结算与物价涨跌。");
            }
        }

        private void RestorePriceReports(SaveData data)
        {
            _priceDays.Clear();
            foreach (var p in (data.priceDays ?? Array.Empty<PriceDay>()).Where(p => p != null).OrderBy(p => p.Day))
            {
                if (p.Day < 0 || p.Day > Clock.Today.TotalDays || p.Prices == null
                    || p.Prices.Length != PriceGroups.Length
                    || p.Prices.Any(v => v <= 0 || double.IsNaN(v) || double.IsInfinity(v))
                    || _priceDays.Any(d => d.Day == p.Day)) continue;
                _priceDays.Add(p.Copy());
            }
            ReportCadence = Enum.IsDefined(typeof(EconomicReportCadence), data.economicReportCadence)
                ? (EconomicReportCadence)data.economicReportCadence : EconomicReportCadence.Weekly;
            EconomicReportPending = data.weeklyPricesPending && EconomicReportAutomatic;
            RecordPriceDay();
        }
    }
}
