using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>标记街景中的自营门店、当前租住处和已购住宅。</summary>
    public sealed class ShopBeacon
    {
        public enum Style { Shop, RentedResidence, OwnedResidence }

        internal const int StreetPropertyEffectLayer = 29;
        private readonly GameObject _root;
        private readonly Light _light;
        private readonly Style _style;
        private bool Residential => _style != Style.Shop;
        private Material _shell;
        private Material _ground;
        private string _placeId;
        private GameObject _building;
        private float _time;

        private static readonly Color Cyan = new Color(0.1f, 0.95f, 0.85f);

        public bool Visible => _root != null && _root.activeSelf;

        public ShopBeacon(Transform parent, Style style = Style.Shop)
        {
            _style = style;
            _root = new GameObject(style switch
            {
                Style.RentedResidence => "MyHomeOutline",
                Style.OwnedResidence => "OwnedResidenceOutline",
                _ => "MyShopGlow"
            });
            _root.layer = StreetPropertyEffectLayer;
            _root.transform.SetParent(parent, false);
            _root.SetActive(false);
            if (Residential) return;

            var lgo = new GameObject("GlowLight") { layer = StreetPropertyEffectLayer };
            lgo.transform.SetParent(_root.transform, false);
            _light = lgo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.3f, 1f, 0.9f);
            _light.range = 16f;
            _light.intensity = 2.2f;
            _light.shadows = LightShadows.None;
        }

        /// <summary>按建筑的实际外观范围重建外罩。</summary>
        private void Rebuild(Bounds b)
        {
            for (int i = _root.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = _root.transform.GetChild(i);
                if (_light != null && c.gameObject == _light.gameObject) continue;
                c.gameObject.SetActive(false);
                if (Application.isPlaying) Object.Destroy(c.gameObject); else Object.DestroyImmediate(c.gameObject);
            }

            bool ownedResidence = _style == Style.OwnedResidence;
            Material tube = Residential
                ? Mats.Unlit(ownedResidence ? new Color(.82f, .65f, 1f) : new Color(.61f, .82f, 1f, .66f))
                : Mats.Glow(new Color(0.3f, 1f, 0.95f), new Color(1.6f, 5f, 4.6f), false);

            Vector3 mn = b.min - new Vector3(0.12f, 0, 0.12f);
            Vector3 mx = b.max + new Vector3(0.12f, 0.12f, 0.12f);
            Vector3 c0 = (mn + mx) * 0.5f, size = mx - mn;
            float t = Residential ? .13f : .22f;

            // 楼体十二条边做成霓虹灯管
            foreach (float y in new[] { mn.y+0.1f, mx.y })
            {
                Prims.Box(_root.transform, "EdgeFront", new Vector3(c0.x, y, mn.z), new Vector3(size.x + t, t, t), tube);
                Prims.Box(_root.transform, "EdgeBack", new Vector3(c0.x, y, mx.z), new Vector3(size.x + t, t, t), tube);
                Prims.Box(_root.transform, "EdgeLeft", new Vector3(mn.x, y, c0.z), new Vector3(t, t, size.z + t), tube);
                Prims.Box(_root.transform, "EdgeRight", new Vector3(mx.x, y, c0.z), new Vector3(t, t, size.z + t), tube);
            }
            foreach (float x in new[] { mn.x, mx.x })
                foreach (float z in new[] { mn.z, mx.z })
                    Prims.Box(_root.transform, "Pillar", new Vector3(x, c0.y, z), new Vector3(t, size.y, t), tube);

            if (Residential)
            {
                if (!ownedResidence)
                    Prims.Box(_root.transform, "HomeShell", c0, size, Mats.Unlit(new Color(.48f, .75f, 1f, .18f)));
            }
            else
            {
                Material soft = Mats.Glow(new Color(0.05f, 0.5f, 0.45f), new Color(0.5f, 3.2f, 2.8f), false);
                _shell = Mats.Unlit(new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f));
                _ground = Mats.Unlit(new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f));
                Prims.Box(_root.transform, "GroundGlow", new Vector3(c0.x, mn.y+0.04f, c0.z), new Vector3(size.x + 3.2f, 0.02f, size.z + 3.2f), _ground);
                Prims.Box(_root.transform, "Ring", new Vector3(c0.x, mn.y+0.07f, mn.z - 0.5f), new Vector3(size.x + 1f, 0.03f, 0.3f), soft);

                // 外面一层半透明光壳，整栋楼像被光包住
                Prims.Box(_root.transform, "Shell", c0, size + new Vector3(0.5f, 0.5f, 0.5f), _shell);

                _light.transform.localPosition = new Vector3(c0.x, mn.y+Mathf.Max(3f, size.y * 0.6f), mn.z - 2.5f);
            }
            foreach (Transform part in _root.transform) part.gameObject.layer = StreetPropertyEffectLayer;
        }

        private static Bounds ResidenceBounds(PlaceVisual building)
        {
            var root = building.Root.transform;
            var bounds = new Bounds();
            bool found = false;
            // 楼体碰撞盒随单栋建筑保存，街区的静态合批不会改变它们的范围。
            foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || box.GetComponent<PlaceMarker>()?.PlaceId != building.Place.Id) continue;
                var toLocal = root.worldToLocalMatrix * box.transform.localToWorldMatrix;
                var half = box.size * .5f;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = toLocal.MultiplyPoint3x4(box.center + new Vector3(x * half.x, y * half.y, z * half.z));
                    if (found) bounds.Encapsulate(corner);
                    else { bounds = new Bounds(corner, Vector3.zero); found = true; }
                }
            }
            if (!found)
            {
                float width = (float)building.Place.Width;
                float depth = (float)building.Place.Depth;
                float height = Mathf.Max(4f, (float)(building.ResidentialBlock?.height ?? 6));
                bounds = new Bounds(new Vector3(0, height / 2, depth / 2), new Vector3(width, height, depth));
            }
            // 碰撞盒覆盖墙体。留出少量屋檐和屋顶空间。
            bounds.Expand(new Vector3(.7f, 0, .7f));
            bounds.Encapsulate(bounds.max + Vector3.up * 1.75f);
            return bounds;
        }

        /// <summary>同步外罩与建筑的位置，placeId 为空时隐藏。</summary>
        public void Update(GameSession s, StreetWorldBuilder world, string placeId, float dt)
        {
            if (s == null || placeId == null || !world.Places.TryGetValue(placeId, out var building) || building.Root == null)
            {
                if (_root.activeSelf) _root.SetActive(false);
                return;
            }
            if (!_root.activeSelf) _root.SetActive(true);
            var transform = building.Root.transform;
            // Match the building's local coordinate system as a sibling, so geometry scans cannot include this glow.
            if(_root.transform.parent!=transform.parent)_root.transform.SetParent(transform.parent,false);
            _root.transform.localPosition=transform.localPosition;
            _root.transform.localRotation=transform.localRotation;
            _root.transform.localScale=transform.localScale;
            if (_placeId != placeId || _building!=building.Root)
            {
                _placeId = placeId;
                _building=building.Root;
                Rebuild(Residential ? ResidenceBounds(building) : building.BuildingBounds(transform));
            }

            if (Residential) return;

            _time += dt;
            float k = 0.5f + 0.5f * Mathf.Sin(_time * 2f);
            if (_shell != null) _shell.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Lerp(0.12f, 0.26f, k));
            if (_ground != null) _ground.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Lerp(0.22f, 0.4f, k));
            _light.intensity = Mathf.Lerp(1.6f, 2.8f, k);
        }
    }
}
