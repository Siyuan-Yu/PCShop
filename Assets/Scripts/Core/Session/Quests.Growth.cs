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
            yield return Step("pricing", 1, QuestTarget.Pricing, "先看懂售价与收入", "打开定价页，确认当前定价，再观察顾客流失",
                "售价跟随今日市场价和加价倍率，盒装另加 12%。报价差额没有扣采购成本、运费和工资，营业额也不是利润。先看当前倍率与愿买概率，可以保留现价；在定价页点击“已了解，完成这一步”，无需为教学乱改价格。",
                s => s.QuestLessonRead("pricing"), s => $"当前全局倍率 {s.PlayerShop.Markup:0.00}× · 愿买概率约 {ShopSettings.BuyChance(s.PlayerShop.Markup):P0}", true);
            yield return Step("workbench", 1, QuestTarget.Renovate, "准备整机工作区", "添置并摆好一张装机台和一张展示桌",
                "去家具城买装机操作台与整机展示桌，送店后在“装修”里摆放；每张展示桌有两个机位。装修当天歇业，第二天恢复营业。小店空间有限，留出过道。整机订单会自然出现，展示整机还能增加曝光；没有展示样机也可以接单。",
                s => s.PlacedFixtures(FixtureKind.Bench) > 0 && s.PlacedFixtures(FixtureKind.Display) > 0,
                s => $"装机台 {Math.Min(1, s.PlacedFixtures(FixtureKind.Bench))}/1 · 展示桌 {Math.Min(1, s.PlacedFixtures(FixtureKind.Display))}/1");
            yield return Step("build", 1, QuestTarget.Orders, "交付第一台订单电脑", "查看订单 → 补齐缺件 → 预留配件 → 开始装机",
                "开门等装机顾客下单，然后在“订单”页看配置、报价与截止时间。点“缺件采购清单”核对缺件：网购次日 10:00 才到，急单去档口现买。配件齐全后先预留，避免被零售卖走，再开始装机。按小游戏提示把配件拖到对应槽位，完成后自动交付收款；员工交付也算完成这一步。",
                s => s.Ledger.Entries.Any(e => e.Category == ShopSimulation.BuildCategory && !(e.Memo ?? "").Contains("展示机")),
                BuildProgress);
            yield return Step("restock", 2, QuestTarget.Restock, "让补货有预算", "在 1788 设置常备数量、每日预算和至少 7 天运营保留",
                "手机应用商店安装 1788，再进“自动补货”。先为一款在售配件设小额常备数，设每日预算上限，并保留至少 7 天运营资金。每天 9:00 检查库存及在途，低于目标的 40% 时补货，次日 10:00 到。钱或仓位不足会少补或跳过；页面显示上次原因。设置即生效，不必为教学手动下单。",
                s => s.HasShop && s.AppBlocker(PhoneApps.Wholesale) == null && s.RestockTargets.Any() && s.RestockDailyBudgetYuan > 0 && s.RestockReserveDays >= 7,
                s => $"{s.AppBlocker(PhoneApps.Wholesale) ?? "1788 可用"} · 常备 {s.RestockTargets.Count()} 款\n每日预算 {(s.RestockDailyBudgetYuan > 0 ? "¥" + s.RestockDailyBudgetYuan : "尚未限制")} · 运营保留 {s.RestockReserveDays}/7 天 · 当前保留额 {s.EffectiveRestockReserve}");
            yield return Step("automation", 2, QuestTarget.Staff, "把装机交给店员", "培训店员或聘请装机师，并开启自动装机",
                "在“招人”页培训现有店员（¥1,200，日薪增加 ¥60），或另外招聘专职装机师；确认自动装机开启。普通店员优先收银，空闲时处理最早到期且配件齐全的订单；专职装机师与收银员同时在岗时可分工。检查班次、库存和剩余交期，开了自动也不会凭空变出配件。",
                s => (s.EmployeeTrained || s.Assembler != null) && s.AutoBuildEnabled,
                s => $"{(s.EmployeeTrained || s.Assembler != null ? "已有会装机的员工" : "需要培训或招聘装机师")} · 自动装机{(s.AutoBuildEnabled ? "已开" : "未开")}");
            yield return Step("cashflow", 2, QuestTarget.Finance, "留住能周转的现金", "查看资产与负债，辨认现金、库存和到期账款",
                "净资产包含库存、押金、物业等，不等于能立即花的钱。分店的收入留在各店周转金里，转回总部才增加现金。先留进货、房租、工资和到期货款，再扩张或投资。直供可全款，无需为了教学借款；赊账 14 天结清，按揭每月还本付息。现金收支报表不是利润表。阅读页面后确认即可。",
                s => s.QuestLessonRead("cashflow"), s => $"现金 {s.Ledger.Balance} · 负债 {s.TotalDebt}\n未来 7 天货款 / 月供 / 物业维护 {s.UpcomingBusinessPayments(7)}", true);
            yield return Step("enterprise", 3, QuestTarget.Enterprise, "完成第一份企业合同", "知名度到 8 后，评估采购与工时，再接批量订单",
                "“企业合同”每周更新邀请。接单需主店、智能手机和知名度 8，先缴合同额 5% 的保证金。先估算全部配件和工时，再决定是否接受；合同进入普通装机队列，逐台交付收款。全部按时交齐返还保证金并奖励 3%；取消或超时会扣未履约部分保证金。资金不足时先继续零售积累。",
                s => s.EnterpriseContracts.Any(c => c.Settled && c.Completed == c.Count && c.Failed == 0), EnterpriseProgress);
            yield return Step("direct", 3, QuestTarget.Direct, "建立稳定直供", "与老周交情到 40、知名度到 10 后签约，完成一笔直供采购",
                "第 5 天同行会介绍老周与他的网店，认识后从“目标与人脉”交谈、送礼或约活动积累交情。达到条件后在“直供采购”免费签约。每型号 3–30 件，市场价 84%，次日 10:00 到主店。首批按真实需求选购，可全款，也可首付 30%、尾款 14 天内结清；教学不要求赊账。已有到期货款时先还清。",
                s => s.DirectShipments.Count > 0 || s.Ledger.Entries.Any(e => e.Category == "进货" && (e.Memo ?? "").StartsWith("直供首付/现款", StringComparison.Ordinal)),
                s => s.DirectAgreement ? $"协议已签 · 在途 {s.DirectShipments.Sum(x => x.Count)} 件 · 待还 {s.SupplierDebtTotal}" : $"知名度 {s.PlayerShop.Reputation:0}/10 · 老周交情 {s.Relation(Npcs.Zhou)}/40\n{s.DirectAgreementBlocker ?? "已可免费签约"}");
            yield return Step("branch", 3, QuestTarget.Ventures, "开设第一家托管门店", "主店现金稳定后，到中介挑铺面和业态",
                "电脑分店需要知名度 12、¥28,000 装修设备费，另加租赁签约款和 ¥5,000 周转金。也可以选其他符合条件的业态。去中介签约前看总价，预留主店经营资金；开办次日起由店长结算。网吧类还需从主店调拨成套配件。开设任意街边托管门店都算完成，不必只选电脑店。",
                s => s.ManagedVentures.Any(v => !s.IsParkVenture(v)), BranchProgress);
            yield return Step("branch_review", 3, QuestTarget.Ventures, "检查分店第一天结算", "等分店结算一天，再查看周转金、成本和维护状态",
                "开店次日起每天结算。比较昨日收入和成本，观察周转金是否能覆盖工资、租金及维护。补资、调价或暂停营业由你决定，教学不要求额外花钱。停业仍付租金和 20% 留任工资；周转金不足时总部还要承担固定费用。看完首日结算后在本页确认。",
                s => s.QuestLessonRead("branch_review"),
                s => s.ManagedVentures.Any(v => !s.IsParkVenture(v) && v.LastSettledDay > v.OpenDay) ? $"已产生首日结算 · 各店周转金合计 {s.VentureReserve}" : "先开一家街边托管门店，等到次日结算；切回柜台或关闭暂停弹窗让时间继续。",
                true, s => s.ManagedVentures.Any(v => !s.IsParkVenture(v) && v.LastSettledDay > v.OpenDay));
            yield return Step("park_plan", 4, QuestTarget.Park, "先算一遍园区预算", "了解买地、七天改造、入驻资金与相邻业态加成",
                "园区是长期目标：主店知名度 35，买当前空置的大型铺面；地价至少 ¥300,000，另加 ¥50,000 公共改造费，施工 7 天。每个铺位还需业态装修费和 ¥5,000 周转金，物业按月收维护费。相邻且营业中的不同业态每家带来 8% 导流，最多 24%。先阅读规划，不必马上买地。",
                s => s.QuestLessonRead("park_plan"), s => $"知名度 {s.PlayerShop.Reputation:0}/35 · 现金 {s.Ledger.Balance}\n买地改造至少 ¥350,000，尚未含各店入驻与运营资金", true);
            yield return Step("park", 4, QuestTarget.Park, "迎来园区第一家店", "资金与知名度充足后买地，完工后让第一家业态入驻",
                "去房产中介签园区购置合同，等待 7 天改造。完工后打开“园区布局”，选空铺位和业态入驻；在“分店经营”管理资金和维护。先开一家再按现金流扩充，没必要一次填满六格。资金不足时先继续主店经营。",
                s => s.HasPark && s.ManagedVentures.Any(s.IsParkVenture), ParkProgress);
            yield return Step("creator", 5, QuestTarget.Creators, "尝试一次达人推广", "先稳住供货，再从合适的创作者开始七天合作",
                "推广是可选路线。看粉丝、费用、资源和交情后决定合作；同一时间只有一位主店推广伙伴。先准备库存和人手，资源不足可赞助，推广表现差可能提前结束。合作期营业额含自然客流，不能当作推广新增利润。以后知名度 25、资金充足时再考虑成立 MCN，无需现在签长期合约。",
                s => s.CreatorPartners.Any(c => c.CampaignUntilDay > 0),
                s => s.CreatorPartners.Any(c => c.CampaignUntilDay > s.Clock.Today.TotalDays) ? "已有进行中的推广合作" : "经营 → 推广合作：逐人查看条件与费用，按需要决定是否合作");
            yield return Step("property_lesson", 5, QuestTarget.Property, "看懂住房与物业选择", "比较租住、全款、按揭的现金压力，再决定是否购买",
                "这是可选知识页，不要求买房。换住处先付首月租金和搬家费；购房可全款或首付 30%。按揭分 36 期等额本金、月利率 0.4%，另有房价 0.1% 的月维护费。商铺自有后免租，但仍需维护；出售按买入价九折并先还贷款。先看报价和月供，再决定是否值得把流动资金投入物业。",
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
