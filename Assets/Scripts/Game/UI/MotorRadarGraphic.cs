using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>Six-axis performance chart shared by vehicle purchase and management.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MotorRadarGraphic : MaskableGraphic
    {
        private static readonly string[] Labels = { "极速", "加速", "抓地", "转向", "制动", "稳定" };
        private double[] _ratings;
        private readonly Text[] _labels = new Text[6];
        private const float Radius = 69;

        public void Initialize(MotorModel model)
        {
            raycastTarget = false;
            _ratings = MotorCatalog.PerformanceRatings(model);
            for (int i = 0; i < 6; i++)
            {
                var label = _labels[i];
                if (label == null) _labels[i] = label = Ui.Label(transform, "", 14, TextAnchor.MiddleCenter, Ui.TextColor);
                label.text = Labels[i] + " " + _ratings[i].ToString("0");
                label.raycastTarget = false;
                var rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                // Side labels sit outside the polygon; top/bottom labels need less horizontal space.
                rt.anchoredPosition = Direction(i) * (i % 3 == 0 ? 90 : 108);
                rt.sizeDelta = new Vector2(76, 24);
            }
            SetVerticesDirty();
        }

        private static Vector2 Direction(int axis)
        {
            float angle = (90 - axis * 60) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_ratings == null) return;
            Vector2 center = rectTransform.rect.center;
            var grid = new Color(.65f, .72f, .78f, .24f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 direction = Direction(i), next = Direction((i + 1) % 6);
                AssetTrendGraphic.Stroke(mesh, center, center + direction * Radius, 1, grid);
                for (int ring = 1; ring <= 4; ring++)
                    AssetTrendGraphic.Stroke(mesh, center + direction * Radius * ring / 4,
                        center + next * Radius * ring / 4, 1, grid);
            }
            Color fill = Ui.Accent; fill.a = .27f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = center + Direction(i) * Radius * (float)(_ratings[i] / 100);
                Vector2 b = center + Direction((i + 1) % 6) * Radius * (float)(_ratings[(i + 1) % 6] / 100);
                int index = mesh.currentVertCount;
                mesh.AddVert(center, fill, Vector2.zero);
                mesh.AddVert(a, fill, Vector2.zero);
                mesh.AddVert(b, fill, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                AssetTrendGraphic.Stroke(mesh, a, b, 2, Ui.Accent);
                AssetTrendGraphic.Stroke(mesh, a - Vector2.right * 2, a + Vector2.right * 2, 4, Ui.Accent);
            }
        }
    }
}
