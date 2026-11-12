using System;
using System.Globalization;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private enum BankPage { Transfers, Borrow, Loans }

        private sealed class BankDeskDraft
        {
            public BankPage Page;
            public bool Withdrawing, ShowTransferRules, ShowLoanRules;
            public string TransferAmount = "100", LoanAmount = "1000";
            public int Months = 1;
        }

        private void OpenBank(Place bank)
        {
            var draft = new BankDeskDraft();
            OpenPlaceWindow(bank, (w, _) => BuildBank(w, draft), 1000, 620);
        }

        private void BuildBank(Window w, BankDeskDraft draft)
        {
            var tabs = Ui.Row(w.Content, 50, 6);
            void Tab(BankPage page, string label, string name)
            {
                Ui.Size(Ui.Button(tabs, label, () =>
                {
                    draft.Page = page;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 19, draft.Page == page ? Ui.ButtonHot : Ui.ButtonBg), 0, 46, 1).name = name;
            }
            Tab(BankPage.Transfers, "存取款", "BankTransfersTab");
            Tab(BankPage.Borrow, "借款", "BankBorrowTab");
            Tab(BankPage.Loans, "我的贷款", "BankLoansTab");
            switch (draft.Page)
            {
                case BankPage.Borrow: BuildBankBorrow(w, draft); break;
                case BankPage.Loans: BuildBankLoans(w); break;
                default: BuildBankTransfers(w, draft); break;
            }
        }

        private void BuildBankTransfers(Window w, BankDeskDraft draft)
        {
            var balances = Ui.Row(w.Content, 75, 8);
            BankBalance(balances, "随身现金", S.Ledger.Balance, "BankCash");
            BankBalance(balances, "活期余额", S.BankDeposit, "BankBalance");

            bool withdraw = draft.Withdrawing;
            var modes = Ui.Row(w.Content, 38, 6);
            void Mode(string caption, bool take, string name)
            {
                var button = Ui.Size(Ui.Button(modes, caption, () =>
                {
                    draft.Withdrawing = take;
                    w.Rebuild();
                }, 17, take == withdraw ? Ui.FlatRow : Ui.ButtonBg), 0, 38, 1);
                button.name = name;
                if (take == withdraw)
                {
                    Ui.FlatBorder(button.image, Ui.FlatHotEdge);
                    button.GetComponentInChildren<Text>().color = Ui.Accent;
                }
            }
            Mode("存入", false, "BankDepositMode");
            Mode("取出", true, "BankWithdrawMode");

            string verb = withdraw ? "取出" : "存入";
            Line(w.Content, verb + "金额（元）", 17);
            var amountRow = Ui.Row(w.Content, 42, 8);
            var amount = BankAmountInput(amountRow, "BankTransferAmount", draft.TransferAmount, 9);
            if (withdraw)
                Ui.Size(Ui.Button(amountRow, "全部取出", () => { if (S.BankWithdrawAll()) w.Rebuild(); },
                    17, Ui.ButtonBg, S.BankDeposit.IsPositive && S.BankBlocker() == null), 116, 42).name = "BankWithdrawAll";

            var feedback = Para(w.Content, "", 15, Ui.FlatDown);
            const long maximum = 100000000;
            var submit = Ui.Size(Ui.Button(w.Content, verb, () =>
            {
                if (!TryBankAmount(amount.text, maximum, 1, out long yuan)) return;
                bool success = withdraw ? S.BankWithdrawMoney(yuan) : S.BankDepositMoney(yuan);
                if (success) w.Rebuild();
            }, 19, Ui.ButtonHot), -1, 46);
            submit.name = withdraw ? "BankWithdraw" : "BankDeposit";
            var status = submit.GetComponentInChildren<Text>();
            void Refresh(string value)
            {
                draft.TransferAmount = value;
                bool valid = TryBankAmount(value, maximum, 1, out long yuan);
                Money money = valid ? Money.FromYuan(yuan) : Money.Zero;
                string blocker = S.BankBlocker() ?? (!valid ? "请输入有效的整数金额。"
                    : withdraw && money > S.BankDeposit ? "活期余额不足。"
                    : !withdraw && !S.Ledger.CanAfford(money) ? "随身现金不足。" : null);
                feedback.text = blocker ?? "";
                feedback.gameObject.SetActive(blocker != null && value.Length > 0);
                status.text = valid ? verb + " " + money : verb;
                submit.interactable = blocker == null;
            }
            amount.onValueChanged.AddListener(Refresh);
            Refresh(amount.text);

            BankDisclosure(w, $"活期月息 {GameSession.BankMonthlyRate * 100:0.#}% · 随时可取", draft.ShowTransferRules,
                () => draft.ShowTransferRules = !draft.ShowTransferRules,
                $"利息每天计入余额，累计利息 {S.BankInterestEarned}。贷款到期时，如现金不足，银行会先划转活期存款还款。");
        }

        private void BuildBankBorrow(Window w, BankDeskDraft draft)
        {
            var inputs = Ui.Row(w.Content, 74, 12);
            var amountField = Ui.Size(Ui.Rect(inputs, "BankLoanAmountField"), 0, 74, 1);
            Ui.VList(amountField, 4, 0);
            Line(amountField, $"借款金额（每 ¥{GameSession.AppLoanAmountStep} 一档）", 17);
            var amount = BankAmountInput(amountField, "BankLoanAmount", draft.LoanAmount, 8);
            var termField = Ui.Size(Ui.Rect(inputs, "BankLoanTermField"), 150, 74, 0);
            Ui.VList(termField, 4, 0);
            Line(termField, "期限", 17);

            var quoteCard = Ui.FlatPanel(w.Content, "BankLoanQuote").rectTransform;
            Ui.VList(quoteCard, 2, 10);
            var interest = BankQuoteRow(quoteCard, $"总利息 · 月息 {GameSession.AppLoanMonthlyRatePercent}%", Ui.TextColor);
            interest.value.name = "BankLoanInterest";
            var due = BankQuoteRow(quoteCard, "", Ui.Accent);
            due.label.name = "BankLoanDueDate";
            due.value.name = "BankLoanDueAmount";

            var feedback = Para(w.Content, "", 15, Ui.FlatDown);
            AppLoan quote = null;
            var apply = Ui.Size(Ui.Button(w.Content, "申请借款", () =>
            {
                if (quote == null) return;
                ConfirmLoanApplication(quote, true, () =>
                {
                    draft.Page = BankPage.Loans;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                });
            }, 19, Ui.ButtonHot), -1, 46);
            apply.name = "BankLoanApply";
            void Refresh()
            {
                bool valid = TryBankAmount(draft.LoanAmount, GameSession.AppLoanMaximumYuan, GameSession.AppLoanAmountStep, out long yuan);
                quote = valid ? LoanQuote(yuan, draft.Months) : null;
                var date = S.Clock.Today.AddDays(draft.Months * GameDate.DaysPerMonth);
                interest.value.text = valid ? quote.TermInterest.ToString() : "—";
                due.label.text = $"{date} 自动扣款";
                due.value.text = valid ? quote.TotalDue.ToString() : "—";
                string blocker = S.BankBlocker() ?? (!valid ? $"借款金额须为 ¥{GameSession.AppLoanAmountStep} 的整数倍，最高 ¥{GameSession.AppLoanMaximumYuan:N0}。" : null);
                feedback.text = blocker ?? "";
                feedback.gameObject.SetActive(blocker != null && draft.LoanAmount.Length > 0);
                apply.interactable = blocker == null;
            }
            var months = Ui.Size(Ui.Choice(termField, Enumerable.Range(1, GameSession.AppLoanMaximumMonths).Select(n => n + " 个月"),
                draft.Months - 1, i => { draft.Months = i + 1; Refresh(); }), -1, 38);
            months.name = "BankLoanMonths";
            amount.onValueChanged.AddListener(value => { draft.LoanAmount = value; Refresh(); });
            Refresh();

            BankDisclosure(w, "扣款与提前结清规则", draft.ShowLoanRules, () => draft.ShowLoanRules = !draft.ShowLoanRules,
                "借款按原本金计单利，每月按 30 天计算。到期自动扣除本息，余额不足可扣为负数。提前整笔结清按已使用月份计息，不足一个月按一个月。");
        }

        private void BuildBankLoans(Window w)
        {
            if (S.AppLoans.Count == 0)
            {
                Line(w.Content, "暂无借款记录", 17, Ui.Dim);
                return;
            }
            var list = Ui.Scroll(w.Content, 360);
            foreach (var loan in LoanRecords())
            {
                var card = Ui.FlatPanel(list, "BankLoan_" + loan.Id, Ui.FlatBand).rectTransform;
                Ui.VList(card, 2, 10);
                var summary = Ui.Row(card, 30);
                Ui.Size(Ui.Label(summary, loan.Repaid ? "已结清" : "到期应还 " + loan.TotalDue, 19,
                    color: loan.Repaid ? Ui.Dim : Ui.Accent), 0, -1, 1);
                if (!loan.Repaid)
                    Ui.Size(Ui.Button(summary, "提前结清", () => ConfirmLoanRepayment(w, loan, atBank: true),
                        16, Ui.ButtonBg, S.BankBlocker() == null && S.Ledger.CanAfford(S.LoanPayoff(loan))), 120, 30).name = "BankRepay_" + loan.Id;
                string lender = loan.NoInterest ? $" · {S.CityPerson(loan.Lender)?.Name ?? "朋友"}的无息借款" : "";
                string date = loan.Repaid ? $"{new GameDate(loan.RepaidDay)} 结清 · 实付利息 {Money.FromCents(loan.PaidInterestCents)}"
                    : $"{new GameDate(loan.DueDay)} 到期";
                Para(card, $"借款 #{loan.Id} · 本金 {loan.Principal} · {date}{lender}", 16, Ui.Dim);
            }
        }

        private static void BankBalance(Transform parent, string caption, Money amount, string name)
        {
            var card = Ui.Size(Ui.FlatPanel(parent, name), 0, 75, 1).rectTransform;
            Ui.VList(card, 0, 9);
            Ui.Size(Ui.Label(card, caption, 15, color: Ui.Dim), -1, 23);
            var value = Ui.Size(Ui.Label(card, amount.ToString(), 22, color: Ui.Good), -1, 30);
            value.fontStyle = FontStyle.Bold;
        }

        private static InputField BankAmountInput(Transform parent, string name, string text, int limit)
        {
            var input = Ui.Size(Ui.TextInput(parent, text, "输入金额", null), 0, 38, 1);
            input.name = name;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = limit;
            return input;
        }

        private static bool TryBankAmount(string text, long max, int step, out long yuan) =>
            long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out yuan)
            && yuan >= step && yuan <= max && yuan % step == 0;

        private static (Text label, Text value) BankQuoteRow(Transform parent, string caption, Color color)
        {
            var row = Ui.Row(parent, 30);
            var label = Ui.Size(Ui.Label(row, caption, 17, color: color), 0, -1, 1);
            var value = Ui.Size(Ui.Label(row, "", 20, TextAnchor.MiddleRight, color), 185);
            value.fontStyle = FontStyle.Bold;
            return (label, value);
        }

        private static void BankDisclosure(Window w, string title, bool open, Action toggle, string details)
        {
            var button = Ui.Size(Ui.Button(w.Content, (open ? "▾ " : "▸ ") + title,
                () => { toggle(); w.Rebuild(); }, 16, Ui.FlatBand), -1, 34);
            var text = button.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Ui.Dim;
            if (open) Para(w.Content, details, 16, Ui.Dim);
        }
    }
}
