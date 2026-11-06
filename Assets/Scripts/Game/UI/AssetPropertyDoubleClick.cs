using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PCShopSell.Game.UI
{
    /// <summary>List selection stays intact between clicks so Unity can detect a true double-click.</summary>
    public sealed class AssetPropertyDoubleClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Focus;
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button==PointerEventData.InputButton.Left&&data.clickCount>=2)Focus?.Invoke();
        }
    }
}
