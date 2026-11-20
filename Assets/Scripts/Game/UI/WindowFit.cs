using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>Keep initial and already-open menus fitted when the viewport changes.</summary>
    [ExecuteAlways]
    public sealed class WindowFit : MonoBehaviour
    {
        private RectTransform _rect;
        private RectTransform _content, _body, _illustration;
        private Vector2 _lastAvailable;
        private float _dialogWidth, _illustrationHeight;
        private bool _layingOut;

        public void FitContent(RectTransform content, RectTransform body, RectTransform illustration = null)
        {
            if (_content == null) _dialogWidth = ((RectTransform)transform).rect.width;
            _content = content;
            _body = body;
            _illustration = illustration;
            _illustrationHeight = illustration == null ? 0 : LayoutUtility.GetPreferredHeight(illustration);
            _lastAvailable = Vector2.zero;
            Fit();
        }

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
            if (!enabled || _layingOut) return;
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null || !(_rect.parent is RectTransform parent)) return;
            Vector2 available = parent.rect.size;
            if (_content != null && available.x > 32 && available.y > 32 && available != _lastAvailable)
            {
                _lastAvailable = available;
                _layingOut = true;
                try { LayoutDialog(available); }
                finally { _layingOut = false; }
            }
            Vector2 wanted = _rect.rect.size;
            if (wanted.x <= 0 || wanted.y <= 0 || available.x <= 0 || available.y <= 0) return;
            float scale = Mathf.Max(.25f, Mathf.Min(1f, Mathf.Min((available.x - 32) / wanted.x, (available.y - 32) / wanted.y)));
            if (Mathf.Abs(_rect.localScale.x - scale) > .0001f) _rect.localScale = Vector3.one * scale;
        }

        private void LayoutDialog(Vector2 available)
        {
            float chromeHeight = _rect.rect.height - _body.rect.height;
            float maxWidth = available.x - 32;
            float maxHeight = Mathf.Max(1, available.y - 32 - 2 * Mathf.Abs(_rect.anchoredPosition.y) - chromeHeight);
            float width = Mathf.Min(_dialogWidth, maxWidth);
            if (_illustration != null) Ui.Size(_illustration, -1, _illustrationHeight);
            float preferred = Measure(width);

            // Keep the text and buttons at their original size. Give wrapped text more room first.
            float wider = Mathf.Min(maxWidth, _dialogWidth + 320);
            if (preferred > maxHeight && wider > width)
            {
                float widerHeight = Measure(wider);
                if (widerHeight < preferred)
                {
                    width = wider;
                    preferred = widerHeight;
                }
                else Measure(width);
            }
            if (_illustration != null && preferred > maxHeight)
            {
                float minimum = Mathf.Min(160, _illustrationHeight);
                float height = Mathf.Max(minimum, _illustrationHeight - (preferred - maxHeight));
                Ui.Size(_illustration, -1, height);
                float shorter = Measure(width);
                if (shorter < preferred) preferred = shorter;
                else
                {
                    Ui.Size(_illustration, -1, _illustrationHeight);
                    preferred = Measure(width);
                }
            }

            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, chromeHeight + Mathf.Min(preferred, maxHeight));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            if (_body.TryGetComponent<ScrollRect>(out var scroll))
            {
                scroll.vertical = preferred > maxHeight;
                if (!scroll.vertical)
                {
                    scroll.StopMovement();
                    _content.anchoredPosition = Vector2.zero;
                }
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(scroll.vertical);
            }

            float Measure(float measureWidth)
            {
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, measureWidth);
                _rect.ForceUpdateRectTransforms();
                _content.ForceUpdateRectTransforms();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                return Mathf.Ceil(LayoutUtility.GetPreferredHeight(_content));
            }
        }
    }
}
