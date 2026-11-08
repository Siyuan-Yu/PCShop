using System;
using System.Linq;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AssetTrendGraphic : MaskableGraphic
    {
        internal static readonly Color ExpenseColor = new Color32(233, 178, 150, 255);

        protected override void OnEnable()
        {
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        private long[] _income = Array.Empty<long>(), _expense = Array.Empty<long>();
        private bool _compact, _groupedBars;
        public void Initialize(long[] income, long[] expense, int firstDay, bool compact, bool groupedBars = false)
        {
            _income = income; _expense = expense; _compact = compact; _groupedBars = groupedBars; raycastTarget = false;
            if (!compact)
            {
                float axisWidth = groupedBars ? 86 : 47;
                long maximum = Math.Max(100, income.Concat(expense).DefaultIfEmpty(0).Max());
                for (int i = 0; i < 3; i++)
                {
                    var label = Ui.Label(transform, (maximum / 200.0 * i).ToString(maximum<10000?"0.##":"N0"), 12, TextAnchor.MiddleRight, Ui.Dim);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, i / 2f);
                    label.rectTransform.pivot = new Vector2(1, .5f);
                    label.rectTransform.anchoredPosition = new Vector2(axisWidth, Mathf.Lerp(25,-12,i/2f));
                    label.rectTransform.sizeDelta = new Vector2(axisWidth, 18);
                }
                int labelStep = groupedBars ? Math.Max(1, (income.Length + 6) / 7) : 1;
                for (int i = 0; i < income.Length; i++)
                {
                    if (i != 0 && i != income.Length - 1 && (i % labelStep != 0 || income.Length - 1 - i < labelStep)) continue;
                    var day = new GameDate(firstDay + i);
                    var label = Ui.Label(transform, day.Month + "/" + day.Day, 12, TextAnchor.MiddleCenter, Ui.Dim);
                    float x = groupedBars ? (i + .5f) / income.Length : income.Length == 1 ? .5f : i / (float)(income.Length - 1);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, 0);
                    label.rectTransform.pivot = new Vector2(.5f, 0);
                    label.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(axisWidth + 8, -12, x), 0);
                    label.rectTransform.sizeDelta = new Vector2(52, 20);
                }
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_income.Length == 0) return;
            Rect rect = rectTransform.rect;
            float left = rect.xMin + (_compact ? 2 : _groupedBars ? 94 : 55), right = rect.xMax - (_compact ? 2 : 12);
            float bottom = rect.yMin + (_compact ? 3 : 25), top = rect.yMax - (_compact ? 3 : 12);
            long max = Math.Max(100, _income.Concat(_expense).Max());
            if (!_compact) for (int i = 0; i < 3; i++) Stroke(mesh, new Vector2(left, Mathf.Lerp(bottom, top, i / 2f)), new Vector2(right, Mathf.Lerp(bottom, top, i / 2f)), 1, new Color(.6f,.65f,.7f,.2f));
            if (_groupedBars)
            {
                float slot = (right - left) / _income.Length;
                float width = Mathf.Min(36, slot * .32f);
                for (int i = 0; i < _income.Length; i++)
                {
                    float center = left + slot * (i + .5f);
                    Bar(_income[i], center - width * .62f, Ui.Good);
                    Bar(_expense[i], center + width * .62f, ExpenseColor);
                }
                return;

                void Bar(long cents, float x, Color tint)
                {
                    if (cents == 0) return;
                    float y = Mathf.Max(bottom + 1, Mathf.Lerp(bottom, top, cents / (float)max));
                    Stroke(mesh, new Vector2(x, bottom), new Vector2(x, y), width, tint);
                }
            }

            void Series(long[] values, Color tint)
            {
                Vector2 previous = Vector2.zero;
                for (int i = 0; i < values.Length; i++)
                {
                    var point = new Vector2(Mathf.Lerp(left, right, values.Length == 1 ? .5f : i / (float)(values.Length - 1)), Mathf.Lerp(bottom, top, values[i] / (float)max));
                    if (i > 0) Stroke(mesh, previous, point, _compact ? 1.3f : 2.1f, tint);
                    if (!_compact) Stroke(mesh, point - Vector2.right * 2, point + Vector2.right * 2, 4, tint);
                    previous = point;
                }
            }
            Series(_income, Ui.Good); Series(_expense, ExpenseColor);
        }
        internal static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var side = new Vector2(-(b-a).y, (b-a).x).normalized * width / 2;
            int index = mesh.currentVertCount;
            mesh.AddVert(a-side,tint,Vector2.zero);mesh.AddVert(a+side,tint,Vector2.zero);mesh.AddVert(b+side,tint,Vector2.zero);mesh.AddVert(b-side,tint,Vector2.zero);
            mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
        }
    }
}
