using System.Linq;
using PCShopSell.Core.Home;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildRestaurantDrinkPricing()
        {
            Line(C,"堂食加购饮料 · 购买意愿+20个百分点",17,Ui.Accent);
            var shop=S.PlayerShop;
            var drinks=shop.GoodsOnShelves().Where(line=>line.Value>0&&RetailProducts.IsDrink(line.Key)).ToArray();
            if(drinks.Length==0){Line(C,"先将饮料上架，顾客才能加购；后仓存货不直接出售。",15,Ui.Dim);return;}
            foreach(var drink in drinks)
            {
                string id=drink.Key;var product=Gifts.Get(id);
                Line(C,product.Name+$" · 已陈列 {drink.Value} 件 · ¥{shop.GoodsPriceOf(id).Yuan} · 购买意愿 {shop.GoodsPurchaseIntentAtPrice(id,shop.GoodsPriceOf(id).Yuan)*100:0}%",16);
                Ui.Size(Ui.Button(C,"调价 · "+product.Name,()=>OpenWindow("调价 · "+product.Name,600,300,w=>
                {
                    Para(w.Content,"堂食顾客结账时可加购一份，按确认后的售价成交。",17);
                    Ui.Size(ShelfSaleTag(w,w.Content,"goods_"+id,shop.GoodsPriceOf(id).Yuan,shop.GoodsPriceRange(id),
                        value=>shop.GoodsPurchaseIntentAtPrice(id,value),value=>{shop.SetGoodsSalePrice(id,value);RebuildDock();}),-1,100);
                }),16,Ui.ButtonHot),-1,36).name="EditPrice_goods_"+id;
            }
        }
    }
}
