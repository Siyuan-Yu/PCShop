using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenCityProcurement()
        {
            var shops = S.MyShops.Where(shop => shop.Rented).ToArray();
            OpenCityProcurementFor(shops.Length == 1 ? shops[0] : null);
        }

        public void OpenCityProcurementFor(OwnedShop shop)
        {
            _managementShops.Bind(S);
            _managementShops.Select(shop?.Rented == true ? shop.Place.Id : null);
            _managementShops.Procurement = true;
            S.VisitQuestTarget(QuestTarget.Restock);
            OpenManagementPanel(false);
        }

        public void OpenProcurementAgreement()
        {
            S.VisitQuestTarget(QuestTarget.Direct);
            OpenWindow("直供协议与账款", 1000, 760, BuildDirectSupply);
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
            OpenWindow("补货设置 · " + title, 760, 570, w =>
            {
                var workers = S.Employees.Where(e => e.Shop?.Place.Id == shopId && e.Role == EmployeeRole.Buyer).ToArray();
                if (!workers.Any(worker => worker.Person.Id == draft.EmployeeId))
                    draft.EmployeeId = workers.FirstOrDefault()?.Person.Id;
                Para(w.Content, "库存低于下限时，按所设方式补到目标。", 17, Ui.Dim);
                if (oldAutomatic) Para(w.Content, "原自动补货设置仍在生效，可改由员工负责或清除补货目标。", 16, Ui.Warn);
                var target = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(target, "常备目标", 17), 130);
                PurchaseQuantity(w, target, "city_target_" + productId, draft.Target, 0, kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100,
                    n => { draft.Target = (int)n; if (draft.Lower > draft.Target) draft.Lower = draft.Target; });
                var lower = Ui.Row(w.Content, 42); Ui.Size(Ui.Label(lower, "补货下限", 17), 130);
                PurchaseQuantity(w, lower, "city_lower_" + productId, draft.Lower < 0 ? (int)(draft.Target * .4) : draft.Lower,
                    0, draft.Target, n => draft.Lower = (int)n);
                foreach (var worker in workers)
                {
                    var person = worker.Person;
                    var row = PersonSummary(w.Content, S.PersonIdOf(person), person.Name, "进货员", 50);
                    bool selected = draft.Primary == ProcurementMethod.Employee && draft.EmployeeId == person.Id;
                    Ui.Size(Ui.Button(row, selected ? "已选 ✓" : "选择", () =>
                    { draft.Primary = ProcurementMethod.Employee; draft.AllowFallback = false; draft.EmployeeId = person.Id; w.Rebuild(); }, 17, Ui.SelectedButton(selected)), 110, 38);
                }
                if (draft.Primary == ProcurementMethod.Employee && workers.Length == 0)
                    Para(w.Content, "请先到员工管理安排本店进货员。", 16, Ui.Dim);
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
                Ui.Size(Ui.Button(actions, "保存补货设置", Save, 18, Ui.ButtonHot, draft.Primary == ProcurementMethod.Online1788 || workers.Length > 0), -1, -1, 1);
                DraftQuantityHints(w.Content);
            });
        }

        private void AddProcurementCancellation(Window parent, string shopId, UnityEngine.RectTransform content)
        {
            var claims = S.ProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.ShopId == shopId).ToArray();
            var orderIds = S.ProcurementOrders.Where(o => o.ShopId == shopId && o.OnlineOrderId > 0)
                .Select(o => o.OnlineOrderId).ToHashSet();
            var orders = S.Online.Pending.Where(o => orderIds.Contains(o.Id) && o.DeliverAtMinute > S.Clock.Now.TotalMinutes).ToArray();
            if (claims.Length == 0 && orders.Length == 0) return;

            Ui.Size(Ui.Button(content, "取消采购…", () => OpenWindow("取消采购", 760, 540, w =>
            {
                foreach (var claim in claims)
                {
                    var item = new PurchaseLine { Kind = claim.Kind, PartId = claim.ProductId, Count = claim.Count };
                    var row = Ui.Row(w.Content, 42, 8);
                    Ui.Size(Ui.Label(row, $"认领 · {GameSession.PurchaseItemName(item)} ×{item.Count}", 16), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "取消认领", () =>
                    {
                        S.CancelProcurementClaim(claim.Id);
                        w.Close();
                        parent.Rebuild();
                    }, 16), 124, 36);
                }

                foreach (var order in orders)
                {
                    var items = string.Join("、", order.Lines.Select(line => PartCatalog.Get(line.PartId).Name + " ×" + line.Count));
                    var row = Ui.Row(w.Content, 54, 8);
                    Ui.Size(Ui.Label(row, $"网购 #{order.Id} · {items}", 16), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "取消并退款…", () => ConfirmAction("取消采购",
                        $"取消网购 #{order.Id}，退还货款和运费 {Money.FromCents(order.TotalCents)}。", "确认取消", () =>
                        {
                            S.CancelOnlineProcurementOrder(order.Id);
                            w.Close();
                            parent.Rebuild();
                        }), 16, Ui.Warn), 124, 36);
                }
            }), 16), -1, 38);
        }
    }
}
