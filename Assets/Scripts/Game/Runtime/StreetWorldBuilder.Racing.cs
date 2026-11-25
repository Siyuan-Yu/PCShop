using System.Collections.Generic;
using PCShopSell.Core.World;
using UnityEngine;
namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void BuildRegionalGarage(Transform root,Place p,Vector3 node,Vector3 door,PlaceVisual vis)
        {
            // A compact annex beside the existing reception, not a new parking island.
            float w=(float)p.Width,d=(float)p.Depth;
            Prims.Box(root,"GarageFoundation",new Vector3(0,.12f,d*.5f),new Vector3(w+.5f,.45f,d+.5f),Mats.Lit(ArtDirection.Hex("858B77")));
            var body=Prims.Box(root,"GarageAnnex",new Vector3(0,1.8f,d*.5f),new Vector3(w,3.6f,d),
                IndustrialMaterials.Surface("brick",ArtDirection.Hex("D6BE9E"),new Vector2(w/4,1.2f)),true);
            body.AddComponent<PlaceMarker>().PlaceId=p.Id;
            int district=ArtDistrict(p.StreetId);
            if(district>=2)
            {
                var deck=new Vector3(0,3.78f,d*.5f);
                GardenStructures.Plate(root,"维修站轻型檐板",deck,new Vector3(w+.54f,.25f,d+.72f),Mats.Lit(ArtDirection.Hex("E3EAE8")));
                if(district==3)
                {
                    var service=new MiniatureDetailMesh();
                    GardenStructures.SolarFins(service,deck+Vector3.up*.14f,w*.66f,d*.61f,3);
                    service.Build(root,"维修站顶棚光伏");
                }
            }
            else
            {
                Prims.Box(root,"GarageRoof",new Vector3(0,3.7f,d*.5f),new Vector3(w+.4f,.25f,d+.4f),
                    IndustrialMaterials.Surface("metal",ArtDirection.Hex("48627A"),new Vector2(w/4,d/5)));
            }
            Prims.Box(root,"RollerDoor",new Vector3(0,1.5f,-.06f),new Vector3(w-1,2.8f,.15f),Mats.Lit(ArtDirection.Blue));
            for(int i=0;i<7;i++)Prims.Box(root,"DoorSlat",new Vector3(0,.35f+i*.36f,-.16f),new Vector3(w-1,.035f,.025f),Mats.Lit(ArtDirection.Ink));
            Prims.SignText(root,"车库",new Vector3(0,3.2f,-.2f),w-.7f,.45f,ArtDirection.Cream);
            var delta=door-node;
            if(!_city.IsLakeCity && delta.sqrMagnitude>.1f)
            {
                var step=Prims.Box(root,"GarageWalkway",root.InverseTransformPoint((door+node)*.5f)+Vector3.up*.08f,
                    new Vector3(1.8f,.38f,delta.magnitude),Mats.Lit(ArtDirection.Hex("858B77")));
                step.transform.rotation=Quaternion.LookRotation(delta);
            }
            vis.LabelPos=root.TransformPoint(new Vector3(0,4.5f,d*.35f));
        }
        // The two service buildings share a node. Their configured offsets differ at the park
        // circuit, so size the waterside apron from both real footprints in reception space.
        private void BuildRacePier(Transform root,int tier)
        {
            if(!_city.IsLakeCity || tier==0 || !_city.HasPlace("garage_"+tier))return;
            var garage=_city.GetPlace("garage_"+tier);
            var garageDoor=DoorXZ(garage);
            var garageCenter=new Vector3(garageDoor.x,root.position.y,garageDoor.y)
                +Quaternion.Euler(0,(float)garage.Heading,0)*Vector3.forward*(float)garage.Depth*.5f;
            var bounds=new Bounds(Vector3.zero,new Vector3(9,0,6));
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 corner=garageCenter+Quaternion.Euler(0,(float)garage.Heading,0)*
                    new Vector3(x*(float)garage.Width*.5f,0,z*(float)garage.Depth*.5f);
                bounds.Encapsulate(root.InverseTransformPoint(corner));
            }
            bounds.Expand(new Vector3(4.8f,0,4.8f));
            var plate=DistrictFinishes.Tile("limestoneWalk",new Vector2(bounds.size.x/4,bounds.size.z/4));
            var midpoint=new Vector3(bounds.center.x,-.27f,bounds.center.z);
            Prims.Box(root,"赛场临水服务平台",midpoint,new Vector3(bounds.size.x,.51f,bounds.size.z),plate);
            var trim=new MiniatureDetailMesh();
            var metal=Mats.Lit(ArtDirection.Hex(tier==3?"8EA9AE":"657972"),.37f);
            var edge=DistrictFinishes.Tile("cutStone",Vector2.one*.4f);
            void Rails(Vector3 a,Vector3 b)
            {
                int spans=Mathf.CeilToInt(Vector3.Distance(a,b)/2.15f);
                for(int i=0;i<spans;i++)
                {
                    Vector3 from=Vector3.Lerp(a,b,(float)i/spans),to=Vector3.Lerp(a,b,(i+1f)/spans);
                    var world=root.TransformPoint((from+to)*.5f);
                    // The existing road and its crossing remain open along the wharf.
                    var at=new Vector2(world.x,world.z);
                    var probe=RoadSurfaceGeometry.Strip(at-Vector2.right*1.4f,at+Vector2.right*1.4f,2.8f);
                    if(_streetPavementEnvelopes!=null && System.Linq.Enumerable.Any(_streetPavementEnvelopes.Nearby(probe),
                        id=>RoadSurfaceGeometry.Intersect(probe,_streetPavementEnvelopes.Polygons[id]).Length>=3))continue;
                    trim.Box(from+Vector3.up*.49f,new Vector3(.09f,.98f,.09f),metal);
                    trim.Box(to+Vector3.up*.49f,new Vector3(.09f,.98f,.09f),metal);
                    GardenStructures.Strut(trim,from+Vector3.up*.93f,to+Vector3.up*.93f,.065f,.065f,metal);
                    GardenStructures.Strut(trim,from+Vector3.up*.51f,to+Vector3.up*.51f,.045f,.045f,metal);
                }
            }
            var farLeft=new Vector3(bounds.min.x,0,bounds.max.z);
            var farRight=new Vector3(bounds.max.x,0,bounds.max.z);
            Rails(farLeft,farRight);
            Rails(farRight,new Vector3(bounds.max.x,0,bounds.center.z));
            foreach(int x in new[]{-1,1})
            foreach(int z in new[]{-1,1})
            {
                var at=midpoint+new Vector3(x*(bounds.extents.x-.55f),-.44f,z*(bounds.extents.z-.55f));
                trim.Box(at,new Vector3(.72f,1.28f,.72f),edge);
            }
            trim.Build(root,"平台临水栏杆与桥桩");
        }
        private static void BuildRaceEntrance(Transform root,Vector3 door,PCShopSell.Core.Session.RaceTrack track)
        {
            var edge=new Vector3(
                Mathf.Clamp(door.x,(float)(track.X-track.Width/2+4),(float)(track.X+track.Width/2-4)),0,
                Mathf.Clamp(door.z,(float)(track.Y-track.Depth/2+4),(float)(track.Y+track.Depth/2-4)));
            var delta=edge-door;delta.y=0;
            if(delta.sqrMagnitude<.01f)return;
            var center = (door + edge) * .5f;
            center.y = Mathf.Max(.32f, door.y - .01f) - .3f;
            var landing=Prims.Box(root,"RaceEntranceLanding",root.InverseTransformPoint(center),
                new Vector3(10,.6f,delta.magnitude+10),Mats.Lit(ArtDirection.Hex("858B77")));
            landing.transform.rotation=Quaternion.LookRotation(delta);
            var resources=root.gameObject.AddComponent<CityLandscapeResources>();
            var normal=Vector3.Cross(Vector3.up,delta.normalized)*3.5f;
            var start=door-delta.normalized*5;start.y=Mathf.Max(.26f,door.y+.02f);
            var end=edge+delta.normalized*3+Vector3.up*.82f;
            var vertices=new[]{root.InverseTransformPoint(start-normal),root.InverseTransformPoint(end-normal),root.InverseTransformPoint(start+normal),root.InverseTransformPoint(end+normal)};
            var mesh=new Mesh{name="RaceEntranceRamp"};mesh.vertices=vertices;mesh.triangles=new[]{0,1,2,2,1,3};mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var ramp=new GameObject("RaceEntranceRamp",typeof(MeshFilter),typeof(MeshRenderer));ramp.transform.SetParent(root,false);
            ramp.GetComponent<MeshFilter>().sharedMesh=mesh;ramp.GetComponent<MeshRenderer>().sharedMaterial=Mats.Lit(ArtDirection.Hex("41494D"));
        }
        private void BuildRaceCauseways()
        {
            var root=new GameObject("LakeRaceCauseways").transform;root.SetParent(_root,false);
            root.gameObject.AddComponent<CityLandscapeResources>();
            const float bottom = -.30f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var segment in _lakeAccessSurfaces)
            {
                var outline = _streetPavementEnvelopes.Polygons[segment.PavingIndex];
                for (int i = 0; i < outline.Length; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Length];
                    var delta = b - a;
                    var outward = new Vector2(delta.y, -delta.x).normalized * .015f;
                    var probe = RoadSurfaceGeometry.Strip(a + outward, b + outward, .02f);
                    var covered = new List<(float From, float To)>();
                    foreach (int id in _streetPavementEnvelopes.Nearby(probe))
                    {
                        if (id == segment.PavingIndex) continue;
                        if (RoadSurfaceGeometry.ClipSegment(a + outward, b + outward,
                            _streetPavementEnvelopes.Polygons[id], out float from, out float to))
                            covered.Add((from, to));
                    }
                    covered.Sort((left, right) => left.From.CompareTo(right.From));
                    float start = 0;
                    foreach (var interval in covered)
                    {
                        AddExposedWall(start, interval.From);
                        start = Mathf.Max(start, interval.To);
                    }
                    AddExposedWall(start, 1);

                    void AddExposedWall(float from, float to)
                    {
                        if (to <= from + .00001f) return;
                        var cuts = new List<float> { from, to };
                        var direction = segment.Direction;
                        float alongDelta = Vector2.Dot(delta, direction);
                        if (Mathf.Abs(alongDelta) > .0001f)
                        {
                            float alongA = Vector2.Dot(a - segment.A, direction);
                            foreach (float distance in segment.RampBoundaries())
                            {
                                float cut = (distance - alongA) / alongDelta;
                                if (cut > from && cut < to) cuts.Add(cut);
                            }
                            cuts.Sort();
                        }
                        for (int j = 1; j < cuts.Count; j++)
                        {
                            var left = a + delta * cuts[j - 1];
                            var right = a + delta * cuts[j];
                            float leftHeight = segment.HeightAt(left);
                            float rightHeight = segment.HeightAt(right);
                            if (Mathf.Max(leftHeight, rightHeight) <= GroundStreetY + .001f) continue;
                            int index = vertices.Count;
                            vertices.Add(new Vector3(left.x, bottom, left.y));
                            vertices.Add(new Vector3(left.x, leftHeight, left.y));
                            vertices.Add(new Vector3(right.x, bottom, right.y));
                            vertices.Add(new Vector3(right.x, rightHeight, right.y));
                            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                            triangles.Add(index + 2); triangles.Add(index + 1); triangles.Add(index + 3);
                        }
                    }
                }
            }
            if (vertices.Count > 0) LandscapeMesh(root, "RaceAccessCauseway", vertices, triangles, Mats.Lit(ArtDirection.Hex("858B77")));
            float scale=(float)_city.ExpansionScale;
            float bankTop = LakeAccessHeight(_city) - .015f;
            Prims.Box(root,"StarterMotorHubBank",new Vector3(-550*scale,(bankTop+bottom)*.5f,-135*scale),new Vector3(65*scale,bankTop-bottom,65*scale),
                HasStarterArt ? QuarterMaterial("DistrictGrass") : IndustrialMaterials.Surface("meadow",ArtDirection.Hex("7D9859"),new Vector2(4,4)));
            var planting=new MiniatureDetailMesh();
            var hub=new Vector3(-550*scale,bankTop,-135*scale);
            int seed=0;
            foreach(int side in new[]{-1,1})for(float offset=-26*scale;offset<27*scale;offset+=9)
            {
                var at=hub+new Vector3(side*29*scale,0,offset);
                var p2=new Vector2(at.x,at.z);
                var probe=RoadSurfaceGeometry.Strip(p2-Vector2.up*3,p2+Vector2.up*3,6);
                bool clear=true;
                foreach(int id in _streetPavementEnvelopes.Nearby(probe))
                    if(RoadSurfaceGeometry.Intersect(probe,_streetPavementEnvelopes.Polygons[id]).Length>=3){clear=false;break;}
                if(!clear)continue;
                foreach(var place in _city.PublicPlaces)
                {
                    var door=NodeWorld(place.NodeId)+new Vector3((float)place.EntranceX,0,(float)place.EntranceY);
                    var local=Quaternion.Euler(0,-(float)place.Heading,0)*(at-door);
                    if(Mathf.Abs(local.x)<(float)place.Width/2+4&&local.z>-5&&local.z<(float)place.Depth+4){clear=false;break;}
                }
                if(!clear)continue;
                CityVegetation.Build(root,at,seed++);
                planting.FlowerBed(at+Vector3.forward*1.6f,2.5f,1.2f,seed,false);
            }
            planting.Build(root,"Motor hub waterside planting");
            BatchStatic(root.gameObject);
        }
    }
}
