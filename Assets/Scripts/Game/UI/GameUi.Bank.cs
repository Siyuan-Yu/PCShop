using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private long _bankAmountYuan = 1000;

        /// <summary>城郊银行柜台：活期存取款 + 借款（和手机“周转借款”同一套接口）。</summary>
        private void BuildBank(Window w, Place p)
        {
            Line(w.Content, "营业时间 " + S.OpeningHoursText(p) + " · 可以不带手机办理存取款和借款", 17, Ui.Dim);
            Line(w.Content, $"当前现金 {S.Ledger.Balance}    银行存款 {S.BankDeposit}    贷款待还 {S.AppLoanDebt}", 21, Ui.Accent).name = "BankSummary";

            Line(w.Content, "活期存款", 20, Ui.Accent);
            Para(w.Content, $"月息 {GameSession.BankMonthlyRate * 100:0.#}%，每天按存款余额计息并滚入存款（复利，满 30 天正好 {GameSession.BankMonthlyRate * 100:0.#}%）。存款计入净资产，随时可取；贷款到期现金不够时，银行会先划转存款还款。累计利息 " + S.BankInterestEarned + "。", 17);
            var amountRow = Ui.Row(w.Content, 40);
            Ui.Size(Ui.Label(amountRow, "存取金额", 18), 120);
            QuantityStepper(w, amountRow, "bankAmount", _bankAmountYuan, 1, 100000000, 100,
                v => _bankAmountYuan = (long)v, v => "¥" + v.ToString("N0"));
            var presets = Ui.Row(w.Content, 34);
            foreach (long amount in new long[] { 100, 1000, 10000, 50000 })
            {
                long selected = amount;
                Ui.Size(Ui.Button(presets, "¥" + amount.ToString("N0"), () => { _bankAmountYuan = selected; w.Rebuild(); }, 17), -1, -1, 1);
            }
            long amountYuan = _bankAmountYuan;
            Money money = Money.FromYuan(amountYuan);
            Money monthly = Money.FromCents((long)System.Math.Round(S.BankDeposit.Cents * GameSession.BankMonthlyRate));
            var actions = Ui.Row(w.Content, 46);
            Ui.Size(Ui.Button(actions, "存入 " + money, () => { if (S.BankDepositMoney(amountYuan)) w.Rebuild(); }, 19, Ui.ButtonHot,
                S.Ledger.CanAfford(money)), -1, 46, 1).name = "BankDeposit";
            Ui.Size(Ui.Button(actions, "取出 " + money, () => { if (S.BankWithdrawMoney(amountYuan)) w.Rebuild(); }, 19, Ui.ButtonBg,
                money <= S.BankDeposit), -1, 46, 1).name = "BankWithdraw";
            Ui.Size(Ui.Button(actions, "全部取出", () => { if (S.BankWithdrawAll()) w.Rebuild(); }, 19, Ui.ButtonBg,
                S.BankDeposit.IsPositive), -1, 46, 1).name = "BankWithdrawAll";
            if (S.BankDeposit.IsPositive) Line(w.Content, $"按当前存款，每月约可得利息 {monthly}", 17, Ui.Good);

            Line(w.Content, "借款", 20, Ui.Accent);
            Para(w.Content, "和手机「周转借款」完全一样：同样的利息、同样到期自动扣款、同样的还款提醒；没有智能手机的话只能在这里借。", 16, Ui.Dim);
            BuildLoanDesk(w, true);
        }
    }
}