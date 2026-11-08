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
        private void OrderActions(Transform parent, BuildOrder order, System.Action refresh)
        {
            Line(parent, $"补货参考成本 {S.BuildReplacementCost(order)} · 报价差额 {order.Price - S.BuildReplacementCost(order)}（未扣运费与工资）", 14, Ui.Dim);
            if (order.EmployeeMinutesLeft > 0)
                Line(parent, $"店员装机还需 {order.EmployeeMinutesLeft} 个空闲工作分钟；材料已预留。", 15, Ui.Accent);
            else if (order.PartsReserved) Line(parent, "材料已预留，不参与零售。", 15, Ui.Accent);
            var row = Ui.Row(parent, 36, 4);
            Ui.Size(Ui.Button(row, "预留配件", () => { if (!S.PlayerShop.ReserveBuild(order.Id)) Toast("配件不足，先生成采购清单。"); refresh(); }, 15, Ui.ButtonBg, !order.PartsReserved), 125);
            Ui.Size(Ui.Button(row, "缺件采购清单", () => OpenOrderShopping(order), 15), 160);
            Ui.Size(Ui.Button(row, "取消订单…", () => OpenWindow("取消订单", 520, 250, w =>
            {
                Para(w.Content, "取消后预留配件退回为散件，知名度降低 1。确定取消这笔订单？");
                Ui.Size(Ui.Button(w.Content, "确认取消", () => { S.PlayerShop.CancelBuild(order.Id); w.Close(); refresh(); }, 17, Ui.Warn), -1, 40);
            }), 15), 140);
        }

        private void OpenOrderShopping(BuildOrder order)
        {
            var shop = S.CurrentShop;
            var missing = S.UncoveredBuildProcurement(shop, order);
            OpenWindow("订单采购清单", 760, 550, w =>
            {
                Line(w.Content, $"订单 #{order.Id} · 剩余 {S.Clock.Now.MinutesUntil(order.DueAt) / 60} 小时", 18);
                foreach (var p in missing) Line(w.Content, $"{p.Name} ×1　送店在途 {S.PendingOnlineCount(p.Id)} 件", 16);
                if (missing.Count == 0) Line(w.Content, "配件已齐全，可预留后开始装机。", 18, Ui.Good);
                Ui.Size(Ui.Button(w.Content, "加入采购员定期清单", () => ChooseOrderBuyer(order, shop, w), 18, Ui.ButtonHot, missing.Count > 0), -1, 44);
                Para(w.Content, "加入后按采购员原有频率重复采购，点“完成”才保存；从明天开始，请留意订单时限。", 15, Ui.Dim);
                Para(w.Content, "线上次日 10:00 送达，另需装机时间；急单请去档口现买。清单补入网购购物车；请核对全部数量和报价，不会自动花钱。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "放入 网购 购物车", () =>
                {
                    string block = S.AppBlocker(PhoneApps.Shop);
                    if (block != null) { Toast(block); return; }
                    foreach (var p in missing) if (S.PendingOnlineCount(p.Id) == 0) _cart[p.Id] = System.Math.Max(1, _cart.TryGetValue(p.Id, out int n) ? n : 0);
                    _onlineTab = 5;
                    w.Close(); OpenOnlinePanel(shop);
                }, 18, Ui.ButtonHot, missing.Count > 0), -1, 42);
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
