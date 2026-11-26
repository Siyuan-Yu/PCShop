using System;
using System.Linq;
using PCShopSell.Core.Catalog;
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
        private void CheckEconomicReport()
        {
            if (S.IsGameOver || S.IsWorking || S.Racing || S.CityActivityExclusive || AnyModalOpen || PopupOpen
                || S.OpeningAgencyGuidePending || OpeningMealRestricted || _ctrl.Placing || _ctrl.PetFocusActive
                || S.Trip != null || S.PendingEvent != null)
                return;
            S.FilterPendingMarketPriceAlerts();
            if (S.MarketPriceAlerts.Count > 0) OpenMarketPriceAlert();
            else if (S.EconomicReportPending && S.EconomicReportAutomatic) OpenEconomicReport();
        }

        private void OpenMarketPriceSubscriptions()
        {
            OpenWindow("订阅物价信息", 1060, 540, w =>
            {
                string threshold = GameSession.MarketPriceAlertThreshold.ToString("P0");
                Line(w.Content, $"勾选品类后，单日涨跌超过 {threshold} 时弹出提醒。", 17, Ui.Dim);
                int partsEnd = PartCatalog.Categories.Length;
                int propertyStart = GameSession.PropertyPriceGroupStart;
                int modelStart = GameSession.PriceGroups.Length;
                var upper = Ui.Row(w.Content, 210, 10);
                var lower = Ui.Row(w.Content, 134, 10);
                BuildGroup(upper, "电脑配件", 210, 0, partsEnd);
                BuildGroup(upper, "商品与生活", 210, partsEnd, propertyStart);
                BuildGroup(lower, "房产与租金", 134, propertyStart, modelStart);
                BuildGroup(lower, "模型系列", 134, modelStart, GameSession.MarketPriceCategories.Count);
                if (!S.ModelPriceAlertsAvailable)
                    Line(w.Content, "模型系列的订阅将在模型店完成装修后生效。", 16, Ui.Dim);

                void BuildGroup(Transform parent, string title, float height, int start, int end)
                {
                    var group = ReportPanel(parent, "PriceSubscriptionGroup", height);
                    Line(group, title, 18, Ui.Accent);
                    for (int i = start; i < end; i += 2)
                    {
                        var row = Ui.Row(group, 34, 6);
                        for (int j = i; j < Math.Min(i + 2, end); j++)
                        {
                            var category = GameSession.MarketPriceCategories[j];
                            string name = j >= modelStart ? ModelProducts.SeriesName(category.Id) : category.Name;
                            bool selected = S.IsMarketPriceSubscribed(category.Id);
                            Ui.Size(Ui.Button(row, (selected ? "✓  " : "□  ") + name, () =>
                            {
                                S.SetMarketPriceSubscription(category.Id, !selected);
                                w.Rebuild();
                            }, 16, Ui.SelectedButton(selected)), 0, 34, 1).name = "MarketSubscribe_" + category.Id;
                        }
                        if (i + 1 == end) Ui.Size(Ui.Rect(row, "PriceSubscriptionSpacer"), 0, 34, 1);
                    }
                }
            }, scrollContent: false);
        }

        private void OpenMarketPriceAlert()
        {
            var moves = S.MarketPriceAlerts;
            var day = new GameDate(S.MarketPriceAlertDay);
            var news = IndustryCycles.CommodityEventOn(day.TotalDays, S.Market.CommoditySeed);
            bool showNews = news.IsVolatile(day.TotalDays) && moves.Any(move => move.CategoryId == news.Key);
            S.DismissMarketPriceAlert();
            OpenWindow("物价异动", 640, Mathf.Min(680, 228 + moves.Count * 34 + (showNews ? 52 : 0)), w =>
            {
                Line(w.Content, $"{day} · 较前一日", 18, Ui.Dim);
                if (showNews) Para(w.Content, $"{news.Name} · {news.Headline}。", 18, Ui.Accent);
                foreach (var move in moves)
                {
                    var row = Ui.Row(w.Content, 34, 10);
                    int seriesIndex = Array.IndexOf(ModelProducts.SeriesIds, move.CategoryId);
                    if (seriesIndex >= 0)
                        Ui.Size(Ui.Button(row, move.Name + "  ›", () =>
                        {
                            w.Close();
                            OpenMarketView(0, ModelPriceGroupStart + seriesIndex, day.TotalDays);
                        }, 16), 0, 34, 1);
                    else Ui.Size(Ui.Label(row, move.Name, 18), -1, -1, 1);
                    var label = Ui.Label(row, PriceMovement(move.Change * 100),
                        19, TextAnchor.MiddleRight, Ui.ChangeColor(move.Change));
                    label.fontStyle = FontStyle.Bold;
                    Ui.Size(label, 154, -1);
                }
                var actions = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Button(actions, "知道了", w.Close, 17, Ui.ButtonSecondary), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "订阅物价信息", () => { w.Close(); OpenMarketPriceSubscriptions(); }, 16), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "查看物价", () => { w.Close(); OpenMarketPanel(); }, 17, Ui.ButtonHot), -1, 42, 1);
            });
        }

        private void OpenEconomicReport()
        {
            S.DismissEconomicReport();
            int period = -1, selectedDay = -1;
            var prices = new ReportPriceViewState();
            bool showPrices = false;
            var window = OpenWindow("经济简报", 1260, 920, w =>
            {
                var (start, end, ongoing) = BuildEconomicPeriodNavigation(w, period, false, next => period = next);

                var tabs = Ui.Row(w.Content, 42);
                Ui.Tab(tabs, "收支结算", () => { showPrices = false; w.Rebuild(); }, !showPrices);
                Ui.Tab(tabs, "物价变化", () => { showPrices = true; w.Rebuild(); }, showPrices);

                if (showPrices) BuildPriceDashboard(w.Content, start, end, prices, w.Rebuild, false);
                else BuildCashDashboard(w.Content, new GameDate(start), new GameDate(end), selectedDay,
                    day => { selectedDay = day; w.Rebuild(); });
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

    }
}
