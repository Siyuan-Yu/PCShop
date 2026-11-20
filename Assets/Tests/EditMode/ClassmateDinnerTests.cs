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
            s.RaiseEvent("friend_meal");
            var def = s.PendingEvent;
            Assert.NotNull(def);
            StringAssert.Contains(s.CityPerson("res_coder").Name,def.Body);
            Assert.IsTrue(s.ResolveEvent(0));
            return s.CityActivities.Single(a=>a.SourceId.StartsWith("friend_meal:"));
        }
        private static void Step(GameSession s)
        {
            s.Player.Hunger=100;s.Player.Energy=100;s.Tick(.05);
        }
        [Test]
        public void InvitationUsesStartingClassmateContactAndCancellationDoesNotCharge()
        {
            using(var s=Make())
            {
                Assert.NotNull(s.CityPersonState("res_coder"));Assert.True(s.IsMet("res_coder"));
                var cash=s.Ledger.Balance;var a=Accept(s);
                Assert.AreEqual("res_coder",a.HostId);
                Assert.AreEqual("res_coder",a.Participants.Single().PersonId);
                Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.True(s.CancelCityActivity(a.Id));Assert.AreEqual(cash,s.Ledger.Balance);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    Assert.True(loaded.IsMet("res_coder"));
            }
        }
        [Test]
        public void ClassmateDinnerCompletesAfterChoosingMealAndKeepsContactAfterReload()
        {
            using(var s=Make())
            {
                var a = Accept(s);
                var venue = s.City.GetPlace(a.VenueId);
                long departure = a.ScheduledStart - s.CityTravelMinutes(venue) - GameSession.CityArrivalWindowMinutes;
                for (int i = 0; i < 60000 && s.Clock.Now.TotalMinutes < departure && a.Active; i++) Step(s);
                Assert.True(s.GoToCityActivity(a.Id));
                for (int i = 0; i < 60000 && s.CurrentPlace != venue && a.Active; i++) Step(s);
                Assert.AreEqual(venue, s.CurrentPlace);
                Assert.False(a.EntryPaid);
                Assert.True(s.SelectVenueService(a.DefinitionId, 1, a.Id), s.VenueServiceBlocker(a.DefinitionId, 1, a.Id));
                Step(s);
                Assert.AreEqual(CityActivityPhase.Running, a.State);
                for(int i=0;i<60000&&a.Active;i++)Step(s);
                Assert.AreEqual(CityActivityPhase.Completed,a.State,a.EndReason);
                Assert.AreEqual(1, a.ServiceChoice);
                Assert.Zero(a.EntryCostCents);
                Assert.IsTrue(a.MealApplied);
                Assert.True(s.IsMet("res_coder"));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled=false;loaded.Time.SetSpeed(8);
                    var meal=loaded.CityActivities.Single(x=>x.Id==a.Id);
                    Assert.AreEqual("res_coder",meal.HostId);
                    Assert.AreEqual(CityActivityPhase.Completed,meal.State,meal.EndReason);
                    Assert.True(loaded.IsMet("res_coder"));
                    Assert.True(loaded.SavedConversationContacts().Any(c=>c.Id=="res_coder"));
                    StringAssert.Contains("已互留联系方式",meal.SettlementSummary);
                    int memories=loaded.MemoriesOf("res_coder").Count(m=>m.Text.Contains("留下了联系方式"));
                    for(int i=0;i<30;i++)Step(loaded);
                    Assert.AreEqual(1,memories);
                    Assert.AreEqual(memories,loaded.MemoriesOf("res_coder").Count(m=>m.Text.Contains("留下了联系方式")));
                    using(var again=GameSession.Load(loaded.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(loaded.CreateSave()))))
                        Assert.True(again.IsMet("res_coder"));
                }
            }
        }
    }
}
