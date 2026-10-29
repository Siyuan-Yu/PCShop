using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>出租屋的家具、网购、家具城、做饭吃饭、店里装修。</summary>
    public class HomeTests
    {
        internal static GameSession Make(long cash = 20000, int minute = 8 * 60)
        {
            CityLayoutData data = GameSessionTests.Layout();
            var places = data.places.ToList();
            places.Add(new LayoutPlaceData { id = "mall", name = "家具城", kind = "shop", node = "dl", size = "large", business = MallBiz });
            places.Add(new LayoutPlaceData { id = "farm", name = "农贸市场", kind = "shop", node = "st2", size = "small", business = FarmBiz });
            data.places = places.ToArray();
            return new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), minute));
        }

        private const string MallBiz = GameSession.MallBusiness;
        private const string FarmBiz = GameSession.FarmBusiness;

        private static void WaitDays(GameSession s, int day)
        {
            while (s.Clock.Today.TotalDays < day) s.AdvanceClock(60);
        }

        /// <summary>网购并摆出：弹簧床换掉硬板床，再挂空调、窗帘。</summary>
        internal static void FurnishBetter(GameSession s)
        {
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "bed_spring"));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "aircon"));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, FurnitureCatalog.Curtain));
            int minute = s.Clock.MinuteOfDay;
            WaitDays(s, 2);
            // 回到当天原来的钟点（睡眠测试要在晚上）
            s.Clock.Restore(s.Clock.Today, minute);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.StoreFurniture(false, 0), "先把硬板床收起来");
            Assert.IsTrue(s.PlaceFromStock(false, "bed_spring", RoomPlanner.Snap(RoomSpec.Home, FurnitureCatalog.Get("bed_spring"), 1.05, 4.0, 0)));
            Assert.IsTrue(s.PlaceFromStock(false, "aircon", RoomPlanner.Snap(RoomSpec.Home, FurnitureCatalog.Get("aircon"), 1.2, 0, 0)));
            Assert.IsTrue(s.PlaceFromStock(false, FurnitureCatalog.Curtain, RoomPlanner.Snap(RoomSpec.Home, FurnitureCatalog.Get(FurnitureCatalog.Curtain), 3.15, 0, 0)));
        }

        [Test]
        public void StartLayout_IsValid_AndHasBedWardrobeCooker()
        {
            GameSession s = Make();
            var defs = s.HomeLayout.Select(p => FurnitureCatalog.Get(p.Id)).ToList();
            Assert.IsTrue(defs.All(d => d != null));
            Assert.IsTrue(s.HomeHas(FurnitureRole.Bed));
            Assert.IsTrue(s.HomeHas(FurnitureRole.Wardrobe));
            Assert.IsTrue(s.HomeHas(FurnitureRole.Cooker));
            for (int i = 0; i < s.HomeLayout.Count; i++)
                Assert.IsNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, defs[i], s.HomeLayout[i], i, null), s.HomeLayout[i].Id);
            Assert.AreEqual(1.0, s.HomeSleepQuality, 1e-9);
            Assert.AreEqual(Pantry.StartRiceJin, s.Pantry.RiceJin, 1e-9, "开局有 5 斤米");
        }

        [Test]
        public void Planner_RejectsOverlap_DoorAndWindow()
        {
            GameSession s = Make();
            FurnitureDef sofa = FurnitureCatalog.Get("sofa");
            Assert.IsNotNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, sofa, new PlacedFurniture { Id = "sofa", X = 1.05, Z = 4.05 }, -1, null), "压在床上");
            Assert.IsNotNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, sofa, new PlacedFurniture { Id = "sofa", X = 5.2, Z = 1.0 }, -1, null), "挡门口");
            Assert.IsNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, sofa, new PlacedFurniture { Id = "sofa", X = 3.0, Z = 1.0 }, -1, null));
            FurnitureDef poster = FurnitureCatalog.Get("poster");
            Assert.IsNotNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, poster, new PlacedFurniture { Id = "poster", X = 3.15, Z = 0, Rot = 0 }, -1, null), "挡窗户");
            Assert.IsNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, poster, new PlacedFurniture { Id = "poster", X = 1.2, Z = 0, Rot = 0 }, -1, null));
            Assert.IsNull(RoomPlanner.Check(RoomSpec.Home, s.HomeLayout, FurnitureCatalog.Get(FurnitureCatalog.Curtain), new PlacedFurniture { Id = FurnitureCatalog.Curtain, X = 3.15, Rot = 0 }, -1, null), "窗帘可以盖窗户");
        }

        [Test]
        public void Snap_PushesFloorItemsBackIntoRoom()
        {
            PlacedFurniture p = RoomPlanner.Snap(RoomSpec.Home, FurnitureCatalog.Get("sofa"), 100, -100, 0);
            Assert.LessOrEqual(p.X + 0.8, 5.95 + 1e-9);
            Assert.GreaterOrEqual(p.Z - 0.4, 0.05 - 1e-9);
        }

        [Test]
        public void OnlineOrder_Arrives_InTwoDays_AtDiscount()
        {
            GameSession s = Make();
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "aircon"));
            Assert.AreEqual(Money.FromYuan(1200).Scale(0.85), before - s.Ledger.Balance);
            Assert.AreEqual(0, s.FurnitureInStock("aircon"));
            Assert.AreEqual(1, s.Parcels.Count);
            WaitDays(s, 1);
            Assert.AreEqual(0, s.FurnitureInStock("aircon"), "第 1 天还没到");
            WaitDays(s, 2);
            Assert.AreEqual(1, s.FurnitureInStock("aircon"));
            Assert.AreEqual(0, s.Parcels.Count);
        }

        [Test]
        public void Mall_SellsAtFullPrice_OnlyAtTheMall()
        {
            GameSession s = Make();
            Assert.IsFalse(s.BuyFurnitureAtMall("sofa"), "不在家具城");
            GameSessionTests.Teleport(s, "mall");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.BuyFurnitureAtMall("sofa"));
            Assert.AreEqual(Money.FromYuan(800), before - s.Ledger.Balance);
            Assert.AreEqual(1, s.FurnitureInStock("sofa"));
            Assert.IsFalse(s.BuyFurnitureAtMall("bed_board"), "起始家具不卖");
        }

        [Test]
        public void Placing_NeedsStock_AndBeingHome()
        {
            GameSession s = Make();
            PlacedFurniture at = new PlacedFurniture { Id = "sofa", X = 3.0, Z = 1.0 };
            Assert.IsFalse(s.PlaceFromStock(false, "sofa", at), "背包里没有");
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "sofa"));
            WaitDays(s, 2);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.PlaceFromStock(false, "sofa", at), "不在家");
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.PlaceFromStock(false, "sofa", at));
            Assert.AreEqual(0, s.FurnitureInStock("sofa"));
            int index = s.HomeLayout.Count - 1;
            Assert.IsTrue(s.MoveFurniture(false, index, new PlacedFurniture { Id = "sofa", X = 2.5, Z = 1.0 }));
            Assert.IsTrue(s.StoreFurniture(false, index));
            Assert.AreEqual(1, s.FurnitureInStock("sofa"));
        }

        [Test]
        public void BetterFurniture_ImprovesSleep_AndCostsUtilities()
        {
            GameSession s = Make(30000, 22 * 60);
            Assert.AreEqual(Money.Zero, s.HomeUtilities);
            FurnishBetter(s);
            Assert.AreEqual(1.12 + 0.12 + 0.04, s.HomeSleepQuality, 1e-9);
            Assert.AreEqual(Money.FromYuan(40), s.HomeUtilities);
        }

        [Test]
        public void NoBed_SleepsOnTheFloor()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.StoreFurniture(false, 0));
            Assert.IsFalse(s.HomeHas(FurnitureRole.Bed));
            Assert.AreEqual(GameSession.NoBedSleepQuality, s.HomeSleepQuality, 1e-9);
        }

        [Test]
        public void Rice_Cook_Eat_AndSpoil()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            s.Player.Hunger = 30;
            long t0 = s.Clock.Now.TotalMinutes;
            Assert.IsTrue(s.CookRice(1, null));
            Assert.AreEqual(4.0, s.Pantry.RiceJin, 1e-9);
            Assert.AreEqual(4, s.Pantry.CookedBowls);
            Assert.GreaterOrEqual(s.Clock.Now.TotalMinutes - t0, Pantry.CookMinutes);
            Assert.IsTrue(s.EatBowlAtHome());
            Assert.AreEqual(3, s.Pantry.CookedBowls);
            Assert.Greater(s.Player.Hunger, 30);

            Assert.IsTrue(s.PackLunch(2));
            Assert.AreEqual(2, s.Pantry.PackedBowls);
            Assert.AreEqual(1, s.Pantry.CookedBowls);

            WaitDays(s, s.Clock.Today.TotalDays + 2);
            Assert.AreEqual(0, s.Pantry.CookedBowls, "没冰箱，隔夜的饭坏了");
            Assert.AreEqual(0, s.Pantry.PackedBowls);
        }

        [Test]
        public void Cook_NeedsRiceCookerAndEnoughRice()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.CookRice(1, null), "不在家");
            GameSessionTests.Teleport(s, "flat");
            Assert.IsFalse(s.CookRice(6, null), "只有 5 斤米");
            Assert.IsFalse(s.CookRice(1, "egg"), "没有鸡蛋");
            int cooker = s.HomeLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.TableCooker);
            Assert.IsTrue(s.StoreFurniture(false, cooker));
            Assert.IsFalse(s.CookRice(1, null), "没摆电饭锅");
        }

        [Test]
        public void FarmMarket_SellsRiceAndIngredients_AndDishIsBetter()
        {
            GameSession s = Make(2000);
            Assert.IsFalse(s.BuyIngredient("egg", 2));
            GameSessionTests.Teleport(s, "farm");
            Assert.IsTrue(s.BuyIngredient("egg", 2));
            Assert.IsTrue(s.BuyRice(2));
            Assert.AreEqual(Pantry.StartRiceJin, s.Pantry.RiceJin, 1e-9, "还在背包里，没到家");
            Assert.AreEqual(2.0, s.Backpack.Food.RiceJin, 1e-9);
            GameSessionTests.Teleport(s, "flat");
            Assert.AreEqual(Pantry.StartRiceJin + 2, s.Pantry.RiceJin, 1e-9, "回家放进厨房");
            Assert.AreEqual(0.0, s.Backpack.Food.RiceJin, 1e-9);
            Assert.IsTrue(s.CookRice(1, "egg"));
            Assert.AreEqual(1, s.Pantry.IngredientCount("egg"));
            Assert.Greater(s.Pantry.Cooked.Last().Hunger, Pantry.BaseBowlHunger);
            Assert.AreEqual("鸡蛋焖饭", s.Pantry.Cooked.Last().Name);
        }

        [Test]
        public void FarmMarket_ClosedAtNight()
        {
            GameSession s = Make(2000, 20 * 60);
            GameSessionTests.Teleport(s, "farm");
            Assert.IsFalse(s.BuyRice(1));
        }

        [Test]
        public void MealTime_UsesPackedLunch_BeforePayingOutside()
        {
            GameSession s = Make(2000, 7 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.CookRice(1, null));
            Assert.IsTrue(s.PackLunch(4));
            s.Leave();
            s.Player.Hunger = 20;
            Money before = s.Ledger.Balance;
            WaitMinutes(s, 12 * 60 - s.Clock.MinuteOfDay + 5);
            Assert.AreEqual(before, s.Ledger.Balance, "午饭吃的是饭盒，没花钱");
            Assert.Less(s.Pantry.PackedBowls, 4);
        }

        private static void WaitMinutes(GameSession s, int minutes)
        {
            for (int i = 0; i < minutes; i++) s.AdvanceClock(1);
        }

        [Test]
        public void OutsideMeal_StillWorks_WithoutFood()
        {
            GameSession s = Make(2000, 7 * 60);
            s.Player.Hunger = 20;
            Money before = s.Ledger.Balance;
            WaitMinutes(s, 40);
            Assert.AreEqual(GameSession.MealPrice, before - s.Ledger.Balance);
        }

        [Test]
        public void ShopDecor_NeedsRenovation_ClosesShopForTheDay_AndBoostsTraffic()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.PlayerShop.IsOpen);
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "shop_plant"));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "shop_led"));
            WaitDays(s, 2);
            GameSessionTests.Teleport(s, "pc");
            PlacedFurniture plant = RoomPlanner.Snap(s.ShopRoom, FurnitureCatalog.Get("shop_plant"), 3, 3, 0);
            Assert.IsFalse(s.PlaceFromStock(true, "shop_plant", plant), "没歇业装修不能摆");
            Assert.IsTrue(s.BeginRenovation());
            Assert.IsTrue(s.IsRenovating);
            Assert.IsFalse(s.PlayerShop.IsOpen, "装修当天歇业");
            double before = s.ShopDecorTrafficMultiplier;
            Assert.IsTrue(s.PlaceFromStock(true, "shop_plant", plant));
            Assert.IsTrue(s.PlaceFromStock(true, "shop_led", RoomPlanner.Snap(s.ShopRoom, FurnitureCatalog.Get("shop_led"), 3, 0, 0)));
            Assert.Greater(s.ShopDecorTrafficMultiplier, before);
            Assert.AreEqual(1.0 + 0.04, s.ShopDecorTrafficMultiplier, 1e-9);

            WaitDays(s, s.Clock.Today.TotalDays + 1);
            Assert.IsFalse(s.IsRenovating);
            s.Leave();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.PlayerShop.IsOpen, "第二天恢复营业");
        }

        [Test]
        public void SaveLoad_KeepsFurnitureParcelsAndPantry()
        {
            GameSession s = Make();
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "sofa"));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Rice, "rice"));
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.CookRice(1, null));
            Assert.IsTrue(s.StoreFurniture(false, 3)); // 塑料凳进背包

            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(s.HomeLayout.Count, loaded.HomeLayout.Count);
            Assert.AreEqual(1, loaded.FurnitureInStock("stool"));
            Assert.AreEqual(2, loaded.Parcels.Count);
            Assert.AreEqual(4, loaded.Pantry.CookedBowls);
            Assert.AreEqual(s.Pantry.RiceJin, loaded.Pantry.RiceJin, 1e-9);
        }

        [Test]
        public void OldSave_WithHomeComfort_GivesFurnitureInStock()
        {
            GameSession s = Make();
            SaveData d = s.CreateSave();
            d.home = null;
            d.homeComfort = 2;
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.AreEqual(1, loaded.FurnitureInStock("bed_spring"));
            Assert.AreEqual(1, loaded.FurnitureInStock("aircon"));
            Assert.AreEqual(0, loaded.FurnitureInStock(FurnitureCatalog.Curtain));
        }
    }
}
