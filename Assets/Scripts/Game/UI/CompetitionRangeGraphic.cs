using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    // The points are projections of the actual world-space circle, so camera zoom,
    // rotation and perspective cannot change the competition boundary.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CompetitionRangeGraphic : MaskableGraphic
    {
        private Vector2[] _points;
        public void Initialize(Vector2[] points)
        { _points = points; raycastTarget = false; color = new Color(1f, .69f, .25f, .9f); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_points == null || _points.Length < 3) return;
            Rect rect = rectTransform.rect;
            for (int i = 0; i < _points.Length; i++)
            {
                var a = new Vector2(rect.xMin + _points[i].x, rect.yMax - _points[i].y);
                var b = new Vector2(rect.xMin + _points[(i + 1) % _points.Length].x, rect.yMax - _points[(i + 1) % _points.Length].y);
                var direction = (b - a).normalized;
                var normal = new Vector2(-direction.y, direction.x) * 1.4f;
                int offset = mesh.currentVertCount;
                mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero);
                mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
                mesh.AddTriangle(offset, offset + 1, offset + 2); mesh.AddTriangle(offset, offset + 2, offset + 3);
            }
        }
    }
}
