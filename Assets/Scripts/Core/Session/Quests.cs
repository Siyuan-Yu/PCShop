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

        /// <summary>完成奖励的说明；没有奖励为 null。</summary>
        public string Reward;

        /// <summary>完成时发奖励（只发一次，记在完成记号里）。</summary>
        public Action<GameSession> Grant;
        public Func<GameSession,string> RewardFor;
        public string RewardText(GameSession s)=>RewardFor?.Invoke(s)??Reward;
        public QuestDef Copy()=>(QuestDef)MemberwiseClone();
    }

    public static partial class Quests
    {
        public static readonly IReadOnlyList<QuestDef> All = new[]
        {
            new QuestDef
            {
                Id = "first_meal", Title = "做第一顿饭", Target = QuestTarget.Cooker,
                Short = $"打开家里的{K("电饭锅")}，开始煮饭",
                Hint = $"选白饭和用米量，点开始焖饭。饭好后可以{K("现在吃或装进背包")}。饭盒在饭点或饿了时会自动吃。",
                Done = s => !s.OpeningMealTutorialPending,
                Progress = s => s.Pantry.Cooking != null
                    ? $"还要 {s.Pantry.CookMinutesLeft(s.Clock.Now.TotalMinutes)} 分钟"
                    : s.Pantry.CookedBowls > 0 ? $"锅里有 {s.Pantry.CookedBowls} 碗熟饭" : null,
            },
            new QuestDef
            {
                Id = "rent", Title = "租一间铺子", Target = QuestTarget.Agency,
                Short = $"去{K("安居房产中介")}租一间铺子",
                Hint = $"签约要付{K("首周租金 + 押金 + 中介费")}，{K("预留开店设备和进货费用")}。",
                Done = s => s.HasShop,
                Progress = s => s.PendingLease != null
                    ? $"已提前签下{s.City.GetPlace(s.PendingLease.PlaceId).Name}，第 {s.PendingLease.StartDay + 1} 天才交房"
                    : null,
            },
            new QuestDef
            {
                Id = "mall", Title = "购齐开店设施", Target = QuestTarget.Mall,
                Short = $"去{K("宜居家具城")}买收银台和货架",
                Hint = "备齐该店型所需的开店设施。",
                Done = s => s.HasShop && s.MissingOpeningFurniture().Length == 0,
                RewardFor=s=>s.OpeningRewardText("mall"),
                Grant = s => s.GrantOpeningReward("mall"),
            },
            new QuestDef
            {
                Id = "place", Title = "歇业装修，把设施摆好", Target = QuestTarget.Renovate,
                Short = $"在店里{K("装修")}，摆好收银台和货架",
                Hint = $"摆完{K("第二天开业")}。",
                Done = s => FitDone(s, true),
                Progress = s => FitText(s, true),
            },
            new QuestDef
            {
                Id = "shelf", Title = "设置第一个配件货架", Target = QuestTarget.Stock,
                Short = $"把货架设成{K("处理器或显卡")}",
                Hint = $"顾客{K("只买货架上的货")}。",
                Done = s => ShelvesSet(s) >= 1,
            },
            new QuestDef
            {
                Id = "stock", Title = "进第一批货", Target = QuestTarget.Market,
                Short = $"店里要有货，缺货去{K("电子市场")}进",
                Hint = $"先卖{K("奖励的 CPU 和显卡")}。",
                Done = s => StockCount(s) >= 1,
            },
            new QuestDef
            {
                Id = "sale", Title = "开业，卖出第一单", Target = QuestTarget.Counter,
                Short = $"守在店里，给顾客{K("结账")}",
                Hint = $"{K("没人看店就关门")}。",
                Done = s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.SalesCategory || e.Category == "餐饮"&&e.Amount.IsPositive),
            },
            new QuestDef
            {
                Id = "hire", Title = "雇一名店员", Target = QuestTarget.Staff,
                Short = $"雇一个人{K("看店")}",
                Hint = $"有人看店，{K("你才能出门进货")}。",
                Done = s => s.Employee != null || s.EverHired,
            },
            new QuestDef
            {
                Id = "phone", Title = "买一部智能手机", Target = QuestTarget.PhoneShop,
                Short = $"去{K("手机店")}买智能手机",
                Hint = $"能{K("网上进货")}，也是{K("开收款码的前提")}。",
                Done = s => s.HasSmartphone,
            },
            new QuestDef
            {
                Id = "qr", Title = "开通收款码", Target = QuestTarget.PhoneShop,
                Short = $"去{K("手机店")}开通收款码",
                Hint = $"不带现金的顾客{K("也能买单")}。",
                Done = s => s.HasUpgrade(ShopUpgrades.Qr),
            },
        }.Concat(GrowthSteps()).ToArray();

        public static QuestDef Get(string id) => All.FirstOrDefault(q => q.Id == id);

        /// <summary>任务文字里的关键信息（uGUI 富文本高亮）。</summary>
        private static string K(string text) => "<color=#FFC966>" + text + "</color>";

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
            if(s.IsSpecialBusiness(s.CurrentShop))return s.SpecialFitGap(s.CurrentShop)==null?1:0;
            if(s.RetailKind!="computer")return s.ShelfIndexes().Count(i=>s.ShelfKindAt(i)?.GiftIds.Any(id=>RetailShopTypes.Sells(s.RetailKind,id))==true);
            return FurnitureCatalog.ShelfCategories.Count(c => s.ShelvesOfKind(ShelfKinds.ForCategory(c).Id) > 0);
        }

        private static int StockCount(GameSession s) => s.RetailKind=="restaurant"
            ?(s.KitchenRice(s.CurrentShop)>=.25?GameSession.RecipeIngredients.Sum(id=>s.KitchenIngredientCount(s.CurrentShop,id)):0)
            :s.RetailKind=="secondhand"?s.SpecialBusiness(s.CurrentShop.Place.Id).Used.Count(i=>i.Owned&&!i.Sold)
            :s.RetailKind!="computer"?s.PlayerShop.Stock.GoodsLines().Where(g=>RetailShopTypes.Sells(s.RetailKind,g.Key)).Sum(g=>g.Value)
            :PartCatalog.All.Sum(p => s.PlayerShop.Stock.CountAll(p));

        public static QuestDef ForShop(QuestDef source,GameSession s)
        {
            if (source?.Target == QuestTarget.Cooker)
            {
                if (s.Pantry.Cooking == null && s.Pantry.CookedBowls == 0) return source;
                var meal = source.Copy();
                meal.Short = s.Pantry.Cooking != null ? "等饭煮好，再吃或装进背包" : "吃一碗，或装一盒放进背包";
                return meal;
            }
            if(source?.Id=="mall" && s.HasShop)
            {
                var purchase=source.Copy();
                var required=GameSession.OpeningFurniture(s.RetailKind).Select(FurnitureCatalog.Get).ToArray();
                var missing=s.MissingOpeningFurniture().Select(FurnitureCatalog.Get).ToArray();
                purchase.Title=RetailShopTypes.Name(s.RetailKind)+" · 购齐开店设施";
                purchase.Short=missing.Length==0?"开店设施已备齐":"去家具城买"+string.Join("、",missing.Take(2).Select(f=>f.Name))+(missing.Length>2?"等 "+missing.Length+" 件":"");
                purchase.Hint="采购清单\n"+string.Join("\n",required.Select(f=>(missing.Contains(f)?"待买  ":"已备  ")+f.Name+" ×1 · "+f.Price));
                purchase.Progress=_=>"已备 "+(required.Length-missing.Length)+"/"+required.Length+" 项";
                return purchase;
            }
            if(source==null||s.RetailKind=="computer")return source;
            var q=source.Copy();
            if(q.Id=="place")q.Short="在店里装修，摆好"+string.Join("、",GameSession.OpeningFurniture(s.RetailKind).Select(id=>FurnitureCatalog.Get(id).Name));
            if(q.Id=="shelf")
            {
                q.Title=s.IsSpecialBusiness(s.CurrentShop)?"准备经营设施":"设置第一个商品货架";
                q.Short=s.IsSpecialBusiness(s.CurrentShop)?"按开店清单摆齐设施":"把货架设成与本店商品对应的品类";
                q.Hint=s.IsSpecialBusiness(s.CurrentShop)?"设施摆齐后才可接待顾客。":"顾客只买货架上已陈列的商品。";
            }
            if(q.Id=="stock")
            {
                q.Short=s.RetailKind=="restaurant"?"准备大米和配菜":s.RetailKind=="secondhand"?"准备可出售的二手商品":"准备本店品类的第一批商品";
                q.Hint="先使用开店任务赠送的库存，不足时再按本店品类进货。";
            }
            if(s.RetailKind==PetShopCatalog.ShopKind)
            {
                if(q.Id=="shelf") q.Short="把货架设成基础宠物用品，陈列首批商品";
                if(q.Id=="stock") q.Short="到暖爪宠物用品批发市场补货";
            }
            if(q.Id=="sale"&&s.RetailKind=="restaurant"){q.Short="接待一桌客人，完成备菜、烹饪与上菜";q.Hint="顾客吃完后付款，记入餐饮收入。";}
            return q;
        }
    }

    public sealed partial class GameSession
    {
        private const string OpeningMealTutorialFlag = "tutorial:opening-meal-pending";
        public bool OpeningMealTutorialPending => Flags.Contains(OpeningMealTutorialFlag);

        private void CompleteOpeningMealTutorial()
        {
            if (Flags.Remove(OpeningMealTutorialFlag)) RefreshQuestProgress();
        }

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
            if(i>=Quests.All.Count)return null;
            if(RetailKind!="computer"&&Quests.All[i].Chapter>0)return null;
            return Quests.ForShop(Quests.All[i],this);
        }
    }
}
