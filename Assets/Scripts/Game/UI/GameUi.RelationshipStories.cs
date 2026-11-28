using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildRelationshipOutingButton(Transform parent, CityActivity activity, Action refresh, string returnLabel = "继续活动")
        {
            string storyPerson = activity.Participants.FirstOrDefault()?.PersonId;
            string storyLine = S.RelationshipStoryOutingDialogue(activity, storyPerson);
            var moment = S.PersonalMoment(activity);
            bool story = storyLine != null;
            string personId = story ? storyPerson : moment.PersonId;
            if (personId == null) return;
            var person = S.CityPerson(personId);
            string label = story ? "聊聊今天" : moment.Title;
            Ui.Size(Ui.Button(parent, label, () => OpenDialogue(personId, person.Name, S.CityPersonRole(personId), "", null,
                onClosed: refresh, start: ctx =>
                {
                    if (story) ShowRelationshipOutingDialogue(ctx, activity, personId, storyLine, returnLabel);
                    else ShowPersonalMoment(ctx, activity, personId, moment.Line, moment.Illustrated, returnLabel);
                }), 17, Ui.ButtonHot), -1, 40);
        }

        private void ShowRelationshipOutingDialogue(TalkCtx ctx, CityActivity activity, string personId, string line, string returnLabel)
        {
            var session = S;
            ShowRelationshipScene(ctx, personId, line, returnLabel, true, session.RelationshipStoryOutingOptions(personId),
                index => session.ReplyInRelationshipOuting(activity, personId, index));
        }

        private void ShowPersonalMoment(TalkCtx ctx, CityActivity activity, string personId, string line, bool illustrated, string returnLabel)
        {
            var session = S;
            ShowRelationshipScene(ctx, personId, line, returnLabel, illustrated, session.PersonalMomentOptions(personId, illustrated),
                index => session.ReplyInPersonalMoment(activity, personId, index));
        }

        private static EventCgFrame RelationshipIllustration(string personId) => new EventCgFrame("Relationships/" + personId);

        private static void ShowRelationshipScene(TalkCtx ctx, string personId, string line, string returnLabel, bool illustrated,
            string[] options, Func<int, string> respond)
        {
            ctx.Illustration = illustrated ? RelationshipIllustration(personId) : null;
            ctx.ShowRelationshipReaction = false;
            var replies = options.Select((label, index) => new TalkOpt
            {
                Label = label,
                Act = () =>
                {
                    string answer = respond(index);
                    ctx.Illustration = null;
                    ctx.ShowRelationshipReaction = illustrated && answer != null;
                    ctx.AlternateRelationshipReaction = index == 1;
                    ctx.Finish(answer ?? "下次有机会再聊这件事。", returnLabel);
                }
            }).ToArray();
            ctx.Show(line, replies);
        }

        private void OpenRelationshipMemory(string personId, string title)
        {
            var picture = RelationshipIllustration(personId);
            OpenWindow(title, 968, 611, w =>
            {
                BuildEventCg(w.Content, picture, 510);
            }, true);
        }

        private void AddRelationshipStoryOption(List<TalkOpt> options, TalkCtx ctx, string personId, bool remote,
            Action<string> reply, Action<string> replyReaction)
        {
            var step = S.GetRelationshipStoryStep(personId, remote);
            if (step == RelationshipStoryStep.None) return;
            string label = step switch
            {
                RelationshipStoryStep.Invite => "聊聊见面的事",
                RelationshipStoryStep.Appointment => "查看见面日程",
                _ => "聊聊上次见面",
            };
            options.Add(new TalkOpt
            {
                Id = "RelationshipStory_" + step,
                Label = label,
                Hot = true,
                Invitation = step == RelationshipStoryStep.Invite,
                Act = () =>
                {
                    switch (step)
                    {
                        case RelationshipStoryStep.Invite:
                            var (text, start) = S.PrepareRelationshipStoryInvitation(personId, remote);
                            if (start < 0) { reply(text); break; }
                            ctx.Show(text,
                                new TalkOpt { Label = "约好时间", Hot = true, Act = () => reply(S.AcceptRelationshipStoryInvitation(personId, start, remote)) },
                                new TalkOpt { Label = "改天再约", Act = () => { S.DeferRelationshipStoryInvitation(personId); reply("好，等你有空再说。"); } });
                            break;
                        case RelationshipStoryStep.Appointment:
                            if (S.RelationshipStoryAppointmentId(personId) is string id) OpenCityActivityDetail(id);
                            break;
                        case RelationshipStoryStep.Decide:
                            var line = S.RelationshipStoryDecisionLine(personId);
                            if (line == null) break;
                            bool alreadyTogether = S.IsPlayerPartner(personId);
                            if (!S.RelationshipStoryOutingHasAnswer(personId)) ctx.Illustration = RelationshipIllustration(personId);
                            ctx.Show(line,
                                new TalkOpt { Label = alreadyTogether ? "我也想和你一起" : "我想和你交往", Hot = true,
                                    Blocker = S.RomanceId != null && !alreadyTogether ? "已有伴侣" : null,
                                    Act = () =>
                                    {
                                        string answer = S.ResolveRelationshipStory(personId, romantic: true);
                                        if (S.IsPlayerPartner(personId)) replyReaction(answer);
                                        else reply(answer);
                                    } },
                                new TalkOpt { Label = "继续做朋友", Blocker = alreadyTogether ? "你们正在交往" : null,
                                    Act = () => reply(S.ResolveRelationshipStory(personId, romantic: false)) },
                                new TalkOpt { Label = "让我再想想", Act = () => reply("好，等你想聊的时候再说。") });
                            break;
                    }
                }
            });
        }
    }
}
