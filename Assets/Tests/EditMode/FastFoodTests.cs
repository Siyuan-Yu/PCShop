using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
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
        [Test] public void Combo_FeedsHungryPlayer_ChargesOnceAndTakesTime()
        {
            using (var s = Make())
            {
                s.Player.Hunger = 5; long before = s.Clock.Now.TotalMinutes;
                var cash = s.Ledger.Balance; var kg = s.Backpack.WeightKg;
                Assert.IsTrue(s.BuyFastFood("combo"));
                Assert.Greater(s.Player.Hunger, 90); Assert.LessOrEqual(s.Player.Hunger, 100);
                Assert.AreEqual(cash.Yuan - 32, s.Ledger.Balance.Yuan);
                Assert.AreEqual(20, s.Clock.Now.TotalMinutes - before);
                Assert.AreEqual(kg, s.Backpack.WeightKg);
                Assert.AreEqual("fast", s.CurrentPlace.Id);
            }
        }
        [Test] public void Cola_ChangesMoodAndNutrition_WithoutReplacingAMeal()
        {
            using (var s = Make())
            {
                s.Player.Hunger = 20; s.Player.Nutrition = 50; s.Player.Mood = 50;
                Assert.IsTrue(s.BuyFastFood("cola"));
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
                    Assert.Greater(loaded.Player.Hunger, 70);
                    Assert.AreEqual(100, loaded.Player.Nutrition); Assert.AreEqual(100, loaded.Player.Mood);
                    Assert.AreEqual(s.Ledger.Balance, loaded.Ledger.Balance);
                }
            }
        }
        [Test] public void InsufficientMoneyOrFullStomach_DoesNotChargeOrAdvance()
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
                Assert.IsFalse(s.BuyFastFood("burger")); Assert.AreEqual(cash, s.Ledger.Balance);
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
