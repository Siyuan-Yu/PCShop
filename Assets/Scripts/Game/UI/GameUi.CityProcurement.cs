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
        private string _procurementShopId, _procurementSearch = "";
        private bool _procurementOrders, _procurementAgreement;
        public void OpenCityProcurement() => OpenCityProcurementFor(S.CurrentShop);
        public void OpenCityProcurementFor(OwnedShop target)
        {
            _procurementShopId = target?.Rented == true ? target.Place.Id : S.ShopAt(_procurementShopId)?.Place.Id ?? S.MyShops.FirstOrDefault()?.Place.Id;
            OpenWorkspace("procurement", "main", "商铺采购与补货", 1160, 840, w =>
            {
                GuideHint(w.Content,QuestTarget.Restock,w.Rebuild);
                var shop = S.ShopAt(_procurementShopId);
                var tabs = Ui.Row(w.Content, 42);
                Ui.Size(Ui.Button(tabs, "商品与补货", () => { _procurementOrders = false; _procurementAgreement = false; w.Rebuild(); }, 18, !_procurementOrders && !_procurementAgreement ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                Ui.Size(Ui.Button(tabs, "在途与订单", () => { _procurementOrders = true; _procurementAgreement = false; w.Rebuild(); }, 18, _procurementOrders && !_procurementAgreement ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                Ui.Size(Ui.Button(tabs, "直供协议与账款", () => { _procurementAgreement = true; w.Rebuild(); }, 18, _procurementAgreement ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                if (_procurementAgreement) { BuildDirectSupply(w); return; }
                HelpLink(w.Content, "采购与补货说明", "先选收货店和商品，可直接网购，也可安排进货员或使用已有供货协议。商品目标决定补到多少，执行人负责实际采购；渠道的解锁、每日配额、运费和路线照常计算。认领会预留缺口和预算，不会立即下单。");
                Para(w.Content, $"现金 {S.Ledger.Balance} · 共同保护额 {S.EffectiveRestockReserve} · 已安排预算 {S.ClaimedProcurementBudget}", 18, Ui.Accent);
                Ui.Size(Ui.Button(w.Content,"全店补货预算",OpenProcurementBudget,16),-1,32).name="ProcurementBudget";
                if (shop == null) { Para(w.Content, "经营进货需要合法使用的门店。普通生活购物仍可从手机或街上商户进入。", 18); return; }
                var destinations = S.MyShops.Where(s=>s.Rented).ToArray();
                Ui.Size(Ui.Choice(w.Content,destinations.Select(S.ShopTitle),Array.FindIndex(destinations,s=>s.Place.Id==_procurementShopId),
                    index=>{_procurementShopId=destinations[index].Place.Id;w.Rebuild();}),-1,38);
                Line(w.Content, "收货店：" + S.ShopTitle(shop), 19, Ui.Accent).name="ProcurementDestination";
                if (_procurementOrders) { BuildCityProcurementOrders(w); return; }
                Ui.Size(Ui.TextInput(w.Content, _procurementSearch, "搜索商品 / 分类", value => { _procurementSearch = value; w.RequestRebuild(); }), -1, 38);
                var list = Ui.Scroll(w.Content, 420);
                var catalog = S.EmployeePurchaseCatalog().Where(o => o.Kind == PurchaseGoodsKind.Part || o.Kind == PurchaseGoodsKind.Gift)
                    .GroupBy(o => ((int)o.Kind) + ":" + o.Id).Select(g => g.First());
                foreach (var entry in catalog.Where(o => string.IsNullOrWhiteSpace(_procurementSearch) || (o.Name + o.Category).IndexOf(_procurementSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var product = entry;
                    var coverage = S.ProcurementCoverageFor(_procurementShopId, product.Kind, product.Id);
                    var policy = S.ProcurementPolicyFor(_procurementShopId, product.Kind, product.Id);
                    string executor = policy.Primary == ProcurementMethod.Employee ? S.FindEmployee(policy.EmployeeId)?.Person.Name ?? "本店进货员（待选择）" : "网购";
                    var row = Ui.Row(list, 76);
                    Ui.Size(Ui.Label(row, product.Name + $"\n<size=14>可用库存 {coverage.FreeStock} · 在途 {coverage.InTransit} · 已安排 {coverage.Claimed} · 订单缺货 {coverage.ContractMissing}</size>", 17), -1, 76, 1);
                    Ui.Size(Ui.Button(row, "目标 " + coverage.Target + " / 下限 " + coverage.Lower + "\n" + executor, () => OpenCityProcurementPolicy(_procurementShopId, product.Kind, product.Id, product.Name, w), 15), 230, 66);
                    Ui.Size(Ui.Button(row, "比较来源 / 采购", () => OpenCityProcurementProduct(_procurementShopId, product.Kind, product.Id, product.Name, w), 17, Ui.ButtonHot), 190, 66);
                }
                Para(w.Content, "自动补货按可用库存＋未交付供给＋已安排覆盖量计算；订单材料优先分配，不在零售目标中重复使用。固定清单仍按固定数量，阈值模式才按缺口补齐。", 15, Ui.Dim);
                if (!string.IsNullOrEmpty(S.LastRestockNote)) Para(w.Content, S.LastRestockNote, 15, Ui.Accent);
            });
        }

        private void OpenProcurementBudget()
        {
            int days=S.RestockReserveDays;long cash=S.RestockReserveYuan,budget=S.RestockDailyBudgetYuan;
            OpenWindow("全店补货预算",760,590,w=>
            {
                Line(w.Content,"所有门店共用；保存后生效。",17,Ui.Accent);
                void Number(string title,string key,long value,long max,long step,Action<long> set)
                {
                    var row=Ui.Row(w.Content,40,6);Ui.Size(Ui.Label(row,title,17),140);
                    QuantityStepper(w,row,key,value,0,max,step,n=>set((long)n));
                }
                Number("预留账单天数","reserveDays",days,365,1,n=>days=(int)n);
                Number("现金底线（元）","reserveCash",cash,10000000,100,n=>cash=n);
                Number("每日预算（元）","dailyBudget",budget,10000000,100,n=>budget=n);
                Line(w.Content,"每日预算为0表示不限；账单预留仍然生效。",15,Ui.Dim);
                Ui.Size(Ui.Button(w.Content,"保存补货设置",()=>
                {
                    S.RestockReserveDays=days;S.RestockReserveYuan=cash;S.RestockDailyBudgetYuan=budget;S.RestockStoreId=null;
                    w.Close();RebuildWindows();
                },18,Ui.ButtonHot),-1,42).name="SaveProcurementBudget";
                DraftQuantityHints(w.Content);
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
                        if (shop == S.MainShop)
                        {
                            string creditBlock = S.DirectOrderBlocker(productId, count, true);
                            Ui.Size(Ui.Button(actions, "账期下单…", () => ConfirmAction("直供账期采购", S.ShopTitle(shop)+"："+name+" ×"+count+"\n总货款 "+total+"，现在支付30%，余款14天内结清。", "确认下单", () => { if(S.OrderDirect(productId,count,true)){w.Rebuild();parent?.Rebuild();} }), 16, Ui.ButtonBg, creditBlock == null), 180, 42);
                            if (creditBlock != null) Para(list, creditBlock, 15, Ui.Dim);
                        }
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
                Para(w.Content, "同店同商品只由一个主执行方式自动补货。旧员工固定清单与网购目标重叠时，默认由原进货员承担，网购不重复购买。", 17, Ui.Dim);
                var target = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(target, "常备目标", 17), 130);
                QuantityStepper(w, target, "city_target_" + productId, draft.Target, 0, kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100, 1, n => { draft.Target = (int)n; if (draft.Lower > draft.Target) draft.Lower = draft.Target; });
                var lower = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(lower, "补货下限", 17), 130);
                QuantityStepper(w, lower, "city_lower_" + productId, draft.Lower < 0 ? (int)(draft.Target * .4) : draft.Lower, 0, draft.Target, 1, n => draft.Lower = (int)n);
                if (kind == PurchaseGoodsKind.Part)
                    Ui.Size(Ui.Button(w.Content, "网购" + (draft.Primary == ProcurementMethod.Online1788 ? " ✓" : ""), () => { draft.Primary = ProcurementMethod.Online1788; draft.EmployeeId = null; w.Rebuild(); }, 18), -1, 44);
                foreach (var worker in S.Employees.Where(e => e.Shop?.Place.Id == shopId && e.Role == EmployeeRole.Buyer))
                {
                    var person = worker.Person;
                    Ui.Size(Ui.Button(w.Content, person.Name + "（员工）" + (draft.Primary == ProcurementMethod.Employee && draft.EmployeeId == person.Id ? " ✓" : ""), () =>
                    { draft.Primary = ProcurementMethod.Employee; draft.EmployeeId = person.Id; w.Rebuild(); }, 18), -1, 44);
                }
                Ui.Size(Ui.Button(w.Content, "不可用时使用备选网购：" + (draft.AllowFallback ? "开启" : "关闭"), () => { draft.AllowFallback = !draft.AllowFallback; w.Rebuild(); }, 17, Ui.ButtonBg, kind == PurchaseGoodsKind.Part), -1, 42);
                HelpLink(w.Content, "补货目标说明", "低于下限时补到目标库存，在途和已安排的数量计入覆盖。员工的固定清单仍按固定数量执行，阈值模式按缺口补齐；食品还受保质期限制。");
                Para(w.Content,S.ProcurementExecutorStatus(draft),16,Ui.Accent);
                Ui.Size(Ui.Button(w.Content, "保存补货责任", () => { if (S.SetProcurementPolicy(draft)) { w.Close(); parent?.Rebuild(); } }, 19, Ui.ButtonHot), -1, 48);
                DraftQuantityHints(w.Content);
            });
        }

        private void BuildCityProcurementOrders(Window w)
        {
            var list = Ui.Scroll(w.Content, 480);
            foreach (var claim in S.ProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.ShopId == _procurementShopId))
            {
                var c = claim; Line(list, "认领 " + c.Id + " · " + GameSession.PurchaseItemName(new PurchaseLine { Kind = c.Kind, PartId = c.ProductId }) + " ×" + c.Count, 18, Ui.Accent);
                Para(list, "预算 " + Money.FromCents(c.BudgetCents) + " · 尚未入在途；不重复计算覆盖。", 15, Ui.Dim);
                var bar = Ui.Row(list, 40);
                if (c.Method == ProcurementMethod.Online1788) Ui.Size(Ui.Button(bar, "确认执行", () => ConfirmAction("执行已认领采购", "按当前实际报价下单，认领将转为在途，保护额仍生效。", "确认下单", () => { S.ExecuteProcurementClaim(c.Id); w.Rebuild(); }), 17, Ui.ButtonHot), -1, 40, 1);
                Ui.Size(Ui.Button(bar, "取消认领", () => { S.CancelProcurementClaim(c.Id); w.Rebuild(); }, 17), -1, 40, 1);
            }
            foreach (var order in S.ProcurementOrders.Where(o => o.ShopId == _procurementShopId).Reverse().Take(60))
            {
                var o = order; var shop = S.ShopAt(o.ShopId);
                string state = o.State;
                if (o.OnlineOrderId > 0 && o.State == "在途" && !S.Online.Pending.Any(x => x.Id == o.OnlineOrderId)) state = "已交付";
                Line(list, o.Id + " · " + o.Channel + " · " + (shop == null ? "家里 / 原门店" : S.ShopTitle(shop)) + " · " + state, 17, Ui.Accent);
                Para(list, string.Join("、", o.Lines.Select(i => GameSession.PurchaseItemName(i) + "×" + i.Count)) + "\n已付 " + Money.FromCents(o.PaidCents) + " · 预计 " + new GameTimestamp(o.DeliveryAt), 15);
                if (o.OnlineOrderId > 0 && S.Online.Pending.Any(x => x.Id == o.OnlineOrderId))
                    Ui.Size(Ui.Button(list, "取消订单并退款…", () => ConfirmAction("取消采购", "尚未交付的网购订单将整单退货款及运费，同时释放在途仓位。", "确认取消", () => { S.CancelOnlineProcurementOrder(o.OnlineOrderId); w.Rebuild(); }), 16, Ui.Warn), -1, 38);
            }
            if (!S.ProcurementOrders.Any(o => o.ShopId == _procurementShopId)) Para(list, "本店暂无已确认采购订单。", 17, Ui.Dim);
            foreach (var run in S.Couriers.Where(r => r.ToShop && (r.ShopId ?? S.MainShop.Place.Id) == _procurementShopId))
            {
                Line(list, "线下货车 · " + run.FromName + " → " + S.CourierRunDestName(run), 17, Ui.Accent);
                Para(list, "已装 " + run.LoadKg.ToString("0.##") + " 千克 · 预计 " + new GameTimestamp(run.DueMinute)
                    + "\n" + string.Join("、", run.Items.Select(i => i.Id + "×" + i.Count)), 15);
            }
        }
    }
}
