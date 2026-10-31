using System;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform ShelfSaleTag(Window w, Transform parent, string key, long price, (long min, long max) range,
            double intent, bool own, Action<long> setPrice, Action follow)
        {
            var tag = Ui.Panel(parent, "PriceTag_" + key, TagPaper);
            var valueLabel = Ui.Label(tag.transform, "¥" + price, 17, TextAnchor.MiddleCenter, TagInk);
            valueLabel.name = "SalePrice_" + key;
            valueLabel.rectTransform.Anchor(.18f, .43f, .61f, .98f);
            void PriceButton(string label, int direction, float x0, float x1)
            {
                bool enabled = direction > 0 ? price < range.max : price > range.min;
                var button = Ui.Button(tag.transform, label, null, 20, WoodLight, enabled);
                button.name = "Price" + (direction > 0 ? "Plus_" : "Minus_") + key;
                ((RectTransform)button.transform).Anchor(x0, .43f, x1, .98f);
                var hold = button.gameObject.AddComponent<HoldRepeat>();
                hold.Max = (int)Math.Min(int.MaxValue, direction > 0 ? range.max - price : price - range.min);
                hold.Label = n => { valueLabel.text = "¥" + (price + direction * n); return label; };
                hold.Commit = n => { setPrice(price + direction * n); w.RequestRebuild(); };
                button.onClick.AddListener(hold.ClickOnce);
                button.Tip("点一下调整1元；长按连续调价，越按越快，松开应用。购买意愿随售价更新。");
            }
            PriceButton("−", -1, .01f, .17f);
            PriceButton("+", 1, .62f, .78f);
            var reset = Ui.Button(tag.transform, "跟随", () => { follow(); w.RequestRebuild(); }, 11, WoodLight, own);
            reset.name = "FollowPrice_" + key;
            ((RectTransform)reset.transform).Anchor(.8f, .43f, .99f, .98f);
            var demand = Ui.Label(tag.transform, $"购买意愿约 {Math.Max(0, Math.Min(1, intent)) * 100:0}%", 13, TextAnchor.MiddleCenter, TagInk);
            demand.name = "PurchaseIntent_" + key;
            demand.rectTransform.Anchor(.02f, .02f, .98f, .4f);
            return tag.rectTransform;
        }
    }
}
