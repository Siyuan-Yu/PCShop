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
            return Math.Max(0.6, Math.Min(1.8, v));
        }

        private static double Clamp(double v) => Math.Max(-1, Math.Min(1, v));
    }
}
