using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class ClassmateDinnerTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,1234,Money.FromYuan(100000),GameTimestamp.From(new GameDate(5),600),true);
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.Time.SetSpeed(8);
            return s;
        }
        private static CityActivity Accept(GameSession s)
        {
            var def=EventCatalog.All.Single(e=>e.Id=="friend_meal");
            StringAssert.Contains(s.CityPerson("res_coder").Name,def.Body);
            def.Choices[0].Apply(s);
            return s.CityActivities.Single(a=>a.SourceId.StartsWith("legacy-friend-meal:"));
        }
        private static void Step(GameSession s)
        {
            s.Player.Hunger=100;s.Player.Energy=100;s.Tick(.05);
        }
        [Test]
        public void InvitationNamesRealClassmateAndCancellationDoesNotGrantContact()
        {
            using(var s=Make())
            {
                Assert.NotNull(s.CityPersonState("res_coder"));Assert.False(s.IsMet("res_coder"));
                var cash=s.Ledger.Balance;var a=Accept(s);
                Assert.AreEqual("res_coder",a.HostId);
                Assert.AreEqual("res_coder",a.Participants.Single().PersonId);
                Assert.False(s.IsMet("res_coder"));Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.True(s.CancelCityActivity(a.Id));Assert.False(s.IsMet("res_coder"));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    Assert.False(loaded.IsMet("res_coder"));
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ContactRequiresCompletedRealDinnerAndPersistsAcrossReload(bool interrupt)
        {
            using(var s=Make())
            {
                var a = Accept(s);
                var venue = s.City.GetPlace(a.VenueId);
                long departure = a.ScheduledStart - s.CityTravelMinutes(venue) - GameSession.CityArrivalWindowMinutes;
                for (int i = 0; i < 60000 && s.Clock.Now.TotalMinutes < departure && a.Active; i++) Step(s);
                Assert.True(s.GoToCityActivity(a.Id));
                for(int i=0;i<60000&&a.Active&&!a.ServiceConfirmed;i++)
                {
                    Step(s);
                    if(s.CurrentPlace?.Id==a.VenueId&&s.VenueServiceBlocker("dinner",0)==null)
                        Assert.True(s.SelectVenueService("dinner",0));
                }
                for(int i=0;i<1000&&a.Active&&a.State!=CityActivityPhase.Running;i++)Step(s);
                Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);
                Assert.AreEqual(a.VenueId,s.CityPersonState("res_coder").LocationPlaceId);
                Assert.False(s.IsMet("res_coder"),"实际到场、开始聚餐时尚未互留联系方式");
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled=false;loaded.Time.SetSpeed(8);
                    var meal=loaded.CityActivities.Single(x=>x.Id==a.Id);
                    Assert.AreEqual("res_coder",meal.HostId);
                    if(interrupt)loaded.Leave();
                    for(int i=0;i<60000&&meal.Active;i++)Step(loaded);
                    Assert.AreEqual(interrupt?CityActivityPhase.Interrupted:CityActivityPhase.Completed,meal.State,meal.EndReason);
                    Assert.AreEqual(!interrupt,loaded.IsMet("res_coder"));
                    if(!interrupt)
                    {
                        Assert.True(loaded.SavedConversationContacts().Any(c=>c.Id=="res_coder"));
                        StringAssert.Contains("已互留联系方式",meal.SettlementSummary);
                        int memories=loaded.MemoriesOf("res_coder").Count(m=>m.Text.Contains("留下了联系方式"));
                        for(int i=0;i<30;i++)Step(loaded);
                        Assert.AreEqual(1,memories);
                        Assert.AreEqual(memories,loaded.MemoriesOf("res_coder").Count(m=>m.Text.Contains("留下了联系方式")));
                    }
                    using(var again=GameSession.Load(loaded.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(loaded.CreateSave()))))
                        Assert.AreEqual(!interrupt,again.IsMet("res_coder"));
                }
            }
        }
    }
}
