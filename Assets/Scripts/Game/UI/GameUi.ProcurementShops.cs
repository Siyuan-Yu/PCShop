using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 采购与补货两层结构：第一层选店（一张卡一眼看出补货/缺货/进货员），
    /// 第二层是这家店的补货清单——只列设了补货的商品，目标/下限直接在行内改。
    /// </summary>
    public sealed partial class GameUi
    {
        private sealed class ProcurementItem
        {
            public PurchaseOffer Offer;
            public ProcurementPolicy Policy;
            public ProcurementCoverage Coverage;
            public bool Listed => Coverage.Target > 0;
            public bool Short => Listed && Coverage.FreeStock <= 0;
        }

        private ProcurementItem[] ProcurementItems(string shopId) =>
            S.EmployeePurchaseCatalog().Where(o => o.Kind == PurchaseGoodsKind.Part || o.Kind == PurchaseGoodsKind.Gift)
                .GroupBy(o => ((int)o.Kind) + ":" + o.Id).Select(g => g.First())
                .Select(o => new ProcurementItem
                {
                    Offer = o,
                    Policy = S.ProcurementPolicyFor(shopId, o.Kind, o.Id),
                    Coverage = S.ProcurementCoverageFor(shopId, o.Kind, o.Id),
                }).ToArray();

        private EmployeeAssignment[] ProcurementBuyers(string shopId) =>
            S.Employees.Where(e => e.Shop?.Place.Id == shopId && e.Role == EmployeeRole.Buyer).ToArray();

        private string BuyerScheduleText(EmployeeAssignment buyer)
        {
            var plan = buyer.Person.PurchasePlan;
            return plan != null && plan.Daily ? plan.FrequencyText + " · " + S.EmployeePurchaseNextText(buyer.Person.Id) : "未启用定期采购";
        }

        private void BuildShopProcurement(Window w, RectTransform content, OwnedShop shop, ShopManagementView view)
        {
            string id = shop.Place.Id;
            var items = ProcurementItems(id);
            var listed = items.Where(item => item.Listed).ToArray();
            var buyers = ProcurementBuyers(id);

            Line(content, "进货员与最近到货", 19, Ui.Accent);
            var methods = S.ShopProcurementSummary(shop);
            if (buyers.Length == 0)
            {
                var assignment = Ui.Row(content, 44);
                Ui.Size(Ui.Label(assignment, BuyerText(buyers, methods, S.LatestShopPurchaseReceipt(id)), 15,
                    TextAnchor.MiddleLeft, methods.Employee ? Ui.Warn : Ui.Dim), 0, 42, 1);
                if (!S.IsOwnedVenue(shop.Place))
                    Ui.Size(Ui.Button(assignment, "管理员工", () => OpenManagementShopEmployees(shop), 15), 92, 32);
            }
            foreach (var buyer in buyers)
            {
                var row = Ui.Row(content, 48);
                Ui.Size(Ui.Label(row, buyer.Person.Name + " · " + S.EmployeeStatus(buyer) + "\n" + BuyerScheduleText(buyer), 15), 0, 46, 1);
                Ui.Size(Ui.Button(row, "安排进货", () => OpenEmployeePurchase(buyer.Person.Id, w), 15), 92, 32)
                    .name = "ProcurementArrange_" + buyer.Person.Id;
            }
            var latest = S.LatestShopPurchaseReceipt(id);
            var history = Ui.Row(content, 35);
            Ui.Size(Ui.Label(history, ArrivalText(latest) + (latest.HasValue ? " · " + latest.Value.Channel : ""), 16), 0, 34, 1);
            if (S.EmployeePurchaseRecords(id).Length > 0)
                Ui.Size(Ui.Button(history, "员工进货记录", () => OpenBuyerHistory(shop), 15), 112, 32);

            var filters = Ui.Row(content, 41);
            int shortCount = listed.Count(item => item.Short);
            Ui.Tab(filters, $"全部 {listed.Length}", () => { view.OnlyShort = false; w.Rebuild(); }, !view.OnlyShort, 82);
            Ui.Tab(filters, $"缺货 {shortCount}", () => { view.OnlyShort = true; w.Rebuild(); }, view.OnlyShort, 82);
            Ui.Size(Ui.TextInput(filters, view.Search, "搜补货商品", value => { view.Search = value; w.RequestRebuild(); }), 0, 36, 1);
            Ui.Size(Ui.Button(filters, "添加商品", () => OpenProcurementAdd(w, shop, items, buyers), 15), 94, 36).name = "ProcurementAdd";

            var columns = Ui.Row(content, 28);
            Ui.Size(Ui.Label(columns, "商品", 14, color: Ui.Dim), 0, 28, 1);
            foreach (string title in new[] { "库存", "下限", "目标" })
                Ui.Size(Ui.Label(columns, title, 14, TextAnchor.MiddleCenter, Ui.Dim), 50, 28);
            Ui.Size(Ui.Rect(columns, "Actions"), 62, 28);

            var visible = listed.Where(item => (!view.OnlyShort || item.Short)
                && (string.IsNullOrWhiteSpace(view.Search)
                    || (item.Offer.Name + item.Offer.Category).IndexOf(view.Search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            if (visible.Length == 0)
                Line(content, listed.Length == 0 ? "还没有设置补货商品。" : "没有符合条件的商品。", 16, Ui.Dim);

            foreach (var item in visible)
            {
                var row = Ui.Row(content, 44);
                var coverage = item.Coverage;
                Ui.Size(Ui.Label(row, item.Offer.Name, 16), 0, 44, 1);
                Ui.Size(Ui.Label(row, coverage.FreeStock.ToString(), 16, TextAnchor.MiddleCenter, item.Short ? Ui.Warn : Ui.TextColor), 50, 44);
                Ui.Size(Ui.Label(row, coverage.Lower.ToString(), 16, TextAnchor.MiddleCenter), 50, 44);
                Ui.Size(Ui.Label(row, coverage.Target.ToString(), 16, TextAnchor.MiddleCenter), 50, 44);
                Ui.Size(Ui.Button(row, "设置", () => OpenCityProcurementPolicy(id, item.Offer.Kind, item.Offer.Id, item.Offer.Name, w), 15), 62, 34);
            }
            AddProcurementCancellation(w, id, content);
        }

        /// <summary>读出策略、交给 edit 修改后保存；新加的补货默认交给本店第一位进货员。</summary>
        private bool EditProcurement(OwnedShop shop, PurchaseOffer offer, EmployeeAssignment[] buyers, Action<ProcurementPolicy> edit)
        {
            var policy = S.ProcurementPolicyFor(shop.Place.Id, offer.Kind, offer.Id);
            bool wasEmpty = policy.Target <= 0;
            edit(policy);
            if (policy.Target > 0)
            {
                if (wasEmpty) { policy.Primary = ProcurementMethod.Employee; policy.AllowFallback = false; }
                if (policy.Primary == ProcurementMethod.Employee && !buyers.Any(b => b.Person.Id == policy.EmployeeId))
                {
                    if (buyers.Length == 0) { Toast("先到员工管理安排本店的进货员。"); return false; }
                    policy.EmployeeId = buyers[0].Person.Id;
                }
            }
            return S.SetProcurementPolicy(policy);
        }

        private void OpenProcurementAdd(Window parent, OwnedShop shop, ProcurementItem[] items, EmployeeAssignment[] buyers)
        {
            string search = "";
            OpenWindow("添加补货商品 · " + S.ShopTitle(shop), 760, 660, w =>
            {
                Ui.Size(Ui.Label(w.Content, "选一件商品加进补货清单，默认目标 6、下限 2，加完可以在清单里调。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
                Ui.Size(Ui.TextInput(w.Content, search, "搜索商品 / 分类", value => { search = value; w.RequestRebuild(); }), -1, 38);
                var list = GoodsList(w.Content, 380);
                var candidates = items.Where(i => !i.Listed)
                    .Where(i => string.IsNullOrWhiteSpace(search) || (i.Offer.Name + i.Offer.Category).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                foreach (var candidate in candidates.Take(80))
                {
                    var pick = candidate;
                    var row = Ui.Row(list, 46, 8);
                    ItemPortrait(row, pick.Offer.Id, 38);
                    Ui.Size(Ui.Label(row, pick.Offer.Name + "  <size=14><color=#655E52>" + pick.Offer.Category + "</color></size>", 17, TextAnchor.MiddleLeft), -1, 40, 1);
                    Ui.Size(Ui.Button(row, "添加", () =>
                    {
                        int target = Math.Min(6, pick.Offer.Kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100);
                        if (EditProcurement(shop, pick.Offer, buyers, p => { p.Target = target; p.Lower = Math.Min(2, target); }))
                        { w.Close(); parent.Rebuild(); }
                    }, 16, Ui.ButtonHot), 90, 36).name = "ProcurementPick_" + pick.Offer.Kind + "_" + pick.Offer.Id;
                }
                if (candidates.Length > 80) Ui.Size(Ui.Label(list, $"还有 {candidates.Length - 80} 件，输入关键字缩小范围。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 28);
                if (candidates.Length == 0) Ui.Size(Ui.Label(list, "没有可添加的商品。", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
            }, scrollContent: false);
        }
    }
}
