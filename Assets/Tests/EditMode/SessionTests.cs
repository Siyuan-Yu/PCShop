using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class GameSessionTests
    {
        internal static CityLayoutData Layout() => new CityLayoutData
        {
            distanceScale = 1,
            nodes = new[]
            {
                new LayoutNodeData { id = "st", x = 0, y = 0 },
                new LayoutNodeData { id = "s1", x = 10, y = 0 },
                new LayoutNodeData { id = "pc", x = 20, y = 0 },
                new LayoutNodeData { id = "home", x = 20, y = -50 },
                new LayoutNodeData { id = "mk", x = -100, y = 0 },
                new LayoutNodeData { id = "dl", x = -100, y = 10 },
                new LayoutNodeData { id = "st2", x = -100, y = -10 },
            },
            edges = new[]
            {
                new LayoutEdgeData { a = "st", b = "s1" },
                new LayoutEdgeData { a = "s1", b = "pc" },
                new LayoutEdgeData { a = "pc", b = "home", length = 500 },
                new LayoutEdgeData { a = "st", b = "mk", length = 2000 },
                new LayoutEdgeData { a = "mk", b = "dl" },
                new LayoutEdgeData { a = "mk", b = "st2" },
            },
            places = new[]
            {
                new LayoutPlaceData { id = "metro_a", name = "商业街站", kind = "station", node = "st" },
                new LayoutPlaceData { id = "tea", name = "奶茶店", kind = "shop", node = "s1", size = "large" },
                new LayoutPlaceData { id = "pc", name = "电脑店", kind = "shop", node = "pc", size = "small", player = true },
                new LayoutPlaceData { id = "flat", name = "出租屋", kind = "home", node = "home" },
                new LayoutPlaceData { id = "stall", name = "CPU 档口", kind = "wholesaler", node = "mk", sells = "Cpu,Gpu" },
                new LayoutPlaceData { id = "dealer", name = "车行", kind = "dealer", node = "dl" },
                new LayoutPlaceData { id = "metro_b", name = "市场站", kind = "station", node = "st2" },
            },
            patrols = new[] { new LayoutPatrolData { id = "p1", nodes = new[] { "s1", "st" } } },
        };

        internal static GameSession Make(long cash = 5000, int minute = 8 * 60, bool emptyBag = true)
        {
            CityLayout city = CityLayout.FromData(Layout());
            var s = new GameSession(city, 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), minute));
            if (emptyBag) PackTests.EmptyBag(s);
            return s;
        }

        /// <summary>按 1 倍速一帧帧推进直到到达（或超时）。</summary>
        internal static void RunTrip(GameSession s, int maxFrames = 20000)
        {
            for (int i = 0; i < maxFrames && s.Trip != null; i++) s.Tick(0.05);
        }

        internal static void Teleport(GameSession s, string placeId)
        {
            Place p = s.City.GetPlace(placeId);
            s.Leave();
            s.ArriveAtNode(p.NodeId);
            s.Enter(p);
        }

        [Test]
        public void StartsAtHome_ShopClosed_WithStarterStock()
        {
            GameSession s = Make();
            Assert.AreEqual(s.Home, s.CurrentPlace);
            Assert.IsFalse(s.PlayerShop.IsOpen);
            Assert.IsFalse(s.PlayerShop.Stock.IsEmpty);
        }

        [Test]
        public void FarthestShop_HasLowestLocationFactor()
        {
            Assert.AreEqual(0.4, Make().PlayerShop.LocationFactor, 1e-9);
        }

        [Test]
        public void EnterShop_OpensIt_LeaveClosesIt()
        {
            GameSession s = Make();
            Teleport(s, "pc");
            Assert.IsTrue(s.PlayerShop.IsOpen);
            s.Leave();
            Assert.IsFalse(s.PlayerShop.IsOpen);
            Assert.IsNull(s.CurrentPlace);
        }

        [Test]
        public void Enter_RequiresStandingAtEntrance()
        {
            GameSession s = Make();
            s.Leave();
            s.ArriveAtNode("s1");
            Assert.Throws<InvalidOperationException>(() => s.Enter(s.PlayerShopPlace));
        }

        [Test]
        public void Walking_AdvancesClockByGameDistance()
        {
            GameSession s = Make();
            Assert.IsTrue(s.GoTo("pc"));
            Assert.IsNull(s.CurrentPlace);
            RunTrip(s);
            Assert.AreEqual("pc", s.PlayerNodeId);
            // 500 米 ÷ 49 米/分钟（约 3 公里/小时）≈ 10.2 → 11 分钟
            Assert.AreEqual(8 * 60 + 11, s.Clock.MinuteOfDay);
        }

        [Test]
        public void CompressedConnector_TakesFullTime()
        {
            GameSession s = Make();
            s.SetVehicle(VehicleKind.Taxi);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.GoTo("mk"));
            RunTrip(s);
            Assert.AreEqual("mk", s.PlayerNodeId);
            // 500 + 20 + 2000 = 2520 米，打车 400 米/分钟 → 7 分钟；车费 ¥10 + ¥2
            Assert.AreEqual(8 * 60 + 7, s.Clock.MinuteOfDay);
            Assert.AreEqual(Money.FromYuan(12), before - s.Ledger.Balance);
        }

        [Test]
        public void Retarget_MidTrip_ContinuesFromNextNode()
        {
            GameSession s = Make();
            s.GoTo("st");
            for (int i = 0; i < 30; i++) s.Tick(0.05);
            Assert.IsNotNull(s.Trip);
            Assert.IsTrue(s.GoTo("home"));
            RunTrip(s);
            Assert.AreEqual("home", s.PlayerNodeId);
        }

        [Test]
        public void Sleep_OnlyAtHomeAtNight_WakesAtEight()
        {
            GameSession s = Make();
            Assert.IsFalse(s.Sleep());
            s.Clock.AdvanceTo(22 * 60);
            Assert.IsTrue(s.Sleep());
            Assert.AreEqual(8 * 60, s.Clock.MinuteOfDay);
            Assert.AreEqual(1, s.Clock.Today.TotalDays);
            s.Leave();
            s.Clock.AdvanceTo(22 * 60);
            Assert.IsFalse(s.Sleep());
        }

        [Test]
        public void StayingUpPast4_Faints()
        {
            GameSession s = Make();
            Teleport(s, "pc");
            string title = null;
            s.Popup += (t, _) => title = t;
            s.AdvanceClock(20 * 60 + 1);
            Assert.AreEqual("你晕倒了", title);
            Assert.AreEqual(s.Home, s.CurrentPlace);
            Assert.AreEqual(12 * 60, s.Clock.MinuteOfDay);
        }

        [Test]
        public void Meals_EatTheStartingLunchboxes_WithoutPaying()
        {
            GameSession s = Make(emptyBag: false);
            Assert.AreEqual(GameSession.StartLunchboxes, s.Backpack.Lunchboxes);
            s.Player.Hunger = 30;
            s.AdvanceClock(5 * 60);
            Assert.AreEqual(Money.FromYuan(5000), s.Ledger.Balance, "吃的是背包里的饭盒");
            Assert.Less(s.Backpack.Lunchboxes, GameSession.StartLunchboxes);
            Assert.Greater(s.Player.Hunger, 30);
        }

        [Test]
        public void BedtimeReminder_FiresWhenAwayFromHome()
        {
            GameSession s = Make();
            s.Leave();
            int notices = 0;
            s.Notice += m => { if (m.Contains("23:00")) notices++; };
            s.AdvanceClock(15 * 60 + 5);
            Assert.AreEqual(1, notices);
        }

        [Test]
        public void Wholesaler_ClosedOnWeekendAndAtNight()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Assert.IsTrue(s.IsWholesalerOpen);
            s.Clock.AdvanceTo(20 * 60);
            Assert.IsFalse(s.IsWholesalerOpen);
            s.Clock.Restore(new GameDate(5), 10 * 60);
            Assert.IsFalse(s.IsWholesalerOpen);
        }

        [Test]
        public void StallPurchase_GoesToCargo_UnloadsInShop()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "stall");
            Place stall = s.CurrentPlace;
            PartDef cpu = PartCatalog.Get("cpu1");
            Assert.IsTrue(s.StallPrice(stall, cpu) < s.Market.PriceOf(cpu));
            Assert.IsTrue(s.BuyAtStall(stall, cpu, 2));
            Assert.AreEqual(2, s.Cargo.Count(cpu, true));
            Assert.IsFalse(s.BuyAtStall(stall, PartCatalog.Get("ram0"), 1), "这家不卖内存");
            Assert.IsFalse(s.BuyAtStall(stall, PartCatalog.Get("gpu0"), 5), "走路带不了这么多显卡");

            Teleport(s, "pc");
            Assert.AreEqual(2, s.Cargo.Count(cpu, true), "进店不会自动卸货");
            Assert.IsTrue(s.UnloadBackpack());
            Assert.AreEqual(0, s.Cargo.Count(cpu, true));
            Assert.AreEqual(2, s.PlayerShop.Stock.Count(cpu, true));
        }

        [Test]
        public void StallPurchase_ByCourier_ArrivesAtShop()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "stall");
            Place stall = s.CurrentPlace;
            PartDef cpu = PartCatalog.Get("cpu1");
            s.SendByCourier = true;
            (Money fee, int minutes) = s.CourierQuote(true);
            Assert.IsTrue(fee.IsPositive);
            Assert.IsTrue(s.BuyAtStall(stall, cpu, 2));
            Assert.AreEqual(0, s.Cargo.Count(cpu, true), "叫车的不上身");
            int before = s.PlayerShop.Stock.Count(cpu, true);
            s.Leave();
            s.AdvanceClock(minutes + 1);
            Assert.AreEqual(before + 2, s.PlayerShop.Stock.Count(cpu, true));
        }

        [Test]
        public void Bargain_OncePerDay()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "stall");
            BargainResult r = s.Bargain(s.CurrentPlace);
            Assert.That(r, Is.EqualTo(BargainResult.Success).Or.EqualTo(BargainResult.Failed));
            Assert.AreEqual(BargainResult.AlreadyTried, s.Bargain(s.CurrentPlace));
        }

        [Test]
        public void Online_FromPhoneAnywhere_DeliveredNextMorning()
        {
            GameSession s = Make();
            PartDef ram = PartCatalog.Get("ram1");
            int before = s.PlayerShop.Stock.CountAll(ram);
            Assert.IsFalse(s.InPlayerShop, "不在店里也能用手机下单");
            Assert.IsTrue(s.PlaceOnlineOrder("s_star", new[] { (ram, 2) }));
            Assert.IsFalse(s.PlaceOnlineOrder("s_zhou", new[] { (ram, 1) }), "还没发现这家");
            s.Leave();
            s.AdvanceClock(Math.Max(0, s.Clock.MinutesUntil(23 * 60)));
            Assert.AreEqual(before, s.PlayerShop.Stock.CountAll(ram));
            Teleport(s, "flat");
            s.Sleep();
            s.AdvanceClock(2 * 60 + 1);
            Assert.AreEqual(2, s.Online.RecordOf("s_star").ItemsReceived);
            Assert.AreEqual(before + 2 - s.Online.RecordOf("s_star").Defects, s.PlayerShop.Stock.CountAll(ram));
        }

        [Test]
        public void Recommendation_RevealsHiddenStore()
        {
            GameSession s = Make();
            Assert.IsFalse(s.Online.IsDiscovered("s_zhou"));
            s.Clock.Restore(new GameDate(3), 23 * 60);
            s.AdvanceClock(60);
            Assert.IsTrue(s.Online.IsDiscovered("s_zhou"));
        }

        [Test]
        public void Tricycle_BuyAndCargoLimit()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "dealer");
            Assert.IsTrue(s.BuyVehicle(VehicleKind.Tricycle));
            Assert.IsTrue(s.SetVehicle(VehicleKind.Tricycle));
            Assert.AreEqual(40, s.Cargo.Capacity, 1e-9);
            s.Cargo.TryAdd(PartCatalog.Get("gpu0"), true, 10);
            Assert.IsFalse(s.SetVehicle(VehicleKind.Walk), "货太多不能改走路");
        }

        [Test]
        public void Police_CatchesTricycleInSight()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "dealer");
            s.BuyVehicle(VehicleKind.Tricycle);
            s.SetVehicle(VehicleKind.Tricycle);
            foreach (Officer o in s.Police.Officers) o.OnDutyToday = true;
            s.Leave();
            s.ArriveAtNode("st");
            string title = null;
            s.Popup += (t, _) => title = t;
            for (int i = 0; i < 400 && title == null; i++) s.Tick(0.05);
            Assert.AreEqual("被交警抓到了", title);
        }

        [Test]
        public void Police_GivesUpWhenPlayerHidesInPlace()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "dealer");
            s.BuyVehicle(VehicleKind.Tricycle);
            s.SetVehicle(VehicleKind.Tricycle);
            foreach (Officer o in s.Police.Officers) o.OnDutyToday = true;
            s.Leave();
            s.ArriveAtNode("st");
            for (int i = 0; i < 400 && !s.Police.AnyChasing; i++) s.Tick(0.05);
            Assert.IsTrue(s.Police.AnyChasing);
            s.Enter(s.City.PlaceAtNode("st"));
            s.Tick(0.05);
            Assert.IsFalse(s.Police.AnyChasing);
        }

        [Test]
        public void Police_IgnoresWalkers()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            foreach (Officer o in s.Police.Officers) o.OnDutyToday = true;
            s.Leave();
            s.ArriveAtNode("st");
            for (int i = 0; i < 400; i++) s.Tick(0.05);
            Assert.IsFalse(s.Police.AnyChasing);
        }

        [Test]
        public void Metro_MovesBetweenStations()
        {
            GameSession s = Make();
            Teleport(s, "metro_a");
            Assert.IsTrue(s.TakeMetro());
            Assert.AreEqual("metro_b", s.CurrentPlace.Id);
            Assert.AreEqual(8 * 60 + GameSession.MetroMinutes, s.Clock.MinuteOfDay);
        }

        [Test]
        public void Hiring_CandidatesNextMorning_EmployeeKeepsShopOpen()
        {
            GameSession s = Make();
            Teleport(s, "pc");
            Assert.IsTrue(s.PostJobAd());
            s.Leave();
            s.Clock.Restore(new GameDate(1), 8 * 60 + 50);
            s.AdvanceClock(20);
            Assert.AreEqual(2, s.Candidates.Count);
            Assert.IsTrue(s.Hire(0));
            Assert.IsTrue(s.PlayerShop.IsOpen, "员工在岗，玩家不在也营业");
            Money before = s.Ledger.Balance;
            s.Clock.Restore(new GameDate(1), 23 * 60);
            s.AdvanceClock(120);
            Assert.IsTrue(s.Ledger.Balance < before, "发了工资");
        }

        [Test]
        public void MonthStart_ChargesRent()
        {
            GameSession s = Make();
            s.Clock.Restore(new GameDate(29), 23 * 60);
            Money before = s.Ledger.Balance;
            s.AdvanceClock(61);
            Assert.AreEqual(GameSession.ShopRent + GameSession.HomeRent, before - s.Ledger.Balance);
            Assert.IsTrue(s.MonthGoalEvaluated);
        }

        [Test]
        public void ThreeNegativeDays_GameOver()
        {
            GameSession s = Make(cash: 0);
            s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(10000));
            Teleport(s, "flat");
            for (int i = 0; i < 4 && !s.IsGameOver; i++)
            {
                s.Clock.Restore(new GameDate(i), 22 * 60);
                s.Sleep();
            }
            Assert.IsTrue(s.IsGameOver);
        }

        [Test]
        public void SaveAndLoad_RoundTrips()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "stall");
            s.BuyAtStall(s.CurrentPlace, PartCatalog.Get("cpu0"), 2);
            Teleport(s, "pc");
            s.SetMarkup(1.3);
            s.PlaceOnlineOrder("s_mid", new[] { (PartCatalog.Get("ssd1"), 3) });

            SaveData data = s.CreateSave();
            GameSession l = GameSession.Load(s.City, data);

            Assert.AreEqual(s.Ledger.Balance, l.Ledger.Balance);
            Assert.AreEqual(s.Ledger.Entries.Count, l.Ledger.Entries.Count);
            Assert.AreEqual(s.Clock.Now, l.Clock.Now);
            Assert.AreEqual(s.PlayerShop.Stock.UsedSpace, l.PlayerShop.Stock.UsedSpace, 1e-9);
            Assert.AreEqual(1.3, l.PlayerShop.Markup, 1e-9);
            Assert.AreEqual(1, l.Online.Pending.Count);
            Assert.AreEqual("pc", l.CurrentPlace.Id);
            Assert.IsTrue(l.PlayerShop.IsOpen);
            Assert.AreEqual(s.Market.Index(PartCategory.Gpu), l.Market.Index(PartCategory.Gpu), 1e-12);
            Assert.AreEqual(s.Random.State, l.Random.State);
        }

        [Test]
        public void DisplayPc_AssembledInShop_SurvivesSave()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(10 * 60);
            Teleport(s, "pc");
            foreach (PartDef p in PartCatalog.BuildList(0)) s.PlayerShop.Stock.ForceAdd(p, true, 1);
            var t0 = s.Clock.Now;
            Assert.IsTrue(s.AssembleDisplayPc(0));
            Assert.Greater(t0.MinutesUntil(s.Clock.Now), 0);
            Assert.IsFalse(s.AssembleDisplayPc(0));

            GameSession l = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(1, l.PlayerShop.DisplayPcs.Count);
            Assert.AreEqual(0, l.PlayerShop.DisplayPcs[0]);
        }

        [Test]
        public void RealLayout_MatchesDesign()
        {
            string json = System.IO.File.ReadAllText("Assets/Resources/Data/city_layout.json");
            CityLayout city = CityLayout.FromData(UnityEngine.JsonUtility.FromJson<CityLayoutData>(json));
            var shops = city.OfKind(PlaceKind.Shop).ToList();
            Assert.AreEqual(20, shops.Count);
            Assert.AreEqual(4, shops.Count(p => p.Size == PCShopSell.Core.Shop.ShopSize.Large));
            Assert.AreEqual(8, shops.Count(p => p.Size == PCShopSell.Core.Shop.ShopSize.Medium));
            Assert.AreEqual(8, shops.Count(p => p.Size == PCShopSell.Core.Shop.ShopSize.Small));

            var s = new GameSession(city, 1, Money.FromYuan(5000), GameTimestamp.From(new GameDate(0), 480));
            Assert.AreEqual(0.4, s.PlayerShop.LocationFactor, 1e-9, "玩家的店离地铁最远");
            double toMarket = Pathfinder.FindPath(city.Map, s.PlayerShopPlace.NodeId, city.GetPlace("stall_cpu").NodeId).Distance;
            double toHome = Pathfinder.FindPath(city.Map, s.PlayerShopPlace.NodeId, s.Home.NodeId).Distance;
            Assert.That(toMarket, Is.InRange(1800, 2400), "电子市场约 2 公里");
            Assert.That(toHome, Is.InRange(450, 750), "出租屋约 5 条街");
            Assert.AreEqual(3, city.Patrols.Count);
            foreach (Place p in city.OfKind(PlaceKind.Wholesaler)) Assert.Greater(p.Sells.Count, 0);
            foreach (PartCategory c in PartCatalog.Categories)
                Assert.IsTrue(city.OfKind(PlaceKind.Wholesaler).Any(p => p.Sells.Contains(c)), $"有档口卖 {c}");
        }

        [Test]
        public void FullDay_Simulation_IsStable()
        {
            GameSession s = Make();
            Teleport(s, "pc");
            for (int i = 0; i < 300; i++)
            {
                s.AdvanceClock(2);
                if (s.PlayerShop.Queue.Count > 0) s.Checkout(s.PlayerShop.Queue[0].Id);
            }
            Assert.GreaterOrEqual(s.PlayerShop.TodayCustomers, 0);
            Assert.IsFalse(s.IsGameOver);
        }
    }
}
