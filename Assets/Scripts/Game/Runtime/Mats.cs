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
        public static readonly List<(Material mat, Color baseEmission, float daylight)> NightGlow = new List<(Material, Color, float)>();

        private static Shader LitShader => _lit != null ? _lit : (_lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        private static Shader UnlitShader => _unlit != null ? _unlit : (_unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));

        public static Material Lit(Color c, float smoothness = 0.2f, float metallic = 0f, bool receiveShadows = true)
        {
            string key = $"L{c}{smoothness}{metallic}{receiveShadows}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = key };
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (!receiveShadows && m.HasProperty("_ReceiveShadows"))
            {
                m.SetFloat("_ReceiveShadows", 0f);
                m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>发光材质。nightOnly 为真时白天几乎不亮，夜里亮起来。</summary>
        public static Material Glow(Color baseColor, Color emission, bool nightOnly = true)
        {
            emission *= 0.5f;
            string key = $"G{baseColor}{emission}{nightOnly}";
            if (Cache.TryGetValue(key, out Material m) && m != null)
            {
                if (nightOnly) RegisterGlow(m,emission,.08f);
                return m;
            }
            m = new Material(LitShader) { name = key };
            m.SetColor("_BaseColor", baseColor);
            m.color = baseColor;
            m.SetFloat("_Smoothness", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            Cache[key] = m;
            if (nightOnly) RegisterGlow(m,emission,.08f);
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

        private static readonly HashSet<Material> NightGlowMaterials=new HashSet<Material>();
        private static void RegisterGlow(Material material,Color emission,float daylight)
        {
            if (NightGlowMaterials.Add(material)) NightGlow.Add((material,emission,daylight));
        }
        public static void RestoreBakedGlow(Material material,Color emission,float daylight)
        { if(material!=null)RegisterGlow(material,emission,daylight); }
        public static void ClearNightGlow()
        {
            NightGlowMaterials.Clear();
            NightGlow.Clear();
        }

        private static Texture2D _roofTiles;
        public static Material TileRoof(Color color)
        {
            string key="Roof"+color;
            if(Cache.TryGetValue(key,out Material material)&&material!=null)return material;
            if(_roofTiles==null)
            {
                const int size=64;
                _roofTiles=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="MiniatureRoofTiles",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    int row=y/16, tileX=(x+(row%2)*8)%16;
                    float shade=y%16<2||tileX<1?.68f:.87f+.12f*Mathf.Sin(tileX/16f*Mathf.PI);
                    pixels[y*size+x]=new Color(shade,shade,shade,1);
                }
                _roofTiles.SetPixels(pixels);_roofTiles.Apply(true);
            }
            material=new Material(LitShader){name=key};material.SetColor("_BaseColor",color);material.SetTexture("_BaseMap",_roofTiles);material.SetFloat("_Smoothness",.14f);
            Cache[key]=material;return material;
        }

        // ---------- 贴图材质（地面质感、带窗户的墙面） ----------

        private static Texture2D _noise;
        private static Texture2D _windowAlbedo;
        private static Texture2D _windowEmission;
        private static readonly Dictionary<Color,Texture2D> WindowPalettes=new Dictionary<Color,Texture2D>();

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
                        float v = Mathf.Clamp01(0.94f + 0.045f * low + 0.015f * fine);
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

        private static void BuildWindowTextures(Color wall)
        {
            if(WindowPalettes.TryGetValue(wall,out _windowAlbedo))return;
            const int cell = 64, cells = 4, n = cell * cells;
            var rng = new System.Random(4242);
            _windowAlbedo = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = "IndustrialMasonryWindowAtlas", wrapMode = TextureWrapMode.Repeat, anisoLevel=4 };
            if(_windowEmission==null)_windowEmission = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "WindowEmission", wrapMode = TextureWrapMode.Repeat };
            var a = new Color32[n * n];
            var e = new Color32[n * n];
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    bool lit = rng.NextDouble() < 0.55;
                    var warm = rng.NextDouble() < 0.88 ? new Color32(255, 197, 111, 255) : new Color32(175, 215, 249, 255);
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            int px = cx * cell + x, py = cy * cell + y;
                            bool pane = x >= 11 && x < cell - 11 && y >= 12 && y < cell - 9;
                            bool frame = !pane && x >= 8 && x < cell - 8 && y >= 9 && y < cell - 6;
                            bool mullion=pane&&(x>=30&&x<=32||y>=34&&y<=36);
                            float reflection=Mathf.Clamp01((float)y/cell)*.30f+(x+y>71?.16f:0);
                            Color glass=Color.Lerp(new Color32(43,59,73,255),new Color32(124,151,165,255),reflection);
                            Color32 albedo = pane ? (Color32)(mullion?new Color32(68,77,82,255):glass) : frame ? new Color32(111,114,111,255) : (Color32)wall;
                            if(!pane&&!frame)
                            {
                                float mortar=y%12==0||((x+(y/12%2)*12)%24==0)? .77f: .97f+.03f*Mathf.PerlinNoise(px*.27f,py*.27f);
                                albedo=(Color32)((Color)albedo*mortar);
                            }
                            a[py * n + px] = albedo;
                            float curtain=(x<17||x>46)?.58f:Mathf.Lerp(.65f,1f,Mathf.Sin(Mathf.PI*y/cell));
                            e[py * n + px] = pane && !mullion && lit ? (Color32)((Color)warm * curtain) : new Color32(0, 0, 0, 255);
                        }
                }
            _windowAlbedo.SetPixels32(a);
            _windowAlbedo.Apply(true);
            _windowEmission.SetPixels32(e);
            _windowEmission.Apply(true);
            WindowPalettes[wall]=_windowAlbedo;
        }

        /// <summary>
        /// 带窗户的墙面材质：每栋楼单独一个材质，用贴图偏移让亮灯的窗户各不相同。
        /// columns/rows 是这面墙上窗户的列数/行数；夜里窗户按 NightGlow 亮起。
        /// </summary>
        public static Material WindowWall(Color wall, int columns, int rows, int seed)
        {
            BuildWindowTextures(wall);
            var m = new Material(LitShader) { name = "WindowWall" };
            m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            m.SetFloat("_Smoothness", 0.24f);
            m.SetTexture("_BaseMap", _windowAlbedo);
            m.mainTexture = _windowAlbedo;
            m.mainTextureScale = new Vector2(Mathf.Max(1, columns) / 4f, Mathf.Max(1, rows) / 4f);
            m.mainTextureOffset = new Vector2((seed % 4) / 4f, ((seed / 4) % 4) / 4f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", _windowEmission);
            Color baseEmission = new Color(2.3f, 1.55f, .68f);
            m.SetColor("_EmissionColor", baseEmission * .19f);
            RegisterGlow(m,baseEmission,.19f);
            return m;
        }
    }

    public static class Prims
    {
        private static readonly Dictionary<PrimitiveType,Mesh> DecorativeMeshes=new Dictionary<PrimitiveType,Mesh>();
        private static GameObject CreateGeometry(PrimitiveType type,bool collider)
        {
            // Keep Unity's original collider construction for interactive/solid objects.
            // Decorations only need the built-in shared mesh, not transient physics shapes.
            if(collider)return GameObject.CreatePrimitive(type);
            if(!DecorativeMeshes.TryGetValue(type,out var mesh)||mesh==null)
            {
                var source=GameObject.CreatePrimitive(type);
                mesh=source.GetComponent<MeshFilter>().sharedMesh;
                DecorativeMeshes[type]=mesh;
                Object.DestroyImmediate(source);
            }
            var go=new GameObject(type.ToString(),typeof(MeshFilter),typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            return go;
        }
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = false)
        {
            GameObject go = CreateGeometry(PrimitiveType.Cube,collider);
            if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= 0.035f && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) <= 3.5f)
                go.GetComponent<MeshFilter>().sharedMesh = ArtDirection.BeveledBox(size);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = false)
        {
            GameObject go = CreateGeometry(type,collider);
            go.name = name;
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
            var root = VehiclePropArt.Car(parent, "c_city", paint);
            root.name = "Parked car";
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0, 90, 0);
            return root.gameObject;
        }

        /// <summary>Articulated 3D character with its gameplay root at foot level.</summary>
        public static GameObject Figure(Transform parent, string name, Color body, Color head, float scale = 1f) =>
            FigureForPerson(parent, name, body, head, scale, null);

        internal static GameObject FigureForPerson(Transform parent, string name, Color body, Color head,
            float scale, string personId, string appearanceSeed = null)
        {
            var appearance = NpcLook.ForPerson(personId);
            return BuildFigure(parent, name, body, head, scale, appearance, personId, appearanceSeed);
        }

        internal static GameObject Figure(Transform parent, string name, Color body, Color head,
            float scale, FigureAppearance appearance) =>
            BuildFigure(parent, name, body, head, scale, appearance, null, null);

        private static GameObject BuildFigure(Transform parent, string name, Color body, Color head,
            float scale, FigureAppearance appearance, string personId, string appearanceSeed)
        {
            // 足球练习等仍传枚举的入口也统一到人物身份，避免出现旧版教练/店员。
            if(appearance!=FigureAppearance.Npc&&appearance!=FigureAppearance.Player)
            {
                personId=appearance switch {FigureAppearance.PhoneClerk=>"keeper_phone",FigureAppearance.Classmate=>"res_coder",FigureAppearance.Florist=>"res_flower",_=>"life_coach"};
                appearance=FigureAppearance.Npc;
            }
            var npc = appearance == FigureAppearance.Npc
                ? NpcLook.Select(personId, appearanceSeed ?? name, head, body) : default;
            float width = 1f;
            switch (appearance)
            {
                case FigureAppearance.Npc: scale *= npc.Height; width = npc.Width; break;
                case FigureAppearance.PhoneClerk: scale *= .94f; break;
                case FigureAppearance.Florist: scale *= .96f; break;
                case FigureAppearance.Coach: scale *= 1.03f; break;
            }

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localScale = new Vector3(scale * width, scale, scale);
            var torso = new GameObject("Torso").transform;
            torso.SetParent(visual, false);

            var model = root.AddComponent<PlayerCharacterModel>();
            model.Build(visual, torso, appearance, npc);
            root.AddComponent<FigureMotion>().Initialize(visual, torso, scale, appearance);
            if (appearance == FigureAppearance.Player)
                Dress(root, "tee");
            else if (appearance == FigureAppearance.Npc)
            {
                string cut = personId switch
                {
                    "service_officer" => "patrol",
                    "service_repair" or "CommunityRoofWorker" => "uniform",
                    _ => "jacket"
                };
                Color accent = cut == "patrol" ? ArtDirection.Hex("DBC84C") : ArtDirection.Brass;
                var art=NpcArtCatalog.Find(personId);
                if(art!=null)art.Dress(model);
                else model.DressNpc("painted_jacket", body, ArtDirection.Hex("3E4350"), ArtDirection.Hex("E5DACC"), accent);
            }
            return root;
        }

        /// <summary>Equip the player's current outfit on the shared character rig.</summary>
        public static void Dress(GameObject figure, string outfitId)
        {
            var outfit = PCShopSell.Core.Session.Outfits.Get(outfitId) ?? PCShopSell.Core.Session.Outfits.Get("tee");
            Color top = ArtDirection.Hex(outfit.ColorHex);
            Color trousers = ArtDirection.Hex(outfit.VisualPreset == "formal" ? "303040" : outfit.VisualPreset == "paintedJacket" ? "3C3D43" : "34394F");
            figure.GetComponent<PlayerCharacterModel>().Dress(outfit.VisualPreset, top, trousers);
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
