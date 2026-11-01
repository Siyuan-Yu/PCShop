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
        public string EntryId { get; }
        public LedgerAttribution Attribution { get; }
        public string ShopId => Attribution?.ShopId;
        public string SourceEventId => Attribution?.SourceEventId;
        public LedgerBusinessKind BusinessKind => Attribution?.BusinessKind ?? LedgerBusinessKind.Historical;

        public LedgerEntry(GameDate date, string category, Money amount, string memo, Money balanceAfter,
            string entryId = null, LedgerAttribution attribution = null)
        {
            Date = date;
            Category = category;
            Amount = amount;
            Memo = memo;
            BalanceAfter = balanceAfter;
            EntryId = entryId;
            Attribution = attribution ?? LedgerAttribution.FromCategory(category);
        }
    }

    /// <summary>
    /// 统一账本：所有玩法（装机、行情、工资、房租、代言…）都通过它收支，
    /// 保证资金只有一个来源，也方便做现金流统计和存档。
    /// </summary>
    public sealed class Ledger
    {
        private readonly List<LedgerEntry> _entries = new List<LedgerEntry>();
        private readonly Dictionary<string, LedgerEntry> _sources = new Dictionary<string, LedgerEntry>();
        private long _nextEntryId = 1;
        public int Revision { get; private set; }

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
        public LedgerEntry Credit(GameDate date, string category, Money amount, string memo = null, LedgerAttribution attribution = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "收入必须为正。");
            return Append(date, category, amount, memo, attribution);
        }

        /// <summary>支出，余额不足则失败并且不记账。amount 为要支付的正数。</summary>
        public bool TryDebit(GameDate date, string category, Money amount, string memo = null, LedgerAttribution attribution = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "支出金额必须为正。");
            if (Existing(attribution, -amount, out _)) return true;
            if (Balance < amount) return false;
            Append(date, category, -amount, memo, attribution);
            return true;
        }

        /// <summary>强制支出，允许余额变为负数（房租、工资等不得不付的账单）。</summary>
        public LedgerEntry ForceDebit(GameDate date, string category, Money amount, string memo = null, LedgerAttribution attribution = null)
        {
            ValidateCategory(category);
            if (!amount.IsPositive)
                throw new ArgumentOutOfRangeException(nameof(amount), "支出金额必须为正。");
            return Append(date, category, -amount, memo, attribution);
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

        /// <summary>读档：替换全部账目和余额。</summary>
        public void Restore(Money balance, IEnumerable<LedgerEntry> entries)
        {
            _entries.Clear();
            if (entries != null) _entries.AddRange(entries);
            Balance = balance;
            _sources.Clear();
            _nextEntryId = _entries.Count + 1;
            foreach (var e in _entries)
            {
                if (!string.IsNullOrEmpty(e.SourceEventId)) _sources[e.SourceEventId] = e;
                if (long.TryParse(e.EntryId, out var id)) _nextEntryId = Math.Max(_nextEntryId, id + 1);
            }
            Revision++;
        }

        private bool Existing(LedgerAttribution attribution, Money amount, out LedgerEntry entry)
        {
            if (attribution?.SourceEventId != null && _sources.TryGetValue(attribution.SourceEventId, out entry))
            {
                if (entry.Amount != amount || entry.ShopId != attribution.ShopId || entry.BusinessKind != attribution.BusinessKind
                    || entry.Attribution.PropertyId != attribution.PropertyId || entry.Attribution.ActivityId != attribution.ActivityId)
                    throw new InvalidOperationException("同一资金来源不能重放为不同金额或业务归属：" + attribution.SourceEventId);
                return true;
            }
            entry = default; return false;
        }
        public bool HasSource(string sourceEventId) => sourceEventId != null && _sources.ContainsKey(sourceEventId);
        /// <summary>资产在经营库存与个人用品之间转用；只记录数量与可信成本，不制造现金收支。</summary>
        public LedgerEntry RecordNonCash(GameDate date, string category, string memo, LedgerAttribution attribution)
        {
            ValidateCategory(category);
            if (attribution == null || attribution.BusinessKind != LedgerBusinessKind.InternalTransfer)
                throw new ArgumentException("非现金记录必须明确为内部划转。");
            return Append(date, category, Money.Zero, memo, attribution);
        }

        private LedgerEntry Append(GameDate date, string category, Money signedAmount, string memo, LedgerAttribution attribution)
        {
            if (Existing(attribution, signedAmount, out var existing)) return existing;
            Balance += signedAmount;
            var entry = new LedgerEntry(date, category, signedAmount, memo, Balance, (_nextEntryId++).ToString(), attribution);
            _entries.Add(entry);
            if (!string.IsNullOrEmpty(entry.SourceEventId)) _sources[entry.SourceEventId] = entry;
            Revision++;
            return entry;
        }

        private static void ValidateCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                throw new ArgumentException("账目类别不能为空。", nameof(category));
        }
    }
}
