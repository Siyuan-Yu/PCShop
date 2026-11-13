using System;
using System.Linq;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        internal bool CanOfferFishingInvitation(int number) => City.HasPlace(FishingVenueId)
            && DistrictAccessBlocker(City.GetPlace(FishingVenueId)) == null
            && CityPersonState(number == 59 ? "resident_shaoan" : "resident_qiaosong") != null
            && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_" + number);
        private EventDef BindFishingInvitation(EventDef template, int number)
        {
            if (!CanOfferFishingInvitation(number)) return null;
            string person = number == 59 ? "resident_shaoan" : "resident_qiaosong";
            string definition = "social_" + number;
            long start = FindSocialBatchStart(definition, FishingVenueId, new[] { person }, false);
            int perPerson = number == 59 ? 800 : 1800;
            string body = number == 59 ? "邵安约周末到青湾垂钓场，实际选择钓位、鱼饵和提竿方式。钓具体验 120 分钟，双人共 ¥1,600，默认各付 ¥800。鱼获可以留作食材、出售、放生或送给同行者。"
                : "乔松约你到青湾垂钓场，湖畔钓叙 180 分钟，双人共 ¥3,600，默认各付 ¥1,800。可以选择安静陪钓、请教园艺、听往事或到远处钓位，效果各不相同。";
            body += start < 0 ? "\n暂时约不上，可以改天。" : "\n时间：" + new GameTimestamp(start);
            string Book(GameSession s, bool treat)
            {
                var activity = s.ScheduleCityActivity(definition, new[] { person }, start, sourceId: "fishing-invitation:" + number + ":" + s._nextCityActivity);
                if (activity == null) return "本次未约成，没有扣款。";
                activity.HostId = person; activity.PlayerTreat = treat; activity.ServiceConfirmed = true; activity.ServiceChoice = 0;
                return "已记入日程，实际到场开始后支付。到场才认识，完整完成后结算相处奖励。";
            }
            return EventScene(template, body,
                Choose($"确认赴约 · 我付 ¥{perPerson:N0}", s => Book(s, false), _ => start >= 0, "没有可赴约时段"),
                Choose($"这次我请 · ¥{perPerson * 2:N0}", s => Book(s, true), _ => start >= 0, "没有可赴约时段"),
                Choose("改天再约", _ => "本次不预约，不扣钱或交情。"), Choose("这次不参加", _ => "你婉拒了，之后仍可再约。"));
        }
        private string FishingInvitationChoiceReply(CityActivity activity, int choice)
        {
            if (activity.DefinitionId == "social_59")
            {
                if (choice == 1) return "邵安提醒：浅滩用面饵、芦苇边用蚯蚓、深水区用小鱼饵。搭配合适，等鱼更快、遛鱼绿区更宽；远投更容易遇到鲈鱼。";
                if (choice == 2) { activity.Choices = activity.Choices.Union(new[] { "fishing-advice" }).ToArray(); return "邵安讲解了看漂和放线技巧，本场遛鱼绿区额外加宽 2 个百分点，仍需自己操作。"; }
            }
            if (activity.DefinitionId != "social_60" || choice == 0 || !FishingActive) return null;
            if (_fishing.Style != 0) return "本场已选定相处方式，不能反复切换领取不同奖励。";
            _fishing.Style = choice;
            if (choice == 1) return "你请教园艺。完成后偏重见识，而不是额外恢复大量精力。";
            if (choice == 2) return "乔松讲起退休前后的生活。完整完成后把经历记入人物资料。";
            if (SelectFishingSpot(2)) { Player.SpendEnergy(8); return "你们换到深水钓位，精力 −8；用小鱼饵、蓄力远投可以多找鲈鱼，仍需实际控线遛鱼。"; }
            _fishing.Style = 0; return "请先处理当前鱼口或鱼获，再选择远处钓位。";
        }
        private void CompleteFishingInvitation(CityActivity activity)
        {
            if (activity.DefinitionId != "social_60" || _fishing?.ActivityId != activity.Id) return;
            if (_fishing.Style == 0) { Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy + 12); Player.Mood = Math.Min(100, Player.Mood + 8); }
            else if (_fishing.Style == 1) Player.Intelligence = Math.Min(100, Player.Intelligence + 2);
            else if (_fishing.Style == 2) { Flags.Add("fishing:qiaosong-story"); Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy + 6); }
        }
    }
}
