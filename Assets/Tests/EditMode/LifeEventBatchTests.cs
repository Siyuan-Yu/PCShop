using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class LifeEventBatchTests
    {
        private static GameSession Make()
        {
            using var f=DistrictProgressionTests.Make();using var first=new GameSession(f.City,1221,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(42),660),false,true,true);
            first.Tick(0);var d=first.CreateSave();d.districtProgress=new DistrictProgress{UnlockedTier=3};var s=GameSession.Load(first.City,d);
            s.EventsEnabled=false;s.Player.Health=s.Player.Hunger=s.Player.Energy=100;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.MainShop.Sim.NotFitted=false;s.MainShop.Sim.ForcedClosed=false;s.MainShop.Sim.SetHours(0,1440);At(s,s.MainShop.Place.Id);return s;
        }
        private static void At(GameSession s,string id)
        {s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,id==null?null:s.City.GetPlace(id));if(id!=null)typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.City.GetPlace(id).NodeId);}
        private static void Person(GameSession s,string id,string place)
        {var p=s.CityPersonState(id);p.Route=null;p.VisitId=null;p.ActivityId=null;p.LocationPlaceId=place;p.Task=CityPersonTask.Leisure;}
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Time(GameSession s,long at){s.Clock.Restore(new GameDate((int)(at/1440)),(int)(at%1440));s.Player.Health=s.Player.Hunger=s.Player.Energy=100;}
        private static void Resolve(GameSession s,string id,int choice){s.RaiseEvent(id);Assert.NotNull(s.PendingEvent,id);Assert.IsTrue(s.ResolveEvent(choice));}
        private static void Attend(GameSession s,CityActivity a)
        {Time(s,a.ScheduledStart);At(s,a.VenueId);Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace));s.AdvanceClock(3);Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);}
        private static object Call(GameSession s,string name,params object[] args)=>typeof(GameSession).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,args);
        [TestCase(0,500)] [TestCase(1,2000)] [TestCase(2,5000)]
        public void DonationNeedsRealRegistrationAndBuysActualLibraryStockAfterThreeDays(int choice,int amount)
        {
            using var s=Make();var cash=s.Ledger.Balance;int charm=s.Player.Charm;Resolve(s,"money_charity_box",choice);Assert.AreEqual(cash,s.Ledger.Balance);
            var t=s.LifeTasks.Single(x=>x.Kind=="donation");Assert.IsFalse(s.StartLifeTask(t.Id));At(s,GameSession.LifeServicePlace);Person(s,"life_teacher",GameSession.LifeServicePlace);
            Assert.IsTrue(s.StartLifeTask(t.Id));Assert.AreEqual(cash-Money.FromYuan(amount),s.Ledger.Balance);s.AdvanceClock(5);At(s,s.Home.Id);s.AdvanceClock(5);
            Assert.AreEqual(5,s.LifeTasks.Single(x=>x.Id==t.Id).Remaining);using var loaded=Reload(s);At(loaded,GameSession.LifeServicePlace);Person(loaded,"life_teacher",GameSession.LifeServicePlace);loaded.AdvanceClock(5);
            Assert.AreEqual(amount,loaded.LifeDonationTotal);var feedback=loaded.LifeTasks.Single(x=>x.Kind=="book-feedback");int count=loaded.LifeBookStock.Sum();
            Time(loaded,feedback.At-1);loaded.AdvanceClock(1);Assert.Greater(loaded.LifeBookStock.Sum(),count);Assert.AreEqual(charm,loaded.Player.Charm);loaded.AdvanceClock(1);Assert.AreEqual(amount,loaded.LifeDonationTotal);
        }
        [Test] public void DisinfectionCosts300OnlyOnStartAndRequiresThirtyOnsiteMinutes()
        {
            using var s=Make();var cash=s.Ledger.Balance;Resolve(s,"life_flu_season",0);var t=s.LifeTasks.Single(x=>x.Kind=="disinfect");Assert.AreEqual(cash,s.Ledger.Balance);
            Assert.IsTrue(s.StartLifeTask(t.Id));s.AdvanceClock(15);At(s,s.Home.Id);s.AdvanceClock(10);Assert.AreEqual(0,s.FluProtectionUntil);
            using var loaded=Reload(s);At(loaded,t.Place);loaded.AdvanceClock(15);Assert.Greater(loaded.FluProtectionUntil,loaded.Clock.Now.TotalMinutes+6*1440);Assert.AreEqual(cash-Money.FromYuan(300),loaded.Ledger.Balance);
        }
        [Test] public void FluMedicineUsesRealPaymentAndCannotBeTakenTwiceTheSameDay()
        {
            using var s=Make();var data=s.CreateSave();data.lifeFluIll=s.Clock.Now.TotalMinutes+3*1440;using var loaded=GameSession.Load(s.City,data);var cash=loaded.Ledger.Balance;
            Assert.IsTrue(loaded.TakeLifeFluMedicine());Assert.AreEqual(cash-Money.FromYuan(20),loaded.Ledger.Balance);Assert.IsFalse(loaded.TakeLifeFluMedicine());Assert.AreEqual(loaded.Clock.Now.TotalMinutes+1440,loaded.FluIllUntil);
        }
        [Test] public void NoiseNeedsAnActualNightDeliveryAndNamesANeighborRatherThanLandlady()
        {
            using var s=Make();s.RaiseEvent("life_neighbor_noise");Assert.IsNull(s.PendingEvent);Time(s,s.Clock.Today.TotalDays*1440L+23*60);At(s,s.Home.Id);
            var run=new CourierRun{FromId="life_market",FromName="实际采购",DueMinute=s.Clock.Now.TotalMinutes};run.Items.Add(new CourierItem{Kind=CourierItemKind.Furniture,Id="fan_new",Count=1});
            ((List<CourierRun>)typeof(GameSession).GetField("_couriers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).Add(run);Call(s,"DeliverCouriers",s.Clock.Now);
            int landlady=s.Relation("landlady");s.RaiseEvent("life_neighbor_noise");Assert.NotNull(s.PendingEvent);Assert.IsTrue(s.ResolveEvent(2));Assert.AreEqual(landlady,s.Relation("landlady"));
            s.RaiseEvent("life_neighbor_noise");Assert.IsNull(s.PendingEvent);
        }
        [Test] public void QuietAgreementDefersNightDeliveryUntilMorningWithoutLosingTheGoods()
        {
            using var s=Make();s.Flags.Add("life:quiet:"+s.Home.Id);Time(s,s.Clock.Today.TotalDays*1440L+23*60);
            var run=new CourierRun{FromId="life_market",FromName="实际采购",DueMinute=s.Clock.Now.TotalMinutes};run.Items.Add(new CourierItem{Kind=CourierItemKind.Furniture,Id="fan_new",Count=1});
            ((List<CourierRun>)typeof(GameSession).GetField("_couriers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).Add(run);Call(s,"DeliverCouriers",s.Clock.Now);Assert.AreEqual(420,run.DueMinute%1440);
            Assert.IsTrue(s.Couriers.Contains(run));Time(s,run.DueMinute);Call(s,"DeliverCouriers",s.Clock.Now);Assert.IsFalse(s.Couriers.Contains(run));
        }
        [Test] public void DogIsPermanentAfterPaidRegistrationAndFoodIsAnActualSavedConsumable()
        {
            using var s=Make();Resolve(s,"life_pet_adopt",0);Assert.AreEqual("stray",s.LifeDog.Stage);var cash=s.Ledger.Balance;At(s,s.LifeDog.Place);Assert.IsTrue(s.AdoptLifeDog());Assert.IsFalse(s.AdoptLifeDog());Assert.AreEqual(cash-Money.FromYuan(800),s.Ledger.Balance);
            At(s,GameSession.LifeServicePlace);var t=s.LifeTasks.Single(x=>x.Kind=="adopt");Assert.IsTrue(s.StartLifeTask(t.Id));s.AdvanceClock(30);Assert.AreEqual("adopted",s.LifeDog.Stage);Assert.AreEqual(3,s.Backpack.GiftCount(LifeEventGoods.DogFood.Id));
            At(s,s.Home.Id);Assert.IsTrue(s.FeedLifeDog());Assert.IsFalse(s.FeedLifeDog());Assert.AreEqual(2,s.Backpack.GiftCount(LifeEventGoods.DogFood.Id));using var loaded=Reload(s);Assert.AreEqual("adopted",loaded.LifeDog.Stage);Assert.AreEqual(2,loaded.Backpack.GiftCount(LifeEventGoods.DogFood.Id));
            int hunger=loaded.LifeDog.Hunger;Time(loaded,loaded.Clock.Now.TotalMinutes+1440);loaded.AdvanceClock(1);Assert.Less(loaded.LifeDog.Hunger,hunger);
        }
        [Test] public void RescueWaitsForAnActualMeetingAndDoesNotPretendTheDogWasAdopted()
        {
            using var s=Make();Resolve(s,"life_pet_adopt",1);var t=s.LifeTasks.Single(x=>x.Kind=="dog-rescue");Assert.AreEqual("stray",s.LifeDog.Stage);var a=s.CityActivities.Single(x=>x.Id==t.Activity);
            Attend(s,a);s.AdvanceClock(21);Assert.AreEqual(CityActivityPhase.Completed,a.State,a.EndReason);Assert.AreEqual("rescued",s.LifeDog.Stage);
        }
        [Test] public void MusicHasRealAttendanceAndDoesNotTipOrRewardImmediately()
        {
            using var s=Make();Resolve(s,"life_street_music",0);var t=s.LifeTasks.Single(x=>x.Kind=="music");var a=s.CityActivities.Single(x=>x.Id==t.Activity);var cash=s.Ledger.Balance;
            Assert.IsFalse(s.TipLifeMusic(200));Attend(s,a);Assert.IsTrue(s.TipLifeMusic(200));Assert.IsFalse(s.TipLifeMusic(1000));Assert.AreEqual(cash-Money.FromYuan(200),s.Ledger.Balance);s.AdvanceClock(11);Assert.AreEqual(CityActivityPhase.Completed,a.State,a.EndReason);
        }
        [Test] public void PhoneChatBindsAnExistingFriendAndUsesFifteenRealMinutes()
        {
            using var s=Make();s.Meet("zhou",50);Resolve(s,"life_old_friend_text",0);var t=s.LifeTasks.Single(x=>x.Kind=="chat");Assert.AreEqual("zhou",t.Person);int relation=s.Relation("zhou");Assert.IsTrue(s.StartLifeTask(t.Id));s.AdvanceClock(14);Assert.AreEqual(relation,s.Relation("zhou"));s.AdvanceClock(1);Assert.AreEqual(relation+3,s.Relation("zhou"));
        }
        [Test] public void FitnessNeedsThreeActualRoundsAndCannotBeRepeatedWithoutAnotherOffer()
        {
            using var s=Make();Resolve(s,"life_gym_trial",0);Assert.IsFalse(s.LifeGymRound(0));At(s,GameSession.LifeFitnessPlace);s.Player.Health=70;
            for(int i=0;i<3;i++){Assert.IsTrue(s.LifeGymRound(i));var t=s.LifeTasks.Single(x=>x.Kind=="gym");Assert.IsTrue(s.StartLifeTask(t.Id));s.AdvanceClock(10);}
            Assert.AreEqual(3,s.LifeGymRounds);Assert.IsFalse(s.LifeGymReady);Assert.IsFalse(s.LifeGymRound(0));Assert.GreaterOrEqual(s.Player.Health,73);
        }
        [Test] public void LibraryExchangesRealBooksAndReadingTheSameBookCannotFarmIntelligence()
        {
            using var s=Make();Resolve(s,"life_book_swap",0);Assert.IsFalse(s.BorrowLifeBook(0));At(s,GameSession.LifeServicePlace);Person(s,"life_librarian",GameSession.LifeServicePlace);Assert.IsTrue(s.BorrowLifeBook(0));Assert.IsFalse(s.BorrowLifeBook(1));
            string id=LifeEventGoods.Books[0].Id;int iq=s.Player.Intelligence;At(s,s.Home.Id);Assert.IsTrue(s.ReadLifeBook(id));var t=s.LifeTasks.Single(x=>x.Kind=="read");Assert.IsTrue(s.StartLifeTask(t.Id));s.AdvanceClock(30);Assert.AreEqual(iq+1,s.Player.Intelligence);
            using var loaded=Reload(s);Assert.IsTrue(loaded.ReadLifeBook(id));t=loaded.LifeTasks.Single(x=>x.Kind=="read");Assert.IsTrue(loaded.StartLifeTask(t.Id));loaded.AdvanceClock(30);Assert.AreEqual(iq+1,loaded.Player.Intelligence);
            At(loaded,GameSession.LifeServicePlace);Person(loaded,"life_librarian",GameSession.LifeServicePlace);Assert.IsTrue(loaded.BorrowLifeBook(1,id));Assert.AreEqual(0,loaded.Backpack.GiftCount(id));Assert.AreEqual(1,loaded.Backpack.GiftCount(LifeEventGoods.Books[1].Id));
        }
        [Test] public void NightSchoolChargesOnceRequiresFourEveningLessonsAndActualAnswers()
        {
            using var s=Make();Resolve(s,"life_night_class",0);At(s,GameSession.LifeServicePlace);var cash=s.Ledger.Balance;int iq=s.Player.Intelligence;Assert.IsTrue(s.EnrollLifeCourse(0,false));Assert.AreEqual(cash-Money.FromYuan(1000),s.Ledger.Balance);Assert.IsFalse(s.EnrollLifeCourse(1,false));
            for(int lesson=0;lesson<4;lesson++)
            {
                var a=s.CityActivities.Single(x=>x.Id==s.LifeCourse.Activity);Assert.AreEqual(1140,a.ScheduledStart%1440);Attend(s,a);Assert.IsFalse(s.AnswerLifeLesson(1));s.AdvanceClock(30);Assert.IsTrue(s.AnswerLifeLesson(new[]{1,0,2,1}[lesson]));Assert.IsFalse(s.AnswerLifeLesson(1));s.AdvanceClock(31);
                Assert.AreEqual(CityActivityPhase.Completed,a.State,a.EndReason);Assert.AreEqual(lesson+1,s.LifeCourse.Lessons);if(lesson<3)Assert.IsTrue(s.BookLifeLesson());
            }
            Assert.AreEqual(4,s.LifeCourse.Score);Assert.AreEqual(iq+2,s.Player.Intelligence);Assert.IsFalse(s.EnrollLifeCourse(0,false));
        }
        [Test] public void MissingClassAnswerDoesNotGrantALessonAndRebookingDoesNotChargeAgain()
        {
            using var s=Make();Resolve(s,"life_night_class",0);At(s,GameSession.LifeServicePlace);Assert.IsTrue(s.EnrollLifeCourse(1,false));var a=s.CityActivities.Single(x=>x.Id==s.LifeCourse.Activity);Attend(s,a);s.AdvanceClock(61);Assert.AreEqual(0,s.LifeCourse.Lessons);var cash=s.Ledger.Balance;Assert.IsTrue(s.BookLifeLesson());Assert.AreEqual(cash,s.Ledger.Balance);
        }
        [Test] public void ClassAnswerAndEnrollmentSurviveSavingMidLesson()
        {
            using var s=Make();Resolve(s,"life_night_class",0);At(s,GameSession.LifeServicePlace);Assert.IsTrue(s.EnrollLifeCourse(2,false));var a=s.CityActivities.Single(x=>x.Id==s.LifeCourse.Activity);
            Attend(s,a);s.AdvanceClock(30);Assert.IsTrue(s.AnswerLifeLesson(2));using var loaded=Reload(s);loaded.AdvanceClock(31);
            Assert.AreEqual(1,loaded.LifeCourse.Lessons);Assert.AreEqual(1,loaded.LifeCourse.Score);Assert.IsTrue(loaded.BookLifeLesson());
        }
        [Test] public void LowCharmSingingRemainsSelectableButFailsWithoutAFreeCharmReward()
        {
            using var s=Make();s.Player.Charm=10;Resolve(s,"life_street_music",0);var t=s.LifeTasks.Single(x=>x.Kind=="music");Attend(s,s.CityActivities.Single(x=>x.Id==t.Activity));s.AdvanceClock(5);
            Assert.IsTrue(s.SingLifeMusic());Assert.AreEqual(1,s.LifeTasks.Single(x=>x.Id==t.Id).Count);Assert.AreEqual(10,s.Player.Charm);Assert.IsFalse(s.SingLifeMusic());
        }
        [Test] public void WalkingDogNeedsFootTravelAndDistanceAndCannotRewardATaxiRide()
        {
            using var s=Make();var data=s.CreateSave();data.lifeEventDog=new LifeDogState{Stage="adopted",Place=s.Home.Id,LastDay=s.Clock.Today.TotalDays};using var loaded=GameSession.Load(s.City,data);
            At(loaded,loaded.Home.Id);Assert.IsTrue(loaded.WalkLifeDog());var t=loaded.LifeTasks.Single(x=>x.Kind=="dog-walk");Assert.IsTrue(loaded.StartLifeTask(t.Id));At(loaded,null);
            var free=typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic);var p=loaded.PlayerPosition;Assert.IsTrue(loaded.SetVehicle(VehicleKind.Taxi));
            for(int i=0;i<10;i++){free.SetValue(loaded,new PCShopSell.Core.World.MapPoint(p.X+i*10,p.Y));Time(loaded,loaded.Clock.Now.TotalMinutes+1);Call(loaded,"TickLifeBatch",loaded.Clock.Now.TotalMinutes);}
            Assert.AreEqual(10,loaded.LifeTasks.Single(x=>x.Id==t.Id).Remaining);Assert.IsTrue(loaded.SetVehicle(VehicleKind.Walk));
            p=loaded.PlayerPosition;for(int i=1;i<=10;i++){free.SetValue(loaded,new PCShopSell.Core.World.MapPoint(p.X+i*3,p.Y));Time(loaded,loaded.Clock.Now.TotalMinutes+1);Call(loaded,"TickLifeBatch",loaded.Clock.Now.TotalMinutes);}
            Assert.IsFalse(loaded.LifeTasks.Any(x=>x.Id==t.Id));Assert.AreEqual(loaded.Clock.Today.TotalDays,loaded.LifeDog.WalkDay);
        }
        [Test] public void FairReservesRealStockChargesActualRentAndOnlyReturnsUnsoldItemsOnsite()
        {
            using var s=Make();Time(s,47*1440L+660);At(s,GameSession.LifeFairPlace);s.Backpack.AddFurniture("fan_new",3);Resolve(s,"life_street_fair",0);var cash=s.Ledger.Balance;
            Assert.IsTrue(s.StartLifeFair("fan_new",true,3,90));Assert.AreEqual(cash-Money.FromYuan(200),s.Ledger.Balance);Assert.AreEqual(0,s.Backpack.FurnitureCount("fan_new"));Assert.IsFalse(s.StartLifeFair("fan_new",true,1,90));
            using var loaded=Reload(s);At(loaded,loaded.Home.Id);Time(loaded,loaded.LifeStall.Ends);loaded.AdvanceClock(1);Assert.AreEqual(0,loaded.Backpack.FurnitureCount("fan_new"));Assert.IsFalse(loaded.LifeStall.Done);
            At(loaded,GameSession.LifeFairPlace);Assert.IsTrue(loaded.EndLifeFair());Assert.AreEqual(3,loaded.Backpack.FurnitureCount("fan_new"));Assert.IsFalse(loaded.EndLifeFair());
        }
        [Test] public void FairSaleRequiresAnActualArrivedBuyerAndReleasesTheirVisit()
        {
            using var s=Make();Time(s,47*1440L+660);At(s,GameSession.LifeFairPlace);s.Backpack.AddFurniture("fan_new",2);Resolve(s,"life_street_fair",0);Assert.IsTrue(s.StartLifeFair("fan_new",true,2,70));
            var stall=(LifeStallState)typeof(GameSession).GetField("_lifeStall",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
            string buyer=new[]{"zhou","life_teacher","life_ning","life_singer","resident_fuzhi"}.First(id=>(long)Call(s,"CityRetailBudgetFor",id)>=stall.Price);
            var cash=s.Ledger.Balance;stall.Buyer=buyer;Person(s,buyer,s.Home.Id);s.CityPersonState(buyer).VisitId="life-fair:"+stall.Ends;
            Call(s,"TickLifeFair",s.Clock.Now.TotalMinutes);Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(2,stall.Remaining);
            for(int tries=0;tries<10&&stall.Sold==0;tries++)
            {stall.Buyer=buyer;Person(s,buyer,GameSession.LifeFairPlace);s.CityPersonState(buyer).VisitId="life-fair:"+stall.Ends;Call(s,"TickLifeFair",s.Clock.Now.TotalMinutes);}
            Assert.AreEqual(1,stall.Sold);Assert.AreEqual(1,stall.Remaining);Assert.AreEqual(cash+Money.FromCents(stall.Price),s.Ledger.Balance);Assert.IsNull(s.CityPersonState(buyer).VisitId);
            Call(s,"TickLifeFair",s.Clock.Now.TotalMinutes);Assert.AreEqual(cash+Money.FromCents(stall.Price),s.Ledger.Balance);
        }
    }
}
