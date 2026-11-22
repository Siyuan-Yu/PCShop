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
        public int SchemaVersion;
        public double[] Prices;
        public PriceDay Copy() => new PriceDay { Day = Day, SchemaVersion = SchemaVersion, Prices = (double[])Prices.Clone() };
    }

    [Serializable]
    public sealed class MarketPriceMove
    {
        public string CategoryId;
        public string Name;
        public double Change;
    }

    public sealed partial class SaveData
    {
        public string commodityPriceSeed;
        public int marketPriceAlertDay;
        public MarketPriceMove[] marketPriceAlerts;
    }

    public sealed partial class GameSession
    {
        private static readonly (string Id, string Name)[] StandardPriceCategories = PartCatalog.Categories
            .Select(category => (category.ToString(), PartCatalog.CategoryName(category)))
            .Concat(new[]
            {
                (FarmBusiness, "米与食材"), (Gifts.TobaccoBusiness, "烟酒饮料与礼品"), ("furniture", "家具与货架"),
                (FastFoodBusiness, "快餐"), ("vehicles", "载具"), ("smartphone", "智能手机"), (ClothingProducts.Business, "服装"),
                ("home-price", "住宅售价"), ("home-rent", "住宅周租"), ("shop-price", "商铺售价"), ("shop-rent", "商铺周租")
            }).ToArray();
        public static readonly string[] PriceGroups = StandardPriceCategories.Select(category => category.Name).ToArray();
        public static readonly IReadOnlyList<(string Id, string Name)> MarketPriceCategories = StandardPriceCategories
            .Concat(ModelProducts.SeriesIds.Select(series => (series, ModelPriceReportName(series)))).ToArray();
        public static int PropertyPriceGroupStart => PriceGroups.Length - 4;
        public static string ModelPriceReportName(string series) => ModelProducts.SeriesName(series) + "（模型行情）";
        private readonly List<PriceDay> _priceDays = new List<PriceDay>();
        public const double MarketPriceAlertThreshold = .05;
        public int MarketPriceAlertDay { get; private set; }
        public IReadOnlyList<MarketPriceMove> MarketPriceAlerts { get; private set; } = Array.Empty<MarketPriceMove>();
        public bool ModelPriceAlertsAvailable => _shops.Any(shop => shop.Rented && shop.RetailKind == "models" && !shop.Sim.NotFitted);
        private const string PriceSubscribed = "price-alert:subscribed:";
        private const string PriceMuted = "price-alert:muted:";
        private const string EconomicReportDisabled = "economy-report:disabled";
        public IReadOnlyList<PriceDay> PriceDays => _priceDays;
        // A live quote is separate from the daily historical snapshot.
        public PriceDay CurrentMarketPriceSnapshot => new PriceDay
        { Day = Clock.Today.TotalDays, SchemaVersion = 1, Prices = CurrentMarketPrices(Clock.Today.TotalDays).ToArray() };
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

        public bool IsMarketPriceSubscribed(string id) => ModelProducts.SeriesIds.Contains(id)
            ? !Flags.Contains(PriceMuted + id)
            : Flags.Contains(PriceSubscribed + id);

        public void SetMarketPriceSubscription(string id, bool enabled)
        {
            if (!MarketPriceCategories.Any(category => category.Id == id)) return;
            bool enabledByDefault = ModelProducts.SeriesIds.Contains(id);
            string flag = (enabledByDefault ? PriceMuted : PriceSubscribed) + id;
            if (enabled == enabledByDefault) Flags.Remove(flag);
            else Flags.Add(flag);
            if (!enabled) FilterPendingMarketPriceAlerts();
        }

        private bool CanReceiveMarketPriceAlert(string id) => IsMarketPriceSubscribed(id)
            && (!ModelProducts.SeriesIds.Contains(id) || ModelPriceAlertsAvailable);

        public void FilterPendingMarketPriceAlerts()
        {
            if (MarketPriceAlerts.Count == 0) return;
            MarketPriceAlerts = MarketPriceAlerts.Where(move => CanReceiveMarketPriceAlert(move.CategoryId)).ToArray();
        }

        private List<double> CurrentMarketPrices(int day)
        {
            var prices = PartCatalog.Categories.Select(c => PartCatalog.All.Where(p => p.Category == c)
                .Average(p => (double)Market.PriceOf(p).Cents)).ToList();
            prices.Add(Ingredients.All.Where(i => !FishingCatalog.RequiresCatch(i.Id)).Select(i => (double)i.Price.Cents)
                .Append(Pantry.RicePricePerKg.Cents).Average() * IndustryCycles.WholesaleMarketFactor(FarmBusiness, day, Market.CommoditySeed));
            prices.Add(Gifts.All.Where(g => g.SoldAt == Gifts.TobaccoBusiness).Average(g => (double)g.Price.Cents) * IndustryCycles.WholesaleMarketFactor(Gifts.TobaccoBusiness, day, Market.CommoditySeed));
            prices.Add(FurnitureCatalog.All.Where(f => !f.Starter).Average(f => (double)f.Price.Cents));
            prices.Add(FastFoodCatalog.All.Average(f => (double)f.Price.Cents) * IndustryCycles.WholesaleMarketFactor(FastFoodBusiness, day, Market.CommoditySeed));
            prices.Add(Vehicles.All.Where(v => v.Purchasable).Average(v => (double)v.Price.Cents));
            prices.Add(PhonePrice.Cents);
            prices.Add(ClothingProducts.All.Average(o => o.Price.Cents * IndustryCycles.GoodsFactor(o, day, Market.CommoditySeed)));
            prices.AddRange(_propertyPriceFactors.Select(v => v * 10000));
            return prices;
        }

        private void RecordPriceDay(bool announceMoves = false)
        {
            int day = Clock.Today.TotalDays;
            var existing = _priceDays.FirstOrDefault(p => p.Day == day);
            if (existing != null)
            {
                if (existing.SchemaVersion < 1)
                {
                    existing.Prices[7] = CurrentMarketPrices(day)[7];
                    existing.SchemaVersion = 1;
                }
                return;
            }
            var prices = CurrentMarketPrices(day);
            var yesterday = _priceDays.LastOrDefault();
            _priceDays.Add(new PriceDay { Day = day, SchemaVersion = 1, Prices = prices.ToArray() });
            if (announceMoves && yesterday?.Day == day - 1)
                RecordMarketPriceMoves(day, yesterday.Prices, prices, yesterday.SchemaVersion);
            int elapsed = day - EconomicReportAnchorDay;
            if (EconomicReportAutomatic && _priceDays.Count > 1 && elapsed > 0 && elapsed % EconomicReportPeriodDays == 0)
            {
                EconomicReportPending = true;
                Notice?.Invoke("新一期经济简报已出，可在“物价 → 经济简报”查看收支结算与物价涨跌。");
            }
        }

        private void RecordMarketPriceMoves(int day, double[] previous, List<double> current, int previousSchema = 1)
        {
            var moves = new List<MarketPriceMove>();
            void Compare(string id, string name, double before, double after)
            {
                if (before <= 0 || !CanReceiveMarketPriceAlert(id)) return;
                double change = after / before - 1;
                if (Math.Abs(change) > MarketPriceAlertThreshold + 1e-9)
                    moves.Add(new MarketPriceMove { CategoryId = id, Name = name, Change = change });
            }
            for (int i = 0; i < StandardPriceCategories.Length; i++)
                if (i != 7 || previousSchema >= 1) Compare(StandardPriceCategories[i].Id, StandardPriceCategories[i].Name, previous[i], current[i]);

            // Wholesale and retail prices share the model series multiplier.
            foreach (string series in ModelProducts.SeriesIds)
                Compare(series, ModelPriceReportName(series),
                    IndustryCycles.ModelFactor(series, day - 1, Market.CommoditySeed),
                    IndustryCycles.ModelFactor(series, day, Market.CommoditySeed));

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
            MarketPriceAlerts = (data.marketPriceAlerts ?? Array.Empty<MarketPriceMove>())
                .Where(move => move != null)
                .Select(move =>
                {
                    string name = move.Name?.Replace("（模型批发）", "（模型行情）");
                    var category = MarketPriceCategories.FirstOrDefault(item => item.Id == move.CategoryId || item.Name == name);
                    return new MarketPriceMove { CategoryId = category.Id, Name = category.Name, Change = move.Change };
                }).Where(move => move.CategoryId != null).ToArray();
            // Keep historical quotes, but compare tomorrow against today's quotes under the new rules.
            if (string.IsNullOrEmpty(data.commodityPriceSeed) && _priceDays.LastOrDefault() is PriceDay latest
                && latest.Day == Clock.Today.TotalDays)
                { latest.Prices = CurrentMarketPrices(latest.Day).ToArray(); latest.SchemaVersion = 1; }
            RecordPriceDay();
        }
    }
}
