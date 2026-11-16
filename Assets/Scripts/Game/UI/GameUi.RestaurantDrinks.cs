using System;
using System.Linq;
using PCShopSell.Core.Home;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Action BuildRestaurantDrinkPricing()
        {
            Line(C, "堂食加购饮料 · 购买意愿+20个百分点", 17, Ui.Accent);
            var shop = S.PlayerShop;
            var empty = Line(C, "可去农贸市场批发饮料。饮料上架后，堂食顾客结账时可加购。", 15, Ui.Dim);
            var drinks = Gifts.All.Where(product => RetailProducts.IsDrink(product.Id)).Select(product =>
            {
                string id = product.Id;
                var row = Ui.Rect(C, "DrinkPricing_" + id);
                Ui.VList(row, 4, 0);
                var caption = ItemLabel(row, id, "", 16);
                Ui.Size(Ui.Button(row, "调价 · " + product.Name, () => OpenWindow("调价 · " + product.Name, 600, 300, w =>
                {
                    Para(w.Content, "堂食顾客结账时可加购一份，按确认后的售价成交。", 17);
                    Ui.Size(ShelfSaleTag(w, w.Content, "goods_" + id, shop.GoodsPriceOf(id).Yuan, shop.GoodsPriceRange(id),
                        value => shop.GoodsPurchaseIntentAtPrice(id, value), value => { shop.SetGoodsSalePrice(id, value); _refreshPricing?.Invoke(); }), -1, 100);
                }), 16), -1, 36).name = "EditPrice_goods_" + id;
                return (product, row, caption);
            }).ToArray();

            return () =>
            {
                bool any = false;
                foreach (var (product, row, caption) in drinks)
                {
                    int count = shop.OnShelfGoods(product.Id);
                    row.gameObject.SetActive(count > 0);
                    if (count == 0) continue;
                    long price = shop.GoodsPriceOf(product.Id).Yuan;
                    caption.text = $"{product.Name} · 已陈列 {count} 件 · ¥{price} · 购买意愿 {shop.GoodsPurchaseIntentAtPrice(product.Id, price) * 100:0}%";
                    any = true;
                }
                empty.gameObject.SetActive(!any);
            };
        }
    }
}
