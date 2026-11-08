using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private long _loanAmountYuan = 1000;
        private int _loanMonths = 1;
        private const float LoanReminderHeight = 96f;
        private RectTransform _loanReminderHud;
        private Text _loanReminderTitle, _loanReminderDue, _loanReminderCash;
        private (GameSession Session, int Day, int LedgerRevision) _loanReminderStamp;
        private bool _loanReminderHasPending;

        private void BuildLoanReminderHud(Transform parent)
        {
            var button = Ui.Button(parent, "", OpenLoansApp, 18, Ui.PanelBg);
            button.name = "LoanReminderHud";
            _loanReminderHud = (RectTransform)button.transform;
            _loanReminderHud.anchorMin = _loanReminderHud.anchorMax = _loanReminderHud.pivot = Vector2.one;
            _loanReminderHud.sizeDelta = new Vector2(UpcomingScheduleWidth, LoanReminderHeight);
            _loanReminderTitle = Ui.Label(_loanReminderHud, "", 18, color: Ui.Accent);
            _loanReminderTitle.rectTransform.Anchor(0, 1, 1, 1, 14, -32, 32, 6);
            var arrow = Ui.Label(_loanReminderHud, "›", 20, TextAnchor.MiddleRight, Ui.Dim);
            arrow.rectTransform.Anchor(1, 1, 1, 1, -30, -32, 14, 6);
            _loanReminderDue = Ui.Label(_loanReminderHud, "", 17);
            _loanReminderDue.rectTransform.Anchor(0, 1, 1, 1, 14, -61, 14, 33);
            _loanReminderCash = Ui.Label(_loanReminderHud, "", 17);
            _loanReminderCash.rectTransform.Anchor(0, 1, 1, 1, 14, -88, 14, 62);
            _loanReminderHud.gameObject.SetActive(false);
        }

        private void UpdateLoanReminderHud()
        {
            int today = S.Clock.Today.TotalDays;
            var stamp = (S, today, S.Ledger.Revision);
            if (!_loanReminderStamp.Equals(stamp))
            {
                _loanReminderStamp = stamp;
                RefreshLoanReminder(today);
            }

            bool visible = _loanReminderHasPending && !MainMenuOpen && !S.IsGameOver && !ExpoHudOpen;
            if (_loanReminderHud.gameObject.activeSelf != visible)
                _loanReminderHud.gameObject.SetActive(visible);

            Vector2 schedulePosition = UpcomingScheduleRestPosition;
            if (visible)
            {
                Vector2 position = schedulePosition;
                if (_shopDockToggle.gameObject.activeSelf) position.y = -120;
                if (DockOpen) position.x = -(ShopDockWidth + 10);
                else schedulePosition.y = position.y - LoanReminderHeight - 8;
                if (_loanReminderHud.anchoredPosition != position) _loanReminderHud.anchoredPosition = position;
            }
            if (_upcomingScheduleHud.anchoredPosition != schedulePosition)
                _upcomingScheduleHud.anchoredPosition = schedulePosition;
        }

        private void RefreshLoanReminder(int today)
        {
            int count = 0;
            int dueDay = int.MaxValue;
            Money due = Money.Zero;
            foreach (var loan in S.AppLoans)
            {
                if (loan.Repaid) continue;
                count++;
                if (loan.DueDay < dueDay)
                {
                    dueDay = loan.DueDay;
                    due = loan.TotalDue;
                }
                else if (loan.DueDay == dueDay) due += loan.TotalDue;
            }
            _loanReminderHasPending = count > 0;
            if (!_loanReminderHasPending) return;

            Money shortfall = Money.Max(Money.Zero, due - S.Ledger.Balance);
            _loanReminderTitle.text = count == 1 ? "还款提醒" : $"还款提醒 · {count} 笔待还";
            _loanReminderDue.text = $"最近应还 {due} · {LoanCountdown(dueDay - today)}";
            _loanReminderCash.text = shortfall.IsPositive ? $"当前现金还差 {shortfall}" : "当前现金足够还款";
            _loanReminderCash.color = shortfall.IsPositive ? Ui.Warn : Ui.Good;
        }

        private static string LoanCountdown(int days) => days <= 0 ? "今天还款" : $"还剩 {days} 天";

        private void OpenLoansApp()
        {
            string blocker = S.AppBlocker(PhoneApps.Loans);
            if (blocker != null) { Toast(blocker); return; }
            OpenWindow("周转借款", 860, 860, w =>
            {
                Line(w.Content, $"当前现金 {S.Ledger.Balance}    贷款待还 {S.AppLoanDebt}", 21, Ui.Accent);
                Para(w.Content, "月息 20%，按最初本金算单利，不复利；每月按游戏日历 30 天，最长 12 个月。到期一次性自动扣除本金和总利息，现金不足会强制扣成负数。", 18, Ui.Warn);
                var amountRow = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Label(amountRow, "借款金额", 18), 120);
                QuantityStepper(w, amountRow, "appLoanAmount", _loanAmountYuan, 100, 10000000, 100,
                    v => _loanAmountYuan = (long)v, v => "¥" + v.ToString("N0"));
                var presets = Ui.Row(w.Content, 34);
                foreach (long amount in new long[] { 1000, 5000, 10000, 50000 })
                {
                    long selectedAmount = amount;
                    Ui.Size(Ui.Button(presets, "¥" + amount.ToString("N0"), () => { _loanAmountYuan = selectedAmount; w.Rebuild(); }, 17), -1, -1, 1);
                }
                var monthsRow = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Label(monthsRow, "借款期限", 18), 120);
                QuantityStepper(w, monthsRow, "appLoanMonths", _loanMonths, 1, 12, 1,
                    v => _loanMonths = (int)v, v => v.ToString("0") + " 个月");
                Money principal = Money.FromYuan(_loanAmountYuan);
                Money monthly = principal.Scale(0.2), interest = monthly * _loanMonths;
                var due = S.Clock.Today.AddDays(_loanMonths * 30);
                Line(w.Content, $"到账本金 {principal} · 每月利息 {monthly}", 19);
                Line(w.Content, $"期限利息 {interest}（本金的 {_loanMonths * 20}%）", 19);
                Line(w.Content, $"到期日 {due} · 自动扣款 {principal + interest}", 21, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, "申请借款 · 到账 " + principal, () =>
                {
                    long amount = _loanAmountYuan;
                    int months = _loanMonths;
                    ConfirmBusiness("确认借款", $"到账 {principal}，期限 {months} 个月。每月利息 {monthly}，总利息 {interest}。\n{due} 自动扣除本息 {principal + interest}，余额不足也会扣到负数。", () =>
                    { if (S.BorrowAppLoan(amount, months)) w.Rebuild(); });
                }, 19, Ui.ButtonHot), -1, 46);
                Para(w.Content, "可提前整笔结清：按实际使用月份计息，不足一个月按一个月。待还负债包含本金和已计利息，未来月份利息不计入当前负债。", 15, Ui.Dim);
                Line(w.Content, "我的贷款", 20, Ui.Accent);
                var list = Ui.Scroll(w.Content, 260);
                if (S.AppLoans.Count == 0) Line(list, "暂无借款记录。", 17, Ui.Dim);
                foreach (var loan in S.AppLoans.OrderBy(l => l.Repaid).ThenBy(l => l.Repaid ? 0 : l.DueDay).ThenByDescending(l => l.Id))
                {
                    int id = loan.Id;
                    Line(list, $"#{id} · 本金 {loan.Principal} · {loan.Months} 个月 · " + (loan.Repaid ? "已结清" : "待还"), 18, loan.Repaid ? Ui.Dim : Ui.Accent);
                    if (loan.Repaid)
                    {
                        Para(list, $"{new GameDate(loan.RepaidDay)} 结清；实付利息 {Money.FromCents(loan.PaidInterestCents)}。", 16, Ui.Dim);
                        continue;
                    }
                    Para(list, $"{new GameDate(loan.StartDay)} 借入，{new GameDate(loan.DueDay)} 到期（{LoanCountdown(loan.DueDay - S.Clock.Today.TotalDays)}）。\n到期本金 {loan.Principal} + 利息 {loan.TermInterest} = {loan.TotalDue}。", 16);
                    Money payoff = S.LoanPayoff(loan);
                    Ui.Size(Ui.Button(list, "提前结清 · " + payoff, () =>
                        ConfirmBusiness("提前结清贷款 #" + id, $"本次支付 {payoff}，其中本金 {loan.Principal}、利息 {payoff - loan.Principal}。", () =>
                        { if (S.RepayAppLoan(id)) w.Rebuild(); }), 17, Ui.ButtonBg, S.Ledger.CanAfford(payoff)), -1, 38);
                }
            });
        }
    }
}
