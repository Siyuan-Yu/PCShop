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
    public sealed class EnergyPayrollTests
    {
        private static GameSession Make(int minute=600)
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7391,Money.FromYuan(100000),GameTimestamp.From(new GameDate(16),minute));
            s.EventsEnabled=false;foreach(var shop in s.MyShops)shop.Sim.Settings.BaseCustomersPerHour=0;
            s.Player.Hunger=100;s.Player.Energy=100;return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static Employee Employ(GameSession s,int start,int end)
        {
            var e=new Employee{Id="m19_employee",PersonId="life_ming",Name="许明",DailyWageYuan=200,ShiftStart=start,ShiftEnd=end};
            s.MainShop.Employee=e;var p=s.CityPersonState("life_ming");p.WorkPlaceId=s.MainShop.Place.Id;p.LocationPlaceId=s.MainShop.Place.Id;p.Route=null;p.ActivityId=null;
            return e;
        }
        [Test] public void FourAmDoesNotForceASleepWithPositiveEnergy()
        {using(var s=Make(230)){s.AdvanceClock(25);Assert.AreEqual(255,s.Clock.MinuteOfDay);Assert.Greater(s.Player.Energy,0);Assert.False(s.EnergyExhausted);}}
        [TestCase(600)] [TestCase(1410)]
        public void ExhaustionGetsExactly240AwakeMinutesIncludingMidnight(int minute)
        {
            using(var s=Make(minute))
            {
                s.Player.Energy=-5;long start=s.Clock.Now.TotalMinutes;int faints=0;s.Popup+=(title,body)=>{if(title=="你晕倒了")faints++;};
                s.AdvanceClock(239);Assert.AreEqual(1,s.EnergyAwakeMinutesLeft);Assert.AreEqual(0,faints);
                s.AdvanceClock(1);Assert.AreEqual(1,faints);Assert.Greater(s.Clock.Now.TotalMinutes,start+240);Assert.Greater(s.Player.Energy,0);
            }
        }
        [Test] public void CrossingZeroMidAdvanceDoesNotCountEarlierTime()
        {using(var s=Make()){s.Player.Energy=.55;s.AdvanceClock(7);Assert.That(s.EnergyAwakeMinutesLeft,Is.InRange(239,240));}}
        [Test] public void SaveLoadPreservesCountdownAndWarnings()
        {
            using(var s=Make())
            {
                s.Player.Energy=0;s.AdvanceClock(181);
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(59,loaded.EnergyAwakeMinutesLeft);int notices=0;loaded.Notice+=t=>{if(t.Contains("剩余清醒"))notices++;};
                    loaded.AdvanceClock(1);Assert.AreEqual(0,notices);Assert.AreEqual(58,loaded.EnergyAwakeMinutesLeft);
                }
            }
        }
        [Test] public void RecoveryAndActualDaytimeSleepStartNewCyclesButReadOnlyUiDoesNot()
        {
            using(var s=Make())
            {
                s.Player.Energy=0;s.AdvanceClock(120);s.CreateSave();var ignored=s.EnergyExhaustionText;Assert.AreEqual(120,s.EnergyAwakeMinutesLeft);
                s.Player.Energy=5;Assert.False(s.EnergyExhausted);s.Player.Energy=0;Assert.AreEqual(240,s.EnergyAwakeMinutesLeft);
                Assert.True(s.SleepUntil((s.Clock.MinuteOfDay+1)%1440));Assert.Greater(s.Player.Energy,0);Assert.False(s.EnergyExhausted);
                Assert.True(s.SleepEightHours());Assert.AreEqual(s.Player.MaxEnergy,s.Player.Energy,.00001);Assert.GreaterOrEqual(s.SleepHoursLast24,8);
            }
        }
        [Test] public void LegacyZeroEnergyStartsAtMigrationAndCannotImmediatelyFaint()
        {
            using(var s=Make())
            {var d=s.CreateSave();d.energy=0;d.energyPayrollVersion=0;d.exhaustedAt=0;using(var loaded=GameSession.Load(s.City,d)){Assert.AreEqual(240,loaded.EnergyAwakeMinutesLeft);loaded.AdvanceClock(1);Assert.AreEqual(239,loaded.EnergyAwakeMinutesLeft);}}
        }
        [TestCase(540,1080,200)] [TestCase(540,1140,200)] [TestCase(540,1170,220)] [TestCase(540,1200,240)] [TestCase(1200,480,280)] [TestCase(1260,0,200)]
        public void ActualShiftMinuteAccountingAndPay(int start,int end,int expected)
        {
            using(var s=Make(start))
            {
                var e=Employ(s,start,end);int duration=GameSession.ShiftMinutes(start,end);
                Assert.AreEqual(Money.FromYuan(expected),GameSession.EstimateShiftPay(e,start,end));
                s.AdvanceClock(duration);var p=s.EmployeePayroll.Single(x=>x.EmployeeId==e.Id&&x.Minutes>0);
                Assert.AreEqual(duration,p.Minutes);Assert.AreEqual(expected*100,p.DailyCents+p.OvertimeCents);
                Assert.False(e.OnDuty(s.Clock.Now));Assert.AreEqual(start,s.CreateSave().employee.shiftStart);
            }
        }
        [Test] public void ShiftEditIsDeferredAndSnapshotSurvivesJsonWithoutPayReplay()
        {
            using(var s=Make(1200))
            {
                var e=Employ(s,1200,480);s.AdvanceClock(360);Assert.True(s.SetEmployeeShift(e.Id,540,1140));e.DailyWageYuan=300;
                using(var loaded=Reload(s))
                {
                    var copy=loaded.MainShop.Employee;Assert.AreEqual(1200,copy.ShiftStart);Assert.True(copy.OnDuty(loaded.Clock.Now));
                    loaded.AdvanceClock(360);var p=loaded.EmployeePayroll.Single(x=>x.EmployeeId==e.Id&&x.Minutes>0);
                    Assert.AreEqual(720,p.Minutes);Assert.AreEqual(28000,p.DailyCents+p.OvertimeCents);Assert.AreEqual(8000,p.Shops.Sum(x=>x.PaidOvertimeCents));
                    Assert.AreEqual(540,copy.ShiftStart);var money=loaded.Ledger.Balance;using(var second=Reload(loaded))Assert.AreEqual(money,second.Ledger.Balance);
                }
            }
        }
        [Test] public void BigAndSmallStepsAndReloadProduceIdenticalMinuteRounding()
        {
            using(var a=Make(540))using(var b=Make(540))
            {
                Employ(a,540,1155);Employ(b,540,1155);a.AdvanceClock(615);
                b.AdvanceClock(601);using(var c=Reload(b))
                {for(int i=0;i<14;i++)c.AdvanceClock(1);Assert.AreEqual(a.EmployeePayroll.Single().OvertimeCents,c.EmployeePayroll.Single().OvertimeCents);Assert.AreEqual(1000,c.EmployeePayroll.Single().OvertimeCents);}
            }
        }
        [Test] public void PersonalAbsenceDoesNotAccrueWorkAndIdleShopStillDoes()
        {
            using(var s=Make(540))
            {
                var e=Employ(s,540,1260);s.AdvanceClock(600);var person=s.CityPersonState("life_ming");person.ActivityId="private_test";person.LocationPlaceId="life_ktv";
                s.AdvanceClock(60);Assert.AreEqual(600,s.EmployeePayroll.Single().Minutes);Assert.False(s.CityEmployeeAvailable(e,s.MainShop));
            }
        }
        [Test] public void InvalidEqualHoursAreRejectedAndViewsDoNotGeneratePayroll()
        {
            using(var s=Make(500)){var e=Employ(s,540,1140);Assert.False(s.SetEmployeeShift(e.Id,540,540));Assert.False(s.SetEmployeeShift(e.Id,540,1440));s.EmployeePaySummary(e);s.GetCityCashReserve();Assert.AreEqual(0,s.EmployeePayroll.Count);}
        }
        [Test] public void FatigueInterruptsRealWorkTravelAndAnActiveExclusiveActivityOnlyOnce()
        {
            using(var s=Make())
            {
                var venue=s.City.GetPlace("life_restaurant");s.Leave();s.ArriveAtNode(venue.NodeId);Assert.True(s.Enter(venue));
                var activity=s.ScheduleCityActivity("dinner",Array.Empty<string>(),s.Clock.Now.TotalMinutes);Assert.NotNull(activity);
                Assert.True(s.GoToCityActivity(activity.Id));s.Time.SetSpeed(8);
                for(int i=0;i<5000&&activity.State!=CityActivityPhase.Running;i++)s.Tick(.025);
                Assert.AreEqual(CityActivityPhase.Running,activity.State);
                s.Player.Energy=0;var save=s.CreateSave();save.exhaustedAt=s.Clock.Now.TotalMinutes-239;
                using(var loaded=GameSession.Load(s.City,save))
                {
                    loaded.AdvanceClock(1);var interrupted=loaded.CityActivities.Single(x=>x.Id==activity.Id);
                    Assert.AreEqual(CityActivityPhase.Interrupted,interrupted.State);Assert.AreEqual(loaded.Home,loaded.CurrentPlace);
                    Assert.Null(loaded.CityIntent);Assert.Null(loaded.Trip);Assert.False(loaded.CityPlayerCanWork(loaded.MainShop));Assert.False(interrupted.RewardApplied);
                }
            }
        }
        [Test] public void ProcurementMinutesCountOnceAndRoleChangesDoNotRestartOvertime()
        {
            using(var s=Make(540))
            {
                var e=Employ(s,540,1260);s.AdvanceClock(600);
                var save=s.CreateSave();save.employeeTrips=new[]{new EmployeePurchaseTrip{EmployeeId=e.Id,ShopId=s.MainShop.Place.Id,EmployeeName=e.Name,DepartAt=s.Clock.Now.TotalMinutes,BoughtAt=s.Clock.Now.TotalMinutes+30,ReturnAt=s.Clock.Now.TotalMinutes+60,DeliveryAt=s.Clock.Now.TotalMinutes+60,Items=Array.Empty<PurchaseLine>()}};
                using(var loaded=GameSession.Load(s.City,save))
                {loaded.AdvanceClock(15);var p=loaded.EmployeePayroll.Single();Assert.AreEqual(615,p.Minutes);Assert.AreEqual(1000,p.OvertimeCents);}
            }
        }
        [Test] public void MidnightBasePlusShiftEndOvertimeAreUniqueAndStructuredAfterJson()
        {
            using(var s=Make(1200))
            {
                var e=Employ(s,1200,480);s.AdvanceClock(720);
                var entries=s.Ledger.Entries.Where(x=>x.Attribution.EmployeeId==e.Id).ToArray();
                Assert.AreEqual(2,entries.Length);Assert.AreEqual(-28000,entries.Sum(x=>x.Amount.Cents));
                Assert.True(entries.All(x=>x.Attribution.PayrollWorkday==16));
                using(var loaded=Reload(s))
                {Assert.AreEqual(2,loaded.Ledger.Entries.Count(x=>x.Attribution.EmployeeId==e.Id));Assert.AreEqual(s.Ledger.Balance,loaded.Ledger.Balance);}
            }
        }
        [Test] public void SameShiftRoleChangeKeepsOneWorkdayAndTenHourAllowance()
        {
            using(var s=Make(540))
            {var e=Employ(s,540,1260);s.AdvanceClock(600);Assert.True(s.ArrangeEmployeeWork(e.Id,s.MainShop.Place.Id,EmployeeRole.Assembler));s.AdvanceClock(15);Assert.AreEqual(1,s.EmployeePayroll.Count);Assert.AreEqual(615,s.EmployeePayroll.Single().Minutes);Assert.AreEqual(1000,s.EmployeePayroll.Single().OvertimeCents);}
        }
        [Test] public void FaintDuringDisplayAssemblyPreservesReservedPartsAndProgressThroughSave()
        {
            using(var s=Make())
            {
                s.Leave();s.ArriveAtNode(s.MainShop.Place.NodeId);Assert.True(s.Enter(s.MainShop.Place));
                foreach(var part in PCShopSell.Core.Catalog.PartCatalog.BuildList(0))s.MainShop.Sim.Stock.ForceAdd(part,true,1);
                s.Player.Energy=0;var data=s.CreateSave();data.exhaustedAt=s.Clock.Now.TotalMinutes-239;
                using(var loaded=GameSession.Load(s.City,data))
                {
                    Assert.False(loaded.AssembleDisplayPc(0));Assert.AreEqual(0,loaded.MainShop.Sim.DisplayPcs.Count);
                    Assert.AreEqual(0,loaded.MainShop.PendingDisplayTier);Assert.Greater(loaded.MainShop.PendingDisplayMinutes,0);
                    using(var again=Reload(loaded))
                    {
                        Assert.AreEqual(loaded.MainShop.PendingDisplayMinutes,again.MainShop.PendingDisplayMinutes);
                        again.Leave();again.ArriveAtNode(again.MainShop.Place.NodeId);Assert.True(again.Enter(again.MainShop.Place));
                        Assert.True(again.AssembleDisplayPc(0));Assert.AreEqual(-1,again.MainShop.PendingDisplayTier);Assert.AreEqual(1,again.MainShop.Sim.DisplayPcs.Count);
                    }
                }
            }
        }
        [Test] public void LongWorkAdvanceStopsAtFatigueBoundaryWithoutFinishingTheShift()
        {
            using(var s=Make(540))
            {
                var employer=s.City.Places.First(p=>s.OffersPlayerWork(p)&&s.PlayerWorkEnd(p)-s.PlayerWorkStart(p)>240&&s.ClosedReason(p)==null);
                s.Leave();s.ArriveAtNode(employer.NodeId);Assert.True(s.Enter(employer));Assert.True(s.SignPlayerWork(employer));
                s.Clock.Restore(new GameDate(s.WorkContract.StartDay),s.WorkContract.ShiftStart);Assert.True(s.StartPlayerWork());s.Player.Energy=0;
                long beginning=s.Clock.Now.TotalMinutes,atFaint=-1;s.Popup+=(title,body)=>{if(title=="你晕倒了")atFaint=s.Clock.Now.TotalMinutes;};
                int remaining=s.WorkMinutesLeft;s.AdvanceClock(remaining);
                Assert.False(s.IsWorking);Assert.AreEqual(WorkDayState.Absent,s.WorkContract.Days[0].State);
                Assert.AreEqual(0,s.WorkContract.Days[0].EarnedYuan);Assert.Greater(atFaint,beginning+239);Assert.AreEqual(s.Home,s.CurrentPlace);
            }
        }
        [Test] public void PausedAndHeldFramesDoNotAccrueExhaustionAndWarningsAreOncePerCycle()
        {
            using(var s=Make())
            {
                int count=0;s.Notice+=text=>{if(text.Contains("精力已耗尽")||text.Contains("剩余清醒时间不足"))count++;};s.Player.Energy=0;
                s.Time.TogglePause();for(int i=0;i<20;i++)s.Tick(.25);Assert.AreEqual(240,s.EnergyAwakeMinutesLeft);s.Time.TogglePause();
                s.Time.Hold();for(int i=0;i<20;i++)s.Tick(.25);Assert.AreEqual(240,s.EnergyAwakeMinutesLeft);s.Time.Release();
                s.AdvanceClock(211);Assert.AreEqual(3,count);s.CreateSave();s.EmployeePayroll.ToArray();Assert.AreEqual(3,count);
                s.Player.Energy=1;s.Player.Energy=0;Assert.AreEqual(4,count);Assert.AreEqual(240,s.EnergyAwakeMinutesLeft);
            }
        }
        [Test] public void TrainingAtShiftStartKeepsTheAlreadyActiveDayRateSnapshot()
        {
            using(var s=Make(540))
            {
                var e=Employ(s,540,1260);s.Leave();s.ArriveAtNode(s.MainShop.Place.NodeId);Assert.True(s.Enter(s.MainShop.Place));
                Assert.True(s.TrainEmployee());Assert.AreEqual(260,e.DailyWageYuan);Assert.AreEqual(20000,s.EmployeePayroll.Single().DailyCents);
                s.AdvanceClock(720);Assert.AreEqual(28000,s.EmployeePayroll.Single(p=>p.Minutes>0).DailyCents+s.EmployeePayroll.Single(p=>p.Minutes>0).OvertimeCents);
            }
        }
    }
}
