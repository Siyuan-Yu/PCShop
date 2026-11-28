using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void CompanyCenterLink(Transform parent, bool primary = false)
        {
            var center = S.EnterpriseCenter;
            if (center == null) return;
            CompanyButton(parent, S.AtEnterpriseCenter ? "打开企业服务中心" : "前往企业服务中心", () =>
            {
                if (S.AtEnterpriseCenter) OpenEnterpriseServiceCenter(center);
                else { CloseAllWindows(); S.BeginCityTravel(center.Id, enter: true); }
            }, S.DistrictAccessBlocker(center), primary);
        }

        private void OpenEnterpriseServiceCenter(Place center)
        {
            bool listing = false;
            string kind = GameSession.BrandKinds[0], selectedCompany = null, name = "", position = "quality";
            var selectedShops = new HashSet<string>();
            bool preselect = true;
            OpenPlaceWindow(center, (window, _) =>
            {
                var body = window.Content;
                var tabs = Ui.Row(body, 42, 8);
                Ui.Tab(tabs, "公司登记", () => { listing = false; window.Rebuild(); }, !listing);
                Ui.Tab(tabs, "上市受理", () => { listing = true; window.Rebuild(); }, listing);
                if (listing)
                {
                    var companies = S.Companies.ToArray();
                    if (S.DistrictAccessTier < 3)
                    {
                        Line(body, "花园开放后可在此办理上市。", 18, Ui.Warn);
                        return;
                    }
                    if (companies.Length == 0) { Line(body, "请先办理公司登记。", 18, Ui.Dim); return; }
                    CompanyRecord company = companies.FirstOrDefault(c => c.BrandId == selectedCompany) ?? companies[0];
                    selectedCompany = company.BrandId;
                    if (companies.Length > 1)
                    {
                        var selector = Ui.Row(body, 40, 7);
                        foreach (var entry in companies)
                            Ui.Tab(selector, S.CompanyName(entry), () => { selectedCompany = entry.BrandId; window.Rebuild(); }, entry == company);
                    }
                    BuildListingDesk(body, window, company);
                    return;
                }

                var industries = Ui.Row(body, 41, 7);
                foreach (string option in GameSession.BrandKinds)
                    Ui.Tab(industries, GameSession.CompanyIndustryName(option), () =>
                    {
                        kind = option; selectedShops.Clear(); preselect = true; window.Rebuild();
                    }, option == kind);
                var existing = S.Companies.FirstOrDefault(x => x.BrandId == "brand:" + kind);
                if (existing != null)
                {
                    Line(body, S.CompanyName(existing) + " · 已登记", 22, Ui.Good);
                    CompanyButton(body, "查看公司", () => OpenRegisteredCompany(existing.BrandId));
                    return;
                }
                var columns = Ui.Row(body, -1, 9);
                var eligibility = CompanyCard(columns, "登记资格", 1);
                Para(eligibility, S.BrandQualificationText(kind), 17);
                string blocked = S.CompanyRegistrationBlocker(kind);
                Line(eligibility, blocked ?? "已符合登记资格", 17, blocked == null ? Ui.Good : Ui.Warn);
                var application = CompanyCard(columns, "公司资料", 1.15f);
                var input = Ui.TextInput(application, name, "公司名称（2–16 字）", value => name = value);
                Ui.Size(input, -1, 38);
                var positions = Ui.Row(application, 38, 7);
                foreach (string option in new[] { "quality", "value" })
                    Ui.Tab(positions, option == "quality" ? "品质定位" : "实惠定位",
                        () => { position = option; window.Rebuild(); }, option == position);
                Line(application, "划入公司的自营门店", 18, Ui.Accent);
                var eligible = S.MyShops.Where(s => s.RetailKind == kind && S.CompanyOfShop(s) == null).ToArray();
                if (preselect)
                {
                    foreach (var shop in eligible) selectedShops.Add(shop.Place.Id);
                    preselect = false;
                }
                foreach (var shop in eligible)
                {
                    bool selected = selectedShops.Contains(shop.Place.Id);
                    Ui.Size(Ui.Button(application, (selected ? "✓ " : "＋ ") + S.ShopTitle(shop), () =>
                    {
                        if (!selectedShops.Add(shop.Place.Id)) selectedShops.Remove(shop.Place.Id);
                        window.Rebuild();
                    }, 16, Ui.SelectedButton(selected)), -1, 34);
                }
                if (eligible.Length == 0) Line(application, "暂无同业自营门店", 16, Ui.Dim);
                CompanyButton(application, "确认登记", () =>
                {
                    if (S.RegisterCompany(kind, input.text, position, selectedShops)) window.Rebuild();
                }, blocked ?? (selectedShops.Count == 0 ? "至少选择一家自营门店" : null), true);
            }, 1120, 850);
        }

        private void BuildListingDesk(Transform parent, Window window, CompanyRecord company)
        {
            if (company.ListedDay >= 0)
            {
                Line(parent, S.CompanyName(company) + " · 已挂牌", 23, Ui.Good);
                Line(parent, $"发行价 {Money.FromCents(company.ListingPriceCents)} · 总股本 {S.CompanyShareCapital(company):N0} 股", 18);
                CompanyButton(parent, "前往股市", OpenCityInvestments);
                return;
            }
            var row = Ui.Row(parent, -1, 10);
            var checks = CompanyCard(row, "上市资格", 1);
            var (income, cost) = S.CompanyLastMonth(company);
            Line(checks, $"实际营业门店　{S.CompanyShops(company).Count(s => !s.Sim.NotFitted)} 家", 18);
            Line(checks, $"上个完整月净额　{income - cost}", 18);
            Line(checks, $"周转金　{Money.FromCents(company.ReserveCents)} / 需 {S.CompanyCashRequirement(company)}", 18);
            string blocker = S.CompanyListingBlocker(company);
            Line(checks, blocker ?? "已满足上市资格", 17, blocker == null ? Ui.Good : Ui.Warn);
            var preview = CompanyCard(row, "系统发行方案", 1);
            var price = S.CompanyListingPrice(company);
            Line(preview, $"每股 {price}　新发 {GameSession.CompanyPublicShares:N0} 股", 19);
            Line(preview, $"挂牌总股本　{GameSession.CompanyTotalShares:N0} 股", 18);
            Line(preview, $"公司到账　{price * GameSession.CompanyPublicShares}", 19);
            Line(preview, "挂牌后　创始人 80%　机构认购 20%", 18);
            CompanyProgress(preview, 80);
            CompanyButton(preview, "确认发行并挂牌", () => { if (S.ListCompany(company.BrandId)) window.Rebuild(); }, blocker, true);
        }

        private void OpenRegisteredCompany(string id)
        {
            OpenManagementPanel(2);
            _workspaceCompany = id;
            _companyPage = 0;
            _managementWindow?.Rebuild();
        }
    }
}
