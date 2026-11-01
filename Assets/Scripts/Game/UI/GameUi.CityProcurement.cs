using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        /// <summary>All business purchasing begins with product/destination, then chooses an actual execution channel.</summary>
        public void OpenCityProcurement()
        {
            string shopId = S.MyShops.FirstOrDefault()?.Place.Id, search = "";
            bool orders = false;
            OpenWindow("经营采购 · 商品、来源与订单", 1120, 820, w =>
            {
                var shop = S.ShopAt(shopId);
                var tabs = Ui.Row(w.Content, 42);
                Ui.Size(Ui.Button(tabs, "选商品 / 补货责任", () => { orders = false; w.Rebuild(); }, 18, orders ? Ui.ButtonBg : Ui.ButtonHot), -1, 42, 1);
                Ui.Size(Ui.Button(tabs, "统一订单 / 在途 / 认领", () => { orders = true; w.Rebuild(); }, 18, orders ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                Para(w.Content, "普通网购：生活/家具临时零买；1788：经营配件批发；线下：看货谈条件，或安排进货员按真实路线代办。保留原手机与人脉解锁条件。", 16, Ui.Dim);
                Para(w.Content, $"现金 {S.Ledger.Balance} · 共同保护额 {S.EffectiveRestockReserve} · 已认领预算 {S.ClaimedProcurementBudget}", 18, Ui.Accent);
                if (orders) { BuildCityProcurementOrders(w); return; }
                if (shop == null) { Para(w.Content, "经营进货需要合法使用的门店。普通生活购物仍可从手机或街上商户进入。", 18); return; }
                var destinations = Ui.Row(w.Content, 38);
                foreach (var s in S.MyShops)
                {
                    var destination = s;
                    Ui.Size(Ui.Button(destinations, S.ShopTitle(s), () => { shopId = destination.Place.Id; w.Rebuild(); }, 16,
                        shopId == s.Place.Id ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
                }
                Ui.Size(Ui.TextInput(w.Content, search, "搜索商品 / 分类", value => { search = value; w.RequestRebuild(); }), -1, 38);
                var list = Ui.Scroll(w.Content, 420);
                var catalog = S.EmployeePurchaseCatalog().Where(o => o.Kind == PurchaseGoodsKind.Part || o.Kind == PurchaseGoodsKind.Gift)
                    .GroupBy(o => ((int)o.Kind) + ":" + o.Id).Select(g => g.First());
                foreach (var entry in catalog.Where(o => string.IsNullOrWhiteSpace(search) || (o.Name + o.Category).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var product = entry;
                    var coverage = S.ProcurementCoverageFor(shopId, product.Kind, product.Id);
                    var policy = S.ProcurementPolicyFor(shopId, product.Kind, product.Id);
                    string executor = policy.Primary == ProcurementMethod.Employee ? S.FindEmployee(policy.EmployeeId)?.Person.Name ?? "本店进货员（待选择）" : "1788";
                    var row = Ui.Row(list, 76);
                    Ui.Size(Ui.Label(row, product.Name + $"\n<size=14>自由现货 {coverage.FreeStock} · 在途 {coverage.InTransit} · 已认领 {coverage.Claimed} · 订单专用缺口 {coverage.ContractMissing}</size>", 17), -1, 76, 1);
                    Ui.Size(Ui.Button(row, "目标 " + coverage.Target + " / 下限 " + coverage.Lower + "\n" + executor, () => OpenCityProcurementPolicy(shopId, product.Kind, product.Id, product.Name, w), 15), 230, 66);
                    Ui.Size(Ui.Button(row, "比较来源 / 采购", () => OpenCityProcurementProduct(shopId, product.Kind, product.Id, product.Name, w), 17, Ui.ButtonHot), 190, 66);
                }
                Para(w.Content, "自动补货按自由现货＋未交付供给＋已认领覆盖量计算；订单材料优先分配，不在零售目标中重复使用。固定清单仍按固定数量，阈值模式才按缺口补齐。", 15, Ui.Dim);
                if (!string.IsNullOrEmpty(S.LastRestockNote)) Para(w.Content, S.LastRestockNote, 15, Ui.Accent);
            });
        }

        private void OpenCityProcurementProduct(string shopId, PurchaseGoodsKind kind, string productId, string name, Window parent)
        {
            int count = 1;
            OpenWindow("采购来源 · " + name, 1000, 760, w =>
            {
                var shop = S.ShopAt(shopId); if (shop == null) { Para(w.Content, "门店已不可用。", 18); return; }
                var coverage = S.ProcurementCoverageFor(shopId, kind, productId);
                Para(w.Content, S.ShopTitle(shop) + $" · 有效零售覆盖 {coverage.Effective}/{coverage.Target} · 尚未覆盖 {coverage.Uncovered}（含订单专用缺口）", 18, Ui.Accent);
                var quantity = Ui.Row(w.Content, 38); Ui.Size(Ui.Label(quantity, "本次数量", 17), 120);
                QuantityStepper(w, quantity, "unified_buy_" + productId, count, 1, 100, 1, n => count = (int)n);
                if (coverage.InTransit + coverage.Claimed > 0) Para(w.Content, "已有在途/认领供给。手动额外下单会增加库存，确认前请核对，不会自动替你再买一份。", 16, Ui.Warn);
                var list = Ui.Scroll(w.Content, 430);
                foreach (var source in S.ProcurementSources(kind, productId))
                {
                    var offer = source; Money total = offer.UnitPrice * count + offer.Shipping;
                    Line(list, offer.SupplierName + " · 单价 " + offer.UnitPrice + " · 本次 " + total, 18, Ui.Accent);
                    Para(list, offer.Conditions + (offer.Shipping.IsPositive ? " · 运费 " + offer.Shipping : " · 车费/运费按实际路线报价"), 15, Ui.Dim);
                    var actions = Ui.Row(list, 42);
                    if (offer.Method == ProcurementMethod.Online1788)
                    {
                        if (offer.Blocker != null) Para(list, offer.Blocker, 16, Ui.Warn);
                        Ui.Size(Ui.Button(actions, "确认网上下单…", () => ConfirmAction("确认经营采购", $"{S.ShopTitle(shop)}：{name} ×{count}\n商品和运费合计 {total}。次日10:00送到后仓。\n已有在途 {coverage.InTransit}，认领 {coverage.Claimed}。" +
                            (S.Ledger.Balance - total < S.EffectiveRestockReserve ? "\n本次将动用未来账单保护资金。" : ""), "确认下单", () =>
                            { if (S.PlaceUnifiedOnlinePurchase(shopId, offer.SupplierId, productId, count)) { w.Rebuild(); parent?.Rebuild(); } }), 17, Ui.ButtonHot, offer.Blocker == null), -1, 42, 1);
                        Ui.Size(Ui.Button(actions, "先认领缺口", () =>
                        {
                            string id = S.ClaimProcurementDemand(shopId, kind, productId, count, ProcurementMethod.Online1788, "online:" + offer.OfferId, offer.SupplierId);
                            Toast(id == null ? "没有足够未覆盖缺口，或可用资金不足。" : "已认领，预算和仓位已保留；在统一订单中确认执行或取消。"); w.Rebuild(); parent?.Rebuild();
                        }, 16, Ui.ButtonBg, offer.Blocker == null && count <= coverage.Uncovered), 210, 42);
                    }
                    else if (offer.Method == ProcurementMethod.DirectSupply)
                    {
                        Ui.Size(Ui.Button(actions, "确认协议直供…", () => ConfirmAction("直供现款采购", name + " ×" + count + "，合计 " + total + "。按原协议次日10:00送达。", "确认下单", () =>
                        { if (S.PlaceUnifiedDirectPurchase(shopId, productId, count)) { w.Rebuild(); parent?.Rebuild(); } }), 17, Ui.ButtonHot, count >= 3 && count <= 30), -1, 42, 1);
                    }
                    else
                    {
                        Ui.Size(Ui.Button(actions, "前往看货", () => { CloseModalWindows(); _ctrl.GoToPlace(S.City.GetPlace(offer.PlaceId)); }, 17), 180, 42);
                        var buyer = S.Employees.FirstOrDefault(e => e.Shop == shop && e.Role == EmployeeRole.Buyer);
                        Ui.Size(Ui.Button(actions, "加入进货员清单…", () => ConfirmAction("安排员工代采", name + " ×" + count + "加入" + buyer?.Person.Name + "的定期清单；保存后次日起执行，今天不扣款。固定清单会按周期重复购买。", "保存清单", () =>
                        { if (buyer != null && S.QueueUnifiedEmployeePurchase(buyer.Person.Id, kind, productId, offer.SupplierId, count)) { Toast("已合并清单，明天起按周期执行。"); w.Rebuild(); } }), 17, Ui.ButtonHot, buyer != null), -1, 42, 1);
                    }
                }
            });
        }

        private void OpenCityProcurementPolicy(string shopId, PurchaseGoodsKind kind, string productId, string title, Window parent)
        {
            var draft = S.ProcurementPolicyFor(shopId, kind, productId);
            OpenWindow("补货责任 · " + title, 760, 650, w =>
            {
                Para(w.Content, "同店同商品只由一个主执行方式自动补货。旧员工固定清单与1788目标重叠时，默认由原进货员承担，1788不重复购买。", 17, Ui.Dim);
                var target = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(target, "常备目标", 17), 130);
                QuantityStepper(w, target, "city_target_" + productId, draft.Target, 0, kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100, 1, n => { draft.Target = (int)n; if (draft.Lower > draft.Target) draft.Lower = draft.Target; });
                var lower = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(lower, "补货下限", 17), 130);
                QuantityStepper(w, lower, "city_lower_" + productId, draft.Lower < 0 ? (int)(draft.Target * .4) : draft.Lower, 0, draft.Target, 1, n => draft.Lower = (int)n);
                if (kind == PurchaseGoodsKind.Part)
                    Ui.Size(Ui.Button(w.Content, "1788" + (draft.Primary == ProcurementMethod.Online1788 ? " ✓" : ""), () => { draft.Primary = ProcurementMethod.Online1788; draft.EmployeeId = null; w.Rebuild(); }, 18), -1, 44);
                foreach (var worker in S.Employees.Where(e => e.Shop?.Place.Id == shopId && e.Role == EmployeeRole.Buyer))
                {
                    var person = worker.Person;
                    Ui.Size(Ui.Button(w.Content, person.Name + "（员工）" + (draft.Primary == ProcurementMethod.Employee && draft.EmployeeId == person.Id ? " ✓" : ""), () =>
                    { draft.Primary = ProcurementMethod.Employee; draft.EmployeeId = person.Id; w.Rebuild(); }, 18), -1, 44);
                }
                Ui.Size(Ui.Button(w.Content, "不可用时使用备选1788：" + (draft.AllowFallback ? "开启" : "关闭"), () => { draft.AllowFallback = !draft.AllowFallback; w.Rebuild(); }, 17, Ui.ButtonBg, kind == PurchaseGoodsKind.Part), -1, 42);
                Para(w.Content, "员工固定清单：每次按清单数量买。库存阈值：按这里的目标与下限买；可在员工采购页切换。食品阈值采购按保质期限制本趟数量，避免无限囤临期品。", 16, Ui.Dim);
                Para(w.Content,S.ProcurementExecutorStatus(draft),16,Ui.Accent);
                Ui.Size(Ui.Button(w.Content, "保存补货责任", () => { if (S.SetProcurementPolicy(draft)) { w.Close(); parent?.Rebuild(); } }, 19, Ui.ButtonHot), -1, 48);
            });
        }

        private void BuildCityProcurementOrders(Window w)
        {
            var list = Ui.Scroll(w.Content, 480);
            foreach (var claim in S.ProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed))
            {
                var c = claim; Line(list, "认领 " + c.Id + " · " + GameSession.PurchaseItemName(new PurchaseLine { Kind = c.Kind, PartId = c.ProductId }) + " ×" + c.Count, 18, Ui.Accent);
                Para(list, "预算 " + Money.FromCents(c.BudgetCents) + " · 尚未入在途；不重复计算覆盖。", 15, Ui.Dim);
                var bar = Ui.Row(list, 40);
                if (c.Method == ProcurementMethod.Online1788) Ui.Size(Ui.Button(bar, "确认执行", () => ConfirmAction("执行已认领采购", "按当前实际报价下单，认领将转为在途，保护额仍生效。", "确认下单", () => { S.ExecuteProcurementClaim(c.Id); w.Rebuild(); }), 17, Ui.ButtonHot), -1, 40, 1);
                Ui.Size(Ui.Button(bar, "取消认领", () => { S.CancelProcurementClaim(c.Id); w.Rebuild(); }, 17), -1, 40, 1);
            }
            foreach (var order in S.ProcurementOrders.Reverse().Take(60))
            {
                var o = order; var shop = S.ShopAt(o.ShopId);
                string state = o.State;
                if (o.OnlineOrderId > 0 && o.State == "在途" && !S.Online.Pending.Any(x => x.Id == o.OnlineOrderId)) state = "已交付";
                Line(list, o.Id + " · " + o.Channel + " · " + (shop == null ? "家里 / 原门店" : S.ShopTitle(shop)) + " · " + state, 17, Ui.Accent);
                Para(list, string.Join("、", o.Lines.Select(i => GameSession.PurchaseItemName(i) + "×" + i.Count)) + "\n已付 " + Money.FromCents(o.PaidCents) + " · 预计 " + new GameTimestamp(o.DeliveryAt), 15);
                if (o.OnlineOrderId > 0 && S.Online.Pending.Any(x => x.Id == o.OnlineOrderId))
                    Ui.Size(Ui.Button(list, "取消订单并退款…", () => ConfirmAction("取消采购", "尚未交付的1788订单将整单退货款及运费，同时释放在途仓位。", "确认取消", () => { S.CancelOnlineProcurementOrder(o.OnlineOrderId); w.Rebuild(); }), 16, Ui.Warn), -1, 38);
            }
            if (S.ProcurementOrders.Count == 0) Para(list, "暂无已确认采购订单。旧档现有在途会在迁移时保留并接入订单列表。", 17, Ui.Dim);
            foreach (var run in S.Couriers)
            {
                Line(list, "线下货车 · " + run.FromName + " → " + S.CourierRunDestName(run), 17, Ui.Accent);
                Para(list, "已装 " + run.LoadKg.ToString("0.##") + " 千克 · 预计 " + new GameTimestamp(run.DueMinute)
                    + "\n" + string.Join("、", run.Items.Select(i => i.Id + "×" + i.Count)), 15);
            }
        }
    }
}
