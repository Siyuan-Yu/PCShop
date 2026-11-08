using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.World
{
    public sealed class ShopFootfallEstimate
    {
        public int DailyPassers { get; }
        public double StreetDemand { get; }
        public double StationMeters { get; }
        public double HousingMeters { get; }
        public Money MonthlyRent => Money.FromYuan(DailyPassers * 5L);
        internal ShopFootfallEstimate(int passers,double streetDemand,double station,double housing)
        { DailyPassers=passers;StreetDemand=streetDemand;StationMeters=station;HousingMeters=housing; }
    }

    // Immutable layout inputs, evaluated once per queried shop. No pathfinding per frame.
    public sealed class ShopFootfall
    {
        private readonly CityLayout _city;
        private readonly string[] _stations;
        private readonly List<(string node,double offset,int people)> _housing=new List<(string,double,int)>();
        private readonly Dictionary<string,ShopFootfallEstimate> _cache=new Dictionary<string,ShopFootfallEstimate>();
        public ShopFootfall(CityLayout city)
        {
            _city=city;
            _stations=city.OfKind(PlaceKind.Station).Select(p=>p.NodeId).Distinct().ToArray();
            foreach(var home in city.OfKind(PlaceKind.Home))_housing.Add((home.NodeId,0,Residents(home.Model)));
            foreach(var block in city.Blocks)
            {
                if(block.model!="oldhome"&&block.model!="apartment"&&block.model!="luxury_apartment"&&block.model!="villa")continue;
                string node=city.NearestNode(block.x,block.y);if(node==null)continue;
                var at=city.ViewPosition(node);double dx=at.X-block.x,dy=at.Y-block.y;
                _housing.Add((node,Math.Sqrt(dx*dx+dy*dy)*city.TravelDistanceScale,
                    Residents(block.model)*Math.Max(1,block.columns)*Math.Max(1,block.rows)));
            }
        }
        private static int Residents(string model)=>model=="apartment"?80:model=="luxury_apartment"?48:model=="villa"?8:24;
        public ShopFootfallEstimate For(Place place)
        {
            if(_cache.TryGetValue(place.Id,out var saved))return saved;
            var distances=Distances(place.NodeId);
            double station=double.PositiveInfinity,housing=double.PositiveInfinity,nearbyResidents=0;
            foreach(var node in _stations)if(distances.TryGetValue(node,out double d))station=Math.Min(station,d);
            foreach(var home in _housing)
            {
                if(!distances.TryGetValue(home.node,out double road))continue;
                double d=road+home.offset;housing=Math.Min(housing,d);
                nearbyResidents+=home.people/Math.Pow(1+d/180.0,2);
            }
            double metro=double.IsPositiveInfinity(station)?0:220/(1+station/180.0);
            double residential=200*nearbyResidents/(nearbyResidents+160);
            double frontage=place.Size==ShopSize.Large?5.4:place.Size==ShopSize.Medium?2.5:1;
            double streetDemand = 80 + metro + residential;
            var estimate=new ShopFootfallEstimate(Math.Max(1,(int)Math.Round(streetDemand*frontage)),streetDemand,station,housing);
            _cache.Add(place.Id,estimate);return estimate;
        }
        private Dictionary<string,double> Distances(string origin)
        {
            var distances=new Dictionary<string,double>{{origin,0}};
            var open=new SortedSet<(double distance,long order,string node)>();long order=0;
            open.Add((0,order++,origin));
            while(open.Count>0)
            {
                var next=open.Min;open.Remove(next);
                if(next.distance>distances[next.node])continue;
                foreach(var edge in _city.Map.NeighborsOf(next.node))
                {
                    double distance=next.distance+edge.Length;
                    if(distances.TryGetValue(edge.To,out var known)&&distance>=known)continue;
                    distances[edge.To]=distance;open.Add((distance,order++,edge.To));
                }
            }
            return distances;
        }
    }
}
