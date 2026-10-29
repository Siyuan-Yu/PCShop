using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Shop
{
    public enum ShopSize
    {
        Small,
        Medium,
        Large,
    }

    [Flags]
    public enum PaymentMethods
    {
        None = 0,
        Cash = 1,
        QrCode = 2,
        SelfCheckout = 4,
    }

    /// <summary>顾客这一单实际的付款方式。</summary>
    public enum PayChannel
    {
        Cash,
        QrCode,
        SelfCheckout,
    }

    public enum CustomerIntent
    {
        Retail,
        Build,
    }

    public enum CustomerState
    {
        Browsing,
        WaitingCheckout,
        Paid,
        Left,
    }

    public enum LeaveReason
    {
        None,
        Impatient,
        NoCash,
        ShopClosed,
        NoStock,
        TooExpensive,
        OrderPlaced,
    }

    /// <summary>一天营业结束时的小结（给“昨日营业”提示用）。</summary>
    public sealed class DayReport
    {
        public GameDate Date;
        public int Customers;
        public int Paid;
        public Money Revenue;
        public int Orders;
        public int LostTooExpensive;
        public int LostImpatient;
        public int LostNoStock;
        public int LostNoCash;
        public int LostClosed;

        public int Lost => LostTooExpensive + LostImpatient + LostNoStock + LostNoCash + LostClosed;
    }

    public sealed class ShopCustomer
    {
        public int Id { get; }
        public CustomerIntent Intent { get; }
        public PartCategory DesiredCategory { get; }
        public int BuildTier { get; }
        public GameTimestamp ArrivedAt { get; }
        public GameTimestamp ReadyAt { get; }
        public bool CarriesCash { get; }

        /// <summary>结账时用的付款方式（结账后才有值）。</summary>
        public PayChannel PaidWith { get; internal set; }

        public CustomerState State { get; internal set; }
        public LeaveReason Reason { get; internal set; }
        public GameTimestamp WaitingSince { get; internal set; }

        /// <summary>挑中的货（排队时已从货架上拿下）。</summary>
        public PartDef Item { get; internal set; }

        public bool ItemBoxed { get; internal set; }
        public Money Spend { get; internal set; }

        internal ShopCustomer(int id, CustomerIntent intent, PartCategory category, int buildTier,
            GameTimestamp arrivedAt, GameTimestamp readyAt, bool carriesCash)
        {
            Id = id;
            Intent = intent;
            DesiredCategory = category;
            BuildTier = buildTier;
            ArrivedAt = arrivedAt;
            ReadyAt = readyAt;
            CarriesCash = carriesCash;
            State = CustomerState.Browsing;
        }
    }

    /// <summary>装机订单：顾客下单后离开，玩家装好后自动交付收款；超过期限顾客取消。</summary>
    public sealed class BuildOrder
    {
        public int Id { get; }
        public int Tier { get; }
        public Money Price { get; }
        public GameTimestamp PlacedAt { get; }
        public GameTimestamp DueAt { get; }

        public BuildOrder(int id, int tier, Money price, GameTimestamp placedAt, GameTimestamp dueAt)
        {
            Id = id;
            Tier = tier;
            Price = price;
            PlacedAt = placedAt;
            DueAt = dueAt;
        }

        public static Money LaborFee(int tier) => Money.FromYuan(tier == 0 ? 150 : tier == 1 ? 220 : 320);
    }

    /// <summary>营业参数。数值均为占位，等原型试玩后平衡。</summary>
    public sealed class ShopSettings
    {
        /// <summary>位置最好、规格系数为 1、知名度为 0 时，高峰时段每小时进店人数。</summary>
        public double BaseCustomersPerHour = 2;

        public double WeekendMultiplier = 1.5;
        /// <summary>逛店时间（游戏分钟）。1 倍速下 1 游戏分钟只有 0.25 秒，给顾客留够走到货架、拿货的时间。</summary>
        public int MinBrowseMinutes = 8;
        public int MaxBrowseMinutes = 25;

        /// <summary>排队等结账超过这么多分钟就走。</summary>
        public int PatienceMinutes = 40;

        /// <summary>顾客身上带现金的概率；只收现金时，没带的人挑完就走。</summary>
        public double CashCarryChance = 0.8;

        /// <summary>进店顾客里想装整机的比例。</summary>
        public double BuildOrderChance = 0.06;

        public int BuildOrderDueDays = 2;

        public int CheckoutMinutesCash = 2;
        public int CheckoutMinutesQr = 1;
        public int EmployeeCheckoutMinutes = 3;

        /// <summary>盒装比散件贵的比例。</summary>
        public double BoxedPremium = 0.12;

        /// <summary>盒装让顾客犹豫离开的概率乘以这个系数。</summary>
        public double BoxedTrust = 0.85;

        /// <summary>每小时客流系数（0~23 点）。营业时间由店主决定，夜里几乎没人。</summary>
        public double[] HourFactors =
        {
            0.01, 0.01, 0.01, 0.01, 0.01, 0.02, 0.05, 0.15,
            0.35, 0.6, 0.9, 1.0, 1.0, 0.9, 0.85, 0.9,
            1.0, 1.0, 1.0, 0.9, 0.7, 0.4, 0.15, 0.05,
        };

        public static double SizeFactor(ShopSize size)
        {
            switch (size)
            {
                case ShopSize.Medium: return 1.6;
                case ShopSize.Large: return 2.5;
                default: return 1.0;
            }
        }

        /// <summary>库存容量（格）。</summary>
        public static double Capacity(ShopSize size)
        {
            switch (size)
            {
                case ShopSize.Medium: return 120;
                case ShopSize.Large: return 300;
                default: return 40;
            }
        }

        /// <summary>离地铁站越远客流越低：最近为 1，最远为 0.4。</summary>
        public static double LocationFactor(double distanceToStation, double maxDistance)
        {
            if (maxDistance <= 0) return 1.0;
            double t = Math.Max(0, Math.Min(1, distanceToStation / maxDistance));
            return 1.0 - 0.6 * t;
        }

        /// <summary>按加价倍率算顾客愿意买的概率（加价越高越低）。</summary>
        public static double BuyChance(double markup)
        {
            return Math.Max(0.05, Math.Min(0.97, 0.95 - (markup - 1.0) * 1.6));
        }
    }

    /// <summary>
    /// 一家店的营业模拟：按世界时钟逐分钟生成顾客。
    /// 散件顾客挑货（从货架拿下）后排队等结账，结账（玩家点击或员工）后才入账；
    /// 等太久、付不了钱或嫌贵就离开，拿下的货放回货架。
    /// 装机顾客下订单后离开，由玩家装好交付。没人值守时关门。
    /// </summary>
    public sealed class ShopSimulation : IDisposable
    {
        /// <summary>知名度涨幅：每笔散件成交、每单整机交付。数值偏小，让知名度要经营大半年才满。</summary>
        public const double SaleReputationGain = 0.012;
        public const double BuildReputationGain = 0.4;

        /// <summary>知名度每天自然衰减，不经营就会掉。</summary>
        public const double DailyReputationRetention = 0.998;

        public const string SalesCategory = "零售";
        public const string BuildCategory = "装机";

        private readonly GameClock _clock;
        private readonly Ledger _ledger;
        private readonly HardwareMarket _market;
        private readonly List<ShopCustomer> _active = new List<ShopCustomer>();
        private readonly List<ShopCustomer> _queue = new List<ShopCustomer>();
        private readonly List<BuildOrder> _orders = new List<BuildOrder>();
        private int _nextCustomerId = 1;
        private int _employeeBusyMinutes;
        private bool _wasOpen;
        private double _reputation;
        private double _markup = 1.15;

        public string ShopId { get; }
        public ShopSettings Settings { get; }
        public GameRandom Random { get; }
        public Stock Stock { get; }
        public ShopSize Size { get; set; }
        public double LocationFactor { get; set; } = 1.0;

        /// <summary>招牌灯箱之类的升级带来的客流倍数。</summary>
        public double TrafficMultiplier { get; set; } = 1.0;

        /// <summary>知名度 0~100，每 10 点约增加 10% 客流。</summary>
        public double Reputation
        {
            get => _reputation;
            set => _reputation = Math.Max(0, Math.Min(100, value));
        }

        /// <summary>售价 = 市场价 × 加价倍率（盒装再加溢价）。范围 0.9~1.6。</summary>
        public double Markup
        {
            get => _markup;
            set => _markup = Math.Max(0.9, Math.Min(1.6, value));
        }

        private readonly Dictionary<string, double> _partMarkups = new Dictionary<string, double>();

        /// <summary>单独定过价的配件（配件 id → 加价倍率）；没单独定的跟全局加价。</summary>
        public IReadOnlyDictionary<string, double> PartMarkups => _partMarkups;

        public bool HasPartMarkup(PartDef part) => _partMarkups.ContainsKey(part.Id);

        /// <summary>这件配件现在用的加价倍率。</summary>
        public double MarkupOf(PartDef part) => _partMarkups.TryGetValue(part.Id, out double m) ? m : _markup;

        /// <summary>给一件配件单独定价；null = 恢复跟随全局加价。</summary>
        public void SetPartMarkup(PartDef part, double? markup)
        {
            if (markup == null) _partMarkups.Remove(part.Id);
            else _partMarkups[part.Id] = Math.Max(0.9, Math.Min(1.6, markup.Value));
        }

        public void RestorePartMarkups(IEnumerable<KeyValuePair<string, double>> items)
        {
            _partMarkups.Clear();
            if (items == null) return;
            foreach (var kv in items) _partMarkups[kv.Key] = Math.Max(0.9, Math.Min(1.6, kv.Value));
        }

        public PaymentMethods Accepted { get; set; } = PaymentMethods.Cash;

        /// <summary>玩家是否在店里。</summary>
        public bool PlayerPresent { get; set; }

        /// <summary>员工此刻是否在岗（由会话按值班时间提供）。</summary>
        public Func<GameTimestamp, bool> EmployeeOnDuty { get; set; } = _ => false;

        public IReadOnlyList<ShopCustomer> ActiveCustomers => _active;
        public IReadOnlyList<ShopCustomer> Queue => _queue;
        public IReadOnlyList<BuildOrder> Orders => _orders;
        public int NextOrderId { get; private set; } = 1;

        public int TodayCustomers { get; private set; }
        public int TodayLost { get; private set; }
        public Money TodaySales { get; private set; }

        public event Action<ShopCustomer> CustomerArrived;
        public event Action<ShopCustomer> CustomerReadyToPay;
        public event Action<ShopCustomer> CustomerPaid;
        public event Action<ShopCustomer> CustomerLeft;

        /// <summary>每天零点前触发一次：昨天的营业小结（当天有顾客才触发）。</summary>
        public event Action<DayReport> DayClosed;
        private DayReport _day = new DayReport();
        public event Action<BuildOrder> OrderPlaced;
        public event Action<BuildOrder> OrderExpired;

        public ShopSimulation(string shopId, ShopSize size, GameClock clock, Ledger ledger, HardwareMarket market,
            GameRandom random, ShopSettings settings = null)
        {
            ShopId = shopId;
            Size = size;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _market = market ?? throw new ArgumentNullException(nameof(market));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Settings = settings ?? new ShopSettings();
            Stock = new Stock(ShopSettings.Capacity(size));
            _clock.TimeAdvanced += OnTimeAdvanced;
            _clock.DayAdvanced += OnDayAdvanced;
        }

        /// <summary>装修歇业：当天不营业。</summary>
        public bool ForcedClosed { get; set; }

        public bool IsOpenAt(GameTimestamp t) => !ForcedClosed && (PlayerPresent || EmployeeOnDuty(t));

        public bool IsOpen => IsOpenAt(_clock.Now);

        /// <summary>当前售价。</summary>
        public Money PriceOf(PartDef part, bool boxed)
        {
            double k = MarkupOf(part) * (boxed ? 1.0 + Settings.BoxedPremium : 1.0);
            return Money.FromYuan((long)Math.Round(_market.PriceOf(part).Yuan * k));
        }

        /// <summary>顾客下装机单时的成交价（报价 × 加价，整机不收盒装溢价）。</summary>
        public Money BuildPrice(int tier) => Money.FromYuan((long)Math.Round(QuoteBuild(tier).Yuan * _markup / 1.15));

        /// <summary>装机订单报价：配件市场价合计 + 手工费。</summary>
        public Money QuoteBuild(int tier)
        {
            Money sum = Money.Zero;
            foreach (PartDef p in PartCatalog.BuildList(tier)) sum += _market.PriceOf(p);
            return sum + BuildOrder.LaborFee(tier);
        }

        /// <summary>当前时刻每分钟来客的期望人数。</summary>
        public double ArrivalRatePerMinute(GameTimestamp at)
        {
            double perHour = Settings.BaseCustomersPerHour
                             * Settings.HourFactors[at.Hour]
                             * ShopSettings.SizeFactor(Size)
                             * LocationFactor
                             * TrafficMultiplier
                             * (1.0 + Reputation / 100.0)
                             * (at.Date.IsWeekend ? Settings.WeekendMultiplier : 1.0);
            return perHour / 60.0;
        }

        /// <summary>
        /// 玩家给排队的顾客结账：入账，返回这次结账耗费的游戏分钟（由调用方推进时钟）。
        /// 顾客不在队列中时返回 -1。
        /// </summary>
        public int Checkout(int customerId)
        {
            ShopCustomer c = _queue.Find(x => x.Id == customerId);
            if (c == null) return -1;
            bool cash = c.CarriesCash && (Accepted & PaymentMethods.Cash) != 0;
            CompleteSale(c);
            return cash ? Settings.CheckoutMinutesCash : Settings.CheckoutMinutesQr;
        }

        /// <summary>装这单还缺哪些配件（空列表表示配件齐了）。</summary>
        public List<PartDef> MissingParts(BuildOrder order)
        {
            var missing = new List<PartDef>();
            foreach (PartDef p in PartCatalog.BuildList(order.Tier))
            {
                if (Stock.CountAll(p) == 0) missing.Add(p);
            }
            return missing;
        }

        /// <summary>交付装好的整机：扣配件、收款、涨知名度。配件不齐返回 false。</summary>
        public bool CompleteBuild(int orderId)
        {
            BuildOrder o = _orders.Find(x => x.Id == orderId);
            if (o == null || MissingParts(o).Count > 0) return false;
            foreach (PartDef p in PartCatalog.BuildList(o.Tier)) Stock.TakeOne(p);
            _orders.Remove(o);
            _ledger.Credit(_clock.Today, BuildCategory, o.Price, $"{PartCatalog.TierName(o.Tier)}整机");
            TodaySales += o.Price;
            Reputation += BuildReputationGain;
            return true;
        }

        public void Dispose()
        {
            _clock.TimeAdvanced -= OnTimeAdvanced;
            _clock.DayAdvanced -= OnDayAdvanced;
        }

        /// <summary>读档时恢复订单。</summary>
        public void RestoreOrders(IEnumerable<BuildOrder> orders, int nextOrderId)
        {
            _orders.Clear();
            _orders.AddRange(orders);
            NextOrderId = nextOrderId;
        }

        private void OnDayAdvanced(GameDate previous, GameDate today)
        {
            if (TodayCustomers > 0)
            {
                _day.Date = previous;
                _day.Customers = TodayCustomers;
                _day.Revenue = TodaySales;
                DayClosed?.Invoke(_day);
            }
            _day = new DayReport();
            TodayCustomers = 0;
            TodayLost = 0;
            TodaySales = Money.Zero;
            Reputation *= DailyReputationRetention;
        }

        private void OnTimeAdvanced(GameTimestamp from, GameTimestamp to)
        {
            for (long m = from.TotalMinutes; m < to.TotalMinutes; m++)
                StepMinute(new GameTimestamp(m));
        }

        private void StepMinute(GameTimestamp now)
        {
            ExpireOrders(now);

            bool open = IsOpenAt(now);
            if (!open)
            {
                if (_wasOpen) CloseAll();
                _wasOpen = false;
                return;
            }
            _wasOpen = true;

            if (Random.Chance(ArrivalRatePerMinute(now)))
                Spawn(now);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ShopCustomer c = _active[i];
                if (c.State == CustomerState.Browsing && now >= c.ReadyAt)
                    Decide(c, now);
                else if (c.State == CustomerState.WaitingCheckout &&
                         c.WaitingSince.MinutesUntil(now) >= Settings.PatienceMinutes)
                    Leave(c, LeaveReason.Impatient);
            }

            // 自助结账平台：每分钟自动给排头的顾客结一单，不用点（仍然需要有人在店里营业）。
            if ((Accepted & PaymentMethods.SelfCheckout) != 0 && _queue.Count > 0)
                CompleteSale(_queue[0], true);
            else if (!PlayerPresent && EmployeeOnDuty(now))
            {
                if (_employeeBusyMinutes > 0) _employeeBusyMinutes--;
                else if (_queue.Count > 0)
                {
                    CompleteSale(_queue[0]);
                    _employeeBusyMinutes = Settings.EmployeeCheckoutMinutes;
                }
            }
        }

        private void Spawn(GameTimestamp now)
        {
            bool build = Random.Chance(Settings.BuildOrderChance);
            PartCategory cat = PartCatalog.Categories[Random.Next(0, PartCatalog.Categories.Length)];
            int tier = PickTier();
            int browse = Random.Next(Settings.MinBrowseMinutes, Settings.MaxBrowseMinutes + 1);
            bool cash = Random.Chance(Settings.CashCarryChance);
            var c = new ShopCustomer(_nextCustomerId++, build ? CustomerIntent.Build : CustomerIntent.Retail,
                cat, tier, now, now.AddMinutes(browse), cash);
            _active.Add(c);
            TodayCustomers++;
            CustomerArrived?.Invoke(c);
        }

        private int PickTier()
        {
            double r = Random.NextDouble();
            return r < 0.55 ? 0 : r < 0.9 ? 1 : 2;
        }

        private void Decide(ShopCustomer c, GameTimestamp now)
        {
            if (c.Intent == CustomerIntent.Build)
            {
                if (!Random.Chance(ShopSettings.BuyChance(_markup)))
                {
                    Leave(c, LeaveReason.TooExpensive);
                    return;
                }
                Money price = BuildPrice(c.BuildTier);
                var order = new BuildOrder(NextOrderId++, c.BuildTier, price, now, now.AddMinutes(Settings.BuildOrderDueDays * GameTimestamp.MinutesPerDay));
                _orders.Add(order);
                OrderPlaced?.Invoke(order);
                Leave(c, LeaveReason.OrderPlaced, countAsLost: false);
                return;
            }

            if (!PickItem(c))
            {
                Reputation -= 0.1;
                Leave(c, LeaveReason.NoStock);
                return;
            }

            double leave = 1.0 - ShopSettings.BuyChance(MarkupOf(c.Item));
            if (c.ItemBoxed) leave *= Settings.BoxedTrust;
            if (Random.Chance(leave))
            {
                Stock.ForceAdd(c.Item, c.ItemBoxed, 1);
                c.Item = null;
                Leave(c, LeaveReason.TooExpensive);
                return;
            }

            if (!CanPay(c))
            {
                Stock.ForceAdd(c.Item, c.ItemBoxed, 1);
                c.Item = null;
                Leave(c, LeaveReason.NoCash);
                return;
            }

            c.State = CustomerState.WaitingCheckout;
            c.WaitingSince = now;
            _queue.Add(c);
            CustomerReadyToPay?.Invoke(c);
        }

        /// <summary>从想要的品类里挑一件有货的（优先想要的档次）；没货时有一半人会改买别的品类。</summary>
        private bool PickItem(ShopCustomer c)
        {
            if (TryPickIn(c, c.DesiredCategory)) return true;
            if (!Random.Chance(0.5)) return false;
            foreach (PartCategory cat in PartCatalog.Categories)
            {
                if (cat != c.DesiredCategory && TryPickIn(c, cat)) return true;
            }
            return false;
        }

        private bool TryPickIn(ShopCustomer c, PartCategory cat)
        {
            var candidates = new List<(PartDef part, bool boxed)>();
            foreach (PartDef p in PartCatalog.InCategory(cat))
            {
                if (Stock.Count(p, true) > 0) candidates.Add((p, true));
                if (Stock.Count(p, false) > 0) candidates.Add((p, false));
            }
            if (candidates.Count == 0) return false;

            (PartDef part, bool boxed) pick = candidates[0];
            int bestDiff = int.MaxValue;
            foreach (var cand in candidates)
            {
                int diff = Math.Abs(cand.part.Tier - c.BuildTier) * 2 + Random.Next(0, 2);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    pick = cand;
                }
            }
            Stock.TryRemove(pick.part, pick.boxed, 1);
            c.Item = pick.part;
            c.ItemBoxed = pick.boxed;
            c.Spend = PriceOf(pick.part, pick.boxed);
            return true;
        }

        private bool CanPay(ShopCustomer c)
        {
            if ((Accepted & (PaymentMethods.QrCode | PaymentMethods.SelfCheckout)) != 0) return true;
            return c.CarriesCash && (Accepted & PaymentMethods.Cash) != 0;
        }

        private void CompleteSale(ShopCustomer c, bool selfCheckout = false)
        {
            c.PaidWith = selfCheckout ? PayChannel.SelfCheckout : c.CarriesCash && (Accepted & PaymentMethods.Cash) != 0 ? PayChannel.Cash : PayChannel.QrCode;
            _queue.Remove(c);
            _active.Remove(c);
            c.State = CustomerState.Paid;
            _ledger.Credit(_clock.Today, SalesCategory, c.Spend, c.Item.Name + (c.ItemBoxed ? "（盒装）" : "（散件）"));
            TodaySales += c.Spend;
            _day.Paid++;
            Reputation += SaleReputationGain;
            CustomerPaid?.Invoke(c);
        }

        private void Leave(ShopCustomer c, LeaveReason reason, bool countAsLost = true)
        {
            if (c.State == CustomerState.WaitingCheckout && c.Item != null)
            {
                Stock.ForceAdd(c.Item, c.ItemBoxed, 1);
                c.Item = null;
            }
            if (reason == LeaveReason.Impatient) Reputation -= 0.3;
            _active.Remove(c);
            _queue.Remove(c);
            c.State = CustomerState.Left;
            c.Reason = reason;
            if (countAsLost) TodayLost++;
            switch (reason)
            {
                case LeaveReason.TooExpensive: _day.LostTooExpensive++; break;
                case LeaveReason.Impatient: _day.LostImpatient++; break;
                case LeaveReason.NoStock: _day.LostNoStock++; break;
                case LeaveReason.NoCash: _day.LostNoCash++; break;
                case LeaveReason.ShopClosed: _day.LostClosed++; break;
                case LeaveReason.OrderPlaced: _day.Orders++; break;
            }
            CustomerLeft?.Invoke(c);
        }

        private void ExpireOrders(GameTimestamp now)
        {
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                BuildOrder o = _orders[i];
                if (now < o.DueAt) continue;
                _orders.RemoveAt(i);
                Reputation -= 3;
                OrderExpired?.Invoke(o);
            }
        }

        private void CloseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Leave(_active[i], LeaveReason.ShopClosed);
        }
    }
}
