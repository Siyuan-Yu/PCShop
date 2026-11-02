using System;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform ShelfSaleTag(Window w, Transform parent, string key, long price, (long min, long max) range,
            Func<long, double> intentAtPrice, Action<long> setPrice)
        {
            var tag = Ui.Panel(parent, "PriceTag_" + key, TagPaper);
            var valueLabel = Ui.Label(tag.transform, "¥" + price, 17, TextAnchor.MiddleCenter, TagInk);
            valueLabel.name = "SalePrice_" + key;
            valueLabel.rectTransform.Anchor(.18f, .43f, .61f, .98f);
            long draft = price;
            double MirrorBonus()=>key.StartsWith("goods_")?S.PlayerShop.GoodsMirrorBonus(key.Substring(6))*100:0;
            Text demand = null;
            Button confirm = null;
            var controls = new System.Collections.Generic.List<(Button button, HoldRepeat hold, int direction)>();
            long Clamp(long value) => Math.Max(range.min, Math.Min(range.max, value));
            void Preview(long value)
            {
                valueLabel.text = "¥" + value;
                if (demand != null)
                {
                    double chance=Math.Max(0,Math.Min(1,intentAtPrice(value)))*100;
                    double bonus=S.PlayerShop.SalesPurchaseIntentBonus*100;
                    double mirror=MirrorBonus();
                    demand.text=mirror>0
                        ?$"购买意愿 {chance:0}% · {(bonus>0?"加成":"镜子")}+{bonus+mirror:0.#}"
                        :bonus>0?$"购买意愿 {chance:0}% · 销售+{bonus:0.#}":$"购买意愿约 {chance:0}%";
                }
                if (confirm != null) confirm.interactable = value != price;
            }
            void RefreshLimits()
            {
                foreach (var control in controls)
                {
                    long room = control.direction > 0 ? range.max - draft : draft - range.min;
                    control.hold.Max = (int)Math.Min(int.MaxValue, room);
                    control.button.interactable = room > 0;
                }
            }
            void PriceButton(string label, int direction, float x0, float x1)
            {
                var button = Ui.Button(tag.transform, label, null, 20, WoodLight);
                button.name = "Price" + (direction > 0 ? "Plus_" : "Minus_") + key;
                ((RectTransform)button.transform).Anchor(x0, .43f, x1, .98f);
                var hold = button.gameObject.AddComponent<HoldRepeat>();
                hold.Owner=w;hold.Key=button.name;
                hold.Label = n => { Preview(Clamp(draft + direction * n)); return label; };
                hold.Commit = n => { draft = Clamp(draft + direction * n); Preview(draft); RefreshLimits(); };
                controls.Add((button, hold, direction));
                button.onClick.AddListener(hold.ClickOnce);
                button.Tip("点一下调整1元；长按连续调价，购买意愿实时预览。点确定后生效。");
            }
            PriceButton("−", -1, .01f, .17f);
            PriceButton("+", 1, .62f, .78f);
            confirm = Ui.Button(tag.transform, "确定", () => { setPrice(draft); w.RequestRebuild(); }, 11, Ui.ButtonHot, false);
            confirm.name = "ConfirmPrice_" + key;
            confirm.GetComponentInChildren<Text>().color = Ui.TextColor;
            confirm.Tip("确认这个商品的当前售价。");
            ((RectTransform)confirm.transform).Anchor(.8f, .43f, .99f, .98f);
            demand = Ui.Label(tag.transform, "", 13, TextAnchor.MiddleCenter, TagInk);
            demand.name = "PurchaseIntent_" + key;
            tag.Tip(()=>"购买意愿已计入当前有效加成。\n"+
                $"在岗销售员：+{S.PlayerShop.SalesPurchaseIntentBonus*100:0.#}个百分点。"+
                (MirrorBonus()>0?"\n全身镜：服装购买意愿 +5个百分点，多面不叠加。":""));
            demand.rectTransform.Anchor(.02f, .02f, .98f, .4f);
            Preview(draft); RefreshLimits();
            return tag.rectTransform;
        }
    }
}
