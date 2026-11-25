using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed class RaceTrackView:MonoBehaviour
    {
        public RaceTrack Track {get;private set;}
        private Transform _moving,_player,_gate;private Transform[] _rivals=Array.Empty<Transform>();
        private readonly Renderer[] _startLamps = new Renderer[3];
        private TrailRenderer[] _skidMarks = Array.Empty<TrailRenderer>();
        private ParticleSystem _tyreSmoke;
        private int _visibleStartLights = -1, _previousRecoveries;
        private bool _wasDrifting;
        public void Build(RaceTrack track,bool combineScenery=true, Material districtLawn = null)
        {
            Track=track;transform.position=new Vector3((float)track.X,.65f,(float)track.Y);
            var scenery=new GameObject("CircuitScenery").transform;scenery.SetParent(transform,false);
            bool starter = track.Tier == 0;
            var resources=gameObject.AddComponent<CityLandscapeResources>();
            BuildIslandShore(scenery,resources,districtLawn);

            Ribbon(scenery,(float)track.HalfRoad+1.3f,.14f,Mats.Lit(ArtDirection.Cream),resources);
            Ribbon(scenery,(float)track.HalfRoad,.18f,OldTownSurfaces.PanelMaterial("streetAsphalt"),resources);
            BuildBarriers(scenery, resources);
            BuildStartingGantry(scenery);
            var stand = starter ? new Vector3(-35, 0, -19) : new Vector3(-10, 0, 0);
            var pit = starter ? new Vector3(-11.5f, 0, -14) : new Vector3(15, 0, 8);
            for (int i = 0; i < 4; i++)
                Prims.Box(scenery, "Grandstand", stand + new Vector3(0, .6f + i * .6f, i * 2),
                    new Vector3(24, .7f, 2), Mats.Lit(i % 2 == 0 ? ArtDirection.Wood : ArtDirection.Cream));
            if (track.Tier == 3)
                BuildTimingCabin(scenery,pit);
            else
            {
                Prims.Box(scenery,"PitOffice",pit+Vector3.up*2.2f,new Vector3(12,4.3f,6),DistrictFinishes.Tile(track.Tier==1?"buffBrick":"ivoryPlaster",new Vector2(2,1)));
                Prims.Box(scenery,"PitRoof",pit+Vector3.up*4.5f,new Vector3(14,.5f,8),CircuitRoof());
            }
            BuildCircuitGarden(scenery,stand,pit);
            if (starter) BuildStarterLandmarks(scenery, resources);

            if(starter)
            {
                QuarterModules.Add(scenery,"TinCanopy",pit+new Vector3(0,3.05f,-3.06f),new Vector3(1.38f,1,1));
                QuarterModules.Add(scenery,"Condenser",pit+new Vector3(-3.5f,4.76f,.75f),Vector3.one*1.24f);
                QuarterModules.Add(scenery,"CargoPallet",pit+new Vector3(4.74f,.09f,2.20f),Vector3.one);
            }
            else if(track.Tier==1)
            {
                QuarterModules.Add(scenery,"CycleShelter",pit+new Vector3(-8f,.12f,0),Vector3.one);
                for(int i=0;i<3;i++)QuarterModules.Add(scenery,"Cycle",pit+new Vector3(-9+i*.74f,.15f,0),Vector3.one,12);
            }
            if (track.Kerbs.Length > 0) BuildKerbs(scenery, resources);
            for(int i=0;i<4;i++){Prims.Box(scenery,"FloodlightPole",new Vector3((i%2==0?-1:1)*(float)track.Width*.43f,4,(i<2?-1:1)*(float)track.Depth*.43f),new Vector3(.3f,8,.3f),Mats.Lit(ArtDirection.Ink));}
            if(combineScenery)StaticBatchingUtility.Combine(scenery.gameObject);
            _moving=new GameObject("RaceVehicles").transform;_moving.SetParent(transform,false);_moving.gameObject.SetActive(false);
        }
        public void RestoreBaked(RaceTrack track)
        {
            Track=track;
            _moving=transform.Find("RaceVehicles");
            var signals=transform.Find("StartSignals");
            for(int i=0;i<_startLamps.Length;i++)_startLamps[i]=signals.Find("StartSignal"+(i+1)).GetComponent<Renderer>();
        }
        private Vector3 Point(int i){var p=Track.Points[i%Track.Points.Length];return new Vector3((float)p.X,0,(float)p.Y);}

        private void BuildIslandShore(Transform scenery,CityLandscapeResources owner,Material districtLawn)
        {
            float x=(float)Track.Width*.5f,z=(float)Track.Depth*.5f;
            // Round the island itself. The independent driving ribbon and its runoff use the race points.
            var rim=new List<Vector3>();
            int samples=Mathf.CeilToInt(4*(x+z)/3.4f);
            for(int i=0;i<samples;i++)
            {
                float a=2*Mathf.PI*i/samples;
                var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float radial=1/Mathf.Pow(Mathf.Pow(Mathf.Abs(direction.x)/x,4)+Mathf.Pow(Mathf.Abs(direction.z)/z,4),.25f);
                var point=direction*radial;
                float variation=Mathf.PerlinNoise(point.x*.040f+31,point.z*.040f+17);
                rim.Add(point+direction*(variation-.5f)*5.8f);
            }
            var turfVertices=new List<Vector3>{ Vector3.up*.075f };
            var turfTriangles=new List<int>();
            for(int i=0;i<samples;i++)
            {
                turfVertices.Add(rim[i]+Vector3.up*.075f);
                turfTriangles.Add(0);turfTriangles.Add(1+(i+1)%samples);turfTriangles.Add(1+i);
            }
            var turfMesh=new Mesh{name="赛道岛草地"};
            turfMesh.SetVertices(turfVertices);turfMesh.SetTriangles(turfTriangles,0);turfMesh.RecalculateNormals();turfMesh.RecalculateBounds();
            owner.Owned.Add(turfMesh);
            var turf=new GameObject("Infield",typeof(MeshFilter),typeof(MeshRenderer));
            turf.transform.SetParent(scenery,false);turf.GetComponent<MeshFilter>().sharedMesh=turfMesh;
            turf.GetComponent<MeshRenderer>().sharedMaterial=districtLawn ?? DistrictFinishes.Meadow();
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();
            var plants=new StreetPlanting();var door=transform.parent==null?Vector3.zero:scenery.InverseTransformPoint(transform.parent.position);
            var access=new Vector3(Mathf.Clamp(door.x,-x,x),0,Mathf.Clamp(door.z,-z,z));
            for(int i=0;i<rim.Count;i++)
            {
                var at=rim[i];var across=Vector3.Cross(Vector3.up,(rim[(i+1)%rim.Count]-rim[(i+rim.Count-1)%rim.Count]).normalized);
                if(Vector3.Dot(at,across)<0)across=-across;
                float irregular=Mathf.PerlinNoise(at.x*.11f+17,at.z*.11f+23);
                var shelf=at+across*(1.8f+irregular*2.7f);
                var wet=at+across*(4.1f+irregular*4.5f);
                foreach(var pt in new[]{at+Vector3.up*.075f,shelf+Vector3.down*.14f,wet+Vector3.down*.65f})
                {vertices.Add(pt);uv.Add(new Vector2(pt.x,pt.z)*.27f);}
                int a=i*3,b=((i+1)%rim.Count)*3;
                indices.AddRange(new[]{a,b+1,b,a,a+1,b+1,a+1,b+2,b+1,a+1,a+2,b+2});
                if(Vector3.Distance(access,at)<13)continue;
                plants.Add(i%4==0?PlantSpecies.Broadleaf:PlantSpecies.Reeds,
                    Vector3.Lerp(shelf,wet,.46f)+Vector3.down*.27f,3.6f,1.28f+irregular*.65f,i+Track.Tier*19);
                if(i%7==1)plants.Add(PlantSpecies.GoldenBush,shelf+Vector3.down*.17f,2.74f,1.46f,i);
                if(i%3==1 && irregular>.24f)PaintedWoodland.Plant(scenery,shelf+Vector3.down*.14f,6+i%4,i%9==1?1:0,i,Track.Tier>0);
            }
            // Keep the sloped shelf visible from above on every section of the irregular bank.
            for(int i=0;i<indices.Count;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                if(Vector3.Cross(b-a,c-a).y<0)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);
            }
            var mesh=new Mesh{name="赛道护岸"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();owner.Owned.Add(mesh);
            var bank=new GameObject("Natural circuit bank",typeof(MeshFilter),typeof(MeshRenderer));
            bank.transform.SetParent(scenery,false);bank.GetComponent<MeshFilter>().sharedMesh=mesh;
            bank.GetComponent<MeshRenderer>().sharedMaterial=DistrictFinishes.Tile("cultivatedSoil",Vector2.one);
            plants.Build(scenery,"赛道沿岸植被");
        }

        private Material CircuitRoof()
        {
            string tint = Track.Tier switch { 0 => "8393A0", 1 => "467762", 2 => "D2DAD6", _ => "C1D1D3" };
            return IndustrialMaterials.Surface("metal",ArtDirection.Hex(tint),new Vector2(4,2));
        }

        private void BuildCircuitGarden(Transform scenery,Vector3 stand,Vector3 pit)
        {
            var details=new MiniatureDetailMesh();
            var timber=IndustrialMaterials.Surface("wood",ArtDirection.Hex("A77E50"),Vector2.one);
            var metal=Mats.Lit(ArtDirection.Hex("405363"));
            var seatPaint=Track.Tier switch {
                1 => Mats.Lit(ArtDirection.Hex("51836A")), 2 => Mats.Lit(ArtDirection.Hex("E0E4DF")),
                3 => Mats.Lit(ArtDirection.Hex("416A83")), _ => timber };
            for(int row=0;row<4;row++)for(int seat=-5;seat<=5;seat++)
            {
                var at=stand+new Vector3(seat*2,1.1f+row*.6f,row*2);
                details.Box(at,new Vector3(1.5f,.12f,.65f),seatPaint);
                details.Box(at+new Vector3(0,.38f,.32f),new Vector3(1.5f,.6f,.10f),seatPaint);
            }
            foreach(int side in new[]{-1,1})
            {
                if (Track.Tier==0) details.Box(stand+new Vector3(side*12,3,3),new Vector3(.18f,6,.18f),metal);
                details.FlowerBed(pit+new Vector3(side*4.3f,.12f,-3.7f),2.5f,.8f,side+9);
            }
            if(Track.Tier==0)
            {
                var roof = CircuitRoof();
                details.Box(stand+new Vector3(0,6.2f,3),new Vector3(26,.25f,9),roof);
                for (int i=-12;i<=12;i++)
                    details.Box(stand+new Vector3(i,6.36f,3),new Vector3(.056f,.075f,8.95f),metal);
                for(int i=-2;i<=2;i++)
                {
                    float x=i*5.75f;
                    details.Box(stand+new Vector3(x,3.2f,6),new Vector3(.16f,6.3f,.16f),metal);
                    details.Box(stand+new Vector3(x,3.1f,-.75f),new Vector3(.14f,6.05f,.14f),metal);
                    details.Box(stand+new Vector3(x,5.94f,2.7f),new Vector3(.13f,.22f,8.9f),timber);
                }
                details.Box(stand+new Vector3(0,2.9f,6.58f),new Vector3(24.2f,.9f,.15f),
                    DistrictFinishes.Tile(Track.Tier==1?"buffBrick":"walnut",new Vector2(6,1)));
            }
            else BuildDistrictGrandstand(scenery,details,stand);
            if(Track.Tier<3)
            {
                for(int i=-5;i<=5;i++)
                    details.Box(pit+new Vector3(i,4.81f,0),new Vector3(.055f,.09f,7.9f),metal);
                details.Window(pit+new Vector3(-3,2.5f,-3.15f),2.7f,1.7f,ArtDirection.Cream,true);
                details.Window(pit+new Vector3(3,2.5f,-3.15f),2.7f,1.7f,ArtDirection.Cream,true);
            }
            var border = new StreetPlanting(maintained:Track.Tier>0);
            // Only decorate clear infield: the driving ribbon and runoff stay empty.
            for(float x=-(float)Track.Width*.33f;x<(float)Track.Width*.34f;x+=13)
            for(float z=-(float)Track.Depth*.30f;z<(float)Track.Depth*.31f;z+=13)
            {
                var at=new Vector3(x,.10f,z);
                if(Mathf.Abs(x-stand.x)<16&&Mathf.Abs(z-stand.z-3)<10 || Mathf.Abs(x-pit.x)<10&&Mathf.Abs(z-pit.z)<8)continue;
                float clearance=float.MaxValue;
                for(int i=0;i<Track.Points.Length;i++)
                {
                    var a=Point(i);var direction=Point(i+1)-a;
                    var closest=a+direction*Mathf.Clamp01(Vector3.Dot(at-a,direction)/Mathf.Max(.001f,direction.sqrMagnitude));
                    clearance=Mathf.Min(clearance,Vector3.Distance(at,closest));
                }
                if(clearance<(float)Track.HalfRoad+7)continue;
                int seed=Mathf.RoundToInt(x*7+z*13);
                if(Track.Tier>0)
                {
                    // Stagger the orchard; the offset stays inside the checked runoff clearance.
                    at+=new Vector3(Mathf.Sin(x*.42f+z)*1.6f,0,Mathf.Cos(z*.37f+x)*1.4f);
                    border.Add(PlantSpecies.Grass,at+Vector3.right*1.2f,2.8f,1.16f,seed+1);
                    border.Add(Track.Tier==3?PlantSpecies.SilverGrass:PlantSpecies.Hydrangea,at+Vector3.forward*1.38f,2.15f,.97f,seed+7);
                }
                PaintedWoodland.Plant(scenery,at,5+Mathf.Abs(seed%3),Track.Tier>0 && seed%4==0 ? 2 : seed%3==0?3:0,seed,Track.Tier>0);
                border.Add(PlantSpecies.Spirea, at + Vector3.left * 2, 1.6f, .85f, seed);
                details.FlowerBed(at+Vector3.forward*1.5f,2.8f,1.3f,seed,false);
            }
            border.Build(scenery,"看台草地与花境");
            details.Build(scenery,"Circuit seating, windows and gardens");
        }

        private void BuildDistrictGrandstand(Transform root, MiniatureDetailMesh detail, Vector3 stand)
        {
            var white=DistrictFinishes.Tile("pearlCeramic",new Vector2(5,2));
            var alloy=Mats.Lit(ArtDirection.Hex("9BADB2"),.42f);
            var glass=DistrictFinishes.Tile("civicGlazing",new Vector2(.31f,.24f));
            var roof=stand+new Vector3(0,6.6f,2.8f);
            var paving=DistrictFinishes.Tile("limestoneWalk",new Vector2(.44f,.44f));
            var bed=new StreetPlanting(maintained:true);
            detail.Box(stand+new Vector3(0,.16f,1.35f),new Vector3(27.4f,.19f,12.6f),paving);
            for(int step=0;step<3;step++)
                detail.Box(stand+new Vector3(0,.23f+step*.13f,-1.82f+step*.46f),new Vector3(22f,.14f,.54f),white);
            foreach(int side in new[]{-1,1})
            {
                int count=Track.Tier==3?14:9;
                for(int k=0;k<count;k++)
                {
                    var at=stand+new Vector3(side*(4.7f+k*8.1f/count),.12f,-4.65f+.34f*Mathf.Sin(k*.6f));
                    bed.Add(Track.Tier==3?PlantSpecies.SilverGrass:k%4==0?PlantSpecies.Hydrangea:PlantSpecies.Boxwood,at,1.52f,.86f,k+side*17);
                }
            }
            bed.Build(root,"看台入口花境");
            if(Track.Tier==1)
            {
                var green=Mats.Lit(ArtDirection.Hex("427361"),.27f);
                var wood=Mats.Lit(ArtDirection.Hex("A78155"));
                var tilt=Quaternion.Euler(-6,0,0);
                detail.Box(roof,new Vector3(25.8f,.28f,9.3f),green,tilt);
                for(int rib=-15;rib<=15;rib++)
                    detail.Box(roof+new Vector3(rib*.82f,.17f,0),new Vector3(.055f,.065f,9.2f),green,tilt);
                foreach(int side in new[]{-1,1})
                {
                    for(int i=0;i<8;i++)
                        detail.Box(stand+new Vector3(side*12.25f,3.1f,-.8f+i*.98f),new Vector3(.21f,5.9f,.23f),wood);
                    detail.FlowerBed(stand+new Vector3(side*10.5f,.1f,-2.45f),3.1f,1.0f,side+13);
                }
                detail.Box(stand+new Vector3(0,3.6f,6.8f),new Vector3(24,3.35f,.18f),wood);
                return;
            }
            bool garden=Track.Tier==3;
            GardenStructures.Plate(root,"看台悬挑翼",roof,new Vector3(garden?28.1f:26.7f,garden?.43f:.24f,10.1f),white);
            if(garden)
            {
                GardenStructures.Plate(root,"翼板下缘",roof-Vector3.up*.24f,new Vector3(27.6f,.15f,9.8f),alloy);
                GardenStructures.SolarFins(detail,roof+Vector3.up*.23f,22,7.35f,8);
                detail.Box(stand+new Vector3(0,4.25f,6.78f),new Vector3(24.6f,3.9f,.15f),glass);
            }
            for(int bay=-1;bay<=1;bay++)
            {
                float x=bay*11.1f;
                var foot=stand+new Vector3(x,.35f,5.55f);
                var joint=foot+new Vector3(0,2.58f,-.23f);
                var forward=stand+new Vector3(x+bay*.53f,6.44f,-1.43f);
                GardenStructures.Strut(detail,foot,joint,garden?.54f:.27f,.43f,white);
                GardenStructures.Strut(detail,joint,forward,garden?.51f:.28f,.37f,white);
                if(bay!=0)
                {
                    var frontFoot=stand+new Vector3(x,.30f,-.85f);
                    var fork=frontFoot+new Vector3(-bay*.60f,2.8f,.22f);
                    GardenStructures.Strut(detail,frontFoot,fork,garden?.44f:.23f,.28f,white);
                    GardenStructures.Strut(detail,fork,forward,.23f,.22f,white);
                    GardenStructures.Strut(detail,fork,forward+new Vector3(-bay*1.53f,0,2.18f),.19f,.20f,white);
                }
                GardenStructures.Strut(detail,joint,stand+new Vector3(x-bay*.36f,6.44f,6.88f),.24f,.27f,alloy);
            }
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<5;i++)
                    detail.Box(stand+new Vector3(side*12.2f,1.05f+i*.47f,i*1.5f),new Vector3(.09f,1.35f,.11f),alloy);
                GardenStructures.Strut(detail,stand+new Vector3(side*12.2f,1.7f,0),stand+new Vector3(side*12.2f,3.55f,6),.075f,.085f,alloy);
                detail.FlowerBed(stand+new Vector3(side*10,.1f,-2.6f),4.4f,1.35f,side+5);
            }
        }

        private static void BuildTimingCabin(Transform root, Vector3 pit)
        {
            var white=DistrictFinishes.Tile("pearlCeramic",new Vector2(3,2));
            var glass=DistrictFinishes.Tile("civicGlazing",new Vector2(8,1));
            var ink=Mats.Lit(ArtDirection.Hex("324D5A"),.33f);
            GardenStructures.Plate(root,"计时舱基座",pit+Vector3.up*.35f,new Vector3(11.8f,.54f,6.6f),white);
            GardenStructures.Plate(root,"计时舱环窗",pit+Vector3.up*2.25f,new Vector3(11.1f,3.46f,5.86f),glass);
            GardenStructures.Plate(root,"计时舱顶",pit+Vector3.up*4.06f,new Vector3(11.75f,.35f,6.4f),white);
            var detail=new MiniatureDetailMesh();
            for(int i=-2;i<=2;i++)
                detail.Box(pit+new Vector3(i*1.92f,2.27f,-2.95f),new Vector3(.105f,3.57f,.18f),white);
            var clock=pit+new Vector3(-6.8f,0,-.54f);
            detail.Box(clock+Vector3.up*2.8f,new Vector3(.35f,5.6f,.46f),white);
            detail.Box(clock+Vector3.up*4.08f,new Vector3(1.45f,2.5f,.42f),ink);
            for(int i=0;i<4;i++)
                detail.Box(clock+new Vector3(0,4.82f-i*.43f,-.225f),new Vector3(.94f-i*.10f,.105f,.045f),Mats.Lit(ArtDirection.Hex("8ACCD3")));
            detail.Build(root,"计时舱窗框与电子计时牌");
        }

        private void BuildStartingGantry(Transform scenery)
        {
            var gantry = new GameObject("StartingGantry").transform;
            gantry.SetParent(scenery, false);
            gantry.localPosition = Point(0);
            gantry.localRotation = Quaternion.LookRotation(Point(1) - Point(0));
            float halfRoad = (float)Track.HalfRoad;
            float postOffset = halfRoad + 1.65f;
            var metal = Mats.Lit(ArtDirection.Ink);
            if (Track.Tier == 0) BuildStarterGrid(gantry);
            else Prims.Box(gantry, "StartFinish", Vector3.up * .22f, new Vector3(2 * halfRoad, .04f, 1.2f), Mats.Lit(ArtDirection.Cream));
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * postOffset;
                Prims.Box(gantry, "GantryFoot", new Vector3(x, .21f, 0), new Vector3(.9f, .24f, 1.05f), metal);
                Prims.Box(gantry, "GantryPost", new Vector3(x, 2.75f, 0), new Vector3(.37f, 5.4f, .45f), metal);
            }
            Prims.Box(gantry, "GantryBeam", Vector3.up * 5.45f, new Vector3(2 * postOffset + .5f, .42f, .5f), metal);
            Prims.Box(gantry, "SignalHousing", new Vector3(0, 5.12f, -.33f), new Vector3(3.35f, 1.04f, .42f), metal);
            // Keep the changing signal materials outside the static circuit batch.
            var signals = new GameObject("StartSignals").transform;
            signals.SetParent(transform, false);
            signals.SetPositionAndRotation(gantry.position, gantry.rotation);
            for (int i = 0; i < _startLamps.Length; i++)
            {
                var lamp = Prims.Shape(PrimitiveType.Sphere, signals, $"StartSignal{i + 1}",
                    new Vector3((i - 1) * 1.02f, 5.12f, -.58f), new Vector3(.74f, .74f, .28f), Mats.Lit(ArtDirection.Ink));
                _startLamps[i] = lamp.GetComponent<Renderer>();
            }
        }

        private void BuildStarterGrid(Transform gantry)
        {
            const int columns = 16;
            float halfRoad = (float)Track.HalfRoad;
            float tile = 2 * halfRoad / columns;
            var white = Mats.Lit(ArtDirection.Cream);
            var dark = Mats.Lit(ArtDirection.Hex("526476"));
            for (int column = 0; column < columns; column++)
            for (int row = 0; row < 2; row++)
                Prims.Box(gantry, "StartFinish", new Vector3(-halfRoad + tile * (column + .5f), .194f, row * .6f - .3f),
                    new Vector3(tile, .02f, .6f), (column + row) % 2 == 0 ? white : dark);

            PaintSlot(0, (float)RaceSimulation.PlayerGridDistance, Mats.Lit(ArtDirection.Brass));
            for (int i = 0; i < RaceSimulation.RivalCount; i++)
                PaintSlot((float)RaceSimulation.RivalLaneOffset(i, halfRoad), (float)RaceSimulation.RivalGridDistance(i), white);

            void PaintSlot(float x, float z, Material color)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Prims.Box(gantry, "GridSlot", new Vector3(x + side, .194f, z), new Vector3(.1f, .02f, 3), color);
                    Prims.Box(gantry, "GridSlot", new Vector3(x, .194f, z + side * 1.5f), new Vector3(2.1f, .02f, .1f), color);
                }
            }
        }

        private static void BuildStarterLandmarks(Transform scenery, CityLandscapeResources resources)
        {
            var rubber = Mats.Lit(ArtDirection.Hex("33383D"));
            var hollow = Mats.Lit(ArtDirection.Hex("171E24"));
            foreach (var (x, z, radius, layers) in new[] { (54f, -22f, 1.45f, 2), (57.4f, -20.5f, 1.3f, 2),
                (56.5f, -26f, 1.6f, 3), (60f, -24f, 1.2f, 1) })
            {
                for (int layer = 0; layer < layers; layer++)
                    Prims.Shape(PrimitiveType.Cylinder, scenery, "OldTyre", new Vector3(x, .25f + layer * .3f, z),
                        new Vector3(radius * 2, .15f, radius * 2), rubber);
                Prims.Shape(PrimitiveType.Cylinder, scenery, "TyreWell", new Vector3(x, .395f + (layers - 1) * .3f, z),
                    new Vector3(radius * 1.15f, .012f, radius * 1.15f), hollow);
            }

            var arrow = new Mesh { name = "StarterDirectionArrow" };
            arrow.vertices = new[] { new Vector3(-.32f, 0, -1.7f), new Vector3(-.32f, 0, .25f),
                new Vector3(-1.06f, 0, .25f), new Vector3(0, 0, 1.65f), new Vector3(1.06f, 0, .25f),
                new Vector3(.32f, 0, .25f), new Vector3(.32f, 0, -1.7f) };
            arrow.triangles = new[] { 0, 1, 5, 0, 5, 6, 2, 3, 4 };
            arrow.RecalculateNormals();
            arrow.RecalculateBounds();
            resources.Owned.Add(arrow);
            foreach (var (x, z, yaw) in new[] { (0f, -47f, 90f), (76f, -22f, 0f), (19f, 21f, -34f),
                (-33f, 42f, -90f), (-76f, -6f, 180f) })
            {
                var mark = new GameObject("DirectionArrow", typeof(MeshFilter), typeof(MeshRenderer));
                mark.transform.SetParent(scenery, false);
                mark.transform.localPosition = new Vector3(x, .195f, z);
                mark.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                mark.GetComponent<MeshFilter>().sharedMesh = arrow;
                mark.GetComponent<MeshRenderer>().sharedMaterial = Mats.Lit(ArtDirection.Cream);
            }
        }

        private void SyncStartingLights(int count)
        {
            if (_visibleStartLights == count) return;
            _visibleStartLights = count;
            var red = Mats.Glow(new Color(1f, .21f, .08f), new Color(3f, .43f, .16f), nightOnly: false);
            var green = Mats.Glow(new Color(.24f, 1f, .32f), new Color(.38f, 2.8f, .52f), nightOnly: false);
            var dark = Mats.Lit(new Color(.19f, .055f, .05f));
            for (int i = 0; i < _startLamps.Length; i++)
                _startLamps[i].sharedMaterial = count == 0 ? green : i < count ? red : dark;
        }

        private void BuildDriftEffects(MotorModel model)
        {
            bool scooter = model.Kind == VehicleKind.EBike;
            bool car = model.Kind == VehicleKind.Car;
            float axle = car ? -1.02f : -.7f;
            float trackWidth = car ? .78f : .65f;
            _skidMarks = new TrailRenderer[scooter ? 1 : 2];
            for (int i = 0; i < _skidMarks.Length; i++)
            {
                var contact = new GameObject("RearTyreMark").transform;
                contact.SetParent(_player, false);
                contact.localPosition = new Vector3(scooter ? 0 : (i == 0 ? -trackWidth : trackWidth), -.02f, axle);
                contact.localRotation = Quaternion.Euler(-90, 0, 0);
                var mark = contact.gameObject.AddComponent<TrailRenderer>();
                mark.sharedMaterial = Mats.Unlit(new Color(.055f, .065f, .08f, .68f));
                mark.alignment = LineAlignment.TransformZ;
                mark.shadowCastingMode = ShadowCastingMode.Off;
                mark.receiveShadows = false;
                mark.time = 4;
                mark.minVertexDistance = .18f;
                mark.startWidth = car ? .18f : .10f;
                mark.endWidth = .025f;
                mark.emitting = false;
                _skidMarks[i] = mark;
            }

            var smoke = new GameObject("TyreSmoke").transform;
            smoke.SetParent(_player, false);
            smoke.localPosition = new Vector3(0, .10f, axle);
            _tyreSmoke = smoke.gameObject.AddComponent<ParticleSystem>();
            _tyreSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _tyreSmoke.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.20f, .38f);
            main.maxParticles = 14;
            var emission = _tyreSmoke.emission;
            emission.rateOverTime = car ? 20 : 13;
            emission.enabled = false;
            var shape = _tyreSmoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = car ? .30f : .15f;
            var size = _tyreSmoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .55f, 1, 1.6f));
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = _startLamps[0].GetComponent<MeshFilter>().sharedMesh;
            renderer.sharedMaterial = Mats.Unlit(new Color(.76f, .79f, .82f, .18f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void SyncDriftEffects(RaceSimulation race)
        {
            bool showing = race.Drifting && !race.Paused && !race.Finished;
            bool recovered = _previousRecoveries != race.Recoveries;
            bool newDrift = showing && !_wasDrifting;
            _previousRecoveries = race.Recoveries;
            _wasDrifting = race.Drifting;
            foreach (var mark in _skidMarks)
            {
                // A fresh slide must not draw a line back to the last corner.
                if (recovered || newDrift) mark.Clear();
                mark.emitting = showing;
            }
            if (race.Paused)
            {
                if (_tyreSmoke.isPlaying) _tyreSmoke.Pause();
                return;
            }
            if (recovered) _tyreSmoke.Clear();
            var emission = _tyreSmoke.emission;
            emission.enabled = showing;
            if ((showing || _tyreSmoke.isPaused) && !_tyreSmoke.isPlaying) _tyreSmoke.Play();
        }

        private void BuildBarriers(Transform parent, CityLandscapeResources resources)
        {
            int count = Track.Points.Length;
            var joins = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var incoming = (Point(i) - Point((i + count - 1) % count)).normalized;
                var outgoing = (Point(i + 1) - Point(i)).normalized;
                var nextNormal = Vector3.Cross(Vector3.up, outgoing);
                var bisector = (Vector3.Cross(Vector3.up, incoming) + nextNormal).normalized;
                // Both neighbouring segments use the same miter joint, with no overlapping end faces.
                joins[i] = bisector / Mathf.Max(.25f, Vector3.Dot(bisector, nextNormal));
            }
            foreach (int side in new[] { -1, 1 })
            {
                var vertices = new List<Vector3>();
                var triangles = new[] { new List<int>(), new List<int>() };
                void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int material)
                {
                    int start = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                    bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) > 0;
                    int[] indices = forward ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                    foreach (int index in indices) triangles[material].Add(start + index);
                }
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    float offset = side * ((float)Track.HalfRoad + 3.8f);
                    var a = Point(i) + joins[i] * (offset - .325f) + Vector3.up * .05f;
                    var b = Point(j) + joins[j] * (offset - .325f) + Vector3.up * .05f;
                    var c = Point(j) + joins[j] * (offset + .325f) + Vector3.up * .05f;
                    var d = Point(i) + joins[i] * (offset + .325f) + Vector3.up * .05f;
                    var height = Vector3.up * 1.1f;
                    var normal = Vector3.Cross(Vector3.up, (Point(j) - Point(i)).normalized);
                    Quad(a, b, b + height, a + height, -normal, i % 2);
                    Quad(d, c, c + height, d + height, normal, i % 2);
                    Quad(a + height, b + height, c + height, d + height, Vector3.up, i % 2);
                    Quad(a, b, c, d, Vector3.down, i % 2);
                }
                var mesh = new Mesh { name = "ContinuousRaceBarrier" };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles[0], 0); mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                resources.Owned.Add(mesh);
                var go = new GameObject("Barrier", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = Track.Tier == 3
                    ? new[] { Mats.Lit(ArtDirection.Hex("4E9391")), Mats.Lit(ArtDirection.Hex("C7D1CC")) }
                    : new[] { Mats.Lit(ArtDirection.Clay), Mats.Lit(ArtDirection.Cream) };
            }
        }
        private Vector3 RoadNormal(int index)
        {
            var tangent = (Point(index + 1) - Point(index + Track.Points.Length - 1)).normalized;
            return Vector3.Cross(Vector3.up, tangent);
        }

        private void BuildKerbs(Transform parent, CityLandscapeResources resources)
        {
            var vertices = new List<Vector3>();
            var faces = new[] { new List<int>(), new List<int>() };
            const float stripeLength = 2.6f;
            foreach (var (first, last, side) in Track.Kerbs)
            {
                float inner = (float)Track.HalfRoad - .12f, outer = (float)Track.HalfRoad + 1.2f;
                float left = side > 0 ? inner : -outer, right = side > 0 ? outer : -inner;
                float travelled = 0;
                for (int i = first; i < last; i++)
                {
                    var a = Point(i) + Vector3.up * .193f;
                    var b = Point(i + 1) + Vector3.up * .193f;
                    var normalA = RoadNormal(i);
                    var normalB = RoadNormal(i + 1);
                    float length = Vector3.Distance(a, b);
                    float end = travelled + length;
                    for (int stripe = Mathf.FloorToInt(travelled / stripeLength); stripe * stripeLength < end; stripe++)
                    {
                        float from = (Mathf.Max(travelled, stripe * stripeLength) - travelled) / length;
                        float to = (Mathf.Min(end, (stripe + 1) * stripeLength) - travelled) / length;
                        if (to <= from) continue;
                        int start = vertices.Count;
                        vertices.Add(Vector3.Lerp(a + normalA * left, b + normalB * left, from));
                        vertices.Add(Vector3.Lerp(a + normalA * left, b + normalB * left, to));
                        vertices.Add(Vector3.Lerp(a + normalA * right, b + normalB * right, to));
                        vertices.Add(Vector3.Lerp(a + normalA * right, b + normalB * right, from));
                        faces[stripe % 2].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                    travelled = end;
                }
            }
            var mesh = new Mesh { name = "RaceKerbs", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(faces[0], 0);
            mesh.SetTriangles(faces[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            resources.Owned.Add(mesh);
            var kerbs = new GameObject("Kerbs", typeof(MeshFilter), typeof(MeshRenderer));
            kerbs.transform.SetParent(parent, false);
            kerbs.GetComponent<MeshFilter>().sharedMesh = mesh;
            kerbs.GetComponent<MeshRenderer>().sharedMaterials = Track.Tier == 3
                    ? new[] { Mats.Lit(ArtDirection.Hex("4E9391")), Mats.Lit(ArtDirection.Hex("C7D1CC")) }
                    : new[] { Mats.Lit(ArtDirection.Clay), Mats.Lit(ArtDirection.Cream) };
        }

        private void Ribbon(Transform parent,float half,float height,Material mat,CityLandscapeResources resources)
        {
            int n=Track.Points.Length;var v=new Vector3[n*2];var tri=new int[n*6];
            for(int i=0;i<n;i++){var normal=RoadNormal(i);var at=Point(i)+Vector3.up*height;v[i*2]=at-normal*half;v[i*2+1]=at+normal*half;int j=(i+1)%n;int k=i*6;tri[k]=i*2;tri[k+1]=j*2;tri[k+2]=i*2+1;tri[k+3]=i*2+1;tri[k+4]=j*2;tri[k+5]=j*2+1;}
            var mesh=new Mesh{name="RaceRoad"};mesh.vertices=v;mesh.triangles=tri;
            var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++) uv[i]=new Vector2(v[i].x,v[i].z)/4;
            mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Owned.Add(mesh);
            var go=new GameObject("RoadRibbon",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        public void Begin(RaceSimulation race)
        {
            foreach(Transform c in _moving)Destroy(c.gameObject);
            _moving.gameObject.SetActive(true);_player=MotorVisual.Build(_moving,race.Model,MotorVisual.Paint(race.Model.Id));
            BuildDriftEffects(race.Model);
            _previousRecoveries = race.Recoveries;
            _wasDrifting = false;
            if (race.WetSurface)
                for (int i = 0; i < race.Track.Points.Length; i += 6)
                {
                    var point = race.Track.Points[i];
                    Prims.Box(_moving, "WetPracticePatch", new Vector3((float)point.X, .055f, (float)point.Y),
                        new Vector3(3.2f, .015f, 2), Mats.Lit(new Color(.18f,.26f,.32f)));
                }
            if (race.SponsorSticker)
            {
                bool car = MotorCatalog.ClassOf(race.Model.Kind) == 2;
                Prims.Box(_player, "PhoneShopSponsorSticker", new Vector3(car ? .78f : .24f, car ? .85f : .64f, 0),
                    new Vector3(.025f, car ? .32f : .20f, car ? .65f : .42f), Mats.Lit(ArtDirection.Brass));
            }
            if (MotorCatalog.ClassOf(race.Model.Kind) != 2)
            {
                var helmet = _player.Find("Helmet");
                var jacket = _player.Find("RiderJacket");
                if (helmet != null && race.ProtectiveHelmet) helmet.GetComponent<Renderer>().sharedMaterial = Mats.Lit(ArtDirection.Blue);
                if (jacket != null && race.ProtectiveSuit) jacket.GetComponent<Renderer>().sharedMaterial = Mats.Lit(ArtDirection.Clay);
            }
            _rivals=new Transform[race.RivalProgress.Length];
            for(int i=0;i<_rivals.Length;i++)
            {
                var model = race.RivalMotor(i);
                var paint = Color.Lerp(MotorVisual.Paint(model.Id), i % 2 == 0 ? ArtDirection.Clay : ArtDirection.Blue, .30f + (i % 3) * .12f);
                _rivals[i] = MotorVisual.Build(_moving, model, paint);
                _rivals[i].name = i < race.Opponents.Count ? "Rival_" + race.Opponents[i].PersonId : "Rival_" + (i + 1);
            }
            _gate=Prims.Box(_moving,"NextCheckpoint",Vector3.zero,new Vector3((float)Track.HalfRoad*2,.12f,1),Mats.Unlit(new Color(.3f,1,.55f))).transform;
            Sync(race);
        }
        public void Sync(RaceSimulation r)
        {
            PlaceMotor(_player,r.X,r.Y,r.Heading);
            SyncStartingLights(r.StartLights);
            SyncDriftEffects(r);
            for(int i=0;i<_rivals.Length;i++){double h;var p=r.RivalPosition(i,out h);PlaceMotor(_rivals[i],p.X,p.Y,h);}
            var next=Point(r.Next);_gate.localPosition=next+Vector3.up*.27f;_gate.localRotation=Quaternion.LookRotation(Point(r.Next+1)-Point(r.Next));
            _gate.gameObject.SetActive(r.StartLights == 0 && !r.ShowGo);
        }
        private static void PlaceMotor(Transform t,double x,double y,double h){t.localPosition=new Vector3((float)x,.23f,(float)y);t.localRotation=Quaternion.Euler(0,(float)h*Mathf.Rad2Deg,0);}
        public void End()
        {
            SyncStartingLights(-1);
            _moving.gameObject.SetActive(false);
        }
    }
    public static class MotorVisual
    {
        internal static readonly Vector3 ScooterPelvis = new Vector3(0, .96f, -.14f);
        private const float ScooterGripOffset = .33f;
        internal const float ScooterDeckHeight = .43f;
        internal const float ScooterAnkleDepth = .22f;

        internal static Vector3 ScooterGrip(float side) => new Vector3(side * ScooterGripOffset, 1.19f, .34f);

        public static Color Paint(string modelId)
        {
            switch(modelId)
            {
                case "e_city": return ArtDirection.Hex("A8D6AC");
                case "e_range": return ArtDirection.Hex("7796BE");
                case "e_turn": return ArtDirection.Hex("EE8A74");
                case "e_dash": return ArtDirection.Hex("F4B427");
                case "e_race": return ArtDirection.Hex("20CBC0");
                case "t_cargo": return ArtDirection.Hex("537B43");
                case "t_sport": return ArtDirection.Hex("2455DA");
                case "v_basic": return ArtDirection.Hex("DADCE2");
                case "v_cargo": return ArtDirection.Hex("E7BD55");
                case "c_city": return ArtDirection.Hex("80B9DD");
                case "c_su7": return ArtDirection.Hex("A0D7D2");
                case "c_718": return ArtDirection.Hex("ECE1CB");
                case "c_921": return ArtDirection.Hex("666973");
                case "c_lambo": return ArtDirection.Hex("A7E51F");
                case "c_fa": return ArtDirection.Hex("D73E38");
                default: return ArtDirection.Sage;
            }
        }
        public static Transform Build(Transform parent,MotorModel model,Color color,bool includeRider=true)
        {
            if(MotorCatalog.ClassOf(model.Kind)==2)
            {
                var car=Prims.ParkedCar(parent,Vector3.zero,color).transform;car.localRotation=Quaternion.identity;
                if(model.Price>=500000){car.localScale=new Vector3(1.12f,.75f,1.05f);Prims.Box(car,"RearWing",new Vector3(0,1.2f,-1.35f),new Vector3(1.9f,.12f,.45f),Mats.Lit(ArtDirection.Ink));}
                if(model.Kind==VehicleKind.Van)Prims.Box(car,"CargoCabin",new Vector3(0,1.3f,-.6f),new Vector3(1.5f,1.2f,2.2f),Mats.Lit(color));
                return car;
            }
            var root=new GameObject(model.Name).transform;root.SetParent(parent,false);bool three=model.Kind==VehicleKind.Tricycle;
            var body = Mats.Lit(color);
            var trim = Mats.Lit(ArtDirection.Ink);
            Prims.Box(root,"Seat",new Vector3(0,.8f,-.35f),new Vector3(.55f,.16f,.7f),trim);
            if (three)
            {
                Prims.Box(root, "Frame", new Vector3(0, .5f, 0), new Vector3(.45f, .3f, 1.7f), body);
                Prims.Box(root, "Handlebar", new Vector3(0, 1.1f, .65f), new Vector3(.9f, .12f, .12f), trim);
            }
            else
            {
                Prims.Box(root, "Frame", new Vector3(0, .32f, 0), new Vector3(.45f, .16f, 1.7f), body);
                Prims.Box(root, "Footboard", new Vector3(0, ScooterDeckHeight - .0275f, .09f), new Vector3(.53f, .055f, .66f), trim);
                Prims.Box(root, "SeatBase", new Vector3(0, .56f, -.47f), new Vector3(.49f, .34f, .62f), body);
                var grip = ScooterGrip(0);
                Prims.Box(root, "SteeringStem", new Vector3(0, 1.04f, .44f), new Vector3(.13f, .36f, .14f), body)
                    .transform.localRotation = Quaternion.Euler(-34, 0, 0);
                Prims.Box(root, "Handlebar", grip, new Vector3(ScooterGripOffset * 2, .06f, .065f), trim);
                Prims.Box(root, "LeftGrip", ScooterGrip(-1), new Vector3(.16f, .074f, .076f), trim);
                Prims.Box(root, "RightGrip", ScooterGrip(1), new Vector3(.16f, .074f, .076f), trim);
            }
            for(int i=0;i<(three?3:2);i++)
            {
                var wheel=Prims.Shape(PrimitiveType.Cylinder,root,"Wheel",new Vector3(three&&i>0?(i==1?-.65f:.65f):0,.3f,i==0?.7f:-.7f),new Vector3(.58f,.12f,.58f),Mats.Lit(ArtDirection.Ink));wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            if(three)Prims.Box(root,"CargoBed",new Vector3(0,.7f,-.8f),new Vector3(1.4f,.4f,1),Mats.Lit(color));
            if(!three)
            {
                Prims.Box(root,"FrontShield",new Vector3(0,.72f,.55f),new Vector3(.52f,.66f,.22f),Mats.Lit(color));
                Prims.Shape(PrimitiveType.Sphere,root,"Headlight",new Vector3(0,1.04f,.71f),Vector3.one*.23f,Mats.Lit(ArtDirection.Cream));
                if(model.Id=="e_range")Prims.Box(root,"StorageBox",new Vector3(0,1.02f,-.78f),new Vector3(.65f,.4f,.5f),Mats.Lit(color));
                if(model.Id=="e_dash"||model.Id=="e_race")Prims.Box(root,"Windscreen",new Vector3(0,1.27f,.63f),new Vector3(.46f,.42f,.08f),Mats.Lit(ArtDirection.Ink));
                if(model.Id=="e_turn")Prims.Box(root,"CreamTrim",new Vector3(0,.75f,.68f),new Vector3(.22f,.5f,.02f),Mats.Lit(ArtDirection.Cream));
            }
            if(!includeRider)return root;
            Prims.Box(root,"RiderJacket",new Vector3(0,1.15f,-.15f),new Vector3(.5f,.7f,.35f),Mats.Lit(ArtDirection.Ink));
            Prims.Shape(PrimitiveType.Sphere,root,"Helmet",new Vector3(0,1.7f,-.1f),Vector3.one*.48f,Mats.Lit(ArtDirection.Cream));return root;
        }
    }
}
