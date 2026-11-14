using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class OnsitePlayTests
    {
        private static GameSession Make()
        {
            using var f=DistrictProgressionTests.Make();using var first=new GameSession(f.City,1221,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(42),660),false,true,true);first.Tick(0);
            var d=first.CreateSave();d.districtProgress=new DistrictProgress{UnlockedTier=3};var s=GameSession.Load(first.City,d);s.EventsEnabled=false;s.SetSmartphone(true);s.Player.Health=s.Player.Hunger=s.Player.Energy=100;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.MainShop.Sim.NotFitted=false;s.MainShop.Sim.SetHours(0,1440);return s;
        }
        private static void At(GameSession s,string id){s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,id==null?null:s.City.GetPlace(id));if(id!=null)typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.City.GetPlace(id).NodeId);}
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static CityActivity Book(GameSession s,int n,int choice=0)
        {
            string id=n==76?"act_comic_ningxi":new[]{"act_bar_bartender","act_bar_taoran","act_bar_zengyu","act_court_coach","act_court_dongyu","act_court_zhang","act_golf_golfer","act_golf_enterprise"}[n-51];
            s.RaiseEvent(id);Assert.NotNull(s.PendingEvent,id);Assert.IsTrue(s.ResolveEvent(choice));return s.CityActivities.Last(x=>GameSession.OnsiteNumber(x.DefinitionId)==n);
        }
        private static void Attend(GameSession s,CityActivity a)
        {
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart/1440)),(int)(a.ScheduledStart%1440));s.Player.Health=s.Player.Hunger=s.Player.Energy=100;At(s,a.VenueId);Assert.IsTrue(s.PrepareVenueCompanions(s.CurrentPlace));s.AdvanceClock(3);Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);Assert.NotNull(s.CurrentOnsite);
        }
        private static void Finish(GameSession s,CityActivity a)=>s.AdvanceClock((int)Math.Max(1,a.ActualStart+a.Duration-s.Clock.Now.TotalMinutes));
        private static void Shot(GameSession s,int mode,double power=.615,double angle=0)
        {
            Assert.IsTrue(s.SelectOnsiteSportMode(mode));Assert.IsTrue(s.AimOnsiteSport(angle));Assert.IsTrue(s.BeginOnsiteCharge());for(int i=0;i<(int)Math.Round(power*1000);i++)s.StepOnsiteSport(.001);Assert.IsTrue(s.ReleaseOnsiteShot());for(int i=0;i<3100;i++)s.StepOnsiteSport(.001);Assert.AreEqual("result",s.CurrentOnsite.Phase);
        }
        [TestCase(51)] [TestCase(52)] [TestCase(53)] [TestCase(54)] [TestCase(55)] [TestCase(56)] [TestCase(57)] [TestCase(58)] [TestCase(76)]
        public void InvitationsNeedRealMeetingAndDoNotRewardOrOfferOrdersOnBooking(int n)
        {
            using var s=Make();var cash=s.Ledger.Balance;int orders=s.CityReferrals.Count;var a=Book(s,n);Assert.IsNull(s.CurrentOnsite);Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(orders,s.CityReferrals.Count);Attend(s,a);Assert.AreEqual(n,s.CurrentOnsite.Number);
        }
        [Test] public void StandingStillDoesNotCompleteTheMissingOnsiteTask()
        {using var s=Make();var a=Book(s,51);Attend(s,a);Finish(s,a);Assert.AreEqual(CityActivityPhase.Interrupted,a.State);Assert.IsFalse(a.RewardApplied);}
        [Test] public void DrinksTakeRealTimeNeedTwoDifferentTastesAndFeedback()
        {
            using var s=Make();var a=Book(s,51);Attend(s,a);Assert.IsFalse(s.OnsiteDrinkFeedback(0));Assert.IsTrue(s.SipOnsiteDrink());s.AdvanceClock(2);Assert.AreEqual(0,s.CurrentOnsite.Round);s.AdvanceClock(1);Assert.AreEqual(1,s.CurrentOnsite.Round);Assert.IsFalse(s.SipOnsiteDrink());
            s.SelectOnsiteDrink(1);Assert.IsTrue(s.SipOnsiteDrink());s.AdvanceClock(3);Assert.IsTrue(s.OnsiteDrinkFeedback(1));Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [TestCase(0,600)] [TestCase(1,2400)] [TestCase(2,0)] [TestCase(3,1800)]
        public void GroupPaymentUsesActualFourPersonBillAndCorrectHost(int choice,int fee)
        {
            using var s=Make();var a=Book(s,52,choice);var cash=s.Ledger.Balance;Attend(s,a);Assert.AreEqual(3,a.Participants.Length);Assert.AreEqual(cash-Money.FromYuan(fee),s.Ledger.Balance);Assert.AreEqual(choice==2,a.HostPays);Assert.AreEqual(choice==1,a.PlayerTreat);
        }
        [Test] public void GroupTopicsBindTheActualPersonAndCannotBeSpammed()
        {
            using var s=Make();var a=Book(s,52,2);Attend(s,a);string person=a.Participants[0].PersonId;int before=s.Relation(person);Assert.IsTrue(s.TalkOnsiteGuest(0,0));s.AdvanceClock(3);Assert.AreEqual(before+3,s.Relation(person));Assert.IsFalse(s.TalkOnsiteGuest(0,2));
            for(int i=1;i<3;i++){Assert.IsTrue(s.TalkOnsiteGuest(i,i));s.AdvanceClock(3);}Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [TestCase(1,0,0,true)] [TestCase(0,0,0,false)] [TestCase(1,1,0,false)] [TestCase(1,0,1,false)]
        public void MixingRequiresRealPreparationAndForcedTastingWithWrongRecipePenalty(int basis,int flavor,int sweet,bool correct)
        {
            using var s=Make();var a=Book(s,53);Attend(s,a);s.SetOnsiteRecipe(0,basis);s.SetOnsiteRecipe(1,flavor);s.SetOnsiteRecipe(2,sweet);Assert.IsTrue(s.MixOnsiteDrink());Assert.IsFalse(s.TasteOnsiteMix());s.AdvanceClock(5);Assert.AreEqual("mixed",s.CurrentOnsite.Phase);Assert.IsFalse(s.MixOnsiteDrink());
            s.Player.Health=80;Assert.IsTrue(s.TasteOnsiteMix());s.AdvanceClock(2);Assert.AreEqual(correct?1:0,s.CurrentOnsite.Success);Assert.AreEqual(1,s.CurrentOnsite.Attempts);Assert.IsFalse(s.TasteOnsiteMix());if(!correct)Assert.Less(s.Player.Health,79);Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [Test] public void MixedDrinkAndRemainingTasteTimeSurviveJson()
        {
            using var s=Make();var a=Book(s,53);Attend(s,a);s.SetOnsiteRecipe(0,1);s.MixOnsiteDrink();s.AdvanceClock(5);s.TasteOnsiteMix();s.AdvanceClock(1);using var loaded=Reload(s);Assert.AreEqual("taste",loaded.CurrentOnsite.Pending);loaded.AdvanceClock(1);Assert.AreEqual(1,loaded.CurrentOnsite.Success);Assert.IsFalse(loaded.TasteOnsiteMix());
        }
        [Test] public void BasketballUsesActualAimPowerFlightAndDistinctTrainingModes()
        {
            using var s=Make();var a=Book(s,54);Attend(s,a);Shot(s,0);Assert.AreEqual(1,s.CurrentOnsite.Success);Shot(s,0,.2,30);Assert.AreEqual(1,s.CurrentOnsite.Success);Shot(s,1,.65);Assert.AreEqual(3,s.CurrentOnsite.Round);Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [Test] public void NewFloorNeedsAllThreeExercisesAndActualFeedback()
        {
            using var s=Make();var a=Book(s,55);Attend(s,a);Assert.IsFalse(s.OnsiteCourtFeedback(0));Shot(s,0,.55);Shot(s,1,.7);Shot(s,2);Assert.AreEqual(7,s.CurrentOnsite.Mask);Assert.IsTrue(s.OnsiteCourtFeedback(1));Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [Test] public void TeacherGameNeedsFivePhysicalPossessions()
        {using var s=Make();var a=Book(s,56);Attend(s,a);for(int i=0;i<5;i++)Shot(s,i%2);Assert.AreEqual(5,s.CurrentOnsite.Round);Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);}
        [Test] public void GolfClubPowerAndAngleChangeRealLandingAndFiveShotsComplete()
        {
            using var s=Make();var a=Book(s,57);Attend(s,a);Shot(s,0,.5);double iron=s.CurrentOnsite.LandingZ;Shot(s,1,.5);Assert.Greater(s.CurrentOnsite.LandingZ,iron+15);Shot(s,2,.8,15);Assert.Greater(s.CurrentOnsite.LandingX,0);Shot(s,0,.7);Shot(s,1,.7);Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [Test] public void AFlightCanBeSavedAndResumedWithoutRerollingItsLanding()
        {
            using var s=Make();var a=Book(s,57);Attend(s,a);s.BeginOnsiteCharge();for(int i=0;i<60;i++)s.StepOnsiteSport(.01);s.ReleaseOnsiteShot();s.StepOnsiteSport(.1);var expected=s.CurrentOnsite.LandingZ;using var loaded=Reload(s);Assert.AreEqual(expected,loaded.CurrentOnsite.LandingZ,1e-9);for(int i=0;i<300;i++)loaded.StepOnsiteSport(.01);Assert.AreEqual(1,loaded.CurrentOnsite.Round);Assert.AreEqual(expected,loaded.CurrentOnsite.LandingZ,1e-9);
        }
        [Test] public void LowIntelligenceCanNegotiateButFailsAndNeverCreatesAnInstantOrder()
        {
            using var s=Make();var a=Book(s,58);Attend(s,a);Assert.IsFalse(s.DecideOnsiteProcurement(0));for(int i=0;i<3;i++)Shot(s,0);int before=s.CityReferrals.Count;s.Player.Intelligence=49;s.Meet("life_enterprise",100);Assert.IsTrue(s.DecideOnsiteProcurement(0));Assert.AreEqual(before,s.CityReferrals.Count);Assert.IsFalse(s.DecideOnsiteProcurement(1));Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
        }
        [Test] public void SuccessfulProcurementCreatesOneRealOfferOnlyAfterShotsAndDiscussion()
        {
            using var s=Make();var a=Book(s,58);Attend(s,a);for(int i=0;i<3;i++)Shot(s,0);int before=s.CityReferrals.Count;s.Player.Intelligence=100;s.Meet("life_enterprise",100);Assert.IsTrue(s.DecideOnsiteProcurement(1));Assert.AreEqual(before+1,s.CityReferrals.Count);Assert.IsFalse(s.DecideOnsiteProcurement(1));using var loaded=Reload(s);Assert.AreEqual(before+1,loaded.CityReferrals.Count);Assert.IsFalse(loaded.DecideOnsiteProcurement(0));
        }
        [Test] public void PhotoInvitationUsesRealExhibitionDatesAndRequiresSavedDifferentSubjects()
        {
            using var s=Make();var a=Book(s,76);Assert.That((int)(a.ScheduledStart/1440)%30+1,Is.InRange(22,24));Attend(s,a);Assert.IsFalse(s.RecordOnsitePhoto("../escape.png",1600,900));
            Assert.IsTrue(s.SetOnsitePhotoFrame(0,20,30,4,0,1));Assert.IsTrue(s.RecordOnsitePhoto("frame-1.png",1600,900));Assert.IsFalse(s.RecordOnsitePhoto("frame-1.png",1600,900));
            s.SetOnsitePhotoFrame(1,35,30,4,0,1);Assert.IsTrue(s.RecordOnsitePhoto("frame-2.png",1600,900));s.SetOnsitePhotoFrame(2,0,75,1,2,0);Assert.IsTrue(s.RecordOnsitePhoto("frame-3.png",1600,900));Assert.AreEqual(2,s.CurrentOnsite.GoodPhotos);Finish(s,a);Assert.AreEqual(CityActivityPhase.Completed,a.State);
            using var loaded=Reload(s);Assert.AreEqual(3,loaded.OnsitePhotos.Length);Assert.AreEqual("frame-2.png",loaded.OnsitePhotos[1].File);
        }
    }
}
