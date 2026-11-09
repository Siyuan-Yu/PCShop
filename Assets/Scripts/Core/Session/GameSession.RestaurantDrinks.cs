using System.Linq;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // Called only at checkout, so every dining order gets one attempt,
        // including across save/load. Keep the dedicated restaurant customer flow.
        private void SellRestaurantDrink(OwnedShop shop,SpecialBusinessState state,DiningOrder order,GameTimestamp now)
        {
            var drinks=shop.Sim.GoodsOnShelves().Where(line=>line.Value>0&&RetailProducts.IsDrink(line.Key)).ToArray();
            if(drinks.Length==0)return;
            string id=drinks[Random.Next(0,drinks.Length)].Key;
            var price=shop.Sim.GoodsPriceOf(id);
            if(!Random.Chance(shop.Sim.GoodsPurchaseIntentAtPrice(id,price.Yuan))||!shop.Sim.Stock.TryRemoveGoods(id,1))return;
            Ledger.Credit(now.Date,"零售",price,"堂食加购 "+Gifts.Get(id).Name,
                LedgerAttribution.ForShop(shop.Place.Id,"meal:"+order.Id+":drink","销售").Product(id,1,price.Cents));
            state.Revenue+=price.Yuan;
        }
    }
}
