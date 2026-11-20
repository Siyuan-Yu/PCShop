using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Open-front social spaces share a legible entrance and real furniture, without loading parallel scenes.</summary>
    public static class CityVenueView
    {
        public static void Build(Transform root, Place p, float width, float depth)
        {
            string kind = (p.Business ?? "").Replace("venue_", "");
            Color floor = kind == "court" || kind == "golf" || kind == "park" || kind == "hike" ? ArtDirection.Sage : ArtDirection.Cream;
            bool lawn=kind=="golf"||kind=="park"||kind=="hike";
            var ground=IndustrialMaterials.Surface(lawn?"meadow":"paving",lawn?ArtDirection.Hex("7D9859"):floor,new Vector2(width/14,depth/14));
            var body = Prims.Box(root, "Body", new Vector3(0, .045f, depth / 2 - 1), new Vector3(width, .09f, depth + 6), ground, true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;
            bool outside=kind=="court"||kind=="golf"||kind=="park"||kind=="hike";
            if(outside)
            {
                for(float x=-width/2;x<=width/2;x+=2)
                    Prims.Box(root,"OutdoorFencePost",new Vector3(x,.85f,depth),new Vector3(.1f,1.7f,.1f),Mats.Lit(ArtDirection.Ink));
                foreach(float y in new[]{.55f,1.35f})Prims.Box(root,"OutdoorFenceRail",new Vector3(0,y,depth),new Vector3(width,.07f,.07f),Mats.Lit(ArtDirection.Sage));
            }
            else Prims.Box(root, "BackBoundary", new Vector3(0, 1.5f, depth), new Vector3(width, 3, .2f), Mats.Lit(ArtDirection.Ink));
            Prims.Box(root, "EntranceFascia", new Vector3(0, 3.5f, -.1f), new Vector3(width - .4f, .9f, .24f), Mats.Lit(ArtDirection.Sage));
            Prims.SignText(root, p.Name, new Vector3(0, 3.5f, -.25f), width - .8f, .65f, ArtDirection.Cream);
            Prims.Box(root, "SignSupportLeft", new Vector3(-width / 2 + .3f, 1.8f, 0), new Vector3(.18f, 3.6f, .18f), Mats.Lit(ArtDirection.Brass));
            Prims.Box(root, "SignSupportRight", new Vector3(width / 2 - .3f, 1.8f, 0), new Vector3(.18f, 3.6f, .18f), Mats.Lit(ArtDirection.Brass));
            switch (kind)
            {
                case "服装": Clothing(root, width, depth); break;
                case "restaurant": Restaurant(root, width, depth); break;
                case "ktv": Karaoke(root, width, depth); break;
                case "bar": Bar(root, width, depth); break;
                case "court": Court(root, width, depth); break;
                case "golf": Golf(root, width, depth); break;
                case "expo": Expo(root, width, depth); break;
                case "oldmarket": OldMarket(root, width, depth); break;
                case "pachinko": case "poker": case "claw": case "billiards": case "cinema":
                    LeisureVenueEntrance.Build(root, p, width, depth);
                    break;
                case "hike": if (p.Id == GameSession.FishingVenueId) FishingPark(root, width, depth); else Park(root, width, depth, true); break;
                default: Park(root, width, depth, false, p.Id == GameSession.CommunityParkId); break;
            }
            if(kind=="restaurant"||kind=="ktv"||kind=="bar"||kind=="expo")
            {
                Prims.Box(root,"VenueClimateUnit",new Vector3(width*.3f,2.7f,depth-.2f),new Vector3(1.5f,.55f,.3f),Mats.Lit(ArtDirection.Cream));
                Prims.Box(root,"ClimateOutlet",new Vector3(width*.3f,2.53f,depth-.37f),new Vector3(1.25f,.08f,.04f),Mats.Lit(ArtDirection.Ink));
            }
            // Arrival/meeting positions deliberately remain in the open forecourt, not behind a wall.
            Bench(root, new Vector3(-3.2f, 0, -1.4f), ArtDirection.Sage);
            Bench(root, new Vector3(3.2f, 0, -1.4f), ArtDirection.Clay);
            var planting=new MiniatureDetailMesh();
            foreach(int side in new[]{-1,1})
            {
                planting.FlowerBed(new Vector3(side*(width*.5f-1.4f),.10f,-1.7f),1.9f,.8f,side+4);
                if(outside)
                    for(float z=depth*.35f;z<depth-.8f;z+=3.6f)
                        planting.FlowerBed(new Vector3(side*(width*.5f-.43f),.10f,z),.65f,1.6f,(int)z+side,false);
            }
            planting.Build(root,"Venue flower borders");
        }
        private static void OldMarket(Transform root, float width, float depth)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -.27f : .27f) * width, z = (i < 2 ? .3f : .72f) * depth;
                var stall = new GameObject("OldMarketStall_" + i).transform; stall.SetParent(root, false); stall.localPosition = new Vector3(x, 0, z);
                Prims.Box(stall, "OldGoodsTable", new Vector3(0, .65f, 0), new Vector3(4.5f, 1.3f, 1.8f), Mats.Lit(ArtDirection.Wood));
                Prims.Box(stall, "Canopy", new Vector3(0, 2.8f, .8f), new Vector3(5, .15f, .7f), Mats.Lit(i % 2 == 0 ? ArtDirection.Clay : ArtDirection.Sage));
                Prims.SignText(stall, OldMarketCatalog.Stalls[i], new Vector3(0, 2.3f, -.9f), 4, .55f, ArtDirection.Cream);
                for (int j = 0; j < 3; j++)
                {
                    var at = new Vector3((j - 1) * 1.25f, i == 1 ? 1.56f : 1.36f, 0);
                    if (i == 0) { Prims.Shape(PrimitiveType.Cylinder, stall, "ClockFace", at, new Vector3(.6f, .12f, .6f), Mats.Lit(ArtDirection.Brass)); Prims.Box(stall, "ClockHand", at + Vector3.up * .08f, new Vector3(.035f, .025f, .3f), Mats.Lit(ArtDirection.Ink)); }
                    else if (i == 1) { Prims.Box(stall, "OldRadio", at, new Vector3(.8f, .5f, .45f), Mats.Lit(ArtDirection.Clay)); Prims.Box(stall, "RadioGrille", at + new Vector3(-.15f, .02f, -.24f), new Vector3(.36f, .3f, .025f), Mats.Lit(ArtDirection.Ink)); }
                    else if (i == 2) Prims.Shape(PrimitiveType.Cylinder, stall, "Vinyl", at, new Vector3(.85f, .055f, .85f), Mats.Lit(ArtDirection.Ink));
                    else Prims.Box(stall, "OldBook", at, new Vector3(.7f, .1f, .85f), Mats.Lit(ArtDirection.Cream));
                }
            }
            Prims.Box(root, "OldMarketWorkbench", new Vector3(0, .65f, depth - 1.5f), new Vector3(3, 1.3f, 1.1f), Mats.Lit(ArtDirection.Ink));
            Prims.SignText(root, "公共修理台 · 检测后选择处理方法", new Vector3(0, 2.2f, depth - .25f), width - 1, .6f, ArtDirection.Cream);
        }
        private static void FishingPark(Transform root, float width, float depth)
        {
            Prims.Box(root,"FishingPondBank",new Vector3(width*.14f,.12f,depth*.63f),new Vector3(width*.63f,.18f,depth*.61f),Mats.Lit(ArtDirection.Cream));
            Prims.Box(root,"FishingPondWater",new Vector3(width*.14f,.225f,depth*.63f),new Vector3(width*.58f,.025f,depth*.55f),IndustrialMaterials.GardenWater());
            Prims.Box(root,"ParkWalkingTrail",new Vector3(-width*.36f,.12f,depth*.5f),new Vector3(2,.12f,depth*.88f),IndustrialMaterials.Surface("paving",ArtDirection.Cream,new Vector2(.5f,depth/4)));
            Prims.Box(root,"FishingReception",new Vector3(-width*.32f,.6f,depth*.3f),new Vector3(2,1.2f,1.2f),Mats.Lit(ArtDirection.Wood));
            Prims.SignText(root,"青湾垂钓场 · 租杆 / 鱼获回收",new Vector3(-width*.29f,2,depth*.28f),width*.38f,.48f,ArtDirection.Cream);
            for (int i = 0; i < 3; i++)
            {
                float x = new[] { -.10f, .12f, .34f }[i] * width;
                Prims.Box(root,"FishingDeck_"+i,new Vector3(x,.32f,depth*.36f),new Vector3(1.9f,.15f,2.5f),IndustrialMaterials.Surface("wood",ArtDirection.Wood,new Vector2(1,2)));
                Prims.Box(root,"FishingSeat_"+i,new Vector3(x,.52f,depth*.25f),new Vector3(.8f,.45f,.65f),Mats.Lit(ArtDirection.Sage));
                Prims.Shape(PrimitiveType.Cylinder,root,"FishingBucket_"+i,new Vector3(x+.65f,.5f,depth*.27f),new Vector3(.5f,.45f,.5f),Mats.Lit(ArtDirection.Blue));
                var rod=Prims.Box(root,"FishingRod_"+i,new Vector3(x,1,depth*.36f),new Vector3(.045f,.045f,2.6f),Mats.Lit(ArtDirection.Ink));rod.transform.localRotation=Quaternion.Euler(-12,0,0);
                Prims.Shape(PrimitiveType.Sphere,root,"FishingFloat_"+i,new Vector3(x,.31f,depth*.49f),new Vector3(.16f,.23f,.16f),Mats.Lit(ArtDirection.Brass));
                Prims.SignText(root,FishingCatalog.Spots[i],new Vector3(x,1.2f,depth*.23f),2,.35f,ArtDirection.Cream);
            }
            var garden=new MiniatureDetailMesh();
            for(int i=0;i<5;i++)
                garden.FlowerBed(new Vector3(-width*.12f+i*width*.13f,.10f,depth*.96f),width*.11f,.65f,i,false);
            foreach(float z in new[]{depth*.12f,depth*.69f,depth*.91f})
                CityVegetation.Build(root,new Vector3(-width*.46f,.10f,z),(int)z);
            var wood=IndustrialMaterials.Surface("wood",ArtDirection.Hex("A78052"),Vector2.one);
            var shelter=new Vector3(-width*.29f,0,depth*.28f);
            foreach(int side in new[]{-1,1})garden.Box(shelter+new Vector3(side*1.4f,1.6f,.9f),new Vector3(.14f,3.2f,.14f),wood);
            for(int i=-4;i<=4;i++)garden.Box(shelter+new Vector3(i*.36f,3.25f,.1f),new Vector3(.18f,.17f,2.9f),wood);
            garden.Build(root,"Fishing garden and reception pergola");
        }

        private static void Clothing(Transform root,float width,float depth)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(width/2-1.4f);
                Prims.Box(root,"GarmentRail",new Vector3(x,1.85f,depth*.5f),new Vector3(.09f,.09f,depth*.6f),Mats.Lit(ArtDirection.Brass));
                foreach(float z in new[]{1.8f,depth-1.7f})Prims.Box(root,"RailStand",new Vector3(x,.95f,z),new Vector3(.12f,1.9f,.25f),Mats.Lit(ArtDirection.Ink));
                for(int i=0;i<5;i++)
                {
                    float z=1.8f+i*(depth-3.4f)/4;
                    Color fabric=i%3==0?ArtDirection.Clay:i%3==1?ArtDirection.Sage:ArtDirection.Cream;
                    Prims.Box(root,"HangingShirt",new Vector3(x,1.25f,z),new Vector3(.65f,.85f,.15f),Mats.Lit(fabric));
                    Prims.Box(root,"Sleeves",new Vector3(x,1.55f,z),new Vector3(.95f,.24f,.16f),Mats.Lit(fabric));
                    Prims.Box(root,"Hanger",new Vector3(x,1.76f,z),new Vector3(.42f,.04f,.06f),Mats.Lit(ArtDirection.Brass));
                }
            }
            Prims.Box(root,"FoldingDisplay",new Vector3(-1.3f,.6f,depth*.55f),new Vector3(1.6f,1.2f,1.4f),Mats.Lit(ArtDirection.Clay));
            for(int i=0;i<3;i++)Prims.Box(root,"FoldedClothing",new Vector3(-1.3f,1.27f+i*.1f,depth*.55f),new Vector3(1,.09f,.8f),Mats.Lit(i%2==0?ArtDirection.Cream:ArtDirection.Sage));
            Prims.Box(root,"FittingMirrorFrame",new Vector3(width/2-2.2f,1.4f,depth-.25f),new Vector3(1.25f,2.6f,.13f),Mats.Lit(ArtDirection.Brass));
            Prims.Box(root,"FittingMirror",new Vector3(width/2-2.2f,1.4f,depth-.34f),new Vector3(1.05f,2.35f,.05f),Mats.Lit(new Color(.65f,.78f,.8f),.95f));
            Prims.Box(root,"ClothingCounter",new Vector3(-width/2+2,.65f,depth-1.2f),new Vector3(2.2f,1.3f,1),Mats.Lit(ArtDirection.Sage));
            Prims.SignText(root,"试衣镜 · 原创成衣 · 尺码咨询",new Vector3(0,2.6f,depth-.2f),width-1,.45f,ArtDirection.Cream);
        }
        private static void Chair(Transform root, Vector3 at, Color color)
        {
            Prims.Box(root, "Seat", at + new Vector3(0, .48f, 0), new Vector3(.65f, .12f, .65f), Mats.Lit(color));
            Prims.Box(root, "Back", at + new Vector3(0, .84f, .27f), new Vector3(.65f, .65f, .1f), Mats.Lit(color));
            foreach (int side in new[] { -1, 1 }) Prims.Box(root, "ChairLeg", at + new Vector3(side * .24f, .22f, 0), new Vector3(.08f, .44f, .45f), Mats.Lit(ArtDirection.Ink));
        }
        private static void Bench(Transform root, Vector3 at, Color color)
        {
            Prims.Box(root, "Bench", at + Vector3.up * .5f, new Vector3(2.2f, .18f, .65f), Mats.Lit(color));
            Prims.Box(root, "BenchBack", at + new Vector3(0, .85f, .3f), new Vector3(2.2f, .7f, .12f), Mats.Lit(color));
            Prims.Box(root, "BenchBase", at + Vector3.up * .2f, new Vector3(1.6f, .4f, .45f), Mats.Lit(ArtDirection.Ink));
        }
        private static void Table(Transform root, Vector3 at)
        {
            Prims.Shape(PrimitiveType.Cylinder, root, "TableTop", at + Vector3.up * .9f, new Vector3(1.9f, .09f, 1.9f), Mats.Lit(ArtDirection.Clay));
            Prims.Box(root, "TableBase", at + Vector3.up * .45f, new Vector3(.25f, .9f, .25f), Mats.Lit(ArtDirection.Ink));
            foreach (int side in new[] { -1, 1 })
            {
                var chair=new GameObject("DiningChair").transform;chair.SetParent(root,false);chair.localPosition=at+new Vector3(side*1.15f,0,0);
                chair.localRotation=Quaternion.Euler(0,side*90,0);Chair(chair,Vector3.zero,ArtDirection.Cream);
            }
        }
        private static void Restaurant(Transform root, float width, float depth)
        {
            foreach (float x in new[] { -width * .27f, width * .27f })
            {
                Table(root, new Vector3(x, 0, 2.3f));
                Prims.Shape(PrimitiveType.Cylinder, root, "ServingDish", new Vector3(x, 1.04f, 2.3f), new Vector3(.62f, .07f, .62f), Mats.Lit(ArtDirection.Cream));
                Prims.Shape(PrimitiveType.Sphere, root, "FamilyMeal", new Vector3(x, 1.14f, 2.3f), new Vector3(.4f, .17f, .4f), Mats.Lit(ArtDirection.Sage));
            }
            Prims.Box(root, "ServiceCounter", new Vector3(0, .7f, depth - 1.5f), new Vector3(width - 2, 1.4f, 1.1f), Mats.Lit(ArtDirection.Clay));
            Prims.SignText(root, "家常菜 · 清淡餐 · 热茶", new Vector3(0, 2.1f, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }
        private static void Karaoke(Transform root, float width, float depth)
        {
            var screen = Prims.Box(root, "OriginalMusicScreen", new Vector3(0, 2.05f, depth - .2f), new Vector3(width - 3, 2.5f, .15f), Mats.Unlit(new Color(.22f, .3f, .45f)));
            Prims.SignText(root, "原创伴奏 · 合唱 / 独唱 / 交流", screen.transform.localPosition + new Vector3(0, 0, -.12f), width - 3.5f, .75f, ArtDirection.Cream);
            Bench(root, new Vector3(-width * .24f, 0, 2.5f), ArtDirection.Clay);
            Bench(root, new Vector3(width * .24f, 0, 2.5f), ArtDirection.Clay);
            for (int side = -1; side <= 1; side += 2)
            {
                Prims.Box(root, "Speaker", new Vector3(side * (width / 2 - .7f), 1, depth - 1), new Vector3(.9f, 2, .8f), Mats.Lit(ArtDirection.Ink));
                Prims.Box(root, "MicrophoneStand", new Vector3(side * 1.3f, .65f, 4.5f), new Vector3(.08f, 1.3f, .08f), Mats.Lit(ArtDirection.Brass));
                Prims.Shape(PrimitiveType.Sphere, root, "Microphone", new Vector3(side * 1.3f, 1.35f, 4.5f), new Vector3(.23f, .32f, .23f), Mats.Lit(ArtDirection.Ink));
            }
        }
        private static void Bar(Transform root, float width, float depth)
        {
            Prims.Box(root, "Counter", new Vector3(0, .8f, depth - 2.5f), new Vector3(width - 2, 1.6f, 1.1f), Mats.Lit(ArtDirection.Clay));
            for (int i = -2; i <= 2; i++)
            {
                Prims.Shape(PrimitiveType.Cylinder, root, "Stool", new Vector3(i * 1.6f, .65f, depth - 4.2f), new Vector3(.75f, .65f, .75f), Mats.Lit(ArtDirection.Ink));
                Prims.Shape(PrimitiveType.Cylinder, root, "AlcoholFreeCup", new Vector3(i * 1.6f, 1.72f, depth - 2.5f), new Vector3(.25f, .25f, .25f), Mats.Lit(ArtDirection.Sage));
            }
            Prims.SignText(root, "果饮 / 热茶 / 同桌交流", new Vector3(0, 2.2f, depth - .13f), width - 1, .7f, ArtDirection.Brass);
        }
        private static void Court(Transform root, float width, float depth)
        {
            Prims.Box(root,"CourtPlayingSurface",new Vector3(0,.105f,depth/2),new Vector3(width-1.4f,.025f,depth-.6f),
                IndustrialMaterials.Surface("concrete",ArtDirection.Hex("4A857A"),new Vector2(width/10,depth/10)));
            var white = Mats.Unlit(ArtDirection.Cream);
            foreach (int side in new[] { -1, 1 })
            {
                Prims.Box(root, "CourtSideline", new Vector3(side * (width / 2 - 1), .13f, depth / 2), new Vector3(.08f, .03f, depth - 1), white);
                Prims.Box(root, "CourtEndline", new Vector3(0, .13f, side < 0 ? .5f : depth - .5f), new Vector3(width - 2, .03f, .08f), white);
            }
            Prims.Box(root, "CenterLine", new Vector3(0, .13f, depth / 2), new Vector3(width - 2, .03f, .08f), white);
            Prims.Box(root, "HoopPost", new Vector3(0, 1.7f, depth - .8f), new Vector3(.12f, 3.4f, .12f), Mats.Lit(ArtDirection.Ink));
            Prims.Box(root, "Backboard", new Vector3(0, 3.1f, depth - 1.05f), new Vector3(1.7f, 1, .12f), white);
            var court=new MiniatureDetailMesh();
            var rim=Mats.Lit(ArtDirection.Clay);var net=Mats.Lit(ArtDirection.Cream);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;
                var from=new Vector3(Mathf.Cos(a)*.36f,2.8f,depth-1.5f+Mathf.Sin(a)*.36f);
                var to=new Vector3(Mathf.Cos(b)*.36f,2.8f,depth-1.5f+Mathf.Sin(b)*.36f);
                court.Box((from+to)/2,new Vector3(.04f,.04f,Vector3.Distance(from,to)+.01f),rim,Quaternion.LookRotation(to-from));
                if(i%3==0)
                {
                    var end=new Vector3(Mathf.Cos(a)*.23f,2.38f,depth-1.5f+Mathf.Sin(a)*.23f);
                    court.Box((from+end)/2,new Vector3(.015f,.015f,Vector3.Distance(from,end)),net,Quaternion.LookRotation(end-from));
                }
            }
            float circleRadius=Mathf.Min(1.8f,width*.15f);
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                var from=new Vector3(Mathf.Cos(a)*circleRadius,.133f,depth/2+Mathf.Sin(a)*circleRadius);
                var to=new Vector3(Mathf.Cos(b)*circleRadius,.133f,depth/2+Mathf.Sin(b)*circleRadius);
                court.Box((from+to)/2,new Vector3(.07f,.012f,Vector3.Distance(from,to)+.015f),white,Quaternion.LookRotation(to-from));
            }
            foreach(int side in new[]{-1,1})
                court.Box(new Vector3(side*2,.133f,depth-2.5f),new Vector3(.08f,.012f,4),white);
            court.Box(new Vector3(0,.133f,depth-4.5f),new Vector3(4,.012f,.08f),white);
            court.Build(root,"Court markings and open hoop net");
            Prims.Shape(PrimitiveType.Sphere, root, "Basketball", new Vector3(-1, .37f, depth / 2), Vector3.one * .58f, Mats.Lit(ArtDirection.Clay));
        }
        private static void Golf(Transform root, float width, float depth)
        {
            Prims.Shape(PrimitiveType.Cylinder,root,"PuttingGreen",new Vector3(0,.11f,depth*.76f),new Vector3(width*.63f,.025f,depth*.32f),
                IndustrialMaterials.Surface("meadow",ArtDirection.Hex("A9BA6C"),new Vector2(2,2)));
            foreach(int side in new[]{-1,1})
            {
                Prims.Shape(PrimitiveType.Cylinder,root,"PracticeBunkerRim",new Vector3(side*width*.32f,.10f,depth*.63f),new Vector3(width*.18f,.025f,depth*.20f),Mats.Lit(ArtDirection.Hex("6F934D")));
                Prims.Shape(PrimitiveType.Cylinder,root,"PracticeBunkerSand",new Vector3(side*width*.32f,.13f,depth*.63f),new Vector3(width*.15f,.025f,depth*.17f),
                    IndustrialMaterials.Surface("concrete",ArtDirection.Hex("E0C99D"),Vector2.one));
            }
            var canopy=new MiniatureDetailMesh();var wood=IndustrialMaterials.Surface("wood",ArtDirection.Hex("AC875B"),Vector2.one);
            foreach(int side in new[]{-1,1})
                canopy.Box(new Vector3(side*5.2f,1.7f,1.6f),new Vector3(.16f,3.4f,.16f),wood);
            canopy.Box(new Vector3(0,3.4f,1.6f),new Vector3(10.6f,.20f,.18f),wood);
            for(float x=-5.2f;x<=5.3f;x+=.65f)
                canopy.Box(new Vector3(x,3.6f,1.6f),new Vector3(.16f,.16f,3.2f),wood);
            canopy.Build(root,"Golf timber practice pergola");
            for (int i = -1; i <= 1; i++)
            {
                Prims.Box(root, "PracticeMat", new Vector3(i * 3, .14f, 1.6f), new Vector3(2, .08f, 2.4f), Mats.Lit(new Color(.21f, .42f, .3f)));
                Prims.Shape(PrimitiveType.Sphere, root, "GolfBall", new Vector3(i * 3 + .45f, .25f, 2), Vector3.one * .18f, Mats.Lit(ArtDirection.Cream));
                Prims.Box(root, "GolfClub", new Vector3(i * 3 - .5f, .75f, 1.6f), new Vector3(.06f, 1.4f, .06f), Mats.Lit(ArtDirection.Brass));
            }
            Prims.Box(root, "FlagPole", new Vector3(0, 1.2f, depth - 1.8f), new Vector3(.07f, 2.4f, .07f), Mats.Lit(ArtDirection.Ink));
            Prims.Box(root, "PracticeFlag", new Vector3(.42f, 2.1f, depth - 1.8f), new Vector3(.8f, .45f, .05f), Mats.Lit(ArtDirection.Clay));
            Prims.SignText(root, "体验练习 · 轮流击球", new Vector3(0, 2, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }
        private static void Expo(Transform root, float width, float depth)
        {
            root.gameObject.AddComponent<QuarterlyExpoView>().Initialize(width,depth);
        }
        private static void Park(Transform root, float width, float depth, bool hike, bool rowing = false)
        {
            if(!hike && width>=30)
            {
                var stone=Mats.Lit(ArtDirection.Cream);
                Prims.Shape(PrimitiveType.Cylinder,root,"LakeShore",new Vector3(0,.12f,depth*.53f),new Vector3(width*.65f,.12f,depth*.63f),stone);
                Prims.Shape(PrimitiveType.Cylinder,root,"GardenLake",new Vector3(0,.25f,depth*.53f),new Vector3(width*.61f,.025f,depth*.57f),IndustrialMaterials.GardenWater());
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"LakesideWalk",new Vector3(0,.16f,depth*(side<0?.13f:.91f)),new Vector3(width*.9f,.08f,1.8f),stone);
                    CityVegetation.Build(root,new Vector3(side*width*.31f,0,depth*.16f),side+7);
                    Prims.Box(root,"LakesideBench",new Vector3(side*width*.25f,.5f,depth*.84f),new Vector3(2,.2f,.6f),Mats.Lit(ArtDirection.Wood));
                }
                var wood = Mats.Lit(ArtDirection.Wood);
                Prims.Box(root, "LakesideDeck", new Vector3(width * .25f, .35f, depth * .35f),
                    new Vector3(5, .18f, 3), wood);
                if (rowing)
                {
                    var boat = new GameObject("CommunityParkRowboat").transform;
                    boat.SetParent(root, false);
                    boat.localPosition = new Vector3(width * .20f, .31f, depth * .44f);
                    boat.localRotation = Quaternion.Euler(0, -38, 0);
                    Prims.Shape(PrimitiveType.Sphere, boat, "WoodenHull", Vector3.zero,
                        new Vector3(1.32f, .30f, 2.8f), wood);
                    Prims.Box(boat, "RowboatSeat", new Vector3(0, .17f, .06f), new Vector3(1.1f, .10f, .37f), stone);
                    Prims.Box(boat, "RowboatOar", new Vector3(0, .20f, -.28f), new Vector3(2.3f, .06f, .10f), wood);
                }
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;
                    Vector3 bank=new Vector3(Mathf.Cos(angle)*width*.37f,0,depth*.53f+Mathf.Sin(angle)*depth*.37f);
                    if(i%4!=0)CityVegetation.Build(root,bank,i+21);
                    Prims.Shape(PrimitiveType.Sphere,root,"ShoreRock",bank+new Vector3(-.65f,.22f,.3f),new Vector3(.9f,.5f,.65f),Mats.Lit(ArtDirection.Hex("C6C4AD")));
                    if(i%2==0)for(int f=-1;f<=1;f++)
                        Prims.Shape(PrimitiveType.Sphere,root,"WatersideFlowers",bank+new Vector3(f*.4f,.3f,-.8f),new Vector3(.5f,.4f,.5f),Mats.Lit(ArtDirection.Hex("E3B54C")));
                }
                var pergola=new GameObject("LakesidePergola").transform;pergola.SetParent(root,false);pergola.localPosition=new Vector3(-width*.27f,0,depth*.20f);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    Prims.Box(pergola,"Post",new Vector3(x*1.7f,1.4f,z),new Vector3(.16f,2.8f,.16f),stone);
                for(int i=-4;i<=4;i++)Prims.Box(pergola,"RoofSlat",new Vector3(i*.45f,2.85f,0),new Vector3(.15f,.18f,2.7f),Mats.Lit(ArtDirection.Wood));
            }
            else Prims.Box(root, hike ? "WalkingTrail" : "River", new Vector3(0, .12f, depth / 2), new Vector3(hike ? 2.2f : 3.5f, .12f, depth), Mats.Lit(hike ? ArtDirection.Cream : new Color(.28f, .48f, .54f)));
            for (int side = -1; side <= 1; side += 2)
            {
                CityVegetation.Build(root,new Vector3(side*(width/2-1.5f),0,depth-2),side+3);
                if (!hike && !rowing) Prims.Box(root, "FishingRod", new Vector3(side * 2.1f, .65f, 2), new Vector3(.06f, 1.2f, .06f), Mats.Lit(ArtDirection.Brass));
            }
            if (hike) Prims.Box(root, "ViewDeck", new Vector3(0, .4f, depth - 1.8f), new Vector3(width - 3, .8f, 2.7f), Mats.Lit(ArtDirection.Clay));
            Prims.SignText(root, hike ? "步道 · 观景平台" : rowing ? "湖边散步 · 小船码头" : "河岸 · 垂钓 · 散步", new Vector3(0, 2, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }
    }
}
