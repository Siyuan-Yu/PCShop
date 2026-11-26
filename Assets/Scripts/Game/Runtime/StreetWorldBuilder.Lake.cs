using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private float LakeScale=>_city.LakeSceneScale;
        private const float LakeWaterLevel = .21f;
        private const float LakeBridgeHalfWidth = 6;
        internal static float LakeAccessHeight(PCShopSell.Core.World.CityLayout city) =>
            Mathf.Max(.25f, LakeWaterLevel * city.LakeSceneScale + .06f);
        private Vector3 LakeNode(string id)=>NodeWorld(id)/LakeScale;
        // Physical scenery and navigation share the same layout; closed bridges are scenery only.
        private IEnumerator BuildLakeLandscape()
        {
            var root=new GameObject("LakeCityLandscape").transform;root.SetParent(_root,false);
            root.localScale=Vector3.one*LakeScale;
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var shore=SmoothShore();
            LakeSurface(root,"StoneShore",shore,1.003f,.12f,DistrictFinishes.Tile("cultivatedSoil",Vector2.one));

            var shader=Resources.Load<Shader>("Shaders/LakeWater");
            var water=shader!=null?new Material(shader):Mats.Lit(ArtDirection.Hex("385E72"),.8f);
            if(shader!=null)
            {
                resources.Owned.Add(water);
                water.SetFloat("_LakeShore",1);water.SetFloat("_ReflectMask",1);
                water.SetTexture("_WindField",Resources.Load<Texture2D>("OldTown/Surfaces/meadowVariation"));
                water.SetTexture("_WaterMarks",Resources.Load<Texture2D>("OldTown/Surfaces/windRipples"));
            }
            BuildLakeWater(root,shore,water);
            yield return null;
            BuildMountainGround(root);
            ReportBuildProgress(.37f);
            ReportBuildStage("loading_forest_layout");
            yield return BuildLakeForest(root,shore);
            ReportBuildStage("loading_shore_details");
            int bridgesDone=0;
            foreach(var bridge in _city.LakeBridges)
            {
                BuildClosedLakeBridge(root,bridge);
                ReportBuildProgress(.89f+.025f*++bridgesDone/Mathf.Max(1,_city.LakeBridges.Count()));
                yield return null;
            }
            // Preserve the neighbourhood fountain in its translated courtyard.
            BuildLakeCourtyards(root);
            ReportBuildProgress(.925f);
            yield return null;
            BuildShoreFurniture(root,shore);
            ReportBuildProgress(.94f);
            yield return null;
            BuildLakeIslands(root);
            ReportBuildProgress(.95f);
            yield return null;
            BuildRaceCauseways();
            _streetRoadEnvelopes = null;
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
        internal float SurveyCanopyHeight(Vector3 world)
        {
            if(!_city.IsLakeCity)return 0;
            float scale=LakeScale;
            return (MountainHeight(world.x/scale,world.z/scale)+30f)*scale;
        }
        private static void BuildMountainGround(Transform root)
        {
            var v=new List<Vector3>();var t=new List<int>();
            const int nx=144,nz=140;const float step=22;
            for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
            {float x=-1500+i*step,z=-1000+j*step;v.Add(new Vector3(x,MountainHeight(x,z)+.001f,z));}
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++)
            {int a=j*(nx+1)+i,b=a+nx+1;t.Add(a);t.Add(b);t.Add(a+1);t.Add(a+1);t.Add(b);t.Add(b+1);}
            LandscapeMesh(root,"ForestedMountainTerrain",v,t,DistrictFinishes.Meadow());
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
                LandscapeMesh(root,"GraniteOutcrop",flat,Enumerable.Range(0,flat.Count).ToList(),Mats.Lit(ArtDirection.Hex(i%2==0?"8C938D":"A8ABA0")));
            }
        }
        private static bool WithinLake(Vector3 p,List<Vector3> shore) => ContainsGroundPoint(p,shore);
        private static bool ContainsGroundPoint(Vector3 p,IReadOnlyList<Vector3> boundary)
        {
            bool inside=false;
            for(int i=0,j=boundary.Count-1;i<boundary.Count;j=i++)
            {var a=boundary[i];var b=boundary[j];if((a.z>p.z)!=(b.z>p.z)&&p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x)inside=!inside;}
            return inside;
        }
        private static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {var d=b-a;return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));}
        private IEnumerator BuildLakeForest(Transform root,List<Vector3> shore)
        {
            var occupied=new List<Rect>();
            void Reserve(Vector3 at,float width,float depth,float heading)
            {
                var rot=Quaternion.Euler(0,heading,0);var bounds=new Bounds(at,Vector3.zero);
                foreach(int s in new[]{-1,1})foreach(int back in new[]{0,1})bounds.Encapsulate(at+rot*new Vector3(s*width/2,0,back*depth));
                occupied.Add(Rect.MinMaxRect(bounds.min.x-4,bounds.min.z-4,bounds.max.x+4,bounds.max.z+4));
            }
            foreach(var b in _city.Blocks.Where(b => BuildsStreet(b.street)))Reserve(new Vector3((float)b.x,0,(float)b.y)/LakeScale,(float)b.width/LakeScale,(float)b.depth/LakeScale,(float)b.heading);
            foreach (var p in _city.PublicPlaces.Where(BuildsPlace))
            {
                var node = LakeNode(p.NodeId);
                var door = node + new Vector3((float)p.EntranceX, 0, (float)p.EntranceY) / LakeScale;
                Reserve(door, (float)p.Width / LakeScale, (float)p.Depth / LakeScale, (float)p.Heading);
                // Long entrance paths can leave a gap between the road and plot clearances.
                if (p.Id == "life_hike" && (door - node).sqrMagnitude > 14f * 14f)
                {
                    const float clearance = 4f;
                    occupied.Add(Rect.MinMaxRect(Mathf.Min(node.x, door.x) - clearance, Mathf.Min(node.z, door.z) - clearance,
                        Mathf.Max(node.x, door.x) + clearance, Mathf.Max(node.z, door.z) + clearance));
                }
            }
            foreach(var p in _city.LakeParcels)Reserve(new Vector3((float)p.x,0,(float)p.y)/LakeScale,(float)p.width/LakeScale,(float)p.depth/LakeScale,(float)p.heading);
            var roads=new List<(Vector3 a,Vector3 b)>();
            foreach(var n in _city.Map.Nodes)foreach(var e in _city.Map.NeighborsOf(n.Id))
                if(string.CompareOrdinal(n.Id,e.To)<0 && BuildsRoad(n.Id, e.To))roads.Add((LakeNode(n.Id),LakeNode(e.To)));
            foreach(var bridge in _city.LakeBridges)for(int i=1;i<bridge.points.Length;i++)
                roads.Add((new Vector3((float)bridge.points[i-1].x,0,(float)bridge.points[i-1].y)/LakeScale,new Vector3((float)bridge.points[i].x,0,(float)bridge.points[i].y)/LakeScale));
            // Keep batches spatially bounded: a city-wide mesh forces every tree through
            // every visible/shadow pass even when only a street corner is on screen.
            var modernPlots=new RoadSurfaceGeometry.Index();
            void Outline(Vector2 door,float heading,float width,float depth)
            {
                float rad=heading*Mathf.Deg2Rad;
                var back=new Vector2(Mathf.Sin(rad),Mathf.Cos(rad));
                modernPlots.Add(RoadSurfaceGeometry.Strip(door-back*1.75f,door+back*(depth+1.75f),width+3.5f));
            }
            foreach(var place in _city.PublicPlaces.Where(BuildsPlace))
            {
                Vector2 door=DoorXZ(place)/LakeScale;
                Outline(door,(float)place.Heading,(float)place.Width/LakeScale,(float)place.Depth/LakeScale);
                var node=LakeNode(place.NodeId);
                var approach=new Vector2(node.x,node.z);
                if((approach-door).sqrMagnitude>.01f)
                    modernPlots.Add(RoadSurfaceGeometry.Strip(approach,door,place.Id=="life_hike"?8f:3.4f));
            }
            foreach(var plot in _city.Blocks.Where(b=>BuildsStreet(b.street)))
                Outline(new Vector2((float)plot.x,(float)plot.y)/LakeScale,(float)plot.heading,(float)plot.width/LakeScale,(float)plot.depth/LakeScale);
            foreach(var plot in _city.LakeParcels)
                Outline(new Vector2((float)plot.x,(float)plot.y)/LakeScale,(float)plot.heading,(float)plot.width/LakeScale,(float)plot.depth/LakeScale);
            var occupiedGrid = new ForestSpatialIndex<Rect>();
            foreach (var rect in occupied)
                occupiedGrid.Add(Rect.MinMaxRect(rect.xMin-25,rect.yMin-25,rect.xMax+25,rect.yMax+25),rect);
            var roadGrid = new ForestSpatialIndex<(Vector3 a,Vector3 b)>();
            foreach (var road in roads)
                roadGrid.Add(Rect.MinMaxRect(Mathf.Min(road.a.x,road.b.x)-26,Mathf.Min(road.a.z,road.b.z)-26,
                    Mathf.Max(road.a.x,road.b.x)+26,Mathf.Max(road.a.z,road.b.z)+26),road);
            // Cache only for this build; no persistent duplicate of the complete woodland.
            var understory=new Dictionary<Vector2Int,StreetPlanting>();
            var woodland=new Dictionary<Vector2Int,BakedCityMap.ForestChunk>();
            if(Baking)_bakedForestRoot=root;
            float deadline = Time.realtimeSinceStartup + .016f;
            Vector2 parkDoor=_city.HasPlace("life_park") ? DoorXZ(_city.GetPlace("life_park")) : new Vector2(float.MaxValue,float.MaxValue);
            var parkParcel=_city.LakeParcels.FirstOrDefault(parcel=>parcel.kind=="park");
            Vector2 parkCentre=parkParcel==null ? parkDoor : new Vector2((float)parkParcel.x,(float)parkParcel.y);
            const int spacing=9;
            for(int z=-850;z<1750;z+=spacing)for(int x=-1400;x<1500;x+=spacing)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    ReportBuildProgress(.37f+.32f*((z+850)+(x+1400)*spacing/2900f)/2600f);
                    yield return null;
                    deadline = Time.realtimeSinceStartup + .016f;
                }
                uint mixed=unchecked((uint)StableCityHash(x+":"+z));
                mixed^=mixed>>16;mixed=unchecked(mixed*0x7feb352dU);mixed^=mixed>>15;mixed=unchecked(mixed*0x846ca68bU);mixed^=mixed>>16;
                int hash=(int)(mixed&int.MaxValue);
                if((Mathf.Abs(x)>1150||z>1350||z< -700)&&hash%5!=0)continue;
                var p=new Vector3(x+(hash%97)/12f,0,z+((hash/97)%97)/12f);
                if(!BuildsLakeSceneryAt(p)||WithinLake(p,shore))continue;
                var world=p*LakeScale;
                bool blocked=false, nearBuildings=false, insetGarden=false;
                var nearbyPlots=occupiedGrid.At(p);
                if(nearbyPlots!=null)foreach(var rect in nearbyPlots)
                {
                    // The axis-aligned envelope also contains the empty triangular corners of rotated plots.
                    // Retain those corners only for the modern tree belts below, after a precise plot check.
                    if(rect.Contains(new Vector2(p.x,p.z))) { blocked=true;break; }
                    if(p.x>rect.xMin-25&&p.x<rect.xMax+25&&p.z>rect.yMin-25&&p.z<rect.yMax+25)nearBuildings=true;
                }
                if(blocked)
                {
                    if(OnOutskirtsLawn(world) || InPaintedQuarter(new Vector2(world.x,world.z),120f))continue;
                    var pos=new Vector2(p.x,p.z);
                    var outline=RoadSurfaceGeometry.Strip(pos-Vector2.up*1.2f,pos+Vector2.up*1.2f,2.4f);
                    if(modernPlots.Nearby(outline).Any(id=>RoadSurfaceGeometry.Intersect(outline,modernPlots.Polygons[id]).Length>=3))continue;
                    nearBuildings=true;
                    insetGarden=true;
                    blocked=false;
                }
                bool roadVerge=false;
                var nearbyRoads=roadGrid.At(p);
                if(nearbyRoads!=null)foreach(var road in nearbyRoads)
                {
                    float distance=SegmentDistanceSquared(p,road.a,road.b);
                    if(distance<72.25f) { blocked=true;break; }
                    if(distance<676)roadVerge=true;
                }
                if(blocked)continue;
                float height=MountainHeight(p.x,p.z);p.y=height;
                // Flat interdistrict land is woodland too. Small park trees only belong around urban plots.
                bool urban=height<1&&(nearBuildings||roadVerge);
                float clustering=Mathf.PerlinNoise(p.x*.028f+13,p.z*.028f+27);
                var worldPosition = root.TransformPoint(p);
                if (InPaintedQuarter(new Vector2(worldPosition.x,worldPosition.z),120f) || OnOutskirtsLawn(worldPosition))
                {
                    if (urban && clustering < .33f || !urban && clustering < .28f && hash%4 != 0) continue;
                    float sizeInQuarter = urban ? 7+hash%4 : height<1 ? 10+hash%6 : 12+hash%9;
                    bool blossom=urban && hash%4==0;
                    var fromHome=new Vector2(worldPosition.x,worldPosition.z)-_quarterOrigin;
                    if(fromHome.sqrMagnitude<114f*114f)
                    {
                        // Across both close views the rear lawn is open, and the low kitchen beds
                        // are visible behind the flowering tree. Keep the forest scatter off them.
                        var local=Quaternion.Euler(0,-PCShopSell.Core.World.OldQuarterLanes.Heading,0)*
                            new Vector3(fromHome.x,0,fromHome.y);
                        var ground=new Vector2(local.x,local.z);
                        if(ContainsGroundPoint(local,QuarterMeadowEdge))continue;
                        if((ground-new Vector2(-1.23f,-24.61f)).sqrMagnitude<3.2f*3.2f ||
                           (ground-new Vector2(-2.75f,31.01f)).sqrMagnitude<3.2f*3.2f)continue;
                        // Leave the mature tree beside the station vegetable patch its authored spot.
                        // A scattered sapling within this radius would cancel that courtyard tree.
                        if((ground-new Vector2(-9.3f,-22.6f)).sqrMagnitude<21f)continue;
                    }
                    if(fromHome.sqrMagnitude<95f*95f)
                    {
                        bool inClearing=false;
                        for(int i=0;i<QuarterBlossoms.Length;i++)
                        {
                            float radius=QuarterBlossoms[i].clearing;
                            var relative=new Vector2(worldPosition.x,worldPosition.z)-_blossomRoots[i];
                            if(relative.sqrMagnitude<radius*radius){inClearing=true;break;}
                        }
                        if(inClearing)continue;
                    }
                    float scale=fromHome.sqrMagnitude<26f*26f ? 1.1f+hash%4*.13f : sizeInQuarter*.255f;
                    QuarterTree(_root,worldPosition,scale,hash%360,blossom);
                    if (fromHome.sqrMagnitude>=26f*26f)
                    {
                        var undergrowth=new GameObject("Street tree undergrowth").transform;
                        undergrowth.SetParent(_root,false);
                        undergrowth.position=worldPosition;
                        undergrowth.rotation=Quaternion.Euler(0,hash%360,0);
                        QuarterLeafCluster(undergrowth,"CrownA",new Vector3(-scale*.78f,scale*.40f,.4f),
                            new Vector2(scale*1.8f,scale*1.03f),"RouteLeaves");
                        QuarterLeafCluster(undergrowth,"CrownB",new Vector3(scale*.86f,scale*.36f,.6f),
                            new Vector2(scale*1.64f,scale*.95f),"RouteLeaves");
                    }
                    continue;
                }
                float cover=DistrictFinishes.GroveCover(world);
                float clearing=Mathf.Lerp(.56f,.23f,cover);
                float blossomBed=DistrictFinishes.BlossomCover(world);
                bool parkMeadow=(new Vector2(world.x,world.z)-parkCentre).sqrMagnitude<392f*392f;
                bool glade=parkMeadow && height<31 && !urban && clustering<.65f && blossomBed<.16f;
                if((glade && hash%5>1) || (clustering<clearing && hash%6!=0 && blossomBed<.18f))continue;
                float age=.78f+.34f*clustering+.16f*cover;
                float size=(urban?8+hash%7:height<1?12+hash%10:14+hash%12)*age;
                if(glade)size=14.2f+hash%6;
                if(insetGarden)size=3.4f+hash%3*.68f;
                // Groups of one flowering cultivar read as a grove from the opposite lake bank.
                float orchard=Mathf.PerlinNoise(p.x*.015f+2.6f,p.z*.015f+14.3f);
                bool plantedBlossom=blossomBed>.14f && (blossomBed>.48f || hash%4!=0);
                bool pondLawn=(new Vector2(world.x,world.z)-parkDoor).sqrMagnitude<60f*60f;
                bool flowering=plantedBlossom || !pondLawn && height<26 && orchard>.59f && hash%9<(urban?2:1);
                if(plantedBlossom && !insetGarden)
                    size=Mathf.Clamp(size,12f+2.3f*blossomBed,16f+hash%4);
                if (!flowering && height < 24 && hash%7==0 && urban)
                {
                    var tree = QuarterModules.Tree(root,p,size/5.1f,hash%360);
                    float width=.87f+(hash%9)*.044f;
                    tree.localScale=Vector3.Scale(tree.localScale,new Vector3(width,1,width));
                    QuarterModules.Tint(tree.Find("Foliage"),ArtDirection.Hex(hash%3==0?"CBCEA9":hash%3==1?"E7D8AC":"D9DCC4"));
                    var woodCell=new Vector2Int(Mathf.FloorToInt(p.x/112),Mathf.FloorToInt(p.z/112));
                    if(!understory.TryGetValue(woodCell,out var shrubs))understory.Add(woodCell,shrubs=new StreetPlanting());
                    for(int understoryIndex=0;understoryIndex<5;understoryIndex++)
                    {
                        float angle=hash%371+understoryIndex*2.4f;
                        var clump=p+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(.6f+understoryIndex*.57f);
                        shrubs.Add(understoryIndex%3==0?PlantSpecies.GoldenBush:understoryIndex==1?PlantSpecies.Grass:PlantSpecies.Broadleaf,
                            clump,1.5f+understoryIndex*.29f,.74f+(hash+understoryIndex)%4*.18f,hash%491+understoryIndex);
                    }
                    continue;
                }
                var cell=new Vector2Int(Mathf.FloorToInt(p.x/148),Mathf.FloorToInt(p.z/148));
                if(!woodland.TryGetValue(cell,out var chunk))
                {
                    chunk=new BakedCityMap.ForestChunk{Name="Forest_"+cell.x+"_"+cell.y};
                    woodland.Add(cell,chunk);
                    if(Baking)_forestCapture.Add(chunk);
                }
                int species=flowering?2:hash%5==0?3:0;
                chunk.Parts.Add(new BakedCityMap.ForestPart{
                    Mesh=PaintedWoodland.Template((int)size,species,hash%6,detailed:urban || flowering,grove:species==0 || species==3),Position=p});
            }
            foreach(var bed in understory.Values)bed.Build(root,"林缘灌丛");
            if(Baking)yield break;
            ReportBuildStage("loading_forest_meshes");
            var owner=root.GetComponent<CityLandscapeResources>();
            int done=0;
            foreach(var chunk in woodland.Values)
            {
                PaintedWoodland.Build(root,chunk,owner);
                ReportBuildProgress(.69f+.20f*++done/Mathf.Max(1,woodland.Count));
                if(Time.realtimeSinceStartup>=deadline){yield return null;deadline=Time.realtimeSinceStartup+.016f;}
            }
        }
        private static float SegmentDistanceSquared(Vector3 p,Vector3 a,Vector3 b)
        {
            var d=b-a;
            return (p-a-d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude))).sqrMagnitude;
        }
        // Each query visits only geometry whose expanded bounds intersect its cell.
        private sealed class ForestSpatialIndex<T>
        {
            private const float CellSize=64;
            private readonly Dictionary<Vector2Int,List<T>> _cells=new Dictionary<Vector2Int,List<T>>();
            public void Add(Rect bounds,T item)
            {
                for(int z=Mathf.FloorToInt(bounds.yMin/CellSize);z<=Mathf.FloorToInt(bounds.yMax/CellSize);z++)
                    for(int x=Mathf.FloorToInt(bounds.xMin/CellSize);x<=Mathf.FloorToInt(bounds.xMax/CellSize);x++)
                    {
                        var key=new Vector2Int(x,z);
                        if(!_cells.TryGetValue(key,out var items))_cells.Add(key,items=new List<T>());
                        items.Add(item);
                    }
            }
            public List<T> At(Vector3 p)
            {
                _cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.z/CellSize)),out var items);
                return items;
            }
        }
        private sealed class ForestChunk
        {
            public readonly List<Vector3>[] Vertices=new List<Vector3>[7];
            public readonly List<int>[] Triangles=new List<int>[7];
            public ForestChunk(){for(int i=0;i<Vertices.Length;i++){Vertices[i]=new List<Vector3>();Triangles[i]=new List<int>();}}
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
            var route = bridge.points.Select(p => new Vector3((float)p.x, 0, (float)p.y) / LakeScale).ToArray();
            if (route.Length < 2) return;
            float routeLength = 0;
            for (int i = 1; i < route.Length; i++) routeLength += Vector3.Distance(route[i - 1], route[i]);
            if (routeLength < .001f) return;

            // Keep both landings level until the deck is beyond the street.
            float landing = Mathf.Min(12, routeLength * .2f);
            var sections = new List<Vector3> { route[0] };
            float walked = 0;
            var landingBounds = new[] { landing, routeLength - landing };
            for (int i = 1; i < route.Length; i++)
            {
                float span = Vector3.Distance(route[i - 1], route[i]);
                if (span < .001f) continue;
                foreach (float at in landingBounds)
                    if (at > walked + .001f && at < walked + span - .001f)
                        sections.Add(Vector3.Lerp(route[i - 1], route[i], (at - walked) / span));
                sections.Add(route[i]);
                walked += span;
            }
            var points = sections.ToArray();
            var distance = new float[points.Length];
            var normals = new Vector3[points.Length - 1];
            for (int i = 0; i < normals.Length; i++)
            {
                var span = points[i + 1] - points[i];
                normals[i] = Vector3.Cross(Vector3.up, span.normalized);
                distance[i + 1] = distance[i] + span.magnitude;
            }
            float total = distance[points.Length - 1];
            if (total < .001f) return;
            float peak = bridge.requiredTier == 2 ? 5.5f : 13.5f;
            var right = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                // Both spans use the same cross-section at each bend.
                var before = normals[Mathf.Max(0, i - 1)];
                var after = normals[Mathf.Min(i, normals.Length - 1)];
                var bisector = (before + after).normalized;
                right[i] = bisector / Mathf.Max(.5f, Vector3.Dot(bisector, after));
                float fraction = Mathf.InverseLerp(landing, total - landing, distance[i]);
                points[i].y = GroundStreetY / LakeScale + peak * Mathf.Max(0, Mathf.Sin(Mathf.PI * fraction));
            }

            const float deckHalfWidth = LakeBridgeHalfWidth;
            const float asphaltHalfWidth = 4;
            const float railCenter = 5.65f;
            const float asphaltLift = .007f;
            var deck = new BridgeMesh();
            var asphalt = new BridgeMesh();
            var rail = new BridgeMesh();
            var markings = new BridgeMesh();
            float gateInset = Mathf.Min(5, total * .2f);
            var sides = new[] { -1, 1 };
            for (int i = 1; i < points.Length; i++)
            {
                float start = distance[i - 1], end = distance[i], length = end - start;
                if (length < .001f) continue;
                Vector3 Section(float at, float lateral, float lift = 0)
                {
                    float fraction = (at - start) / length;
                    return Vector3.Lerp(points[i - 1], points[i], fraction)
                        + Vector3.Lerp(right[i - 1], right[i], fraction) * lateral + Vector3.up * lift;
                }
                void Top(BridgeMesh mesh, float from, float to, float left, float rightEdge, float lift, bool trimAtStreet = false)
                {
                    var a = Section(from, left, lift);
                    var b = Section(to, left, lift);
                    var c = Section(to, rightEdge, lift);
                    var d = Section(from, rightEdge, lift);
                    if (trimAtStreet && (from < landing + 4 || to > total - landing - 4))
                        mesh.QuadOutsideStreets(a, b, c, d, _streetRoadEnvelopes, LakeScale);
                    else
                        mesh.Quad(a, b, c, d);
                }
                void Side(BridgeMesh mesh, float from, float to, float edge, float low, float high, int facing)
                {
                    if (facing > 0) mesh.Quad(Section(from, edge, low), Section(from, edge, high),
                        Section(to, edge, high), Section(to, edge, low));
                    else mesh.Quad(Section(to, edge, low), Section(to, edge, high),
                        Section(from, edge, high), Section(from, edge, low));
                }
                Top(deck, start, end, -deckHalfWidth, -asphaltHalfWidth, asphaltLift, true);
                Top(deck, start, end, asphaltHalfWidth, deckHalfWidth, asphaltLift, true);
                Top(asphalt, start, end, -asphaltHalfWidth, asphaltHalfWidth, asphaltLift, true);
                float railStart = Mathf.Max(start, gateInset), railEnd = Mathf.Min(end, total - gateInset);
                foreach (int side in sides)
                {
                    Side(deck, start, end, deckHalfWidth * side, -.7f, asphaltLift, side);
                    if (railStart >= railEnd) continue;
                    float left = side * railCenter - .11f, rightEdge = side * railCenter + .11f;
                    Top(rail, railStart, railEnd, left, rightEdge, 1.49f);
                    Side(rail, railStart, railEnd, left, 1.31f, 1.49f, -1);
                    Side(rail, railStart, railEnd, rightEdge, 1.31f, 1.49f, 1);
                }

                for (int mark = Mathf.FloorToInt(start / 5); mark * 5 < end; mark++)
                {
                    float station = mark * 5;
                    float from = Mathf.Max(railStart, station), to = Mathf.Min(railEnd, station + 2.3f);
                    if (to > from) Top(markings, from, to, -.07f, .07f, asphaltLift + .014f);
                    if (station < railStart || station >= railEnd) continue;
                    foreach (int side in sides)
                    {
                        Prims.Box(root, "Baluster", Section(station, side * railCenter, .7f),
                            new Vector3(.17f, 1.4f, .17f), Mats.Lit(ArtDirection.Cream));
                        if (mark % 5 == 0) LakeLamp(root, Section(station, side * 5.1f));
                    }
                    var pos = Section(station, 0);
                    if (mark % 6 == 0 && pos.y > 2)
                    {
                        var pair=DistrictFinishes.Tile("cutStone",new Vector2(1,2),ArtDirection.Hex("BCB7AC"));
                        foreach(int side in sides)
                        {
                            var foot=Section(station,side*3.15f);
                            Prims.Shape(PrimitiveType.Cylinder,root,"BridgePier",new Vector3(foot.x,foot.y*.5f-.59f,foot.z),
                                new Vector3(1.65f,foot.y*.5f,1.65f),pair);
                        }
                        var cap=Prims.Box(root,"桥墩盖梁",pos-Vector3.up*.96f,new Vector3(10.6f,.69f,2.15f),pair);
                        cap.transform.localRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(points[i]-points[i-1],Vector3.up));
                    }
                }
            }
            LandscapeMesh(root, "ConcreteBridgeDeck", deck.Vertices, deck.Triangles,
                Mats.Lit(ArtDirection.Hex("D6C8B4")));
            LandscapeMesh(root, "BridgeAsphalt", asphalt.Vertices, asphalt.Triangles, IndustrialMaterials.Surface("asphalt", Asphalt, Vector2.one));
            LandscapeMesh(root, "ContinuousRailing", rail.Vertices, rail.Triangles, Mats.Lit(ArtDirection.Hex("D6C8B4")));
            LandscapeMesh(root, "BridgeLaneMark", markings.Vertices, markings.Triangles, Mats.Lit(ArtDirection.Cream));
            foreach (int end in new[] { 0, points.Length - 1 })
            {
                float at = end == 0 ? gateInset : total - gateInset;
                int span = 1;
                while (span < points.Length - 1 && distance[span] < at) span++;
                var forward = points[span] - points[span - 1];
                var gate = new GameObject("ClosedBridgeGate").transform;
                gate.SetParent(root, false);
                gate.localPosition = Vector3.Lerp(points[span - 1], points[span], Mathf.InverseLerp(distance[span - 1], distance[span], at));
                forward.y = 0;
                gate.localRotation = Quaternion.LookRotation(end == 0 ? forward : -forward);
                foreach(int side in new[]{-1,1})Prims.Box(gate,"GatePost",new Vector3(side*5,1.5f,0),new Vector3(.6f,3,.6f),Mats.Lit(ArtDirection.Ink),true);
                Prims.Box(gate,"ClosedBarrier",new Vector3(0,1.1f,0),new Vector3(10,.5f,.35f),Mats.Lit(ArtDirection.Clay),true);
                for(int i=-4;i<=4;i++)Prims.Box(gate,"BarrierStripe",new Vector3(i,1.1f,-.19f),new Vector3(.4f,.5f,.03f),Mats.Lit(ArtDirection.Cream));
                Prims.Box(gate,"BridgeSign",new Vector3(0,3.4f,0),new Vector3(10,1.5f,.3f),Mats.Lit(ArtDirection.Ink));
                Prims.SignText(gate,bridge.name+" · 暂未开放",new Vector3(0,3.4f,-.18f),9.5f,1.2f,ArtDirection.Cream);
            }
            var reflections=new LakesideReflections(root,LakeWaterLevel);
            foreach(var part in root.GetComponentsInChildren<MeshRenderer>())
                if(part.name=="ContinuousRailing" || part.name=="ConcreteBridgeDeck")reflections.Add(part);
            reflections.Build();
            BatchStatic(root.gameObject);
        }

        private sealed class BridgeMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Vertices.Add(d);
                Triangles.Add(first);
                Triangles.Add(first + 1);
                Triangles.Add(first + 2);
                Triangles.Add(first);
                Triangles.Add(first + 2);
                Triangles.Add(first + 3);
            }

            public void QuadOutsideStreets(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                RoadSurfaceGeometry.Index streets, float scale)
            {
                // Clip the two faces before lifting them back to the bridge grade.
                AddFace(a, b, c);
                AddFace(a, c, d);

                Vector2 Plan(Vector3 point) => new Vector2(point.x, point.z) * scale;
                float Cross(Vector2 x, Vector2 y) => x.x * y.y - x.y * y.x;

                void AddFace(Vector3 start, Vector3 middle, Vector3 end)
                {
                    var origin = Plan(start);
                    var u = Plan(middle) - origin;
                    var v = Plan(end) - origin;
                    float area = Cross(u, v);
                    if (Mathf.Abs(area) < .0001f) return;
                    var outline = new[] { origin, Plan(end), Plan(middle) };
                    foreach (var piece in streets.Cut(new List<Vector2[]> { outline }, outline))
                    {
                        int first = Vertices.Count;
                        foreach (var point in piece)
                        {
                            var delta = point - origin;
                            float height = start.y + (middle.y - start.y) * Cross(delta, v) / area
                                + (end.y - start.y) * Cross(u, delta) / area;
                            Vertices.Add(new Vector3(point.x / scale, height, point.y / scale));
                        }
                        for (int i = 1; i + 1 < piece.Length; i++)
                        {
                            Triangles.Add(first);
                            Triangles.Add(first + i + 1);
                            Triangles.Add(first + i);
                        }
                    }
                }
            }
        }
        private static void LakeLamp(Transform root,Vector3 p)
        {
            Prims.Box(root,"LakesideLamp",p+Vector3.up*2.3f,new Vector3(.16f,4.6f,.16f),Mats.Lit(ArtDirection.Ink));
            Prims.Shape(PrimitiveType.Sphere,root,"LampLantern",p+Vector3.up*4.65f,Vector3.one*.55f,Mats.Glow(ArtDirection.Cream,new Color(3,2.5f,1.8f)));
        }
        private void BuildShoreFurniture(Transform root,List<Vector3> shore)
        {
            var detail=new GameObject("ShorePromenades").transform;detail.SetParent(root,false);
            var bridgeSpans = new List<(Vector3 A, Vector3 B)>();
            foreach (var bridge in _city.LakeBridges)
            {
                var points = bridge.points.Select(p => new Vector3((float)p.x, 0, (float)p.y) / LakeScale).ToArray();
                for (int i = 1; i < points.Length; i++) bridgeSpans.Add((points[i - 1], points[i]));
            }
            bool NearBridge(Vector3 point, float margin)
            {
                point.y = 0;
                return bridgeSpans.Any(span => SegmentDistance(point, span.A, span.B) < LakeBridgeHalfWidth + margin);
            }
            var shoreBeds=new Dictionary<Vector2Int,StreetPlanting>();
            var reflections=new LakesideReflections(detail,LakeWaterLevel);
            for(int i=0;i<shore.Count;i+=2)
            {
                if(!BuildsLakeSceneryAt(shore[i]))continue;
                var inward=(new Vector3(-25,0,90)-shore[i]).normalized;
                var key=new Vector2Int(Mathf.FloorToInt(shore[i].x/90),Mathf.FloorToInt(shore[i].z/90));
                if(!shoreBeds.TryGetValue(key,out var bed)) shoreBeds.Add(key,bed=new StreetPlanting());
                var tangent=Vector3.Cross(Vector3.up,inward);
                for(int j=0;j<19;j++)
                {
                    int seed=i*37+j*11;
                    var p=shore[i]+tangent*((j-9)*1.19f)+inward*(.4f+2.7f*Mathf.Sin(seed*1.63f));
                    p.y=LakeWaterLevel-.13f;
                    if (NearBridge(p, 1)) continue;
                    bed.Add(j%4==0?PlantSpecies.SilverGrass:PlantSpecies.Cattail,p,1.72f+j%4*.39f,.97f+j%3*.38f,seed);
                    if(j%3==0)bed.Add(j%2==0?PlantSpecies.GoldenBush:PlantSpecies.Hedge,p-inward*(2.8f+j%3),2.3f,1.20f+j%4*.17f,seed+6);
                }
            }
            foreach(var bed in shoreBeds.Values)bed.Build(detail,"湿地芦苇和灌木");
            var roads=new List<(Vector3 a,Vector3 b)>();
            foreach(var node in _city.Map.Nodes)foreach(var edge in _city.Map.NeighborsOf(node.Id))
                if(string.CompareOrdinal(node.Id,edge.To)<0)roads.Add((LakeNode(node.Id),LakeNode(edge.To)));
            for(int i=0;i<shore.Count;i+=2)
            {
                Vector3 outward=(shore[i]-new Vector3(-25,0,90)).normalized;
                Vector3 p=shore[i]+outward*6;
                if(!BuildsLakeSceneryAt(p) || NearBridge(p, 4) || roads.Any(r=>SegmentDistance(p,r.a,r.b)<7))continue;
                int kind=i%9==0?1:i%5==0?2:0;
                reflections.Add(PaintedWoodland.Plant(detail,p,9+i%5,kind,i+1));
                if(i%4==0)
                {
                    var mid=Vector3.Lerp(shore[i],shore[(i+1)%shore.Count],.62f)+outward*1.5f;
                    if(!NearBridge(mid,6) && !roads.Any(r=>SegmentDistance(mid,r.a,r.b)<7))
                        reflections.Add(PaintedWoodland.Plant(detail,mid,7+i%5,i%8==0?1:3,i+37));
                }
                var edge=shore[i]+outward*2.9f+Vector3.Cross(Vector3.up,outward)*2.3f;
                if(i%3==0 && !NearBridge(edge,5) && !roads.Any(r=>SegmentDistance(edge,r.a,r.b)<7))
                    reflections.Add(PaintedWoodland.Plant(detail,edge,5,1,i+13));
            }
            // Viewing decks and reed islands are decoration; no new gameplay destinations.
            foreach(var pos in new[]{new Vector3(610,0,230),new Vector3(-70,0,-310),new Vector3(-70,0,675)})
            {
                if (!BuildsLakeSceneryAt(pos)) continue;
                Prims.Box(detail,"ViewingDeck",pos+Vector3.up*.4f,new Vector3(12,.6f,8),Mats.Lit(ArtDirection.Wood));
                for(int i=-5;i<=5;i++)Prims.Box(detail,"DeckJoint",pos+new Vector3(i,.71f,0),new Vector3(.025f,.02f,8),Mats.Lit(ArtDirection.Ink));
                foreach(int s in new[]{-1,1})LakeLamp(detail,pos+new Vector3(s*5,0,3));
            }
            for(int i=0;i<shore.Count;i+=3)
            {
                var p=shore[i];if (!BuildsLakeSceneryAt(p)) continue;
                Vector3 toward=(new Vector3(-25,0,90)-p).normalized;
                for(int j=0;j<3;j++)
                {
                    Vector3 at=p+toward*(2+j*1.3f)+new Vector3(j,0,0);
                    if (NearBridge(at, 1)) continue;
                    Prims.Shape(PrimitiveType.Sphere,detail,"ShoreRock",at+Vector3.up*.3f,new Vector3(1.7f,.9f,1.3f),Mats.Lit(ArtDirection.Hex("A5AE8D")));
                }
            }
            foreach(var n in _city.Map.Nodes.Where(n=>n.Id.StartsWith("lake_park_")&&StableCityHash(n.Id)%5==0 && BuildsLakeSceneryAt(LakeNode(n.Id))))
            {Vector3 p=LakeNode(n.Id);LakeLamp(detail,p+new Vector3(7,0,0));}
            reflections.Build();
            BatchStatic(detail.gameObject);
        }
        private void BuildLakeIslands(Transform root)
        {
            var islands=new GameObject("ScenicLakeIslands").transform;islands.SetParent(root,false);
            var reflections=new LakesideReflections(islands,LakeWaterLevel);
            foreach(var center in new[]{new Vector3(360,0,350),new Vector3(-580,0,300)})
            {
                if (!BuildsLakeSceneryAt(center)) continue;
                var contour=new List<Vector3>();
                for(int i=0;i<36;i++)
                {
                    float angle=i*Mathf.PI*2/36,r=1+.12f*Mathf.Sin(i*1.7f);
                    contour.Add(center+new Vector3(Mathf.Cos(angle)*22*r,0,Mathf.Sin(angle)*11*r));
                }
                // These small islands use their own fan center, unlike the main lake.
                var v=new List<Vector3>{center+Vector3.up*.45f};v.AddRange(contour.Select(p=>p+Vector3.up*.4f));var t=new List<int>();
                for(int i=0;i<36;i++){t.Add(0);t.Add((i+1)%36+1);t.Add(i+1);}
                LandscapeMesh(islands,"IslandBank",v,t,DistrictFinishes.Tile("cultivatedSoil",Vector2.one));
                for(int i=0;i<v.Count;i++)v[i]=center+(v[i]-center)*.9f+Vector3.up*.08f;
                LandscapeMesh(islands,"IslandGrass",v,t,DistrictFinishes.Meadow());
                var undergrowth=new StreetPlanting();
                for(int j=0;j<contour.Count;j++)
                {
                    var rim=contour[j];
                    undergrowth.Add(j%3==0?PlantSpecies.Reeds:PlantSpecies.Grass,Vector3.Lerp(center,rim,.98f)+Vector3.up*.31f,2.9f,1.18f,j);
                    undergrowth.Add(j%4==0?PlantSpecies.Spirea:PlantSpecies.Broadleaf,Vector3.Lerp(center,rim,.76f)+Vector3.up*.49f,3.6f,1.62f,j*11);
                }
                undergrowth.Build(islands,"岛岸植被");
                for(int i=-2;i<=2;i++)
                {
                    var tree=new GameObject("IslandTree").transform;tree.SetParent(islands,false);tree.localPosition=center+new Vector3(i*6,.5f,Mathf.Sin(i*4)*3);tree.localScale=Vector3.one*1.7f;
                    CityVegetation.Build(tree,Vector3.zero,i+8);
                }
            }
            foreach(var tree in islands.GetComponentsInChildren<MeshRenderer>())
                if(tree.name=="Foliage" || tree.name=="Street")reflections.Add(tree);
            reflections.Build();
            BatchStatic(islands.gameObject);
        }
        internal static PCShopSell.Core.World.LayoutLakeParcel TailorBenchPlot(PCShopSell.Core.World.CityLayout city,out float side)
        {
            side=0;
            const string shopId="expansion_shop_s_market_03";
            if(!city.HasPlace(shopId))return null;
            var shop=city.GetPlace(shopId);
            var node=ToWorld(city.ViewPosition(shop.NodeId));
            var door=node+new Vector3((float)shop.EntranceX,0,(float)shop.EntranceY);
            PCShopSell.Core.World.LayoutLakeParcel selected=null;
            float distance=32f*32f;
            foreach(var plot in city.LakeParcels)
            {
                if(plot.kind!="plaza")continue;
                var offset=door-new Vector3((float)plot.x,0,(float)plot.y);
                if(offset.sqrMagnitude>=distance)continue;
                selected=plot;distance=offset.sqrMagnitude;
                side=Mathf.Sign(Vector3.Dot(offset,Quaternion.Euler(0,(float)plot.heading,0)*Vector3.right));
            }
            return selected;
        }

        private void BuildLakeCourtyards(Transform parent)
        {
            var tailorBench=TailorBenchPlot(_city,out float benchSide);
            var gardens=new GameObject("AuthoredCourtyardGardens").transform;gardens.SetParent(parent,false);int index=0;
            var frontages=_city.PublicPlaces.Where(place=>!IsLakeAccessNode(place.NodeId))
                .Select(place=>(door:DoorXZ(place),district:ArtDistrict(place.StreetId))).ToArray();
            Vector3 rays=DayNightLighting.TownDaylightRotation*Vector3.forward;
            Vector3 groundRays=Vector3.ProjectOnPlane(rays,Vector3.up);
            float shadowReach=groundRays.magnitude/Mathf.Max(.001f,-rays.y);
            foreach(var parcel in _city.LakeParcels)
            {
                var position = new Vector3((float)parcel.x, 0, (float)parcel.y) / LakeScale;
                if (!BuildsLakeSceneryAt(position)) continue;
                var root=new GameObject("Courtyard_"+index).transform;root.SetParent(gardens,false);
                root.localPosition=position;root.localRotation=Quaternion.Euler(0,(float)parcel.heading,0);
                float w=(float)parcel.width/LakeScale,d=(float)parcel.depth/LakeScale;
                bool painted=OnOutskirtsLawn(root.position);
                var courtyard=new Vector2(root.position.x,root.position.z);
                int district=painted ? 0 : frontages.OrderBy(place=>(place.door-courtyard).sqrMagnitude).FirstOrDefault().district;
                var lawn = painted ? QuarterMaterial("DistrictGrass") : DistrictFinishes.Meadow();
                var stone=Mats.Lit(ArtDirection.Hex("D8CEB3"));
                void PromenadeFlowers(bool fountain)
                {
                    if(painted || district==0)return;
                    var verge=new StreetPlanting(maintained:true);
                    // Plant in the four lawn corners. The principal walks stay open to the pool and fountain.
                    foreach(int across in new[]{-1,1})
                    foreach(int along in new[]{-1,1})
                    {
                        int variety=3*(index%4+5+across+along);
                        for(int j=0;j<7;j++)
                        {
                            float angle=.33f+j*.146f;
                            var foot=new Vector3(across*Mathf.Cos(angle)*w*.44f,.15f,d*.5f+along*Mathf.Sin(angle)*d*.45f);
                            if(fountain && (foot-new Vector3(0,foot.y,d*.5f)).sqrMagnitude<16f*16f)continue;
                            verge.Add(j==0 || j==6?PlantSpecies.Spirea:PlantSpecies.Hydrangea,foot,1.55f,.97f+(j%3)*.09f,variety);
                        }
                    }
                    verge.Build(root,"广场边缘的分组花境");
                }
                if(parcel.kind=="park")
                {
                    Garden(root,new Rect(-w/2,0,w,d),false,lawn,customCentre:true);
                    CityVenueView.PromenadePond(root,w,d);
                    for(int j=0;j<12;j++)
                    {
                        float angle=j*Mathf.PI/6;var tree=new GameObject("ParkGrove").transform;tree.SetParent(root,false);
                        tree.localPosition=new Vector3(Mathf.Cos(angle)*w*.43f,.2f,d*.52f+Mathf.Sin(angle)*d*.39f);tree.localScale=Vector3.one*2;
                        if(painted) QuarterModules.Tree(tree,Vector3.zero,1,j*37);
                        else PaintedWoodland.Plant(tree,Vector3.zero,4+j%2,j%7==0?2:0,index*31+j,district:district);
                    }
                    PromenadeFlowers(false);
                    index++;continue;
                }
                if(parcel.kind=="fountain")
                {
                    Garden(root,new Rect(-w/2,0,w,d),false,lawn,customCentre:!painted);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralPlazaRing",new Vector3(0,.14f,d*.5f),new Vector3(29,.12f,29),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainBasin",new Vector3(0,.45f,d*.5f),new Vector3(20,.38f,20),Mats.Lit(ArtDirection.Cream));
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainWater",new Vector3(0,.85f,d*.5f),new Vector3(18.8f,.04f,18.8f),IndustrialMaterials.GardenWater());
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralFountainPedestal",new Vector3(0,1.2f,d*.5f),new Vector3(3,.5f,3),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CentralWaterJet",new Vector3(0,3,d*.5f),new Vector3(.25f,1.5f,.25f),Mats.Lit(ArtDirection.Hex("C4E8E7"),.7f));
                    for(int j=0;j<8;j++)
                    {float a=j*Mathf.PI/4;Prims.Shape(PrimitiveType.Cylinder,root,"FountainJet",new Vector3(Mathf.Cos(a)*3,1.5f,d*.5f+Mathf.Sin(a)*3),new Vector3(.12f,.65f,.12f),Mats.Lit(ArtDirection.Hex("C4E8E7"),.7f));}
                    PromenadeFlowers(true);
                    index++;continue;
                }
                Prims.Box(root,"GardenRetainingEdge",new Vector3(0,.18f,d/2),new Vector3(w,.35f,d),stone);
                Prims.Box(root,"InsetLawn",new Vector3(0,.37f,d/2),new Vector3(w-.6f,.12f,d-.6f),
                    lawn);
                Prims.Box(root,"GardenPath",new Vector3(0,.46f,d/2),new Vector3(1.4f,.08f,d),
                    DistrictFinishes.Tile(painted?"limestoneWalk":"graniteWalk",new Vector2(.36f,d/4f),painted?ArtDirection.Hex("E7DEC9"):Color.white));
                var planting=new StreetPlanting(maintained:!painted);
                Vector3 upSun=root.InverseTransformDirection(-groundRays.normalized);
                bool wideCourt=w>14 && d>14;
                foreach(int side in new[]{-1,1})
                {
                    for(float along=1.25f;along<d-1;along+=1.30f)
                    {
                        int seed=index+Mathf.RoundToInt(along);
                        planting.Add(PlantSpecies.Boxwood,new Vector3(side*(w/2-.82f),.43f,along),1.47f,.74f,seed);
                        if(seed%7<5)
                            planting.Add(PlantSpecies.Hydrangea,new Vector3(side*(w/2-1.58f),.43f,along+.15f),
                                1.61f,1.08f+seed%3*.11f,index*7+Mathf.FloorToInt(along/4.8f)*3);
                        if(!painted && district>0 && along<d-1.9f && seed%7<4)
                            planting.Add(PlantSpecies.Spirea,new Vector3(side*(w/2-2.13f),.43f,along+.62f),
                                1.17f,.60f,seed+19);
                    }
                    var foot=new Vector3(side*w*(side<0?.24f:.31f),.44f,d*(side<0?.60f:.75f));
                    int mainHeight=side<0?8:6;
                    if(district==1 && !wideCourt && w>8 && d>5 && side*upSun.x>.55f)
                    {
                        // Small plazas have only these two trees. A mature tree on the sunward
                        // side shades the opposite seat across the path without blocking either.
                        Vector3 farSeat=new Vector3(-side*w*.25f,.44f,d*.22f);
                        foot=farSeat+upSun*((mainHeight*.71f-.65f)*shadowReach);
                        foot.x=side*Mathf.Clamp(side*foot.x,1.65f,w*.5f-1f);
                        foot.z=Mathf.Clamp(foot.z,1.35f,d-1.25f);
                    }
                    bool grove=!painted && DistrictFinishes.BlossomCover(root.TransformPoint(foot))>.12f;
                    PaintedWoodland.Plant(root,foot,mainHeight,grove || index%7==0 && side<0?2:0,index*41+side*23,!painted,district);
                    if (!painted && wideCourt)
                    {
                        var edge=new Vector3(side*w*.34f,.44f,d*(side<0?.23f:.41f));
                        int height=5+(index&1);
                        if(district==1)
                        {
                            // The afternoon-lit side of each seat has a canopy overhead. Keep its
                            // trunk in the lawn strip with clearance to the stone walk and edging.
                            Vector3 seat=new Vector3(side*w*.25f,.44f,d*.22f);
                            edge=seat+upSun*((height*.75f-.65f)*shadowReach)+Vector3.forward*.66f;
                            edge.x=side*Mathf.Clamp(side*edge.x,1.85f,w*.5f-1.25f);
                            edge.z=Mathf.Clamp(edge.z,1.45f,d-1.45f);
                            // In a narrow bed a smaller ornamental tree keeps the crown over the
                            // seat instead of casting its shadow beyond the opposite retaining wall.
                            float available=Vector3.Dot(edge-seat,upSun);
                            height=Mathf.Clamp(Mathf.RoundToInt((available/Mathf.Max(.001f,shadowReach)+.65f)/.75f),4,height);
                        }
                        PaintedWoodland.Plant(root,edge,height,index%5==0?2:0,index+side*37,district:district);
                        for(int cluster=0;cluster<6;cluster++)
                        {
                            var at=edge+new Vector3(Mathf.Cos(cluster)*1.7f,0,Mathf.Sin(cluster)*1.05f);
                            planting.Add(cluster%3==0?PlantSpecies.Hydrangea:PlantSpecies.Spirea,at,1.65f,.94f,index+cluster);
                        }
                    }
                    for(int group=0;group<5;group++)
                    {
                        float angle=group*.77f+side*.46f;
                        var at=foot+new Vector3(side*(1.05f+Mathf.Cos(angle)*.80f),0,Mathf.Sin(angle)*1.40f);
                        planting.Add(group%3==0?PlantSpecies.Boxwood:PlantSpecies.Hydrangea,at,1.45f,1.04f+group*.055f,index+group);
                    }
                    var seatAt=new Vector3(side*w*.25f,.46f,d*.22f);
                    float yaw=parcel==tailorBench && side==benchSide ? 0 : 180;
                    if(painted)QuarterModules.Add(root,"Bench",seatAt,Vector3.one,yaw);
                    else DistrictFinishes.SlattedBench(root,seatAt,yaw);
                }
                planting.Build(root,"Garden flowering borders");
                if(index%5==0&&d>12)
                {
                    Prims.Shape(PrimitiveType.Cylinder,root,"CourtyardBasin",new Vector3(0,.8f,d*.52f),new Vector3(3,.35f,3),stone);
                    Prims.Shape(PrimitiveType.Cylinder,root,"CourtyardWater",new Vector3(0,1.17f,d*.52f),new Vector3(2.6f,.035f,2.6f),IndustrialMaterials.GardenWater());
                }
                index++;
            }
            BatchStatic(gardens.gameObject);
        }
        private void BuildLakeExhibitionHall(Transform parent)
        {
            // Landmark backdrop for the existing convention district, not another gameplay shop.
            var root=new GameObject("ConventionDistrictExhibitionHall").transform;root.SetParent(parent,false);root.localPosition=new Vector3(525,0,310);
            var glass=Mats.Lit(ArtDirection.Hex("607E90"),.65f);var white=Mats.Lit(ArtDirection.Cream);
            Prims.Box(root,"CivicPlaza",new Vector3(0,.05f,20),new Vector3(65,.1f,52),Mats.Lit(Sidewalk));
            Prims.Box(root,"Hall",new Vector3(0,7,20),new Vector3(52,14,36),glass);
            foreach(int side in new[]{-1,1})for(int i=0;i<7;i++)
                Prims.Box(root,"FacadeFrame",new Vector3(side*26.1f,7,3+i*5.7f),new Vector3(.5f,14,.5f),white);
            for(int i=-4;i<=4;i++)Prims.Box(root,"LobbyFrame",new Vector3(i*6,7,1.8f),new Vector3(.45f,14,.5f),white);
            for(int bay=-1;bay<=1;bay++)
            {
                var roof=Prims.Box(root,"BlueRoofBay",new Vector3(bay*17.5f,14.8f,20),new Vector3(17,1.2f,37),Mats.Lit(ArtDirection.Hex("526C83"),.4f));
                foreach(int side in new[]{-1,1})Prims.Box(root,"RoofRib",new Vector3(bay*17.5f+side*8,15.5f,20),new Vector3(.45f,.4f,38),white);
            }
            Prims.Box(root,"EntranceCanopy",new Vector3(0,5,-2),new Vector3(20,.4f,8),white);
            Prims.SignText(root,"会展中心",new Vector3(0,8,1.4f),18,2,ArtDirection.Cream);
            foreach(int side in new[]{-1,1})for(int i=0;i<4;i++)CityVegetation.Build(root,new Vector3(side*29,0,6+i*10),i);
            BatchStatic(root.gameObject);
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
