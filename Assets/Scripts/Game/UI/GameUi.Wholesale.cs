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

        private bool _onlineToHome;   // 网购 送到哪：false = 店里，true = 家里
        private readonly Dictionary<string, int> _cart = new Dictionary<string, int>();

        public void OpenWholesaleApp(bool restockPage = false)
        {
            if (restockPage) { OpenCityProcurement(); return; }
            _onlineTab = 5;
            OpenOnlinePanel();
        }

        private void BuildOnline(Window w)
        {
            Transform c = w.Content;
            Para(c, "选商品、数量和收货地点，次日10:00送达。", 15, Ui.Dim);

            if (!S.HasShop) _onlineToHome = true;
            RectTransform dest = Ui.Row(c, 48, 6);
            Ui.Size(Ui.Label(dest, "送到", 16, TextAnchor.MiddleRight, Ui.Dim), 50);
            Ui.Size(Ui.Button(dest, $"{ShopDestName}\n<size=13>配件入后仓，已设类别的货架自动补货</size>", () => { _onlineToHome = false; w.Rebuild(); }, 16,
                _onlineToHome ? Ui.ButtonBg : Ui.ButtonHot, S.HasShop), -1, -1, 1);
            Ui.Size(Ui.Button(dest, "家里\n<size=13>放入家中后仓</size>", () => { _onlineToHome = true; w.Rebuild(); }, 16,
                _onlineToHome ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
            OnlineStore s = Online1788.Shopping;

            RectTransform list = Ui.Scroll(c, 440);
            Money total = Online1788.ShippingFee;
            double space = 0;
            foreach (PartDef p in PartCatalog.All.Where(p=>PCShopSell.Core.Home.IndustryCycles.Available(p,S.Clock.Today.TotalDays)))
            {
                PartDef part = p;
                Money price = S.OnlinePrice(s, p);
                _cart.TryGetValue(p.Id, out int n);
                total += price * n;
                space += Stock.SpaceOf(p, true, n);
                RectTransform row = Ui.Row(list, 54);
                Ui.Size(ProductText(row, ProductLabel(p.Name, PartUnitKg(p)), 16), 260);
                Ui.Size(Ui.Label(row, $"{price}", 16, TextAnchor.MiddleLeft, Ui.Good), 90);
                Ui.Size(Ui.Label(row, $"市价 {S.Market.PriceOf(p)}", 13, TextAnchor.MiddleLeft, Ui.Dim), 100);
                Ui.Size(Ui.Label(row, $"店里有 {S.PlayerShop.Stock.CountAll(p)}", 13, TextAnchor.MiddleLeft, Ui.Dim), 90);
                QuantityStepper(w, row, "wholesale_" + part.Id, n, 0, 1000, 1, value => _cart[part.Id] = (int)value);
            }
            RectTransform orow = Ui.Row(c, 46);
            string room = _onlineToHome ? "家中后仓容量不限" : "店铺后仓容量不限";
            Ui.Size(Ui.Label(orow, $"合计 {total}（含运费 {Online1788.ShippingFee}） · {space:0.#} 格　送到{(_onlineToHome ? "家里" : "店里")}　{room}", 16), -1, -1, 1);
            Ui.Size(Ui.Button(orow, "下单", () =>
            {
                var lines = _cart.Where(kv => kv.Value > 0).Select(kv => (PartCatalog.Get(kv.Key), kv.Value)).ToList();
                void Place() { if (S.PlaceOnlineOrder(Online1788.Shopping.Id, lines, _onlineToHome)) _cart.Clear(); w.Rebuild(); }
                bool covered = !_onlineToHome && lines.Any(l => S.PendingOnlineCount(l.Item1.Id) + S.ClaimedProcurementCount(S.CurrentShop.Place.Id, PurchaseGoodsKind.Part, l.Item1.Id) > 0);
                if (covered) ConfirmAction("额外采购确认", "这些商品已有在途或已认领供给。本次属于额外囤货，仍要再下这一单吗？", "确认额外下单", Place);
                else Place();
            }, 19, Ui.ButtonHot, _cart.Values.Any(v => v > 0)), 140);
        }

        private void AddToCart(PartDef p, int delta)
        {
            _cart.TryGetValue(p.Id, out int n);
            _cart[p.Id] = Mathf.Max(0, n + delta);
        }

    }
}
