using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData { public int shopFootfallVersion; }
    public sealed partial class GameSession
    {
        private ShopFootfall _shopFootfall;
        public ShopFootfallEstimate FootfallOf(Place place) => (_shopFootfall??(_shopFootfall=new ShopFootfall(City))).For(place);
        public Money ShopRentQuote(Place place) => FootfallOf(place).MonthlyRent;
        // Size is already counted by the footfall estimate; avoid multiplying it twice.
        public double LocationFactorOf(Place place) => FootfallOf(place).DailyPassers/(600.0*ShopSettings.SizeFactor(place.Size));
        public double StationDistance(Place place) => FootfallOf(place).StationMeters;
        private long SpecialVisitInterval(OwnedShop shop,int baseMinutes) => Math.Max(1,(long)Math.Round(baseMinutes*300.0*ShopCompetitionCount(shop.Place,shop.RetailKind)/FootfallOf(shop.Place).DailyPassers));
    }
}
