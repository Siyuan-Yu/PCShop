using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Game.Runtime;
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
                typeof(StreetWorldBuilder).GetMethod("BuildContinuousStreetSurfaces",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(builder,null);
                timer.Stop();
                var filters=root.GetComponentsInChildren<MeshFilter>();
                Assert.AreEqual(4,filters.Length);
                Assert.Less(filters.Sum(f=>f.sharedMesh.vertexCount),400000,"Merged streets must not explode into excessive geometry");
                Debug.Log("ROAD_SURFACE_BUILD ms="+timer.ElapsedMilliseconds+" vertices="+filters.Sum(f=>f.sharedMesh.vertexCount));
                var road=filters.Single(f=>f.name=="RoadSurface").sharedMesh;
                var walk=filters.Single(f=>f.name=="PavementSurface").sharedMesh;
                var roadVertices=road.vertices;var roadTriangles=road.triangles;var walkVertices=walk.vertices;var walkTriangles=walk.triangles;
                for(int i=0;i<data.edges.Length;i+=17)
                {
                    var a=city.ViewPosition(data.edges[i].a);var b=city.ViewPosition(data.edges[i].b);
                    var p=new Vector2((float)(a.X+(b.X-a.X)*.371),(float)(a.Y+(b.Y-a.Y)*.371));
                    Assert.True(Covered(roadVertices,roadTriangles,p),data.edges[i].a+" road missing");
                    Assert.False(Covered(walkVertices,walkTriangles,p),data.edges[i].a+" paving inside carriageway");
                }
            }
            finally{Object.DestroyImmediate(root);}
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
