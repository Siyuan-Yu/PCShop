using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>货架窗口里杂货那一半：烟酒、饮料、点心、水果，列出货架上摆着的、后仓里的、手边能放进来的。</summary>
    public sealed partial class GameUi
    {
        private static Color GoodsColor(string id)
        {
            switch (id)
            {
                case Gifts.Cig:
                case Gifts.CigHi: return new Color(0.8f, 0.2f, 0.2f);
                case Gifts.Wine:
                case Gifts.Baijiu: return new Color(0.2f, 0.5f, 0.3f);
                case Gifts.Drink:
                case Gifts.Soda: return new Color(0.3f, 0.6f, 0.88f);
                case Gifts.Snack: return new Color(0.9f, 0.62f, 0.25f);
                default: return new Color(0.9f, 0.4f, 0.25f);
            }
        }

        private void BuildGoodsShelfBody(Window w, ShelfKind kind, ShelfFill fill)
        {
            ShopSimulation shop = S.PlayerShop;
            Stock st = shop.Stock;
            Para(w.Content, "把储物柜或背包里的货放进店里，货架上有这类货架就自动摆上去；来店里的人多半是买电脑配件的，杂货卖得慢，摆什么、卖多少钱自己定。", 15, Ui.Dim);

            RectTransform list = Ui.Scroll(w.Content, 380);
            int shown = 0;
            foreach (string id in kind.GiftIds)
            {
                GiftDef g = Gifts.Get(id);
                string gid = id;
                int onShelf = fill.GoodsCount(gid);
                int inStock = st.GoodsCount(gid);
                int handy = S.GoodsAvailableToList(gid);
                if (inStock == 0 && handy == 0) continue; // 店里没有、手边也没有的，不显示
                shown++;
                double m = shop.MarkupOfGoods(gid);
                bool own = shop.HasGoodsMarkup(gid);

                RectTransform row = Ui.Row(list, 56, 6);
                Image chip = Ui.Panel(row, "Chip", GoodsColor(gid));
                Ui.Size(chip, 14, 40);
                Ui.Size(Ui.Label(row, $"<b>{g.Name}</b>\n<size=13><color=#ABB5A8>进价 {g.Price}　每件 {ShelfPlanner.GoodsKg(gid):0.#} 公斤</color></size>", 17), 200);
                Ui.Size(Ui.Label(row, $"货架上 <b>{onShelf}</b> 件\n<size=13><color=#ABB5A8>后仓共 {inStock} 件" + (handy > 0 ? $"　手边 {handy}" : "") + "</color></size>", 16), 170);
                Ui.Size(Ui.Label(row, $"售 <color=#A9C9A9>{shop.GoodsPriceOf(gid)}</color>\n<size=13><color=#ABB5A8>{m:0.00}×{(own ? "" : "（跟随）")}　愿买约 {ShopSettings.BuyChance(m) * shop.Settings.GoodsBuyFactor * 100:0}%</color></size>", 16), 190);
                Ui.Size(Ui.Button(row, "−", () => { S.SetGoodsMarkup(gid, Mathf.Round((float)m * 20 - 1) / 20f); w.Rebuild(); }, 20, Ui.ButtonBg, m > 0.9 + 1e-6), 34);
                Ui.Size(Ui.Button(row, "+", () => { S.SetGoodsMarkup(gid, Mathf.Round((float)m * 20 + 1) / 20f); w.Rebuild(); }, 20, Ui.ButtonBg, m < 1.6 - 1e-6), 34);
                Ui.Size(Ui.Button(row, "跟随", () => { S.SetGoodsMarkup(gid, null); w.Rebuild(); }, 13, Ui.ButtonBg, own), 54);
                Ui.Size(Ui.Button(row, "放进 1", () => { S.ListGoods(gid, 1); w.Rebuild(); }, 15, Ui.ButtonHot, handy > 0), 74);
                Ui.Size(Ui.Button(row, "全放进", () => { S.ListGoods(gid, handy); w.Rebuild(); }, 15, Ui.ButtonBg, handy > 0), 70);
                Ui.Size(Ui.Button(row, "收回 1", () => { S.UnlistGoods(gid, 1); w.Rebuild(); }, 15, Ui.ButtonBg, inStock > 0), 74);
            }
            if (shown == 0)
                Ui.Size(Ui.Label(list, $"店里没有「{kind.Name}」，储物柜和背包里也没有。\n去烟酒茶行、农贸市场买，或者网购，放进店里储物柜，再回来放进来。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 70);
        }
    }
}
