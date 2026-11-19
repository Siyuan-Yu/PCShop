using System;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Shop
{
    public sealed partial class ShopSimulation
    {
        private static readonly long[] ModelRetailBudgets = { 8000, 15000, 30000, 60000 };
        private static readonly (int Share, long Cents)[] ClothingRetailBudgets =
        {
            (65, 60000), (25, 180000), (9, 600000), (1, 5000000)
        };

        private static uint RetailBudgetHash(string identity)
        {
            uint hash = 2166136261;
            foreach (char c in identity) hash = unchecked((hash ^ c) * 16777619);
            return hash;
        }

        internal static long ModelRetailBudgetFor(string identity) =>
            ModelRetailBudgets[RetailBudgetHash(identity ?? "visitor") % (uint)ModelRetailBudgets.Length];

        private static long ClothingRetailBudgetFor(string identity)
        {
            int segment = (int)(RetailBudgetHash(identity) % 100);
            foreach (var (share, cents) in ClothingRetailBudgets)
            {
                if (segment < share) return cents;
                segment -= share;
            }
            return ClothingRetailBudgets[ClothingRetailBudgets.Length - 1].Cents;
        }

        private double ModelBudgetFactor(string id) =>
            IndustryCycles.GoodsFactor(Gifts.Get(id), _clock.Today.TotalDays, _market.CommoditySeed);

        private static bool RetailBudgetCovers(Money price, long budgetCents, double factor = 1) =>
            price.Cents <= budgetCents * factor;

        private readonly System.Collections.Generic.Dictionary<string,long> _fixedSalePrices=new System.Collections.Generic.Dictionary<string,long>();
        public System.Collections.Generic.IReadOnlyDictionary<string,long> FixedSalePrices=>_fixedSalePrices;
        public void RestoreFixedSalePrice(string key, long price)
        {
            // Older saves already store the model's markup alongside its fixed price.
            if (key != null && price > 0 && !key.StartsWith("g:model:", StringComparison.Ordinal))
                _fixedSalePrices[key] = price;
        }
        private static string FixedPartKey(PartDef part,bool boxed)=>"p:"+part.Id+":"+boxed;
        private void ClearFixedPartPrices(PartDef part){_fixedSalePrices.Remove(FixedPartKey(part,true));_fixedSalePrices.Remove(FixedPartKey(part,false));}

        public Func<double> SalesBonusOf { get; set; } = () => 0;
        public Func<bool> HasClothingMirror { get; set; } = () => false;
        public double ClothingMirrorPurchaseIntentBonus => HasClothingMirror?.Invoke()==true?.05:0;
        public double GoodsMirrorBonus(string id)=>ClothingProducts.IsClothing(id)?ClothingMirrorPurchaseIntentBonus:0;
        public double RestaurantDrinkPurchaseIntentBonus(string id) => RetailKind == "restaurant" && RetailProducts.IsDrink(id) ? .20 : 0;
        public double SalesPurchaseIntentBonus => Math.Max(0,Math.Min(.25,SalesBonusOf?.Invoke()??0));
        public double ReputationPurchaseIntentBonus => (ShopReputation - NeutralShopReputation) * .003;
        public double MarkupPurchaseIntent(double markup) => AssistedPurchaseIntent(ShopSettings.BuyChance(markup));
        private double AssistedPurchaseIntent(double baseline)=>Math.Max(0,Math.Min(1,baseline+SalesPurchaseIntentBonus+ReputationPurchaseIntentBonus));
        private double PartPurchaseChance(double markup,bool boxed)
        {
            double chance=ShopSettings.BuyChance(markup);
            return AssistedPurchaseIntent(boxed?1-(1-chance)*Settings.BoxedTrust:chance);
        }
        private double GoodsPurchaseChance(string id,double markup)=>AssistedPurchaseIntent(
            ShopSettings.BuyChance(markup)*(IsGoodsShop?1:Settings.GoodsBuyFactor)*(ModelProducts.IsModel(id)?Math.Min(1.4,IndustryCycles.GoodsFactor(Gifts.Get(id),_clock.Today.TotalDays,_market.CommoditySeed)):1)+GoodsMirrorBonus(id)+RestaurantDrinkPurchaseIntentBonus(id));
        private static (long min, long max) SalePriceRange(double basis) =>
            (Math.Max(1, (long)Math.Round(basis * ShopSettings.MinMarkup)), Math.Max(1, (long)Math.Round(basis * ShopSettings.MaxMarkup)));

        public (long min, long max) PartPriceRange(PartDef part, bool boxed) =>
            SalePriceRange(_market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1));

        public double PartPurchaseIntentAtPrice(PartDef part, bool boxed, long yuan)
        {
            double basis = _market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1);
            return basis > 0 ? PartPurchaseChance(yuan / basis,boxed) : 0;
        }

        public double GoodsPurchaseIntentAtPrice(string id, long yuan) =>
            GoodsPurchaseIntentAtPrice(id, Money.FromYuan(yuan));

        private double GoodsPurchaseIntentAtPrice(string id, Money price)
        {
            double basis = GoodsReferencePrice(id);
            return basis > 0 ? GoodsPurchaseChance(id, price.Cents / 100.0 / basis) : 0;
        }

        public double GoodsPurchaseEstimateAtPrice(string id, long yuan) =>
            GoodsPurchaseEstimateAtPrice(id, Money.FromYuan(yuan));

        public double GoodsPurchaseEstimateAtPrice(string id, Money price)
        {
            double intent = GoodsPurchaseIntentAtPrice(id, price);
            if (ModelProducts.IsModel(id))
            {
                double factor = ModelBudgetFactor(id);
                int affordable = 0;
                foreach (long budget in ModelRetailBudgets)
                    if (RetailBudgetCovers(price, budget, factor)) affordable++;
                return intent * affordable / ModelRetailBudgets.Length;
            }
            if (ClothingProducts.IsClothing(id))
            {
                int affordable = 0;
                foreach (var (share, cents) in ClothingRetailBudgets)
                    if (RetailBudgetCovers(price, cents)) affordable += share;
                return intent * affordable / 100;
            }
            return intent;
        }

        public bool SetPartSalePrice(PartDef part, bool boxed, long yuan)
        {
            if (part == null) return false;
            double basis = _market.PriceOf(part).Yuan * (boxed ? 1 + Settings.BoxedPremium : 1);
            var range = SalePriceRange(basis);
            if (basis <= 0 || yuan < range.min || yuan > range.max) return false;
            SetPartMarkup(part, yuan / basis);
            _fixedSalePrices[FixedPartKey(part,boxed)]=yuan;
            return true;
        }

        public double GoodsReferencePrice(string id) => (Gifts.Get(id)?.Price.Cents??0)/100.0*IndustryCycles.GoodsFactor(Gifts.Get(id),_clock.Today.TotalDays,_market.CommoditySeed);
        public (long min, long max) GoodsPriceRange(string id) => SalePriceRange(GoodsReferencePrice(id));
        public bool SetGoodsSalePrice(string id, long yuan)
        {
            var gift = Gifts.Get(id);
            if (gift == null || gift.Price.Yuan <= 0) return false;
            var range = GoodsPriceRange(id);
            if (yuan < range.min || yuan > range.max) return false;
            SetGoodsMarkup(id, yuan / GoodsReferencePrice(id));
            if (!ModelProducts.IsModel(id)) _fixedSalePrices[GoodsKey(id)] = yuan;
            return true;
        }
    }
}
