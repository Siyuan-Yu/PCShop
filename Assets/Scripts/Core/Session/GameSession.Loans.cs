using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class AppLoan
    {
        public int Id, StartDay, Months;
        public long PrincipalCents;
        public bool Repaid;
        public int RepaidDay;
        public long PaidInterestCents;
        public int DueDay => StartDay + Months * GameDate.DaysPerMonth;
        public Money Principal => Money.FromCents(PrincipalCents);
        public Money MonthlyInterest => Money.FromCents(PrincipalCents / 5);
        public Money TermInterest => MonthlyInterest * Months;
        public Money TotalDue => Principal + TermInterest;
        public AppLoan Copy() => (AppLoan)MemberwiseClone();
    }

    public sealed partial class GameSession
    {
        private readonly List<AppLoan> _appLoans = new List<AppLoan>();
        private int _nextAppLoanId = 1;
        public IReadOnlyList<AppLoan> AppLoans => _appLoans;
        public Money LoanPayoff(AppLoan loan)
        {
            int months = Math.Min(loan.Months, Math.Max(1, (Clock.Today.TotalDays - loan.StartDay + 29) / 30));
            return loan.Principal + loan.MonthlyInterest * months;
        }
        public Money AppLoanDebt => Money.FromCents(_appLoans.Where(l => !l.Repaid).Sum(l => LoanPayoff(l).Cents));
        public bool BorrowAppLoan(long yuan, int months)
        {
            if (IsGameOver || IsWorking) return Fail("当前不能办理贷款。");
            string blocker = AppBlocker(PhoneApps.Loans);
            if (blocker != null) return Fail(blocker);
            if (yuan < 100 || yuan > 10000000 || yuan % 100 != 0 || months < 1 || months > 12) return Fail("金额按 ¥100 调整，期限为 1–12 个月。");
            var loan = new AppLoan { Id = _nextAppLoanId++, StartDay = Clock.Today.TotalDays, Months = months,
                PrincipalCents = Money.FromYuan(yuan).Cents };
            _appLoans.Add(loan);
            Ledger.Credit(Clock.Today, "贷款到账", loan.Principal, "周转借款 #" + loan.Id);
            Say($"贷款 #{loan.Id} 已到账 {loan.Principal}，{new GameDate(loan.DueDay)} 自动扣款 {loan.TotalDue}（含利息 {loan.TermInterest}）。");
            return true;
        }
        public bool RepayAppLoan(int id)
        {
            if (IsGameOver || IsWorking) return Fail("当前不能办理还款。");
            var loan = _appLoans.FirstOrDefault(l => l.Id == id && !l.Repaid);
            if (loan == null) return Fail("这笔贷款已结清或不存在。");
            Money due = LoanPayoff(loan);
            if (!Ledger.CanAfford(due)) return Fail($"结清需 {due}，当前现金不足。");
            SettleAppLoan(loan, due - loan.Principal, false);
            return true;
        }
        private void SettleAppLoan(AppLoan loan, Money interest, bool automatic)
        {
            if (loan.Repaid) return;
            loan.Repaid = true; loan.RepaidDay = Clock.Today.TotalDays; loan.PaidInterestCents = interest.Cents;
            string memo = $"周转借款 #{loan.Id}" + (automatic ? "到期自动扣款" : "提前结清");
            Ledger.ForceDebit(Clock.Today, "偿还贷款", loan.Principal, memo + "本金");
            if (interest.IsPositive) Ledger.ForceDebit(Clock.Today, "贷款利息", interest, memo + "利息");
            Say($"{memo}：本金 {loan.Principal} + 利息 {interest} = {loan.Principal + interest}，当前现金 {Ledger.Balance}。");
        }
        private void CollectDueAppLoans()
        {
            foreach (var loan in _appLoans.Where(l => !l.Repaid && l.DueDay <= Clock.Today.TotalDays).OrderBy(l => l.DueDay).ThenBy(l => l.Id))
                SettleAppLoan(loan, loan.TermInterest, true);
        }
        private void RestoreAppLoans(SaveData data)
        {
            _appLoans.Clear();
            foreach (var loan in data.appLoans ?? Array.Empty<AppLoan>())
                if (loan != null && loan.Id > 0 && loan.StartDay >= 0 && loan.StartDay <= Clock.Today.TotalDays
                    && loan.Months >= 1 && loan.Months <= 12 && loan.PrincipalCents >= 10000 && loan.PrincipalCents <= 1000000000
                    && !_appLoans.Any(l => l.Id == loan.Id)) _appLoans.Add(loan.Copy());
            _nextAppLoanId = _appLoans.Count == 0 ? 1 : _appLoans.Max(l => l.Id) + 1;
            CollectDueAppLoans();
        }
    }
}
