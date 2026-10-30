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
            yield return Step("pricing", 1, QuestTarget.Pricing, "先看懂售价与收入", "打开定价页，看懂售价后确认",
                $"售价 = 当天市场价 × {K("加价倍率")}，盒装再贵 12%。营业额{K("不是利润")}，还要扣进价、运费和工资。看懂后点{K("“已了解，完成这一步”")}，不必改价。",
                s => s.QuestLessonRead("pricing"), s => $"当前全局倍率 {s.PlayerShop.Markup:0.00}× · 愿买概率约 {ShopSettings.BuyChance(s.PlayerShop.Markup):P0}", true);
            yield return Step("workbench", 1, QuestTarget.Renovate, "准备整机工作区", $"买并摆好{K("装机台")}和{K("展示桌")}",
                $"去{K("宜居家具城")}买，再到“装修”页摆好（当天歇业），记得留出过道。没有展示样机也能接整机单。",
                s => s.PlacedFixtures(FixtureKind.Bench) > 0 && s.PlacedFixtures(FixtureKind.Display) > 0,
                s => $"装机台 {Math.Min(1, s.PlacedFixtures(FixtureKind.Bench))}/1 · 展示桌 {Math.Min(1, s.PlacedFixtures(FixtureKind.Display))}/1");
            yield return Step("build", 1, QuestTarget.Orders, "交付第一台订单电脑", "订单页：补缺件 → 预留 → 装机",
                $"顾客下单后，在{K("“订单”页")}看配置和{K("截止时间")}。按“缺件采购清单”补齐（网购次日 10:00 到，急单去档口），先{K("预留配件")}再装机。员工交付也算。",
                s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.BuildCategory && !(e.Memo ?? "").Contains("展示机")),
                BuildProgress);
            yield return Step("restock", 2, QuestTarget.Restock, "让补货有预算", "在 1788 设常备数、每日预算、保留 ≥ 7 天",
                $"打开 1788 的{K("“自动补货”")}：给一款配件设常备数，设{K("每日预算")}，{K("运营保留至少 7 天")}。每天 9:00 自动补货，次日到。设好即生效。",
                s => s.HasShop && s.AppBlocker(PhoneApps.Wholesale) == null && s.RestockTargets.Any() && s.RestockDailyBudgetYuan > 0 && s.RestockReserveDays >= 7,
                s => $"{s.AppBlocker(PhoneApps.Wholesale) ?? "1788 可用"} · 常备 {s.RestockTargets.Count()} 款\n每日预算 {(s.RestockDailyBudgetYuan > 0 ? "¥" + s.RestockDailyBudgetYuan : "尚未限制")} · 运营保留 {s.RestockReserveDays}/7 天 · 当前保留额 {s.EffectiveRestockReserve}");
            yield return Step("automation", 2, QuestTarget.Staff, "把装机交给店员", "培训店员或招装机师，打开自动装机",
                $"在“招人”页{K("培训店员")}（¥1,200，日薪 +¥60）或招{K("专职装机师")}，再打开{K("自动装机")}。配件不齐也装不了。",
                s => (s.EmployeeTrained || s.Assembler != null) && s.AutoBuildEnabled,
                s => $"{(s.EmployeeTrained || s.Assembler != null ? "已有会装机的员工" : "需要培训或招聘装机师")} · 自动装机{(s.AutoBuildEnabled ? "已开" : "未开")}");
            yield return Step("cashflow", 2, QuestTarget.Finance, "留住能周转的现金", "看资产与负债，分清现金、库存和欠款",
                $"净资产含库存、押金、物业，{K("不等于能花的现金")}；分店收入在各店周转金里。先留够{K("进货、房租、工资和到期货款")}再扩张。看完确认即可。",
                s => s.QuestLessonRead("cashflow"), s => $"现金 {s.Ledger.Balance} · 负债 {s.TotalDebt}\n未来 7 天货款 / 月供 / 物业维护 {s.UpcomingBusinessPayments(7)}", true);
            yield return Step("enterprise", 3, QuestTarget.Enterprise, "完成第一份企业合同", "接一份企业合同并按时交齐",
                $"“企业合同”每周更新。接单先交{K("5% 保证金")}，先算好配件和工时。{K("按时交齐")}退保证金并奖励 3%，超时或取消扣保证金。",
                s => s.EnterpriseContracts.Any(c => c.Settled && c.Completed == c.Count && c.Failed == 0), EnterpriseProgress);
            yield return Step("direct", 3, QuestTarget.Direct, "建立稳定直供", "老周交情 40、知名度 10 后签直供并进一次货",
                $"和老周聊天、送礼、约活动涨交情。达标后在“直供采购”{K("免费签约")}，进价{K("市场价 84%")}，次日到。可全款，也可首付 30%、{K("14 天内结清")}。",
                s => s.DirectShipments.Count > 0 || s.Ledger.Entries.Any(e => e.Category == "进货" && (e.Memo ?? "").StartsWith("直供首付/现款", StringComparison.Ordinal)),
                s => s.DirectAgreement ? $"协议已签 · 在途 {s.DirectShipments.Sum(x => x.Count)} 件 · 待还 {s.SupplierDebtTotal}" : $"知名度 {s.PlayerShop.Reputation:0}/10 · 老周交情 {s.Relation(Npcs.Zhou)}/40\n{s.DirectAgreementBlocker ?? "已可免费签约"}");
            yield return Step("branch", 3, QuestTarget.Ventures, "开设第一家托管门店", "到中介开一家托管门店",
                $"电脑分店要{K("知名度 12")}、装修设备 ¥28,000，另加签约款和 ¥5,000 周转金；开其他业态也算。签约前{K("留足主店经营资金")}。",
                s => s.ManagedVentures.Any(v => !s.IsParkVenture(v)), BranchProgress);
            yield return Step("branch_review", 3, QuestTarget.Ventures, "检查分店第一天结算", "等分店结算一天，看周转金和成本",
                $"开店次日起每天结算。看{K("收入能否覆盖工资、租金和维护")}；停业也要付租金和 20% 工资。看完在本页确认。",
                s => s.QuestLessonRead("branch_review"),
                s => s.ManagedVentures.Any(v => !s.IsParkVenture(v) && v.LastSettledDay > v.OpenDay) ? $"已产生首日结算 · 各店周转金合计 {s.VentureReserve}" : "先开一家街边托管门店，等到次日结算；切回柜台或关闭暂停弹窗让时间继续。",
                true, s => s.ManagedVentures.Any(v => !s.IsParkVenture(v) && v.LastSettledDay > v.OpenDay));
            yield return Step("park_plan", 4, QuestTarget.Park, "先算一遍园区预算", "了解买地、改造和入驻的总花费",
                $"要{K("知名度 35")}，买空置大型铺面：地价至少 ¥300,000 + 改造 ¥50,000，{K("施工 7 天")}。每格还要装修费和周转金；相邻不同业态各带 8% 客流。不必马上买。",
                s => s.QuestLessonRead("park_plan"), s => $"知名度 {s.PlayerShop.Reputation:0}/35 · 现金 {s.Ledger.Balance}\n买地改造至少 ¥350,000，尚未含各店入驻与运营资金", true);
            yield return Step("park", 4, QuestTarget.Park, "迎来园区第一家店", "买地，完工后让第一家店入驻",
                $"到{K("房产中介")}签园区合同，等 7 天改造，再在{K("“园区布局”")}选铺位和业态。先开一家，按现金流再扩。",
                s => s.HasPark && s.ManagedVentures.Any(s.IsParkVenture), ParkProgress);
            yield return Step("creator", 5, QuestTarget.Creators, "尝试一次达人推广", "选一位创作者做七天推广",
                $"可选路线。看粉丝和费用再决定，{K("先备好库存和人手")}。合作期营业额含自然客流，{K("不全是推广带来的")}。",
                s => s.CreatorPartners.Any(c => c.CampaignUntilDay > 0),
                s => s.CreatorPartners.Any(c => c.CampaignUntilDay > s.Clock.Today.TotalDays) ? "已有进行中的推广合作" : "经营 → 推广合作：逐人查看条件与费用，按需要决定是否合作");
            yield return Step("property_lesson", 5, QuestTarget.Property, "看懂住房与物业选择", "比较租房、全款、按揭的现金压力",
                $"可选，不要求买房。购房可全款或{K("首付 30%")}，按揭 36 期、月利率 0.4%，另有维护费。{K("别把周转资金都压进物业")}。看完确认即可。",
                s => s.QuestLessonRead("property_lesson"), s => $"当前住处：{s.HomeTitle} · 房贷本金合计 {s.MortgageDebt}", true);
        }

        private static string BuildProgress(GameSession s)
        {
            var order = s.PlayerShop.Orders.OrderBy(o => o.DueAt.TotalMinutes).FirstOrDefault();
            if (order == null) return "暂时没有订单：开门营业等顾客，切回柜台页或关闭弹窗让时间继续。";
            return $"订单 #{order.Id} · 缺 {s.MissingParts(order).Count} 类配件 · 剩余 {Math.Max(0, s.Clock.Now.MinutesUntil(order.DueAt) / 60)} 小时\n{(order.PartsReserved ? "配件已预留" : "配件齐全后可先预留")} · {(order.EmployeeMinutesLeft > 0 ? "店员正在装机" : "可在订单页操作")}";
        }
        private static string EnterpriseProgress(GameSession s)
        {
            var active = s.EnterpriseContracts.FirstOrDefault(c => c.Accepted && !c.Settled);
            if (active != null) return $"{active.Client} · 已交 {active.Completed}/{active.Count} · 失败 {active.Failed}\n到订单页补件装机；剩余 {Math.Max(0, (active.DueMinute - s.Clock.Now.TotalMinutes) / 60)} 小时";
            var offer = s.EnterpriseContracts.FirstOrDefault(c => !c.Accepted && c.OfferUntilDay > s.Clock.Today.TotalDays);
            return $"知名度 {s.PlayerShop.Reputation:0}/8 · {(s.HasSmartphone ? "已有智能手机" : "还需智能手机")}\n{(offer == null ? "打开企业合同刷新本周邀请；先完成零售和装机积累知名度。" : s.EnterpriseBlocker(offer) ?? "本期合同可接，先核对采购资金和交期。")}";
        }
        private static string BranchProgress(GameSession s)
        {
            var listing = s.Listings.Where(l => l.OpenDay <= s.Clock.Today.TotalDays && l.CloseDay >= s.Clock.Today.TotalDays && !s.IsMyShop(s.City.GetPlace(l.PlaceId)) && s.VentureAt(l.PlaceId) == null).OrderBy(l => l.Rent.Cents).FirstOrDefault();
            return $"电脑分店知名度 {s.PlayerShop.Reputation:0}/12 · 现金 {s.Ledger.Balance}\n" + (listing == null ? "暂时没有空铺，等中介更新房源。" : $"最低挂牌开办总价 {s.VentureOpeningCost(listing, Ventures.Get("branch"))} · {s.VentureBlocker(listing.PlaceId, "branch") ?? "条件已满足，到中介签约"}");
        }
        private static string ParkProgress(GameSession s)
        {
            if (s.HasPark) return s.Clock.Today.TotalDays < s.ParkReadyDay ? $"正在改造，还需 {s.ParkReadyDay - s.Clock.Today.TotalDays} 天；可继续主店经营。" : "园区已完工：选一个空铺位，核对装修费、周转金与配件后入驻。";
            var site = s.Listings.FirstOrDefault(l => l.Size == ShopSize.Large && l.OpenDay <= s.Clock.Today.TotalDays && l.CloseDay >= s.Clock.Today.TotalDays);
            return site == null ? $"知名度 {s.PlayerShop.Reputation:0}/35 · 暂无空置大型铺面，等中介挂牌。" : $"地价及改造 {s.ParkLandPrice(site) + Money.FromYuan(50000)} · {s.ParkPurchaseBlocker(site) ?? "可以到中介签约"}";
        }
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
