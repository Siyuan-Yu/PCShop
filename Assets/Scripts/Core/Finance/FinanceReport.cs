using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Finance
{
    /// <summary>某一天的收支：营业额（零售 + 装机）和总支出。</summary>
    public sealed class DayFinance
    {
        public GameDate Date;
        public Money Revenue;
        public Money Expense;

        public Money Net => Revenue - Expense;
    }

    /// <summary>从账本里按天汇总，给“经营报表”画图用。</summary>
    public static class FinanceReport
    {
        public const string RetailCategory = "零售";
        public const string BuildCategory = "装机";

        /// <summary>截止 today（含）的最近 days 天，按日期从早到晚。</summary>
        public static List<DayFinance> Daily(Ledger ledger, GameDate today, int days)
        {
            var list = new List<DayFinance>();
            for (int i = days - 1; i >= 0; i--)
            {
                int d = today.TotalDays - i;
                if (d < 0) continue;
                list.Add(new DayFinance { Date = new GameDate(d) });
            }
            if (list.Count == 0) return list;
            int first = list[0].Date.TotalDays;
            foreach (LedgerEntry e in ledger.Entries)
            {
                int idx = e.Date.TotalDays - first;
                if (idx < 0 || idx >= list.Count) continue;
                if (e.Amount.IsPositive)
                {
                    if (e.Category == RetailCategory || e.Category == BuildCategory) list[idx].Revenue += e.Amount;
                }
                else
                {
                    list[idx].Expense += -e.Amount;
                }
            }
            return list;
        }
    }
}
