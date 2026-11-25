using PCShopSell.Core.World;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private const string StarterHome = "home";
        private const string StarterStation = "metro_a";
        private const string StarterShop = "life_unit_s_oldhomes_2";
        private static readonly Color StarterStone = ArtDirection.Hex("CEB99D");
        private static readonly Color StarterTimber = ArtDirection.Hex("855637");
        private static readonly Color StarterBlue = ArtDirection.Hex("355474");

        private bool HasStarterArt => _city.IsLakeCity && _city.HasPlace(StarterHome)
            && _city.HasPlace(StarterShop) && _city.HasPlace(StarterStation);

        private bool TryBuildStarterArt(Transform root, Place place, float width, float depth)
        {
            if (!HasStarterArt) return false;
            switch (place.Id)
            {
                case StarterHome:
                    BuildQuarterShell(root, place, width, depth, StarterResidenceHeight, false, StableCityHash(place.Id));
                    return true;
                case StarterShop:
                    CityArchitecture.StreetApron(root,width,true);
                    BuildStarterShop(root, place, width, depth);
                    return true;
                case StarterStation:
                    CityArchitecture.StreetApron(root,width,true);
                    StarterMetro(root, place, width, depth);
                    return true;
                default:
                    if (!OutskirtsStreet(place.StreetId) || !(GameSession.IsCommercialPremises(place) || place.Model == "shop" && !CityArchitecture.HasExterior(place))) return false;
                    CityArchitecture.StreetApron(root, width,true);
                    BuildStarterShop(root, place, width, depth);
                    return true;
            }
        }

        private void BuildStarterTrees()
        {
            if (!HasStarterArt || !Places.TryGetValue(StarterHome, out var home)
                || !Places.TryGetValue(StarterShop, out var shop)
                || !Places.TryGetValue(StarterStation, out var station)) return;
            var back = home.Root.transform.forward;
            var grove = new GameObject("Starter street planting").transform;
            grove.SetParent(_root,false);
            var points = new[] {
                Vector3.Lerp(home.Entrance, station.Entrance, .55f) + back*3.1f,
                Vector3.Lerp(home.Entrance, station.Entrance, .38f) + back*11.7f,
                Vector3.Lerp(station.Entrance, shop.Entrance, .52f) + back*9.2f
            };
            for (int i = 0; i < points.Length; i++)
            {
                var tree = new GameObject("Courtyard tree").transform;
                tree.SetParent(grove,false);
                tree.position = points[i];
                tree.localScale = Vector3.one * (1.20f+i*.13f);
                if (OldTownTree != null) Object.Instantiate(OldTownTree, tree, false);
                else CityVegetation.Build(tree,Vector3.zero,5+i*2,true);
                for (int j = 0; j < 5; j++)
                {
                    var offset = new Vector3(Mathf.Cos(j*1.52f)*(.9f+j*.16f), .11f,
                        Mathf.Sin(j*1.52f)*(.9f+j*.16f));
                    Prims.Shape(PrimitiveType.Sphere,tree,"Flowering groundcover",offset,new Vector3(.33f,.22f,.29f),
                        Mats.Lit(ArtDirection.Hex(j%2==0 ? "B4B457" : "78914B")));
                }
            }
            BatchStatic(grove.gameObject);
        }

        private static GameObject StarterPart(Transform root, string name, Vector3 position, Vector3 size,
            Color color, string surface = null, Vector2 tiling = default, bool solid = false)
        {
            var material = surface == null ? Mats.Lit(color, .18f)
                : IndustrialMaterials.Surface(surface, color, tiling == default ? Vector2.one : tiling);
            return Prims.Box(root, name, position, size, material, solid);
        }

        private static float OldTownEaves(Place place) => place.Size switch
        {
            ShopSize.Medium => 5.9f, ShopSize.Large => 8.5f, _ => 3.60f
        };

        private void BuildStarterShop(Transform root, Place place, float width, float depth)
        {
            BuildQuarterShell(root, place, width, depth, OldTownEaves(place), true, StableCityHash(place.Id));
        }

        private static void StarterRoof(Transform root, float width, float depth, float height, float rise,
            Color roof, Color wall, Color trim, bool oldResidence = false, int repairSeed = -1)
        {
            var owner = root.gameObject.AddComponent<CityLandscapeResources>();
            foreach (int side in new[] {-1,1})
            {
                float run = width/2+.29f;
                float slope = Mathf.Atan2(rise,run)*Mathf.Rad2Deg;
                var panel = StarterPart(root,"Pitched tile roof",
                    new Vector3(side*run/2,height+rise/2,depth/2),
                    new Vector3(Mathf.Sqrt(run*run+rise*rise),.18f,depth+.72f),
                    roof,"claytiles",new Vector2(run/1.9f,depth/2.4f));
                panel.transform.localRotation = Quaternion.Euler(0,0,-side*slope);
                var bar = StarterPart(root,"Rake trim",new Vector3(side*run/2,height+rise/2-.055f,-.38f),
                    new Vector3(Mathf.Sqrt(run*run+rise*rise),.19f,.14f),trim);
                bar.transform.localRotation = panel.transform.localRotation;
                StarterPart(root,"Roof gutter",new Vector3(side*run,height-.03f,depth/2),
                    new Vector3(.22f,.15f,depth+.63f),trim);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side < 0 ? -.018f : depth+.018f;
                var gable = new Mesh { name = "Textured pilot gable" };
                gable.vertices = new[] { new Vector3(-width/2,height,z),new Vector3(0,height+rise,z),new Vector3(width/2,height,z) };
                gable.uv = new[] { Vector2.zero,new Vector2(.5f,1),Vector2.right };
                gable.triangles = side < 0 ? new[] { 0,1,2 } : new[] { 2,1,0 };
                gable.RecalculateNormals();
                owner.Owned.Add(gable);
                var end = new GameObject("Gable masonry",typeof(MeshFilter),typeof(MeshRenderer));
                end.transform.SetParent(root,false);
                end.GetComponent<MeshFilter>().sharedMesh = gable;
                end.GetComponent<MeshRenderer>().sharedMaterial = oldResidence
                    ? IndustrialMaterials.Surface("old_plaster",wall,Vector2.one)
                    : IndustrialMaterials.Surface("brick",wall,new Vector2(width/4,rise/2));
            }
            CityArchitecture.SculptedRoofTiles(root,new Vector3(0,height+.04f,depth/2),width+.58f,depth+.72f,rise,roof,false,repairSeed);
            StarterPart(root,"Ridge cap",new Vector3(0,height+rise+.065f,depth/2),new Vector3(.24f,.18f,depth+.74f),roof);
            if (oldResidence) return;
            StarterPart(root,"Chimney",new Vector3(-width*.23f,height+rise*.78f,depth*.68f),
                new Vector3(.79f,1.28f,.72f),wall,"brick");
            StarterPart(root,"Chimney cap",new Vector3(-width*.23f,height+rise*.78f+.64f,depth*.68f),
                new Vector3(.92f,.15f,.85f),trim);
        }

        private static void StarterDoor(Transform root, float x, Color color)
        {
            StarterPart(root,"Inset door",new Vector3(x,1.15f,-.06f),new Vector3(1.37f,2.25f,.13f),color,"wood");
            StarterPart(root,"Door lintel",new Vector3(x,2.33f,-.13f),new Vector3(1.65f,.15f,.21f),StarterStone);
            foreach (int side in new[] {-1,1})
                StarterPart(root,"Door jamb",new Vector3(x+side*.74f,1.17f,-.13f),new Vector3(.13f,2.31f,.20f),StarterStone);
            StarterPart(root,"Door handle",new Vector3(x+.43f,1.11f,-.145f),new Vector3(.06f,.24f,.08f),ArtDirection.Brass);
        }

        private static void StarterWindow(Transform root, Vector3 center, float width, float height, Color trim, bool lit)
        {
            StarterPart(root,"Stone window reveal",center+Vector3.back*.055f,new Vector3(width+.27f,height+.27f,.15f),StarterStone);
            StarterPart(root,"Window frame",center+Vector3.back*.145f,new Vector3(width,height,.18f),trim);
            var glass = lit ? IndustrialMaterials.LamplitWindow()
                : Mats.Lit(ArtDirection.Hex("30475B"),.58f,.1f);
            Prims.Box(root,"Glazing",center+Vector3.back*.239f,new Vector3(width-.14f,height-.16f,.035f),glass);
            StarterPart(root,"Window mullion",center+Vector3.back*.273f,new Vector3(.075f,height,.075f),trim);
            StarterPart(root,"Window transom",center+Vector3.back*.273f,new Vector3(width,.067f,.075f),trim);
            StarterPart(root,"Projecting window sill",center+new Vector3(0,-height/2-.09f,-.19f),
                new Vector3(width+.36f,.15f,.37f),StarterStone);
        }

        private static void StarterPlanter(Transform root, Vector3 at, float width, int variant)
        {
            StarterPart(root,"Wood flower box",at+Vector3.up*.26f,new Vector3(width,.49f,.62f),StarterTimber,"wood");
            StarterPart(root,"Flower box rim",at+Vector3.up*.50f,new Vector3(width+.08f,.095f,.69f),StarterTimber);
            for (int i = 0; i < 5; i++)
            {
                var point = at+new Vector3((i-2)*width*.184f,.62f+((i+variant)%3)*.055f,0);
                Prims.Shape(PrimitiveType.Sphere,root,"Box foliage",point,new Vector3(.34f,.36f,.45f),
                    Mats.Lit(ArtDirection.Hex(i%2==0?"799844":"5D8249")));
                if ((i+variant)%2==0)
                    Prims.Shape(PrimitiveType.Sphere,root,"Small flowers",point+new Vector3(.04f,.16f,-.10f),new Vector3(.15f,.12f,.14f),
                        Mats.Lit(ArtDirection.Hex(i%3==0?"E6AA83":"EBC45D")));
            }
        }

        private void StarterWallLamp(Transform root, Vector3 at)
        {
            StarterPart(root,"Lamp shade",at+Vector3.up*.12f,new Vector3(.31f,.09f,.28f),StarterBlue);
            Prims.Box(root,"Warm lamp glass",at,new Vector3(.23f,.19f,.21f),
                Mats.Glow(ArtDirection.Hex("FFD097"),new Color(2.5f,1.1f,.38f),false));
            var lamp = new GameObject("Entrance light").AddComponent<Light>();
            lamp.transform.SetParent(root,false);
            lamp.transform.localPosition = at + Vector3.back*.28f;
            lamp.type = LightType.Point;
            lamp.color = new Color(1,.68f,.35f);
            lamp.range = 4.1f;
            lamp.intensity = 0;
            lamp.shadows = LightShadows.None;
            NightLights.Add(lamp);
        }

        private void StarterMetro(Transform root, Place place, float width, float depth)
        {
            var baseGo = StarterPart(root,"Station platform",new Vector3(0,.12f,depth/2),
                new Vector3(width,.24f,depth),StarterStone,"paving",new Vector2(2,2),true);
            baseGo.AddComponent<PlaceMarker>().PlaceId = place.Id;
            for (int i = 0; i < 6; i++)
                StarterPart(root,"Station stair",new Vector3(0,.27f+i*.12f,.54f+i*.57f),
                    new Vector3(width-1.7f,.19f,.59f),StarterStone);
            foreach (int side in new[] {-1,1})
            {
                StarterPart(root,"Station cheek wall",new Vector3(side*(width/2-.39f),.86f,depth/2),
                    new Vector3(.59f,1.72f,depth),ArtDirection.Hex("AAA8A3"),"brick");
                StarterPart(root,"Canopy column",new Vector3(side*(width/2-.43f),1.48f,.32f),
                    new Vector3(.31f,2.96f,.32f),StarterBlue);
                StarterWallLamp(root,new Vector3(side*width*.27f,2.36f,.29f));
            }
            StarterPart(root,"Station back",new Vector3(0,1.44f,depth-.15f),new Vector3(width,2.88f,.30f),StarterStone);
            var canopy = StarterPart(root,"Blue standing-seam canopy",new Vector3(0,3.23f,depth/2-.14f),
                new Vector3(width+.55f,.17f,depth+.5f),StarterBlue,"metal",new Vector2(width/4,depth/5));
            canopy.transform.localRotation = Quaternion.Euler(5,0,0);
            int district=ArtDistrict(place.StreetId);
            if(district==0)
            {
                // Faded metal panels sit on the same roof slope and keep the station mouth open.
                var ribs=new MiniatureDetailMesh();
                var metal=DistrictFinishes.Tile("patina",Vector2.one*.42f,ArtDirection.Hex("AAA99D"));
                var span=new Vector3(0,0,depth+.5f);
                for(float x=-width*.49f;x<width*.51f;x+=.91f)
                    ribs.Box(canopy.transform.localRotation*new Vector3(x,.11f,0)+canopy.transform.localPosition,
                        new Vector3(.045f,.062f,span.z),metal,canopy.transform.localRotation);
                ribs.Build(root,"站棚金属咬边");
                QuarterModules.Add(root,"Bench",new Vector3(width*.27f,.65f,depth*.66f),Vector3.one*.77f,270);
                QuarterModules.Add(root,"Cycle",new Vector3(-width*.5f-.22f,.07f,depth*.51f),Vector3.one*.88f);
            }
            if(district>0)
            {
                canopy.GetComponent<MeshRenderer>().sharedMaterial=DistrictFinishes.Tile(district==1?"slate":"patina",new Vector2(width/4,depth/4));
                var steel=DistrictFinishes.Tile(district==3?"walnut":"patina",Vector2.one*.34f);
                var joinery=new MiniatureDetailMesh();
                foreach(int side in new[]{-1,1})
                {
                    for(int post=0;post<3;post++)
                    {
                        float z=.8f+post*(depth-1.6f)/2;
                        joinery.Box(new Vector3(side*(width*.5f-.32f),2.16f,z),new Vector3(.13f,1.78f,.16f),steel);
                    }
                    joinery.Box(new Vector3(side*(width*.5f-.32f),2.76f,depth*.5f),new Vector3(.11f,.12f,depth-.46f),steel);
                    QuarterModules.Add(root,"PlantPot",new Vector3(side*width*.43f,-.07f,-.35f),Vector3.one);
                }
                joinery.Build(root,"站棚侧廊与栏杆");
            }
            StarterPart(root,"Station fascia",new Vector3(0,3.17f,-.40f),new Vector3(width+.52f,.72f,.22f),StarterBlue);
            StarterPart(root,"Station nameplate",new Vector3(0,3.96f,-.39f),new Vector3(width*.49f,.88f,.25f),StarterBlue);
            Prims.SignText(root,place.Name,new Vector3(0,3.96f,-.535f),width*.45f,.70f,ArtDirection.Cream);
            StarterPart(root,"Metro pylon",new Vector3(-width*.57f,1.07f,-.15f),new Vector3(.31f,2.14f,.32f),StarterBlue);
            StarterPart(root,"Metro roundel backing",new Vector3(-width*.57f,2.13f,-.15f),new Vector3(.67f,.63f,.22f),StarterBlue);
            Prims.SignText(root,"M",new Vector3(-width*.57f,2.13f,-.285f),.64f,.45f,ArtDirection.Cream);
        }
    }
}
