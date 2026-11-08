using System;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform MotorThumbnail(Transform parent, string modelId, float edge)
        {
            var portrait = Ui.Rect(parent, "VehiclePortrait");
            var size = Ui.Size(portrait, edge, edge, 0).GetComponent<LayoutElement>();
            size.minWidth = edge;
            var image = portrait.gameObject.AddComponent<RawImage>();
            image.texture = MotorPortraitArt.Get(modelId);
            image.raycastTarget = false;
            return portrait;
        }

        private void MotorIdentity(Transform parent, string modelId, string title, string subtitle = null)
        {
            var row = Ui.Row(parent, 72, 12);
            MotorThumbnail(row, modelId, 72);
            var text = Ui.Label(row, title + (string.IsNullOrEmpty(subtitle) ? "" : "\n" + subtitle), 17);
            Ui.Size(text, 0, 72, 1).GetComponent<LayoutElement>().minWidth = 0;
        }

        private RectTransform MotorCard(Transform parent, MotorModel model, string title = null)
        {
            var card = Ui.Panel(parent, "VehicleCard", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 8, 12);
            MotorIdentity(card, model.Id, title ?? model.Name);
            return card;
        }

        private void MotorChoice(Transform parent, string modelId, string title, Action choose,
            bool selected = false, bool enabled = true)
        {
            var button = Ui.Button(parent, title, choose, 17, selected ? Ui.ButtonHot : Ui.ButtonBg, enabled);
            Ui.Size(button, -1, 72);
            var portrait = MotorThumbnail(button.transform, modelId, 60);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0, .5f);
            portrait.pivot = new Vector2(0, .5f);
            portrait.anchoredPosition = new Vector2(6, 0);
            portrait.sizeDelta = new Vector2(60, 60);
            var label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.Anchor(0, 0, 1, 1, 80, 4, 10, 4);
        }
    }
}
