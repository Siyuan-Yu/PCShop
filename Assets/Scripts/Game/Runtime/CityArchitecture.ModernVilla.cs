using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public static partial class CityArchitecture
    {
        private static void ModernGardenVilla(Transform root,float w,float d,float configuredHeight,int seed)
        {
            int variant=(seed&int.MaxValue)%3;
            Color white=ArtDirection.Hex(variant==0?"F1EFE4":variant==1?"E9E3D4":"DADFE0");
            Color dark=ArtDirection.Hex("293F4A"),wood=ArtDirection.Hex("A27D51"),paving=ArtDirection.Hex("D6D6C5");
            float floor=2.7f,h=floor*4,bw=w*.72f,bd=d*.37f,z=d*.73f,front=z-bd/2;
            Box(root,"PrivateEstateLawn",new Vector3(0,.045f,d/2),new Vector3(w,.09f,d),ArtDirection.Hex("7FA45B"));
            Box(root,"StoneTerrace",new Vector3(0,.14f,d*.37f),new Vector3(w*.85f,.18f,d*.36f),paving);
            Box(root,"GroundFloor",new Vector3(0,floor/2+.2f,z),new Vector3(bw,floor,bd),white,true);
            Box(root,"GroundFloorPlinth",new Vector3(0,.26f,z),new Vector3(bw+.3f,.3f,bd+.25f),dark);
            // A smaller, shifted upper storey leaves a real terrace and an overhanging wing.
            float upperW=bw*(variant==1?.61f:.73f),upperD=bd*.88f,upperX=(variant==2?1:-1)*bw*.13f;
            Box(root,"CantileverFloor",new Vector3(upperX,floor+.38f,z-.1f),new Vector3(upperW+.65f,.3f,upperD+.7f),white);
            Box(root,"UpperStorey",new Vector3(upperX,floor*1.5f+.5f,z),new Vector3(upperW,floor,upperD),white,true);
            Box(root,"SecondFloorRoofTerrace",new Vector3(upperX,floor*2+.59f,z),new Vector3(upperW+.65f,.24f,upperD+.7f),white);
            Box(root,"SecondFloorShadowGap",new Vector3(upperX,floor*2+.44f,z),new Vector3(upperW+.25f,.1f,upperD+.25f),dark);
            ModernGlazing(root,new Vector3(0,1.65f,front-.065f),bw*.84f,2.15f,0,dark);
            ModernGlazing(root,new Vector3(upperX,floor+1.72f,z-upperD/2-.065f),upperW*.84f,2.0f,0,dark);
            foreach(int side in new[]{-1,1})
            {
                ModernGlazing(root,new Vector3(side*(bw/2+.065f),1.65f,z),bd*.72f,2.15f,side<0?90:270,dark);
                ModernGlazing(root,new Vector3(upperX+side*(upperW/2+.065f),floor+1.72f,z),upperD*.70f,2,side<0?90:270,dark);
            }
            ModernGlazing(root,new Vector3(upperX,floor+1.72f,z+upperD/2+.065f),upperW*.70f,2,180,dark);
            // Two progressively recessed levels preserve the villa's terraced silhouette.
            float topW=upperW,topD=upperD,topX=upperX,topZ=z;
            for(int level=2;level<4;level++)
            {
                topW=upperW*(level==2?.90f:.73f);
                topD=upperD*(level==2?.86f:.72f);
                topX=upperX+Mathf.Sign(upperX)*upperW*(level==2?.025f:.07f);
                topZ=z+bd*(level==2?.04f:.08f);
                float baseY=level*floor+.5f,glassY=level*floor+1.72f;
                Box(root,"SetbackStorey"+(level+1),new Vector3(topX,baseY+floor/2,topZ),new Vector3(topW,floor,topD),white,true);
                ModernGlazing(root,new Vector3(topX,glassY,topZ-topD/2-.065f),topW*.84f,2,0,dark);
                ModernGlazing(root,new Vector3(topX,glassY,topZ+topD/2+.065f),topW*.75f,2,180,dark);
                foreach(int side in new[]{-1,1})
                    ModernGlazing(root,new Vector3(topX+side*(topW/2+.065f),glassY,topZ),topD*.72f,2,side<0?90:270,dark);
                Box(root,"FloatingFloorPlate"+(level+1),new Vector3(topX,(level+1)*floor+.59f,topZ),new Vector3(topW+.65f,.24f,topD+.7f),white);
                Box(root,"FloorShadowGap"+(level+1),new Vector3(topX,(level+1)*floor+.44f,topZ),new Vector3(topW+.25f,.1f,topD+.25f),dark);
                float terraceFront=z-upperD/2-.2f;
                Box(root,"SetbackGlassBalustrade"+(level+1),new Vector3(topX,baseY+.55f,terraceFront),new Vector3(topW,1,.055f),ArtDirection.Hex("86B8C4"));
                Box(root,"SetbackBalustradeCap"+(level+1),new Vector3(topX,baseY+1.08f,terraceFront),new Vector3(topW,.055f,.08f),dark);
                // The fourth-floor terrace rests on the third-floor roof rather than floating.
                if(level==3)
                    Box(root,"PenthouseTerraceDeck",new Vector3(topX,baseY+.08f,(terraceFront+topZ-topD/2)/2),new Vector3(topW,.14f,topZ-topD/2-terraceFront+.18f),wood);
            }
            // Full-height timber fins provide warm material contrast and a distinctive silhouette.
            float screenX=variant==2?bw*.34f:-bw*.34f;
            for(int i=0;i<5;i++)Box(root,"TimberPrivacyFin",new Vector3(screenX+(i-2)*.18f,1.7f,front-.19f),new Vector3(.09f,2.9f,.30f),wood);
            Box(root,"StoneFeatureWall",new Vector3(-screenX,floor+.25f,front+.15f),new Vector3(bw*.13f,floor*2,.45f),ArtDirection.Hex("B7B5A5"));
            for(int i=0;i<9;i++)Box(root,"StoneWallJoint",new Vector3(-screenX,.6f+i*.59f,front-.085f),new Vector3(bw*.13f,.035f,.025f),ArtDirection.Hex("8D968E"));
            Box(root,"TerraceCanopy",new Vector3(0,2.98f,front-.8f),new Vector3(bw+.5f,.17f,1.9f),dark);
            Prims.Box(root,"CanopyLight",new Vector3(0,2.88f,front-1.45f),new Vector3(bw*.8f,.035f,.035f),Mats.Glow(white,new Color(1,.77f,.44f)));
            // Open roof terrace, bounded by slim glass balustrades.
            float terraceX=-Mathf.Sign(upperX)*bw*.38f,terraceW=bw*.20f;
            Box(root,"UpperTerraceDeck",new Vector3(terraceX,floor+.42f,z),new Vector3(terraceW,.12f,bd*.8f),wood);
            Box(root,"GlassBalustrade",new Vector3(terraceX,floor+.97f,front-.09f),new Vector3(terraceW,1,.055f),ArtDirection.Hex("86B8C4"));
            Box(root,"BalustradeCap",new Vector3(terraceX,floor+1.5f,front-.09f),new Vector3(terraceW,.055f,.08f),dark);
            // Every property gets a conspicuous private swimming pool, including the smallest lots.
            float poolW=w*.43f,poolD=d*.22f,poolX=-w*.15f,poolZ=d*.28f;
            Box(root,"PoolCoping",new Vector3(poolX,.29f,poolZ),new Vector3(poolW+.5f,.22f,poolD+.5f),white);
            Box(root,"PoolDeepWater",new Vector3(poolX,.405f,poolZ),new Vector3(poolW,.025f,poolD),ArtDirection.Hex("258EA8"));
            Box(root,"PoolShallowShelf",new Vector3(poolX-poolW*.37f,.423f,poolZ),new Vector3(poolW*.23f,.015f,poolD*.95f),ArtDirection.Hex("63C9D2"));
            var water=Mats.Lit(ArtDirection.Hex("70C9CF"),.7f,.12f);
            for(int i=0;i<7;i++)
                Prims.Box(root,"WaterGlint",new Vector3(poolX+Mathf.Sin(i*2.1f)*poolW*.27f,.43f,poolZ+(i-3)*poolD*.12f),new Vector3(poolW*(i%2==0?.15f:.09f),.008f,.035f),water);
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
            // Boundary, entrance and planting stay inside the original plot and leave its door aisle open.
            foreach(int side in new[]{-1,1})
            {
                Box(root,"EstateBoundary",new Vector3(side*(w/2-.15f),.65f,d/2),new Vector3(.25f,1.3f,d),white);
                Box(root,"BoundaryPlanting",new Vector3(side*(w/2-.6f),.62f,d*.65f),new Vector3(.55f,1.15f,d*.65f),Hedge);
                Box(root,"EntranceWall",new Vector3(side*(w*.27f+.5f),.65f,.22f),new Vector3(w*.46f-1,1.3f,.3f),white);
                Box(root,"EntryPier",new Vector3(side*1.05f,.9f,.22f),new Vector3(.35f,1.8f,.4f),dark);
                Prims.Box(root,"EntryLight",new Vector3(side*1.05f,1.4f,-.005f),new Vector3(.12f,.45f,.035f),Mats.Glow(white,new Color(1,.75f,.4f)));
                CityVegetation.Build(root,new Vector3(side*w*.40f,0,d*.82f),seed+side);
                CityVegetation.Build(root,new Vector3(side*w*.40f,0,d*.12f),seed+side+11);
            }
            for(int i=0;i<4;i++)Box(root,"EntrySteppingStone",new Vector3(0,.15f,.65f+i*d*.025f),new Vector3(1.6f,.12f,d*.019f),paving);
            Box(root,"EntryPathTurn",new Vector3(w*.07f,.15f,d*.12f),new Vector3(w*.14f+1,.12f,.9f),paving);
            Box(root,"GardenWalkToLivingRoom",new Vector3(w*.14f,.15f,d*.33f),new Vector3(.9f,.12f,d*.42f),paving);
            // Low-profile solar panels reinforce the contemporary silhouette from the aerial view.
            for(int i=0;i<3;i++)
                Box(root,"SolarRoofPanel",new Vector3(topX+(i-1)*topW*.21f,h+.75f,topZ+topD*.13f),new Vector3(topW*.18f,.10f,topD*.40f),ArtDirection.Hex("274D67"));
        }

        private static void ModernGlazing(Transform parent,Vector3 at,float width,float height,float yaw,Color frame)
        {
            var root=new GameObject("FloorToCeilingGlazing").transform;root.SetParent(parent,false);root.localPosition=at;root.localRotation=Quaternion.Euler(0,yaw,0);
            Box(root,"RecessFrame",Vector3.zero,new Vector3(width+.18f,height+.18f,.15f),frame);
            Prims.Box(root,"ReflectiveGlass",new Vector3(0,0,-.09f),new Vector3(width,height,.04f),Mats.Glow(ArtDirection.Hex("477E91"),new Color(.55f,.43f,.26f)));
            Box(root,"SkyReflection",new Vector3(0,height*.28f,-.115f),new Vector3(width,height*.22f,.012f),ArtDirection.Hex("6DABB9"));
            int panes=Mathf.Max(2,Mathf.RoundToInt(width/1.7f));
            for(int i=1;i<panes;i++)Box(root,"SlenderMullion",new Vector3(-width/2+i*width/panes,0,-.13f),new Vector3(.065f,height,.06f),frame);
        }
    }
}
