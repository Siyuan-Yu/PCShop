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
            float floor=2.7f,h=floor*3,bw=w*Mathf.Lerp(.83f,.89f,compact),bd=d*Mathf.Lerp(.42f,.54f,compact);
            float z=d*Mathf.Lerp(.70f,.685f,compact),front=z-bd/2;
            Prims.Box(root,"PrivateEstateLawn",new Vector3(0,.045f,d/2),new Vector3(w,.09f,d),
                IndustrialMaterials.Surface("meadow",Lawn,new Vector2(w/25,d/25)));
            Prims.Box(root,"StoneTerrace",new Vector3(0,.14f,d*.37f),new Vector3(w*.85f,.18f,d*.36f),
                IndustrialMaterials.Surface("paving",paving,new Vector2(w/5,d/10)));
            void Storey(string name,Vector3 at,Vector3 size)
            {
                // Recess the visible envelope without shrinking the building's collision footprint.
                var recessed=size-new Vector3(1.76f,0,1.76f);
                var shell=Box(root,name,at,recessed,white,true);
                shell.GetComponent<BoxCollider>().size=new Vector3(size.x/recessed.x,1,size.z/recessed.z);
            }
            Storey("GroundFloor",new Vector3(0,floor/2+.2f,z),new Vector3(bw,floor,bd));
            Box(root,"GroundFloorPlinth",new Vector3(0,.26f,z),new Vector3(bw+.3f,.3f,bd+.25f),dark);
            // A smaller, shifted upper storey leaves a real terrace and an overhanging wing.
            float upperW=bw*Mathf.Lerp(variant==1?.77f:.84f,.95f,compact),upperD=bd*Mathf.Lerp(.88f,.93f,compact);
            float upperX=(variant==2?1:-1)*bw*Mathf.Lerp(.13f,.022f,compact);
            float upperZ=z-.64f;
            var ceramic = DistrictFinishes.Tile("pearlCeramic",new Vector2(2,2),white);
            var alloy = DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("80969B"));
            var terraceStone = DistrictFinishes.Tile("pearlCeramic",new Vector2(4,3),ArtDirection.Hex("879795"));
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
                GardenStructures.Plate(root,"悬挑板银灰阴缝",at-Vector3.up*.14f,new Vector3(width-.24f,.18f,depth-.22f),alloy,sculpted:true);
                GardenStructures.Plate(root,"弧形白瓷露台",at,new Vector3(width,.28f,depth),ceramic,sculpted:true);
                GardenStructures.Plate(root,"露台面",at+Vector3.up*.16f,new Vector3(width-.24f,.045f,depth-.24f),terraceStone,sculpted:true);
                GardenStructures.BalconyGlass(root,garden,at,width,depth,glassEdge);
                foreach(int side in new[]{-1,1})
                    for(int j=0;j<3;j++)
                    {
                        var planting=GardenStructures.FrontEdge(at,width,depth,side*(.43f-(j*.67f)/width));
                        borders.Add((j+level)%3==0?PlantSpecies.Boxwood:PlantSpecies.Hydrangea,
                            planting+new Vector3(-side*.22f,.17f,.39f),1.12f,.71f+(j%2)*.09f,seed%97+j+side+level);
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
            ModernGlazing(root,garden,new Vector3(upperX,floor+1.72f,upperZ-upperD/2-.065f),upperW*.94f,2.18f,0,dark,sheltered:true);
            foreach(int side in new[]{-1,1})
            {
                ModernGlazing(root,garden,new Vector3(side*(bw/2+.065f),1.65f,z),bd*.92f,2.28f,side<0?90:270,dark);
                ModernGlazing(root,garden,new Vector3(upperX+side*(upperW/2+.065f),floor+1.72f,upperZ),upperD*.90f,2.18f,side<0?90:270,dark,sheltered:true);
            }
            ModernGlazing(root,garden,new Vector3(upperX,floor+1.72f,upperZ+upperD/2+.065f),upperW*.93f,2.18f,180,dark);
            float topW=upperW*Mathf.Lerp(.94f,.965f,compact), topD=upperD*Mathf.Lerp(.91f,.95f,compact);
            float topX=upperX+Mathf.Sign(upperX)*upperW*.025f, topZ=z+bd*.04f-.82f;
            float glassY=floor*2+1.72f;
            Storey("SetbackStorey",new Vector3(topX,floor*2.5f+.5f,topZ),new Vector3(topW,floor,topD));
            ModernGlazing(root,garden,new Vector3(topX,glassY,topZ-topD/2-.065f),topW*.94f,2.12f,0,dark);
            ModernGlazing(root,garden,new Vector3(topX,glassY,topZ+topD/2+.065f),topW*.86f,2.12f,180,dark);
            foreach(int side in new[]{-1,1})
                ModernGlazing(root,garden,new Vector3(topX+side*(topW/2+.065f),glassY,topZ),topD*.88f,2.12f,side<0?90:270,dark);
            var crown=new Vector3(topX,h+.63f,topZ-.34f);
            GardenStructures.Plate(root,"顶层悬挑翼",crown,new Vector3(topW+2.35f,.28f,topD+1.95f),ceramic,sculpted:true);
            var soffit=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("3C5559"));
            var roofLiner=DistrictFinishes.Tile("pearlCeramic",new Vector2(3,2),ArtDirection.Hex("B8CACB"));
            GardenStructures.Plate(root,"顶层暗银檐底",crown-Vector3.up*.18f,new Vector3(topW+2.20f,.15f,topD+1.77f),soffit,sculpted:true);
            GardenStructures.Plate(root,"屋面内嵌灰青衬板",crown+Vector3.up*.145f,new Vector3((topW+2.35f)*.88f,.018f,(topD+1.95f)*.88f),roofLiner,sculpted:true);
            GardenStructures.RoofInlay(root,crown+Vector3.up*.161f,topW+2.35f,topD+1.95f,alloy);
            GardenStructures.VillaSolarBlades(garden,crown+Vector3.up*.16f,topW);
            var fork=GardenStructures.FrontEdge(second,terraceWidth*.91f,terraceDepth*.92f,-.30f)+new Vector3(0,.19f,.24f);
            Column(fork,crown,topW+2.35f,topD+1.95f,-.25f);
            // Full-height timber fins provide warm material contrast and a distinctive silhouette.
            float screenX=variant==2?bw*.34f:-bw*.34f;
            for(int i=0;i<5;i++)Box(root,"TimberPrivacyFin",new Vector3(screenX+(i-2)*.18f,1.7f,front-.19f),new Vector3(.09f,2.9f,.30f),wood);
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
            var bronze=DistrictFinishes.Tile("walnut",Vector2.one*.45f);
            for(int fin=0;fin<7;fin++)
                garden.Box(new Vector3(topX+topW*(.26f+fin*.027f),floor*2+1.79f,topZ-topD/2-.25f),new Vector3(.09f,2.35f,.32f),bronze,
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
                    new Vector3(.09f,height+.11f,reveal+.12f),timber);
                Prims.Box(root,"室内地台与吊顶",new Vector3(0,sign*(height+.06f)*.5f,reveal*.5f),
                    new Vector3(width+.16f,.11f,reveal+.14f),timber);
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
