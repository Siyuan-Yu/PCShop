using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenMealDeliverySettings(Window parent)
        {
            OpenWindow("缺饭时自动订餐", 780, 660, w =>
            {
                Para(w.Content, "到饭点或饿了时，优先吃现有食物；没有可吃的食物时，按选定档位自动订一餐。", 18);
                Ui.Size(Ui.Button(w.Content, S.MealDeliveryEnabled ? "自动订餐：开" : "自动订餐：关", () =>
                { S.ToggleMealDelivery(); w.Rebuild(); parent?.RequestRebuild(); }, 19,
                    S.MealDeliveryEnabled ? Ui.ButtonHot : Ui.ButtonBg, S.HasSmartphone), -1, 42);
                if (!S.HasSmartphone) Para(w.Content, "需要先买智能手机才能开启自动订餐，可以先选择餐食。", 17, Ui.Warn);
                for (int i = 0; i < GameSession.DeliveryMeals.Count; i++)
                {
                    int tier = i;
                    var meal = GameSession.DeliveryMeals[i];
                    string selected = S.MealDeliveryTier == tier ? "✓ 已选 · " : "";
                    Ui.Size(Ui.Button(w.Content, $"{selected}{meal.Name} · ¥{meal.Price}/餐\n{meal.Effects}", () =>
                    { S.SelectMealDeliveryTier(tier); w.Rebuild(); parent?.RequestRebuild(); }, 18,
                        S.MealDeliveryTier == tier ? Ui.ButtonHot : Ui.ButtonBg), -1, 76);
                }
                Para(w.Content, "选择立即保存，点选不扣钱；实际订餐时扣费。余额不足时不下单，也不会自动换档。", 16, Ui.Dim);
                Line(w.Content, "心情目前只记录；精力恢复不超过当前精力上限。", 15, Ui.Dim);
            });
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
                Para(w.Content, "可放熟饭、食材、米和饮料。食物在冰箱里永久保存，不会过期；取出后继续计算存入前剩余的保质期。在这里饿了或到饭点，会自动吃冰箱里的即食食物。", 16, Ui.Dim);
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
            var food = CollectStacks(bag, new Stock(0)).Where(s => s.Key == "rice" || s.Key.StartsWith("ing:") || s.Key.StartsWith("lunch:")
                || (s.Key.StartsWith("gift:") && !Gifts.IsCigarette(s.Key.Substring(5)))).ToList();
            if (food.Count == 0) { Para(list, put ? "背包里没有可存入的食物。可先做饭或去农贸市场、烟酒茶行购买。" : "冰箱是空的。", 16, Ui.Dim); return; }
            foreach (var stack in food)
            {
                var item = stack;
                var row = Ui.Row(list, 64);
                string expiry = "";
                var batches = item.Key.StartsWith("lunch:") ? bag.Food.Packed.Where(b => b.Id == item.Key.Substring(6))
                    : item.Key.StartsWith("ing:") ? bag.Food.Ingredient.Where(b => b.Id == item.Key.Substring(4)) : Enumerable.Empty<FoodBatch>();
                expiry = " · " + (put ? FoodExpiryBadge.Countdown(batches.Any() ? (int?)batches.Min(b => b.ExpireDay) : null, S.Clock.Now.TotalMinutes) : "永久保存");
                Ui.Size(Ui.Label(row, item.Name + " ×" + item.Count + "\n<size=14>" + item.Kg.ToString("0.##") + " 千克" + expiry + "</size>", 17), -1, -1, 1);
                Ui.Size(Ui.Button(row, put ? "存入 1 份" : "取出 1 份", () => { S.MoveFridgeFood(item.Key, 1, put); w.Rebuild(); }, 16), 130);
                Ui.Size(Ui.Button(row, put ? "全部存入" : "全部取出", () => { S.MoveFridgeFood(item.Key, item.Count, put); w.Rebuild(); }, 16, Ui.ButtonHot), 130);
            }
        }
    }
}
