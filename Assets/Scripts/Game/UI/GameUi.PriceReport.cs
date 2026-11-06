using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void CheckWeeklyPriceReport()
        {
            if (S.WeeklyPricesPending && S.WeeklyPricesSubscribed && !S.IsGameOver && !S.IsWorking
                && !AnyModalOpen && !_ctrl.Placing && S.Trip == null && S.PendingEvent == null)
                OpenWeeklyPriceReport();
        }

        private void OpenWeeklyPriceReport()
        {
            S.DismissWeeklyPrices();
            int first = S.PriceDays.Count > 0 ? S.PriceDays[0].Day : S.Clock.Today.TotalDays;
            int last = S.PriceDays.Count > 0 ? S.PriceDays[S.PriceDays.Count - 1].Day : first;
            int maxWeek = Math.Max(0, (last - first - 1) / 7);
            int week = maxWeek, selected = -1;
            OpenWindow("每周物价简报", 960, 900, w =>
            {
                var nav = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(nav, "上一周", () => { week--; w.Rebuild(); }, 17, Ui.ButtonBg, week > 0), 110);
                int start = first + week * 7;
                var days = S.PriceDays.Where(p => p.Day >= start && p.Day <= start + 7).ToArray();
                string range = days.Length == 0 ? "暂无数据" : new GameDate(days[0].Day) + " — " + new GameDate(days[days.Length - 1].Day);
                Ui.Size(Ui.Label(nav, range, 18, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(nav, "下一周", () => { week++; w.Rebuild(); }, 17, Ui.ButtonBg, week < maxWeek), 110);
                Ui.Size(Ui.Button(w.Content, "每周自动简报：" + (S.WeeklyPricesSubscribed ? "开（点击取消订阅）" : "关（点击重新订阅）"), () =>
                { S.ToggleWeeklyPrices(); w.Rebuild(); }, 18, S.WeeklyPricesSubscribed ? Ui.ButtonHot : Ui.ButtonBg), -1, 40);
                Para(w.Content, "每天记录、每7天发布。住宅和商铺的售价、月租分别每周浮动；已签租约保留原价。其他商品沿用真实标价，不含交情折扣。", 15, Ui.Dim);
                if (days.Length == 0) { Line(w.Content, "尚无价格记录。", 18); return; }
                var baseline = days[0].Prices;
                double Value(PriceDay day) => selected < 0
                    ? day.Prices.Select((v, i) => v / baseline[i] * 100).Average()
                    : day.Prices[selected] / baseline[selected] * 100;
                string name = selected < 0 ? "综合物价" : GameSession.PriceGroups[selected];
                double[] values = days.Select(Value).ToArray();
                double change = values[values.Length - 1] - 100;
                Line(w.Content, $"{name} · 本期涨跌 {change:+0.00;-0.00;0.00}%" + (days.Length < 8 ? "（记录未满一周）" : ""), 21, change > 0 ? Ui.Warn : change < 0 ? Ui.Good : Ui.Accent);
                DrawPriceTrend(w.Content, days, values);
                Para(w.Content, "折线以期初价格为100；综合走势为各品类涨跌幅的等权平均。点击下方品类查看单独走势。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "查看综合走势", () => { selected = -1; w.Rebuild(); }, 17, selected < 0 ? Ui.ButtonHot : Ui.ButtonBg), -1, 34);
                var list = Ui.Scroll(w.Content, 260);
                var order = Enumerable.Range(GameSession.PropertyPriceGroupStart, 4).Concat(Enumerable.Range(0, GameSession.PropertyPriceGroupStart));
                foreach (int i in order)
                {
                    int group = i;
                    if (i == GameSession.PropertyPriceGroupStart) Line(list, "住宅与商铺", 18, Ui.Accent);
                    else if (i == 0) Line(list, "商品与生活物价", 18, Ui.Accent);
                    double pct = (days[days.Length - 1].Prices[i] / baseline[i] - 1) * 100;
                    string movement = days.Length == 1 ? "开始记录" : Math.Abs(pct) < 0.005 ? "持平" : pct.ToString("+0.00;-0.00") + "%";
                    Ui.Size(Ui.Button(list, GameSession.PriceGroups[i] + "    " + movement + "    查看走势", () =>
                    { selected = group; w.Rebuild(); }, 17, selected == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 36);
                }
                Line(w.Content, "关掉订阅后仍会记录价格；可随时从“行情 → 每周物价简报”查看或重新订阅。", 14, Ui.Dim);
            });
        }

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
                var date = new GameDate(days[i].Day);
                var text = Ui.Label(area, $"{date.Month}/{date.Day}", 12, TextAnchor.MiddleCenter, Ui.Dim).rectTransform;
                text.anchorMin = text.anchorMax = Vector2.zero; text.pivot = new Vector2(0.5f, 0);
                text.anchoredPosition = new Vector2(x, -24); text.sizeDelta = new Vector2(48, 20);
                previous = point;
            }
        }
    }
}
