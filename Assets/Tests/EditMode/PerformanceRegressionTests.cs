using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class PerformanceRegressionTests
    {
        [TestCase(0, false)] [TestCase(0, true)] [TestCase(3, false)] [TestCase(3, true)]
        public void ForestCrownsHaveTexturedLeafLayersInsteadOfExposedSolidGreenPanels(int species, bool detailed)
        {
            var woodland = typeof(ShopInteriorView).Assembly.GetType("PCShopSell.Game.Runtime.PaintedWoodland");
            var template = woodland.GetMethod("Template", BindingFlags.Static | BindingFlags.NonPublic);
            for (int aspect = 0; aspect < 6; aspect++)
            {
                var mesh = (Mesh)template.Invoke(null, new object[] { 6, species, aspect, detailed, false, true });
                var uv = mesh.uv;
                var offsets = new List<Vector2>();
                mesh.GetUVs(2, offsets);
                Assert.AreEqual(mesh.vertexCount, offsets.Count, "所有顶点都需要明确的树皮或叶簇朝向信息");
                int leaves = 0;
                for (int i = 0; i < uv.Length; i++)
                {
                    if (offsets[i].sqrMagnitude > .0001f)
                    {
                        leaves++;
                        Assert.Greater(uv[i].y, .10f, "叶簇必须采样有透明轮廓的叶片区");
                    }
                    else Assert.Less(uv[i].x, .50f, "固定几何只能取树皮区，不能再用纯绿条生成实心树冠壳");
                }
                Assert.GreaterOrEqual(leaves, 24, "去除实心壳后中心仍需有叶簇填充，不能留下空冠");
            }
        }

        [Test]
        public void SoundPrewarmPreparesAllClipsWithoutPlaybackOrPreferenceChanges()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var clips = (AudioClip[])typeof(SoundFx).GetField("Clips", flags).GetValue(null);
            var original = (AudioClip[])clips.Clone();
            var cooldowns = (float[])typeof(SoundFx).GetField("NextPlayTime", flags).GetValue(null);
            var originalCooldowns = (float[])cooldowns.Clone();
            var sourceField = typeof(SoundFx).GetField("_source", flags);
            var source = sourceField.GetValue(null);
            bool muted = SoundFx.Muted;
            float volume = SoundFx.Volume;
            Array.Clear(clips, 0, clips.Length);
            try
            {
                var warm = SoundFx.Prewarm();
                int steps = 0;
                while (warm.MoveNext()) steps++;
                Assert.AreEqual(Enum.GetValues(typeof(Sfx)).Length, steps, "每帧最多准备一种音效");
                Assert.True(clips.All(c => c != null && c.samples > 0 && c.channels == 1));
                var prepared = (AudioClip[])clips.Clone();
                Assert.False(SoundFx.Prewarm().MoveNext(), "重复加载必须复用已有音效");
                CollectionAssert.AreEqual(prepared, clips);
                CollectionAssert.AreEqual(originalCooldowns, cooldowns);
                Assert.AreSame(source, sourceField.GetValue(null), "预热不能创建播放源或开始播放");
                Assert.AreEqual(muted, SoundFx.Muted);
                Assert.AreEqual(volume, SoundFx.Volume);
            }
            finally
            {
                foreach (var clip in clips) if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                Array.Copy(original, clips, clips.Length);
            }
        }

        [Test]
        public void IndexedPathSearchMatchesOriginalTieBreakingAndDistances()
        {
            var map = new WorldMap();
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) map.AddNode($"{x}:{y}", x, y);
            for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
            {
                if (x < 6) map.AddEdge($"{x}:{y}", $"{x+1}:{y}", (x+y)%3 == 0 ? 2 : 1);
                if (y < 6) map.AddEdge($"{x}:{y}", $"{x}:{y+1}");
            }
            map.AddNode("isolated", 100, 100);
            PathResult Reference(string from, string to)
            {
                var distances = new Dictionary<string, double> { [from] = 0 };
                var parents = new Dictionary<string, string>(); var closed = new HashSet<string>();
                var open = new SortedSet<(double f, long order, string id)>(); long order = 0;
                open.Add((map.StraightDistance(from, to), order++, from));
                while (open.Count > 0)
                {
                    var head = open.Min; open.Remove(head); string current = head.id;
                    if (!closed.Add(current)) continue;
                    if (current == to)
                    {
                        var path = new List<string> { current };
                        while (parents.TryGetValue(current, out var previous)) { path.Add(previous); current = previous; }
                        path.Reverse(); return PathResult.Success(path, distances[to]);
                    }
                    foreach (var edge in map.NeighborsOf(current))
                    {
                        if (closed.Contains(edge.To)) continue;
                        double candidate = distances[current] + edge.Length;
                        if (distances.TryGetValue(edge.To, out var known) && candidate >= known) continue;
                        distances[edge.To] = candidate; parents[edge.To] = current;
                        open.Add((candidate + map.StraightDistance(edge.To, to), order++, edge.To));
                    }
                }
                return PathResult.NotFound;
            }
            foreach (var from in map.Nodes) foreach (var to in map.Nodes)
            {
                var expected = Reference(from.Id, to.Id); var actual = Pathfinder.FindPath(map, from.Id, to.Id);
                Assert.AreEqual(expected.Found, actual.Found); Assert.AreEqual(expected.Distance, actual.Distance);
                CollectionAssert.AreEqual(expected.NodeIds, actual.NodeIds, from.Id + " -> " + to.Id);
            }
        }

        [Test]
        public void ReloadReusesOnlyGeographicFootfallAndKeepsSaveAndRentUnchanged()
        {
            using (var first = GameSessionTests.Make())
            using (var loaded = GameSession.Load(first.City, first.CreateSave()))
            {
                Assert.AreSame(first.FootfallOf(first.PlayerShopPlace), loaded.FootfallOf(loaded.PlayerShopPlace));
                Assert.AreEqual(first.ShopRentQuote(first.PlayerShopPlace), loaded.ShopRentQuote(loaded.PlayerShopPlace));
                var before = loaded.FootfallOf(loaded.PlayerShopPlace);
                first.City.Map.AddNode("footfall-new", 1000, 1000);
                Assert.AreNotSame(before, loaded.FootfallOf(loaded.PlayerShopPlace));
            }
        }

        [Test]
        public void CachedPathsInvalidateOnNewEdgesAndDoNotLeakBetweenMaps()
        {
            var map = new WorldMap();
            map.AddNode("a", 0, 0); map.AddNode("b", 10, 0); map.AddNode("c", 20, 0);
            Assert.False(Pathfinder.FindPath(map, "a", "c").Found);
            map.AddEdge("a", "b", 100); map.AddEdge("b", "c", 100);
            var first = Pathfinder.FindPath(map, "a", "c");
            Assert.AreEqual(200, first.Distance);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, first.NodeIds);
            Assert.AreSame(first, Pathfinder.FindPath(map, "a", "c"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)first.NodeIds)[0] = "broken");
            map.AddEdge("a", "c", 20);
            Assert.AreEqual(20, Pathfinder.FindPath(map, "a", "c").Distance);
            CollectionAssert.AreEqual(new[] { "c", "a" }, Pathfinder.FindPath(map, "c", "a").NodeIds);
            var other = new WorldMap(); other.AddNode("a", 0, 0); other.AddNode("c", 20, 0);
            Assert.False(Pathfinder.FindPath(other, "a", "c").Found);
            Assert.Throws<KeyNotFoundException>(() => Pathfinder.FindPath(map, "a", "missing"));
        }

        [Test]
        public void StreetServiceSpatialIndexKeepsOriginalClearanceAndMeetingNodes()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using (var session = new GameSession(city, 7, PCShopSell.Core.Common.Money.FromYuan(5000),
                PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(1), 600)))
            {
                var method = typeof(GameSession).GetMethod("CommunityStreetNode", BindingFlags.Instance | BindingFlags.NonPublic);
                var doors = city.Places.Select(session.DoorOf).ToArray();
                double Distance(MapPoint a, MapPoint b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
                foreach (var place in city.Places.Where(p => p.Kind == PlaceKind.Agency || p.Business == "venue_expo"))
                {
                    var origin = city.ViewPosition(place.NodeId);
                    string expected = city.Map.Nodes.OrderBy(n => Distance(city.ViewPosition(n.Id), origin))
                        .First(n => doors.All(d => Distance(city.ViewPosition(n.Id), d) >= GameSession.EnterRadius + 6)).Id;
                    Assert.AreEqual(expected, method.Invoke(session, new object[] { place }), place.Id);
                    Assert.AreEqual(expected, method.Invoke(session, new object[] { place }), "重复查询不能改变 NPC 会面位置");
                }
            }
        }

        [Test]
        public void CachedDistrictMembershipPreservesFirstMatchAndUnknownStreets()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            foreach(string street in city.Streets.Select(s=>s.id).Concat(new string[]{null,"missing-street"}))
            {
                var expected=city.AgencyDistricts.FirstOrDefault(d=>Array.IndexOf(d.streets??Array.Empty<string>(),street)>=0);
                Assert.AreSame(expected,city.AgencyDistrictFor(street));
                Assert.AreSame(expected,city.AgencyDistrictFor(street));
            }
            Assert.Throws<KeyNotFoundException>(()=>city.GetPlace(null));
            Assert.Throws<KeyNotFoundException>(()=>city.GetPlace("missing-place"));
        }

        [Test]
        public void CachedNpcWorkplaceFollowsMovingHomeAndChangedDefinition()
        {
            using(var s=GameSessionTests.Make())
            {
                var method=typeof(GameSession).GetMethod("CityNpcWorkplace",BindingFlags.Instance|BindingFlags.NonPublic);
                var definition=new CityPersonDefinition{Id="cache-probe",HomeId="authored-home",WorkId="authored-home"};
                string Work(string home)=>(string)method.Invoke(s,new object[]{definition,home});
                Assert.AreEqual(s.Home.Id,Work(s.Home.Id));
                Assert.AreEqual(s.PlayerShopPlace.Id,Work(s.PlayerShopPlace.Id),"迁移住址不能继续取旧缓存");
                definition.Recruitable=true;Assert.IsNull(Work(s.Home.Id));
                definition.Recruitable=false;definition.WorkId="missing-place";Assert.IsNull(Work(s.Home.Id));
                definition.WorkId="authored-home";Assert.AreEqual(s.Home.Id,Work(s.Home.Id));
            }
        }

        [Test]
        public void CachedNearestNodeMatchesExhaustiveSearchAndInvalidatesAfterMapGrowth()
        {
            var city=CityLayout.FromData(GameSessionTests.Layout());
            string Brute(double x,double y)
            {
                string best=null;double distance=double.MaxValue;
                foreach(var n in city.Map.Nodes)
                {
                    double dx=n.X-x*city.DistanceScale,dy=n.Y-y*city.DistanceScale,d=dx*dx+dy*dy;
                    if(d<distance){distance=d;best=n.Id;}
                }
                return best;
            }
            var random=new System.Random(7);
            for(int i=0;i<4500;i++)
            {
                double x=random.NextDouble()*1000-500,y=random.NextDouble()*1000-500;
                string expected=Brute(x,y);
                Assert.AreEqual(expected,city.NearestNode(x,y));
                Assert.AreEqual(expected,city.NearestNode(x,y));
            }
            city.NearestNode(10000,10000);
            city.Map.AddNode("new-nearest",10000*city.DistanceScale,10000*city.DistanceScale);
            Assert.AreEqual("new-nearest",city.NearestNode(10000,10000));
        }

        [Test]
        public void FurnitureDetectsInPlaceMutationAndRoomResizeButRetainsUnchangedModels()
        {
            var root=new GameObject("FurniturePerformanceRegression");
            var view=new RoomFurniture(root.transform);
            var layout=new List<PlacedFurniture>{new PlacedFurniture{Id="stool",X=2,Z=2}};
            try
            {
                Assert.True(view.Sync(layout,RoomSpec.Home));
                var first=view.Entries[0].Go;
                for(int i=0;i<60;i++)Assert.False(view.Sync(layout,RoomSpec.Home));
                Assert.AreSame(first,view.Entries[0].Go);
                layout[0].X=3;Assert.True(view.Sync(layout,RoomSpec.Home));
                Assert.AreEqual(3,view.Entries[0].Go.transform.localPosition.x,.001);
                Assert.True(view.Sync(layout,RoomSpec.ForShop(ShopSize.Medium)));
                Assert.False(view.Sync(layout,RoomSpec.ForShop(ShopSize.Medium)),"等价的新 RoomSpec 不应导致重建");
                layout.Clear();Assert.True(view.Sync(layout,RoomSpec.Home));Assert.IsEmpty(view.Entries);
                view.Destroy();Assert.True(view.Sync(layout,RoomSpec.Home));
            }
            finally{view.Destroy();UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
