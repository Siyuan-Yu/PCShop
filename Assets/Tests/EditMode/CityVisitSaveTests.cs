using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class CityVisitSaveTests
    {
        [Test]
        public void RestoreWaitingVisitDoesNotDuplicateGoodsOrReplayArrival()
        {
            var clock = new GameClock(new GameDate(0), 600); var ledger = new Ledger();
            var rng = new GameRandom(7); var market = new HardwareMarket(rng);
            using (var shop = new ShopSimulation("test-shop", ShopSize.Small, clock, ledger, market, rng))
            {
                string goods = ModelProducts.All[0].Id;
                shop.Stock.ForceAddGoods(goods, 1); Assert.IsTrue(shop.Stock.TryRemoveGoods(goods, 1));
                int arrivals = 0; shop.CustomerArrived += _ => arrivals++;
                var data = new CitySavedShopVisits { shopId = "test-shop", nextCustomerId = 9, queue = new[] { 7 },
                    customers = new[] { new CitySavedCustomer { id = 7, intent = (int)CustomerIntent.Goods,
                        category = (int)PartCategory.Cpu, tier = 0, state = (int)CustomerState.WaitingCheckout,
                        personId = "same-person", visitId = "same-visit", goodsId = goods,
                        arrived = 580, ready = 590, waiting = 600, spendCents = 4800, cash = true, goodsExpiry = -1,
                        interest = "harbor", budgetCents = 8000, preferenceAssigned = true } } };
                shop.RestoreCityCustomers(data);
                shop.RestoreCityCustomers(shop.SaveCityCustomers());
                Assert.AreEqual(0, shop.Stock.GoodsCount(goods), "The held item remains held, not also in the warehouse.");
                Assert.AreEqual(1, shop.Queue.Count); Assert.AreEqual(1, shop.ActiveCustomers.Count);
                Assert.AreEqual("same-person", shop.Queue[0].PersonId); Assert.AreEqual("same-visit", shop.Queue[0].VisitId);
                Assert.AreEqual(0, arrivals); Assert.AreEqual(0, ledger.Entries.Count);
                Assert.GreaterOrEqual(shop.Checkout(7), 0);
                Assert.AreEqual(-1, shop.Checkout(7));
                Assert.AreEqual(Money.FromYuan(48), ledger.Balance); Assert.AreEqual(1, ledger.Entries.Count);
            }
        }
    }
}
