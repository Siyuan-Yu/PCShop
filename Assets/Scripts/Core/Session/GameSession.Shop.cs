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
        public string Name;
        public long DailyWageYuan;

        /// <summary>每结一单账要几分钟。</summary>
        public int CheckoutMinutes;

        public string Description;

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

        public Employee Employee { get; private set; }
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
            if (!PlayerShop.CompleteBuild(orderId)) return Fail("配件不齐，装不了。");
            int minutes = Math.Max(20, 60 - Player.Skill / 3 + mistakes * 5 + (Player.Tired ? 15 : 0));
            Player.Skill = Math.Min(100, Player.Skill + 2);
            AdvanceClock(minutes);
            Say($"整机交付！用时 {minutes} 分钟。");
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
            if (candidateIndex < 0 || candidateIndex >= _candidates.Count) return false;
            Employee = _candidates[candidateIndex];
            EverHired = true;
            PlayerShop.Settings.EmployeeCheckoutMinutes = Employee.CheckoutMinutes;
            _candidates.Clear();
            Show("雇到人了", $"{Employee.Name} 每天 9:00–21:00 看店（只会收银，不会装机），日薪 {Employee.DailyWage}，每天结算。\n你不在店里时，他在班就照常营业。");
            return true;
        }

        public void Fire()
        {
            if (Employee == null) return;
            Say($"{Employee.Name} 走了。");
            Employee = null;
        }

        public bool IsEmployeeOnDuty(GameTimestamp t)
        {
            return Employee != null && t.MinuteOfDay >= DutyStartMinute && t.MinuteOfDay < DutyEndMinute;
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
            if (Employee == null) return;
            Ledger.ForceDebit(day, "工资", Employee.DailyWage, Employee.Name);
        }
    }
}
