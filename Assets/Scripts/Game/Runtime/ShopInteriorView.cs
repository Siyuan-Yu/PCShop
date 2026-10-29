using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在店内顾客身上，点击排队的顾客即结账。</summary>
    public sealed class CustomerMarker : MonoBehaviour
    {
        public int CustomerId;
    }

    /// <summary>
    /// 玩家店铺的室内（按规格生成：小 48㎡ / 中 108㎡ / 大 216㎡）。放在远离城市的位置，进店时镜头切过来。
    /// 顾客进门 → 去货架前挑货 → 到柜台前排队（头顶有 ¥）→ 结账后出门。
    /// </summary>
    public sealed class ShopInteriorView
    {
        public static readonly Vector3 Origin = new Vector3(600, 0, -400);

        private sealed class Visitor
        {
            public GameObject Go;
            public Vector3 Target;
            public bool Leaving;
            public GameObject Bubble;

            /// <summary>0 = 逛货架；1 = 挑中了，去货架前拿货；2 = 拿着货排队等结账。</summary>
            public int Phase;
            public Vector3 PickSpot;
            public PartCategory ItemCat;
            public bool ItemBoxed;
            public GameObject Held;
            public float BrowseTimer;
            public bool Build;

            /// <summary>拿的是展示台上的整机（不是货架上的配件）。</summary>
            public bool DisplayPc;
            public int PcTier;

            /// <summary>离开时的路线；dropFirst 表示先走到货架前把货放回去。</summary>
            public bool DropFirst;
            public bool Dropped;

            /// <summary>先依次走过这些点（街上 → 门口 → 店里），再走向 Target。</summary>
            public readonly List<Vector3> Via = new List<Vector3>();

            /// <summary>绕开展示桌的拐点（目标变了才重新规划）。</summary>
            public readonly List<Vector3> Detours = new List<Vector3>();
            public Vector3 PlannedFor = new Vector3(float.NaN, 0, 0);

            /// <summary>冒烟用：没到目标、又没走出多远的时间（原地打转就会一直涨）。</summary>
            public Vector3 StuckAnchor;
            public float StuckFor;

            public PartCategory Want;
            public string Spend;
            public string Profile;
            public string Emote;
            public Color EmoteColor;
            public float EmoteUntil;
        }

        /// <summary>顾客头顶的小字（想买什么、为什么走了、收了多少钱）。</summary>
        public struct EmoteInfo
        {
            public Vector3 Pos;
            public string Text;
            public Color Color;
            public int Size;

            /// <summary>画成白底对话气泡（顾客说话），否则是带阴影的飘字。</summary>
            public bool Bubble;
        }

        private sealed class Pop
        {
            public Vector3 Start;
            public string Text;
            public float T0;
        }

        private readonly List<Pop> _pops = new List<Pop>();
        private readonly List<EmoteInfo> _emotes = new List<EmoteInfo>();
        private float _now;

        /// <summary>本帧所有要显示的头顶小字。</summary>
        public IReadOnlyList<EmoteInfo> Emotes => _emotes;

        /// <summary>老板站在柜台旁边（能给排队的人结账）。</summary>
        public bool NearCounter { get; private set; }

        public Vector3 CounterWorld => Origin + new Vector3(0.9f, 2.3f, CounterZ);

        private static string LeaveText(LeaveReason r, out Color color)
        {
            color = new Color(1f, 0.55f, 0.45f);
            switch (r)
            {
                case LeaveReason.Impatient: return "等太久了！";
                case LeaveReason.TooExpensive: return "太贵了……";
                case LeaveReason.NoStock: return "怎么没货？";
                case LeaveReason.NoCash: return "没带现金";
                case LeaveReason.ShopClosed: return "关门了？";
                case LeaveReason.OrderPlaced:
                    color = new Color(0.5f, 0.95f, 0.6f);
                    return "下单了，装好叫我";
                default: return null;
            }
        }

        private readonly Transform _root;
        private readonly Dictionary<int, Visitor> _visitors = new Dictionary<int, Visitor>();
        private readonly List<Visitor> _leaving = new List<Visitor>();
        private readonly List<Vector3> _shelfSpots = new List<Vector3>();
        private readonly List<Vector2> _tables = new List<Vector2>();
        private readonly List<ShopFixtures.PcSlot> _pcSlots = new List<ShopFixtures.PcSlot>();

        /// <summary>展示桌占的地（世界坐标 x/z），顾客走路绕开。</summary>
        private readonly List<Rect> _walkAround = new List<Rect>();
        private readonly List<ShelfDisplay> _shelves = new List<ShelfDisplay>();
        private readonly System.Random _rng = new System.Random(5);
        private readonly GameObject _owner;
        private readonly GameObject _clerk;
        private readonly Vector3 _door;
        private readonly float _doorX;
        private readonly Vector3 _counterFront;
        private float _stockTimer;
        private ShopSimulation _hooked;
        private readonly Dictionary<int, LeaveReason> _leaveReasons = new Dictionary<int, LeaveReason>();

        private readonly Dictionary<int, PayChannel> _payChannels = new Dictionary<int, PayChannel>();

        private void OnCustomerLeft(ShopCustomer c) => _leaveReasons[c.Id] = c.Reason;

        private void OnCustomerPaid(ShopCustomer c)
        {
            _payChannels[c.Id] = c.PaidWith;
            SoundFx.Play(Sfx.Coin);
        }

        private void OnCustomerArrived(ShopCustomer c) => SoundFx.Play(Sfx.Bell);

        private static string PayText(PayChannel p)
        {
            switch (p)
            {
                case PayChannel.QrCode: return "扫码";
                case PayChannel.SelfCheckout: return "自助";
                default: return "现金";
            }
        }

        /// <summary>顾客的类型：决定衣服颜色、身高和头顶的称呼（装机党、游戏玩家、DIY 爱好者、办公族、学生）。</summary>
        private static string ProfileOf(ShopCustomer c, out Color shirt)
        {
            if (c.Intent == CustomerIntent.Build) { shirt = new Color(0.92f, 0.55f, 0.15f); return "装机党"; }
            switch (c.DesiredCategory)
            {
                case PartCategory.Gpu: shirt = new Color(0.55f, 0.3f, 0.78f); return "游戏玩家";
                case PartCategory.Cpu:
                case PartCategory.Motherboard: shirt = new Color(0.25f, 0.66f, 0.4f); return "DIY 爱好者";
                case PartCategory.Memory:
                case PartCategory.Storage: shirt = new Color(0.35f, 0.5f, 0.72f); return "办公族";
                default: shirt = new Color(0.2f, 0.66f, 0.72f); return "学生";
            }
        }

        private void Hook(ShopSimulation shop)
        {
            if (_hooked == shop) return;
            if (_hooked != null) { _hooked.CustomerLeft -= OnCustomerLeft; _hooked.CustomerPaid -= OnCustomerPaid; _hooked.CustomerArrived -= OnCustomerArrived; }
            _hooked = shop;
            _leaveReasons.Clear();
            if (shop != null) { shop.CustomerLeft += OnCustomerLeft; shop.CustomerPaid += OnCustomerPaid; shop.CustomerArrived += OnCustomerArrived; }
        }

        /// <summary>某个品类的货架前（离 from 最近的那个）；没有这个品类的货架就随便一个。</summary>
        private Vector3 ShelfSpotFor(PartCategory cat, Vector3 from, System.Random rng = null)
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < _shelves.Count && i < _shelfSpots.Count; i++)
                if (_shelves[i].Category == cat) spots.Add(_shelfSpots[i]);
            if (spots.Count == 0) return _shelfSpots.Count > 0 ? _shelfSpots[0] : Center;
            if (rng != null) return spots[rng.Next(spots.Count)];
            Vector3 best = spots[0];
            float bd = float.MaxValue;
            foreach (Vector3 s in spots)
            {
                float d = (s - from).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        private Vector3 RandomDeskSpot()
        {
            int deskCount = _shelfSpots.Count - _shelves.Count;
            if (deskCount <= 0) return Center;
            return _shelfSpots[_shelves.Count + _rng.Next(deskCount)];
        }

        private Vector3 Jitter(Vector3 p) => p + new Vector3((float)_rng.NextDouble() * 0.6f - 0.3f, 0, (float)_rng.NextDouble() * 0.3f - 0.15f);

        private static bool Reached(Visitor v)
        {
            if (v.Via.Count > 0) return false;
            Vector3 d = v.Go.transform.position - v.Target;
            d.y = 0;
            return d.sqrMagnitude < 0.04f;
        }

        private static Color CategoryColor(PartCategory c)
        {
            switch (c)
            {
                case PartCategory.Gpu: return new Color(0.12f, 0.12f, 0.14f);
                case PartCategory.Cpu: return new Color(0.85f, 0.55f, 0.15f);
                case PartCategory.Memory: return new Color(0.2f, 0.65f, 0.35f);
                case PartCategory.Storage: return new Color(0.25f, 0.45f, 0.85f);
                case PartCategory.PowerCase: return new Color(0.75f, 0.75f, 0.78f);
                default: return new Color(0.7f, 0.25f, 0.3f);
            }
        }

        private Vector3 NearestDeskSpot(Vector3 from)
        {
            int deskCount = _shelfSpots.Count - _shelves.Count;
            if (deskCount <= 0) return Center;
            Vector3 best = _shelfSpots[_shelves.Count];
            for (int i = _shelves.Count; i < _shelfSpots.Count; i++)
                if ((_shelfSpots[i] - from).sqrMagnitude < (best - from).sqrMagnitude) best = _shelfSpots[i];
            return best;
        }

        private void ShowHeld(Visitor v)
        {
            if (v.Held != null) return;
            if (v.DisplayPc)
            {
                v.Held = Prims.Box(v.Go.transform, "HeldPc", new Vector3(0, 0.95f, 0.4f), new Vector3(0.3f, 0.5f, 0.42f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f));
                return;
            }
            v.Held = Prims.Box(v.Go.transform, "Held", new Vector3(0, 0.95f, 0.38f), new Vector3(0.3f, 0.2f, 0.36f), Mats.Lit(CategoryColor(v.ItemCat), 0.5f));
        }

        public ShopSize Size { get; }
        public float Width { get; }
        public float Depth { get; }
        public Vector3 Center => Origin + new Vector3(Width / 2, 0, Depth / 2);
        /// <summary>刚好看到整间店的镜头大小（缩放上限）。</summary>
        public float CameraSize => Mathf.Max(Width, Depth + 3.5f) * 0.62f + 1.5f;

        /// <summary>进店时默认的镜头大小：比较近，跟着老板走。</summary>
        public const float DefaultZoom = 5.5f;
        public const float MinZoom = 3.2f;

        public Vector3 OwnerWorld => _owner.transform.position;
        private float CounterZ => Depth * 0.55f;
        private float CounterLength => Mathf.Min(2.2f, Depth * 0.5f);
        private bool _wasOn;
        private readonly List<GameObject> _buildGlow = new List<GameObject>();
        private readonly List<Rect> _blockers = new List<Rect>();
        private RoomFurniture _decor;

        /// <summary>摆出来的店内摆设（装修后由玩家摆放）。</summary>
        public RoomFurniture Furniture => _decor;
        public Transform Root => _root;

        /// <summary>店里固定的东西（柜台、装机台、展示桌）占的地，摆放摆设时不能压上去。</summary>
        public List<RectD> FixedBlockers()
        {
            var l = new List<RectD>();
            foreach (Rect r in _blockers) l.Add(new RectD(r.xMin, r.yMin, r.xMax, r.yMax));
            return l;
        }

        /// <summary>顾客 ID → 世界坐标头顶位置（给 UI 标签用）。</summary>
        public IEnumerable<KeyValuePair<int, Vector3>> QueueHeads()
        {
            foreach (var kv in _visitors)
            {
                if (kv.Value.Bubble != null && kv.Value.Bubble.activeSelf)
                    yield return new KeyValuePair<int, Vector3>(kv.Key, kv.Value.Go.transform.position + Vector3.up * 2.2f);
            }
        }

        /// <summary>摆设：门边两盆绿植、地毯、墙上的钟和海报，让店里不那么空。</summary>
        private void BuildDecor(float w, float d, float doorX)
        {
            Material pot = Mats.Lit(new Color(0.55f, 0.32f, 0.2f), 0.4f);
            Material leaf = Mats.Lit(new Color(0.22f, 0.5f, 0.28f), 0.3f);
            Material leaf2 = Mats.Lit(new Color(0.3f, 0.62f, 0.34f), 0.3f);
            foreach (float px in new[] { doorX - 1.3f, doorX + 1.3f })
            {
                Prims.Shape(PrimitiveType.Cylinder, _root, "PlantPot", new Vector3(px, 0.2f, 0.4f), new Vector3(0.38f, 0.2f, 0.38f), pot);
                Prims.Shape(PrimitiveType.Sphere, _root, "PlantLeaf", new Vector3(px, 0.62f, 0.4f), new Vector3(0.55f, 0.5f, 0.55f), leaf);
                Prims.Shape(PrimitiveType.Sphere, _root, "PlantLeaf", new Vector3(px + 0.08f, 0.92f, 0.36f), new Vector3(0.36f, 0.34f, 0.36f), leaf2);
            }

            // 后墙：挂钟 + 海报
            Prims.Shape(PrimitiveType.Cylinder, _root, "Clock", new Vector3(w * 0.62f, 2.45f, d - 0.03f), new Vector3(0.42f, 0.02f, 0.42f), Mats.Lit(new Color(0.95f, 0.95f, 0.92f), 0.3f)).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Prims.Box(_root, "ClockHandH", new Vector3(w * 0.62f, 2.5f, d - 0.05f), new Vector3(0.025f, 0.12f, 0.01f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f)));
            Prims.Box(_root, "ClockHandM", new Vector3(w * 0.62f + 0.05f, 2.45f, d - 0.05f), new Vector3(0.1f, 0.02f, 0.01f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f)));
            Prims.Box(_root, "PosterB", new Vector3(w * 0.78f, 2.4f, d - 0.01f), new Vector3(0.7f, 0.9f, 0.02f), Mats.Glow(new Color(0.9f, 0.4f, 0.3f), new Color(1.6f, 0.6f, 0.4f), false));
            // 右墙海报
            Prims.Box(_root, "PosterR", new Vector3(w - 0.01f, 2.4f, d * 0.5f), new Vector3(0.02f, 0.7f, 1.0f), Mats.Glow(new Color(0.5f, 0.8f, 0.4f), new Color(0.7f, 1.4f, 0.6f), false));
        }

        // ---------- 门外的街 ----------

        /// <summary>门外的人行道 / 马路往店两边各伸出去多远。</summary>
        private const float StreetExtent = 14f;
        private const float CurbZ = -3.0f;
        private const float RoadFarZ = -9.4f;

        private static float SidewalkZ(System.Random rng) => -0.9f - (float)rng.NextDouble() * 1.5f;

        private sealed class Walker
        {
            public GameObject Go;
            public Vector3 Target;
            public float Speed;
            public float Bob;
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly List<Walker> _cars = new List<Walker>();
        private readonly List<Light> _streetLamps = new List<Light>();
        private float _walkerTimer, _carTimer;

        /// <summary>
        /// 店门外：人行道、马路（带车道线）、路灯，右边是隔壁店的门面，左边是矮花坛。
        /// 镜头从左前方斜着看进店里，所以店的左边、前面不能放高的东西挡住柜台。
        /// </summary>
        private void BuildStreet(float w, float d, float doorX)
        {
            float x0 = -StreetExtent, x1 = w + StreetExtent, len = x1 - x0, cx = (x0 + x1) / 2;
            Prims.Box(_root, "Outside", new Vector3(cx, -0.12f, d / 2 - 6f), new Vector3(len + 20f, 0.05f, d + 40f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f)));
            Prims.Box(_root, "Sidewalk", new Vector3(cx, -0.03f, CurbZ / 2), new Vector3(len, 0.06f, -CurbZ), Mats.Lit(new Color(0.55f, 0.54f, 0.52f), 0.15f));
            Material seam = Mats.Lit(new Color(0.45f, 0.44f, 0.43f), 0.1f);
            for (float x = x0 + 1.5f; x < x1; x += 1.5f)
                Prims.Box(_root, "Paving", new Vector3(x, 0.002f, CurbZ / 2), new Vector3(0.04f, 0.01f, -CurbZ), seam);
            Prims.Box(_root, "Curb", new Vector3(cx, 0.04f, CurbZ), new Vector3(len, 0.16f, 0.2f), Mats.Lit(new Color(0.7f, 0.7f, 0.68f), 0.2f));
            Prims.Box(_root, "Road", new Vector3(cx, -0.06f, (CurbZ + RoadFarZ) / 2), new Vector3(len, 0.04f, CurbZ - RoadFarZ), Mats.Lit(new Color(0.2f, 0.2f, 0.22f), 0.25f));
            Material paint = Mats.Lit(new Color(0.9f, 0.88f, 0.75f), 0.2f);
            float mid = (CurbZ + RoadFarZ) / 2;
            for (float x = x0 + 1f; x < x1; x += 3.2f)
                Prims.Box(_root, "LaneDash", new Vector3(x, -0.035f, mid), new Vector3(1.6f, 0.01f, 0.14f), paint);
            Prims.Box(_root, "CurbFar", new Vector3(cx, 0.0f, RoadFarZ - 0.1f), new Vector3(len, 0.12f, 0.2f), Mats.Lit(new Color(0.6f, 0.6f, 0.58f), 0.2f));

            // 门前的台阶
            Prims.Box(_root, "Step", new Vector3(doorX, 0.03f, -0.35f), new Vector3(1.6f, 0.06f, 0.6f), Mats.Lit(new Color(0.42f, 0.42f, 0.44f), 0.2f));

            // 路灯：放在店两边，不挡门
            Material pole = Mats.Lit(new Color(0.22f, 0.24f, 0.27f), 0.4f, 0.4f);
            foreach (float lx in new[] { -2.5f, w + 2.5f, -10f, w + 10f })
            {
                Prims.Box(_root, "LampPole", new Vector3(lx, 1.9f, CurbZ + 0.35f), new Vector3(0.12f, 3.8f, 0.12f), pole);
                Prims.Box(_root, "LampArm", new Vector3(lx, 3.75f, CurbZ + 0.0f), new Vector3(0.1f, 0.08f, 0.8f), pole);
                Prims.Box(_root, "LampHead", new Vector3(lx, 3.65f, CurbZ - 0.35f), new Vector3(0.35f, 0.12f, 0.45f), Mats.Glow(new Color(0.9f, 0.85f, 0.6f), new Color(3f, 2.6f, 1.4f)));
                var lg = new GameObject("StreetLamp");
                lg.transform.SetParent(_root, false);
                lg.transform.localPosition = new Vector3(lx, 3.4f, CurbZ - 0.3f);
                var light = lg.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 0f;
                light.color = new Color(1f, 0.82f, 0.55f);
                _streetLamps.Add(light);
            }

            // 右边隔壁：一家奶茶店的门面（在店的右后方，不挡视线）
            float nx0 = w + 0.15f, nw = StreetExtent - 0.15f;
            Prims.Box(_root, "NeighborR", new Vector3(nx0 + nw / 2, 1.7f, d / 2), new Vector3(nw, 3.4f, d), Mats.Lit(new Color(0.78f, 0.7f, 0.6f), 0.1f));
            Prims.Box(_root, "NeighborRWindow", new Vector3(nx0 + 2.6f, 1.2f, -0.02f), new Vector3(3.2f, 1.6f, 0.04f), Mats.Glow(new Color(0.35f, 0.42f, 0.48f), new Color(1.4f, 1.2f, 0.8f), false));
            Prims.Box(_root, "NeighborRDoor", new Vector3(nx0 + 5.0f, 1.0f, -0.02f), new Vector3(1.1f, 2.0f, 0.04f), Mats.Lit(new Color(0.3f, 0.22f, 0.18f), 0.3f));
            Prims.Box(_root, "NeighborRSign", new Vector3(nx0 + 3.6f, 2.75f, -0.05f), new Vector3(5.2f, 0.55f, 0.08f), Mats.Glow(new Color(0.85f, 0.35f, 0.5f), new Color(2.6f, 0.8f, 1.3f), false));
            Prims.Box(_root, "NeighborRAwning", new Vector3(nx0 + 3.6f, 2.35f, -0.45f), new Vector3(5.4f, 0.08f, 0.9f), Mats.Lit(new Color(0.75f, 0.2f, 0.25f), 0.2f));
            Prims.Box(_root, "NeighborR2", new Vector3(nx0 + 9.6f, 1.6f, -0.02f), new Vector3(3.6f, 1.4f, 0.04f), Mats.Glow(new Color(0.3f, 0.35f, 0.3f), new Color(0.6f, 1.3f, 0.7f), false));

            // 左边：矮花坛 + 共享单车，不挡柜台
            Material planter = Mats.Lit(new Color(0.5f, 0.48f, 0.45f), 0.1f);
            Material bush = Mats.Lit(new Color(0.24f, 0.46f, 0.26f), 0.2f);
            Prims.Box(_root, "PlanterL", new Vector3(-1.6f, 0.2f, d / 2), new Vector3(2.4f, 0.4f, d - 0.6f), planter);
            Prims.Box(_root, "BushL", new Vector3(-1.6f, 0.5f, d / 2), new Vector3(2.1f, 0.3f, d - 0.9f), bush);
            Material bike = Mats.Lit(new Color(0.95f, 0.75f, 0.1f), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float bx = -6.5f + i * 0.7f;
                Prims.Box(_root, "Bike", new Vector3(bx, 0.45f, -0.7f), new Vector3(0.08f, 0.5f, 1.3f), bike);
                Prims.Box(_root, "BikeSeat", new Vector3(bx, 0.75f, -0.5f), new Vector3(0.14f, 0.05f, 0.25f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)));
            }
        }

        private static readonly Color[] Shirts =
        {
            new Color(0.8f, 0.2f, 0.25f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.25f, 0.25f, 0.25f),
            new Color(0.6f, 0.3f, 0.7f), new Color(0.3f, 0.7f, 0.4f), new Color(0.85f, 0.6f, 0.2f), new Color(0.4f, 0.55f, 0.6f),
        };

        /// <summary>人流：白天多、半夜几乎没人（0 ~ 1）。</summary>
        private static float StreetBusy(float minuteOfDay)
        {
            float h = minuteOfDay / 60f;
            if (h < 6f) return 0.05f;
            if (h < 8f) return Mathf.Lerp(0.2f, 0.8f, (h - 6f) / 2f);
            if (h < 21f) return h > 11.5f && h < 13.5f || h > 17.5f && h < 19.5f ? 1f : 0.75f;
            if (h < 24f) return Mathf.Lerp(0.6f, 0.1f, (h - 21f) / 3f);
            return 0.05f;
        }

        /// <summary>门外的路人和车：只是背景，随时间多少变化，不影响生意。</summary>
        private void UpdateStreet(float realDt, float minuteOfDay)
        {
            float busy = StreetBusy(minuteOfDay);
            float h = minuteOfDay / 60f;
            float lampOn = h < 6.3f || h > 18.6f ? 1f : 0f;
            foreach (Light l in _streetLamps) l.intensity = lampOn * 1.6f;

            _walkerTimer -= realDt;
            if (_walkerTimer <= 0 && _walkers.Count < 4 + Mathf.RoundToInt(busy * 10))
            {
                _walkerTimer = Mathf.Lerp(9f, 0.9f, busy) * (0.6f + (float)_rng.NextDouble() * 0.8f);
                SpawnWalker();
            }
            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                Walker wk = _walkers[i];
                Transform tr = wk.Go.transform;
                Vector3 dv = wk.Target - tr.position;
                dv.y = 0;
                float m = dv.magnitude;
                if (m < 0.1f)
                {
                    Kill.It(wk.Go);
                    _walkers.RemoveAt(i);
                    continue;
                }
                wk.Bob += realDt * wk.Speed * 6f;
                Vector3 p = tr.position + dv / m * Mathf.Min(m, wk.Speed * realDt);
                p.y = Origin.y + Mathf.Abs(Mathf.Sin(wk.Bob)) * 0.04f;
                tr.position = p;
                tr.rotation = Quaternion.LookRotation(dv / m);
            }

            _carTimer -= realDt;
            if (_carTimer <= 0 && _cars.Count < 5)
            {
                _carTimer = Mathf.Lerp(14f, 2.5f, busy) * (0.6f + (float)_rng.NextDouble() * 0.8f);
                SpawnCar();
            }
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                Walker car = _cars[i];
                Transform tr = car.Go.transform;
                Vector3 dv = car.Target - tr.position;
                dv.y = 0;
                float m = dv.magnitude;
                if (m < 0.2f)
                {
                    Kill.It(car.Go);
                    _cars.RemoveAt(i);
                    continue;
                }
                tr.position += dv / m * Mathf.Min(m, car.Speed * realDt);
            }
        }

        private void SpawnWalker()
        {
            bool fromLeft = _rng.NextDouble() < 0.5;
            float z = SidewalkZ(_rng);
            float xa = -StreetExtent + 0.5f, xb = Width + StreetExtent - 0.5f;
            Color shirt = Shirts[_rng.Next(Shirts.Length)];
            Color[] skins = { new Color(0.92f, 0.78f, 0.62f), new Color(0.85f, 0.66f, 0.5f), new Color(0.98f, 0.84f, 0.72f) };
            GameObject go = Prims.Figure(_root, "Passerby", shirt, skins[_rng.Next(skins.Length)], 0.85f + (float)_rng.NextDouble() * 0.25f);
            if (_rng.NextDouble() < 0.35)
                Prims.Box(go.transform, "Bag", new Vector3(0.32f, 0.6f, 0), new Vector3(0.12f, 0.3f, 0.28f), Mats.Lit(Shirts[_rng.Next(Shirts.Length)], 0.3f));
            go.transform.position = Origin + new Vector3(fromLeft ? xa : xb, 0, z);
            _walkers.Add(new Walker
            {
                Go = go,
                Target = Origin + new Vector3(fromLeft ? xb : xa, 0, z + ((float)_rng.NextDouble() - 0.5f) * 0.6f),
                Speed = 1.0f + (float)_rng.NextDouble() * 0.7f,
            });
        }

        private void SpawnCar()
        {
            bool eastbound = _rng.NextDouble() < 0.5;
            float z = eastbound ? CurbZ - 1.6f : RoadFarZ + 1.6f;
            float xa = -StreetExtent - 2f, xb = Width + StreetExtent + 2f;
            var root = new GameObject("Car");
            root.transform.SetParent(_root, false);
            root.transform.position = Origin + new Vector3(eastbound ? xa : xb, 0, z);
            root.transform.rotation = Quaternion.LookRotation(eastbound ? Vector3.right : Vector3.left);
            Color body = Shirts[_rng.Next(Shirts.Length)];
            bool taxi = _rng.NextDouble() < 0.3;
            if (taxi) body = new Color(0.95f, 0.78f, 0.15f);
            Prims.Box(root.transform, "Body", new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.55f, 3.8f), Mats.Lit(body, 0.6f, 0.2f));
            Prims.Box(root.transform, "Cabin", new Vector3(0, 0.95f, -0.2f), new Vector3(1.4f, 0.5f, 2.0f), Mats.Lit(new Color(0.15f, 0.18f, 0.22f), 0.8f));
            Prims.Box(root.transform, "HeadL", new Vector3(-0.55f, 0.5f, 1.91f), new Vector3(0.3f, 0.14f, 0.03f), Mats.Glow(new Color(0.9f, 0.9f, 0.8f), new Color(3f, 3f, 2.4f)));
            Prims.Box(root.transform, "HeadR", new Vector3(0.55f, 0.5f, 1.91f), new Vector3(0.3f, 0.14f, 0.03f), Mats.Glow(new Color(0.9f, 0.9f, 0.8f), new Color(3f, 3f, 2.4f)));
            Prims.Box(root.transform, "Tail", new Vector3(0, 0.5f, -1.91f), new Vector3(1.3f, 0.1f, 0.03f), Mats.Glow(new Color(0.6f, 0.1f, 0.1f), new Color(2.4f, 0.2f, 0.2f)));
            if (taxi) Prims.Box(root.transform, "TaxiSign", new Vector3(0, 1.28f, -0.2f), new Vector3(0.5f, 0.15f, 0.25f), Mats.Glow(new Color(0.9f, 0.9f, 0.9f), new Color(2f, 2f, 1.6f), false));
            foreach (float wz in new[] { -1.2f, 1.2f })
            foreach (float wx in new[] { -0.8f, 0.8f })
                Prims.Shape(PrimitiveType.Cylinder, root.transform, "Wheel", new Vector3(wx, 0.22f, wz), new Vector3(0.44f, 0.1f, 0.44f), Mats.Lit(new Color(0.08f, 0.08f, 0.08f))).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            _cars.Add(new Walker { Go = root, Target = Origin + new Vector3(eastbound ? xb : xa, 0, z), Speed = 6f + (float)_rng.NextDouble() * 4f });
        }

        public ShopInteriorView(Transform parent, ShopSize size)
        {
            Size = size;
            _root = new GameObject("ShopInterior").transform;
            _root.SetParent(parent, false);
            _root.position = Origin;
            _decor = new RoomFurniture(_root);

            (Width, Depth) = size == ShopSize.Large ? (18f, 12f) : size == ShopSize.Medium ? (12f, 9f) : (8f, 6f);
            float w = Width, d = Depth;

            Prims.Box(_root, "Floor", new Vector3(w / 2, -0.05f, d / 2), new Vector3(w, 0.1f, d), Mats.Lit(new Color(0.78f, 0.76f, 0.72f), 0.4f), true);
            Material tileMat = Mats.Lit(new Color(0.7f, 0.69f, 0.66f), 0.4f);
            for (int tx = 0; tx * 2f < w; tx++)
            for (int tz = 0; tz * 2f < d; tz++)
            {
                if ((tx + tz) % 2 != 0) continue;
                float sx = Mathf.Min(2f, w - tx * 2f), sz = Mathf.Min(2f, d - tz * 2f);
                Prims.Box(_root, "Tile", new Vector3(tx * 2f + sx / 2, 0.005f, tz * 2f + sz / 2), new Vector3(sx, 0.012f, sz), tileMat);
            }
            BuildStreet(w, d, w - 3.0f);
            Prims.Box(_root, "WallBack", new Vector3(w / 2, 1.5f, d + 0.05f), new Vector3(w + 0.2f, 3f, 0.1f), Mats.Lit(new Color(0.86f, 0.88f, 0.9f)));
            Prims.Box(_root, "WallRight", new Vector3(w + 0.05f, 1.5f, d / 2), new Vector3(0.1f, 3f, d + 0.2f), Mats.Lit(new Color(0.8f, 0.83f, 0.86f)));
            Prims.Box(_root, "WallLeftLow", new Vector3(-0.05f, 0.3f, d / 2), new Vector3(0.1f, 0.6f, d), Mats.Lit(new Color(0.6f, 0.6f, 0.62f)));
            // 大门开在货架这一侧（右前方），收银台在左边：顾客进门先逛货架，再回左边结账
            float doorX = w - 3.0f;
            Material lowWall = Mats.Lit(new Color(0.6f, 0.6f, 0.62f));
            Prims.Box(_root, "WallFrontLowA", new Vector3((doorX - 0.7f) / 2, 0.3f, -0.05f), new Vector3(doorX - 0.7f, 0.6f, 0.1f), lowWall);
            Prims.Box(_root, "WallFrontLowB", new Vector3((doorX + 0.7f + w) / 2, 0.3f, -0.05f), new Vector3(w - doorX - 0.7f, 0.6f, 0.1f), lowWall);
            Prims.Box(_root, "DoorMat", new Vector3(doorX, 0.012f, 0.45f), new Vector3(1.3f, 0.014f, 0.8f), Mats.Lit(new Color(0.35f, 0.18f, 0.16f)));
            Material doorPost = Mats.Lit(new Color(0.3f, 0.32f, 0.36f), 0.5f);
            Prims.Box(_root, "DoorPostL", new Vector3(doorX - 0.72f, 0.7f, -0.05f), new Vector3(0.1f, 1.4f, 0.14f), doorPost);
            Prims.Box(_root, "DoorPostR", new Vector3(doorX + 0.72f, 0.7f, -0.05f), new Vector3(0.1f, 1.4f, 0.14f), doorPost);
            Prims.Box(_root, "DoorSign", new Vector3(doorX, 1.55f, -0.05f), new Vector3(1.5f, 0.2f, 0.05f), Mats.Glow(new Color(0.9f, 0.5f, 0.15f), new Color(2.4f, 1.3f, 0.3f), false));
            Prims.Box(_root, "Poster", new Vector3(w * 0.3f, 2f, d - 0.01f), new Vector3(1.2f, 0.8f, 0.02f), Mats.Glow(new Color(0.2f, 0.6f, 0.9f), new Color(0.4f, 1.2f, 1.8f), false));

            BuildDecor(w, d, doorX);

            int lampsX = Mathf.Max(1, Mathf.CeilToInt(w / 6f)), lampsZ = Mathf.Max(1, Mathf.CeilToInt(d / 6f));
            for (int lx = 0; lx < lampsX; lx++)
            for (int lz = 0; lz < lampsZ; lz++)
            {
                var lampGo = new GameObject("CeilingLight");
                lampGo.transform.SetParent(_root, false);
                lampGo.transform.localPosition = new Vector3(w * (lx + 0.5f) / lampsX, 2.8f, d * (lz + 0.5f) / lampsZ);
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 9f;
                lamp.intensity = lampsX * lampsZ > 1 ? 1.6f : 2.2f;
                lamp.color = new Color(1f, 0.95f, 0.85f);
            }

            // 布局：整机展示桌摆在店正中间（一进门就看到），货架沿后墙、右墙和左墙前部；
            // 柜台前到门口这一片留空，给顾客排队结账。
            var tables = new List<Vector2>();
            if (size == ShopSize.Small) tables.Add(new Vector2(w / 2 - 0.1f, d / 2 + 0.1f));
            else if (size == ShopSize.Medium)
            {
                tables.Add(new Vector2(w / 2 - 1.5f, d / 2));
                tables.Add(new Vector2(w / 2 + 1.5f, d / 2));
            }
            else
            {
                foreach (float tz in new[] { d / 2 - 1.5f, d / 2 + 1.5f })
                {
                    tables.Add(new Vector2(w / 2 - 1.5f, tz));
                    tables.Add(new Vector2(w / 2 + 1.5f, tz));
                }
            }

            // 货架：每类配件一个货架（显卡、CPU、内存、硬盘、机箱电源、主板），沿后墙和右墙均匀摆开
            PartCategory[] cats = { PartCategory.Gpu, PartCategory.Cpu, PartCategory.Memory, PartCategory.Storage, PartCategory.PowerCase, PartCategory.Motherboard };
            int backCount = size == ShopSize.Small ? 4 : 3;
            int shelfNo = 0;
            for (int i = 0; i < backCount; i++)
            {
                float x = Mathf.Lerp(1.9f, w - 0.9f, i / (float)(backCount - 1));
                _shelves.Add(ShopFixtures.BuildShelf(_root, new Vector3(x, 0, d - 0.2f), 0f, cats[shelfNo++]));
                _shelfSpots.Add(Origin + new Vector3(x, 0, d - 1.2f));
            }
            int sideCount = cats.Length - backCount;
            for (int i = 0; i < sideCount; i++)
            {
                float z = Mathf.Lerp(1.0f, d - 2.25f, sideCount == 1 ? 0f : i / (float)(sideCount - 1));
                _shelves.Add(ShopFixtures.BuildShelf(_root, new Vector3(w - 0.2f, 0, z), 90f, cats[shelfNo++]));
                _shelfSpots.Add(Origin + new Vector3(w - 1.2f, 0, z));
            }
            // 柜台 + 电脑（靠左前方）
            Vector3 counter = new Vector3(0.9f, 0.5f, d * 0.55f);
            Prims.Box(_root, "Counter", counter, new Vector3(0.8f, 1.0f, Mathf.Min(2.2f, d * 0.5f)), Mats.Lit(new Color(0.3f, 0.32f, 0.36f), 0.5f));
            Prims.Box(_root, "Monitor", counter + new Vector3(0, 0.75f, 0.2f), new Vector3(0.1f, 0.45f, 0.6f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 0.8f, 1.5f), false));
            Prims.Box(_root, "Register", counter + new Vector3(0, 0.6f, -0.5f), new Vector3(0.35f, 0.2f, 0.35f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)));

            // 装机台（靠左墙后部）：有装机单时屏幕亮着
            Prims.Box(_root, "Workbench", new Vector3(0.45f, 0.45f, d - 0.9f), new Vector3(0.6f, 0.9f, 1.3f), Mats.Lit(new Color(0.36f, 0.38f, 0.42f), 0.5f));
            Prims.Box(_root, "BenchCase", new Vector3(0.45f, 1.15f, d - 1.2f), new Vector3(0.35f, 0.5f, 0.5f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.6f));
            _buildGlow.Add(Prims.Box(_root, "BenchScreen", new Vector3(0.45f, 1.2f, d - 0.5f), new Vector3(0.06f, 0.4f, 0.45f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 1.4f, 0.6f), false)));
            Prims.Box(_root, "BenchParts", new Vector3(0.4f, 0.95f, d - 1.0f), new Vector3(0.3f, 0.08f, 0.3f), Mats.Lit(new Color(0.15f, 0.5f, 0.3f)));

            // 整机展示区：店中间的长桌，摆上去的整机开着机，顾客能直接买走
            _tables.Clear();
            foreach (Vector2 tb in tables)
            {
                _tables.Add(tb);
                _pcSlots.AddRange(ShopFixtures.BuildPcDesk(_root, new Vector3(tb.x, 0, tb.y)));
                _shelfSpots.Add(Origin + new Vector3(tb.x, 0, tb.y - 1.15f));
            }
            _blockers.Add(new Rect(0.5f, CounterZ - CounterLength / 2 - 0.15f, 0.95f, CounterLength + 0.3f));
            _blockers.Add(new Rect(0f, d - 1.7f, 0.9f, 1.6f));
            foreach (Vector2 tb in tables) _blockers.Add(new Rect(tb.x - 1.4f, tb.y - 0.6f, 2.8f, 1.2f));
            // 顾客绕着走的区域：同一排的桌子连成一块
            var rows = new Dictionary<float, Rect>();
            foreach (Vector2 tb in tables)
            {
                var r = new Rect(tb.x - 1.25f, tb.y - 0.5f, 2.5f, 1.0f);
                rows[tb.y] = rows.TryGetValue(tb.y, out Rect old) ? Rect.MinMaxRect(Mathf.Min(old.xMin, r.xMin), r.yMin, Mathf.Max(old.xMax, r.xMax), r.yMax) : r;
            }
            foreach (Rect r in rows.Values) _walkAround.Add(new Rect(r.x + Origin.x, r.y + Origin.z, r.width, r.height));

            _owner = Prims.Figure(_root, "Owner", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.05f);
            _owner.transform.localPosition = new Vector3(0.35f, 0, d * 0.55f);
            _owner.transform.localRotation = Quaternion.LookRotation(Vector3.right);
            _clerk = Prims.Figure(_root, "Clerk", new Color(0.3f, 0.6f, 0.35f), new Color(0.95f, 0.8f, 0.65f), 1.0f);
            _clerk.transform.localPosition = new Vector3(0.35f, 0, d * 0.55f - 0.8f);
            _clerk.transform.localRotation = Quaternion.LookRotation(Vector3.right);

            _doorX = doorX;
            _door = Origin + new Vector3(doorX, 0, -0.6f);
            _counterFront = Origin + new Vector3(1.8f, 0, d * 0.55f);
        }

        private Color ItemColor()
        {
            Color[] c = { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.85f), new Color(0.15f, 0.15f, 0.15f), new Color(0.9f, 0.9f, 0.92f), new Color(0.2f, 0.7f, 0.4f) };
            return c[_rng.Next(c.Length)];
        }

        public void SetActive(bool on)
        {
            _root.gameObject.SetActive(on);
            if (on && !_wasOn)
            {
                _owner.transform.localPosition = new Vector3(_doorX, 0, 0.9f);
                _owner.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
                if (_walkers.Count == 0)
                    for (int i = 0; i < 5; i++)
                    {
                        SpawnWalker();
                        Walker wk = _walkers[_walkers.Count - 1];
                        wk.Go.transform.position = Vector3.Lerp(wk.Go.transform.position, wk.Target, (float)_rng.NextDouble() * 0.9f);
                    }
            }
            _wasOn = on;
        }

        /// <summary>WASD：按世界方向走一步，会被柜台、装机台、展示桌挡住（沿墙滑动）。</summary>
        public void MoveOwner(Vector3 worldDir, float realDt)
        {
            Vector3 d = new Vector3(worldDir.x, 0, worldDir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            Transform tr = _owner.transform;
            Vector3 p = tr.localPosition;
            float step = 3.2f * realDt;
            Vector3 tx = p + new Vector3(d.x * step, 0, 0);
            if (CanStand(tx, p)) p = tx;
            Vector3 tz = p + new Vector3(0, 0, d.z * step);
            if (CanStand(tz, p)) p = tz;
            tr.localPosition = p;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(d), 0.35f);
        }

        private bool CanStand(Vector3 to, Vector3 from)
        {
            bool doorway = Mathf.Abs(to.x - _doorX) < 0.5f && to.z >= -0.7f && to.z < 0.4f;
            if (to.x < 0.3f || to.x > Width - 0.9f || (to.z < 0.4f && !doorway) || to.z > Depth - 1.2f) return false;
            if (doorway) return true;
            if (!Blocked(to)) return true;
            return Blocked(from); // 已经卡在里面时允许走出来
        }

        private bool Blocked(Vector3 p)
        {
            foreach (Rect r in _blockers)
                if (r.Contains(new Vector2(p.x, p.z))) return true;
            foreach (Rect r in _decor.Blockers)
                if (r.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        /// <summary>老板走到门口（够得着门，按 F 或者接着往外走就出门）。</summary>
        public bool NearDoor
        {
            get
            {
                Vector3 p = _owner.transform.localPosition;
                return Mathf.Abs(p.x - _doorX) < 0.9f && p.z < 1.1f;
            }
        }

        public Vector3 DoorWorld => Origin + new Vector3(_doorX, 2.0f, 0f);

        /// <summary>老板已经走出门槛（该回到街上了）。</summary>
        public bool OwnerOutside => _owner.transform.localPosition.z < -0.55f;

        /// <summary>出不去（比如有事走不开）时，把老板退回门里。</summary>
        public void StepBackInside()
        {
            _owner.transform.localPosition = new Vector3(_doorX, 0, 0.6f);
            _owner.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
        }

        /// <summary>镜头看全景时的中心：店里加上门外一截人行道。</summary>
        public Vector3 ViewCenter => Origin + new Vector3(Width / 2, 0, (Depth - 3.5f) / 2);

        /// <summary>冒烟 / 调试：直接站到柜台后面。</summary>
        public void StandAtCounter()
        {
            _owner.transform.localPosition = new Vector3(0.35f, 0, CounterZ);
            _owner.transform.localRotation = Quaternion.LookRotation(Vector3.right);
        }

        private string _dressed;

        public List<PartCategory> ShelfCategories => _shelves.ConvertAll(s => s.Category);

        /// <summary>冒烟用：顾客在没到目标时原地打转的最长时间（游戏帧时间，秒）。</summary>
        public float MaxStuckSeconds { get; set; }

        /// <summary>冒烟 / 调试：直接站到第 i 个货架前面。</summary>
        public void StandAtShelf(int i)
        {
            if (i < 0 || i >= _shelfSpots.Count) return;
            _owner.transform.localPosition = _shelfSpots[i] - Origin;
        }

        /// <summary>冒烟 / 调试：直接站到店门里面。</summary>
        public void StandAtDoor() => StepBackInside();

        /// <summary>冒烟 / 调试：直接站到整机展示台前。</summary>
        public void StandAtDisplay()
        {
            if (_tables.Count == 0) return;
            _owner.transform.localPosition = new Vector3(_tables[0].x, 0, _tables[0].y - 1.0f);
        }

        /// <summary>走到货架跟前时，能查看的那个货架的品类；没靠近则为空。</summary>
        public PartCategory? NearbyShelf { get; private set; }

        /// <summary>靠近的货架头顶的世界坐标（给“按 F”提示用）。</summary>
        public Vector3 NearbyShelfWorld { get; private set; }

        /// <summary>站在整机展示台旁边（能按 F 看展示的整机和装机单）。</summary>
        public bool NearDisplay { get; private set; }

        public Vector3 NearDisplayWorld { get; private set; }

        /// <summary>站在装机台旁边（能按 F 开装机单和装机小游戏）。</summary>
        public bool NearBench { get; private set; }

        public Vector3 NearBenchWorld { get; private set; }

        /// <summary>冒烟 / 调试：直接站到装机台正前方。</summary>
        public void StandAtBench()
        {
            _owner.transform.localPosition = new Vector3(1.4f, 0, Depth - 0.9f);
        }

        private void UpdateNearbyShelf(bool active)
        {
            NearbyShelf = null;
            NearCounter = false;
            NearDisplay = false;
            NearBench = false;
            if (!active) return;
            Vector3 op = _owner.transform.localPosition;
            NearCounter = op.x < 2.3f && Mathf.Abs(op.z - CounterZ) < CounterLength / 2 + 0.3f;
            Vector3 lp = _owner.transform.localPosition;
            float best = 1.5f;
            foreach (ShelfDisplay sh in _shelves)
            {
                float dd = new Vector2(lp.x - sh.Pos.x, lp.z - sh.Pos.z).magnitude;
                if (dd >= best) continue;
                best = dd;
                NearbyShelf = sh.Category;
                NearbyShelfWorld = Origin + sh.Pos + new Vector3(0, 2.3f, 0);
            }
            // 展示台：长桌 2.8×1.2，离桌边 1.0 米以内
            float bestTable = 1.0f;
            foreach (Vector2 tb in _tables)
            {
                float dx = Mathf.Max(Mathf.Abs(lp.x - tb.x) - 1.4f, 0f);
                float dz = Mathf.Max(Mathf.Abs(lp.z - tb.y) - 0.6f, 0f);
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd >= bestTable) continue;
                bestTable = dd;
                NearDisplay = true;
                NearDisplayWorld = Origin + new Vector3(tb.x, 1.9f, tb.y);
            }
            // 装机台：靠左墙后部，离台边 1.0 米以内；和展示桌都够得着时取近的
            {
                float bdx = Mathf.Max(0f - lp.x, lp.x - 0.9f, 0f);
                float bdz = Mathf.Max((Depth - 1.7f) - lp.z, lp.z - (Depth - 0.1f), 0f);
                float bd = Mathf.Sqrt(bdx * bdx + bdz * bdz);
                if (bd < 1.0f && (!NearDisplay || bd <= bestTable))
                {
                    NearBench = true;
                    NearDisplay = false;
                    NearBenchWorld = Origin + new Vector3(0.45f, 2.0f, Depth - 0.9f);
                }
            }
        }

        public void Update(GameSession s, float dt, float realDt)
        {
            if (_leaveReasons.Count > 200) _leaveReasons.Clear();
            if (_payChannels.Count > 200) _payChannels.Clear();
            _now += realDt;
            _emotes.Clear();
            if (_dressed != s.EquippedOutfit)
            {
                _dressed = s.EquippedOutfit;
                Prims.Dress(_owner, _dressed);
            }
            UpdateStreet(realDt, s.Clock.MinuteOfDay);
            ShopSimulation shop = s.PlayerShop;
            _decor.Sync(s.ShopLayout, s.ShopRoom);
            _owner.SetActive(s.InPlayerShop);
            UpdateNearbyShelf(s.InPlayerShop);
            bool building = shop.Orders.Count > 0;
            foreach (GameObject g in _buildGlow) g.SetActive(building);
            _clerk.SetActive(s.Employee != null && s.IsEmployeeOnDuty(s.Clock.Now));

            _stockTimer -= dt;
            if (_stockTimer <= 0)
            {
                _stockTimer = 0.5f;
                var counts = new Dictionary<(PartCategory, bool), int>();
                void Bump((PartCategory, bool) k, int d) => counts[k] = Mathf.Max(0, (counts.TryGetValue(k, out int n0) ? n0 : 0) + d);
                foreach (var line in shop.Stock.Lines())
                    Bump((line.Part.Category, line.Boxed), line.Count);
                // 顾客还在走去拿货的路上：货架上还摆着；正在走去放回去的：货架上还没有
                var pcs = new List<int>(shop.DisplayPcs);
                foreach (Visitor pv in _visitors.Values)
                    if (pv.Phase == 1)
                    {
                        if (pv.DisplayPc) pcs.Add(pv.PcTier);
                        else Bump((pv.ItemCat, pv.ItemBoxed), 1);
                    }
                foreach (Visitor pv in _leaving)
                    if (pv.DropFirst && !pv.Dropped)
                    {
                        if (pv.DisplayPc) pcs.Remove(pv.PcTier);
                        else Bump((pv.ItemCat, pv.ItemBoxed), -1);
                    }
                pcs.Sort();
                for (int i = 0; i < _pcSlots.Count; i++)
                {
                    bool on = i < pcs.Count;
                    _pcSlots[i].Root.SetActive(on);
                    if (on) ShopFixtures.SetSlotTier(_pcSlots[i], pcs[i]);
                }
                foreach (ShelfDisplay sh in _shelves)
                {
                    int boxedHave = counts.TryGetValue((sh.Category, true), out int bh) ? bh : 0;
                    int looseHave = counts.TryGetValue((sh.Category, false), out int lh) ? lh : 0;
                    // 整盒摆在最上层，散件摆在下面两层；完全没货时留两件散件样品
                    int showBoxed = Mathf.Min(sh.BoxedItems.Count, boxedHave);
                    int showLoose = Mathf.Min(sh.LooseItems.Count, boxedHave + looseHave == 0 ? 2 : looseHave);
                    for (int i = 0; i < sh.BoxedItems.Count; i++) sh.BoxedItems[i].SetActive(i < showBoxed);
                    for (int i = 0; i < sh.LooseItems.Count; i++) sh.LooseItems[i].SetActive(i < showLoose);
                }
            }
            Hook(shop);
            var alive = new HashSet<int>();
            int queueIndex = 0;
            var queueIds = new Dictionary<int, int>();
            foreach (ShopCustomer c in shop.Queue) queueIds[c.Id] = queueIndex++;

            foreach (ShopCustomer c in shop.ActiveCustomers)
            {
                alive.Add(c.Id);
                if (!_visitors.TryGetValue(c.Id, out Visitor v))
                {
                    string profile = ProfileOf(c, out Color shirt);
                    float shade = 0.85f + (c.Id * 37 % 100) / 100f * 0.3f;
                    Color[] skins = { new Color(0.92f, 0.78f, 0.62f), new Color(0.85f, 0.66f, 0.5f), new Color(0.98f, 0.84f, 0.72f) };
                    v = new Visitor
                    {
                        Go = Prims.Figure(_root, "Customer" + c.Id, new Color(shirt.r * shade, shirt.g * shade, shirt.b * shade), skins[c.Id % skins.Length], 0.9f + (c.Id * 53 % 100) / 100f * 0.15f),
                        Profile = profile,
                    };
                    v.Go.transform.position = StreetSpawn();
                    RouteIn(v);
                    var col = v.Go.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0, 0.8f, 0);
                    col.height = 1.8f;
                    col.radius = 0.4f;
                    v.Go.AddComponent<CustomerMarker>().CustomerId = c.Id;
                    v.Bubble = Prims.Shape(PrimitiveType.Sphere, v.Go.transform, "Bubble", new Vector3(0, 1.85f, 0), Vector3.one * 0.28f,
                        Mats.Glow(new Color(1f, 0.8f, 0.1f), new Color(3f, 2.2f, 0.2f), false));
                    v.Build = c.Intent == CustomerIntent.Build;
                    v.Want = c.DesiredCategory;
                    v.Target = _shelfSpots.Count == 0 ? Center
                        : v.Build ? Jitter(RandomDeskSpot())
                        : Jitter(ShelfSpotFor(c.DesiredCategory, _door, _rng));
                    v.BrowseTimer = 0.5f + (float)_rng.NextDouble() * 1.5f;
                    _visitors[c.Id] = v;
                }

                if (c.Spend.IsPositive) v.Spend = c.Spend.ToString();
                bool waiting = queueIds.TryGetValue(c.Id, out int qi);
                v.Bubble.SetActive(waiting && v.Phase == 2);
                if (waiting && (c.Item != null || c.DisplayTier >= 0))
                {
                    if (v.Phase == 0)
                    {
                        // 挑中了：先去货架 / 展示台前把货拿下来
                        v.DisplayPc = c.DisplayTier >= 0;
                        if (v.DisplayPc)
                        {
                            v.PcTier = c.DisplayTier;
                            v.PickSpot = NearestDeskSpot(v.Go.transform.position);
                        }
                        else
                        {
                            v.ItemCat = c.Item.Category;
                            v.ItemBoxed = c.ItemBoxed;
                            v.PickSpot = Jitter(ShelfSpotFor(v.ItemCat, v.Go.transform.position));
                        }
                        v.Target = v.PickSpot;
                        v.Phase = 1;
                    }
                    if (v.Phase == 1 && Reached(v))
                    {
                        ShowHeld(v);
                        v.Phase = 2;
                    }
                    if (v.Phase == 2) v.Target = QueueSpot(qi);
                }
                else if (v.Phase == 0 && Reached(v) && _shelfSpots.Count > 0)
                {
                    // 逛：站一会儿，再挪到别的货架前看看
                    v.BrowseTimer -= dt;
                    if (v.BrowseTimer <= 0)
                    {
                        v.BrowseTimer = 1f + (float)_rng.NextDouble() * 2f;
                        v.Target = v.Build ? Jitter(RandomDeskSpot())
                            : _rng.NextDouble() < 0.6 ? Jitter(ShelfSpotFor(c.DesiredCategory, v.Go.transform.position, _rng))
                            : Jitter(_shelfSpots[_rng.Next(_shelves.Count > 0 ? _shelves.Count : _shelfSpots.Count)]);
                    }
                }
                Move(v, dt);
                if (Reached(v) || Flat(v.Go.transform.position - v.StuckAnchor).sqrMagnitude > 0.36f)
                {
                    v.StuckAnchor = v.Go.transform.position;
                    v.StuckFor = 0;
                }
                else v.StuckFor += dt;
                MaxStuckSeconds = Mathf.Max(MaxStuckSeconds, v.StuckFor);

                // 头顶的对话气泡：逛的时候说想买什么、店里有没有货；挑中了说要哪件、多少钱
                string say = Say(c, v, shop, s.Clock.Now);
                if (say != null)
                    _emotes.Add(new EmoteInfo
                    {
                        Pos = v.Go.transform.position + Vector3.up * 2.05f,
                        Text = say,
                        Color = new Color(0.12f, 0.13f, 0.16f),
                        Size = 18,
                        Bubble = true,
                    });
            }

            var gone = new List<int>();
            foreach (var kv in _visitors)
            {
                if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            }
            foreach (int id in gone)
            {
                Visitor v = _visitors[id];
                _visitors.Remove(id);
                v.Leaving = true;
                v.Bubble.SetActive(false);
                Kill.It(v.Go.GetComponent<Collider>());
                // 付了钱的拿着货走；等烦了 / 关门 / 嫌贵的，先把货放回货架再走
                bool paid = !_leaveReasons.ContainsKey(id);
                if (paid && v.Spend != null)
                    _pops.Add(new Pop { Start = _counterFront + new Vector3(-0.4f, 1.8f, 0), Text = "+" + v.Spend + (_payChannels.TryGetValue(id, out PayChannel pc) ? " " + PayText(pc) : ""), T0 = _now });
                else if (_leaveReasons.TryGetValue(id, out LeaveReason lr))
                {
                    v.Emote = LeaveText(lr, out v.EmoteColor);
                    v.EmoteUntil = _now + 2.6f;
                }
                v.DropFirst = !paid && v.Held != null;
                if (v.DropFirst)
                {
                    v.Via.Clear();
                    v.Target = v.PickSpot;
                }
                else SendOut(v);
                _leaveReasons.Remove(id);
                _leaving.Add(v);
            }
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                Visitor v = _leaving[i];
                Move(v, dt);
                if (v.DropFirst && !v.Dropped && Reached(v))
                {
                    v.Dropped = true;
                    if (v.Held != null) Kill.It(v.Held);
                    v.Held = null;
                    SendOut(v);
                    continue;
                }
                if (!v.DropFirst || v.Dropped)
                {
                    if (v.Via.Count == 0 && (v.Go.transform.position - v.Target).sqrMagnitude < 0.05f)
                    {
                        Kill.It(v.Go);
                        _leaving.RemoveAt(i);
                        continue;
                    }
                }
                if (v.Emote != null && _now < v.EmoteUntil)
                    _emotes.Add(new EmoteInfo { Pos = v.Go.transform.position + Vector3.up * 2.3f, Text = v.Emote, Color = v.EmoteColor, Size = 22 });
            }

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                float age = _now - _pops[i].T0;
                if (age > 1.8f) { _pops.RemoveAt(i); continue; }
                _emotes.Add(new EmoteInfo { Pos = _pops[i].Start + Vector3.up * (age * 0.7f), Text = _pops[i].Text, Color = new Color(0.55f, 1f, 0.6f, 1f - age / 2.2f), Size = 28 });
            }
        }

        private const string Ok = "#2E9E4F";
        private const string Bad = "#D2453A";

        /// <summary>顾客气泡里说的话；排队等结账时头顶已有结账按钮，不再说话。</summary>
        private static string Say(ShopCustomer c, Visitor v, ShopSimulation shop, GameTimestamp now)
        {
            if (v.Phase == 2) return null;
            if (v.Phase == 1 && c.DisplayTier >= 0)
                return $"展示台这台我要了！\n<b>{c.ItemName}</b>\n<color={Ok}>{c.Spend}</color>";
            if (v.Phase == 1 && c.Item != null)
                return $"就要这个！\n<b>{c.Item.Name}</b>（{(c.ItemBoxed ? "盒装" : "散件")}）\n<color={Ok}>{shop.PriceOf(c.Item, c.ItemBoxed)}</color>";
            string browsing = BrowseLine(c, now);
            if (v.Build)
            {
                string tierName = PartCatalog.TierName(c.BuildTier);
                if (shop.DisplayCount(c.BuildTier) > 0)
                    return $"<size=13>{v.Profile}</size>\n想买一台<b>{tierName}</b>整机\n<color={Ok}>✓ 展示台上就有，看看就带走</color>\n{browsing}";
                int missing = 0;
                foreach (PartDef p in PartCatalog.BuildList(c.BuildTier))
                    if (shop.Stock.CountAll(p) == 0) missing++;
                string parts = missing == 0 ? $"<color={Ok}>✓ 配件都有，可以下单装</color>" : $"<color={Bad}>✗ 展台没有，店里还缺 {missing} 样配件</color>";
                return $"<size=13>{v.Profile}</size>\n想装一台<b>{tierName}</b>整机\n{parts}\n{browsing}";
            }
            int have = 0;
            foreach (var line in shop.Stock.Lines())
                if (line.Part.Category == c.DesiredCategory) have += line.Count;
            string stock = have > 0 ? $"<color={Ok}>✓ 有货（{have} 件）</color>" : $"<color={Bad}>✗ 没货……</color>";
            return $"<size=13>{v.Profile}</size>\n想买<b>{PartCatalog.CategoryName(c.DesiredCategory)}</b>\n{stock}\n{browsing}";
        }

        /// <summary>还在逛：挑款式、比价格，进度条走满才决定买不买（嫌贵就走）。</summary>
        private static string BrowseLine(ShopCustomer c, GameTimestamp now)
        {
            long total = Mathf.Max(1, (int)c.ArrivedAt.MinutesUntil(c.ReadyAt));
            long done = Mathf.Clamp((int)c.ArrivedAt.MinutesUntil(now), 0, (int)total);
            int pct = Mathf.Clamp(Mathf.RoundToInt(done * 100f / total), 0, 100);
            return $"<size=13><color=#6B6F78>正在挑、比价格… {pct}%</color></size>";
        }

        /// <summary>排队的第 k 个位置：从柜台前往前墙方向排，排到离前墙 1.9 米处再往右拐。</summary>
        private Vector3 QueueSpot(int k)
        {
            float run = k * 0.75f;
            float straight = Mathf.Max(0f, _counterFront.z - Origin.z - 1.9f);
            if (run <= straight) return _counterFront + new Vector3(0, 0, -run);
            return new Vector3(_counterFront.x + (run - straight), _counterFront.y, Origin.z + 1.9f);
        }

        private void Move(Visitor v, float dt)
        {
            Vector3 p = v.Go.transform.position;
            Vector3 goal = v.Via.Count > 0 ? v.Via[0] : v.Target;
            if (float.IsNaN(v.PlannedFor.x) || (v.PlannedFor - goal).sqrMagnitude > 0.01f)
            {
                v.PlannedFor = goal;
                v.Detours.Clear();
                v.Detours.AddRange(PlanAround(p, goal));
            }
            while (v.Detours.Count > 0 && Flat(v.Detours[0] - p).sqrMagnitude < 0.0025f) v.Detours.RemoveAt(0);
            Vector3 next = v.Detours.Count > 0 ? v.Detours[0] : goal;
            Vector3 d = next - p;
            d.y = 0;
            float m = d.magnitude;
            if (m < 0.05f)
            {
                if (v.Detours.Count > 0) v.Detours.RemoveAt(0);
                else if (v.Via.Count > 0) v.Via.RemoveAt(0);
                return;
            }
            v.Go.transform.position = p + d / m * Mathf.Min(m, 3.0f * dt);
            v.Go.transform.rotation = Quaternion.LookRotation(d / m);
        }

        private const float BodyRadius = 0.35f;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        /// <summary>
        /// 从 p 走到 goal 要绕开展示桌时的拐点（桌角外面一点）：在起点、终点和所有桌角之间找最短的不穿桌路线。
        /// 起点或终点本身就贴着某张桌子时，那张桌子不算障碍（免得原地打转）。
        /// </summary>
        private List<Vector3> PlanAround(Vector3 p, Vector3 goal)
        {
            var result = new List<Vector3>();
            var a = new Vector2(p.x, p.z);
            var b = new Vector2(goal.x, goal.z);
            var hits = new List<Rect>();
            foreach (Rect zone in _walkAround)
            {
                Rect hit = Grow(zone, BodyRadius);
                if (!hit.Contains(a) && !hit.Contains(b)) hits.Add(hit);
            }
            bool Clear(Vector2 u, Vector2 w)
            {
                foreach (Rect h in hits)
                    if (SegmentHits(u, w, h)) return false;
                return true;
            }
            if (hits.Count == 0 || Clear(a, b)) return result;

            var nodes = new List<Vector2> { a, b };
            foreach (Rect zone in _walkAround)
            {
                Rect c = Grow(zone, BodyRadius + 0.25f);
                foreach (Vector2 corner in new[] { new Vector2(c.xMin, c.yMin), new Vector2(c.xMax, c.yMin), new Vector2(c.xMin, c.yMax), new Vector2(c.xMax, c.yMax) })
                {
                    bool inside = false;
                    foreach (Rect h in hits) inside |= h.Contains(corner);
                    if (!inside) nodes.Add(corner);
                }
            }
            int n = nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[0] = 0;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == 1) break;
                done[u] = true;
                for (int k = 0; k < n; k++)
                {
                    if (done[k] || k == u) continue;
                    float alt = dist[u] + (nodes[k] - nodes[u]).magnitude;
                    if (alt < dist[k] && Clear(nodes[u], nodes[k])) { dist[k] = alt; prev[k] = u; }
                }
            }
            if (prev[1] < 0) return result;
            for (int k = prev[1]; k > 0; k = prev[k]) result.Insert(0, new Vector3(nodes[k].x, p.y, nodes[k].y));
            return result;
        }

        private static Rect Grow(Rect r, float k) => Rect.MinMaxRect(r.xMin - k, r.yMin - k, r.xMax + k, r.yMax + k);

        /// <summary>线段 a→b 是否穿过矩形（Liang–Barsky 裁剪）。</summary>
        private static bool SegmentHits(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            float[] pp = { -d.x, d.x, -d.y, d.y };
            float[] qq = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(pp[i]) < 1e-6f)
                {
                    if (qq[i] < 0) return false;
                    continue;
                }
                float t = qq[i] / pp[i];
                if (pp[i] < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return t1 - t0 > 1e-4f;
        }

        /// <summary>顾客从街上一头走过来，到门口拐进店。</summary>
        private Vector3 StreetSpawn()
        {
            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            float x = _doorX + side * (4f + (float)_rng.NextDouble() * 4f);
            return Origin + new Vector3(x, 0, SidewalkZ(_rng));
        }

        private void RouteIn(Visitor v)
        {
            v.Via.Clear();
            v.Via.Add(Origin + new Vector3(_doorX + ((float)_rng.NextDouble() - 0.5f) * 0.4f, 0, -1.0f));
            v.Via.Add(Origin + new Vector3(_doorX, 0, 0.7f));
        }

        /// <summary>出门，拐上人行道，往街的一头走掉。</summary>
        private void SendOut(Visitor v)
        {
            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            v.Via.Clear();
            v.Via.Add(Origin + new Vector3(_doorX, 0, 0.6f));
            float z = SidewalkZ(_rng);
            v.Via.Add(Origin + new Vector3(_doorX + side * 0.3f, 0, z));
            v.Target = Origin + new Vector3(side < 0 ? -StreetExtent + 1f : Width + StreetExtent - 1f, 0, z);
        }

        private Color RandomColor()
        {
            Color[] c = { new Color(0.8f, 0.2f, 0.25f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.25f, 0.25f, 0.25f), new Color(0.6f, 0.3f, 0.7f), new Color(0.3f, 0.7f, 0.4f) };
            return c[_rng.Next(c.Length)];
        }

        /// <summary>整个店内场景销毁（店面扩建后换一个新尺寸的）。</summary>
        public void Destroy()
        {
            Hook(null);
            Clear();
            Kill.It(_root.gameObject);
        }

        public void Clear()
        {
            foreach (var v in _visitors.Values) Kill.It(v.Go);
            foreach (var v in _leaving) Kill.It(v.Go);
            _visitors.Clear();
            _leaving.Clear();
        }
    }
}
