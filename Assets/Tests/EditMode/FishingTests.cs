using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class FishingTests
    {
        private static GameSession Make()
        {
            using var fixture = DistrictProgressionTests.Make();
            var s = new GameSession(fixture.City, 73, Money.FromYuan(100000), GameTimestamp.From(new GameDate(42), 600), true, false, false);
            s.Player.Health = 100; s.Player.Energy = 100; s.Player.Hunger = 100;
            At(s, GameSession.FishingVenueId); return s;
        }
        private static void At(GameSession s, string id)
        {
            s.StopTrip(); s.ArriveAtNode(s.City.GetPlace(id).NodeId);
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(id));
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static FishingState Mutable(GameSession s) => (FishingState)typeof(GameSession).GetField("_fishing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
        private static CityActivity Start(GameSession s)
        {
            Assert.IsTrue(s.SelectVenueService("fishing", 0), s.VenueServiceBlocker("fishing", 0));
            s.AdvanceClock(2); Assert.IsTrue(s.FishingActive); return s.RunningCityActivity;
        }
        private static void Cast(GameSession s, double charge = .2)
        {
            Assert.IsTrue(s.BeginFishingCast(), s.FishingCastBlocker);
            s.AdvanceFishingInteraction(charge, true); Assert.IsTrue(s.ReleaseFishingCast());
            s.AdvanceFishingInteraction(.81, false); Assert.AreEqual(FishingPhase.Waiting, s.CurrentFishing.Phase);
        }
        private static void Bite(GameSession s, double charge = .2, string fishId = null)
        {
            Cast(s, charge);
            var state = Mutable(s);
            string target = fishId ?? new[] { "fish_crucian", "fish_carp", "fish_bass" }[state.Spot];
            state.SpeciesRoll = Enumerable.Range(0, 10000).First(roll => FishingCatalog.Pick(state.CastDistance, state.Spot, state.Bait, roll).Id == target);
            s.AdvanceClock((int)(s.CurrentFishing.BiteAt - s.Clock.Now.TotalMinutes));
            Assert.AreEqual(FishingPhase.Bite, s.CurrentFishing.Phase);
        }
        private static void PlayFight(GameSession s)
        {
            Assert.IsTrue(s.HookFishingFish());
            for (int i = 0; i < 3000 && s.CurrentFishing.Phase == FishingPhase.Fighting; i++)
            {
                var f = s.CurrentFishing;
                bool held = f.LineTension < .75 && f.ControlPosition + f.ControlVelocity * .18 < f.FishPosition;
                s.AdvanceFishingInteraction(.02, held);
            }
            Assert.AreEqual(FishingPhase.Landed, s.CurrentFishing.Phase, s.CurrentFishing.Message);
        }
        private static void Catch(GameSession s)
        {
            Bite(s, s.CurrentFishing.Spot == 0 ? .2 : s.CurrentFishing.Spot == 1 ? .7 : 1);
            PlayFight(s);
        }
        private static CityActivity Invite(GameSession s, int number, int choice = 0)
        {
            s.RaiseEvent(number == 59 ? "act_fishing_shaoan" : "act_fishing_qiaosong"); Assert.NotNull(s.PendingEvent);
            string body = s.PendingEvent.Body; Assert.IsTrue(s.ResolveEvent(choice), body);
            var a = s.CityActivities.Last(x => x.Active);
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(s, a.VenueId); Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace)); s.AdvanceClock(3);
            Assert.IsTrue(s.FishingActive); return a;
        }

        private static System.Collections.Generic.IEnumerable<TestCaseData> EveryFish => FishingCatalog.Fish.Select(f => new TestCaseData(f.Id));
        [TestCaseSource(nameof(EveryFish))]
        public void EverySpeciesCanActuallyBeCaughtKeptReloadedCookedAndSold(string fishId)
        {
            var fish = FishingCatalog.Get(fishId);
            using var s = Make(); Start(s); s.SelectFishingSpot(fish.Habitat); s.AdvanceClock(2); s.SelectFishingBait(fish.Bait);
            double startingWeight = s.Backpack.WeightKg;
            Bite(s, fish.Habitat == 0 ? .2 : fish.Habitat == 1 ? .7 : 1, fishId); PlayFight(s);
            Assert.AreEqual(fishId, s.CurrentFishing.PendingFishId); Assert.IsTrue(s.KeepCaughtFish());
            using var loaded = Reload(s);
            Assert.AreEqual(1, loaded.Backpack.Food.IngredientCount(fishId)); Assert.AreEqual(startingWeight + fish.Kg, loaded.Backpack.WeightKg, .001);
            Assert.IsNotNull(Ingredients.Get(fishId)); Assert.Greater(Ingredients.Get(fishId).Price.Yuan, fish.SalePrice);
            var food = new Pantry { RiceKg = 1 }; food.AddIngredient(fishId, 1);
            Assert.IsTrue(food.StartCook(.25, fishId, 0, out string reason), reason);
            Assert.AreEqual(fishId, food.Cooking.Batch.Id);
            var cash = loaded.Ledger.Balance; Assert.IsTrue(loaded.SellFishingCatch(fishId, 1));
            Assert.AreEqual(cash + Money.FromYuan(fish.SalePrice), loaded.Ledger.Balance);
            Assert.IsFalse(loaded.SellFishingCatch(fishId, 1));
        }
        [Test]
        public void TwentySpeciesHaveDistinctPoolsPreferencesAndBoundedMovement()
        {
            Assert.AreEqual(20, FishingCatalog.Fish.Length); Assert.AreEqual(20, FishingCatalog.Fish.Select(f => f.Id).Distinct().Count());
            Assert.AreEqual(6, FishingCatalog.Fish.Select(f => f.Movement).Distinct().Count());
            foreach (var fish in FishingCatalog.Fish)
            {
                double distance = fish.Habitat == 0 ? 8 : fish.Habitat == 1 ? 17 : 27;
                Assert.Greater(FishingCatalog.PoolWeight(fish, distance, fish.Habitat, fish.Bait),
                    FishingCatalog.PoolWeight(fish, distance, fish.Habitat, (fish.Bait + 1) % 3));
                for (int i = 0; i < 600; i++) Assert.That(fish.Target(i * .1, 7.3), Is.InRange(.05, .95));
            }
        }

        [Test]
        public void CastingPowerChangesRealDistanceAndBaitPoolWithoutInstantRewards()
        {
            using var s = Make(); Start(s);
            Assert.IsTrue(s.BeginFishingCast()); s.AdvanceFishingInteraction(.2, true);
            Assert.AreEqual(6, s.CurrentFishing.Baits[0]);
            Assert.IsTrue(s.ReleaseFishingCast()); double near = s.CurrentFishing.CastDistance;
            s.AdvanceFishingInteraction(.81, false); s.AdvanceClock((int)(s.CurrentFishing.BiteAt - s.Clock.Now.TotalMinutes));
            for (int i = 0; i < 4; i++) s.AdvanceFishingInteraction(1, false);
            Assert.IsTrue(s.SelectFishingBait(2)); Cast(s, 1);
            Assert.Greater(s.CurrentFishing.CastDistance, near + 10);
            Assert.AreEqual(5, s.CurrentFishing.Baits[0]); Assert.AreEqual(1, s.CurrentFishing.Baits[2]);
            Assert.AreEqual(0, s.CurrentFishing.Catches);
        }

        [Test]
        public void HoldingWithoutTrackingLosesButControlledReelingWinsAndFightStatePersists()
        {
            using var s = Make(); Start(s); Bite(s); Assert.IsTrue(s.HookFishingFish());
            var initial = s.CurrentFishing; s.AdvanceFishingInteraction(.4, true);
            Assert.Greater(s.CurrentFishing.ControlPosition, initial.ControlPosition);
            using var restored = Reload(s);
            Assert.AreEqual(s.CurrentFishing.CatchProgress, restored.CurrentFishing.CatchProgress, 1e-10);
            Assert.AreEqual(s.CurrentFishing.FishPosition, restored.CurrentFishing.FishPosition, 1e-10);
            for (int i = 0; i < 60 && s.CurrentFishing.Phase == FishingPhase.Fighting; i++) s.AdvanceFishingInteraction(1, true);
            Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase); Assert.AreEqual(0, s.FishCaughtTotal);
            for (int i = 0; i < 3000 && restored.CurrentFishing.Phase == FishingPhase.Fighting; i++)
            {
                var f = restored.CurrentFishing;
                restored.AdvanceFishingInteraction(.02, f.LineTension < .75 && f.ControlPosition + f.ControlVelocity * .18 < f.FishPosition);
            }
            Assert.AreEqual(FishingPhase.Landed, restored.CurrentFishing.Phase);
        }

        [Test]
        public void ClosingDuringFightDropsTheHookAndReleasesOnlyItsOwnPause()
        {
            using var s = Make(); Start(s); s.SetFishingInterfaceOpen(true); Bite(s); s.HookFishingFish();
            s.Time.Hold(); s.SetFishingInterfaceOpen(false);
            Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase); Assert.IsTrue(s.Time.IsHeld);
            s.Time.Release(); Assert.IsFalse(s.Time.IsHeld); Assert.AreEqual(0, s.FishCaughtTotal);
        }

        [Test]
        public void VenueIsReachableInTheOutskirtsAndARealPaidSessionStartsAtItsDock()
        {
            using var s = Make(); var p = s.City.GetPlace(GameSession.FishingVenueId);
            Assert.AreEqual(0, s.DistrictTierOf(p)); Assert.IsNull(s.DistrictAccessBlocker(p));
            Assert.That(p.Name, Does.Contain("垂钓场")); var balance = s.Ledger.Balance;
            var a = Start(s); Assert.AreEqual(balance - Money.FromYuan(300), s.Ledger.Balance);
            Assert.AreEqual(12, s.CurrentFishing.Baits.Sum()); Assert.AreEqual(90, a.Duration);
            Assert.IsTrue(a.PlayerVenueRoute.HasPath); Assert.IsTrue(s.Backpack.Food.Ingredient.All(b => FishingCatalog.Get(b.Id) == null));
        }

        [Test]
        public void CastWaitsForGameTimeAndConsumesBaitEvenWhenTheFishEscapes()
        {
            using var s = Make(); Start(s); int count = s.CurrentFishing.Baits[0]; long now = s.Clock.Now.TotalMinutes;
            Cast(s); Assert.Greater(s.CurrentFishing.BiteAt, now); Assert.AreEqual(count - 1, s.CurrentFishing.Baits[0]);
            Assert.IsFalse(s.HookFishingFish()); s.AdvanceClock((int)(s.CurrentFishing.BiteAt - s.Clock.Now.TotalMinutes));
            s.AdvanceFishingInteraction(1, false); s.AdvanceFishingInteraction(1, false); s.AdvanceFishingInteraction(1, false);
            Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase);
            Assert.AreEqual(0, s.CurrentFishing.Catches); Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian"));
        }

        [TestCase(0, "fish_crucian", .6)]
        [TestCase(1, "fish_carp", 1.2)]
        [TestCase(2, "fish_bass", 2)]
        public void DifferentSpotsYieldRealWeightedFishAndKeepingCannotDuplicateThem(int spot, string id, double kg)
        {
            using var s = Make(); Start(s); Assert.IsTrue(s.SelectFishingSpot(spot)); s.AdvanceClock(2); s.SelectFishingBait(spot);
            double weight = s.Backpack.WeightKg; Catch(s);
            Assert.AreEqual(id, s.CurrentFishing.PendingFishId); Assert.AreEqual(0, s.Backpack.Food.IngredientCount(id));
            Assert.IsTrue(s.KeepCaughtFish()); Assert.AreEqual(1, s.Backpack.Food.IngredientCount(id));
            Assert.AreEqual(weight + kg, s.Backpack.WeightKg, .001); Assert.IsFalse(s.KeepCaughtFish());
            Assert.IsFalse(s.ReleaseCaughtFish()); Assert.AreEqual(1, s.FishCaughtTotal);
        }

        [Test]
        public void ReleasedFishCannotBeKeptAndSellingConsumesTheActualIngredient()
        {
            using var s = Make(); Start(s); Catch(s); Assert.IsTrue(s.ReleaseCaughtFish()); Assert.IsFalse(s.KeepCaughtFish());
            Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian")); Catch(s); Assert.IsTrue(s.KeepCaughtFish());
            var balance = s.Ledger.Balance; Assert.IsTrue(s.SellFishingCatch("fish_crucian", 1));
            Assert.AreEqual(balance + Money.FromYuan(180), s.Ledger.Balance); Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian"));
            Assert.IsFalse(s.SellFishingCatch("fish_crucian", 1));
            Assert.Less(FishingCatalog.Get("fish_crucian").SalePrice, Ingredients.Get("fish_crucian").Price.Yuan);
        }

        [Test]
        public void WaitingAndLandedFishPersistWithoutRerollOrDuplicateGrant()
        {
            using var s = Make(); Start(s); Cast(s); Mutable(s).SpeciesRoll = 50;
            var waiting = s.CurrentFishing; using var loaded = Reload(s);
            Assert.AreEqual(waiting.BiteAt, loaded.CurrentFishing.BiteAt); Assert.AreEqual(waiting.SpeciesRoll, loaded.CurrentFishing.SpeciesRoll);
            loaded.AdvanceClock((int)(waiting.BiteAt - loaded.Clock.Now.TotalMinutes));
            PlayFight(loaded);
            using var landed = Reload(loaded); Assert.AreEqual(FishingPhase.Landed, landed.CurrentFishing.Phase);
            Assert.AreEqual(0, landed.Backpack.Food.IngredientCount("fish_crucian")); Assert.IsTrue(landed.KeepCaughtFish());
            using var kept = Reload(landed); Assert.IsFalse(kept.KeepCaughtFish()); Assert.AreEqual(1, kept.Backpack.Food.IngredientCount("fish_crucian"));
        }

        [Test]
        public void DecisionPauseOnlyHoldsItsOwnTimeAndClosingTheInterfaceReleasesIt()
        {
            using var s = Make(); Start(s); s.SetFishingInterfaceOpen(true); Bite(s);
            Assert.IsTrue(s.Time.IsHeld); s.Time.Hold(); s.Time.SetPaused(true);
            s.SetFishingInterfaceOpen(false); Assert.IsTrue(s.Time.IsHeld); Assert.IsTrue(s.Time.IsUserPaused);
            s.Time.Release(); Assert.IsFalse(s.Time.IsHeld); Assert.IsTrue(s.Time.IsUserPaused);
            s.SetFishingInterfaceOpen(true); Assert.IsTrue(s.Time.IsHeld);
            s.CancelCityActivity(s.RunningCityActivity.Id); Assert.IsFalse(s.Time.IsHeld); Assert.IsTrue(s.Time.IsUserPaused);
        }

        [Test]
        public void EndingASessionReleasesTheBiteAndDoesNotGrantUnclaimedFish()
        {
            using var s = Make(); var a = Start(s); s.SetFishingInterfaceOpen(true); Catch(s);
            Assert.IsTrue(s.Time.IsHeld); s.AdvanceClock(100);
            Assert.AreEqual(CityActivityPhase.Completed, a.State); Assert.IsFalse(s.FishingActive); Assert.IsFalse(s.Time.IsHeld);
            Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian")); Assert.That(a.SettlementSummary, Does.Contain("本次抛竿"));
        }

        [Test]
        public void KeptFishIsCompatibleWithCookingStorageAndIngredientNutrition()
        {
            var food = new Pantry(); food.RiceKg = 1; food.AddIngredient("fish_bass", 1);
            Assert.AreEqual(FoodBatch.NoExpiry, food.Ingredient.Single().ExpireDay);
            Assert.IsTrue(food.StartCook(.25, "fish_bass", 0, out string reason), reason);
            Assert.AreEqual(0, food.IngredientCount("fish_bass")); Assert.AreEqual("fish_bass", food.Cooking.Batch.Id);
            Assert.Greater(GameSession.MealNutrition("fish_bass"), 0);
            var bag = new Backpack(); bag.Food.AddIngredient("fish_carp", 1); Assert.AreEqual(1.2, bag.WeightKg, .001);
        }

        [TestCase(59, 0, 800)]
        [TestCase(59, 1, 1600)]
        [TestCase(60, 0, 1800)]
        [TestCase(60, 1, 3600)]
        public void FishingInvitationsUseActualSessionsAndTheConfirmedPayment(int number, int choice, int cost)
        {
            using var s = Make(); var balance = s.Ledger.Balance; var a = Invite(s, number, choice);
            Assert.AreEqual(balance - Money.FromYuan(cost), s.Ledger.Balance);
            Assert.AreEqual(GameSession.FishingVenueId, a.VenueId);
            if (number == 59) Assert.IsTrue(new GameDate((int)(a.ScheduledStart / 1440)).IsWeekend);
            Assert.IsTrue(s.IsMet(number == 59 ? "resident_shaoan" : "resident_qiaosong"));
        }

        [Test]
        public void FishGiftGoesToAnActualCompanionAndCannotAlsoEnterTheBackpack()
        {
            using var s = Make(); Invite(s, 59); Catch(s);
            Assert.Contains("resident_shaoan", s.FishingCompanions); Assert.IsTrue(s.GiveCaughtFish("resident_shaoan"));
            Assert.IsFalse(s.KeepCaughtFish()); Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian"));
            Catch(s); Assert.IsFalse(s.GiveCaughtFish("resident_shaoan")); Assert.IsTrue(s.ReleaseCaughtFish());
        }

        [Test]
        public void QiaosongChoicesGiveDifferentCompletionOutcomesAndRemainSaved()
        {
            using var s = Make(); var a = Invite(s, 60); s.Player.Intelligence = 50;
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); Assert.AreEqual(1, s.CurrentFishing.Style);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); Assert.AreEqual(1, s.CurrentFishing.Style);
            using var loaded = Reload(s); loaded.AdvanceClock(181);
            Assert.AreEqual(52, loaded.Player.Intelligence); Assert.IsFalse(loaded.Flags.Contains("fishing:qiaosong-story"));
            using var other = Make(); var story = Invite(other, 60); Assert.IsTrue(other.ChooseCityActivity(story.Id, 2));
            Assert.IsFalse(other.Flags.Contains("fishing:qiaosong-story")); other.AdvanceClock(181);
            Assert.IsTrue(other.Flags.Contains("fishing:qiaosong-story"));
        }
    }
}
