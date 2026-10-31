using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class FastFoodTests
    {
        private static GameSession Make(long cash = 1000)
        {
            var layout = GameSessionTests.Layout();
            layout.places = layout.places.Concat(new[] { new LayoutPlaceData { id = "fast", name = "好味快餐", kind = "shop", node = "s1", size = "medium", business = GameSession.FastFoodBusiness } }).ToArray();
            var s = new GameSession(CityLayout.FromData(layout), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 10 * 60));
            s.EventsEnabled = false; GameSessionTests.Teleport(s, "fast");
            return s;
        }
        [Test] public void Menu_HasRequestedFoodsAndFillingMeals()
        {
            Assert.AreEqual(GameSession.FastFoodMenu.Count, GameSession.FastFoodMenu.Select(i => i.Id).Distinct().Count());
            foreach (string id in new[] { "burger", "chicken", "cola", "rice", "combo" })
                Assert.IsTrue(GameSession.FastFoodMenu.Any(i => i.Id == id));
            Assert.IsTrue(GameSession.FastFoodMenu.Any(i => i.Hunger >= 65 && i.Price.Yuan <= 20));
        }
        [Test] public void Combo_PurchasePacksAndManualMealFeedsLater()
        {
            using (var s = Make())
            {
                s.Player.Hunger = 5; long before = s.Clock.Now.TotalMinutes;
                var cash = s.Ledger.Balance; var kg = s.Backpack.WeightKg;
                Assert.IsTrue(s.BuyFastFood("combo"));
                Assert.AreEqual(5, s.Player.Hunger);
                Assert.AreEqual(1, s.Backpack.Food.Packed.Count(b => b.Id == "fast:combo"));
                Assert.AreEqual(cash.Yuan - 32, s.Ledger.Balance.Yuan);
                Assert.AreEqual(0, s.Clock.Now.TotalMinutes - before);
                Assert.AreEqual(kg + .9, s.Backpack.WeightKg, 1e-8);
                Assert.IsTrue(s.EatLunchboxNow("fast:combo"));
                Assert.Greater(s.Player.Hunger, 90);
                Assert.AreEqual(20, s.Clock.Now.TotalMinutes - before);
                Assert.AreEqual("fast", s.CurrentPlace.Id);
            }
        }
        [Test] public void Cola_ChangesMoodAndNutrition_WithoutReplacingAMeal()
        {
            using (var s = Make())
            {
                s.Player.Hunger = 20; s.Player.Nutrition = 50; s.Player.Mood = 50;
                Assert.IsTrue(s.BuyFastFood("cola"));
                Assert.AreEqual(50, s.Player.Nutrition);
                Assert.IsTrue(s.EatLunchboxNow("fast:cola"));
                Assert.AreEqual(47, s.Player.Nutrition); Assert.AreEqual(58, s.Player.Mood);
                Assert.Less(s.Player.Hunger, 25);
            }
        }
        [Test] public void Rice_ProvidesNutritionAndEffectsSurviveJsonSave()
        {
            using (var s = Make())
            {
                s.Player.Hunger = 10; s.Player.Nutrition = 98; s.Player.Mood = 99;
                Assert.IsTrue(s.BuyFastFood("rice"));
                var json = JsonUtility.ToJson(s.CreateSave());
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(json)))
                {
                    Assert.AreEqual(10, loaded.Player.Hunger);
                    Assert.IsTrue(loaded.Backpack.Food.Packed.Any(b => b.Id == "fast:rice" && b.Name == "鸡肉蔬菜饭"));
                    Assert.IsTrue(loaded.EatLunchboxNow("fast:rice"));
                    Assert.Greater(loaded.Player.Hunger, 70);
                    Assert.AreEqual(100, loaded.Player.Nutrition); Assert.AreEqual(100, loaded.Player.Mood);
                    Assert.AreEqual(s.Ledger.Balance, loaded.Ledger.Balance);
                }
            }
        }
        [Test] public void InsufficientMoneyRejects_ButFullStomachCanBuyForLater()
        {
            using (var s = Make(1))
            {
                long time = s.Clock.Now.TotalMinutes; var cash = s.Ledger.Balance;
                Assert.IsFalse(s.BuyFastFood("burger")); Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.AreEqual(time, s.Clock.Now.TotalMinutes);
            }
            using (var s = Make())
            {
                s.Player.Hunger = 100; var cash = s.Ledger.Balance;
                Assert.IsTrue(s.BuyFastFood("burger")); Assert.AreEqual(cash.Yuan - 18, s.Ledger.Balance.Yuan);
                Assert.IsFalse(s.EatLunchboxNow("fast:burger"));
                Assert.IsTrue(s.BuyFastFood("cola"));
            }
        }
        [Test] public void InvalidLocationClosedShopAndUnknownItem_RejectWithoutCharging()
        {
            using (var s = Make())
            {
                var cash = s.Ledger.Balance;
                Assert.IsFalse(s.BuyFastFood("missing")); Assert.IsFalse(s.BuyFastFood(null));
                s.Clock.Restore(s.Clock.Today, 21 * 60);
                Assert.IsFalse(s.BuyFastFood("burger")); Assert.AreEqual(cash, s.Ledger.Balance);
                s.Leave(); Assert.IsFalse(s.BuyFastFood("burger"));
                Assert.AreEqual(cash, s.Ledger.Balance);
            }
        }
        [Test] public void PackedFood_AutoEatsAtMealTimeAndWhenHungry()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Clear(); s.Player.Hunger = 60;
                Assert.IsTrue(s.BuyFastFood("burger"));
                s.Clock.Restore(s.Clock.Today, 11 * 60 + 59); s.AdvanceClock(2);
                Assert.IsFalse(s.Backpack.Food.Packed.Any()); Assert.Greater(s.Player.Hunger, 95);
                Assert.AreEqual(49, s.Player.Nutrition); Assert.AreEqual(55, s.Player.Mood);
            }
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Clear(); s.Player.Hunger = 20;
                Assert.IsTrue(s.BuyFastFood("rice")); s.AdvanceClock(1);
                Assert.IsFalse(s.Backpack.Food.Packed.Any()); Assert.Greater(s.Player.Hunger, 80);
            }
        }
        [Test] public void PackedFood_UsesCapacityAndFridgeWithoutChangingIdentity()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Clear();
                for (int i = 0; i < 13; i++) Assert.IsTrue(s.BuyFastFood("combo"));
                var cash = s.Ledger.Balance; Assert.IsFalse(s.BuyFastFood("combo")); Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.AreEqual(11.7, s.Backpack.WeightKg, 1e-8);
                GameSessionTests.Teleport(s, s.Home.Id);
                var save = s.CreateSave();
                save.home.homeLayout = save.home.homeLayout.Concat(new[] { new SavePlaced { id = FurnitureCatalog.Fridge, x = 3, z = 1 } }).ToArray();
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Assert.IsTrue(loaded.MoveFridgeFood("lunch:fast:combo", 1, true));
                    var food = loaded.HomeFridge.Food.Packed.Single(); Assert.AreEqual("汉堡炸鸡套餐（含可乐）", food.Name);
                    Assert.IsTrue(food.Chilled); Assert.AreEqual(3, food.ExpireDay);
                    Assert.IsTrue(loaded.MoveFridgeFood("lunch:fast:combo", 1, false)); Assert.AreEqual(11.7, loaded.Backpack.WeightKg, 1e-8);
                }
            }
        }
        [Test] public void PackedFood_ExpiresAndColaDoesNotDisplaceSolidMeal()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Clear(); s.Player.Hunger = 30;
                Assert.IsTrue(s.BuyFastFood("cola")); Assert.IsTrue(s.BuyFastFood("rice"));
                s.Clock.Restore(s.Clock.Today, 11 * 60 + 59); s.AdvanceClock(2);
                Assert.IsTrue(s.Backpack.Food.Packed.Any(b => b.Id == "fast:cola"));
                Assert.IsFalse(s.Backpack.Food.Packed.Any(b => b.Id == "fast:rice"));
                Assert.IsTrue(s.BuyFastFood("burger"));
                s.Backpack.Food.Spoil(s.Clock.Today.TotalDays + 2);
                Assert.IsFalse(s.Backpack.Food.Packed.Any(b => b.Id == "fast:burger"));
                Assert.IsTrue(s.Backpack.Food.Packed.Any(b => b.Id == "fast:cola"));
            }
        }
        [Test] public void BatchPurchase_UsesCountPriceAndCapacityWithoutEating()
        {
            using (var s = Make())
            {
                s.Backpack.Food.Packed.Clear(); s.Player.Hunger = 10;
                var cash = s.Ledger.Balance; long time = s.Clock.Now.TotalMinutes;
                Assert.IsTrue(s.BuyFastFood("burger", 3));
                Assert.AreEqual(3, s.Backpack.Food.Packed.Single().Count);
                Assert.AreEqual(.75, s.Backpack.WeightKg, 1e-8);
                Assert.AreEqual(cash.Yuan - 54, s.Ledger.Balance.Yuan);
                Assert.AreEqual(time, s.Clock.Now.TotalMinutes); Assert.AreEqual(10, s.Player.Hunger);
                Assert.IsFalse(s.BuyFastFood("burger", 0)); Assert.IsFalse(s.BuyFastFood("combo", 100));
                Assert.AreEqual(cash.Yuan - 54, s.Ledger.Balance.Yuan);
            }
        }
        [Test] public void WorkEntryRemainsAvailable_AndFoodIsBlockedDuringWork()
        {
            using (var s = Make())
            {
                var shop = s.CurrentPlace; Assert.IsTrue(s.OffersPlayerWork(shop)); Assert.IsTrue(s.SignPlayerWork(shop));
                s.Leave(); s.Clock.Restore(new GameDate(s.WorkContract.StartDay), s.WorkContract.ShiftStart);
                GameSessionTests.Teleport(s, "fast"); Assert.IsTrue(s.IsWorking);
                var cash = s.Ledger.Balance; Assert.IsFalse(s.BuyFastFood("combo")); Assert.AreEqual(cash, s.Ledger.Balance);
            }
        }
    }
}
