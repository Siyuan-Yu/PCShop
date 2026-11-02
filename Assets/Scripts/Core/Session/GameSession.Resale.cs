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
        public static double UsedWeight(UsedItem item)=>new[]{1.2,3,.2,1}[Math.Max(0,Math.Min(3,item.Product))];
        public bool UsedReserved(OwnedShop shop,int id)=>shop.Sim.Queue.Any(c=>c.ResaleId==id);
        private IEnumerable<ResaleOffer> UsedOffers(OwnedShop shop)=>shop.RetailKind!="secondhand"?Enumerable.Empty<ResaleOffer>():
            SpecialBusiness(shop.Place.Id).Used.Where(i=>i.Owned&&i.Inspected&&i.Listed&&!i.Sold&&!UsedReserved(shop,i.Id))
                .Select(i=>new ResaleOffer{Id=i.Id,Product=i.Product,Name=UsedNames[i.Product]+" · "+ConditionName(i.Condition),Kg=UsedWeight(i),Price=i.Price,ReferencePrice=UsedValue(i)});
        private bool SetUsedListing(OwnedShop shop,UsedItem item,long price)
        {
            if(item==null||!item.Owned||!item.Inspected||item.Sold||price<1||price>1000000)return false;
            if(UsedReserved(shop,item.Id))return Fail("这件商品已被顾客拿去结账，暂时不能改价或下架。");
            var fill=shop.Sim.ShelfFillOf(ShelfKinds.Get("secondhand"));
            if(fill.CapacityKg<=0)return Fail("请先摆放货架，并将品类设为“二手转售品”。");
            if(!item.Listed&&(fill.Total>=fill.CapacityItems||fill.UsedKg+UsedWeight(item)>fill.CapacityKg+1e-9))return Fail("二手转售品货架已满，请增加货架或先售出一些商品。");
            item.Price=price;item.Listed=true;return true;
        }
        public bool UnlistUsedItem(int id)
        {
            if(!SpecialPlayer(CurrentShop))return false;
            var item=SpecialBusiness(CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&i.Owned&&!i.Sold);
            if(item==null||UsedReserved(CurrentShop,id))return Fail("顾客正在结账，不能取回商品。");
            item.Listed=false;Say("已取回"+UsedNames[item.Product]+"，可在收购柜台重新定价上架。");return true;
        }
        private void CompleteUsedSale(OwnedShop shop,ShopCustomer customer)
        {
            if(customer.ResaleId<=0)return;
            var state=SpecialBusiness(shop.Place.Id);
            var item=state.Used.FirstOrDefault(i=>i.Id==customer.ResaleId&&i.Owned&&!i.Sold);
            if(item==null)return;
            item.Sold=true;item.Listed=false;item.SalePrice=customer.Spend.Yuan;item.SoldAt=Clock.Now.TotalMinutes;
            state.Paid++;state.Revenue+=item.SalePrice;
            Say(ShopTitle(shop)+"："+UsedNames[item.Product]+"已售出，收入 ¥"+item.SalePrice+"，收购成本 ¥"+item.Cost+"，毛利 ¥"+(item.SalePrice-item.Cost)+"。");
        }
    }
}
