using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    // Small reusable meshes for authored silhouettes and bevels. Physics stays on the original furniture volumes.
    internal static class PropForms
    {
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        internal static Color InkColor => ArtDirection.Hex("382F28");
        internal static Material Ink => Mats.Lit(InkColor, .13f);
        internal static Material Cream => Mats.Lit(ArtDirection.Hex("E8DDC6"), .12f);
        internal static Material Pigment(Color color) => Mats.LitTex(color, Vector2.one, .12f);
        internal static Material Pigment(string hex) => Pigment(ArtDirection.Hex(hex));
        internal static Material Wood(string hex) =>
            IndustrialMaterials.Surface("wood", ArtDirection.Hex(hex), Vector2.one);

        internal static Transform Group(Transform parent, string name, Vector3 at = default)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = at;
            return group;
        }

        internal static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material mat,
            bool pick = false) => Prims.Box(parent, name, at, size, mat, pick).transform;

        internal static GameObject Box(Transform parent, string name, float x, float y, float z,
            float width, float height, float depth, Material material) =>
            Prims.Box(parent, name, new Vector3(x, y, z), new Vector3(width, height, depth), material);

        internal static Transform Tube(Transform parent, string name, Vector3 from, Vector3 to, float diameter, Material mat)
        {
            Vector3 axis = to - from;
            var link = Prims.Shape(PrimitiveType.Cylinder, parent, name, (from + to) * .5f,
                new Vector3(diameter, axis.magnitude * .5f, diameter), mat).transform;
            link.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            return link;
        }

        internal static Transform Mesh(Transform parent, string name, string key, Material mat, Func<Mesh> build)
        {
            if (!Meshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = build();
                mesh.name = name;
                Meshes[key] = mesh;
            }
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            part.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return part;
        }

        internal static Transform Revolve(Transform parent, string name, Material material, string profileName,
            Vector3 at, Vector3 scale, params Vector2[] profile)
        {
            var root = Mesh(parent, name, "revolve:" + profileName, material, () =>
            {
                const int sides = 16;
                var form = new Faces();
                Vector3 Point(int level, int i)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    return new Vector3(Mathf.Cos(angle) * profile[level].x, profile[level].y, Mathf.Sin(angle) * profile[level].x);
                }
                for (int y = 0; y < profile.Length - 1; y++)
                for (int i = 0; i < sides; i++)
                    form.Face(Point(y, i), Point(y + 1, i), Point(y + 1, i + 1), Point(y, i + 1));
                return form.Build();
            });
            root.localPosition = at;
            root.localScale = scale;
            return root;
        }

        internal sealed class Faces
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _indices = new List<int>();
            internal void Face(params Vector3[] points)
            {
                int start = _vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    _vertices.Add(points[i]);
                    _uv.Add(new Vector2(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0));
                }
                for (int i = 1; i < points.Length - 1; i++)
                {
                    _indices.Add(start);
                    _indices.Add(start + i);
                    _indices.Add(start + i + 1);
                }
            }

            internal Mesh Build()
            {
                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
