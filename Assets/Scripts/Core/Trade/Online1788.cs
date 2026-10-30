using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Trade
{
    public enum StoreQuality
    {
        /// <summary>靠谱：价格低、次品少，但评分不起眼。</summary>
        Good,

        /// <summary>坑货：最便宜、评分刷得很高，次品多。</summary>
        Shoddy,

        /// <summary>品牌店：贵，几乎没次品。</summary>
        Pricey,
    }

    public sealed class OnlineStore
    {
        public string Id { get; }
        public string Name { get; }
        public StoreQuality Quality { get; }

        /// <summary>价格 = 市场价 × 这个系数。</summary>
        public double PriceFactor { get; }

        public double DefectRate { get; }

        /// <summary>平台上显示的评分（不一定可信）。</summary>
        public double ShownRating { get; }

        public int ShownSales { get; }
        public IReadOnlyList<string> Reviews { get; }

        /// <summary>一开始就能在平台搜到；否则要靠推荐发现。</summary>
        public bool VisibleAtStart { get; }

        public OnlineStore(string id, string name, StoreQuality quality, double priceFactor, double defectRate,
            double shownRating, int shownSales, bool visibleAtStart, params string[] reviews)
        {
            Id = id;
            Name = name;
            Quality = quality;
            PriceFactor = priceFactor;
            DefectRate = defectRate;
            ShownRating = shownRating;
            ShownSales = shownSales;
            VisibleAtStart = visibleAtStart;
            Reviews = reviews;
        }
    }

    /// <summary>玩家自己和某家店打交道的记录（试单）。</summary>
    public sealed class StoreRecord
    {
        public int ItemsReceived;
        public int Defects;
    }

    public sealed class OnlineOrderLine
    {
        public string PartId;
        public int Count;
    }

    public sealed class OnlineOrder
    {
        public int Id;
        public string StoreId;
        public List<OnlineOrderLine> Lines = new List<OnlineOrderLine>();
        public long DeliverAtMinute;
        public long TotalCents;

        /// <summary>送到家里（放在家里纸箱）；否则送到店里上货架。</summary>
        public bool ToHome;

        /// <summary>送到哪家店（铺面 id）；null = 主店。</summary>
        public string ShopId;
    }

    /// <summary>"1788" 进货平台：比档口便宜，但第二天才到，还可能收到次品。用智能手机上的 1788 app 下单。</summary>
    public sealed class Online1788
    {
        public const int DeliveryMinuteOfDay = 10 * 60;
        public static readonly Money ShippingFee = Money.FromYuan(15);

        public static readonly OnlineStore[] Stores =
        {
            new OnlineStore("s_star", "星河数码旗舰店", StoreQuality.Pricey, 0.95, 0.01, 4.9, 52000, true,
                "正品，包装完好，就是贵了点。", "官方授权，放心。", "比电脑城还贵？"),
            new OnlineStore("s_cheap", "超低价电脑配件批发", StoreQuality.Shoddy, 0.74, 0.32, 4.8, 98000, true,
                "便宜！好评！（返现 5 元）", "收到的内存点不亮，客服说是我不会装。", "好评好评好评", "显卡有矿渣味……"),
            new OnlineStore("s_mid", "华强北老陈", StoreQuality.Good, 0.84, 0.05, 4.5, 3100, true,
                "价格实在，偶尔有一件小问题，换货快。", "老板人不错。", "发货慢了一天。"),
            new OnlineStore("s_zhou", "老周电子（同行推荐）", StoreQuality.Good, 0.80, 0.03, 4.3, 860, false,
                "同行都在这拿货，懂的都懂。", "没有花里胡哨的页面，东西靠谱。"),
            new OnlineStore("s_flash", "闪电优选", StoreQuality.Shoddy, 0.78, 0.22, 4.7, 41000, false,
                "物流快。", "硬盘是翻新的。", "包装像被拆过。"),
        };

        private readonly HashSet<string> _discovered = new HashSet<string>();
        private readonly Dictionary<string, StoreRecord> _records = new Dictionary<string, StoreRecord>();
        private readonly List<OnlineOrder> _pending = new List<OnlineOrder>();

        public IReadOnlyList<OnlineOrder> Pending => _pending;
        public int NextOrderId { get; set; } = 1;
        public int TotalOrders { get; set; }

        public Online1788()
        {
            foreach (OnlineStore s in Stores)
            {
                if (s.VisibleAtStart) _discovered.Add(s.Id);
            }
        }

        public static OnlineStore GetStore(string id)
        {
            foreach (OnlineStore s in Stores)
            {
                if (s.Id == id) return s;
            }
            throw new KeyNotFoundException($"店铺不存在：{id}");
        }

        public bool IsDiscovered(string storeId) => _discovered.Contains(storeId);

        /// <summary>发现一家新店；已发现返回 false。</summary>
        public bool Discover(string storeId) => _discovered.Add(storeId);

        public IEnumerable<string> DiscoveredIds => _discovered;

        public StoreRecord RecordOf(string storeId)
        {
            if (!_records.TryGetValue(storeId, out StoreRecord r))
            {
                r = new StoreRecord();
                _records[storeId] = r;
            }
            return r;
        }

        public IEnumerable<KeyValuePair<string, StoreRecord>> Records => _records;

        public static Money PriceOf(OnlineStore store, Money marketPrice)
        {
            return Money.FromYuan((long)Math.Round(marketPrice.Yuan * store.PriceFactor));
        }

        public void AddPending(OnlineOrder order)
        {
            _pending.Add(order);
        }

        /// <summary>取出到时间的订单。</summary>
        public List<OnlineOrder> TakeDue(GameTimestamp now)
        {
            var due = new List<OnlineOrder>();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].DeliverAtMinute <= now.TotalMinutes)
                {
                    due.Add(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }
            due.Reverse();
            return due;
        }

        /// <summary>还没送到店里的货要占的空间（送家里的不占店里的仓位）。</summary>
        public double PendingSpace()
        {
            double s = 0;
            foreach (OnlineOrder o in _pending)
            {
                if (o.ToHome) continue;
                foreach (OnlineOrderLine l in o.Lines)
                    s += Inventory.Stock.SpaceOf(PartCatalog.Get(l.PartId), true, l.Count);
            }
            return s;
        }

        public void Restore(IEnumerable<string> discovered, IEnumerable<KeyValuePair<string, StoreRecord>> records,
            IEnumerable<OnlineOrder> pending)
        {
            _discovered.Clear();
            foreach (string d in discovered) _discovered.Add(d);
            _records.Clear();
            foreach (var kv in records) _records[kv.Key] = kv.Value;
            _pending.Clear();
            _pending.AddRange(pending);
        }
    }
}
