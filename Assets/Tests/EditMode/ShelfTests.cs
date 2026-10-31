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
    /// <summary>货架：所有货架一样，每个摆一种货，按重量装；顾客只买货架上摆着的。</summary>
    public class ShelfTests
    {
        private static ShopSimulation MakeSim(out GameClock clock, ShopSettings settings = null)
        {
            var rng = new GameRandom(5);
            clock = new GameClock(new GameDate(0), 10 * 60);
            var shop = new ShopSimulation("s", ShopSize.Large, clock, new Ledger(), new HardwareMarket(rng), rng, settings ?? new ShopSettings());
            shop.Markup = 1.0;
            shop.PlayerPresent = true;
            return shop;
        }

        private static PartDef Part(PartCategory c, int tier) => PartCatalog.InCategory(c).First(p => p.Tier == tier);

        // ---------- 按重量装 ----------

        [Test]
        public void Shelf_HoldsSixtyKg_BoxedCpuIsTwoKg()
        {
            ShopSimulation shop = MakeSim(out _);
            PartDef cpu = Part(PartCategory.Cpu, 1);
            shop.Stock.ForceAdd(cpu, true, 100);
            shop.SetShelfCapacity(new Dictionary<string, double> { { "cpu", 60 } });
            ShelfFill f = shop.ShelfFillOf(PartCategory.Cpu);
            Assert.AreEqual(30, f.Total, "60 公斤 / 2 公斤 = 30 盒");
            Assert.AreEqual(60, f.UsedKg, 1e-6);
            Assert.AreEqual(100, shop.Stock.CountAll(cpu), "后仓的货没动");
        }

        [Test]
        public void LooseWeighsHalfOfBoxed_AndBoxedGpuIsFourKg()
        {
            ShopSimulation shop = MakeSim(out _);
            PartDef cpu = Part(PartCategory.Cpu, 1);
            PartDef gpu = Part(PartCategory.Gpu, 1);
            Assert.AreEqual(2.0, ShelfPlanner.PartKg(cpu, true), 1e-9);
            Assert.AreEqual(1.0, ShelfPlanner.PartKg(cpu, false), 1e-9, "散件重量是盒装的一半");
            Assert.AreEqual(4.0, ShelfPlanner.PartKg(gpu, true), 1e-9, "显卡体积翻倍");
            shop.Stock.ForceAdd(cpu, false, 200);
            shop.Stock.ForceAdd(gpu, true, 100);
            shop.SetShelfCapacity(new Dictionary<string, double> { { "cpu", 60 }, { "gpu", 60 } });
            Assert.AreEqual(60, shop.ShelfFillOf(PartCategory.Cpu).Total, "60 件散件");
            Assert.AreEqual(15, shop.ShelfFillOf(PartCategory.Gpu).Total, "15 盒显卡");
        }

        [Test]
        public void BoxedAndLoose_AreTheSameKind_AndModelsShareTheShelf()
        {
            ShopSimulation shop = MakeSim(out _);
            PartDef a = Part(PartCategory.Cpu, 0), b = Part(PartCategory.Cpu, 2);
            shop.Stock.ForceAdd(a, true, 50);
            shop.Stock.ForceAdd(b, false, 50);
            shop.SetShelfCapacity(new Dictionary<string, double> { { "cpu", 60 } });
            ShelfFill f = shop.ShelfFillOf(PartCategory.Cpu);
            Assert.Greater(f.PartCount(a, true), 0, "盒装的上了架");
            Assert.Greater(f.PartCount(b, false), 0, "散件的也上了架（同一种货）");
            Assert.LessOrEqual(f.UsedKg, 60 + 1e-9);
        }

        [Test]
        public void ShelfKinds_Goods_GroupSimilarGifts()
        {
            Assert.AreEqual("smoke", ShelfKinds.ForGift(Gifts.Cig).Id);
            Assert.AreEqual("smoke", ShelfKinds.ForGift(Gifts.CigHi).Id);
            Assert.AreEqual("liquor", ShelfKinds.ForGift(Gifts.Wine).Id);
            Assert.AreEqual("liquor", ShelfKinds.ForGift(Gifts.Baijiu).Id);
            Assert.AreEqual("drink", ShelfKinds.ForGift(Gifts.Drink).Id);
            Assert.AreEqual("drink", ShelfKinds.ForGift(Gifts.Soda).Id);
            foreach (GiftDef g in Gifts.All) Assert.IsNotNull(ShelfKinds.ForGift(g.Id), g.Id + " 要有对应的货架类");
            Assert.AreEqual(11, ShelfKinds.All.Count);

            ShopSimulation shop = MakeSim(out _);
            shop.Stock.ForceAddGoods(Gifts.Cig, 500); // 0.2 公斤一条
            shop.SetShelfCapacity(new Dictionary<string, double> { { "smoke", 60 } });
            Assert.AreEqual(300, shop.ShelfFillOf(ShelfKinds.Get("smoke")).Total, "60 公斤 = 300 条烟");
        }

        // ---------- 顾客只买架上的 ----------

        [Test]
        public void Customers_OnlyBuyKindsThatHaveAShelf()
        {
            ShopSettings s = ShopSimulationTests.BusySettings();
            ShopSimulation shop = MakeSim(out GameClock clock, s);
            PartDef gpu = Part(PartCategory.Gpu, 1);
            foreach (PartDef p in PartCatalog.InCategory(PartCategory.Cpu)) shop.Stock.ForceAdd(p, true, 20);
            shop.Stock.ForceAdd(gpu, true, 20);
            shop.SetShelfCapacity(new Dictionary<string, double> { { "cpu", 60 } }); // 只有处理器货架
            clock.Advance(180);
            Assert.AreEqual(20, shop.Stock.CountAll(gpu), "没有显卡货架，后仓里的显卡一张都卖不出去");
            Assert.Less(PartCatalog.InCategory(PartCategory.Cpu).Sum(p => shop.Stock.CountAll(p)), 60, "处理器卖出去了");
        }

        [Test]
        public void ShelfRefillsFromBackroom_AsItSells()
        {
            ShopSimulation shop = MakeSim(out _);
            PartDef cpu = Part(PartCategory.Cpu, 1);
            shop.Stock.ForceAdd(cpu, true, 100);
            shop.SetShelfCapacity(new Dictionary<string, double> { { "cpu", 60 } });
            Assert.AreEqual(30, shop.OnShelf(cpu));
            shop.Stock.TryRemove(cpu, true, 10); // 卖了 10 盒
            Assert.AreEqual(30, shop.OnShelf(cpu), "货架空出来就从后仓补");
            shop.Stock.TryRemove(cpu, true, 70);
            Assert.AreEqual(20, shop.OnShelf(cpu), "后仓只剩 20 盒了");
        }

        [Test]
        public void NoShelvesConfigured_EverythingInStockIsSellable()
        {
            ShopSimulation shop = MakeSim(out _);
            PartDef cpu = Part(PartCategory.Cpu, 1);
            shop.Stock.ForceAdd(cpu, true, 100);
            Assert.IsFalse(shop.ShelvesManaged);
            Assert.AreEqual(100, shop.OnShelf(cpu));
        }

        // ---------- 会话：设置货架 ----------

        [Test]
        public void StarterShop_HasSixShelves_OnePerPartCategory()
        {
            CityLayoutData data = GameSessionTests.Layout();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, true);
            Assert.AreEqual(6, s.ShelvesPlaced);
            var kinds = s.ShelfIndexes().Select(i => s.ShelfKindAt(i).Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "gpu", "cpu", "memory", "storage", "powercase", "motherboard" }, kinds);
            Assert.AreEqual(ShelfKinds.CapacityKg, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("gpu")), 1e-9);
            Assert.AreEqual(0, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("smoke")), 1e-9);
        }

        [Test]
        public void SetShelfKind_ChangesWhatTheShelfHolds_AndTwoShelvesDoubleTheRoom()
        {
            CityLayoutData data = GameSessionTests.Layout();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, true);
            GameSessionTests.Teleport(s, "pc");
            int memShelf = s.ShelfIndexes().First(i => s.ShelfKindAt(i).Id == "memory");
            int gpuShelf = s.ShelfIndexes().First(i => s.ShelfKindAt(i).Id == "gpu");
            Assert.AreEqual(1, s.ShelvesOfKind("gpu"));
            Assert.IsTrue(s.SetShelfKind(memShelf, "gpu"));
            Assert.AreEqual(2, s.ShelvesOfKind("gpu"));
            Assert.AreEqual(0, s.ShelvesOfKind("memory"));
            Assert.AreEqual(120, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("gpu")), 1e-9, "两个显卡货架 = 120 公斤");
            Assert.AreEqual(0, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("memory")), 1e-9);
            Assert.IsTrue(s.SetShelfKind(gpuShelf, null));
            Assert.IsNull(s.ShelfKindAt(gpuShelf));
            Assert.AreEqual(60, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("gpu")), 1e-9);
            Assert.IsFalse(s.SetShelfKind(gpuShelf, "no_such_kind"));
        }

        [Test]
        public void ShelfKinds_SurviveSaveAndLoad()
        {
            CityLayoutData data = GameSessionTests.Layout();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, true);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.SetShelfKind(s.ShelfIndexes()[0], "fruit"));
            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(1, loaded.ShelvesOfKind("fruit"));
            Assert.AreEqual(ShelfKinds.CapacityKg, loaded.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("fruit")), 1e-9);
        }

        [Test]
        public void OldSave_CategoryShelves_BecomeTypedGenericShelves()
        {
            CityLayoutData data = GameSessionTests.Layout();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, true);
            SaveData d = s.CreateSave();
            foreach (SavePlaced p in d.home.shopLayout)
            {
                if (p.id != FurnitureCatalog.Shelf) continue;
                p.id = "shelf_" + p.item; // 老存档：一类一种货架，没有 item 字段
                p.item = null;
            }
            d.home.stockIds = new[] { "shelf_gpu", "shelf_goods" };
            d.home.stockCounts = new[] { 2, 1 };
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.AreEqual(6, loaded.ShelvesPlaced);
            CollectionAssert.AreEquivalent(new[] { "gpu", "cpu", "memory", "storage", "powercase", "motherboard" },
                loaded.ShelfIndexes().Select(i => loaded.ShelfKindAt(i).Id).ToList());
            Assert.IsTrue(loaded.ShopFitted);
            Assert.AreEqual(3, loaded.FurnitureStored(FurnitureCatalog.Shelf), "买了没摆的老货架也都是通用货架");
        }

        [Test]
        public void ShelfIsOneItem_AtTheMall_ForThreeHundred()
        {
            FurnitureDef d = FurnitureCatalog.Get(FurnitureCatalog.Shelf);
            Assert.AreEqual(Money.FromYuan(300), d.Price);
            Assert.AreEqual(d, FurnitureCatalog.Get("shelf_gpu"), "老 id 当成现在的货架");
            Assert.AreEqual(3, FurnitureCatalog.Purchasable(true).Count(x => x.Fixture == FixtureKind.Shelf), "普通 + 加固 + 重型");
        }

        [Test]
        public void BetterShelves_HoldMore_AndEnlargeTheBackroom()
        {
            GameSession s = GameSessionTests.Make(50000);
            GameSessionTests.Teleport(s, "pc");
            s.DebugFitShop();
            double baseCap = s.PlayerShop.Stock.Capacity;
            int idx = s.ShelfIndexes()[0];
            Assert.IsTrue(s.SetShelfKind(idx, "gpu"), "a");
            double gpuBase = s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("gpu"));

            // 把第一个货架换成重型货架（同位置）
            Assert.IsTrue(s.BeginRenovation());
            PlacedFurniture old = s.ShopLayout[idx].Clone();
            Assert.IsTrue(s.StoreFurniture(true, idx), "b");
            s.DebugGiveFurniture(FurnitureCatalog.RackHeavy, 1);
            PlacedFurniture at = RoomPlanner.Snap(s.ShopRoom, FurnitureCatalog.Get(FurnitureCatalog.RackHeavy), old.X, old.Z, old.Rot);
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.RackHeavy, at), "c");
            int heavy = s.ShelfIndexes().First(i => s.ShopLayout[i].Id == FurnitureCatalog.RackHeavy);
            Assert.IsTrue(s.SetShelfKind(heavy, "gpu"), "d");
            Assert.AreEqual(gpuBase - ShelfKinds.CapacityKg + 160, s.PlayerShop.ShelfCapacityOf(ShelfKinds.Get("gpu")), 1e-9);
            Assert.AreEqual(baseCap + 20, s.PlayerShop.Stock.Capacity, 1e-9, "重型货架让后仓大 20 格");
            Assert.AreEqual(6, s.ShopFitList().Single(n => n.Kind == FixtureKind.Shelf).Placed, "三档货架都算开店清单里的货架");
        }
    }
}
