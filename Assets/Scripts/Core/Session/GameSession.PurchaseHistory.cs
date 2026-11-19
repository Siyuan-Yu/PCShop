using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class ShopPurchaseReceipt
    {
        public string ShopId, Channel;
        public long Minute;
    }

    public sealed partial class SaveData
    {
        public ShopPurchaseReceipt[] shopPurchaseReceipts;
    }

    public sealed partial class GameSession
    {
        private readonly List<EmployeePurchaseTrip> _employeePurchaseHistory = new List<EmployeePurchaseTrip>();

        private readonly Dictionary<string, ShopPurchaseReceipt> _latestShopReceipts = new Dictionary<string, ShopPurchaseReceipt>();

        public int ShopPurchaseReceiptRevision { get; private set; }

        private void RecordShopPurchaseReceipt(OwnedShop shop, long minute, string channel)
        {
            if (shop?.Rented != true) return;
            string id = shop.Place.Id;
            if (_latestShopReceipts.TryGetValue(id, out var receipt) && receipt.Minute > minute) return;
            ShopPurchaseReceiptRevision++;
            _latestShopReceipts[id] = new ShopPurchaseReceipt { ShopId = id, Minute = minute, Channel = channel };
        }

        public (long Minute, string Channel)? LatestShopPurchaseReceipt(string shopId)
        {
            if (_latestShopReceipts.TryGetValue(shopId, out var receipt)) return (receipt.Minute, receipt.Channel);

            var employee = _employeeTrips.Concat(_employeePurchaseHistory)
                .Where(trip => trip.ShopId == shopId && trip.Delivered && trip.DeliveryAt > 0 && trip.DeliveryAt <= Clock.Now.TotalMinutes)
                .OrderByDescending(trip => trip.DeliveryAt).FirstOrDefault();
            var order = _cityProcurementOrders.Where(record => record.ShopId == shopId && record.EmployeeId == null
                    && record.State == "已交付" && record.DeliveryAt > 0 && record.DeliveryAt <= Clock.Now.TotalMinutes)
                .OrderByDescending(record => record.DeliveryAt).FirstOrDefault();
            if (employee != null && (order == null || employee.DeliveryAt >= order.DeliveryAt))
                return (employee.DeliveryAt, "员工代采");
            return order == null ? null : (order.DeliveryAt, order.Channel);
        }

        public (bool Employee, bool Online, int Short) ShopProcurementSummary(OwnedShop shop)
        {
            string id = shop.Place.Id;
            var keys = _cityProcurementPolicies.Values.Where(policy => policy.ShopId == id && policy.Target > 0)
                .Select(policy => (policy.Kind, policy.ProductId))
                .Concat(shop.Restock.Where(target => target.Value > 0 && PartCatalog.Exists(target.Key))
                    .Select(target => (PurchaseGoodsKind.Part, target.Key))).Distinct();
            bool employee = false, online = false;
            int shortage = 0;
            foreach (var (kind, product) in keys)
            {
                var policy = ProcurementPolicyFor(id, kind, product);
                if (policy.Target <= 0) continue;
                employee |= policy.Primary == ProcurementMethod.Employee;
                online |= policy.Primary == ProcurementMethod.Online1788;
                if (ProcurementCoverageFor(id, kind, product).FreeStock == 0) shortage++;
            }
            return (employee, online, shortage);
        }

        // Include active trips, but never expose a mutable trip to UI or receipt callers.
        public EmployeePurchaseTrip[] EmployeePurchaseRecords(string shopId, string employeeId = null) =>
            _employeeTrips.Concat(_employeePurchaseHistory)
                .Where(t => t.ShopId == shopId && (employeeId == null || t.EmployeeId == employeeId))
                .OrderByDescending(t => t.DepartAt).Select(t => t.Copy()).ToArray();

        public string EmployeePurchaseTripStatus(EmployeePurchaseTrip trip)
        {
            if (trip.Delivered && trip.Returned) return "已入库 · 员工已返店";
            if (trip.Delivered) return "已入库 · 员工返店中";
            if (trip.Returned) return "员工已返店 · 货物配送中";
            if (Clock.Now.TotalMinutes >= trip.BoughtAt) return "已采购 · 返程 / 配送中";
            var leg = (trip.TravelLegs ?? Array.Empty<CityRoute>()).LastOrDefault(r => r!=null && r.DepartMinute<=Clock.Now.TotalMinutes);
            return leg!=null&&Clock.Now.TotalMinutes>=leg.ArriveMinute&&leg.TargetPlaceId!=trip.ShopId ? "店内采购中" : "前往货源途中";
        }
    }
}
