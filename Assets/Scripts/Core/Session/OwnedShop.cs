using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 玩家自己经营的一家电脑店：铺面、租约、营业模拟、店里摆的家具、装的升级、员工、补货目标都各管各的。
    /// 知名度、现金、储物柜、家具库存是全部店共用的。还没租铺子时只有一个 Lease 为 null 的占位。
    /// </summary>
    public sealed class OwnedShop
    {
        public Place Place;
        public ShopLease Lease;
        public ShopSimulation Sim;
        public readonly List<PlacedFurniture> Layout = new List<PlacedFurniture>();
        public readonly HashSet<string> Upgrades = new HashSet<string>();
        public readonly Dictionary<string, int> Restock = new Dictionary<string, int>();
        public Employee Employee, Assembler;
        public bool EmployeeTrained, AutoBuild;
        public bool EmployeeBuilding;
        public int RenovationDay = -1;

        public bool Rented => Lease != null;
    }
}
