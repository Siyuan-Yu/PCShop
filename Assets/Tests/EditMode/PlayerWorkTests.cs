using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class PlayerWorkTests
    {
        private static GameSession Make(string business = null)
        {
            var data = GameSessionTests.Layout(); data.places.First(p => p.id == "tea").business = business;
            var s = new GameSession(CityLayout.FromData(data), 7, Money.Zero, GameTimestamp.From(new GameDate(0), 9 * 60));
            PackTests.EmptyBag(s);
            s.EventsEnabled = false;
            GameSessionTests.Teleport(s, "tea");
            return s;
        }
        private static void Attend(GameSession s, int index, int late = 0)
        {
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
            s.Clock.Restore(new GameDate(s.WorkContract.StartDay + index), s.WorkContract.ShiftStart + late);
            GameSessionTests.Teleport(s, "tea");
        }
        private static void Finish(GameSession s) => s.AdvanceClock(s.WorkMinutesLeft);

        [Test]
        public void ZeroCashAndEmptyStockCanSignSevenDayContract_WithoutDeposit()
        {
            using (var s = Make())
            {
                Assert.True(s.SignPlayerWork(s.CurrentPlace));
                Assert.AreEqual(Money.Zero, s.Ledger.Balance);
                Assert.AreEqual(1, s.WorkContract.StartDay);
                Assert.AreEqual(7, s.WorkContract.EndDay);
                Assert.AreEqual(7, s.Schedule().Count(e => e.Kind == "work"));
                Assert.False(s.IsWorking, "签约次日开始");
                Assert.False(s.SignPlayerWork(s.CurrentPlace), "不能同时签两份");
            }
        }

        [Test]
        public void CannotSignAtOwnShopOrFromElsewhere()
        {
            using (var s = Make())
            {
                Assert.False(s.OffersPlayerWork(s.MainShop.Place));
                Assert.False(s.SignPlayerWork(s.MainShop.Place));
                s.Leave();
                Assert.False(s.SignPlayerWork(s.City.GetPlace("tea")));
            }
        }

        [Test]
        public void EnteringEmployerStartsAutomatically_AndBlocksTravelUntilEnd()
        {
            using (var s = Make())
            {
                Assert.True(s.SignPlayerWork(s.CurrentPlace)); Attend(s, 0);
                Assert.True(s.IsWorking);
                Assert.False(s.CanSteer);
                Assert.False(s.GoTo(s.Home));
                s.Leave(); Assert.AreEqual("tea", s.CurrentPlace.Id);
                Assert.False(s.Enter(s.CurrentPlace));
                Finish(s);
                Assert.False(s.IsWorking);
                Assert.IsNull(s.LeaveBlocker);
                s.Leave(); Assert.IsNull(s.CurrentPlace);
            }
        }

        [TestCase(1, 141)]
        [TestCase(30, 141)]
        [TestCase(31, 132)]
        [TestCase(539, 0)]
        public void LatenessReducesOnlyTodaysWage_WithNoExtraDebt(int late, long pay)
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 0, late);
                Assert.True(s.IsWorking); Finish(s);
                Assert.AreEqual(pay, s.WorkContract.Days[0].EarnedYuan);
                Assert.AreEqual(late, s.WorkContract.Days[0].LateMinutes);
                Assert.AreEqual(Money.Zero, s.Ledger.Balance, "工资周结，迟到不会直接扣现金");
            }
        }

        [Test]
        public void FullDayProvidesLunchAndAppliesFatigue_WithoutUsingPackedFood()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 0);
                s.Player.Hunger = 30; s.Player.Mood = 50;
                Finish(s);
                Assert.True(s.WorkContract.Days[0].LunchServed);
                double expectedWorkMood=50-s.WorkContract.MoodCost;
                double maximumClimateMoodCost=.15*(s.WorkContract.ShiftEnd-s.WorkContract.ShiftStart)/60.0;
                Assert.That(s.Player.Mood,Is.InRange(expectedWorkMood-maximumClimateMoodCost,expectedWorkMood),"固定工作疲劳保留，明显不适最多每小时额外0.15心情");
                Assert.AreEqual(94, s.Player.Health, 0.01);
                Assert.Greater(s.Player.Hunger, 0);
                Assert.AreEqual(150, s.WorkContract.EarnedYuan);
            }
        }

        [Test]
        public void FullWeekPaysOnce_AfterClaim_AndAllowsRenewal()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace);
                Assert.False(s.ClaimPlayerWorkPay());
                for (int i = 0; i < 7; i++) { Attend(s, i); Finish(s); }
                Assert.True(s.WorkContract.ReadyToPay);
                Assert.AreEqual(1050, s.WorkContract.EarnedYuan);
                Assert.True(s.ClaimPlayerWorkPay());
                Assert.AreEqual(Money.FromYuan(1050), s.Ledger.Balance);
                Assert.False(s.ClaimPlayerWorkPay());
                Assert.AreEqual(Money.FromYuan(1050), s.Ledger.Balance);
                Assert.True(s.SignPlayerWork(s.CurrentPlace));
            }
        }

        [Test]
        public void LastDayAbsenceStillClosesPayroll_AndCanClaimAwayFromShop()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace);
                for (int i = 0; i < 6; i++) { Attend(s, i); Finish(s); }
                s.Leave();
                s.Clock.Restore(new GameDate(s.WorkContract.EndDay), s.WorkContract.ShiftEnd); s.AdvanceClock(1);
                Assert.True(s.WorkContract.ReadyToPay);
                Assert.AreEqual(WorkDayState.Absent, s.WorkContract.Days[6].State);
                Assert.AreEqual(150, s.WorkContract.DeductionsYuan);
                Assert.True(s.ClaimPlayerWorkPay()); Assert.AreEqual(Money.FromYuan(900), s.Ledger.Balance);
            }
        }

        [Test]
        public void MidShiftJsonRestoreKeepsWorkLockAndDoesNotDuplicateWageOrFatigue()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 0, 31); s.AdvanceClock(60);
                var data = s.CreateSave();
                Assert.AreNotSame(s.WorkContract.Days[0], data.playerWork.Days[0]);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data))))
                {
                      loaded.EventsEnabled=false;
                    Assert.True(loaded.IsWorking); Assert.False(loaded.CanSteer);
                      Finish(s);
                    Finish(loaded); Assert.AreEqual(132, loaded.WorkContract.EarnedYuan);
                      Assert.AreEqual(s.Player.Mood,loaded.Player.Mood,1e-10,"读档后半班不能重复或漏掉体感/工作心情");
                      Assert.AreEqual(s.Player.Energy,loaded.Player.Energy,1e-10);
                      Assert.AreEqual(s.Player.Health,loaded.Player.Health,1e-10);
                      long cash=loaded.Ledger.Balance.Cents;
                      s.AdvanceClock(2);loaded.AdvanceClock(2);
                      Assert.AreEqual(132, loaded.WorkContract.EarnedYuan);
                      Assert.AreEqual(cash,loaded.Ledger.Balance.Cents,"后续时钟不重复发工资");
                      Assert.AreEqual(s.Player.Mood,loaded.Player.Mood,1e-10,"持续气候也必须与未读档会话逐分相同");
                      Assert.AreEqual(s.Player.Energy,loaded.Player.Energy,1e-10);
                      Assert.AreEqual(s.Player.Health,loaded.Player.Health,1e-10);
                }
            }
        }

        [Test]
        public void AttendedWeeklyWageGetsBankruptcyGraceUntilClaimDeadline()
        {
            using (var s = Make())
            {
                s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(10), "待周结");
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 0); Finish(s);
                for (int day = 1; day <= 7; day++)
                {
                    s.Player.Health = s.Player.Energy = s.Player.Hunger = 100;
                    s.Clock.Restore(new GameDate(day), 23 * 60 + 59); s.AdvanceClock(1);
                    Assert.False(s.IsGameOver, "不能在周薪领取前因为三天负现金结束游戏");
                }
                Assert.True(s.WorkContract.ReadyToPay); Assert.True(s.ClaimPlayerWorkPay());
                Assert.AreEqual(Money.FromYuan(140), s.Ledger.Balance);
            }
        }

        [Test]
        public void SigningWithoutAttendingKeepsDebtAndEarnsNoWagesButLifeContinues()
        {
            using (var s = Make())
            {
                s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(10), "无出勤"); s.SignPlayerWork(s.CurrentPlace); s.Leave();
                for (int day = 0; day < 3; day++) { s.Clock.Restore(new GameDate(day), 23 * 60 + 59); s.AdvanceClock(1); }
                  Assert.False(s.IsGameOver,"v3经济失败可继续，不依赖签空合同取得保护");
                  Assert.Less(s.Ledger.Balance.Cents,0,"未出勤不能凭空消掉欠款");
                  Assert.Zero(s.WorkContract.EarnedYuan,"未出勤不生成工资");
                  Assert.True(s.WorkContract.Days.Any(d=>d.State==WorkDayState.Absent));
                Assert.False(s.IsWorking);
            }
        }

        [Test]
        public void HospitalInterruptsShiftAndReleasesLock_InsteadOfWorkingFromHome()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 0);
                s.Player.Health = 1; s.Player.Hunger = s.Player.Energy = 0;
                s.AdvanceClock(2);
                Assert.False(s.IsWorking);
                Assert.AreEqual(WorkDayState.Absent, s.WorkContract.Days[0].State);
                Assert.AreEqual(s.Home, s.CurrentPlace);
                Assert.IsNull(s.LeaveBlocker);
            }
        }

        [Test]
        public void OldOrInvalidWorkDataDoesNotCreateAWorkLock()
        {
            using (var s = Make())
            {
                var data = s.CreateSave(); Assert.IsNull(data.playerWork);
                data.playerWork = new PlayerWorkContract { PlaceId = "tea", Days = null };
                using (var loaded = GameSession.Load(s.City, data)) Assert.False(loaded.IsWorking);
            }
        }

        [Test]
        public void BrokeWorkerCanFinishTheWeekWithWorkMealsAndSleep_WithoutBuyingStockOrFood()
        {
            using (var s = Make())
            {
                s.SignPlayerWork(s.CurrentPlace);
                s.Clock.Restore(new GameDate(s.WorkContract.StartDay), 9 * 60);
                for (int i = 0; i < 7; i++)
                {
                    GameSessionTests.Teleport(s, "tea");
                    Assert.True(s.IsWorking); Finish(s);
                    Assert.False(s.IsGameOver);
                    Assert.AreEqual(s.WorkContract.StartDay + i, s.Clock.Today.TotalDays);
                    GameSessionTests.Teleport(s, "flat");
                    if (i < 6) Assert.True(s.Sleep());
                }
                Assert.True(s.WorkContract.ReadyToPay);
                Assert.AreEqual(Money.Zero, s.Ledger.Balance);
                Assert.True(s.ClaimPlayerWorkPay());
                Assert.AreEqual(Money.FromYuan(1050), s.Ledger.Balance);
            }
        }

        [TestCase("服装", 10, 18, 135, 5, 6)]
        [TestCase("家具", 9, 19, 155, 8, 10)]
        [TestCase("面包", 22, 30, 175, 11, 12)]
        [TestCase("便利店", 23, 31, 185, 12, 14)]
        public void DifferentStoresSaveTheirOwnWageShiftAndFatigue(string business, int start, int end, int wage, int health, int mood)
        {
            using (var s = Make(business))
            {
                var offer = s.PlayerWorkFor(s.CurrentPlace);
                Assert.AreEqual(start * 60, offer.Start); Assert.AreEqual(end * 60, offer.End);
                Assert.AreEqual(wage, offer.Wage);
                Assert.True(s.SignPlayerWork(s.CurrentPlace));
                Assert.AreEqual(wage, s.WorkContract.DailyWageYuan);
                Assert.AreEqual(health, s.WorkContract.HealthCost); Assert.AreEqual(mood, s.WorkContract.MoodCost);
            }
        }

        [Test]
        public void ExistingStoreOffersAverageAround150_WithDifferentLengthsAndNightShifts()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using (var s = new GameSession(city, 7, Money.Zero, GameTimestamp.From(new GameDate(0), 9 * 60)))
            {
                var offers = city.Places.Where(p => p.Kind == PlaceKind.Shop).Select(s.PlayerWorkFor).ToArray();
                Assert.That(offers.Average(o => o.Wage), Is.InRange(140, 160));
                Assert.Greater(offers.Select(o => o.End - o.Start).Distinct().Count(), 1);
                Assert.Greater(offers.Select(o => o.Start).Distinct().Count(), 3);
                Assert.True(offers.Any(o => o.End >= 1440));
                Assert.Greater(offers.Where(o => o.End >= 1440).Average(o => o.HealthCost), offers.Where(o => o.End < 1440).Average(o => o.HealthCost));
            }
        }

        [Test]
        public void ContractAllowsStaffEntryAfterClosing_OvernightShiftDoesNotFaintAtFour_AndDaytimeSleepWorks()
        {
            using (var s = Make("面包"))
            {
                var employer = s.CurrentPlace;
                s.Clock.Restore(s.Clock.Today, 22 * 60);
                Assert.IsNotNull(s.ClosedReason(employer));
                s.Clock.Restore(s.Clock.Today, 10 * 60); Assert.True(s.SignPlayerWork(employer));
                Attend(s, 0);
                Assert.True(s.IsWorking); Assert.IsNull(s.ClosedReason(employer));
                Finish(s);
                Assert.AreEqual(s.WorkContract.StartDay + 1, s.Clock.Today.TotalDays);
                Assert.AreEqual(6 * 60, s.Clock.MinuteOfDay);
                Assert.AreEqual(175, s.WorkContract.EarnedYuan);
                Assert.AreEqual(employer, s.CurrentPlace, "夜班凌晨4点不能强制晕倒回家");
                GameSessionTests.Teleport(s, "flat"); Assert.True(s.IsSleepTime);
                Assert.True(s.Sleep()); Assert.AreEqual(14 * 60, s.Clock.MinuteOfDay);
                s.Clock.Restore(s.Clock.Today, 21 * 60 + 30);
                Assert.True(s.Sleep(), "允许自行决定白天或夜间睡眠，不再按时刻阻止睡觉");
            }
        }

        [Test]
        public void LastNightPaysOnlyAfterNextMorning_AndMidnightJsonKeepsCorrectShiftDay()
        {
            using (var s = Make("面包"))
            {
                s.SignPlayerWork(s.CurrentPlace); Attend(s, 6); s.AdvanceClock(120);
                Assert.True(s.IsWorking); Assert.False(s.WorkContract.ReadyToPay);
                Assert.AreEqual(6, s.WorkContract.WorkingIndex);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.True(loaded.IsWorking); Finish(loaded);
                    Assert.AreEqual(6 * 60, loaded.Clock.MinuteOfDay);
                    Assert.True(loaded.WorkContract.ReadyToPay);
                    Assert.AreEqual(175, loaded.WorkContract.EarnedYuan);
                    Assert.True(loaded.ClaimPlayerWorkPay());
                    Assert.AreEqual(Money.FromYuan(175), loaded.Ledger.Balance);
                }
            }
        }
    }
}
