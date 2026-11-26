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
            var asphalt=OldTownSurfaces.PanelMaterial("streetAsphalt");
            if(!starter)
            {
                asphalt=DistrictFinishes.Tile("streetAsphalt",Vector2.one,new Color(.79f,.77f,.76f));
                asphalt.SetFloat("_PatinaStrength",.32f);
            }
            Ribbon(scenery,(float)track.HalfRoad,.18f,asphalt,resources);
            BuildBarriers(scenery, resources);
            BuildStartingGantry(scenery);
            var stand = starter ? new Vector3(-35, 0, -19) : new Vector3(-10, 0, track.Tier==2?-4:0);
            var pit = starter ? new Vector3(-11.5f, 0, -14) : new Vector3(15, 0, 8);
            if (starter) for (int i = 0; i < 4; i++)
                Prims.Box(scenery, "Grandstand", stand + new Vector3(0, .6f + i * .6f, i * 2),
                    new Vector3(24, .7f, 2), Mats.Lit(i % 2 == 0 ? ArtDirection.Wood : ArtDirection.Cream));
            if (track.Tier == 3)
                BuildTimingCabin(scenery,pit);
            else
            {
                Prims.Box(scenery,"PitOffice",pit+Vector3.up*2.2f,new Vector3(12,4.3f,6),DistrictFinishes.Tile(track.Tier==1?"buffBrick":"ivoryPlaster",new Vector2(2,1)));
                if (starter)
                    Prims.Box(scenery,"PitRoof",pit+Vector3.up*4.5f,new Vector3(14,.5f,8),CircuitRoof());
                else
                {
                    var zinc=Mats.Lit(ArtDirection.Hex(track.Tier==1?"9CA8A8":"BAC3C0"),.21f);
                    Prims.Box(scenery,"值班室薄钢屋面",pit+Vector3.up*4.44f,new Vector3(12.66f,.20f,6.75f),zinc);
                }
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
                QuarterModules.Add(scenery,"CycleShelter",pit+new Vector3(-8f,.24f,2.1f),Vector3.one);
                for(int i=0;i<3;i++)QuarterModules.Add(scenery,"Cycle",pit+new Vector3(-9+i*.74f,.25f,2.1f),Vector3.one,12);
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
            if(Track.Tier==0) for(int row=0;row<4;row++)for(int seat=-5;seat<=5;seat++)
            {
                var at=stand+new Vector3(seat*2,1.1f+row*.6f,row*2);
                details.Box(at,new Vector3(1.5f,.12f,.65f),seatPaint);
                details.Box(at+new Vector3(0,.38f,.32f),new Vector3(1.5f,.6f,.10f),seatPaint);
            }
            foreach(int side in new[]{-1,1})
            {
                if (Track.Tier==0) details.Box(stand+new Vector3(side*12,3,3),new Vector3(.18f,6,.18f),metal);
                details.FlowerBed(pit+new Vector3(side*4.3f,.12f,Track.Tier==0?-3.7f:-4.85f),2.5f,.8f,side+9);
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
            else
            {
                // Seats look across the racing line. All seating, stairs and roof share this physical orientation.
                var pavilion=new GameObject("临赛道观众席").transform;
                pavilion.SetParent(scenery,false);
                pavilion.localPosition=stand+Vector3.forward*5.7f;
                pavilion.localRotation=Quaternion.Euler(0,180,0);
                pavilion.localScale=new Vector3(1.12f,1.04f,1.06f);
                var seats=new MiniatureDetailMesh();
                BuildDistrictGrandstand(pavilion,seats,Vector3.zero);
                BuildSeatingBays(seats,seatPaint);
                seats.Build(pavilion,"看台座椅、屋面与踏步");
            }
            if(Track.Tier<3)
            {
                if (Track.Tier==0)
                    for(int i=-5;i<=5;i++)
                        details.Box(pit+new Vector3(i,4.81f,0),new Vector3(.055f,.09f,7.9f),metal);
                else
                {
                    var seam=Mats.Lit(ArtDirection.Hex("949F9F"),.22f);
                    for(int joint=-2;joint<=2;joint++)
                        details.Box(pit+new Vector3(joint*2.1f,4.56f,0),new Vector3(.048f,.045f,6.67f),seam);
                }
                details.Window(pit+new Vector3(-3,2.5f,-3.15f),2.7f,1.7f,ArtDirection.Cream,true);
                details.Window(pit+new Vector3(3,2.5f,-3.15f),2.7f,1.7f,ArtDirection.Cream,true);
            }
            if (Track.Tier > 0) BuildPitApproach(scenery, pit);
            var border = new StreetPlanting(maintained:Track.Tier>0);
            // Only decorate clear infield: the driving ribbon and runoff stay empty.
            for(float x=-(float)Track.Width*.33f;x<(float)Track.Width*.34f;x+=13)
            for(float z=-(float)Track.Depth*.30f;z<(float)Track.Depth*.31f;z+=13)
            {
                var at=new Vector3(x,.10f,z);
                if(Mathf.Abs(x-stand.x)<16&&Mathf.Abs(z-stand.z-3)<10 || Mathf.Abs(x-pit.x)<10&&Mathf.Abs(z-pit.z)<8)continue;
                float clearance=DistanceFromRacingLine(at);
                if(clearance<(float)Track.HalfRoad+7)continue;
                int seed=Mathf.RoundToInt(x*7+z*13);
                if(Track.Tier>0)
                {
                    // Stagger the orchard; the offset stays inside the checked runoff clearance.
                    var shift=new Vector3(Mathf.Sin(x*.42f+z)*3.3f,0,Mathf.Cos(z*.37f+x)*2.6f);
                    if(clearance-Track.HalfRoad<12 || Mathf.Abs(x-stand.x)<20&&Mathf.Abs(z-stand.z-3)<15) shift*=.25f;
                    at+=shift;
                    border.Add(PlantSpecies.Grass,at+Vector3.right*1.2f,2.8f,1.16f,seed+1);
                    border.Add(Track.Tier==3?PlantSpecies.SilverGrass:PlantSpecies.Hydrangea,at+Vector3.forward*1.38f,2.15f,.97f,seed+7);
                }
                // Leave a clear sightline between the seats, pit office and the racing line.
                if(Track.Tier>0 && at.x>stand.x-16 && at.x<pit.x+7 && at.z>stand.z-2 && at.z<stand.z+39)
                    continue;
                if(Track.Tier>0 && Mathf.Abs(at.x-stand.x)<22 && at.z<stand.z-4 && at.z>stand.z-18)
                    continue;
                float height=Track.Tier==0 ? 5+Mathf.Abs(seed%3) : 6.4f+Mathf.Abs(seed%6)*.82f;
                PaintedWoodland.Plant(scenery,at,Mathf.RoundToInt(height),Track.Tier>0 && seed%4==0 ? 2 : seed%3==0?3:0,seed,Track.Tier>0,Track.Tier);
                border.Add(PlantSpecies.Spirea, at + Vector3.left * 2, 1.6f, .85f, seed);
                if(Track.Tier==0) details.FlowerBed(at+Vector3.forward*1.5f,2.8f,1.3f,seed,false);
            }
            if(Track.Tier>0) SpectatorPlanting(scenery,border,stand);
            border.Build(scenery,"看台草地与花境");
            details.Build(scenery,"Circuit seating, windows and gardens");
        }

        private float DistanceFromRacingLine(Vector3 at)
        {
            float nearest=float.MaxValue;
            for(int i=0;i<Track.Points.Length;i++)
            {
                var a=Point(i);var direction=Point(i+1)-a;
                var closest=a+direction*Mathf.Clamp01(Vector3.Dot(at-a,direction)/Mathf.Max(.001f,direction.sqrMagnitude));
                nearest=Mathf.Min(nearest,(closest-at).sqrMagnitude);
            }
            return Mathf.Sqrt(nearest);
        }

        private void SpectatorPlanting(Transform root,StreetPlanting bed,Vector3 stand)
        {
            // Plant the rear buffer and the two sides of the forecourt as continuous borders.
            // Keep access from the pit office and the strip next to the guardrail clear.
            for(int i=0;i<7;i++)
            {
                float x=(i-3)*7.1f;
                var tree=stand+new Vector3(x+Mathf.Sin(i*1.7f)*1.1f,.1f,-10.5f-1.5f*Mathf.Cos(i*1.22f));
                if(DistanceFromRacingLine(tree)<Track.HalfRoad+10)continue;
                int height=Track.Tier==2?8+i%3:9+i%4;
                PaintedWoodland.Plant(root,tree,height,0,48+i*31,true,Track.Tier);
                for(int j=0;j<5;j++)
                {
                    float angle=(i+j*2.4f);
                    var shrub=tree+new Vector3(Mathf.Cos(angle)*2.5f,0,Mathf.Sin(angle)*1.8f);
                    bed.Add(j==1?PlantSpecies.Spirea:PlantSpecies.Boxwood,shrub,1.65f+j*.1f,.78f+j*.052f,i*19+j);
                }
            }
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<27;i++)
            {
                float t=i/26f;
                float x=side*Mathf.Lerp(3.8f,16.5f,t);
                var at=stand+new Vector3(x,.12f,14.1f-.92f*Mathf.Cos(t*5.3f)+.48f*Mathf.Sin(i*2.40f));
                if(DistanceFromRacingLine(at)<Track.HalfRoad+5.8)continue;
                PlantSpecies flower=Track.Tier==3?PlantSpecies.SilverGrass:PlantSpecies.Hydrangea;
                bed.Add(i%6<2?flower:i%3==0?PlantSpecies.Spirea:PlantSpecies.Boxwood,at,
                    1.24f+.25f*Mathf.Sin(i*.65f),.69f+.22f*Mathf.Cos(i*.53f),side*61+i+21);
            }
        }

        private static void BuildSeatingBays(MiniatureDetailMesh detail, Material seatPaint)
        {
            var stone=DistrictFinishes.Tile("cutStone",new Vector2(4,1));
            var steel=Mats.Lit(ArtDirection.Hex("435456"));
            for(int row=0;row<5;row++)
            {
                float z=.05f+row*1.43f, top=.45f+row*.49f;
                detail.Box(new Vector3(0,top*.5f,z),new Vector3(23.9f,top,1.43f),stone);
                for(int bay=-1;bay<=1;bay++)
                {
                    float x=bay*7.95f;
                    detail.Box(new Vector3(x,top+.28f,z+.13f),new Vector3(6.45f,.10f,.14f),steel);
                    for(int chair=0;chair<8;chair++)
                    {
                        var at=new Vector3(x+(chair-3.5f)*.82f,top+.44f,z);
                        detail.Box(at,new Vector3(.65f,.12f,.56f),seatPaint);
                        detail.Box(at+new Vector3(0,.24f,.27f),new Vector3(.61f,.46f,.105f),seatPaint,Quaternion.Euler(9,0,0));
                    }
                }
                if(row>0) foreach(int side in new[]{-1,1})
                    detail.Box(new Vector3(side*3.97f,top-.34f,z-.94f),new Vector3(.9f,.20f,.48f),stone);
            }
        }

        private void BuildDistrictGrandstand(Transform root, MiniatureDetailMesh detail, Vector3 stand)
        {
            var white=DistrictFinishes.Tile("pearlCeramic",new Vector2(5,2));
            var alloy=Mats.Lit(ArtDirection.Hex("9BADB2"),.42f);
            var glass=DistrictFinishes.Tile("civicGlazing",new Vector2(.31f,.24f));
            var roof=stand+new Vector3(0,6.6f,2.8f);
            var paving=DistrictFinishes.Tile("limestoneWalk",new Vector2(.44f,.44f));
            var bed=new StreetPlanting(maintained:true);
            detail.Box(stand+new Vector3(0,.16f,1.35f),new Vector3(29.6f,.19f,15.9f),paving);
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
                var green=Mats.Lit(ArtDirection.Hex("344E40"),.18f);
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
                detail.Box(stand+new Vector3(0,2.41f,6.8f),new Vector3(24,.91f,.18f),wood);
                for(int post=-2;post<=2;post++)
                {
                    float x=post*5.8f;
                    detail.Box(stand+new Vector3(x,3.52f,6.8f),new Vector3(.16f,5.94f,.18f),wood);
                    detail.Box(stand+new Vector3(x,5.98f,2.7f),new Vector3(.16f,.24f,8.8f),wood);
                }
                return;
            }
            bool garden=Track.Tier==3;
            // Sections rise towards the end supports. The soffit traces the same curve as the skin.
            var steel=Mats.Lit(ArtDirection.Hex(garden?"7B929D":"B1B3AA"));
            var cell=DistrictFinishes.Tile("solarCells",Vector2.one*.40f);
            const int sections=24;
            float half=garden?14.2f:13.37f;
            Vector3 Skin(float u,float v)
            {
                float end=u*u, depth=(garden?10.3f:9.9f)-end*.95f;
                float z=v*depth*.5f;
                float crown=(1-v*v)*(garden?.24f:.17f);
                float y=end*(garden?.91f:.55f)+z*(garden?.076f:.103f)+crown;
                return roof+new Vector3(u*half,y,z+end*.27f);
            }
            var vertices=new List<Vector3>();
            var uv=new List<Vector2>();
            var topFaces=new List<int>();
            var rimFaces=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> faces)
            {
                int index=vertices.Count;
                foreach(var v in new[]{a,b,c,d})
                {
                    vertices.Add(v);uv.Add(new Vector2(v.x*.16f,v.z*.16f));
                }
                faces.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
            }
            var lip=Vector3.down*(garden?.32f:.17f);
            const int lanes=6;
            for(int i=0;i<sections;i++)
            {
                float l=2f*i/sections-1,r=2f*(i+1)/sections-1;
                for(int j=0;j<lanes;j++)
                {
                    float a=2f*j/lanes-1,b=2f*(j+1)/lanes-1;
                    var p0=Skin(l,a);var p1=Skin(l,b);var p2=Skin(r,b);var p3=Skin(r,a);
                    Quad(p0,p1,p2,p3,topFaces);Quad(p3+lip,p2+lip,p1+lip,p0+lip,rimFaces);
                    if(j==0) Quad(p0,p3,p3+lip,p0+lip,rimFaces);
                    if(j==lanes-1) Quad(p2,p1,p1+lip,p2+lip,rimFaces);
                    if(i==0) Quad(p1,p0,p0+lip,p1+lip,rimFaces);
                    if(i==sections-1) Quad(p3,p2,p2+lip,p3+lip,rimFaces);
                }
            }
            var skin=new Mesh{name="弧形看台顶棚",subMeshCount=2};
            skin.SetVertices(vertices);skin.SetUVs(0,uv);
            skin.SetTriangles(topFaces,0);skin.SetTriangles(rimFaces,1);skin.RecalculateNormals();skin.RecalculateBounds();
            var membrane=DistrictFinishes.Tile("pearlCeramic",new Vector2(1.7f,1.4f),
                garden?ArtDirection.Hex("CBD7DC"):ArtDirection.Hex("D3D2CA"));
            var owner=root.GetComponentInParent<CityLandscapeResources>();
            owner.Owned.Add(skin);
            var cover=new GameObject("顶棚与金属封边",typeof(MeshFilter),typeof(MeshRenderer));
            cover.transform.SetParent(root,false);cover.GetComponent<MeshFilter>().sharedMesh=skin;
            cover.GetComponent<MeshRenderer>().sharedMaterials=new[]{membrane,steel};
            for(int rib=-4;rib<=4;rib++)
            {
                float u=rib/4.5f;
                GardenStructures.Strut(detail,Skin(u,-1)+lip,Skin(u,1)+lip,garden?.11f:.062f,.093f,steel);
                if(!garden || Mathf.Abs(rib)>3)continue;
                var pos=Skin(u,.04f)+Vector3.up*.16f;
                var slope=Quaternion.LookRotation((Skin(u,1)-Skin(u,-1)).normalized,
                    Vector3.Cross(Skin(u,1)-Skin(u,-1),Skin(u+.035f,0)-Skin(u-.035f,0)).normalized);
                detail.Box(pos,new Vector3(half*.206f,.10f,7.15f),cell,slope);
                for(int line=-1;line<=1;line++)
                    detail.Box(pos+slope*new Vector3(0,.057f,line*1.78f),new Vector3(half*.205f,.018f,.032f),alloy,slope);
            }
            if(garden)
            {
                // Terraced glazed windscreen below the rear edge, with clear views through the open ends.
                for(int bay=-4;bay<=4;bay++)
                {
                    var at=stand+new Vector3(bay*2.59f,4.36f,6.81f);
                    detail.Box(at,new Vector3(2.50f,2.83f,.12f),glass);
                    detail.Box(at+Vector3.right*1.29f,new Vector3(.095f,3.28f,.16f),alloy);
                }
                foreach(int side in new[]{-1,1})
                {
                    var foot=stand+new Vector3(side*11.24f,.45f,-.80f);
                    var knee=foot+new Vector3(side*.24f,3.24f,.38f);
                    var neck=knee+new Vector3(side*1.18f,2.13f,.10f);
                    var tip=stand+new Vector3(side*13.96f,7.03f,-.86f);
                    GardenStructures.Strut(detail,foot,knee,.48f,1.18f,white);
                    GardenStructures.Strut(detail,knee,neck,.63f,1.26f,white);
                    GardenStructures.Strut(detail,neck,tip,.75f,1.30f,white);
                }
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

        private void BuildPitApproach(Transform scenery, Vector3 pit)
        {
            var paving = DistrictFinishes.Tile("limestoneWalk", Vector2.one * .44f);
            // Join the apron to the actual west face of the clubhouse or the narrower timing cabin.
            const float west = 6.576f;
            float halfWidth = Track.Tier == 3 ? 5.54f : 6f;
            float east = pit.x-halfWidth;
            float front = pit.z-4.2f;
            float end = pit.z+(Track.Tier<3?4.2f:.3f);
            Prims.Box(scenery, "看台至辅房步道", new Vector3((west+east)*.5f,.16f,(front+end)*.5f),
                new Vector3(east-west,.16f,end-front), paving);
            Prims.Box(scenery, "辅房门前铺地", pit + new Vector3(0,.16f,-3.6f),
                new Vector3(halfWidth*2,.16f,1.2f), paving);
            var side = new GameObject("临看台的辅房入口").transform;
            side.SetParent(scenery, false);
            side.localPosition = pit + Vector3.left * (Track.Tier == 3 ? 5.57f : 6.03f);
            side.localRotation = Quaternion.Euler(0,90,0);
            var trim = Mats.Lit(ArtDirection.Hex(Track.Tier == 1 ? "59685B" : "69787B"),.26f);
            var glass = DistrictFinishes.Tile("civicGlazing", Vector2.one * .28f);
            var panels = new MiniatureDetailMesh();
            panels.Box(new Vector3(.64f,1.40f,-.045f),new Vector3(1.49f,2.55f,.12f),trim);
            panels.Box(new Vector3(.64f,1.86f,-.122f),new Vector3(1.20f,1.13f,.035f),glass);
            panels.Box(new Vector3(.12f,1.25f,-.18f),new Vector3(.045f,.36f,.065f),Mats.Lit(ArtDirection.Cream));
            panels.Box(new Vector3(-1.42f,2.27f,-.056f),new Vector3(1.48f,1.13f,.14f),trim);
            panels.Box(new Vector3(-1.42f,2.27f,-.14f),new Vector3(1.31f,.95f,.038f),glass);
            panels.Box(new Vector3(-1.42f,2.27f,-.18f),new Vector3(.055f,.97f,.046f),trim);
            panels.Box(new Vector3(.32f,2.88f,-.37f),new Vector3(2.63f,.10f,.75f),trim);
            panels.Build(side,"值班门窗、门把与雨眉");
            if (Track.Tier == 3) return;
            Prims.Box(scenery, "器材门卸货踏步", pit+new Vector3(0,.16f,3.6f),
                new Vector3(halfWidth*2,.16f,1.2f),paving);
            var rear = new GameObject("场务辅房器材立面").transform;
            rear.SetParent(scenery,false);
            rear.localPosition = pit + Vector3.forward * 3.03f;
            rear.localRotation = Quaternion.Euler(0,180,0);
            var sash = new MiniatureDetailMesh();
            var shutter=Mats.Lit(ArtDirection.Hex(Track.Tier==1?"A5A9A1":"B0BAB9"),.19f);
            if (Track.Tier==1)
            {
                foreach(int bay in new[]{-1,1})
                {
                    float x=bay*2.46f;
                    sash.Box(new Vector3(x,1.62f,-.054f),new Vector3(3.68f,2.76f,.14f),trim);
                    sash.Box(new Vector3(x,1.59f,-.14f),new Vector3(3.49f,2.59f,.045f),shutter);
                    for(int rib=0;rib<8;rib++)
                        sash.Box(new Vector3(x,.59f+rib*.29f,-.178f),new Vector3(3.44f,.033f,.046f),shutter);
                }
            }
            else
            {
                sash.Box(new Vector3(-2.47f,2.66f,-.04f),new Vector3(3.0f,.93f,.11f),trim);
                sash.Box(new Vector3(-2.47f,2.66f,-.12f),new Vector3(2.81f,.78f,.052f),glass);
                sash.Box(new Vector3(-2.47f,2.66f,-.16f),new Vector3(.07f,.81f,.057f),trim);
                sash.Box(new Vector3(2.84f,1.61f,-.04f),new Vector3(1.65f,2.76f,.12f),trim);
                sash.Box(new Vector3(2.84f,1.60f,-.13f),new Vector3(1.44f,2.57f,.052f),shutter);
                sash.Box(new Vector3(3.33f,1.47f,-.20f),new Vector3(.2f,.056f,.11f),trim);
            }
            sash.Build(rear,"器材门与采光窗");
        }

        private static void BuildTimingCabin(Transform root, Vector3 pit)
        {
            var white=DistrictFinishes.Tile("pearlCeramic",new Vector2(3,2));
            var glass=DistrictFinishes.VillaPane();
            var alloy=Mats.Lit(ArtDirection.Hex("A7B6BD"),.37f);
            var ink=Mats.Lit(ArtDirection.Hex("324D5A"),.33f);
            GardenStructures.Plate(root,"计时舱基座",pit+Vector3.up*.35f,new Vector3(11.8f,.54f,6.6f),white);
            GardenStructures.Plate(root,"舱内防滑地台",pit+Vector3.up*.64f,new Vector3(11.08f,.04f,5.84f),
                Mats.Lit(ArtDirection.Hex("A5B1AA"),.13f));
            var envelope=GardenStructures.Plate(root,"计时舱半透明环窗",pit+Vector3.up*2.25f,
                new Vector3(11.1f,3.46f,5.86f),glass);
            envelope.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var detail=new MiniatureDetailMesh();
            const float roofHalf = 3.08f;
            Vector3 RoofEdge(float z) => pit+new Vector3(0,3.96f+.73f*(1-z*z/(roofHalf*roofHalf)),z);
            for(int section=0;section<6;section++)
            {
                var a=RoofEdge(Mathf.Lerp(-roofHalf,roofHalf,section/6f));
                var b=RoofEdge(Mathf.Lerp(-roofHalf,roofHalf,(section+1)/6f));
                var across=(b-a).normalized;
                var pane=Prims.Shape(PrimitiveType.Quad,root,"计时舱弧形天窗",(a+b)*.5f,
                    new Vector3(11.32f,Vector3.Distance(a,b),1),glass);
                pane.transform.localRotation=Quaternion.LookRotation(Vector3.Cross(Vector3.right,across),across);
                pane.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                for (int rib=-3;rib<=3;rib++)
                {
                    var x=Vector3.right*(rib*1.90f);
                    GardenStructures.Strut(detail,a+x,b+x,.13f,.13f,alloy);
                }
            }
            foreach (int side in new[] {-1,1})
            {
                detail.Box(pit+new Vector3(0,3.96f,side*2.99f),new Vector3(11.36f,.15f,.19f),white);
                detail.Box(pit+new Vector3(0,.72f,side*2.89f),new Vector3(10.45f,.12f,.15f),alloy);
                for(int bay=-2;bay<=2;bay++)
                    detail.Box(pit+new Vector3(bay*1.9f,2.26f,side*2.91f),new Vector3(.095f,3.42f,.14f),alloy);
                foreach(int corner in new[]{-1,1})
                {
                    float z=corner*1.5f;
                    var foot=pit+new Vector3(side*5.49f,.62f,z);
                    GardenStructures.Strut(detail,foot,RoofEdge(z)+Vector3.right*(side*5.70f),.12f,.13f,alloy);
                }
            }
            detail.Box(pit+new Vector3(0,1.46f,.38f),new Vector3(6.8f,.16f,1.27f),white);
            for(int desk=-1;desk<=1;desk++)
            {
                float x=desk*2.28f;
                detail.Box(pit+new Vector3(x,.99f,.38f),new Vector3(.14f,.83f,.72f),alloy);
                detail.Box(pit+new Vector3(x,1.91f,.50f),new Vector3(1.03f,.56f,.085f),ink);
                detail.Box(pit+new Vector3(x,1.14f,-.74f),new Vector3(.62f,.16f,.65f),ink);
                detail.Box(pit+new Vector3(x,.86f,-.74f),new Vector3(.17f,.49f,.17f),alloy);
            }
            var clock=pit+new Vector3(-6.8f,0,2.62f);
            detail.Box(clock+Vector3.up*1.44f,new Vector3(.35f,2.88f,.38f),white);
            detail.Box(clock+Vector3.up*4.08f,new Vector3(1.45f,2.5f,.42f),ink);
            var readout=Mats.Lit(ArtDirection.Hex("A3D2CA"));
            foreach (int face in new[] {-1,1})
                for(int i=0;i<4;i++)
                    detail.Box(clock+new Vector3(0,4.82f-i*.43f,face*.225f),
                        new Vector3(.94f-i*.10f,.105f,.045f),readout);
            detail.Build(root,"计时舱钢肋、操作台与双面计时牌");
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
            float circumference = 0;
            for (int i = 0; i < count; i++)
            {
                var incoming = (Point(i) - Point((i + count - 1) % count)).normalized;
                var outgoing = (Point(i + 1) - Point(i)).normalized;
                circumference += Vector3.Distance(Point(i), Point(i+1));
                var nextNormal = Vector3.Cross(Vector3.up, outgoing);
                var bisector = (Vector3.Cross(Vector3.up, incoming) + nextNormal).normalized;
                // Both neighbouring segments use the same miter joint, with no overlapping end faces.
                joins[i] = bisector / Mathf.Max(.25f, Vector3.Dot(bisector, nextNormal));
            }
            foreach (int side in new[] { -1, 1 })
            {
                bool garden = Track.Tier == 3;
                var materials = garden
                    ? new[] { Mats.Lit(ArtDirection.Hex("A7B3B4"),.39f), Mats.Lit(ArtDirection.Hex("CFD5D0"),.32f) }
                    : Track.Tier == 0 ? new[] { Mats.Lit(ArtDirection.Clay), Mats.Lit(ArtDirection.Cream) }
                    : new[] { Mats.Lit(ArtDirection.Hex("B75E4D"),.16f), Mats.Lit(ArtDirection.Hex("E1DBCA"),.15f),
                              Mats.Lit(ArtDirection.Hex("A69F8F"),.15f) };
                // Uniform lengths close around the loop even where the centreline has uneven chords.
                int pairs = Mathf.Max(1, Mathf.RoundToInt(circumference / 12f));
                float panelLength = circumference / (2*pairs);
                int postCount = Mathf.Max(1, Mathf.RoundToInt(circumference/3.4f));
                float postSpacing = circumference / postCount;
                float traveled = 0;
                int nextPost = 0;
                var fittings = new MiniatureDetailMesh();
                var vertices = new List<Vector3>();
                var triangles = new List<int>[materials.Length];
                for (int m=0;m<triangles.Length;m++) triangles[m]=new List<int>();
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
                    float length = Vector3.Distance(Point(i),Point(j));
                    float offset = side * ((float)Track.HalfRoad + 3.8f);
                    var normal = Vector3.Cross(Vector3.up, (Point(j) - Point(i)).normalized);
                    Vector3 At(float t, float lateral, float y) => Vector3.Lerp(Point(i)+joins[i]*(offset+lateral),
                        Point(j)+joins[j]*(offset+lateral),t)+Vector3.up*y;
                    void Beam(float from, float to, float lateral, float width, float y, float height, int mat)
                    {
                        var a=At(from,lateral-width*.5f,y);var b=At(to,lateral-width*.5f,y);
                        var c=At(to,lateral+width*.5f,y);var d=At(from,lateral+width*.5f,y);
                        var rise=Vector3.up*height;
                        Quad(a,b,b+rise,a+rise,-normal,mat); Quad(d,c,c+rise,d+rise,normal,mat);
                        Quad(a+rise,b+rise,c+rise,d+rise,Vector3.up,mat); Quad(a,b,c,d,Vector3.down,mat);
                    }
                    if (Track.Tier == 0) Beam(0,1,0,.65f,.05f,1.1f,i%2);
                    else if (!garden)
                    {
                        Beam(0,1,0,.65f,.05f,.16f,2);
                        int first=Mathf.FloorToInt(traveled/panelLength);
                        int last=Mathf.CeilToInt((traveled+length)/panelLength)-1;
                        for (int band=first;band<=last;band++)
                        {
                            float from=Mathf.Clamp01((band*panelLength-traveled)/length);
                            float to=Mathf.Clamp01(((band+1)*panelLength-traveled)/length);
                            if (to>from) Beam(from,to,0,.56f,.21f,.51f,band%2);
                        }
                    }
                    else
                    {
                        for (int rail=0;rail<3;rail++)
                            Beam(0,1,-side*.11f,.16f,.31f+rail*.37f,.145f,rail==2?1:0);
                        var facing = Quaternion.LookRotation(-normal*side);
                        while(nextPost<postCount && nextPost*postSpacing<traveled+length)
                        {
                            float at=(nextPost*postSpacing-traveled)/length;
                            var post=At(at,side*.08f,.615f);
                            fittings.Box(post,new Vector3(.15f,1.14f,.16f),materials[0],facing);
                            if (nextPost%19==11) AddBarrierChevrons(fittings,At(at,-side*.25f,.68f),facing,side);
                            nextPost++;
                        }
                    }
                    traveled+=length;
                }
                var mesh = new Mesh { name = "ContinuousRaceBarrier" };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = materials.Length;
                for (int m=0;m<triangles.Length;m++) mesh.SetTriangles(triangles[m],m);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                resources.Owned.Add(mesh);
                var go = new GameObject("Barrier", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = materials;
                if (garden) fittings.Build(parent,"赛道钢护栏立柱及导向标");
            }
        }

        private static void AddBarrierChevrons(MiniatureDetailMesh fittings, Vector3 center, Quaternion facing, int raceDirection)
        {
            var enamel=Mats.Lit(ArtDirection.Hex("428A87"),.30f);
            var stripe=Mats.Lit(ArtDirection.Hex("ECE9D9"),.20f);
            fittings.Box(center,new Vector3(1.38f,.49f,.055f),enamel,facing);
            foreach (float x in new[] {-.4f,.14f})
            {
                Vector3 P(float u,float h) => center+facing*new Vector3((x+u)*raceDirection,h,.036f);
                GardenStructures.Strut(fittings,P(0,.17f),P(.20f,0),.064f,.064f,stripe);
                GardenStructures.Strut(fittings,P(.20f,0),P(0,-.17f),.064f,.064f,stripe);
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
