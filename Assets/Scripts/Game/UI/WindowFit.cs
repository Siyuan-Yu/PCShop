using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>Keep initial and already-open menus fitted when the viewport changes.</summary>
    [ExecuteAlways]
    public sealed class WindowFit : MonoBehaviour
    {
        private RectTransform _rect;
        private void OnEnable()
        {
            Canvas.willRenderCanvases += Fit;
            Fit();
        }
        private void OnDisable() => Canvas.willRenderCanvases -= Fit;
        private void OnRectTransformDimensionsChange() => Fit();
        private void LateUpdate() => Fit();
        public void Fit()
        {
            if(!enabled)return;
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null || !(_rect.parent is RectTransform parent)) return;
            Vector2 wanted = _rect.rect.size;
            Vector2 available = parent.rect.size;
            if (wanted.x <= 0 || wanted.y <= 0 || available.x <= 0 || available.y <= 0) return;
            float scale = Mathf.Max(.25f, Mathf.Min(1f, Mathf.Min((available.x - 32) / wanted.x, (available.y - 32) / wanted.y)));
            if (Mathf.Abs(_rect.localScale.x - scale) > .0001f) _rect.localScale = Vector3.one * scale;
        }
    }
}
