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
                HelpLink(w.Content, "采购与补货说明", "选门店后可以查看库存和在途数量，也可以给本店进货员设置补货目标。");
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
                    string restock = coverage.Target == 0 ? "设置补货" : $"目标 {coverage.Target} / 下限 {coverage.Lower}";
                    var row = Ui.Row(list, 76);
                    Ui.Size(Ui.Label(row, product.Name + $"\n<size=14>可用库存 {coverage.FreeStock} · 在途 {coverage.InTransit} · 已安排 {coverage.Claimed} · 订单缺货 {coverage.ContractMissing}</size>", 17), -1, 76, 1);
                    string assigned = policy.Primary == ProcurementMethod.Employee ? S.FindEmployee(policy.EmployeeId)?.Person.Name : null;
                    if (coverage.Target > 0 && assigned != null) restock += "\n" + assigned;
                    Ui.Size(Ui.Button(row, restock, () => OpenCityProcurementPolicy(shop.Place.Id, product.Kind, product.Id, product.Name, w), 16), 230, 60);
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

        private void OpenCityProcurementPolicy(string shopId, PurchaseGoodsKind kind, string productId, string title, Window parent)
        {
            var draft = S.ProcurementPolicyFor(shopId, kind, productId);
            bool oldAutomatic = draft.Target > 0 && (draft.Primary == ProcurementMethod.Online1788 || draft.AllowFallback);
            draft.Primary = ProcurementMethod.Employee;
            draft.AllowFallback = false;
            OpenWindow("补货设置 · " + title, 760, 570, w =>
            {
                var workers = S.Employees.Where(e => e.Shop?.Place.Id == shopId && e.Role == EmployeeRole.Buyer).ToArray();
                if (!workers.Any(worker => worker.Person.Id == draft.EmployeeId))
                    draft.EmployeeId = workers.FirstOrDefault()?.Person.Id;
                Para(w.Content, "库存低于下限时，由选中的进货员按库存阈值计划补到目标。", 17, Ui.Dim);
                if (oldAutomatic) Para(w.Content, "原自动补货设置仍在生效，可改由员工负责或清除补货目标。", 16, Ui.Warn);
                var target = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(target, "常备目标", 17), 130);
                QuantityStepper(w, target, "city_target_" + productId, draft.Target, 0, kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100, 1, n => { draft.Target = (int)n; if (draft.Lower > draft.Target) draft.Lower = draft.Target; });
                var lower = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(lower, "补货下限", 17), 130);
                QuantityStepper(w, lower, "city_lower_" + productId, draft.Lower < 0 ? (int)(draft.Target * .4) : draft.Lower, 0, draft.Target, 1, n => draft.Lower = (int)n);
                foreach (var worker in workers)
                {
                    var person = worker.Person;
                    Ui.Size(Ui.Button(w.Content, person.Name + "（进货员）" + (draft.EmployeeId == person.Id ? " ✓" : ""), () =>
                    { draft.EmployeeId = person.Id; w.Rebuild(); }, 18), -1, 44);
                }
                if (workers.Length == 0) Para(w.Content, "请先到员工管理安排本店进货员。", 16, Ui.Dim);
                else Para(w.Content, S.ProcurementExecutorStatus(draft), 16, Ui.Accent);
                void Save()
                {
                    if (S.SetProcurementPolicy(draft)) { w.Close(); parent?.Rebuild(); }
                }
                var actions = Ui.Row(w.Content, 46, 8);
                Ui.Size(Ui.Button(actions, "清除补货目标", () =>
                {
                    draft.Target = 0; draft.Lower = 0; Save();
                }, 16, Ui.ButtonBg), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "保存补货设置", Save, 18, Ui.ButtonHot, workers.Length > 0), -1, -1, 1);
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
