using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
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
        private static ShopSettings BusySettings() => new ShopSettings
        {
            BaseCustomersPerHour = 600,
            HourFactors = Enumerable.Repeat(1.0, 24).ToArray(),
            CashCarryChance = 1.0,
            MinBrowseMinutes = 2,
            MaxBrowseMinutes = 2,
            PatienceMinutes = 5,
        };

        private static (GameClock clock, Ledger ledger, ShopSimulation shop) Make(ShopSettings settings, int seed = 1)
        {
            var clock = new GameClock(new GameDate(0), 10 * 60);
            var ledger = new Ledger();
            var shop = new ShopSimulation("s", clock, ledger, seed, settings);
            return (clock, ledger, shop);
        }

        [Test]
        public void ClosedShop_GetsNoCustomers()
        {
            var (clock, _, shop) = Make(BusySettings());
            clock.Advance(120);
            Assert.AreEqual(0, shop.TodayCustomers);
        }

        [Test]
        public void OpenShop_CustomersBrowseThenQueue()
        {
            var (clock, _, shop) = Make(BusySettings());
            shop.IsOpen = true;
            clock.Advance(3);
            Assert.Greater(shop.TodayCustomers, 0);
            Assert.Greater(shop.Queue.Count, 0);
            Assert.IsTrue(shop.Queue.All(c => c.State == CustomerState.WaitingCheckout));
        }

        [Test]
        public void Checkout_CreditsLedgerAndReturnsMinutes()
        {
            var (clock, ledger, shop) = Make(BusySettings());
            shop.IsOpen = true;
            clock.Advance(3);
            ShopCustomer c = shop.Queue[0];

            int minutes = shop.Checkout(c.Id);

            Assert.AreEqual(shop.Settings.CheckoutMinutesCash, minutes);
            Assert.AreEqual(c.Spend, ledger.Balance);
            Assert.AreEqual(CustomerState.Paid, c.State);
            Assert.AreEqual(-1, shop.Checkout(c.Id));
        }

        [Test]
        public void WaitingTooLong_CustomerLeaves()
        {
            var (clock, _, shop) = Make(BusySettings());
            shop.IsOpen = true;
            clock.Advance(3);
            ShopCustomer c = shop.Queue[0];
            clock.Advance(10);
            Assert.AreEqual(CustomerState.Left, c.State);
            Assert.AreEqual(LeaveReason.Impatient, c.Reason);
        }

        [Test]
        public void CashOnly_CustomersWithoutCashLeave()
        {
            ShopSettings s = BusySettings();
            s.CashCarryChance = 0.0;
            var (clock, _, shop) = Make(s);
            int noCash = 0;
            shop.CustomerLeft += c => { if (c.Reason == LeaveReason.NoCash) noCash++; };
            shop.IsOpen = true;
            clock.Advance(5);
            Assert.Greater(noCash, 0);
            Assert.AreEqual(0, shop.Queue.Count);
        }

        [Test]
        public void QrCode_LetsCashlessCustomersPay()
        {
            ShopSettings s = BusySettings();
            s.CashCarryChance = 0.0;
            var (clock, _, shop) = Make(s);
            shop.Accepted = PaymentMethods.Cash | PaymentMethods.QrCode;
            shop.IsOpen = true;
            clock.Advance(3);
            Assert.Greater(shop.Queue.Count, 0);
            Assert.AreEqual(s.CheckoutMinutesQr, shop.Checkout(shop.Queue[0].Id));
        }

        [Test]
        public void Closing_SendsEveryoneAway()
        {
            var (clock, _, shop) = Make(BusySettings());
            shop.IsOpen = true;
            clock.Advance(3);
            shop.IsOpen = false;
            Assert.AreEqual(0, shop.ActiveCustomers.Count);
            Assert.AreEqual(0, shop.Queue.Count);
        }

        [Test]
        public void SameSeed_SameCustomers()
        {
            var a = Make(new ShopSettings(), 42);
            var b = Make(new ShopSettings(), 42);
            a.shop.IsOpen = true;
            b.shop.IsOpen = true;
            a.clock.Advance(600);
            b.clock.Advance(600);
            Assert.AreEqual(a.shop.TodayCustomers, b.shop.TodayCustomers);
            Assert.AreEqual(a.shop.TodayLost, b.shop.TodayLost);
        }

        [Test]
        public void ArrivalRate_DependsOnLocationSizeReputationAndWeekend()
        {
            var (clock, _, shop) = Make(new ShopSettings());
            GameTimestamp weekday = GameTimestamp.From(new GameDate(0), 11 * 60);
            double baseRate = shop.ArrivalRatePerMinute(weekday);

            shop.LocationFactor = 0.4;
            Assert.AreEqual(baseRate * 0.4, shop.ArrivalRatePerMinute(weekday), 1e-9);

            shop.Size = ShopSize.Large;
            shop.Reputation = 50;
            Assert.AreEqual(baseRate * 0.4 * 2.5 * 1.5, shop.ArrivalRatePerMinute(weekday), 1e-9);

            GameTimestamp saturday = GameTimestamp.From(new GameDate(5), 11 * 60);
            Assert.Greater(shop.ArrivalRatePerMinute(saturday), shop.ArrivalRatePerMinute(weekday));
            _ = clock;
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

    public class GameSessionTests
    {
        private static GameSession Make()
        {
            CityLayout city = CityLayout.FromData(CityLayoutTests.SampleData());
            return new GameSession(city, 7, Money.FromYuan(1000), GameTimestamp.From(new GameDate(0), 8 * 60));
        }

        [Test]
        public void StartsAtHome_ShopClosed()
        {
            GameSession s = Make();
            Assert.AreEqual(s.Home, s.CurrentPlace);
            Assert.IsFalse(s.PlayerShop.IsOpen);
        }

        [Test]
        public void FarthestShop_HasLowestLocationFactor()
        {
            GameSession s = Make();
            Assert.AreEqual(0.4, s.PlayerShop.LocationFactor, 1e-9);
        }

        [Test]
        public void EnterShop_OpensIt_LeaveClosesIt()
        {
            GameSession s = Make();
            s.Leave();
            s.ArriveAtNode("b");
            s.Enter(s.PlayerShopPlace);
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
            s.ArriveAtNode("a");
            Assert.Throws<InvalidOperationException>(() => s.Enter(s.PlayerShopPlace));
        }

        [Test]
        public void Sleep_OnlyAtHome_WakesAtEight()
        {
            GameSession s = Make();
            s.Clock.AdvanceTo(22 * 60);
            Assert.IsTrue(s.Sleep());
            Assert.AreEqual(8 * 60, s.Clock.MinuteOfDay);
            Assert.AreEqual(1, s.Clock.Today.TotalDays);

            s.Leave();
            Assert.IsFalse(s.Sleep());
        }

        [Test]
        public void BedtimeReminder_FiresWhenAwayFromHome()
        {
            GameSession s = Make();
            s.Leave();
            int notices = 0;
            s.Notice += _ => notices++;
            s.Clock.AdvanceTo(23 * 60 + 5);
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
    }
}
