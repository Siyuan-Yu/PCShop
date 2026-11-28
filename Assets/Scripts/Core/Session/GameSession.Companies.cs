using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum CompanyDepartment { Personnel, Research, Finance, Operations, Marketing }
    public enum CompanyProject { Training, NewProduct, Workflow, Service, Promotion }

    [Serializable]
    public sealed class CompanyDepartmentState
    {
        public CompanyDepartment Department;
        public int OrdinarySeats = 2;
        public int BudgetYuan = 45000;
        public int Project = -1, WorkMinutes, Completed;
        public double WorkMinuteRemainder;
        public int PromotionUntilDay = -1;
        public CompanyDepartmentState Copy() => (CompanyDepartmentState)MemberwiseClone();
        public bool Delivered(CompanyProject project) => (Completed & (1 << (int)project)) != 0;
    }

    [Serializable]
    public sealed class CompanyPost
    {
        public string EmployeeId;
        public CompanyDepartment Department;
        public bool Leader;
        public long EffectiveAt;
        public string StoreId, ManagedStoreId;
        public EmployeeRole StoreRole;
        public CompanyPost Copy()
        {
            var post = (CompanyPost)MemberwiseClone();
            // JsonUtility restores absent string fields as empty strings.
            if (string.IsNullOrEmpty(post.StoreId)) post.StoreId = null;
            if (string.IsNullOrEmpty(post.ManagedStoreId)) post.ManagedStoreId = null;
            return post;
        }
        public bool InDepartment(CompanyDepartment department) => ManagedStoreId == null && StoreId == null && Department == department;
    }

    [Serializable]
    public sealed class CompanyExpense
    {
        public int Day;
        public long Cents;
        public string Label;
        public CompanyExpense Copy() => (CompanyExpense)MemberwiseClone();
    }

    [Serializable]
    public sealed class CompanyShopHistory
    {
        public string PlaceId, SalesAccountId;
        public int OpenedDay = -1;
        public int ClosedDay = -1;
        public CompanyShopHistory Copy() => (CompanyShopHistory)MemberwiseClone();
    }

    [Serializable]
    public sealed class CompanyRecord
    {
        public string BrandId;
        public string[] ShopIds = Array.Empty<string>();
        public CompanyShopHistory[] StoreHistory = Array.Empty<CompanyShopHistory>();
        public string[] UnassignedEmployees = Array.Empty<string>();
        public CompanyDepartmentState[] Departments = Array.Empty<CompanyDepartmentState>();
        public CompanyPost[] Posts = Array.Empty<CompanyPost>();
        public CompanyPost[] PendingPosts = Array.Empty<CompanyPost>();
        public CompanyExpense[] Expenses = Array.Empty<CompanyExpense>();
        public long ReserveCents, ListingPriceCents;
        public int ListedDay = -1;
        public int CashAccountingFromDay = -1;
        public CompanyRecord Copy() => new CompanyRecord
        {
            BrandId = BrandId, ShopIds = (ShopIds ?? Array.Empty<string>()).ToArray(),
            StoreHistory = (StoreHistory ?? Array.Empty<CompanyShopHistory>()).Select(x => x.Copy()).ToArray(),
            UnassignedEmployees = (UnassignedEmployees ?? Array.Empty<string>()).ToArray(),
            Departments = (Departments ?? Array.Empty<CompanyDepartmentState>()).Select(x => x.Copy()).ToArray(),
            Posts = (Posts ?? Array.Empty<CompanyPost>()).Select(x => x.Copy()).ToArray(),
            PendingPosts = (PendingPosts ?? Array.Empty<CompanyPost>()).Select(x => x.Copy()).ToArray(),
            Expenses = (Expenses ?? Array.Empty<CompanyExpense>()).Select(x => x.Copy()).ToArray(),
            ReserveCents = ReserveCents, ListingPriceCents = ListingPriceCents, ListedDay = ListedDay,CashAccountingFromDay=CashAccountingFromDay
        };
    }

    public readonly struct CompanyProjectDefinition
    {
        public readonly CompanyProject Id;
        public readonly CompanyDepartment Department;
        public readonly string Name, Benefit;
        public readonly int Cost, Minutes;
        public CompanyProjectDefinition(CompanyProject id, CompanyDepartment department, string name, string benefit, int cost, int minutes)
        { Id = id; Department = department; Name = name; Benefit = benefit; Cost = cost; Minutes = minutes; }
    }

    public sealed partial class SaveData { public CompanyRecord[] companies; }

    public sealed partial class GameSession
    {
        public const string EnterpriseCenterId = "enterprise_service_center";
        public const long CompanyFounderShares = 1000000L;
        public const long CompanyPublicShares = 250000L;
        public const long CompanyTotalShares = CompanyFounderShares + CompanyPublicShares;
        private readonly List<CompanyRecord> _companies = new List<CompanyRecord>();
        private static readonly CompanyProjectDefinition[] CompanyProjects =
        {
            new CompanyProjectDefinition(CompanyProject.Training, CompanyDepartment.Personnel, "岗位带教", "公司员工培训费用下降 25%", 1200, 960),
            new CompanyProjectDefinition(CompanyProject.NewProduct, CompanyDepartment.Research, "自有新品", "公司门店开放自有商品", 32000, 2400),
            new CompanyProjectDefinition(CompanyProject.Workflow, CompanyDepartment.Research, "流程优化", "公司门店生产或服务工时缩短 10%", 20000, 1800),
            new CompanyProjectDefinition(CompanyProject.Service, CompanyDepartment.Operations, "接待与售后整改", "当前所属门店口碑 +6", 24000, 1440),
            new CompanyProjectDefinition(CompanyProject.Promotion, CompanyDepartment.Marketing, "周末到店宣传", "到店客流 +15%，持续到下次月结", 18000, 960)
        };
        public IReadOnlyList<CompanyRecord> Companies => _companies;
        public bool AtEnterpriseCenter => CurrentPlace?.Id == EnterpriseCenterId;
        public Place EnterpriseCenter => City.HasPlace(EnterpriseCenterId) ? City.GetPlace(EnterpriseCenterId) : null;
        public CompanyRecord Company(string id) => _companies.Find(x => x.BrandId == id);
        public string CompanyName(CompanyRecord company) => Brand(company?.BrandId)?.Name ?? "公司";
        public static string CompanyDepartmentName(CompanyDepartment department) => department switch
        {
            CompanyDepartment.Personnel => "人事部",
            CompanyDepartment.Research => "研发部",
            CompanyDepartment.Finance => "财务部",
            CompanyDepartment.Operations => "运营部",
            _ => "市场部"
        };
        public static string CompanyIndustryName(string kind) => kind == "computer" ? "电脑" : kind == "clothing" ? "服装" : "模型手办";
        public IEnumerable<CompanyProjectDefinition> CompanyDepartmentProjects(CompanyDepartment department) =>
            CompanyProjects.Where(x => x.Department == department);
        public IEnumerable<OwnedShop> CompanyShops(CompanyRecord company) => company == null
            ? Enumerable.Empty<OwnedShop>()
            : MyShops.Where(s => company.ShopIds.Contains(s.Place.Id) && s.RetailKind == Brand(company.BrandId)?.Kind);
        public CompanyRecord CompanyOfShop(OwnedShop shop) => shop == null || !shop.Rented ? null :
            _companies.Find(c => c.ShopIds.Contains(shop.Place.Id) && Brand(c.BrandId)?.Kind == shop.RetailKind);
        public CompanyPost CompanyPostOf(string employeeId) =>
            _companies.SelectMany(x => x.Posts).FirstOrDefault(x => x.EmployeeId == employeeId);
        public CompanyPost PendingCompanyPostOf(string employeeId) =>
            _companies.SelectMany(x => x.PendingPosts).FirstOrDefault(x => x.EmployeeId == employeeId);
        private CompanyRecord CompanyEmployerOf(EmployeeAssignment row)
        {
            if (row == null) return null;
            if (row.Shop != null) return CompanyOfShop(row.Shop);
            return _companies.Find(c => c.Posts.Any(p => p.EmployeeId == row.Person.Id) || c.UnassignedEmployees.Contains(row.Person.Id))
                ?? CompanyOfShop(_employeeHireShops.TryGetValue(row.Person.Id, out string shopId) ? ShopAt(shopId) : null);
        }
        public IEnumerable<EmployeeAssignment> CompanyRoster(CompanyRecord company) =>
            company == null ? Enumerable.Empty<EmployeeAssignment>() : Employees.Where(row => CompanyEmployerOf(row) == company);
        public CompanyRecord CompanyOfEmployee(string employeeId) => CompanyEmployerOf(FindEmployee(employeeId));

        private void EnsureCompany(BrandRecord brand, IEnumerable<string> shops)
        {
            if (Company(brand.Id) != null) return;
            _companies.Add(new CompanyRecord
            {
                BrandId = brand.Id, ShopIds = shops.Distinct().ToArray(),
                StoreHistory = shops.Distinct().Select(id=>CompanyStoreSnapshot(id,brand.Founded)).ToArray(),CashAccountingFromDay=Clock.Today.TotalDays,
                Departments = new[] { new CompanyDepartmentState { Department = CompanyDepartment.Personnel } }
            });
        }

        private CompanyShopHistory CompanyStoreSnapshot(string id,int? openedDay=null) =>
            new CompanyShopHistory { PlaceId = id, SalesAccountId = ShopAt(id)?.SalesAccountId,OpenedDay=openedDay??Clock.Today.TotalDays };

        private void RetireCompanyShop(OwnedShop shop)
        {
            var company = CompanyOfShop(shop);
            if (company == null) return;
            company.UnassignedEmployees = company.UnassignedEmployees.Concat(CompanyRoster(company)
                .Where(row => row.Shop == shop || row.Shop == null && CompanyPostOf(row.Person.Id) == null)
                .Select(row => row.Person.Id)).Distinct().ToArray();
            company.ShopIds = company.ShopIds.Where(id => id != shop.Place.Id).ToArray();
            var history = company.StoreHistory.FirstOrDefault(x => x.PlaceId == shop.Place.Id && x.ClosedDay < 0);
            if (history != null) history.ClosedDay = Clock.Today.TotalDays;
            company.UnassignedEmployees = company.UnassignedEmployees.Concat(company.Posts
                .Where(p => p.ManagedStoreId == shop.Place.Id).Select(p => p.EmployeeId)).Distinct().ToArray();
            company.Posts = company.Posts.Where(p => p.ManagedStoreId != shop.Place.Id).ToArray();
            company.PendingPosts = company.PendingPosts.Where(p => p.StoreId != shop.Place.Id && p.ManagedStoreId != shop.Place.Id).ToArray();
        }

        private void RestoreCompanies(SaveData data)
        {
            _companies.Clear();
            foreach (var record in data.companies ?? Array.Empty<CompanyRecord>())
                if (record != null && Brand(record.BrandId) != null && Company(record.BrandId) == null)
                    _companies.Add(record.Copy());
            foreach (var brand in _brands.Brands) EnsureCompany(brand, brand.ShopIds ?? Array.Empty<string>());
            foreach (var company in _companies.Where(x => x.StoreHistory.Length == 0))
                company.StoreHistory = company.ShopIds.Select(id=>CompanyStoreSnapshot(id,Brand(company.BrandId).Founded)).ToArray();
        }

        public string CompanyRegistrationBlocker(string kind) =>
            !AtEnterpriseCenter ? "请到湖城企业服务中心的公司登记柜台" : BrandFoundBlocker(kind);

        public bool RegisterCompany(string kind, string name, string position, IEnumerable<string> shopIds)
        {
            var blocker = CompanyRegistrationBlocker(kind);
            if (blocker != null) return Fail(blocker);
            var eligible = MyShops.Where(x => x.RetailKind == kind && CompanyOfShop(x) == null)
                .Select(x => x.Place.Id).ToHashSet();
            var selected = (shopIds ?? Enumerable.Empty<string>()).Distinct().ToArray();
            if (selected.Length == 0 || selected.Any(x => !eligible.Contains(x))) return Fail("请选择至少一家同业自营门店");
            return CreateBrandAndCompany(kind, name, position, selected);
        }

        public bool EstablishCompanyDepartment(string id, CompanyDepartment department)
        {
            var company = Company(id);
            if(CompanyOfficeBlocker(company) is string authority)return Fail(authority);
            if (IsGameOver || company == null || !Enum.IsDefined(typeof(CompanyDepartment), department)
                || company.Departments.Any(x => x.Department == department)) return false;
            company.Departments = company.Departments.Append(new CompanyDepartmentState { Department = department }).ToArray();
            return true;
        }

        public bool ExpandCompanyDepartment(string id, CompanyDepartment department)
        {
            if(CompanyOfficeBlocker(Company(id)) is string authority)return Fail(authority);
            var state = Company(id)?.Departments.FirstOrDefault(x => x.Department == department);
            if (IsGameOver || state == null || state.OrdinarySeats >= 12) return false;
            state.OrdinarySeats++;
            return true;
        }

        public string CompanyTransferBlocker(CompanyRecord company, string employeeId, CompanyDepartment department, bool leader)
        {
            if(CompanyOfficeBlocker(company) is string authority)return authority;
            var row = FindEmployee(employeeId);
            if (IsGameOver || company == null || CompanyEmployerOf(row) != company) return "请选择本公司的员工";
            if (PendingCompanyPostOf(employeeId) != null || row.Person.TransferDay >= 0) return "该员工已有待生效的调岗";
            if (EmployeeTrip(employeeId) != null) return "该员工尚未结束本次店外工作";
            if (BrandExpoPersonReserved(PersonIdOf(row.Person))) return "该员工已有展会安排";
            var state = company.Departments.FirstOrDefault(x => x.Department == department);
            if (state == null) return "请先设立该部门";
            var post = CompanyPostOf(employeeId);
            if (post?.InDepartment(department) == true && post.Leader == leader) return "已在该岗位";
            int seats = leader ? 1 : state.OrdinarySeats;
            var occupied = company.Posts.Concat(company.PendingPosts)
                .Where(x => x.EmployeeId != employeeId && x.InDepartment(department) && x.Leader == leader)
                .Select(x => x.EmployeeId).Distinct().Count();
            return occupied < seats ? null : leader ? "负责人席位已占用" : "普通席位已满";
        }

        public bool ScheduleCompanyPost(string id, string employeeId, CompanyDepartment department, bool leader, bool confirmVacancy)
        {
            var company = Company(id);
            var blocker = CompanyTransferBlocker(company, employeeId, department, leader);
            if (blocker != null) return Fail(blocker);
            var row = FindEmployee(employeeId);
            if (row.Shop != null && !confirmVacancy) return Fail("请确认原门店会空出该岗位");
            QueueCompanyPost(company, row.Person, new CompanyPost { EmployeeId = employeeId, Department = department, Leader = leader });
            return true;
        }

        public bool ScheduleCompanyStorePost(string id, string employeeId, string storeId, EmployeeRole role)
        {
            var company = Company(id);
            if(CompanyOfficeBlocker(company) is string authority)return Fail(authority);
            var employee = FindEmployee(employeeId);
            if (IsGameOver || company == null || CompanyEmployerOf(employee) != company || employee.Shop != null
                || CompanyOfShop(ShopAt(storeId)) != company || PendingCompanyPostOf(employeeId) != null) return false;
            string blocker = EmployeeWorkAssignmentBlocker(employeeId, storeId, role, companyTransfer: true);
            if (role == EmployeeRole.Unassigned || blocker != null) return Fail(blocker ?? "请选择有效的门店普通岗");
            QueueCompanyPost(company, employee.Person, new CompanyPost { EmployeeId = employeeId, StoreId = storeId, StoreRole = role });
            return true;
        }

        public string CompanyStoreManager(OwnedShop shop) => CompanyOfShop(shop)?.Posts
            .FirstOrDefault(x => x.ManagedStoreId == shop.Place.Id)?.EmployeeId;

        public string CompanyStoreManagerBlocker(CompanyRecord company, string employeeId, OwnedShop shop)
        {
            if(CompanyOfficeBlocker(company) is string authority)return authority;
            var row = FindEmployee(employeeId);
            if (IsGameOver || company == null || CompanyEmployerOf(row) != company || CompanyOfShop(shop) != company) return "请选择本公司的员工及门店";
            if (PendingCompanyPostOf(employeeId) != null || row.Person.TransferDay >= 0) return "该员工已有待生效的调岗";
            if (EmployeeTrip(employeeId) != null || BrandExpoPersonReserved(PersonIdOf(row.Person))) return "该员工还有店外安排";
            if (company.Posts.Concat(company.PendingPosts).Any(x => x.ManagedStoreId == shop.Place.Id)) return "该店店长席位已占用";
            return null;
        }

        public bool ScheduleCompanyStoreManager(string id, string employeeId, string storeId, bool confirmVacancy)
        {
            var company = Company(id);
            var blocker = CompanyStoreManagerBlocker(company, employeeId, ShopAt(storeId));
            if (blocker != null) return Fail(blocker);
            var row = FindEmployee(employeeId);
            if (row.Shop != null && !confirmVacancy) return Fail("请确认原门店会空出该岗位");
            QueueCompanyPost(company, row.Person, new CompanyPost { EmployeeId = employeeId, Leader = true, ManagedStoreId = storeId });
            return true;
        }

        private void QueueCompanyPost(CompanyRecord company, Employee employee, CompanyPost post)
        {
            int shiftStart = employee.Schedule?.PendingAt >= 0 ? employee.Schedule.PendingStart : employee.ShiftStart;
            long now = Clock.Now.TotalMinutes, earliest = Math.Max(now + 1, employee.Schedule?.NextNotBefore ?? -1);
            if (employee.TryShift(now, out _, out long end)) earliest = Math.Max(earliest, end);
            long at = earliest / 1440 * 1440 + shiftStart;
            post.EffectiveAt = at >= earliest ? at : at + 1440;
            company.PendingPosts = company.PendingPosts.Append(post).ToArray();
            Say(employee.Name + "的岗位将在下一实际班次生效。");
        }

        public bool CancelCompanyPost(string id, string employeeId)
        {
            if(CompanyOfficeBlocker(Company(id)) is string authority)return Fail(authority);
            var company = Company(id);
            if (IsGameOver || company == null || !company.PendingPosts.Any(x => x.EmployeeId == employeeId)) return false;
            company.PendingPosts = company.PendingPosts.Where(x => x.EmployeeId != employeeId).ToArray();
            return true;
        }

        private void ForgetCompanyEmployee(string employeeId)
        {
            foreach (var company in _companies)
            {
                company.Posts = company.Posts.Where(x => x.EmployeeId != employeeId).ToArray();
                company.PendingPosts = company.PendingPosts.Where(x => x.EmployeeId != employeeId).ToArray();
                company.UnassignedEmployees = company.UnassignedEmployees.Where(x => x != employeeId).ToArray();
            }
        }

        private void ApplyCompanyPosts(GameTimestamp now)
        {
            foreach (var company in _companies)
            foreach (var post in company.PendingPosts)
            {
                if (post.EffectiveAt > now.TotalMinutes) continue;
                var row = FindEmployee(post.EmployeeId);
                if (row != null && CompanyEmployerOf(row) == company)
                {
                    if (!row.Person.OnDuty(now) || EmployeeTrip(post.EmployeeId) != null
                        || BrandExpoPersonReserved(PersonIdOf(row.Person))) continue;
                    if (post.ManagedStoreId != null && CompanyOfShop(ShopAt(post.ManagedStoreId)) != company)
                    {
                        CancelCompanyPost(company.BrandId, post.EmployeeId);
                        continue;
                    }
                    if (post.StoreId != null)
                    {
                        var shop = ShopAt(post.StoreId);
                        if (CompanyOfShop(shop) != company || EmployeeWorkAssignmentBlocker(post.EmployeeId, post.StoreId, post.StoreRole, companyTransfer: true) != null)
                        {
                            CancelCompanyPost(company.BrandId, post.EmployeeId);
                            Say(row.Person.Name + "的门店岗位已不可用，原岗位保留。");
                            continue;
                        }
                        MoveEmployeeWork(row, shop, post.StoreRole);
                    }
                    else
                    {
                        if (row.Shop != null)
                        {
                            MoveEmployeeWork(row, null, EmployeeRole.Unassigned);
                            row.Shop.WorkingAssemblers.Remove(post.EmployeeId);
                            ClearUnstaffedAssembly(row.Shop);
                        }
                        _employeeHireShops.Remove(post.EmployeeId);
                    }
                    company.UnassignedEmployees = company.UnassignedEmployees.Where(x => x != post.EmployeeId).ToArray();
                    company.Posts = company.Posts.Where(x => x.EmployeeId != post.EmployeeId)
                        .Concat(post.StoreId == null ? new[] { post } : Array.Empty<CompanyPost>()).ToArray();
                    Say(row.Person.Name + "已到任：" + (post.ManagedStoreId != null ? "店长" : post.StoreId == null
                        ? CompanyDepartmentName(post.Department) + (post.Leader ? "负责人" : "专员") : RoleName(post.StoreRole)) + "。");
                }
                company.PendingPosts = company.PendingPosts.Where(x => x != post).ToArray();
            }
        }

        public string CompanyDepartmentLeader(CompanyRecord company, CompanyDepartment department) => company?.Posts
            .FirstOrDefault(p => p.InDepartment(department) && p.Leader)?.EmployeeId;

        public string CompanyRecruitmentBlocker(CompanyRecord company, OwnedShop shop)
        {
            if(CompanyOfficeBlocker(company) is string authority)return authority;
            if (company == null || IsGameOver || CompanyOfShop(shop) != company) return "请选择本公司的门店";
            if (CompanyDepartmentLeader(company, CompanyDepartment.Personnel) == null) return "请先任命人事部负责人";
            if (RecruitmentNoticePosted) return "已有一批应聘者待到达";
            if (_candidates.Any(CandidateAvailable)) return "请先面试现有应聘者";
            return StaffCapacityBlocker(shop);
        }

        public bool ApproveCompanyRecruitment(string id, string shopId)
        {
            var company = Company(id);
            var shop = ShopAt(shopId);
            string blocker = CompanyRecruitmentBlocker(company, shop);
            if (blocker != null) return Fail(blocker);
            _recruitmentArrivalAt = GameTimestamp.From(Clock.Today.AddDays(1), CandidateMinute).TotalMinutes;
            _recruitmentShopId = shop.Place.Id;
            Say(CompanyName(company) + "已核准" + ShopTitle(shop) + "的招聘告示。明日可按现有到店面试流程聘用。");
            return true;
        }

        public bool SetCompanyProjectBudget(string id, int yuan)
        {
            if(CompanyOfficeBlocker(Company(id)) is string authority)return Fail(authority);
            var company = Company(id);
            var finance = company?.Departments.FirstOrDefault(d => d.Department == CompanyDepartment.Finance);
            if (IsGameOver || finance == null || CompanyDepartmentLeader(company, CompanyDepartment.Finance) == null
                || yuan < 0 || yuan > 1000000) return false;
            finance.BudgetYuan = yuan;
            return true;
        }

        public string CompanyProjectBlocker(CompanyRecord company, CompanyProjectDefinition project)
        {
            if(CompanyOfficeBlocker(company) is string authority)return authority;
            var state = company?.Departments.FirstOrDefault(x => x.Department == project.Department);
            if (IsGameOver || state == null) return "请先设立部门";
            if (CompanyDepartmentLeader(company, project.Department) == null) return "请先任命负责人";
            if (state.Project >= 0) return "本部门正在执行项目";
            if (project.Id == CompanyProject.Promotion ? state.PromotionUntilDay > Clock.Today.TotalDays : state.Delivered(project.Id))
                return project.Id == CompanyProject.Promotion ? "宣传仍在生效" : "已交付";
            if (!CompanyShops(company).Any()) return "请先经营一家公司门店";
            int limit = company.Departments.FirstOrDefault(x => x.Department == CompanyDepartment.Finance)?.BudgetYuan ?? 45000;
            if (project.Cost > limit) return "超出财务核定的单项预算";
            return company.ReserveCents < Money.FromYuan(project.Cost).Cents ? "公司周转金不足" : null;
        }

        public bool FundCompany(string id, int yuan)
        {
            var company = Company(id);
            if (IsGameOver || company == null || yuan <= 0) return false;
            var amount = Money.FromYuan(yuan);
            if (!Ledger.TryDebit(Clock.Today, "企业周转金", amount, CompanyName(company),
                new LedgerAttribution(LedgerBusinessKind.InternalTransfer, activityId: company.BrandId))) return Fail("个人现金不足");
            company.ReserveCents += amount.Cents;
            return true;
        }

        public bool StartCompanyProject(string id, CompanyProject projectId)
        {
            if (!Enum.IsDefined(typeof(CompanyProject), projectId)) return false;
            var company = Company(id);
            var project = CompanyProjects[(int)projectId];
            string blocker = CompanyProjectBlocker(company, project);
            if (blocker != null) return Fail(blocker);
            ChargeCompanyReserve(company, Money.FromYuan(project.Cost), project.Name);
            var state = company.Departments.First(x => x.Department == project.Department);
            state.Project = (int)project.Id;
            state.WorkMinutes = 0;
            state.WorkMinuteRemainder = 0;
            return true;
        }

        private void ChargeCompanyReserve(CompanyRecord company, Money cost, string label)
        {
            company.ReserveCents -= cost.Cents;
            company.Expenses = company.Expenses.Append(new CompanyExpense
                { Day = Clock.Today.TotalDays, Cents = cost.Cents, Label = label }).ToArray();
            Ledger.RecordNonCash(Clock.Today, "公司支出", label + " · " + cost,
                new LedgerAttribution(LedgerBusinessKind.InternalTransfer, activityId: company.BrandId, transactionKind: "公司周转金支出"));
        }

        private void TickCompanyWork(GameTimestamp from, GameTimestamp to)
        {
            if (_companies.Count == 0) return;
            if (_companies.Any(c => c.PendingPosts.Any(p => p.EffectiveAt <= to.TotalMinutes))) ApplyCompanyPosts(to);
            foreach (var company in _companies)
            foreach (var department in company.Departments.Where(x => x.Project >= 0))
            {
                var project = CompanyProjects[department.Project];
                foreach (var post in company.Posts.Where(x => x.InDepartment(department.Department)))
                {
                    var employee = FindEmployee(post.EmployeeId)?.Person;
                    if (employee != null && employee.OnDuty(from) && !BrandExpoPersonReserved(PersonIdOf(employee)))
                        department.WorkMinuteRemainder += from.MinutesUntil(to) * EmployeeWorkRate(employee);
                }
                int wholeMinutes=(int)Math.Floor(department.WorkMinuteRemainder);
                department.WorkMinutes+=wholeMinutes;department.WorkMinuteRemainder-=wholeMinutes;
                if (department.WorkMinutes < project.Minutes) continue;
                department.WorkMinutes = project.Minutes;
                department.Project = -1;
                department.Completed |= 1 << (int)project.Id;
                if (project.Id == CompanyProject.Service)
                    foreach (var shop in CompanyShops(company)) shop.Sim.AdjustShopReputation(6);
                if (project.Id == CompanyProject.Promotion)
                    department.PromotionUntilDay = (Clock.Today.TotalDays / 30 + 1) * 30;
                RefreshBrandTraffic();
                Say(CompanyName(company) + " · " + project.Name + "已交付。");
            }
        }

        public double CompanyPromotionTraffic(OwnedShop shop)
        {
            var company=CompanyOfShop(shop);if(company==null)return 0;
            bool active=company.Departments.Any(x=>x.Department==CompanyDepartment.Marketing&&x.PromotionUntilDay>Clock.Today.TotalDays)
                ||(GovernanceOf(CompanyTicker(company))?.PromotionUntilDay??-1)>Clock.Today.TotalDays;
            return active ? .15 : 0;
        }
        public bool CompanyResearchDelivered(OwnedShop shop, CompanyProject project) => CompanyOfShop(shop)?.Departments
            .Any(x => x.Department == CompanyDepartment.Research && x.Delivered(project)) == true;
        public GiftDef CompanyRetailProduct(OwnedShop shop) => !CompanyResearchDelivered(shop, CompanyProject.NewProduct) ? null
            : shop.RetailKind == "models" ? RetailProducts.CompanyModel : shop.RetailKind == "clothing" ? RetailProducts.CompanyApparel : null;

        public bool MakeCompanyProduct(OwnedShop shop, int quantity)
        {
            if(ShopAuthorityBlocker(shop) is string authority)return Fail(authority);
            var product = CompanyRetailProduct(shop);
            var company = CompanyOfShop(shop);
            if (IsGameOver || product == null || company == null || quantity < 1 || quantity > 100) return false;
            var cost = product.WholesalePrice * quantity;
            if (company.ReserveCents < cost.Cents) return Fail("公司周转金不足");
            if (!shop.Sim.Stock.TryAddGoods(product.Id, quantity)) return Fail("该门店无法接收这批商品");
            ChargeCompanyReserve(company, cost, product.Name + "生产");
            Say($"{ShopTitle(shop)}后仓已收到 {quantity} 件{product.Name}，可按正常流程上架。");
            return true;
        }
        public Money CompanyTrainingCost(CompanyRecord company) => Money.FromYuan(company?.Departments
            .Any(x => x.Department == CompanyDepartment.Personnel && x.Delivered(CompanyProject.Training)) == true ? 450 : 600);

        public bool TrainCompanyEmployee(string id, string employeeId)
        {
            if(EmployeeAuthorityBlocker(employeeId) is string authority)return Fail(authority);
            var company = Company(id);
            var row = FindEmployee(employeeId);
            if (IsGameOver || company == null || CompanyEmployerOf(row) != company
                || CompanyDepartmentLeader(company, CompanyDepartment.Personnel) == null) return false;
            if (row.Person.Intelligence >= 100) return Fail("该员工的岗位技能已达上限");
            var cost = CompanyTrainingCost(company);
            if (company.ReserveCents < cost.Cents) return Fail("公司周转金不足");
            ChargeCompanyReserve(company, cost, row.Person.Name + "岗位培训");
            row.Person.Intelligence = Math.Min(100, row.Person.Intelligence + 2);
            Say(row.Person.Name + "完成岗位培训，智力 +2。");
            return true;
        }
        public Money CompanyReserves => Money.FromCents(_companies.Where(x=>x.ListedDay<0).Sum(x => x.ReserveCents));

        public (Money Revenue, Money Costs) CompanyAccounts(CompanyRecord company, int firstDay, int untilDay)
        {
            if (company == null) return (Money.Zero, Money.Zero);
            int founded = Brand(company.BrandId)?.Founded ?? 0;
            var entries = Ledger.Entries.Where(e => e.Date.TotalDays >= Math.Max(founded, firstDay) && e.Date.TotalDays < untilDay
                && (e.CashAccountId==company.BrandId||e.CashAccountId==null&&(company.CashAccountingFromDay<0||e.Date.TotalDays<company.CashAccountingFromDay))
                && (e.Attribution?.ActivityId == company.BrandId || e.ShopId != null && company.StoreHistory.Any(shop =>
                    shop.PlaceId == e.ShopId && (shop.ClosedDay < 0 || shop.ClosedDay >= e.Date.TotalDays)
                    && (shop.OpenedDay<0||shop.OpenedDay<=e.Date.TotalDays)
                    && (shop.SalesAccountId == null || e.Attribution?.SalesAccountId == null || shop.SalesAccountId == e.Attribution.SalesAccountId))));
            long income = entries.Where(e => e.Amount.IsPositive && e.BusinessKind == LedgerBusinessKind.Operating).Sum(e => e.Amount.Cents);
            long outflow = -entries.Where(e => e.Amount.IsNegative &&
                (e.BusinessKind == LedgerBusinessKind.Operating || e.Category == "房租")).Sum(e => e.Amount.Cents);
            outflow += company.Expenses.Where(e => e.Day >= firstDay && e.Day < untilDay).Sum(e => e.Cents);
            return (Money.FromCents(income), Money.FromCents(outflow));
        }

        public (Money Revenue, Money Costs) CompanyLastMonth(CompanyRecord company)
        {
            int until = Clock.Today.TotalDays / GameDate.DaysPerMonth * GameDate.DaysPerMonth;
            return CompanyAccounts(company, until - GameDate.DaysPerMonth, until);
        }

        public Money CompanyCashRequirement(CompanyRecord company)
        {
            var shops = CompanyShops(company).ToArray();
            int cutoff = Clock.Today.TotalDays + 7;
            long obligations = _supplierDebts.Where(x => x.DueDay <= cutoff && shops.Any(s => s.Place.Id == x.ShopId)).Sum(x => x.Amount.Cents);
            return Money.FromCents(obligations + shops.Sum(x => x.Lease?.Rent.Cents ?? 0)
                + CompanyRoster(company).Sum(x => x.Person.DailyWage.Cents * 7));
        }

        public string CompanyListingBlocker(CompanyRecord company, bool requireVisit = true)
        {
            if (IsGameOver || company == null) return "请先成立公司";
            if (company.ListedDay >= 0) return "公司已挂牌";
            if (DistrictAccessTier < 3) return "花园开放后办理上市";
            if (CompanyOfficeBlocker(company) is string authority) return authority;
            EnsureGovernanceCompanies();
            if(_companyInvestors.Where(i=>i.Id.StartsWith("capital_",StringComparison.Ordinal)).Sum(i=>i.CashCents)<CompanyListingPrice(company).Cents*CompanyPublicShares)return "机构认购资金不足，请等待市场资金或调整经营估值";
            if (requireVisit && !AtEnterpriseCenter) return "请到湖城企业服务中心的上市柜台";
            if (!CompanyShops(company).Any(x => ShopReadyForOperation(x) && !x.Sim.NotFitted)) return "需要至少一家实际营业的公司门店";
            int completeMonth = Clock.Today.TotalDays / GameDate.DaysPerMonth * GameDate.DaysPerMonth - GameDate.DaysPerMonth;
            if (Brand(company.BrandId).Founded > completeMonth) return "需要一个完整经营月";
            var (revenue, costs) = CompanyLastMonth(company);
            if (revenue <= costs) return "上个完整经营月须盈利";
            if (Money.FromCents(company.ReserveCents) < CompanyCashRequirement(company)) return "公司周转金须覆盖近七天工资、房租和到期货款";
            return null;
        }

        public Money CompanyListingPrice(CompanyRecord company)
        {
            var (revenue, costs) = CompanyLastMonth(company);
            decimal earnings = Math.Max(0, revenue.Cents - costs.Cents);
            decimal usableCash = Math.Max(0, (company?.ReserveCents ?? 0) - CompanyCashRequirement(company).Cents);
            var shops = CompanyShops(company).Select(s => s.Place.Id).ToHashSet();
            decimal supplierDebt = _supplierDebts.Where(d => shops.Contains(d.ShopId)).Sum(d => d.Amount.Cents);
            decimal priceCents = (earnings * 12 * 6 + usableCash - supplierDebt) / CompanyFounderShares;
            return Money.FromCents((long)Math.Max(100m, Math.Min(1000000m, Math.Round(priceCents))));
        }

        public string CompanyTicker(CompanyRecord company) => company == null ? null : "listed:" + company.BrandId;
        public CompanyRecord ListedCompanyOf(string ticker) =>
            _companies.Find(c => c.ListedDay >= 0 && CompanyTicker(c) == ticker);
        public CityEquityDefinition StockDefinition(string id)
        {
            var company = ListedCompanyOf(id);
            if (company == null) return CityInvestments.Get(id);
            return new CityEquityDefinition(id, CompanyName(company), "以所属门店实际经营财报定价。", (int)company.ListingPriceCents,
                CompanyIndustryName(Brand(company.BrandId).Kind), 0, .08, .008, 15000, totalShares: _companyGovernance.FirstOrDefault(g=>g.Ticker==id)?.TotalShares??CompanyTotalShares);
        }

        public long CompanyFounderHolding(CompanyRecord company) => 0; // 创始股份现已并入实际持仓，保留旧调用兼容。
        private void EnsureCompanyTicker(CompanyRecord company)
        {
            string id = CompanyTicker(company);
            if (StockState(id) != null) return;
            var equity = new CityEquityState { Id = id, PriceCents = Math.Max(100, company.ListingPriceCents) };
            _cityEquities.Add(equity);
            InitializeStockCompany(equity);
        }
        public bool ListCompany(string id)
        {
            var company = Company(id);
            var blocker = CompanyListingBlocker(company);
            if (blocker != null) return Fail(blocker);
            var price = CompanyListingPrice(company);
            var governance=GovernanceOf(CompanyTicker(company));
            company.ListedDay = Clock.Today.TotalDays;
            company.ListingPriceCents = price.Cents;
            EnsureCompanyTicker(company);
            CompleteGovernanceIPO(company,governance,price.Cents);
            Ledger.RecordNonCash(Clock.Today, "公司挂牌", $"{CompanyName(company)} · 首发募集 {price * CompanyPublicShares}",
                new LedgerAttribution(LedgerBusinessKind.InternalTransfer, activityId: id, transactionKind: "首次公开发行"));
            AddStockNews(CompanyTicker(company), Clock.Now.TotalMinutes, CompanyName(company) + "挂牌",
                $"每股 {price}，发行 {CompanyPublicShares:N0} 股，创始人持股 80%。");
            Say(CompanyName(company) + "已挂牌。请到现有股市查看。");
            return true;
        }
    }
}
