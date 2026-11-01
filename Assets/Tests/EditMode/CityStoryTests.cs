using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityStoryTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(0),9*60));
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;return s;
        }
        private static void PutPersonHere(GameSession s,string id)
        {
            var p=s.CityPersonState(id);Assert.NotNull(p,id);
            p.Route=null;p.LocationPlaceId=s.CurrentPlace.Id;p.X=s.PlayerPosition.X;p.Y=s.PlayerPosition.Y;
        }
        [Test]
        public void AcceptingStoryDinnerSchedulesWithoutConsumingTimeMoneyOrRelationship()
        {
            using(var s=Make())
            {
                s.Meet(Npcs.Li,12);PutPersonHere(s,Npcs.Li);s.CityTalk(Npcs.Li,"work");
                Assert.AreEqual(1,s.CityStory("supply").Stage);
                var money=s.Ledger.Balance;long now=s.Clock.Now.TotalMinutes;int relation=s.Relation("life_enterprise");
                string failure=null;s.Failed+=reason=>failure=reason;
                var activity=s.ScheduleCityStoryActivity("supply");Assert.NotNull(activity,failure);
                Assert.AreEqual(CityActivityPhase.Scheduled,activity.State);Assert.False(activity.EntryPaid);
                Assert.AreEqual(now,s.Clock.Now.TotalMinutes);Assert.AreEqual(money,s.Ledger.Balance);
                Assert.AreEqual(relation,s.Relation("life_enterprise"));Assert.AreEqual(1,s.CityStory("supply").Stage);
                Assert.False(s.StartCityCreatorPromotion(s.MainShop.Place.Id),"business story cannot skip actual introduction/expo");
            }
        }
        [Test]
        public void CompletionNotificationOnlyProgressesCompletedActivityAndIsIdempotent()
        {
            using(var s=Make())
            {
                var p=s.CityStory("supply");p.Stage=1;p.PersonId=Npcs.Li;p.ActivityId="proof_dinner";
                var a=new CityActivity {Id=p.ActivityId,DefinitionId="dinner",State=CityActivityPhase.Scheduled,Participants=Array.Empty<CityActivityParticipant>()};
                var method=typeof(GameSession).GetMethod("OnCityActivityCompleted",BindingFlags.NonPublic|BindingFlags.Instance);
                Assert.NotNull(method);method.Invoke(s,new object[]{a});Assert.AreEqual(1,p.Stage);
                a.State=CityActivityPhase.Completed;a.RewardApplied=true;
                method.Invoke(s,new object[]{a});Assert.AreEqual(2,p.Stage);
                int relation=s.Relation("life_enterprise");var cash=s.Ledger.Balance;
                method.Invoke(s,new object[]{a});Assert.AreEqual(2,p.Stage);Assert.AreEqual(relation,s.Relation("life_enterprise"));Assert.AreEqual(cash,s.Ledger.Balance);
                var saved=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,saved))
                {Assert.AreEqual(2,loaded.CityStory("supply").Stage);Assert.AreEqual(cash,loaded.Ledger.Balance);}
            }
        }
        [Test]
        public void LegacyCreatorPrivateInvitationNoLongerChargesOrFastForwards()
        {
            using(var s=Make())
            {
                var creator=s.Creator("momo");creator.Relationship=25;
                var money=s.Ledger.Balance;long time=s.Clock.Now.TotalMinutes;int affection=creator.Affection;
                Assert.True(s.InviteCreatorPrivately("momo"));
                Assert.AreEqual(money,s.Ledger.Balance);Assert.AreEqual(time,s.Clock.Now.TotalMinutes);Assert.AreEqual(affection,creator.Affection);
                Assert.True(s.CityActivities.Any(a=>a.Active&&a.Participants.Any(p=>p.PersonId=="momo")));
            }
        }
        [Test]
        public void ChoosingFriendshipDoesNotRequireOrConsumePromotionMoney()
        {
            using(var s=Make())
            {
                var story=s.CityStory("romance");story.Stage=3;PutPersonHere(s,"life_creator");
                var cash=s.Ledger.Balance;Assert.True(s.ChooseCityRomance(false));
                Assert.AreEqual("friends",story.Branch);Assert.AreEqual(4,story.Stage);Assert.IsNull(s.RomanceId);Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.False(s.ChooseCityRomance(true),"same decision is not replayable for additional effects");
            }
        }
        [Test]
        public void LongNpcTripUsesSameOrdinaryTransportForEstimateAndActualSavedRoute()
        {
            using(var s=Make())
            {
                var person=s.CityPersonState(Npcs.Li);var market=s.City.GetPlace("stall_cpu");
                var at=s.City.ViewPosition(market.NodeId);person.X=at.X;person.Y=at.Y;person.LocationPlaceId=market.Id;person.Route=null;
                var plan=typeof(GameSession).GetMethod("PlanCityRoute",BindingFlags.NonPublic|BindingFlags.Instance);
                var route=(CityRoute)plan.Invoke(s,new object[]{person,"life_restaurant",s.Clock.Now.TotalMinutes,long.MaxValue,null});
                Assert.NotNull(route);Assert.AreEqual(VehicleKind.Taxi,route.Transport);
                Assert.Less(route.ArriveMinute-route.DepartMinute,90);
                Assert.Greater(route.ArriveMinute,route.DepartMinute,"ordinary transport follows the route rather than teleporting");
                var saved=JsonUtility.FromJson<CityRoute>(JsonUtility.ToJson(route));
                Assert.AreEqual(route.Transport,saved.Transport);Assert.AreEqual(route.ArriveMinute,saved.ArriveMinute);
                Assert.AreEqual(route.Position(route.DepartMinute+1).X,saved.Position(route.DepartMinute+1).X);
            }
        }
        [Test]
        public void ReservationRetainsTravelAndLeaveApprovalAcrossSave()
        {
            using(var s=Make())
            {
                long start=s.SuggestCityActivityStart("dinner");
                var a=s.ScheduleCityActivity("dinner",Array.Empty<string>(),start,true);
                Assert.NotNull(a);Assert.Less(a.OccupiedFrom,a.ScheduledStart);
                Assert.Greater(a.OccupiedUntil,a.ScheduledStart+a.Duration);
                Assert.NotNull(s.CityActivityBlocker("dinner",Array.Empty<string>(),a.ScheduledStart+a.Duration+1));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var restored=loaded.CityActivities.Single(x=>x.Id==a.Id);
                    Assert.True(restored.EmployeeLeaveApproved);
                    Assert.AreEqual(a.OccupiedFrom,restored.OccupiedFrom);
                    Assert.AreEqual(a.OccupiedUntil,restored.OccupiedUntil);
                }
            }
        }
        [Test]
        public void CancellingActivityClearsOnlyItsMatchingPersonRoute()
        {
            using(var s=Make())
            {
                var a=s.ScheduleCityActivity("dinner",Array.Empty<string>(),s.SuggestCityActivityStart("dinner"));
                Assert.NotNull(a);var person=s.CityPersonState("life_creator");
                a.Participants=new[]{new CityActivityParticipant{PersonId=person.PersonId,IntentId="this-appointment",Phase=CityParticipantPhase.Travelling}};
                person.ActivityId=a.Id;
                person.Route=new CityRoute{IntentId="this-appointment",TargetPlaceId=a.VenueId,
                    DepartMinute=s.Clock.Now.TotalMinutes,ArriveMinute=s.Clock.Now.TotalMinutes+20,
                    X=new[]{person.X,person.X+2},Y=new[]{person.Y,person.Y+2},Meters=new[]{2.8}};
                Assert.True(s.CancelCityActivity(a.Id));Assert.IsNull(person.Route);Assert.IsNull(person.ActivityId);
                Assert.False(a.EntryPaid);Assert.False(a.RewardApplied);
            }
        }
        [Test]
        public void PromotionFeeNeverCreatesRevenueOrPersonalRomanceProgress()
        {
            using(var s=Make())
            {
                var story=s.CityStory("creator");story.Stage=4;s.Meet("life_creator",20);
                // Arrange prior real-activity trust; this test verifies the paid campaign transaction boundary.
                var trust=(System.Collections.Generic.Dictionary<string,int>)typeof(GameSession).GetField("_cityTrust",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
                trust["life_creator"]=1;
                var money=s.Ledger.Balance;int romance=s.CityStory("romance").Stage;
                Assert.True(s.StartCityCreatorPromotion(s.MainShop.Place.Id));Assert.AreEqual(money-Money.FromYuan(300),s.Ledger.Balance);
                Assert.AreEqual(romance,s.CityStory("romance").Stage);Assert.AreEqual(0,s.CityPromotions.Single().SalesCents);
                Assert.False(s.StartCityCreatorPromotion(s.MainShop.Place.Id));Assert.AreEqual(1,s.CityPromotions.Count);
            }
        }
    }
}
