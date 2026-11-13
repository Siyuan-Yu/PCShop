using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class OutdoorBatchState
    {
        public string ActivityId, Pending, Message, PartId = "gpu1";
        public int Number, Visited, Mode, Answers, Correct, OrderCount, PurchaseSource;
        public long ReadyAt;
        public bool Tested, CreditGranted, Negotiated, Delivered;
        public string[] QuoteSources = Array.Empty<string>();
        public string[] QuoteNames = Array.Empty<string>();
        public long[] QuoteCents = Array.Empty<long>();
        public OutdoorBatchState Copy() { var c = (OutdoorBatchState)MemberwiseClone(); c.QuoteSources = (QuoteSources ?? Array.Empty<string>()).ToArray(); c.QuoteNames = (QuoteNames ?? Array.Empty<string>()).ToArray(); c.QuoteCents = (QuoteCents ?? Array.Empty<long>()).ToArray(); return c; }
    }
    public sealed partial class GameSession
    {
        private static readonly string[] OutdoorIds = { "act_fishing_grandpa", "act_hike_luqiao", "act_hike_gardener", "act_hike_shiyang",
            "act_snack_moxin", "act_snack_maoning", "act_snack_rider", "act_market_zhou", "act_market_li", "act_market_wang" };
        private static readonly string[] OutdoorPeople = { "res_grandpa", "resident_luqiao", "life_gardener", "resident_shiyang",
            "resident_moxin", "resident_maoning", "res_rider", "zhou", "li", "wang" };
        private readonly List<OutdoorBatchState> _outdoorBatch = new List<OutdoorBatchState>();
        private static int OutdoorNumber(string id) => id != null && id.StartsWith("social_", StringComparison.Ordinal)
            && int.TryParse(id.Substring(7), out int n) && n >= 61 && n <= 77 ? n : 0;
        public static bool IsOutdoorBatchActivity(CityActivity a) => OutdoorNumber(a?.DefinitionId) != 0;
        private OutdoorBatchState OutdoorState(CityActivity a) => _outdoorBatch.FirstOrDefault(s => s.ActivityId == a?.Id);
        public OutdoorBatchState OutdoorActivityState(string id) => _outdoorBatch.FirstOrDefault(s => s.ActivityId == id)?.Copy();
        public bool HasOpenedComputerShop => MyShops.Any(shop => shop.RetailKind == "computer" && shop.Sim != null && !shop.Sim.NotFitted);
        internal bool CanOfferOutdoorBatchEvent(int n) => n >= 61 && n <= 70 && (n != 70 || HasOpenedComputerShop)
            && CityPersonState(OutdoorPeople[n - 61]) != null && City.HasPlace(OutdoorVenue(n))
            && DistrictAccessBlocker(City.GetPlace(OutdoorVenue(n))) == null
            && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_" + n);
        private string OutdoorVenue(int n) => n <= 62 ? FishingVenueId : n <= 64 ? CityPersonState(OutdoorPeople[n - 61])?.WorkPlaceId
            : n <= 67 ? "life_restaurant" : n == 70 ? "stall_gpu" : CityPersonState(OutdoorPeople[n - 61])?.WorkPlaceId;
        private EventDef BindOutdoorBatchEvent(EventDef template, int n)
        {
            if (!CanOfferOutdoorBatchEvent(n)) return null;
            string person = OutdoorPeople[n - 61], venue = OutdoorVenue(n);
            string body = n switch {
                61 => "周国良约你到青湾垂钓场，租杆、选饵、真实甩竿遛鱼。双人基础服务各付 ¥300，也可由你请客付 ¥600。鱼获可当面送给老人。",
                62 => "卢桥带队沿步道郊游。生态向导 ¥900/人、120 分钟；私享野餐 ¥2,200/人、180 分钟。到场后逐站行走，可休息或选择观景支线。",
                63 => "方林邀请你沿公园步道看花。可以赏花、帮扶倒伏植物或巡查损坏设施；需要实际到现场完成，不收活动费。",
                64 => "石扬带你在公园认三株植物。走到样株旁观察叶片，再选择香樟、桂花或银杏；答错可以继续，奖励按实际辨认结果结算。",
                65 => "莫欣约你下班后吃夜宵。海味砂锅 ¥480/人、90 分钟，私厨套餐 ¥1,200/人、120 分钟；付款方式与聊天话题分别选择。",
                66 => "茅宁要值夜班。可约班前快餐（¥160/人），或买好实际餐食送到他的工作地点；送餐不收额外活动费，交付会消耗背包里的实物。也可改约下班后。",
                67 => "吴磊完成当天跑单后才来赴约。海味砂锅 ¥480/人或私厨 ¥1,200/人，可聊跑单经历，也可另谈真实配送合作；不会替你自动生成合作订单。",
                68 => "周维成带你实际走访市场档口。看新品、核对供货条件后可自行选择型号和数量，现款采购按真实报价扣钱并运输，不会自动赠送配件。",
                69 => "李承业陪你实地看货，比较本市场与真实网购报价；已签直供协议时再加入直供报价。走完三个核对节点，再判断哪家合算或暂缓采购。谈价仍可在属性不足时尝试，但会失败。",
                _ => "王振海请你到显卡档口看货。你已开办电脑店，可以先看样、花时间上机核对参数，再选型号与数量现款采购或谈 30% 首款、14 天尾款的账期。商品、付款、债务与运输都实际发生。" };
            var choices = new List<EventChoice>();
            void Offer(string label, string definition, bool treat = false, bool hostPays = false, string at = null, bool after = false, bool nextShiftEnd = false)
            {
                string place = at ?? venue; long earliest = 0;
                if (nextShiftEnd)
                {
                    earliest = Clock.Today.TotalDays * 1440L + CityPerson(person).WorkEnd;
                    while (earliest <= Clock.Now.TotalMinutes) earliest += 1440;
                }
                long start = FindOutdoorStart(definition, place, person, after, earliest);
                choices.Add(Choose(label + (start >= 0 ? " · " + new GameTimestamp(start) : " · 暂无合适时段"), s =>
                {
                    if (n == 70 && !s.HasOpenedComputerShop) return "电脑店已不可用，本次未预约。";
                    if (hostPays)
                    {
                        double chance = EventScript.ProgressiveChance(.5, (s.Relation(person), 35, .015), (s.EffectiveCharm, 45, .01));
                        if (!s.Random.Chance(chance)) return $"请客商量成功率 {chance:P0}。对方这次没有答应，本次未预约，不扣钱或交情。";
                    }
                    var a = s.ScheduleCityActivityAt(definition, new[] { person }, start, false, "outdoor:" + n + ":" + s._nextCityActivity, place);
                    if (a == null) return "日程或条件变化，未预约、未扣款。";
                    a.HostId = person; a.HostPays = hostPays; a.PlayerTreat = treat; a.ServiceConfirmed = true; a.ServiceChoice = 0;
                    return "已加入日程，到场才认识，开始时按确认的付款方式消费，实际完成后结算。";
                }, _ => start >= 0, "暂无双方可赴约的时段"));
            }
            if (n == 61) { Offer("AA · 我付 ¥300", "social_61"); Offer("我请 · ¥600", "social_61", true); }
            else if (n == 62) { Offer("生态向导 · ¥900", "social_62"); Offer("向导我请 · ¥1,800", "social_62", true); Offer("私享野餐 · ¥2,200", "social_62_picnic"); Offer("野餐我请 · ¥4,400", "social_62_picnic", true); }
            else if (n is 65 or 67)
            {
                Offer("砂锅 AA · ¥480", "social_" + n, after: true); Offer("砂锅我请 · ¥960", "social_" + n, true, after: true);
                Offer("私厨 AA · ¥1,200", "social_" + n + "_private", after: true); Offer("私厨我请 · ¥2,400", "social_" + n + "_private", true, after: true);
                Offer("商量让对方请砂锅 · 魅力45、交情35起有机会", "social_" + n, hostPays: true, after: true);
            }
            else if (n == 66)
            {
                Offer("班前快餐 · AA ¥160", "social_66_before");
                Offer("送餐到岗 · 自备实际餐食", "social_66_delivery", at: CityPersonState(person).WorkPlaceId);
                Offer("约下班快餐 · AA ¥160", "social_66", nextShiftEnd: true);
            }
            else Offer("确认赴约 · 活动免费，购物另付", "social_" + n);
            choices.Add(Choose("改天再约", _ => "本次未预约，不扣钱或交情。")); choices.Add(Choose("这次不参加", _ => "你婉拒了，交情不变。"));
            return EventScene(template, body + "\n同行：" + CityPerson(person).Name + " · " + City.GetPlace(venue).Name, choices.ToArray());
        }
        private long FindOutdoorStart(string def, string venue, string person, bool afterWork, long earliest)
        {
            if (!City.HasPlace(venue)) return -1;
            long first = SuggestCityActivityStartAt(def, person, earliest, venue);
            for (int i = 0; i < 35 * 1440; i += 15)
            {
                long t = first + i;
                if (afterWork)
                {
                    var p = CityPerson(person);
                    // Overnight workers finish in the morning; don't offer them an evening slot during their next shift.
                    if (p.WorkEnd < p.WorkStart)
                    { if (t % 1440 < p.WorkEnd || t % 1440 > p.WorkEnd + 180) continue; }
                    else if (t % 1440 < 19 * 60 || t % 1440 > 23 * 60) continue;
                }
                if (CityActivityBlockerAt(def, new[] { person }, t, false, venue) == null) return t;
            }
            return -1;
        }
        private string OutdoorScheduleBlocker(CityActivityDefinition def, long start)
        {
            if (ExhibitionScheduleBlocker(def, start) is string exhibitionBlocker) return exhibitionBlocker;
            if ((def.LogicalId ?? def.Id) == "social_70" && !HasOpenedComputerShop) return "需要先开办一家已经装修好的电脑店";
            if (def.Id == "social_66_before")
            {
                long shift = start / 1440 * 1440 + CityPerson("resident_maoning").WorkStart;
                if (shift < start) shift += 1440;
                if (shift - start > 180 || start + def.Minutes > shift - 10) return "请约在上班前三小时内，吃完后预留返岗时间";
            }
            if ((def.LogicalId ?? def.Id) == "social_62" && !new GameDate((int)(start / 1440)).IsWeekend) return "郊游约在周末";
            return null;
        }
        private void StartOutdoorBatch(CityActivity a)
        {
            int n = OutdoorNumber(a.DefinitionId); if (n == 0) return;
            var state = new OutdoorBatchState { ActivityId = a.Id, Number = n, Message = "到场后选择实际互动。" };
            if (n >= 73) state.PartId = "model:garden:greenhouse";
            _outdoorBatch.Add(state);
            if (n is 62 or 64 || n >= 68) QueueOutdoorWalk(a, state, 0);
        }
        private void QueueOutdoorWalk(CityActivity a, OutdoorBatchState state, int node)
        {
            var venue = City.GetPlace(a.VenueId); var door = DoorOf(venue); long now = Clock.Now.TotalMinutes;
            double yaw = venue.Heading * Math.PI / 180, lx = new[] { -.18, .20, .05 }[node % 3] * venue.Width;
            double lz = new[] { .18, .38, .57 }[node % 3] * venue.Depth;
            MapPoint Anchor(int seat) => new MapPoint(door.X + (lx + seat * .6) * Math.Cos(yaw) + lz * Math.Sin(yaw), door.Y - (lx + seat * .6) * Math.Sin(yaw) + lz * Math.Cos(yaw));
            CityRoute Route(MapPoint from, int seat) => new CityRoute { IntentId = a.Id + ":stop:" + node + ":" + seat, TargetPlaceId = venue.Id,
                DepartMinute = now, ArriveMinute = now + (state.Number <= 64 ? 6 : 4), Transport = VehicleKind.Walk, Nodes = Array.Empty<string>(),
                X = new[] { from.X, Anchor(seat).X }, Y = new[] { from.Y, Anchor(seat).Y }, Meters = new[] { DistanceCity(from, Anchor(seat)) } };
            a.PlayerVenueRoute = Route(PlayerPosition, 0);
            for (int i = 0; i < a.Participants.Length; i++)
            {
                var p = CityPersonState(a.Participants[i].PersonId); p.Route = Route(new MapPoint(p.X, p.Y), i + 1);
                a.Participants[i].IntentId = p.Route.IntentId; p.TaskLabel = state.Number <= 64 ? "步道同行" : "走访档口";
            }
            state.Pending = "walk"; state.ReadyAt = a.PlayerVenueRoute.ArriveMinute;
            state.Message = "正前往第 " + (node + 1) + " 处现场。";
        }
        public string OutdoorActivityStatus(string id)
        {
            var state = _outdoorBatch.FirstOrDefault(s => s.ActivityId == id); if (state == null) return null;
            return state.Message + (state.Pending != null ? "\n还需 " + Math.Max(0, state.ReadyAt - Clock.Now.TotalMinutes) + " 游戏分钟；收起面板让时间继续。" : "");
        }
        private void TickOutdoorBatch(long now)
        {
            foreach (var state in _outdoorBatch.Where(s => s.Pending != null && now >= s.ReadyAt).ToArray())
            {
                var a = _cityActivities.FirstOrDefault(x => x.Id == state.ActivityId && x.State == CityActivityPhase.Running);
                if (a == null) { state.Pending = null; continue; }
                string task = state.Pending; state.Pending = null;
                if (task == "walk")
                {
                    state.Visited++;
                    if (state.Number == 64) state.Message = new[] {
                        "样株一：叶片有三条明显主脉，揉叶有清香。请选择树种；也可以先观察。",
                        "样株二：叶片革质、边缘细锯齿，枝间有小簇香花。请选择树种。",
                        "样株三：叶片像小扇子，叶脉放射展开。请选择树种。" }[Math.Min(2, state.Visited - 1)];
                    else if (state.Number == 63 && state.Mode > 1)
                    { state.Pending = "work"; state.ReadyAt = now + 8; state.Message = "已到达任务现场，开始八分钟的维护操作。"; }
                    else if (state.Number is 71 or 72) state.Message = "已到青湾旧物市场第 " + state.Visited + " 个摊位。可现场学习，也可打开市场看货、购买或维修。";
                    else if (state.Number >= 73) ExhibitionNodeArrived(a, state);
                    else if (state.Number >= 68) CaptureOutdoorQuote(a, state);
                    else state.Message = "已走到第 " + state.Visited + " 处现场，可继续、休息或选择本次任务。";
                }
                else if (task == "expo_fix")
                {
                    bool right = state.Mode == state.Visited;
                    if (right) { state.Answers = state.Visited; state.Correct++; state.Message = "该展台问题已解决，可以走到下一展台。"; }
                    else { Player.SpendEnergy(2); state.Message = "操作后问题仍在，精力 −2；请重新观察并选择处理方法。"; }
                }
                else if (task == "oldmarket_talk") { state.Tested = true; state.Message = "已完成本场现场观察和学习，可以继续走访、挑选实物。"; }
                else if (task == "expo_talk") { state.Tested = true; state.Message = "这段现场体验与交流已完成，可以继续看展。"; }
                else if (task == "test") { state.Tested = true; var part = PartCatalog.Get(state.PartId); state.Message = $"核对完成：{part.Name}，代际 {part.Tier}，标称功率 {part.Watts}W。"; }
                else if (task == "work") { a.Choices = a.Choices.Union(new[] { "outdoor-work" }).ToArray(); state.Message = state.Mode == 2 ? "已在现场扶正并固定倒伏植物。" : "已检查损坏设施并提交位置记录。"; }
                else state.Message = "这次休息／交流已实际完成，可以继续活动。";
                Say(state.Message);
            }
        }
        private void CaptureOutdoorQuote(CityActivity a, OutdoorBatchState state)
        {
            var part = PartCatalog.Get(state.PartId);
            if (state.Number == 69 && state.Visited > 1)
            {
                if (state.Visited == 2)
                {
                    state.QuoteSources = state.QuoteSources.Concat(new[] { Online1788.Shopping.Id }).ToArray();
                    state.QuoteNames = state.QuoteNames.Concat(new[] { "网购 · 单价，另计统一运费" }).ToArray();
                    state.QuoteCents = state.QuoteCents.Concat(new[] { OnlinePrice(Online1788.Shopping, part).Cents }).ToArray();
                    state.Message = "李承业在平台核对同款报价：" + OnlinePrice(Online1788.Shopping, part) + "，运费另计，次日到货。";
                }
                else if (DirectAgreement)
                {
                    state.QuoteSources = state.QuoteSources.Concat(new[] { "direct:zhou" }).ToArray();
                    state.QuoteNames = state.QuoteNames.Concat(new[] { "已签协议直供 · 至少3件" }).ToArray();
                    state.QuoteCents = state.QuoteCents.Concat(new[] { DirectPrice(part).Cents }).ToArray();
                    state.Message = "已核对直供协议单价 " + DirectPrice(part) + "，至少3件，按真实协议条件下单。";
                }
                else state.Message = "三个核对节点已完成。当前有市场与网购两份可用单价；未签直供协议，不虚构第三家报价。";
                return;
            }
            var places = WholesaleStallsAt(City.GetPlace(a.VenueId)).Where(p => StallSells(p, part)).ToArray();
            if (places.Length == 0) { state.Message = "本市场没有这个型号，请更换型号。"; return; }
            var place = places[(state.Visited - 1) % places.Length];
            state.QuoteSources = state.QuoteSources.Concat(new[] { place.Id }).ToArray();
            state.QuoteNames = state.QuoteNames.Concat(new[] { place.Name }).ToArray();
            state.QuoteCents = state.QuoteCents.Concat(new[] { StallPrice(place, part).Cents }).ToArray();
            state.Message = "现场报价 " + state.QuoteCents.Length + "：" + place.Name + " · " + part.Name + " · " + StallPrice(place, part)
                + "。本市场统一报价。";
        }
        public bool OutdoorRepeatChoice(CityActivity a, int choice) => IsOutdoorBatchActivity(a) &&
            (choice == 1 && (OutdoorNumber(a.DefinitionId) == 62 || OutdoorNumber(a.DefinitionId) >= 68) || a.DefinitionId == "social_64" && choice > 0
                || a.ServiceDefinitionId == "social_74_work" && choice > 1);
        private bool TryOutdoorChoice(CityActivity a, int choice, out string reply)
        {
            reply = null; var state = OutdoorState(a); if (state == null) return true;
            int n = state.Number;
            if (choice == 0) { reply = "本次项目已确认，请按现场选项继续。"; return true; }
            if (n >= 73) return TryExhibitionChoice(a, state, choice, out reply);
            if (n is 71 or 72) return TryOldMarketActivityChoice(a, state, choice, out reply);
            if (state.Pending != null) return Fail("请先走到现场或完成当前操作，再选择下一项。");
            if (n == 61) { reply = choice == 1 ? "请打开池塘实际甩竿；钓到鱼后可选择当面赠送。" : "周国良讲起年轻时的河边生活，完整相处后记入人物经历。"; return true; }
            if (n == 62)
            {
                if (choice == 1) { if (state.Visited >= 3) return Fail("路线已走完，可等待本场结束。"); QueueOutdoorWalk(a, state, state.Visited); }
                if (choice == 2) { state.Pending = "rest"; state.ReadyAt = Clock.Now.TotalMinutes + 5; }
                if (choice == 3) { if (state.Visited >= 3) return Fail("路线已走完。"); state.Mode = 3; QueueOutdoorWalk(a, state, state.Visited); }
                reply = "按所选路线实际行走或休息。"; return true;
            }
            if (n == 63)
            {
                if (state.Mode != 0 && state.Mode != choice) return Fail("本场已选择主要任务，不能切换领取所有奖励。");
                state.Mode = choice;
                if (state.Visited == 0) QueueOutdoorWalk(a, state, choice - 1);
                else if (choice > 1 && !a.Choices.Contains("outdoor-work")) { state.Pending = "work"; state.ReadyAt = Clock.Now.TotalMinutes + 8; }
                reply = state.Visited == 0 ? "先走到任务现场，到达后再次选择该任务开始操作。" : "正在现场操作，需要八分钟。"; return true;
            }
            if (n == 64)
            {
                if (state.Visited == 0) return Fail("先走到样株旁。");
                if (choice >= 1 && choice <= 3)
                {
                    if (state.Answers >= state.Visited) return Fail("这一株已经作答，请去看下一株。");
                    bool right = choice == state.Visited; state.Answers++; if (right) state.Correct++;
                    reply = right ? "辨认正确，已记下这株植物。" : "辨认有误，石扬解释了区别；这题没有见识奖励，可以继续下一株。";
                }
                else if (choice == 4) reply = state.Message;
                else { if (state.Answers < state.Visited || state.Visited >= 3) return Fail("请先完成当前辨认，或本次三株已看完。"); QueueOutdoorWalk(a, state, state.Visited); reply = "正在走向下一株。"; }
                return true;
            }
            if (n is 65 or 67 || n == 66 && a.ServiceDefinitionId != "social_66_delivery")
            {
                if (state.Mode != 0 && state.Mode != choice) return Fail("这次已经选定主要聊天方向。");
                state.Mode = choice; state.Pending = "talk"; state.ReadyAt = Clock.Now.TotalMinutes + 5;
                reply = choice == 2 ? "实际聊工作经历与经营；涉及配送合作仍需另下真实订单。" : "坐下来吃饭交流，五分钟后完成这一段。"; return true;
            }
            if (n == 66)
            {
                if (state.Delivered) return Fail("这场已交付餐食，不能重复赠送。");
                var food = Backpack.Food.Packed.FirstOrDefault(b => b.Count > 0 && FastFoodCatalog.Packed(b.Id)?.Drink != true && (b.ExpireDay < 0 || Clock.Today.TotalDays <= b.ExpireDay));
                if (food == null) return Fail("请先实际购买或做好一份未过期餐食，饮料不能代替夜宵。");
                food.Count--; if (food.Count == 0) Backpack.Food.Packed.Remove(food); state.Delivered = true; ApplyCargoLimit();
                reply = "已在岗亭当面交付 " + food.Name + "，餐食从背包扣除，茅宁仍在工作地点。"; return true;
            }
            if (choice == 1)
            {
                if (state.Visited >= 3) return Fail("三个档口已经走访完。");
                QueueOutdoorWalk(a, state, state.Visited); reply = "正在走向下一档口采集真实报价。"; return true;
            }
            if (n == 69 && choice is 2 or 3 or 4)
            {
                if (state.Visited < 3 || state.QuoteCents.Length < 2) return Fail("先走完三个核对节点再判断。");
                if (choice - 2 >= state.QuoteCents.Length) return Fail("当前没有第三来源的有效报价。");
                if (state.Answers > 0) return Fail("本次已作出判断，不能反复试答案。");
                state.Answers = 1; state.Correct = state.QuoteCents[choice - 2] == state.QuoteCents.Min() ? 1 : 0;
                reply = state.Correct == 1 ? "按真实商品单价判断正确；下单时还需考虑运费、起订量和到货时间。" : "这家并非当前最低单价；可以结合运费和到货时间再决定是否采购。"; return true;
            }
            if (n == 70 && choice == 2)
            {
                state.Pending = "test"; state.ReadyAt = Clock.Now.TotalMinutes + 5;
                reply = "已开始上机核对所选型号，五分钟后显示核对结果。"; return true;
            }
            if ((n == 70 && choice == 3) || (n == 69 && choice == 5))
            {
                if (state.Negotiated) return Fail("本场已经尝试谈价或账期，不能反复刷概率。");
                if (n == 70 && !state.Tested) return Fail("先完成所选显卡实测。");
                state.Negotiated = true;
                double chance = EventScript.ProgressiveChance(.5, (Player.Intelligence, 50, .01), (Relation(OutdoorPeople[n - 61]), 35, .015));
                bool ok = Random.Chance(chance);
                if (ok && n == 70) state.CreditGranted = true;
                if (ok && n == 69) { var market = PurchaseDestination(CurrentPlace); _bargains[market.Id] = (Clock.Today.TotalDays, .04); }
                reply = $"成功率 {chance:P0}。" + (ok ? (n == 70 ? "已同意30%首款、14天尾款；还需你确认实际订单。" : "本市场今日采购折扣已更新，可核对真实报价后下单。") : "商谈失败，没有订单、费用或债务。属性不足仍可选，但必定失败。"); return true;
            }
            reply = "可以在下方选择型号、数量和付款方式；不采购也可以结束走访。"; return true;
        }
        private bool OutdoorCompletionReady(CityActivity a)
        {
            var s = OutdoorState(a); if (s == null) return true;
            if (s.Number >= 73) return ExhibitionCompletionReady(a, s);
            return s.Pending == null && (s.Number switch { 62 => s.Visited >= 3, 63 => s.Mode == 1 && s.Visited > 0 || a.Choices.Contains("outdoor-work"),
                64 => s.Answers >= 3, 66 when a.ServiceDefinitionId == "social_66_delivery" => s.Delivered, 71 or 72 => s.Visited >= 3 && s.Tested, >= 68 => s.Visited >= 3, _ => true });
        }
        private void CompleteOutdoorBatch(CityActivity a)
        {
            var s = OutdoorState(a); if (s == null) return;
            if (s.Number >= 73) { CompleteExhibition(a, s); return; }
            if (s.Number is 71 or 72 && s.Tested) Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
            if (s.Number == 64) Player.Intelligence = Math.Min(100, Player.Intelligence + Math.Min(2, s.Correct));
            if (s.Number == 69 && s.Correct == 1) Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
            if (s.Number == 68 || s.Number == 70 && s.Tested) Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
            if (s.Number == 63 && s.Mode > 1) { Player.Intelligence = Math.Min(100, Player.Intelligence + 1); AddRelation(OutdoorPeople[2], 2); }
            if (s.Number is 65 or 67 && s.Mode == 1) AddRelation(OutdoorPeople[s.Number - 61], 2);
            if (s.Number <= 67 && a.PlayerTreat) AddRelation(OutdoorPeople[s.Number - 61], 2);
            if (s.Number == 61 && a.Choices.Contains("2")) Flags.Add("outdoor:grandpa-story");
            a.EventSettlementNote = (a.EventSettlementNote ?? "") + $"\n现场节点 {s.Visited}；辨认 {s.Correct}/{s.Answers}；实际采购 {s.OrderCount} 件。";
        }
        private void SaveOutdoorBatch(SaveData d) => d.outdoorBatch = _outdoorBatch.Select(s => s.Copy()).ToArray();
        private void RestoreOutdoorBatch(SaveData d)
        {
            _outdoorBatch.Clear();
            foreach (var source in d.outdoorBatch ?? Array.Empty<OutdoorBatchState>())
            {
                var s = source.Copy();
                if (s.QuoteNames.Length != s.QuoteSources.Length) s.QuoteNames = s.QuoteSources.Select(id => City.GetPlace(id)?.Name ?? id).ToArray();
                s.PurchaseSource = Math.Max(0, Math.Min(Math.Max(0, s.QuoteSources.Length - 1), s.PurchaseSource));
                _outdoorBatch.Add(s);
            }
        }
    }
    public sealed partial class SaveData { public OutdoorBatchState[] outdoorBatch; }
}
