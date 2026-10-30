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
    /// 完成后记入存档；售货、搬家或重新布置不会撤销已完成的教学。
    /// </summary>
    public sealed class QuestDef
    {
        public string Id;

        public int Chapter;
        public QuestTarget Target;
        public bool Lesson;
        public Func<GameSession, bool> LessonReady;

        public string Title;

        /// <summary>一句话：现在去做什么（任务条上显示）。</summary>
        public string Short;

        /// <summary>详细说明：去哪里、怎么做（当前任务说明里显示）。</summary>
        public string Hint;

        public Func<GameSession, bool> Done;

        /// <summary>进度文字（例如“货架 3/6”）。</summary>
        public Func<GameSession, string> Progress;
    }

    public static partial class Quests
    {
        public static readonly IReadOnlyList<QuestDef> All = new[]
        {
            new QuestDef
            {
                Id = "rent", Title = "租一间铺子", Target = QuestTarget.Agency,
                Short = "去商业街的「安居房产中介」挑一间铺面签约",
                Hint = "先出门到商业街，找到“安居房产中介”的招牌，走到门口按 F 进入，挑一间铺面签约。要付首月租金、押金和中介费；留些钱作为开业后的进货和周转资金。",
                Done = s => s.HasShop,
                Progress = s => $"现金 {s.Ledger.Balance}",
            },
            new QuestDef
            {
                Id = "buy", Title = "准备最低开业设施", Target = QuestTarget.Renovate,
                Short = "准备一个收银台和一个货架；首次租铺已赠送",
                Hint = "首次租铺送一个收银台和一个货架，在待安装家具里。先用它们开业赚钱；展示桌、装机台和更多货架以后再去家具城购买，不必一次买齐。",
                Done = s => FitDone(s, false),
                Progress = s => FitText(s, false),
            },
            new QuestDef
            {
                Id = "place", Title = "歇业装修，把设施摆好", Target = QuestTarget.Renovate,
                Short = "回店里打开右侧“装修”页，摆好收银台和一个货架",
                Hint = "货车把东西送到店里后，回到自己的店，打开右侧“装修”页（今天歇业），在右侧列表选家具、点地上摆下；货架靠墙摆，收银台放门边。摆完点“完成”，第二天才能开业。",
                Done = s => FitDone(s, true),
                Progress = s => FitText(s, true),
            },
            new QuestDef
            {
                Id = "shelf", Title = "设置第一个配件货架", Target = QuestTarget.Stock,
                Short = "走到货架前按 F，把第一个货架设为已有货物的类别（例如处理器）",
                Hint = "新摆的货架是空的，每个货架只摆一种货。走到货架前按 F（或店内右侧面板“库存”页点“设置”）选摆什么。先选后仓已有的处理器或显卡，以后再逐步扩充其他品类，顾客只买货架上摆着的。",
                Done = s => ShelvesSet(s) >= 1,
                Progress = s => $"已设置 {Math.Min(1, ShelvesSet(s))}/1 类配件",
            },
            new QuestDef
            {
                Id = "stock", Title = "进第一批货", Target = QuestTarget.Market,
                Short = "先销售租铺赠送的配件；没有货再去档口补货",
                Hint = "先用租铺赠送的 CPU 和显卡开业。缺货时去电子市场，走近配件档口按 F 进入购买，用背包或三轮车带回来，到店里放进后仓，货架会自动摆上。有了智能手机、装上 1788 app 也能网上批发，便宜但要等一天。",
                Done = s => StockCount(s) >= 1,
                Progress = s => $"后仓 {StockCount(s)} 件，覆盖 {StockCategories(s)} 类配件（至少 1 件即可开始）",
            },
            new QuestDef
            {
                Id = "sale", Title = "开业，卖出第一单", Target = QuestTarget.Counter,
                Short = "人守在店里，顾客来了去收银台结账",
                Hint = "装修完第二天店就开业了。人要在店里：顾客挑好货会在收银台前排队，走到收银台旁按 F，或点顾客头顶的“结账”收钱。没人看店就关门。",
                Done = s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.SalesCategory),
                Progress = s => s.PlayerShop.IsOpen ? "店在营业，等顾客上门" : "店还没开业",
            },
            new QuestDef
            {
                Id = "hire", Title = "雇一名店员", Target = QuestTarget.Staff,
                Short = "店内“招人”页贴启事，雇个人看店",
                Hint = "一个人守店走不开。在店内右侧面板“招人”页贴招聘启事，次日 9:00 来人后面试雇一个，日薪每天结算。店员替你收银，你就能出去进货、跑腿。第一个月的目标也要求雇过人。",
                Done = s => s.Employee != null || s.EverHired,
                Progress = s => s.Employee != null ? $"已雇 {s.Employee.Name}" : "还没雇人",
            },
            new QuestDef
            {
                Id = "phone", Title = "买一部智能手机", Target = QuestTarget.PhoneShop,
                Short = "去街上的「手机店」买智能手机",
                Hint = "按键机只能打电话。去街上的「手机店」买一部智能手机，再到应用商店下载 1788 app，网上进货便宜（要等一天）；它也是开通收款码的前提。",
                Done = s => s.HasSmartphone,
                Progress = s => s.HasSmartphone ? "已有智能手机" : "现在只有按键机",
            },
            new QuestDef
            {
                Id = "qr", Title = "开通收款码", Target = QuestTarget.Upgrades,
                Short = "店内“升级”页开通收款码（¥200）",
                Hint = "在店内右侧面板“升级”页开通收款码（¥200）。不带现金的顾客也能买单，结账更快，排队的人更少。",
                Done = s => s.HasUpgrade(ShopUpgrades.Qr),
                Progress = s => s.HasUpgrade(ShopUpgrades.Qr) ? "已开通" : "还没开通",
            },
        }.Concat(GrowthSteps()).ToArray();

        public static QuestDef Get(string id) => All.FirstOrDefault(q => q.Id == id);

        // ---------- 判断用 ----------

        private static bool FitDone(GameSession s, bool placedOnly)
        {
            if (!s.HasShop) return false;
            return s.ShopFitList().Where(n => n.Required).All(n => (placedOnly ? n.Placed : n.Placed + n.Owned) >= 1);
        }

        private static string FitText(GameSession s, bool placedOnly)
        {
            if (!s.HasShop) return "先租铺子";
            return string.Join("　", s.ShopFitList().Where(n => n.Required).Select(n => $"{n.Name} {Math.Min(1, placedOnly ? n.Placed : n.Placed + n.Owned)}/1"));
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
        /// <summary>只派发当前经营阶段可做的一步；没有合适任务时返回列表长度。</summary>
        public int QuestIndex()
        {
            RefreshQuestProgress();
            for (int i = 0; i < Quests.All.Count; i++)
            {
                var q = Quests.All[i];
                if (QuestCompleted(q.Id) || !QuestStageReady(q)) continue;
                Flags.Add("tutorial:issued:" + q.Id);
                return i;
            }
            return Quests.All.Count;
        }

        /// <summary>当前任务；阶段条件未到或全部完成时返回 null，不预告未来任务。</summary>
        public QuestDef CurrentQuest()
        {
            int i = QuestIndex();
            return i < Quests.All.Count ? Quests.All[i] : null;
        }
    }
}
