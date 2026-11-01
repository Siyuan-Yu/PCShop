using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private readonly Dictionary<string, int> _onlineQuantities = new Dictionary<string, int>();

        private void QuantityStepper(Window w, Transform parent, string key, double value, double min, double max, double step,
            Action<double> set, Func<double, string> display = null, bool canAdd = true)
        {
            display = display ?? (n => n.ToString("0.##"));
            var row = Ui.Row(parent, 36, 4); row.name = "Quantity_" + key; Ui.Size(row, 184, 36);
            Text quantity = null;
            void Button(string label, int direction, double room, bool enabled)
            {
                var button = Ui.Size(Ui.Button(row, label, null, 21, Ui.ButtonBg, enabled && room > 1e-9), 38, 36);
                button.name = "Quantity" + (direction > 0 ? "Plus_" : "Minus_") + key;
                var hold = button.gameObject.AddComponent<HoldRepeat>();
                hold.Owner=w;hold.Key=button.name;
                hold.Max = Math.Max(1, (int)Math.Ceiling(room / step - 1e-9));
                hold.Label = n => { if (quantity != null) quantity.text = display(Math.Max(min, Math.Min(max, value + direction * n * step))); return label; };
                hold.Commit = n => { set(Math.Max(min, Math.Min(max, value + direction * n * step))); w.RequestRebuild(); };
                button.onClick.AddListener(hold.ClickOnce);
                button.Tip("点一下调整一档；按住连续调整，越按越快。按下立即应用。");
            }
            Button("−", -1, value - min, true);
            quantity = Ui.Size(Ui.Label(row, display(value), 16, TextAnchor.MiddleCenter), 100, 36);
            quantity.name = "QuantityValue_" + key;
            Button("+", 1, max - value, canAdd);
        }

        private void ShopQuantity(Window w, Transform row, string key, Func<bool> add, bool enabled,double? maxAmount=null)
        {
            if (row.Find("Quantity_" + key) != null) return;
            double amount = S.CartAmount(key), step = key == "rice" ? .25 : 1;
            double max = maxAmount??1000;
            if (key.StartsWith("part:")) max = Math.Min(1000, S.StallRemaining(S.CurrentPlace, PartCatalog.Get(key.Substring(5))));
            QuantityStepper(w, row, key, amount, 0, max, step, value =>
            {
                double delta = value - S.CartAmount(key);
                if (delta < 0) S.RemoveFromCart(key, -delta);
                else if (key == "rice") S.CartAddRice(delta);
                else for (int i = 0; i < (int)Math.Round(delta); i++) if (!add()) break;
            }, n => key == "rice" ? n.ToString("0.##") + "千克" : key.StartsWith("ing:")
                ? (n * Backpack.IngredientKg(key.Substring(4))).ToString("0.##") + "千克" : n.ToString("0"), enabled);
        }

        private int OnlineQuantity(Window w, Transform row, string key)
        {
            int amount = _onlineQuantities.TryGetValue(key, out int n) ? n : 1;
            QuantityStepper(w, row, "online_" + key, amount, 1, 100, 1, value => _onlineQuantities[key] = (int)value);
            return amount;
        }
    }
}
