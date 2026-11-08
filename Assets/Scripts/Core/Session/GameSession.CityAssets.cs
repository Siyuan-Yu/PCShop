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
        public string CityInvestmentNews => _stockMarket.News.Length>0?_stockMarket.News[_stockMarket.News.Length-1].Headline:"交易日三次报价；企业盈利、行业需求与经营事件共同影响股价。";

        public IEnumerable<Place> CityCommercialProperties => City.Places.Where(IsCommercialPremises);
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
            var place = City.GetPlace(id);
            return place.Kind == PlaceKind.Home ? ResidentialPrice(place) : (FootfallOf(place).MonthlyRent * 120).Scale(ShopSaleFactor);
        }
        public CityPropertyPurchase PreviewCityPropertyPurchase(string id, bool mortgage)
        {
            if (!City.HasPlace(id)) return null;
            var place = City.GetPlace(id);
            if (!IsCommercialPremises(place)) return null;
            var shop = ShopAt(id); var venture = VentureAt(id);
            var price = CityPropertyPrice(id);
            var quote = new CityPropertyPurchase { PlaceId = id, Address = CityPropertyAddress(place), Price = price,
                Fee = price.Scale(.02), IsSelfUse = shop != null || venture != null,
                HasTenant = shop == null && venture == null && !IsVacant(place) && !IsBareUnit(place),
                Tenant = shop != null ? ShopTitle(shop) : venture != null ? Ventures.Get(venture.Kind).Name : IsVacant(place) || IsBareUnit(place) ? "空置" : place.Name + "原经营者" };
            quote.Mortgage = Money.Zero;
            quote.DownPayment = price;
            if (shop?.Lease != null)
            {
                quote.DepositCredit = shop.Lease.Deposit;
                quote.PrepaidCredit = shop.Lease.Rent.Scale(shop.Lease.UnusedRentShare(Clock.Today.TotalDays));
            }
            else if (venture != null)
            {
                quote.DepositCredit = Money.FromCents(venture.DepositCents);
                quote.PrepaidCredit = Money.FromCents(venture.RentCents).Scale((7 - Math.Max(0, Clock.Today.TotalDays - venture.RentStartDay) % 7) / 7.0);
            }
            quote.TotalDue = Money.Max(Money.Zero, quote.DownPayment + quote.Fee - quote.DepositCredit - quote.PrepaidCredit);
            quote.CashAfter = Ledger.Balance - quote.TotalDue;
            return quote;
        }
        public string CityPropertyPurchaseBlocker(string id, bool mortgage)
        {
            if (IsGameOver) return "当前不能交易";
            if (mortgage) return "物业仅支持全款购买";
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
            if (q.TotalDue.IsPositive && !Ledger.TryDebit(Clock.Today, "购置物业", q.TotalDue, q.Address + "（含2%交易费，抵扣押金及本周未使用租金）", LedgerAttribution.ForAsset(id, eventId))) return Fail("交易时现金不足，产权未改变。");
            var property = new OwnedProperty { PlaceId = id, PriceCents = q.Price.Cents,
                BoughtDay = Clock.Today.TotalDays, CitySchema = 1,
                UseMode = q.IsSelfUse ? "self" : q.HasTenant ? "npc" : "vacant", TenantName = q.IsSelfUse ? "玩家自营" : q.Tenant,
                WeeklyRentCents = ShopRentQuote(place).Cents, CommercialRentFromDay = Clock.Today.TotalDays,
                LeaseEndDay = Clock.Today.TotalDays + 360, LastSettledMonth = Clock.Today.TotalDays / 30 };
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
            return null;
        }
        private bool SellCityProperty(string id)
        {
            if (!InAgency || IsGameOver) return Fail("请到中介办理出售。");
            string why = CityPropertySaleBlocker(id); if (why != null) return Fail(why + "。");
            var p = PropertyAt(id); Money net = PropertySaleNet(p);
            string key = "property:sale:" + id + ":" + p.BoughtDay + ":" + _cityAssetEffects.Count;
            if (net.IsPositive) Ledger.Credit(Clock.Today, "物业出售", net, "按估值九折，包含交易成本", LedgerAttribution.ForAsset(id, key));
            _cityAssetEffects.Add(key); _properties.Remove(p);
            if (id == ParkPlaceId) { ParkPlaceId = null; ParkReadyDay = 0; }
            if (p.UseMode == "vacant" || p.UseMode == "managed") { _vacant.Add(id); if (!_listings.Any(l => l.PlaceId == id)) AddListing(City.GetPlace(id), Clock.Today.TotalDays, Clock.Today.TotalDays + 45); }
            Say("物业已出售；原租户继续经营，人物、商品与关系不会删除。"); return true;
        }
        public bool SetCityPropertyRental(string id, bool rentOut)
        {
            if(City.HasPlace(id)&&City.GetPlace(id).Kind==PlaceKind.Home)return Fail("住宅只按实际NPC住户收租，空房不产生托管租金。");
            var p = PropertyAt(id); if (p == null || IsGameOver) return false;
            if (rentOut && City.HasPlace(id) && !IsCommercialPremises(City.GetPlace(id))) return Fail("专用场所不可重新出租。");
            if (!InAgency) return Fail("请到中介办理出租登记。");
            if (!City.HasPlace(id) || !AgencyServes(CurrentPlace, City.GetPlace(id))) return Fail("请到房源所属片区的中介办理。");
            if (ShopAt(id) != null || VentureAt(id) != null) return Fail("自营物业不能同时向自己收租。");
            if (p.UseMode == "npc") return Fail("现有租户租约受保护，本轮不提供强制驱逐。");
            if (!rentOut && p.UseMode == "managed" && Clock.Today.TotalDays < p.LeaseEndDay) return Fail("托管租约尚未到期，不能直接腾空。");
            p.UseMode = rentOut ? "managed" : "vacant"; p.TenantName = rentOut ? "中介托管商户" : "";
            if (rentOut) { p.LeaseEndDay = Clock.Today.TotalDays + 28; p.WeeklyRentCents = ShopRentQuote(City.GetPlace(id)).Cents; p.CommercialRentFromDay = Clock.Today.TotalDays; }
            Say(rentOut ? "出租已登记：下一月账期收取租金，管理及空置折损为15%。" : "租约结束，物业恢复空置。"); return true;
        }
        public bool OpenCityOwnedShop(string id, string retailKind = "computer")
        {
            if (!City.HasPlace(id) || !IsCommercialPremises(City.GetPlace(id))) return Fail("该地点不能作为商铺开业。");
            var p = PropertyAt(id); if (p == null || IsGameOver || !InAgency || !AgencyServes(CurrentPlace, City.GetPlace(id))) return Fail("在中介选择自有空置物业开店。");
            if (p.UseMode != "vacant" || ShopAt(id) != null || VentureAt(id) != null) return Fail("该物业仍被使用，不能清走原租户。");
            var place = City.GetPlace(id); var kind = IsDedicatedVenue(place)?"computer":RetailShopTypes.Valid(retailKind);
            if (!HasShop) MoveInto(place, Money.Zero, Money.Zero); else OpenNewShop(place, Money.Zero, Money.Zero);
            _focus.OwnsProperty = true; _focus.RetailKind = kind; _focus.Sim.RetailKind = kind; p.UseMode = "self"; p.TenantName = "玩家自营";
            if(IsDedicatedVenue(place)){ShowVenueHandover(place);return true;}
            Say("自有物业开店完成，不收自己的房租。按正常流程购买设施、装修并在次日营业。"); return true;
        }
        private void SettleCityProperties(GameDate today)
        {
            int day = today.TotalDays;
            int month = today.TotalDays / 30;
            foreach (var p in _properties)
            {
                bool residential=City.HasPlace(p.PlaceId)&&City.GetPlace(p.PlaceId).Kind==PlaceKind.Home;
                bool self = ShopAt(p.PlaceId) != null || VentureAt(p.PlaceId) != null || p.PlaceId == ParkPlaceId;
                if (!residential && !self && day > p.CommercialRentFromDay && (day - p.CommercialRentFromDay) % GameDate.DaysPerWeek == 0
                    && (p.UseMode == "npc" || p.UseMode == "managed"))
                {
                    Money rent = Money.FromCents(p.WeeklyRentCents).Scale(p.UseMode == "managed" ? .85 : 1);
                    if (rent.IsPositive) Ledger.Credit(today, "物业租金", rent, p.TenantName + "本周租金",
                        LedgerAttribution.ForAsset(p.PlaceId, "property:" + p.PlaceId + ":week:" + day + ":rent"));
                }
                if (!today.IsMonthStart || p.LastSettledMonth >= month || day <= p.BoughtDay) continue;
                p.LastSettledMonth = month;
                string basis = "property:" + p.PlaceId + ":month:" + month;
                var residents=residential?ResidentialTenants(p.PlaceId):null;
                if(residential)
                {
                    p.TenantName=string.Join("、",residents.Select(r=>CityPerson(r.PersonId)?.Name??r.PersonId));
                    p.TenantPersonId=residents.FirstOrDefault()?.PersonId;
                    p.UseMode=residents.Length>0?"npc":"vacant";
                }
                if (residential && !self && (p.UseMode == "npc" || p.UseMode == "managed"))
                {
                    Money rent = Money.FromCents(p.MonthlyRentCents).Scale(p.UseMode == "managed" ? .85 : 1);
                    if (rent.IsPositive) Ledger.Credit(today, "物业租金", rent, p.TenantName + "本月租金", LedgerAttribution.ForAsset(p.PlaceId, basis + ":rent"));
                }
                Money maintenance = Money.FromCents(p.PriceCents).Scale(.001);
                if (maintenance.IsPositive) Ledger.ForceDebit(today, "物业维护", maintenance, residential?"出租住宅月维护":"商用物业月维护", LedgerAttribution.ForAsset(p.PlaceId, basis + ":maintenance"));
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
            string appBlocker=AppBlocker(PhoneApps.Securities);if(appBlocker!=null)return Fail(appBlocker);
            string tradeBlocker=StockTradeBlocker(id,units,buy?"buy":"sell");if(tradeBlocker!=null)return Fail(tradeBlocker+"。");
            var e = _cityEquities.Find(x => x.Id == id);
            if ((id != "hardware" && e == null) || IsGameOver || units <= 0 || units > 1000000) return Fail("请通过手机证券，按整数份额交易。");
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
            ConsumeStockLiquidity(id,units);
            _cityAssetEffects.Add(key); Say("交易完成，已按本次报价扣减可成交数量。"); return true;
        }
        public void SaveCityAssets(SaveData d)
        {
            d.stockMarket=_stockMarket.Copy();
            d.cityAssetsSchema = 1; d.cityOwnedShopPlaces = _shops.Where(s => s.OwnsProperty).Select(s => s.Place.Id).ToArray();
            d.cityEquities = _cityEquities.Select(e => e.Copy()).ToArray(); d.cityEquityRandom = _cityEquityRandom.State.ToString(CultureInfo.InvariantCulture);
            d.cityEquityLastDay = _cityEquityLastDay; d.cityFundCostCents = CityFundCostCents; d.cityFundRealizedCents = CityFundRealizedCents;
            d.cityFundCostEstimated = CityFundCostEstimated; d.cityAssetEffects = _cityAssetEffects.ToArray();
            d.cityFundHistory = (long[])_cityFundHistory.Clone();
        }
        public void LoadCityAssets(SaveData d)
        {
            // Retire legacy property financing without changing ownership, leases or cash.
            HomeMortgageCents = HomeMonthlyPrincipalCents = 0;
            var uniqueProperties = _properties.GroupBy(p => p.PlaceId).Select(g => g.First()).ToArray();
            if (uniqueProperties.Length != _properties.Count) { _properties.Clear(); _properties.AddRange(uniqueProperties); }
            foreach (var p in _properties)
            {
                p.PriceCents = Math.Max(1, p.PriceCents);
                p.MortgageCents = p.MonthlyPrincipalCents = 0;
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
            _stockMarket=d.stockMarket?.Copy()??new StockMarketState {LastMinute=Clock.Now.TotalMinutes,IndexPriceCents=HardwareMarketIndex.Cents,IndexPreviousCloseCents=HardwareMarketIndex.Cents};
            _stockMarket.LastMinute=Math.Min(Clock.Now.TotalMinutes,Math.Max(0,_stockMarket.LastMinute));
            _stockMarket.News=_stockMarket.News??Array.Empty<StockNews>();
            _stockMarket.IndexQuotes=_stockMarket.IndexQuotes??Array.Empty<StockQuote>();
            if(_stockMarket.IndexQuotes.Length==0)_stockMarket.IndexQuotes=new[]{new StockQuote{Minute=Clock.Now.TotalMinutes,PriceCents=_stockMarket.IndexPriceCents}};
            foreach(var e in _cityEquities)InitializeStockCompany(e);
            EnsureVacantUnitListings(Clock.Today.TotalDays);
            TickCityAssets();
        }
    }

    internal static class CityAssetSequence
    {
        internal static long[] TakeLastCompat(this IEnumerable<long> values, int count)
        { var data = values.ToArray(); return data.Skip(Math.Max(0, data.Length - count)).ToArray(); }
    }
}
