using System.Linq;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        private void BuildQuarterCourtyards(Vector3 origin)
        {
            var site=new GameObject("Old quarter shared gardens").transform;
            site.SetParent(_root,false);site.position=origin;site.rotation=Quaternion.Euler(0,OldQuarterLanes.Heading,0);
            var trunks = _quarterTrees.Select(site.InverseTransformPoint).ToList();
            var metal=Mats.Lit(ArtDirection.Hex("495956"),.26f);
            var wood=IndustrialMaterials.Surface("wood",ArtDirection.Hex("977558"),new Vector2(1.5f,1));
            var tile=Mats.Lit(ArtDirection.Hex("998879"),.13f);
            var brick=IndustrialMaterials.Surface("brick",ArtDirection.Hex("A68E79"),new Vector2(2.8f,.7f));
            var tire=Mats.Lit(ArtDirection.Hex("3F4543"),.12f);
            Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
            Transform Group(string name,float x,float z,float yaw=0)
            {
                var g=new GameObject(name).transform;g.SetParent(site,false);g.localPosition=V(x,0,z);
                g.localRotation=Quaternion.Euler(0,yaw,0);return g;
            }
            void Box(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
                =>Prims.Box(parent,name,pos,scale,mat);
            void Deck(Transform parent,float x,float z,float w,float d,string tint="B7AB96",float top=.067f)
            {
                var mat=IndustrialMaterials.Surface("paving",ArtDirection.Hex(tint),new Vector2(w/4,d/4));
                Box(parent,"Set stone",V(x,top-.022f,z),V(w,.044f,d),mat);
            }
            void Wall(Transform parent,float x,float z,float length,bool across=true)
            {
                float w=across?length:.24f,d=across?.24f:length;
                Box(parent,"Brick garden wall",V(x,.38f,z),V(w,.69f,d),brick);
                Box(parent,"Stone coping",V(x,.75f,z),V(w+.07f,.085f,d+.07f),tile);
                int gaps=Mathf.CeilToInt(length/3.7f);
                for(int k=0;k<=gaps;k++)
                {
                    float pos=(k/(float)gaps-.5f)*length;
                    Box(parent,"Gate pier",V(x+(across?pos:0),.46f,z+(across?0:pos)),V(.32f,.88f,.32f),brick);
                }
            }
            void Strip(Transform parent,float x,float z,float length,float phase)
            {
                int n=Mathf.CeilToInt(length/1.2f);
                for(int i=0;i<n;i++)
                {
                    float t=(i+.5f)/n-.5f,s=1+.15f*Mathf.Sin(i*3.1f+phase);
                    QuarterLeafCluster(parent,i%2==0?"CrownB":"CrownA",V(x+t*length,.52f*s,z+.12f*Mathf.Sin(i+phase)),new Vector2(1.60f*s,.87f*s),"RouteLeaves");
                }
            }
            void Tree(float x,float z,float scale,int angle)
            {
                var p=V(x,0,z);if(trunks.Exists(t=>(t-p).sqrMagnitude<21))return;
                QuarterTree(site,site.TransformPoint(p),scale,angle);trunks.Add(p);
            }
            void Bench(Transform parent,float x,float z,float yaw)
            {
                var b=new GameObject("Timber bench").transform;b.SetParent(parent,false);
                b.localPosition=V(x,.075f,z);b.localRotation=Quaternion.Euler(0,yaw,0);
                foreach(float off in new[]{-.22f,0,.22f})Box(b,"Seat",V(0,.47f,off),V(2.35f,.09f,.19f),wood);
                foreach(float h in new[]{.76f,.96f})Box(b,"Back",V(0,h,.29f),V(2.35f,.14f,.09f),wood);
                foreach(float leg in new[]{-.93f,.93f})
                {
                    Box(b,"Foot",V(leg,.225f,-.19f),V(.08f,.45f,.08f),metal);
                    Box(b,"Back leg",V(leg,.49f,.25f),V(.08f,.98f,.08f),metal);
                }
            }
            void Court(Transform g,float w,float d)
            {
                Deck(g,0,0,w,d);
                Wall(g,0,d/2-.15f,w);Wall(g,-w/2+.15f,d*.18f,d*.64f,false);
                Strip(g,w*.27f,d*.5f-.65f,w*.32f,.7f);
            }
            void Line(MiniatureDetailMesh m,Vector3 a,Vector3 b,float width,Material mat)
            {
                Vector3 delta=b-a;
                m.Box((a+b)*.5f,V(width,delta.magnitude,width),mat,Quaternion.FromToRotation(Vector3.up,delta.normalized));
            }
            void Washing(float x,float z,float yaw)
            {
                var g=Group("Shared washing court",x,z,yaw);Court(g,9.1f,4.7f);
                var detail=new MiniatureDetailMesh();
                var colors=new[]{"DDD1AA","809BA1","B58067","DDD9BD","758989"};
                for(int row=0;row<2;row++)
                {
                    float zz=row*.99f-.65f, h=2.05f+row*.13f;
                    foreach(float edge in new[]{-3.05f,3.05f})
                        detail.Box(V(edge,h*.5f,zz),V(.088f,h,.088f),metal);
                    Line(detail,V(-3.05f,h,zz),V(0,h-.09f,zz),.035f,metal);
                    Line(detail,V(0,h-.09f,zz),V(3.05f,h,zz),.035f,metal);
                    for(int i=0;i<3;i++)
                    {
                        float center=-1.85f+1.76f*i+row*.22f,drop=.66f+.17f*((row+i)%3);
                        var fabric=Mats.Lit(ArtDirection.Hex(colors[(i+row*2)%colors.Length]),.07f);
                        for(int k=0;k<4;k++)
                        {
                            float fold=Mathf.Sin(k*1.8f+i);
                            detail.Box(V(center+(k-1.5f)*.259f,h-drop*.5f-.07f,zz+.049f*fold),V(.271f,drop+.048f*fold,.054f),fabric,Quaternion.Euler(3*fold,2*fold,0));
                        }
                    }
                }
                detail.Build(g,"Clotheslines and linen");StarterPlanter(g,V(-3.5f,0,1.6f),.95f,3);
            }
            void Cycle(MiniatureDetailMesh d,float x,float z,int i)
            {
                Vector3 P(float u,float h,float off=0)=>V(x+u,h+.10f,z+off);
                var frame=Mats.Lit(ArtDirection.Hex(i%2==0?"72948C":"A17C61"),.18f);
                foreach(float wheel in new[]{-.55f,.55f})
                {
                    for(int k=0;k<12;k++)
                    {
                        float a=k*Mathf.PI/6,b=(k+1)*Mathf.PI/6;
                        var pt=P(wheel+.34f*Mathf.Cos(a),.34f+.34f*Mathf.Sin(a));
                        Line(d,pt,P(wheel+.34f*Mathf.Cos(b),.34f+.34f*Mathf.Sin(b)),.044f,tire);
                        if(k%2==0)Line(d,P(wheel,.34f),pt,.018f,metal);
                    }
                }
                var back=P(-.55f,.34f);var crank=P(-.04f,.30f);var seat=P(-.21f,.82f);var head=P(.39f,.78f);
                Line(d,back,crank,.057f,frame);Line(d,back,seat,.052f,frame);Line(d,crank,seat,.058f,frame);
                Line(d,crank,head,.061f,frame);Line(d,seat,head,.052f,frame);Line(d,P(.55f,.34f),head,.053f,metal);
                Line(d,head,P(.39f,.94f),.046f,metal);Line(d,P(.39f,.94f,-.20f),P(.39f,.94f,.20f),.048f,metal);
                d.Box(P(-.20f,.86f),V(.26f,.07f,.17f),tire);
            }
            void Bikes(float x,float z,float yaw)
            {
                var g=Group("Neighbour bicycle parking",x,z,yaw);
                Deck(g,0,0,7.8f,4.2f);Wall(g,0,1.92f,7.8f);
                var d=new MiniatureDetailMesh();for(int i=0;i<4;i++)Cycle(d,i*.60f-1.55f,i*.44f-.9f,i);
                foreach(float side in new[]{-2.9f,2.9f})d.Box(V(side,1.28f,.9f),V(.105f,2.55f,.105f),metal);
                var roof=IndustrialMaterials.Surface("metal",ArtDirection.Hex("758E86"),new Vector2(2,1));
                d.Box(V(0,2.58f,.1f),V(6.6f,.12f,2.8f),roof,Quaternion.Euler(-7,0,0));
                d.Build(g,"Cycle shelter");Strip(g,2.65f,-.94f,1.65f,2);
            }
            void KitchenGarden(float x,float z,float yaw)
            {
                var g=Group("Residents vegetable beds",x,z,yaw);
                var soil=IndustrialMaterials.Surface("concrete",ArtDirection.Hex("86745A"),new Vector2(2,1));
                Wall(g,0,2.23f,8.3f);
                for(int k=0;k<3;k++)
                {
                    float cx=(k-1)*2.55f;
                    Box(g,"Raised soil",V(cx,.15f,0),V(1.68f,.24f,3.35f),soil);
                    foreach(float side in new[]{-1f,1f})
                    {
                        Box(g,"Bed edging",V(cx+side*.88f,.17f,0),V(.11f,.26f,3.56f),wood);
                        Box(g,"Bed edging",V(cx,.17f,side*1.73f),V(1.84f,.26f,.10f),wood);
                    }
                    for(int j=0;j<4;j++)for(int i=0;i<2;i++)
                        QuarterLeafCluster(g,(i+j+k)%2==0?"CrownA":"CrownB",V(cx+(i-.5f)*.66f,.42f+(k%2)*.12f,(j-1.5f)*.77f),new Vector2(.8f,.49f+(k%2)*.14f),"RouteLeaves");
                }
            }
            void FlowerBorder(float x,float z,float width,float yaw,int seed)
            {
                var bed=Group("Residents flower border",x,z,yaw);
                var blooms=new MiniatureDetailMesh();
                var coral=Mats.Lit(ArtDirection.Hex("C88773"),.08f);
                var cream=Mats.Lit(ArtDirection.Hex("E2C992"),.08f);
                var gold=Mats.Lit(ArtDirection.Hex("C6A358"),.08f);
                int clumps=Mathf.CeilToInt(width/.83f);
                for(int i=0;i<clumps;i++)
                {
                    float along=(i+.5f)*width/clumps-width*.5f, turn=seed+i*2.1f;
                    float height=.39f+.12f*Mathf.Sin(turn);
                    QuarterLeafCluster(bed,i%2==0?"CrownA":"CrownB",V(along,height-.12f,.16f*Mathf.Cos(turn)),
                        new Vector2(1.04f,height*1.55f),"RouteLeaves");
                    for(int j=0;j<4;j++)
                    {
                        float angle=turn+j*1.69f;
                        blooms.Lobe(V(along+.31f*Mathf.Cos(angle),height+.11f+.07f*Mathf.Sin(angle),.19f*Mathf.Sin(angle)),
                            V(.087f,.046f,.077f),i%4==0?coral:j%3==0?gold:cream,(i+j)%4);
                    }
                }
                blooms.Build(bed,"Flower heads");
            }
            void ShadeCourt(float x,float z,float yaw,float w=9f)
            {
                var g=Group("Tree court",x,z,yaw);Court(g,w,4.8f);
                Bench(g,-w*.23f,1.12f,0);Bench(g,w*.31f,-.60f,90);
                var stone=Mats.Lit(ArtDirection.Hex("A59984"),.12f);
                Box(g,"Stone table base",V(0,.39f,.02f),V(.46f,.75f,.46f),stone);
                Box(g,"Stone table",V(0,.80f,.02f),V(1.24f,.12f,1.24f),stone);
                StarterPlanter(g,V(-w*.40f,.02f,-1.42f),.9f,1);
            }
            void Walk(float x1,float z1,float x2,float z2,float width,float top=.050f)
            {
                float dx=x2-x1,dz=z2-z1,length=Mathf.Sqrt(dx*dx+dz*dz);
                var g=Group("Courtyard footpath",(x1+x2)/2,(z1+z2)/2,Mathf.Atan2(dx,dz)*Mathf.Rad2Deg);
                Deck(g,0,0,width,length,"A99E88",top);
            }
            void Trellis(float x,float z,float yaw,bool seat=true)
            {
                var g=Group("Grape arbour",x,z,yaw);
                foreach(float a in new[]{-2.15f,2.15f})
                    foreach(float b in new[]{-1.10f,1.10f})
                        Box(g,"Timber post",V(a,1.27f,b),V(.15f,2.50f,.15f),wood);
                foreach(float b in new[]{-1.10f,1.10f})
                    Box(g,"Timber rail",V(0,2.51f,b),V(4.8f,.19f,.16f),wood);
                for(int i=0;i<8;i++)
                    Box(g,"Vine slat",V((i-3.5f)*.62f,2.65f,0),V(.11f,.12f,2.78f),wood);
                for(int j=0;j<2;j++)for(int k=0;k<5;k++)
                    if(j==0||k!=2)QuarterLeafCluster(g,k%2==0?"CrownA":"CrownB",
                        V((k-2)*.90f+.18f*Mathf.Sin(j+k),2.74f+.09f*Mathf.Cos(k),j-.48f),
                        new Vector2(1.36f,.71f+.13f*Mathf.Sin(k)),"RouteLeaves");
                for(int k=0;k<5;k++)
                {
                    float side=k%2==0?1:-1;
                    QuarterLeafCluster(g,"CrownB",V(side*(1.94f-.08f*k),2.32f-.26f*k,side*.96f),
                        new Vector2(.70f-.06f*k,.67f-.035f*k),"RouteLeaves");
                }
                if(seat)Bench(g,-.15f,.50f,0);
            }
            // The alleys join gaps between the surveyed door approaches.
            foreach (var path in OldQuarterLanes.Paths) Walk(path.x1,path.z1,path.x2,path.z2,path.width,path.top);
            ShadeCourt(-30.4f,18f,90,9);Washing(-55.5f,32.9f,180);Bikes(-18.5f,31.6f,0);
            KitchenGarden(-77.6f,30.4f,180);ShadeCourt(-94,37.8f,0);
            Washing(-118,37.3f,0);KitchenGarden(0,30.3f,180);Bikes(15.9f,40.1f,0);
            ShadeCourt(-14,-8,90,8.2f);
            ShadeCourt(-61,-7,180,12.2f);Bikes(-69,-11.5f,22);
            // Shared rear gardens of the starter house and the station, beyond their doors.
            var backYard=Group("Home and station common garden",-4.1f,-14.1f);
            Deck(backYard,0,0,9.2f,4.1f);Wall(backYard,0,1.99f,9.4f);
            Trellis(-4.9f,-14.1f,0);
            var endYard=Group("Laundry and potting bench",-25,-22.1f);
            Wall(endYard,-2.4f,-1.9f,13);Strip(endYard,-2.3f,-1.25f,8,0);
            Washing(-30.8f,-20.2f,180);KitchenGarden(-15.7f,-21.3f,180);
            KitchenGarden(-54.6f,-18.5f,175);Trellis(-59.5f,-7.9f,180,false);
            Trellis(-93.5f,37.5f,0,false);
            Trellis(-91,17.4f,90);
            FlowerBorder(-91.9f,17.1f,4.2f,90,11);
            FlowerBorder(-105,37.8f,5.4f,0,3);
            FlowerBorder(-50.3f,38.1f,6.2f,0,4);
            FlowerBorder(-116,32.3f,5.0f,0,15);
            FlowerBorder(7.9f,38.1f,3.1f,0,9);
            FlowerBorder(-20.3f,33.6f,4.2f,0,1);
            var hedge=Group("Courtyard edges",0,0);
            Wall(hedge,-51.8f,28.4f,10.4f);Strip(hedge,-52.6f,27.8f,7.4f,1.8f);
            Wall(hedge,-20.1f,40.8f,9.8f);Strip(hedge,-20.6f,40.15f,6.6f,2.7f);
            Wall(hedge,-65.8f,-19.3f,12.3f);Strip(hedge,-66.5f,-18.6f,8.8f,5.1f);
            Tree(-33.1f,-25,1.73f,160);Tree(-22.8f,-26.3f,1.47f,45);Tree(-9.3f,-22.6f,1.55f,110);
            Tree(-47.6f,-24.3f,1.75f,217);Tree(-67.5f,-23,1.48f,35);
            KitchenGarden(-52,91,180);Washing(-104,89.5f,9);
            var bounds=Group("Divided back gardens",0,0);
            foreach(var q in new[]{V(-113,0,28),V(-91,0,28.8f),V(-64,0,40.8f),V(-37,0,28.9f),V(-14,0,39),V(13.8f,0,28)})
            {
                Wall(bounds,q.x,q.z,8.1f);Strip(bounds,q.x+.7f,q.z-.65f,5.8f,q.x);
            }
            foreach(var q in new[]{V(-121,1.38f,30),V(-96,1.55f,40.1f),V(-73.8f,1.48f,39),V(-48.4f,1.55f,29.2f),V(-32,1.52f,23.1f),V(-11.9f,1.45f,36.9f),V(12.2f,1.3f,31),V(-66,1.55f,-7.7f),V(-11.2f,1.25f,-11.8f)})
                Tree(q.x,q.z,q.y,Mathf.RoundToInt(-q.x*13));
            BuildQuarterVerges(site);
            BatchStatic(site.gameObject);
        }
    }
}
