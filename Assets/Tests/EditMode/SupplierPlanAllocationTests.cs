using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class SupplierPlanAllocationTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7,Money.FromYuan(2000000),GameTimestamp.From(new GameDate(0),600));s.EventsEnabled=false;
            s.MainShop.Sim.Settings.BaseCustomersPerHour=0;
            foreach(string id in new[]{"a","b","c"})
            {
                var employee=new Employee{Id="supply-test-"+id,Name="采购员"+id,DailyWageYuan=150,CheckoutMinutes=3};
                if(s.MainShop.Buyer==null)s.MainShop.Buyer=employee;else s.MainShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=EmployeeRole.Buyer,Person=employee});
                CityTestPeople.AtAssignedWork(s,employee);
            }
            return s;
        }
        private static string Buyer(string id)=>"supply-test-"+id;
        private static PurchaseOffer Offer(GameSession s)=>s.EmployeePurchaseCatalog().First(o=>o.Kind==PurchaseGoodsKind.Part&&o.Id=="gpu0"&&s.PurchaseLimit(o)>=20);
        private static EmployeePurchasePlan Plan(PurchaseOffer offer,int count,PurchaseFrequency frequency=PurchaseFrequency.Daily,bool maximum=false)
        {
            var line=offer.Line(count);line.BuyMaximum=maximum;
            return new EmployeePurchasePlan{Daily=true,HasWeeklySchedule=true,Frequency=frequency,Transport=VehicleKind.Taxi,Items=new[]{line}};
        }
        [Test] public void ThreeBuyersSplitOneMerchantsExactModelWithoutOverbookingOrChargingAtConfiguration()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);var cash=s.Ledger.Balance;
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6)));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),Plan(offer,4)));
                var draft=Plan(offer,1);
                Assert.AreEqual(cap-10,s.SupplierPlanHeadroom(Buyer("c"),draft,offer,0));
                Assert.False(s.ConfigureEmployeePurchase(Buyer("c"),Plan(offer,cap-9)));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("c"),Plan(offer,1,maximum:true)));
                var third=s.FindEmployee(Buyer("c")).Person.PurchasePlan.Items.Single();Assert.AreEqual(cap-10,third.ReservedByWeekday[0]);
                Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(cap,s.PurchaseRemaining(offer));
                Assert.True(s.DispatchEmployeePurchase(Buyer("a")));Assert.True(s.DispatchEmployeePurchase(Buyer("b")));Assert.True(s.DispatchEmployeePurchase(Buyer("c")));
                Assert.AreEqual(cap,s.EmployeePurchaseTrips.Sum(t=>t.Items.Sum(i=>i.Count)));Assert.Zero(s.PurchaseRemaining(offer));
            }
        }
        [Test] public void WednesdayReservationsDoNotReduceMondayOrFridayAndMaximumCanDifferByWeekday()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6,PurchaseFrequency.Wednesday)));
                var draft=Plan(offer,1,PurchaseFrequency.MondayWednesdayFriday,true);
                Assert.AreEqual(cap,s.SupplierPlanHeadroom(Buyer("b"),draft,offer,0));Assert.AreEqual(cap-6,s.SupplierPlanHeadroom(Buyer("b"),draft,offer,2));Assert.AreEqual(cap,s.SupplierPlanHeadroom(Buyer("b"),draft,offer,4));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),draft));
                var saved=s.FindEmployee(Buyer("b")).Person.PurchasePlan.Items.Single();
                CollectionAssert.AreEqual(new[]{cap,0,cap-6,0,cap,0,0},saved.ReservedByWeekday);
                Assert.False(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6,PurchaseFrequency.Daily)),"不能扩展到已经分满的周一和周五");
                Assert.AreEqual(PurchaseFrequency.Wednesday,s.FindEmployee(Buyer("a")).Person.PurchasePlan.Schedule);
            }
        }
        [Test] public void MerchantAndModelEachHaveTheirOwnCapacityAndDoNotShareReservations()
        {
            using(var s=Make())
            {
                var first=Offer(s);int cap=s.PurchaseLimit(first);Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(first,cap)));
                var sources=s.EmployeePurchaseCatalog().Where(o=>o.Kind==PurchaseGoodsKind.Part&&o.Id==first.Id&&o.Source.Id!=first.Source.Id).ToArray();Assert.IsNotEmpty(sources);
                var other=sources.First();Assert.AreEqual(s.PurchaseLimit(other),s.SupplierPlanHeadroom(Buyer("b"),Plan(other,1),other,0));
                var anotherModel=s.EmployeePurchaseCatalog().First(o=>o.Kind==PurchaseGoodsKind.Part&&o.Source.Id==first.Source.Id&&o.Id!=first.Id);
                Assert.AreEqual(s.PurchaseLimit(anotherModel),s.SupplierPlanHeadroom(Buyer("b"),Plan(anotherModel,1),anotherModel,0));
                Assert.Greater(Enumerable.Range(0,20).Select(i=>s.SupplierCapacity("merchant-"+i,"gpu0")).Distinct().Count(),1);
                var price= s.PurchaseLimit(first);s.AddRelation(Npcs.Jie,80);Assert.AreEqual(price,s.PurchaseLimit(first),"交情不能凭空增加店家的物理供货");
            }
        }
        [Test] public void PausingOrReducingAPlanImmediatelyReleasesItsFutureAllocation()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6)));
                Assert.AreEqual(cap-6,s.SupplierPlanMaximum(Buyer("b"),Plan(offer,1),offer,true));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,2)));
                Assert.AreEqual(cap-2,s.SupplierPlanMaximum(Buyer("b"),Plan(offer,1),offer,true));
                var paused=s.FindEmployee(Buyer("a")).Person.PurchasePlan.Copy();paused.Daily=false;Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),paused));
                Assert.AreEqual(cap,s.SupplierPlanMaximum(Buyer("b"),Plan(offer,1),offer,true));
            }
        }
        [Test] public void ExecutingBeforeAnotherBuyerCannotConsumeThatBuyersReservedShare()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6)));
                var extra=Plan(offer,cap,maximum:true);extra.Daily=false;
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),extra));Assert.True(s.DispatchEmployeePurchase(Buyer("b")));
                Assert.AreEqual(cap-6,s.EmployeePurchaseTrips.Single().Items.Single().Count);Assert.AreEqual(6,s.PurchaseRemaining(offer));
                Assert.True(s.DispatchEmployeePurchase(Buyer("a")));Assert.Zero(s.PurchaseRemaining(offer));
            }
        }
        [Test] public void WeeklyAllocationsAndFrequencySurviveJsonWithoutDuplicatingPhysicalConsumption()
        {
            using(var s=Make())
            {
                var offer=Offer(s);Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6,PurchaseFrequency.Wednesday)));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),Plan(offer,1,PurchaseFrequency.MondayWednesdayFriday,true)));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var plan=loaded.FindEmployee(Buyer("b")).Person.PurchasePlan;
                    Assert.AreEqual(PurchaseFrequency.MondayWednesdayFriday,plan.Schedule);Assert.AreEqual(s.PurchaseLimit(offer)-6,plan.Items.Single().ReservedByWeekday[2]);
                    Assert.AreEqual(s.PurchaseLimit(offer),loaded.PurchaseRemaining(loaded.EmployeePurchaseOffer(plan.Items.Single())));
                    var copy=plan.Copy();copy.Items[0].ReservedByWeekday[2]=0;Assert.Greater(plan.Items[0].ReservedByWeekday[2],0);
                }
            }
        }
        [TestCase(PurchaseFrequency.Daily,3,3)]
        [TestCase(PurchaseFrequency.MondayWednesdayFriday,3,4)]
        [TestCase(PurchaseFrequency.Wednesday,3,9)]
        public void FrequenciesUseCalendarWeekdaysRatherThanSlidingThreeDayIntervals(PurchaseFrequency frequency,int today,int next)
        {
            var plan=new EmployeePurchasePlan{HasWeeklySchedule=true,Frequency=frequency,FirstDay=1};
            Assert.AreEqual(next,plan.NextOnOrAfter(today));
            if(frequency!=PurchaseFrequency.Daily)Assert.False(plan.ScheduledOn(5));
        }
        [Test] public void LegacyEveryThreeDaysMigratesToMondayWednesdayFriday()
        {
            var old=new EmployeePurchasePlan{IntervalDays=3,FirstDay=1,LastDay=1};Assert.AreEqual(PurchaseFrequency.MondayWednesdayFriday,old.Schedule);Assert.AreEqual(2,old.NextPurchaseDay);
            using(var s=Make())
            {
                var data=s.CreateSave();data.buyer.purchasePlan=new EmployeePurchasePlan{Daily=true,IntervalDays=3,FirstDay=1,Items=new[]{Offer(s).Line(3)}};
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data))))
                {
                    var plan=loaded.MainShop.Buyer.PurchasePlan;Assert.AreEqual(PurchaseFrequency.MondayWednesdayFriday,plan.Schedule);Assert.AreEqual(7,plan.Items.Single().ReservedByWeekday.Length);Assert.Zero(plan.Items.Single().ReservedByWeekday[1]);
                }
            }
        }
        [Test] public void MaximumAtExecutionCanAbsorbReleasedSharesWithoutOverridingAnotherActivePlan()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,6)));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),Plan(offer,1,maximum:true)));
                var paused=s.FindEmployee(Buyer("a")).Person.PurchasePlan.Copy();paused.Daily=false;Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),paused));
                Assert.True(s.DispatchEmployeePurchase(Buyer("b")));Assert.AreEqual(cap,s.EmployeePurchaseTrips.Single().Items.Single().Count);
            }
        }
        [Test] public void MaximumCanUseAReleasedWednesdayEvenIfItsOriginalWednesdayShareWasZero()
        {
            using(var s=Make())
            {
                var offer=Offer(s);int cap=s.PurchaseLimit(offer);
                Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,cap,PurchaseFrequency.Wednesday)));
                Assert.True(s.ConfigureEmployeePurchase(Buyer("b"),Plan(offer,1,PurchaseFrequency.MondayWednesdayFriday,true)));
                Assert.Zero(s.FindEmployee(Buyer("b")).Person.PurchasePlan.Items.Single().ReservedByWeekday[2]);
                var paused=s.FindEmployee(Buyer("a")).Person.PurchasePlan.Copy();paused.Daily=false;Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),paused));
                s.Clock.Restore(new GameDate(2),600);CityTestPeople.AtAssignedWork(s,s.FindEmployee(Buyer("b")).Person);
                Assert.True(s.DispatchEmployeePurchase(Buyer("b")));Assert.AreEqual(cap,s.EmployeePurchaseTrips.Single().Items.Single().Count);
            }
        }
        [Test] public void DailyWholesalerPlanSkipsClosedWeekendWhenShowingNextPurchaseDay()
        {
            using(var s=Make())
            {
                var offer=Offer(s);Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),Plan(offer,3)));
                s.Clock.Restore(new GameDate(5),600);Assert.AreEqual(7,s.NextEmployeePurchaseDay(Buyer("a")));
            }
        }
        [Test] public void WholesalerWeekendCapacityIsNotPromisedInAWeeklyPlan()
        {
            using(var s=Make()){var offer=Offer(s);var plan=Plan(offer,1);Assert.Zero(s.SupplierPlanHeadroom(Buyer("a"),plan,offer,5));Assert.True(s.ConfigureEmployeePurchase(Buyer("a"),plan));Assert.Zero(s.MainShop.Buyer.PurchasePlan.Items[0].ReservedByWeekday[6]);}
        }
    }
}
