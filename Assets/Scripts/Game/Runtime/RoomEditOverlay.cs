using System.Collections.Generic;
using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>
    /// 装修 / 摆家具的“编辑模式”叠层（俯视图）：铺在地面上的网格（粗线 = 1 米，细线 = 0.25 米吸附格），
    /// 门口留空区和固定设施（红色），鼠标所在格的十字准线，以及手里这件家具的占地框（绿 = 能放，红 = 不行）。
    /// </summary>
    public sealed class RoomEditOverlay
    {
        private readonly GameObject _root;
        private readonly GameObject _minor;
        private readonly Transform _crossX, _crossZ;
        private readonly Transform _foot;
        private readonly Renderer _footRenderer;
        private readonly Material _ok, _bad;
        private readonly float _w, _d;
        private readonly List<Mesh> _meshes = new List<Mesh>();

        private const float GridY = 0.035f;

        public RoomEditOverlay(Vector3 origin, RoomSpec room, IReadOnlyList<RectD> blockers)
        {
            _w = (float)room.Width;
            _d = (float)room.Depth;
            _root = new GameObject("RoomEditOverlay");
            _root.transform.position = origin;

            Material minor = Overlay(new Color(1f, 1f, 1f, 0.10f));
            Material major = Overlay(new Color(1f, 1f, 1f, 0.30f));
            Material border = Overlay(new Color(0.35f, 0.9f, 1f, 0.85f));
            Material zone = Overlay(new Color(0.95f, 0.25f, 0.2f, 0.28f));
            _ok = Overlay(new Color(0.3f, 0.95f, 0.45f, 0.45f));
            _bad = Overlay(new Color(0.95f, 0.3f, 0.25f, 0.5f));
            Material cross = Overlay(new Color(1f, 0.85f, 0.25f, 0.55f));

            _minor = MeshObject("Minor", Lines(0.25f, 0.018f, skipWhole: true), minor);
            MeshObject("Major", Lines(1f, 0.04f, skipWhole: false), major);
            MeshObject("Border", Border(0.09f), border);

            // 门口留空区 + 固定设施：摆不了的地方
            Rect("DoorClear", room.DoorClear, zone, 0.03f);
            if (blockers != null)
                foreach (RectD b in blockers) Rect("Blocked", b, zone, 0.03f);

            _crossX = Strip("CrossX", new Vector3(_w, 0.012f, 0.03f), cross);
            _crossZ = Strip("CrossZ", new Vector3(0.03f, 0.012f, _d), cross);
            // A thin footprint frame leaves both the floor and the furniture's own finish visible.
            var footVertices = new List<Vector3>();
            var footTriangles = new List<int>();
            const float edge = 0.015f;
            Quad(footVertices, footTriangles, -.5f, -.5f, .5f, -.5f + edge);
            Quad(footVertices, footTriangles, -.5f, .5f - edge, .5f, .5f);
            Quad(footVertices, footTriangles, -.5f, -.5f + edge, -.5f + edge, .5f - edge);
            Quad(footVertices, footTriangles, .5f - edge, -.5f + edge, .5f, .5f - edge);
            _foot = MeshObject("Footprint", Build(footVertices, footTriangles), _ok).transform;
            _footRenderer = _foot.GetComponent<Renderer>();
            _crossX.gameObject.SetActive(false);
            _crossZ.gameObject.SetActive(false);
            _foot.gameObject.SetActive(false);
        }

        private static Material Overlay(Color c)
        {
            Material m = Mats.Unlit(c);
            m.SetFloat("_Cull", 0);
            m.renderQueue = 3100;
            return m;
        }

        /// <summary>格线：每 step 米一条，宽 width 米；skipWhole 时跳过整米的线（交给粗线画）。</summary>
        private Mesh Lines(float step, float width, bool skipWhole)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (float x = 0; x <= _w + 1e-4f; x += step)
            {
                if (skipWhole && Mathf.Abs(x - Mathf.Round(x)) < 1e-3f) continue;
                Quad(verts, tris, x - width / 2, 0, x + width / 2, _d);
            }
            for (float z = 0; z <= _d + 1e-4f; z += step)
            {
                if (skipWhole && Mathf.Abs(z - Mathf.Round(z)) < 1e-3f) continue;
                Quad(verts, tris, 0, z - width / 2, _w, z + width / 2);
            }
            return Build(verts, tris);
        }

        private Mesh Border(float t)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Quad(verts, tris, -t, -t, _w + t, 0);
            Quad(verts, tris, -t, _d, _w + t, _d + t);
            Quad(verts, tris, -t, 0, 0, _d);
            Quad(verts, tris, _w, 0, _w + t, _d);
            return Build(verts, tris);
        }

        private static void Quad(List<Vector3> v, List<int> t, float x0, float z0, float x1, float z1)
        {
            int i = v.Count;
            v.Add(new Vector3(x0, GridY, z0));
            v.Add(new Vector3(x0, GridY, z1));
            v.Add(new Vector3(x1, GridY, z1));
            v.Add(new Vector3(x1, GridY, z0));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        private static Mesh Build(List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = "EditGrid" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        private GameObject MeshObject(string name, Mesh mesh, Material mat)
        {
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private Transform Strip(string name, Vector3 size, Material mat)
        {
            GameObject go = Prims.Box(_root.transform, name, new Vector3(0, 0.04f, 0), size, mat);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void Rect(string name, RectD r, Material mat, float y)
        {
            GameObject go = Prims.Box(_root.transform, name,
                new Vector3((float)(r.MinX + r.MaxX) / 2, y, (float)(r.MinZ + r.MaxZ) / 2),
                new Vector3((float)(r.MaxX - r.MinX), 0.01f, (float)(r.MaxZ - r.MinZ)), mat);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>每帧：cursor 是鼠标所在格（房间局部坐标，已吸附），foot 是手里这件的占地框；camSize 太大时不画 0.25 米细线。</summary>
        public void Update(Vector2? cursor, RectD? foot, bool valid, float camSize)
        {
            _minor.SetActive(camSize < 7.5f);
            bool show = cursor.HasValue;
            _crossX.gameObject.SetActive(show);
            _crossZ.gameObject.SetActive(show);
            if (show)
            {
                _crossX.localPosition = new Vector3(_w / 2, 0.045f, cursor.Value.y);
                _crossZ.localPosition = new Vector3(cursor.Value.x, 0.045f, _d / 2);
            }

            _foot.gameObject.SetActive(foot.HasValue);
            if (foot.HasValue)
            {
                RectD r = foot.Value;
                _foot.localPosition = new Vector3((float)(r.MinX + r.MaxX) / 2, 0.05f, (float)(r.MinZ + r.MaxZ) / 2);
                _foot.localScale = new Vector3((float)(r.MaxX - r.MinX), 0.01f, (float)(r.MaxZ - r.MinZ));
                _footRenderer.sharedMaterial = valid ? _ok : _bad;
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                Kill.It(_root);
            }
            foreach (Mesh mesh in _meshes) Kill.It(mesh);
            _meshes.Clear();
        }
    }
}
