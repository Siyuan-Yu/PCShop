using System;
using System.Linq;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReportPriceTrendGraphic : MaskableGraphic
    {
        private int[] _days = Array.Empty<int>();
        private double[] _values = Array.Empty<double>();
        private double _min, _max;
        public void Initialize(int[] days, double[] values)
        {
            _days = days; _values = values; raycastTarget = false;
            if (values.Length == 0)
            {
                var empty = Ui.Label(transform, "暂无同口径历史报价", 15, TextAnchor.MiddleCenter, Ui.Dim);
                empty.rectTransform.Anchor(0, 0, 1, 1);
                return;
            }
            double pad = Math.Max(.01, (values.Max() - values.Min()) * .18);
            _min = Math.Max(0, values.Min() - pad); _max = values.Max() + pad;
            for (int i = 0; i <= 2; i++)
            {
                float y = i / 2f;
                var label = Ui.Label(transform, (_min + (_max - _min) * y).ToString("#,0.##"), 12, TextAnchor.MiddleRight, Ui.Dim);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, y);
                label.rectTransform.pivot = new Vector2(0, .5f);
                label.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(24, -10, y));
                label.rectTransform.sizeDelta = new Vector2(72, 18);
            }
            foreach (int i in new[] { 0, days.Length / 2, days.Length - 1 }.Distinct())
            {
                float x = days.Length < 2 ? .5f : (days[i] - days[0]) / (float)Math.Max(1, days.Last() - days[0]);
                var date = new GameDate(days[i]);
                var label = Ui.Label(transform, date.Month + "/" + date.Day, 12, TextAnchor.MiddleCenter, Ui.Dim);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, 0);
                label.rectTransform.pivot = new Vector2(.5f, 0);
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(82, -12, x), 0);
                label.rectTransform.sizeDelta = new Vector2(48, 20);
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_values.Length == 0) return;
            var r = rectTransform.rect;
            float left = r.xMin + 82, right = r.xMax - 12, bottom = r.yMin + 24, top = r.yMax - 10;
            for (int i = 0; i <= 2; i++)
                AssetTrendGraphic.Stroke(mesh, new Vector2(left, Mathf.Lerp(bottom, top, i / 2f)),
                    new Vector2(right, Mathf.Lerp(bottom, top, i / 2f)), 1, Ui.Edge);
            Vector2 previous = default;
            for (int i = 0; i < _values.Length; i++)
            {
                float t = _values.Length == 1 ? .5f : (_days[i] - _days[0]) / (float)Math.Max(1, _days.Last() - _days[0]);
                var point = new Vector2(Mathf.Lerp(left, right, t), Mathf.Lerp(bottom, top, (float)((_values[i] - _min) / (_max - _min))));
                var tint = i == 0 ? Ui.Accent : Ui.ChangeColor(_values[i] - _values[i - 1]);
                if (i > 0) AssetTrendGraphic.Stroke(mesh, previous, point, 2.5f, tint);
                AssetTrendGraphic.Stroke(mesh, point - Vector2.right * 2, point + Vector2.right * 2, 4, tint);
                previous = point;
            }
        }
    }
}
