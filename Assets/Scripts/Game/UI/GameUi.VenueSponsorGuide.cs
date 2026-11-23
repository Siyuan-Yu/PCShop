using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const string SponsorGuidePortraitControl = "VenueSponsorGuidePortrait";
        private const string SponsorGuideChatControl = "VenueSponsorGuideChat";
        private Window _venueSponsorGuideWindow;
        private Window _venueSponsorGuideDialogue;
        private VenueSponsorView _venueSponsorGuideView;

        private (Button target, string instruction) VenueSponsorGuideTarget(Window top)
        {
            if (top == null)
            {
                OpenCityVenueMenu(S.CurrentPlace);
                if (!AnyModalOpen) return (null, null);
                top = _windows[_windows.Count - 1];
                Canvas.ForceUpdateCanvases();
            }

            var step = S.VenueSponsorGuideStep;
            if (top == _venueSponsorGuideDialogue)
                return step == SponsorGuideStep.Talk
                    ? (SponsorGuideControl(top, SponsorGuideChatControl), "点「聊一聊」，把熟悉度提到 30")
                    : (SponsorGuideControl(top, "DialogueEnd"), "和杜建川聊完了，点「结束对话」");

            if (top != _venueSponsorGuideWindow) return (null, null);
            return step switch
            {
                SponsorGuideStep.Talk => (SponsorGuideControl(top, SponsorGuidePortraitControl), "点杜建川的头像，跟他打声招呼"),
                SponsorGuideStep.Activities => (_venueSponsorGuideView.Events, "点「近期活动」，看看接下来有什么活动"),
                SponsorGuideStep.Sponsor => (_venueSponsorGuideView.Sponsor, "点「赞助活动」查看方案，是否出资由你决定"),
                _ => (null, null)
            };
        }

        private static Button SponsorGuideControl(Window window, string name) =>
            window.Content.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name);
    }
}
