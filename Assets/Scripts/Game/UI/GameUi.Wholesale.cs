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
    /// <summary>手机上的 1788 批发 app：进货下单 + 自动补货设置。买了智能机、在应用商店装了 1788 才能用，人在哪都能打开。</summary>
    public sealed partial class GameUi
    {
        private bool _wholesaleRestockPage;
        private string _onlineStore;
        private bool _onlineToHome;   // 1788 送到哪：false = 店里，true = 家里
        private readonly Dictionary<string, int> _cart = new Dictionary<string, int>();

        public void OpenWholesaleApp(bool restockPage = false)
        {
            string block = S.AppBlocker(PhoneApps.Wholesale);
            if (block != null)
            {
                Toast(block);
                return;
            }
            _wholesaleRestockPage = restockPage;
            OpenWindow("1788 批发", 860, 820, w =>
            {
                RectTransform tabs = Ui.Row(w.Content, 44, 6);
                Ui.Size(Ui.Button(tabs, "进货", () => { _wholesaleRestockPage = false; w.Rebuild(); }, 18, _wholesaleRestockPage ? Ui.ButtonBg : Ui.ButtonHot), -1, -1, 1);
                Ui.Size(Ui.Button(tabs, "自动补货", () => { _wholesaleRestockPage = true; w.Rebuild(); }, 18, _wholesaleRestockPage ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                if (_wholesaleRestockPage) BuildRestock(w);
                else BuildOnline(w);
            });
        }

        private void BuildOnline(Window w)
        {
            Transform c = w.Content;
            Para(c, "比档口便宜，下单后次日 10:00 送货上门，可能有次品。评分不一定可信，自己买过才知道。", 15, Ui.Dim);
            if (!S.HasShop) _onlineToHome = true;
            RectTransform dest = Ui.Row(c, 48, 6);
            Ui.Size(Ui.Label(dest, "送到", 16, TextAnchor.MiddleRight, Ui.Dim), 50);
            Ui.Size(Ui.Button(dest, $"{ShopDestName}\n<size=13>配件直接上货架</size>", () => { _onlineToHome = false; w.Rebuild(); }, 16,
                _onlineToHome ? Ui.ButtonBg : Ui.ButtonHot, S.HasShop), -1, -1, 1);
            Ui.Size(Ui.Button(dest, "家里\n<size=13>放在家里的纸箱，自己带去店里</size>", () => { _onlineToHome = true; w.Rebuild(); }, 16,
                _onlineToHome ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
            RectTransform stores = Ui.Scroll(c, 190);
            foreach (OnlineStore st in Online1788.Stores.Where(x => S.Online.IsDiscovered(x.Id)))
            {
                OnlineStore store = st;
                StoreRecord rec = S.Online.RecordOf(st.Id);
                string mine = rec.ItemsReceived > 0 ? $"你的记录：收货 {rec.ItemsReceived} 件，次品 {rec.Defects} 件" : "你还没在这家买过";
                Button b = Ui.Button(stores, $"{st.Name}  ★{st.ShownRating:0.0}  销量 {st.ShownSales}\n<size=14>{mine}</size>", () =>
                {
                    _onlineStore = store.Id;
                    _cart.Clear();
                    w.Rebuild();
                }, 16, _onlineStore == st.Id ? Ui.ButtonHot : Ui.ButtonBg);
                Ui.Size(b, -1, 48);
            }

            if (_onlineStore == null || !S.Online.IsDiscovered(_onlineStore))
            {
                Line(c, "选一家店看看。", 17, Ui.Dim);
                return;
            }
            OnlineStore s = Online1788.GetStore(_onlineStore);
            Line(c, "评价：" + string.Join(" / ", s.Reviews), 15, Ui.Dim);

            RectTransform list = Ui.Scroll(c, 280);
            Money total = Online1788.ShippingFee;
            double space = 0;
            foreach (PartDef p in PartCatalog.All)
            {
                PartDef part = p;
                Money price = S.OnlinePrice(s, p);
                _cart.TryGetValue(p.Id, out int n);
                total += price * n;
                space += Stock.SpaceOf(p, true, n);
                RectTransform row = Ui.Row(list, 34);
                Ui.Size(Ui.Label(row, p.Name, 16), 260);
                Ui.Size(Ui.Label(row, $"{price}", 16, TextAnchor.MiddleLeft, Ui.Good), 90);
                Ui.Size(Ui.Label(row, $"市价 {S.Market.PriceOf(p)}", 13, TextAnchor.MiddleLeft, Ui.Dim), 100);
                Ui.Size(Ui.Label(row, $"店里有 {S.PlayerShop.Stock.CountAll(p)}", 13, TextAnchor.MiddleLeft, Ui.Dim), 90);
                Ui.Size(Ui.Button(row, "-", () => { AddToCart(part, -1); w.Rebuild(); }, 18), 36);
                Ui.Size(Ui.Label(row, n.ToString(), 17, TextAnchor.MiddleCenter), 30);
                Ui.Size(Ui.Button(row, "+", () => { AddToCart(part, 1); w.Rebuild(); }, 18), 36);
            }
            RectTransform orow = Ui.Row(c, 46);
            string room = _onlineToHome ? "送家里不占店里仓位" : $"店里仓位还空 {S.PlayerShop.Stock.FreeSpace:0.#} 格";
            Ui.Size(Ui.Label(orow, $"合计 {total}（含运费 {Online1788.ShippingFee}） · {space:0.#} 格　送到{(_onlineToHome ? "家里" : "店里")}　{room}", 16), -1, -1, 1);
            Ui.Size(Ui.Button(orow, "下单", () =>
            {
                var lines = _cart.Where(kv => kv.Value > 0).Select(kv => (PartCatalog.Get(kv.Key), kv.Value)).ToList();
                if (S.PlaceOnlineOrder(_onlineStore, lines, _onlineToHome)) _cart.Clear();
                w.Rebuild();
            }, 19, Ui.ButtonHot, _cart.Values.Any(v => v > 0)), 140);
        }

        private void AddToCart(PartDef p, int delta)
        {
            _cart.TryGetValue(p.Id, out int n);
            _cart[p.Id] = Mathf.Max(0, n + delta);
        }

        private void BuildRestock(Window w)
        {
            Transform c = w.Content;
            Para(c, "<b>自动补货是什么？</b> 给每样配件定一个“常备几件”。每天早上 9:00，1788 自动看一眼店里的库存（含还在路上的），"
                + "哪样低于常备数的 40%，就自动下单补到常备数，第二天 10:00 送到店里。设成“关”就不自动补，全靠你自己进货。", 15, Ui.Dim);
            Para(c, $"自动下单后至少给你留 {GameSession.RestockCashReserve} 现金；店里仓位放不下的会少补。", 15, Ui.Dim);

            RectTransform storeRow = Ui.Row(c, 40);
            Text storeText = Ui.Label(storeRow, "", 16);
            Ui.Size(storeText, -1, -1, 1);
            void RefreshStore()
            {
                string id = S.RestockStoreId;
                storeText.text = id != null && S.Online.IsDiscovered(id)
                    ? "向哪家下单：" + Online1788.GetStore(id).Name
                    : "向哪家下单：自动（挑最便宜的，避开次品多的）";
            }
            Ui.Size(Ui.Button(storeRow, "换一家", () =>
            {
                var ids = new List<string> { null };
                ids.AddRange(Online1788.Stores.Where(s => S.Online.IsDiscovered(s.Id)).Select(s => s.Id));
                int i = ids.IndexOf(S.RestockStoreId);
                S.RestockStoreId = ids[(i + 1) % ids.Count];
                RefreshStore();
            }, 16), 100);
            RefreshStore();
            if (!string.IsNullOrEmpty(S.LastRestockNote)) Line(c, "上次：" + S.LastRestockNote, 14, Ui.Dim);

            RectTransform head = Ui.Row(c, 26, 4);
            Ui.Size(Ui.Label(head, "配件", 14, TextAnchor.MiddleLeft, Ui.Dim), 420);
            Ui.Size(Ui.Label(head, "常备几件", 14, TextAnchor.MiddleCenter, Ui.Dim), 150);
            RectTransform list = Ui.Scroll(c, 420);
            foreach (PartDef part in PartCatalog.All)
            {
                PartDef p = part;
                RectTransform row = Ui.Row(list, 40, 4);
                Text name = Ui.Label(row, "", 15);
                Ui.Size(name, 420);
                Button minus = Ui.Button(row, "−", null, 18);
                Text tgt = Ui.Label(row, "", 16, TextAnchor.MiddleCenter);
                Button plus = Ui.Button(row, "+", null, 18);
                Ui.Size(minus, 40);
                Ui.Size(tgt, 60);
                Ui.Size(plus, 40);

                void Refresh()
                {
                    int have = S.PlayerShop.Stock.CountAll(p), pend = S.PendingOnlineCount(p.Id);
                    string stock = have == 0 ? "<color=#E0806A>店里有 0 件</color>" : $"店里有 {have} 件";
                    name.text = $"<b>{p.Name}</b>　<size=13>{stock}{(pend > 0 ? $" · 路上 {pend}" : "")}</size>";
                    int target = S.RestockTarget(p.Id);
                    tgt.text = target == 0 ? "<color=#9AA3AD>关</color>" : target.ToString();
                }

                minus.onClick.AddListener(() =>
                {
                    S.SetRestockTarget(p.Id, S.RestockTarget(p.Id) - 2);
                    Refresh();
                });
                plus.onClick.AddListener(() =>
                {
                    S.SetRestockTarget(p.Id, S.RestockTarget(p.Id) + 2);
                    Refresh();
                });
                Refresh();
            }
        }
    }
}
