using System;
using System.Linq;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<Employee> _idleEmployees = new List<Employee>();
        public static int AssemblyMinutesOf(Employee person) => Math.Max(45, person.AssemblyMinutes - person.BuildsCompleted / 2);
        public static string EmployeeAbilities(Employee person) =>
            $"收银 {person.CheckoutMinutes} 分钟/单 · 装机 {AssemblyMinutesOf(person)} 分钟/台 · 采购 {person.PurchaseMinutes} 分钟/型号";

        public Employee RecruitEmployee(int index)
        {
            if (IsGameOver || !InPlayerShop || index < 0 || index >= _candidates.Count) return null;
            var person = _candidates[index];
            _candidates.RemoveAt(index);
            _idleEmployees.Add(person);
            EverHired = true;
            Say($"聘用了{person.Name}，日薪 {person.DailyWage}。请选择工作岗位和门店；待安排期间也按日发薪。");
            return person;
        }

        public EmployeeRole PlannedEmployeeRole(EmployeeAssignment row) =>
            row.Person.TransferRole >= 0 && row.Person.TransferRole <= (int)EmployeeRole.Buyer
                ? (EmployeeRole)row.Person.TransferRole : row.Role;

        public string EmployeeAssignmentBlocker(string id, string destination, EmployeeRole role)
        {
            var row = FindEmployee(id);
            if (IsGameOver || row == null) return "员工不存在或游戏已结束";
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) return "请选择有效岗位";
            if (role == EmployeeRole.Unassigned)
            {
                if (_employeeTrips.Any(t => t.EmployeeId == id)) return "本趟进货还没结束，等人和货回店再换岗";
                return row.Role == EmployeeRole.Unassigned ? "已经是待安排" : null;
            }
            var dest = ShopAt(destination);
            if (dest == null) return "请选择自己的店铺";
            if (dest == row.Shop && role == row.Role) return "已经在这家店做这项工作";
            var occupant = EmployeeAt(dest, role);
            if (occupant != null && occupant != row.Person) return "该店的" + RoleName(role) + "岗位已有员工，可先给对方换岗或设为待安排";
            if (Employees.Any(x => x.Person.Id != id && x.Person.TransferShopId == destination && PlannedEmployeeRole(x) == role))
                return "这个岗位已安排其他员工明天到岗";
            if ((row.Shop == null || row.Shop == dest) && _employeeTrips.Any(t => t.EmployeeId == id))
                return "本趟进货还没结束，等人和货回店再换岗";
            return null;
        }

        /// <summary>岗位是工作安排而非职业。首次安排/同店换岗即时生效，跨店的岗位和门店一起次日生效。</summary>
        public bool ArrangeEmployeeWork(string id, string destination, EmployeeRole role)
        {
            string why = EmployeeAssignmentBlocker(id, destination, role);
            if (why != null) return Fail(why + "。");
            var row = FindEmployee(id);
            var dest = role == EmployeeRole.Unassigned ? null : ShopAt(destination);
            if (row.Shop != null && dest != null && row.Shop != dest)
            {
                row.Person.TransferShopId = destination;
                row.Person.TransferRole = (int)role;
                row.Person.TransferDay = Clock.Today.TotalDays + 1;
                Say($"{row.Person.Name}今天仍在{ShopTitle(row.Shop)}做{RoleName(row.Role)}，明天到{ShopTitle(dest)}做{RoleName(role)}。");
                return true;
            }
            MoveEmployeeWork(row, dest, role);
            Say(role == EmployeeRole.Unassigned ? $"{row.Person.Name}已设为待安排，日薪不变。"
                : $"{row.Person.Name}已安排在{ShopTitle(dest)}做{RoleName(role)}，日薪不变。");
            return true;
        }

        private void MoveEmployeeWork(EmployeeAssignment row, OwnedShop dest, EmployeeRole role)
        {
            var person = row.Person;
            var source = row.Shop;
            if (source == null) _idleEmployees.Remove(person);
            else
            {
                if (row.Role == EmployeeRole.Cashier && source.EmployeeTrained) person.AssemblyTrained = true;
                PutEmployee(source, row.Role, null);
                if (row.Role == EmployeeRole.Cashier) source.EmployeeTrained = false;
                source.EmployeeBuilding = false;
                if (source.Assembler == null && !source.EmployeeTrained)
                    foreach (var order in source.Sim.Orders) order.EmployeeMinutesLeft = 0;
            }
            if (role == EmployeeRole.Unassigned) _idleEmployees.Add(person);
            else
            {
                PutEmployee(dest, role, person);
                if (role == EmployeeRole.Cashier)
                {
                    dest.EmployeeTrained = person.AssemblyTrained;
                    dest.Sim.Settings.EmployeeCheckoutMinutes = person.CheckoutMinutes;
                }
                if (role == EmployeeRole.Assembler || (role == EmployeeRole.Cashier && person.AssemblyTrained)) dest.AutoBuild = true;
            }
            person.TransferShopId = null;
            person.TransferRole = -1;
            person.TransferDay = -1;
        }
    }
}
