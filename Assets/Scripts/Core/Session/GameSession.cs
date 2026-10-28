using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 一局游戏的运行状态：世界时钟、账本、城市、玩家所在位置、玩家的店。
    /// 界面层每帧调用 <see cref="Tick"/>，并在玩家走到/进入/离开地点时通知这里。
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        public const int WakeUpMinute = 8 * 60;
        public const int BedtimeReminderMinute = 23 * 60;
        public const int WholesalerOpenMinute = 9 * 60;
        public const int WholesalerCloseMinute = 18 * 60;

        public GameClock Clock { get; }
        public Ledger Ledger { get; }
        public CityLayout City { get; }
        public TimeController Time { get; }
        public ShopSimulation PlayerShop { get; }
        public Place PlayerShopPlace { get; }
        public Place Home { get; }

        /// <summary>玩家当前所在（或最后经过）的路径图节点。</summary>
        public string PlayerNodeId { get; private set; }

        /// <summary>玩家所在的地点；在街上时为 null。</summary>
        public Place CurrentPlace { get; private set; }

        /// <summary>给玩家的提示（睡觉提醒等）。</summary>
        public event Action<string> Notice;

        public GameSession(CityLayout city, int seed, Money openingCash, GameTimestamp start)
        {
            City = city ?? throw new ArgumentNullException(nameof(city));
            PlayerShopPlace = city.PlayerShop() ?? throw new ArgumentException("布局里没有玩家的店。", nameof(city));
            Home = city.FirstOfKind(PlaceKind.Home) ?? throw new ArgumentException("布局里没有住所。", nameof(city));

            Clock = new GameClock(start.Date, start.MinuteOfDay);
            Ledger = new Ledger(openingCash);
            Time = new TimeController();

            PlayerShop = new ShopSimulation(PlayerShopPlace.Id, Clock, Ledger, seed)
            {
                Size = PlayerShopPlace.Size,
                LocationFactor = ComputeLocationFactor(city, PlayerShopPlace),
            };

            PlayerNodeId = Home.NodeId;
            CurrentPlace = Home;
            Clock.TimeAdvanced += OnTimeAdvanced;
        }

        /// <summary>按真实时间推进世界时钟，返回推进的整分钟数。</summary>
        public int Tick(double realSeconds)
        {
            int minutes = Time.Tick(realSeconds);
            if (minutes > 0) Clock.Advance(minutes);
            return minutes;
        }

        /// <summary>玩家在街上走到了某个节点。</summary>
        public void ArriveAtNode(string nodeId)
        {
            City.Map.GetNode(nodeId);
            PlayerNodeId = nodeId;
        }

        /// <summary>进入地点（玩家必须站在它的入口节点上）。进入自己的店即开门营业。</summary>
        public void Enter(Place place)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (place.NodeId != PlayerNodeId)
                throw new InvalidOperationException($"玩家不在 {place.Name} 门口。");
            CurrentPlace = place;
            if (place == PlayerShopPlace) PlayerShop.IsOpen = true;
        }

        /// <summary>离开当前地点回到街上。离开自己的店且没人值守时关门。</summary>
        public void Leave()
        {
            if (CurrentPlace == null) return;
            if (CurrentPlace == PlayerShopPlace) PlayerShop.IsOpen = false;
            CurrentPlace = null;
        }

        /// <summary>给排队的顾客结账，并推进结账耗时。返回是否成功。</summary>
        public bool Checkout(int customerId)
        {
            if (CurrentPlace != PlayerShopPlace) return false;
            int minutes = PlayerShop.Checkout(customerId);
            if (minutes < 0) return false;
            Clock.Advance(minutes);
            return true;
        }

        /// <summary>在家睡到早上 8:00。不在家时返回 false。</summary>
        public bool Sleep()
        {
            if (CurrentPlace != Home) return false;
            Clock.AdvanceTo(WakeUpMinute);
            return true;
        }

        public bool IsWholesalerOpen
        {
            get
            {
                GameTimestamp now = Clock.Now;
                return !now.Date.IsWeekend &&
                       now.MinuteOfDay >= WholesalerOpenMinute &&
                       now.MinuteOfDay < WholesalerCloseMinute;
            }
        }

        public void Dispose()
        {
            Clock.TimeAdvanced -= OnTimeAdvanced;
            PlayerShop.Dispose();
        }

        private void OnTimeAdvanced(GameTimestamp from, GameTimestamp to)
        {
            if (CurrentPlace == Home) return;
            long reminder = (long)from.Date.TotalDays * GameTimestamp.MinutesPerDay + BedtimeReminderMinute;
            if (from.TotalMinutes <= reminder && reminder < to.TotalMinutes)
                Notice?.Invoke("23:00 了，该回家睡觉了。熬夜会让明天精神变差。");
        }

        /// <summary>按到地铁站的路程算位置系数：所有店里离地铁站最远的为 0.4，最近的接近 1。</summary>
        private static double ComputeLocationFactor(CityLayout city, Place shop)
        {
            Place station = city.FirstOfKind(PlaceKind.Station);
            if (station == null) return 1.0;

            double max = 0;
            double mine = 0;
            foreach (Place p in city.Places)
            {
                if (p.Kind != PlaceKind.Shop) continue;
                PathResult path = Pathfinder.FindPath(city.Map, station.NodeId, p.NodeId);
                if (!path.Found) continue;
                if (path.Distance > max) max = path.Distance;
                if (p == shop) mine = path.Distance;
            }
            return ShopSettings.LocationFactor(mine, max);
        }
    }
}
