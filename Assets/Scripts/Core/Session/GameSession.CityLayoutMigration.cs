using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // A geometry migration does not replay time, payments, inventory or activity stages.
        // Existing promises retain their timestamps; only their physical route is rebuilt.
        private void MigrateCityGeometry(SaveData data)
        {
            if(City.LayoutVersion<7 || data.cityLayoutVersion>=City.LayoutVersion)return;
            long now=Clock.Now.TotalMinutes;
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
