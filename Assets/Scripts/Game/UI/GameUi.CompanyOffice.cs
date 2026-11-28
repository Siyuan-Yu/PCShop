using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private GameSession _handoverCheckedSession;
        private int _handoverCheckedDay = -1;
        private string _receptionCompanyKey;
        private static readonly string[] CompanyOfficeIds = { "park_office_tower", "garden_office_tower" };

        private IEnumerable<Place> CompanyOffices() => CompanyOfficeIds.Where(S.City.HasPlace).Select(S.City.GetPlace);

        private IEnumerable<CompanyGovernance> RelatedCompanyGovernance() => S.GovernanceCompanies.Where(g =>
            g.ControllerId == GameSession.PlayerShareholderId || g.LastPlayerHandoverAt > 0
                || S.ShareholderShares(g, GameSession.PlayerShareholderId) > 0);

        private void TryShowCompanyHandover()
        {
            if (!S.CompanyGovernanceUnlocked || AnyModalOpen || _ctrl.Placing || S.IsGameOver
                || S.IsWorking || S.Racing || S.CityActivityExclusive) return;
            int day = S.Clock.Today.TotalDays;
            if (_handoverCheckedSession == S && _handoverCheckedDay == day) return;
            var pending = RelatedCompanyGovernance()
                .Where(g => g.LastPlayerHandoverAt > g.ReviewedPlayerHandoverAt)
                .OrderBy(g => g.LastPlayerHandoverAt).FirstOrDefault();
            if (pending != null) { OpenCompanyHandover(pending); return; }
            _handoverCheckedSession = S;
            _handoverCheckedDay = day;
        }

        private void BuildRecentHandoverLinks(Transform parent)
        {
            foreach (var g in RelatedCompanyGovernance().Where(g => g.LastPlayerHandoverAt > 0)
                .OrderByDescending(g => g.LastPlayerHandoverAt))
                CompanyButton(parent, S.GovernanceName(g.Ticker) + " · 查看交接结果", () => OpenCompanyHandover(g));
        }

        private string StoreNames(IEnumerable<OwnedShop> shops)
        {
            var names = shops.Select(S.ShopTitle).ToArray();
            return names.Length == 0 ? "无" : string.Join("、", names);
        }

        private void OpenCompanyHandover(CompanyGovernance g)
        {
            var company = S.CompanyRecordFor(g.Ticker);
            var route = CompanyOffices().Where(p => S.DistrictAccessBlocker(p) == null).Select(office =>
            {
                string blocker = S.CityDestinationTravelBlocker(office, out int minutes, out var fare, allowCurrentPlace: true);
                return (office, blocker, minutes, fare);
            }).OrderBy(r => r.blocker != null).ThenBy(r => r.minutes).FirstOrDefault();
            OpenWindow("公司交接完成", 860, 590, w =>
            {
                Line(w.Content, S.GovernanceName(g.Ticker), 24, Ui.Accent);
                Line(w.Content, $"控股人 {S.ShareholderName(g.ControllerId)}　·　总经理 {S.ShareholderName(g.ManagerId)}", 18);
                if (company != null)
                {
                    var shops = S.CompanyShops(company).ToArray();
                    Para(w.Content, "你可以经营的公司门店　" + StoreNames(shops.Where(S.PlayerManagesShop)), 17);
                    Para(w.Content, "由公司管理层经营　" + StoreNames(shops.Where(shop => !S.PlayerManagesShop(shop))), 17);
                }
                else
                    Para(w.Content, "这家同行公司的经营按整体规模结算。可在董事会查看经营、调整预算和提出扩张议案。", 17);
                Para(w.Content, "继续由你经营的个人门店　" + StoreNames(S.MyShops.Where(shop => S.CompanyOfShop(shop) == null)), 17);
                if (route.office != null)
                {
                    if (route.blocker == null && route.minutes > 0)
                    {
                        string duration = route.minutes >= 60
                            ? $"{route.minutes / 60} 小时 {route.minutes % 60} 分钟" : $"{route.minutes} 分钟";
                        Line(w.Content, $"预计路程 {duration}" + (route.fare.IsPositive ? $" · 车费 {route.fare}" : ""), 16, Ui.Dim);
                    }
                    Line(w.Content, "到左侧前台按 F 办理。以后可从「经营 → 公司」前往。", 16, Ui.Dim);
                    BuildOfficeGoButton(w.Content, route.office, company?.BrandId ?? g.Ticker, primary: true);
                }
                CompanyButton(w.Content, "查看公司", () =>
                {
                    CloseAllWindows();
                    if (company != null) OpenManagementPanel(2, company.BrandId);
                    else OpenCompanyGovernance(g.Ticker);
                });
            }, onClosed: () => g.ReviewedPlayerHandoverAt = g.LastPlayerHandoverAt, fitContent: true);
        }

        private void BuildOfficeGoButton(Transform parent, Place office, string companyKey = null, bool primary = false)
        {
            bool inside = S.CurrentPlace == office;
            string blocker = inside ? null : S.CityDestinationTravelBlocker(office, out _, out _);
            CompanyButton(parent, inside ? office.Name + " · 当前所在" : "前往" + office.Name, () =>
            {
                _receptionCompanyKey = companyKey;
                CloseAllWindows();
                if (inside) Toast("走到大厅左侧前台按 F 办理。");
                else _ctrl.GoToPlace(office);
            }, blocker, primary);
        }

        private void BuildCompanyOfficeLinks(Transform parent)
        {
            var offices = CompanyOffices().ToArray();
            var markets = RelatedCompanyGovernance().Where(g => S.CompanyRecordFor(g.Ticker) == null).ToArray();
            if (offices.Length == 0 && markets.Length == 0) return;
            var row = Ui.Row(parent, -1, 8);
            if (offices.Length > 0)
            {
                var card = CompanyCard(row, "写字楼", 1);
                foreach (var office in offices) BuildOfficeGoButton(card, office, _workspaceCompany);
            }
            if (markets.Length > 0)
            {
                var card = CompanyCard(row, "控股与参股公司", 1);
                foreach (var g in markets)
                    CompanyButton(card, S.GovernanceName(g.Ticker) + " · " + S.CompanyControlStatus(g),
                        () => OpenCompanyGovernance(g.Ticker));
            }
            BuildRecentHandoverLinks(parent);
        }

        public void OpenOfficeReception(Place office)
        {
            var companies = S.Companies.ToArray();
            var markets = RelatedCompanyGovernance().Where(g => S.CompanyRecordFor(g.Ticker) == null
                && (g.ControllerId == GameSession.PlayerShareholderId || S.CompanyBoardMember(g))).ToArray();
            var entries = companies.Select(c => (key: c.BrandId, label: S.CompanyName(c))).Concat(
                markets.Select(g => (key: g.Ticker, label: S.GovernanceName(g.Ticker)))).ToArray();
            string selected = entries.Any(x => x.key == _receptionCompanyKey)
                ? _receptionCompanyKey : entries.FirstOrDefault().key;

            OpenWindow(office.Name + " · 前台", 1050, 740, w =>
            {
                var body = w.Content;
                if (entries.Length == 0)
                {
                    Line(body, "可以在企业服务中心登记公司。", 18);
                    CompanyCenterLink(body);
                    return;
                }
                if (entries.Length > 1)
                {
                    var selector = Ui.Choice(body, entries.Select(x => x.label),
                        Array.FindIndex(entries, x => x.key == selected), i => { selected = entries[i].key; w.Rebuild(); });
                    Ui.Size(selector, -1, 40);
                }
                else Line(body, entries[0].label, 23, Ui.Accent);
                var company = companies.FirstOrDefault(c => c.BrandId == selected);
                if (company == null)
                {
                    Para(body, "这家公司的门店和用工按整体经营规模结算。", 17);
                    CompanyButton(body, "查看经营与门店", () => { _governancePage = 2; OpenCompanyGovernance(selected); }, primary: true);
                    CompanyButton(body, "打开董事会", () => { _governancePage = 1; OpenCompanyGovernance(selected); });
                    return;
                }
                var split = Ui.Row(body, -1, 12);
                BuildOfficeCompanyStores(CompanyCard(split, "公司门店", 1), company);
                var hiring = CompanyCard(split, "人事", 1);
                string authority = S.CompanyOfficeBlocker(company);
                if (authority != null) Line(hiring, authority, 16, Ui.Dim);
                string leader = S.CompanyDepartmentLeader(company, CompanyDepartment.Personnel);
                var pending = company.PendingPosts.FirstOrDefault(p => p.Leader && p.InDepartment(CompanyDepartment.Personnel));
                if (leader != null) Line(hiring, "人事负责人　" + S.FindEmployee(leader)?.Person.Name, 17);
                else if (pending != null) Line(hiring,
                    $"{S.FindEmployee(pending.EmployeeId)?.Person.Name} · {new GameTimestamp(pending.EffectiveAt)} 到任", 17);
                var roster = S.CompanyRoster(company).ToArray();
                bool appointmentsAvailable = roster.Any(row => S.CompanyTransferBlocker(company, row.Person.Id, CompanyDepartment.Personnel, true) == null);
                if (authority == null && leader == null && pending == null)
                    Line(hiring, roster.Length == 0 ? "先在门店聘用员工，再任命人事负责人。"
                        : "人事负责人到任后可核准招聘。", 16, Ui.Dim);
                CompanyButton(hiring, "任命人事负责人",
                    () => OpenCompanyStaffPicker(w, company, CompanyDepartment.Personnel, true),
                    authority ?? (appointmentsAvailable ? null : "暂无可任命人选或负责人席位已有安排"));
                CompanyButton(hiring, "核准招聘", () => OpenCompanyRecruiting(company),
                    authority ?? (leader == null ? "人事负责人到任后可核准招聘" : null), primary: true);
                CompanyButton(body, "打开公司经营", () => { CloseAllWindows(); OpenManagementPanel(2, company.BrandId); });
            }, fitContent: true);
        }

        private void BuildOfficeCompanyStores(Transform parent, CompanyRecord company)
        {
            var shops = S.CompanyShops(company).ToArray();
            if (shops.Length == 0) Line(parent, "暂无经营门店", 16, Ui.Dim);
            foreach (var shop in shops)
            {
                var status = S.PlayerManagesShop(shop) ? "你可经营" : "公司管理层经营";
                var title = $"{S.ShopTitle(shop)} · {status} · 口碑 {shop.Sim.ShopReputation:0.#}";
                double promotion = S.CompanyPromotionTraffic(shop);
                if (promotion > 0) title += " · 宣传客流 +" + promotion.ToString("P0");
                CompanyButton(parent, title, () =>
                {
                    CloseAllWindows();
                    _managementShops.Bind(S);
                    _managementShops.CompanyId = company.BrandId;
                    _managementShops.Select(shop.Place.Id);
                    OpenManagementPanel(0);
                }, S.ShopAuthorityBlocker(shop));
            }
        }
    }
}
