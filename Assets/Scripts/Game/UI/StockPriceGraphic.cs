using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockPriceGraphic : MaskableGraphic
    {
        internal static readonly Color CostColor = new Color32(160, 208, 238, 255);
        private StockQuote[][] _series = Array.Empty<StockQuote[]>();
        private Color[] _colors = Array.Empty<Color>();
        private (long minute, long open, long high, long low, long close)[] _candles =
            Array.Empty<(long, long, long, long, long)>();
        private double? _cost;
        private double _low, _high;
        private long _firstMinute, _lastMinute;
        private bool _hasQuotes;

        public void Initialize(StockQuote[] quotes, double? cost, bool candlesticks)
        {
            _cost = cost;
            if (candlesticks)
                _candles = quotes.GroupBy(q => q.Minute / 1440).Select(day =>
                    (day.Last().Minute, day.First().PriceCents, day.Max(q => q.PriceCents),
                        day.Min(q => q.PriceCents), day.Last().PriceCents)).ToArray();
            Initialize(new[] { quotes }, new[] { Ui.Accent });
        }

        public void Initialize(StockQuote[][] series, Color[] colors)
        {
            _series = series;
            _colors = colors;
            raycastTarget = false;
            var points = series.SelectMany(quotes => quotes).ToArray();
            _hasQuotes = points.Length > 0;
            if (!_hasQuotes) return;
            _firstMinute = points.Min(q => q.Minute);
            _lastMinute = points.Max(q => q.Minute);
            double low = points.Min(q => q.PriceCents), high = points.Max(q => q.PriceCents);
            if (_cost.HasValue)
            {
                low = Math.Min(low, _cost.Value);
                high = Math.Max(high, _cost.Value);
            }
            double pad = Math.Max(10, (high - low) / 12);
            _low = Math.Max(0, low - pad);
            _high = high + pad;
            for (int i = 0; i < 3; i++)
            {
                float fraction = i / 2f;
                var label = Ui.Label(transform, ((_low + (_high - _low) * fraction) / 100).ToString("N2"),
                    12, TextAnchor.MiddleRight, Ui.Dim);
                label.raycastTarget = false;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, fraction);
                label.rectTransform.pivot = new Vector2(1, .5f);
                label.rectTransform.anchoredPosition = new Vector2(74, Mathf.Lerp(28, -12, fraction));
                label.rectTransform.sizeDelta = new Vector2(73, 20);
            }
            AddTimeLabel(_firstMinute, 0, 105);
            if (_lastMinute != _firstMinute) AddTimeLabel(_lastMinute, 1, -60);
            SetVerticesDirty();
        }

        private void AddTimeLabel(long time, int anchor, int offset)
        {
            var date = new GameDate((int)(time / 1440));
            string text = $"{date.Month}/{date.Day}";
            if (_candles.Length == 0) text += " " + GameSession.ClockText((int)(time % 1440));
            var label = Ui.Label(transform, text, 12, TextAnchor.MiddleCenter, Ui.Dim);
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(anchor, 0);
            label.rectTransform.pivot = new Vector2(.5f, 0);
            label.rectTransform.anchoredPosition = new Vector2(offset, 0);
            label.rectTransform.sizeDelta = new Vector2(110, 22);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!_hasQuotes) return;
            var rect = rectTransform.rect;
            float left = rect.xMin + 84, right = rect.xMax - 12, bottom = rect.yMin + 28, top = rect.yMax - 12;
            if (right <= left || top <= bottom) return;
            float X(long minute) => Mathf.Lerp(left, right, _lastMinute == _firstMinute ? .5f
                : (float)((minute - _firstMinute) / (double)(_lastMinute - _firstMinute)));
            float Y(double cents) => Mathf.Lerp(bottom, top, (float)((cents - _low) / (_high - _low)));
            for (int i = 0; i < 3; i++)
            {
                float y = Mathf.Lerp(bottom, top, i / 2f);
                AssetTrendGraphic.Stroke(mesh, new Vector2(left, y), new Vector2(right, y), 1, new Color(.6f, .65f, .7f, .2f));
            }
            if (_cost.HasValue)
            {
                float y = Y(_cost.Value);
                for (float x = left; x < right; x += 13)
                    AssetTrendGraphic.Stroke(mesh, new Vector2(x, y), new Vector2(Mathf.Min(x + 8, right), y), 1.4f, CostColor);
            }
            if (_candles.Length > 0)
            {
                float width = Mathf.Clamp((right - left) / (_candles.Length * 3), 4, 24);
                foreach (var candle in _candles)
                {
                    float x = Mathf.Clamp(X(candle.minute), left + width / 2, right - width / 2);
                    var tint = candle.close < candle.open ? Ui.Warn : Ui.Good;
                    AssetTrendGraphic.Stroke(mesh, new Vector2(x, Y(candle.low)), new Vector2(x, Y(candle.high)), 1.5f, tint);
                    float open = Y(candle.open), close = Y(candle.close);
                    if (Mathf.Abs(open - close) < 1) close = open + 1;
                    AssetTrendGraphic.Stroke(mesh, new Vector2(x, open), new Vector2(x, close), width, tint);
                }
                return;
            }
            for (int i = 0; i < _series.Length; i++)
            {
                var quotes = _series[i];
                var tint = _colors[i];
                var previous = Vector2.zero;
                for (int j = 0; j < quotes.Length; j++)
                {
                    var point = new Vector2(X(quotes[j].Minute), Y(quotes[j].PriceCents));
                    if (j > 0) AssetTrendGraphic.Stroke(mesh, previous, point, tint.a == 1 ? 2.3f : 1.35f, tint);
                    if (quotes.Length == 1)
                        AssetTrendGraphic.Stroke(mesh, point - Vector2.right * 2, point + Vector2.right * 2, 4, tint);
                    previous = point;
                }
            }
        }
    }
}
