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
    public sealed class OldMarketTests
    {
        private static GameSession Make()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 731, Money.FromYuan(100000), GameTimestamp.From(new GameDate(42), 600), starterStock: false, smartphone: false, startWithShop: false);
            s.EventsEnabled = false; s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100; s.Tick(0); return s;
        }
        private static void At(GameSession s, string place = GameSession.OldMarketVenueId)
        { s.StopTrip(); typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, place == null ? null : s.City.GetPlace(place)); if (place != null) typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s, s.City.GetPlace(place).NodeId); }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        [Test] public void IndependentMarketHasRouteAndOpeningHoursAndDoesNotRequireOwnedShop()
        {
            using var s = Make(); var p = s.City.GetPlace(GameSession.OldMarketVenueId);
            Assert.AreNotEqual("stall_cpu", p.Id); Assert.IsFalse(p.IsTradable); Assert.AreEqual(540, p.OpenMinute); Assert.AreEqual(1320, p.CloseMinute);
            Assert.Less(s.CityTravelMinutes(p), int.MaxValue / 8); Assert.AreEqual(p.Id, CityLifeCatalog.Activity("oldmarket_visit").VenueId);
            Assert.AreEqual("stall_cpu", CityLifeCatalog.Activity("old_market").VenueId);
            Assert.IsFalse(Gifts.All.Any(g => g.Id.StartsWith("oldmarket:", StringComparison.Ordinal)), "Market collectibles must not enter ordinary retail/online catalogues or the general price index");
            At(s); Assert.IsTrue(s.AtOldMarket); Assert.IsFalse(s.HasOpenedComputerShop);
            s.Clock.Restore(new GameDate(42), 1320); Assert.IsFalse(s.AtOldMarket); Assert.IsFalse(s.BuyOldMarketGoods("oldmarket:watch"));
        }
        [Test] public void PurchaseUsesActualMoneyPhysicalBackpackWeightAndSavedDailyStock()
        {
            using var s = Make(); At(s); var cash = s.Ledger.Balance; double kg = s.Backpack.WeightKg;
            Assert.IsTrue(s.BuyOldMarketGoods("oldmarket:watch")); Assert.AreEqual(cash - Money.FromYuan(2000), s.Ledger.Balance);
            Assert.AreEqual(1, s.Backpack.GiftCount("oldmarket:watch")); Assert.AreEqual(kg + .15, s.Backpack.WeightKg, .0001);
            Assert.IsFalse(s.BuyOldMarketGoods("oldmarket:watch"));
            using var loaded = Reload(s); At(loaded); Assert.AreEqual(0, loaded.OldMarketRemaining("oldmarket:watch"));
            Assert.AreEqual(1, loaded.Backpack.GiftCount("oldmarket:watch")); loaded.Clock.Restore(new GameDate(43), 600);
            Assert.AreEqual(1, loaded.OldMarketRemaining("oldmarket:watch"));
        }
        [Test] public void ShoppingRequiresPresenceCashAndCapacity()
        {
            using var s = Make(); Assert.IsFalse(s.BuyOldMarketGoods("oldmarket:watch")); At(s);
            s.Backpack.AddGift("oldmarket:clock", 100); Assert.IsFalse(s.BuyOldMarketGoods("oldmarket:watch"));
            using var poor = new GameSession(s.City, 1, Money.Zero, s.Clock.Now, false, false, false); At(poor);
            Assert.IsFalse(poor.BuyOldMarketGoods("oldmarket:watch")); Assert.AreEqual(1, poor.OldMarketRemaining("oldmarket:watch"));
        }
        [TestCase("radio", 1)] [TestCase("cassette", 2)] [TestCase("speaker", 3)]
        public void RepairRequiresRealFaultyItemDiagnosisCorrectChoiceAndTenMinutes(string key, int answer)
        {
            using var s = Make(); At(s); string id = "oldmarket:" + key;
            Assert.IsFalse(s.RepairOldMarketGoods(id, answer)); Assert.IsTrue(s.BuyOldMarketGoods(id));
            Assert.IsFalse(s.RepairOldMarketGoods(id, answer)); Assert.IsTrue(s.InspectOldMarketGoods(id));
            s.AdvanceClock(4); Assert.IsFalse(s.OldMarketInspected(id)); s.AdvanceClock(1); Assert.IsTrue(s.OldMarketInspected(id));
            var cash = s.Ledger.Balance; Assert.IsTrue(s.RepairOldMarketGoods(id, answer));
            Assert.AreEqual(cash - Money.FromYuan(OldMarketCatalog.Get(id).RepairFee), s.Ledger.Balance);
            Assert.AreEqual(0, s.Backpack.GiftCount(id + ":faulty")); Assert.AreEqual(0, s.Backpack.GiftCount(id));
            s.AdvanceClock(9); Assert.AreEqual(0, s.Backpack.GiftCount(id)); s.AdvanceClock(1);
            Assert.AreEqual(1, s.Backpack.GiftCount(id)); Assert.IsFalse(s.RepairOldMarketGoods(id, answer));
        }
        [TestCase("radio")] [TestCase("cassette")] [TestCase("speaker")]
        public void MasterRepairsInOneActionWithoutPriorInspectionAndReturnsOneItemAfter15Minutes(string key)
        {
            using var s = Make(); At(s); string id = "oldmarket:" + key;
            Assert.IsFalse(s.AskOldMarketRepair(id)); Assert.IsTrue(s.BuyOldMarketGoods(id)); var cash = s.Ledger.Balance;
            Assert.IsFalse(s.OldMarketInspected(id)); Assert.IsTrue(s.AskOldMarketRepair(id));
            Assert.AreEqual(cash - Money.FromYuan(OldMarketCatalog.Get(id).RepairFee), s.Ledger.Balance);
            Assert.IsFalse(s.AskOldMarketRepair(id)); Assert.AreEqual(0, s.Backpack.GiftCount(id + ":faulty"));
            s.AdvanceClock(14); Assert.AreEqual(0, s.Backpack.GiftCount(id));
            using var loaded = Reload(s); At(loaded); loaded.AdvanceClock(1);
            Assert.AreEqual(1, loaded.Backpack.GiftCount(id)); Assert.AreEqual(0, loaded.Backpack.GiftCount(id + ":faulty"));
            loaded.AdvanceClock(10); Assert.AreEqual(1, loaded.Backpack.GiftCount(id));
            Assert.IsFalse(loaded.AskOldMarketRepair(id));
        }
        [Test] public void WrongRepairReturnsFaultyItemAndCanRetryWithoutGrantingNormalGoods()
        {
            using var s = Make(); At(s); const string id = "oldmarket:radio";
            s.BuyOldMarketGoods(id); s.InspectOldMarketGoods(id); s.AdvanceClock(5); double energy = s.Player.Energy;
            Assert.IsTrue(s.RepairOldMarketGoods(id, 3)); s.AdvanceClock(10);
            Assert.AreEqual(1, s.Backpack.GiftCount(id + ":faulty")); Assert.AreEqual(0, s.Backpack.GiftCount(id)); Assert.Less(s.Player.Energy, energy);
            Assert.IsTrue(s.RepairOldMarketGoods(id, 1)); s.AdvanceClock(10); Assert.AreEqual(1, s.Backpack.GiftCount(id));
        }
        [Test] public void RepairPausesAwayAndSurvivesSaveWithoutDuplicatingTheWorkbenchItem()
        {
            using var s = Make(); At(s); const string id = "oldmarket:radio"; s.BuyOldMarketGoods(id); s.InspectOldMarketGoods(id); s.AdvanceClock(5);
            s.RepairOldMarketGoods(id, 1); s.AdvanceClock(3); At(s, null); s.AdvanceClock(20); Assert.AreEqual(7, s.OldMarketProgress.Remaining);
            using var loaded = Reload(s); At(loaded); loaded.AdvanceClock(7); Assert.AreEqual(1, loaded.Backpack.GiftCount(id));
            loaded.AdvanceClock(10); Assert.AreEqual(1, loaded.Backpack.GiftCount(id)); Assert.AreEqual(0, loaded.Backpack.GiftCount(id + ":faulty"));
        }
        [Test] public void BuybackConsumesActualItemOnceAndCannotGenerateProfitFromImmediateResale()
        {
            using var s = Make(); At(s); var cash = s.Ledger.Balance; s.BuyOldMarketGoods("oldmarket:watch");
            Assert.IsTrue(s.SellOldMarketGoods("oldmarket:watch")); Assert.AreEqual(cash - Money.FromYuan(900), s.Ledger.Balance);
            Assert.IsFalse(s.SellOldMarketGoods("oldmarket:watch")); Assert.IsFalse(s.SellOldMarketGoods("wine"));
        }
        [TestCase(71, "act_oldmarket_guwen")] [TestCase(72, "act_oldmarket_leiming")]
        public void InvitationsActuallyMeetAtNewMarketAndRewardOnlyAfterWalkAndLearning(int n, string eventId)
        {
            using var s = Make(); var cash = s.Ledger.Balance; int iq = s.Player.Intelligence;
            s.RaiseEvent(eventId); Assert.NotNull(s.PendingEvent); Assert.IsTrue(s.PendingEvent.Choices[0].Enabled(s), s.PendingEvent.Choices[0].DisabledReason);
            Assert.IsTrue(s.ResolveEvent(0)); var a = s.CityActivities.Last(x => x.Active); Assert.AreEqual(GameSession.OldMarketVenueId, a.VenueId);
            Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreEqual(iq, s.Player.Intelligence);
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); At(s); s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3);
            Assert.AreEqual(CityActivityPhase.Running, a.State, a.EndReason); s.AdvanceClock(4);
            Assert.IsTrue(s.ChooseCityActivity(a.Id, 2)); s.AdvanceClock(5);
            for (int i = 0; i < 2; i++) { Assert.IsTrue(s.ChooseCityActivity(a.Id, 1)); s.AdvanceClock(4); }
            s.AdvanceClock(100); Assert.AreEqual(CityActivityPhase.Completed, a.State, a.EndReason);
            Assert.AreEqual(iq + 1, s.Player.Intelligence); Assert.AreEqual(cash, s.Ledger.Balance);
        }
    }
}
