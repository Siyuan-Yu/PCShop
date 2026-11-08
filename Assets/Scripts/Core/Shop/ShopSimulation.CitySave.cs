using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Shop
{
    [Serializable]
    public sealed class CitySavedCustomer
    {
        public int id, intent, category, tier, state, reason, paidWith, goodsExpiry = -1, displayTier = -1;
        public string personId, visitId, itemId, goodsId, interest;
        public long arrived, ready, waiting, spendCents, budgetCents;
        public bool cash, boxed, chilled, preferenceAssigned;
        public bool countedBrandVisit;
        public int resaleId;
        public string resaleName;
        public CitySavedCustomer Copy() => (CitySavedCustomer)MemberwiseClone();
    }
    [Serializable]
    public sealed class CitySavedShopVisits
    {
        public string shopId;
        public int nextCustomerId, checkoutMinutes, todayCustomers, todayLost;
        public long todaySalesCents;
        public double arrivalCredit, arrivalThreshold = 1;
        public bool wasOpen;
        public CitySavedCustomer[] customers;
        public int[] queue;
        public DayReportData day;
    }
    [Serializable]
    public sealed class DayReportData
    {
        public int day, customers, paid, orders, expensive, impatient, stock, cash, closed;
        public long revenue;
    }
    public sealed partial class ShopSimulation
    {
        public CitySavedShopVisits SaveCityCustomers() => new CitySavedShopVisits {
            shopId = ShopId, nextCustomerId = _nextCustomerId, checkoutMinutes = _employeeBusyMinutes, wasOpen = _wasOpen,
            todayCustomers = TodayCustomers, todayLost = TodayLost, todaySalesCents = TodaySales.Cents,
            arrivalCredit = _arrivalCredit, arrivalThreshold = _arrivalThreshold,
            queue = _queue.Select(c => c.Id).ToArray(),
            day = new DayReportData { day = _day.Date.TotalDays, customers = _day.Customers, paid = _day.Paid, orders = _day.Orders,
                expensive = _day.LostTooExpensive, impatient = _day.LostImpatient, stock = _day.LostNoStock, cash = _day.LostNoCash, closed = _day.LostClosed, revenue = _day.Revenue.Cents },
            customers = _active.Where(c => c.State == CustomerState.Browsing || c.State == CustomerState.WaitingCheckout).Select(c => new CitySavedCustomer {
                id = c.Id, intent = (int)c.Intent, category = (int)c.DesiredCategory, tier = c.BuildTier, countedBrandVisit = c.CountedBrandVisit,
                state = (int)c.State, reason = (int)c.Reason, paidWith = (int)c.PaidWith,
                personId = c.PersonId, visitId = c.VisitId, itemId = c.Item?.Id, goodsId = c.GoodsId, interest = c.RetailInterestSeries,
                arrived = c.ArrivedAt.TotalMinutes, ready = c.ReadyAt.TotalMinutes, waiting = c.WaitingSince.TotalMinutes,
                spendCents = c.Spend.Cents, budgetCents = c.RetailBudgetCents, cash = c.CarriesCash,
                boxed = c.ItemBoxed, chilled = c.GoodsChilled, goodsExpiry = c.GoodsExpiryDay, displayTier = c.DisplayTier,
                resaleId=c.ResaleId,resaleName=c.ResaleName,preferenceAssigned = c.RetailPreferenceAssigned }).ToArray()
        };

        /// <summary>Inventory already excludes goods held by these visitors. Restore references only: no arrival events, purchases or stock removal.</summary>
        public void RestoreCityCustomers(CitySavedShopVisits data)
        {
            if (data == null || data.shopId != ShopId) return;
            _active.Clear(); _queue.Clear();
            var byId = new Dictionary<int, ShopCustomer>();
            foreach (var x in data.customers ?? Array.Empty<CitySavedCustomer>())
            {
                if (x == null) continue;
                if (x.id <= 0 || byId.ContainsKey(x.id) || x.arrived < 0 || x.ready < 0 || x.waiting < 0
                    || x.spendCents < 0 || !Enum.IsDefined(typeof(CustomerIntent), x.intent) || !Enum.IsDefined(typeof(PartCategory), x.category)
                    || (x.state != (int)CustomerState.Browsing && x.state != (int)CustomerState.WaitingCheckout))
                    throw new InvalidOperationException("顾客存档状态无效，保留原存档并检查版本：" + x.id);
                string itemId = string.IsNullOrEmpty(x.itemId) ? null : x.itemId, goodsId = string.IsNullOrEmpty(x.goodsId) ? null : x.goodsId;
                if (itemId != null && !PartCatalog.Exists(itemId) || goodsId != null && Gifts.Get(goodsId) == null)
                    throw new InvalidOperationException("存档顾客持有未知商品，保留原存档并检查目录：" + (x.itemId ?? x.goodsId));
                var c = new ShopCustomer(x.id, (CustomerIntent)x.intent, (PartCategory)x.category,
                    Math.Max(0, Math.Min(PartCatalog.TierCount - 1, x.tier)), new GameTimestamp(x.arrived), new GameTimestamp(x.ready), x.cash) {
                    State = (CustomerState)x.state, CountedBrandVisit = x.countedBrandVisit, Reason = Enum.IsDefined(typeof(LeaveReason), x.reason) ? (LeaveReason)x.reason : LeaveReason.None,
                    PaidWith = Enum.IsDefined(typeof(PayChannel), x.paidWith) ? (PayChannel)x.paidWith : PayChannel.Cash,
                    PersonId = string.IsNullOrEmpty(x.personId) ? null : x.personId, VisitId = string.IsNullOrEmpty(x.visitId) ? null : x.visitId, WaitingSince = new GameTimestamp(x.waiting),
                    Item = itemId == null ? null : PartCatalog.Get(itemId), GoodsId = goodsId,ResaleId=x.resaleId,ResaleName=x.resaleName,
                    ItemBoxed = x.boxed, GoodsChilled = x.chilled, GoodsExpiryDay = x.goodsExpiry,
                    DisplayTier = x.displayTier >= 0 && x.displayTier < PartCatalog.TierCount ? x.displayTier : -1,
                    Spend = Money.FromCents(x.spendCents), RetailInterestSeries = x.interest,
                    RetailBudgetCents = Math.Max(0, x.budgetCents), RetailPreferenceAssigned = x.preferenceAssigned };
                _active.Add(c); byId[c.Id] = c;
            }
            foreach (int id in data.queue ?? Array.Empty<int>())
                if (byId.TryGetValue(id, out var c) && c.State == CustomerState.WaitingCheckout && !_queue.Contains(c)) _queue.Add(c);
            foreach (var c in _active.Where(c => c.State == CustomerState.WaitingCheckout && !_queue.Contains(c))) _queue.Add(c);
            _nextCustomerId = Math.Max(Math.Max(1, data.nextCustomerId), byId.Count == 0 ? 1 : byId.Keys.Max() + 1);
            _employeeBusyMinutes = Math.Max(0, data.checkoutMinutes); _wasOpen = data.wasOpen;
            TodayCustomers = Math.Max(0, data.todayCustomers); TodayLost = Math.Max(0, data.todayLost); TodaySales = Money.FromCents(Math.Max(0, data.todaySalesCents));
            _arrivalCredit = data.arrivalCredit >= -.35 && data.arrivalCredit <= 1.35 ? data.arrivalCredit : 0;
            _arrivalThreshold = data.arrivalThreshold >= .65 && data.arrivalThreshold <= 1.35 ? data.arrivalThreshold : 1;
            var d = data.day;
            _day = d == null ? new DayReport() : new DayReport { Date = new GameDate(Math.Max(0, d.day)), Customers = d.customers,
                Paid = d.paid, Orders = d.orders, LostTooExpensive = d.expensive, LostImpatient = d.impatient, LostNoStock = d.stock,
                LostNoCash = d.cash, LostClosed = d.closed, Revenue = Money.FromCents(d.revenue) };
        }
    }
}
