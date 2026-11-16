using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityTravelTimingTests
    {
        private static GameSession Make(double visualScale = 1)
        {
            var data = GameSessionTests.Layout(); data.distanceScale = visualScale;
            data.places = data.places.Append(new LayoutPlaceData { id = "motor_cars", name = "汽车城", kind = "dealer", node = "dl" }).ToArray();
            foreach (var node in data.nodes) { node.x /= visualScale; node.y = (node.y + 20) / visualScale; }
            foreach (var edge in data.edges) if (edge.length > 0) edge.length /= visualScale;
            var s = new GameSession(CityLayout.FromData(data), 812, Money.FromYuan(100000), GameTimestamp.From(new GameDate(0), 600), false);
            s.EventsEnabled = false; s.PlayerShop.Settings.BaseCustomersPerHour = 0;
            s.Leave(); s.ArriveAtNode("st"); return s;
        }
        [TestCase(VehicleKind.EBike,3)]
        [TestCase(VehicleKind.Tricycle,2.4)]
        [TestCase(VehicleKind.Van,3.84)]
        [TestCase(VehicleKind.Car,4.32)]
        [TestCase(VehicleKind.Taxi,4.8)]
        public void VehicleMovementPresentationAndEtaUseTheSameWalkingRatio(VehicleKind kind,double ratio)
        {
            var vehicle=Vehicles.Get(kind);
            Assert.AreEqual(ratio,vehicle.SpeedPerMinute/Vehicles.Walk.SpeedPerMinute,1e-10);
            Assert.AreEqual(ratio,vehicle.VisualSpeed/Vehicles.Walk.VisualSpeed,1e-10);
            Assert.AreEqual(ratio,CityTravelTiming.ScaledSecondsMeters(vehicle,.25,TimeController.DefaultSecondsPerMinute)
                /CityTravelTiming.ScaledSecondsMeters(Vehicles.Walk,.25,TimeController.DefaultSecondsPerMinute),1e-10);
            Assert.That(CityTravelTiming.Minutes(1000,vehicle)*ratio,
                Is.EqualTo(CityTravelTiming.Minutes(1000,Vehicles.Walk)).Within(ratio));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void EBikeActuallyMovesThreeTimesAsFarForManualAndAutomaticTravel(int speed)
        {
            using(var walk=Make())using(var ride=Make())using(var automatic=Make())
            {
                foreach(var s in new[]{ride,automatic})
                {
                    GameSessionTests.Teleport(s,"dealer");Assert.IsTrue(s.BuyVehicle(VehicleKind.EBike));
                    Assert.IsTrue(s.SetVehicle(VehicleKind.EBike));s.Leave();s.ArriveAtNode("st");
                    foreach(var officer in s.Police.Officers)officer.OnDutyToday=false;
                }
                foreach(var s in new[]{walk,ride,automatic})s.Time.SetSpeed(speed);
                var walkStart=walk.PlayerPosition;var rideStart=ride.PlayerPosition;
                walk.SetSteer(-1,0);ride.SetSteer(-1,0);Assert.IsTrue(automatic.GoTo("mk"));
                for(int i=0;i<30;i++){walk.Tick(.02);ride.Tick(.02);automatic.Tick(.02);}
                double walked=Math.Abs(walk.PlayerPosition.X-walkStart.X),ridden=Math.Abs(ride.PlayerPosition.X-rideStart.X);
                Assert.Greater(walked,0);Assert.AreEqual(walked*3/CityTravelTiming.PlayerWalkSpeedFactor,ridden,.0001);
                Assert.AreEqual(ride.PlayerPosition.X,automatic.PlayerPosition.X,.0001);
                Assert.AreEqual(walk.Clock.Now,ride.Clock.Now);Assert.AreEqual(ride.Clock.Now,automatic.Clock.Now);
            }
        }

        [TestCase(VehicleKind.EBike, "e_city", "e_dash", "dealer")]
        [TestCase(VehicleKind.Tricycle, "t_cargo", "t_sport", "dealer")]
        [TestCase(VehicleKind.Car, "c_city", "c_su7", "motor_cars")]
        public void ActualCommuteModelControlsManualSpeedAndRouteTime(VehicleKind kind, string basicModel, string fasterModel, string dealer)
        {
            using var basic = Make();
            using var faster = Make();
            using var manual = Make();
            Prepare(basic, basicModel);
            Prepare(faster, fasterModel);
            Prepare(manual, fasterModel);

            Assert.AreEqual(Vehicles.Get(kind).SpeedPerMinute, basic.CurrentVehicle.SpeedPerMinute, 1e-9);
            Assert.Greater(faster.CurrentVehicle.SpeedPerMinute, basic.CurrentVehicle.SpeedPerMinute);
            Assert.AreEqual(basic.CarryLimitKg, faster.CarryLimitKg);
            Assert.True(basic.Estimate("mk", out double basicDistance, out int basicMinutes, out _));
            Assert.True(faster.Estimate("mk", out double fasterDistance, out int fasterMinutes, out _));
            Assert.AreEqual(basicDistance, fasterDistance, 1e-9);
            Assert.Less(fasterMinutes, basicMinutes);
            Assert.True(basic.GoTo("mk"));
            Assert.True(faster.GoTo("mk"));
            Assert.AreEqual(fasterMinutes, faster.Trip.EstimatedMinutes);
            manual.SetSteer(-1, 0);
            for (int i = 0; i < 30; i++) { basic.Tick(.02); faster.Tick(.02); manual.Tick(.02); }
            Assert.Less(faster.Trip.RemainingGameMeters, basic.Trip.RemainingGameMeters);
            Assert.AreEqual(faster.PlayerPosition.X, manual.PlayerPosition.X, .0001);
            Assert.AreEqual(basic.Clock.Now, faster.Clock.Now);

            using var restored = GameSession.Load(faster.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(faster.CreateSave())));
            restored.EventsEnabled = false;
            foreach (var officer in restored.Police.Officers) officer.OnDutyToday = false;
            Assert.AreEqual(faster.Trip.Vehicle.Name, restored.Trip.Vehicle.Name);
            Assert.AreEqual(faster.Trip.EstimatedMinutes, restored.Trip.EstimatedMinutes);
            Assert.AreEqual(faster.Trip.RemainingGameMeters, restored.Trip.RemainingGameMeters, .00001);
            faster.Tick(.02);
            restored.Tick(.02);
            Assert.AreEqual(faster.Trip.RemainingGameMeters, restored.Trip.RemainingGameMeters, .00001);

            void Prepare(GameSession session, string model)
            {
                GameSessionTests.Teleport(session, dealer);
                session.DebugAddCash(250000);
                Assert.True(session.BuyMotor(model));
                Assert.True(session.SetVehicle(kind));
                session.Leave();
                session.ArriveAtNode("st");
                foreach (var officer in session.Police.Officers) officer.OnDutyToday = false;
            }
        }
        [Test] public void RealEdgeLengthControlsEtaNotCompressedDrawingLength()
        {
            using (var s = Make())
            {
                Assert.True(s.GoTo("mk"));
                Assert.AreEqual(2000, s.Trip.TotalGameMeters, 1e-6);
                Assert.AreEqual(CityTravelTiming.PlayerMinutes(2000, Vehicles.Walk), s.Trip.EstimatedMinutes);
                double gameBudget = CityTravelTiming.SpeedPerMinute(Vehicles.Walk)*CityTravelTiming.PlayerWalkSpeedFactor;
                for (int i = 0; i < 20; i++) s.Tick(TimeController.DefaultSecondsPerMinute / 20);
                Assert.That(s.Trip.RemainingGameMeters, Is.EqualTo(2000 - gameBudget).Within(.001));
                Assert.Less(Math.Abs(s.PlayerPosition.X), 1, "2km road drawn as 100m must not be crossed at uncompressed visual speed");
            }
        }
        [Test] public void DrawingScaleChangeDoesNotMultiplyAnAlreadyScaledEdgeAgain()
        {
            using (var a = Make(1)) using (var b = Make(2))
            {
                Assert.True(a.GoTo("mk")); Assert.True(b.GoTo("mk"));
                Assert.AreEqual(a.Trip.TotalGameMeters, b.Trip.TotalGameMeters, 1e-6);
                Assert.AreEqual(a.Trip.EstimatedMinutes, b.Trip.EstimatedMinutes);
                for (int i = 0; i < 40; i++) { a.Tick(.02); b.Tick(.02); }
                Assert.That(a.Trip.RemainingGameMeters, Is.EqualTo(b.Trip.RemainingGameMeters).Within(.001));
                Assert.That(a.PlayerPosition.X, Is.EqualTo(b.PlayerPosition.X * 2).Within(.001));
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void ManualAutomaticAndIdleShareClockAndPhysicalPace(int speed)
        {
            using (var manual = Make()) using (var automatic = Make()) using (var idle = Make())
            {
                foreach (var s in new[] { manual, automatic, idle }) s.Time.SetSpeed(speed);
                manual.SetSteer(-1, 0); Assert.True(automatic.GoTo("mk"));
                for (int i = 0; i < 30; i++)
                {
                    manual.Tick(.02); automatic.Tick(.02); idle.Tick(.02);
                    Assert.AreEqual(idle.Clock.Now, manual.Clock.Now); Assert.AreEqual(idle.Clock.Now, automatic.Clock.Now);
                    Assert.That(manual.PlayerPosition.X, Is.EqualTo(automatic.PlayerPosition.X).Within(.001));
                }
                manual.Time.SetPaused(true); automatic.Time.SetPaused(true);
                var before = manual.PlayerPosition; var tripBefore = automatic.Trip.RemainingGameMeters;
                manual.Tick(.25); automatic.Tick(.25);
                Assert.AreEqual(before.X, manual.PlayerPosition.X); Assert.AreEqual(tripBefore, automatic.Trip.RemainingGameMeters);
            }
        }
        [Test] public void SavingMidRoutePreservesPositionRemainingDistanceAndArrival()
        {
            using (var s = Make())
            {
                Assert.True(s.GoTo("mk")); for (int i = 0; i < 30; i++) s.Tick(.02);
                var position = s.PlayerPosition; var remaining = s.Trip.RemainingGameMeters;
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                {
                    loaded.EventsEnabled = false; loaded.PlayerShop.Settings.BaseCustomersPerHour = 0;
                    Assert.NotNull(loaded.Trip); Assert.AreEqual(position.X, loaded.PlayerPosition.X, .00001);
                    Assert.AreEqual(remaining, loaded.Trip.RemainingGameMeters, .00001);
                    s.Time.SetSpeed(8); loaded.Time.SetSpeed(8);
                    for (int i = 0; i < 3000 && (s.Trip != null || loaded.Trip != null); i++)
                    { s.Tick(.02); loaded.Tick(.02); }
                    Assert.IsNull(s.Trip); Assert.IsNull(loaded.Trip); Assert.AreEqual("mk", loaded.PlayerNodeId);
                    Assert.AreEqual(s.Clock.Now, loaded.Clock.Now);
                }
            }
        }

        [Test] public void NewDestinationResumesManualPauseWithoutReleasingWindowHold()
        {
            using (var s = Make())
            {
                s.Time.SetSpeed(4);
                s.Time.SetPaused(true);
                Assert.False(s.GoTo("missing"));
                Assert.True(s.Time.IsUserPaused);

                s.Time.Hold();
                Assert.True(s.BeginCityTravel("flat"));
                Assert.False(s.Time.IsUserPaused);
                Assert.True(s.Time.IsHeld);
                Assert.AreEqual(4, s.Time.Speed);
                var distance = s.Trip.RemainingGameMeters;
                s.Tick(.2);
                Assert.AreEqual(distance, s.Trip.RemainingGameMeters);
                s.Time.Release();
                s.Tick(.2);
                Assert.Less(s.Trip.RemainingGameMeters, distance);
            }
        }

        [Test] public void CancelOrRetargetNearFinalApproachKeepsRealPosition()
        {
            using (var s = Make())
            {
                Assert.True(s.GoTo(s.City.GetPlace("flat")));
                while (s.Trip != null && s.Trip.RemainingGameMeters > 1) s.Tick(.001);
                Assert.NotNull(s.Trip); var at = s.PlayerPosition;
                Assert.True(s.GoTo("mk")); Assert.That(s.PlayerPosition.X, Is.EqualTo(at.X).Within(.00001));
                at = s.PlayerPosition; s.StopTrip(); Assert.IsNull(s.Trip);
                Assert.That(s.PlayerPosition.X, Is.EqualTo(at.X).Within(.00001)); Assert.That(s.PlayerPosition.Y, Is.EqualTo(at.Y).Within(.00001));
            }
        }
    }
}
