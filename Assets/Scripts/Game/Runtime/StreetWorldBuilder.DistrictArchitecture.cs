using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private int ArtDistrict(string street) => _city.AgencyDistrictFor(street)?.id switch
        {
            "community" => 1, "park" => 2, "garden" => 3, _ => 0
        };

        private static void DistrictVenueEntrance(Transform shell, Place p, float w, float d, int district)
        {
            float h=CityArchitecture.VenueHeight(p);
            var detail=new MiniatureDetailMesh();
            var bronze=DistrictFinishes.Tile(district==3?"walnut":"patina",Vector2.one*.38f);
            var masonry=DistrictFinishes.Tile("cutStone",Vector2.one*.4f);
            bool expo=p.Business=="venue_expo";
            float span=expo ? Mathf.Min(w*.76f,29) : Mathf.Min(w*.78f,13);
            if (expo)
            {
                // The public arcade follows the existing exhibition entrance; the skylights sit on the hall roof.
                for(int rib=0;rib<7;rib++)
                {
                    float x=(rib-3)*span/7;
                    detail.Box(new Vector3(x,4.83f,-.71f),new Vector3(.15f,.32f,2.03f),bronze);
                    if(rib%2==0) detail.Box(new Vector3(x,2.45f,-1.54f),new Vector3(.19f,4.8f,.23f),masonry);
                }
                detail.Box(new Vector3(0,5.04f,-.80f),new Vector3(span,.12f,2.18f),bronze);
                // Three roof monitors sit above the crown of each barrel vault.
                for(int bay=-1;bay<=1;bay++)
                {
                    var pos=new Vector3(bay*w*.30f,h+3.15f,d*.49f);
                    detail.Box(pos,new Vector3(w*.105f,.34f,d*.32f),masonry);
                    detail.Box(pos+Vector3.up*.25f,new Vector3(w*.098f,.17f,d*.30f),DistrictFinishes.WindowGlass());
                    for(int mullion=-2;mullion<=2;mullion++)
                        detail.Box(pos+new Vector3(0,.37f,mullion*d*.061f),new Vector3(w*.10f,.09f,.12f),bronze);
                }
            }
            else
            {
                var hedge=new StreetPlanting(maintained:true);
                foreach(int side in new[]{-1,1})
                {
                    if(p.Model!="warehouse")
                        for(float along=d*.27f;along<d*.74f;along+=1.25f)
                            hedge.Add(PlantSpecies.Hedge,new Vector3(side*w*.33f,h+.64f,along),w*.16f,1.09f,Mathf.RoundToInt(along*7+side));
                    float x=side*w*.37f;
                    QuarterModules.Add(shell,"SideWindow",new Vector3(side*w*.5f,h*.4f,d*.65f),new Vector3(1.65f,1.34f,1),side<0?90:270);
                    QuarterModules.Add(shell,"PlantPot",new Vector3(x,-.04f,-1.12f),Vector3.one*1.28f);
                    if(p.Business=="venue_restaurant")
                        QuarterModules.Add(shell,"StripedCanopy",new Vector3(x,2.98f,-.22f),new Vector3(w*.22f/7.8f,.76f,.91f));
                    else for(int slat=0;slat<4;slat++)
                        detail.Box(new Vector3(x+(slat-1.5f)*.17f,h*.52f,-.37f),new Vector3(.09f,h*.84f,.20f),bronze);
                }
                hedge.Build(shell,"屋顶花池");
            }
            detail.Build(shell,"Arcade and entrance joinery");
            if(district==3) GardenStructures.StreetFront(shell,w,d,h);
        }

        // All elevations use the original pavement-level front door as their origin.
        private void DistrictBusiness(Transform root, Place p, float w, float d, float h, int district, int seed)
        {
            bool retail = p.Model == "shop", market = p.Model == "market" || p.Model == "warehouse";
            string roof = DistrictFinishes.Roof(district);
            var trim = ArtDirection.Hex(district == 1 ? "E5DBCB" : district == 2 ? "E9EBE3" : "E0E8EC");
            var frame = Mats.Lit(ArtDirection.Hex(district == 1 ? "655842" : district == 2 ? "516D67" : "504C44"));
            Color accent = ArtDirection.Hex(district == 1 ? (seed%2==0?"327476":"46767C") : district == 2 ? "558498":"467789");
            var shell = Prims.Box(root,"Street masonry",new Vector3(0,h*.5f,d*.5f),new Vector3(w,h,d),
                DistrictFinishes.Tile(DistrictFinishes.Masonry(district),new Vector2(Mathf.Max(1,w/4),Mathf.Max(1,h/3.5f))),true);
            shell.AddComponent<PlaceMarker>().PlaceId=p.Id;
            ForestFloorShade.Footprint(root,new Vector3(0,.115f,d*.48f),w+3.6f,d+3,true);
            var detail = new MiniatureDetailMesh();
            var stone = DistrictFinishes.Tile("cutStone",Vector2.one*.36f);
            var wood = DistrictFinishes.Tile("walnut",Vector2.one*.55f);
            detail.Box(new Vector3(0,.22f,d*.5f),new Vector3(w+.20f,.44f,d+.20f),stone);
            detail.Box(new Vector3(0,h+.15f,d*.5f),new Vector3(w+.39f,.30f,d+.39f),stone);
            Prims.Box(root,"Roof covering",new Vector3(0,h+.333f,d*.5f),new Vector3(w+.14f,.10f,d+.14f),
                DistrictFinishes.Tile(roof,new Vector2(w/4,d/4),Color.white));
            foreach(int side in new[]{-1,1})
            {
                detail.Box(new Vector3(side*w*.49f,h+.58f,d*.5f),new Vector3(.18f,.48f,d+.20f),stone);
                detail.Box(new Vector3(0,h+.58f,d*.5f+side*d*.50f),new Vector3(w,.48f,.18f),stone);
            }
            int floors = Mathf.Max(0,Mathf.FloorToInt((h-3.55f)/2.3f));
            for(int face=0;face<4;face++)
            {
                var elevation=new GameObject("Window bays").transform;
                elevation.SetParent(root,false);
                elevation.localPosition=new Vector3(0,0,d*.5f);
                elevation.localRotation=Quaternion.Euler(0,face*90,0);
                float width=face%2==0?w:d, depth=face%2==0?d:w;
                int bays=Mathf.Clamp(Mathf.RoundToInt(width/3.2f),2,8);
                for(int level=face==0?1:0;level<=floors;level++)
                {
                    float y=level==0?.65f:3.65f+(level-.76f)*(h-3.65f)/floors;
                    for(int bay=0;bay<bays;bay++)
                    {
                        float x=((bay+.5f)/bays-.5f)*width;
                        var at=new Vector3(x,y,-depth*.5f-.025f);
                        if (!retail && (district==1 || level==0))
                            QuarterModules.Add(elevation,"SideWindow",at,new Vector3(Mathf.Min(1.45f,width/bays*.44f),1.03f,.8f));
                        else
                        {
                            float ww=width/bays*.73f;
                            var glass=district==1 ? DistrictFinishes.WindowGlass() : DistrictFinishes.Tile("celadonGlass",Vector2.one);
                            Prims.Box(elevation,"Bay frame",at+Vector3.up*.68f,new Vector3(ww+.16f,1.57f,.18f),frame);
                            Prims.Box(elevation,"Glazing",at+new Vector3(0,.68f,-.12f),new Vector3(ww,1.38f,.08f),glass);
                            Prims.Box(elevation,"Slender mullion",at+new Vector3(0,.68f,-.18f),new Vector3(.07f,1.42f,.08f),frame);
                            Prims.Box(elevation,"Projecting sill",at+Vector3.back*.10f,new Vector3(ww+.24f,.11f,.38f),stone);
                        }
                        if(face==0 && level>0 && district==1 && (bay+level+seed)%3==0)
                            QuarterModules.Add(elevation,"BalconyRail",at,Vector3.one*.88f);
                    }
                }
            }

            // Three dimensional recessed shopfront, with separate operating and closed states.
            var scale = new Vector3(w/8.70f,1,1);
            if (retail)
            {
                var front=QuarterModules.Add(root,"DisplayFront",Vector3.back*.035f,scale);
                DistrictFinishes.ShopDisplay(front,p);
                if(p.Business=="裁缝")
                {
                    var rack=new GameObject("收摊时移入店内的布架").transform;rack.SetParent(root,false);
                    rack.localPosition=new Vector3(-w*.34f,.12f,-.68f);_cur.OpenOnly.Add(rack.gameObject);
                    var display=new MiniatureDetailMesh();
                    display.Box(new Vector3(0,.41f,0),new Vector3(1.03f,.09f,.53f),wood);
                    foreach(int leg in new[]{-1,1})display.Box(new Vector3(leg*.43f,.19f,0),new Vector3(.09f,.39f,.42f),wood);
                    for(int bolt=0;bolt<3;bolt++)
                    {
                        var fabric=Mats.Lit(ArtDirection.Hex(bolt==0?"536679":bolt==1?"A38563":"B29E8E"));
                        var roll=Prims.Shape(PrimitiveType.Cylinder,rack,"布卷",new Vector3((bolt-1)*.30f,.79f,-.02f),new Vector3(.25f,.38f,.25f),fabric);
                        roll.transform.localRotation=Quaternion.Euler(-14,0,(bolt-1)*7);
                    }
                    display.Build(rack,"布架");
                    // The shop keeps its original entrance; the mannequin sits in the outer display bay.
                    var dummy=new MiniatureDetailMesh();
                    var foot=new Vector3(w*.33f,.12f,-.52f);
                    var linen=Mats.Lit(ArtDirection.Hex("CFB695"));
                    dummy.Box(foot+Vector3.up*.05f,new Vector3(.49f,.10f,.44f),frame);
                    dummy.Box(foot+Vector3.up*.49f,new Vector3(.075f,.86f,.075f),frame);
                    dummy.Lobe(foot+Vector3.up*1.09f,new Vector3(.26f,.37f,.16f),linen,3);
                    dummy.Lobe(foot+Vector3.up*1.38f,new Vector3(.29f,.19f,.16f),linen,4);
                    dummy.Box(foot+Vector3.up*1.56f,new Vector3(.11f,.13f,.10f),wood);
                    var dummyDisplay=new GameObject("营业时陈列的人台").transform;
                    dummyDisplay.SetParent(root,false);
                    dummy.Build(dummyDisplay,"橱窗布样人台");
                    _cur.OpenOnly.Add(dummyDisplay.gameObject);
                    QuarterModules.Add(root,"Cycle",new Vector3(-w*.5f-.26f,.07f,d*.25f),Vector3.one,8);
                    var pots=new StreetPlanting(maintained:true);
                    pots.Add(PlantSpecies.Broadleaf,new Vector3(w*.44f,.48f,-.74f),.88f,.87f,seed);
                    pots.Build(root,"门边盆栽");
                }
                if(district==1 && p.Business=="裁缝")
                {
                    var joinery=new MiniatureDetailMesh();
                    var timber=Mats.Lit(ArtDirection.Hex("A1825E"));
                    foreach(int side in new[]{-1,1})
                    {
                        var bay=new Vector3(side*w*.31f,1.29f,-.27f);
                        joinery.Box(bay-Vector3.up*.81f,new Vector3(w*.26f,.11f,.24f),timber);
                        joinery.Box(bay+Vector3.up*.83f,new Vector3(w*.26f,.10f,.19f),timber);
                        for(int edge=-1;edge<=1;edge+=2)
                            joinery.Box(bay+Vector3.right*edge*w*.124f,new Vector3(.085f,1.65f,.14f),timber);
                    }
                    var frames=new GameObject("营业窗框").transform;frames.SetParent(root,false);
                    joinery.Build(frames,"暖木橱窗压条");_cur.OpenOnly.Add(frames.gameObject);
                }
                var shutter=QuarterModules.Add(root,"ShutterFront",Vector3.back*.045f,scale);
                shutter.gameObject.SetActive(false);_cur.OpenOnly.Add(front.gameObject);_cur.ClosedOnly.Add(shutter.gameObject);
                if(district==3)
                {
                    for(int j=0;j<5;j++)
                        detail.Box(new Vector3(-w*.458f+j*.12f,1.53f,-.34f),new Vector3(.065f,2.85f,.17f),wood);
                }
            }
            else
            {
                detail.Window(new Vector3(0,1.38f,-.08f),1.9f,2.56f,trim,true);
                foreach(int side in new[]{-1,1})
                {
                    if(market) QuarterModules.Add(root,"CargoBay",new Vector3(side*w*.29f,0,-.03f),new Vector3(w*.28f/3.47f,1,1));
                    else detail.Window(new Vector3(side*w*.29f,1.45f,-.08f),w*.27f,1.95f,trim,true);
                }
            }
            float canopyWidth=Mathf.Max(2,w*(retail || market ? .95f : .52f));
            if(market && district < 3)
            {
                var awning=QuarterModules.Add(root,"TinCanopy",new Vector3(0,2.88f,-.12f),new Vector3(canopyWidth/7.8f,.9f,.90f));
                if (seed%3==0) QuarterModules.Tint(awning,ArtDirection.Hex("E2CBB3"));
            }
            else
            {
                var cap=DistrictFinishes.Tile(district==1?"sageCloth":"steel",Vector2.one*.4f);
                detail.Box(new Vector3(0,2.85f,-.67f),new Vector3(canopyWidth,.16f,1.49f),district==1 ? Mats.Lit(ArtDirection.Hex("527C79")) : cap,Quaternion.Euler(-8,0,0));
                for(int i=0;i<4;i++)
                    detail.Box(new Vector3((i-1.5f)*canopyWidth/4,2.61f,-.50f),new Vector3(.09f,.11f,1.03f),frame,Quaternion.Euler(-23,0,0));
            }
            var fascia=Mats.Lit(p.Kind==PlaceKind.Agency?AgencyColor(p):accent);
            detail.Box(new Vector3(0,3.34f,-.15f),new Vector3(w*.96f,.76f,.22f),fascia);
            Prims.SignText(root,p.Name,new Vector3(0,3.34f,-.27f),w*.90f,.54f,ArtDirection.Cream);
            if(p.Kind==PlaceKind.Agency)
                Prims.SignText(root,_city.AgencyCoverageName(p)+" · 租售办理",new Vector3(0,2.19f,-.29f),w*.6f,.32f,ArtDirection.Cream);
            if(p.Business==GameSession.HospitalBusiness)
            {
                var red=Mats.Lit(ArtDirection.Clay);
                detail.Box(new Vector3(-w*.42f,3.34f,-.28f),new Vector3(.12f,.47f,.06f),red);
                detail.Box(new Vector3(-w*.42f,3.34f,-.28f),new Vector3(.47f,.12f,.06f),red);
            }
            var bulb=Mats.Glow(ArtDirection.Hex("E4C79A"),new Color(1.4f,.83f,.36f));
            foreach(int side in new[]{-1,1})
            {
                float x=side*(w*.45f);
                detail.Box(new Vector3(x,2.11f,-.25f),new Vector3(.17f,.44f,.22f),frame);
                detail.Box(new Vector3(x,2.11f,-.39f),new Vector3(.13f,.34f,.07f),bulb);
            }
            var lantern=new GameObject("Warm entrance downlight").AddComponent<Light>();
            lantern.transform.SetParent(root,false);lantern.transform.localPosition=new Vector3(0,2.54f,-.76f);
            lantern.type=LightType.Point;lantern.range=5.4f;lantern.intensity=0;
            lantern.color=new Color(1,.76f,.51f);lantern.shadows=LightShadows.None;NightLights.Add(lantern);
            var open=Prims.Box(root,"Open lamp",new Vector3(w*.40f,2.56f,-.31f),new Vector3(.23f,.13f,.04f),Mats.Unlit(UiGreen));
            var closed=Prims.Box(root,"Closed lamp",open.transform.localPosition,open.transform.localScale,Mats.Lit(ArtDirection.Clay));
            _cur.OpenOnly.Add(open);_cur.ClosedOnly.Add(closed);
            if(district==3) GardenStructures.StreetFront(root,w,d,h);
            else QuarterModules.Add(root,"Condenser",new Vector3(w*.26f,h+.39f,d*.61f),Vector3.one,90);
            if(district<3) QuarterModules.Add(root,"ServiceStack",new Vector3(-w*.48f,0,-.04f),new Vector3(1,h/3.05f,.8f));
            QuarterModules.Add(root,"PlantPot",new Vector3(w*.42f,-.06f,-.73f),Vector3.one);
            if(market)
            {
                var stock=new StreetPlanting(maintained:true);
                foreach(int side in new[]{-1,1})
                {
                    float x=side*w*.31f;
                    var display=QuarterModules.Add(root,"Crates",new Vector3(x,.10f,-.81f),new Vector3(1.44f,1,1));
                    _cur.OpenOnly.Add(display.gameObject);
                    stock.Add(PlantSpecies.Cabbage,new Vector3(x,.65f,-.87f),1.02f,.33f,seed%241+side);
                }
                var produce=new GameObject("Produce on display").transform;produce.SetParent(root,false);
                stock.Build(produce,"时蔬");_cur.OpenOnly.Add(produce.gameObject);
            }
            if(p.Model=="studio")
            {
                for(int sample=0;sample<3;sample++)
                {
                    var at=new Vector3((sample-1)*1.16f-w*.23f,.2f,-.68f);
                    detail.Box(at,new Vector3(.81f,.34f,.76f),wood);
                    detail.Lobe(at+Vector3.up*.61f,new Vector3(.31f,.48f,.25f),sample==1?frame:stone,sample+8);
                }
            }
            if(retail && seed%3==1) QuarterModules.Add(root,"Chalkboard",new Vector3(-w*.33f,.1f,-.86f),Vector3.one,6);
            if((p.Business??"").Contains("模型"))
                for(int i=-1;i<=1;i++)
                {
                    var at=new Vector3(i*.4f-w*.29f,.8f,-.35f);
                    detail.Box(at,new Vector3(.22f,.31f,.18f),i==0?frame:wood);
                    detail.Lobe(at+Vector3.up*.23f,new Vector3(.15f,.15f,.14f),stone,i+1);
                }
            detail.Build(root,"Cornices, recessed entrance and facade fittings");
            CityArchitecture.StreetApron(root,w,paving:DistrictFinishes.Tile("graniteWalk",new Vector2(w/4,.9f)));
        }
    }
}
