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
        [Test] public void AllAuthoredWorkersRespectClosedVenuesOnInitializationAndDailyTicks()
        {
            using(var s=Make())
            {
                void Check()
                {
                    var employees=s.Employees.Select(e=>s.PersonIdOf(e.Person)).ToArray();
                    foreach(var p in s.CityPeople.Where(p=>p.Task==CityPersonTask.Working&&p.WorkPlaceId!=null
                        &&p.WorkPlaceId!=p.HomePlaceId&&!employees.Contains(p.PersonId)))
                        Assert.True(s.IsPlaceOpen(s.City.GetPlace(p.WorkPlaceId)),p.PersonId+" working in closed "+p.WorkPlaceId+" at "+s.Clock.Now);
                }
                Check();
                foreach(int day in new[]{0,5,6})
                    foreach(int minute in new[]{60,8*60,10*60,19*60,21*60,23*60})
                    {
                        s.Clock.Restore(new GameDate(day),minute);
                        s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                        s.AdvanceClock(1);Check();
                    }
            }
        }

        [TestCase(0,true)] [TestCase(5,false)] [TestCase(6,false)]
        public void WorkAvailabilityUsesWholesaleRestDays(int day,bool expected)
        {
            using(var s=Make())
            {
                var p=s.CityPersonState("zhou");p.WorkPlaceId="stall_cpu";
                var method=typeof(GameSession).GetMethod("AuthoredCityWorkOverlaps",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Assert.AreEqual(expected,method.Invoke(s,new object[]{p,(long)day*1440+600,(long)day*1440+660}));
                Assert.False((bool)method.Invoke(s,new object[]{p,(long)day*1440+21*60,(long)day*1440+22*60}));
            }
        }

        [TestCase(21*60+42)] [TestCase(23*60+42)] [TestCase(60)]
        public void NeighborhoodRestaurantAcceptsAndCompletesLateMeals(int minute)
        {
            using(var s=Make())
            {
                s.Clock.Restore(new GameDate(5),minute);
                var venue=s.City.GetPlace("life_restaurant");
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,venue);
                typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,venue.NodeId);
                Assert.True(s.IsPlaceOpen(venue));Assert.IsNull(s.ClosedReason(venue));
                Assert.AreEqual("24小时",s.OpeningHoursText(venue));
                Assert.IsNull(s.VenueServiceBlocker("dinner",0));
                Assert.True(s.SelectVenueService("dinner",0));
                var activity=s.CityActivities.Last();
                for(int i=0;i<180&&activity.Active;i++)
                {s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;s.AdvanceClock(1);}
                Assert.AreEqual(CityActivityPhase.Completed,activity.State,activity.EndReason);
                Assert.True(activity.RewardApplied);
                Assert.AreEqual(1,s.Ledger.Entries.Count(e=>e.SourceEventId==activity.Id+":entry"));
            }
        }

        [TestCase(5,600)] [TestCase(0,20*60+30)]
        public void ZhouLeavesClosedWholesaleWorkplaceAndDoesNotKeepItAsLeisureTarget(int day,int minute)
        {
            using(var s=Make())
            {
                s.Clock.Restore(new GameDate(day),minute);
                var venue=s.City.GetPlace("stall_cpu");Assert.False(s.IsPlaceOpen(venue));
                var p=s.CityPersonState("zhou");p.Route=null;p.ActivityId=null;p.VisitId=null;
                p.LocationPlaceId=venue.Id;p.WorkPlaceId=venue.Id;p.Task=CityPersonTask.Working;
                p.LeisureTargetId=venue.Id;p.LeisureNextMove=s.Clock.Now.TotalMinutes+120;
                var point=s.StandOf(venue);p.X=point.X;p.Y=point.Y;
                s.AdvanceClock(1);
                Assert.AreNotEqual(CityPersonTask.Working,p.Task);
                Assert.AreNotEqual(venue.Id,p.Route?.TargetPlaceId);
                Assert.AreNotEqual(venue.Id,p.LeisureTargetId);
                Assert.False(s.CityPersonInside(p)&&p.LocationPlaceId==venue.Id);
            }
        }

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
        [Test] public void AppointmentTravelWaitsForTheDepartureWindowWithoutChargingTaxiFare()
        {
            using (var s = Make())
            {
                s.Clock.Restore(new GameDate(6), 21 * 60 + 53);
                Assert.True(s.SetVehicle(VehicleKind.Taxi));
                var venue = s.City.GetPlace("life_restaurant");
                var appointment = GameTimestamp.From(s.Clock.Today.AddDays(1), 18 * 60 + 15);
                var activity = s.ScheduleCityActivity("dinner", System.Array.Empty<string>(), appointment.TotalMinutes);
                Assert.NotNull(activity);
                var departure = appointment.AddMinutes(-s.CityTravelMinutes(venue) - GameSession.CityArrivalWindowMinutes);
                var cash = s.Ledger.Balance;
                var location = s.CurrentPlace;

                string hint = s.CityActivityTravelBlocker(activity.Id);
                StringAssert.Contains("还早呢", hint);
                StringAssert.Contains(departure.ToString(), hint);
                Assert.False(s.GoToCityActivity(activity.Id));
                Assert.IsNull(s.CityIntent);
                Assert.IsNull(s.Trip);
                Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.AreSame(location, s.CurrentPlace);
                Assert.True(activity.Active);

                var justBefore = departure.AddMinutes(-1);
                s.Clock.Restore(justBefore.Date, justBefore.MinuteOfDay);
                Assert.False(s.GoToCityActivity(activity.Id));
                s.Clock.Restore(departure.Date, departure.MinuteOfDay);
                Assert.IsNull(s.CityActivityTravelBlocker(activity.Id));
                Assert.True(s.GoToCityActivity(activity.Id));
                Assert.AreEqual(activity.Id, s.CityIntent?.ActivityId);
                Assert.NotNull(s.Trip);
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
                for(int i=0;i<16000&&s.CurrentPlace!=venue&&a.Active;i++)Step(s,1);
                Assert.AreEqual(venue,s.CurrentPlace);Assert.False(a.EntryPaid,"Arrival alone must not order a meal");
                Assert.True(s.SelectVenueService("dinner",0),s.VenueServiceBlocker("dinner",0));
                for(int i=0;i<16000&&a.State!=CityActivityPhase.Running&&a.Active;i++)Step(s,1);
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
                    for(int i=0;i<16000&&loaded.CurrentPlace?.Id!=venue.Id&&a.Active;i++)Step(loaded,1);
                    Assert.AreEqual(venue.Id,loaded.CurrentPlace?.Id);
                    Assert.True(loaded.SelectVenueService("dinner",0),loaded.VenueServiceBlocker("dinner",0));
                    for(int i=0;i<16000&&a.State!=CityActivityPhase.Running&&a.Active;i++)Step(loaded,1);
                    Assert.AreEqual(CityActivityPhase.Running,a.State,a.EndReason);
                    Assert.AreEqual(venue.Id,loaded.CurrentPlace.Id);Assert.NotNull(a.PlayerVenueRoute);
                    var entrance=loaded.StandOf(loaded.City.GetPlace(venue.Id));
                    double dx=loaded.PlayerPosition.X-entrance.X,dy=loaded.PlayerPosition.Y-entrance.Y;
                    Assert.Less(dx*dx+dy*dy,100,"The restored actor remains at the actual restaurant in the current map, not the origin");
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
