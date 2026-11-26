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
                    {
                        float x0=side*w*.33f, z0=d*.48f, width=w*.18f, depth=d*.50f;
                        var soil=DistrictFinishes.Tile("cultivatedSoil",new Vector2(2,4));
                        var coping=DistrictFinishes.Tile("cutStone",new Vector2(1,3),ArtDirection.Hex("D6CCB9"));
                        var mid=new Vector3(x0,h+.41f,z0);
                        detail.Box(mid,new Vector3(width,.28f,depth),coping);
                        detail.Box(mid+Vector3.up*.15f,new Vector3(width-.16f,.048f,depth-.16f),soil);
                        foreach(int edge in new[]{-1,1})
                        {
                            detail.Box(mid+new Vector3(edge*(width-.13f)*.5f,.21f,0),new Vector3(.13f,.22f,depth),coping);
                            detail.Box(mid+new Vector3(0,.21f,edge*(depth-.13f)*.5f),new Vector3(width,.22f,.13f),coping);
                        }
                        int cols=Mathf.Max(1,Mathf.FloorToInt(width/.87f));
                        int rows=Mathf.Max(1,Mathf.FloorToInt(depth/1.05f));
                        for(int row=0;row<rows;row++)
                        for(int col=0;col<cols;col++)
                        {
                            int seed=col*11+row*3+side+2;
                            var foot=mid+new Vector3(((col+.5f)/cols-.5f)*(width-.22f),.18f,
                                ((row+.5f)/rows-.5f)*(depth-.25f));
                            var species=seed%11==0?PlantSpecies.Spirea:seed%7==0?PlantSpecies.SilverGrass:
                                seed%5==0?PlantSpecies.GoldenBush:PlantSpecies.Broadleaf;
                            hedge.Add(species,foot,.84f+(seed%4)*.084f,.46f+(seed%5)*.102f,seed);
                        }
                    }
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
            bool tailor = p.Id=="expansion_shop_s_market_03";
            bool retail = p.Model == "shop", market = p.Model == "market" || p.Model == "warehouse";
            bool glassPavilion = district==3 && retail && w<=15 && d<=14 && h>4.4f;
            string roof = DistrictFinishes.Roof(district);
            var trim = ArtDirection.Hex(district == 1 ? "E5DBCB" : district == 2 ? "E9EBE3" : "E0E8EC");
            var frame = Mats.Lit(ArtDirection.Hex(district == 1 ? "655842" : district == 2 ? "516D67" : "504C44"));
            Color accent = ArtDirection.Hex(district == 1 ? (seed%2==0?"327476":"46767C") : district == 2 ? "558498":"467789");
            float wallHeight=glassPavilion?3.48f:h;
            var shell = Prims.Box(root,"Street masonry",new Vector3(0,wallHeight*.5f,d*.5f),new Vector3(w,wallHeight,d),
                DistrictFinishes.Tile(DistrictFinishes.Masonry(district),new Vector2(Mathf.Max(1,w/4),Mathf.Max(1,h/3.5f))),true);
            shell.AddComponent<PlaceMarker>().PlaceId=p.Id;
            if(glassPavilion)
            {
                // Keep the original selectable envelope while opening a recessed continuous glazed gallery above the shop.
                var collider=shell.GetComponent<BoxCollider>();
                collider.center=new Vector3(0,(h-wallHeight)/(2*wallHeight),0);
                collider.size=new Vector3(1,h/wallHeight,1);
                Prims.Box(root,"展廊后墙",new Vector3(0,(h+wallHeight)*.5f,d*.5f+.37f),
                    new Vector3(w-1.32f,h-wallHeight,d-.74f),DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("657678")));
            }
            ForestFloorShade.Footprint(root,new Vector3(0,.115f,d*.48f),w+3.6f,d+3,true);
            var detail = new MiniatureDetailMesh();
            var stone = DistrictFinishes.Tile("cutStone",Vector2.one*.36f);
            var wood = DistrictFinishes.Tile("walnut",Vector2.one*.55f);
            detail.Box(new Vector3(0,.22f,d*.5f),new Vector3(w+.20f,.44f,d+.20f),stone);
            detail.Box(new Vector3(0,h+.15f,d*.5f),new Vector3(w+.39f,.30f,d+.39f),stone);
            if(tailor)
            {
                float transom=3.64f;
                var lime=DistrictFinishes.Tile("ivoryPlaster",new Vector2(w*.26f,(h-transom)*.32f),ArtDirection.Hex("F2DDD0"));
                Prims.Box(root,"楼上石灰抹面",new Vector3(0,(h+transom)*.5f,d*.5f),new Vector3(w+.03f,h-transom,d+.03f),lime);
                detail.Box(new Vector3(0,transom-.08f,d*.5f),new Vector3(w+.11f,.14f,d+.13f),stone);
            }
            Prims.Box(root,"Roof covering",new Vector3(0,h+.333f,d*.5f),new Vector3(w+.14f,.10f,d+.14f),
                DistrictFinishes.Tile(roof,new Vector2(w/4,d/4),tailor?new Color(.91f,.92f,1.0f):Color.white));
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
                    if(glassPavilion && level>0 && face!=2)continue;
                    float y=level==0?.65f:3.65f+(level-.76f)*(h-3.65f)/floors;
                    for(int bay=0;bay<bays;bay++)
                    {
                        float x=((bay+.5f)/bays-.5f)*width;
                        var at=new Vector3(x,y,-depth*.5f-.025f);
                        if(tailor)
                        {
                            float span=Mathf.Min(1.67f,width/bays*.66f);
                            var center=at+Vector3.up*.77f;
                            var reveal=new Vector3(.12f,.13f,.12f);
                            var size=new Vector3(span,1.62f,.14f);
                            Prims.Box(elevation,"窗洞外沿",center,size+reveal,stone);
                            Prims.Box(elevation,"窗框",center+Vector3.back*.086f,size,frame);
                            var panes=Mats.Lit(ArtDirection.Hex((bay+face)%2==0?"788784":"859390"),.34f);
                            Prims.Box(elevation,"住家玻璃",center+Vector3.back*.159f,new Vector3(span-.14f,1.47f,.035f),panes);
                            var curtain=DistrictFinishes.Tile("ivoryPlaster",Vector2.one,ArtDirection.Hex("A7A89A"));
                            for(int fold=0;fold<3;fold++)
                            {
                                float inset=(bay%2==0?-1:1)*(span*.43f-fold*.077f);
                                Prims.Box(elevation,"折帘",center+new Vector3(inset,-.10f,-.184f),
                                    new Vector3(.077f,1.15f,.024f),curtain);
                            }
                            Prims.Box(elevation,"中梃",center+Vector3.back*.19f,new Vector3(.054f,1.51f,.052f),frame);
                            Prims.Box(elevation,"上亮横档",center+new Vector3(0,.44f,-.19f),new Vector3(span-.095f,.048f,.05f),frame);
                            Prims.Box(elevation,"石质窗台",at+Vector3.back*.12f,new Vector3(span+.27f,.12f,.39f),stone);
                            if(face==0 && bay==bays-1 && level==1)
                            {
                                var box=at+new Vector3(.16f,.035f,-.37f);
                                var enamel=Mats.Lit(ArtDirection.Hex("D7D5C2"));
                                var screen=Mats.Lit(ArtDirection.Hex("646C66"));
                                Prims.Box(elevation,"窗下空调外壳",box,new Vector3(.87f,.49f,.39f),enamel);
                                var fan=Prims.Shape(PrimitiveType.Cylinder,elevation,"风扇护网",box+new Vector3(-.105f,0,-.203f),new Vector3(.39f,.014f,.39f),screen);
                                fan.transform.localRotation=Quaternion.Euler(90,0,0);
                                for(int slit=-2;slit<=2;slit++)
                                    Prims.Box(elevation,"风扇横栅",box+new Vector3(-.105f,slit*.060f,-.222f),new Vector3(.32f,.016f,.015f),enamel);
                            }
                        }
                        else if (!retail && (district==1 || level==0))
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
                        if(!tailor && face==0 && level>0 && district==1 && (bay+level+seed)%3==0)
                            QuarterModules.Add(elevation,"BalconyRail",at,Vector3.one*.88f);
                    }
                }
            }

            // Three dimensional recessed shopfront, with separate operating and closed states.
            var scale = new Vector3(w/8.70f,1,1);
            if (retail)
            {
                Transform front;
                if(tailor)
                {
                    // This shop has its own fitted open frontage; the generic module includes opaque stock and a solid door.
                    front=new GameObject("DisplayFront").transform;front.SetParent(root,false);
                    front.localPosition=Vector3.back*.035f;front.localScale=scale;
                }
                else front=QuarterModules.Add(root,"DisplayFront",Vector3.back*.035f,scale);
                DistrictFinishes.ShopDisplay(front,p);
                if(glassPavilion) GardenStructures.ShopDisplayWindows(front,string.IsNullOrEmpty(p.Business)||p.Business=="空铺");
                if(p.Business=="裁缝")
                {
                    var rack=new GameObject("收摊时移入店内的布架").transform;rack.SetParent(root,false);
                    rack.localPosition=new Vector3(-w*(tailor?.398f:.34f),.12f,-.68f);
                    if(tailor)rack.localScale=new Vector3(.75f,.88f,.80f);
                    _cur.OpenOnly.Add(rack.gameObject);
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
                    var foot=new Vector3(w*(tailor?.264f:.33f),.12f,-.52f);
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
                    if(tailor)
                    {
                        var shelter=new Vector3(-w*.5f-1.15f,0,d-1.40f);
                        var honey=Mats.Lit(ArtDirection.Hex("B99162"));
                        var cedar=Mats.Lit(ArtDirection.Hex("A78257"));
                        var rail=Mats.Lit(ArtDirection.Hex("767D73"),.26f);
                        var slope=Quaternion.Euler(0,0,-4);
                        for(int side=-1;side<=1;side+=2)
                        {
                            float x=side*.76f, eave=2.14f-x*.07f;
                            detail.Box(shelter+new Vector3(x,eave,0),new Vector3(.080f,.095f,3.03f),honey);
                            foreach(int end in new[]{-1,1})
                                detail.Box(shelter+new Vector3(x,eave*.5f,end*1.31f),new Vector3(.081f,eave,.083f),cedar);
                            float rackX=side*.37f+.20f;
                            foreach(int end in new[]{-1,1})
                                detail.Box(shelter+new Vector3(rackX,.41f,end*.34f),new Vector3(.053f,.74f,.053f),rail);
                            detail.Box(shelter+new Vector3(rackX,.78f,0),new Vector3(.053f,.055f,.73f),rail);
                            QuarterModules.Add(root,"Cycle",shelter+new Vector3(side*.37f,.07f,side*.16f),Vector3.one,side<0?3:176);
                        }
                        // Short cedar boards span the two long purlins. The warm cut ends and fine seams read from above.
                        for(int board=0;board<12;board++)
                            detail.Box(shelter+new Vector3(0,2.218f,(board-5.5f)*3.10f/12),new Vector3(1.80f,.068f,.253f),
                                board%4==1?cedar:honey,slope);
                    }
                    else QuarterModules.Add(root,"Cycle",new Vector3(-w*.5f-.26f,.07f,d*.25f),Vector3.one,8);
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
                    if(tailor)
                    {
                        // Folded stock occupies the outer third of one bay. The opposite bay is the dress-form display.
                        var bolts=new[]{"8F644E","6A8085","BDAC86"};
                        var stand=new Vector3(-w*.388f,.78f,-.35f);
                        joinery.Box(stand,new Vector3(w*.097f,.070f,.24f),wood);
                        for(int pile=0;pile<2;pile++)for(int layer=0;layer<2+pile;layer++)
                            joinery.Box(stand+new Vector3((pile-.5f)*.32f,.070f+layer*.065f,-.02f),
                                new Vector3(.29f,.062f,.20f),Mats.Lit(ArtDirection.Hex(bolts[(pile+layer)%bolts.Length])));
                        var oak=DistrictFinishes.Tile("walnut",Vector2.one*.62f,new Color(1.38f,1.18f,.92f));
                        var brass=Mats.Lit(ArtDirection.Hex("AA8B5D"),.27f);
                        var door=new Vector3(0,1.19f,-.42f);
                        joinery.Box(door+Vector3.up*1.05f,new Vector3(1.36f,.12f,.12f),oak);
                        joinery.Box(door+Vector3.down*1.05f,new Vector3(1.36f,.12f,.12f),oak);
                        joinery.Box(door+Vector3.down*.65f,new Vector3(1.21f,.65f,.095f),oak);
                        joinery.Box(door+Vector3.down*.33f,new Vector3(1.28f,.082f,.14f),oak);
                        foreach(int side in new[]{-1,1})
                        {
                            joinery.Box(door+Vector3.right*side*.62f,new Vector3(.105f,2.11f,.13f),oak);
                            joinery.Box(door+new Vector3(side*.24f,-.61f,-.076f),new Vector3(.39f,.43f,.054f),wood);
                        }
                        joinery.Box(door+new Vector3(0,.27f,-.12f),new Vector3(.047f,1.40f,.055f),oak);
                        joinery.Box(door+new Vector3(0,.61f,-.12f),new Vector3(1.13f,.045f,.055f),oak);
                        joinery.Box(door+new Vector3(.42f,-.07f,-.17f),new Vector3(.048f,.28f,.072f),brass);
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
            if(tailor)
            {
                float pitch=-17f;
                var pose=Quaternion.Euler(pitch,0,0);
                var canvas=DistrictFinishes.Tile("sageCloth",new Vector2(2,1),new Color(.50f,.79f,.90f));
                var piping=Mats.Lit(ArtDirection.Hex("A1A494"));
                var at=new Vector3(0,3.02f,-.55f);
                detail.Box(at,new Vector3(canopyWidth,.10f,1.21f),canvas,pose);
                var lip=at+pose*new Vector3(0,-.09f,-.58f);
                detail.Box(lip,new Vector3(canopyWidth,.195f,.072f),canvas);
                detail.Box(lip+new Vector3(0,-.082f,-.04f),new Vector3(canopyWidth,.024f,.027f),piping);
                foreach(int side in new[]{-1,1})
                    GardenStructures.Strut(detail,new Vector3(side*w*.42f,2.30f,-.08f),lip+new Vector3(side*w*.42f,0,.05f),.045f,.045f,frame);
            }
            else if(market && district < 3)
            {
                var awning=QuarterModules.Add(root,"TinCanopy",new Vector3(0,2.88f,-.12f),new Vector3(canopyWidth/7.8f,.9f,.90f));
                if (seed%3==0) QuarterModules.Tint(awning,ArtDirection.Hex("E2CBB3"));
            }
            else if(district==3 && retail)
            {
                // The upper silver screen shades the window. A narrow reveal keeps the full display visible.
                var reveal=DistrictFinishes.Tile("brushedAlloy",Vector2.one);
                detail.Box(new Vector3(0,2.95f,-.30f),new Vector3(canopyWidth,.13f,.56f),reveal);
            }
            else
            {
                var cap=DistrictFinishes.Tile(district==1?"sageCloth":"steel",Vector2.one*.4f);
                detail.Box(new Vector3(0,2.85f,-.67f),new Vector3(canopyWidth,.16f,1.49f),district==1 ? Mats.Lit(ArtDirection.Hex("527C79")) : cap,Quaternion.Euler(-8,0,0));
                for(int i=0;i<4;i++)
                    detail.Box(new Vector3((i-1.5f)*canopyWidth/4,2.61f,-.50f),new Vector3(.09f,.11f,1.03f),frame,Quaternion.Euler(-23,0,0));
            }
            bool gardenRetail=district==3 && retail;
            if(gardenRetail)accent=ArtDirection.Hex("535E60");
            var fascia=Mats.Lit(p.Kind==PlaceKind.Agency?AgencyColor(p):accent);
            float signY=tailor?3.46f:3.34f;
            float signWidth=w*(tailor||gardenRetail?.65f:.96f);
            float signHeight=gardenRetail?.39f:tailor?.33f:.76f;
            detail.Box(new Vector3(0,signY,-.15f),new Vector3(signWidth,signHeight,.22f),tailor?wood:fascia);
            Prims.SignText(root,p.Name,new Vector3(0,signY,-.27f),signWidth*.94f,tailor||gardenRetail?.29f:.54f,ArtDirection.Cream);
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
            if(district==3) GardenStructures.StreetFront(root,w,d,h,glassPavilion);
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
