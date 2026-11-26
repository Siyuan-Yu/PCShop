using PCShopSell.Core.Home;
using UnityEngine;
using static PCShopSell.Game.Runtime.PropForms;

namespace PCShopSell.Game.Runtime
{
    internal static class FurniturePropArt
    {
        internal static Material TexturedWood(int district) => Wood(district switch
        {
            1 => "9D7952", 2 => "98704A", 3 => "B2A292", _ => "86623F"
        });

        internal static void Refrigerator(Transform root, FurnitureDef def, bool pick)
        {
            bool compact = def.Id == FurnitureCatalog.FridgeSmall;
            float w = (float)def.Width, d = (float)def.Depth, height = compact ? 1.15f : 1.8f;
            var enamel = Mats.Lit(ArtDirection.Hex(compact ? "E5DFD0" : "C7C7C3"), .16f);
            var door = Mats.Lit(ArtDirection.Hex(compact ? "F0E9DB" : "DDDCD7"), .19f);
            var rubber = Mats.Lit(ArtDirection.Hex("5A5650"), .09f);
            var handle = Mats.Lit(ArtDirection.Hex("686762"), .19f);
            Box(root,"FridgeBody",new Vector3(0,height*.51f,.012f),new Vector3(w,height*.96f,d-.024f),enamel,pick);
            Box(root,"FridgeBase",new Vector3(0,.024f,0),new Vector3(w-.041f,.047f,d-.04f),rubber);
            float lower = compact ? height - .054f : height * .60f;
            DrawDoor(.04f, lower, 0);
            if (!compact) DrawDoor(lower+.016f, height-.020f, 1);
            if (!compact)
                Box(root,"FridgeBadge",new Vector3(w*.255f,height-.117f,-d/2-.020f),new Vector3(.091f,.013f,.005f),handle);

            void DrawDoor(float start, float top, int index)
            {
                float mid=(start+top)*.5f;
                Box(root,"FridgeGasket_"+index,new Vector3(0,mid,-d/2+.009f),new Vector3(w-.028f,top-start,.021f),rubber);
                Box(root,"FridgeDoor_0_"+index,new Vector3(0,mid,-d/2-.004f),new Vector3(w-.012f,top-start-.005f,.031f),door);
                float x = -w * .31f, z = -d/2 - .041f, half = compact ? .138f : index == 0 ? .143f : .102f;
                Tube(root,"FridgeHandle_0_"+index,new Vector3(x,mid-half,z),new Vector3(x,mid+half,z),.019f,handle);
                foreach (int end in new[] { -1, 1 })
                    Tube(root,"HandleMount", new Vector3(x,mid+end*half,-d/2-.018f),
                        new Vector3(x,mid+end*half,z),.020f,handle);
            }
        }

        internal static void Wardrobe(Transform root, float w, float d, bool wooden, bool pick, int district)
        {
            float height = wooden ? 1.9f : 1.7f;
            if (wooden && district >= 2)
            {
                DisplayWardrobe(root, w, d, height, pick, district == 3);
                return;
            }
            Material body = wooden ? (district == 1 ? Pigment("C6C2B3") : TexturedWood(district)) : Pigment(district == 0 ? "55565A" : "B7B3A9");
            Material edge = wooden ? (district == 1 ? Pigment("A5A396") : Wood("674732")) : Pigment(district == 0 ? "434349" : "8C8579");
            Material grip = Pigment(district == 1 && wooden ? "524B43" : wooden ? "A68B66" : "BAB1A0");
            Box(root,"Body",new Vector3(0,height*.50f,.015f),new Vector3(w-.024f,height-.06f,d-.026f),body,pick);
            Box(root,"Top",new Vector3(0,height-.031f,0),new Vector3(w,.061f,d),body);
            Box(root,"Plinth",new Vector3(0,.037f,0),new Vector3(w-.026f,.068f,d-.029f),edge);
            float front=-d*.5f;
            for (int side=-1;side<=1;side+=2)
            {
                float x=side*w*.249f;
                Box(root,"Door",new Vector3(x,height*.51f,front+.004f),new Vector3(w*.482f,height-.121f,.026f),body);
                if (wooden)
                {
                    Box(root,"InsetEdge",new Vector3(x,height*.51f,front-.012f),new Vector3(w*.429f,height-.22f,.013f),edge);
                    Box(root,"InsetPanel",new Vector3(x,height*.51f,front-.019f),new Vector3(w*.405f,height-.264f,.015f),body);
                }
                else
                {
                    Box(root,"ClothSeam",new Vector3(side*w*.457f,height*.51f,front-.015f),
                        new Vector3(.015f,height-.148f,.013f),edge);
                    for (int fold=0;fold<3;fold++)
                        Box(root,"ClothFold",new Vector3(x+(fold-1)*w*.088f,height*.53f,front-.014f),
                            new Vector3(.018f,height*.73f,.011f),fold%2==0?body:edge);
                }
                Tube(root,wooden?"DoorPull":"Zipper",new Vector3(side*.077f,height*.49f,front-.038f),
                    new Vector3(side*.077f,height*.56f,front-.038f),.021f,grip);
            }
            if (!wooden)
                Carton(root,"TopCarton",new Vector3(.074f,height,.025f),new Vector3(w*.56f,.144f,d*.67f),false);
        }

        private static void DisplayWardrobe(Transform root, float w, float d, float h, bool pick, bool garden)
        {
            Material body = garden ? Pigment("CCCAC0") : TexturedWood(2);
            Material lining = garden ? Pigment("656E6D") : Wood("73583F");
            Box(root, "WardrobeBack", 0, .50f*h, d*.46f, w-.04f, h-.065f, .052f, lining);
            Box(root, "WardrobeFloor", 0, .081f,0,w,.121f,d,body);
            Box(root, "WardrobeTop", 0,h-.042f,0,w,.084f,d,body);
            foreach (int side in new[] {-1,1})
                Box(root,"WardrobeSide",side*(w*.5f-.03f),h*.506f,0,.060f,h-.12f,d,body);
            float divide = w * .12f;
            Box(root,"ShelfPartition",divide,h*.496f,0,.036f,h-.167f,d-.055f,body);
            for (int shelf=0;shelf<3;shelf++)
                Box(root,"WardrobeShelf",w*.29f,.325f+shelf*.453f,.012f,w*.326f,.037f,d-.069f,body);
            Tube(root,"HangerRail",new Vector3(-w*.45f,1.52f,.025f),new Vector3(divide,1.52f,.025f),.023f,Pigment("928C7E"));
            for(int hanger=0;hanger<5;hanger++)
            {
                float x=-w*.40f+hanger*w*.093f;
                var cloth=Pigment(hanger%2==0?"AAA28E":"6C7D7A");
                Box(root,"HangingCoat",x,1.12f,.016f,.052f,.67f,d*.51f,cloth);
                Box(root,"CoatHem",x,.806f,.016f,.053f,.018f,d*.52f,lining);
            }
            if (garden)
            {
                Box(root,"ClosetGlazing",0,.514f*h,-d*.46f,w-.112f,h-.166f,.012f,DistrictFinishes.VillaPane(balustrade:true));
                Box(root,"ClosetInsetFrame",divide,.514f*h,-d*.48f,.024f,h-.16f,.024f,body);
                Box(root,"ClosetHandle",w*.359f,1.018f,-d*.512f,.027f,.139f,.034f,Ink);
                Box(root,"ClosetLight",0,h-.092f,-d*.382f,w-.135f,.013f,.015f,
                    Mats.Glow(ArtDirection.Hex("9CB7B8"), new Color(.22f,.57f,.65f),false));
            }
            if (pick)
            {
                var clicks=Group(root,"WardrobePick");
                var bounds=clicks.gameObject.AddComponent<BoxCollider>();
                bounds.center=new Vector3(0,h*.5f,0);
                bounds.size=new Vector3(w-.024f,h-.06f,d-.026f);
            }
        }

        internal static void Sofa(Transform root,float w,float d,bool pick,Color color,int district)
        {
            if (district == 3)
            {
                Material blue = Pigment("A5BAC0");
                Material fabric = Pigment("D0CFBE");
                Prims.Shape(PrimitiveType.Sphere,root,"CloudBase",new Vector3(0,.216f,.047f),
                    new Vector3(w,.427f,d-.037f),blue);
                Prims.Shape(PrimitiveType.Sphere,root,"RoundedSeat",new Vector3(0,.257f,-d*.107f),
                    new Vector3(w*.97f,.386f,d*.79f),fabric);
                foreach (int side in new[] {-1,1})
                    Prims.Shape(PrimitiveType.Sphere,root,"SideBackCushion",new Vector3(side*w*.268f,.465f,d*.185f),
                        new Vector3(w*.40f,.58f,d*.48f),blue);
                Prims.Shape(PrimitiveType.Sphere,root,"CenterBackCushion",new Vector3(0,.528f,d*.229f),
                    new Vector3(w*.40f,.625f,d*.44f),fabric);
                if (pick)
                {
                    var bounds=root.gameObject.AddComponent<BoxCollider>();
                    bounds.center=Vector3.up*.25f;
                    bounds.size=new Vector3(w,.4f,d);
                }
                return;
            }
            var cloth = district switch
            {
                1 => Color.Lerp(color,ArtDirection.Hex("A7937D"),.45f),
                2 => Color.Lerp(color,ArtDirection.Hex("D0C2AA"),.66f),
                _ => color
            };
            var main = Pigment(cloth);
            var shade = Pigment(Color.Lerp(cloth,InkColor,.25f));
            var seam = Pigment(Color.Lerp(cloth,InkColor,.37f));
            Box(root,"Seat",new Vector3(0,.219f,0),new Vector3(w,.322f,d),shade,pick);
            Box(root,"SeatRail",new Vector3(0,.175f,-d/2-.004f),new Vector3(w-.038f,.118f,.021f),main);
            Box(root,"Back",new Vector3(0,.615f,d*.397f),new Vector3(w,.55f,d*.206f),shade)
                .localRotation=Quaternion.Euler(-4,0,0);
            for (int side=-1;side<=1;side+=2)
            {
                Box(root,"Arm",new Vector3(side*(w*.5f-.10f),.443f,0),new Vector3(.191f,.39f,d*.965f),main);
                Box(root,"ArmPiping",new Vector3(side*(w*.5f-.10f),.580f,-d*.489f),new Vector3(.17f,.012f,.013f),seam);
                foreach (int end in new[] { -1, 1 })
                    Box(root,"WalnutFoot",new Vector3(side*w*.412f,.052f,end*d*.382f),new Vector3(.077f,.103f,.090f),Wood("654C36"));
                float cushionW=(w-.401f)*.5f;
                float x=side*(cushionW/2+.010f);
                Box(root,"SeatCushion",new Vector3(x,FurnitureVisuals.SofaSeatHeight-.047f,-d*.083f),
                    new Vector3(cushionW,.094f,d*.683f),main);
                Box(root,"CushionPiping",new Vector3(x,FurnitureVisuals.SofaSeatHeight-.069f,-d*.428f),
                    new Vector3(cushionW-.032f,.013f,.014f),seam);
                Box(root,"BackCushion",new Vector3(x,.672f,d*.292f),new Vector3(cushionW,.351f,d*.16f),main)
                    .localRotation=Quaternion.Euler(-8,0,0);
            }
        }

        internal static Transform Carton(Transform root,string name,Vector3 at,Vector3 size,bool pick)
        {
            var carton=Group(root,name,at);
            float w=size.x,h=size.y,d=size.z;
            var cardboard=Pigment("AE8D5E");
            var flap=Pigment("B99A6C");
            var tape=Pigment("D2B889");
            var ink=Pigment("755B3C");
            Box(carton,"Box",new Vector3(0,h*.5f,0),size,cardboard,pick);
            for(int sign=-1;sign<=1;sign+=2)
            {
                Box(carton,"TopFlap",new Vector3(sign*w*.254f,h+.001f,0),new Vector3(w*.483f,.010f,d*.989f),flap);
                Box(carton,"FoldEdge",new Vector3(sign*w*.47f,h*.2f,-d*.503f),new Vector3(w*.020f,h*.35f,.006f),ink);
            }
            Box(carton,"Tape",new Vector3(0,h+.008f,0),new Vector3(w*.15f,.007f,d),tape);
            Box(carton,"TapeTab",new Vector3(0,h*.91f,-d*.502f),new Vector3(w*.143f,h*.18f,.007f),tape);
            Box(carton,"ShippingLabel",new Vector3(w*.18f,h*.51f,-d*.507f),new Vector3(w*.35f,h*.24f,.006f),Cream);
            for(int i=0;i<6;i++)
                Box(carton,"LabelBars",new Vector3(w*(.084f+.033f*i),h*.503f,-d*.515f),
                    new Vector3(w*(i%3==0?.022f:.011f),h*.088f,.004f),ink);
            return carton;
        }

        internal static void AirConditioner(Transform root,float w)
        {
            var enamel=Mats.Lit(ArtDirection.Hex("E2DDD0"),.17f);
            Box(root,"Body",new Vector3(0,2.4f,-.145f),new Vector3(w,.32f,.255f),enamel);
            Box(root,"FrontCover",new Vector3(0,2.436f,-.273f),new Vector3(w*.963f,.197f,.044f),Cream);
            Box(root,"LouvreWell",new Vector3(0,2.316f,-.276f),new Vector3(w*.894f,.071f,.065f),Ink);
            for(int blade=0;blade<3;blade++)
                Box(root,"Louvre",new Vector3(0,2.293f+blade*.020f,-.294f),new Vector3(w*.868f,.012f,.048f),enamel)
                    .localRotation=Quaternion.Euler(16,0,0);
            Box(root,"Led",new Vector3(w*.37f,2.437f,-.298f),new Vector3(.046f,.018f,.008f),
                Mats.Glow(ArtDirection.Hex("648F66"),new Color(.32f,.63f,.34f),false));
            Box(root,"DisplayInset",new Vector3(w*.293f,2.437f,-.298f),new Vector3(.063f,.018f,.008f),Pigment("ABA99F"));
        }

        internal static void Drapes(Transform root)
        {
            var metal=Pigment("766B5C");
            Tube(root,"Rod",new Vector3(-.74f,2.46f,-.081f),new Vector3(.74f,2.46f,-.081f),.034f,metal);
            var lining=Pigment("D9C9AC");
            var cloth=Pigment("875A63");
            void Pleats(string name,float middle,float span, float high, Material mat)
            {
                var panel=Mesh(root,name,"furniture:curtain:"+name,mat,() =>
                {
                    const int n=16;
                    var faces=new Faces();
                    Vector3 Point(int i,float y)
                    {
                        float u=i/(float)n;
                        return new Vector3((u-.5f)*span*(y==0?.92f:1.03f),-y*high,
                            Mathf.Cos(u*Mathf.PI*8)*(.015f+y*.007f));
                    }
                    for (int i=0;i<n;i++)
                    {
                        var a=Point(i,0);var b=Point(i+1,0);
                        var c=Point(i+1,1);var d=Point(i,1);
                        faces.Face(a,b,c,d);faces.Face(d,c,b,a);
                    }
                    return faces.Build();
                });
                panel.localPosition=new Vector3(middle,2.423f,name=="Sheer"?-.071f:-.097f);
            }
            Pleats("Sheer",0,1.08f,1.3f,lining);
            Pleats("Drape",-0.572f,.32f,1.43f,cloth);
            Pleats("Drape",0.572f,.32f,1.43f,cloth);
        }

        internal static void Plant(Transform root,float size,float height,bool pick,bool loose)
        {
            var clay=Pigment("A56747");var rim=Pigment("7B5038");var soil=Pigment("5B4534");
            float potHeight = size*.62f;
            Revolve(root,"陶盆",clay,"陶盆",Vector3.zero,new Vector3(size,potHeight,size),
                new Vector2(0,0), new Vector2(.315f,0),new Vector2(.34f,.09f),new Vector2(.474f,.83f),
                new Vector2(.487f,.94f),new Vector2(.465f,1),new Vector2(.352f,1),new Vector2(.35f,.85f),new Vector2(0,.85f));
            Tube(root,"PotRim",Vector3.up*potHeight*.86f,Vector3.up*potHeight*.945f,size*.968f,rim);
            Tube(root,"Soil",Vector3.up*potHeight*.95f,Vector3.up*potHeight*.97f,size*.792f,soil);
            if(pick)
            {
                var hit=root.gameObject.AddComponent<BoxCollider>();
                hit.center=Vector3.up*potHeight/2f;hit.size=new Vector3(size,potHeight,size);
            }
            var greens=new[]{Pigment("4D613D"),Pigment("68834B"),Pigment("79915A")};
            var stem=Pigment("77613C");
            Vector3 start=Vector3.up*(potHeight*.95f);
            Tube(root,"Stem",start,Vector3.up*height*.86f,size*.071f,stem);
            if (!loose)
            {
                var detail=new MiniatureDetailMesh();
                detail.Lobe(Vector3.up*height*.568f,new Vector3(size*.44f,height*.236f,size*.43f),greens[1],49);
                detail.Lobe(new Vector3(size*.11f,height*.84f,.01f),new Vector3(size*.269f,height*.168f,size*.28f),greens[2],27);
                detail.Build(root,"圆叶盆栽");
                return;
            }
            var foliage=new MiniatureDetailMesh();
            for(int i=0;i<11;i++)
            {
                float t=i/11f, angle=i*2.40f;
                var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 from=Vector3.up*Mathf.Lerp(potHeight*1.68f,height*.84f,t);
                Vector3 endpoint=from+dir*size*Mathf.Lerp(.65f,.39f,t)+Vector3.up*height*.13f;
                Tube(root,"LeafStem",from,endpoint,.014f,stem);
                var leaf=foliage.Part(greens[i%greens.Length]);
                AddLeaf(leaf,from+dir*size*.15f+Vector3.up*height*.07f,endpoint,dir,size*(.21f-.061f*t));
            }
            foliage.Build(root,"分层的店铺绿叶");
        }

        private static void AddLeaf(MiniatureDetailMesh.Buffer mesh,Vector3 from, Vector3 tip,Vector3 facing,float width)
        {
            Vector3 mid=Vector3.Lerp(from,tip,.56f);
            Vector3 side=Vector3.Cross(facing,Vector3.up)*width;
            int n=mesh.V.Count;
            mesh.V.AddRange(new[]{from,mid+side,tip,mid-side,mid+Vector3.up*.023f});
            mesh.T.AddRange(new[]{n,n+1,n+4,n+1,n+2,n+4,n+2,n+3,n+4,n+3,n,n+4,
                n,n+4,n+1,n+1,n+4,n+2,n+2,n+4,n+3,n+3,n+4,n});
        }

        internal static void Dispenser(Transform root,bool pick)
        {
            var enamel=Mats.Lit(ArtDirection.Hex("DDD9CE"),.17f);
            var shadow=Pigment("848177");
            Box(root,"Body",new Vector3(0,.498f,0),new Vector3(.36f,.977f,.36f),enamel,pick);
            Box(root,"Recess",new Vector3(0,.62f,-.179f),new Vector3(.249f,.286f,.009f),shadow);
            Box(root,"DripTray",new Vector3(0,.493f,-.181f),new Vector3(.25f,.035f,.093f),enamel);
            for(int side=-1;side<=1;side+=2)
            {
                var tap=Pigment(side<0?"AA5148":"4D7193");
                Box(root,"Tap",new Vector3(side*.064f,.72f,-.204f),new Vector3(.052f,.045f,.045f),tap);
                Tube(root,"Spout",new Vector3(side*.064f,.719f,-.225f),new Vector3(side*.064f,.676f,-.225f),.023f,shadow);
            }
            Revolve(root,"蓝水桶",Pigment("578FB5"),"饮水机水桶",new Vector3(0,.991f,0),
                new Vector3(.32f,.465f,.32f),new Vector2(0,0),new Vector2(.23f,0),new Vector2(.25f,.08f),
                new Vector2(.448f,.21f),new Vector2(.486f,.36f),new Vector2(.486f,.86f),new Vector2(.426f,.96f),
                new Vector2(.36f,1),new Vector2(0,1));
            for(int ring=0;ring<3;ring++)
                Tube(root,"BottleBand",Vector3.up*(1.165f+.090f*ring),Vector3.up*(1.190f+.090f*ring),.314f,Pigment("6399B9"));
        }
    }
}
