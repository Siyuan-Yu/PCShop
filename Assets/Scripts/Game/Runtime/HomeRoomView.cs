using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在家具上，点击时触发对应的互动。</summary>
    public sealed class HomeFurniture : MonoBehaviour
    {
        public string Id;
    }

    /// <summary>
    /// 开局住的城中村出租屋（一间真实的小屋）：又破又旧，只有一张硬板床、一个布衣柜、一张折叠小桌和一个坏风扇。
    /// 放在远离城市的位置，回家时镜头切过来。床 = 睡觉，衣柜 = 换衣服，桌子 = 看状态，门 = 出门。
    /// 住处改善后屋里会跟着变：新床垫 → 二手空调、风扇修好 → 窗帘、热水器。
    /// </summary>
    public sealed class HomeRoomView
    {
        public const string Bed = "bed";
        public const string Wardrobe = "wardrobe";
        public const string Table = "table";
        public const string Door = "door";

        public static readonly Vector3 Origin = new Vector3(600, 0, -470);
        public const float Width = 6f;
        public const float Depth = 4.6f;
        public const float DefaultZoom = 4.2f;
        public const float MinZoom = 2.8f;
        /// <summary>刚好看到整间屋子的镜头大小（缩放上限）。</summary>
        public static float CameraSize => Mathf.Max(Width, Depth) * 0.62f + 1.5f;

        private struct Spot
        {
            public string Id;
            public string Label;
            public Rect Area;
        }

        private readonly Transform _root;
        private readonly GameObject _owner;
        private readonly List<Rect> _blockers = new List<Rect>();
        private readonly List<Spot> _spots = new List<Spot>();
        private readonly List<GameObject>[] _levelOnly = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private readonly List<GameObject>[] _levelFrom = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private readonly Material _windowMat;
        private readonly Light _windowLight;
        private Transform _fanHead;
        private Transform _blades;
        private int _level = -1;
        private bool _wasOn;

        /// <summary>老板脚边最近的、够得着的家具 ID；没有则为空。</summary>
        public string NearbyId { get; private set; }

        public string NearbyLabel { get; private set; }

        public Vector3 Center => Origin + new Vector3(Width / 2, 0, Depth / 2);
        public Vector3 OwnerWorld => _owner.transform.position;

        public HomeRoomView(Transform parent)
        {
            _root = new GameObject("HomeRoom").transform;
            _root.SetParent(parent, false);
            _root.position = Origin;
            float w = Width, d = Depth;

            // ---------- 地面、墙 ----------
            Prims.Box(_root, "Floor", new Vector3(w / 2, -0.05f, d / 2), new Vector3(w, 0.1f, d), Mats.LitTex(new Color(0.52f, 0.47f, 0.41f), new Vector2(4, 3), 0.05f));
            Prims.Box(_root, "Outside", new Vector3(w / 2, -0.1f, d / 2), new Vector3(w + 30, 0.05f, d + 30), Mats.Lit(new Color(0.1f, 0.1f, 0.12f)));
            Stain("FloorStain1", new Vector3(2.9f, 0.007f, 2.1f), new Vector3(1.1f, 0.01f, 0.7f), new Color(0.40f, 0.36f, 0.31f), 20f);
            Stain("FloorStain2", new Vector3(4.8f, 0.007f, 3.0f), new Vector3(0.7f, 0.01f, 0.5f), new Color(0.43f, 0.39f, 0.33f), -30f);
            Stain("Crack1", new Vector3(3.6f, 0.008f, 1.0f), new Vector3(1.6f, 0.01f, 0.03f), new Color(0.25f, 0.22f, 0.2f), 25f);
            Stain("Crack2", new Vector3(1.4f, 0.008f, 2.5f), new Vector3(1.0f, 0.01f, 0.03f), new Color(0.25f, 0.22f, 0.2f), -50f);

            Color wallUpper = new Color(0.76f, 0.72f, 0.58f);
            Color wallDado = new Color(0.43f, 0.54f, 0.47f);
            Prims.Box(_root, "WallBack", new Vector3(w / 2, 1.5f, d + 0.05f), new Vector3(w + 0.2f, 3f, 0.1f), Mats.LitTex(wallUpper, new Vector2(3, 1), 0.05f));
            Prims.Box(_root, "WallRight", new Vector3(w + 0.05f, 1.5f, d / 2), new Vector3(0.1f, 3f, d + 0.2f), Mats.LitTex(wallUpper * 0.93f, new Vector2(3, 1), 0.05f));
            Prims.Box(_root, "DadoBack", new Vector3(w / 2, 0.55f, d - 0.01f), new Vector3(w, 1.1f, 0.02f), Mats.Lit(wallDado, 0.05f));
            Prims.Box(_root, "DadoRight", new Vector3(w - 0.01f, 0.55f, d / 2), new Vector3(0.02f, 1.1f, d), Mats.Lit(wallDado * 0.93f, 0.05f));
            Prims.Box(_root, "WallLeftLow", new Vector3(-0.05f, 0.3f, d / 2), new Vector3(0.1f, 0.6f, d), Mats.Lit(new Color(0.6f, 0.58f, 0.52f)));
            Prims.Box(_root, "WallFrontLow", new Vector3(w / 2, 0.3f, -0.05f), new Vector3(w, 0.6f, 0.1f), Mats.Lit(new Color(0.6f, 0.58f, 0.52f)));
            // 墙上的水渍、掉皮、霉斑
            Prims.Box(_root, "LeakStain", new Vector3(4.7f, 2.45f, d - 0.014f), new Vector3(0.9f, 0.8f, 0.01f), Mats.Lit(new Color(0.62f, 0.56f, 0.40f), 0.05f));
            Prims.Box(_root, "LeakStain2", new Vector3(4.7f, 2.75f, d - 0.016f), new Vector3(0.5f, 0.3f, 0.01f), Mats.Lit(new Color(0.55f, 0.49f, 0.34f), 0.05f));
            Prims.Box(_root, "Peeling", new Vector3(1.0f, 1.55f, d - 0.014f), new Vector3(0.6f, 0.5f, 0.01f), Mats.Lit(new Color(0.84f, 0.81f, 0.7f), 0.05f));
            Prims.Box(_root, "Mold", new Vector3(w - 0.014f, 2.3f, 1.9f), new Vector3(0.01f, 0.7f, 0.9f), Mats.Lit(new Color(0.48f, 0.5f, 0.4f), 0.05f));
            Prims.Box(_root, "Calendar", new Vector3(5.3f, 1.55f, d - 0.02f), new Vector3(0.32f, 0.45f, 0.02f), Mats.Lit(new Color(0.8f, 0.25f, 0.2f)));

            // ---------- 灯 ----------
            Prims.Box(_root, "BulbWire", new Vector3(w * 0.5f, 2.78f, d * 0.5f), new Vector3(0.02f, 0.44f, 0.02f), Mats.Lit(new Color(0.1f, 0.1f, 0.1f)));
            Prims.Shape(PrimitiveType.Sphere, _root, "Bulb", new Vector3(w * 0.5f, 2.5f, d * 0.5f), Vector3.one * 0.17f, Mats.Glow(new Color(1f, 0.95f, 0.7f), new Color(4f, 3.4f, 2f), false));
            var bulbGo = new GameObject("BulbLight");
            bulbGo.transform.SetParent(_root, false);
            bulbGo.transform.localPosition = new Vector3(w * 0.5f, 2.3f, d * 0.5f);
            var bulb = bulbGo.AddComponent<Light>();
            bulb.type = LightType.Point;
            bulb.range = 8f;
            bulb.intensity = 1.1f;
            bulb.color = new Color(1f, 0.84f, 0.58f);

            // ---------- 窗（带铁栏杆） ----------
            Prims.Box(_root, "WindowFrame", new Vector3(3.15f, 1.75f, d - 0.02f), new Vector3(1.15f, 1.05f, 0.04f), Mats.Lit(new Color(0.25f, 0.27f, 0.3f), 0.3f, 0.3f));
            GameObject pane = Prims.Box(_root, "WindowPane", new Vector3(3.15f, 1.75f, d - 0.045f), new Vector3(1.0f, 0.9f, 0.02f), Mats.Glow(new Color(0.3f, 0.35f, 0.4f), new Color(0.2f, 0.25f, 0.35f), false));
            _windowMat = new Material(pane.GetComponent<Renderer>().sharedMaterial);
            pane.GetComponent<Renderer>().sharedMaterial = _windowMat;
            for (int i = 0; i < 4; i++)
                Prims.Box(_root, "Bar", new Vector3(2.8f + i * 0.23f, 1.75f, d - 0.09f), new Vector3(0.03f, 0.95f, 0.03f), Mats.Lit(new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f));
            Prims.Box(_root, "BarCross", new Vector3(3.15f, 1.75f, d - 0.09f), new Vector3(1.05f, 0.03f, 0.03f), Mats.Lit(new Color(0.18f, 0.18f, 0.2f), 0.3f, 0.5f));
            var wl = new GameObject("WindowLight");
            wl.transform.SetParent(_root, false);
            wl.transform.localPosition = new Vector3(3.15f, 1.7f, d - 0.7f);
            _windowLight = wl.AddComponent<Light>();
            _windowLight.type = LightType.Point;
            _windowLight.range = 5f;
            _windowLight.color = new Color(0.7f, 0.82f, 1f);
            _windowLight.intensity = 0f;

            BuildBed();
            BuildWardrobe();
            BuildTable();
            BuildDoor();
            BuildFan();
            BuildClutter();
            BuildUpgrades();

            _blockers.Add(new Rect(0f, d - 1.15f, 2.25f, 1.15f));         // 床
            _blockers.Add(new Rect(w - 0.65f, 2.5f, 0.65f, 1.2f));        // 衣柜
            _blockers.Add(new Rect(3.95f, d - 0.8f, 1.3f, 0.8f));         // 桌子
            _blockers.Add(new Rect(0f, 0f, 1.1f, 1.0f));                  // 纸箱和盆
            _blockers.Add(new Rect(2.3f, 1.7f, 0.4f, 0.4f));              // 风扇

            _spots.Add(new Spot { Id = Bed, Label = "睡觉 / 小睡", Area = new Rect(0f, d - 1.15f, 2.25f, 1.15f) });
            _spots.Add(new Spot { Id = Wardrobe, Label = "打开衣柜", Area = new Rect(w - 0.65f, 2.5f, 0.65f, 1.2f) });
            _spots.Add(new Spot { Id = Table, Label = "看看自己（状态）", Area = new Rect(3.95f, d - 0.8f, 1.3f, 0.8f) });
            _spots.Add(new Spot { Id = Door, Label = "出门，回到街上", Area = new Rect(w - 0.2f, 0.5f, 0.2f, 1.0f) });

            _owner = Prims.Figure(_root, "Owner", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.05f);
            ResetOwner();
            ApplyLevel(0);
        }

        // ---------- 家具 ----------

        private void Stain(string name, Vector3 pos, Vector3 size, Color c, float yaw)
        {
            GameObject go = Prims.Box(_root, name, pos, size, Mats.Lit(c, 0.02f));
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private GameObject Furniture(string id, string name, Vector3 center, Vector3 size, Color c, float smooth = 0.1f, float metallic = 0f)
        {
            GameObject go = Prims.Box(_root, name, center, size, Mats.Lit(c, smooth, metallic), id != null);
            if (id != null) go.AddComponent<HomeFurniture>().Id = id;
            return go;
        }

        /// <summary>只在某个住处档位显示（levelOnly），或从某档起一直显示（from）。</summary>
        private GameObject Only(int level, GameObject go)
        {
            _levelOnly[level].Add(go);
            return go;
        }

        private GameObject From(int level, GameObject go)
        {
            _levelFrom[level].Add(go);
            return go;
        }

        private void BuildBed()
        {
            float d = Depth;
            Color metal = new Color(0.22f, 0.22f, 0.25f);
            foreach (Vector2 c in new[] { new Vector2(0.1f, d - 0.1f), new Vector2(2.0f, d - 0.1f), new Vector2(0.1f, d - 1.0f), new Vector2(2.0f, d - 1.0f) })
                Furniture(null, "BedLeg", new Vector3(c.x, 0.15f, c.y), new Vector3(0.06f, 0.3f, 0.06f), metal, 0.3f, 0.5f);
            Furniture(Bed, "BedBoard", new Vector3(1.05f, 0.33f, d - 0.55f), new Vector3(2.0f, 0.06f, 1.0f), new Color(0.45f, 0.33f, 0.22f));
            Furniture(Bed, "BedHead", new Vector3(0.07f, 0.65f, d - 0.55f), new Vector3(0.08f, 0.6f, 1.0f), metal, 0.3f, 0.5f);

            // 开局：薄薄一层发黄的旧床垫、发灰的枕头和格子被
            Only(0, Furniture(Bed, "OldMattress", new Vector3(1.15f, 0.40f, d - 0.55f), new Vector3(1.8f, 0.08f, 0.92f), new Color(0.62f, 0.6f, 0.5f), 0.02f));
            Only(0, Furniture(null, "OldMattressStain", new Vector3(1.3f, 0.445f, d - 0.5f), new Vector3(0.5f, 0.01f, 0.4f), new Color(0.5f, 0.46f, 0.36f), 0.02f));
            Only(0, Furniture(null, "OldPillow", new Vector3(0.45f, 0.47f, d - 0.55f), new Vector3(0.38f, 0.1f, 0.6f), new Color(0.72f, 0.7f, 0.64f), 0.02f));
            Only(0, Furniture(null, "OldBlanket", new Vector3(1.55f, 0.46f, d - 0.55f), new Vector3(0.95f, 0.06f, 0.9f), new Color(0.55f, 0.38f, 0.3f), 0.02f));
            // 新床垫之后：厚一点、干净
            From(1, Furniture(Bed, "NewMattress", new Vector3(1.15f, 0.47f, d - 0.55f), new Vector3(1.85f, 0.22f, 0.94f), new Color(0.45f, 0.62f, 0.74f), 0.05f));
            From(1, Furniture(null, "NewPillow", new Vector3(0.45f, 0.64f, d - 0.55f), new Vector3(0.38f, 0.12f, 0.6f), new Color(0.9f, 0.9f, 0.88f), 0.05f));
            Only(1, Furniture(null, "Blanket1", new Vector3(1.55f, 0.6f, d - 0.55f), new Vector3(0.95f, 0.07f, 0.92f), new Color(0.55f, 0.45f, 0.4f), 0.05f));
            Only(2, Furniture(null, "Blanket2", new Vector3(1.55f, 0.6f, d - 0.55f), new Vector3(0.95f, 0.07f, 0.92f), new Color(0.4f, 0.5f, 0.65f), 0.05f));
            From(3, Furniture(null, "Blanket3", new Vector3(1.55f, 0.6f, d - 0.55f), new Vector3(0.95f, 0.07f, 0.92f), new Color(0.3f, 0.55f, 0.6f), 0.05f));
        }

        private void BuildWardrobe()
        {
            float w = Width;
            Furniture(Wardrobe, "Wardrobe", new Vector3(w - 0.35f, 0.85f, 3.1f), new Vector3(0.6f, 1.7f, 1.1f), new Color(0.48f, 0.5f, 0.56f), 0.05f);
            Furniture(null, "WardrobeSeam", new Vector3(w - 0.665f, 0.85f, 3.1f), new Vector3(0.02f, 1.6f, 0.03f), new Color(0.25f, 0.26f, 0.3f));
            Furniture(null, "WardrobeSeam2", new Vector3(w - 0.665f, 0.85f, 3.65f), new Vector3(0.02f, 1.6f, 0.03f), new Color(0.3f, 0.31f, 0.35f));
            Furniture(null, "WardrobeHandle", new Vector3(w - 0.68f, 0.9f, 3.02f), new Vector3(0.03f, 0.18f, 0.03f), new Color(0.75f, 0.75f, 0.78f), 0.5f, 0.6f);
            Furniture(null, "WardrobeHandle2", new Vector3(w - 0.68f, 0.9f, 3.18f), new Vector3(0.03f, 0.18f, 0.03f), new Color(0.75f, 0.75f, 0.78f), 0.5f, 0.6f);
            Furniture(null, "WardrobeTop", new Vector3(w - 0.4f, 1.78f, 3.1f), new Vector3(0.45f, 0.16f, 0.8f), new Color(0.68f, 0.58f, 0.4f)); // 顶上塞着的旧纸箱
        }

        private void BuildTable()
        {
            float d = Depth;
            Color wood = new Color(0.55f, 0.45f, 0.32f);
            Furniture(Table, "TableTop", new Vector3(4.6f, 0.72f, d - 0.38f), new Vector3(1.2f, 0.05f, 0.6f), wood, 0.1f);
            foreach (Vector2 c in new[] { new Vector2(4.05f, d - 0.12f), new Vector2(5.15f, d - 0.12f), new Vector2(4.05f, d - 0.64f), new Vector2(5.15f, d - 0.64f) })
                Furniture(null, "TableLeg", new Vector3(c.x, 0.36f, c.y), new Vector3(0.04f, 0.72f, 0.04f), new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f);
            Prims.Shape(PrimitiveType.Cylinder, _root, "RiceCooker", new Vector3(4.25f, 0.88f, d - 0.38f), new Vector3(0.34f, 0.15f, 0.34f), Mats.Lit(new Color(0.86f, 0.86f, 0.8f), 0.3f));
            Prims.Box(_root, "CookerLight", new Vector3(4.25f, 0.9f, d - 0.54f), new Vector3(0.05f, 0.03f, 0.02f), Mats.Glow(new Color(0.6f, 0.1f, 0.1f), new Color(3f, 0.3f, 0.2f), false));
            Prims.Shape(PrimitiveType.Cylinder, _root, "Bowl", new Vector3(4.85f, 0.77f, d - 0.4f), new Vector3(0.2f, 0.04f, 0.2f), Mats.Lit(new Color(0.9f, 0.9f, 0.92f), 0.4f));
            Prims.Shape(PrimitiveType.Cylinder, _root, "Mug", new Vector3(5.1f, 0.8f, d - 0.3f), new Vector3(0.1f, 0.06f, 0.1f), Mats.Lit(new Color(0.3f, 0.5f, 0.7f), 0.4f));
            // 塑料矮凳
            Prims.Shape(PrimitiveType.Cylinder, _root, "Stool", new Vector3(4.4f, 0.2f, d - 1.25f), new Vector3(0.34f, 0.2f, 0.34f), Mats.Lit(new Color(0.7f, 0.25f, 0.2f), 0.3f));
        }

        private void BuildDoor()
        {
            float w = Width;
            Furniture(null, "DoorFrame", new Vector3(w - 0.03f, 1.05f, 1.0f), new Vector3(0.05f, 2.1f, 1.05f), new Color(0.3f, 0.22f, 0.15f));
            Furniture(Door, "Door", new Vector3(w - 0.07f, 1.0f, 1.0f), new Vector3(0.06f, 2.0f, 0.9f), new Color(0.42f, 0.3f, 0.2f), 0.1f);
            Furniture(null, "DoorPeel", new Vector3(w - 0.108f, 0.7f, 0.8f), new Vector3(0.01f, 0.6f, 0.35f), new Color(0.55f, 0.42f, 0.3f));
            Furniture(null, "DoorHandle", new Vector3(w - 0.12f, 1.0f, 0.65f), new Vector3(0.05f, 0.05f, 0.1f), new Color(0.75f, 0.7f, 0.5f), 0.5f, 0.6f);
            Prims.Box(_root, "ExitMark", new Vector3(w - 0.11f, 2.3f, 1.0f), new Vector3(0.01f, 0.2f, 0.5f), Mats.Glow(new Color(0.1f, 0.4f, 0.2f), new Color(0.4f, 2f, 0.9f), false));
        }

        private void BuildFan()
        {
            Furniture(null, "FanBase", new Vector3(2.5f, 0.03f, 1.9f), new Vector3(0.38f, 0.05f, 0.38f), new Color(0.5f, 0.5f, 0.53f), 0.2f, 0.3f);
            Furniture(null, "FanPole", new Vector3(2.5f, 0.55f, 1.9f), new Vector3(0.05f, 1.0f, 0.05f), new Color(0.55f, 0.55f, 0.58f), 0.3f, 0.4f);
            var head = new GameObject("FanHead");
            head.transform.SetParent(_root, false);
            head.transform.localPosition = new Vector3(2.5f, 1.1f, 1.9f);
            _fanHead = head.transform;
            GameObject cage = Prims.Shape(PrimitiveType.Cylinder, _fanHead, "Cage", Vector3.zero, new Vector3(0.45f, 0.025f, 0.45f), Mats.Lit(new Color(0.75f, 0.75f, 0.78f), 0.3f, 0.4f));
            cage.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var blades = new GameObject("Blades");
            blades.transform.SetParent(_fanHead, false);
            blades.transform.localPosition = new Vector3(-0.02f, 0, 0);
            _blades = blades.transform;
            Material bm = Mats.Lit(new Color(0.55f, 0.65f, 0.75f), 0.2f);
            Prims.Box(_blades, "BladeA", Vector3.zero, new Vector3(0.015f, 0.36f, 0.07f), bm);
            Prims.Box(_blades, "BladeB", Vector3.zero, new Vector3(0.015f, 0.07f, 0.36f), bm);
        }

        private void BuildClutter()
        {
            float w = Width;
            // 左前角：纸箱、红塑料盆、水桶
            Furniture(null, "Box1", new Vector3(0.45f, 0.28f, 0.45f), new Vector3(0.7f, 0.56f, 0.6f), new Color(0.7f, 0.58f, 0.4f), 0.02f);
            Furniture(null, "Box2", new Vector3(0.35f, 0.7f, 0.4f), new Vector3(0.5f, 0.28f, 0.45f), new Color(0.66f, 0.54f, 0.37f), 0.02f);
            Prims.Shape(PrimitiveType.Cylinder, _root, "Basin", new Vector3(0.95f, 0.1f, 0.4f), new Vector3(0.45f, 0.1f, 0.45f), Mats.Lit(new Color(0.8f, 0.3f, 0.25f), 0.3f));
            Prims.Shape(PrimitiveType.Cylinder, _root, "Bucket", new Vector3(0.9f, 0.2f, 0.85f), new Vector3(0.3f, 0.2f, 0.3f), Mats.Lit(new Color(0.25f, 0.45f, 0.7f), 0.3f));
            // 门边的垃圾袋
            Prims.Shape(PrimitiveType.Sphere, _root, "Trash", new Vector3(w - 0.6f, 0.25f, 1.9f), new Vector3(0.45f, 0.5f, 0.45f), Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f));
            // 墙上钉的一根晾衣绳和两件旧衣服
            Prims.Box(_root, "Line", new Vector3(w * 0.5f, 2.1f, Depth - 0.06f), new Vector3(2.0f, 0.015f, 0.015f), Mats.Lit(new Color(0.2f, 0.2f, 0.2f)));
        }

        private void BuildUpgrades()
        {
            float w = Width, d = Depth;
            // 二手空调（挂在床上方）
            From(2, Furniture(null, "AirCon", new Vector3(1.2f, 2.4f, d - 0.15f), new Vector3(1.1f, 0.3f, 0.25f), new Color(0.88f, 0.88f, 0.86f), 0.3f));
            From(2, Prims.Box(_root, "AirConLed", new Vector3(1.55f, 2.4f, d - 0.285f), new Vector3(0.06f, 0.03f, 0.02f), Mats.Glow(new Color(0.1f, 0.4f, 0.1f), new Color(0.3f, 3f, 0.6f), false)));
            // 窗帘
            From(3, Furniture(null, "CurtainL", new Vector3(2.45f, 1.75f, d - 0.13f), new Vector3(0.28f, 1.4f, 0.04f), new Color(0.6f, 0.4f, 0.45f), 0.02f));
            From(3, Furniture(null, "CurtainR", new Vector3(3.85f, 1.75f, d - 0.13f), new Vector3(0.28f, 1.4f, 0.04f), new Color(0.6f, 0.4f, 0.45f), 0.02f));
            From(3, Furniture(null, "CurtainRod", new Vector3(3.15f, 2.45f, d - 0.13f), new Vector3(1.6f, 0.03f, 0.03f), new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            // 热水器
            From(3, Furniture(null, "Heater", new Vector3(w - 0.2f, 2.0f, 4.15f), new Vector3(0.3f, 0.55f, 0.4f), new Color(0.9f, 0.9f, 0.92f), 0.4f));
            // 小书桌上的台灯（在桌上）
            From(3, Prims.Box(_root, "DeskLamp", new Vector3(5.1f, 0.95f, d - 0.5f), new Vector3(0.08f, 0.35f, 0.08f), Mats.Lit(new Color(0.2f, 0.2f, 0.25f), 0.3f, 0.5f)));
            From(3, Prims.Box(_root, "DeskLampHead", new Vector3(5.1f, 1.15f, d - 0.5f), new Vector3(0.22f, 0.06f, 0.18f), Mats.Glow(new Color(0.9f, 0.9f, 0.7f), new Color(3f, 2.8f, 1.8f), false)));
        }

        private void ApplyLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            if (level == _level) return;
            _level = level;
            for (int i = 0; i < 4; i++)
            {
                foreach (GameObject go in _levelOnly[i]) go.SetActive(i == level);
                foreach (GameObject go in _levelFrom[i]) go.SetActive(level >= i);
            }
            // 风扇：开局是坏的（头耷拉着、不转）；二手空调之后修好
            _fanHead.localRotation = level >= 2 ? Quaternion.identity : Quaternion.Euler(0, 0, -38f);
        }

        // ---------- 走动 ----------

        public void ResetOwner()
        {
            _owner.transform.localPosition = new Vector3(Width - 1.15f, 0, 1.3f);
            _owner.transform.localRotation = Quaternion.LookRotation(new Vector3(-1, 0, 0.3f));
        }

        public void SetActive(bool on)
        {
            _root.gameObject.SetActive(on);
            if (on && !_wasOn) ResetOwner();
            _wasOn = on;
        }

        /// <summary>WASD：按世界方向走一步，被家具挡住时沿边滑。</summary>
        public void MoveOwner(Vector3 worldDir, float realDt)
        {
            Vector3 d = new Vector3(worldDir.x, 0, worldDir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            Transform tr = _owner.transform;
            Vector3 p = tr.localPosition;
            float step = 2.6f * realDt;
            Vector3 tx = p + new Vector3(d.x * step, 0, 0);
            if (CanStand(tx, p)) p = tx;
            Vector3 tz = p + new Vector3(0, 0, d.z * step);
            if (CanStand(tz, p)) p = tz;
            tr.localPosition = p;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(d), 0.35f);
        }

        private bool CanStand(Vector3 to, Vector3 from)
        {
            if (to.x < 0.3f || to.x > Width - 0.3f || to.z < 0.3f || to.z > Depth - 0.3f) return false;
            if (!Blocked(to)) return true;
            return Blocked(from);
        }

        private bool Blocked(Vector3 p)
        {
            foreach (Rect r in _blockers)
                if (r.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        private static float DistToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public void Update(GameSession s, float realDt, float minuteOfDay)
        {
            ApplyLevel(s.HomeComfort);
            if (_level >= 2) _blades.Rotate(Vector3.right, 900f * realDt, Space.Self);

            // 窗外的天光：白天亮、夜里黑
            float hour = minuteOfDay / 60f;
            float day = Mathf.Clamp01(Mathf.Min(hour - 6f, 19f - hour) / 1.5f);
            _windowMat.SetColor("_EmissionColor", Color.Lerp(new Color(0.03f, 0.04f, 0.07f), new Color(1.3f, 1.55f, 1.9f), day));
            _windowLight.intensity = day * 0.9f;

            // 够得着的家具
            Vector3 lp = _owner.transform.localPosition;
            Vector2 at = new Vector2(lp.x, lp.z);
            string best = null, label = null;
            float bestDist = 0.75f;
            foreach (Spot sp in _spots)
            {
                float dd = DistToRect(at, sp.Area);
                if (dd < bestDist)
                {
                    bestDist = dd;
                    best = sp.Id;
                    label = sp.Label;
                }
            }
            NearbyId = best;
            NearbyLabel = label;
        }

        public void Destroy() => Kill.It(_root.gameObject);
    }
}
