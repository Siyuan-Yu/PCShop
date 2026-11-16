using System;
using System.Linq;
using PCShopSell.Core.Common;
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
        internal static readonly Color BuyColor = Ui.Good, SellColor = new Color32(243, 156, 156, 255);
        private StockQuote[][] _series = Array.Empty<StockQuote[]>();
        private Color[] _colors = Array.Empty<Color>();
        private (long minute, long open, long high, long low, long close)[] _candles =
            Array.Empty<(long, long, long, long, long)>();
        private double? _cost;
        private double _low, _high;
        private long _firstMinute, _lastMinute;
        private bool _hasQuotes;
        private (long Minute, long Price, StockExecution[] Orders, bool HasBuy, bool HasSell)[] _tradeMarks =
            Array.Empty<(long, long, StockExecution[], bool, bool)>();

        public void Initialize(StockQuote[] quotes, double? cost, bool candlesticks, StockExecution[] executions)
        {
            _cost = cost;
            // Trades provide exact prices between published quotes, including after the last quote.
            var points = executions.Length == 0 ? quotes
                : quotes.Concat(executions.Select(e => new StockQuote { Minute = e.Minute, PriceCents = e.PriceCents }))
                .OrderBy(q => q.Minute).ToArray();
            if (candlesticks)
                _candles = points.GroupBy(q => q.Minute / 1440).Select(day =>
                    (day.Last().Minute, day.First().PriceCents, day.Max(q => q.PriceCents),
                        day.Min(q => q.PriceCents), day.Last().PriceCents)).ToArray();
            var candleMinutes = _candles.ToDictionary(candle => candle.minute / 1440, candle => candle.minute);
            _tradeMarks = executions.GroupBy(e =>
                    (Minute: candleMinutes.TryGetValue(e.Minute / 1440, out long candle) ? candle : e.Minute, Price: e.PriceCents))
                .Select(group => (group.Key.Minute, group.Key.Price, group.ToArray(), group.Any(IsBuy), group.Any(e => !IsBuy(e))))
                .ToArray();
            Initialize(new[] { points }, new[] { Ui.Accent });
            if (_tradeMarks.Length > 0) this.Tip(TradeTooltip);
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
            Rect plot = Plot();
            if (plot.width <= 0 || plot.height <= 0) return;
            float left = plot.xMin, right = plot.xMax;
            float Y(double cents) => PriceY(plot, cents);
            for (int i = 0; i < 3; i++)
            {
                float y = Mathf.Lerp(plot.yMin, plot.yMax, i / 2f);
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
                float width = CandleWidth(plot);
                foreach (var candle in _candles)
                {
                    float x = CandleX(plot, candle.minute);
                    var tint = candle.close < candle.open ? Ui.Warn : Ui.Good;
                    AssetTrendGraphic.Stroke(mesh, new Vector2(x, Y(candle.low)), new Vector2(x, Y(candle.high)), 1.5f, tint);
                    float open = Y(candle.open), close = Y(candle.close);
                    if (Mathf.Abs(open - close) < 1) close = open + 1;
                    AssetTrendGraphic.Stroke(mesh, new Vector2(x, open), new Vector2(x, close), width, tint);
                }
            }
            else for (int i = 0; i < _series.Length; i++)
            {
                var quotes = _series[i];
                var tint = _colors[i];
                var previous = Vector2.zero;
                for (int j = 0; j < quotes.Length; j++)
                {
                    var point = new Vector2(TimeX(plot, quotes[j].Minute), Y(quotes[j].PriceCents));
                    if (j > 0) AssetTrendGraphic.Stroke(mesh, previous, point, tint.a == 1 ? 2.3f : 1.35f, tint);
                    if (quotes.Length == 1)
                        AssetTrendGraphic.Stroke(mesh, point - Vector2.right * 2, point + Vector2.right * 2, 4, tint);
                    previous = point;
                }
            }
            foreach (var mark in _tradeMarks)
            {
                Vector2 point = TradePoint(plot, mark.Minute, mark.Price);
                float offset = mark.HasBuy && mark.HasSell ? 6 : 0;
                if (mark.HasBuy) TradeArrow(mesh, point - Vector2.right * offset, 1, BuyColor);
                if (mark.HasSell) TradeArrow(mesh, point + Vector2.right * offset, -1, SellColor);
            }
        }

        private Rect Plot()
        {
            var rect = rectTransform.rect;
            return Rect.MinMaxRect(rect.xMin + 84, rect.yMin + 28, rect.xMax - 12, rect.yMax - 12);
        }

        private float TimeX(Rect plot, long minute) => Mathf.Lerp(plot.xMin, plot.xMax, _lastMinute == _firstMinute
            ? .5f : (float)((minute - _firstMinute) / (double)(_lastMinute - _firstMinute)));
        private float PriceY(Rect plot, double cents) => Mathf.Lerp(plot.yMin, plot.yMax, (float)((cents - _low) / (_high - _low)));
        private float CandleWidth(Rect plot) => Mathf.Clamp(plot.width / (_candles.Length * 3), 4, 24);
        private float CandleX(Rect plot, long minute)
        {
            float half = CandleWidth(plot) / 2;
            return Mathf.Clamp(TimeX(plot, minute), plot.xMin + half, plot.xMax - half);
        }
        private Vector2 TradePoint(Rect plot, long minute, long cents) =>
            new Vector2(_candles.Length == 0 ? TimeX(plot, minute) : CandleX(plot, minute), PriceY(plot, cents));

        private static void TradeArrow(VertexHelper mesh, Vector2 center, float direction, Color tint)
        {
            void Triangle(float size, Color color)
            {
                int index = mesh.currentVertCount;
                mesh.AddVert(center + new Vector2(0, direction * size), color, Vector2.zero);
                mesh.AddVert(center + new Vector2(-size, -direction * size), color, Vector2.zero);
                mesh.AddVert(center + new Vector2(size, -direction * size), color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
            }
            Triangle(7, Ui.PanelBg);
            Triangle(5.5f, tint);
        }

        private string TradeTooltip()
        {
            var canvas = this.canvas;
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, camera, out var local)) return null;
            Rect plot = Plot();
            var nearby = _tradeMarks.Where(mark => (TradePoint(plot, mark.Minute, mark.Price) - local).sqrMagnitude <= 18 * 18)
                .SelectMany(mark => mark.Orders).OrderByDescending(e => e.Minute).Take(7).ToArray();
            if (nearby.Length == 0) return null;
            string result = string.Join("\n", nearby.Take(6).Select(ExecutionText));
            return nearby.Length <= 6 ? result : result + "\n其余成交可在「成交明细」查看";
        }

        internal static bool IsBuy(StockExecution trade) =>
            trade.Kind is StockExecutionKind.Buy or StockExecutionKind.Cover or StockExecutionKind.ForcedCover;

        internal static string ExecutionText(StockExecution trade)
        {
            string action = trade.Kind switch { StockExecutionKind.Buy => "买入", StockExecutionKind.Sell => "卖出",
                StockExecutionKind.Short => "做空", StockExecutionKind.Cover => "平仓", _ => "自动平仓" };
            string unit = trade.StockId == "hardware" ? "份" : "股";
            return $"{new GameTimestamp(trade.Minute)} · {action} {trade.Units:N0}{unit} · {Money.FromCents(trade.PriceCents)}/{unit}";
        }
    }
}
