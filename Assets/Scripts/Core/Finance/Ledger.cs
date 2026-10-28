using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Finance
{
    /// <summary>一条账目。Amount 带符号：收入为正，支出为负。</summary>
    public readonly struct LedgerEntry
    {
        public GameDate Date { get; }
        public string Category { get; }
        public Money Amount { get; }
        public string Memo { get; }
        public Money BalanceAfter { get; }

        public LedgerEntry(GameDate date, string category, Money amount, string memo, Money balanceAfter)
        {
            Date = date;
            Category = category;
            Amount = amount;
            Memo = memo;
            BalanceAfter = balanceAfter;
        }
    }

    /// <summary>
    /// 统一账本：所有玩法（装机、行情、工资、房租、代言…）都通过它收支，
    /// 保证资金只有一个来源，也方便做现金流统计和存档。
    /// </summary>
    public sealed class Ledger
    {
        private readonly List<LedgerEntry> _entries = new List<LedgerEntry>();

        public Money Balance { get; private set; }
        public IReadOnlyList<LedgerEntry> Entries => _entries;

        public Ledger(Money openingBalance)
        {
            Balance = openingBalance;
        }

        public Ledger() : this(Money.Zero)
        {
        }

        public bool CanAfford(Money amount) => amount >= Money.Zero && Balance >= amount;

        /// <summary>收入。amount 必须为正。</summary>
        public LedgerEntry Credit(GameDate date, string category, Money amount, string memo = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "收入必须为正。");
            return Append(date, category, amount, memo);
        }

        /// <summary>支出，余额不足则失败并且不记账。amount 为要支付的正数。</summary>
        public bool TryDebit(GameDate date, string category, Money amount, string memo = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "支出金额必须为正。");
            if (Balance < amount) return false;
            Append(date, category, -amount, memo);
            return true;
        }

        /// <summary>强制支出，允许余额变为负数（房租、工资等不得不付的账单）。</summary>
        public LedgerEntry ForceDebit(GameDate date, string category, Money amount, string memo = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "支出金额必须为正。");
            return Append(date, category, -amount, memo);
        }

        /// <summary>日期区间 [from, to]（含两端）内的净收支。</summary>
        public Money NetBetween(GameDate from, GameDate to)
        {
            Money sum = Money.Zero;
            for (int i = 0; i < _entries.Count; i++)
            {
                LedgerEntry e = _entries[i];
                if (e.Date >= from && e.Date <= to) sum += e.Amount;
            }
            return sum;
        }

        /// <summary>日期区间 [from, to]（含两端）内某类别的合计（带符号）。</summary>
        public Money SumCategory(string category, GameDate from, GameDate to)
        {
            Money sum = Money.Zero;
            for (int i = 0; i < _entries.Count; i++)
            {
                LedgerEntry e = _entries[i];
                if (e.Category == category && e.Date >= from && e.Date <= to) sum += e.Amount;
            }
            return sum;
        }

        private LedgerEntry Append(GameDate date, string category, Money signedAmount, string memo)
        {
            Balance += signedAmount;
            var entry = new LedgerEntry(date, category, signedAmount, memo, Balance);
            _entries.Add(entry);
            return entry;
        }

        private static void ValidateCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                throw new ArgumentException("账目类别不能为空。", nameof(category));
        }
    }
}
