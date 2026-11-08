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
        private int _stockTab,_stockAction,_stockChartDays=30;
        private string _stockTarget;
        private bool _stockHelp;
        private static readonly string[] StockActions={"买入","卖出","借股做空","买回平仓"};
        private static readonly string[] StockActionKeys={"buy","sell","short","cover"};
        private static int StockOrderStep(string id) => id == "hardware" ? 1 : GameSession.StockLotSize;
        private long StockMaxQuantity(string id, int action)
        {
            int step = StockOrderStep(id);
            long max = Math.Min(StockQuantityLimit, S.StockAvailable(id));
            if (action == 1 || action == 3)
            {
                long held = action == 1 ? S.CityInvestmentUnits(id) : S.StockState(id)?.ShortUnits ?? 0;
                return held <= max ? held : max / step * step;
            }
            if (action == 2) max = Math.Min(max, S.StockBorrowAvailable(id));
            long low = 0, high = max / step;
            while (low < high)
            {
                long lots = (low + high + 1) / 2;
                long shares = lots * step;
                var cost = action == 2 ? S.StockShortOpeningCost(id, shares) : S.CityInvestmentTradeTotal(id, shares, true);
                if (cost <= S.Ledger.Balance) low = lots;
                else high = lots - 1;
            }
            return low * step;
        }
        private string StockTradeSummary(string id,long quantity,int action)
        {
            var gross=S.CityInvestmentPrice(id)*quantity;
            if(action==2)return "冻结自有保证金 "+gross+" + 手续费 "+CityInvestments.Fee(gross)+"；卖出所得 "+gross+"另行冻结";
            if(action==3)return "买回费用 "+(gross+CityInvestments.Fee(gross))+"（从冻结资金扣除），预计释放到账 "+S.StockCoverReturn(id,quantity);
            return (action==0?"支付 ":"到手 ")+S.CityInvestmentTradeTotal(id,quantity,action==0)+"（已含手续费）";
        }
    }
    internal static class StockUiSequence
    {
        internal static StockQuote[] TakeLastCompatQuotes(this StockQuote[] values,int count) => values.Skip(Math.Max(0,values.Length-count)).ToArray();
    }
}
