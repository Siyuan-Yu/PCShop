using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _mealDeliveryTutorialWindow;

        private void OpenMealDeliverySettings(Window parent)
        {
            if (S.HasSmartphone) OpenMealDeliveryApp(parent, openSettings: true);
            else OpenWindow("缺饭时自动订餐", 780, 550,
                w => BuildMealDeliverySettings(w.Content, () => { w.Rebuild(); parent?.RequestRebuild(); }), scrollContent: false);
        }

        private void OpenMealDeliveryApp(Window parent = null, bool openSettings = false)
        {
            if (S.AppBlocker(PhoneApps.FoodDelivery) is string blocker) { Toast(blocker); return; }
            bool tutorial = S.MealDeliveryTutorialPaused;
            if (tutorial && _mealDeliveryTutorialWindow?.Closed == false) return;
            bool automatic = openSettings || tutorial;
            string lastOrder = null;
            var window = OpenPhoneAppWindow("外卖", w =>
            {
                bool locked = tutorial && S.MealDeliveryTutorialPaused;
                w.Closable = !locked;
                if (w.CloseButton != null) w.CloseButton.interactable = !locked;
                void Refresh() { w.Rebuild(); parent?.RequestRebuild(); }
                var tabs = Ui.Row(w.Content, 42, 8);
                var orderTab = Ui.Tab(tabs, "点餐", () => { automatic = false; w.Rebuild(); }, !automatic);
                orderTab.name = "MealDeliveryOrderTab";
                orderTab.interactable = !locked;
                var autoTab = Ui.Tab(tabs, "自动订餐", () => { automatic = true; w.Rebuild(); }, automatic);
                autoTab.name = "MealDeliveryAutoTab";
                autoTab.interactable = !locked;
                Ui.Size(Ui.Rect(tabs, "MealDeliveryTabSpacing"), 0, -1, 1);
                Ui.Size(Ui.Button(tabs, "返回手机", () => { w.Close(); _phoneStoreWindow?.Close(); OpenPhone(); },
                    16, Ui.ButtonSecondary, interactable: !locked), 124, 36);

                var columns = Ui.Row(w.Content, -1, 16);
                columns.GetComponent<LayoutElement>().flexibleHeight = 1;
                var columnsLayout = columns.GetComponent<HorizontalLayoutGroup>();
                columnsLayout.childAlignment = TextAnchor.UpperLeft;
                columnsLayout.childForceExpandHeight = false;
                var menu = Ui.Size(Ui.Rect(columns, "MealDeliveryMenu"), 0, -1, 1);
                Ui.VList(menu, 8, 0);
                var details = Ui.Size(Ui.FlatPanel(columns, "MealDeliveryDetails", Ui.FlatBand), 380, -1, 0).rectTransform;
                Ui.VList(details, 8, 16);
                Line(details, "可用现金", 16, Ui.Dim);
                Line(details, S.Ledger.Balance.ToString(), 28, Ui.Accent);
                Line(details, $"当前饱腹  {S.Player.Hunger:0} / 100", 19);
                if (automatic)
                {
                    BuildMealDeliverySettings(menu, Refresh, locked);
                    return;
                }

                Para(menu, "付款后立即送达并用餐。", 17, Ui.Dim);
                BuildMealDeliveryRows(menu, automatic: false, tier =>
                {
                    if (S.OrderDeliveryMeal(tier))
                    {
                        var meal = GameSession.DeliveryMeals[tier];
                        lastOrder = $"{meal.Name}已送达 · 已支付 ¥{meal.Price:N0}";
                    }
                    Refresh();
                });
                if (lastOrder != null)
                    Para(details, lastOrder, 17, Ui.Good).name = "MealDeliveryReceipt";
                if (S.OrderDeliveryMealBlocker(0) is string orderBlocker)
                    Para(details, orderBlocker, 16, Ui.Dim);
            }, scrollContent: false);
            if (tutorial) _mealDeliveryTutorialWindow = window;
        }

        private void BuildMealDeliverySettings(Transform parent, Action refresh, bool locked = false)
        {
            bool enabled = S.MealDeliveryEnabled;
            var header = Ui.Row(parent, 42, 10);
            Ui.Size(Ui.Label(header, enabled ? "自动订餐已开启" : "自动订餐已关闭", 20), 0, 42, 1);
            var toggle = Ui.Size(Ui.Button(header, enabled ? "关闭自动订餐" : "开启自动订餐", () =>
            {
                S.ToggleMealDelivery();
                _mealDeliveryGuideButton = null;
                refresh();
            }, 17, Ui.SelectedButton(enabled), S.HasSmartphone), 148, 38);
            toggle.name = "MealDeliveryAutoToggle";
            if (locked) _mealDeliveryGuideButton = toggle;
            if (!S.HasSmartphone) Para(parent, "需要智能手机才能开启。可以先选好餐档。", 16, Ui.Warn);
            Para(parent, "饭点或饿了时优先吃现有食物，缺餐时按选定档位点餐。", 17, Ui.Dim);
            BuildMealDeliveryRows(parent, automatic: true, tier => { S.SelectMealDeliveryTier(tier); refresh(); }, !locked);
            Para(parent, $"设置即时保存，{Ui.Emphasize("实际下单时扣费")}。{Ui.Emphasize("余额不足", Ui.Warn)}会跳过本次订餐。", 16, Ui.Dim);
        }

        private void BuildMealDeliveryRows(Transform parent, bool automatic, Action<int> choose, bool allowSelection = true)
        {
            for (int i = 0; i < GameSession.DeliveryMeals.Count; i++)
            {
                int tier = i;
                var meal = GameSession.DeliveryMeals[tier];
                bool selected = automatic && S.MealDeliveryTier == tier;
                var card = Ui.Size(Ui.FlatPanel(parent, "DeliveryMealRow", Ui.FlatBand,
                    selected ? Ui.FlatHotEdge : Ui.FlatCardEdge), -1, 66);
                card.raycastTarget = false;
                var row = Ui.Row(card.transform, 66, 12).Anchor(0, 0, 1, 1, 14, 0, 14, 0);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                ItemPortrait(row, "delivery:" + tier, 56);
                Ui.Size(Ui.Label(row, $"<b>{meal.Name}</b>\n<size=15>{meal.Effects}</size>", 19), 0, 54, 1);
                Ui.Size(Ui.Label(row, $"¥{meal.Price:N0} / 餐", 20, TextAnchor.MiddleRight, Ui.Accent), 128, 40);
                string blocker = automatic ? null : S.OrderDeliveryMealBlocker(tier);
                string label = automatic ? selected ? "当前餐档 ✓" : "设为自动餐档" : "点这份";
                var button = Ui.Size(Ui.Button(row, label, () => choose(tier), 17,
                    automatic ? Ui.SelectedButton(selected) : Ui.ButtonHot, allowSelection && blocker == null), 148, 38);
                button.name = (automatic ? "MealDeliveryAutoTier_" : "MealDeliveryOrder_") + tier;
                if (blocker != null) button.Tip(blocker);
            }
        }

        private static string FridgeLoadText(double usedKg, double capacityKg) =>
            double.IsPositiveInfinity(capacityKg) ? $"{usedKg:0.##} 千克（容量不限）" : $"{usedKg:0.##} / {capacityKg:0.##} 千克";

        public void OpenFridgeWindow()
        {
            bool shop = S.InPlayerShop;
            bool restaurant = shop && S.RetailKind == "restaurant";
            if (!restaurant && !S.CanOpenFridge) { Toast("这里还没有摆冰箱。家具城有售，可摆在家或自己的店里。"); return; }
            string placeId = S.CurrentPlace.Id;
            string selection = null;
            FoodStorageSource chosenSource = FoodStorageSource.Backpack;
            OpenWindow((restaurant ? "管理食材 · " : "冰箱 · ") + (shop ? S.ShopTitle(S.CurrentShop) : "家里"), 1500, 880, w =>
            {
                if (S.CurrentPlace?.Id != placeId || (!restaurant && !S.CanOpenFridge)) { w.Close(); return; }
                bool warehouse = S.HasWarehouseHere;
                if (restaurant) Line(w.Content, "厨房从冰箱取用食材", 16, Ui.Dim);
                if (_ctrl.InHome && S.Pantry.CookedBowls > 0)
                    Ui.Size(Ui.Button(w.Content, "把锅里的饭全部放入冰箱", () =>
                    {
                        if (S.ChillCookedFood(S.Pantry.CookedBowls)) RefreshFoodStorageViews(w);
                    }, 17, Ui.ButtonHot), -1, 38);
                var columns = Ui.Row(w.Content, 420, 14);
                columns.GetComponent<LayoutElement>().flexibleHeight = 1;
                BagStack picked = null;
                AddColumn("背包", $"{S.CarriedKg:0.##} / {S.CarryLimitKg:0.##} 千克", S.Backpack, FoodStorageSource.Backpack);
                if (warehouse)
                    AddColumn(shop ? "本店后仓" : "后仓", "容量不限", S.WarehouseBagHere, FoodStorageSource.Warehouse);
                var fridge = FoodStorageColumn(columns, "冰箱", S.CanOpenFridge
                    ? FridgeLoadText(S.CurrentFridge.WeightKg, S.CurrentFridgeLimitKg) : "尚未摆放");
                if (S.CanOpenFridge)
                    DrawGrid(fridge, S.CurrentFridge, FoodStorageSource.Fridge);

                var footer = Ui.Row(w.Content, 46, 9);
                Ui.Trim(footer, "FoodStorageFooterRule", Ui.FlatCardEdge, 0, top: true);
                if (picked != null) BuildFoodStorageActions(w, footer, picked, chosenSource, warehouse);
                else Ui.Size(Ui.Label(footer, "选择食材查看取放操作", 16, color: Ui.Dim), 0, 46, 1);

                void AddColumn(string title, string value, Backpack bag, FoodStorageSource source)
                {
                    DrawGrid(FoodStorageColumn(columns, title, value), bag, source);
                }

                void DrawGrid(Transform panel, Backpack bag, FoodStorageSource source)
                {
                    var items = CollectStacks(bag, new Stock(0)).Where(s => S.FridgeAcceptsFood(s.Key)).ToArray();
                    if (chosenSource == source) picked = items.FirstOrDefault(item => item.Key == selection);
                    BuildFoodStorageGrid(panel, items, source == FoodStorageSource.Fridge,
                        chosenSource == source ? selection : null, key =>
                        {
                            selection = key;
                            chosenSource = source;
                            w.Rebuild();
                        });
                }
            }, scrollContent: false);
        }

        private void RefreshFoodStorageViews(Window fridgeWindow)
        {
            fridgeWindow.Rebuild();
            _backpackWin?.Rebuild();
            RebuildDock();
        }

        private enum FoodStorageSource { Fridge, Warehouse, Backpack }

        private static RectTransform FoodStorageColumn(Transform parent, string title, string subtitle)
        {
            var panel = StorageWell(parent, "FoodStorageColumn_" + title);
            var header = Ui.Row(panel, 34, 8);
            Ui.Size(Line(header, title, 20), 0, 34, 1);
            Ui.Size(Ui.Label(header, subtitle, 15, TextAnchor.MiddleRight, Ui.Dim), 170, 34);
            Ui.Trim(header, "FoodStorageSectionRule", Ui.FlatCardEdge, 0);
            var list = Ui.Scroll(panel);
            var size = Ui.Size(list.parent.parent).GetComponent<LayoutElement>();
            size.minHeight = 100;
            size.flexibleHeight = 1;
            return list;
        }

        private void BuildFoodStorageGrid(Transform content, BagStack[] food, bool inFridge, string selected, Action<string> pick)
        {
            const int columns = 3;
            if (food.Length == 0) { Line(content, "暂无食材或食物", 16, Ui.Dim); return; }
            for (int i = 0; i < food.Length; i += columns)
            {
                var row = Ui.Row(content, 104, 6);
                for (int index = i; index < i + columns; index++)
                {
                    if (index >= food.Length)
                    {
                        Ui.Size(Ui.Rect(row, "EmptyFoodColumn"), 0, -1, 1);
                        continue;
                    }
                    var item = food[index];
                    bool active = item.Key == selected;
                    var button = Ui.Size(Ui.Button(row, ShortName(item.Name), () => pick(item.Key), 16,
                        active ? Ui.FlatRow : Ui.FlatWell), 0, -1, 1);
                    button.name = "FoodStorageItem_" + item.Key;
                    Ui.FlatBorder(button.image, active ? Ui.SelectionEdge : Ui.FlatWellEdge);
                    button.GetComponentInChildren<Text>().rectTransform.Anchor(0, 0, 1, 0, 2, 8, 2, -33);
                    RectTransform slot = button.image.rectTransform;
                    FillItemPortrait(slot, item.Portrait ?? item.Key).Anchor(.5f, 0, .5f, 0, -25, 34, -25, -84);
                    string count = item.Key == "rice" ? $"{item.Kg:0.##} 千克" : $"×{item.Count}";
                    Ui.Label(slot, count, 14, TextAnchor.MiddleRight, Ui.Dim).rectTransform.Anchor(0, 1, 1, 1, 5, -23, 6, 3);
                    string hint = $"{item.Name} · {item.Kg:0.##} 千克";
                    if (item.Batches?.Length > 0)
                    {
                        int expiry = item.Batches.Min(b => b.ExpireDay);
                        hint += "\n" + (inFridge ? "冷藏期间暂停保质期" : FoodExpiryBadge.Countdown(expiry, S.Clock.Now.TotalMinutes));
                    }
                    button.Tip(hint);
                }
            }
        }

        private void BuildFoodStorageActions(Window w, Transform row, BagStack item, FoodStorageSource source, bool warehouse)
        {
            bool inFridge = source == FoodStorageSource.Fridge;
            bool inWarehouse = source == FoodStorageSource.Warehouse;
            string location = inFridge ? "冰箱" : inWarehouse ? "后仓" : "背包";
            string unit = item.Key == "rice" ? " 千克" : " 份";
            ItemPortrait(row, item.Portrait ?? item.Key, 38);
            Ui.Size(Ui.Label(row, $"<b>{item.Name}</b> · {item.Kg:0.##} 千克\n<size=14>{location}</size>", 17), 0, 42, 1);
            void Transfer(int count)
            {
                bool moved = inWarehouse
                    ? S.MoveFridgeWarehouseFood(item.Key, count, true)
                    : S.MoveFridgeFood(item.Key, count, !inFridge);
                if (moved) RefreshFoodStorageViews(w);
            }
            if (S.CanOpenFridge)
            {
                Ui.Size(Ui.Button(row, inFridge ? "取出 1" + unit : "存 1" + unit + "到冰箱", () => Transfer(1), 16), 178, 38);
                Ui.Size(Ui.Button(row, inFridge ? "全部取到背包" : "全部存入冰箱", () => Transfer(item.Count), 16, Ui.ButtonHot), 158, 38);
            }
            if (inWarehouse)
                Ui.Size(Ui.Button(row, "取 1" + unit + "到背包", () =>
                {
                    if (S.TakeFromBox(item.Key)) RefreshFoodStorageViews(w);
                }, 16), 178, 38);
            if (warehouse && !inWarehouse)
                Ui.Size(Ui.Button(row, "全部放后仓", () =>
                {
                    bool moved = inFridge
                        ? S.MoveFridgeWarehouseFood(item.Key, item.Count, false)
                        : S.StoreInBox(item.Key, item.Count);
                    if (moved) RefreshFoodStorageViews(w);
                }, 16), 148, 38);
        }
    }
}
