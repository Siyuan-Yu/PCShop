using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class UnifiedClockAndWeightTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void HomeStreetWalkingBlockedAndAutoTravelUseIdenticalElapsedTime(int speed)
        {
            using (var home = GameSessionTests.Make(50000, 10 * 60))
            using (var idle = GameSessionTests.Make(50000, 10 * 60))
            using (var moving = GameSessionTests.Make(50000, 10 * 60))
            using (var auto = GameSessionTests.Make(50000, 10 * 60))
            {
                idle.Leave(); moving.Leave(); auto.Leave();
                idle.ArriveAtNode("pc"); moving.ArriveAtNode("pc"); auto.ArriveAtNode("pc");
                foreach (var s in new[] { home, idle, moving, auto }) { s.EventsEnabled = false; s.Time.SetSpeed(speed); }
                Assert.True(auto.GoTo("mk")); moving.SetSteer(0, -1);
                for (int i = 0; i < 100; i++)
                {
                    if (i % 9 == 0) moving.SetSteer(i % 18 == 0 ? 0 : 1, -1);
                    home.Tick(.03); idle.Tick(.03); moving.Tick(.03); auto.Tick(.03);
                    Assert.AreEqual(home.Clock.Now, idle.Clock.Now);
                    Assert.AreEqual(home.Clock.Now, moving.Clock.Now);
                    Assert.AreEqual(home.Clock.Now, auto.Clock.Now);
                }
            }
        }

        [Test]
        public void StoppingTurningAndChangingSpeedKeepsFractionalClockTime_AndPauseStopsBoth()
        {
            using (var moving = GameSessionTests.Make())
            using (var idle = GameSessionTests.Make())
            {
                moving.Leave(); moving.ArriveAtNode("st");
                for (int i = 0; i < 100; i++)
                {
                    if (i == 30) { moving.Time.SetSpeed(4); idle.Time.SetSpeed(4); }
                    moving.SetSteer(i % 2 == 0 ? -1 : 0, 0);
                    moving.Tick(.01); idle.Tick(.01);
                    Assert.AreEqual(idle.Clock.Now, moving.Clock.Now);
                }
                moving.Time.SetPaused(true); var before = moving.Clock.Now; var pos = moving.PlayerPosition;
                moving.SetSteer(-1, 0); moving.Tick(.2);
                Assert.AreEqual(before, moving.Clock.Now); Assert.AreEqual(pos.X, moving.PlayerPosition.X);
                moving.Time.SetPaused(false); moving.Time.Hold(); moving.Tick(.2);
                Assert.AreEqual(before, moving.Clock.Now); moving.Time.Release();
            }
        }

        [Test]
        public void ArrivalPopupCannotConsumeOrDuplicateTheArrivalFramesTime_AndEtaCountsDown()
        {
            using (var s = GameSessionTests.Make())
            using (var idle = GameSessionTests.Make())
            {
                Assert.True(s.GoTo("pc")); int estimate = s.Trip.EstimatedMinutes;
                s.TripArrived += _ => s.Time.Hold();
                for (int i = 0; i < 2000 && s.Trip != null; i++)
                {
                    s.Tick(.05); idle.Tick(.05);
                    Assert.AreEqual(idle.Clock.Now, s.Clock.Now);
                    if (s.Trip != null) Assert.LessOrEqual(s.Trip.EstimatedMinutes, estimate);
                }
                Assert.IsNull(s.Trip); Assert.True(s.Time.IsHeld); s.Time.Release();
                s.Tick(.05); idle.Tick(.05); Assert.AreEqual(idle.Clock.Now, s.Clock.Now);
            }
        }

        [Test]
        public void RiceUsesKilogramsForPriceCapacityAndCookingYield()
        {
            using (var s = HomeTests.Make())
            {
                Assert.AreEqual(2.5, s.Pantry.RiceKg); Assert.AreEqual(8, Pantry.BowlsPerKg);
                GameSessionTests.Teleport(s, "farm"); var before = s.Ledger.Balance;
                Assert.True(s.BuyRice(1.25));
                Assert.AreEqual(1.25, s.Backpack.Food.RiceKg); Assert.AreEqual(1.25, s.Backpack.WeightKg);
                Assert.AreEqual(Money.FromCents(750), before - s.Ledger.Balance);
                Assert.False(s.BuyRice(12));
                GameSessionTests.Teleport(s, "flat");
                Assert.True(s.CookRice(.25, null));
                Assert.AreEqual(2.25, s.Pantry.RiceKg); s.AdvanceClock(Pantry.CookMinutes);
                Assert.AreEqual(2, s.Pantry.CookedBowls);
            }
        }

        [Test]
        public void LegacyRiceStorageAndCourierConvertOnce_WithoutChangingCashOrFood()
        {
            using (var source = HomeTests.Make())
            {
                var old = source.CreateSave(); old.weightUnitsVersion = 0;
                old.home.riceJin = 10; old.home.packRice = 4; old.home.boxRice = 6;
                old.home.homeTemp.rice = 8; old.home.homeFridge.rice = 2;
                old.extraShops = new[] { new SaveShop { place = "tea", locker = new SaveStorage { rice = 10 }, fridge = new SaveStorage { rice = 12 } } };
                old.home.couriers = new[] { new SaveCourier {
                    fromId = "farm", fromName = "旧配送", dueMinute = source.Clock.Now.TotalMinutes + 10,
                    kinds = new[] { (int)CourierItemKind.Rice }, ids = new[] { "rice" }, counts = new[] { 0 }, jins = new[] { 4.0 }
                } };
                using (var loaded = GameSession.Load(source.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old))))
                {
                    Assert.AreEqual(5, loaded.Pantry.RiceKg); Assert.AreEqual(2, loaded.Backpack.Food.RiceKg);
                    Assert.AreEqual(3, loaded.HomeBox.Food.RiceKg); Assert.AreEqual(4, loaded.HomeTemp.Food.RiceKg);
                    Assert.AreEqual(1, loaded.HomeFridge.Food.RiceKg);
                    Assert.AreEqual(5, loaded.ShopAt("tea").Locker.Food.RiceKg); Assert.AreEqual(6, loaded.ShopAt("tea").Fridge.Food.RiceKg);
                    Assert.AreEqual(2, loaded.Couriers.Single().LoadKg);
                    Assert.AreEqual(source.Ledger.Balance, loaded.Ledger.Balance);
                    var save = loaded.CreateSave(); Assert.AreEqual(1, save.weightUnitsVersion);
                    using (var again = GameSession.Load(source.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save))))
                    {
                        Assert.AreEqual(5, again.Pantry.RiceKg); Assert.AreEqual(2, again.Couriers.Single().LoadKg);
                        again.AdvanceClock(11); Assert.AreEqual(7, again.Pantry.RiceKg);
                    }
                }
            }
        }

        [Test]
        public void IngredientWeightsAndPerKilogramPricesAgree_AndRiceProcurementKeepsHalfKgPacks()
        {
            foreach (var ingredient in Ingredients.All)
            {
                Assert.AreEqual("千克", ingredient.Unit);
                Assert.AreEqual(ingredient.Price, ingredient.PricePerKg.Scale(ingredient.WeightKg));
            }
            using (var s = HomeTests.Make())
            {
                var rice = s.EmployeePurchaseCatalog().First(o => o.Kind == PurchaseGoodsKind.Rice);
                Assert.AreEqual(.5, rice.UnitKg); Assert.AreEqual(Money.FromYuan(3), rice.Price);
            }
        }

        [Test]
        public void RiceQuarterKilogramFitsTheLastBackpackSpace_AndRemainderShipsWithoutLoss()
        {
            using (var s = HomeTests.Make())
            {
                GameSessionTests.Teleport(s, "farm");
                s.Backpack.Food.RiceKg = 11.75;
                Assert.True(s.CartAddRice(1)); s.CartFillBag();
                Assert.AreEqual(.25, s.Cart.Single().InBag);
                Assert.AreEqual(.75, s.Cart.Single().ByTruck);
                Assert.True(s.Checkout(false));
                Assert.AreEqual(12, s.Backpack.Food.RiceKg);
                Assert.AreEqual(.75, s.Couriers.Single().LoadKg);
            }
        }
    }
}
