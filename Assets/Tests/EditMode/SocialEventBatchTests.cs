using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class SocialEventBatchTests
    {
        private static GameSession Make()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var first = new GameSession(fixture.City, 17, Money.FromYuan(10000000), GameTimestamp.From(new GameDate(42), 600), true, false, false);
            first.Tick(0);
            var data = first.CreateSave(); data.districtProgress = new DistrictProgress { UnlockedTier = 3 };
            return GameSession.Load(first.City, data);
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void At(GameSession s, string place)
        {
            s.StopTrip();
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(place));
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(place).NodeId);
        }
        private static void Attend(GameSession s, CityActivity activity)
        {
            s.Clock.Restore(new GameDate((int)(activity.ScheduledStart / 1440)), (int)(activity.ScheduledStart % 1440));
            At(s, activity.VenueId); s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100;
            Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace));
            s.AdvanceClock(1);
            Assert.AreEqual(CityActivityPhase.Running, activity.State, activity.EndReason);
        }
        private static CityActivity Accept(GameSession s, string eventId, int choice = 0)
        {
            s.RaiseEvent(eventId); Assert.NotNull(s.PendingEvent, eventId);
            string body = s.PendingEvent.Body;
            Assert.IsTrue(s.ResolveEvent(choice), body);
            return s.CityActivities.Last(a => a.Active);
        }

        [Test]
        public void EveryOrdinaryActivityGetsTwiceTheConsumptionChoicesWithHigherTiers()
        {
            // The independent market visit is free companionship; purchases happen at real stalls.
            // It was added after the paid venue expansion and has no bundled premium consumption.
            foreach (var basic in CityLifeCatalog.Activities.Where(d => !d.EventOnly && d.LogicalId == null && d.Id != "oldmarket_visit"))
            {
                var menu = CityLifeCatalog.Activities.Where(d => !d.EventOnly && (d.Id == basic.Id || d.LogicalId == basic.Id)).ToArray();
                Assert.AreEqual(basic.ServiceCount * 2, menu.Sum(d => d.ServiceCount), basic.Id);
                Assert.IsTrue(menu.Any(d => d.Minutes > basic.Minutes), basic.Id);
                Assert.IsTrue(menu.Any(d => d.PriceYuan > basic.PriceYuan), basic.Id);
            }
            Assert.AreEqual(4, CityLifeCatalog.Activities.Where(d => !d.EventOnly && d.VenueId == "life_ktv").Sum(d => d.ServiceCount));
            Assert.AreEqual(4, CityLifeCatalog.Activities.Where(d => !d.EventOnly && d.VenueId == "life_bar").Sum(d => d.ServiceCount));
            Assert.AreEqual(8, CityLifeCatalog.Activities.Where(d => !d.EventOnly && d.VenueId == "life_restaurant").Sum(d => d.ServiceCount));
            Assert.AreEqual(0, CityLifeCatalog.Activity("oldmarket_visit").PriceYuan);
            Assert.AreEqual(100, CityLifeCatalog.Activity("dinner").ServicePriceYuan(0));
            Assert.AreEqual("聊近况", CityLifeCatalog.Activity("dinner").Choices[2], "Do not reinterpret saved social choices");
        }

        [Test]
        public void PremiumKtvUsesItsRealPriceDurationAndSavedServiceWithoutChangingActivityKind()
        {
            using var s = Make(); s.Clock.Restore(new GameDate(42), 900); At(s, "life_ktv"); var balance = s.Ledger.Balance;
            Assert.IsTrue(s.SelectVenueService("ktv_premium1", 0), s.VenueServiceBlocker("ktv_premium1", 0));
            var a = s.CityActivities.Single(x => x.Active);
            Assert.AreEqual("ktv", a.DefinitionId); Assert.AreEqual("ktv_premium1", a.ServiceDefinitionId);
            Assert.AreEqual(120, a.Duration); Assert.AreEqual(balance - Money.FromYuan(1800), s.Ledger.Balance);
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Active);
            Assert.AreEqual("ktv_premium1", loaded.CityActivityService(saved).Id);
            loaded.AdvanceClock(121);
            Assert.AreEqual(CityActivityPhase.Completed, saved.State);
            Assert.AreEqual(balance - Money.FromYuan(1800), loaded.Ledger.Balance);
        }

        [TestCase(false, 900)]
        [TestCase(true, 1800)]
        public void PremiumRoomIsChargedOnceAndCanBeSplitOrTreated(bool treat, int cost)
        {
            using var s = Make();
            long start = s.SuggestCityActivityStart("ktv_premium1", "life_ning");
            var a = s.ScheduleCityActivity("ktv_premium1", new[] { "life_ning" }, start);
            Assert.NotNull(a); a.ServiceConfirmed = true; a.ServiceChoice = 0; a.PlayerTreat = treat;
            Assert.AreEqual(Money.FromYuan(cost), s.CityActivityEntryPrice(s.CityActivityService(a), 0, a));
            var balance = s.Ledger.Balance; Attend(s, a);
            Assert.AreEqual(balance - Money.FromYuan(cost), s.Ledger.Balance);
            s.AdvanceClock(121); Assert.AreEqual(balance - Money.FromYuan(cost), s.Ledger.Balance);
        }

        [TestCase("race_ning_study", "life_ning", 43, 0)]
        [TestCase("act_dinner_cook", "res_cook", 44, 0)]
        [TestCase("act_dinner_chef", "life_chef", 45, 0)]
        [TestCase("act_dinner_linhe", "resident_linhe", 46, 1000)]
        [TestCase("act_dinner_farm", "keeper_farm", 47, 0)]
        [TestCase("act_ktv_singer", "life_singer", 48, 0)]
        [TestCase("act_ktv_fuzhi", "resident_fuzhi", 49, -1500)]
        [TestCase("act_ktv_luoya", "resident_luoya", 50, 0)]
        public void InvitationsOnlyMeetOnAttendanceAndSettleAtTheirRealVenue(string eventId, string personId, int number, int netCost)
        {
            using var s = Make(); Assert.IsFalse(s.IsMet(personId));
            var balance = s.Ledger.Balance; var activity = Accept(s, eventId);
            Assert.AreEqual(balance, s.Ledger.Balance); Assert.IsFalse(s.IsMet(personId));
            if (number is 44 or 45 or 46 or 48 or 49) Assert.AreEqual(s.CityPersonState(personId).WorkPlaceId, activity.VenueId);
            if (number == 47) Assert.AreEqual(s.CityPersonState(personId).HomePlaceId, activity.VenueId);
            if (number == 43) Assert.IsEmpty(s.RaceInvitations);
            using var loaded = Reload(s); var a = loaded.CityActivities.Single(x => x.Id == activity.Id);
            Assert.AreEqual(a.VenueId, loaded.CityActivityService(a).VenueId);
            Attend(loaded, a); Assert.IsTrue(loaded.IsMet(personId));
            int relation = loaded.Relation(personId);
            loaded.AdvanceClock(a.Duration + 1);
            Assert.AreEqual(CityActivityPhase.Completed, a.State, a.EndReason);
            Assert.Greater(loaded.Relation(personId), relation);
            Assert.AreEqual(balance - Money.FromYuan(netCost), loaded.Ledger.Balance);
            if (number == 45) Assert.IsTrue(loaded.LearnedPersonStories(personId).Any(story => story.Contains("招牌炖菜")));
            if (number == 50) Assert.IsTrue(a.Participants.All(p => loaded.IsMet(p.PersonId)));
            loaded.RaiseEvent(eventId);
            Assert.NotNull(loaded.PendingEvent, "Already acquainted friends remain eligible for later invitations");
        }

        [Test]
        public void CelebrationUsesRealWinGuestsAndCosts3000ForTheWholeTable()
        {
            using var s = Make(); s.RaiseEvent("race_prize_dinner"); Assert.IsNull(s.PendingEvent);
            var data = s.CreateSave(); data.celebrationRaceKey = "race_0:600"; data.celebrationRacePeople = new[] { "racer_0", "racer_1" };
            using var loaded = GameSession.Load(s.City, data); var balance = loaded.Ledger.Balance;
            var a = Accept(loaded, "race_prize_dinner"); Assert.AreEqual(2, a.Participants.Length);
            Assert.AreEqual(balance, loaded.Ledger.Balance); Attend(loaded, a);
            Assert.AreEqual(balance - Money.FromYuan(3000), loaded.Ledger.Balance);
            loaded.AdvanceClock(91); loaded.RaiseEvent("race_prize_dinner"); Assert.IsNull(loaded.PendingEvent);
        }

        [Test]
        public void LinheTreatCosts2000AndDecliningNeverChangesRelations()
        {
            using var s = Make(); var balance = s.Ledger.Balance;
            var a = Accept(s, "act_dinner_linhe", 1); Attend(s, a);
            Assert.AreEqual(balance - Money.FromYuan(2000), s.Ledger.Balance);
            s.CancelCityActivity(a.Id); int relation = s.Relation("resident_linhe");
            s.RaiseEvent("act_dinner_linhe"); Assert.IsTrue(s.ResolveEvent(s.PendingEvent.Choices.Count - 1));
            Assert.AreEqual(relation, s.Relation("resident_linhe"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FamilyGiftIsARealItemAndCancellationDoesNotConsumeIt(bool cancel)
        {
            using var s = Make(); var balance = s.Ledger.Balance;
            var a = Accept(s, "act_dinner_farm", 1);
            Assert.AreEqual(balance - Money.FromYuan(1000), s.Ledger.Balance);
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.VisitHamper));
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Id == a.Id);
            if (cancel)
            {
                Assert.IsTrue(loaded.CancelCityActivity(saved.Id));
                Assert.AreEqual(1, loaded.Backpack.GiftCount(Gifts.VisitHamper));
            }
            else
            {
                Attend(loaded, saved);
                Assert.IsTrue(saved.EventGiftDelivered); Assert.AreEqual(0, loaded.Backpack.GiftCount(Gifts.VisitHamper));
                loaded.AdvanceClock(91); Assert.That(saved.SettlementSummary, Does.Contain("礼盒"));
                Assert.AreEqual(balance - Money.FromYuan(1000), loaded.Ledger.Balance);
            }
        }

        [TestCase(49, false)]
        [TestCase(100, true)]
        public void AudioInspectionRemainsSelectableBelowThresholdAndPaysOnlyOnceAtCompletion(int intelligence, bool findsProblem)
        {
            using var s = Make(); s.Player.Intelligence = intelligence;
            var a = Accept(s, "act_ktv_fuzhi"); var balance = s.Ledger.Balance;
            Attend(s, a);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 3));
            Assert.AreEqual(findsProblem, a.Choices.Contains("audio-finding"));
            Assert.AreEqual(balance, s.Ledger.Balance);
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Id == a.Id);
            loaded.AdvanceClock(91);
            Assert.AreEqual(balance + Money.FromYuan(1500), loaded.Ledger.Balance);
            loaded.AdvanceClock(20); Assert.AreEqual(balance + Money.FromYuan(1500), loaded.Ledger.Balance);
            Assert.That(saved.SettlementSummary, Does.Contain("1,500"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WetPracticeUsesRealReducedGripCosts1000AndKeepsOrdinaryRecordsSeparate(bool abandon)
        {
            using var s = Make(); At(s, "dealer"); Assert.IsTrue(s.BuyMotor("e_range")); var id = s.Garage.Single().Id;
            At(s, "garage_0"); Assert.IsTrue(s.AcquireGarage("garage_0", false)); Assert.IsTrue(s.ParkCommuteMotor(id, "garage_0"));
            s.RaiseEvent("race_track_visit_rain"); Assert.IsTrue(s.ResolveEvent(0));
            using var loaded = Reload(s); var b = loaded.EventRaceBookings.Single();
            loaded.Clock.Restore(new GameDate(b.Day), 600); At(loaded, "race_0"); var balance = loaded.Ledger.Balance;
            var motor = MotorCatalog.Performance(loaded.Garage.Single()); Assert.IsTrue(loaded.StartEventRace("wet", id));
            Assert.IsTrue(loaded.ActiveRace.WetSurface); Assert.AreEqual(motor.Grip * .6, loaded.ActiveRace.Model.Grip, .001);
            Assert.AreEqual(motor.Braking * .8, loaded.ActiveRace.Model.Braking, .001);
            Assert.AreEqual(balance - Money.FromYuan(1000), loaded.Ledger.Balance);
            loaded.ActiveRace.Finished = true; loaded.ActiveRace.Abandoned = abandon; loaded.ActiveRace.Elapsed = 100; loaded.ActiveRace.Damage = 25;
            loaded.FinishRace();
            Assert.AreEqual(73, loaded.Garage.Single().Condition); Assert.IsEmpty(loaded.EventRaceBookings);
            Assert.IsTrue(loaded.RacingRecords.Single().Key.EndsWith(":event:wet")); Assert.AreEqual(0, loaded.RacingWins);
            Assert.AreEqual(balance - Money.FromYuan(1000), loaded.Ledger.Balance);
        }

        [Test]
        public void BatchAcceptancePrepares41Through50WithoutChangingTheirNumbers()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var s = GameSession.CreateCityLifePreset(fixture.City, "events");
            for (int number = 41; number <= 50; number++)
            {
                Assert.IsTrue(s.DebugRaiseNumberedEvent(number), "Event " + number);
                Assert.AreEqual(GameSession.DebugNumberedEvent(number).Id, s.PendingEvent.Id);
                Assert.IsTrue(s.ResolveEvent(s.PendingEvent.Choices.Count - 1));
            }
        }

        [Test]
        public void LeavingAudioWorkEarlyDoesNotPayAndChefStoryWaitsForCompletion()
        {
            using var s = Make(); var a = Accept(s, "act_ktv_fuzhi"); var balance = s.Ledger.Balance;
            Attend(s, a); s.AdvanceClock(20); Assert.IsTrue(s.CancelCityActivity(a.Id));
            Assert.AreEqual(balance, s.Ledger.Balance);
            using var other = Make(); var meal = Accept(other, "act_dinner_chef"); Attend(other, meal);
            Assert.IsTrue(other.ChooseCityActivity(meal.Id, 1)); Assert.IsFalse(other.Flags.Contains("social:chef-story"));
            Assert.IsTrue(other.CancelCityActivity(meal.Id)); Assert.IsFalse(other.Flags.Contains("social:chef-story"));
        }

        [Test]
        public void MissingPromisedGiftBlocksTheMealWithoutChargingAgain()
        {
            using var s = Make(); var a = Accept(s, "act_dinner_farm", 1);
            Assert.IsTrue(s.Backpack.TakeGift(Gifts.VisitHamper)); var balance = s.Ledger.Balance;
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(s, a.VenueId); Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace)); s.AdvanceClock(1);
            Assert.AreEqual(CityActivityPhase.Blocked, a.State); Assert.AreEqual(balance, s.Ledger.Balance);
            Assert.IsFalse(a.EntryPaid); Assert.IsFalse(a.EventGiftDelivered);
        }

        [Test]
        public void RetriedCelebrationCannotReuseTheSameWinAfterCompletion()
        {
            using var s = Make(); var data = s.CreateSave();
            data.celebrationRaceKey = "race_0:600"; data.celebrationRacePeople = new[] { "racer_0" };
            using var loaded = GameSession.Load(s.City, data);
            var first = Accept(loaded, "race_prize_dinner"); Assert.IsTrue(loaded.CancelCityActivity(first.Id));
            var retry = Accept(loaded, "race_prize_dinner"); Assert.That(retry.SourceId, Does.Contain(":retry:"));
            Attend(loaded, retry); loaded.AdvanceClock(91);
            loaded.RaiseEvent("race_prize_dinner"); Assert.IsNull(loaded.PendingEvent);
        }

        [Test]
        public void ActualFormalVictoryCreatesTheCelebrationInvitationAndPersistsItsParticipants()
        {
            using var s = Make(); At(s, "dealer"); Assert.IsTrue(s.BuyMotor("e_range")); var id = s.Garage.Single().Id;
            At(s, "garage_0"); Assert.IsTrue(s.AcquireGarage("garage_0", false)); Assert.IsTrue(s.ParkCommuteMotor(id, "garage_0"));
            At(s, "race_0"); var coach = s.CityPersonState("racer_0"); coach.Route = null; coach.LocationPlaceId = "race_0";
            Assert.IsTrue(s.StartRace("race_0", id, false));
            var race = s.ActiveRace; Assert.Greater(race.Opponents.Count, 0);
            for (int i = 0; i < race.RivalFinishTime.Length; i++) race.RivalFinishTime[i] = 120 + i;
            race.Elapsed = 100; race.Finished = true; s.FinishRace();
            using var loaded = Reload(s); var a = Accept(loaded, "race_prize_dinner");
            CollectionAssert.AreEquivalent(race.Opponents.Select(o => o.PersonId).Distinct().Take(5), a.Participants.Select(p => p.PersonId));
        }
    }
}
