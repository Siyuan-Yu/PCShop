using System.Collections.Generic;
using PCShopSell.Core.Catalog;
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
        }

        private readonly Transform _root;
        private readonly Dictionary<int, Visitor> _visitors = new Dictionary<int, Visitor>();
        private readonly List<Visitor> _leaving = new List<Visitor>();
        private readonly List<Vector3> _shelfSpots = new List<Vector3>();
        private readonly List<ShelfDisplay> _shelves = new List<ShelfDisplay>();
        private readonly System.Random _rng = new System.Random(5);
        private readonly GameObject _owner;
        private readonly GameObject _clerk;
        private readonly Vector3 _door;
        private readonly Vector3 _counterFront;
        private readonly Vector3 _queueDir;
        private float _stockTimer;

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
            Prims.Box(_root, "WallFrontLow", new Vector3(w / 2 + 0.6f, 0.3f, -0.05f), new Vector3(w - 1.2f, 0.6f, 0.1f), Mats.Lit(new Color(0.6f, 0.6f, 0.62f)));
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

            // 货架：沿后墙和右墙，每个货架摆一类配件（显卡、CPU、内存、硬盘、机箱电源、主板轮流）
            PartCategory[] cats = { PartCategory.Gpu, PartCategory.Cpu, PartCategory.Memory, PartCategory.Storage, PartCategory.PowerCase, PartCategory.Motherboard };
            int shelfNo = 0;
            int backShelves = Mathf.Max(1, Mathf.FloorToInt((w - 2.8f) / 1.6f) + 1);
            for (int i = 0; i < backShelves; i++)
            {
                float x = 1.5f + i * 1.6f;
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

            // 整机展示区：中间的长桌，上面是开着机的整机；中、大店更多
            var tables = new List<Vector2>();
            if (size == ShopSize.Small) tables.Add(new Vector2(w * 0.58f, d * 0.42f));
            else if (size == ShopSize.Medium) { tables.Add(new Vector2(w * 0.4f, d * 0.35f)); tables.Add(new Vector2(w * 0.68f, d * 0.6f)); }
            else
            {
                tables.Add(new Vector2(w * 0.3f, d * 0.3f)); tables.Add(new Vector2(w * 0.55f, d * 0.3f));
                tables.Add(new Vector2(w * 0.4f, d * 0.65f)); tables.Add(new Vector2(w * 0.68f, d * 0.6f));
            }
            int deskNo = 0;
            foreach (Vector2 tb in tables)
            {
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

            _door = Origin + new Vector3(0.6f, 0, -0.6f);
            _counterFront = Origin + new Vector3(1.8f, 0, d * 0.55f);
            _queueDir = new Vector3(0.75f, 0, -0.6f).normalized;
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

        public void Update(GameSession s, float dt, float realDt)
        {
            ShopSimulation shop = s.PlayerShop;
            _owner.SetActive(s.InPlayerShop);
            MoveOwner(realDt);
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
                foreach (ShelfDisplay sh in _shelves)
                {
                    // 空货架会留几件样品，有货时按库存多少摆满
                    int stock = counts.TryGetValue(sh.Category, out int have) ? have : 0;
                    int show = Mathf.Min(sh.Items.Count, stock > 0 ? 3 + stock : 2);
                    for (int i = 0; i < sh.Items.Count; i++) sh.Items[i].SetActive(i < show);
                }
            }
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
                    v.Target = _shelfSpots.Count > 0 ? _shelfSpots[_rng.Next(_shelfSpots.Count)] + new Vector3((float)_rng.NextDouble() * 0.6f - 0.3f, 0, (float)_rng.NextDouble() * 0.4f - 0.2f) : Center;
                    _visitors[c.Id] = v;
                }
                bool waiting = queueIds.TryGetValue(c.Id, out int qi);
                v.Bubble.SetActive(waiting);
                if (waiting) v.Target = _counterFront + _queueDir * (qi * 0.75f);
                Move(v, dt);
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
                v.Target = _door + new Vector3(-1.5f, 0, -1.5f);
                v.Bubble.SetActive(false);
                Kill.It(v.Go.GetComponent<Collider>());
                _leaving.Add(v);
            }
            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                Visitor v = _leaving[i];
                Move(v, dt);
                if ((v.Go.transform.position - v.Target).sqrMagnitude < 0.05f)
                {
                    Kill.It(v.Go);
                    _leaving.RemoveAt(i);
                }
            }
        }

        private static void Move(Visitor v, float dt)
        {
            Vector3 p = v.Go.transform.position;
            Vector3 d = v.Target - p;
            float m = d.magnitude;
            if (m < 0.02f) return;
            v.Go.transform.position = p + d / m * Mathf.Min(m, 2.0f * dt);
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
