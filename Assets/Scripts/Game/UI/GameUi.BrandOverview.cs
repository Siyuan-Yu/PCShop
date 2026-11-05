using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private readonly HashSet<string> _expandedBrands = new HashSet<string>();
        private RectTransform BrandCard(Transform parent, string title)
        {
            var card = Ui.Panel(parent, "BrandCard", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 5, 10);
            if (!string.IsNullOrEmpty(title)) Line(card, title, 20, Ui.Accent);
            return card;
        }

        private void BuildBrandOverview(Transform body, Window w, BrandBusinessState state)
        {
            var header = Ui.Row(body, 40, 8);
            Ui.Size(Ui.Label(header, "所有品牌 · 共" + state.Brands.Length + "个", 20, color: Ui.Accent), -1, 40, 1);
            var remaining = GameSession.BrandKinds.Where(kind => !state.Brands.Any(b => b.Kind == kind)).ToArray();
            if (remaining.Length > 0)
                Ui.Size(Ui.Button(header, _brandCreating ? "收起创建表单" : "＋ 创建品牌", () =>
                {
                    _brandCreating = !_brandCreating;
                    w.Rebuild();
                }, 17), 160, 36);
            if (_brandCreating && remaining.Length > 0) BuildBrandCreation(body, w, remaining);
            if (state.Brands.Length == 0) return;
            Line(body, "统计区间：" + new GameDate(S.BrandStatsFirstDay) + "—" + S.Clock.Today + "（含今天）", 15, Ui.Dim);
            foreach (var brand in state.Brands)
            {
                var b = brand;
                var shops = S.MyShops.Where(s => b.ShopIds.Contains(s.Place.Id) && s.RetailKind == b.Kind).ToArray();
                var card = BrandCard(body, b.Name + " · " + RetailShopTypes.Name(b.Kind)
                    + (b.Position == "value" ? " · 实惠定位" : " · 品质定位"));
                var metrics = Ui.Row(card, 56, 8);
                BrandMetric(metrics, "旗下门店", shops.Length + "家");
                BrandMetric(metrics, "近7天营收", S.BrandWeeklyRevenue(b.Id).ToString());
                BrandMetric(metrics, "近7天支出", S.BrandWeeklyExpenses(b.Id).ToString());
                BrandMetric(metrics, "口碑", b.Reputation.ToString());
                BrandMetric(metrics, "知名度", b.Influence.ToString());
                var deals = state.Deals.Where(d => d.BrandId == b.Id && !d.Ended && d.End > S.Clock.Today.TotalDays).ToArray();
                Line(card, "有效代言 " + deals.Count(d => d.Mode == "endorse")
                    + "人 · 短期推广 " + deals.Count(d => d.Mode == "campaign") + "项", 16, Ui.Dim);
                var actions = Ui.Row(card, 36, 8);
                Ui.Size(Ui.Button(actions, _expandedBrands.Contains(b.Id) ? "收起旗下门店" : "展开旗下门店", () =>
                {
                    if (!_expandedBrands.Remove(b.Id)) _expandedBrands.Add(b.Id);
                    w.Rebuild();
                }, 17), -1, 36, 1).name = "BrandExpand_" + b.Id;
                Ui.Size(Ui.Button(actions, "查看推广与代言", () =>
                {
                    _brandId = b.Id; _brandTab = 1; _promotionPage = "overview"; w.Rebuild();
                }, 17), -1, 36, 1);
                Ui.Size(Ui.Button(actions, "修改品牌名…", () => OpenBrandRename(b, w), 17), 170, 36).name = "BrandRename_" + b.Id;
                if (!_expandedBrands.Contains(b.Id)) continue;
                var tableHeader = Ui.Row(card, 28, 6);
                Ui.Size(Ui.Label(tableHeader, "门店 / 状态", 16, color: Ui.Accent), -1, 28, 2);
                Ui.Size(Ui.Label(tableHeader, "近7天营收", 16, color: Ui.Accent), -1, 28, 1);
                Ui.Size(Ui.Label(tableHeader, "近7天支出", 16, color: Ui.Accent), -1, 28, 1);
                Ui.Size(Ui.Label(tableHeader, "口碑", 16, color: Ui.Accent), 80, 28);
                foreach (var shop in shops)
                {
                    var finance = S.GetCityFinance(new GameDate(S.BrandStatsFirstDay), S.Clock.Today, shop.Place.Id);
                    var row = Ui.Row(card, 44, 6);
                    var name = Ui.Rect(row, "BrandShopName"); Ui.Size(name, -1, 44, 2); Ui.VList(name, 2, 0);
                    Ui.Size(Ui.Label(name, S.ShopTitle(shop), 16), -1, 22);
                    Ui.Size(Ui.Label(name, shop.Sim.NotFitted ? "待装修" : shop.Sim.IsOpenAt(S.Clock.Now) ? "营业中" : "已打烊", 14, color: Ui.Dim), -1, 18);
                    Ui.Size(Ui.Label(row, finance.Sales.ToString(), 16), -1, 44, 1);
                    Ui.Size(Ui.Label(row, finance.OperatingOut.ToString(), 16), -1, 44, 1);
                    Ui.Size(Ui.Label(row, shop.Sim.Reputation.ToString("0.#"), 16), 80, 44);
                }
                Line(card, "品牌公共支出 " + S.BrandWeeklyExpenses(b.Id, true) + "，已计入品牌支出。", 15, Ui.Dim);
                Ui.Size(Ui.Button(card, "调整门店归属", () => OpenBrandShopMembership(b.Id), 16), -1, 32);
            }
        }

        private void BuildBrandCreation(Transform body, Window w, string[] industries)
        {
            var card = BrandCard(body, "创建品牌");
            Para(card, "满足以下条件后即可创建品牌，每个行业可创建一个品牌。", 17);
            bool communityOpen = S.DistrictAccessTier >= 1;
            Line(card, communityOpen ? "✓ 已开放社区" : "待完成：完成第一段主线，开放社区", 17, communityOpen ? Ui.Good : Ui.Warn);
            var name = Ui.TextInput(card, _brandName, "品牌名称（2—16字）", value => _brandName = value);
            Ui.Size(name, -1, 38);
            var positioning = Ui.Row(card, 36, 6);
            foreach (var position in new[] { "value", "quality" })
            {
                string label = position == "value" ? "实惠定位" : "品质定位";
                Ui.Size(Ui.Button(positioning, label + (_brandPosition == position ? " ✓" : ""), () =>
                {
                    _brandPosition = position;
                    w.Rebuild();
                }, 17), -1, 36, 1);
            }
            foreach (var kind in industries)
            {
                string industry = RetailShopTypes.Name(kind);
                Line(card, industry, 20, Ui.Accent);
                Para(card, S.BrandQualificationText(kind), 17);
                Para(card, "同行指标为市场营收估计，用作资格基准。", 14, Ui.Dim);
                string blocker = S.BrandFoundBlocker(kind);
                if (blocker != null) Para(card, blocker, 16, Ui.Warn);
                BrandButton(card, "创建" + industry + "品牌", () =>
                {
                    bool created = S.FoundBrand(kind, name.text, _brandPosition);
                    if (created) _brandCreating = false;
                    return created;
                }, w, blocker == null);
            }
        }

        private void BrandMetric(Transform row, string label, string value)
        {
            var cell = Ui.Rect(row, "BrandMetric"); Ui.Size(cell, -1, 56, 1); Ui.VList(cell, 3, 0);
            Ui.Size(Ui.Label(cell, label, 15, color: Ui.Dim), -1, 22);
            Ui.Size(Ui.Label(cell, value, 20, color: Ui.Accent), -1, 28);
        }

        private void OpenBrandRename(BrandRecord brand, Window overview)
        {
            OpenWindow("修改品牌名 · " + brand.Name, 620, 345, w =>
            {
                Para(w.Content, "每次确认改名，当前口碑和知名度各减半，结果向下取整。", 17);
                var metrics = Ui.Row(w.Content, 56, 8);
                BrandMetric(metrics, "改名后口碑", $"{brand.Reputation} → {brand.Reputation / 2}");
                BrandMetric(metrics, "改名后知名度", $"{brand.Influence} → {brand.Influence / 2}");
                var input = Ui.TextInput(w.Content, brand.Name, "输入新品牌名（2—16字）", null);
                Ui.Size(input, -1, 40);
                var actions = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(actions, "取消", w.Close, 17), -1, 40, 1);
                Ui.Size(Ui.Button(actions, "确认改名", () =>
                {
                    if (!S.RenameBrand(brand.Id, input.text)) return;
                    w.Close();
                    overview.Rebuild();
                }, 17, Ui.Warn), -1, 40, 1);
            });
        }

        private void OpenBrandShopMembership(string brandId)
        {
            OpenWindow("调整品牌门店归属", 760, 650, w =>
            {
                var brand = S.BrandBusiness.Brands.First(b => b.Id == brandId);
                Para(w.Content, "同品类自营门店共享品牌知名度和口碑。纳入品牌后，还会计入品牌统计并享受推广与代言效果。至少保留一家门店。", 17);
                var list = Ui.Scroll(w.Content, 450);
                foreach (var shop in S.MyShops.Where(s => s.RetailKind == brand.Kind))
                {
                    string id = shop.Place.Id;
                    BrandButton(list, (brand.ShopIds.Contains(id) ? "已纳入 · 移出 " : "未纳入 · 加入 ") + S.ShopTitle(shop),
                        () => S.ToggleBrandShop(brandId, id), w);
                }
                Ui.Size(Ui.Button(w.Content, "返回品牌总览", () => { w.Close(); RebuildWindows(); }, 17), -1, 38);
            });
        }
    }
}
