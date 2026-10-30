using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Shared material vocabulary: painted timber, enamel steel, warm paper and brass.
    public static class ArtDirection
    {
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
        public static readonly Color Cream = Hex("E5DDCA");
        public static readonly Color Sage = Hex("758A7D");
        public static readonly Color Ink = Hex("293E3D");
        public static readonly Color Wood = Hex("977457");
        public static readonly Color Brass = Hex("C7A36B");
        public static readonly Color Clay = Hex("AD6C55");
        public static readonly Color Blue = Hex("6D8896");

        private static readonly Dictionary<Vector3, Mesh> Bevels = new Dictionary<Vector3, Mesh>();
        // Small objects have physical edge highlights; their existing box colliders stay unchanged.
        public static Mesh BeveledBox(Vector3 size)
        {
            if (Bevels.TryGetValue(size, out var mesh)) return mesh;
            float radius = Mathf.Min(0.035f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.12f);
            Vector3 inner = Vector3.one * 0.5f - new Vector3(radius / size.x, radius / size.y, radius / size.z);
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            void Face(params Vector3[] points)
            {
                Vector3 center = Vector3.zero; foreach (var p in points) center += p;
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), center) < 0) System.Array.Reverse(points);
                int start = vertices.Count;
                for (int i = 0; i < points.Length; i++) { vertices.Add(points[i]); uv.Add(new Vector2(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0)); }
                for (int i = 1; i < points.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            }
            for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vector3 P(float a, float b) { var p = Vector3.zero; p[axis] = sign * 0.5f; p[u] = a * inner[u]; p[v] = b * inner[v]; return p; }
                Face(P(-1,-1), P(1,-1), P(1,1), P(-1,1));
            }
            for (int axis = 0; axis < 3; axis++)
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vector3 P(float along, bool first) { var p = Vector3.zero; p[axis] = along * inner[axis]; p[u] = a * (first ? 0.5f : inner[u]); p[v] = b * (first ? inner[v] : 0.5f); return p; }
                Face(P(-1,true), P(1,true), P(1,false), P(-1,false));
            }
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Face(new Vector3(x * 0.5f,y * inner.y,z * inner.z), new Vector3(x * inner.x,y * 0.5f,z * inner.z), new Vector3(x * inner.x,y * inner.y,z * 0.5f));
            mesh = new Mesh { name = "Chamfered prop" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Bevels[size] = mesh; return mesh;
        }
    }
}
