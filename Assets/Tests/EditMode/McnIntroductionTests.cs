using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class McnIntroductionTests
    {
        private static GameSession Make(int tier=1)
        {
            var s=DistrictProgressionTests.Make();s.DebugPrepareDistrictPrerequisites(1);
            s.PlayerShop.Reputation=40;s.DebugAddCash(100000);
            typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=tier,Chapter=tier});
            At(s,GameSession.CommunityMcnId);return s;
        }
        private static void At(GameSession s,string place)=>typeof(GameSession).GetProperty("CurrentPlace",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,s.City.GetPlace(place));
        private static void Arrive(GameSession s,string person)
        {
            var a=s.McnIntroductions.First(x=>x.PersonId==person);s.Clock.Restore(new GameDate((int)(a.Arrival/1440)),(int)(a.Arrival%1440));
            typeof(GameSession).GetMethod("TickCityPeople",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{s.Clock.Now.TotalMinutes});
        }
        [Test] public void CommunityHasAccessiblePermanentMcnAndCreatorsWorkThere()
        {
            using(var s=Make())
            {var p=s.City.GetPlace(GameSession.CommunityMcnId);Assert.AreEqual(1,s.DistrictTierOf(p));Assert.IsNull(s.DistrictAccessBlocker(p));foreach(var c in Creators.All)Assert.AreEqual(p.Id,CityLifeCatalog.Person(c.Id).WorkId);}
        }
        [Test] public void FoundingIsBlockedInCommunityAndAllowedInPark()
        {
            using(var s=Make()){long cash=s.Ledger.Balance.Cents;Assert.False(s.FoundMcn());Assert.AreEqual(cash,s.Ledger.Balance.Cents);Assert.False(s.HasMcn);}
            using(var s=Make(2)){long cash=s.Ledger.Balance.Cents;Assert.IsNull(s.McnFoundBlocker);Assert.True(s.FoundMcn());Assert.AreEqual(cash-5000000,s.Ledger.Balance.Cents);Assert.False(s.FoundMcn());}
        }
        [Test] public void UnmetCreatorCannotBePaidToBypassIntroduction()
        {
            using(var s=Make())
            {Assert.False(s.IsMet("momo"));long cash=s.Ledger.Balance.Cents;Assert.NotNull(s.CreatorCampaignBlocker(s.Creator("momo")));Assert.False(s.SupportCreator("momo"));Assert.AreEqual(cash,s.Ledger.Balance.Cents);}
        }
        [Test] public void RemoteAndClosedOfficeCannotArrangeIntroduction()
        {
            using(var s=Make())
            {At(s,s.Home.Id);Assert.False(s.RequestMcnIntroduction("momo"));At(s,GameSession.CommunityMcnId);s.Clock.Restore(s.Clock.Today,1200);Assert.False(s.RequestMcnIntroduction("momo"));Assert.Zero(s.McnIntroductions.Length);}
        }
        [Test] public void IntroductionTravelsBeforeContactAndCannotFarmRelationship()
        {
            using(var s=Make())
            {
                var person=s.CityPersonState("momo");var home=s.City.GetPlace(person.HomePlaceId);var position=s.City.ViewPosition(home.NodeId);
                person.LocationPlaceId=home.Id;person.Route=null;person.X=position.X;person.Y=position.Y;
                Assert.True(s.RequestMcnIntroduction("momo"));Assert.False(s.RequestMcnIntroduction("momo"));Assert.False(s.IsMet("momo"));Assert.NotNull(s.CityPersonState("momo").Route);
                Assert.False(s.ExchangeMcnContact("momo"));Arrive(s,"momo");Assert.IsNull(s.McnExchangeBlocker("momo"));Assert.True(s.ExchangeMcnContact("momo"));
                Assert.True(s.IsMet("momo"));Assert.IsNull(s.KolContactBlocker("momo"));Assert.AreEqual(5,s.Creator("momo").Relationship);Assert.AreEqual(0,s.Relation("momo"));
                Assert.False(s.ExchangeMcnContact("momo"));Assert.AreEqual(5,s.Creator("momo").Relationship);
            }
        }
        [Test] public void AppointmentAndContactSurviveJsonSave()
        {
            using(var s=Make())
            {
                Assert.True(s.RequestMcnIntroduction("qiao"));var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,save))
                {Assert.AreEqual(1,loaded.McnIntroductions.Length);Assert.False(loaded.IsMet("qiao"));At(loaded,GameSession.CommunityMcnId);Arrive(loaded,"qiao");Assert.True(loaded.ExchangeMcnContact("qiao"));
                    using(var again=GameSession.Load(s.City,loaded.CreateSave()))Assert.True(again.IsMet("qiao"));}
            }
        }
        [Test] public void ExpiredIntroductionCanBeBookedAgainWithoutInstantContact()
        {
            using(var s=Make())
            {Assert.True(s.RequestMcnIntroduction("momo"));s.Clock.Restore(s.Clock.Today.AddDays(1),600);Assert.False(s.ExchangeMcnContact("momo"));Assert.True(s.RequestMcnIntroduction("momo"));Assert.False(s.IsMet("momo"));}
        }
        [Test] public void ExistingPaidContactsAndMcnAreKeptOnOldSaves()
        {
            using(var s=Make(2))
            {Assert.True(s.FoundMcn());var save=s.CreateSave();save.mcnIntroductions=null;save.districtProgress.UnlockedTier=1;save.business.Creators.First(c=>c.Id=="momo").CampaignUntilDay=10;
                using(var loaded=GameSession.Load(s.City,save)){Assert.True(loaded.HasMcn);Assert.True(loaded.IsMet("momo"));}}
        }
    }
}
