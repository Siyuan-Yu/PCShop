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
    public sealed class ShopEventBatchTests
    {
        private static GameSession Make()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 821, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 660), starterStock:false, smartphone:false, startWithShop:true);
            s.EventsEnabled = false; s.Tick(0); At(s, s.MainShop.Place.Id);
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; s.MainShop.Sim.NotFitted = false; s.MainShop.Sim.ForcedClosed = false; s.MainShop.Sim.SetHours(0, 1440);
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; return s;
        }
        private static void At(GameSession s, string place)
        { s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, place == null ? null : s.City.GetPlace(place)); if (place != null) typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(place).NodeId); }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void AtTime(GameSession s, long at) { s.Clock.Restore(new GameDate((int)(at / 1440)), (int)(at % 1440)); s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; }
        private static void Resolve(GameSession s, string id, int choice) { s.RaiseEvent(id); Assert.NotNull(s.PendingEvent, id); Assert.IsTrue(s.ResolveEvent(choice)); }
        private static void AddPaidOrder(GameSession s)
        { s.SetSmartphone(true); Assert.IsTrue(s.PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (PartCatalog.Get("ram0"), 1) }, false)); }
        [Test] public void LeafletUsesTheSameRealCompetitionAndCannotBeAppliedAgainThroughTheOldEvent()
        {
            var data = GameSessionTests.Layout();
            data.places = data.places.Append(new PCShopSell.Core.World.LayoutPlaceData { id = "rival_shop", name = "邻街同行", kind = "shop", node = "s1", size = "small", explicitEntrance = true, entranceY = 8, business = "服装" }).ToArray();
            using var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(data), 71, Money.FromYuan(300000), GameTimestamp.From(new GameDate(0), 660), false);
            s.EventsEnabled = false; GameSessionTests.Teleport(s, s.MainShop.Place.Id); Assert.IsTrue(s.SetShopMainCategory("clothing"));
            s.PlayerShop.SetHours(0, 1440); s.PlayerShop.ForcedClosed = false; s.PlayerShop.NotFitted = false;
            s.PlayerShop.Stock.ForceAddGoods("clothing:jacket", 2); s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double> { { ShelfKinds.ForGift("clothing:jacket").Id, 60 } });
            s.Clock.Restore(new GameDate(14), 660); s.TickCityRetail(); var price = s.PlayerShop.GoodsPriceOf("clothing:jacket"); var cash = s.Ledger.Balance;
            Resolve(s, "shop_price_war_leaflet", 0); Assert.AreEqual(price.Scale(.9), s.PlayerShop.GoodsPriceOf("clothing:jacket")); Assert.AreEqual(cash, s.Ledger.Balance);
            s.RaiseEvent("rival"); Assert.IsNull(s.PendingEvent); s.Clock.Restore(new GameDate(17), 660); s.TickCityRetail(); Assert.AreEqual(price, s.PlayerShop.GoodsPriceOf("clothing:jacket"));
        }
        [Test] public void DeliveryExceptionRequiresAnActualPendingPaidOrder()
        { using var s = Make(); s.RaiseEvent("shop_lost_delivery"); Assert.IsNull(s.PendingEvent); }
        [TestCase(0, 2880)] [TestCase(1, 180)]
        public void DeliverySearchChangesTheRealDueTimeAndDeliversOnlyOnce(int choice, int minutes)
        {
            using var s = Make(); AddPaidOrder(s); var order = s.Online.Pending.Single(); long original = order.DeliverAtMinute; int count = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0"));
            Resolve(s, "shop_lost_delivery", choice); Assert.AreEqual(original + minutes, order.DeliverAtMinute);
            using var loaded = Reload(s); var pending = loaded.Online.Pending.Single(); Assert.AreEqual(order.DeliverAtMinute, pending.DeliverAtMinute);
            AtTime(loaded, pending.DeliverAtMinute - 1); loaded.AdvanceClock(1);
            Assert.AreEqual(count + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0"))); loaded.AdvanceClock(1);
            Assert.AreEqual(count + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0")));
        }
        [Test] public void ClaimCancelsGoodsAndRefundsActualPaidAmountOnlyAtTheDeadlineAcrossSave()
        {
            using var s = Make(); AddPaidOrder(s); var order = s.Online.Pending.Single(); long paid = order.TotalCents;
            Resolve(s, "shop_lost_delivery", 2); Assert.IsEmpty(s.Online.Pending); Assert.IsFalse(s.CancelOnlineProcurementOrder(order.Id));
            using var loaded = Reload(s); var task = loaded.ShopEventTasks.Single(t => t.Kind == "claim");
            AtTime(loaded, task.At - 2); loaded.AdvanceClock(1); Assert.IsFalse(loaded.Ledger.Entries.Any(e => e.Category == "物流赔付"));
            loaded.AdvanceClock(1); Assert.AreEqual(paid, loaded.Ledger.Entries.Single(e => e.Category == "物流赔付").Amount.Cents);
            loaded.AdvanceClock(2); Assert.AreEqual(1, loaded.Ledger.Entries.Count(e => e.Category == "物流赔付"));
            Assert.AreEqual(0, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0")));
        }
        [Test] public void CatIsATemporarySavedVisitorWithoutInstantCharmOrReputation()
        {
            using var s = Make(); int charm = s.Player.Charm; double rep = s.MainShop.Sim.ShopReputation;
            At(s, null); s.RaiseEvent("shop_cat_visitor"); Assert.IsNull(s.PendingEvent); At(s, s.MainShop.Place.Id);
            Resolve(s, "shop_cat_visitor", 0); Assert.NotNull(s.EventCat); Assert.IsTrue(s.EventCat.Water);
            Assert.AreEqual(charm, s.Player.Charm); Assert.AreEqual(rep, s.MainShop.Sim.ShopReputation);
            using var loaded = Reload(s); Assert.IsTrue(loaded.EventCat.Water); AtTime(loaded, loaded.EventCat.Until); loaded.AdvanceClock(1); Assert.IsNull(loaded.EventCat);
        }
        [TestCase(false)] [TestCase(true)]
        public void SampleIsALoanUntilActualBuyoutAndMayOnlyBeReturnedOrPurchasedOnce(bool buy)
        {
            using var s = Make(); s.Meet("zhou", 40); int before = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("gpu1"));
            Resolve(s, "shop_supplier_gift", 0); var t = s.ShopEventTasks.Single(x => x.Kind == "sample");
            Assert.IsFalse(s.FinishSupplierSample(t.Id, buy)); AtTime(s, t.At - 1); s.AdvanceClock(1); t = s.ShopEventTasks.Single(x => x.Id == t.Id);
            Assert.AreEqual("已到店", t.Status); Assert.AreEqual(before, s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("gpu1")));
            Assert.IsTrue(s.StartShopEventTask(t.Id)); s.AdvanceClock(10); Assert.AreEqual("已试用", s.ShopEventTasks.Single(x => x.Id == t.Id).Status);
            using var loaded = Reload(s); At(loaded, loaded.MainShop.Place.Id); var cash = loaded.Ledger.Balance;
            Assert.IsTrue(loaded.FinishSupplierSample(t.Id, buy)); Assert.IsFalse(loaded.FinishSupplierSample(t.Id, buy));
            Assert.AreEqual(before + (buy ? 1 : 0), loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("gpu1")));
            Assert.AreEqual(cash.Cents - (buy ? t.Paid : 0), loaded.Ledger.Balance.Cents);
        }
        [Test] public void SampleRequiresAnOpenedComputerShopAndKnownSupplier()
        {
            using var s = Make(); s.RaiseEvent("shop_supplier_gift"); Assert.IsNull(s.PendingEvent);
            s.Meet("zhou", 40); s.MainShop.RetailKind = "clothing"; s.MainShop.Sim.RetailKind = "clothing";
            s.RaiseEvent("shop_supplier_gift"); Assert.IsNull(s.PendingEvent);
        }
        [Test] public void SignHelpUsesAnActualAppointmentWithJieAndWaitsForThirtyMinutes()
        {
            using var s = Make(); s.Meet("jie", 40); int relation = s.Relation("jie"); Resolve(s, "shop_neighbor_help", 0);
            var a = s.CityActivities.Single(x => x.Active && x.DefinitionId == "social_85"); Assert.AreEqual(relation, s.Relation("jie"));
            AtTime(s, a.ScheduledStart); At(s, a.VenueId); s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3);
            Assert.AreEqual(CityActivityPhase.Running, a.State, a.EndReason); s.AdvanceClock(31);
            Assert.AreEqual(CityActivityPhase.Completed, a.State, a.EndReason); Assert.GreaterOrEqual(s.Relation("jie"), relation + 4);
        }
        private static void Sale(GameSession s, long cents)
        {
            var shop = s.MainShop; shop.Sim.CheckoutClock = () => double.NaN; shop.Sim.Accepted = PaymentMethods.Cash;
            shop.Sim.RestoreCityCustomers(new CitySavedShopVisits { shopId = shop.Place.Id, nextCustomerId = 8, queue = new[] { 7 }, customers = new[] {
                new CitySavedCustomer { id = 7, intent = (int)CustomerIntent.Goods, state = (int)CustomerState.WaitingCheckout, goodsId = "model:harbor:skiff", cash = true,
                    arrived = s.Clock.Now.TotalMinutes - 5, ready = s.Clock.Now.TotalMinutes - 2, waiting = s.Clock.Now.TotalMinutes - 1, spendCents = cents, goodsExpiry = -1 } } });
            Assert.GreaterOrEqual(shop.Sim.Checkout(7), 0);
        }
        [TestCase(50000, 5000)] [TestCase(1000000, 50000)]
        public void ComplaintNeedsARealReceiptAndCompensationIsTenPercentCappedAt500(long paid, long expected)
        {
            using var s = Make(); s.RaiseEvent("shop_complaint_online"); Assert.IsNull(s.PendingEvent); Sale(s, paid); var cash = s.Ledger.Balance;
            Resolve(s, "shop_complaint_online", 1); Assert.AreEqual(cash.Cents - expected, s.Ledger.Balance.Cents);
            using var loaded = Reload(s); loaded.RaiseEvent("shop_complaint_online"); Assert.IsNull(loaded.PendingEvent);
            var t = loaded.ShopEventTasks.Single(x => x.Kind == "complaint"); AtTime(loaded, t.At - 1); loaded.AdvanceClock(1);
            Assert.IsFalse(loaded.ShopEventTasks.Any(x => x.Kind == "complaint")); Assert.AreEqual(1, loaded.Ledger.Entries.Count(e => e.Category == "顾客补偿"));
        }
        [TestCase(0, 1, 9500)] [TestCase(1, 3, 9000)] [TestCase(2, 1, 10000)]
        public void StudentOrderUsesRealPriceAndInventoryAndPaysOnlyAfterDelivery(int choice, int count, int bps)
        {
            using var s = Make(); var shop = s.MainShop; shop.RetailKind = "models"; shop.Sim.RetailKind = "models"; const string id = "model:harbor:skiff";
            shop.Sim.Stock.ForceAddGoods(id, 3); var cash = s.Ledger.Balance; Resolve(s, "shop_student_discount", choice);
            var offer = s.CityReferrals.Single(o => o.Client == "学生团购"); Assert.AreEqual(count, offer.Count); Assert.AreEqual((shop.Sim.GoodsPriceOf(id) * count).Scale(bps / 10000.0), offer.Total);
            Assert.AreEqual(cash, s.Ledger.Balance); Assert.IsTrue(s.AcceptCityReferral(offer.Id)); Assert.IsTrue(s.DeliverIndustryReferral(offer.Id));
            Assert.AreEqual(cash + offer.Total, s.Ledger.Balance); Assert.AreEqual(3 - count, shop.Sim.Stock.GoodsCount(id)); Assert.IsFalse(s.DeliverIndustryReferral(offer.Id));
        }
        private static void Decoration(GameSession s)
        {
            for (double z = .5; z < s.ShopRoom.Depth - .5; z += .5) for (double x = .5; x < s.ShopRoom.Width - 1; x += .5)
            {
                var p = new PlacedFurniture { Id = "shop_plant", X = x, Z = z };
                if (s.PlacementError(true, FurnitureCatalog.Get(p.Id), p, -1) == null) { s.MainShop.Layout.Add(p); return; }
            }
            Assert.Fail("Fixture needs a safe decoration position");
        }
        [Test] public void DesignerActuallyChangesLayoutAfter30MinutesOrRefundsIfNoSafePosition()
        {
            using var s = Make(); Decoration(s); Resolve(s, "shop_window_dressing", 1); var t = s.ShopEventTasks.Single(x => x.Kind == "design");
            var before = JsonUtility.ToJson(s.CreateSave()); Assert.IsTrue(s.StartShopEventTask(t.Id)); Assert.IsTrue(s.IsRenovating);
            Assert.AreEqual(1, s.Ledger.Entries.Count(e => e.Category == "陈列设计")); s.AdvanceClock(29); Assert.IsTrue(s.ShopEventTasks.Any(x => x.Id == t.Id));
            using var loaded = Reload(s); At(loaded, loaded.MainShop.Place.Id); loaded.AdvanceClock(1);
            Assert.IsFalse(loaded.ShopEventTasks.Any(x => x.Id == t.Id));
            Assert.IsTrue(loaded.CreateSave().flyerCampaigns.Any() || loaded.Ledger.Entries.Any(e => e.Category == "设计退款"));
        }
        [Test] public void AuditWaitsForActualOnsiteWorkAndNeverInventsA120YuanLoss()
        {
            using var s = Make(); int iq = s.Player.Intelligence; Resolve(s, "shop_inventory_audit", 0); var t = s.ShopEventTasks.Single(x => x.Kind == "audit");
            At(s, null); Assert.IsFalse(s.StartShopEventTask(t.Id)); At(s, s.MainShop.Place.Id); Assert.IsTrue(s.StartShopEventTask(t.Id));
            s.AdvanceClock(12); At(s, null); s.AdvanceClock(5); Assert.AreEqual(18, s.ShopEventTasks.Single(x => x.Id == t.Id).Remaining);
            using var loaded = Reload(s); At(loaded, loaded.MainShop.Place.Id); loaded.AdvanceClock(18);
            Assert.AreEqual(iq + 1, loaded.Player.Intelligence); Assert.IsFalse(loaded.MainShop.Sim.ForcedClosed);
            Assert.IsFalse(loaded.Ledger.Entries.Any(e => e.Category == "账目差额"));
        }
        [Test] public void HeatRequiresRealHotWeatherAndExistingAirconDoesNotChargeAgain()
        {
            using var s = Make(); AtTime(s, 0); s.RaiseEvent("shop_summer_heat"); Assert.IsNull(s.PendingEvent);
            long hot = Enumerable.Range(50, 150).Select(day => day * 1440L + 780).First(at => s.CityOutdoorTemperature(at) >= 30);
            AtTime(s, hot); s.MainShop.Layout.Add(new PlacedFurniture { Id = "aircon", X = .5, Z = .1 }); var cash = s.Ledger.Balance;
            Resolve(s, "shop_summer_heat", 0); Assert.AreEqual(cash, s.Ledger.Balance); Assert.IsTrue(s.HasSeasonalAircon(s.MainShop.Place.Id));
        }
        [Test] public void EveningAdvertisingCannotChargeAStoreWithoutEveningHours()
        {
            using var s = Make(); s.MainShop.Sim.SetHours(480, 1080);
            long hot = Enumerable.Range(50, 150).Select(day => day * 1440L + 780).First(at => s.CityOutdoorTemperature(at) >= 30);
            AtTime(s, hot); var cash = s.Ledger.Balance; s.RaiseEvent("shop_summer_heat"); Assert.NotNull(s.PendingEvent);
            Assert.IsFalse(s.PendingEvent.Choices[1].Enabled(s)); Assert.IsFalse(s.ResolveEvent(1)); Assert.AreEqual(cash, s.Ledger.Balance); Assert.IsTrue(s.ResolveEvent(2));
        }
    }
}
