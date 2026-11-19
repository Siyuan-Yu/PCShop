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

        /// <summary>签下（搬到）一间新铺子（界面据此重建店内场景、改街上顾客去的店门）。</summary>
        public event Action<ShopSize> ShopExpanded;

        public static string MetFlag(string npcId) => "met_" + npcId;

        public bool IsMet(string npcId) => !string.IsNullOrEmpty(npcId) && Flags.Contains(MetFlag(npcId));

        private double RelationPoints(string npcId) => _relations.TryGetValue(npcId, out double points) ? points : 0;
        public int Relation(string npcId) => (int)Math.Floor(RelationPoints(npcId));

        /// <summary>第一次认识。已认识则不变；初始交情不会拉低已有的。</summary>
        public void Meet(string npcId, double startRelation = 15)
        {
            if (Flags.Add(MetFlag(npcId))) RememberSocial(npcId, "结识了对方，留下了联系方式。");
            if (Relation(npcId) < startRelation) _relations[npcId] = startRelation;
            ApplyUpgrades();
        }

        private static readonly (string Id, int Familiarity)[] StartingContacts =
        {
            (ClassmateId, 35), (Npcs.Lin, 35), (Npcs.Jie, 16), (Npcs.Landlady, 12), ("res_cook", 14), ("res_grandpa", 12)
        };

        private void InitializeStartingContacts()
        {
            foreach (var (id, familiarity) in StartingContacts)
                if (CityPerson(id) != null && !IsMet(id)) Meet(id, familiarity);
        }

        private void RestoreConversationContacts()
        {
            InitializeStartingContacts();
            const string greetedPrefix = "greeted:";
            foreach (string flag in Flags.Where(flag => flag.StartsWith(greetedPrefix, StringComparison.Ordinal)).ToArray())
            {
                string id = flag.Substring(greetedPrefix.Length);
                if (!IsMet(id) && (CityPerson(id) != null || Npcs.Get(id) != null || Rosters.Get(id) != null || Residents.Get(id) != null))
                    Meet(id, 0);
            }
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
            if(CityPersonState(npcId)!=null&&!CitySamePlace(npcId))return "需要在实际地点当面送礼";
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
            RememberSocial(npcId, $"送给对方{n.GiftName}。\n{n.GiftReply}");
            AddSocialRelation(npcId, Npcs.GiftGain + EffectiveCharm * 0.02);
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

        /// <summary>资金连续为负的第 2 天，林远航救一次急。</summary>
        private bool TryFriendBailout()
        {
            if (Flags.Contains(BailoutUsedFlag) || Relation(Npcs.Lin) < NpcPerks.LinBailout) return false;
            Flags.Add(BailoutUsedFlag);
            Ledger.Credit(Clock.Today, "人情", NpcPerks.BailoutAmount, "林远航救急");
            ShowEventCg("林远航来了", VisualResult(
                $"林远航听说你的账上告急，二话没说转了 {NpcPerks.BailoutAmount} 过来：\"先拿着，别跟我客气，以后赚了再还。\"\n（这个人情只能用一次）",
                "event_phone_message", personId: Npcs.Lin));
            return true;
        }

        private double NpcTrafficMultiplier() => NpcPerks.TrafficBonus(Relation(Npcs.Zhang));

        private void SaveNpcs(SaveData d)
        {
            d.relationIds = _relations.Keys.ToArray();
            d.relationValues = d.relationIds.Select(k => _relations[k]).ToArray();
            d.giftIds = _giftDays.Keys.ToArray();
            d.giftDays = d.giftIds.Select(k => _giftDays[k]).ToArray();
            SaveTalk(d);
        }

        private void RestoreNpcs(SaveData d)
        {
            _relations.Clear();
            int n = Math.Min(d.relationIds?.Length ?? 0, d.relationValues?.Length ?? 0);
            for (int i = 0; i < n; i++)
                if (!string.IsNullOrWhiteSpace(d.relationIds[i])) _relations[d.relationIds[i]] = Math.Max(0,Math.Min(Npcs.MaxRelation,d.relationValues[i]));
            _giftDays.Clear();
            n = Math.Min(d.giftIds?.Length ?? 0, d.giftDays?.Length ?? 0);
            for (int i = 0; i < n; i++) _giftDays[d.giftIds[i]] = d.giftDays[i];
            RestoreTalk(d);
        }
    }
}
