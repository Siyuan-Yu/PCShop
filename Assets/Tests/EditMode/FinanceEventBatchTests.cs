using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;

namespace PCShopSell.Tests
{
    public sealed class FinanceEventBatchTests
    {
        private static GameSession Make()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 921, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 660), false, true, true);
            s.EventsEnabled = false; s.Tick(0); At(s, s.MainShop.Place.Id);
            s.MainShop.Sim.NotFitted = false; s.MainShop.Sim.ForcedClosed = false; s.MainShop.Sim.PlayerPresent = true; s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; s.MainShop.Sim.SetHours(0, 1440);
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; return s;
        }
        private static void At(GameSession s, string id)
        { s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, id == null ? null : s.City.GetPlace(id)); if (id != null) typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(id).NodeId); }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Resolve(GameSession s, string id, int index)
        { s.RaiseEvent(id); Assert.NotNull(s.PendingEvent, id); Assert.IsTrue(s.ResolveEvent(index)); }
        private static void Time(GameSession s, long at)
        { s.Clock.Restore(new GameDate((int)(at / 1440)), (int)(at % 1440)); s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; }
        private static void Stock(GameSession s)
        {
            s.MainShop.Sim.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 20);
            s.MainShop.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double> { { "memory", 100 } });
        }
        private static void Sale(GameSession s, long cents, string person = "zhou")
        {
            var sim = s.MainShop.Sim; sim.CheckoutClock = () => double.NaN; sim.Accepted = PaymentMethods.Cash;
            int id = 100 + sim.TodayCustomers + (int)sim.TodaySales.Yuan;
            sim.RestoreCityCustomers(new CitySavedShopVisits { shopId = s.MainShop.Place.Id, nextCustomerId = id + 1, queue = new[] { id }, customers = new[] {
                new CitySavedCustomer { id = id, personId = person, intent = (int)CustomerIntent.Retail, state = (int)CustomerState.WaitingCheckout, itemId = "ram0", boxed = true, cash = true,
                    arrived = s.Clock.Now.TotalMinutes - 5, ready = s.Clock.Now.TotalMinutes - 2, waiting = s.Clock.Now.TotalMinutes - 1, spendCents = cents, goodsExpiry = -1 } } });
            Assert.GreaterOrEqual(sim.Checkout(id), 0);
        }
        [Test] public void MembershipChargesOnceAndLaterPurchaseRedeemsUsingActualNetPaid()
        {
            using var s = Make(); var start = s.Ledger.Balance; Resolve(s, "shop_loyalty_card", 0);
            Assert.AreEqual(start - Money.FromYuan(600), s.Ledger.Balance); s.RaiseEvent("shop_loyalty_card"); Assert.IsNull(s.PendingEvent);
            Sale(s, 500000); Assert.AreEqual(50, s.EventMembers.Single().Points);
            using var loaded = Reload(s); var cash = loaded.Ledger.Balance; Sale(loaded, 30000);
            Assert.AreEqual(cash + Money.FromYuan(200), loaded.Ledger.Balance); Assert.AreEqual(2, loaded.EventMembers.Single().Points);
            Assert.AreEqual(520000, loaded.EventMembers.Single().NetPaid);
        }
        [Test] public void AnonymousAndAnotherPersonDoNotSpendSomeoneElsesPoints()
        {
            using var s = Make(); Resolve(s, "shop_loyalty_card", 0); Sale(s, 500000); Sale(s, 30000, null); Sale(s, 30000, "jie");
            Assert.AreEqual(50, s.EventMembers.Single(m => m.PersonId == "zhou").Points); Assert.AreEqual(3, s.EventMembers.Single(m => m.PersonId == "jie").Points);
            Assert.AreEqual(2, s.EventMembers.Length);
        }
        [Test] public void UnsupportedSpecialBusinessCannotPayForAnInoperativeMembership()
        {
            using var s = Make(); s.MainShop.RetailKind = "restaurant"; s.PlayerShop.RetailKind = "restaurant"; var cash = s.Ledger.Balance;
            s.RaiseEvent("shop_loyalty_card"); Assert.IsNull(s.PendingEvent); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [Test] public void CompensationReversesPointsAndMemberPaymentIsNotASecondReward()
        {
            using var s = Make(); Resolve(s, "shop_loyalty_card", 0); Sale(s, 500000); var cash = s.Ledger.Balance;
            Resolve(s, "shop_complaint_online", 1); Assert.AreEqual(cash - Money.FromYuan(500), s.Ledger.Balance); Assert.AreEqual(45, s.EventMembers.Single().Points);
        }
        [Test] public void CouponAppliesOnlyToActualStoreProcurementAndCancellationRefundsNet()
        {
            using var s = Make(); Resolve(s, "money_found_coupon", 0); var cash = s.Ledger.Balance;
            var part = PartCatalog.Get("gpu1"); int count = (int)Math.Ceiling(5000.0 / s.OnlinePrice(Online1788.Shopping, part).Yuan);
            var gross = s.OnlinePrice(Online1788.Shopping, part) * count + Online1788.ShippingFee;
            Assert.IsTrue(s.PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (part, count) }, false));
            Assert.AreEqual(gross - Money.FromYuan(500), Money.FromCents(s.Online.Pending.Single().TotalCents)); Assert.IsTrue(s.EventCoupon.Used);
            using var loaded = Reload(s); Assert.IsTrue(loaded.CancelOnlineProcurementOrder(loaded.Online.Pending.Single().Id));
            Assert.AreEqual(cash, loaded.Ledger.Balance); Assert.IsTrue(loaded.EventCoupon.Used); Assert.IsFalse(loaded.CancelOnlineProcurementOrder(1));
        }
        [TestCase(true)] [TestCase(false)]
        public void CouponDoesNotDiscountPersonalOrBelowThresholdPurchases(bool home)
        {
            using var s = Make(); Resolve(s, "money_found_coupon", 0);
            var part = PartCatalog.Get(home ? "gpu1" : "ram0"); int count = home ? 10 : 1;
            var gross = s.OnlinePrice(Online1788.Shopping, part) * count + Online1788.ShippingFee;
            Assert.IsTrue(s.PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (part, count) }, home));
            Assert.AreEqual(gross.Cents, s.Online.Pending.Single().TotalCents); Assert.IsFalse(s.EventCoupon.Used);
        }
        [Test] public void CouponExpiresAtFourteenDaysWithoutCreatingCash()
        {
            using var s = Make(); var cash = s.Ledger.Balance; Resolve(s, "money_found_coupon", 0); Assert.AreEqual(cash, s.Ledger.Balance);
            Time(s, s.EventCoupon.Until); Assert.AreEqual(Money.Zero, s.EventProcurementCouponQuote(Money.FromYuan(8000), false));
        }
        [Test] public void UnaffordableOrderDoesNotConsumeCouponOrCreateDelivery()
        {
            using var s = Make(); Resolve(s, "money_found_coupon", 0); s.Ledger.ForceDebit(s.Clock.Today, "test", s.Ledger.Balance - Money.FromYuan(1), "fixture"); var cash = s.Ledger.Balance;
            Assert.IsFalse(s.PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (PartCatalog.Get("gpu1"), 10) }, false));
            Assert.IsFalse(s.EventCoupon.Used); Assert.IsEmpty(s.Online.Pending); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [Test] public void RumorMakesAnActualPaidOrderWithoutAnyGuaranteedProfit()
        {
            using var s = Make(); var cash = s.Ledger.Balance; var count = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("gpu1"));
            Resolve(s, "shop_hot_item_rumor", 0); Assert.AreEqual(1, s.Online.Pending.Count); Assert.Less(s.Ledger.Balance.Cents, cash.Cents);
            Assert.AreEqual(count, s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("gpu1")));
        }
        [TestCase(0)] [TestCase(1)]
        public void FlashSaleIsActualProductPricingWithNoInstantIncomeAndExpires(int choice)
        {
            using var s = Make(); Stock(s); var normal = s.PlayerShop.PriceOf(PartCatalog.Get("ram0"), true); var cash = s.Ledger.Balance;
            Resolve(s, "shop_flash_sale", choice); Assert.AreEqual(normal.Scale(.9), s.PlayerShop.PriceOf(PartCatalog.Get("ram0"), true));
            Assert.AreEqual(cash - Money.FromYuan(choice == 1 ? 1000 : 0), s.Ledger.Balance);
            using var loaded = Reload(s); Time(loaded, loaded.EventFlashSales.Single().Until); Assert.AreEqual(normal, loaded.PlayerShop.PriceOf(PartCatalog.Get("ram0"), true));
        }
        [Test] public void FollowupRequiresRealPreviousInspectionAndDoesNotSettleTwice()
        {
            using var s = Make(); s.RaiseEvent("shop_inspection_followup"); Assert.IsNull(s.PendingEvent);
            var save = s.CreateSave(); save.inspectionShops = new[] { s.MainShop.Place.Id }; save.inspectionDays = new[] { 45 };
            using var loaded = GameSession.Load(s.City, save); var cash = loaded.Ledger.Balance; var rep = loaded.PlayerShop.ShopReputation;
            Resolve(loaded, "shop_inspection_followup", 0); Assert.AreEqual(cash, loaded.Ledger.Balance); Assert.AreEqual(rep, loaded.PlayerShop.ShopReputation);
            Assert.AreEqual(45, loaded.CreateSave().inspectionDays.Single());
        }
        [Test] public void FlashQuotaIncludesWaitingCustomersAndSurvivesSaveWithoutADoubleDiscount()
        {
            using var s = Make(); Stock(s); s.PlayerShop.Markup = .5; s.PlayerShop.Accepted = PaymentMethods.Cash;
            var normal = s.PlayerShop.PriceOf(PartCatalog.Get("ram0"), true);
            Resolve(s, "shop_flash_sale", 0);
            var arrive = typeof(ShopSimulation).GetMethod("ArriveReferral", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 12; i++) arrive.Invoke(s.PlayerShop, new object[] { PartCatalog.Get("ram0"), null, true, 10000000L, false, null });
            s.AdvanceClock(4);
            Assert.GreaterOrEqual(s.PlayerShop.Queue.Count, 6);
            Assert.AreEqual(5, s.PlayerShop.Queue.Count(c => c.Spend == normal.Scale(.9)));
            Assert.AreEqual(0, s.EventFlashSales.Single().Remaining);
            using var loaded = Reload(s); loaded.PlayerShop.CheckoutClock = () => double.NaN; var cash = loaded.Ledger.Balance;
            var queued = loaded.PlayerShop.Queue.ToArray(); var quoted = queued.Aggregate(Money.Zero, (sum, c) => sum + c.Spend);
            foreach (var c in queued) Assert.GreaterOrEqual(loaded.PlayerShop.Checkout(c.Id), 0);
            Assert.AreEqual(cash + quoted, loaded.Ledger.Balance); Assert.AreEqual(0, loaded.EventFlashSales.Single().Remaining);
        }
        [Test] public void SubsidyRequiresThirtyDaysAndSalesThenPaysOnlyAfterThreeDaysOnce()
        {
            using var s = Make(); s.RaiseEvent("money_tax_refund"); Assert.IsNull(s.PendingEvent); Sale(s, 10000);
            Time(s, (s.MainShop.Lease.SignedDay + 30) * 1440L + 660); Resolve(s, "money_tax_refund", 0);
            var task = s.ShopEventTasks.Single(t => t.Kind == "subsidy"); Assert.IsFalse(s.Ledger.Entries.Any(e => e.Category == "经营补贴"));
            using var loaded = Reload(s); Time(loaded, task.At - 1); loaded.AdvanceClock(1);
            Assert.AreEqual(300000, loaded.Ledger.Entries.Single(e => e.Category == "经营补贴").Amount.Cents); loaded.AdvanceClock(1);
            loaded.RaiseEvent("money_tax_refund"); Assert.IsNull(loaded.PendingEvent);
            var donation = loaded.ShopEventTasks.Single(t => t.Kind == "subsidy-donation"); var cash = loaded.Ledger.Balance;
            Assert.IsTrue(loaded.DonateEventSubsidy(donation.Id)); Assert.AreEqual(cash - Money.FromYuan(1500), loaded.Ledger.Balance); Assert.IsFalse(loaded.DonateEventSubsidy(donation.Id));
        }
        [Test] public void ScamTransferActuallyLosesFiveThousand()
        { using var s = Make(); var cash = s.Ledger.Balance; Resolve(s, "money_phone_scam", 2); Assert.AreEqual(cash - Money.FromYuan(5000), s.Ledger.Balance); }
        [Test] public void ChangedBusinessCannotReceiveTheOldSubsidy()
        {
            using var s = Make(); Sale(s, 10000); Time(s, (s.MainShop.Lease.SignedDay + 30) * 1440L + 660); Resolve(s, "money_tax_refund", 0);
            var task = s.ShopEventTasks.Single(t => t.Kind == "subsidy"); s.MainShop.RetailKind = "clothing"; s.PlayerShop.RetailKind = "clothing";
            Time(s, task.At - 1); s.AdvanceClock(1); Assert.IsFalse(s.Ledger.Entries.Any(e => e.Category == "经营补贴"));
        }
        [Test] public void BankAdviceRequiresOpenOnsiteTimeAndGivesOneIntelligenceOnlyOnce()
        {
            using var s = Make(); At(s, GameSession.BankPlaceId); Assert.IsTrue(s.BankDepositMoney(1000)); int iq = s.Player.Intelligence;
            Resolve(s, "money_bank_manager", 0); var t = s.ShopEventTasks.Single(x => x.Kind == "bank"); At(s, s.MainShop.Place.Id); Assert.IsFalse(s.StartBankEventTask(t.Id));
            At(s, GameSession.BankPlaceId); Assert.IsTrue(s.StartBankEventTask(t.Id)); s.AdvanceClock(10); At(s, null); s.AdvanceClock(5);
            Assert.AreEqual(20, s.ShopEventTasks.Single(x => x.Id == t.Id).Remaining); using var loaded = Reload(s); At(loaded, GameSession.BankPlaceId); loaded.AdvanceClock(20);
            Assert.AreEqual(iq + 1, loaded.Player.Intelligence); Assert.AreEqual(Money.FromYuan(1000), loaded.BankDeposit); Assert.IsFalse(loaded.StartBankEventTask(t.Id));
        }
        [Test] public void RiderRequiresKnownPersonAndSchedulesTwentyMinuteRealVisit()
        {
            using var s = Make(); s.RaiseEvent("shop_delivery_boy"); Assert.IsNull(s.PendingEvent); s.Meet("res_rider", 30); int relation = s.Relation("res_rider");
            Resolve(s, "shop_delivery_boy", 1); var a = s.CityActivities.Single(x => x.Active && x.DefinitionId == "social_93");
            Assert.AreEqual(relation, s.Relation("res_rider")); Assert.AreEqual(s.MainShop.Place.Id, a.VenueId);
            Time(s, a.ScheduledStart); At(s, a.VenueId); s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3); s.AdvanceClock(21);
            Assert.AreEqual(CityActivityPhase.Completed, a.State, a.EndReason); Assert.GreaterOrEqual(s.Relation("res_rider"), relation + 4);
        }
        [Test] public void ReturnCustomerRequiresARealIdentifiedSaleAndDoesNotPayAtInvitation()
        {
            using var s = Make(); Stock(s); s.RaiseEvent("shop_old_customer_return"); Assert.IsNull(s.PendingEvent); Sale(s, 40000); var cash = s.Ledger.Balance;
            var person = s.CityPersonState("zhou"); person.Route = null; person.ActivityId = null; person.VisitId = null; person.Task = CityPersonTask.Leisure;
            Resolve(s, "shop_old_customer_return", 0); Assert.AreEqual(cash, s.Ledger.Balance); var t = s.ShopEventTasks.Single(x => x.Kind == "return-customer");
            using var loaded = Reload(s); Assert.AreEqual(t.Id, loaded.CityPersonState("zhou").VisitId);
            Time(loaded, t.At - 1); loaded.AdvanceClock(1); Assert.IsTrue(loaded.MainShop.Sim.ActiveCustomers.Any(c => c.PersonId == "zhou"));
        }
    }
}
