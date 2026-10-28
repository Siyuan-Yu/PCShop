using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace PCShopSell.Game.Procedural
{
    /// <summary>
    /// 用代码生成一条街区：不依赖任何美术素材，只用方块、圆柱、球和发光材质。
    /// 约定：相机从 (-x, +y, -z) 方向斜俯视，所以建筑的 -Z 面和 -X 面是"正面"。
    /// 拆成多个 partial 文件：地面与基础构件 / 建筑 / 街道设施 / 车辆与行人。
    /// </summary>
    public sealed partial class ProceduralCityBuilder
    {
        private const float CellSize = 14f;
        private const float RoadHalfWidth = 5f;
        private const float BlockStart = 8f;
        private const float WorldHalf = 95f;

        private static readonly Color[] WallPalette =
        {
            new Color(0.82f, 0.78f, 0.72f), new Color(0.55f, 0.62f, 0.70f),
            new Color(0.72f, 0.55f, 0.50f), new Color(0.45f, 0.50f, 0.55f),
            new Color(0.78f, 0.70f, 0.55f), new Color(0.35f, 0.40f, 0.48f),
            new Color(0.62f, 0.72f, 0.66f), new Color(0.80f, 0.64f, 0.55f),
        };

        private static readonly Color[] NeonPalette =
        {
            new Color(1.0f, 0.15f, 0.55f), new Color(0.1f, 0.85f, 1.0f),
            new Color(1.0f, 0.75f, 0.1f), new Color(0.55f, 0.35f, 1.0f),
            new Color(0.2f, 1.0f, 0.55f),
        };

        private static readonly Color[] AwningPalette =
        {
            new Color(0.85f, 0.25f, 0.25f), new Color(0.15f, 0.55f, 0.55f),
            new Color(0.90f, 0.70f, 0.20f), new Color(0.30f, 0.35f, 0.75f),
        };

        private static readonly Color[] CarPalette =
        {
            new Color(0.80f, 0.15f, 0.15f), new Color(0.15f, 0.30f, 0.70f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.15f, 0.15f, 0.18f),
            new Color(0.90f, 0.75f, 0.15f), new Color(0.20f, 0.55f, 0.35f),
        };

        private static readonly Color[] ShirtPalette =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.25f, 0.55f, 0.90f),
            new Color(0.95f, 0.80f, 0.25f), new Color(0.40f, 0.75f, 0.45f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.65f, 0.35f, 0.80f),
        };

        private static readonly Color[] HairPalette =
        {
            new Color(0.08f, 0.07f, 0.07f), new Color(0.25f, 0.15f, 0.08f),
            new Color(0.55f, 0.40f, 0.15f), new Color(0.70f, 0.70f, 0.72f),
        };

        private static readonly Color[] PantsPalette =
        {
            new Color(0.15f, 0.17f, 0.25f), new Color(0.25f, 0.25f, 0.28f),
            new Color(0.40f, 0.33f, 0.25f), new Color(0.12f, 0.12f, 0.14f),
        };

        private readonly Random _rng;
        private readonly CityLookProfile _look;
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private Mesh _cube, _quad, _sphere, _cylinder, _capsule;
        private Shader _shader;
        private Transform _root;
        private Texture2D _noiseTex, _paveTex, _asphaltTex, _blobTex;
        private Material _blobMat;
        private int _lampLights;
        private int _shopLights;

        public Vector3 HeroFrontCenter { get; private set; }

        public ProceduralCityBuilder(CityLookProfile look, int seed)
        {
            _look = look;
            _rng = new Random(seed);
        }

        public GameObject Build()
        {
            LoadMeshes();
            MakeTextures();
            _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Standard");

            var rootGo = new GameObject("ProceduralCity_" + _look.Name);
            _root = rootGo.transform;

            BuildGround();
            BuildLotsAndBuildings();
            BuildHeroMall();
            BuildStreetFurniture();
            BuildStreetProps();
            BuildCars();
            BuildPeople();
            return rootGo;
        }

        // ------------------------------------------------------------------ 地面

        private void BuildGround()
        {
            Cube("Asphalt", new Vector3(0, -0.1f, 0), new Vector3(WorldHalf * 2, 0.2f, WorldHalf * 2),
                LitTex(new Color(0.16f, 0.17f, 0.19f), _asphaltTex, new Vector2(30, 30), _look.RoadSmoothness), false);

            float len = WorldHalf - RoadHalfWidth;
            var pave = LitTex(new Color(0.60f, 0.61f, 0.64f), _paveTex, new Vector2(len / 6f, len / 6f),
                Mathf.Max(0.15f, _look.RoadSmoothness * 0.7f));
            var curb = Lit(new Color(0.68f, 0.68f, 0.70f), 0.2f);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float c = RoadHalfWidth + len / 2f;
                    Cube("Block", new Vector3(sx * c, 0.15f, sz * c), new Vector3(len, 0.3f, len), pave, false);

                    // 路缘石
                    Cube("CurbX", new Vector3(sx * c, 0.17f, sz * (RoadHalfWidth + 0.12f)),
                        new Vector3(len, 0.34f, 0.24f), curb, false);
                    Cube("CurbZ", new Vector3(sx * (RoadHalfWidth + 0.12f), 0.17f, sz * c),
                        new Vector3(0.24f, 0.34f, len), curb, false);
                }
            }

            // 车道虚线
            var white = Lit(new Color(0.85f, 0.85f, 0.80f), 0.2f);
            for (float t = -WorldHalf + 2; t < WorldHalf; t += 4f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 4f) continue;
                Cube("LaneX", new Vector3(t, 0.01f, 0), new Vector3(2f, 0.02f, 0.25f), white, false);
                Cube("LaneZ", new Vector3(0, 0.01f, t), new Vector3(0.25f, 0.02f, 2f), white, false);
            }

            // 斑马线
            for (int i = -4; i <= 4; i++)
            {
                float o = i * 1.1f;
                Cube("CrossA", new Vector3(RoadHalfWidth + 2.5f, 0.012f, o), new Vector3(2.4f, 0.02f, 0.55f), white, false);
                Cube("CrossB", new Vector3(-RoadHalfWidth - 2.5f, 0.012f, o), new Vector3(2.4f, 0.02f, 0.55f), white, false);
                Cube("CrossC", new Vector3(o, 0.012f, RoadHalfWidth + 2.5f), new Vector3(0.55f, 0.02f, 2.4f), white, false);
                Cube("CrossD", new Vector3(o, 0.012f, -RoadHalfWidth - 2.5f), new Vector3(0.55f, 0.02f, 2.4f), white, false);
            }

            // 路面补丁与井盖
            var patch = Lit(new Color(0.12f, 0.12f, 0.14f), 0.5f);
            var manhole = Lit(new Color(0.08f, 0.08f, 0.09f), 0.6f, 0.4f);
            for (int i = 0; i < 26; i++)
            {
                bool alongX = i % 2 == 0;
                float t = -WorldHalf + 8f + (float)_rng.NextDouble() * (WorldHalf * 2 - 16f);
                if (Mathf.Abs(t) < RoadHalfWidth + 2f) continue;
                float lane = ((float)_rng.NextDouble() - 0.5f) * 6f;
                Vector3 pos = alongX ? new Vector3(t, 0.008f, lane) : new Vector3(lane, 0.008f, t);
                if (i % 5 == 0)
                    Cylinder("Manhole", pos + new Vector3(0, 0.01f, 0), new Vector3(1.1f, 0.01f, 1.1f), manhole);
                else
                    Cube("Patch", pos, alongX ? new Vector3(3 + (float)_rng.NextDouble() * 3f, 0.012f, 1.6f)
                        : new Vector3(1.6f, 0.012f, 3 + (float)_rng.NextDouble() * 3f), patch, false);
            }
        }

        // ------------------------------------------------------------------ 基础构件

        private void LoadMeshes()
        {
            _cube = Harvest(PrimitiveType.Cube);
            _quad = Harvest(PrimitiveType.Quad);
            _sphere = Harvest(PrimitiveType.Sphere);
            _cylinder = Harvest(PrimitiveType.Cylinder);
            _capsule = Harvest(PrimitiveType.Capsule);
        }

        private static Mesh Harvest(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            return mesh;
        }

        private GameObject NewMesh(string name, Mesh mesh, Material mat, bool castShadow, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, bool castShadow)
        {
            return Cube(null, name, pos, scale, mat, castShadow);
        }

        private GameObject Cube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, bool castShadow)
        {
            var go = NewMesh(name, _cube, mat, castShadow, parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            return go;
        }

        private GameObject RotCube(string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, bool castShadow)
        {
            var go = Cube(name, pos, scale, mat, castShadow);
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        private GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = NewMesh(name, _cylinder, mat, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private GameObject Sphere(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = NewMesh(name, _sphere, mat, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private GameObject Capsule(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = NewMesh(name, _capsule, mat, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>朝向 outwardNormal 的面片（可见面朝外）。</summary>
        private GameObject Quad(string name, Vector3 pos, Vector3 outwardNormal, Vector3 scale, Material mat)
        {
            var go = NewMesh(name, _quad, mat, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(-outwardNormal.normalized);
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>地面上的柔和接触阴影（半透明黑色渐变圆）。</summary>
        private void Blob(Vector3 groundPos, float sizeX, float sizeZ, float alpha = 0.55f)
        {
            if (_blobMat == null)
            {
                _blobMat = new Material(_shader) { name = "BlobShadow" };
                _blobMat.SetColor("_BaseColor", new Color(0, 0, 0, 1));
                _blobMat.SetTexture("_BaseMap", _blobTex);
                _blobMat.SetFloat("_Surface", 1f);
                _blobMat.SetFloat("_Blend", 0f);
                _blobMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _blobMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _blobMat.SetFloat("_ZWrite", 0f);
                _blobMat.SetFloat("_Smoothness", 0f);
                _blobMat.SetOverrideTag("RenderType", "Transparent");
                _blobMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _blobMat.renderQueue = (int)RenderQueue.Transparent;
                _blobMat.SetShaderPassEnabled("ShadowCaster", false);
            }

            var go = NewMesh("Blob", _quad, _blobMat, false);
            go.transform.position = groundPos + new Vector3(0, 0.02f, 0);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
            // 通过顶点色以外的办法调整强度：缩放 alpha 用独立实例太贵，这里统一强度即可
            _ = alpha;
        }

        private static Color Tint(Color c, float k)
        {
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        private float Rand(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        // ------------------------------------------------------------------ 材质与贴图

        private Material Lit(Color color, float smoothness, float metallic = 0f)
        {
            string key = "L" + color + smoothness + metallic;
            if (_materials.TryGetValue(key, out var m)) return m;
            m = new Material(_shader) { name = "Lit_" + _materials.Count };
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            _materials[key] = m;
            return m;
        }

        private Material LitTex(Color color, Texture2D tex, Vector2 tiling, float smoothness)
        {
            string key = "T" + color + tex.name + tiling + smoothness;
            if (_materials.TryGetValue(key, out var m)) return m;
            m = new Material(_shader) { name = "LitTex_" + _materials.Count };
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            _materials[key] = m;
            return m;
        }

        private Material Emissive(Color color, float intensity)
        {
            string key = "E" + color + Mathf.Round(intensity * 20f);
            if (_materials.TryGetValue(key, out var m)) return m;
            m = new Material(_shader) { name = "Emissive_" + _materials.Count };
            SetColor(m, color * 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", color * intensity);
            _materials[key] = m;
            return m;
        }

        private static void SetColor(Material m, Color c)
        {
            c.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private void MakeTextures()
        {
            const int n = 64;
            _noiseTex = NewTex("noise", n);
            _paveTex = NewTex("pave", n);
            _asphaltTex = NewTex("asphalt", n);
            _blobTex = NewTex("blob", n);

            float s = (float)_rng.NextDouble() * 50f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // 墙面：大块渐变 + 竖向污渍
                    float a = Mathf.PerlinNoise(x * 0.08f + s, y * 0.08f + 7f) * 0.6f
                              + Mathf.PerlinNoise(x * 0.35f, y * 0.35f + s) * 0.4f;
                    float streak = Mathf.PerlinNoise(x * 0.7f + 11f, y * 0.04f + s);
                    float v = (0.80f + 0.20f * a) * (1f - 0.10f * streak);
                    _noiseTex.SetPixel(x, y, new Color(v, v, v, 1f));

                    // 人行道：地砖缝
                    float p = 0.88f + 0.12f * Mathf.PerlinNoise(x * 0.2f + s, y * 0.2f);
                    if (x % 32 < 2 || y % 32 < 2) p *= 0.70f;
                    _paveTex.SetPixel(x, y, new Color(p, p, p, 1f));

                    // 柏油：细碎颗粒
                    float q = 0.80f + 0.20f * Mathf.PerlinNoise(x * 0.6f + s, y * 0.6f);
                    if (Mathf.PerlinNoise(x * 1.7f, y * 1.7f + s) > 0.78f) q *= 0.75f;
                    _asphaltTex.SetPixel(x, y, new Color(q, q, q, 1f));

                    // 接触阴影：中心不透明，向外渐隐
                    float dx = (x - n / 2f) / (n / 2f);
                    float dy = (y - n / 2f) / (n / 2f);
                    float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = r * r * 0.85f;
                    _blobTex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            _noiseTex.Apply();
            _paveTex.Apply();
            _asphaltTex.Apply();
            _blobTex.Apply();
            _blobTex.wrapMode = TextureWrapMode.Clamp;
        }

        private static Texture2D NewTex(string name, int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
        }
    }
}
