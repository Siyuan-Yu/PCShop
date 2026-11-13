using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed class EventChoice
    {
        public string Text;

        /// <summary>这个选项现在能不能选（比如钱够不够）。为 null 表示总能选。</summary>
        public Func<GameSession, bool> Enabled;

        /// <summary>灰掉时显示的原因。</summary>
        public string DisabledReason;

        /// <summary>执行选择，返回结果文字。</summary>
        public Func<GameSession, string> Apply;
    }

    public sealed class EventDef
    {
        public string Id;
        /// <summary>分组，目前只有 npc（围绕固定人物）用到。</summary>
        public string Group;
        public string Title;
        public string Body;
        public int MinDay;
        /// <summary>最晚触发的游戏天数；-1 = 没有上限。</summary>
        public int MaxDay = -1;
        /// <summary>每次轮到它时先掷一次，掷中才有资格被抽（0–1，默认 1）。</summary>
        public double TriggerChance = 1;
        public int CooldownDays = 10;
        public int Weight = 10;
        public Func<GameSession, bool> When;
        public List<EventChoice> Choices = new List<EventChoice>();
    }

    public sealed partial class GameSession
    {
        /// <summary>每天的抽取时刻：11:00、15:00、20:00。每个时刻各掷一次，没有未处理的事件才会抽。</summary>
        public static readonly int[] EventMinutes = { 11 * 60, 15 * 60, 20 * 60 };
        /// <summary>每个时刻出事件的概率（一天三次，满足条件的事件够多时约 1.2 个/天，实际受条件限制会少一些）。</summary>
        public const double EventSlotChance = 0.4;
        /// <summary>频率保障：每天最多 EventDayCap 个，任意连续 7 天最多 EventWeekCap 个；前 EventEarlyDays 天（第 0–13 天）概率只有一半。
        /// 只统计随机抽出来的事件，约会、剧情等不算。</summary>
        public const int EventDayCap = 3, EventWeekCap = 10, EventEarlyDays = 14;
        public const double EventEarlyFactor = 0.5;
        private readonly List<int> _randomEventDays = new List<int>();
        private readonly Dictionary<string, int> _eventLastDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _eventCount = new Dictionary<string, int>();
        private int _basePatience;

        /// <summary>随机事件开关。游戏运行时打开，单元测试默认关闭，保证老测试的数值不受影响。</summary>
        public bool EventsEnabled { get; set; }

        /// <summary>等玩家做选择的事件。有它时不能存档。</summary>
        public EventDef PendingEvent { get; private set; }

        public event Action<EventDef> EventRaised;

        public int EventCountOf(string id) => _eventCount.TryGetValue(id, out int n) ? n : 0;

        private void RollDailyEvent(GameDate today)
        {
            if (!EventsEnabled || IsGameOver || PendingEvent != null || _sleeping || !HasShop || BrandExpoExclusive) return;
            if (today.TotalDays < 2) return;
            int day = today.TotalDays;
            _randomEventDays.RemoveAll(d => day - d >= 7);
            if (_randomEventDays.Count(d => d == day) >= EventDayCap) return;
            if (_randomEventDays.Count >= EventWeekCap) return;
            double chance = day < EventEarlyDays ? EventSlotChance * EventEarlyFactor : EventSlotChance;
            if (!Random.Chance(chance)) return;

            var eligible = new List<EventDef>();
            bool raceEventsAvailable = RacingUnlocked && RacingIntroduction == RacingIntroStep.Complete;
            foreach (EventDef def in EventCatalog.All)
            {
                if (today.TotalDays < def.MinDay) continue;
                if (def.Group == "race" && !raceEventsAvailable && def.Id != "race_spectator") continue;
                if (def.MaxDay >= 0 && today.TotalDays > def.MaxDay) continue;
                if (_eventLastDay.TryGetValue(def.Id, out int last) && today.TotalDays - last < def.CooldownDays) continue;
                if (def.When != null && !def.When(this)) continue;
                if (def.TriggerChance < 1 && !Random.Chance(def.TriggerChance)) continue;
                eligible.Add(def);
            }
            if (eligible.Count == 0) return;

            int total = eligible.Sum(e => e.Weight);
            int roll = Random.Next(0, total);
            foreach (EventDef def in eligible)
            {
                roll -= def.Weight;
                if (roll >= 0) continue;
                Raise(def, today);
                if (PendingEvent != null) _randomEventDays.Add(day);
                return;
            }
        }

        /// <summary>直接触发某个事件（测试和脚本用）。</summary>
        public void RaiseEvent(string id)
        {
            EventDef def = EventCatalog.All.First(e => e.Id == id);
            Raise(def, Clock.Today);
        }

        private void Raise(EventDef def, GameDate today)
        {
            def = def.Id switch
            {
                "inspection" => BindInspection(def),
                "regulars" => BindReferral(def),
                "blackout" => BindBlackout(),
                "thief" => BindStockDiscrepancy(def),
                "students" => BindStudentOrder(def),
                "rival" => BindPriceWar(def),
                "wallet" => BindWallet(def),
                "friend_meal" => BindClassmateDinner(def, false),
                "lend" => BindRelativeLoan(def),
                "classmate_help" => BindClassmateDinner(def, true),
                "review" => BindRetailStory(def),
                "flyer" => BindFlyers(def),
                "cold" => BindCold(def),
                "intro_zhang" => BindShopIntroduction(def, Npcs.Zhang),
                "intro_landlady" => BindShopIntroduction(def, Npcs.Landlady),
                "intro_lin" => BindShopIntroduction(def, Npcs.Lin),
                "zhang_visit" => BindZhangVisit(def),
                "zhang_family" => BindIndustryReferral(def, Npcs.Zhang),
                "landlady_help" => BindIndustryReferral(def, Npcs.Landlady),
                "landlady_rent" => BindRentNegotiation(def, RentNegotiationShop(), true),
                "zhou_deal" => BindZhouClearance(def),
                "jie_loan" => BindJieLoan(def),
                "jie_trade" => BindJieTrade(def),
                "lin_visit" => BindLinVisit(def),
                "lin_loan" => BindLinLoan(def),
                "race_xinglan_invite" => BindEventRace(def, "racer_0"),
                "race_zhixia_club" => BindClubGathering(def),
                "race_jingxing_tuning" => BindEventTuning(def),
                "race_ruoning_rare" => BindCollectorDrive(def),
                "race_ming_lunch" => BindEventRace(def, "life_ming"),
                "race_rider_bet" or "race_battery_old" or "race_rematch" or "race_sponsor_sticker"
                    or "race_crash_help" or "race_spectator" or "race_charity" or "race_maker_gear"
                    or "race_night_ride" or "race_helmet_deal" => BindRacingBatchEvent(def),
                "race_prize_dinner" or "race_track_visit_rain" or "race_ning_study" or "act_dinner_cook"
                    or "act_dinner_chef" or "act_dinner_linhe" or "act_dinner_farm" or "act_ktv_singer"
                    or "act_ktv_fuzhi" or "act_ktv_luoya" => BindSocialBatchEvent(def),
                "act_fishing_shaoan" => BindFishingInvitation(def, 59),
                "act_fishing_qiaosong" => BindFishingInvitation(def, 60),
                "act_fishing_grandpa" => BindOutdoorBatchEvent(def, 61),
                "act_hike_luqiao" => BindOutdoorBatchEvent(def, 62),
                "act_hike_gardener" => BindOutdoorBatchEvent(def, 63),
                "act_hike_shiyang" => BindOutdoorBatchEvent(def, 64),
                "act_snack_moxin" => BindOutdoorBatchEvent(def, 65),
                "act_snack_maoning" => BindOutdoorBatchEvent(def, 66),
                "act_snack_rider" => BindOutdoorBatchEvent(def, 67),
                "act_market_zhou" => BindOutdoorBatchEvent(def, 68),
                "act_market_li" => BindOutdoorBatchEvent(def, 69),
                "act_market_wang" => BindOutdoorBatchEvent(def, 70),
                "act_expo_curator" => BindExhibitionEvent(def, 73),
                "act_oldmarket_guwen" => BindOldMarketEvent(def, 71),
                "act_oldmarket_leiming" => BindOldMarketEvent(def, 72),
                "act_expo_hanzheng" => BindExhibitionEvent(def, 74),
                "act_comic_creator" => BindExhibitionEvent(def, 75),
                "act_comic_shenxiao" => BindExhibitionEvent(def, 77),
                "shop_regular_review" => BindCustomerReview(def),
                "shop_wrong_change" => BindWrongChange(def),
                "shop_bulk_order" => BindCommunityBulkOrder(def),
                "shop_lost_delivery" => BindShopBatch(def, 81),
                "shop_price_war_leaflet" => BindShopBatch(def, 82),
                "shop_cat_visitor" => BindShopBatch(def, 83),
                "shop_supplier_gift" => BindShopBatch(def, 84),
                "shop_neighbor_help" => BindShopBatch(def, 85),
                "shop_complaint_online" => BindShopBatch(def, 86),
                "shop_student_discount" => BindShopBatch(def, 87),
                "shop_window_dressing" => BindShopBatch(def, 88),
                "shop_inventory_audit" => BindShopBatch(def, 89),
                "shop_summer_heat" => BindShopBatch(def, 90),
                "shop_old_customer_return" => BindFinanceBatch(def, 91),
                "shop_flash_sale" => BindFinanceBatch(def, 92),
                "shop_delivery_boy" => BindFinanceBatch(def, 93),
                "shop_inspection_followup" => BindFinanceBatch(def, 94),
                "shop_hot_item_rumor" => BindFinanceBatch(def, 95),
                "shop_loyalty_card" => BindFinanceBatch(def, 96),
                "money_found_coupon" => BindFinanceBatch(def, 97),
                "money_tax_refund" => BindFinanceBatch(def, 98),
                "money_phone_scam" => BindFinanceBatch(def, 99),
                "money_bank_manager" => BindFinanceBatch(def, 100),
                _ => def,
            };
            if (def == null) return;
            PendingEvent = def;
            _eventLastDay[def.Id] = today.TotalDays;
            _eventCount[def.Id] = EventCountOf(def.Id) + 1;
            EventRaised?.Invoke(def);
        }

        private const string LeisureInvitationId = "established-shop-leisure-invitation";
        private long _lastLeisureInvitationCheck = -1;

        private bool TryOfferLeisureInvitation()
        {
            long hour = Clock.Now.TotalMinutes / 60;
            if (!EventsEnabled || FirstChapterTrial || EventRaised == null || EventCountOf(LeisureInvitationId) > 0
                || _lastLeisureInvitationCheck == hour || Ledger.Balance < Money.FromYuan(50000)
                || !HasShop || !QuestCompleted("sale")) return false;
            _lastLeisureInvitationCheck = hour;
            var friend = _cityPeople.Values.Where(person => IsMet(person.PersonId) && Relation(person.PersonId) >= 30)
                .OrderByDescending(person => Relation(person.PersonId))
                .Select(person => CityPerson(person.PersonId)).FirstOrDefault(person => person != null);
            if (friend == null) return false;
            var invitation = BuildLeisureInvitation(friend);
            if (invitation == null) return false;
            Raise(invitation, Clock.Today);
            return true;
        }

        private EventDef BuildLeisureInvitation(CityPersonDefinition friend)
        {
            var activity = CityLifeCatalog.Activity(friend.Interest);
            if (activity == null || !City.HasPlace(activity.VenueId)
                || DistrictAccessBlocker(City.GetPlace(activity.VenueId)) != null
                || activity.Id.StartsWith("expo_", StringComparison.Ordinal))
                activity = CityLifeCatalog.Activity("dinner");
            if (activity == null || !City.HasPlace(activity.VenueId)
                || DistrictAccessBlocker(City.GetPlace(activity.VenueId)) != null) return null;
            long start = SuggestCityActivityStart(activity.Id, friend.Id);
            var people = new[] { friend.Id };
            if (CityActivityBlocker(activity.Id, people, start) != null) return null;
            var time = new GameTimestamp(start);
            string venue = City.GetPlace(activity.VenueId).Name;
            return new EventDef
            {
                Id = LeisureInvitationId,
                Title = friend.Name + "约你出去走走",
                Body = $"「最近店里忙得过来了吗？有空一起去{venue}吧。」\n{activity.Name} · {time}",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "答应，加入日程",
                        Apply = session =>
                        {
                            var appointment = session.ScheduleCityActivity(activity.Id, people, start, sourceId: LeisureInvitationId);
                            if (appointment == null) return "这个时段约不上了，可以在人物对话里另约时间。";
                            appointment.HostId = friend.Id;
                            return $"已约好 {time} 在{venue}见面，出发前可在日程查看。";
                        }
                    },
                    new EventChoice { Text = "改天再约", Apply = _ => null }
                }
            };
        }

        private const int EmergencyLightPrice = 150, PowerCutMinutes = 120;

        private static EventChoice Choose(string text, Func<GameSession, string> apply,
            Func<GameSession, bool> enabled = null, string reason = null) =>
            new EventChoice { Text = text, Apply = apply, Enabled = enabled, DisabledReason = reason };

        private static EventDef EventScene(EventDef template, string body, params EventChoice[] choices) =>
            new EventDef { Id = template.Id, Title = template.Title, Group = template.Group,
                Body = body, Choices = choices.ToList() };

        internal OwnedShop InspectionShop() => CurrentShop?.Rented == true ? CurrentShop : MyShops.FirstOrDefault();

        private bool HasPurchaseReceipt(OwnedShop shop) => Flags.Contains("inspection:receipts:" + shop.Place.Id)
            || Ledger.Entries.Any(entry => entry.ShopId == shop.Place.Id && entry.Attribution?.TransactionKind == "采购");

        private EventDef BindInspection(EventDef template)
        {
            var shop = InspectionShop();
            if (shop?.Rented != true) return null;
            bool equipment = IsSpecialBusiness(shop) ? SpecialFitGap(shop) == null : !shop.Sim.NotFitted;
            bool receipts = shop.Sim.Stock.IsEmpty || HasPurchaseReceipt(shop);
            string checks = $"开业设施：{(equipment ? "齐全" : "未齐")} · 进货记录：{(receipts ? "可核验" : "待补")}";
            if (equipment && receipts)
                return EventScene(template, $"{ShopTitle(shop)}\n{checks}。本次检查通过。",
                    Choose("确认检查结果", s => { shop.Sim.AdjustShopReputation(1); return "检查通过，门店口碑 +1。"; }));

            const int charmThreshold = EventScript.CharmThreshold;
            double Chance(GameSession s) => EventScript.ProgressiveChance(.35, (s.EffectiveCharm, charmThreshold, .01));
            var remediation = Money.FromYuan(300);
            string ScheduleInspection(GameSession session, int days)
            {
                int due = session.Clock.Today.TotalDays + days;
                session._inspectionFollowups[shop.Place.Id] = due;
                return $"请在 {new GameDate(due)} 前备齐开业设施和采购记录，届时复查。";
            }
            return EventScene(template, $"{ShopTitle(shop)}\n{checks}。你可以付费整改，或争取 7 天整改宽限。",
                Choose("现场整改 · ¥300", s =>
                {
                    s.Ledger.ForceDebit(s.Clock.Today, "整改", remediation, ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id));
                    s.Flags.Add("inspection:receipts:" + shop.Place.Id);
                    if (!equipment) return "消防和单据现场处理完毕。开业设施仍需补齐。" + ScheduleInspection(s, 3);
                    shop.Sim.AdjustShopReputation(1);
                    return "已补办并通过检查，门店口碑 +1。";
                }, s => s.Ledger.CanAfford(remediation), "现金不足"),
                Choose($"争取 7 天宽限 · 成功率 {Chance(this) * 100:0}%", s =>
                {
                    if (s.Random.Chance(Chance(s))) return ScheduleInspection(s, 7);
                    s.Ledger.ForceDebit(s.Clock.Today, "罚款", Money.FromYuan(500), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id));
                    shop.Sim.AdjustShopReputation(-2);
                    return "未争取到宽限，罚款 ¥500，门店口碑 −2。";
                }));
        }

        private readonly Dictionary<string, int> _inspectionFollowups = new Dictionary<string, int>();

        private EventDef BindStockDiscrepancy(EventDef template)
        {
            var shop = DiscrepancyShop();
            if (shop == null) return null;
            var products = EventShelfProducts(shop).ToArray();
            if (products.Length == 0) return null;
            var product = products[Random.Next(0, products.Length)];
            string kind = shop.RetailKind;
            int missing = Random.Next(1, Math.Min(3, product.Count) + 1);
            bool countingError = Random.Chance(.3);
            string Investigate(GameSession session, bool camera, bool report)
            {
                if (session.ShopAt(shop.Place.Id) != shop || shop.RetailKind != kind) return "门店状态已变化，本次未扣货。";
                string result;
                if (report && countingError) result = $"{ShopTitle(shop)} · {product.Name} ×{missing} 是盘点差错，实际库存完整。";
                else if (report && session.Random.Chance(camera ? .8 : .35))
                    result = $"{ShopTitle(shop)} · 找回了{product.Name} ×{missing}，库存已核对。";
                else
                {
                    int available = product.Part is PartDef part ? shop.Sim.Stock.Count(part, product.Boxed) : shop.Sim.Stock.GoodsCount(product.Id);
                    int loss = Math.Min(available, missing);
                    result = loss > 0 && RemoveEventProduct(shop, product, loss)
                        ? $"{ShopTitle(shop)} · {product.Name} ×{loss} 已登记损耗。" : "该商品已不在库存，本次无需再扣。";
                }
                if (camera) session.EventRest(15);
                return result;
            }
            return EventScene(template, $"{ShopTitle(shop)}盘点发现{product.Name}少了 {missing} 件。可能是记错，也可能真的丢了。",
                Choose("复盘并核查", s => Investigate(s, false, true)),
                Choose("查看监控，找到线索就报警 · 15 分钟", s => Investigate(s, true, true),
                    _ => shop.Upgrades.Contains(ShopUpgrades.Cctv), "需要安装监控"),
                Choose("直接登记损耗", s => Investigate(s, false, false)));
        }

        [Serializable]
        public sealed class EventStock
        {
            public string Id;
            public bool Boxed;
            public int Count, ExpiryDay;
            public bool Chilled;
            public PartDef Part => PartCatalog.Exists(Id) ? PartCatalog.Get(Id) : null;
            public string Name => Part?.Name ?? Gifts.Get(Id)?.Name ?? Id;
            public EventStock Copy() => (EventStock)MemberwiseClone();
        }

        private IEnumerable<EventStock> EventShelfProducts(OwnedShop shop)
        {
            if (shop.RetailKind == "computer")
                foreach (var line in PartCatalog.Categories.SelectMany(category => shop.Sim.ShelfFillOf(category).Parts))
                    yield return new EventStock { Id = line.Part.Id, Boxed = line.Boxed, Count = line.Count, ExpiryDay = -1 };
            foreach (var line in shop.Sim.GoodsOnShelves().Where(item => RetailShopTypes.Sells(shop.RetailKind, item.Key)))
                yield return new EventStock { Id = line.Key, Count = line.Value,
                    ExpiryDay = shop.Sim.Stock.GoodsExpiry(line.Key), Chilled = shop.Sim.Stock.GoodsChilled(line.Key) };
        }

        private static bool RemoveEventProduct(OwnedShop shop, EventStock product, int count) => product.Part is PartDef part
            ? shop.Sim.Stock.TryRemove(part, product.Boxed, count) : shop.Sim.Stock.TryRemoveGoods(product.Id, count);

        private static void RestoreEventProduct(OwnedShop shop, EventStock product, int count)
        {
            if (product.Part is PartDef part) shop.Sim.Stock.ForceAdd(part, product.Boxed, count);
            else shop.Sim.Stock.ForceAddGoods(product.Id, count, product.ExpiryDay, product.Chilled);
        }

        private static Money EventPrice(OwnedShop shop, EventStock product) => product.Part is PartDef part
            ? shop.Sim.PriceOf(part, product.Boxed) : shop.Sim.GoodsPriceOf(product.Id);

        private IEnumerable<OwnedShop> EventRetailShops() => MyShops.Where(shop => IsCompetingRetailShop(shop))
            .OrderBy(shop => shop == CurrentShop ? 0 : 1).ThenBy(shop => shop.Place.Id, StringComparer.Ordinal);

        internal OwnedShop DiscrepancyShop() => EventRetailShops().FirstOrDefault(shop => EventShelfProducts(shop).Any());
        internal OwnedShop ReferralShop() => EventRetailShops().FirstOrDefault(shop =>
            shop.Sim.Reputation >= 3 && !_eventReferrals.Any(visit => visit.ShopId == shop.Place.Id) && EventShelfProducts(shop).Any());

        [Serializable]
        public sealed class EventReferral
        {
            public string ShopId, RetailKind;
            public EventStock Product;
            public int Remaining, Reserved;
            public long BudgetCents, Earliest, Expires;
            public EventReferral Copy()
            {
                var copy = (EventReferral)MemberwiseClone();
                copy.Product = Product.Copy();
                return copy;
            }
        }

        private readonly List<EventReferral> _eventReferrals = new List<EventReferral>();
        private bool HasEventReservedStock(string shopId) => _eventReferrals.Any(visit => visit.ShopId == shopId && visit.Reserved > 0);

        [Serializable]
        public sealed class FlyerCampaign
        {
            public string ShopId, RetailKind;
            public int StartDay, Remaining;
            public long NextVisit;
            public FlyerCampaign Copy() => (FlyerCampaign)MemberwiseClone();
        }

        private readonly List<FlyerCampaign> _flyerCampaigns = new List<FlyerCampaign>();

        internal OwnedShop FlyerShop() => MyShops.Where(shop => RetailShopTypes.All.Contains(shop.RetailKind)
                && !IsDedicatedVenue(shop.Place) && !_flyerCampaigns.Any(campaign => campaign.ShopId == shop.Place.Id))
            .OrderBy(shop => shop.Place == CurrentPlace ? 0 : 1).FirstOrDefault();

        private EventDef BindFlyers(EventDef template)
        {
            var shop = FlyerShop();
            if (shop == null) return null;
            var fee = Money.FromYuan(200);
            string kind = shop.RetailKind;
            return EventScene(template, $"地推人员愿意在{ShopTitle(shop)}附近发传单。明天起的两天内，预计有 2–4 位顾客在营业时来看。",
                Choose($"发传单 · {fee}", s =>
                {
                    if (s.ShopAt(shop.Place.Id) != shop || shop.RetailKind != kind
                        || !s.Ledger.TryDebit(s.Clock.Today, "推广", fee, "发传单 · " + ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id)))
                        return "推广未安排。";
                    s._flyerCampaigns.Add(new FlyerCampaign { ShopId = shop.Place.Id, RetailKind = kind,
                        StartDay = s.Clock.Today.TotalDays + 1, Remaining = s.Random.Next(2, 5) });
                    return $"已为{ShopTitle(shop)}安排传单推广，明天起开始到访。";
                }, s => s.Ledger.CanAfford(fee), "现金不足"),
                Choose("不用", _ => "地推人员去问下一家了。"));
        }

        internal bool CanOfferColdEvent => NearbyHospital() != null && (Player.Energy < 45 || Player.SleepDebt > 2 || Player.Health < 85);

        private EventDef BindCold(EventDef template)
        {
            var hospital = NearbyHospital();
            var medicine = Money.FromYuan(20);
            var choices = new List<EventChoice>();
            if (hospital != null)
                choices.Add(Choose($"前往{hospital.Name} · 门诊 {OutpatientFee}", s =>
                    s.BeginCityTravel(hospital.Id) ? null : $"可以稍后从地图前往{hospital.Name}。"));
            choices.Add(Choose($"买感冒药 · {medicine}", s =>
            {
                if (!s.Ledger.TryDebit(s.Clock.Today, "医疗", medicine, "感冒药")) return "现金不足。";
                s.Player.Energy -= 5;
                return "吃过药，身体稳住了。精力 −5。";
            }, s => s.Ledger.CanAfford(medicine), "现金不足"));
            choices.Add(Choose("继续手上的事", s =>
            {
                s.Player.Health = Math.Max(0, s.Player.Health - 10);
                s.Player.Energy -= 10;
                if (s.Player.Health < 15)
                {
                    s._pendingHospital = true;
                    s.ProcessPending();
                    return null;
                }
                return "你继续忙，头还是昏沉。健康 −10，精力 −10。";
            }));
            return EventScene(template, "你觉得头沉、嗓子疼。可以去医院看诊，也可以先吃点药。", choices.ToArray());
        }

        internal bool CanOfferShopIntroduction(string personId) =>
            ShopAt(CurrentPlace) != null && CitySamePlace(personId) && !Flags.Contains("event:intro:" + personId)
            && (personId == Npcs.Landlady ? LandladyShop()?.Place == CurrentPlace : personId == Npcs.Lin || !IsMet(personId));

        private string LearnEventHome(string personId)
        {
            var person = CityPersonState(personId);
            if (person == null || !City.HasPlace(person.HomePlaceId)) return "";
            person.KnownTopics = (person.KnownTopics ?? Array.Empty<string>()).Append("life").Distinct().ToArray();
            RememberSocial(personId, "对方告诉了你住址：" + person.Address);
            return person.Address;
        }

        private EventDef BindShopIntroduction(EventDef template, string personId)
        {
            var shop = ShopAt(CurrentPlace) ?? InspectionShop();
            if (shop == null) return null;
            void Mark() => Flags.Add("event:intro:" + personId);
            if (personId == Npcs.Zhang)
            {
                return EventScene(template, $"张绍文走进{ShopTitle(shop)}，问你开店这几天还顺利吗？",
                    Choose("坐下聊聊 · 15 分钟", s =>
                    {
                        Mark();
                        s.Meet(personId, 22);
                        s.EventRest(15);
                        string address = s.LearnEventHome(personId);
                        if (address.Length == 0) return "你和张绍文互留了联系方式。";
                        s.Flags.Add("home-invitation:" + personId);
                        return $"张绍文和你互留了号码，说有空可以去他家坐坐。住址已存到人物资料：{address}。";
                    }),
                    Choose("聊聊店里的生意", s => { Mark(); s.Meet(personId, 16); return "你和张绍文聊了几句，互留了号码。"; }),
                    Choose("先打个照面", s => { Mark(); s.AddRelation(personId, Math.Max(0, 8 - s.Relation(personId))); return "张绍文打了个招呼，说下次有空再聊。"; }));
            }
            if (personId == Npcs.Lin)
            {
                string Reconnect(GameSession s)
                {
                    Mark();
                    s.Meet(personId, 20);
                    string address = s.LearnEventHome(personId);
                    return address.Length == 0 ? "你存下了林远航的号码。" : $"你存下了林远航的号码。他说自己住在{address}，住址已记到人物资料。";
                }
                return EventScene(template, $"发小林远航听说{ShopTitle(shop)}开张了，特意过来看看。",
                    Choose("互留号码，约顿饭", s => Reconnect(s) + "\n" + s.ScheduleLegacyCityMeeting(personId, "dinner", "legacy-lin-reunion:" + s.Clock.Today.TotalDays)),
                    Choose("互留号码，在店里聊会儿", s => Reconnect(s) + "你们在店里聊起了小时候的事。"));
            }

            shop = LandladyShop();
            if (shop == null) return null;
            return EventScene(template, $"李秀兰来看看{ShopTitle(shop)}，顺便问你刚接手铺面有没有什么不清楚的。",
                Choose("问下次交租日，互留号码", s =>
                {
                    Mark();
                    s.Meet(personId, 22);
                    return $"已存下李秀兰的号码。{ShopTitle(shop)}下次在{new GameDate(NextShopRentDay(shop))}缴周租 {s.RentOf(shop)}。";
                }),
                Choose("问问附近去哪儿进货", s =>
                {
                    Mark();
                    s.Meet(personId, 16);
                    string sources = string.Join("、", s.EmployeePurchaseCatalog().Where(o => shop.RetailKind == "computer"
                        ? o.Kind == PurchaseGoodsKind.Part : o.Kind == PurchaseGoodsKind.Gift && RetailShopTypes.Sells(shop.RetailKind, o.Id))
                        .Select(o => o.Source.Name).Distinct().Take(2));
                    return sources.Length == 0 ? "李秀兰和你互留了号码，让你有经营上的问题随时找她。"
                        : $"李秀兰推荐你先去{sources}看看，也把号码留给了你。";
                }),
                Choose("今天忙，改天再聊", s => { Mark(); return "李秀兰点点头，说有问题可以再来问她。"; }));
        }

        private int NextShopRentDay(OwnedShop shop)
        {
            int today = Clock.Today.TotalDays, signed = shop.Lease.SignedDay;
            return signed + (Math.Max(0, today - signed) / GameDate.DaysPerWeek + 1) * GameDate.DaysPerWeek;
        }

        private EventDef BindZhangVisit(EventDef template)
        {
            var shop = ShopAt(CurrentPlace) ?? InspectionShop();
            if (shop == null) return null;
            return EventScene(template, $"张绍文下班后到{ShopTitle(shop)}歇脚，问你有空聊两句吗？",
                Choose("就在店里坐会儿 · 15 分钟", s =>
                {
                    s.EventRest(15);
                    s.AddRelation(Npcs.Zhang, 2);
                    return "你们在店里聊了会儿近况。交情 +2。";
                }),
                Choose("约到家常馆吃饭", s => s.ScheduleLegacyCityMeeting(Npcs.Zhang, "dinner", "legacy-zhang-chat:" + s.Clock.Today.TotalDays)),
                Choose("约去市场逛逛", s => s.ScheduleLegacyCityMeeting(Npcs.Zhang, "market", "legacy-zhang-market:" + s.Clock.Today.TotalDays)),
                Choose("今天忙，改天再聊", _ => "张绍文说那就下次见。"));
        }

        private static string RentEventKey(OwnedShop shop) => $"event:rent:{shop.Place.Id}:{shop.Lease.SignedDay}";

        private IEnumerable<OwnedShop> LandladyShops() => MyShops.Where(shop => shop.Lease != null && !shop.OwnsProperty
                && DistrictTierOf(shop.Place) == 0 && shop.Lease.Rent.IsPositive)
            .OrderBy(shop => shop.Place == CurrentPlace ? 0 : 1);

        private OwnedShop LandladyShop() => LandladyShops().FirstOrDefault();

        internal OwnedShop RentNegotiationShop() => LandladyShops().FirstOrDefault(shop => !Flags.Contains(RentEventKey(shop))
            && !_cityActivities.Any(a => a.Active && a.SourceId?.StartsWith($"legacy-rent-talk:{shop.Place.Id}:{shop.Lease.SignedDay}:", StringComparison.Ordinal) == true));

        private EventDef BindRentNegotiation(EventDef template, OwnedShop shop, bool canSchedule)
        {
            if (shop?.Lease == null) return null;
            var lease = shop.Lease;
            Money before = RentOf(shop);
            Money proposed = before.Scale(1.1);
            double Chance(GameSession s) => EventScript.ProgressiveChance(.30,
                (s.EffectiveCharm, EventScript.CharmThreshold, .01),
                (s.Relation(Npcs.Landlady), EventScript.RelationThreshold, .004));
            string Resolve(GameSession s, bool negotiate)
            {
                if (s.ShopAt(shop.Place.Id) != shop || shop.Lease != lease || shop.OwnsProperty || !s.Flags.Add(RentEventKey(shop)))
                    return "这份租约已经处理或变更。";
                if (negotiate && s.Random.Chance(Chance(s)))
                {
                    s.AddRelation(Npcs.Landlady, 2);
                    return "李秀兰同意维持原周租。交情 +2。";
                }
                lease.Rent = lease.Rent.Scale(1.1);
                s.AddRelation(Npcs.Landlady, negotiate ? -3 : 1);
                return $"{ShopTitle(shop)}从{new GameDate(s.NextShopRentDay(shop))}起按新周租 {s.RentOf(shop)}缴费。";
            }
            var choices = new List<EventChoice>();
            if (canSchedule)
                choices.Add(Choose("约饭，当面再谈", s => s.ScheduleLegacyCityMeeting(Npcs.Landlady, "dinner",
                    $"legacy-rent-talk:{shop.Place.Id}:{lease.SignedDay}:{s.Clock.Today.TotalDays}")));
            choices.Add(Choose($"讨价还价 · 成功率 {Chance(this) * 100:0.#}%", s => Resolve(s, true)));
            choices.Add(Choose("接受新周租", s => Resolve(s, false)));
            return EventScene(template, $"李秀兰想调整{ShopTitle(shop)}的租金。\n现周租 {before}，拟调整为 {proposed}，从{new GameDate(NextShopRentDay(shop))}起缴。", choices.ToArray());
        }

        private void CheckRentDinner()
        {
            if (PendingEvent != null || _sleeping || IsWorking || CityActivityExclusive || IsGameOver) return;
            var dinner = _cityActivities.FirstOrDefault(a => a.State == CityActivityPhase.Completed &&
                a.SourceId?.StartsWith("legacy-rent-talk:", StringComparison.Ordinal) == true
                && !Flags.Contains("rent-dinner:" + a.Id));
            if (dinner == null) return;
            Flags.Add("rent-dinner:" + dinner.Id);
            var key = dinner.SourceId.Split(':');
            if (key.Length != 4 || !int.TryParse(key[2], out int signedDay)) return;
            var shop = ShopAt(key[1]);
            if (shop?.Lease == null || shop.Lease.SignedDay != signedDay || shop.OwnsProperty || Flags.Contains(RentEventKey(shop))) return;
            var followup = BindRentNegotiation(new EventDef { Id = "rent-dinner-result", Title = "饭后谈周租" }, shop, false);
            Raise(followup, Clock.Today);
        }

        private EventDef BindReferral(EventDef template)
        {
            var shop = ReferralShop();
            if (shop == null) return null;
            var product = EventShelfProducts(shop).OrderByDescending(item => item.Count).First();
            int visitors = Random.Next(2, 4);
            Money budget = EventPrice(shop, product).Scale(Random.Range(.95, 1.20));
            string Visit(GameSession session, bool reserve)
            {
                int reserved = reserve && RemoveEventProduct(shop, product, visitors) ? visitors : 0;
                if (reserve && reserved == 0) return "这款货已经不足，请等熟客下次介绍。";
                session._eventReferrals.Add(new EventReferral { ShopId = shop.Place.Id, RetailKind = shop.RetailKind,
                    Product = product, Remaining = visitors, Reserved = reserved, BudgetCents = budget.Cents,
                    Earliest = Clock.Now.TotalMinutes + 45, Expires = Clock.Now.TotalMinutes + 2 * 1440 });
                return $"张绍文会带 {visitors} 人在未来两天营业时到{ShopTitle(shop)}。"
                    + (reserved > 0 ? $"已从可售库存留出{product.Name} ×{reserved}。" : "他们会按现货和价格挑选。");
            }
            bool familiar = Relation(Npcs.Zhang) >= 35;
            return EventScene(template, $"张绍文想带 {visitors} 位朋友到{ShopTitle(shop)}看看。"
                + (familiar ? $"他们想看{product.Name}，每人预算约 {budget}。" : "你可以先留出一款货让他们看。"),
                Choose($"留货 · {product.Name} ×{visitors}", s => Visit(s, true),
                    _ => product.Count >= visitors && product.ExpiryDay < 0, "陈列数量不足或该商品需要保鲜"),
                Choose("欢迎来看看", s => Visit(s, false)),
                Choose("这两天不方便", _ => "张绍文说下次再带朋友来。"));
        }

        private const int StudentOrderQuantity = 5;

        private sealed class StudentOrderLine
        {
            public string Group, Name;
            public EventStock Stock;
            public UsedItem Used;
            public int Recipe = -1;
            public int Count;
            public Money Price, Cost;
            public (string Group, bool Boxed, int UsedId) Key => (Group, Stock?.Boxed == true, Used?.Id ?? 0);
        }

        private IEnumerable<StudentOrderLine> StudentOrderStock(OwnedShop shop)
        {
            if (IsSpecialBusiness(shop))
            {
                if (!SpecialOpen(shop, Clock.Now)) yield break;
                var state = SpecialBusiness(shop.Place.Id);
                if (shop.RetailKind == "secondhand")
                {
                    var listed = shop.Sim.ShelfFillOf(ShelfKinds.Get("secondhand")).Resale.Select(offer => offer.Id).ToHashSet();
                    foreach (var item in state.Used.Where(item => listed.Contains(item.Id)))
                        yield return new StudentOrderLine { Group = "used", Name = "二手旧物组合", Used = item,
                            Count = 1, Price = Money.FromYuan(item.Price), Cost = Money.FromYuan(item.Cost) };
                    yield break;
                }

                int ricePortions = (int)Math.Floor((KitchenRice(shop) + 1e-9) / .25);
                for (int recipe = 0; recipe < RecipeNames.Length; recipe++)
                {
                    if ((state.RecipeMask & (1 << recipe)) == 0) continue;
                    string ingredient = RecipeIngredients[recipe];
                    int portions = Math.Min(ricePortions, KitchenIngredientCount(shop, ingredient));
                    if (portions < 1) continue;
                    yield return new StudentOrderLine { Group = "recipe:" + recipe, Name = RecipeNames[recipe], Recipe = recipe,
                        Count = portions, Price = Money.FromYuan((long)Math.Round(RecipePrices[recipe] * state.MenuMarkup)),
                        Cost = Gifts.Get("produce:" + ingredient).WholesalePrice + Gifts.Get("produce:rice").WholesalePrice.Scale(.5) };
                }
                yield break;
            }

            if (!shop.Sim.IsOpen || (shop.Place.Business ?? "").StartsWith("venue_", StringComparison.Ordinal)) yield break;
            foreach (var stock in EventShelfProducts(shop).GroupBy(stock => (stock.Id, stock.Boxed))
                .Select(group => group.OrderByDescending(stock => stock.Count).First()))
                yield return new StudentOrderLine { Group = stock.Id, Name = stock.Name, Stock = stock, Count = stock.Count,
                    Price = EventPrice(shop, stock) };
        }

        private EventDef BindStudentOrder(EventDef template)
        {
            var shop = RetailStoryTarget("students");
            if (shop == null) return null;
            string kind = shop.RetailKind;
            var lines = StudentOrderStock(shop).OrderBy(item => item.Price.Cents).ToArray();

            (StudentOrderLine Item, int Count)[] Pack(IEnumerable<StudentOrderLine> stock, Money? limit = null)
            {
                var packed = new List<(StudentOrderLine, int)>();
                int remaining = StudentOrderQuantity;
                Money available = limit ?? Money.Zero;
                foreach (var item in stock)
                {
                    int count = Math.Min(remaining, item.Count);
                    if (limit.HasValue && item.Price.IsPositive) count = (int)Math.Min(count, available.Cents / item.Price.Cents);
                    if (count <= 0) continue;
                    packed.Add((item, count));
                    available -= item.Price * count;
                    remaining -= count;
                    if (remaining == 0) break;
                }
                return packed.ToArray();
            }

            Money Total((StudentOrderLine Item, int Count)[] packed) =>
                Money.FromCents(packed.Sum(line => line.Item.Price.Cents * line.Count));
            Money Cost((StudentOrderLine Item, int Count)[] packed) =>
                Money.FromCents(packed.Sum(line => (line.Item.Stock == null ? line.Item.Cost
                    : EstimatedEventUnitCost(shop, line.Item.Stock)).Cents * line.Count));
            string Sell((StudentOrderLine Item, int Count)[] packed, Money total, bool discounted = false)
            {
                if (packed.Length == 0 || ShopAt(shop.Place.Id) != shop || shop.RetailKind != kind)
                    return "店铺或库存状态已变化，本次未成交。";
                var available = StudentOrderStock(shop).ToDictionary(line => line.Key);
                if (packed.Any(line => !available.TryGetValue(line.Item.Key, out var current) || current.Count < line.Count))
                    return "店铺或库存状态已变化，本次未成交。";

                int count = packed.Sum(line => line.Count);
                string name = packed[0].Item.Name;
                if (kind == "secondhand")
                {
                    var state = SpecialBusiness(shop.Place.Id);
                    long retailCents = Total(packed).Cents, runningCents = 0, allocated = 0;
                    foreach (var line in packed)
                    {
                        runningCents += line.Item.Price.Cents;
                        long subtotal = total.Yuan * runningCents / retailCents;
                        RecordUsedSale(state, available[line.Item.Key].Used, subtotal - allocated);
                        allocated = subtotal;
                    }
                }
                else if (kind == "restaurant")
                {
                    for (int i = 0; i < count; i++) ConsumeRecipe(shop, packed[0].Item.Recipe);
                    var state = SpecialBusiness(shop.Place.Id);
                    state.Paid++;
                    state.Revenue += total.Yuan;
                }
                else
                {
                    foreach (var line in packed) RemoveEventProduct(shop, line.Item.Stock, line.Count);
                }
                shop.Sim.RecordBulkSale(name, count, total);
                if (discounted) shop.Sim.AdjustShopAwareness(1);
                return $"{ShopTitle(shop)} · 售出{name} ×{count}，实收 {total}。" + (discounted ? "团购折扣额外带来门店人气 +1。" : "");
            }

            var groups = lines.GroupBy(item => item.Group).Where(group => group.Sum(item => item.Count) >= StudentOrderQuantity).ToArray();
            var requested = groups.OrderBy(group => group.Key == "ram0" ? 0 : 1).FirstOrDefault();
            if (requested == null) return null;
            var full = Pack(requested);
            Money budget = Total(full).Scale(.95);
            if (kind == "secondhand" || kind == "restaurant") budget = Money.FromYuan(budget.Yuan);
            var partial = Pack(requested, budget);
            Money partialTotal = Total(partial);
            int partialCount = partial.Sum(line => line.Count);
            var choices = new List<EventChoice>
            {
                Choose($"整单九五折 · 实收 {budget} · 估算毛利 {budget - Cost(full)}",
                    _ => Sell(full, budget, true)),
                Choose($"现价卖 {partialCount} 件 · 实收 {partialTotal} · 估算毛利 {partialTotal - Cost(partial)}",
                    _ => Sell(partial, partialTotal), _ => partialCount > 0, "预算不足"),
            };
            var requestedPart = requested.First().Stock?.Part;
            var alternative = groups.Where(group => group.Key != requested.Key &&
                    (requestedPart == null || group.First().Stock?.Part?.Category == requestedPart.Category))
                .Select(group => Pack(group, budget)).Where(pack => pack.Sum(line => line.Count) == StudentOrderQuantity)
                .OrderByDescending(pack => Total(pack).Cents).FirstOrDefault();
            if (alternative != null)
            {
                var total = Total(alternative);
                choices.Add(Choose($"推荐{alternative[0].Item.Name} ×5 · 实收 {total} · 估算毛利 {total - Cost(alternative)}",
                    _ => Sell(alternative, total), s => s.ShopSalesBonus(shop) >= .1, "需在岗销售员且购买意愿加成至少 10 个百分点"));
            }
            choices.Add(Choose("留给零售顾客", _ => "这批货继续留在店里。"));
            return EventScene(template, $"{ShopTitle(shop)} · 有学生想团购{requested.First().Name}，共 {StudentOrderQuantity} 份，总预算 {budget}。", choices.ToArray());
        }

        private Money EstimatedEventUnitCost(OwnedShop shop, EventStock item)
        {
            long units = 0, cents = 0;
            foreach (var entry in Ledger.Entries)
            {
                var a = entry.Attribution;
                if (entry.ShopId != shop.Place.Id || !entry.Amount.IsNegative || a?.ProductId != item.Id
                    || a.Quantity <= 0 || a.UnitCostCents <= 0) continue;
                units += a.Quantity;
                cents += a.Quantity * a.UnitCostCents;
            }
            if (units > 0) return Money.FromCents(cents / units);
            return item.Part is PartDef part ? Market.PriceOf(part).Scale(.84) : Gifts.Get(item.Id).WholesalePrice;
        }

        private string _walletDropOff;
        internal bool CanFindWallet => _walletDropOff == null && City.Places.Any(p => p.Kind == PlaceKind.Station && DistrictAccessBlocker(p) == null);

        private EventDef BindWallet(EventDef template)
        {
            if (!CanFindWallet) return null;
            var station = City.Places.Where(p => p.Kind == PlaceKind.Station && DistrictAccessBlocker(p) == null)
                .OrderBy(p => DistanceCity(PlayerPosition, StandOf(p))).First();
            var owner = CityPeople
                .Where(person => City.HasPlace(person.HomePlaceId) && CityPerson(person.PersonId) != null
                    && DistrictAccessBlocker(City.GetPlace(person.HomePlaceId)) == null)
                .OrderBy(person => IsMet(person.PersonId))
                .ThenBy(person => DistanceCity(PlayerPosition, StandOf(City.GetPlace(person.HomePlaceId))))
                .Select(person => CityPerson(person.PersonId))
                .FirstOrDefault();
            bool witnessed = CityPeople.Any(person => person.PersonId != owner?.Id && person.Route == null
                && (CurrentPlace != null ? person.LocationPlaceId == CurrentPlace.Id
                    : person.LocationPlaceId == null && DistanceCity(PlayerPosition, new MapPoint(person.X, person.Y)) <= 6));
            return EventScene(template, $"路边有个钱包，里面有 ¥300 和一张留有联系方式的卡片。最近的失物代收点在{station.Name}。",
                Choose("交给地铁站服务台", s =>
                {
                    s._walletDropOff = station.Id;
                    if (s.CurrentPlace?.Id != station.Id) s.BeginCityTravel(station.Id);
                    return $"请到{station.Name}，进站后交给服务台。";
                }),
                Choose("联系失主，等对方来取 · 20 分钟", s =>
                {
                    double rewardChance = EventScript.ProgressiveChance(.35,
                        (s.Relation(owner.Id), EventScript.RelationThreshold, .004));
                    s.EventRest(20);
                    s.Meet(owner.Id, 10);
                    s.AddRelation(owner.Id, 3);
                    s.RememberSocial(owner.Id, "拾到钱包后联系本人归还。");
                    bool rewarded = s.Random.Chance(rewardChance);
                    if (rewarded) s.EventGain(Money.FromYuan(50), owner.Name + "酬谢");
                    return $"{owner.Name}来取回钱包。交情增加。" + (rewarded ? "对方留下 ¥50 表示感谢。" : "");
                }, _ => owner != null, "暂时找不到可联系的失主"),
                Choose("留下现金", s =>
                {
                    s.EventGain(Money.FromYuan(300), "钱包里的现金");
                    if (witnessed && owner != null)
                    {
                        s.AddRelation(owner.Id, -5);
                        s.InspectionShop()?.Sim.AdjustShopReputation(-2);
                        return "获得 ¥300。有在场的人认出了钱包，失主得知后降低了对你的评价，门店口碑 −2。";
                    }
                    return "获得 ¥300。周围没有人认领钱包。";
                }));
        }

        private const string ClassmateId = "res_coder";
        internal bool CanOfferClassmateDinner => CityPerson(ClassmateId) != null && City.HasPlace("life_restaurant")
            && !_cityActivities.Any(a => a.Active && a.Participants.Any(p => p.PersonId == ClassmateId) && a.DefinitionId == "dinner");

        private EventDef BindClassmateDinner(EventDef template, bool help)
        {
            var meal = CityLifeCatalog.Activity("dinner");
            bool canMeet = CanOfferClassmateDinner && meal != null;
            if (!help && !canMeet) return null;
            long start = canMeet ? SuggestCityActivityStart(meal.Id, ClassmateId) : 0;
            long later = canMeet ? SuggestCityActivityStart(meal.Id, ClassmateId, start + 1440) : 0;
            string ReserveMeal(GameSession s, long when)
            {
                var booking = s.ScheduleCityActivity(meal.Id, new[] { ClassmateId }, when,
                    sourceId: template.Id + ":" + s.Clock.Today.TotalDays);
                if (booking == null) return "时段约不上，可以在人物对话中另约。";
                booking.HostId = ClassmateId;
                booking.HostPays = true;
                return $"已加入日程 · {new GameTimestamp(when)} · {s.City.GetPlace(meal.VenueId).Name}。这顿由陈志衡请客。";
            }
            string where = canMeet ? $"\n{City.GetPlace(meal.VenueId).Name} · {new GameTimestamp(start)} · 陈志衡请客。" : "";
            var choices = new List<EventChoice>();
            if (canMeet)
            {
                choices.Add(Choose("接受饭局", s => ReserveMeal(s, start),
                    s => s.CityActivityBlocker(meal.Id, new[] { ClassmateId }, start) == null, "这个时段暂不可用"));
                choices.Add(Choose($"改约 · {new GameTimestamp(later)}", s => ReserveMeal(s, later),
                    s => s.CityActivityBlocker(meal.Id, new[] { ClassmateId }, later) == null, "没有合适的备选时段"));
            }
            if (help)
            {
                choices.Add(Choose("仅接受 ¥5,000 无息借款", s => s.AcceptFriendLoan(ClassmateId, 5000, 3)));
                if (canMeet)
                    choices.Add(Choose("接受饭局和 ¥5,000 无息借款", s => ReserveMeal(s, start) + "\n" + s.AcceptFriendLoan(ClassmateId, 5000, 3),
                        s => s.CityActivityBlocker(meal.Id, new[] { ClassmateId }, start) == null, "这个饭局时段暂不可用"));
            }
            choices.Add(Choose("这次先婉拒", _ => "陈志衡说有空再聊。"));
            string offer = help ? "\n他另外提出借你 ¥5,000 应急。无息，90 天到期自动扣款，可提前还。接受饭局本身不会借款。" : "";
            return EventScene(template, "陈志衡想和你见面聊聊近况。" + where + offer, choices.ToArray());
        }

        [Serializable]
        public sealed class RelativeReceivable
        {
            public int Amount, DueDay, Repaid, LateRepaid, Defaults;
            public bool Extended;
            public RelativeReceivable Copy() => (RelativeReceivable)MemberwiseClone();
        }

        private RelativeReceivable _relativeReceivable = new RelativeReceivable();
        internal bool CanOfferRelativeLoan => _relativeReceivable.Amount == 0 && Ledger.CanAfford(Money.FromYuan(2000));

        private EventDef BindRelativeLoan(EventDef template)
        {
            if (!CanOfferRelativeLoan) return null;
            int due = Clock.Today.TotalDays + 3;
            string Lend(GameSession s, int amount)
            {
                if (!s.Ledger.TryDebit(s.Clock.Today, "借款借出", Money.FromYuan(amount), "借给表哥")) return "现金不足。";
                s._relativeReceivable.Amount = amount;
                s._relativeReceivable.DueDay = due;
                s._relativeReceivable.Extended = false;
                return $"已借出 ¥{amount}，约定 {new GameDate(due)} 归还。";
            }
            return EventScene(template, $"表哥要先垫一笔维修费，想借 ¥800，约定 {new GameDate(due)} 还款。"
                + $"\n过往按时还清 {_relativeReceivable.Repaid} 笔，逾期后还清 {_relativeReceivable.LateRepaid} 笔，认亏 {_relativeReceivable.Defaults} 笔。",
                Choose("借 ¥800", s => Lend(s, 800)),
                Choose("只借 ¥400", s => Lend(s, 400)),
                Choose("婉拒", _ => "你说明手头也需要留周转金。"));
        }

        private void ReceiveRelativeRepayment(bool late)
        {
            Money amount = Money.FromYuan(_relativeReceivable.Amount);
            Ledger.Credit(Clock.Today, NpcRepaymentTitle, amount, "表哥还钱");
            _relativeReceivable.Amount = 0;
            if (late) _relativeReceivable.LateRepaid++;
            else _relativeReceivable.Repaid++;
            Show(NpcRepaymentTitle, $"表哥已还款 {amount}，当前现金 {Ledger.Balance}。");
        }

        private void CheckRelativeRepayment(GameDate today)
        {
            var debt = _relativeReceivable;
            if (debt.Amount == 0 || debt.DueDay != today.TotalDays) return;
            double chance = debt.Extended ? .5 : debt.Amount <= 400 ? .85 : .75;
            if (Random.Chance(chance)) ReceiveRelativeRepayment(debt.Extended);
            else Show("表哥的借款已逾期", $"还有 ¥{debt.Amount} 没还。空下来可以决定催款、延期或认亏。");
        }

        private bool TryShowRelativeOverdue()
        {
            var debt = _relativeReceivable;
            if (debt.Amount == 0 || debt.DueDay > Clock.Today.TotalDays || EventRaised == null) return false;
            string WriteOff()
            {
                int amount = debt.Amount;
                debt.Amount = 0;
                debt.Defaults++;
                return $"已记下这次未收回的 ¥{amount}。";
            }
            var choices = new List<EventChoice>
            {
                Choose("联系表哥催款", s =>
                {
                    if (!s.Random.Chance(.5)) return "表哥目前仍拿不出钱。" + WriteOff();
                    s.ReceiveRelativeRepayment(true);
                    return null;
                }),
                Choose("记为收不回", _ => WriteOff())
            };
            if (!debt.Extended)
                choices.Insert(1, Choose("同意延期 3 天", s =>
                {
                    debt.DueDay = s.Clock.Today.TotalDays + 3;
                    debt.Extended = true;
                    return $"新的还款日是 {new GameDate(debt.DueDay)}。";
                }));
            Raise(EventScene(new EventDef { Id = "relative-loan-overdue", Title = "表哥的借款逾期" },
                $"表哥还欠你 ¥{debt.Amount}。这笔款原定 {new GameDate(debt.DueDay)} 归还。", choices.ToArray()), Clock.Today);
            return true;
        }

        internal double ShopPowerTraffic(OwnedShop shop, GameTimestamp time)
        {
            var light = shop.EmergencyLighting;
            if (!light.PowerOutAt(time.TotalMinutes) || light.WorksOn(time.Date.TotalDays)) return 1;
            return light.UsingCandles ? .4 : 0;
        }

        private void TickEventFollowups(GameTimestamp now)
        {
            TickRacingBatchEvents(now);
            if (_walletDropOff != null && CurrentPlace?.Id == _walletDropOff)
            {
                _walletDropOff = null;
                Show("钱包已交到服务台", "站务员已登记失物，等失主来认领。");
            }
            for (int i = _eventReferrals.Count - 1; i >= 0; i--)
            {
                var visit = _eventReferrals[i];
                var shop = ShopAt(visit.ShopId);
                if (shop == null || shop.RetailKind != visit.RetailKind || now.TotalMinutes >= visit.Expires)
                {
                    if (shop != null && visit.Reserved > 0) RestoreEventProduct(shop, visit.Product, visit.Reserved);
                    _eventReferrals.RemoveAt(i);
                    if (shop != null) Say($"{ShopTitle(shop)}的介绍到访已取消，剩余留货已恢复销售。");
                    continue;
                }
                if (now.TotalMinutes < visit.Earliest || !shop.Sim.IsOpenAt(now, CityPlayerCanWork(shop))) continue;
                bool reserved = visit.Reserved > 0;
                if (reserved) visit.Reserved--;
                shop.Sim.ArriveReferral(visit.Product.Part, visit.Product.Part == null ? visit.Product.Id : null,
                    visit.Product.Boxed, visit.BudgetCents, reserved);
                if (--visit.Remaining <= 0) _eventReferrals.RemoveAt(i);
                else visit.Earliest = now.TotalMinutes + 3;
            }
            for (int i = _flyerCampaigns.Count - 1; i >= 0; i--)
            {
                var campaign = _flyerCampaigns[i];
                var shop = ShopAt(campaign.ShopId);
                if (shop == null || shop.RetailKind != campaign.RetailKind || now.Date.TotalDays >= campaign.StartDay + 2)
                {
                    _flyerCampaigns.RemoveAt(i);
                    continue;
                }
                if (now.Date.TotalDays < campaign.StartDay || now.TotalMinutes < campaign.NextVisit) continue;
                if (shop.RetailKind == "restaurant")
                {
                    if (!SpecialOpen(shop, now) || !TrySeatRestaurantGuest(shop, SpecialBusiness(shop.Place.Id), now)) continue;
                }
                else
                {
                    if (!shop.Sim.IsOpenAt(now, CityPlayerCanWork(shop))) continue;
                    shop.Sim.Spawn(now);
                }
                if (--campaign.Remaining == 0) _flyerCampaigns.RemoveAt(i);
                else campaign.NextVisit = now.TotalMinutes + 15;
            }
            ExpireIndustryReferrals(now);
            CheckRentDinner();
            TickCustomerBatch(now.TotalMinutes);
            TickShopEventBatch(now.TotalMinutes);
        }

        private void CheckInspectionFollowups(GameDate today)
        {
            if (_inspectionFollowups.Count == 0) return;
            foreach (var check in _inspectionFollowups.Where(entry => entry.Value <= today.TotalDays).ToArray())
            {
                _inspectionFollowups.Remove(check.Key);
                var shop = ShopAt(check.Key);
                if (shop == null) continue;
                bool fitted = IsSpecialBusiness(shop) ? SpecialFitGap(shop) == null : !shop.Sim.NotFitted;
                if (fitted && (shop.Sim.Stock.IsEmpty || HasPurchaseReceipt(shop)))
                {
                    shop.Sim.AdjustShopReputation(1);
                    Show("门店复查通过", $"{ShopTitle(shop)}的设施和采购记录已复查通过，口碑 +1。");
                    continue;
                }
                Ledger.ForceDebit(today, "罚款", Money.FromYuan(500), "复查未通过 · " + ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id));
                shop.Sim.AdjustShopReputation(-2);
                Show("门店复查未通过", $"{ShopTitle(shop)}的设施或单据仍有缺项。罚款 ¥500，口碑 −2。");
            }
        }

        internal bool NeedsEmergencyLighting => _shops.Any(shop => shop.Rented && !shop.EmergencyLighting.WorksOn(Clock.Today.TotalDays));

        private EventDef BindBlackout()
        {
            int day = Clock.Today.TotalDays;
            var shops = MyShops;
            var affected = shops.Where(shop => !shop.EmergencyLighting.WorksOn(day)).ToArray();
            if (affected.Length == 0) return null;

            long end = Clock.Now.AddMinutes(PowerCutMinutes).TotalMinutes;
            foreach (var shop in shops)
            {
                shop.EmergencyLighting.OutageEndsAt = end;
                shop.EmergencyLighting.UsingCandles = false;
            }

            var price = Money.FromYuan(EmergencyLightPrice);
            var total = price * affected.Length;
            int broken = affected.Count(shop => shop.EmergencyLighting.Installed);
            string details = broken > 0 ? $"其中 {broken} 家的应急灯已损坏。" : "";
            return new EventDef
            {
                Id = "blackout",
                Title = "停电了",
                Body = $"电力预计两小时后恢复。你经营的 {shops.Count} 家店里，有 {affected.Length} 家没有可用的应急灯。{details}\n每家安装或更换一盏，单价 {price}，合计 {total}。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = $"为 {affected.Length} 家店安装应急灯 · {total}",
                        Enabled = s => s.Ledger.CanAfford(total),
                        DisabledReason = $"现金不足，需要 {total}",
                        Apply = s =>
                        {
                            foreach (var shop in affected)
                            {
                                s.Ledger.ForceDebit(s.Clock.Today, "装修", price, "应急灯安装", LedgerAttribution.ForShop(shop.Place.Id));
                                shop.EmergencyLighting.FailureDay = s.Clock.Today.TotalDays + s.Random.Next(45, 91);
                            }
                            return $"{affected.Length} 家店的应急灯已安装在后墙。停电期间会自动亮起。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "暂停营业，等两小时",
                        Apply = s =>
                        {
                            s.EventRest(PowerCutMinutes);
                            return "缺少应急灯的门店暂停接待。供电恢复后，门店按原营业安排恢复。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "点蜡烛，维持有限接待",
                        Apply = s =>
                        {
                            if (s.Random.Chance(.3))
                            {
                                s.EventPay(Money.FromYuan(200), "小火警");
                                return "蜡烛烧着了纸箱。火已经扑灭，损失 ¥200。";
                            }
                            foreach (var shop in affected) shop.EmergencyLighting.UsingCandles = true;
                            return "已点蜡烛。缺灯门店的到店客流暂为平时的40%，直到供电恢复。";
                        },
                    },
                },
            };
        }

        private bool HasRetailStoryGoods(OwnedShop shop,string eventId)
        {
            if(eventId=="students")return StudentOrderStock(shop).GroupBy(item=>item.Group)
                .Any(group=>group.Sum(item=>item.Count)>=StudentOrderQuantity);
            if(!IsCompetingRetailShop(shop))return false;
            if(shop.RetailKind=="computer")return shop.Sim.DisplayPcs.Count>0 || ShelfKinds.PartKinds.Any(k=>shop.Sim.ShelfFillOf(k).Total>0);
            return shop.Sim.GoodsOnShelves().Any(g=>g.Value>0 && RetailShopTypes.Sells(shop.RetailKind,g.Key));
        }

        internal OwnedShop RetailStoryTarget(string eventId)=>MyShops
            .Where(shop=>HasRetailStoryGoods(shop,eventId) && shop.Sim.Reputation>=(eventId=="review"?5:2))
            .OrderBy(shop=>shop==CurrentShop?0:1).ThenBy(shop=>shop.Place.Id,StringComparer.Ordinal).FirstOrDefault();

        private EventDef BindRetailStory(EventDef template)
        {
            var target=RetailStoryTarget(template.Id);if(target==null)return null;
            string kind=target.RetailKind;
            var bound=new EventDef{Id=template.Id,Title=template.Title,Body=ShopTitle(target)+"："+template.Body,
                MinDay=template.MinDay,CooldownDays=template.CooldownDays,Weight=template.Weight};
            if(template.Id=="review")
            {
                string accusation=kind=="computer"?"配件是翻新的，冒充新品销售":kind=="clothing"?"衣服的吊牌与实际面料不符":kind=="models"?"模型手办是仿品，冒充正版销售":"商品的质量和来源有问题";
                bound.Title=kind=="computer"?template.Title:kind=="clothing"?"网上有人质疑你家服装质量":kind=="models"?"网上有人质疑你家模型手办的正版来源":"网上有人质疑你家的商品";
                bound.Body=$"有人在本地论坛发帖，质疑{ShopTitle(target)}的{accusation}，帖子下面已经有人跟帖。";
            }
            foreach(var original in template.Choices)
            {
                var choice=original;
                string text = choice.Text == "公开回应" ? $"公开回应 · 成功率 {EventCatalog.ReviewChance(this) * 100:0.#}%" : choice.Text;
                bound.Choices.Add(new EventChoice{Text=text,DisabledReason=choice.DisabledReason,
                    Enabled=session=>
                    {
                        if(choice.Enabled==null)return true;
                        bool enabled=false;session.WithShop(target,()=>enabled=choice.Enabled(session));return enabled;
                    },
                    Apply=session=>
                    {
                        if(session.ShopAt(target.Place.Id)!=target || target.RetailKind!=kind || !session.HasRetailStoryGoods(target,template.Id))
                            return "这家店已停业、退租、改换主售或不再陈列相关商品，本次事件不再结算，其他店铺未受影响。";
                        string result=null;session.WithShop(target,()=>result=choice.Apply(session));return result;
                    }});
            }
            return bound;
        }

        public bool IsChoiceEnabled(EventChoice c) => c.Enabled == null || c.Enabled(this);

        /// <summary>做出选择。选项灰掉时返回 false。结果通过 Popup 告诉玩家。</summary>
        public bool ResolveEvent(int choiceIndex)
        {
            EventDef def = PendingEvent;
            if (def == null || choiceIndex < 0 || choiceIndex >= def.Choices.Count) return false;
            EventChoice choice = def.Choices[choiceIndex];
            if (!IsChoiceEnabled(choice)) return false;
            PendingEvent = null;
            string result = choice.Apply(this);
            if (!string.IsNullOrEmpty(result)) Popup?.Invoke(def.Title, result);
            return true;
        }

        // ---------- 给事件目录用的小工具 ----------

        internal void EventGain(Money amount, string memo)
        {
            if (amount.IsPositive) Ledger.Credit(Clock.Today, "事件", amount, memo);
        }

        internal void EventPay(Money amount, string memo)
        {
            if (amount.IsPositive) Ledger.ForceDebit(Clock.Today, "事件", amount, memo);
        }

        internal void EventLendToNpc(Money amount, string borrower) =>
            Ledger.ForceDebit(Clock.Today, "借款借出", amount, "借给" + borrower);

        internal string ScheduleNpcRepayment(int days, Money amount, string memo) =>
            $"{ScheduleCredit(days, amount, memo)} 00:00 自动到账 {amount}，已加入日程。";

        internal void EventRest(int minutes)
        {
            AdvanceClock(minutes);
        }

        private void SaveEvents(SaveData d)
        {
            d.celebrationRaceKey = _celebrationRaceKey;
            d.celebrationRacePeople = _celebrationRacePeople.ToArray();
            SaveRacingBatchEvents(d);
            d.randomEventDays = _randomEventDays.ToArray();
            d.eventIds = _eventLastDay.Keys.ToArray();
            d.eventLastDays = d.eventIds.Select(k => _eventLastDay[k]).ToArray();
            d.eventCounts = d.eventIds.Select(EventCountOf).ToArray();
            d.weekendWorkEnds = _weekendWorkEnds;
            d.relativeReceivable = _relativeReceivable.Copy();
            d.linEventLoan = _linLoan?.Copy();
            d.collectorTrialThrough = _collectorTrialThrough + 1;
            d.mingAfterRace = _mingAfterRace;
            d.eventReferrals = _eventReferrals.Select(visit => visit.Copy()).ToArray();
            d.flyerCampaigns = _flyerCampaigns.Select(campaign => campaign.Copy()).ToArray();
            d.walletDropOff = _walletDropOff;
            d.inspectionShops = _inspectionFollowups.Keys.ToArray();
            d.inspectionDays = d.inspectionShops.Select(id => _inspectionFollowups[id]).ToArray();
        }

        private void RestoreEvents(SaveData d)
        {
            _celebrationRaceKey = d.celebrationRaceKey;
            _celebrationRacePeople = d.celebrationRacePeople ?? Array.Empty<string>();
            RestoreRacingBatchEvents(d);
            _weekendWorkEnds = Math.Max(0, d.weekendWorkEnds);
            _relativeReceivable = d.relativeReceivable?.Copy() ?? new RelativeReceivable();
            _linLoan = d.linEventLoan?.Copy();
            _collectorTrialThrough = d.collectorTrialThrough - 1;
            _mingAfterRace = d.mingAfterRace;
            _eventReferrals.Clear();
            _eventReferrals.AddRange((d.eventReferrals ?? Array.Empty<EventReferral>()).Where(visit => visit?.Product != null)
                .Select(visit => visit.Copy()));
            _flyerCampaigns.Clear();
            _flyerCampaigns.AddRange((d.flyerCampaigns ?? Array.Empty<FlyerCampaign>()).Where(campaign => campaign != null && campaign.Remaining > 0)
                .Select(campaign => campaign.Copy()));
            _walletDropOff = d.walletDropOff;
            _inspectionFollowups.Clear();
            for (int i = 0; i < Math.Min(d.inspectionShops?.Length ?? 0, d.inspectionDays?.Length ?? 0); i++)
                _inspectionFollowups[d.inspectionShops[i]] = d.inspectionDays[i];
            _randomEventDays.Clear();
            if (d.randomEventDays != null) _randomEventDays.AddRange(d.randomEventDays);
            _eventLastDay.Clear();
            _eventCount.Clear();
            int n = d.eventIds?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                _eventLastDay[d.eventIds[i]] = d.eventLastDays[i];
                _eventCount[d.eventIds[i]] = d.eventCounts[i];
            }
            PendingEvent = null;
        }
    }

    public sealed partial class SaveData
    {
        public GameSession.RelativeReceivable relativeReceivable;
        public GameSession.LinLoanState linEventLoan;
        public int collectorTrialThrough;
        public bool mingAfterRace;
        public GameSession.EventReferral[] eventReferrals;
        public GameSession.FlyerCampaign[] flyerCampaigns;
        public string walletDropOff;
        public string[] inspectionShops;
        public int[] inspectionDays;
    }
}
