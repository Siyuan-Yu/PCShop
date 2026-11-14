using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class MoneyEventBatchTests
    {
        private static GameSession Make()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 1121, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 660), false, true, true);
            s.EventsEnabled = false; s.Tick(0); s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; return s;
        }
        private static object Call(GameSession s, string method, params object[] args) => typeof(GameSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Time(GameSession s, long at)
        { s.Clock.Restore(new GameDate((int)(at / 1440)), (int)(at % 1440)); s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; }
        private static void At(GameSession s, string id)
        { s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(id)); typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(id).NodeId); }
        private static string Bill(GameSession s, bool rent, long expected = 10000, long extra = 12000)
        {
            string id = "test-bill:" + rent, property = rent ? s.MainShop.Place.Id : s.Home.Id;
            Call(s, "ChargeRecordedBill", rent ? "rent-shop" : "utilities", property, rent ? s.MainShop.Lease.SignedDay : 0,
                id, Money.FromCents(expected), rent ? "房租" : "水电", "实际账单", new LedgerAttribution(rent ? LedgerBusinessKind.Operating : LedgerBusinessKind.Personal,
                    rent ? property : null, property, sourceEventId: id));
            if (extra > 0) s.Ledger.ForceDebit(s.Clock.Today, rent ? "房租" : "水电", Money.FromCents(extra), "同账单额外实付",
                new LedgerAttribution(rent ? LedgerBusinessKind.Operating : LedgerBusinessKind.Personal, rent ? property : null, property, sourceEventId: id + ":extra:1"));
            return id;
        }
        [Test] public void RetiredEventsAreAbsentAndLaterAcceptanceNumbersNeverShift()
        {
            using var s = Make();
            foreach (string id in new[] { "money_lost_wallet_own", "money_parttime_offer", "money_utilities_hike" })
            { Assert.IsNull(EventCatalog.Get(id)); s.RaiseEvent(id); Assert.IsNull(s.PendingEvent); }
            foreach (int n in new[] { 102, 104, 108, 110 }) Assert.IsNull(GameSession.DebugNumberedEvent(n));
            Assert.AreEqual("money_insurance_agent", GameSession.DebugNumberedEvent(106).Id);
            Assert.AreEqual("money_charity_box", GameSession.DebugNumberedEvent(111).Id);
            Assert.AreEqual("life_festival_lantern", GameSession.DebugNumberedEvent(124).Id);
            Assert.AreEqual(120, EventCatalog.All.Count);
        }
        [TestCase(false)] [TestCase(true)]
        public void RefundNeedsActualExtraPaymentAndReturnsOnlyThatAmountOnce(bool rent)
        {
            using var s = Make(); s.RaiseEvent(rent ? "money_overpaid_rent" : "money_utility_refund"); Assert.IsNull(s.PendingEvent);
            string id = Bill(s, rent); var cash = s.Ledger.Balance; Assert.AreEqual(12000, s.EventBillRefundDue(id));
            s.RaiseEvent(rent ? "money_overpaid_rent" : "money_utility_refund"); Assert.NotNull(s.PendingEvent); Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(cash + Money.FromYuan(120), s.Ledger.Balance); Assert.IsFalse(s.SettleEventBill(id, false));
            using var loaded = Reload(s); Assert.IsFalse(loaded.SettleEventBill(id, true)); Assert.AreEqual(0, loaded.EventBillRefundDue(id));
        }
        [Test] public void CorrectlyChargedInvoicesNeverManufactureRefunds()
        {
            using var s = Make(); string id = Bill(s, false, extra: 0); Assert.AreEqual(0, s.EventBillRefundDue(id));
            s.RaiseEvent("money_utility_refund"); Assert.IsNull(s.PendingEvent); Assert.IsFalse(s.SettleEventBill(id, false));
        }
        [Test] public void UtilityCreditActuallyReducesNextBillAndKeepsRemainingBalanceAcrossSave()
        {
            using var s = Make(); string id = Bill(s, false); Assert.IsTrue(s.SettleEventBill(id, true));
            using var loaded = Reload(s); var cash = loaded.Ledger.Balance;
            Call(loaded, "ChargeRecordedBill", "utilities", loaded.Home.Id, 0, "next-utility", Money.FromYuan(50), "水电", "下期水电", new LedgerAttribution(LedgerBusinessKind.Personal));
            Assert.AreEqual(cash, loaded.Ledger.Balance); Assert.AreEqual(7000, loaded.EventBills.Single(b => b.Id == id).CreditCents);
            Call(loaded, "ChargeRecordedBill", "utilities", loaded.Home.Id, 0, "next-utility", Money.FromYuan(50), "水电", "重放", new LedgerAttribution(LedgerBusinessKind.Personal));
            Assert.AreEqual(7000, loaded.EventBills.Single(b => b.Id == id).CreditCents);
            Call(loaded, "ChargeRecordedBill", "utilities", loaded.Home.Id, 0, "third-utility", Money.FromYuan(100), "水电", "再下期水电", new LedgerAttribution(LedgerBusinessKind.Personal));
            Assert.AreEqual(cash - Money.FromYuan(30), loaded.Ledger.Balance); Assert.AreEqual(0, loaded.EventBills.Single(b => b.Id == id).CreditCents);
        }
        [Test] public void RentCreditAffectsActualWeeklyDebitAndCashReserveForecast()
        {
            using var s = Make(); string id = Bill(s, true, extra: 5000); var before = s.GetCityCashReserve(7).KnownMandatory;
            Assert.IsTrue(s.SettleEventBill(id, true)); Assert.AreEqual(before - Money.FromYuan(50), s.GetCityCashReserve(7).KnownMandatory);
            int day = s.MainShop.Lease.SignedDay + 7; while (day <= s.Clock.Today.TotalDays) day += 7;
            Time(s, day * 1440L); var cash = s.Ledger.Balance; Call(s, "CollectWeeklyShopRent", s.Clock.Today);
            var debit = s.Ledger.Entries.Single(e => e.SourceEventId == "rent:" + s.MainShop.Place.Id + ":" + day);
            Assert.AreEqual(-(s.MainShop.Lease.Rent - Money.FromYuan(50)), debit.Amount);
            Assert.AreEqual(cash + debit.Amount, s.Ledger.Balance); Assert.AreEqual(0, s.EventBills.Single(b => b.Id == id).CreditCents);
        }
        [Test] public void OldLeaseCreditCannotBeUsedByANewLeaseAndIsReturnedOnce()
        {
            using var s = Make(); string id = Bill(s, true); Assert.IsTrue(s.SettleEventBill(id, true)); s.MainShop.Lease.SignedDay++;
            var cash = s.Ledger.Balance; Call(s, "TickMoneyBatch", s.Clock.Now.TotalMinutes); Assert.AreEqual(cash + Money.FromYuan(120), s.Ledger.Balance);
            Call(s, "TickMoneyBatch", s.Clock.Now.TotalMinutes); Assert.AreEqual(cash + Money.FromYuan(120), s.Ledger.Balance);
        }
        [Test] public void ScratchRequiresManualClearingAndCannotRerollOrPayTwiceAfterSave()
        {
            using var s = Make(); var cash = s.Ledger.Balance; Assert.IsTrue(s.BuyEventScratchTicket()); var ticket = s.ScratchTicket;
            Assert.AreEqual(cash - Money.FromYuan(200), s.Ledger.Balance); Assert.IsFalse(s.ClaimEventScratch()); Assert.IsFalse(s.BuyEventScratchTicket());
            for (int n = 0; n < 50; n++) Assert.IsTrue(s.ScratchEventCell(n));
            using var loaded = Reload(s); Assert.AreEqual(ticket.PrizeYuan, loaded.ScratchTicket.PrizeYuan); Assert.IsFalse(loaded.ScratchEventCell(0));
            Assert.AreEqual(50, loaded.ScratchTicket.Cleared.Count(x => x)); Assert.IsFalse(loaded.ClaimEventScratch());
            for (int n = 50; n < 87; n++) loaded.ScratchEventCell(n);
            Assert.IsTrue(loaded.ClaimEventScratch()); Assert.AreEqual(cash - Money.FromYuan(200) + Money.FromYuan(ticket.PrizeYuan), loaded.Ledger.Balance);
            Assert.IsFalse(loaded.ClaimEventScratch());
        }
        [Test] public void AnnualPolicyRenewsFromExpiryAndNoLongerCoversAfterExpiry()
        {
            using var s = Make(); var cash = s.Ledger.Balance; long now = s.Clock.Now.TotalMinutes;
            Assert.IsTrue(s.BuyHospitalPolicy()); Assert.AreEqual(now + 360 * 1440L, s.HospitalPolicyUntil); Assert.AreEqual(cash - Money.FromYuan(3000), s.Ledger.Balance);
            Assert.IsTrue(s.BuyHospitalPolicy()); Assert.AreEqual(now + 720 * 1440L, s.HospitalPolicyUntil);
            using var loaded = Reload(s); Time(loaded, loaded.HospitalPolicyUntil); Assert.IsFalse(loaded.HasHospitalCoverage);
            var c = loaded.Ledger.ForceDebit(loaded.Clock.Today, "医疗", Money.FromYuan(200), "实际住院");
            Assert.AreEqual(Money.Zero, Call(loaded, "ReimburseEventHospital", c, loaded.HasHospitalCoverage));
            long restart = loaded.Clock.Now.TotalMinutes; loaded.BuyHospitalPolicy(); Assert.AreEqual(restart + 360 * 1440L, loaded.HospitalPolicyUntil);
        }
        [Test] public void HospitalCoverageHasNoSingleOrCumulativeCapAndDoesNotDuplicateClaims()
        {
            using var s = Make(); Assert.IsTrue(s.BuyHospitalPolicy());
            foreach (long cost in new[] { 100000L, 200000L, 300000L })
            {
                var actual = s.Ledger.ForceDebit(s.Clock.Today, "医疗", Money.FromYuan(cost), "实际住院费"); var cash = s.Ledger.Balance;
                Assert.AreEqual(Money.FromYuan(cost).Scale(.8), Call(s, "ReimburseEventHospital", actual, true));
                Assert.AreEqual(cash + Money.FromYuan(cost).Scale(.8), s.Ledger.Balance); Assert.AreEqual(Money.Zero, Call(s, "ReimburseEventHospital", actual, true));
            }
            var outpatient = s.Ledger.ForceDebit(s.Clock.Today, "医疗", Money.FromYuan(150), "医院门诊");
            Assert.AreEqual(Money.Zero, Call(s, "ReimburseEventHospital", outpatient, true));
        }
        [Test] public void ActualHospitalizationHonorsAdmissionCoverageWhenPolicyExpiresDuringStay()
        {
            using var s = Make(); s.BuyHospitalPolicy(); Time(s, s.HospitalPolicyUntil - 10);
            Call(s, "Hospitalize"); Assert.IsFalse(s.HasHospitalCoverage);
            var bill = s.Ledger.Entries.Last(e => e.Category == "医疗"); var payout = s.Ledger.Entries.Single(e => e.Category == "住院保险理赔");
            Assert.AreEqual(bill.Amount.Abs().Scale(.8), payout.Amount);
        }
        [TestCase(49, 0)] [TestCase(50, .3)] [TestCase(70, .5)] [TestCase(100, .8)] [TestCase(150, .8)]
        public void InvestmentRiskFollowsIntelligence(int iq, double expected) => Assert.AreEqual(expected, GameSession.PrivateInvestmentChance(iq), 1e-9);
        [Test] public void LowIntelligenceInvestmentIsSelectableAndLosesAtTheActualDeadline()
        {
            using var s = Make(); s.Player.Intelligence = 20; var cash = s.Ledger.Balance; var worth = s.NetWorth();
            s.RaiseEvent("money_invest_tip"); Assert.NotNull(s.PendingEvent); Assert.IsTrue(s.IsChoiceEnabled(s.PendingEvent.Choices[1])); Assert.IsTrue(s.ResolveEvent(1));
            Assert.AreEqual(cash - Money.FromYuan(10000), s.Ledger.Balance); Assert.AreEqual(worth, s.NetWorth()); var i = s.PrivateInvestments.Single();
            Assert.IsFalse(i.Success); Assert.AreEqual(0, i.Chance); s.Player.Intelligence = 100;
            using var loaded = Reload(s); Assert.IsFalse(loaded.PrivateInvestments.Single().Success); Time(loaded, i.At - 1); loaded.AdvanceClock(1);
            Assert.IsTrue(loaded.PrivateInvestments.Single().Settled); Assert.IsFalse(loaded.Ledger.Entries.Any(e => e.Category == "私人投资回款"));
        }
        [TestCase(false)] [TestCase(true)]
        public void SavedInvestmentSettlesExactlyOnceAndPaysTotalTwentyThousandOnlyOnSuccess(bool success)
        {
            using var s = Make(); s.Player.Intelligence = 70; Assert.IsTrue(s.StartPrivateInvestment()); Assert.IsFalse(s.StartPrivateInvestment());
            var data = s.CreateSave(); data.moneyEventInvestments[0].Success = success; using var loaded = GameSession.Load(s.City, data); var i = loaded.PrivateInvestments.Single();
            Time(loaded, i.At - 2); loaded.AdvanceClock(1); Assert.IsFalse(loaded.Ledger.Entries.Any(e => e.Category == "私人投资回款")); loaded.AdvanceClock(1); loaded.AdvanceClock(1);
            Assert.AreEqual(success ? 1 : 0, loaded.Ledger.Entries.Count(e => e.Category == "私人投资回款"));
            if (success) Assert.AreEqual(2000000, loaded.Ledger.Entries.Single(e => e.Category == "私人投资回款").Amount.Cents);
        }
        [Test] public void IdleSaleRequiresRealItemsAndMarketAttendanceAndDoesNotTouchPlayerShop()
        {
            using var s = Make(); s.Backpack.AddFurniture("fan_new", 2); s.Flags.Add("event-idle-sale"); var cash = s.Ledger.Balance; var layout = s.MainShop.Layout.Count;
            Assert.IsFalse(s.SellEventIdle("fan_new", true, 1)); At(s, GameSession.OldMarketVenueId); var price = s.EventIdleItems.Single(i => i.Id == "fan_new").Price;
            Assert.IsFalse(s.SellEventIdle("fan_new", true, 3)); Assert.IsTrue(s.SellEventIdle("fan_new", true, 2));
            Assert.AreEqual(cash + price * 2, s.Ledger.Balance); Assert.AreEqual(0, s.Backpack.FurnitureCount("fan_new")); Assert.IsFalse(s.SellEventIdle("fan_new", true, 1));
            Assert.AreEqual(layout, s.MainShop.Layout.Count);
            Assert.IsFalse(s.Flags.Contains("event-idle-sale"));
        }
    }
}
