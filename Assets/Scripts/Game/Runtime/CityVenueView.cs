using PCShopSell.Core.World;
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
            var body = Prims.Box(root, "Body", new Vector3(0, .045f, depth / 2 - 1), new Vector3(width, .09f, depth + 6), Mats.Lit(floor), true);
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
                case "hike": Park(root, width, depth, true); break;
                default: Park(root, width, depth, false); break;
            }
            if(kind=="restaurant"||kind=="ktv"||kind=="bar"||kind=="expo")
            {
                Prims.Box(root,"VenueClimateUnit",new Vector3(width*.3f,2.7f,depth-.2f),new Vector3(1.5f,.55f,.3f),Mats.Lit(ArtDirection.Cream));
                Prims.Box(root,"ClimateOutlet",new Vector3(width*.3f,2.53f,depth-.37f),new Vector3(1.25f,.08f,.04f),Mats.Lit(ArtDirection.Ink));
            }
            // Arrival/meeting positions deliberately remain in the open forecourt, not behind a wall.
            Bench(root, new Vector3(-3.2f, 0, -1.4f), ArtDirection.Sage);
            Bench(root, new Vector3(3.2f, 0, -1.4f), ArtDirection.Clay);
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
            var white = Mats.Unlit(ArtDirection.Cream);
            foreach (int side in new[] { -1, 1 })
            {
                Prims.Box(root, "CourtSideline", new Vector3(side * (width / 2 - 1), .13f, depth / 2), new Vector3(.08f, .03f, depth - 1), white);
                Prims.Box(root, "CourtEndline", new Vector3(0, .13f, side < 0 ? .5f : depth - .5f), new Vector3(width - 2, .03f, .08f), white);
            }
            Prims.Box(root, "CenterLine", new Vector3(0, .13f, depth / 2), new Vector3(width - 2, .03f, .08f), white);
            Prims.Box(root, "HoopPost", new Vector3(0, 1.7f, depth - .8f), new Vector3(.12f, 3.4f, .12f), Mats.Lit(ArtDirection.Ink));
            Prims.Box(root, "Backboard", new Vector3(0, 3.1f, depth - 1.05f), new Vector3(1.7f, 1, .12f), white);
            Prims.Shape(PrimitiveType.Cylinder, root, "Hoop", new Vector3(0, 2.8f, depth - 1.5f), new Vector3(.72f, .04f, .72f), Mats.Lit(ArtDirection.Clay));
            Prims.Shape(PrimitiveType.Sphere, root, "Basketball", new Vector3(-1, .37f, depth / 2), Vector3.one * .58f, Mats.Lit(ArtDirection.Clay));
        }
        private static void Golf(Transform root, float width, float depth)
        {
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
            string[] names = { "供应商 / 新品", "企业需求 / 行业", "原创模型 / 创作者" };
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * (width / 3);
                Prims.Box(root, "ExhibitionBooth", new Vector3(x, .8f, depth - 3), new Vector3(width / 3 - .6f, 1.6f, 1.4f), Mats.Lit(i == 2 ? ArtDirection.Clay : ArtDirection.Sage));
                Prims.SignText(root, names[i], new Vector3(x, 2.8f, depth - 1.9f), width / 3 - .5f, .8f, ArtDirection.Cream);
                for (int j = -1; j <= 1; j++) Prims.Box(root, "OriginalDisplay", new Vector3(x + j * .7f, 1.9f, depth - 3), new Vector3(.45f, .6f, .45f), Mats.Lit(j == 0 ? ArtDirection.Brass : ArtDirection.Cream));
            }
            Prims.Box(root, "Reception", new Vector3(width / 2 - 1.6f, .6f, -.4f), new Vector3(2, 1.2f, 1.2f), Mats.Lit(ArtDirection.Ink));
        }
        private static void Park(Transform root, float width, float depth, bool hike)
        {
            if(!hike && width>=30)
            {
                var stone=Mats.Lit(ArtDirection.Cream);
                Prims.Shape(PrimitiveType.Cylinder,root,"LakeShore",new Vector3(0,.12f,depth*.53f),new Vector3(width*.65f,.12f,depth*.63f),stone);
                Prims.Shape(PrimitiveType.Cylinder,root,"GardenLake",new Vector3(0,.25f,depth*.53f),new Vector3(width*.61f,.025f,depth*.57f),Mats.Lit(ArtDirection.Hex("479FB3")));
                foreach(int side in new[]{-1,1})
                {
                    Prims.Box(root,"LakesideWalk",new Vector3(0,.16f,depth*(side<0?.13f:.91f)),new Vector3(width*.9f,.08f,1.8f),stone);
                    CityVegetation.Build(root,new Vector3(side*width*.31f,0,depth*.16f),side+7);
                    Prims.Box(root,"LakesideBench",new Vector3(side*width*.25f,.5f,depth*.84f),new Vector3(2,.2f,.6f),Mats.Lit(ArtDirection.Wood));
                }
                Prims.Box(root,"LakesideDeck",new Vector3(width*.25f,.35f,depth*.35f),new Vector3(5,.18f,3),Mats.Lit(ArtDirection.Wood));
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
                if (!hike) Prims.Box(root, "FishingRod", new Vector3(side * 2.1f, .65f, 2), new Vector3(.06f, 1.2f, .06f), Mats.Lit(ArtDirection.Brass));
            }
            if (hike) Prims.Box(root, "ViewDeck", new Vector3(0, .4f, depth - 1.8f), new Vector3(width - 3, .8f, 2.7f), Mats.Lit(ArtDirection.Clay));
            Prims.SignText(root, hike ? "步道 · 观景平台" : "河岸 · 垂钓 · 散步", new Vector3(0, 2, depth - .13f), width - 1, .65f, ArtDirection.Cream);
        }
    }
}
