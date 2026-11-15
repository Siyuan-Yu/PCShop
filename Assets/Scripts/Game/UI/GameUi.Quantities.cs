using System;
using System.Collections.Generic;
using System.Globalization;
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
            Action<double> set, bool canAdd = true, string unit = null, double displayScale = 1, Func<double> readValue = null)
        {
            double current = value;
            double limit = canAdd ? Math.Max(value, max) : value;
            string Format() => (current * displayScale).ToString("0.###", CultureInfo.InvariantCulture);
            var row = Ui.Row(parent, 36, 4); row.name = "Quantity_" + key; Ui.Size(row, 184, 36);
            (Button button, HoldRepeat repeat) less = default, more = default;
            InputField quantity = null;
            less = AddButton("−", -1);
            quantity = Ui.Size(Ui.TextInput(row, Format(), "数量", null), unit == null ? 100 : 66, 36);
            quantity.name = "QuantityValue_" + key;
            quantity.textComponent.alignment = TextAnchor.MiddleCenter;
            quantity.placeholder.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            double displayStep = step * displayScale;
            quantity.contentType = Math.Abs(displayStep - Math.Round(displayStep)) < 1e-9
                ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
            if (unit != null) Ui.Size(Ui.Label(row, unit, 14, TextAnchor.MiddleCenter, Ui.Dim), 30, 36);
            more = AddButton("+", 1);
            quantity.Tip("可直接输入，回车或点到别处应用数量。");
            quantity.onEndEdit.AddListener(text =>
            {
                if (w.Closed) return;
                if (!quantity.wasCanceled && double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                    | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out double number)
                    && !double.IsNaN(number) && !double.IsInfinity(number))
                {
                    double target = Math.Max(min, Math.Min(limit, number / displayScale));
                    Apply(Math.Round(target / step, MidpointRounding.AwayFromZero) * step, true);
                }
                else quantity.SetTextWithoutNotify(Format());
            });
            RefreshButtons();

            (Button button, HoldRepeat repeat) AddButton(string label, int direction)
            {
                var button = Ui.Size(Ui.Button(row, label, null, 21, Ui.ButtonBg), 38, 36);
                button.name = "Quantity" + (direction > 0 ? "Plus_" : "Minus_") + key;
                var hold = button.gameObject.AddComponent<HoldRepeat>();
                hold.Owner = w;
                hold.Key = button.name;
                hold.Commit = n => Apply(current + direction * n * step, false);
                button.onClick.AddListener(hold.ClickOnce);
                button.Tip("点一下调整一档；按住连续调整，越按越快。按下立即应用。");
                return (button, hold);
            }
            void RefreshButtons()
            {
                less.button.interactable = current > min + 1e-9;
                more.button.interactable = canAdd && current < limit - 1e-9;
                less.repeat.Max = less.button.interactable ? 1 : 0;
                more.repeat.Max = more.button.interactable ? 1 : 0;
            }
            void Apply(double target, bool fromInput)
            {
                double next = Math.Max(min, Math.Min(limit, target));
                if (!canAdd) next = Math.Min(current, next);
                if (Math.Abs(next - current) > 1e-9)
                {
                    set(next);
                    current = readValue?.Invoke() ?? next;
                    w.RequestQuantityRebuild(fromInput);
                }
                quantity.SetTextWithoutNotify(Format());
                RefreshButtons();
            }
        }

        private void ShopQuantity(Window w, Transform row, string key, Func<bool> add, bool enabled,double? maxAmount=null)
        {
            if (row.Find("Quantity_" + key) != null) return;
            double amount = S.CartAmount(key), step = key == "rice" ? .25 : 1;
            double max = maxAmount??1000;
            if (maxAmount == null && key.StartsWith("part:"))
            {
                var stall = S.CurrentPlace;
                var part = PartCatalog.Get(key.Substring(5));
                if (stall != null && part != null) max = Math.Min(1000, S.StallRemaining(stall, part));
            }
            bool ingredient = key.StartsWith("ing:");
            QuantityStepper(w, row, key, amount, 0, max, step, value =>
            {
                double delta = value - S.CartAmount(key);
                if (delta < 0) S.RemoveFromCart(key, -delta);
                else if (key == "rice") S.CartAddRice(delta);
                else for (int i = 0; i < (int)Math.Round(delta); i++) if (!add()) break;
            }, canAdd: enabled, unit: key == "rice" || ingredient ? "千克" : null,
                displayScale: ingredient ? Backpack.IngredientKg(key.Substring(4)) : 1,
                readValue: () => S.CartAmount(key));
        }

        private int OnlineQuantity(Window w, Transform row, string key)
        {
            int amount = _onlineQuantities.TryGetValue(key, out int n) ? n : 1;
            QuantityStepper(w, row, "online_" + key, amount, 1, 100, 1, value => _onlineQuantities[key] = (int)value);
            return amount;
        }
    }
}
