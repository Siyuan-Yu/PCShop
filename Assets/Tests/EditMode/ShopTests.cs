using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class TimeControllerTests
    {
        [Test]
        public void Tick_AccumulatesFractionalMinutes()
        {
            var t = new TimeController(0.25);
            Assert.AreEqual(0, t.Tick(0.1));
            Assert.AreEqual(0, t.Tick(0.1));
            Assert.AreEqual(1, t.Tick(0.1));
        }

        [Test]
        public void Speed_MultipliesMinutes()
        {
            var t = new TimeController(0.25);
            t.SetSpeed(4);
            Assert.AreEqual(4, t.Tick(0.25));
        }

        [Test]
        public void HoldAndPause_StopTime()
        {
            var t = new TimeController(0.25);
            t.Hold();
            Assert.AreEqual(0, t.Tick(0.25));
            t.Release();
            t.SetPaused(true);
            Assert.AreEqual(0, t.Tick(0.25));
            t.SetSpeed(1);
            Assert.IsTrue(t.IsRunning);
            Assert.AreEqual(1, t.Tick(0.25));
        }

        [Test]
        public void LongFrame_IsClamped()
        {
            var t = new TimeController(0.25);
            Assert.AreEqual(1, t.Tick(5.0));
        }

        [Test]
        public void Release_WithoutHold_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new TimeController().Release());
        }
    }

    public class ShopSimulationTests
    {
        internal static ShopSettings BusySettings() => new ShopSettings
        {
            BaseCustomersPerHour = 600,
            HourFactors = Enumerable.Repeat(1.0, 24).ToArray(),
            CashCarryChance = 1.0,
            MinBrowseMinutes = 2,
            MaxBrowseMinutes = 2,
            PatienceMinutes = 5,
            BuildOrderChance = 0,
        };

        private sealed class Rig
        {
            public GameClock Clock;
            public Ledger Ledger;
            public HardwareMarket Market;
            public ShopSimulation Shop;
        }

        private static Rig Make(ShopSettings settings, ulong seed = 1, bool stock = true)
        {
            var rng = new GameRandom(seed);
            var r = new Rig
            {
                Clock = new GameClock(new GameDate(0), 10 * 60),
                Ledger = new Ledger(),
            };
            r.Market = new HardwareMarket(rng);
            r.Shop = new ShopSimulation("s", ShopSize.Large, r.Clock, r.Ledger, r.Market, rng, settings);
            r.Shop.Markup = 1.0;
            if (stock)
            {
                foreach (PartDef p in PartCatalog.All) r.Shop.Stock.ForceAdd(p, true, 20);
            }
            return r;
        }

        [Test]
        public void ClosedShop_GetsNoCustomers()
        {
            Rig r = Make(BusySettings());
            r.Clock.Advance(120);
            Assert.AreEqual(0, r.Shop.TodayCustomers);
        }

        [Test]
        public void OpenShop_CustomersPickStockThenQueue()
        {
            Rig r = Make(BusySettings());
            int before = r.Shop.Stock.Lines().Sum(l => l.Count);
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(3);
            Assert.Greater(r.Shop.TodayCustomers, 0);
            Assert.Greater(r.Shop.Queue.Count, 0);
            Assert.IsTrue(r.Shop.Queue.All(c => c.State == CustomerState.WaitingCheckout && c.Item != null));
            Assert.AreEqual(before - r.Shop.Queue.Count, r.Shop.Stock.Lines().Sum(l => l.Count));
        }

        [Test]
        public void Checkout_CreditsLedgerAndReturnsMinutes()
        {
            Rig r = Make(BusySettings());
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(3);
            ShopCustomer c = r.Shop.Queue[0];

            int minutes = r.Shop.Checkout(c.Id);

            Assert.AreEqual(r.Shop.Settings.CheckoutMinutesCash, minutes);
            Assert.AreEqual(c.Spend, r.Ledger.Balance);
            Assert.AreEqual(r.Shop.PriceOf(c.Item, c.ItemBoxed), c.Spend);
            Assert.AreEqual(CustomerState.Paid, c.State);
            Assert.AreEqual(-1, r.Shop.Checkout(c.Id));
        }

        [Test]
        public void WaitingTooLong_CustomerLeaves_ItemReturned()
        {
            Rig r = Make(BusySettings());
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(3);
            ShopCustomer c = r.Shop.Queue[0];
            PartDef item = c.Item;
            bool boxed = c.ItemBoxed;
            int stockBefore = r.Shop.Stock.Count(item, boxed);
            r.Shop.Settings.BaseCustomersPerHour = 0;
            r.Clock.Advance(10);
            Assert.AreEqual(CustomerState.Left, c.State);
            Assert.AreEqual(LeaveReason.Impatient, c.Reason);
            Assert.Greater(r.Shop.Stock.Count(item, boxed), stockBefore);
        }

        [Test]
        public void DayClosed_ReportsPaidAndLostByReason()
        {
            Rig r = Make(BusySettings(), stock: false);
            DayReport report = null;
            r.Shop.DayClosed += d => report = d;
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(5);
            r.Shop.PlayerPresent = false;
            r.Clock.Advance(24 * 60);
            Assert.IsNotNull(report, "过了零点要结算昨天");
            Assert.Greater(report.Customers, 0);
            Assert.Greater(report.LostNoStock, 0);
            Assert.AreEqual(0, report.Paid);
            StringAssert.Contains("没货", PCShopSell.Core.Session.GameSession.DayReportText(report));
            StringAssert.Contains("进货", PCShopSell.Core.Session.GameSession.DayReportText(report));
            Assert.AreEqual(0, r.Shop.TodayCustomers, "新的一天从零开始");
        }

        [Test]
        public void EmptyShop_CustomersLeaveForNoStock()
        {
            Rig r = Make(BusySettings(), stock: false);
            int noStock = 0;
            r.Shop.CustomerLeft += c => { if (c.Reason == LeaveReason.NoStock) noStock++; };
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(5);
            Assert.Greater(noStock, 0);
            Assert.AreEqual(0, r.Shop.Queue.Count);
        }

        [Test]
        public void CashOnly_CustomersWithoutCashLeave()
        {
            ShopSettings s = BusySettings();
            s.CashCarryChance = 0.0;
            Rig r = Make(s);
            int noCash = 0;
            r.Shop.CustomerLeft += c => { if (c.Reason == LeaveReason.NoCash) noCash++; };
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(5);
            Assert.Greater(noCash, 0);
            Assert.AreEqual(0, r.Shop.Queue.Count);
        }

        [Test]
        public void QrCode_LetsCashlessCustomersPay()
        {
            ShopSettings s = BusySettings();
            s.CashCarryChance = 0.0;
            Rig r = Make(s);
            r.Shop.Accepted = PaymentMethods.Cash | PaymentMethods.QrCode;
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(3);
            Assert.Greater(r.Shop.Queue.Count, 0);
            Assert.AreEqual(s.CheckoutMinutesQr, r.Shop.Checkout(r.Shop.Queue[0].Id));
        }

        [Test]
        public void HighMarkup_ScaresCustomersAway()
        {
            Rig cheap = Make(BusySettings(), 5);
            Rig pricey = Make(BusySettings(), 5);
            pricey.Shop.Markup = 1.6;
            int lostCheap = 0, lostPricey = 0;
            cheap.Shop.CustomerLeft += c => { if (c.Reason == LeaveReason.TooExpensive) lostCheap++; };
            pricey.Shop.CustomerLeft += c => { if (c.Reason == LeaveReason.TooExpensive) lostPricey++; };
            cheap.Shop.PlayerPresent = pricey.Shop.PlayerPresent = true;
            cheap.Clock.Advance(30);
            pricey.Clock.Advance(30);
            Assert.Greater(lostPricey, lostCheap);
        }

        [Test]
        public void BoxedSellsForMoreThanLoose()
        {
            Rig r = Make(BusySettings());
            PartDef p = PartCatalog.Get("gpu1");
            Assert.IsTrue(r.Shop.PriceOf(p, true) > r.Shop.PriceOf(p, false));
        }

        [Test]
        public void Closing_SendsEveryoneAway_AndReturnsStock()
        {
            Rig r = Make(BusySettings());
            int before = r.Shop.Stock.Lines().Sum(l => l.Count);
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(3);
            r.Shop.PlayerPresent = false;
            r.Clock.Advance(1);
            Assert.AreEqual(0, r.Shop.ActiveCustomers.Count);
            Assert.AreEqual(0, r.Shop.Queue.Count);
            Assert.AreEqual(before, r.Shop.Stock.Lines().Sum(l => l.Count));
        }

        [Test]
        public void Employee_ChecksOutWhenPlayerAway()
        {
            Rig r = Make(BusySettings());
            r.Shop.EmployeeOnDuty = _ => true;
            r.Clock.Advance(30);
            Assert.IsTrue(r.Ledger.Balance > Money.Zero);
        }

        [Test]
        public void BuildOrder_PlacedAndCompleted()
        {
            ShopSettings s = BusySettings();
            s.BuildOrderChance = 1.0;
            Rig r = Make(s);
            r.Shop.Settings.BaseCustomersPerHour = 60;
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(20);
            Assert.Greater(r.Shop.Orders.Count, 0);
            BuildOrder o = r.Shop.Orders[0];
            Assert.AreEqual(0, r.Shop.MissingParts(o).Count);
            Assert.IsTrue(r.Shop.CompleteBuild(o.Id));
            Assert.AreEqual(o.Price, r.Ledger.Balance);
        }

        [Test]
        public void BuildOrder_ExpiresAfterDueDays()
        {
            ShopSettings s = BusySettings();
            s.BuildOrderChance = 1.0;
            Rig r = Make(s);
            int expired = 0;
            r.Shop.OrderExpired += _ => expired++;
            r.Shop.PlayerPresent = true;
            r.Clock.Advance(5);
            r.Shop.PlayerPresent = false;
            Assert.Greater(r.Shop.Orders.Count, 0);
            r.Clock.Advance(3 * 1440);
            Assert.AreEqual(0, r.Shop.Orders.Count);
            Assert.Greater(expired, 0);
        }

        [Test]
        public void SameSeed_SameCustomers()
        {
            Rig a = Make(new ShopSettings(), 42);
            Rig b = Make(new ShopSettings(), 42);
            a.Shop.PlayerPresent = true;
            b.Shop.PlayerPresent = true;
            a.Clock.Advance(600);
            b.Clock.Advance(600);
            Assert.AreEqual(a.Shop.TodayCustomers, b.Shop.TodayCustomers);
            Assert.AreEqual(a.Shop.TodayLost, b.Shop.TodayLost);
        }

        [Test]
        public void ArrivalRate_DependsOnLocationReputationAndWeekend()
        {
            Rig r = Make(new ShopSettings());
            GameTimestamp weekday = GameTimestamp.From(new GameDate(0), 11 * 60);
            double baseRate = r.Shop.ArrivalRatePerMinute(weekday);

            r.Shop.LocationFactor = 0.4;
            Assert.AreEqual(baseRate * 0.4, r.Shop.ArrivalRatePerMinute(weekday), 1e-9);

            r.Shop.Reputation = 50;
            Assert.AreEqual(baseRate * 0.4 * 1.5, r.Shop.ArrivalRatePerMinute(weekday), 1e-9);

            GameTimestamp saturday = GameTimestamp.From(new GameDate(5), 11 * 60);
            Assert.Greater(r.Shop.ArrivalRatePerMinute(saturday), r.Shop.ArrivalRatePerMinute(weekday));
        }

        [Test]
        public void LocationFactor_FarthestIsFortyPercent()
        {
            Assert.AreEqual(1.0, ShopSettings.LocationFactor(0, 100), 1e-9);
            Assert.AreEqual(0.4, ShopSettings.LocationFactor(100, 100), 1e-9);
        }
    }

    public class CityLayoutTests
    {
        internal static CityLayoutData SampleData() => new CityLayoutData
        {
            distanceScale = 2,
            nodes = new[]
            {
                new LayoutNodeData { id = "station", x = 0, y = 0 },
                new LayoutNodeData { id = "a", x = 10, y = 0 },
                new LayoutNodeData { id = "b", x = 20, y = 0 },
                new LayoutNodeData { id = "home", x = 20, y = -50 },
            },
            edges = new[]
            {
                new LayoutEdgeData { a = "station", b = "a" },
                new LayoutEdgeData { a = "a", b = "b" },
                new LayoutEdgeData { a = "b", b = "home" },
            },
            places = new[]
            {
                new LayoutPlaceData { id = "metro", name = "地铁站", kind = "station", node = "station" },
                new LayoutPlaceData { id = "tea", name = "奶茶店", kind = "shop", node = "a", size = "large" },
                new LayoutPlaceData { id = "pc", name = "电脑店", kind = "shop", node = "b", size = "small", player = true },
                new LayoutPlaceData { id = "flat", name = "出租屋", kind = "home", node = "home" },
            },
        };

        [Test]
        public void FromData_ScalesDistancesAndParsesPlaces()
        {
            CityLayout city = CityLayout.FromData(SampleData());
            Assert.AreEqual(20, city.Map.StraightDistance("station", "a"), 1e-9);
            Assert.AreEqual(ShopSize.Large, city.GetPlace("tea").Size);
            Assert.AreEqual("pc", city.PlayerShop().Id);
            Assert.AreEqual(PlaceKind.Home, city.GetPlace("flat").Kind);
        }

        [Test]
        public void EdgeLengthOverride_IsScaled()
        {
            CityLayoutData d = SampleData();
            d.edges[2].length = 400;
            CityLayout city = CityLayout.FromData(d);
            PathResult p = Pathfinder.FindPath(city.Map, "b", "home");
            Assert.AreEqual(800, p.Distance, 1e-9);
        }

        [Test]
        public void SellsAndPatrols_AreParsed()
        {
            CityLayoutData d = SampleData();
            d.places[1].kind = "wholesaler";
            d.places[1].sells = "Cpu, gpu";
            d.patrols = new[] { new LayoutPatrolData { id = "p", nodes = new[] { "station", "b" } } };
            CityLayout city = CityLayout.FromData(d);
            CollectionAssert.AreEqual(new[] { PartCategory.Cpu, PartCategory.Gpu }, city.GetPlace("tea").Sells.ToArray());
            Assert.AreEqual(1, city.Patrols.Count);
        }

        [Test]
        public void NearestNode_UsesViewCoordinates()
        {
            CityLayout city = CityLayout.FromData(SampleData());
            Assert.AreEqual("a", city.NearestNode(11, 1));
            MapPoint p = city.ViewPosition("b");
            Assert.AreEqual(20, p.X, 1e-9);
        }

        [Test]
        public void PlaceWithMissingNode_Throws()
        {
            CityLayoutData data = SampleData();
            data.places[0].node = "nope";
            Assert.Throws<ArgumentException>(() => CityLayout.FromData(data));
        }
    }

    public class StockAndCatalogTests
    {
        [Test]
        public void Loose_TakesLessSpaceThanBoxed()
        {
            PartDef gpu = PartCatalog.Get("gpu0");
            Assert.AreEqual(2.0, Stock.SpaceOf(gpu, true, 1), 1e-9);
            Assert.AreEqual(0.8, Stock.SpaceOf(gpu, false, 1), 1e-9);
        }

        [Test]
        public void Capacity_IsEnforced_AndUnboxFreesSpace()
        {
            var s = new Stock(4);
            PartDef gpu = PartCatalog.Get("gpu0");
            Assert.IsTrue(s.TryAdd(gpu, true, 2));
            Assert.IsFalse(s.TryAdd(gpu, true, 1));
            Assert.IsTrue(s.Unbox(gpu, 2));
            Assert.AreEqual(1.6, s.UsedSpace, 1e-9);
            Assert.IsTrue(s.TryAdd(gpu, true, 1));
            Assert.AreEqual(2, s.Count(gpu, false));
        }

        [Test]
        public void TakeOne_PrefersLoose()
        {
            var s = new Stock(10);
            PartDef p = PartCatalog.Get("ram0");
            s.TryAdd(p, true, 1);
            s.TryAdd(p, false, 1);
            Assert.AreEqual(false, s.TakeOne(p));
            Assert.AreEqual(true, s.TakeOne(p));
            Assert.IsNull(s.TakeOne(p));
        }

        [Test]
        public void Catalog_HasSixCategoriesTimesThreeTiers()
        {
            Assert.AreEqual(18, PartCatalog.All.Count);
            foreach (PartCategory c in PartCatalog.Categories)
            {
                for (int t = 0; t < 3; t++) Assert.IsNotNull(PartCatalog.Find(c, t));
            }
        }

        [Test]
        public void BuildLists_AreCompatible()
        {
            for (int t = 0; t < 3; t++)
                Assert.IsNull(PartCatalog.CheckCompatibility(PartCatalog.BuildList(t)), $"tier {t}");
        }

        [Test]
        public void Compatibility_CatchesSocketAndPower()
        {
            var list = PartCatalog.BuildList(2);
            list[1] = PartCatalog.Get("mb1");
            StringAssert.Contains("不匹配", PartCatalog.CheckCompatibility(list));

            list = PartCatalog.BuildList(2);
            list[5] = PartCatalog.Get("psu0");
            StringAssert.Contains("功率", PartCatalog.CheckCompatibility(list));
        }
    }

    public class MarketTests
    {
        [Test]
        public void ScriptedCryptoBoom_RaisesGpuPrice()
        {
            var m = new HardwareMarket(new GameRandom(3));
            for (int d = 1; d <= 8; d++) m.OnNewDay(new GameDate(d));
            double before = m.Index(PartCategory.Gpu);
            m.OnNewDay(new GameDate(9));
            Assert.IsTrue(m.News.Any(n => n.Headline.Contains("矿潮")));
            Assert.Greater(m.Index(PartCategory.Gpu), before + 0.1);
        }

        [Test]
        public void Index_StaysInRange()
        {
            var m = new HardwareMarket(new GameRandom(9));
            for (int d = 1; d < 400; d++)
            {
                m.OnNewDay(new GameDate(d));
                foreach (PartCategory c in PartCatalog.Categories)
                {
                    Assert.That(m.Index(c), Is.InRange(0.6, 1.8));
                }
            }
            Assert.AreEqual(HardwareMarket.HistoryDays, m.History(PartCategory.Cpu).Count);
        }

        [Test]
        public void Random_IsDeterministic()
        {
            var a = new GameRandom(77);
            var b = new GameRandom(77);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
            var c = new GameRandom(1) { State = a.State };
            Assert.AreEqual(a.NextDouble(), c.NextDouble());
        }
    }
}
