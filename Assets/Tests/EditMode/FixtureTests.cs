using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>租来的铺子是空的：货架、收银台、展示桌、操作台要自己买、自己摆，摆好第二天才能开业；中介费能凭熟络度打折。</summary>
    public class FixtureTests
    {
        private static GameSession NoShop(long cash = 30000)
        {
            CityLayoutData data = GameSessionTests.Layout();
            var places = data.places.ToList();
            places.Add(new LayoutPlaceData { id = "mall", name = "家具城", kind = "shop", node = "dl", size = "large", business = GameSession.MallBusiness });
            data.places = places.ToArray();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, false);
            PackTests.EmptyBag(s);
            return s;
        }

        /// <summary>店里摆的东西里除去租下时送的纸箱。</summary>
        private static int NonBoxes(IEnumerable<PlacedFurniture> layout) => layout.Count(p => p.Id != FurnitureCatalog.ShopBox);

        private static GameSession Rented(long cash = 30000)
        {
            GameSession s = NoShop(cash);
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("pc"));
            return s;
        }

        private static void WaitDays(GameSession s, int day)
        {
            while (s.Clock.Today.TotalDays < day) s.AdvanceClock(60);
        }

        /// <summary>网购一整套最低要求的设施（收银台 + 一个显卡货架），等 2 天送到。</summary>
        private static void BuyMinimum(GameSession s)
        {
            // 第一次租铺子房东送了 1 货架 + 1 收银台，最低开业配置不用再买
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.ShopCounter));
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.Shelf));
        }

        private static PlacedFurniture At(GameSession s, string id, double x, double z, int rot) =>
            RoomPlanner.Snap(s.ShopRoom, FurnitureCatalog.Get(id), x, z, rot);

        [Test]
        public void Catalog_HasShelfTiers_AndTheOtherFixtures()
        {
            var shelves = FurnitureCatalog.All.Where(d => d.Fixture == FixtureKind.Shelf).ToList();
            Assert.AreEqual(3, shelves.Count, "普通 / 加固 / 重型三档货架，摆什么都自己设置");
            Assert.AreEqual(FurnitureCatalog.Shelf, shelves[0].Id);
            Assert.IsTrue(shelves[0].ShelfKg < shelves[1].ShelfKg && shelves[1].ShelfKg < shelves[2].ShelfKg);
            foreach (FurnitureDef d in FurnitureCatalog.All.Where(x => x.IsFixture))
            {
                Assert.IsTrue(d.ForShop, d.Id);
                Assert.IsFalse(d.Starter, d.Id);
                Assert.AreEqual(FurnitureSlot.Floor, d.Slot, d.Id);
                Assert.IsTrue(FurnitureCatalog.Purchasable(true).Contains(d), d.Id + " 要能在家具城买到");
            }
            Assert.AreEqual(1, FurnitureCatalog.All.Count(d => d.Fixture == FixtureKind.Counter));
            Assert.AreEqual(1, FurnitureCatalog.All.Count(d => d.Fixture == FixtureKind.Display));
            Assert.AreEqual(1, FurnitureCatalog.All.Count(d => d.Fixture == FixtureKind.Bench));
            Assert.IsTrue(FurnitureCatalog.Get(FurnitureCatalog.ShopCounter).IsHeavy, "收银台太重，得叫货车");
        }

        [Test]
        public void DefaultShopFixtures_FitEverySize_WithoutOverlapping()
        {
            foreach (ShopSize size in new[] { ShopSize.Small, ShopSize.Medium, ShopSize.Large })
            {
                RoomSpec room = RoomSpec.ForShop(size);
                var placed = new List<PlacedFurniture>();
                foreach (PlacedFurniture p in FurnitureCatalog.DefaultShopFixtures(size))
                {
                    FurnitureDef def = FurnitureCatalog.Get(p.Id);
                    Assert.IsNull(RoomPlanner.Check(room, placed, def, p, -1, null), $"{size} {p.Id} ({p.X},{p.Z})");
                    placed.Add(p);
                }
                Assert.AreEqual(1, FurnitureCatalog.CountFixtures(placed, FixtureKind.Counter));
                Assert.AreEqual(6, FurnitureCatalog.CountFixtures(placed, FixtureKind.Shelf));
                Assert.AreEqual(1, FurnitureCatalog.CountFixtures(placed, FixtureKind.Bench));
                Assert.AreEqual(ShopSettings.DisplaySlots(size) / 2, FurnitureCatalog.CountFixtures(placed, FixtureKind.Display), size.ToString());
            }
        }

        [Test]
        public void NewlyRentedShop_IsBare_AndClosed()
        {
            GameSession s = Rented();
            Assert.AreEqual(0, NonBoxes(s.ShopLayout));
            Assert.AreEqual(GameSession.StarterShopBoxes, s.ShopBoxes, "租下送的纸箱已经摆好");
            Assert.IsFalse(s.ShopFitted);
            StringAssert.Contains("收银台", s.ShopFitGap());
            StringAssert.Contains("货架", s.ShopFitGap());
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.InPlayerShop);
            Assert.IsFalse(s.PlayerShop.IsOpen, "没摆设施开不了业");
            Assert.AreEqual(0, s.PlayerShop.DisplaySlots, "没有展示桌就没有机位");
        }

        [Test]
        public void RentQuestReward_GivesAShelfAndCounter_SoABrokePlayerCanStillOpen()
        {
            GameSession s = Rented();
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.Shelf));
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.ShopCounter));
            Assert.IsTrue(s.QuestCompleted("rent"));
            Assert.AreEqual(0, s.PlayerShop.Stock.CountAll(PartCatalog.Get("cpu0")), "配件是家具城任务的奖励，租铺不送");

            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.BeginRenovation());
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.ShopCounter, At(s, FurnitureCatalog.ShopCounter, 0.9, 3.3, 3)));
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.Shelf, At(s, FurnitureCatalog.Shelf, 3, 5.8, 0)));
            Assert.IsTrue(s.ShopFitted, "只靠送的两样就够开业");
        }

        [Test]
        public void MallQuest_AnyPurchase_RewardsCpuAndGpu()
        {
            GameSession s = Rented();
            Assert.AreEqual("mall", s.CurrentQuest().Id);
            GameSessionTests.Teleport(s, "mall");
            s.SendByCourier = true;
            Assert.IsTrue(s.BuyFurniture(FurnitureCatalog.Purchasable(true).First().Id));
            Assert.IsTrue(s.QuestCompleted("mall"));
            Assert.AreEqual(1, s.PlayerShop.Stock.CountAll(PartCatalog.Get("cpu0")));
            Assert.AreEqual(1, s.PlayerShop.Stock.CountAll(PartCatalog.Get("gpu0")));
            Assert.AreEqual("place", s.CurrentQuest().Id);
        }

        [Test]
        public void BuyAndPlaceFixtures_ShopOpensTheDayAfter()
        {
            GameSession s = Rented();
            BuyMinimum(s);
            GameSessionTests.Teleport(s, "pc");
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.ShopCounter));

            Assert.IsFalse(s.PlaceFromStock(true, FurnitureCatalog.ShopCounter, At(s, FurnitureCatalog.ShopCounter, 0.9, 3.3, 3)), "要先歇业装修");
            Assert.IsTrue(s.BeginRenovation());
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.ShopCounter, At(s, FurnitureCatalog.ShopCounter, 0.9, 3.3, 3)));
            Assert.IsFalse(s.ShopFitted, "还差货架");
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.Shelf, At(s, FurnitureCatalog.Shelf, 3, 5.8, 0)));
            Assert.IsTrue(s.ShopFitted);
            Assert.IsNull(s.ShopFitGap());
            Assert.IsFalse(s.PlayerShop.IsOpen, "摆完当天还在装修歇业");

            WaitDays(s, s.Clock.Today.TotalDays + 1);
            s.Leave();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.IsRenovating);
            Assert.IsTrue(s.PlayerShop.IsOpen, "第二天开业");
        }

        [Test]
        public void StoringTheCounter_ClosesTheShopAgain()
        {
            GameSession s = Rented();
            BuyMinimum(s);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.BeginRenovation());
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.ShopCounter, At(s, FurnitureCatalog.ShopCounter, 0.9, 3.3, 3)));
            Assert.IsTrue(s.PlaceFromStock(true, FurnitureCatalog.Shelf, At(s, FurnitureCatalog.Shelf, 3, 5.8, 0)));
            Assert.IsTrue(s.ShopFitted);
            Assert.IsTrue(s.StoreFurniture(true, s.ShopLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.ShopCounter)));
            Assert.IsFalse(s.ShopFitted);
            Assert.IsTrue(s.PlayerShop.NotFitted);
        }

        [Test]
        public void FixtureChecklist_CountsPlacedOwnedAndShipping()
        {
            GameSession s = Rented();
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, FurnitureCatalog.ShopBench));
            FixtureNeed bench = s.ShopFitList().Single(n => n.Kind == FixtureKind.Bench);
            Assert.AreEqual(0, bench.Placed);
            Assert.AreEqual(1, bench.Owned, "在路上的包裹也算买了");
            FixtureNeed counter = s.ShopFitList().Single(n => n.Kind == FixtureKind.Counter);
            Assert.IsTrue(counter.Required);
            Assert.AreEqual(1, counter.Owned, "房东送了一个收银台");
            Assert.AreEqual(1 + 1 + 1 + 1, s.ShopFitList().Count);
            FixtureNeed shelf = s.ShopFitList().Single(n => n.Kind == FixtureKind.Shelf);
            Assert.AreEqual(6, shelf.Want, "建议 6 个货架");
            Assert.IsTrue(shelf.Required);
        }

        [Test]
        public void DisplayDesks_DecideDisplaySlots()
        {
            GameSession s = Make(true);
            Assert.AreEqual(ShopSettings.DisplaySlots(ShopSize.Small), s.PlayerShop.DisplaySlots);
            GameSessionTests.Teleport(s, "pc");
            s.PlayerShop.RestoreDisplay(new[] { 0 });
            Assert.AreEqual(1, s.PlayerShop.DisplayPcs.Count);
            Assert.IsTrue(s.BeginRenovation());
            int desk = s.ShopLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.ShopDisplay);
            Assert.GreaterOrEqual(desk, 0);
            Assert.IsTrue(s.StoreFurniture(true, desk));
            Assert.AreEqual(0, s.PlayerShop.DisplaySlots, "桌子收走了就没机位");
            Assert.AreEqual(0, s.PlayerShop.DisplayPcs.Count);
        }

        private static GameSession Make(bool startWithShop)
        {
            CityLayoutData data = GameSessionTests.Layout();
            return new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, startWithShop);
        }

        [Test]
        public void StarterShop_AndCheat_AreFullyFitted()
        {
            GameSession s = Make(true);
            Assert.IsTrue(s.ShopFitted);
            Assert.AreEqual(6, s.PlacedFixtures(FixtureKind.Shelf));
            GameSession c = NoShop();
            Assert.IsTrue(c.DebugRentStarterShop());
            Assert.IsTrue(c.ShopFitted, "作弊开店直接摆好整套设施");
            Assert.AreEqual(1, c.PlacedFixtures(FixtureKind.Counter));
        }

        [Test]
        public void MovingShop_ReturnsFixturesToStock()
        {
            GameSession s = Make(true);
            s.PlayerShop.Reputation = 90;
            s.DebugAddListing("tea", 0, 20, Money.FromYuan(5000));
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea"), "搬店");
            Assert.AreEqual(0, NonBoxes(s.ShopLayout));
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.ShopCounter));
            Assert.AreEqual(6, s.FurnitureInStock(FurnitureCatalog.Shelf));
            Assert.IsFalse(s.ShopFitted, "新店要重新摆");
        }

        [Test]
        public void SaveLoad_KeepsFixtures()
        {
            GameSession s = Make(true);
            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.IsTrue(loaded.ShopFitted);
            Assert.AreEqual(s.ShopLayout.Count, loaded.ShopLayout.Count);

            GameSession bare = Rented();
            GameSession loadedBare = GameSession.Load(bare.City, bare.CreateSave());
            Assert.AreEqual(0, NonBoxes(loadedBare.ShopLayout));
            Assert.IsFalse(loadedBare.ShopFitted, "毛坯店读档还是毛坯");
            Assert.IsTrue(loadedBare.PlayerShop.NotFitted);
        }

        [Test]
        public void OldSave_WithShop_GetsStarterFixtures()
        {
            GameSession s = Make(true);
            SaveData d = s.CreateSave();
            d.home.fixturesV = 0;
            d.home.shopLayout = new SavePlaced[0];
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.IsTrue(loaded.ShopFitted, "旧存档的店补上整套设施，照常营业");
            Assert.AreEqual(6, loaded.PlacedFixtures(FixtureKind.Shelf));
        }

        // ---------- 中介费打折 ----------

        [Test]
        public void AgentDiscount_FollowsRapport_AndIsCapped()
        {
            Assert.AreEqual(0, RealEstate.AgentDiscount(0), 1e-9);
            Assert.AreEqual(0.2, RealEstate.AgentDiscount(40), 1e-9);
            Assert.AreEqual(0.5, RealEstate.AgentDiscount(100), 1e-9);
            Assert.AreEqual(0.5, RealEstate.AgentDiscount(300), 1e-9);
            Assert.AreEqual(2000, RealEstate.AgentFeeAfter(Money.FromYuan(2000), 0).Yuan);
            Assert.AreEqual(1600, RealEstate.AgentFeeAfter(Money.FromYuan(2000), 40).Yuan);
            Assert.AreEqual(1000, RealEstate.AgentFeeAfter(Money.FromYuan(2000), 100).Yuan);
        }

        [Test]
        public void ChattingWithTheAgent_LowersTheSigningCost()
        {
            GameSession s = NoShop();
            ShopListing l = s.DebugAddListing("pc", 0, 999, Money.FromYuan(1500));
            Money full = s.LeaseCost(l);
            Assert.AreEqual(RealEstate.SignCost(l.Rent).Yuan, full.Yuan, "没熟之前按标价");
            for (int i = 0; i < 12; i++)
            {
                Assert.IsNotNull(s.StaffChat(Rosters.AgentChen));
                s.AdvanceClock(24 * 60);
            }
            l = s.DebugAddListing("pc", 0, 999, Money.FromYuan(1500));
            Assert.Greater(s.AgentRapport, 10);
            Assert.Greater(s.AgentDiscount, 0.05);
            Money cheaper = s.LeaseCost(l);
            Assert.Less(cheaper.Cents, full.Cents);
            Assert.AreEqual(full.Yuan - (RealEstate.AgentFee(l.Rent).Yuan - s.AgentFeeFor(l.Rent).Yuan), cheaper.Yuan);

            long before = s.Ledger.Balance.Yuan;
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("pc"));
            Assert.AreEqual(cheaper.Yuan, before - s.Ledger.Balance.Yuan, "实际付的就是打折后的");
        }

        [Test]
        public void AgentChen_IsTheGreeterAtTheAgency()
        {
            GameSession s = NoShop();
            PlaceRoster r = Rosters.For(s.City.GetPlace("agency"));
            Assert.IsNotNull(r);
            Assert.AreEqual(Rosters.AgentChen, r.Greeter);
            Assert.IsTrue(r.Greeter.IsBoss);
            Assert.AreSame(Rosters.AgentChen, Rosters.Get(Rosters.AgentChen.Id));
            Assert.AreEqual(3, Rosters.StoriesOf(Rosters.AgentChen.Id).Length);
        }
    }
}
