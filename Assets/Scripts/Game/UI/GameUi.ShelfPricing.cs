using System;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static string PurchaseEstimateText(double chance)
        {
            if (chance >= 1) return "100%";
            if (chance > .99) return "99%+";
            if (chance > 0 && chance < .01) return "不足1%";
            return chance.ToString("0%");
        }

        private RectTransform ShelfSaleTag(Window w, Transform parent, string key, long price, (long min, long max) range,
            Func<long, double> intentAtPrice, Action<long> setPrice, PCShopSell.Core.Shop.ShopSimulation pricingShop=null)
        {
            var shop=pricingShop??S.PlayerShop;
            var tag = Ui.FlatPanel(parent, "PriceTag_" + key, Ui.FlatWell, Ui.FlatWellEdge);
            var valueLabel = Ui.Label(tag.transform, "¥" + price, 18, TextAnchor.MiddleCenter, Ui.Accent);
            valueLabel.fontStyle = FontStyle.Bold;
            valueLabel.name = "SalePrice_" + key;
            valueLabel.rectTransform.Anchor(.18f, .43f, .61f, .98f);
            long draft = price;
            double MirrorBonus()=>key.StartsWith("goods_")?shop.GoodsMirrorBonus(key.Substring(6))*100:0;
            double RestaurantBonus()=>key.StartsWith("goods_")?shop.RestaurantDrinkPurchaseIntentBonus(key.Substring(6))*100:0;
            Text demand = null;
            Button confirm = null;
            var controls = new System.Collections.Generic.List<(Button button, HoldRepeat hold, int direction)>();
            long Clamp(long value) => Math.Max(range.min, Math.Min(range.max, value));
            void Preview(long value)
            {
                valueLabel.text = "¥" + value;
                if (demand != null)
                {
                    string estimate = PurchaseEstimateText(intentAtPrice(value));
                    double bonus=shop.SalesPurchaseIntentBonus*100;
                    double mirror=MirrorBonus();
                    double restaurant=RestaurantBonus();
                    demand.text=restaurant>0?$"购买意愿 {estimate} · 餐馆+{restaurant:0}"
                        :mirror>0
                        ?$"购买意愿 {estimate} · {(bonus>0?"加成":"镜子")}+{bonus+mirror:0.#}"
                        :bonus>0?$"购买意愿 {estimate} · 销售+{bonus:0.#}":$"购买意愿约 {estimate}";
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
                var button = Ui.Button(tag.transform, label, null, 18, Ui.ButtonBg);
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
            confirm = Ui.Button(tag.transform, "确定", () => { setPrice(draft); w.RequestRebuild(); }, 13, Ui.ButtonHot, false);
            confirm.name = "ConfirmPrice_" + key;
            confirm.Tip("确认这个商品的当前售价。");
            ((RectTransform)confirm.transform).Anchor(.8f, .43f, .99f, .98f);
            demand = Ui.Label(tag.transform, "", 13, TextAnchor.MiddleCenter, Ui.Dim);
            demand.name = "PurchaseIntent_" + key;
            tag.Tip(()=>"购买意愿计入当前有效加成，手办和服装还会考虑顾客预算。\n"+
                $"在岗销售员：+{shop.SalesPurchaseIntentBonus*100:0.#}个百分点。"+
                (MirrorBonus()>0?"\n全身镜：服装购买意愿 +5个百分点，多面不叠加。":"")+
                (RestaurantBonus()>0?"\n餐馆：饮料购买意愿 +20个百分点，总意愿最高100%。\n堂食顾客结账时有机会加购一份已陈列饮料，按当前售价成交。":""));
            demand.rectTransform.Anchor(.02f, .02f, .98f, .4f);
            Preview(draft); RefreshLimits();
            return tag.rectTransform;
        }
    }
}
