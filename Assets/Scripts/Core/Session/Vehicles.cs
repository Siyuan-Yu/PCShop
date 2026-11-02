using System;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public enum VehicleKind
    {
        Walk,
        Taxi,
        EBike,
        Tricycle,
        Car,
        Van,
    }

    /// <summary>出行方式参数。物理速度为游戏内米/分钟，表现速度与其使用相同的步行倍率；实际街道移动与行程时间由 CityTravelTiming 统一换算。</summary>
    public sealed class VehicleSpec
    {
        public VehicleKind Kind { get; }
        public string Name { get; }
        public double SpeedPerMinute { get; }

        /// <summary>能随身带的货（格）。</summary>
        public double CargoCapacity { get; }

        public Money Price { get; }
        public double VisualSpeed { get; }

        /// <summary>城里不让上路，会被交警追。</summary>
        public bool Illegal { get; }

        public bool Purchasable => Price.IsPositive;

        public VehicleSpec(VehicleKind kind, string name, double speedPerMinute, double cargoCapacity,
            long priceYuan, double visualSpeed, bool illegal)
        {
            Kind = kind;
            Name = name;
            SpeedPerMinute = speedPerMinute;
            CargoCapacity = cargoCapacity;
            Price = Money.FromYuan(priceYuan);
            VisualSpeed = visualSpeed;
            Illegal = illegal;
        }
    }

    public static class Vehicles
    {
        // Street presentation only (walking and vehicles); indoor movement has its own unchanged speed.
        public const double StreetSpeedMultiplier = .5;
        // Shared outdoor player/pedestrian adjustment; rooms retain their authored walking speeds.
        public const double WalkingSpeedMultiplier = .25;
        public static readonly VehicleSpec Walk = new VehicleSpec(VehicleKind.Walk, "步行", 49 * WalkingSpeedMultiplier, 6, 0, 5.5 * StreetSpeedMultiplier * WalkingSpeedMultiplier, false);
        // Anchor both physical pace and presentation to the current walking speed.
        public static readonly VehicleSpec Taxi = AtWalkingRatio(VehicleKind.Taxi, "打车", 4.8, 15, 0, false);
        public static readonly VehicleSpec EBike = AtWalkingRatio(VehicleKind.EBike, "小电驴", 3, 10, 1800, true);
        public static readonly VehicleSpec Tricycle = AtWalkingRatio(VehicleKind.Tricycle, "三轮车", 2.4, 40, 2500, true);

        public static readonly VehicleSpec Car = AtWalkingRatio(VehicleKind.Car, "小轿车", 4.32, 18, 68000, false);
        public static readonly VehicleSpec Van = AtWalkingRatio(VehicleKind.Van, "小货车", 3.84, 80, 46000, false);
        public static readonly VehicleSpec[] All = { Walk, Taxi, EBike, Tricycle, Car, Van };

        private static VehicleSpec AtWalkingRatio(VehicleKind kind,string name,double ratio,double capacity,long price,bool illegal)
            =>new VehicleSpec(kind,name,Walk.SpeedPerMinute*ratio,capacity,price,Walk.VisualSpeed*ratio,illegal);

        public static VehicleSpec Get(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Taxi: return Taxi;
                case VehicleKind.EBike: return EBike;
                case VehicleKind.Tricycle: return Tricycle;
                case VehicleKind.Car: return Car;
                case VehicleKind.Van: return Van;
                default: return Walk;
            }
        }

        /// <summary>出租车车费：起步价 ¥10 含 2 公里，之后每公里 ¥2（不足 1 公里按 1 公里）。</summary>
        public static Money TaxiFare(double meters)
        {
            double km = meters / 1000.0;
            if (km <= 2) return Money.FromYuan(10);
            return Money.FromYuan(10 + 2 * (long)Math.Ceiling(km - 2));
        }
    }
}
