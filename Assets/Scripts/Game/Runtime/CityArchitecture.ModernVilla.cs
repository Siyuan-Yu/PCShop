using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static partial class CityArchitecture
    {
        private static void ModernGardenVilla(Transform root,float w,float d,float configuredHeight,int seed)
        {
            int variant=(seed&int.MaxValue)%3;
            Color white=ArtDirection.Hex(variant==0?"EDF2EF":variant==1?"E3EDF0":"D8E5E5");
            Color dark=ArtDirection.Hex("3D5365"),wood=ArtDirection.Hex("A37955"),paving=ArtDirection.Hex("D0DCDC");
            float compact=1-Mathf.InverseLerp(16,24,w);
            float floor=3.0f,h=floor*3,bw=w*Mathf.Lerp(.83f,.89f,compact),bd=d*Mathf.Lerp(.42f,.54f,compact);
            float z=d*Mathf.Lerp(.70f,.685f,compact),front=z-bd/2;
            Prims.Box(root,"PrivateEstateLawn",new Vector3(0,.045f,d/2),new Vector3(w,.09f,d),
                IndustrialMaterials.Surface("meadow",Lawn,new Vector2(w/25,d/25)));
            Prims.Box(root,"StoneTerrace",new Vector3(0,.14f,d*.37f),new Vector3(w*.85f,.18f,d*.36f),
                DistrictFinishes.Tile("pearlCeramic",new Vector2(w*.85f/6.4f,d*.36f/4.8f),ArtDirection.Hex("D4DEE0")));
            var innerWall=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("48625E"));
            void Storey(string name,Vector3 at,Vector3 size)
            {
                // Recess the visible envelope without shrinking the building's collision footprint.
                var recessed=size-new Vector3(1.76f,0,1.76f);
                var shell=Prims.Box(root,name,at,recessed,innerWall,true);
                shell.GetComponent<BoxCollider>().size=new Vector3(size.x/recessed.x,1,size.z/recessed.z);
            }
            Storey("GroundFloor",new Vector3(0,floor/2+.2f,z),new Vector3(bw,floor,bd));
            Box(root,"GroundFloorPlinth",new Vector3(0,.26f,z),new Vector3(bw+.3f,.3f,bd+.25f),dark);
            // A smaller, shifted upper storey leaves a real terrace and an overhanging wing.
            float upperW=bw*Mathf.Lerp(variant==1?.86f:.91f,.86f,compact),upperD=bd*Mathf.Lerp(.97f,.88f,compact);
            float upperX=(variant==2?1:-1)*bw*Mathf.Lerp(.13f,.022f,compact);
            float upperZ=z-Mathf.Lerp(1.15f,.64f,compact);
            var ceramic = DistrictFinishes.Tile("pearlCeramic",new Vector2(2,2),white);
            var planterStone=DistrictFinishes.Tile("cutStone",new Vector2(3,2),ArtDirection.Hex("9BAD9C"));
            var satinAlloy=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("A1B7BA"));
            var insetTrim=Mats.Lit(ArtDirection.Hex("8FC8C8"),.45f,.22f);
            var glassEdge=Mats.Lit(ArtDirection.Hex("85B9B4"),.44f,.24f);
            var garden = new MiniatureDetailMesh();
            var borders = new StreetPlanting(maintained:true);
            Storey("UpperStorey",new Vector3(upperX,floor*1.5f+.5f,upperZ),new Vector3(upperW,floor,upperD));
            float firstY=floor+.38f, secondY=floor*2+.59f;
            float terraceDepth=bd+2.05f, terraceWidth=bw+1.42f;
            Vector3 first=new Vector3(0,firstY,z-.60f);
            Vector3 second=new Vector3(upperX*.55f,secondY,z-.53f);
            void Terrace(Vector3 at,float width,float depth,int level)
            {
                GardenStructures.Plate(root,"悬挑板阴缝",at-Vector3.up*.21f,new Vector3(width-.10f,.22f,depth-.11f),innerWall,sculpted:true);
                GardenStructures.Plate(root,"弧形白瓷露台",at,new Vector3(width,.28f,depth),ceramic,sculpted:true);
                // Large porcelain panels return to the curved nosing. The recessed rim
                // catches daylight while the sheltered floor holds a cooler, softer reflection.
                var porcelain=DistrictFinishes.Tile("pearlCeramic",new Vector2(width/6.4f,depth/4.8f),ArtDirection.Hex("ADC3C8"));
                GardenStructures.Plate(root,"拉丝铝封边",at+Vector3.up*.151f,new Vector3(width-.10f,.033f,depth-.10f),satinAlloy,sculpted:true);
                GardenStructures.Plate(root,"退台哑光瓷板",at+Vector3.up*.178f,new Vector3(width-.21f,.043f,depth-.21f),porcelain,sculpted:true);
                GardenStructures.WingInlay(root,"露台嵌条",at+Vector3.up*.204f,width-.21f,depth-.21f,insetTrim);
                GardenStructures.BalconyGlass(root,garden,at,width,depth,glassEdge);
                var rear=at+new Vector3(0,.81f,depth*.472f);
                garden.Box(rear,new Vector3(width*.58f,.075f,.084f),glassEdge);
                foreach(int post in new[]{-2,-1,0,1,2})
                    garden.Box(rear+new Vector3(post*width*.138f,-.31f,0),new Vector3(.048f,.62f,.058f),innerWall);
                for(int j=0;j<3;j++)
                {
                    float fraction=(level==1?-.19f:.16f)+(j-1)*.047f;
                    var foot=GardenStructures.FrontEdge(at,width,depth,fraction)+new Vector3(0,.23f,.39f);
                    garden.Box(foot-Vector3.up*.08f,new Vector3(width*.056f,.25f,.53f),planterStone);
                    borders.Add(j==1?PlantSpecies.Spirea:PlantSpecies.Hydrangea,foot,1.27f,.94f,level*9+j/2);
                }
                foreach(int side in new[]{-1,1})
                {
                    // The planters follow the real curved terrace and return along its short sides.
                    // Massed leaves sit above the sill while the middle bays stay open to the rooms.
                    for(int j=0;j<4;j++)
                    {
                        var rim=GardenStructures.FrontEdge(at,width,depth,side*(.46f-j*.056f));
                        var foot=rim+new Vector3(-side*.17f,.23f,.32f);
                        var species=j==2?PlantSpecies.Boxwood:PlantSpecies.Hydrangea;
                        garden.Box(foot-Vector3.up*.07f,new Vector3(width*.064f,.25f,.52f),planterStone);
                        borders.Add(species,foot,1.43f,1.03f+.15f*(j%2),(seed%97)+side*3+level*5);
                        if(j%2==0)
                            borders.Add(PlantSpecies.Broadleaf,foot+new Vector3(0,-.13f,-.19f),.72f,.57f,j+level*23);
                    }
                    for(int j=0;j<6;j++)
                    {
                        float fraction=-.20f+j*.115f;
                        float outerX=width*(j>4?.44f:.48f)-.27f;
                        var foot=at+new Vector3(side*outerX,.24f,depth*fraction);
                        garden.Box(foot-Vector3.up*.07f,new Vector3(.52f,.25f,depth*.14f),planterStone);
                        borders.Add(j==1?PlantSpecies.Hydrangea:PlantSpecies.Boxwood,foot,1.07f,.80f,seed%127+side+j);
                    }
                }
            }
            Terrace(first,terraceWidth,terraceDepth,1);
            Terrace(second,terraceWidth*.91f,terraceDepth*.92f,2);
            // Each pair of white branches seats directly under the curved front edge of the slab above.
            void Column(Vector3 foot,Vector3 slab,float width,float depth,float fraction)
            {
                var head=GardenStructures.FrontEdge(slab,width,depth,fraction)+new Vector3(0,-.14f,.12f);
                var split=Vector3.Lerp(foot,head,.36f);
                GardenStructures.Strut(garden,foot,split,.27f,.30f,ceramic);
                foreach(int arm in new[]{-1,1})
                {
                    var seat=GardenStructures.FrontEdge(slab,width,depth,fraction+arm*.112f)+new Vector3(0,-.14f,.12f);
                    var elbow=Vector3.Lerp(split,seat,.47f)-new Vector3(arm*.16f,0,0);
                    GardenStructures.Strut(garden,split,elbow,.23f,.26f,ceramic);
                    GardenStructures.Strut(garden,elbow,seat,.19f,.23f,ceramic);
                }
            }
            foreach(int side in new[]{-1,1})
            {
                var foot=new Vector3(side*w*.40f,.24f,front-1.04f);
                Column(foot,first,terraceWidth,terraceDepth,side*.33f);
                var balcony=GardenStructures.FrontEdge(first,terraceWidth,terraceDepth,side*.33f);
                Column(balcony+new Vector3(0,.19f,.23f),second,terraceWidth*.91f,terraceDepth*.92f,side*.30f);
            }
            ModernGlazing(root,garden,new Vector3(0,1.65f,front-.065f),bw*.94f,2.28f,0,dark);
            ModernGlazing(root,garden,new Vector3(0,1.65f,z+bd/2+.065f),bw*.90f,2.28f,180,dark);
            ModernGlazing(root,garden,new Vector3(upperX,floor+1.86f,upperZ-upperD/2-.065f),upperW*.94f,2.50f,0,dark,sheltered:true);
            foreach(int side in new[]{-1,1})
            {
                ModernGlazing(root,garden,new Vector3(side*(bw/2+.065f),1.65f,z),bd*.92f,2.28f,side<0?90:270,dark);
                ModernGlazing(root,garden,new Vector3(upperX+side*(upperW/2+.065f),floor+1.86f,upperZ),upperD*.90f,2.50f,side<0?90:270,dark,sheltered:true);
            }
            ModernGlazing(root,garden,new Vector3(upperX,floor+1.86f,upperZ+upperD/2+.065f),upperW*.93f,2.50f,180,dark);
            float topW=upperW*Mathf.Lerp(.90f,.87f,compact), topD=upperD*Mathf.Lerp(.88f,.83f,compact);
            float topX=upperX+Mathf.Sign(upperX)*upperW*.025f, topZ=z+bd*.085f-.52f;
            float glassY=floor*2+1.87f;
            Storey("SetbackStorey",new Vector3(topX,floor*2.5f+.5f,topZ),new Vector3(topW,floor,topD));
            ModernGlazing(root,garden,new Vector3(topX,glassY,topZ-topD/2-.065f),topW*.94f,2.45f,0,dark);
            ModernGlazing(root,garden,new Vector3(topX,glassY,topZ+topD/2+.065f),topW*.86f,2.45f,180,dark);
            foreach(int side in new[]{-1,1})
                ModernGlazing(root,garden,new Vector3(topX+side*(topW/2+.065f),glassY,topZ),topD*.88f,2.45f,side<0?90:270,dark);
            var crown=new Vector3(topX,h+.63f,topZ-.34f);
            float roofWidth=topW+3.27f,roofDepth=topD+1.40f;
            GardenStructures.Plate(root,"顶层悬挑翼",crown,new Vector3(roofWidth,.28f,roofDepth),ceramic,sculpted:true);
            var soffit=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("3C5559"));
            var roofLiner=DistrictFinishes.Tile("pearlCeramic",new Vector2(3,2),ArtDirection.Hex("B8CACB"));
            GardenStructures.Plate(root,"顶层暗银檐底",crown-Vector3.up*.18f,new Vector3(roofWidth-.15f,.15f,roofDepth-.18f),soffit,sculpted:true);
            GardenStructures.Plate(root,"屋面内嵌灰青衬板",crown+Vector3.up*.145f,new Vector3((roofWidth)*.88f,.018f,(roofDepth)*.88f),roofLiner,sculpted:true);
            GardenStructures.WingInlay(root,"屋顶青色嵌条",crown+Vector3.up*.161f,roofWidth,roofDepth,Mats.Lit(ArtDirection.Hex("68C8C7"),.32f));
            GardenStructures.VillaSolarBlades(garden,crown+Vector3.up*.16f,topW);
            var fork=GardenStructures.FrontEdge(second,terraceWidth*.91f,terraceDepth*.92f,-.30f)+new Vector3(0,.19f,.24f);
            Column(fork,crown,roofWidth,roofDepth,-.25f);
            // Match the privacy fins to the terrace's satin edge in front of the glass.
            float screenX=variant==2?bw*.34f:-bw*.34f;
            for(int i=0;i<5;i++)Prims.Box(root,"PrivacyFin",new Vector3(screenX+(i-2)*.18f,1.7f,front-.19f),new Vector3(.09f,2.9f,.30f),satinAlloy);
            Box(root,"StoneFeatureWall",new Vector3(-screenX,floor+.25f,front+.15f),new Vector3(bw*.13f,floor*2,.45f),ArtDirection.Hex("B7B5A5"));
            for(int i=0;i<9;i++)Box(root,"StoneWallJoint",new Vector3(-screenX,.6f+i*.59f,front-.085f),new Vector3(bw*.13f,.035f,.025f),ArtDirection.Hex("8D968E"));
            Box(root,"TerraceCanopy",new Vector3(0,2.98f,front-.8f),new Vector3(bw+.5f,.17f,1.9f),dark);
            Prims.Box(root,"CanopyLight",new Vector3(0,2.88f,front-1.45f),new Vector3(bw*.8f,.035f,.035f),Mats.Glow(white,new Color(1,.77f,.44f)));
            // Every property gets a conspicuous private swimming pool, including the smallest lots.
            float poolW=w*.43f,poolD=d*Mathf.Lerp(.22f,.245f,compact),poolX=-w*.15f,poolZ=d*.275f;
            // An open coping rim keeps the pale stone out of the water depth buffer at aerial distances.
            const float coping = .28f;
            foreach (int edge in new[] { -1, 1 })
            {
                Box(root,"PoolCoping",new Vector3(poolX+edge*(poolW+coping)*.5f,.32f,poolZ),
                    new Vector3(coping,.27f,poolD),white);
                Box(root,"PoolCoping",new Vector3(poolX,.32f,poolZ+edge*(poolD+coping)*.5f),
                    new Vector3(poolW+2*coping,.27f,coping),white);
            }
            Prims.Box(root,"PoolDeepWater",new Vector3(poolX,.405f,poolZ),new Vector3(poolW,.025f,poolD),IndustrialMaterials.GardenWater(IndustrialMaterials.WaterBasin.Pool));

            // Moving caustics are resolved on the pool surface in both street and aerial views.
            foreach(int side in new[]{-1,1})
            {
                float loungeX=w*.25f+side*w*.055f;
                float chairW=Mathf.Min(.85f,w*.055f),chairD=Mathf.Min(2.1f,poolD*.68f);
                Box(root,"PoolLounger",new Vector3(loungeX,.55f,poolZ),new Vector3(chairW,.18f,chairD),white);
                var back=Box(root,"LoungerBack",new Vector3(loungeX,.82f,poolZ+chairD*.34f),new Vector3(chairW,.12f,chairD*.40f),white);back.transform.localRotation=Quaternion.Euler(-28,0,0);
                Box(root,"LoungerFrame",new Vector3(loungeX,.39f,poolZ),new Vector3(chairW+.08f,.12f,chairD+.1f),wood);
            }
            // A slatted pergola and outdoor lounge sit alongside the living room.
            float pergolaX=w*.25f,pergolaZ=d*.46f,pergolaW=w*.25f,pergolaD=d*.16f;
            foreach(int x in new[]{-1,1})foreach(int depth in new[]{-1,1})
                Box(root,"PergolaPost",new Vector3(pergolaX+x*pergolaW/2,1.6f,pergolaZ+depth*pergolaD/2),new Vector3(.12f,2.8f,.12f),dark);
            for(int i=0;i<8;i++)Box(root,"PergolaSlat",new Vector3(pergolaX-pergolaW/2+i*pergolaW/7,3.05f,pergolaZ),new Vector3(.13f,.16f,pergolaD+.4f),wood);
            Box(root,"OutdoorSofa",new Vector3(pergolaX,.65f,pergolaZ+pergolaD*.23f),new Vector3(pergolaW*.8f,.65f,.65f),white);
            Box(root,"OutdoorTable",new Vector3(pergolaX,.5f,pergolaZ-.3f),new Vector3(pergolaW*.45f,.17f,.65f),dark);
            for(int fin=0;fin<7;fin++)
                garden.Box(new Vector3(topX+topW*(.26f+fin*.027f),floor*2+1.79f,topZ-topD/2-.25f),new Vector3(.09f,2.35f,.32f),satinAlloy,
                    Quaternion.Euler(0,22,0));
            QuarterModules.Add(root,"PlantPot",new Vector3(pergolaX-pergolaW*.38f,.14f,pergolaZ-pergolaD*.32f),Vector3.one*1.3f);
            foreach(int edge in new[]{-1,1})
            {
                for(float x=w*.15f;x<w*.46f;x+=.91f)
                    borders.Add(PlantSpecies.Hydrangea,new Vector3(edge*x,.45f,.85f),1.36f,.97f,seed%59+Mathf.RoundToInt(x));
                for(float along=d*.21f;along<d*.91f;along+=1.25f)
                {
                    int clump=Mathf.RoundToInt(along)+seed%71;
                    borders.Add(clump%4==0?PlantSpecies.Boxwood:PlantSpecies.Hydrangea,
                        new Vector3(edge*(w/2-.75f),.48f,along),1.60f,1.03f+clump%3*.14f,clump);
                }
            }
            borders.Build(root,"别墅庭院绣球与黄杨");
            garden.Build(root,"Layered villa gardens and terrace planters");
            // Boundary, entrance and planting stay inside the original plot and leave its door aisle open.
            foreach(int side in new[]{-1,1})
            {
                Box(root,"EstateBoundary",new Vector3(side*(w/2-.15f),.44f,d/2),new Vector3(.25f,.88f,d),white);

                Box(root,"EntranceWall",new Vector3(side*(w*.27f+.5f),.44f,.22f),new Vector3(w*.46f-1,.88f,.3f),white);
                Box(root,"EntryPier",new Vector3(side*1.05f,.9f,.22f),new Vector3(.35f,1.8f,.4f),dark);
                Prims.Box(root,"EntryLight",new Vector3(side*1.05f,1.4f,-.005f),new Vector3(.12f,.45f,.035f),Mats.Glow(white,new Color(1,.75f,.4f)));
                PaintedWoodland.Plant(root,new Vector3(side*w*.40f,.10f,d*.81f),side<0?7:6,side<0?3:0,seed+side,district:3);
                float treeX=side<0?-w*.45f:w*.40f;
                float treeZ=d*(side<0?.34f:.14f);
                PaintedWoodland.Plant(root,new Vector3(treeX,.10f,treeZ),4,side<0?0:3,seed+side+11,district:3);
            }
            for(int i=0;i<4;i++)Box(root,"EntrySteppingStone",new Vector3(0,.15f,.65f+i*d*.025f),new Vector3(1.6f,.12f,d*.019f),paving);
            Box(root,"EntryPathTurn",new Vector3(w*.07f,.15f,d*.12f),new Vector3(w*.14f+1,.12f,.9f),paving);
            Box(root,"GardenWalkToLivingRoom",new Vector3(w*.14f,.15f,d*.33f),new Vector3(.9f,.12f,d*.42f),paving);

        }

        private static void ModernGlazing(Transform parent,MiniatureDetailMesh furniture,Vector3 at,float width,float height,float yaw,Color frame,bool sheltered=false)
        {
            var root=new GameObject("FloorToCeilingGlazing").transform;root.SetParent(parent,false);root.localPosition=at;root.localRotation=Quaternion.Euler(0,yaw,0);
            const float reveal=.73f;
            // A return, a furnished back wall and a clear outer pane make the room readable from both diagonals.
            int bays=Mathf.Max(1,Mathf.RoundToInt(width/5.6f));
            float bayWidth=width/bays;
            int arrangement=Mathf.Abs(Mathf.RoundToInt(parent.position.x*.13f+parent.position.z*.07f+at.y+yaw/90));
            var timber=DistrictFinishes.Tile("walnut",Vector2.one);
            var sillFinish=DistrictFinishes.Tile("pearlCeramic",new Vector2(width/6.4f,.22f),ArtDirection.Hex("C0CED0"));
            var windowReturn=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("8B9D9F"));
            var upholstery=Mats.Lit(ArtDirection.Hex("C6B194"));
            var brass=Mats.Lit(ArtDirection.Hex("997953"));
            var linen=Mats.Glow(ArtDirection.Hex("F2DBB2"),new Color(2.2f,1.61f,.82f));
            float sill=-height*.5f;
            var orientation=root.localRotation;
            Vector3 Within(float x,float y,float depth)=>at+orientation*new Vector3(x,y,depth);
            for(int i=0;i<bays;i++)
            {
                float x=(i+.5f)*bayWidth-width/2;
                int style=(arrangement+i)%3;
                var back=Prims.Shape(PrimitiveType.Quad,root,"起居室内景",new Vector3(x,0,reveal),
                    new Vector3(bayWidth,height,1),DistrictFinishes.VillaRoom(style));
                back.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                if(style==0)
                {
                    float seat=x-bayWidth*.10f;
                    furniture.Box(Within(seat,sill+.31f,.43f),new Vector3(1.27f,.34f,.44f),upholstery,orientation);
                    furniture.Box(Within(seat,sill+.57f,.60f),new Vector3(1.27f,.35f,.14f),upholstery,orientation);
                    float lamp=seat+1.03f;
                    furniture.Box(Within(lamp,sill+.55f,.45f),new Vector3(.055f,1.1f,.055f),brass,orientation);
                    furniture.Box(Within(lamp,sill+1.17f,.45f),new Vector3(.38f,.30f,.25f),linen,orientation);
                }
                else if(style==1)
                {
                    float desk=x-bayWidth*.12f;
                    furniture.Box(Within(desk,sill+.78f,.44f),new Vector3(1.48f,.085f,.52f),timber,orientation);
                    foreach(int side in new[]{-1,1})
                        furniture.Box(Within(desk+side*.61f,sill+.38f,.47f),new Vector3(.09f,.76f,.37f),timber,orientation);
                    furniture.Box(Within(desk+.33f,sill+.86f,.45f),new Vector3(.29f,.07f,.19f),upholstery,orientation);
                    for(int shelf=0;shelf<3;shelf++)
                        furniture.Box(Within(x+bayWidth*.26f,sill+.63f+shelf*.42f,.54f),
                            new Vector3(.73f,.055f,.28f),timber,orientation);
                }
                else
                {
                    // A half-drawn fabric curtain and a low cabinet change the silhouette in the quiet rooms.
                    float curtain=Mathf.Min(bayWidth*.24f,1.15f);
                    for(int fold=0;fold<5;fold++)
                        furniture.Box(Within(x+bayWidth*.45f-fold*curtain*.20f,0,.29f+(fold%2)*.045f),
                            new Vector3(curtain*.22f,height*.89f,.06f),upholstery,orientation);
                    furniture.Box(Within(x-.48f,sill+.30f,.55f),new Vector3(1.09f,.55f,.33f),timber,orientation);
                }
            }
            foreach(int sign in new[]{-1,1})
            {
                Prims.Box(root,"窗洞侧壁",new Vector3(sign*(width+.06f)*.5f,0,reveal*.5f),
                    new Vector3(.09f,height+.11f,reveal+.12f),windowReturn);
                Prims.Box(root,sign<0?"幕墙内侧瓷板地面":"窗洞铝合金吊顶",new Vector3(0,sign*(height+.06f)*.5f,reveal*.5f),
                    new Vector3(width+.16f,.11f,reveal+.14f),sign<0?sillFinish:windowReturn);
            }
            if(sheltered)
                Prims.Box(root,"露台下吊顶阴缝",new Vector3(0,height*.45f,-.13f),new Vector3(width+.14f,.13f,.38f),
                    DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("3F5356")));
            var pane=Prims.Shape(PrimitiveType.Quad,root,"外层幕墙玻璃",Vector3.zero,new Vector3(width,height,1),DistrictFinishes.VillaPane(sheltered:sheltered));
            pane.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            int panes=Mathf.Max(2,Mathf.RoundToInt(width/1.9f));
            for(int i=0;i<=panes;i++)Box(root,"SlenderMullion",new Vector3(-width/2+i*width/panes,0,-.025f),new Vector3(.055f,height,.075f),frame);
        }
    }
}
