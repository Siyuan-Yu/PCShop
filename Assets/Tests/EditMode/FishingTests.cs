using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

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
            Assert.IsTrue(s.FishingActive); return s.RunningCityActivity;
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
            WaitForBite(s);
        }
        private static void WaitForBite(GameSession s)
        {
            for (int i = 0; i < 8 && s.CurrentFishing.Phase == FishingPhase.Waiting; i++)
                s.AdvanceFishingInteraction(.9, false);
            Assert.AreEqual(FishingPhase.Bite, s.CurrentFishing.Phase);
        }
        private static void UseRemainingCasts(GameSession s)
        {
            while (s.ManualAttemptsLeft > 0)
            {
                Cast(s);
                WaitForBite(s);
                for (int i = 0; i < 4; i++) s.AdvanceFishingInteraction(1, false);
                Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase);
            }
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
        private static CityActivity BookFishing(GameSession s, int number, int choice)
        {
            s.RaiseEvent(number switch
            {
                59 => "act_fishing_shaoan",
                60 => "act_fishing_qiaosong",
                61 => "act_fishing_grandpa",
                _ => throw new ArgumentOutOfRangeException(nameof(number))
            });
            Assert.NotNull(s.PendingEvent);
            string body = s.PendingEvent.Body; Assert.IsTrue(s.ResolveEvent(choice), body);
            return s.CityActivities.Last(x => x.Active);
        }

        private static CityActivity Invite(GameSession s, int number, int choice = 0)
        {
            var a = BookFishing(s, number, choice);
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440));
            At(s, a.VenueId);
            Assert.IsTrue(s.StartVenueAppointment(a.Id), s.VenueAppointmentStartBlocker(a));
            s.AdvanceClock(3);
            Assert.IsTrue(s.FishingActive); return a;
        }

        private static System.Collections.Generic.IEnumerable<TestCaseData> EveryFish => FishingCatalog.Fish.Select(f => new TestCaseData(f.Id));
        [TestCaseSource(nameof(EveryFish))]
        public void EverySpeciesCanBeCaughtKeptReloadedAndSold(string fishId)
        {
            var fish = FishingCatalog.Get(fishId);
            using var s = Make(); Start(s); s.SelectFishingSpot(fish.Habitat); s.AdvanceClock(2); s.SelectFishingBait(fish.Bait);
            double startingWeight = s.Backpack.WeightKg;
            Bite(s, fish.Habitat == 0 ? .2 : fish.Habitat == 1 ? .7 : 1, fishId); PlayFight(s);
            Assert.AreEqual(fishId, s.CurrentFishing.PendingFishId); Assert.IsTrue(s.KeepCaughtFish());
            using var loaded = Reload(s);
            Assert.AreEqual(1, loaded.Backpack.Food.IngredientCount(fishId)); Assert.AreEqual(startingWeight + fish.Kg, loaded.Backpack.WeightKg, .001);
            Assert.IsNotNull(Ingredients.Get(fishId)); Assert.Greater(Ingredients.Get(fishId).Price.Yuan, fish.SalePrice);
            Assert.IsNull(Ingredients.GetRiceCookerRecipe(fishId));
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
            s.AdvanceFishingInteraction(.81, false); WaitForBite(s);
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
        public void ClosingDuringFightDropsTheHookWithoutResumingTimeOrReleasingAnotherHold()
        {
            using var s = Make(); Start(s); Bite(s); s.HookFishingFish();
            s.Time.Hold(); s.CloseFishingInterface();
            Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase); Assert.IsTrue(s.Time.IsHeld);
            s.Time.Release(); Assert.IsFalse(s.Time.IsHeld); Assert.IsTrue(s.ManualActivityClockPaused); Assert.AreEqual(0, s.FishCaughtTotal);
        }

        [TestCase("fishing", 2, 300)]
        [TestCase("fishing_pass_4h", 4, 500)]
        [TestCase("fishing_pass_6h", 6, 700)]
        [TestCase("fishing_pass_8h", 8, 900)]
        public void VenueIsReachableInTheOutskirtsAndARealPaidSessionStartsAtItsDock(string passId, int hours, int price)
        {
            using var s = Make(); var p = s.City.GetPlace(GameSession.FishingVenueId);
            Assert.AreEqual(0, s.DistrictTierOf(p)); Assert.IsNull(s.DistrictAccessBlocker(p));
            Assert.That(p.Name, Does.Contain("垂钓场")); var balance = s.Ledger.Balance;
            Assert.True(s.SelectVenueService(passId, 0));
            var a = s.RunningCityActivity;
            Assert.True(s.FishingActive);
            Assert.AreEqual(balance - Money.FromYuan(price), s.Ledger.Balance);
            Assert.AreEqual(12, s.CurrentFishing.Baits.Sum()); Assert.AreEqual(hours * 60, a.Duration);
            Assert.AreEqual(hours / 2 * 3, s.ManualAttemptsLeft);
            Assert.IsTrue(a.PlayerVenueRoute.HasPath); Assert.IsTrue(s.Backpack.Food.Ingredient.All(b => FishingCatalog.Get(b.Id) == null));
        }

        [TestCase(0, 0, 480, 900)]
        [TestCase(60, 0, 180, 1800)]
        [TestCase(61, 0, 90, 300)]
        [TestCase(61, 1, 90, 600)]
        public void PassPickerOpensManualFishingForWalkInsAndConfirmedInvitations(int invitation, int option, int duration, int cost)
        {
            using var s = Make();
            var booking = invitation == 0 ? null : BookFishing(s, invitation, option);
            var cash = s.Ledger.Balance;
            if (booking != null)
            {
                var time = new GameTimestamp(booking.ScheduledStart);
                s.Clock.Restore(time.Date, time.MinuteOfDay);
                var venue = s.CurrentPlace;
                s.Leave();
                s.ArriveAtNode(venue.NodeId);
                Assert.IsTrue(s.Enter(venue, booking.Id));
                s.AdvanceClock(3);
                Assert.IsFalse(booking.EntryPaid);
                Assert.IsFalse(s.FishingActive);
                Assert.AreEqual(cash, s.Ledger.Balance);
            }
            var root = new GameObject("FishingPassPickerTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller, s);
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var ui = new GameUi(controller);
            try
            {
                ui.OnEnteredPlace(s.CurrentPlace, booking?.Id);
                var windows = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Transform>())
                    .Where(item => item.name.StartsWith("Window_")).ToArray();
                Assert.That(windows, Has.Length.EqualTo(1));
                Assert.That(windows[0].GetComponentsInChildren<RectTransform>().Any(item => item.name == "People"));
                Assert.That(windows[0].GetComponentsInChildren<RectTransform>().Any(item => item.name == "PlaceGoodsScroll"));
                var passes = windows[0].GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("FishingPass_")).ToArray();
                Assert.That(passes, Has.Length.EqualTo(booking == null ? 4 : 1));
                string passName = booking == null ? "FishingPass_fishing_pass_8h" : "FishingPass_" + booking.DefinitionId;
                var entry = passes.Single(button => button.name == passName);
                Assert.IsTrue(entry.interactable);
                StringAssert.Contains($"{CityLifeCatalog.ManualPlayAttempts(duration)} 次", entry.transform.parent.GetComponentsInChildren<Text>()
                    .Single(label => label.text.Contains("全部用完")).text);
                Assert.AreEqual(36, entry.GetComponent<LayoutElement>().preferredHeight);
                if (booking != null) StringAssert.Contains("付款并开始钓鱼", entry.GetComponentInChildren<Text>().text);
                entry.onClick.Invoke();
                Assert.True(ui.FishingInputActive);
                Assert.AreEqual(duration, s.RunningCityActivity.Duration);
                Assert.AreEqual(cash - Money.FromYuan(cost), s.Ledger.Balance);
                if (booking != null)
                {
                    Assert.AreEqual(booking.Id, s.RunningCityActivity.Id);
                    Assert.IsFalse(s.StartVenueAppointment(booking.Id));
                    Assert.AreEqual(cash - Money.FromYuan(cost), s.Ledger.Balance);
                }
                Assert.AreEqual(0, s.FishCaughtTotal);
                Assert.False(ui.CloseTopWindow(), "Fishing should only exit through its own controls.");
                if (booking == null)
                {
                    Catch(s);
                    var caught = FishingCatalog.Get(s.CurrentFishing.PendingFishId);
                    typeof(GameUi).GetMethod("UpdateFishingInteraction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { 0f });
                    var result = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<RectTransform>())
                        .Single(rect => rect.name == "FishingCatchResult");
                    Assert.That(result.GetComponentsInChildren<Text>().Select(label => label.text), Does.Contain(caught.Name));
                    var keep = result.GetComponentsInChildren<Button>().Single(button => button.GetComponentInChildren<Text>().text == "带走 · 放入背包");
                    Assert.IsTrue(keep.interactable);
                    keep.onClick.Invoke();
                    Assert.AreEqual(FishingPhase.Ready, s.CurrentFishing.Phase);
                    Assert.AreEqual(1, s.Backpack.Food.IngredientCount(caught.Id));

                    var finish = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>())
                        .Single(button => button.GetComponentInChildren<Text>()?.text == "收竿并结算");
                    finish.onClick.Invoke();
                    Assert.IsFalse(ui.FishingInputActive);
                    Assert.That(ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>())
                        .Any(label => label.text.Contains("钓获 1 条") && label.text.Contains("带走 1")));
                }
            }
            finally
            {
                ui.CloseAllWindows();
                foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
            }
        }

        [Test]
        public void CastUsesRealSecondsAndConsumesAnAttemptEvenWhenTheFishEscapes()
        {
            using var s = Make(); Start(s); int count = s.CurrentFishing.Baits[0]; long now = s.Clock.Now.TotalMinutes;
            Cast(s); Assert.Greater(s.CurrentFishing.WaitSeconds, 0); Assert.AreEqual(count - 1, s.CurrentFishing.Baits[0]);
            Assert.AreEqual(2, s.ManualAttemptsLeft); Assert.IsFalse(s.HookFishingFish());
            s.Time.SetSpeed(8); Assert.Zero(s.Tick(300)); s.AdvanceClock(300);
            Assert.AreEqual(FishingPhase.Waiting, s.CurrentFishing.Phase);
            WaitForBite(s);
            for (int i = 0; i < 4; i++) s.AdvanceFishingInteraction(1, false);
            Assert.AreEqual(now, s.Clock.Now.TotalMinutes);
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
            s.AdvanceFishingInteraction(1, false);
            var waiting = s.CurrentFishing; using var loaded = Reload(s);
            Assert.AreEqual(waiting.WaitSeconds, loaded.CurrentFishing.WaitSeconds); Assert.AreEqual(waiting.SpeciesRoll, loaded.CurrentFishing.SpeciesRoll);
            Assert.AreEqual(waiting.PhaseSeconds, loaded.CurrentFishing.PhaseSeconds);
            Assert.AreEqual(2, loaded.ManualAttemptsLeft);
            WaitForBite(loaded);
            PlayFight(loaded);
            using var landed = Reload(loaded); Assert.AreEqual(FishingPhase.Landed, landed.CurrentFishing.Phase);
            Assert.AreEqual(0, landed.Backpack.Food.IngredientCount("fish_crucian")); Assert.IsTrue(landed.KeepCaughtFish());
            using var kept = Reload(landed); Assert.IsFalse(kept.KeepCaughtFish()); Assert.AreEqual(1, kept.Backpack.Food.IngredientCount("fish_crucian"));
        }

        [Test]
        public void EarlySettlementChargesUsedTimeOnceAndPreservesOtherPauses()
        {
            using var s = Make(); var a = Start(s); Bite(s);
            s.Time.Hold(); s.Time.SetPaused(true);
            s.CloseFishingInterface();
            Assert.IsTrue(s.CancelCityActivity(a.Id, true));
            Assert.IsFalse(s.CancelCityActivity(a.Id, true));
            Assert.AreEqual(a.ActualStart + 40, s.Clock.Now.TotalMinutes);
            Assert.IsFalse(s.ManualActivityClockPaused); Assert.IsTrue(s.Time.IsHeld); Assert.IsTrue(s.Time.IsUserPaused);
            s.Time.Release(); Assert.IsFalse(s.Time.IsHeld);
        }

        [Test]
        public void QiaosongEarlySettlementGrantsEightyPercentOfTheBaseAffinityOnce()
        {
            using var s = Make();
            var activity = Invite(s, 60);
            var participant = activity.Participants.Single();
            double fullReward = s.CityActivityService(activity).Service(activity.ServiceChoice).Relation;
            Assert.That(fullReward, Is.GreaterThan(0));
            for (int i = 0; i < 4; i++)
            {
                Cast(s);
                WaitForBite(s);
                for (int j = 0; j < 4; j++) s.AdvanceFishingInteraction(1, false);
            }

            Assert.That(s.CancelCityActivity(activity.Id, true), Is.True);
            Assert.That(activity.State, Is.EqualTo(CityActivityPhase.Interrupted));
            Assert.That(activity.HasPartialSettlement, Is.True);
            Assert.That(activity.PartialSettlementProgress, Is.EqualTo(.8).Within(1e-9));
            Assert.That(participant.RelationAfter - participant.RelationBefore, Is.EqualTo(fullReward * .8).Within(1e-9));
            Assert.That(participant.TrustAfter, Is.EqualTo(participant.TrustBefore));
            Assert.That(s.CancelCityActivity(activity.Id, true), Is.False);
            using var loaded = Reload(s);
            var settled = loaded.CityActivities.Single(item => item.Id == activity.Id);
            Assert.That(settled.PartialSettlementProgress, Is.EqualTo(.8).Within(1e-9));
            Assert.That(settled.Participants.Single().RelationAfter, Is.EqualTo(participant.RelationAfter).Within(1e-9));
        }

        [Test]
        public void EndingASessionReleasesTheBiteAndDoesNotGrantUnclaimedFish()
        {
            using var s = Make(); var a = Start(s); Catch(s);
            Assert.IsTrue(s.ManualActivityClockPaused); s.AdvanceClock(a.Duration + 1);
            Assert.AreEqual(FishingPhase.Landed, s.CurrentFishing.Phase);
            Assert.IsTrue(s.CancelCityActivity(a.Id, true));
            Assert.AreEqual(CityActivityPhase.Completed, a.State); Assert.IsFalse(s.FishingActive);
            Assert.AreEqual(0, s.Backpack.Food.IngredientCount("fish_crucian")); Assert.That(a.SettlementSummary, Does.Contain("本次抛竿"));
        }

        [Test]
        public void FinalCatchCanBeKeptBeforeSettlingTheFullPass()
        {
            using var s = Make(); var a = Start(s);
            for (int i = 0; i < 2; i++) { Bite(s); for (int j = 0; j < 4; j++) s.AdvanceFishingInteraction(1, false); }
            Catch(s);
            Assert.AreEqual(0, s.ManualAttemptsLeft);
            Assert.AreEqual(a.ActualStart, s.Clock.Now.TotalMinutes);
            Assert.IsTrue(s.KeepCaughtFish());
            Assert.IsFalse(s.BeginFishingCast());
            Assert.IsTrue(s.CancelCityActivity(a.Id, true));
            Assert.AreEqual(a.ActualStart + 120, s.Clock.Now.TotalMinutes);
            Assert.AreEqual(1, s.Backpack.Food.IngredientCount("fish_crucian"));
        }

        [Test]
        public void TryingToCookFishLeavesRiceAndCatchInStorage()
        {
            var food = new Pantry(); food.RiceKg = 1; food.AddIngredient("fish_bass", 1);
            Assert.AreEqual(FoodBatch.NoExpiry, food.Ingredient.Single().ExpireDay);
            Assert.IsFalse(food.StartCook(.25, "fish_bass", 0, out _));
            Assert.AreEqual(1, food.IngredientCount("fish_bass"));
            Assert.AreEqual(1, food.RiceKg);
            Assert.IsNull(food.Cooking);
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
            using var loaded = Reload(s); UseRemainingCasts(loaded); Assert.True(loaded.CancelCityActivity(a.Id, true));
            Assert.AreEqual(52, loaded.Player.Intelligence); Assert.IsFalse(loaded.Flags.Contains("fishing:qiaosong-story"));
            using var other = Make(); var story = Invite(other, 60); Assert.IsTrue(other.ChooseCityActivity(story.Id, 2));
            Assert.IsFalse(other.Flags.Contains("fishing:qiaosong-story")); UseRemainingCasts(other); Assert.True(other.CancelCityActivity(story.Id, true));
            Assert.IsTrue(other.Flags.Contains("fishing:qiaosong-story"));
        }
    }
}
