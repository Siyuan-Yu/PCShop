using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PCShopSell.Game.UI
{
    /// <summary>悬停说明的触发点：鼠标移上来记下自己，由 <see cref="GameUi"/> 每帧显示说明。</summary>
    public sealed class TipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static TipTarget Hover;
        public Func<string> Text;

        public void OnPointerEnter(PointerEventData eventData) => Hover = this;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Hover == this) Hover = null;
        }

        private void OnDisable()
        {
            if (Hover == this) Hover = null;
        }
    }
}