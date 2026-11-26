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
            void Tree(float x,float z,float scale,int angle,bool blossom=false)
            {
                var p=V(x,0,z);if(trunks.Exists(t=>(t-p).sqrMagnitude<21))return;
                var world=site.TransformPoint(p);
                if(blossom)
                {
                    var at=new Vector2(world.x,world.z);
                    var pit=RoadSurfaceGeometry.Strip(at-Vector2.right*.72f,at+Vector2.right*.72f,1.44f);
                    if(_streetPavementEnvelopes.Nearby(pit).Any(id=>
                        RoadSurfaceGeometry.Intersect(pit,_streetPavementEnvelopes.Polygons[id]).Length>=3))return;
                }
                QuarterTree(site,world,scale,angle,blossom);trunks.Add(p);
                if(blossom)
                {
                    QuarterLeafCluster(site,"CrownB",p+V(-.68f,.30f,.33f),new Vector2(1.12f,.62f),"RouteLeaves");
                    QuarterLeafCluster(site,"CrownA",p+V(.52f,.24f,.50f),new Vector2(.79f,.44f),"RouteLeaves");
                }
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
            void Trellis(float x,float z,float yaw,bool seat=true,float depth=1)
            {
                var g=Group("Grape arbour",x,z,yaw);
                g.localScale=new Vector3(1,1,depth);
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
            for(int i=0;i<QuarterBlossoms.Length;i++)
            {
                var tree=QuarterBlossoms[i];Tree(tree.x,tree.z,tree.scale,28+i*82,true);
            }
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
            PlantResidentialEdges(site,(x,z)=>Trellis(x,z,0,false,.42f));
            BuildQuarterVerges(site);
            BatchStatic(site.gameObject);
        }

        private void PlantResidentialEdges(Transform garden,System.Action<float,float> narrowArbour)
        {
            // The painted back gardens have short, irregular shrubs beneath the gables.
            // Use the actual cleared ground between the walls, the alleys and the planted courtyards.
            var occupied=new RoadSurfaceGeometry.Index();
            var fitted=new RoadSurfaceGeometry.Index();
            void Reserve(Vector2[] footprint){occupied.Add(footprint);fitted.Add(footprint);}
            foreach(var item in Places.Values)
            {
                var house=item.Root.transform;
                Vector2 Flat(Vector3 world) => new Vector2(world.x,world.z);
                Reserve(RoadSurfaceGeometry.Strip(Flat(house.position),
                    Flat(house.position+house.forward*(float)item.Place.ExteriorDepth),(float)item.Place.ExteriorWidth));
            }
            foreach(var stone in OldQuarterLanes.Paths)
            {
                Vector2 End(float x,float z)
                {
                    var p=garden.TransformPoint(new Vector3(x,0,z));
                    return new Vector2(p.x,p.z);
                }
                Reserve(RoadSurfaceGeometry.Strip(End(stone.x1,stone.z1),End(stone.x2,stone.z2),stone.width));
            }
            foreach(Transform plot in garden)
            {
                // A single bounds around an entire garden would also reserve the grass between its beds.
                foreach(var renderer in plot.GetComponentsInChildren<MeshRenderer>())
                {
                    var bounds=renderer.bounds;
                    if(bounds.min.y>1.1f || bounds.size.y<.006f) continue;
                    var a=bounds.min;var b=bounds.max;float mid=(a.z+b.z)*.5f;
                    var envelope=RoadSurfaceGeometry.Strip(new Vector2(a.x,mid),new Vector2(b.x,mid),b.z-a.z);
                    occupied.Add(envelope);
                    // Narrow construction can sit parallel to a wall on a rotated block.
                    // Rotation-invariant clearance is also required for a billboarded leaf cluster.
                    var tr=renderer.transform;var box=renderer.localBounds;
                    if(Mathf.Abs(tr.up.y)>.999f && tr.name!="Leaf cluster" && box.size.x>.001f && box.size.z>.001f)
                    {
                        var left=tr.TransformPoint(box.center-Vector3.right*box.extents.x);
                        var right=tr.TransformPoint(box.center+Vector3.right*box.extents.x);
                        fitted.Add(RoadSurfaceGeometry.Strip(new Vector2(left.x,left.z),new Vector2(right.x,right.z),
                            tr.TransformVector(Vector3.forward*box.size.z).magnitude));
                    }
                    else fitted.Add(envelope);
                }
            }
            bool Blocked(RoadSurfaceGeometry.Index index,Vector2[] probe) =>
                index.Nearby(probe).Any(i=>RoadSurfaceGeometry.Intersect(probe,index.Polygons[i]).Length>=3);
            // Ground the visible trees from the overhead painting in the same garden frame as the
            // footpaths. The final four points follow the grass between the homes and the fish pond.
            var belt=new GameObject("住宅绿带乔木").transform;
            belt.SetParent(garden,false);
            var greenSites=new (float x,float z,float scale,int angle)[]{
                (-8.94f,.33f,.92f,177),(-10.61f,32.39f,1.35f,254),
                (-46.30f,89.95f,1.73f,331),(-26.69f,102.83f,1.78f,48),
                (-40.62f,107.41f,1.68f,125),(1.37f,101.40f,1.70f,202)
            };
            var offsets=new[]{Vector3.zero,Vector3.left*1.25f,Vector3.right*1.25f,Vector3.back*1.25f,Vector3.forward*1.25f};
            void PlantTree(Transform parent,(float x,float z,float scale,int angle) sample,bool blossom,bool grove,bool rootLeaves)
            {
                foreach(var offset in offsets)
                {
                    var world=garden.TransformPoint(new Vector3(sample.x,0,sample.z)+offset);
                    var at=new Vector2(world.x,world.z);
                    float radius=Mathf.Max(.85f,sample.scale*.72f);
                    var pit=RoadSurfaceGeometry.Strip(at-Vector2.right*radius,at+Vector2.right*radius,radius*2);
                    if(Blocked(occupied,pit) || Blocked(_streetPlotEnvelopes,pit) || Blocked(_streetPavementEnvelopes,pit))continue;
                    if(_quarterTrees.Any(tree=>(tree-world).sqrMagnitude<4.25f*4.25f))continue;
                    if(_blossomRoots.Any(blossom=>(blossom-at).sqrMagnitude<6.0f*6.0f))continue;
                    QuarterTree(parent,world,sample.scale,sample.angle,blossom,grove);
                    if(rootLeaves)
                    {
                        var foot=parent.InverseTransformPoint(world);
                        QuarterLeafCluster(parent,"CrownB",foot+new Vector3(-.61f,.30f,.27f)*sample.scale,
                            new Vector2(1.26f,.64f)*sample.scale,"RouteLeaves");
                    }
                    Reserve(pit);
                    break;
                }
            }
            foreach(var sample in greenSites)PlantTree(belt,sample,false,false,false);
            var hillside=new GameObject("后坡树丛").transform;
            hillside.SetParent(garden,false);
            var groves=new (float x,float z,float scale,int angle)[]{
                (-36.83f,-92.95f,2.60f,72),(-45.13f,-74.23f,3.05f,161),(-61.11f,-74.33f,2.68f,214),
                (-69.44f,-59.46f,2.25f,47),(-70.69f,-47.50f,1.73f,312),
                (-45.04f,-59.60f,2.75f,196),(-54.13f,-52.44f,3.10f,56),
                (-3.90f,-73.32f,2.57f,254),(-16.61f,-67.99f,2.85f,118),(-25.94f,-61.43f,2.13f,191),
                (-42.05f,-41.35f,2.46f,163),(-53.07f,-35.92f,2.06f,338),
                (-66.25f,-29.23f,2.07f,118),(-67.07f,-24.28f,1.32f,221),
                (-2.13f,-59.87f,1.77f,301),(-13.39f,-52.73f,1.73f,101)
            };
            for(int i=0;i<groves.Length;i++)
                PlantTree(hillside,groves[i],i==4 || i>=14,true,i%3==0);

            Vector2[] GardenBed(float x,float z,float width,float depth)
            {
                var left=garden.TransformPoint(new Vector3(x-width*.5f,0,z));
                var right=garden.TransformPoint(new Vector3(x+width*.5f,0,z));
                return RoadSurfaceGeometry.Strip(new Vector2(left.x,left.z),new Vector2(right.x,right.z),depth);
            }
            // The slim row of posts fits between the existing shared lane and its brick wall.
            foreach(float x in new[]{-21.05f,-21.75f,-22.45f})
            {
                var footprint=GardenBed(x,38.05f,4.88f,1.21f);
                if(Blocked(fitted,footprint) || Blocked(_streetPavementEnvelopes,footprint))continue;
                narrowArbour(x,38.05f);Reserve(footprint);break;
            }
            foreach(float z in new[]{26.44f,25.96f,25.48f})
            {
                var bed=GardenBed(-91.88f,z,4.38f,.85f);
                if(Blocked(fitted,bed) || Blocked(_streetPavementEnvelopes,bed))continue;
                var boxes=new GameObject("旧居矮墙花箱").transform;boxes.SetParent(garden,false);
                boxes.localPosition=new Vector3(-91.88f,0,z);
                StarterPlanter(boxes,new Vector3(-1.14f,.03f,0),2.03f,1);
                StarterPlanter(boxes,new Vector3(1.09f,.03f,0),1.95f,2);
                Reserve(bed);break;
            }
            var plants=new StreetPlanting();
            foreach(var item in Places.Values.Where(v=>v.Place.HousingForm!=ResidenceForm.Standard))
            {
                var p=item.Place;var frame=item.Root.transform;
                int seed=StableCityHash(p.Id);
                float width=(float)p.ExteriorWidth, depth=(float)p.ExteriorDepth;
                int home=item.ResidentialBlock?.homeNumber??0;
                if(p.StreetId=="s_oldhomes" && ((home>=8 && home<=20 && home%2==0) || home==24))
                    PlantFrontGarden(garden,frame,width,home,seed,
                        probe=>!Blocked(occupied,probe) && !Blocked(_streetPavementEnvelopes,probe));
                for(int side=-1;side<=1;side+=2)
                {
                    // Keep the first third of the plot free around its steps and doors.
                    int count=2+(seed+(side+1)/2)%3;
                    for(int i=0;i<count;i++)
                    {
                        float along=.38f+.36f*(side<0?.19f:.78f)+i*.105f;
                        float size=.9f+.36f*Mathf.Abs(Mathf.Sin(seed%311+i*2.31f));
                        var world=frame.TransformPoint(new Vector3(side*(width*.5f+.35f+size*.5f),.045f,depth*along));
                        var at=new Vector2(world.x,world.z);float radius=size*.50f;
                        var probe=RoadSurfaceGeometry.Strip(at-Vector2.right*radius,at+Vector2.right*radius,2*radius);
                        if(Blocked(occupied,probe)||Blocked(_streetPavementEnvelopes,probe)) continue;
                        var plant=(i+seed)%4==0?PlantSpecies.GoldenBush:PlantSpecies.Broadleaf;
                        plants.Add(plant,garden.InverseTransformPoint(world),size,size*(.53f+.08f*(i%3)),seed%173+i);
                    }
                }
            }
            plants.Build(garden,"住宅山墙下的灌木");
        }

        private static void PlantFrontGarden(Transform garden,Transform house,float width,int number,int seed,
            System.Func<Vector2[],bool> clear)
        {
            // These homes have a narrow strip of soil between their front wall and the street.
            // The two metres down the middle belong to the door path, including where it meets the road at an angle.
            var earth=Mats.Lit(ArtDirection.Hex("84755A"),.08f);
            var cream=Mats.Lit(ArtDirection.Hex("FFF1CC"),.09f);
            var pink=Mats.Lit(ArtDirection.Hex("E6ADA1"),.09f);
            var underleaf=Mats.Lit(ArtDirection.Hex("687740"),.09f);
            var sunleaf=Mats.Lit(ArtDirection.Hex("90984F"),.08f);
            float inner=1.36f,outer=width*.5f-.15f;
            int bays=Mathf.Clamp(Mathf.FloorToInt((outer-inner)/1.05f),2,4);
            float step=(outer-inner)/bays;
            Vector2 Ground(float x,float z)
            {
                var at=house.TransformPoint(new Vector3(x,0,z));return new Vector2(at.x,at.z);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Transform bed=null;
                var shrubs=new StreetPlanting();
                var detail=new MiniatureDetailMesh();
                for(int bay=0;bay<bays;bay++)
                {
                    float x=side*(inner+(bay+.5f)*step);
                    int variant=seed%167+bay*7+(side+1)*5;
                    float radius=Mathf.Min(step*.49f,.66f);
                    bool flowering=number>=12 && number<=18;
                    int rows=flowering?3:2;
                    for(int row=0;row<rows;row++)
                    {
                        // Trailing flowers occupy a staggered second row at the path and street edges.
                        if(row==rows-1 && (bay+number/2+(side+1)/2)%4==0)continue;
                        float z=-1.13f-row*1.02f+(row==0?0:.11f*Mathf.Sin(variant+row));
                        float rz=row==0?.55f:.50f;
                        var probe=RoadSurfaceGeometry.Strip(Ground(x-radius,z),Ground(x+radius,z),rz*2);
                        if(!clear(probe))continue;
                        if(bed==null)
                        {
                            bed=new GameObject($"旧居前庭花圃 {number:D2} {(side<0?"左":"右")}").transform;
                            bed.SetParent(garden,false);bed.SetPositionAndRotation(house.position,house.rotation);
                        }
                        var foot=new Vector3(x,.065f,z);
                        detail.Lobe(foot,new Vector3(radius,.028f,rz),earth,variant);
                        if(row==0 && bay==bays-1 && side==(number%4==0?1:-1) && number%6!=0)
                        {
                            PlaceClayPot(bed,foot);
                            continue;
                        }
                        if(row==0)
                        {
                            float height=.79f+.27f*Mathf.Abs(Mathf.Sin(variant));
                            var species=bay%3==1?PlantSpecies.Hedge:PlantSpecies.Broadleaf;
                            shrubs.Add(species,foot,radius*2,height,variant);
                            detail.Lobe(foot+Vector3.up*height*.30f,new Vector3(radius*.93f,height*.35f,rz*.93f),underleaf,variant);
                            QuarterLeafCluster(bed,bay%2==0?"CrownB":"CrownA",foot+Vector3.up*height*.43f,
                                new Vector2(radius*1.98f,height*.92f),"RouteLeaves");
                        }
                        else
                        {
                            float height=flowering && row==1?.69f:.44f;
                            shrubs.Add(PlantSpecies.Broadleaf,foot,radius*1.83f,height,variant+25);
                            detail.Lobe(foot+Vector3.up*height*.38f,new Vector3(radius*.93f,height*.38f,rz*.85f),underleaf,variant);
                            var leaf=foot+new Vector3(radius*.27f,height*.58f,-rz*.15f);
                            detail.Lobe(leaf,new Vector3(radius*.51f,height*.22f,rz*.57f),sunleaf,variant+row);
                            var petals=(bay+number/2+row)%5==0?pink:cream;
                            int heads=flowering?5:3;
                            for(int head=0;head<heads;head++)
                            {
                                float angle=variant+row*1.7f+head*2.4f;
                                float size=flowering?.12f+.047f*Mathf.Abs(Mathf.Cos(angle)):.1f;
                                var bloom=foot+new Vector3(Mathf.Cos(angle)*radius*.64f,
                                    height*.84f+.13f*Mathf.Sin(angle*3),Mathf.Sin(angle)*rz*.60f);
                                detail.Lobe(bloom,new Vector3(size,size*.65f,size*.92f),petals,variant+head);
                            }
                        }
                    }
                }
                if(bed==null)continue;
                shrubs.Build(bed,"庭院矮灌与花叶");
                detail.Build(bed,"种植土与花簇");
            }
        }
    }
}
