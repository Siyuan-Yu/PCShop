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
    public sealed class OutdoorBatchTests
    {
        private static readonly string[] Ids = { "act_fishing_grandpa", "act_hike_luqiao", "act_hike_gardener", "act_hike_shiyang", "act_snack_moxin", "act_snack_maoning", "act_snack_rider", "act_market_zhou", "act_market_li", "act_market_wang" };
        private static GameSession Make(bool shop = true)
        {
            using var fixture = DistrictProgressionTests.Make();
            using var first = new GameSession(fixture.City, 417, Money.FromYuan(1000000), GameTimestamp.From(new GameDate(42), 600),
                starterStock: false, smartphone: false, startWithShop: shop);
            first.Tick(0); var data = first.CreateSave(); data.districtProgress = new DistrictProgress { UnlockedTier = 3 };
            var s = GameSession.Load(first.City, data); s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100;
            s.Meet("zhou", 60); s.Meet("li", 60); s.Meet("wang", 60); return s;
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void At(GameSession s, string place)
        {
            s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(place));
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(place).NodeId);
        }
        private static CityActivity Accept(GameSession s, int n, int choice = 0)
        {
            s.RaiseEvent(Ids[n - 61]); Assert.NotNull(s.PendingEvent, Ids[n - 61] + " hasComputer=" + s.HasOpenedComputerShop
                + " shops=" + string.Join(",", s.MyShops.Select(x => x.RetailKind + ":" + x.Sim.NotFitted))
                + " person=" + (s.CityPersonState("wang") != null) + " access=" + s.DistrictAccessBlocker(s.City.GetPlace("stall_gpu")));
            Assert.IsTrue(s.PendingEvent.Choices[choice].Enabled == null || s.PendingEvent.Choices[choice].Enabled(s), s.PendingEvent.Body);
            Assert.IsTrue(s.ResolveEvent(choice)); return s.CityActivities.Last(a => a.Active);
        }
        private static void Attend(GameSession s, CityActivity a)
        {
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); At(s, a.VenueId);
            Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace)); s.AdvanceClock(3);
            Assert.AreEqual(CityActivityPhase.Running, a.State, a.EndReason);
        }
        [TestCase(61, 0, 300)] [TestCase(62, 0, 900)] [TestCase(63, 0, 0)] [TestCase(64, 0, 0)]
        [TestCase(65, 0, 480)] [TestCase(66, 1, 0)] [TestCase(67, 0, 480)] [TestCase(68, 0, 0)] [TestCase(69, 0, 0)] [TestCase(70, 0, 0)]
        public void InvitationsDoNotChargeOrGrantContactUntilRealAttendance(int number, int choice, int cost)
        {
            using var s = Make(); var cash = s.Ledger.Balance; int intelligence = s.Player.Intelligence;
            var a = Accept(s, number, choice); Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(intelligence, s.Player.Intelligence);
            string person = a.Participants.Single().PersonId; bool met = s.IsMet(person);
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Id == a.Id);
            Assert.AreEqual(met, loaded.IsMet(person)); Attend(loaded, saved);
            Assert.IsTrue(loaded.IsMet(person)); Assert.AreEqual(cash - Money.FromYuan(cost), loaded.Ledger.Balance);
            if (number == 61) Assert.IsTrue(loaded.FishingActive);
        }
        [Test]
        public void GpuInvitationRequiresAnActuallyOpenedComputerShopAndChecksAgainAtStart()
        {
            using var noShop = Make(false); noShop.RaiseEvent(Ids[9]); Assert.IsNull(noShop.PendingEvent);
            using var s = Make(); s.MainShop.RetailKind = "grocery"; s.RaiseEvent(Ids[9]); Assert.IsNull(s.PendingEvent);
            s.MainShop.RetailKind = "computer"; s.MainShop.Sim.NotFitted = true; s.RaiseEvent(Ids[9]); Assert.IsNull(s.PendingEvent);
            s.MainShop.Sim.NotFitted = false; var a = Accept(s, 70); var cash = s.Ledger.Balance;
            s.MainShop.RetailKind = "grocery";
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); At(s, a.VenueId);
            s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3);
            Assert.AreEqual(CityActivityPhase.Blocked, a.State); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [Test]
        public void HikeNeedsActualWalkingAndContinuesAfterReloadWithoutDuplicateRewards()
        {
            using var s = Make(); var a = Accept(s, 62); Attend(s, a);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 1)); s.AdvanceClock(6);
            Assert.AreEqual(1, s.OutdoorActivityState(a.Id).Visited); Assert.IsTrue(s.ChooseCityActivity(a.Id, 1));
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Id == a.Id);
            loaded.AdvanceClock(6); Assert.AreEqual(2, loaded.OutdoorActivityState(a.Id).Visited);
            Assert.IsTrue(loaded.ChooseCityActivity(a.Id, 1)); loaded.AdvanceClock(6);
            Assert.AreEqual(3, loaded.OutdoorActivityState(a.Id).Visited); loaded.AdvanceClock(130);
            Assert.AreEqual(CityActivityPhase.Completed, saved.State); int relation = loaded.Relation(saved.Participants[0].PersonId);
            loaded.AdvanceClock(10); Assert.AreEqual(relation, loaded.Relation(saved.Participants[0].PersonId));
        }
        [Test]
        public void UnfinishedHikeDoesNotGrantCompletionRewards()
        {
            using var s = Make(); var a = Accept(s, 62); Attend(s, a); s.AdvanceClock(130);
            Assert.AreEqual(CityActivityPhase.Interrupted, a.State); Assert.IsFalse(a.RewardApplied);
        }
        [Test]
        public void PlantAnswersAreAtTheSiteAndAreDifferentAcrossAllThreeSamples()
        {
            using var s = Make(); var a = Accept(s, 64); Attend(s, a);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 1)); s.AdvanceClock(6);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); Assert.IsFalse(s.ChooseCityActivity(a.Id, 2));
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); s.AdvanceClock(6);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); // Wrong answer on sample 2 remains a real choice.
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); s.AdvanceClock(6); Assert.IsTrue(s.ChooseCityActivity(a.Id, 3));
            Assert.AreEqual(2, s.OutdoorActivityState(a.Id).Correct); int before = s.Player.Intelligence;
            s.AdvanceClock(100); Assert.AreEqual(before + 2, s.Player.Intelligence);
        }
        [Test]
        public void GuardDeliveryConsumesRealFoodOnceWithoutTakingTheGuardAway()
        {
            using var s = Make(); var a = Accept(s, 66, 1); Attend(s, a);
            Assert.AreEqual(s.CityPersonState("resident_maoning").WorkPlaceId, a.VenueId);
            s.Backpack.Food.Packed.Clear();
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 1));
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "fast:rice", Name = "鸡肉蔬菜饭", Count = 1, ExpireDay = s.Clock.Today.TotalDays + 2, Hunger = 65 });
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); Assert.IsFalse(s.Backpack.Food.Packed.Any(b => b.Id == "fast:rice"));
            Assert.IsTrue(s.OutdoorActivityState(a.Id).Delivered); s.AdvanceClock(25); Assert.AreEqual(CityActivityPhase.Completed, a.State);
        }
        [Test]
        public void ComparisonNeedsThreeSameProductQuotesAndLowStatsCanStillTryAndFailNegotiating()
        {
            using var s = Make(); var a = Accept(s, 69); Attend(s, a);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 2));
            for (int i = 0; i < 3; i++) { s.AdvanceClock(4); if (i < 2) Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); }
            var state = s.OutdoorActivityState(a.Id); Assert.AreEqual(3, state.Visited); Assert.AreEqual(2, state.QuoteCents.Length);
            Assert.AreNotEqual(state.QuoteSources[0], state.QuoteSources[1]); Assert.IsFalse(s.ChooseCityActivity(a.Id, 4));
            int best = Array.IndexOf(state.QuoteCents, state.QuoteCents.Min()); Assert.IsTrue(s.ChooseCityActivity(a.Id, best + 2));
            s.Player.Intelligence = 1; Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); Assert.IsTrue(s.OutdoorActivityState(a.Id).Negotiated);
        }
        [Test]
        public void GpuTestTakesTimeThenPurchaseCreatesRealDeliveryAndCannotDuplicate()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); s.AdvanceClock(4);
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2));
            Assert.IsFalse(s.OutdoorActivityState(a.Id).Tested); s.AdvanceClock(5); Assert.IsTrue(s.OutdoorActivityState(a.Id).Tested);
            var cash = s.Ledger.Balance; Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3));
            Assert.Less(s.Ledger.Balance.Cents, cash.Cents); Assert.AreEqual(3, s.OutdoorActivityState(a.Id).OrderCount);
            Assert.IsTrue(s.Couriers.Any(c => c.Items.Any(i => i.Id == "gpu1" && i.Count == 3)));
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3));
        }
        [Test]
        public void ComparisonCanPurchaseFromTheActualOnlineSourceInsteadOfTheLocalStall()
        {
            using var s = Make(); s.SetSmartphone(true); var a = Accept(s, 69); Attend(s, a);
            for (int i = 0; i < 3; i++) { s.AdvanceClock(4); if (i < 2) s.ChooseCityActivity(a.Id, 1); }
            Assert.IsTrue(s.SelectOutdoorPurchaseSource(a.Id, 1));
            Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 1));
            Assert.IsTrue(s.Online.Pending.Any(o => o.Lines.Any(l => l.PartId == "gpu1" && l.Count == 1)));
        }
        [Test]
        public void AThirdComparisonSourceRequiresTheRealSignedSupplyAgreement()
        {
            using var s = Make(); s.SetSmartphone(true); s.MainShop.Sim.Reputation = 30; s.Meet("zhou", 80);
            Assert.IsTrue(s.SignDirectAgreement()); var a = Accept(s, 69); Attend(s, a);
            for (int i = 0; i < 3; i++) { s.AdvanceClock(4); if (i < 2) s.ChooseCityActivity(a.Id, 1); }
            var state = s.OutdoorActivityState(a.Id); Assert.AreEqual(3, state.QuoteCents.Length); Assert.AreEqual("direct:zhou", state.QuoteSources[2]);
            Assert.IsTrue(s.SelectOutdoorPurchaseSource(a.Id, 2)); Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3));
            Assert.AreEqual(3, s.OutdoorActivityState(a.Id).OrderCount);
        }
        [Test]
        public void GpuCreditIsAnActualSavedDebtAndDeliveryAndChangingModelsRequiresRetesting()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); s.AdvanceClock(4);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); s.AdvanceClock(5);
            Assert.IsTrue(s.SelectOutdoorMarketPart(a.Id, "gpu0")); Assert.IsFalse(s.OutdoorActivityState(a.Id).Tested);
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); s.AdvanceClock(5);
            s.Player.Intelligence = 100; s.Meet("wang", 100); Assert.IsTrue(s.ChooseCityActivity(a.Id, 3));
            Assert.IsTrue(s.OutdoorActivityState(a.Id).CreditGranted);
            var price = s.OutdoorMarketGoodsPrice(a.Id, 3); var cash = s.Ledger.Balance; var debts = s.SupplierDebtTotal;
            Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3, true));
            Assert.AreEqual(cash - price.Scale(.3), s.Ledger.Balance); Assert.AreEqual(debts + price - price.Scale(.3), s.SupplierDebtTotal);
            using var loaded = Reload(s); Assert.AreEqual(s.SupplierDebtTotal, loaded.SupplierDebtTotal);
            Assert.IsFalse(loaded.OrderOutdoorMarketGoods(a.Id, 3, true));
            loaded.Clock.Restore(loaded.Clock.Today.AddDays(1), 599); loaded.AdvanceClock(1);
            Assert.AreEqual(3, loaded.MainShop.Sim.Stock.Count(PCShopSell.Core.Catalog.PartCatalog.Get("gpu0"), true));
        }
        [Test]
        public void LowAttributesCanChooseGpuCreditNegotiationButMustFailWithoutDebt()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); s.AdvanceClock(4);
            s.ChooseCityActivity(a.Id, 2); s.AdvanceClock(5); s.Player.Intelligence = 1; var cash = s.Ledger.Balance;
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 3)); Assert.IsFalse(s.OutdoorActivityState(a.Id).CreditGranted);
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3, true)); Assert.AreEqual(Money.Zero, s.SupplierDebtTotal); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [TestCase(61, 1, 600)] [TestCase(62, 1, 1800)] [TestCase(62, 2, 2200)] [TestCase(65, 1, 960)] [TestCase(65, 3, 2400)] [TestCase(67, 3, 2400)]
        public void TreatAndPremiumVariantsChargeTheirConfirmedAmounts(int n, int choice, int amount)
        {
            using var s = Make(); var a = Accept(s, n, choice); var cash = s.Ledger.Balance; Attend(s, a);
            Assert.AreEqual(cash - Money.FromYuan(amount), s.Ledger.Balance);
        }
    }
}
