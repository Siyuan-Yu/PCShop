using System;
using System.Collections.Generic;
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
        public void Weekday_ContinuesAcrossMonthAndYear()
        {
            Assert.AreEqual(GameWeekday.Tuesday, GameDate.FromYmd(1, 1, 30).Weekday);
            Assert.AreEqual(GameWeekday.Wednesday, GameDate.FromYmd(1, 2, 1).Weekday);
            Assert.AreEqual(GameWeekday.Thursday, GameDate.FromYmd(2, 1, 1).Weekday);
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

    public class GameTimestampTests
    {
        [Test]
        public void From_SplitsDateAndTime()
        {
            var t = GameTimestamp.From(GameDate.FromYmd(1, 2, 3), 8 * 60 + 30);
            Assert.AreEqual(GameDate.FromYmd(1, 2, 3), t.Date);
            Assert.AreEqual(8, t.Hour);
            Assert.AreEqual(30, t.Minute);
            Assert.AreEqual(8 * 60 + 30, t.MinuteOfDay);
        }

        [Test]
        public void AddMinutes_RollsIntoNextDay()
        {
            var t = GameTimestamp.From(GameDate.FromYmd(1, 1, 1), 23 * 60 + 50);
            var later = t.AddMinutes(20);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), later.Date);
            Assert.AreEqual(10, later.MinuteOfDay);
        }

        [Test]
        public void Comparison_AndMinutesUntil()
        {
            var a = GameTimestamp.From(GameDate.FromYmd(1, 1, 1), 100);
            var b = GameTimestamp.From(GameDate.FromYmd(1, 1, 2), 100);
            Assert.IsTrue(a < b);
            Assert.AreEqual(1440, a.MinutesUntil(b));
        }

        [Test]
        public void ToString_IsReadable()
        {
            var t = GameTimestamp.From(GameDate.FromYmd(1, 1, 1), 9 * 60 + 5);
            Assert.AreEqual("第1年1月1日 09:05", t.ToString());
        }
    }

    public class GameClockTests
    {
        private static GameClock At(int hour, int minute = 0)
        {
            return new GameClock(GameDate.FromYmd(1, 1, 1), hour * 60 + minute);
        }

        [Test]
        public void Advance_MovesTime_WithinSameDay()
        {
            var clock = At(8);
            clock.Advance(90);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 1), clock.Today);
            Assert.AreEqual(9 * 60 + 30, clock.MinuteOfDay);
        }

        [Test]
        public void Advance_RaisesTimeAdvanced_WithRangeAndUpdatedState()
        {
            var clock = At(8);
            GameTimestamp from = default, to = default;
            int calls = 0;
            clock.TimeAdvanced += (f, t) =>
            {
                from = f;
                to = t;
                calls++;
                Assert.AreEqual(t, clock.Now, "触发时时钟应已更新到区间终点");
            };

            clock.Advance(30);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(8 * 60, from.MinuteOfDay);
            Assert.AreEqual(8 * 60 + 30, to.MinuteOfDay);
        }

        [Test]
        public void Advance_AcrossMidnight_SplitsIntoSlices_AndRaisesDayAdvanced()
        {
            var clock = At(23, 30);
            var slices = new List<(long from, long to)>();
            var days = new List<(GameDate prev, GameDate now)>();
            clock.TimeAdvanced += (f, t) => slices.Add((f.TotalMinutes, t.TotalMinutes));
            clock.DayAdvanced += (p, n) => days.Add((p, n));

            clock.Advance(90); // 23:30 -> 01:00

            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), clock.Today);
            Assert.AreEqual(60, clock.MinuteOfDay);

            Assert.AreEqual(2, slices.Count);
            Assert.AreEqual(30, slices[0].to - slices[0].from);
            Assert.AreEqual(60, slices[1].to - slices[1].from);
            Assert.AreEqual(slices[0].to, slices[1].from, "两段应首尾相接");

            Assert.AreEqual(1, days.Count);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 1), days[0].prev);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), days[0].now);
        }

        [Test]
        public void Advance_ManyDays_RaisesDayAdvancedForEach()
        {
            var clock = At(0);
            int days = 0;
            clock.DayAdvanced += (p, n) => days++;
            clock.Advance(3 * GameClock.MinutesPerDay + 100);
            Assert.AreEqual(3, days);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 4), clock.Today);
            Assert.AreEqual(100, clock.MinuteOfDay);
        }

        [Test]
        public void Advance_ExactlyToMidnight_RollsOver()
        {
            var clock = At(23, 0);
            clock.Advance(60);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), clock.Today);
            Assert.AreEqual(0, clock.MinuteOfDay);
        }

        [Test]
        public void Advance_Zero_DoesNothing()
        {
            var clock = At(8);
            int calls = 0;
            clock.TimeAdvanced += (f, t) => calls++;
            clock.Advance(0);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(8 * 60, clock.MinuteOfDay);
        }

        [Test]
        public void MinutesUntil_And_AdvanceTo_WrapAroundMidnight()
        {
            var clock = At(23, 0);
            Assert.AreEqual(9 * 60, clock.MinutesUntil(8 * 60)); // 23:00 睡到 8:00
            clock.AdvanceTo(8 * 60);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), clock.Today);
            Assert.AreEqual(8 * 60, clock.MinuteOfDay);
            Assert.AreEqual(0, clock.MinutesUntil(8 * 60), "恰好是该时刻时返回 0");
        }

        [Test]
        public void AdvanceDay_GoesToNextMidnight()
        {
            var clock = At(15, 20);
            GameDate prev = default, now = default;
            int calls = 0;
            clock.DayAdvanced += (p, n) => { prev = p; now = n; calls++; };

            clock.AdvanceDay();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 1), prev);
            Assert.AreEqual(GameDate.FromYmd(1, 1, 2), now);
            Assert.AreEqual(0, clock.MinuteOfDay);
        }

        [Test]
        public void Restore_DoesNotRaiseEvents()
        {
            var clock = At(8);
            int calls = 0;
            clock.TimeAdvanced += (f, t) => calls++;
            clock.DayAdvanced += (p, n) => calls++;

            clock.Restore(GameDate.FromYmd(3, 4, 5), 12 * 60);

            Assert.AreEqual(0, calls);
            Assert.AreEqual(GameDate.FromYmd(3, 4, 5), clock.Today);
            Assert.AreEqual(12 * 60, clock.MinuteOfDay);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var clock = At(8);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.MinutesUntil(1440));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(new GameDate(0), -1));
        }
    }
}
