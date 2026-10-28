using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace PCShopSell.Game.Procedural
{
    /// <summary>
    /// 用代码生成一条街区：不依赖任何美术素材，只用方块、圆柱、球和发光材质。
    /// 约定：相机从 (-x, +y, -z) 方向斜俯视，所以建筑的 -Z 面和 -X 面是"正面"。
    /// </summary>
    public sealed class ProceduralCityBuilder
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
            new Color(0.90f, 0.75f, 0.15f),
        };

        private static readonly Color[] ShirtPalette =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.25f, 0.55f, 0.90f),
            new Color(0.95f, 0.80f, 0.25f), new Color(0.40f, 0.75f, 0.45f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.65f, 0.35f, 0.80f),
        };

        private readonly Random _rng;
        private readonly CityLookProfile _look;
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private Mesh _cube, _quad, _sphere, _cylinder, _capsule;
        private Shader _shader;
        private Transform _root;
        private int _realLightCount;

        public Vector3 HeroFrontCenter { get; private set; }

        public ProceduralCityBuilder(CityLookProfile look, int seed)
        {
            _look = look;
            _rng = new Random(seed);
        }

        public GameObject Build()
        {
            LoadMeshes();
            _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Standard");

            var rootGo = new GameObject("ProceduralCity_" + _look.Name);
            _root = rootGo.transform;

            BuildGround();
            BuildLotsAndBuildings();
            BuildHeroMall();
            BuildStreetFurniture();
            BuildCars();
            BuildPeople();
            return rootGo;
        }

        // ------------------------------------------------------------------ 地面

        private void BuildGround()
        {
            Cube("Asphalt", new Vector3(0, -0.1f, 0), new Vector3(WorldHalf * 2, 0.2f, WorldHalf * 2),
                Lit(new Color(0.13f, 0.14f, 0.16f), 0.35f), false);

            var pave = Lit(new Color(0.46f, 0.47f, 0.50f), 0.15f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float len = WorldHalf - RoadHalfWidth;
                    Cube("Block", new Vector3(sx * (RoadHalfWidth + len / 2), 0.15f, sz * (RoadHalfWidth + len / 2)),
                        new Vector3(len, 0.3f, len), pave, false);
                }
            }

            // 车道虚线与斑马线
            var white = Lit(new Color(0.85f, 0.85f, 0.80f), 0.2f);
            for (float t = -WorldHalf + 2; t < WorldHalf; t += 4f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 4f) continue;
                Cube("LaneX", new Vector3(t, 0.01f, 0), new Vector3(2f, 0.02f, 0.25f), white, false);
                Cube("LaneZ", new Vector3(0, 0.01f, t), new Vector3(0.25f, 0.02f, 2f), white, false);
            }

            for (int i = -4; i <= 4; i++)
            {
                float o = i * 1.1f;
                Cube("CrossA", new Vector3(RoadHalfWidth + 2.5f, 0.012f, o), new Vector3(2.4f, 0.02f, 0.55f), white, false);
                Cube("CrossB", new Vector3(-RoadHalfWidth - 2.5f, 0.012f, o), new Vector3(2.4f, 0.02f, 0.55f), white, false);
                Cube("CrossC", new Vector3(o, 0.012f, RoadHalfWidth + 2.5f), new Vector3(0.55f, 0.02f, 2.4f), white, false);
                Cube("CrossD", new Vector3(o, 0.012f, -RoadHalfWidth - 2.5f), new Vector3(0.55f, 0.02f, 2.4f), white, false);
            }
        }

        // ------------------------------------------------------------------ 普通建筑

        private void BuildLotsAndBuildings()
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    for (int xi = 0; xi < 4; xi++)
                    {
                        for (int zi = 0; zi < 4; zi++)
                        {
                            // 给"电脑城"让位：(-,+) 象限最靠近路口的一排两格
                            if (sx == -1 && sz == 1 && zi == 0 && (xi == 0 || xi == 1)) continue;

                            float cx = sx * (BlockStart + CellSize / 2 + xi * CellSize);
                            float cz = sz * (BlockStart + CellSize / 2 + zi * CellSize);
                            float w = 9f + (float)_rng.NextDouble() * 3f;
                            float d = 9f + (float)_rng.NextDouble() * 3f;

                            // 越远离相机越高，形成层层升高的天际线
                            float far01 = Mathf.Clamp01((cx + cz + 45f) / 95f);
                            float h = 5f + far01 * 13f + (float)_rng.NextDouble() * 5f;
                            Building(cx, cz, w, d, h, WallPalette[_rng.Next(WallPalette.Length)], true);
                        }
                    }
                }
            }
        }

        private void Building(float cx, float cz, float w, float d, float h, Color wall, bool withShop)
        {
            var wallMat = Lit(wall, 0.2f);
            Cube("Body", new Vector3(cx, h / 2f, cz), new Vector3(w, h, d), wallMat, true);

            // 屋顶
            Cube("Roof", new Vector3(cx, h + 0.2f, cz), new Vector3(w + 0.5f, 0.4f, d + 0.5f),
                Lit(new Color(0.18f, 0.19f, 0.22f), 0.2f), true);
            RoofClutter(cx, cz, w, d, h + 0.4f);

            // 窗户：只做相机可见的 -Z 面和 -X 面
            float floorH = 3.2f;
            int floors = Mathf.Max(1, Mathf.FloorToInt((h - 1f) / floorH));
            float colStep = 2.6f;
            int colsZ = Mathf.Max(1, Mathf.FloorToInt((w - 1.2f) / colStep));
            int colsX = Mathf.Max(1, Mathf.FloorToInt((d - 1.2f) / colStep));

            for (int f = withShop ? 1 : 0; f < floors; f++)
            {
                float y = 1.7f + f * floorH;
                for (int c = 0; c < colsZ; c++)
                {
                    float x = cx - (colsZ - 1) * colStep / 2f + c * colStep;
                    Window(new Vector3(x, y, cz - d / 2f - 0.02f), new Vector3(0, 0, -1));
                }

                for (int c = 0; c < colsX; c++)
                {
                    float z = cz - (colsX - 1) * colStep / 2f + c * colStep;
                    Window(new Vector3(cx - w / 2f - 0.02f, y, z), new Vector3(-1, 0, 0));
                }
            }

            if (withShop) ShopFront(cx, cz - d / 2f, w, false);
        }

        private void Window(Vector3 pos, Vector3 normal)
        {
            Material mat;
            if (_rng.NextDouble() < _look.WindowLitChance)
            {
                Color c = _rng.NextDouble() < 0.7
                    ? new Color(1.0f, 0.82f, 0.50f)
                    : new Color(0.65f, 0.85f, 1.0f);
                mat = Emissive(c, _look.EmissiveIntensity * (0.6f + (float)_rng.NextDouble() * 0.6f));
            }
            else
            {
                mat = Lit(_look.UnlitGlassColor, 0.85f);
            }

            var go = NewMesh("Window", _quad, mat, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(-normal);
            go.transform.localScale = new Vector3(1.5f, 1.9f, 1f);
        }

        private void ShopFront(float cx, float frontZ, float w, bool hero)
        {
            float shopW = w * (hero ? 0.8f : 0.7f);
            Color neon = NeonPalette[_rng.Next(NeonPalette.Length)];
            Color awning = AwningPalette[_rng.Next(AwningPalette.Length)];

            // 橱窗
            var glass = NewMesh("ShopGlass", _quad,
                Emissive(new Color(1.0f, 0.85f, 0.6f), _look.EmissiveIntensity * (hero ? 0.9f : 0.55f)), false);
            glass.transform.position = new Vector3(cx, 1.6f, frontZ - 0.03f);
            glass.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            glass.transform.localScale = new Vector3(shopW, 2.4f, 1f);

            // 遮阳篷
            Cube("Awning", new Vector3(cx, 3.1f, frontZ - 0.7f), new Vector3(shopW + 0.6f, 0.15f, 1.5f),
                Lit(awning, 0.3f), true);

            // 招牌
            Cube("Sign", new Vector3(cx, 3.9f, frontZ - 0.1f), new Vector3(shopW * 0.8f, 0.7f, 0.2f),
                Emissive(neon, _look.EmissiveIntensity * 1.1f), false);
        }

        private void RoofClutter(float cx, float cz, float w, float d, float y)
        {
            int units = _rng.Next(1, 4);
            var metal = Lit(new Color(0.55f, 0.57f, 0.60f), 0.5f);
            for (int i = 0; i < units; i++)
            {
                float ox = ((float)_rng.NextDouble() - 0.5f) * (w - 3f);
                float oz = ((float)_rng.NextDouble() - 0.5f) * (d - 3f);
                Cube("AC", new Vector3(cx + ox, y + 0.5f, cz + oz), new Vector3(1.6f, 1f, 1.2f), metal, true);
            }

            if (_rng.NextDouble() < 0.3)
            {
                Cylinder("Tank", new Vector3(cx + w * 0.25f, y + 1.2f, cz + d * 0.2f), new Vector3(1.8f, 1.2f, 1.8f),
                    Lit(new Color(0.40f, 0.30f, 0.25f), 0.2f));
            }
        }

        // ------------------------------------------------------------------ 主角：电脑城

        private void BuildHeroMall()
        {
            float cx = -(BlockStart + CellSize); // 与让出的两格对齐
            float cz = BlockStart + 7f;
            float w = 24f, d = 14f, h = 15f;

            Building(cx, cz, w, d, h, new Color(0.26f, 0.30f, 0.38f), false);

            float front = cz - d / 2f;
            HeroFrontCenter = new Vector3(cx, 0f, front);

            // 一楼大橱窗与入口
            ShopFront(cx, front, w, true);

            // 顶部大招牌：双色霓虹条（暗示"电子城"）
            Cube("MallSignTop", new Vector3(cx, h - 1.2f, front - 0.2f), new Vector3(w * 0.85f, 1.8f, 0.35f),
                Emissive(NeonPalette[1], _look.EmissiveIntensity * 1.6f), false);
            Cube("MallSignBar", new Vector3(cx, h - 3.0f, front - 0.2f), new Vector3(w * 0.85f, 0.4f, 0.3f),
                Emissive(NeonPalette[0], _look.EmissiveIntensity * 1.6f), false);

            // 侧面竖向霓虹
            Cube("MallSideNeon", new Vector3(cx - w / 2f - 0.25f, h / 2f, front + 1.5f), new Vector3(0.3f, h * 0.9f, 0.5f),
                Emissive(NeonPalette[3], _look.EmissiveIntensity * 1.3f), false);

            // 大屏幕
            var screen = NewMesh("MallScreen", _quad,
                Emissive(NeonPalette[2], _look.EmissiveIntensity * 0.9f), false);
            screen.transform.position = new Vector3(cx + w * 0.22f, 8.2f, front - 0.04f);
            screen.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            screen.transform.localScale = new Vector3(8f, 4.2f, 1f);
        }

        // ------------------------------------------------------------------ 街道设施

        private void BuildStreetFurniture()
        {
            var poleMat = Lit(new Color(0.22f, 0.23f, 0.26f), 0.4f);
            var bulbMat = Emissive(new Color(1f, 0.82f, 0.5f), _look.LampsOn ? _look.EmissiveIntensity * 2f : 0.2f);

            // 路灯：沿两条路的人行道
            for (float t = -WorldHalf + 10; t < WorldHalf - 5; t += 14f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 2f) continue;
                Lamp(new Vector3(t, 0, RoadHalfWidth + 0.6f), poleMat, bulbMat);
                Lamp(new Vector3(t + 7f, 0, -RoadHalfWidth - 0.6f), poleMat, bulbMat);
                Lamp(new Vector3(RoadHalfWidth + 0.6f, 0, t + 7f), poleMat, bulbMat);
                Lamp(new Vector3(-RoadHalfWidth - 0.6f, 0, t), poleMat, bulbMat);
            }

            // 树
            var trunk = Lit(new Color(0.35f, 0.25f, 0.18f), 0.1f);
            for (float t = -WorldHalf + 14; t < WorldHalf - 8; t += 14f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 3f) continue;
                Tree(new Vector3(t, 0, RoadHalfWidth + 2.0f), trunk);
                Tree(new Vector3(t + 7f, 0, -RoadHalfWidth - 2.0f), trunk);
            }
        }

        private void Lamp(Vector3 pos, Material pole, Material bulb)
        {
            Cylinder("LampPole", pos + new Vector3(0, 3.2f, 0), new Vector3(0.18f, 3.2f, 0.18f), pole);
            Sphere("LampBulb", pos + new Vector3(0, 6.5f, 0), new Vector3(0.7f, 0.5f, 0.7f), bulb);

            // 只在靠近画面中心的一部分路灯上放真实光源，避免性能和光源数量失控
            if (_look.LampsOn && _realLightCount < 18 && Mathf.Abs(pos.x) < 45f && Mathf.Abs(pos.z) < 45f)
            {
                var lightGo = new GameObject("LampLight");
                lightGo.transform.SetParent(_root, false);
                lightGo.transform.position = pos + new Vector3(0, 6.2f, 0);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.78f, 0.5f);
                l.range = 13f;
                l.intensity = _look.LampIntensity;
                l.shadows = LightShadows.None;
                _realLightCount++;
            }
        }

        private void Tree(Vector3 pos, Material trunk)
        {
            Cylinder("Trunk", pos + new Vector3(0, 1.1f, 0), new Vector3(0.4f, 1.1f, 0.4f), trunk);
            Color leaf = new Color(0.22f + (float)_rng.NextDouble() * 0.1f, 0.48f + (float)_rng.NextDouble() * 0.12f, 0.28f);
            Sphere("Canopy", pos + new Vector3(0, 3.5f, 0), new Vector3(3.2f, 2.8f, 3.2f), Lit(leaf, 0.1f));
        }

        // ------------------------------------------------------------------ 车辆与行人

        private void BuildCars()
        {
            // 沿 X 方向的路（z = ±2.5）
            for (int i = 0; i < 7; i++)
            {
                float x = -60f + i * 17f + (float)_rng.NextDouble() * 4f;
                if (Mathf.Abs(x) < 7f) x += 12f;
                Car(new Vector3(x, 0, i % 2 == 0 ? -2.5f : 2.5f), i % 2 == 0 ? 1 : -1, true);
            }

            // 沿 Z 方向的路（x = ±2.5）
            for (int i = 0; i < 6; i++)
            {
                float z = -55f + i * 19f + (float)_rng.NextDouble() * 4f;
                if (Mathf.Abs(z) < 7f) z += 12f;
                Car(new Vector3(i % 2 == 0 ? 2.5f : -2.5f, 0, z), i % 2 == 0 ? 1 : -1, false);
            }
        }

        private void Car(Vector3 pos, int dir, bool alongX)
        {
            Color body = CarPalette[_rng.Next(CarPalette.Length)];
            var bodyMat = Lit(body, 0.6f, 0.2f);
            var glassMat = Lit(new Color(0.08f, 0.10f, 0.14f), 0.9f);

            var carRoot = new GameObject("Car").transform;
            carRoot.SetParent(_root, false);
            carRoot.position = pos;
            carRoot.rotation = Quaternion.Euler(0, alongX ? (dir > 0 ? 90f : -90f) : (dir > 0 ? 0f : 180f), 0);

            // 局部坐标：前进方向为 +Z
            Cube(carRoot, "CarBody", new Vector3(0, 0.65f, 0), new Vector3(1.9f, 0.8f, 4.3f), bodyMat, true);
            Cube(carRoot, "CarCabin", new Vector3(0, 1.3f, -0.2f), new Vector3(1.7f, 0.7f, 2.2f), glassMat, true);

            var head = Emissive(new Color(1f, 0.95f, 0.8f), _look.LampsOn ? _look.EmissiveIntensity * 2.2f : 0.5f);
            var tail = Emissive(new Color(1f, 0.1f, 0.08f), _look.LampsOn ? _look.EmissiveIntensity * 1.8f : 0.4f);
            Cube(carRoot, "HeadL", new Vector3(-0.65f, 0.7f, 2.16f), new Vector3(0.4f, 0.22f, 0.08f), head, false);
            Cube(carRoot, "HeadR", new Vector3(0.65f, 0.7f, 2.16f), new Vector3(0.4f, 0.22f, 0.08f), head, false);
            Cube(carRoot, "TailL", new Vector3(-0.65f, 0.75f, -2.16f), new Vector3(0.4f, 0.2f, 0.08f), tail, false);
            Cube(carRoot, "TailR", new Vector3(0.65f, 0.75f, -2.16f), new Vector3(0.4f, 0.2f, 0.08f), tail, false);
        }

        private void BuildPeople()
        {
            // 在电脑城门口聚一小群顾客
            float baseX = HeroFrontCenter.x;
            float baseZ = HeroFrontCenter.z - 1.6f;
            for (int i = 0; i < 9; i++)
            {
                float x = baseX - 8f + i * 2f + ((float)_rng.NextDouble() - 0.5f) * 1.2f;
                float z = baseZ - (float)_rng.NextDouble() * 2.5f;
                Person(new Vector3(x, 0.3f, z));
            }

            // 人行道上的散步者
            for (int i = 0; i < 22; i++)
            {
                bool alongX = i % 2 == 0;
                float t = -WorldHalf + 6f + (float)_rng.NextDouble() * (WorldHalf * 2 - 12f);
                if (Mathf.Abs(t) < RoadHalfWidth + 1f) t += 10f;
                float side = (i % 4 < 2 ? 1 : -1) * (RoadHalfWidth + 1.4f + (float)_rng.NextDouble() * 1.0f);
                Vector3 pos = alongX ? new Vector3(t, 0.3f, side) : new Vector3(side, 0.3f, t);
                Person(pos);
            }
        }

        private void Person(Vector3 pos)
        {
            Color shirt = ShirtPalette[_rng.Next(ShirtPalette.Length)];
            Capsule("Person", pos + new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.85f, 0.6f), Lit(shirt, 0.2f));
            Sphere("Head", pos + new Vector3(0, 1.85f, 0), new Vector3(0.42f, 0.42f, 0.42f),
                Lit(new Color(0.88f, 0.72f, 0.60f), 0.2f));
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
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
            return mesh;
        }

        private GameObject NewMesh(string name, Mesh mesh, Material mat, bool castShadow, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
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

        // ------------------------------------------------------------------ 材质

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
    }
}
