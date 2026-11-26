using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>Open-front social spaces share a legible entrance and real furniture, without loading parallel scenes.</summary>
    public static class CityVenueView
    {
        public static void Build(Transform root, Place p, float width, float depth, Material districtLawn = null)
        {
            string kind = (p.Business ?? "").Replace("venue_", "");
            if (kind is "ferris" or "amusement")
            {
                AmusementParkView.Build(root, p, width, depth);
                return;
            }
            if (kind == "pachinko")
            {
                LeisureVenueEntrance.BuildPachinkoRoom(root, p, width, depth);
                return;
            }
            bool walkingPark = kind == "hike" && p.Id != GameSession.FishingVenueId;
            bool lawn = kind is "golf" or "park" or "hike";
            Color floor = kind == "court" ? ArtDirection.Sage : ArtDirection.Cream;
            Vector3 plotCenter = new Vector3(0, .045f, depth / 2 - 1);
            Vector3 plotSize = new Vector3(width, .09f, depth + 6);
            var venueTiling = new Vector2(width / 14, depth / 14);
            Material ground;
            if (walkingPark)
                ground = IndustrialMaterials.Surface("paving", ArtDirection.Hex("D3B993"), new Vector2(width / 4, (depth + 6) / 4));
            else if (lawn)
                ground = districtLawn ?? (kind=="park" && width>=30 ? DistrictFinishes.Meadow() : IndustrialMaterials.Surface("meadow", ArtDirection.Hex("7D9859"), venueTiling));
            else
                ground = IndustrialMaterials.Surface("paving", floor, venueTiling);
            var body = Prims.Box(root, "Body", plotCenter, plotSize, ground, true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;
            if (walkingPark)
            {
                var trimmedLawn = districtLawn ?? IndustrialMaterials.Surface("meadow", ArtDirection.Hex("A9C986"), venueTiling);
                Prims.Box(root, "WalkingParkLawn", new Vector3(0, .09f, plotCenter.z),
                    new Vector3(plotSize.x - 1.15f, .06f, plotSize.z - 1.15f), trimmedLawn);
            }
            bool outside=kind=="court"||kind=="golf"||kind=="park"||kind=="hike";
            if(outside)
            {
                for(float x=-width/2;x<=width/2;x+=2)
                    Prims.Box(root,"OutdoorFencePost",new Vector3(x,.85f,depth),new Vector3(.1f,1.7f,.1f),Mats.Lit(ArtDirection.Ink));
                foreach(float y in new[]{.55f,1.35f})Prims.Box(root,"OutdoorFenceRail",new Vector3(0,y,depth),new Vector3(width,.07f,.07f),Mats.Lit(ArtDirection.Sage));
            }
            else Prims.Box(root, "BackBoundary", new Vector3(0, 1.5f, depth), new Vector3(width, 3, .2f), Mats.Lit(ArtDirection.Ink));
            float gateWidth = walkingPark ? Mathf.Min(width - .4f, 10.8f) : width - .4f;
            bool civicPark = kind=="park" && width>=30;
            Color fascia = walkingPark ? ArtDirection.Hex("945B3C") : ArtDirection.Sage;
            var support = Mats.Lit(walkingPark ? ArtDirection.Hex("79533A") : ArtDirection.Brass);
            float postSize = walkingPark ? .32f : .18f;
            if(civicPark)
            {
                var wall=DistrictFinishes.Tile("pearlCeramic",new Vector2(4,1),ArtDirection.Hex("ECEAE0"));
                float wing=width*.5f-1.8f;
                foreach(int side in new[]{-1,1})
                    Prims.Box(root,"池岸景墙",new Vector3(side*(1.65f+wing*.5f),.72f,0),new Vector3(wing,1.28f,.30f),wall);
                var arch=new MiniatureDetailMesh();
                float end=width*.5f-.24f;
                arch.Box(new Vector3(end,1.28f,0),new Vector3(.27f,2.55f,.30f),wall);
                arch.Box(new Vector3(end-1.43f,2.49f,0),new Vector3(3.12f,.18f,.30f),wall);
                arch.Box(new Vector3(end-2.87f,1.28f,0),new Vector3(.23f,2.55f,.30f),wall);
                arch.Build(root,"白瓷观景框");
                Prims.SignText(root,p.Name,new Vector3(wing*.56f,.85f,-.17f),Mathf.Min(5,wing*.7f),.63f,ArtDirection.Hex("49625A"));
            }
            else
            {
                Prims.Box(root, "EntranceFascia", new Vector3(0, 3.5f, -.1f), new Vector3(gateWidth, .9f, .24f), Mats.Lit(fascia));
                Prims.SignText(root, p.Name, new Vector3(0, 3.5f, -.25f), gateWidth - .4f, .65f, ArtDirection.Cream);
                Prims.Box(root, "SignSupportLeft", new Vector3(-gateWidth / 2 + .1f, 1.8f, 0), new Vector3(postSize, 3.6f, postSize), support);
                Prims.Box(root, "SignSupportRight", new Vector3(gateWidth / 2 - .1f, 1.8f, 0), new Vector3(postSize, 3.6f, postSize), support);
            }
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
                case "poker": case "claw": case "billiards": case "cinema":
                    LeisureVenueEntrance.Build(root, p, width, depth);
                    break;
                case "hike":
                    if (walkingPark) WalkingPark(root, width, depth);
                    else FishingPark(root, width, depth);
                    break;
                default: Park(root, width, depth, p.Id == GameSession.CommunityParkId); break;
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
            var bankStone=DistrictFinishes.Tile("cutStone",new Vector2(width*.63f/7f,depth*.61f/4.9f),ArtDirection.Hex("BAB8B5"));
            Prims.Box(root,"FishingPondBank",new Vector3(width*.14f,.12f,depth*.63f),new Vector3(width*.63f,.18f,depth*.61f),bankStone);
            var wetStone=DistrictFinishes.Tile("cutStone",new Vector2(width*.58f/7f,depth*.55f/4.9f),ArtDirection.Hex("757F76"));
            Prims.Box(root,"FishingWaterline",new Vector3(width*.14f,.214f,depth*.63f),new Vector3(width*.58f+.34f,.012f,depth*.55f+.34f),wetStone);
            Prims.Box(root,"FishingPondWater",new Vector3(width*.14f,.225f,depth*.63f),new Vector3(width*.58f,.025f,depth*.55f),IndustrialMaterials.GardenWater(IndustrialMaterials.WaterBasin.FishingPond));
            Prims.Box(root,"ParkWalkingTrail",new Vector3(-width*.36f,.12f,depth*.5f),new Vector3(2,.12f,depth*.88f),
                DistrictFinishes.Tile("cutStone",new Vector2(.40f,depth*.88f/6.8f),ArtDirection.Hex("C3BDB4")));
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
            var reflections = new LakesideReflections(root,.2375f);
            foreach(float z in new[]{depth*.12f,depth*.69f,depth*.91f})
                reflections.Add(PaintedWoodland.Plant(root,new Vector3(-width*.46f,.10f,z),6,0,(int)z,district:1));
            var margin = new StreetPlanting();
            float right = width*.43f, back = depth*.905f;
            // Keep water beside the three fishing floats clear. Low clumps gather
            // in the shallow corners and in just one gap between the timber decks.
            foreach(var fringe in new[]{new Vector2(-.143f,.428f),new Vector2(.025f,.365f),new Vector2(.422f,.614f)})
            {
                for(int stem=0;stem<4;stem++)
                {
                    float angle=stem*2.4f+fringe.y*8f;
                    var at=new Vector3(width*fringe.x+.36f*Mathf.Cos(angle),.227f,depth*fringe.y+.28f*Mathf.Sin(angle));
                    margin.Add(PlantSpecies.Reeds,at,.35f+.10f*(stem%3),.25f+.13f*(stem%2),stem+Mathf.RoundToInt(fringe.y*130));
                }
            }
            for (int i=0;i<12;i++)
            {
                float t=(i+.5f)/12;
                var bank=new Vector3(Mathf.Lerp(-width*.14f,right,t),.235f,back-.18f);
                margin.Add(i%4==0?PlantSpecies.Cattail:PlantSpecies.Reeds,bank,.48f,.43f+(i%3)*.17f,i+32);
                var stone=bank+new Vector3(.13f,-.034f,-.20f-i%2*.19f);
                garden.Lobe(stone,new Vector3(.26f,.073f,.16f),Mats.Lit(ArtDirection.Hex("848777")),i);
            }
            reflections.Add(PaintedWoodland.Plant(root,new Vector3(right+.46f,.11f,back+.50f),6,0,89,district:1));
            margin.Build(root,"钓池岸边的蒲草");reflections.Build();
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
        private static void WalkingPark(Transform root, float width, float depth)
        {
            float trailLength = depth + 4;
            var paving = IndustrialMaterials.Surface("paving", ArtDirection.Hex("DDC9A8"), new Vector2(.85f, trailLength / 4));
            Prims.Box(root, "WalkingTrail", new Vector3(0, .126f, depth / 2 - 2),
                new Vector3(3.4f, .10f, trailLength), paving);
            var timber = IndustrialMaterials.Surface("wood", ArtDirection.Hex("B88B64"), new Vector2((width - 3) / 4, 1));
            Prims.Box(root, "ViewDeck", new Vector3(0, .4f, depth - 1.8f), new Vector3(width - 3, .8f, 2.7f), timber);
            for (int side = -1; side <= 1; side += 2)
                CityVegetation.Build(root, new Vector3(side * (width / 2 - 1.5f), 0, depth - 2), side + 3);
            Prims.SignText(root, "步道 · 观景平台", new Vector3(0, 2, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }

        internal static void PromenadePond(Transform parcel,float width,float depth)
        {
            var park=new ParkPondWalk(width*.94f,depth*.82f);
            var waterGarden=new GameObject("中央绿地椭圆池").transform;
            waterGarden.SetParent(parcel,false);
            waterGarden.localPosition=Vector3.forward*(depth*.52f-(float)park.CenterZ);
            var stone=DistrictFinishes.Tile("graniteWalk",Vector2.one,ArtDirection.Hex("D9D6DB"));
            PondWalk(waterGarden,park,stone);
            var radius=new Vector2((float)park.RadiusX,(float)park.RadiusZ);
            PondSurface(waterGarden,new Vector3(0,.241f,(float)park.CenterZ),radius+Vector2.one*.06f);
            var shrubs=new StreetPlanting(maintained:true);
            var reflected=new LakesideReflections(waterGarden,.241f,parkPond:true);
            for(int group=0;group<15;group++)
            {
                float angle=(group+.41f)*2*Mathf.PI/15;
                var edge=park.Edge(angle,park.WidthAt(angle)+1.18);
                var bank=new Vector3((float)edge.X,.12f,(float)edge.Y);
                if(group%3==0)
                    reflected.Add(PaintedWoodland.Plant(waterGarden,bank,group==6?10:8,group==6?1:0,group+41,district:2));
                var tangent=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle));
                for(int j=0;j<5;j++)
                    shrubs.Add(j==1 && group%3==0?PlantSpecies.Hydrangea:j<2?PlantSpecies.Spirea:PlantSpecies.Broadleaf,
                        bank+tangent*(j-2)*.82f,1.47f,.82f+group%3*.13f,group*7+j/3);
            }
            shrubs.Build(waterGarden,"池岸植床");reflected.Build();
        }

        private static void PondWalk(Transform root, ParkPondWalk park, Material paving)
        {
            const int sectors = ParkPondWalk.Segments;
            const int stride=6;
            var vertices = new Vector3[(sectors+1)*stride];
            var uv = new Vector2[vertices.Length];
            var triangles = new System.Collections.Generic.List<int>[4];
            for(int i=0;i<triangles.Length;i++) triangles[i]=new System.Collections.Generic.List<int>();
            void Strip(int layer,int v)
            {
                var t=triangles[layer];
                t.Add(v);t.Add(v+stride);t.Add(v+1);
                t.Add(v+1);t.Add(v+stride);t.Add(v+stride+1);
            }
            float distance = 0;
            for (int i=0;i<=sectors;i++)
            {
                var bank=park.Inner(i);var outer=park.Outer(i);
                int v = stride*i;
                var inner=new Vector3((float)bank.X,ParkPondWalk.Top,(float)bank.Y);
                var outside=new Vector3((float)outer.X,ParkPondWalk.Top,(float)outer.Y);
                var normal=(outside-inner).normalized;
                // The dry footway follows the exact navigation polygon. Only its bevel
                // projects over the water, within the old coping's 13 cm overhang.
                vertices[v]=inner-.13f*normal-.080f*Vector3.up;
                vertices[v+1]=inner-.070f*normal+.009f*Vector3.up;
                vertices[v+2]=vertices[v+1];
                vertices[v+3]=inner+.16f*normal+.006f*Vector3.up;
                vertices[v+4]=vertices[v+3];
                vertices[v+5]=outside;
                if(i>0) distance+=Vector3.Distance(outside,vertices[v-stride+5]);
                float u=distance/3.65f;
                uv[v]=new Vector2(u,.13f);uv[v+1]=new Vector2(u,.17f);
                uv[v+2]=new Vector2(u,.34f);uv[v+3]=new Vector2(u,.39f);
                uv[v+4]=new Vector2(u,.09f);
                uv[v+5]=new Vector2(u,.09f+Vector3.Distance(vertices[v+4],outside)/3.65f);
                if(i==sectors) continue;
                // Moss is confined to intermittent stretches of the damp face by reed beds.
                float angle=i*2*Mathf.PI/sectors;
                bool moss=(angle>1.01f && angle<1.33f)||(angle>3.72f && angle<4.10f)||(angle>5.61f && angle<5.84f);
                Strip(moss?3:2,v);Strip(1,v+2);Strip(0,v+4);
            }
            var mesh = new Mesh { name="河岸公园石板环道", vertices=vertices, uv=uv, subMeshCount=4 };
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var owner=root.GetComponent<CityLandscapeResources>() ?? root.gameObject.AddComponent<CityLandscapeResources>();
            owner.Owned.Add(mesh);
            var walk = new GameObject("池畔环道",typeof(MeshFilter),typeof(MeshRenderer));
            walk.transform.SetParent(root,false);
            walk.GetComponent<MeshFilter>().sharedMesh=mesh;
            walk.GetComponent<MeshRenderer>().sharedMaterials=new[]{
                paving, DistrictFinishes.Tile("cutStone",Vector2.one,ArtDirection.Hex("D4D7D2")),
                DistrictFinishes.Tile("cutStone",Vector2.one,ArtDirection.Hex("748583")),
                DistrictFinishes.Tile("cutStone",Vector2.one,ArtDirection.Hex("747F64"))};
        }

        private static void PondSurface(Transform root, Vector3 center, Vector2 radius)
        {
            // Use the same vertices as the coping and the traversable stone path.
            const int sectors=ParkPondWalk.Segments;
            var vertices=new Vector3[sectors+1];
            var coordinates=new Vector2[sectors+1];
            var triangles=new int[sectors*3];
            vertices[0]=center;
            coordinates[0]=Vector2.one*.5f;
            for(int i=0;i<sectors;i++)
            {
                float angle=i*2*Mathf.PI/sectors;
                var ray=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                coordinates[i+1]=ray*.5f+Vector2.one*.5f;
                vertices[i+1]=center+new Vector3(ray.x*radius.x,0,ray.y*radius.y);
                triangles[i*3]=0;
                triangles[i*3+1]=(i+1)%sectors+1;
                triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh { name="公园椭圆池面", vertices=vertices, uv=coordinates, triangles=triangles };
            mesh.RecalculateNormals();
            var owner=root.GetComponent<CityLandscapeResources>() ?? root.gameObject.AddComponent<CityLandscapeResources>();
            owner.Owned.Add(mesh);
            var surface=new GameObject("GardenLake",typeof(MeshFilter),typeof(MeshRenderer));
            surface.transform.SetParent(root,false);
            surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=IndustrialMaterials.GardenWater(IndustrialMaterials.WaterBasin.Pond);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;

            // Floating pads follow two sheltered banks and leave the boat landing and open water free.
            var pads=new MiniatureDetailMesh();
            var colors=new[]{Mats.Lit(ArtDirection.Hex("789E65")),Mats.Lit(ArtDirection.Hex("A5B776")),Mats.Lit(ArtDirection.Hex("608B68"))};
            for(int patch=0;patch<3;patch++)
            {
                int count=patch==1?14:9;
                float clusterAngle=patch==0?.58f:patch==1?2.8f:4.56f;
                for(int leaf=0;leaf<count;leaf++)
                {
                    float angle=clusterAngle+.32f*Mathf.Sin(leaf*2.40f)*Mathf.Sqrt((leaf+.5f)/count);
                    float radial=.77f+.16f*(.5f+.5f*Mathf.Cos(leaf*1.77f+patch));
                    var at=center+new Vector3(Mathf.Cos(angle)*radius.x*radial,.025f+.002f*(leaf%4),Mathf.Sin(angle)*radius.y*radial);
                    float size=.17f+.14f*(.5f+.5f*Mathf.Sin(leaf*8.3f+patch));
                    var part=pads.Part(colors[(leaf+patch)%colors.Length]);
                    int first=part.V.Count;
                    part.V.Add(at);
                    for(int k=0;k<=10;k++)
                    {
                        float a=leaf*2.4f+k*.57f;
                        part.V.Add(at+new Vector3(Mathf.Cos(a)*size,.012f,Mathf.Sin(a)*size*.84f));
                        if(k==0)continue;
                        part.T.Add(first);part.T.Add(first+k+1);part.T.Add(first+k);
                    }
                }
            }
            pads.Build(root,"岸边睡莲浮叶");
        }

        private static void Park(Transform root, float width, float depth, bool rowing)
        {
            if(width>=30)
            {
                var stone=DistrictFinishes.Tile("cutStone",new Vector2(width/4,depth/4));
                var park=new ParkPondWalk(width,depth);
                var center=new Vector3(0,ParkPondWalk.Top,(float)park.CenterZ);
                var radius=new Vector2((float)park.RadiusX,(float)park.RadiusZ);
                var pavingTint=ArtDirection.Hex("EAE8E6");
                PondWalk(root,park,DistrictFinishes.Tile("graniteWalk",Vector2.one,pavingTint));
                var footpath=DistrictFinishes.Tile("cutStone",new Vector2(.44f,1.12f),pavingTint);
                PondSurface(root,new Vector3(0,.241f,center.z),radius+Vector2.one*.06f);
                var reflections=new LakesideReflections(root,.241f,parkPond:true);
                var planting=new StreetPlanting(maintained:true);
                // The entrance spur joins the ring on its near side; a second straight white strip
                // running the length of the front wall used to read as a gap through the masonry.
                float gateToRing=(float)park.Centerline(3*ParkPondWalk.Segments/4).Y;
                Prims.Box(root,"池畔入口步径",new Vector3(0,ParkPondWalk.Top-.026f,gateToRing*.5f),
                    new Vector3(ParkPondWalk.EntranceWidth,.05f,gateToRing),footpath);
                float rise=ParkPondWalk.Top-.027f;
                float rampLength=Mathf.Sqrt(ParkPondWalk.EntranceStart*ParkPondWalk.EntranceStart+rise*rise);
                var ramp=Prims.Box(root,"入园缓坡",new Vector3(0,(ParkPondWalk.Top+.027f)*.5f-.025f,ParkPondWalk.EntranceStart*.5f),
                    new Vector3(ParkPondWalk.EntranceWidth,.05f,rampLength),footpath);
                ramp.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(rise,-ParkPondWalk.EntranceStart)*Mathf.Rad2Deg,0,0);
                foreach(int side in new[]{-1,1})
                {
                    DistrictFinishes.SlattedBench(root,new Vector3(side*width*.25f,.16f,depth*.86f),side*18);
                    DistrictFinishes.SlattedBench(root,new Vector3(side*width*.33f,.16f,depth*.20f),180-side*14);
                }
                var wood = Mats.Lit(ArtDirection.Wood);
                var landing=rowing?wood:DistrictFinishes.Tile("limestoneWalk",Vector2.one*.8f);
                Prims.Box(root,"LakesideDeck",new Vector3(width*.25f,.31f,depth*.35f),
                    new Vector3(rowing?5:3.1f,.14f,rowing?3:1.8f),landing);
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
                Vector3 BankAt(float angle,float offset,float y=.09f)
                {
                    var point=park.Edge(angle,offset);
                    return new Vector3((float)point.X,y,(float)point.Y);
                }
                var trees=new[]{(angle:.03f,height:7),(angle:.81f,height:6),(angle:1.24f,height:8),
                    (angle:2.37f,height:10),(angle:3.16f,height:7),(angle:3.86f,height:6),
                    (angle:5.63f,height:7),(angle:1.78f,height:6),(angle:5.26f,height:5)};
                for(int i=0;i<trees.Length;i++)
                {
                    var tree=trees[i];
                    var foot=BankAt(tree.angle,(float)park.WidthAt(tree.angle)+1.0f+(i%3)*.26f);
                    var canopy=PaintedWoodland.Plant(root,foot,tree.height,i==3?1:0,i+21,district:2);
                    reflections.Add(canopy);
                }
                // Gaps between drifts expose the stone circuit. The long axis of each bed follows its local bank.
                float[] beds={.12f,.63f,1.04f,1.91f,2.28f,2.85f,3.38f,3.99f,4.31f,5.12f,5.57f,6.06f};
                for(int i=0;i<beds.Length;i++)
                {
                    int count=4+(i*3)%7;
                    for(int j=0;j<count;j++)
                    {
                        float spread=Mathf.Sqrt((j+.5f)/count);
                        float a=beds[i]+.18f*spread*Mathf.Cos(j*2.40f+i);
                        float outward=(float)park.WidthAt(a)+.85f+.94f*(.5f+.5f*spread*Mathf.Sin(j*2.40f+i));
                        var foot=BankAt(a,outward);
                        if(foot.z>depth-.55f || foot.z<.75f || (Mathf.Abs(foot.x)<1.85f && foot.z<center.z))continue;
                        var species=(i+j)%6==0?PlantSpecies.SilverGrass:i%3==1?PlantSpecies.Hydrangea:
                            j%3==0?PlantSpecies.Broadleaf:PlantSpecies.Spirea;
                        float size=1.12f+.52f*(.5f+.5f*Mathf.Sin(i*1.7f+j*3.2f));
                        planting.Add(species,foot,size,.59f+.33f*spread,i*13+j);
                    }
                }
                foreach(float cluster in new[]{1.17f,3.95f,5.73f})
                    for(int stem=0;stem<5;stem++)
                    {
                        float a=cluster+.085f*Mathf.Sin(stem*2.4f);
                        var foot=BankAt(a,-.23f-.39f*(.5f+.5f*Mathf.Cos(stem*2.4f)),.18f);
                        planting.Add(stem==1?PlantSpecies.Reeds:PlantSpecies.Cattail,foot,.60f,.65f+.16f*(stem%3),stem+48);
                    }
                // The upper flowers show over the 1.36 m wall from the street. Shorter
                // mounds face the pond, with lawn breaks between the drifts and gate.
                foreach(int side in new[]{-1,1})
                {
                    for(int j=0;j<9;j++)
                    {
                        float along=width*.47f-.79f*(j%5);
                        bool behind=j>=5;
                        var foot=new Vector3(side*along,.11f,behind?1.64f:.79f);
                        float height=behind?.88f+.07f*(j%3):1.26f+.11f*(j%3);
                        planting.Add(PlantSpecies.Hydrangea,foot,1.29f+.14f*(j%3),height,side<0?8:11);
                    }
                    for(int j=0;j<4;j++)
                    {
                        var foot=new Vector3(side*(width*.27f+.78f*j),.11f,1.07f+.16f*(j%2));
                        planting.Add(j==3?PlantSpecies.Spirea:PlantSpecies.Hydrangea,foot,1.29f,1.18f+.14f*(j%2),side<0?17:8);
                    }
                }
                planting.Build(root,"池畔花丛与浅水芦苇");reflections.Build();
                var pergola=new GameObject("LakesidePergola").transform;pergola.SetParent(root,false);pergola.localPosition=new Vector3(-width*.27f,0,depth*.20f);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    Prims.Box(pergola,"Post",new Vector3(x*1.7f,1.4f,z),new Vector3(.16f,2.8f,.16f),stone);
                for(int i=-4;i<=4;i++)Prims.Box(pergola,"RoofSlat",new Vector3(i*.45f,2.85f,0),new Vector3(.15f,.18f,2.7f),Mats.Lit(ArtDirection.Wood));
            }
            else Prims.Box(root, "River", new Vector3(0, .12f, depth / 2), new Vector3(3.5f, .12f, depth), Mats.Lit(new Color(.28f, .48f, .54f)));
            for (int side = -1; side <= 1; side += 2)
            {
                CityVegetation.Build(root,new Vector3(side*(width/2-1.5f),0,depth-2),side+3);
                if (!rowing) Prims.Box(root, "FishingRod", new Vector3(side * 2.1f, .65f, 2), new Vector3(.06f, 1.2f, .06f), Mats.Lit(ArtDirection.Brass));
            }
            Prims.SignText(root, rowing ? "湖边散步 · 小船码头" : "河岸 · 垂钓 · 散步", new Vector3(0, 2, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }
    }
}
