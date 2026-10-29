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

        // ---------- 贴图材质（地面质感、带窗户的墙面） ----------

        private static Texture2D _noise;
        private static Texture2D _windowAlbedo;
        private static Texture2D _windowEmission;

        /// <summary>灰度噪点贴图（0.78~1），乘在底色上让大面积的地面不再是一整块平色。</summary>
        public static Texture2D NoiseTex
        {
            get
            {
                if (_noise != null) return _noise;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "Noise", wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float low = Mathf.PerlinNoise(x * 0.05f + 11f, y * 0.05f + 3f);
                        float fine = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                        float v = Mathf.Clamp01(0.78f + 0.14f * low + 0.08f * fine);
                        byte b = (byte)(v * 255f);
                        px[y * n + x] = new Color32(b, b, b, 255);
                    }
                t.SetPixels32(px);
                t.Apply(true);
                return _noise = t;
            }
        }

        /// <summary>带噪点贴图的地面材质。</summary>
        public static Material LitTex(Color c, Vector2 tiling, float smoothness = 0.2f)
        {
            string key = $"T{c}{tiling}{smoothness}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = key };
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", NoiseTex);
            m.mainTexture = NoiseTex;
            m.mainTextureScale = tiling;
            Cache[key] = m;
            return m;
        }

        private static void BuildWindowTextures()
        {
            if (_windowAlbedo != null) return;
            const int cell = 64, cells = 4, n = cell * cells;
            var rng = new System.Random(4242);
            _windowAlbedo = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "WindowAlbedo", wrapMode = TextureWrapMode.Repeat };
            _windowEmission = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "WindowEmission", wrapMode = TextureWrapMode.Repeat };
            var a = new Color32[n * n];
            var e = new Color32[n * n];
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    bool lit = rng.NextDouble() < 0.55;
                    var warm = rng.NextDouble() < 0.8 ? new Color32(255, 214, 140, 255) : new Color32(150, 215, 255, 255);
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            int px = cx * cell + x, py = cy * cell + y;
                            bool pane = x >= 12 && x < cell - 12 && y >= 14 && y < cell - 10;
                            bool frame = !pane && x >= 9 && x < cell - 9 && y >= 11 && y < cell - 7;
                            Color32 albedo = pane ? new Color32(36, 46, 62, 255) : frame ? new Color32(215, 215, 215, 255) : new Color32(255, 255, 255, 255);
                            a[py * n + px] = albedo;
                            e[py * n + px] = pane && lit ? warm : new Color32(0, 0, 0, 255);
                        }
                }
            _windowAlbedo.SetPixels32(a);
            _windowAlbedo.Apply(true);
            _windowEmission.SetPixels32(e);
            _windowEmission.Apply(true);
        }

        /// <summary>
        /// 带窗户的墙面材质：每栋楼单独一个材质，用贴图偏移让亮灯的窗户各不相同。
        /// columns/rows 是这面墙上窗户的列数/行数；夜里窗户按 NightGlow 亮起。
        /// </summary>
        public static Material WindowWall(Color wall, int columns, int rows, int seed)
        {
            BuildWindowTextures();
            var m = new Material(LitShader) { name = "WindowWall" };
            m.SetColor("_BaseColor", wall);
            m.color = wall;
            m.SetFloat("_Smoothness", 0.35f);
            m.SetTexture("_BaseMap", _windowAlbedo);
            m.mainTexture = _windowAlbedo;
            m.mainTextureScale = new Vector2(Mathf.Max(1, columns) / 4f, Mathf.Max(1, rows) / 4f);
            m.mainTextureOffset = new Vector2((seed % 4) / 4f, ((seed / 4) % 4) / 4f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", _windowEmission);
            Color baseEmission = new Color(2.2f, 1.9f, 1.4f);
            m.SetColor("_EmissionColor", baseEmission * 0.08f);
            NightGlow.Add((m, baseEmission));
            return m;
        }
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
