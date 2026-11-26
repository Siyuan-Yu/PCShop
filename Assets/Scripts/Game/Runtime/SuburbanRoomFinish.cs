using System.Collections.Generic;
using UnityEngine;
using static PCShopSell.Game.Runtime.DistrictRoomArt;

namespace PCShopSell.Game.Runtime
{
    internal static class SuburbanRoomFinish
    {
        internal static Material Floor(float w,float d,bool home)
        {
            float span=home?5.8f:6.15f;
            return DistrictFinishes.Tile(home?"suburbCement":"suburbShopTile",new Vector2(w/span,d/span),ArtDirection.Hex(home?"D9D7D5":"FAF5F0"));
        }

        // Use wall coordinates for the two paint coats, so the plaster continues across a window.
        private static void Coat(Transform wall,string key,float x0,float x1,float bottom,float top,float repeat,float bandBottom,float bandHeight)
        {
            if(x1<=x0 || top<=bottom) return;
            var material=DistrictFinishes.Tile(key,Vector2.one,ArtDirection.Hex(key=="suburbPlaster"?"DCE0EB":"FFFCF1"));
            var panel=Prims.Shape(PrimitiveType.Quad,wall,key,new Vector3((x0+x1)*.5f,(bottom+top)*.5f,-.034f),
                new Vector3(x1-x0,top-bottom,1),material);
            var block=new MaterialPropertyBlock();
            block.SetVector("_BaseMap_ST",new Vector4((x1-x0)/repeat,(top-bottom)/bandHeight,x0/repeat,(bottom-bandBottom)/bandHeight));
            var renderer=panel.GetComponent<Renderer>();renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        internal static void Wall(Transform wall,float length,bool home,bool window=false)
        {
            float band=home?1.05f:.93f;
            Coat(wall,"suburbEnamel",0,length,0,band,4.8f,0,band);
            void Plaster(float x0,float x1,float y0,float y1) =>
                Coat(wall,"suburbPlaster",x0,x1,y0,y1,4.75f,band,3-band);
            if(window)
            {
                Plaster(0,2.56f,band,3);Plaster(3.74f,length,band,3);
                Plaster(2.56f,3.74f,band,1.22f);Plaster(2.56f,3.74f,2.28f,3);
            }
            else Plaster(0,length,band,3);
            Box(wall,"水泥墙基",length*.5f,.054f,-.057f,length,.106f,.077f,Finish("756F5D",.085f));
            Box(wall,"上沿旧压条",length*.5f,3.035f,.04f,length+.055f,.088f,.18f,Finish("92846E",.15f));
        }

        private static GameObject Rod(Transform r,string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            var v=b-a;
            var go=Prims.Shape(PrimitiveType.Cylinder,r,name,(a+b)*.5f,new Vector3(radius*2,v.magnitude*.5f,radius*2),material);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,v.normalized);
            return go;
        }
        internal static void Electrical(Transform wall,float length,bool withFan)
        {
            var tube=Finish("746F60",.32f,.28f);
            const float high=2.82f,offset=-.081f;
            Rod(wall,"外露穿线管",new Vector3(.11f,high,offset),new Vector3(length-.10f,high,offset),.020f,tube);
            for(float x=.43f;x<length-.25f;x+=1.32f)
                Box(wall,"管卡",x,high,offset,.034f,.074f,.062f,Finish("ADA18B",.35f,.4f));
            float junction=length-.38f;
            Rod(wall,"开关落管",new Vector3(junction,high,offset),new Vector3(junction,1.30f,offset),.021f,tube);
            Box(wall,"接线盒",junction,high,offset,.14f,.13f,.088f,tube);
            Box(wall,"瓷底开关",junction,1.29f,offset-.015f,.11f,.15f,.075f,Finish("BBAE92",.19f));
            if(!withFan)return;
            int lamps=Mathf.Clamp(Mathf.RoundToInt(length/4.5f),2,4);
            float cx=length*(lamps/2)/(float)lamps;
            var fan=Group(wall,"旧墙排风扇",new Vector3(cx,2.31f,-.105f));
            var iron=Finish("625B4B",.28f,.32f);
            var dark=Finish("48483E",.22f);
            Rod(fan,"风道口",new Vector3(0,0,.026f),new Vector3(0,0,-.09f),.315f,dark);
            for(int i=0;i<5;i++)
            {
                float angle=i*72f+.3f;
                var hub=Group(fan,"铝叶",new Vector3(0,0,-.102f));
                hub.localRotation=Quaternion.Euler(0,0,angle);
                var blade=Prims.Shape(PrimitiveType.Sphere,hub,"扇叶",new Vector3(.12f,.078f,0),new Vector3(.27f,.13f,.045f),Finish("8C8571",.37f,.36f));
                blade.transform.localRotation=Quaternion.Euler(0,18,28);
            }
            // A single mesh for the two concentric guards, eliminating dozens of separate renderers.
            var grille=new MiniatureDetailMesh();
            void Wire(Vector3 a,Vector3 b,float radius)
            {
                Vector3 delta=b-a;
                grille.Box((a+b)*.5f,new Vector3(radius*2,delta.magnitude,radius*2),iron,
                    Quaternion.FromToRotation(Vector3.up,delta.normalized));
            }
            for(int ring=1;ring<=3;ring++)
            {
                float rad=ring*.10f;
                for(int j=0;j<32;j++)
                {
                    float a=Mathf.PI*j/16,b=Mathf.PI*(j+1)/16;
                    Wire(new Vector3(Mathf.Cos(a)*rad,Mathf.Sin(a)*rad,-.155f),
                        new Vector3(Mathf.Cos(b)*rad,Mathf.Sin(b)*rad,-.155f),ring==3?.013f:.0065f);
                }
            }
            for(int j=0;j<6;j++)
            {
                float a=Mathf.PI*j/3;
                Wire(new Vector3(0,0,-.165f),new Vector3(.30f*Mathf.Cos(a),.30f*Mathf.Sin(a),-.165f),.007f);
            }
            grille.Build(fan,"铁丝护罩");
            Rod(fan,"风扇轴盖",new Vector3(0,0,-.14f),new Vector3(0,0,-.17f),.047f,iron);
            Rod(wall,"排风扇供电管",new Vector3(cx+.33f,2.31f,offset),new Vector3(cx+.33f,high,offset),.014f,tube);
        }

        internal static void Lamps(Transform wall,float length,List<GameObject> meshes,List<Renderer> diffusers,List<Light> lights,Material lit)
        {
            int count=Mathf.Clamp(Mathf.RoundToInt(length/4.5f),2,4);
            var enamel=Finish("898B78",.26f,.16f);
            for(int i=0;i<count;i++)
            {
                float x=length*(i+.5f)/count;
                var mount=Group(wall,"搪瓷双管灯",new Vector3(x,2.51f,-.13f));
                meshes.Add(Box(mount,"搪瓷托槽",0,0,0,1.04f,.14f,.16f,enamel));
                foreach(float end in new[]{-.48f,.48f})
                    meshes.Add(Box(mount,"灯脚",end,-.01f,-.126f,.075f,.13f,.23f,enamel));
                foreach(float z in new[]{-.143f,-.208f})
                {
                    var tube=Rod(mount,"日光灯管",new Vector3(-.46f,-.02f,z),new Vector3(.46f,-.02f,z),.025f,lit);
                    diffusers.Add(tube.GetComponent<Renderer>());meshes.Add(tube);
                }
                var lift=Group(mount,"管灯照明",new Vector3(0,-.14f,-.23f));
                lift.localRotation=Quaternion.Euler(54,180,0);
                var lamp=lift.gameObject.AddComponent<Light>();
                lamp.type=LightType.Spot;lamp.spotAngle=116;lamp.innerSpotAngle=72;
                lamp.range=6f;lamp.intensity=1.35f;lamp.color=new Color(1,.935f,.83f);
                lights.Add(lamp);
            }
        }

        internal static void RollerDoor(Transform root,float doorX)
        {
            var zinc=Finish("797A70",.36f,.25f);var seam=Finish("585C56",.26f,.31f);
            var side=Finish("77766C",.32f,.3f);
            // A fixed shutter bay and a lifted leaf share a coil housing. The original doorway is clear.
            const float closedWidth=1.22f; float leftBay=doorX-.73f-closedWidth*.5f;
            Box(root,"镀锌卷帘罩",doorX-closedWidth*.5f,2.35f,-.075f,2.85f,.225f,.26f,zinc);
            Box(root,"卷帘侧跨",leftBay,1.12f,-.069f,closedWidth,2.2f,.082f,zinc);
            for(float y=.11f;y<2.24f;y+=.10f)
                Box(root,"卷边搭口",leftBay,y,-.12f,closedWidth,.026f,.035f,seam);
            foreach(float x in new[]{leftBay-closedWidth/2,doorX-.72f,doorX+.72f})
                Box(root,"旧钢导轨",x,1.14f,-.11f,.076f,2.28f,.107f,side);
            for(int i=0;i<4;i++)
                Box(root,"升起的卷帘",doorX,2.21f-i*.060f,-.078f,1.42f,.057f,.086f,i%2==0?zinc:side);
            Box(root,"旧水泥门槛",doorX,.038f,.02f,1.42f,.073f,.24f,Finish("938675",.12f));
        }
    }
}
