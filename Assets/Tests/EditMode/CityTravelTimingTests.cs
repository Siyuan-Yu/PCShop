using System;
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
            foreach (var node in data.nodes) { node.x /= visualScale; node.y = (node.y + 20) / visualScale; }
            foreach (var edge in data.edges) if (edge.length > 0) edge.length /= visualScale;
            var s = new GameSession(CityLayout.FromData(data), 812, Money.FromYuan(100000), GameTimestamp.From(new GameDate(0), 600), false);
            s.EventsEnabled = false; s.PlayerShop.Settings.BaseCustomersPerHour = 0;
            s.Leave(); s.ArriveAtNode("st"); return s;
        }
        [Test] public void RealEdgeLengthControlsEtaNotCompressedDrawingLength()
        {
            using (var s = Make())
            {
                Assert.True(s.GoTo("mk"));
                Assert.AreEqual(2000, s.Trip.TotalGameMeters, 1e-6);
                Assert.AreEqual(CityTravelTiming.Minutes(2000, Vehicles.Walk), s.Trip.EstimatedMinutes);
                double gameBudget = CityTravelTiming.SpeedPerMinute(Vehicles.Walk);
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
