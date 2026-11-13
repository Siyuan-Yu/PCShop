using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private static readonly string[] SocialBatchIds = { "race_prize_dinner", "race_track_visit_rain", "race_ning_study",
            "act_dinner_cook", "act_dinner_chef", "act_dinner_linhe", "act_dinner_farm", "act_ktv_singer", "act_ktv_fuzhi", "act_ktv_luoya" };
        private static readonly string[] SocialBatchPeople = { null, null, "life_ning", "res_cook", "life_chef", "resident_linhe",
            "keeper_farm", "life_singer", "resident_fuzhi", "resident_luoya" };
        public const int WetPracticeFee = 1000, AudioHelpPayment = 1500, FamilyGiftPrice = 1000;
        private string _celebrationRaceKey;
        private string[] _celebrationRacePeople = Array.Empty<string>();

        private static int SocialBatchNumber(string id) => Array.IndexOf(SocialBatchIds, id) + 41;
        private string SocialBatchVenue(int number)
        {
            string person = SocialBatchPeople[number - 41];
            if (number == 41) return "life_restaurant";
            if (number == 43) return StarterRaceTrackId;
            var state = CityPersonState(person);
            if (number == 47) return state?.HomePlaceId;
            if (number == 50) return City.PublicPlaces.Where(p => p.Business == "venue_ktv" && DistrictAccessBlocker(p) == null)
                .OrderBy(p => CityTravelMinutes(p)).FirstOrDefault()?.Id;
            return state?.WorkPlaceId;
        }
        private string[] SocialBatchGuests(int number) => number == 41
            ? _celebrationRacePeople.Where(id => CityPersonState(id) != null).Distinct().Take(5).ToArray()
            : number == 50 ? new[] { "resident_luoya", "life_singer", "resident_fuzhi" }.Where(id => CityPersonState(id) != null).Distinct().ToArray()
            : new[] { SocialBatchPeople[number - 41] };

        internal bool CanOfferSocialBatchEvent(string id)
        {
            int number = SocialBatchNumber(id);
            if (number < 41 || number > 50) return false;
            if (number == 42) return RacingAccessBlocker == null && City.HasPlace(StarterRaceTrackId)
                && !_eventRaceBookings.Any(b => b.Kind == "wet" && b.Day >= Clock.Today.TotalDays);
            if (number is 41 or 43 && RacingAccessBlocker != null) return false;
            if (_cityActivities.Any(a => a.Active && a.DefinitionId == "social_" + number)) return false;
            if (number == 41 && (string.IsNullOrEmpty(_celebrationRaceKey) || SocialBatchGuests(number).Length == 0
                || _cityActivities.Any(a => a.State == CityActivityPhase.Completed && (a.SourceId == "social41:" + _celebrationRaceKey
                    || a.SourceId?.StartsWith("social41:" + _celebrationRaceKey + ":retry:", StringComparison.Ordinal) == true)))) return false;
            string venue = SocialBatchVenue(number);
            return City.HasPlace(venue) && DistrictAccessBlocker(City.GetPlace(venue)) == null
                && SocialBatchGuests(number).All(person => CityPersonState(person) != null);
        }

        private long FindSocialBatchStart(string definitionId, string venueId, string[] guests, bool afterWork)
        {
            long earliest = afterWork ? (long)Clock.Today.TotalDays * 1440 + 19 * 60 : 0;
            long start = SuggestCityActivityStartAt(definitionId, guests.FirstOrDefault(), earliest, venueId);
            for (int offset = 0; offset < 35 * 1440; offset += 15)
            {
                long candidate = start + offset;
                if (afterWork && (candidate % 1440 < 19 * 60 || candidate % 1440 > 22 * 60)) continue;
                if (CityActivityBlockerAt(definitionId, guests, candidate, false, venueId) == null) return candidate;
            }
            return -1;
        }

        private EventDef BindSocialBatchEvent(EventDef template)
        {
            if (!CanOfferSocialBatchEvent(template.Id)) return null;
            int number = SocialBatchNumber(template.Id);
            if (number == 42) return BindWetPracticeInvitation(template);
            string definitionId = "social_" + number;
            string venueId = SocialBatchVenue(number);
            string[] guests = SocialBatchGuests(number);
            long start = FindSocialBatchStart(definitionId, venueId, guests, number is 46 or 50);
            string names = string.Join("、", guests.Select(person => CityPerson(person)?.Name ?? person));
            string body = number switch
            {
                41 => "最近一次实际夺冠后，和同场车友吃顿庆功饭。整桌 ¥3,000，由你请客，赴宴开始时才支付。",
                43 => "顾宁想先和你看一场真实比赛，了解赛车。观赛免费，不替她报名参赛；到场才认识，完整观赛后增加见识和交情。",
                44 => "罗志勇请你到自己工作的店尝新菜。试吃由店方提供，你免费，可以评价口味、分量和定价。",
                45 => "杜建川请你到自己的餐馆品尝招牌炖菜。店家招待，你免费；到场后可以聊炖菜故事和熟客偏好。",
                46 => "林荷约你下班后聊餐厅经营。双人餐合计 ¥2,000，默认各付 ¥1,000；也可以由你请客付全桌 ¥2,000。到场开始才支付。",
                47 => "王桂芬请你到她真实住处吃家常饭。她请客，你免费。可以空手去，也可以买一份 ¥1,000 的伴手礼带上。",
                48 => "温音邀请你试唱新伴奏。两小时包间原价 ¥2,000，店方招待，你免费。可选歌、合唱和反馈；续时属于另一次需要自行确认的付费服务。",
                49 => "傅知请你帮忙试听设备。不收包间费，完整完成后支付 ¥1,500 劳务报酬。比较人声、低频或杂音时，智力影响发现问题的概率；低于 50 也可选择，必定未能找出专业问题。",
                _ => "罗雅邀请你和实际团员一起排练。两小时场地与指导原价 ¥3,000，由合唱团承担，你免费。包含热身、合唱和休息交流。"
            };
            body += "\n同行：" + names + "\n地点：" + City.GetPlace(venueId).Name;
            body += start < 0 ? "\n暂时没有双方都能赴约的时段，可以下次再约。" : "\n时间：" + new GameTimestamp(start);
            var choices = new List<EventChoice>();
            choices.Add(Choose(number == 46 ? "确认赴约 · 我付 ¥1,000" : "确认赴约", s => s.BookSocialBatchActivity(number, venueId, guests, start, false, false),
                _ => start >= 0, "暂时没有可赴约的时段"));
            if (number == 46) choices.Add(Choose("这顿我请 · ¥2,000", s => s.BookSocialBatchActivity(number, venueId, guests, start, true, false),
                _ => start >= 0, "暂时没有可赴约的时段"));
            if (number == 47) choices.Add(Choose("买份伴手礼再赴约 · ¥1,000", s => s.BookSocialBatchActivity(number, venueId, guests, start, false, true),
                s => start >= 0 && s.Ledger.CanAfford(Money.FromYuan(FamilyGiftPrice)), "需要可赴约时间和 ¥1,000 伴手礼预算"));
            choices.Add(Choose("改天再约", _ => "本次未预约，也没有费用或交情损失。"));
            choices.Add(Choose("这次不参加", _ => "你婉拒了，交情不变，今后仍有机会再邀约。"));
            return EventScene(template, body, choices.ToArray());
        }

        private string BookSocialBatchActivity(int number, string venueId, string[] guests, long start, bool treat, bool gift)
        {
            if (start < 0 || gift && !Ledger.CanAfford(Money.FromYuan(FamilyGiftPrice))) return "时间或预算已经变化，未预约、未扣款。";
            string source = number == 41 ? "social41:" + _celebrationRaceKey : "social-event:" + number + ":" + _nextCityActivity;
            var previous = _cityActivities.FirstOrDefault(a => a.SourceId == source);
            if (previous != null && !previous.Active && previous.State != CityActivityPhase.Completed) source += ":retry:" + _nextCityActivity;
            var activity = ScheduleCityActivityAt("social_" + number, guests, start, false, source, venueId);
            if (activity == null) return "当前无法预约，尚未扣款或认识人物。";
            activity.HostId = number == 41 ? "player" : guests[0];
            activity.HostPays = number != 41 && number != 46;
            activity.PlayerTreat = number == 41 || treat;
            activity.ServiceConfirmed = true; activity.ServiceChoice = 0;
            if (number == 47)
            {
                Flags.Add("home-invitation:keeper_farm");
                if (gift)
                {
                    if (!Ledger.TryDebit(Clock.Today, "伴手礼采购", Money.FromYuan(FamilyGiftPrice), "家常臻选礼盒"))
                    { CancelCityActivity(activity.Id); return "伴手礼预算不足，已取消预约，未扣餐费。"; }
                    Backpack.AddGift(Gifts.VisitHamper, 1); activity.EventGiftId = Gifts.VisitHamper;
                    ApplyCargoLimit();
                }
            }
            return "已记入日程。请实际到场，见面才认识人物，完成后才结算活动奖励。"
                + (gift ? "\n¥1,000 伴手礼已买入背包，请带上；取消活动不会吞掉礼盒。" : "");
        }

        private EventDef BindWetPracticeInvitation(EventDef template)
        {
            int day = Clock.Today.TotalDays;
            int travel = CityTravelMinutes(City.GetPlace(StarterRaceTrackId));
            if (Clock.MinuteOfDay + travel + 30 >= 1080) day++;
            return EventScene(template, "城郊练习场开放人工洒水湿地训练。预约当天 09:00–18:00 到场，使用本区实际入库车辆。"
                + "\n费用 ¥1,000，抓地力降至平常的 60%、制动降至 80%，按实际操作和碰撞结算，不用胜场掷骰。维修按真实车损另算。"
                + "\n日期：" + new GameDate(day),
                Choose("预约湿地驾驶", s =>
                {
                    if (s._eventRaceBookings.Any(b => b.Kind == "wet" && b.Day >= s.Clock.Today.TotalDays)) return "已有湿地训练预约。";
                    s._eventRaceBookings.Add(new EventRaceBooking { Kind = "wet", Day = day, TrackId = StarterRaceTrackId });
                    return "湿地训练已加入日程，到场选车后从专属入口开始，报名时才扣 ¥1,000。";
                }),
                Choose("改天再跑", _ => "本次不预约、不扣钱。"));
        }

        private void RecordSocialBatchRace(RaceSimulation race)
        {
            if (race.Abandoned || race.Practice || race.Meeting == RaceMeeting.None || race.Position != 1) return;
            _celebrationRacePeople = race.Opponents.Select(o => o.PersonId).Where(id => CityPersonState(id) != null).Distinct().Take(5).ToArray();
            _celebrationRaceKey = race.Track.Id + ":" + Clock.Now.TotalMinutes;
        }

        private void StartSocialBatchActivity(CityActivity activity)
        {
            if (!activity.DefinitionId.StartsWith("social_", StringComparison.Ordinal)) return;
            foreach (var guest in activity.Participants) Meet(guest.PersonId, Math.Max(14, Relation(guest.PersonId)));
            if (!string.IsNullOrEmpty(activity.EventGiftId) && !activity.EventGiftDelivered && Backpack.TakeGift(activity.EventGiftId))
            {
                activity.EventGiftDelivered = true;
                activity.EventSettlementNote = "已当面送出家常臻选礼盒（采购支出 ¥1,000，餐费由主人承担）。";
                RememberSocial("keeper_farm", "赴家宴时当面送出了家常臻选礼盒。");
            }
        }
        private string SocialBatchChoiceReply(CityActivity activity, int choice)
        {
            if (activity.DefinitionId == "social_44" && choice > 0)
                return new[] { "", "你们讨论了咸淡和食材搭配，罗志勇记下了意见。", "你们比较了分量和出餐速度，罗志勇准备再试一锅。", "你们核对食材成本和客群，聊了定价理由。" }[Math.Min(3, choice)];
            if (activity.DefinitionId == "social_45" && choice > 0)
                return "杜建川讲起这道炖菜和老熟客的故事，活动完成后会记入人物经历。";
            if (activity.DefinitionId == "social_49" && choice > 0)
            {
                if (activity.Choices.Contains("audio-finding")) return "本次已经确认一项专业问题，额外发现奖励不会重复发放。";
                double chance = EventScript.ProgressiveChance(.5, (Player.Intelligence, EventScript.IntelligenceThreshold, .01));
                if (!Random.Chance(chance)) return $"发现问题的成功率 {chance * 100:0}%。你提出了听感，但未能定位专业问题；完整配合仍有 ¥1,500 基础报酬。";
                activity.Choices = activity.Choices.Concat(new[] { "audio-finding" }).ToArray();
                return $"发现问题的成功率 {chance * 100:0}%。你找到了需要调整的频段，完成后获得额外交情与见识奖励。";
            }
            return null;
        }
        private void CompleteSocialBatchActivity(CityActivity activity)
        {
            if (!activity.DefinitionId.StartsWith("social_", StringComparison.Ordinal)) return;
            Flags.Add("social:completed:" + activity.DefinitionId);
            if (activity.DefinitionId == "social_45") Flags.Add("social:chef-story");
            if (activity.DefinitionId == "social_47" && activity.EventGiftDelivered) AddRelation("keeper_farm", 2);
            if (activity.DefinitionId == "social_49")
            {
                Ledger.Credit(Clock.Today, "试听劳务", Money.FromYuan(AudioHelpPayment), "傅知的音响调试");
                activity.EventSettlementNote = "试听劳务报酬已到账 ¥1,500。";
                if (activity.Choices.Contains("audio-finding"))
                { AddRelation("resident_fuzhi", 2); Player.Intelligence = Math.Min(100, Player.Intelligence + 1); activity.EventSettlementNote += "定位了专业问题，额外交情 +2、见识 +1。"; }
            }
        }
        private IEnumerable<string> SocialBatchLearnedStories(string personId)
        {
            if (personId == "life_chef" && Flags.Contains("social:chef-story"))
                yield return "杜建川的招牌炖菜从后厨试菜留了下来。他更在意的是，让忙了一天的熟客愿意坐下，吃一口热饭。";
            if (personId == "resident_luoya" && Flags.Contains("social:completed:social_50"))
                yield return "你和罗雅参加过社区合唱团排练，认识了实际到场的团员。以后仍可以再约练习。";
        }

#if UNITY_EDITOR
        private void PrepareSocialBatchAcceptance(int number)
        {
            _district.UnlockedTier = 3;
            Flags.Add(RacingAccessFlag); Flags.Add(RacingIntroShownFlag);
            if (number == 41) { _celebrationRaceKey = "debug:" + _nextCityActivity; _celebrationRacePeople = new[] { "racer_0", "racer_1" }; }
            if (number == 42) PrepareRacingBatchAcceptance(39);
        }
#endif
    }
    public sealed partial class SaveData
    {
        public string celebrationRaceKey;
        public string[] celebrationRacePeople;
    }
}
