using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>WASD 手动走路：沿路网走、路中停、掉头、进门。</summary>
    public class SteerTests
    {
        // 测试地图：st(0,0) — s1(10,0) — pc(20,0) — home(20,-50)；st — mk(-100,0)。
        private static GameSession AtStreet(string node = "st", long cash = 5000)
        {
            GameSession s = GameSessionTests.Make(cash);
            s.Leave();
            s.ArriveAtNode(node);
            return s;
        }

        private static void Walk(GameSession s, double dx, double dy, int ticks, double dt = 0.1)
        {
            s.SetSteer(dx, dy);
            for (int i = 0; i < ticks; i++) s.Tick(dt);
            s.SetSteer(0, 0);
        }

        [Test]
        public void Steer_AlongStreet_ReachesNextNodes_AndStopsAtJunction()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 100); // 9 m/s × 0.1 × 100 = 90 m，足够走到 pc
            Assert.AreEqual("pc", s.PlayerNodeId);
            Assert.IsFalse(s.OffNode);
            Assert.AreEqual(20.0, s.PlayerPosition.X, 1e-6);
            Assert.IsNull(s.Trip, "手动走路不产生 Trip");
        }

        [Test]
        public void Steer_Time_FollowsDistance()
        {
            GameSession s = AtStreet("pc");
            long before = s.Clock.Now.TotalMinutes;
            // pc -> home：游戏里 500 米，走路 80 米/分钟 ≈ 6.25 分钟
            s.SetSteer(0, -1);
            for (int i = 0; i < 300 && s.PlayerNodeId != "home"; i++) s.Tick(0.1);
            s.SetSteer(0, 0);
            Assert.AreEqual("home", s.PlayerNodeId);
            long used = s.Clock.Now.TotalMinutes - before;
            Assert.GreaterOrEqual(used, 6);
            Assert.LessOrEqual(used, 7);
        }

        [Test]
        public void Steer_CanStopBetweenJunctions_AndTurnBack()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 5);
            Assert.IsTrue(s.OffNode);
            Assert.Greater(s.PlayerPosition.X, 0);
            Assert.Less(s.PlayerPosition.X, 10);
            double x = s.PlayerPosition.X;

            Walk(s, 1, 0, 2);
            Assert.Greater(s.PlayerPosition.X, x, "继续走");

            double mid = s.PlayerPosition.X;
            Walk(s, -1, 0, 3);
            Assert.Less(s.PlayerPosition.X, mid, "掉头往回走");
            Assert.Greater(s.PlayerPosition.X, 0);
            Walk(s, -1, 0, 10);
            Assert.AreEqual("st", s.PlayerNodeId);
            Assert.Less(s.PlayerPosition.X, 0, "走过起点后进了往 mk 的那条路");
        }

        [Test]
        public void Steer_PerpendicularInput_DoesNotMove()
        {
            GameSession s = AtStreet("st");
            Walk(s, 0, 1, 10); // st 只有 +x 和 -x 方向的路
            Assert.AreEqual("st", s.PlayerNodeId);
            Assert.AreEqual(0.0, s.PlayerPosition.X, 1e-9);
        }

        [Test]
        public void Taxi_CannotBeSteered()
        {
            GameSession s = AtStreet("st");
            Assert.IsTrue(s.SetVehicle(VehicleKind.Taxi));
            Assert.IsFalse(s.CanSteer);
            Walk(s, 1, 0, 20);
            Assert.AreEqual("st", s.PlayerNodeId);
            Assert.IsFalse(s.OffNode);
        }

        [Test]
        public void EnterNearby_WorksWithinRadius_AndSnapsToDoor()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 9); // 走 ≈ 8 米，离 s1 的奶茶店入口 < 3 米
            Assert.IsTrue(s.OffNode);
            Assert.IsNotNull(s.NearbyPlace);
            Assert.AreEqual("tea", s.NearbyPlace.Id);
            Assert.IsTrue(s.EnterNearby());
            Assert.AreEqual("tea", s.CurrentPlace.Id);
            Assert.AreEqual("s1", s.PlayerNodeId);
            Assert.IsFalse(s.OffNode);
        }

        [Test]
        public void EnterNearby_FarFromDoors_Fails()
        {
            GameSession s = AtStreet("st");
            Walk(s, -1, 0, 20); // 往 mk 方向走一小段（画面 100 米的长路），离两头的门都很远
            Assert.IsTrue(s.OffNode);
            Assert.IsNull(s.NearbyPlace);
            Assert.IsFalse(s.EnterNearby());
        }

        [Test]
        public void GoTo_FromBetweenJunctions_StillArrives()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 5);
            Assert.IsTrue(s.OffNode);
            Assert.IsTrue(s.GoTo("pc"));
            GameSessionTests.RunTrip(s);
            Assert.AreEqual("pc", s.PlayerNodeId);
            Assert.IsFalse(s.OffNode);
        }

        [Test]
        public void Steer_TakesOverFromAutoTravel()
        {
            GameSession s = AtStreet("st");
            Assert.IsTrue(s.GoTo("home"));
            for (int i = 0; i < 5; i++) s.Tick(0.1);
            Assert.IsNotNull(s.Trip);

            Walk(s, -1, 0, 100); // 反方向走：应当接管并回到 st
            Assert.IsNull(s.Trip);
            Assert.AreEqual("st", s.PlayerNodeId);
        }

        [Test]
        public void Enter_WhileBetweenJunctions_IsRefused()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 5);
            Assert.IsTrue(s.OffNode);
            Assert.IsFalse(s.Enter(s.City.GetPlace("tea")));
        }
    }
}
