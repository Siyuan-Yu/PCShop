using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class DistrictDetailTests
    {
        private static void At(GameSession s,string place)
        {
            s.StopTrip();var p=s.City.GetPlace(place);
            typeof(GameSession).GetProperty("CurrentPlace",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,p);
            typeof(GameSession).GetProperty("PlayerNodeId",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,p.NodeId);
        }
        private static GameSession Stage(string node,int chapter=2)
        {
            using(var seed=DistrictProgressionTests.Make())
            {
                var save=seed.CreateSave();save.districtProgress=new DistrictProgress{Chapter=chapter,UnlockedTier=chapter-1,NodeId=node};
                var s=GameSession.Load(seed.City,save);s.EventsEnabled=false;s.DebugAddCash(100000);return s;
            }
        }
        private static void Meeting(GameSession s,string choice=null)
        {
            Assert.True(s.BeginDistrictMeeting(s.CurrentDistrictNode.Id,choice));
            var m=s.DistrictStoryState;
            s.Clock.Restore(new GameDate((int)(m.MeetingAt/1440)),(int)(m.MeetingAt%1440));At(s,m.MeetingPlaceId);
            // Only movement is positioned by this fixture; evidence must use production actions.
            foreach(var id in m.MeetingPeople){var p=s.CityPersonState(id);p.Route=null;p.LocationPlaceId=m.MeetingPlaceId;}
            Assert.IsNull(s.DistrictMeetingBlocker);
        }
        [TestCase(720,1)] [TestCase(1140,1)] [TestCase(720,2)] [TestCase(1140,3)]
        public void SleepingPastInviteDeliversExactlyTwoHoursAfterActualWake(int wake,int chapter)
        {
            using(var seed=DistrictProgressionTests.Make())
            {
                var save=seed.CreateSave();save.districtProgress=new DistrictProgress{Chapter=chapter-1,UnlockedTier=chapter-1,PendingChapter=chapter,InvitationDueAt=seed.Clock.Now.TotalMinutes+30};
                using(var s=GameSession.Load(seed.City,save))
                {
                    s.EventsEnabled=false;At(s,s.Home.Id);int popups=0;s.Popup+=(title,body)=>{if(title.EndsWith("发来主线邀请"))popups++;};
                    Assert.True(s.SleepUntil(wake));Assert.IsNull(s.CurrentDistrictNode);Assert.Zero(popups);
                    Assert.AreEqual(s.Clock.Now.TotalMinutes+120,s.DistrictStoryState.InvitationDueAt);
                    s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;s.AdvanceClock(119);
                    Assert.IsNull(s.CurrentDistrictNode);s.AdvanceClock(1);Assert.AreEqual(new[]{"community.intro","park.proposal","garden.invitation"}[chapter-1],s.CurrentDistrictNode.Id);
                    Assert.AreEqual(1,popups);s.RefreshDistrictNarrative();Assert.AreEqual(1,popups);
                }
            }
        }
        [Test] public void DelayedInvitationSurvivesSaveAndAnotherSleep()
        {
            using(var seed=DistrictProgressionTests.Make())
            {
                var save=seed.CreateSave();save.districtProgress=new DistrictProgress{PendingChapter=1,InvitationDueAt=seed.Clock.Now.TotalMinutes+10};
                using(var first=GameSession.Load(seed.City,save))
                {
                    At(first,first.Home.Id);first.SleepUntil(720);
                    var roundtrip=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(first.CreateSave()));
                    using(var s=GameSession.Load(seed.City,roundtrip))
                    {
                        Assert.AreEqual(first.DistrictStoryState.InvitationDueAt,s.DistrictStoryState.InvitationDueAt);
                        At(s,s.Home.Id);Assert.True(s.SleepUntil(900));
                        Assert.IsNull(s.CurrentDistrictNode);Assert.AreEqual(s.Clock.Now.TotalMinutes+120,s.DistrictStoryState.InvitationDueAt);
                        s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;s.AdvanceClock(120);
                        Assert.AreEqual("community.intro",s.CurrentDistrictNode.Id);
                    }
                }
            }
        }
        [TestCase("creator")] [TestCase("supplier")]
        public void ModelClueBranchesRequireOrderedEvidenceNotOneContinue(string choice)
        {
            using(var s=Stage("model.clues"))
            {
                Meeting(s,choice);var tasks=s.DistrictTasks.ToArray();Assert.AreEqual(3,tasks.Length);
                Assert.IsNull(s.DistrictPersonObjective(choice=="supplier"?"life_creator":"life_model_supplier"),"Unselected lead must not keep a quest marker");
                Assert.False(s.ActDistrictNode("model.clues"));
                Assert.NotNull(s.DistrictTaskBlocker(tasks[2].Id));s.PerformDistrictTask(tasks[2].Id);Assert.False(s.HasDistrictTask(tasks[2].Id));
                foreach(var t in tasks)
                {
                    At(s,t.PlaceId??s.DistrictStoryState.MeetingPlaceId);
                    Assert.IsNull(s.DistrictTaskBlocker(t.Id),t.Id);s.PerformDistrictTask(t.Id);Assert.True(s.HasDistrictTask(t.Id));
                }
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,save))CollectionAssert.AreEqual(s.DistrictStoryState.TaskEvidence,loaded.DistrictStoryState.TaskEvidence);
                At(s,s.DistrictStoryState.MeetingPlaceId);Assert.True(s.ActDistrictNode("model.clues"));
                Assert.AreEqual("model.repair",s.CurrentDistrictNode.Id);Assert.False(s.DistrictRoadOpen(2));
            }
        }
        [Test] public void InspectionCannotBeDoneRemotelyAndEvidenceCopyIsIndependent()
        {
            using(var s=Stage("park.change"))
            {
                Meeting(s,"honor");At(s,s.Home.Id);s.PerformDistrictTask("fair.walk");Assert.False(s.HasDistrictTask("fair.walk"));
                At(s,"life_service");s.PerformDistrictTask("fair.walk");Assert.True(s.HasDistrictTask("fair.walk"));
                s.PerformDistrictTask("fair.walk");Assert.AreEqual(1,s.DistrictStoryState.TaskEvidence.Count(id=>id=="fair.walk"));
                var copy=s.DistrictStoryState;copy.TaskEvidence[0]="corrupt";Assert.True(s.HasDistrictTask("fair.walk"));
                Assert.False(s.ActDistrictNode("park.change"),"inspection alone does not settle merchant consent");
            }
        }
        [TestCase("trial","life_tenant")] [TestCase("limited","life_tenant")] [TestCase("respect","life_clothier")]
        public void GardenTermsRequireConsentFromActualPartner(string choice,string partner)
        {
            using(var s=Stage("garden.terms",3))
            {
                Meeting(s,choice);var tasks=s.DistrictTasks.ToArray();
                Assert.AreEqual(partner,tasks.Single(t=>t.Id=="garden.partner").PersonId);
                Assert.False(s.ActDistrictNode("garden.terms"));
                foreach(var t in tasks){Assert.IsNull(s.DistrictTaskBlocker(t.Id),t.Id);s.PerformDistrictTask(t.Id);}
                Assert.IsNull(s.DistrictTasksBlocker);Assert.False(s.DistrictRoadOpen(3));
            }
        }
        [Test] public void AlreadyPaidRepairCannotChargeAgainAndStillRequiresReceipt()
        {
            using(var s=Stage("model.repair"))
            {
                Meeting(s,"restore");foreach(var t in s.DistrictTasks.ToArray()){s.PerformDistrictTask(t.Id);Assert.True(s.HasDistrictTask(t.Id));}
                Assert.True(s.ActDistrictNode("model.repair"));var balance=s.Ledger.Balance;
                Assert.NotNull(s.DistrictTaskBlocker("model.receipt"));Assert.False(s.ActDistrictNode("model.repair"));Assert.AreEqual(balance,s.Ledger.Balance);
                s.Clock.Restore(new GameDate((int)(s.DistrictStoryState.RepairReadyAt/1440)),(int)(s.DistrictStoryState.RepairReadyAt%1440));
                s.PerformDistrictTask("model.receipt");Assert.True(s.ActDistrictNode("model.repair"));Assert.AreEqual(balance,s.Ledger.Balance);Assert.True(s.DistrictStoryState.HasModel);
            }
        }
    }
}
