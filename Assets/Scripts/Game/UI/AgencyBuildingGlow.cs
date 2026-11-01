using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>Screen-space building glow, independent of preview-camera lighting and transparent culling.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AgencyBuildingGlow : MaskableGraphic
    {
        private Vector2[] _outline;
        protected override void OnEnable()
        {
            // Also repair objects created before the renderer requirement was introduced.
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }
        public void Initialize(Vector2[] projectedCorners, Color tint)
        {
            raycastTarget = false; color = tint;
            var points = projectedCorners.Select(p => new Vector2(p.x, -p.y)).Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            var hull = new List<Vector2>();
            foreach (var point in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lower = hull.Count;
            for (int i = points.Length - 2; i >= 0; i--)
            {
                while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            _outline = hull.ToArray(); SetVerticesDirty();
        }
        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_outline == null || _outline.Length < 3) return;
            var fill = new Color(color.r, color.g, color.b, .28f);
            Vector2 center = Vector2.zero; foreach (var point in _outline) center += point; center /= _outline.Length;
            mesh.AddVert(center, fill, Vector2.zero);
            foreach (var point in _outline) mesh.AddVert(point, fill, Vector2.zero);
            for (int i = 0; i < _outline.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % _outline.Length + 1);
            for (int layer = 0; layer < 3; layer++)
            {
                float width = layer == 0 ? 14 : layer == 1 ? 7 : 2.5f;
                var tint = new Color(color.r, color.g, color.b, layer == 0 ? .12f : layer == 1 ? .32f : 1);
                for (int i = 0; i < _outline.Length; i++)
                {
                    var a = _outline[i]; var b = _outline[(i + 1) % _outline.Length]; var delta = b - a;
                    var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
                    int start = mesh.currentVertCount;
                    mesh.AddVert(a + normal, tint, Vector2.zero); mesh.AddVert(b + normal, tint, Vector2.zero);
                    mesh.AddVert(b - normal, tint, Vector2.zero); mesh.AddVert(a - normal, tint, Vector2.zero);
                    mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }
    }
}
