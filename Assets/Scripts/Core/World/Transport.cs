using System;

namespace PCShopSell.Core.World
{
    /// <summary>交通方式。速度单位：米/分钟（游戏内分钟）。</summary>
    public sealed class Transport
    {
        public string Id { get; }
        public string Name { get; }
        public double SpeedPerMinute { get; }

        public Transport(string id, string name, double speedPerMinute)
        {
            if (speedPerMinute <= 0)
                throw new ArgumentOutOfRangeException(nameof(speedPerMinute), "速度必须为正。");
            Id = id;
            Name = name;
            SpeedPerMinute = speedPerMinute;
        }
    }

    /// <summary>内置交通方式的默认参数（后续可由数据配置覆盖）。</summary>
    public static class Transports
    {
        /// <summary>步行，约 4.8 km/h。</summary>
        public static readonly Transport Walk = new Transport("walk", "步行", 80);

        /// <summary>电动车，约 15 km/h。</summary>
        public static readonly Transport EBike = new Transport("ebike", "电动车", 250);

        /// <summary>汽车（市区），约 30 km/h。</summary>
        public static readonly Transport Car = new Transport("car", "汽车", 500);
    }
}
