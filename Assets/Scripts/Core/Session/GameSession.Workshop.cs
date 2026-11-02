using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private bool _employeeBuilding
        {
            get => _focus.EmployeeBuilding;
            set => _focus.EmployeeBuilding = value;
        }

        private int _manualBuildId = -1;
        private OwnedShop _manualDisplayShop;
        public bool EmployeeTrained => Employee != null && _focus.EmployeeTrained;
        public bool AutoBuildEnabled => _focus.AutoBuild;
        public void ToggleAutoBuild() => _focus.AutoBuild = !_focus.AutoBuild;
        public bool TrainEmployee()
        {
            if (!InPlayerShop || Employee == null || EmployeeTrained) return Fail("请在店里选择一位尚未培训的店员。");
            if (!Ledger.TryDebit(Clock.Today, "培训", Money.FromYuan(1200), "店员装机培训")) return Fail("培训需要 ¥1,200。");
            SnapshotCurrentEmployeePay(Employee);
            Employee.DailyWageYuan += 60;
            _focus.EmployeeTrained = true;
            Employee.AssemblyTrained = true;
            _focus.AutoBuild = true;
            Say("培训完成：店员可以装机，日薪增加 ¥60。无排队顾客时优先赶最早到期的订单。");
            return true;
        }

        private void UpdateEmployeeWork(GameTimestamp now)
        {
            _employeeBuilding = false;
            var workers=ShopStaff(_focus,EmployeeRole.Assembler).Concat(ShopStaff(_focus,EmployeeRole.Cashier).Where(e=>e.AssemblyTrained))
                .Where(e=>CityEmployeeAvailable(e,_focus)).Distinct().ToArray();
            if (workers.Length==0 || !AutoBuildEnabled) return;
            var handled=new System.Collections.Generic.HashSet<int>();
            foreach(var worker in workers)
            {
            bool dedicated = ShopStaff(_focus,EmployeeRole.Assembler).Contains(worker) && ShopStaff(_focus,EmployeeRole.Cashier).Any(e=>CityEmployeeAvailable(e,_focus));
            int workMinutes = AssemblyMinutesOf(worker);
            var order = PlayerShop.Orders.Where(o => !handled.Contains(o.Id)&&o.Id != _manualBuildId && o.EmployeeMinutesLeft > 0).FirstOrDefault();
            if (order == null)
            {
                if (!dedicated && PlayerShop.Queue.Count > 0) continue;
                order = PlayerShop.Orders.Where(o => !handled.Contains(o.Id)&&o.Id != _manualBuildId && now.MinutesUntil(o.DueAt) > workMinutes && PlayerShop.MissingParts(o).Count == 0)
                    .OrderBy(o => o.DueAt.TotalMinutes).FirstOrDefault();
                if (order == null || !PlayerShop.ReserveBuild(order.Id)) continue;
                order.EmployeeMinutesLeft = workMinutes;
            }
            // 服务优先：有人排队时先收银，装机进度保留。
            if (!dedicated && PlayerShop.Queue.Count > 0) continue;
            handled.Add(order.Id);_employeeBuilding |= !dedicated;
            order.EmployeeMinutesLeft--;
            if (order.EmployeeMinutesLeft <= 0)
            {
                if (!PlayerShop.CompleteBuild(order.Id, now)) continue;
                worker.BuildsCompleted++;
                Say($"{worker.Name}完成订单 #{order.Id}，收款 {order.Price}。");
            }
            }
        }

        public Money BuildReplacementCost(BuildOrder order)
        {
            var store = PickRestockStore(PartCatalog.BuildList(order.Tier));
            Money sum = Money.Zero;
            foreach (var part in PartCatalog.BuildList(order.Tier)) sum += store == null ? Market.PriceOf(part) : OnlinePrice(store, part);
            return sum;
        }
        public bool TrainNamedEmployee(string id)
        {
            var row=FindEmployee(id);if(row?.Shop==null||row.Person.AssemblyTrained)return Fail("请选择尚未培训的在职员工。");
            if(IsDedicatedVenue(row.Shop.Place))return Fail("场馆值守无需装机培训。");
            if(!Ledger.TryDebit(Clock.Today,"培训",Money.FromYuan(1200),row.Person.Name+" 装机培训"))return Fail("培训需要 ¥1,200。");
            SnapshotCurrentEmployeePay(row.Person);row.Person.DailyWageYuan+=60;row.Person.AssemblyTrained=true;row.Shop.AutoBuild=true;
            if(row.Shop.Employee==row.Person)row.Shop.EmployeeTrained=true;
            return true;
        }
    }
}
