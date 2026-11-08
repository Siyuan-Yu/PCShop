using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.World
{
    /// <summary>All physical routes use the same meters/game-minute calibration. Edge lengths already contain DistanceScale.</summary>
    public static class CityTravelTiming
    {
        // One balance knob, shared by player, NPCs and employee route estimates. Does not change the world's clock rate.
        public const double PaceFactor = .2;
        public const double PlayerWalkSpeedFactor = 1.2;
        // NPC walking uses the shared outdoor pace without the player's walking boost.
        public const double NpcWalkSpeedFactor = .85;
        public static int NpcMinutes(double meters, VehicleSpec vehicle) =>
            Minutes(vehicle.Kind == VehicleKind.Walk ? meters / NpcWalkSpeedFactor : meters, vehicle);
        public static double PlayerSpeedFactor(VehicleKind kind) => kind == VehicleKind.Walk ? PlayerWalkSpeedFactor : 1;
        public static int PlayerMinutes(double meters, VehicleSpec vehicle) =>
            Minutes(meters / PlayerSpeedFactor(vehicle.Kind), vehicle);
        public static double PlayerScaledSecondsMeters(VehicleSpec vehicle, double scaledSeconds, double secondsPerMinuteAt1x) =>
            ScaledSecondsMeters(vehicle, scaledSeconds, secondsPerMinuteAt1x) * PlayerSpeedFactor(vehicle.Kind);
        public static double SpeedPerMinute(VehicleSpec vehicle) => Math.Max(.01, vehicle.SpeedPerMinute * PaceFactor);
        public static int Minutes(double meters, VehicleSpec vehicle) => meters <= 1e-8 ? 0 : Math.Max(1,
            (int)Math.Min(int.MaxValue, Math.Ceiling(meters / SpeedPerMinute(vehicle) - 1e-9)));
        public static double ScaledSecondsMeters(VehicleSpec vehicle, double scaledSeconds, double secondsPerMinuteAt1x) =>
            Math.Max(0, scaledSeconds) * SpeedPerMinute(vehicle) / (TimeController.WorldRate * secondsPerMinuteAt1x);
        public static double ViewLegMeters(CityLayout city, MapPoint a, MapPoint b) => Distance(a, b) * city.TravelDistanceScale;
        public static double EdgeMeters(CityLayout city, string a, string b)
        {
            foreach (var edge in city.Map.NeighborsOf(a)) if (edge.To == b) return edge.Length;
            throw new ArgumentException("Route edge missing: " + a + " -> " + b);
        }
        public static double[] RouteMeters(CityLayout city, MapPoint start, IReadOnlyList<string> path, MapPoint end, double startScale = -1)
        {
            var result = new List<double>(); MapPoint previous = start;
            for (int i = 0; i < path.Count; i++)
            {
                MapPoint point = city.ViewPosition(path[i]);
                result.Add(i == 0 ? Distance(previous, point) * (city.HasUniformRoadScale || startScale < 0 ? city.TravelDistanceScale : startScale) : EdgeMeters(city, path[i - 1], path[i]));
                previous = point;
            }
            result.Add(ViewLegMeters(city, previous, end)); return result.ToArray();
        }
        public static double LocalScale(CityLayout city, MapPoint point)
        {
            if (city.HasUniformRoadScale) return city.TravelDistanceScale;
            double best = double.MaxValue, scale = city.TravelDistanceScale;
            foreach (var node in city.Map.Nodes)
            foreach (var edge in city.Map.NeighborsOf(node.Id))
            {
                var a = city.ViewPosition(node.Id); var b = city.ViewPosition(edge.To);
                double dx = b.X - a.X, dy = b.Y - a.Y, length2 = dx * dx + dy * dy;
                if (length2 < 1e-10) continue;
                double t = Math.Max(0, Math.Min(1, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length2));
                double distance = Distance(point, new MapPoint(a.X + dx * t, a.Y + dy * t));
                if (distance < best) { best = distance; scale = edge.Length / Math.Sqrt(length2); }
            }
            return scale;
        }
        private static double Distance(MapPoint a, MapPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
