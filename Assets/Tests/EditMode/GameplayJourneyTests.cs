using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    // 准备已解锁片区和资金；见面、预约、交割、调岗、招聘和存读档走正式逻辑。
    public class GameplayJourneyTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static GameSession Ready()
        {
            var s = DistrictProgressionTests.Make();
            s.DebugAddCash(200000000); s.SetSmartphone(true); s.DebugRentStarterShop(true);
            ((DistrictProgress)typeof(GameSession).GetField("_district", F).GetValue(s)).UnlockedTier = 3;
            foreach (var lesson in new[]{"first_meal","rent","mall","place","shelf","stock","sale","hire"}) s.Flags.Add("tutorial:"+lesson);
            foreach(var shop in s.MyShops) shop.Sim.Settings.BaseCustomersPerHour=0;
            s.Clock.Restore(new GameDate(0),600);
            return s;
        }
        static GameSession Reload(GameSession s) => GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        static void At(GameSession s,string place)
        {s.StopTrip();s.Leave();s.ArriveAtNode(s.City.GetPlace(place).NodeId);s.Enter(s.City.GetPlace(place));Assert.AreEqual(place,s.CurrentPlace?.Id);}
        static void Meet(GameSession s,string id)
        {
            var p=s.CityPersonState(id);Assert.NotNull(p,id);
            p.Route=null;p.LocationPlaceId=s.Home.Id;At(s,s.Home.Id);s.Meet(id,80);
            Assert.True(s.CitySamePlace(id),id);
        }
        static void Advance(GameSession s,long until)
        {
            while(s.Clock.Now.TotalMinutes<until)
            {
                s.Player.Hunger=s.Player.Energy=s.Player.Health=100;
                long before=s.Clock.Now.TotalMinutes;s.AdvanceClock((int)Math.Min(30,until-before));
                Assert.Greater(s.Clock.Now.TotalMinutes,before,"时钟被活动阻止");
            }
        }
        [TestCase("life_ming")][TestCase("lin")][TestCase("life_singer")]
        [TestCase("life_ning")][TestCase("momo")][TestCase("life_curator")]
        [TestCase("life_creator")][TestCase("life_designer")][TestCase("resident_zhaoyu")]
        [TestCase("resident_dengyue")][TestCase("resident_zengyu")][TestCase("resident_jiyun")]
        public void EveryStoryCanScheduleItsActualVenueAndKeepAppointmentAfterReload(string person)
        {
            using var s=Ready();Meet(s,person);s.CasualConversation(person);
            Assert.True(s.HasStartedRelationshipStory(person));
            s.Clock.Restore(new GameDate(1),600);Meet(s,person);
            var invite=s.PrepareRelationshipStoryInvitation(person,false);
            Assert.Greater(invite.start,s.Clock.Now.TotalMinutes,invite.text);
            string reply=s.AcceptRelationshipStoryInvitation(person,invite.start,false);
            string id=s.RelationshipStoryAppointmentId(person);Assert.NotNull(id,reply);
            var appointment=s.CityActivities.Single(a=>a.Id==id);
            Assert.True(s.City.HasPlace(appointment.VenueId));
            using var loaded=Reload(s);Meet(loaded,person);
            Assert.AreEqual(id,loaded.RelationshipStoryAppointmentId(person));
            Assert.AreEqual(RelationshipStoryStep.Appointment,loaded.GetRelationshipStoryStep(person));
            Assert.NotNull(loaded.ConfessionBlocker(person),"预约并不等于完成赴约");
        }
        [TestCase("lin",true)][TestCase("momo",false)][TestCase("resident_jiyun",true)]
        [TestCase("life_designer",true)]
        [TestCase("life_creator",true)]
        [TestCase("life_ming",true)][TestCase("life_singer",true)][TestCase("life_ning",false)]
        [TestCase("life_curator",true)][TestCase("resident_zhaoyu",false)]
        [TestCase("resident_dengyue",true)][TestCase("resident_zengyu",false)]
        public void ScheduledOutingActuallyStartsCompletesAndUnlocksRelationshipChoice(string person,bool romantic)
        {
            using var s=Ready();Meet(s,person);s.CasualConversation(person);
            s.Clock.Restore(new GameDate(1),600);Meet(s,person);
            var invite=s.PrepareRelationshipStoryInvitation(person,false);
            Assert.Greater(invite.start,0,invite.text);s.AcceptRelationshipStoryInvitation(person,invite.start,false);
            string id=s.RelationshipStoryAppointmentId(person);Assert.NotNull(id);
            using var loaded=Reload(s);
            var activity=loaded.CityActivities.Single(a=>a.Id==id);
            Advance(loaded,invite.start);At(loaded,activity.VenueId);
            Assert.True(loaded.SelectVenueService(activity.DefinitionId,0,id),loaded.VenueServiceBlocker(activity.DefinitionId,0,id));
            Assert.AreEqual(CityActivityPhase.Running,activity.State,activity.EndReason);
            Assert.IsNotEmpty(loaded.ReplyInRelationshipOuting(activity,person,0));
            if (GameSession.IsInteractiveFishingActivity(activity))
            {
                Assert.True(loaded.BeginFishingCast(),loaded.FishingCastBlocker);
                Assert.True(loaded.ReleaseFishingCast());
                Assert.True(loaded.CancelCityActivity(id));
            }
            else if (activity.DefinitionId.StartsWith("ktv",StringComparison.Ordinal))
                Assert.True(loaded.AdvanceKaraokePlayback(id,1));
            else if (activity.DefinitionId=="oldmarket_visit")
            {
                Assert.True(loaded.BuyMarketHuntPass(0,1),loaded.MarketHuntPassBlocker(0,1));
                Assert.True(loaded.BeginMarketHunt("watch-box"));
                Assert.True(loaded.GiftMarketFindToCompanion(person));
            }
            else Assert.True(loaded.FinishManualCityActivity(id));
            Assert.AreEqual(CityActivityPhase.Completed,activity.State,activity.EndReason);
            loaded.Clock.Restore(loaded.Clock.Today.AddDays(1),600);Meet(loaded,person);
            Assert.AreEqual(RelationshipStoryStep.Decide,loaded.GetRelationshipStoryStep(person));
            loaded.ResolveRelationshipStory(person,romantic);
            Assert.AreEqual(romantic?person:null,loaded.RomanceId);
            using var final=Reload(loaded);Assert.AreEqual(loaded.RomanceId,final.RomanceId);
            Assert.NotNull(final.RelationshipIllustrationTitle(person), "专属赴约 CG 应能在读档后回看");
        }

        [TestCase("keeper_agent", "park_walk")]
        [TestCase("keeper_phone", "oldmarket_visit")]
        [TestCase("life_coach", "court")]
        [TestCase("racer_0", "park_walk")]
        [TestCase("ake", "leisure_claw")]
        [TestCase("qiao", "dinner")]
        [TestCase("resident_baiming", "expo_comic")]
        [TestCase("resident_ningxi", "oldmarket_visit")]
        [TestCase("res_coder", "oldmarket_visit")]
        [TestCase("life_golfer", "park_walk")]
        [TestCase("racer_3", "oldmarket_visit")]
        [TestCase("resident_luoya", "ktv")]
        public void FamiliarFriendHasPersonalMomentAtActualVenueAndKeepsChoiceAcrossSave(string person, string definitionId)
        {
            using var s = Ready();
            Meet(s, person);
            Assert.IsNotEmpty(GameSession.PersonalInterest(person));
            string interest = s.CityTalk(person, "interest");
            StringAssert.DoesNotContain("有空的时候，也想给自己放个假", interest);
            Assert.IsTrue(GameSession.LikesPersonalActivity(person, definitionId));
            long now = s.Clock.Now.TotalMinutes;
            long start = s.SuggestCityActivityStart(definitionId, person);
            Assert.Greater(start, now, "实际排班中应有可以赴约的时段");
            Assert.IsNull(s.CityActivityInvitationBlocker(definitionId, person, start, false));
            var activity = s.ScheduleCityActivityInvitation(definitionId, person, start, false);
            Assert.NotNull(activity);
            if (start - now > 1440)
                s.Clock.Restore(new GameDate((int)(start / 1440)), Math.Max(0, (int)(start % 1440) - 180));
            Advance(s, start);
            At(s, activity.VenueId);
            Assert.IsTrue(s.SelectVenueService(activity.DefinitionId, 0, activity.Id), s.VenueServiceBlocker(definitionId, 0, activity.Id));
            Assert.AreEqual(CityActivityPhase.Running, activity.State);
            var moment = s.PersonalMoment(activity);
            Assert.AreEqual(person, moment.PersonId);
            Assert.False(moment.Illustrated, "好友版不会提前展示交往 CG");
            Assert.IsNull(s.ReplyInPersonalMoment(activity, person, 2));
            var existingMemories = s.MemoriesOf(person).ToHashSet();
            Assert.IsNotEmpty(s.ReplyInPersonalMoment(activity, person, 1));
            Assert.IsNull(s.ReplyInPersonalMoment(activity, person, 0), "同一次相处只能选择一个回答");
            Assert.IsNull(s.RelationshipIllustrationTitle(person));
            string answer = s.MemoriesOf(person).Single(memory => !existingMemories.Contains(memory)).Text;
            using var loaded = Reload(s);
            Assert.IsTrue(loaded.MemoriesOf(person).Any(memory => memory.Text == answer),
                $"读档前后时间 {s.Clock.Now.TotalMinutes}/{loaded.Clock.Now.TotalMinutes}，应保留「{answer}」，当前记录："
                + string.Join("／", loaded.MemoriesOf(person).Select(memory => memory.Text)));
            Assert.IsNull(loaded.PersonalMoment(loaded.RunningCityActivity).PersonId, "读档不会让已读片段再次出现");
        }

        [Test]
        public void RelationshipSceneAnswersDifferAfterConfessionAndLeadToAnAlbumMemory()
        {
            const string person = "keeper_phone";
            const string definition = "oldmarket_visit";
            using var s = Ready();
            Meet(s, person);
            var friendOptions = s.PersonalMomentOptions(person);
            var dateOptions = s.PersonalMomentOptions(person, together: true);
            Assert.AreEqual(2, dateOptions.Distinct().Count());
            CollectionAssert.IsEmpty(friendOptions.Intersect(dateOptions));
            s.Random.State = Enumerable.Range(1, 100).Select(seed => (ulong)seed)
                .First(seed => new GameRandom(seed).Chance(GameSession.ConfessionChance(s.Relation(person))));
            StringAssert.Contains("交往", s.ConfessToPerson(person));
            long start = s.SuggestCityActivityStart(definition, person);
            Assert.IsNull(s.CityActivityInvitationBlocker(definition, person, start, false));
            var date = s.ScheduleCityActivityInvitation(definition, person, start, false);
            Assert.NotNull(date);
            s.Clock.Restore(new GameDate((int)(start / 1440)), Math.Max(0, (int)(start % 1440) - 180));
            Advance(s, start);
            At(s, date.VenueId);
            Assert.IsTrue(s.SelectVenueService(definition, 0, date.Id));
            Assert.IsTrue(s.PersonalMoment(date).Illustrated);
            StringAssert.Contains("缝歪了", s.ReplyInPersonalMoment(date, person, 0));
            StringAssert.Contains("那一针是你缝的", s.InteractWithActivityPartner(date.Id, holdHands: false));
            Assert.IsTrue(s.FinishManualCityActivity(date.Id));
            using var loaded = Reload(s);
            Assert.AreEqual("旧兔子的补丁", loaded.RelationshipIllustrationTitle(person));
        }
        [Test] public void CompanyDepartmentRecruitmentAndDismissalSurviveRealMapAndReload()
        {
            using var s=Ready();
            // 创立资格已有专项测试；此处以已注册公司的存档阶段开始。
            Assert.True((bool)typeof(GameSession).GetMethod("CreateBrandAndCompany",F).Invoke(s,new object[]{"computer","流程测试公司","value",new[]{s.MainShop.Place.Id}}));
            var company=s.Companies.Single();
            var employee=new Employee{Id="journey_leader",Name="流程员工",ShiftStart=540,ShiftEnd=1020,DailyWageYuan=180};
            s.MainShop.Employee=employee;
            Assert.True(s.ScheduleCompanyPost(company.BrandId,employee.Id,CompanyDepartment.Personnel,true,true));
            long due=company.PendingPosts.Single().EffectiveAt;
            using var loaded=Reload(s);Advance(loaded,due);
            company=loaded.Company(company.BrandId);
            Assert.AreEqual(employee.Id,loaded.CompanyDepartmentLeader(company,CompanyDepartment.Personnel));
            Assert.IsNull(loaded.FindEmployee(employee.Id).Shop);
            string otherShop = loaded.City.Places.First(p => p.Id.StartsWith("expansion_shop_")
                && loaded.DistrictTierOf(p)==0 && p.Id!=loaded.MainShop.Place.Id).Id;
            var agency = loaded.City.Places.First(p => p.Kind==PCShopSell.Core.World.PlaceKind.Agency
                && loaded.DistrictTierOf(p)==0);
            At(loaded,agency.Id);loaded.PlayerShop.Reputation=100;
            loaded.DebugAddListing(otherShop,0,100,Money.FromYuan(1000));
            Assert.True(loaded.SignLease(otherShop,true,"computer"));
            company.ShopIds=company.ShopIds.Append(otherShop).ToArray();
            var branch=loaded.ShopAt(otherShop);
            Assert.True(loaded.ApproveCompanyRecruitment(company.BrandId,otherShop),loaded.CompanyRecruitmentBlocker(company,branch));
            using var applicants=Reload(loaded);
            Assert.AreEqual(otherShop,applicants.RecruitmentInterviewPlace?.Id);
            Advance(applicants,GameTimestamp.From(applicants.Clock.Today.AddDays(1),GameSession.CandidateMinute).TotalMinutes);
            At(applicants,applicants.MainShop.Place.Id);Assert.IsNotEmpty(applicants.Candidates);
            Assert.IsNull(applicants.RecruitEmployee(0),"不能在另一间店提前聘用");
            At(applicants,otherShop);Assert.IsTrue(applicants.AtRecruitmentInterview);
            var recruit=applicants.Candidates.First();
            Assert.True(applicants.RecruitAndAssign(recruit.Id,otherShop,EmployeeRole.Cashier));
            Assert.AreEqual(otherShop,applicants.FindEmployee(recruit.Id).Shop.Place.Id);
            Assert.True(applicants.DismissEmployee(recruit.Id));Assert.IsNull(applicants.FindEmployee(recruit.Id));
            Assert.True(applicants.DismissEmployee(employee.Id));
            company=applicants.Company(company.BrandId);
            Assert.IsNull(applicants.CompanyDepartmentLeader(company,CompanyDepartment.Personnel));
            Assert.IsNotNull(applicants.CompanyRecruitmentBlocker(company,applicants.ShopAt(otherShop)));
            using var final=Reload(applicants);Assert.IsNull(final.FindEmployee(employee.Id));Assert.IsNull(final.FindEmployee(recruit.Id));
        }
        [Test] public void RealSharePurchasesLeadToControlHandoverAndOfficeVisit()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");
            long first=g.TotalShares*45/100;
            Assert.True(s.TradeCompanyBlock(g.Ticker,"founder:"+g.Ticker,first,true),s.CompanyBlockTradeBlocker(g.Ticker,"founder:"+g.Ticker,first,true));
            Assert.True(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Director,0),s.CompanyNominationBlocker(g));
            Advance(s,g.Resolutions.Single().DueAt);
            Assert.True(s.CompanyBoardMember(g),"董事提名投票通过后才获得董事权限");
            long second=g.TotalShares/2+1-first;
            Assert.True(s.TradeCompanyBlock(g.Ticker,"capital_qing",second,true));
            Assert.True(s.CompanyBoardMember(g));Assert.AreEqual("player",s.MajorityShareholder(g));
            Assert.True(s.ReorganizeControlledCompany(g.Ticker,"player",1000000),s.CompanyTakeoverBlocker(g));
            using var loaded=Reload(s);var after=loaded.GovernanceOf(g.Ticker);Advance(loaded,after.HandoverAt);
            Assert.AreEqual("player",after.ControllerId);Assert.AreEqual("player",after.ManagerId);
            Assert.Greater(after.LastPlayerHandoverAt,0);
            Assert.Less(after.ReviewedPlayerHandoverAt,after.LastPlayerHandoverAt);
            after.ReviewedPlayerHandoverAt=after.LastPlayerHandoverAt;
            using var reviewed=Reload(loaded);
            Assert.AreEqual(after.LastPlayerHandoverAt,reviewed.GovernanceOf(g.Ticker).ReviewedPlayerHandoverAt);
            Assert.AreEqual(after.TotalShares,after.Holders.Sum(h=>h.Shares));
            foreach(var office in loaded.City.Places.Where(p=>p.Model=="office_tower")) {At(loaded,office.Id);loaded.Leave();Assert.IsNull(loaded.CurrentPlace);}
        }
    }
}
