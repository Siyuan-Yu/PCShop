using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Shared unit plate for the garden villas, shop sunshades and circuit pavilions.
    // Separate top and rim vertices keep the flat ceramic faces and rounded edge normals distinct.
    internal static class GardenStructures
    {
        private static Mesh _plate, _villaWing;

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
                        float leading=Mathf.SmoothStep(0,1,.5f-p.y);
                        p.y-=leading*(.048f+.071f*Mathf.Cos((p.x+.24f)*Mathf.PI*2));
                        p.x+=.045f*Mathf.Sin((p.y+.15f)*Mathf.PI);
                        rim.Add(p);
                    }
                }
                outline=rim;
                radial.Clear();
                for(int i=0;i<rim.Count;i++)
                {
                    var tangent=rim[(i+1)%rim.Count]-rim[(i+rim.Count-1)%rim.Count];
                    radial.Add(new Vector2(tangent.y,-tangent.x).normalized);
                }
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

        internal static void StreetFront(Transform root, float width, float depth, float height)
        {
            var porcelain=DistrictFinishes.Tile("pearlCeramic",new Vector2(2,2));
            var alloy=DistrictFinishes.Tile("brushedAlloy",Vector2.one*.46f);
            var blue=Mats.Lit(ArtDirection.Hex("344F61"),.48f);
            var display=Mats.Glow(ArtDirection.Hex("77CCD1"),new Color(.24f,.81f,.86f));
            var detail=new MiniatureDetailMesh();
            // The doors and open/shutter variants stay below this wraparound sun screen.
            Plate(root,"悬浮檐口",new Vector3(0,height+.63f,depth*.48f),new Vector3(width+.48f,.29f,depth+.72f),porcelain);
            Plate(root,"设备层",new Vector3(0,height+.81f,depth*.48f),new Vector3(width-.48f,.17f,depth-.36f),alloy);
            float span=width*.88f;
            int ribs=Mathf.Clamp(Mathf.RoundToInt(span/.49f),7,32);
            for(int i=0;i<ribs;i++)
            {
                float x=((i+.5f)/ribs-.5f)*span;
                var a=new Vector3(x,height+1.02f,depth*.48f);
                var b=new Vector3(x,height+.96f,-.38f);
                var c=new Vector3(x,height+.43f,-.77f);
                var d=new Vector3(x,Mathf.Max(3.91f,height-.72f),-.77f);
                Strut(detail,a,b,.105f,.11f,alloy);
                Strut(detail,b,c,.105f,.11f,alloy);
                if (d.y<c.y) Strut(detail,c,d,.095f,.10f,alloy);
            }
            detail.Box(new Vector3(0,Mathf.Max(3.87f,height-.76f),-.77f),new Vector3(span,.13f,.15f),alloy);
            var planter=new StreetPlanting(maintained:true);
            float back=depth*.72f;
            detail.Box(new Vector3(0,height+.94f,back),new Vector3(width*.68f,.22f,depth*.20f),alloy);
            for(int k=0;k<6;k++)
                planter.Add(k==0||k==4?PlantSpecies.Spirea:PlantSpecies.Boxwood,
                    new Vector3((k-2.5f)*width*.105f,height+1.03f,back),width*.14f,.48f,k+3);
            planter.Build(root,"设备顶庭院");
            var sign=new Vector3(width*.30f,1.6f,-.34f);
            detail.Box(sign,new Vector3(width*.19f,1.58f,.11f),blue);
            detail.Box(sign+new Vector3(0,.44f,-.068f),new Vector3(width*.14f,.17f,.035f),display);
            for(int i=0;i<3;i++)
                detail.Box(sign+new Vector3(-.06f,.10f-.18f*i,-.068f),new Vector3(width*(.115f-.019f*i),.04f,.033f),display);
            detail.Build(root,"遮阳鳍片与橱窗显示屏");
        }
    }
}
