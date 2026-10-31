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
        public bool EmployeeTrained => Employee != null && _focus.EmployeeTrained;
        public bool AutoBuildEnabled => _focus.AutoBuild;
        public void ToggleAutoBuild() => _focus.AutoBuild = !_focus.AutoBuild;
        public bool TrainEmployee()
        {
            if (!InPlayerShop || Employee == null || EmployeeTrained) return Fail("请在店里选择一位尚未培训的店员。");
            if (!Ledger.TryDebit(Clock.Today, "培训", Money.FromYuan(1200), "店员装机培训")) return Fail("培训需要 ¥1,200。");
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
            Employee worker = Assembler != null && Assembler.OnDuty(now) ? Assembler : EmployeeTrained && Employee.OnDuty(now) ? Employee : null;
            if (worker == null || !AutoBuildEnabled) return;
            bool dedicated = worker == Assembler && Employee != null && Employee.OnDuty(now);
            int workMinutes = AssemblyMinutesOf(worker);
            var order = PlayerShop.Orders.Where(o => o.Id != _manualBuildId && o.EmployeeMinutesLeft > 0).FirstOrDefault();
            if (order == null)
            {
                if (!dedicated && PlayerShop.Queue.Count > 0) return;
                order = PlayerShop.Orders.Where(o => o.Id != _manualBuildId && now.MinutesUntil(o.DueAt) > workMinutes && PlayerShop.MissingParts(o).Count == 0)
                    .OrderBy(o => o.DueAt.TotalMinutes).FirstOrDefault();
                if (order == null || !PlayerShop.ReserveBuild(order.Id)) return;
                order.EmployeeMinutesLeft = workMinutes;
            }
            // 服务优先：有人排队时先收银，装机进度保留。
            if (!dedicated && PlayerShop.Queue.Count > 0) return;
            _employeeBuilding = !dedicated;
            order.EmployeeMinutesLeft--;
            if (order.EmployeeMinutesLeft <= 0)
            {
                if (!PlayerShop.CompleteBuild(order.Id, now)) return;
                worker.BuildsCompleted++;
                Say($"{worker.Name}完成订单 #{order.Id}，收款 {order.Price}。");
                _employeeBuilding = false;
            }
        }

        public Money BuildReplacementCost(BuildOrder order)
        {
            var store = PickRestockStore(PartCatalog.BuildList(order.Tier));
            Money sum = Money.Zero;
            foreach (var part in PartCatalog.BuildList(order.Tier)) sum += store == null ? Market.PriceOf(part) : OnlinePrice(store, part);
            return sum;
        }
    }
}
