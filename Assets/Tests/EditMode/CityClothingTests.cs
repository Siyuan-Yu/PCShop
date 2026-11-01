using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
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
    }
}
