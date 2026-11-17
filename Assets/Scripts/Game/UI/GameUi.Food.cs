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
        private void OpenMealDeliverySettings(Window parent)
        {
            if (S.HasSmartphone) OpenMealDeliveryApp(parent, openSettings: true);
            else OpenWindow("缺饭时自动订餐", 780, 550,
                w => BuildMealDeliverySettings(w.Content, () => { w.Rebuild(); parent?.RequestRebuild(); }), scrollContent: false);
        }

        private void OpenMealDeliveryApp(Window parent = null, bool openSettings = false)
        {
            if (S.AppBlocker(PhoneApps.FoodDelivery) is string blocker) { Toast(blocker); return; }
            bool automatic = openSettings;
            string lastOrder = null;
            OpenPhoneAppWindow("外卖", w =>
            {
                void Refresh() { w.Rebuild(); parent?.RequestRebuild(); }
                var tabs = Ui.Row(w.Content, 42, 8);
                Ui.Tab(tabs, "点餐", () => { automatic = false; w.Rebuild(); }, !automatic).name = "MealDeliveryOrderTab";
                Ui.Tab(tabs, "自动订餐", () => { automatic = true; w.Rebuild(); }, automatic).name = "MealDeliveryAutoTab";
                Ui.Size(Ui.Rect(tabs, "MealDeliveryTabSpacing"), 0, -1, 1);
                Ui.Size(Ui.Button(tabs, "返回手机", () => { w.Close(); _phoneStoreWindow?.Close(); OpenPhone(); }, 16), 124, 36);

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
                    BuildMealDeliverySettings(menu, Refresh);
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
        }

        private void BuildMealDeliverySettings(Transform parent, Action refresh)
        {
            bool enabled = S.MealDeliveryEnabled;
            var header = Ui.Row(parent, 42, 10);
            Ui.Size(Ui.Label(header, enabled ? "自动订餐已开启" : "自动订餐已关闭", 20), 0, 42, 1);
            Ui.Size(Ui.Button(header, enabled ? "关闭自动订餐" : "开启自动订餐", () =>
            {
                S.ToggleMealDelivery();
                refresh();
            }, 17, Ui.SelectedButton(enabled), S.HasSmartphone), 148, 38).name = "MealDeliveryAutoToggle";
            if (!S.HasSmartphone) Para(parent, "需要智能手机才能开启。可以先选好餐档。", 16, Ui.Warn);
            Para(parent, "饭点或饿了时优先吃现有食物，缺餐时按选定档位点餐。", 17, Ui.Dim);
            BuildMealDeliveryRows(parent, automatic: true, tier => { S.SelectMealDeliveryTier(tier); refresh(); });
            Para(parent, $"设置即时保存，{Ui.Emphasize("实际下单时扣费")}。{Ui.Emphasize("余额不足", Ui.Warn)}会跳过本次订餐。", 16, Ui.Dim);
        }

        private void BuildMealDeliveryRows(Transform parent, bool automatic, Action<int> choose)
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
                    automatic ? Ui.SelectedButton(selected) : Ui.ButtonHot, blocker == null), 148, 38);
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
            var pages = new int[3];
            OpenCompactWindow((restaurant ? "管理食材 · " : "冰箱 · ") + (shop ? S.ShopTitle(S.CurrentShop) : "家里"), 900, w =>
            {
                if (S.CurrentPlace?.Id != placeId || (!restaurant && !S.CanOpenFridge)) { w.Close(); return; }
                bool warehouse = S.HasWarehouseHere;
                Para(w.Content, restaurant
                    ? "厨房只从冰箱取料，大米也需要放进去。送货时冰箱装不下的食材会留在门店后仓。"
                    : "成品在冰箱里暂停保质期，取出后继续计时。", 16, Ui.Dim);
                if (!shop && S.Pantry.CookedBowls > 0)
                    Ui.Size(Ui.Button(w.Content, "把锅里的饭全部放入冰箱", () => { S.ChillCookedFood(S.Pantry.CookedBowls); w.Rebuild(); }, 17, Ui.ButtonHot), -1, 38);
                Line(w.Content, S.CanOpenFridge ? "冰箱 · " + FridgeLoadText(S.CurrentFridge.WeightKg, S.CurrentFridgeLimitKg)
                    : "冰箱 · 尚未摆放", 18, Ui.Accent);
                if (S.CanOpenFridge)
                    BuildFridgeFoodRows(w, S.CurrentFridge, FoodStorageSource.Fridge, warehouse, pages);
                else
                    Para(w.Content, "可先从农贸市场进货，摆好冰箱后再把后仓食材移进去。", 16, Ui.Dim);
                if (warehouse)
                {
                    Line(w.Content, "后仓", 18, Ui.Accent);
                    BuildFridgeFoodRows(w, shop ? S.ShopLocker : S.HomeBox, FoodStorageSource.Warehouse, true, pages);
                }
                Line(w.Content, "背包", 18, Ui.Accent);
                BuildFridgeFoodRows(w, S.Backpack, FoodStorageSource.Backpack, warehouse, pages);
            });
        }

        private enum FoodStorageSource { Fridge, Warehouse, Backpack }

        private void BuildFridgeFoodRows(Window w, Backpack bag, FoodStorageSource source, bool warehouse, int[] pages)
        {
            const int pageSize = 3;
            var food = CollectStacks(bag, new Stock(0)).Where(s => S.FridgeAcceptsFood(s.Key)).ToArray();
            if (food.Length == 0) { Para(w.Content, "这里没有食材或食物。", 16, Ui.Dim); return; }
            int pageCount = (food.Length + pageSize - 1) / pageSize;
            int page = pages[(int)source] = Mathf.Clamp(pages[(int)source], 0, pageCount - 1);
            foreach (var item in food.Skip(page * pageSize).Take(pageSize))
            {
                var row = Ui.Row(w.Content, 46);
                row.name = $"FoodStorage_{source}_{item.Key}";
                ItemPortrait(row, item.Key, 42);
                bool inFridge = source == FoodStorageSource.Fridge;
                bool inWarehouse = source == FoodStorageSource.Warehouse;
                bool rice = item.Key == "rice";
                string unit = rice ? " 千克" : " 份";
                string expiry = "";
                if (item.Key.StartsWith("lunch:"))
                    expiry = " · " + (inFridge ? "永久保存" : FoodExpiryBadge.Countdown(item.Batches.Min(b => b.ExpireDay), S.Clock.Now.TotalMinutes));
                Ui.Size(Ui.Label(row, $"{item.Name}{(rice ? "" : " ×" + item.Count)}\n<size=14>{item.Kg:0.##} 千克{expiry}</size>", 17), -1, -1, 1);
                void Transfer(int count)
                {
                    if (inWarehouse) S.MoveFridgeWarehouseFood(item.Key, count, true);
                    else S.MoveFridgeFood(item.Key, count, !inFridge);
                    w.Rebuild();
                    RebuildDock();
                }
                if (S.CanOpenFridge)
                {
                    Ui.Size(Ui.Button(row, (inFridge ? "取出 1" : "存入 1") + unit, () => Transfer(1), 16), 118);
                    Ui.Size(Ui.Button(row, inFridge ? "全部取到背包" : "全部存入冰箱", () => Transfer(item.Count), 16, Ui.ButtonHot), 124);
                }
                if (inWarehouse)
                    Ui.Size(Ui.Button(row, "取出 1" + unit + "到背包", () =>
                    {
                        S.TakeFromBox(item.Key);
                        w.Rebuild();
                        RebuildDock();
                    }, 16), 150);
                if (warehouse && !inWarehouse)
                    Ui.Size(Ui.Button(row, "全部放后仓", () =>
                    {
                        if (inFridge) S.MoveFridgeWarehouseFood(item.Key, item.Count, false);
                        else S.StoreInBox(item.Key, item.Count);
                        w.Rebuild();
                        RebuildDock();
                    }, 16), 118);
            }
            BuildCompactPageControls(w.Content, page, pageCount, value =>
            {
                pages[(int)source] = value;
                w.Rebuild();
            });
        }
    }
}
