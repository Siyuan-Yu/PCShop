using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly HashSet<string> _cityAssetEffects = new HashSet<string>();
        private readonly GameRandom _cityEquityRandom = new GameRandom(0x75BA927CD315UL);
        private int _cityEquityLastDay;
        private long[] _cityFundHistory = { 10000 };
        private readonly List<CityEquityState> _cityEquities = CityInvestments.All.Select(d => new CityEquityState {
            Id = d.Id, PriceCents = d.BaseCents, History = new[] { (long)d.BaseCents } }).ToList();
        public IReadOnlyList<CityEquityState> CityEquities => _cityEquities;
        public Money CityEquityValue => Money.FromCents(_cityEquities.Sum(e => e.PriceCents * e.Units));
        public Money CityEquityCost => Money.FromCents(_cityEquities.Sum(e => e.CostCents));
        public Money CityEquityRealized => Money.FromCents(_cityEquities.Sum(e => e.RealizedCents));
        public long CityFundCostCents { get; private set; }
        public long CityFundRealizedCents { get; private set; }
        public bool CityFundCostEstimated { get; private set; }
        public string CityInvestmentNews
        {
            get
            {
                int day = Clock.Today.TotalDays, dom = day % 30;
                if ((day + 1) % 47 == 0) return "产业消息：城芯算力明日公布渠道供给调整，算力设备板块可能出现较大波动。";
                if (day > 0 && day % 47 == 0) return "产业消息：渠道供给调整公布，城芯算力承压；这只影响其实际持仓，不扣除未投资现金。";
                if (dom >= 20 && dom <= 23) return "文创消息：本月漫展预期活跃，星桥文创受到关注；展后存在预期回落风险。";
                if (dom == 25) return "文创消息：展期消费预期消退，星桥文创调整。";
                return "产业消息：城芯算力关联硬件景气，晴川生活侧重社区消费，星桥文创关联原创商品与展会。行情没有保底回报。";
            }
        }

        public IEnumerable<Place> CityCommercialProperties => City.Places.Where(p => p.IsTradable ||
            (p.Kind == PlaceKind.Shop && !(p.Business ?? "").StartsWith("venue_", StringComparison.Ordinal)));
        public bool IsPlayerPropertyOccupied(string id)
        {
            var p = PropertyAt(id);
            return p != null && p.UseMode != "npc" && p.UseMode != "managed" && ShopAt(id) == null && VentureAt(id) == null;
        }
        public string CityPropertyTitle(Place p)
        {
            var owned = PropertyAt(p.Id);
            return owned == null || owned.UseMode == "npc" ? p.Name : owned.UseMode == "vacant" ? p.Name + "（自有空置）" : p.Name + "（自有出租）";
        }
        public string CityPropertyAddress(Place p) => City.StreetName(p.StreetId) + " · " + p.Name;
        public Money CityPropertyPrice(string id)
        {
            if (!City.HasPlace(id)) return Money.Zero;
            var existing = PropertyAt(id); if (existing != null) return Money.FromCents(existing.PriceCents);
            var own = ShopAt(id); var venture = VentureAt(id); var place = City.GetPlace(id);
            Money rent = own?.Lease != null ? own.Lease.Rent : venture != null ? Money.FromCents(venture.RentCents) : _listings.Find(l => l.PlaceId == id)?.Rent ?? Money.Zero;
            if (!rent.IsPositive) rent = RealEstate.RentFor(place.Size, LocationFactorOf(place), 1);
            return rent * 120;
        }
        public CityPropertyPurchase PreviewCityPropertyPurchase(string id, bool mortgage)
        {
            if (!City.HasPlace(id)) return null;
            var place = City.GetPlace(id); var shop = ShopAt(id); var venture = VentureAt(id);
            var price = CityPropertyPrice(id);
            var quote = new CityPropertyPurchase { PlaceId = id, Address = CityPropertyAddress(place), Price = price,
                Fee = price.Scale(.02), IsSelfUse = shop != null || venture != null,
                Tenant = shop != null ? ShopTitle(shop) : venture != null ? Ventures.Get(venture.Kind).Name : IsVacant(place) || IsBareUnit(place) ? "空置" : place.Name + "原经营者" };
            quote.Mortgage = mortgage ? price.Scale(.7) : Money.Zero;
            quote.DownPayment = price - quote.Mortgage;
            if (shop?.Lease != null)
            {
                quote.DepositCredit = shop.Lease.Deposit;
                int firstPaid = shop.Lease.SignedDay > 0 ? shop.Lease.SignedDay : shop.Lease.StartDay;
                int remaining = Math.Max(0, 30 - Math.Max(0, Clock.Today.TotalDays - firstPaid));
                quote.PrepaidCredit = shop.Lease.Rent.Scale(remaining / 30.0);
            }
            else if (venture != null)
            {
                quote.DepositCredit = Money.FromCents(venture.DepositCents);
                quote.PrepaidCredit = Money.FromCents(venture.RentCents).Scale(Math.Max(0, 30 - (Clock.Today.TotalDays - venture.OpenDay)) / 30.0);
            }
            quote.TotalDue = Money.Max(Money.Zero, quote.DownPayment + quote.Fee - quote.DepositCredit - quote.PrepaidCredit);
            quote.CashAfter = Ledger.Balance - quote.TotalDue;
            return quote;
        }
        public string CityPropertyPurchaseBlocker(string id, bool mortgage)
        {
            if (IsGameOver) return "当前不能交易";
            if (!CityCommercialProperties.Any(p => p.Id == id)) return "该地点不是可交易的商业物业";
            if (InAgency && !AgencyServes(CurrentPlace, City.GetPlace(id))) return "请到房源所属片区的中介办理";
            if (PropertyAt(id) != null) return "已经持有这处物业";
            if (IsPendingShop(City.GetPlace(id))) return "该物业已有待交房租约，交房后再办理租转买";
            var quote = PreviewCityPropertyPurchase(id, mortgage);
            return quote == null || !quote.Price.IsPositive ? "物业估值不可用" : quote.TotalDue > Ledger.Balance ? "现金还差 " + (quote.TotalDue - Ledger.Balance) : null;
        }
        private bool PurchaseCityProperty(string id, bool mortgage)
        {
            if (!InAgency) return Fail("请到中介确认产权交易。");
            string why = CityPropertyPurchaseBlocker(id, mortgage); if (why != null) return Fail(why + "。");
            var q = PreviewCityPropertyPurchase(id, mortgage); var place = City.GetPlace(id); var own = ShopAt(id); var venture = VentureAt(id);
            string eventId = "property:purchase:" + id + ":" + Clock.Now.TotalMinutes + ":" + _cityAssetEffects.Count;
            if (q.TotalDue.IsPositive && !Ledger.TryDebit(Clock.Today, "购置物业", q.TotalDue, q.Address + "（含2%交易费，抵扣已记录的押金及未用首月租金）", LedgerAttribution.ForAsset(id, eventId))) return Fail("交易时现金不足，产权未改变。");
            var property = new OwnedProperty { PlaceId = id, PriceCents = q.Price.Cents, MortgageCents = q.Mortgage.Cents,
                MonthlyPrincipalCents = (q.Mortgage.Cents + 35) / 36, BoughtDay = Clock.Today.TotalDays, CitySchema = 1,
                UseMode = q.IsSelfUse ? "self" : q.Tenant == "空置" ? "vacant" : "npc", TenantName = q.IsSelfUse ? "玩家自营" : q.Tenant,
                MonthlyRentCents = q.Price.Cents / 120, LeaseEndDay = Clock.Today.TotalDays + 360, LastSettledMonth = Clock.Today.TotalDays / 30 };
            _properties.Add(property); _cityAssetEffects.Add(eventId); _listings.RemoveAll(l => l.PlaceId == id); _vacant.Remove(id);
            if (own != null) { own.OwnsProperty = true; own.Lease.Deposit = Money.Zero; own.Lease.Rent = Money.Zero; }
            if (venture != null) { venture.DepositCents = 0; venture.RentCents = 0; }
            Say(q.IsSelfUse ? "租转买完成：店铺、库存、员工、订单、装修及采购计划全部保留，旧租金已停止。" : property.UseMode == "npc" ? "带租约购入：原商户继续经营，商品与员工仍归租户；从下一账期起收租。" : "空置物业已购入，可自营或登记出租。");
            return true;
        }
        public string CityPropertySaleBlocker(string id)
        {
            if (PropertyAt(id) == null) return "尚未拥有该物业";
            if (InAgency && !AgencyServes(CurrentPlace, City.GetPlace(id))) return "请到物业所属片区的中介办理";
            if (ShopAt(id) != null || VentureAt(id) != null) return "请先结束自营并清理经营资产；本轮不办理售后回租";
            if (id == ParkPlaceId && _ventures.Any(IsParkVenture)) return "先清退园区全部铺位";
            if (Ledger.Balance + PropertySaleNet(PropertyAt(id)) < Money.Zero) return "售价不足偿还房贷，需要先补足现金";
            return null;
        }
        private bool SellCityProperty(string id)
        {
            if (!InAgency || IsGameOver) return Fail("请到中介办理出售。");
            string why = CityPropertySaleBlocker(id); if (why != null) return Fail(why + "。");
            var p = PropertyAt(id); Money net = PropertySaleNet(p);
            string key = "property:sale:" + id + ":" + p.BoughtDay + ":" + _cityAssetEffects.Count;
            if (net.IsPositive) Ledger.Credit(Clock.Today, "物业出售", net, "按估值九折，已扣交易成本并清偿剩余房贷", LedgerAttribution.ForAsset(id, key));
            else if (net.IsNegative && !Ledger.TryDebit(Clock.Today, "物业出售", -net, "补足房贷差额", LedgerAttribution.ForAsset(id, key))) return false;
            _cityAssetEffects.Add(key); _properties.Remove(p);
            if (id == ParkPlaceId) { ParkPlaceId = null; ParkReadyDay = 0; }
            if (p.UseMode == "vacant" || p.UseMode == "managed") { _vacant.Add(id); if (!_listings.Any(l => l.PlaceId == id)) AddListing(City.GetPlace(id), Clock.Today.TotalDays, Clock.Today.TotalDays + 45); }
            Say("物业已出售；原租户继续经营，人物、商品与关系不会删除。"); return true;
        }
        public bool SetCityPropertyRental(string id, bool rentOut)
        {
            var p = PropertyAt(id); if (p == null || IsGameOver) return false;
            if (!InAgency) return Fail("请到中介办理出租登记。");
            if (!City.HasPlace(id) || !AgencyServes(CurrentPlace, City.GetPlace(id))) return Fail("请到房源所属片区的中介办理。");
            if (ShopAt(id) != null || VentureAt(id) != null) return Fail("自营物业不能同时向自己收租。");
            if (p.UseMode == "npc") return Fail("现有租户租约受保护，本轮不提供强制驱逐。");
            if (!rentOut && p.UseMode == "managed" && Clock.Today.TotalDays < p.LeaseEndDay) return Fail("托管租约尚未到期，不能直接腾空。");
            p.UseMode = rentOut ? "managed" : "vacant"; p.TenantName = rentOut ? "中介托管商户" : "";
            if (rentOut) p.LeaseEndDay = Clock.Today.TotalDays + 30;
            Say(rentOut ? "出租已登记：下一月账期收取租金，管理及空置折损为15%。" : "租约结束，物业恢复空置。"); return true;
        }
        public bool OpenCityOwnedShop(string id, string retailKind = "computer")
        {
            var p = PropertyAt(id); if (p == null || IsGameOver || !InAgency || !AgencyServes(CurrentPlace, City.GetPlace(id))) return Fail("在中介选择自有空置物业开店。");
            if (p.UseMode != "vacant" || ShopAt(id) != null || VentureAt(id) != null) return Fail("该物业仍被使用，不能清走原租户。");
            var place = City.GetPlace(id); var kind = IsDedicatedVenue(place)?"computer":RetailShopTypes.Valid(retailKind);
            if (kind != "computer" && Clock.Today.TotalDays < 14) return Fail("第15天起可选其他自营业态。");
            if (!HasShop) MoveInto(place, Money.Zero, Money.Zero); else OpenNewShop(place, Money.Zero, Money.Zero);
            _focus.OwnsProperty = true; _focus.RetailKind = kind; _focus.Sim.RetailKind = kind; p.UseMode = "self"; p.TenantName = "玩家自营";
            if(IsDedicatedVenue(place)){ShowVenueHandover(place);return true;}
            Say("自有物业开店完成，不收自己的房租。按正常流程购买设施、装修并在次日营业。"); return true;
        }
        private void SettleCityProperties(GameDate today)
        {
            if (!today.IsMonthStart) return;
            int month = today.TotalDays / 30;
            foreach (var p in _properties)
            {
                if (p.LastSettledMonth >= month || today.TotalDays <= p.BoughtDay) continue;
                p.LastSettledMonth = month;
                string basis = "property:" + p.PlaceId + ":month:" + month;
                bool self = ShopAt(p.PlaceId) != null || VentureAt(p.PlaceId) != null || p.PlaceId == ParkPlaceId;
                if (!self && (p.UseMode == "npc" || p.UseMode == "managed"))
                {
                    Money rent = Money.FromCents(p.MonthlyRentCents).Scale(p.UseMode == "managed" ? .85 : 1);
                    if (rent.IsPositive) Ledger.Credit(today, "物业租金", rent, p.TenantName + "本月租金", LedgerAttribution.ForAsset(p.PlaceId, basis + ":rent"));
                }
                Money maintenance = Money.FromCents(p.PriceCents).Scale(.001);
                if (maintenance.IsPositive) Ledger.ForceDebit(today, "物业维护", maintenance, "商用物业月维护", LedgerAttribution.ForAsset(p.PlaceId, basis + ":maintenance"));
                if (p.MortgageCents > 0)
                {
                    long principal = Math.Min(p.MortgageCents, p.MonthlyPrincipalCents);
                    Money interest = Money.FromCents(p.MortgageCents).Scale(.004);
                    if (principal > 0) Ledger.ForceDebit(today, "偿还房贷", Money.FromCents(principal), "物业月供本金", new LedgerAttribution(LedgerBusinessKind.Financing, propertyId: p.PlaceId, sourceEventId: basis + ":principal"));
                    if (interest.IsPositive) Ledger.ForceDebit(today, "房贷利息", interest, "物业月供利息", new LedgerAttribution(LedgerBusinessKind.Financing, propertyId: p.PlaceId, sourceEventId: basis + ":interest"));
                    p.MortgageCents -= principal;
                }
            }
        }

        public Money CityInvestmentPrice(string id) => id == "hardware" ? FundUnitPrice : Money.FromCents(_cityEquities.Find(e => e.Id == id)?.PriceCents ?? 0);
        public long CityInvestmentUnits(string id) => id == "hardware" ? FundUnits : _cityEquities.Find(e => e.Id == id)?.Units ?? 0;
        public Money CityInvestmentCost(string id) => Money.FromCents(id == "hardware" ? CityFundCostCents : _cityEquities.Find(e => e.Id == id)?.CostCents ?? 0);
        public Money CityInvestmentRealized(string id) => Money.FromCents(id == "hardware" ? CityFundRealizedCents : _cityEquities.Find(e => e.Id == id)?.RealizedCents ?? 0);
        public long[] CityInvestmentHistory(string id) => id == "hardware" ? _cityFundHistory : _cityEquities.Find(e => e.Id == id)?.History;
        public Money CityInvestmentTradeTotal(string id, long units, bool buy)
        {
            if (units <= 0 || units > 1000000) return Money.Zero;
            Money gross = CityInvestmentPrice(id) * units; if (!gross.IsPositive) return Money.Zero;
            Money fee = CityInvestments.Fee(gross, id == "hardware"); return buy ? gross + fee : gross - fee;
        }
        public bool CityInvestmentUsesReserve(string id, long units) => Ledger.Balance - CityInvestmentTradeTotal(id, units, true) < GetCityCashReserve().Total;
        public bool TradeCityInvestment(string id, long units, bool buy, bool acknowledgeReserve = false)
        {
            var e = _cityEquities.Find(x => x.Id == id);
            if ((id != "hardware" && e == null) || IsGameOver || !HasSmartphone || units <= 0 || units > 1000000) return Fail("请使用智能手机投资入口，按整数份额交易。");
            Money amount = CityInvestmentTradeTotal(id, units, buy); if (!amount.IsPositive) return Fail("交易金额不足以支付手续费。");
            if (buy && !acknowledgeReserve && CityInvestmentUsesReserve(id, units)) return Fail("这笔买入将动用未来必付款及资金保护额，请在投资确认页阅读预警后继续。");
            long held = CityInvestmentUnits(id); if (!buy && held < units) return Fail("不能卖出超过实际持仓的份额。");
            string key = "investment:" + id + ":" + Clock.Now.TotalMinutes + ":" + _cityAssetEffects.Count;
            var attribution = LedgerAttribution.ForAsset(id, key);
            if (buy)
            {
                if (!Ledger.TryDebit(Clock.Today, "证券投资", amount, id + "买入（含交易费）", attribution)) return Fail("现金不足，未产生持仓或借款。");
                if (id == "hardware") { FundUnits += units; CityFundCostCents += amount.Cents; }
                else { e.Units += units; e.CostCents += amount.Cents; }
            }
            else
            {
                long cost = id == "hardware" ? CityFundCostCents : e.CostCents;
                long soldCost = units == held ? cost : (long)(cost * (decimal)units / held);
                Ledger.Credit(Clock.Today, "证券赎回", amount, id + "卖出（已扣交易费）", attribution);
                if (id == "hardware") { FundUnits -= units; CityFundCostCents -= soldCost; CityFundRealizedCents += amount.Cents - soldCost; }
                else { e.Units -= units; e.CostCents -= soldCost; e.RealizedCents += amount.Cents - soldCost; }
            }
            _cityAssetEffects.Add(key); Say("交易完成。证券现金流单列，浮动盈亏不会改变未投资的现金或人物关系。"); return true;
        }
        public void TickCityAssets()
        {
            int today = Clock.Today.TotalDays;
            while (_cityEquityLastDay < today)
            {
                _cityEquityLastDay++;
                int dom = _cityEquityLastDay % 30;
                foreach (var e in _cityEquities)
                {
                    var cpu = PartCatalog.Get("cpu1");
                    double industry = Math.Max(-.5, Math.Min(.5, Market.PriceOf(cpu).Cents / (double)cpu.BasePrice.Cents - 1));
                    double news = e.Id == "city_chip" ? Math.Sin(_cityEquityLastDay / 11.0) * .006 + industry * .012
                        : e.Id == "city_art" ? (dom >= 20 && dom <= 23 ? .012 : dom == 25 ? -.025 : 0) : Math.Sin(_cityEquityLastDay / 17.0) * .004;
                    double shock = _cityEquityRandom.Range(-.035, .035) + news;
                    if (_cityEquityLastDay % 47 == 0 && e.Id == "city_chip") shock -= .09;
                    e.PriceCents = Math.Max(100, Math.Min(1000000, (long)Math.Round(e.PriceCents * (1 + shock))));
                    e.History = (e.History ?? Array.Empty<long>()).Concat(new[] { e.PriceCents }).TakeLastCompat(90);
                }
                _cityFundHistory = _cityFundHistory.Concat(new[] { FundUnitPrice.Cents }).TakeLastCompat(90);
            }
        }
        public void SaveCityAssets(SaveData d)
        {
            d.cityAssetsSchema = 1; d.cityOwnedShopPlaces = _shops.Where(s => s.OwnsProperty).Select(s => s.Place.Id).ToArray();
            d.cityEquities = _cityEquities.Select(e => e.Copy()).ToArray(); d.cityEquityRandom = _cityEquityRandom.State.ToString(CultureInfo.InvariantCulture);
            d.cityEquityLastDay = _cityEquityLastDay; d.cityFundCostCents = CityFundCostCents; d.cityFundRealizedCents = CityFundRealizedCents;
            d.cityFundCostEstimated = CityFundCostEstimated; d.cityAssetEffects = _cityAssetEffects.ToArray();
            d.cityFundHistory = (long[])_cityFundHistory.Clone();
        }
        public void LoadCityAssets(SaveData d)
        {
            var uniqueProperties = _properties.GroupBy(p => p.PlaceId).Select(g => g.First()).ToArray();
            if (uniqueProperties.Length != _properties.Count) { _properties.Clear(); _properties.AddRange(uniqueProperties); }
            foreach (var p in _properties)
            {
                p.PriceCents = Math.Max(1, p.PriceCents); p.MortgageCents = Math.Max(0, p.MortgageCents);
                if (p.MortgageCents > 0 && p.MonthlyPrincipalCents <= 0) p.MonthlyPrincipalCents = (p.MortgageCents + 35) / 36;
                if (p.CitySchema == 0)
                {
                    p.CitySchema = 1; p.UseMode = ShopAt(p.PlaceId) != null || VentureAt(p.PlaceId) != null ? "self" : "managed";
                    p.MonthlyRentCents = p.PriceCents / 120; p.TenantName = p.UseMode == "self" ? "玩家自营" : "既有托管租户";
                    p.LeaseEndDay = Clock.Today.TotalDays + 30; p.LastSettledMonth = Clock.Today.TotalDays / 30;
                }
                var shop = _shops.Find(s => s.Place.Id == p.PlaceId && s.Lease != null);
                if (shop != null) { shop.OwnsProperty = true; shop.Lease.Rent = Money.Zero; shop.Lease.Deposit = Money.Zero; }
            }
            if (d.cityAssetsSchema > 0)
            {
                foreach (var saved in d.cityEquities ?? Array.Empty<CityEquityState>())
                {
                    int i = _cityEquities.FindIndex(e => e.Id == saved.Id); if (i < 0) continue;
                    var state = saved.Copy(); state.PriceCents = Math.Max(100, Math.Min(1000000, state.PriceCents)); state.Units = Math.Max(0, state.Units); state.CostCents = Math.Max(0, state.CostCents);
                    _cityEquities[i] = state;
                }
                if (ulong.TryParse(d.cityEquityRandom, out ulong rng)) _cityEquityRandom.State = rng;
                _cityEquityLastDay = Math.Max(0, Math.Min(Clock.Today.TotalDays, d.cityEquityLastDay));
                CityFundCostCents = Math.Max(0, d.cityFundCostCents); CityFundRealizedCents = d.cityFundRealizedCents; CityFundCostEstimated = d.cityFundCostEstimated;
                _cityFundHistory = d.cityFundHistory != null && d.cityFundHistory.Length > 0 ? (long[])d.cityFundHistory.Clone() : new[] { FundUnitPrice.Cents };
                _cityAssetEffects.Clear(); foreach (string effect in d.cityAssetEffects ?? Array.Empty<string>()) if (!string.IsNullOrEmpty(effect)) _cityAssetEffects.Add(effect);
            }
            else
            {
                // Old index holdings have no acquisition-cost history; preserve holdings and label the estimate.
                CityFundCostCents = FundValue.Cents; CityFundCostEstimated = FundUnits > 0;
                _cityEquityLastDay = Clock.Today.TotalDays;
            }
            TickCityAssets();
        }
    }

    internal static class CityAssetSequence
    {
        internal static long[] TakeLastCompat(this IEnumerable<long> values, int count)
        { var data = values.ToArray(); return data.Skip(Math.Max(0, data.Length - count)).ToArray(); }
    }
}
