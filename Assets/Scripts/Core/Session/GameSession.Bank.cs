using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 城郊银行：活期存款（月息 0.3%，按天复利计入存款）和柜台借款。
    /// 借款走的是和手机“周转借款”同一套接口（<see cref="GameSession.BorrowAppLoan"/> / RepayAppLoan），
    /// 利息、到期扣款、还款提醒都一样，只是给没有智能手机的玩家多一个办理渠道。
    /// </summary>
    public sealed partial class GameSession
    {
        public const string BankBusiness = "银行";
        public const string BankPlaceId = "city_bank";
        /// <summary>存款月息 0.3%。每天按 (1+月息)^(1/30)−1 计息，满 30 天正好是 0.3%。</summary>
        public const double BankMonthlyRate = 0.003;
        private static readonly double BankDailyRate = Math.Pow(1 + BankMonthlyRate, 1.0 / GameDate.DaysPerMonth) - 1;

        private long _bankDepositCents, _bankInterestCents;
        private double _bankFraction;

        public Money BankDeposit => Money.FromCents(_bankDepositCents);
        /// <summary>存款累计收到的利息。</summary>
        public Money BankInterestEarned => Money.FromCents(_bankInterestCents);
        public bool AtBank => CurrentPlace != null && CurrentPlace.Business == BankBusiness;
        public Place BankPlace => City.HasPlace(BankPlaceId) ? City.GetPlace(BankPlaceId) : null;

        /// <summary>为什么现在办不了银行业务；能办返回 null。</summary>
        public string BankBlocker()
        {
            if (IsGameOver || IsWorking) return "当前不能办理银行业务。";
            if (!AtBank) return "请到城郊银行柜台办理。";
            if (!IsPlaceOpen(CurrentPlace)) return "银行已打烊，营业时间 " + OpeningHoursText(CurrentPlace) + "。";
            return null;
        }

        public bool BankDepositMoney(long yuan)
        {
            string blocker = BankBlocker(); if (blocker != null) return Fail(blocker);
            if (yuan < 1) return Fail("存入金额至少 ¥1。");
            Money amount = Money.FromYuan(yuan);
            if (!Ledger.CanAfford(amount)) return Fail("现金不够存。");
            Ledger.TryDebit(Clock.Today, "银行存入", amount, "活期存款");
            _bankDepositCents += amount.Cents;
            Say($"存入 {amount}，银行存款 {BankDeposit}，月息 {BankMonthlyRate * 100:0.#}%。");
            return true;
        }

        public bool BankWithdrawMoney(long yuan)
        {
            string blocker = BankBlocker(); if (blocker != null) return Fail(blocker);
            if (yuan < 1) return Fail("取出金额至少 ¥1。");
            Money amount = Money.FromYuan(yuan);
            if (amount.Cents > _bankDepositCents) return Fail("存款不够取。");
            _bankDepositCents -= amount.Cents;
            Ledger.Credit(Clock.Today, "银行取出", amount, "活期存款");
            Say($"取出 {amount}，银行存款剩 {BankDeposit}。");
            return true;
        }

        /// <summary>取出全部存款（含零头）。</summary>
        public bool BankWithdrawAll()
        {
            string blocker = BankBlocker(); if (blocker != null) return Fail(blocker);
            if (_bankDepositCents <= 0) return Fail("没有存款可取。");
            Money amount = BankDeposit;
            _bankDepositCents = 0; _bankFraction = 0;
            Ledger.Credit(Clock.Today, "银行取出", amount, "活期存款");
            Say($"取出全部存款 {amount}。");
            return true;
        }

        /// <summary>每天凌晨按存款余额计息，利息直接滚进存款。</summary>
        private void AccrueBankInterest()
        {
            if (_bankDepositCents <= 0) return;
            _bankFraction += _bankDepositCents * BankDailyRate;
            long whole = (long)Math.Floor(_bankFraction);
            if (whole <= 0) return;
            _bankFraction -= whole;
            _bankDepositCents += whole; _bankInterestCents += whole;
        }

        /// <summary>贷款到期自动扣款时，现金不够就先从银行存款里划（不够的部分仍扣成负数，和原来一样）。</summary>
        private void CoverFromBankDeposit(Money due)
        {
            if (_bankDepositCents <= 0) return;
            long need = due.Cents - Ledger.Balance.Cents;
            if (need <= 0) return;
            long take = Math.Min(need, _bankDepositCents);
            _bankDepositCents -= take;
            Ledger.Credit(Clock.Today, "银行取出", Money.FromCents(take), "到期还款自动从存款划转");
            Say($"现金不够还款，银行自动划转存款 {Money.FromCents(take)}。");
        }

        private void RestoreBank(SaveData data)
        {
            _bankDepositCents = Math.Max(0, data.bankDepositCents);
            _bankInterestCents = Math.Max(0, data.bankInterestCents);
            _bankFraction = double.IsNaN(data.bankFraction) || data.bankFraction < 0 || data.bankFraction >= 1 ? 0 : data.bankFraction;
        }
    }
}