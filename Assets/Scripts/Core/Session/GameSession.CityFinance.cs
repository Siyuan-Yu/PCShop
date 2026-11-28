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
            string accountId = ShopAt(shopId)?.SalesAccountId;
            string key = from.TotalDays + ":" + to.TotalDays + ":" + (shopId ?? "*") + ":" + accountId;
            if (!_cityFinanceCache.TryGetValue(key, out var summary))
                _cityFinanceCache[key] = summary = FinanceReport.CitySummary(Ledger, from, to, shopId, accountId);
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
            result.KnownMandatory += Money.FromYuan(Employees.Where(x=>CompanyOfEmployee(x.Person.Id)==null).Sum(x => (long)x.Person.DailyWageYuan) + McnDailyWages) * days;
            var billCredits = ForecastBillCredits();
            for (int offset = 1; offset <= days; offset++)
            {
                var date = today.AddDays(offset);
                foreach (var shop in _shops)
                    if (CompanyOfShop(shop)==null&&CommercialRentDue(shop.Lease, date.TotalDays)) result.KnownMandatory += ForecastBillNet(billCredits, "rent-shop", shop.Place.Id, shop.Lease.SignedDay, RentOf(shop));
                if (CommercialRentDue(PendingLease, date.TotalDays)) result.KnownMandatory += ForecastBillNet(billCredits, "rent-shop", PendingLease.PlaceId, PendingLease.SignedDay, PendingLease.Rent);
                foreach (var place in ResidentialRentsDue(date.TotalDays)) result.KnownMandatory += ForecastBillNet(billCredits, "rent-home", place.Id, ResidentialLeaseSignedDay(place), SignedResidentialRent(place));
                if (date.IsMonthStart) result.KnownMandatory += ForecastBillNet(billCredits, "utilities", Home.Id, 0, HomeUtilitiesAtDay(date.TotalDays));
            }
            result.EstimatedDaily = Money.FromYuan(VehicleCount(VehicleKind.Car) * 40L + VehicleCount(VehicleKind.Van) * 60L) * days;
            result.KnownMandatory += Money.FromCents(_employeePayroll.Where(p=>p.CompanyId==null).Sum(p=>Math.Max(0,p.OvertimeCents-p.Shops.Sum(x=>x.PaidOvertimeCents))));
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
            var displayTiers = shop.PendingDisplayTier >= 0 ? shop.Sim.DisplayPcs.Append(shop.PendingDisplayTier) : shop.Sim.DisplayPcs;
            foreach (var part in displayTiers.SelectMany(PartCatalog.BuildList).Concat(shop.Sim.Orders.SelectMany(o => o.ReservedParts)))
            {
                if (known.TryGetValue(part.Id, out var actual) && actual.count > 0) total += actual.cents / actual.count;
                else { estimated = true; total += Market.PriceOf(part).Scale(.84).Cents; }
            }
            return Money.FromCents(total);
        }

        public void SaveCityFinance(SaveData d)
        {
            d.cityFinanceVersion = 2;
            d.cityLedgerAttributions = Ledger.Entries.Select((e, index) => new SaveCityLedgerAttribution {
                index = index, entryId = e.EntryId, businessKind = (int)e.BusinessKind, shopId = e.ShopId,
                salesAccountId = e.Attribution?.SalesAccountId,
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
                if (!metadata.TryGetValue(i, out var m)) return new LedgerEntry(e.Date, e.Category, e.Amount, e.Memo, e.BalanceAfter, (i + 1).ToString(), LedgerAttribution.FromCategory(e.Category),e.CashAccountId);
                var a = new LedgerAttribution((LedgerBusinessKind)m.businessKind, m.shopId, m.propertyId, m.activityId, m.sourceEventId, m.transactionKind).Product(m.productId, m.quantity, m.unitCostCents).Payroll(m.employeeId,m.payrollWorkday);
                if (!string.IsNullOrEmpty(m.salesAccountId)) a = a.AssignShopSale(m.shopId, m.salesAccountId);
                return new LedgerEntry(e.Date, e.Category, e.Amount, e.Memo, e.BalanceAfter, m.entryId ?? (i + 1).ToString(), a,e.CashAccountId);
            }).ToArray();
            Ledger.Restore(Ledger.Balance, entries);
        }

        private void RestoreLegacyShopSales(SaveData d)
        {
            if (d.cityFinanceVersion >= 2) return;

            var receipts = _eventPaidReceipts
                .Where(r => r.ShopId != null && r.Product != null && (r.CustomerId > 0 || r.Key?.Contains(":bulk:") == true))
                .GroupBy(r => (r.Day, r.PaidCents, r.Product)).ToDictionary(group => group.Key, group => group.ToArray());
            var sales = Ledger.Entries.Select((entry, index) => (entry, index))
                .Where(row => row.entry.Category != FinanceReport.PetBoardingCategory
                    && row.entry.Amount.IsPositive && FinanceReport.IsOperatingSale(row.entry)).ToArray();
            if (sales.Length == 0) return;

            var matched = new Dictionary<int, EventPaidReceipt>();
            var saleGroups = sales.Select(row => (row.index, Key: (row.entry.Date.TotalDays, row.entry.Amount.Cents, LegacyReceiptProduct(row.entry))))
                .Where(row => row.Key.Item3 != null).GroupBy(row => row.Key);
            foreach (var group in saleGroups)
            {
                // Receipts and ledger rows were appended in checkout order. Only match a complete group.
                if (!receipts.TryGetValue(group.Key, out var paid) || group.Count() != paid.Length) continue;
                int i = 0;
                foreach (var row in group) matched.Add(row.index, paid[i++]);
            }

            var possiblyMisassigned = new Dictionary<string, int>();
            var legacyOrder = LegacyVisitShopOrder(d);
            if (legacyOrder != null)
                for (int i = 0; i < legacyOrder.Length; i++)
                {
                    var shop = legacyOrder[i];
                    string oldId = d.cityShopVisits[i].shopId;
                    if (oldId == shop.Place.Id) continue;
                    if (!possiblyMisassigned.TryGetValue(oldId, out int fromDay) || shop.SalesStartDay < fromDay)
                        possiblyMisassigned[oldId] = shop.SalesStartDay;
                }

            var entries = Ledger.Entries.ToArray();
            foreach (var (entry, index) in sales)
            {
                string shopId = entry.ShopId;
                OwnedShop shop;
                if (matched.TryGetValue(index, out var receipt))
                {
                    shopId = receipt.ShopId;
                    shop = ShopAt(shopId);
                    if (shop != null && (shop.Lease?.SignedDay ?? -1) != receipt.SignedDay) shop = null;
                }
                else
                {
                    if (shopId != null && IsSimulatedShopSale(entry)
                        && possiblyMisassigned.TryGetValue(shopId, out int fromDay) && entry.Date.TotalDays >= fromDay)
                        shopId = null; // Keep the cash entry without claiming it for a different storefront.
                    shop = ShopAt(shopId);
                    if (shop != null && entry.Date.TotalDays < shop.SalesStartDay) shop = null;
                }
                var attribution = entry.Attribution.AssignShopSale(shopId, shop?.SalesAccountId);
                entries[index] = new LedgerEntry(entry.Date, entry.Category, entry.Amount, entry.Memo,
                    entry.BalanceAfter, entry.EntryId, attribution,entry.CashAccountId);
            }
            Ledger.Restore(Ledger.Balance, entries);
        }

        private void RestorePetBoardingSalesAccounts()
        {
            LedgerEntry[] entries = null;
            for (int i = 0; i < Ledger.Entries.Count; i++)
            {
                var entry = Ledger.Entries[i];
                if (entry.Category != FinanceReport.PetBoardingCategory || !entry.Amount.IsPositive
                    || entry.BusinessKind != LedgerBusinessKind.Operating || entry.Attribution.SalesAccountId != null
                    || entry.ShopId == null) continue;

                var shop = ShopAt(entry.ShopId);
                if (shop == null || entry.Date.TotalDays < shop.SalesStartDay) continue;
                // Pet stay ids record the offer minute. An old stay can settle on the day a new shop opens.
                string prefix = $"pet:{entry.ShopId}:";
                string source = entry.SourceEventId;
                if (source?.StartsWith(prefix, StringComparison.Ordinal) != true) continue;
                int end = source.IndexOf(':', prefix.Length);
                if (end < 0 || !long.TryParse(source.Substring(prefix.Length, end - prefix.Length), out long offeredAt)
                    || offeredAt < (long)shop.SalesStartDay * GameTimestamp.MinutesPerDay) continue;

                entries ??= Ledger.Entries.ToArray();
                var attribution = entry.Attribution.AssignShopSale(entry.ShopId, shop.SalesAccountId);
                entries[i] = new LedgerEntry(entry.Date, entry.Category, entry.Amount, entry.Memo,
                    entry.BalanceAfter, entry.EntryId, attribution, entry.CashAccountId);
            }
            if (entries != null) Ledger.Restore(Ledger.Balance, entries);
        }

        private static bool IsSimulatedShopSale(LedgerEntry entry) =>
            LegacyReceiptProduct(entry) != null
            || entry.Category == FinanceReport.BuildCategory && entry.SourceEventId?.StartsWith("build:", StringComparison.Ordinal) == true;

        private static readonly string[] ReceiptMemoSuffixes = { "（杂货）", "（二手转售）", "（盒装）", "（散件）", "（展示机）" };
        private static string LegacyReceiptProduct(LedgerEntry entry)
        {
            if ((entry.Category != FinanceReport.RetailCategory && entry.Category != FinanceReport.BuildCategory)
                || string.IsNullOrEmpty(entry.Memo)) return null;
            foreach (var suffix in ReceiptMemoSuffixes)
                if (entry.Memo.EndsWith(suffix, StringComparison.Ordinal))
                    return entry.Memo.Substring(0, entry.Memo.Length - suffix.Length);
            int separator = entry.Memo.IndexOf(" · ", StringComparison.Ordinal);
            return entry.Category == FinanceReport.RetailCategory && separator >= 0 && entry.Memo.LastIndexOf(" ×", StringComparison.Ordinal) > separator + 3
                ? entry.Memo.Substring(separator + 3) : null;
        }
    }
}
