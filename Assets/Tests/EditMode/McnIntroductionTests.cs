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
            {Assert.False(s.IsMet("momo"));long cash=s.Ledger.Balance.Cents;Assert.False(s.SignKolAmbassador("momo", 90, 1, remote: true, phoneCall: true));Assert.False(s.SupportCreator("momo"));Assert.AreEqual(cash,s.Ledger.Balance.Cents);}
        }
        [Test] public void RemoteAndClosedOfficeCannotArrangeIntroduction()
        {
            using(var s=Make())
            {At(s,s.Home.Id);Assert.False(s.RequestMcnIntroduction("momo"));At(s,GameSession.CommunityMcnId);s.Clock.Restore(s.Clock.Today,1200);Assert.False(s.RequestMcnIntroduction("momo"));Assert.Zero(s.McnIntroductions.Length);}
        }
        [Test] public void PaidIntroductionGrantsContactImmediatelyAndOnlyChargesOnce()
        {
            using (var s = Make())
            {
                var person = s.CityPersonState("momo");
                person.LocationPlaceId = person.HomePlaceId;
                person.Route = null;
                long cash = s.Ledger.Balance.Cents;
                Assert.True(s.RequestMcnIntroduction("momo"));
                Assert.True(s.IsMet("momo"));
                Assert.Contains("momo", s.SavedConversationContacts().Select(c => c.Id).ToArray());
                Assert.IsNull(s.RemoteConversationBlocker("momo", phoneCall: true));
                Assert.AreEqual(cash - s.McnIntroductionFee("momo") * 100, s.Ledger.Balance.Cents);
                Assert.False(s.RequestMcnIntroduction("momo"));
                Assert.AreEqual(cash - s.McnIntroductionFee("momo") * 100, s.Ledger.Balance.Cents);
                Assert.AreEqual(person.HomePlaceId, person.LocationPlaceId);
                Assert.AreEqual(0, s.Relation("momo"));
                Assert.AreEqual(5, s.Creator("momo").Relationship);
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                    Assert.True(loaded.IsMet("momo"));
            }
        }

        [Test] public void PriorBookedIntroductionCanStillExchangeWithoutCharging()
        {
            using (var s = Make())
            {
                var save = s.CreateSave();
                long now = s.Clock.Now.TotalMinutes;
                save.mcnIntroductions = new[] { new McnIntroduction { PersonId = "qiao", Arrival = now, Until = now + 120 } };
                using (var loaded = GameSession.Load(s.City, save))
                {
                    At(loaded, GameSession.CommunityMcnId);
                    var person = loaded.CityPersonState("qiao");
                    person.Route = null;
                    person.LocationPlaceId = GameSession.CommunityMcnId;
                    long cash = loaded.Ledger.Balance.Cents;
                    Assert.True(loaded.ExchangeMcnContact("qiao"));
                    Assert.True(loaded.IsMet("qiao"));
                    Assert.AreEqual(cash, loaded.Ledger.Balance.Cents);
                }
            }
        }

        [Test] public void ExistingPaidContactsAndMcnAreKeptOnOldSaves()
        {
            using(var s=Make(2))
            {Assert.True(s.FoundMcn());var save=s.CreateSave();save.mcnIntroductions=null;save.districtProgress.UnlockedTier=1;save.business.Creators.First(c=>c.Id=="momo").CampaignUntilDay=10;
                using(var loaded=GameSession.Load(s.City,save)){Assert.True(loaded.HasMcn);Assert.True(loaded.IsMet("momo"));}}
        }
    }
}
