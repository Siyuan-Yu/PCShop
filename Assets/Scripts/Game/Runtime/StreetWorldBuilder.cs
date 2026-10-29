using System.Collections.Generic;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在可点击的建筑上，点击后走到它门口并进去。</summary>
    public sealed class PlaceMarker : MonoBehaviour
    {
        public string PlaceId;
    }

    /// <summary>挂在地面上，点击地面走到最近的路口。</summary>
    public sealed class GroundMarker : MonoBehaviour
    {
    }

    public sealed class PlaceVisual
    {
        public Place Place;
        public Vector3 Entrance;
        public Vector3 LabelPos;
        public GameObject Root;
    }

    /// <summary>
    /// 按城市布局数据生成灰盒 3D 街区。地图坐标 (x, y) 对应世界 (x, 0, y)。
    /// 镜头固定从西南方斜俯视（只看得到建筑的 -Z、-X 面），所以商铺都在街北侧、门脸朝 -Z，街南侧只放矮的东西。
    /// </summary>
    public sealed class StreetWorldBuilder
    {
        public const float StreetZ = 0f;
        public const float FrontZ = 4.5f;

        private static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.19f);
        private static readonly Color Sidewalk = new Color(0.55f, 0.54f, 0.52f);
        private static readonly Color Grass = new Color(0.30f, 0.38f, 0.26f);

        private static readonly Color[] Facades =
        {
            new Color(0.86f, 0.78f, 0.66f), new Color(0.72f, 0.80f, 0.86f), new Color(0.90f, 0.86f, 0.80f),
            new Color(0.78f, 0.70f, 0.74f), new Color(0.70f, 0.76f, 0.68f), new Color(0.88f, 0.74f, 0.60f),
            new Color(0.64f, 0.66f, 0.72f), new Color(0.82f, 0.82f, 0.74f),
        };

        private static readonly Color[] SignColors =
        {
            new Color(1f, 0.35f, 0.35f), new Color(1f, 0.75f, 0.2f), new Color(0.3f, 0.85f, 1f),
            new Color(0.5f, 1f, 0.5f), new Color(1f, 0.45f, 0.9f), new Color(0.65f, 0.5f, 1f),
        };

        private readonly CityLayout _city;
        private readonly System.Random _rng;
        private Transform _root;
        private int _shopNeonCount;

        public Dictionary<string, PlaceVisual> Places { get; } = new Dictionary<string, PlaceVisual>();
        public List<Light> NightLights { get; } = new List<Light>();
        public GameObject Root => _root.gameObject;

        public StreetWorldBuilder(CityLayout city, int seed)
        {
            _city = city;
            _rng = new System.Random(seed);
        }

        public static Vector3 ToWorld(MapPoint p, float y = 0f) => new Vector3((float)p.X, y, (float)p.Y);

        public Vector3 NodeWorld(string nodeId) => ToWorld(_city.ViewPosition(nodeId));

        public GameObject Build()
        {
            Mats.ClearNightGlow();
            _root = new GameObject("City").transform;

            var ground = Prims.Box(_root, "Ground", new Vector3(-60, -0.06f, -20), new Vector3(520, 0.1f, 220), Mats.LitTex(Grass, new Vector2(70, 30), 0.05f), true);
            ground.AddComponent<GroundMarker>();

            BuildRoads();
            BuildSidewalks();
            foreach (Place p in _city.Places) BuildPlace(p);
            BuildFiller();
            BuildStreetProps();
            return _root.gameObject;
        }

        // ---------- 路 ----------

        private void BuildRoads()
        {
            var roads = new GameObject("Roads").transform;
            roads.SetParent(_root, false);
            var done = new HashSet<string>();
            foreach (WorldNode n in _city.Map.Nodes)
            {
                foreach (WorldEdge e in _city.Map.NeighborsOf(n.Id))
                {
                    string key = string.CompareOrdinal(n.Id, e.To) < 0 ? n.Id + "|" + e.To : e.To + "|" + n.Id;
                    if (!done.Add(key)) continue;
                    Vector3 a = NodeWorld(n.Id), b = NodeWorld(e.To);
                    bool main = Mathf.Abs(a.z) < 0.1f && Mathf.Abs(b.z) < 0.1f;
                    float width = main ? 7f : 5f;
                    Vector3 d = b - a;
                    float len = d.magnitude + width;
                    var go = Prims.Box(roads, "Road_" + key, (a + b) * 0.5f + Vector3.up * 0.005f, new Vector3(width, 0.02f, len), Mats.LitTex(Asphalt, new Vector2(Mathf.Max(1f, width / 5f), Mathf.Max(1f, len / 5f)), 0.5f));
                    go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);

                    if (len > 14f)
                    {
                        var mark = Prims.Box(roads, "Mark", (a + b) * 0.5f + Vector3.up * 0.02f, new Vector3(0.18f, 0.01f, len - width), Mats.Lit(new Color(0.85f, 0.8f, 0.55f)));
                        mark.transform.localRotation = go.transform.localRotation;
                    }
                }
            }
        }

        private void BuildSidewalks()
        {
            var sw = Mats.LitTex(Sidewalk, new Vector2(60, 1), 0.2f);
            Prims.Box(_root, "SidewalkNorth", new Vector3(-85, 0.06f, 3.9f), new Vector3(380, 0.12f, 1.4f), sw);
            Prims.Box(_root, "SidewalkSouth", new Vector3(-85, 0.06f, -4.2f), new Vector3(380, 0.12f, 1.4f), sw);
        }

        // ---------- 地点 ----------

        private void BuildPlace(Place p)
        {
            Vector3 entrance = NodeWorld(p.NodeId);
            var root = new GameObject("Place_" + p.Id).transform;
            root.SetParent(_root, false);
            var vis = new PlaceVisual { Place = p, Entrance = entrance, Root = root.gameObject };
            Places[p.Id] = vis;

            switch (p.Kind)
            {
                case PlaceKind.Shop:
                case PlaceKind.Agency:
                    vis.LabelPos = BuildShop(root, p, entrance);
                    break;
                case PlaceKind.Station:
                    vis.LabelPos = BuildStation(root, p, entrance);
                    break;
                case PlaceKind.Home:
                    vis.LabelPos = BuildHome(root, p, entrance);
                    break;
                case PlaceKind.Wholesaler:
                    vis.LabelPos = BuildStall(root, p, entrance, false);
                    break;
                case PlaceKind.Dealer:
                    vis.LabelPos = BuildStall(root, p, entrance, true);
                    break;
                default:
                    vis.LabelPos = entrance + Vector3.up * 3;
                    break;
            }
        }

        private static float WidthOf(ShopSize s) => s == ShopSize.Large ? 13.6f : s == ShopSize.Medium ? 8.6f : 4.6f;
        private static float DepthOf(ShopSize s) => s == ShopSize.Large ? 16f : s == ShopSize.Medium ? 11f : 7f;

        private Vector3 BuildShop(Transform root, Place p, Vector3 entrance)
        {
            float w = WidthOf(p.Size), d = DepthOf(p.Size);
            int floors = p.Size == ShopSize.Large ? 4 : p.Size == ShopSize.Medium ? 3 : 2;
            if (p.IsPlayerShop) floors = 1;
            float h = floors * 3.2f;
            Color facade = p.IsPlayerShop ? new Color(0.62f, 0.60f, 0.56f) : Facades[_rng.Next(Facades.Length)];
            Color sign = p.IsPlayerShop ? new Color(0.2f, 1f, 0.9f) : SignColors[_rng.Next(SignColors.Length)];
            float cz = FrontZ + d / 2;

            var body = Prims.Box(root, "Body", new Vector3(entrance.x, h / 2, cz), new Vector3(w, h, d), Mats.Lit(facade, 0.1f), true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;

            Prims.Box(root, "Glass", new Vector3(entrance.x, 1.4f, FrontZ - 0.05f), new Vector3(w - 1f, 2.6f, 0.1f),
                Mats.Glow(new Color(0.35f, 0.45f, 0.55f), new Color(1.6f, 1.3f, 0.9f)));
            Prims.Box(root, "Door", new Vector3(entrance.x, 1.1f, FrontZ - 0.08f), new Vector3(Mathf.Min(1.4f, w * 0.3f), 2.2f, 0.1f), Mats.Lit(new Color(0.2f, 0.22f, 0.25f), 0.6f));
            Prims.Box(root, "Sign", new Vector3(entrance.x, 3.2f, FrontZ - 0.15f), new Vector3(w - 0.4f, 0.8f, 0.2f),
                Mats.Glow(sign * 0.8f, sign * 2.2f));
            Prims.Box(root, "Awning", new Vector3(entrance.x, 2.75f, FrontZ - 0.6f), new Vector3(w - 0.2f, 0.08f, 1.2f), Mats.Lit(sign * 0.6f));
            if (_shopNeonCount++ % 2 == 0)
            {
                var ngo = new GameObject("NeonLight");
                ngo.transform.SetParent(root, false);
                ngo.transform.localPosition = new Vector3(entrance.x, 2.6f, FrontZ - 2.2f);
                var nl = ngo.AddComponent<Light>();
                nl.type = LightType.Point;
                nl.range = 9f;
                nl.color = Color.Lerp(sign, Color.white, 0.15f);
                nl.intensity = 0;
                nl.shadows = LightShadows.None;
                NightLights.Add(nl);
            }

            if (floors > 1)
                WindowFace(root, new Vector3(entrance.x, 3.2f + (h - 3.2f) / 2f, FrontZ - 0.03f), w - 0.4f, h - 3.2f, facade, false);
            Prims.Box(root, "Roof", new Vector3(entrance.x, h + 0.15f, cz), new Vector3(w + 0.2f, 0.3f, d + 0.2f), Mats.Lit(facade * 0.7f));

            if (p.IsPlayerShop)
            {
                Prims.Box(root, "RoofSign", new Vector3(entrance.x, h + 1.0f, FrontZ + 0.5f), new Vector3(w, 1.2f, 0.2f),
                    Mats.Glow(new Color(0.1f, 0.6f, 0.55f), new Color(0.3f, 2.5f, 2.2f), false));
            }
            return new Vector3(entrance.x, h + (p.IsPlayerShop ? 2.4f : 1.2f), FrontZ);
        }

        private Vector3 BuildStation(Transform root, Place p, Vector3 e)
        {
            var body = Prims.Box(root, "Body", new Vector3(e.x, 2.2f, FrontZ + 4), new Vector3(10, 4.4f, 8), Mats.Lit(new Color(0.75f, 0.78f, 0.82f), 0.3f), true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;
            Prims.Box(root, "Canopy", new Vector3(e.x, 4.6f, FrontZ + 2), new Vector3(12, 0.3f, 6), Mats.Lit(new Color(0.15f, 0.35f, 0.75f)));
            Prims.Box(root, "Logo", new Vector3(e.x, 3.5f, FrontZ - 0.1f), new Vector3(1.6f, 1.6f, 0.2f), Mats.Glow(new Color(0.1f, 0.3f, 0.9f), new Color(0.4f, 0.9f, 3f), false));
            Prims.Box(root, "Gate", new Vector3(e.x, 1.2f, FrontZ - 0.05f), new Vector3(6, 2.4f, 0.1f), Mats.Glow(new Color(0.3f, 0.35f, 0.4f), new Color(1.2f, 1.2f, 1.4f)));
            return new Vector3(e.x, 6.5f, FrontZ);
        }

        private Vector3 BuildHome(Transform root, Place p, Vector3 e)
        {
            float x = e.x + 6;
            var body = Prims.Box(root, "Body", new Vector3(x, 3.2f, e.z), new Vector3(8, 6.4f, 8), Mats.Lit(new Color(0.70f, 0.62f, 0.52f), 0.05f), true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;
            Prims.Box(root, "Door", new Vector3(x - 4.05f, 1.1f, e.z), new Vector3(0.1f, 2.2f, 1.2f), Mats.Lit(new Color(0.35f, 0.2f, 0.12f)));
            WindowFace(root, new Vector3(x - 4.03f, 4.8f, e.z), 8f, 3.2f, new Color(0.70f, 0.62f, 0.52f), true);
            Prims.Box(root, "Laundry", new Vector3(x - 4.4f, 3.6f, e.z - 1.5f), new Vector3(0.05f, 0.05f, 3f), Mats.Lit(Color.gray));
            for (int i = 0; i < 3; i++)
                Prims.Box(root, "Cloth" + i, new Vector3(x - 4.4f, 3.2f, e.z - 2.6f + i), new Vector3(0.04f, 0.7f, 0.6f), Mats.Lit(SignColors[i]));
            return new Vector3(x - 2, 8.5f, e.z);
        }

        private Vector3 BuildStall(Transform root, Place p, Vector3 e, bool dealer)
        {
            float w = dealer ? 11f : 10f;
            var body = Prims.Box(root, "Body", new Vector3(e.x, 2.5f, FrontZ + 4), new Vector3(w, 5, 8), Mats.Lit(new Color(0.62f, 0.64f, 0.66f), 0.1f), true);
            body.AddComponent<PlaceMarker>().PlaceId = p.Id;
            Color c = dealer ? new Color(1f, 0.55f, 0.1f) : SignColors[_rng.Next(SignColors.Length)];
            Prims.Box(root, "Shutter", new Vector3(e.x, 1.3f, FrontZ - 0.05f), new Vector3(w - 1.5f, 2.6f, 0.1f), Mats.Glow(new Color(0.4f, 0.42f, 0.45f), new Color(1.4f, 1.3f, 1.1f)));
            Prims.Box(root, "Sign", new Vector3(e.x, 3.3f, FrontZ - 0.15f), new Vector3(w - 0.6f, 0.9f, 0.2f), Mats.Glow(c * 0.8f, c * 2f));
            if (dealer)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = e.x - 3.5f + i * 3.2f;
                    Prims.Box(root, "Bike" + i, new Vector3(x, 0.5f, FrontZ - 1.6f), new Vector3(0.5f, 0.6f, 1.6f), Mats.Lit(i == 2 ? new Color(0.25f, 0.5f, 0.85f) : new Color(0.9f, 0.3f, 0.3f)));
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    Prims.Box(root, "Box" + i, new Vector3(e.x - 3.5f + i * 2.2f, 0.35f, FrontZ - 1.0f), new Vector3(1.0f, 0.7f, 0.8f), Mats.Lit(new Color(0.72f, 0.58f, 0.40f)));
            }
            return new Vector3(e.x, 6.5f, FrontZ);
        }

        // ---------- 填充 ----------

        private void BuildFiller()
        {
            var t = new GameObject("Filler").transform;
            t.SetParent(_root, false);

            // 电子市场背后的大楼（作背景）
            var mall = Prims.Box(t, "MarketHall", new Vector3(-236, 8, 26), new Vector3(70, 16, 22), Mats.Lit(new Color(0.55f, 0.6f, 0.66f), 0.3f));
            WindowFace(t, new Vector3(-236, 6.5f, 14.97f), 68f, 10f, new Color(0.55f, 0.6f, 0.66f), false);
            Prims.Box(t, "MarketHallSign", new Vector3(-236, 13, 14.9f), new Vector3(30, 3, 0.3f), Mats.Glow(new Color(0.8f, 0.2f, 0.2f), new Color(3f, 0.5f, 0.4f), false));

            // 商业街背后的楼
            for (float x = -98; x < 100; x += 14f)
            {
                float h = 14 + (float)_rng.NextDouble() * 12;
                float zc = 30 + (float)_rng.NextDouble() * 4;
                Color wall = Facades[_rng.Next(Facades.Length)] * 0.8f;
                Prims.Box(t, "Back", new Vector3(x, h / 2, zc), new Vector3(13, h, 8), Mats.Lit(wall));
                WindowFace(t, new Vector3(x, h / 2, zc - 4.03f), 12.6f, h - 0.5f, wall, false);
                RoofClutter(t, new Vector3(x, h, zc), 13, 8);
            }

            // 连接路两侧：北侧仓库（可以高），中间和南侧矮房
            for (float x = -120; x > -195; x -= 12f)
            {
                float h = 5 + (float)_rng.NextDouble() * 6;
                Prims.Box(t, "Warehouse", new Vector3(x, h / 2, 9 + (float)_rng.NextDouble() * 2), new Vector3(10, h, 8), Mats.Lit(new Color(0.6f, 0.58f, 0.55f) * (0.8f + (float)_rng.NextDouble() * 0.3f)));
                Prims.Box(t, "LowMid", new Vector3(x - 3, 1.2f, -10), new Vector3(7, 2.4f, 6), Mats.Lit(new Color(0.55f, 0.5f, 0.45f)));
                Tree(t, new Vector3(x + 4, 0, -27));
            }

            // 城中村：矮而密的自建房
            for (int row = 0; row < 5; row++)
            {
                float z = -6 - row * 12;
                for (int k = 0; k < 2; k++)
                {
                    float x = 111 + k * 7;
                    float h = 3 + (float)_rng.NextDouble() * 3.5f;
                    VillageHouse(t, new Vector3(x, h / 2, z - 0.5f), h);
                }
                for (int k = 0; k < 3; k++)
                {
                    float x = 131 + k * 7;
                    if (row == 4) continue;
                    float h = 3 + (float)_rng.NextDouble() * 4;
                    VillageHouse(t, new Vector3(x, h / 2, z - 0.5f), h);
                }
            }
            for (int k = 0; k < 5; k++)
                Prims.Box(t, "VillageSouth", new Vector3(108 + k * 9, 1.5f, -72), new Vector3(7, 3, 6), Mats.Lit(VillageColor()));
        }

        /// <summary>在一面朝 -Z（或 -X）的墙上铺一层窗户贴图（镜头只看得到这两个方向）。</summary>
        private void WindowFace(Transform parent, Vector3 center, float width, float height, Color wall, bool faceMinusX)
        {
            int cols = Mathf.Max(1, Mathf.RoundToInt(width / 2.4f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / 3.2f));
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Windows";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = center;
            q.transform.localRotation = faceMinusX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            q.transform.localScale = new Vector3(width, height, 1f);
            q.GetComponent<Renderer>().sharedMaterial = Mats.WindowWall(wall, cols, rows, _rng.Next(16));
        }

        private void VillageHouse(Transform parent, Vector3 center, float h)
        {
            Color wall = VillageColor();
            Prims.Box(parent, "Village", center, new Vector3(5.5f, h, 7.5f), Mats.Lit(wall));
            WindowFace(parent, new Vector3(center.x, center.y, center.z - 3.78f), 5.2f, h - 0.4f, wall, false);
            WindowFace(parent, new Vector3(center.x - 2.78f, center.y, center.z), 7.0f, h - 0.4f, wall, true);
            RoofClutter(parent, new Vector3(center.x, h, center.z), 5.5f, 7.5f);
        }

        /// <summary>屋顶上的空调外机、水箱，让天际线不再是一块平板。</summary>
        private void RoofClutter(Transform parent, Vector3 roofCenter, float w, float d)
        {
            Material metal = Mats.Lit(new Color(0.62f, 0.64f, 0.66f), 0.4f, 0.4f);
            int n = 1 + _rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float x = roofCenter.x + ((float)_rng.NextDouble() - 0.5f) * (w - 2f);
                float z = roofCenter.z + ((float)_rng.NextDouble() - 0.5f) * (d - 2f);
                if (_rng.NextDouble() < 0.35)
                {
                    Prims.Shape(PrimitiveType.Cylinder, parent, "Tank", new Vector3(x, roofCenter.y + 0.9f, z), new Vector3(1.1f, 0.9f, 1.1f), Mats.Lit(new Color(0.55f, 0.5f, 0.45f)));
                }
                else
                {
                    Prims.Box(parent, "AC", new Vector3(x, roofCenter.y + 0.4f, z), new Vector3(1.2f, 0.8f, 0.9f), metal);
                }
            }
        }

        private Color VillageColor()
        {
            float g = 0.55f + (float)_rng.NextDouble() * 0.25f;
            return new Color(g, g * 0.95f, g * 0.88f);
        }

        private void BuildStreetProps()
        {
            var t = new GameObject("Props").transform;
            t.SetParent(_root, false);
            Material pole = Mats.Lit(new Color(0.25f, 0.27f, 0.3f), 0.5f, 0.6f);
            Material lampHead = Mats.Glow(new Color(0.8f, 0.8f, 0.7f), new Color(4f, 3.2f, 2f));
            int lights = 0;
            for (float x = -95; x <= 100; x += 18f)
            {
                Prims.Box(t, "Pole", new Vector3(x, 2.2f, -4.6f), new Vector3(0.15f, 4.4f, 0.15f), pole);
                Prims.Box(t, "Head", new Vector3(x, 4.4f, -4.0f), new Vector3(0.5f, 0.15f, 1.2f), lampHead);
                if (lights++ % 2 == 0)
                {
                    var lgo = new GameObject("LampLight");
                    lgo.transform.SetParent(t, false);
                    lgo.transform.localPosition = new Vector3(x, 4f, -3.2f);
                    var l = lgo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 12f;
                    l.color = new Color(1f, 0.82f, 0.6f);
                    l.intensity = 0;
                    NightLights.Add(l);
                }
                Tree(t, new Vector3(x + 9, 0, -6.5f));
            }
            for (int i = 0; i < 6; i++)
                Prims.Box(t, "ParkedCar", new Vector3(-80 + i * 31, 0.6f, -2.6f), new Vector3(4f, 1.2f, 1.8f), Mats.Lit(SignColors[i % SignColors.Length] * 0.7f, 0.7f, 0.3f));
            for (int i = 0; i < 6; i++) Tree(t, new Vector3(100 + i * 1.5f * (i % 2 == 0 ? 1 : -1), 0, -66 - i * 1.3f));
        }

        private void Tree(Transform parent, Vector3 at)
        {
            float s = 0.8f + (float)_rng.NextDouble() * 0.5f;
            Prims.Shape(PrimitiveType.Cylinder, parent, "Trunk", at + new Vector3(0, 1f * s, 0), new Vector3(0.25f, 1f * s, 0.25f), Mats.Lit(new Color(0.35f, 0.25f, 0.15f)));
            Prims.Shape(PrimitiveType.Sphere, parent, "Leaves", at + new Vector3(0, 2.6f * s, 0), new Vector3(2.2f, 2f, 2.2f) * s, Mats.Lit(new Color(0.25f, 0.45f, 0.22f) * (0.85f + (float)_rng.NextDouble() * 0.3f)));
        }
    }
}
