using System;
using System.Linq;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public sealed class KolTalentDef
    {
        public readonly string Id,Topic,Kind;
        public readonly int Charm,Intelligence,Audience;
        public int PublicStartMinute { get; internal set; }
        public KolTalentDef(string id,string topic,string kind,int charm,int intelligence,int audience)
        {Id=id;Topic=topic;Kind=kind;Charm=charm;Intelligence=intelligence;Audience=audience;}
    }
    public static class KolMarket
    {
        public static readonly KolTalentDef[] All={
            new KolTalentDef("momo","实用装机讲解","computer",78,94,4000),
            new KolTalentDef("ake","电竞设备体验","computer",93,73,18000),
            new KolTalentDef("qiao","数码测评","computer",95,91,60000),
            new KolTalentDef("life_creator","原创模型与手作","models",86,90,8500),
            new KolTalentDef("life_designer","原创穿搭","clothing",96,87,16000),
            new KolTalentDef("life_clothier","日常搭配","clothing",82,76,3200),
            new KolTalentDef("life_singer","舞台与生活分享","clothing",94,64,11000),
            new KolTalentDef("resident_ningxi","模型摄影","models",91,92,7200),
            new KolTalentDef("resident_dengyue","插画与收藏","models",85,89,1800),
            new KolTalentDef("resident_rongke","生活陈设","models",87,88,5600),
            new KolTalentDef("resident_wuyou","收藏与装裱","models",75,95,3800),
            new KolTalentDef("resident_lanxin","陶艺手作","models",84,91,6400),
            new KolTalentDef("resident_yujia","玩具开箱","models",92,72,9200),
            new KolTalentDef("resident_zengyu","创意生活分享","clothing",90,82,4300),
            new KolTalentDef("resident_fanglu","阅读与生活美学","clothing",88,93,2700)
        };
        private static readonly Dictionary<string, KolTalentDef> ById = All.ToDictionary(t => t.Id);
        public static KolTalentDef Get(string id) => id != null && ById.TryGetValue(id, out var talent) ? talent : null;
        static KolMarket()
        {
            for (int i = 0; i < All.Length; i++) All[i].PublicStartMinute = 19 * 60 + i % 6 * 45;
        }
    }
    [Serializable] public sealed class KolCareerState
    {
        public string PersonId;public int Followers,Popularity;public long TipsReceivedCents,LiveMinutes;
        public KolCareerState Copy()=>(KolCareerState)MemberwiseClone();
    }
    [Serializable] public sealed class KolLiveContract
    {
        public string Id,BrandId,PersonId,AgencyId;public long SignedAt,EndAt,LastProcessedAt,ScheduleEffectiveAt;
        public int StartMinute=1080,EndMinute=120,ScheduleVersion;public bool Enabled=true,Ended;public long FeeCents,SessionFeeCents;
        public int FrequencyDays = 1, FirstDay;
        public long LastPaidThrough,WagesPaidCents;
        public KolLiveContract Copy()=>(KolLiveContract)MemberwiseClone();
    }
    [Serializable] public sealed class KolLiveSession
    {
        public string Id,ContractId;public long StartedAt,EndsAt,LiveMinutes,ViewerMinutes,TipsCents,LastTipAt=-999999;
        public int PeakViewers,TipCount,BrandGiftWins,TipBoost,LastGiftYuan;
        public string PersonId, BrandId, RivalKind, SponsorName, ShowcaseKind;
        public bool Settled;
        public long LegacyMinutes, FeeCents;
        public int ScheduledDay;
        public int InfluenceBefore, InfluenceAfter, ReputationBefore, ReputationAfter, RecentSpecials;
        public KolLiveSession Copy()=>(KolLiveSession)MemberwiseClone();
    }
    [Serializable] public sealed class KolStreamingState
    {
        public int Version=2,NextId=1;
        public KolLiveContract[] Contracts=Array.Empty<KolLiveContract>();
        public KolCareerState[] Careers=Array.Empty<KolCareerState>();
        public KolLiveSession[] Sessions=Array.Empty<KolLiveSession>();
        public string[] Followed = Array.Empty<string>();
        public KolRivalStanding[] Rivals = Array.Empty<KolRivalStanding>();
        public KolStreamingState Copy()=>new KolStreamingState{Version=Version,NextId=NextId,
            Contracts=Array.ConvertAll(Contracts??Array.Empty<KolLiveContract>(),c=>c.Copy()),
            Careers=Array.ConvertAll(Careers??Array.Empty<KolCareerState>(),c=>c.Copy()),
            Sessions=Array.ConvertAll(Sessions??Array.Empty<KolLiveSession>(),c=>c.Copy()),
            Followed=(Followed??Array.Empty<string>()).ToArray(),
            Rivals=Array.ConvertAll(Rivals??Array.Empty<KolRivalStanding>(),r=>r.Copy())};
    }
    [Serializable] public sealed class KolRivalStanding
    {
        public string Kind;
        public int Influence, Reputation = 50;
        public KolRivalStanding Copy() => (KolRivalStanding)MemberwiseClone();
    }

    public sealed class KolRoomStatus
    {
        public string PersonId, BrandId, Sponsor, Blocker, SessionId;
        public string NextBrandId, NextSponsor, UpcomingBrandName;
        public long NextStart = -1, EndAt = -1, UpcomingBrandStart = -1;
        public int Viewers, GiftWait, SponsorInfluence, SponsorReputation = 50;
        public bool IsLive => Blocker == null;
        public KolLiveSession Session;
    }
    public sealed partial class SaveData {public KolStreamingState kolStreaming;}
}
