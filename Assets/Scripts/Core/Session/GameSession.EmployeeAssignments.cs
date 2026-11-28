using System;
using System.Linq;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<Employee> _idleEmployees = new List<Employee>();
        private static int WorkTaskMinutes(int baseline, int aptitude) =>
            Math.Max(1, (int)Math.Round(baseline * (1 + (50 - aptitude) * .014), MidpointRounding.AwayFromZero));

        public static int EmployeeCheckoutCycleMinutes(Employee person) => EmployeeTaskMinutes(WorkTaskMinutes(Math.Max(1, person.CheckoutMinutes), person.Dexterity) + 1,person);
        public static int AssemblyAptitude(Employee person) => (person.Dexterity + person.Intelligence) / 2;
        public static int AssemblyMinutesOf(Employee person) => Math.Max(45, EmployeeTaskMinutes(WorkTaskMinutes(person.AssemblyMinutes, AssemblyAptitude(person)) - person.BuildsCompleted / 2,person));
        public static double AssemblyPriceBonus(Employee person) => AssemblyPriceBonus(AssemblyAptitude(person));

        private static double AssemblyPriceBonus(int aptitude)
        {
            aptitude = Math.Max(60, Math.Min(100, aptitude));
            return Math.Round((aptitude - 60) * .75, MidpointRounding.AwayFromZero) / 1000;
        }

        public static int EmployeePurchaseMinutes(Employee person) => 2;
        public static double EmployeePurchaseDiscount(Employee person) => Math.Max(0, Math.Min(100, person.Intelligence)) * .001;
        public static PCShopSell.Core.Common.Money EmployeePurchasePrice(Employee person, PurchaseOffer offer) =>
            offer.Price.Scale(1 - EmployeePurchaseDiscount(person));
        public static int RestaurantEmployeeOperationMinutes(Employee person, EmployeeRole role) =>
            EmployeeTaskMinutes(WorkTaskMinutes(RestaurantOperationMinutes, person.Dexterity),person);
        public const double WaiterTipMaximumRate = .15;
        public static double WaiterTipChance(int charm) => .05 + Math.Max(0, Math.Min(100, charm)) * .0035;
        public static double WaiterTipUpperRate(int charm) => .05 + Math.Max(0, Math.Min(100, charm)) * .001;
        public static int EmployeeCharm(Employee person)
        {
            if(person==null)return 0;
            if(person.Charm>=0)return Math.Max(0,Math.Min(100,person.Charm));
            // Older employees receive a stable aptitude, without consuming simulation RNG.
            uint hash=2166136261;
            foreach(char ch in person.Id??person.Name??"employee")hash=unchecked((hash^ch)*16777619);
            return 20+(int)(hash%71);
        }
        public static double EmployeeSalesBonus(Employee person)=>EmployeeCharm(person)*.0025*EmployeeWorkRate(person);
        public double ShopSalesBonus(OwnedShop shop)=>Math.Min(.25,ShopStaff(shop,EmployeeRole.Salesperson)
            .Where(e=>CityEmployeeAvailable(e,shop)).Sum(EmployeeSalesBonus));

        public Employee RecruitEmployee(int index,string destination=null)
        {
            if (IsGameOver || !AtRecruitmentInterview || index < 0 || index >= _candidates.Count) return null;
            var hiringShop=ShopAt(destination)??_focus;
            string capacity=StaffCapacityBlocker(hiringShop);if(capacity!=null){Fail(capacity);return null;}
            var person = _candidates[index];
            if (!CandidateAvailable(person)) { Fail("这位应聘者已经离开了。"); return null; }
            EnsureEmployeeWellbeing(person,Clock.Now.TotalMinutes);
            _candidates.RemoveAt(index);
            _idleEmployees.Add(person);
            _employeeHireShops[person.Id]=hiringShop.Place.Id;
            EverHired = true;
            Say($"聘用了{person.Name}，日薪 {person.DailyWage}。请选择工作岗位和门店；待安排期间也按日发薪。");
            return person;
        }

        public EmployeeRole PlannedEmployeeRole(EmployeeAssignment row) =>
            row.Person.TransferRole >= 0 && Enum.IsDefined(typeof(EmployeeRole),row.Person.TransferRole) && row.Person.TransferRole != (int)EmployeeRole.Unassigned
                ? (EmployeeRole)row.Person.TransferRole : row.Role;

        private static (string kind, string name) EmployeeRequiredShopType(EmployeeRole role) => role switch
        {
            EmployeeRole.ModelPainter => ("models", "模型手办店"),
            EmployeeRole.Assembler => ("computer", "电脑店"),
            EmployeeRole.Chef or EmployeeRole.Waiter => ("restaurant", "餐馆"),
            EmployeeRole.Appraiser or EmployeeRole.Repairer => ("secondhand", "二手旧物店"),
            EmployeeRole.PetKeeper => (PCShopSell.Core.Home.PetShopCatalog.ShopKind, "宠物用品店"),
            _ => (null, null)
        };

        public static string EmployeeRoleShopHint(EmployeeRole role) => role switch
        {
            EmployeeRole.Cashier => "适用门店 · 餐馆以外的店铺，场馆为值守岗位",
            EmployeeRole.Buyer or EmployeeRole.Salesperson => "适用门店 · 各类自营店铺",
            EmployeeRole.Unassigned => "暂不安排门店",
            _ => "适用门店 · " + EmployeeRequiredShopType(role).name
        };

        public string EmployeeRoleShopBlocker(OwnedShop shop, EmployeeRole role)
        {
            var (kind, name) = EmployeeRequiredShopType(role);
            if (role == EmployeeRole.Cashier && shop.RetailKind == "restaurant")
                return "餐馆由服务员上菜、收桌和收款";
            if (kind != null && shop.RetailKind != kind) return $"请安排到{name}";
            if (IsDedicatedVenue(shop.Place) && role != EmployeeRole.Cashier && role != EmployeeRole.Unassigned)
                return "场馆仅可安排值守岗位";
            return null;
        }

        private void MoveIncompatibleWorkshopStaffToStandby(OwnedShop shop, bool notify)
        {
            foreach (var role in new[] { EmployeeRole.Assembler, EmployeeRole.ModelPainter })
            {
                string blocker = EmployeeRoleShopBlocker(shop, role);
                if (blocker == null) continue;
                foreach (var person in ShopStaff(shop, role).ToArray())
                {
                    var row = new EmployeeAssignment { Shop = shop, Person = person, Role = role };
                    _employeeHireShops[person.Id] = shop.Place.Id;
                    MoveEmployeeWork(row, null, EmployeeRole.Unassigned);
                    if (notify) Say($"{person.Name}已转为待安排。{blocker}");
                }
            }
        }

        public string EmployeeAssignmentBlocker(string id, string destination, EmployeeRole role, bool replace = false) =>
            EmployeeWorkAssignmentBlocker(id, destination, role, replace, companyTransfer: false);

        private string EmployeeWorkAssignmentBlocker(string id, string destination, EmployeeRole role, bool replace = false, bool companyTransfer = false)
        {
            if(EmployeeAuthorityBlocker(id) is string authority)return authority;
            if(ShopAuthorityBlocker(ShopAt(destination)) is string destinationAuthority)return destinationAuthority;
            var row = FindEmployee(id);
            if (IsGameOver || row == null) return "员工不存在或游戏已结束";
            if (!companyTransfer && (CompanyPostOf(id) != null || PendingCompanyPostOf(id) != null))
                return "请从公司管理窗口调整该员工岗位";
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) return "请选择有效岗位";
            if (role == EmployeeRole.Unassigned)
            {
                if (_employeeTrips.Any(t => t.EmployeeId == id)) return "本趟进货还没结束，等人和货回店再换岗";
                return row.Role == EmployeeRole.Unassigned ? "已经是待安排" : null;
            }
            var dest = ShopAt(destination);
            if (dest == null) return "请选择自己的店铺";
            string roleBlocker = EmployeeRoleShopBlocker(dest, role);
            if (roleBlocker != null) return roleBlocker;
            string capacity=dest==row.Shop?null:StaffCapacityBlocker(dest,id,replace,role);if(capacity!=null)return capacity;
            if (dest == row.Shop && role == row.Role) return "已经在这家店做这项工作";
            var occupant = EmployeeAt(dest, role);
            if (replace && occupant != null && _employeeTrips.Any(t => t.EmployeeId == occupant.Id)) return "原员工或货物还在路上，不能替换";
            if (replace && occupant != null && PendingCompanyPostOf(occupant.Id) != null) return "原员工已有待生效的公司岗位";
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
            if(source!=null&&dest!=null&&source!=dest&&person.OnDuty(Clock.Now))
            { if(person.Schedule==null)person.Schedule=new EmployeeSchedule();person.Schedule.WorkTravelTarget=dest.Place.Id; }
            if (source == null) _idleEmployees.Remove(person);
            else
            {
                RemoveShopEmployee(source, row.Role, person);
                source.EmployeeBuilding = false;
            }
            if (role == EmployeeRole.Unassigned) _idleEmployees.Add(person);
            else
            {
                AddShopEmployee(dest, role, person);_employeeHireShops[person.Id]=dest.Place.Id;
                if (role == EmployeeRole.Cashier&&dest.Employee==person)
                    dest.Sim.Settings.EmployeeCheckoutMinutes = EmployeeCheckoutCycleMinutes(person) - 1;
                if (role == EmployeeRole.Assembler) dest.AutoBuild = true;
            }
            person.TransferShopId = null;
            person.TransferRole = -1;
            person.TransferDay = -1;
        }
    }
}
