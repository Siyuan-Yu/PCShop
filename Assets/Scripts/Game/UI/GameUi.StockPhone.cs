using System;
using System.Collections.Generic;
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
        private GameSession _stockSession;
        private Window _stockWindow;
        private bool _stockWatchOnly, _stockShowFees;
        private int _stockSort;
        private const int StockQuantityLimit = 1000000;
        private const int StockQuoteQuantityWidth = 114;
        private readonly Dictionary<string, int> _stockQuoteQuantities = new Dictionary<string, int>();
        private int _stockOrderQuantity = GameSession.StockLotSize;
        private int _indexOrderQuantity = 10;
        private StockOrderDraft _stockDraft;
        private string _stockTradeFeedback;
        private bool _stockTradeFailed;
        private sealed class StockOrderDraft
        {
            public string Id;
            public int Action;
            public long Quantity, PriceCents;
            public Money ExtraMargin;
        }

        public void OpenCityInvestments()
        {
            string blocker = S.AppBlocker(PhoneApps.Securities);
            if (blocker != null) { Toast(blocker); if (S.HasSmartphone) OpenPhoneStore(); return; }
            if (_stockSession != S)
            {
                _stockSession = S; _stockTab = 0; _stockTarget = "city_chip";
                _stockDraft = null; _stockShowFees = false; _stockWatchOnly = false; _stockSort = 0;
                _stockQuoteQuantities.Clear();
            }
            ClosePhone();
            if (_stockWindow != null && !_stockWindow.Closed)
            { _stockWindow.Root.SetAsLastSibling(); _stockWindow.Rebuild(); return; }
            _stockDraft = null;
            _stockTradeFeedback = null;
            _stockWindow = OpenWindow("证券", 1480, 850, BuildStockWorkspace,
                onClosed: () => _stockDraft = null, scrollContent: false);
        }

        private static string StockTitle(string id) => id == "hardware" ? "硬件景气指数" : CityInvestments.Get(id)?.Name ?? id;
        private static Text StockText(Transform parent, string text, int font = 16, Color? color = null) => Para(parent, text, font, color);
        private static RectTransform StockCard(Transform parent, string title = null)
        {
            var card = Ui.Panel(parent, "StockCard", Ui.PanelBg2).rectTransform; Ui.VList(card, 6, 10);
            if (title != null) StockText(card, title, 18, Ui.Accent);
            return card;
        }
        private static void StockFlex(Component component, float height)
        {
            var size = Ui.Size(component, 0, height, 1).GetComponent<LayoutElement>(); size.minWidth = 0;
        }
        private static RectTransform StockPane(Transform parent, string name, float left, float right)
        {
            var panel = Ui.Panel(parent, name, new Color(.12f, .16f, .20f)).rectTransform;
            panel.Anchor(left, 0, right, 1, left == 0 ? 0 : 5, 0, right == 1 ? 0 : 5, 0);
            Ui.VList(panel, 7, 10); return panel;
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
            if (string.IsNullOrEmpty(_stockTarget)) _stockTarget = "city_chip";
            var navigation = Ui.Row(w.Content, 38, 8);
            string[] names = { "交易所", "走势", "持仓", "新闻" };
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                Ui.Size(Ui.Button(navigation, names[i], () =>
                { _stockTab = tab; _stockDraft = null; w.ResetScrollOnRebuild = true; w.Rebuild(); },
                    18, i == _stockTab ? Ui.ButtonHot : Ui.ButtonBg), 150, 38).name = "StockTab_" + i;
            }
            StockFlex(Ui.Rect(navigation, "StockNavigationSpace"), 38);
            Ui.Size(Ui.Button(navigation, "返回手机", () => { w.Close(); OpenPhone(); }, 16), 130, 38);

            var summary = Ui.Row(w.Content, 48, 14);
            StockFlex(Ui.Label(summary, "可用现金  " + S.Ledger.Balance, 19, color: Ui.Accent), 48);
            StockFlex(Ui.Label(summary, "持股市值  " + (S.CityEquityValue + S.FundValue), 18), 48);
            StockFlex(Ui.Label(summary, "浮动盈亏  " + StockUnrealized, 18,
                color: StockUnrealized.IsNegative ? Ui.Warn : Ui.Good), 48);
            StockFlex(Ui.Label(summary, S.StockTradingStatus + "\n下次报价 " + S.StockNextQuote, 15, color: Ui.Dim), 48);
            if (!string.IsNullOrEmpty(_stockTradeFeedback))
            {
                var feedback = StockCard(w.Content);
                StockText(feedback, _stockTradeFeedback, 17, _stockTradeFailed ? Ui.Warn : Ui.Good).name = "StockTradeFeedback";
            }

            // Fixed anchors prevent text preferred widths from changing the three-column proportions.
            var split = Ui.Rect(w.Content, "StockWorkspaceColumns");
            var size = Ui.Size(split).GetComponent<LayoutElement>();
            size.minHeight = 0; size.preferredHeight = 0; size.flexibleHeight = 1;
            float first = _stockTab == 1 ? .29f : .41f;
            var left = StockPane(split, "StockTablePane", 0, first);
            var center = StockPane(split, "StockChartPane", first, .75f);
            var right = StockPane(split, "StockOrderPane", .75f, 1);
            if (_stockTab == 2) BuildStockHoldings(w, left);
            else if (_stockTab == 3) BuildStockNews(w, StockScroll(left, "StockNewsScroll"), null);
            else BuildStockQuotes(w, left, _stockTab == 0);
            BuildStockCompany(w, center, _stockTarget);
            var order = StockScroll(right, "StockOrderScroll");
            if (_stockDraft != null) BuildStockConfirmation(w, order);
            else BuildStockOrder(w, order, _stockTarget);
            string hint = _stockTab == 0 ? "股票 − / + 每次调整100股 · 点买 / 卖直接成交 · 动用预留资金时先确认" : "各栏可独立滚动";
            Ui.Size(Ui.Label(w.Content, hint, 14, color: Ui.Dim), -1, 24);
        }
        private void SelectStock(Window w, string id, int action = 0)
        { _stockTarget = id; _stockAction = action; _stockHelp = false; _stockDraft = null; RefreshStockOrder(w); }
        private static void RefreshStockOrder(Window w)
        {
            w.Rebuild();
            // Reset only the order pane; preserve the user's market/holdings scroll position.
            LayoutRebuilder.ForceRebuildLayoutImmediate(w.Content);
            var scroll = w.Root.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s => s.name == "StockOrderScroll");
            if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
        }

        private void BuildStockQuotes(Window w, Transform parent, bool trading)
        {
            var filters = Ui.Row(parent, 34, 6);
            foreach (bool watched in new[] { false, true })
            {
                bool only = watched;
                Ui.Size(Ui.Button(filters, watched ? "自选" : "全部", () =>
                { _stockWatchOnly = only; w.ResetScrollOnRebuild = true; w.Rebuild(); },
                    16, watched == _stockWatchOnly ? Ui.ButtonHot : Ui.ButtonBg), 85, 34);
            }
            var heading = Ui.Row(parent, 27, 4);
            Ui.Size(Ui.Label(heading, "", 14), 24, 27);
            StockFlex(Ui.Label(heading, "公司", 15, color: Ui.Dim), 27);
            Ui.Size(Ui.Button(heading, "现价", () => { _stockSort = _stockSort == 1 ? 0 : 1; w.Rebuild(); }, 14), 68, 27);
            Ui.Size(Ui.Button(heading, "涨跌", () => { _stockSort = _stockSort == 2 ? 0 : 2; w.Rebuild(); }, 14), 68, 27);
            if (trading)
            {
                Ui.Size(Ui.Label(heading, "持股", 14, TextAnchor.MiddleRight, Ui.Dim), 48, 27);
                Ui.Size(Ui.Label(heading, "交易数量", 14, TextAnchor.MiddleCenter, Ui.Dim), StockQuoteQuantityWidth, 27);
                Ui.Size(Ui.Label(heading, "操作", 14, TextAnchor.MiddleCenter, Ui.Dim), 72, 27);
            }
            var list = StockScroll(parent, "StockQuoteScroll");
            var ids = new[] { "hardware" }.Concat(CityInvestments.All.Select(d => d.Id))
                .Where(id => !_stockWatchOnly || S.StockWatched(id)).ToArray();
            if (_stockSort == 1) ids = ids.OrderByDescending(id => S.CityInvestmentPrice(id).Cents).ToArray();
            if (_stockSort == 2) ids = ids.OrderByDescending(id => S.StockDayChange(id)).ToArray();
            foreach (string id in ids)
            {
                string stock = id; double change = S.StockDayChange(id);
                var row = Ui.Row(list, 43, 4); row.name = "StockQuote_" + id;
                Ui.Size(Ui.Button(row, S.StockWatched(id) ? "★" : "☆", () => { S.ToggleStockWatch(stock); w.Rebuild(); }, 17), 24, 36);
                var name = Ui.Button(row, StockTitle(id), () => SelectStock(w, stock), 15,
                    id == _stockTarget ? Ui.ButtonHot : Ui.ButtonBg);
                StockFlex(name, 39); name.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                Ui.Size(Ui.Label(row, (S.CityInvestmentPrice(id).Cents / 100d).ToString("N2"), 15, TextAnchor.MiddleRight), 68, 39);
                Ui.Size(Ui.Label(row, change.ToString("+0.00%;-0.00%;0.00%"), 14, TextAnchor.MiddleRight, change < 0 ? Ui.Warn : Ui.Good), 68, 39);
                if (!trading) continue;
                Ui.Size(Ui.Label(row, S.CityInvestmentUnits(id).ToString("N0"), 14, TextAnchor.MiddleRight), 48, 39);
                BuildStockQuoteOrder(w, row, stock);
            }
            if (ids.Length == 0) StockText(list, "暂无自选，点☆关注公司。", 16, Ui.Dim);
        }

        private void BuildStockQuoteOrder(Window w, Transform row, string id)
        {
            int step = StockOrderStep(id);
            if (!_stockQuoteQuantities.TryGetValue(id, out int count)) count = step;
            var stepper = Ui.Size(Ui.Row(row, 34, 4), StockQuoteQuantityWidth);
            var number = Ui.Size(Ui.Label(stepper, count.ToString(), 14, TextAnchor.MiddleCenter), 58, 34);
            void Step(int delta)
            {
                count = Mathf.Clamp(count + delta * step, step, StockQuantityLimit);
                _stockQuoteQuantities[id] = count;
                number.text = count.ToString();
            }
            Ui.Size(Ui.Button(stepper, "−", () => Step(-1), 15), 24, 34).transform.SetAsFirstSibling();
            Ui.Size(Ui.Button(stepper, "+", () => Step(1), 15), 24, 34);
            Ui.Size(Ui.Button(row, "买", () => TradeStockQuote(w, id, count, buy: true), 15), 34, 34).name = "StockQuickBuy_" + id;
            Ui.Size(Ui.Button(row, "卖", () => TradeStockQuote(w, id, count, buy: false), 15), 34, 34).name = "StockQuickSell_" + id;
        }

        private void TradeStockQuote(Window w, string id, int quantity, bool buy)
        {
            if (w.Closed) return;
            string label = StockTradeLabel(id, buy ? 0 : 1, quantity);
            void Trade(bool acknowledgeReserve)
            {
                RunStockAction(label, () => S.TradeCityInvestment(id, quantity, buy, acknowledgeReserve));
                w.Rebuild();
            }

            if (!buy || S.StockTradeBlocker(id, quantity, "buy") != null)
            {
                Trade(false);
                return;
            }

            var reserve = S.GetCityCashReserve();
            Money cost = S.CityInvestmentTradeTotal(id, quantity, true);
            Money remaining = reserve.Balance - cost;
            if (remaining >= reserve.Total)
            {
                Trade(false);
                return;
            }

            long price = S.CityInvestmentPrice(id).Cents;
            OpenWindow("买入会动用预留资金", 640, 390, confirmation =>
            {
                Line(confirmation.Content, label, 21, Ui.Accent);
                Para(confirmation.Content, $"本次支付 {cost}（含手续费），买入后现金剩 {remaining}。", 18);
                Para(confirmation.Content, $"未来 {reserve.Days} 天账单、预计支出及资金保护额共 {reserve.Total}。买入后将低于预留额 {reserve.Total - remaining}。", 18, Ui.Warn);
                Para(confirmation.Content, "当前尚未成交，也未扣款。", 16, Ui.Dim);
                var actions = Ui.Row(confirmation.Content, 44, 8);
                Ui.Size(Ui.Button(actions, "取消买入", confirmation.Close, 18), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "仍然买入", () =>
                {
                    if (confirmation.Closed || w.Closed) return;
                    confirmation.Close();
                    if (S.CityInvestmentPrice(id).Cents == price)
                        Trade(true);
                    else
                    {
                        SetStockTradeFeedback(label, "报价已变化，请重新下单");
                        w.Rebuild();
                    }
                }, 18, Ui.Warn), -1, -1, 1).name = "StockReserveConfirm";
            });
        }

        private static string StockTradeLabel(string id, int action, long quantity) =>
            $"{StockTitle(id)} · {StockActions[action]} {quantity:N0}{(id == "hardware" ? "份" : "股")}";

        private void SetStockTradeFeedback(string label, string failure)
        {
            _stockTradeFailed = failure != null;
            _stockTradeFeedback = failure == null ? $"已完成 · {label}" : $"未完成 · {label} · {failure}";
        }

        private bool RunStockAction(string label, Func<bool> trade)
        {
            var session = S;
            string failure = null;
            void CaptureFailure(string message) => failure = message;
            session.Failed += CaptureFailure;
            bool ok;
            try { ok = trade(); }
            finally { session.Failed -= CaptureFailure; }
            SetStockTradeFeedback(label, ok ? null : failure ?? "当前条件已变化，请核对后重试");
            return ok;
        }

        private void BuildStockCompany(Window w, Transform parent, string id)
        {
            var body = StockScroll(parent, "StockCompanyScroll");
            var heading = Ui.Row(body, 35, 5);
            StockFlex(Ui.Label(heading, StockTitle(id), 22, color: Ui.Accent), 35);
            Ui.Size(Ui.Button(heading, S.StockWatched(id) ? "★ 自选" : "☆ 自选", () =>
            { S.ToggleStockWatch(id); w.Rebuild(); }, 15), 85, 32);
            var definition = CityInvestments.Get(id); var state = S.StockState(id);
            StockText(body, definition?.Industry ?? "硬件行业景气指数", 15, Ui.Dim);
            var price = Ui.Row(body, 44, 6); double change = S.StockDayChange(id);
            StockFlex(Ui.Label(price, S.CityInvestmentPrice(id).ToString(), 29, color: Ui.Accent), 44);
            Ui.Size(Ui.Label(price, change.ToString("+0.00%;-0.00%;0.00%"), 20, TextAnchor.MiddleRight,
                change < 0 ? Ui.Warn : Ui.Good), 116, 44);
            var ranges = Ui.Row(body, 32, 5);
            foreach (int days in new[] { 7, 30, 90 })
            {
                int range = days;
                StockFlex(Ui.Button(ranges, "近" + days + "天", () => { _stockChartDays = range; w.Rebuild(); },
                    15, days == _stockChartDays ? Ui.ButtonHot : Ui.ButtonBg), 32);
            }
            var quotes = S.StockQuotes(id).Where(q => q.Minute >= S.Clock.Now.TotalMinutes - _stockChartDays * 1440L).ToArray();
            if (quotes.Length == 0) quotes = S.StockQuotes(id).TakeLastCompatQuotes(1);
            var chart = Ui.Rect(body, "StockPriceLine"); Ui.Size(chart, -1, _stockTab == 1 ? 310 : 230);
            chart.gameObject.AddComponent<StockPriceGraphic>().Initialize(quotes);
            if (quotes.Length < 2) StockText(body, "历史报价不足，后续随交易日更新。", 14, Ui.Dim);
            StockText(body, "昨收 " + Money.FromCents(S.StockPreviousClose(id)), 15, Ui.Dim);
            if (state != null)
            {
                var facts = StockCard(body, "公司经营");
                StockText(facts, state.YesterdayRevenueCents > 0 ? "昨日营收  " + CompactStockMoney(state.YesterdayRevenueCents)
                    + "    利润  " + CompactStockMoney(state.YesterdayProfitCents) : "营收与利润待下一交易日披露", 15);
                StockText(facts, "最近每股分红  " + Money.FromCents(state.LastDividendCents), 15, Ui.Dim);
                if (!string.IsNullOrEmpty(state.LastReason)) StockText(facts, state.LastReason, 14, Ui.Dim);
            }
            BuildStockNews(w, body, id);
        }
        private static string CompactStockMoney(long cents)
        { double yuan = cents / 100d; return Math.Abs(yuan) >= 10000 ? "¥" + (yuan / 10000).ToString("0.##") + "万" : Money.FromCents(cents).ToString(); }

        private void BuildStockOrder(Window w, Transform body, string id)
        {
            StockText(body, "交易 · " + StockTitle(id), 21, Ui.Accent);
            bool index = id == "hardware";
            if (index && _stockAction > 1) _stockAction = 0;
            var state = S.StockState(id);
            int actionCount = index ? 2 : 4;
            for (int first = 0; first < actionCount; first += 2)
            {
                var row = Ui.Row(body, 36, 5);
                for (int i = first; i < Math.Min(first + 2, actionCount); i++)
                {
                    int action = i;
                    StockFlex(Ui.Button(row, StockActions[i], () => { _stockAction = action; w.Rebuild(); },
                        16, i == _stockAction ? Ui.ButtonHot : Ui.ButtonBg), 36);
                }
            }
            int OrderQuantity() => index ? _indexOrderQuantity : _stockOrderQuantity;
            void SetQuantity(int count)
            {
                if (index) _indexOrderQuantity = count;
                else _stockOrderQuantity = count;
            }
            int units = OrderQuantity();
            var quantity = Ui.Row(body, 38, 6); Ui.Size(Ui.Label(quantity, "数量", 17), 42, 38);
            var field = Ui.TextInput(quantity, units.ToString(), index ? "整数份数" : "每手100股", value =>
            {
                if (int.TryParse(value, out int number) && number >= 1 && number <= StockQuantityLimit) SetQuantity(number);
                w.RequestRebuild();
            });
            field.contentType = InputField.ContentType.IntegerNumber; StockFlex(field, 38);
            Ui.Size(Ui.Label(quantity, index ? "份" : "股", 15), 22, 38);
            var shortcuts = Ui.Row(body, 32, 4);
            foreach (int count in index ? new[] { 1, 100, 1000 } : new[] { 100, 500, 1000 })
            { int n = count; StockFlex(Ui.Button(shortcuts, n.ToString(), () => { SetQuantity(n); w.Rebuild(); }, 15), 32); }
            long maximum = StockMaxQuantity(id, _stockAction);
            StockFlex(Ui.Button(shortcuts, "最大", () => { SetQuantity((int)maximum); w.Rebuild(); }, 15, interactable: maximum > 0), 32);
            StockText(body, "当前持股 " + S.CityInvestmentUnits(id).ToString("N0") + " · 做空 " + (state?.ShortUnits ?? 0).ToString("N0"), 15, Ui.Dim);
            StockText(body, "本报价额度 " + S.StockAvailable(id).ToString("N0")
                + (_stockAction >= 2 ? " · 可借 " + S.StockBorrowAvailable(id).ToString("N0") : ""), 14, Ui.Dim);
            StockText(body, "当前操作最多 " + maximum.ToString("N0") + (id == "hardware" ? "份" : "股"), 15);
            var gross = S.CityInvestmentPrice(id) * units;
            string amount = _stockAction == 2 ? "需自有资金 " + S.StockShortOpeningCost(id, units)
                : _stockAction == 3 ? "预计释放 " + S.StockCoverReturn(id, units)
                : (_stockAction == 0 ? "预计支付 " : "预计到手 ") + S.CityInvestmentTradeTotal(id, units, _stockAction == 0);
            StockText(body, amount, 21, Ui.Accent);
            StockText(body, "含手续费 " + CityInvestments.Fee(gross, id == "hardware"), 15, Ui.Dim);
            string why = S.StockTradeBlocker(id, units, StockActionKeys[_stockAction]);
            if (why != null) StockText(body, why, 15, Ui.Warn);
            Ui.Size(Ui.Button(body, "预览" + StockActions[_stockAction], () => ConfirmStockTrade(w, id, _stockAction, OrderQuantity()),
                19, Ui.ButtonHot, why == null), -1, 46).name = "StockTradePreview";
            StockText(body, "确认价格与数量后成交。", 14, Ui.Dim);
            if (state?.ShortUnits > 0)
            {
                double ratio = S.StockMarginRatio(id);
                StockText(body, "担保比例 " + ratio.ToString("P0") + " · 平仓线130%", 16, ratio < 1.5 ? Ui.Warn : Ui.Good);
                Ui.Size(Ui.Button(body, "补充保证金", () => DraftStockMargin(w, id), 16), -1, 36);
            }
            Ui.Size(Ui.Button(body, _stockHelp ? "收起交易规则" : "交易规则 ›", () => { _stockHelp = !_stockHelp; w.Rebuild(); }, 15), -1, 32);
            if (_stockHelp) StockText(body, "周一至周五全天可交易，周末休市。工作日10:00、13:00、16:00更新报价。股票买卖各0.2%，指数各0.5%。每次报价共用额度。做空冻结100%自有保证金及卖出所得；每日借股费0.03%，担保比例≤130%自动平仓。分红同步除息，做空方补偿分红。", 15, Ui.Dim);
        }

        private void BuildStockHoldings(Window w, Transform parent)
        {
            StockText(parent, "持仓对照", 20, Ui.Accent);
            StockText(parent, "已实现盈亏 " + S.StockTotalRealized + " · 冻结保证金 " + S.StockFrozenMargin, 15, Ui.Dim);
            var list = StockScroll(parent, "StockHoldingsScroll");
            var header = Ui.Row(list, 28, 5);
            StockFlex(Ui.Label(header, "公司 / 数量", 14, color: Ui.Dim), 28);
            Ui.Size(Ui.Label(header, "均价 / 现价", 14, TextAnchor.MiddleRight, Ui.Dim), 100, 28);
            Ui.Size(Ui.Label(header, "浮动盈亏", 14, TextAnchor.MiddleRight, Ui.Dim), 100, 28);
            var longs = new[] { "hardware" }.Concat(CityInvestments.All.Select(d => d.Id)).Where(id => S.CityInvestmentUnits(id) > 0).ToArray();
            foreach (string id in longs)
            {
                long units = S.CityInvestmentUnits(id); string stock = id;
                Money value = S.CityInvestmentPrice(id) * units, profit = value - S.CityInvestmentCost(id);
                var row = Ui.Row(list, 62, 5);
                StockFlex(Ui.Button(row, StockTitle(id) + "\n<size=13>" + units.ToString("N0") + (id == "hardware" ? "份" : "股") + "</size>",
                    () => SelectStock(w, stock, 1), 16, id == _stockTarget ? Ui.ButtonHot : Ui.ButtonBg), 58);
                Ui.Size(Ui.Label(row, (S.CityInvestmentCost(id).Cents / (100d * units)).ToString("N2") + "\n" + (S.CityInvestmentPrice(id).Cents / 100d).ToString("N2"),
                    15, TextAnchor.MiddleRight), 100, 58);
                Ui.Size(Ui.Label(row, profit.ToString(), 16, TextAnchor.MiddleRight, profit.IsNegative ? Ui.Warn : Ui.Good), 100, 58);
            }
            if (longs.Length == 0) StockText(list, "暂无普通持股", 15, Ui.Dim);
            StockText(list, "借股做空", 18, Ui.Accent);
            var shorts = S.CityEquities.Where(e => e.ShortUnits > 0).ToArray();
            foreach (var state in shorts)
            {
                string id = state.Id; var card = StockCard(list);
                var profit = Money.FromCents(state.ShortBasisCents - state.PriceCents * state.ShortUnits);
                var row = Ui.Row(card, 36, 5);
                StockFlex(Ui.Button(row, StockTitle(id) + " · " + state.ShortUnits + "股", () => SelectStock(w, id, 3), 16,
                    id == _stockTarget ? Ui.ButtonHot : Ui.ButtonBg), 36);
                Ui.Size(Ui.Label(row, profit.ToString(), 16, TextAnchor.MiddleRight, profit.IsNegative ? Ui.Warn : Ui.Good), 120, 36);
                StockText(card, "开仓均价 " + (state.ShortBasisCents / (100d * state.ShortUnits)).ToString("N2")
                    + " · 现价 " + S.CityInvestmentPrice(id) + " · 担保 " + S.StockMarginRatio(id).ToString("P0"), 15, Ui.Dim);
                StockText(card, "自有保证金 " + Money.FromCents(state.ShortMarginCents) + " · 冻结总额 " + Money.FromCents(state.ShortCollateralCents), 14, Ui.Dim);
                var actions = Ui.Row(card, 32, 5);
                StockFlex(Ui.Button(actions, "补保证金", () => DraftStockMargin(w, id), 15), 32);
                StockFlex(Ui.Button(actions, "买回平仓", () => SelectStock(w, id, 3), 15), 32);
            }
            if (shorts.Length == 0) StockText(list, "暂无做空持仓", 15, Ui.Dim);
            Ui.Size(Ui.Button(list, _stockShowFees ? "收起账户明细" : "账户与费用明细 ›", () => { _stockShowFees = !_stockShowFees; w.Rebuild(); }, 15), -1, 32);
            if (_stockShowFees)
            {
                StockText(list, "借股费每天按市值0.03%收取。保证金冻结与释放为内部转账，不是费用或盈利。", 14, Ui.Dim);
                var ids = CityInvestments.All.Select(d => d.Id).Concat(new[] { "hardware" }).ToArray();
                foreach (var entry in S.Ledger.Entries.Where(e => ids.Contains(e.Attribution?.PropertyId)).Reverse().Take(12))
                    StockText(list, entry.Date + " · " + entry.Category + " " + entry.Amount, 14);
            }
        }

        private void BuildStockNews(Window w, Transform body, string id)
        {
            if (id != null) StockText(body, "近期消息", 18, Ui.Accent);
            var news = S.StockNewsItems.Where(n => id == null || n.StockId == id).Reverse().Take(id == null ? 30 : 2).ToArray();
            if (news.Length == 0) StockText(body, "暂无已发布消息，经营披露后更新。", 15, Ui.Dim);
            foreach (var n in news)
            {
                var item = n; var card = StockCard(body, n.Headline);
                var date = new GameDate((int)(n.Minute / 1440)); int minute = (int)(n.Minute % 1440);
                StockText(card, date + " " + GameSession.ClockText(minute), 13, Ui.Dim);
                StockText(card, n.Detail, 15);
                if (id == null) Ui.Size(Ui.Button(card, "查看 " + StockTitle(item.StockId), () => SelectStock(w, item.StockId), 15), -1, 32);
            }
        }
        private void ConfirmStockTrade(Window w, string id, int action, int quantity)
        {
            _stockTarget = id;
            _stockAction = action;
            _stockDraft = new StockOrderDraft
            {
                Id = id,
                Action = action,
                Quantity = quantity,
                PriceCents = S.CityInvestmentPrice(id).Cents
            };
            RefreshStockOrder(w);
        }
        private void DraftStockMargin(Window w,string id)
        {
            _stockTarget=id;_stockAction=3;
            _stockDraft=new StockOrderDraft {Id=id,ExtraMargin=Money.FromYuan(1000)};
            RefreshStockOrder(w);
        }
        private void BuildStockConfirmation(Window w,Transform body)
        {
            var draft=_stockDraft;
            StockText(body,draft.ExtraMargin.IsPositive?"补充保证金":"订单确认",21,Ui.Accent);
            StockText(body,StockTitle(draft.Id),20);
            if(draft.ExtraMargin.IsPositive)
            {
                StockText(body,"补充金额",16);
                var choices=Ui.Row(body,38,6);
                foreach(long yuan in new long[]{1000,10000})
                {long amount=yuan;Ui.Size(Ui.Button(choices,Money.FromYuan(amount).ToString(),()=>{draft.ExtraMargin=Money.FromYuan(amount);w.Rebuild();},16,draft.ExtraMargin==Money.FromYuan(amount)?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
                StockText(body,"本次冻结 "+draft.ExtraMargin+"\n补充后可用现金 "+(S.Ledger.Balance-draft.ExtraMargin),17);
                StockText(body,"补充金额进入保证金账户，买回平仓后释放剩余资金。",15,Ui.Dim);
                var e=S.StockState(draft.Id);bool enabled=e?.ShortUnits>0&&S.Ledger.Balance>=draft.ExtraMargin;
                if(!enabled)StockText(body,e?.ShortUnits>0?"可用现金不足":"这只股票已经没有做空持仓",15,Ui.Warn);
                Ui.Size(Ui.Button(body,"确认补充保证金",()=>SubmitStockOrderDraft(w,draft,false),18,Ui.ButtonHot,enabled),-1,44);
            }
            else
            {
                StockText(body,StockActions[draft.Action]+" "+draft.Quantity+(draft.Id=="hardware"?"份":"股"),20);
                StockText(body,"成交价格 "+Money.FromCents(draft.PriceCents),18);
                StockText(body,StockTradeSummary(draft.Id,draft.Quantity,draft.Action),17);
                var cost=draft.Action==2?S.StockShortOpeningCost(draft.Id,draft.Quantity):S.CityInvestmentTradeTotal(draft.Id,draft.Quantity,true);
                bool reserve=(draft.Action==0||draft.Action==2)&&S.Ledger.Balance-cost<S.GetCityCashReserve().Total;
                if(draft.Action==0||draft.Action==2)StockText(body,"支付后可用现金 "+(S.Ledger.Balance-cost),16);
                if(draft.Action==2)StockText(body,"股价跌了赚差价，涨了会亏损。卖出所得也会冻结，每天计借股费；担保比例≤130%自动买回平仓。",15,Ui.Dim);
                if(reserve)StockText(body,"本次会动用工资、房租或采购的预留资金。",15,Ui.Warn);
                string why=S.StockTradeBlocker(draft.Id,draft.Quantity,StockActionKeys[draft.Action]);
                if(S.CityInvestmentPrice(draft.Id).Cents!=draft.PriceCents)why="报价已变化，请返回重新预览";
                if(why!=null)StockText(body,why,15,Ui.Warn);
                Ui.Size(Ui.Button(body,"确认"+StockActions[draft.Action],()=>SubmitStockOrderDraft(w,draft,reserve),18,Ui.ButtonHot,why==null),-1,44).name="StockTradeConfirm";
            }
            Ui.Size(Ui.Button(body,"返回修改",()=>{_stockDraft=null;RefreshStockOrder(w);},16),-1,38);
        }
        private void SubmitStockOrderDraft(Window w,StockOrderDraft draft,bool reserve)
        {
            if(_stockDraft!=draft)return;
            _stockDraft=null; // A stale button cannot submit the same draft twice.
            bool ok;
            if (draft.ExtraMargin.IsPositive)
                ok = RunStockAction($"{StockTitle(draft.Id)} · 补充保证金 {draft.ExtraMargin}",
                    () => S.AddStockShortMargin(draft.Id, draft.ExtraMargin));
            else
            {
                string label = StockTradeLabel(draft.Id, draft.Action, draft.Quantity);
                if (S.CityInvestmentPrice(draft.Id).Cents != draft.PriceCents)
                {
                    SetStockTradeFeedback(label, "报价已变化，请重新预览");
                    ok = false;
                }
                else
                    ok = RunStockAction(label, () => draft.Action switch
                    {
                        2 => S.OpenStockShort(draft.Id, draft.Quantity, reserve),
                        3 => S.CoverStockShort(draft.Id, draft.Quantity),
                        _ => S.TradeCityInvestment(draft.Id, draft.Quantity, draft.Action == 0, reserve)
                    });
            }
            if (ok) _stockTarget = draft.Id;
            else _stockDraft=draft;
            RefreshStockOrder(w);
        }
    }
}
