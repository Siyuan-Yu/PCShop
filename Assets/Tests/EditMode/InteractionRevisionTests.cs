using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public class InteractionRevisionTests
    {
        [Test] public void ChosenWake_CrossesMidnightAndFourWithoutFaint()
        {
            var s = GameSessionTests.Make(minute: 23 * 60); s.EventsEnabled = false;
            s.Player.Energy = 10;
            Assert.IsTrue(s.SleepUntil(7 * 60));
            Assert.AreEqual(1, s.Clock.Today.TotalDays);
            Assert.AreEqual(420, s.Clock.MinuteOfDay);
            Assert.AreEqual(8, s.SleepHoursLast24, 1e-8);
            Assert.AreEqual(0, s.Player.SleepDebt);
            Assert.AreEqual(100, s.Player.Energy);
        }
        [Test] public void SplitSleep_CountsTowardEightAndSurvivesSave()
        {
            var s = GameSessionTests.Make(minute: 20 * 60); s.EventsEnabled = false;
            Assert.IsTrue(s.Nap());
            Assert.AreEqual(2, s.SleepHoursLast24);
            var loaded = GameSession.Load(s.City, s.CreateSave()); loaded.EventsEnabled = false;
            Assert.AreEqual(2, loaded.SleepHoursLast24);
            Assert.IsTrue(loaded.SleepUntil(4 * 60));
            Assert.AreEqual(8, loaded.SleepHoursLast24);
            Assert.AreEqual(0, loaded.Player.SleepDebt);
            Assert.AreEqual(100, loaded.Player.Energy);
        }
        [Test] public void SevenHours_LeavesOneHourDebt()
        {
            var s = GameSessionTests.Make(minute: 23 * 60); s.EventsEnabled = false;
            Assert.IsTrue(s.SleepUntil(6 * 60));
            Assert.AreEqual(1, s.Player.SleepDebt);
        }
        [Test] public void EightHourShortcut_UsesDurationInsteadOfFixedEightAm()
        {
            var s = GameSessionTests.Make(minute: 21 * 60); s.EventsEnabled = false;
            Assert.IsTrue(s.SleepEightHours());
            Assert.AreEqual(5 * 60, s.Clock.MinuteOfDay);
        }
        [Test] public void WakeSelection_RejectsInvalidSameTimeAndAwayFromHome()
        {
            var s = GameSessionTests.Make(minute: 22 * 60);
            Assert.IsFalse(s.SleepUntil(-1)); Assert.IsFalse(s.SleepUntil(1440));
            Assert.IsFalse(s.SleepUntil(22 * 60)); s.Leave();
            Assert.IsFalse(s.SleepEightHours());
        }
        [Test] public void OldSaveWithoutSleepHistory_LoadsAndCanRestFully()
        {
            var s = GameSessionTests.Make(minute: 22 * 60); var d = s.CreateSave();
            d.sleepSpans = null; d.sleepDebt = 4;
            var loaded = GameSession.Load(s.City, d); loaded.EventsEnabled = false;
            Assert.AreEqual(4, loaded.Player.SleepDebt);
            Assert.IsTrue(loaded.SleepEightHours()); Assert.AreEqual(0, loaded.Player.SleepDebt);
        }
        [Test] public void PriceTags_ExactPricesBoundsAndIndependentModelsSurviveSave()
        {
            var s = GameSessionTests.Make(); var p = PartCatalog.Get("cpu0");
            long other = s.PlayerShop.PriceOf(PartCatalog.Get("cpu1"), true).Yuan;
            var range = s.PlayerShop.PartPriceRange(p, true);
            foreach (long price in new[] { range.min, (range.min + range.max) / 2, range.max })
            {
                Assert.IsTrue(s.PlayerShop.SetPartSalePrice(p, true, price));
                Assert.AreEqual(price, s.PlayerShop.PriceOf(p, true).Yuan);
            }
            Assert.IsFalse(s.PlayerShop.SetPartSalePrice(p, true, range.min - 1));
            Assert.IsFalse(s.PlayerShop.SetPartSalePrice(p, true, range.max + 1));
            Assert.AreEqual(other, s.PlayerShop.PriceOf(PartCatalog.Get("cpu1"), true).Yuan);
            var loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(range.max, loaded.PlayerShop.PriceOf(p, true).Yuan);
        }
    }
}
