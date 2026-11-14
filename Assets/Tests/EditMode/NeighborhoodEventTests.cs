using System;
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
    public sealed class NeighborhoodEventTests
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
        private static void Resolve(GameSession s,string id,int choice=0){s.RaiseEvent(id);Assert.NotNull(s.PendingEvent,id);Assert.IsTrue(s.ResolveEvent(choice));}
        private static LifeEventTask Task(GameSession s,string kind)=>s.LifeTasks.Single(t=>t.Kind==kind);
        private static void Start(GameSession s,string kind){Assert.IsTrue(s.StartLifeTask(Task(s,kind).Id),kind);}
        private static void Rain(GameSession s)
        {for(int d=42;d<65;d++)for(int h=0;h<24;h+=3){long at=d*1440L+h*60;if(s.IsRainingAt(at)){Time(s,at);return;}}Assert.Fail("No deterministic rain");}
        private static void Child(GameSession s)
        {Person(s,"life_ning","life_restaurant");Resolve(s,"life_lost_child");}
        private static void Festival(GameSession s)
        {Time(s,s.Clock.Today.TotalDays*1440L+1080);At(s,GameSession.LifeServicePlace);Resolve(s,"life_festival_lantern");}
        [Test] public void FinalFourUseCodeAndPreservePoolAndRetiredNumbers()
        {Assert.AreEqual(120,EventCatalog.All.Count);foreach(var id in new[]{"life_blood_donation","life_lost_child","life_roof_leak","life_festival_lantern"})Assert.IsTrue(EventCatalog.CodeEvents.Any(e=>e.Id==id));}
        [Test] public void BloodNeedsCheckDonationAndObservationAndRewardsOneRealPack()
        {
            using var s=Make();Resolve(s,"life_blood_donation");Assert.AreEqual(0,s.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));Assert.IsFalse(s.StartLifeTask(Task(s,"blood-check").Id));
            At(s,GameSession.LifeServicePlace);Start(s,"blood-check");s.AdvanceClock(10);Assert.AreEqual("donating",s.BloodDonation.Stage);s.AdvanceClock(15);Assert.AreEqual("observing",s.BloodDonation.Stage);
            Assert.AreEqual(0,s.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));s.AdvanceClock(15);Assert.AreEqual("done",s.BloodDonation.Stage);Assert.AreEqual(1,s.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));
            Assert.IsFalse(s.BeginBloodCheck());s.Player.Hunger=20;Assert.IsTrue(s.EatNeighborhoodNutrition());Assert.AreEqual(40,s.Player.Hunger);Assert.IsFalse(s.EatNeighborhoodNutrition());
        }
        [Test] public void LowHealthCanChooseCheckButFailsWithoutDonationPenaltyAndCanRetryNextDay()
        {
            using var s=Make();At(s,GameSession.LifeServicePlace);Resolve(s,"life_blood_donation");s.Player.Health=60;Start(s,"blood-check");s.AdvanceClock(10);
            Assert.AreEqual("retry",s.BloodDonation.Stage);Assert.GreaterOrEqual(s.Player.Health,60);Assert.AreEqual(0,s.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));Assert.IsFalse(s.BeginBloodCheck());
            Time(s,s.Clock.Today.AddDays(1).TotalDays*1440L+660);Assert.IsTrue(s.BeginBloodCheck());Start(s,"blood-check");s.AdvanceClock(10);Assert.AreEqual("donating",s.BloodDonation.Stage);
        }
        [Test] public void BloodPausesAwayAndSavesMidObservationWithoutDuplicatingPack()
        {
            using var s=Make();At(s,GameSession.LifeServicePlace);Resolve(s,"life_blood_donation");Start(s,"blood-check");s.AdvanceClock(28);int remaining=Task(s,"blood-observe").Remaining;
            At(s,s.Home.Id);s.AdvanceClock(5);Assert.AreEqual(remaining,Task(s,"blood-observe").Remaining);using var loaded=Reload(s);At(loaded,GameSession.LifeServicePlace);loaded.AdvanceClock(remaining);
            Assert.AreEqual(1,loaded.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));using var again=Reload(loaded);again.AdvanceClock(2);Assert.AreEqual(1,again.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id));
        }
        [Test] public void BloodCannotStartAtNightOrWhileIll()
        {
            using var s=Make();Resolve(s,"life_blood_donation");At(s,GameSession.LifeServicePlace);Time(s,s.Clock.Today.TotalDays*1440L+1320);Assert.IsFalse(s.StartLifeTask(Task(s,"blood-check").Id));
            var data=s.CreateSave();data.lifeFluIll=s.Clock.Now.TotalMinutes+3*1440;using var loaded=GameSession.Load(s.City,data);Time(loaded,loaded.Clock.Today.AddDays(1).TotalDays*1440L+660);At(loaded,GameSession.LifeServicePlace);Start(loaded,"blood-check");loaded.AdvanceClock(10);Assert.AreEqual("retry",loaded.BloodDonation.Stage);
        }
        [Test] public void LostChildRequiresCluePhysicalParentAndFiveMinuteReunion()
        {
            using var s=Make();Child(s);Assert.IsFalse(s.AskLostChild());At(s,s.LostChild.Origin);Assert.IsTrue(s.AskLostChild());Assert.IsFalse(s.ReuniteLostChild());Assert.IsTrue(s.EscortLostChild());
            At(s,s.LostChild.CluePlace);int relation=s.Relation(s.LostChild.Parent);Assert.IsTrue(s.ReuniteLostChild());Start(s,"child-reunion");s.AdvanceClock(5);
            Assert.AreEqual("done",s.LostChild.Stage);Assert.AreEqual(relation+3,s.Relation(s.LostChild.Parent));Assert.IsFalse(s.ReuniteLostChild());Assert.IsNull(s.CityPersonState(s.LostChild.Parent).VisitId);
        }
        [Test] public void ChildCanBeEscortedToServiceButHelperMustReallyTravelAndHandoff()
        {
            using var s=Make();Child(s);At(s,s.LostChild.Origin);Assert.IsTrue(s.EscortLostChild());Assert.IsFalse(s.ContactChildHelper());At(s,GameSession.LifeServicePlace);Assert.IsTrue(s.ContactChildHelper());
            Assert.IsFalse(s.StartLifeTask(Task(s,"child-handoff").Id));Assert.NotNull(s.LostChild.Helper.Route);long arrival=s.LostChild.Helper.Route.ArriveMinute;
            s.AdvanceClock((int)(arrival-s.Clock.Now.TotalMinutes));Start(s,"child-handoff");s.AdvanceClock(14);Assert.AreEqual("handoff",s.LostChild.Stage);s.AdvanceClock(1);Assert.AreEqual("done",s.LostChild.Stage);
        }
        [Test] public void ChildAndHelperRouteSurviveJsonAndCannotBeRequestedTwice()
        {
            using var s=Make();Child(s);At(s,s.LostChild.Origin);Assert.IsTrue(s.ContactChildHelper());var route=s.LostChild.Helper.Route;using var loaded=Reload(s);
            Assert.AreEqual(s.LostChild.Parent,loaded.LostChild.Parent);Assert.AreEqual("community-child",loaded.CityPersonState(loaded.LostChild.Parent).VisitId);Assert.IsFalse(loaded.ContactChildHelper());
            Assert.AreEqual(route.ArriveMinute,loaded.LostChild.Helper.Route.ArriveMinute);At(loaded,loaded.LostChild.Place);loaded.AdvanceClock((int)(route.ArriveMinute-loaded.Clock.Now.TotalMinutes));Start(loaded,"child-handoff");loaded.AdvanceClock(15);Assert.AreEqual("done",loaded.LostChild.Stage);
        }
        [Test] public void RainIsDeterministicAndLeakRequiresRainAndActualRentedShop()
        {
            using var s=Make();long sunny=Enumerable.Range(0,500).Select(i=>42*1440L+i*60).First(x=>!s.IsRainingAt(x));Time(s,sunny);s.RaiseEvent("life_roof_leak");Assert.IsNull(s.PendingEvent);Rain(s);Resolve(s,"life_roof_leak",2);
            Assert.AreEqual(s.MainShop.Place.Id,s.RoofLeak.Shop);using var loaded=Reload(s);Assert.IsTrue(loaded.CityIsRaining);Assert.AreEqual(s.WeatherSummary,loaded.WeatherSummary);
        }
        [Test] public void BucketIsOnsiteWorkStopsWaterButDoesNotPermanentlyFixRoof()
        {
            using var s=Make();Rain(s);Resolve(s,"life_roof_leak",2);s.AdvanceClock(30);Assert.Less(s.NeighborhoodShopTraffic(s.MainShop.Place.Id),1);
            At(s,s.Home.Id);Assert.IsFalse(s.PlaceLeakBucket());At(s,s.MainShop.Place.Id);Assert.IsTrue(s.PlaceLeakBucket());Start(s,"leak-bucket");s.AdvanceClock(5);
            Assert.IsTrue(s.RoofLeak.Bucket);Assert.IsFalse(s.RoofLeak.Fixed);Assert.AreEqual(1,s.NeighborhoodShopTraffic(s.MainShop.Place.Id));s.AdvanceClock(20);Assert.AreEqual(0,s.RoofLeak.Water);
        }
        [Test] public void RepairCharges2000OnceTravelsAndWorksSixtyMinutesEvenWhenPlayerLeaves()
        {
            using var s=Make();Rain(s);var cash=s.Ledger.Balance;Resolve(s,"life_roof_leak");Assert.AreEqual(cash-Money.FromYuan(2000),s.Ledger.Balance);Assert.IsFalse(s.RequestLeakRepair());
            long arrival=s.RoofLeak.Worker.Route.ArriveMinute;At(s,s.Home.Id);s.AdvanceClock((int)(arrival-s.Clock.Now.TotalMinutes));Assert.IsFalse(s.RoofLeak.Fixed);Assert.GreaterOrEqual(Task(s,"leak-repair").Remaining,59);
            using var loaded=Reload(s);loaded.AdvanceClock(60);Assert.IsTrue(loaded.RoofLeak.Fixed);Assert.AreEqual(0,loaded.RoofLeak.Water);Assert.IsFalse(loaded.RequestLeakRepair());
        }
        [Test] public void ChangingLeaseDoesNotTransferRepairToTheReplacementBusiness()
        {
            using var s=Make();Rain(s);Resolve(s,"life_roof_leak");s.MainShop.RetailKind="restaurant";s.AdvanceClock(1);Assert.IsTrue(s.RoofLeak.Fixed);Assert.AreEqual(1,s.NeighborhoodShopTraffic(s.MainShop.Place.Id));Assert.IsFalse(s.LifeTasks.Any(t=>t.Kind=="leak-repair"));
        }
        [TestCase(0,0,300)] [TestCase(1,2,1000)]
        public void LanternCraftCostsActualTierProducesItemAndHangingConsumesOne(int tier,int style,int price)
        {
            using var s=Make();Festival(s);var cash=s.Ledger.Balance;Assert.IsTrue(s.BeginLanternCraft(tier,style));Assert.AreEqual(cash,s.Ledger.Balance);Start(s,"lantern-craft");Assert.AreEqual(cash-Money.FromYuan(price),s.Ledger.Balance);
            s.AdvanceClock(10);string id=NeighborhoodGoods.Lanterns[tier*3+style].Id;Assert.AreEqual(1,s.Backpack.GiftCount(id));Assert.IsTrue(s.HangFestivalLantern(id,1));Assert.AreEqual(0,s.Backpack.GiftCount(id));Assert.IsFalse(s.HangFestivalLantern(id,1));
            using var loaded=Reload(s);Assert.AreEqual(tier*3+style,loaded.LanternFestival.Hung[1]);Assert.AreEqual(0,loaded.Backpack.GiftCount(id));
        }
        [Test] public void RiddleRequiresFiveMinutesHasRealAnswersAndCannotRepeat()
        {
            using var s=Make();Festival(s);Assert.IsFalse(s.AnswerFestivalRiddle(0));Assert.IsTrue(s.BeginFestivalRiddle(0));Start(s,"lantern-riddle");s.AdvanceClock(4);Assert.IsFalse(s.AnswerFestivalRiddle(0));s.AdvanceClock(1);
            using var loaded=Reload(s);Assert.AreEqual(0,loaded.LanternFestival.PendingRiddle);Assert.IsTrue(loaded.AnswerFestivalRiddle(0));Assert.IsFalse(loaded.AnswerFestivalRiddle(0));Assert.IsFalse(loaded.BeginFestivalRiddle(0));Assert.AreEqual(1,loaded.LanternFestival.RiddleMask);
        }
        [Test] public void FestivalLastsThreeNightsAndUnhungPaidLanternRemainsInBackpack()
        {
            using var s=Make();Festival(s);Assert.IsTrue(s.BeginLanternCraft(0,1));Start(s,"lantern-craft");s.AdvanceClock(10);string id=NeighborhoodGoods.Lanterns[1].Id;
            Time(s,s.Clock.Today.AddDays(3).TotalDays*1440L+1080);s.AdvanceClock(1);Assert.IsFalse(s.FestivalOpen);Assert.IsFalse(s.HangFestivalLantern(id,0));Assert.AreEqual(1,s.Backpack.GiftCount(id));Assert.IsFalse(s.BeginFestivalRiddle(1));
        }
        [Test] public void FestivalVisitNeedsActualParticipationToRewardTheSpecificFriend()
        {
            using var s=Make();Festival(s);s.Meet("life_ming",30);int other=s.Relation("landlady");Assert.IsTrue(s.InviteFestivalFriend("life_ming"));var task=Task(s,"festival-visit");var a=s.CityActivities.Single(x=>x.Id==task.Activity);
            Time(s,a.ScheduledStart);At(s,a.VenueId);s.PrepareVenueCompanions(s.CurrentPlace);s.AdvanceClock(3);Assert.AreEqual(CityActivityPhase.Running,a.State);
            Assert.IsTrue(s.BrowseFestivalStall());Start(s,"lantern-browse");s.AdvanceClock(10);Assert.IsFalse(s.BrowseFestivalStall());s.AdvanceClock(50);Assert.AreEqual(CityActivityPhase.Completed,a.State,a.EndReason);
            Assert.AreEqual(other,s.Relation("landlady"));Assert.IsFalse(s.InviteFestivalFriend("life_ming"));
        }
        [Test] public void EmptyJsonObjectsDoNotInventWorkersChildrenOrFestivals()
        {
            using var s=Make();using var loaded=Reload(s);Assert.IsNull(loaded.BloodDonation);Assert.IsNull(loaded.LostChild);Assert.IsNull(loaded.RoofLeak);Assert.IsNull(loaded.LanternFestival);
        }
        [Test] public void FestivalExtendsForecourtAccessTo22ButDoesNotExtendItAfterThreeDays()
        {
            using var s=Make();Festival(s);Time(s,s.Clock.Today.TotalDays*1440L+1290);Assert.IsTrue(s.FestivalOpen);Assert.IsTrue(s.IsPlaceOpen(s.City.GetPlace(GameSession.LifeServicePlace)));
            Assert.IsTrue(s.BeginFestivalRiddle(0));Start(s,"lantern-riddle");s.AdvanceClock(5);Assert.IsTrue(s.AnswerFestivalRiddle(0));
            Time(s,s.Clock.Today.AddDays(3).TotalDays*1440L+1290);Assert.IsFalse(s.FestivalOpen);Assert.IsFalse(s.IsPlaceOpen(s.City.GetPlace(GameSession.LifeServicePlace)));
        }
        [Test] public void PaidUnfinishedLanternCanBeCompletedAfterFestivalWithoutChargingAgain()
        {
            using var s=Make();Festival(s);Assert.IsTrue(s.BeginLanternCraft(1,0));Start(s,"lantern-craft");s.AdvanceClock(4);var cash=s.Ledger.Balance;At(s,s.Home.Id);
            Time(s,s.Clock.Today.AddDays(3).TotalDays*1440L+660);using var loaded=Reload(s);At(loaded,GameSession.LifeServicePlace);loaded.AdvanceClock(6);
            Assert.AreEqual(1,loaded.Backpack.GiftCount(NeighborhoodGoods.Lanterns[3].Id));Assert.AreEqual(cash,loaded.Ledger.Balance);
        }
        [Test] public void MerelyStandingWithFriendDoesNotCompleteFestivalVisit()
        {
            using var s=Make();Festival(s);s.Meet("life_ming",30);Assert.IsTrue(s.InviteFestivalFriend("life_ming"));var a=s.CityActivities.Single(x=>x.Id==Task(s,"festival-visit").Activity);
            Time(s,a.ScheduledStart);At(s,a.VenueId);s.PrepareVenueCompanions(s.CurrentPlace);s.AdvanceClock(65);Assert.AreEqual(CityActivityPhase.Interrupted,a.State);Assert.IsFalse(a.RewardApplied);
        }
    }
}
