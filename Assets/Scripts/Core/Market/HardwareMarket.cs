using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Market
{
    public enum MacroFactor
    {
        Crypto,
        AiDemand,
    }

    public sealed class NewsItem
    {
        public GameDate Date { get; }
        public string Headline { get; }
        public MacroFactor Factor { get; }
        public double Shock { get; }

        public NewsItem(GameDate date, string headline, MacroFactor factor, double shock)
        {
            Date = date;
            Headline = headline;
            Factor = factor;
            Shock = shock;
        }
    }

    /// <summary>
    /// 硬件行情：新闻（原因）→ 宏观指标（币圈热度、AI 需求，-1~1）→ 品类价格指数。
    /// 每天开盘前更新一次。
    /// </summary>
    public sealed class HardwareMarket
    {
        public const int HistoryDays = 30;

        private static readonly MacroFactor[] Factors = { MacroFactor.Crypto, MacroFactor.AiDemand };

        /// <summary>品类对各宏观指标的敏感度（指数 = 1 + Σ 敏感度 × 指标）。</summary>
        private static readonly Dictionary<PartCategory, double[]> Sensitivity = new Dictionary<PartCategory, double[]>
        {
            [PartCategory.Gpu] = new[] { 0.35, 0.15 },
            [PartCategory.Memory] = new[] { 0.05, 0.22 },
            [PartCategory.Storage] = new[] { 0.14, 0.06 },
            [PartCategory.Cpu] = new[] { 0.02, 0.06 },
            [PartCategory.Motherboard] = new[] { 0.0, 0.02 },
            [PartCategory.PowerCase] = new[] { 0.08, 0.02 },
        };

        private static readonly Dictionary<MacroFactor, string[]> UpNews = new Dictionary<MacroFactor, string[]>
        {
            [MacroFactor.Crypto] = new[] { "部分矿场开始补货，显卡询价增多", "海外矿场追加少量显卡和硬盘订单" },
            [MacroFactor.AiDemand] = new[] { "几家软件公司追加内存和显卡订单", "高校公布新学期的装机采购计划" },
        };

        private static readonly Dictionary<MacroFactor, string[]> DownNews = new Dictionary<MacroFactor, string[]>
        {
            [MacroFactor.Crypto] = new[] { "有矿场拆下闲置显卡，交给经销商出货", "几家矿场推迟设备采购" },
            [MacroFactor.AiDemand] = new[] { "服务器厂商调整排期，内存订单减少", "部分算力采购推迟，内存报价略松" },
        };

        private readonly GameRandom _rng;
        private int _day;
        private readonly double[] _factor = new double[Factors.Length];
        private readonly Dictionary<PartCategory, double> _index = new Dictionary<PartCategory, double>();
        private readonly Dictionary<PartCategory, List<double>> _history = new Dictionary<PartCategory, List<double>>();
        private readonly List<NewsItem> _news = new List<NewsItem>();
        private readonly Dictionary<PartCategory, double> _bias = new Dictionary<PartCategory, double>();
        private readonly List<(int day, PartCategory category, double delta)> _shocks = new List<(int, PartCategory, double)>();

        /// <summary>每天衰减后保留的比例：小道消息带来的短期价格偏移会逐渐消退。</summary>
        public const double BiasRetention = 0.88;
        public const double MaxBias = 0.30;

        public IReadOnlyList<NewsItem> News => _news;
        public GameRandom Random => _rng;
        public ulong CommoditySeed { get; internal set; }

        public HardwareMarket(GameRandom rng, int day = 0)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            CommoditySeed = rng.State;
            _day = day;
            foreach (PartCategory c in PartCatalog.Categories)
            {
                _history[c] = new List<double>();
            }
            Recompute();
        }

        public double FactorValue(MacroFactor f) => _factor[(int)f];

        public double Index(PartCategory c) => _index[c];

        public IReadOnlyList<double> History(PartCategory c) => _history[c];

        public static string FactorName(MacroFactor f)
        {
            switch (f)
            {
                case MacroFactor.Crypto: return "币圈热度";
                case MacroFactor.AiDemand: return "AI 需求";
                default: return f.ToString();
            }
        }

        /// <summary>今天的市场价（零售参考价），取整到元。</summary>
        public Money PriceOf(PartDef part)
        {
            return Money.FromYuan((long)Math.Round(part.BasePrice.Yuan * _index[part.Category]));
        }

        /// <summary>新的一天：指标回归 + 随机波动 + 新闻冲击，然后更新价格指数。</summary>
        public void OnNewDay(GameDate today)
        {
            _day = today.TotalDays;
            for (int i = 0; i < _factor.Length; i++)
                _factor[i] = Clamp(_factor[i] * .94 + _rng.Range(-.02, .02));

            ApplyBias(today);
            EnsureOutlook(today);

            NewsItem scripted = ScriptedNews(today);
            if (scripted != null) Apply(scripted);
            else if (_rng.Chance(.10)) Apply(RandomNews(today));

            Recompute();
        }

        /// <summary>读档：恢复指标、新闻与历史。</summary>
        public void Restore(double[] factors, IEnumerable<NewsItem> news, Dictionary<PartCategory, List<double>> history)
        {
            for (int i = 0; i < _factor.Length && i < factors.Length; i++) _factor[i] = factors[i];
            _news.Clear();
            foreach (var item in news)
                if ((uint)item.Factor < (uint)_factor.Length) _news.Add(item);
            foreach (var kv in history)
            {
                _history[kv.Key].Clear();
                _history[kv.Key].AddRange(kv.Value);
            }
            foreach (PartCategory c in PartCatalog.Categories) _index[c] = ComputeIndex(c);
        }

        public double[] FactorValues() => (double[])_factor.Clone();

        public IReadOnlyList<(int day, PartCategory category, double delta)> PendingShocks => _shocks;

        /// <summary>安排一次品类的短期价格冲击：到了那天价格被推高/压低一截，之后逐日回落。</summary>
        public void ScheduleShock(GameDate day, PartCategory category, double delta)
        {
            _shocks.Add((day.TotalDays, category, delta));
        }

        public bool OutlookEnabled { get; set; } = true;
        public void EnsureOutlook(GameDate today)
        {
            if (!OutlookEnabled) return;
            foreach (PartCategory category in PartCatalog.Categories)
            {
                bool planned = false;
                foreach (var shock in _shocks) if (shock.category == category && shock.day > today.TotalDays) { planned = true; break; }
                if (!planned) ScheduleShock(today.AddDays(_rng.Next(5, 11)), category, (_rng.Chance(.5) ? 1 : -1) * _rng.Range(.006, .015));
            }
        }

        public double BiasOf(PartCategory c) => _bias.TryGetValue(c, out double b) ? b : 0;

        /// <summary>按品类顺序导出当前偏移，存档用。</summary>
        public double[] BiasValues()
        {
            var r = new double[PartCatalog.Categories.Length];
            for (int i = 0; i < r.Length; i++) r[i] = BiasOf(PartCatalog.Categories[i]);
            return r;
        }

        /// <summary>读档：恢复偏移与待生效的冲击，并按现在的偏移重算价格指数（不追加历史）。</summary>
        public void RestoreBias(double[] bias, IEnumerable<(int day, PartCategory category, double delta)> shocks)
        {
            _bias.Clear();
            for (int i = 0; bias != null && i < bias.Length && i < PartCatalog.Categories.Length; i++)
                if (bias[i] != 0) _bias[PartCatalog.Categories[i]] = bias[i];
            _shocks.Clear();
            _shocks.AddRange(shocks);
            foreach (PartCategory c in PartCatalog.Categories) _index[c] = ComputeIndex(c);
        }

        private void ApplyBias(GameDate today)
        {
            foreach (PartCategory c in new List<PartCategory>(_bias.Keys)) _bias[c] *= BiasRetention;
            for (int i = _shocks.Count - 1; i >= 0; i--)
            {
                if (_shocks[i].day > today.TotalDays) continue;
                PartCategory c = _shocks[i].category;
                _bias[c] = Math.Max(-MaxBias, Math.Min(MaxBias, BiasOf(c) + _shocks[i].delta));
                _shocks.RemoveAt(i);
            }
        }

        /// <summary>第 10 天的矿场补货消息，属于普通行情。</summary>
        private static NewsItem ScriptedNews(GameDate today)
        {
            if (today.TotalDays == 9)
                return new NewsItem(today, "部分矿场开始补货，显卡询价比前几天多", MacroFactor.Crypto, .065);
            return null;
        }

        private NewsItem RandomNews(GameDate today)
        {
            MacroFactor f = Factors[_rng.Next(0, Factors.Length)];
            bool up = _rng.Chance(0.5);
            string[] pool = up ? UpNews[f] : DownNews[f];
            string headline = pool[_rng.Next(0, pool.Length)];
            double shock = (up ? 1 : -1) * _rng.Range(.025, .045);
            return new NewsItem(today, headline, f, shock);
        }

        private void Apply(NewsItem n)
        {
            _factor[(int)n.Factor] = Clamp(_factor[(int)n.Factor] + n.Shock);
            _news.Add(n);
        }

        private void Recompute()
        {
            foreach (PartCategory c in PartCatalog.Categories)
            {
                double v = ComputeIndex(c);
                _index[c] = v;
                List<double> h = _history[c];
                h.Add(v);
                if (h.Count > HistoryDays) h.RemoveAt(0);
            }
        }

        private double ComputeIndex(PartCategory c)
        {
            double[] s = Sensitivity[c];
            double v = 1.0;
            for (int i = 0; i < _factor.Length; i++) v += s[i] * _factor[i];
            v += BiasOf(c);
            string key = c.ToString();
            v *= IndustryCycles.OrdinaryFactor(key, _day) * IndustryCycles.CommodityEventFactor(key, _day, CommoditySeed);
            return Math.Max(0.6, Math.Min(1.8, v));
        }

        private static double Clamp(double v) => Math.Max(-1, Math.Min(1, v));
    }
}
