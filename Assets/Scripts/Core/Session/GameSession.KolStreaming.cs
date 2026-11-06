using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private KolStreamingState _kol=new KolStreamingState();
        private readonly System.Collections.Generic.Dictionary<string,KolLiveSession> _kolSessionIndex=new System.Collections.Generic.Dictionary<string,KolLiveSession>();
        private readonly List<KolLiveSession> _openKolSessions = new List<KolLiveSession>();
        public KolStreamingState KolStreaming=>_kol.Copy();
        public bool BrandManagementUnlocked=>true;
        public static int KolCapacity(int level)=>level>=2?12:level==1?7:3;
        public string BrandLevelName(string id)=>Brand(id)?.Level>=2?"高级品牌":Brand(id)?.Level==1?"中级品牌":"初级品牌";
        public int BrandKolCapacity(string id)=>KolCapacity(Brand(id)?.Level??0);
        private bool KolContractCurrent(KolLiveContract c,long at)=>!c.Ended&&c.SignedAt<=at&&at<c.EndAt;
        public int BrandKolCount(string id)=>_kol.Contracts.Count(c=>c.BrandId==id&&KolContractCurrent(c,Clock.Now.TotalMinutes));
        public long KolSigningFee(string person)=>KolMarket.Get(person)==null?0:1000L+KolMarket.Get(person).Audience/8;
        public string KolUpgradeBlocker(string brandId)
        {
            var b=Brand(brandId);if(IsGameOver||b==null)return "没有可升级的品牌";
            if(b.Level>=2)return "已达到高级品牌";
            int influence=b.Level==0?100:300;long price=b.Level==0?20000:100000;
            if(b.Influence<influence)return "品牌知名度需要达到"+influence;
            return Ledger.Balance<Money.FromYuan(price)?"升级需要¥"+price:null;
        }
        public bool UpgradeKolBrand(string brandId)
        {
            string why=KolUpgradeBlocker(brandId);if(why!=null)return Fail(why);
            var b=Brand(brandId);if(!BrandPay((b.Level==0?20000L:100000L)*100,"品牌升级 · "+b.Name,b.Id))return false;
            b.Level++;return true;
        }
        public string KolSigningBlocker(string brandId,string person)
        {
            if(IsGameOver||Brand(brandId)==null||KolMarket.Get(person)==null)return "无法签约该直播合作";
            if(City.IsLakeCity&&!IsMet(person))return "先去社区MCN实际认识、交换名片，再签约";
            string reception=McnSigningBlocker(person);if(reception!=null)return reception;
            if(_kol.Contracts.Any(c=>c.PersonId==person&&KolContractCurrent(c,Clock.Now.TotalMinutes)))return "此KOL已有品牌代言合约";
            if(_brands.Deals.Any(c=>c.PersonId==person&&!c.Ended&&c.End>BrandDay&&(c.BrandId==brandId||c.Exclusive)))return "此KOL已有其他品牌独家代言";
            if(BrandKolCount(brandId)>=BrandKolCapacity(brandId))return "代言签约名额已满，请升级品牌或解除合约";
            return Ledger.Balance<Money.FromYuan(KolSigningFee(person))?"签约资金不足":null;
        }
        public bool SignKolLive(string brandId,string person)
        {
            string why=KolSigningBlocker(brandId,person);if(why!=null)return Fail(why);
            long fee=KolSigningFee(person)*100;
            if(!BrandPay(fee,"90天品牌代言签约 · "+(CityPerson(person)?.Name??person),brandId))return false;
            long now=Clock.Now.TotalMinutes;
            _kol.Contracts=AppendBrand(_kol.Contracts,new KolLiveContract{Id="kol:"+_kol.NextId++,BrandId=brandId,PersonId=person,
                SignedAt=now,EndAt=now+90*1440L,LastProcessedAt=now,LastPaidThrough=now,ScheduleEffectiveAt=now,FeeCents=fee,FirstDay=BrandDay,
                AgencyId=BrandAgency.ForPerson(person).Id});
            LinkAmbassador(_kol.Contracts.Last());
            EnsureKolCareer(person);return true;
        }
        public bool EndKolLive(string id)
        {
            var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);if(IsGameOver||c==null||!KolContractCurrent(c,Clock.Now.TotalMinutes))return false;
            if(ActiveBrandExpo?.KolIds.Contains(c.PersonId)==true)return Fail("正在为展会站台，闭展后才能解除合约");
            AccrueKolWages(c,Clock.Now.TotalMinutes);
            FinishKolContractSessions(c, Clock.Now.TotalMinutes);
            c.Ended=true;
            foreach(var d in _brands.Deals.Where(d=>d.KolContractId==c.Id))d.Ended=true;
            RefreshBrandTraffic();return true;
        }
        public bool SetKolSchedule(string id, int start, int end, bool enabled) =>
            SetKolSchedule(id, start, end, enabled, FindKolContract(id)?.FrequencyDays ?? 1, FindKolContract(id)?.FirstDay ?? BrandDay);

        public bool SetKolSchedule(string id, int start, int end, bool enabled, int frequencyDays, int firstDay)
        {
            var c = FindKolContract(id);
            long now = Clock.Now.TotalMinutes;
            if (IsGameOver || c == null || !KolContractCurrent(c, now)) return false;
            int duration = (end - start + 1440) % 1440;
            if (start < 0 || start >= 1440 || end < 0 || end >= 1440 || start % 15 != 0 || end % 15 != 0 || duration < 15 || duration > 720)
                return Fail("直播时间按15分钟选择，每场15分钟至12小时，可跨午夜");
            if (frequencyDays != 1 && frequencyDays != 2 && frequencyDays != 7)
                return Fail("请选择每天、隔天或每周一次");
            if (firstDay < 0 || firstDay * 1440L + start >= c.EndAt)
                return Fail("首次专场需安排在合约期限内");
            if (c.StartMinute == start && c.EndMinute == end && c.Enabled == enabled && c.FrequencyDays == frequencyDays && c.FirstDay == firstDay)
                return true;
            FinishKolContractSessions(c, now);
            c.StartMinute = start;
            c.EndMinute = end;
            c.Enabled = enabled;
            c.FrequencyDays = frequencyDays;
            c.FirstDay = firstDay;
            c.ScheduleVersion++;
            c.ScheduleEffectiveAt = now;
            return true;
        }

        private KolLiveContract FindKolContract(string id) => _kol.Contracts.FirstOrDefault(c => c.Id == id);
        private static int KolDuration(KolLiveContract c) => (c.EndMinute - c.StartMinute + 1440) % 1440;
        private static bool KolSpecialDay(KolLiveContract c, long day) =>
            c != null && c.Enabled && day >= c.FirstDay && (day - c.FirstDay) % Math.Max(1, c.FrequencyDays) == 0
            && day * 1440 + c.StartMinute < c.EndAt && day * 1440 + c.StartMinute + KolDuration(c) > Math.Max(c.SignedAt, c.ScheduleEffectiveAt);

        public long KolNextSpecialStart(KolLiveContract c, long from)
        {
            if (c == null || c.Ended) return -1;
            for (long day = from / 1440; day <= from / 1440 + 7; day++)
            {
                long start = day * 1440 + c.StartMinute;
                if (start >= from && KolSpecialDay(c, day)) return start;
            }
            return -1;
        }

        private long KolScheduledStart(KolLiveContract c, long at)
        {
            if (c == null || !KolContractCurrent(c, at) || at < c.ScheduleEffectiveAt) return -1;
            long day = at / 1440;
            if (at % 1440 < c.StartMinute) day--;
            long start = day * 1440 + c.StartMinute;
            return KolSpecialDay(c, day) && at < start + KolDuration(c) ? start : -1;
        }

        private bool KolPersonAvailable(string id, long at)
        {
            var p = CityPersonState(id);
            if (p == null || p.Route != null || p.LocationPlaceId == null || !string.IsNullOrEmpty(p.ActivityId) || !string.IsNullOrEmpty(p.VisitId))
                return false;
            if (BrandExpoPersonReserved(id) || _district.MeetingPeople.Contains(id)) return false;
            if (Employees.Any(e => PersonIdOf(e.Person) == id && e.Person.OnDuty(Clock.Now))) return false;
            return !AuthoredCityWorkAt(p, at) || Creators.Get(id) != null && p.LocationPlaceId == CommunityMcnId;
        }

        public string KolLiveBlocker(string id)
        {
            var c = FindKolContract(id);
            long now = Clock.Now.TotalMinutes;
            if (c == null || !KolContractCurrent(c, now)) return "代言合约已结束";
            if (!c.Enabled) return "品牌专场已暂停";
            if (KolScheduledStart(c, now) < 0) return "未到品牌专场时段";
            return KolPersonAvailable(c.PersonId, now) ? null : "主播正在工作、赶路或参加活动，延迟开播";
        }

        private readonly struct KolSlot
        {
            public readonly string Person, Rival;
            public readonly KolLiveContract Contract;
            public readonly long Start, End;
            public KolSlot(string person, KolLiveContract contract, string rival, long start, long end)
            { Person = person; Contract = contract; Rival = rival; Start = start; End = end; }
            public string Id => Contract == null ? "public:" + Person + ":" + Start : Contract.Id + ":" + Start + ":" + Contract.ScheduleVersion;
        }

        private KolLiveContract KolPersonContract(string person, long at)
        {
            foreach (var c in _kol.Contracts)
                if (c.PersonId == person && KolContractCurrent(c, at)) return c;
            return null;
        }

        private long KolPublicStart(KolTalentDef talent, long day) => day * 1440 + talent.PublicStartMinute;

        private KolSlot? KolSlotAt(KolTalentDef talent, long at)
        {
            var contract = KolPersonContract(talent.Id, at);
            long brandStart = KolScheduledStart(contract, at);
            if (brandStart >= 0)
                return new KolSlot(talent.Id, contract, null, brandStart, Math.Min(contract.EndAt, brandStart + KolDuration(contract)));
            long day = at / 1440;
            if (!City.IsLakeCity || KolSpecialDay(contract, day)) return null;
            long start = KolPublicStart(talent, day);
            long end = Math.Min((day + 1) * 1440, start + 90);
            if (at < start || at >= end) return null;

            // A few creators have regular local-business specials. An exclusive signing reserves
            // their commercial slots for the player's brand, leaving ordinary personal broadcasts.
            bool rival = contract == null && day % 7 == 4 &&
                (talent.Id == "qiao" || talent.Id == "life_designer" || talent.Id == "life_creator");
            return new KolSlot(talent.Id, null, rival ? talent.Kind : null, start, end);
        }

        private bool HoldKolLivePerson(CityPersonState p, long now)
        {
            var talent = KolMarket.Get(p.PersonId);
            if (talent == null || !KolSlotAt(talent, now).HasValue || !KolPersonAvailable(p.PersonId, now)) return false;
            p.Task = CityPersonTask.Working;
            p.TaskLabel = "正在直播";
            return true;
        }

        private long NextKolRoomStart(KolTalentDef talent, long now)
        {
            var contract = KolPersonContract(talent.Id, now);
            long next = KolNextSpecialStart(contract, now + 1);
            if (!City.IsLakeCity) return next;
            for (long day = now / 1440; day <= now / 1440 + 7; day++)
            {
                long start = KolPublicStart(talent, day);
                if (start <= now || KolSpecialDay(KolPersonContract(talent.Id, start), day)) continue;
                return next < 0 ? start : Math.Min(next, start);
            }
            return next;
        }

        public bool IsFollowingKol(string id) => _kol.Followed.Contains(id);

        public void ToggleKolFollow(string id)
        {
            if (KolMarket.Get(id) == null) return;
            _kol.Followed = IsFollowingKol(id) ? _kol.Followed.Where(p => p != id).ToArray() : AppendBrand(_kol.Followed, id);
        }

        public string KolPublicReputation(string id)
        {
            var career = _brands.Careers.FirstOrDefault(c => c.PersonId == id);
            return career == null || career.Severity == 0 ? "风评稳定" : career.Severity >= 3 ? "近期争议较多" : "近期风评有分歧";
        }

        public int KolBrandRecentSpecials(string brandId) => _kol.Sessions.Count(s =>
            s.BrandId == brandId && s.Settled && s.InfluenceAfter > s.InfluenceBefore &&
            s.EndsAt > Clock.Now.TotalMinutes - 7 * 1440L && s.EndsAt <= Clock.Now.TotalMinutes);

        public KolLiveSession[] KolBrandHistory(string brandId) => _kol.Sessions
            .Where(s => s.BrandId == brandId && s.Settled && s.LiveMinutes > 0)
            .OrderByDescending(s => s.EndsAt).Select(s => s.Copy()).ToArray();

        public KolLiveSession KolSessionResult(string id) =>
            id != null && _kolSessionIndex.TryGetValue(id, out var session) ? session.Copy() : null;

        public KolRoomStatus GetKolRoom(string person, bool includeNext = true)
        {
            var talent = KolMarket.Get(person);
            var room = new KolRoomStatus { PersonId = person, Blocker = "未在线" };
            if (talent == null) return room;
            long now = Clock.Now.TotalMinutes;
            var scheduled = KolSlotAt(talent, now);
            if (includeNext) room.NextStart = NextKolRoomStart(talent, now);
            if (!scheduled.HasValue) return room;
            var slot = scheduled.Value;
            room.SessionId = slot.Id;
            room.BrandId = slot.Contract?.BrandId;
            var brand = Brand(room.BrandId);
            var rival = slot.Rival == null ? null : _kol.Rivals.FirstOrDefault(r => r.Kind == slot.Rival);
            room.Sponsor = slot.Rival == null ? brand?.Name : BrandRivalName(slot.Rival);
            room.SponsorInfluence = brand?.Influence ?? rival?.Influence ?? 0;
            room.SponsorReputation = brand?.Reputation ?? rival?.Reputation ?? 50;
            room.EndAt = slot.End;
            if (!KolPersonAvailable(person, now))
            {
                room.Blocker = "延迟开播 · 主播正在工作、赶路或参加活动";
                return room;
            }
            room.Blocker = null;
            _kolSessionIndex.TryGetValue(room.SessionId, out var session);
            room.Session = session?.Copy();
            room.Viewers = KolViewersAt(talent, session, now);
            room.GiftWait = session == null ? 0 : (int)Math.Max(0, 15 - (now - session.LastTipAt));
            return room;
        }

        public int KolFollowers(string person) => Creator(person)?.Followers
            ?? _kol.Careers.FirstOrDefault(x => x.PersonId == person)?.Followers ?? KolMarket.Get(person)?.Audience ?? 0;
        public int KolPopularity(string person) => _kol.Careers.FirstOrDefault(x => x.PersonId == person)?.Popularity ?? 0;

        private KolCareerState EnsureKolCareer(string person)
        {
            var state = _kol.Careers.FirstOrDefault(x => x.PersonId == person);
            if (state != null) return state;
            state = new KolCareerState { PersonId = person, Followers = KolFollowers(person) };
            _kol.Careers = AppendBrand(_kol.Careers, state);
            return state;
        }

        private int KolViewersAt(KolTalentDef talent, KolLiveSession session, long at)
        {
            double wave = .9 + ((at / 5 + talent.Audience + talent.Charm) % 21) * .01;
            double boost = session == null ? 0 : session.TipBoost * Math.Max(0, 1 - (at - session.LastTipAt) / 60.0);
            var brand = Brand(session?.BrandId);
            int known = brand?.Influence ?? (session?.RivalKind == null ? 0 : _kol.Rivals.FirstOrDefault(r => r.Kind == session.RivalKind)?.Influence ?? 0);
            double draw = 1 + Math.Min(250, known) / 1000.0;
            return Math.Max(1, (int)(KolFollowers(talent.Id) * .035 * (.4 + talent.Charm / 100.0)
                * (1 + Math.Min(1000, KolPopularity(talent.Id)) / 500.0) * wave * draw + boost));
        }

        public int KolRoomViewers(string id)
        {
            var c = FindKolContract(id);
            return c == null || KolLiveBlocker(id) != null ? 0 : GetKolRoom(c.PersonId, false).Viewers;
        }

        private KolLiveSession StartKolSession(KolSlot slot, long at)
        {
            string id = slot.Id;
            if (_kolSessionIndex.TryGetValue(id, out var session)) return session;
            session = new KolLiveSession
            {
                Id = id, ContractId = slot.Contract?.Id, PersonId = slot.Person, BrandId = slot.Contract?.BrandId,
                RivalKind = slot.Rival, StartedAt = at, EndsAt = slot.End, ScheduledDay = (int)(slot.Start / 1440)
            };
            _kol.Sessions = AppendBrand(_kol.Sessions, session);
            _kolSessionIndex[id] = session;
            _openKolSessions.Add(session);
            return session;
        }

        private void TickKolStreams(long from, long to)
        {
            if (!City.IsLakeCity && _kol.Contracts.Length == 0) return;
            bool changed = false;
            for (long at = from; at < to; at++)
            {
                changed |= FinishDueKolSessions(at);
                foreach (var talent in KolMarket.All)
                {
                    var slot = KolSlotAt(talent, at);
                    if (!slot.HasValue || !KolPersonAvailable(talent.Id, at)) continue;
                    var session = StartKolSession(slot.Value, at);
                    if (session.Settled) continue;
                    int viewers = KolViewersAt(talent, session, at);
                    session.LiveMinutes++;
                    session.ViewerMinutes += viewers;
                    session.PeakViewers = Math.Max(session.PeakViewers, viewers);
                    var career = EnsureKolCareer(talent.Id);
                    career.LiveMinutes++;
                    if (session.LiveMinutes % 30 != 0) continue;
                    career.Popularity = Math.Min(1000, career.Popularity + 1);
                    career.Followers = Math.Min(2000000, KolFollowers(talent.Id) + Math.Max(1, viewers / 100));
                    var creator = Creator(talent.Id);
                    if (creator != null) creator.Followers = career.Followers;
                }
            }
            changed |= FinishDueKolSessions(to);
            if (changed) RefreshBrandTraffic();
            if (_kol.Sessions.Length <= 720) return;
            _kol.Sessions = _kol.Sessions.OrderByDescending(s => s.EndsAt).Take(720).ToArray();
            _kolSessionIndex.Clear();
            foreach (var session in _kol.Sessions) _kolSessionIndex[session.Id] = session;
        }

        private bool FinishDueKolSessions(long at)
        {
            bool changed = false;
            for (int i = _openKolSessions.Count - 1; i >= 0; i--)
            {
                var session = _openKolSessions[i];
                if (session.EndsAt > at) continue;
                changed |= SettleKolSession(session);
                _openKolSessions.RemoveAt(i);
            }
            return changed;
        }

        private void FinishKolContractSessions(KolLiveContract contract, long now)
        {
            foreach (var session in _openKolSessions)
                if (session.ContractId == contract.Id) session.EndsAt = Math.Min(session.EndsAt, now);
            if (FinishDueKolSessions(now)) RefreshBrandTraffic();
        }

        private bool SettleKolSession(KolLiveSession session)
        {
            if (session.Settled) return false;
            session.Settled = true;
            long minutes = session.LiveMinutes - session.LegacyMinutes;
            if (minutes < 15) return false;
            var brand = Brand(session.BrandId);
            if (brand == null && session.RivalKind == null) return false;
            if (brand != null && _kol.Sessions.Any(s => s != session && s.Settled && s.BrandId == session.BrandId &&
                s.PersonId == session.PersonId && s.ScheduledDay == session.ScheduledDay && s.InfluenceAfter > s.InfluenceBefore))
                return false;
            int prior = _kol.Sessions.Count(s => s != session && s.Settled && s.InfluenceAfter > s.InfluenceBefore &&
                s.EndsAt > session.EndsAt - 7 * 1440L && s.EndsAt <= session.EndsAt &&
                (brand != null ? s.BrandId == brand.Id : s.RivalKind == session.RivalKind));
            session.RecentSpecials = prior + 1;
            var talent = KolMarket.Get(session.PersonId);
            double fit = brand == null || talent.Kind == brand.Kind ? 1 : .65;
            double quality = Math.Min(2.5, .75 + session.ViewerMinutes / (double)Math.Max(1, session.LiveMinutes) / 400);
            double returnFactor = fit * quality / (1 + prior * .22);
            int awareness = Math.Max(1, (int)Math.Round(Math.Min(3, minutes / 45.0) * returnFactor));
            int reputation = Math.Max(0, (int)Math.Round(Math.Min(2, minutes / 75.0) * returnFactor * talent.Intelligence / 80.0));
            var rival = brand == null ? EnsureKolRival(session.RivalKind) : null;
            session.InfluenceBefore = brand?.Influence ?? rival.Influence;
            session.ReputationBefore = brand?.Reputation ?? rival.Reputation;
            session.InfluenceAfter = session.InfluenceBefore + awareness;
            session.ReputationAfter = session.ReputationBefore + reputation;
            if (brand == null)
            {
                rival.Influence = session.InfluenceAfter;
                rival.Reputation = session.ReputationAfter;
                return false;
            }
            brand.Influence = session.InfluenceAfter;
            brand.Reputation = session.ReputationAfter;
            return true;
        }

        private KolRivalStanding EnsureKolRival(string kind)
        {
            var rival = _kol.Rivals.FirstOrDefault(r => r.Kind == kind);
            if (rival != null) return rival;
            rival = new KolRivalStanding { Kind = kind };
            _kol.Rivals = AppendBrand(_kol.Rivals, rival);
            return rival;
        }

        public bool TipKolLive(string id, int yuan)
        {
            var contract = FindKolContract(id);
            string why = KolLiveBlocker(id);
            return why != null ? Fail(why) : TipKolRoom(contract.PersonId, yuan);
        }

        public bool TipKolRoom(string person, int yuan, string expectedSession = null)
        {
            if (IsGameOver || (yuan != 10 && yuan != 100 && yuan != 1000)) return false;
            var room = GetKolRoom(person, false);
            if (!room.IsLive) return Fail(room.Blocker);
            if (room.Session == null) return Fail("直播刚刚开始，请先让游戏时间推进一分钟");
            if (expectedSession != null && expectedSession != room.Session.Id) return Fail("这场直播已经结束");
            if (room.GiftWait > 0) return Fail("每个直播间每15游戏分钟可打赏一次");
            var session = _kolSessionIndex[room.Session.Id];
            long now = Clock.Now.TotalMinutes;
            if (!Ledger.TryDebit(Clock.Today, "直播打赏", Money.FromYuan(yuan), CityPerson(person).Name,
                LedgerAttribution.ForAsset(room.BrandId, "kol:gift:" + session.Id + ":" + now))) return Fail("打赏现金不足");
            int remaining = (int)(session.TipBoost * Math.Max(0, 1 - (now - session.LastTipAt) / 60.0));
            session.LastTipAt = now;
            session.TipsCents += yuan * 100L;
            session.TipCount++;
            session.LastGiftYuan = yuan;
            session.TipBoost = Math.Min(300, remaining + (yuan == 10 ? 25 : yuan == 100 ? 60 : 120));
            var career = EnsureKolCareer(person);
            career.TipsReceivedCents += yuan * 100L;
            career.Popularity = Math.Min(1000, career.Popularity + (yuan == 10 ? 1 : yuan == 100 ? 3 : 8));
            career.Followers = Math.Min(2000000, KolFollowers(person) + (yuan == 10 ? 5 : yuan == 100 ? 20 : 80));
            var creator = Creator(person);
            if (creator != null) creator.Followers = career.Followers;
            return true;
        }

        private void RestoreKolStreaming(SaveData data)
        {
            _kol = data.kolStreaming?.Copy() ?? new KolStreamingState();
            _kol.Contracts = _kol.Contracts.Where(c => KolMarket.Get(c.PersonId) != null && Brand(c.BrandId) != null)
                .GroupBy(c => c.Id).Select(g => g.First()).ToArray();
            long now = Clock.Now.TotalMinutes;
            foreach (var c in _kol.Contracts)
            {
                if (c.FrequencyDays != 2 && c.FrequencyDays != 7) c.FrequencyDays = 1;
                if (c.LastPaidThrough == 0) c.LastPaidThrough = now;
            }
            if (_kol.Version < 2)
            {
                foreach (var session in _kol.Sessions)
                {
                    var c = FindKolContract(session.ContractId);
                    session.PersonId = c?.PersonId;
                    session.BrandId = c?.BrandId;
                    session.LegacyMinutes = session.LiveMinutes;
                    session.ScheduledDay = (int)(session.StartedAt / 1440);
                    session.Settled = c == null || !KolContractCurrent(c, now) || session.EndsAt <= now;
                }
                _kol.Version = 2;
            }
            _kol.Sessions = _kol.Sessions.Where(s => KolMarket.Get(s.PersonId) != null).ToArray();
            _kolSessionIndex.Clear();
            _openKolSessions.Clear();
            foreach (var session in _kol.Sessions)
            {
                _kolSessionIndex[session.Id] = session;
                if (!session.Settled) _openKolSessions.Add(session);
            }
            RestoreBrandAmbassadors();
            _kol.NextId = Math.Max(_kol.NextId, _kol.Contracts.Length + 1);
        }

    }
}
