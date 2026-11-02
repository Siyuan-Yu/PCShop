using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<EmployeePurchaseTrip> _employeePurchaseHistory = new List<EmployeePurchaseTrip>();

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
