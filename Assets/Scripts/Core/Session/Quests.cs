using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 新手任务里的一步：告诉玩家现在该去哪、做什么。是否完成直接看游戏里的实际状态（租了没、买了没、摆了没……），
    /// 不单独存档；每一步都完成了才会跳到下一步。
    /// </summary>
    public sealed class QuestDef
    {
        public string Id;

        public string Title;

        /// <summary>一句话：现在去做什么（任务条上显示）。</summary>
        public string Short;

        /// <summary>详细说明：去哪里、怎么做（任务列表里显示）。</summary>
        public string Hint;

        public Func<GameSession, bool> Done;

        /// <summary>进度文字（例如“货架 3/6”）。</summary>
        public Func<GameSession, string> Progress;
    }

    public static class Quests
    {
        public static readonly IReadOnlyList<QuestDef> All = new[]
        {
            new QuestDef
            {
                Id = "rent", Title = "租一间铺子",
                Short = "去商业街的「安居房产中介」挑一间铺面签约",
                Hint = "新游戏你还没有店。点下方“房产中介”快捷前往，进去挑一间铺面签约：要付首月租金、押金和中介费，别把钱全花光，后面还要买家具和进货。",
                Done = s => s.HasShop,
                Progress = s => $"现金 {s.Ledger.Balance}",
            },
            new QuestDef
            {
                Id = "buy", Title = "去家具城买店面设施",
                Short = "去「宜居家具城」买货架、收银台、操作台、展示桌",
                Hint = "租来的是毛坯铺子，什么都没有。去「宜居家具城」买：货架 ×6（每类配件一个）、收银台 ×1、装机操作台 ×1、整机展示桌 ×1。大件会叫货车送到店里，要等一会儿。更结实的货架（加固 / 重型）也在这里买。",
                Done = s => FitDone(s, false),
                Progress = s => FitText(s, false),
            },
            new QuestDef
            {
                Id = "place", Title = "歇业装修，把设施摆好",
                Short = "回店里点右上角“我的”→“装修店面”，把家具摆好",
                Hint = "货车把东西送到店里后，回到自己的店，点右上角“我的”→“装修店面”（今天歇业），在右侧列表选家具、点地上摆下；货架靠墙摆，收银台放门边。摆完点“完成”，第二天才能开业。",
                Done = s => FitDone(s, true),
                Progress = s => FitText(s, true),
            },
            new QuestDef
            {
                Id = "shelf", Title = "给每个货架设置摆什么",
                Short = "走到货架前按 F，给每类配件设置一个货架",
                Hint = "新摆的货架是空的，每个货架只摆一种货。走到货架前按 F（或店内右侧面板“库存”页点“设置”）选摆什么。处理器、显卡、内存、硬盘、主板、电源机箱各一个，顾客只买货架上摆着的。",
                Done = s => ShelvesSet(s) >= FurnitureCatalog.ShelfCategories.Length,
                Progress = s => $"已设置 {ShelvesSet(s)}/{FurnitureCatalog.ShelfCategories.Length} 类配件",
            },
            new QuestDef
            {
                Id = "stock", Title = "进第一批货",
                Short = "去电子市场的档口进货，带回店里",
                Hint = "后仓是空的，顾客来了也没东西卖。点下方“电子市场”快捷前往，在各家档口买几类配件，用背包 / 三轮车带回来，到店里放进后仓，货架会自动摆上。有了智能手机、装上 1788 app 也能网上批发，便宜但要等一天。",
                Done = s => StockCategories(s) >= 3 && StockCount(s) >= 6,
                Progress = s => $"后仓 {StockCount(s)} 件，覆盖 {StockCategories(s)} 类配件（至少 6 件、3 类）",
            },
            new QuestDef
            {
                Id = "sale", Title = "开业，卖出第一单",
                Short = "人守在店里，顾客来了去收银台结账",
                Hint = "装修完第二天店就开业了。人要在店里：顾客挑好货会在收银台前排队，走到收银台旁按 F，或点顾客头顶的“结账”收钱。没人看店就关门。",
                Done = s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.SalesCategory),
                Progress = s => s.PlayerShop.IsOpen ? "店在营业，等顾客上门" : "店还没开业",
            },
            new QuestDef
            {
                Id = "hire", Title = "雇一名店员",
                Short = "店内“招人”页贴启事，雇个人看店",
                Hint = "一个人守店走不开。在店内右侧面板“招人”页贴招聘启事，过几天来人了面试雇一个，日薪每天结算。店员替你收银，你就能出去进货、跑腿。第一个月的目标也要求雇过人。",
                Done = s => s.Employee != null || s.EverHired,
                Progress = s => s.Employee != null ? $"已雇 {s.Employee.Name}" : "还没雇人",
            },
            new QuestDef
            {
                Id = "phone", Title = "买一部智能手机",
                Short = "去街上的「手机店」买智能手机",
                Hint = "按键机只能打电话。去街上的「手机店」买一部智能手机，再到应用商店下载 1788 app，网上进货便宜（要等一天）；它也是开通收款码的前提。",
                Done = s => s.HasSmartphone,
                Progress = s => s.HasSmartphone ? "已有智能手机" : "现在只有按键机",
            },
            new QuestDef
            {
                Id = "qr", Title = "开通收款码",
                Short = "店内“升级”页开通收款码（¥200）",
                Hint = "在店内右侧面板“升级”页开通收款码（¥200）。不带现金的顾客也能买单，结账更快，排队的人更少。",
                Done = s => s.HasUpgrade(ShopUpgrades.Qr),
                Progress = s => s.HasUpgrade(ShopUpgrades.Qr) ? "已开通" : "还没开通",
            },
        };

        public static QuestDef Get(string id) => All.FirstOrDefault(q => q.Id == id);

        // ---------- 判断用 ----------

        private static bool FitDone(GameSession s, bool placedOnly)
        {
            if (!s.HasShop) return false;
            return s.ShopFitList().All(n => (placedOnly ? n.Placed : n.Placed + n.Owned) >= n.Want);
        }

        private static string FitText(GameSession s, bool placedOnly)
        {
            if (!s.HasShop) return "先租铺子";
            return string.Join("　", s.ShopFitList().Select(n => $"{n.Name} {(placedOnly ? n.Placed : n.Placed + n.Owned)}/{n.Want}"));
        }

        /// <summary>设置过摆什么的配件货架覆盖了几类配件。</summary>
        private static int ShelvesSet(GameSession s)
        {
            if (!s.HasShop) return 0;
            return FurnitureCatalog.ShelfCategories.Count(c => s.ShelvesOfKind(ShelfKinds.ForCategory(c).Id) > 0);
        }

        private static int StockCount(GameSession s) => PartCatalog.All.Sum(p => s.PlayerShop.Stock.CountAll(p));

        private static int StockCategories(GameSession s) =>
            PartCatalog.All.Where(p => s.PlayerShop.Stock.CountAll(p) > 0).Select(p => p.Category).Distinct().Count();
    }

    public sealed partial class GameSession
    {
        /// <summary>现在该做的新手任务下标（全部做完 = <see cref="Quests.All"/> 的长度）。</summary>
        public int QuestIndex()
        {
            for (int i = 0; i < Quests.All.Count; i++)
                if (!Quests.All[i].Done(this)) return i;
            return Quests.All.Count;
        }

        /// <summary>现在该做的新手任务；都做完了返回 null。</summary>
        public QuestDef CurrentQuest()
        {
            int i = QuestIndex();
            return i < Quests.All.Count ? Quests.All[i] : null;
        }
    }
}
