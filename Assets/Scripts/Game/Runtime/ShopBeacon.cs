using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>
    /// 玩家租下的铺子在街上的醒目标记：整栋铺子被一层青色光晕包住——楼体十二条边是发亮的霓虹灯管，
    /// 脚下地面一圈光，外面套一层半透明的光壳（缓慢呼吸），门前还有一盏青色点光源。白天晚上都看得见。
    /// 跟着当前租的铺子走（搬店就换栋），没有店就不显示。
    /// </summary>
    public sealed class ShopBeacon
    {
        private readonly GameObject _root;
        private readonly Light _light;
        private Material _shell;
        private Material _ground;
        private string _placeId;
        private float _time;

        private static readonly Color Cyan = new Color(0.1f, 0.95f, 0.85f);

        public bool Visible => _root != null && _root.activeSelf;

        public ShopBeacon(Transform parent)
        {
            _root = new GameObject("MyShopGlow");
            _root.transform.SetParent(parent, false);
            var lgo = new GameObject("GlowLight");
            lgo.transform.SetParent(_root.transform, false);
            _light = lgo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.3f, 1f, 0.9f);
            _light.range = 16f;
            _light.intensity = 2.2f;
            _light.shadows = LightShadows.None;
            _root.SetActive(false);
        }

        /// <summary>按铺子楼体的范围重建一圈光。</summary>
        private void Rebuild(Bounds b)
        {
            for (int i = _root.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = _root.transform.GetChild(i);
                if (c.gameObject == _light.gameObject) continue;
                if (Application.isPlaying) Object.Destroy(c.gameObject); else Object.DestroyImmediate(c.gameObject);
            }

            Material tube = Mats.Glow(new Color(0.3f, 1f, 0.95f), new Color(1.6f, 5f, 4.6f), false);
            Material soft = Mats.Glow(new Color(0.05f, 0.5f, 0.45f), new Color(0.5f, 3.2f, 2.8f), false);
            _shell = Mats.Unlit(new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f));
            _ground = Mats.Unlit(new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f));

            Vector3 mn = b.min - new Vector3(0.12f, 0, 0.12f);
            Vector3 mx = b.max + new Vector3(0.12f, 0.12f, 0.12f);
            Vector3 c0 = (mn + mx) * 0.5f, size = mx - mn;
            const float t = 0.22f;

            // 脚下一大片柔光 + 贴着楼体的一圈亮边
            Prims.Box(_root.transform, "GroundGlow", new Vector3(c0.x, 0.04f, c0.z), new Vector3(size.x + 3.2f, 0.02f, size.z + 3.2f), _ground);
            Prims.Box(_root.transform, "Ring", new Vector3(c0.x, 0.07f, mn.z - 0.5f), new Vector3(size.x + 1f, 0.03f, 0.3f), soft);

            // 楼体十二条边做成霓虹灯管
            foreach (float y in new[] { 0.1f, mx.y })
            {
                Prims.Box(_root.transform, "EdgeFront", new Vector3(c0.x, y, mn.z), new Vector3(size.x + t, t, t), tube);
                Prims.Box(_root.transform, "EdgeBack", new Vector3(c0.x, y, mx.z), new Vector3(size.x + t, t, t), tube);
                Prims.Box(_root.transform, "EdgeLeft", new Vector3(mn.x, y, c0.z), new Vector3(t, t, size.z + t), tube);
                Prims.Box(_root.transform, "EdgeRight", new Vector3(mx.x, y, c0.z), new Vector3(t, t, size.z + t), tube);
            }
            foreach (float x in new[] { mn.x, mx.x })
                foreach (float z in new[] { mn.z, mx.z })
                    Prims.Box(_root.transform, "Pillar", new Vector3(x, c0.y, z), new Vector3(t, size.y, t), tube);

            // 外面一层半透明光壳，整栋楼像被光包住
            Prims.Box(_root.transform, "Shell", c0, size + new Vector3(0.5f, 0.5f, 0.5f), _shell);

            _light.transform.position = new Vector3(c0.x, Mathf.Max(3f, mx.y * 0.6f), mn.z - 2.5f);
        }

        /// <summary>每帧：有店就显示，没店隐藏；搬店后换楼；光壳缓慢呼吸。</summary>
        public void Update(GameSession s, StreetWorldBuilder world, float dt)
        {
            bool show = s != null && s.HasShop && world.Places.ContainsKey(s.PlayerShopPlace.Id);
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            if (_placeId != s.PlayerShopPlace.Id)
            {
                _placeId = s.PlayerShopPlace.Id;
                Transform body = world.Places[_placeId].Root.transform.Find("Body");
                var r = body != null ? body.GetComponent<Renderer>() : null;
                Rebuild(r != null ? r.bounds : new Bounds(world.Places[_placeId].Entrance + new Vector3(0, 2, 4), new Vector3(5, 4, 7)));
            }

            _time += dt;
            float k = 0.5f + 0.5f * Mathf.Sin(_time * 2f);
            if (_shell != null) _shell.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Lerp(0.12f, 0.26f, k));
            if (_ground != null) _ground.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Lerp(0.22f, 0.4f, k));
            _light.intensity = Mathf.Lerp(1.6f, 2.8f, k);
        }
    }
}
