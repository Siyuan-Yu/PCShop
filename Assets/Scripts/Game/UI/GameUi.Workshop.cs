using System.Linq;
using PCShopSell.Core.Catalog;
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
            var missing = S.MissingParts(order);
            OpenWindow("订单采购清单", 760, 550, w =>
            {
                Line(w.Content, $"订单 #{order.Id} · 剩余 {S.Clock.Now.MinutesUntil(order.DueAt) / 60} 小时", 18);
                foreach (var p in missing) Line(w.Content, $"{p.Name} ×1　送店在途 {S.PendingOnlineCount(p.Id)} 件", 16);
                if (missing.Count == 0) Line(w.Content, "配件已齐全，可预留后开始装机。", 18, Ui.Good);
                Para(w.Content, "线上次日 10:00 送达，另需装机时间；急单请去档口现买。清单补入现有购物车并切换到推荐供应商；请核对全部数量和新报价，不会自动花钱。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "放入 1788 购物车", () =>
                {
                    string block = S.AppBlocker(PhoneApps.Wholesale);
                    if (block != null) { Toast(block); return; }
                    var store = S.PickRestockStore(missing);
                    if (store == null) { Toast("没有已发现的供应商。"); return; }
                    _onlineStore = store.Id;
                    _onlineToHome = false;
                    foreach (var p in missing) if (S.PendingOnlineCount(p.Id) == 0) _cart[p.Id] = System.Math.Max(1, _cart.TryGetValue(p.Id, out int n) ? n : 0);
                    w.Close(); OpenWholesaleApp();
                }, 18, Ui.ButtonHot, missing.Count > 0), -1, 42);
            });
        }
    }
}
