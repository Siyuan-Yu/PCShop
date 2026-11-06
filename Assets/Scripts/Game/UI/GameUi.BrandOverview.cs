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
                Ui.Size(Ui.Button(header, _brandCreating ? "收起行业资格" : "＋ 创建品牌", () =>
                {
                    _brandCreating = !_brandCreating;
                    w.Rebuild();
                }, 17), 160, 36);
            if (_brandCreating && remaining.Length > 0) BuildBrandIndustryChoices(body, w, remaining);
            if (state.Brands.Length == 0) return;
            Line(body, "统计区间：" + new GameDate(S.BrandStatsFirstDay) + "—" + S.Clock.Today + "（含今天）", 15, Ui.Dim);
            long now = S.Clock.Now.TotalMinutes;
            var activeEndorsers = S.KolStreaming.Contracts.Where(c => !c.Ended && c.SignedAt <= now && now < c.EndAt)
                .ToLookup(c => c.BrandId);
            foreach (var brand in state.Brands)
            {
                var b = brand;
                var shops = S.MyShops.Where(s => b.ShopIds.Contains(s.Place.Id) && s.RetailKind == b.Kind).ToArray();
                var endorsers = activeEndorsers[b.Id].ToArray();
                var card = BrandCard(body, "");
                BuildBrandOverviewHeading(card, w, b, endorsers.Length);
                var metrics = Ui.Row(card, 56, 8);
                BrandMetric(metrics, "旗下门店", shops.Length + "家");
                BrandMetric(metrics, "近7天营收", S.BrandWeeklyRevenue(b.Id).ToString());
                BrandMetric(metrics, "近7天支出", S.BrandWeeklyExpenses(b.Id).ToString());
                BrandMetric(metrics, "口碑", b.Reputation.ToString());
                BrandMetric(metrics, "知名度", b.Influence.ToString());
                Line(card, "近7天实际到店 " + S.BrandActualVisits(b.Id) + "人次", 16, Ui.Dim);
                BuildBrandOverviewEndorsers(card, b.Id, endorsers);
                var actions = Ui.Row(card, 36, 8);
                Ui.Size(Ui.Button(actions, _expandedBrands.Contains(b.Id) ? "收起旗下门店" : "展开旗下门店", () =>
                {
                    if (!_expandedBrands.Remove(b.Id)) _expandedBrands.Add(b.Id);
                    w.Rebuild();
                }, 17), -1, 36, 1).name = "BrandExpand_" + b.Id;
                Ui.Size(Ui.Button(actions, "查看品牌代言人", () =>
                {
                    _brandId = b.Id;
                    _brandTab = BrandKolTab;
                    _kolPage = "我的代言人";
                    _selectedKolContract = endorsers.FirstOrDefault()?.Id;
                    w.Rebuild();
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

        private void BuildBrandOverviewHeading(Transform card, Window w, BrandRecord brand, int endorsers)
        {
            var row = Ui.Row(card, 40, 8);
            string title = brand.Name + " · " + RetailShopTypes.Name(brand.Kind)
                + (brand.Position == "value" ? " · 实惠定位" : " · 品质定位");
            Ui.Size(Ui.Label(row, title, 20, color: Ui.Accent), 0, 40, 1);
            Ui.Size(Ui.Label(row, $"{S.BrandLevelName(brand.Id)} · 代言名额 {endorsers}/{S.BrandKolCapacity(brand.Id)}",
                15, color: Ui.Dim), 180, 40);
            if (brand.Level >= 2) return;
            string blocker = S.KolUpgradeBlocker(brand.Id);
            if (blocker != null)
                Ui.Size(Ui.Label(row, blocker, 14, TextAnchor.MiddleRight, Ui.Dim), 165, 40);
            var upgrade = Ui.Button(row, brand.Level == 0 ? "升级中级 · ¥20,000" : "升级高级 · ¥100,000", () =>
                ConfirmBusiness("升级品牌", brand.Level == 0 ? "支付¥20,000，代言名额由3人增至7人。" : "支付¥100,000，代言名额由7人增至12人。",
                    () => { S.UpgradeKolBrand(brand.Id); w.Rebuild(); }), 16, Ui.ButtonBg, blocker == null);
            Ui.Size(upgrade, 190, 34).name = "KolBrandUpgrade_" + brand.Id;
        }

        private void BuildBrandOverviewEndorsers(Transform card, string brandId, KolLiveContract[] endorsers)
        {
            if (endorsers.Length == 0)
            {
                Line(card, "当前暂无代言人", 16, Ui.Dim);
                return;
            }

            Line(card, $"当前代言人 · {endorsers.Length}人　｜　近7天品牌直播贡献", 17, Ui.Accent);
            long from = S.BrandStatsFirstDay * 1440L;
            long now = S.Clock.Now.TotalMinutes;
            var recent = S.KolBrandHistory(brandId).Where(s => s.EndsAt >= from && s.EndsAt <= now).ToLookup(s => s.PersonId);
            for (int i = 0; i < endorsers.Length; i += 2)
            {
                var row = Ui.Row(card, 72, 8);
                foreach (var endorser in endorsers.Skip(i).Take(2))
                {
                    var profile = Ui.Panel(row, "BrandEndorser_" + endorser.PersonId, Ui.PanelBg).rectTransform;
                    Ui.Size(profile, 0, 72, 1);
                    var details = Ui.Row(profile, 58, 8).Fill(6);
                    KolAvatar(details, endorser.PersonId, 45, 58);
                    var info = Ui.Rect(details, "BrandEndorserInfo");
                    Ui.Size(info, 0, 58, 1);
                    Ui.VList(info, 0, 0);
                    var shows = recent[endorser.PersonId];
                    int awareness = shows.Sum(s => s.InfluenceAfter - s.InfluenceBefore);
                    int reputation = shows.Sum(s => s.ReputationAfter - s.ReputationBefore);
                    Line(info, $"{S.CityPerson(endorser.PersonId).Name} · 完成 {shows.Count()} 场", 17);
                    Line(info, shows.Any() ? $"知名度 {awareness:+0;-0;0}　·　口碑 {reputation:+0;-0;0}" : "近7天暂无品牌直播结算", 15,
                        awareness != 0 || reputation != 0 ? Ui.Good : Ui.Dim);
                }
                if (i + 1 == endorsers.Length) Ui.Size(Ui.Rect(row, "EmptyEndorserCell"), 0, 72, 1);
            }
        }

        private void BuildBrandIndustryChoices(Transform body, Window overview, string[] industries)
        {
            var card = BrandCard(body, "选择要创建的行业");
            Para(card, "每个行业可创建一个品牌。", 17);
            foreach (var kind in industries)
            {
                string industry = RetailShopTypes.Name(kind);
                string blocker = S.BrandFoundBlocker(kind);
                Line(card, industry + (blocker == null ? " · 可创建" : " · 待达标"), 20, blocker == null ? Ui.Good : Ui.Accent);
                Para(card, S.BrandQualificationText(kind), 17);
                if (blocker != null) Para(card, blocker, 16, Ui.Warn);
                Ui.Size(Ui.Button(card, "创建" + industry + "品牌", () => OpenBrandCreation(kind, overview),
                    17, Ui.ButtonHot, blocker == null), -1, 38).name = "BrandCreate_" + kind;
            }
        }

        private void OpenBrandCreation(string kind, Window overview)
        {
            string brandName = "";
            string position = "quality";
            OpenWindow("创建" + RetailShopTypes.Name(kind) + "品牌", 650, 400, w =>
            {
                Line(w.Content, "品牌名称", 19, Ui.Accent);
                var input = Ui.TextInput(w.Content, brandName, "输入品牌名称（2—16字）", value => brandName = value);
                Ui.Size(input, -1, 40).name = "BrandCreationName";
                Line(w.Content, "品牌定位", 19, Ui.Accent);
                var positions = Ui.Row(w.Content, 42, 8);
                foreach (string option in new[] { "value", "quality" })
                {
                    string label = option == "value" ? "实惠定位" : "品质定位";
                    Ui.Size(Ui.Button(positions, label, () => { brandName = input.text; position = option; w.Rebuild(); }, 18,
                        position == option ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                }
                string blocker = S.BrandFoundBlocker(kind);
                if (blocker != null) Para(w.Content, blocker, 17, Ui.Warn);
                var actions = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(actions, "取消", w.Close, 17), -1, 44, 1);
                Ui.Size(Ui.Button(actions, "确认创建", () =>
                {
                    if (!S.FoundBrand(kind, input.text, position)) return;
                    _brandCreating = false;
                    S.RefreshDistrictNarrative();
                    w.Close();
                    overview.Rebuild();
                }, 18, Ui.ButtonHot, blocker == null), -1, 44, 1).name = "BrandConfirmCreation";
            });
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
                Para(w.Content, "同品类自营门店共享品牌知名度和口碑。纳入品牌后，门店会计入品牌统计。至少保留一家门店。", 17);
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
