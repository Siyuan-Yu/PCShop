using System.Collections.Generic;
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

    /// <summary>
    /// 玩家店铺的室内（按规格生成：小 12㎡ / 中 36㎡ / 大 96㎡）。放在远离城市的位置，进店时镜头切过来。
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
        private readonly List<GameObject> _shelfItems = new List<GameObject>();
        private readonly System.Random _rng = new System.Random(5);
        private readonly GameObject _owner;
        private readonly GameObject _clerk;
        private readonly Vector3 _door;
        private readonly Vector3 _counterFront;
        private readonly Vector3 _queueDir;
        private float _stockTimer;

        public float Width { get; }
        public float Depth { get; }
        public Vector3 Center => Origin + new Vector3(Width / 2, 0, Depth / 2);
        public float CameraSize => Mathf.Max(Width, Depth) * 0.62f + 1.5f;

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
            _root = new GameObject("ShopInterior").transform;
            _root.SetParent(parent, false);
            _root.position = Origin;

            (Width, Depth) = size == ShopSize.Large ? (12f, 8f) : size == ShopSize.Medium ? (6f, 6f) : (4f, 3f);
            float w = Width, d = Depth;

            Prims.Box(_root, "Floor", new Vector3(w / 2, -0.05f, d / 2), new Vector3(w, 0.1f, d), Mats.Lit(new Color(0.78f, 0.76f, 0.72f), 0.4f));
            Prims.Box(_root, "Outside", new Vector3(w / 2, -0.1f, d / 2), new Vector3(w + 30, 0.05f, d + 30), Mats.Lit(new Color(0.12f, 0.12f, 0.14f)));
            Prims.Box(_root, "WallBack", new Vector3(w / 2, 1.5f, d + 0.05f), new Vector3(w + 0.2f, 3f, 0.1f), Mats.Lit(new Color(0.86f, 0.88f, 0.9f)));
            Prims.Box(_root, "WallRight", new Vector3(w + 0.05f, 1.5f, d / 2), new Vector3(0.1f, 3f, d + 0.2f), Mats.Lit(new Color(0.8f, 0.83f, 0.86f)));
            Prims.Box(_root, "WallLeftLow", new Vector3(-0.05f, 0.3f, d / 2), new Vector3(0.1f, 0.6f, d), Mats.Lit(new Color(0.6f, 0.6f, 0.62f)));
            Prims.Box(_root, "WallFrontLow", new Vector3(w / 2 + 0.6f, 0.3f, -0.05f), new Vector3(w - 1.2f, 0.6f, 0.1f), Mats.Lit(new Color(0.6f, 0.6f, 0.62f)));
            Prims.Box(_root, "Poster", new Vector3(w * 0.3f, 2f, d - 0.01f), new Vector3(1.2f, 0.8f, 0.02f), Mats.Glow(new Color(0.2f, 0.6f, 0.9f), new Color(0.4f, 1.2f, 1.8f), false));

            var lampGo = new GameObject("CeilingLight");
            lampGo.transform.SetParent(_root, false);
            lampGo.transform.localPosition = new Vector3(w / 2, 2.8f, d / 2);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = Mathf.Max(w, d) * 1.5f;
            lamp.intensity = 2.2f;
            lamp.color = new Color(1f, 0.95f, 0.85f);

            // 货架：沿后墙和右墙
            Material shelfMat = Mats.Lit(new Color(0.45f, 0.35f, 0.25f));
            int backShelves = Mathf.Max(1, Mathf.FloorToInt((w - 1.5f) / 1.6f));
            for (int i = 0; i < backShelves; i++)
            {
                float x = 1.3f + i * 1.6f;
                Prims.Box(_root, "Shelf", new Vector3(x, 0.9f, d - 0.2f), new Vector3(1.4f, 1.8f, 0.3f), shelfMat);
                _shelfSpots.Add(Origin + new Vector3(x, 0, d - 1.2f));
                for (int k = 0; k < 6; k++)
                    _shelfItems.Add(Prims.Box(_root, "Item", new Vector3(x - 0.5f + (k % 3) * 0.5f, 0.45f + (k / 3) * 0.7f, d - 0.5f), new Vector3(0.38f, 0.38f, 0.3f), Mats.Lit(ItemColor())));
            }
            int sideShelves = Mathf.Max(0, Mathf.FloorToInt((d - 2f) / 1.6f));
            for (int i = 0; i < sideShelves; i++)
            {
                float z = 1.0f + i * 1.6f;
                Prims.Box(_root, "Shelf", new Vector3(w - 0.2f, 0.9f, z), new Vector3(0.3f, 1.8f, 1.4f), shelfMat);
                _shelfSpots.Add(Origin + new Vector3(w - 1.2f, 0, z));
                for (int k = 0; k < 6; k++)
                    _shelfItems.Add(Prims.Box(_root, "Item", new Vector3(w - 0.5f, 0.45f + (k / 3) * 0.7f, z - 0.5f + (k % 3) * 0.5f), new Vector3(0.3f, 0.38f, 0.38f), Mats.Lit(ItemColor())));
            }

            // 柜台 + 电脑（靠左前方）
            Vector3 counter = new Vector3(0.9f, 0.5f, d * 0.55f);
            Prims.Box(_root, "Counter", counter, new Vector3(0.8f, 1.0f, Mathf.Min(2.2f, d * 0.5f)), Mats.Lit(new Color(0.3f, 0.32f, 0.36f), 0.5f));
            Prims.Box(_root, "Monitor", counter + new Vector3(0, 0.75f, 0.2f), new Vector3(0.1f, 0.45f, 0.6f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 0.8f, 1.5f), false));
            Prims.Box(_root, "Register", counter + new Vector3(0, 0.6f, -0.5f), new Vector3(0.35f, 0.2f, 0.35f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)));

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

        public void SetActive(bool on) => _root.gameObject.SetActive(on);

        public void Update(GameSession s, float dt)
        {
            ShopSimulation shop = s.PlayerShop;
            _owner.SetActive(s.InPlayerShop);
            _clerk.SetActive(s.Employee != null && s.IsEmployeeOnDuty(s.Clock.Now));

            _stockTimer -= dt;
            if (_stockTimer <= 0)
            {
                _stockTimer = 0.5f;
                int items = 0;
                foreach (var line in shop.Stock.Lines()) items += line.Count;
                int show = Mathf.Min(_shelfItems.Count, items);
                for (int i = 0; i < _shelfItems.Count; i++) _shelfItems[i].SetActive(i < show);
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

        public void Clear()
        {
            foreach (var v in _visitors.Values) Kill.It(v.Go);
            foreach (var v in _leaving) Kill.It(v.Go);
            _visitors.Clear();
            _leaving.Clear();
        }
    }
}
