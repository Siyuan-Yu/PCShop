using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private float LakeScale=>_city.LakeSceneScale;
        private Vector3 LakeNode(string id)=>NodeWorld(id)/LakeScale;
        // Physical scenery and navigation share the same layout; closed bridges are scenery only.
        private void BuildLakeLandscape()
        {
            var root=new GameObject("LakeCityLandscape").transform;root.SetParent(_root,false);
            root.localScale=Vector3.one*LakeScale;
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var shore=SmoothShore();
            LakeSurface(root,"StoneShore",shore,1.006f,.12f,Mats.Lit(ArtDirection.Hex("B3BE99")));
            LakeSurface(root,"ShallowWater",shore,1,.18f,Mats.Lit(ArtDirection.Hex("4E9FA9"),.65f));
            var shader=Resources.Load<Shader>("Shaders/LakeWater");
            var water=shader!=null?new Material(shader):Mats.Lit(ArtDirection.Hex("348EAF"),.8f);
            if(shader!=null)resources.Owned.Add(water);
            LakeSurface(root,"CentralLake",shore,.995f,.21f,water);
            BuildMountainGround(root);
            BuildLakeForest(root,shore);
            foreach(var bridge in _city.LakeBridges)BuildClosedLakeBridge(root,bridge);
            // Preserve the neighbourhood fountain in its translated courtyard.
            BuildLakeCourtyards(root);
            BuildShoreFurniture(root,shore);
            BuildLakeIslands(root);
        }

        private List<Vector3> SmoothShore()
        {
            var p=_city.LakeShore.Select(v=>new Vector3((float)v.x,0,(float)v.y)/LakeScale).ToArray();
            var result=new List<Vector3>();
            for(int i=0;i<p.Length;i++)for(int j=0;j<10;j++)
            {
                float t=j/10f;Vector3 a=p[(i+p.Length-1)%p.Length],b=p[i],c=p[(i+1)%p.Length],d=p[(i+2)%p.Length];
                result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            return result;
        }
        private static void LakeSurface(Transform root,string name,List<Vector3> points,float scale,float height,Material material)
        {
            Vector3 center=new Vector3(-25,0,90);var v=new List<Vector3>{new Vector3(center.x,height,center.z)};
            foreach(var p in points)v.Add(center+(p-center)*scale+Vector3.up*height);
            var triangles=new List<int>();
            for(int i=0;i<points.Count;i++){triangles.Add(0);triangles.Add((i+1)%points.Count+1);triangles.Add(i+1);}
            LandscapeMesh(root,name,v,triangles,material);
        }
        private static GameObject LandscapeMesh(Transform parent,string name,List<Vector3> vertices,List<int> triangles,Material material)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.SetUVs(0,vertices.Select(p=>new Vector2(p.x/7,p.z/7)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            var resources=parent.GetComponentInParent<CityLandscapeResources>();if(resources!=null)resources.Owned.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static float MountainHeight(float x,float z)
        {
            // Flat urban terraces stay at gameplay elevation. Mountains frame the western and northern shores.
            float west=Mathf.SmoothStep(0,1,Mathf.Clamp01((-x-740)/150));
            west*=Mathf.SmoothStep(0,1,Mathf.Clamp01((z+250)/120)); // southwest wholesale district remains level
            float north=Mathf.SmoothStep(0,1,Mathf.Clamp01((z-940)/160));
            float east=Mathf.SmoothStep(0,1,Mathf.Clamp01((x-900)/160));
            float envelope=Mathf.Max(west,Mathf.Max(north,east*.65f));
            float ridge=45+180*Mathf.PerlinNoise(x*.005f+19,z*.006f+31)+70*Mathf.PerlinNoise(x*.017f+12,z*.014f+7);
            ridge+=85*Mathf.Pow(Mathf.Max(0,Mathf.Sin(x*.015f+z*.013f)*Mathf.Cos(z*.021f)),3);
            return envelope*ridge;
        }
        private static void BuildMountainGround(Transform root)
        {
            var v=new List<Vector3>();var t=new List<int>();
            const int nx=144,nz=140;const float step=22;
            for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
            {float x=-1500+i*step,z=-1000+j*step;v.Add(new Vector3(x,MountainHeight(x,z)+.001f,z));}
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++)
            {int a=j*(nx+1)+i,b=a+nx+1;t.Add(a);t.Add(b);t.Add(a+1);t.Add(a+1);t.Add(b);t.Add(b+1);}
            LandscapeMesh(root,"ForestedMountainTerrain",v,t,Mats.LitTex(ArtDirection.Hex("7D9F59"),new Vector2(1,1),.12f));
            // Granite outcrops break up the forested silhouette, never inside the road network.
            for(int i=0;i<25;i++)
            {
                float x=-790-(i%4)*37+Mathf.Sin(i*5.7f)*42,z=-70+(i/4)*130+Mathf.Cos(i*3.1f)*35;
                float h=MountainHeight(x,z),size=13+i%3*7;
                var rv=new List<Vector3>();var rt=new List<int>();
                AddCrown(rv,rt,new Vector3(x,h+size*.35f,z),new Vector3(size*.65f,size*1.55f,size*.55f),false);
                AddCrown(rv,rt,new Vector3(x+size*.55f,h+size*.1f,z+4),new Vector3(size*.4f,size,size*.4f),false);
                // Split vertices for crisp granite planes rather than smooth stretched spheres.
                var flat=rt.Select(index=>rv[index]).ToList();
                LandscapeMesh(root,"GraniteOutcrop",flat,Enumerable.Range(0,flat.Count).ToList(),Mats.Lit(ArtDirection.Hex(i%2==0?"A7AA96":"B8BAA1")));
            }
        }
        private static bool WithinLake(Vector3 p,List<Vector3> shore)
        {
            bool inside=false;
            for(int i=0,j=shore.Count-1;i<shore.Count;j=i++)
            {var a=shore[i];var b=shore[j];if((a.z>p.z)!=(b.z>p.z)&&p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x)inside=!inside;}
            return inside;
        }
        private static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));}
        private void BuildLakeForest(Transform root,List<Vector3> shore)
        {
            var occupied=new List<Rect>();
            void Reserve(Vector3 at,float width,float depth,float heading)
            {
                var rot=Quaternion.Euler(0,heading,0);var bounds=new Bounds(at,Vector3.zero);
                foreach(int s in new[]{-1,1})foreach(int back in new[]{0,1})bounds.Encapsulate(at+rot*new Vector3(s*width/2,0,back*depth));
                occupied.Add(Rect.MinMaxRect(bounds.min.x-4,bounds.min.z-4,bounds.max.x+4,bounds.max.z+4));
            }
            foreach(var b in _city.Blocks)Reserve(new Vector3((float)b.x,0,(float)b.y)/LakeScale,(float)b.width/LakeScale,(float)b.depth/LakeScale,(float)b.heading);
            foreach(var p in _city.Places)Reserve(LakeNode(p.NodeId)+new Vector3((float)p.EntranceX,0,(float)p.EntranceY)/LakeScale,(float)p.Width/LakeScale,(float)p.Depth/LakeScale,(float)p.Heading);
            foreach(var p in _city.LakeParcels)Reserve(new Vector3((float)p.x,0,(float)p.y)/LakeScale,(float)p.width/LakeScale,(float)p.depth/LakeScale,(float)p.heading);
            var roads=new List<(Vector3 a,Vector3 b)>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
                if(string.CompareOrdinal(n.Id,e.To)<0)roads.Add((LakeNode(n.Id),LakeNode(e.To)));
            foreach(var bridge in _city.LakeBridges)for(int i=1;i<bridge.points.Length;i++)
                roads.Add((new Vector3((float)bridge.points[i-1].x,0,(float)bridge.points[i-1].y)/LakeScale,new Vector3((float)bridge.points[i].x,0,(float)bridge.points[i].y)/LakeScale));
            // Keep batches spatially bounded: a city-wide mesh forces every tree through
            // every visible/shadow pass even when only a street corner is on screen.
            var chunks=new Dictionary<Vector2Int,ForestChunk>();
            for(int z=-850;z<1750;z+=12)for(int x=-1400;x<1500;x+=12)
            {
                uint mixed=unchecked((uint)StableCityHash(x+":"+z));
                mixed^=mixed>>16;mixed=unchecked(mixed*0x7feb352dU);mixed^=mixed>>15;mixed=unchecked(mixed*0x846ca68bU);mixed^=mixed>>16;
                int hash=(int)(mixed&int.MaxValue);
                if((Mathf.Abs(x)>1150||z>1350||z< -700)&&hash%3!=0)continue;
                var p=new Vector3(x+(hash%97)/12f,0,z+((hash/97)%97)/12f);
                if(WithinLake(p,shore)||occupied.Any(r=>r.Contains(new Vector2(p.x,p.z))))continue;
                if(roads.Any(r=>SegmentDistance(p,r.a,r.b)<10))continue;
                bool roadVerge=roads.Any(r=>SegmentDistance(p,r.a,r.b)<26);
                float height=MountainHeight(p.x,p.z);p.y=height;
                // Flat interdistrict land is woodland too. Small park trees only belong around urban plots.
                bool nearBuildings=occupied.Any(r=>p.x>r.xMin-25&&p.x<r.xMax+25&&p.z>r.yMin-25&&p.z<r.yMax+25);
                bool urban=height<1&&(nearBuildings||roadVerge);
                float clustering=Mathf.PerlinNoise(p.x*.028f+13,p.z*.028f+27);
                if(urban&&clustering<.46f)continue;
                if(!urban&&clustering<.28f&&hash%4!=0)continue;
                float size=urban?6+hash%5:height<1?9+hash%9:12+hash%11;
                int color=hash%4;if(urban&&hash%4==0)color=4;
                var cell=new Vector2Int(Mathf.FloorToInt(p.x/192),Mathf.FloorToInt(p.z/192));
                if(!chunks.TryGetValue(cell,out var chunk)){chunk=new ForestChunk();chunks.Add(cell,chunk);}
                var verts=chunk.Vertices;var tris=chunk.Triangles;
                AddTreeMesh(verts[color],tris[color],verts[5],tris[5],p,size,hash%3==0&&color!=4);
                // A separate understorey fills the bare grass between crowns and along the coast.
                if(urban||hash%2==0)
                {
                    AddCrown(verts[1],tris[1],p+new Vector3(1,.8f,1),new Vector3(2.6f,1.1f,2.1f),false);
                    AddCrown(verts[2],tris[2],p+new Vector3(-1.8f,.6f,-1),new Vector3(2.1f,.8f,1.9f),false);
                    if(urban&&hash%3==0)AddCrown(verts[4],tris[4],p+new Vector3(1.8f,1.2f,-1.3f),new Vector3(1.15f,.6f,1.1f),false);
                }
            }
            string[] colors={"4E8542","709D45","8DAF4F","A2BA56","DFA6C0","887254"};
            foreach(var pair in chunks)
                for(int i=0;i<6;i++)
                    if(pair.Value.Vertices[i].Count>0)
                        LandscapeMesh(root,(i==5?"ForestTrunks":"ForestCanopies_"+i)+"_"+pair.Key.x+"_"+pair.Key.y,
                            pair.Value.Vertices[i],pair.Value.Triangles[i],Mats.Lit(ArtDirection.Hex(colors[i])));
        }
        private sealed class ForestChunk
        {
            public readonly List<Vector3>[] Vertices=new List<Vector3>[6];
            public readonly List<int>[] Triangles=new List<int>[6];
            public ForestChunk(){for(int i=0;i<6;i++){Vertices[i]=new List<Vector3>();Triangles[i]=new List<int>();}}
        }
        private static void AddTreeMesh(List<Vector3> v,List<int> t,List<Vector3> trunks,List<int> tt,Vector3 p,float height,bool conifer)
        {
            AddCrown(trunks,tt,p+Vector3.up*height*.3f,new Vector3(.18f,height*.3f,.18f),false);
            if(conifer)
            {
                for(int i=0;i<3;i++)AddCrown(v,t,p+Vector3.up*height*(.34f+i*.2f),new Vector3(height*(.32f-i*.055f),height*.37f,height*(.32f-i*.055f)),true);
            }
            else
            {
                AddCrown(v,t,p+Vector3.up*height*.68f,new Vector3(height*.43f,height*.36f,height*.40f),false);
                AddCrown(v,t,p+new Vector3(height*.26f,height*.57f,0),new Vector3(height*.31f,height*.25f,height*.32f),false);
                AddCrown(v,t,p+new Vector3(-height*.23f,height*.55f,height*.16f),new Vector3(height*.3f,height*.24f,height*.28f),false);
            }
        }
        private static void AddCrown(List<Vector3> v,List<int> t,Vector3 center,Vector3 scale,bool cone)
        {
            const int sides=7,rings=4;int start=v.Count;
            for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
            {
                float phi=Mathf.PI*j/rings,a=2*Mathf.PI*i/sides;
                float radius=cone?j/(float)rings:Mathf.Sin(phi),y=cone?1-2*j/(float)rings:Mathf.Cos(phi);
                v.Add(center+Vector3.Scale(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius),scale));
            }
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {int a=start+j*(sides+1)+i,b=a+sides+1;t.Add(a);t.Add(a+1);t.Add(b);t.Add(b);t.Add(a+1);t.Add(b+1);}
        }
        private void BuildClosedLakeBridge(Transform parent,PCShopSell.Core.World.LayoutLakeBridge bridge)
        {
            var root=new GameObject(bridge.id+"_Closed").transform;root.SetParent(parent,false);
            var points=bridge.points.Select(p=>new Vector3((float)p.x,0,(float)p.y)/LakeScale).ToArray();
            float total=0;for(int i=1;i<points.Length;i++)total+=Vector3.Distance(points[i-1],points[i]);
            float traveled=0,peak=bridge.requiredTier==2?5.5f:13.5f;
            for(int i=1;i<points.Length;i++)
            {
                Vector3 a=points[i-1],b=points[i];float length=Vector3.Distance(a,b),start=traveled;
                float ha=.35f+peak*Mathf.Sin(Mathf.PI*traveled/total);traveled+=length;
                float hb=.35f+peak*Mathf.Sin(Mathf.PI*traveled/total);a.y=ha;b.y=hb;
                var deck=Prims.Box(root,"ConcreteBridgeDeck",(a+b)*.5f-Vector3.up*.35f,new Vector3(12,.7f,Vector3.Distance(a,b)+.5f),Mats.Lit(ArtDirection.Hex("C9CCBE")));
                deck.transform.rotation=Quaternion.LookRotation(b-a);
                var road=Prims.Box(root,"BridgeAsphalt",(a+b)*.5f+Vector3.up*.025f,new Vector3(8,.08f,Vector3.Distance(a,b)+.1f),Mats.Lit(Asphalt));road.transform.rotation=deck.transform.rotation;
                Vector3 right=Vector3.Cross(Vector3.up,(b-a).normalized);
                for(float s=Mathf.Ceil(start/5)*5-start;s<length;s+=5)
                {
                    Vector3 pos=Vector3.Lerp(a,b,s/length);
                    var dash=Prims.Box(root,"BridgeLaneMark",pos+Vector3.up*.08f,new Vector3(.14f,.02f,2.3f),Mats.Lit(ArtDirection.Cream));dash.transform.rotation=deck.transform.rotation;
                    foreach(int side in new[]{-1,1})
                    {
                        Prims.Box(root,"Baluster",pos+right*side*5.65f+Vector3.up*.7f,new Vector3(.17f,1.4f,.17f),Mats.Lit(ArtDirection.Cream));
                        if(Mathf.RoundToInt((start+s)/5)%5==0)LakeLamp(root,pos+right*side*5.1f);
                    }
                    if(Mathf.RoundToInt((start+s)/5)%6==0&&pos.y>2)
                        Prims.Shape(PrimitiveType.Cylinder,root,"BridgePier",new Vector3(pos.x,pos.y*.5f-.5f,pos.z),new Vector3(2.2f,pos.y*.5f,2.2f),Mats.Lit(ArtDirection.Hex("B7BBAF")));
                }
                foreach(int side in new[]{-1,1})
                {
                    var rail=Prims.Box(root,"ContinuousRailing",(a+b)*.5f+right*side*5.65f+Vector3.up*1.4f,new Vector3(.22f,.18f,Vector3.Distance(a,b)+.5f),Mats.Lit(ArtDirection.Cream));rail.transform.rotation=deck.transform.rotation;
                }
            }
            foreach(int end in new[]{0,points.Length-1})
            {
                Vector3 p=points[end];var gate=new GameObject("ClosedBridgeGate").transform;gate.SetParent(root,false);gate.position=p;
                gate.localRotation=Quaternion.LookRotation(points[end==0?1:end-1]-p);gate.localPosition=p;
                foreach(int side in new[]{-1,1})Prims.Box(gate,"GatePost",new Vector3(side*5,1.5f,0),new Vector3(.6f,3,.6f),Mats.Lit(ArtDirection.Ink),true);
                Prims.Box(gate,"ClosedBarrier",new Vector3(0,1.1f,0),new Vector3(10,.5f,.35f),Mats.Lit(ArtDirection.Clay),true);
                for(int i=-4;i<=4;i++)Prims.Box(gate,"BarrierStripe",new Vector3(i,1.1f,-.19f),new Vector3(.4f,.5f,.03f),Mats.Lit(ArtDirection.Cream));
                Prims.Box(gate,"BridgeSign",new Vector3(0,3.4f,0),new Vector3(10,1.5f,.3f),Mats.Lit(ArtDirection.Ink));
                Prims.SignText(gate,bridge.name+" · 暂未开放",new Vector3(0,3.4f,-.18f),9.5f,1.2f,ArtDirection.Cream);
            }
            StaticBatchingUtility.Combine(root.gameObject);
        }
        private static void LakeLamp(Transform root,Vector3 p)
        {
            Prims.Box(root,"LakesideLamp",p+Vector3.up*2.3f,new Vector3(.16f,4.6f,.16f),Mats.Lit(ArtDirection.Ink));
            Prims.Shape(PrimitiveType.Sphere,root,"LampLantern",p+Vector3.up*4.65f,Vector3.one*.55f,Mats.Glow(ArtDirection.Cream,new Color(3,2.5f,1.8f)));
        }
        private void BuildShoreFurniture(Transform root,List<Vector3> shore)
        {
            var detail=new GameObject("ShorePromenades").transform;detail.SetParent(root,false);
            var reeds=new List<Vector3>();var reedTriangles=new List<int>();
            for(int i=0;i<shore.Count;i+=2)
            {
                if(shore[i].x>-210&&shore[i].z>-130)continue;
                var inward=(new Vector3(-25,0,90)-shore[i]).normalized;
                for(int j=0;j<12;j++)
                {
                    var p=shore[i]+inward*(.6f+j%3*.7f)+new Vector3(Mathf.Sin(j*2.7f)*2,.7f,Mathf.Cos(j*1.9f)*2);
                    AddCrown(reeds,reedTriangles,p,new Vector3(.12f,.8f+j%3*.3f,.12f),true);
                }
            }
            LandscapeMesh(detail,"NaturalShoreReeds",reeds,reedTriangles,Mats.Lit(ArtDirection.Hex("8C9E48")));
            var roads=new List<(Vector3 a,Vector3 b)>();
            foreach(var node in _city.Map.Nodes)foreach(var edge in _city.Map.NeighborsOf(node.Id))
                if(string.CompareOrdinal(node.Id,edge.To)<0)roads.Add((LakeNode(node.Id),LakeNode(edge.To)));
            for(int i=0;i<shore.Count;i+=2)
            {
                Vector3 outward=(shore[i]-new Vector3(-25,0,90)).normalized;
                Vector3 p=shore[i]+outward*6;
                if(roads.Any(r=>SegmentDistance(p,r.a,r.b)<7))continue;
                var tree=new GameObject("LakeshoreFloweringTree").transform;tree.SetParent(detail,false);tree.localPosition=p;tree.localScale=Vector3.one*2;
                CityVegetation.Build(tree,Vector3.zero,i+1);
                if(i%6==0)foreach(var renderer in tree.GetComponentsInChildren<MeshRenderer>())
                    if(renderer.name=="SculptedCanopy")renderer.sharedMaterial=Mats.Lit(ArtDirection.Hex("DFA6C0"));
            }
            // Viewing decks and reed islands are decoration; no new gameplay destinations.
            foreach(var pos in new[]{new Vector3(610,0,230),new Vector3(-70,0,-310),new Vector3(-70,0,675)})
            {
                Prims.Box(detail,"ViewingDeck",pos+Vector3.up*.4f,new Vector3(12,.6f,8),Mats.Lit(ArtDirection.Wood));
                for(int i=-5;i<=5;i++)Prims.Box(detail,"DeckJoint",pos+new Vector3(i,.71f,0),new Vector3(.025f,.02f,8),Mats.Lit(ArtDirection.Ink));
                foreach(int s in new[]{-1,1})LakeLamp(detail,pos+new Vector3(s*5,0,3));
            }
            for(int i=0;i<shore.Count;i+=3)
            {
                var p=shore[i];Vector3 toward=(new Vector3(-25,0,90)-p).normalized;
                for(int j=0;j<3;j++)
                {
                    Vector3 at=p+toward*(2+j*1.3f)+new Vector3(j,0,0);
                    Prims.Shape(PrimitiveType.Sphere,detail,"ShoreRock",at+Vector3.up*.3f,new Vector3(1.7f,.9f,1.3f),Mats.Lit(ArtDirection.Hex("A5AE8D")));
                }
            }
            foreach(var n in _city.Map.Nodes.Where(n=>n.Id.StartsWith("lake_park_")&&StableCityHash(n.Id)%5==0))
            {Vector3 p=LakeNode(n.Id);LakeLamp(detail,p+new Vector3(7,0,0));}
            StaticBatchingUtility.Combine(detail.gameObject);
        }
        private static void BuildLakeIslands(Transform root)
        {
            var islands=new GameObject("ScenicLakeIslands").transform;islands.SetParent(root,false);
            foreach(var center in new[]{new Vector3(360,0,350),new Vector3(-580,0,300)})
            {
                var contour=new List<Vector3>();
                for(int i=0;i<36;i++)
                {
                    float angle=i*Mathf.PI*2/36,r=1+.12f*Mathf.Sin(i*1.7f);
                    contour.Add(center+new Vector3(Mathf.Cos(angle)*22*r,0,Mathf.Sin(angle)*11*r));
                }
                // These small islands use their own fan center, unlike the main lake.
                var v=new List<Vector3>{center+Vector3.up*.45f};v.AddRange(contour.Select(p=>p+Vector3.up*.4f));var t=new List<int>();
                for(int i=0;i<36;i++){t.Add(0);t.Add((i+1)%36+1);t.Add(i+1);}
                LandscapeMesh(islands,"IslandBank",v,t,Mats.Lit(ArtDirection.Hex("C7C7A0")));
                for(int i=0;i<v.Count;i++)v[i]=center+(v[i]-center)*.9f+Vector3.up*.08f;
                LandscapeMesh(islands,"IslandGrass",v,t,Mats.Lit(ArtDirection.Hex("87AB5D")));
                for(int i=-2;i<=2;i++)
                {
                    var tree=new GameObject("IslandTree").transform;tree.SetParent(islands,false);tree.localPosition=center+new Vector3(i*6,.5f,Mathf.Sin(i*4)*3);tree.localScale=Vector3.one*1.7f;
                    CityVegetation.Build(tree,Vector3.zero,i+8);
                }
            }
            StaticBatchingUtility.Combine(islands.gameObject);
        }
        private void BuildLakeCourtyards(Transform parent)
        {
            var gardens=new GameObject("AuthoredCourtyardGardens").transform;gardens.SetParent(parent,false);int index=0;
            foreach(var parcel in _city.LakeParcels)
            {
                var root=new GameObject("Courtyard_"+index).transform;root.SetParent(gardens,false);
                root.localPosition=new Vector3((float)parcel.x,0,(float)parcel.y)/LakeScale;root.localRotation=Quaternion.Euler(0,(float)parcel.heading,0);
                float w=(float)parcel.width/LakeScale,d=(float)parcel.depth/LakeScale;
                var stone=Mats.Lit(ArtDirection.Hex("D8CEB3"));var leaf=Mats.Lit(ArtDirection.Hex("6D9545"));
                if(parcel.kind=="park")
                {
                    Garden(root,new Rect(-w/2,0,w,d),false);
                    Prims.Shape(PrimitiveType.Cylinder,root,"ParkPromenade",new Vector3(0,.15f,d*.52f),new Vector3(w*.8f,.1f,d*.62f),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"ParkIslandLawn",new Vector3(0,.27f,d*.52f),new Vector3(w*.69f,.04f,d*.53f),leaf);
                    Prims.Shape(PrimitiveType.Cylinder,root,"ParkReflectingPond",new Vector3(0,.34f,d*.52f),new Vector3(w*.45f,.04f,d*.28f),Mats.Lit(ArtDirection.Hex("4AA8BE"),.7f));
                    for(int j=0;j<12;j++)
                    {
                        float angle=j*Mathf.PI/6;var tree=new GameObject("ParkGrove").transform;tree.SetParent(root,false);
                        tree.localPosition=new Vector3(Mathf.Cos(angle)*w*.43f,.2f,d*.52f+Mathf.Sin(angle)*d*.39f);tree.localScale=Vector3.one*2;
                        CityVegetation.Build(tree,Vector3.zero,j);
                    }
                    index++;continue;
                }
                if(parcel.kind=="fountain")
                {
                    Garden(root,new Rect(-w/2,0,w,d),false);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralPlazaRing",new Vector3(0,.14f,d*.5f),new Vector3(29,.12f,29),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainBasin",new Vector3(0,.45f,d*.5f),new Vector3(20,.38f,20),Mats.Lit(ArtDirection.Cream));
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainWater",new Vector3(0,.85f,d*.5f),new Vector3(18.8f,.04f,18.8f),Mats.Lit(ArtDirection.Hex("3FADCD"),.7f));
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainPedestal",new Vector3(0,1.2f,d*.5f),new Vector3(3,.5f,3),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralWaterJet",new Vector3(0,3,d*.5f),new Vector3(.25f,1.5f,.25f),Mats.Lit(ArtDirection.Hex("C4E8E7"),.7f));
                    for(int j=0;j<8;j++)
                    {float a=j*Mathf.PI/4;Prims.Shape(PrimitiveType.Cylinder,root,"FountainJet",new Vector3(Mathf.Cos(a)*3,1.5f,d*.5f+Mathf.Sin(a)*3),new Vector3(.12f,.65f,.12f),Mats.Lit(ArtDirection.Hex("C4E8E7"),.7f));}
                    index++;continue;
                }
                Prims.Box(root,"GardenRetainingEdge",new Vector3(0,.18f,d/2),new Vector3(w,.35f,d),stone);
                Prims.Box(root,"InsetLawn",new Vector3(0,.37f,d/2),new Vector3(w-.6f,.12f,d-.6f),Mats.Lit(ArtDirection.Hex("8BA75D")));
                Prims.Box(root,"GardenPath",new Vector3(0,.46f,d/2),new Vector3(1.4f,.08f,d),stone);
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"ClippedHedge",new Vector3(side*(w/2-.6f),.8f,d/2),new Vector3(.65f,.8f,d-1),leaf);
                    Prims.Box(root,"FlowerBorder",new Vector3(side*(w/2-1.8f),.55f,d*.55f),new Vector3(1,.3f,d*.65f),Mats.Lit(ArtDirection.Hex(index%2==0?"D6AFBE":"DCC779")));
                    var tree=new GameObject("GardenTree").transform;tree.SetParent(root,false);tree.localPosition=new Vector3(side*w*.27f,.44f,d*.7f);tree.localScale=Vector3.one*(index%3==0?1.55f:1.25f);
                    CityVegetation.Build(tree,Vector3.zero,index%3==0?1:index+side+4);
                    if(index%3==0)foreach(var renderer in tree.GetComponentsInChildren<MeshRenderer>())if(renderer.name=="SculptedCanopy")renderer.sharedMaterial=Mats.Lit(ArtDirection.Hex("E3ABC4"));
                    Prims.Box(root,"BenchSeat",new Vector3(side*w*.25f,.9f,d*.22f),new Vector3(2.1f,.18f,.65f),Mats.Lit(ArtDirection.Wood));
                    Prims.Box(root,"BenchBack",new Vector3(side*w*.25f,1.25f,d*.22f+.3f),new Vector3(2.1f,.6f,.12f),Mats.Lit(ArtDirection.Wood));
                }
                if(index%5==0&&d>12)
                {
                    Prims.Shape(PrimitiveType.Cylinder,root,"CourtyardBasin",new Vector3(0,.8f,d*.52f),new Vector3(3,.35f,3),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CourtyardWater",new Vector3(0,1.17f,d*.52f),new Vector3(2.6f,.035f,2.6f),Mats.Lit(ArtDirection.Hex("56ADBB"),.7f));
                }
                index++;
            }
            StaticBatchingUtility.Combine(gardens.gameObject);
        }
        private static void BuildLakeExhibitionHall(Transform parent)
        {
            // Landmark backdrop for the existing convention district, not another gameplay shop.
            var root=new GameObject("ConventionDistrictExhibitionHall").transform;root.SetParent(parent,false);root.localPosition=new Vector3(525,0,310);
            var glass=Mats.Lit(ArtDirection.Hex("579BBE"),.65f);var white=Mats.Lit(ArtDirection.Cream);
            Prims.Box(root,"CivicPlaza",new Vector3(0,.05f,20),new Vector3(65,.1f,52),Mats.Lit(Sidewalk));
            Prims.Box(root,"Hall",new Vector3(0,7,20),new Vector3(52,14,36),glass);
            foreach(int side in new[]{-1,1})for(int i=0;i<7;i++)
                Prims.Box(root,"FacadeFrame",new Vector3(side*26.1f,7,3+i*5.7f),new Vector3(.5f,14,.5f),white);
            for(int i=-4;i<=4;i++)Prims.Box(root,"LobbyFrame",new Vector3(i*6,7,1.8f),new Vector3(.45f,14,.5f),white);
            for(int bay=-1;bay<=1;bay++)
            {
                var roof=Prims.Box(root,"BlueRoofBay",new Vector3(bay*17.5f,14.8f,20),new Vector3(17,1.2f,37),Mats.Lit(ArtDirection.Hex("467FB0"),.4f));
                foreach(int side in new[]{-1,1})Prims.Box(root,"RoofRib",new Vector3(bay*17.5f+side*8,15.5f,20),new Vector3(.45f,.4f,38),white);
            }
            Prims.Box(root,"EntranceCanopy",new Vector3(0,5,-2),new Vector3(20,.4f,8),white);
            Prims.SignText(root,"会展中心",new Vector3(0,8,1.4f),18,2,ArtDirection.Cream);
            foreach(int side in new[]{-1,1})for(int i=0;i<4;i++)CityVegetation.Build(root,new Vector3(side*29,0,6+i*10),i);
            StaticBatchingUtility.Combine(root.gameObject);
        }
    }
    // Runtime-created meshes/materials must leave with the scene when a save is reloaded.
    public sealed class CityLandscapeResources : MonoBehaviour
    {
        public readonly List<UnityEngine.Object> Owned=new List<UnityEngine.Object>();
        private void OnDestroy()
        {
            foreach(var item in Owned)if(item!=null)
            {if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        }
    }
}
