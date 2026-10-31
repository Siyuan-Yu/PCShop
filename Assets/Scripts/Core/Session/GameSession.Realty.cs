using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 租铺子：商业街上的房产中介挂着在租的铺面（大小、位置、月租各不相同）。
    /// 有的铺子现租户还有几个月才到期（先挂出来，到期才能签）；挂牌过期没人签就被别人租走了。
    /// 新游戏没有店，要先去中介签一间；已经开着店再签就是搬过去（货、储物柜跟着走，旧铺押金退还）。
    /// </summary>
    public sealed partial class GameSession
    {
        public const string MyShopName = "我的电脑店";
        public const string VacantTitle = "旺铺招租";
        public const int MaxListings = 6;
        public const double DailyListingChance = 0.12;

        private readonly GameRandom _realtyRng;
        private readonly List<ShopListing> _listings = new List<ShopListing>();
        private readonly HashSet<string> _vacant = new HashSet<string>();
        private readonly Dictionary<string, double> _locationCache = new Dictionary<string, double>();

        /// <summary>当前这家店的租约；还没租铺子时为 null。</summary>
        public ShopLease Lease
        {
            get => _focus.Lease;
            private set => _focus.Lease = value;
        }

        public bool HasShop => Lease != null;

        /// <summary>提前签下、现租户还没搬走的铺子：租金照交，到 StartDay 那天才自动搬进去。没有则为 null。</summary>
        public ShopLease PendingLease { get; private set; }

        /// <summary>提前签的那间是开新店（否则是把当前店搬过去）。</summary>
        public bool PendingIsNewShop { get; private set; }

        public bool IsPendingShop(Place p) => PendingLease != null && p != null && p.Id == PendingLease.PlaceId;

        public IReadOnlyList<ShopListing> Listings => _listings;

        public bool InAgency => CurrentPlace != null && CurrentPlace.Kind == PlaceKind.Agency;

        public bool IsMyShop(Place p) => ShopAt(p) != null;

        /// <summary>空着、门上贴着“旺铺招租”的铺面。</summary>
        public bool IsVacant(Place p) => p != null && _vacant.Contains(p.Id) && !IsMyShop(p) && VentureAt(p.Id) == null && PropertyAt(p.Id) == null;

        /// <summary>街上名牌显示的名字：我的店 / 旺铺招租 / 原来的店名。</summary>
        public string PlaceTitle(Place p) => p == null ? "" : IsMyShop(p) ? ShopTitle(ShopAt(p)) : p == Home ? HomeTitle : p.Id == ParkPlaceId ? "电脑乐园" : VentureAt(p.Id) != null ? Ventures.Get(VentureAt(p.Id).Kind).Name : PropertyAt(p.Id) != null ? "我的出租物业" : IsVacant(p) ? VacantTitle : p.Name;

        public Place ListingPlace(ShopListing l) => City.GetPlace(l.PlaceId);

        /// <summary>位置系数（离地铁站最远 0.4，最近接近 1），决定客流和月租。</summary>
        public double LocationFactorOf(Place p)
        {
            if (!_locationCache.TryGetValue(p.Id, out double f))
            {
                f = ComputeLocationFactor(City, p);
                _locationCache[p.Id] = f;
            }
            return f;
        }

        /// <summary>到商业街地铁站的步行距离（米）。</summary>
        public double StationDistance(Place p)
        {
            Place station = City.FirstOfKind(PlaceKind.Station);
            if (station == null) return 0;
            PathResult path = Pathfinder.FindPath(City.Map, station.NodeId, p.NodeId);
            return path.Found ? path.Distance : 0;
        }

        /// <summary>
        /// 签这一间当场要付多少。newShop = 已经有店、再开一家（不搬）；否则已开着店就是搬店：
        /// 加搬家费、减去退回的旧押金（提前签的铺子搬家那天再结算这两项）。
        /// </summary>
        public Money LeaseCost(ShopListing l, bool newShop = false)
        {
            Money cost = l.Rent + RealEstate.Deposit(l.Rent) + AgentFeeFor(l.Rent);
            if (Lease != null && !newShop && !IsAdvance(l)) cost = cost + RealEstate.MoveFee - Lease.Deposit;
            return cost;
        }

        /// <summary>现租户还没搬走：签了也要等到 OpenDay 才能搬进去。</summary>
        public bool IsAdvance(ShopListing l) => l.OpenDay > Clock.Today.TotalDays;

        /// <summary>为什么现在签不了这一间；能签返回 null。newShop 见 <see cref="LeaseCost"/>。</summary>
        public string LeaseBlocker(ShopListing l, bool newShop = false)
        {
            if (VentureAt(l.PlaceId) != null || PropertyAt(l.PlaceId) != null) return "这间铺子已纳入你的经营资产";
            if (PendingLease != null) return $"你已经提前签了{City.GetPlace(PendingLease.PlaceId).Name}，第 {PendingLease.StartDay + 1} 天交房前不能再签";
            if (ShopAt(l.PlaceId) != null) return "这已经是你的店了";
            double rep = RealEstate.MinReputation(l.Size);
            if (PlayerShop.Reputation < rep) return $"房东要看名气：知名度要到 {rep:0}";
            if (Lease != null && !newShop)
            {
                double cap = ShopSettings.Capacity(l.Size) + ShopUpgrades.ExtraCapacity(_upgrades);
                if (PlayerShop.Stock.UsedSpace + IncomingShopSpace > cap + 1e-9) return $"铺子太小，放不下现在的 {PlayerShop.Stock.UsedSpace:0.#} 格货";
            }
            if (Ledger.Balance < LeaseCost(l, newShop)) return "钱不够";
            return null;
        }

        /// <summary>
        /// 在房产中介签下一间铺子。还没店就是开第一家；已有店时 newShop = 再开一家（几家店同时经营），
        /// 否则把当前这家店搬过去：旧铺退押金，摆在店里的家具收回“我的”。
        /// </summary>
        public bool SignLease(string placeId, bool newShop = false)
        {
            if (!InAgency) return Fail("要在房产中介签约。");
            ShopListing l = _listings.FirstOrDefault(x => x.PlaceId == placeId);
            if (l == null) return Fail("这间铺子已经不在挂牌了。");
            string blocker = LeaseBlocker(l, newShop);
            if (blocker != null) return Fail(blocker + "。");
            newShop = newShop && HasShop;

            Place place = City.GetPlace(placeId);
            Money deposit = RealEstate.Deposit(l.Rent);
            if (IsAdvance(l))
            {
                PaySigning(l.Rent, place);
                PendingLease = new ShopLease { PlaceId = place.Id, Rent = l.Rent, Deposit = deposit, StartDay = l.OpenDay, SignedDay = Clock.Today.TotalDays };
                PendingIsNewShop = newShop;
                _listings.Remove(l);
                int wait = l.OpenDay - Clock.Today.TotalDays;
                Show("提前签约",
                    $"{place.Name}那间{SizeName(place.Size)}（{RealEstate.SizeArea(place.Size)}）签下来了，月租 {l.Rent}，押金 {deposit}。\n" +
                    $"不过现租户的租约还没到期，要到第 {l.OpenDay + 1} 天（还有 {wait} 天）才搬走，到那天你才能搬进去。\n" +
                    "房租从今天就开始算，这段时间每月 1 号照样要交。" +
                    (newShop ? "\n交房那天就多了一家新店（毛坯，要自己摆收银台和货架），现在的店照常营业。"
                        : Lease != null ? "\n交房那天自动搬店：退旧铺押金、收搬家费，旧铺之前照常营业。" : "\n在那之前你还是没有店铺。"));
                return true;
            }
            if (newShop)
            {
                PaySigning(l.Rent, place);
                OpenNewShop(place, l.Rent, deposit);
                ShowNewShop(place, l.Rent, deposit, "开了新店");
                RefreshQuestProgress();
                return true;
            }
            bool moving = Lease != null;
            if (moving && Lease.Deposit.IsPositive) Ledger.Credit(Clock.Today, "押金", Lease.Deposit, "退还旧铺押金");
            PaySigning(l.Rent, place);
            if (moving) Ledger.ForceDebit(Clock.Today, "搬家", RealEstate.MoveFee, "搬店");
            MoveInto(place, l.Rent, deposit);
            ShowMovedIn(place, l.Rent, deposit, moving, "签约成功");
            RefreshQuestProgress();
            return true;
        }

        private void ShowMovedIn(Place place, Money rent, Money deposit, bool moving, string title)
        {
            Show(moving ? "搬店完成" : title,
                $"{place.Name}那间{SizeName(place.Size)}（{RealEstate.SizeArea(place.Size)}）租下来了，月租 {rent}，押金 {deposit}。\n" +
                (moving ? "货、储物柜、展示机都搬过去了；原来店里摆的家具、货架、收银台都收回了“我的”，要在新店里重新摆。\n"
                        : "这是间毛坯铺子，里面什么都没有：至少摆好收银台和一个货架才能营业。\n") +
                "以后每月 1 号交房租。");
        }

        private void ShowNewShop(Place place, Money rent, Money deposit, string title)
        {
            Show(title,
                $"{place.Name}那间{SizeName(place.Size)}（{RealEstate.SizeArea(place.Size)}）租下来了，是你的{ShopTitle(_focus)}，月租 {rent}，押金 {deposit}。\n" +
                "原来的店照常营业，货、员工、货架各管各的。新店是毛坯：至少摆好收银台和一个货架才能营业，人不在店里时要雇人看店才开门。\n" +
                "进哪家店，店里的面板、进货送货就对哪家店。以后每月 1 号每家店各交各的房租。");
        }

        /// <summary>提前签的铺子到了交房日：开新店，或者自动搬进去（搬店：退旧押金、收搬家费）。</summary>
        private void MoveIntoPending(int today)
        {
            if (PendingLease == null || today < PendingLease.StartDay) return;
            ShopLease p = PendingLease;
            PendingLease = null;
            Place place = City.GetPlace(p.PlaceId);
            if (PendingIsNewShop && HasShop)
            {
                PendingIsNewShop = false;
                OpenNewShop(place, p.Rent, p.Deposit);
                ShowNewShop(place, p.Rent, p.Deposit, "交房了");
                RefreshQuestProgress();
                return;
            }
            PendingIsNewShop = false;
            bool moving = Lease != null;
            if (moving)
            {
                if (Lease.Deposit.IsPositive) Ledger.Credit(Clock.Today, "押金", Lease.Deposit, "退还旧铺押金");
                Ledger.ForceDebit(Clock.Today, "搬家", RealEstate.MoveFee, "搬店");
            }
            MoveInto(place, p.Rent, p.Deposit);
            ShowMovedIn(place, p.Rent, p.Deposit, moving, "交房了");
            RefreshQuestProgress();
        }

        private void PaySigning(Money rent, Place place)
        {
            GameDate today = Clock.Today;
            Ledger.ForceDebit(today, "房租", rent, $"{place.Name} 首月租金");
            Ledger.ForceDebit(today, "押金", RealEstate.Deposit(rent), $"{place.Name} 押金");
            Money fee = AgentFeeFor(rent);
            double off = AgentDiscount;
            Ledger.ForceDebit(today, "中介费", fee, off > 0 ? $"安居房产中介（熟人价 {off * 100:0.#}% 折扣）" : "安居房产中介");
        }

        private void MoveInto(Place place, Money rent, Money deposit)
        {
            if (Lease != null && PlayerShopPlace != place)
            {
                _vacant.Add(PlayerShopPlace.Id);
                foreach (var f in _shopLayout) AddFurniture(f.Id, 1);
                _shopLayout.Clear();
                foreach (var kv in _focus.Locker.GiftLines.ToList()) _focus.Temp.AddGift(kv.Key, kv.Value);
                foreach (var kv in _focus.Locker.FurnitureLines.ToList()) _focus.Temp.AddFurniture(kv.Key, kv.Value);
                _focus.Temp.Food.RiceJin += _focus.Locker.Food.RiceJin;
                _focus.Temp.Food.Ingredient.AddRange(_focus.Locker.Food.Ingredient);
                _focus.Temp.Food.Packed.AddRange(_focus.Locker.Food.Packed);
                _focus.Locker.ClearGifts();
                _focus.Locker.ClearFurniture();
                _focus.Locker.Food.RiceJin = 0;
                _focus.Locker.Food.Ingredient.Clear();
                _focus.Locker.Food.Packed.Clear();
                _focus.BoxesGiven = false;
            }
            Lease = new ShopLease { PlaceId = place.Id, Rent = rent, Deposit = deposit, StartDay = Clock.Today.TotalDays };
            PlayerShopPlace = place;
            _vacant.Remove(place.Id);
            _listings.RemoveAll(x => x.PlaceId == place.Id);
            PlayerShop.Size = place.Size;
            PlayerShop.LocationFactor = LocationFactorOf(place);
            GiveStarterBoxes(_focus);
            ApplyUpgrades();
            ShopExpanded?.Invoke(place.Size);
        }

        private void AddStarterStock()
        {
            PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 3);
            PlayerShop.Stock.ForceAdd(PartCatalog.Get("ssd0"), true, 3);
            PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"), true, 1);
        }

        /// <summary>
        /// 作弊 / 验收用：不去中介，直接签下开局那间街尾小店（照常扣首月租金 + 押金 + 中介费），再送旧开局的那几件货。
        /// 新游戏点这个 = 回到以前“一开局就有店”的状态。
        /// </summary>
        public bool DebugRentStarterShop()
        {
            if (Lease != null) return false;
            Place start = City.PlayerShop();
            PaySigning(ShopRent, start);
            MoveInto(start, ShopRent, RealEstate.Deposit(ShopRent));
            FitStarterShop();
            if (PlayerShop.Stock.IsEmpty) AddStarterStock();
            SkipOpeningQuests();
            Say("（作弊）直接租下了街尾小店，货架收银台都已摆好，送了几件开局的货。");
            return true;
        }

        /// <summary>直接给了一间摆好的店（旧开局 / 作弊）：租铺、家具城两个任务算完成，不再发奖励。</summary>
        private void SkipOpeningQuests()
        {
            Flags.Add("tutorial:rent");
            Flags.Add("tutorial:mall");
        }

        /// <summary>测试 / 冒烟用：让中介挂出一间指定的铺子。</summary>
        public ShopListing DebugAddListing(string placeId, int openDay, int closeDay, Money? rent = null)
        {
            Place p = City.GetPlace(placeId);
            _listings.RemoveAll(x => x.PlaceId == placeId);
            var l = new ShopListing
            {
                PlaceId = placeId, Size = p.Size, OpenDay = openDay, CloseDay = closeDay,
                Rent = rent ?? RealEstate.RentFor(p.Size, LocationFactorOf(p), 1.0),
            };
            _listings.Add(l);
            if (openDay <= Clock.Today.TotalDays) _vacant.Add(placeId);
            return l;
        }

        // ---------- 挂牌 ----------

        private static bool IsFixedBusiness(string b) =>
            b == MallBusiness || b == PhoneBusiness || b == FarmBusiness || b == Gifts.TobaccoBusiness || b == "电脑";

        /// <summary>本来就是空铺（开局那间）：没人租也一直空着，不会“被别人租走”。</summary>
        private bool IsBareUnit(Place p) => p.IsPlayerShop || p.Business == RealEstate.VacantBusiness;

        private IEnumerable<Place> RentableUnits() =>
            City.Places.Where(p => p.Kind == PlaceKind.Shop && (IsBareUnit(p) || !IsFixedBusiness(p.Business)));

        private bool Listed(Place p) => _listings.Any(l => l.PlaceId == p.Id);

        private ShopListing AddListing(Place p, int openDay, int closeDay, Money? rent = null)
        {
            var l = new ShopListing
            {
                PlaceId = p.Id, Size = p.Size, OpenDay = openDay, CloseDay = closeDay,
                Rent = rent ?? RealEstate.RentFor(p.Size, LocationFactorOf(p), _realtyRng.Range(0.9, 1.15)),
            };
            _listings.Add(l);
            if (openDay <= Clock.Today.TotalDays) _vacant.Add(p.Id);
            return l;
        }

        private List<Place> FreeUnits() => RentableUnits().Where(p => !IsMyShop(p) && !IsPendingShop(p) && VentureAt(p.Id) == null && PropertyAt(p.Id) == null && !Listed(p)).ToList();

        private Place Pick(List<Place> pool)
        {
            if (pool.Count == 0) return null;
            Place p = pool[_realtyRng.Next(0, pool.Count)];
            pool.Remove(p);
            return p;
        }

        /// <summary>开局挂着的几间：街尾空铺（最便宜）、一间刚空出来的小铺、一间一个月后到期的中型铺、五金店那间（还有五个月到期）。</summary>
        private void SeedListings(int today)
        {
            _listings.Clear();
            Place start = City.PlayerShop();
            if (Lease == null) AddListing(start, today, today + 120, ShopRent);
            List<Place> pool = FreeUnits().Where(p => !IsBareUnit(p)).ToList();

            Place hardware = pool.FirstOrDefault(p => p.Business == "五金");
            if (hardware != null)
            {
                pool.Remove(hardware);
                AddListing(hardware, today + 150, today + 190);
            }
            Place small = Pick(pool.Where(p => p.Size == ShopSize.Small).ToList());
            if (small != null)
            {
                pool.Remove(small);
                AddListing(small, today, today + 45);
            }
            Place medium = Pick(pool.Where(p => p.Size == ShopSize.Medium).ToList());
            if (medium != null) AddListing(medium, today + _realtyRng.Next(20, 40), today + 80);
        }

        private void AddRandomListing(int today)
        {
            List<Place> pool = FreeUnits();
            if (pool.Count == 0) return;
            double r = _realtyRng.NextDouble();
            ShopSize want = r < 0.55 ? ShopSize.Small : r < 0.9 ? ShopSize.Medium : ShopSize.Large;
            List<Place> sized = pool.Where(p => p.Size == want).ToList();
            List<Place> vacantFirst = (sized.Count > 0 ? sized : pool).Where(IsVacant).ToList();
            Place p = Pick(vacantFirst.Count > 0 ? vacantFirst : sized.Count > 0 ? sized : pool);
            int open = IsVacant(p) ? today : today + _realtyRng.Next(20, 181);
            AddListing(p, open, open + _realtyRng.Next(20, 51));
        }

        /// <summary>每天：到期的老租户搬走（铺子空出来）、过期没人签的挂牌下架、偶尔挂出新铺子。</summary>
        private void OnRealtyDay(int today)
        {
            MoveIntoPending(today);
            foreach (ShopListing l in _listings.ToList())
            {
                Place p = ListingPlace(l);
                if (l.OpenDay <= today && !_vacant.Contains(l.PlaceId) && today <= l.CloseDay)
                {
                    _vacant.Add(l.PlaceId);
                    Say($"房产中介：{p.Name}的租户搬走了，{SizeName(l.Size)}空出来了（月租 {l.Rent}），可以去签约。");
                }
                if (today > l.CloseDay)
                {
                    _listings.Remove(l);
                    if (!IsBareUnit(p) && !IsMyShop(p)) _vacant.Remove(l.PlaceId);
                }
            }
            if (_listings.Count < MaxListings && _realtyRng.Chance(DailyListingChance)) AddRandomListing(today);
            if (Lease == null && PendingLease == null && !_listings.Any(l => l.OpenDay <= today && l.Size == ShopSize.Small))
            {
                Place start = City.PlayerShop();
                if (!Listed(start)) AddListing(start, today, today + 60, ShopRent);
            }
        }

        // ---------- 存档 ----------

        private void SaveRealty(SaveData d)
        {
            d.shopState = Lease != null ? 1 : 2;
            if (Lease != null)
            {
                d.leasePlace = Lease.PlaceId;
                d.leaseRentCents = Lease.Rent.Cents;
                d.leaseDepositCents = Lease.Deposit.Cents;
                d.leaseStartDay = Lease.StartDay;
            }
            if (PendingLease != null)
            {
                d.pendingPlace = PendingLease.PlaceId;
                d.pendingRentCents = PendingLease.Rent.Cents;
                d.pendingDepositCents = PendingLease.Deposit.Cents;
                d.pendingStartDay = PendingLease.StartDay;
                d.pendingSignedDay = PendingLease.SignedDay;
                d.pendingNewShop = PendingIsNewShop;
            }
            d.listings = _listings.Select(l => new SaveListing
            {
                place = l.PlaceId, size = (int)l.Size, rentCents = l.Rent.Cents, openDay = l.OpenDay, closeDay = l.CloseDay,
            }).ToArray();
            d.vacantPlaces = _vacant.ToArray();
            d.realtyRng = _realtyRng.State.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void RestoreRealty(SaveData d)
        {
            Place start = City.PlayerShop();
            if (d.shopState == 2)
            {
                Lease = null;
                PlayerShopPlace = start;
            }
            else
            {
                bool saved = d.shopState == 1 && City.HasPlace(d.leasePlace);
                PlayerShopPlace = saved ? City.GetPlace(d.leasePlace) : start;
                Lease = new ShopLease
                {
                    PlaceId = PlayerShopPlace.Id,
                    Rent = saved ? Money.FromCents(d.leaseRentCents) : RealEstate.BaseRent((ShopSize)d.shopSize),
                    Deposit = saved ? Money.FromCents(d.leaseDepositCents) : Money.Zero,
                    StartDay = saved ? d.leaseStartDay : 0,
                };
            }
            PlayerShop.LocationFactor = LocationFactorOf(PlayerShopPlace);
            PendingLease = !string.IsNullOrEmpty(d.pendingPlace) && City.HasPlace(d.pendingPlace)
                ? new ShopLease
                {
                    PlaceId = d.pendingPlace, Rent = Money.FromCents(d.pendingRentCents), Deposit = Money.FromCents(d.pendingDepositCents),
                    StartDay = d.pendingStartDay, SignedDay = d.pendingSignedDay,
                }
                : null;
            PendingIsNewShop = PendingLease != null && d.pendingNewShop;

            if (!string.IsNullOrEmpty(d.realtyRng) && ulong.TryParse(d.realtyRng, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong st))
                _realtyRng.State = st;
            _vacant.Clear();
            foreach (string id in d.vacantPlaces ?? Array.Empty<string>())
                if (City.HasPlace(id)) _vacant.Add(id);
            _listings.Clear();
            if (d.listings == null)
            {
                SeedListings(Clock.Today.TotalDays);
                return;
            }
            foreach (SaveListing l in d.listings)
            {
                if (!City.HasPlace(l.place)) continue;
                _listings.Add(new ShopListing
                {
                    PlaceId = l.place, Size = (ShopSize)l.size, Rent = Money.FromCents(l.rentCents), OpenDay = l.openDay, CloseDay = l.closeDay,
                });
            }
        }
    }
}
