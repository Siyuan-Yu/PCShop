using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const int UsedStorageWithoutShelves = 20;

        private static int UsedStockLimit(OwnedShop shop) =>
            shop.Sim.ShelfItemLimitOf(ShelfKinds.Get("secondhand")) ?? UsedStorageWithoutShelves;

        private static int UsedStockCount(SpecialBusinessState state) =>
            state.Used.Count(item => item.Owned && !item.Sold);

        public static double UsedWeight(UsedItem item) => OldMarketHuntCatalog.Spot(item.MarketSpotId)?.Weight
            ?? UsedWeights[Math.Clamp(item.Product, 0, UsedWeights.Length - 1)];
        public bool UsedReserved(OwnedShop shop,int id)=>shop.Sim.Queue.Any(c=>c.ResaleId==id);
        private IEnumerable<ResaleOffer> UsedOffers(OwnedShop shop)=>shop.RetailKind!="secondhand"?Enumerable.Empty<ResaleOffer>():
            SpecialBusiness(shop.Place.Id).Used.Where(i=>i.Owned&&i.Inspected&&i.Listed&&!i.Sold&&!UsedReserved(shop,i.Id))
                .Select(item => new ResaleOffer
                {
                    Id = item.Id, Product = item.Product, Name = UsedItemName(item) + " · " + UsedConditionName(item),
                    ProductName = UsedItemName(item), Portrait = UsedItemPortrait(item), Kg = UsedWeight(item), Price = item.Price,
                    ReferencePrice = UsedValue(item), ListedAt = item.ListedAt
                });
        private string SetUsedListing(OwnedShop shop, UsedItem item, long price)
        {
            if (item == null || !item.Owned || !item.Inspected || item.Sold)
                return "这件旧物目前无法上架。请确认已收购、已鉴定且尚未售出。";
            if (item.RepairWorkRemaining > 0) return "物品正在修复，请完成后再上架。";
            long priceLimit = UsedListingPriceLimit(item);
            if (price < 1 || price > priceLimit) return $"请将标价设在 ¥1 到 ¥{priceLimit:N0} 之间。";
            if (UsedReserved(shop, item.Id)) return "这件商品已被顾客拿去结账，暂时不能改价或下架。";
            var fill = shop.Sim.ShelfFillOf(ShelfKinds.Get("secondhand"));
            if (fill.CapacityKg <= 0)
                return "请先摆放货架，并将品类设为“二手转售品”。\n\n在店内走到货架旁按 F，选择“选择摆什么”或“换品类”，再从“零售商品”中选“二手转售品”。设置好后，回到收购鉴定台确认上架。";
            if (!item.Listed && (fill.Total >= fill.CapacityItems || fill.UsedKg + UsedWeight(item) > fill.CapacityKg + 1e-9))
                return "二手转售品货架已满，或剩余承重不足。请增加货架或等货架腾出空位。";
            item.Price = price;
            if (!item.Listed) item.ListedAt = Clock.Now.TotalMinutes;
            item.Listed = true;
            return null;
        }
        public bool UnlistUsedItem(int id)
        {
            if(!SpecialPlayer(CurrentShop))return false;
            var item=SpecialBusiness(CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&i.Owned&&!i.Sold);
            if(item==null||UsedReserved(CurrentShop,id))return Fail("顾客正在结账，不能取回商品。");
            item.Listed=false;Say("已取回"+UsedItemName(item)+"，可在收购柜台重新定价上架。");return true;
        }
        private void CompleteUsedSale(OwnedShop shop,ShopCustomer customer)
        {
            if(customer.ResaleId<=0)return;
            var state=SpecialBusiness(shop.Place.Id);
            var item=state.Used.FirstOrDefault(i=>i.Id==customer.ResaleId&&i.Owned&&!i.Sold);
            if(item==null)return;
            RecordUsedSale(state, item, customer.Spend.Yuan);
            double reputation = UsedSaleReputation(item);
            if (reputation > 0) shop.Sim.AdjustPersonalReputation(reputation);
            string reward = reputation > 0 ? $" · 名望 +{reputation:0.#}" : "";
            Say($"{ShopTitle(shop)}：{UsedItemName(item)}已售出，收入 ¥{item.SalePrice}，总成本 ¥{item.Cost}，毛利 ¥{item.SalePrice - item.Cost}{reward}。");
        }

        private void RecordUsedSale(SpecialBusinessState state, UsedItem item, long salePrice)
        {
            item.Sold=true;item.Listed=false;item.SalePrice=salePrice;item.SoldAt=Clock.Now.TotalMinutes;
            state.Paid++;state.Revenue+=item.SalePrice;
        }
    }
}
