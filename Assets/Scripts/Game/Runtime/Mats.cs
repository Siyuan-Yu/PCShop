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
            emission *= 0.5f;
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
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= 0.035f && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) <= 3.5f)
                go.GetComponent<MeshFilter>().sharedMesh = ArtDirection.BeveledBox(size);
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

        public static void SignText(Transform parent, string text, Vector3 at, float width, float height, Color color)
        {
            var canvas = new GameObject("Painted lettering", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = at;
            canvas.transform.localScale = Vector3.one * 0.01f;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(width * 100, height * 100);
            var label = PCShopSell.Game.UI.Ui.Label(rect, text, Mathf.RoundToInt(Mathf.Min(height * 76, width * 90 / Mathf.Max(1, text.Length))), TextAnchor.MiddleCenter, color);
            label.fontStyle = FontStyle.Bold;
            PCShopSell.Game.UI.Ui.Fill(label.rectTransform);
        }

        public static GameObject ParkedCar(Transform parent, Vector3 at, Color paint)
        {
            var root = new GameObject("Parked car").transform; root.SetParent(parent, false); root.localPosition = at; root.localRotation = Quaternion.Euler(0, 90, 0);
            Box(root, "Body", new Vector3(0, 0.52f, 0), new Vector3(1.55f, 0.48f, 3.4f), Mats.Lit(paint, 0.3f));
            Box(root, "Cabin", new Vector3(0, 0.94f, -0.15f), new Vector3(1.34f, 0.52f, 1.75f), Mats.Lit(ArtDirection.Ink, 0.35f));
            Box(root, "Roof", new Vector3(0, 1.22f, -0.2f), new Vector3(1.38f, 0.08f, 1.35f), Mats.Lit(paint));
            foreach (int side in new[] { -1, 1 })
            {
                foreach (int axle in new[] { -1, 1 })
                {
                    var wheel = Shape(PrimitiveType.Cylinder, root, "Wheel", new Vector3(side * 0.78f, 0.3f, axle * 1.02f), new Vector3(0.52f, 0.10f, 0.52f), Mats.Lit(ArtDirection.Ink));
                    wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    var hub = Shape(PrimitiveType.Cylinder, root, "Hub", new Vector3(side * 0.89f, 0.3f, axle * 1.02f), new Vector3(0.25f, 0.012f, 0.25f), Mats.Lit(ArtDirection.Cream));
                    hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                Box(root, "Headlight", new Vector3(side * 0.5f, 0.6f, 1.71f), new Vector3(0.32f, 0.14f, 0.025f), Mats.Lit(ArtDirection.Cream));
                Box(root, "TailLight", new Vector3(side * 0.5f, 0.6f, -1.71f), new Vector3(0.28f, 0.12f, 0.025f), Mats.Lit(ArtDirection.Clay));
            }
            Box(root, "Plate", new Vector3(0, 0.42f, 1.72f), new Vector3(0.35f, 0.12f, 0.02f), Mats.Lit(ArtDirection.Blue));
            return root.gameObject;
        }

        /// <summary>低多边形小人：有清晰衣着和肢体轮廓，脚底在原点。</summary>
        public static GameObject Figure(Transform parent, string name, Color body, Color head, float scale = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform Joint(Transform parentJoint, string id, Vector3 at)
            {
                var joint = new GameObject(id).transform;
                joint.SetParent(parentJoint, false);
                joint.localPosition = at;
                return joint;
            }
            Transform visual = Joint(root.transform, "Visual", Vector3.zero);
            visual.localScale = Vector3.one * scale;
            Transform t = Joint(visual, "Torso", Vector3.zero);
            Color shirt = Color.Lerp(body, ArtDirection.Cream, 0.22f);
            GameObject Part(string id, Vector3 p, Vector3 sz, Color c) => Box(t, id, p, sz, Mats.Lit(c, 0.12f));
            Part("Body", new Vector3(0, 0.82f, 0), new Vector3(0.39f, 0.57f, 0.27f), shirt);
            foreach (int side in new[] { -1, 1 })
            {
                string suffix = side < 0 ? "L" : "R";
                Transform hip = Joint(visual, "Hip" + suffix, new Vector3(side * .105f, .56f, 0));
                Box(hip, "Leg" + suffix, new Vector3(0, -.125f, 0), new Vector3(.16f, .25f, .22f), Mats.Lit(ArtDirection.Ink));
                Transform knee = Joint(hip, "Knee" + suffix, new Vector3(0, -.25f, 0));
                Box(knee, "Shin" + suffix, new Vector3(0, -.125f, 0), new Vector3(.15f, .25f, .20f), Mats.Lit(ArtDirection.Ink));
                Transform foot = Joint(knee, "Foot" + suffix, new Vector3(0, -.25f, 0));
                Box(foot, "Shoe", new Vector3(0, -.005f, .055f), new Vector3(.18f, .15f, .31f), Mats.Lit(ArtDirection.Hex("423F38")));
                Transform shoulder = Joint(t, "Shoulder" + suffix, new Vector3(side * .245f, 1.025f, 0));
                Box(shoulder, "Sleeve" + suffix, new Vector3(0, -.175f, 0), new Vector3(.15f, .35f, .23f), Mats.Lit(shirt));
                Box(shoulder, "Hand", new Vector3(0, -.405f, .015f), new Vector3(.12f, .16f, .15f), Mats.Lit(head));
            }
            Part("Neck", new Vector3(0, 1.13f, 0), new Vector3(0.12f, 0.12f, 0.12f), head);
            Shape(PrimitiveType.Sphere, t, "Head", new Vector3(0, 1.3f, 0), Vector3.one * 0.32f, Mats.Lit(head));
            Shape(PrimitiveType.Sphere, t, "Hair", new Vector3(0, 1.425f, -0.025f), new Vector3(0.35f, 0.18f, 0.34f), Mats.Lit(ArtDirection.Hex("443B32")));
            Part("Fringe", new Vector3(-0.06f, 1.405f, 0.105f), new Vector3(0.22f, 0.09f, 0.1f), ArtDirection.Hex("443B32"));
            Part("Nose", new Vector3(0, 1.29f, 0.164f), new Vector3(0.045f, 0.05f, 0.05f), head);
            foreach (int side in new[] { -1, 1 }) Part("Eye", new Vector3(side * 0.065f, 1.34f, 0.143f), new Vector3(0.023f, 0.026f, 0.015f), ArtDirection.Ink);
            root.AddComponent<FigureMotion>().Initialize(visual, t, scale);
            return root;
        }

        /// <summary>
        /// 按衣服给 Figure 换装：T 恤（橙）/ 衬衫（白）/ 夹克（棕）/ 西装（深灰 + 领带），
        /// 同时给所有小人统一加上裤子和头发。可以反复调用，旧的衣着配件会被换掉。
        /// </summary>
        public static void Dress(GameObject figure, string outfitId)
        {
            var motion = figure.GetComponent<FigureMotion>();
            Transform t = motion.Torso;
            Transform body = t.Find("Body");
            Transform head = t.Find("Head");
            if (body == null || head == null) return;
            float s = head.localScale.x / 0.32f;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform c = t.GetChild(i);
                if (!c.name.StartsWith("Outfit_")) continue;
                c.gameObject.SetActive(false);
                Kill.It(c.gameObject);
            }

            Color top, pants;
            switch (outfitId)
            {
                case "shirt": top = new Color(0.92f, 0.93f, 0.95f); pants = new Color(0.20f, 0.22f, 0.30f); break;
                case "jacket": top = new Color(0.36f, 0.27f, 0.18f); pants = new Color(0.18f, 0.22f, 0.35f); break;
                case "suit": top = new Color(0.17f, 0.19f, 0.26f); pants = new Color(0.14f, 0.15f, 0.20f); break;
                default: top = ArtDirection.Clay; pants = new Color(0.30f, 0.35f, 0.45f); break;
            }
            body.GetComponent<Renderer>().sharedMaterial = Mats.Lit(top);

            void Add(string n, Vector3 p, Vector3 sz, Color c) => Box(t, "Outfit_" + n, p * s, sz * s, Mats.Lit(c));
            motion.RecolorLimbs(Mats.Lit(pants), Mats.Lit(top));

            switch (outfitId)
            {
                case "shirt":
                    Add("Collar", new Vector3(0, 1.0f, 0.0f), new Vector3(0.26f, 0.06f, 0.2f), Color.white);
                    Add("Buttons", new Vector3(0, 0.7f, 0.17f), new Vector3(0.03f, 0.5f, 0.02f), new Color(0.7f, 0.72f, 0.78f));
                    break;
                case "jacket":
                    Add("Collar", new Vector3(0, 1.0f, 0.0f), new Vector3(0.3f, 0.07f, 0.24f), new Color(0.28f, 0.2f, 0.13f));
                    Add("Zip", new Vector3(0, 0.66f, 0.17f), new Vector3(0.03f, 0.6f, 0.02f), new Color(0.8f, 0.78f, 0.7f));
                    Add("Pocket", new Vector3(0.12f, 0.45f, 0.14f), new Vector3(0.1f, 0.12f, 0.03f), new Color(0.28f, 0.2f, 0.13f));
                    break;
                case "suit":
                    Add("Shirt", new Vector3(0, 0.82f, 0.16f), new Vector3(0.11f, 0.34f, 0.02f), Color.white);
                    Add("Tie", new Vector3(0, 0.74f, 0.175f), new Vector3(0.05f, 0.36f, 0.02f), new Color(0.75f, 0.12f, 0.15f));
                    Add("LapelL", new Vector3(-0.08f, 0.8f, 0.165f), new Vector3(0.05f, 0.34f, 0.02f), new Color(0.1f, 0.11f, 0.15f));
                    Add("LapelR", new Vector3(0.08f, 0.8f, 0.165f), new Vector3(0.05f, 0.34f, 0.02f), new Color(0.1f, 0.11f, 0.15f));
                    break;
            }
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
