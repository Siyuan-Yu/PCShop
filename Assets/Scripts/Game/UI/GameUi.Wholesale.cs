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
            Para(c, "经营配件批发，下单后次日 10:00 送货上门。食品、生活用品与家具保留普通网购/线下入口；比较经营来源请用统一采购。", 15, Ui.Dim);
            Ui.Size(Ui.Button(c, "统一商品采购 / 在途订单", OpenCityProcurement, 17), -1, 38);
            if (!S.HasShop) _onlineToHome = true;
            RectTransform dest = Ui.Row(c, 48, 6);
            Ui.Size(Ui.Label(dest, "送到", 16, TextAnchor.MiddleRight, Ui.Dim), 50);
            Ui.Size(Ui.Button(dest, $"{ShopDestName}\n<size=13>配件入后仓，已设类别的货架自动补货</size>", () => { _onlineToHome = false; w.Rebuild(); }, 16,
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
                void Place() { if (S.PlaceOnlineOrder(_onlineStore, lines, _onlineToHome)) _cart.Clear(); w.Rebuild(); }
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

        private void BuildRestock(Window w)
        {
            Transform c = w.Content;
            GuideHint(c, QuestTarget.Restock, w.Rebuild);
            Para(c, "<b>自动补货是什么？</b> 给每样配件定一个“常备几件”。每天早上 9:00，1788 自动看一眼店里的库存（含还在路上的），"
                + "按常备目标/补货下限补齐未覆盖需求。员工已承担的缺口不会再由1788重复买；可在统一采购页选择唯一主执行方式。设成“关”就不自动补。", 15, Ui.Dim);
            Ui.Size(Ui.Button(c, "设置每件商品的执行方式与下限", OpenCityProcurement, 16), -1, 38);
            Para(c, $"自动下单后至少给你留 {S.EffectiveRestockReserve} 现金；店里仓位放不下的会少补。", 15, Ui.Dim);

            var policy = Ui.Row(c, 38);
            Ui.Size(Ui.Label(policy, "保留天数", 15), 90);
            QuantityStepper(w, policy, "reserveDays", S.RestockReserveDays, 0, 365, 1, v => S.RestockReserveDays = (int)v);
            var reserve = Ui.Row(c, 38);
            Ui.Size(Ui.Label(reserve, "现金底线", 15), 90);
            QuantityStepper(w, reserve, "reserveCash", S.RestockReserveYuan, 0, 10000000, 100, v => S.RestockReserveYuan = (int)v, v => "¥" + v.ToString("0"));
            var budget = Ui.Row(c, 38);
            Ui.Size(Ui.Label(budget, "日预算", 15), 90);
            QuantityStepper(w, budget, "dailyBudget", S.RestockDailyBudgetYuan, 0, 10000000, 100, v => S.RestockDailyBudgetYuan = (int)v, v => v == 0 ? "不限" : "¥" + v.ToString("0"));
            if (!string.IsNullOrEmpty(S.LastRestockNote)) Line(c, S.LastRestockNote, 14, Ui.Accent);
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
            Ui.Size(Ui.Button(storeRow, "选择商家 ▾", () => OpenWindow("补货商家", 580, 480, picker =>
            {
                Ui.Size(Ui.Button(picker.Content, "自动挑选", () => { S.RestockStoreId = null; picker.Close(); w.Rebuild(); }), -1, 42);
                foreach (var store in Online1788.Stores.Where(x => S.Online.IsDiscovered(x.Id)))
                {
                    var choice = store;
                    Ui.Size(Ui.Button(picker.Content, choice.Name, () => { S.RestockStoreId = choice.Id; picker.Close(); w.Rebuild(); }), -1, 42);
                }
            }), 16), 150);
            RefreshStore();
            if (!string.IsNullOrEmpty(S.LastRestockNote)) Line(c, "上次：" + S.LastRestockNote, 14, Ui.Dim);

            RectTransform head = Ui.Row(c, 26, 4);
            Ui.Size(Ui.Label(head, "配件", 14, TextAnchor.MiddleLeft, Ui.Dim), 420);
            Ui.Size(Ui.Label(head, "常备几件", 14, TextAnchor.MiddleCenter, Ui.Dim), 150);
            RectTransform list = Ui.Scroll(c, 250);
            foreach (PartDef part in PartCatalog.All.Where(p=>PCShopSell.Core.Home.IndustryCycles.Available(p,S.Clock.Today.TotalDays)))
            {
                PartDef p = part;
                RectTransform row = Ui.Row(list, 66, 4);
                Text name = ProductText(row, "", 15);
                Ui.Size(name, 420);
                int have = S.PlayerShop.Stock.CountAll(p), pend = S.PendingOnlineCount(p.Id);
                string stock = have == 0 ? "<color=#D79780>店里有 0 件</color>" : $"店里有 {have} 件";
                name.text = ProductLabel($"<b>{p.Name}</b>", PartUnitKg(p), stock + (pend > 0 ? $" · 路上 {pend}" : "")).Replace("<size=13>", "<size=14>");
                QuantityStepper(w, row, "restock_" + p.Id, S.RestockTarget(p.Id), 0, 1000, 1,
                    value => S.SetRestockTarget(p.Id, (int)value), value => value == 0 ? "关" : value.ToString("0"));
            }
        }
    }
}
