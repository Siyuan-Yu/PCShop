using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const string Xinglan = "racer_0", Zhixia = "racer_1", Jingxing = "racer_2", Ruoning = "racer_3";
        private const string Ming = "life_ming", CollectorTrack = "race_3", CollectorModel = "c_718";
        private const int EventTuningFee = 800, EventTuningMinutes = 30, MingLoanAmount = 3000;
        private int _collectorTrialThrough = -1;
        private bool _collectorTrialDriving, _mingAfterRace;

        internal RaceInvitation FindEventRace(string personId)
        {
            if (RacingAccessBlocker != null || CityPersonState(personId) == null) return null;
            var tracks = RaceTracks.All.Where(track => City.HasPlace(track.Id) && DistrictRoadOpen(track.Tier)
                && DistrictAccessTier >= track.Tier && RacingWins >= track.RequiredWins)
                .Select(track => (Track: track, Travel: CityTravelMinutes(City.GetPlace(track.Id))))
                .Where(track => track.Travel < int.MaxValue / 8).OrderBy(track => track.Travel).ToArray();
            long now = Clock.Now.TotalMinutes;
            for (int offset = 0; offset <= 14; offset++)
            {
                var day = Clock.Today.AddDays(offset);
                if (RacingSchedule.On(day) == RaceMeeting.None) continue;
                long opens = (long)day.TotalDays * 1440 + RacingSchedule.MeetOpenMinute;
                long closes = (long)day.TotalDays * 1440 + RacingSchedule.MeetCloseMinute;
                foreach (var entry in tracks)
                {
                    long earliest = Math.Max(opens, now + entry.Travel);
                    if (earliest + 30 > closes || RaceInviteBlocker(personId, entry.Track.Id, day, true) != null) continue;
                    if (_raceInvitations.Any(i => i.Day == day.TotalDays && i.TrackId != entry.Track.Id)) continue;
                    var conflicts = _cityActivities.Where(a => a.Active && a.OccupiedUntil > earliest && a.OccupiedFrom < closes)
                        .OrderBy(a => a.OccupiedFrom);
                    long available = earliest;
                    foreach (var conflict in conflicts)
                    {
                        if (available + 30 <= conflict.OccupiedFrom) break;
                        available = Math.Max(available, conflict.OccupiedUntil);
                    }
                    if (available + 30 <= closes) return new RaceInvitation { PersonId = personId, TrackId = entry.Track.Id, Day = day.TotalDays };
                }
            }
            return null;
        }

        private string EventRaceSummary(RaceInvitation offer)
        {
            var day = new GameDate(offer.Day);
            var meeting = RacingSchedule.On(day);
            return $"{day} 09:00–18:00 · {RaceTracks.Get(offer.TrackId).Name} · {RacingSchedule.Name(meeting)} · 到场报名 ¥{RacingSchedule.Fee(meeting):N0}";
        }

        private bool AcceptEventRace(RaceInvitation offer)
        {
            var current = FindEventRace(offer.PersonId);
            if (current == null || current.Day != offer.Day || current.TrackId != offer.TrackId) return false;
            _raceInvitations.RemoveAll(i => i.Day < Clock.Today.TotalDays);
            _raceInvitations.Add(offer);
            return true;
        }

        internal bool CanOfferMingRace => !PersonHasWork(Ming) && FindEventRace(Ming) != null;
        private bool MingCanBorrow => !_scheduledCredits.Any(c => c.memo == MingRepaymentMemo || c.memo == "许明还钱")
            && Ledger.CanAfford(Money.FromYuan(MingLoanAmount));

        private EventDef BindEventRace(EventDef template, string personId)
        {
            bool ming = personId == Ming;
            if (ming && PersonHasWork(Ming)) return null;
            var offer = FindEventRace(personId);
            if (offer == null) return null;
            string Accept(bool lend)
            {
                if (lend && (!MingCanBorrow || PersonHasWork(Ming))) return "这笔周转款未登记。";
                if (!AcceptEventRace(offer)) return "这场比赛已经约不上了。";
                Meet(personId, ming ? lend ? 16 : 12 : 18);
                string result = "已记入日程。请到场后自行报名。";
                if (!lend) return result;
                EventLendToNpc(Money.FromYuan(MingLoanAmount), "许明");
                return result + "\n已借给许明 ¥3,000。" + ScheduleNpcRepayment(5, Money.FromYuan(MingLoanAmount), MingRepaymentMemo);
            }
            var choices = new List<EventChoice>();
            if (ming)
                choices.Add(Choose("一起参赛，借他 ¥3,000", _ => Accept(true), s => s.MingCanBorrow && !s.PersonHasWork(Ming),
                    "现金不足，或上一笔周转款还没收回"));
            choices.Add(Choose(ming ? "一起参赛，这次不借钱" : "确认一起参赛", _ => Accept(false)));
            choices.Add(Choose("这次不去", _ => "你们约好有空再联系。"));
            string invitation = ming ? "许明最近还没找到工作，约你去比赛，也问能不能先借 ¥3,000 周转，五天后归还。"
                : "许星澜邀你一起参加这场正式比赛。";
            return EventScene(template, invitation + "\n" + EventRaceSummary(offer), choices.ToArray());
        }

        private IEnumerable<(CityActivityDefinition Activity, long Start)> ClubGatheringOptions()
        {
            if (RacingAccessBlocker != null || CityPersonState(Zhixia) == null) yield break;
            foreach (string id in new[] { "dinner", "bar" })
            {
                var activity = CityLifeCatalog.Activity(id);
                if (!City.HasPlace(activity?.VenueId) || DistrictAccessBlocker(City.GetPlace(activity.VenueId)) != null) continue;
                long start = SuggestCityActivityStart(id, Zhixia);
                if (start > Clock.Now.TotalMinutes && CityActivityBlocker(id, new[] { Zhixia }, start) == null)
                    yield return (activity, start);
            }
        }

        internal bool CanArrangeClubGathering => RacingAccessBlocker == null && CityPersonState(Zhixia) != null
            && new[] { "dinner", "bar" }.Any(id => City.HasPlace(CityLifeCatalog.Activity(id)?.VenueId)
                && DistrictAccessBlocker(City.GetPlace(CityLifeCatalog.Activity(id).VenueId)) == null);

        private EventDef BindClubGathering(EventDef template)
        {
            if (!CanArrangeClubGathering) return null;
            var choices = new List<EventChoice>();
            foreach (var option in ClubGatheringOptions())
            {
                string name = option.Activity.Id == "dinner" ? "吃饭小聚" : "喝点东西";
                string venue = City.GetPlace(option.Activity.VenueId).Name;
                choices.Add(Choose($"{name} · {venue} · {new GameTimestamp(option.Start)}", s =>
                    s.ScheduleCityActivity(option.Activity.Id, new[] { Zhixia }, option.Start, sourceId: "legacy-racing-club:" + s.Clock.Today.TotalDays) != null
                        ? "聚会已记入日程，到场后再点单。" : "这个时段已经约不上了。"));
            }
            var offer = FindEventRace(Zhixia);
            if (offer != null)
                choices.Add(Choose("约正式比赛", s =>
                {
                    if (!s.AcceptEventRace(offer)) return "这场比赛已经约不上了。";
                    s.Meet(Zhixia, 16);
                    return "已记入日程。" + s.EventRaceSummary(offer);
                }));
            if (choices.Count == 0) return null;
            choices.Add(Choose("这次不参加", _ => "沈知夏说下次聚会再叫你。"));
            return EventScene(template, "沈知夏在约车友小聚。可以到店吃饭或喝点东西，到场再点单。"
                + (offer == null ? "" : "\n也可以约正式比赛：" + EventRaceSummary(offer)), choices.ToArray());
        }

        internal IEnumerable<GarageVehicle> EventTuningVehicles() => _garage.Where(v => RacingAccessBlocker == null && v.Condition < 100 && MotorAvailable(v)
            && MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind)).OrderBy(v => v.Condition);

        private EventDef BindEventTuning(EventDef template)
        {
            var fee = Money.FromYuan(EventTuningFee);
            var choices = EventTuningVehicles().Select(vehicle => Choose(
                $"{MotorCatalog.Get(vehicle.ModelId).Name} · {vehicle.Condition:0.#}% → {Math.Min(100, vehicle.Condition + 15):0.#}%",
                s =>
                {
                    if (!s.EventTuningVehicles().Contains(vehicle)
                        || !s.Ledger.TryDebit(s.Clock.Today, "车辆整备", fee, MotorCatalog.Get(vehicle.ModelId).Name))
                        return "车况、车辆状态或现金已变化，未进行整备。";
                    double repaired = Math.Min(100, vehicle.Condition + 15);
                    vehicle.Condition = repaired;
                    s.AdvanceClock(EventTuningMinutes);
                    s.AddRelation(Jingxing, 6);
                    s.Player.Intelligence = Math.Min(100, s.Player.Intelligence + 1);
                    return $"陆景行完成整备，车况恢复至 {repaired:0.#}%。交情 +6，智力 +1。";
                }, s => s.Ledger.CanAfford(fee), "现金不足")).ToList();
            if (choices.Count == 0) return null;
            choices.Add(Choose("这次先不整备", _ => "陆景行说，需要时再找他。"));
            return EventScene(template, $"陆景行可以整备一辆车。费用 {fee}，用时 {EventTuningMinutes} 分钟，最多恢复 15 点车况。", choices.ToArray());
        }

        internal bool CanOfferCollectorDrive => RacingAccessBlocker == null && DistrictAccessTier >= 3
            && City.HasPlace(CollectorTrack) && CityPersonState(Ruoning) != null
            && !Flags.Contains("event:collector-trial-complete") && _collectorTrialThrough < Clock.Today.TotalDays;

        private EventDef BindCollectorDrive(EventDef template)
        {
            if (!CanOfferCollectorDrive) return null;
            return EventScene(template, $"顾若宁想请你到{City.GetPlace(CollectorTrack).Name}试驾她的{MotorCatalog.Get(CollectorModel).Name}。她在场时可以借你跑一圈计时练习。",
                Choose("接受试驾邀请", s =>
                {
                    s._collectorTrialThrough = s.Clock.Today.TotalDays + 7;
                    return $"试驾已记入日程，有效至 {new GameDate(s._collectorTrialThrough)}。她白天在赛场时可从接待处开始。";
                }),
                Choose("这次先不试", _ => "顾若宁收起钥匙，说以后有机会再试。"));
        }

        public bool HasCollectorTrial => _collectorTrialThrough >= Clock.Today.TotalDays;
        public string CollectorTrialBlocker => !HasCollectorTrial ? "没有有效的试驾邀请"
            : Racing || IsWorking || _sleeping || IsGameOver || PendingEvent != null || CityActivityExclusive ? "请先完成当前活动"
            : CurrentPlace?.Id != CollectorTrack || !CitySamePlace(Ruoning) ? "请在顾若宁到场时到赛场接待处"
            : !RaceTracks.Get(CollectorTrack).IsOpenAt(Clock.MinuteOfDay) ? "赛场当前关闭" : null;

        public bool StartCollectorTrial()
        {
            if (CollectorTrialBlocker is string reason) return Fail(reason);
            _collectorTrialDriving = true;
            StartDriving(new RaceSimulation(RaceTracks.Get(CollectorTrack, City), MotorCatalog.Get(CollectorModel),
                true, lapLimit: 1), null);
            return true;
        }

        private string FinishCollectorTrial(RaceSimulation race)
        {
            _collectorTrialDriving = false;
            double seconds = race.Elapsed + race.Penalty;
            double target = race.Length / (race.Model.Speed / 7.2) * 3 + 15;
            bool clean = !race.Abandoned && race.Collisions == 0 && seconds <= target;
            int before = Relation(Ruoning);
            if (!race.Abandoned)
            {
                Meet(Ruoning, 8);
                AddRelation(Ruoning, clean ? 6 : 3);
                Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
                Flags.Add("event:collector-trial-complete");
                _collectorTrialThrough = -1;
            }
            Player.SpendEnergy(8);
            LastRaceSettlement = new RaceSettlement { Track = race.Track.Name, Vehicle = race.Model.Name, Practice = true,
                Abandoned = race.Abandoned, Seconds = seconds, Penalty = race.Penalty, Collisions = race.Collisions,
                Competitors = 1, Condition = Math.Max(0, 100 - race.Damage),
                Relationships = race.Abandoned ? Array.Empty<RaceRelationshipChange>() :
                    new[] { new RaceRelationshipChange { PersonId = Ruoning, Name = "顾若宁", Before = before, After = Relation(Ruoning) } },
                Extra = race.Abandoned ? "已把车交还顾若宁，邀请有效期内还可再试。" : clean
                    ? "顾若宁说你这一圈跑得又稳又快，车已交还。" : "顾若宁看完用时和碰撞记录，和你聊了聊驾驶感受。车已交还。" };
            LastRaceReport = LastRaceSettlement.Extra;
            AdvanceClock(30);
            return LastRaceReport;
        }

        private bool TryShowMingAfterRace()
        {
            if (!_mingAfterRace || EventRaised == null) return false;
            _mingAfterRace = false;
            if (PersonHasWork(Ming)) return false;
            Raise(EventScene(new EventDef { Id = "ming-race-meal", Title = "许明想赛后吃顿饭" },
                "跑完后，许明问要不要找个地方吃饭，聊聊这场比赛。",
                Choose("约个饭局", s => s.ScheduleLegacyCityMeeting(Ming, "dinner", "legacy-ming-race-meal:" + s.Clock.Today.TotalDays)),
                Choose("下次吃饭我请客", s => s.ScheduleLegacyCityMeeting(Ming, "dinner", "legacy-ming-race-meal:" + s.Clock.Today.TotalDays, playerTreat: true)),
                Choose("下次再说", _ => "你们聊了两句赛况，各自回去了。")), Clock.Today);
            return true;
        }

        private void AddEventRaceSchedule(List<ScheduleEntry> entries)
        {
            foreach (var group in RaceInvitations.Where(i => City.HasPlace(i.TrackId)).GroupBy(i => (i.Day, i.TrackId)))
            {
                var day = new GameDate(group.Key.Day);
                if (Clock.Now.TotalMinutes >= (long)day.TotalDays * 1440 + RacingSchedule.MeetCloseMinute) continue;
                string companions = string.Join("、", group.Select(i => CityPerson(i.PersonId)?.Name ?? i.PersonId));
                entries.Add(new ScheduleEntry { Minute = (long)day.TotalDays * 1440 + RacingSchedule.MeetOpenMinute,
                    Day = DescribeDay(day.TotalDays), TimeText = "09:00–18:00", Kind = "race_invite", DestinationId = group.Key.TrackId,
                    Text = $"{RacingSchedule.Name(RacingSchedule.On(day))} · {City.GetPlace(group.Key.TrackId).Name} · 和{companions}参赛 · 报名 ¥{RacingSchedule.Fee(RacingSchedule.On(day)):N0}" });
            }
            if (HasCollectorTrial && City.HasPlace(CollectorTrack))
                entries.Add(new ScheduleEntry { Minute = (long)_collectorTrialThrough * 1440, Day = DescribeDay(_collectorTrialThrough),
                    HasTime = false, Kind = "collector_trial", DestinationId = CollectorTrack,
                    Text = "顾若宁的收藏车试驾 · 邀请截止日 · 可提前到场" });
        }
    }
}
