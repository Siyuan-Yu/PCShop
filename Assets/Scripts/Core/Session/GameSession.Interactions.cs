using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public IReadOnlyList<PartDef> EmployeePurchaseParts(string sourceId) => PartCatalog.All
            .Where(part => City.OfKind(PlaceKind.Wholesaler).Any(stall =>
                (string.IsNullOrEmpty(sourceId) || stall.Id == sourceId) && StallSells(stall, part))).ToArray();

        // Only edits the supplied draft. Never changes an active trip or saved plan implicitly.
        public int ChangePurchaseSource(EmployeePurchasePlan draft, string sourceId)
        {
            PrepareEmployeePurchaseDraft(draft);
            return 0; // Selecting a browse source must never delete purchases from other sources.
        }

        public bool CanEditShop => IsRenovating;

        public void TrainEmployeeAt(OwnedShop shop) => WithShop(shop, () => { TrainEmployee(); });
        public void ToggleAutoBuildAt(OwnedShop shop) => WithShop(shop, ToggleAutoBuild);

        public int PendingPurchaseCount(OwnedShop shop, string partId)
        {
            int count = 0;
            WithShop(shop, () => count = PendingOnlineCount(partId));
            return count;
        }

        public bool SetEmployeeShift(string id, int start, int end)
        {
            var row = FindEmployee(id);
            if (row == null || IsGameOver) return Fail("员工不存在或不可安排。");
            if(start<0||start>=1440||end<0||end>=1440||start==end) return Fail("请输入有效的HH:mm；班次必须大于0且小于24小时，上下班不能相同。");
            var e=row.Person;long now=Clock.Now.TotalMinutes;
            PrepareEmployeeShift(e,now);
            var schedule=e.Schedule;
            if(e.TryShift(now,out long activeStart,out long activeEnd))
            {
                PayrollDay(e,activeStart,activeEnd);
                schedule.PendingAt=activeEnd;schedule.PendingStart=start;schedule.PendingEnd=end;
                Say("已保存下次排班；当前班次时间、日薪和已发生工时保持不变。");
            }
            else {e.ShiftStart=start;e.ShiftEnd=end;schedule.PendingAt=-1;schedule.NextNotBefore=now;schedule.ActiveStart=-1;schedule.ActiveEnd=-1;}
            return true;
        }

        public EmployeeAssignment EmployeeInPosition(string shopId, EmployeeRole role) =>
            Employees.FirstOrDefault(x => x.Shop?.Place.Id == shopId && x.Role == role);

        // Validate both sides before moving either employee. No hire/fire or wage changes.
        public bool ReplaceEmployeeWork(string id, string destination, EmployeeRole role)
        {
            string why = EmployeeAssignmentBlocker(id, destination, role, true);
            if (why != null) return Fail(why);
            var occupant = EmployeeInPosition(destination, role);
            if (occupant != null && occupant.Person.Id != id)
                MoveEmployeeWork(occupant, null, EmployeeRole.Unassigned);
            return ArrangeEmployeeWork(id, destination, role);
        }

        public string RecruitAssignmentBlocker(string candidateId, string destination, EmployeeRole role)
        {
            if (IsGameOver || !InPlayerShop || !_candidates.Any(c => c.Id == candidateId && CandidateAvailable(c))) return "候选人已不可聘用";
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) return "请选择岗位";
            if (role == EmployeeRole.Unassigned) return null;
            var shop = ShopAt(destination);
            if (shop == null) return "请选择门店";
            if (EmployeeAt(shop, role) != null || Employees.Any(x => x.Person.TransferShopId == destination && PlannedEmployeeRole(x) == role))
                return "岗位已占用或已预留，请选择其他岗位";
            return null;
        }

        public bool RecruitAndAssign(string candidateId, string destination, EmployeeRole role)
        {
            var why = RecruitAssignmentBlocker(candidateId, destination, role);
            if (why != null) return Fail(why);
            int index = _candidates.FindIndex(c => c.Id == candidateId);
            var person = RecruitEmployee(index);
            if (role == EmployeeRole.Unassigned) return true;
            MoveEmployeeWork(FindEmployee(person.Id), ShopAt(destination), role);
            return true;
        }

        public bool CartWholeRowToBag(string key)
        {
            var line = _cart.Find(l => l.Key == key);
            if (line == null) return false;
            double fit = FitAmount(line.ByWeight, line.UnitKg, CartBagFreeKg, line.ByTruck);
            if (fit <= 0) return Fail("背包已装不下，剩余物品继续叫车送。");
            if (fit < line.ByTruck) Say($"背包只能再装 {fit:0.#}，剩余物品继续叫车送。");
            return CartToBag(key, fit);
        }
    }
}
