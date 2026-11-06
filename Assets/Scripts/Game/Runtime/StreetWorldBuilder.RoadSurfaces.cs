using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        sealed class StreetSegment
        {
            public Vector2 A,B;public float Width;public bool Raised;
        }
        sealed class SurfaceMesh
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
            public void Add(IEnumerable<Vector2[]> polygons,float y)
            {
                foreach(var p in polygons)
                {
                    if(RoadSurfaceGeometry.Area(p)<.0001f)continue;
                    int offset=vertices.Count;
                    foreach(var v in p){vertices.Add(new Vector3(v.x,y,v.y));uv.Add(v/4);}
                    for(int i=1;i+1<p.Length;i++){triangles.Add(offset);triangles.Add(offset+i+1);triangles.Add(offset+i);}
                }
            }
            public void Build(Transform root,string name,Material material,CityLandscapeResources owner)
            {
                if(vertices.Count==0)return;
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.Owned.Add(mesh);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        private void BuildContinuousStreetSurfaces()
        {
            var root=new GameObject("ContinuousStreetSurfaces").transform;root.SetParent(_root,false);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var segments=new List<StreetSegment>();var seen=new HashSet<string>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
            {
                string key=string.CompareOrdinal(n.Id,e.To)<0?n.Id+"|"+e.To:e.To+"|"+n.Id;if(!seen.Add(key))continue;
                var a=NodeWorld(n.Id);var b=NodeWorld(e.To);if((b-a).sqrMagnitude<.01f)continue;
                segments.Add(new StreetSegment{A=new Vector2(a.x,a.z),B=new Vector2(b.x,b.z),Width=PlannedRoadWidth(a,b),
                    Raised=n.Id.StartsWith("race_")||e.To.StartsWith("race_")||n.Id.StartsWith("motor_")||e.To.StartsWith("motor_")});
            }
            // A raised access road owns its junction; the ground road terminates at that boundary.
            segments=segments.OrderByDescending(s=>s.Raised).ThenByDescending(s=>(s.B-s.A).sqrMagnitude).ToList();
            var roadIndex=new RoadSurfaceGeometry.Index();var pavingIndex=new RoadSurfaceGeometry.Index();
            var roads=new SurfaceMesh();var paving=new SurfaceMesh();var white=new SurfaceMesh();var yellow=new SurfaceMesh();
            foreach(var s in segments)
            {
                var p=RoadSurfaceGeometry.Road(s.A,s.B,s.Width);
                roads.Add(roadIndex.Cut(new List<Vector2[]>{p},p),s.Raised?.25f:.025f);roadIndex.Add(p);
            }
            void Pavement(Vector2[] p,float y)
            {
                var parts=roadIndex.Cut(new List<Vector2[]>{p},p);parts=pavingIndex.Cut(parts,p);paving.Add(parts,y);pavingIndex.Add(p);
            }
            foreach(var s in segments)Pavement(RoadSurfaceGeometry.Road(s.A,s.B,s.Width+(float)GameSession.CityPavingWidth*2),s.Raised?.25f:.025f);
            foreach(var p in _city.Places)
            {
                var a=NodeWorld(p.NodeId);var b=a+new Vector3((float)p.EntranceX,0,(float)p.EntranceY);
                if((a-b).sqrMagnitude>.01f)Pavement(RoadSurfaceGeometry.Strip(new Vector2(a.x,a.z),new Vector2(b.x,b.z),2),.025f);
            }
            var markIndex=new RoadSurfaceGeometry.Index();
            for(int i=0;i<segments.Count;i++)
            {
                var s=segments[i];var delta=s.B-s.A;float length=delta.magnitude;if(length<.1f)continue;
                var d=delta/length;var normal=new Vector2(-d.y,d.x);float inset=s.Width*.5f+.4f;
                void Mark(Vector2[] p,SurfaceMesh buffer)
                {
                    var parts=roadIndex.Cut(new List<Vector2[]>{p},p,id=>id!=i&&Mathf.Abs(Vector2.Dot((segments[id].B-segments[id].A).normalized,d))<.85f);
                    parts=markIndex.Cut(parts,p);buffer.Add(parts,s.Raised?.265f:.04f);markIndex.Add(p);
                }
                foreach(int sign in new[]{-1,1})
                {
                    var offset=normal*sign*(s.Width*.5f-.35f);
                    Mark(RoadSurfaceGeometry.Strip(s.A+offset,s.B+offset,.08f),white);
                }
                float projection=Vector2.Dot(s.A,d);
                for(float at=Mathf.Floor(projection/5)*5-projection;at<length;at+=5)
                {
                    float start=Mathf.Max(0,at),end=Mathf.Min(at+2,length);
                    if(end>start+.05f)Mark(RoadSurfaceGeometry.Strip(s.A+d*start,s.A+d*end,.12f),yellow);
                }
                bool Junction(Vector2 point)
                {
                    var directions=new List<Vector2>();
                    var probe=RoadSurfaceGeometry.Strip(point-Vector2.right*2,point+Vector2.right*2,4);
                    foreach(int id in roadIndex.Nearby(probe))
                    {
                        var other=segments[id];if((other.B-other.A).sqrMagnitude<4)continue;
                        Vector2 direction;
                        if((other.A-point).sqrMagnitude<9)direction=(other.B-point).normalized;
                        else if((other.B-point).sqrMagnitude<9)direction=(other.A-point).normalized;
                        else continue;
                        if(!directions.Any(v=>Vector2.Dot(v,direction)>.94f))directions.Add(direction);
                    }
                    return directions.Count>=3;
                }
                if(length>18)foreach(bool end in new[]{false,true})
                {
                    var point=end?s.B:s.A;var forward=end?-d:d;if(!Junction(point))continue;
                    for(float across=-s.Width*.5f+.65f;across<s.Width*.5f-.4f;across+=.7f)
                    {
                        var start=point+forward*(inset+1)+normal*across;
                        Mark(RoadSurfaceGeometry.Strip(start,start+forward*1.7f,.4f),white);
                    }
                }
            }
            roads.Build(root,"RoadSurface",IndustrialMaterials.Surface("asphalt",Asphalt,Vector2.one),resources);
            paving.Build(root,"PavementSurface",Mats.Lit(Sidewalk),resources);
            white.Build(root,"RoadBoundaryLines",Mats.Lit(ArtDirection.Cream),resources);
            yellow.Build(root,"LaneMarkings",Mats.Lit(ArtDirection.Brass),resources);
        }
    }
}
