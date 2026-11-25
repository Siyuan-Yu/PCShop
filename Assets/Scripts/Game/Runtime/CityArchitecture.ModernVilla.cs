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
            float floor=2.7f,h=floor*3,bw=w*.83f,bd=d*.42f,z=d*.70f,front=z-bd/2;
            Prims.Box(root,"PrivateEstateLawn",new Vector3(0,.045f,d/2),new Vector3(w,.09f,d),
                IndustrialMaterials.Surface("meadow",Lawn,new Vector2(w/25,d/25)));
            Prims.Box(root,"StoneTerrace",new Vector3(0,.14f,d*.37f),new Vector3(w*.85f,.18f,d*.36f),
                IndustrialMaterials.Surface("paving",paving,new Vector2(w/5,d/10)));
            Box(root,"GroundFloor",new Vector3(0,floor/2+.2f,z),new Vector3(bw,floor,bd),white,true);
            Box(root,"GroundFloorPlinth",new Vector3(0,.26f,z),new Vector3(bw+.3f,.3f,bd+.25f),dark);
            // A smaller, shifted upper storey leaves a real terrace and an overhanging wing.
            float upperW=bw*(variant==1?.77f:.84f),upperD=bd*.88f,upperX=(variant==2?1:-1)*bw*.13f;
            var ceramic = DistrictFinishes.Tile("pearlCeramic",new Vector2(2,2),white);
            var alloy = DistrictFinishes.Tile("brushedAlloy",Vector2.one);
            var graphite = DistrictFinishes.Tile("graniteWalk",new Vector2(2,2),ArtDirection.Hex("A1ABAC"));
            var garden = new MiniatureDetailMesh();
            var borders = new StreetPlanting(maintained:true);
            Box(root,"UpperStorey",new Vector3(upperX,floor*1.5f+.5f,z),new Vector3(upperW,floor,upperD),white,true);
            float firstY=floor+.38f, secondY=floor*2+.59f;
            float terraceDepth=bd+2.05f, terraceWidth=bw+1.42f;
            Vector3 first=new Vector3(0,firstY,z-.60f);
            Vector3 second=new Vector3(upperX*.55f,secondY,z-.53f);
            void Terrace(Vector3 at,float width,float depth,int level)
            {
                GardenStructures.Plate(root,"悬挑板银灰阴缝",at-Vector3.up*.14f,new Vector3(width-.24f,.18f,depth-.22f),alloy,sculpted:true);
                GardenStructures.Plate(root,"弧形白瓷露台",at,new Vector3(width,.28f,depth),ceramic,sculpted:true);
                GardenStructures.Plate(root,"露台面",at+Vector3.up*.16f,new Vector3(width-.24f,.045f,depth-.24f),graphite,sculpted:true);
                float edgeZ=at.z-depth*.5f+.33f;
                for(int k=-3;k<=3;k++)
                {
                    float x=at.x+k*width*.09f;
                    garden.Box(new Vector3(x,at.y+.48f,edgeZ),new Vector3(.045f,.67f,.05f),alloy);
                    if(k%2==0)
                        borders.Add(level==1?PlantSpecies.Hydrangea:PlantSpecies.Boxwood,new Vector3(x,at.y+.17f,edgeZ+.25f),width*.23f,.62f,seed+k+level);
                }
                garden.Box(new Vector3(at.x,at.y+.82f,edgeZ),new Vector3(width*.56f,.055f,.07f),alloy);
            }
            Terrace(first,terraceWidth,terraceDepth,1);
            Terrace(second,terraceWidth*.91f,terraceDepth*.92f,2);
            // Branching load-bearing frames sit under the leading edge of each cantilever.
            foreach(int side in new[]{-1,1})
            {
                Vector3 a=new Vector3(side*bw*.40f,.26f,front-.52f);
                Vector3 branch=new Vector3(side*bw*.38f,firstY*.55f,front-.75f);
                GardenStructures.Strut(garden,a,branch,.22f,.24f,ceramic);
                GardenStructures.Strut(garden,branch,first+new Vector3(side*terraceWidth*.43f,-.10f,-terraceDepth*.31f),.20f,.24f,ceramic);
                GardenStructures.Strut(garden,branch,first+new Vector3(side*terraceWidth*.22f,-.10f,-terraceDepth*.47f),.17f,.20f,ceramic);
            }
            ModernGlazing(root,new Vector3(0,1.65f,front-.065f),bw*.84f,2.15f,0,dark);
            ModernGlazing(root,new Vector3(upperX,floor+1.72f,z-upperD/2-.065f),upperW*.84f,2.0f,0,dark);
            foreach(int side in new[]{-1,1})
            {
                ModernGlazing(root,new Vector3(side*(bw/2+.065f),1.65f,z),bd*.72f,2.15f,side<0?90:270,dark);
                ModernGlazing(root,new Vector3(upperX+side*(upperW/2+.065f),floor+1.72f,z),upperD*.70f,2,side<0?90:270,dark);
            }
            ModernGlazing(root,new Vector3(upperX,floor+1.72f,z+upperD/2+.065f),upperW*.70f,2,180,dark);
            float topW=upperW*.94f, topD=upperD*.91f;
            float topX=upperX+Mathf.Sign(upperX)*upperW*.025f, topZ=z+bd*.04f;
            float glassY=floor*2+1.72f;
            Box(root,"SetbackStorey",new Vector3(topX,floor*2.5f+.5f,topZ),new Vector3(topW,floor,topD),white,true);
            ModernGlazing(root,new Vector3(topX,glassY,topZ-topD/2-.065f),topW*.94f,2.12f,0,dark);
            ModernGlazing(root,new Vector3(topX,glassY,topZ+topD/2+.065f),topW*.86f,2.12f,180,dark);
            foreach(int side in new[]{-1,1})
                ModernGlazing(root,new Vector3(topX+side*(topW/2+.065f),glassY,topZ),topD*.88f,2.12f,side<0?90:270,dark);
            var crown=new Vector3(topX,h+.63f,topZ-.34f);
            GardenStructures.Plate(root,"顶层悬挑翼",crown,new Vector3(topW+2.35f,.28f,topD+1.95f),ceramic,sculpted:true);
            GardenStructures.Plate(root,"顶层银边",crown-Vector3.up*.17f,new Vector3(topW+2.08f,.09f,topD+1.70f),alloy,sculpted:true);
            GardenStructures.SolarFins(garden,crown+Vector3.up*.16f,topW*.85f,topD*1.06f,3);
            var fork=new Vector3(topX-topW*.42f,secondY+.18f,topZ-topD*.5f-.48f);
            var joint=fork+Vector3.up*.85f;
            GardenStructures.Strut(garden,fork,joint,.15f,.18f,ceramic);
            GardenStructures.Strut(garden,joint,crown+new Vector3(-topW*.49f,-.14f,-topD*.55f),.13f,.16f,ceramic);
            GardenStructures.Strut(garden,joint,crown+new Vector3(-topW*.21f,-.14f,-topD*.68f),.13f,.16f,ceramic);
            // Full-height timber fins provide warm material contrast and a distinctive silhouette.
            float screenX=variant==2?bw*.34f:-bw*.34f;
            for(int i=0;i<5;i++)Box(root,"TimberPrivacyFin",new Vector3(screenX+(i-2)*.18f,1.7f,front-.19f),new Vector3(.09f,2.9f,.30f),wood);
            Box(root,"StoneFeatureWall",new Vector3(-screenX,floor+.25f,front+.15f),new Vector3(bw*.13f,floor*2,.45f),ArtDirection.Hex("B7B5A5"));
            for(int i=0;i<9;i++)Box(root,"StoneWallJoint",new Vector3(-screenX,.6f+i*.59f,front-.085f),new Vector3(bw*.13f,.035f,.025f),ArtDirection.Hex("8D968E"));
            Box(root,"TerraceCanopy",new Vector3(0,2.98f,front-.8f),new Vector3(bw+.5f,.17f,1.9f),dark);
            Prims.Box(root,"CanopyLight",new Vector3(0,2.88f,front-1.45f),new Vector3(bw*.8f,.035f,.035f),Mats.Glow(white,new Color(1,.77f,.44f)));
            // Every property gets a conspicuous private swimming pool, including the smallest lots.
            float poolW=w*.43f,poolD=d*.22f,poolX=-w*.15f,poolZ=d*.28f;
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
            garden.FlowerBed(new Vector3(upperX,floor*2+.76f,z-upperD/2-.10f),upperW*.74f,.70f,seed+6);
            borders.Build(root,"别墅庭院绣球与黄杨");
            garden.Build(root,"Layered villa gardens and terrace planters");
            // Boundary, entrance and planting stay inside the original plot and leave its door aisle open.
            foreach(int side in new[]{-1,1})
            {
                Box(root,"EstateBoundary",new Vector3(side*(w/2-.15f),.44f,d/2),new Vector3(.25f,.88f,d),white);

                Box(root,"EntranceWall",new Vector3(side*(w*.27f+.5f),.44f,.22f),new Vector3(w*.46f-1,.88f,.3f),white);
                Box(root,"EntryPier",new Vector3(side*1.05f,.9f,.22f),new Vector3(.35f,1.8f,.4f),dark);
                Prims.Box(root,"EntryLight",new Vector3(side*1.05f,1.4f,-.005f),new Vector3(.12f,.45f,.035f),Mats.Glow(white,new Color(1,.75f,.4f)));
                PaintedWoodland.Plant(root,new Vector3(side*w*.40f,.10f,d*.81f),side<0?7:6,side<0?3:0,seed+side);
                PaintedWoodland.Plant(root,new Vector3(side*w*.40f,.10f,d*.14f),side<0?6:4,side<0?0:3,seed+side+11);
            }
            for(int i=0;i<4;i++)Box(root,"EntrySteppingStone",new Vector3(0,.15f,.65f+i*d*.025f),new Vector3(1.6f,.12f,d*.019f),paving);
            Box(root,"EntryPathTurn",new Vector3(w*.07f,.15f,d*.12f),new Vector3(w*.14f+1,.12f,.9f),paving);
            Box(root,"GardenWalkToLivingRoom",new Vector3(w*.14f,.15f,d*.33f),new Vector3(.9f,.12f,d*.42f),paving);

        }

        private static void ModernGlazing(Transform parent,Vector3 at,float width,float height,float yaw,Color frame)
        {
            var root=new GameObject("FloorToCeilingGlazing").transform;root.SetParent(parent,false);root.localPosition=at;root.localRotation=Quaternion.Euler(0,yaw,0);
            Box(root,"RecessFrame",Vector3.zero,new Vector3(width+.18f,height+.18f,.15f),frame);
            Prims.Box(root,"幕墙窗带",new Vector3(0,0,-.09f),new Vector3(width,height,.04f),
                DistrictFinishes.Tile("civicGlazing",new Vector2(2,1)));
            // Only part of each glazed ribbon looks through into a furnished bay.
            Prims.Box(root,"起居室通透窗",new Vector3(width*.19f,0,-.119f),new Vector3(width*.33f,height*.98f,.014f),
                DistrictFinishes.WindowGlass(true));
            int panes=Mathf.Max(2,Mathf.RoundToInt(width/1.7f));
            for(int i=1;i<panes;i++)Box(root,"SlenderMullion",new Vector3(-width/2+i*width/panes,0,-.13f),new Vector3(.065f,height,.06f),frame);
        }
    }
}
