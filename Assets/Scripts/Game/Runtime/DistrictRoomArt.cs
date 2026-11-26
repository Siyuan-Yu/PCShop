using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Room coordinates start at the near left corner. Wall runs use local X for their length.
    internal static class DistrictRoomArt
    {
        internal static int District(CityLayout city, string street) => city?.AgencyDistrictFor(street)?.id switch
        {
            "community" => 1, "park" => 2, "garden" => 3, _ => 0
        };

        internal static Material Finish(string hex, float gloss=.23f, float metal=0) =>
            Mats.Lit(ArtDirection.Hex(hex),gloss,metal);
        internal static Material Warm => Mats.Glow(ArtDirection.Hex("FFE1AD"),new Color(1.15f,.76f,.39f),false);
        internal static GameObject Box(Transform r,string name,float x,float y,float z,float w,float h,float d,Material mat) =>
            Prims.Box(r,name,new Vector3(x,y,z),new Vector3(w,h,d),mat);
        internal static Transform Group(Transform parent,string name,Vector3 center,float yaw=0)
        {
            var t=new GameObject(name).transform;
            t.SetParent(parent,false);
            t.localPosition=center;
            t.localRotation=Quaternion.Euler(0,yaw,0);
            return t;
        }

        internal static void Build(Transform r,float w,float d,int district,bool home,bool office=false)
        {
            bool garden=district==3, park=district==2;
            string surface=office ? (garden?"interiorPorcelain":"officeCarpet") :
                garden ? "interiorPorcelain" : park ? "oakParquet" : district==1?"creamTile":"terraTile";
            float span = park && !office ? 2.4f : 3.2f;
            string tint=office ? (garden?"DDD0BD":"D5D3CF") : district switch
            {
                0 => "FFF0DB",1 => home?"E0DCCD":"F0E9DE",2 => home?"E4C6A9":"FBF0DF",
                _ => home?"D0C8BF":"E2CEBC"
            };
            var floor=district==0?SuburbanRoomFinish.Floor(w,d,home):DistrictFinishes.Tile(surface,new Vector2(w/span,d/span),ArtDirection.Hex(tint));
            // A local box-projected environment supplies grazing reflections at every quality level.
            // The convolved cubemap contains the window bays and warm coves of the room.
            if(district>=2)
            {
                var holder=Group(r,"室内反射环境",new Vector3(w*.5f,1.6f,d*.5f));
                var probe=holder.gameObject.AddComponent<UnityEngine.ReflectionProbe>();
                probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;
                probe.customBakedTexture=Resources.Load<Cubemap>("OldTown/Surfaces/roomReflection");
                probe.boxProjection=true;probe.size=new Vector3(w+.65f,4.4f,d+.65f);
                probe.blendDistance=.12f;probe.intensity=garden?.86f:.50f;
            }
            ShadowCeiling(r,w,d,office?3.73f:3.08f);
            var plinth=Box(r,"Floor",w/2,-.07f,d/2,w,.14f,d,floor);
            if(!home) plinth.AddComponent<BoxCollider>();
            Material trim=Finish(garden?"A48860":park?"88735A":"8B8977");
            if(garden && office)
            {
                var border=DistrictFinishes.Tile("officeSlate",new Vector2(2,.35f),ArtDirection.Hex("BDAA96"));
                Box(r,"前厅嵌石",w*.26f,.008f,d*.42f,w*.51f,.014f,.23f,border);
                Box(r,"会议区嵌石",w*.515f,.008f,d*.68f,.23f,.014f,d*.54f,border);
            }
            if(garden)
            {
                float edge=.38f, line=office?.07f:.035f;
                for(int i=0;i<2;i++)
                {
                    Box(r,"石材嵌线",w/2,.004f,i==0?edge:d-edge,w-edge*2,.007f,line,trim);
                    Box(r,"石材嵌线",i==0?edge:w-edge,.004f,d/2,line,.007f,d-edge*2,trim);
                }
            }
            if(garden && home)
            {
                var inlay=Mats.Glow(ArtDirection.Hex("E9D9BE"),new Color(.54f,.39f,.23f),false);
                float x=w*.22f,z=d*.32f,radius=.86f;
                void Strip(Vector2 a,Vector2 b)
                {
                    Vector2 mid=(a+b)*.5f, delta=b-a;
                    var run=Box(r,"弧转地面导光缝",mid.x,.010f,mid.y,.032f,.008f,delta.magnitude+.008f,inlay);
                    run.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg,0);
                }
                Strip(new Vector2(x,d-.60f),new Vector2(x,z+radius));
                Vector2 prior=new Vector2(x,z+radius);
                for(int i=1;i<=16;i++)
                {
                    float a=Mathf.PI+i*Mathf.PI/32f;
                    var next=new Vector2(x+radius+Mathf.Cos(a)*radius,z+radius+Mathf.Sin(a)*radius);
                    Strip(prior,next);prior=next;
                }
                Strip(prior,new Vector2(w*.75f,z));
            }
            var back=Group(r,"后墙",new Vector3(0,0,d));
            var side=Group(r,"侧墙",new Vector3(w,0,d),90);
            Wall(back,w,district,home,office,glazed:home || office);
            Wall(side,d,district,home,office,glazed:(district>=2 && !home && !office) || (garden && (home||office)),side:true);
            Box(r,"切面收边",-.04f,.08f,d/2,.09f,.16f,d,trim);
            if(home) Box(r,"前沿",w/2,.08f,-.04f,w,.16f,.09f,trim);
        }

        internal static void ShadowCeiling(Transform r,float w,float d,float height)
        {
            var roof=Box(r,"采光遮阳顶",w*.5f,height+.04f,d*.5f,w+.18f,.085f,d+.18f,Finish("C2BCAC"));
            roof.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private static Material _joint,_cove;
        private static void Feather(Transform wall,float length,float span,float y,float z,bool horizontal)
        {
            var shader=Resources.Load<Shader>("OldTown/RoomJunction");
            if(!_joint) { _joint=new Material(shader){name="墙脚接触阴影"};_joint.SetColor("_Tint",new Color(.14f,.11f,.085f,.27f)); }
            if(!_cove) { _cove=new Material(shader){name="暗槽暖色漫反射"};_cove.SetColor("_Tint",new Color(.96f,.70f,.39f,.29f)); }
            var grad=Prims.Shape(PrimitiveType.Quad,wall,horizontal?"墙脚接触阴影":"檐口漫反射",
                new Vector3(length*.5f,y,z),new Vector3(length,span,1),horizontal?_joint:_cove);
            if(horizontal) grad.transform.localRotation=Quaternion.Euler(90f,0,0);
            var render=grad.GetComponent<Renderer>();render.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            render.receiveShadows=false;
        }

        private static void Wall(Transform r,float length,int district,bool home,bool office,bool glazed,bool side=false)
        {
            bool garden=district==3,park=district==2;
            float height=office?3.65f:3.0f;
            var plaster=district==3 && !home && !office ? Finish("655F51",.18f) :
                DistrictFinishes.Tile("roomLimewash",new Vector2(length/3f,height/3f),
                    ArtDirection.Hex(garden?"EFEAE2":park?"F6EBDB":"F4EFDF"));
            Feather(r,length,.44f,.019f,-.22f,true);
            if(district>=2 && !glazed) Feather(r,length,.54f,height-.42f,-.09f,false);
            var frame=Finish(garden?"786B58":"5A5D55",.43f,.28f);
            var oak=DistrictFinishes.Tile("walnut",new Vector2(1,1.4f));
            float left=length*(home && district==2?.39f:district>=2?.12f:.36f);
            float right=length*(district>=2?.89f:.62f);
            // Reserve the bedroom exit at local world z=1, including its solid jamb.
            if(side && home) right=Mathf.Min(right,length-1.62f);
            float sill=district>=2?(home||office?.12f:.62f):1.02f;
            float head=height-(district>=2?.26f:.58f);
            void Panel(float a,float b,float bottom,float top,Material material)
            {
                if(b-a>.005f && top-bottom>.005f)
                    Box(r,"墙板",(a+b)*.5f,(bottom+top)*.5f,.075f,b-a,top-bottom,.15f,material);
            }
            if(glazed)
            {
                Panel(0,left,0,height,plaster); Panel(right,length,0,height,plaster);
                Panel(left,right,0,sill,plaster); Panel(left,right,head,height,plaster);
                Window(r,left,right,sill,head,frame,district,garden&&home&&!side);
            }
            else Panel(0,length,0,height,plaster);
            if(district==0)
            {
                SuburbanRoomFinish.Wall(r,length,home);
                SuburbanRoomFinish.Electrical(r,length,!side);
                return;
            }
            if (glazed)
            {
                var daylight=Group(r,"窗边漫射",new Vector3((left+right)*.5f,head-.36f,-.48f));
                var fill=daylight.gameObject.AddComponent<Light>();
                daylight.localRotation=Quaternion.Euler(23,180,0);
                fill.type=LightType.Spot;fill.spotAngle=115;fill.innerSpotAngle=65;fill.color=new Color(.86f,.91f,1);
                fill.range=Mathf.Min(8f,2f+(right-left)*.75f);fill.intensity=.62f;
                daylight.gameObject.AddComponent<InteriorWindowLight>().Initialize(fill);
            }
            if(district<=1)
            {
                var dado=Finish(district==0?"909779":"A1AD96");
                Box(r,"漆墙裙",length/2,.42f,-.018f,length,.84f,.035f,dado);
                Box(r,"压条",length/2,.85f,-.021f,length,.035f,.04f,Finish("BBBBA4"));
            }
            else if(!glazed)
            {
                var wood= garden?(side?oak:Finish("666759")):Finish("A7A18B");
                if(!home)
                {
                    float x0=length*.10f,x1=length*.89f;
                    Box(r,"饰面",length*.495f,1.43f,-.034f,x1-x0,2.66f,.062f,wood);
                    float spacing=garden?.86f:.115f;
                    for(float x=x0;x<x1;x+=spacing)
                        Box(r,garden?"黄铜分缝":"细竖格栅",x,1.43f,-.073f,.018f,2.62f,.025f,frame);
                }
                else
                {
                    for(int j=0;j<3;j++)
                    {
                        var x=length*(.20f+j*.09f);
                        Box(r,"画框",x,1.76f,-.04f,.47f,.64f,.07f,frame);
                        Box(r,"画芯",x,1.76f,-.084f,.40f,.56f,.025f,Finish(j==1?"A3B4A2":"E1CAA2"));
                    }
                }
            }
            if (district>=2 && (home || office || !glazed))
            {
                int lamps=Mathf.Clamp(Mathf.FloorToInt(length/4.6f),1,3);
                for(int i=0;i<lamps;i++)
                {
                    // Light spills down the finish, while the window supplies cooler daylight.
                    var mount=Group(r,"檐槽洗墙灯",new Vector3(length*(i+.5f)/lamps,height-.20f,-.45f));
                    mount.localRotation=Quaternion.Euler(68,0,0);
                    var lamp=mount.gameObject.AddComponent<Light>();
                    lamp.type=LightType.Spot;lamp.spotAngle=87;lamp.innerSpotAngle=45;
                    lamp.range=5f;lamp.color=new Color(1f,.82f,.63f);lamp.intensity=2.1f;
                }
            }
            if(district==3 && !glazed && !home)
            {
                var brass=Finish("B19B72",.47f,.48f);
                Box(r,"镶铜饰线",length/2,2.47f,-.087f,length*.88f,.022f,.022f,brass);
                Box(r,"石材踢脚",length/2,.082f,-.032f,length,.164f,.064f,Finish("8C8378",.39f));
            }
            Box(r,"墙顶收口",length/2,height+.015f,.05f,length+.10f,.10f,.21f,Finish(garden?"A49B89":"D7CCB7"));
            if(district>=2)
            {
                Box(r,"悬挑檐板",length/2,height-.045f,-.13f,length,.12f,.34f,Finish(garden?"978C7C":"C9BDAA"));
                Box(r,"暗槽反光面",length/2,height-.13f,-.11f,length-.08f,.082f,.16f,Finish("F2D9B9",.32f));
                Box(r,"檐内灯槽",length/2,height-.15f,-.02f,length-.18f,.059f,.072f,frame);
                Box(r,"线性漫射罩",length/2,height-.122f,-.306f,length-.15f,.049f,.034f,Warm);
            }
            else for(float x=1.25f;x<length-.45f;x+=2.55f)
            {
                Box(r,"灯座",x,2.64f,-.084f,district==0?.79f:.20f,.12f,.16f,frame);
                Box(r,"灯罩",x,2.56f,-.095f,district==0?.71f:.15f,.045f,.13f,Warm);
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string,Material> Vistas=new();

        private static void Window(Transform r,float a,float b,float sill,float top,Material frame,int district,bool pool)
        {
            float w=b-a, h=top-sill, x=(a+b)/2;
            // The recessed view stays within the window reveal from every camera angle.
            string vista=pool?"poolVista":district>=2?"parkVista":"townVista";
            if (!Vistas.TryGetValue(vista,out var view) || view==null)
            {
                view=new Material(Resources.Load<Shader>("OldTown/InteriorVista")) { name="隔窗远景 / "+vista };
                view.SetTexture("_BaseMap",Resources.Load<Texture2D>("OldTown/Surfaces/"+vista));
                Vistas[vista]=view;
            }
            var picture=Prims.Shape(PrimitiveType.Quad,r,"窗外景深",new Vector3(x,(sill+top)/2,.169f),new Vector3(w,h,1),view);
            picture.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var glass=DistrictFinishes.VillaPane(true);
            Box(r,"窗玻璃",x,(sill+top)/2,.05f,w,h,.012f,glass);
            int bays=Mathf.Max(2,Mathf.RoundToInt(w/1.28f));
            for(int i=0;i<=bays;i++) Box(r,"窗挺",a+w*i/bays,(sill+top)/2,-.018f,.045f,h,.082f,frame);
            foreach(float y in new[]{sill,top}) Box(r,"窗框",x,y,-.026f,w+.07f,.064f,.11f,frame);
            if(district<2) Box(r,"窗横档",x,sill+h*.63f,-.036f,w,.04f,.07f,frame);
            Box(r,"窗台",x,sill-.035f,-.035f,w+.14f,.09f,.22f,Finish(district==3?"B4AAA0":"B4A38C"));
            if(district==3)
            {
                var jamb=DistrictFinishes.Tile("walnut",new Vector2(.5f,1.5f));
                foreach(float edge in new[]{a-.085f,b+.085f})
                {
                    Box(r,"胡桃木窗套",edge,top*.5f,-.075f,.15f,top,.14f,jamb);
                    Box(r,"铜嵌条",edge,top*.5f,-.151f,.012f,top,.012f,Finish("AB9168",.52f,.48f));
                }
            }
            if(district==1 || district==2)
                for(int i=0;i<6;i++)
                {
                    Box(r,"窗帘褶",a+i*.045f,top-h*.5f,-.077f-(i%2)*.024f,.056f,h,.048f,Finish(i%2==0?"E2D8C5":"CBBDA6"));
                }
        }

        internal static void Plant(Transform root,Vector3 at,float s)
        {
            var r=Group(root,"室内观叶",at);
            r.localScale=Vector3.one*s;
            Prims.Shape(PrimitiveType.Cylinder,r,"陶盆",new Vector3(0,.20f,0),new Vector3(.42f,.20f,.42f),Finish("B5A28A"));
            for(int i=0;i<11;i++)
            {
                float angle=i*137.5f,rad=angle*Mathf.Deg2Rad;
                var leaf=Prims.Shape(PrimitiveType.Sphere,r,"叶片",
                    new Vector3(Mathf.Sin(rad)*.16f,.63f+(i%3)*.11f,Mathf.Cos(rad)*.16f),
                    new Vector3(.105f,.63f,.07f),Finish(i%3==0?"798B52":"506C4B"));
                leaf.transform.localRotation=Quaternion.Euler(0,angle,0)*Quaternion.Euler(27f+i%3*9,0,0);
            }
        }

    }
}
