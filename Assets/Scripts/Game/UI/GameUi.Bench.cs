using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>装机台：走到店里左后角的装机台按 F，列出所有装机单，配件齐了就能开始装机。</summary>
    public sealed partial class GameUi
    {
        public void OpenBenchWindow()
        {
            OpenWindow("装机台", 900, 680, w =>
            {
                ShopSimulation shop = S.PlayerShop;
                Line(w.Content, "顾客下单后离开，两天内装好交付才收钱；超时取消、掉知名度。配件从库存里出，先备齐。", 15, Ui.Dim);

                // 台面：深色桌面，每张装机单是一张工单卡
                Image desk = Ui.Panel(w.Content, "Desk", new Color(0.12f, 0.13f, 0.16f));
                Ui.Size(desk, -1, 470);
                RectTransform list = Ui.Scroll(desk.transform, 470);
                list.parent.parent.GetComponent<RectTransform>().Fill();

                if (shop.Orders.Count == 0)
                {
                    Ui.Size(Ui.Label(list, "台面上空空的，暂时没有装机单。\n约每 8 位顾客里有 1 位想装整机：他们会在展示桌看中一台，离店后下装机单。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 70);
                }
                foreach (BuildOrder o in shop.Orders)
                {
                    BuildOrder order = o;
                    List<PartDef> missing = S.MissingParts(order);
                    long left = S.Clock.Now.MinutesUntil(order.DueAt);
                    Image card = Ui.Panel(list, "Order", new Color(0.93f, 0.91f, 0.84f));
                    var v = Ui.VList(card.rectTransform, 2, 10);
                    Color ink = new Color(0.16f, 0.16f, 0.18f);
                    string urgent = left < 12 * 60 ? "<color=#B33A2A>（快到期了！）</color>" : "";
                    Text head = Ui.Label(card.transform, $"<b>装机单 #{order.Id}　{PartCatalog.TierName(order.Tier)}整机</b>　报价 <b>{order.Price}</b>　剩 {left / 60} 小时 {urgent}", 18, TextAnchor.MiddleLeft, ink);
                    Ui.Size(head, -1, 30);
                    foreach (PartDef p in PartCatalog.BuildList(order.Tier))
                    {
                        int have = shop.Stock.CountAll(p);
                        string mark = have > 0 ? "<color=#1E8E3E>有</color>" : "<color=#C62828>缺</color>";
                        Ui.Size(Ui.Label(card.transform, $"{mark}　<b>{PartCatalog.CategoryName(p.Category)}</b>　{p.Name}　<size=14>库存 {have}</size>", 16, TextAnchor.MiddleLeft, ink), -1, 24);
                    }
                    Button go = Ui.Button(card.transform, missing.Count == 0 ? "开始装机" : $"还缺 {missing.Count} 样配件，先去补货",
                        () =>
                        {
                            w.Close();
                            OpenBuildMinigame(order);
                        }, 18, Ui.ButtonHot, missing.Count == 0);
                    Ui.Size(go, -1, 40);
                }

                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }
    }
}
