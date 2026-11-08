using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // A geometry migration does not replay time, payments, inventory or activity stages.
        // Walking routes are subsequently recalibrated so new geometry cannot force sprinting.
        private void MigrateCityGeometry(SaveData data)
        {
            if(City.LayoutVersion<7 || data.cityLayoutVersion>=City.LayoutVersion)return;
            long now=Clock.Now.TotalMinutes;
            foreach(var person in _cityPeople.Values)
            {
                person.WorkPlaceId=City.CanonicalPlaceId(person.WorkPlaceId);
                person.HomePlaceId=City.CanonicalPlaceId(person.HomePlaceId);
                person.LocationPlaceId=City.CanonicalPlaceId(person.LocationPlaceId);
                if(person.Route!=null)person.Route.TargetPlaceId=City.CanonicalPlaceId(person.Route.TargetPlaceId);
            }
            for(int i=0;i<_cityFavorites.Count;i++)_cityFavorites[i]=City.CanonicalPlaceId(_cityFavorites[i]);
            var favorites=_cityFavorites.Distinct().ToArray();_cityFavorites.Clear();_cityFavorites.AddRange(favorites);
            CityAgencyShortcut=City.CanonicalPlaceId(CityAgencyShortcut);
            if(CityIntent!=null)CityIntent.TargetPlaceId=City.CanonicalPlaceId(CityIntent.TargetPlaceId);

            if(CurrentPlace?.Id=="home" && HomeTier>0)CurrentPlace=Home;
            if(CurrentPlace!=null)PlayerNodeId=CurrentPlace.NodeId;
            Trip=null;ClearEdge();
            foreach(var person in _cityPeople.Values)
            {
                if(person.Route!=null)
                {
                    RebaseCityRoute(person.Route);
                    var point=person.Route.Position(now);person.X=point.X;person.Y=point.Y;
                }
                else
                {
                    string id=City.HasPlace(person.LocationPlaceId)?person.LocationPlaceId:person.HomePlaceId;
                    if(!City.HasPlace(id))id=Home.Id;
                    var point=StandOf(City.GetPlace(id));person.X=point.X;person.Y=point.Y;person.LocationPlaceId=id;
                }
            }
            foreach(var order in _employeeTrips)
                foreach(var leg in order.TravelLegs??Array.Empty<CityRoute>())RebaseCityRoute(leg);
            foreach(var activity in _cityActivities.Where(a=>a.Active))
            {
                if(!City.HasPlace(activity.VenueId))continue;
                if(activity.PlayerVenueRoute!=null)
                {
                    var point=CityActivityAnchor(City.GetPlace(activity.VenueId),activity.Seat);
                    activity.PlayerVenueRoute.X=new[]{point.X,point.X};activity.PlayerVenueRoute.Y=new[]{point.Y,point.Y};
                    activity.PlayerVenueRoute.Meters=new[]{0d};activity.PlayerVenueRoute.Nodes=Array.Empty<string>();
                }
            }
            if(CurrentPlace==null && data.cityTrip!=null)
            {
                string target=CityIntent!=null && City.HasPlace(CityIntent.TargetPlaceId)?City.GetPlace(CityIntent.TargetPlaceId).NodeId:data.cityTrip.target;
                if(City.Map.HasNode(target))
                {
                    var end=CityIntent!=null&&City.HasPlace(CityIntent.TargetPlaceId)?(MapPoint?)StandOf(City.GetPlace(CityIntent.TargetPlaceId)):null;
                    Trip=PlanTrip(target,end,out _);
                }
            }
            foreach(var shop in _shops)shop.Sim.PlayerPresent=CityPlayerCanWork(shop);
        }
        private static void EnforceNpcWalkingPace(CityRoute route)
        {
            if(route==null||route.Transport!=VehicleKind.Walk||!route.HasPath)return;
            int minutes=CityTravelTiming.NpcMinutes(route.Meters.Sum(),Vehicles.Walk);
            route.ArriveMinute=Math.Max(route.ArriveMinute,route.DepartMinute+minutes);
        }
        private void NormalizeCityWalkingRoutes()
        {
            foreach(var activity in _cityActivities.Where(a=>a.Active))
            {
                var route=activity.PlayerVenueRoute;
                if(route!=null&&route.HasPath&&route.Transport==VehicleKind.Walk)
                    route.ArriveMinute=Math.Max(route.ArriveMinute,route.DepartMinute+CityTravelTiming.PlayerMinutes(route.Meters.Sum(),Vehicles.Walk));
            }
            foreach(var trip in _employeeTrips.Where(t=>!t.Returned))
                NormalizePurchaseWalkingLegs(trip,trip.TravelLegs);
            foreach(var person in _cityPeople.Values)
            {
                EnforceNpcWalkingPace(person.Route);
                if(person.Route==null)continue;
                var point=person.Route.Position(Clock.Now.TotalMinutes+Time.FractionalMinute);
                person.X=point.X;person.Y=point.Y;
            }
        }
        private void RebaseCityRoute(CityRoute route)
        {
            if(route==null||!City.HasPlace(route.TargetPlaceId))return;
            var target=City.GetPlace(route.TargetPlaceId);
            string from=(route.Nodes??Array.Empty<string>()).FirstOrDefault(City.Map.HasNode)??target.NodeId;
            var path=Pathfinder.FindPath(City.Map,from,target.NodeId);
            if(!path.Found)return;
            var points=path.NodeIds.Select(City.ViewPosition).ToList();
            points.Add(StandOf(target));
            route.Nodes=path.NodeIds.ToArray();route.X=points.Select(p=>p.X).ToArray();route.Y=points.Select(p=>p.Y).ToArray();
            route.Meters=points.Zip(points.Skip(1),(a,b)=>DistanceCity(a,b)*City.TravelDistanceScale).ToArray();
        }
    }
}
