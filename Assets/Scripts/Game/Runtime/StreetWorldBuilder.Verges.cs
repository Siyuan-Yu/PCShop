using System.Collections.Generic;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        // Short individual blades only where turf meets paths, tree pits and garden walls.
        private void BuildQuarterVerges(Transform site)
        {
            var rng = new System.Random(1491);
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            var palette = new Color32[] {new Color32(92,101,47,255),new Color32(122,128,64,255),
                new Color32(156,148,69,255),new Color32(185,164,79,255),new Color32(221,176,78,255)};
            float Range(float a,float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());
            void Stroke(Vector3 a,Vector3 b,Vector3 c,Color32 tint)
            {
                int i=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                colors.Add(tint);colors.Add(tint);colors.Add(tint);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
            }
            void Clump(Vector3 at, bool flowering)
            {
                var world = site.TransformPoint(at);
                var center = new Vector2(world.x, world.z);
                const float reach = .32f;
                var probe = RoadSurfaceGeometry.Strip(center - Vector2.right * reach, center + Vector2.right * reach, reach * 2);
                bool paved = false;
                foreach (int id in _streetPavementEnvelopes.Nearby(probe))
                    if (RoadSurfaceGeometry.Area(RoadSurfaceGeometry.Intersect(probe, _streetPavementEnvelopes.Polygons[id])) > .0001f)
                    {
                        paved = true;
                        break;
                    }
                int stems=rng.Next(5,11);
                for(int k=0;k<stems;k++)
                {
                    float angle=Range(0,Mathf.PI*2);
                    var lean=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var width=new Vector3(-lean.z,0,lean.x)*Range(.024f,.056f);
                    var foot=at+lean*Range(0,.15f);
                    var tip=foot+lean*Range(.055f,.19f)+Vector3.up*Range(.11f,.31f);
                    // Keep the planted verge pattern deterministic when a clump lands on public paving.
                    var tint = palette[rng.Next(4)];
                    if (paved) continue;
                    Stroke(foot-width,foot+width,tip,tint);
                    if(flowering && k%3==0)
                    {
                        var r=Vector3.right*.051f;var f=Vector3.forward*.051f;
                        Stroke(tip-r,tip+f,tip+r,palette[4]);Stroke(tip-r,tip+r,tip-f,palette[4]);
                    }
                }
            }
            foreach(var p in OldQuarterLanes.Paths)
            {
                var a=new Vector3(p.x1,.060f,p.z1);var b=new Vector3(p.x2,.060f,p.z2);
                var direction=(b-a).normalized;var side=new Vector3(-direction.z,0,direction.x);
                for(float run=Range(0,2);run<Vector3.Distance(a,b);run+=Range(2.3f,5.3f))
                {
                    var edge=a+direction*run+side*((rng.Next(2)==0?-1:1)*(p.width*.5f+Range(.12f,.48f)));
                    for(int i=0;i<4;i++) Clump(edge+direction*Range(-.43f,.43f)+side*Range(-.12f,.12f),i==0);
                }
            }
            foreach(var tree in _quarterTrees)
            {
                var at=site.InverseTransformPoint(tree);
                if(at.x < -185 || at.x>60 || at.z < -72 || at.z>140)continue;
                for(int i=0;i<12;i++)
                {
                    float a=Range(0,6.283f),r=Range(.43f,1.14f);
                    Clump(at+new Vector3(Mathf.Cos(a)*r,.07f,Mathf.Sin(a)*r),i%5==0);
                }
            }
            // Scattered groups of pale seed heads carry the transition from the blossom understorey
            // out into the open lawn. Leave the intervening ground quiet.
            foreach(var tree in QuarterBlossoms)
            {
                for(int group=0;group<5;group++)
                {
                    float angle=Range(0,Mathf.PI*2),radius=Range(.95f,2.65f);
                    var center=new Vector3(tree.x+Mathf.Cos(angle)*radius,.063f,tree.z+Mathf.Sin(angle)*radius);
                    for(int tuft=0;tuft<7;tuft++)
                        Clump(center+new Vector3(Range(-.52f,.52f),0,Range(-.28f,.28f)),tuft%3==0);
                }
            }
            var patch=new GameObject("Grass and wildflowers",typeof(MeshFilter),typeof(MeshRenderer));
            patch.transform.SetParent(site,false);
            var mesh=new Mesh {name="Painted garden verges",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.RecalculateBounds();
            patch.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=patch.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=QuarterMaterial("Verge");
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            patch.AddComponent<CityLandscapeResources>().Owned.Add(mesh);
        }
    }
}
