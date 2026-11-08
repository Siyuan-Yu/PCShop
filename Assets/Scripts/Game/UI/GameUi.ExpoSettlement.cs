using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private BrandOrder[] ExpoResultOrders(string id) => S.BrandBusiness.Orders.Where(o => o.Source == "expo" && o.Id == id + ":lead").ToArray();

        private void OpenExpoSettlement(string id)
        {
            var e = S.BrandBusiness.Exhibits.FirstOrDefault(x => x.Id == id);
            if (e == null) return;
            if (_quarterlyExpoWindow != null && !_quarterlyExpoWindow.Closed) _quarterlyExpoWindow.Close();
            OpenWindow("本届展会结算", 960, 800, w =>
            {
                var body = Ui.Scroll(w.Content, 610);
                var peers = S.BrandBusiness.Exhibits.Where(x => x.Format == 2 && x.Day == e.Day && (x.Finished || x.Missed)).ToArray();
                if (peers.Length > 1)
                {
                    Line(body, "本届多品牌分别结算", 17, Ui.Dim);
                    Ui.Size(Ui.Choice(body, peers.Select(x => S.BrandBusiness.Brands.FirstOrDefault(b => b.Id == x.BrandId)?.Name ?? "品牌"),
                        Math.Max(0, Array.FindIndex(peers, x => x.Id == id)), choice => { w.Close(); OpenExpoSettlement(peers[choice].Id); }), -1, 38).name = "ExpoSettlementBrandPicker";
                }
                Line(body, (S.BrandBusiness.Brands.FirstOrDefault(b => b.Id == e.BrandId)?.Name ?? "品牌")
                    + " · " + new GameDate(e.Day) + "—" + new GameDate(e.Day + 3), 22, Ui.Accent);
                if (e.Missed) Para(body, e.LastFeedback ?? "本届未完成参展，保留已发生的销售与费用。", 18, Ui.Warn);
                var totals = Ui.Row(body, 60, 12);
                BrandMetric(totals, "现场销售收入", Money.FromCents(e.RevenueCents).ToString());
                BrandMetric(totals, "售出 / 备货", e.Products.Sum(p => p.Sold) + " / " + e.Products.Sum(p => p.Reserved) + "件");
                BrandMetric(totals, "退回来源门店", e.Products.Sum(p => p.Reserved - p.Sold) + "件");
                if (e.ResultRecorded)
                {
                    var rewards = BrandCard(body, "本届品牌提升 · 已生效");
                    var values = Ui.Row(rewards, 78, 16);
                    ExpoRewardMetric(values, "品牌知名度", e.InfluenceBefore, e.InfluenceAfter);
                    ExpoRewardMetric(values, "品牌口碑", e.ReputationBefore, e.ReputationAfter);
                }
                var goods = BrandCard(body, "商品明细");
                var finance = S.GetExpoFinance(e.Id);
                foreach (var product in e.Products)
                {
                    bool recorded = finance.ProductSales.TryGetValue(product.ProductId, out var revenue);
                    string income = recorded || product.Sold == 0 ? revenue.ToString() : "历史金额待核对";
                    Line(goods, S.BrandProductName(product.ProductId) + " · 售出" + product.Sold + "件 · 收入 " + income
                        + " · 退回" + (product.Reserved - product.Sold) + "件", 17);
                }
                var costs = BrandCard(body, "本届费用与收支");
                Line(costs, "商品销售收入  +" + Money.FromCents(e.RevenueCents) + "（已入账）", 20, Ui.Good);
                Line(costs, "已记录摊位费 " + finance.BoothPaid + " · 海报费 " + finance.PosterPaid, 18);
                Line(costs, "实际站台费 " + Money.FromCents(e.ExpoWagesCents) + "（" + e.AttendanceDays + "天）", 18);
                Line(costs, finance.FeesRecorded ? "现场收支 " + finance.CashNet : "费用记录不完整，暂不计算现场收支", 21, Ui.Accent);
                Line(costs, "收入减以上费用，未扣商品进货成本；门店日常收支另计。", 15, Ui.Dim);
                var orders = ExpoResultOrders(id);
                if (orders.Length == 0)
                {
                    Line(body, "本届未获得采购订单", 20, Ui.Accent);
                    Line(body, "现场评分 " + e.Score + " / 获单要求180；现场销售收入已结算。", 16, Ui.Dim);
                }
                else
                {
                    Line(body, "获得采购订单 · 需另备货交付", 20, Ui.Good);
                    foreach (var order in orders)
                        Line(body, S.BrandProductName(order.ProductId) + " ×" + order.Count + " · 截止 " + new GameDate(order.Due), 17);
                    Ui.Size(Ui.Button(body, "查看本届采购订单", () => { w.Close(); OpenExpoOrders(id); }, 18, Ui.ButtonHot), -1, 42);
                }
                var actions = Ui.Row(w.Content, 44, 10);
                Ui.Size(Ui.Button(actions, "关闭结算", w.Close, 18), -1, 44, 1);
                if (S.CurrentPlace?.Id == "life_expo")
                    Ui.Size(Ui.Button(actions, "离开展馆", () => { w.Close(); S.Leave(); }, 18, Ui.ButtonHot), -1, 44, 1);
            });
        }

        private void ExpoRewardMetric(Transform parent, string label, int before, int after)
        {
            var metric = Ui.Rect(parent, "ExpoReward"); Ui.Size(metric, 0, 78, 1); Ui.VList(metric, 2, 0);
            Line(metric, label + " · " + before + " → " + after, 16, Ui.Dim);
            var delta = Ui.Size(Ui.Label(metric, (after - before).ToString("+0;-0;0"), 32,
                color: after >= before ? Ui.Good : Ui.Warn), -1, 42);
            delta.fontStyle = FontStyle.Bold;
        }

        private void OpenExpoOrders(string id)
        {
            OpenWindow("本届展会 · 采购订单", 880, 690, w =>
            {
                var list = Ui.Scroll(w.Content, 500);
                var orders = ExpoResultOrders(id);
                if (orders.Length == 0) Para(list, "本届没有采购订单。现场销售已在展会结算中记录。", 19);
                foreach (var order in orders)
                {
                    var card = BrandCard(list, S.BrandProductName(order.ProductId) + " ×" + order.Count);
                    Line(card, "货款 " + Money.FromCents(order.UnitCents * order.Count) + " · 截止 " + new GameDate(order.Due), 19);
                    Line(card, "供货门店：" + S.City.GetPlace(order.ShopId).Name, 17);
                    Line(card, "现货 " + S.BrandProductCount(order.ShopId, order.ProductId) + "件 · 运费¥30 · 到货后结算货款", 16, Ui.Dim);
                    if (order.Done || order.Failed) Line(card, order.Done ? "已交付并结算" : "订单已逾期", 18, order.Done ? Ui.Good : Ui.Warn);
                    else if (order.Shipped) Line(card, "运输中 · 预计" + new GameTimestamp(order.Arrival) + "到货", 18, Ui.Good);
                    else
                    {
                        string orderId = order.Id;
                        bool enough = S.BrandProductCount(order.ShopId, order.ProductId) >= order.Count;
                        Ui.Size(Ui.Button(card, enough ? "安排发货 · ¥30运费" : "现货不足，请先补货", () =>
                        { S.FulfilBrandOrder(orderId); w.Rebuild(); }, 18, Ui.ButtonHot, enough), -1, 42);
                    }
                }
                Ui.Size(Ui.Button(w.Content, "返回本届结算", () => { w.Close(); OpenExpoSettlement(id); }, 18), -1, 42);
            });
        }
    }
}
