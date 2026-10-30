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

        /// <summary>正在租的铺子；还没租时为 null。</summary>
        public ShopLease Lease { get; private set; }

        public bool HasShop => Lease != null;

        public IReadOnlyList<ShopListing> Listings => _listings;

        public bool InAgency => CurrentPlace != null && CurrentPlace.Kind == PlaceKind.Agency;

        public bool IsMyShop(Place p) => Lease != null && p == PlayerShopPlace;

        /// <summary>空着、门上贴着“旺铺招租”的铺面。</summary>
        public bool IsVacant(Place p) => p != null && _vacant.Contains(p.Id) && !IsMyShop(p);

        /// <summary>街上名牌显示的名字：我的店 / 旺铺招租 / 原来的店名。</summary>
        public string PlaceTitle(Place p) => p == null ? "" : IsMyShop(p) ? MyShopName : IsVacant(p) ? VacantTitle : p.Name;

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

        /// <summary>签这一间当场要付多少（已开着店的话加搬家费、减去退回的旧押金）。</summary>
        public Money LeaseCost(ShopListing l)
        {
            Money cost = l.Rent + RealEstate.Deposit(l.Rent) + AgentFeeFor(l.Rent);
            if (Lease != null) cost = cost + RealEstate.MoveFee - Lease.Deposit;
            return cost;
        }

        /// <summary>为什么现在签不了这一间；能签返回 null。</summary>
        public string LeaseBlocker(ShopListing l)
        {
            int today = Clock.Today.TotalDays;
            if (l.OpenDay > today) return $"现租户还没搬走，第 {l.OpenDay + 1} 天起才能签";
            if (Lease != null && l.PlaceId == Lease.PlaceId) return "这就是你现在的店";
            double rep = RealEstate.MinReputation(l.Size);
            if (PlayerShop.Reputation < rep) return $"房东要看名气：知名度要到 {rep:0}";
            if (Lease != null)
            {
                double cap = ShopSettings.Capacity(l.Size) + ShopUpgrades.ExtraCapacity(_upgrades);
                if (PlayerShop.Stock.UsedSpace > cap + 1e-9) return $"铺子太小，放不下现在的 {PlayerShop.Stock.UsedSpace:0.#} 格货";
            }
            if (Ledger.Balance < LeaseCost(l)) return "钱不够";
            return null;
        }

        /// <summary>在房产中介签下一间铺子。已有店就搬过去：旧铺退押金，摆在店里的家具收回“我的”。</summary>
        public bool SignLease(string placeId)
        {
            if (!InAgency) return Fail("要在房产中介签约。");
            ShopListing l = _listings.FirstOrDefault(x => x.PlaceId == placeId);
            if (l == null) return Fail("这间铺子已经不在挂牌了。");
            string blocker = LeaseBlocker(l);
            if (blocker != null) return Fail(blocker + "。");

            Place place = City.GetPlace(placeId);
            bool moving = Lease != null;
            Money deposit = RealEstate.Deposit(l.Rent);
            if (moving && Lease.Deposit.IsPositive) Ledger.Credit(Clock.Today, "押金", Lease.Deposit, "退还旧铺押金");
            PaySigning(l.Rent, place);
            if (moving) Ledger.ForceDebit(Clock.Today, "搬家", RealEstate.MoveFee, "搬店");
            MoveInto(place, l.Rent, deposit);
            // 第一次租铺子：房东送一个货架 + 一个收银台，哪怕把钱花光了也能摆出最低开业的摊子，不会卡死
            bool gifted = !moving && Flags.Add(LeaseGiftFlag);
            if (gifted)
            {
                AddFurniture(FurnitureCatalog.Shelf, 1);
                AddFurniture(FurnitureCatalog.ShopCounter, 1);
                // 再送 1 个 CPU + 1 张显卡进后仓：卖掉就是第一笔进货的本钱，钱花光了也不会没货可卖
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"), true, 1);
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu0"), true, 1);
            }
            Show(moving ? "搬店完成" : "签约成功",
                $"{place.Name}那间{SizeName(place.Size)}（{RealEstate.SizeArea(place.Size)}）租下来了，月租 {l.Rent}，押金 {deposit}。\n" +
                (moving ? "货、储物柜、展示机都搬过去了；原来店里摆的家具、货架、收银台都收回了“我的”，要在新店里重新摆。\n"
                        : "这是间毛坯铺子：房东送了你一个货架和一个收银台（在“我的”里），还有 1 个 CPU、1 张显卡放在后仓，回店里歇业装修时摆好就能开业；想卖得多，再去家具城买货架（每类配件一个）、展示桌、装机操作台。\n") +
                "至少摆好收银台和一个货架才能营业。以后每月 1 号交房租。");
            return true;
        }

        /// <summary>第一次租铺子送过货架、收银台和 CPU、显卡各 1 的记号。</summary>
        public const string LeaseGiftFlag = "lease_gift";

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
            }
            Lease = new ShopLease { PlaceId = place.Id, Rent = rent, Deposit = deposit, StartDay = Clock.Today.TotalDays };
            PlayerShopPlace = place;
            _vacant.Remove(place.Id);
            _listings.RemoveAll(x => x.PlaceId == place.Id);
            PlayerShop.Size = place.Size;
            PlayerShop.LocationFactor = LocationFactorOf(place);
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
            Say("（作弊）直接租下了街尾小店，货架收银台都已摆好，送了几件开局的货。");
            return true;
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

        private List<Place> FreeUnits() => RentableUnits().Where(p => !IsMyShop(p) && !Listed(p)).ToList();

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
            if (Lease == null && !_listings.Any(l => l.OpenDay <= today && l.Size == ShopSize.Small))
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
