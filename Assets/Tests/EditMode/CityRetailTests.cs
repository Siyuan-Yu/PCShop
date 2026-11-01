using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class CityRetailTests
    {
        [Test]
        public void ModelsUseTheRealGoodsStockAndDistinctSeriesShelves()
        {
            var stock = new Stock(100);
            foreach (var product in ModelProducts.All)
            {
                Assert.AreSame(product, Gifts.Get(product.Id));
                Assert.IsTrue(RetailShopTypes.Sells("models", product.Id));
                Assert.IsNotNull(ShelfKinds.ForGift(product.Id));
                stock.ForceAddGoods(product.Id, 1);
            }
            Assert.AreEqual(12, stock.GoodsTotal);
            Assert.AreEqual(4, ModelProducts.All.Select(p => ShelfKinds.ForGift(p.Id).Id).Distinct().Count());
            Assert.IsFalse(RetailShopTypes.Sells("models", Gifts.Fruit));
            Assert.AreEqual(4, ModelProducts.Limited.Count());
        }
        [Test]
        public void CompetitionExpiresAndDoesNotDirectlyModifyCash()
        {
            using (var s = GameSessionTests.Make())
            {
                var before = s.Ledger.Balance;
                s.Clock.Restore(new GameDate(14), 10 * 60); s.TickCityRetail();
                Assert.AreEqual(1, s.CityCompetitions.Count);
                Assert.Less(s.MainShop.Sim.CityRetailDemandMultiplier, 1);
                Assert.AreEqual(before, s.Ledger.Balance);
                s.Clock.Restore(new GameDate(17), 10 * 60); s.TickCityRetail();
                Assert.AreEqual(1, s.MainShop.Sim.CityRetailDemandMultiplier);
                Assert.IsTrue(s.CityCompetitions[0].Ended);
                var save = new SaveData(); s.SaveCityRetail(save); s.LoadCityRetail(save); s.LoadCityRetail(save);
                Assert.AreEqual(1, s.CityCompetitions.Count);
                Assert.AreEqual(before, s.Ledger.Balance);
            }
        }
    }
}
