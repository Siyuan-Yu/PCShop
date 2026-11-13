using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [System.Serializable]
    public struct ShopEmergencyLighting
    {
        public int FailureDay;
        public long OutageEndsAt;
        public bool UsingCandles;

        public bool Installed => FailureDay > 0;
        public bool WorksOn(int day) => Installed && day < FailureDay;
        public bool PowerOutAt(long minute) => minute < OutageEndsAt;
    }

    /// <summary>
    /// 玩家经营的一家店：铺面、租约、营业模拟、家具、升级、员工和补货目标各管各的。
    /// 知名度、现金、家具库存是全部店共用的；纸箱和临时存放每家店各有各的。还没租铺子时只有一个 Lease 为 null 的占位。
    /// </summary>
    public sealed class OwnedShop
    {
        public string RetailKind = RetailShopTypes.Default;
        public string CustomName;
        public Place Place;
        public ShopLease Lease;
        public bool OwnsProperty;
        public ShopSimulation Sim;
        public readonly List<PlacedFurniture> Layout = new List<PlacedFurniture>();
        public readonly HashSet<string> Upgrades = new HashSet<string>();
        public readonly Dictionary<string, int> Restock = new Dictionary<string, int>();
        public Employee Employee, Assembler, Buyer, Salesperson;
        public readonly List<AdditionalShopEmployee> AdditionalEmployees=new List<AdditionalShopEmployee>();
        public bool AutoBuild;
        public bool EmployeeBuilding;
        public int PendingDisplayTier = -1, PendingDisplayMinutes;
        public int RenovationDay = -1;
        public ShopEmergencyLighting EmergencyLighting;

        /// <summary>这家店纸箱里的东西（吃的、礼品、小家具），容量看店里摆了几个纸箱。</summary>
        public readonly Backpack Fridge = new Backpack();

        public readonly Backpack Locker = new Backpack();

        /// <summary>这家店的临时存放：送来的东西纸箱放不下就先堆在这里。</summary>
        public readonly Backpack Temp = new Backpack();

        /// <summary>临时存放从什么时候开始有东西（游戏分钟）；0 = 空着。</summary>
        public long TempSince;

        /// <summary>租下时送的纸箱已经给过了（收起来再读档也不会重复送）。</summary>
        public bool BoxesGiven;

        // Legacy callers use Rented to mean an active lawful operating unit, including owned premises.
        public bool Rented => OwnsProperty || Lease != null;
    }
}
