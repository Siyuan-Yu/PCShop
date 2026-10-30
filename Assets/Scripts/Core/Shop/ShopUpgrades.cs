using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Shop
{
    /// <summary>店铺升级项。买一次永久生效，效果由会话统一套用。数值是占位，等试玩后平衡。</summary>
    public sealed class ShopUpgradeDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Money Price { get; }

        /// <summary>前置升级（没有则为 null）。</summary>
        public string Requires { get; }

        /// <summary>需要的最低知名度。</summary>
        public double MinReputation { get; }

        /// <summary>能不能在店里的升级页直接买。不能的：老存档里买过的照样生效；新玩法里换了拿法（手机去手机店、货架去家具城、自助结账是任务奖励）。</summary>
        public bool Offered { get; }

        /// <summary>要先有智能手机（去手机店买）。</summary>
        public bool NeedsSmartphone { get; }

        /// <summary>在哪种店里买（地点的业态，例如“手机”“家具”）；店里的升级页只列出来，不能直接买。</summary>
        public string SoldAt { get; }

        /// <summary>卖这项的店叫什么（给玩家看）。</summary>
        public string SoldAtName => SoldAt == ShopUpgrades.PhoneShop ? "手机店" : SoldAt == ShopUpgrades.FurnitureMall ? "宜居家具城" : "";

        public ShopUpgradeDef(string id, string name, string description, long priceYuan, string requires = null, double minReputation = 0, bool offered = true, bool needsSmartphone = false, string soldAt = null)
        {
            SoldAt = soldAt;
            Id = id;
            Name = name;
            Description = description;
            Price = Money.FromYuan(priceYuan);
            Requires = requires;
            MinReputation = minReputation;
            Offered = offered;
            NeedsSmartphone = needsSmartphone;
        }
    }

    public static class ShopUpgrades
    {
        public const string Phone = "phone";
        public const string Qr = "qr";
        public const string SelfCheckout = "selfcheck";
        public const string Shelf1 = "shelf1";
        public const string Shelf2 = "shelf2";
        public const string Shelf3 = "shelf3";
        public const string Sign = "sign";
        public const string AirCon = "aircon";
        public const string Cctv = "cctv";

        public const double SignTrafficBonus = 1.15;
        public const int AirConPatienceBonus = 5;

        /// <summary>卖升级的店的业态（和地图里 business 一致）。</summary>
        public const string PhoneShop = "手机";
        public const string FurnitureMall = "家具";

        public static readonly IReadOnlyList<ShopUpgradeDef> All = new[]
        {
            new ShopUpgradeDef(Phone, "智能手机", "换掉按键机。本身不赚钱，但是开通收款码的前提。", 1200, null, 0, false),
            new ShopUpgradeDef(Qr, "开通收款码", "没带现金的顾客也能买单，结账也更快（1 分钟一单）。需要先有智能手机。", 200, null, 0, true, true, PhoneShop),
            new ShopUpgradeDef(SelfCheckout, "自助结账平台", "排队的顾客自动结账，不用你一单一单点；仍然要有人在店里营业。", 4500, Qr, 8, true, false, PhoneShop),
            new ShopUpgradeDef(Shelf1, "货架扩容 I", "库存容量 +20 格。", 1000, null, 0, false),
            new ShopUpgradeDef(Shelf2, "货架扩容 II", "库存容量再 +20 格。", 2000, Shelf1, 0, false),
            new ShopUpgradeDef(Shelf3, "货架扩容 III", "库存容量再 +30 格。", 3500, Shelf2, 0, false),
            new ShopUpgradeDef(Sign, "招牌灯箱", "夜里也亮的招牌，客流约 +15%。", 900, soldAt: FurnitureMall),
            new ShopUpgradeDef(AirCon, "空调", "顾客排队更有耐心（多等 5 分钟才走）。", 1800, soldAt: FurnitureMall),
            new ShopUpgradeDef(Cctv, "监控摄像头", "小偷不敢来了。", 600, soldAt: PhoneShop),
        };

        /// <summary>某种店里卖的店铺设备。</summary>
        public static IEnumerable<ShopUpgradeDef> SoldBy(string business)
        {
            foreach (ShopUpgradeDef u in All)
                if (u.Offered && u.SoldAt == business) yield return u;
        }

        /// <summary>升级页上能直接买的。</summary>
        public static IEnumerable<ShopUpgradeDef> OfferedList()
        {
            foreach (ShopUpgradeDef u in All)
                if (u.Offered) yield return u;
        }

        public static ShopUpgradeDef Get(string id)
        {
            foreach (ShopUpgradeDef u in All)
                if (u.Id == id) return u;
            return null;
        }

        public static bool Exists(string id) => Get(id) != null;

        /// <summary>货架升级带来的额外容量（格）。</summary>
        public static double ExtraCapacity(ICollection<string> owned)
        {
            double extra = 0;
            if (owned.Contains(Shelf1)) extra += 20;
            if (owned.Contains(Shelf2)) extra += 20;
            if (owned.Contains(Shelf3)) extra += 30;
            return extra;
        }
    }
}
