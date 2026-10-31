using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum EmployeeRole { Cashier, Assembler, Buyer }

    [Serializable]
    public sealed class PurchaseLine
    {
        public string PartId;
        public int Count;
        public PurchaseLine Copy() => new PurchaseLine { PartId = PartId, Count = Count };
    }

    [Serializable]
    public sealed class EmployeePurchasePlan
    {
        public string SourceId;
        public VehicleKind Transport = VehicleKind.Taxi;
        public long BudgetYuan = 3000;
        public bool Daily;
        public int LastDay = -1;
        public PurchaseLine[] Items = Array.Empty<PurchaseLine>();
        public EmployeePurchasePlan Copy() => new EmployeePurchasePlan
        {
            SourceId = SourceId, Transport = Transport, BudgetYuan = BudgetYuan, Daily = Daily, LastDay = LastDay,
            Items = (Items ?? Array.Empty<PurchaseLine>()).Select(x => x.Copy()).ToArray()
        };
    }

    [Serializable]
    public sealed class EmployeePurchaseTrip
    {
        public string EmployeeId, ShopId, EmployeeName;
        public VehicleKind Transport;
        public long BoughtAt, ReturnAt, DeliveryAt;
        public bool Returned, Delivered;
        public PurchaseLine[] Items;
        public EmployeePurchaseTrip Copy() => new EmployeePurchaseTrip
        {
            EmployeeId = EmployeeId, ShopId = ShopId, EmployeeName = EmployeeName, Transport = Transport,
            BoughtAt = BoughtAt, ReturnAt = ReturnAt, DeliveryAt = DeliveryAt, Returned = Returned, Delivered = Delivered,
            Items = (Items ?? Array.Empty<PurchaseLine>()).Select(x => x.Copy()).ToArray()
        };
    }

    public sealed class EmployeeAssignment
    {
        public OwnedShop Shop;
        public Employee Person;
        public EmployeeRole Role;
    }

    public sealed class EmployeePurchaseQuote
    {
        public string Error;
        public Money Goods, Travel, Freight;
        public double Kg;
        public int BuyMinutes, ReturnMinutes, DeliveryMinutes, Trucks;
        public Money Total => Goods + Travel + Freight;
        internal readonly List<(Place stall, PartDef part, int count)> Lines = new List<(Place, PartDef, int)>();
    }

    public sealed partial class GameSession
    {
        private readonly List<EmployeePurchaseTrip> _employeeTrips = new List<EmployeePurchaseTrip>();
        public IReadOnlyList<EmployeePurchaseTrip> EmployeePurchaseTrips => _employeeTrips;

        public static string RoleName(EmployeeRole role) => role == EmployeeRole.Cashier ? "收银员" : role == EmployeeRole.Assembler ? "装机员" : "进货员";
        public static string PurchaseTransportName(VehicleKind kind) =>
            kind == VehicleKind.Walk ? "步行去，叫货车送回" : kind == VehicleKind.Taxi ? "打车去，叫货车送回"
            : "开" + Vehicles.Get(kind).Name + "去，自己运回";
        public static bool IsPurchaseTransport(VehicleKind kind) => kind == VehicleKind.Walk || kind == VehicleKind.Taxi
            || kind == VehicleKind.EBike || kind == VehicleKind.Tricycle || kind == VehicleKind.Van;

        public IReadOnlyList<EmployeeAssignment> Employees
        {
            get
            {
                var rows = new List<EmployeeAssignment>();
                foreach (var shop in _shops.Where(x => x.Rented))
                    foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
                    {
                        Employee e = EmployeeAt(shop, role);
                        if (e == null) continue;
                        if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
                        rows.Add(new EmployeeAssignment { Shop = shop, Person = e, Role = role });
                    }
                return rows;
            }
        }

        private static Employee EmployeeAt(OwnedShop s, EmployeeRole role) => role == EmployeeRole.Cashier ? s.Employee : role == EmployeeRole.Assembler ? s.Assembler : s.Buyer;
        private static void PutEmployee(OwnedShop s, EmployeeRole role, Employee e)
        {
            if (role == EmployeeRole.Cashier) s.Employee = e;
            else if (role == EmployeeRole.Assembler) s.Assembler = e;
            else s.Buyer = e;
        }
        public EmployeeAssignment FindEmployee(string id) => Employees.FirstOrDefault(x => x.Person.Id == id);
        public EmployeePurchaseTrip EmployeeTrip(string id) => _employeeTrips.FirstOrDefault(x => x.EmployeeId == id && !x.Returned);
        public string EmployeeStatus(EmployeeAssignment row)
        {
            var trip = EmployeeTrip(row.Person.Id);
            if (trip != null) return Clock.Now.TotalMinutes < trip.BoughtAt ? "去批发市场路上" : "返店途中";
            return row.Person.OnDuty(Clock.Now) ? "在岗" : "休息中";
        }

        public string EmployeeTransferBlocker(string id, string destination)
        {
            var row = FindEmployee(id);
            var dest = ShopAt(destination);
            if (IsGameOver || row == null || dest == null) return "员工或店铺不存在";
            if (dest == row.Shop) return "已经在这家店";
            if (EmployeeAt(dest, row.Role) != null) return "目标店已有" + RoleName(row.Role);
            if (Employees.Any(x => x.Person.Id != id && x.Role == row.Role && x.Person.TransferShopId == destination)) return "这个岗位已安排其他员工明天到岗";
            return null;
        }

        public bool ScheduleEmployeeTransfer(string id, string destination)
        {
            string why = EmployeeTransferBlocker(id, destination);
            if (why != null) return Fail(why + "。");
            var row = FindEmployee(id);
            row.Person.TransferShopId = destination;
            row.Person.TransferDay = Clock.Today.TotalDays + 1;
            Say($"{row.Person.Name}今天仍在{ShopTitle(row.Shop)}，明天调到{ShopTitle(ShopAt(destination))}，岗位不变。");
            return true;
        }

        public void CancelEmployeeTransfer(string id)
        {
            var row = FindEmployee(id);
            if (row == null) return;
            row.Person.TransferShopId = null;
            row.Person.TransferDay = -1;
        }

        private void ApplyEmployeeTransfers(GameDate today)
        {
            foreach (var row in Employees.ToList())
            {
                var e = row.Person;
                if (string.IsNullOrEmpty(e.TransferShopId) || e.TransferDay > today.TotalDays) continue;
                var dest = ShopAt(e.TransferShopId);
                if (dest == null || EmployeeAt(dest, row.Role) != null || EmployeeTrip(e.Id) != null)
                {
                    Say($"{e.Name}调店未生效：目的店岗位已占用、店铺已退租或仍在出差。已取消安排，请重新选择。");
                    CancelEmployeeTransfer(e.Id);
                    continue;
                }
                bool trained = row.Shop.EmployeeTrained;
                PutEmployee(row.Shop, row.Role, null);
                PutEmployee(dest, row.Role, e);
                if (row.Role == EmployeeRole.Cashier)
                {
                    row.Shop.EmployeeTrained = false;
                    dest.EmployeeTrained = trained;
                    dest.Sim.Settings.EmployeeCheckoutMinutes = e.CheckoutMinutes;
                    if (trained) dest.AutoBuild = row.Shop.AutoBuild;
                }
                if (row.Role == EmployeeRole.Assembler) dest.AutoBuild = row.Shop.AutoBuild;
                if (row.Role != EmployeeRole.Buyer && row.Shop.Assembler == null && !row.Shop.EmployeeTrained)
                    foreach (var order in row.Shop.Sim.Orders) order.EmployeeMinutesLeft = 0;
                Say($"{e.Name}已调到{ShopTitle(dest)}，继续担任{RoleName(row.Role)}。");
                e.TransferShopId = null; e.TransferDay = -1;
            }
        }

        public bool HireBuyer(int index)
        {
            if (IsGameOver || !InPlayerShop || _focus.Buyer != null || index < 0 || index >= _candidates.Count) return false;
            _focus.Buyer = _candidates[index];
            EverHired = true;
            _candidates.Clear();
            Say($"{_focus.Buyer.Name}入职进货岗位，日薪 {_focus.Buyer.DailyWage}。点“员工”安排采购清单和交通方式。");
            return true;
        }

        public bool DismissEmployee(string id)
        {
            var row = FindEmployee(id);
            if (row == null || IsGameOver) return false;
            if (_employeeTrips.Any(t => t.EmployeeId == id)) return Fail("员工或货物还在路上，等本次进货结束再辞退。");
            WithShop(row.Shop, () =>
            {
                if (row.Role == EmployeeRole.Cashier) Fire();
                else if (row.Role == EmployeeRole.Assembler) FireAssembler();
                else { row.Shop.Buyer = null; Say(row.Person.Name + "离职了。"); }
            });
            return true;
        }

        public void CycleEmployeeShift(string id)
        {
            var row = FindEmployee(id);
            if (row == null || EmployeeTrip(id) != null) { Fail("员工正在进货，返店后再调整班次。"); return; }
            var e = row.Person;
            if (e.ShiftStart == 9 * 60 && e.ShiftEnd == 21 * 60) e.ShiftEnd = 17 * 60;
            else if (e.ShiftStart == 9 * 60) { e.ShiftStart = 13 * 60; e.ShiftEnd = 21 * 60; }
            else { e.ShiftStart = 9 * 60; e.ShiftEnd = 21 * 60; }
        }

        public bool ConfigureEmployeePurchase(string id, EmployeePurchasePlan plan)
        {
            var row = FindEmployee(id);
            if (row == null || row.Role != EmployeeRole.Buyer || plan == null || !IsPurchaseTransport(plan.Transport)) return Fail("请选择进货员和有效的运输方式。");
            if (plan.BudgetYuan <= 0 || plan.BudgetYuan > 10000000) return Fail("采购预算须在 ¥1–¥10,000,000 之间。");
            if (!string.IsNullOrEmpty(plan.SourceId) && !City.OfKind(PlaceKind.Wholesaler).Any(p => p.Id == plan.SourceId)) return Fail("请选择电子市场的档口。");
            if ((plan.Items ?? Array.Empty<PurchaseLine>()).Any(x => x == null || !PartCatalog.Exists(x.PartId) || x.Count <= 0 || x.Count > 100)) return Fail("每种配件每趟数量须在 1–100 件之间。");
            if ((plan.Items ?? Array.Empty<PurchaseLine>()).GroupBy(x => x.PartId).Any(g => g.Count() > 1)) return Fail("同一种配件请合并成一行。");
            var copy = plan.Copy();
            copy.LastDay = row.Person.PurchasePlan?.LastDay ?? -1;
            row.Person.PurchasePlan = copy;
            return true;
        }

        public EmployeePurchaseQuote QuoteEmployeePurchase(string id, EmployeePurchasePlan plan = null)
        {
            var q = new EmployeePurchaseQuote();
            var row = FindEmployee(id);
            if (row == null || row.Role != EmployeeRole.Buyer) { q.Error = "请选择进货员"; return q; }
            plan = plan ?? row.Person.PurchasePlan;
            string blocker = IsGameOver ? "游戏已结束"
                : _employeeTrips.Any(t => t.EmployeeId == id) ? "上一趟员工或货物还没回来"
                : !row.Person.OnDuty(Clock.Now) ? "还没到该员工的上班时间"
                : !IsWholesalerOpen ? "电子市场工作日 9:00–18:00 营业" : null;
            if (plan == null || (plan.Items?.Length ?? 0) == 0) q.Error = "先在采购清单里添加配件";
            else if (!IsPurchaseTransport(plan.Transport)) q.Error = "请选择有效运输方式";
            if (q.Error != null) return q;
            bool truck = plan.Transport == VehicleKind.Walk || plan.Transport == VehicleKind.Taxi;
            if (!truck && FreeVehicleCount(plan.Transport) <= 0) blocker = blocker ?? "没有空闲的" + Vehicles.Get(plan.Transport).Name + "，到车行再买一台或换运输方式";
            string node = row.Shop.Place.NodeId;
            double route = 0, homeLeg = 0, space = 0;
            int buyMinutes = 0;
            foreach (var item in plan.Items)
            {
                if (item == null || !PartCatalog.Exists(item.PartId) || item.Count <= 0 || item.Count > 100) { q.Error = "采购清单含无效数量或配件"; return q; }
                var part = PartCatalog.Get(item.PartId);
                var stall = City.OfKind(PlaceKind.Wholesaler).Where(p => (string.IsNullOrEmpty(plan.SourceId) || p.Id == plan.SourceId) && StallSells(p, part))
                    .OrderBy(p => StallPrice(p, part).Cents).FirstOrDefault();
                if (stall == null) { q.Error = part.Name + "在选定档口没有卖"; return q; }
                if (item.Count > StallRemaining(stall, part)) blocker = blocker ?? part.Name + "超过今天档口剩余额度";
                var path = Pathfinder.FindPath(City.Map, node, stall.NodeId);
                if (!path.Found) { q.Error = "去不了该档口"; return q; }
                route += path.Distance; node = stall.NodeId;
                buyMinutes += 2;
                q.Goods += StallPrice(stall, part) * item.Count;
                space += Stock.SpaceOf(part, true, item.Count);
                q.Lines.Add((stall, part, item.Count));
            }
            var back = Pathfinder.FindPath(City.Map, node, row.Shop.Place.NodeId);
            if (!back.Found) { q.Error = "找不到回店路线"; return q; }
            homeLeg = back.Distance;
            q.Kg = space * Backpack.KgPerSpace;
            if (!truck && q.Kg > Vehicles.Get(plan.Transport).CargoCapacity * Backpack.KgPerSpace + 1e-9) blocker = blocker ?? $"清单重 {q.Kg:0.#} 公斤，{Vehicles.Get(plan.Transport).Name}装不下；减少数量或改叫货车";
            double incoming = 0;
            WithShop(row.Shop, () => incoming = IncomingShopSpace);
            if (space > row.Shop.Sim.Stock.FreeSpace - incoming + 1e-9) blocker = blocker ?? "店里仓位不足（已预留在途货物）";
            var spec = Vehicles.Get(plan.Transport);
            q.BuyMinutes = Math.Max(1, (int)Math.Ceiling(route / spec.SpeedPerMinute)) + buyMinutes;
            q.ReturnMinutes = q.BuyMinutes + Math.Max(1, (int)Math.Ceiling(homeLeg / spec.SpeedPerMinute));
            if (Clock.MinuteOfDay + q.BuyMinutes >= WholesalerCloseMinute) blocker = blocker ?? "到档口时已打烊，明天开门后再派出";
            if (Clock.MinuteOfDay + q.ReturnMinutes >= row.Person.ShiftEnd) blocker = blocker ?? "来不及在下班前返店，明天上班后再派出";
            q.DeliveryMinutes = q.ReturnMinutes;
            if (plan.Transport == VehicleKind.Taxi) q.Travel = Vehicles.TaxiFare(route) + Vehicles.TaxiFare(homeLeg);
            if (truck)
            {
                q.Trucks = Math.Max(1, (int)Math.Ceiling(q.Kg / CourierCapacityKg));
                q.Freight = (Vehicles.TaxiFare(homeLeg) + CourierHandling) * q.Trucks;
                q.DeliveryMinutes = q.BuyMinutes + CourierBaseMinutes + (int)Math.Ceiling(homeLeg / CourierMetersPerMinute);
            }
            if (q.Total > Money.FromYuan(plan.BudgetYuan)) blocker = blocker ?? $"本趟合计 {q.Total}，超过预算 ¥{plan.BudgetYuan:N0}；不会买一半或自动加预算";
            else if (!Ledger.CanAfford(q.Total + EffectiveRestockReserve)) blocker = blocker ?? $"付款后须保留 {EffectiveRestockReserve} 供房租、工资和近期账款，当前现金不足";
            q.Error = blocker;
            return q;
        }

        public bool DispatchEmployeePurchase(string id)
        {
            var q = QuoteEmployeePurchase(id);
            if (q.Error != null) return Fail(q.Error + "。");
            var row = FindEmployee(id);
            var plan = row.Person.PurchasePlan;
            // 统一预付并锁定本趟报价与档口额度，途中不重复扣款。
            if (!Ledger.TryDebit(Clock.Today, "进货", q.Goods, row.Person.Name + "代采预付款")) return false;
            if (q.Travel.IsPositive) Ledger.ForceDebit(Clock.Today, "交通", q.Travel, row.Person.Name + "进货往返打车");
            if (q.Freight.IsPositive) Ledger.ForceDebit(Clock.Today, "运费", q.Freight, row.Person.Name + "采购叫货车");
            foreach (var line in q.Lines) RecordStallPurchase(line.stall, line.part, line.count);
            long now = Clock.Now.TotalMinutes;
            _employeeTrips.Add(new EmployeePurchaseTrip
            {
                EmployeeId = id, EmployeeName = row.Person.Name, ShopId = row.Shop.Place.Id, Transport = plan.Transport,
                BoughtAt = now + q.BuyMinutes, ReturnAt = now + q.ReturnMinutes, DeliveryAt = now + q.DeliveryMinutes,
                Items = plan.Items.Select(x => x.Copy()).ToArray()
            });
            plan.LastDay = Clock.Today.TotalDays;
            row.Person.LastPurchaseNote = $"已出发：{q.Kg:0.#} 公斤，预付 {q.Total}，货约 {q.DeliveryMinutes} 分钟送到{ShopTitle(row.Shop)}。";
            Say(row.Person.Name + row.Person.LastPurchaseNote);
            return true;
        }

        private void UpdateEmployeePurchases(GameTimestamp to)
        {
            foreach (var trip in _employeeTrips.ToList())
            {
                if (!trip.Returned && to.TotalMinutes >= trip.ReturnAt)
                {
                    trip.Returned = true;
                    if (trip.Transport == VehicleKind.Tricycle && Random.Chance(.15))
                    {
                        Ledger.ForceDebit(to.Date, "罚款", PoliceSystem.Fine, trip.EmployeeName + "进货三轮车违规");
                        if (Random.Chance(PoliceSystem.ImpoundChance)) _impoundedTricycles++;
                        Say(trip.EmployeeName + "进货途中被交警处罚，请查看车辆余量。");
                    }
                }
                if (!trip.Delivered && to.TotalMinutes >= trip.DeliveryAt)
                {
                    var dest = DeliveryShop(trip.ShopId);
                    foreach (var line in trip.Items) dest.Sim.Stock.ForceAdd(PartCatalog.Get(line.PartId), true, line.Count);
                    trip.Delivered = true;
                    var person = FindEmployee(trip.EmployeeId)?.Person;
                    string note = $"{trip.EmployeeName}采购的货已送到{ShopTitle(dest)}：" + string.Join("、", trip.Items.Select(x => PartCatalog.Get(x.PartId).Name + "×" + x.Count)) + "。";
                    if (person != null) person.LastPurchaseNote = note;
                    Show("进货送到了", note);
                }
                if (trip.Returned && trip.Delivered) _employeeTrips.Remove(trip);
            }
            foreach (var row in Employees.Where(x => x.Role == EmployeeRole.Buyer).ToList())
            {
                var plan = row.Person.PurchasePlan;
                if (plan == null || !plan.Daily || plan.LastDay == to.Date.TotalDays || !row.Person.OnDuty(to)) continue;
                // 每天只尝试一次，避免预算或车辆不足时逐分钟刷失败消息。
                plan.LastDay = to.Date.TotalDays;
                var q = QuoteEmployeePurchase(row.Person.Id);
                if (q.Error == null) DispatchEmployeePurchase(row.Person.Id);
                else { row.Person.LastPurchaseNote = "今日自动进货未出发：" + q.Error; Say(row.Person.Name + "：" + row.Person.LastPurchaseNote); }
            }
        }

        public int VehicleCount(VehicleKind kind) => _owned.Count(x => x == kind);
        public int BusyVehicleCount(VehicleKind kind) => _employeeTrips.Count(t => !t.Returned && t.Transport == kind);
        public int ImpoundedVehicleCount(VehicleKind kind) => kind == VehicleKind.Tricycle ? _impoundedTricycles : 0;
        public int FreeVehicleCount(VehicleKind kind) => Math.Max(0, VehicleCount(kind) - BusyVehicleCount(kind)
            - ImpoundedVehicleCount(kind) - (Vehicle == kind ? 1 : 0));
        public string VehicleUsage(VehicleKind kind)
        {
            string users = string.Join("、", _employeeTrips.Where(t => !t.Returned && t.Transport == kind).Select(t => t.EmployeeName));
            return $"共 {VehicleCount(kind)} 台 · 空闲 {FreeVehicleCount(kind)} 台"
                + (Vehicle == kind ? " · 老板使用 1 台" : "") + (users.Length > 0 ? " · 员工使用：" + users : "")
                + (ImpoundedVehicleCount(kind) > 0 ? $" · 扣押 {ImpoundedVehicleCount(kind)} 台" : "");
        }
    }
}
