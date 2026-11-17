using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void CheckEconomicReport()
        {
            if (S.IsGameOver || S.IsWorking || S.Racing || S.CityActivityExclusive || AnyModalOpen || PopupOpen
                || S.OpeningAgencyGuidePending || OpeningMealRestricted || _ctrl.Placing || _ctrl.PetFocusActive
                || S.Trip != null || S.PendingEvent != null)
                return;
            if (S.MarketPriceAlerts.Count > 0) OpenMarketPriceAlert();
            else if (S.EconomicReportPending && S.EconomicReportAutomatic) OpenEconomicReport();
        }

        private void OpenMarketPriceAlert()
        {
            var moves = S.MarketPriceAlerts;
            var day = new GameDate(S.MarketPriceAlertDay);
            S.DismissMarketPriceAlert();
            OpenWindow("物价异动", 640, Mathf.Min(680, 228 + moves.Count * 34), w =>
            {
                Line(w.Content, $"{day} · 较前一日", 18, Ui.Dim);
                foreach (var move in moves)
                {
                    var row = Ui.Row(w.Content, 34, 10);
                    Ui.Size(Ui.Label(row, move.Name, 18), -1, -1, 1);
                    var label = Ui.Label(row, $"{(move.Change > 0 ? "上涨" : "下跌")} {Math.Abs(move.Change):0.##%}",
                        19, TextAnchor.MiddleRight, move.Change > 0 ? Ui.Warn : Ui.Good);
                    label.fontStyle = FontStyle.Bold;
                    Ui.Size(label, 154, -1);
                }
                var actions = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Button(actions, "知道了", w.Close, 17), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "查看物价", () => { w.Close(); OpenMarketPanel(); }, 17, Ui.ButtonHot), -1, 42, 1);
            });
        }

        private void OpenEconomicReport()
        {
            S.DismissEconomicReport();
            int period = -1, selected = -1;
            bool showPrices = false;
            var window = OpenWindow("经济简报", 1260, 920, w =>
            {
                var (start, end, ongoing) = BuildEconomicPeriodNavigation(w, period, false, next => period = next);

                var tabs = Ui.Row(w.Content, 42);
                Ui.Tab(tabs, "收支结算", () => { showPrices = false; w.Rebuild(); }, !showPrices);
                Ui.Tab(tabs, "物价涨跌", () => { showPrices = true; w.Rebuild(); }, showPrices);

                if (showPrices) BuildEconomicPriceSummary(w.Content, start, end, ongoing, selected, group =>
                {
                    selected = group;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                });
                else BuildEconomicCashSummary(w.Content, new GameDate(start), new GameDate(end));
            });

            var scroll = (RectTransform)window.Content.parent.parent;
            scroll.Anchor(0, 0, 1, 1, 0, 48, 0, 50);
            var footer = Ui.Row(scroll.parent, 30);
            footer.Anchor(0, 0, 1, 0, 16, 10, 16, -40);
            Ui.Size(Ui.Label(footer, "自动弹出", 15, color: Ui.Dim), 70);
            var cadences = new (string label, EconomicReportCadence? cadence)[]
            {
                ("每周", EconomicReportCadence.Weekly),
                ("每两周", EconomicReportCadence.Biweekly),
                ("每月", EconomicReportCadence.Monthly),
                ("关闭", null)
            };
            EconomicReportCadence? currentCadence = S.EconomicReportAutomatic ? S.ReportCadence : (EconomicReportCadence?)null;
            Ui.Size(Ui.Choice(footer, cadences.Select(option => option.label), Array.FindIndex(cadences, option => option.cadence == currentCadence), index =>
            {
                var cadence = cadences[index].cadence;
                if (cadence.HasValue && S.ReportCadence != cadence.Value) period = -1;
                S.SetEconomicReportCadence(cadence);
                window.Rebuild();
            }, opensUp: true), 120, 30);
        }

        private (int Start, int End, bool Ongoing) BuildEconomicPeriodNavigation(Window window, int period,
            bool startAtCurrent, Action<int> select)
        {
            int today = S.Clock.Today.TotalDays;
            int span = S.EconomicReportPeriodDays;
            int anchor = S.EconomicReportAnchorDay;
            int first = S.PriceDays.FirstOrDefault()?.Day ?? today;
            int earliest = (first - anchor) / span;
            int current = (today - anchor) / span;
            if (period < 0) period = startAtCurrent ? current : current - 1;
            period = Math.Max(earliest, Math.Min(current, period));
            int start = anchor + period * span;
            int end = Math.Min(today, start + span - 1);
            bool ongoing = period == current;

            var nav = Ui.Row(window.Content, 40);
            void ChangePeriod(int next)
            {
                select(next);
                window.Rebuild();
            }
            Ui.Size(Ui.Button(nav, "上一期", () => ChangePeriod(period - 1), 17, Ui.ButtonBg, period > earliest), 110).name = "EconomicPrevPeriod";
            string range = $"{new GameDate(start)} — {new GameDate(end)}" + (ongoing ? " · 进行中" : "");
            Ui.Size(Ui.Label(nav, range, 18, TextAnchor.MiddleCenter), -1, -1, 1).name = "EconomicPeriodRange";
            Ui.Size(Ui.Button(nav, "下一期", () => ChangePeriod(period + 1), 17, Ui.ButtonBg, period < current), 110).name = "EconomicNextPeriod";
            return (start, end, ongoing);
        }

        private void BuildEconomicCashSummary(Transform parent, GameDate from, GameDate to)
        {
            var cash = FinanceReport.CashSummary(S.Ledger, from, to);
            var cards = Ui.Row(parent, 160, 8);
            BuildAssetMoneyCard(cards, "总收入", cash.Income.Cents, Ui.Good, cash.IncomeSources);
            BuildAssetMoneyCard(cards, "总支出", cash.Expense.Cents, AssetTrendGraphic.ExpenseColor, cash.ExpenseSources);
            var balance = Ui.Size(Ui.Panel(cards, "EconomicCashNet", Ui.PanelBg2), -1, 160, 1).rectTransform;
            Ui.VList(balance, 2, 8);
            Line(balance, "净收支", 16, Ui.Dim);
            Line(balance, SignedReportCash(cash.Net), 26, cash.Net.IsNegative ? Ui.Warn : Ui.Good);
            Line(balance, "期初现金 " + cash.OpeningBalance, 16);
            Line(balance, "期末现金 " + cash.ClosingBalance, 16);
            if (cash.InternalNet != Money.Zero)
                Line(parent, "内部调拨净额 " + SignedReportCash(cash.InternalNet), 16, Ui.Dim);

            var legend = Ui.Row(parent, 26);
            Ui.Size(Ui.Label(legend, "每日现金收支", 18), -1, 26, 1);
            Ui.Size(Ui.Label(legend, "● 收入", 15, TextAnchor.MiddleRight, Ui.Good), 74);
            Ui.Size(Ui.Label(legend, "● 支出 · 元", 15, TextAnchor.MiddleRight, AssetTrendGraphic.ExpenseColor), 98);
            var chart = Ui.Size(Ui.Rect(parent, "EconomicCashTrend"), -1, 154);
            chart.gameObject.AddComponent<AssetTrendGraphic>().Initialize(cash.IncomeDays, cash.ExpenseDays, from.TotalDays,
                compact: false, groupedBars: true);

            var reports = S.AssetStoreReports(from, to);
            var storeHeader = Ui.Row(parent, 34);
            Ui.Size(Ui.Label(storeHeader, "门店收支", 21, color: Ui.Accent), -1, 34, 1);
            Ui.Size(Ui.Button(storeHeader, "总账分类明细", () => OpenEconomicCashSources(cash, from, to), 16), 150, 32);
            if (reports.Length == 0) Line(parent, "本期暂无门店记录。", 17, Ui.Dim);
            else
            {
                var headers = new[] { "收入", "总支出", "进货", "工资", "租金", "其他支出", "收支差额" };
                var header = Ui.Row(parent, 30);
                Ui.Size(Ui.Label(header, "门店 · 点名称查看明细", 15, color: Ui.Dim), -1, 30, 1);
                foreach (string title in headers) EconomicStoreCell(header, title, Ui.Dim);
                foreach (var report in reports.OrderByDescending(r => r.IncomeCents + r.ExpenseCents))
                {
                    var row = Ui.Row(parent, 38);
                    Ui.Size(Ui.Button(row, report.Name + (report.Managed ? " · 托管" : ""), () => OpenAssetSources(new[] { report }, from, to), 16),
                        -1, 34, 1);
                    long other = report.ExpenseCents - report.ProcurementCents - report.WageCents - report.RentCents;
                    var net = Money.FromCents(report.IncomeCents - report.ExpenseCents);
                    EconomicStoreCell(row, Money.FromCents(report.IncomeCents).ToString(), Ui.Good);
                    EconomicStoreCell(row, Money.FromCents(report.ExpenseCents).ToString(), AssetTrendGraphic.ExpenseColor);
                    foreach (long cents in new[] { report.ProcurementCents, report.WageCents, report.RentCents, other })
                        EconomicStoreCell(row, Money.FromCents(cents).ToString(), Ui.TextColor);
                    EconomicStoreCell(row, SignedReportCash(net), net.IsNegative ? Ui.Warn : Ui.Good);
                }
            }
            Para(parent, "总账含经营、生活、借还款和资产交易。托管店的进货列为商品与食材成本，门店收支差额按本期统计。", 15, Ui.Dim);
        }

        private static void EconomicStoreCell(Transform row, string text, Color color) =>
            Ui.Size(Ui.Label(row, text, 16, TextAnchor.MiddleRight, color), 118, 30);

        private void OpenEconomicCashSources(CashPeriodSummary cash, GameDate from, GameDate to)
        {
            OpenWindow("总账分类明细", 780, 680, w =>
            {
                Line(w.Content, $"{from} — {to}", 18, Ui.Dim);
                foreach (bool income in new[] { true, false })
                {
                    var sources = income ? cash.IncomeSources : cash.ExpenseSources;
                    Line(w.Content, (income ? "总收入 " + cash.Income : "总支出 " + cash.Expense),
                        21, income ? Ui.Good : AssetTrendGraphic.ExpenseColor);
                    if (sources.Count == 0) Line(w.Content, "本期暂无记录。", 17, Ui.Dim);
                    foreach (var source in sources.OrderByDescending(s => s.Value))
                    {
                        var row = Ui.Row(w.Content, 30);
                        Ui.Size(Ui.Label(row, source.Key, 17), -1, 30, 1);
                        Ui.Size(Ui.Label(row, Money.FromCents(source.Value).ToString(), 17, TextAnchor.MiddleRight), 160, 30);
                    }
                }
            });
        }

        private static string SignedReportCash(Money amount) => (amount.IsPositive ? "+" : "") + amount;

        private void BuildEconomicPriceSummary(Transform parent, int start, int end, bool ongoing, int selected, Action<int> select)
        {
            var records = S.PriceDays;
            var days = records.Where(day => day.Day >= start && day.Day <= end).ToArray();
            if (days.Length == 0)
            {
                Line(parent, "本期暂无价格记录。", 18, Ui.Dim);
                return;
            }

            // Compare against the previous close so a price change on the first day counts in this period.
            var opening = records.LastOrDefault(day => day.Day < start);
            if (opening != null) days = new[] { opening }.Concat(days).ToArray();
            var baseline = days[0].Prices;
            var closing = days[days.Length - 1].Prices;
            double GroupIndex(double[] prices, int group) => prices[group] / baseline[group] * 100;
            double[] values = days.Select(day => selected < 0
                ? Enumerable.Range(0, baseline.Length).Average(group => GroupIndex(day.Prices, group))
                : GroupIndex(day.Prices, selected)).ToArray();
            var changes = baseline.Select((_, group) => GroupIndex(closing, group) - 100).ToArray();
            int biggestRise = Array.IndexOf(changes, changes.Max());
            int biggestFall = Array.IndexOf(changes, changes.Min());
            const double movementThreshold = 0.005;

            var cards = Ui.Row(parent, 84, 8);
            double? change = days.Length > 1 ? values[values.Length - 1] - 100 : (double?)null;
            AddPriceCard(cards, selected < 0 ? "综合物价" : GameSession.PriceGroups[selected], change);
            AddPriceCard(cards, "涨幅最大", changes[biggestRise] >= movementThreshold ? changes[biggestRise] : (double?)null, GameSession.PriceGroups[biggestRise]);
            AddPriceCard(cards, "跌幅最大", changes[biggestFall] <= -movementThreshold ? changes[biggestFall] : (double?)null, GameSession.PriceGroups[biggestFall]);

            var heading = Ui.Row(parent, 34);
            Ui.Size(Ui.Label(heading, $"报价走势 · {new GameDate(days[0].Day)} — {new GameDate(days[days.Length - 1].Day)}", 17), -1, 34, 1);
            if (selected >= 0) Ui.Size(Ui.Button(heading, "返回综合走势", () => select(-1), 16), 140, 32);
            DrawPriceTrend(parent, days, values);
            Para(parent, days.Length == 1 ? "本期只有一份报价，累计两天的记录后可比较涨跌。"
                : "折线以基准日为 100，各品类等权计算综合物价。商品为品类参考均价，房产为挂牌报价指数。", 15, Ui.Dim);

            int propertyStart = GameSession.PropertyPriceGroupStart;
            DrawPriceGroupRows("住宅与商铺", propertyStart, changes.Length);
            DrawPriceGroupRows("电脑配件", 0, PCShopSell.Core.Catalog.PartCatalog.Categories.Length);
            DrawPriceGroupRows("商品与生活", PCShopSell.Core.Catalog.PartCatalog.Categories.Length, propertyStart);

            void DrawPriceGroupRows(string title, int first, int last)
            {
                bool property = first >= propertyStart;
                var header = Ui.Row(parent, 28);
                Ui.Size(Ui.Label(header, title, 18, color: Ui.Accent), -1, 28, 1);
                string unit = property ? "指数" : "报价";
                foreach (string label in new[] { "基准" + unit, (ongoing ? "最新" : "期末") + unit, "本期涨跌" })
                    EconomicStoreCell(header, label, Ui.Dim);
                for (int group = first; group < last; group++)
                {
                    int index = group;
                    string Quote(double[] prices) => property
                        ? (prices[index] / 100).ToString("0.00")
                        : "¥" + (prices[index] / 100).ToString("#,0.##");
                    var row = Ui.Row(parent, 32);
                    Ui.Size(Ui.Button(row, GameSession.PriceGroups[index] + "  ›", () => select(index), 16,
                        Ui.SelectedButton(selected == index)), -1, 30, 1).name = "EconomicPriceGroup_" + index;
                    EconomicStoreCell(row, Quote(baseline), Ui.TextColor);
                    EconomicStoreCell(row, Quote(closing), Ui.TextColor);
                    EconomicStoreCell(row, days.Length == 1 ? "—" : PriceMovement(changes[index]), PriceMovementColor(changes[index]));
                }
            }
        }

        private void AddPriceCard(Transform row, string title, double? change, string groupName = null)
        {
            var card = Ui.Size(Ui.Panel(row, "PriceSummary", Ui.PanelBg2), 0, 84, 1).rectTransform;
            Ui.VList(card, 1, 8);
            Ui.Size(Line(card, title, 15, Ui.Dim), -1, 20);
            Ui.Size(Line(card, change.HasValue ? PriceMovement(change.Value) : "—", 23,
                change.HasValue ? PriceMovementColor(change.Value) : Ui.Dim), -1, 26);
            if (change.HasValue && groupName != null) Ui.Size(Line(card, groupName, 15), -1, 20);
        }

        private static string PriceMovement(double change) => Math.Abs(change) < 0.005 ? "持平" : change.ToString("+0.00;-0.00") + "%";
        private static Color PriceMovementColor(double change) => change >= 0.005 ? Ui.Warn : change <= -0.005 ? Ui.Good : Ui.Dim;

        private void DrawPriceTrend(Transform parent, PriceDay[] days, double[] values)
        {
            const float width = 1110, height = 158;
            var chart = Ui.Panel(parent, "PriceTrend", Ui.PanelBg2);
            Ui.Size(chart, -1, height + 36);
            var area = Ui.Rect(chart.transform, "Plot");
            area.anchorMin = area.anchorMax = new Vector2(0, 0);
            area.pivot = Vector2.zero; area.anchoredPosition = new Vector2(56, 26); area.sizeDelta = new Vector2(width, height);
            double min = Math.Min(100, values.Min()), max = Math.Max(100, values.Max());
            double pad = Math.Max(1, (max - min) * 0.15);
            min -= pad; max += pad;
            for (int j = 0; j <= 2; j++)
            {
                float y = j * height / 2;
                var grid = Ui.Panel(area, "Grid", Ui.Edge);
                grid.rectTransform.Anchor(0, j / 2f, 1, j / 2f, 0, 0, 0, -1);
                var label = Ui.Label(area, (min + (max - min) * j / 2).ToString("0.0"), 12, TextAnchor.MiddleRight, Ui.Dim);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
                label.rectTransform.pivot = new Vector2(1, .5f); label.rectTransform.anchoredPosition = new Vector2(-8, y); label.rectTransform.sizeDelta = new Vector2(44, 18);
            }
            Vector2 previous = Vector2.zero;
            int dateStep = Math.Max(1, Mathf.CeilToInt((values.Length - 1) / 7f));
            for (int i = 0; i < values.Length; i++)
            {
                float x = values.Length == 1 ? width / 2 : i * width / (values.Length - 1);
                var point = new Vector2(x, (float)((values[i] - min) / (max - min) * height));
                if (i > 0)
                {
                    var delta = point - previous;
                    var line = Ui.Panel(area, "Line", Ui.Accent).rectTransform;
                    line.anchorMin = line.anchorMax = Vector2.zero; line.pivot = new Vector2(0, 0.5f);
                    line.anchoredPosition = previous; line.sizeDelta = new Vector2(delta.magnitude, 3);
                    line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
                var dot = Ui.Panel(area, "Point", Ui.Accent).rectTransform;
                dot.anchorMin = dot.anchorMax = Vector2.zero; dot.pivot = new Vector2(0.5f, 0.5f); dot.anchoredPosition = point; dot.sizeDelta = new Vector2(7, 7);
                previous = point;
                if (i != values.Length - 1 && (i % dateStep != 0 || values.Length - 1 - i < dateStep)) continue;
                var date = new GameDate(days[i].Day);
                var text = Ui.Label(area, $"{date.Month}/{date.Day}", 12, TextAnchor.MiddleCenter, Ui.Dim).rectTransform;
                text.anchorMin = text.anchorMax = Vector2.zero; text.pivot = new Vector2(0.5f, 0);
                text.anchoredPosition = new Vector2(x, -24); text.sizeDelta = new Vector2(48, 20);
            }
        }
    }
}
