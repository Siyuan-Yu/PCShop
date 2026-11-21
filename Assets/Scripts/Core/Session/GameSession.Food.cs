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
        public Backpack HomeFridge { get; private set; } = new Backpack();
        public Backpack FridgeOf(bool shop) => shop ? CurrentShop.Fridge : HomeFridge;
        public int FridgeCount(bool shop) => LayoutOf(shop).Count(p => FurnitureCatalog.Get(p.Id)?.Role == FurnitureRole.Fridge);
        private Backpack ResidenceFridgeHere => HasSecondaryResidenceWarehouse ? FreightStorageAt(CurrentPlace.Id).Fridge : HomeFridge;
        public bool CanOpenFridge => InPlayerShop ? FridgeCount(true) > 0 : ResidenceHasHere(FurnitureRole.Fridge);
        public Backpack CurrentFridge => InPlayerShop ? FridgeOf(true) : ResidenceFridgeHere;
        public double CurrentFridgeLimitKg => HasSecondaryResidenceWarehouse
            ? FridgeCapacityOf(CurrentResidenceLayout) : FridgeLimitKg(InPlayerShop);

        private static int FridgeUnitsThatFit(double roomKg, double unitKg, int requested) =>
            unitKg <= 0 ? requested : (int)Math.Min(requested, Math.Floor(Math.Max(0, roomKg) / unitKg + 1e-9));

        private static bool HasMeal(System.Collections.Generic.List<FoodBatch> food) =>
            food.Any(b => b.Count > 0 && FastFoodCatalog.Packed(b.Id)?.Drink != true);
        private bool HasFridgeMeal => CanOpenFridge && HasMeal(CurrentFridge.Food.Packed);

        private static string MealBaseId(string id, out FishDefinition fish)
        {
            int split = id?.IndexOf('+') ?? -1;
            fish = split < 0 ? null : FishingCatalog.Get(id.Substring(split + 1));
            return fish == null ? id : id.Substring(0, split);
        }
        public static string MealName(string id)
        {
            string baseId = MealBaseId(id, out var fish);
            string name = FastFoodCatalog.Packed(baseId)?.Name ?? Ingredients.Get(baseId)?.DishName ?? "白米饭";
            return fish == null ? name : name + " + " + fish.Name;
        }
        public static double MealNutrition(string id)
        {
            string baseId = MealBaseId(id, out var fish);
            double nutrition = FastFoodCatalog.Packed(baseId)?.Nutrition ?? (baseId == "rice" ? -5
                : baseId == "pork" ? 5 : baseId == "egg" ? 3 : baseId == "tofu" ? 2 : FishingCatalog.Get(baseId) != null ? 4 : 0);
            return nutrition + (fish?.MealNutritionBonus ?? 0);
        }
        public static string SignedDiet(double value) => value > 0 ? "+" + value.ToString("0.#") : value.ToString("0.#");
        public static double MealMood(string id)
        {
            string baseId = MealBaseId(id, out var fish);
            return (FastFoodCatalog.Packed(baseId)?.Mood ?? 0) + (fish?.MealMoodBonus ?? 0);
        }
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

        public bool FridgeAcceptsFood(string key) => !string.IsNullOrEmpty(key) && FridgeKey(key);

        // 餐厅入库优先冷藏，余量继续由调用方放入后仓。
        private double AutoChillRestaurantFood(Backpack from, string key, double requested)
        {
            if (!InPlayerShop || !RestaurantIngredientsOnly(_focus) || !CanOpenFridge || !FridgeAcceptsFood(key)) return 0;
            var stored = ParseStored(key);
            if (stored == null) return 0;
            double available = Math.Min(requested, StoredHave(stored, from, null));
            double room = RestaurantFridgeRoom(_focus);
            double amount = stored.Rice ? Math.Min(available, room)
                : FridgeUnitsThatFit(room, stored.UnitKg, (int)Math.Min(int.MaxValue, available));
            if (amount <= 1e-9) return 0;
            if (stored.Kind == "gift" && IsRawIngredientProduct(stored.Id))
            {
                from.TakeGift(stored.Id, (int)amount);
                AddRetailToBag(CurrentFridge, Gifts.Get(stored.Id), (int)amount, FoodBatch.NoExpiry, chilled: true);
            }
            else StoredMove(stored, amount, from, null, CurrentFridge, null);
            Chill(CurrentFridge);
            return amount;
        }

        private bool AutoChillRestaurantIngredients(Backpack from)
        {
            bool moved = AutoChillRestaurantFood(from, "rice", from.Food.RiceKg) > 0;
            foreach (var id in from.Food.Ingredient.Select(batch => batch.Id).Distinct().ToArray())
                moved |= AutoChillRestaurantFood(from, "ing:" + id, int.MaxValue) > 0;
            foreach (var item in from.GiftLines.Where(item => IsRawIngredientProduct(item.Key)).ToArray())
                moved |= AutoChillRestaurantFood(from, "gift:" + item.Key, item.Value) > 0;
            return moved;
        }

        // Keep legacy chilled metadata, but refrigeration no longer grants a one-time expiry bonus.
        private static void Chill(Backpack bag)
        {
            foreach (var b in bag.Food.Ingredient.Concat(bag.Food.Packed).Where(b => !b.Chilled))
            { b.Chilled = true; }
        }

        public bool MoveFridgeFood(string key, int count, bool intoFridge) => MoveFridgeFood(key, count, intoFridge, false);

        public bool MoveFridgeWarehouseFood(string key, int count, bool intoFridge) => MoveFridgeFood(key, count, intoFridge, true);

        private bool MoveFridgeFood(string key, int count, bool intoFridge, bool warehouse)
        {
            if (!CanOpenFridge) return Fail("要在摆有冰箱的家或自己的店里存取食物。");
            if (string.IsNullOrEmpty(key) || !FridgeKey(key) || count <= 0) return Fail("冰箱只放食物和饮料。");
            if (warehouse && !HasWarehouseHere) return Fail("这里还没有可用的后仓。");
            var sk = ParseStored(key);
            if (sk == null) return false;
            var other = warehouse ? WarehouseBagHere : Backpack;
            var from = intoFridge ? other : CurrentFridge;
            var to = intoFridge ? CurrentFridge : other;
            double amount = Math.Min(count, StoredHave(sk, from, null));
            if (amount <= 0) return Fail("没有这种食物。");
            double kg = amount * sk.UnitKg;
            if (intoFridge && CurrentFridge.WeightKg + kg > CurrentFridgeLimitKg + 1e-9) return Fail("冰箱放不下了，可减少数量或再摆一台冰箱。");
            if (!intoFridge && !warehouse && !CanCarry(kg)) return false;
            if (intoFridge && sk.Kind == "gift" && IsRawIngredientProduct(sk.Id))
            {
                int units = (int)amount;
                from.TakeGift(sk.Id, units);
                AddRetailToBag(to, Gifts.Get(sk.Id), units, FoodBatch.NoExpiry, chilled: true);
            }
            else StoredMove(sk, amount, from, null, to, null);
            if (intoFridge) Chill(CurrentFridge);
            ApplyCargoLimit();
            string destination = intoFridge ? "冰箱" : warehouse ? "后仓" : "背包";
            string quantity = sk.Rice ? $" {amount:0.##} 千克" : $"×{amount:0}";
            Say($"「{sk.Name}」{quantity}已移到{destination}。");
            return true;
        }

        public bool ChillCookedFood(int count)
        {
            if (!AtHome || !CanOpenFridge) return Fail("要在家里摆好冰箱后存饭。");
            count = FridgeUnitsThatFit(CurrentFridgeLimitKg - CurrentFridge.WeightKg,
                Backpack.LunchboxKg, Math.Min(count, Pantry.CookedBowls));
            if (count <= 0) return Fail("锅里没有饭，或冰箱装不下了。");
            for (int i = 0; i < count; i++) CurrentFridge.Food.Packed.Add(Pantry.TakeBowl(Pantry.Cooked));
            Chill(CurrentFridge);
            Say("把锅里做好的饭 " + count + " 份放进了冰箱。");
            return true;
        }

        // Food deadlines use whole days. Offset every refrigerated batch at each midnight
        // so time in any fridge never consumes its remaining outdoor shelf life.
        private void PreserveFridges()
        {
            foreach (var bag in _shops.Select(shop => shop.Fridge).Prepend(HomeFridge).Concat(_residenceFreight.Values.Select(storage => storage.Fridge)))
                foreach (var batch in bag.Food.Packed)
                    batch.ExpireDay++;
        }
    }
}
