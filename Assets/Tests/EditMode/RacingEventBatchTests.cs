using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class RacingEventBatchTests
    {
        private static GameSession Make()
        {
            using var fixture = DistrictProgressionTests.Make();
            var s = new GameSession(fixture.City, 7, Money.FromYuan(10000000), GameTimestamp.From(new GameDate(42), 600), true, false, false);
            s.Tick(0);
            At(s, "dealer"); Assert.IsTrue(s.BuyMotor("e_range"));
            At(s, "garage_0"); Assert.IsTrue(s.AcquireGarage("garage_0", false));
            Assert.IsTrue(s.ParkCommuteMotor(s.Garage.Single().Id, "garage_0"));
            return s;
        }
        private static void At(GameSession s, string place)
        {
            s.StopTrip();
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(place));
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(place).NodeId);
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void ArriveForBooking(GameSession s, EventRaceBooking booking)
        {
            s.Clock.Restore(new GameDate(booking.Day), 600); At(s, booking.TrackId);
            var person = s.CityPersonState(booking.PersonId);
            person.Route = null; person.LocationPlaceId = booking.TrackId; person.ActivityId = null; person.VisitId = null;
        }
        private static bool Prompt(GameSession s)
        {
            s.EventRaised += _ => { };
            return (bool)typeof(GameSession).GetMethod("TryShowRacingBatchFollowup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RiderChallengeDrivesOneLapAndSchedulesRealMealWithoutCashPrize(bool win)
        {
            using var s = Make();
            s.RaiseEvent("race_rider_bet"); Assert.NotNull(s.PendingEvent);
            var before = s.Ledger.Balance;
            Assert.IsTrue(s.ResolveEvent(0)); Assert.AreEqual(before, s.Ledger.Balance);
            using var loaded = Reload(s);
            var booking = loaded.EventRaceBookings.Single(); ArriveForBooking(loaded, booking);
            string id = loaded.Garage.Single().Id;
            Assert.IsNull(loaded.EventRaceStartBlocker("rider", id));
            Assert.IsTrue(loaded.StartEventRace("rider", id));
            var race = loaded.ActiveRace;
            Assert.AreEqual(1, race.Laps); Assert.AreEqual(1, race.Opponents.Count);
            race.Elapsed = 100; race.Finished = true; race.RivalFinishTime[0] = win ? 110 : 90;
            var balance = loaded.Ledger.Balance;
            Assert.NotNull(loaded.FinishRace());
            Assert.AreEqual(balance, loaded.Ledger.Balance, "No transfer in place of a meal or ordinary race prize");
            Assert.AreEqual(0, loaded.RacingWins); Assert.IsEmpty(loaded.EventRaceBookings);
            Assert.IsTrue(Prompt(loaded)); Assert.IsTrue(loaded.ResolveEvent(0));
            var meal = loaded.CityActivities.Single(a => a.DefinitionId == "race_bet_meal");
            Assert.AreEqual(win, meal.HostPays); Assert.AreEqual(2000, CityLifeCatalog.Activity(meal.DefinitionId).PriceYuan);
            Assert.AreEqual(balance, loaded.Ledger.Balance, "Booking is not payment");
            Assert.IsFalse(Prompt(loaded), "Must not offer the same meal repeatedly after booking");
            loaded.Clock.Restore(new GameDate((int)(meal.ScheduledStart / 1440)), (int)(meal.ScheduledStart % 1440));
            At(loaded, meal.VenueId); Assert.IsTrue(loaded.PrepareVenueCompanions(loaded.CurrentPlace));
            var mealBalance = loaded.Ledger.Balance;
            loaded.AdvanceClock(61);
            Assert.AreEqual(CityActivityPhase.Completed, meal.State);
            Assert.AreEqual(mealBalance - Money.FromYuan(win ? 0 : 2000), loaded.Ledger.Balance);
        }

        [Test]
        public void CharityDonationCharges500WithoutBookingOrRaceRewards()
        {
            using var s = Make(); var balance = s.Ledger.Balance;
            s.RaiseEvent("race_charity"); Assert.NotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(1));
            Assert.AreEqual(balance - Money.FromYuan(500), s.Ledger.Balance);
            Assert.IsEmpty(s.EventRaceBookings); Assert.IsFalse(s.Flags.Contains("racing:charity-medal"));
        }

        [Test]
        public void CharityCharges2000AtStartAndAwardsMedalOnlyAfterFinish()
        {
            using var s = Make();
            s.RaiseEvent("race_charity"); Assert.IsTrue(s.ResolveEvent(0));
            var b = s.EventRaceBookings.Single(); ArriveForBooking(s, b);
            string id = s.Garage.Single().Id; var balance = s.Ledger.Balance;
            Assert.IsTrue(s.StartEventRace("charity", id));
            Assert.AreEqual(balance - Money.FromYuan(2000), s.Ledger.Balance);
            Assert.IsFalse(s.Flags.Contains("racing:charity-medal"));
            s.ActiveRace.Finished = true; s.ActiveRace.Elapsed = 100;
            s.FinishRace();
            Assert.AreEqual(balance - Money.FromYuan(2000), s.Ledger.Balance);
            Assert.IsTrue(s.Flags.Contains("racing:charity-medal")); Assert.AreEqual(0, s.RacingWins);
            Assert.IsNull(s.FinishRace());
        }

        [Test]
        public void BatteryWarningPersistsUntilPaidWorkshopReplacementAndReducesPerformance()
        {
            using var s = Make(); var healthy = MotorCatalog.Performance(s.Garage.Single()).Speed;
            s.RaiseEvent("race_battery_old"); Assert.IsTrue(s.ResolveEvent(1));
            using var loaded = Reload(s); var v = loaded.Garage.Single();
            Assert.IsTrue(v.EventBatteryWorn); Assert.AreEqual(healthy * .75, MotorCatalog.Performance(v).Speed, .001);
            Assert.IsFalse(loaded.ReplaceEventBattery(v.Id), "Remote replacement is not allowed");
            At(loaded, "motor_scooter_mod");
            var balance = loaded.Ledger.Balance; long now = loaded.Clock.Now.TotalMinutes;
            Assert.IsTrue(loaded.ReplaceEventBattery(v.Id));
            Assert.AreEqual(balance - Money.FromYuan(loaded.EventBatteryReplacementPrice(v)), loaded.Ledger.Balance);
            Assert.AreEqual(now + 30, loaded.Clock.Now.TotalMinutes);
            Assert.IsFalse(loaded.Garage.Single().EventBatteryWorn); Assert.IsFalse(loaded.ReplaceEventBattery(v.Id));
        }

        [Test]
        public void BatteryFailureHappensDuringDrivingAndForcesAbandonment()
        {
            var race = new RaceSimulation(RaceTracks.Get("race_0"), MotorCatalog.Get("e_city"), true);
            race.Countdown = 0; race.EventBatteryFailureAt = .05;
            race.Step(.2, 1, 0);
            Assert.IsTrue(race.EventBatteryFailed); Assert.IsTrue(race.Abandoned); Assert.IsTrue(race.Finished);
            Assert.Greater(race.Elapsed, 0);
        }

        [Test]
        public void GearIsRealEquipmentPersistsAndDoesNotHealPlayer()
        {
            using var s = Make(); s.Player.Health = 70;
            s.RaiseEvent("race_maker_gear"); Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(70, s.Player.Health); Assert.AreEqual(.12, s.RacingInjuryReduction, .001);
            var balance = s.Ledger.Balance;
            s.RaiseEvent("race_helmet_deal"); Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(balance - Money.FromYuan(2500), s.Ledger.Balance);
            Assert.AreEqual(70, s.Player.Health); Assert.AreEqual(.32, s.RacingInjuryReduction, .001);
            using var loaded = Reload(s); Assert.AreEqual(2, loaded.OwnedRacingGear.Count);
            Assert.AreEqual(.32, loaded.RacingInjuryReduction, .001);
            Assert.IsTrue(loaded.EquipRacingGear("clearance-helmet", false)); Assert.AreEqual(.12, loaded.RacingInjuryReduction, .001);
            Assert.IsTrue(loaded.RepairRacingGear("maker-suit")); Assert.AreEqual(.2, loaded.RacingInjuryReduction, .001);
        }

        [Test]
        public void GearReducesActualCrashInjuryAndSponsorPaysOnlyFormalFinishes()
        {
            using var s = Make(); var data = s.CreateSave();
            data.racingGear = new[] { new RacingGear { Id = "helmet", Slot = "helmet", Condition = 100, Equipped = true } };
            data.racing.Garage[0].SponsorRacesRemaining = 2;
            using var loaded = GameSession.Load(s.City, data); At(loaded, "race_0");
            var coach = loaded.CityPersonState("racer_0"); coach.Route = null; coach.LocationPlaceId = "race_0";
            var balance = loaded.Ledger.Balance;
            Assert.IsTrue(loaded.StartRace("race_0", loaded.Garage.Single().Id, false));
            loaded.ActiveRace.Finished = true; loaded.ActiveRace.Elapsed = 100;
            loaded.FinishRace();
            Assert.AreEqual(1, loaded.Garage.Single().SponsorRacesRemaining);
            Assert.IsTrue(loaded.Ledger.Entries.Any(e => e.Amount == Money.FromYuan(3000) && e.Category == "贴纸赞助"));
            Assert.IsTrue(loaded.StartRace("race_0", loaded.Garage.Single().Id, true));
            loaded.ActiveRace.Finished = true; loaded.ActiveRace.Elapsed = 100;
            loaded.FinishRace(); Assert.AreEqual(1, loaded.Garage.Single().SponsorRacesRemaining);
        }

        [Test]
        public void SpectatorBookingExistsBeforeUnlockAndOnlyRewardsCompletedAttendance()
        {
            using var s = Make(); var data = s.CreateSave();
            data.flags = data.flags.Where(f => f != "racing:access").ToArray();
            using var loaded = GameSession.Load(s.City, data);
            loaded.RaiseEvent("race_spectator"); Assert.NotNull(loaded.PendingEvent);
            var intelligence = loaded.Player.Intelligence;
            Assert.IsTrue(loaded.ResolveEvent(0));
            var a = loaded.CityActivities.Single(x => x.DefinitionId == "race_watch");
            Assert.IsFalse(loaded.IsMet("racer_1")); Assert.AreEqual(intelligence, loaded.Player.Intelligence);
            loaded.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(loaded, "race_0"); Assert.IsTrue(loaded.PrepareVenueCompanions(loaded.CurrentPlace));
            loaded.AdvanceClock(61);
            Assert.AreEqual(CityActivityPhase.Completed, a.State);
            Assert.IsTrue(loaded.IsMet("racer_1")); Assert.AreEqual(intelligence + 1, loaded.Player.Intelligence);
        }

        [Test]
        public void NightRideIsAnEveningAppointmentAndCanBeCancelledWithoutInstantRewards()
        {
            using var s = Make(); var mood = s.Player.Mood;
            s.RaiseEvent("race_night_ride"); Assert.NotNull(s.PendingEvent); Assert.IsTrue(s.ResolveEvent(0));
            var a = s.CityActivities.Single(x => x.DefinitionId == "race_night");
            Assert.GreaterOrEqual(a.ScheduledStart % 1440, 19 * 60); Assert.AreEqual(90, a.Duration);
            Assert.AreEqual(mood, s.Player.Mood); Assert.IsTrue(s.CancelCityActivity(a.Id));
            Assert.AreEqual(CityActivityPhase.Cancelled, a.State);
        }

        [Test]
        public void RematchRequiresAnActualRecordedOpponentAndBookingCancellationRemovesSpecialRace()
        {
            using var s = Make(); s.RaiseEvent("race_rematch"); Assert.IsNull(s.PendingEvent);
            var data = s.CreateSave(); data.lastEventRaceOpponents = new[] { "racer_2" };
            using var loaded = GameSession.Load(s.City, data);
            loaded.RaiseEvent("race_rematch"); Assert.NotNull(loaded.PendingEvent); Assert.IsTrue(loaded.ResolveEvent(0));
            Assert.AreEqual("racer_2", loaded.RaceInvitations.Single().PersonId);
            s.RaiseEvent("race_rider_bet"); Assert.IsTrue(s.ResolveEvent(0)); var b = s.EventRaceBookings.Single();
            Assert.IsTrue(s.CancelRaceInvitation(b.PersonId, b.TrackId, b.Day)); Assert.IsEmpty(s.EventRaceBookings);
        }

        [Test]
        public void InjuryProtectionReducesTheSameActualCrashWithoutChangingVehicleDamage()
        {
            using var s = Make(); var data = s.CreateSave();
            using var plain = GameSession.Load(s.City, data);
            data.racingGear = new[] { new RacingGear { Id = "helmet", Slot = "helmet", Equipped = true, Condition = 100 },
                new RacingGear { Id = "suit", Slot = "suit", Equipped = true, Condition = 100 } };
            using var protectedDriver = GameSession.Load(s.City, data);
            foreach (var driver in new[] { plain, protectedDriver })
            {
                At(driver, "race_0"); Assert.IsTrue(driver.StartRace("race_0", driver.Garage.Single().Id, true));
                driver.ActiveRace.Damage = 100; driver.ActiveRace.Collisions = 2; driver.ActiveRace.Finished = true;
                driver.ActiveRace.Elapsed = 100; driver.FinishRace();
            }
            Assert.Greater(plain.LastRaceSettlement.Injury, 0);
            Assert.AreEqual(plain.LastRaceSettlement.Injury * .6, protectedDriver.LastRaceSettlement.Injury, .001);
            Assert.AreEqual(plain.Garage.Single().Condition, protectedDriver.Garage.Single().Condition);
            Assert.IsTrue(protectedDriver.OwnedRacingGear.All(g => g.Condition == 96));
        }

        [Test]
        public void SponsorOfferHasNoUpfrontPaymentAndPersistsOnTheSelectedVehicle()
        {
            using var s = Make(); var data = s.CreateSave();
            data.racing.Records = new[] { new RaceRecord { Key = "race_0:0:race", Wins = 2 } };
            using var loaded = GameSession.Load(s.City, data);
            var balance = loaded.Ledger.Balance;
            loaded.RaiseEvent("race_sponsor_sticker"); Assert.NotNull(loaded.PendingEvent); Assert.IsTrue(loaded.ResolveEvent(0));
            Assert.AreEqual(balance, loaded.Ledger.Balance); Assert.AreEqual(5, loaded.Garage.Single().SponsorRacesRemaining);
            using var restored = Reload(loaded); Assert.AreEqual(5, restored.Garage.Single().SponsorRacesRemaining);
        }

        [Test]
        public void BatchAcceptanceCanPrepareAllTenAndKeepTheNumbering()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var s = GameSession.CreateCityLifePreset(fixture.City, "events");
            for (int number = 31; number <= 40; number++)
            {
                Assert.IsTrue(s.DebugRaiseNumberedEvent(number), "Event " + number);
                Assert.AreEqual(GameSession.DebugNumberedEvent(number).Id, s.PendingEvent.Id);
                Assert.IsTrue(s.ResolveEvent(s.PendingEvent.Choices.Count - 1));
            }
        }

        [Test]
        public void RaceAidUsesARealHospitalAndChargesOnlyAtAttendanceWithoutHealthPenalty()
        {
            using var s = Make(); var data = s.CreateSave();
            data.raceAidPerson = "racer_1"; data.raceAidDay = s.Clock.Today.TotalDays;
            using var loaded = GameSession.Load(s.City, data);
            loaded.RaiseEvent("race_crash_help"); Assert.NotNull(loaded.PendingEvent);
            var balance = loaded.Ledger.Balance;
            Assert.IsTrue(loaded.ResolveEvent(0)); Assert.AreEqual(balance, loaded.Ledger.Balance);
            var a = loaded.CityActivities.Single(x => x.DefinitionId.StartsWith("race_aid_"));
            Assert.AreEqual(GameSession.HospitalBusiness, loaded.City.GetPlace(a.VenueId).Business);
            loaded.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(loaded, a.VenueId); loaded.Player.Health = 100;
            Assert.IsTrue(loaded.PrepareVenueCompanions(loaded.CurrentPlace)); loaded.AdvanceClock(91);
            Assert.AreEqual(CityActivityPhase.Completed, a.State);
            Assert.AreEqual(balance - Money.FromYuan(1500), loaded.Ledger.Balance);
            Assert.GreaterOrEqual(loaded.Player.Health, 99);
        }

        [Test]
        public void NightRideFollowsAnActualCourseAndItsRouteSurvivesSaving()
        {
            using var s = Make();
            s.RaiseEvent("race_night_ride"); Assert.IsTrue(s.ResolveEvent(0));
            var a = s.CityActivities.Single(x => x.DefinitionId == "race_night");
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(s, a.VenueId); Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace));
            s.AdvanceClock(1);
            Assert.AreEqual(CityActivityPhase.Running, a.State);
            Assert.AreEqual(VehicleKind.EBike, a.PlayerVenueRoute.Transport);
            Assert.Greater(a.PlayerVenueRoute.X.Length, 10);
            var begin = s.PlayerPosition;
            using var restored = Reload(s);
            Assert.AreEqual(begin, restored.PlayerPosition);
            restored.AdvanceClock(15);
            Assert.AreNotEqual(begin, restored.PlayerPosition);
            restored.AdvanceClock(76);
            Assert.AreEqual(CityActivityPhase.Completed, restored.CityActivities.Single(x => x.Id == a.Id).State);
        }
    }
}
