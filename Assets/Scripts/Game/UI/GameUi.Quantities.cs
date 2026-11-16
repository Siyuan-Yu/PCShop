using System;
using System.Collections.Generic;
using System.Globalization;
using PCShopSell.Core.Catalog;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    internal sealed class PurchaseAmountSlider : Slider
    {
        public bool Previewing { get; private set; }
        public Action<float> Commit;

        public override void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
            {
                GetComponentInParent<ScrollRect>()?.StopMovement();
                Previewing = true;
            }
            base.OnPointerDown(e);
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (e.button != PointerEventData.InputButton.Left || !Previewing) return;
            Previewing = false;
            Commit?.Invoke(value);
        }

        protected override void OnDisable()
        {
            Previewing = false;
            base.OnDisable();
        }
    }

    public sealed partial class GameUi
    {
        private readonly Dictionary<string, int> _onlineQuantities = new Dictionary<string, int>();

        private Action<double> QuantityStepper(Window w, Transform parent, string key, double value, double min, double max, double step,
            Action<double> set, bool canAdd = true, string unit = null, double displayScale = 1, Func<double> readValue = null,
            Action<double> changed = null, int trackWidth = 0, int minimumOrder = 1)
        {
            double current = value;
            double limit = canAdd ? Math.Max(value, max) : value;
            string Format(double target) => (target * displayScale).ToString("0.###", CultureInfo.InvariantCulture);
            var row = Ui.Row(parent, 36, 4);
            row.name = "Quantity_" + key;
            Ui.Size(row, trackWidth == 0 ? 184 : trackWidth + 112, 36);
            (Button button, HoldRepeat repeat) less = default, more = default;
            InputField quantity = null;
            PurchaseAmountSlider slider = null;
            // For wholesale orders, the first notch after zero is the minimum order.
            double gap = min == 0 ? Math.Max(0, Math.Min(minimumOrder, limit) - 1) : 0;
            float Position(double count) => count <= 0 ? 0 : (float)Math.Max(1, count - gap);
            double Amount(float position) => position <= 0 ? 0 : position + gap;
            if (trackWidth > 0)
            {
                var range = Ui.Size(Ui.Rect(row, "QuantityRange"), trackWidth, 36, 0);
                Ui.Label(range, Format(min), 11, TextAnchor.MiddleLeft, Ui.Dim).rectTransform.Anchor(0, 0, .5f, 0, 2, 0, 0, -13);
                Ui.Label(range, Format(limit), 11, TextAnchor.MiddleRight, Ui.Dim).rectTransform.Anchor(.5f, 0, 1, 0, 0, 0, 2, -13);
                slider = Ui.Slider<PurchaseAmountSlider>(range, (float)min, Position(limit), Position(current), position =>
                {
                    if (slider.Previewing) quantity.SetTextWithoutNotify(Format(Amount(position)));
                    else Apply(Amount(position), true);
                }, wholeNumbers: true);
                slider.name = "QuantitySlider_" + key;
                ((RectTransform)slider.transform).Anchor(0, 0, 1, 1, 0, 12, 0, 0);
                slider.Commit = position => Apply(Amount(position), true);
                slider.gameObject.AddComponent<Image>().color = Color.clear;
                ((RectTransform)slider.handleRect.parent).Anchor(0, 0, 1, 1, 8, 3, 8, 3);
                slider.handleRect.sizeDelta = new Vector2(14, 0);
                slider.Tip($"拖动选择数量，松开应用。范围 {Format(min)}–{Format(limit)}。"
                    + (minimumOrder > 1 ? $" {minimumOrder} 件起订，滑回 0 可移除。" : ""));
            }
            less = AddButton("−", -1);
            quantity = Ui.Size(Ui.TextInput(row, Format(current), "数量", null), trackWidth > 0 ? 44 : unit == null ? 100 : 66, 36);
            quantity.name = "QuantityValue_" + key;
            quantity.textComponent.alignment = TextAnchor.MiddleCenter;
            var placeholder = quantity.placeholder.GetComponent<Text>();
            placeholder.alignment = TextAnchor.MiddleCenter;
            if (trackWidth > 0)
            {
                quantity.textComponent.rectTransform.Anchor(0, 0, 1, 1, 4, 7, 4, 7);
                placeholder.rectTransform.Anchor(0, 0, 1, 1, 4, 7, 4, 7);
            }
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
                else quantity.SetTextWithoutNotify(Format(current));
            });
            RefreshControls();
            return target => Apply(target, false);

            (Button button, HoldRepeat repeat) AddButton(string label, int direction)
            {
                var button = Ui.Size(Ui.Button(row, label, null, trackWidth > 0 ? 17 : 21, Ui.ButtonBg), trackWidth > 0 ? 28 : 38, 36);
                button.name = "Quantity" + (direction > 0 ? "Plus_" : "Minus_") + key;
                var hold = button.gameObject.AddComponent<HoldRepeat>();
                hold.Owner = w;
                hold.Key = button.name;
                hold.Commit = n => Apply(trackWidth > 0 && direction < 0 && min == 0 && current <= minimumOrder
                    ? 0 : current + direction * n * step, false);
                button.onClick.AddListener(hold.ClickOnce);
                button.Tip("点一下调整一档；按住连续调整，越按越快。按下立即应用。");
                return (button, hold);
            }
            void RefreshControls()
            {
                less.button.interactable = current > min + 1e-9;
                more.button.interactable = canAdd && current < limit - 1e-9;
                less.repeat.Max = less.button.interactable ? 1 : 0;
                more.repeat.Max = more.button.interactable ? 1 : 0;
                if (slider != null)
                {
                    slider.SetValueWithoutNotify(Position(current));
                    slider.interactable = less.button.interactable || more.button.interactable;
                }
            }
            void Apply(double target, bool waitForPointer)
            {
                if (w.Closed) return;
                double next = Math.Max(min, Math.Min(limit, target));
                if (trackWidth > 0 && next > 0 && next < minimumOrder) next = limit >= minimumOrder ? minimumOrder : 0;
                if (!canAdd) next = Math.Min(current, next);
                if (Math.Abs(next - current) > 1e-9)
                {
                    set(next);
                    current = readValue?.Invoke() ?? next;
                    changed?.Invoke(current);
                    w.RequestQuantityRebuild(waitForPointer);
                }
                quantity.SetTextWithoutNotify(Format(current));
                RefreshControls();
            }
        }

        private Action<double> PurchaseQuantity(Window w, Transform row, string key, double amount, double min, double max,
            Action<double> set, bool canAdd = true, int minimumOrder = 1, Func<double> readValue = null, Action<double> changed = null,
            int trackWidth = 112) =>
            QuantityStepper(w, row, key, amount, min, max, 1, set, canAdd: canAdd, readValue: readValue, changed: changed,
                trackWidth: trackWidth, minimumOrder: minimumOrder);

        private Action<double> ShopQuantity(Window w, Transform row, string key, Func<bool> add, bool enabled,
            double? maxAmount = null, Func<int, bool> addCount = null, Action<double> changed = null, int minimumOrder = 1)
        {
            if (row.Find("Quantity_" + key) != null) return null;
            double amount = S.CartAmount(key);
            double max = maxAmount??1000;
            if (maxAmount == null && key.StartsWith("part:"))
            {
                var stall = S.CurrentPlace;
                var part = PartCatalog.Get(key.Substring(5));
                if (stall != null && part != null) max = Math.Min(1000, S.StallRemaining(stall, part));
            }
            return PurchaseQuantity(w, row, key, amount, 0, max, value =>
            {
                double delta = value - S.CartAmount(key);
                if (delta < 0) S.RemoveFromCart(key, -delta);
                else if (addCount != null) addCount((int)Math.Round(delta));
                else while (S.CartAmount(key) < value)
                {
                    double before = S.CartAmount(key);
                    if (!add() || S.CartAmount(key) <= before) break;
                }
            }, canAdd: enabled, minimumOrder: minimumOrder, readValue: () => S.CartAmount(key), changed: changed);
        }

        private int OnlineQuantity(Window w, Transform row, string key)
        {
            int amount = _onlineQuantities.TryGetValue(key, out int n) ? n : 1;
            PurchaseQuantity(w, row, "online_" + key, amount, 1, 100, value => _onlineQuantities[key] = (int)value);
            return amount;
        }
    }
}
