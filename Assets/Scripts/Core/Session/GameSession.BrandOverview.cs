using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public int BrandStatsFirstDay => Math.Max(0, BrandDay - 6);
        public int BrandVisitsSinceDay => _brands.VisitsSinceDay;

        private bool RecordBrandShopVisit(OwnedShop shop, ShopCustomer customer)
        {
            if (!CityCustomerIsInside(customer, shop.Place.Id)) return false;
            if (customer.CountedBrandVisit) return true;
            customer.CountedBrandVisit = true;
            if (_brands.VisitsSinceDay < 0) _brands.VisitsSinceDay = BrandDay;
            _brands.Visits = _brands.Visits.Where(v => v.Day >= BrandStatsFirstDay).ToArray();
            var record = _brands.Visits.FirstOrDefault(v => v.ShopId == shop.Place.Id && v.Day == BrandDay);
            if (record == null)
            {
                record = new BrandShopVisits { ShopId = shop.Place.Id, Day = BrandDay };
                _brands.Visits = AppendBrand(_brands.Visits, record);
            }
            record.Count++;
            return true;
        }

        public int BrandActualVisits(string brandId)
        {
            var brand = Brand(brandId);
            if (brand == null) return 0;
            var ids = BrandShops(brand).Select(s => s.Place.Id).ToArray();
            return _brands.Visits.Where(v => ids.Contains(v.ShopId) && v.Day >= BrandStatsFirstDay && v.Day <= BrandDay).Sum(v => v.Count);
        }

        public Money BrandWeeklyRevenue(string brandId)
        {
            var brand = Brand(brandId);
            return brand == null ? Money.Zero : Money.FromCents(BrandShops(brand).Sum(shop =>
                GetCityFinance(new GameDate(BrandStatsFirstDay), Clock.Today, shop.Place.Id).Sales.Cents));
        }

        public Money BrandWeeklyExpenses(string brandId, bool commonOnly = false)
        {
            var brand = Brand(brandId);
            if (brand == null) return Money.Zero;
            var ids = BrandShops(brand).Select(s => s.Place.Id).ToArray();
            return Money.FromCents(-Ledger.Entries.Where(e => e.Date.TotalDays >= BrandStatsFirstDay && e.Date.TotalDays <= BrandDay
                && e.Amount.IsNegative && e.BusinessKind == LedgerBusinessKind.Operating
                && (commonOnly ? e.ShopId == null && e.Attribution.PropertyId == brandId
                    : ids.Contains(e.ShopId) || e.Attribution.PropertyId == brandId)).Sum(e => e.Amount.Cents));
        }

        public double BrandDealTraffic(BrandDeal deal)
        {
            var brand = Brand(deal.BrandId);
            if (!string.IsNullOrEmpty(deal.KolContractId)) return 0;
            if (brand == null || deal.Ended || deal.End <= BrandDay || deal.Response == "pause") return 0;
            return (deal.Mode == "endorse" ? .12 : .08) * (deal.Exclusive ? 1.25 : 1) * BrandAudienceFit(deal.PersonId, brand.Kind)
                + (_brands.Careers.FirstOrDefault(c => c.PersonId == deal.PersonId)?.Momentum ?? 0) * .002
                - deal.Severity * .065 * (deal.Response == "negotiate" ? .5 : 1);
        }

        public double BrandPromotionTraffic(string brandId) =>
            _brands.Deals.Where(d => d.BrandId == brandId).Sum(BrandDealTraffic);

        private static double BrandStandingTraffic(BrandRecord brand) =>
            Math.Min(.15, brand.Influence * .001) + (brand.Reputation - 50) * .002;

        private double BrandBackgroundTraffic(BrandRecord brand) =>
            BrandStandingTraffic(brand)
            + _brands.Resources.Where(r => r.BrandId == brand.Id && r.End > BrandDay).Sum(r => r.Mode == "channel" ? .12 : .05);

        public double BrandEffectivePromotionTraffic(string brandId)
        {
            var brand = Brand(brandId);
            if (brand == null) return 0;
            double background = BrandBackgroundTraffic(brand);
            return Math.Max(-.30, Math.Min(.60, background + BrandPromotionTraffic(brandId)))
                - Math.Max(-.30, Math.Min(.60, background));
        }

        public double BrandEffectiveDealTraffic(BrandDeal deal)
        {
            double total = BrandPromotionTraffic(deal.BrandId);
            if (Math.Abs(total) < .000001) return BrandDealTraffic(deal);
            return BrandDealTraffic(deal) * BrandEffectivePromotionTraffic(deal.BrandId) / total;
        }

        private LedgerAttribution BrandAttribution(string brandId) =>
            new LedgerAttribution(LedgerBusinessKind.Operating, propertyId: brandId);
    }
}
