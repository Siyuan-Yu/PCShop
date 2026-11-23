using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum SponsorGuideStep { Inactive, Talk, Activities, Sponsor }

    public sealed partial class GameSession
    {
        private const string VenueSponsorGuideId = "venue_sponsorship";
        private const string SponsorGuideIssued = "tutorial:issued:" + VenueSponsorGuideId;
        private const string SponsorGuideTalked = "tutorial:venue-sponsorship:talked";
        private const string SponsorGuideViewedActivities = "tutorial:venue-sponsorship:activities";
        private const string BankLessonId = "bank_securities";
        public const string VenueSponsorGuidePlaceId = "life_restaurant";
        public const string VenueSponsorGuidePersonId = "life_chef";
        private int _venueSponsorGuideEarliestDay;

        public SponsorGuideStep VenueSponsorGuideStep => !Flags.Contains(SponsorGuideIssued) || QuestCompleted(VenueSponsorGuideId)
            ? SponsorGuideStep.Inactive : Flags.Contains(SponsorGuideViewedActivities) ? SponsorGuideStep.Sponsor
            : Flags.Contains(SponsorGuideTalked) ? SponsorGuideStep.Activities : SponsorGuideStep.Talk;

        private Place VenueSponsorGuidePlace => City.HasPlace(VenueSponsorGuidePlaceId)
            ? City.GetPlace(VenueSponsorGuidePlaceId) : null;
        private bool VenueSponsorGuideContactOnDuty => IsOnDutyVenueNpc(VenueSponsorGuidePlace, CityPersonState(VenueSponsorGuidePersonId));
        public bool VenueSponsorGuidePaused => VenueSponsorGuideStep != SponsorGuideStep.Inactive && CurrentPlace != null
            && CurrentPlace.Id == VenueSponsorGuidePlaceId && CanPauseForTutorial && HasVenueSponsorship(CurrentPlace)
            && MyShops.Any() && VenueSponsorGuideContactOnDuty && !AutomaticMealAt(CurrentPlace);

        private void RefreshVenueSponsorGuide()
        {
            if (IsGameOver || _venueSponsorGuideEarliestDay <= 0 || Clock.Today.TotalDays < _venueSponsorGuideEarliestDay
                || Flags.Contains(SponsorGuideIssued)) return;
            var venue = VenueSponsorGuidePlace;
            var profile = VenueCommunityAt(venue);
            if (profile == null || !Ledger.CanAfford(Money.FromYuan(2L * profile.MinimumFee)) || !MyShops.Any()
                || !HasVenueSponsorship(venue) || DistrictAccessBlocker(venue) != null || CityPerson(VenueSponsorGuidePersonId) == null)
                return;
            Flags.Add(SponsorGuideIssued);
            Meet(VenueSponsorGuidePersonId, VenueSponsorFamiliarity - 1);
            Say("新任务「认识街坊活动」：去街坊餐厅找杜建川聊聊。");
        }

        public QuestDef VenueSponsorGuideQuest()
        {
            var step = VenueSponsorGuideStep;
            var venue = VenueSponsorGuidePlace;
            if (step == SponsorGuideStep.Inactive || IsGameOver || !HasVenueSponsorship(venue) || !MyShops.Any()) return null;
            return new QuestDef
            {
                Id = VenueSponsorGuideId,
                Title = "认识街坊活动",
                Target = QuestTarget.VenueSponsorship,
                GuideDestination = venue,
                Short = step switch
                {
                    SponsorGuideStep.Activities => "打开「近期活动」，看看接下来有什么活动。",
                    SponsorGuideStep.Sponsor => "打开「赞助活动」，查看赞助方案。",
                    _ when VenueSponsorGuideContactOnDuty => "去街坊餐厅找杜建川聊一聊。",
                    _ => "杜建川还没到岗，等他值班时再去聊。"
                },
                Hint = "到街坊餐厅找杜建川聊天，再查看近期活动和赞助方案。是否赞助由你决定。",
                Progress = session => step == SponsorGuideStep.Talk
                    ? $"熟悉度 {session.Relation(VenueSponsorGuidePersonId)} / {VenueSponsorFamiliarity} · 通常 09:00–18:00 值班"
                    : step == SponsorGuideStep.Activities ? "已交谈 · 待查看近期活动" : "已查看近期活动 · 待打开赞助方案",
                Done = session => session.QuestCompleted(VenueSponsorGuideId),
            };
        }

        private string TryFinishVenueSponsorGuideChat(string personId, bool remote)
        {
            if (remote || personId != VenueSponsorGuidePersonId || VenueSponsorGuideStep != SponsorGuideStep.Talk
                || !VenueSponsorGuidePaused) return null;
            double missing = VenueSponsorFamiliarity - RelationPoints(personId);
            if (missing > 0) AddSocialRelation(personId, missing);
            Flags.Add(SponsorGuideTalked);
            return "我们最近在筹备街坊试菜小桌。你可以先看看店里的「近期活动」，感兴趣的话也能让自己的店来赞助。";
        }

        public void ViewVenueSponsorGuideActivities(Place place)
        {
            if (place == CurrentPlace && VenueSponsorGuideStep == SponsorGuideStep.Activities && VenueSponsorGuidePaused)
                Flags.Add(SponsorGuideViewedActivities);
        }

        public void FinishVenueSponsorGuide(Place place)
        {
            if (place == CurrentPlace && VenueSponsorGuideStep == SponsorGuideStep.Sponsor && VenueSponsorGuidePaused
                && Flags.Add("tutorial:" + VenueSponsorGuideId))
                Say("已完成「认识街坊活动」。以后可以到其他场所查看活动，和熟悉的工作人员谈赞助。");
        }
    }
}
