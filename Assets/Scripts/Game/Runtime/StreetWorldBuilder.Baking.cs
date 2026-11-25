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
            var residentialBlocks = _city.Blocks.Where(block => _city.HasPlace(block.homeId)).ToDictionary(block => block.homeId);
            foreach(var p in data.Places)
            {
                residentialBlocks.TryGetValue(p.Id, out var block);
                var v=new PlaceVisual {Place=_city.GetPlace(p.Id),Entrance=p.Entrance,LabelPos=p.LabelPos,Root=p.Root,
                    Exterior=p.Exterior,VenueInterior=p.Interior,Sign=p.Sign,Neon=p.Neon,ResidentialBlock=block};
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
        private IEnumerator RestoreBakedForest(BakedCityMap data)
        {
            if(data.ForestRoot==null)yield break;
            ReportBuildStage("loading_forest_meshes");
            var owner=data.ForestRoot.GetComponent<CityLandscapeResources>()??data.ForestRoot.gameObject.AddComponent<CityLandscapeResources>();
            var forest=data.ForestData.Chunks;
            float deadline=Time.realtimeSinceStartup+.016f;
            for(int i=0;i<forest.Length;i++)
            {
                PaintedWoodland.Build(data.ForestRoot,forest[i],owner);
                ReportBuildProgress(.60f+.36f*(i+1)/Mathf.Max(1,forest.Length));
                if(Time.realtimeSinceStartup>=deadline){yield return null;deadline=Time.realtimeSinceStartup+.016f;}
            }
        }
    }
}
