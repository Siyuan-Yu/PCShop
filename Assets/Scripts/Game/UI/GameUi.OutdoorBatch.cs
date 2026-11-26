using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Trade;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _outdoorPurchaseCount = 3;

        private void OpenOutdoorMarketVisit(CityActivity activity)
        {
            var definition = S.CityActivityService(activity);
            CloseAllWindows();
            var window = OpenWindow(definition.Name, 1040, 860, w =>
            {
                if (activity.State != CityActivityPhase.Running)
                {
                    Line(w.Content, GameSession.CityPhaseText(activity.State), 22, Ui.Accent);
                    Para(w.Content, activity.EndReason, 18);
                    if (!string.IsNullOrEmpty(activity.SettlementSummary))
                        Ui.Size(Ui.Button(w.Content, "查看活动结算", () => OpenCityActivitySettlement(activity.Id), 19, Ui.ButtonHot), -1, 44);
                    return;
                }

                var state = S.OutdoorActivityState(activity.Id);
                if (state == null) return;
                Line(w.Content, $"走访进度 {state.Visited}/3", 22, Ui.Accent);
                Line(w.Content, $"游戏时间暂停 · 完成后结算 {activity.Duration} 分钟", 18, Ui.Dim);
                BuildOutdoorBatchStatus(w, activity);
                BuildMarketVisitChoices(w, activity, definition, state);
                if (state.Pending == null && state.Visited > 0) BuildOutdoorBatchProcurement(w, activity);
                BuildManualActivityFinish(w, activity);
                Ui.Size(Ui.Button(w.Content, "提前离开", () => ConfirmEndCityActivity(activity, false), 18, Ui.ButtonDanger), -1, 44);
            }, fitContent: true);
            if (window == null) return;
            window.LiveConversation = true;
            window.LiveKey = () =>
            {
                if (window.HasFocusedInput()) return window.LiveStamp;
                var state = S.OutdoorActivityState(activity.Id);
                return $"{activity.State}:{state?.Pending}:{state?.Visited}";
            };
            window.LiveStamp = window.LiveKey();
        }

        private void BuildMarketVisitChoices(Window w, CityActivity activity, CityActivityDefinition definition, OutdoorBatchState state)
        {
            // The last choice points to the procurement form already shown below.
            for (int i = definition.ServiceCount; i < definition.Choices.Length - 1; i++)
            {
                int index = i;
                if (index == 1 && state.Visited >= 3) continue;
                bool available = state.Pending == null;
                if (IsActivityTopic(definition, index)) available &= !activity.Choices.Contains(index.ToString())
                    && ActivitySpeakers(activity, definition.Choices[index]).Length > 0;
                if (state.Number == 69 && index is >= 2 and <= 4)
                    available &= state.Visited >= 3 && state.Answers == 0 && state.QuoteCents.Length >= 2 && index - 2 < state.QuoteCents.Length;
                if (state.Number == 69 && index == 5) available &= !state.Negotiated;
                if (state.Number == 70 && index == 2) available &= !state.Tested;
                if (state.Number == 70 && index == 3) available &= state.Tested && !state.Negotiated;
                Ui.Size(Ui.Button(w.Content, S.EventActivityChoiceLabel(definition, index), () =>
                {
                    if (SelectCityActivityOption(activity, definition, index)) w.RequestRebuild();
                }, 18, Ui.ButtonBg, available), -1, 44).name = "CityActivityChoice_" + index;
            }
        }

        private void BuildOutdoorBatchStatus(Window w, CityActivity activity)
        {
            var state = S.OutdoorActivityState(activity.Id); if (state == null) return;
            Para(w.Content, S.OutdoorActivityStatus(activity.Id), 18, Ui.Accent);
            if (state.Number == 64)
                Line(w.Content, $"已看样株 {state.Visited}/3 · 已答 {state.Answers}/3 · 正确 {state.Correct}", 17, Ui.Dim);
            if (state.Number < 68) return;
            if (state.QuoteSources.Length > 0)
                for (int i = 0; i < state.QuoteSources.Length; i++)
                    Line(w.Content, $"报价 {i + 1} · {state.QuoteNames[i]} · ¥{state.QuoteCents[i] / 100.0:0.00}", 16);
        }

        private void BuildManualActivityFinish(Window w, CityActivity activity)
        {
            Ui.Size(Ui.Button(w.Content, $"结束并结算 · {activity.Duration} 分钟", () =>
            {
                if (S.FinishManualCityActivity(activity.Id)) CloseWindowSilently(w);
            }, 19, Ui.ButtonHot, S.CanFinishManualCityActivity(activity.Id)), -1, 46).name = "FinishManualCityActivity";
        }
        private void BuildOutdoorBatchProcurement(Window w, CityActivity activity)
        {
            var state = S.OutdoorActivityState(activity.Id); if (state == null || state.Number < 68) return;
            var parts = S.OutdoorMarketParts(activity.Id);
            if (parts.Length == 0) return;
            Line(w.Content, "实际采购 · 不下单不会扣款或生成商品", 19, Ui.Accent);
            int selected = Array.FindIndex(parts, p => p.Id == state.PartId);
            var product = Ui.Row(w.Content, 52);
            ItemPortrait(product, parts[Math.Max(0, selected)].Id, 48);
            Ui.Size(Ui.Choice(product, parts.Select(p => p.Name), Math.Max(0, selected), index =>
            { S.SelectOutdoorMarketPart(activity.Id, parts[index].Id); w.RequestRebuild(); }), 0, 42, 1);
            if (state.Number == 69 && state.QuoteSources.Length > 0)
                Ui.Size(Ui.Choice(w.Content, state.QuoteNames, state.PurchaseSource, index =>
                { S.SelectOutdoorPurchaseSource(activity.Id, index); w.RequestRebuild(); }), -1, 42);
            string source = state.Number == 69 && state.PurchaseSource < state.QuoteSources.Length ? state.QuoteSources[state.PurchaseSource] : null;
            int minimum = source == "direct:zhou" ? 3 : 1;
            _outdoorPurchaseCount = Math.Max(minimum, _outdoorPurchaseCount);
            var countRow = Ui.Row(w.Content, 38, 12);
            Ui.Size(Ui.Label(countRow, "数量", 18), 52);
            PurchaseQuantity(w, countRow, "outdoorPurchase", _outdoorPurchaseCount, minimum, 30, value => _outdoorPurchaseCount = (int)value);
            var price = S.OutdoorMarketGoodsPrice(activity.Id, _outdoorPurchaseCount);
            string delivery = source == Online1788.Shopping.Id ? "网购运费 " + Online1788.ShippingFee + "，按实际网购时间配送。"
                : source == "direct:zhou" ? "协议直供至少3件，次日10点送店。"
                : "有电脑店时走实际货运，运费按路线另计；无电脑店时放入随身载具。";
            Para(w.Content, "货款 " + price + "。" + delivery, 16, Ui.Dim);
            if (state.Number == 70) Para(w.Content, "先完成所选型号实测；切换型号需要重新实测。账期采购需先谈成，30%首款、70%尾款14天内结清，次日10点送店。", 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, "确认现款采购 · " + price, () =>
            {
                int count = _outdoorPurchaseCount;
                var total = S.OutdoorMarketGoodsPrice(activity.Id, count);
                ConfirmBusiness("确认实际采购", "所选型号 ×" + count + "，货款 " + total + "。" + delivery, () =>
                { S.OrderOutdoorMarketGoods(activity.Id, count); w.RequestRebuild(); });
            }, 18, Ui.ButtonHot, state.OrderCount == 0 && (state.Number != 70 || state.Tested)), -1, 42);
            if (state.Number == 70)
                Ui.Size(Ui.Button(w.Content, "确认账期采购 · 首款 " + price.Scale(.3), () =>
                {
                    int count = _outdoorPurchaseCount;
                    var total = S.OutdoorMarketGoodsPrice(activity.Id, count);
                    ConfirmBusiness("确认账期采购", "所选型号 ×" + count + "，货款 " + total + "；首款30%，尾款70%在14天内支付。", () =>
                    { S.OrderOutdoorMarketGoods(activity.Id, count, true); w.RequestRebuild(); });
                }, 18, Ui.ButtonHot, state.CreditGranted && state.Tested && state.OrderCount == 0), -1, 42);
        }
    }
}
