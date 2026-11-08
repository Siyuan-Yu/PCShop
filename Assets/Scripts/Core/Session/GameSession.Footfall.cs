using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData { public int shopFootfallVersion, leaseSignedDay; }
    public sealed partial class GameSession
    {
        private ShopFootfall _shopFootfall;
        private Dictionary<int,(double min,double max)> _districtDemandRange;
        private readonly Dictionary<string,double> _streetTraffic = new Dictionary<string,double>();
        private readonly Dictionary<(string from,string to),string> _trafficStreetByEdge = new Dictionary<(string,string),string>();
        private long _streetTrafficAt = -1;
        private double _streetTrafficReputation;
        private const int RestaurantVisitMinutes = 12;
        private const double RestaurantTurnoverMinutes = 30;
        private const double ReferenceStreetDemand = 300;
        private const double LowStreetDemandFactor = .52;
        private static readonly ShopSettings PreviewShopSettings = new ShopSettings();
        public ShopFootfallEstimate FootfallOf(Place place) => (_shopFootfall??(_shopFootfall=new ShopFootfall(City))).For(place);
        public Money ShopRentQuote(Place place)
        {
            // Price the site's baseline arrival demand. Day of week and shop operations affect visits only.
            const double minimumRent = 2000;
            double demandRatio = Math.Max(1, ShopArrivalDemand(place) / (ReferenceStreetDemand * LowStreetDemandFactor));
            double baseRent = minimumRent * Math.Pow(demandRatio, .65);
            return Money.FromYuan((long)Math.Round(baseRent / 100, MidpointRounding.AwayFromZero) * 100).Scale(ShopRentFactor);
        }
        private double StreetDemandFactor(Place place)
        {
            _districtDemandRange ??= City.Places.Where(IsCommercialPremises).GroupBy(p=>p.Tier)
                .ToDictionary(g=>g.Key,g=>(g.Min(p=>FootfallOf(p).StreetDemand),g.Max(p=>FootfallOf(p).StreetDemand)));
            double demand = FootfallOf(place).StreetDemand;
            if (!_districtDemandRange.TryGetValue(place.Tier,out var range) || range.max - range.min < 1) return 1;
            double rank = Math.Max(0,Math.Min(1,(demand-range.min)/(range.max-range.min)));
            return LowStreetDemandFactor + 1.08 * rank;
        }

        public double StreetTrafficMultiplier(string streetId)
        {
            long now = Clock.Now.TotalMinutes;
            double reputation = MainShop.Sim.Reputation;
            if (_streetTrafficAt != now || _streetTrafficReputation != reputation)
            {
                _streetTrafficAt = now;
                _streetTrafficReputation = reputation;
                _streetTraffic.Clear();
                foreach (var shop in _shops)
                {
                    if (!shop.Rented || IsDedicatedVenue(shop.Place) || shop.Sim.ForcedClosed || shop.Sim.NotFitted) continue;
                    var brand = _brands.Brands.FirstOrDefault(b=>b.Kind==shop.RetailKind);
                    double brandPull = brand == null ? 0 : Math.Max(0,BrandStandingTraffic(brand))*.4;
                    string street = shop.Place.StreetId;
                    _streetTraffic.TryGetValue(street,out double pull);
                    _streetTraffic[street] = Math.Min(.25,pull + shop.Sim.Awareness*.001 + brandPull);
                }
            }
            return streetId != null && _streetTraffic.TryGetValue(streetId,out double bonus) ? 1 + bonus : 1;
        }

        public string TrafficStreetOfSegment(string from, string to)
        {
            var key = string.CompareOrdinal(from,to) < 0 ? (from,to) : (to,from);
            if (_trafficStreetByEdge.TryGetValue(key,out var street)) return street;
            var a = City.ViewPosition(from);
            var b = City.ViewPosition(to);
            double x = (a.X + b.X)*.5, y = (a.Y + b.Y)*.5;
            double nearest = double.MaxValue;
            foreach (var place in City.Places)
            {
                if (place.Kind != PlaceKind.Home && place.Kind != PlaceKind.Shop && place.Kind != PlaceKind.Station) continue;
                var point = City.ViewPosition(place.NodeId);
                double dx = point.X-x, dy = point.Y-y, distance = dx*dx+dy*dy;
                if (distance >= nearest) continue;
                nearest = distance;
                street = place.StreetId;
            }
            _trafficStreetByEdge.Add(key,street);
            return street;
        }

        // Use one geography adjustment for the map preview and actual arrivals.
        public double ShopArrivalDemand(Place place)
        {
            var footfall = FootfallOf(place);
            return footfall.DailyPassers * StreetDemandFactor(place) * ReferenceStreetDemand / footfall.StreetDemand;
        }
        public double LocationFactorOf(Place place) => ShopArrivalDemand(place)/(600.0*ShopSettings.SizeFactor(place.Size));
        public double StationDistance(Place place) => FootfallOf(place).StationMeters;
        private long SpecialVisitInterval(Place place,int baseMinutes,int competition,double traffic)
        {
            double rate = ShopSettings.BaseFootfallMultiplier * ShopArrivalDemand(place) * traffic / (300.0 * competition * baseMinutes);
            return Math.Max(1,(long)Math.Round(1 / rate));
        }
        private long SpecialVisitInterval(OwnedShop shop,int baseMinutes) => SpecialVisitInterval(shop.Place,baseMinutes,
            ShopCompetitionCount(shop.Place,shop.RetailKind),shop.Sim.ArrivalTrafficMultiplier);

        public double ExpectedDailyShopEntrants(Place place, string kind = null, bool asNewShop = false)
        {
            var shop = asNewShop ? null : ShopAt(place);
            kind = shop?.RetailKind ?? RetailShopTypes.Valid(kind ?? (IsPendingShop(place) ? PendingRetailKind : null));
            int competition = ShopCompetitionCount(place,kind);
            int open = shop?.Sim.OpenMinute ?? ShopSimulation.DefaultOpenMinute;
            int close = shop?.Sim.CloseMinute ?? ShopSimulation.DefaultCloseMinute;
            double traffic = shop?.Sim.ArrivalTrafficMultiplier ?? StreetTrafficMultiplier(place.StreetId);
            if (kind == "restaurant")
            {
                int tables = shop == null ? 1 : Math.Max(1,Furnished(shop,"restaurant_table"));
                long interval = SpecialVisitInterval(place,RestaurantVisitMinutes,competition,traffic);
                int openMinutes = open < close ? close-open : 1440-open+close;
                return openMinutes * Math.Min(1.0/interval,tables/RestaurantTurnoverMinutes);
            }
            double count = 0;
            var settings = shop?.Sim.Settings ?? PreviewShopSettings;
            var size = shop?.Sim.Size ?? place.Size;
            double location = shop?.Sim.LocationFactor ?? LocationFactorOf(place);
            var date = Clock.Today;
            for (int hour = 0; hour < 24; hour++)
            {
                int from = hour * 60, to = from + 60;
                int Overlap(int start,int end) => Math.Max(0, Math.Min(end,to) - Math.Max(start,from));
                int minutes = open < close ? Overlap(open,close) : Overlap(0,close) + Overlap(open,1440);
                if (minutes == 0) continue;
                var at = GameTimestamp.From(date,from);
                count += ShopSimulation.ArrivalRatePerMinute(settings,at,size,location,traffic / competition) * minutes;
            }
            return count;
        }
    }
}
