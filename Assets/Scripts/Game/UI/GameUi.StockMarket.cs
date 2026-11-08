using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static readonly string[] StockActions = { "买入", "卖出", "借股做空", "买回平仓" };
        private static readonly string[] StockActionKeys = { "buy", "sell", "short", "cover" };

        private void TradeStockQuote(Window w, string id, long quantity, int action)
        {
            if (w.Closed) return;
            string label = $"{StockTitle(id)} · {StockActions[action]} {quantity:N0}{(id == "hardware" ? "份" : "股")}";
            string blocker = S.StockTradeBlocker(id, quantity, StockActionKeys[action]);
            if (blocker != null)
            {
                SetStockTradeFeedback(label, blocker);
                w.Rebuild();
                return;
            }

            void Trade(bool acknowledgeReserve)
            {
                Money gross = S.CityInvestmentPrice(id) * quantity;
                Money fee = CityInvestments.Fee(gross, id == "hardware");
                RunStockAction(label, () => action switch
                {
                    2 => S.OpenStockShort(id, quantity, acknowledgeReserve),
                    3 => S.CoverStockShort(id, quantity),
                    _ => S.TradeCityInvestment(id, quantity, action == 0, acknowledgeReserve)
                }, $" · 成交额 {gross} · 手续费 {fee}");
                w.Rebuild();
            }

            if (action == 1 || action == 3)
            {
                Trade(false);
                return;
            }
            var reserve = S.GetCityCashReserve();
            Money cost = action == 2 ? S.StockShortOpeningCost(id, quantity) : S.CityInvestmentTradeTotal(id, quantity, true);
            Money remaining = reserve.Balance - cost;
            if (remaining >= reserve.Total)
            {
                Trade(false);
                return;
            }

            long price = S.CityInvestmentPrice(id).Cents;
            string title = StockActions[action] + "会动用预留资金";
            OpenWindow(title, 640, action == 2 ? 445 : 390, confirmation =>
            {
                Line(confirmation.Content, label, 21, Ui.Accent);
                Para(confirmation.Content, $"本次支付 {cost}（含手续费），成交后现金剩 {remaining}。", 18);
                Para(confirmation.Content, $"未来 {reserve.Days} 天账单、预计支出及资金保护额共 {reserve.Total}。成交后将低于预留额 {reserve.Total - remaining}。", 18, Ui.Warn);
                if (action == 2)
                    Para(confirmation.Content, "自有保证金与卖出所得均会冻结，借股费按日结算。", 16, Ui.Dim);
                Para(confirmation.Content, "当前尚未成交，也未扣款。", 16, Ui.Dim);
                var actions = Ui.Row(confirmation.Content, 44, 8);
                Ui.Size(Ui.Button(actions, "取消", confirmation.Close, 18), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "仍然" + StockActions[action], () =>
                {
                    if (confirmation.Closed || w.Closed) return;
                    confirmation.Close();
                    if (S.CityInvestmentPrice(id).Cents == price) Trade(true);
                    else
                    {
                        SetStockTradeFeedback(label, "报价已变化，请重新下单");
                        w.Rebuild();
                    }
                }, 18, Ui.Warn), -1, -1, 1).name = "StockReserveConfirm";
            });
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
                        16, amount == choice ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
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
