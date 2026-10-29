using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>进货渠道上的固定人物：认识、折扣与额度、约会、行情消息。</summary>
    public class SocialTests
    {
        private const string Stall = "stall_cpu";

        private static GameSession Make(long cash = 30000, int minute = 10 * 60)
        {
            CityLayoutData data = GameSessionTests.Layout();
            data.places = data.places.Concat(new[]
            {
                new LayoutPlaceData { id = Stall, name = "老李 CPU 档口", kind = "wholesaler", node = "mk", sells = "Cpu,Motherboard" },
            }).ToArray();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), minute));
            GameDate d = new GameDate(0);
            while (d.IsWeekend) d = d.AddDays(1);
            s.Clock.Restore(d, minute);
            return s;
        }

        private static PartDef CpuPart() => PartCatalog.All.First(p => p.Category == PartCategory.Cpu);

        private static void GoToStall(GameSession s) => GameSessionTests.Teleport(s, Stall);

        [Test]
        public void EnteringOwnersStall_MeetsTheOwner_WithAPopup()
        {
            GameSession s = Make();
            string popup = null;
            s.Popup += (title, body) => popup = title;
            Assert.IsFalse(s.IsMet(Npcs.Li));
            GoToStall(s);
            Assert.IsTrue(s.IsMet(Npcs.Li));
            Assert.AreEqual(10, s.Relation(Npcs.Li));
            StringAssert.Contains("老李", popup);
        }

        [Test]
        public void Owner_IsFoundByStallId()
        {
            Assert.AreEqual(Npcs.Li, Npcs.OwnerOf("stall_cpu").Id);
            Assert.AreEqual(Npcs.Wang, Npcs.OwnerOf("stall_gpu").Id);
            Assert.AreEqual(Npcs.Qian, Npcs.OwnerOf("stall_mem").Id);
            Assert.AreEqual(Npcs.Sun, Npcs.OwnerOf("stall_psu").Id);
            Assert.IsNull(Npcs.OwnerOf("nope"));
        }

        [Test]
        public void HigherRelation_GivesCheaperPrice()
        {
            GameSession s = Make();
            GoToStall(s);
            Place stall = s.City.GetPlace(Stall);
            Money strangers = s.StallPrice(stall, CpuPart());
            s.AddRelation(Npcs.Li, 90);
            Money friends = s.StallPrice(stall, CpuPart());
            Assert.Less(friends.Cents, strangers.Cents);
            Assert.AreEqual(0.06, NpcPerks.OwnerDiscount(s.Relation(Npcs.Li)), 1e-9);
        }

        [Test]
        public void Quota_LimitsDailyPurchase_AndGrowsWithRelation()
        {
            GameSession s = Make(200000);
            GoToStall(s);
            Place stall = s.City.GetPlace(Stall);
            PartDef cpu = CpuPart();
            Assert.AreEqual(8, s.StallQuota(stall));
            Assert.IsFalse(s.BuyAtStall(stall, cpu, 9), "超出额度不能买");
            Assert.AreEqual(0, s.StallBoughtToday(stall, cpu));

            Assert.IsTrue(s.BuyAtStall(stall, cpu, 2));
            Assert.AreEqual(6, s.StallRemaining(stall, cpu));
            Assert.IsFalse(s.BuyAtStall(stall, cpu, 7));

            s.AddRelation(Npcs.Li, 60);
            Assert.AreEqual(28, s.StallQuota(stall), "交情高了额度变大");

            s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
            Assert.AreEqual(s.StallQuota(stall), s.StallRemaining(stall, cpu), "第二天额度重置");
        }

        [Test]
        public void Invite_NeedsEnoughRelation_AndMeeting()
        {
            GameSession s = Make();
            Assert.IsNotNull(s.InviteBlocker(Npcs.Li, OutingSlot.Evening), "没认识");
            s.Meet(Npcs.Li, 5);
            Assert.IsNotNull(s.InviteBlocker(Npcs.Li, OutingSlot.Evening), "交情不够");
            s.AddRelation(Npcs.Li, 30);
            Assert.IsNull(s.InviteBlocker(Npcs.Li, OutingSlot.Evening));
        }

        [Test]
        public void NextSlotDate_EveningAndWeekend()
        {
            GameSession s = Make(minute: 10 * 60);
            Assert.AreEqual(s.Clock.Today, s.NextSlotDate(OutingSlot.Evening));
            s.Clock.Restore(s.Clock.Today, 18 * 60);
            Assert.AreEqual(s.Clock.Today.AddDays(1), s.NextSlotDate(OutingSlot.Evening), "太晚了约明晚");
            Assert.IsTrue(s.NextSlotDate(OutingSlot.Weekend).IsWeekend);
        }

        private static bool ForceInvite(GameSession s, string id, OutingSlot slot)
        {
            for (int i = 0; i < 30; i++)
            {
                if (s.Invite(id, slot)) return true;
                s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
            }
            return false;
        }

        [Test]
        public void Appointment_RaisesAnEvent_AndOutingRaisesRelationAndCostsTime()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 90);
            Assert.IsTrue(ForceInvite(s, Npcs.Li, OutingSlot.Evening));
            Assert.AreEqual(1, s.Appointments.Count);
            Assert.IsNotNull(s.InviteBlocker(Npcs.Li, OutingSlot.Evening), "已经约了");

            int day = s.Appointments[0].Day;
            while (s.Clock.Today.TotalDays < day) s.AdvanceClock(60);
            while (s.PendingEvent == null && s.Clock.MinuteOfDay < 22 * 60) s.AdvanceClock(15);
            Assert.IsNotNull(s.PendingEvent);
            Assert.AreEqual("date_li", s.PendingEvent.Id);
            Assert.AreEqual(0, s.Appointments.Count);

            Money before = s.Ledger.Balance;
            int rel = s.Relation(Npcs.Li);
            int minuteBefore = s.Clock.MinuteOfDay;
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.Greater(s.Relation(Npcs.Li), rel);
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);
            Assert.IsNull(s.PendingEvent);
            Assert.AreNotEqual(minuteBefore, s.Clock.MinuteOfDay, "出去玩要花时间");
            Assert.IsNotNull(s.InviteBlocker(Npcs.Li, OutingSlot.Evening), "冷却中");
        }

        [Test]
        public void LikedActivity_GivesMoreRelation()
        {
            // 老李最爱钓鱼；同样的魅力下，钓鱼的收益比同价位其它周末活动高。
            ActivityDef fishing = Activities.Get(Activities.Fishing);
            Assert.AreEqual(Activities.Fishing, Npcs.Get(Npcs.Li).Likes);
            Assert.Greater(fishing.BaseGain * 1.5, Activities.Get(Activities.MarketWalk).BaseGain);
        }

        [Test]
        public void StandingThemUp_CostsRelation()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 90);
            Assert.IsTrue(ForceInvite(s, Npcs.Li, OutingSlot.Evening));
            while (s.PendingEvent == null && s.Clock.MinuteOfDay < 22 * 60) s.AdvanceClock(15);
            Assert.IsNotNull(s.PendingEvent);
            int rel = s.Relation(Npcs.Li);
            int last = s.PendingEvent.Choices.Count - 1;
            Assert.IsTrue(s.ResolveEvent(last), "放鸽子");
            Assert.AreEqual(rel - 8, s.Relation(Npcs.Li));
        }

        [Test]
        public void MissedAppointment_IsDropped_AndHurtsRelation()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 90);
            Assert.IsTrue(ForceInvite(s, Npcs.Li, OutingSlot.Evening));
            int day = s.Appointments[0].Day;
            s.Clock.Restore(new GameDate(day), 15 * 60);
            s.RaiseEvent(EventCatalog.All.First().Id); // 有别的事压着，没法赴约
            int rel = s.Relation(Npcs.Li);
            while (s.Appointments.Count > 0 && s.Clock.MinuteOfDay < 23 * 60 + 30) s.AdvanceClock(30);
            Assert.AreEqual(0, s.Appointments.Count);
            Assert.AreEqual(rel - 5, s.Relation(Npcs.Li));
        }

        [Test]
        public void AskForTip_NeedsRelation_CostsMoney_AndHasCooldown()
        {
            GameSession s = Make();
            Assert.IsNotNull(s.TipBlocker(Npcs.Li), "没认识");
            s.Meet(Npcs.Li, 5);
            Assert.IsNotNull(s.TipBlocker(Npcs.Li), "交情不够");
            s.AddRelation(Npcs.Li, 20);
            Assert.IsNotNull(s.TipBlocker(Npcs.Li), "没带烟");
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 1);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.AskForTip(Npcs.Li));
            Assert.AreEqual(0, s.Backpack.CigaretteCount, "递出去一包烟");
            Assert.AreEqual(before.Cents, s.Ledger.Balance.Cents);
            Assert.AreEqual(1, s.Tips.Count);
            Assert.IsFalse(s.AskForTip(Npcs.Li), "冷却中");
            Assert.IsNotNull(s.TipBlocker(Npcs.Zhang), "老张不爱聊这个");
        }

        [Test]
        public void Tip_IsResolvedAfterFiveDays_AndCounted()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 30);
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 1);
            s.AskForTip(Npcs.Li);
            Assert.AreEqual(TipOutcome.Pending, s.Tips[0].Outcome);
            for (int i = 0; i < GameSession.TipResolveDays + 1; i++)
            {
                s.Clock.Restore(s.Clock.Today, 23 * 60 + 30);
                s.AdvanceClock(60);
            }
            Assert.AreNotEqual(TipOutcome.Pending, s.Tips[0].Outcome);
            Assert.AreEqual(1, s.TipsRight(Npcs.Li) + s.TipsWrong(Npcs.Li));
        }

        [Test]
        public void TipTruthChance_RisesWithRelation_AndDiffersBetweenPeople()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 10);
            s.Meet(Npcs.Wang, 10);
            Assert.Greater(s.TipTruthChance(Npcs.Get(Npcs.Li)), s.TipTruthChance(Npcs.Get(Npcs.Wang)));
            double before = s.TipTruthChance(Npcs.Get(Npcs.Li));
            s.AddRelation(Npcs.Li, 50);
            Assert.Greater(s.TipTruthChance(Npcs.Get(Npcs.Li)), before);
            Assert.LessOrEqual(s.TipTruthChance(Npcs.Get(Npcs.Li)), 0.95);
        }

        [Test]
        public void MarketShock_MovesCategoryPrice_ThenFades()
        {
            GameSession s = Make();
            double start = s.Market.Index(PartCategory.Cpu);
            s.Market.ScheduleShock(s.Clock.Today.AddDays(1), PartCategory.Cpu, 0.15);
            s.Clock.Restore(s.Clock.Today.AddDays(1), 23 * 60 + 30);
            s.AdvanceClock(60);
            double peak = s.Market.Index(PartCategory.Cpu);
            Assert.AreEqual(0.15, s.Market.BiasOf(PartCategory.Cpu), 1e-9);
            for (int i = 0; i < 15; i++)
            {
                s.Clock.Restore(s.Clock.Today, 23 * 60 + 30);
                s.AdvanceClock(60);
            }
            Assert.Less(s.Market.BiasOf(PartCategory.Cpu), 0.05, "冲击会逐渐消退");
            Assert.AreNotEqual(start, peak);
        }

        [Test]
        public void SaveLoad_KeepsSocialState()
        {
            GameSession s = Make();
            GoToStall(s);
            Place stall = s.City.GetPlace(Stall);
            s.AddRelation(Npcs.Li, 80);
            s.BuyAtStall(stall, CpuPart(), 3);
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 1);
            s.AskForTip(Npcs.Li);
            s.Leave();
            Assert.IsTrue(ForceInvite(s, Npcs.Li, OutingSlot.Weekend));
            s.Market.ScheduleShock(s.Clock.Today.AddDays(2), PartCategory.Gpu, 0.12);

            SaveData d = s.CreateSave();
            string json = UnityEngine.JsonUtility.ToJson(d);
            GameSession l = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(json));

            Assert.AreEqual(s.Relation(Npcs.Li), l.Relation(Npcs.Li));
            Assert.AreEqual(1, l.Appointments.Count);
            Assert.AreEqual(s.Appointments[0].Day, l.Appointments[0].Day);
            Assert.AreEqual(OutingSlot.Weekend, l.Appointments[0].Slot);
            Assert.AreEqual(1, l.Tips.Count);
            Assert.AreEqual(s.Tips[0].Category, l.Tips[0].Category);
            Assert.AreEqual(s.StallBoughtToday(stall, CpuPart()), l.StallBoughtToday(l.City.GetPlace(Stall), CpuPart()));
            Assert.AreEqual(s.Market.PendingShocks.Count, l.Market.PendingShocks.Count);
            foreach (PartCategory c in PartCatalog.Categories)
                Assert.AreEqual(s.Market.Index(c), l.Market.Index(c), 1e-9);
        }

        [Test]
        public void OldSaves_WithoutSocialData_StillLoad()
        {
            GameSession s = Make();
            SaveData d = s.CreateSave();
            d.social = null;
            GameSession l = GameSession.Load(s.City, d);
            Assert.AreEqual(0, l.Appointments.Count);
            Assert.AreEqual(0, l.Tips.Count);
        }

        // ---------- 点人物后的交谈选项 ----------

        [Test]
        public void SmallTalk_OncePerDay_GainsRelation_AndTakesTenMinutes()
        {
            GameSession s = Make();
            Assert.IsNotNull(s.SmallTalkBlocker(Npcs.Li), "没认识不能聊");
            s.Meet(Npcs.Li, 20);
            Assert.IsNull(s.SmallTalkBlocker(Npcs.Li));
            double before = s.Relation(Npcs.Li);
            GameTimestamp t0 = s.Clock.Now;
            string line = s.SmallTalk(Npcs.Li);
            Assert.IsNotEmpty(line);
            Assert.AreEqual(10, (int)(s.Clock.Now.TotalMinutes - t0.TotalMinutes));
            Assert.AreEqual("今天聊过了", s.SmallTalkBlocker(Npcs.Li));
            Assert.IsNull(s.SmallTalk(Npcs.Li));
            s.AdvanceClock(24 * 60);
            Assert.IsNull(s.SmallTalkBlocker(Npcs.Li));
            Assert.GreaterOrEqual(s.Relation(Npcs.Li), before);
        }

        [Test]
        public void SmallTalk_StopsGainingAtTheCap()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, NpcTalk.SmallTalkMaxRelation);
            int rel = s.Relation(Npcs.Li);
            s.SmallTalk(Npcs.Li);
            Assert.AreEqual(rel, s.Relation(Npcs.Li));
        }

        [Test]
        public void LifeTalk_UnlocksChapterByChapter_WithRelation()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 20);
            StringAssert.Contains("30", s.LifeBlocker(Npcs.Li));
            Assert.IsNull(s.LifeTalk(Npcs.Li));

            s.AddRelation(Npcs.Li, 10);
            Assert.IsNull(s.LifeBlocker(Npcs.Li));
            string story = s.LifeTalk(Npcs.Li);
            Assert.AreEqual(NpcTalk.LifeStory(Npcs.Li, 0), story);
            Assert.AreEqual(1, s.LifeChaptersHeard(Npcs.Li));
            // 第二天交情不到 50，不肯讲下一段
            s.AdvanceClock(24 * 60);
            StringAssert.Contains("50", s.LifeBlocker(Npcs.Li));
            s.AddRelation(Npcs.Li, 30);
            Assert.AreEqual(NpcTalk.LifeStory(Npcs.Li, 1), s.LifeTalk(Npcs.Li));
            s.AdvanceClock(24 * 60);
            s.AddRelation(Npcs.Li, 30);
            Assert.AreEqual(NpcTalk.LifeStory(Npcs.Li, 2), s.LifeTalk(Npcs.Li));
            s.AdvanceClock(24 * 60);
            s.AddRelation(Npcs.Li, 30);
            Assert.AreEqual("他的故事你都听过了", s.LifeBlocker(Npcs.Li));
        }

        [Test]
        public void EveryNpc_HasSmallTalkAndThreeLifeChapters()
        {
            foreach (NpcDef n in Npcs.All)
            {
                for (int d = 0; d < 4; d++) Assert.IsNotEmpty(NpcTalk.SmallTalkLine(n.Id, d), n.Id);
                for (int c = 0; c < NpcTalk.LifeChapterTotal; c++) Assert.IsNotNull(NpcTalk.LifeStory(n.Id, c), n.Id + c);
            }
        }

        [Test]
        public void Insider_NeedsHighRelation_NoCigarette_AndIsAlwaysTrue()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 40);
            StringAssert.Contains("60", s.InsiderBlocker(Npcs.Li));
            Assert.IsNull(s.AskInsider(Npcs.Li));

            s.AddRelation(Npcs.Li, 30);
            Assert.IsNull(s.InsiderBlocker(Npcs.Li), "不用带烟");
            int shocksBefore = s.Market.PendingShocks.Count;
            string text = s.AskInsider(Npcs.Li);
            Assert.IsNotEmpty(text);
            Assert.AreEqual(shocksBefore + 1, s.Market.PendingShocks.Count, "说的是真话：行情会真的动");
            Assert.AreEqual(1, s.Tips.Count);
            StringAssert.Contains("天后再问", s.InsiderBlocker(Npcs.Li));

            s.Meet(Npcs.Zhang, 90);
            Assert.AreEqual("他不爱聊这个", s.InsiderBlocker(Npcs.Zhang));
        }

        [Test]
        public void TalkProgress_SurvivesSaveAndLoad()
        {
            GameSession s = Make();
            s.Meet(Npcs.Li, 60);
            s.SmallTalk(Npcs.Li);
            s.LifeTalk(Npcs.Li);
            s.AskInsider(Npcs.Li);
            GameSession l = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual("今天聊过了", l.SmallTalkBlocker(Npcs.Li));
            Assert.AreEqual(1, l.LifeChaptersHeard(Npcs.Li));
            Assert.IsNotNull(l.InsiderBlocker(Npcs.Li));
        }

        // ---------- 店里的人：店员接待，老板交情够了才见得到 ----------

        [Test]
        public void StallRoster_ClerkGreets_BossNeedsRelation()
        {
            GameSession s = Make();
            PlaceRoster r = Rosters.For(s.City.GetPlace(Stall));
            Assert.IsNotNull(r);
            Assert.AreEqual("staff_cpu", r.Greeter.Id);
            Assert.AreEqual(Npcs.Li, r.BossNpcId);
            Assert.AreEqual(Rosters.StallBossGate, r.BossGate);

            Assert.IsFalse(s.BossReachable(r), "还没认识老板");
            Assert.IsNotNull(s.StaffBossBlocker(r));

            s.Meet(Npcs.Li, 10);
            Assert.IsFalse(s.BossReachable(r), "交情不够，老板不出来");
            Assert.IsNull(s.StaffBossBlocker(r));

            int before = s.Relation(Npcs.Li);
            StringAssert.Contains("老李", s.AskStaffAboutBoss(r));
            Assert.AreEqual("今天问过了", s.StaffBossBlocker(r));
            Assert.IsNull(s.AskStaffAboutBoss(r));

            for (int i = 0; i < 6 && !s.BossReachable(r); i++)
            {
                s.AdvanceClock(24 * 60);
                s.AskStaffAboutBoss(r);
            }
            Assert.IsTrue(s.BossReachable(r), "跟店员聊几天，老板肯见了");
            Assert.Greater(s.Relation(Npcs.Li), before);
            StringAssert.Contains("直接找他", s.StaffBossBlocker(r));
        }

        [Test]
        public void Roster_NullForNoPlace_AndEveryStallHasClerk()
        {
            Assert.IsNull(Rosters.For(null));
            foreach (string id in new[] { Stalls.Cpu, Stalls.Gpu, Stalls.Memory, Stalls.PowerCase })
            {
                Assert.IsNotNull(Npcs.OwnerOf(id), id);
            }
            Assert.IsNotEmpty(Rosters.CpuClerk.LineFor(5));
            Assert.IsNotEmpty(Rosters.GpuClerk.LineFor(-1));
        }

        [Test]
        public void StaffTalkProgress_SurvivesSaveAndLoad()
        {
            GameSession s = Make();
            PlaceRoster r = Rosters.For(s.City.GetPlace(Stall));
            s.Meet(Npcs.Li, 10);
            s.AskStaffAboutBoss(r);
            GameSession l = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual("今天问过了", l.StaffBossBlocker(r));
        }

        // ---------- 店员的熟络度 ----------

        [Test]
        public void StaffChat_OncePerDay_RaisesRapportUpToCap()
        {
            GameSession s = Make();
            StaffDef c = Rosters.CpuClerk;
            Assert.AreEqual(0, s.StaffRapport(c.Id));
            StringAssert.Contains("小慧", s.StaffChat(c));
            Assert.AreEqual(1, s.StaffRapport(c.Id));
            Assert.AreEqual("今天聊过了", s.StaffChatBlocker(c.Id));
            Assert.IsNull(s.StaffChat(c));
            for (int i = 0; i < 40; i++) { s.AdvanceClock(24 * 60); s.StaffChat(c); }
            Assert.LessOrEqual(s.StaffRapport(c.Id), StaffTalk.ChatMaxRapport + 1, "光闲聊涨到 40 就停");
        }

        [Test]
        public void StaffGift_CostsMoney_AndHasCooldown()
        {
            GameSession s = Make(cash: 1000);
            StaffDef c = Rosters.CpuClerk;
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.GiveStaffGift(c));
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);
            Assert.GreaterOrEqual(s.StaffRapport(c.Id), 4);
            Assert.IsNotNull(s.StaffGiftBlocker(c.Id));
            Assert.IsFalse(s.GiveStaffGift(c));
            s.AdvanceClock(StaffTalk.GiftCooldownDays * 24 * 60);
            Assert.IsNull(s.StaffGiftBlocker(c.Id));
        }

        [Test]
        public void StaffStories_UnlockWithRapport_AndEveryStaffHasThree()
        {
            foreach (StaffDef st in Rosters.All)
            {
                Assert.AreEqual(3, Rosters.StoriesOf(st.Id)?.Length, st.Id);
                Assert.AreSame(st, Rosters.Get(st.Id));
            }
            GameSession s = Make(cash: 100000);
            StaffDef c = Rosters.GpuClerk;
            StringAssert.Contains("解锁", s.StaffStoryBlocker(c.Id));
            for (int i = 0; i < 12; i++) { s.AdvanceClock(24 * 60); s.StaffChat(c); s.GiveStaffGift(c); }
            Assert.GreaterOrEqual(s.StaffRapport(c.Id), StaffTalk.StoryUnlock[0]);
            Assert.IsNull(s.StaffStoryBlocker(c.Id));
            Assert.IsNotEmpty(s.StaffStory(c));
            Assert.AreEqual(1, s.StaffStoriesHeard(c.Id));
            Assert.AreEqual("今天聊过了", s.StaffStoryBlocker(c.Id));
        }

        [Test]
        public void StaffRapport_SurvivesSave_AndMakesBossTalkWorthMore()
        {
            GameSession s = Make(cash: 100000);
            PlaceRoster r = Rosters.For(s.City.GetPlace(Stall));
            s.Meet(Npcs.Li, 10);
            for (int i = 0; i < 25; i++) { s.AdvanceClock(24 * 60); s.StaffChat(r.Greeter); s.GiveStaffGift(r.Greeter); }
            int rapport = s.StaffRapport(r.Greeter.Id);
            Assert.GreaterOrEqual(rapport, 30);
            GameSession l = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(rapport, l.StaffRapport(r.Greeter.Id));
            Assert.AreEqual("今天聊过了", l.StaffChatBlocker(r.Greeter.Id));

            int before = s.Relation(Npcs.Li);
            s.AdvanceClock(24 * 60);
            s.AskStaffAboutBoss(r);
            Assert.GreaterOrEqual(s.Relation(Npcs.Li) - before, 1, "熟络之后替你说话更有分量");
        }

        [Test]
        public void Roster_ExtraStaffShareTheTier()
        {
            Assert.AreEqual(1, Rosters.For(MakePlace(Stalls.Gpu)).Others.Length);
            Assert.AreEqual(0, Rosters.For(MakePlace(Stalls.Cpu)).Others.Length);
        }

        private static Place MakePlace(string id) => new Place(id, id, PlaceKind.Wholesaler, "mk");
    }
}
