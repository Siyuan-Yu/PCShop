using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class RomanceConversationTests
    {
        private const string Partner = "resident_jiangning";
        private const string Lu = "life_creator";
        private const string Venue = "life_restaurant";

        [Test]
        public void AuthoredCandidatesAndFamiliarityCurveMatchTheApprovedRules()
        {
            Assert.AreEqual(30, CityLifeCatalog.People.Count(p => GameSession.IsRomanceCandidate(p.Id)));
            foreach (string id in new[] { "res_tea", "res_mom", "res_pupil", "story_shenxiao", "resident_yeli" })
                Assert.IsFalse(GameSession.IsRomanceCandidate(id), id);
            Assert.AreEqual(0, GameSession.ConfessionChance(70));
            Assert.That(GameSession.ConfessionChance(71), Is.EqualTo(.30).Within(.0001));
            Assert.That(GameSession.ConfessionChance(80), Is.EqualTo(.50).Within(.0001));
            Assert.That(GameSession.ConfessionChance(90), Is.EqualTo(.75).Within(.0001));
            Assert.That(GameSession.ConfessionChance(100), Is.EqualTo(.95).Within(.0001));
            for (int familiarity = 72; familiarity <= 100; familiarity++)
                Assert.Greater(GameSession.ConfessionChance(familiarity), GameSession.ConfessionChance(familiarity - 1));
        }

        [Test]
        public void RefusalKeepsTheFriendshipAndCooldownSurvivesReload()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                MeetHere(s, Partner);
                s.Meet(Partner, 71);
                SetRoll(s, Partner, false);
                StringAssert.Contains("照常相处", s.ConfessToPerson(Partner));
                Assert.IsNull(s.RomanceId);
                Assert.AreEqual(71, s.Relation(Partner));
                Assert.IsTrue(s.IsMet(Partner));
                Assert.IsNotEmpty(s.CasualConversation(Partner));
                int retryDay = s.Clock.Today.TotalDays + GameSession.ConfessionCooldownDays;
                using (var loaded = Reload(s, s.CreateSave()))
                {
                    MeetHere(loaded, Partner);
                    Assert.AreEqual(retryDay, loaded.CityPersonState(Partner).ConfessionRetryDay);
                    ulong rng = loaded.Random.State;
                    StringAssert.Contains("7 天", loaded.ConfessToPerson(Partner));
                    Assert.AreEqual(rng, loaded.Random.State);
                    loaded.Clock.Restore(new GameDate(retryDay), 600);
                    Assert.IsNull(loaded.ConfessionBlocker(Partner));
                    SetRoll(loaded, Partner, true);
                    StringAssert.Contains("交往", loaded.ConfessToPerson(Partner));
                    using (var savedPartner = Reload(loaded, loaded.CreateSave()))
                    {
                        Assert.AreEqual(Partner, savedPartner.RomanceId);
                        Assert.AreEqual("你的伴侣", savedPartner.PersonRelationshipText(Partner));
                        Assert.IsTrue(savedPartner.EndRomance());
                        Assert.IsNull(savedPartner.PartnerConversation(Partner));
                        Assert.AreEqual("单身", savedPartner.PersonRelationshipText(Partner));
                    }
                }
            }
        }

        [Test]
        public void TheSameActivityExposesPartnerInteractionsOnlyWhenTogether()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                MeetHere(s, Partner);
                s.Meet(Partner, 100);
                var activity = new CityActivity
                {
                    Id = "partner-activity", DefinitionId = "dinner", VenueId = Venue,
                    State = CityActivityPhase.Running,
                    Participants = new[] { new CityActivityParticipant { PersonId = Partner, Phase = CityParticipantPhase.Participating } }
                };
                var field = typeof(GameSession).GetField("_cityActivities", BindingFlags.Instance | BindingFlags.NonPublic);
                ((List<CityActivity>)field.GetValue(s)).Add(activity);
                Assert.IsNull(s.PartnerActivityLine(activity));
                Assert.IsNull(s.InteractWithActivityPartner(activity.Id, true));
                SetRoll(s, Partner, true);
                s.ConfessToPerson(Partner);
                StringAssert.Contains("吃完", s.PartnerActivityLine(activity));
                var cash = s.Ledger.Balance;
                Assert.IsNotEmpty(s.InteractWithActivityPartner(activity.Id, true));
                Assert.IsNotEmpty(s.InteractWithActivityPartner(activity.Id, true));
                Assert.IsNotEmpty(s.InteractWithActivityPartner(activity.Id, false));
                Assert.AreEqual(2, activity.Choices.Length, "Repeated choices do not write additional activity records");
                CollectionAssert.AreEqual(new[] { "牵手", "聊聊心事" },
                    activity.Choices.Select(key => GameSession.CityActivityChoiceText(s.CityActivityService(activity), key)).ToArray());
                Assert.AreEqual(cash, s.Ledger.Balance);
                activity.Participants[0].Phase = CityParticipantPhase.Left;
                Assert.IsNull(s.PartnerActivityLine(activity));
            }
        }

        [Test]
        public void LuJoinsTheSharedExpoMeetingWithoutForcingAContactOrRomance()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                var field = typeof(GameSession).GetField("_district", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(s, new DistrictProgress { Chapter = 2, NodeId = "expo.review", UnlockedTier = 1 });
                Assert.IsTrue(s.BeginDistrictMeeting("expo.review"));
                var appointment = s.DistrictStoryState;
                CollectionAssert.Contains(appointment.MeetingPeople, Lu);
                Assert.IsFalse(s.IsMet(Lu));
                s.Clock.Restore(new GameDate((int)(appointment.MeetingAt / 1440)), (int)(appointment.MeetingAt % 1440));
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(appointment.MeetingPlaceId));
                foreach (string id in appointment.MeetingPeople)
                {
                    var person = s.CityPersonState(id);
                    person.LocationPlaceId = appointment.MeetingPlaceId;
                    person.Route = null;
                }
                typeof(GameSession).GetMethod("CompleteDistrictNode", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(s, null);
                Assert.AreEqual(2, s.DistrictAccessTier);
                Assert.AreEqual(1, s.CityStory("romance").Stage);
                Assert.AreEqual(20, s.Relation(Lu));
                Assert.IsFalse(s.IsMet(Lu));
                Assert.IsNull(s.RomanceId);
            }
        }

        [Test]
        public void LuStoryIncreasesFamiliarityAndOlderConfirmedRelationshipCanBeRestored()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                MeetHere(s, Lu);
                Assert.IsNotEmpty(s.CasualConversation(Lu));
                Assert.AreEqual(20, s.Relation(Lu));
                s.HearCityPersonalStory(Lu);
                var story = s.CityStory("romance");
                Assert.AreEqual(2, story.Stage);
                Assert.AreEqual(45, s.Relation(Lu));
                story.ActivityId = "lu-outing";
                var completed = new CityActivity
                {
                    Id = story.ActivityId, State = CityActivityPhase.Completed, DefinitionId = "fishing", RewardApplied = true
                };
                typeof(GameSession).GetMethod("OnCityActivityCompleted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(s, new object[] { completed });
                Assert.AreEqual(4, story.Stage);
                Assert.AreEqual(75, s.Relation(Lu));
                Assert.IsNull(s.RomanceId);
                Assert.IsNull(s.ConfessionBlocker(Lu));

                var old = s.CreateSave();
                old.cityRomanceStateRecorded = false;
                old.cityStories.Single(item => item.Id == "romance").Branch = "romance";
                using (var loaded = Reload(s, old))
                {
                    Assert.AreEqual(Lu, loaded.RomanceId);
                    Assert.IsTrue(loaded.EndRomance());
                    using (var ended = Reload(loaded, loaded.CreateSave()))
                        Assert.IsNull(ended.RomanceId);
                }
            }
        }

        private static void MeetHere(GameSession session, string id)
        {
            var person = session.CityPersonState(id);
            person.Route = null;
            person.LocationPlaceId = Venue;
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(session, session.City.GetPlace(Venue));
        }

        private static void SetRoll(GameSession session, string id, bool accepted)
        {
            double chance = GameSession.ConfessionChance(session.Relation(id));
            session.Random.State = Enumerable.Range(1, 1000).Select(seed => (ulong)seed)
                .First(seed => new GameRandom(seed).Chance(chance) == accepted);
        }

        private static GameSession Reload(GameSession source, SaveData save) =>
            GameSession.Load(source.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)));
    }
}
