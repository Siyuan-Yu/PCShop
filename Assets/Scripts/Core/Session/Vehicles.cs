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
    }

    /// <summary>出行方式参数。速度为游戏内米/分钟；画面速度为画面米/真实秒（1 倍速）。</summary>
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
        public static readonly VehicleSpec Walk = new VehicleSpec(VehicleKind.Walk, "步行", 80, 6, 0, 9, false);
        public static readonly VehicleSpec Taxi = new VehicleSpec(VehicleKind.Taxi, "打车", 400, 15, 0, 24, false);
        public static readonly VehicleSpec EBike = new VehicleSpec(VehicleKind.EBike, "小电驴", 250, 10, 1800, 17, false);
        public static readonly VehicleSpec Tricycle = new VehicleSpec(VehicleKind.Tricycle, "三轮车", 200, 40, 2500, 15, true);

        public static readonly VehicleSpec[] All = { Walk, Taxi, EBike, Tricycle };

        public static VehicleSpec Get(VehicleKind kind)
        {
            switch (kind)
            {
                case VehicleKind.Taxi: return Taxi;
                case VehicleKind.EBike: return EBike;
                case VehicleKind.Tricycle: return Tricycle;
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
