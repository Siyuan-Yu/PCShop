using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    /// <summary>店员（没有完整交情系统的人）的熟络度：闲聊、送小礼物、听他讲自己的事；熟了替你在老板面前说话更有分量。</summary>
    public static class StaffTalk
    {
        public const double ChatGain = 1.5;
        public const int ChatMaxRapport = 40;
        public const int ChatMinutes = 8;
        public const long GiftCostYuan = 30;
        public const double GiftGain = 4;
        public const int GiftCooldownDays = 3;
        public const double StoryGain = 3;
        public const int StoryMinutes = 30;
        public const int MaxRapport = 100;

        /// <summary>听他讲第 1、2、3 段自己的事各需要的熟络度。</summary>
        public static readonly int[] StoryUnlock = { 15, 35, 60 };

        public static string LevelName(int rapport)
        {
            if (rapport < 10) return "陌生";
            if (rapport < 30) return "脸熟";
            if (rapport < 55) return "熟人";
            if (rapport < 80) return "朋友";
            return "知心";
        }

        /// <summary>熟络度给“打听老板的事”加的老板交情：0 ~ +1.6。</summary>
        public static double BossBonus(int rapport) => Math.Min(1.6, rapport / 60.0);
    }

    public sealed partial class GameSession
    {
        private readonly Dictionary<string, double> _staffRapport = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _staffChatDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _staffGiftDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _staffStoryDay = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _staffStoryCount = new Dictionary<string, int>();

        public int StaffRapport(string staffId) => _staffRapport.TryGetValue(staffId, out double v) ? (int)Math.Floor(v) : 0;

        private void AddStaffRapport(string staffId, double delta)
        {
            double cur = _staffRapport.TryGetValue(staffId, out double v) ? v : 0;
            _staffRapport[staffId] = Math.Max(0, Math.Min(StaffTalk.MaxRapport, cur + delta));
        }

        // ---------- 闲聊 ----------

        public string StaffChatBlocker(string staffId)
        {
            if (_staffChatDay.TryGetValue(staffId, out int last) && last == Clock.Today.TotalDays) return "今天聊过了";
            return null;
        }

        /// <summary>和店员闲聊几句：每天一次，花 8 分钟，熟络度 +1.5（到 40 为止，再往上要送礼、听他讲故事）。</summary>
        public string StaffChat(StaffDef staff, int chats = 0)
        {
            string blocker = StaffChatBlocker(staff.Id);
            if (blocker != null) { Fail(blocker); return null; }
            _staffChatDay[staff.Id] = Clock.Today.TotalDays;
            string line = staff.Name + "：“" + staff.LineFor(Clock.Today.TotalDays + chats) + "”";
            if (StaffRapport(staff.Id) < StaffTalk.ChatMaxRapport) AddStaffRapport(staff.Id, StaffTalk.ChatGain);
            else line += $"\n（和{staff.Name}聊得很投缘，不过光闲聊熟络不动了。送点小礼物、听他讲讲自己的事更有用。）";
            AdvanceClock(StaffTalk.ChatMinutes);
            return line;
        }

        // ---------- 送小礼物 ----------

        public string StaffGiftBlocker(string staffId)
        {
            if (_staffGiftDay.TryGetValue(staffId, out int last) && Clock.Today.TotalDays - last < StaffTalk.GiftCooldownDays)
                return $"{StaffTalk.GiftCooldownDays - (Clock.Today.TotalDays - last)} 天后才能再送";
            if (!Backpack.GiftLines.Any()) return "背包里没有能送的东西（去烟酒茶行买瓶饮料）";
            return null;
        }

        /// <summary>送一份礼品熟络度涨多少：一瓶饮料（¥5）约 +2，¥30 的东西 +4，越贵越多，最多 +10。</summary>
        public static double StaffGiftGain(GiftDef g) =>
            StaffTalk.GiftGain * Math.Max(0.5, Math.Min(2.5, g.Price.Yuan / (double)StaffTalk.GiftCostYuan));

        /// <summary>从背包里拿一份礼品送给店员，三天一次。</summary>
        public bool GiveStaffGift(StaffDef staff, string giftId)
        {
            string blocker = StaffGiftBlocker(staff.Id);
            if (blocker != null) return Fail(blocker);
            GiftDef g = Gifts.Get(giftId);
            if (g == null || !Backpack.TakeGift(giftId)) return Fail("背包里没有这个。");
            ApplyCargoLimit();
            _staffGiftDay[staff.Id] = Clock.Today.TotalDays;
            AddStaffRapport(staff.Id, StaffGiftGain(g) + EffectiveCharm * 0.02);
            Say(g.Id == Gifts.Soda || g.Id == Gifts.Drink
                ? $"{staff.Name}接过饮料笑了：“哎呀，太客气了。”"
                : $"{staff.Name}接过「{g.Name}」愣了一下：“这么破费干嘛……谢了啊。”");
            return true;
        }

        // ---------- 听他讲自己的事 ----------

        public int StaffStoriesHeard(string staffId) => _staffStoryCount.TryGetValue(staffId, out int c) ? c : 0;

        public string StaffStoryBlocker(string staffId)
        {
            int heard = StaffStoriesHeard(staffId);
            string[] stories = Rosters.StoriesOf(staffId);
            if (stories == null || stories.Length == 0) return "他不太爱讲自己";
            if (heard >= stories.Length) return "他的事你都听过了";
            int need = StaffTalk.StoryUnlock[Math.Min(heard, StaffTalk.StoryUnlock.Length - 1)];
            if (StaffRapport(staffId) < need) return heard == 0 ? $"熟络 {need} 解锁" : $"熟络 {need} 再听下一段";
            if (_staffStoryDay.TryGetValue(staffId, out int last) && last == Clock.Today.TotalDays) return "今天聊过了";
            return null;
        }

        /// <summary>坐下来听他讲一段自己的事：花 30 分钟，熟络度 +3。</summary>
        public string StaffStory(StaffDef staff)
        {
            string blocker = StaffStoryBlocker(staff.Id);
            if (blocker != null) { Fail(blocker); return null; }
            int heard = StaffStoriesHeard(staff.Id);
            string story = Rosters.StoriesOf(staff.Id)[heard];
            _staffStoryCount[staff.Id] = heard + 1;
            _staffStoryDay[staff.Id] = Clock.Today.TotalDays;
            AddStaffRapport(staff.Id, StaffTalk.StoryGain);
            AdvanceClock(StaffTalk.StoryMinutes);
            return story;
        }

        // ---------- 存档 ----------

        private void SaveStaffRapport(SaveData d)
        {
            d.rapIds = _staffRapport.Keys.ToArray();
            d.rapValues = d.rapIds.Select(k => _staffRapport[k]).ToArray();
            d.rapChatIds = _staffChatDay.Keys.ToArray();
            d.rapChatDays = d.rapChatIds.Select(k => _staffChatDay[k]).ToArray();
            d.rapGiftIds = _staffGiftDay.Keys.ToArray();
            d.rapGiftDays = d.rapGiftIds.Select(k => _staffGiftDay[k]).ToArray();
            d.rapStoryIds = _staffStoryCount.Keys.ToArray();
            d.rapStoryCounts = d.rapStoryIds.Select(k => _staffStoryCount[k]).ToArray();
            d.rapStoryDayIds = _staffStoryDay.Keys.ToArray();
            d.rapStoryDays = d.rapStoryDayIds.Select(k => _staffStoryDay[k]).ToArray();
        }

        private void RestoreStaffRapport(SaveData d)
        {
            _staffRapport.Clear();
            int n = Math.Min(d.rapIds?.Length ?? 0, d.rapValues?.Length ?? 0);
            for (int i = 0; i < n; i++) _staffRapport[d.rapIds[i]] = d.rapValues[i];
            ReadDict(_staffChatDay, d.rapChatIds, d.rapChatDays);
            ReadDict(_staffGiftDay, d.rapGiftIds, d.rapGiftDays);
            ReadDict(_staffStoryCount, d.rapStoryIds, d.rapStoryCounts);
            ReadDict(_staffStoryDay, d.rapStoryDayIds, d.rapStoryDays);
        }
    }
}
