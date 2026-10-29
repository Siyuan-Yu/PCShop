using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在店内顾客身上，点击排队的顾客即结账。</summary>
    public sealed class CustomerMarker : MonoBehaviour
    {
        public int CustomerId;
    }

    /// <summary>店内地板：点击地板让老板走过去。</summary>
    public sealed class InteriorFloorMarker : MonoBehaviour
    {
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
            public GameObject Held;
            public float BrowseTimer;
            public bool Build;

            /// <summary>离开时的路线；dropFirst 表示先走到货架前把货放回去。</summary>
            public bool DropFirst;
            public bool Dropped;

            public PartCategory Want;
            public string Spend;
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
        private readonly List<ShelfDisplay> _shelves = new List<ShelfDisplay>();
        private readonly System.Random _rng = new System.Random(5);
        private readonly GameObject _owner;
        private readonly GameObject _clerk;
        private readonly Vector3 _door;
        private readonly Vector3 _counterFront;
        private float _stockTimer;
        private ShopSimulation _hooked;
        private readonly Dictionary<int, LeaveReason> _leaveReasons = new Dictionary<int, LeaveReason>();

        private void OnCustomerLeft(ShopCustomer c) => _leaveReasons[c.Id] = c.Reason;

        private void Hook(ShopSimulation shop)
        {
            if (_hooked == shop) return;
            if (_hooked != null) _hooked.CustomerLeft -= OnCustomerLeft;
            _hooked = shop;
            _leaveReasons.Clear();
            if (shop != null) shop.CustomerLeft += OnCustomerLeft;
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

        private void ShowHeld(Visitor v)
        {
            if (v.Held != null) return;
            v.Held = Prims.Box(v.Go.transform, "Held", new Vector3(0, 0.95f, 0.38f), new Vector3(0.3f, 0.2f, 0.36f), Mats.Lit(CategoryColor(v.ItemCat), 0.5f));
        }

        public ShopSize Size { get; }
        public float Width { get; }
        public float Depth { get; }
        public Vector3 Center => Origin + new Vector3(Width / 2, 0, Depth / 2);
        /// <summary>刚好看到整间店的镜头大小（缩放上限）。</summary>
        public float CameraSize => Mathf.Max(Width, Depth) * 0.62f + 1.5f;

        /// <summary>进店时默认的镜头大小：比较近，跟着老板走。</summary>
        public const float DefaultZoom = 5.5f;
        public const float MinZoom = 3.2f;

        public Vector3 OwnerWorld => _owner.transform.position;
        private float CounterZ => Depth * 0.55f;
        private float CounterLength => Mathf.Min(2.2f, Depth * 0.5f);
        private readonly List<Vector3> _path = new List<Vector3>();
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

        public ShopInteriorView(Transform parent, ShopSize size)
        {
            Size = size;
            _root = new GameObject("ShopInterior").transform;
            _root.SetParent(parent, false);
            _root.position = Origin;
            _decor = new RoomFurniture(_root);

            (Width, Depth) = size == ShopSize.Large ? (18f, 12f) : size == ShopSize.Medium ? (12f, 9f) : (8f, 6f);
            float w = Width, d = Depth;

            GameObject floor = Prims.Box(_root, "Floor", new Vector3(w / 2, -0.05f, d / 2), new Vector3(w, 0.1f, d), Mats.Lit(new Color(0.78f, 0.76f, 0.72f), 0.4f), true);
            floor.AddComponent<InteriorFloorMarker>();
            Material tileMat = Mats.Lit(new Color(0.7f, 0.69f, 0.66f), 0.4f);
            for (int tx = 0; tx * 2f < w; tx++)
            for (int tz = 0; tz * 2f < d; tz++)
            {
                if ((tx + tz) % 2 != 0) continue;
                float sx = Mathf.Min(2f, w - tx * 2f), sz = Mathf.Min(2f, d - tz * 2f);
                Prims.Box(_root, "Tile", new Vector3(tx * 2f + sx / 2, 0.005f, tz * 2f + sz / 2), new Vector3(sx, 0.012f, sz), tileMat);
            }
            Prims.Box(_root, "Outside", new Vector3(w / 2, -0.1f, d / 2), new Vector3(w + 30, 0.05f, d + 30), Mats.Lit(new Color(0.12f, 0.12f, 0.14f)));
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

            // 布局：整机展示桌靠后墙（顾客逛完不挡路），货架沿后墙剩下的位置、右墙和左墙前部；
            // 柜台前到门口这一片留空，给顾客排队结账。
            int deskCount = size == ShopSize.Small ? 1 : size == ShopSize.Medium ? 2 : 4;
            var tables = new List<Vector2>();
            for (int i = 0; i < deskCount; i++) tables.Add(new Vector2(2.5f + i * 3.0f, d - 0.55f));

            // 货架：每个货架摆一类配件（显卡、CPU、内存、硬盘、机箱电源、主板轮流）
            PartCategory[] cats = { PartCategory.Gpu, PartCategory.Cpu, PartCategory.Memory, PartCategory.Storage, PartCategory.PowerCase, PartCategory.Motherboard };
            int shelfNo = 0;
            for (float x = tables[tables.Count - 1].x + 2.5f; x <= w - 0.9f; x += 1.6f)
            {
                _shelves.Add(ShopFixtures.BuildShelf(_root, new Vector3(x, 0, d - 0.2f), 0f, cats[shelfNo++ % cats.Length]));
                _shelfSpots.Add(Origin + new Vector3(x, 0, d - 1.2f));
            }
            int sideShelves = Mathf.Max(0, Mathf.FloorToInt((d - 2.25f) / 1.6f) + 1);
            for (int i = 0; i < sideShelves; i++)
            {
                float z = 1.0f + i * 1.6f;
                _shelves.Add(ShopFixtures.BuildShelf(_root, new Vector3(w - 0.2f, 0, z), 90f, cats[shelfNo++ % cats.Length]));
                _shelfSpots.Add(Origin + new Vector3(w - 1.2f, 0, z));
            }
            // 左墙前部（柜台前面）再摆一个，离排队的路线远
            _shelves.Add(ShopFixtures.BuildShelf(_root, new Vector3(0.2f, 0, 1.0f), 270f, cats[shelfNo++ % cats.Length]));
            _shelfSpots.Add(Origin + new Vector3(1.2f, 0, 1.0f));
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

            // 整机展示区：靠后墙的长桌，上面是开着机的整机
            int deskNo = 0;
            _tables.Clear();
            foreach (Vector2 tb in tables)
            {
                _tables.Add(tb);
                ShopFixtures.BuildPcDesk(_root, new Vector3(tb.x, 0, tb.y), deskNo++);
                _shelfSpots.Add(Origin + new Vector3(tb.x, 0, tb.y - 1.0f));
            }
            _blockers.Add(new Rect(0.5f, CounterZ - CounterLength / 2 - 0.15f, 0.95f, CounterLength + 0.3f));
            _blockers.Add(new Rect(0f, d - 1.7f, 0.9f, 1.6f));
            foreach (Vector2 tb in tables) _blockers.Add(new Rect(tb.x - 1.4f, tb.y - 0.6f, 2.8f, 1.2f));

            _owner = Prims.Figure(_root, "Owner", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.05f);
            _owner.transform.localPosition = new Vector3(0.35f, 0, d * 0.55f);
            _owner.transform.localRotation = Quaternion.LookRotation(Vector3.right);
            _clerk = Prims.Figure(_root, "Clerk", new Color(0.3f, 0.6f, 0.35f), new Color(0.95f, 0.8f, 0.65f), 1.0f);
            _clerk.transform.localPosition = new Vector3(0.35f, 0, d * 0.55f - 0.8f);
            _clerk.transform.localRotation = Quaternion.LookRotation(Vector3.right);

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
                _path.Clear();
                _owner.transform.localPosition = new Vector3(0.35f, 0, CounterZ);
                _owner.transform.localRotation = Quaternion.LookRotation(Vector3.right);
            }
            _wasOn = on;
        }

        /// <summary>点击地板：老板走过去（会绕开柜台）。</summary>
        public void WalkTo(Vector3 world)
        {
            Vector3 t = world - Origin;
            t.y = 0;
            t.x = Mathf.Clamp(t.x, 0.35f, Width - 0.9f);
            t.z = Mathf.Clamp(t.z, 0.4f, Depth - 1.2f);
            bool toBehind = InCounterRange(t.z) && t.x < 1.5f;
            if (toBehind) t.x = 0.35f;
            Vector3 from = _owner.transform.localPosition;
            bool fromBehind = InCounterRange(from.z) && from.x < 0.7f;
            _path.Clear();
            if (fromBehind != toBehind)
            {
                float zLow = Mathf.Max(0.4f, CounterZ - CounterLength / 2 - 0.5f);
                if (fromBehind) { _path.Add(new Vector3(0.35f, 0, zLow)); _path.Add(new Vector3(1.7f, 0, zLow)); }
                else { _path.Add(new Vector3(1.7f, 0, zLow)); _path.Add(new Vector3(0.35f, 0, zLow)); }
            }
            _path.Add(t);
        }

        /// <summary>WASD：按世界方向走一步，会被柜台、装机台、展示桌挡住（沿墙滑动）。</summary>
        public void MoveOwner(Vector3 worldDir, float realDt)
        {
            Vector3 d = new Vector3(worldDir.x, 0, worldDir.z);
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            _path.Clear();
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
            if (to.x < 0.3f || to.x > Width - 0.9f || to.z < 0.4f || to.z > Depth - 1.2f) return false;
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

        /// <summary>回到柜台后面。</summary>
        public void WalkToCounter() => WalkTo(Origin + new Vector3(0.35f, 0, CounterZ));

        private bool InCounterRange(float z) => z > CounterZ - CounterLength / 2 - 0.2f && z < CounterZ + CounterLength / 2 + 0.2f;

        private void MoveOwner(float realDt)
        {
            if (_path.Count == 0) return;
            Transform tr = _owner.transform;
            Vector3 p = tr.localPosition;
            Vector3 dv = _path[0] - p;
            float m = dv.magnitude;
            if (m < 0.05f)
            {
                _path.RemoveAt(0);
                if (_path.Count == 0 && p.x < 0.7f) tr.localRotation = Quaternion.LookRotation(Vector3.right);
                return;
            }
            tr.localPosition = p + dv / m * Mathf.Min(m, 3.2f * realDt);
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(dv / m), 0.35f);
        }

        private string _dressed;

        /// <summary>冒烟 / 调试：直接站到第 i 个货架前面。</summary>
        public void StandAtShelf(int i)
        {
            if (i < 0 || i >= _shelfSpots.Count) return;
            _path.Clear();
            _owner.transform.localPosition = _shelfSpots[i] - Origin;
        }

        /// <summary>冒烟 / 调试：直接站到整机展示台前。</summary>
        public void StandAtDisplay()
        {
            if (_tables.Count == 0) return;
            _path.Clear();
            _owner.transform.localPosition = new Vector3(_tables[0].x, 0, _tables[0].y - 1.0f);
        }

        /// <summary>走到货架跟前时，能查看的那个货架的品类；没靠近则为空。</summary>
        public PartCategory? NearbyShelf { get; private set; }

        /// <summary>靠近的货架头顶的世界坐标（给“按 F”提示用）。</summary>
        public Vector3 NearbyShelfWorld { get; private set; }

        /// <summary>站在整机展示台旁边（能按 F 看展示的整机和装机单）。</summary>
        public bool NearDisplay { get; private set; }

        public Vector3 NearDisplayWorld { get; private set; }

        private void UpdateNearbyShelf(bool active)
        {
            NearbyShelf = null;
            NearCounter = false;
            NearDisplay = false;
            if (!active) return;
            Vector3 op = _owner.transform.localPosition;
            NearCounter = op.x < 2.2f && Mathf.Abs(op.z - CounterZ) < CounterLength / 2 + 0.9f;
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
        }

        public void Update(GameSession s, float dt, float realDt)
        {
            if (_leaveReasons.Count > 200) _leaveReasons.Clear();
            _now += realDt;
            _emotes.Clear();
            if (_dressed != s.EquippedOutfit)
            {
                _dressed = s.EquippedOutfit;
                Prims.Dress(_owner, _dressed);
            }
            ShopSimulation shop = s.PlayerShop;
            _decor.Sync(s.ShopLayout, s.ShopRoom);
            _owner.SetActive(s.InPlayerShop);
            MoveOwner(realDt);
            UpdateNearbyShelf(s.InPlayerShop);
            bool building = shop.Orders.Count > 0;
            foreach (GameObject g in _buildGlow) g.SetActive(building);
            _clerk.SetActive(s.Employee != null && s.IsEmployeeOnDuty(s.Clock.Now));

            _stockTimer -= dt;
            if (_stockTimer <= 0)
            {
                _stockTimer = 0.5f;
                var counts = new Dictionary<PartCategory, int>();
                foreach (var line in shop.Stock.Lines())
                    counts[line.Part.Category] = (counts.TryGetValue(line.Part.Category, out int n) ? n : 0) + line.Count;
                // 顾客还在走去拿货的路上：货架上还摆着；正在走去放回去的：货架上还没有
                foreach (Visitor pv in _visitors.Values)
                    if (pv.Phase == 1) counts[pv.ItemCat] = (counts.TryGetValue(pv.ItemCat, out int a1) ? a1 : 0) + 1;
                foreach (Visitor pv in _leaving)
                    if (pv.DropFirst && !pv.Dropped) counts[pv.ItemCat] = Mathf.Max(0, (counts.TryGetValue(pv.ItemCat, out int a2) ? a2 : 0) - 1);
                foreach (ShelfDisplay sh in _shelves)
                {
                    // 空货架会留几件样品，有货时按库存多少摆满
                    int stock = counts.TryGetValue(sh.Category, out int have) ? have : 0;
                    int show = Mathf.Min(sh.Items.Count, stock > 0 ? 3 + stock : 2);
                    for (int i = 0; i < sh.Items.Count; i++) sh.Items[i].SetActive(i < show);
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
                    v = new Visitor { Go = Prims.Figure(_root, "Customer" + c.Id, RandomColor(), new Color(0.92f, 0.78f, 0.62f), 0.95f) };
                    v.Go.transform.position = _door;
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
                if (waiting && c.Item != null)
                {
                    if (v.Phase == 0)
                    {
                        // 挑中了：先去货架前把货拿下来
                        v.ItemCat = c.Item.Category;
                        v.PickSpot = Jitter(ShelfSpotFor(v.ItemCat, v.Go.transform.position));
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

                // 逛的时候头顶写着想买什么，缺货一眼就能看出来
                if (v.Phase == 0)
                    _emotes.Add(new EmoteInfo
                    {
                        Pos = v.Go.transform.position + Vector3.up * 2.15f,
                        Text = v.Build ? "想装整机" : "想买：" + PartCatalog.CategoryName(v.Want),
                        Color = new Color(0.85f, 0.9f, 1f, 0.9f),
                        Size = 17,
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
                    _pops.Add(new Pop { Start = _counterFront + new Vector3(-0.4f, 1.8f, 0), Text = "+" + v.Spend, T0 = _now });
                else if (_leaveReasons.TryGetValue(id, out LeaveReason lr))
                {
                    v.Emote = LeaveText(lr, out v.EmoteColor);
                    v.EmoteUntil = _now + 2.6f;
                }
                v.DropFirst = !paid && v.Held != null;
                v.Target = v.DropFirst ? v.PickSpot : _door + new Vector3(0, 0, -2.0f);
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
                    v.Target = _door + new Vector3(0, 0, -2.0f);
                    continue;
                }
                if (!v.DropFirst || v.Dropped)
                {
                    if ((v.Go.transform.position - v.Target).sqrMagnitude < 0.05f)
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

        /// <summary>排队的第 k 个位置：从柜台前往前墙方向排，排到离前墙 1.9 米处再往右拐。</summary>
        private Vector3 QueueSpot(int k)
        {
            float run = k * 0.75f;
            float straight = Mathf.Max(0f, _counterFront.z - Origin.z - 1.9f);
            if (run <= straight) return _counterFront + new Vector3(0, 0, -run);
            return new Vector3(_counterFront.x + (run - straight), _counterFront.y, Origin.z + 1.9f);
        }

        private static void Move(Visitor v, float dt)
        {
            Vector3 p = v.Go.transform.position;
            Vector3 d = v.Target - p;
            float m = d.magnitude;
            if (m < 0.02f) return;
            v.Go.transform.position = p + d / m * Mathf.Min(m, 3.0f * dt);
            v.Go.transform.rotation = Quaternion.LookRotation(d / m);
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
