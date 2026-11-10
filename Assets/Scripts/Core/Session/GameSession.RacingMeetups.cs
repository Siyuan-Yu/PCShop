using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class RaceInvitation
    {
        public string PersonId, TrackId;
        public int Day;
        public RaceInvitation Copy() => (RaceInvitation)MemberwiseClone();
    }

    public sealed class RaceOpponent
    {
        public string PersonId;
        public MotorModel Motor;
        public double Pace;
    }

    public sealed class RaceStanding
    {
        public string PersonId, Name, Vehicle;
        public int Place;
        public double Seconds;
        public bool Player, Invited;
    }

    public sealed class RaceRelationshipChange
    {
        public string PersonId, Name;
        public int Before, After;
        public bool Invited;
    }

    public sealed class RaceSettlement
    {
        public string Track, Vehicle;
        public RaceMeeting Meeting;
        public bool Practice, Abandoned, PersonalBest;
        public int Position, Competitors, Collisions;
        public long Prize;
        public double Seconds, Penalty, Condition, Injury;
        public RaceStanding[] Standings = Array.Empty<RaceStanding>();
        public RaceRelationshipChange[] Relationships = Array.Empty<RaceRelationshipChange>();
    }

    public sealed partial class GameSession
    {
        private readonly List<RaceInvitation> _raceInvitations = new List<RaceInvitation>();
        public RaceSettlement LastRaceSettlement { get; private set; }

        public static bool LikesRacing(string personId) => Array.IndexOf(CityLifeCatalog.RacingEnthusiasts, personId) >= 0;
        public IReadOnlyList<RaceInvitation> RaceInvitations => _raceInvitations.Where(i => i.Day >= Clock.Today.TotalDays)
            .OrderBy(i => i.Day).Select(i => i.Copy()).ToArray();

        private static long RaceDayStart(int day) => day * 1440L;
        private RaceInvitation RaceInviteFor(string personId, int day) => _raceInvitations.Find(i => i.Day == day && i.PersonId == personId);

        public string RaceInviteBlocker(string personId, string trackId, GameDate day)
        {
            var track = RaceTracks.Get(trackId);
            var person = CityPersonState(personId);
            if (IsGameOver || Racing) return "请先结束当前比赛";
            if (person == null || !LikesRacing(personId)) return "对方平时不参加赛车";
            if (!IsMet(personId) && !CitySamePlace(personId)) return "请先当面交谈或交换联系方式";
            if (track == null || !City.HasPlace(trackId) || DistrictAccessTier < track.Tier) return "赛场还未开放";
            if (RacingWins < track.RequiredWins) return "这座赛场的正赛需要累计 " + track.RequiredWins + " 胜";
            int date = day.TotalDays;
            long start = RaceDayStart(date) + RacingSchedule.MeetOpenMinute;
            long end = RaceDayStart(date) + RacingSchedule.MeetCloseMinute;
            if (RacingSchedule.On(day) == RaceMeeting.None || end <= Clock.Now.TotalMinutes) return "请选择尚未结束的正式场次";
            var booked = RaceInviteFor(personId, date);
            if (booked != null) return booked.TrackId == trackId ? "已经约好这场比赛" : "对方当天已经约好去另一个赛场";
            if (_raceInvitations.Count(i => i.Day == date && i.TrackId == trackId) >= RaceSimulation.RivalCount) return "这场比赛的邀请席位已满";
            long depart = RaceDayStart(date) + RacingSchedule.DriverDepartureMinute;
            var employee = Employees.FirstOrDefault(e => PersonIdOf(e.Person) == personId)?.Person;
            if (employee != null && EmployeeShiftOverlaps(employee, depart, end)) return "对方当天要为你的店值班";
            if (_candidates.Any(c => PersonIdOf(c) == personId && c.CandidateLeavesAt > depart)) return "对方还在等店铺面试";
            if (_cityActivities.Any(a => a.Active && a.Participants.Any(p => p.PersonId == personId)
                && (a.OccupiedUntil > a.ScheduledStart ? a.OccupiedFrom : a.ScheduledStart) < end
                && (a.OccupiedUntil > a.ScheduledStart ? a.OccupiedUntil : a.ScheduledStart + a.Duration) > depart))
                return "对方当天已有其他活动";
            if (_district.MeetingAt >= depart && _district.MeetingAt < end && _district.MeetingPeople?.Contains(personId) == true)
                return "对方当天有主线会面";
            if (person.LocationPlaceId != trackId || person.Route != null)
            {
                var route = PlanCityRoute(person, trackId, Clock.Now.TotalMinutes, start);
                if (route == null || Math.Max(Clock.Now.TotalMinutes, depart) + route.ArriveMinute - route.DepartMinute >= end)
                    return "对方来不及在比赛结束前到达";
            }
            return null;
        }

        public bool InviteRaceOpponent(string personId, string trackId, GameDate day)
        {
            string why = RaceInviteBlocker(personId, trackId, day);
            if (why != null) return Fail(why);
            _raceInvitations.RemoveAll(i => i.Day < Clock.Today.TotalDays);
            _raceInvitations.Add(new RaceInvitation { PersonId = personId, TrackId = trackId, Day = day.TotalDays });
            Say(CityPerson(personId).Name + "接受邀请，将参加 " + day + " 的" + RaceTracks.Get(trackId).Name + "比赛。");
            return true;
        }

        /// <summary>事件里约对方参加下一场正式比赛；约不成时返回原因（不弹窗、不扣任何东西）。</summary>
        internal string EventRaceInvite(string personId, string trackId)
        {
            string name = CityPerson(personId)?.Name ?? personId;
            var day = RacingSchedule.On(Clock.Today) != RaceMeeting.None && Clock.MinuteOfDay < RacingSchedule.MeetOpenMinute ? Clock.Today : RacingSchedule.Next(Clock.Today);
            string why = RaceInviteBlocker(personId, trackId, day);
            if (why != null) return "（" + name + "的赛车邀约这次没约成：" + why + "。）";
            _raceInvitations.RemoveAll(i => i.Day < Clock.Today.TotalDays);
            _raceInvitations.Add(new RaceInvitation { PersonId = personId, TrackId = trackId, Day = day.TotalDays });
            return name + "约好和你一起参加 " + day + " 的" + RaceTracks.Get(trackId).Name + "比赛，记得去报名。";
        }

        public bool CancelRaceInvitation(string personId, string trackId, int day)
            => !Racing && _raceInvitations.RemoveAll(i => i.PersonId == personId && i.TrackId == trackId && i.Day == day) > 0;

        private bool RaceInviteOverlaps(string personId, long from, long until)
            => _raceInvitations.Any(i => i.PersonId == personId
                && from < RaceDayStart(i.Day) + RacingSchedule.MeetCloseMinute
                && until > RaceDayStart(i.Day) + RacingSchedule.DriverDepartureMinute);

        private string RaceDestination(string personId, GameDate day)
        {
            int driver = Array.IndexOf(CityLifeCatalog.RacingEnthusiasts, personId);
            if (driver < 0 || RacingSchedule.On(day) == RaceMeeting.None) return null;
            var invited = RaceInviteFor(personId, day.TotalDays);
            if (invited != null) return invited.TrackId;
            if (driver < RaceTracks.All.Length) return RaceTracks.All[driver].Id;
            // Rotate the hobbyists between open districts. Each person has one destination for the day.
            int districts = Math.Min(RaceTracks.All.Length, Math.Max(1, DistrictAccessTier + 1));
            return RaceTracks.All[(driver - RaceTracks.All.Length + day.TotalDays / 3) % districts].Id;
        }

        private bool TickRaceAttendee(CityPersonState person, long now, bool workingForPlayer)
        {
            int minute = (int)(now % 1440);
            if (workingForPlayer || minute < RacingSchedule.DriverDepartureMinute || minute >= RacingSchedule.MeetCloseMinute) return false;
            var date = new GameDate((int)(now / 1440));
            string trackId = RaceDestination(person.PersonId, date);
            if (trackId == null || !City.HasPlace(trackId)) return false;
            if (person.LocationPlaceId != trackId)
            {
                long start = RaceDayStart(date.TotalDays) + RacingSchedule.MeetOpenMinute;
                var route = PlanCityRoute(person, trackId, now, start);
                if (route == null || route.ArriveMinute >= RaceDayStart(date.TotalDays) + RacingSchedule.MeetCloseMinute) return false;
                person.Route = route;
                person.LocationPlaceId = null;
                person.Task = CityPersonTask.Travelling;
                person.TaskLabel = "前往" + RaceTracks.Get(trackId).Name + "参赛";
                return true;
            }
            person.Task = CityPersonTask.Waiting;
            person.TaskLabel = "已到赛场，准备参加" + RacingSchedule.Name(RacingSchedule.On(date));
            return true;
        }

        public bool RaceAttendeeHere(string personId, string trackId)
        {
            var person = CityPersonState(personId);
            return person != null && person.Route == null && person.LocationPlaceId == trackId
                && person.ActivityId == null && person.VisitId == null && RaceDestination(personId, Clock.Today) == trackId;
        }

        public IReadOnlyList<string> RaceParticipantIds(string trackId)
        {
            var track = RaceTracks.Get(trackId);
            if (track == null || CurrentRaceMeeting == RaceMeeting.None || Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute)
                return Array.Empty<string>();
            return CityLifeCatalog.RacingEnthusiasts.Where(id => RaceAttendeeHere(id, trackId))
                .OrderByDescending(id => RaceInviteFor(id, Clock.Today.TotalDays) != null)
                .ThenByDescending(id => id == track.Npc)
                .ThenBy(id => (ConversationSeed(id) + Clock.Today.TotalDays * 17L) % 101)
                .Take(RaceSimulation.RivalCount).ToArray();
        }

        private RaceOpponent[] BuildRaceOpponents(RaceTrack track, VehicleKind kind)
        {
            var models = MotorCatalog.All.Where(model => model.Kind == kind).OrderBy(model => model.Price).ToArray();
            // Low-tier events favor modest cars. Driving pace differs by rider, not grid position.
            int modelCount = Math.Min(models.Length, track.Tier + 2);
            return RaceParticipantIds(track.Id).Select(id =>
            {
                int seed = ConversationSeed(id);
                return new RaceOpponent { PersonId = id, Motor = models[(seed / 11) % modelCount], Pace = .54 + .025 * (seed % 9) };
            }).ToArray();
        }

        private RaceRelationshipChange[] SettleRaceRelationships(RaceSimulation race)
        {
            var changes = new List<RaceRelationshipChange>();
            foreach (var rival in race.Opponents)
            {
                string id = rival.PersonId;
                var person = CityPersonState(id);
                if (person == null || person.LastRaceTogetherDayPlusOne == Clock.Today.TotalDays + 1) continue;
                person.LastRaceTogetherDayPlusOne = Clock.Today.TotalDays + 1;
                int before = Relation(id);
                bool invited = RaceInviteFor(id, Clock.Today.TotalDays) != null;
                if (id == race.Track.Npc && !IsMet(id)) Meet(id);
                AddRelation(id, invited ? 3 : race.Position <= 3 ? 2 : 1);
                int after = Relation(id);
                if (after == before) continue;
                RememberSocial(id, "在" + race.Track.Name + "同场完赛。");
                changes.Add(new RaceRelationshipChange { PersonId = id, Name = CityPerson(id)?.Name ?? id, Before = before, After = after, Invited = invited });
            }
            return changes.ToArray();
        }

        private RaceStanding[] RaceStandings(RaceSimulation race)
        {
            double seconds = race.Elapsed + race.Penalty;
            var entries = race.Opponents.Select((rival, i) => new RaceStanding
            {
                PersonId = rival.PersonId, Name = CityPerson(rival.PersonId)?.Name ?? rival.PersonId,
                Vehicle = rival.Motor.Name, Seconds = race.RivalResultSeconds(i),
                Invited = RaceInviteFor(rival.PersonId, Clock.Today.TotalDays) != null
            }).ToList();
            entries.Add(new RaceStanding { Player = true, Name = "你", Vehicle = race.Model.Name, Seconds = seconds });
            var ordered = entries.OrderBy(entry => entry.Seconds - (entry.Player ? 1e-7 : 0)).ToArray();
            for (int i = 0; i < ordered.Length; i++) ordered[i].Place = i + 1;
            return ordered;
        }
    }
}
