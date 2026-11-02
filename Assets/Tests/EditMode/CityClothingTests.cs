using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Market;
namespace PCShopSell.Tests
{
    public sealed class CityClothingTests
    {
        [Test] public void ClothingCatalogHasDistinctAffordableWarmAndLuxuryCoolChoices()
        {
            Assert.AreEqual(24,ClothingProducts.All.Count);
            foreach(int tier in new[]{0,1,2,3})Assert.GreaterOrEqual(Outfits.All.Count(o=>o.Id!="tee"&&o.Tier==tier),4);
            Assert.AreEqual(280,Outfits.Get("shirt").Price.Yuan);Assert.AreEqual(700,Outfits.Get("jacket").Price.Yuan);Assert.AreEqual(1800,Outfits.Get("suit").Price.Yuan);
            Assert.LessOrEqual(Outfits.All.Max(o=>o.CharmBonus),9);
            Assert.LessOrEqual(Outfits.Get("winter_basic").Price.Yuan,299);
            Assert.Less(Outfits.Get("winter_basic").ComfortMinC,Outfits.Get("lux_short").ComfortMinC);
            Assert.GreaterOrEqual(Outfits.All.Select(o=>o.VisualPreset).Distinct().Count(),4);
            foreach(var g in ClothingProducts.All){Assert.AreSame(g,Gifts.Get(g.Id));Assert.IsNotNull(ShelfKinds.ForGift(g.Id));Assert.IsTrue(RetailShopTypes.Sells("clothing",g.Id));Assert.AreEqual(0,g.ShelfDays);}
        }
        [Test] public void SeasonalSelectionNeverChangesBasePricesOrExpiresClothing()
        {
            var winter=Outfits.Get("winter_basic");var before=winter.Price;
            Assert.AreEqual(1.25,ClothingProducts.SeasonDemandAt(winter.ProductId,5));Assert.AreEqual(.65,ClothingProducts.SeasonDemandAt(winter.ProductId,31));
            Assert.AreEqual(before,winter.Price);var stock=new Stock(100);stock.ForceAddGoods(winter.ProductId,2);stock.SpoilGoods(10000);Assert.AreEqual(2,stock.GoodsCount(winter.ProductId));
        }
        [Test] public void InternalUseRecordDoesNotCreateSalesOrMoveCash()
        {
            var ledger=new Ledger(Money.FromYuan(1000));
            ledger.RecordNonCash(new GameDate(1),"库存转自用","one coat",new LedgerAttribution(LedgerBusinessKind.InternalTransfer,shopId:"shop").Product("clothing:winter_basic",1,12000));
            Assert.AreEqual(Money.FromYuan(1000),ledger.Balance);Assert.AreEqual(Money.Zero,ledger.Entries.Single().Amount);Assert.AreEqual(LedgerBusinessKind.InternalTransfer,ledger.Entries.Single().BusinessKind);
        }
        [Test] public void EveryQualityCanBeCombinedWithEachSeasonFilter()
        {
            foreach(int tier in new[]{0,1,2,3})
            foreach(string season in new[]{"春秋装","夏装","冬装"})
                Assert.IsTrue(ClothingProducts.All.Any(g=>ClothingProducts.Outfit(g.Id).Tier==tier && ClothingProducts.SeasonGroup(g.Id)==season),tier+" / "+season);
            Assert.AreEqual("夏装",ClothingProducts.SeasonGroup("clothing:cotton_short"));
            Assert.AreEqual("春秋装",ClothingProducts.SeasonGroup("clothing:shirt"));
            Assert.AreEqual("冬装",ClothingProducts.SeasonGroup("clothing:winter_basic"));
        }
        [TestCase(89,"clothing:shirt")]
        [TestCase(90,"clothing:cotton_short")]
        [TestCase(180,"clothing:shirt")]
        [TestCase(270,"clothing:winter_basic")]
        [TestCase(360,"clothing:shirt")]
        public void SeasonalDemandFollowsTheCalendarAtSeasonAndYearBoundaries(int day,string expected)
        {
            foreach(string id in new[]{"clothing:shirt","clothing:cotton_short","clothing:winter_basic"})
                Assert.AreEqual(id==expected,ClothingProducts.IsInSeason(id,(long)day*1440),id);
        }
        [TestCase(44,true)]
        [TestCase(134,true)]
        [TestCase(224,true)]
        [TestCase(279,true)]
        [TestCase(134,false)]
        public void RealClothingSalesFavorTheSeasonAndStillSellOffSeasonStock(int day,bool stockSeasonal)
        {
            var clock=new GameClock(new GameDate(day),10*60);
            var random=new GameRandom(347);
            using(var shop=new ShopSimulation("seasonal",ShopSize.Large,clock,new Ledger(),new HardwareMarket(random),random,ShopSimulationTests.BusySettings()))
            {
                shop.RetailKind="clothing";shop.PlayerPresent=true;shop.Markup=.9;
                // Keep the temperature identical across seasons to verify the calendar itself affects actual sales.
                shop.RetailTemperature=()=>22;
                foreach(var product in ClothingProducts.All)
                    if(stockSeasonal || !ClothingProducts.IsInSeason(product.Id,clock.Now.TotalMinutes))shop.Stock.ForceAddGoods(product.Id,10000);
                int seasonal=0,other=0;
                for(int minute=0;minute<360;minute++)
                {
                    clock.Advance(1);
                    foreach(var customer in shop.Queue.ToArray())
                    {
                        if(ClothingProducts.IsInSeason(customer.GoodsId,clock.Now.TotalMinutes))seasonal++;else other++;
                        shop.Checkout(customer.Id);
                    }
                }
                Assert.Greater(seasonal+other,100);
                Assert.Greater(other,0,"反季库存仍然可以卖出");
                if(stockSeasonal)Assert.That(seasonal/(double)(seasonal+other),Is.InRange(.6,.8));
                else Assert.AreEqual(0,seasonal);
            }
        }
    }
}
