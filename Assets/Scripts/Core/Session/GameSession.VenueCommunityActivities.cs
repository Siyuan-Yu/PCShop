using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum VenueCommunityKind { Fishing, Billiards, Cinema, Football, Golf, Fitness, Restaurant, Market, Racing, Basketball, Karaoke, Bar }

    public sealed class VenueCommunityService
    {
        public CityActivityDefinition Definition;
        public int Choice;
        public string Name => GameSession.VenueServiceName(Definition, Choice);
    }

    public sealed partial class GameSession
    {
        private const string VenueCommunityServicePrefix = "walk-in:venue-community:";

        private sealed class VenueCommunityProfile
        {
            public readonly VenueCommunityKind Kind;
            public readonly string Title, Funding, Exposure;
            public readonly int MinimumFee, MaximumFee, Capacity, Duration, Hour;

            public VenueCommunityProfile(VenueCommunityKind kind, string title, string funding, string exposure,
                int minimumFee, int maximumFee, int capacity, int duration, int hour)
            {
                Kind = kind;
                Title = title;
                Funding = funding;
                Exposure = exposure;
                MinimumFee = minimumFee;
                MaximumFee = maximumFee;
                Capacity = capacity;
                Duration = duration;
                Hour = hour;
            }
        }

        private static readonly VenueCommunityProfile FishingCommunity = new(VenueCommunityKind.Fishing,
            "周末钓鱼会", "租竿、鱼饵和鱼获奖品", "回收柜台与鱼获告示", 1200, 8000, 8, 165, 10);
        private static readonly VenueCommunityProfile BilliardsCommunity = new(VenueCommunityKind.Billiards,
            "街坊台球短局", "球台与轮换场次", "前台对阵告示", 1000, 5000, 8, 105, 16);
        private static readonly VenueCommunityProfile CinemaCommunity = new(VenueCommunityKind.Cinema,
            "街坊放映场", "电影票与放映场次", "取票处与开场鸣谢", 2000, 10000, 12, 140, 16);
        private static readonly VenueCommunityProfile FootballCommunity = new(VenueCommunityKind.Football,
            "街坊五球点球赛", "器材和点球轮次", "球场签到与比分告示", 2000, 12000, 12, 100, 15);
        private static readonly VenueCommunityProfile GolfCommunity = new(VenueCommunityKind.Golf,
            "近旗挑战", "练习球与球位", "休息区与成绩告示", 3000, 15000, 8, 165, 14);
        private static readonly VenueCommunityProfile FitnessCommunity = new(VenueCommunityKind.Fitness,
            "街坊健身小组", "器材、饮水和体验名额", "健身场签到板", 1000, 5000, 10, 85, 10);
        private static readonly VenueCommunityProfile RestaurantCommunity = new(VenueCommunityKind.Restaurant,
            "街坊试菜小桌", "试吃食材和座位", "接待处与试吃菜单", 1000, 8000, 8, 100, 17);
        private static readonly VenueCommunityProfile MarketCommunity = new(VenueCommunityKind.Market,
            "旧物主题寻宝日", "淘货体验与鉴别台", "活动地图和接待柜台", 1000, 6000, 10, 105, 15);
        private static readonly VenueCommunityProfile RacePracticeCommunity = new(VenueCommunityKind.Racing,
            "车友练习日", "练习时段、计时和补给", "接待与维修区鸣谢", 5000, 20000, 10, 105, 15);
        private static readonly VenueCommunityProfile RaceMeetCommunity = new(VenueCommunityKind.Racing,
            "赛道观赛与挑战日", "赛场补给与观赛席", "观赛区与赛程告示", 8000, 30000, 14, 105, 15);
        private static readonly VenueCommunityProfile BasketballCommunity = new(VenueCommunityKind.Basketball,
            "街坊篮球友谊赛", "球场、器材和报名名额", "报名告示和开场鸣谢", 2000, 12000, 16, 155, 16);
        private static readonly VenueCommunityProfile KaraokeCommunity = new(VenueCommunityKind.Karaoke,
            "街坊点歌夜", "包间与点歌名额", "点歌单署名和开场鸣谢", 1500, 8000, 12, 115, 19);
        private static readonly VenueCommunityProfile BarCommunity = new(VenueCommunityKind.Bar,
            "街坊试饮夜", "饮品材料和试饮批次", "试饮桌牌署名和现场鸣谢", 1500, 10000, 12, 95, 19);

        private static VenueCommunityProfile VenueCommunityAt(Place venue) => venue?.Id switch
        {
            FishingVenueId => FishingCommunity,
            OldMarketVenueId => MarketCommunity,
            LifeFitnessPlace => FitnessCommunity,
            CityLifeCatalog.CommunityFootballVenueId => FootballCommunity,
            CityLifeCatalog.BilliardsVenue => BilliardsCommunity,
            CityLifeCatalog.CommunityCinema or CityLifeCatalog.ParkCinema => CinemaCommunity,
            "race_0" or "race_1" => RacePracticeCommunity,
            "race_2" or "race_3" => RaceMeetCommunity,
            _ => venue?.Business switch
            {
                "venue_golf" => GolfCommunity,
                "venue_restaurant" => RestaurantCommunity,
                "venue_court" => BasketballCommunity,
                "venue_ktv" => KaraokeCommunity,
                "venue_bar" => BarCommunity,
                _ => null
            }
        };

        public VenueCommunityKind? VenueCommunityType(Place venue) => VenueCommunityAt(venue)?.Kind;
        public bool IsVenueCommunityParticipant(CityActivity activity) =>
            activity?.SourceId?.StartsWith(VenueCommunityServicePrefix, StringComparison.Ordinal) == true;

        private bool VenueCommunityOpen(Place venue, VenueCommunityProfile profile, long start, int duration)
        {
            var track = RaceTracks.Get(venue.Id);
            if (track == null) return CityVenueFits(venue, start, duration);
            var raceDay = new GameDate((int)(start / 1440));
            if (profile == RaceMeetCommunity && RacingSchedule.On(raceDay) == RaceMeeting.None) return false;
            int minute = (int)(start % 1440);
            return track.IsOpenAt(minute) && minute + duration <= track.CloseMinute;
        }

        public VenueSponsorship ActiveVenueCommunity(Place venue) =>
            venue == null ? null : _venueSponsorships.LastOrDefault(a => a.VenueId == venue.Id && !a.Completed);

        public string RegisterVenueCommunity(Place venue)
        {
            if (!HasVenueSponsorship(venue) || CurrentPlace != venue) return "请到场所现场报名。";
            if (ActiveVenueCommunity(venue) is VenueSponsorship active)
            {
                active.PlayerRegistered = true;
                return null;
            }
            if (VenueSponsorContact(venue) == null) return "现在没有工作人员可以报名。";
            var plan = PlanVenueSponsorship(venue, true);
            if (plan is not { Starts: >= 0 }) return "近期找不到足够街坊有空的时段。";
            var activity = CreateVenueCommunity(venue, plan, plan.MinimumFee);
            activity.PlayerRegistered = true;
            _venueSponsorships.Add(activity);
            Say(activity.Title + "已报名，时间已记入日程。");
            return null;
        }

        private VenueSponsorship CreateVenueCommunity(Place venue, VenueSponsorPlan plan, int fee)
        {
            int invited = plan.EstimatedGuests(fee);
            return new VenueSponsorship
            {
                Id = VenueSponsorPrefix + Guid.NewGuid().ToString("N"),
                VenueId = venue.Id, Title = plan.Title, Starts = plan.Starts, Ends = plan.Starts + plan.Duration,
                ContactId = VenueSponsorContact(venue)?.PersonId,
                DispatchedCount = invited, Guests = plan.Guests.Take(invited + 3).ToArray()
            };
        }

        public VenueCommunityService[] VenueCommunityServices(Place venue)
        {
            var kind = VenueCommunityType(venue);
            if (kind is null or VenueCommunityKind.Fitness or VenueCommunityKind.Racing)
                return Array.Empty<VenueCommunityService>();
            var regular = CityLifeCatalog.Activities.Where(a => a.VenueId == venue.Id && !a.EventOnly);
            var choices = kind switch
            {
                VenueCommunityKind.Fishing => regular.Where(a => a.Id == FishingCatalog.Passes[0].Id),
                VenueCommunityKind.Football => regular.Where(a => a.Id == CityLifeCatalog.FootballPenaltyId),
                VenueCommunityKind.Basketball or VenueCommunityKind.Golf => CityLifeCatalog.SportPasses(venue.Id).Take(1),
                VenueCommunityKind.Billiards or VenueCommunityKind.Market => regular.Take(1),
                VenueCommunityKind.Bar => regular.Where(IsBarDrinkService).Take(2),
                _ => regular.Take(4)
            };
            return choices.SelectMany(def => Enumerable.Range(0, def.ServiceCount)
                .Select(i => new VenueCommunityService { Definition = def, Choice = i })).ToArray();
        }

        public string VenueCommunityPlayBlocker(VenueSponsorship activity)
        {
            if (activity == null || !_venueSponsorships.Contains(activity) || CurrentPlace?.Id != activity.VenueId) return "请到场参加。";
            if (!activity.Running || activity.Completed) return "等街坊到齐后再开始。";
            if (IsGameOver || IsWorking || _sleeping || Racing || CityActivityExclusive) return "请先结束当前活动。";
            return null;
        }

        public static int VenueMarketTheme(long starts) =>
            (int)(starts / (7 * 1440) % OldMarketHuntCatalog.Stalls.Length);

        public int ActiveVenueMarketTheme => IsVenueCommunityParticipant(RunningCityActivity)
            && CurrentPlace?.Id == OldMarketVenueId && ActiveVenueCommunity(CurrentPlace) is VenueSponsorship activity
                ? VenueMarketTheme(activity.Starts) : 0;

        public bool BeginVenueCommunityService(VenueSponsorship activity, VenueCommunityService service)
        {
            if (VenueCommunityPlayBlocker(activity) is string blocker) return Fail(blocker);
            if (service == null || !VenueCommunityServices(CurrentPlace)
                .Any(x => x.Definition.Id == service.Definition.Id && x.Choice == service.Choice))
                return Fail("该场次不提供这项活动。");
            var def = service.Definition;
            var now = Clock.Now.TotalMinutes;
            if (!CityVenueFits(CurrentPlace, now, def.Minutes)) return Fail("距离场所打烊太近，无法开始");
            bool sponsored = activity.FeeCents > 0;
            var cost = sponsored ? Money.Zero : VenueServicePayment(def, service.Choice, 1, false);
            if (cost.IsPositive && !Ledger.CanAfford(cost)) return Fail($"现金不足，需要 {cost}");
            var kind = VenueCommunityType(CurrentPlace);
            int seats = kind is VenueCommunityKind.Cinema or VenueCommunityKind.Restaurant or VenueCommunityKind.Karaoke ? 3
                : kind is VenueCommunityKind.Football or VenueCommunityKind.Billiards or VenueCommunityKind.Basketball
                    or VenueCommunityKind.Market or VenueCommunityKind.Bar or VenueCommunityKind.Golf or VenueCommunityKind.Fishing ? 1 : 0;
            var companions = PeopleInPlace(CurrentPlace.Id).Where(p => p.ActivityId == activity.Id
                    && activity.Arrived.Contains(p.PersonId)
                    && !RaceInviteOverlaps(p.PersonId, now, now + def.Minutes))
                .OrderBy(p => activity.PlayedTogether.Contains(p.PersonId)).Take(seats).ToArray();
            if (seats == 1 && companions.Length == 0 && kind is VenueCommunityKind.Football or VenueCommunityKind.Billiards)
                return Fail("当前没有可一起参加的街坊。");

            foreach (var person in companions)
            {
                person.ActivityId = null;
                person.Task = CityPersonTask.Waiting;
            }
            var booking = ScheduleCityActivityAt(def.Id, companions.Select(p => p.PersonId), now, false,
                VenueCommunityServicePrefix + activity.Id + "/" + _nextCityActivity, null, startOnSite: true);
            if (booking != null)
            {
                booking.HostPays = sponsored;
                booking.ServiceDefinitionId = def.Id;
                booking.ServiceChoice = service.Choice;
                booking.ServiceConfirmed = true;
                PrepareVenueCompanions(CurrentPlace, booking.Id);
                TickCityActivities(now, booking.Id);
                if (booking.State == CityActivityPhase.Running)
                {
                    activity.PlayerRegistered = true;
                    activity.PlayerJoined = true;
                    activity.PlayedTogether = activity.PlayedTogether.Concat(companions.Select(p => p.PersonId)).Distinct().ToArray();
                    if (sponsored && kind == VenueCommunityKind.Market && !activity.MarketPassGranted
                        && _marketHunt.Remaining == 0 && _marketHunt.Pending == null)
                    {
                        _marketHunt.Tier = 0;
                        _marketHunt.PassSize = _marketHunt.Remaining = OldMarketHuntCatalog.PassSizes[0];
                        activity.MarketPassGranted = true;
                        Say("本场赞助包含一次基础淘货。");
                    }
                    return true;
                }
                if (booking.Active) EndCityActivity(booking, CityActivityPhase.Cancelled, "现场活动未能开始", now);
            }
            foreach (var person in companions)
                if (person.ActivityId == null && person.Route == null && person.LocationPlaceId == activity.VenueId)
                {
                    person.ActivityId = activity.Id;
                    person.Task = CityPersonTask.Participating;
                    person.TaskLabel = activity.Title;
                }
            return false;
        }

        private void ReturnVenueCommunityCompanions(CityActivity service)
        {
            if (!IsVenueCommunityParticipant(service)) return;
            string id = service.SourceId.Substring(VenueCommunityServicePrefix.Length).Split('/')[0];
            var activity = _venueSponsorships.FirstOrDefault(a => a.Id == id && a.Running && !a.Completed);
            if (activity == null) return;
            foreach (var participant in service.Participants)
            {
                var person = CityPersonState(participant.PersonId);
                if (person?.ActivityId != null || person?.Route != null || person?.LocationPlaceId != activity.VenueId) continue;
                person.ActivityId = activity.Id;
                person.Task = CityPersonTask.Participating;
                person.TaskLabel = activity.Title;
            }
        }

        public bool DoVenueFitnessRound(VenueSponsorship activity, int mode)
        {
            if (VenueCommunityPlayBlocker(activity) is string blocker) return Fail(blocker);
            if (CurrentPlace?.Id != LifeFitnessPlace || mode is < 0 or > 2 || activity.FitnessRounds >= 3)
                return Fail("本次训练当前不可开始。");
            if (Player.Energy < (mode == 1 ? 6 : 3)) return Fail("精力不足，先休息一下。");
            if (Clock.Now.TotalMinutes + 10 > activity.Ends) return Fail("本次活动快结束了。");
            AdvanceClock(10);
            if (CurrentPlace?.Id != LifeFitnessPlace || IsGameOver) return false;
            ApplyFitnessRound(mode);
            activity.FitnessRounds++;
            activity.PlayerRegistered = true;
            activity.PlayerJoined = true;
            activity.PlayerResult = $"完成训练 {activity.FitnessRounds} / 3 轮";
            if (activity.FitnessRounds == 3) Say("和街坊一起完成了三轮训练。");
            return true;
        }

        private void ApplyFitnessRound(int mode)
        {
            if (mode == 3) { Player.Energy = Math.Min(Player.MaxEnergy, Player.Energy + 3); return; }
            Player.SpendEnergy(mode == 1 ? 6 : 3);
            Player.Health = Math.Min(100, Player.Health + (mode == 1 ? 2 : 1));
        }

        private void CaptureVenueCommunityResult(CityActivity service, CityActivityPhase phase, BilliardsPlay billiards)
        {
            if (phase != CityActivityPhase.Completed || !IsVenueCommunityParticipant(service)) return;
            string id = service.SourceId.Substring(VenueCommunityServicePrefix.Length).Split('/')[0];
            var activity = _venueSponsorships.FirstOrDefault(a => a.Id == id);
            if (activity == null) return;
            if (billiards != null)
                activity.PlayerResult = $"台球比分　你 {billiards.PlayerScore} : {billiards.OpponentScore} {billiards.OpponentName}";
            else if (VenueSport(service) == "golf" && _onsitePlays.FirstOrDefault(play => play.ActivityId == service.Id) is { Round: > 0 } golf)
                activity.PlayerResult = $"击球 {golf.Round} 杆 · 最近离目标 {golf.BestGolfTargetError:0.0} 米";
            else
                activity.PlayerResult = !string.IsNullOrWhiteSpace(service.EventSettlementNote) ? service.EventSettlementNote.Trim()
                    : $"和街坊一起完成了{VenueServiceName(CityActivityService(service), service.ServiceChoice)}";
        }

        private void CaptureVenueCommunityRaceResult(RaceSimulation race, int position)
        {
            var activity = _venueSponsorships.LastOrDefault(a => a.VenueId == race.Track.Id && a.Running && a.PlayerJoined);
            if (activity == null) return;
            if (position > 0) activity.PlayerResult = $"正式比赛第 {position} / {race.CompetitorCount} 名";
            else if (race.Lap > 0 && race.BestLap < double.MaxValue)
                activity.PlayerResult = $"练习完成 {race.Lap} 圈 · 最快单圈 {race.BestLap:0.00} 秒";
        }

        public bool AttendVenueCommunityRace(VenueSponsorship activity)
        {
            if (VenueCommunityPlayBlocker(activity) is string blocker) return Fail(blocker);
            if (RaceTracks.Get(CurrentPlace?.Id) == null || RacingAccessBlocker != null) return false;
            activity.PlayerRegistered = true;
            activity.PlayerJoined = true;
            return true;
        }
    }
}
