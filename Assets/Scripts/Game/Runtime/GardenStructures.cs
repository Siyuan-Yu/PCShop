using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Shared unit plate for the garden villas, shop sunshades and circuit pavilions.
    // Separate top and rim vertices keep the flat ceramic faces and rounded edge normals distinct.
    internal static class GardenStructures
    {
        private static Mesh _plate, _villaWing, _roofInlay;

        internal static Transform Plate(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool sculpted=false)
        {
            if (_plate == null) _plate = MakePlate(false);
            if (sculpted && _villaWing == null) _villaWing = MakePlate(true);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = sculpted ? _villaWing : _plate;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Mesh MakePlate(bool sculpted)
        {
            const int steps = 6;
            const float radius = .22f;
            var outline = new List<Vector2>();
            var radial = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? 1 : -1, corner < 2 ? 1 : -1) * (.5f - radius);
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (corner + (float)i / steps) * Mathf.PI * .5f;
                    var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    radial.Add(normal);
                    outline.Add(center + normal * radius);
                }
            }
            if (sculpted)
            {
                // Sample the straight runs too. The shallow S along the leading edge sweeps
                // through consecutive balcony layers and leaves clearance for the window wall.
                var rim = new List<Vector2>();
                for (int i=0; i<outline.Count; i++)
                {
                    var a=outline[i];
                    var b=outline[(i+1)%outline.Count];
                    int divisions=Mathf.Max(1,Mathf.CeilToInt((b-a).magnitude/.05f));
                    for (int j=0;j<divisions;j++)
                    {
                        var p=Vector2.Lerp(a,b,(float)j/divisions);
                        rim.Add(Sculpt(p));
                    }
                }
                outline=rim;
                radial.Clear();
                for(int i=0;i<rim.Count;i++)
                {
                    var tangent=rim[(i+1)%rim.Count]-rim[(i+rim.Count-1)%rim.Count];
                    radial.Add(new Vector2(tangent.y,-tangent.x).normalized);
                }
                var lineVertices=new Vector3[rim.Count*2];
                var lineIndices=new int[rim.Count*6];
                for(int i=0;i<rim.Count;i++)
                {
                    Vector2 outer=rim[i]*.91f,inner=outer-radial[i]*.0115f;
                    int v=i*2,next=((i+1)%rim.Count)*2,t=i*6;
                    lineVertices[v]=new Vector3(outer.x,0,outer.y);
                    lineVertices[v+1]=new Vector3(inner.x,0,inner.y);
                    lineIndices[t]=v;lineIndices[t+1]=v+1;lineIndices[t+2]=next;
                    lineIndices[t+3]=v+1;lineIndices[t+4]=next+1;lineIndices[t+5]=next;
                }
                _roofInlay=new Mesh{name="屋顶青色嵌条",vertices=lineVertices,triangles=lineIndices};
                _roofInlay.RecalculateNormals();
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            int n = outline.Count;
            for (int layer = 0; layer < 4; layer++)
            for (int i = 0; i < n; i++)
            {
                var p = outline[i];
                bool top = layer == 0 || layer == 2;
                vertices.Add(new Vector3(p.x, top ? .5f : -.5f, p.y));
                normals.Add(layer == 0 ? Vector3.up : layer == 1 ? Vector3.down : new Vector3(radial[i].x, 0, radial[i].y));
                uv.Add(layer < 2 ? p + Vector2.one * .5f : new Vector2((float)i / n, top ? 1 : 0));
            }
            int topCenter=vertices.Count;
            vertices.Add(Vector3.up*.5f); normals.Add(Vector3.up); uv.Add(Vector2.one*.5f);
            vertices.Add(Vector3.down*.5f); normals.Add(Vector3.down); uv.Add(Vector2.one*.5f);
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                indices.AddRange(new[] {topCenter,j,i,topCenter+1,n+i,n+j});
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i+1) % n, t = 2*n, b = 3*n;
                indices.AddRange(new[] { t+i, t+j, b+i, b+i, t+j, b+j });
            }
            var mesh = new Mesh { name = "圆角悬挑板" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv);
            mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            return mesh;
        }

        internal static void RoofInlay(Transform parent,Vector3 at,float width,float depth,Material edge)
        {
            var strip=new GameObject("屋顶青色嵌条",typeof(MeshFilter),typeof(MeshRenderer));
            strip.transform.SetParent(parent,false);strip.transform.localPosition=at;
            strip.transform.localScale=new Vector3(width,1,depth);
            strip.GetComponent<MeshFilter>().sharedMesh=_roofInlay;
            var renderer=strip.GetComponent<MeshRenderer>();renderer.sharedMaterial=edge;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Vector2 Sculpt(Vector2 point)
        {
            float leading=Mathf.SmoothStep(0,1,.5f-point.y);
            point.y-=leading*(.048f+.071f*Mathf.Cos((point.x+.24f)*Mathf.PI*2));
            point.x+=.045f*Mathf.Sin((point.y+.15f)*Mathf.PI);
            return point;
        }

        internal static Vector3 FrontEdge(Vector3 center,float width,float depth,float fraction)
        {
            const float radius=.22f;
            float turn=Mathf.Clamp(Mathf.Abs(fraction)-(.5f-radius),0,radius);
            float front=-(.5f-radius)-Mathf.Sqrt(radius*radius-turn*turn);
            var p=Sculpt(new Vector2(fraction,front));
            return center+new Vector3(p.x*width,0,p.y*depth);
        }

        internal static void BalconyGlass(Transform root,MiniatureDetailMesh detail,Vector3 center,float width,float depth,Material rail)
        {
            const int panels=16;
            var vertices=new Vector3[(panels+1)*2];
            var uv=new Vector2[vertices.Length];
            var indices=new int[panels*6];
            for(int i=0;i<=panels;i++)
            {
                var foot=FrontEdge(center,width-.23f,depth-.25f,Mathf.Lerp(-.497f,.497f,(float)i/panels))+Vector3.up*.17f;
                vertices[i*2]=foot;
                vertices[i*2+1]=foot+Vector3.up*.68f;
                uv[i*2]=new Vector2((float)i/panels,0);uv[i*2+1]=new Vector2((float)i/panels,1);
                if(i%2==0)detail.Box(foot+Vector3.up*.34f,new Vector3(.043f,.68f,.047f),rail);
                if(i==0)continue;
                int v=(i-1)*2,t=(i-1)*6;
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;
                indices[t+3]=v+1;indices[t+4]=v+3;indices[t+5]=v+2;
                Strut(detail,vertices[v+1],vertices[v+3],.043f,.052f,rail);
            }
            var mesh=new Mesh {name="弧形夹胶玻璃",vertices=vertices,uv=uv,triangles=indices};
            mesh.RecalculateNormals();
            var owner=root.GetComponent<CityLandscapeResources>()??root.gameObject.AddComponent<CityLandscapeResources>();
            owner.Owned.Add(mesh);
            var go=new GameObject("弧形露台栏板",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=DistrictFinishes.VillaPane(balustrade:true);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        internal static void Strut(MiniatureDetailMesh mesh, Vector3 a, Vector3 b, float width, float depth, Material material)
        {
            var axis = b - a;
            mesh.Box((a+b)*.5f, new Vector3(width,axis.magnitude,depth), material, Quaternion.FromToRotation(Vector3.up,axis));
        }

        internal static void SolarFins(MiniatureDetailMesh mesh, Vector3 baseCenter, float span, float depth, int count)
        {
            var cell = DistrictFinishes.Tile("solarCells",Vector2.one*.36f);
            var edge = Mats.Lit(ArtDirection.Hex("B3CAD0"), .38f);
            var stand = Mats.Lit(ArtDirection.Hex("61747E"));
            float width = span / count * .76f;
            var rotation = Quaternion.Euler(-8,0,-31);
            for (int i=0; i<count; i++)
            {
                var foot = baseCenter + Vector3.right * (((i+.5f)/count-.5f)*span);
                var at = foot + Vector3.up * (.18f + .27f*width + .07f*depth);
                mesh.Box((at+foot)*.5f, new Vector3(.11f, at.y-foot.y, depth*.60f),stand);
                mesh.Box(at,new Vector3(width+.08f,.11f,depth+.08f),edge,rotation);
                mesh.Box(at+rotation*Vector3.up*.075f,new Vector3(width,.055f,depth),cell,rotation);
                for(int row=1;row<5;row++)
                    mesh.Box(at+rotation*new Vector3(0,.11f,(row/5f-.5f)*depth),new Vector3(width,.018f,.024f),edge,rotation);
            }
        }

        internal static void VillaSolarBlades(MiniatureDetailMesh mesh,Vector3 center,float width)
        {
            var cells=DistrictFinishes.Tile("solarCells",Vector2.one*.64f,ArtDirection.Hex("7791A1"));
            var backing=Mats.Lit(ArtDirection.Hex("2D485A"),.24f);
            var metal=Mats.Lit(ArtDirection.Hex("A5B8BB"),.28f);
            var baseRail=Mats.Lit(ArtDirection.Hex("617784"),.20f);
            // Bifacial blades share a low mounting rail. Their raised tips leave sky between
            // the three silhouettes when viewed from either corner of the street.
            var cross=new Vector3(.133f,0,-.014f)*width;
            var lean=new Vector3(.192f,.144f,.038f)*width;
            for(int i=-1;i<=1;i++)
            {
                var foot=center+Vector3.up*.085f+Vector3.right*(i*.234f*width-lean.x*.5f);
                var a=foot-cross*.5f;
                var b=foot+cross*.5f;
                var c=foot+lean+cross*.35f;
                var d=foot+lean-cross*.35f;
                void Panel(Material material,Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3)
                {
                    var face=mesh.Part(material);
                    int v=face.V.Count;
                    face.V.Add(p0);face.V.Add(p1);face.V.Add(p2);face.V.Add(p3);
                    face.T.Add(v);face.T.Add(v+1);face.T.Add(v+2);
                    face.T.Add(v);face.T.Add(v+2);face.T.Add(v+3);
                }
                Panel(cells,b,a,d,c);
                Panel(backing,a,b,c,d);
                Strut(mesh,a,b,.046f,.047f,baseRail);
                Strut(mesh,a,d,.035f,.040f,metal);
                Strut(mesh,b,c,.035f,.040f,metal);
                Strut(mesh,c,d,.030f,.035f,metal);
                var knee=Vector3.Lerp(b,c,.48f);
                Strut(mesh,new Vector3(knee.x,foot.y,knee.z-.13f),knee,.045f,.05f,baseRail);
            }
        }

        internal static void ShopDisplayWindows(Transform openFront,bool empty)
        {
            var timber=DistrictFinishes.Tile("walnut",Vector2.one);
            var linen=Mats.Glow(ArtDirection.Hex("D9AC77"),new Color(.77f,.43f,.20f));
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*2.78f;
                // Let premises have fitted, illuminated cabinets; occupied shops retain their own merchandise panel.
                if(empty)
                {
                    Prims.Box(openFront,"空铺陈列柜背板",new Vector3(x,1.32f,-.246f),new Vector3(2.36f,1.61f,.036f),
                        DistrictFinishes.Tile("ivoryPlaster",Vector2.one,ArtDirection.Hex("D6B18B")));
                    for(int level=0;level<2;level++)
                        Prims.Box(openFront,"空铺展架",new Vector3(x,.94f+level*.52f,-.345f),new Vector3(2.24f,.055f,.19f),timber);
                    Prims.Box(openFront,"柜内侧板",new Vector3(x+side*.81f,1.34f,-.34f),new Vector3(.07f,1.48f,.19f),timber);
                }
                Prims.Box(openFront,"橱窗陈列台",new Vector3(x,.54f,-.34f),new Vector3(2.39f,.12f,.28f),timber);
                Prims.Box(openFront,"檐下灯槽挡板",new Vector3(x,2.14f,-.38f),new Vector3(2.36f,.095f,.10f),timber);
                Prims.Box(openFront,"橱窗灯槽",new Vector3(x,2.09f,-.32f),new Vector3(2.14f,.026f,.08f),linen);
                var pane=Prims.Shape(PrimitiveType.Quad,openFront,"低铁橱窗",new Vector3(x,1.32f,-.49f),
                    new Vector3(2.36f,1.63f,1),DistrictFinishes.VillaPane(sheltered:true));
                pane.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        internal static void StreetFront(Transform root, float width, float depth, float height,bool glassPavilion=false)
        {
            var porcelain=DistrictFinishes.Tile("pearlCeramic",new Vector2(2,2));
            var alloy=DistrictFinishes.Tile("brushedAlloy",Vector2.one*.46f);
            var blue=Mats.Lit(ArtDirection.Hex("344F61"),.48f);
            var display=Mats.Glow(ArtDirection.Hex("77CCD1"),new Color(.24f,.81f,.86f));
            var detail=new MiniatureDetailMesh();
            var foldedSilver=glassPavilion?DistrictFinishes.Tile("brushedAlloy",Vector2.one*.46f,ArtDirection.Hex("B6BEBD")):porcelain;
            // The doors and open/shutter variants stay below this wraparound sun screen.
            Plate(root,"悬浮檐口",new Vector3(0,height+.63f,depth*.48f),new Vector3(width+.48f,.29f,depth+.72f),foldedSilver);
            Plate(root,"设备层",new Vector3(0,height+.81f,depth*.48f),new Vector3(width-.48f,.17f,depth-.36f),alloy);
            bool smallPavilion=width<=15 && depth<=14;
            float span=smallPavilion?width+.44f:width*.88f;
            if(glassPavilion)
            {
                // Silver edge and recessed glass turn the corner together; the existing door and shutters remain below.
                var lining=DistrictFinishes.Tile("brushedAlloy",Vector2.one,ArtDirection.Hex("647272"));
                var gilded=Mats.Glow(ArtDirection.Hex("C0936A"),new Color(.71f,.40f,.19f));
                Plate(root,"展廊弧形腰封",new Vector3(0,3.68f,depth*.47f),new Vector3(width+.51f,.32f,depth+1.06f),alloy);
                Plate(root,"展廊腰封暗槽",new Vector3(0,3.49f,depth*.47f),new Vector3(width+.23f,.12f,depth+.81f),lining);
                float baseY=3.82f,ceiling=height+.22f,windowH=ceiling-baseY;
                void Gallery(Vector3 center,float length,float yaw)
                {
                    var rotation=Quaternion.Euler(0,yaw,0);
                    var opening=new GameObject("通长展廊").transform;
                    opening.SetParent(root,false);opening.localPosition=center;opening.localRotation=rotation;
                    int bays=Mathf.Max(2,Mathf.RoundToInt(length/2.7f));
                    float bayW=length/bays;
                    for(int i=0;i<bays;i++)
                    {
                        float x=(i+.5f)*bayW-length*.5f;
                        Prims.Shape(PrimitiveType.Quad,opening,"内墙与陈设",new Vector3(x,0,.55f),
                            new Vector3(bayW,windowH,1),DistrictFinishes.VillaRoom(i==1?1:0));
                        detail.Box(center+rotation*new Vector3(x,-windowH*.36f,.26f),new Vector3(bayW*.83f,.075f,.26f),lining,rotation);
                        detail.Box(center+rotation*new Vector3(x+bayW*.5f,0,0),new Vector3(.058f,windowH,.10f),lining,rotation);
                    }
                    detail.Box(center+rotation*new Vector3(0,windowH*.46f,.21f),new Vector3(length-.12f,.054f,.16f),gilded,rotation);
                    var pane=Prims.Shape(PrimitiveType.Quad,opening,"展廊幕墙",Vector3.zero,new Vector3(length,windowH,1),DistrictFinishes.VillaPane(sheltered:true));
                    pane.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                float windowY=(baseY+ceiling)*.5f;
                Gallery(new Vector3(0,windowY,.01f),width-.12f,0);
                foreach(int side in new[]{-1,1})
                    Gallery(new Vector3(side*(width*.5f-.01f),windowY,depth*.5f),depth-.12f,side<0?90:270);
                Plate(root,"屋顶百叶内庭",new Vector3(0,height+.92f,depth*.49f),new Vector3(width*.86f,.09f,depth*.75f),lining);
                // The shallow transoms still admit warm light above the closed storefront.
                foreach(int side in new[]{-1,1})
                    detail.Box(new Vector3(side*width*.30f,2.66f,-.28f),new Vector3(width*.23f,.11f,.06f),gilded);
            }
            int ribs=Mathf.Clamp(Mathf.RoundToInt(span/.49f),7,32);
            for(int i=0;i<ribs;i++)
            {
                float x=((i+.5f)/ribs-.5f)*span;
                var a=new Vector3(x,height+1.02f,depth*.48f);
                float projection=smallPavilion?1.06f:.77f;
                var b=new Vector3(x,height+1.02f,.13f);
                var c=new Vector3(x,height+.42f,-projection);
                bool endRib=glassPavilion && (i==0||i==ribs-1);
                float lower=glassPavilion?Mathf.Max(4.02f,height-(endRib?1.03f:.28f)):Mathf.Max(3.91f,height-.72f);
                var d=new Vector3(x,lower,-projection);
                float gauge=endRib?.23f:.105f;
                Strut(detail,a,b,gauge,.11f,alloy);
                var from=b;
                for(int section=1;section<=4;section++)
                {
                    float angle=section*Mathf.PI/8;
                    var to=new Vector3(x,b.y-.60f*(1-Mathf.Cos(angle)),b.z-(projection+.13f)*Mathf.Sin(angle));
                    Strut(detail,from,to,gauge,.11f,alloy);
                    from=to;
                }
                if (d.y<c.y) Strut(detail,c,d,gauge,.10f,alloy);
                if(glassPavilion)
                {
                    var start=d;
                    for(int j=1;j<=4;j++)
                    {
                        float angle=j*Mathf.PI/8;
                        var end=d+new Vector3(0,-.30f*Mathf.Sin(angle),.48f*(1-Mathf.Cos(angle)));
                        Strut(detail,start,end,gauge,.11f,alloy);start=end;
                    }
                    // Each return clips to a recessed rail above the unobstructed gallery glazing.
                    var clip=start+Vector3.forward*.38f;
                    Strut(detail,start,clip,gauge,.10f,alloy);
                    if(endRib)
                        Strut(detail,clip,new Vector3(clip.x,Mathf.Max(4.02f,height-.28f)-.30f,clip.z),gauge,.10f,alloy);
                }
            }
            if(glassPavilion)
                detail.Box(new Vector3(0,Mathf.Max(4.02f,height-.28f)-.30f,-.20f),new Vector3(span-.45f,.075f,.12f),blue);
            if(!glassPavilion)detail.Box(new Vector3(0,Mathf.Max(3.87f,height-.76f),smallPavilion?-1.06f:-.77f),new Vector3(span,.13f,.15f),alloy);
            var planter=new StreetPlanting(maintained:true);
            float back=depth*.72f;
            detail.Box(new Vector3(0,height+.94f,back),new Vector3(width*.68f,.22f,depth*.20f),alloy);
            for(int k=0;k<6;k++)
                planter.Add(k==0||k==4?PlantSpecies.Spirea:PlantSpecies.Boxwood,
                    new Vector3((k-2.5f)*width*.105f,height+1.03f,back),width*.14f,.48f,k+3);
            planter.Build(root,"设备顶庭院");
            var sign=new Vector3(width*(glassPavilion?.38f:.30f),1.6f,-.37f);
            detail.Box(sign,new Vector3(width*.19f,1.58f,.11f),blue);
            detail.Box(sign+new Vector3(0,.44f,-.068f),new Vector3(width*.14f,.17f,.035f),display);
            for(int i=0;i<3;i++)
                detail.Box(sign+new Vector3(-.06f,.10f-.18f*i,-.068f),new Vector3(width*(.115f-.019f*i),.04f,.033f),display);
            detail.Build(root,"遮阳鳍片与橱窗显示屏");
        }
    }
}
