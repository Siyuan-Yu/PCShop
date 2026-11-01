using System;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    // Compatibility DTOs for the retired room simulation. New sessions never instantiate them.
    public enum CityClimateMode { Off, Auto, Manual }
    [Serializable] public sealed class CityClimateDevice
    {
        public string Id, Kind;
        public CityClimateMode Mode = CityClimateMode.Auto;
        public double Setpoint = 24, Kwh;
        public bool Running, Installed;
        public int Direction;
    }
    [Serializable] public sealed class CityClimateZone
    {
        public string PlaceId;
        public string UpgradeLinkedId;
        public double Air, Area, BilledKwh, CentRemainder;
        public long LastMinute;
        public int LastBillMonth;
        public List<CityClimateDevice> Devices = new List<CityClimateDevice>();
    }
    public static class CityWeather
    {
        private static readonly double[] Means = { 12,15,19,24,28,29,25,20,15,10,6,8 };
        private static double Noise(int seed, long day)
        {
            unchecked { uint x = (uint)seed ^ (uint)day * 747796405u; x = (x ^ (x >> 16)) * 2246822519u; x ^= x >> 13; return x / (double)uint.MaxValue * 4 - 2; }
        }
        public static double Temperature(int seed, long minute)
        {
            double day = minute / 1440.0;
            double anchor = (day - 14.5) / 30;
            int left = (int)Math.Floor(anchor); double f = anchor - Math.Floor(anchor);
            int a = (left % 12 + 12) % 12;
            long d = (long)Math.Floor(day); double t = day - d; t = t * t * (3 - 2 * t);
            return Means[a] + (Means[(a + 1) % 12] - Means[a]) * f
                + Noise(seed,d) * (1-t) + Noise(seed,d+1) * t + 4 * Math.Cos((minute % 1440 - 900) * Math.PI / 720);
        }
        public static string Season(long minute) => new[] { "春", "夏", "秋", "冬" }[(int)((minute / 1440 % 360) / 90)];
        public static bool IsDevice(string id) => id == "fan_fixed" || id == "fan_new" || id == "aircon" || id == "aircon_large";
        public static bool IsFan(string id) => id != null && id.StartsWith("fan_", StringComparison.Ordinal);
    }
}
