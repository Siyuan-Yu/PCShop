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
            Para(parent, "设置即时保存，实际下单时扣费。余额不足会跳过本次订餐。", 16, Ui.Dim);
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

        public void OpenFridgeWindow()
        {
            if (!S.CanOpenFridge) { Toast("这里还没有摆冰箱。家具城有售，可摆在家或自己的店里。"); return; }
            bool shop = S.InPlayerShop;
            string placeId = S.CurrentPlace.Id;
            OpenWindow("冰箱 · " + (shop ? S.ShopTitle(S.CurrentShop) : "家里"), 900, 790, w =>
            {
                if (!S.CanOpenFridge || S.CurrentPlace.Id != placeId) { w.Close(); return; }
                Line(w.Content, $"已放 {S.CurrentFridge.WeightKg:0.##}/{S.CurrentFridgeLimitKg:0} 千克· 共 {S.FridgeCount(shop)} 台", 18, Ui.Accent);
                var layout = shop ? S.ShopLayout : S.HomeLayout;
                Para(w.Content, string.Join(" · ", layout.Select(p => FurnitureCatalog.Get(p.Id)).Where(d => d?.Role == FurnitureRole.Fridge).GroupBy(d => d.Id)
                    .Select(g => g.First().Name + " ×" + g.Count() + "（每台 " + g.First().FridgeCapacityKg.ToString("0") + " 千克）")), 15, Ui.Dim);
                string storage = shop && S.RetailKind == "restaurant"
                    ? "大米存入门店后仓。冰箱可放熟饭、其他食材和饮料。"
                    : "可放熟饭、食材、米和饮料。食材随处都能长期保存。";
                Para(w.Content, storage + "成品在冰箱里暂停保质期，取出后继续计时。到饭点或饿了时，会自动吃冰箱里的即食食物。", 16, Ui.Dim);
                if (!shop && S.Pantry.CookedBowls > 0)
                    Ui.Size(Ui.Button(w.Content, "把锅里的饭全部放入冰箱", () => { S.ChillCookedFood(S.Pantry.CookedBowls); w.Rebuild(); }, 17, Ui.ButtonHot), -1, 38);
                Line(w.Content, "冰箱里的食物（取出到背包后可吃）", 18, Ui.Accent);
                BuildFridgeFoodRows(w, S.CurrentFridge, false, 205);
                Line(w.Content, "背包里的食物（选择存入）", 18, Ui.Accent);
                BuildFridgeFoodRows(w, S.Backpack, true, 205);
                Line(w.Content, $"营养 {S.Player.Nutrition:0.#}/100 · 心情 {S.Player.Mood:0.#}/100。当前只记录，不影响健康、精力或经营。", 16, Ui.Dim);
            });
        }

        private void BuildFridgeFoodRows(Window w, Backpack bag, bool put, float height)
        {
            var list = Ui.Scroll(w.Content, height);
            var food = CollectStacks(bag, new Stock(0)).Where(s => (s.Key == "rice" || s.Key.StartsWith("ing:") || s.Key.StartsWith("lunch:")
                || (s.Key.StartsWith("gift:") && !Gifts.IsCigarette(s.Key.Substring(5)))) && (!put || S.FridgeAcceptsFood(s.Key))).ToList();
            if (food.Count == 0) { Para(list, put ? "背包里没有可存入的食物。可先做饭或去农贸市场、烟酒茶行购买。" : "冰箱是空的。", 16, Ui.Dim); return; }
            foreach (var stack in food)
            {
                var item = stack;
                var row = Ui.Row(list, 64);
                ItemPortrait(row, item.Key, 54);
                string expiry = "";
                if (item.Key.StartsWith("lunch:"))
                    expiry = " · " + (put ? FoodExpiryBadge.Countdown(item.Batches.Min(b => b.ExpireDay), S.Clock.Now.TotalMinutes) : "永久保存");
                Ui.Size(Ui.Label(row, item.Name + " ×" + item.Count + "\n<size=14>" + item.Kg.ToString("0.##") + " 千克" + expiry + "</size>", 17), -1, -1, 1);
                Ui.Size(Ui.Button(row, put ? "存入 1 份" : "取出 1 份", () => { S.MoveFridgeFood(item.Key, 1, put); w.Rebuild(); }, 16), 130);
                Ui.Size(Ui.Button(row, put ? "全部存入" : "全部取出", () => { S.MoveFridgeFood(item.Key, item.Count, put); w.Rebuild(); }, 16, Ui.ButtonHot), 130);
            }
        }
    }
}
