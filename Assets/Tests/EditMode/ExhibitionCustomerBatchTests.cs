using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class ExhibitionCustomerBatchTests
    {
        private static GameSession Make(bool computer = true)
        {
            using var f = DistrictProgressionTests.Make();
            using var first = new GameSession(f.City, 731, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 600), starterStock: false, smartphone: false, startWithShop: computer);
            first.Tick(0); var d = first.CreateSave(); d.districtProgress = new DistrictProgress { UnlockedTier = 3 };
            var s = GameSession.Load(first.City, d); s.EventsEnabled = false; s.Player.Health = 100; s.Player.Energy = 100; s.Player.Hunger = 100;
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; return s;
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void At(GameSession s, string id)
        { s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(id)); typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(id).NodeId); }
        private static CityActivity Accept(GameSession s, int n, int choice = 0)
        {
            string id = n switch { 73 => "act_expo_curator", 74 => "act_expo_hanzheng", 75 => "act_comic_creator", _ => "act_comic_shenxiao" };
            s.RaiseEvent(id); Assert.NotNull(s.PendingEvent); Assert.IsTrue(s.PendingEvent.Choices[choice].Enabled(s), s.PendingEvent.Body);
            Assert.IsTrue(s.ResolveEvent(choice)); return s.CityActivities.Last(a => a.Active);
        }
        private static void Attend(GameSession s, CityActivity a)
        {
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); At(s, a.VenueId);
            Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace)); s.AdvanceClock(3); Assert.AreEqual(CityActivityPhase.Running, a.State, a.EndReason);
        }
        private static void WalkAll(GameSession s, CityActivity a)
        { for (int i = 0; i < 3; i++) { s.AdvanceClock(4); if (i < 2) Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); } }
        [TestCase(73)] [TestCase(74)] [TestCase(75)] [TestCase(77)]
        public void ExhibitionsUseTheActualCalendarAndAttendanceWithoutUpfrontRewards(int n)
        {
            using var s = Make(); var cash = s.Ledger.Balance; int iq = s.Player.Intelligence; var a = Accept(s, n);
            Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(iq, s.Player.Intelligence);
            int date = (int)(a.ScheduledStart / 1440) % 30 + 1;
            Assert.That(date, n == 74 ? Is.EqualTo(9) : n == 73 ? Is.InRange(10, 12) : Is.InRange(22, 24));
            if (n == 77) { Assert.IsTrue(a.Participants.Any(p => p.PersonId == "life_enterprise")); Assert.AreEqual(2, a.Participants.Length); }
            Attend(s, a); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [Test]
        public void ExhibitionOrdinaryVisitDoesNotRequireComputerShopButBusinessDoes()
        {
            using var s = Make(false); var a = Accept(s, 73); Attend(s, a); s.AdvanceClock(4);
            Assert.IsFalse(s.ChooseCityActivity(a.Id, 4)); Assert.IsTrue(s.ChooseCityActivity(a.Id, 2));
        }
        [Test]
        public void ExhibitionWorkRequiresCorrectOnSiteActionsAndPays1500OnlyAfterCompletion()
        {
            using var s = Make(); var a = Accept(s, 74, 1); Attend(s, a); var cash = s.Ledger.Balance;
            for (int sample = 1; sample <= 3; sample++)
            {
                s.AdvanceClock(4); Assert.IsTrue(s.ChooseCityActivity(a.Id, sample + 1));
                Assert.AreEqual(sample - 1, s.OutdoorActivityState(a.Id).Correct); s.AdvanceClock(5);
                Assert.AreEqual(sample, s.OutdoorActivityState(a.Id).Correct); Assert.AreEqual(cash, s.Ledger.Balance);
                if (sample < 3) Assert.IsTrue(s.ChooseCityActivity(a.Id, 1));
            }
            using var loaded = Reload(s); loaded.AdvanceClock(100);
            Assert.AreEqual(cash + Money.FromYuan(1500), loaded.Ledger.Balance);
            loaded.AdvanceClock(10); Assert.AreEqual(cash + Money.FromYuan(1500), loaded.Ledger.Balance);
        }
        [Test]
        public void IncorrectExhibitionWorkCanBeRetriedButIncompleteWorkDoesNotPay()
        {
            using var s = Make(); var a = Accept(s, 74, 1); Attend(s, a); s.AdvanceClock(4);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 4)); s.AdvanceClock(5); Assert.AreEqual(0, s.OutdoorActivityState(a.Id).Correct);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); s.AdvanceClock(5); Assert.AreEqual(1, s.OutdoorActivityState(a.Id).Correct);
            var cash = s.Ledger.Balance; s.AdvanceClock(100); Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(CityActivityPhase.Interrupted, a.State);
        }
        [Test]
        public void ModelPurchaseAndChildGiftUseRealItemsAndRequireTheParentAtTheVenue()
        {
            using var s = Make(); var a = Accept(s, 77); Attend(s, a); s.AdvanceClock(4);
            const string model = "model:harbor:skiff"; var cash = s.Ledger.Balance;
            Assert.IsTrue(s.BuyExhibitionModel(a.Id, model)); Assert.AreEqual(cash - Gifts.Get(model).Price, s.Ledger.Balance);
            Assert.AreEqual(1, s.Backpack.GiftCount(model)); Assert.IsTrue(s.GiftExhibitionModel(a.Id, model));
            Assert.AreEqual(0, s.Backpack.GiftCount(model)); Assert.IsFalse(s.GiftExhibitionModel(a.Id, model));
            Assert.IsFalse(s.Flags.Contains("exhibit:shenxiao-details"));
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); s.AdvanceClock(5); s.ChooseCityActivity(a.Id, 1); s.AdvanceClock(4); s.ChooseCityActivity(a.Id, 1); s.AdvanceClock(4); s.AdvanceClock(130);
            Assert.IsTrue(s.Flags.Contains("exhibit:shenxiao-details"));
        }
        private static void PaidSale(GameSession s, bool cash = true)
        {
            var shop = s.MainShop; At(s, shop.Place.Id); shop.Sim.CheckoutClock = () => double.NaN;
            shop.Sim.Accepted = cash ? PaymentMethods.Cash : PaymentMethods.QrCode;
            shop.Sim.RestoreCityCustomers(new CitySavedShopVisits { shopId = shop.Place.Id, nextCustomerId = 8, queue = new[] { 7 }, customers = new[] {
                new CitySavedCustomer { id = 7, intent = (int)CustomerIntent.Goods, category = 0, tier = 0, state = (int)CustomerState.WaitingCheckout,
                    goodsId = "model:harbor:skiff", cash = cash, arrived = s.Clock.Now.TotalMinutes - 5, ready = s.Clock.Now.TotalMinutes - 2,
                    waiting = s.Clock.Now.TotalMinutes - 1, spendCents = 50000, goodsExpiry = -1 } } });
            Assert.GreaterOrEqual(shop.Sim.Checkout(7), 0);
        }
        [Test]
        public void ReviewsNeedARealSaleAndTransferOnlySchedulesPotentialVisits()
        {
            using var s = Make(); s.RaiseEvent("shop_regular_review"); Assert.IsNull(s.PendingEvent); PaidSale(s);
            s.RaiseEvent("shop_regular_review"); Assert.NotNull(s.PendingEvent); var cash = s.Ledger.Balance;
            s.ResolveEvent(2); Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(1, s.CreateSave().flyerCampaigns.Length);
            s.RaiseEvent("shop_regular_review"); Assert.IsNull(s.PendingEvent);
        }
        [Test]
        public void ChangeNeedsCashAndRefundBalancesTheRecordedLiabilityExactlyOnceAcrossSave()
        {
            using var s = Make(); PaidSale(s, false); s.RaiseEvent("shop_wrong_change"); Assert.IsNull(s.PendingEvent);
            using var cashSession = Make(); PaidSale(cashSession); var cash = cashSession.Ledger.Balance; var worth = cashSession.NetWorth();
            cashSession.RaiseEvent("shop_wrong_change"); Assert.NotNull(cashSession.PendingEvent);
            Assert.AreEqual(Money.FromYuan(50), cashSession.ChangeRefundDebt); Assert.AreEqual(cash + Money.FromYuan(50), cashSession.Ledger.Balance);
            Assert.AreEqual(worth, cashSession.NetWorth()); cashSession.ResolveEvent(2);
            using var loaded = Reload(cashSession); loaded.RaiseEvent("shop_wrong_change");
            Assert.AreEqual(cash + Money.FromYuan(50), loaded.Ledger.Balance); loaded.ResolveEvent(0);
            Assert.AreEqual(cash, loaded.Ledger.Balance); Assert.AreEqual(Money.Zero, loaded.ChangeRefundDebt);
            Assert.IsFalse(loaded.RefundEventChange(cashSession.PendingChangeRefunds.Single().Key));
        }
        [TestCase(0, 9500)] [TestCase(1, 9000)]
        public void TenItemCommunityOrdersUseRealPriceStockAndDeliveryWithoutAnInstantReward(int choice, int bps)
        {
            using var s = Make(); var shop = s.MainShop; At(s, shop.Place.Id); shop.RetailKind = "models"; shop.Sim.RetailKind = "models";
            const string product = "model:harbor:skiff"; shop.Sim.Stock.ForceAddGoods(product, 10);
            var cash = s.Ledger.Balance; s.RaiseEvent("shop_bulk_order"); Assert.NotNull(s.PendingEvent); s.ResolveEvent(choice);
            var order = s.CityReferrals.Single(o => o.CommunityOrder); Assert.AreEqual(10, order.Count);
            Assert.AreEqual((shop.Sim.GoodsPriceOf(product) * 10).Scale(bps / 10000.0), order.Total);
            Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(10, shop.Sim.Stock.GoodsCount(product));
            Assert.IsTrue(s.AcceptCityReferral(order.Id)); Assert.AreEqual(cash, s.Ledger.Balance);
            Assert.IsTrue(s.DeliverIndustryReferral(order.Id)); Assert.AreEqual(0, shop.Sim.Stock.GoodsCount(product)); Assert.AreEqual(cash + order.Total, s.Ledger.Balance);
            Assert.IsFalse(s.DeliverIndustryReferral(order.Id));
        }
        [Test]
        public void LowStatsCanInsistOnFullBulkPriceButFailWithoutCreatingAnOrder()
        {
            using var s = Make(); s.Player.Charm = 1; s.Player.Intelligence = 1;
            var cash = s.Ledger.Balance; s.RaiseEvent("shop_bulk_order"); Assert.NotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(2)); Assert.IsFalse(s.CityReferrals.Any(o => o.CommunityOrder)); Assert.AreEqual(cash, s.Ledger.Balance);
        }
        [Test]
        public void ComputerCommunityOrderCreatesTenRealBuildOrdersAndPersistsTheDiscount()
        {
            using var s = Make(); var cash = s.Ledger.Balance; s.RaiseEvent("shop_bulk_order"); s.ResolveEvent(0);
            var offer = s.CityReferrals.Single(o => o.CommunityOrder); Assert.IsTrue(s.AcceptCityReferral(offer.Id));
            var orders = s.MainShop.Sim.Orders.Where(o => o.ContractId == offer.ContractId).ToArray();
            Assert.AreEqual(10, orders.Length); Assert.AreEqual(offer.Total.Cents, orders.Sum(o => o.Price.Cents)); Assert.AreEqual(cash, s.Ledger.Balance);
            using var loaded = Reload(s); var saved = loaded.CityReferrals.Single(o => o.Id == offer.Id);
            Assert.IsTrue(saved.CommunityOrder); Assert.AreEqual(9500, saved.PriceBasisPoints); Assert.AreEqual(offer.Total, saved.Total);
        }
        [Test]
        public void ScheduledChangeRefundWaitsForTheRealHourAndOnlyPaysOnce()
        {
            using var s = Make(); PaidSale(s); var cash = s.Ledger.Balance; s.RaiseEvent("shop_wrong_change"); s.ResolveEvent(1);
            s.MainShop.Sim.PlayerPresent = true; s.AdvanceClock(59); Assert.IsTrue(s.ChangeRefundDebt.IsPositive);
            s.AdvanceClock(1); Assert.AreEqual(Money.Zero, s.ChangeRefundDebt); Assert.AreEqual(cash, s.Ledger.Balance);
            s.AdvanceClock(5); Assert.AreEqual(cash, s.Ledger.Balance);
        }
    }
}
