using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 采购与补货两层结构：第一层选店（一张卡一眼看出补货/缺货/在途/进货员），
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
            public bool Transit => Listed && Coverage.InTransit + Coverage.Claimed > 0;
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

        private static void Chip(Transform parent, string text, Color color)
        {
            var chip = Ui.FlatPanel(parent, "Chip", Ui.FlatRow, null).rectTransform;
            Ui.Size(chip, 110, 28);
            var label = Ui.Label(chip, text, 15, TextAnchor.MiddleCenter, color);
            label.rectTransform.Fill(2);
        }

        private void ProcurementTabs(Window w, bool agreement)
        {
            var tabs = Ui.Row(w.Content, 40, 6);
            Ui.Size(Ui.Button(tabs, "补货清单", () => { _procurementAgreement = false; w.Rebuild(); }, 17, agreement ? Ui.FlatBand : Ui.FlatRow, true), 150, 36);
            Ui.Size(Ui.Button(tabs, "直供协议与账款", () => { _procurementAgreement = true; w.Rebuild(); }, 17, agreement ? Ui.FlatRow : Ui.FlatBand, true), 170, 36);
            Ui.Size(Ui.Label(tabs, "", 14), -1, 36, 1);
            Ui.Size(Ui.Label(tabs, $"现金 {S.Ledger.Balance} · 共同保护额 {S.EffectiveRestockReserve}", 15, TextAnchor.MiddleRight, Ui.Dim), -1, 36, 1);
            Ui.Size(Ui.Button(tabs, "全店补货预算", OpenProcurementBudget, 15), 130, 34).name = "ProcurementBudget";
        }

        private void BuildProcurementAgreement(Window w, bool multi)
        {
            ProcurementTabs(w, true);
            BuildDirectSupply(w);
        }

        // ---- 第一层：店铺卡片 ----
        private void BuildProcurementShops(Window w, OwnedShop[] shops)
        {
            ProcurementTabs(w, false);
            HelpLink(w.Content, "采购与补货说明", "每家店单独设补货目标：库存低于下限时，由这家店的进货员补到目标。订单材料优先，在途和已认领的数量都会算进库存。");
            foreach (var shop in shops)
            {
                var target = shop;
                var items = ProcurementItems(shop.Place.Id);
                var listed = items.Where(i => i.Listed).ToArray();
                var buyers = ProcurementBuyers(shop.Place.Id);
                var card = Ui.FlatPanel(w.Content, "ProcurementShop_" + shop.Place.Id, Ui.FlatBand, Ui.FlatCardEdge).rectTransform;
                Ui.VList(card, 6, 12);
                Ui.Size(card, -1, 124);
                var head = Ui.Row(card, 38, 8);
                Ui.Size(Ui.Label(head, "<b>" + S.ShopTitle(shop) + "</b>", 22, TextAnchor.MiddleLeft), -1, 38, 1);
                Ui.Size(Ui.Button(head, "进入 ›", () => { _procurementShopId = target.Place.Id; _procurementDetail = true; _procurementFilter = 0; w.Rebuild(); }, 18, Ui.ButtonHot), 120, 36)
                    .name = "ProcurementEnter_" + shop.Place.Id;
                var chips = Ui.Row(card, 30, 8);
                Chip(chips, $"补货 {listed.Length} 种", Ui.Accent);
                int shortCount = listed.Count(i => i.Short), transitCount = listed.Count(i => i.Transit);
                Chip(chips, $"缺货 {shortCount}", shortCount > 0 ? Ui.Warn : Ui.Dim);
                Chip(chips, $"在途 {transitCount}", transitCount > 0 ? Ui.Good : Ui.Dim);
                Ui.Size(Ui.Label(chips, "", 14), -1, 28, 1);
                var buyer = Ui.Row(card, 28, 8);
                if (buyers.Length == 0)
                    Ui.Size(Ui.Label(buyer, "进货员：未安排", 16, TextAnchor.MiddleLeft, Ui.Warn), -1, 28, 1);
                else
                {
                    Ui.Size(Ui.Label(buyer, "进货员：" + string.Join("、", buyers.Select(b => b.Person.Name)), 16, TextAnchor.MiddleLeft), -1, 28, 1);
                    Ui.Size(Ui.Label(buyer, BuyerScheduleText(buyers[0]), 15, TextAnchor.MiddleRight, Ui.Dim), -1, 28, 1);
                }
            }
            if (!string.IsNullOrEmpty(S.LastRestockNote)) Para(w.Content, S.LastRestockNote, 15, Ui.Accent);
        }

        // ---- 第二层：某家店的补货清单 ----
        private void BuildProcurementShop(Window w, OwnedShop shop, bool multi)
        {
            string shopId = shop.Place.Id;
            var items = ProcurementItems(shopId);
            var listed = items.Where(i => i.Listed).ToArray();
            var buyers = ProcurementBuyers(shopId);

            var head = Ui.Row(w.Content, 40, 8);
            if (multi) Ui.Size(Ui.Button(head, "‹ 全部店铺", () => { _procurementDetail = false; w.Rebuild(); }, 16), 120, 34).name = "ProcurementAll";
            var title = Ui.Size(Ui.Label(head, "<b>" + S.ShopTitle(shop) + "</b> · 补货", 22, TextAnchor.MiddleLeft), -1, 38, 1);
            title.name = "ProcurementDestination";
            Ui.Size(Ui.Label(head, $"现金 {S.Ledger.Balance}", 15, TextAnchor.MiddleRight, Ui.Dim), 200, 34);
            Ui.Size(Ui.Button(head, "全店补货预算", OpenProcurementBudget, 15), 130, 34).name = "ProcurementBudget";
            Ui.Size(Ui.Button(head, "直供协议", () => { _procurementAgreement = true; w.Rebuild(); }, 15), 100, 34);

            int shortCount = listed.Count(i => i.Short), transitCount = listed.Count(i => i.Transit);

            if (buyers.Length == 0)
            {
                var none = Ui.FlatPanel(w.Content, "BuyerBar", Ui.FlatBand, Ui.FlatCardEdge).rectTransform;
                Ui.Size(none, -1, 50); Ui.VList(none, 0, 6); var noneRow = Ui.Row(none, 36, 8);
                Ui.Size(Ui.Label(noneRow, "这家店还没有进货员，设了补货目标也没人去补。到员工管理招聘进货员。", 16, TextAnchor.MiddleLeft, Ui.Warn), -1, 40, 1);
            }
            foreach (var buyer in buyers.Take(3))
            {
                var worker = buyer;
                var bar = Ui.FlatPanel(w.Content, "BuyerBar_" + buyer.Person.Id, Ui.FlatBand, Ui.FlatCardEdge).rectTransform;
                Ui.Size(bar, -1, 50); Ui.VList(bar, 0, 6);
                var row = Ui.Row(bar, 36, 8);
                Ui.Size(Ui.Label(row, "进货员 <b>" + buyer.Person.Name + "</b> · " + BuyerScheduleText(buyer), 16, TextAnchor.MiddleLeft), -1, 40, 1);
                Ui.Size(Ui.Button(row, "安排进货 ›", () => OpenEmployeePurchase(worker.Person.Id, w), 16, Ui.ButtonHot), 140, 34).name = "ProcurementArrange_" + buyer.Person.Id;
            }

            // 筛选 + 搜索 + 添加
            var filters = Ui.Row(w.Content, 40, 6);
            void Tab(string label, int index)
            {
                Ui.Size(Ui.Button(filters, label, () => { _procurementFilter = index; w.Rebuild(); }, 16, _procurementFilter == index ? Ui.FlatRow : Ui.FlatBand), 118, 36);
            }
            Tab("全部 " + listed.Length, 0); Tab("缺货 " + shortCount, 1); Tab("在途 " + transitCount, 2);
            Ui.Size(Ui.TextInput(filters, _procurementSearch, "搜索已设补货的商品", value => { _procurementSearch = value; w.RequestRebuild(); }), -1, 36, 1);
            Ui.Size(Ui.Button(filters, "＋ 添加商品", () => OpenProcurementAdd(w, shop, items, buyers), 17, Ui.ButtonHot), 150, 36).name = "ProcurementAdd";

            var visible = listed.Where(i => _procurementFilter == 0 || (_procurementFilter == 1 ? i.Short : i.Transit))
                .Where(i => string.IsNullOrWhiteSpace(_procurementSearch) || (i.Offer.Name + i.Offer.Category).IndexOf(_procurementSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();

            var columns = Ui.Row(w.Content, 22, 8);
            columns.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            Ui.Size(Ui.Label(columns, "商品", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 22, 1);
            Ui.Size(Ui.Label(columns, "库存", 14, TextAnchor.MiddleCenter, Ui.Dim), 60, 22);
            Ui.Size(Ui.Label(columns, "在途", 14, TextAnchor.MiddleCenter, Ui.Dim), 60, 22);
            Ui.Size(Ui.Label(columns, "目标", 14, TextAnchor.MiddleCenter, Ui.Dim), 184, 22);
            Ui.Size(Ui.Label(columns, "下限", 14, TextAnchor.MiddleCenter, Ui.Dim), 184, 22);
            Ui.Size(Ui.Label(columns, "负责", 14, TextAnchor.MiddleCenter, Ui.Dim), 110, 22);
            Ui.Size(Ui.Label(columns, "", 14), 56, 22);

            var orders = S.ProcurementOrders.Count(o => o.ShopId == shopId && o.State == "在途")
                + S.ProcurementClaims.Count(c => c.State == ProcurementClaimState.Claimed && c.ShopId == shopId);
            var list = Ui.Scroll(w.Content, _procurementOrdersOpen ? 200 : 440);
            if (visible.Length == 0)
                Para(list, listed.Length == 0 ? "这家店还没有设补货的商品。点右上角「＋ 添加商品」，设好目标就会由进货员补货。" : "没有符合条件的商品。", 17, Ui.Dim);
            foreach (var item in visible)
                BuildProcurementRow(w, list, shop, item, buyers);

            if (!string.IsNullOrEmpty(S.LastRestockNote)) Para(w.Content, S.LastRestockNote, 15, Ui.Accent);

            var orderBar = Ui.Row(w.Content, 40, 8);
            Ui.Size(Ui.Button(orderBar, $"在途订单（{orders}）  {(_procurementOrdersOpen ? "收起 ▾" : "展开 ▸")}", () => { _procurementOrdersOpen = !_procurementOrdersOpen; w.Rebuild(); }, 16, Ui.FlatBand), -1, 38, 1)
                .name = "ProcurementOrdersToggle";
            if (_procurementOrdersOpen) BuildCityProcurementOrders(w, 230);
        }

        private void BuildProcurementRow(Window w, RectTransform list, OwnedShop shop, ProcurementItem item, EmployeeAssignment[] buyers)
        {
            string shopId = shop.Place.Id;
            var offer = item.Offer; var policy = item.Policy; var cov = item.Coverage;
            var row = Ui.Row(list, 56, 8);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            if (item.Short) row.gameObject.AddComponent<Image>().color = new Color(.42f, .17f, .15f, .22f);
            string sub = offer.Category + (cov.ContractMissing > 0 ? $" · 订单缺货 {cov.ContractMissing}" : "") + (cov.Claimed > 0 ? $" · 已安排 {cov.Claimed}" : "");
            var label = Ui.Label(row, offer.Name + "\n<size=14><color=#AAB4BB>" + sub + "</color></size>", 17, TextAnchor.MiddleLeft);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.Size(label, -1, 52, 1);
            { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
            Ui.Size(Ui.Label(row, cov.FreeStock.ToString(), 18, TextAnchor.MiddleCenter, cov.FreeStock <= 0 ? Ui.Warn : Color.white), 60, 52);
            int transit = cov.InTransit + cov.Claimed;
            Ui.Size(Ui.Label(row, transit > 0 ? transit.ToString() : "—", 18, TextAnchor.MiddleCenter, transit > 0 ? Ui.Good : Ui.Dim), 60, 52);
            string key = offer.Kind + "_" + offer.Id;
            int max = offer.Kind == PurchaseGoodsKind.Part ? GameSession.RestockMaxTarget : 100;
            int lower = policy.Lower < 0 ? cov.Lower : policy.Lower;
            QuantityStepper(w, row, "ptgt_" + key, cov.Target, 1, max, 1, n =>
                EditProcurement(shop, offer, buyers, p => { p.Target = (int)n; if (p.Lower > p.Target || p.Lower < 0) p.Lower = Math.Min(p.Target, p.Lower < 0 ? (int)(p.Target * GameSession.RestockTriggerRatio) : p.Lower); }));
            QuantityStepper(w, row, "plow_" + key, Math.Min(lower, cov.Target), 0, cov.Target, 1, n =>
                EditProcurement(shop, offer, buyers, p => p.Lower = (int)n));
            string owner = policy.Primary == ProcurementMethod.Online1788 ? "网购自动" : buyers.FirstOrDefault(b => b.Person.Id == policy.EmployeeId)?.Person.Name ?? "未指定";
            var ownerButton = Ui.Size(Ui.Button(row, owner, () =>
            {
                if (buyers.Length < 2) return;
                int index = Array.FindIndex(buyers, b => b.Person.Id == policy.EmployeeId);
                var next = buyers[(index + 1) % buyers.Length];
                EditProcurement(shop, offer, buyers, p => { p.Primary = ProcurementMethod.Employee; p.EmployeeId = next.Person.Id; });
                w.Rebuild();
            }, 15, Ui.ButtonBg), 110, 34);
            ownerButton.Tip(buyers.Length < 2 ? "这件商品由本店进货员负责。" : "点一下换下一位进货员负责。");
            Ui.Size(Ui.Button(row, "移除", () =>
            {
                EditProcurement(shop, offer, buyers, p => { p.Target = 0; p.Lower = 0; });
                w.Rebuild();
            }, 15, Ui.ButtonBg), 56, 34);
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
                    Ui.Size(Ui.Label(row, pick.Offer.Name + "  <size=14><color=#AAB4BB>" + pick.Offer.Category + "</color></size>", 17, TextAnchor.MiddleLeft), -1, 40, 1);
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
