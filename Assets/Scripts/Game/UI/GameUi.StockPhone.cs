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
        private enum StockPage { Charts, Exchange, News }
        private static readonly string[] StockIds = CityInvestments.All.Select(d => d.Id).Append("hardware").ToArray();
        private static readonly Color[] StockChartColors =
        {
            new Color32(151,96,31,255), new Color32(42,111,101,255), new Color32(114,78,147,255),
            new Color32(165,65,64,255), new Color32(56,97,151,255), new Color32(105,107,42,255),
            new Color32(161,82,46,255), new Color32(48,116,69,255), new Color32(147,71,117,255),
            new Color32(35,113,132,255), new Color32(82,92,94,255)
        };
        private const int StockQuoteGap = 6, StockQuoteWidth = 218, StockPositionWidth = 104;
        private const int StockCostWidth = 100, StockDraftWidth = 116, StockActionWidth = 74;
        private const int StockCashCardHeight = 68;
        private static readonly int[] StockLotSteps = { 1, 5, 25, 100, 200, 500 };
        private readonly Dictionary<string, long> _stockSpotDraft = new Dictionary<string, long>();
        private readonly Dictionary<string, long> _stockShortDraft = new Dictionary<string, long>();
        private readonly HashSet<string> _stockHiddenChartSeries = new HashSet<string>();
        private GameSession _stockSession;
        private Window _stockWindow;
        private Button _securitiesLessonButton;
        private bool _stockAtBank;
        private StockPage _stockPage = StockPage.Exchange;
        private string _stockExpanded, _stockTradeFeedback;
        private bool _stockShortMode, _stockCandles, _stockWatchOnly, _stockHelp, _stockTradeFailed;
        private bool _stockTradeRevealPending;
        private int _stockSort, _stockChartDays = 7, _stockLotStep;
        private Dictionary<string, long> StockDraft(bool shortMode) => shortMode ? _stockShortDraft : _stockSpotDraft;
        private int StockStep => StockLotSteps[_stockLotStep];

        private void ResetStockOrderDrafts()
        {
            _stockSpotDraft.Clear();
            _stockShortDraft.Clear();
            _stockTradeFeedback = null;
            _stockTradeFailed = false;
            _stockTradeRevealPending = false;
            _stockShortMode = false;
            _stockLotStep = 0;
        }

        // 证券页可以放进别的容器（居家工作台「投资」页）：构建期间 _stockHost 指向容器，_stockEmbedded 为真。
        private Transform _stockHost;
        private bool _stockEmbedded;
        private Transform StockRoot(Window w) => _stockHost != null ? _stockHost : w.Content;

        private void EnsureStockSessionState()
        {
            if (_stockSession == S) return;
            _stockSession = S;
            _stockPage = StockPage.Exchange;
            _stockExpanded = null;
            _stockShortMode = _stockCandles = _stockWatchOnly = _stockHelp = false;
            _stockSort = _stockLotStep = 0;
            _stockChartDays = 7;
            _stockTradeFeedback = null;
            _stockSpotDraft.Clear();
            _stockShortDraft.Clear();
            _stockHiddenChartSeries.Clear();
        }

        /// <summary>把证券交易所放进 host（居家工作台）。不暂停时间，没有「返回手机」。</summary>
        private void BuildEmbeddedStock(Window w, Transform host)
        {
            string blocker = S.AppBlocker(PhoneApps.Securities);
            if (blocker != null)
            {
                Ui.Notice(host, blocker);
                return;
            }
            EnsureStockSessionState();
            _stockHost = host;
            _stockEmbedded = true;
            try { BuildStockWorkspace(w); }
            finally { _stockHost = null; _stockEmbedded = false; }
        }

        public void OpenCityInvestments()
        {
            string blocker = S.AppBlocker(PhoneApps.Securities);
            if (blocker != null) { Toast(blocker); return; }
            _stockAtBank = false;
            EnsureStockSessionState();
            // 仅在主动进入应用时清空，界面刷新及本次浏览期间切页保留当前操作。
            ResetStockOrderDrafts();
            ClosePhone();
            if (_stockWindow != null && !_stockWindow.Closed)
            {
                _stockWindow.TitleText.text = "证券";
                _stockWindow.Root.SetAsLastSibling();
                _stockWindow.Rebuild();
                return;
            }
            _stockTradeFeedback = null;
            _stockWindow = OpenPhoneAppWindow("证券", BuildStockWorkspace, scrollContent: false);
            _stockWindow.OnClosed = () =>
            {
                ResetStockOrderDrafts();
                _stockAtBank = false;
                if (_workbenchTab == 2) _homeWorkbench?.RequestRebuild();
            };
        }

        private void OpenBankSecurities()
        {
            if (S.BankBorrowTutorialPending) return;
            string blocker = S.BankBlocker();
            if (blocker != null) { Toast(blocker); return; }
            EnsureStockSessionState();
            ResetStockOrderDrafts();
            S.VisitQuestTarget(QuestTarget.BankSecurities);
            _stockAtBank = true;
            if (_stockWindow != null && !_stockWindow.Closed)
            {
                _stockWindow.TitleText.text = "银行 · 证券交易";
                _stockWindow.Root.SetAsLastSibling();
                _stockWindow.Rebuild();
                return;
            }
            _stockWindow = OpenPhoneAppWindow("银行 · 证券交易", BuildStockWorkspace, scrollContent: false);
            _stockWindow.OnClosed = () =>
            {
                ResetStockOrderDrafts();
                _stockAtBank = false;
                if (_workbenchTab == 2) _homeWorkbench?.RequestRebuild();
            };
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
        private static (Text amount, Text detail) StockCashCard(Transform parent, string amountName,
            string title, int width, bool withDetail = false)
        {
            var card = Ui.Size(Ui.FlatPanel(parent, "StockCashCard", Ui.FlatWell, Ui.FlatHotEdge),
                width, StockCashCardHeight, 0);
            card.raycastTarget = false;
            var layout = Ui.VList(card.rectTransform, 3, 0);
            layout.padding = new RectOffset(12, 12, 5, 5);
            var heading = Ui.Row(card.transform, 19, 8);
            Ui.Size(Ui.Label(heading, title, 15, color: Ui.TextColor), 96, 19);
            Text detail = null;
            if (withDetail)
            {
                detail = Ui.Size(Ui.Label(heading, "", 14, TextAnchor.MiddleRight, Ui.Dim), 0, 19, 1);
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 10;
                detail.resizeTextMaxSize = 14;
                detail.verticalOverflow = VerticalWrapMode.Truncate;
            }
            var amount = Ui.Size(Ui.Label(card.transform, "", 32, color: Ui.Accent), -1, 36);
            amount.name = amountName;
            amount.fontStyle = FontStyle.Bold;
            amount.resizeTextForBestFit = true;
            amount.resizeTextMinSize = 16;
            amount.resizeTextMaxSize = 32;
            amount.verticalOverflow = VerticalWrapMode.Truncate;
            return (amount, detail);
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
            var root = StockRoot(w);
            var summary = Ui.Row(root, StockCashCardHeight, 12);
            var floating = StockUnrealized;
            var realized = S.StockTotalRealized;
            var (cash, _) = StockCashCard(summary, "StockAvailableCash", "可用现金", 300);
            var balance = S.Ledger.Balance;
            cash.text = balance.ToString();
            cash.color = balance.IsNegative ? Ui.FlatDown : Ui.Accent;
            StockFlex(Ui.Label(summary, "总浮动盈亏  " + floating, 18, color: floating.IsNegative ? Ui.Warn : Ui.Good), 48);
            StockFlex(Ui.Label(summary, "已实现盈亏  " + realized, 18, color: realized.IsNegative ? Ui.Warn : Ui.Good), 48);
            StockFlex(Ui.Label(summary, S.StockTradingStatus + "\n下次报价 " + S.StockNextQuote, 15, color: Ui.Dim), 48);
            if (!_stockEmbedded)
            {
                bool atBank = _stockAtBank;
                Ui.Size(Ui.Button(summary, atBank ? "返回银行" : "返回手机", () =>
                {
                    w.Close();
                    if (atBank) { if (S.AtBank) OpenBank(S.CurrentPlace); }
                    else OpenPhone();
                }, 16), 124, 36);
            }
            if (!_stockEmbedded && _stockAtBank && S.AtBank && S.CurrentQuest()?.Id == "bank_securities")
            {
                var lesson = Ui.Row(root, 88, 12);
                Ui.Size(Ui.Label(lesson, "证券入门\n先看股价和涨跌，点 + 或 − 选择待成交股数，再点「成交」。最低 1 股，买卖收取 0.2% 手续费。\n股价会波动，留足门店周转资金。手机里的「证券」也能查看持仓。", 17), 0, 88, 1);
                _securitiesLessonButton = Ui.Size(Ui.Button(lesson, "已了解，继续开店", () =>
                {
                    if (!S.CompleteQuestLesson("bank_securities")) return;
                    w.Close();
                    UpdateQuestTracker();
                }, 18, Ui.ButtonHot, S.CanCompleteQuestLesson("bank_securities")), 190, 60);
                _securitiesLessonButton.name = "BankSecuritiesLessonComplete";
            }

            var (navigation, refreshNavigation) = BuildStockPageTabs(w, root);
            switch (_stockPage)
            {
                case StockPage.Charts: BuildStockCharts(w); break;
                case StockPage.News: BuildStockNews(w, StockScroll(root, "StockNewsScroll")); break;
                default: BuildStockExchange(w, refreshNavigation); break;
            }
            navigation.SetAsLastSibling();
        }

        private (RectTransform navigation, Action refresh) BuildStockPageTabs(Window w, Transform parent)
        {
            string[] names = { "走势图", "交易所", "新闻" };
            var navigation = Ui.Row(parent, 38, StockQuoteGap);
            Text exchange = null;
            foreach (StockPage page in new[] { StockPage.Charts, StockPage.Exchange, StockPage.News })
            {
                var button = Ui.Button(navigation, names[(int)page], () =>
                {
                    if (_stockPage == page) return;
                    _stockPage = page;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(page == _stockPage));
                button.name = "StockPage_" + page;
                Ui.Size(button, 132, 36);
                if (page == StockPage.Exchange) exchange = button.GetComponentInChildren<Text>();
            }
            void Refresh()
            {
                int count = _stockSpotDraft.Count + _stockShortDraft.Count;
                exchange.text = count == 0 ? "交易所" : $"交易所 · {count}";
            }
            Refresh();
            return (navigation, Refresh);
        }

        private void BuildStockExchange(Window w, Action refreshNavigation)
        {
            bool shortMode = _stockShortMode;
            var draft = StockDraft(shortMode);
            var root = StockRoot(w);
            var modes = Ui.Row(root, 37, StockQuoteGap);
            foreach (bool mode in new[] { false, true })
            {
                var button = Ui.Button(modes, mode ? "做空" : "交易所", () =>
                {
                    if (mode == shortMode) return;
                    _stockShortMode = mode;
                    _stockTradeFeedback = null;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(mode == shortMode));
                button.name = "StockMode_" + (mode ? "short" : "spot");
                StockFlex(button, 36);
            }
            Ui.Size(Ui.Button(modes, _stockWatchOnly ? "只看自选 ✓" : "只看自选", () =>
            {
                _stockWatchOnly = !_stockWatchOnly;
                w.ResetScrollOnRebuild = true;
                w.Rebuild();
            }, 15, Ui.SelectedButton(_stockWatchOnly)), 110, 36);
            var help = Ui.Size(Ui.Button(modes, "?", () => { _stockHelp = !_stockHelp; w.Rebuild(); }, 18), 38, 36);
            help.Tip("交易规则");
            if (_stockHelp)
            {
                StockText(root, $"个股单日涨跌上限为 ±{GameSession.StockDailyLimitPercent}%，第 {GameSession.StockEarlyMarketDays + 1} 天起波动加大。\n"
                    + $"股票最低买入 {Ui.Emphasize("1 股")}。表头数量控制每次加减的股数，点{Ui.Emphasize("「成交」")}统一结算。做空冻结自有保证金及卖出所得，每日借股费 0.03%，{Ui.Emphasize("担保比例≤130%时自动平仓", Ui.Warn)}。", 15, Ui.Dim);
                Ui.Size(Ui.Button(root, "账户与费用明细", OpenStockFees, 15), -1, 30);
            }

            var refreshRows = new List<Action>();
            Text net = null, fees = null, status = null;
            Button clear = null, execute = null;
            StockBatchQuote shownQuote = default;
            void Refresh()
            {
                foreach (var refreshRow in refreshRows) refreshRow();
                refreshNavigation();
                var quote = shownQuote = S.PreviewStockBatch(draft, shortMode, calculateReserve: false);
                net.text = StockSignedMoney(quote.Net);
                net.color = quote.Net.IsNegative ? Ui.FlatDown : quote.Net.IsPositive ? Ui.Good : Ui.TextColor;
                fees.text = (draft.ContainsKey("hardware") ? "佣金 0.2% · 旧指数 0.5%" : "佣金 0.2%")
                    + (draft.Count > 0 ? " · " + quote.Fees : "");
                bool ready = quote.Blocker == null;
                status.text = _stockTradeFeedback ?? (ready
                    ? $"{draft.Count} 笔待成交 · 成交后现金 {quote.CashAfter}"
                    : quote.Blocker);
                status.color = _stockTradeFeedback != null ? (_stockTradeFailed ? Ui.Warn : Ui.Good)
                    : !ready && draft.Count > 0 ? Ui.Warn : Ui.Dim;
                clear.interactable = draft.Count > 0;
                execute.interactable = ready;
            }

            var heading = Ui.Row(root, 33, StockQuoteGap);
            heading.GetComponent<HorizontalLayoutGroup>().padding.right = 20;
            StockFlex(Ui.Label(heading, "公司", 15, color: Ui.Dim), 33);
            var quotes = Ui.Row(heading, 33, StockQuoteGap);
            Ui.Size(quotes, StockQuoteWidth, 33, 0);
            void Sort(int column) { _stockSort = _stockSort == column ? 0 : column; w.Rebuild(); }
            Ui.Size(Ui.Button(quotes, "股价", () => Sort(1), 15), 65, 31);
            Text stepLabel = null;
            string StepLabel() => "×" + StockLotSteps[_stockLotStep] + " 股";
            var cycle = Ui.Size(Ui.Button(quotes, StepLabel(), () =>
            {
                _stockLotStep = (_stockLotStep + 1) % StockLotSteps.Length;
                stepLabel.text = StepLabel();
            }, 15, Ui.SelectedButton(true)), 74, 31);
            cycle.name = "StockLotCycle";
            cycle.Tip(() => $"每次加减 {StockStep:N0} 股 · 点击切换");
            stepLabel = cycle.GetComponentInChildren<Text>();
            Ui.Size(Ui.Button(quotes, "涨跌", () => Sort(2), 15), 65, 31);
            StockCell(heading, shortMode ? "做空股数" : "我的持股", StockPositionWidth, Ui.Dim);
            StockCell(heading, shortMode ? "开仓均价" : "成本价", StockCostWidth, Ui.Dim);
            StockCell(heading, "待成交", StockDraftWidth, Ui.Dim);
            Ui.Size(Ui.Label(heading, shortMode ? "做空" : "买入", 15, TextAnchor.MiddleCenter, Ui.Dim), StockActionWidth, 39);
            Ui.Size(Ui.Label(heading, shortMode ? "平仓" : "卖出", 15, TextAnchor.MiddleCenter, Ui.Dim), StockActionWidth, 39);
            Ui.Size(Ui.Rect(heading, "StockActionsSpacer"), StockActionWidth, 39);

            var list = StockScroll(root, "StockQuoteScroll");
            var ids = StockIds.Where(id => (!_stockWatchOnly || S.StockWatched(id) || draft.ContainsKey(id))
                && (id != "hardware" || !shortMode && (S.FundUnits > 0 || draft.ContainsKey(id))));
            if (_stockSort == 1) ids = ids.OrderByDescending(id => S.CityInvestmentPrice(id).Cents);
            if (_stockSort == 2) ids = ids.OrderByDescending(S.StockDayChange);
            foreach (string id in ids) refreshRows.Add(BuildStockRow(w, list, id, shortMode, Refresh));
            if (refreshRows.Count == 0) StockText(list, "暂无自选股票。", 16, Ui.Dim);

            var footer = Ui.Row(root, StockCashCardHeight, 10);
            (net, fees) = StockCashCard(footer, "StockBatchNet", "现金净变动", 360, withDetail: true);
            status = Ui.Label(footer, "", 16, TextAnchor.MiddleCenter);
            status.name = "StockTradeFeedback";
            StockFlex(status, 56);
            clear = Ui.Size(Ui.Button(footer, "清空", () =>
            {
                draft.Clear();
                _stockTradeFeedback = null;
                if (_stockWatchOnly) w.Rebuild();
                else Refresh();
            }, 16), 82, 42);
            clear.name = "StockDraftClear";
            execute = Ui.Size(Ui.Button(footer, "成交！", () => ExecuteStockDraft(w, shortMode, shownQuote), 20, Ui.ButtonHot), 124, 46);
            execute.name = "StockBatchExecute";
            Refresh();
            if (_stockTradeRevealPending)
            {
                _stockTradeRevealPending = false;
                if (_stockTradeFeedback != null && !_stockTradeFailed)
                {
                    var reveal = NewUiReveal();
                    reveal?.Fade(status.transform, 0);
                    reveal?.Emphasize(status.transform, 0);
                    PlayUiReveal(reveal);
                }
            }
        }

        private static Text StockCell(Transform row, string text, int width, Color? color = null) =>
            Ui.Size(Ui.Label(row, text, 16, TextAnchor.MiddleRight, color), width, 39);

        private long StockHeld(string id, bool shortMode) =>
            shortMode ? S.StockState(id)?.ShortUnits ?? 0 : S.CityInvestmentUnits(id);

        private bool CanOpenStockPosition(string id, bool shortMode) => id != "hardware"
            && StockHeld(id, !shortMode) == 0;

        private void SetStockDraft(string id, bool shortMode, long units)
        {
            var draft = StockDraft(shortMode);
            if (units == 0) draft.Remove(id);
            else draft[id] = units;
            _stockTradeFeedback = null;
        }

        private void StepStockDraft(string id, bool shortMode, int direction, Action refresh)
        {
            var draft = StockDraft(shortMode);
            long current = draft.TryGetValue(id, out long units) ? units : 0;
            long held = StockHeld(id, shortMode);
            long minimum = -Math.Min(held, GameSession.StockOrderLimit);
            long maximum = CanOpenStockPosition(id, shortMode) ? GameSession.StockOrderLimit : 0;
            long next = Math.Max(minimum, Math.Min(maximum, current + direction * StockStep));
            SetStockDraft(id, shortMode, next);
            refresh();
        }

        private Action BuildStockRow(Window w, Transform list, string id, bool shortMode, Action refresh)
        {
            bool expanded = _stockExpanded == id;
            var stock = S.StockState(id);
            long held = StockHeld(id, shortMode);
            long basis = shortMode ? stock.ShortBasisCents : S.CityInvestmentCost(id).Cents;
            double? cost = held > 0 ? basis / (double)held : (double?)null;
            string costText = cost.HasValue ? "¥" + (cost.Value / 100).ToString("N2") : "—";
            var row = Ui.Row(list, 42, StockQuoteGap);
            row.name = "StockQuote_" + id;
            var background = row.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            Ui.Size(Ui.Button(row, S.StockWatched(id) ? "★" : "☆", () => { S.ToggleStockWatch(id); w.Rebuild(); }, 17), 40, 36);
            var name = Ui.Button(row, (expanded ? "▾  " : "▸  ") + StockTitle(id), () =>
            {
                _stockExpanded = expanded ? null : id;
                _stockCandles = false;
                w.Rebuild();
                if (!expanded) RevealStock(w, id);
            }, 17, Ui.SelectedButton(expanded));
            name.name = "StockExpand_" + id;
            StockFlex(name, 37);
            name.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            double change = S.StockDayChange(id);
            StockCell(row, S.CityInvestmentPrice(id).ToString(), 106);
            string changeText = change == 0 ? "0.00%" : Math.Abs(change) < .00005
                ? (change > 0 ? "+<0.01%" : "−<0.01%") : change.ToString("+0.00%;-0.00%");
            StockCell(row, changeText, StockQuoteWidth - 106 - StockQuoteGap, Ui.ChangeColor(change, 0))
                .Tip("较上一交易日收盘价的涨跌幅。尚无新报价时保留最近一次涨跌幅。\n对比价格："
                    + PCShopSell.Core.Common.Money.FromCents(S.StockPreviousClose(id)));
            StockCell(row, held.ToString("N0"), StockPositionWidth);
            StockCell(row, costText, StockCostWidth, cost.HasValue ? StockPriceGraphic.CostColor : Ui.Dim);
            var pending = StockCell(row, "—", StockDraftWidth, Ui.Dim);
            pending.name = "StockDraft_" + id;
            var plus = Ui.Size(Ui.Button(row, "+", () => StepStockDraft(id, shortMode, 1, refresh), 23, Ui.ButtonBg), StockActionWidth, 35);
            plus.name = "StockDraftPlus_" + id;
            var minus = Ui.Size(Ui.Button(row, "−", () => StepStockDraft(id, shortMode, -1, refresh), 23), StockActionWidth, 35);
            minus.name = "StockDraftMinus_" + id;
            foreach (var button in new[] { plus, minus })
            {
                var repeat = button.GetComponent<HoldRepeat>();
                repeat.Owner = w;
                repeat.Key = (shortMode ? "short/" : "spot/") + button.name;
            }
            plus.Tip(() => $"每次增加 {StockStep:N0} 股");
            minus.Tip(() => $"每次减少 {StockStep:N0} 股");
            Ui.Size(Ui.Button(row, "…", () => OpenStockOrderTools(w, id, shortMode), 20), StockActionWidth, 35).name = "StockDraftMore_" + id;
            if (expanded) BuildStockDetails(w, list, id, held, basis, cost, costText);
            return () =>
            {
                var draft = StockDraft(shortMode);
                long units = draft.TryGetValue(id, out long value) ? value : 0;
                pending.text = units == 0 ? "—" : $"{(units > 0 ? "+" : "−")}{Math.Abs(units):N0}{(id == "hardware" ? " 份" : " 股")}";
                pending.color = units == 0 ? Ui.Dim : Ui.Accent;
                background.color = units == 0 ? Color.clear : Ui.PanelBg2;
                plus.interactable = units < 0 || CanOpenStockPosition(id, shortMode) && units < GameSession.StockOrderLimit;
                minus.interactable = units > -Math.Min(held, GameSession.StockOrderLimit);
            };
        }

        private void BuildStockDetails(Window w, Transform parent, string id, long units, long basis, double? cost, string costText)
        {
            var card = StockCard(parent);
            card.name = "StockDetails_" + id;
            const int days = 7;
            var executions = S.StockExecutions(id, StockRangeStart(days), _stockShortMode);
            var heading = Ui.Row(card, 34, 8);
            StockFlex(Ui.Label(heading, "近7天走势 · " + StockTitle(id), 18, color: Ui.Accent), 34);
            if (executions.Length > 0)
            {
                Ui.Size(Ui.Label(heading, _stockShortMode ? "▲ 平仓" : "▲ 买入", 15, TextAnchor.MiddleCenter, StockPriceGraphic.BuyColor), 64, 30);
                Ui.Size(Ui.Label(heading, _stockShortMode ? "▼ 做空" : "▼ 卖出", 15, TextAnchor.MiddleCenter, StockPriceGraphic.SellColor), 64, 30);
                Ui.Size(Ui.Button(heading, "成交明细", () => OpenStockTradeHistory(id, executions), 15), 100, 32);
            }
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

            var quotes = StockQuotesInRange(id, days);
            var graph = Ui.Size(Ui.Rect(card, "StockPriceLine"), -1, 218);
            graph.gameObject.AddComponent<StockPriceGraphic>().Initialize(quotes, cost, _stockCandles, executions);
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
        }

        private void OpenStockTradeHistory(string id, StockExecution[] executions)
        {
            OpenWindow("成交明细 · " + StockTitle(id), 680, 520, w =>
            {
                foreach (var trade in executions.Reverse().OrderByDescending(e => e.Minute))
                    Line(w.Content, StockPriceGraphic.ExecutionText(trade), 17,
                        StockPriceGraphic.IsBuy(trade) ? StockPriceGraphic.BuyColor : StockPriceGraphic.SellColor);
            });
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

        private long StockRangeStart(int days) => Math.Max(0, S.Clock.Today.TotalDays - days + 1) * 1440L;

        private StockQuote[] StockQuotesInRange(string id, int days)
        {
            long from = StockRangeStart(days);
            return S.StockQuotes(id).Where(q => q.Minute >= from).ToArray();
        }

        private void BuildStockCharts(Window w)
        {
            var body = StockScroll(StockRoot(w), "StockChartsScroll");
            var ranges = Ui.Row(body, 36, 6);
            StockFlex(Ui.Label(ranges, "股价走势", 18, color: Ui.Accent), 36);
            foreach (int days in new[] { 1, 7, 30 })
                Ui.Size(Ui.Button(ranges, days == 1 ? "今日" : $"近{days}天", () => { _stockChartDays = days; w.Rebuild(); },
                    16, Ui.SelectedButton(days == _stockChartDays)), 112, 34);

            var chartRow = Ui.Row(body, 430, 12);
            var legend = Ui.Rect(chartRow, "StockChartLegend");
            Ui.Size(legend, 264, 430, 0);
            Ui.VList(legend, 3, 0);
            var indices = Enumerable.Range(0, StockIds.Length).ToArray();
            foreach (int i in indices)
            {
                string id = StockIds[i];
                bool hidden = _stockHiddenChartSeries.Contains(id);
                Color tint = hidden ? Ui.Dim : StockChartColors[i % StockChartColors.Length];
                var item = Ui.Row(legend, 34, 5);
                var color = Ui.Panel(item, "SeriesColor", tint);
                color.raycastTarget = false;
                Ui.Size(color, 5, 24);
                var button = Ui.Button(item, $"{StockTitle(id)}   {S.CityInvestmentPrice(id)}", () =>
                {
                    if (!_stockHiddenChartSeries.Add(id)) _stockHiddenChartSeries.Remove(id);
                    w.Rebuild();
                }, 15);
                button.name = "StockChartSeries_" + id;
                button.Tip(hidden ? "显示曲线" : "隐藏曲线");
                StockFlex(button, 33);
                var label = button.GetComponentInChildren<Text>();
                label.color = tint;
                label.alignment = TextAnchor.MiddleLeft;
                if (hidden)
                {
                    var strike = Ui.Panel(button.transform, "SeriesHidden", tint);
                    strike.raycastTarget = false;
                    strike.rectTransform.Anchor(0, .5f, 0, .5f, 6, -1, 6 + label.preferredWidth, 1);
                }
            }
            var graph = Ui.Rect(chartRow, "StockMarketChart");
            StockFlex(graph, 430);
            var visible = indices.Where(i => !_stockHiddenChartSeries.Contains(StockIds[i])).ToArray();
            var series = visible.Select(i => StockQuotesInRange(StockIds[i], _stockChartDays)).ToArray();
            var colors = visible.Select(i => StockChartColors[i % StockChartColors.Length]).ToArray();
            graph.gameObject.AddComponent<StockPriceGraphic>().Initialize(series, colors);
            if (series.All(quotes => quotes.Length == 0))
                StockText(body, series.Length == 0 ? "点左侧公司名称可显示曲线。" : "该时段暂无报价。", 15, Ui.Dim);
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
