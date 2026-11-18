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
        public bool AutoBuildEnabled => _focus.AutoBuild;
        public void ToggleAutoBuild()
        {
            _focus.AutoBuild = !_focus.AutoBuild;
            if (!_focus.AutoBuild) _focus.WorkingAssemblers.Clear();
        }

        public bool EmployeeAssembling(OwnedShop shop, string employeeId) => shop.WorkingAssemblers.Contains(employeeId);

        private static void ClearUnstaffedAssembly(OwnedShop shop)
        {
            if (shop.Assembler != null) return;
            shop.EmployeeBuilding = false;
            shop.WorkingAssemblers.Clear();
            foreach (var order in shop.Sim.Orders) order.EmployeeMinutesLeft = 0;
        }

        private void UpdateEmployeeWork(GameTimestamp now)
        {
            _employeeBuilding = false;
            _focus.WorkingAssemblers.Clear();
            var workers = ShopStaff(_focus, EmployeeRole.Assembler).Where(e => CityEmployeeAvailable(e, _focus)).ToArray();
            if (workers.Length == 0 || !AutoBuildEnabled) return;
            bool dedicated = ShopStaff(_focus, EmployeeRole.Cashier).Any(e => CityEmployeeAvailable(e, _focus));
            var handled = new System.Collections.Generic.HashSet<int>();
            foreach (var worker in workers)
            {
                // 预订装机优先于兼任收银，普通顾客排队不能持续打断装机。
                int workMinutes = AssemblyMinutesOf(worker);
                var order = PlayerShop.Orders.FirstOrDefault(o => !handled.Contains(o.Id) && o.Id != _manualBuildId && o.EmployeeMinutesLeft > 0);
                if (order == null)
                {
                    order = PlayerShop.Orders.Where(o => !handled.Contains(o.Id) && o.Id != _manualBuildId && now.MinutesUntil(o.DueAt) > workMinutes && PlayerShop.MissingParts(o).Count == 0)
                        .OrderBy(o => o.DueAt.TotalMinutes).FirstOrDefault();
                    if (order == null || !PlayerShop.ReserveBuild(order.Id)) continue;
                    order.EmployeeMinutesLeft = workMinutes;
                }
                handled.Add(order.Id);
                _employeeBuilding |= !dedicated;
                order.EmployeeMinutesLeft--;
                if (order.EmployeeMinutesLeft > 0)
                    _focus.WorkingAssemblers.Add(worker.Id);
                else
                {
                    if (!PlayerShop.CompleteBuild(order.Id, now)) continue;
                    worker.BuildsCompleted++;
                    Say($"{worker.Name}完成订单 #{order.Id}，收款 {order.Price}。" + (order.ReputationReward > 0 ? $"名望 +{order.ReputationReward:0.#}。" : ""));
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
    }
}
