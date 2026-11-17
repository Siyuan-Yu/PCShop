using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class RacingTests
    {
        private static GameSession Make(int day = 40, bool enroll = true)
        {
            using var fixture = DistrictProgressionTests.Make();
            var s = new GameSession(fixture.City, 7, Money.FromYuan(10000000),
                GameTimestamp.From(new GameDate(day), 600), true, false, false) { EventsEnabled = false };
            if (enroll)
            {
                At(s, GameSession.StarterRaceGarageId);
                Assert.That(s.UnlockRacingAtGarage(), Is.True);
                At(s, s.Home.Id);
            }
            return s;
        }

        private static string PrepareStarterScooter(GameSession s)
        {
            At(s, "dealer");
            Assert.That(s.BuyMotor("e_city"), Is.True);
            string id = s.Garage.Single().Id;
            At(s, GameSession.StarterRaceGarageId);
            Assert.That(s.AcquireGarage(GameSession.StarterRaceGarageId), Is.True);
            Assert.That(s.ParkCommuteMotor(id, GameSession.StarterRaceGarageId), Is.True);
            return id;
        }

        private static void EnterStarterRaceWithOpponent(GameSession s)
        {
            At(s, GameSession.StarterRaceTrackId);
            var coach = s.CityPersonState("racer_0");
            coach.Route = null;
            coach.LocationPlaceId = GameSession.StarterRaceTrackId;
        }
        private static void At(GameSession s,string id){s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(id));typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.City.GetPlace(id).NodeId);}
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        [Test] public void ChangingTheCommuteCarUpdatesSpeedWhileCarModeIsStillSelected()
        {
            using var s = Make();
            At(s, GameSession.StarterRaceGarageId);
            Assert.True(s.AcquireGarage(GameSession.StarterRaceGarageId));
            At(s, "motor_cars");
            Assert.True(s.BuyMotor("c_city"));
            Assert.True(s.BuyMotor("c_su7", false, GameSession.StarterRaceGarageId));
            Assert.True(s.SetVehicle(VehicleKind.Car));
            double originalSpeed = s.CurrentVehicle.SpeedPerMinute;
            var newerCar = s.Garage.Single(v => v.ModelId == "c_su7");
            At(s, GameSession.StarterRaceGarageId);
            Assert.True(s.SetMotorCommuter(newerCar.Id));
            Assert.AreEqual(VehicleKind.Car, s.Vehicle);
            Assert.AreEqual(MotorCatalog.Get("c_su7").Name, s.CurrentVehicle.Name);
            Assert.Greater(s.CurrentVehicle.SpeedPerMinute, originalSpeed);
            Assert.AreEqual("c_su7", s.CommuteMotorModelId(VehicleKind.Car));
        }

        [Test] public void RacingEnrollmentRequiresCashAndNoLoansAndMustBeDoneAtGarage()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var opening = new GameSession(fixture.City, 7, Money.FromYuan(99999), fixture.Clock.Now);
            foreach (var quest in Quests.All.TakeWhile(q => q.Id != "racing_enrollment"))
                opening.Flags.Add("tutorial:" + quest.Id);
            Assert.That(opening.CurrentQuest()?.Id, Is.EqualTo("racing_enrollment"));
            var garage = opening.City.GetPlace(GameSession.StarterRaceGarageId);
            Assert.That(opening.CityDestinationTravelBlocker(garage, out _, out _), Does.Contain("现金需达到"));
            Assert.That(opening.BeginCityTravel(garage.Id), Is.False);
            opening.ArriveAtNode(garage.NodeId);
            Assert.That(opening.IsPlaceOpen(garage), Is.False);
            Assert.That(opening.Enter(garage), Is.False);
            At(opening, GameSession.StarterRaceGarageId);
            Assert.That(opening.UnlockRacingAtGarage(), Is.False);
            opening.DebugAddCash(1);
            At(opening, "dealer");
            opening.Tick(0);
            Assert.That(opening.RacingUnlocked, Is.False);
            Assert.That(opening.UnlockRacingAtGarage(), Is.False);
            var save = opening.CreateSave();
            save.appLoans = new[] { new AppLoan { Id = 1, StartDay = opening.Clock.Today.TotalDays,
                PrincipalCents = 100000, Months = 1, NoInterest = true } };
            using var s = GameSession.Load(fixture.City, save);
            s.DebugAddCash(1000);
            Assert.That(s.CityDestinationTravelBlocker(garage, out _, out _), Does.Contain("结清所有贷款"));
            Assert.That(s.BeginCityTravel(garage.Id), Is.False);
            s.ArriveAtNode(garage.NodeId);
            Assert.That(s.Enter(garage), Is.False);
            At(s, GameSession.StarterRaceGarageId);
            Assert.That(s.ClosedReason(s.City.GetPlace(GameSession.StarterRaceTrackId)), Does.Contain("结清所有贷款"));
            Assert.That(s.UnlockRacingAtGarage(), Is.False);
            Assert.That(s.RepayAppLoan(1), Is.True);
            s.Tick(0);
            Assert.That(s.RacingUnlocked, Is.False);
            var before = s.Ledger.Balance;
            var time = s.Clock.Now;
            Assert.That(before, Is.EqualTo(Money.FromYuan(100000)));
            Assert.That(s.UnlockRacingAtGarage(), Is.True);
            Assert.That(s.UnlockRacingAtGarage(), Is.False);
            Assert.That(s.Ledger.Balance, Is.EqualTo(before));
            Assert.That(s.Clock.Now, Is.EqualTo(time));
            Assert.That(s.RacingUnlocked, Is.True);
            Assert.That(s.QuestCompleted("racing_enrollment"), Is.True);
            Assert.That(s.CurrentQuest()?.Id, Is.EqualTo("community_goal"));
            s.Ledger.ForceDebit(s.Clock.Today, "测试支出", Money.FromYuan(10000));
            using var loaded = Reload(s);
            Assert.That(loaded.RacingUnlocked, Is.True);
            Assert.That(loaded.QuestCompleted("racing_enrollment"), Is.True);
        }

        [Test] public void GarageTravelStopsAtDoorAndRequiresManualRegistrationBeforeBuyingParking()
        {
            using var s = Make(enroll: false);
            var garage = s.City.GetPlace(GameSession.StarterRaceGarageId);
            At(s, "dealer");
            Assert.That(s.BeginCityTravel(garage.Id), Is.True);
            Assert.That(s.CityIntent.EnterOnArrival, Is.False);
            s.CityIntent.EnterOnArrival = true; // A trip saved before registration-only navigation.
            for (int i = 0; i < 5000 && s.CityIntent != null; i++) s.Tick(.2);
            Assert.That(s.CityIntent, Is.Null);
            Assert.That(s.PlayerNodeId, Is.EqualTo(garage.NodeId));
            Assert.That(s.CurrentPlace, Is.Null);
            Assert.That(s.EnterNearby(), Is.True);
            Assert.That(s.CurrentPlace, Is.EqualTo(garage));
            Assert.That(s.RacingUnlocked, Is.False);
            Money cash = s.Ledger.Balance;
            Assert.That(s.AcquireGarage(garage.Id), Is.False);
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash));
            Assert.That(s.UnlockRacingAtGarage(), Is.True);
            Assert.That(s.AcquireGarage(garage.Id), Is.True);
        }

        [Test] public void OldSaveInsideAnIneligibleStarterGarageLoadsAtTheDoor()
        {
            using var fixture = DistrictProgressionTests.Make();
            var garage = fixture.City.GetPlace(GameSession.StarterRaceGarageId);
            using var s = new GameSession(fixture.City, 7, Money.FromYuan(5000), fixture.Clock.Now);
            At(s, garage.Id);
            var save = s.CreateSave();
            save.racing.RegionalGarages = new[] { new RegionalGarageLease { PlaceId = garage.Id, OwnedSpaces = 1 } };
            using var loaded = GameSession.Load(fixture.City, save);
            Assert.That(loaded.CurrentPlace, Is.Null);
            Assert.That(loaded.PlayerNodeId, Is.EqualTo(garage.NodeId));
            Assert.That(loaded.PlayerPosition, Is.EqualTo(loaded.StandOf(garage)));
            Assert.That(loaded.GarageCapacity(garage.Id), Is.EqualTo(1));
            Assert.That(loaded.EnterNearby(), Is.False);
        }
        private static void Drive(RaceSimulation r)
        {
            for(int i=0;i<36000&&!r.Finished;i++)
            {
                var p=r.Track.Points[r.Next];double angle=Math.Atan2(p.X-r.X,p.Y-r.Y)-r.Heading;angle=Math.Atan2(Math.Sin(angle),Math.Cos(angle));
                double target=Math.Min(r.Model.Speed/7.2*.75,Math.Abs(angle)>.45?5:12);
                r.Step(1d/60,r.Speed<target?1:-.4,Math.Max(-1,Math.Min(1,angle*3)));
            }
        }

        [Test]
        public void TrackPracticePassChargesOnceAndEndsOnlyAfterThePurchasedLaps()
        {
            using var s = Make();
            var vehicle = PrepareStarterScooter(s);
            var trackId = GameSession.StarterRaceTrackId;
            At(s, trackId);
            var cash = s.Ledger.Balance;
            var start = s.Clock.Now.TotalMinutes;
            Assert.That(s.StartRacePracticePass(trackId, vehicle, 4), Is.False);
            Assert.That(s.StartRacePracticePass(trackId, vehicle, 6), Is.True);
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash - Money.FromYuan(180)));
            Assert.That(s.StartRacePracticePass(trackId, vehicle, 6), Is.False);
            var race = s.ActiveRace;
            Assert.That(race.PaidPractice, Is.True);
            Assert.That(race.Laps, Is.EqualTo(6));
            race.Countdown = 0;
            race.Elapsed = 610;
            race.Step(.02, 0, 0);
            Assert.That(race.Finished, Is.False);
            Assert.Zero(s.Tick(60));
            s.AdvanceClock(60);
            Assert.That(s.Clock.Now.TotalMinutes, Is.EqualTo(start));
            race.Lap = race.Laps - 1;
            race.Next = 0;
            race.X = race.Track.Points[0].X - 1;
            race.Y = race.Track.Points[0].Y;
            race.Step(.02, 0, 0);
            Assert.That(race.Finished, Is.False);
            race.X = race.Track.Points[0].X;
            race.Step(.02, 0, 0);
            Assert.That(race.Lap, Is.EqualTo(race.Laps));
            Assert.That(race.Finished, Is.True);
            Assert.That(s.FinishRace(), Is.Not.Null);
            Assert.That(s.Clock.Now.TotalMinutes, Is.EqualTo(start + 240));
            Assert.That(s.Ledger.Balance, Is.EqualTo(cash - Money.FromYuan(180)));
            Assert.That(s.FinishRace(), Is.Null);
        }

        [Test]
        public void TuningCharges800AndRepairsActualVehicle()
        {
            using var initial = Make();
            PrepareStarterScooter(initial);
            var save = initial.CreateSave();
            save.racing.Garage.Single().Condition = 70;
            using var s = GameSession.Load(initial.City, save);
            var cash = s.Ledger.Balance;
            var time = s.Clock.Now.TotalMinutes;
            s.RaiseEvent("race_jingxing_tuning");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(cash - Money.FromYuan(800), s.Ledger.Balance);
            Assert.AreEqual(time + 30, s.Clock.Now.TotalMinutes);
            Assert.AreEqual(85, s.Garage.Single().Condition);
        }

        [Test]
        public void CollectorTrialUsesBorrowedCarForOneActualLap()
        {
            using var initial = Make();
            var save = initial.CreateSave();
            save.districtProgress.UnlockedTier = 3;
            using var s = GameSession.Load(initial.City, save);
            Assert.AreEqual(3, s.DistrictAccessTier);
            s.RaiseEvent("race_ruoning_rare");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.IsTrue(s.HasCollectorTrial);
            At(s, "race_3");
            var owner = s.CityPersonState("racer_3");
            owner.LocationPlaceId = "race_3";
            owner.Route = null;
            Assert.IsTrue(s.StartCollectorTrial());
            Assert.AreEqual(1, s.ActiveRace.Laps);
            Assert.AreEqual("c_718", s.ActiveRace.Model.Id);
            Drive(s.ActiveRace);
            Assert.IsTrue(s.ActiveRace.Finished);
            Assert.IsFalse(s.ActiveRace.Abandoned);
            Assert.IsNotNull(s.FinishRace());
            Assert.AreEqual(0, s.Garage.Count);
            Assert.IsFalse(s.HasCollectorTrial);
            Assert.That(s.Relation("racer_3"), Is.GreaterThan(8));
        }

        [Test]
        public void MingRaceBooksBeforeLending3000AndWritesBothToSchedule()
        {
            using var s = Make();
            var cash = s.Ledger.Balance;
            s.RaiseEvent("race_ming_lunch");
            Assert.NotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(cash - Money.FromYuan(3000), s.Ledger.Balance);
            Assert.IsTrue(s.RaceInvitations.Any(i => i.PersonId == "life_ming"));
            Assert.IsTrue(s.Schedule().Any(entry => entry.Kind == "race_invite"));
            Assert.IsTrue(s.Schedule().Any(entry => entry.Text.Contains("许明归还周转款")));
        }

        [Test]
        public void ClubGatheringBooksARealMealWithoutChargingBeforeArrival()
        {
            using var s = Make();
            var before = s.Ledger.Balance;
            s.RaiseEvent("race_zhixia_club");
            Assert.NotNull(s.PendingEvent);
            int meal = s.PendingEvent.Choices.FindIndex(c => c.Text.Contains("吃饭小聚"));
            Assert.That(meal, Is.GreaterThanOrEqualTo(0));
            Assert.IsTrue(s.ResolveEvent(meal));
            var activity = s.CityActivities.Single();
            Assert.AreEqual("life_restaurant", activity.VenueId);
            Assert.AreEqual("racer_1", activity.Participants.Single().PersonId);
            Assert.AreEqual(before, s.Ledger.Balance);
            Assert.AreEqual(0, s.RaceInvitations.Count);
        }
        [Test] public void CatalogDealerSplitAndDistinctScooters()
        {
            Assert.That(MotorCatalog.All.Count(m=>m.Kind==VehicleKind.EBike),Is.GreaterThanOrEqualTo(5));
            var s=Make();At(s,"dealer");Assert.That(s.BuyMotor("c_su7"),Is.False);Assert.That(s.BuyMotor("e_turn"),Is.True);Assert.That(s.RacingUnlocked,Is.True);
            At(s,"motor_cars");Assert.That(s.BuyMotor("e_city"),Is.False);Assert.That(s.BuyMotor("v_basic"),Is.True);
            Assert.That(s.Garage.Select(v=>v.ModelId),Is.EquivalentTo(new[]{"e_turn","v_basic"}));
        }
        [Test] public void GarageMigratesOldFleetAndRoundTripsUpgrades()
        {
            var s=Make();var d=s.CreateSave();d.racing=null;d.ownedVehicles=new[]{(int)VehicleKind.EBike,(int)VehicleKind.EBike,(int)VehicleKind.Car};s=GameSession.Load(s.City,d);
            Assert.That(s.Garage.Count,Is.EqualTo(3));Assert.That(s.Garage.Select(v=>v.Id).Distinct().Count(),Is.EqualTo(3));
            At(s,"motor_scooter_mod");var car=s.Garage.First(v=>v.ModelId=="e_city");double old=MotorCatalog.Performance(car).Acceleration;
            Assert.That(s.UpgradeMotor(car.Id,0),Is.True);Assert.That(MotorCatalog.Performance(s.Garage.First(v=>v.Id==car.Id)).Acceleration,Is.GreaterThan(old));
            s=Reload(s);Assert.That(s.Garage.First(v=>v.Id==car.Id).Motor,Is.EqualTo(1));Assert.That(s.Garage.Count,Is.EqualTo(3));
            Assert.That(s.UpgradeMotor(s.Garage.First(v=>v.ModelId=="c_city").Id,0),Is.False);
        }
        [Test] public void RaceRequiresPhysicalArrivalAndLocallyParkedVehicle()
        {
            var s=Make();var id=PrepareStarterScooter(s);
            Assert.That(s.StartRace("race_0",id,true),Is.False);At(s,"race_0");
            Assert.That(s.StartRace("race_0",id,true),Is.True);Assert.That(s.CanSave,Is.False);Assert.That(s.StartRace("race_0",id,true),Is.False);
            long now=s.Clock.Now.TotalMinutes;Assert.That(s.Tick(10),Is.Zero);Assert.That(s.Clock.Now.TotalMinutes,Is.EqualTo(now));Assert.That(s.BeginCityTravel("dealer"),Is.False);
            s.Leave();Assert.That(s.CurrentPlace.Id,Is.EqualTo("race_0"));
            s.ActiveRace.Abandon();s.FinishRace();Assert.That(s.CanSave,Is.True);
            At(s,"race_2");Assert.That(s.StartRace("race_2",id,false),Is.False);
        }
        [TestCase(0,"e_city")][TestCase(0,"t_cargo")][TestCase(0,"c_city")]
        [TestCase(1,"e_race")][TestCase(1,"t_sport")][TestCase(1,"c_su7")]
        [TestCase(2,"e_race")][TestCase(2,"t_sport")][TestCase(2,"c_718")]
        [TestCase(3,"e_race")][TestCase(3,"t_sport")][TestCase(3,"c_fa")]
        public void EveryTrackAndVehicleClassCanCompleteUsingDrivingInputs(int tier,string model)
        {
            var r=new RaceSimulation(RaceTracks.All[tier],MotorCatalog.Get(model),true);Drive(r);
            Assert.That(r.Abandoned,Is.False,$"{model} {tier}: lap {r.Lap}, next {r.Next}, collisions {r.Collisions}");Assert.That(r.Finished,Is.True);Assert.That(r.Lap,Is.EqualTo(r.Track.Laps));
        }
        [TestCase(0, 20, 416.175)]
        [TestCase(1, 28, 540.375)]
        [TestCase(2, 28, 695.52)]
        [TestCase(3, 28, 836.205)]
        public void ResizedTrackKeepsItsReceptionClearAndHasTheRequestedDrivingDimensions(
            int tier, double roadWidth, double lapLength)
        {
            using var s = DistrictProgressionTests.Make();
            var track = RaceTracks.Get(RaceTracks.All[tier].Id, s.City);
            var race = new RaceSimulation(track, MotorCatalog.Get("e_city"), true);
            Assert.That(track.HalfRoad * 2, Is.EqualTo(roadWidth));
            Assert.That(race.Length, Is.EqualTo(lapLength).Within(.01));

            foreach (var point in track.Points)
            {
                Assert.That(Math.Abs(point.X) + track.HalfRoad + 4.2, Is.LessThan(track.Width / 2));
                Assert.That(Math.Abs(point.Y) + track.HalfRoad + 4.2, Is.LessThan(track.Depth / 2));
            }
            foreach (var place in new[] { s.City.GetPlace(track.Id), s.City.GetPlace("garage_" + tier) })
            {
                var node = s.City.ViewPosition(place.NodeId);
                double dx = Math.Abs(track.X - (node.X + place.EntranceX));
                double dy = Math.Abs(track.Y - (node.Y + place.EntranceY));
                Assert.That(dx > track.Width / 2 + 5 + place.Width / 2
                    || dy > track.Depth / 2 + 5 + place.Depth, Is.True, place.Id);
            }
        }

        [Test] public void InputsPauseRecoveryAndCollisionArePhysical()
        {
            var r=new RaceSimulation(RaceTracks.All[0],MotorCatalog.Get("e_city"),false);r.Countdown=0;
            for(int i=0;i<120;i++)r.Step(1d/60,1,0);Assert.That(r.Speed,Is.GreaterThan(0));double h=r.Heading;r.Step(.1,1,1);Assert.That(r.Heading,Is.GreaterThan(h));
            r.Paused=true;double x=r.X;r.Step(.1,1,1);Assert.That(r.X,Is.EqualTo(x));r.Paused=false;r.Recover();Assert.That(r.Penalty,Is.EqualTo(5));Assert.That(r.Lap,Is.Zero);
            for(int i=0;i<900;i++)r.Step(1d/60,1,0);Assert.That(r.Collisions,Is.GreaterThan(0));var fast=new RaceSimulation(RaceTracks.All[0],MotorCatalog.Get("c_fa"),true);fast.Countdown=0;for(int i=0;i<900;i++)fast.Step(1d/60,1,0);Assert.That(fast.Damage,Is.GreaterThan(0));
        }
        [Test] public void SettlementOnlyOnceNoPrizeForRetiringAndSavedRecords()
        {
            var s=Make(42);var id=PrepareStarterScooter(s);EnterStarterRaceWithOpponent(s);var balance=s.Ledger.Balance.Cents;
            Assert.That(s.StartRace("race_0",id,false),Is.True);Assert.That(s.Ledger.Balance.Cents,Is.EqualTo(balance-200000));
            s.ActiveRace.Abandon();Assert.That(s.FinishRace(),Does.Contain("退赛"));var after=s.Ledger.Balance.Cents;Assert.That(s.FinishRace(),Is.Null);Assert.That(s.Ledger.Balance.Cents,Is.EqualTo(after));
            Assert.That(Reload(s).RacingRecords.Single().Starts,Is.EqualTo(1));
        }
        [Test] public void ActualRaceCompletionCreditsOnceAndMeetsRacer()
        {
            var s=Make(42);var id=PrepareStarterScooter(s);EnterStarterRaceWithOpponent(s);Assert.That(s.StartRace("race_0",id,false),Is.True);Drive(s.ActiveRace);
            Assert.That(s.ActiveRace.Abandoned,Is.False);s.FinishRace();Assert.That(s.IsMet("racer_0"),Is.True);Assert.That(s.Ledger.Entries.Count(e=>e.Category=="赛事奖金"),Is.EqualTo(1));
        }
        [Test]
        public void OldCourseBestDoesNotPreventNewBestOrRemoveCareerWins()
        {
            using var initial = Make();
            var vehicle = PrepareStarterScooter(initial);
            var save = initial.CreateSave();
            save.racing.Records = new[]
            {
                new RaceRecord { Key = "race_0:0:practice", BestCourseRevision = 1, BestSeconds = 1, Wins = 3, Starts = 4 },
                new RaceRecord { Key = "race_1:0:practice", BestCourseRevision = 1, BestSeconds = 2 }
            };
            using var s = GameSession.Load(initial.City, save);
            At(s, GameSession.StarterRaceTrackId);
            Assert.That(s.RacingRecords.Single(r => r.Key.StartsWith("race_0:")).BestOnCurrentCourse, Is.False);
            Assert.That(s.RacingRecords.Single(r => r.Key.StartsWith("race_1:")).BestOnCurrentCourse, Is.True);
            Assert.That(s.StartRace(GameSession.StarterRaceTrackId, vehicle, true), Is.True);
            Drive(s.ActiveRace);
            Assert.That(s.ActiveRace.Abandoned, Is.False);
            s.FinishRace();
            using var loaded = Reload(s);
            var record = loaded.RacingRecords.Single(r => r.Key.StartsWith("race_0:"));
            Assert.That(record.BestOnCurrentCourse, Is.True);
            Assert.That(record.BestSeconds, Is.GreaterThan(1));
            Assert.That(record.Wins, Is.EqualTo(3));
            Assert.That(record.Starts, Is.EqualTo(5));
        }
        [Test] public void RivalsFinishingFirstRemainAheadAfterPlayerCrossesFinish()
        {
            var r=new RaceSimulation(RaceTracks.All[0],MotorCatalog.Get("e_city"),false);
            for(int i=0;i<12000;i++)r.Step(1d/60,0,0);
            Drive(r);Assert.That(r.Finished,Is.True);Assert.That(r.Abandoned,Is.False);Assert.That(r.Position,Is.EqualTo(3));
        }
        [Test] public void AllFourNewContactsHaveTheirOwnImportedPortrait()
        {
            foreach(var t in RaceTracks.All)Assert.That(Resources.Load<Texture2D>("PortraitsRetro/"+t.Npc),Is.Not.Null,t.Npc);
        }
        [Test] public void TrackModelsBuildWithoutChangingSessionOrTakingScreenshots()
        {
            foreach(var t in RaceTracks.All){var go=new GameObject("test-track");try{var view=go.AddComponent<RaceTrackView>();view.Build(t);var r=new RaceSimulation(t,MotorCatalog.Get("e_city"),false);view.Begin(r);view.Sync(r);Assert.That(go.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(20));view.End();}finally{UnityEngine.Object.DestroyImmediate(go);}}
        }
        [Test] public void MapHasFourNonTradableReachableVenuesAndSeparateShops()
        {
            var s=Make();foreach(var t in RaceTracks.All){var p=s.City.GetPlace(t.Id);Assert.That(p.IsTradable,Is.False);Assert.That(s.DistrictTierOf(p),Is.EqualTo(t.Tier));Assert.That(s.City.Map.Nodes.Any(n=>n.Id==p.NodeId),Is.True);}
            Assert.That(s.City.GetPlace("dealer").IsTradable,Is.False);Assert.That(s.City.HasPlace("motor_scooter_mod"),Is.True);Assert.That(s.City.HasPlace("motor_cars"),Is.True);
        }
        [Test] public void AcceptanceFixtureHasRealVehiclesShowroomAndIndependentSave()
        {
            var city=Make().City;var s=GameSession.CreateCityLifePreset(city,"racing");
            Assert.That(s.IsCityLifeTest,Is.True);Assert.That(s.Garage.Count,Is.EqualTo(6));Assert.That(s.ManagedVentures.Count(v=>v.Kind=="cars"),Is.EqualTo(1));
            Assert.That(s.CurrentPlace.Id,Is.EqualTo("race_0"));Assert.That(s.DistrictAccessTier,Is.EqualTo(3));Assert.That(Reload(s).Garage.Count,Is.EqualTo(6));
        }
        [Test] public void RealUiCanShowDealerGarageReceptionAndDrivingHud()
        {
            var s=Make(enroll: false);var root=new GameObject("RacingUiTest");root.SetActive(false);var ctrl=root.AddComponent<PCShopSell.Game.GameController>();
            typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(ctrl,s);typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(ctrl,s.City);
            var previous=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(ctrl);typeof(PCShopSell.Game.GameController).GetProperty("Ui").SetValue(ctrl,ui);
            try
            {
                At(s,"dealer");ui.OnEnteredPlace(s.CurrentPlace);Assert.That(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Text>()).Any(t=>t.text.Contains("顺风·逐风R")),Is.True);
                ui.CloseAllWindows();
                At(s, GameSession.StarterRaceGarageId);
                ui.OnEnteredPlace(s.CurrentPlace);
                var registration = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
                    .Single(b => b.GetComponentInChildren<UnityEngine.UI.Text>()?.text == "办理赛车登记");
                Assert.That(registration.interactable, Is.True);
                registration.onClick.Invoke();
                Assert.That(s.RacingUnlocked, Is.True);
                ui.CloseAllWindows();
                PrepareStarterScooter(s);
                At(s, "race_0");
                ui.OnEnteredPlace(s.CurrentPlace);
                var passes = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
                    .Where(button => button.name.StartsWith("RacePracticePass_")).ToArray();
                Assert.That(passes, Has.Length.EqualTo(4));
                var pass = passes.Single(button => button.name == "RacePracticePass_3laps");
                Assert.That(pass.interactable, Is.True);
                StringAssert.Contains("3 圈", pass.GetComponentInChildren<UnityEngine.UI.Text>().text);
                pass.onClick.Invoke();
                Assert.That(s.ActiveRace?.PaidPractice, Is.True);
                ui.BeginDrivingHud();ui.UpdateDrivingHud();Canvas.ForceUpdateCanvases();
                var hud=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<RectTransform>()).Single(t=>t.name=="DrivingHud");Assert.That(hud.rect.height,Is.GreaterThan(80));
                s.ActiveRace.Abandon();ui.EndDrivingHud(s.FinishRace());Assert.That(ui.Canvases.Single(c=>c.name=="UI_Hud").enabled,Is.True);
            }
            finally{ui.CloseAllWindows();foreach(var c in ui.Canvases)UnityEngine.Object.DestroyImmediate(c.gameObject);UnityEngine.Object.DestroyImmediate(root);s.Dispose();if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
        }
        [Test] public void UsedCarLoopUsesContactsRealStockCashAndSingleDelivery()
        {
            var s=Make();var ventures=(System.Collections.Generic.List<ManagedVenture>)typeof(GameSession).GetField("_ventures",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
            var v=new ManagedVenture{PlaceId="life_unit_s_market_2",Kind="cars",OpenDay=40,LastSettledDay=40,ReserveCents=500000};ventures.Add(v);
            Assert.That(s.MotorLeads().Any(),Is.False);At(s,"race_0");Assert.That(s.MeetRacer("race_0"),Is.True);At(s,v.PlaceId);
            var lead=s.MotorLeads().Single();var before=s.Ledger.Balance.Cents;Assert.That(s.BuyMotorLead(v.PlaceId,lead.Id),Is.True);Assert.That(s.Ledger.Balance.Cents,Is.LessThanOrEqualTo(before-lead.Price*100));Assert.That(s.BuyMotorLead(v.PlaceId,lead.Id),Is.False);
            var id=s.UsedMotors.Single().Vehicle.Id;Assert.That(s.ListUsedMotor(id,50000),Is.False);Assert.That(s.InspectUsedMotor(id),Is.True);Assert.That(s.RepairUsedMotor(id),Is.True);
            Assert.That(s.ListUsedMotor(id,(long)(MotorCatalog.Get(lead.ModelId).Price*.8)),Is.True);s=Reload(s);Assert.That(s.UsedMotors.Single().Vehicle.Id,Is.EqualTo(id));Assert.That(s.CloseVenture(v.PlaceId),Is.False);
            v=s.VentureAt(v.PlaceId);var settle=typeof(GameSession).GetMethod("SettleUsedMotors",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int day=43;day<130&&s.UsedMotors.Single().Offer==0;day+=3)settle.Invoke(s,new object[]{v,new GameDate(day),0L});
            Assert.That(s.UsedMotors.Single().Offer,Is.GreaterThan(0));At(s,v.PlaceId);Assert.That(s.AcceptMotorBuyer(id),Is.True);Assert.That(s.AcceptMotorBuyer(id),Is.False);Assert.That(s.UsedMotors.Count,Is.Zero);Assert.That(s.Garage.Count,Is.Zero);
        }
        [Test] public void PersonalVehiclesMoveBetweenGarageAndSizedShowroomWithoutDuplicates()
        {
            var s=Make();var ventures=(System.Collections.Generic.List<ManagedVenture>)typeof(GameSession).GetField("_ventures",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
            const string shop="life_unit_s_market_2";ventures.Add(new ManagedVenture{PlaceId=shop,Kind="cars",ReserveCents=500000});At(s,"motor_cars");
            s.BuyMotor("v_basic");s.BuyMotor("c_city");s.BuyMotor("c_su7");var ids=s.Garage.Select(v=>v.Id).ToArray();At(s,shop);
            Assert.That(s.UsedMotorCapacity(shop),Is.EqualTo(2));Assert.That(s.ConsignMotor(shop,ids[0]),Is.True);Assert.That(s.ConsignMotor(shop,ids[0]),Is.False);
            Assert.That(s.ConsignMotor(shop,ids[1]),Is.True);Assert.That(s.ConsignMotor(shop,ids[2]),Is.False);Assert.That(s.Garage.Count,Is.EqualTo(1));
            Assert.That(s.WithdrawUsedMotor(ids[0]),Is.True);Assert.That(s.WithdrawUsedMotor(ids[0]),Is.False);Assert.That(s.Garage.Count,Is.EqualTo(2));Assert.That(Reload(s).UsedMotors.Count,Is.EqualTo(1));
        }
    }
}
