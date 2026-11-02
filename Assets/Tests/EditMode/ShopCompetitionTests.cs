using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class ShopCompetitionTests
    {
        private static GameSession Make(double scale = 1, double entranceOffset = 0)
        {
            var data = GameSessionTests.Layout(); data.distanceScale = scale;
            var places = data.places.ToList(); var nodes = data.nodes.ToList(); var edges = data.edges.ToList();
            places.Single(p => p.id == "pc").explicitEntrance = true;
            places.Single(p => p.id == "pc").entranceX = entranceOffset;
            foreach (var entry in new[] { ("a", 30.0, 0.0), ("b", 40.0, 0.0), ("c", 50.0, 0.0), ("edge", 60.0, 0.0), ("outside", 60.01, 0.0), ("diagonal", 50.0, 30.0), ("chain", 80.0, 0.0) })
            {
                nodes.Add(new LayoutNodeData { id = entry.Item1, x = entry.Item2, y = entry.Item3 });
                edges.Add(new LayoutEdgeData { a = "pc", b = entry.Item1 });
                places.Add(new LayoutPlaceData { id = entry.Item1, node = entry.Item1, name = entry.Item1, kind = "shop", size = "small", explicitEntrance = true });
            }
            data.places = places.ToArray(); data.nodes = nodes.ToArray(); data.edges = edges.ToArray();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(5000000), GameTimestamp.From(new GameDate(15), 600), startWithShop: true);
            s.EventsEnabled = false; s.PlayerShop.Reputation = 100;
            return s;
        }

        private static void Rent(GameSession s, string id, string kind = "computer", int day = 15)
        {
            s.DebugAddListing(id, day, 60, Money.FromYuan(1000));
            GameSessionTests.Teleport(s, "agency");
            Assert.True(s.SignRetailLease(id, true, kind), id);
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EveryNearbyShopGetsExactlyOneNthOfItsOwnUnsharedArrivalRate(int count)
        {
            using (var s = Make())
            {
                foreach (string id in new[] { "a", "b", "c" }.Take(count - 1)) Rent(s, id);
                foreach (var shop in s.MyShops)
                {
                    Assert.AreEqual(count, s.ShopCompetitionCount(shop.Place));
                    var callback = shop.Sim.CompetitionFactorOf;
                    shop.Sim.CompetitionFactorOf = null;
                    double unshared = shop.Sim.ArrivalRatePerMinute(s.Clock.Now);
                    shop.Sim.CompetitionFactorOf = callback;
                    Assert.AreEqual(unshared / count, shop.Sim.ArrivalRatePerMinute(s.Clock.Now), 1e-10);
                    Assert.AreEqual(s.FootfallOf(shop.Place).DailyPassers / (double)count, s.CompetitionDailyPassers(shop.Place), 1e-10);
                }
            }
        }

        [Test]
        public void CircularDistanceIncludesBoundaryButExcludesOutsideAndSquareCorners()
        {
            using (var s = Make())
            {
                Rent(s, "edge"); Rent(s, "outside"); Rent(s, "diagonal");
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                CollectionAssert.AreEquivalent(new[] { "edge" }, s.NearbyCompetingShops(s.MainShop.Place).Select(x => x.Place.Id));
            }
        }

        [Test]
        public void EachShopUsesItsOwnCircle_NotATransitiveChainAcrossTheCity()
        {
            using (var s = Make())
            {
                Rent(s, "c"); Rent(s, "chain");
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                Assert.AreEqual(3, s.ShopCompetitionCount(s.ShopAt("c").Place));
                Assert.AreEqual(2, s.ShopCompetitionCount(s.ShopAt("chain").Place));
            }
        }

        [Test]
        public void RadiusUsesGameMetersAndEntranceOffsets()
        {
            using (var s = Make(2))
            {
                Rent(s, "b"); Rent(s, "c");
                Assert.AreEqual(20, s.ShopCompetitionRadiusWorld);
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                Assert.AreEqual(s.DoorOf(s.MainShop.Place).X, 20);
            }
            using (var s = Make(1, 5))
            {
                Rent(s, "outside");
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place), "以真实入口为圆心而非道路节点");
            }
        }

        [Test]
        public void RangeGraphicCreatesRendererAndClosedNonBlockingBoundary()
        {
            var root = new GameObject("CompetitionGraphicTest", typeof(RectTransform));
            try
            {
                var graphic = root.AddComponent<CompetitionRangeGraphic>();
                graphic.Initialize(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) });
                Assert.NotNull(root.GetComponent<CanvasRenderer>()); Assert.False(graphic.raycastTarget);
                using (var mesh = new VertexHelper())
                {
                    typeof(CompetitionRangeGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
                    Assert.AreEqual(16, mesh.currentVertCount); Assert.AreEqual(24, mesh.currentIndexCount);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CategoryChangeAndClosingUpdateBothShopsWithoutAClockTick()
        {
            using (var s = Make())
            {
                Rent(s, "a"); Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                GameSessionTests.Teleport(s, "a"); Assert.True(s.SetShopMainCategory("models"));
                Assert.AreEqual(1, s.ShopCompetitionCount(s.MainShop.Place));
                Assert.AreEqual(1, s.ShopCompetitionCount(s.CurrentShop.Place));
                Assert.True(s.SetShopMainCategory("computer"));
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                s.PlayerShop.ForcedClosed = true;
                Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place), "关门不能规避分流");
                Assert.True(s.CloseShop("a"));
                Assert.AreEqual(1, s.ShopCompetitionCount(s.MainShop.Place));
            }
        }

        [Test]
        public void VacantAndPendingUnitsDoNotCompete_ButPreviewIncludesProposedShop()
        {
            using (var s = Make())
            {
                Assert.AreEqual(1, s.ShopCompetitionCount(s.MainShop.Place));
                Assert.AreEqual(2, s.ShopCompetitionCount(s.City.GetPlace("a"), "computer"));
                Assert.AreEqual(1, s.ShopCompetitionCount(s.City.GetPlace("a"), "models"));
                Rent(s, "a", day: 18);
                Assert.NotNull(s.PendingLease);
                Assert.AreEqual(1, s.ShopCompetitionCount(s.MainShop.Place));
            }
        }

        [Test]
        public void MovingOneShopBeyondRadiusImmediatelyRestoresOtherShopTraffic()
        {
            using (var s = Make())
            {
                Rent(s, "a"); Assert.AreEqual(2, s.ShopCompetitionCount(s.MainShop.Place));
                s.DebugAddListing("outside", 15, 60, Money.FromYuan(1000));
                GameSessionTests.Teleport(s, "agency");
                Assert.True(s.SignRetailLease("outside", false, "computer"));
                Assert.AreEqual(2, s.MyShopCount);
                Assert.AreEqual(1, s.ShopCompetitionCount(s.MainShop.Place));
                Assert.AreEqual(1, s.MainShop.Sim.CompetitionFactorOf());
            }
        }

        [TestCase("restaurant", 12)] [TestCase("secondhand", 45)]
        public void RestaurantAndUsedShopVisitsAreSlowedByTheSameCompetitionRule(string kind, int minutes)
        {
            using (var s = Make())
            {
                GameSessionTests.Teleport(s, "pc"); Assert.True(s.SetShopMainCategory(kind));
                Rent(s, "a", kind); Rent(s, "b", kind);
                var method = typeof(GameSession).GetMethod("SpecialVisitInterval", BindingFlags.Instance | BindingFlags.NonPublic);
                long expected = Math.Max(1, (long)Math.Round(minutes * 300.0 * 3 / s.FootfallOf(s.MainShop.Place).DailyPassers));
                Assert.AreEqual(expected, method.Invoke(s, new object[] { s.MainShop, minutes }));
            }
        }

        [Test]
        public void JsonReloadRestoresCompetition_WhileLocationEstimateAndRentStayUnchanged()
        {
            using (var s = Make())
            {
                var original = s.FootfallOf(s.MainShop.Place); var quote = s.ShopRentQuote(s.MainShop.Place);
                Rent(s, "a"); Rent(s, "b");
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.AreEqual(3, loaded.ShopCompetitionCount(loaded.MainShop.Place));
                    Assert.AreEqual(1.0 / 3, loaded.MainShop.Sim.CompetitionFactorOf(), 1e-10);
                    Assert.AreEqual(original.DailyPassers, loaded.FootfallOf(loaded.MainShop.Place).DailyPassers);
                    Assert.AreEqual(quote, loaded.ShopRentQuote(loaded.MainShop.Place));
                }
                Assert.AreSame(original, s.FootfallOf(s.MainShop.Place));
            }
        }

        [Test]
        public void ShopWindowExplainsRealCountAndHasCounterEntry()
        {
            using (var s = Make())
            {
                Rent(s, "a"); GameSessionTests.Teleport(s, "pc");
                var root = new GameObject("CompetitionUiTest"); root.SetActive(false);
                var controller = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller, s);
                typeof(GameController).GetProperty("City").SetValue(controller, s.City);
                var previous = UnityEngine.EventSystems.EventSystem.current;
                var ui = new GameUi(controller);
                try
                {
                    ui.OpenDock(); ui.DebugSelectTab(0);
                    Assert.True(ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).Any(b => b.name == "ShopCompetitionEntry"));
                    ui.OpenShopCompetition(s.MainShop.Place);
                    string text = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Single(t => t.name == "ShopCompetitionSummary").text;
                    StringAssert.Contains("2 家", text); StringAssert.Contains("1/2", text);
                    StringAssert.Contains(s.CompetitionDailyPassers(s.MainShop.Place).ToString("N0"), text);
                }
                finally
                {
                    ui.CloseAllWindows(); foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if (previous == null && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
