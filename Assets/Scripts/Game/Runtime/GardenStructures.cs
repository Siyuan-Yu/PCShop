using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Shared unit plate for the garden villas, shop sunshades and circuit pavilions.
    // Separate top and rim vertices keep the flat ceramic faces and rounded edge normals distinct.
    internal static class GardenStructures
    {
        private static Mesh _plate;

        internal static Transform Plate(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            if (_plate == null) _plate = MakePlate();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = _plate;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Mesh MakePlate()
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
            for (int i = 1; i < n - 1; i++)
            {
                indices.AddRange(new[] { 0, i+1, i, n, n+i, n+i+1 });
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
            var rotation = Quaternion.Euler(-8,0,-21);
            for (int i=0; i<count; i++)
            {
                var foot = baseCenter + Vector3.right * (((i+.5f)/count-.5f)*span);
                var at = foot + Vector3.up * (.19f + .19f*width + .07f*depth);
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
                var a=new Vector3(x,height+.80f,depth*.35f);
                var b=new Vector3(x,height+.78f,-.38f);
                var c=new Vector3(x,height+.43f,-.77f);
                var d=new Vector3(x,3.91f,-.77f);
                Strut(detail,a,b,.105f,.11f,alloy);
                Strut(detail,b,c,.105f,.11f,alloy);
                if (d.y<c.y) Strut(detail,c,d,.095f,.10f,alloy);
            }
            detail.Box(new Vector3(0,3.87f,-.77f),new Vector3(span,.13f,.15f),porcelain);
            var sign=new Vector3(width*.30f,1.6f,-.34f);
            detail.Box(sign,new Vector3(width*.19f,1.58f,.11f),blue);
            detail.Box(sign+new Vector3(0,.44f,-.068f),new Vector3(width*.14f,.17f,.035f),display);
            for(int i=0;i<3;i++)
                detail.Box(sign+new Vector3(-.06f,.10f-.18f*i,-.068f),new Vector3(width*(.115f-.019f*i),.04f,.033f),display);
            detail.Build(root,"遮阳鳍片与橱窗显示屏");
        }
    }
}
