using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed class Employee
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string PersonId;
        public long CandidateLeavesAt;
        public string TransferShopId;
        public int TransferDay = -1;
        public int TransferRole = -1;
        public int AssemblyMinutes = 90, PurchaseMinutes = 2;
        public int Charm = -1;
        public int Intelligence = 50;
        public bool AssemblyTrained;
        public EmployeePurchasePlan PurchasePlan = new EmployeePurchasePlan();
        public string LastPurchaseNote;
        public string Name;
        public long DailyWageYuan;

        /// <summary>每结一单账要几分钟。</summary>
        public int CheckoutMinutes;

        public string Description;
        public int ShiftStart = 9 * 60, ShiftEnd = 21 * 60;
        public EmployeeSchedule Schedule = new EmployeeSchedule();
        public int BuildsCompleted;
        public bool OnDuty(GameTimestamp t) => TryShift(t.TotalMinutes, out _, out _);
        public bool TryShift(long at, out long start, out long end)
        {
            if(Schedule!=null && at>=Schedule.ActiveStart && at<Schedule.ActiveEnd) {start=Schedule.ActiveStart;end=Schedule.ActiveEnd;return true;}
            int a=Schedule!=null&&Schedule.PendingAt>=0&&at>=Schedule.PendingAt?Schedule.PendingStart:ShiftStart;
            int b=Schedule!=null&&Schedule.PendingAt>=0&&at>=Schedule.PendingAt?Schedule.PendingEnd:ShiftEnd;
            start=at/1440*1440+a;if(start>at)start-=1440;
            end=start+GameSession.ShiftMinutes(a,b);
            return at>=start&&at<end&&(Schedule==null||start>=Schedule.NextNotBefore);
        }

        public Money DailyWage => Money.FromYuan(DailyWageYuan);
    }

    public sealed partial class GameSession
    {
        public static readonly Money JobAdFee = Money.FromYuan(50);
        public const int CandidateMinute = 9 * 60;
        public const int DutyStartMinute = 9 * 60;
        public const int DutyEndMinute = 21 * 60;

        private static readonly string[] Surnames = { "王", "李", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴" };
        private static readonly string[] GivenNames = { "小明", "小红", "阿强", "阿珍", "建国", "晓燕", "大伟", "婷婷", "浩然", "丽丽" };

        private readonly List<Employee> _candidates = new List<Employee>();

        /// <summary>当前这家店的收银员 / 装机员（每家店各雇各的）。</summary>
        public Employee Employee
        {
            get => _focus.Employee;
            private set => _focus.Employee = value;
        }

        public Employee Assembler
        {
            get => _focus.Assembler;
            private set => _focus.Assembler = value;
        }
        public Employee Salesperson => _focus.Salesperson;
        public string OnDutyStaffName => string.Join("、",ShopStaff(_focus,EmployeeRole.Cashier).Concat(ShopStaff(_focus,EmployeeRole.Assembler)).Where(e=>e.OnDuty(Clock.Now)).Select(e=>e.Name));
        public bool EverHired { get; private set; }
        public IReadOnlyList<Employee> Candidates => _candidates;

        /// <summary>贴了招聘启事的日期；-1 表示没贴。</summary>
        public int JobAdDay { get; private set; } = -1;

        public const int CandidateLeaveMinute = 18 * 60;
        public bool CandidateAvailable(Employee person) => person != null && _candidates.Contains(person)
            && person.CandidateLeavesAt > Clock.Now.TotalMinutes;
        public string CandidateWaitText(Employee person)
        {
            long left = Math.Max(0, person.CandidateLeavesAt - Clock.Now.TotalMinutes);
            return $"{DescribeDay((int)(person.CandidateLeavesAt / 1440))} {Hm((int)(person.CandidateLeavesAt % 1440))} 离开 · 还等 {left / 60} 小时 {left % 60} 分钟";
        }
        private void ExpireCandidates()
        {
            var departed = new List<string>();
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                var person = _candidates[i];
                if (person.CandidateLeavesAt > Clock.Now.TotalMinutes) continue;
                departed.Add(person.Name); _candidates.RemoveAt(i);
            }
            if (departed.Count > 0) Say("应聘等待时间已到，" + string.Join("、", departed) + "离开了。需要招人可以再贴招聘告示。");
        }

        // ---------- 店里 ----------

        /// <summary>给排队的顾客结账，并推进结账耗时（太累会慢一点）。</summary>
        public bool Checkout(int customerId)
        {
            if (!InPlayerShop) return false;
            int minutes = PlayerShop.Checkout(customerId);
            if (minutes < 0) return false;
            AdvanceClock(minutes + (Player.Tired ? 1 : 0));
            return true;
        }

        /// <summary>拆包装：盒装变散件，占地方少但卖得便宜。每件 1 分钟。</summary>
        public bool Unbox(PartDef part, int count)
        {
            if (!InPlayerShop) return false;
            if (!PlayerShop.Stock.Unbox(part, count)) return Fail("没有这么多盒装的。");
            AdvanceClock(count);
            return true;
        }

        public void SetMarkup(double markup) => PlayerShop.Markup = markup;

        /// <summary>装机小游戏完成后交付：耗时随失误增加，技术 +2。</summary>
        public bool CompleteBuild(int orderId, int mistakes)
        {
            if (!InPlayerShop) return false;
            var order = System.Linq.Enumerable.FirstOrDefault(PlayerShop.Orders, o => o.Id == orderId);
            if (order == null) return Fail("订单已取消或完成。");
            int minutes = Math.Max(20, 60 - Player.Skill / 3 + Math.Max(order.Mistakes, Math.Max(0, mistakes)) * 5 + (Player.Tired ? 15 : 0));
            if (Clock.Now.MinutesUntil(order.DueAt) <= minutes) return Fail($"来不及交付：装机需要 {minutes} 分钟，已超过剩余交期。");
            if (order.EmployeeMinutesLeft > 0) return Fail("店员正在装这台机器，请等他完成。");
            if (!PlayerShop.ReserveBuild(orderId)) return Fail("配件不齐，装不了。");
            _manualBuildId = orderId;
            try { AdvanceClock(minutes); } finally { _manualBuildId = -1; }
            if (IsGameOver || !InPlayerShop || !PlayerShop.CompleteBuild(orderId)) return Fail("装机未交付，请检查订单状态。");
            Player.Skill = Math.Min(100, Player.Skill + 2);
            Say($"整机交付！用时 {minutes} 分钟。");
            return true;
        }

        /// <summary>用库存配件装一台整机摆上展示台：顾客能直接买走，也能吸引更多装机客。</summary>
        public bool AssembleDisplayPc(int tier)
        {
            if (!InPlayerShop) return Fail("得在店里才能装。");
            if (PlayerShop.DisplayPcs.Count >= PlayerShop.DisplaySlots) return Fail($"展示台摆满了（{PlayerShop.DisplaySlots} 台）。");
            if(_focus.PendingDisplayTier>=0&&_focus.PendingDisplayTier!=tier)return Fail("请先完成已保留进度的展示整机。");
            if(_focus.PendingDisplayTier<0)
            {
                if(PartCatalog.BuildList(tier).Any(p=>PlayerShop.Stock.CountAll(p)==0))return Fail("配件不齐，装不了。");
                foreach(var part in PartCatalog.BuildList(tier))PlayerShop.Stock.TakeOne(part);
                _focus.PendingDisplayTier=tier;_focus.PendingDisplayMinutes=Math.Max(20,60-Player.Skill/3+(Player.Tired?15:0));
            }
            var shop=_focus;int minutes=shop.PendingDisplayMinutes;
            int interruption=_bodyInterruptVersion;
            _manualDisplayShop=shop;try{AdvanceClock(minutes);}finally{_manualDisplayShop=null;}
            if(interruption!=_bodyInterruptVersion||!InPlayerShop)return Fail("身体不适，装机暂停；配件和剩余进度已保留，回店后可继续。");
            if(shop.PendingDisplayMinutes>0)return false;
            shop.Sim.RestoreDisplay(shop.Sim.DisplayPcs.Concat(new[]{tier}).ToArray());shop.PendingDisplayTier=-1;
            Player.Skill = Math.Min(100, Player.Skill + 1);
            Say($"装好一台{PartCatalog.TierName(tier)}整机摆上展示台，用时 {minutes} 分钟。");
            return true;
        }

        /// <summary>把展示台上的一台拆回散件。</summary>
        public bool DisassembleDisplayPc(int tier)
        {
            if (!InPlayerShop) return Fail("得在店里才能拆。");
            if (!PlayerShop.DisassembleDisplay(tier)) return Fail("展示台上没有这一档的整机。");
            AdvanceClock(15);
            Say($"把{PartCatalog.TierName(tier)}整机拆回了散件。");
            return true;
        }

        /// <summary>装机报价（市场价合计 + 手工费），用于界面显示配件需求。</summary>
        public List<PartDef> MissingParts(BuildOrder order) => PlayerShop.MissingParts(order);

        // ---------- 招人 ----------

        public bool PostJobAd()
        {
            if (!InPlayerShop) return Fail("要在店门口贴招聘启事。");
            if (JobAdDay >= 0) return Fail("已经贴过了，明天 9:00 会有人来。");
            if (!Ledger.TryDebit(Clock.Today, "招聘", JobAdFee, "招聘启事")) return Fail("钱不够。");
            JobAdDay = Clock.Today.TotalDays;
            Say("招聘启事贴出去了，明天 9:00 会有人来面试。");
            return true;
        }

        public bool Hire(int candidateIndex)
        {
            if (IsGameOver || candidateIndex < 0 || candidateIndex >= _candidates.Count) return false;
            string capacity=StaffCapacityBlocker(_focus);if(capacity!=null)return Fail(capacity);
            if (!CandidateAvailable(_candidates[candidateIndex])) return Fail("这位应聘者已经离开了。");
            var hired=_candidates[candidateIndex];AddShopEmployee(_focus,EmployeeRole.Cashier,hired);_employeeHireShops[hired.Id]=_focus.Place.Id;
            if(Employee==hired)_focus.EmployeeTrained = false;
            EverHired = true;
            PlayerShop.Settings.EmployeeCheckoutMinutes = Employee.CheckoutMinutes;
            _candidates.Clear();
            Show("雇到人了",IsDedicatedVenue(PlayerShopPlace)?$"{hired.Name}受聘为场馆值守员，默认每天 9:00–21:00，日薪 {hired.DailyWage}，按实际排班与到岗结算。可在员工管理调整班次。":$"{hired.Name} 每天 9:00–21:00 在本店收银（可在员工管理重新安排岗位），日薪 {hired.DailyWage}，每天结算。\n他在班就持续收银，你在店内装机或外出都不影响。");
            return true;
        }

        public bool HireAssembler(int index)
        {
            if (!InPlayerShop || index < 0 || index >= _candidates.Count) return false;
            string capacity=StaffCapacityBlocker(_focus);if(capacity!=null)return Fail(capacity);
            if (!CandidateAvailable(_candidates[index])) return Fail("这位应聘者已经离开了。");
            if (!Ledger.TryDebit(Clock.Today, "培训", Money.FromYuan(1200), "装机员入职培训")) return Fail("入职培训需要 ¥1,200。");
            var hired=_candidates[index];hired.DailyWageYuan += 60;hired.AssemblyTrained=true;
            AddShopEmployee(_focus,EmployeeRole.Assembler,hired);_employeeHireShops[hired.Id]=_focus.Place.Id;
            EverHired = true; _candidates.Clear(); _focus.AutoBuild = true;
            Say($"{hired.Name}入职装机岗位，日薪 {hired.DailyWage}。可与收银员同时工作。");
            return true;
        }
        public void FireAssembler()
        {
            if (Assembler == null) return;
            Say(Assembler.Name + "离职了。"); RemoveShopEmployee(_focus,EmployeeRole.Assembler,Assembler);
            foreach (var o in PlayerShop.Orders) o.EmployeeMinutesLeft = 0;
        }
        public void CycleShift(bool assembly)
        {
            var e = assembly ? Assembler : Employee;
            if (e == null) return;
            SetEmployeeShift(e.Id, e.ShiftStart==540&&e.ShiftEnd==1020?780:540, e.ShiftStart==540&&e.ShiftEnd==1260?1020:1260);
        }

        public void Fire()
        {
            if (Employee == null) return;
            Say($"{Employee.Name} 走了。");
            RemoveShopEmployee(_focus,EmployeeRole.Cashier,Employee);
            _employeeBuilding = false;
            if (Assembler == null) foreach (var order in PlayerShop.Orders) order.EmployeeMinutesLeft = 0;
            _focus.EmployeeTrained = Employee?.AssemblyTrained??false;
        }

        public bool IsEmployeeOnDuty(GameTimestamp t)
        {
            return Employees.Where(e=>e.Shop==_focus&&(e.Role==EmployeeRole.Cashier||e.Role==EmployeeRole.Assembler||IsSpecialBusiness(_focus)&&(e.Role==EmployeeRole.Chef||e.Role==EmployeeRole.Waiter||e.Role==EmployeeRole.Appraiser))).Any(e=>e.Person.OnDuty(t));
        }

        private void OnCandidateHour(GameDate today)
        {
            if (JobAdDay < 0 || today.TotalDays <= JobAdDay) return;
            JobAdDay = -1;
            int newStart = _candidates.Count;
            for (int i = 0; i < 2; i++)
            {
                bool fast = Random.Chance(0.5);
                var e = new Employee
                {
                    CandidateLeavesAt = (long)today.TotalDays * 1440 + CandidateLeaveMinute,
                    Name = Surnames[Random.Next(0, Surnames.Length)] + GivenNames[Random.Next(0, GivenNames.Length)],
                    CheckoutMinutes = fast ? 2 : 4,
                    Charm = Random.Next(20, 91),
                    Intelligence = Random.Next(20, 96),
                    AssemblyMinutes = Random.Next(70, 131),
                    PurchaseMinutes = Random.Next(2, 7),
                    DailyWageYuan = fast ? Random.Next(150, 181) : Random.Next(100, 131),
                };
                e.Description = fast ? "有收银经验，其他工作也愿意学" : "踏实肯干，做事比较仔细";
                BindCityCandidate(e);
                _candidates.Add(e);
            }
            Show("有人来应聘", $"{_candidates[newStart].Name}（{_candidates[newStart].Description}，日薪 ¥{_candidates[newStart].DailyWageYuan}）\n" +
                             $"{_candidates[newStart + 1].Name}（{_candidates[newStart + 1].Description}，日薪 ¥{_candidates[newStart + 1].DailyWageYuan}）\n应聘者会等到今天 18:00，之后离开。请去店里的'招人'面板决定。");
        }

        private void PayWages(GameDate day)
        {
            PayEmployeeWorkday(day);
        }
    }
}
