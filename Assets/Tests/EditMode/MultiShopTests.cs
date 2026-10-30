using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public class MultiShopTests
    {
        private static GameSession TwoShops()
        {
            GameSession s = GameSessionTests.Make(200000);
            s.PlayerShop.Reputation = 80;
            s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea", true));
            return s;
        }

        private static void CrossInto(GameSession s, int day)
        {
            s.Clock.Restore(new GameDate(day - 1), 23 * 60 + 30);
            s.AdvanceClock(60);
        }

        [Test]
        public void SecondLease_AsNewShop_KeepsTheOldOne()
        {
            GameSession s = TwoShops();
            Assert.AreEqual(2, s.MyShopCount);
            Assert.IsTrue(s.IsMyShop(s.City.GetPlace("pc")), "旧店还是我的");
            Assert.IsTrue(s.IsMyShop(s.City.GetPlace("tea")));
            Assert.IsFalse(s.IsVacant(s.City.GetPlace("pc")), "旧店不会回到旺铺招租");
            Assert.IsFalse(s.Listings.Any(l => l.PlaceId == "pc" || l.PlaceId == "tea"));
            Assert.AreEqual("tea", s.PlayerShopPlace.Id, "签完切到新店");
            Assert.IsTrue(s.PlayerShop.Stock.IsEmpty, "新店是空的");
            Assert.IsFalse(s.MainShop.Sim.Stock.IsEmpty, "主店的货不动");
            Assert.AreEqual(GameSession.MyShopName + "·二店", s.PlaceTitle(s.City.GetPlace("tea")));
        }

        [Test]
        public void EnteringAShop_SwitchesCurrentShop()
        {
            GameSession s = TwoShops();
            GameSessionTests.Teleport(s, "pc");
            Assert.AreEqual("pc", s.PlayerShopPlace.Id);
            Assert.IsTrue(s.InPlayerShop);
            Assert.IsFalse(s.PlayerShop.Stock.IsEmpty);
            Assert.IsTrue(s.ShopAt("pc").Sim.PlayerPresent);
            Assert.IsFalse(s.ShopAt("tea").Sim.PlayerPresent, "人不在的店不算有人看店");
            GameSessionTests.Teleport(s, "tea");
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            Assert.IsFalse(s.ShopAt("pc").Sim.PlayerPresent);
        }

        [Test]
        public void Rent_IsChargedForEveryShop()
        {
            GameSession s = TwoShops();
            Money total = s.TotalShopRent;
            Assert.AreEqual((s.RentOf(s.MainShop) + Money.FromYuan(3000)).Cents, total.Cents);
            s.Leave();
            Money before = s.Ledger.Balance;
            CrossInto(s, 30);
            long rent = s.Ledger.Entries.Where(e => e.Category == "房租" && e.Date.TotalDays == 30 && (e.Memo ?? "").StartsWith(GameSession.MyShopName)).Sum(e => System.Math.Abs(e.Amount.Cents));
            Assert.AreEqual(total.Cents, rent, "两家店的月租都扣");
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);
        }

        [Test]
        public void OwnShop_CannotBeLeasedAgain()
        {
            GameSession s = TwoShops();
            StringAssert.Contains("已经是你的店", s.LeaseBlocker(s.DebugAddListing("pc", 0, 60), true));
            Assert.AreEqual(2, s.MyShopCount);
        }

        [Test]
        public void SaveLoad_KeepsAllShops()
        {
            GameSession s = TwoShops();
            GameSessionTests.Teleport(s, "tea");
            s.ShopAt("tea").Sim.Markup = 1.4;
            GameSession b = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(2, b.MyShopCount);
            Assert.AreEqual("pc", b.MainShop.Place.Id, "主店还是主店");
            Assert.AreEqual("tea", b.PlayerShopPlace.Id, "人在哪家店就是当前店");
            Assert.AreEqual(1.4, b.ShopAt("tea").Sim.Markup, 1e-9);
            Assert.AreEqual(3000, b.RentOf(b.ShopAt("tea")).Yuan);
            Assert.AreEqual(s.MainShop.Sim.Stock.Lines().Sum(l => l.Count), b.MainShop.Sim.Stock.Lines().Sum(l => l.Count));
            Assert.IsFalse(b.IsVacant(b.City.GetPlace("pc")));
            Assert.IsFalse(b.IsVacant(b.City.GetPlace("tea")));
        }

        [Test]
        public void CloseShop_MovesGoodsHomeAndRefundsDeposit()
        {
            GameSession s = TwoShops();
            int mainCount = s.MainShop.Sim.Stock.Lines().Sum(l => l.Count);
            var part = s.MainShop.Sim.Stock.Lines().First().Part;
            s.ShopAt("tea").Sim.Stock.ForceAdd(part, true, 3);
            Money deposit = s.ShopAt("tea").Lease.Deposit;
            Money before = s.Ledger.Balance;
            Assert.IsNotNull(s.CloseShopBlocker(s.MainShop), "主店不能退");
            Assert.IsTrue(s.CloseShop("tea"));
            Assert.AreEqual(1, s.MyShopCount);
            Assert.AreEqual("pc", s.PlayerShopPlace.Id);
            Assert.AreEqual(mainCount + 3, s.MainShop.Sim.Stock.Lines().Sum(l => l.Count), "货搬回主店");
            Assert.AreEqual((before + deposit).Cents, s.Ledger.Balance.Cents, "押金退回");
            Assert.IsTrue(s.IsVacant(s.City.GetPlace("tea")));
            Assert.IsTrue(s.Listings.Any(l => l.PlaceId == "tea"), "铺子重新挂牌");
        }

        [Test]
        public void SecondLease_AsMove_StillRelocates()
        {
            GameSession s = GameSessionTests.Make(200000);
            s.PlayerShop.Reputation = 80;
            s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea"));
            Assert.AreEqual(1, s.MyShopCount);
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            Assert.IsTrue(s.IsVacant(s.City.GetPlace("pc")));
        }
    }
}
