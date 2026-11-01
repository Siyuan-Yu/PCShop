using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>轻量地图只绘制权威路网，不生成第二份世界或修改导航状态。</summary>
    public sealed class CityRouteMapGraphic : MaskableGraphic
    {
        private readonly List<Vector2> _segments = new List<Vector2>();
        public void SetSegments(IEnumerable<Vector2> segments) { _segments.Clear(); _segments.AddRange(segments); raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            for (int i = 0; i + 1 < _segments.Count; i += 2)
            {
                Vector2 a = r.min + Vector2.Scale(_segments[i], r.size), b = r.min + Vector2.Scale(_segments[i + 1], r.size);
                Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * 1.4f;
                int index = mesh.currentVertCount;
                mesh.AddVert(a - side, color, Vector2.zero); mesh.AddVert(a + side, color, Vector2.zero);
                mesh.AddVert(b + side, color, Vector2.zero); mesh.AddVert(b - side, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
