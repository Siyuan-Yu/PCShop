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
            if (S.EconomicReportPending && S.EconomicReportAutomatic && !S.IsGameOver && !S.IsWorking
                && !S.Racing && !S.CityActivityExclusive && !AnyModalOpen && !_ctrl.Placing && S.Trip == null && S.PendingEvent == null)
                OpenEconomicReport();
        }

        private void OpenEconomicReport()
        {
            S.DismissEconomicReport();
            int period = -1, selected = -1;
            bool showPrices = false;
            var window = OpenWindow("经济简报", 960, 900, w =>
            {
                int today = S.Clock.Today.TotalDays;
                int span = S.EconomicReportPeriodDays;
                int anchor = S.EconomicReportAnchorDay;
                int first = S.PriceDays.FirstOrDefault()?.Day ?? today;
                int earliest = (first - anchor) / span;
                int current = (today - anchor) / span;
                if (period < earliest) period = Math.Max(earliest, current - 1);
                int start = anchor + period * span;
                int end = Math.Min(today, start + span - 1);

                var nav = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(nav, "上一期", () => { period--; w.Rebuild(); }, 17, Ui.ButtonBg, period > earliest), 110);
                string range = $"{new GameDate(start)} — {new GameDate(end)}" + (period == current ? " · 进行中" : "");
                Ui.Size(Ui.Label(nav, range, 18, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(nav, "下一期", () => { period++; w.Rebuild(); }, 17, Ui.ButtonBg, period < current), 110);

                var tabs = Ui.Row(w.Content, 42);
                Ui.Size(Ui.Button(tabs, "收支结算", () => { showPrices = false; w.Rebuild(); }, 19, !showPrices ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                Ui.Size(Ui.Button(tabs, "物价涨跌", () => { showPrices = true; w.Rebuild(); }, 19, showPrices ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);

                if (!showPrices)
                {
                    BuildEconomicCashSummary(w.Content, new GameDate(start), new GameDate(end));
                    return;
                }

                var days = S.PriceDays.Where(p => p.Day >= start && p.Day <= Math.Min(today, start + span)).ToArray();
                if (days.Length == 0) { Line(w.Content, "尚无价格记录。", 18); return; }
                var baseline = days[0].Prices;
                double Value(PriceDay day) => selected < 0
                    ? day.Prices.Select((v, i) => v / baseline[i] * 100).Average()
                    : day.Prices[selected] / baseline[selected] * 100;
                string name = selected < 0 ? "综合物价" : GameSession.PriceGroups[selected];
                double[] values = days.Select(Value).ToArray();
                double change = values[values.Length - 1] - 100;
                Line(w.Content, $"{name} · 本期涨跌 {change:+0.00;-0.00;0.00}%", 21, change > 0 ? Ui.Warn : change < 0 ? Ui.Good : Ui.Accent);
                Line(w.Content, $"报价对比　{new GameDate(days[0].Day)} → {new GameDate(days[days.Length - 1].Day)}", 15, Ui.Dim);
                DrawPriceTrend(w.Content, days, values);
                Para(w.Content, "期初价格为 100，综合走势按品类等权计算。房产按当期报价，已签租约沿用签约价。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "查看综合走势", () => { selected = -1; w.Rebuild(); }, 17, selected < 0 ? Ui.ButtonHot : Ui.ButtonBg), -1, 34);
                var order = Enumerable.Range(GameSession.PropertyPriceGroupStart, 4).Concat(Enumerable.Range(0, GameSession.PropertyPriceGroupStart));
                foreach (int i in order)
                {
                    int group = i;
                    if (i == GameSession.PropertyPriceGroupStart) Line(w.Content, "住宅与商铺", 18, Ui.Accent);
                    else if (i == 0) Line(w.Content, "商品与生活物价", 18, Ui.Accent);
                    double pct = (days[days.Length - 1].Prices[i] / baseline[i] - 1) * 100;
                    string movement = days.Length == 1 ? "开始记录" : Math.Abs(pct) < 0.005 ? "持平" : pct.ToString("+0.00;-0.00") + "%";
                    Ui.Size(Ui.Button(w.Content, GameSession.PriceGroups[i] + "    " + movement + "    查看走势", () =>
                    { selected = group; w.ResetScrollOnRebuild = true; w.Rebuild(); }, 17, selected == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 36);
                }
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

        private void BuildEconomicCashSummary(Transform parent, GameDate from, GameDate to)
        {
            var cash = FinanceReport.CashSummary(S.Ledger, from, to);
            var cards = Ui.Row(parent, 160, 8);
            BuildAssetMoneyCard(cards, "总收入", cash.Income.Cents, Ui.Good, cash.IncomeSources);
            BuildAssetMoneyCard(cards, "总支出", cash.Expense.Cents, new Color32(233, 178, 150, 255), cash.ExpenseSources);
            var balance = Ui.Size(Ui.Panel(cards, "EconomicCashNet", Ui.PanelBg2), -1, 160, 1).rectTransform;
            Ui.VList(balance, 2, 8);
            Line(balance, "净收支", 16, Ui.Dim);
            Line(balance, SignedReportCash(cash.Net), 26, cash.Net.IsNegative ? Ui.Warn : Ui.Good);
            Line(balance, "期初现金 " + cash.OpeningBalance, 16);
            Line(balance, "期末现金 " + cash.ClosingBalance, 16);
            if (cash.InternalNet != Money.Zero)
                Line(parent, "内部调拨净额 " + SignedReportCash(cash.InternalNet), 16, Ui.Dim);
            Para(parent, "汇总所有实际到账和付出的现金，含经营、生活和资产交易。内部调拨单列，净收支与经营利润口径不同。", 15, Ui.Dim);
        }

        private static string SignedReportCash(Money amount) => (amount.IsPositive ? "+" : "") + amount;

        private void DrawPriceTrend(Transform parent, PriceDay[] days, double[] values)
        {
            const float width = 820, height = 200;
            var chart = Ui.Panel(parent, "PriceTrend", new Color(0, 0, 0, 0.22f));
            Ui.Size(chart, width + 40, height + 36);
            var area = Ui.Rect(chart.transform, "Plot");
            area.anchorMin = area.anchorMax = new Vector2(0, 0);
            area.pivot = Vector2.zero; area.anchoredPosition = new Vector2(24, 26); area.sizeDelta = new Vector2(width, height);
            double min = Math.Min(100, values.Min()), max = Math.Max(100, values.Max());
            double pad = Math.Max(1, (max - min) * 0.15);
            min -= pad; max += pad;
            for (int j = 0; j <= 2; j++)
            {
                float y = j * height / 2;
                var grid = Ui.Panel(area, "Grid", new Color(1, 1, 1, 0.13f));
                grid.rectTransform.Anchor(0, j / 2f, 1, j / 2f, 0, 0, 0, -1);
                var label = Ui.Label(area, (min + (max - min) * j / 2).ToString("0.0"), 12, TextAnchor.MiddleLeft, Ui.Dim);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
                label.rectTransform.pivot = new Vector2(0, 0); label.rectTransform.anchoredPosition = new Vector2(0, y); label.rectTransform.sizeDelta = new Vector2(60, 18);
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
