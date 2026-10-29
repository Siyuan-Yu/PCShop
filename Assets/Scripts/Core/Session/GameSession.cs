using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 一局游戏的运行状态：世界时钟、账本、行情、城市、玩家（位置、载具、货、身体）、玩家的店、进货、员工。
    /// 界面层每帧调用 <see cref="Tick"/>；玩家的操作都走这里的方法，成功与否通过返回值和 <see cref="Notice"/> 反馈。
    /// </summary>
    public sealed partial class GameSession : IDisposable
    {
        public const int WakeUpMinute = 8 * 60;
        public const int BedtimeReminderMinute = 23 * 60;
        public const int FaintMinute = 4 * 60;
        public const int WholesalerOpenMinute = 9 * 60;
        public const int WholesalerCloseMinute = 18 * 60;
        public const int DealerOpenMinute = 9 * 60;
        public const int DealerCloseMinute = 19 * 60;
        public const int MonthGoalDay = 30;
        public static readonly Money MonthGoalNetWorth = Money.FromYuan(8000);
        public static readonly Money ShopRent = Money.FromYuan(1500);
        public static readonly Money HomeRent = Money.FromYuan(450);
        public static readonly Money MetroFare = Money.FromYuan(3);
        public const int MetroMinutes = 12;
        public const int BankruptDays = 3;

        public GameRandom Random { get; }
        public GameClock Clock { get; }
        public Ledger Ledger { get; }
        public HardwareMarket Market { get; }
        public CityLayout City { get; }
        public TimeController Time { get; }
        public ShopSimulation PlayerShop { get; }
        public Place PlayerShopPlace { get; }
        public Place Home { get; }
        public PoliceSystem Police { get; }
        public Online1788 Online { get; }
        public PlayerStats Player { get; } = new PlayerStats();

        /// <summary>随身（载具上）的货。容量随出行方式变化。</summary>
        public Stock Cargo { get; }

        /// <summary>玩家当前所在（或最后经过）的路径图节点。</summary>
        public string PlayerNodeId { get; private set; }

        /// <summary>玩家所在的地点；在街上时为 null。</summary>
        public Place CurrentPlace { get; private set; }

        public Trip Trip { get; private set; }

        public bool IsGameOver { get; private set; }
        public string GameOverReason { get; private set; }
        public bool MonthGoalEvaluated { get; private set; }
        public bool MonthGoalPassed { get; private set; }
        public int NegativeCashDays { get; private set; }

        /// <summary>小提示（右上角飘字）。</summary>
        public event Action<string> Notice;

        /// <summary>需要玩家看一眼的事件（标题，正文）。</summary>
        public event Action<string, string> Popup;

        /// <summary>出行到达目的地。</summary>
        public event Action<string> TripArrived;

        private bool _sleeping;
        private bool _pendingFaint;
        private bool _pendingHospital;

        public GameSession(CityLayout city, ulong seed, Money openingCash, GameTimestamp start, bool starterStock = true, bool smartphone = true)
        {
            City = city ?? throw new ArgumentNullException(nameof(city));
            PlayerShopPlace = city.PlayerShop() ?? throw new ArgumentException("布局里没有玩家的店。", nameof(city));
            Home = city.FirstOfKind(PlaceKind.Home) ?? throw new ArgumentException("布局里没有住所。", nameof(city));

            SetSmartphone(smartphone);
            Random = new GameRandom(seed);
            Clock = new GameClock(start.Date, start.MinuteOfDay);
            Ledger = new Ledger(openingCash);
            Time = new TimeController();
            Market = new HardwareMarket(Random);
            Online = new Online1788();
            Police = new PoliceSystem(city);
            Cargo = new Stock(Vehicles.Walk.CargoCapacity);

            PlayerShop = new ShopSimulation(PlayerShopPlace.Id, PlayerShopPlace.Size, Clock, Ledger, Market, Random)
            {
                LocationFactor = ComputeLocationFactor(city, PlayerShopPlace),
                EmployeeOnDuty = IsEmployeeOnDuty,
            };
            PlayerShop.DayClosed += r => Notice?.Invoke(DayReportText(r));
            PlayerShop.CustomerLeft += OnShopCustomerLeft;
            PlayerShop.OrderPlaced += o => Notice?.Invoke($"新装机订单：{PartCatalog.TierName(o.Tier)}整机 {o.Price}，两天内交付。");
            PlayerShop.OrderExpired += o => Popup?.Invoke("订单取消", $"{PartCatalog.TierName(o.Tier)}整机订单超时，顾客取消了订单，知名度下降。");

            _basePatience = PlayerShop.Settings.PatienceMinutes;

            if (starterStock)
            {
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 3);
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("ssd0"), true, 3);
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"), true, 1);
            }

            PlayerNodeId = Home.NodeId;
            CurrentPlace = Home;
            Police.RollDuty(Random);
            Clock.TimeAdvanced += OnTimeAdvanced;
            Clock.DayAdvanced += OnDayAdvanced;
        }

        public VehicleSpec CurrentVehicle => Vehicles.Get(Vehicle);

        /// <summary>玩家的画面坐标。</summary>
        public MapPoint PlayerPosition => Trip != null ? Trip.Position : OffNode ? _free.Value : City.ViewPosition(PlayerNodeId);

        public bool InPlayerShop => CurrentPlace == PlayerShopPlace;

        /// <summary>按真实时间推进世界：出行中按路程推进时钟，否则按倍速推进。返回推进的整分钟数。</summary>
        public int Tick(double realSeconds)
        {
            _manualMoving = false;
            if (IsGameOver || !Time.IsRunning || realSeconds <= 0) return 0;
            double scaled = Math.Min(realSeconds, TimeController.MaxTickSeconds) * Time.Speed;

            int minutes;
            if (SteerActive && Trip != null) BreakTripToEdge();
            if (Trip != null)
            {
                minutes = StepTrip(scaled);
            }
            else if (SteerActive && StepManual(scaled, out minutes))
            {
                _manualMoving = true;
            }
            else
            {
                minutes = Time.Tick(realSeconds);
                if (minutes > 0) AdvanceClock(minutes);
            }

            if (!IsGameOver) StepPolice(scaled);
            return minutes;
        }

        /// <summary>推进时钟并处理推进中触发的晕倒、住院。所有改变时间的操作都走这里。</summary>
        public void AdvanceClock(int minutes)
        {
            if (minutes <= 0) return;
            Clock.Advance(minutes);
            ProcessPending();
        }

        // ---------- 地点 ----------

        /// <summary>进入地点（玩家必须站在它的入口节点上、没在路上）。进入自己的店即开门营业并卸货。</summary>
        public bool Enter(Place place)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (Trip != null) return Fail("还在路上。");
            if (OffNode) return Fail("还没走到门口。");
            if (place.NodeId != PlayerNodeId)
                throw new InvalidOperationException($"玩家不在 {place.Name} 门口。");
            CurrentPlace = place;
            if (place == PlayerShopPlace)
            {
                PlayerShop.PlayerPresent = true;
                UnloadCargo();
                UnloadFurniture();
            }
            else if (place == Home)
            {
                UnloadFoodAtHome();
            }
            else if (place.Kind == PlaceKind.Wholesaler)
            {
                MeetStallOwner(place);
            }
            return true;
        }

        /// <summary>离开当前地点回到街上。离开自己的店且没有员工值守时关门。</summary>
        public void Leave()
        {
            if (CurrentPlace == null) return;
            if (CurrentPlace == PlayerShopPlace) PlayerShop.PlayerPresent = false;
            CurrentPlace = null;
        }

        /// <summary>玩家直接站到某个节点（读档、测试用）。</summary>
        public void ArriveAtNode(string nodeId)
        {
            City.Map.GetNode(nodeId);
            PlayerNodeId = nodeId;
            ClearEdge();
        }

        public bool IsWholesalerOpen => IsOpenWeekday(WholesalerOpenMinute, WholesalerCloseMinute);

        public bool IsDealerOpen
        {
            get
            {
                int m = Clock.MinuteOfDay;
                return m >= DealerOpenMinute && m < DealerCloseMinute;
            }
        }

        private bool IsOpenWeekday(int open, int close)
        {
            GameTimestamp now = Clock.Now;
            return !now.Date.IsWeekend && now.MinuteOfDay >= open && now.MinuteOfDay < close;
        }

        // ---------- 身家 / 目标 ----------

        /// <summary>库存按市场价 9 折计。</summary>
        public Money InventoryValue()
        {
            double sum = 0;
            foreach (StockLine l in PlayerShop.Stock.Lines()) sum += Market.PriceOf(l.Part).Yuan * l.Count;
            foreach (StockLine l in Cargo.Lines()) sum += Market.PriceOf(l.Part).Yuan * l.Count;
            return Money.FromYuan((long)Math.Round(sum * 0.9));
        }

        public Money NetWorth() => Ledger.Balance + InventoryValue();

        public void Dispose()
        {
            Clock.TimeAdvanced -= OnTimeAdvanced;
            Clock.DayAdvanced -= OnDayAdvanced;
            PlayerShop.Dispose();
        }

        // ---------- 时间事件 ----------

        private void OnTimeAdvanced(GameTimestamp from, GameTimestamp to)
        {
            int minutes = (int)from.MinutesUntil(to);
            UpdateBody(minutes);
            UpdateCooking(to);
            CheckCalendarReminders(from, to);
            if (Crosses(from, to, RestockCheckMinute)) RunAutoRestock();
            CheckHomeVisits(from, to);

            if (CurrentPlace != Home && Crosses(from, to, BedtimeReminderMinute))
                Notice?.Invoke("23:00 了，该回家睡觉了。凌晨 4 点还不睡会晕倒。");

            if (!_sleeping)
            {
                if (Crosses(from, to, 7 * 60 + 30)) EatMeal("早饭");
                if (Crosses(from, to, 12 * 60)) EatMeal("午饭");
                if (Crosses(from, to, 18 * 60 + 30)) EatMeal("晚饭");
                if (Crosses(from, to, FaintMinute)) _pendingFaint = true;
            }

            if (Crosses(from, to, CandidateMinute)) OnCandidateHour(to.Date);
            if (Crosses(from, to, EventMinute)) RollDailyEvent(to.Date);
            CheckAppointments(to);
            DeliverOnlineOrders(to);

            if (!_sleeping && Player.Health < 15) _pendingHospital = true;
        }

        /// <summary>昨日营业小结：成交多少、流失多少、为什么，再给一句建议。</summary>
        public static string DayReportText(DayReport r)
        {
            string s = $"昨日营业：进店 {r.Customers} 人，成交 {r.Paid} 单，营业额 {r.Revenue}";
            if (r.Orders > 0) s += $"，装机单 {r.Orders} 个";
            if (r.Lost > 0)
            {
                var why = new System.Collections.Generic.List<string>();
                if (r.LostTooExpensive > 0) why.Add($"嫌贵 {r.LostTooExpensive}");
                if (r.LostImpatient > 0) why.Add($"等太久 {r.LostImpatient}");
                if (r.LostNoStock > 0) why.Add($"没货 {r.LostNoStock}");
                if (r.LostNoCash > 0) why.Add($"没带现金 {r.LostNoCash}");
                if (r.LostClosed > 0) why.Add($"关门 {r.LostClosed}");
                s += $"，走了 {r.Lost} 人（{string.Join("、", why)}）";
            }
            s += "。";
            if (r.LostNoStock >= 1) s += "有人因为缺货空手走了，该进货了。";
            else if (r.LostImpatient >= 2) s += "排队的人等烦了，记得及时结账或者雇个人。";
            else if (r.LostTooExpensive >= 2 && r.LostTooExpensive >= r.Paid) s += "嫌贵的人不少，可以降一点加价。";
            return s;
        }

        private void OnDayAdvanced(GameDate previous, GameDate today)
        {
            Market.OnNewDay(today);
            if (Market.News.Count > 0)
            {
                NewsItem last = Market.News[Market.News.Count - 1];
                if (last.Date == today)
                {
                    if (last.Headline.StartsWith("【头条】")) Popup?.Invoke("今日新闻", last.Headline);
                    else Notice?.Invoke("新闻：" + last.Headline);
                }
            }
            Police.RollDuty(Random);
            ResolveTips(today);
            OnHomeDayAdvanced(today);

            PayWages(previous);
            ProcessScheduledCredits(today);
            DecayRelations();
            if (today.IsMonthStart)
            {
                if (HomeUtilities.IsPositive) Ledger.ForceDebit(today, "水电", HomeUtilities, "住处水电");
                Ledger.ForceDebit(today, "房租", CurrentShopRent, "店铺月租");
                Ledger.ForceDebit(today, "房租", HomeRent, "城中村月租");
                Popup?.Invoke("交房租", $"店铺 {CurrentShopRent} + 出租屋 {HomeRent} 已扣款。");
            }

            if (today.TotalDays == 4 && Online.Discover("s_zhou"))
            {
                Meet(Npcs.Jie, 20);
                Meet(Npcs.Zhou, 10);
                Popup?.Invoke("同行的建议", "隔壁手机店的老板说：\"别在 1788 上看评分，评分都是刷的。去搜'老周电子'，我们都在他那拿货。\"\n（1788 上出现了新店铺）");
            }

            if (!MonthGoalEvaluated && today.TotalDays >= MonthGoalDay) EvaluateMonthGoal();
            EvaluateCampaign(today);

            if (Ledger.Balance.IsNegative)
            {
                NegativeCashDays++;
                if (NegativeCashDays >= 2 && TryFriendBailout() && !Ledger.Balance.IsNegative)
                {
                    NegativeCashDays = 0;
                    return;
                }
                if (NegativeCashDays >= BankruptDays)
                {
                    SetGameOver($"连续 {BankruptDays} 天资金为负，店铺倒闭了。");
                    return;
                }
                Popup?.Invoke("资金告急", $"账上是负数（{Ledger.Balance}）。连续 {BankruptDays} 天为负就会倒闭，已经 {NegativeCashDays} 天。");
            }
            else
            {
                NegativeCashDays = 0;
            }
        }

        private void EvaluateMonthGoal()
        {
            MonthGoalEvaluated = true;
            Money worth = NetWorth();
            bool hired = Employee != null || EverHired;
            MonthGoalPassed = worth >= MonthGoalNetWorth && hired;
            string body = $"身家（现金 + 库存九折）：{worth}（目标 {MonthGoalNetWorth}）\n" +
                          $"雇到员工：{(hired ? "是" : "否")}\n\n" +
                          (MonthGoalPassed ? "第一个月站稳了脚跟！可以继续经营。" : "第一个月没达标。可以继续经营，但房东和同行都在看着你。");
            Popup?.Invoke(MonthGoalPassed ? "第一个月：达标" : "第一个月：未达标", body);
        }

        private void SetGameOver(string reason)
        {
            IsGameOver = true;
            GameOverReason = reason;
            Trip = null;
            Popup?.Invoke("游戏结束", reason);
        }

        private void ProcessPending()
        {
            if (_pendingHospital)
            {
                _pendingHospital = false;
                _pendingFaint = false;
                Hospitalize();
            }
            else if (_pendingFaint)
            {
                _pendingFaint = false;
                Faint();
            }
        }

        private static bool Crosses(GameTimestamp from, GameTimestamp to, int minuteOfDay)
        {
            long mark = (long)from.Date.TotalDays * GameTimestamp.MinutesPerDay + minuteOfDay;
            return from.TotalMinutes <= mark && mark < to.TotalMinutes;
        }

        private bool Fail(string message)
        {
            Notice?.Invoke(message);
            return false;
        }

        private void Say(string message) => Notice?.Invoke(message);

        private void Show(string title, string body) => Popup?.Invoke(title, body);

        /// <summary>按到地铁站的路程算位置系数：所有店里离地铁站最远的为 0.4，最近的接近 1。</summary>
        private static double ComputeLocationFactor(CityLayout city, Place shop)
        {
            Place station = city.FirstOfKind(PlaceKind.Station);
            if (station == null) return 1.0;

            double max = 0;
            double mine = 0;
            foreach (Place p in city.Places)
            {
                if (p.Kind != PlaceKind.Shop) continue;
                PathResult path = Pathfinder.FindPath(city.Map, station.NodeId, p.NodeId);
                if (!path.Found) continue;
                if (path.Distance > max) max = path.Distance;
                if (p == shop) mine = path.Distance;
            }
            return ShopSettings.LocationFactor(mine, max);
        }
    }
}
