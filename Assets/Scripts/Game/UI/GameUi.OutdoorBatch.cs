using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Trade;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _outdoorPurchaseCount = 3;
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
            var countRow = Ui.Row(w.Content, 38, 12);
            Ui.Size(Ui.Button(countRow, "−", () => { _outdoorPurchaseCount = Math.Max(1, _outdoorPurchaseCount - 1); w.Rebuild(); }), 48, 38);
            Ui.Size(Ui.Label(countRow, "数量 " + _outdoorPurchaseCount, 18), -1, 38, 1);
            Ui.Size(Ui.Button(countRow, "+", () => { _outdoorPurchaseCount = Math.Min(30, _outdoorPurchaseCount + 1); w.Rebuild(); }), 48, 38);
            var price = S.OutdoorMarketGoodsPrice(activity.Id, _outdoorPurchaseCount);
            string source = state.Number == 69 && state.PurchaseSource < state.QuoteSources.Length ? state.QuoteSources[state.PurchaseSource] : null;
            string delivery = source == Online1788.Shopping.Id ? "网购运费 " + Online1788.ShippingFee + "，按实际网购时间配送。"
                : source == "direct:zhou" ? "协议直供至少3件，次日10点送店。"
                : "有电脑店时走实际货运，运费按路线另计；无电脑店时放入随身载具。";
            Para(w.Content, "货款 " + price + "。" + delivery, 16, Ui.Dim);
            if (state.Number == 70) Para(w.Content, "先完成所选型号实测；切换型号需要重新实测。账期采购需先谈成，30%首款、70%尾款14天内结清，次日10点送店。", 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, "确认现款采购 · " + price, () =>
            {
                ConfirmBusiness("确认实际采购", "所选型号 ×" + _outdoorPurchaseCount + "，货款 " + price + "。" + delivery, () =>
                { S.OrderOutdoorMarketGoods(activity.Id, _outdoorPurchaseCount); w.RequestRebuild(); });
            }, 18, Ui.ButtonHot, state.OrderCount == 0), -1, 42);
            if (state.Number == 70)
                Ui.Size(Ui.Button(w.Content, "确认账期采购 · 首款 " + price.Scale(.3), () =>
                {
                    ConfirmBusiness("确认账期采购", "所选型号 ×" + _outdoorPurchaseCount + "，货款 " + price + "；首款30%，尾款70%在14天内支付。", () =>
                    { S.OrderOutdoorMarketGoods(activity.Id, _outdoorPurchaseCount, true); w.RequestRebuild(); });
                }, 18, Ui.ButtonHot, state.CreditGranted && state.OrderCount == 0), -1, 42);
        }
    }
}
