using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        internal bool CanOfferExhibitionEvent(int number)
        {
            string person = ExhibitionHost(number);
            return person != null && CityPersonState(person) != null && City.HasPlace("life_expo")
                && DistrictAccessBlocker(City.GetPlace("life_expo")) == null
                && (number != 77 || CityPersonState("life_enterprise") != null)
                && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_" + number);
        }
        private static string ExhibitionHost(int n) => n switch { 73 => "life_curator", 74 => "resident_hanzheng", 75 => "life_creator", 77 => "story_shenxiao", _ => null };
        private EventDef BindExhibitionEvent(EventDef template, int number)
        {
            if (!CanOfferExhibitionEvent(number)) return null;
            string person = ExhibitionHost(number);
            string[] guests = number == 77 ? new[] { person, "life_enterprise" } : new[] { person };
            string body = number switch {
                73 => "任帆为你提供本届电子展普通入场名额。先实际走到展位，再体验新品、交流供货条件、了解企业需求。普通参观免费；商务采购需要你保有已开办的电脑店，订单另外确认。",
                74 => "韩征邀请你在电子展开幕前一天看布展。可只参观，也可花九十分钟协助三个展台的供电与信号调试；全部正确完成后支付劳务费 ¥1,500，未完成不发全额报酬。",
                75 => "陆栀邀请你到原创潮玩展的展位看原型、比较结构、反馈设计。普通入场由她提供，购买现货另外付款；常规款与展期限定款沿用真实商品、价格和限额。",
                _ => "沈晓想和父亲一起看城市模型。家长必须实际同行，按展期赴约。可以看街景细节、听他说喜欢的小凳子、一起观察搭建；也可买一件真实现货赠送，不必购买才能陪逛。" };
            var choices = new List<EventChoice>();
            void Offer(string label, string definition)
            {
                long start = SuggestCityActivityStartAt(definition, person, 0, "life_expo"); long found = -1;
                for (int i = 0; i < 93 * 1440; i += 15)
                    if (CityActivityBlockerAt(definition, guests, start + i, false, "life_expo") == null) { found = start + i; break; }
                choices.Add(Choose(label + (found >= 0 ? " · " + new GameTimestamp(found) : " · 暂无时段"), s =>
                {
                    var a = s.ScheduleCityActivity(definition, guests, found, sourceId: "exhibit:" + number + ":" + s._nextCityActivity);
                    if (a == null) return "日程已变化，本次未预约或扣款。";
                    a.HostId = person; a.HostPays = true; a.ServiceConfirmed = true; a.ServiceChoice = 0;
                    return "已加入日程，实际到场才认识，按现场操作完成后再结算。";
                }, _ => found >= 0, "没有展期内双方可到场的时段"));
            }
            Offer("确认赴约 · 普通入场免费", "social_" + number);
            if (number == 74) Offer("协助调试 · 完成后收 ¥1,500", "social_74_work");
            choices.Add(Choose("改天再约", _ => "本次未预约，不影响交情。")); choices.Add(Choose("这次不参加", _ => "你婉拒了，没有费用或交情损失。"));
            return EventScene(template, body, choices.ToArray());
        }
        private string ExhibitionScheduleBlocker(CityActivityDefinition def, long start)
        {
            int n = OutdoorNumber(def.LogicalId ?? def.Id); if (ExhibitionHost(n) == null) return null;
            int day = (int)(start / 1440) % 30 + 1;
            if (n == 74 && day != 9) return "布展参观与调试约在每月9日，正式电子展开幕前一天";
            if (n == 73 && (day < 10 || day > 12)) return "电子展在每月10–12日开放";
            if (n >= 75 && (day < 22 || day > 24)) return "原创潮玩展在每月22–24日开放";
            if (n == 77 && !new GameDate((int)(start / 1440)).IsWeekend) return "亲子看展约在展期内的周末休息日";
            return null;
        }
        private void ExhibitionNodeArrived(CityActivity a, OutdoorBatchState s)
        {
            s.Message = s.Number switch {
                73 => new[] { "已到新品体验台，可比较真实型号参数。", "已到供应商交流区，可交流实际进货渠道。", "已到企业需求区，可了解试单条件。" }[Math.Min(2, s.Visited - 1)],
                74 when a.ServiceDefinitionId == "social_74_work" => new[] { "展台一：同一供电支路负载过多。请选处理方法。", "展台二：设备无电，插头完好，电源开关未接通。请选处理方法。", "展台三：屏幕有电但无画面，信号线松脱。请选处理方法。" }[Math.Min(2, s.Visited - 1)],
                74 => "已到布展现场第 " + s.Visited + " 个展台，韩征介绍布展与设备安排。",
                75 => new[] { "已到陆栀的原型展示台。", "已到结构体验台，可观察连接与拼装方式。", "已到现货展示台，可反馈设计，也可按实价购买。" }[Math.Min(2, s.Visited - 1)],
                _ => new[] { "沈晓和父亲已一起到城市微缩景展示台。", "沈晓指着窗边，聊起他记得的小凳子；父亲在旁边听。", "已到搭建体验区，可以继续陪逛或选择购买真实现货。" }[Math.Min(2, s.Visited - 1)] };
        }
        private bool TryExhibitionChoice(CityActivity a, OutdoorBatchState s, int choice, out string reply)
        {
            reply = null;
            if (s.Pending != null) return Fail("请先完成当前行走或操作。");
            if (choice == 1)
            {
                if (s.Visited >= 3) return Fail("三个展位已看完。");
                if (s.Number == 74 && a.ServiceDefinitionId == "social_74_work" && s.Answers < s.Visited) return Fail("先处理好当前展台的问题。");
                QueueOutdoorWalk(a, s, s.Visited); reply = "正走向下一个展位。"; return true;
            }
            if (s.Number == 74 && a.ServiceDefinitionId == "social_74_work")
            {
                if (s.Answers >= s.Visited) return Fail("这个展台已经完成，请去下一个展台。");
                s.Mode = choice - 1; s.Pending = "expo_fix"; s.ReadyAt = Clock.Now.TotalMinutes + 5;
                reply = "已选择处理方式，实际操作五分钟后检查结果。"; return true;
            }
            if (s.Number == 73 && choice == 4)
            {
                if (!HasOpenedComputerShop) return Fail("普通参观可以继续，商务采购需要先开办电脑店。");
                s.Pending = "expo_talk"; s.ReadyAt = Clock.Now.TotalMinutes + 5; s.Mode = 4;
                reply = "开始核对实际进货渠道与企业试单条件；不会自动生成采购。"; return true;
            }
            if (s.Mode != 0 && s.Mode != choice) return Fail("本次已选择主要体验或反馈方向。");
            s.Mode = choice; s.Pending = "expo_talk"; s.ReadyAt = Clock.Now.TotalMinutes + 5;
            reply = s.Number == 75 ? "开始观察原型、体验结构或讨论反馈，五分钟后完成。" : s.Number == 77 ? "你和父亲一起听沈晓说、看模型细节，五分钟后完成这一段。" : "在当前展位实际体验交流五分钟。";
            return true;
        }
        private bool ExhibitionCompletionReady(CityActivity a, OutdoorBatchState s) => s.Visited >= 3 && s.Pending == null
            && (s.Number != 74 || a.ServiceDefinitionId != "social_74_work" || s.Correct >= 3);
        private void CompleteExhibition(CityActivity a, OutdoorBatchState s)
        {
            if (s.Number == 74 && a.ServiceDefinitionId == "social_74_work" && s.Correct >= 3)
            {
                Ledger.Credit(Clock.Today, "布展调试劳务", Money.FromYuan(1500), "韩征的三个展台调试");
                a.EventSettlementNote = "三个展台已完成正确调试，劳务报酬 ¥1,500 已到账。";
            }
            if (s.Number == 73 || s.Number == 75 && s.Mode == 3 && s.Tested) Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
            if (s.Number == 77 && s.Mode == 2 && s.Tested) Flags.Add("exhibit:shenxiao-details");
            if (s.Number == 75 && s.Mode == 4 && s.Tested) { Flags.Add("exhibit:luzhi-feedback"); Player.Charm = Math.Min(100, Player.Charm + 1); }
        }
        public GiftDef[] ExhibitionModels(string activityId)
        {
            var a = RunningCityActivity; return a?.Id == activityId && a.DefinitionId is "social_75" or "social_77"
                ? ModelProducts.All.ToArray() : Array.Empty<GiftDef>();
        }
        public bool BuyExhibitionModel(string activityId, string productId)
        {
            var a = RunningCityActivity; var s = OutdoorState(a); var product = Gifts.Get(productId);
            if (a?.Id != activityId || s == null || s.Number is not (75 or 77) || CurrentPlace?.Id != a.VenueId || s.Pending != null || !CityComicExpoOpen
                || product == null || !ModelProducts.IsModel(productId)) return false;
            if (product.LimitedEdition) return BuyCityExpoModel(productId, 1);
            if (!CanCarry(product.WeightKg) || !Ledger.TryDebit(Clock.Today, "展位作品购买", product.Price, product.Name)) return false;
            Backpack.AddGift(product.Id, 1); ApplyCargoLimit(); Say("已购入 " + product.Name + "，放入背包。"); return true;
        }
        public Money ExhibitionModelPrice(string productId)
        {
            var product = Gifts.Get(productId);
            return product == null ? Money.Zero : product.LimitedEdition ? CityRetailWholesalePrice(CurrentPlace, product) : product.Price;
        }
        public bool GiftExhibitionModel(string activityId, string productId)
        {
            var a = RunningCityActivity; var s = OutdoorState(a);
            if (a?.Id != activityId || s?.Number != 77 || s.Delivered || s.Pending != null || !CitySamePlace("story_shenxiao") || !CitySamePlace("life_enterprise")
                || !ModelProducts.IsModel(productId)) return false;
            if (_staffGiftDay.TryGetValue("story_shenxiao", out int last) && Clock.Today.TotalDays - last < 3) return Fail("沈晓最近已收到礼物，可以继续陪逛。");
            if (!Backpack.TakeGift(productId)) return false;
            _staffGiftDay["story_shenxiao"] = Clock.Today.TotalDays;
            s.Delivered = true; AddRelation("story_shenxiao", 2); ApplyCargoLimit(); Say("已当着父亲的面把这件现货模型送给沈晓；陪逛不要求购买或送礼。"); return true;
        }
    }
}
