using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static string StockSignedMoney(Money amount) => amount.IsPositive ? "+" + amount : amount.ToString();

        private void ExecuteStockDraft(Window w, bool shortMode, StockBatchQuote shownQuote)
        {
            if (w.Closed) return;
            var session = S;
            var draft = StockDraft(shortMode);
            var orders = draft.ToDictionary(pair => pair.Key, pair => pair.Value);
            var quote = session.PreviewStockBatch(orders, shortMode);
            string label = $"成交 {orders.Count} 笔";
            string issue = quote.Blocker;
            if (issue == null && (quote.Net != shownQuote.Net || quote.Fees != shownQuote.Fees || quote.CashAfter != shownQuote.CashAfter))
                issue = "报价或现金已更新，请重新核对";
            if (issue != null)
            {
                SetStockTradeFeedback(label, issue);
                w.Rebuild();
                return;
            }

            void Trade(bool acknowledgeReserve)
            {
                if (w.Closed || S != session) return;
                RunStockAction(label, () =>
                {
                    if (!session.TradeStockBatch(orders, shortMode, acknowledgeReserve)) return false;
                    draft.Clear();
                    return true;
                }, $" · 现金变动 {StockSignedMoney(quote.Net)} · 手续费 {quote.Fees}");
                w.Rebuild();
            }

            if (!quote.UsesReserve)
            {
                Trade(false);
                return;
            }

            var prices = orders.Keys.ToDictionary(id => id, session.CityInvestmentPrice);
            OpenWindow("成交会动用预留资金", 680, 360, confirmation =>
            {
                Line(confirmation.Content, label, 21, Ui.Accent);
                Para(confirmation.Content, $"现金净变动 {StockSignedMoney(quote.Net)}（手续费 {quote.Fees}），成交后现金 {quote.CashAfter}。", 17);
                Para(confirmation.Content, $"未来账单及资金保护额 {quote.Reserve}，成交后低于预留额 {quote.Reserve - quote.CashAfter}。", 17, Ui.Warn);
                var actions = Ui.Row(confirmation.Content, 44, 8);
                Ui.Size(Ui.Button(actions, "取消", confirmation.Close, 18), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "仍然成交", () =>
                {
                    if (confirmation.Closed || w.Closed || S != session) return;
                    confirmation.Close();
                    var current = session.PreviewStockBatch(orders, shortMode);
                    string blocker = current.Blocker;
                    if (blocker == null && (current.CashAfter != quote.CashAfter || current.Reserve != quote.Reserve
                        || prices.Any(pair => session.CityInvestmentPrice(pair.Key) != pair.Value)
                        || draft.Count != orders.Count || orders.Any(pair => !draft.TryGetValue(pair.Key, out long units) || units != pair.Value)))
                        blocker = "报价、现金或草稿已变化，请重新核对";
                    if (blocker == null) Trade(true);
                    else
                    {
                        SetStockTradeFeedback(label, blocker);
                        w.Rebuild();
                    }
                }, 18, Ui.Warn), -1, -1, 1).name = "StockReserveConfirm";
            });
        }

        private void OpenStockOrderTools(Window w, string id, bool shortMode)
        {
            var session = S;
            var draft = StockDraft(shortMode);
            string open = shortMode ? "做空" : "买入";
            string close = shortMode ? "平仓" : "卖出";
            string unit = id == "hardware" ? "份" : "股";
            OpenWindow(StockTitle(id) + " · 快捷交易", 620, 410, tools =>
            {
                long signed = draft.TryGetValue(id, out long current) ? current : 0;
                long held = StockHeld(id, shortMode);
                var others = draft.Where(pair => pair.Key != id).Aggregate(session.Ledger.Balance,
                    (cash, pair) => cash + session.StockOrderCashChange(pair.Key, pair.Value, shortMode));
                long max = session.StockMaximumOpeningQuantity(id, shortMode, others);
                void Apply(long quantity)
                {
                    if (tools.Closed || w.Closed || S != session) return;
                    SetStockDraft(id, shortMode, quantity);
                    tools.Close();
                    w.Rebuild();
                }

                void Quick(string text, string name, long quantity, bool enabled)
                {
                    var button = Ui.Size(Ui.Button(tools.Content, text, () => Apply(quantity), 17, Ui.ButtonBg, enabled), -1, 35);
                    button.name = name + id;
                }
                Quick($"最大{open} · {max:N0} {unit}", "StockQuickBuyMax_", max, max > 0);
                Quick($"全部{close} · {held:N0} {unit}", "StockQuickSellAll_", -held, held > 0 && held <= GameSession.StockOrderLimit);
                Quick("取消本股草稿", "StockDraftRemove_", 0, signed != 0);

                string input = (signed == 0 ? GameSession.StockLotSize : Math.Abs(signed)).ToString();
                var manual = Ui.Row(tools.Content, 38, 6);
                Ui.Size(Ui.Label(manual, id == "hardware" ? "指定份数" : "指定股数", 16), 96, 38);
                var field = Ui.TextInput(manual, input, "", value => input = value);
                field.contentType = InputField.ContentType.IntegerNumber;
                field.characterLimit = 7;
                Ui.Size(field, 116, 36);
                Text error = null;
                void Manual(bool opening)
                {
                    long position = StockHeld(id, shortMode);
                    error.text = !long.TryParse(input, out long units) || units < 1 || units > GameSession.StockOrderLimit
                        ? $"请输入 1–{GameSession.StockOrderLimit:N0} 的整数"
                        : !opening && units > position ? $"数量超过实际{(shortMode ? "做空股数" : "持股")}"
                        : "";
                    if (error.text.Length == 0) Apply(opening ? units : -units);
                }
                if (id != "hardware")
                    Ui.Size(Ui.Button(manual, "待" + open, () => Manual(true), 16, Ui.ButtonHot, CanOpenStockPosition(id, shortMode)), -1, 36, 1);
                Ui.Size(Ui.Button(manual, "待" + close, () => Manual(false), 16, Ui.ButtonBg, held > 0), -1, 36, 1);
                error = Ui.Label(tools.Content, "", 15, color: Ui.Warn);
                Ui.Size(error, -1, 25);
            }, holds: false);
        }

        private void OpenStockMargin(Window w, string id)
        {
            Money amount = Money.FromYuan(1000);
            OpenWindow("补充保证金", 600, 350, confirmation =>
            {
                Line(confirmation.Content, StockTitle(id), 21, Ui.Accent);
                var choices = Ui.Row(confirmation.Content, 38, 6);
                foreach (long yuan in new[] { 1000L, 10000L })
                {
                    var choice = Money.FromYuan(yuan);
                    Ui.Size(Ui.Button(choices, choice.ToString(), () => { amount = choice; confirmation.Rebuild(); },
                        16, Ui.SelectedButton(amount == choice)), -1, 38, 1);
                }
                Para(confirmation.Content, $"本次冻结 {amount} · 补充后可用现金 {S.Ledger.Balance - amount}", 17);
                bool hasPosition = S.StockState(id)?.ShortUnits > 0;
                bool affordable = S.Ledger.Balance >= amount;
                if (!hasPosition || !affordable)
                    Para(confirmation.Content, hasPosition ? "可用现金不足" : "这只股票已经没有做空持仓", 15, Ui.Warn);
                Ui.Size(Ui.Button(confirmation.Content, "确认补充保证金", () =>
                {
                    if (confirmation.Closed || w.Closed) return;
                    confirmation.Close();
                    RunStockAction($"{StockTitle(id)} · 补充保证金 {amount}", () => S.AddStockShortMargin(id, amount));
                    w.Rebuild();
                }, 18, Ui.ButtonHot, hasPosition && affordable), -1, 44);
            });
        }

        private void OpenStockFees()
        {
            OpenWindow("账户与费用明细", 680, 525, w =>
            {
                Line(w.Content, "已实现盈亏 " + S.StockTotalRealized + " · 冻结自有保证金 " + S.StockFrozenMargin, 18, Ui.Accent);
                Para(w.Content, "借股费每天按市值0.03%收取。冻结或释放保证金属于账户转账。", 15, Ui.Dim);
                var entries = S.Ledger.Entries.Where(e => StockIds.Contains(e.Attribution?.PropertyId)).Reverse().Take(12).ToArray();
                if (entries.Length == 0) Para(w.Content, "暂无证券流水。", 15, Ui.Dim);
                foreach (var entry in entries)
                    StockText(w.Content, $"{entry.Date} · {entry.Category}  {entry.Amount}", 15);
            });
        }

        private void SetStockTradeFeedback(string label, string failure)
        {
            _stockTradeFailed = failure != null;
            _stockTradeRevealPending = failure == null;
            _stockTradeFeedback = failure == null ? $"已完成 · {label}" : $"未完成 · {label} · {failure}";
        }

        private void RunStockAction(string label, Func<bool> trade, string receipt = null)
        {
            var session = S;
            string failure = null;
            void CaptureFailure(string message) => failure = message;
            session.Failed += CaptureFailure;
            bool ok;
            try { ok = trade(); }
            finally { session.Failed -= CaptureFailure; }
            SetStockTradeFeedback(ok ? label + receipt : label, ok ? null : failure ?? "当前条件已变化，请核对后重试");
        }
    }
}
