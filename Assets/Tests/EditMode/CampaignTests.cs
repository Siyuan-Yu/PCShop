using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;

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
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.GiveGift(Npcs.Zhang));
            Assert.AreEqual((before - Npcs.Get(Npcs.Zhang).GiftPrice).Cents, s.Ledger.Balance.Cents);
            Assert.Greater(s.Relation(Npcs.Zhang), 22);
            Assert.IsFalse(s.GiveGift(Npcs.Zhang), "冷却期内不能再送");
            s.Clock.Restore(s.Clock.Today.AddDays(Npcs.GiftCooldownDays), 9 * 60);
            Assert.IsTrue(s.GiveGift(Npcs.Zhang));
        }

        [Test]
        public void Gift_NeedsMoney()
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

    public class ExpansionTests
    {
        private static GameSession InShop(long cash = 2000000, int day = 150)
        {
            GameSession s = GameSessionTests.Make(cash);
            GameSessionTests.Teleport(s, "pc");
            s.Clock.Restore(new GameDate(day), 10 * 60);
            return s;
        }

        [Test]
        public void Medium_NeedsDay_Reputation_AndMoney()
        {
            ShopExpansionDef med = ShopExpansions.Get(ShopSize.Medium);

            GameSession early = InShop(day: 10);
            early.PlayerShop.Reputation = 50;
            StringAssert.Contains("天", early.ExpansionBlocker(med));

            GameSession unknown = InShop();
            unknown.PlayerShop.Reputation = 5;
            StringAssert.Contains("知名度", unknown.ExpansionBlocker(med));

            GameSession poor = InShop(1000);
            poor.PlayerShop.Reputation = 50;
            Assert.AreEqual("钱不够", poor.ExpansionBlocker(med));

            GameSession ok = InShop();
            ok.PlayerShop.Reputation = 50;
            Assert.IsNull(ok.ExpansionBlocker(med));
        }

        [Test]
        public void Large_NeedsMediumFirst()
        {
            GameSession s = InShop(day: 500);
            s.PlayerShop.Reputation = 80;
            Assert.IsFalse(s.BuyExpansion(ShopSize.Large));
            Assert.IsTrue(s.BuyExpansion(ShopSize.Medium));
            Assert.IsTrue(s.BuyExpansion(ShopSize.Large));
            Assert.AreEqual(ShopSize.Large, s.PlayerShop.Size);
        }

        [Test]
        public void Buying_ChangesCapacityTrafficAndRent()
        {
            GameSession s = InShop();
            s.PlayerShop.Reputation = 50;
            double rate = s.PlayerShop.ArrivalRatePerMinute(GameTimestamp.From(new GameDate(150), 12 * 60));
            Money cash = s.Ledger.Balance;
            ShopSize announced = ShopSize.Small;
            s.ShopExpanded += z => announced = z;

            Assert.IsTrue(s.BuyExpansion(ShopSize.Medium));
            Assert.AreEqual(ShopSize.Medium, announced);
            Assert.AreEqual((cash - ShopExpansions.Get(ShopSize.Medium).Price).Cents, s.Ledger.Balance.Cents);
            Assert.AreEqual(ShopSettings.Capacity(ShopSize.Medium), s.PlayerShop.Stock.Capacity, 1e-9);
            Assert.AreEqual(ShopExpansions.Get(ShopSize.Medium).MonthlyRent.Cents, s.CurrentShopRent.Cents);
            double after = s.PlayerShop.ArrivalRatePerMinute(GameTimestamp.From(new GameDate(150), 12 * 60));
            Assert.AreEqual(1.6, after / rate, 1e-6);
        }

        [Test]
        public void MustBeInShop()
        {
            GameSession s = GameSessionTests.Make(2000000);
            s.Clock.Restore(new GameDate(150), 10 * 60);
            s.PlayerShop.Reputation = 50;
            Assert.IsFalse(s.BuyExpansion(ShopSize.Medium));
        }

        [Test]
        public void SaveLoad_KeepsSizeAndRent()
        {
            GameSession s = InShop();
            s.PlayerShop.Reputation = 50;
            s.BuyExpansion(ShopSize.Medium);
            s.BuyUpgrade(ShopUpgrades.Shelf1);
            GameSession loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(ShopSize.Medium, loaded.PlayerShop.Size);
            Assert.AreEqual(s.PlayerShop.Stock.Capacity, loaded.PlayerShop.Stock.Capacity, 1e-9);
            Assert.AreEqual(s.CurrentShopRent.Cents, loaded.CurrentShopRent.Cents);
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
