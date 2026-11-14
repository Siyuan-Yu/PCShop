using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScratchTicketGraphic : MaskableGraphic, IPointerDownHandler, IDragHandler
    {
        public GameSession Session;
        public System.Action Changed;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var ticket = Session?.ScratchTicket; if (ticket == null || ticket.Paid) return;
            var r = rectTransform.rect; float width = r.width / 18, height = r.height / 8;
            for (int i = 0; i < 144; i++)
            {
                if (ticket.Cleared[i]) continue;
                float x = r.xMin + i % 18 * width, y = r.yMin + i / 18 * height;
                int first = vh.currentVertCount; Color32 c = new Color(.64f, .67f, .70f, 1);
                vh.AddVert(new Vector3(x, y), c, Vector2.zero); vh.AddVert(new Vector3(x + width, y), c, Vector2.zero);
                vh.AddVert(new Vector3(x + width, y + height), c, Vector2.zero); vh.AddVert(new Vector3(x, y + height), c, Vector2.zero);
                vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
            }
        }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Scratch(e); }
        public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Scratch(e); }
        private void Scratch(PointerEventData e)
        {
            if (Session == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out var p)) return;
            var r = rectTransform.rect; if (!r.Contains(p)) return;
            int col = Mathf.Clamp((int)((p.x - r.xMin) / r.width * 18), 0, 17), row = Mathf.Clamp((int)((p.y - r.yMin) / r.height * 8), 0, 7);
            bool changed = false;
            for (int y = Mathf.Max(0, row - 1); y <= Mathf.Min(7, row + 1); y++)
                for (int x = Mathf.Max(0, col - 1); x <= Mathf.Min(17, col + 1); x++) changed |= Session.ScratchEventCell(y * 18 + x);
            if (changed) { SetVerticesDirty(); Changed?.Invoke(); }
        }
    }
}
