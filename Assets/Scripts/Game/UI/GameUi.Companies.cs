using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static readonly string[] CompanyPages = { "总览", "公司部门", "人事管理", "统计", "股权与治理" };
        private static readonly string[] DepartmentPurpose = { "招聘与带教", "产品与流程", "账目与预算", "门店口碑", "宣传引流" };
        private static readonly string[] DepartmentAuthority =
            { "核准招聘与排班请求", "选研发方向并批准拨款", "核定单项开支上限", "批准服务整改", "批准宣传投放" };
        private static readonly string[] DepartmentWork =
            { "协助筛选候选人、岗位带教", "制作样品、试验和验证流程", "对账与核对工资费用", "巡店、回访并落实整改", "制作宣传物料并执行活动" };
        private string _workspaceCompany, _companyPerson;
        private int _companyPage;
        private bool _companyManagers;
        private string _companyUnit;
        private CompanyDepartment _companyDepartment = CompanyDepartment.Personnel;

        private void ResetCompanyWorkspace()
        {
            _workspaceCompany = _companyPerson = _companyUnit = null;
            _companyPage = 0;
            _companyDepartment = CompanyDepartment.Personnel;
            _companyManagers = false;
        }

        private RectTransform CompanyCard(Transform parent, string title, float flex = -1)
        {
            var card = Ui.Panel(parent, "CompanyCard", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 7, 10);
            if (flex >= 0) Ui.Size(card, 0, -1, flex);
            if (title != null) Line(card, title, 19, Ui.Accent);
            return card;
        }

        private void CompanyButton(Transform parent, string label, Action action, string blocker = null, bool primary = false)
        {
            Ui.Size(Ui.Button(parent, label, action, 16, primary ? Ui.ButtonHot : Ui.ButtonSecondary, blocker == null)
                .Tip(blocker), -1, 36);
        }

        private void BuildCompanyWorkspace(Window window)
        {
            var body = _companyPage==2||_companyPage==4 ? Ui.Rect(window.Content,"CompanyWorkspaceContent") : PhoneFlexibleScroll(window.Content, "CompanyWorkspaceContent");
            if(_companyPage==2||_companyPage==4){Ui.VList(body,6,0);Ui.Size(body).GetComponent<LayoutElement>().flexibleHeight=1;}
            if (_companyPage == 0) BuildCompanyOfficeLinks(body);
            var companies = S.Companies.ToArray();
            if (companies.Length == 0)
            {
                Line(body, "暂无公司", 23, Ui.Accent);
                Para(body, "到湖城企业服务中心办理公司登记。", 17);
                CompanyCenterLink(body);
                CompanyButton(body,"上市公司股权与治理",()=>OpenCompanyGovernance());
                return;
            }
            var company = companies.FirstOrDefault(c => c.BrandId == _workspaceCompany) ?? companies[0];
            _workspaceCompany = company.BrandId;
            if (companies.Length > 1)
            {
                var switcher = Ui.Row(body, 38, 7);
                foreach (var choice in companies)
                    Ui.Tab(switcher, S.CompanyName(choice), () =>
                    {
                        _workspaceCompany = choice.BrandId;
                        _companyPerson = _companyUnit = null;
                        window.Rebuild();
                    }, choice == company);
            }
            var tabs = Ui.Row(body, 40, 7);
            for (int i = 0; i < CompanyPages.Length; i++)
            {
                int page = i;
                Ui.Tab(tabs, CompanyPages[i], () => { _companyPage = page; window.Rebuild(); }, page == _companyPage);
            }
            Line(body, S.CompanyName(company) + (company.ListedDay >= 0 ? " · 已挂牌" : ""), 23, Ui.Accent);
            switch (_companyPage)
            {
                case 1: BuildCompanyDepartments(body, window, company); break;
                case 2: BuildCompanyRoster(body, window, company); break;
                case 3: BuildCompanyStatistics(body, company); break;
                case 4: BuildGovernanceWorkspace(body,window,S.CompanyTicker(company),false); break;
                default: BuildCompanyOverview(body, window, company); break;
            }
        }

        private void BuildCompanyOverview(Transform body, Window window, CompanyRecord company)
        {
            var staffRows=S.CompanyRoster(company).ToArray();foreach(var employee in staffRows)S.EmployeeWellbeingOf(employee.Person.Id);
            var team=Ui.Row(body,34,8);
            Ui.Size(Ui.Label(team,$"员工 {staffRows.Length} 人 · 平均满意度 {(staffRows.Length==0?0:staffRows.Average(r=>r.Person.Wellbeing.Satisfaction)):0}",17,color:Ui.Dim),0,34,1);
            Ui.Size(Ui.Button(team,"需处理 "+staffRows.Count(r=>S.EmployeeNeedsAttention(r.Person)),()=>{_companyPage=2;_companyAttentionOnly=true;_companyUnit=null;_companyPerson=null;window.RequestRebuild();},16,Ui.ButtonSecondary),150,34);
            var accounting = CompanyCard(body, "资金与经营");
            Line(accounting, "公司周转金　" + Money.FromCents(company.ReserveCents), 22);
            CompanyButton(accounting, "投入周转金", () => OpenCompanyNumber("投入公司周转金", "注入金额（元）",
                "从个人现金转入公司，用于部门项目和生产。", 10000, value => S.FundCompany(company.BrandId, value), window));
            var shops = CompanyCard(body, "公司门店");
            BuildOfficeCompanyStores(shops,company);
            var listing = CompanyCard(body, "证券");
            if (company.ListedDay >= 0)
            {
                string ticker = S.CompanyTicker(company);
                long shares = S.CompanyPlayerShares(company);
                Line(listing, $"报价 {S.CityInvestmentPrice(ticker)} · 持股 {shares:N0} / {S.CompanyShareCapital(company):N0}", 18);
                CompanyButton(listing, "前往股市", OpenCityInvestments);
                CompanyButton(listing,"股东名册 / 董事会 / 控制权",()=>{_companyPage=4;window.Rebuild();});
            }
            else
            {
                Line(listing, "未挂牌 · 创始人持股 100%", 17);
                Line(listing, S.CompanyListingBlocker(company, requireVisit: false) ?? "已满足上市资格", 16, Ui.Dim);
                CompanyCenterLink(listing);
            }
        }

        private void BuildCompanyStatistics(Transform body, CompanyRecord company)
        {
            int today = S.Clock.Today.TotalDays, first = today / 30 * 30;
            var periods = new[]
            {
                ("本经营月", S.CompanyAccounts(company, first, today + 1)),
                ("上个完整月", S.CompanyLastMonth(company))
            };
            foreach (var (label, accounts) in periods)
            {
                var card = CompanyCard(body, label);
                Line(card, $"流入 {accounts.Revenue}　支出 {accounts.Costs}　净额 {accounts.Revenue - accounts.Costs}", 19);
            }
            var projects = CompanyCard(body, "部门项目");
            foreach (var state in company.Departments)
            foreach (var project in S.CompanyDepartmentProjects(state.Department))
            {
                if (state.Project == (int)project.Id || state.Delivered(project.Id))
                    Line(projects, project.Name + (state.Project == (int)project.Id
                        ? $"　{state.WorkMinutes * 100 / project.Minutes}%" : "　已交付"), 17);
            }
        }

        private void BuildCompanyDepartments(Transform body, Window window, CompanyRecord company)
        {
            if(!S.PlayerManagesCompany(company))
            {Line(body,"公司部门由现任总经理负责",20,Ui.Accent);Line(body,"你可以从董事会查阅内部摘要、提出议案和任免经理。",16,Ui.Dim);CompanyButton(body,"股权与治理",()=>{_companyPage=4;window.Rebuild();});return;}
            var selector = Ui.Row(body, 52, 7);
            foreach (CompanyDepartment department in Enum.GetValues(typeof(CompanyDepartment)))
            {
                var button = Ui.Size(Ui.Button(selector, "", () => { _companyDepartment = department; _companyPerson = null; window.Rebuild(); },
                    16, Ui.SelectedButton(_companyDepartment == department)), 0, 52, 1);
                var label = Ui.Row(button.transform, 42, 5);
                label.Fill(7);
                var icon = Ui.Rect(label, "DepartmentIcon");
                Ui.Size(icon, 25, 25);
                icon.gameObject.AddComponent<CompanyDepartmentGlyph>().Configure(department, Ui.Accent);
                Ui.Size(Ui.Label(label, GameSession.CompanyDepartmentName(department) + "\n" + DepartmentPurpose[(int)department], 15), 0, 42, 1);
            }
            var current = company.Departments.FirstOrDefault(d => d.Department == _companyDepartment);
            string name = GameSession.CompanyDepartmentName(_companyDepartment);
            if (current == null)
            {
                var empty = CompanyCard(body, name);
                Line(empty, DepartmentAuthority[(int)_companyDepartment], 17);
                CompanyButton(empty, "设立部门", () =>
                {
                    if (S.EstablishCompanyDepartment(company.BrandId, _companyDepartment)) window.Rebuild();
                }, primary: true);
                return;
            }

            var layout = Ui.Row(body, -1, 10);
            var staff = CompanyCard(layout, name + " · 成员席位", 1.2f);
            var work = CompanyCard(layout, "负责人权限", 1);
            var members = company.Posts.Where(p => p.InDepartment(current.Department)).ToArray();
            var leader = members.FirstOrDefault(p => p.Leader);
            var pending = company.PendingPosts.Where(p => p.InDepartment(current.Department)).ToArray();
            var ordinary = members.Where(p => !p.Leader).ToArray();
            Line(staff, "负责人 · 管理岗", 17, Ui.Accent);
            if (leader != null) BuildDepartmentMember(staff, window, leader);
            if (leader == null && !pending.Any(p => p.Leader))
                CompanyButton(staff, "＋ 任命负责人", () => OpenCompanyStaffPicker(window, company, current.Department, true));
            Line(staff, $"普通成员 · {ordinary.Length} / {current.OrdinarySeats} 席", 17, Ui.Accent);
            foreach (var group in ordinary.Select((post, i) => (post, i)).GroupBy(x => x.i / 3))
            {
                var row = Ui.Row(staff, 50, 8);
                foreach (var entry in group) BuildDepartmentMember(row, window, entry.post);
            }
            if (ordinary.Length + pending.Count(p => !p.Leader && ordinary.All(x => x.EmployeeId != p.EmployeeId)) < current.OrdinarySeats)
                CompanyButton(staff, "＋ 从公司名册调入", () => OpenCompanyStaffPicker(window, company, current.Department, false));
            foreach (var post in pending.Where(p => !members.Any(m => m.EmployeeId == p.EmployeeId && m.Leader == p.Leader)))
            {
                var worker = S.FindEmployee(post.EmployeeId)?.Person;
                if (worker == null) continue;
                var row = Ui.Row(staff, 46, 7);
                PortraitThumb(row, S.PersonIdOf(worker), 40, () => { _companyPerson = worker.Id; window.Rebuild(); });
                Ui.Size(Ui.Label(row, worker.Name + " · 下个班次到岗", 16), 0, 42, 1);
                Ui.Size(Ui.Button(row, "撤回安排", () =>
                {
                    if (S.CancelCompanyPost(company.BrandId, worker.Id)) window.Rebuild();
                }, 15), 94, 34);
            }
            var selected = members.FirstOrDefault(p => p.EmployeeId == _companyPerson) ?? leader ?? ordinary.FirstOrDefault();
            if (selected != null && S.FindEmployee(selected.EmployeeId) is EmployeeAssignment assignment)
            {
                Line(staff, $"{assignment.Person.Name} · {DepartmentPosition(selected)}", 18, Ui.Accent);
                Line(staff, selected.Leader ? DepartmentAuthority[(int)current.Department] : DepartmentWork[(int)current.Department], 16);
                Line(staff, $"{Ui.Hm(assignment.Person.ShiftStart)}–{Ui.Hm(assignment.Person.ShiftEnd)} · 日薪 {assignment.Person.DailyWage}", 16, Ui.Dim);
                BuildEmployeeMorale(staff,assignment.Person,window);
                CompanyButton(staff,"员工档案 / 处理请求",()=>ShowEmployeeProfile(assignment.Person.Id));
                CompanyButton(staff, "调整岗位", () => OpenCompanyPostOptions(window, company, assignment),
                    S.PendingCompanyPostOf(assignment.Person.Id) != null ? "已有待生效安排" : null);
            }
            CompanyButton(staff, "扩编普通岗", () =>
            {
                if (S.ExpandCompanyDepartment(company.BrandId, current.Department)) window.Rebuild();
            }, current.OrdinarySeats >= 12 ? "普通岗编制已达上限" : null);
            BuildCompanyDepartmentWork(work, window, company, current, leader);
        }

        private void BuildDepartmentMember(Transform parent, Window window, CompanyPost post)
        {
            var person = S.FindEmployee(post.EmployeeId)?.Person;
            if (person == null) return;
            var row = Ui.Row(parent, 48, 5);
            Ui.Size(row, 0, 48, 1);
            Action show = () => { _companyPerson = person.Id; window.Rebuild(); };
            PortraitThumb(row, S.PersonIdOf(person), 42, show);
            Ui.Size(Ui.Button(row, person.Name, show, 15, Ui.SelectedButton(_companyPerson == person.Id)), 0, 37, 1);
        }

        private void BuildCompanyDepartmentWork(Transform panel, Window window, CompanyRecord company, CompanyDepartmentState state, CompanyPost leader)
        {
            string leaderBlocker = leader == null ? "请先任命负责人" : null;
            Line(panel, leader == null ? "负责人空缺" : S.FindEmployee(leader.EmployeeId)?.Person.Name ?? "负责人空缺", 18, Ui.Dim);
            Line(panel, DepartmentAuthority[(int)state.Department], 17);
            if (state.Department == CompanyDepartment.Finance)
            {
                Line(panel, "单项开支上限　" + Money.FromYuan(state.BudgetYuan), 19);
                CompanyButton(panel, "核定单项预算", () => OpenCompanyNumber("核定单项预算", "上限（元）",
                    "适用于之后申请的部门项目。", state.BudgetYuan, value => S.SetCompanyProjectBudget(company.BrandId, value), window), leaderBlocker, true);
                CompanyButton(panel, "查看公司账本", () => { _companyPage = 3; window.Rebuild(); });
                return;
            }
            if (state.Department == CompanyDepartment.Personnel)
            {
                CompanyButton(panel, "核准招聘", () => OpenCompanyRecruiting(company), leaderBlocker);
                CompanyButton(panel, "员工请求 · "+S.CompanyRoster(company).Count(r=>S.EmployeeNeedsAttention(r.Person)), () => OpenCompanyShiftRequests(window, company), leaderBlocker);
            }
            if (state.Department == CompanyDepartment.Marketing)
            {
                CompanyButton(panel, "展会、竞标与 KOL", () => { _brandId = company.BrandId; OpenBrandBusiness(); });
                if (state.PromotionUntilDay > S.Clock.Today.TotalDays)
                    Line(panel, $"当前到店客流 +15% · 剩余 {state.PromotionUntilDay - S.Clock.Today.TotalDays} 天", 17, Ui.Good);
            }
            if (state.Department == CompanyDepartment.Operations)
            {
                var shops = S.CompanyShops(company).ToArray();
                if (shops.Length > 0) Line(panel, $"公司门店平均口碑　{shops.Average(x => x.Sim.ShopReputation):0.#} / 100", 17);
            }
            foreach (var project in S.CompanyDepartmentProjects(state.Department))
            {
                Line(panel, project.Name, 19, Ui.Accent);
                Line(panel, project.Benefit, 16, Ui.Dim);
                if (state.Project == (int)project.Id)
                {
                    int percent = Math.Min(100, state.WorkMinutes * 100 / project.Minutes);
                    CompanyProgress(panel, percent);
                    Line(panel, $"执行中　{percent}% · 按成员实际班次推进", 16);
                    continue;
                }
                if (project.Id != CompanyProject.Promotion && state.Delivered(project.Id))
                {
                    Line(panel, "已交付", 16, Ui.Good);
                    continue;
                }
                var blocker = S.CompanyProjectBlocker(company, project);
                CompanyButton(panel, $"批准 · {Money.FromYuan(project.Cost)}", () => OpenCompanyProject(window, company, project), blocker, true);
                if (blocker != null && blocker != leaderBlocker) Line(panel, blocker, 15, Ui.Warn);
            }
        }

        private static void CompanyProgress(Transform parent, int percent)
        {
            var track = Ui.Size(Ui.Panel(parent, "ProjectProgress", Ui.ButtonBg), -1, 8);
            if (percent <= 0) return;
            var fill = Ui.Panel(track.transform, "ProgressFill", Ui.Good).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(percent / 100f, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        private void OpenCompanyProject(Window parent, CompanyRecord company, CompanyProjectDefinition project)
        {
            OpenWindow(project.Name, 620, 355, w =>
            {
                Line(w.Content, project.Benefit, 19, Ui.Accent);
                Line(w.Content, "拨款 " + Money.FromYuan(project.Cost) + " · 公司周转金", 17);
                var approvedBy = S.CompanyDepartmentLeader(company, project.Department);
                Line(w.Content, "审批人 " + S.FindEmployee(approvedBy)?.Person.Name, 17);
                CompanyButton(w.Content, "确认拨款", () =>
                {
                    if (!S.StartCompanyProject(company.BrandId, project.Id)) return;
                    w.Close(); parent.Rebuild();
                }, S.CompanyProjectBlocker(company, project), true);
            });
        }

        private static string DepartmentPosition(CompanyPost post) => post.ManagedStoreId != null ? "店长" :
            GameSession.CompanyDepartmentName(post.Department) + (post.Leader ? "负责人" : "专员");

        private string CompanyCurrentPosition(EmployeeAssignment row)
        {
            var post = S.CompanyPostOf(row.Person.Id);
            return post == null ? GameSession.RoleName(row.Role) : DepartmentPosition(post);
        }

        private void OpenCompanyStaffPicker(Window parent, CompanyRecord company, CompanyDepartment department, bool leader)
        {
            string selected = null;
            bool confirmVacancy = false;
            OpenWindow(leader ? "任命负责人" : "从公司名册调入", 840, 750, w =>
            {
                string target = GameSession.CompanyDepartmentName(department) + (leader ? "负责人 · 管理岗" : "专员 · 普通岗");
                Line(w.Content, $"{S.CompanyName(company)}　→　{target}", 20, Ui.Accent);
                var roster = S.CompanyRoster(company).OrderBy(x => S.CompanyPostOf(x.Person.Id)?.InDepartment(department) == true ? 0 : 1).ToArray();
                selected = roster.Any(x => x.Person.Id == selected) ? selected : null;
                foreach (var group in roster.Select((row, i) => (row, i)).GroupBy(x => x.i / 3))
                {
                    var cards = Ui.Row(w.Content, 70, 7);
                    foreach (var entry in group)
                    {
                        var row = entry.row;
                        string why = S.CompanyTransferBlocker(company, row.Person.Id, department, leader);
                        var tile = CompanyCard(cards, null, 1);
                        var line = Ui.Row(tile, 46, 5);
                        Action select = () => { selected = row.Person.Id; confirmVacancy = false; w.Rebuild(); };
                        PortraitThumb(line, S.PersonIdOf(row.Person), 42, why == null ? select : null, dimmed: why != null);
                        string source = row.Shop == null ? CompanyCurrentPosition(row) : S.ShopTitle(row.Shop);
                        Ui.Size(Ui.Button(line, row.Person.Name + "\n" + source, select, 14, Ui.SelectedButton(selected == row.Person.Id),
                            why == null).Tip(why), 0, 46, 1);
                    }
                }
                var chosen = roster.FirstOrDefault(x => x.Person.Id == selected);
                if (chosen == null) return;
                if (chosen.Shop != null)
                {
                    string shop = S.ShopTitle(chosen.Shop);
                    Line(w.Content, $"下个班次到任 · {shop}会空出{GameSession.RoleName(chosen.Role)}岗位", 17, Ui.Warn);
                    CompanyButton(w.Content, confirmVacancy ? "✓ 已确认原店空缺" : "确认原店空缺", () => { confirmVacancy = !confirmVacancy; w.Rebuild(); });
                }
                else Line(w.Content, $"{CompanyCurrentPosition(chosen)} → {target} · 下个班次生效", 17);
                CompanyButton(w.Content, leader ? "确认任命" : "确认调岗", () =>
                {
                    if (!S.ScheduleCompanyPost(company.BrandId, selected, department, leader, confirmVacancy)) return;
                    w.Close(); parent.Rebuild();
                }, chosen.Shop != null && !confirmVacancy ? "请确认原店空缺" : S.CompanyTransferBlocker(company, selected, department, leader), true);
            });
        }

        private void BuildCompanyRoster(Transform body, Window window, CompanyRecord company)
        {
            var all = S.CompanyRoster(company).ToArray();
            foreach(var employee in all)S.EmployeeWellbeingOf(employee.Person.Id);
            var groups = Ui.Row(body, 38, 7);
            int management = all.Count(r => S.CompanyPostOf(r.Person.Id)?.Leader == true);
            Ui.Tab(groups, $"管理岗 {management}", () => { _companyAttentionOnly=false;_companyManagers = true; _companyPerson = null; window.Rebuild(); }, _companyManagers&&!_companyAttentionOnly);
            Ui.Tab(groups, $"普通岗 {all.Length - management}", () => { _companyAttentionOnly=false;_companyManagers = false; _companyPerson = null; window.Rebuild(); }, !_companyManagers&&!_companyAttentionOnly);
            Ui.Tab(groups,"员工请求 "+all.Count(r=>S.EmployeeNeedsAttention(r.Person)),()=>{_companyAttentionOnly=true;_companyPerson=null;window.Rebuild();},_companyAttentionOnly);
            Ui.Size(Ui.Button(groups, _companyUnit == null ? "全部部门与门店" : "清除工作单位筛选",
                () => { if (_companyUnit == null) OpenCompanyUnitFilter(window, company); else { _companyUnit = null; window.Rebuild(); } }, 16), 210, 34);
            var roster = all.Where(r => (_companyAttentionOnly?S.EmployeeNeedsAttention(r.Person):(S.CompanyPostOf(r.Person.Id)?.Leader == true) == _companyManagers)
                && (_companyUnit == null || _companyUnit == CompanyUnitId(r))).ToArray();
            if(!roster.Any(r=>r.Person.Id==_companyPerson))_companyPerson=roster.FirstOrDefault()?.Person.Id;
            var layout = Ui.Row(body, -1, 10);
            Ui.Size(layout).GetComponent<LayoutElement>().flexibleHeight=1;
            var listShell = CompanyCard(layout, $"公司名册 · {roster.Length} / {all.Length} 人", .85f);
            var list=PhoneFlexibleScroll(listShell,"CompanyRosterList");
            foreach (var group in roster.Select((row, i) => (row, i)).GroupBy(x => x.i / 3))
            {
                var row = Ui.Row(list, 88, 6);
                foreach (var entry in group)
                {
                    var employee = entry.row.Person;
                    var tile = Ui.Rect(row,"CompanyEmployee_"+employee.Id);Ui.VList(tile,3,3);
                    Ui.Size(tile, 0, 88, 1);
                    Action show = () => { _companyPerson = employee.Id; window.Rebuild(); };
                    var face=Ui.Row(tile,40,4);PortraitThumb(face, S.PersonIdOf(employee), 38, show);
                    Ui.Size(Ui.Button(face, employee.Name, show, 14, Ui.SelectedButton(employee.Id == _companyPerson)), 0, 35, 1);
                    BuildEmployeeMorale(tile,employee,window,compact:true);
                }
            }
            var detailShell = CompanyCard(layout, "员工档案", 1.25f);
            var detail=PhoneFlexibleScroll(detailShell,"CompanyEmployeeDetail");
            var current = roster.FirstOrDefault(x => x.Person.Id == _companyPerson) ?? roster.FirstOrDefault();
            if (current == null) { Line(detail, "该范围暂无员工", 17, Ui.Dim); return; }
            Line(detail, current.Person.Name + "　" + CompanyCurrentPosition(current), 20, Ui.Accent);
            Line(detail, "所属公司　" + S.CompanyName(company), 17);
            Line(detail, "岗位类型　" + (_companyManagers ? "管理岗" : "普通岗"), 17);
            var managedStore = S.CompanyPostOf(current.Person.Id)?.ManagedStoreId;
            var assignedShop = current.Shop ?? (managedStore == null ? null : S.ShopAt(managedStore));
            Line(detail, (managedStore == null ? "工作单位　" : "分管门店　") +
                (assignedShop == null ? CompanyCurrentPosition(current) : S.ShopTitle(assignedShop)), 17);
            Line(detail, $"班次　{Ui.Hm(current.Person.ShiftStart)}–{Ui.Hm(current.Person.ShiftEnd)}　日薪 {current.Person.DailyWage}", 16);
            BuildEmployeeWellbeingDetails(detail,current,window);
            if(!S.PlayerManagesEmployee(current.Person.Id)){Line(detail,"该员工由公司管理层安排",16,Ui.Dim);return;}
            if (S.PendingCompanyPostOf(current.Person.Id) != null)
                CompanyButton(detail, "撤回待生效安排", () =>
                {
                    if (S.CancelCompanyPost(company.BrandId, current.Person.Id)) window.Rebuild();
                });
            else CompanyButton(detail, "调岗 / 任命", () => OpenCompanyPostOptions(window, company, current));
            CompanyButton(detail, "岗位培训 · " + S.CompanyTrainingCost(company), () =>
            {
                if (S.TrainCompanyEmployee(company.BrandId, current.Person.Id)) window.Rebuild();
            }, S.CompanyDepartmentLeader(company, CompanyDepartment.Personnel) == null ? "请先任命人事部负责人" : null);
            Ui.Size(Ui.Button(detail,"辞退…",()=>ConfirmAction("辞退公司员工","结清工资和加班费，解除雇佣及公司职务；原岗位将出现空缺。","确认辞退",()=>{S.DismissEmployee(current.Person.Id);window.RequestRebuild();},danger:true),16,Ui.ButtonDanger),-1,36);
        }

        private string CompanyUnitId(EmployeeAssignment row)
        {
            var post = S.CompanyPostOf(row.Person.Id);
            return post?.ManagedStoreId ?? (post != null ? "dept:" + post.Department : row.Shop?.Place.Id);
        }

        private void OpenCompanyUnitFilter(Window parent, CompanyRecord company)
        {
            OpenWindow("筛选工作单位", 600, 570, w =>
            {
                void Select(string id) { _companyUnit = id; _companyPerson = null; w.Close(); parent.Rebuild(); }
                foreach (var department in company.Departments)
                    CompanyButton(w.Content, GameSession.CompanyDepartmentName(department.Department), () => Select("dept:" + department.Department));
                foreach (var shop in S.CompanyShops(company))
                    CompanyButton(w.Content, S.ShopTitle(shop), () => Select(shop.Place.Id));
            });
        }

        private void OpenCompanyPostOptions(Window parent, CompanyRecord company, EmployeeAssignment row)
        {
            bool vacancy = false;
            OpenWindow(row.Person.Name + " · 调岗 / 任命", 750, 690, w =>
            {
                if (row.Shop != null)
                {
                    Line(w.Content, "原店　" + S.ShopTitle(row.Shop) + " · " + GameSession.RoleName(row.Role), 18);
                    CompanyButton(w.Content, vacancy ? "✓ 已确认原店空缺" : "确认原店空缺", () => { vacancy = !vacancy; w.Rebuild(); });
                }
                foreach (var d in company.Departments)
                {
                    var placements = Ui.Row(w.Content, 42, 6);
                    Ui.Size(Ui.Label(placements, GameSession.CompanyDepartmentName(d.Department), 17), 110, 40);
                    foreach (bool leader in new[] { false, true })
                    {
                        bool appoint = leader;
                        string blocker = row.Shop != null && !vacancy ? "请确认原店空缺"
                            : S.CompanyTransferBlocker(company, row.Person.Id, d.Department, appoint);
                        Ui.Size(Ui.Button(placements, appoint ? "任命负责人" : "调为普通专员", () =>
                        {
                            if (!S.ScheduleCompanyPost(company.BrandId, row.Person.Id, d.Department, appoint, vacancy)) return;
                            w.Close(); parent.Rebuild();
                        }, 16, Ui.ButtonBg, blocker == null).Tip(blocker), 0, 37, 1);
                    }
                }
                CompanyButton(w.Content, "任命店长", () => { w.Close(); OpenCompanyStoreManagerPicker(parent, company, row, vacancy); },
                    row.Shop != null && !vacancy ? "请确认原店空缺" : null);
                if (row.Shop == null)
                    CompanyButton(w.Content, "调回门店普通岗", () => { w.Close(); OpenCompanyStorePost(parent, company, row); });
            });
        }

        private void OpenCompanyStoreManagerPicker(Window parent, CompanyRecord company, EmployeeAssignment row, bool vacancy)
        {
            OpenWindow("任命公司店长", 630, 550, w =>
            {
                Line(w.Content, row.Person.Name + " · 管理岗 · 下个班次生效", 18);
                foreach (var shop in S.CompanyShops(company))
                    CompanyButton(w.Content, "分管 " + S.ShopTitle(shop), () =>
                    {
                        if (!S.ScheduleCompanyStoreManager(company.BrandId, row.Person.Id, shop.Place.Id, vacancy)) return;
                        w.Close(); parent.Rebuild();
                    }, S.CompanyStoreManagerBlocker(company, row.Person.Id, shop));
            });
        }

        private void OpenCompanyStorePost(Window parent, CompanyRecord company, EmployeeAssignment row)
        {
            OpenWindow("调回门店普通岗", 650, 685, w =>
            {
                foreach (var shop in S.CompanyShops(company))
                {
                    Line(w.Content, S.ShopTitle(shop), 18, Ui.Accent);
                    var jobs = Ui.Row(w.Content, 37, 6);
                    foreach (var role in new[] { EmployeeRole.Cashier, EmployeeRole.Assembler, EmployeeRole.Buyer, EmployeeRole.Salesperson, EmployeeRole.ModelPainter })
                    {
                        string blocker = S.EmployeeRoleShopBlocker(shop, role);
                        if (blocker != null) continue;
                        Ui.Size(Ui.Button(jobs, GameSession.RoleName(role), () =>
                        {
                            if (!S.ScheduleCompanyStorePost(company.BrandId, row.Person.Id, shop.Place.Id, role)) return;
                            w.Close(); parent.Rebuild();
                        }, 15), 0, 34, 1);
                    }
                }
            });
        }

        private void OpenCompanyRecruiting(CompanyRecord company)
        {
            OpenWindow("核准门店招聘", 630, 510, w =>
            {
                foreach (var shop in S.CompanyShops(company))
                    CompanyButton(w.Content, S.ShopTitle(shop) + $" · {S.StaffCommitted(shop)}/{GameSession.StaffLimit(shop.Place.Size)}",
                        () =>
                        {
                            if (S.ApproveCompanyRecruitment(company.BrandId, shop.Place.Id)) w.Rebuild();
                        }, S.CompanyRecruitmentBlocker(company, shop));
                if (S.RecruitmentNoticePosted) Line(w.Content,
                    $"{S.NextCandidateArrivalText} · {S.RecruitmentInterviewPlace?.Name ?? "门店"}到店面试", 17, Ui.Good);
            });
        }

        private void OpenCompanyShiftRequests(Window parent, CompanyRecord company)
        {
            OpenWindow("员工请求", 760, 600, w =>
            {
                var list=PhoneFlexibleScroll(w.Content,"CompanyRequestsList");
                var requests = S.CompanyRoster(company).Where(x=>S.EmployeeNeedsAttention(x.Person)).ToArray();
                if(requests.Length==0)Line(list,"暂无需处理员工",17,Ui.Dim);
                foreach(var row in requests)
                {
                    CompanyButton(list,row.Person.Name+" · "+S.EmployeeAttentionLabel(row.Person),()=>
                    {_companyPage=2;_companyPerson=row.Person.Id;_companyAttentionOnly=true;_companyUnit=null;w.Close();parent.Rebuild();});
                }
            });
        }

        private void OpenCompanyNumber(string title, string field, string hint, int value, Func<int, bool> save, Window parent)
        {
            OpenWindow(title, 580, 330, w =>
            {
                Para(w.Content, hint, 16, Ui.Dim);
                Line(w.Content, field, 18);
                var input = Ui.TextInput(w.Content, value.ToString(), field, null);
                Ui.Size(input, -1, 41);
                CompanyButton(w.Content, "确认", () =>
                {
                    if (!int.TryParse(input.text, out int yuan) || yuan < 0) { Toast("请输入有效的金额。"); return; }
                    if (save(yuan)) { w.Close(); parent.Rebuild(); }
                }, primary: true);
            });
        }
    }
}
