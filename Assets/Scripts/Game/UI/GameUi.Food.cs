using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
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
                Para(w.Content, "可放熟饭、食材、米和饮料。熟饭与食材额外保鲜 2 天，每批只延长一次，反复存取不会刷新保质期。取出后保留到期日。", 16, Ui.Dim);
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
                if (batches.Any()) expiry = " · 最早到期：第 " + batches.Min(b => b.ExpireDay) + " 天";
                Ui.Size(Ui.Label(row, item.Name + " ×" + item.Count + "\n<size=14>" + item.Kg.ToString("0.##") + " 千克" + expiry + "</size>", 17), -1, -1, 1);
                Ui.Size(Ui.Button(row, put ? "存入 1 份" : "取出 1 份", () => { S.MoveFridgeFood(item.Key, 1, put); w.Rebuild(); }, 16), 130);
                Ui.Size(Ui.Button(row, put ? "全部存入" : "全部取出", () => { S.MoveFridgeFood(item.Key, item.Count, put); w.Rebuild(); }, 16, Ui.ButtonHot), 130);
            }
        }
    }
}
