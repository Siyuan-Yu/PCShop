using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>斜俯视正交镜头：默认俯角 35°、偏航 45°。街上可以旋转/俯仰（右键拖动、Q/R），店内固定；平滑跟随目标，滚轮缩放。</summary>
    public sealed class CameraRig
    {
        public const float DefaultYaw = 45f;
        public const float DefaultPitch = 35f;
        public const float MinPitch = 18f;
        public const float MaxPitch = 78f;

        public static float Yaw = DefaultYaw;
        public static float Pitch = DefaultPitch;
        public static Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        private bool _locked;
        private float _streetYaw = DefaultYaw, _streetPitch = DefaultPitch;
        private readonly Camera _cam;
        private Vector3 _focus;
        public float Size = 16f;
        public float MinSize = 5f;
        public float MaxSize = 60f;

        public CameraRig(Camera cam)
        {
            _cam = cam;
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 600f;
            _cam.transform.rotation = Rotation;
        }

        public void Snap(Vector3 focus, float size)
        {
            _focus = focus;
            Size = size;
            _cam.orthographicSize = size;
            Apply();
        }

        /// <summary>进出店铺：店内固定默认视角，出来时恢复街上的视角。</summary>
        public void SetInterior(bool interior)
        {
            if (interior == _locked) return;
            _locked = interior;
            if (interior)
            {
                _streetYaw = Yaw;
                _streetPitch = Pitch;
                Yaw = DefaultYaw;
                Pitch = DefaultPitch;
            }
            else
            {
                Yaw = _streetYaw;
                Pitch = _streetPitch;
            }
        }

        public void Orbit(float dYaw, float dPitch)
        {
            if (_locked) return;
            Yaw = Mathf.Repeat(Yaw + dYaw, 360f);
            Pitch = Mathf.Clamp(Pitch + dPitch, MinPitch, MaxPitch);
        }

        public void ResetView()
        {
            if (_locked) return;
            Yaw = DefaultYaw;
            Pitch = DefaultPitch;
        }

        public void Zoom(float scroll)
        {
            if (Mathf.Abs(scroll) < 1e-4f) return;
            Size = Mathf.Clamp(Size * (1f - scroll * 0.12f), MinSize, MaxSize);
        }

        public void Follow(Vector3 focus, float dt)
        {
            _focus = Vector3.Lerp(_focus, focus, 1f - Mathf.Exp(-dt * 6f));
            Apply();
        }

        private void Apply()
        {
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, Size, 0.25f);
            _cam.transform.rotation = Rotation;
            _cam.transform.position = _focus - Rotation * Vector3.forward * 120f;
        }
    }

    /// <summary>玩家小人 + 载具。</summary>
    public sealed class AvatarView
    {
        private readonly GameObject _root;
        private readonly GameObject _figure;
        private readonly GameObject _ebike;
        private readonly GameObject _tricycle;
        private readonly GameObject _taxi;
        private float _bob;

        public Vector3 Position => _root.transform.position;

        public AvatarView(Transform parent)
        {
            _root = new GameObject("Player");
            _root.transform.SetParent(parent, false);
            _figure = Prims.Figure(_root.transform, "Figure", new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.8f, 0.65f), 1.1f);
            Prims.Shape(PrimitiveType.Cylinder, _root.transform, "Ring", new Vector3(0, 0.03f, 0), new Vector3(1.4f, 0.01f, 1.4f), Mats.Unlit(new Color(1f, 0.85f, 0.2f, 0.6f)));

            _ebike = new GameObject("EBike");
            _ebike.transform.SetParent(_root.transform, false);
            Prims.Box(_ebike.transform, "Frame", new Vector3(0, 0.45f, 0), new Vector3(0.35f, 0.4f, 1.5f), Mats.Lit(new Color(0.2f, 0.5f, 0.9f), 0.6f));
            Prims.Box(_ebike.transform, "Basket", new Vector3(0, 0.8f, 0.7f), new Vector3(0.5f, 0.3f, 0.4f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)));

            _tricycle = new GameObject("Tricycle");
            _tricycle.transform.SetParent(_root.transform, false);
            Prims.Box(_tricycle.transform, "Front", new Vector3(0, 0.45f, 0.4f), new Vector3(0.35f, 0.4f, 1.0f), Mats.Lit(new Color(0.85f, 0.2f, 0.2f), 0.5f));
            Prims.Box(_tricycle.transform, "Bed", new Vector3(0, 0.55f, -0.9f), new Vector3(1.3f, 0.35f, 1.4f), Mats.Lit(new Color(0.5f, 0.5f, 0.55f), 0.3f, 0.5f));
            Prims.Box(_tricycle.transform, "Cargo", new Vector3(0, 0.95f, -0.9f), new Vector3(1.0f, 0.5f, 1.0f), Mats.Lit(new Color(0.75f, 0.6f, 0.4f)));

            _taxi = new GameObject("Taxi");
            _taxi.transform.SetParent(_root.transform, false);
            Prims.Box(_taxi.transform, "Body", new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.9f, 4f), Mats.Lit(new Color(0.95f, 0.8f, 0.1f), 0.7f, 0.2f));
            Prims.Box(_taxi.transform, "Cabin", new Vector3(0, 1.3f, -0.2f), new Vector3(1.6f, 0.6f, 2.2f), Mats.Lit(new Color(0.2f, 0.25f, 0.3f), 0.9f));
            Prims.Box(_taxi.transform, "TaxiSign", new Vector3(0, 1.7f, -0.2f), new Vector3(0.6f, 0.2f, 0.3f), Mats.Glow(Color.white, new Color(2f, 2f, 1.5f), false));
        }

        public void Update(GameSession s, float dt)
        {
            bool visible = s.CurrentPlace == null;
            _root.SetActive(visible);
            if (!visible) return;

            Vector3 p = StreetWorldBuilder.ToWorld(s.PlayerPosition);
            _root.transform.position = p;
            if (s.IsWalking)
            {
                var dir = s.Trip != null ? new Vector3((float)s.Trip.DirX, 0, (float)s.Trip.DirY) : new Vector3((float)s.HeadingX, 0, (float)s.HeadingY);
                if (dir.sqrMagnitude > 0.01f)
                    _root.transform.rotation = Quaternion.Slerp(_root.transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-dt * 12f));
                _bob += dt * 12f;
            }

            VehicleKind v = s.Vehicle;
            _ebike.SetActive(v == VehicleKind.EBike);
            _tricycle.SetActive(v == VehicleKind.Tricycle);
            _taxi.SetActive(v == VehicleKind.Taxi && s.Trip != null);
            _figure.SetActive(!(v == VehicleKind.Taxi && s.Trip != null));
            float lift = v == VehicleKind.EBike || v == VehicleKind.Tricycle ? 0.35f : 0f;
            float bob = s.IsWalking && v == VehicleKind.Walk ? Mathf.Abs(Mathf.Sin(_bob)) * 0.12f : 0f;
            _figure.transform.localPosition = new Vector3(0, lift + bob, 0);
        }
    }

    /// <summary>交警与视野扇形。</summary>
    public sealed class PoliceView
    {
        private readonly List<(Officer o, GameObject go, MeshRenderer cone)> _items = new List<(Officer, GameObject, MeshRenderer)>();
        private readonly Material _calm = Mats.Unlit(new Color(1f, 0.9f, 0.2f, 0.22f));
        private readonly Material _alarm = Mats.Unlit(new Color(1f, 0.15f, 0.1f, 0.38f));

        public PoliceView(Transform parent, PoliceSystem police)
        {
            Mesh fan = Prims.Fan((float)Officer.SightRange, (float)Officer.SightHalfAngleDeg);
            foreach (Officer o in police.Officers)
            {
                GameObject go = Prims.Figure(parent, "Officer_" + o.RouteId, new Color(0.15f, 0.25f, 0.55f), new Color(0.9f, 0.75f, 0.6f), 1.1f);
                Prims.Box(go.transform, "Vest", new Vector3(0, 0.75f, 0), new Vector3(0.52f, 0.4f, 0.42f), Mats.Glow(new Color(0.9f, 0.9f, 0.2f), new Color(1.5f, 1.5f, 0.2f), false));
                Prims.Shape(PrimitiveType.Cylinder, go.transform, "Cap", new Vector3(0, 1.55f, 0), new Vector3(0.36f, 0.06f, 0.36f), Mats.Lit(Color.white));
                var coneGo = new GameObject("Cone");
                coneGo.transform.SetParent(go.transform, false);
                coneGo.transform.localPosition = new Vector3(0, 0.08f, 0);
                coneGo.AddComponent<MeshFilter>().sharedMesh = fan;
                var mr = coneGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _calm;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _items.Add((o, go, mr));
            }
        }

        public void Update(bool streetVisible)
        {
            foreach (var (o, go, cone) in _items)
            {
                bool show = streetVisible && o.State != OfficerState.OffDuty;
                go.SetActive(show);
                if (!show) continue;
                go.transform.position = StreetWorldBuilder.ToWorld(o.Position);
                var dir = new Vector3((float)o.DirX, 0, (float)o.DirY);
                if (dir.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(dir);
                cone.sharedMaterial = o.State == OfficerState.Chase ? _alarm : _calm;
                cone.gameObject.SetActive(o.State != OfficerState.Cooldown);
            }
        }
    }

    /// <summary>街上的行人（纯装饰），以及走进玩家店里的顾客。</summary>
    public sealed class CrowdView
    {
        private sealed class Walker
        {
            public GameObject Go;
            public Vector3 Target;
            public float Speed;
            public bool VanishOnArrive;
        }

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Transform _parent;
        private readonly System.Random _rng = new System.Random(11);
        private readonly float _minX, _maxX;
        private readonly Vector3 _shopDoor;

        private static readonly Color[] Clothes =
        {
            new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.2f, 0.2f),
            new Color(0.3f, 0.7f, 0.4f), new Color(0.85f, 0.65f, 0.2f), new Color(0.6f, 0.3f, 0.7f),
        };

        public CrowdView(Transform parent, float minX, float maxX, Vector3 shopDoor, int count)
        {
            _parent = parent;
            _minX = minX;
            _maxX = maxX;
            _shopDoor = shopDoor;
            for (int i = 0; i < count; i++)
            {
                var w = Spawn(RandomSidewalk());
                w.Target = RandomSidewalk();
                _walkers.Add(w);
            }
        }

        /// <summary>有顾客进玩家的店：从街上某处走向店门。</summary>
        public void SendShopper()
        {
            float x = Mathf.Lerp(_minX, _maxX, 0.55f + (float)_rng.NextDouble() * 0.45f);
            var w = Spawn(new Vector3(x, 0, _rng.NextDouble() < 0.5 ? 2.6f : -2.8f));
            w.Target = _shopDoor;
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
                Vector3 d = w.Target - p;
                if (d.magnitude < 0.2f)
                {
                    if (w.VanishOnArrive)
                    {
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

        private Vector3 RandomSidewalk()
        {
            float x = Mathf.Lerp(_minX, _maxX, (float)_rng.NextDouble());
            float z = _rng.NextDouble() < 0.5 ? 2.4f + (float)_rng.NextDouble() * 1.2f : -2.4f - (float)_rng.NextDouble() * 1.4f;
            return new Vector3(x, 0, z);
        }

        private Walker Spawn(Vector3 at)
        {
            GameObject go = Prims.Figure(_parent, "Pedestrian", Clothes[_rng.Next(Clothes.Length)], new Color(0.92f, 0.78f, 0.62f), 0.95f);
            go.transform.position = at;
            return new Walker { Go = go, Speed = 1.0f + (float)_rng.NextDouble() * 0.8f };
        }
    }
}
