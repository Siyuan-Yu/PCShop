using System;
using NUnit.Framework;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public class GameDateTests
    {
        [Test]
        public void FromYmd_RoundTrips()
        {
            var d = GameDate.FromYmd(2, 3, 15);
            Assert.AreEqual(2, d.Year);
            Assert.AreEqual(3, d.Month);
            Assert.AreEqual(15, d.Day);
        }

        [Test]
        public void Start_IsYear1Month1Day1_Monday()
        {
            var d = new GameDate(0);
            Assert.AreEqual(1, d.Year);
            Assert.AreEqual(1, d.Month);
            Assert.AreEqual(1, d.Day);
            Assert.AreEqual(GameWeekday.Monday, d.Weekday);
        }

        [Test]
        public void AddDays_RollsOverMonthAndYear()
        {
            var endOfYear1 = GameDate.FromYmd(1, 12, 30);
            var next = endOfYear1.AddDays(1);
            Assert.AreEqual(2, next.Year);
            Assert.AreEqual(1, next.Month);
            Assert.AreEqual(1, next.Day);
        }

        [Test]
        public void Weekday_CyclesEverySevenDays()
        {
            var d = new GameDate(0);
            Assert.AreEqual(GameWeekday.Sunday, d.AddDays(6).Weekday);
            Assert.AreEqual(GameWeekday.Monday, d.AddDays(7).Weekday);
            Assert.IsTrue(d.AddDays(5).IsWeekend);
            Assert.IsFalse(d.AddDays(4).IsWeekend);
        }

        [Test]
        public void MonthBoundaries_AreDetected()
        {
            Assert.IsTrue(GameDate.FromYmd(1, 5, 1).IsMonthStart);
            Assert.IsTrue(GameDate.FromYmd(1, 5, 30).IsMonthEnd);
            Assert.IsFalse(GameDate.FromYmd(1, 5, 15).IsMonthEnd);
        }

        [Test]
        public void Comparison_AndDaysUntil()
        {
            var a = GameDate.FromYmd(1, 1, 10);
            var b = GameDate.FromYmd(1, 2, 10);
            Assert.IsTrue(a < b);
            Assert.AreEqual(30, a.DaysUntil(b));
            Assert.AreEqual(-30, b.DaysUntil(a));
        }

        [Test]
        public void NegativeDays_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(0).AddDays(-1));
        }
    }

    public class GameClockTests
    {
        [Test]
        public void AdvanceDay_MovesDateAndRaisesEvent()
        {
            var clock = new GameClock(GameDate.FromYmd(1, 1, 1));
            GameDate prev = default, now = default;
            int calls = 0;
            clock.DayAdvanced += (p, n) => { prev = p; now = n; calls++; };

            clock.AdvanceDay();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 1), prev);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), now);
            Assert.AreEqual(now, clock.Today);
        }

        [Test]
        public void Restore_DoesNotRaiseEvent()
        {
            var clock = new GameClock(new GameDate(0));
            int calls = 0;
            clock.DayAdvanced += (p, n) => calls++;

            clock.Restore(GameDate.FromYmd(3, 4, 5));

            Assert.AreEqual(0, calls);
            Assert.AreEqual(GameDate.FromYmd(3, 4, 5), clock.Today);
        }
    }

    public class TimeBudgetTests
    {
        [Test]
        public void TrySpend_ReducesRemaining()
        {
            var b = new TimeBudget(600);
            Assert.IsTrue(b.TrySpend(240));
            Assert.AreEqual(360, b.RemainingMinutes);
            Assert.AreEqual(240, b.SpentMinutes);
        }

        [Test]
        public void TrySpend_FailsWhenNotEnough_AndKeepsState()
        {
            var b = new TimeBudget(100);
            Assert.IsFalse(b.TrySpend(101));
            Assert.AreEqual(100, b.RemainingMinutes);
        }

        [Test]
        public void SpendingAll_ExhaustsBudget()
        {
            var b = new TimeBudget(60);
            Assert.IsTrue(b.TrySpend(60));
            Assert.IsTrue(b.IsExhausted);
            Assert.IsFalse(b.TrySpend(1));
        }

        [Test]
        public void Refill_RestoresAndCanChangeTotal()
        {
            var b = new TimeBudget(600);
            b.TrySpend(500);
            b.Refill();
            Assert.AreEqual(600, b.RemainingMinutes);

            b.Refill(720); // 例如买车后每日可用时间增加
            Assert.AreEqual(720, b.TotalMinutes);
            Assert.AreEqual(720, b.RemainingMinutes);
        }

        [Test]
        public void Adjust_ClampsAtZero()
        {
            var b = new TimeBudget(100);
            b.Adjust(-500);
            Assert.AreEqual(0, b.RemainingMinutes);
            b.Adjust(30);
            Assert.AreEqual(30, b.RemainingMinutes);
        }

        [Test]
        public void NegativeSpend_Throws()
        {
            var b = new TimeBudget(100);
            Assert.Throws<ArgumentOutOfRangeException>(() => b.TrySpend(-1));
        }
    }
}
