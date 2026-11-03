using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.World
{
    public sealed class DistrictRoadGate
    {
        public int Tier;
        public string FromNode, ToNode;
        public MapPoint Center, Direction;
        public const double HalfWidth = 12;

        // The lake has exactly three inter-district connecting roads. Use their actual
        // graph nodes, without changing the shared graph used by residents and deliveries.
        public static IReadOnlyList<DistrictRoadGate> ForCity(CityLayout city)
        {
            var gates = new List<DistrictRoadGate>();
            if (!city.IsLakeCity) return gates;
            for (int tier = 1; tier <= 3; tier++)
            {
                string prefix = "lake_gap" + tier + "_";
                var nodes = city.Map.Nodes.Where(n => n.Id.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(n => int.Parse(n.Id.Substring(prefix.Length))).ToArray();
                if (nodes.Length < 2) continue;
                string from = nodes[(nodes.Length - 1) / 2].Id, to = nodes[(nodes.Length - 1) / 2 + 1].Id;
                var a = city.ViewPosition(from); var b = city.ViewPosition(to);
                double length = Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y));
                gates.Add(new DistrictRoadGate { Tier = tier, FromNode = from, ToNode = to,
                    Center = new MapPoint((a.X+b.X)/2, (a.Y+b.Y)/2),
                    Direction = new MapPoint((b.X-a.X)/length, (b.Y-a.Y)/length) });
            }
            return gates;
        }

        public bool Crosses(MapPoint from, MapPoint to, out MapPoint stop)
        {
            stop = from;
            double a = (from.X-Center.X)*Direction.X + (from.Y-Center.Y)*Direction.Y;
            double b = (to.X-Center.X)*Direction.X + (to.Y-Center.Y)*Direction.Y;
            // Residents and old saves on the far side may always return to the open area.
            if (a > .001 || b <= 0 || b <= a) return false;
            double t = -a/(b-a);
            double x = from.X + (to.X-from.X)*t, y = from.Y + (to.Y-from.Y)*t;
            double lateral = -(x-Center.X)*Direction.Y + (y-Center.Y)*Direction.X;
            if (Math.Abs(lateral) > HalfWidth) return false;
            stop = new MapPoint(x-Direction.X*.15, y-Direction.Y*.15);
            return true;
        }
    }
}
