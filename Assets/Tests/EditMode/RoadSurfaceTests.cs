using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Game.Runtime;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public sealed class RoadSurfaceTests
    {
        static float Area(IEnumerable<Vector2[]> p)=>p.Sum(x=>RoadSurfaceGeometry.Area(x));
        [TestCase(0f,0f)] [TestCase(650f,705f)]
        public void JunctionOwnsAsphaltOnceAndPavementNeverCrossesIt(float x,float y)
        {
            var at=new Vector2(x,y);var road=RoadSurfaceGeometry.Strip(at-Vector2.right*10,at+Vector2.right*10,6);
            var crossing=RoadSurfaceGeometry.Strip(at-Vector2.up*10,at+Vector2.up*10,6);
            var index=new RoadSurfaceGeometry.Index();index.Add(road);
            var accepted=index.Cut(new List<Vector2[]>{crossing},crossing);index.Add(crossing);
            Assert.AreEqual(204,RoadSurfaceGeometry.Area(road)+Area(accepted),.02);
            var outside=RoadSurfaceGeometry.Strip(at-Vector2.right*10,at+Vector2.right*10,8);
            var paving=index.Cut(new List<Vector2[]>{outside},outside);
            Assert.AreEqual(28,Area(paving),.02);
            foreach(var piece in paving)foreach(var asphalt in new[]{road,crossing})
                Assert.AreEqual(RoadSurfaceGeometry.Area(piece),Area(RoadSurfaceGeometry.Subtract(piece,asphalt)),.02);
        }
        [Test] public void IdenticalAndReverseGeometryDoesNotCreateASecondSurface()
        {
            var a=new Vector2(-500,-400);var b=new Vector2(-470,-380);
            var road=RoadSurfaceGeometry.Road(a,b,7);var same=RoadSurfaceGeometry.Road(b,a,7);
            var index=new RoadSurfaceGeometry.Index();index.Add(road);
            Assert.Less(Area(index.Cut(new List<Vector2[]>{same},same)),.01);
        }
        [Test] public void AcuteBendAndDoorApproachAreClippedWithoutRemovingRemainingWalkway()
        {
            var index=new RoadSurfaceGeometry.Index();
            var a=RoadSurfaceGeometry.Road(new Vector2(-20,0),Vector2.zero,7);index.Add(a);
            var b=RoadSurfaceGeometry.Road(Vector2.zero,new Vector2(20,8),7);
            var added=index.Cut(new List<Vector2[]>{b},b);index.Add(b);
            Assert.Greater(Area(added),70);Assert.Less(Area(added),RoadSurfaceGeometry.Area(b));
            var approach=RoadSurfaceGeometry.Strip(new Vector2(-10,0),new Vector2(-10,10),2);
            var path=index.Cut(new List<Vector2[]>{approach},approach);
            Assert.AreEqual(13,Area(path),.01);
        }
        [Test] public void AdjacentPavingPiecesShareNoArea()
        {
            var first=RoadSurfaceGeometry.Road(new Vector2(0,0),new Vector2(12,0),9.8f);
            var second=RoadSurfaceGeometry.Road(new Vector2(12,0),new Vector2(22,5),9.8f);
            var index=new RoadSurfaceGeometry.Index();index.Add(first);
            var parts=index.Cut(new List<Vector2[]>{second},second);
            foreach(var piece in parts)Assert.AreEqual(RoadSurfaceGeometry.Area(piece),Area(RoadSurfaceGeometry.Subtract(piece,first)),.01);
        }
        [Test] public void ActualCityRoadCentersStayCoveredAndPavementDoesNotCoverThem()
        {
            var data=JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text);
            var city=CityLayout.FromData(data);var builder=new StreetWorldBuilder(city,12345);
            var root=new GameObject("RoadSurfaceTest");
            try
            {
                typeof(StreetWorldBuilder).GetField("_root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(builder,root.transform);
                var timer=System.Diagnostics.Stopwatch.StartNew();
                var build = (IEnumerator)typeof(StreetWorldBuilder)
                    .GetMethod("BuildContinuousStreetSurfaces", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(builder, null);
                // The nested verge routine plants scenery after the road meshes have been built.
                while (build.MoveNext()) { }
                timer.Stop();
                var filters = root.transform.Find("ContinuousStreetSurfaces").GetComponentsInChildren<MeshFilter>();
                Assert.Less(filters.Sum(f=>f.sharedMesh.vertexCount),400000,"Merged streets must not explode into excessive geometry");
                Debug.Log("ROAD_SURFACE_BUILD ms="+timer.ElapsedMilliseconds+" vertices="+filters.Sum(f=>f.sharedMesh.vertexCount));
                var quarterRoad = Surface("Old quarter asphalt");
                var townRoad = Surface("Town asphalt");
                var quarterWalk = Surface("Old quarter paving");
                var townWalk = Surface("Town flagstones");
                Assert.That(filters.Select(f => f.name), Has.None.EqualTo("RoadSurface").And.None.EqualTo("PavementSurface"));

                foreach (var edge in data.edges.Where((_, index) => index % 17 == 0))
                {
                    Vector2 center = Vector2.Lerp(Point(edge.a), Point(edge.b), .371f);
                    Assert.True(On(quarterRoad, center) || On(townRoad, center), edge.a + " road missing");
                    Assert.False(On(quarterWalk, center) || On(townWalk, center), edge.a + " paving inside carriageway");
                }

                // Opposite sides of the old colour break between the farm market and the hardware shop.
                AssertQuarterFinish("lake_old_back_28", "lake_old_back_29");
                AssertQuarterFinish("lake_old_back_32", "lake_old_back_33");

                (Vector3[] vertices, int[] triangles) Surface(string name)
                {
                    Mesh mesh = filters.Single(f => f.name == name).sharedMesh;
                    return (mesh.vertices, mesh.triangles);
                }
                Vector2 Point(string node)
                {
                    var point = city.ViewPosition(node);
                    return new Vector2((float)point.X, (float)point.Y);
                }
                bool On((Vector3[] vertices, int[] triangles) mesh, Vector2 point) =>
                    Covered(mesh.vertices, mesh.triangles, point);
                void AssertQuarterFinish(string first, string last)
                {
                    Vector2 a = Point(first), b = Point(last), center = (a + b) * .5f;
                    Vector2 direction = (b - a).normalized;
                    float width = (float)GameSession.CityRoadWidth(city.ViewPosition(first), city.ViewPosition(last));
                    var pavingPoint = center + new Vector2(-direction.y, direction.x) * (width * .5f + .7f);
                    Assert.True(On(quarterRoad, center), first + " outskirts asphalt");
                    Assert.True(On(quarterWalk, pavingPoint), first + " outskirts paving");
                }
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void AmbientWalkersOnWideRoadUseSidewalksAndKeepSpaceWhenReentering()
        {
            var layout = GameSessionTests.Layout();
            layout.layoutVersion = 7;
            layout.nodes = layout.nodes.Concat(new[]
            {
                new LayoutNodeData { id = "wide_a", x = -27, y = 135 },
                new LayoutNodeData { id = "wide_b", x = 27, y = 135 },
            }).ToArray();
            layout.edges = layout.edges.Append(new LayoutEdgeData { a = "wide_a", b = "wide_b" }).ToArray();
            using var session = new GameSession(CityLayout.FromData(layout), 7, Money.FromYuan(5000),
                GameTimestamp.From(new GameDate(0), 480));
            var root = new GameObject("FootTraffic");
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 12;
                camera.aspect = 2;
                camera.transform.SetPositionAndRotation(new Vector3(0, 30, 135), Quaternion.Euler(90, 0, 0));
                var actors = new GameObject("Actors").transform;
                actors.SetParent(root.transform, false);
                var crowd = new CrowdView(actors, -100, 100, Vector3.zero);

                AmbientPersonMarker[] Sync()
                {
                    crowd.SyncCity(session, camera.transform.position, camera);
                    crowd.Update(0, true);
                    return actors.GetComponentsInChildren<AmbientPersonMarker>();
                }

                var initial = Sync();
                Assert.GreaterOrEqual(initial.Length, 4);
                foreach (var walker in initial)
                {
                    Assert.That(Mathf.Abs(walker.transform.position.z - 135), Is.InRange(4.1f, 5.3f));
                    walker.transform.position += Vector3.right * 100;
                }

                // Several recycled figures try to enter on the same frame. Occupied
                // entrances must wait for room rather than spawning on top of one another.
                var entering = Sync();
                Assert.AreEqual(2, entering.Length);
                Assert.Greater(Vector3.Distance(entering[0].transform.position, entering[1].transform.position), .8f);
                crowd.Update(.5f, true);
                Assert.Greater(Sync().Length, entering.Length, "Waiting walkers can enter once the sidewalk clears.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static bool Covered(Vector3[] v,int[] t,Vector2 p)
        {
            for(int i=0;i<t.Length;i+=3)
            {
                Vector2 a=new Vector2(v[t[i]].x,v[t[i]].z),b=new Vector2(v[t[i+1]].x,v[t[i+1]].z),c=new Vector2(v[t[i+2]].x,v[t[i+2]].z);
                double Cross(Vector2 u,Vector2 w)=>(double)u.x*w.y-(double)u.y*w.x;
                if(System.Math.Abs(Cross(b-a,c-a))<.00001)continue;
                double x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);
                if(x<=.00001&&y<=.00001&&z<=.00001||x>=-.00001&&y>=-.00001&&z>=-.00001)return true;
            }
            return false;
        }
    }
}
