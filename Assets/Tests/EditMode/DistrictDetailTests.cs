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


        [TestCase(600,1)] [TestCase(720,1)] [TestCase(1140,1)] [TestCase(720,2)] [TestCase(1140,3)]
        public void SleepingPastInviteDeliversExactlyTwoHoursAfterActualWake(int wake,int chapter)
        {
            using(var seed=DistrictProgressionTests.Make())
            {
                if(wake==600)seed.Clock.Restore(seed.Clock.Today,540);
                var save=seed.CreateSave();save.districtProgress=new DistrictProgress{Chapter=chapter-1,UnlockedTier=chapter-1,PendingChapter=chapter,InvitationDueAt=seed.Clock.Now.TotalMinutes+30};
                using(var s=GameSession.Load(seed.City,save))
                {
                    s.EventsEnabled=false;At(s,s.Home.Id);int popups=0;s.Popup+=(title,body)=>{if(title.EndsWith("发来主线邀请"))popups++;};
                    Assert.True(s.SleepUntil(wake));Assert.IsNull(s.CurrentDistrictNode);Assert.Zero(popups);
                    Assert.AreEqual(s.Clock.Now.TotalMinutes+120,s.DistrictStoryState.InvitationDueAt);
                    s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;s.AdvanceClock(119);
                    Assert.IsNull(s.CurrentDistrictNode);s.AdvanceClock(1);Assert.AreEqual(new[]{"community.intro","expo.invitation","tender.invitation"}[chapter-1],s.CurrentDistrictNode.Id);
                    Assert.AreEqual(1,popups);s.RefreshDistrictNarrative();Assert.AreEqual(1,popups);
                }
            }
        }
        [Test] public void SleepingToTenBeforeSavedDeadlineStillInvitesAtFourteenOhOne()
        {
            using(var seed=DistrictProgressionTests.Make())
            {
                var save=seed.CreateSave();
                save.districtProgress=new DistrictProgress{PendingChapter=1,InvitationDueAt=5161};
                using(var s=GameSession.Load(seed.City,save))
                {
                    s.EventsEnabled=false;
                    s.Clock.Restore(new GameDate(2),1320);
                    At(s,s.Home.Id);
                    int invites=0;
                    s.Popup+=(title,body)=>{if(title.EndsWith("发来主线邀请"))invites++;};
                    Assert.True(s.SleepUntil(600));
                    Assert.AreEqual(4920,s.Clock.Now.TotalMinutes);
                    StringAssert.Contains("第4天 14:01",s.DistrictInvitationStatus);
                    s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                    s.AdvanceClock(240);
                    Assert.IsNull(s.CurrentDistrictNode);
                    StringAssert.Contains("0小时1分钟",s.DistrictInvitationStatus);
                    s.AdvanceClock(1);
                    Assert.AreEqual("community.intro",s.CurrentDistrictNode.Id);
                    Assert.IsNull(s.DistrictInvitationStatus);
                    Assert.AreEqual(1,invites);
                    s.RefreshDistrictNarrative();Assert.AreEqual(1,invites);
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





    }
}
