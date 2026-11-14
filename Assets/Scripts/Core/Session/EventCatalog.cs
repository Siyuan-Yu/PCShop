using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 随机事件目录。每天 11:00 按概率抽一个满足条件的事件，弹出选项让玩家选。
    /// 数值是占位，等试玩后平衡；新增事件只需要在这里加一项。
    /// </summary>
    public static class EventCatalog
    {
        private static Money Y(long yuan) => Money.FromYuan(yuan);

        private static bool CanPay(GameSession s, long yuan) => s.Ledger.Balance >= Y(yuan);

        /// <summary>从店里随机拿走一件货，返回名字；店里没货返回 null。</summary>
        internal static string LoseRandomItem(GameSession s)
        {
            List<StockLine> lines = s.PlayerShop.Stock.Lines();
            if (lines.Count == 0) return null;
            StockLine l = lines[s.Random.Next(0, lines.Count)];
            s.PlayerShop.Stock.TryRemove(l.Part, l.Boxed, 1);
            return l.Part.Name;
        }

        private static readonly IReadOnlyList<EventDef> Base = new List<EventDef>
        {
            new EventDef
            {
                Id = "blackout", Title = "停电了", MinDay = 3, CooldownDays = 25, Weight = 8,
                When = s => s.NeedsEmergencyLighting,
            },
            new EventDef { Id = "inspection", Title = "市场监管来检查", When = s => s.InspectionShop() != null },
            new EventDef { Id = "regulars", Title = "熟客想带朋友", MinDay = 3, CooldownDays = 6, Weight = 10, When = s => s.ReferralShop() != null },
            new EventDef { Id = "thief", Title = "货少了", When = s => s.DiscrepancyShop() != null },
            new EventDef { Id = "students", Title = "学生团购", MinDay = 6, CooldownDays = 12, Weight = 9, When = s => s.RetailStoryTarget("students") != null },
            new EventDef { Id = "rival", Title = "同行价格战", MinDay = 7, CooldownDays = 14, Weight = 8, When = s => s.EventPromotion() != null },
            new EventDef { Id = "wallet", Title = "捡到钱包", When = s => s.CanFindWallet },
            new EventDef { Id = "friend_meal", Title = "老同学请吃饭", When = s => s.CanOfferClassmateDinner },
            new EventDef { Id = "lend", Title = "亲戚来借钱", When = s => s.CanOfferRelativeLoan },
            new EventDef { Id = "classmate_help", Title = "老同学雪中送炭", Group = "npc",
                When = s => s.NegativeCashDays >= 3 && s.Ledger.Balance.IsNegative },
            new EventDef { Id = "flyer", Title = "发传单的上门了", When = s => s.FlyerShop() != null },
            new EventDef { Id = "cold", Title = "身体不舒服", When = s => s.CanOfferColdEvent },
            new EventDef { Id = "intro_zhang", Title = "戴眼镜的熟客", Group = "npc", When = s => s.CanOfferShopIntroduction(Npcs.Zhang) },
            new EventDef { Id = "intro_landlady", Title = "房东上门", Group = "npc", When = s => s.CanOfferShopIntroduction(Npcs.Landlady) },
            new EventDef { Id = "intro_lin", Title = "发小来看你", Group = "npc", When = s => s.CanOfferShopIntroduction(Npcs.Lin) },
            new EventDef { Id = "zhang_visit", Title = "张绍文来闲坐", Group = "npc",
                When = s => s.InPlayerShop && s.CitySamePlace(Npcs.Zhang) && s.IsMet(Npcs.Zhang) && s.Relation(Npcs.Zhang) < 95 },
            new EventDef { Id = "zhang_family", Title = "张绍文介绍亲戚", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Zhang) && s.Relation(Npcs.Zhang) >= 40 && s.FindReferralShop(1) != null },
            new EventDef { Id = "landlady_rent", Title = "房东想涨租", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Landlady) && s.IsMet(Npcs.Landlady) && s.Relation(Npcs.Landlady) < 70 && s.RentNegotiationShop() != null },
            new EventDef { Id = "landlady_help", Title = "李秀兰介绍大客户", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Landlady) && s.Relation(Npcs.Landlady) >= 55 && s.FindReferralShop(3) != null },
            new EventDef { Id = "jie_loan", Title = "唐俊杰周转不开", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Jie) && s.IsMet(Npcs.Jie) && s.Relation(Npcs.Jie) >= 15 && !s.HasJieLoan },
            new EventDef { Id = "jie_trade", Title = "唐俊杰缺个配件", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Jie) && s.Relation(Npcs.Jie) >= 50 && s.JieTradeShop() != null },
            new EventDef { Id = "lin_visit", Title = "林远航来吐槽", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Lin) && s.IsMet(Npcs.Lin) && s.Relation(Npcs.Lin) < 95 },
            new EventDef { Id = "lin_loan", Title = "林远航想借钱", Group = "npc",
                When = s => s.CitySamePlace(Npcs.Lin) && s.IsMet(Npcs.Lin) && s.Relation(Npcs.Lin) >= 30 && !s.HasLinLoan },
            new EventDef { Id = "race_xinglan_invite", Title = "星澜约你上赛道", Group = "race",
                When = s => s.FindEventRace("racer_0") != null },
            new EventDef { Id = "race_zhixia_club", Title = "车友俱乐部的邀请", Group = "race",
                When = s => s.CanArrangeClubGathering },
            new EventDef { Id = "race_jingxing_tuning", Title = "赛前整备", Group = "race",
                When = s => s.EventTuningVehicles().Any() },
            new EventDef { Id = "race_ruoning_rare", Title = "收藏车的试驾", Group = "race", When = s => s.CanOfferCollectorDrive },
            new EventDef { Id = "race_ming_lunch", Title = "待业的朋友约比赛", Group = "race",
                When = s => s.CanOfferMingRace },
            new EventDef { Id = "race_rider_bet", Title = "骑手的小挑战", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_rider_bet") },
            new EventDef { Id = "race_battery_old", Title = "电池掉电", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_battery_old") },
            new EventDef { Id = "race_rematch", Title = "再来一场？", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_rematch") },
            new EventDef { Id = "race_sponsor_sticker", Title = "车身贴纸赞助", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_sponsor_sticker") },
            new EventDef { Id = "race_crash_help", Title = "弯道边的意外", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_crash_help") },
            new EventDef { Id = "race_spectator", Title = "来赛道看看", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_spectator") },
            new EventDef { Id = "race_charity", Title = "慈善赛", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_charity") },
            new EventDef { Id = "race_maker_gear", Title = "旧护具", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_maker_gear") },
            new EventDef { Id = "race_night_ride", Title = "夜骑小队", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_night_ride") },
            new EventDef { Id = "race_helmet_deal", Title = "头盔清仓", Group = "race", When = s => s.CanOfferRacingBatchEvent("race_helmet_deal") },
            new EventDef { Id = "race_prize_dinner", Title = "庆功饭", Group = "race", When = s => s.CanOfferSocialBatchEvent("race_prize_dinner") },
            new EventDef { Id = "race_track_visit_rain", Title = "湿地驾驶体验", Group = "race", When = s => s.CanOfferSocialBatchEvent("race_track_visit_rain") },
            new EventDef { Id = "race_ning_study", Title = "夜校同学爱好赛车", Group = "race", When = s => s.CanOfferSocialBatchEvent("race_ning_study") },
            new EventDef { Id = "act_dinner_cook", Title = "罗师傅请你尝新菜", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_dinner_cook") },
            new EventDef { Id = "act_dinner_chef", Title = "同桌家常馆的邀请", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_dinner_chef") },
            new EventDef { Id = "act_dinner_linhe", Title = "社区餐厅领班约饭", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_dinner_linhe") },
            new EventDef { Id = "act_dinner_farm", Title = "农贸市场的王婶请客", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_dinner_farm") },
            new EventDef { Id = "act_ktv_singer", Title = "练歌房的新歌", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_ktv_singer") },
            new EventDef { Id = "act_ktv_fuzhi", Title = "音响助理的调试局", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_ktv_fuzhi") },
            new EventDef { Id = "act_ktv_luoya", Title = "合唱团的练习", Group = "activity", When = s => s.CanOfferSocialBatchEvent("act_ktv_luoya") },
            new EventDef { Id = "act_fishing_shaoan", Title = "周末钓友的邀请", Group = "activity", When = s => s.CanOfferFishingInvitation(59) },
            new EventDef { Id = "act_fishing_qiaosong", Title = "退休老园艺的钓鱼局", Group = "activity", When = s => s.CanOfferFishingInvitation(60) },
            new EventDef { Id = "act_fishing_grandpa", Title = "周大爷约你钓鱼", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(61) },
            new EventDef { Id = "act_hike_luqiao", Title = "户外领队的郊游", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(62) },
            new EventDef { Id = "act_hike_gardener", Title = "公园维护员的步道", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(63) },
            new EventDef { Id = "act_hike_shiyang", Title = "绿化员带你认植物", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(64) },
            new EventDef { Id = "act_snack_moxin", Title = "便利店夜宵局", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(65) },
            new EventDef { Id = "act_snack_maoning", Title = "夜班保安的夜宵", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(66) },
            new EventDef { Id = "act_snack_rider", Title = "骑手收工宵夜", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(67) },
            new EventDef { Id = "act_market_zhou", Title = "周维成带你逛市场", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(68) },
            new EventDef { Id = "act_market_li", Title = "李承业的行情走访", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(69) },
            new EventDef { Id = "act_market_wang", Title = "显卡供货商的看货邀请", Group = "activity", When = s => s.CanOfferOutdoorBatchEvent(70) },
            new EventDef { Id = "act_expo_curator", Title = "会展策划的电子展邀请", Group = "activity", When = s => s.CanOfferExhibitionEvent(73) },
            new EventDef { Id = "act_oldmarket_guwen", Title = "钟表师傅的旧物局", Group = "activity", When = s => s.CanOfferOldMarketEvent(71) },
            new EventDef { Id = "act_oldmarket_leiming", Title = "电路维修员的旧物局", Group = "activity", When = s => s.CanOfferOldMarketEvent(72) },
            new EventDef { Id = "act_expo_hanzheng", Title = "展会电工的内部场", Group = "activity", When = s => s.CanOfferExhibitionEvent(74) },
            new EventDef { Id = "act_comic_creator", Title = "陆栀的原创潮玩展", Group = "activity", When = s => s.CanOfferExhibitionEvent(75) },
            new EventDef { Id = "act_comic_shenxiao", Title = "喜欢城市模型的小学生", Group = "activity", When = s => s.CanOfferExhibitionEvent(77) },
            new EventDef { Id = "shop_regular_review", Title = "熟客的好评", Group = "shop", When = s => s.CanOfferCustomerReview },
            new EventDef { Id = "shop_wrong_change", Title = "找错零钱", Group = "shop", When = s => s.CanOfferChangeDiscrepancy },
            new EventDef { Id = "shop_bulk_order", Title = "团购订单", Group = "shop", When = s => s.FindReferralShop(10) != null },
            new EventDef { Id = "shop_lost_delivery", Title = "快递丢件", Group = "shop", When = s => s.CanOfferShopBatch(81) },
            new EventDef { Id = "shop_price_war_leaflet", Title = "对面贴了降价单", Group = "shop", When = s => s.CanOfferShopBatch(82) },
            new EventDef { Id = "shop_cat_visitor", Title = "店里来了只猫", Group = "shop", When = s => s.CanOfferShopBatch(83) },
            new EventDef { Id = "shop_supplier_gift", Title = "供货商送样品", Group = "shop", When = s => s.CanOfferShopBatch(84) },
            new EventDef { Id = "shop_neighbor_help", Title = "邻居换招牌", Group = "shop", When = s => s.CanOfferShopBatch(85) },
            new EventDef { Id = "shop_complaint_online", Title = "差评", Group = "shop", When = s => s.CanOfferShopBatch(86) },
            new EventDef { Id = "shop_student_discount", Title = "学生党想要折扣", Group = "shop", When = s => s.CanOfferShopBatch(87) },
            new EventDef { Id = "shop_window_dressing", Title = "门面陈列建议", Group = "shop", When = s => s.CanOfferShopBatch(88) },
            new EventDef { Id = "shop_inventory_audit", Title = "库存变动待复核", Group = "shop", When = s => s.CanOfferShopBatch(89) },
            new EventDef { Id = "shop_summer_heat", Title = "热得没人逛", Group = "shop", When = s => s.CanOfferShopBatch(90) },
            new EventDef { Id = "shop_old_customer_return", Group = "shop", Title = "老客户回头", When = s => s.CanOfferFinanceBatch(91) },
            new EventDef { Id = "shop_flash_sale", Group = "shop", Title = "朋友圈限时抢", When = s => s.CanOfferFinanceBatch(92) },
            new EventDef { Id = "shop_delivery_boy", Group = "shop", Title = "骑手想歇个脚", When = s => s.CanOfferFinanceBatch(93) },
            new EventDef { Id = "shop_inspection_followup", Group = "shop", Title = "上次检查的回头看", When = s => s.CanOfferFinanceBatch(94) },
            new EventDef { Id = "shop_hot_item_rumor", Group = "shop", Title = "爆款传闻", When = s => s.CanOfferFinanceBatch(95) },
            new EventDef { Id = "shop_loyalty_card", Group = "shop", Title = "会员卡的点子", When = s => s.CanOfferFinanceBatch(96) },
            new EventDef { Id = "money_found_coupon", Group = "money", Title = "收到采购优惠券", When = s => s.CanOfferFinanceBatch(97) },
            new EventDef { Id = "money_tax_refund", Group = "money", Title = "经营补贴申请", When = s => s.CanOfferFinanceBatch(98) },
            new EventDef { Id = "money_phone_scam", Group = "money", Title = "诈骗电话", When = s => s.CanOfferFinanceBatch(99) },
            new EventDef { Id = "money_bank_manager", Group = "money", Title = "银行客户经理", When = s => s.CanOfferFinanceBatch(100) },
            new EventDef { Id = "money_utility_refund", Title = "水电多收了", Group = "money", When = s => s.CanOfferMoneyBatch(101) },
            new EventDef { Id = "money_win_lottery", Title = "刮刮乐", Group = "money", When = s => s.CanOfferMoneyBatch(103) },
            new EventDef { Id = "money_overpaid_rent", Title = "多交了房租", Group = "money", When = s => s.CanOfferMoneyBatch(105) },
            new EventDef { Id = "money_insurance_agent", Title = "住院保险", Group = "money", When = s => s.CanOfferMoneyBatch(106) },
            new EventDef { Id = "money_invest_tip", Title = "朋友推荐的理财", Group = "money", When = s => s.CanOfferMoneyBatch(107) },
            new EventDef { Id = "money_second_hand", Title = "闲置旧物处理", Group = "money", When = s => s.CanOfferMoneyBatch(109) },
            new EventDef { Id = "money_charity_box", Title = "社区募书", Group = "social", When = s => s.CanOfferLifeBatch(111) },
            new EventDef { Id = "life_flu_season", Title = "流感季", Group = "social", When = s => s.CanOfferLifeBatch(112) },
            new EventDef { Id = "life_neighbor_noise", Title = "邻居嫌吵", Group = "social", When = s => s.CanOfferLifeBatch(113) },
            new EventDef { Id = "life_pet_adopt", Title = "流浪狗", Group = "social", When = s => s.CanOfferLifeBatch(114) },
            new EventDef { Id = "life_street_music", Title = "街头音乐", Group = "social", When = s => s.CanOfferLifeBatch(115) },
            new EventDef { Id = "life_old_friend_text", Title = "老友的消息", Group = "social", When = s => s.CanOfferLifeBatch(116) },
            new EventDef { Id = "life_gym_trial", Title = "健身场体验", Group = "social", When = s => s.CanOfferLifeBatch(117) },
            new EventDef { Id = "life_book_swap", Title = "图书漂流", Group = "social", When = s => s.CanOfferLifeBatch(118) },
            new EventDef { Id = "life_night_class", Title = "夜校开班", Group = "social", When = s => s.CanOfferLifeBatch(119) },
            new EventDef { Id = "life_street_fair", Title = "社区集市", Group = "social", When = s => s.CanOfferLifeBatch(120) },
            new EventDef { Id = "life_blood_donation", Title = "献血车", Group = "social", When = s => s.CanOfferNeighborhood(121) },
            new EventDef { Id="act_bar_bartender",Title="灯塔吧台的新饮品",Group="activity",When=s=>s.CanOfferOnsite(51)},
            new EventDef { Id="act_bar_taoran",Title="跑团领队的聚会",Group="activity",When=s=>s.CanOfferOnsite(52)},
            new EventDef { Id="act_bar_zengyu",Title="饮品研发助理的试饮",Group="activity",When=s=>s.CanOfferOnsite(53)},
            new EventDef { Id="act_court_coach",Title="社区篮球教练的邀请",Group="activity",When=s=>s.CanOfferOnsite(54)},
            new EventDef { Id="act_court_dongyu",Title="球场管理员的邀请",Group="activity",When=s=>s.CanOfferOnsite(55)},
            new EventDef { Id="act_court_zhang",Title="张老师约你打球",Group="activity",When=s=>s.CanOfferOnsite(56)},
            new EventDef { Id="act_golf_golfer",Title="练习场体验",Group="activity",When=s=>s.CanOfferOnsite(57)},
            new EventDef { Id="act_golf_enterprise",Title="企业采购的高尔夫局",Group="activity",When=s=>s.CanOfferOnsite(58)},
            new EventDef { Id="act_comic_ningxi",Title="模型摄影师的邀请",Group="activity",When=s=>s.CanOfferOnsite(76)},
            new EventDef { Id = "life_lost_child", Title = "走丢的孩子", Group = "social", When = s => s.CanOfferNeighborhood(122) },
            new EventDef { Id = "life_roof_leak", Title = "屋顶漏水", Group = "social", When = s => s.CanOfferNeighborhood(123) },
            new EventDef { Id = "life_festival_lantern", Title = "社区花灯会", Group = "social", When = s => s.CanOfferNeighborhood(124) },
            new EventDef
            {
                Id = "review", Title = "网上有人说你家卖翻新货", MinDay = 10, CooldownDays = 14, Weight = 7,
                When = s => s.RetailStoryTarget("review")!=null,
                Body = "有人在本地论坛发帖，说你家的配件是翻新的，帖子下面已经有人跟帖。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "公开回应",
                        Apply = s =>
                        {
                            if (s.Random.Chance(ReviewChance(s)))
                            {
                                s.PlayerShop.AdjustShopReputation(1.5);
                                return "你把店里的情况讲清楚了，质疑渐渐平息。口碑 +1.5。";
                            }
                            s.PlayerShop.AdjustShopReputation(-1);
                            return "回应没说到点子上，被截图嘲了。口碑 -1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "请供货商协助核验来源 · ¥1,000",
                        Enabled = s => CanPay(s, 1000), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.Ledger.ForceDebit(s.Clock.Today, "来源核验", Y(1000), "请供货商协助核验来源",
                                PCShopSell.Core.Finance.LedgerAttribution.ForShop(s.CurrentShop.Place.Id));
                            return "供货商确认了商品来源，争议平息。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "无视",
                        Apply = s =>
                        {
                            s.PlayerShop.AdjustShopReputation(-2);
                            return "帖子越传越广，口碑 -2。";
                        },
                    },
                },
            },
        };

        internal static double ReviewChance(GameSession s) => EventScript.ProgressiveChance(.35,
            (s.EffectiveCharm, EventScript.CharmThreshold, .008),
            (s.Player.Intelligence, EventScript.IntelligenceThreshold, .004));

        /// <summary>内容写在 C# 里的事件（要绑定具体店铺、检查货架等，json 表达不了）。顺序和数值在 Resources/Data/events.json 里调。</summary>
        public static IReadOnlyList<EventDef> CodeEvents => Base.Concat(new[] { NpcEvents.ZhouDeal }).ToList();

        /// <summary>全部事件，顺序和数值以 Resources/Data/events.json 为准。</summary>
        public static IReadOnlyList<EventDef> All => _all ?? (_all = EventLoader.Load());
        private static IReadOnlyList<EventDef> _all;

        /// <summary>events.json 的文本从哪来。Game 层启动时设置（读 Resources），Core 自己不能引用 Unity。</summary>
        public static System.Func<string> JsonSource { get; set; }

        /// <summary>上次加载 events.json 发现的格式问题（空 = 没问题）。</summary>
        public static IReadOnlyList<string> LoadErrors { get; internal set; } = new List<string>();

        /// <summary>改了 events.json 之后重新读（编辑器里用）。</summary>
        public static void Reload() => _all = null;

        public static EventDef Get(string id) => All.FirstOrDefault(e => e.Id == id);
    }
}
