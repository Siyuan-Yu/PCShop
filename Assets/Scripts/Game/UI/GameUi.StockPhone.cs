using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private enum StockPage { Charts, Exchange, News }
        private static readonly string[] StockIds = CityInvestments.All.Select(d => d.Id).Append("hardware").ToArray();
        private static readonly Color[] StockChartColors =
        {
            new Color32(241,198,114,255), new Color32(140,208,198,255), new Color32(201,164,242,255),
            new Color32(243,156,156,255), new Color32(136,181,236,255), new Color32(214,223,142,255),
            new Color32(239,180,135,255), new Color32(149,216,151,255), new Color32(215,165,203,255),
            new Color32(142,216,239,255), new Color32(194,203,209,255)
        };
        private const int StockQuantityLimit = 1000000;
        private const int StockColumnWidth = 124;
        private GameSession _stockSession;
        private Window _stockWindow;
        private StockPage _stockPage = StockPage.Exchange;
        private string _stockExpanded, _stockChartTarget = "city_chip", _stockTradeFeedback;
        private bool _stockShortMode, _stockCandles, _stockWatchOnly, _stockHelp, _stockTradeFailed;
        private int _stockSort, _stockChartDays = 1;
        private int _stockOrderQuantity = GameSession.StockLotSize, _indexOrderQuantity = 1;

        public void OpenCityInvestments()
        {
            string blocker = S.AppBlocker(PhoneApps.Securities);
            if (blocker != null) { Toast(blocker); if (S.HasSmartphone) OpenPhoneStore(); return; }
            if (_stockSession != S)
            {
                _stockSession = S;
                _stockPage = StockPage.Exchange;
                _stockExpanded = null;
                _stockChartTarget = "city_chip";
                _stockShortMode = _stockCandles = _stockWatchOnly = _stockHelp = false;
                _stockSort = 0;
                _stockChartDays = _indexOrderQuantity = 1;
                _stockOrderQuantity = GameSession.StockLotSize;
            }
            ClosePhone();
            if (_stockWindow != null && !_stockWindow.Closed)
            {
                _stockWindow.Root.SetAsLastSibling();
                _stockWindow.Rebuild();
                return;
            }
            _stockTradeFeedback = null;
            _stockWindow = OpenWindow("证券", 1480, 850, BuildStockWorkspace, scrollContent: false);
        }

        private static string StockTitle(string id) => id == "hardware" ? "硬件景气指数" : CityInvestments.Get(id)?.Name ?? id;
        private static Text StockText(Transform parent, string text, int font = 16, Color? color = null) => Para(parent, text, font, color);
        private static RectTransform StockCard(Transform parent, string title = null)
        {
            var card = Ui.Panel(parent, "StockCard", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 6, 10);
            if (title != null) StockText(card, title, 18, Ui.Accent);
            return card;
        }
        private static void StockFlex(Component component, float height)
        {
            var size = Ui.Size(component, 0, height, 1).GetComponent<LayoutElement>();
            size.minWidth = 0;
        }
        private static RectTransform StockScroll(Transform parent, string name)
        {
            var content = PhoneFlexibleScroll(parent, name);
            content.parent.parent.GetComponent<Image>().color = Color.clear;
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            return content;
        }
        private Money StockUnrealized => S.CityEquityValue - S.CityEquityCost + S.FundValue
            - Money.FromCents(S.CityFundCostCents) + S.StockShortUnrealized;

        private void BuildStockWorkspace(Window w)
        {
            var summary = Ui.Row(w.Content, 48, 12);
            StockFlex(Ui.Label(summary, "可用现金  " + S.Ledger.Balance, 19, color: Ui.Accent), 48);
            var floating = StockUnrealized;
            StockFlex(Ui.Label(summary, "总浮动盈亏  " + floating, 18, color: floating.IsNegative ? Ui.Warn : Ui.Good), 48);
            var realized = S.StockTotalRealized;
            StockFlex(Ui.Label(summary, "已实现盈亏  " + realized, 18, color: realized.IsNegative ? Ui.Warn : Ui.Good), 48);
            StockFlex(Ui.Label(summary, S.StockTradingStatus + "\n下次报价 " + S.StockNextQuote, 15, color: Ui.Dim), 48);
            Ui.Size(Ui.Button(summary, "返回手机", () => { w.Close(); OpenPhone(); }, 16), 124, 36);

            switch (_stockPage)
            {
                case StockPage.Charts: BuildStockCharts(w); break;
                case StockPage.News: BuildStockNews(w, StockScroll(w.Content, "StockNewsScroll")); break;
                default: BuildStockExchange(w); break;
            }

            if (!string.IsNullOrEmpty(_stockTradeFeedback))
            {
                if (_stockTradeFailed) Ui.Notice(w.Content, _stockTradeFeedback).name = "StockTradeFeedback";
                else StockText(StockCard(w.Content), _stockTradeFeedback, 17, Ui.Good).name = "StockTradeFeedback";
            }
            else if (_stockPage == StockPage.Exchange)
                Ui.Size(Ui.Label(w.Content, "手续费 · 股票 0.2% · 指数 0.5%", 14, color: Ui.Dim), -1, 24);

            var navigation = Ui.Row(w.Content, 40, 6);
            string[] names = { "走势图", "交易所", "新闻" };
            for (int i = 0; i < names.Length; i++)
            {
                var page = (StockPage)i;
                StockFlex(Ui.Button(navigation, names[i], () =>
                {
                    if (_stockPage == page) return;
                    _stockPage = page;
                    _stockTradeFeedback = null;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 18, page == _stockPage ? Ui.ButtonHot : Ui.ButtonBg), 40);
            }
        }

        private void BuildStockExchange(Window w)
        {
            var modes = Ui.Row(w.Content, 36, 6);
            foreach (bool shortMode in new[] { false, true })
                StockFlex(Ui.Button(modes, shortMode ? "做空" : "现货交易", () =>
                {
                    if (_stockShortMode == shortMode) return;
                    _stockShortMode = shortMode;
                    _stockTradeFeedback = null;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 17, shortMode == _stockShortMode ? Ui.ButtonHot : Ui.ButtonBg), 36);

            var controls = Ui.Row(w.Content, 36, 6);
            BuildStockQuantity(w, controls, index: false);
            foreach (int lots in new[] { 1, 5, 10 })
            {
                int count = lots * GameSession.StockLotSize;
                Ui.Size(Ui.Button(controls, lots + " 手", () => { _stockOrderQuantity = count; w.Rebuild(); }, 15,
                    count == _stockOrderQuantity ? Ui.ButtonHot : Ui.ButtonBg), 61, 34);
            }
            if (!_stockShortMode) BuildStockQuantity(w, controls, index: true);
            StockFlex(Ui.Rect(controls, "StockControlsSpace"), 36);
            Ui.Size(Ui.Button(controls, _stockWatchOnly ? "只看自选 ✓" : "只看自选", () =>
            {
                _stockWatchOnly = !_stockWatchOnly;
                w.ResetScrollOnRebuild = true;
                w.Rebuild();
            }, 15, _stockWatchOnly ? Ui.ButtonHot : Ui.ButtonBg), 110, 34);
            Ui.Size(Ui.Button(controls, _stockHelp ? "收起规则" : "交易规则", () =>
            { _stockHelp = !_stockHelp; w.Rebuild(); }, 15), 100, 34);
            if (_stockHelp)
            {
                StockText(w.Content, "股票每手100股，指数按份。周一至周五全天可交易，每次报价有成交额度。做空冻结自有保证金及卖出所得，每日借股费0.03%，担保比例≤130%时自动平仓。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "账户与费用明细", OpenStockFees, 15), -1, 30);
            }

            var heading = Ui.Row(w.Content, 29, 6);
            heading.GetComponent<HorizontalLayoutGroup>().padding.right = 20; // Match the scrollbar gutter.
            StockCell(heading, "", 29, 40);
            StockFlex(Ui.Label(heading, "公司", 15, color: Ui.Dim), 29);
            Ui.Size(Ui.Button(heading, "现价", () => { _stockSort = _stockSort == 1 ? 0 : 1; w.Rebuild(); }, 15), StockColumnWidth, 29);
            if (_stockShortMode)
            {
                StockCell(heading, "开仓均价", 29);
                StockCell(heading, "借股／持股", 29);
                StockCell(heading, "冻结自有保证金", 29);
            }
            else
            {
                Ui.Size(Ui.Button(heading, "涨跌", () => { _stockSort = _stockSort == 2 ? 0 : 2; w.Rebuild(); }, 15), StockColumnWidth, 29);
                StockCell(heading, "我的持仓", 29);
                StockCell(heading, "成本价", 29, color: Ui.Accent);
            }
            StockCell(heading, _stockShortMode ? "借入做空" : "买入", 29, 94);
            StockCell(heading, _stockShortMode ? "买回归还" : "卖出", 29, 94);

            var list = StockScroll(w.Content, "StockQuoteScroll");
            var ids = StockIds.Where(id => (!_stockWatchOnly || S.StockWatched(id)) && (!_stockShortMode || id != "hardware"));
            if (_stockSort == 1) ids = ids.OrderByDescending(id => S.CityInvestmentPrice(id).Cents);
            if (_stockSort == 2 && !_stockShortMode) ids = ids.OrderByDescending(S.StockDayChange);
            bool any = false;
            foreach (string id in ids)
            {
                BuildStockRow(w, list, id);
                any = true;
            }
            if (!any) StockText(list, "暂无自选股票。", 16, Ui.Dim);
        }

        private void BuildStockQuantity(Window w, Transform parent, bool index)
        {
            int step = index ? 1 : GameSession.StockLotSize;
            int Quantity() => index ? _indexOrderQuantity : _stockOrderQuantity;
            void SetQuantity(int value)
            {
                if (index) _indexOrderQuantity = value;
                else _stockOrderQuantity = value;
            }
            void StepQuantity(int direction)
            {
                int stepped = (Quantity() / step + direction) * step;
                SetQuantity(Mathf.Clamp(stepped, step, StockQuantityLimit));
                w.Rebuild();
            }
            Ui.Size(Ui.Label(parent, index ? "指数" : "每次交易", 15), index ? 38 : 74, 36);
            Ui.Size(Ui.Button(parent, "−", () => StepQuantity(-1), 16), 34, 34);
            var input = Ui.TextInput(parent, Quantity().ToString(), "", text =>
            {
                SetQuantity(int.TryParse(text, out int value) ? value : 0);
                w.RequestRebuild();
            });
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 7;
            Ui.Size(input, 88, 34);
            Ui.Size(Ui.Button(parent, "+", () => StepQuantity(1), 16), 34, 34);
            Ui.Size(Ui.Label(parent, index ? "份" : "股", 15), 24, 36);
        }

        private static void StockCell(Transform row, string text, int height, int width = StockColumnWidth, Color? color = null) =>
            Ui.Size(Ui.Label(row, text, 16, TextAnchor.MiddleRight, color), width, height);

        private void BuildStockRow(Window w, Transform list, string id)
        {
            bool expanded = _stockExpanded == id;
            bool shortMode = _stockShortMode;
            var stock = S.StockState(id);
            long held = S.CityInvestmentUnits(id);
            long units = shortMode ? stock.ShortUnits : held;
            long basis = shortMode ? stock.ShortBasisCents : S.CityInvestmentCost(id).Cents;
            double? cost = units > 0 ? basis / (double)units : (double?)null;
            var row = Ui.Row(list, 43, 6);
            row.name = "StockQuote_" + id;
            Ui.Size(Ui.Button(row, S.StockWatched(id) ? "★" : "☆", () => { S.ToggleStockWatch(id); w.Rebuild(); }, 17), 40, 36);
            var name = Ui.Button(row, (expanded ? "▾  " : "▸  ") + StockTitle(id), () =>
            {
                _stockExpanded = expanded ? null : id;
                _stockCandles = false;
                w.Rebuild();
                if (!expanded) RevealStock(w, id);
            }, 17, expanded ? Ui.ButtonHot : Ui.ButtonBg);
            name.name = "StockExpand_" + id;
            StockFlex(name, 39);
            name.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            StockCell(row, S.CityInvestmentPrice(id).ToString(), 39);
            string costText = cost.HasValue ? "¥" + (cost.Value / 100).ToString("N2") : "—";
            if (shortMode)
            {
                StockCell(row, costText, 39, color: cost.HasValue ? Ui.Accent : Ui.Dim);
                StockCell(row, units.ToString("N0") + "／" + held.ToString("N0"), 39);
                StockCell(row, Money.FromCents(stock.ShortMarginCents).ToString(), 39);
            }
            else
            {
                double change = S.StockDayChange(id);
                StockCell(row, change.ToString("+0.00%;-0.00%;0.00%"), 39, color: change < 0 ? Ui.Warn : Ui.Good);
                StockCell(row, held.ToString("N0"), 39);
                StockCell(row, costText, 39, color: cost.HasValue ? Ui.Accent : Ui.Dim);
            }
            int Quantity() => id == "hardware" ? _indexOrderQuantity : _stockOrderQuantity;
            Ui.Size(Ui.Button(row, shortMode ? "借入" : "买入", () => TradeStockQuote(w, id, Quantity(), shortMode ? 2 : 0),
                16, Ui.ButtonHot), 94, 36).name = (shortMode ? "StockQuickShort_" : "StockQuickBuy_") + id;
            Ui.Size(Ui.Button(row, shortMode ? "归还" : "卖出", () => TradeStockQuote(w, id, Quantity(), shortMode ? 3 : 1),
                16), 94, 36).name = (shortMode ? "StockQuickCover_" : "StockQuickSell_") + id;
            if (expanded) BuildStockDetails(w, list, id, units, basis, cost, costText);
        }

        private void BuildStockDetails(Window w, Transform parent, string id, long units, long basis, double? cost, string costText)
        {
            var card = StockCard(parent);
            card.name = "StockDetails_" + id;
            var heading = Ui.Row(card, 34, 8);
            StockFlex(Ui.Label(heading, "近7天走势 · " + StockTitle(id), 18, color: Ui.Accent), 34);
            Ui.Size(Ui.Button(heading, _stockCandles ? "切换折线" : "切换K线", () =>
            { _stockCandles = !_stockCandles; w.Rebuild(); }, 15), 110, 32);

            var figures = Ui.Row(card, 34, 8);
            long value = S.CityInvestmentPrice(id).Cents * units;
            var profit = Money.FromCents(_stockShortMode ? basis - value : value - basis);
            StockFlex(Ui.Label(figures, (_stockShortMode ? "开仓均价  " : "平均持仓成本  ") + costText, 17,
                color: StockPriceGraphic.CostColor), 34);
            StockFlex(Ui.Label(figures, (_stockShortMode ? "借股金额  " : "持仓总成本  ") + (cost.HasValue ? Money.FromCents(basis).ToString() : "—"), 17), 34);
            StockFlex(Ui.Label(figures, "浮动盈亏  " + (cost.HasValue ? profit.ToString() : "—"), 17,
                color: profit.IsNegative ? Ui.Warn : Ui.Good), 34);

            var quotes = StockQuotesInRange(id, 7);
            var graph = Ui.Size(Ui.Rect(card, "StockPriceLine"), -1, 218);
            graph.gameObject.AddComponent<StockPriceGraphic>().Initialize(quotes, cost, _stockCandles);
            if (quotes.Length < 2) StockText(card, "近7天报价不足，后续随交易日更新。", 14, Ui.Dim);

            var stock = S.StockState(id);
            if (stock != null)
            {
                var disclosure = stock.YesterdayRevenueCents > 0
                    ? $"昨日营收 {Money.FromCents(stock.YesterdayRevenueCents)} · 利润 {Money.FromCents(stock.YesterdayProfitCents)}"
                    : "经营数据待披露";
                StockText(card, disclosure + " · 最近每股分红 " + Money.FromCents(stock.LastDividendCents), 14, Ui.Dim);
            }
            if (_stockShortMode && units > 0)
            {
                var margin = Ui.Row(card, 34, 8);
                double ratio = S.StockMarginRatio(id);
                StockFlex(Ui.Label(margin, $"担保比例 {ratio:P0} · 冻结总额 {Money.FromCents(stock.ShortCollateralCents)}",
                    16, color: ratio < 1.5 ? Ui.Warn : Ui.Dim), 34);
                Ui.Size(Ui.Button(margin, "补充保证金", () => OpenStockMargin(w, id), 15), 126, 32);
            }
            if (id != "hardware" && units % GameSession.StockLotSize != 0)
            {
                int action = _stockShortMode ? 3 : 1;
                Ui.Size(Ui.Button(card, (_stockShortMode ? "全部归还" : "全部卖出") + $" · {units:N0}股",
                    () => TradeStockQuote(w, id, units, action), 15), -1, 32);
            }
        }

        private static void RevealStock(Window w, string id)
        {
            Canvas.ForceUpdateCanvases();
            var scroll = w.Root.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "StockQuoteScroll");
            var row = scroll?.content.Find("StockQuote_" + id) as RectTransform;
            var details = scroll?.content.Find("StockDetails_" + id) as RectTransform;
            if (row == null || details == null) return;
            var rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, row);
            var detailBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, details);
            float offset = Mathf.Max(0, rowBounds.max.y - scroll.viewport.rect.yMax);
            if (offset == 0 && detailBounds.min.y < scroll.viewport.rect.yMin)
                offset = Mathf.Max(rowBounds.max.y - scroll.viewport.rect.yMax, detailBounds.min.y - scroll.viewport.rect.yMin);
            float max = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y - offset, 0, max);
            scroll.StopMovement();
            scroll.content.anchoredPosition = position;
        }

        private StockQuote[] StockQuotesInRange(string id, int days)
        {
            long from = Math.Max(0, S.Clock.Today.TotalDays - days + 1) * 1440L;
            return S.StockQuotes(id).Where(q => q.Minute >= from).ToArray();
        }

        private void BuildStockCharts(Window w)
        {
            var body = StockScroll(w.Content, "StockChartsScroll");
            var ranges = Ui.Row(body, 36, 6);
            foreach (int days in new[] { 1, 7, 30 })
                Ui.Size(Ui.Button(ranges, days == 1 ? "今日" : $"近{days}天", () => { _stockChartDays = days; w.Rebuild(); },
                    16, days == _stockChartDays ? Ui.ButtonHot : Ui.ButtonBg), 112, 34);
            var chartRow = Ui.Row(body, 430, 12);
            var legend = Ui.Rect(chartRow, "StockChartLegend");
            Ui.Size(legend, 235, 430);
            Ui.VList(legend, 3, 0);
            for (int i = 0; i < StockIds.Length; i++)
            {
                string id = StockIds[i];
                var item = Ui.Row(legend, 34, 5);
                var tint = Ui.Panel(item, "SeriesColor", StockChartColors[i % StockChartColors.Length]);
                tint.raycastTarget = false;
                Ui.Size(tint, 5, 24);
                StockFlex(Ui.Button(item, $"{StockTitle(id)}   {S.CityInvestmentPrice(id)}",
                    () => { _stockChartTarget = id; w.Rebuild(); }, 15,
                    id == _stockChartTarget ? Ui.ButtonHot : Ui.ButtonBg), 33);
            }
            var graph = Ui.Rect(chartRow, "StockMarketChart");
            StockFlex(graph, 430);
            int selected = Array.IndexOf(StockIds, _stockChartTarget);
            var indices = Enumerable.Range(0, StockIds.Length).OrderBy(i => i == selected).ToArray();
            var series = indices.Select(i => StockQuotesInRange(StockIds[i], _stockChartDays)).ToArray();
            var colors = indices.Select(i =>
            {
                var tint = StockChartColors[i % StockChartColors.Length];
                if (i != selected) tint.a = .52f;
                return tint;
            }).ToArray();
            graph.gameObject.AddComponent<StockPriceGraphic>().Initialize(series, colors);
            if (series.All(quotes => quotes.Length == 0)) StockText(body, "该时段暂无报价。", 15, Ui.Dim);
        }

        private void BuildStockNews(Window w, Transform body)
        {
            var news = S.StockNewsItems.Reverse().Take(30).ToArray();
            if (news.Length == 0) StockText(body, "暂无已发布消息，经营披露后更新。", 15, Ui.Dim);
            foreach (var item in news)
            {
                var card = StockCard(body, item.Headline);
                var date = new GameDate((int)(item.Minute / 1440));
                StockText(card, date + " " + GameSession.ClockText((int)(item.Minute % 1440)), 13, Ui.Dim);
                StockText(card, item.Detail, 15);
                Ui.Size(Ui.Button(card, "查看 " + StockTitle(item.StockId), () =>
                {
                    _stockPage = StockPage.Exchange;
                    _stockShortMode = _stockWatchOnly = _stockCandles = false;
                    _stockExpanded = item.StockId;
                    _stockTradeFeedback = null;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                    RevealStock(w, item.StockId);
                }, 15), -1, 32);
            }
        }
    }
}
