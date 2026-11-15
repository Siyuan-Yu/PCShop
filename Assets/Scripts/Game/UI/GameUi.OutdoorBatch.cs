using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _outdoorPurchaseCount = 3;

        private void OpenOutdoorMarketVisit(CityActivity activity)
        {
            var definition = S.CityActivityService(activity);
            CloseAllWindows();
            S.Time.SetPaused(false);
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
                var time = Line(w.Content, "", 18, Ui.Dim);
                var status = BuildOutdoorBatchStatus(w, activity, keepOpen: true);
                long lastMinute = -1;
                void RefreshStatus()
                {
                    long now = S.Clock.Now.TotalMinutes;
                    if (now == lastMinute) return;
                    lastMinute = now;
                    time.text = $"活动剩余 {Math.Max(0, activity.ActualStart + activity.Duration - now)} 游戏分钟";
                    status.text = S.OutdoorActivityStatus(activity.Id, keepOpen: true);
                }
                w.RefreshContent = RefreshStatus;
                RefreshStatus();
                BuildMarketVisitChoices(w, activity, definition, state);
                if (state.Pending == null && state.Visited > 0) BuildOutdoorBatchProcurement(w, activity);
                Ui.Size(Ui.Button(w.Content, "提前离开", () =>
                    ConfirmBusiness("结束本次走访", "保留已经发生的交易和阶段效果。", () =>
                    {
                        S.CancelCityActivity(activity.Id, true);
                        w.RequestRebuild();
                    }), 18, Ui.Warn), -1, 44);
            }, holds: false);
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
                if (state.Number == 69 && index is >= 2 and <= 4)
                    available &= state.Visited >= 3 && state.Answers == 0 && state.QuoteCents.Length >= 2 && index - 2 < state.QuoteCents.Length;
                if (state.Number == 69 && index == 5) available &= !state.Negotiated;
                if (state.Number == 70 && index == 2) available &= !state.Tested;
                if (state.Number == 70 && index == 3) available &= state.Tested && !state.Negotiated;
                Ui.Size(Ui.Button(w.Content, definition.Choices[index], () =>
                {
                    S.ChooseCityActivity(activity.Id, index);
                    w.RequestRebuild();
                }, 18, Ui.ButtonHot, available), -1, 44).name = "CityActivityChoice_" + index;
            }
        }

        private Text BuildOutdoorBatchStatus(Window w, CityActivity activity, bool keepOpen = false)
        {
            var state = S.OutdoorActivityState(activity.Id); if (state == null) return null;
            var status = Para(w.Content, S.OutdoorActivityStatus(activity.Id, keepOpen), 18, Ui.Accent);
            if (state.Number == 64)
                Line(w.Content, $"已看样株 {state.Visited}/3 · 已答 {state.Answers}/3 · 正确 {state.Correct}", 17, Ui.Dim);
            if (state.Number < 68) return status;
            if (state.QuoteSources.Length > 0)
                for (int i = 0; i < state.QuoteSources.Length; i++)
                    Line(w.Content, $"报价 {i + 1} · {state.QuoteNames[i]} · ¥{state.QuoteCents[i] / 100.0:0.00}", 16);
            return status;
        }
        private void BuildOutdoorBatchProcurement(Window w, CityActivity activity)
        {
            var state = S.OutdoorActivityState(activity.Id); if (state == null || state.Number < 68) return;
            var parts = S.OutdoorMarketParts(activity.Id);
            if (parts.Length == 0) return;
            Line(w.Content, "实际采购 · 不下单不会扣款或生成商品", 19, Ui.Accent);
            int selected = Array.FindIndex(parts, p => p.Id == state.PartId);
            Ui.Size(Ui.Choice(w.Content, parts.Select(p => p.Name), Math.Max(0, selected), index =>
            { S.SelectOutdoorMarketPart(activity.Id, parts[index].Id); w.RequestRebuild(); }), -1, 42);
            if (state.Number == 69 && state.QuoteSources.Length > 0)
                Ui.Size(Ui.Choice(w.Content, state.QuoteNames, state.PurchaseSource, index =>
                { S.SelectOutdoorPurchaseSource(activity.Id, index); w.RequestRebuild(); }), -1, 42);
            string source = state.Number == 69 && state.PurchaseSource < state.QuoteSources.Length ? state.QuoteSources[state.PurchaseSource] : null;
            int minimum = source == "direct:zhou" ? 3 : 1;
            _outdoorPurchaseCount = Math.Max(minimum, _outdoorPurchaseCount);
            var countRow = Ui.Row(w.Content, 38, 12);
            Ui.Size(Ui.Label(countRow, "数量", 18), 52);
            QuantityStepper(w, countRow, "outdoorPurchase", _outdoorPurchaseCount, minimum, 30, 1, value => _outdoorPurchaseCount = (int)value);
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
