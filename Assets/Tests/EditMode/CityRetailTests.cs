using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Tests
{
    public sealed class CityRetailTests
    {
        [Test]
        public void ClothingShopCanConfigureEveryCategoryAndListSecondaryGoods()
        {
            using(var s=GameSessionTests.Make(100000,9*60))
            {
                GameSessionTests.Teleport(s,"pc");Assert.IsTrue(s.SetShopMainCategory("clothing"));
                s.PlayerShop.Stock.Clear();
                int shelf=s.CurrentShop.Layout.Count;s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});
                int display=s.CurrentShop.Layout.Count;s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.ShopDisplay,Item="computer"});
                foreach(var kind in ShelfKinds.All)
                {
                    Assert.IsTrue(s.SetShelfKind(shelf,kind.Id),kind.Id);
                    Assert.IsTrue(s.SetDisplayKind(display,kind.Id),kind.Id);
                }
                Assert.IsTrue(s.SetDisplayKind(display,"computer"));
                Assert.IsTrue(s.SetShelfKind(shelf,"drink"));
                s.Backpack.AddGift(Gifts.Soda,1);
                Assert.IsTrue(s.ListGoods(Gifts.Soda));
                s.Backpack.AddGift(Gifts.Soda,1);
                Assert.IsTrue(s.UnloadBackpack());
                Assert.AreEqual(2,s.PlayerShop.Stock.GoodsCount(Gifts.Soda));
                Assert.AreEqual(2,s.PlayerShop.OnShelfGoods(Gifts.Soda));
                using(var loaded=GameSession.Load(s.City,s.CreateSave()))
                {
                    Assert.AreEqual("clothing",loaded.RetailKind);
                    Assert.AreEqual("drink",loaded.ShelfKindAt(shelf).Id);
                    Assert.AreEqual(2,loaded.PlayerShop.Stock.GoodsCount(Gifts.Soda));
                }
            }
        }
        [TestCase("clothing","clothing:cotton_short","flower_rose",true)]
        [TestCase("flowers","flower_rose","clothing:cotton_short",true)]
        [TestCase("clothing","clothing:cotton_short","flower_rose",false)]
        public void MainCategoryBiasesSalesWithoutBanningOtherDisplayedGoods(string kind,string primary,string other,bool primaryInStock)
        {
            var clock=new GameClock(new GameDate(0),10*60);var random=new GameRandom(523);
            using(var shop=new ShopSimulation("mixed",ShopSize.Large,clock,new Ledger(),new HardwareMarket(random),random,ShopSimulationTests.BusySettings()))
            {
                shop.RetailKind=kind;shop.PlayerPresent=true;shop.Markup=.9;
                if(primaryInStock)shop.Stock.ForceAddGoods(primary,10000);
                shop.Stock.ForceAddGoods(other,10000);
                int mainSales=0,otherSales=0;
                for(int i=0;i<360;i++)
                {
                    clock.Advance(1);
                    foreach(var customer in shop.Queue.ToArray())
                    {
                        if(customer.GoodsId==primary)mainSales++;else if(customer.GoodsId==other)otherSales++;
                        shop.Checkout(customer.Id);
                    }
                }
                Assert.Greater(otherSales,0,"其他陈列商品也应有成交");
                if(primaryInStock)Assert.That(mainSales/(double)(mainSales+otherSales),Is.InRange(.65,.9));
                else{Assert.AreEqual(0,mainSales);Assert.Greater(otherSales,100);}
            }
        }
        [Test]
        public void ClothingShopCanAlsoSellDisplayedComputerParts()
        {
            var clock=new GameClock(new GameDate(0),10*60);var random=new GameRandom(731);
            using(var shop=new ShopSimulation("mixedparts",ShopSize.Large,clock,new Ledger(),new HardwareMarket(random),random,ShopSimulationTests.BusySettings()))
            {
                shop.RetailKind="clothing";shop.PlayerPresent=true;shop.Markup=.9;
                shop.Stock.ForceAddGoods("clothing:cotton_short",10000);
                foreach(var category in PartCatalog.Categories)shop.Stock.ForceAdd(PartCatalog.InCategory(category).First(),true,10000);
                int clothes=0,parts=0;
                for(int i=0;i<360;i++)
                {
                    clock.Advance(1);
                    foreach(var customer in shop.Queue.ToArray())
                    {
                        if(customer.Item!=null)parts++;else if(customer.GoodsId!=null)clothes++;
                        shop.Checkout(customer.Id);
                    }
                }
                Assert.Greater(parts,10,"主售服装也不能屏蔽配件成交");Assert.Greater(clothes,parts);
            }
        }
        [Test]
        public void ClothingShopCanAlsoSellCompletedDisplayComputers()
        {
            var clock=new GameClock(new GameDate(0),10*60);var random=new GameRandom(732);
            using(var shop=new ShopSimulation("mixedcomputers",ShopSize.Large,clock,new Ledger(),new HardwareMarket(random),random,ShopSimulationTests.BusySettings()))
            {
                shop.RetailKind="clothing";shop.PlayerPresent=true;shop.Markup=.9;shop.FixedDisplayDesks=1;
                shop.Stock.ForceAddGoods("clothing:cotton_short",10000);shop.RestoreDisplay(new[]{0});
                int clothes=0,computers=0;
                for(int i=0;i<360;i++)
                {
                    clock.Advance(1);
                    foreach(var customer in shop.Queue.ToArray())
                    {
                        if(customer.DisplayTier>=0)computers++;else if(customer.GoodsId!=null)clothes++;
                        shop.Checkout(customer.Id);
                    }
                    if(shop.DisplayPcs.Count==0)shop.RestoreDisplay(new[]{0});
                }
                Assert.Greater(computers,10);Assert.Greater(clothes,computers);
            }
        }
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
                s.PlayerShop.NotFitted=false;s.PlayerShop.ForcedClosed=false;s.PlayerShop.PlayerPresent=true;s.PlayerShop.SetHours(0,1440);
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
