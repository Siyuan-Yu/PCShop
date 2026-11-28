using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Finance
{
    /// <summary>某一天的营业收款和支出。</summary>
    public sealed class DayFinance
    {
        public GameDate Date;
        public Money Revenue;
        public Money Expense;
        public Money OtherIncome;
        public Money CashChange => Revenue + OtherIncome - Expense;

        public Money Net => Revenue - Expense;
    }

    public sealed class CashPeriodSummary
    {
        public Money Income, Expense, ClosingBalance, InternalNet;
        public Money Net => Income - Expense;
        public Money OpeningBalance => ClosingBalance - Net - InternalNet;
        public long[] IncomeDays, ExpenseDays;
        public readonly Dictionary<string, long> IncomeSources = new Dictionary<string, long>();
        public readonly Dictionary<string, long> ExpenseSources = new Dictionary<string, long>();
    }

    /// <summary>从账本里按天汇总，给“经营报表”画图用。</summary>
    public static class FinanceReport
    {
        /// <summary>按入账日期汇总所有实际现金收付，内部调拨单列以便核对余额。</summary>
        public static CashPeriodSummary CashSummary(Ledger ledger, GameDate from, GameDate to)
        {
            int days = System.Math.Max(0, to.TotalDays - from.TotalDays + 1);
            var result = new CashPeriodSummary
            {
                ClosingBalance = ledger.Balance, IncomeDays = new long[days], ExpenseDays = new long[days]
            };
            foreach (var entry in ledger.Entries)
            {
                if (entry.Date > to) { result.ClosingBalance -= entry.Amount; continue; }
                if (entry.Date < from || entry.Amount == Money.Zero) continue;
                if (entry.BusinessKind == LedgerBusinessKind.InternalTransfer)
                {
                    result.InternalNet += entry.Amount;
                    continue;
                }

                bool income = entry.Amount.IsPositive;
                var amount = entry.Amount.Abs();
                int day = entry.Date.TotalDays - from.TotalDays;
                if (income)
                {
                    result.Income += amount;
                    result.IncomeDays[day] += amount.Cents;
                }
                else
                {
                    result.Expense += amount;
                    result.ExpenseDays[day] += amount.Cents;
                }
                var sources = income ? result.IncomeSources : result.ExpenseSources;
                sources.TryGetValue(entry.Category, out long previous);
                sources[entry.Category] = previous + amount.Cents;
            }
            return result;
        }

        /// <summary>经营现金口径；内部划转、借贷本金、投资交易永远不计入销售或经营净额。</summary>
        public static CityFinanceSummary CitySummary(Ledger ledger, GameDate from, GameDate to, string shopId = null, string salesAccountId = null)
        {
            var result = new CityFinanceSummary { From = from, To = to, ShopId = shopId };
            foreach (var e in ledger.Entries)
            {
                if (e.Date < from || e.Date > to) continue;
                if (shopId != null && (e.Amount.IsPositive && IsOperatingSale(e)
                    ? !IsShopSale(e, shopId, salesAccountId) : e.ShopId != shopId)) continue;
                switch (e.BusinessKind)
                {
                    case LedgerBusinessKind.Operating:
                        if (e.Amount.IsPositive) result.OperatingIn += e.Amount; else result.OperatingOut -= e.Amount;
                        if (IsOperatingSale(e)) result.Sales += e.Amount;
                        if (e.ShopId == null) { if (e.Amount.IsPositive) result.UnassignedOperatingIn += e.Amount; else result.UnassignedOperatingOut -= e.Amount; }
                        break;
                    case LedgerBusinessKind.Personal: result.PersonalNet += e.Amount; break;
                    case LedgerBusinessKind.AssetInvestment: result.InvestmentNet += e.Amount; break;
                    case LedgerBusinessKind.Financing: result.FinancingNet += e.Amount; break;
                    case LedgerBusinessKind.InternalTransfer: result.InternalNet += e.Amount; break;
                    default: result.HistoricalNet += e.Amount; break;
                }
            }
            return result;
        }
        public const string RetailCategory = "零售";
        public const string BuildCategory = "装机";
        public const string PetBoardingCategory = "宠物代养";

        public static bool IsOperatingSale(LedgerEntry entry) => IsOperatingSale(entry.Category, entry.Attribution);

        internal static bool IsOperatingSale(string category, LedgerAttribution attribution) =>
            attribution?.BusinessKind == LedgerBusinessKind.Operating
            && (category == RetailCategory || category == BuildCategory || category == PetBoardingCategory
                || attribution.TransactionKind == "销售");

        public static bool IsShopSale(LedgerEntry entry, string shopId, string salesAccountId) =>
            entry.Amount.IsPositive && IsOperatingSale(entry)
            && (salesAccountId == null ? entry.ShopId == shopId : entry.Attribution.SalesAccountId == salesAccountId);

        public static List<DayFinance> OperatingDaily(Ledger ledger, GameDate today, int days, string shopId = null, string salesAccountId = null)
        {
            var result = new List<DayFinance>();
            for (int i = days - 1; i >= 0; i--)
            {
                int day = today.TotalDays - i; if (day < 0) continue;
                var date = new GameDate(day); var summary = CitySummary(ledger, date, date, shopId, salesAccountId);
                result.Add(new DayFinance { Date = date, Revenue = summary.Sales, Expense = summary.OperatingOut,
                    OtherIncome = summary.OperatingIn - summary.Sales });
            }
            return result;
        }

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
                    else list[idx].OtherIncome += e.Amount;
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
