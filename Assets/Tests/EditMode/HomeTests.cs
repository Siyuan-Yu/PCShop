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
        public void GrantOutfit_ThenEquip_Works()
        {
            GameSession s = Make();
            Assert.IsFalse(s.Equip("suit"));
            s.GrantOutfit("suit");
            Assert.IsTrue(s.Equip("suit"));
            Assert.AreEqual("suit", s.EquippedOutfit);
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
            Assert.IsTrue(s.BuyFurnitureAtMall("plant"));
            Assert.AreEqual(Money.FromYuan(60), before - s.Ledger.Balance);
            Assert.AreEqual(0, s.FurnitureInStock("plant"), "还背在身上，没到家");
            Assert.AreEqual(1, s.Backpack.FurnitureCount("plant"));
            GameSessionTests.Teleport(s, "flat");
            Assert.AreEqual(1, s.FurnitureInStock("plant"));
            Assert.AreEqual(0, s.Backpack.FurnitureCount("plant"));
            Assert.IsFalse(s.BuyFurnitureAtMall("bed_board"), "起始家具不卖");
        }

        private static long At(GameSession s, int dayOffset, int hour) => (long)(s.Clock.Today.TotalDays + dayOffset) * 1440 + hour * 60;

        [Test]
        public void Repair_MustBeBooked_WithLeadTime_AndNightCostsHalfMore()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.AreEqual("fan_broken", s.BrokenHomeItems().Single());
            FurnitureDef broken = FurnitureCatalog.Get("fan_broken");
            Assert.AreEqual(Money.FromYuan(60), s.RepairPrice(broken, At(s, 0, 13)), "白天平价");
            Assert.AreEqual(Money.FromYuan(90), s.RepairPrice(broken, At(s, 0, 19)), "晚上加收 50%");
            Assert.AreEqual(Money.FromYuan(90), s.RepairPrice(broken, At(s, 1, 5) ), "凌晨也算晚上");

            Assert.IsFalse(s.BookRepair("fan_broken", At(s, 0, 10) + 30), "至少提前 1 小时");
            Assert.IsFalse(s.BookRepair("fan_broken", At(s, 1, 3)), "半夜师傅不上门");
            Assert.IsFalse(s.BookRepair("fan_broken", At(s, 5, 10)), "只能约 3 天内");
            Assert.IsFalse(s.BookRepair("stool", At(s, 0, 14)), "没坏的不能约");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 0, 19)));
            Assert.AreEqual((before - Money.FromYuan(90)).Cents, s.Ledger.Balance.Cents, "下单就付上门费");
            Assert.IsFalse(s.BookRepair("fan_broken", At(s, 0, 20)), "已经约了");
            Assert.IsTrue(s.CancelRepair("fan_broken"));
            Assert.AreEqual(0, s.HomeVisits.Count);
            Assert.AreEqual((before - Money.FromYuan(90)).Cents, s.Ledger.Balance.Cents, "取消不退款（黑心）");
        }

        [Test]
        public void Repair_CannotBookWithoutEnoughMoney()
        {
            GameSession s = Make(40, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsFalse(s.BookRepair("fan_broken", At(s, 0, 13)), "付不起上门费");
            Assert.AreEqual(0, s.HomeVisits.Count);
        }

        [Test]
        public void Repair_Visit_FixesWhenSomeoneIsHome()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 0, 13)));
            Money before = s.Ledger.Balance;
            WaitMinutes(s, 2 * 60 + 50);
            Assert.AreEqual("fan_broken", s.HomeLayout.Select(p => p.Id).First(id => id.StartsWith("fan_")), "还没到点");
            Assert.IsNull(s.RepairmanAtWork);
            WaitMinutes(s, 15);
            Assert.IsNotNull(s.RepairmanAtWork, "师傅进门了");
            Assert.AreEqual(1, s.BrokenHomeItems().Count(), "还在修，没好");
            Assert.IsFalse(s.CancelRepair("fan_broken"), "师傅在干活，取消不了");
            Assert.IsTrue(s.Schedule().Any(e => e.Kind == "repairing"));
            Assert.IsNotNull(s.LeaveBlocker, "师傅在屋里干活，不能出门");
            s.Leave();
            Assert.AreEqual(s.Home, s.CurrentPlace, "Leave 被拦住");
            Assert.IsFalse(s.GoTo("pc"), "出发也被拦住");
            Assert.AreEqual(s.Home, s.CurrentPlace);
            WaitMinutes(s, GameSession.RepairWorkMinutes);
            Assert.IsNull(s.LeaveBlocker, "修好了可以出门");
            s.Leave();
            Assert.IsNull(s.CurrentPlace);
            GameSessionTests.Teleport(s, "flat");
            Assert.AreEqual(0, s.BrokenHomeItems().Count());
            Assert.AreEqual(0, s.HomeVisits.Count, "修好走人");
            Assert.AreEqual(1, s.Ledger.Entries.Count(e => e.Category == "维修"), "只有下单时扣的那一笔，修好不再收");
            Assert.AreEqual(-Money.FromYuan(60).Cents, s.Ledger.Entries.Single(e => e.Category == "维修").Amount.Cents);
        }

        [Test]
        public void Repair_Visit_NobodyHome_NoShowFee_AndStillBroken()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 0, 13)));
            GameSessionTests.Teleport(s, "pc");
            Money before = s.Ledger.Balance;
            WaitMinutes(s, 3 * 60 + 20);
            Assert.AreEqual(1, s.BrokenHomeItems().Count(), "没人在家，没修");
            Assert.AreEqual(-Money.FromYuan(60).Cents, s.Ledger.Entries.Where(e => e.Category == "维修").Sum(e => e.Amount.Cents), "白跑一趟，上门费不退");
            Assert.AreEqual(0, s.HomeVisits.Count);
        }

        [Test]
        public void Repair_Visit_GetHomeWithinGrace_StillFixed()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 0, 13)));
            GameSessionTests.Teleport(s, "pc");
            WaitMinutes(s, 3 * 60 + 8);
            Assert.AreEqual(1, s.HomeVisits.Count, "师傅在门口等");
            GameSessionTests.Teleport(s, "flat");
            WaitMinutes(s, 2);
            Assert.IsNotNull(s.RepairmanAtWork, "宽限期内回到家，师傅才进门");
            WaitMinutes(s, GameSession.RepairWorkMinutes);
            Assert.AreEqual(0, s.BrokenHomeItems().Count());
        }

        [Test]
        public void Repair_Visit_BrokeCannotPay_AndSurvivesSave_AndShowsInSchedule()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "sofa"));
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 1, 9)));
            GameSession l = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(1, l.HomeVisits.Count);
            Assert.AreEqual(s.HomeVisits[0].StartMinute, l.HomeVisits[0].StartMinute);
            Assert.AreEqual(Money.FromYuan(60), l.HomeVisits[0].Price);
            var entries = l.Schedule();
            Assert.IsTrue(entries.Any(e => e.Kind == "repair"));
            Assert.AreEqual(-1, l.HomeVisits[0].WorkStart);
            Assert.IsTrue(entries.Any(e => e.Kind == "parcel"));
            Assert.IsTrue(entries.SequenceEqual(entries.OrderBy(e => e.Minute).ThenBy(e => e.Kind, System.StringComparer.Ordinal)), "按时间排序");
        }

        [Test]
        public void Repair_WorkingVisit_SurvivesSave()
        {
            GameSession s = Make(20000, 10 * 60);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.BookRepair("fan_broken", At(s, 0, 13)));
            WaitMinutes(s, 3 * 60 + 10);
            Assert.IsNotNull(s.RepairmanAtWork);
            GameSession l = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));
            Assert.IsNotNull(l.RepairmanAtWork);
            Assert.AreEqual(s.RepairmanAtWork.WorkStart, l.RepairmanAtWork.WorkStart);
        }

        [Test]
        public void HeavyFurniture_WalkingCannotCarry_NeedsTruck_AndArrivesNextDay()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "mall");
            Assert.Greater(FurnitureCatalog.Get("sofa").WeightKg, s.FreeKg, "走路背不动沙发");
            Assert.IsFalse(s.BuyFurnitureAtMall("sofa"), "只有一个人又没载具，带不走");
            Assert.IsTrue(s.MallCartAdd("sofa"));
            Assert.IsNotNull(s.PlanMallCheckout(MallCarry.Self).Blocker);
            MallPlan truck = s.PlanMallCheckout(MallCarry.Truck);
            Assert.IsNull(truck.Blocker);
            Assert.AreEqual(1, truck.Trucks);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.CheckoutMall(MallCarry.Truck));
            Assert.AreEqual(Money.FromYuan(800) + FurnitureCatalog.TruckFee, before - s.Ledger.Balance);
            Assert.AreEqual(0, s.Backpack.WeightKg, 1e-9, "货车拉的，不占背包");
            Assert.AreEqual(0, s.MallCartCount, "结算后购物车清空");
            Assert.AreEqual(0, s.FurnitureInStock("sofa"));
            GameSessionTests.Teleport(s, "flat");
            int day = s.Clock.Today.TotalDays + 2;
            while (s.Clock.Today.TotalDays < day) s.AdvanceClock(60);
            Assert.AreEqual(1, s.FurnitureInStock("sofa"), "次日送到");
        }

        [Test]
        public void Mall_BuyingTooMuch_NeedsSeveralTrucks()
        {
            GameSession s = Make(200000);
            GameSessionTests.Teleport(s, "mall");
            double bed = FurnitureCatalog.Get("bed_good").WeightKg;
            Assert.LessOrEqual(bed, GameSession.TruckCapacityKg);
            s.MallCartAdd("bed_good", 5);
            MallPlan plan = s.PlanMallCheckout(MallCarry.Truck);
            Assert.AreEqual(GameSession.TrucksNeeded(s.MallCart), plan.Trucks);
            Assert.GreaterOrEqual(plan.Trucks, (int)System.Math.Ceiling(bed * 5 / GameSession.TruckCapacityKg));
            Assert.Greater(plan.Trucks, 1, "一辆装不下");
            Assert.AreEqual(FurnitureCatalog.TruckFee * plan.Trucks, plan.TruckFees);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.CheckoutMall(MallCarry.Truck));
            Assert.AreEqual(plan.Total, before - s.Ledger.Balance);
        }

        [Test]
        public void Mall_SplitCheckout_CarriesWhatFits_TrucksTheRest()
        {
            GameSession s = Make(50000);
            GameSessionTests.Teleport(s, "mall");
            s.MallCartAdd("poster", 2);
            s.MallCartAdd("sofa");
            MallPlan split = s.PlanMallCheckout(MallCarry.Split);
            Assert.IsNull(split.Blocker);
            Assert.AreEqual(2, split.SelfCount, "轻的海报自己带");
            Assert.AreEqual(1, split.TruckCount, "沙发叫货车");
            Assert.AreEqual(1, split.Trucks);
            Assert.IsTrue(s.CheckoutMall(MallCarry.Split));
            Assert.AreEqual(2, s.Backpack.FurnitureCount("poster"));
            Assert.AreEqual(1, s.Parcels.Count);
        }

        [Test]
        public void Mall_SplitStay_LoadsWhatFits_LeavesRestInStore_NoTruckFee_AndSurvivesSave()
        {
            GameSession s = Make(50000);
            GameSessionTests.Teleport(s, "mall");
            s.MallCartAdd("poster", 2);
            s.MallCartAdd("sofa");
            MallPlan plan = s.PlanMallCheckout(MallCarry.SplitStay);
            Assert.IsNull(plan.Blocker);
            Assert.AreEqual(2, plan.SelfCount);
            Assert.AreEqual(1, plan.StashCount);
            Assert.AreEqual(Money.Zero, plan.TruckFees, "寄存不收运费");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.CheckoutMall(MallCarry.SplitStay));
            Assert.AreEqual(FurnitureCatalog.Get("poster").Price * 2 + FurnitureCatalog.Get("sofa").Price, before - s.Ledger.Balance);
            Assert.AreEqual(2, s.Backpack.FurnitureCount("poster"));
            Assert.AreEqual(1, s.MallStashCount);
            Assert.AreEqual(0, s.Parcels.Count);

            GameSession l = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(1, l.MallStashCount, "寄存的货存档后还在");
        }

        [Test]
        public void Mall_Stash_TakeInSeveralTrips_OrCallTruck_AndOnlyAtTheMall()
        {
            GameSession s = Make(50000);
            GameSessionTests.Teleport(s, "mall");
            s.MallCartAdd("poster", 2);
            s.MallCartAdd("sofa");
            Assert.IsTrue(s.CheckoutMall(MallCarry.Stay), "什么都不带，全寄存");
            Assert.AreEqual(3, s.MallStashCount);
            Assert.AreEqual(0, s.Backpack.WeightKg, 1e-9);

            Assert.IsFalse(s.TakeFromStash("sofa"), "走路背不动沙发");
            Assert.AreEqual(2, s.TakeStashWhatFits(), "轻的海报搬得动");
            Assert.AreEqual(1, s.MallStashCount);
            Assert.AreEqual(2, s.Backpack.FurnitureCount("poster"));
            Assert.AreEqual(0, s.TakeStashWhatFits(), "沙发还是搬不动");

            GameSessionTests.Teleport(s, "flat");
            Assert.IsFalse(s.CallTruckForStash(), "不在家具城不能叫车拉寄存的货");
            Assert.IsFalse(s.TakeFromStash("sofa"));
            GameSessionTests.Teleport(s, "mall");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.CallTruckForStash());
            Assert.AreEqual(FurnitureCatalog.TruckFee, before - s.Ledger.Balance);
            Assert.AreEqual(0, s.MallStashCount);
            Assert.AreEqual(1, s.Parcels.Count);
        }

        [Test]
        public void Mall_Cart_OnlyAtTheMall_AndClearedOnLeaving()
        {
            GameSession s = Make();
            Assert.IsFalse(s.MallCartAdd("plant"), "不在家具城");
            GameSessionTests.Teleport(s, "mall");
            Assert.IsFalse(s.MallCartAdd("bed_board"), "起始家具不卖");
            Assert.IsTrue(s.MallCartAdd("plant", 2));
            Assert.AreEqual(2, s.MallCartOf("plant"));
            Assert.IsTrue(s.MallCartRemove("plant"));
            Assert.AreEqual(1, s.MallCartOf("plant"));
            Assert.AreEqual("购物车是空的", new GameSession(s.City, 1, Money.FromYuan(10), s.Clock.Now).PlanMallCheckout(MallCarry.Self).Blocker);
            s.Leave();
            Assert.AreEqual(0, s.MallCartCount);
        }

        [Test]
        public void Mall_LightFurniture_CountsTowardBackpackWeight_AndSurvivesSave()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "mall");
            Assert.IsTrue(s.BuyFurnitureAtMall("fan_new"));
            Assert.AreEqual(FurnitureCatalog.Get("fan_new").WeightKg, s.Backpack.WeightKg, 1e-9);
            GameSession l = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(1, l.Backpack.FurnitureCount("fan_new"));
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
            Assert.AreEqual(4.0, s.Pantry.RiceJin, 1e-9, "米马上用掉");
            Assert.AreEqual(t0, s.Clock.Now.TotalMinutes, "点了就开始焖，时间不会卡住");
            Assert.AreEqual(0, s.Pantry.CookedBowls, "还没焖好");
            Assert.AreEqual(Pantry.CookMinutes, s.Pantry.CookMinutesLeft(t0));
            Assert.IsFalse(s.EatBowlAtHome(), "焖饭期间锅里没饭可取");
            Assert.IsFalse(s.PackLunch(1));
            Assert.IsFalse(s.CookRice(1, null), "焖饭期间不能再放米");
            WaitMinutes(s, 15);
            Assert.AreEqual(Pantry.CookMinutes - 15, s.Pantry.CookMinutesLeft(s.Clock.Now.TotalMinutes));
            Assert.AreEqual(0, s.Pantry.CookedBowls);
            WaitMinutes(s, Pantry.CookMinutes - 15);
            Assert.IsNull(s.Pantry.Cooking);
            Assert.AreEqual(4, s.Pantry.CookedBowls, "40 分钟后饭好了");
            Assert.IsFalse(s.CookRice(1, null), "锅里的饭没取完不能再焖");
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
            WaitMinutes(s, Pantry.CookMinutes);
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
            WaitMinutes(s, Pantry.CookMinutes);
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
            WaitMinutes(s, 10);
            Assert.IsTrue(s.StoreFurniture(false, 3)); // 塑料凳进背包

            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(s.HomeLayout.Count, loaded.HomeLayout.Count);
            Assert.AreEqual(1, loaded.FurnitureInStock("stool"));
            Assert.AreEqual(2, loaded.Parcels.Count);
            Assert.IsNotNull(loaded.Pantry.Cooking, "存档时还在焖");
            Assert.AreEqual(Pantry.CookMinutes - 10, loaded.Pantry.CookMinutesLeft(loaded.Clock.Now.TotalMinutes));
            WaitMinutes(loaded, Pantry.CookMinutes - 10);
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
