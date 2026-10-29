using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>点人物后的交谈：随便闲聊、聊聊人生、交心打听内幕（随交情解锁）。送礼 / 邀约 / 打听行情 / 砍价见别处。</summary>
    public sealed partial class GameSession
    {
        private readonly Dictionary<string, int> _talkDays = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _lifeDays = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _lifeCount = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _insiderDays = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _staffDays = new Dictionary<string, int>();

        public const double StaffBossGain = 1.0;

        // ---------- 随便闲聊 ----------

        public string SmallTalkBlocker(string npcId)
        {
            if (Npcs.Get(npcId) == null) return "没这个人";
            if (!IsMet(npcId)) return "还没认识";
            if (_talkDays.TryGetValue(npcId, out int last) && last == Clock.Today.TotalDays) return "今天聊过了";
            return null;
        }

        /// <summary>当面闲聊几句：每天一次，花 10 分钟，交情不到 50 时 +0.8。返回他说的话；失败返回 null。</summary>
        public string SmallTalk(string npcId)
        {
            string blocker = SmallTalkBlocker(npcId);
            if (blocker != null) { Fail(blocker); return null; }
            NpcDef n = Npcs.Get(npcId);
            _talkDays[npcId] = Clock.Today.TotalDays;
            string line = NpcTalk.SmallTalkLine(npcId, Clock.Today.TotalDays);
            if (Relation(npcId) < NpcTalk.SmallTalkMaxRelation) AddRelation(npcId, NpcTalk.SmallTalkGain);
            else line += $"\n（和{n.Name}聊得很开心，不过交情到这儿，光闲聊不涨了。送礼、约出去、听他讲讲过去会更有用。）";
            AdvanceClock(NpcTalk.SmallTalkMinutes);
            return line;
        }

        // ---------- 聊聊人生 ----------

        public int LifeChaptersHeard(string npcId) => _lifeCount.TryGetValue(npcId, out int c) ? c : 0;

        /// <summary>聊人生现在不能的原因（交情不够 / 今天聊过 / 故事听完了）；能聊返回 null。</summary>
        public string LifeBlocker(string npcId)
        {
            if (Npcs.Get(npcId) == null) return "没这个人";
            if (!IsMet(npcId)) return "还没认识";
            int heard = LifeChaptersHeard(npcId);
            if (heard >= NpcTalk.LifeChapterTotal) return "他的故事你都听过了";
            int need = NpcTalk.LifeUnlock[heard];
            if (Relation(npcId) < need) return heard == 0 ? $"交情 {need} 解锁" : $"交情 {need} 再听下一段";
            if (_lifeDays.TryGetValue(npcId, out int last) && last == Clock.Today.TotalDays) return "今天聊过了";
            return null;
        }

        /// <summary>坐下来听他讲一段过去：花 40 分钟，交情 +2.5。返回故事；失败返回 null。</summary>
        public string LifeTalk(string npcId)
        {
            string blocker = LifeBlocker(npcId);
            if (blocker != null) { Fail(blocker); return null; }
            int heard = LifeChaptersHeard(npcId);
            string story = NpcTalk.LifeStory(npcId, heard) ?? "他讲了很久，你听得很认真。";
            _lifeCount[npcId] = heard + 1;
            _lifeDays[npcId] = Clock.Today.TotalDays;
            AddRelation(npcId, NpcTalk.LifeGain);
            AdvanceClock(NpcTalk.LifeMinutes);
            return story;
        }

        // ---------- 交心打听内幕 ----------

        public string InsiderBlocker(string npcId)
        {
            NpcDef n = Npcs.Get(npcId);
            if (n == null || !n.IsTipster) return "他不爱聊这个";
            if (!IsMet(npcId)) return "还没认识";
            if (Relation(npcId) < NpcTalk.InsiderRelation) return $"交情 {NpcTalk.InsiderRelation} 解锁";
            if (_insiderDays.TryGetValue(npcId, out int last) && Clock.Today.TotalDays - last < NpcTalk.InsiderCooldownDays)
                return $"刚说过，{NpcTalk.InsiderCooldownDays - (Clock.Today.TotalDays - last)} 天后再问";
            return null;
        }

        /// <summary>交情够了，他把压箱底的消息告诉你：一定是真的（行情真会按他说的走），不用递烟，一周一次。</summary>
        public string AskInsider(string npcId)
        {
            string blocker = InsiderBlocker(npcId);
            if (blocker != null) { Fail(blocker); return null; }
            NpcDef n = Npcs.Get(npcId);
            _insiderDays[npcId] = Clock.Today.TotalDays;
            string text = CreateTip(n, true);
            AdvanceClock(NpcTalk.InsiderMinutes);
            return text.Replace("压低声音", "凑近你，认真地说");
        }

        // ---------- 店员与老板 ----------

        /// <summary>老板肯不肯见你：认识他、且交情够了。没有交情系统的小店老板一直在柜台，直接可见。</summary>
        public bool BossReachable(PlaceRoster r)
        {
            if (r == null || r.BossNpcId == null) return false;
            return IsMet(r.BossNpcId) && Relation(r.BossNpcId) >= r.BossGate;
        }

        /// <summary>找店员打听老板的事现在不能的原因；能就返回 null。</summary>
        public string StaffBossBlocker(PlaceRoster r)
        {
            if (r == null || r.BossNpcId == null) return "这里没有别的老板";
            if (!IsMet(r.BossNpcId)) return "还没听说过老板";
            if (BossReachable(r)) return "老板肯见你了，直接找他";
            if (_staffDays.TryGetValue(r.BossNpcId, out int last) && last == Clock.Today.TotalDays) return "今天问过了";
            return null;
        }

        /// <summary>找店员聊老板：店员透露老板的喜好，顺便替你说了几句好话，老板的交情 +1（每天一次，直到老板肯见你）。</summary>
        public string AskStaffAboutBoss(PlaceRoster r)
        {
            string blocker = StaffBossBlocker(r);
            if (blocker != null) { Fail(blocker); return null; }
            NpcDef boss = r.Boss;
            _staffDays[boss.Id] = Clock.Today.TotalDays;
            AddRelation(boss.Id, StaffBossGain);
            ActivityDef like = boss.Likes == null ? null : Activities.Get(boss.Likes);
            string hint = like != null ? $"他爱{like.Name}，" : "";
            string text = $"{r.Greeter.Name}压低声音：“{boss.Name}啊？{hint}也爱收{boss.GiftName}。我帮你在他跟前提过你了。”";
            if (BossReachable(r)) text += $"\n（{boss.Name}现在肯见你了。）";
            return text;
        }

        // ---------- 存档 ----------

        private void SaveTalk(SaveData d)
        {
            d.talkIds = _talkDays.Keys.ToArray();
            d.talkDays = d.talkIds.Select(k => _talkDays[k]).ToArray();
            d.lifeIds = _lifeCount.Keys.ToArray();
            d.lifeCounts = d.lifeIds.Select(k => _lifeCount[k]).ToArray();
            d.lifeDayIds = _lifeDays.Keys.ToArray();
            d.lifeDays = d.lifeDayIds.Select(k => _lifeDays[k]).ToArray();
            d.insiderIds = _insiderDays.Keys.ToArray();
            d.insiderDays = d.insiderIds.Select(k => _insiderDays[k]).ToArray();
            d.staffIds = _staffDays.Keys.ToArray();
            d.staffDays = d.staffIds.Select(k => _staffDays[k]).ToArray();
        }

        private void RestoreTalk(SaveData d)
        {
            ReadDict(_talkDays, d.talkIds, d.talkDays);
            ReadDict(_lifeCount, d.lifeIds, d.lifeCounts);
            ReadDict(_lifeDays, d.lifeDayIds, d.lifeDays);
            ReadDict(_insiderDays, d.insiderIds, d.insiderDays);
            ReadDict(_staffDays, d.staffIds, d.staffDays);
        }
    }
}
