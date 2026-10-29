using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>WASD 自由走路：走廊内任意方向、贴边滑动、时间随路程、进门、接管自动前往。</summary>
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
        public void Steer_FreeDirection_MovesDiagonally()
        {
            GameSession s = AtStreet("s1");
            Walk(s, 1, 1, 3); // 斜着走，x、y 都变
            Assert.IsTrue(s.OffNode);
            Assert.Greater(s.PlayerPosition.X, 10);
            Assert.Greater(s.PlayerPosition.Y, 0);
            Assert.IsNull(s.Trip, "手动走路不产生 Trip");
        }

        [Test]
        public void Steer_AcrossStreet_StopsAtCorridorEdge()
        {
            GameSession s = AtStreet("s1");
            Walk(s, 0, 1, 100); // 垂直街道一路走：到人行道边就停
            Assert.AreEqual(GameSession.MainStreetHalfWidth, s.PlayerPosition.Y, 1e-6);
            Assert.AreEqual(10.0, s.PlayerPosition.X, 1e-6);
        }

        [Test]
        public void Steer_AgainstEdge_SlidesAlong()
        {
            GameSession s = AtStreet("s1");
            Walk(s, 0, 1, 100);
            double x = s.PlayerPosition.X;
            Walk(s, 1, 1, 5); // 斜着顶边：y 不再增加，x 继续
            Assert.AreEqual(GameSession.MainStreetHalfWidth, s.PlayerPosition.Y, 1e-6);
            Assert.Greater(s.PlayerPosition.X, x + 1);
        }

        [Test]
        public void Steer_PastEndOfRoad_StopsAtEndCap()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 100);
            Assert.AreEqual(20.0 + GameSession.MainStreetHalfWidth, s.PlayerPosition.X, 1e-6);
            Assert.AreEqual("pc", s.PlayerNodeId);
        }

        [Test]
        public void Steer_Time_FollowsDistance()
        {
            GameSession s = AtStreet("pc");
            long before = s.Clock.Now.TotalMinutes;
            // pc -> home：游戏里 500 米，走路 80 米/分钟 ≈ 6.25 分钟
            s.SetSteer(0, -1);
            for (int i = 0; i < 300 && s.PlayerPosition.Y > -49.99; i++) s.Tick(0.1);
            s.SetSteer(0, 0);
            Assert.AreEqual("home", s.PlayerNodeId);
            long used = s.Clock.Now.TotalMinutes - before;
            Assert.GreaterOrEqual(used, 6);
            Assert.LessOrEqual(used, 7);
        }

        [Test]
        public void Steer_CanStopAnywhere_AndTurnBack()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 5);
            double x = s.PlayerPosition.X;
            Assert.Greater(x, 0);
            Walk(s, 1, 0, 2);
            Assert.Greater(s.PlayerPosition.X, x, "继续走");
            double mid = s.PlayerPosition.X;
            Walk(s, -1, 0, 3);
            Assert.Less(s.PlayerPosition.X, mid, "掉头往回走");
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
        public void EnterNearby_FromStorefrontSide_SnapsToDoor()
        {
            GameSession s = AtStreet("s1");
            Walk(s, 0, 1, 100); // 贴着店面一侧的人行道站着，离门口 4 米多
            Walk(s, -1, 0, 3); // 往西走一点
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
            Walk(s, -1, 0, 20); // 往 mk 方向走一小段（画面 100 米的长路）
            Assert.IsNull(s.NearbyPlace);
            Assert.IsFalse(s.EnterNearby());
        }

        [Test]
        public void GoTo_FromFreePosition_StillArrives()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 1, 5);
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

            Walk(s, -1, 0, 100); // 反方向走：应当接管
            Assert.IsNull(s.Trip);
            Assert.Less(s.PlayerPosition.X, 0);
        }

        [Test]
        public void Enter_WhileFree_IsRefused()
        {
            GameSession s = AtStreet("st");
            Walk(s, 1, 0, 5);
            Assert.IsTrue(s.OffNode);
            Assert.IsFalse(s.Enter(s.City.GetPlace("tea")));
        }
    }
}