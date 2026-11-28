using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum RelationshipStoryStep { None, Invite, Appointment, Decide }

    [Serializable]
    public sealed class RelationshipStoryProgress
    {
        public string PersonId, AppointmentId;
        public int FirstTalkDay, OutingCompletedDay, DecisionDay, OutingAnswer;
        public bool InvitationAnswered, MissedAppointment, OutingCompleted, FollowUpRead;
        public RelationshipStoryOutcome Outcome;

        internal RelationshipStoryProgress Copy() => (RelationshipStoryProgress)MemberwiseClone();
    }

    public enum RelationshipStoryOutcome { Undecided, Friends, Together, Separated }

    public sealed partial class GameSession
    {
        private const string RelationshipOutingPrefix = "relationship-outing:";
        private const string RelationshipInvitationPrefix = "relationship-invitation:";
        private readonly Dictionary<string, RelationshipStoryProgress> _relationshipStories = new(StringComparer.Ordinal);
        private long _relationshipInvitationHour = -1;

        public static bool HasRelationshipStory(string personId) => RelationshipStoryCatalog.For(personId) != null;
        public static string RelationshipStoryTitle(string personId) => RelationshipStoryCatalog.For(personId)?.Title;
        public bool HasStartedRelationshipStory(string personId) => personId != null && _relationshipStories.ContainsKey(personId);

        private bool RelationshipStoryAvailable(RelationshipStory story) => story != null && !IsGameOver && HasShop
            && DistrictRoadOpen(story.District) && CityPersonState(story.PersonId) != null;

        private CityActivity RelationshipStoryActivity(RelationshipStoryProgress progress) => progress.AppointmentId == null
            ? null : _cityActivities.FirstOrDefault(activity => activity.Id == progress.AppointmentId);

        public RelationshipStoryStep GetRelationshipStoryStep(string personId, bool remote = false)
        {
            if (personId == null || !_relationshipStories.TryGetValue(personId, out var progress)
                || !RelationshipStoryAvailable(RelationshipStoryCatalog.For(personId)) || progress.Outcome != RelationshipStoryOutcome.Undecided)
                return RelationshipStoryStep.None;
            if (remote ? RemoteConversationBlocker(personId) != null : !CitySamePlace(personId)) return RelationshipStoryStep.None;
            if (progress.OutingCompleted) return !remote && Clock.Today.TotalDays > progress.OutingCompletedDay
                ? RelationshipStoryStep.Decide : RelationshipStoryStep.None;
            if (RelationshipStoryActivity(progress)?.Active == true) return RelationshipStoryStep.Appointment;
            return Clock.Today.TotalDays > progress.FirstTalkDay ? RelationshipStoryStep.Invite : RelationshipStoryStep.None;
        }

        public string RelationshipStoryAppointmentId(string personId) => personId != null
            && _relationshipStories.TryGetValue(personId, out var progress) && RelationshipStoryActivity(progress)?.Active == true
            ? progress.AppointmentId : null;

        private string TryRelationshipStoryChat(string personId, bool remote)
        {
            var story = RelationshipStoryCatalog.For(personId);
            if (!RelationshipStoryAvailable(story)) return null;
            if (!_relationshipStories.TryGetValue(personId, out var progress))
            {
                if (remote || !CitySamePlace(personId)) return null;
                progress = new RelationshipStoryProgress { PersonId = personId, FirstTalkDay = Clock.Today.TotalDays };
                if (personId == LuZhi && CityStory("romance")?.ActivityId is string legacyId)
                {
                    progress.AppointmentId = legacyId;
                    if (RelationshipStoryActivity(progress) is { State: CityActivityPhase.Completed, RewardApplied: true } previous)
                    {
                        progress.OutingCompleted = true;
                        progress.OutingCompletedDay = new GameTimestamp(previous.ActualEnd).Date.TotalDays;
                    }
                }
                _relationshipStories[personId] = progress;
                AddSocialRelation(personId, 1);
                RememberSocial(personId, story.Intro);
                return story.Intro;
            }
            if (progress.MissedAppointment)
            {
                progress.MissedAppointment = false;
                return "上次没碰上。我把那天留的东西收起来了。你哪天真有空，我们再约。";
            }
            if (progress.FollowUpRead || Clock.Today.TotalDays <= progress.DecisionDay) return null;
            string reply = progress.Outcome switch
            {
                RelationshipStoryOutcome.Friends => story.FriendFollowUp,
                RelationshipStoryOutcome.Together when IsPlayerPartner(personId) => story.PartnerFollowUp,
                _ => null,
            };
            if (reply == null) return null;
            progress.FollowUpRead = true;
            RememberSocial(personId, reply);
            return reply;
        }

        public (string text, long start) PrepareRelationshipStoryInvitation(string personId, bool remote)
        {
            if (GetRelationshipStoryStep(personId, remote) != RelationshipStoryStep.Invite)
                return ("这次约见还没准备好。", -1);
            return BuildRelationshipStoryInvitation(RelationshipStoryCatalog.For(personId));
        }

        private (string text, long start) BuildRelationshipStoryInvitation(RelationshipStory story)
        {
            var definition = CityLifeCatalog.Activity(story.ActivityId);
            if (definition == null || !City.HasPlace(definition.VenueId)) return ("暂时找不到合适的场所。", -1);
            long earliest = Clock.Now.TotalMinutes + 60;
            long start = FindCityActivityStartAt(definition.Id, story.PersonId, earliest, null, out _,
                candidate => candidate % 1440 >= 8 * 60 && candidate % 1440 + definition.Minutes <= 23 * 60);
            if (start < 0) return ("最近的班次和日程碰不上。等排班空下来再约吧。", -1);
            var venue = City.GetPlace(definition.VenueId);
            return ($"{story.Invitation}\n\n{new GameTimestamp(start)} · {venue.Name}\n预计费用 {VenuePriceRange(definition)}，到场开始时结算。", start);
        }

        public string AcceptRelationshipStoryInvitation(string personId, long start, bool remote)
        {
            if (GetRelationshipStoryStep(personId, remote) != RelationshipStoryStep.Invite)
                return "这次约见的情况已经变了。";
            var story = RelationshipStoryCatalog.For(personId);
            var activity = ScheduleCityActivity(story.ActivityId, new[] { personId }, start,
                sourceId: RelationshipOutingPrefix + personId + ":" + _nextCityActivity);
            if (activity == null) return "这个时段约不上了。有空再联系我，我们换个时间。";
            var progress = _relationshipStories[personId];
            activity.HostId = personId;
            progress.AppointmentId = activity.Id;
            progress.OutingAnswer = 0;
            progress.InvitationAnswered = true;
            progress.MissedAppointment = false;
            return $"约好了。{new GameTimestamp(start)} 在{City.GetPlace(activity.VenueId).Name}见。";
        }

        public void DeferRelationshipStoryInvitation(string personId)
        {
            if (personId != null && _relationshipStories.TryGetValue(personId, out var progress))
                progress.InvitationAnswered = true;
        }

        private bool TryOfferRelationshipStoryInvitation()
        {
            long hour = Clock.Now.TotalMinutes / 60;
            if (hour == _relationshipInvitationHour || Clock.Now.Hour < 9 || Clock.Now.Hour >= 21 || !HasSmartphone
                || !EventsEnabled || EventRaised == null || IncomingInvitations == InvitationMode.DoNotDisturb) return false;
            _relationshipInvitationHour = hour;

            foreach (var story in RelationshipStoryCatalog.Stories)
            {
                string personId = story.PersonId;
                if (!_relationshipStories.TryGetValue(personId, out var progress) || progress.InvitationAnswered
                    || GetRelationshipStoryStep(personId, remote: true) != RelationshipStoryStep.Invite) continue;
                var (text, start) = BuildRelationshipStoryInvitation(story);
                if (start < 0) continue;
                Raise(new EventDef
                {
                    Id = RelationshipInvitationPrefix + personId,
                    Title = CityPerson(personId).Name + "约你见面",
                    Body = text,
                    Cg = new EventCgFrame("event_phone_message", personId: personId),
                    Choices =
                    {
                        Choose("答应，加入日程", session => session.AcceptRelationshipStoryInvitation(personId, start, remote: true)),
                        Choose("改天再约", session => { session.DeferRelationshipStoryInvitation(personId); return null; })
                    }
                }, Clock.Today);
                return true;
            }
            return false;
        }

        private RelationshipStoryProgress RelationshipProgressFor(CityActivity activity)
        {
            if (activity == null || activity.Participants.Length != 1) return null;
            return _relationshipStories.TryGetValue(activity.Participants[0].PersonId, out var progress)
                && progress.AppointmentId == activity.Id ? progress : null;
        }

        public string RelationshipStoryOutingDialogue(CityActivity activity, string personId) =>
            CanReplyInRelationshipOuting(activity, personId) ? RelationshipStoryCatalog.For(personId).DuringOuting : null;

        public string[] RelationshipStoryOutingOptions(string personId) =>
            RelationshipStoryCatalog.ResponsesFor(personId).Select(response => response.Label).ToArray();

        private bool CanReplyInRelationshipOuting(CityActivity activity, string personId)
        {
            var progress = RelationshipProgressFor(activity);
            return progress != null && progress.PersonId == personId && progress.OutingAnswer == 0
                && MomentPerson(activity) == personId;
        }

        public string ReplyInRelationshipOuting(CityActivity activity, string personId, int choice)
        {
            if (!CanReplyInRelationshipOuting(activity, personId)) return null;
            var responses = RelationshipStoryCatalog.ResponsesFor(personId);
            if (choice < 0 || choice >= responses.Length) return null;
            _relationshipStories[personId].OutingAnswer = choice + 1;
            RememberRelationshipIllustration(personId);
            var response = responses[choice];
            RememberSocial(personId, response.Label + " · " + response.Reply);
            return response.Reply;
        }

        private void SettleRelationshipStoryOuting(CityActivity activity, CityActivityPhase phase)
        {
            var progress = RelationshipProgressFor(activity);
            if (progress == null || progress.OutingCompleted) return;
            if (phase == CityActivityPhase.NoShow) progress.MissedAppointment = true;
            if (phase != CityActivityPhase.Completed || !activity.RewardApplied) return;
            progress.OutingCompleted = true;
            progress.OutingCompletedDay = Clock.Today.TotalDays;
            progress.MissedAppointment = false;
            RememberRelationshipIllustration(progress.PersonId);
            RememberSocial(progress.PersonId, "一起赴约，完成了《" + RelationshipStoryTitle(progress.PersonId) + "》的相处。下次见面再聊。");
            if (progress.PersonId == LuZhi && CityStory("romance") is CityStoryProgress previous && previous.Stage < 4)
            {
                previous.Stage = 4;
                previous.Branch = IsPlayerPartner(LuZhi) ? "romance" : "story-complete";
            }
            Say("明天起再见到" + CityPerson(progress.PersonId).Name + "，可以聊聊这次见面的事。");
        }

        public string RelationshipStoryDecisionLine(string personId)
        {
            if (GetRelationshipStoryStep(personId) != RelationshipStoryStep.Decide) return null;
            string line = RelationshipStoryCatalog.For(personId).AfterOuting;
            int response = _relationshipStories[personId].OutingAnswer - 1;
            var responses = RelationshipStoryCatalog.ResponsesFor(personId);
            return response >= 0 && response < responses.Length ? responses[response].Recall + "\n\n" + line : line;
        }

        public bool RelationshipStoryOutingHasAnswer(string personId) =>
            personId != null && _relationshipStories.TryGetValue(personId, out var progress) && progress.OutingAnswer > 0;

        public string ResolveRelationshipStory(string personId, bool romantic)
        {
            if (GetRelationshipStoryStep(personId) != RelationshipStoryStep.Decide) return "等当面见到对方，再聊这件事。";
            if (romantic && RomanceId != null && !IsPlayerPartner(personId)) return "先处理好现有的伴侣关系。";
            if (!romantic && IsPlayerPartner(personId)) return "你们正在交往，可以继续聊聊彼此。";
            var progress = _relationshipStories[personId];
            var story = RelationshipStoryCatalog.For(personId);
            progress.Outcome = romantic ? RelationshipStoryOutcome.Together : RelationshipStoryOutcome.Friends;
            progress.DecisionDay = Clock.Today.TotalDays;
            if (romantic && !IsPlayerPartner(personId)) ConfirmPartner(personId);
            if (!romantic) RememberSocial(personId, "你们决定继续作为朋友相处。");
            RememberSocial(personId, romantic ? story.PartnerReply : story.FriendReply);
            return romantic ? story.PartnerReply : story.FriendReply;
        }

        private void SeparateRelationshipStory(string personId)
        {
            if (personId != null && _relationshipStories.TryGetValue(personId, out var progress)
                && progress.Outcome == RelationshipStoryOutcome.Together)
            {
                progress.Outcome = RelationshipStoryOutcome.Separated;
                progress.DecisionDay = Clock.Today.TotalDays;
            }
        }

        private void SaveRelationshipStories(SaveData data) =>
            data.relationshipStories = _relationshipStories.Values.Select(progress => progress.Copy()).ToArray();

        private void LoadRelationshipStories(SaveData data)
        {
            _relationshipStories.Clear();
            _relationshipInvitationHour = -1;
            foreach (var progress in data.relationshipStories ?? Array.Empty<RelationshipStoryProgress>())
                if (HasRelationshipStory(progress?.PersonId)) _relationshipStories[progress.PersonId] = progress.Copy();
        }
    }
}
