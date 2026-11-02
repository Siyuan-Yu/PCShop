using System;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Exterior shells share the real activity entrance; entering reveals the existing furnished space.
    public static partial class CityArchitecture
    {
        private static GameObject Box(Transform p,string name,Vector3 at,Vector3 size,Color color,bool solid=false)
            =>Prims.Box(p,name,at,size,Mats.Lit(color),solid);
        private static void Gables(Transform root,float w,float d,float h,float rise,Color wall)
        {
            foreach(int side in new[]{-1,1})
            {
                var go=new GameObject("GableEnd",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                float z=side<0?-.06f:d+.06f;
                var mesh=new Mesh{name="GableEnd"};mesh.vertices=new[]{new Vector3(-w/2,h,z),new Vector3(0,h+rise,z),new Vector3(w/2,h,z)};
                mesh.triangles=side<0?new[]{0,1,2}:new[]{2,1,0};mesh.RecalculateNormals();mesh.RecalculateBounds();
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=Mats.Lit(wall);
            }
        }
        public static float VenueHeight(Place p)=>p.Business=="venue_expo"?9:p.Business=="服装"?5.5f:p.Business=="venue_ktv"?7+p.Tier*2:p.Business=="venue_bar"?5+p.Tier*1.5f:5.5f+p.Tier*1.7f;
        public static bool HasExterior(Place p)=>p.Business=="服装"||p.Business=="venue_restaurant"||p.Business=="venue_ktv"||p.Business=="venue_bar"||p.Business=="venue_expo";
        public static void Venue(Transform root,Place p,float w,float d)
        {
            float h=VenueHeight(p);string kind=p.Business??"";
            Color wall=p.Tier==3?ArtDirection.Cream:p.Tier==2?ArtDirection.Blue:ArtDirection.Hex("D4C5AA");
            Color accent=p.Model=="warehouse"?ArtDirection.Blue:kind=="venue_ktv"?ArtDirection.Hex("776088"):kind=="venue_bar"?ArtDirection.Hex("394B50"):kind=="venue_expo"?ArtDirection.Blue:ArtDirection.Clay;
            Box(root,"HallBack",new Vector3(0,h/2,d-.2f),new Vector3(w,h,.4f),wall,true);
            foreach(int side in new[]{-1,1})Box(root,"HallSide",new Vector3(side*(w/2-.2f),h/2,d/2),new Vector3(.4f,h,d),wall,true);
            // A genuine frontage, with an open central doorway, rather than an empty room and sign.
            float door=2.6f,wing=(w-door)/2;
            foreach(int side in new[]{-1,1})
                Box(root,"StreetFacade",new Vector3(side*(door/2+wing/2),h/2,0),new Vector3(wing,h,.35f),wall,true);
            Box(root,"DoorLintel",new Vector3(0,(h+2.8f)/2,0),new Vector3(door,h-2.8f,.35f),wall);
            Box(root,"EntranceGlass",new Vector3(0,1.35f,-.03f),new Vector3(2.2f,2.7f,.08f),ArtDirection.Hex("42646A"));
            Box(root,"RoofSlab",new Vector3(0,h+.12f,d/2),new Vector3(w+.35f,.25f,d+.35f),accent);
            // Wrap the shell in architectural detail on every visible elevation.
            for(int elevation=0;elevation<4;elevation++)
            {
                var facade=new GameObject("VenueElevation").transform;facade.SetParent(root,false);
                facade.localPosition=new Vector3(0,0,d/2);facade.localRotation=Quaternion.Euler(0,elevation*90,0);
                float span=elevation%2==0?w:d,deep=elevation%2==0?d:w;
                for(float x=-span/2+1.4f;x<span/2-1;x+=2.8f)
                {
                    if(elevation==0&&Mathf.Abs(x)<2)continue;
                    for(float y=5.1f;y<h-.8f;y+=2.7f)
                        VillaWindow(facade,new Vector3(x,y,-deep/2-.24f),1.55f,1.65f);
                }
                Box(facade,"UpperCornice",new Vector3(0,h-.25f,-deep/2-.12f),new Vector3(span+.25f,.28f,.38f),Limestone);
                Box(facade,"RoofParapet",new Vector3(0,h+.46f,-deep/2),new Vector3(span,.65f,.24f),Limestone);
                foreach(int edge in new[]{-1,1})Box(facade,"CornerPilaster",new Vector3(edge*(span/2-.25f),h/2,-deep/2-.18f),new Vector3(.4f,h,.35f),Limestone);
            }
            if(kind!="venue_expo"&&p.Model!="warehouse")
            {
                foreach(int side in new[]{-1,1})
                {
                    Box(root,"RoofTerracePlanter",new Vector3(side*w*.33f,h+.45f,d*.48f),new Vector3(w*.18f,.55f,d*.50f),Hedge);
                    Box(root,"EntrancePlanter",new Vector3(side*(w/2-1),.4f,-.6f),new Vector3(1.2f,.8f,1),Limestone);
                    Prims.Shape(PrimitiveType.Sphere,root,"EntranceTopiary",new Vector3(side*(w/2-1),1.1f,-.6f),Vector3.one*1.1f,Mats.Lit(Hedge));
                }
                if(kind=="venue_restaurant")HipRoof(root,new Vector3(0,h+.2f,d*.56f),w*.79f,d*.69f,1.6f,accent);
            }
            Box(root,"SignBand",new Vector3(0,3.7f,-.24f),new Vector3(w-.6f,1.15f,.22f),accent);
            Prims.SignText(root,p.Name,new Vector3(0,3.7f,-.37f),w-1,.85f,ArtDirection.Cream);
            string category=p.Model=="warehouse"?"服装批发":kind=="venue_ktv"?"KTV":kind=="venue_bar"?"酒吧":kind=="venue_expo"?"会展中心":kind=="服装"?"原创服饰":"餐厅";
            Prims.SignText(root,category,new Vector3(w*.27f,h-.9f,-.24f),w*.4f,1.25f,ArtDirection.Cream);
            for(int floor=0;floor<Math.Max(1,(int)((h-4)/2.7f));floor++)
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*w*.29f,y=1.6f+floor*2.7f+(floor>0?2:0);
                if(y>h-.8f)continue;
                Box(root,"FrontWindow",new Vector3(x,y,-.21f),new Vector3(w*.23f,1.5f,.09f),ArtDirection.Hex("4D7079"));
                Box(root,"WindowSill",new Vector3(x,y-.8f,-.25f),new Vector3(w*.25f,.12f,.18f),ArtDirection.Cream);
            }
            Box(root,"DoorCanopy",new Vector3(0,2.9f,-.65f),new Vector3(4,.22f,1.3f),p.Tier>1?ArtDirection.Brass:accent);
            if(p.Model=="warehouse")
            {
                foreach(int side in new[]{-1,1})
                {
                    var roof=Box(root,"WholesaleRoof",new Vector3(side*w*.25f,h+.5f,d/2),new Vector3(w*.53f,.25f,d+.4f),ArtDirection.Blue);
                    roof.transform.localRotation=Quaternion.Euler(0,0,-side*9);
                }
            }
            else if(kind=="venue_ktv")
            {
                foreach(int side in new[]{-1,1})
                {
                    Box(root,"KaraokeLightTower",new Vector3(side*(w/2-.55f),h/2,-.3f),new Vector3(.4f,h,.2f),ArtDirection.Hex("B584B9"));
                    Box(root,"KaraokeRoofTower",new Vector3(side*w*.35f,h+.9f,d*.75f),new Vector3(w*.18f,1.5f,d*.3f),accent);
                }
                Prims.SignText(root,"K T V",new Vector3(0,h-.9f,-.25f),w*.65f,1.6f,ArtDirection.Cream);
            }
            else if(kind=="venue_restaurant")
            {
                for(int i=0;i<8;i++)Box(root,"RestaurantAwning",new Vector3((i-3.5f)*w/9,2.8f,-.65f),new Vector3(w/9,.12f,1.3f),i%2==0?accent:ArtDirection.Cream);
            }
            else if(kind=="venue_bar")
            {
                Box(root,"BarVerticalSign",new Vector3(-w*.39f,h*.62f,-.45f),new Vector3(.9f,2.5f,.2f),ArtDirection.Brass);
                Prims.SignText(root,"BAR",new Vector3(-w*.39f,h*.62f,-.58f),.85f,1.9f,ArtDirection.Cream);
            }
            else if(kind=="venue_expo")
            {
                Gables(root,w,d,h,.8f+w*.046f,wall);
                foreach(int side in new[]{-1,1})
                {
                    var roof=Box(root,"ExpoPitchedRoof",new Vector3(side*w*.25f,h+.8f,d/2),new Vector3(w*.53f,.3f,d+.4f),ArtDirection.Blue);
                    roof.transform.localRotation=Quaternion.Euler(0,0,-side*10);
                }
                for(int i=-2;i<=2;i++)Box(root,"ExpoPortico",new Vector3(i*w*.17f,2.1f,-.4f),new Vector3(.28f,4.2f,.28f),ArtDirection.Cream);
            }
        }
        public static void Residence(Transform root,string model,int tier,float w,float d,float h,int seed)
        {
            if(model!="oldhome"&&model!="apartment"&&model!="luxury_apartment"&&model!="villa")return;
            if(model=="villa")
            {
                Gables(root,w,d,h,1+w*.11f,ArtDirection.Cream);
                foreach(int side in new[]{-1,1})
                {
                    Box(root,"VillaPorchColumn",new Vector3(side*1.2f,1.25f,-.25f),new Vector3(.2f,2.5f,.2f),ArtDirection.Cream);
                    Box(root,"VillaGardenGate",new Vector3(side*(w*.3f),.5f,-.55f),new Vector3(w*.3f,1,.12f),ArtDirection.Cream);
                }
                Box(root,"VillaChimney",new Vector3(w*.3f,h+1,d*.7f),new Vector3(.7f,2,.7f),ArtDirection.Clay);
                return;
            }
            for(float y=4;y<h-1;y+=3)
            {
                float bw=w*(model=="luxury_apartment"?.78f:.48f);
                Box(root,"BalconyFloor",new Vector3(0,y,-.25f),new Vector3(bw,.18f,.6f),ArtDirection.Cream);
                Box(root,"BalconyRail",new Vector3(0,y+.45f,-.5f),new Vector3(bw,.8f,.08f),tier>1?ArtDirection.Blue:ArtDirection.Ink);
                if(model=="oldhome")Box(root,"WallAircon",new Vector3(w*.38f,y+.5f,-.25f),new Vector3(.8f,.5f,.35f),ArtDirection.Cream);
            }
            if(model=="luxury_apartment")
                Box(root,"ApartmentCrown",new Vector3(0,h+.65f,d*.6f),new Vector3(w*.8f,1,d*.65f),ArtDirection.Cream);
            if(model=="oldhome")
            {
                Box(root,"ExternalPipe",new Vector3(-w*.44f,h/2,-.22f),new Vector3(.1f,h,.1f),ArtDirection.Ink);
                Box(root,"RoofWaterTank",new Vector3(-w*.23f,h+.6f,d*.7f),new Vector3(.9f,1.2f,1),ArtDirection.Hex("7F8178"));
            }
        }
    }
}
