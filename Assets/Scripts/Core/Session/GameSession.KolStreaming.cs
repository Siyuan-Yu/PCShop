using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private KolStreamingState _kol=new KolStreamingState();
        private readonly System.Collections.Generic.Dictionary<string,KolLiveSession> _kolSessionIndex=new System.Collections.Generic.Dictionary<string,KolLiveSession>();
        private readonly List<KolLiveSession> _openKolSessions = new List<KolLiveSession>();
        public KolStreamingState KolStreaming=>_kol.Copy();
        public const int KolMinimumLiveMinutes = 180;
        public bool BrandManagementUnlocked=>true;
        public static int KolCapacity(int level)=>level>=2?12:level==1?7:3;
        public string BrandLevelName(string id)=>Brand(id)?.Level>=2?"高级品牌":Brand(id)?.Level==1?"中级品牌":"初级品牌";
        public int BrandKolCapacity(string id)=>KolCapacity(Brand(id)?.Level??0);
        private bool KolContractCurrent(KolLiveContract c,long at)=>!c.Ended&&c.SignedAt<=at&&at<c.EndAt;
        public int BrandKolCount(string id)=>_kol.Contracts.Count(c=>c.BrandId==id&&KolContractCurrent(c,Clock.Now.TotalMinutes));
        public long KolSigningFee(string person, int days = 90) =>
            KolMarket.Get(person) == null ? 0 : (long)Math.Ceiling((1000L + KolMarket.Get(person).Audience / 8) * days / 90.0) * 10;

        public KolLiveContract ActiveKolAmbassador(string person) =>
            KolPersonContract(person, Clock.Now.TotalMinutes)?.Copy();
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
        public string KolSigningBlocker(string person, int days, int frequency, bool remote = false, bool phoneCall = false)
        {
            if (IsGameOver || KolMarket.Get(person) == null) return "无法签约该代言人";
            if (!IsMet(person)) return "先交换联系方式，或请MCN介绍";
            if (remote)
            {
                string contact = RemoteConversationBlocker(person, phoneCall);
                if (contact != null) return contact;
            }
            else if (!CitySamePlace(person)) return "请与本人当面交谈，或通过手机联系";
            if (KolPersonContract(person, Clock.Now.TotalMinutes) != null) return "此人已有有效代言合约";
            if (days != 30 && days != 60 && days != 90) return "请选择30、60或90天";
            if (frequency != 1 && frequency != 2 && frequency != 7) return "请选择每天、隔天或每周一次";
            return Ledger.Balance < Money.FromYuan(KolSigningFee(person, days)) ? "签约资金不足" : null;
        }

        public bool SignKolAmbassador(string person, int days, int frequency, bool remote = false, bool phoneCall = false)
        {
            string why = KolSigningBlocker(person, days, frequency, remote, phoneCall);
            if (why != null) return Fail(why);
            long fee = KolSigningFee(person, days) * 100;
            if (!BrandPay(fee, days + "天代言签约 · " + CityPerson(person).Name)) return false;
            long now = Clock.Now.TotalMinutes;
            var contract = new KolLiveContract {
                Id = "kol:" + _kol.NextId++, PersonId = person, SignedAt = now, EndAt = now + days * 1440L,
                LastProcessedAt = now, ScheduleEffectiveAt = now,
                FeeCents = fee, SessionFeeCents = KolLiveSessionQuote(person) * 100, FirstDay = BrandDay, FrequencyDays = frequency, Enabled = false,
                AgencyId = BrandAgency.ForPerson(person).Id
            };
            _kol.Contracts = AppendBrand(_kol.Contracts, contract);
            foreach (var session in _openKolSessions)
                if (session.PersonId == person && session.RivalKind != null)
                    session.EndsAt = Math.Min(session.EndsAt, now);
            FinishDueKolSessions(now);
            var brand = AssignDefaultKolBrand(contract);
            EnsureKolCareer(person);
            Say("已和" + CityPerson(person).Name + "签约。" + (brand == null
                ? "暂无有空位的品牌，可到「我的代言人」分配。"
                : "已分配给" + brand.Name + "，首播为第" + (contract.FirstDay + 1) + "天 18:00。"));
            return true;
        }

        public string DefaultKolBrandName(string person) => DefaultKolBrand(person)?.Name;

        private BrandRecord DefaultKolBrand(string person)
        {
            string kind = KolMarket.Get(person)?.Kind;
            return _brands.Brands.OrderByDescending(b => b.Kind == kind)
                .FirstOrDefault(b => BrandKolCount(b.Id) < BrandKolCapacity(b.Id));
        }

        private BrandRecord AssignDefaultKolBrand(KolLiveContract contract)
        {
            var brand = DefaultKolBrand(contract.PersonId);
            if (brand == null) return null;
            contract.BrandId = brand.Id;
            contract.FirstDay = BrandDay + (Clock.MinuteOfDay > contract.StartMinute ? 1 : 0);
            contract.ScheduleEffectiveAt = Clock.Now.TotalMinutes;
            contract.Enabled = true;
            LinkAmbassador(contract);
            return brand;
        }

        public string KolAssignmentBlocker(string contractId, string brandId)
        {
            var c = FindKolContract(contractId);
            if (IsGameOver || c == null || !KolContractCurrent(c, Clock.Now.TotalMinutes)) return "代言合约已结束";
            if (!string.IsNullOrEmpty(c.BrandId)) return "这份合约已分配品牌";
            if (Brand(brandId) == null) return "请先创立品牌";
            if (BrandKolCount(brandId) >= BrandKolCapacity(brandId)) return "当前品牌代言名额已满";
            return null;
        }

        public bool AssignKolBrand(string contractId, string brandId)
        {
            string why = KolAssignmentBlocker(contractId, brandId);
            if (why != null) return Fail(why);
            var c = FindKolContract(contractId);
            long now = Clock.Now.TotalMinutes;
            c.BrandId = brandId;
            c.FirstDay = BrandDay;
            c.ScheduleEffectiveAt = now;
            LinkAmbassador(c);
            return true;
        }

        public bool EndKolLive(string id)
        {
            var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);if(IsGameOver||c==null||!KolContractCurrent(c,Clock.Now.TotalMinutes))return false;
            if(_brands.Exhibits.Any(e=>e.Format==2&&e.Attending&&!e.Finished&&!e.Missed&&e.KolIds.Contains(c.PersonId)))return Fail("正在为展会站台，闭展后才能解除合约");
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
            if (c == null || Brand(c.BrandId) == null) return Fail("请先为代言人分配品牌");
            return ConfigureKolSchedule(id, c.BrandId, start, end, enabled, frequencyDays, firstDay);
        }

        public int KolBrandScheduleAnchor(KolLiveContract contract, int start, int frequencyDays) =>
            contract.Enabled && contract.FrequencyDays == frequencyDays
                ? contract.FirstDay : BrandDay + (Clock.MinuteOfDay > start ? 1 : 0);

        public string KolScheduleBlocker(string id, string brandId, int start, int end, bool enabled, int frequencyDays, int firstDay)
        {
            var c = FindKolContract(id);
            if (IsGameOver || c == null || !KolContractCurrent(c, Clock.Now.TotalMinutes)) return "代言合约已结束";
            if (string.IsNullOrEmpty(brandId) && enabled) return "请先选择代言品牌";
            if (!string.IsNullOrEmpty(brandId) && Brand(brandId) == null) return "请选择已有的品牌";
            if (c.BrandId != brandId)
            {
                if (brandId != null && BrandKolCount(brandId) >= BrandKolCapacity(brandId)) return "当前品牌代言名额已满";
                if (c.BrandId != null && _brands.Exhibits.Any(e => e.Format == 2 && e.BrandId == c.BrandId
                    && !e.Finished && !e.Missed && BrandDay < e.Day + 4 && e.KolIds.Contains(c.PersonId)))
                    return "该代言人已安排参展，参展结束后可更改品牌";
            }
            int duration = (end - start + 1440) % 1440;
            if (start < 0 || start >= 1440 || end < 0 || end >= 1440 || start % 15 != 0 || end % 15 != 0 || duration < KolMinimumLiveMinutes || duration > 720)
                return "直播时间按15分钟选择，每场3至12小时，可跨午夜";
            if (frequencyDays != 1 && frequencyDays != 2 && frequencyDays != 7)
                return "请选择每天、隔天或每周一次";
            if (enabled && (firstDay < 0 || firstDay * 1440L + start >= c.EndAt))
                return "合约到期前已没有可安排的品牌直播时段";
            return null;
        }

        public bool ConfigureKolSchedule(string id, string brandId, int start, int end, bool enabled, int frequencyDays, int firstDay)
        {
            string why = KolScheduleBlocker(id, brandId, start, end, enabled, frequencyDays, firstDay);
            if (why != null) return Fail(why);
            var c = FindKolContract(id);
            bool reassigning = c.BrandId != brandId;
            if (!reassigning && c.StartMinute == start && c.EndMinute == end && c.Enabled == enabled
                && c.FrequencyDays == frequencyDays && c.FirstDay == firstDay) return true;

            long now = Clock.Now.TotalMinutes;
            if (reassigning)
            {
                FinishKolContractSessions(c, now);
                foreach (var deal in _brands.Deals.Where(d => d.KolContractId == c.Id && !d.Ended)) deal.Ended = true;
                c.BrandId = brandId;
            }
            c.StartMinute = start;
            c.EndMinute = end;
            c.Enabled = enabled;
            c.FrequencyDays = frequencyDays;
            c.FirstDay = firstDay;
            c.ScheduleVersion++;
            c.ScheduleEffectiveAt = now;
            if (reassigning) LinkAmbassador(c);
            return true;
        }

        private KolLiveContract FindKolContract(string id) => _kol.Contracts.FirstOrDefault(c => c.Id == id);
        private static int KolDuration(KolLiveContract c) => (c.EndMinute - c.StartMinute + 1440) % 1440;
        private static bool KolSpecialDay(KolLiveContract c, long day) =>
            c != null && c.Enabled && !string.IsNullOrEmpty(c.BrandId) && day >= c.FirstDay && (day - c.FirstDay) % Math.Max(1, c.FrequencyDays) == 0
            && day * 1440 + c.StartMinute < c.EndAt && day * 1440 + c.StartMinute >= Math.Max(c.SignedAt, c.ScheduleEffectiveAt);

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
            long start = KolWindowStart(at, c.StartMinute);
            return start >= 0 && KolSpecialDay(c, start / 1440) && at < start + KolDuration(c) ? start : -1;
        }

        private static long KolWindowStart(long at, int minute)
        {
            long start = at / 1440 * 1440 + minute;
            return start > at ? start - 1440 : start;
        }

        private bool KolPersonAvailable(KolSlot slot, long at)
        {
            string id = slot.Person;
            var p = CityPersonState(id);
            if (p == null || !string.IsNullOrEmpty(p.ActivityId) || !string.IsNullOrEmpty(p.VisitId)) return false;
            string place = p.Route == null ? p.LocationPlaceId : at >= p.Route.ArriveMinute ? p.Route.TargetPlaceId : null;
            if (place == null || BrandExpoPersonReserved(id) || _district.MeetingPeople.Contains(id)) return false;
            if (Employees.Any(e => PersonIdOf(e.Person) == id && e.Person.OnDuty(new GameTimestamp(at)))) return false;
            return slot.Contract != null || !AuthoredCityWorkAt(p, at) || Creators.Get(id) != null && place == CommunityMcnId;
        }

        public string KolLiveBlocker(string id)
        {
            var c = FindKolContract(id);
            long now = Clock.Now.TotalMinutes;
            if (c == null || !KolContractCurrent(c, now)) return "代言合约已结束";
            var slot = KolSlotAt(KolMarket.Get(c.PersonId), now);
            if (!slot.HasValue || slot.Value.Contract != c)
                return c.Enabled ? "未到品牌专场时段" : "品牌专场已暂停";
            return KolSlotBlocker(slot.Value) ?? (slot.Value.Running != null || KolPersonAvailable(slot.Value, now)
                ? null : "主播正在工作、赶路或参加活动，延迟开播");
        }

        private readonly struct KolSlot
        {
            public readonly string Person, Rival;
            public readonly KolLiveContract Contract;
            public readonly long Start, End;
            public readonly KolLiveSession Running;
            public KolSlot(string person, KolLiveContract contract, string rival, long start, long end, KolLiveSession running = null)
            { Person = person; Contract = contract; Rival = rival; Start = start; End = end; Running = running; }
            public string FeeSource => "kol:session-fee:" + Contract.Id + ":" + Start / 1440;
            public string Id => Running?.Id ?? (Contract == null ? "public:" + Person + ":" + Start : Contract.Id + ":" + Start + ":" + Contract.ScheduleVersion);
        }

        private KolLiveContract KolPersonContract(string person, long at)
        {
            foreach (var c in _kol.Contracts)
                if (c.PersonId == person && KolContractCurrent(c, at)) return c;
            return null;
        }

        private KolSlot? KolSlotAt(KolTalentDef talent, long at)
        {
            var running = _openKolSessions.Find(s => s.PersonId == talent.Id && s.StartedAt <= at && at < s.EndsAt);
            if (running != null)
                return new KolSlot(talent.Id, FindKolContract(running.ContractId), running.RivalKind,
                    running.StartedAt, running.EndsAt, running);
            return KolPlannedSlotAt(talent, at);
        }

        private KolSlot? KolPlannedSlotAt(KolTalentDef talent, long at)
        {
            var contract = KolPersonContract(talent.Id, at);
            long brandStart = KolScheduledStart(contract, at);
            if (brandStart >= 0)
                return new KolSlot(talent.Id, contract, null, brandStart, Math.Min(contract.EndAt, brandStart + KolDuration(contract)));
            if (contract != null)
            {
                long personalStart = KolWindowStart(at, contract.StartMinute);
                long personalEnd = personalStart + KolDuration(contract);
                if (personalStart < Math.Max(contract.SignedAt, contract.ScheduleEffectiveAt) || at >= personalEnd) return null;
                return new KolSlot(talent.Id, null, null, personalStart, personalEnd);
            }

            if (!City.IsLakeCity) return null;
            long start = KolWindowStart(at, talent.PublicStartMinute);
            long day = start / 1440;
            long end = start + KolMinimumLiveMinutes;
            if (start < 0 || at >= end) return null;

            // A few creators have regular local-business specials. An exclusive signing reserves
            // their commercial slots for the player's brand, leaving ordinary personal broadcasts.
            bool rival = KolPersonContract(talent.Id, start) == null && day % 7 == 4 &&
                (talent.Id == "qiao" || talent.Id == "life_designer" || talent.Id == "life_creator");
            return new KolSlot(talent.Id, null, rival ? talent.Kind : null, start, end);
        }

        private bool HoldKolLivePerson(CityPersonState p, long now)
        {
            var talent = KolMarket.Get(p.PersonId);
            if (talent == null) return false;
            var slot = KolSlotAt(talent, now);
            if (!slot.HasValue || KolSlotBlocker(slot.Value) != null) return false;
            if (slot.Value.Running == null && !KolPersonAvailable(slot.Value, now)) return false;
            p.Task = CityPersonTask.Working;
            p.TaskLabel = "正在直播";
            return true;
        }

        private long NextKolRoomStart(KolTalentDef talent, long now)
        {
            var contract = KolPersonContract(talent.Id, now);
            if (contract != null)
            {
                long next = KolWindowStart(now, contract.StartMinute) + 1440;
                if (next < contract.EndAt) return next;
                now = contract.EndAt - 1;
            }
            if (!City.IsLakeCity) return -1;
            return KolWindowStart(now, talent.PublicStartMinute) + 1440;
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
            if (includeNext)
            {
                room.NextStart = NextKolRoomStart(talent, now);
                if (room.NextStart >= 0)
                {
                    var next = KolPlannedSlotAt(talent, room.NextStart);
                    room.NextBrandId = next?.Contract?.BrandId;
                    string rivalKind = next?.Rival;
                    room.NextSponsor = rivalKind == null ? Brand(room.NextBrandId)?.Name : BrandRivalName(rivalKind);
                }
                var contract = KolPersonContract(person, now);
                room.UpcomingBrandStart = KolNextSpecialStart(contract, now + 1);
                if (room.UpcomingBrandStart >= 0) room.UpcomingBrandName = Brand(contract.BrandId)?.Name;
            }
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
            room.Blocker = KolSlotBlocker(slot);
            if (room.Blocker != null) return room;
            if (slot.Running == null && !KolPersonAvailable(slot, now))
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

        private string KolSlotBlocker(KolSlot slot)
        {
            if (_kolSessionIndex.TryGetValue(slot.Id, out var session)) return session.Settled ? "本场直播已结束" : null;
            if (slot.Contract == null) return null;
            if (Ledger.HasSource(slot.FeeSource)) return "本场直播已结束";
            return Ledger.CanAfford(Money.FromCents(KolLiveSessionFee(slot.Contract))) ? null : "资金不足，等待支付本场直播费";
        }

        private KolLiveSession StartKolSession(KolSlot slot, long at)
        {
            string id = slot.Id;
            if (_kolSessionIndex.TryGetValue(id, out var session)) return session;
            long fee = 0;
            if (slot.Contract != null)
            {
                if (Ledger.HasSource(slot.FeeSource)) return null;
                fee = KolLiveSessionFee(slot.Contract);
                if (!Ledger.TryDebit(new GameDate((int)(at / 1440)), "KOL直播场次费", Money.FromCents(fee),
                    CityPerson(slot.Person).Name + " · " + Brand(slot.Contract.BrandId).Name,
                    new LedgerAttribution(LedgerBusinessKind.Operating, propertyId: slot.Contract.BrandId, sourceEventId: slot.FeeSource)))
                    return null;
            }
            var brand = Brand(slot.Contract?.BrandId);
            session = new KolLiveSession
            {
                Id = id, ContractId = slot.Contract?.Id, PersonId = slot.Person, BrandId = brand?.Id,
                RivalKind = slot.Rival, SponsorName = brand?.Name ?? (slot.Rival == null ? null : BrandRivalName(slot.Rival)),
                ShowcaseKind = brand?.Kind ?? slot.Rival ?? KolMarket.Get(slot.Person).Kind,
                FeeCents = fee, StartedAt = at, EndsAt = Math.Max(slot.End, at + KolMinimumLiveMinutes),
                ScheduledDay = (int)(slot.Start / 1440)
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
                    if (!slot.HasValue || (slot.Value.Running == null && !KolPersonAvailable(slot.Value, at))) continue;
                    var session = slot.Value.Running ?? StartKolSession(slot.Value, at);
                    if (session == null || session.Settled) continue;
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
            _kol.Contracts = _kol.Contracts.Where(c => KolMarket.Get(c.PersonId) != null && (string.IsNullOrEmpty(c.BrandId) || Brand(c.BrandId) != null))
                .GroupBy(c => c.Id).Select(g => g.First()).ToArray();
            long now = Clock.Now.TotalMinutes;
            foreach (var c in _kol.Contracts)
            {
                if (string.IsNullOrEmpty(c.BrandId)) c.Enabled = false;
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
            foreach (var contract in _kol.Contracts)
            {
                if (contract.SessionFeeCents <= 0) contract.SessionFeeCents = KolLiveSessionFee(contract);
                if (KolDuration(contract) < KolMinimumLiveMinutes)
                    contract.EndMinute = (contract.StartMinute + KolMinimumLiveMinutes) % 1440;
                if (string.IsNullOrEmpty(contract.BrandId) && contract.ScheduleVersion == 0 && KolContractCurrent(contract, now))
                    AssignDefaultKolBrand(contract);
            }
            _kol.NextId = Math.Max(_kol.NextId, _kol.Contracts.Length + 1);
        }

    }
}
