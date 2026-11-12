using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const int ConfessionMinFamiliarity = 71;
        public const int ConfessionCooldownDays = 7;
        private const string LuZhi = "life_creator";
        private const string HoldHandsChoice = "partner:hold-hands";
        private const string PersonalTalkChoice = "partner:talk";

        // Each entry is an adult, single character who is open to a relationship.
        private static readonly IReadOnlyDictionary<string, string> PartnerTalk = new Dictionary<string, string>
        {
            // 城郊
            ["life_ming"] = "今天修好了一台旧机器。听见它重新响起来的时候，我就想讲给你听。",
            ["resident_sujun"] = "我下班揉了点面。下次你来，我做一碗刚出锅的给你。",
            ["resident_leiming"] = "我练了一小段口琴。只练熟了开头，你愿意听的话，我吹给你听。",
            ["keeper_agent"] = "我路过一扇好看的旧门，拍下来了。下次想跟你一起慢慢走那条街。",
            ["keeper_phone"] = "今天看了一整天屏幕。见到你，我倒想把手机收起来，好好说会儿话。",
            ["resident_dengyue"] = "我画了两张早餐店的招牌速写。哪张更像我们平时一起走的那条街？",
            ["life_coach"] = "收完球我还留了点时间。今天不用计分，想跟你沿河走一圈。",
            ["racer_0"] = "头盔已经收起来了。和你一起走的时候，我一点也不赶时间。",
            // 社区
            ["momo"] = "今天的教程已经拍完了。接下来这会儿留给我们，没开镜头。",
            ["ake"] = "直播关了。跟你在一起的时候，不找话题也挺自在。",
            ["qiao"] = "我把工作用的消息都关了。今天想只听听你最近过得怎么样。",
            ["resident_baiming"] = "我找到一扇傍晚会发光的窗。下次带你去看，那条路走着也舒服。",
            ["life_curator"] = "展位图终于画完了。今天想和你去一个不用先看平面图的地方。",
            ["life_ning"] = "今天夜校下课早。我想先见你，再慢慢想晚饭吃什么。",
            ["resident_zengyu"] = "我试了一个新配方，酸一点，没那么甜。下次想先给你尝。",
            // 公园
            [LuZhi] = "我今天画了一扇旧窗。等下把本子给你看，然后我们聊点作品以外的事。",
            ["life_designer"] = "收店时顺手留了一块好看的布。下次见面我给你看，看见它时我刚好想起你。",
            ["resident_ningxi"] = "今天拍的照片里，有一张光线很像上次我们散步的时候。我留着想给你看。",
            ["resident_taoran"] = "跑团已经散了。跟你走一段，我可以故意慢一点。",
            ["resident_zhaoyu"] = "下雨那会儿我录到一段很好听的声音。今天想先和你一起听。",
            ["res_coder"] = "今天已经盯够屏幕了。下次去河边吧，我带上以前钓鱼用的那副线。",
            ["racer_2"] = "赛道这边都整备好了。终于可以不看秒表，和你待一会儿。",
            ["lin"] = "见到你还是会想起以前那块球场。我们现在有空，也还可以一起去。",
            // 花园
            ["resident_jiangning"] = "我在地图上圈了一条安静的路。不是给客人的路线，是想和你一起走的。",
            ["life_golfer"] = "今天的课上完了。等下我们走一段，球杆就留在场边吧。",
            ["resident_jiyun"] = "客人都安顿好了。我想带你看看那盆新开的兰花。",
            ["resident_xiaqing"] = "今天的位置总算都排妥了。晚上还留了一小会儿，想和你拼几块。",
            ["racer_3"] = "今天聊了一整天车。和你见面的时候，我更想听你说说自己的事。",
            ["resident_siyu"] = "今天下班路上看见一只没认出来的鸟。下次想和你一起去碰碰运气。",
            ["resident_luoya"] = "散场后还有一小段旋律在脑子里。走一会儿吧，我哼给你听。",
        };

        public static bool IsRomanceCandidate(string personId) =>
            personId != null && PartnerTalk.ContainsKey(personId) && CityLifeCatalog.Person(personId)?.Age >= 18;

        public bool IsPlayerPartner(string personId) => personId != null && RomanceId == personId;

        public string PersonRelationshipText(string personId)
        {
            if (IsPlayerPartner(personId)) return "你的伴侣";
            if (!HasGreeted(personId)) return null;
            if (IsRomanceCandidate(personId)) return "单身";
            switch (personId)
            {
                case "res_tea": return "恋爱中";
                case "res_mom": return "已婚";
                case "res_grandpa": return "丧偶";
                default: return null;
            }
        }

        public static double ConfessionChance(int familiarity)
        {
            if (familiarity < ConfessionMinFamiliarity) return 0;
            if (familiarity <= 80) return .30 + (familiarity - 71) * .20 / 9;
            if (familiarity <= 90) return .50 + (familiarity - 80) * .25 / 10;
            return .75 + (Math.Min(100, familiarity) - 90) * .20 / 10;
        }

        public string ConfessionBlocker(string personId)
        {
            if (!IsRomanceCandidate(personId) || CityPersonState(personId) == null) return "对方目前无法发展恋爱关系";
            if (IsGameOver) return "现在无法表白";
            if (IsPlayerPartner(personId)) return "你们已经是伴侣";
            if (RomanceId != null) return "你已有伴侣";
            if (Relation(personId) < ConfessionMinFamiliarity) return "熟悉度高于 70 后可以表白";
            if (!CitySamePlace(personId)) return "需要当面表白";
            int days = CityPersonState(personId).ConfessionRetryDay - Clock.Today.TotalDays;
            return days > 0 ? "还要等 " + days + " 天" : null;
        }

        public string ConfessToPerson(string personId)
        {
            string blocker = ConfessionBlocker(personId);
            if (blocker != null) return blocker;
            if (!Random.Chance(ConfessionChance(Relation(personId))))
            {
                CityPersonState(personId).ConfessionRetryDay = Clock.Today.TotalDays + ConfessionCooldownDays;
                RememberSocial(personId, "表达了心意，对方希望先保持现在的相处方式。");
                return personId == LuZhi ? "我很珍惜我们一起做过的事。感情上我还没想好，我们还是照常见面吧。"
                    : "谢谢你愿意告诉我。我还没想好要不要开始一段恋爱，我们可以照常相处。";
            }
            ConfirmPartner(personId);
            return personId == LuZhi ? "我也想和你在一起。以后可以一起做东西，也可以只是见个面，什么都不赶。"
                : "我也喜欢和你在一起。那我们就试着认真交往吧。";
        }

        private void ConfirmPartner(string personId)
        {
            RomanceId = personId;
            var person = CityPersonState(personId);
            if (person != null) person.ConfessionRetryDay = 0;
            if (personId == LuZhi && CityStory("romance") is CityStoryProgress story && story.Stage >= 3)
                story.Branch = "romance";
            RememberSocial(personId, "你们确认了恋爱关系。");
        }

        public string PartnerConversation(string personId) =>
            IsPlayerPartner(personId) && PartnerTalk.TryGetValue(personId, out var line) ? line : null;

        public string ActivityPartnerId(CityActivity activity)
        {
            if (activity?.State != CityActivityPhase.Running || activity.Participants.Length != 1) return null;
            var participant = activity.Participants[0];
            return participant.Phase == CityParticipantPhase.Participating && IsPlayerPartner(participant.PersonId)
                ? participant.PersonId : null;
        }

        public string PartnerActivityLine(CityActivity activity)
        {
            string id = ActivityPartnerId(activity);
            if (id == null) return null;
            string kind = activity.DefinitionId ?? "";
            string line;
            if (kind.StartsWith("fishing", StringComparison.Ordinal) || kind.StartsWith("hike", StringComparison.Ordinal))
                line = "我们走慢一点吧。今天难得能在一起待这么久。";
            else if (kind.StartsWith("dinner", StringComparison.Ordinal) || kind.StartsWith("snack", StringComparison.Ordinal))
                line = "吃完也不急着走吧？今天我想和你多坐一会儿。";
            else if (kind.StartsWith("ktv", StringComparison.Ordinal))
                line = "下一首一起唱吧。唱错了也没关系，我跟着你。";
            else if (kind.StartsWith("court", StringComparison.Ordinal) || kind.StartsWith("golf", StringComparison.Ordinal))
                line = "歇一会儿吧，我把水放在你手边了。";
            else
                line = "今天想和你慢慢逛。看到喜欢的东西，就叫我一起看。";
            return (CityPerson(id)?.Name ?? "伴侣") + "：" + line;
        }

        public bool CanTalkToActivityPartner(CityActivity activity) =>
            !IsGameOver && ActivityPartnerId(activity) != null && CurrentPlace?.Id == activity.VenueId;

        public string InteractWithActivityPartner(string activityId, bool holdHands)
        {
            var activity = RunningCityActivity;
            if (activity?.Id != activityId || !CanTalkToActivityPartner(activity)) return null;
            string id = ActivityPartnerId(activity);
            string key = holdHands ? HoldHandsChoice : PersonalTalkChoice;
            string line = holdHands ? "好。就这样牵一会儿吧。" : PartnerConversation(id);
            if (!activity.Choices.Contains(key))
            {
                activity.Choices = activity.Choices.Concat(new[] { key }).ToArray();
                RememberSocial(id, holdHands ? "这次约会里，你们牵了手。" : "这次约会里，你们聊了彼此最近的心事。");
            }
            return line;
        }
    }
}
