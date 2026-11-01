using System;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Shop
{
    public sealed partial class ShopSimulation
    {
        public Func<double> SalesBonusOf { get; set; } = () => 0;
        public Func<bool> HasClothingMirror { get; set; } = () => false;
        public double ClothingMirrorPurchaseIntentBonus => HasClothingMirror?.Invoke()==true?.05:0;
        public double GoodsMirrorBonus(string id)=>ClothingProducts.IsClothing(id)?ClothingMirrorPurchaseIntentBonus:0;
        public double SalesPurchaseIntentBonus => Math.Max(0,Math.Min(.25,SalesBonusOf?.Invoke()??0));
        private double AssistedPurchaseIntent(double baseline)=>Math.Max(0,Math.Min(1,baseline+SalesPurchaseIntentBonus));
        private double PartPurchaseChance(double markup,bool boxed)
        {
            double chance=ShopSettings.BuyChance(markup);
            return AssistedPurchaseIntent(boxed?1-(1-chance)*Settings.BoxedTrust:chance);
        }
        private double GoodsPurchaseChance(string id,double markup)=>AssistedPurchaseIntent(
            ShopSettings.BuyChance(markup)*(IsGoodsShop?1:Settings.GoodsBuyFactor)+GoodsMirrorBonus(id));
        private static (long min, long max) SalePriceRange(double basis) =>
            (Math.Max(1, (long)Math.Round(basis * .9)), Math.Max(1, (long)Math.Round(basis * 1.6)));

        public (long min, long max) PartPriceRange(PartDef part, bool boxed) =>
            SalePriceRange(_market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1));

        public double PartPurchaseIntentAtPrice(PartDef part, bool boxed, long yuan)
        {
            double basis = _market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1);
            return basis > 0 ? PartPurchaseChance(yuan / basis,boxed) : 0;
        }

        public double GoodsPurchaseIntentAtPrice(string id, long yuan)
        {
            double basis = Gifts.Get(id)?.Price.Yuan ?? 0;
            return basis > 0 ? GoodsPurchaseChance(id,yuan / basis) : 0;
        }

        public bool SetPartSalePrice(PartDef part, bool boxed, long yuan)
        {
            if (part == null) return false;
            double basis = _market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1);
            var range = SalePriceRange(basis);
            if (basis <= 0 || yuan < range.min || yuan > range.max) return false;
            SetPartMarkup(part, yuan / basis);
            return true;
        }

        public (long min, long max) GoodsPriceRange(string id) => SalePriceRange(Gifts.Get(id)?.Price.Yuan ?? 0);
        public bool SetGoodsSalePrice(string id, long yuan)
        {
            var gift = Gifts.Get(id);
            if (gift == null || gift.Price.Yuan <= 0) return false;
            var range = GoodsPriceRange(id);
            if (yuan < range.min || yuan > range.max) return false;
            SetGoodsMarkup(id, yuan / (double)gift.Price.Yuan);
            return true;
        }
    }
}
