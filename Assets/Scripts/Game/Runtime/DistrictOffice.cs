using System.Collections.Generic;
using UnityEngine;
using static PCShopSell.Game.Runtime.DistrictRoomArt;

namespace PCShopSell.Game.Runtime
{
    internal static class DistrictOffice
    {
        private enum Use { Reception, Desk, Meeting, Lounge, Divider }
        private readonly struct Zone
        {
            public readonly Use Use;
            public readonly Rect Rect;
            public Zone(Use use,float x,float z,float width,float depth)
            {
                Use=use;Rect=new Rect(x-width/2,z-depth/2,width,depth);
            }
        }
        // All dimensions are relative to the entrance centre, shared by rendering and walking.
        internal static Vector2 ReceptionCenter(float width,float depth) => new Vector2(-width*.25f,depth*.23f);
        internal static bool AtReception(Vector3 local,float width,float depth)
        {
            var center=ReceptionCenter(width,depth);
            return Mathf.Abs(local.x-center.x)<=width*.13f+.3f
                && local.z>=center.y-1.75f && local.z<=center.y-1.05f*.5f-.19f;
        }

        private static IEnumerable<Zone> Layout(float w,float d)
        {
            var reception=ReceptionCenter(w,d);
            yield return new Zone(Use.Reception,reception.x,reception.y,w*.26f,1.05f);
            yield return new Zone(Use.Lounge,w*.30f,d*.18f,w*.22f,1.35f);
            for(int i=0;i<2;i++)
                yield return new Zone(Use.Desk,-w*.26f,d*(.54f+i*.25f),w*.32f,2.32f);
            yield return new Zone(Use.Meeting,w*.24f,d*.73f,w*.28f,2.68f);
            yield return new Zone(Use.Divider,w*.04f,d*.75f,.09f,d*.45f);
            yield return new Zone(Use.Divider,w*.35f,d*.525f,w*.28f,.09f);
        }
        internal static List<Rect> WalkingObstacles(float w,float d)
        {
            var list=new List<Rect>();
            foreach(var zone in Layout(w,d))
            {
                var rect=zone.Rect;
                list.Add(Rect.MinMaxRect(rect.xMin-.19f,rect.yMin-.19f,rect.xMax+.19f,rect.yMax+.19f));
            }
            return list;
        }

        internal static void Exterior(Transform r,float w,float d,bool garden)
        {
            int floors=garden?16:10;
            const float level=2.85f;
            float h=level*floors;
            var facade=new MiniatureDetailMesh();
            var stone=Finish(garden?"888B88":"D6D1C3",.34f);
            var cap=Finish(garden?"A39A89":"A9AAA2",.42f,.22f);
            var mullion=Finish(garden?"867F70":"636F70",.49f,.33f);
            var glazing=Finish(garden?"7E9DA4":"8CA5A8",.52f,.19f);
            var windowShade=Finish(garden?"749098":"81989A",.48f,.18f);
            void Mass(float x,float y,float z,float width,float height,float depth,Material m) =>
                facade.Box(new Vector3(x,y,z),new Vector3(width,height,depth),m);
            Mass(0,h/2,d/2,w-.22f,h,d-.22f,mullion);
            for(int i=0;i<floors;i++)
            {
                float y=level*(i+.5f);
                Mass(0,y,d/2,w,level-.24f,d,i%4==0?windowShade:glazing);
                Mass(0,y+.60f,d/2,w+.035f,.072f,d+.035f,cap);
                Mass(0,level*i+.12f,d/2,w+.13f,garden?.16f:.50f,d+.13f,stone);
            }
            for(int i=0;i<=Mathf.CeilToInt(w/1.7f);i++)
            {
                float x=-w/2+w*i/Mathf.CeilToInt(w/1.7f);
                Mass(x,h/2,d/2,garden?.10f:.075f,h,d+.09f,mullion);
            }
            for(int i=0;i<=Mathf.CeilToInt(d/1.6f);i++)
                Mass(0,h/2,d*i/Mathf.CeilToInt(d/1.6f),w+.08f,h,.083f,mullion);
            Mass(0,h+.10f,d/2,w+.22f,.25f,d+.22f,cap);
            Mass(0,h+.54f,d*.58f,w*.50f,.89f,d*.41f,stone);
            for(int i=0;i<3;i++) Mass(-w*.23f+i*w*.20f,h+1.14f,d*.60f,1.38f,.45f,1.12f,mullion);
            Mass(0,3.0f,-.83f,w*.42f,.19f,2.0f,cap);
            for(int i=-1;i<=1;i+=2) Mass(i*1.55f,1.4f,-.15f,.14f,2.8f,.22f,cap);
            Mass(0,1.30f,-.10f,3.00f,2.60f,.13f,Finish("586C70",.62f,.19f));
            Mass(0,1.32f,-.19f,.04f,2.64f,.03f,cap);
            if(!garden)
            {
                for(int j=0;j<4;j++)
                {
                    float x=-w*.37f+j*w*.235f;
                    Mass(x,h+.27f,d*.21f,w*.18f,.40f,1.2f,stone);
                    Mass(x,h+.62f,d*.21f,w*.16f,.42f,.95f,Finish("728657"));
                }
            }
            facade.Build(r,"塔楼幕墙与楼板");
            var click=r.gameObject.AddComponent<BoxCollider>();
            click.center=new Vector3(0,h*.5f,d*.5f);click.size=new Vector3(w,h,d);
        }

        internal static void Interior(Transform root,float w,float d,int district)
        {
            bool garden=district==3;
            var corner=Group(root,"办公大堂",new Vector3(-w/2,0,0));
            DistrictRoomArt.Build(corner,w,d,district,false,true);
            var ivory=Finish("DDD4C3",.45f);
            var timber=DistrictFinishes.Tile("walnut",Vector2.one);
            var metal=Finish(garden?"9E8C70":"4F5957",.36f,.35f);
            var fabric=Finish(garden?"B9B0A0":"84908A");
            foreach(var zone in Layout(w,d))
            {
                var b=zone.Rect;float x=b.center.x,z=b.center.y,bw=b.width,bd=b.height;
                var t=Group(root,zone.Use.ToString(),new Vector3(x,0,z));
                switch(zone.Use)
                {
                    case Use.Reception:
                        Box(t,"接待台",0,.53f,0,bw,1.04f,bd,garden?timber:ivory);
                        Box(t,"台面",0,1.07f,0,bw+.08f,.08f,bd+.05f,ivory);
                        Box(t,"柜脚灯槽",0,.11f,-bd/2-.012f,bw-.18f,.036f,.028f,Warm);
                        Screen(t,0,1.12f,.05f,metal);break;
                    case Use.Desk:
                        Table(t,bw,1.04f,.77f,garden?ivory:timber,metal);
                        for(int i=-1;i<=1;i+=2)
                        {
                            Screen(t,i*bw*.25f,.81f,.12f,metal);
                            Chair(t,i*bw*.25f,-.78f,0,metal,fabric);
                            Chair(t,i*bw*.25f,.78f,180,metal,fabric);
                        }
                        break;
                    case Use.Meeting:
                        Table(t,bw-.5f,1.2f,.77f,timber,metal);
                        for(int i=0;i<3;i++)
                        {
                            float cx=(i-1)*bw*.30f;
                            Chair(t,cx,-.98f,0,metal,fabric);Chair(t,cx,.98f,180,metal,fabric);
                        }
                        break;
                    case Use.Divider:
                        Box(t,"隔断玻璃",0,1.36f,0,bw,2.69f,bd,DistrictFinishes.VillaPane(true));
                        Box(t,"上槛",0,2.74f,0,bw+.025f,.06f,bd+.025f,metal);
                        Box(t,"下槛",0,.05f,0,bw+.025f,.09f,bd+.025f,metal);
                        for(int i=0;i<=3;i++)
                            Box(t,"拼缝",(bw>.2f?bw:0)*(i/3f-.5f),1.37f,(bd>.2f?bd:0)*(i/3f-.5f),.044f,2.7f,.044f,metal);
                        break;
                    case Use.Lounge:
                        Box(t,"接待沙发",0,.34f,0,bw,.46f,.8f,fabric);
                        Box(t,"靠背",0,.65f,.31f,bw,.45f,.19f,fabric);
                        foreach(int sign in new[]{-1,1}) Box(t,"扶手",sign*(bw*.5f-.10f),.53f,0,.18f,.35f,.78f,fabric);
                        break;
                }
            }
            Plant(root,new Vector3(-w*.44f,0,d*.18f),1.45f);
            Plant(root,new Vector3(w*.42f,0,d*.93f),1.6f);
            Plant(root,new Vector3(-w*.44f,0,d*.93f),1.4f);
            var jamb=Finish("7F837D",.42f,.26f);
            foreach(float x in new[]{-1.02f,1.02f}) Box(root,"入口门框",x,1.24f,0,.072f,2.48f,.10f,jamb);
            Box(root,"门楣",0,2.5f,0,2.12f,.085f,.10f,jamb);
        }

        internal static void Chair(Transform r,float x,float z,float yaw,Material frame,Material seat)
        {
            var c=Group(r,"椅子",new Vector3(x,0,z),yaw);
            Box(c,"坐垫",0,.45f,0,.46f,.105f,.44f,seat);
            Box(c,"靠背",0,.70f,-.19f,.46f,.40f,.085f,seat);
            for(int i=-1;i<=1;i+=2)for(int j=-1;j<=1;j+=2)
                Box(c,"椅脚",i*.175f,.215f,j*.16f,.045f,.43f,.045f,frame);
        }
        internal static void Table(Transform r,float w,float d,float h,Material top,Material legs)
        {
            Box(r,"桌板",0,h,0,w,.085f,d,top);
            for(int i=-1;i<=1;i+=2)for(int j=-1;j<=1;j+=2)
                Box(r,"桌脚",i*(w/2-.12f),h/2,j*(d/2-.12f),.065f,h,.065f,legs);
        }
        private static void Screen(Transform r,float x,float y,float z,Material metal)
        {
            Box(r,"屏幕底座",x,y+.025f,z,.34f,.05f,.22f,metal);
            Box(r,"支臂",x,y+.15f,z,.065f,.25f,.06f,metal);
            Box(r,"显示器",x,y+.36f,z,.61f,.35f,.055f,metal);
            Box(r,"屏面",x,y+.36f,z-.035f,.56f,.30f,.012f,Finish("708F99",.47f));
            Box(r,"键盘",x,y+.012f,z-.20f,.42f,.026f,.14f,metal);
        }
    }
}
