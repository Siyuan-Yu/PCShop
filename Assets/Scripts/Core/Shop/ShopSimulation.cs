using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
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
    }

    public sealed class ShopCustomer
    {
        public int Id { get; }
        public GameTimestamp ArrivedAt { get; }
        public GameTimestamp ReadyAt { get; }
        public Money Spend { get; }
        public bool CarriesCash { get; }
        public CustomerState State { get; internal set; }
        public LeaveReason Reason { get; internal set; }
        public GameTimestamp WaitingSince { get; internal set; }

        internal ShopCustomer(int id, GameTimestamp arrivedAt, GameTimestamp readyAt, Money spend, bool carriesCash)
        {
            Id = id;
            ArrivedAt = arrivedAt;
            ReadyAt = readyAt;
            Spend = spend;
            CarriesCash = carriesCash;
            State = CustomerState.Browsing;
        }
    }

    /// <summary>营业参数。数值均为占位，等原型试玩后平衡。</summary>
    public sealed class ShopSettings
    {
        /// <summary>位置最好、规格系数为 1、知名度为 0 时，高峰时段每小时进店人数。</summary>
        public double BaseCustomersPerHour = 10;

        public double WeekendMultiplier = 1.5;
        public int MinBrowseMinutes = 3;
        public int MaxBrowseMinutes = 12;

        /// <summary>排队等结账超过这么多分钟就走。</summary>
        public int PatienceMinutes = 15;

        public long MinSpendYuan = 20;
        public long MaxSpendYuan = 300;

        /// <summary>顾客身上带现金的概率；只收现金时，没带的人挑完就走。</summary>
        public double CashCarryChance = 0.8;

        public int CheckoutMinutesCash = 2;
        public int CheckoutMinutesQr = 1;

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

        /// <summary>离地铁站越远客流越低：最近为 1，最远为 0.4。</summary>
        public static double LocationFactor(double distanceToStation, double maxDistance)
        {
            if (maxDistance <= 0) return 1.0;
            double t = Math.Max(0, Math.Min(1, distanceToStation / maxDistance));
            return 1.0 - 0.6 * t;
        }
    }

    /// <summary>
    /// 一家店的营业模拟：按世界时钟逐分钟生成顾客，顾客挑货后排队等结账；
    /// 结账（玩家点击、员工或自助结账）后才入账，等太久或付不了钱就离开。
    /// 没人值守时关门，店里的顾客全部离开。
    /// </summary>
    public sealed class ShopSimulation : IDisposable
    {
        public const string SalesCategory = "零售";

        private readonly GameClock _clock;
        private readonly Ledger _ledger;
        private readonly Random _rng;
        private readonly List<ShopCustomer> _active = new List<ShopCustomer>();
        private readonly List<ShopCustomer> _queue = new List<ShopCustomer>();
        private int _nextId = 1;
        private bool _attended;

        public string ShopId { get; }
        public ShopSettings Settings { get; }
        public ShopSize Size { get; set; }
        public double LocationFactor { get; set; } = 1.0;

        /// <summary>知名度 0~100，每 10 点约增加 10% 客流。</summary>
        public double Reputation { get; set; }

        public PaymentMethods Accepted { get; set; } = PaymentMethods.Cash;

        public IReadOnlyList<ShopCustomer> ActiveCustomers => _active;

        /// <summary>排队等结账的顾客，按到达柜台的先后排序。</summary>
        public IReadOnlyList<ShopCustomer> Queue => _queue;

        public int TodayCustomers { get; private set; }
        public int TodayLost { get; private set; }
        public Money TodaySales { get; private set; }

        public event Action<ShopCustomer> CustomerArrived;
        public event Action<ShopCustomer> CustomerReadyToPay;
        public event Action<ShopCustomer> CustomerPaid;
        public event Action<ShopCustomer> CustomerLeft;

        public ShopSimulation(string shopId, GameClock clock, Ledger ledger, int seed, ShopSettings settings = null)
        {
            ShopId = shopId;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _rng = new Random(seed);
            Settings = settings ?? new ShopSettings();
            _clock.TimeAdvanced += OnTimeAdvanced;
            _clock.DayAdvanced += OnDayAdvanced;
        }

        /// <summary>有人值守才营业。置为 false 时关门，店里的顾客全部离开。</summary>
        public bool IsOpen
        {
            get => _attended;
            set
            {
                if (_attended == value) return;
                _attended = value;
                if (!value) CloseAll();
            }
        }

        /// <summary>当前时刻每分钟来客的期望人数。</summary>
        public double ArrivalRatePerMinute(GameTimestamp at)
        {
            double perHour = Settings.BaseCustomersPerHour
                             * Settings.HourFactors[at.Hour]
                             * ShopSettings.SizeFactor(Size)
                             * LocationFactor
                             * (1.0 + Reputation / 100.0)
                             * (at.Date.IsWeekend ? Settings.WeekendMultiplier : 1.0);
            return perHour / 60.0;
        }

        /// <summary>
        /// 给排队的顾客结账：入账，返回这次结账耗费的游戏分钟（由调用方推进时钟）。
        /// 顾客不在队列中时返回 -1。
        /// </summary>
        public int Checkout(int customerId)
        {
            ShopCustomer c = _queue.Find(x => x.Id == customerId);
            if (c == null) return -1;

            bool cash = c.CarriesCash && (Accepted & PaymentMethods.Cash) != 0;
            _queue.Remove(c);
            _active.Remove(c);
            c.State = CustomerState.Paid;
            _ledger.Credit(_clock.Today, SalesCategory, c.Spend, "散件零售");
            TodaySales += c.Spend;
            CustomerPaid?.Invoke(c);
            return cash ? Settings.CheckoutMinutesCash : Settings.CheckoutMinutesQr;
        }

        public void Dispose()
        {
            _clock.TimeAdvanced -= OnTimeAdvanced;
            _clock.DayAdvanced -= OnDayAdvanced;
        }

        private void OnDayAdvanced(GameDate previous, GameDate today)
        {
            TodayCustomers = 0;
            TodayLost = 0;
            TodaySales = Money.Zero;
        }

        private void OnTimeAdvanced(GameTimestamp from, GameTimestamp to)
        {
            if (!_attended) return;
            for (long m = from.TotalMinutes; m < to.TotalMinutes; m++)
                StepMinute(new GameTimestamp(m));
        }

        private void StepMinute(GameTimestamp now)
        {
            if (_rng.NextDouble() < ArrivalRatePerMinute(now))
                Spawn(now);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ShopCustomer c = _active[i];
                if (c.State == CustomerState.Browsing && now >= c.ReadyAt)
                {
                    if (!CanPay(c))
                    {
                        Leave(c, LeaveReason.NoCash);
                        continue;
                    }
                    c.State = CustomerState.WaitingCheckout;
                    c.WaitingSince = now;
                    _queue.Add(c);
                    CustomerReadyToPay?.Invoke(c);
                }
                else if (c.State == CustomerState.WaitingCheckout &&
                         c.WaitingSince.MinutesUntil(now) >= Settings.PatienceMinutes)
                {
                    Leave(c, LeaveReason.Impatient);
                }
            }
        }

        private void Spawn(GameTimestamp now)
        {
            int browse = _rng.Next(Settings.MinBrowseMinutes, Settings.MaxBrowseMinutes + 1);
            long spend = Settings.MinSpendYuan + (long)(_rng.NextDouble() * (Settings.MaxSpendYuan - Settings.MinSpendYuan));
            bool cash = _rng.NextDouble() < Settings.CashCarryChance;
            var c = new ShopCustomer(_nextId++, now, now.AddMinutes(browse), Money.FromYuan(spend), cash);
            _active.Add(c);
            TodayCustomers++;
            CustomerArrived?.Invoke(c);
        }

        private bool CanPay(ShopCustomer c)
        {
            if ((Accepted & (PaymentMethods.QrCode | PaymentMethods.SelfCheckout)) != 0) return true;
            return c.CarriesCash && (Accepted & PaymentMethods.Cash) != 0;
        }

        private void Leave(ShopCustomer c, LeaveReason reason)
        {
            _active.Remove(c);
            _queue.Remove(c);
            c.State = CustomerState.Left;
            c.Reason = reason;
            TodayLost++;
            CustomerLeft?.Invoke(c);
        }

        private void CloseAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Leave(_active[i], LeaveReason.ShopClosed);
        }
    }
}
