using System.Linq;
using PCShopSell.Core.Catalog;
using System;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string BuildOrderReputationText(BuildOrder order)
        {
            if (order.ReputationReward > 0) return $"按时交付 · 名望 +{order.ReputationReward:0.#}";
            double reward = S.EnterpriseReputationReward(S.EnterpriseContracts.FirstOrDefault(c => c.Id == order.ContractId));
            return reward > 0 ? $"整份合同按时交齐 · 名望 +{reward:0.#}" : "本单名望 +0";
        }

        private void OrderActions(Transform parent, BuildOrder order, System.Action refresh)
        {
            Line(parent, BuildOrderReputationText(order), 15, Ui.Good);
            Line(parent, $"补货参考成本 {S.BuildReplacementCost(order)} · 报价差额 {order.Price - S.BuildReplacementCost(order)}（未扣运费与工资）", 14, Ui.Dim);
            if (order.EmployeeMinutesLeft > 0)
            {
                string Remaining() => $"装机剩余 {order.EmployeeMinutesLeft} 个工作分钟";
                if (parent == C) CounterText(Remaining, 15, Ui.Accent);
                else Line(parent, Remaining(), 15, Ui.Accent);
            }
            var row = Ui.Row(parent, 36, 4);
            var shop = S.PlayerShop;
            Ui.Size(Ui.Button(row, "缺件采购清单", () => OpenOrderShopping(order), 15, Ui.ButtonBg, !order.PartsReserved), 160);
            Ui.Size(Ui.Button(row, "取消订单…", () => OpenWindow("取消订单", 520, 250, w =>
            {
                string consequence = order.ContractId == null
                    ? $"门店口碑最多降低 {ShopSimulation.CustomerBuildCancellationReputationLoss:0.#}。"
                    : "这台整机计入合同的未交付数量。";
                Para(w.Content, "取消后会释放预留配件。" + consequence);
                Ui.Size(Ui.Button(w.Content, "确认取消", () => { shop.CancelBuild(order.Id); w.Close(); refresh(); }, 17, Ui.ButtonDanger), -1, 40);
            }), 15, Ui.ButtonSecondary), 140);
        }

        private void OpenOrderShopping(BuildOrder order)
        {
            var shop = S.CurrentShop;
            var missing = S.UncoveredBuildProcurement(shop, order);
            OpenWindow("订单采购清单", 760, 550, w =>
            {
                Line(w.Content, $"订单 #{order.Id} · {S.Clock.OrderDeadlineText(order.DueAt.TotalMinutes)}", 18);
                foreach (var p in missing) ItemLabel(w.Content, p.Id, $"{p.Name} ×1");
                if (missing.Count == 0) Line(w.Content, order.PartsReserved ? "配件已备齐，可以开始装机。" : "缺件已有采购安排。", 18, Ui.Good);
                Ui.Size(Ui.Button(w.Content, "加入采购员定期清单", () => ChooseOrderBuyer(order, shop, w), 18, Ui.ButtonBg, missing.Count > 0), -1, 44);
                Para(w.Content, "加入后按采购员原有频率重复采购，点“完成”才保存；从明天开始，请留意订单时限。", 15, Ui.Dim);
                Para(w.Content, "线上次日 10:00 送达，另需装机时间；急单请去档口现买。清单补入网购购物车；请核对全部数量和报价，不会自动花钱。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "放入 网购 购物车", () =>
                {
                    string block = S.AppBlocker(PhoneApps.Shop);
                    if (block != null) { Toast(block); return; }
                    foreach (var p in missing) if (S.PendingOnlineCount(p.Id) == 0) _cart[p.Id] = System.Math.Max(1, _cart.TryGetValue(p.Id, out int n) ? n : 0);
                    _onlineTab = 5;
                    w.Close(); OpenOnlinePanel(shop);
                }, 18, Ui.ButtonBg, missing.Count > 0), -1, 42);
            });
        }

        private void ChooseOrderBuyer(BuildOrder order, OwnedShop shop, Window shopping)
        {
            if (S.CurrentShop != shop) { Toast("请回到订单所属的门店再安排采购。"); return; }
            var buyers = S.Employees.Where(row => row.Shop == shop && row.Role == EmployeeRole.Buyer
                && S.PlannedEmployeeRole(row) == EmployeeRole.Buyer
                && (string.IsNullOrEmpty(row.Person.TransferShopId) || row.Person.TransferShopId == shop.Place.Id)).ToArray();
            if (buyers.Length == 1) { AddOrderToBuyerDraft(order, shop, buyers[0].Person.Id, shopping); return; }
            OpenWindow("选择本店采购员", 640, 460, picker =>
            {
                if (buyers.Length == 0)
                {
                    Para(picker.Content, "这家店还没有可负责采购的员工。先到员工管理安排进货岗位；跨店或换岗次日生效。", 18);
                    Ui.Size(Ui.Button(picker.Content, "打开员工管理", () => { picker.Close(); OpenEmployeesPanel(); }, 18, Ui.ButtonHot), -1, 44);
                    return;
                }
                Para(picker.Content, "选择负责本店的采购员，下一步核对他的完整采购清单。", 18);
                foreach (var row in buyers)
                {
                    var buyer = row.Person;
                    Ui.Size(Ui.Button(picker.Content, buyer.Name + " · " + buyer.PurchasePlan.FrequencyText, () =>
                    { picker.Close(); AddOrderToBuyerDraft(order, shop, buyer.Id, shopping); }, 18), -1, 46);
                }
            });
        }

        private void AddOrderToBuyerDraft(BuildOrder order, OwnedShop shop, string employeeId, Window shopping)
        {
            var worker = S.FindEmployee(employeeId);
            if (worker == null || worker.Role != EmployeeRole.Buyer || worker.Shop != shop || S.CurrentShop != shop
                || !shop.Sim.Orders.Contains(order))
            { Toast("订单或员工安排已变化，请重新选择。"); return; }
            var missing = shop.Sim.MissingParts(order);
            if (missing.Count == 0) { Toast("这笔订单的配件已齐，不需要加入采购。"); return; }
            var draft = (_purchaseDrafts.TryGetValue(employeeId, out var previous) ? previous : worker.Person.PurchasePlan).Copy();
            S.PrepareEmployeePurchaseDraft(draft);
            var lines = draft.Items.ToList(); var catalog = S.EmployeePurchaseCatalog();
            foreach (var part in missing)
            {
                // Merge the outstanding deadline-ordered requirement, not one new line on each click.
                int needed = S.UncoveredBuildPartCount(shop, order, part.Id);
                var existing = lines.FirstOrDefault(line => line.Kind == PurchaseGoodsKind.Part && line.PartId == part.Id);
                if (existing != null) { existing.Count = Math.Max(existing.Count, needed); continue; }
                var offer = catalog.Where(item => item.Kind == PurchaseGoodsKind.Part && item.Id == part.Id)
                    .OrderBy(item => item.Price.Cents).FirstOrDefault();
                if (offer == null) { Toast("找不到“" + part.Name + "”的采购货源，请先自行补货。"); return; }
                lines.Add(offer.Line(needed));
            }
            draft.Items = lines.ToArray(); _purchaseDrafts[employeeId] = draft;
            shopping.Close(); OpenEmployeePurchase(employeeId, importedOrderId: order.Id);
        }
    }
}
