using System;
using System.Linq;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public static double FridgeCapacityOf(System.Collections.Generic.IEnumerable<PlacedFurniture> layout) =>
            layout.Select(p => FurnitureCatalog.Get(p.Id)).Where(d => d?.Role == FurnitureRole.Fridge).Sum(d => d.FridgeCapacityKg);
        public double FridgeLimitKg(bool shop) => FridgeCapacityOf(LayoutOf(shop));
        public readonly Backpack HomeFridge = new Backpack();
        public Backpack FridgeOf(bool shop) => shop ? CurrentShop.Fridge : HomeFridge;
        public int FridgeCount(bool shop) => LayoutOf(shop).Count(p => FurnitureCatalog.Get(p.Id)?.Role == FurnitureRole.Fridge);
        public bool CanOpenFridge => (AtHome || InPlayerShop) && FridgeCount(InPlayerShop) > 0;
        public Backpack CurrentFridge => FridgeOf(InPlayerShop);
        public double CurrentFridgeLimitKg => FridgeLimitKg(InPlayerShop);
        private const int FridgeBonusDays = 7;
        private static bool HasMeal(System.Collections.Generic.List<FoodBatch> food) =>
            food.Any(b => b.Count > 0 && FastFoodCatalog.Packed(b.Id)?.Drink != true);
        private bool HasFridgeMeal => CanOpenFridge && HasMeal(CurrentFridge.Food.Packed);

        public static string MealName(string id) => FastFoodCatalog.Packed(id)?.Name ?? Ingredients.Get(id)?.DishName ?? "白米饭";
        public static double MealNutrition(string id) => FastFoodCatalog.Packed(id)?.Nutrition ?? (id == "rice" ? -5 : id == "pork" ? 5 : id == "egg" ? 3 : id == "tofu" ? 2 : 0);
        public static string SignedDiet(double value) => value > 0 ? "+" + value.ToString("0.#") : value.ToString("0.#");
        public static double MealMood(string id) => FastFoodCatalog.Packed(id)?.Mood ?? 0;
        public static string MealEffects(string id) => "营养 " + SignedDiet(MealNutrition(id)) + " · 心情 " + SignedDiet(MealMood(id));
        private void ApplyDiet(double nutrition, double mood)
        {
            Player.Nutrition = Math.Max(0, Math.Min(100, Player.Nutrition + nutrition));
            Player.Mood = Math.Max(0, Math.Min(100, Player.Mood + mood));
        }

        public static bool IsEdibleGift(string id) => id == Gifts.Soda || id == Gifts.Fruit || id == Gifts.Snack;
        public static string GiftEffects(string id) => id == Gifts.Soda ? "饥饿 +2 · 营养 −3 · 心情 +8"
            : id == Gifts.Fruit ? "饥饿 +8 · 营养 +5 · 心情 +2" : id == Gifts.Snack ? "饥饿 +10 · 营养 +1 · 心情 +4" : "";
        public bool ConsumeGift(string id)
        {
            if (!IsEdibleGift(id) || Backpack.GiftCount(id) <= 0) return Fail("背包里没有这种可直接食用的东西。");
            Backpack.TakeGift(id);
            double hunger = id == Gifts.Soda ? 2 : id == Gifts.Fruit ? 8 : 10;
            ApplyDiet(id == Gifts.Soda ? -3 : id == Gifts.Fruit ? 5 : 1, id == Gifts.Soda ? 8 : id == Gifts.Fruit ? 2 : 4);
            Player.Hunger = Math.Min(100, Player.Hunger + hunger);
            ApplyCargoLimit();
            Say("吃喝了" + Gifts.Get(id).Name + "：" + GiftEffects(id) + "。");
            return true;
        }

        private static bool FridgeKey(string key) => key == "rice" || key == "lunch" || key.StartsWith("lunch:") || key.StartsWith("ing:")
            || (key.StartsWith("gift:") && Gifts.Get(key.Substring(5)) != null && !Gifts.IsCigarette(key.Substring(5)));

        // Extend a batch only once, so repeatedly moving it through the fridge cannot reset spoilage.
        private static void Chill(Backpack bag)
        {
            foreach (var b in bag.Food.Ingredient.Concat(bag.Food.Packed).Where(b => !b.Chilled))
            { b.ExpireDay += FridgeBonusDays; b.Chilled = true; b.ChilledDays = FridgeBonusDays; }
        }

        public bool MoveFridgeFood(string key, int count, bool intoFridge)
        {
            if (!CanOpenFridge) return Fail("要在摆有冰箱的家或自己的店里存取食物。");
            if (string.IsNullOrEmpty(key) || !FridgeKey(key) || count <= 0) return Fail("冰箱只放食物和饮料。");
            var sk = ParseStored(key);
            if (sk == null) return false;
            var from = intoFridge ? Backpack : CurrentFridge;
            var to = intoFridge ? CurrentFridge : Backpack;
            double amount = Math.Min(count, StoredHave(sk, from, null));
            if (amount <= 0) return Fail("没有这种食物。");
            double kg = amount * sk.UnitKg;
            if (intoFridge && CurrentFridge.WeightKg + kg > CurrentFridgeLimitKg + 1e-9) return Fail("冰箱放不下了，可减少数量或再摆一台冰箱。");
            if (!intoFridge && !CanCarry(kg)) return false;
            StoredMove(sk, amount, from, null, to, null);
            if (intoFridge) Chill(CurrentFridge);
            ApplyCargoLimit();
            Say((intoFridge ? "放入冰箱：" : "从冰箱取出：") + sk.Name + "×" + amount.ToString("0.#") + "。");
            return true;
        }

        public bool ChillCookedFood(int count)
        {
            if (!AtHome || !CanOpenFridge) return Fail("要在家里摆好冰箱后存饭。");
            int fit = (int)Math.Floor((CurrentFridgeLimitKg - CurrentFridge.WeightKg) / Backpack.LunchboxKg + 1e-9);
            count = Math.Min(Math.Min(count, fit), Pantry.CookedBowls);
            if (count <= 0) return Fail("锅里没有饭，或冰箱装不下了。");
            for (int i = 0; i < count; i++) CurrentFridge.Food.Packed.Add(Pantry.TakeBowl(Pantry.Cooked));
            Chill(CurrentFridge);
            Say("把锅里做好的饭 " + count + " 份放进了冰箱。");
            return true;
        }

        private void SpoilFridges(int today)
        {
            var lost = HomeFridge.Food.Spoil(today);
            if (lost.Count > 0) Say("家里冰箱的食物过期：" + string.Join("、", lost));
            foreach (var shop in _shops)
            {
                lost = shop.Fridge.Food.Spoil(today);
                if (lost.Count > 0) Say(ShopTitle(shop) + "冰箱的食物过期：" + string.Join("、", lost));
            }
        }
    }
}
