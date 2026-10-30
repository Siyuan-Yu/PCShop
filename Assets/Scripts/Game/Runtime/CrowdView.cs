using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>
    /// 街上的人：每个都是住在这一带的真实街坊（Residents），按作息上班、下班、出门逛；
    /// 上班的人走到店门口进去，回家的人走到街头（地铁站或城中村路口）离开。另有走进玩家店里的顾客（不能搭话）。
    /// </summary>
    public sealed class CrowdView
    {
        private sealed class Walker
        {
            public GameObject Go;
            public Vector3 Target;
            public float Speed;
            public bool VanishOnArrive;
            public ResidentDef Resident;
            public ResidentTrip Trip;
            public bool Leaving;
        }

        private const float MetroEndX = -97f, VillageEndX = 102f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Dictionary<string, ResidentTrip> _arrived = new Dictionary<string, ResidentTrip>();
        private readonly Transform _parent;
        private readonly System.Random _rng = new System.Random(11);
        private readonly float _minX, _maxX;
        private bool _synced;

        /// <summary>玩家店门口（搬店后改到新铺子）。</summary>
        public Vector3 ShopDoor { get; set; }

        /// <summary>正在和玩家说话的人（停下来面向玩家）；没有是 null。</summary>
        public string Talking { get; set; }

        public Vector3 TalkFace { get; set; }

        private static readonly Color[] Clothes =
        {
            new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.2f, 0.2f),
            new Color(0.3f, 0.7f, 0.4f), new Color(0.85f, 0.65f, 0.2f), new Color(0.6f, 0.3f, 0.7f),
        };

        public CrowdView(Transform parent, float minX, float maxX, Vector3 shopDoor)
        {
            _parent = parent;
            _minX = minX;
            _maxX = maxX;
            ShopDoor = shopDoor;
        }

        /// <summary>离 pos 最近、radius 以内能搭话的人（不算正要进店的顾客）。</summary>
        public bool Nearest(Vector3 pos, float radius, out ResidentDef who, out Vector3 world)
        {
            who = null;
            world = Vector3.zero;
            float best = radius;
            foreach (Walker w in _walkers)
            {
                if (w.Resident == null || !w.Go.activeSelf) continue;
                Vector3 p = w.Go.transform.position;
                float d = new Vector2(p.x - pos.x, p.z - pos.z).magnitude;
                if (d >= best) continue;
                best = d;
                who = w.Resident;
                world = p;
            }
            return who != null;
        }

        /// <summary>
        /// 按此刻谁在街上增减行人。doorOf 给出店门口的位置。
        /// 第一次同步（刚进游戏）时人直接散在半路上，之后新出门的人从门口或街头走出来。
        /// </summary>
        public void Sync(List<KeyValuePair<ResidentDef, ResidentTrip>> outside, Func<string, Vector3?> doorOf)
        {
            var want = new Dictionary<string, KeyValuePair<ResidentDef, ResidentTrip>>();
            foreach (var kv in outside) want[kv.Key.Id] = kv;

            foreach (string id in new List<string>(_arrived.Keys))
                if (!want.TryGetValue(id, out var kv) || kv.Value != _arrived[id]) _arrived.Remove(id);

            var present = new HashSet<string>();
            foreach (Walker w in _walkers)
            {
                if (w.Resident == null) continue;
                present.Add(w.Resident.Id);
                if (want.TryGetValue(w.Resident.Id, out var kv))
                {
                    if (kv.Value != w.Trip || w.Leaving) Retarget(w, kv.Value, doorOf);
                }
                else if (!w.Leaving)
                {
                    w.Leaving = true;
                    w.VanishOnArrive = true;
                    if (w.Trip == ResidentTrip.Stroll) w.Target = HomeOrShop(w.Resident, doorOf);
                }
            }

            foreach (var kv in want.Values)
            {
                if (present.Contains(kv.Key.Id) || _arrived.ContainsKey(kv.Key.Id)) continue;
                Walker w = Spawn(kv.Key, Vector3.zero);
                Retarget(w, kv.Value, doorOf);
                Vector3 start = StartOf(w, doorOf);
                w.Go.transform.position = _synced ? start : Vector3.Lerp(start, w.Target, (float)_rng.NextDouble() * 0.8f);
                _walkers.Add(w);
            }
            _synced = true;
        }

        private void Retarget(Walker w, ResidentTrip trip, Func<string, Vector3?> doorOf)
        {
            w.Trip = trip;
            w.Leaving = false;
            switch (trip)
            {
                case ResidentTrip.ToWork:
                    w.Target = doorOf(w.Resident.WorkPlaceId) ?? EndOf(w.Resident);
                    w.VanishOnArrive = true;
                    break;
                case ResidentTrip.ToHome:
                    w.Target = EndOf(w.Resident);
                    w.VanishOnArrive = true;
                    break;
                default:
                    w.Target = RandomSidewalk();
                    w.VanishOnArrive = false;
                    break;
            }
        }

        /// <summary>下班回家、上班间隙出来逛的人从自己店门口走出来；其余从街头走进来。</summary>
        private Vector3 StartOf(Walker w, Func<string, Vector3?> doorOf) =>
            w.Trip == ResidentTrip.ToWork ? EndOf(w.Resident) : HomeOrShop(w.Resident, doorOf);

        /// <summary>有工作的人逛完回店里，没工作的回家（走到街头）。</summary>
        private Vector3 HomeOrShop(ResidentDef r, Func<string, Vector3?> doorOf) =>
            r.WorkPlaceId != null ? doorOf(r.WorkPlaceId) ?? EndOf(r) : EndOf(r);

        private Vector3 EndOf(ResidentDef r) => new Vector3(r.FromMetro ? MetroEndX : VillageEndX, 0, SidewalkZ());

        /// <summary>有顾客进玩家的店：从街上某处走向店门（只是画面，不能搭话）。</summary>
        public void SendShopper()
        {
            float x = Mathf.Lerp(_minX, _maxX, 0.55f + (float)_rng.NextDouble() * 0.45f);
            float widen = StreetWorldBuilder.ShoppingWiden;
            Walker w = Spawn(null, new Vector3(x, 0, _rng.NextDouble() < 0.5 ? 2.6f + widen : -2.8f - widen));
            w.Target = ShopDoor;
            w.VanishOnArrive = true;
            w.Speed = 1.8f;
            _walkers.Add(w);
        }

        public void Update(float dt, bool visible)
        {
            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                Walker w = _walkers[i];
                w.Go.SetActive(visible);
                Vector3 p = w.Go.transform.position;
                if (w.Resident != null && w.Resident.Id == Talking)
                {
                    Vector3 face = TalkFace - p;
                    face.y = 0;
                    if (face.sqrMagnitude > 0.01f) w.Go.transform.rotation = Quaternion.LookRotation(face.normalized);
                    continue;
                }
                Vector3 d = w.Target - p;
                if (d.magnitude < 0.2f)
                {
                    if (w.VanishOnArrive)
                    {
                        if (w.Resident != null && !w.Leaving) _arrived[w.Resident.Id] = w.Trip;
                        Kill.It(w.Go);
                        _walkers.RemoveAt(i);
                        continue;
                    }
                    w.Target = RandomSidewalk();
                    continue;
                }
                Vector3 step = d.normalized * Mathf.Min(d.magnitude, w.Speed * dt);
                w.Go.transform.position = p + step;
                w.Go.transform.rotation = Quaternion.LookRotation(d.normalized);
            }
        }

        private float SidewalkZ()
        {
            float widen = StreetWorldBuilder.ShoppingWiden;
            return _rng.NextDouble() < 0.5 ? 2.4f + widen + (float)_rng.NextDouble() * 1.2f : -2.4f - widen - (float)_rng.NextDouble() * 1.4f;
        }

        private Vector3 RandomSidewalk() => new Vector3(Mathf.Lerp(_minX, _maxX, (float)_rng.NextDouble()), 0, SidewalkZ());

        private Walker Spawn(ResidentDef r, Vector3 at)
        {
            int seed = r == null ? _rng.Next() : StableHash(r.Id);
            var look = new System.Random(seed);
            GameObject go = Prims.Figure(_parent, r == null ? "Shopper" : "Resident_" + r.Id, Clothes[look.Next(Clothes.Length)], new Color(0.92f, 0.78f, 0.62f), 0.95f);
            go.transform.position = at;
            return new Walker { Go = go, Resident = r, Speed = 1.0f + (float)look.NextDouble() * 0.8f };
        }

        private static int StableHash(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
