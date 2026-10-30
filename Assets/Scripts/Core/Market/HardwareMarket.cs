using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Market
{
    public enum MacroFactor
    {
        Crypto,
        AiDemand,
        NewGeneration,
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
    /// 硬件行情：新闻（原因）→ 宏观指标（币圈热度、AI 需求、新品周期，-1~1）→ 品类价格指数。
    /// 每天开盘前更新一次。
    /// </summary>
    public sealed class HardwareMarket
    {
        public const int HistoryDays = 30;

        private static readonly MacroFactor[] Factors = { MacroFactor.Crypto, MacroFactor.AiDemand, MacroFactor.NewGeneration };

        /// <summary>品类对各宏观指标的敏感度（指数 = 1 + Σ 敏感度 × 指标）。</summary>
        private static readonly Dictionary<PartCategory, double[]> Sensitivity = new Dictionary<PartCategory, double[]>
        {
            [PartCategory.Gpu] = new[] { 0.35, 0.15, -0.05 },
            [PartCategory.Memory] = new[] { 0.05, 0.22, -0.03 },
            [PartCategory.Storage] = new[] { 0.14, 0.06, -0.02 },
            [PartCategory.Cpu] = new[] { 0.02, 0.06, -0.14 },
            [PartCategory.Motherboard] = new[] { 0.0, 0.02, -0.10 },
            [PartCategory.PowerCase] = new[] { 0.08, 0.02, 0.0 },
        };

        private static readonly Dictionary<MacroFactor, string[]> UpNews = new Dictionary<MacroFactor, string[]>
        {
            [MacroFactor.Crypto] = new[] { "某虚拟币一周涨了 40%，矿老板又开始扫货显卡", "海外矿场扩产，显卡、硬盘出货紧张" },
            [MacroFactor.AiDemand] = new[] { "大模型创业潮，公司抢购内存和显卡", "高校开设 AI 专业，学生装机需求上升" },
            [MacroFactor.NewGeneration] = new[] { "锐芯发布新一代处理器，老款 CPU、主板准备降价清库存", "英擎新品下月上市，经销商提前甩货" },
        };

        private static readonly Dictionary<MacroFactor, string[]> DownNews = new Dictionary<MacroFactor, string[]>
        {
            [MacroFactor.Crypto] = new[] { "虚拟币暴跌，矿机二手显卡大量流入市场", "多地清理矿场，显卡需求骤降" },
            [MacroFactor.AiDemand] = new[] { "AI 公司融资遇冷，服务器内存订单减少", "大厂暂停算力采购，内存价格松动" },
            [MacroFactor.NewGeneration] = new[] { "新品延期上市，老款 CPU 价格回稳", "新一代处理器缺货，旧款反而抢手" },
        };

        private readonly GameRandom _rng;
        private readonly double[] _factor = new double[3];
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

        public HardwareMarket(GameRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
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
                default: return "新品周期";
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
            for (int i = 0; i < _factor.Length; i++)
                _factor[i] = Clamp(_factor[i] * 0.9 + _rng.Gaussian(0.05));

            ApplyBias(today);
            EnsureOutlook(today);

            NewsItem scripted = ScriptedNews(today);
            if (scripted != null) Apply(scripted);
            else if (_rng.Chance(0.3)) Apply(RandomNews(today));

            Recompute();
        }

        /// <summary>读档：恢复指标、新闻与历史。</summary>
        public void Restore(double[] factors, IEnumerable<NewsItem> news, Dictionary<PartCategory, List<double>> history)
        {
            for (int i = 0; i < _factor.Length && i < factors.Length; i++) _factor[i] = factors[i];
            _news.Clear();
            _news.AddRange(news);
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
                if (!planned) ScheduleShock(today.AddDays(2 + _rng.Next(0, 3)), category, (_rng.Chance(0.5) ? 1 : -1) * _rng.Range(0.06, 0.12));
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

        /// <summary>第一个月固定的一次行情波动：第 10 天矿潮，第 20 天新品发布。</summary>
        private static NewsItem ScriptedNews(GameDate today)
        {
            if (today.TotalDays == 9)
                return new NewsItem(today, "【头条】某虚拟币一夜翻倍，全国矿潮来袭，显卡被抢购一空", MacroFactor.Crypto, 0.9);
            if (today.TotalDays == 19)
                return new NewsItem(today, "【头条】锐芯新一代处理器正式发布，老款 CPU、主板集体降价", MacroFactor.NewGeneration, 0.8);
            return null;
        }

        private NewsItem RandomNews(GameDate today)
        {
            MacroFactor f = Factors[_rng.Next(0, Factors.Length)];
            bool up = _rng.Chance(0.5);
            string[] pool = up ? UpNews[f] : DownNews[f];
            string headline = pool[_rng.Next(0, pool.Length)];
            double shock = (up ? 1 : -1) * _rng.Range(0.2, 0.45);
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
            return Math.Max(0.6, Math.Min(1.8, v));
        }

        private static double Clamp(double v) => Math.Max(-1, Math.Min(1, v));
    }
}
