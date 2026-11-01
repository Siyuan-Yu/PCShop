using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityActivityTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,1234,Money.FromYuan(100000),GameTimestamp.From(new GameDate(5),600),true);
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.Time.SetSpeed(8);
            return s;
        }
        private static void Step(GameSession s,int frames)
        {
            for(int i=0;i<frames;i++){s.Player.Hunger=100;s.Player.Energy=100;s.Tick(.025);}
        }
        [Test] public void FutureAcceptanceAndCancellationNeverChargeOrGrantCompletion()
        {
            using(var s=Make())
            {
                var cash=s.Ledger.Balance;var now=s.Clock.Now;
                var a=s.ScheduleCityActivity("dinner",new string[0],now.TotalMinutes+180);
                Assert.NotNull(a);Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(now,s.Clock.Now);
                Assert.True(s.CancelCityActivity(a.Id));Assert.False(s.CancelCityActivity(a.Id));
                Assert.False(a.EntryPaid);Assert.False(a.RewardApplied);Assert.AreEqual(cash,s.Ledger.Balance);
            }
        }
        [Test] public void RealTravelMealAndReloadSettleEachStageOnce()
        {
            using(var s=Make())
            {
                var venue=s.City.GetPlace("life_restaurant");var initial=s.CurrentPlace;
                long start=s.Clock.Now.TotalMinutes+s.CityTravelMinutes(venue)+10;
                var a=s.ScheduleCityActivity("dinner",new string[0],start);Assert.NotNull(a);
                Assert.True(s.GoToCityActivity(a.Id));Assert.AreNotEqual(venue,s.CurrentPlace);
                Assert.False(s.CityPlayerCanWork(s.MainShop));
                for(int i=0;i<16000&&a.State!=CityActivityPhase.Running;i++)Step(s,1);
                Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);Assert.AreEqual(venue,s.CurrentPlace);
                Assert.True(a.EntryPaid);var cash=s.Ledger.Balance;
                Step(s,30);Assert.True(s.ChooseCityActivity(a.Id,0));
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var restored=GameSession.Load(s.City,save))
                {
                    restored.EventsEnabled=false;restored.Time.SetSpeed(8);
                    var loaded=restored.CityActivities.Single(x=>x.Id==a.Id);
                    Assert.True(loaded.EntryPaid);Assert.AreEqual(s.Ledger.Balance,restored.Ledger.Balance);
                    Assert.True(restored.ChooseCityActivity(loaded.Id,0));
                    for(int i=0;i<12000&&loaded.Active;i++)Step(restored,1);
                    Assert.AreEqual(CityActivityPhase.Completed,loaded.State,loaded.EndReason);
                    Assert.AreEqual(venue.Id,restored.CurrentPlace.Id);
                    Assert.AreEqual(cash,restored.Ledger.Balance,"Only the one entry payment is due for this meal");
                    Assert.AreEqual(1,restored.Ledger.Entries.Count(e=>e.SourceEventId==a.Id+":entry"));
                    Assert.False(restored.CancelCityActivity(loaded.Id));
                }
            }
        }
        [Test] public void FutureAppointmentWithEmptyJsonSeatRouteStillWalksToTheVenueAfterLoad()
        {
            using(var s=Make())
            {
                var venue=s.City.GetPlace("life_restaurant");
                var activity=s.ScheduleCityActivity("dinner",new string[0],s.Clock.Now.TotalMinutes+s.CityTravelMinutes(venue)+20);
                Assert.NotNull(activity);
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                save.cityActivities.Single(x=>x.Id==activity.Id).PlayerVenueRoute=new CityRoute();
                using(var loaded=GameSession.Load(s.City,save))
                {
                    loaded.EventsEnabled=false;loaded.Time.SetSpeed(8);loaded.MainShop.Sim.Settings.BaseCustomersPerHour=0;
                    var a=loaded.CityActivities.Single(x=>x.Id==activity.Id);
                    Assert.IsNull(a.PlayerVenueRoute);Assert.True(loaded.GoToCityActivity(a.Id));
                    for(int i=0;i<16000&&a.State!=CityActivityPhase.Running&&a.Active;i++)Step(loaded,1);
                    Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);
                    Assert.AreEqual(venue.Id,loaded.CurrentPlace.Id);Assert.NotNull(a.PlayerVenueRoute);
                    Assert.Greater(loaded.PlayerPosition.Y,40,"The restored actor remains at the real restaurant, not the origin");
                }
            }
        }
        [Test] public void TenStreetNetworkHasRoutesToEveryConfiguredEntrance()
        {
            using(var s=Make())
            {
                Assert.GreaterOrEqual(s.City.Streets.Count,10);
                foreach(var place in s.City.Places)Assert.GreaterOrEqual(s.CityTravelMinutes(place),0,place.Id);
                Assert.GreaterOrEqual(s.CityCommercialProperties.Count(),20);
                Assert.GreaterOrEqual(s.CityPeople.Count(),40);
                Assert.AreEqual(s.CityPeople.Count(),s.CityPeople.Select(p=>p.PersonId).Distinct().Count());
            }
        }
    }
}
