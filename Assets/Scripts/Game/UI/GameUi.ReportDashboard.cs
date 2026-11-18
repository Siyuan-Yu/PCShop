using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class ReportPriceViewState { public int Filter; public int Selected = -1; }
        private static string PriceMovement(double change) => Math.Abs(change) < .005 ? "—"
            : (change > 0 ? "↑ " : "↓ ") + Math.Abs(change).ToString("0.00") + "%";
        private static Color PriceMovementColor(double change) => Ui.ChangeColor(change, .004999999);
        private static void EconomicStoreCell(Transform row, string text, Color color) => ReportCell(row, text, 118, color);
        private static string ReportMoney(long cents) => Money.FromCents(cents).ToString();
        private static Text ReportText(Transform parent, string text, int size = 17, Color? color = null, float height = 26)
            => Ui.Size(Ui.Label(parent, text, size, color: color), -1, height);
        private static RectTransform ReportPanel(Transform parent, string name, float height, float flex = 1)
        {
            var panel = Ui.Size(Ui.Panel(parent, name, Ui.PanelBg2), 0, height, flex).rectTransform;
            Ui.VList(panel, 5, 12);
            return panel;
        }
        private static void ReportCard(Transform parent, string title, string value, string note, Color color)
        {
            var card = ReportPanel(parent, "ReportCard", 106);
            ReportText(card, title, 16, Ui.Dim, 22);
            ReportText(card, value, 27, color, 32).fontStyle = FontStyle.Bold;
            ReportText(card, note, 15, Ui.Dim, 23);
        }
        private static void ReportCell(Transform parent, string text, float width, Color? color = null)
            => Ui.Size(Ui.Label(parent, text, 16, TextAnchor.MiddleRight, color), width, 30);

        private void BuildCashDashboard(Transform parent, GameDate from, GameDate to, int selectedDay, Action<int> selectDay)
        {
            var cash = FinanceReport.CashSummary(S.Ledger, from, to);
            var entries = S.Ledger.Entries.Where(e => e.Date >= from && e.Date <= to
                && e.BusinessKind != LedgerBusinessKind.InternalTransfer).ToArray();
            var cards = Ui.Row(parent, 106, 8);
            ReportCard(cards, "现金变化", SignedReportCash(cash.Net + cash.InternalNet), "期初 " + cash.OpeningBalance + " → 期末 " + cash.ClosingBalance,
                (cash.Net + cash.InternalNet).IsNegative ? Ui.Warn : Ui.Good);
            ReportCard(cards, "实际收入", cash.Income.ToString(), "含经营、资产交易及借款等现金流入", Ui.Good);
            ReportCard(cards, "实际支出", cash.Expense.ToString(), "含进货、工资、租金及生活等现金流出", Ui.Warn);
            var largest = entries.Where(e => e.Amount.IsNegative).OrderBy(e => e.Amount.Cents).ToArray();
            string insight = largest.Length == 0 ? "本期没有现金支出记录。" :
                $"本期最大单笔支出：{largest[0].Date.Month}/{largest[0].Date.Day} · {largest[0].Category} · {ReportMoney(-largest[0].Amount.Cents)}。";
            if (cash.InternalNet != Money.Zero) insight += " 内部调拨 " + SignedReportCash(cash.InternalNet) + "，单独计入余额。";
            ReportText(parent, insight, 17, Ui.Accent, 36);
            var body = Ui.Row(parent, 460, 10);
            var left = ReportPanel(body, "CashOverview", 460, 1.65f);
            var right = ReportPanel(body, "CashDetails", 460);
            ReportText(left, "每日收支  ·  点击柱状图查看当天", 18, Ui.Accent, 28);
            ReportText(left, "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.Good) + ">● 收入</color>                         <color=#"
                + ColorUtility.ToHtmlStringRGB(Ui.Warn) + ">● 支出</color>   / 元", 14, Ui.Dim, 20);
            var chart = Ui.Size(Ui.Rect(left, "EconomicCashTrend"), -1, 160).gameObject.AddComponent<AssetTrendGraphic>();
            chart.DaySelected = selectDay;
            chart.Initialize(cash.IncomeDays, cash.ExpenseDays, from.TotalDays, false, true);
            var heading = Ui.Row(left, 32);
            Ui.Size(Ui.Label(heading, "门店收支", 18, color: Ui.Accent), 0, 32, 1);
            Ui.Size(Ui.Button(heading, "总账分类明细", () => OpenEconomicCashSources(cash, from, to), 15), 132, 30);
            var headers = Ui.Row(left, 30, 4);
            Ui.Size(Ui.Label(headers, "门店 · 点击查看明细", 15, color: Ui.Dim), 0, 30, 1);
            foreach (var name in new[] { "收入", "总支出", "现金差额" }) ReportCell(headers, name, 108, Ui.Dim);
            var reports = S.AssetStoreReports(from, to);
            Transform list = left;
            if (reports.Length > 3) list = Ui.Scroll(left, 112);
            foreach (var report in reports.OrderByDescending(r => r.IncomeCents + r.ExpenseCents))
            {
                var row = Ui.Row(list, 32, 4);
                Ui.Size(Ui.Button(row, report.Name, () => OpenAssetSources(new[] { report }, from, to), 15), 0, 32, 1);
                ReportCell(row, ReportMoney(report.IncomeCents), 108, Ui.Good);
                ReportCell(row, ReportMoney(report.ExpenseCents), 108, Ui.Warn);
                ReportCell(row, SignedReportCash(Money.FromCents(report.IncomeCents - report.ExpenseCents)), 108,
                    Ui.ChangeColor(report.IncomeCents - report.ExpenseCents));
            }
            if (reports.Length == 0) ReportText(left, "本期暂无门店记录。", 16, Ui.Dim);
            ReportText(right, "钱主要花在哪里", 19, Ui.Accent, 28);
            long topTotal = 0;
            foreach (var source in cash.ExpenseSources.OrderByDescending(s => s.Value).Take(3))
            {
                topTotal += source.Value;
                var row = Ui.Row(right, 28);
                Ui.Size(Ui.Label(row, source.Key, 16), 0, 28, 1);
                ReportCell(row, ReportMoney(source.Value) + $"  {source.Value / (double)Math.Max(1, cash.Expense.Cents):0%}", 175, Ui.Warn);
            }
            if (topTotal < cash.Expense.Cents) ReportText(right, "其余支出  " + ReportMoney(cash.Expense.Cents - topTotal), 15, Ui.Dim);
            if (cash.ExpenseSources.Count == 0) ReportText(right, "暂无支出", 16, Ui.Dim);
            int day = selectedDay >= from.TotalDays && selectedDay <= to.TotalDays ? selectedDay
                : largest.Length > 0 ? largest[0].Date.TotalDays : to.TotalDays;
            var date = new GameDate(day);
            ReportText(right, $"{date.Month}/{date.Day} · 当天明细", 19, Ui.Accent, 30);
            int index = day - from.TotalDays;
            ReportText(right, "收入 " + ReportMoney(cash.IncomeDays[index]) + "    支出 " + ReportMoney(cash.ExpenseDays[index]), 16);
            var daily = entries.Where(e => e.Date.TotalDays == day).OrderByDescending(e => Math.Abs(e.Amount.Cents)).ToArray();
            Transform detail = daily.Length > 4 ? Ui.Scroll(right, 124) : right;
            foreach (var e in daily)
            {
                var row = Ui.Row(detail, 27);
                Ui.Size(Ui.Label(row, e.Category, 15), 0, 27, 1);
                ReportCell(row, SignedReportCash(e.Amount), 140, e.Amount.IsNegative ? Ui.Warn : Ui.Good);
            }
            if (daily.Length == 0) ReportText(detail, "当天没有收支记录。", 16, Ui.Dim);
            ReportText(parent, "现金差额包含进货、资产交易与借还款，不等于经营利润。门店明细保留各项成本分类。", 15, Ui.Dim, 28);
        }

        private static bool ValidPrice(PriceDay day, int group) => day != null && (group != 7 || day.SchemaVersion >= 1);
        private static double? ComparePrice(PriceDay current, PriceDay baseline, int group)
            => ValidPrice(current, group) && ValidPrice(baseline, group) ? (current.Prices[group] / baseline.Prices[group] - 1) * 100 : (double?)null;
        private static string PriceQuote(PriceDay day, int group) => !ValidPrice(day, group) ? "—"
            : group >= GameSession.PropertyPriceGroupStart ? (day.Prices[group] / 100).ToString("0.00")
            : "¥" + (day.Prices[group] / 100).ToString("#,0.##");
        private void BuildPriceDashboard(Transform parent, int start, int end, ReportPriceViewState state, Action rebuild, bool live)
        {
            var history = S.PriceDays;
            var current = live ? S.CurrentMarketPriceSnapshot : history.LastOrDefault(d => d.Day >= start && d.Day <= end);
            if (current == null) { ReportText(parent, "本期暂无价格记录。", 18, Ui.Dim); return; }
            // Do not substitute an arbitrarily old observation for a missing previous close.
            var baseline = history.FirstOrDefault(d => d.Day == (live ? current.Day - 1 : start - 1));
            if (!live && start == 0) baseline = history.FirstOrDefault(d => d.Day == 0);
            var week = history.FirstOrDefault(d => d.Day == current.Day - 7);
            var groups = Enumerable.Range(0, GameSession.PriceGroups.Length).ToArray();
            var changes = groups.Select(g => ComparePrice(current, baseline, g)).ToArray();
            var comparable = groups.Where(g => g < GameSession.PropertyPriceGroupStart && changes[g].HasValue).ToArray();
            int[] up = comparable.Where(g => changes[g] >= .005).OrderByDescending(g => changes[g]).ToArray();
            int[] down = comparable.Where(g => changes[g] <= -.005).OrderBy(g => changes[g]).ToArray();
            if (live) ReportText(parent, $"实时报价 · {S.Clock.Today}  {S.Clock.Now.Hour:00}:{S.Clock.Now.Minute:00}    |    对比昨日 / 7 日前", 17, Ui.Dim, 32);
            var cards = Ui.Row(parent, 106, 8);
            ReportCard(cards, live ? "今日变化" : "本期变化", $"<color=#{ColorUtility.ToHtmlStringRGB(Ui.Good)}>↑ {up.Length}</color>     <color=#{ColorUtility.ToHtmlStringRGB(Ui.Warn)}>↓ {down.Length}</color>     — {comparable.Length - up.Length - down.Length}",
                comparable.Length == 0 ? "暂无相邻基准日记录" : "同口径品类数量 · 房产单列", Ui.Accent);
            void Extreme(int[] indices, string title, Color tint)
            {
                int g = indices.FirstOrDefault();
                ReportCard(cards, title + (indices.Length == 0 ? "" : " · " + GameSession.PriceGroups[g]), indices.Length == 0 ? "—" : PriceMovement(changes[g].Value),
                    indices.Length == 0 ? "暂无符合条件的品类" : PriceQuote(baseline, g) + " → " + PriceQuote(current, g), tint);
            }
            Extreme(up, "↑ 最大变化", Ui.Good); Extreme(down, "↓ 最大变化", Ui.Warn);
            ReportText(parent, baseline == null ? "缺少相邻基准日记录，变化显示 —；仍可查看已有报价与历史走势。"
                : $"对比基准：{new GameDate(baseline.Day)}。点击品类可查看实际报价走势、公开消息和报价口径。", 16, Ui.Accent, 34);
            var body = Ui.Row(parent, 402, 10);
            var left = ReportPanel(body, "PriceTable", 402, 1.65f);
            var right = ReportPanel(body, "PriceDetail", 402);
            var filters = Ui.Row(left, 32, 4);
            string[] names = { live ? "今日变化" : "本期变化", "商品与生活", "电脑配件", "房产与租金" };
            for (int i = 0; i < names.Length; i++)
            {
                int f = i;
                Ui.Size(Ui.Button(filters, names[i], () => { state.Filter = f; state.Selected = -1; rebuild(); }, 15,
                    Ui.SelectedButton(state.Filter == f)), 0, 32, 1);
            }
            int property = GameSession.PropertyPriceGroupStart;
            var visible = groups.Where(g => state.Filter == 1 ? g >= 6 && g < property : state.Filter == 2 ? g < 6
                : state.Filter == 3 ? g >= property : g < property && changes[g].HasValue && Math.Abs(changes[g].Value) >= .005)
                .OrderByDescending(g => state.Filter == 0 ? Math.Abs(changes[g] ?? 0) : 0).ToArray();
            var header = Ui.Row(left, 28, 4);
            Ui.Size(Ui.Label(header, "品类", 15, color: Ui.Dim), 0, 28, 1);
            ReportCell(header, "参考均价", 112, Ui.Dim);
            ReportCell(header, live ? "较昨日" : "较期初", 96, Ui.Dim);
            if (live) ReportCell(header, "较 7 日前", 96, Ui.Dim);
            else ReportCell(header, "基准报价", 112, Ui.Dim);
            if (!visible.Contains(state.Selected)) state.Selected = (visible.Length == 0 ? -1 : visible[0]);
            Transform list = visible.Length > 8 ? Ui.Scroll(left, 277) : left;
            foreach (int g in visible)
            {
                var row = Ui.Row(list, 32, 4);
                Ui.Size(Ui.Button(row, GameSession.PriceGroups[g] + "  ›", () => { state.Selected = g; rebuild(); }, 16,
                    Ui.SelectedButton(state.Selected == g)), 0, 32, 1).name = "EconomicPriceGroup_" + g;
                ReportCell(row, PriceQuote(current, g), 112);
                ReportCell(row, changes[g].HasValue ? PriceMovement(changes[g].Value) : "—", 96,
                    changes[g].HasValue ? PriceMovementColor(changes[g].Value) : Ui.Dim);
                var secondary = live ? ComparePrice(current, week, g) : null;
                ReportCell(row, live ? secondary.HasValue ? PriceMovement(secondary.Value) : "—" : PriceQuote(baseline, g),
                    live ? 96 : 112, live && secondary.HasValue ? PriceMovementColor(secondary.Value) : Ui.Dim);
            }
            if (visible.Length == 0)
            {
                ReportText(left, "本期暂无可比较的变化。", 17, Ui.Dim);
                Ui.Button(left, "查看商品与生活", () => { state.Filter = 1; rebuild(); }, 16);
            }
            int selected = state.Selected;
            if (selected >= 0)
            {
                bool isProperty = selected >= property;
                ReportText(right, GameSession.PriceGroups[selected], 21, Ui.Accent, 26);
                ReportText(right, PriceQuote(current, selected), 28, Ui.Accent, 30).fontStyle = FontStyle.Bold;
                ReportText(right, isProperty ? "挂牌指数 · 100 = 基准水平" : "品类参考均价 · 非单件商品成交价", 14, Ui.Dim, 20);
                var points = history.Where(d => d.Day >= (live ? Math.Max(0, end - 29) : start - 1) && d.Day <= current.Day
                    && d.Day != current.Day && ValidPrice(d, selected)).Concat(ValidPrice(current, selected) ? new[] { current } : Array.Empty<PriceDay>()).ToArray();
                var trend = Ui.Size(Ui.Rect(right, "SelectedPriceTrend"), -1, 94).gameObject.AddComponent<ReportPriceTrendGraphic>();
                trend.Initialize(points.Select(p => p.Day).ToArray(), points.Select(p => p.Prices[selected] / 100).ToArray());
                var news = IndustryCycles.CommodityEventOn(current.Day, S.Market.CommoditySeed);
                bool relevant = news.IsActive(current.Day) && news.Name == GameSession.PriceGroups[selected];
                ReportText(right, "相关公开消息", 17, Ui.Accent, 22);
                ReportText(right, relevant ? news.Headline : "暂无已确认的相关公开消息。", 15, Ui.Dim, 32);
                ReportText(right, changes[selected].HasValue ? "基准 " + PriceQuote(baseline, selected) + " → " + PriceQuote(current, selected)
                    + "  " + PriceMovement(changes[selected].Value) : "—  缺少同口径基准报价", 15, Ui.Dim, 24);
            }
            else ReportText(right, "选择左侧品类查看详情。", 17, Ui.Dim);
            var actions = Ui.Row(parent, 34);
            Ui.Size(Ui.Label(actions, "房产显示指数；商品显示元。缺少历史或报价口径变化时显示 —。", 14, color: Ui.Dim), 0, 34, 1);
            Ui.Size(Ui.Button(actions, "查看具体商品", OpenCityProcurement, 15), 132, 32);
            Ui.Size(Ui.Button(actions, "查看我的资产", () => OpenAssetWorkspace(), 15), 132, 32);
        }
    }
}
