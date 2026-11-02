using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class CityPersonMarker : MonoBehaviour { public string PersonId; }
    /// <summary>
    /// 街上的人：每个都是住在这一带的真实街坊（Residents），按作息上班、下班、出门逛；
    /// 上班的人走到店门口进去，回家的人走到街头（地铁站或城中村路口）离开。另有走进玩家店里的顾客（不能搭话）。
    /// </summary>
    public sealed partial class CrowdView
    {
        private sealed class Walker
        {
            public GameObject Go;
            public Vector3 Target;
            public bool VanishOnArrive;
            public ResidentDef Resident;
            public ResidentTrip Trip;
            public bool Leaving;
            public Transform ActivityName;
            public FigureMotion Motion;
        }

        private const float MetroEndX = -97f, VillageEndX = 102f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Dictionary<string, ResidentTrip> _arrived = new Dictionary<string, ResidentTrip>();
        private readonly Transform _parent;
        private readonly System.Random _rng = new System.Random(11);
        private readonly float _minX, _maxX;
        private bool _synced;
        private bool _cityMode;
        private GameSession _citySession;
        private readonly Dictionary<string, Walker> _cityWalkers = new Dictionary<string, Walker>();
        private readonly Dictionary<string, ResidentDef> _cityResidents = new Dictionary<string, ResidentDef>();

        /// <summary>玩家店门口（搬店后改到新铺子）。</summary>
        public Vector3 ShopDoor { get; set; }

        /// <summary>正在和玩家说话的人（停下来面向玩家）；没有是 null。</summary>
        public string Talking { get; set; }

        public Vector3 TalkFace { get; set; }

        /// <summary>Views follow the session's one authoritative person position. Visibility never advances a person or deletes history.</summary>
        public void SyncCity(GameSession session, Vector3 playerWorld, Camera camera = null)
        {
            _citySession = session;
            if (!_cityMode)
            {
                foreach (var old in _walkers) if (old.Go != null) Kill.It(old.Go);
                _walkers.Clear(); _arrived.Clear(); _cityMode = true;
            }
            var wanted = new HashSet<string>();
            foreach (var state in session.CityPeople)
            {
                var definition = session.CityPerson(state.PersonId); if (definition == null) continue;
                Vector3 position = CityPresentationPosition(session, state);
                bool privateInterior = session.CityPersonInside(state) || CityPersonInVehicle(state);
                if (privateInterior) continue;
                if (camera != null)
                {
                    Vector3 screen = camera.WorldToViewportPoint(position + Vector3.up);
                    float margin = _cityWalkers.ContainsKey(state.PersonId) ? .2f : .08f;
                    if (screen.z <= 0 || screen.x < -margin || screen.x > 1 + margin || screen.y < -margin || screen.y > 1 + margin) continue;
                }
                else if ((position - playerWorld).sqrMagnitude > 80 * 80) continue;
                wanted.Add(state.PersonId);
                if (!_cityWalkers.TryGetValue(state.PersonId, out var view))
                {
                    if (!_cityResidents.TryGetValue(state.PersonId, out var resident))
                    {
                        resident = new ResidentDef(definition.Staff, state.WorkPlaceId, definition.WorkStart, definition.WorkEnd, false, stories: definition.Stories);
                        _cityResidents[state.PersonId] = resident;
                    }
                    view = Spawn(resident, position); CityNpcClothing.Apply(view.Go, session, state.PersonId); _cityWalkers[state.PersonId] = view; _walkers.Add(view);
                }
                // Membership and labels are refreshed at low frequency. Never snap an existing
                // actor here: its root and walking rig are advanced every rendered frame below.
                bool participating = state.Route == null && !string.IsNullOrEmpty(state.ActivityId)
                    && session.RunningCityActivity?.Id == state.ActivityId;
                if(view.ActivityName==null)
                {
                    var label=new GameObject("ActivityName").transform;label.SetParent(view.Go.transform,false);label.localPosition=Vector3.up*1.8f;
                    Prims.SignText(label,definition.Staff.Name,Vector3.zero,3f,.38f,ArtDirection.Cream);view.ActivityName=label;
                }
                if(view.ActivityName!=null){view.ActivityName.gameObject.SetActive(participating || (position-playerWorld).sqrMagnitude <= 18*18);view.ActivityName.rotation=CameraRig.Rotation;}
            }
            SyncAmbient(session, camera);
            foreach (string id in new List<string>(_cityWalkers.Keys))
            {
                if (wanted.Contains(id)) continue;
                var view = _cityWalkers[id]; if (view.Go != null) Kill.It(view.Go);
                _walkers.Remove(view); _cityWalkers.Remove(id);
            }
        }

        // Passengers are inside their transport, not pedestrians sprinting at vehicle speed.
        private static bool CityPersonInVehicle(CityPersonState person) =>
            person.Route != null && person.Route.Transport != VehicleKind.Walk;

        private static Vector3 CityPresentationPosition(GameSession session, CityPersonState person)
        {
            var point = person.Route != null
                ? person.Route.Position(session.Clock.Now.TotalMinutes + session.Time.FractionalMinute)
                : new PCShopSell.Core.World.MapPoint(person.X, person.Y);
            return new Vector3((float)point.X, 0, (float)point.Y);
        }

        private void UpdateCityPresentation(float dt, bool visible)
        {
            if (_citySession == null) return;
            foreach (var pair in _cityWalkers)
            {
                var view = pair.Value;
                if (view.Go == null) continue;
                var state = _citySession.CityPersonState(pair.Key);
                bool show = visible && state != null && !_citySession.CityPersonInside(state) && !CityPersonInVehicle(state);
                if (view.Go.activeSelf != show) view.Go.SetActive(show);
                if (!show) continue;
                Vector3 position = CityPresentationPosition(_citySession, state);
                Vector3 direction = position - view.Go.transform.position;
                view.Go.transform.position = position;
                bool participating = state.Route == null && !string.IsNullOrEmpty(state.ActivityId)
                    && _citySession.RunningCityActivity?.Id == state.ActivityId;
                if (participating) direction = StreetWorldBuilder.ToWorld(_citySession.PlayerPosition) - position;
                if (pair.Key == Talking && state.Route == null) direction = TalkFace - position;
                direction.y = 0;
                if (direction.sqrMagnitude > .00001f && dt > 0)
                    view.Go.transform.rotation = Quaternion.RotateTowards(view.Go.transform.rotation,
                        Quaternion.LookRotation(direction.normalized), 540 * dt);
                if (view.Motion != null)
                {
                    view.Motion.ActivityKind = participating ? _citySession.CityActivityService(_citySession.RunningCityActivity)?.Id : null;
                    view.Motion.LocomotionEnabled = state.Route != null;
                    view.Motion.Seated = participating && _citySession.City.GetPlace(_citySession.RunningCityActivity.VenueId).Business == "venue_restaurant";
                }
            }
        }

        private static readonly Color[] Clothes =
        {
            new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.2f, 0.2f),
            new Color(0.3f, 0.7f, 0.4f), new Color(0.85f, 0.65f, 0.2f), new Color(0.6f, 0.3f, 0.7f),
        };

        public static Color PersonClothing(string personId) => Clothes[new System.Random(StableHash(personId ?? "visitor")).Next(Clothes.Length)];

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

        /// <summary>有顾客进玩家的某家店：从街上某处走向那家店的门（只是画面，不能搭话）。door 为空就去 ShopDoor。</summary>
        public void SendShopper(Vector3? door = null)
        {
            if (_cityMode) return; // Shop visitors are drawn by the interior; street foot traffic has its own bounded pool.
            Vector3 target = door ?? ShopDoor;
            float x = Mathf.Clamp(target.x + ((float)_rng.NextDouble() * 2f - 1f) * 45f, _minX, _maxX);
            float widen = StreetWorldBuilder.ShoppingWiden;
            Walker w = Spawn(null, new Vector3(x, 0, _rng.NextDouble() < 0.5 ? 2.6f + widen : -2.8f - widen));
            w.Target = target;
            w.VanishOnArrive = true;
            _walkers.Add(w);
        }

        public void Update(float dt, bool visible)
        {
            if (_cityMode)
            {
                UpdateCityPresentation(dt, visible);
                UpdateAmbient(dt, visible);
                return;
            }
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
                float distance=(float)(PCShopSell.Core.World.CityTravelTiming.ScaledSecondsMeters(Vehicles.Walk,dt,
                    _citySession?.Time.SecondsPerMinuteAt1x??PCShopSell.Core.Time.TimeController.DefaultSecondsPerMinute)
                    *PCShopSell.Core.World.CityTravelTiming.NpcWalkSpeedFactor);
                if(_citySession!=null)distance/=(float)PCShopSell.Core.World.CityTravelTiming.LocalScale(_citySession.City,
                    new PCShopSell.Core.World.MapPoint(p.x,p.z));
                Vector3 step = d.normalized * Mathf.Min(d.magnitude,distance);
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
            if (r != null)
            {
                go.AddComponent<CityPersonMarker>().PersonId = r.Id;
                var collider = go.AddComponent<SphereCollider>(); collider.center = Vector3.up * .9f; collider.radius = .55f;
            }
            return new Walker { Go = go, Resident = r, Motion = go.GetComponent<FigureMotion>() };
        }

        private static int StableHash(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
