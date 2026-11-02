using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class CheckoutPatienceTests
    {
        private static ShopSimulation Make(out GameClock clock,out Ledger ledger,bool stock=true)
        {
            clock=new GameClock(new GameDate(0),600);ledger=new Ledger();var random=new GameRandom(1);
            var shop=new ShopSimulation("patience",ShopSize.Large,clock,ledger,new HardwareMarket(random),random,ShopSimulationTests.BusySettings());
            shop.PlayerPresent=true;shop.Markup=1;
            if(stock)foreach(var p in PartCatalog.All)shop.Stock.ForceAdd(p,true,100);
            return shop;
        }
        [Test]
        public void SelectedGoodsRemainReservedUntilFifteenActiveSecondsThenReturnOnce()
        {
            using(var shop=Make(out var clock,out var ledger))
            {
                double seconds=0;shop.CheckoutClock=()=>seconds;
                int initial=shop.Stock.Lines().Sum(l=>l.Count);
                clock.Advance(3);Assert.Greater(shop.Queue.Count,0);var customer=shop.Queue[0];
                shop.Settings.BaseCustomersPerHour=0;
                seconds=14.9;clock.Advance(90);
                Assert.AreEqual(CustomerState.WaitingCheckout,customer.State,"游戏分钟与倍速不能把现场15秒耐心压缩掉");
                Assert.AreEqual(14.9,shop.CheckoutElapsedSeconds(customer).Value,1e-6);Assert.AreEqual(Money.Zero,ledger.Balance);
                seconds=15.1;clock.Advance(1);
                Assert.AreEqual(CustomerState.Left,customer.State);Assert.AreEqual(LeaveReason.Impatient,customer.Reason);
                seconds=30.1;clock.Advance(1);
                Assert.AreEqual(initial,shop.Stock.Lines().Sum(l=>l.Count),"未结账的商品全部归还");
                clock.Advance(5);Assert.AreEqual(initial,shop.Stock.Lines().Sum(l=>l.Count));
            }
        }
        [Test]
        public void CanCheckOutImmediatelyWithoutForcingAFifteenSecondDelay()
        {
            using(var shop=Make(out var clock,out var ledger))
            {
                shop.CheckoutClock=()=>0;clock.Advance(3);Assert.Greater(shop.Queue.Count,0);
                var customer=shop.Queue[0];Assert.GreaterOrEqual(shop.Checkout(customer.Id),0);
                Assert.AreEqual(CustomerState.Paid,customer.State);Assert.AreEqual(customer.Spend,ledger.Balance);
                Assert.AreEqual(-1,shop.Checkout(customer.Id));Assert.AreEqual(customer.Spend,ledger.Balance);
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void NoStockAndUnacceptablePricesCanStillLeaveImmediately(bool expensive)
        {
            using(var shop=Make(out var clock,out _,expensive))
            {
                var left=new List<ShopCustomer>();shop.CustomerLeft+=left.Add;shop.CheckoutClock=()=>0;
                if(expensive)shop.Markup=4;
                clock.Advance(10);
                Assert.True(left.Any(c=>c.Reason==(expensive?LeaveReason.TooExpensive:LeaveReason.NoStock)));
                Assert.True(left.All(c=>c.Reason!=LeaveReason.Impatient),"只延长排队耐心，不延长挑选或拒绝购买");
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void QueueClockUsesActiveRealTimeRegardlessOfSpeedAndStopsForPauseAndWindowHolds(int speed)
        {
            using(var s=GameSessionTests.Make())
            {
                GameSessionTests.Teleport(s,"pc");s.EventsEnabled=false;s.PlayerShop.Settings.BaseCustomersPerHour=0;s.Time.SetSpeed(speed);
                var clock=s.PlayerShop.CheckoutClock;double before=clock();
                for(int i=0;i<20;i++)s.Tick(.1);
                Assert.AreEqual(before+2,clock(),1e-6);
                s.Time.SetPaused(true);for(int i=0;i<50;i++)s.Tick(.1);Assert.AreEqual(before+2,clock(),1e-6);
                s.Time.SetPaused(false);s.Time.Hold();for(int i=0;i<50;i++)s.Tick(.1);Assert.AreEqual(before+2,clock(),1e-6);
                s.Time.Release();s.Tick(.1);Assert.AreEqual(before+2.1,clock(),1e-6);
            }
        }
    }
}
