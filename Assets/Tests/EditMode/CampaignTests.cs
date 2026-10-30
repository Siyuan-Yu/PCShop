using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class NpcTests
    {
        private static GameSession Make(long cash = 20000) => GameSessionTests.Make(cash);

        [Test]
        public void Meet_MarksAsMet_AndSetsStartingRelation()
        {
            GameSession s = Make();
            Assert.IsFalse(s.IsMet(Npcs.Zhang));
            s.Meet(Npcs.Zhang, 22);
            Assert.IsTrue(s.IsMet(Npcs.Zhang));
            Assert.AreEqual(22, s.Relation(Npcs.Zhang));
            s.Meet(Npcs.Zhang, 5);
            Assert.AreEqual(22, s.Relation(Npcs.Zhang), "再次认识不会拉低交情");
        }

        [Test]
        public void Relation_IsClampedTo0And100()
        {
            GameSession s = Make();
            s.Meet(Npcs.Lin);
            s.AddRelation(Npcs.Lin, 500);
            Assert.AreEqual(100, s.Relation(Npcs.Lin));
            s.AddRelation(Npcs.Lin, -500);
            Assert.AreEqual(0, s.Relation(Npcs.Lin));
        }

        [Test]
        public void Gift_NeedsMeeting_CostsMoney_AndHasCooldown()
        {
            GameSession s = Make();
            Assert.IsFalse(s.GiveGift(Npcs.Zhang), "没认识的人不能送礼");
            s.Meet(Npcs.Zhang, 20);
            Assert.IsNotNull(s.GiftBlocker(Npcs.Zhang), "背包里没烟，送不了");
            Assert.IsFalse(s.GiveGift(Npcs.Zhang));
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 2);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.GiveGift(Npcs.Zhang));
            Assert.AreEqual(before.Cents, s.Ledger.Balance.Cents, "送的是背包里的烟，不再当场花钱");
            Assert.AreEqual(1, s.Backpack.GiftCount(PCShopSell.Core.Home.Gifts.Cig));
            Assert.Greater(s.Relation(Npcs.Zhang), 22);
            Assert.IsFalse(s.GiveGift(Npcs.Zhang), "冷却期内不能再送");
            s.Clock.Restore(s.Clock.Today.AddDays(Npcs.GiftCooldownDays), 9 * 60);
            Assert.IsTrue(s.GiveGift(Npcs.Zhang));
        }

        [Test]
        public void Gift_MealStillPaidOnTheSpot()
        {
            GameSession s = Make(50);
            s.Meet(Npcs.Jie, 20);
            Assert.IsFalse(s.GiveGift(Npcs.Jie));
        }

        [Test]
        public void Relation_DecaysSlowly_ButNotBelow20()
        {
            GameSession s = Make();
            s.Meet(Npcs.Zhang, 50);
            s.Meet(Npcs.Jie, 20);
            s.Clock.Restore(new GameDate(5), 23 * 60 + 30);
            s.AdvanceClock(60);
            Assert.Less(s.Relation(Npcs.Zhang), 50);
            Assert.GreaterOrEqual(s.Relation(Npcs.Zhang), 49);
            Assert.AreEqual(20, s.Relation(Npcs.Jie));
        }

        [Test]
        public void Zhang_RaisesTraffic()
        {
            GameSession s = Make();
            s.Meet(Npcs.Zhang, 10);
            Assert.AreEqual(1.0, s.PlayerShop.TrafficMultiplier, 1e-9);
            s.AddRelation(Npcs.Zhang, 35);
            Assert.AreEqual(1.05, s.PlayerShop.TrafficMultiplier, 1e-9);
            s.AddRelation(Npcs.Zhang, 30);
            Assert.AreEqual(1.10, s.PlayerShop.TrafficMultiplier, 1e-9);
        }

        [Test]
        public void Zhou_LowersOnlinePrices()
        {
            GameSession s = Make();
            OnlineStore store = Online1788.GetStore("s_mid");
            PartDef part = PartCatalog.Get("gpu1");
            Money baseline = s.OnlinePrice(store, part);
            s.Meet(Npcs.Zhou, 10);
            s.AddRelation(Npcs.Zhou, 35);
            Assert.Less(s.OnlinePrice(store, part).Cents, baseline.Cents);
        }

        [Test]
        public void Jie_LowersStallPrices()
        {
            GameSession s = Make();
            var stall = s.City.GetPlace("stall");
            PartDef part = PartCatalog.Get("cpu1");
            Money baseline = s.StallPrice(stall, part);
            s.Meet(Npcs.Jie, 20);
            s.AddRelation(Npcs.Jie, 35);
            Assert.Less(s.StallPrice(stall, part).Cents, baseline.Cents);
        }

        [Test]
        public void Landlady_LowersShopRent()
        {
            GameSession s = Make();
            Assert.AreEqual(GameSession.ShopRent.Cents, s.CurrentShopRent.Cents);
            s.Meet(Npcs.Landlady, 15);
            s.AddRelation(Npcs.Landlady, 30);
            Assert.AreEqual(GameSession.ShopRent.Scale(0.95).Cents, s.CurrentShopRent.Cents);
        }

        [Test]
        public void Lin_BailsYouOutOnce()
        {
            GameSession s = Make(5000);
            s.Meet(Npcs.Lin, 15);
            s.AddRelation(Npcs.Lin, 40);
            s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(5500), "把账弄成负数");
            Assert.IsTrue(s.Ledger.Balance.IsNegative);

            s.Clock.Restore(new GameDate(10), 23 * 60 + 30);
            s.AdvanceClock(60);
            Assert.AreEqual(1, s.NegativeCashDays);
            s.AdvanceClock(60 * 24);
            Assert.IsTrue(s.Flags.Contains(GameSession.BailoutUsedFlag), "第二天小林来救急");
            Assert.IsFalse(s.IsGameOver);
            Assert.IsFalse(s.Ledger.Balance.IsNegative);
            Assert.AreEqual(0, s.NegativeCashDays);
        }

        [Test]
        public void Lin_DoesNotBailOutWithoutFriendship()
        {
            GameSession s = Make(100);
            s.Meet(Npcs.Lin, 15);
            s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(5000), "负数");
            s.Clock.Restore(new GameDate(10), 23 * 60 + 30);
            for (int i = 0; i < 4 && !s.IsGameOver; i++) s.AdvanceClock(60 * 24);
            Assert.IsTrue(s.IsGameOver, "交情不够就是普通破产");
        }

        [Test]
        public void IntroEvents_MeetTheCharacter()
        {
            GameSession s = Make();
            s.RaiseEvent("intro_zhang");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.IsTrue(s.IsMet(Npcs.Zhang));
            Assert.AreEqual(22, s.Relation(Npcs.Zhang));
        }

        [Test]
        public void LandladyRentEvent_CanRaiseRent()
        {
            GameSession s = Make();
            s.Meet(Npcs.Landlady, 20);
            s.RaiseEvent("landlady_rent");
            Assert.IsTrue(s.ResolveEvent(2));
            Assert.AreEqual(1, s.RentHikes);
            Assert.Greater(s.CurrentShopRent.Cents, GameSession.ShopRent.Cents * 0.99);
        }

        [Test]
        public void LandladyRentEvent_DinnerKeepsRent()
        {
            GameSession s = Make();
            s.Meet(Npcs.Landlady, 20);
            s.RaiseEvent("landlady_rent");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(0, s.RentHikes);
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);
            Assert.AreEqual(26, s.Relation(Npcs.Landlady));
        }

        [Test]
        public void LinLoan_DebitsAndRaisesRelation()
        {
            GameSession s = Make();
            s.Meet(Npcs.Lin, 35);
            s.RaiseEvent("lin_loan");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual((before - Money.FromYuan(2000)).Cents, s.Ledger.Balance.Cents);
            Assert.AreEqual(45, s.Relation(Npcs.Lin));
        }

        [Test]
        public void ZhouDeal_AddsStock()
        {
            GameSession s = Make();
            s.Meet(Npcs.Zhou, 30);
            s.RaiseEvent("zhou_deal");
            int gpu = s.PlayerShop.Stock.CountAll(PartCatalog.Get("gpu1"));
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(gpu + 1, s.PlayerShop.Stock.CountAll(PartCatalog.Get("gpu1")));
        }

        [Test]
        public void JiePriceWar_StopsAtHighFriendship()
        {
            GameSession s = Make();
            s.PlayerShop.Markup = 1.4;
            EventDef rival = EventCatalog.Get("rival");
            Assert.IsTrue(rival.When(s));
            s.Meet(Npcs.Jie, 20);
            s.AddRelation(Npcs.Jie, NpcPerks.JieNoPriceWar);
            Assert.IsFalse(rival.When(s));
        }
    }

    public class RealtyTests
    {
        private static GameSession NoShop(long cash = 10000)
        {
            CityLayout city = CityLayout.FromData(GameSessionTests.Layout());
            var s = new GameSession(city, 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, false);
            PackTests.EmptyBag(s);
            return s;
        }

        private static void CrossInto(GameSession s, int day)
        {
            s.Clock.Restore(new GameDate(day - 1), 23 * 60 + 30);
            s.AdvanceClock(60);
        }

        [Test]
        public void NewGame_HasNoShop_StarterUnitListed()
        {
            GameSession s = NoShop();
            Assert.IsFalse(s.HasShop);
            Assert.AreEqual(0, s.CurrentShopRent.Cents);
            Assert.IsTrue(s.PlayerShop.Stock.IsEmpty, "没店就没有开局货");
            Assert.IsTrue(s.IsVacant(s.PlayerShopPlace));
            Assert.AreEqual(GameSession.VacantTitle, s.PlaceTitle(s.PlayerShopPlace));
            ShopListing l = s.Listings.Single(x => x.PlaceId == "pc");
            Assert.AreEqual(GameSession.ShopRent.Cents, l.Rent.Cents);
            Assert.LessOrEqual(l.OpenDay, 0);

            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.InPlayerShop, "空铺进去不是自己的店");
            Assert.IsFalse(s.PlayerShop.IsOpen);
            Assert.IsFalse(s.CourierToShop(true), "没店叫车只能送家里");
        }

        [Test]
        public void SignLease_AtAgency_PaysRentDepositAndFee()
        {
            GameSession s = NoShop();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.SignLease("pc"), "不在中介签不了");

            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("pc"));
            Assert.IsTrue(s.HasShop);
            Assert.AreEqual(10000 - 1500 * 3, s.Ledger.Balance.Yuan, "首月 + 押金 + 中介费");
            Assert.AreEqual(1500, s.Lease.Deposit.Yuan);
            // 身家 = 现金 + 押金 + 房东送的 CPU / 显卡的货值
            Assert.Greater(s.NetWorth().Yuan, s.Ledger.Balance.Yuan + 1500, "押金算进身家");
            Assert.Less(s.NetWorth().Yuan, s.Ledger.Balance.Yuan + 1500 + 1500, "多出来的只是送的那 2 件货");
            Assert.AreEqual(GameSession.MyShopName, s.PlaceTitle(s.PlayerShopPlace));
            Assert.IsFalse(s.Listings.Any(x => x.PlaceId == "pc"));

            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.InPlayerShop);
            Assert.IsFalse(s.PlayerShop.IsOpen, "租来的是毛坯铺子，没摆收银台和货架开不了业");
        }

        [Test]
        public void UpcomingListing_OpensOnDay_ThenExpires()
        {
            GameSession s = NoShop(200000);
            s.PlayerShop.Reputation = 80;
            s.DebugAddListing("tea", 5, 10, Money.FromYuan(9000));
            Place tea = s.City.GetPlace("tea");
            GameSessionTests.Teleport(s, "agency");
            StringAssert.Contains("还没搬走", s.LeaseBlocker(s.Listings.Single(x => x.PlaceId == "tea")));
            Assert.IsFalse(s.IsVacant(tea));

            CrossInto(s, 5);
            Assert.IsTrue(s.IsVacant(tea), "到期那天空出来");
            Assert.IsNull(s.LeaseBlocker(s.Listings.Single(x => x.PlaceId == "tea")));

            CrossInto(s, 11);
            Assert.IsFalse(s.Listings.Any(x => x.PlaceId == "tea"), "过了挂牌期被别人租走");
            Assert.IsFalse(s.IsVacant(tea));
        }

        [Test]
        public void BigShop_NeedsReputation()
        {
            GameSession s = GameSessionTests.Make(200000);
            ShopListing l = s.DebugAddListing("tea", 0, 30);
            s.PlayerShop.Reputation = 10;
            StringAssert.Contains("知名度", s.LeaseBlocker(l));
            s.PlayerShop.Reputation = 80;
            Assert.IsNull(s.LeaseBlocker(l));
        }

        [Test]
        public void Moving_ChangesPlaceSizeTrafficAndRent()
        {
            GameSession s = GameSessionTests.Make(200000);
            s.PlayerShop.Reputation = 80;
            s.Clock.Restore(new GameDate(150), 10 * 60);
            double rate = s.PlayerShop.ArrivalRatePerMinute(GameTimestamp.From(new GameDate(150), 12 * 60));
            double lf = s.PlayerShop.LocationFactor;
            ShopListing l = s.DebugAddListing("tea", 0, 200, Money.FromYuan(20000));
            ShopSize announced = ShopSize.Small;
            s.ShopExpanded += z => announced = z;
            Money cash = s.Ledger.Balance;
            Money cost = s.LeaseCost(l);

            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea"));
            Assert.AreEqual(ShopSize.Large, announced);
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            Assert.AreEqual(ShopSize.Large, s.PlayerShop.Size);
            Assert.AreEqual((cash - cost).Cents, s.Ledger.Balance.Cents);
            Assert.AreEqual(20000 * 3 + 800, cost.Yuan, "首月 + 押金 + 中介费 + 搬家费（旧店没押金）");
            Assert.AreEqual(20000, s.CurrentShopRent.Yuan);
            Assert.AreEqual(ShopSettings.Capacity(ShopSize.Large), s.PlayerShop.Stock.Capacity, 1e-9);
            Assert.Greater(s.PlayerShop.LocationFactor, lf, "离地铁更近");
            Assert.Greater(s.PlayerShop.ArrivalRatePerMinute(GameTimestamp.From(new GameDate(150), 12 * 60)), rate);
            Assert.IsTrue(s.IsVacant(s.City.GetPlace("pc")), "旧铺子空出来了");
            Assert.IsFalse(s.PlayerShop.Stock.IsEmpty, "货跟着搬过去");
        }

        [Test]
        public void SaveLoad_KeepsLeaseListingsAndNoShopState()
        {
            GameSession a = NoShop();
            a.DebugAddListing("tea", 40, 80, Money.FromYuan(7000));
            GameSession la = GameSession.Load(a.City, a.CreateSave());
            Assert.IsFalse(la.HasShop);
            Assert.IsTrue(la.Listings.Any(x => x.PlaceId == "pc"));
            Assert.AreEqual(7000, la.Listings.Single(x => x.PlaceId == "tea").Rent.Yuan);

            GameSessionTests.Teleport(a, "agency");
            a.SignLease("pc");
            GameSession lb = GameSession.Load(a.City, a.CreateSave());
            Assert.IsTrue(lb.HasShop);
            Assert.AreEqual("pc", lb.PlayerShopPlace.Id);
            Assert.AreEqual(a.Lease.Deposit.Cents, lb.Lease.Deposit.Cents);
            Assert.AreEqual(a.CurrentShopRent.Cents, lb.CurrentShopRent.Cents);
        }

        [Test]
        public void OldSave_WithoutLease_KeepsTheShop()
        {
            GameSession s = GameSessionTests.Make();
            SaveData d = s.CreateSave();
            d.shopState = 0;
            d.listings = null;
            d.vacantPlaces = null;
            GameSession l = GameSession.Load(s.City, d);
            Assert.IsTrue(l.HasShop);
            Assert.AreEqual(GameSession.ShopRent.Cents, l.CurrentShopRent.Cents);
        }

        [Test]
        public void DebugRentStarterShop_RecreatesOldStart()
        {
            GameSession s = NoShop();
            Assert.IsTrue(s.DebugRentStarterShop());
            Assert.IsTrue(s.HasShop);
            Assert.AreEqual("pc", s.PlayerShopPlace.Id);
            Assert.IsFalse(s.PlayerShop.Stock.IsEmpty, "送开局货");
            Assert.AreEqual(10000 - 4500, s.Ledger.Balance.Yuan);
            Assert.IsFalse(s.DebugRentStarterShop(), "有店了不能再用");
        }
    }

    public class CampaignTests
    {
        private static GameSession Make(long cash = 5000) => GameSessionTests.Make(cash);

        /// <summary>把时间调到某天前夜，再推一个小时跨过午夜。</summary>
        private static void CrossInto(GameSession s, int day)
        {
            s.Clock.Restore(new GameDate(day - 1), 23 * 60 + 30);
            s.AdvanceClock(60);
        }

        [Test]
        public void Goals_ListInOrder_AndNextGoalAdvances()
        {
            GameSession s = Make();
            List<GoalLine> goals = s.Goals();
            CollectionAssert.AreEqual(new[] { "m1", "y1", "y2", "end" }, goals.Select(g => g.Id).ToArray());
            Assert.AreEqual("m1", s.NextGoal().Id);
            CrossInto(s, 31);
            Assert.AreEqual("y1", s.NextGoal().Id);
        }

        [Test]
        public void Year1_Passed_GivesReputation()
        {
            GameSession s = Make();
            s.Ledger.Credit(s.Clock.Today, "测试", Campaign.Year1Wealth, "");
            s.PlayerShop.Reputation = 40;
            CrossInto(s, Campaign.Year1Day);
            Assert.IsTrue(s.MilestonePassed("y1"));
            Assert.AreEqual(0, s.RentHikes);
            Assert.Greater(s.PlayerShop.Reputation, 40);
            Assert.IsTrue(s.HasUpgrade(PCShopSell.Core.Shop.ShopUpgrades.SelfCheckout), "站稳一年奖励自助结账平台");
            Assert.IsTrue((s.PlayerShop.Accepted & PCShopSell.Core.Shop.PaymentMethods.SelfCheckout) != 0);
        }

        [Test]
        public void Year1_Failed_RaisesRent()
        {
            GameSession s = Make();
            var popups = new List<string>();
            s.Popup += (t, b) => popups.Add(t);
            CrossInto(s, Campaign.Year1Day);
            Assert.IsTrue(s.MilestoneEvaluated("y1"));
            Assert.IsFalse(s.MilestonePassed("y1"));
            Assert.AreEqual(1, s.RentHikes);
            Assert.AreEqual(GameSession.ShopRent.Scale(1.1).Cents, s.CurrentShopRent.Cents);
            Assert.IsTrue(popups.Any(t => t.Contains("未达标")));
            Assert.IsFalse(s.IsGameOver, "没达标不会直接结束游戏");
            Assert.IsFalse(s.HasUpgrade(PCShopSell.Core.Shop.ShopUpgrades.SelfCheckout), "没达标就没有奖励");
        }

        [Test]
        public void Year1_NeedsReputationToo()
        {
            GameSession s = Make();
            s.Ledger.Credit(s.Clock.Today, "测试", Campaign.Year1Wealth, "");
            s.PlayerShop.Reputation = 5;
            CrossInto(s, Campaign.Year1Day);
            Assert.IsFalse(s.MilestonePassed("y1"));
        }

        [Test]
        public void Year2_NeedsExpansion()
        {
            GameSession s = Make();
            s.Ledger.Credit(s.Clock.Today, "测试", Campaign.Year2Wealth, "");
            CrossInto(s, Campaign.Year2Day);
            Assert.IsFalse(s.MilestonePassed("y2"), "没扩建就算钱够也不行");

            GameSession t = Make();
            t.Ledger.Credit(t.Clock.Today, "测试", Campaign.Year2Wealth, "");
            t.PlayerShop.Size = ShopSize.Medium;
            CrossInto(t, Campaign.Year2Day);
            Assert.IsTrue(t.MilestonePassed("y2"));
        }

        [Test]
        public void RentHikes_AreCappedAtThree()
        {
            GameSession s = Make();
            for (int i = 0; i < 5; i++) s.RaiseEvent("landlady_rent");
            for (int i = 0; i < 5; i++)
            {
                s.RaiseEvent("landlady_rent");
                s.ResolveEvent(2);
            }
            Assert.AreEqual(3, s.RentHikes);
        }

        [Test]
        public void Ending_Scrape_ForTheBrokeButAlive()
        {
            GameSession s = Make();
            CrossInto(s, Campaign.TotalDays);
            Assert.AreEqual("scrape", s.EndingId);
            Assert.IsFalse(s.IsGameOver);
        }

        [Test]
        public void Ending_Tiers_FollowWealth()
        {
            GameSession steady = Make();
            steady.Ledger.Credit(steady.Clock.Today, "测试", Campaign.TierSteady, "");
            CrossInto(steady, Campaign.TotalDays);
            Assert.AreEqual("steady", steady.EndingId);

            GameSession star = Make();
            star.Ledger.Credit(star.Clock.Today, "测试", Campaign.TierStar, "");
            CrossInto(star, Campaign.TotalDays);
            Assert.AreEqual("star", star.EndingId);

            GameSession richButSmall = Make();
            richButSmall.Ledger.Credit(richButSmall.Clock.Today, "测试", Campaign.TierBoss, "");
            richButSmall.PlayerShop.Reputation = 90;
            CrossInto(richButSmall, Campaign.TotalDays);
            Assert.AreEqual("star", richButSmall.EndingId, "没有大型店拿不到大亨结局");

            GameSession boss = Make();
            boss.Ledger.Credit(boss.Clock.Today, "测试", Campaign.TierBoss, "");
            boss.PlayerShop.Size = ShopSize.Large;
            boss.PlayerShop.Reputation = 90;
            CrossInto(boss, Campaign.TotalDays);
            Assert.AreEqual("boss", boss.EndingId);
        }

        [Test]
        public void Ending_OnlyOnce_AndGameContinues()
        {
            GameSession s = Make();
            int endings = 0;
            s.Popup += (t, b) => { if (t.StartsWith("三年之后")) endings++; };
            CrossInto(s, Campaign.TotalDays);
            CrossInto(s, Campaign.TotalDays + 5);
            Assert.AreEqual(1, endings);
            Assert.IsFalse(s.IsGameOver);
        }

        [Test]
        public void Epilogue_MentionsCloseFriends()
        {
            GameSession s = Make();
            s.Meet(Npcs.Lin, 15);
            s.AddRelation(Npcs.Lin, 70);
            string text = s.ComposeEpilogue(Campaign.Steady);
            StringAssert.Contains("小林", text);
            StringAssert.DoesNotContain("老周", text);
        }

        [Test]
        public void SaveLoad_KeepsCampaignAndRelations()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            s.Meet(Npcs.Zhang, 20);
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 1);
            s.GiveGift(Npcs.Zhang);
            CrossInto(s, Campaign.Year1Day);
            CrossInto(s, Campaign.TotalDays);
            Assert.IsNotNull(s.EndingId);

            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(s.EndingId, loaded.EndingId);
            Assert.AreEqual(s.RentHikes, loaded.RentHikes);
            Assert.AreEqual(s.MilestonePassed("y1"), loaded.MilestonePassed("y1"));
            Assert.IsTrue(loaded.MilestoneEvaluated("y2"));
            Assert.AreEqual(s.Relation(Npcs.Zhang), loaded.Relation(Npcs.Zhang));
            Assert.IsTrue(loaded.IsMet(Npcs.Zhang));
            Assert.AreEqual(s.CurrentShopRent.Cents, loaded.CurrentShopRent.Cents);
        }

        [Test]
        public void SaveLoad_OldSavesWithoutNewFields_StillLoad()
        {
            GameSession s = Make();
            SaveData d = s.CreateSave();
            d.milestoneIds = null;
            d.milestonePassed = null;
            d.relationIds = null;
            d.relationValues = null;
            d.giftIds = null;
            d.giftDays = null;
            d.endingId = null;
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.IsNull(loaded.EndingId);
            Assert.AreEqual(0, loaded.Relation(Npcs.Zhang));
        }

        [Test]
        public void NpcEvents_AreRegistered_AndHaveUniqueIds()
        {
            Assert.GreaterOrEqual(NpcEvents.All.Count, 10);
            var ids = EventCatalog.All.Select(e => e.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            foreach (EventDef def in NpcEvents.All) Assert.IsNotNull(EventCatalog.Get(def.Id));
        }

        [Test]
        public void ThreeYears_WithEventsEnabled_RunsWithoutErrors()
        {
            for (ulong seed = 11; seed <= 12; seed++)
            {
                var bot = new EconomyBot(seed);
                bot.Run(Campaign.TotalDays + 5);
                Assert.IsTrue(bot.Session.EndingId != null || bot.GameOverDay >= 0, "跑满三年要么有结局，要么中途破产");
            }
        }
    }
}
