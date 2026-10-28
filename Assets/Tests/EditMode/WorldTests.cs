using System;
using System.Collections.Generic;
using NUnit.Framework;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class WorldMapTests
    {
        [Test]
        public void AddEdge_DefaultsToStraightDistance()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 3, 4);
            map.AddEdge("a", "b");
            Assert.AreEqual(5.0, map.NeighborsOf("a")[0].Length, 1e-9);
            Assert.AreEqual(5.0, map.NeighborsOf("b")[0].Length, 1e-9);
        }

        [Test]
        public void AddEdge_AllowsLongerDetour_ButNotShorter()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 3, 4);
            Assert.Throws<ArgumentException>(() => map.AddEdge("a", "b", 4.0));
            map.AddEdge("a", "b", 9.0);
            Assert.AreEqual(9.0, map.NeighborsOf("a")[0].Length, 1e-9);
        }

        [Test]
        public void DuplicateNodeOrEdge_Throws()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 1, 0);
            Assert.Throws<ArgumentException>(() => map.AddNode("a", 5, 5));
            map.AddEdge("a", "b");
            Assert.Throws<ArgumentException>(() => map.AddEdge("b", "a"));
        }

        [Test]
        public void UnknownNode_Throws()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            Assert.Throws<KeyNotFoundException>(() => map.AddEdge("a", "zzz"));
            Assert.Throws<KeyNotFoundException>(() => map.NeighborsOf("zzz"));
        }
    }

    public class PathfinderTests
    {
        private static WorldMap BuildSquareWithShortcut()
        {
            // a(0,0) -- b(100,0)
            // |         |
            // d(0,100)--c(100,100)
            // 另有一条从 a 到 c 的"绕路"边，实际长度 400（比直线 141.4 长很多）
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 100, 0);
            map.AddNode("c", 100, 100);
            map.AddNode("d", 0, 100);
            map.AddEdge("a", "b");
            map.AddEdge("b", "c");
            map.AddEdge("c", "d");
            map.AddEdge("d", "a");
            map.AddEdge("a", "c", 400);
            return map;
        }

        [Test]
        public void FindsShortestPath_NotTheDetourEdge()
        {
            var map = BuildSquareWithShortcut();
            PathResult r = Pathfinder.FindPath(map, "a", "c");
            Assert.IsTrue(r.Found);
            Assert.AreEqual(200.0, r.Distance, 1e-9);
            Assert.AreEqual(3, r.NodeIds.Count);
            Assert.AreEqual("a", r.NodeIds[0]);
            Assert.AreEqual("c", r.NodeIds[2]);
        }

        [Test]
        public void SameNode_ReturnsZeroDistancePath()
        {
            var map = BuildSquareWithShortcut();
            PathResult r = Pathfinder.FindPath(map, "b", "b");
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0.0, r.Distance, 1e-9);
            CollectionAssert.AreEqual(new[] { "b" }, r.NodeIds);
        }

        [Test]
        public void DisconnectedNodes_ReturnNotFound()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 10, 0);
            map.AddNode("island", 500, 500);
            map.AddEdge("a", "b");
            PathResult r = Pathfinder.FindPath(map, "a", "island");
            Assert.IsFalse(r.Found);
            Assert.AreEqual(0, r.NodeIds.Count);
        }

        [Test]
        public void UnknownEndpoint_Throws()
        {
            var map = BuildSquareWithShortcut();
            Assert.Throws<KeyNotFoundException>(() => Pathfinder.FindPath(map, "a", "nope"));
        }

        [Test]
        public void MatchesBruteForce_OnRandomGraphs()
        {
            // 用 Floyd–Warshall 作为对照，验证 A* 在随机图上总能得到最短距离。
            for (int seed = 1; seed <= 20; seed++)
            {
                var rng = new Random(seed);
                int n = 25;
                var map = new WorldMap();
                var xs = new double[n];
                var ys = new double[n];
                for (int i = 0; i < n; i++)
                {
                    xs[i] = rng.NextDouble() * 1000;
                    ys[i] = rng.NextDouble() * 1000;
                    map.AddNode("n" + i, xs[i], ys[i]);
                }

                var dist = new double[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        dist[i, j] = i == j ? 0 : double.PositiveInfinity;

                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (rng.NextDouble() > 0.15) continue;
                        double straight = Math.Sqrt((xs[i] - xs[j]) * (xs[i] - xs[j]) + (ys[i] - ys[j]) * (ys[i] - ys[j]));
                        double len = straight * (1.0 + rng.NextDouble() * 0.5);
                        map.AddEdge("n" + i, "n" + j, len);
                        dist[i, j] = len;
                        dist[j, i] = len;
                    }
                }

                for (int k = 0; k < n; k++)
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                            if (dist[i, k] + dist[k, j] < dist[i, j])
                                dist[i, j] = dist[i, k] + dist[k, j];

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        PathResult r = Pathfinder.FindPath(map, "n" + i, "n" + j);
                        if (double.IsPositiveInfinity(dist[i, j]))
                        {
                            Assert.IsFalse(r.Found, $"seed {seed}: {i}->{j} 应无路径");
                        }
                        else
                        {
                            Assert.IsTrue(r.Found, $"seed {seed}: {i}->{j} 应有路径");
                            Assert.AreEqual(dist[i, j], r.Distance, 1e-6, $"seed {seed}: {i}->{j}");
                        }
                    }
                }
            }
        }
    }


    public class TravelTests
    {
        private static WorldMap Street()
        {
            // 住所 -- 电脑城：800 米；电脑城 -- 远处：2000 米
            var map = new WorldMap();
            map.AddNode("home", 0, 0);
            map.AddNode("mall", 800, 0);
            map.AddNode("far", 800, 2000);
            map.AddEdge("home", "mall");
            map.AddEdge("mall", "far");
            return map;
        }

        [Test]
        public void Minutes_RoundUp_AndAtLeastOne()
        {
            Assert.AreEqual(10, TravelCalculator.MinutesFor(800, Transports.Walk));
            Assert.AreEqual(11, TravelCalculator.MinutesFor(801, Transports.Walk));
            Assert.AreEqual(1, TravelCalculator.MinutesFor(1, Transports.Car));
            Assert.AreEqual(0, TravelCalculator.MinutesFor(0, Transports.Walk));
        }

        [Test]
        public void FasterTransport_TakesLess()
        {
            int walk = TravelCalculator.MinutesFor(2000, Transports.Walk);
            int ebike = TravelCalculator.MinutesFor(2000, Transports.EBike);
            int car = TravelCalculator.MinutesFor(2000, Transports.Car);
            Assert.Greater(walk, ebike);
            Assert.Greater(ebike, car);
        }

        [Test]
        public void TimeFactor_SlowsTravel()
        {
            Assert.AreEqual(10, TravelCalculator.MinutesFor(800, Transports.Walk, 1.0));
            Assert.AreEqual(15, TravelCalculator.MinutesFor(800, Transports.Walk, 1.5));
        }

        [Test]
        public void Plan_DoesNotAdvanceClock()
        {
            var map = Street();
            var clock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            TravelPlan plan = TravelPlanner.Plan(map, "home", "mall", Transports.Walk);
            Assert.IsTrue(plan.Found);
            Assert.AreEqual(10, plan.Minutes);
            Assert.AreEqual(800.0, plan.Distance, 1e-9);
            Assert.AreEqual(8 * 60, clock.MinuteOfDay);
        }

        [Test]
        public void Travel_AdvancesWorldClock()
        {
            var map = Street();
            var clock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            TravelResult r = TravelPlanner.Travel(map, "home", "mall", Transports.Walk, clock);
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(8 * 60 + 10, clock.MinuteOfDay);
        }

        [Test]
        public void Travel_RaisesTimeAdvanced_SoShopsKeepRunningWhileWalking()
        {
            var map = Street();
            var clock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            long advanced = 0;
            clock.TimeAdvanced += (from, to) => advanced += from.MinutesUntil(to);

            TravelPlanner.Travel(map, "home", "far", Transports.Walk, clock);

            Assert.AreEqual(35, advanced);
        }

        [Test]
        public void Travel_NoPath_DoesNotAdvanceClock()
        {
            var map = Street();
            map.AddNode("island", 9999, 9999);
            var clock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            TravelResult r = TravelPlanner.Travel(map, "home", "island", Transports.Walk, clock);
            Assert.AreEqual(TravelStatus.NoPath, r.Status);
            Assert.AreEqual(8 * 60, clock.MinuteOfDay);
        }

        [Test]
        public void CarSavesWorldTime()
        {
            // 体现"车的价值"：同一段 2800 米，步行要 35 分钟，汽车只要 6 分钟。
            var map = Street();
            var walkClock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            var carClock = new GameClock(GameDate.FromYmd(1, 1, 1), 8 * 60);
            TravelPlanner.Travel(map, "home", "far", Transports.Walk, walkClock);
            TravelPlanner.Travel(map, "home", "far", Transports.Car, carClock);
            Assert.AreEqual(8 * 60 + 35, walkClock.MinuteOfDay);
            Assert.AreEqual(8 * 60 + 6, carClock.MinuteOfDay);
        }
    }

    public class PathWalkerTests
    {
        private static WorldMap L()
        {
            // a(0,0) -- b(100,0) -- c(100,50)
            var map = new WorldMap();
            map.AddNode("a", 0, 0);
            map.AddNode("b", 100, 0);
            map.AddNode("c", 100, 50);
            map.AddEdge("a", "b");
            map.AddEdge("b", "c");
            return map;
        }

        [Test]
        public void TotalLength_IsSumOfEdges()
        {
            var w = new PathWalker(L(), new[] { "a", "b", "c" });
            Assert.AreEqual(150.0, w.TotalLength, 1e-9);
        }

        [Test]
        public void PositionAt_InterpolatesAlongSegments()
        {
            var w = new PathWalker(L(), new[] { "a", "b", "c" });

            MapPoint p0 = w.PositionAt(0);
            Assert.AreEqual(0.0, p0.X, 1e-9);
            Assert.AreEqual(0.0, p0.Y, 1e-9);

            MapPoint mid1 = w.PositionAt(50);
            Assert.AreEqual(50.0, mid1.X, 1e-9);
            Assert.AreEqual(0.0, mid1.Y, 1e-9);

            MapPoint onSecond = w.PositionAt(125);
            Assert.AreEqual(100.0, onSecond.X, 1e-9);
            Assert.AreEqual(25.0, onSecond.Y, 1e-9);
        }

        [Test]
        public void PositionAt_ClampsToEnds()
        {
            var w = new PathWalker(L(), new[] { "a", "b", "c" });
            MapPoint before = w.PositionAt(-10);
            MapPoint after = w.PositionAt(9999);
            Assert.AreEqual(0.0, before.X, 1e-9);
            Assert.AreEqual(100.0, after.X, 1e-9);
            Assert.AreEqual(50.0, after.Y, 1e-9);
        }

        [Test]
        public void SingleNodePath_StaysPut()
        {
            var w = new PathWalker(L(), new[] { "b" });
            Assert.AreEqual(0.0, w.TotalLength, 1e-9);
            MapPoint p = w.PositionAt(50);
            Assert.AreEqual(100.0, p.X, 1e-9);
        }

        [Test]
        public void NonAdjacentNodes_Throw()
        {
            Assert.Throws<ArgumentException>(() => new PathWalker(L(), new[] { "a", "c" }));
        }
    }
}