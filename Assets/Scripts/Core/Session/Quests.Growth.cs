using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public enum QuestTarget
    {
        Agency, Renovate, Stock, Market, Counter, Staff, PhoneShop, Upgrades, Mall,
        Pricing, Orders, Restock, Finance, Enterprise, Direct, Ventures, Park, Creators, Property
    }

    public static partial class Quests
    {
        public static readonly string[] Chapters = { "起步开店", "整机生意", "学会放手", "扩大经营", "电脑乐园", "自选路线" };

        private static QuestDef Step(string id, int chapter, QuestTarget target, string title, string brief,
            string hint, Func<GameSession, bool> done, Func<GameSession, string> progress,
            bool lesson = false, Func<GameSession, bool> ready = null) => new QuestDef
        {
            Id = id, Chapter = chapter, Target = target, Title = title, Short = brief, Hint = hint,
            Done = done, Progress = progress, Lesson = lesson, LessonReady = ready
        };

        private static IEnumerable<QuestDef> GrowthSteps()
        {
            yield return Step("pricing", 1, QuestTarget.Pricing, "先看懂售价与收入", "看懂定价页",
                $"营业额{K("不是利润")}，还要扣进价和工资。",
                s => s.QuestLessonRead("pricing"), null, true);
            yield return Step("workbench", 1, QuestTarget.Renovate, "准备整机工作区", $"摆好{K("装机台")}和{K("展示桌")}",
                $"有了它们才能{K("接整机单")}。",
                s => s.PlacedFixtures(FixtureKind.Bench) > 0 && s.PlacedFixtures(FixtureKind.Display) > 0,
                s => $"装机台 {Math.Min(1, s.PlacedFixtures(FixtureKind.Bench))}/1 · 展示桌 {Math.Min(1, s.PlacedFixtures(FixtureKind.Display))}/1");
            yield return Step("build", 1, QuestTarget.Orders, "交付第一台订单电脑", "装好一台顾客订的电脑",
                $"注意{K("截止时间")}，超时作废。",
                s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.BuildCategory && !(e.Memo ?? "").Contains("展示机")),
                BuildProgress);
            yield return Step("restock", 2, QuestTarget.Restock, "让补货有预算", $"设置 1788 {K("自动补货")}",
                $"设好{K("每日预算")}，{K("保留至少 7 天")}运营资金。",
                s => s.HasShop && s.AppBlocker(PhoneApps.Wholesale) == null && s.RestockTargets.Any() && s.RestockDailyBudgetYuan > 0 && s.RestockReserveDays >= 7,
                null);
            yield return Step("automation", 2, QuestTarget.Staff, "把装机交给店员", $"让店员会装机，打开{K("自动装机")}",
                $"{K("培训店员")}或招{K("装机师")}。",
                s => (s.EmployeeTrained || s.Assembler != null) && s.AutoBuildEnabled,
                null);
            yield return Step("cashflow", 2, QuestTarget.Finance, "留住能周转的现金", "看懂资产与负债",
                $"净资产{K("不等于能花的现金")}。",
                s => s.QuestLessonRead("cashflow"), null, true);
            yield return Step("enterprise", 3, QuestTarget.Enterprise, "完成第一份企业合同", "按时交齐一份企业合同",
                $"先交 {K("5% 保证金")}，{K("按时交齐")}才退还并有奖励。",
                s => s.EnterpriseContracts.Any(c => c.Settled && c.Completed == c.Count && c.Failed == 0), EnterpriseProgress);
            yield return Step("direct", 3, QuestTarget.Direct, "建立稳定直供", $"和{K("老周")}签直供，进一次货",
                $"进价只要{K("市场价 84%")}。",
                s => s.DirectShipments.Count > 0 || s.Ledger.Entries.Any(e => e.Category == "进货" && (e.Memo ?? "").StartsWith("直供首付/现款", StringComparison.Ordinal)),
                s => s.DirectAgreement ? null : $"知名度 {s.PlayerShop.Reputation:0}/10 · 老周交情 {s.Relation(Npcs.Zhou)}/40");
            yield return Step("branch", 3, QuestTarget.Ventures, "开设第一家托管门店", $"开一家{K("托管门店")}",
                $"{K("留足主店的周转资金")}再开。",
                s => s.ManagedVentures.Any(v => !s.IsParkVenture(v)),
                s => $"知名度 {s.PlayerShop.Reputation:0}/12");
            yield return Step("branch_review", 3, QuestTarget.Ventures, "检查分店第一天结算", "看分店第一天的结算",
                $"看{K("收入能否覆盖成本")}。",
                s => s.QuestLessonRead("branch_review"), null,
                true, s => s.ManagedVentures.Any(v => !s.IsParkVenture(v) && v.LastSettledDay > v.OpenDay));
            yield return Step("park_plan", 4, QuestTarget.Park, "先算一遍园区预算", "了解园区的总花费",
                $"要{K("知名度 35")}，买地加改造至少 {K("¥350,000")}。",
                s => s.QuestLessonRead("park_plan"), null, true);
            yield return Step("park", 4, QuestTarget.Park, "迎来园区第一家店", "让园区第一家店入驻",
                $"先开一家，{K("按现金流再扩")}。",
                s => s.HasPark && s.ManagedVentures.Any(s.IsParkVenture), ParkProgress);
            yield return Step("creator", 5, QuestTarget.Creators, "尝试一次达人推广", $"做一次{K("达人推广")}",
                $"{K("先备好库存和人手")}。",
                s => s.CreatorPartners.Any(c => c.CampaignUntilDay > 0), null);
            yield return Step("property_lesson", 5, QuestTarget.Property, "看懂住房与物业选择", "比较租房、买房和按揭",
                $"{K("别把周转资金都压进物业")}。",
                s => s.QuestLessonRead("property_lesson"), null, true);
        }

        private static string BuildProgress(GameSession s)
        {
            var order = s.PlayerShop.Orders.OrderBy(o => o.DueAt.TotalMinutes).FirstOrDefault();
            return order == null ? null : $"订单 #{order.Id} · 剩余 {Math.Max(0, s.Clock.Now.MinutesUntil(order.DueAt) / 60)} 小时";
        }
        private static string EnterpriseProgress(GameSession s)
        {
            var active = s.EnterpriseContracts.FirstOrDefault(c => c.Accepted && !c.Settled);
            return active == null ? null : $"已交 {active.Completed}/{active.Count} · 剩余 {Math.Max(0, (active.DueMinute - s.Clock.Now.TotalMinutes) / 60)} 小时";
        }
        private static string ParkProgress(GameSession s) =>
            s.HasPark && s.Clock.Today.TotalDays < s.ParkReadyDay ? $"改造中，还需 {s.ParkReadyDay - s.Clock.Today.TotalDays} 天" : null;
    }

    public sealed partial class GameSession
    {
        public bool QuestCompleted(string id) => Flags.Contains("tutorial:" + id);
        public bool QuestLessonRead(string id) => Flags.Contains("tutorial:read:" + id);
        public void RefreshQuestProgress()
        {
            // Ignore the old directory's arbitrary selection/skip state, preserving real achievements.
            Flags.RemoveWhere(f => f.StartsWith("tutorial:track:", StringComparison.Ordinal) || f.StartsWith("tutorial:defer:", StringComparison.Ordinal));
            // Observe every milestone, including out-of-order achievements; all flags use the existing save format.
            foreach (var q in Quests.All)
                if (!QuestCompleted(q.Id) && q.Done(this))
                {
                    Flags.Add("tutorial:" + q.Id);
                    if (q.Grant != null)
                    {
                        q.Grant(this);
                        Say($"任务完成「{q.Title}」，奖励：{q.Reward}。");
                    }
                }
        }
        public void VisitQuestTarget(QuestTarget target)
        {
            var q = CurrentQuest();
            if (q != null && q.Target == target) Flags.Add("tutorial:visited:" + q.Id);
        }
        public bool CompleteQuestLesson(string id)
        {
            var q = Quests.Get(id);
            if (IsGameOver || q == null || !q.Lesson || CurrentQuest()?.Id != id || !Flags.Contains("tutorial:visited:" + id) || (q.LessonReady != null && !q.LessonReady(this))) return false;
            Flags.Add("tutorial:read:" + id);
            RefreshQuestProgress();
            return true;
        }
        private bool QuestStageReady(QuestDef q)
        {
            if (IsGameOver) return false;
            if (q.Chapter == 0)
            {
                // Basic instructions follow actual opening progress, never an old manually pinned task.
                if (q.Id != "rent" && !HasShop) return false;
                return Quests.All.TakeWhile(x => x.Id != q.Id).All(x => QuestCompleted(x.Id));
            }
            if (!HasShop || Quests.All.Any(x => x.Chapter == 0 && !QuestCompleted(x.Id))) return false;
            // Once a real milestone has issued a task, a temporary cash/reputation dip won't erase it.
            if (Flags.Contains("tutorial:issued:" + q.Id)) return true;
            switch (q.Id)
            {
                case "pricing": return true;
                case "workbench": return QuestCompleted("pricing");
                case "build": return QuestCompleted("workbench");
                case "restock": return QuestCompleted("build") && HasSmartphone;
                case "automation": return QuestCompleted("restock") && (Employee != null || Assembler != null);
                case "cashflow": return QuestCompleted("automation") || TotalDebt.IsPositive || ManagedVentures.Count > 0;
            }
            if (!QuestCompleted("cashflow")) return false;
            switch (q.Id)
            {
                case "enterprise": return HasSmartphone && PlayerShop.Reputation >= 8;
                case "direct": return HasSmartphone && (DirectAgreement || DirectAgreementBlocker == null);
                case "branch": return Listings.Any(l => Ventures.All.Any(v => VentureBlocker(l.PlaceId, v.Id) == null));
                case "branch_review": return ManagedVentures.Any(v => !IsParkVenture(v) && v.LastSettledDay > v.OpenDay);
                case "park_plan": return HasPark || (QuestCompleted("branch_review") && Listings.Any(l => ParkPurchaseBlocker(l) == null));
                case "park": return QuestCompleted("park_plan") && (HasPark || Listings.Any(l => ParkPurchaseBlocker(l) == null));
                case "creator": return CreatorPartners.Any(c => CreatorCampaignBlocker(c) == null);
                case "property_lesson": return HomeTier > 0 || OwnedProperties.Count > 0 || Ledger.Balance >= HomePurchasePrice.Scale(.3);
                default: return false;
            }
        }
    }
}
