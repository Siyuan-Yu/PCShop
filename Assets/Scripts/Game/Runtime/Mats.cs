using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    /// <summary>运行时材质缓存（URP Lit / Unlit）。同色同参数共用一个材质。</summary>
    public static class Mats
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit;
        private static Shader _unlit;

        /// <summary>夜里要亮起来的材质：原始发光色。由昼夜灯光按时间调亮度。</summary>
        public static readonly List<(Material mat, Color baseEmission)> NightGlow = new List<(Material, Color)>();

        private static Shader LitShader => _lit != null ? _lit : (_lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        private static Shader UnlitShader => _unlit != null ? _unlit : (_unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));

        public static Material Lit(Color c, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"L{c}{smoothness}{metallic}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = key };
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        /// <summary>发光材质。nightOnly 为真时白天几乎不亮，夜里亮起来。</summary>
        public static Material Glow(Color baseColor, Color emission, bool nightOnly = true)
        {
            string key = $"G{baseColor}{emission}{nightOnly}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = key };
            m.SetColor("_BaseColor", baseColor);
            m.color = baseColor;
            m.SetFloat("_Smoothness", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            Cache[key] = m;
            if (nightOnly) NightGlow.Add((m, emission));
            return m;
        }

        public static Material Unlit(Color c)
        {
            string key = $"U{c}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(UnlitShader) { name = key };
            m.SetColor("_BaseColor", c);
            m.color = c;
            if (c.a < 0.999f) MakeTransparent(m);
            Cache[key] = m;
            return m;
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void ClearNightGlow() => NightGlow.Clear();
    }

    public static class Prims
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>低多边形小人：身体 + 头。脚底在原点。</summary>
        public static GameObject Figure(Transform parent, string name, Color body, Color head, float scale = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Shape(PrimitiveType.Capsule, root.transform, "Body", new Vector3(0, 0.55f, 0) * scale, new Vector3(0.45f, 0.55f, 0.35f) * scale, Mats.Lit(body));
            Shape(PrimitiveType.Sphere, root.transform, "Head", new Vector3(0, 1.3f, 0) * scale, Vector3.one * 0.32f * scale, Mats.Lit(head));
            return root;
        }

        /// <summary>扇形网格（视野锥），在 XZ 平面，朝 +Z。</summary>
        public static Mesh Fan(float radius, float halfAngleDeg, int segments = 16)
        {
            var mesh = new Mesh { name = "Fan" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(-halfAngleDeg, halfAngleDeg, i / (float)segments);
                verts[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius);
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
