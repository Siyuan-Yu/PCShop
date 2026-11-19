using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenFurnitureBuyback(Window mallWindow, Place mall)
        {
            var session = S;
            var drafts = new Dictionary<string, Dictionary<string, int>>();
            var order = session.FurnitureBuybacks.LastOrDefault(item => item.MallId == mall.Id);
            string pickup = order?.ShopId;
            bool restoreMallHold = mallWindow.Holds;

            void SetHold(Window window, bool hold)
            {
                if (window.Closed || window.Holds == hold) return;
                window.Holds = hold;
                if (hold) session.Time.Hold();
                else session.Time.Release();
            }

            if (order != null && restoreMallHold) SetHold(mallWindow, false);
            OpenWindow($"{mall.Name} · 家具回收", 1240, 720, w =>
            {
                if (session.CurrentPlace != mall) { w.Close(); return; }
                Line(w.Content, $"回收单价 · 家具标价 × {GameSession.FurnitureBuybackRate:P0}", 16, Ui.Accent);
                BuildStoreSplit(w.Content, out var goods, out var summary);

                var shops = session.MyShops;
                var selected = shops.FirstOrDefault(shop => shop.Place.Id == pickup) ?? shops.FirstOrDefault();
                pickup = selected?.Place.Id;
                if (pickup != null && !drafts.ContainsKey(pickup))
                    drafts.Add(pickup, new Dictionary<string, int>());
                var draft = pickup == null ? new Dictionary<string, int>() : drafts[pickup];

                var selector = Ui.Row(goods, 42, 8);
                Ui.Size(Ui.Label(selector, "取货门店", 17), 92, 42);
                if (order != null || shops.Count <= 1)
                    Ui.Size(Ui.Label(selector, order?.ShopName ?? (selected == null ? "暂无门店" : session.ShopTitle(selected)), 17), -1, 42, 1);
                else
                {
                    int index = Array.IndexOf(shops.ToArray(), selected);
                    var choice = Ui.Choice(selector, shops.Select(shop => session.ShopTitle(shop)), index, i =>
                    {
                        pickup = shops[i].Place.Id;
                        w.ResetScrollOnRebuild = true;
                        w.RequestRebuild();
                    });
                    Ui.Size(choice, -1, 42, 1).name = "FurnitureBuybackShop";
                }

                var stock = selected?.Locker.FurnitureLines
                    .Where(line => FurnitureCatalog.Get(line.Key) != null)
                    .OrderBy(line => line.Key, StringComparer.Ordinal).ToArray()
                    ?? Array.Empty<KeyValuePair<string, int>>();
                var heading = Ui.Row(goods, 32, 6);
                Ui.Size(Ui.Label(heading, "门店后仓", 19, color: Ui.Accent), -1, 32, 1);
                Ui.Size(Ui.Label(heading, $"可回收 {stock.Sum(line => line.Value)} 件", 15, TextAnchor.MiddleRight, Ui.Dim), 160, 32);
                var columns = Ui.Row(goods, 32, 8);
                Ui.Size(Ui.Label(columns, "家具", 15, color: Ui.Dim), -1, 32, 1);
                Ui.Size(Ui.Label(columns, "回收单价", 15, TextAnchor.MiddleRight, Ui.Dim), 96, 32);
                Ui.Size(Ui.Label(columns, "数量", 15, TextAnchor.MiddleCenter, Ui.Dim), 184, 32);

                var list = GoodsList(goods, 145);
                if (stock.Length == 0) Line(list, "这个后仓没有可回收的家具。", 17, Ui.Dim);
                foreach (var item in stock)
                {
                    var furniture = FurnitureCatalog.Get(item.Key);
                    draft.TryGetValue(furniture.Id, out int requested);
                    int quantity = order == null ? Math.Min(requested, item.Value) : 0;
                    if (order == null && requested != quantity)
                    {
                        if (quantity == 0) draft.Remove(furniture.Id);
                        else draft[furniture.Id] = quantity;
                    }
                    var row = Ui.Row(list, 62, 8);
                    CompactProduct(row, "furn:" + furniture.Id, furniture.Name, furniture.WeightKg, $"可回收 {item.Value} 件", false);
                    Ui.Size(Ui.Label(row, GameSession.FurnitureBuybackPrice(furniture).ToString(), 17, TextAnchor.MiddleRight, Ui.Good), 96, 62);
                    if (order != null)
                        Ui.Size(Ui.Label(row, "—", 16, TextAnchor.MiddleCenter, Ui.Dim), 184, 62);
                    else
                        QuantityStepper(w, row, "Buyback_" + furniture.Id, quantity, 0, item.Value, 1, next =>
                        {
                            if (next <= 0) draft.Remove(furniture.Id);
                            else draft[furniture.Id] = (int)next;
                        });
                }

                void RefreshSummary()
                {
                    w.RefreshContent = null;
                    Ui.Clear(summary);
                    string blocker = null;
                    var details = order ?? session.QuoteFurnitureBuyback(pickup, draft, out blocker);
                    bool sent = order != null;
                    bool delivered = order?.Delivered == true;
                    string title = delivered ? "回收已完成" : sent ? "等待回收" : "回收单";
                    var head = Ui.Row(summary, 36, 8);
                    Ui.Size(Ui.Label(head, title, 22, color: Ui.Accent), -1, 36, 1);
                    Ui.Size(Ui.Label(head, $"{details?.Count ?? 0} 件", 16, TextAnchor.MiddleRight, Ui.Dim), 78, 36);
                    string origin = details?.ShopName ?? (selected == null ? "请选择门店" : session.ShopTitle(selected) + "后仓");
                    if (details != null) origin += "后仓";
                    Line(summary, $"{origin} → {mall.Name}", 17);
                    var delivery = Line(summary, "", 15, Ui.Dim);
                    long displayedMinutes = -1;
                    void RefreshEta()
                    {
                        if (details == null) delivery.text = "选好家具后显示车数和预计到达时间";
                        else
                        {
                            long remaining = Math.Max(0, details.DueMinute - session.Clock.Now.TotalMinutes);
                            if (remaining == displayedMinutes) return;
                            displayedMinutes = remaining;
                            string eta = delivered ? "已送达" : $"约 {remaining} 分钟";
                            delivery.text = $"{details.Trucks} 辆面包车 · {details.Kg:0.##} 千克 · {eta}";
                        }
                    }
                    RefreshEta();
                    var items = GoodsList(summary, 65);
                    if (details == null) Line(items, "尚未选择家具", 16, Ui.Dim);
                    else
                        foreach (var line in details.Lines)
                            FurnitureBuybackAmount(items, $"{line.Name} ×{line.Count}", line.Amount.ToString());

                    FurnitureBuybackAmount(summary, delivered ? "回收款 · 已到账" : "回收款 · 到货到账", (details?.Payout ?? default).ToString());
                    FurnitureBuybackAmount(summary, sent ? "运费 · 已支付" : "运费 · 叫车时支付", (-(details?.Freight ?? default)).ToString(), Ui.Warn);
                    var net = details?.Net ?? default;
                    string netLabel = (delivered ? "实际" : "预计") + (net.IsNegative ? "净支出" : "净收入");
                    FurnitureBuybackAmount(summary, netLabel, net.Abs().ToString(), net.IsNegative ? Ui.Warn : Ui.Good, 25);

                    string action = delivered ? "继续回收" : sent ? "等待家具城收货" : "确认叫车回收";
                    var confirm = Ui.Button(summary, action, () =>
                    {
                        if (delivered)
                        {
                            order = null;
                            SetHold(w, true);
                            if (restoreMallHold) SetHold(mallWindow, true);
                        }
                        else if (order == null)
                        {
                            order = session.DispatchFurnitureBuyback(pickup, draft);
                            if (order != null)
                            {
                                draft.Clear();
                                SetHold(w, false);
                                if (restoreMallHold) SetHold(mallWindow, false);
                            }
                        }
                        w.RequestRebuild();
                    }, 19, Ui.ButtonHot, delivered || (!sent && details != null && blocker == null));
                    Ui.Size(confirm, -1, 46).name = "ConfirmFurnitureBuyback";
                    if (sent)
                    {
                        var status = Line(summary, delivered ? "家具城已收货，回收款已一次性发放。" : "", 15, Ui.Dim);
                        if (!delivered)
                        {
                            void RefreshTransit()
                            {
                                if (details.Delivered) { w.RequestRebuild(); return; }
                                RefreshEta();
                                status.text = !session.Time.IsRunning || session.OpeningClockFrozen
                                    ? "游戏时间已暂停，恢复后继续运输。"
                                    : "家具已出库，全部送达后结算。";
                            }
                            RefreshTransit();
                            w.RefreshContent = RefreshTransit;
                        }
                    }
                    else if (blocker != null && draft.Count > 0) Para(summary, blocker, 15, Ui.Warn);
                }

                RefreshSummary();
                w.RefreshQuantityContent = RefreshSummary;
            }, holds: order == null, onClosed: () =>
            {
                if (S != session || session.CurrentPlace != mall || mallWindow.Closed) return;
                if (restoreMallHold) SetHold(mallWindow, true);
                mallWindow.RequestRebuild();
            }, scrollContent: false);
        }

        private static void FurnitureBuybackAmount(Transform parent, string label, string value, Color? ink = null, int size = 17)
        {
            var row = Ui.Row(parent, Math.Max(26, size + 9), 8);
            Ui.Size(Ui.Label(row, label, 16, color: Ui.Dim), -1, -1, 1);
            var amount = Ui.Label(row, value, size, TextAnchor.MiddleRight, ink);
            Ui.Size(amount, 162, -1);
        }
    }
}
