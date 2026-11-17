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

    [Serializable]
    public sealed class MarketPriceMove
    {
        public string Name;
        public double Change;
    }

    public sealed partial class SaveData
    {
        public int marketPriceAlertDay;
        public MarketPriceMove[] marketPriceAlerts;
    }

    public sealed partial class GameSession
    {
        public static readonly string[] PriceGroups = PartCatalog.Categories.Select(PartCatalog.CategoryName)
            .Concat(new[] { "米与食材", "烟酒饮料与礼品", "家具与货架", "快餐", "载具", "智能手机", "服装", "住宅售价", "住宅周租", "商铺售价", "商铺周租" }).ToArray();
        public static int PropertyPriceGroupStart => PriceGroups.Length - 4;
        private readonly List<PriceDay> _priceDays = new List<PriceDay>();
        public const double MarketPriceAlertThreshold = .10;
        public int MarketPriceAlertDay { get; private set; }
        public IReadOnlyList<MarketPriceMove> MarketPriceAlerts { get; private set; } = Array.Empty<MarketPriceMove>();
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

        private void RecordPriceDay(bool announceMoves = false)
        {
            int day = Clock.Today.TotalDays;
            if (_priceDays.Any(p => p.Day == day)) return;
            var prices = PartCatalog.Categories.Select(c => PartCatalog.All.Where(p => p.Category == c)
                .Average(p => (double)Market.PriceOf(p).Cents)).ToList();
            prices.Add(Ingredients.All.Where(i => !FishingCatalog.RequiresCatch(i.Id)).Select(i => (double)i.Price.Cents)
                .Append(Pantry.RicePricePerKg.Cents).Average() * IndustryCycles.WholesaleMarketFactor(FarmBusiness, day));
            prices.Add(Gifts.All.Average(g => (double)g.Price.Cents) * IndustryCycles.WholesaleMarketFactor(Gifts.TobaccoBusiness, day));
            prices.Add(FurnitureCatalog.All.Where(f => !f.Starter).Average(f => (double)f.Price.Cents));
            prices.Add(FastFoodCatalog.All.Average(f => (double)f.Price.Cents) * IndustryCycles.WholesaleMarketFactor(FastFoodBusiness, day));
            prices.Add(Vehicles.All.Where(v => v.Purchasable).Average(v => (double)v.Price.Cents));
            prices.Add(PhonePrice.Cents);
            prices.Add(ClothingProducts.All.Average(o => o.Price.Cents * IndustryCycles.GoodsFactor(o, day)));
            prices.AddRange(_propertyPriceFactors.Select(v => v * 10000));
            var yesterday = _priceDays.LastOrDefault();
            _priceDays.Add(new PriceDay { Day = day, Prices = prices.ToArray() });
            if (announceMoves && yesterday?.Day == day - 1)
                RecordMarketPriceMoves(day, yesterday.Prices, prices);
            int elapsed = day - EconomicReportAnchorDay;
            if (EconomicReportAutomatic && _priceDays.Count > 1 && elapsed > 0 && elapsed % EconomicReportPeriodDays == 0)
            {
                EconomicReportPending = true;
                Notice?.Invoke("新一期经济简报已出，可在“物价 → 经济简报”查看收支结算与物价涨跌。");
            }
        }

        private void RecordMarketPriceMoves(int day, double[] previous, List<double> current)
        {
            var moves = new List<MarketPriceMove>();
            void Compare(string name, double before, double after)
            {
                if (before <= 0) return;
                double change = after / before - 1;
                if (Math.Abs(change) > MarketPriceAlertThreshold + 1e-9)
                    moves.Add(new MarketPriceMove { Name = name, Change = change });
            }
            for (int i = 0; i < PriceGroups.Length; i++)
                Compare(PriceGroups[i], previous[i], current[i]);

            // Products in a model series share their wholesale multiplier.
            foreach (string series in ModelProducts.SeriesIds)
                Compare(ModelProducts.SeriesName(series) + "（模型批发）",
                    IndustryCycles.ModelFactor(series, day - 1), IndustryCycles.ModelFactor(series, day));

            if (moves.Count == 0) return;
            MarketPriceAlertDay = day;
            MarketPriceAlerts = moves.OrderByDescending(move => Math.Abs(move.Change)).ToArray();
            Notice?.Invoke(new GameDate(day) + "物价异动：" + string.Join("、", moves.Select(move =>
                $"{move.Name} {move.Change:+0.#%;-0.#%}")));
        }

        public void DismissMarketPriceAlert() => MarketPriceAlerts = Array.Empty<MarketPriceMove>();

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
            MarketPriceAlertDay = data.marketPriceAlertDay;
            MarketPriceAlerts = data.marketPriceAlerts ?? Array.Empty<MarketPriceMove>();
            RecordPriceDay();
        }
    }
}
