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
                if (def.Group == "race" && !raceEventsAvailable) continue;
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

            const int charmThreshold = 45;
            double chance = EffectiveCharm < charmThreshold ? 0 : Math.Min(1, .35 + (EffectiveCharm - charmThreshold) * .01);
            var remediation = Money.FromYuan(300);
            string ScheduleInspection(GameSession session, int days)
            {
                int due = session.Clock.Today.TotalDays + days;
                session._inspectionFollowups[shop.Place.Id] = due;
                return $"请在 {new GameDate(due)} 前备齐开业设施和采购记录，届时复查。";
            }
            return EventScene(template, $"{ShopTitle(shop)}\n{checks}。你可以现场补办消防与单据整改，或争取复查期限。",
                Choose("现场整改 · ¥300", s =>
                {
                    s.Ledger.ForceDebit(s.Clock.Today, "整改", remediation, ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id));
                    s.Flags.Add("inspection:receipts:" + shop.Place.Id);
                    if (!equipment) return "消防和单据现场处理完毕。开业设施仍需补齐。" + ScheduleInspection(s, 3);
                    shop.Sim.AdjustShopReputation(1);
                    return "已补办并通过检查，门店口碑 +1。";
                }, s => s.Ledger.CanAfford(remediation), "现金不足"),
                Choose("申请复查 · 3 天", s => ScheduleInspection(s, 3)),
                Choose($"说明情况 · 成功率 {chance:P0}", s =>
                {
                    if (s.Random.Chance(chance)) return ScheduleInspection(s, 7);
                    s.Ledger.ForceDebit(s.Clock.Today, "罚款", Money.FromYuan(500), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id));
                    shop.Sim.AdjustShopReputation(-2);
                    return "未争取到宽限，罚款 ¥500，门店口碑 −2。";
                }, _ => chance > 0, $"有效魅力达到 {charmThreshold} 后从 35% 起算"));
        }

        private readonly Dictionary<string, int> _inspectionFollowups = new Dictionary<string, int>();

        private EventDef BindStockDiscrepancy(EventDef template)
        {
            var shop = DiscrepancyShop();
            if (shop == null) return null;
            var products = EventShelfProducts(shop).ToArray();
            if (products.Length == 0) return null;
            var product = products[Random.Next(0, products.Length)];
            bool countingError = Random.Chance(.3);
            bool hasCamera = HasUpgrade(ShopUpgrades.Cctv);
            double evidenceBonus = Math.Max(0, Player.Intelligence - 40) * .003;
            int reviewMinutes = Player.Intelligence >= 60 ? 20 : 30;
            string Investigate(GameSession session, bool camera, bool report)
            {
                if (report && countingError)
                {
                    session.EventRest(camera ? 15 : reviewMinutes);
                    return $"{ShopTitle(shop)} · {product.Name}是盘点差错，实际库存完整。";
                }
                if (report && session.Random.Chance(Math.Min(.95, (camera ? .8 : .35) + evidenceBonus)))
                {
                    session.EventRest(camera ? 15 : reviewMinutes);
                    return $"{ShopTitle(shop)} · 找回了{product.Name} ×1，库存已核对。";
                }
                bool removed = RemoveEventProduct(shop, product, 1);
                if (report) session.EventRest(camera ? 15 : reviewMinutes);
                return removed ? $"{ShopTitle(shop)} · {product.Name} ×1 已登记损耗。" : "该商品已不在库存，本次无需再扣。";
            }
            return EventScene(template, $"{ShopTitle(shop)}盘点发现{product.Name}少了一件。可能是记错，也可能真的丢了。",
                Choose($"复盘并核查 · {reviewMinutes} 分钟", s => Investigate(s, false, true)),
                Choose("查看监控，找到线索就报警 · 15 分钟", s => Investigate(s, true, true),
                    _ => hasCamera, "需要安装监控"),
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

        private EventDef BindStudentOrder(EventDef template)
        {
            var shop = RetailStoryTarget("students");
            if (shop == null) return null;
            const int quantity = 5;
            var lines = EventShelfProducts(shop).Where(item => item.Part?.Category == PartCatalog.Get("ram0").Category)
                .OrderBy(item => EventPrice(shop, item).Cents).ToArray();

            (EventStock Item, int Count)[] Pack(IEnumerable<EventStock> stock, Money? limit = null)
            {
                var packed = new List<(EventStock, int)>();
                int remaining = quantity;
                Money available = limit ?? Money.Zero;
                foreach (var item in stock)
                {
                    var price = EventPrice(shop, item);
                    int count = Math.Min(remaining, item.Count);
                    if (limit.HasValue && price.IsPositive) count = (int)Math.Min(count, available.Cents / price.Cents);
                    if (count <= 0) continue;
                    packed.Add((item, count));
                    available -= price * count;
                    remaining -= count;
                    if (remaining == 0) break;
                }
                return packed.ToArray();
            }

            Money Total((EventStock Item, int Count)[] packed) =>
                Money.FromCents(packed.Sum(line => EventPrice(shop, line.Item).Cents * line.Count));
            Money Cost((EventStock Item, int Count)[] packed) =>
                Money.FromCents(packed.Sum(line => EstimatedEventUnitCost(shop, line.Item).Cents * line.Count));
            string Sell((EventStock Item, int Count)[] packed, Money total)
            {
                if (packed.Length == 0 || !IsCompetingRetailShop(shop) || shop.RetailKind != "computer"
                    || packed.Any(line => shop.Sim.Stock.Count(line.Item.Part, line.Item.Boxed) < line.Count))
                    return "店铺或库存状态已变化，本次未成交。";
                foreach (var line in packed) RemoveEventProduct(shop, line.Item, line.Count);
                int count = packed.Sum(line => line.Count);
                string name = packed[0].Item.Name;
                shop.Sim.RecordBulkSale(name, count, total);
                return $"{ShopTitle(shop)} · 售出{name} ×{count}，实收 {total}。";
            }

            var requested = lines.Where(item => item.Id == "ram0").ToArray();
            var full = Pack(requested);
            Money budget = Total(full).Scale(.95);
            var partial = Pack(requested, budget);
            Money partialTotal = Total(partial);
            int partialCount = partial.Sum(line => line.Count);
            var choices = new List<EventChoice>
            {
                Choose($"整单九五折 · 实收 {budget} · 估算毛利 {budget - Cost(full)}",
                    _ => Sell(full, budget)),
                Choose($"现价卖 {partialCount} 件 · 实收 {partialTotal} · 估算毛利 {partialTotal - Cost(partial)}",
                    _ => Sell(partial, partialTotal), _ => partialCount > 0, "预算不足"),
            };
            var alternative = lines.Where(item => item.Id != "ram0").GroupBy(item => item.Id)
                .Select(group => Pack(group, budget)).Where(pack => pack.Sum(line => line.Count) == quantity)
                .OrderByDescending(pack => Total(pack).Cents).FirstOrDefault();
            if (alternative != null)
            {
                var total = Total(alternative);
                choices.Add(Choose($"推荐{alternative[0].Item.Name} ×5 · 实收 {total} · 估算毛利 {total - Cost(alternative)}",
                    _ => Sell(alternative, total), s => s.ShopSalesBonus(shop) >= .1, "需在岗销售员且购买意愿加成至少 10 个百分点"));
            }
            choices.Add(Choose("留给零售顾客", _ => "这批货继续留在店里。"));
            return EventScene(template, $"{ShopTitle(shop)} · 学生想给宿舍电脑配 5 条入门内存，总预算 {budget}。", choices.ToArray());
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
            return units > 0 ? Money.FromCents(cents / units) : Market.PriceOf(item.Part).Scale(.84);
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
                    s.EventRest(20);
                    s.Meet(owner.Id, 10);
                    s.AddRelation(owner.Id, 3);
                    s.RememberSocial(owner.Id, "拾到钱包后联系本人归还。");
                    bool rewarded = s.Random.Chance(.35 + s.Relation(owner.Id) * .004);
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
            if(!IsCompetingRetailShop(shop))return false;
            if(eventId=="students" && shop.RetailKind!="computer")return false;
            if(eventId=="students")return shop.Sim.OnShelf(PartCatalog.Get("ram0"))>=5;
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
                bound.Choices.Add(new EventChoice{Text=choice.Text,DisabledReason=choice.DisabledReason,
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
            d.randomEventDays = _randomEventDays.ToArray();
            d.eventIds = _eventLastDay.Keys.ToArray();
            d.eventLastDays = d.eventIds.Select(k => _eventLastDay[k]).ToArray();
            d.eventCounts = d.eventIds.Select(EventCountOf).ToArray();
            d.weekendWorkEnds = _weekendWorkEnds;
            d.relativeReceivable = _relativeReceivable.Copy();
            d.eventReferrals = _eventReferrals.Select(visit => visit.Copy()).ToArray();
            d.walletDropOff = _walletDropOff;
            d.inspectionShops = _inspectionFollowups.Keys.ToArray();
            d.inspectionDays = d.inspectionShops.Select(id => _inspectionFollowups[id]).ToArray();
        }

        private void RestoreEvents(SaveData d)
        {
            _weekendWorkEnds = Math.Max(0, d.weekendWorkEnds);
            _relativeReceivable = d.relativeReceivable?.Copy() ?? new RelativeReceivable();
            _eventReferrals.Clear();
            _eventReferrals.AddRange((d.eventReferrals ?? Array.Empty<EventReferral>()).Where(visit => visit?.Product != null)
                .Select(visit => visit.Copy()));
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
        public GameSession.EventReferral[] eventReferrals;
        public string walletDropOff;
        public string[] inspectionShops;
        public int[] inspectionDays;
    }
}
