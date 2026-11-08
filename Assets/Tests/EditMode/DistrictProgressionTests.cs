using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class DistrictProgressionTests
    {
        internal static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7,Money.FromYuan(5000),GameTimestamp.From(new GameDate(40),600),true,false,false);
            s.EventsEnabled=false;return s;
        }
        private static GameSession Reload(GameSession s)
        {
            var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            loaded.EventsEnabled=false;return loaded;
        }
        private static void Set(GameSession s,string name,object value)=>typeof(GameSession).GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(s,value);
        private static void At(GameSession s,Place place)
        {
            s.StopTrip();Set(s,"PlayerNodeId",place.NodeId);Set(s,"CurrentPlace",place);
        }
        private static void Street(GameSession s,MapPoint point,string node)
        {
            s.StopTrip();Set(s,"CurrentPlace",null);Set(s,"PlayerNodeId",node);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)point);
        }
        private static void Advance(GameSession s,long until)
        {
            for(int guard=0;s.Clock.Now.TotalMinutes<until&&guard<10000;guard++)
            {s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;s.AdvanceClock((int)Math.Min(60,until-s.Clock.Now.TotalMinutes));}
            Assert.GreaterOrEqual(s.Clock.Now.TotalMinutes,until);
        }
        internal static void CompleteLeadOuting(GameSession s,int index)
        {
            string id=GameSession.CommunityLeadPerson(index),def=GameSession.CommunityLeadActivity(index);
            s.Meet(id,35);s.AddRelation(id,Math.Max(0,35-s.Relation(id)));
            StandWithLead(s,index);
            long start=s.SuggestCityActivityStart(def,id);
            var activity=s.ScheduleCityActivity(def,new[]{id},start);
            Assert.NotNull(activity,def+": "+s.CityActivityBlocker(def,new[]{id},start));
            // Fixture travel places the player at the venue; scheduling, service selection,
            // elapsed time, participants and settlement use the production activity system.
            var venue=s.City.GetPlace(GameSession.CommunityLeadVenue(index));
            Street(s,s.City.ViewPosition(venue.NodeId),venue.NodeId);
            while(s.Clock.Now.TotalMinutes<start)
            {
                s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                Advance(s,Math.Min(start,s.Clock.Now.TotalMinutes+30));
            }
            At(s,venue);
            Assert.True(s.SelectVenueService(def,0),s.VenueServiceBlocker(def,0));
            Assert.AreEqual(CityActivityPhase.Running,activity.State,def+" start="+start+" now="+s.Clock.Now.TotalMinutes+" hours="+venue.OpenMinute+"-"+venue.CloseMinute+" place="+s.CurrentPlace?.Id+" "+activity.EndReason);
            long end=s.Clock.Now.TotalMinutes+activity.Duration+1;
            while(s.Clock.Now.TotalMinutes<end)
            {
                s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                Advance(s,Math.Min(end,s.Clock.Now.TotalMinutes+15));
            }
            Assert.AreEqual(CityActivityPhase.Completed,activity.State,def+" start="+start+" now="+s.Clock.Now.TotalMinutes+" hours="+venue.OpenMinute+"-"+venue.CloseMinute+" place="+s.CurrentPlace?.Id+" "+activity.EndReason);
            Assert.True(s.CommunityHasOuting(index));
        }
        internal static void StandWithLead(GameSession s,int index)
        {
            // Fixture positions a face-to-face encounter; conversation and booking use production APIs.
            StandWithPerson(s,GameSession.CommunityLeadPerson(index));
        }
        internal static void StandWithPerson(GameSession s,string id)
        {
            var p=s.CityPersonState(id);
            p.Route=null;p.LocationPlaceId=null;p.Task=CityPersonTask.Waiting;
            var point=new MapPoint(p.X,p.Y);
            Street(s,point,s.City.NearestNode(p.X,p.Y));
            Assert.True(s.CitySamePlace(p.PersonId));
        }
        internal static string AskLeadLocally(GameSession s,int index)
        {
            StandWithLead(s,index);return s.AskCommunityLead(index);
        }
        internal static void CompleteCommunitySearch(GameSession s)
        {
            Assert.AreEqual("community.clue",s.CurrentDistrictNode.Id);Finish(s);
            Assert.AreEqual("community.search",s.CurrentDistrictNode.Id);
            foreach(int i in new[]{2,0,1})
            {
                CompleteLeadOuting(s,i);AskLeadLocally(s,i);Assert.True(s.CommunityHasClue(i));
                Street(s,s.CommunityBoxPosition(i),s.City.GetPlace(GameSession.CommunityBoxPlace(i)).NodeId);
                Assert.True(s.SearchCommunityBox(i));
            }
            Assert.AreEqual("community.trial",s.CurrentDistrictNode.Id);
        }
        internal static void AwaitInvitation(GameSession s)
        {
            if(s.CurrentDistrictNode!=null)return;
            Assert.Greater(s.DistrictStoryState.PendingChapter,0);
            Advance(s,s.DistrictStoryState.InvitationDueAt);
            Assert.NotNull(s.CurrentDistrictNode);
        }
        private static void Meet(GameSession s,string choice=null)
        {
            var node=s.CurrentDistrictNode;
            Assert.NotNull(node);
            if(s.DistrictStoryState.MeetingNodeId!=node.Id)
                Assert.True(s.BeginDistrictMeeting(node.Id,choice??node.Choices.FirstOrDefault()?.Id),node.Id);
            var state=s.DistrictStoryState;
            At(s,s.City.GetPlace(state.MeetingPlaceId));
            Advance(s,state.MeetingAt+1);
            if (s.DistrictMeetingStreetNode is string meetingNode)
            {
                Assert.NotNull(s.DistrictMeetingBlocker, "Entering the landmark must not count as the outdoor meeting");
                Street(s, s.City.ViewPosition(meetingNode), meetingNode);
                Assert.IsNull(s.NearbyPlace, "The meeting is outside every door interaction radius");
            }
            else At(s,s.City.GetPlace(state.MeetingPlaceId));
            Assert.IsNull(s.DistrictMeetingBlocker,s.CurrentDistrictNode.Id);
            foreach(string id in s.DistrictStoryState.MeetingPeople)Assert.False(s.CityPersonInside(s.CityPersonState(id)),"Meeting participants wait visibly at the entrance");
        }
        private static void Finish(GameSession s)
        {
            string id=s.CurrentDistrictNode.Id;Meet(s);
            if(s.CurrentDistrictNode?.Id==id)Assert.True(s.ActDistrictNode(id),id);
        }






        [Test] public void SavedPagesBringTangFromLockedCommunityToAccessibleHandover()
        {
            using(var s=Make())
            {
                var state=new DistrictProgress{Chapter=1,NodeId="community.search",CommunityPieces=7,CommunitySearchVersion=2};
                typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,state);
                var locked=s.City.GetPlace("agency_s_services");
                Assert.NotNull(s.DistrictAccessBlocker(locked));
                var tang=s.CityPersonState(CityLifeCatalog.TangWei);var point=s.StandOf(locked);
                tang.LocationPlaceId=locked.Id;tang.Route=null;tang.X=point.X;tang.Y=point.Y;
                s.RefreshDistrictNarrative();
                Assert.AreEqual("community.trial",s.CurrentDistrictNode.Id);
                Assert.AreEqual("agency",tang.Route?.TargetPlaceId);
                Assert.AreEqual(7,s.DistrictStoryState.CommunityPieces);
                Assert.False(s.ActDistrictNode("community.trial"));
                Meet(s);
                Assert.True(s.ActDistrictNode("community.trial"));
                Assert.Zero(s.DistrictStoryState.CommunityPieces);
                Assert.AreEqual(1,s.DistrictAccessTier);
            }
        }

        [TestCase(15)] [TestCase(1500)]
        public void EarlyArrivalKeepsExactMeetingDateTimeAndRemainingWait(int minutes)
        {
            using(var s=Make())
            {
                long due=s.Clock.Now.TotalMinutes+minutes;
                var state=new DistrictProgress{Chapter=1,NodeId="community.meeting",MeetingNodeId="community.meeting",
                    MeetingPlaceId="agency",MeetingAt=due};
                typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,state);
                var node=s.DistrictMeetingStreetNode;Street(s,s.City.ViewPosition(node),node);
                Assert.True(s.AtCurrentDistrictMeeting);
                string date=new GameDate((int)(due/1440)).ToString();
                string time=$"{due%1440/60}:{due%60:00}";
                StringAssert.Contains(date,s.DistrictMeetingDescription);
                StringAssert.Contains(time,s.DistrictMeetingDescription);
                StringAssert.Contains("还需等待",s.DistrictMeetingDescription);
                StringAssert.Contains(time,s.DistrictMeetingBlocker);
                using(var loaded=Reload(s))StringAssert.Contains(time,loaded.DistrictMeetingDescription);
                s.Clock.Restore(new GameDate((int)(due/1440)),(int)(due%1440));
                Assert.IsNull(s.DistrictMeetingBlocker);
                StringAssert.Contains(time,s.DistrictMeetingDescription);
                StringAssert.DoesNotContain("还需等待",s.DistrictMeetingDescription);
            }
        }

        [Test] public void LowAffinityQuestContactAtHomeHasPublicWorkplaceWaitingRoute()
        {
            using(var s=Make())
            {
                var person=s.CityPersonState("keeper_agent");
                person.Route=null;person.LocationPlaceId=person.HomePlaceId;person.Task=CityPersonTask.Home;
                Assert.Less(s.Relation(person.PersonId),GameSession.HomeVisitMinRelation);
                Assert.AreEqual(person.WorkPlaceId,s.DistrictPersonWaitingPlace(person.PersonId)?.Id);
                person.LocationPlaceId=person.WorkPlaceId;person.Task=CityPersonTask.Working;
                Assert.IsNull(s.DistrictPersonWaitingPlace(person.PersonId));
            }
        }

        [Test] public void OldMarketOutingCannotBeBookedOnSupplierWeekend()
        {
            using(var s=Make())
            {
                s.Meet("zhou",65);StandWithLead(s,2);
                long weekend=40*1440L+1080;
                StringAssert.Contains("周末",s.CityActivityBlocker("old_market",new[]{"zhou"},weekend));
                long suggested=s.SuggestCityActivityStart("old_market","zhou");
                Assert.False(new GameDate((int)(suggested/1440)).IsWeekend);
                Assert.GreaterOrEqual(suggested%1440,GameSession.WholesalerOpenMinute);
                Assert.LessOrEqual(suggested%1440+60,GameSession.WholesalerCloseMinute);
                Assert.IsNull(s.CityActivityBlocker("old_market",new[]{"zhou"},suggested));
            }
        }

        [Test] public void LockedDistrictLeadDirectsPlayerToAccessibleHomeInsteadOfCheckpoint()
        {
            using(var s=Make())
            {
                var person=s.CityPersonState("lin");person.Route=null;person.LocationPlaceId=person.WorkPlaceId;
                Assert.NotNull(s.DistrictAccessBlocker(s.City.GetPlace(person.WorkPlaceId)));
                var waiting=s.DistrictPersonWaitingPlace("lin");
                Assert.NotNull(waiting);Assert.AreEqual(person.HomePlaceId,waiting.Id);
                Assert.IsNull(s.DistrictAccessBlocker(waiting));
                person.LocationPlaceId=person.HomePlaceId;
                Assert.IsNull(s.DistrictPersonWaitingPlace("lin"));
            }
        }

        [Test] public void HomeWorkingLeadUsesAssignedResidenceInsteadOfOldAuthoredAddress()
        {
            using(var s=Make())
            {
                s.Meet("landlady",65);
                s.AdvanceClock(1);
                var person=s.CityPersonState("landlady");
                Assert.AreEqual(person.HomePlaceId,person.WorkPlaceId);
                Assert.AreEqual(person.HomePlaceId,person.LocationPlaceId);
                Assert.IsNull(s.HomeVisitBlocker(s.City.GetPlace(person.LocationPlaceId)));
                using(var loaded=Reload(s))
                {
                    loaded.AdvanceClock(1);var resident=loaded.CityPersonState("landlady");
                    Assert.AreEqual(resident.HomePlaceId,resident.WorkPlaceId);
                    Assert.AreEqual(resident.HomePlaceId,resident.LocationPlaceId);
                }
            }
        }

        [Test] public void AgencyPeopleWaitOnStreetBeforeBookingAndAfterLegacyReload()
        {
            using(var s=Make())
            {
                var person=s.CityPersonState("keeper_agent");
                var agency=s.City.GetPlace("agency");
                var door=s.StandOf(agency);
                // Reproduce a saved, unbooked quest target still at his indoor workplace.
                person.Route=null;person.LocationPlaceId=agency.Id;person.WorkPlaceId=agency.Id;
                person.Task=CityPersonTask.Working;person.X=door.X;person.Y=door.Y;
                var save=s.CreateSave();
                save.districtProgress=new DistrictProgress{Chapter=1,NodeId="community.meeting"};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    var actor=loaded.CityPersonState(person.PersonId);
                    Assert.False(loaded.CityPersonInside(actor));
                    var point=new MapPoint(actor.X,actor.Y);
                    Assert.AreEqual(loaded.City.ViewPosition(loaded.DistrictMeetingStreetNode),point);
                    Street(loaded,point,loaded.DistrictMeetingStreetNode);
                    Assert.IsNull(loaded.NearbyPlace,"Talking here must not open the agency or a neighboring building");
                    Assert.True(loaded.CitySamePlace(actor.PersonId));
                    Assert.True(loaded.BeginDistrictMeeting("community.meeting"));
                    var route=loaded.CityPersonState(CityLifeCatalog.TangWei).Route;
                    Assert.NotNull(route);
                    Assert.AreEqual(point.X,route.X.Last());Assert.AreEqual(point.Y,route.Y.Last());
                    Advance(loaded,loaded.DistrictStoryState.MeetingAt+1);
                    Assert.IsNull(loaded.DistrictMeetingBlocker);
                    Assert.False(loaded.CityPersonInside(actor));
                    Assert.IsNull(loaded.NearbyPlace);
                    // Include an existing two-person appointment from older saves.
                    var meetingState=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(loaded);
                    meetingState.MeetingPeople=meetingState.MeetingPeople.Concat(new[]{"keeper_agent"}).Distinct().ToArray();
                    actor.Route=null;actor.LocationPlaceId="agency";
                    Advance(loaded,loaded.Clock.Now.TotalMinutes+2);
                    AssertMeetingPeopleSeparated(loaded);
                    using(var restored=Reload(loaded))
                    {
                        AssertMeetingPeopleSeparated(restored);
                        Advance(restored,restored.Clock.Now.TotalMinutes+2);
                        AssertMeetingPeopleSeparated(restored);
                    }
                }
            }
        }

        private static void AssertMeetingPeopleSeparated(GameSession s)
        {
            var people=s.DistrictStoryState.MeetingPeople.Select(s.CityPersonState).ToArray();
            Assert.GreaterOrEqual(people.Length,2);
            for(int i=0;i<people.Length;i++)
            {
                var p=people[i];
                for(int j=0;j<i;j++)
                {
                    var q=people[j];
                    Assert.Greater((p.X-q.X)*(p.X-q.X)+(p.Y-q.Y)*(p.Y-q.Y),4,"Meeting actors must have separate interaction positions");
                }
                Street(s,new MapPoint(p.X,p.Y),s.DistrictMeetingStreetNode);
                Assert.IsNull(s.NearbyPlace,"Individual actor positions must stay clear of building interactions");
                Assert.True(s.CitySamePlace(p.PersonId));
                Assert.IsNull(s.DistrictMeetingBlocker);
            }
        }

        [Test] public void InvitationWaitsThreeDaysSurvivesReloadAndNotifiesExactlyOnce()
        {
            using(var s=Make())
            {
                s.DebugPrepareDistrictPrerequisites(1);
                var due=s.DistrictStoryState.InvitationDueAt;
                Assert.AreEqual(s.Clock.Now.TotalMinutes+4320,due);
                Assert.IsNull(s.CurrentDistrictNode);
                s.DebugPrepareDistrictPrerequisites(1);
                Assert.AreEqual(due,s.DistrictStoryState.InvitationDueAt);
                using(var loaded=Reload(s))
                {
                    int invitations=0;
                    loaded.Popup+=(title,body)=>{if(title.EndsWith("发来主线邀请"))invitations++;};
                    Assert.True(loaded.Ledger.TryDebit(loaded.Clock.Today,"验收支出",Money.FromYuan(900000),"达标后经营投入不撤回邀请"));
                    // Position the clock just before the deadline without triggering unrelated
                    // three-day sleep/hospital time jumps; full chains above exercise real elapsed days.
                    typeof(GameClock).GetProperty("Today").SetValue(loaded.Clock,new GameDate((int)((due-1)/1440)));
                    typeof(GameClock).GetProperty("MinuteOfDay").SetValue(loaded.Clock,(int)((due-1)%1440));
                    loaded.RefreshDistrictNarrative();Assert.IsNull(loaded.CurrentDistrictNode);Assert.Zero(invitations);
                    Advance(loaded,due);Assert.AreEqual("community.intro",loaded.CurrentDistrictNode.Id);
                    Assert.AreEqual(1,invitations);loaded.RefreshDistrictNarrative();Assert.AreEqual(1,invitations);
                    Assert.Zero(loaded.DistrictAccessTier);
                }
            }
        }
        [Test] public void FirstPresetCreatesRealFittedShopsAndCashOnceButDoesNotUnlockRoads()
        {
            using(var s=Make())
            {
                Assert.True(s.DebugPrepareDistrictPrerequisites(1));
                Assert.AreEqual(5,s.DistrictOperatingShops(0));Assert.AreEqual(1000000,s.Ledger.Balance.Yuan);
                var cash=s.Ledger.Balance;var stores=s.MyShops.Select(x=>x.Place.Id).ToArray();
                Assert.True(s.DebugPrepareDistrictPrerequisites(1));Assert.AreEqual(cash,s.Ledger.Balance);
                CollectionAssert.AreEqual(stores,s.MyShops.Select(x=>x.Place.Id));
                AwaitInvitation(s);Assert.AreEqual("community.intro",s.CurrentDistrictNode.Id);Assert.Zero(s.DistrictAccessTier);
                Assert.False(s.ActDistrictNode("community.meeting"));Assert.False(s.DebugPrepareDistrictPrerequisites(2));
                using(var loaded=Reload(s)){Assert.AreEqual(5,loaded.DistrictOperatingShops(0));Assert.Zero(loaded.DistrictAccessTier);Assert.AreEqual("community.intro",loaded.CurrentDistrictNode.Id);}
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void GatesStopForwardCrossingAtMiddleAndAlwaysPermitReturn(int tier)
        {
            using(var s=Make())
            {
                var gate=s.DistrictRoadGates.Single(g=>g.Tier==tier);
                var before=new MapPoint(gate.Center.X-gate.Direction.X*2,gate.Center.Y-gate.Direction.Y*2);
                var after=new MapPoint(gate.Center.X+gate.Direction.X*2,gate.Center.Y+gate.Direction.Y*2);
                Assert.True(s.TryDistrictRoadStop(before,after,out var stop,out int blocked));Assert.AreEqual(tier,blocked);
                Assert.Less((stop.X-gate.Center.X)*gate.Direction.X+(stop.Y-gate.Center.Y)*gate.Direction.Y,0);
                Assert.False(s.TryDistrictRoadStop(after,before,out _,out _));
                Assert.True(Pathfinder.FindPath(s.City.Map,gate.FromNode,gate.ToNode).Found,"NPC graph remains intact");
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ManualAndAutoTravelStopAtCheckpointAtEightTimesSpeed(bool auto)
        {
            using(var s=Make())
            {
                var gate=s.DistrictRoadGates.First();var before=new MapPoint(gate.Center.X-gate.Direction.X,gate.Center.Y-gate.Direction.Y);
                Street(s,before,gate.FromNode);int popups=0;s.Popup+=(title,body)=>{if(title=="前方暂不通行")popups++;};
                s.Time.SetSpeed(8);
                if(auto)Assert.True(s.GoTo(gate.ToNode));else s.SetSteer(gate.Direction.X,gate.Direction.Y);
                for(int i=0;i<20;i++)s.Tick(.2);
                Assert.IsNull(s.Trip);Assert.AreEqual(1,popups);
                var pos=s.PlayerPosition;Assert.Less((pos.X-gate.Center.X)*gate.Direction.X+(pos.Y-gate.Center.Y)*gate.Direction.Y,0);
            }
        }
        [Test] public void MetroCannotBypassGateOrChargeFareForRejectedTrip()
        {
            using(var s=Make())
            {
                At(s,s.City.GetPlace("metro_a"));var cash=s.Ledger.Balance;long time=s.Clock.Now.TotalMinutes;
                Assert.False(s.TakeMetro("metro_community"));Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(time,s.Clock.Now.TotalMinutes);
            }
        }
        [Test] public void TaxiCannotBypassGateAndDoesNotChargeForBlockedRoad()
        {
            using(var s=Make())
            {
                var gate=s.DistrictRoadGates.First();Street(s,s.City.ViewPosition(gate.FromNode),gate.FromNode);
                Assert.True(s.SetVehicle(VehicleKind.Taxi));var cash=s.Ledger.Balance;
                Assert.False(s.GoTo(gate.ToNode));Assert.AreEqual(cash,s.Ledger.Balance);Assert.IsNull(s.Trip);
            }
        }
        [Test] public void SaveReloadInFlightStillStopsAtRoadAndDoesNotUnlockAnything()
        {
            using(var s=Make())
            {
                var gate=s.DistrictRoadGates.First();Street(s,s.City.ViewPosition(gate.FromNode),gate.FromNode);
                Assert.True(s.GoTo(gate.ToNode));
                using(var loaded=Reload(s))
                {
                    int stops=0;loaded.Popup+=(title,body)=>{if(title=="前方暂不通行")stops++;};
                    loaded.Time.SetSpeed(8);for(int i=0;i<100&&loaded.Trip!=null;i++)loaded.Tick(.2);
                    Assert.AreEqual(1,stops);Assert.Zero(loaded.DistrictAccessTier);
                }
            }
        }
        [Test] public void CuttingTheThreeCheckpointEdgesSeparatesEveryHigherDistrictFromTheStarterArea()
        {
            using(var s=Make())
            {
                var seen=new System.Collections.Generic.HashSet<string>{s.Home.NodeId};
                var queue=new System.Collections.Generic.Queue<string>();queue.Enqueue(s.Home.NodeId);
                while(queue.Count>0)
                {
                    string from=queue.Dequeue();
                    foreach(var edge in s.City.Map.NeighborsOf(from))
                    {
                        if(s.DistrictRoadGates.Any(g=>g.FromNode==from&&g.ToNode==edge.To||g.ToNode==from&&g.FromNode==edge.To))continue;
                        if(seen.Add(edge.To))queue.Enqueue(edge.To);
                    }
                }
                foreach(var place in s.City.Places)Assert.AreEqual(s.DistrictTierOf(place)==0,seen.Contains(place.NodeId),place.Id);
            }
        }
        [Test] public void InvitationAndRealMeetingUnlockOnlyFirstGateAndSurviveReload()
        {
            using(var s=Make())
            {
                s.DebugPrepareDistrictPrerequisites(1);AwaitInvitation(s);
                Assert.True(s.BeginDistrictMeeting("community.intro","later"));Assert.AreEqual("community.intro",s.CurrentDistrictNode.Id);
                Assert.True(s.BeginDistrictMeeting("community.intro","accept"));Assert.Zero(s.DistrictAccessTier);
                Assert.False(s.ActDistrictNode("community.meeting"),"Remote opening does not complete a meeting");
                Finish(s);Assert.Zero(s.DistrictAccessTier);
                Assert.False(s.SearchCommunityBox(2),"No remote pickup without the clue");
                CompleteCommunitySearch(s);Assert.Zero(s.DistrictAccessTier);
                Assert.False(s.SearchCommunityBox(2),"Cannot collect twice");
                using(var pending=Reload(s)){Assert.AreEqual(7,pending.DistrictStoryState.CommunityPieces);Assert.Zero(pending.DistrictAccessTier);}
                Finish(s);Assert.AreEqual(1,s.DistrictAccessTier);
                Assert.True(s.DistrictStoryState.CommunityMapDelivered);
                Assert.True(s.DistrictRoadOpen(1));Assert.False(s.DistrictRoadOpen(2));
                using(var loaded=Reload(s)){Assert.AreEqual(1,loaded.DistrictAccessTier);Assert.True(loaded.DistrictNodeCompleted("community.meeting"));}
            }
        }
        [Test] public void ThreeIndependentLeadsRequireActualOutingsAndPhysicalBoxesAndSurviveReload()
        {
            using(var s=Make())
            {
                s.DebugPrepareDistrictPrerequisites(1);AwaitInvitation(s);s.BeginDistrictMeeting("community.intro","accept");Finish(s);Finish(s);
                Assert.False(s.BeginDistrictMeeting("community.search"));Assert.False(s.ActDistrictNode("community.search"));
                for(int i=0;i<3;i++)
                {
                    s.Meet(GameSession.CommunityLeadPerson(i),40);s.AddRelation(GameSession.CommunityLeadPerson(i),40);
                    AskLeadLocally(s,i);Assert.False(s.CommunityHasClue(i),"Relation alone is insufficient");
                    Street(s,s.CommunityBoxPosition(i),s.City.GetPlace(GameSession.CommunityBoxPlace(i)).NodeId);
                    Assert.False(s.SearchCommunityBox(i));Assert.False(s.CommunityBoxAvailable(i));
                    Assert.Zero(s.DistrictTierOf(s.City.GetPlace(GameSession.CommunityBoxPlace(i))));
                }
                CompleteLeadOuting(s,0);Assert.True(s.CommunityHasClue(0),"Completed outing reveals the clue without another conversation");
                var outing=s.CityActivities.Last(a=>a.State==CityActivityPhase.Completed);
                using(var restored=Reload(s))
                {
                    Assert.AreEqual(0,restored.TakeCommunityActivityClue(outing.Id));
                    Assert.AreEqual(-1,restored.TakeCommunityActivityClue(outing.Id));
                    using(var twice=Reload(restored))Assert.AreEqual(-1,twice.TakeCommunityActivityClue(outing.Id));
                }
                AskLeadLocally(s,0);Assert.True(s.CommunityBoxAvailable(0));Assert.False(s.CommunityBoxAvailable(1));Assert.False(s.CommunityBoxAvailable(2));
                Assert.False(s.SearchCommunityBox(0),"Remote inquiry cannot collect a piece");
                Street(s,s.CommunityBoxPosition(0),s.City.GetPlace(GameSession.CommunityBoxPlace(0)).NodeId);
                Assert.True(s.SearchCommunityBox(0));Assert.False(s.SearchCommunityBox(0));Assert.AreEqual("community.search",s.CurrentDistrictNode.Id);
                using(var loaded=Reload(s))
                {
                    Assert.True(loaded.CommunityHasPiece(0));Assert.True(loaded.CommunityHasClue(0));Assert.True(loaded.CommunityHasOuting(0));
                    Assert.False(loaded.CommunityBoxAvailable(0));Assert.False(loaded.CommunityBoxAvailable(1));
                    foreach(int i in new[]{2,1})
                    {
                        CompleteLeadOuting(loaded,i);AskLeadLocally(loaded,i);
                        ReachCommunityBox(loaded, i);
                        Assert.True(loaded.SearchCommunityBox(i));
                        Assert.Zero(loaded.DistrictAccessTier);
                    }
                    Assert.AreEqual("community.trial",loaded.CurrentDistrictNode.Id);Assert.Zero(loaded.DistrictAccessTier);
                    Finish(loaded);Assert.True(loaded.DistrictRoadOpen(1));
                }
            }
        }
        [Test] public void IncompleteWrongPersonAndWrongServiceOutingsDoNotRevealClues()
        {
            using(var s=Make())
            {
                s.SetSmartphone(true);
                var state=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
                state.NodeId="community.search";state.CommunityMapQuest=true;state.CommunitySearchVersion=2;state.CommunityAcceptedAt=s.Clock.Now.TotalMinutes;
                var record=typeof(GameSession).GetMethod("RecordCommunityOuting",BindingFlags.Instance|BindingFlags.NonPublic);
                var a=new CityActivity{DefinitionId="hike",VenueId="life_hike",ActualStart=state.CommunityAcceptedAt,EntryPaid=true,RewardApplied=true,
                    State=CityActivityPhase.Completed,Participants=new[]{new CityActivityParticipant{PersonId="landlady"}}};
                foreach(var phase in new[]{CityActivityPhase.Scheduled,CityActivityPhase.Cancelled,CityActivityPhase.NoShow,CityActivityPhase.Interrupted})
                {a.State=phase;record.Invoke(s,new object[]{a});Assert.Zero(state.CommunityOutings);}
                a.State=CityActivityPhase.Completed;a.ActualStart--;record.Invoke(s,new object[]{a});Assert.Zero(state.CommunityOutings);
                a.ActualStart++;a.Participants[0].PersonId="lin";record.Invoke(s,new object[]{a});Assert.Zero(state.CommunityOutings);
                a.Participants[0].PersonId="landlady";a.ServiceDefinitionId="dinner";record.Invoke(s,new object[]{a});Assert.Zero(state.CommunityOutings);
                a.ServiceDefinitionId=null;record.Invoke(s,new object[]{a});Assert.AreEqual(1,state.CommunityOutings);
                s.Meet("landlady",0);s.AddRelation("landlady",-s.Relation("landlady"));AskLeadLocally(s,0);Assert.False(s.CommunityHasClue(0));
                s.AddRelation("landlady",30);AskLeadLocally(s,0);Assert.True(s.CommunityHasClue(0));
            }
        }
        [Test] public void ServiceEndingExactlyAtClosingCountsAsCompleted()
        {
            using(var s=Make())
            {
                var venue=s.City.GetPlace("life_hike");
                s.Clock.Restore(s.Clock.Today,venue.CloseMinute-105);
                long start=s.Clock.Now.TotalMinutes+15;At(s,venue);
                var a=s.ScheduleCityActivity("hike",Array.Empty<string>(),start);Assert.NotNull(a);
                At(s,venue);Advance(s,start);Assert.True(s.SelectVenueService("hike",0));
                s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                Advance(s,start+90);Assert.AreEqual(venue.CloseMinute,s.Clock.MinuteOfDay);
                Assert.AreEqual(CityActivityPhase.Completed,a.State);Assert.True(a.RewardApplied);
            }
        }

        private static void ReachCommunityBox(GameSession s, int index)
        {
            var landmark = s.City.GetPlace(GameSession.CommunityBoxPlace(index));
            Street(s, s.City.ViewPosition(landmark.NodeId), landmark.NodeId);
            s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100;
            s.Time.SetSpeed(8);
            Assert.True(s.GoTo(s.CommunityBoxNode(index)));
            for (int i = 0; i < 1200 && s.Trip != null; i++) s.Tick(.05);
            Assert.AreEqual(index, s.NearbyCommunityBox);
            Assert.IsNull(s.NearbyPlace, "Picking up a box must not overlap a building entrance");
        }

        [Test] public void VisibleHomesHaveReachableDoorsAndStableResidentAddresses()
        {
            using (var s = Make())
            {
                var homes = s.City.Blocks.Select(block => s.City.GetPlace(block.homeId)).ToArray();
                Assert.AreEqual(s.City.Blocks.Count, homes.Select(home => home.Id).Distinct().Count());
                foreach (var home in homes)
                {
                    Assert.AreEqual(PlaceKind.Home, home.Kind);
                    Assert.True(s.CityPeople.Any(person => person.HomePlaceId == home.Id), home.Id);
                    Assert.Less(s.CityPeople.Count(person => person.HomePlaceId == home.Id), 3, home.Id);
                    Street(s, s.StandOf(home), home.NodeId);
                    Assert.AreEqual(home, s.NearbyPlace, home.Id);
                }
                using (var restored = Reload(s))
                    foreach (var resident in s.CityPeople)
                        Assert.AreEqual(resident.HomePlaceId, restored.CityPersonState(resident.PersonId).HomePlaceId);

                s.Meet("landlady", 65);
                var legacy = s.CreateSave();
                legacy.cityLifeVersion = 2;
                foreach (var resident in legacy.cityPeople)
                {
                    var original = CityLifeCatalog.Person(resident.PersonId);
                    if (original == null || !s.City.HasPlace(original.HomeId)) continue;
                    resident.HomePlaceId = original.HomeId;
                    if (resident.Task == CityPersonTask.Home) resident.LocationPlaceId = original.HomeId;
                }
                using (var migrated = GameSession.Load(s.City, legacy))
                {
                    Assert.AreEqual(65, migrated.Relation("landlady"));
                    foreach (var home in homes)
                        Assert.True(migrated.CityPeople.Any(person => person.HomePlaceId == home.Id), home.Id);
                    foreach (var resident in migrated.CityPeople.Where(person => person.Task == CityPersonTask.Home && person.Route == null))
                        Assert.AreEqual(resident.HomePlaceId, resident.LocationPlaceId, resident.PersonId);
                }
            }
        }

        [Test] public void HomeVisitRequiresFamiliarResidentToBePresent()
        {
            using (var s = Make())
            {
                var resident = s.CityPersonState("landlady");
                var home = s.City.GetPlace(resident.HomePlaceId);
                resident.Route = null;
                resident.LocationPlaceId = home.Id;
                resident.Task = CityPersonTask.Home;
                Street(s, s.StandOf(home), home.NodeId);
                s.AddRelation(resident.PersonId, GameSession.HomeVisitMinRelation - 1 - s.Relation(resident.PersonId));
                Assert.False(s.EnterNearby());

                s.AddRelation(resident.PersonId, 1);
                resident.LocationPlaceId = null;
                Assert.False(s.EnterNearby());

                resident.LocationPlaceId = home.Id;
                Assert.True(s.EnterNearby());
                Assert.AreEqual(home, s.CurrentPlace);
                Assert.True(s.CitySamePlace(resident.PersonId));
                s.Leave();
                Assert.False(s.CitySamePlace(resident.PersonId));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void EachDispersedBoxIsWalkableInsideStarterDistrict(int index)
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.districtProgress=new DistrictProgress{Chapter=1,NodeId="community.search",CommunityMapQuest=true,CommunitySearchVersion=2,CommunityClues=7};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(3,Enumerable.Range(0,3).Select(i=>loaded.City.GetPlace(GameSession.CommunityBoxPlace(i)).StreetId).Distinct().Count());
                    var place=loaded.City.GetPlace(GameSession.CommunityBoxPlace(index));Assert.Zero(loaded.DistrictTierOf(place));
                    ReachCommunityBox(loaded, index);
                    Assert.True(loaded.SearchCommunityBox(index));
                }
            }
        }

        [Test] public void PreviouslyUnlockedCommunitySaveKeepsAccessWithoutNewTrial()
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.districtProgress=new DistrictProgress {UnlockedTier=1,Chapter=1,Completed=new[]{"community.intro","community.meeting"}};
                using(var loaded=GameSession.Load(s.City,save))
                {Assert.True(loaded.DistrictRoadOpen(1));Assert.IsNull(loaded.CurrentDistrictNode);}
            }
        }

        [Test] public void LegacySaveKeepsAssetsButNewRoadRulesApplyWithoutSharingStateBetweenSessions()
        {
            using(var s=Make())
            {
                s.DebugPrepareDistrictPrerequisites(1);var save=s.CreateSave();save.districtProgress=null;
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(s.MyShopCount,loaded.MyShopCount);Assert.AreEqual(s.Ledger.Balance,loaded.Ledger.Balance);
                    Assert.Zero(loaded.DistrictAccessTier);Assert.False(s.DistrictRoadOpen(1));
                }
            }
        }
    }
}
