using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly Dictionary<string, CityFinanceSummary> _cityFinanceCache = new Dictionary<string, CityFinanceSummary>();
        private int _cityFinanceRevision = -1;
        public CityFinanceSummary GetCityFinance(GameDate from, GameDate to, string shopId = null)
        {
            if (_cityFinanceRevision != Ledger.Revision) { _cityFinanceCache.Clear(); _cityFinanceRevision = Ledger.Revision; }
            string key = from.TotalDays + ":" + to.TotalDays + ":" + (shopId ?? "*");
            if (!_cityFinanceCache.TryGetValue(key, out var summary))
                _cityFinanceCache[key] = summary = FinanceReport.CitySummary(Ledger, from, to, shopId);
            return summary;
        }

        public CityCashReserve GetCityCashReserve(int days = 7)
        {
            days = Math.Max(0, Math.Min(365, days));
            var today = Clock.Today;
            var result = new CityCashReserve { Days = days, From = today, To = today.AddDays(days), Balance = Ledger.Balance,
                ExtraProtection = Money.FromYuan(Math.Max(500, RestockReserveYuan)) };
            result.KnownMandatory = UpcomingBusinessPayments(days);
            result.KnownMandatory += GarageRentDueWithin(days);
            result.KnownMandatory += CityClimatePaymentsWithin(days);
            // Daily wages are contracted fixed bills, not a second additional safety margin.
            result.KnownMandatory += Money.FromYuan(Employees.Sum(x => (long)x.Person.DailyWageYuan) + McnDailyWages) * days;
            var billCredits = ForecastBillCredits();
            for (int offset = 1; offset <= days; offset++)
            {
                var date = today.AddDays(offset);
                foreach (var shop in _shops)
                    if (shop.Lease?.RentDue(date.TotalDays) == true) result.KnownMandatory += ForecastBillNet(billCredits, "rent-shop", shop.Place.Id, shop.Lease.SignedDay, RentOf(shop));
                if (PendingLease?.RentDue(date.TotalDays) == true) result.KnownMandatory += ForecastBillNet(billCredits, "rent-shop", PendingLease.PlaceId, PendingLease.SignedDay, PendingLease.Rent);
                foreach (var place in ResidentialRentsDue(date.TotalDays)) result.KnownMandatory += ForecastBillNet(billCredits, "rent-home", place.Id, ResidentialLeaseSignedDay(place), SignedResidentialRent(place));
                if (date.IsMonthStart) result.KnownMandatory += ForecastBillNet(billCredits, "utilities", Home.Id, 0, HomeUtilitiesAtDay(date.TotalDays));
            }
            result.EstimatedDaily = Money.FromYuan(VehicleCount(VehicleKind.Car) * 40L + VehicleCount(VehicleKind.Van) * 60L) * days;
            result.KnownMandatory += Money.FromCents(_employeePayroll.Sum(p=>Math.Max(0,p.OvertimeCents-p.Shops.Sum(x=>x.PaidOvertimeCents))));
            result.EstimatedDaily += EstimatedEmployeeOvertime(days);
            return result;
        }

        /// <summary>优先使用商品采购单的加权采购成本。没有可靠旧成本的商品按批发参考价估算并明确标注。</summary>
        public Money CityShopInventoryCapital(OwnedShop shop, out bool estimated)
        {
            estimated = false;
            if (shop == null) return Money.Zero;
            var known = new Dictionary<string, (long cents, long count)>();
            foreach (var e in Ledger.Entries)
            {
                var a = e.Attribution;
                if (e.ShopId != shop.Place.Id || !e.Amount.IsNegative || a == null || a.Quantity <= 0 || a.UnitCostCents <= 0 || a.ProductId == null) continue;
                known.TryGetValue(a.ProductId, out var old);
                known[a.ProductId] = (old.cents + a.UnitCostCents * a.Quantity, old.count + a.Quantity);
            }
            long total = 0;
            foreach (var line in shop.Sim.Stock.Lines())
            {
                long cost;
                if (known.TryGetValue(line.Part.Id, out var actual) && actual.count > 0) cost = actual.cents / actual.count;
                else { estimated = true; cost = Market.PriceOf(line.Part).Scale(.84).Cents; }
                total += cost * line.Count;
            }
            foreach (var line in shop.Sim.Stock.GoodsLines())
            {
                long cost;
                if ((known.TryGetValue(line.Key, out var actual) || known.TryGetValue("g:" + line.Key, out actual)) && actual.count > 0) cost = actual.cents / actual.count;
                else { estimated = true; cost = (Gifts.Get(line.Key)?.Price ?? Money.Zero).Scale(.65).Cents; }
                total += cost * line.Value;
            }
            // Goods already held by a checkout customer remain owned until the actual payment.
            foreach(var c in shop.Sim.ActiveCustomers.Where(c=>c.State==PCShopSell.Core.Shop.CustomerState.WaitingCheckout))
            {
                string id=c.GoodsId??c.Item?.Id;
                if(id!=null)
                {
                    if((known.TryGetValue(id,out var a)||known.TryGetValue("g:"+id,out a))&&a.count>0)total+=a.cents/a.count;
                    else {estimated=true;total+=c.Item!=null?Market.PriceOf(c.Item).Scale(.84).Cents:(Gifts.Get(id)?.WholesalePrice.Cents??0);}
                }
                if(c.DisplayTier>=0)foreach(var part in PartCatalog.BuildList(c.DisplayTier))
                {
                    if(known.TryGetValue(part.Id,out var a)&&a.count>0)total+=a.cents/a.count;
                    else {estimated=true;total+=Market.PriceOf(part).Scale(.84).Cents;}
                }
            }
            // Reserved components and assembled display units still tie up capital.
            foreach (int tier in shop.Sim.DisplayPcs.Concat(shop.Sim.Orders.Where(o => o.PartsReserved).Select(o => o.Tier)).Concat(shop.PendingDisplayTier>=0?new[]{shop.PendingDisplayTier}:Array.Empty<int>()))
                foreach (var part in PartCatalog.BuildList(tier))
                {
                    if (known.TryGetValue(part.Id, out var actual) && actual.count > 0) total += actual.cents / actual.count;
                    else { estimated = true; total += Market.PriceOf(part).Scale(.84).Cents; }
                }
            return Money.FromCents(total);
        }

        public void SaveCityFinance(SaveData d)
        {
            d.cityFinanceVersion = 1;
            d.cityLedgerAttributions = Ledger.Entries.Select((e, index) => new SaveCityLedgerAttribution {
                index = index, entryId = e.EntryId, businessKind = (int)e.BusinessKind, shopId = e.ShopId,
                propertyId = e.Attribution?.PropertyId, activityId = e.Attribution?.ActivityId, sourceEventId = e.SourceEventId,
                transactionKind = e.Attribution?.TransactionKind, productId = e.Attribution?.ProductId,
                employeeId=e.Attribution?.EmployeeId,payrollWorkday=e.Attribution?.PayrollWorkday??-1,
                quantity = e.Attribution?.Quantity ?? 0, unitCostCents = e.Attribution?.UnitCostCents ?? 0 }).ToArray();
        }
        public void LoadCityFinance(SaveData d)
        {
            var metadata = new Dictionary<int, SaveCityLedgerAttribution>();
            foreach (var m in d.cityLedgerAttributions ?? Array.Empty<SaveCityLedgerAttribution>())
                if (m != null && m.index >= 0 && m.index < Ledger.Entries.Count && Enum.IsDefined(typeof(LedgerBusinessKind), m.businessKind)) metadata[m.index] = m;
            var entries = Ledger.Entries.Select((e, i) => {
                if (!metadata.TryGetValue(i, out var m)) return new LedgerEntry(e.Date, e.Category, e.Amount, e.Memo, e.BalanceAfter, (i + 1).ToString(), LedgerAttribution.FromCategory(e.Category));
                var a = new LedgerAttribution((LedgerBusinessKind)m.businessKind, m.shopId, m.propertyId, m.activityId, m.sourceEventId, m.transactionKind).Product(m.productId, m.quantity, m.unitCostCents).Payroll(m.employeeId,m.payrollWorkday);
                return new LedgerEntry(e.Date, e.Category, e.Amount, e.Memo, e.BalanceAfter, m.entryId ?? (i + 1).ToString(), a);
            }).ToArray();
            Ledger.Restore(Ledger.Balance, entries);
        }
    }
}
