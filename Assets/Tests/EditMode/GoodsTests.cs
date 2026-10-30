using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>杂货：礼品表里的东西都能摆上杂货架卖，只是来买配件的顾客多半不买。</summary>
    public class GoodsTests
    {
        private sealed class Rig
        {
            public GameClock Clock;
            public Ledger Ledger;
            public ShopSimulation Shop;
        }

        private static Rig MakeSim(ShopSettings settings)
        {
            var rng = new GameRandom(3);
            var r = new Rig { Clock = new GameClock(new GameDate(0), 10 * 60), Ledger = new Ledger() };
            r.Shop = new ShopSimulation("s", ShopSize.Large, r.Clock, r.Ledger, new HardwareMarket(rng), rng, settings);
            r.Shop.Markup = 1.0;
            r.Shop.PlayerPresent = true;
            return r;
        }

        [Test]
        public void Stock_HoldsGoods_UsesSpaceByWeight_AndSavesNothingElse()
        {
            var st = new Stock(10);
            Assert.IsTrue(st.IsEmpty);
            Assert.IsTrue(st.TryAddGoods(Gifts.Soda, 4));
            Assert.AreEqual(4, st.GoodsCount(Gifts.Soda));
            Assert.AreEqual(1.0, st.UsedSpace, 1e-6, "一瓶饮料 0.5 公斤 = 0.25 格");
            Assert.IsFalse(st.IsEmpty);
            Assert.IsFalse(st.TryAddGoods(Gifts.Drink, 4), "一箱饮料 3 格，4 箱放不下");
            Assert.IsFalse(st.TryAddGoods("no_such_gift", 1));
            Assert.IsTrue(st.TryRemoveGoods(Gifts.Soda, 4));
            Assert.IsTrue(st.IsEmpty);
        }

        [Test]
        public void GoodsPrice_FollowsMarkup_AndOwnMarkup()
        {
            Rig r = MakeSim(new ShopSettings());
            Assert.AreEqual(Money.FromYuan(100), r.Shop.GoodsPriceOf(Gifts.Cig));
            r.Shop.SetGoodsMarkup(Gifts.Cig, 1.5);
            Assert.AreEqual(Money.FromYuan(150), r.Shop.GoodsPriceOf(Gifts.Cig));
            Assert.IsTrue(r.Shop.HasGoodsMarkup(Gifts.Cig));
            r.Shop.SetGoodsMarkup(Gifts.Cig, null);
            Assert.AreEqual(Money.FromYuan(100), r.Shop.GoodsPriceOf(Gifts.Cig));
        }

        private static readonly IReadOnlyDictionary<string, double> DrinkShelf = new Dictionary<string, double> { { "drink", ShelfKinds.CapacityKg } };

        [Test]
        public void Goods_NeedAShelfOfThatKind_ToSell()
        {
            ShopSettings s = ShopSimulationTests.BusySettings();
            s.GoodsImpulseChance = 1;
            s.GoodsBuyFactor = 5;
            Rig r = MakeSim(s);
            r.Shop.SetShelfCapacity(new Dictionary<string, double> { { "smoke", 60 } }); // 只有烟的货架，没有饮料的
            r.Shop.Stock.ForceAddGoods(Gifts.Soda, 5);
            r.Clock.Advance(3);
            Assert.AreEqual(5, r.Shop.Stock.GoodsCount(Gifts.Soda), "没有饮料货架，后仓里的饮料卖不出去");
            Assert.IsTrue(r.Shop.Queue.All(c => c.GoodsId == null));
        }

        [Test]
        public void Goods_SellThroughCheckout_AndReturnWhenCustomerLeaves()
        {
            ShopSettings s = ShopSimulationTests.BusySettings();
            s.GoodsImpulseChance = 1;
            s.GoodsBuyFactor = 5;
            Rig r = MakeSim(s);
            r.Shop.SetShelfCapacity(DrinkShelf);
            r.Shop.Stock.ForceAddGoods(Gifts.Soda, 50);
            r.Clock.Advance(3);
            Assert.Greater(r.Shop.Queue.Count, 0);
            ShopCustomer c = r.Shop.Queue[0];
            Assert.AreEqual(Gifts.Soda, c.GoodsId);
            Assert.AreEqual(r.Shop.GoodsPriceOf(Gifts.Soda), c.Spend);
            Assert.AreEqual("一瓶饮料", c.ItemName);
            Assert.AreEqual(50 - r.Shop.Queue.Count, r.Shop.Stock.GoodsCount(Gifts.Soda), "排队时已经从货架上拿下");

            Money before = r.Ledger.Balance;
            Assert.GreaterOrEqual(r.Shop.Checkout(c.Id), 0);
            Assert.AreEqual(before + c.Spend, r.Ledger.Balance);

            // 等太久的顾客走了：货放回货架
            r.Clock.Advance(3);
            ShopCustomer d = r.Shop.Queue.First();
            int shelf = r.Shop.Stock.GoodsCount(Gifts.Soda);
            r.Shop.Settings.BaseCustomersPerHour = 0;
            r.Clock.Advance(10);
            Assert.AreEqual(CustomerState.Left, d.State);
            Assert.Greater(r.Shop.Stock.GoodsCount(Gifts.Soda), shelf);
        }

        [Test]
        public void Goods_RarelyBought_ByPartsShoppers()
        {
            ShopSettings s = ShopSimulationTests.BusySettings(); // 默认的杂货兴趣
            Rig r = MakeSim(s);
            r.Shop.SetShelfCapacity(DrinkShelf);
            foreach (PartDef p in PartCatalog.All) r.Shop.Stock.ForceAdd(p, true, 500);
            r.Shop.Stock.ForceAddGoods(Gifts.Soda, 100);
            r.Clock.Advance(240);
            int goodsSold = 100 - r.Shop.Stock.GoodsCount(Gifts.Soda);
            Assert.Greater(r.Shop.TodayCustomers, 200);
            Assert.Less(goodsSold, r.Shop.TodayCustomers * 0.1, "来买配件的人多半不买杂货");
        }

        [Test]
        public void Goods_StoredInLocker_ListedAndUnlisted()
        {
            GameSession s = FixtureLike.ShopWithDrinkShelf();
            s.ShopLocker.AddGift(Gifts.Soda, 3);
            s.Backpack.AddGift(Gifts.Soda, 2);
            Assert.AreEqual(5, s.GoodsAvailableToList(Gifts.Soda));
            Assert.IsTrue(s.ListGoods(Gifts.Soda, 4));
            Assert.AreEqual(4, s.PlayerShop.Stock.GoodsCount(Gifts.Soda));
            Assert.AreEqual(4, s.PlayerShop.OnShelfGoods(Gifts.Soda), "饮料货架自动摆上");
            Assert.AreEqual(0, s.ShopLocker.GiftCount(Gifts.Soda), "先用储物柜里的");
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.Soda));
            Assert.IsFalse(s.ListGoods(Gifts.Soda, 3), "手边只剩 1 件");
            Assert.IsTrue(s.UnlistGoods(Gifts.Soda, 2));
            Assert.AreEqual(2, s.PlayerShop.Stock.GoodsCount(Gifts.Soda));
            Assert.AreEqual(2, s.ShopLocker.GiftCount(Gifts.Soda));
        }

        [Test]
        public void ListGoods_WithoutAShelfOfThatKind_StillStores_ButNothingIsOnShelf()
        {
            GameSession s = FixtureLike.ShopWithDrinkShelf();
            s.ShopLocker.AddGift(Gifts.Cig, 3);
            Assert.IsTrue(s.ListGoods(Gifts.Cig, 2));
            Assert.AreEqual(2, s.PlayerShop.Stock.GoodsCount(Gifts.Cig));
            Assert.AreEqual(0, s.PlayerShop.OnShelfGoods(Gifts.Cig), "没有摆烟的货架，顾客买不到");
            // 把货架改成摆烟：自动摆上
            Assert.IsTrue(s.SetShelfKind(s.ShelfIndexes()[0], "smoke"));
            Assert.AreEqual(2, s.PlayerShop.OnShelfGoods(Gifts.Cig));
        }

        [Test]
        public void Goods_SaveAndLoad_KeepStockPricesAndShelfKind()
        {
            GameSession s = FixtureLike.ShopWithDrinkShelf();
            s.PlayerShop.Stock.ForceAddGoods(Gifts.Fruit, 2);
            s.SetGoodsMarkup(Gifts.Fruit, 1.3);
            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(2, loaded.PlayerShop.Stock.GoodsCount(Gifts.Fruit));
            Assert.AreEqual(1.3, loaded.PlayerShop.MarkupOfGoods(Gifts.Fruit), 1e-6);
            Assert.AreEqual(1, loaded.ShelvesOfKind("drink"));
            Assert.AreEqual(ShelfKinds.CapacityKg, loaded.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("drink")), 1e-9);
        }
    }

    /// <summary>GoodsTests 用的现成店：标准设施已摆好，再摆一个设成饮料的货架。</summary>
    internal static class FixtureLike
    {
        public static GameSession ShopWithDrinkShelf()
        {
            CityLayoutData data = GameSessionTests.Layout();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, true);
            PackTests.EmptyBag(s);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.InPlayerShop);
            Assert.IsTrue(s.BeginRenovation());
            // 标准设施的货架全清掉，只摆一个新买的
            for (int i = s.ShopLayout.Count - 1; i >= 0; i--)
                if (FurnitureCatalog.Get(s.ShopLayout[i].Id).Fixture == FixtureKind.Shelf) Assert.IsTrue(s.StoreFurniture(true, i));
            Assert.AreEqual(0, s.ShelvesPlaced);
            PlacedFurniture p = RoomPlanner.Snap(s.ShopRoom, FurnitureCatalog.Get(FurnitureCatalog.Shelf), 2.0, 0.2, 0);
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.Shelf, p));
            Assert.AreEqual(1, s.ShelvesPlaced);
            Assert.IsTrue(s.SetShelfKind(s.ShelfIndexes()[0], "drink"));
            return s;
        }
    }
}