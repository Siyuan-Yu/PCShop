using PCShopSell.Core.Session;
using static PCShopSell.Core.Session.EventScratchTicket;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScratchTicketGraphic : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public GameSession Session;
        public System.Action Changed;
        private Vector2Int? _previousCell;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var ticket = Session?.ScratchTicket; if (ticket == null || ticket.Paid) return;
            var r = rectTransform.rect; float width = r.width / Columns, height = r.height / Rows;
            for (int i = 0; i < CellCount; i++)
            {
                if (ticket.Cleared[i]) continue;
                float x = r.xMin + i % Columns * width, y = r.yMin + i / Columns * height;
                int first = vh.currentVertCount; Color32 c = new Color(.64f, .67f, .70f, 1);
                vh.AddVert(new Vector3(x, y), c, Vector2.zero); vh.AddVert(new Vector3(x + width, y), c, Vector2.zero);
                vh.AddVert(new Vector3(x + width, y + height), c, Vector2.zero); vh.AddVert(new Vector3(x, y + height), c, Vector2.zero);
                vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
            }
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _previousCell = null;
            Scratch(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) _previousCell = null;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Scratch(e);
        }

        private void Scratch(PointerEventData e)
        {
            var r = rectTransform.rect;
            if (Session == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                e.position, e.pressEventCamera, out var p) || !r.Contains(p))
            {
                _previousCell = null;
                return;
            }

            var point = new Vector2Int(
                Mathf.Clamp((int)((p.x - r.xMin) / r.width * Columns), 0, Columns - 1),
                Mathf.Clamp((int)((p.y - r.yMin) / r.height * Rows), 0, Rows - 1));
            var start = _previousCell ?? point;
            _previousCell = point;
            int steps = Mathf.Max(Mathf.Abs(point.x - start.x), Mathf.Abs(point.y - start.y));
            bool changed = false;
            for (int i = 0; i <= steps; i++)
            {
                // Trace the space between pointer samples so fast drags erase a continuous stroke.
                float t = steps == 0 ? 0 : i / (float)steps;
                int col = Mathf.RoundToInt(Mathf.Lerp(start.x, point.x, t));
                int row = Mathf.RoundToInt(Mathf.Lerp(start.y, point.y, t));
                for (int y = Mathf.Max(0, row - 1); y <= Mathf.Min(Rows - 1, row + 1); y++)
                    for (int x = Mathf.Max(0, col - 1); x <= Mathf.Min(Columns - 1, col + 1); x++)
                        changed |= Session.ScratchEventCell(y * Columns + x);
            }
            if (!changed) return;
            SetVerticesDirty();
            Changed?.Invoke();
        }
    }
}
