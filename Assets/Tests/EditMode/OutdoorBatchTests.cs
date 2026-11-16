using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class OutdoorBatchTests
    {
        private static readonly string[] Ids = { "act_fishing_grandpa", "act_hike_luqiao", "act_hike_gardener", "act_hike_shiyang", "act_snack_moxin", "act_snack_maoning", "act_snack_rider", "act_market_zhou", "act_market_li", "act_market_wang" };
        private static GameSession Make(bool shop = true, int? hikeClosingMinute = null)
        {
            using var fixture = DistrictProgressionTests.Make();
            var city = fixture.City;
            if (hikeClosingMinute.HasValue)
            {
                var layout = JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text);
                layout.places.Single(p => p.id == "life_hike").closeMinute = hikeClosingMinute.Value;
                city = CityLayout.FromData(layout);
            }
            using var first = new GameSession(city, 417, Money.FromYuan(1000000), GameTimestamp.From(new GameDate(42), 600),
                starterStock: false, smartphone: false, startWithShop: shop);
            first.Tick(0); var data = first.CreateSave(); data.districtProgress = new DistrictProgress { UnlockedTier = 3 };
            var s = GameSession.Load(first.City, data); s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100;
            s.Meet("zhou", 60); s.Meet("li", 60); s.Meet("wang", 60); return s;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveningHikeInvitationUsesActualWeekendSlotsOrSkipsThePopup(bool venueClosesTooEarly)
        {
            using var s = Make(hikeClosingMinute: venueClosesTooEarly ? 7 * 60 : (int?)null);
            s.Clock.Restore(new GameDate(15), 20 * 60 + 1);
            At(s, GameSession.FishingVenueId);
            Assert.That(s.GoTo(s.City.GetPlace("stall_cpu")), Is.True);
            var trip = s.Trip;
            Assert.That(trip, Is.Not.Null);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            s.RaiseEvent("act_hike_luqiao");
            TestContext.WriteLine($"Invitation generation took {timer.Elapsed.TotalMilliseconds:0.0} ms.");
            Assert.That(s.Trip, Is.SameAs(trip));
            if (venueClosesTooEarly)
            {
                Assert.That(s.PendingEvent, Is.Null);
                return;
            }

            Assert.That(s.PendingEvent, Is.Not.Null);
            var choices = s.PendingEvent.Choices.Take(4).ToArray();
            Assert.That(choices, Has.Length.EqualTo(4));
            Assert.That(choices.All(s.IsChoiceEnabled), Is.True);
            Assert.That(choices[0].Text.Split('·').Last(), Is.EqualTo(choices[1].Text.Split('·').Last()));
            Assert.That(choices[2].Text.Split('·').Last(), Is.EqualTo(choices[3].Text.Split('·').Last()));
            Assert.That(s.ResolveEvent(3), Is.True);
            var booking = s.CityActivities.Single(a => a.Active);
            var time = new GameTimestamp(booking.ScheduledStart);
            Assert.That(time.Date.IsWeekend, Is.True);
            Assert.That(time.MinuteOfDay + booking.Duration, Is.LessThanOrEqualTo(20 * 60));
            Assert.That(booking.Duration, Is.EqualTo(180));
            Assert.That(booking.PlayerTreat, Is.True);
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
            Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace));
            if (GameSession.IsOutdoorMarketVisit(a) || GameSession.IsInteractiveFishingActivity(a))
                Assert.IsTrue(s.StartVenueAppointment(a.Id), s.VenueAppointmentStartBlocker(a));
            s.AdvanceClock(3);
            Assert.AreEqual(CityActivityPhase.Running, a.State, a.EndReason);
        }

        [TestCase(65)]
        [TestCase(67)]
        public void NightMealPickerShowsOnlyTheSelectedPackagesPaymentOptionsAndBooksOnConfirmation(int number)
        {
            using var s = Make();
            s.RaiseEvent(Ids[number - 61]);
            var invitation = s.PendingEvent;
            Assert.That(invitation, Is.Not.Null);
            var initialTime = s.Clock.Now;
            var initialCash = s.Ledger.Balance;
            var root = new GameObject("NightMealPickerTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller, s);
            typeof(GameController).GetProperty("City").SetValue(controller, s.City);
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var ui = new GameUi(controller);
            Button[] Buttons() => ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>()).ToArray();
            Button Find(string name) => Buttons().Single(button => button.name == name);
            string Summary() => ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>())
                .Single(label => label.name == "EventAppointmentSummary").text;
            try
            {
                ui.ShowEvent(invitation);
                Assert.That(Summary(), Does.Contain("90 分钟 · 到场付 ¥480"));
                Find("EventAppointmentPayment_4").onClick.Invoke();
                Assert.That(Summary(), Does.Contain("对方同意后免费"));
                Assert.That(Find("EventAppointmentConfirm").GetComponentInChildren<Text>().text, Is.EqualTo("商量请客"));

                Find("EventAppointmentPayment_1").onClick.Invoke();
                Find("EventAppointmentPackage_1").onClick.Invoke();
                Assert.That(Buttons().Any(b => b.name == "EventAppointmentPayment_4"), Is.False);
                Assert.That(Summary(), Does.Contain("120 分钟 · 到场付 ¥2,400"));
                Assert.That(Buttons().Any(b => b.GetComponentInChildren<Text>()?.text == "改天再约"), Is.True);
                Assert.That(Buttons().Any(b => b.GetComponentInChildren<Text>()?.text == "这次不参加"), Is.True);
                Assert.That(s.PendingEvent, Is.SameAs(invitation));
                Assert.That(s.CityActivities.Any(a => a.Active), Is.False);
                Assert.That(s.Ledger.Balance, Is.EqualTo(initialCash));

                Find("EventAppointmentConfirm").onClick.Invoke();
                Assert.That(s.PendingEvent, Is.Null);
                var booking = s.CityActivities.Single(a => a.Active);
                Assert.That(s.CityActivityService(booking).Id, Is.EqualTo($"social_{number}_private"));
                Assert.That(booking.Duration, Is.EqualTo(120));
                Assert.That(booking.PlayerTreat, Is.True);
                Assert.That(booking.ScheduledStart, Is.EqualTo(invitation.Choices[3].Appointment.Value.Start));
                Assert.That(s.Ledger.Balance, Is.EqualTo(initialCash));
                Assert.That(s.Clock.Now, Is.EqualTo(initialTime));
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

        [TestCase(68)]
        [TestCase(69)]
        [TestCase(70)]
        public void FreeMarketVisitStartsAtReceptionAndWalksWithActivityWindowOpen(int number)
        {
            using var s = Make();
            s.EventsEnabled = false;
            var activity = Accept(s, number);
            s.Clock.Restore(new GameTimestamp(activity.ScheduledStart).Date, (int)(activity.ScheduledStart % 1440));
            At(s, activity.VenueId);
            s.Time.SetPaused(true);
            var cash = s.Ledger.Balance;
            var root = new GameObject("MarketVisitReceptionTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller, s);
            typeof(GameController).GetProperty("City").SetValue(controller, s.City);
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var ui = new GameUi(controller);
            Button Find(string name) => ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>())
                .Single(button => button.name == name);
            try
            {
                ui.OnEnteredPlace(s.CurrentPlace);
                var start = Find("StartVenueAppointment");
                Assert.That(start.interactable, Is.True);
                Assert.That(start.GetComponentInChildren<Text>().text, Does.Contain("免费"));
                Assert.That(ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>()).Select(label => label.text),
                    Has.None.EqualTo("选择这项服务"));
                Assert.That(s.Time.IsHeld, Is.True);
                s.AdvanceClock(1);
                Assert.That(activity.State, Is.EqualTo(CityActivityPhase.Gathering));

                var now = s.Clock.Now;
                start.onClick.Invoke();
                Assert.That(s.RunningCityActivity, Is.SameAs(activity));
                Assert.That(s.Clock.Now, Is.EqualTo(now));
                Assert.That(s.ManualActivityClockPaused, Is.True);
                Assert.That(ui.AnyModalOpen, Is.True);
                Assert.That(Find("CityActivityChoice_1").interactable, Is.False);

                for (int visited = 1; visited <= 3; visited++)
                {
                    OnsitePlayTests.StepManualActivity(s, 4);
                    Assert.That(s.OutdoorActivityState(activity.Id).Visited, Is.EqualTo(visited));
                    ui.RebuildWindows();
                    if (visited == 3) break;
                    var next = Find("CityActivityChoice_1");
                    Assert.That(next.interactable, Is.True);
                    now = s.Clock.Now;
                    next.onClick.Invoke();
                    Assert.That(s.Clock.Now, Is.EqualTo(now));
                    Assert.That(s.ManualActivityClockPaused, Is.True);
                    Assert.That(ui.AnyModalOpen, Is.True);
                }
                var startTime = s.Clock.Now;
                var finish = Find("FinishManualCityActivity");
                Assert.That(finish.interactable, Is.True);
                finish.onClick.Invoke();
                Assert.That(s.Clock.Now, Is.EqualTo(startTime.AddMinutes(activity.Duration)));
                Assert.That(activity.State, Is.EqualTo(CityActivityPhase.Completed));
                Assert.That(s.OutdoorActivityState(activity.Id).OrderCount, Is.Zero);
                Assert.That(s.Ledger.Balance, Is.EqualTo(cash));
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
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 1)); OnsitePlayTests.StepManualActivity(s, 6);
            Assert.AreEqual(1, s.OutdoorActivityState(a.Id).Visited); Assert.IsTrue(s.ChooseCityActivity(a.Id, 1));
            using var loaded = Reload(s); var saved = loaded.CityActivities.Single(x => x.Id == a.Id);
            OnsitePlayTests.StepManualActivity(loaded, 6); Assert.AreEqual(2, loaded.OutdoorActivityState(a.Id).Visited);
            Assert.IsTrue(loaded.ChooseCityActivity(a.Id, 1)); OnsitePlayTests.StepManualActivity(loaded, 6);
            Assert.AreEqual(3, loaded.OutdoorActivityState(a.Id).Visited); Assert.IsTrue(loaded.FinishManualCityActivity(a.Id));
            Assert.AreEqual(CityActivityPhase.Completed, saved.State); int relation = loaded.Relation(saved.Participants[0].PersonId);
            loaded.AdvanceClock(10); Assert.AreEqual(relation, loaded.Relation(saved.Participants[0].PersonId));
        }
        [Test]
        public void UnfinishedHikeDoesNotGrantCompletionRewards()
        {
            using var s = Make(); var a = Accept(s, 62); Attend(s, a); s.AdvanceClock(130);
            Assert.AreEqual(CityActivityPhase.Running, a.State);
            Assert.IsFalse(s.FinishManualCityActivity(a.Id));
            Assert.IsTrue(s.CancelCityActivity(a.Id, early: true));
            Assert.AreEqual(CityActivityPhase.Interrupted, a.State); Assert.IsFalse(a.RewardApplied);
        }
        [Test]
        public void PlantAnswersAreAtTheSiteAndAreDifferentAcrossAllThreeSamples()
        {
            using var s = Make(); var a = Accept(s, 64); Attend(s, a);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 1)); OnsitePlayTests.StepManualActivity(s, 6);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); Assert.IsFalse(s.ChooseCityActivity(a.Id, 2));
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); OnsitePlayTests.StepManualActivity(s, 6);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); // Wrong answer on sample 2 remains a real choice.
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); OnsitePlayTests.StepManualActivity(s, 6); Assert.IsTrue(s.ChooseCityActivity(a.Id, 3));
            Assert.AreEqual(2, s.OutdoorActivityState(a.Id).Correct); int before = s.Player.Intelligence;
            Assert.IsTrue(s.FinishManualCityActivity(a.Id)); Assert.AreEqual(before + 2, s.Player.Intelligence);
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
            Assert.IsTrue(s.OutdoorActivityState(a.Id).Delivered); Assert.IsTrue(s.FinishManualCityActivity(a.Id)); Assert.AreEqual(CityActivityPhase.Completed, a.State);
        }
        [Test]
        public void ComparisonNeedsThreeSameProductQuotesAndLowStatsCanStillTryAndFailNegotiating()
        {
            using var s = Make(); var a = Accept(s, 69); Attend(s, a);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 2));
            for (int i = 0; i < 3; i++) { OnsitePlayTests.StepManualActivity(s, 4); if (i < 2) Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); }
            var state = s.OutdoorActivityState(a.Id); Assert.AreEqual(3, state.Visited); Assert.AreEqual(2, state.QuoteCents.Length);
            Assert.AreNotEqual(state.QuoteSources[0], state.QuoteSources[1]); Assert.IsFalse(s.ChooseCityActivity(a.Id, 4));
            int best = Array.IndexOf(state.QuoteCents, state.QuoteCents.Min()); Assert.IsTrue(s.ChooseCityActivity(a.Id, best + 2));
            s.Player.Intelligence = 1; Assert.IsTrue(s.ChooseCityActivity(a.Id, 5)); Assert.IsTrue(s.OutdoorActivityState(a.Id).Negotiated);
        }
        [Test]
        public void GpuTestTakesTimeThenPurchaseCreatesRealDeliveryAndCannotDuplicate()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); OnsitePlayTests.StepManualActivity(s, 4);
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2));
            Assert.IsFalse(s.OutdoorActivityState(a.Id).Tested); OnsitePlayTests.StepManualActivity(s, 5); Assert.IsTrue(s.OutdoorActivityState(a.Id).Tested);
            var cash = s.Ledger.Balance; Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3));
            Assert.Less(s.Ledger.Balance.Cents, cash.Cents); Assert.AreEqual(3, s.OutdoorActivityState(a.Id).OrderCount);
            Assert.IsTrue(s.Couriers.Any(c => c.Items.Any(i => i.Id == "gpu1" && i.Count == 3)));
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3));
        }
        [Test]
        public void ComparisonCanPurchaseFromTheActualOnlineSourceInsteadOfTheLocalStall()
        {
            using var s = Make(); s.SetSmartphone(true); var a = Accept(s, 69); Attend(s, a);
            for (int i = 0; i < 3; i++) { OnsitePlayTests.StepManualActivity(s, 4); if (i < 2) s.ChooseCityActivity(a.Id, 1); }
            Assert.IsTrue(s.SelectOutdoorPurchaseSource(a.Id, 1));
            Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 1));
            Assert.IsTrue(s.Online.Pending.Any(o => o.Lines.Any(l => l.PartId == "gpu1" && l.Count == 1)));
        }
        [Test]
        public void AThirdComparisonSourceRequiresTheRealSignedSupplyAgreement()
        {
            using var s = Make(); s.SetSmartphone(true); s.MainShop.Sim.Reputation = 30; s.Meet("zhou", 80);
            Assert.IsTrue(s.SignDirectAgreement()); var a = Accept(s, 69); Attend(s, a);
            for (int i = 0; i < 3; i++) { OnsitePlayTests.StepManualActivity(s, 4); if (i < 2) s.ChooseCityActivity(a.Id, 1); }
            var state = s.OutdoorActivityState(a.Id); Assert.AreEqual(3, state.QuoteCents.Length); Assert.AreEqual("direct:zhou", state.QuoteSources[2]);
            Assert.IsTrue(s.SelectOutdoorPurchaseSource(a.Id, 2)); Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3));
            Assert.AreEqual(3, s.OutdoorActivityState(a.Id).OrderCount);
        }
        [Test]
        public void GpuCreditIsAnActualSavedDebtAndDeliveryAndChangingModelsRequiresRetesting()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); OnsitePlayTests.StepManualActivity(s, 4);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); OnsitePlayTests.StepManualActivity(s, 5);
            Assert.IsTrue(s.SelectOutdoorMarketPart(a.Id, "gpu0")); Assert.IsFalse(s.OutdoorActivityState(a.Id).Tested);
            Assert.IsFalse(s.OrderOutdoorMarketGoods(a.Id, 3)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); OnsitePlayTests.StepManualActivity(s, 5);
            s.Player.Intelligence = 100; s.Meet("wang", 100); Assert.IsTrue(s.ChooseCityActivity(a.Id, 3));
            Assert.IsTrue(s.OutdoorActivityState(a.Id).CreditGranted);
            var price = s.OutdoorMarketGoodsPrice(a.Id, 3); var cash = s.Ledger.Balance; var debts = s.SupplierDebtTotal;
            Assert.IsTrue(s.OrderOutdoorMarketGoods(a.Id, 3, true));
            Assert.AreEqual(cash - price.Scale(.3), s.Ledger.Balance); Assert.AreEqual(debts + price - price.Scale(.3), s.SupplierDebtTotal);
            using var loaded = Reload(s); Assert.AreEqual(s.SupplierDebtTotal, loaded.SupplierDebtTotal);
            Assert.IsFalse(loaded.OrderOutdoorMarketGoods(a.Id, 3, true));
            Assert.IsTrue(loaded.CancelCityActivity(a.Id, early: true));
            loaded.Clock.Restore(loaded.Clock.Today.AddDays(1), 599); loaded.AdvanceClock(1);
            Assert.AreEqual(3, loaded.MainShop.Sim.Stock.Count(PCShopSell.Core.Catalog.PartCatalog.Get("gpu0"), true));
        }
        [Test]
        public void LowAttributesCanChooseGpuCreditNegotiationButMustFailWithoutDebt()
        {
            using var s = Make(); var a = Accept(s, 70); Attend(s, a); OnsitePlayTests.StepManualActivity(s, 4);
            s.ChooseCityActivity(a.Id, 2); OnsitePlayTests.StepManualActivity(s, 5); s.Player.Intelligence = 1; var cash = s.Ledger.Balance;
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
