using System;
using System.Collections.Generic;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const double ShopCompetitionRadiusMeters = 40;
        public double ShopCompetitionRadiusWorld => ShopCompetitionRadiusMeters / Math.Max(.001, City.TravelDistanceScale);
        private readonly Dictionary<string, HashSet<string>> _competitionNeighbors = new Dictionary<string, HashSet<string>>();

        // Geometry is immutable. Cache it once; ownership and category remain live so
        // signing, closing, moving, changing category and loading cannot leave stale counts.
        private HashSet<string> CompetitionNeighbors(Place place)
        {
            if (_competitionNeighbors.TryGetValue(place.Id, out var result)) return result;
            result = new HashSet<string>();
            var center = DoorOf(place);
            double radius = ShopCompetitionRadiusWorld;
            foreach (var other in City.Places)
            {
                if (other.Kind != PlaceKind.Shop || other.Id == place.Id || IsDedicatedVenue(other)) continue;
                var point = DoorOf(other);
                double x = point.X - center.X, y = point.Y - center.Y;
                if (x * x + y * y <= radius * radius + 1e-9) result.Add(other.Id);
            }
            _competitionNeighbors.Add(place.Id, result);
            return result;
        }

        public string CompetitionKind(Place place) => ShopAt(place)?.RetailKind ?? PendingRetailKind;

        /// <summary>Includes this shop, or one proposed shop when previewing a vacant unit.</summary>
        public int ShopCompetitionCount(Place place, string kind = null)
        {
            if (place == null || place.Kind != PlaceKind.Shop || IsDedicatedVenue(place)) return 1;
            kind = kind ?? CompetitionKind(place);
            var neighbors = CompetitionNeighbors(place);
            int count = 1;
            foreach (var shop in _shops)
                if (shop.Rented && shop.RetailKind == kind && neighbors.Contains(shop.Place.Id)) count++;
            return count;
        }

        public double ShopCompetitionFactor(Place place) => 1.0 / ShopCompetitionCount(place);
        public double CompetitionDailyPassers(Place place, string kind = null) => FootfallOf(place).DailyPassers / (double)ShopCompetitionCount(place, kind);

        public IReadOnlyList<OwnedShop> NearbyCompetingShops(Place place, string kind = null)
        {
            var result = new List<OwnedShop>();
            if (place == null || IsDedicatedVenue(place)) return result;
            kind = kind ?? CompetitionKind(place);
            var neighbors = CompetitionNeighbors(place);
            foreach (var shop in _shops)
                if (shop.Rented && shop.RetailKind == kind && neighbors.Contains(shop.Place.Id)) result.Add(shop);
            return result;
        }
    }
}
