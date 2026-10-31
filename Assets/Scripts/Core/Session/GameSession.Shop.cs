using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed class Employee
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string TransferShopId;
        public int TransferDay = -1;
        public EmployeePurchasePlan PurchasePlan = new EmployeePurchasePlan();
        public string LastPurchaseNote;
        public string Name;
        public long DailyWageYuan;

        /// <summary>每结一单账要几分钟。</summary>
        public int CheckoutMinutes;

        public string Description;
        public int ShiftStart = 9 * 60, ShiftEnd = 21 * 60;
        public int BuildsCompleted;
        public bool OnDuty(GameTimestamp t) => t.MinuteOfDay >= ShiftStart && t.MinuteOfDay < ShiftEnd;

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
        public string OnDutyStaffName => Employee != null && Employee.OnDuty(Clock.Now) ? Employee.Name : Assembler?.Name ?? "店员";
        public bool EverHired { get; private set; }
        public IReadOnlyList<Employee> Candidates => _candidates;

        /// <summary>贴了招聘启事的日期；-1 表示没贴。</summary>
        public int JobAdDay { get; private set; } = -1;

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
            if (IsGameOver || !PlayerShop.CompleteBuild(orderId)) return Fail("装机未交付，请检查订单状态。");
            Player.Skill = Math.Min(100, Player.Skill + 2);
            Say($"整机交付！用时 {minutes} 分钟。");
            return true;
        }

        /// <summary>用库存配件装一台整机摆上展示台：顾客能直接买走，也能吸引更多装机客。</summary>
        public bool AssembleDisplayPc(int tier)
        {
            if (!InPlayerShop) return Fail("得在店里才能装。");
            if (PlayerShop.DisplayPcs.Count >= PlayerShop.DisplaySlots) return Fail($"展示台摆满了（{PlayerShop.DisplaySlots} 台）。");
            if (!PlayerShop.AssembleForDisplay(tier)) return Fail("配件不齐，装不了。");
            int minutes = Math.Max(20, 60 - Player.Skill / 3 + (Player.Tired ? 15 : 0));
            Player.Skill = Math.Min(100, Player.Skill + 1);
            AdvanceClock(minutes);
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
            _candidates.Clear();
            Say("招聘启事贴出去了，明天 9:00 会有人来面试。");
            return true;
        }

        public bool Hire(int candidateIndex)
        {
            if (IsGameOver || Employee != null || candidateIndex < 0 || candidateIndex >= _candidates.Count) return false;
            Employee = _candidates[candidateIndex];
            _focus.EmployeeTrained = false;
            EverHired = true;
            PlayerShop.Settings.EmployeeCheckoutMinutes = Employee.CheckoutMinutes;
            _candidates.Clear();
            Show("雇到人了", $"{Employee.Name} 每天 9:00–21:00 看店（只会收银，不会装机），日薪 {Employee.DailyWage}，每天结算。\n他在班就持续收银，你在店内装机或外出都不影响。");
            return true;
        }

        public bool HireAssembler(int index)
        {
            if (!InPlayerShop || Assembler != null || index < 0 || index >= _candidates.Count) return false;
            if (!Ledger.TryDebit(Clock.Today, "培训", Money.FromYuan(1200), "装机员入职培训")) return Fail("入职培训需要 ¥1,200。");
            Assembler = _candidates[index]; Assembler.DailyWageYuan += 60;
            EverHired = true; _candidates.Clear(); _focus.AutoBuild = true;
            Say($"{Assembler.Name}入职装机岗位，日薪 {Assembler.DailyWage}。可与收银员同时工作。");
            return true;
        }
        public void FireAssembler()
        {
            if (Assembler == null) return;
            Say(Assembler.Name + "离职了。"); Assembler = null;
            foreach (var o in PlayerShop.Orders) o.EmployeeMinutesLeft = 0;
        }
        public void CycleShift(bool assembly)
        {
            var e = assembly ? Assembler : Employee;
            if (e == null) return;
            if (e.ShiftStart == 9 * 60 && e.ShiftEnd == 21 * 60) e.ShiftEnd = 17 * 60;
            else if (e.ShiftStart == 9 * 60) { e.ShiftStart = 13 * 60; e.ShiftEnd = 21 * 60; }
            else { e.ShiftStart = 9 * 60; e.ShiftEnd = 21 * 60; }
        }

        public void Fire()
        {
            if (Employee == null) return;
            Say($"{Employee.Name} 走了。");
            Employee = null;
            _employeeBuilding = false;
            if (Assembler == null) foreach (var order in PlayerShop.Orders) order.EmployeeMinutesLeft = 0;
            _focus.EmployeeTrained = false;
        }

        public bool IsEmployeeOnDuty(GameTimestamp t)
        {
            return (Employee != null && Employee.OnDuty(t)) || (Assembler != null && Assembler.OnDuty(t));
        }

        private void OnCandidateHour(GameDate today)
        {
            if (JobAdDay < 0 || today.TotalDays <= JobAdDay) return;
            JobAdDay = -1;
            _candidates.Clear();
            for (int i = 0; i < 2; i++)
            {
                bool fast = Random.Chance(0.5);
                var e = new Employee
                {
                    Name = Surnames[Random.Next(0, Surnames.Length)] + GivenNames[Random.Next(0, GivenNames.Length)],
                    CheckoutMinutes = fast ? 2 : 4,
                    DailyWageYuan = fast ? Random.Next(150, 181) : Random.Next(100, 131),
                };
                e.Description = fast ? "手脚麻利，干过收银" : "老实，但动作慢";
                _candidates.Add(e);
            }
            Show("有人来应聘", $"{_candidates[0].Name}（{_candidates[0].Description}，日薪 ¥{_candidates[0].DailyWageYuan}）\n" +
                             $"{_candidates[1].Name}（{_candidates[1].Description}，日薪 ¥{_candidates[1].DailyWageYuan}）\n去店里的'招人'面板决定。");
        }

        private void PayWages(GameDate day)
        {
            foreach (OwnedShop s in _shops)
            {
                if (s.Employee != null) Ledger.ForceDebit(day, "工资", s.Employee.DailyWage, s.Employee.Name);
                if (s.Assembler != null) Ledger.ForceDebit(day, "工资", s.Assembler.DailyWage, s.Assembler.Name);
                if (s.Buyer != null) Ledger.ForceDebit(day, "工资", s.Buyer.DailyWage, s.Buyer.Name);
            }
        }
    }
}
