using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const string BailoutUsedFlag = "bailout_used";

        private readonly Dictionary<string, double> _relations = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _giftDays = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _chatDays = new Dictionary<string, int>();

        /// <summary>每天能给每个人发一次消息，回了就涨一点交情；聊到这个数就不再涨了（再往上得当面送礼、约出去）。</summary>
        public const int ChatMaxRelation = 40;
        public const double ChatGain = 0.5;
        /// <summary>没装聊天 app，只能发短信：现在大家都不看短信了，常常没人回。</summary>
        public const double SmsReplyChance = 0.4;

        /// <summary>手机上装了聊天 app：发消息一定有人回；否则只能发短信。</summary>
        public bool HasChatApp => HasApp(PhoneApps.Chat);

        /// <summary>刚发的那条消息有没有人回。</summary>
        public bool LastChatReplied { get; private set; }

        public string ChatBlocker(string npcId)
        {
            if (Npcs.Get(npcId) == null) return "没这个人";
            if (!IsMet(npcId)) return "还没认识";
            if (_chatDays.TryGetValue(npcId, out int last) && last == Clock.Today.TotalDays) return HasChatApp ? "今天聊过了" : "今天发过了";
            return null;
        }

        /// <summary>发消息 / 短信：每天一次。聊天 app 一定有回复；短信只有 SmsReplyChance 的把握有人回。回了且交情不到 40 时 +0.5。</summary>
        public bool Chat(string npcId)
        {
            string blocker = ChatBlocker(npcId);
            if (blocker != null) return Fail(blocker);
            _chatDays[npcId] = Clock.Today.TotalDays;
            NpcDef n = Npcs.Get(npcId);
            LastChatReplied = HasChatApp || Random.Chance(SmsReplyChance);
            if (!LastChatReplied)
            {
                Say($"给{n.Name}发了条短信，一直没回。现在大家都不习惯看短信了。");
                return true;
            }
            if (Relation(npcId) < ChatMaxRelation)
            {
                AddRelation(npcId, ChatGain);
                Say($"和{n.Name}聊了几句，交情涨了一点。");
            }
            else Say($"和{n.Name}闲聊了几句。交情已经到头了，再往上得当面送礼、约出去。");
            return true;
        }

        /// <summary>店铺扩建成功（界面据此重建店内场景）。</summary>
        public event Action<ShopSize> ShopExpanded;

        public static string MetFlag(string npcId) => "met_" + npcId;

        public bool IsMet(string npcId) => Flags.Contains(MetFlag(npcId));

        public int Relation(string npcId) => _relations.TryGetValue(npcId, out double v) ? (int)Math.Floor(v) : 0;

        /// <summary>第一次认识。已认识则不变；初始交情不会拉低已有的。</summary>
        public void Meet(string npcId, double startRelation = 15)
        {
            Flags.Add(MetFlag(npcId));
            if (Relation(npcId) < startRelation) _relations[npcId] = startRelation;
            ApplyUpgrades();
        }

        public void AddRelation(string npcId, double delta)
        {
            double cur = _relations.TryGetValue(npcId, out double v) ? v : 0;
            _relations[npcId] = Math.Max(0, Math.Min(Npcs.MaxRelation, cur + delta));
            ApplyUpgrades();
        }

        public string GiftBlocker(string npcId)
        {
            NpcDef n = Npcs.Get(npcId);
            if (n == null) return "没这个人";
            if (!IsMet(npcId)) return "还没认识";
            if (_giftDays.TryGetValue(npcId, out int last) && Clock.Today.TotalDays - last < Npcs.GiftCooldownDays)
                return $"{Npcs.GiftCooldownDays - (Clock.Today.TotalDays - last)} 天后才能再送";
            if (n.GiftItemId != null)
            {
                if (Backpack.GiftCount(n.GiftItemId) <= 0) return "没带" + n.GiftName;
                return null;
            }
            if (Ledger.Balance < n.GiftPrice) return "钱不够";
            return null;
        }

        public bool GiveGift(string npcId)
        {
            string blocker = GiftBlocker(npcId);
            if (blocker != null) return Fail(blocker);
            NpcDef n = Npcs.Get(npcId);
            if (n.GiftItemId != null)
            {
                if (!Backpack.TakeGift(n.GiftItemId)) return Fail("没带" + n.GiftName + "。");
                ApplyCargoLimit();
            }
            else if (!Ledger.TryDebit(Clock.Today, "人情", n.GiftPrice, n.GiftName)) return Fail("钱不够。");
            _giftDays[npcId] = Clock.Today.TotalDays;
            AddRelation(npcId, Npcs.GiftGain + EffectiveCharm * 0.02);
            Say(n.GiftReply);
            return true;
        }

        /// <summary>每天交情自然淡一点（20 以下不再往下掉）。</summary>
        private void DecayRelations()
        {
            foreach (string id in _relations.Keys.ToList())
            {
                if (_relations[id] > 20) _relations[id] = Math.Max(20, _relations[id] - 0.1);
            }
            ApplyUpgrades();
        }

        /// <summary>资金连续为负的第 2 天，小林救一次急。</summary>
        private bool TryFriendBailout()
        {
            if (Flags.Contains(BailoutUsedFlag) || Relation(Npcs.Lin) < NpcPerks.LinBailout) return false;
            Flags.Add(BailoutUsedFlag);
            Ledger.Credit(Clock.Today, "人情", NpcPerks.BailoutAmount, "小林救急");
            Popup?.Invoke("小林来了", $"小林听说你的账上告急，二话没说转了 {NpcPerks.BailoutAmount} 过来：\"先拿着，别跟我客气，以后赚了再还。\"\n（这个人情只能用一次）");
            return true;
        }

        private double NpcTrafficMultiplier() => NpcPerks.TrafficBonus(Relation(Npcs.Zhang));

        private void SaveNpcs(SaveData d)
        {
            d.relationIds = _relations.Keys.ToArray();
            d.relationValues = d.relationIds.Select(k => _relations[k]).ToArray();
            d.giftIds = _giftDays.Keys.ToArray();
            d.giftDays = d.giftIds.Select(k => _giftDays[k]).ToArray();
            d.chatIds = _chatDays.Keys.ToArray();
            d.chatDays = d.chatIds.Select(k => _chatDays[k]).ToArray();
            SaveTalk(d);
        }

        private void RestoreNpcs(SaveData d)
        {
            _relations.Clear();
            int n = Math.Min(d.relationIds?.Length ?? 0, d.relationValues?.Length ?? 0);
            for (int i = 0; i < n; i++)
                if (Npcs.Get(d.relationIds[i]) != null) _relations[d.relationIds[i]] = d.relationValues[i];
            _giftDays.Clear();
            n = Math.Min(d.giftIds?.Length ?? 0, d.giftDays?.Length ?? 0);
            for (int i = 0; i < n; i++) _giftDays[d.giftIds[i]] = d.giftDays[i];
            _chatDays.Clear();
            n = Math.Min(d.chatIds?.Length ?? 0, d.chatDays?.Length ?? 0);
            for (int i = 0; i < n; i++) _chatDays[d.chatIds[i]] = d.chatDays[i];
            RestoreTalk(d);
        }
    }
}
