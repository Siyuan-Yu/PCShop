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
        private string[] StockIds => S.CityEquities.Select(d => d.Id).Append("hardware").ToArray();
        private static readonly Color[] StockChartColors =
        {
            new Color32(151,96,31,255), new Color32(42,111,101,255), new Color32(114,78,147,255),
            new Color32(165,65,64,255), new Color32(56,97,151,255), new Color32(105,107,42,255),
            new Color32(161,82,46,255), new Color32(48,116,69,255), new Color32(147,71,117,255),
            new Color32(35,113,132,255), new Color32(81,91,165,255), new Color32(82,92,94,255)
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

        private string StockTitle(string id) => id == "hardware" ? "硬件景气指数" : S.StockDefinition(id)?.Name ?? id;
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
            var card = Ui.Size(Ui.FlatPanel(parent, "StockCashCard", Ui.FlatWell, Ui.FlatWellEdge),
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
            content.GetComponent<VerticalLayoutGroup>().spacing = 3;
            return content;
        }
        private Money StockUnrealized => S.CityEquityValue - S.CityEquityCost + S.FundValue
            - Money.FromCents(S.CityFundCostCents) + S.StockShortUnrealized;

        private void BuildStockWorkspace(Window w)
        {
            var root = StockRoot(w);
            var summary = Ui.Row(root, _stockEmbedded ? 48 : StockCashCardHeight, 12);
            var floating = StockUnrealized;
            var realized = S.StockTotalRealized;
            Text cash = null;
            if (!_stockEmbedded) (cash, _) = StockCashCard(summary, "StockAvailableCash", "可用现金", 260);
            var balance = S.Ledger.Balance;
            if (cash != null) { cash.text = balance.ToString(); cash.color = balance.IsNegative ? Ui.FlatDown : Ui.Accent; }
            StockFlex(Ui.Label(summary, "持仓市值\n" + (S.CityEquityValue + S.FundValue), 17), 48);
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
                }, 16, Ui.ButtonSecondary), 124, 36);
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
            // 页签固定在摘要下方，不移到底部。
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
                }, 17, page == _stockPage ? Ui.Chrome : Ui.ButtonBg);
                button.name = "StockPage_" + page;
                Ui.Size(button, 132, 36);
                if (page == StockPage.Exchange) exchange = button.GetComponentInChildren<Text>();
            }
            void Refresh()
            {
                int count = _stockSpotDraft.Count + _stockShortDraft.Count;
                exchange.text = count == 0 ? "交易所" : $"交易所 · {count}";
            }
            WbFlex(navigation);
            if (_stockPage == StockPage.Exchange)
            {
                foreach (bool mode in new[] { false, true })
                {
                    bool chosen = mode;
                    Ui.Size(Ui.Button(navigation, mode ? "做空" : "买入", () =>
                    { _stockShortMode = chosen; _stockTradeFeedback = null; w.Rebuild(); }, 16,
                        mode == _stockShortMode ? Ui.Chrome : Ui.ButtonBg), 80, 34).name = "StockMode_" + (mode ? "short" : "spot");
                }
                Ui.Size(Ui.Button(navigation, _stockWatchOnly ? "只看自选 ✓" : "只看自选", () =>
                { _stockWatchOnly = !_stockWatchOnly; w.Rebuild(); }, 15), 110, 34);
                Ui.Size(Ui.Button(navigation, "?", () => { _stockHelp = !_stockHelp; w.Rebuild(); }, 18), 34, 34).Tip("交易规则");
            }
            Refresh();
            return (navigation, Refresh);
        }

        private void BuildStockExchange(Window w, Action refreshNavigation)
        {
            bool shortMode = _stockShortMode;
            var draft = StockDraft(shortMode);
            var root = StockRoot(w);
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
                net.color = quote.Net.IsNegative ? Ui.ChromeWarn : quote.Net.IsPositive ? Ui.ChromeGood : Ui.ChromeInk;
                fees.text = (draft.ContainsKey("hardware") ? "佣金 0.2% · 旧指数 0.5%" : "佣金 0.2%")
                    + (draft.Count > 0 ? " · " + quote.Fees : "");
                bool ready = quote.Blocker == null;
                string summaryText = draft.Count > 2
                    ? $"待{(shortMode ? "做空" : "买入")} {draft.Count(p => p.Value > 0)} 笔 · {draft.Where(p => p.Value > 0).Sum(p => p.Value):N0} 股 / 待{(shortMode ? "平仓" : "卖出")} {draft.Count(p => p.Value < 0)} 笔 · {draft.Where(p => p.Value < 0).Sum(p => -p.Value):N0} 股"
                    : string.Join(" · ", draft.Select(pair => (pair.Value > 0 ? "待" + (shortMode ? "做空" : "买入") : "待" + (shortMode ? "平仓" : "卖出")) + " " + StockTitle(pair.Key) + " " + Math.Abs(pair.Value).ToString("N0") + " 股"));
                status.text = _stockTradeFeedback ?? (ready
                    ? (draft.Count == 0 ? "暂无待成交订单" : summaryText)
                    : quote.Blocker);
                status.color = _stockTradeFeedback != null ? (_stockTradeFailed ? Ui.ChromeWarn : Ui.ChromeGood)
                    : !ready && draft.Count > 0 ? Ui.ChromeWarn : Ui.ChromeInk;
                clear.interactable = draft.Count > 0;
                execute.interactable = ready;
            }

            var workspace = Ui.Row(root, -1, 14);
            Ui.Size(workspace, -1, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;
            var table = Ui.Rect(workspace, "StockQuoteTable"); Ui.VList(table, 3, 0);
            Ui.Size(table, 0, -1, 1.75f);
            var detail = Ui.FlatPanel(workspace, "StockDetailPane", Ui.ButtonBg).rectTransform;
            Ui.Size(detail, 0, -1, 1); Ui.VList(detail, 6, 10);
            var heading = Ui.Row(table, 32, 4);
            StockFlex(Ui.Label(heading, "公司", 14, color: Ui.Dim), 32);
            void Sort(int column) { _stockSort = _stockSort == column ? 0 : column; w.Rebuild(); }
            Ui.Size(Ui.Button(heading, "股价", () => Sort(1), 14), 86, 30);
            Text stepLabel = null;
            string StepLabel() => "×" + StockStep + " 股";
            var cycle = Ui.Size(Ui.Button(heading, StepLabel(), () =>
            { _stockLotStep = (_stockLotStep + 1) % StockLotSteps.Length; stepLabel.text = StepLabel(); }, 14), 68, 30);
            cycle.name = "StockLotCycle"; cycle.Tip(() => $"每次加减 {StockStep:N0} 股 · 点击切换");
            stepLabel = cycle.GetComponentInChildren<Text>();
            Ui.Size(Ui.Button(heading, "涨跌", () => Sort(2), 14), 78, 30);
            StockCell(heading, shortMode ? "做空股数" : "我的持股", 76, Ui.Dim);
            StockCell(heading, "持股比例", 80, Ui.Dim);
            StockCell(heading, shortMode ? "开仓均价" : "成本价", 86, Ui.Dim);
            StockCell(heading, "待成交", 174, Ui.Dim);
            var list = StockScroll(table, "StockQuoteScroll");
            var ids = StockIds.Where(id => (!_stockWatchOnly || S.StockWatched(id) || draft.ContainsKey(id))
                && (id != "hardware" || !shortMode && (S.FundUnits > 0 || draft.ContainsKey(id))));
            if (_stockSort == 1) ids = ids.OrderByDescending(id => S.CityInvestmentPrice(id).Cents);
            if (_stockSort == 2) ids = ids.OrderByDescending(S.StockDayChange);
            var visibleIds = ids.ToArray();
            if (!visibleIds.Contains(_stockExpanded)) _stockExpanded = visibleIds.FirstOrDefault();
            foreach (string id in visibleIds) refreshRows.Add(BuildStockRow(w, list, id, shortMode, Refresh));
            if (refreshRows.Count == 0) StockText(list, "暂无自选股票。", 16, Ui.Dim);
            if (_stockExpanded != null)
            {
                string id = _stockExpanded; long held = StockHeld(id, shortMode);
                long basis = shortMode ? S.StockState(id)?.ShortBasisCents ?? 0 : S.CityInvestmentCost(id).Cents;
                double? cost = held > 0 ? basis / (double)held : (double?)null;
                BuildStockDetails(w, detail, id, held, basis, cost, cost.HasValue ? "¥" + (cost.Value / 100).ToString("N2") : "—");
            }
            var footer = Ui.Row(root, StockCashCardHeight, 12);
            var footerImage = footer.gameObject.AddComponent<Image>(); footerImage.color = Ui.Chrome;
            footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(12,12,6,6);
            var netCard = Ui.Rect(footer, "StockNetCard"); Ui.Size(netCard, 280, -1); Ui.VList(netCard, 0, 0);
            Ui.Size(Ui.Label(netCard, "现金净变动", 14, color: Ui.ChromeDim), -1, 18);
            net = Ui.Size(Ui.Label(netCard, "", 23, color: Ui.ChromeInk), -1, 28); net.name = "StockBatchNet";
            fees = Ui.Size(Ui.Label(netCard, "", 12, color: Ui.ChromeDim), -1, 17);
            status = Ui.Label(footer, "", 16, TextAnchor.MiddleLeft, Ui.ChromeInk);
            status.name = "StockTradeFeedback"; StockFlex(status, 54);
            clear = Ui.Size(Ui.Button(footer, "清空", () =>
            { draft.Clear(); _stockTradeFeedback = null; if (_stockWatchOnly) w.Rebuild(); else Refresh(); }, 16, Ui.ButtonSecondary), 92, 42);
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

        private static string StockRatioText(double ratio) => ratio > 0 && ratio < .0001 ? "<0.01%" : ratio == 0 ? "0%" : ratio.ToString("P2");

        private Action BuildStockRow(Window w, Transform list, string id, bool shortMode, Action refresh)
        {
            bool selected = _stockExpanded == id;
            long held = StockHeld(id, shortMode);
            long basis = shortMode ? S.StockState(id)?.ShortBasisCents ?? 0 : S.CityInvestmentCost(id).Cents;
            string cost = held > 0 ? "¥" + (basis / (double)held / 100).ToString("N2") : "—";
            var row = Ui.Row(list, _stockEmbedded ? 44 : 46, 4); row.name = "StockQuote_" + id;
            var bg = row.gameObject.AddComponent<Image>(); bg.color = selected ? Ui.ButtonBg : Color.clear; bg.raycastTarget = false;
            if (selected) Ui.FlatBorder(bg, Ui.SelectionEdge, 1.5f); else WbDivider(row);
            var company = Ui.Row(row, 43, 2); Ui.Size(company, 0, 43, 1);
            Ui.Size(Ui.Button(company, S.StockWatched(id) ? "★" : "☆", () => { S.ToggleStockWatch(id); w.Rebuild(); }, 17), 27, 32);
            var name = Ui.Size(Ui.Button(company, "", () => { _stockExpanded = id; _stockCandles = false; w.Rebuild(); }, 15, Color.clear), 0, 43, 1);
            name.name = "StockExpand_" + id;
            var nameText = name.GetComponentInChildren<Text>();
            nameText.text = "<b>" + StockTitle(id) + "</b>\n<size=11><color=#615951>" + (S.StockDefinition(id)?.Industry ?? "指数") + "</color></size>";
            nameText.alignment = TextAnchor.MiddleLeft;
            StockCell(row, S.CityInvestmentPrice(id).ToString(), 86);
            Ui.Size(Ui.Rect(row, "StockStepSpacer"), 68, 32);
            double change = S.StockDayChange(id);
            var changeSlot = Ui.Size(Ui.Rect(row,"StockChangeSlot"),78,43,0);
            var pill = Ui.FlatPanel(changeSlot, "StockChange_" + id, Ui.ChangeColor(change), Ui.ChangeColor(change));
            pill.rectTransform.Anchor(0,.5f,1,.5f,0,-13,0,-13);
            var changeText = Ui.Label(pill.transform, change.ToString("+0.00%;-0.00%;0.00%"), 14, TextAnchor.MiddleCenter, Color.white);
            changeText.rectTransform.Fill(); changeText.raycastTarget = false;
            pill.Tip("较上一交易日收盘价的涨跌幅。对比价格：" + Money.FromCents(S.StockPreviousClose(id)));
            long founder = !shortMode ? S.CompanyFounderHolding(S.ListedCompanyOf(id)) : 0;
            var holding = StockCell(row, (held + founder).ToString("N0"), 76);
            if (founder > 0) holding.Tip($"创始持股 {founder:N0} 股 · 可交易持股 {held:N0} 股。加减与成本价按可交易持仓计算。");
            StockCell(row, shortMode || id == "hardware" ? "—" : StockRatioText(S.StockOwnershipRatio(id)), 80, shortMode ? Ui.Dim : Ui.TextColor);
            StockCell(row, cost, 86, held > 0 ? Ui.TextColor : Ui.Dim);
            var draftRow = Ui.Row(row, 35, 2); Ui.Size(draftRow, 174, 35);
            draftRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var minus = Ui.Size(Ui.Button(draftRow, "−", () => StepStockDraft(id, shortMode, -1, refresh), 20), 29, 31);
            minus.name = "StockDraftMinus_" + id;
            var box = Ui.FlatPanel(draftRow, "StockDraftBox", Ui.ButtonBg); Ui.Size(box, 74, 31);
            var pending = Ui.Label(box.transform, "—", 15, TextAnchor.MiddleCenter, Ui.Dim); pending.rectTransform.Fill(); pending.name = "StockDraft_" + id;
            var plus = Ui.Size(Ui.Button(draftRow, "+", () => StepStockDraft(id, shortMode, 1, refresh), 20), 29, 31);
            plus.name = "StockDraftPlus_" + id;
            foreach (var button in new[] { plus, minus })
            { var repeat = button.GetComponent<HoldRepeat>(); repeat.Owner = w; repeat.Key = (shortMode ? "short/" : "spot/") + button.name; }
            plus.Tip(() => $"每次增加 {StockStep:N0} 股"); minus.Tip(() => $"每次减少 {StockStep:N0} 股");
            Ui.Size(Ui.Button(draftRow, "…", () => OpenStockOrderTools(w, id, shortMode), 19), 34, 31).name = "StockDraftMore_" + id;
            return () =>
            {
                long units = StockDraft(shortMode).TryGetValue(id, out long value) ? value : 0;
                pending.text = units == 0 ? "—" : (units > 0 ? "+" : "−") + Math.Abs(units).ToString("N0");
                pending.color = units == 0 ? Ui.Dim : Ui.Accent; pending.fontStyle = units == 0 ? FontStyle.Normal : FontStyle.Bold;
                plus.interactable = units < 0 || CanOpenStockPosition(id, shortMode) && units < GameSession.StockOrderLimit;
                minus.interactable = units > -Math.Min(held, GameSession.StockOrderLimit);
            };
        }

        private void BuildStockDetails(Window w, Transform parent, string id, long units, long basis, double? cost, string costText)
        {
            var card = Ui.Rect(parent, "StockDetails_" + id); Ui.VList(card, 8, 0);
            Ui.Size(card, 0, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;
            var heading = Ui.Row(card, 42, 6);
            StockFlex(Bold(Ui.Label(heading, StockTitle(id) + " · " + (S.StockDefinition(id)?.Industry ?? "指数"), 20)), 42);
            Ui.Size(Ui.Button(heading, S.StockWatched(id) ? "★" : "☆", () => { S.ToggleStockWatch(id); w.Rebuild(); }, 20), 36, 32);
            if(id!="hardware")Ui.Size(Ui.Button(heading,"股权与治理",()=>OpenCompanyGovernance(id),14,Ui.ButtonSecondary),120,32);
            var switches = Ui.Row(card, 30, 6);
            foreach (bool candles in new[] { false, true })
            { bool pick = candles; Ui.Size(Ui.Button(switches, candles ? "K线" : "折线", () => { _stockCandles = pick; w.Rebuild(); }, 14, candles == _stockCandles ? Ui.Chrome : Ui.ButtonBg), 62, 28); }
            WbFlex(switches);
            var executions = S.StockExecutions(id, StockRangeStart(7), _stockShortMode);
            var graph = Ui.Rect(card, "StockPriceLine"); var graphLayout = Ui.Size(graph).GetComponent<LayoutElement>();
            graphLayout.minHeight = 190; graphLayout.flexibleHeight = 1;
            graph.gameObject.AddComponent<StockPriceGraphic>().Initialize(StockQuotesInRange(id, 7), cost, _stockCandles, executions);
            if (S.StockDefinition(id) is CityEquityDefinition company)
                Ui.Size(Ui.Label(card, company.Description, 13, TextAnchor.UpperLeft, Ui.Dim), -1, 40);
            var stock = S.StockState(id);
            Ui.Size(Ui.Label(card, "最近每股分红 " + Money.FromCents(stock?.LastDividendCents ?? 0), 13, color: Ui.Dim), -1, 22);
            long value = S.CityInvestmentPrice(id).Cents * units;
            var profit = Money.FromCents(_stockShortMode ? basis - value : value - basis);
            var figures = Ui.Row(card, 66, 7);
            WbStripCard(figures, _stockShortMode ? "借股金额" : "持仓成本", units > 0 ? Money.FromCents(basis).ToString() : "—");
            WbStripCard(figures, "浮动盈亏", units > 0 ? profit.ToString() : "—", profit.IsNegative ? Ui.Warn : Ui.Good);
            WbStripCard(figures, "昨日营收", stock?.YesterdayRevenueCents > 0 ? Money.FromCents(stock.YesterdayRevenueCents).ToString() : "—");
            Ui.Size(Ui.Button(card, "成交明细", () => OpenStockTradeHistory(id, executions), 15, Ui.ButtonSecondary), -1, 32);
            if (_stockShortMode && units > 0)
            {
                Ui.Size(Ui.Label(card, "担保比例 " + S.StockMarginRatio(id).ToString("P0") + " · 冻结 " + Money.FromCents(stock.ShortCollateralCents), 14, color: S.StockMarginRatio(id) < 1.5 ? Ui.Warn : Ui.Dim), -1, 25);
                Ui.Size(Ui.Button(card, "补充保证金", () => OpenStockMargin(w, id), 15), -1, 32);
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

            const int legendRowHeight = 34, legendSpacing = 3;
            int chartHeight = Math.Max(430, StockIds.Length * (legendRowHeight + legendSpacing) - legendSpacing);
            var chartRow = Ui.Row(body, chartHeight, 12);
            var legend = Ui.Rect(chartRow, "StockChartLegend");
            Ui.Size(legend, 264, chartHeight, 0);
            Ui.VList(legend, legendSpacing, 0);
            var indices = Enumerable.Range(0, StockIds.Length).ToArray();
            foreach (int i in indices)
            {
                string id = StockIds[i];
                bool hidden = _stockHiddenChartSeries.Contains(id);
                Color tint = hidden ? Ui.Dim : StockChartColors[i % StockChartColors.Length];
                var item = Ui.Row(legend, legendRowHeight, 5);
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
            StockFlex(graph, chartHeight);
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
