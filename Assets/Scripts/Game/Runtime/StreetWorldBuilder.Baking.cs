using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        public bool Baking {get;private set;}
        private readonly bool _firstChapterTrial;
        private readonly List<BakedCityMap.ForestChunk> _forestCapture=new List<BakedCityMap.ForestChunk>();
        private Transform _bakedForestRoot;
        private readonly Dictionary<int,Mesh[]> _forestTemplates=new Dictionary<int,Mesh[]>();
        private readonly List<GameObject> _bakeBatchRoots=new List<GameObject>();
        private void BatchStatic(GameObject root)
        {
            if(Baking)_bakeBatchRoots.Add(root);
            else StaticBatchingUtility.Combine(root);
        }
        private readonly List<Transform> _detailRoots=new List<Transform>();
        private readonly List<BakedDetailLayout.Group> _detailGroups=new List<BakedDetailLayout.Group>();
        public void PrepareBakeCapture()
        {
            Baking=true;
            MiniatureDetailMesh.BakeModelSink=(root,material,parts)=>{
                _detailRoots.Add(root);_detailGroups.Add(new BakedDetailLayout.Group {Material=material,Parts=parts});
            };
        }
        public void EndBakeCapture()=>MiniatureDetailMesh.BakeModelSink=null;
        public BakedCityMap CaptureBakedMap(string layoutJson)
        {
            if(!Baking)throw new InvalidOperationException("地图未进入预生成模式");
            var data=_root.gameObject.AddComponent<BakedCityMap>();
            data.LayoutHash=BakedCityMap.HashLayout(layoutJson);data.Trial=_firstChapterTrial;data.Bounds=CityBounds;
            data.Places=Places.Values.Select(v=>new BakedCityMap.PlaceRecord {
                Id=v.Place.Id,Entrance=v.Entrance,LabelPos=v.LabelPos,Root=v.Root,Exterior=v.Exterior,Interior=v.VenueInterior,
                Sign=v.Sign,OpenOnly=v.OpenOnly.ToArray(),ClosedOnly=v.ClosedOnly.ToArray(),Neon=v.Neon }).ToArray();
            data.Markers=_root.GetComponentsInChildren<PlaceMarker>(true).Select(m=>new BakedCityMap.MarkerRecord {Target=m.gameObject,PlaceId=m.PlaceId}).ToArray();
            data.GroundMarkers=_root.GetComponentsInChildren<GroundMarker>(true).Select(m=>m.gameObject).ToArray();
            data.NightLights=NightLights.ToArray();
            data.Glows=Mats.NightGlow.Select(g=>new BakedCityMap.GlowRecord {Material=g.mat,Emission=g.baseEmission,Daylight=g.daylight}).ToArray();
            data.Ramps=_lakeAccessSurfaces.Select(r=>new BakedCityMap.RampRecord {
                A=r.A,B=r.B,Width=r.Width,PavedWidth=r.PavedWidth,Deck=r.Deck,DryStart=r.DryStart,DryEnd=r.DryEnd,
                RampLength=r.RampLength,Raised=r.Raised,GroundAtA=r.GroundAtA,GroundAtB=r.GroundAtB }).ToArray();
            data.Races=Places.Values.SelectMany(v=>v.Root.GetComponentsInChildren<RaceTrackView>(true)
                .Select(r=>new BakedCityMap.RaceRecord {Root=r.gameObject,PlaceId=v.Place.Id})).ToArray();
            data.Expos=Places.Values.SelectMany(v=>v.Root.GetComponentsInChildren<QuarterlyExpoView>(true)
                .Select(e=>new BakedCityMap.ExpoRecord {Root=e.gameObject,Width=(float)v.Place.Width,Depth=(float)v.Place.Depth})).ToArray();
            data.StaticBatches=_bakeBatchRoots.ToArray();
            data.DetailRoots=_detailRoots.ToArray();
            data.Details=ScriptableObject.CreateInstance<BakedDetailLayout>();data.Details.Groups=_detailGroups.ToArray();
            data.ForestRoot=_bakedForestRoot;data.Forest=_forestCapture.ToArray();
            return data;
        }
        private void RestoreBakedMap(BakedCityMap data)
        {
            _root=data.transform;CityBounds=data.Bounds;LimitTrialScenery();Mats.ClearNightGlow();
            foreach(var g in data.Glows)Mats.RestoreBakedGlow(g.Material,g.Emission,g.Daylight);
            NightLights.AddRange(data.NightLights);
            foreach(var label in _root.GetComponentsInChildren<UnityEngine.UI.Text>(true))label.font=PCShopSell.Game.UI.Ui.Font;
            foreach(var p in data.Places)
            {
                var v=new PlaceVisual {Place=_city.GetPlace(p.Id),Entrance=p.Entrance,LabelPos=p.LabelPos,Root=p.Root,
                    Exterior=p.Exterior,VenueInterior=p.Interior,Sign=p.Sign,Neon=p.Neon};
                v.OpenOnly.AddRange(p.OpenOnly);v.ClosedOnly.AddRange(p.ClosedOnly);Places.Add(p.Id,v);
                if(v.Exterior!=null&&v.VenueInterior!=null)_venueVisuals.Add(v);
            }
            foreach(var m in data.Markers)m.Target.AddComponent<PlaceMarker>().PlaceId=m.PlaceId;
            foreach(var g in data.GroundMarkers)g.AddComponent<GroundMarker>();
            foreach(var r in data.Ramps)_lakeAccessSurfaces.Add(new StreetSegment {A=r.A,B=r.B,Width=r.Width,
                PavedWidth=r.PavedWidth,Deck=r.Deck,DryStart=r.DryStart,DryEnd=r.DryEnd,RampLength=r.RampLength,
                Raised=r.Raised,GroundAtA=r.GroundAtA,GroundAtB=r.GroundAtB});
            foreach(var r in data.Races)r.Root.AddComponent<RaceTrackView>().RestoreBaked(RaceTracks.Get(r.PlaceId,_city));
            foreach(var e in data.Expos)e.Root.AddComponent<QuarterlyExpoView>().Initialize(e.Width,e.Depth);
        }
        private IEnumerator LoadBakedMap(BakedCityMap prefab)
        {
            ReportBuildStage("loading_baked_map");
            var instance=UnityEngine.Object.Instantiate(prefab);
            instance.name="City";
            RestoreBakedMap(instance);
            ReportBuildProgress(.44f);yield return RestoreBakedDetails(instance);
            foreach(var race in instance.Races)
            {
                var scenery=race.Root.transform.Find("CircuitScenery");
                if(scenery!=null)StaticBatchingUtility.Combine(scenery.gameObject);
            }
            foreach(var batch in instance.StaticBatches)
            {
                StaticBatchingUtility.Combine(batch);
                yield return null;
            }
            ReportBuildProgress(.60f);yield return RestoreBakedForest(instance);
            ReportBuildProgress(.96f);
        }
        private IEnumerator RestoreBakedDetails(BakedCityMap data)
        {
            ReportBuildStage("loading_baked_details");
            var groups=data.Details.Groups;
            float deadline=Time.realtimeSinceStartup+.016f;
            for(int index=0;index<groups.Length;index++)
            {
                var group=groups[index];var root=data.DetailRoots[index];
                // Dynamic expo children were intentionally omitted from the baked map.
                if(root==null)continue;
                var parts=group.Parts.Select(p=>new CombineInstance {mesh=p.Mesh,
                    transform=Matrix4x4.TRS(p.Position,BakedDetailLayout.ValidRotation(p.Rotation),p.Scale)}).ToArray();
                var mesh=new Mesh {name=root.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts,true,true,false);
                var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<uv.Length;i++)
                {
                    var n=normals[i];var p=vertices[i];
                    uv[i]=Mathf.Abs(n.y)>.6f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.6f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                }
                mesh.uv=uv;if(group.Material.IsKeywordEnabled("_NORMALMAP"))mesh.RecalculateTangents();
                root.GetComponent<MeshFilter>().sharedMesh=mesh;
                var owner=root.GetComponent<CityLandscapeResources>()??root.gameObject.AddComponent<CityLandscapeResources>();owner.Owned.Add(mesh);
                ReportBuildProgress(.44f+.16f*(index+1)/Mathf.Max(1,groups.Length));
                if(Time.realtimeSinceStartup>=deadline){yield return null;deadline=Time.realtimeSinceStartup+.016f;}
            }
        }
        private static readonly string[] ForestColors={"3F683C","608437","7C9938","A8B643","B87594","866342","E5A9BE"};
        private IEnumerator RestoreBakedForest(BakedCityMap data)
        {
            if(data.ForestRoot==null)yield break;
            ReportBuildStage("loading_forest_meshes");
            var owner=data.ForestRoot.gameObject.AddComponent<CityLandscapeResources>();
            var forest=data.ForestData.Chunks;
            int done=0,total=forest.Length*ForestColors.Length;
            float deadline=Time.realtimeSinceStartup+.016f;
            foreach(var chunk in forest)for(int color=0;color<ForestColors.Length;color++)
            {
                var parts=chunk.Parts.Where(p=>p.Color==color).Select(p=>new CombineInstance {
                    mesh=p.Mesh,transform=Matrix4x4.Translate(p.Position) }).ToArray();
                if(parts.Length>0)
                {
                    var mesh=new Mesh {name=chunk.Name+"_"+color,indexFormat=IndexFormat.UInt32};
                    mesh.CombineMeshes(parts,true,true,false);
                    var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
                    go.transform.SetParent(data.ForestRoot,false);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial=Mats.Lit(ArtDirection.Hex(ForestColors[color]));
                    owner.Owned.Add(mesh);
                }
                ReportBuildProgress(.60f+.36f*++done/Mathf.Max(1,total));
                if(Time.realtimeSinceStartup>=deadline){yield return null;deadline=Time.realtimeSinceStartup+.016f;}
            }
        }
        private static Mesh ForestTemplateMesh(List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh {name="共用树林模型",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            // Forest materials are untextured. Local UVs preserve the same shading.
            mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x/7,v.z/7)).ToList());
            return mesh;
        }
        private Mesh[] CaptureTreeTemplate(int key,float size,bool conifer,int seed)
        {
            if(_forestTemplates.TryGetValue(key,out var meshes))return meshes;
            var template=new ForestTreeTemplate(size,conifer,seed);
            meshes=template.ExportMeshes();_forestTemplates.Add(key,meshes);return meshes;
        }
        private Mesh CaptureUnderstoreyTemplate(int key,Action<List<Vector3>,List<int>> build)
        {
            if(_forestTemplates.TryGetValue(key,out var meshes))return meshes[0];
            var v=new List<Vector3>();var t=new List<int>();build(v,t);
            var mesh=ForestTemplateMesh(v,t);_forestTemplates.Add(key,new[]{mesh});return mesh;
        }
        private void CaptureForestTree(BakedCityMap.ForestChunk chunk,Vector3 p,float size,bool conifer,int seed,int color,int tip,int hash,bool urban)
        {
            void Part(Mesh mesh,int shade,Vector3 at)=>chunk.Parts.Add(new BakedCityMap.ForestPart {Mesh=mesh,Color=shade,Position=at});
            int key=seed+((int)size<<5)+(conifer?1<<10:0);
            var tree=CaptureTreeTemplate(key,size,conifer,seed);
            Part(tree[0],color,p);Part(tree[1],tip,p);Part(tree[2],5,p);
            if(!urban&&hash%2!=0)return;
            for(int shrub=0;shrub<5;shrub++)
            {
                int j=shrub;
                var mesh=CaptureUnderstoreyTemplate(2000+j,(v,t)=>MiniatureDetailMesh.Lobe(v,t,Vector3.zero,new Vector3(1.15f,.75f,1.05f),j,true));
                float angle=j*2.4f+hash%13;
                Part(mesh,1+j%2,p+new Vector3(Mathf.Cos(angle)*2.2f,.6f+j%2*.25f,Mathf.Sin(angle)*2.2f));
            }
            if(urban&&hash%3==0)Part(CaptureUnderstoreyTemplate(2100,(v,t)=>AddCrown(v,t,Vector3.zero,new Vector3(1.15f,.6f,1.1f),false)),4,p+new Vector3(1.8f,1.2f,-1.3f));
            if(urban&&hash%2==0)for(int j=0;j<3;j++)
            {
                int grassSeed=hash%47+j*7;
                Part(CaptureUnderstoreyTemplate(2200+grassSeed,(v,t)=>AddGrassTuft(v,t,Vector3.zero,grassSeed)),3,p);
            }
        }
    }
}
