using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public class FoodExpiryTests
    {
        [Test] public void ExpiryCountdown_UsesTheSameInclusiveDayBoundaryAsSpoilage()
        {
            var batch = new FoodBatch { Id = "rice", Name = "白米饭", Count = 1, ExpireDay = 1 };
            Assert.AreEqual(1, batch.MinutesLeft(2879));
            Assert.AreEqual(0, batch.MinutesLeft(2880));
            var bag = new Pantry(); bag.Packed.Add(batch);
            Assert.IsEmpty(bag.Spoil(1)); Assert.AreEqual(1, bag.PackedBowls);
            Assert.IsNotEmpty(bag.Spoil(2)); Assert.AreEqual(0, bag.PackedBowls);
        }
        [TestCase(0, "剩2天")]
        [TestCase(1440, "剩1天")]
        [TestCase(2580, "剩5小时")]
        [TestCase(2875, "剩5分")]
        [TestCase(2880, "已到期")]
        [TestCase(3000, "已到期")]
        public void BadgeText_TracksGameMinutesAndNeverShowsNegativeTime(long now, string expected)
        { Assert.AreEqual(expected, FoodExpiryBadge.Countdown(1, now)); }
        [Test] public void NoDeadline_IsExplicitAndChilledDeadlineUsesItsUpdatedDay()
        {
            Assert.AreEqual("无期限", FoodExpiryBadge.Countdown(null, 50000));
            Assert.AreEqual("剩4天", FoodExpiryBadge.Countdown(3, 0));
        }
    }
}
