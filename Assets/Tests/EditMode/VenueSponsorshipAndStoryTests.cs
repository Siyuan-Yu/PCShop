using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>场所赞助（预订、现货扣减、取消退款、读档）与人物关系故事（阶段推进、一次性回应、读档）。</summary>
    public sealed class VenueSponsorshipAndStoryTests
    {
        private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Venue = "life_restaurant";

        // ───────── 场所赞助 ─────────

        private static GameSession MakeShop()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 921, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 660), false, true, true);
            s.EventsEnabled = false; s.Tick(0);
            s.MainShop.Sim.NotFitted = false; s.MainShop.Sim.ForcedClosed = false; s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
            s.MainShop.Sim.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 20);
            return s;
        }

        private static void At(GameSession s, string id)
        {
            s.StopTrip();
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(id));
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(id).NodeId);
        }

        private static GameSession Reload(GameSession s) =>
            GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        /// <summary>把玩家放到钓鱼场，找一个周末且当班员工在岗的时刻，并把该员工摆到岗位上、熟悉度拉到可办理。</summary>
        private static Place PrepareFishingVenue(GameSession s)
        {
            var venue = s.City.GetPlace(GameSession.FishingVenueId);
            var authored = typeof(GameSession).GetMethod("AuthoredCityWorkAt", NonPublic);
            var worker = s.CityPeople.FirstOrDefault(p => p.WorkPlaceId == venue.Id);
            Assert.NotNull(worker, "夹具前提：钓鱼场有固定员工");
            for (int day = 42; day < 60; day++)
            {
                if (!new GameDate(day).IsWeekend) continue;
                for (int minute = 8 * 60; minute < 20 * 60; minute += 15)
                {
                    long at = day * 1440L + minute;
                    if (!(bool)authored.Invoke(s, new object[] { worker, at })) continue;
                    s.Clock.Restore(new GameDate(day), minute);
                    s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
                    At(s, venue.Id);
                    worker.Task = CityPersonTask.Working; worker.LocationPlaceId = venue.Id; worker.Route = null; worker.ActivityId = null; worker.VisitId = null;
                    s.Meet(worker.PersonId, GameSession.VenueSponsorFamiliarity);
                    return venue;
                }
            }
            Assert.Inconclusive("没有找到员工在岗的周末时刻，夹具需要调整");
            return null;
        }

        /// <summary>集合时间过了仍没有足够街坊到场：活动取消、退款、现货退回门店。</summary>
        private static void TickSponsorships(GameSession s, long now) =>
            typeof(GameSession).GetMethod("TickVenueSponsorships", NonPublic).Invoke(s, new object[] { now });

        private static int RamCount(GameSession s) =>
            s.MainShop.Sim.Stock.Lines().Where(l => l.Part.Id == "ram0" && l.Boxed).Sum(l => l.Count);

        [Test]
        public void Booking_DebitsFeeAndTakesStock_ThenCancelRefundsAndReturnsStock_AcrossReload()
        {
            using var s = MakeShop();
            var venue = PrepareFishingVenue(s);
            var plan = s.PlanVenueSponsorship(venue, true);
            Assert.GreaterOrEqual(plan.Starts, 0, "夹具前提：能排出赞助时段");
            var product = s.VenueSponsorProducts(s.MainShop).Single(p => p.Key == "b:ram0");
            var salePrice = Money.FromCents(product.Price.Cents / 2);
            int stockBefore = RamCount(s); var cash = s.Ledger.Balance; int fee = plan.MinimumFee;

            Assert.IsNull(s.BookVenueSponsorship(venue, s.MainShop.Place.Id, fee, product.Key, 5, salePrice));
            var record = s.VenueSponsorships.Single();
            Assert.AreEqual(cash - Money.FromYuan(fee), s.Ledger.Balance, "赞助费应从账上扣除");
            Assert.AreEqual(stockBefore - 5, RamCount(s), "特卖品应从门店现货中扣出");
            Assert.AreEqual(fee * 100L, record.FeeCents);

            using var loaded = Reload(s);
            var restored = loaded.VenueSponsorships.Single();
            Assert.AreEqual(record.Id, restored.Id);
            Assert.AreEqual(record.Starts, restored.Starts);
            CollectionAssert.AreEqual(record.Guests, restored.Guests);
            Assert.AreEqual(5, restored.Product.Count);
            Assert.AreEqual(salePrice.Cents, restored.SaleCents);
            Assert.AreEqual("已安排", restored.Status);

            TickSponsorships(loaded, restored.Starts + 20);
            Assert.IsTrue(loaded.VenueSponsorships.Single().Cancelled);
            Assert.AreEqual(cash, loaded.Ledger.Balance, "取消应全额退回赞助费");
            Assert.AreEqual(stockBefore, loaded.MainShop.Sim.Stock.Lines().Where(l => l.Part.Id == "ram0" && l.Boxed).Sum(l => l.Count), "取消应退回现货");

            TickSponsorships(loaded, restored.Starts + 20);
            Assert.AreEqual(cash, loaded.Ledger.Balance, "已取消的记录不能重复退款");
        }

        [Test]
        public void Booking_RejectsBadFeeOrPrice_WithoutTouchingCashOrStock()
        {
            using var s = MakeShop();
            var venue = PrepareFishingVenue(s);
            var plan = s.PlanVenueSponsorship(venue, true);
            var product = s.VenueSponsorProducts(s.MainShop).Single(p => p.Key == "b:ram0");
            int stockBefore = RamCount(s); var cash = s.Ledger.Balance;

            Assert.IsNotNull(s.BookVenueSponsorship(venue, s.MainShop.Place.Id, plan.MinimumFee - 1, null, 0, Money.Zero), "低于下限");
            Assert.IsNotNull(s.BookVenueSponsorship(venue, s.MainShop.Place.Id, plan.MaximumFee + 1, null, 0, Money.Zero), "高于上限");
            Assert.IsNotNull(s.BookVenueSponsorship(venue, s.MainShop.Place.Id, plan.MinimumFee, product.Key, 5, product.Price), "活动价不低于原价");
            Assert.IsNotNull(s.BookVenueSponsorship(venue, s.MainShop.Place.Id, plan.MinimumFee, product.Key, 999, Money.FromCents(1)), "数量超过现货");
            Assert.IsEmpty(s.VenueSponsorships);
            Assert.AreEqual(cash, s.Ledger.Balance);
            Assert.AreEqual(stockBefore, RamCount(s));
        }

        [Test]
        public void Booking_WithoutEnoughCash_ReturnsStockAndCreatesNoRecord()
        {
            using var s = MakeShop();
            var venue = PrepareFishingVenue(s);
            var plan = s.PlanVenueSponsorship(venue, true);
            var product = s.VenueSponsorProducts(s.MainShop).Single(p => p.Key == "b:ram0");
            s.Ledger.ForceDebit(s.Clock.Today, "test", s.Ledger.Balance - Money.FromYuan(plan.MinimumFee - 1), "fixture");
            int stockBefore = RamCount(s); var cash = s.Ledger.Balance;

            Assert.AreEqual("可用现金不足。", s.BookVenueSponsorship(venue, s.MainShop.Place.Id, plan.MinimumFee, product.Key, 5, Money.FromCents(product.Price.Cents / 2)));
            Assert.IsEmpty(s.VenueSponsorships);
            Assert.AreEqual(cash, s.Ledger.Balance);
            Assert.AreEqual(stockBefore, RamCount(s));
        }

        // ───────── 人物关系故事 ─────────

        private const string Ming = "life_ming";

        private static void MeetHere(GameSession session, string id)
        {
            var person = session.CityPersonState(id);
            person.Route = null;
            person.LocationPlaceId = Venue;
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(session, session.City.GetPlace(Venue));
        }

        private static void NextDay(GameSession s) => s.Clock.Restore(new GameDate(s.Clock.Today.TotalDays + 1), 600);

        private static GameSession ReloadStory(GameSession source) =>
            GameSession.Load(source.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source.CreateSave())));

        private static CityActivity AddOuting(GameSession s, string personId, CityActivityPhase phase)
        {
            var activity = new CityActivity
            {
                Id = "story-outing", DefinitionId = "oldmarket_visit", VenueId = Venue, State = phase, RewardApplied = phase == CityActivityPhase.Completed,
                Participants = new[] { new CityActivityParticipant { PersonId = personId, Phase = CityParticipantPhase.Participating } }
            };
            ((List<CityActivity>)typeof(GameSession).GetField("_cityActivities", NonPublic).GetValue(s)).Add(activity);
            var progress = ((Dictionary<string, RelationshipStoryProgress>)typeof(GameSession).GetField("_relationshipStories", NonPublic).GetValue(s))[personId];
            progress.AppointmentId = activity.Id;
            return activity;
        }

        private static void Settle(GameSession s, CityActivity activity, CityActivityPhase phase) =>
            typeof(GameSession).GetMethod("SettleRelationshipStoryOuting", NonPublic).Invoke(s, new object[] { activity, phase });

        private static GameSession ReachDecide()
        {
            var s = MakeShop();
            MeetHere(s, Ming);
            s.Meet(Ming, 40);
            Assert.IsNotEmpty(s.CasualConversation(Ming));
            Assert.IsTrue(s.HasStartedRelationshipStory(Ming));
            Assert.AreEqual(RelationshipStoryStep.None, s.GetRelationshipStoryStep(Ming), "首次交谈当天不能立刻邀约");
            NextDay(s); MeetHere(s, Ming);
            Assert.AreEqual(RelationshipStoryStep.Invite, s.GetRelationshipStoryStep(Ming));
            var outing = AddOuting(s, Ming, CityActivityPhase.Completed);
            Settle(s, outing, CityActivityPhase.Completed);
            Assert.AreEqual(RelationshipStoryStep.None, s.GetRelationshipStoryStep(Ming), "赴约当天不能立刻表态");
            NextDay(s); MeetHere(s, Ming);
            Assert.AreEqual(RelationshipStoryStep.Decide, s.GetRelationshipStoryStep(Ming));
            return s;
        }

        [Test]
        public void Story_ProgressesInOrder_AndSurvivesReloadAtEachStage()
        {
            using var s = MakeShop();
            MeetHere(s, Ming); s.Meet(Ming, 40);
            s.CasualConversation(Ming);
            using (var atFirst = ReloadStory(s))
            {
                Assert.IsTrue(atFirst.HasStartedRelationshipStory(Ming));
                NextDay(atFirst); MeetHere(atFirst, Ming);
                Assert.AreEqual(RelationshipStoryStep.Invite, atFirst.GetRelationshipStoryStep(Ming));
            }
            using var decide = ReachDecide();
            using var loaded = ReloadStory(decide);
            MeetHere(loaded, Ming);
            Assert.AreEqual(RelationshipStoryStep.Decide, loaded.GetRelationshipStoryStep(Ming));
            Assert.IsNull(loaded.ConfessionBlocker(Ming), "走完赴约后才能当面表态");
        }

        [Test]
        public void Story_BeforeOuting_CannotBeConfessedOrResolved()
        {
            using var s = MakeShop();
            MeetHere(s, Ming); s.Meet(Ming, 100);
            s.CasualConversation(Ming);
            StringAssert.Contains("赴约", s.ConfessionBlocker(Ming), "熟悉度满也不能绕过赴约");
            StringAssert.Contains("当面", s.ResolveRelationshipStory(Ming, romantic: true));
            Assert.IsNull(s.RomanceId);
        }

        [Test]
        public void Story_BecomingPartners_PersistsAndSeparationIsRecorded()
        {
            using var s = ReachDecide();
            Assert.IsNotEmpty(s.RelationshipStoryDecisionLine(Ming));
            Assert.IsNotEmpty(s.ResolveRelationshipStory(Ming, romantic: true));
            Assert.AreEqual(Ming, s.RomanceId);
            StringAssert.Contains("当面", s.ResolveRelationshipStory(Ming, romantic: true), "结果只能定一次");

            using var loaded = ReloadStory(s);
            Assert.AreEqual(Ming, loaded.RomanceId);
            Assert.AreEqual(RelationshipStoryOutcome.Together, StoryOf(loaded).Outcome);
            Assert.IsTrue(loaded.EndRomance());
            Assert.AreEqual(RelationshipStoryOutcome.Separated, StoryOf(loaded).Outcome);

            using var ended = ReloadStory(loaded);
            Assert.IsNull(ended.RomanceId);
            Assert.AreEqual(RelationshipStoryOutcome.Separated, StoryOf(ended).Outcome);
            MeetHere(ended, Ming);
            Assert.AreEqual(RelationshipStoryStep.None, ended.GetRelationshipStoryStep(Ming), "分手后故事不再重复进入表态阶段");
        }

        [Test]
        public void Story_StayingFriends_FollowUpIsReadOnlyOnce()
        {
            using var s = ReachDecide();
            Assert.IsNotEmpty(s.ResolveRelationshipStory(Ming, romantic: false));
            Assert.IsNull(s.RomanceId);
            Assert.AreEqual(RelationshipStoryOutcome.Friends, StoryOf(s).Outcome);

            using var loaded = ReloadStory(s);
            NextDay(loaded); MeetHere(loaded, Ming);
            string first = loaded.CasualConversation(Ming);
            Assert.IsNotEmpty(first);
            Assert.IsTrue(StoryOf(loaded).FollowUpRead, "第二天首次聊天会读到后续对话");
            using var again = ReloadStory(loaded);
            Assert.IsTrue(StoryOf(again).FollowUpRead, "已读状态应随存档保存");
        }

        [Test]
        public void Story_CannotStartRomanceWhileAlreadyHavingAnotherPartner()
        {
            using var s = ReachDecide();
            typeof(GameSession).GetMethod("ConfirmPartner", NonPublic).Invoke(s, new object[] { "resident_baiming" });
            Assert.AreEqual("先处理好现有的伴侣关系。", s.ResolveRelationshipStory(Ming, romantic: true));
            Assert.AreEqual("resident_baiming", s.RomanceId);
            Assert.AreEqual(RelationshipStoryOutcome.Undecided, StoryOf(s).Outcome);
        }

        [Test]
        public void Story_OutingAnswerCanBeGivenOnlyOnce_AndIsRecalledAtDecision()
        {
            using var s = MakeShop();
            MeetHere(s, Ming); s.Meet(Ming, 40); s.CasualConversation(Ming);
            var outing = AddOuting(s, Ming, CityActivityPhase.Running);
            Assert.IsNotNull(s.RelationshipStoryOutingDialogue(outing, Ming));
            Assert.IsNull(s.ReplyInRelationshipOuting(outing, Ming, 99), "越界选项不应生效");
            Assert.IsNotEmpty(s.ReplyInRelationshipOuting(outing, Ming, 0));
            Assert.IsNull(s.ReplyInRelationshipOuting(outing, Ming, 1), "同一次赴约只能回答一次");
            Assert.IsNull(s.RelationshipStoryOutingDialogue(outing, Ming));
            Assert.AreEqual(1, StoryOf(s).OutingAnswer);
        }

        [Test]
        public void Story_MissedAppointment_IsMentionedOnceAndDoesNotCountAsOuting()
        {
            using var s = MakeShop();
            MeetHere(s, Ming); s.Meet(Ming, 40); s.CasualConversation(Ming);
            NextDay(s); MeetHere(s, Ming);
            var outing = AddOuting(s, Ming, CityActivityPhase.NoShow);
            Settle(s, outing, CityActivityPhase.NoShow);
            Assert.IsTrue(StoryOf(s).MissedAppointment);
            Assert.IsFalse(StoryOf(s).OutingCompleted);
            Assert.AreEqual(RelationshipStoryStep.Invite, s.GetRelationshipStoryStep(Ming), "没赴约还可以重新约");
            StringAssert.Contains("没碰上", s.CasualConversation(Ming));
            Assert.IsFalse(StoryOf(s).MissedAppointment, "提示只出现一次");
        }

        private static RelationshipStoryProgress StoryOf(GameSession s) =>
            ((Dictionary<string, RelationshipStoryProgress>)typeof(GameSession).GetField("_relationshipStories", NonPublic).GetValue(s))[Ming];
    }
}
