using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>统一网购的电脑配件分类；保留旧调用入口以兼容任务导航。</summary>
    public sealed partial class GameUi
    {

        private readonly Dictionary<string, int> _cart = new Dictionary<string, int>();

        public void OpenWholesaleApp(bool restockPage = false)
        {
            if (restockPage) { OpenCityProcurement(); return; }
            _onlineTab = 5;
            OpenOnlinePanel();
        }

        private void BuildOnline(Window w, OwnedShop destination)
        {
            Transform c = w.Content;
            OnlineStore s = Online1788.Shopping;
            BuildStoreSplit(c, out var goods, out var side);
            var splitSize = goods.parent.GetComponent<LayoutElement>();
            splitSize.flexibleHeight = 0; splitSize.minHeight = splitSize.preferredHeight = 640;

            RectTransform list = GoodsList(goods, 200);
            Money total = Online1788.ShippingFee;
            double space = 0;
            var picked = new List<(PartDef part, int count, Money price)>();
            foreach (PartDef p in PartCatalog.All.Where(p=>PCShopSell.Core.Home.IndustryCycles.Available(p,S.Clock.Today.TotalDays)))
            {
                PartDef part = p;
                Money price = S.OnlinePrice(s, p);
                _cart.TryGetValue(p.Id, out int n);
                total += price * n;
                space += Stock.SpaceOf(p, true, n);
                if (n > 0) picked.Add((part, n, price));
                RectTransform row = Ui.Row(list, 56, 10);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                if (n > 0) row.gameObject.AddComponent<Image>().color = Ui.FlatRow;
                int available = destination == null ? S.HomeBoxParts.CountAll(p) : destination.Sim.Stock.CountAll(p);
                CompactProduct(row, p.Name, PartUnitKg(p), $"市价 {S.Market.PriceOf(p)} · 收货点有 {available}", false);
                Ui.Size(Ui.Label(row, $"{price}", 18, TextAnchor.MiddleLeft, Ui.Good), 90);
                QuantityStepper(w, row, "wholesale_" + part.Id, n, 0, 1000, 1, value => _cart[part.Id] = (int)value);
            }

            var head = Ui.Row(side, 34, 8);
            Ui.Size(Ui.Label(head, picked.Count == 0 ? "<b>待下单</b>　还是空的" : $"<b>待下单</b>　{picked.Count} 样 · {space:0.#} 格", 20, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
            if (picked.Count > 0) Ui.Size(Ui.Button(head, "清空", () => { _cart.Clear(); w.Rebuild(); }, 15, Ui.ButtonBg), 64, 30);
            RectTransform picks = GoodsList(side, 120);
            if (picked.Count == 0) Para(picks, "在左边商品点“＋”选数量。\n次日 10:00 统一送到收货点。", 16, Ui.Dim);
            foreach (var item in picked)
            {
                var part = item.part; var row = Ui.Row(picks, 46, 6);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                var label = Ui.Label(row, $"{part.Name} <color=#E8BE62>×{item.count}</color>\n<size=14><color=#AAB4BB>{item.price * item.count}</color></size>", 17, TextAnchor.MiddleLeft);
                label.verticalOverflow = VerticalWrapMode.Truncate;
                Ui.Size(label, -1, -1, 1);
                Ui.Size(Ui.Button(row, "移除", () => { _cart.Remove(part.Id); w.Rebuild(); }, 15, Ui.ButtonBg), 58, 34);
            }
            Ui.Size(Ui.Label(side, $"送到 {S.CourierDestName(destination)}　运费 {Online1788.ShippingFee}", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 28);
            Ui.Size(Ui.Label(side, $"合计 <b><color=#E8C77A>{total}</color></b>", 28, TextAnchor.MiddleRight), -1, 40);
            var order = Ui.Button(side, "下单", () =>
            {
                var lines = _cart.Where(kv => kv.Value > 0).Select(kv => (PartCatalog.Get(kv.Key), kv.Value)).ToList();
                void Place() { if (S.PlaceOnlineOrder(Online1788.Shopping.Id, lines, destination)) _cart.Clear(); w.Rebuild(); }
                bool covered = destination != null && lines.Any(l =>
                    S.PendingProcurementCount(destination, PurchaseGoodsKind.Part, l.Item1.Id)
                    + S.ClaimedProcurementCount(destination.Place.Id, PurchaseGoodsKind.Part, l.Item1.Id) > 0);
                if (covered) ConfirmAction("额外采购确认", "这些商品已有在途或已认领供给。本次属于额外囤货，仍要再下这一单吗？", "确认额外下单", Place);
                else Place();
            }, 22, Ui.ButtonHot, _cart.Values.Any(v => v > 0));
            order.name = "OnlineOrder";
            Ui.Size(order, -1, 54);
        }
        private void AddToCart(PartDef p, int delta)
        {
            _cart.TryGetValue(p.Id, out int n);
            _cart[p.Id] = Mathf.Max(0, n + delta);
        }

    }
}
