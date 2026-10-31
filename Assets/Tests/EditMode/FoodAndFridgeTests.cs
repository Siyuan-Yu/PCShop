using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class FoodAndFridgeTests
    {
        private static GameSession Make()
        {
            using (var source = HomeTests.Make())
            {
                var save = source.CreateSave();
                save.home.homeLayout = save.home.homeLayout.Concat(new[] { new SavePlaced { id = FurnitureCatalog.Fridge, x = 3, z = 1 } }).ToArray();
                save.home.shopLayout = save.home.shopLayout.Concat(new[] { new SavePlaced { id = FurnitureCatalog.Fridge, x = 1, z = 1 } }).ToArray();
                var s = GameSession.Load(source.City, save);
                s.EventsEnabled = false;
                GameSessionTests.Teleport(s, "flat");
                return s;
            }
        }
        private static FoodBatch Meal(string id, int count = 1) => new FoodBatch
        { Id = id, Name = GameSession.MealName(id), Hunger = 22, Count = count, ExpireDay = 1 };

        [Test]
        public void FridgeIsSoldForBothRooms_AndSupportsHeavyFurnitureDelivery()
        {
            Assert.True(FurnitureCatalog.Purchasable(false).Any(f => f.Id == FurnitureCatalog.Fridge));
            Assert.True(FurnitureCatalog.Purchasable(true).Any(f => f.Id == FurnitureCatalog.Fridge));
            Assert.True(FurnitureCatalog.Get(FurnitureCatalog.Fridge).IsHeavy);
            using (var s = Make())
            {
                Assert.True(s.CanOpenFridge);
                Assert.AreEqual(60, s.CurrentFridgeLimitKg);
                Assert.True(s.EmployeePurchaseCatalog().Any(o => o.Id == FurnitureCatalog.Fridge));
            }
        }

        [Test]
        public void NamedFoodCanBeMovedWithoutTakingAnotherDish_AndChillingCannotBeRefreshed()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Add(Meal("rice", 2));
                s.Backpack.Food.Packed.Add(Meal("veg", 2));
                Assert.True(s.MoveFridgeFood("lunch:veg", 2, true));
                Assert.AreEqual(2, s.Backpack.Food.Packed.Single().Count);
                Assert.AreEqual("rice", s.Backpack.Food.Packed.Single().Id);
                Assert.AreEqual("青菜焖饭", s.HomeFridge.Food.Packed.Single().Name);
                Assert.AreEqual(3, s.HomeFridge.Food.Packed.Single().ExpireDay);
                Assert.True(s.MoveFridgeFood("lunch:veg", 1, false));
                Assert.True(s.MoveFridgeFood("lunch:veg", 1, true));
                Assert.True(s.HomeFridge.Food.Packed.All(b => b.ExpireDay == 3 && b.Chilled));
                Assert.False(s.MoveFridgeFood("furn:fridge", 1, true));
            }
        }

        [Test]
        public void CookedFoodGoesDirectlyToFridge_AndCannotRemoveOccupiedLastFridge()
        {
            using (var s = Make())
            {
                s.Pantry.Cooked.Add(Meal("pork", 2));
                Assert.True(s.ChillCookedFood(2));
                Assert.Zero(s.Pantry.CookedBowls);
                Assert.AreEqual(2, s.HomeFridge.Lunchboxes);
                int index = s.HomeLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.Fridge);
                Assert.False(s.StoreFurniture(false, index));
                Assert.True(s.MoveFridgeFood("lunch:pork", 2, false));
                Assert.True(s.StoreFurniture(false, index));
                Assert.False(s.CanOpenFridge);
            }
        }

        [Test]
        public void HomeAndShopFoodStaySeparate_AndSaveRoundTripRetainsExpiryAndDietStats()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Add(Meal("rice"));
                Assert.True(s.MoveFridgeFood("lunch:rice", 1, true));
                GameSessionTests.Teleport(s, "pc");
                Assert.True(s.CanOpenFridge);
                Assert.Zero(s.CurrentFridge.Lunchboxes);
                s.Backpack.Food.Packed.Add(Meal("pork"));
                Assert.True(s.MoveFridgeFood("lunch:pork", 1, true));
                s.Player.Nutrition = 61; s.Player.Mood = 72;
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.AreEqual("rice", loaded.HomeFridge.Food.Packed.Single().Id);
                    Assert.AreEqual("pork", loaded.MainShop.Fridge.Food.Packed.Single().Id);
                    Assert.True(loaded.MainShop.Fridge.Food.Packed.Single().Chilled);
                    Assert.AreEqual(3, loaded.HomeFridge.Food.Packed.Single().ExpireDay);
                    Assert.AreEqual(61, loaded.Player.Nutrition);
                    Assert.AreEqual(72, loaded.Player.Mood);
                }
            }
        }

        [TestCase("rice", -5)]
        [TestCase("veg", 0)]
        [TestCase("pork", 5)]
        public void EatingChosenDishChangesOnlyRecordedNutrition_AndHunger(string id, int delta)
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Add(Meal("egg"));
                s.Backpack.Food.Packed.Add(Meal(id));
                s.Player.Hunger = 30; s.Player.Nutrition = 50;
                double health = s.Player.Health;
                Assert.True(s.EatLunchboxNow(id));
                Assert.AreEqual(50 + delta, s.Player.Nutrition);
                Assert.AreEqual(health, s.Player.Health);
                Assert.True(s.Backpack.Food.Packed.Any(b => b.Id == "egg"));
                Assert.Greater(s.Player.Hunger, 30);
            }
        }

        [Test]
        public void AutomaticWhiteRiceMealSubtractsFiveOnce_NotPerBowl()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Add(Meal("rice", 4));
                s.Player.Hunger = 0; s.Player.Nutrition = 50;
                s.Clock.Restore(new GameDate(0), 11 * 60 + 59);
                s.AdvanceClock(2);
                Assert.AreEqual(45, s.Player.Nutrition);
                Assert.Less(s.Backpack.Lunchboxes, 3);
            }
        }

        [Test]
        public void ColaIsConsumableEvenWhenFull_AndStatsClampWithoutAffectingHealth()
        {
            using (var s = Make())
            {
                s.Backpack.AddGift(Gifts.Soda, 1);
                s.Player.Hunger = 100; s.Player.Mood = 98; s.Player.Nutrition = 2;
                double health = s.Player.Health, energy = s.Player.Energy;
                Assert.True(s.ConsumeGift(Gifts.Soda));
                Assert.AreEqual(0, s.Backpack.GiftCount(Gifts.Soda));
                Assert.AreEqual(0, s.Player.Nutrition);
                Assert.AreEqual(100, s.Player.Mood);
                Assert.AreEqual(health, s.Player.Health);
                Assert.AreEqual(energy, s.Player.Energy);
                Assert.False(s.ConsumeGift(Gifts.Soda));
            }
        }

        [Test]
        public void OldSavesDefaultDietStatsToFifty_AndChilledFoodStillExpires()
        {
            using (var s = Make())
            {
                var data = s.CreateSave(); data.hasDietStats = false; data.nutrition = 0; data.mood = 0;
                using (var loaded = GameSession.Load(s.City, data))
                {
                    Assert.AreEqual(50, loaded.Player.Nutrition); Assert.AreEqual(50, loaded.Player.Mood);
                    loaded.Backpack.Food.Packed.Add(Meal("veg"));
                    Assert.True(loaded.MoveFridgeFood("lunch:veg", 1, true));
                    Assert.Zero(loaded.HomeFridge.Food.Spoil(3).Count);
                    Assert.AreEqual(1, loaded.HomeFridge.Food.Spoil(4).Count);
                }
            }
        }

        [Test]
        public void FridgeCapacityAndRetrievalWeightAreCheckedBeforeMovingFood()
        {
            using (var s = Make())
            {
                s.HomeFridge.Food.Packed.Add(Meal("rice", 120));
                s.Backpack.Food.Packed.Add(Meal("veg"));
                Assert.False(s.MoveFridgeFood("lunch:veg", 1, true));
                Assert.AreEqual(1, s.Backpack.Lunchboxes);
                Assert.AreEqual(120, s.HomeFridge.Lunchboxes);
                Assert.False(s.MoveFridgeFood("lunch:rice", 120, false));
                Assert.AreEqual(120, s.HomeFridge.Lunchboxes);
            }
        }

        [Test]
        public void RedoCannotRemoveAFridgeThatWasFilledAfterUndo()
        {
            using (var s = Make())
            {
                s.Time.Hold(); s.BeginLayoutHistory(false);
                int fridge = s.HomeLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.Fridge);
                Assert.True(s.StoreFurniture(false, fridge));
                Assert.True(s.UndoLayout());
                s.Backpack.Food.Packed.Add(Meal("veg"));
                Assert.True(s.MoveFridgeFood("lunch:veg", 1, true));
                Assert.False(s.RedoLayout());
                Assert.True(s.CanOpenFridge);
                Assert.AreEqual(1, s.HomeFridge.Lunchboxes);
                s.EndLayoutHistory(); s.Time.Release();
            }
        }
    }
}
