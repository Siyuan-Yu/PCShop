using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // Called only at checkout, so every dining order gets one attempt,
        // including across save/load. Keep the dedicated restaurant customer flow.
        private (string Name, Money Price) SellRestaurantDrink(OwnedShop shop,SpecialBusinessState state,DiningOrder order,GameTimestamp now)
        {
            var drinks=shop.Sim.GoodsOnShelves().Where(line=>line.Value>0&&RetailProducts.IsDrink(line.Key)).ToArray();
            if(drinks.Length==0)return default;
            string id=drinks[Random.Next(0,drinks.Length)].Key;
            var price=shop.Sim.GoodsPriceOf(id);
            if(!Random.Chance(shop.Sim.GoodsPurchaseIntentAtPrice(id,price.Yuan))||!shop.Sim.Stock.TryRemoveGoods(id,1))return default;
            string name = Gifts.Get(id).Name;
            Ledger.Credit(now.Date,"零售",price,"堂食加购 "+name,
                LedgerAttribution.ForShop(shop.Place.Id,RestaurantSaleId(shop, order) + ":drink","销售").Product(id,1,price.Cents));
            state.Revenue+=price.Yuan;
            return (name, price);
        }
    }
}
