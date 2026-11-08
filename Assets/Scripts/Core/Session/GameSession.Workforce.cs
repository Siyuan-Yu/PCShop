using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum PurchaseFrequency { Daily, MondayWednesdayFriday, Wednesday }
    public enum EmployeeRole { Cashier, Assembler, Buyer, Unassigned, Salesperson, Chef, Waiter, Appraiser, PetKeeper }

    [Serializable]
    public sealed class PurchaseLine
    {
        public string PartId, SourceId;
        public PurchaseGoodsKind Kind;
        public int Count, ExpireDay;
        public bool BuyMaximum;
        public int[] ReservedByWeekday;
        public PurchaseLine Copy() => new PurchaseLine { PartId = PartId, SourceId = SourceId, Kind = Kind, Count = Count, ExpireDay = ExpireDay, BuyMaximum = BuyMaximum, ReservedByWeekday = ReservedByWeekday == null ? null : (int[])ReservedByWeekday.Clone() };
    }

    [Serializable]
    public sealed class EmployeePurchasePlan
    {
        public string SourceId;
        // Browsing preference only; each purchase line still owns its actual supplier.
        public string BrowseSourceId;
        public bool BrowseSourceSelected;
        public VehicleKind Transport = VehicleKind.Taxi;
        /// <summary>仅保留以读取旧存档。员工采购按当日实际费用结算，不使用手填预算。</summary>
        public long BudgetYuan = 3000;
        public bool Daily;
        public bool InventoryThreshold;
        public int LastDay = -1;
        public int FirstDay;
        public int IntervalDays = 1;
        public bool HasWeeklySchedule;
        public PurchaseFrequency Frequency;
        public PurchaseFrequency Schedule => HasWeeklySchedule ? (Enum.IsDefined(typeof(PurchaseFrequency),Frequency)?Frequency:PurchaseFrequency.Daily)
            : IntervalDays==3?PurchaseFrequency.MondayWednesdayFriday:PurchaseFrequency.Daily;
        public int EveryDays => Schedule==PurchaseFrequency.Daily?1:3; // Legacy readers only; execution uses weekdays.
        public string FrequencyText => Schedule==PurchaseFrequency.Daily?"每天一次":Schedule==PurchaseFrequency.MondayWednesdayFriday?"周一、三、五":"每周三";
        public bool ScheduledOn(int day) => day>=FirstDay && MatchesWeekday(day%7);
        public bool MatchesWeekday(int weekday) => Schedule==PurchaseFrequency.Daily || Schedule==PurchaseFrequency.MondayWednesdayFriday&&(weekday==0||weekday==2||weekday==4) || Schedule==PurchaseFrequency.Wednesday&&weekday==2;
        public int NextOnOrAfter(int day)
        {
            int next=Math.Max(day,Math.Max(FirstDay,LastDay+1));
            while(!MatchesWeekday(next%7))next++;
            return next;
        }
        public int NextPurchaseDay => NextOnOrAfter(FirstDay);
        public PurchaseLine[] Items = Array.Empty<PurchaseLine>();
        public EmployeePurchasePlan Copy() => new EmployeePurchasePlan
        {
            SourceId = SourceId, BrowseSourceId = BrowseSourceId, BrowseSourceSelected = BrowseSourceSelected,
            Transport = Transport, BudgetYuan = BudgetYuan, Daily = Daily, InventoryThreshold = InventoryThreshold, LastDay = LastDay, FirstDay = FirstDay, IntervalDays = EveryDays, HasWeeklySchedule = true, Frequency = Schedule,
            Items = (Items ?? Array.Empty<PurchaseLine>()).Where(x => x != null).Select(x => x.Copy()).ToArray()
        };
    }

    [Serializable]
    public sealed class EmployeePurchaseReceiptLine
    {
        public string SourceId, SourceName, ProductName;
        public int Count;
        public long UnitCents;
        public EmployeePurchaseReceiptLine Copy() => (EmployeePurchaseReceiptLine)MemberwiseClone();
    }

    [Serializable]
    public sealed class EmployeePurchaseTrip
    {
        public string EmployeeId, ShopId, EmployeeName, OrderId, VehicleId;
        public string SkippedNote;
        public VehicleKind Transport;
        public long BoughtAt, ReturnAt, DeliveryAt;
        public long DepartAt;
        public CityRoute[] TravelLegs;
        public long ChangeCents;
        public bool Returned, Delivered;
        public PurchaseLine[] Items;
        // Snapshot the paid quote: later market prices, plans and supplier names must not rewrite receipts.
        public bool CostsRecorded;
        public long GoodsCents, TravelCents, FreightCents;
        public EmployeePurchaseReceiptLine[] ReceiptLines;
        public EmployeePurchaseTrip Copy() => new EmployeePurchaseTrip
        {
            EmployeeId = EmployeeId, ShopId = ShopId, EmployeeName = EmployeeName, OrderId = OrderId, VehicleId=VehicleId, Transport = Transport, SkippedNote = SkippedNote,
            BoughtAt = BoughtAt, ReturnAt = ReturnAt, DeliveryAt = DeliveryAt, Returned = Returned, Delivered = Delivered, ChangeCents = ChangeCents,
            DepartAt=DepartAt,TravelLegs=TravelLegs?.Select(r=>r.Copy()).ToArray(),
            CostsRecorded=CostsRecorded,GoodsCents=GoodsCents,TravelCents=TravelCents,FreightCents=FreightCents,
            ReceiptLines=ReceiptLines?.Where(r=>r!=null).Select(r=>r.Copy()).ToArray(),
            Items = (Items ?? Array.Empty<PurchaseLine>()).Where(x => x != null).Select(x => x.Copy()).ToArray()
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
        public readonly List<string> Skipped = new List<string>();
        public Money Goods, Travel, Freight;
        public Money RequiredTotal, Budget;
        public double Kg;
        public int BuyMinutes, ReturnMinutes, DeliveryMinutes, Trucks;
        public Money Total => Goods + Travel + Freight;
        internal readonly List<(PurchaseOffer offer, PurchaseLine item)> Lines = new List<(PurchaseOffer, PurchaseLine)>();
    }

    public sealed partial class GameSession
    {
        private readonly List<EmployeePurchaseTrip> _employeeTrips = new List<EmployeePurchaseTrip>();
        public IReadOnlyList<EmployeePurchaseTrip> EmployeePurchaseTrips => _employeeTrips;

        public static string RoleName(EmployeeRole role) => role == EmployeeRole.Cashier ? "收银员" : role == EmployeeRole.Assembler ? "装机员" : role == EmployeeRole.Buyer ? "进货员" : role == EmployeeRole.Salesperson ? "销售员" : role == EmployeeRole.Chef ? "厨师" : role == EmployeeRole.Waiter ? "服务员" : role == EmployeeRole.Appraiser ? "收购师" : role == EmployeeRole.PetKeeper ? "饲养员" : "待安排";
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
                    foreach (EmployeeRole role in new[] { EmployeeRole.Cashier, EmployeeRole.Assembler, EmployeeRole.Buyer, EmployeeRole.Salesperson, EmployeeRole.Chef, EmployeeRole.Waiter, EmployeeRole.Appraiser, EmployeeRole.PetKeeper })
                    {
                        foreach(Employee e in ShopStaff(shop,role))
                        {
                        if (e == null) continue;
                        if (string.IsNullOrEmpty(e.Id)) e.Id = Guid.NewGuid().ToString("N");
                        if (role == EmployeeRole.Cashier && shop.Employee==e&&shop.EmployeeTrained) e.AssemblyTrained = true;
                        rows.Add(new EmployeeAssignment { Shop = shop, Person = e, Role = role });
                        }
                    }
                foreach (var e in _idleEmployees) rows.Add(new EmployeeAssignment { Person = e, Role = EmployeeRole.Unassigned });
                return rows;
            }
        }

        private static Employee EmployeeAt(OwnedShop s, EmployeeRole role) => role == EmployeeRole.Cashier ? s.Employee : role == EmployeeRole.Assembler ? s.Assembler : role == EmployeeRole.Buyer ? s.Buyer : role == EmployeeRole.Salesperson ? s.Salesperson : null;
        private static void PutEmployee(OwnedShop s, EmployeeRole role, Employee e)
        {
            if (role == EmployeeRole.Cashier) s.Employee = e;
            else if (role == EmployeeRole.Assembler) s.Assembler = e;
            else if (role == EmployeeRole.Buyer) s.Buyer = e;
            else if (role == EmployeeRole.Salesperson) s.Salesperson = e;
        }
        public EmployeeAssignment FindEmployee(string id) => Employees.FirstOrDefault(x => x.Person.Id == id);
        public EmployeePurchaseTrip EmployeeTrip(string id) => _employeeTrips.FirstOrDefault(x => x.EmployeeId == id && !x.Returned);
        public string EmployeeStatus(EmployeeAssignment row)
        {
            if (row.Role == EmployeeRole.Unassigned) return "待安排工作";
            var trip = EmployeeTrip(row.Person.Id);
            if (trip != null) return Clock.Now.TotalMinutes < trip.BoughtAt ? "去批发市场路上" : "返店途中";
            if(!row.Person.OnDuty(Clock.Now))return "休息中";
            if(CityEmployeeAvailable(row.Person,row.Shop))return "实际在岗";
            var person=CityPersonState(PersonIdOf(row.Person));
            return person?.ActivityId!=null?"场所活动中（店内无服务能力）":person?.Route!=null?"班内移动中，未到店":"班内未在店";
        }

        public string EmployeeTransferBlocker(string id, string destination)
        {
            var row = FindEmployee(id);
            return row == null ? "员工不存在" : EmployeeAssignmentBlocker(id, destination, row.Role);
        }

        public bool ScheduleEmployeeTransfer(string id, string destination)
        {
            var row = FindEmployee(id);
            return row != null && ArrangeEmployeeWork(id, destination, row.Role);
        }

        public void CancelEmployeeTransfer(string id)
        {
            var row = FindEmployee(id);
            if (row == null) return;
            row.Person.TransferShopId = null;
            row.Person.TransferRole = -1;
            row.Person.TransferDay = -1;
        }

        private void ApplyEmployeeTransfers(PCShopSell.Core.Time.GameDate today)
        {
            foreach (var row in Employees.ToList())
            {
                var e = row.Person;
                if (string.IsNullOrEmpty(e.TransferShopId) || e.TransferDay > today.TotalDays) continue;
                var dest = ShopAt(e.TransferShopId);
                var role = PlannedEmployeeRole(row);
                if (dest == null || StaffCapacityBlocker(dest,e.Id)!=null || _employeeTrips.Any(t => t.EmployeeId == e.Id))
                {
                    Say($"{e.Name}明日工作安排未生效：人数已满、店铺已退租或本趟进货未结束。已取消，请重新安排。");
                    CancelEmployeeTransfer(e.Id);
                    continue;
                }
                MoveEmployeeWork(row, dest, role);
                Say($"{e.Name}已到{ShopTitle(dest)}做{RoleName(role)}。");
            }
        }

        public bool HireBuyer(int index)
        {
            if (IsGameOver || !InPlayerShop || index < 0 || index >= _candidates.Count) return false;
            string capacity=StaffCapacityBlocker(_focus);if(capacity!=null)return Fail(capacity);
            if (!CandidateAvailable(_candidates[index])) return Fail("这位应聘者已经离开了。");
            var hired=_candidates[index];AddShopEmployee(_focus,EmployeeRole.Buyer,hired);_employeeHireShops[hired.Id]=_focus.Place.Id;
            EverHired = true;
            _candidates.Clear();
            Say($"{hired.Name}入职进货岗位，日薪 {hired.DailyWage}。点“员工”安排采购清单和交通方式。");
            return true;
        }

        public bool DismissEmployee(string id)
        {
            var row = FindEmployee(id);
            if (row == null || IsGameOver) return false;
            if (_employeeTrips.Any(t => t.EmployeeId == id)) return Fail("员工或货物还在路上，等本次进货结束再辞退。");
            if (row.Role == EmployeeRole.Unassigned)
            {
                _idleEmployees.Remove(row.Person);
                Say(row.Person.Name + "离职了。");
                return true;
            }
            WithShop(row.Shop, () =>
            {
                RemoveShopEmployee(row.Shop,row.Role,row.Person);_employeeHireShops.Remove(id);Say(row.Person.Name + "离职了。");
            });
            return true;
        }

        public void CycleEmployeeShift(string id)
        {
            var row = FindEmployee(id);
            if (row == null || EmployeeTrip(id) != null) { Fail("员工正在进货，返店后再调整班次。"); return; }
            var e = row.Person;
            SetEmployeeShift(e.Id,e.ShiftStart==540&&e.ShiftEnd==1020?780:540,e.ShiftStart==540&&e.ShiftEnd==1260?1020:1260);
        }

        public bool ConfigureEmployeePurchase(string id, EmployeePurchasePlan plan)
        {
            var row = FindEmployee(id);
            if (row == null || row.Role != EmployeeRole.Buyer || plan == null || !IsPurchaseTransport(plan.Transport)) return Fail("请选择进货员和有效的运输方式。");
            var copy = plan.Copy();
            PrepareEmployeePurchaseDraft(copy);
            if (copy.Items.Any(x => x.Count <= 0 || x.Count > 100 || EmployeePurchaseOffer(x) == null)) return Fail("清单中有无法采购的商品，单项数量须为 1–100。");
            if (copy.Items.GroupBy(PurchaseKey).Any(g => g.Count() > 1)) return Fail("同一货源的同种商品请合成一行。");
            if(copy.Daily)
            {
                string allocationError=AllocateSupplierPlan(id,copy);
                if(allocationError!=null)return Fail(allocationError);
            }
            copy.LastDay = row.Person.PurchasePlan?.LastDay ?? -1;
            row.Person.PurchasePlan = copy;
            if(copy.Daily)BindDefaultProcurementOwner(row,copy);
            return true;
        }

        public bool CompleteEmployeePurchase(string id, EmployeePurchasePlan plan)
        {
            if (plan == null) return false;
            var saved = plan.Copy();
            saved.Daily = saved.InventoryThreshold || (saved.Items?.Length ?? 0) > 0;
            saved.FirstDay = Clock.Today.TotalDays + 1;
            return ConfigureEmployeePurchase(id, saved);
        }

        public string EmployeePurchaseStorageText(string employeeId)
        {
            var row = FindEmployee(employeeId);
            if (row?.Shop == null) return "";
            double incoming = 0;
            WithShop(row.Shop, () => incoming = IncomingShopSpace);
            var stock = row.Shop.Sim.Stock;
            double available = Math.Max(0, stock.FreeSpace - incoming);
            double kg = Backpack.KgPerSpace;
            return $"店内后仓容量不限 · 已存 {stock.UsedSpace * kg:0.##} 千克 · 在途 {incoming * kg:0.##} 千克；采购仍受货款、运输承重和店家本型号余量约束";
        }

        private EmployeePurchaseQuote QuoteEmployeePurchaseExact(string id, EmployeePurchasePlan plan = null, bool planningOnly = false,
            IReadOnlyList<PurchaseOffer> catalog = null)
        {
            var q = new EmployeePurchaseQuote();
            var row = FindEmployee(id);
            if (row == null || row.Role != EmployeeRole.Buyer) { q.Error = "请选择进货员"; return q; }
            plan = plan ?? row.Person.PurchasePlan;
            plan = EffectiveEmployeePurchasePlan(row, plan, planningOnly, catalog);
            string blocker = planningOnly?null:IsGameOver ? "游戏已结束"
                : _employeeTrips.Any(t => t.EmployeeId == id) ? "上一趟员工或货物还没回来"
                : !row.Person.OnDuty(Clock.Now) ? "还没到该员工的上班时间"
                : !CityEmployeeAvailable(row.Person, row.Shop) ? "员工尚未实际到岗，或正在赴约/参加其他活动"
                : null;
            if (plan == null || (plan.Items?.Length ?? 0) == 0) q.Error = plan?.InventoryThreshold == true ? "当前没有未覆盖的阈值缺口（含现货、在途和已认领）" : row.Person.PurchasePlan.Items.Any(i=>i.BuyMaximum)?"该采购日暂无可用的店家份额或实货":"先在采购清单里添加商品";
            else if (!IsPurchaseTransport(plan.Transport)) q.Error = "请选择有效运输方式";
            if (q.Error != null) return q;
            bool truck = plan.Transport == VehicleKind.Walk || plan.Transport == VehicleKind.Taxi;
            if (!truck && FreeVehicleCount(plan.Transport) <= 0) blocker = blocker ?? "没有空闲的" + Vehicles.Get(plan.Transport).Name + "，到车行再买一台或换运输方式";
            var shopStand=StandOf(row.Shop.Place);
            var probe=new CityPersonState {PersonId=PersonIdOf(row.Person),X=shopStand.X,Y=shopStand.Y,LocationPlaceId=row.Shop.Place.Id};
            double route = 0, homeLeg = 0, space = 0;
            int buyMinutes = 0, travelMinutes=0;
            var selected = new List<(PurchaseOffer offer, PurchaseLine item)>();
            foreach (var item in plan.Items)
            {
                var offer = EmployeePurchaseOffer(item, plan.SourceId, catalog);
                if (item == null || item.Count <= 0 || item.Count > 100 || offer == null) { q.Error = "清单中有不可采购的商品或货源，请重新选择"; return q; }
                if (!planningOnly)
                {
                    if (offer.Kind == PurchaseGoodsKind.Part && item.Count > StallRemaining(offer.Source, PartCatalog.Get(offer.Id)))
                        blocker = blocker ?? offer.Source.Name + "的" + offer.Name + "超过今天剩余额度";
                    if (offer.Kind == PurchaseGoodsKind.Gift && item.Count > CitySupplierRemaining(offer.Source.Id, offer.Id))
                        blocker = blocker ?? offer.Source.Name + "的" + offer.Name + "超过本日供货配额";
                    if (item.Count > EmployeeSupplyAvailable(id,offer,Clock.Today.TotalDays)) blocker=blocker??offer.Name+"超过今日剩余库存";
                    if (item.Count < PurchaseMinimum(offer)) blocker = blocker ?? offer.Source.Name + "的"+offer.Name+"当前起订量为" + PurchaseMinimum(offer) + "件";
                }
                selected.Add((offer, item));
            }
            if (!planningOnly)
                foreach(var sameStock in selected.GroupBy(x=>PhysicalSupplyKey(x.offer)))
                    if(sameStock.Sum(x=>x.item.Count)>EmployeeSupplyAvailable(id,sameStock.First().offer,Clock.Today.TotalDays))blocker=blocker??sameStock.First().offer.Name+"的不同包装/清单行合计超过剩余供货";
            foreach (var stop in selected.GroupBy(x => x.offer.Source.Id))
            {
                var source = stop.First().offer.Source;
                var leg=PlanCityRoute(probe,source.Id,Clock.Now.TotalMinutes);
                if(leg==null){q.Error="去不了"+source.Name;return q;}
                double legMeters=leg.Meters.Sum();route+=legMeters;
                travelMinutes+=CityTravelTiming.NpcMinutes(legMeters,Vehicles.Get(plan.Transport));
                var at=StandOf(source);probe.X=at.X;probe.Y=at.Y;probe.LocationPlaceId=source.Id;
                int arrival = Clock.MinuteOfDay + travelMinutes + buyMinutes;
                blocker = blocker ?? PurchaseOpeningBlocker(source, arrival);
                foreach (var entry in stop)
                {
                    buyMinutes += EmployeePurchaseMinutes(row.Person);
                    q.Goods += entry.offer.Price * entry.item.Count;
                    q.Kg += entry.offer.UnitKg * entry.item.Count;
                    space += entry.offer.StockSpace * entry.item.Count;
                    q.Lines.Add(entry);
                }
                blocker = blocker ?? PurchaseOpeningBlocker(source, Clock.MinuteOfDay + travelMinutes + buyMinutes);
            }
            var back=PlanCityRoute(probe,row.Shop.Place.Id,Clock.Now.TotalMinutes);
            if(back==null){q.Error="找不到回店路线";return q;}
            homeLeg=back.Meters.Sum();
            if (!truck && q.Kg > Vehicles.Get(plan.Transport).CargoCapacity * Backpack.KgPerSpace + 1e-9) blocker = blocker ?? $"清单重 {q.Kg:0.##} 千克，{Vehicles.Get(plan.Transport).Name}装不下；减少数量或改叫货车";
            double incoming = 0;
            WithShop(row.Shop, () => incoming = IncomingShopSpace);
            incoming -= _cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.ShopId == row.Shop.Place.Id && c.OwnerId == id)
                .Sum(c => PurchaseStockSpace(new PurchaseLine { Kind = c.Kind, PartId = c.ProductId, Count = c.Count }));
            double freeStockSpace = Math.Max(0, row.Shop.Sim.Stock.FreeSpace - incoming);
            if (space > freeStockSpace + 1e-9) blocker = blocker ?? $"店内仓储空间不足（剩余 {freeStockSpace * Backpack.KgPerSpace:0.##} 千克，本趟需 {space * Backpack.KgPerSpace:0.##} 千克）";
            var spec = Vehicles.Get(plan.Transport);
            q.BuyMinutes = travelMinutes + buyMinutes;
            q.ReturnMinutes = q.BuyMinutes + CityTravelTiming.NpcMinutes(homeLeg, spec);
            if (!row.Person.TryShift(Clock.Now.TotalMinutes, out _, out long purchaseShiftEnd) || Clock.Now.TotalMinutes + q.ReturnMinutes > purchaseShiftEnd) blocker = blocker ?? "来不及在下班前返店，明天上班后再派出";
            q.DeliveryMinutes = q.ReturnMinutes;
            if (plan.Transport == VehicleKind.Taxi) q.Travel = Vehicles.TaxiFare(route) + Vehicles.TaxiFare(homeLeg);
            if (truck)
            {
                q.Trucks = Math.Max(1, (int)Math.Ceiling(q.Kg / CourierCapacityKg));
                q.Freight = (Vehicles.TaxiFare(homeLeg) + CourierHandling) * q.Trucks;
                q.DeliveryMinutes = q.BuyMinutes + CourierBaseMinutes + CityTravelTiming.NpcMinutes(homeLeg, Vehicles.Get(VehicleKind.Van));
            }
            // This is an explicit recurring employee shopping list. Future bill estimates
            // and unspent purchase claims are planning figures, not cash already spent.
            // Fit the trip (goods + actual travel/freight) to the real balance; partial
            // selection below continues through every later line when one cannot fit.
            if (Ledger.Balance < q.Total) blocker = blocker ?? $"现金不足（本趟需 {q.Total}，现有 {Ledger.Balance}）";
            q.Error = planningOnly?null:blocker;
            q.RequiredTotal = q.Total; q.Budget = q.Total;
            return q;
        }

        /// <summary>整份清单的预算展示，不按当前现金缩减商品。</summary>
        public EmployeePurchaseQuote EstimateEmployeePurchase(string id, EmployeePurchasePlan plan = null, IReadOnlyList<PurchaseOffer> catalog = null)
            => QuoteEmployeePurchaseExact(id, plan, true, catalog);

        public EmployeePurchaseQuote QuoteEmployeePurchase(string id, EmployeePurchasePlan plan = null)
        {
            var row = FindEmployee(id);
            plan = plan ?? row?.Person.PurchasePlan;
            if (row != null) plan = EffectiveEmployeePurchasePlan(row, plan);
            var full = QuoteEmployeePurchaseExact(id, plan);
            if (full.Error == null) return full;
            // These conditions prevent the employee from making any trip. Item-specific failures do not.
            if (row == null || row.Role != EmployeeRole.Buyer || plan == null || (plan.Items?.Length ?? 0) == 0
                || IsGameOver || _employeeTrips.Any(t => t.EmployeeId == id) || !row.Person.OnDuty(Clock.Now) || !CityEmployeeAvailable(row.Person, row.Shop)
                || !IsPurchaseTransport(plan.Transport)) return full;
            bool truck = plan.Transport == VehicleKind.Walk || plan.Transport == VehicleKind.Taxi;
            if (!truck && FreeVehicleCount(plan.Transport) <= 0) return full;
            var partial = plan.Copy();
            var selected = new List<PurchaseLine>();
            var skipped = new List<string>();
            EmployeePurchaseQuote best = null;
            // Visit the entire list in priority order. Reduce quantities to what fits the quota,
            // cash, vehicle and storage, and continue after a closed or unreachable source.
            foreach (var item in plan.Items)
            {
                var offer = EmployeePurchaseOffer(item, plan.SourceId);
                if (item == null || item.Count <= 0 || item.Count > 100 || offer == null)
                { skipped.Add((item == null ? "无效清单项" : PurchaseItemName(item)) + "：商品或货源不可用"); continue; }
                string label = offer.Source.Name + " · " + offer.Name;
                int minimum = Math.Max(1, PurchaseMinimum(offer));
                string reason = item.Count < minimum ? "当前起订量为"+minimum+"件" : "本次条件不满足";
                int lo = minimum - 1, hi = item.Count;
                EmployeePurchaseQuote accepted = null;
                while (lo < hi)
                {
                    int count = (lo + hi + 1) / 2;
                    var candidate = item.Copy(); candidate.Count = count;
                    partial.Items = selected.Concat(new[] { candidate }).ToArray();
                    var quote = QuoteEmployeePurchaseExact(id, partial);
                    if (quote.Error == null) { lo = count; accepted = quote; }
                    else { hi = count - 1; reason = quote.Error; }
                }
                if (accepted == null) lo = 0;
                if (lo > 0)
                {
                    var bought = item.Copy(); bought.Count = lo; selected.Add(bought);
                    partial.Items = selected.ToArray();
                    best = accepted ?? QuoteEmployeePurchaseExact(id, partial);
                }
                if (lo < item.Count)
                    skipped.Add(label + (lo > 0 ? $"：计划 {item.Count} 件，购入 {lo} 件；" : "：未购；") + reason);
            }
            if (best == null)
            {
                full.Skipped.AddRange(skipped);
                full.Error = "清单已逐项尝试，均未能购买：" + string.Join("；", skipped);
                return full;
            }
            best.RequiredTotal = full.Total;
            var available=Ledger.Balance;
            if(available<Money.Zero)available=Money.Zero;
            best.Budget = full.Total > available ? available : full.Total;
            best.Skipped.AddRange(skipped);
            return best;
        }

        public bool DispatchEmployeePurchase(string id, EmployeePurchasePlan executionPlan = null)
        {
            var row = FindEmployee(id);
            var savedPlan = row?.Person.PurchasePlan;
            int today = Clock.Today.TotalDays;
            if (savedPlan?.FirstDay > today) return Fail("采购安排明天开始，今天只保存清单。");
            if (savedPlan?.Daily == true && (!savedPlan.ScheduledOn(today) || !SupplierPlanHasDay(savedPlan, today)))
                return Fail("今天不在该采购计划的星期安排内。");
            if (savedPlan?.Daily == true && savedPlan.LastDay >= today) return Fail("该进货员今天已经执行过采购计划。");
            var q = QuoteEmployeePurchase(id, executionPlan);
            if (q.Error != null) return Fail(q.Error + "。");
            return DispatchEmployeePurchase(row, q);
        }

        private bool DispatchEmployeePurchase(EmployeeAssignment row, EmployeePurchaseQuote q)
        {
            string id = row.Person.Id;
            var plan = row.Person.PurchasePlan;
            string orderId = "employee-order:" + _cityProcurementSequence++;
            var transportMotor=plan.Transport==VehicleKind.Walk||plan.Transport==VehicleKind.Taxi?null:EmployeeTransportMotor(plan.Transport);
            if(plan.Transport!=VehicleKind.Walk&&plan.Transport!=VehicleKind.Taxi&&transportMotor==null)return Fail("当前没有可供员工使用的空闲车辆。");
            // 统一预付并锁定本趟报价与档口额度，途中不重复扣款。
            if (!Ledger.CanAfford(q.Total)) return false;
            for (int i = 0; i < q.Lines.Count; i++)
            {
                var line = q.Lines[i];
                if (!Ledger.TryDebit(Clock.Today, "进货", line.offer.Price * line.item.Count, row.Person.Name + "代采预付款",
                    LedgerAttribution.ForShop(row.Shop.Place.Id, orderId + ":goods:" + i, "采购").Product(line.item.PartId, line.item.Count, line.offer.Price.Cents)))
                    throw new InvalidOperationException("采购总额预检通过后扣款失败，未继续发货。");
            }
            if (q.Travel.IsPositive) Ledger.ForceDebit(Clock.Today, "交通", q.Travel, row.Person.Name + "进货往返打车", LedgerAttribution.ForShop(row.Shop.Place.Id, orderId + ":travel", "物流"));
            if (q.Freight.IsPositive) Ledger.ForceDebit(Clock.Today, "运费", q.Freight, row.Person.Name + "采购叫货车", LedgerAttribution.ForShop(row.Shop.Place.Id, orderId + ":freight", "物流"));
            Money change = Money.Zero;
            foreach (var line in q.Lines.Where(x => x.offer.Kind == PurchaseGoodsKind.Part))
                RecordStallPurchase(line.offer.Source, PartCatalog.Get(line.offer.Id), line.item.Count);
            foreach (var line in q.Lines.Where(x => x.offer.Kind == PurchaseGoodsKind.Gift))
                RecordCitySupplierPurchase(line.offer.Source.Id, line.offer.Id, line.item.Count);
            foreach(var line in q.Lines.Where(x=>x.offer.Kind!=PurchaseGoodsKind.Part&&x.offer.Kind!=PurchaseGoodsKind.Gift))RecordOtherSupply(line.offer,line.item.Count);
            long now = Clock.Now.TotalMinutes;
            _employeeTrips.Add(new EmployeePurchaseTrip
            {
                EmployeeId = id, EmployeeName = row.Person.Name, ShopId = row.Shop.Place.Id, OrderId = orderId, VehicleId=transportMotor?.Id, Transport = plan.Transport, ChangeCents = change.Cents,
                SkippedNote = string.Join("；", q.Skipped),
                CostsRecorded=true,GoodsCents=q.Goods.Cents,TravelCents=q.Travel.Cents,FreightCents=q.Freight.Cents,
                ReceiptLines=q.Lines.Select(x=>new EmployeePurchaseReceiptLine {SourceId=x.offer.Source.Id,SourceName=x.offer.Source.Name,
                    ProductName=x.offer.Name,Count=x.item.Count,UnitCents=x.offer.Price.Cents}).ToArray(),
                DepartAt=now,BoughtAt = now + q.BuyMinutes, ReturnAt = now + q.ReturnMinutes, DeliveryAt = now + q.DeliveryMinutes,
                Items = q.Lines.Select(x =>
                {
                    var item = x.item.Copy(); item.SourceId = x.offer.Source.Id;
                    if (item.Kind == PurchaseGoodsKind.Ingredient) item.ExpireDay = FoodBatch.NoExpiry;
                    if (item.Kind == PurchaseGoodsKind.Gift && Gifts.Get(item.PartId).ShelfDays > 0) item.ExpireDay = Clock.Today.TotalDays + Gifts.Get(item.PartId).ShelfDays;
                    return item;
                }).ToArray()
            });
            var dispatched=_employeeTrips.Last();
            var cityPerson=CityPersonState(PersonIdOf(row.Person));
            if(cityPerson!=null)ApplyCityPurchasePosition(dispatched,cityPerson,now);
            RegisterProcurementOrder(orderId, "员工代采", row.Shop.Place.Id, null, q.Total.Cents, now + q.DeliveryMinutes,
                q.Lines.Select(x => x.item), 0, id);
            HandOffProcurementClaims(id, row.Shop.Place.Id, q.Lines.Select(x => x.item), orderId);
            plan.LastDay = Clock.Today.TotalDays;
            row.Person.LastPurchaseNote = $"已出发：采购 {q.Lines.Count} 种商品、{q.Kg:0.##} 千克，本趟费用 {q.Total}，货约 {q.DeliveryMinutes} 分钟送到{ShopTitle(row.Shop)}。"
                + (q.Skipped.Count > 0 ? "\n部分未购：" + string.Join("；", q.Skipped) : "");
            Say(row.Person.Name + row.Person.LastPurchaseNote);
            return true;
        }

        private void UpdateEmployeePurchases(GameTimestamp to)
        {
            foreach (var trip in _employeeTrips.ToList())
            {
                if (!trip.Returned && to.TotalMinutes >= trip.ReturnAt)
                {
                    var returning=FindEmployee(trip.EmployeeId)?.Person;
                    var person=returning==null?null:CityPersonState(PersonIdOf(returning));
                    if(person!=null)ApplyCityPurchasePosition(trip,person,trip.ReturnAt);
                    trip.Returned = true;
                    if (trip.ChangeCents > 0) Ledger.Credit(to.Date, "采购找零", Money.FromCents(trip.ChangeCents), trip.EmployeeName + "退回采购余款");
                    if (Vehicles.Get(trip.Transport).Illegal && Random.Chance(.15))
                    {
                        Ledger.ForceDebit(to.Date, "罚款", PoliceSystem.Fine, trip.EmployeeName + "进货驾驶" + Vehicles.Get(trip.Transport).Name + "违规");
                        if (Random.Chance(PoliceSystem.ImpoundChance)) ImpoundVehicle(trip.Transport,trip.VehicleId);
                        Say(trip.EmployeeName + "进货途中被交警处罚，请查看车辆余量。");
                    }
                }
                if (!trip.Delivered && to.TotalMinutes >= trip.DeliveryAt)
                {
                    var dest = DeliveryShop(trip.ShopId);
                    WithShop(dest, () => { foreach (var line in trip.Items) Unpack(PurchaseCourierItem(line), true); });
                    TrackTempClocks();
                    trip.Delivered = true;
                    var person = FindEmployee(trip.EmployeeId)?.Person;
                    string note = $"{trip.EmployeeName}采购的货已送到{ShopTitle(dest)}：" + string.Join("、", trip.Items.Select(x => PurchaseItemName(x) + "×" + x.Count)) + "。";
                    if (!string.IsNullOrEmpty(trip.SkippedNote)) note += "\n部分未购：" + trip.SkippedNote;
                    if (person != null) person.LastPurchaseNote = note;
                    Show("进货送到了", note);
                }
                if (trip.Returned && trip.Delivered)
                {
                    _employeePurchaseHistory.Add(trip.Copy());
                    _employeeTrips.Remove(trip);
                }
            }
            foreach (var row in Employees.Where(x => x.Role == EmployeeRole.Buyer).ToList())
            {
                var plan = row.Person.PurchasePlan;
                if (plan == null || !plan.Daily || !plan.ScheduledOn(to.Date.TotalDays) || to.Date.TotalDays<=plan.LastDay || !row.Person.OnDuty(to) || !CityEmployeeAvailable(row.Person, row.Shop) || _employeeTrips.Any(t => t.EmployeeId == row.Person.Id) || !SupplierPlanHasDay(plan,to.Date.TotalDays)) continue;
                // 每天只尝试一次，避免现金或车辆不足时逐分钟刷失败消息。
                plan.LastDay = to.Date.TotalDays;
                var execution = AutomaticEmployeePurchasePlan(row);
                var q = QuoteEmployeePurchase(row.Person.Id, execution);
                if (q.Error == null) DispatchEmployeePurchase(row, q);
                else { row.Person.LastPurchaseNote = "今日自动进货未出发：" + q.Error; Say(row.Person.Name + "：" + row.Person.LastPurchaseNote); }
            }
            UpdateCityProcurement();
        }

        public int VehicleCount(VehicleKind kind) => _garage.Count(v => MotorCatalog.Get(v.ModelId).Kind == kind);
        public int BusyVehicleCount(VehicleKind kind) => _employeeTrips.Count(t => !t.Returned && t.Transport == kind);
        public int ImpoundedVehicleCount(VehicleKind kind) => _garage.Count(v => v.Impounded && MotorCatalog.Get(v.ModelId).Kind == kind);
        public int FreeVehicleCount(VehicleKind kind)
        {
            return _garage.Count(v=>MotorCatalog.Get(v.ModelId).Kind==kind&&MotorUsable(v)&&!(v.Commuter&&Vehicle==kind));
        }
        public string VehicleUsage(VehicleKind kind)
        {
            string users = string.Join("、", _employeeTrips.Where(t => !t.Returned && t.Transport == kind).Select(t => t.EmployeeName));
            return $"共 {VehicleCount(kind)} 台 · 空闲 {FreeVehicleCount(kind)} 台"
                + (Vehicle == kind ? " · 老板使用 1 台" : "") + (users.Length > 0 ? " · 员工使用：" + users : "")
                + (ImpoundedVehicleCount(kind) > 0 ? $" · 扣押 {ImpoundedVehicleCount(kind)} 台" : "");
        }
    }
}
