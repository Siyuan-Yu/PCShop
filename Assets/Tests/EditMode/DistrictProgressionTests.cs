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
        internal static void CompleteCommunitySearch(GameSession s)
        {
            Assert.AreEqual("community.clue",s.CurrentDistrictNode.Id);Finish(s);
            Assert.AreEqual("community.search",s.CurrentDistrictNode.Id);
            foreach(int i in new[]{2,0,1})
            {
                CompleteLeadOuting(s,i);s.AskCommunityLead(i,true);Assert.True(s.CommunityHasClue(i));
                Street(s,s.CommunityBoxPosition(i),s.City.GetPlace(GameSession.CommunityBoxPlace(i)).NodeId);
                Assert.True(s.SearchCommunityBox(i));Assert.False(s.DistrictStoryState.HasCommunityMap);
            }
            Assert.True(s.AssembleCommunityMap());Assert.True(s.DistrictStoryState.HasCommunityMap);
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
            At(s,s.City.GetPlace(state.MeetingPlaceId));
            Assert.IsNull(s.DistrictMeetingBlocker,s.CurrentDistrictNode.Id);
            foreach(string id in s.DistrictStoryState.MeetingPeople)Assert.False(s.CityPersonInside(s.CityPersonState(id)),"Meeting participants wait visibly at the entrance");
        }
        private static void Finish(GameSession s)
        {
            string id=s.CurrentDistrictNode.Id;Meet(s);
            CompleteDetailedTasks(s);
            Assert.True(s.ActDistrictNode(id),id);
        }
        private static void CompleteDetailedTasks(GameSession s)
        {
            foreach(var task in s.DistrictTasks.ToArray())
            {
                if(s.HasDistrictTask(task.Id))continue;
                At(s,s.City.GetPlace(task.PlaceId??s.DistrictStoryState.MeetingPlaceId));
                Assert.IsNull(s.DistrictTaskBlocker(task.Id),task.Id);
                s.PerformDistrictTask(task.Id);Assert.True(s.HasDistrictTask(task.Id),task.Id);
            }
            At(s,s.City.GetPlace(s.DistrictStoryState.MeetingPlaceId));
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
                using(var pending=Reload(s)){Assert.True(pending.DistrictStoryState.HasCommunityMap);Assert.Zero(pending.DistrictAccessTier);}
                Finish(s);Assert.AreEqual(1,s.DistrictAccessTier);
                Assert.False(s.DistrictStoryState.HasCommunityMap);Assert.True(s.DistrictStoryState.CommunityMapDelivered);
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
                    s.AskCommunityLead(i,true);Assert.False(s.CommunityHasClue(i),"Relation alone is insufficient");
                    Street(s,s.CommunityBoxPosition(i),s.City.GetPlace(GameSession.CommunityBoxPlace(i)).NodeId);
                    Assert.False(s.SearchCommunityBox(i));Assert.False(s.CommunityBoxAvailable(i));
                    Assert.Zero(s.DistrictTierOf(s.City.GetPlace(GameSession.CommunityBoxPlace(i))));
                }
                CompleteLeadOuting(s,0);Assert.False(s.CommunityHasClue(0),"Must ask after outing");
                s.AskCommunityLead(0,true);Assert.True(s.CommunityBoxAvailable(0));Assert.False(s.CommunityBoxAvailable(1));Assert.False(s.CommunityBoxAvailable(2));
                Assert.False(s.SearchCommunityBox(0),"Remote inquiry cannot collect a piece");
                Street(s,s.CommunityBoxPosition(0),s.City.GetPlace(GameSession.CommunityBoxPlace(0)).NodeId);
                Assert.True(s.SearchCommunityBox(0));Assert.False(s.SearchCommunityBox(0));Assert.False(s.AssembleCommunityMap());
                using(var loaded=Reload(s))
                {
                    Assert.True(loaded.CommunityHasPiece(0));Assert.True(loaded.CommunityHasClue(0));Assert.True(loaded.CommunityHasOuting(0));
                    Assert.False(loaded.CommunityBoxAvailable(0));Assert.False(loaded.CommunityBoxAvailable(1));
                    foreach(int i in new[]{2,1})
                    {
                        CompleteLeadOuting(loaded,i);loaded.AskCommunityLead(i,true);
                        var place=loaded.City.GetPlace(GameSession.CommunityBoxPlace(i));var start=loaded.City.ViewPosition(place.NodeId);var target=loaded.CommunityBoxPosition(i);
                        Street(loaded,start,place.NodeId);loaded.Player.Health=100;loaded.Player.Hunger=100;loaded.Player.Energy=100;loaded.Time.SetSpeed(1);
                        double dx=target.X-start.X,dy=target.Y-start.Y,len=Math.Sqrt(dx*dx+dy*dy);
                        loaded.SetSteer(dx/len,dy/len);
                        for(int n=0;n<600&&loaded.NearbyCommunityBox!=i;n++)loaded.Tick(.05);
                        Assert.AreEqual(i,loaded.NearbyCommunityBox,"Box must be reachable from its street: "+i);
                        loaded.SetSteer(0,0);Assert.True(loaded.SearchCommunityBox(i));
                        Assert.False(loaded.DistrictStoryState.HasCommunityMap);Assert.Zero(loaded.DistrictAccessTier);
                    }
                    Assert.True(loaded.AssembleCommunityMap());Assert.False(loaded.AssembleCommunityMap());Assert.Zero(loaded.DistrictAccessTier);
                    Finish(loaded);Assert.True(loaded.DistrictRoadOpen(1));Assert.False(loaded.DistrictStoryState.HasCommunityMap);
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
                s.Meet("landlady",0);s.AddRelation("landlady",-s.Relation("landlady"));s.AskCommunityLead(0,true);Assert.False(s.CommunityHasClue(0));
                s.AddRelation("landlady",30);s.AskCommunityLead(0,true);Assert.True(s.CommunityHasClue(0));
            }
        }
        [Test] public void ServiceEndingExactlyAtClosingCountsAsCompleted()
        {
            using(var s=Make())
            {
                var venue=s.City.GetPlace("life_restaurant");
                s.Clock.Restore(s.Clock.Today,venue.CloseMinute-75);
                long start=s.Clock.Now.TotalMinutes+15;At(s,venue);
                var a=s.ScheduleCityActivity("dinner",Array.Empty<string>(),start);Assert.NotNull(a);
                At(s,venue);Advance(s,start);Assert.True(s.SelectVenueService("dinner",0));
                s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                Advance(s,start+60);Assert.AreEqual(venue.CloseMinute,s.Clock.MinuteOfDay);
                Assert.AreEqual(CityActivityPhase.Completed,a.State);Assert.True(a.RewardApplied);
            }
        }
        [TestCase(false)] [TestCase(true)] public void LegacyMapSavesPreserveEarnedMapAndMigrateUnfinishedSearch(bool hasMap)
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.districtProgress=new DistrictProgress{Chapter=1,CommunityMapQuest=true,
                    NodeId=hasMap?"community.trial":"community.search",HasCommunityMap=hasMap,CommunityBoxesInspected=1};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(hasMap,loaded.DistrictStoryState.HasCommunityMap);Assert.Zero(loaded.DistrictAccessTier);
                    Assert.AreEqual(hasMap?"community.trial":"community.search",loaded.CurrentDistrictNode.Id);
                    if(!hasMap){Assert.AreEqual(2,loaded.DistrictStoryState.CommunitySearchVersion);Assert.Zero(loaded.DistrictStoryState.CommunityClues);Assert.Zero(loaded.DistrictStoryState.CommunityPieces);}
                }
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
                    var start=loaded.City.ViewPosition(place.NodeId);var target=loaded.CommunityBoxPosition(index);Street(loaded,start,place.NodeId);
                    loaded.Time.SetSpeed(1);double dx=target.X-start.X,dy=target.Y-start.Y,len=Math.Sqrt(dx*dx+dy*dy);loaded.SetSteer(dx/len,dy/len);
                    for(int n=0;n<600&&loaded.NearbyCommunityBox!=index;n++)loaded.Tick(.05);
                    Assert.AreEqual(index,loaded.NearbyCommunityBox);loaded.SetSteer(0,0);Assert.True(loaded.SearchCommunityBox(index));
                }
            }
        }
        [Test] public void SupersededTradingAssignmentMigratesToClueWithoutUnlocking()
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.districtProgress=new DistrictProgress {Chapter=1,NodeId="community.trial",CommunityTrialStarted=true,
                    Completed=new[]{"community.intro","community.meeting"}};
                using(var loaded=GameSession.Load(s.City,save))
                {Assert.AreEqual("community.clue",loaded.CurrentDistrictNode.Id);Assert.Zero(loaded.DistrictAccessTier);Assert.False(loaded.DistrictStoryState.HasCommunityMap);}
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
        [TestCase("trial")] [TestCase("limited")] [TestCase("respect")]
        public void AllThreeStoryChainsRequireActualCostsItemTimeAndProjectSalesWithoutReplay(string gardenChoice)
        {
            using(var s=Make())
            {
                Assert.True(s.DebugPrepareDistrictPrerequisites(1));AwaitInvitation(s);s.BeginDistrictMeeting("community.intro","accept");Finish(s);CompleteCommunitySearch(s);Finish(s);
                Assert.True(s.DebugPrepareDistrictPrerequisites(2));Assert.AreEqual(6,s.DistrictOperatingShops(1));
                Assert.GreaterOrEqual(s.NetWorth().Yuan,30000000);Assert.AreEqual(1,s.DistrictAccessTier);AwaitInvitation(s);
                for(int guard=0;s.CurrentDistrictNode!=null&&s.DistrictAccessTier<2&&guard<12;guard++)
                {
                    string id=s.CurrentDistrictNode.Id;Meet(s);CompleteDetailedTasks(s);Assert.True(s.ActDistrictNode(id),id);
                    if(id=="park.delivery"||id=="model.dinner")
                    {
                        Assert.False(s.ActDistrictNode(id));Advance(s,s.Clock.Now.TotalMinutes+30);Assert.True(s.ActDistrictNode(id));
                    }
                    if(id=="model.repair")
                    {
                        var cash=s.Ledger.Balance;Assert.False(s.ActDistrictNode(id));Assert.AreEqual(cash,s.Ledger.Balance);
                        using(var loaded=Reload(s)){Assert.True(loaded.DistrictStoryState.RepairPaid);Assert.False(loaded.ActDistrictNode(id));Assert.AreEqual(cash,loaded.Ledger.Balance);}
                        Advance(s,s.DistrictStoryState.RepairReadyAt);CompleteDetailedTasks(s);Assert.True(s.ActDistrictNode(id));Assert.True(s.DistrictStoryState.HasModel);
                    }
                    var after=s.Ledger.Balance;Assert.False(s.ActDistrictNode(id));Assert.AreEqual(after,s.Ledger.Balance,"Old callback cannot repeat a transaction");
                }
                Assert.AreEqual(2,s.DistrictAccessTier);Assert.True(s.DistrictStoryState.ModelDelivered);Assert.False(s.DistrictStoryState.HasModel);
                Assert.True(s.DebugPrepareDistrictPrerequisites(3));Assert.IsNull(s.DistrictPrerequisiteBlocker(3));Assert.AreEqual(2,s.DistrictAccessTier);AwaitInvitation(s);
                Finish(s);
                var project=s.MyShops.First(shop=>s.DistrictTierOf(shop.Place)==2);
                Assert.True(s.SelectDistrictProjectShop(project.Place.Id));Finish(s);
                Meet(s,gardenChoice);CompleteDetailedTasks(s);var beforeContract=s.Ledger.Balance;Assert.True(s.ActDistrictNode("garden.terms"));
                Assert.AreEqual(gardenChoice=="limited"?50000:gardenChoice=="respect"?10000:5000,(beforeContract-s.Ledger.Balance).Yuan);
                Assert.AreEqual(gardenChoice=="respect"?"life_clothier":"life_tenant",s.DistrictStoryState.ProjectPartnerId);
                Assert.AreEqual("garden.fulfilment",s.CurrentDistrictNode.Id);Meet(s);
                Assert.False(s.ActDistrictNode("garden.fulfilment"));
                Advance(s,s.DistrictStoryState.ProjectStartedAt+1441);
                Meet(s);
                s.Ledger.Credit(s.Clock.Today,"贷款",Money.FromYuan(100000),"不是营业收入",LedgerAttribution.ForShop(project.Place.Id));
                Assert.False(s.ActDistrictNode("garden.fulfilment"));
                s.Ledger.Credit(s.Clock.Today,ShopSimulation.SalesCategory,Money.FromYuan(100),"测试实际销售凭据",LedgerAttribution.ForShop(project.Place.Id));
                CompleteDetailedTasks(s);
                Assert.True(s.ActDistrictNode("garden.fulfilment"));Assert.AreEqual(3,s.DistrictAccessTier);Finish(s);
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(3,loaded.DistrictAccessTier);Assert.IsNull(loaded.CurrentDistrictNode);
                    Assert.AreEqual(18,loaded.DistrictStoryState.Completed.Distinct().Count());
                    Assert.False(loaded.DebugPrepareDistrictPrerequisites(3));
                }
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
