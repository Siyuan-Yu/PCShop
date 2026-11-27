using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum OnsiteSportResult { Unrecorded, Success, Miss, Blocked }

    [Serializable] public sealed partial class OnsitePlayState
    {
        public const double BasketballFlightSeconds = 1.4;
        public const double BasketballDropSeconds = .5;
        public const double BasketballReleaseHeight = 1.52;
        public const double BasketballRimHeight = 3.05;
        public const double BasketballRimDepth = 8;
        public const double BasketballRimTolerance = .65;
        public const double BasketballThreePointRadius = 6.65;
        public const double BasketballCourtLineWidth = .072;
        public string ActivityId,Phase="ready",Pending,Message;
        public int Number,Mode,Selected,Round,Success,OpponentScore,Mask,Feedback=-1,Attempts,Base,Flavor,Sweet,Subject,PhotoCount,GoodPhotos,PhotoSubjects;
        public int[] TalkMasks=Array.Empty<int>();
        public bool MixedCorrect,ProcurementDecided,OrderOffered;
        public OnsiteSportResult LastSportResult;
        public long ReadyAt;
        public double Time,Power,Angle,Flight,Distance,LandingX,LandingZ,BallX,BallY=1.7,BallZ,Wind,TargetX,TargetZ,DefenderX,ShotOriginX;
        public double PhotoYaw=25,PhotoPitch=25,PhotoDistance=4,PhotoOffset,Exposure=1;
        public double BasketballPlayerX, BasketballDepthOffset, BasketballAimHeightOffset, BasketballLandingHeightOffset;
        public int BasketballPoints;
        public double BestGolfTargetError;
        public bool IsBasketball => Number is >= 54 and <= 56;
        public bool BasketballDrill => Number == 55 && Mode < 2 || Number == 54 && Mode == 2;
        public double BasketballOriginZ => (Mode == 1 ? 2.8 : 0) + BasketballDepthOffset;
        public double BasketballShotRange => Mode == 1 ? 8 : 13;
        public double BasketballRimDistance => Math.Sqrt(TargetX * TargetX + TargetZ * TargetZ);
        public int BasketballShotValue => BasketballRimDistance > BasketballThreePointRadius + BasketballCourtLineWidth / 2 ? 3 : 2;
        public double SportTargetError => Math.Sqrt(Math.Pow(LandingX - TargetX, 2) + Math.Pow(LandingZ - TargetZ, 2)
            + Math.Pow(BasketballLandingHeightOffset, 2));
        public bool BasketballShotBlocked
        {
            get
            {
                if (Number != 56) return false;
                if (Phase == "result") return LastSportResult == OnsiteSportResult.Blocked;
                double defenderDepth = 4.5 - BasketballOriginZ;
                if (defenderDepth <= 0 || LandingZ <= defenderDepth) return false;
                double defender = Math.Sin((Time - Flight + BasketballFlightSeconds) * 1.7 + Round) * 2;
                double offset = defender - (BasketballPlayerX + LandingX * defenderDepth / LandingZ);
                return Mode == 0 && Math.Abs(offset) < .55 || Mode == 1 && offset > 1.3 && offset < 2;
            }
        }

        public (double X, double Y, double Z) BasketballFlightPoint(double progress)
        {
            if (BasketballDrill)
            {
                double bounce = progress < 1 ? Math.Abs(Math.Sin(progress * Math.PI * (Mode == 1 ? 2 : 3))) : 0;
                return (0, .22 + bounce * 1.08, Mode == 1 ? 1.35 * Math.Sin(progress * Math.PI / 2) : 0);
            }
            bool nearRim = SportTargetError < BasketballRimTolerance;
            double x = nearRim ? TargetX : LandingX;
            double z = nearRim ? TargetZ : LandingZ;
            bool blocked = BasketballShotBlocked;
            if (blocked) z = Math.Min(z, 4.5 - BasketballOriginZ);
            double endHeight = blocked ? 1.7 : nearRim ? BasketballRimHeight
                : Math.Max(.22, BasketballRimHeight + BasketballLandingHeightOffset);
            double height = BasketballReleaseHeight + (endHeight - BasketballReleaseHeight) * progress;
            return (x * progress, height + (blocked ? .8 : 2.5) * Math.Sin(Math.PI * progress), z * progress);
        }

        public OnsitePlayState Copy(){var c=(OnsitePlayState)MemberwiseClone();c.TalkMasks=TalkMasks?.ToArray()??Array.Empty<int>();return c;}
    }
    [Serializable] public sealed class OnsitePhoto
    {
        public string Id,ActivityId,SubjectId,File; public int Score,Width,Height; public long At;
        public OnsitePhoto Copy()=>(OnsitePhoto)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        private static readonly string[] OnsiteIds={"act_bar_bartender","act_bar_taoran","act_bar_zengyu","act_court_coach","act_court_dongyu","act_court_zhang","act_golf_golfer","act_golf_enterprise"};
        private static readonly string[] OnsitePeople={"life_bartender","resident_taoran","resident_zengyu","life_coach","resident_dongyu","zhang","life_golfer","life_enterprise"};
        private static readonly string[] TastingDrinks = { "莓果气泡", "桂花乌龙", "姜汁柠檬" };
        private const double ManualMinutesPerSecond = 5;
        public static IReadOnlyList<string> DrinkTastingMenu { get; } = Array.AsReadOnly(TastingDrinks);
        private readonly List<OnsitePlayState> _onsitePlays=new List<OnsitePlayState>();
        private readonly List<OnsitePhoto> _onsitePhotos=new List<OnsitePhoto>();
        private int _onsitePhotoSequence;
        private bool _settlingManualActivity;
        public static int OnsiteNumber(string id)
        {var bits=(id??"").Split('_');return bits.Length>=2&&bits[0]=="social"&&int.TryParse(bits[1],out int n)&&(n>=51&&n<=58||n==76)?n:0;}
        private static string VenueSport(CityActivity activity)
        {
            if (activity == null) return null;
            var definition = CityLifeCatalog.Activity(string.IsNullOrEmpty(activity.ServiceDefinitionId)
                ? activity.DefinitionId : activity.ServiceDefinitionId);
            return definition?.LogicalId ?? definition?.Id;
        }
        public static bool IsVenueBasketball(CityActivity activity) => VenueSport(activity) == "court";
        public static bool IsDrinkTasting(CityActivity activity) => activity?.DefinitionId == "social_51";
        public static bool IsVenueFootball(CityActivity activity) => activity?.VenueId == CityLifeCatalog.CommunityFootballVenueId
            && VenueSport(activity) == "football";
        public static bool IsVenueSport(CityActivity activity) => IsVenueFootball(activity) || VenueSport(activity) is "court" or "golf";
        public static bool IsOnsiteBasketball(CityActivity activity) =>
            OnsiteNumber(activity?.DefinitionId) is >= 54 and <= 56 || IsVenueBasketball(activity);
        public static bool IsOnsiteSport(CityActivity activity) =>
            OnsiteNumber(activity?.DefinitionId) is >= 54 and <= 58 || IsVenueSport(activity);
        public static bool IsManualPlayActivity(CityActivity activity) =>
            activity != null && (IsInteractiveFishingActivity(activity) || IsOnsiteSport(activity)
                || IsOnsiteActivity(activity) || IsOutdoorBatchActivity(activity)
                || IsCookTasting(activity) || IsAutomaticMeal(activity) || IsRestaurantMeal(activity) || IsParkWalkActivity(activity)
                || IsOldMarketVisit(activity) || IsParkRowing(activity) || RideAt(activity) != AmusementRide.None
                || IsKaraokeActivity(activity) || IsBarVisit(activity)
                || CityLifeCatalog.LeisureService(activity) != LeisureGame.None
                || activity.DefinitionId is StreetMusicActivityId or NightSchoolActivityId);
        public bool ManualActivityClockPaused => IsManualPlayActivity(RunningCityActivity);

        public static bool IsKaraokeActivity(CityActivity activity) => activity?.DefinitionId is "ktv" or "social_48" or "social_49" or "social_50"
            || activity?.DefinitionId?.StartsWith("ktv_", StringComparison.Ordinal) == true;

        public static readonly (string Title, string Genre, int Mood, int Charm, int Intelligence)[] KaraokeSongs =
        {
            ("晚风慢一点", "舒缓抒情", 6, 0, 0),
            ("绿灯亮起来", "轻快流行", 0, 2, 0),
            ("沿河老街", "复古民谣", 0, 0, 2)
        };

        public static (int Mood, int Charm, int Intelligence) KaraokeSongRewards(int songIndex, int difficulty)
        {
            var song = KaraokeSongs[Math.Clamp(songIndex, 0, KaraokeSongs.Length - 1)];
            int halfUnits = 2 + Math.Clamp(difficulty, 0, 2);
            return (song.Mood * halfUnits / 2, song.Charm * halfUnits / 2, song.Intelligence * halfUnits / 2);
        }

        public bool SelectKaraokeSong(string id, int song, int difficulty)
        {
            var activity = RunningCityActivity;
            if (!IsKaraokeActivity(activity) || activity.Id != id || CurrentPlace?.Id != activity.VenueId
                || song < 0 || song >= KaraokeSongs.Length || difficulty is < 0 or > 2) return false;
            activity.KaraokeSongIndex = song;
            activity.KaraokeDifficulty = difficulty;
            return true;
        }

        private void ApplyKaraokeSongRewards(CityActivity activity)
        {
            if (!IsKaraokeActivity(activity) || !activity.KaraokeSang) return;
            var reward = KaraokeSongRewards(activity.KaraokeSongIndex, activity.KaraokeDifficulty);
            int Earned(int maximum) => (int)Math.Round(maximum * activity.KaraokeAccuracy, MidpointRounding.AwayFromZero);
            Player.Mood = Math.Min(100, Player.Mood + Earned(reward.Mood));
            Player.Charm = Math.Min(100, Player.Charm + Earned(reward.Charm));
            Player.Intelligence = Math.Min(100, Player.Intelligence + Earned(reward.Intelligence));
        }

        /// <summary>Advance to the playback position and return true when the outing has settled.</summary>
        public bool AdvanceKaraokePlayback(string id, double progress, string result = null, bool sang = false, double accuracy = 1)
        {
            var activity = RunningCityActivity;
            if (activity?.Id != id || !IsKaraokeActivity(activity) || _settlingManualActivity
                || CurrentPlace?.Id != activity.VenueId || IsGameOver || IsWorking || _sleeping
                || !double.IsFinite(progress) || progress < 0 || progress > 1) return false;

            if (progress == 1)
            {
                if (result != null) activity.EventSettlementNote = result;
                activity.KaraokeSang = sang;
                activity.KaraokeAccuracy = sang && double.IsFinite(accuracy) ? Math.Clamp(accuracy, 0, 1) : 0;
                return SettleManualActivity(activity, activity.Duration, CityActivityPhase.Completed,
                    CityActivityService(activity)?.EndText ?? "本次包间活动已结束。");
            }

            // Elapsed world minutes already include any playback before a save or restart.
            long remaining = activity.ActualStart + (int)(activity.Duration * progress) - Clock.Now.TotalMinutes;
            if (remaining <= 0) return false;
            _settlingManualActivity = true;
            try { AdvanceClock((int)remaining); }
            finally { _settlingManualActivity = false; }
            return false;
        }

        private double ManualOperationTime(CityActivity activity) => activity.ManualOperationMinute > 0
            ? activity.ManualOperationMinute : Clock.Now.TotalMinutes;

        public double CityPersonRouteTime(CityPersonState person)
        {
            var activity = RunningCityActivity;
            return activity != null && activity.Id == person.ActivityId && IsManualPlayActivity(activity)
                ? ManualOperationTime(activity) : Clock.Now.TotalMinutes + Time.FractionalMinute;
        }

        private void TickManualActivity(double seconds)
        {
            var activity = RunningCityActivity;
            if (_settlingManualActivity || activity == null) return;
            if (activity.VenueId != CurrentPlace?.Id || _sleeping || IsWorking)
            {
                EndCityActivity(activity, CityActivityPhase.Interrupted, "已离开活动现场，保留已完成的互动。", Clock.Now.TotalMinutes);
                return;
            }
            var onsite = Play;
            var outdoor = OutdoorState(activity);
            bool oldMarketTask = activity.VenueId == OldMarketVenueId && _oldMarket.Pending != null;
            if (onsite?.Pending == null && outdoor?.Pending == null && !oldMarketTask) return;
            double from = ManualOperationTime(activity);
            // Timed gestures and short walks use presentation time and do not tick the world.
            activity.ManualOperationMinute = from + Math.Min(seconds, TimeController.MaxTickSeconds) * ManualMinutesPerSecond;
            long minute = (long)activity.ManualOperationMinute;
            if (onsite?.Pending != null) TickOnsitePlay(minute);
            if (outdoor?.Pending != null) TickOutdoorBatch(minute);
            if (oldMarketTask)
                for (long step = (long)from + 1; step <= minute; step++) TickOldMarket(step);
        }
        public int ManualAttemptsLeft
        {
            get
            {
                var activity = RunningCityActivity;
                if (!UsesManualVenuePass(activity)) return 0;
                int used = UsedManualAttempts(activity);
                return Math.Max(0, CityLifeCatalog.ManualPlayAttempts(activity.Duration, CityActivityService(activity)) - used);
            }
        }
        private int UsedManualAttempts(CityActivity activity) => IsInteractiveFishingActivity(activity)
            ? _fishing?.ActivityId == activity.Id ? _fishing.Casts : 0
            : _onsitePlays.FirstOrDefault(state => state.ActivityId == activity.Id)?.Round ?? 0;

        private double ManualParticipationProgress(CityActivity activity, long now)
        {
            if (IsParkRowing(activity)) return _parkRowing?.ActivityId == activity.Id ? _parkRowing.Furthest : 0;
            if (IsCookTasting(activity))
            {
                var dishes = Enumerable.Range(0, CookTastingDishes.Count);
                return (dishes.Count(dish => HasTastedCooksDish(activity, dish))
                    + dishes.Count(dish => CookTastingReviewIndex(activity, dish) >= 0)) / (2d * CookTastingDishes.Count);
            }
            if (UsesManualVenuePass(activity))
                return UsedManualAttempts(activity) / (double)CityLifeCatalog.ManualPlayAttempts(activity.Duration, CityActivityService(activity));

            var onsite = _onsitePlays.FirstOrDefault(state => state.ActivityId == activity.Id);
            if (onsite != null)
            {
                return onsite.Number switch
                {
                    51 => onsite.Round / (double)TastingDrinks.Length,
                    52 => onsite.TalkMasks.Count(mask => mask != 0) / (double)Math.Max(1, activity.Participants.Length),
                    53 => onsite.Round > 0 ? 1 : onsite.Phase == "mixed" ? .5 : 0,
                    54 => Math.Min(onsite.Round, (onsite.Mask & (onsite.Mask - 1)) != 0 ? 3 : 2) / 3d,
                    55 => (Enumerable.Range(0, 3).Count(mode => (onsite.Mask & (1 << mode)) != 0) + (onsite.Feedback >= 0 ? 1 : 0)) / 4d,
                    56 or 57 => onsite.Round / 5d,
                    58 => (Math.Min(onsite.Round, 3) + (onsite.ProcurementDecided ? 1 : 0)) / 4d,
                    76 => (Math.Min(onsite.PhotoCount, 3) + Math.Min(onsite.GoodPhotos, 2)
                        + ((onsite.PhotoSubjects & (onsite.PhotoSubjects - 1)) != 0 ? 2 : onsite.PhotoSubjects != 0 ? 1 : 0)) / 7d,
                    _ => 0
                };
            }

            var outdoor = OutdoorState(activity);
            if (outdoor != null)
            {
                switch (outdoor.Number)
                {
                    case 63: return outdoor.Mode == 1 ? Math.Min(1, outdoor.Visited)
                        : (Math.Min(1, outdoor.Visited) + (activity.Choices.Contains("outdoor-work") ? 1 : 0)) / 2d;
                    case 64: return outdoor.Answers / 3d;
                    case 66 when activity.ServiceDefinitionId == "social_66_delivery": return outdoor.Delivered ? 1 : 0;
                    case 71 or 72: return (Math.Min(3, outdoor.Visited) + (outdoor.Tested ? 1 : 0)) / 4d;
                    case 74 when activity.ServiceDefinitionId == "social_74_work":
                        return (Math.Min(3, outdoor.Visited) + Math.Min(3, outdoor.Correct)) / 6d;
                    case 62 or >= 68: return outdoor.Visited / 3d;
                }
            }

            if (activity.DefinitionId == NightSchoolActivityId)
                return _lifeTasks.Any(task => task.Activity == activity.Id && task.Kind == "lesson" && task.Mode >= 0) ? 1 : 0;
            if (activity.DefinitionId == StreetMusicActivityId && _lifeTasks.Any(task => task.Activity == activity.Id && task.Kind == "music" && task.Count > 0))
                return .5;
            return (Math.Max(now, ManualOperationTime(activity)) - activity.ActualStart) / Math.Max(1, activity.Duration);
        }
        public bool CanFinishOnsiteBasketball => IsOnsiteBasketball(RunningCityActivity) && CanFinishOnsiteSport;
        public bool CanFinishOnsiteSport => IsOnsiteSport(RunningCityActivity) && OnsiteHere
            && Play is { Phase: not ("charging" or "flight") }
            && (IsVenueSport(RunningCityActivity) || OnsiteCompletionReady(RunningCityActivity));

        public bool FinishOnsiteBasketball() => CanFinishOnsiteBasketball && FinishOnsiteSport();

        public bool FinishOnsiteSport()
        {
            if (!CanFinishOnsiteSport) return false;
            var activity = RunningCityActivity;
            if (IsVenueSport(activity)) return SettleManualAttempts(activity, Play.Round);
            return FinishManualCityActivity(activity.Id);
        }

        public bool CanFinishManualCityActivity(string id)
        {
            var activity = RunningCityActivity;
            return activity?.Id == id && IsManualPlayActivity(activity) && !UsesManualVenuePass(activity) && !IsKaraokeActivity(activity)
                && CurrentPlace?.Id == activity.VenueId && !IsGameOver && !_sleeping && !IsWorking
                && (activity.VenueId != OldMarketVenueId || _oldMarket.Pending == null)
                && (!IsOldMarketVisit(activity) || _marketHunt.Pending == null && _marketHunt.Remaining == 0)
                && (!IsParkRowing(activity) || ParkRowingActive && _parkRowing.AtDock)
                && (RideAt(activity) != AmusementRide.RingToss || RingThrows(activity) >= RingsPerRound)
                && OnsiteCompletionReady(activity) && OutdoorCompletionReady(activity) && LifeActivityReady(activity)
                && CookTastingReady(activity) && (!IsCommunityMarketOuting(activity) || CommunityMarketGiftDelivered(activity));
        }

        public bool FinishManualCityActivity(string id)
        {
            if (!CanFinishManualCityActivity(id)) return false;
            var activity = RunningCityActivity;
            return IsParkRowing(activity) ? SettleParkRowing(activity)
                : SettleManualActivity(activity, activity.Duration, CityActivityPhase.Completed,
                    CityActivityService(activity)?.EndText ?? "本场已完成");
        }

        private bool SettleManualAttempts(CityActivity activity, int attempts)
        {
            int total = CityLifeCatalog.ManualPlayAttempts(activity.Duration, CityActivityService(activity));
            int minutes = activity.Duration * Math.Max(0, Math.Min(attempts, total)) / total;
            bool completed = attempts >= total || attempts > 0
                && (IsVenueSport(activity) || activity.DefinitionId == "fishing");
            return SettleManualActivity(activity, minutes, completed ? CityActivityPhase.Completed : CityActivityPhase.Interrupted,
                attempts >= total ? CityActivityService(activity)?.EndText ?? "本场已完成" : "已提前结束，按已用次数结算时间");
        }

        private bool SettleManualActivity(CityActivity activity, int minutes, CityActivityPhase phase, string reason)
        {
            if (!activity.Active || _settlingManualActivity) return false;
            _settlingManualActivity = true;
            try
            {
                long remaining = activity.ActualStart + Math.Max(minutes, MarketHuntMinutes(activity)) - Clock.Now.TotalMinutes;
                if (remaining > 0) AdvanceClock((int)remaining);
                EndCityActivity(activity, phase, reason, Clock.Now.TotalMinutes);
            }
            finally { _settlingManualActivity = false; }
            ProcessPending();
            return !activity.Active;
        }

        public static bool IsOnsiteActivity(CityActivity a)=>OnsiteNumber(a?.DefinitionId)!=0;
        private static bool OnsiteWorkingMeeting(string definition,string person)=>OnsiteNumber(definition)==58&&person=="life_enterprise"||OnsiteNumber(definition)==76&&person=="resident_ningxi";
        public OnsitePlayState OnsiteState(string id)=>_onsitePlays.FirstOrDefault(s=>s.ActivityId==id)?.Copy();
        public OnsitePlayState CurrentOnsite=>OnsiteState(RunningCityActivity?.Id);
        public OnsitePhoto[] OnsitePhotos=>_onsitePhotos.Select(x=>x.Copy()).ToArray();
        public static readonly string[] OnsitePhotoSubjects={"model:garden:greenhouse","model:rainbot:repairer","model:starlight:rover"};
        private OnsitePlayState Play=>_onsitePlays.FirstOrDefault(s=>s.ActivityId==RunningCityActivity?.Id);
        private bool OnsiteHere=>RunningCityActivity is CityActivity a&&CurrentPlace?.Id==a.VenueId&&!_sleeping&&!IsWorking&&!IsGameOver&&a.Participants.All(p=>CitySamePlace(p.PersonId));
        internal bool CanOfferOnsite(int n)
        {
            string place=n<=53?"life_bar":n<=56?"life_court":n<=58?"life_golf":"life_expo";
            return (n>=51&&n<=58||n==76)&&City.HasPlace(place)&&DistrictAccessBlocker(City.GetPlace(place))==null
                &&(n!=76||HasSmartphone)&&!_cityActivities.Any(a=>a.Active&&OnsiteNumber(a.DefinitionId)==n);
        }
        private EventDef BindOnsiteEvent(EventDef template,int n)
        {
            if(!CanOfferOnsite(n))return null;
            string place=n<=53?"life_bar":n<=56?"life_court":n<=58?"life_golf":"life_expo";
            string person=n==76?"resident_ningxi":OnsitePeople[n-51];
            string[] guests=n==52?new[]{person,"life_ning","resident_luoya"}:new[]{person};
            string definition="social_"+n;
            long Start(string def)
            {
                long first=SuggestCityActivityStartAt(def,person,0,place);
                for(int d=0;d<40*96;d++)
                {long at=first+d*15L;if(n==76&&!CityExpoDay(at,true))continue;if(CityActivityBlockerAt(def,guests,at,false,place)==null)return at;}
                return -1;
            }
            var choices=new List<EventChoice>();
            void Offer(string label,string def,bool treat=false,bool host=false)
            {
                long at=Start(def);choices.Add(Choose(label,s=>
                {
                    var a=ScheduleCityActivityAt(def,guests,at,false,"onsite:"+n+":"+_nextCityActivity,place);if(a==null)return "暂无共同空闲时段，未扣款。";
                    a.HostId=host?person:treat?"player":person;a.HostPays=host;a.PlayerTreat=treat;a.ServiceChoice=0;
                    var time = new PCShopSell.Core.Time.GameTimestamp(at);
                    return n == 56 ? $"已约好 {time} 在青湾球场会合。到场后再和张绍文商量费用。"
                        : $"已预约{time}，到场付款并实际操作，未完成现场任务不结算完成奖励。";
                },s=>at>=0,"暂无共同空闲时段"));
            }
            if(n is 51 or 53 or 76)Offer("确认赴约 · 邀请人招待",definition,host:true);
            else if (n == 56) Offer("约好去青湾球场", definition);
            else
            {
                Offer("各付自己的费用",definition);Offer("这场我请",definition,treat:true);
                if(n==52){Offer("陶然请客 · 接受邀请",definition,host:true);Offer("臻享聚会 · 各付¥1,800","social_52_premium");}
                if(n==58)Offer("私人指导 · 各付¥2,500","social_58_lesson");
            }
            choices.Add(Choose("改天再约",s=>"没有预约或扣款，也不损失交情。"));
            string body=n switch{
                51=>"夏澄请你品尝三款新饮品，店方招待。品尝时暂停世界时间，三杯尝完后结算90分钟。",
                52=>"陶然、顾宁和罗雅的聚会。普通饮品餐食¥600/人，臻享¥1,800/人；可AA、你请四人或接受陶然招待。选择具体的人和话题，按各自兴趣产生不同反馈。",
                53=>"曾玉想听听你对几种新饮品的感受。到吧台聊一聊，选一杯，等她调好后亲自尝尝。",
                54=>"郑禾的篮球训练¥1,200/人，选择定点、上篮或防守训练，按住蓄力、松开出手，实际完成三轮。",
                55=>"董宇的新地板体验¥600/人，实际体验运球、急停和跳投，再根据脚感选择反馈。",
                56=>"张绍文想约你去青湾球场打一场。答应后会记到日程里，到球场碰面再商量怎么付。",
                57=>"陶川的高尔夫练习¥2,500/人。选择铁杆、木杆或挖起杆，调整方向，按住蓄力松开击球，观察真实飞行和落点，完成五杆。",
                58=>"商务球叙¥6,800/人，也可选¥2,500/人指导。至少打三杆后选择采购话题；可明确不承诺，谈成才形成真实订单，不在答应邀约时发订单。",
                _=>"与宁汐到每月22–24日原创模型展摄影。选择展品、角度、远近和曝光，保存真实照片。至少三张照片、两种展品且两张构图合格才完成。"};
            return EventScene(template,body,choices.ToArray());
        }

        public CityActivity OnsiteActivityAt(Place place, string activityId = null)
        {
            if (place == null || place.Id != CurrentPlace?.Id) return null;
            var running = RunningCityActivity;
            if (IsOnsiteActivity(running) && running.VenueId == place.Id && (activityId == null || running.Id == activityId))
                return running;
            return FindPendingVenueActivity(place, activityId, onsiteOnly: true);
        }

        public bool OnsiteCompanionsPresent(CityActivity activity) => activity != null && activity.RendezvousReady
            && activity.VenueId == CurrentPlace?.Id
            && activity.Participants.All(p => p.Phase == CityParticipantPhase.Waiting && CitySamePlace(p.PersonId));

        public string OnsiteStartBlocker(CityActivity activity, bool? playerTreat = null)
        {
            if (activity == null || activity != OnsiteActivityAt(CurrentPlace, activity.Id)) return "请在约定时间到活动地点";
            if (IsGameOver || _sleeping || IsWorking || CityActivityExclusive) return "请先结束当前活动";
            long now = Clock.Now.TotalMinutes;
            if (now > activity.ScheduledStart + CityArrivalWindowMinutes) return "已超过约定的到场时间";
            if (NpcVenueStaffingBlocker(CurrentPlace,activity) is string staffing) return staffing;
            if (OnsiteNumber(activity.DefinitionId) == 56 && activity.Participants.Length == 0) return "这场球缺少同行者";
            if (!OnsiteCompanionsPresent(activity))
                return "等同行者到齐后开始";
            if (!CityVenueFits(CurrentPlace, now, activity.Duration)) return "距离场所打烊太近，无法开始";
            if (OnsiteNumber(activity.DefinitionId) == 76 && !CityExpoDay(now, true)) return "原创模型漫展尚未开放";
            Money cost = CityActivityEntryPrice(CityActivityService(activity), 0, activity, playerTreat);
            return cost.IsPositive && !Ledger.CanAfford(cost) ? $"现金不足，需要 {cost}" : null;
        }

        public bool StartOnsiteActivity(string id, bool? playerTreat = null)
        {
            var activity = OnsiteActivityAt(CurrentPlace, id);
            if (activity == null || activity.Id != id) return Fail("请在约定时间到活动地点");
            PrepareVenueCompanions(CurrentPlace, id);
            bool chooseAtCourt = OnsiteNumber(activity.DefinitionId) == 56;
            if (OnsiteStartBlocker(activity, chooseAtCourt ? playerTreat : null) is string reason) return Fail(reason);
            if (chooseAtCourt && !playerTreat.HasValue) return Fail("请先在球场选择怎么付场地费");
            bool previousTreat = activity.PlayerTreat;
            string previousHost = activity.HostId;
            if (chooseAtCourt)
            {
                activity.PlayerTreat = playerTreat.Value;
                activity.HostId = playerTreat.Value ? "player" : activity.Participants.First().PersonId;
            }
            activity.ServiceConfirmed = true;
            TickCityActivities(Clock.Now.TotalMinutes);
            if (activity.State == CityActivityPhase.Running) return true;
            if (activity.Active)
            {
                activity.ServiceConfirmed = false;
                activity.PlayerTreat = previousTreat;
                activity.HostId = previousHost;
            }
            return false;
        }

        private void StartOnsitePlay(CityActivity a)
        {
            int n = IsVenueFootball(a) ? OnsitePlayState.FootballPenaltyNumber
                : VenueSport(a) switch { "court" => 54, "golf" => 57, _ => OnsiteNumber(a.DefinitionId) };
            if(n==0||_onsitePlays.Any(s=>s.ActivityId==a.Id))return;
            var state=new OnsitePlayState{ActivityId=a.Id,Number=n,TalkMasks=new int[a.Participants.Length],Message="打开现场操作界面，按自己的选择完成活动。"};
            if (n is >= 54 and <= 56)
            {
                state.BallY = OnsitePlayState.BasketballReleaseHeight;
                if (n == 55) state.Mode = 2;
                state.Message = BasketballReadyMessage(state);
            }
            if (IsVenueBasketball(a)) state.Message = "对准篮筐，按住蓄力再松手投篮。";
            if (VenueSport(a) == "golf") state.Message = "选择球杆，调整方向，按住蓄力后松手击球。";
            if (n == 56) state.Message = "张绍文站到防守位置。选投篮或突破，按住蓄力再松手出球。";
            if (n == 51) state.Message = "夏澄把三杯新饮品摆到吧台上，你可以按喜欢的顺序品尝。";
            if(n==53)state.Message="曾玉朝你招招手，把几只杯子摆到了吧台上。";
            if (state.IsFootballPenalty)
            {
                ResetFootballBall(state);
                state.DefenderX = -1.1;
                state.Message = "瞄准球门，按住蓄力，松手射门。";
            }
            else SetNextOnsiteTarget(state);
            _onsitePlays.Add(state);
        }
        private void SetNextOnsiteTarget(OnsitePlayState s)
        {
            if (s.Number <= 56)
            {
                s.TargetX = -s.BasketballPlayerX;
                s.TargetZ = OnsitePlayState.BasketballRimDepth - s.BasketballOriginZ;
                s.Wind = 0;
                return;
            }
            s.TargetZ = new[] { 60.0, 85, 110 }[s.Round % 3];
            s.TargetX = new[] { 0.0, -12, 12 }[s.Round % 3];
            s.Wind = new[] { -2.0, 1.5, 3 }[s.Round % 3];
        }
        public bool SipOnsiteDrink(int drink)
        {
            var activity = RunningCityActivity;
            var tasting = Play;
            if (!IsDrinkTasting(activity) || !OnsiteHere || tasting == null || drink < 0 || drink >= TastingDrinks.Length
                || tasting.Round >= TastingDrinks.Length || (tasting.Mask & (1 << drink)) != 0) return false;

            tasting.Mask |= 1 << drink;
            tasting.Round++;
            tasting.Message = "已品尝" + TastingDrinks[drink] + "。";
            Player.Mood = Math.Min(100, Player.Mood + 2);
            if (tasting.Round == TastingDrinks.Length) FinishManualCityActivity(activity.Id);
            return true;
        }

        private void QueueOnsite(OnsitePlayState s, string kind, int minutes)
        {
            s.Pending = kind;
            s.Distance = minutes;
            s.ReadyAt = (long)ManualOperationTime(RunningCityActivity) + minutes;
            s.Message = "正在" + (kind == "chat" ? "交流" : kind == "mix" ? "调配" : "试饮") + "。";
        }
        public bool TalkOnsiteGuest(int person,int topic)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=52||s.Pending!=null||person<0||person>=s.TalkMasks.Length||topic<0||topic>2||s.TalkMasks[person]!=0)return false;
            s.Selected=person;s.Mode=topic;QueueOnsite(s,"chat",3);return true;
        }
        public bool SetOnsiteRecipe(int slot,int option)
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3||slot<0||slot>2||option<0||option>2)return false;if(slot==0)s.Base=option;else if(slot==1)s.Flavor=option;else s.Sweet=option;return true;}
        public bool MixOnsiteDrink()
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3)return false;s.MixedCorrect=s.Base==1&&s.Flavor==0&&s.Sweet==0;QueueOnsite(s,"mix",5);return true;}
        public bool ChooseTastingDialogue(int choice)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3||choice<0||choice>2)return false;
            s.Selected=choice;
            s.Base=new[]{1,2,0}[choice];s.Flavor=new[]{0,1,2}[choice];s.Sweet=new[]{0,1,2}[choice];
            if(!MixOnsiteDrink())return false;
            s.Message="曾玉：好，稍等，我来调这杯。";return true;
        }
        public bool TasteOnsiteMix()
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase!="mixed")return false;QueueOnsite(s,"taste",2);return true;}
        private static string BasketballReadyMessage(OnsitePlayState s) => s.BasketballDrill
            ? "按住蓄力，松手完成这项练习。" : "瞄准篮筐，按住蓄力，松手投篮。";
        public bool SelectOnsiteSportMode(int mode)
        {
            var s = Play;
            if (!OnsiteHere || s == null || s.Number < 54 || s.Number > 58 || s.Phase is "flight" or "charging" || mode < 0 || mode > 2) return false;
            s.Mode = mode;
            s.BasketballDepthOffset = 0;
            s.Phase = "ready";
            s.Power = 0;
            s.BallX = s.BallZ = 0;
            s.BallY = s.Number <= 56 ? OnsitePlayState.BasketballReleaseHeight : 1.7;
            if (s.Number <= 56) s.Message = BasketballReadyMessage(s);
            SetNextOnsiteTarget(s);
            return true;
        }
        public bool AimOnsiteSport(double degrees)
        {
            var s = Play;
            if (!OnsiteHere || s == null || s.Number is < 54 or > 58 || !double.IsFinite(degrees)
                || s.Phase == "flight" || s.Phase == "charging" && (s.Number > 56 || s.BasketballDrill)) return false;
            double limit = s.IsBasketball ? 180 : 50;
            s.Angle = Math.Clamp(degrees, -limit, limit);
            s.BasketballAimHeightOffset = 0;
            return true;
        }

        public void AimOnsiteBasketball(double degrees, double heightOffset)
        {
            var s = Play;
            if (s?.IsBasketball != true || s.BasketballDrill || !double.IsFinite(heightOffset)
                || !AimOnsiteSport(degrees)) return;
            s.BasketballAimHeightOffset = Math.Clamp(heightOffset, -3, 3);
        }

        public bool MoveOnsiteBasketball(double sideways, double forward)
        {
            var s = Play;
            if (!OnsiteHere || s == null || s.IsFootballPenalty || s.Number is < 54 or > 56
                || s.Phase is "charging" or "flight" || !double.IsFinite(sideways) || !double.IsFinite(forward)) return false;
            double x = Math.Clamp(s.BasketballPlayerX + sideways, -4.6, 4.6);
            double z = Math.Clamp(s.BasketballOriginZ + forward, s.Mode == 1 ? 2 : -3.5, 5.3);
            if (x == s.BasketballPlayerX && z == s.BasketballOriginZ) return false;
            s.BasketballPlayerX = x;
            s.BasketballDepthOffset = z - (s.Mode == 1 ? 2.8 : 0);
            if (s.Phase == "result")
            {
                s.Phase = "ready";
                s.Power = s.BallX = s.BallZ = 0;
                s.BallY = OnsitePlayState.BasketballReleaseHeight;
                s.Message = BasketballReadyMessage(s);
            }
            SetNextOnsiteTarget(s);
            return true;
        }
        public bool BeginOnsiteCharge()
        {
            var s = Play;
            if (!OnsiteHere || s == null || s.Phase is "flight" or "charging") return false;
            if (s.IsFootballPenalty) return BeginFootballCharge(s);
            if (s.Number is < 54 or > 58) return false;
            if (IsVenueSport(RunningCityActivity) ? ManualAttemptsLeft == 0 : s.Round >= (s.Number == 56 ? 5 : 8)) return false;
            SetNextOnsiteTarget(s);s.Phase="charging";s.Power=0;s.Flight=0;s.BallX=s.BallZ=0;
            s.BallY = s.Number <= 56 ? OnsitePlayState.BasketballReleaseHeight : 1.7;
            if (s.Number <= 56) s.Message = s.BasketballDrill ? "蓄力中，松手开始练习。" : "蓄力中，松手投篮。";
            return true;
        }

        public void CancelOnsiteBasketballCharge()
        {
            var s = Play;
            if (s?.IsBasketball != true || s.Phase != "charging") return;
            s.Phase = "ready";
            s.Flight = s.Power = 0;
            s.Message = BasketballReadyMessage(s);
        }

        public bool ReleaseOnsiteShot()
        {
            var s=Play;if(!OnsiteHere||s?.Phase!="charging")return false;
            if (s.IsFootballPenalty) return ReleaseFootballShot(s);
            s.Phase="flight";s.Flight=0;double rad=s.Angle*Math.PI/180;
            double range=s.Number<=56?s.BasketballShotRange*s.Power:new[]{100.0,180,55}[s.Mode]*s.Power*s.Power;
            s.Distance=range;s.LandingX=Math.Sin(rad)*range+s.Wind;s.LandingZ=Math.Cos(rad)*range;s.ShotOriginX=0;
            s.BasketballLandingHeightOffset = s.IsBasketball && !s.BasketballDrill ? s.BasketballAimHeightOffset : 0;
            s.Message = s.BasketballDrill ? "练习进行中…" : "球已出手，观察飞行和落点。";
            return true;
        }
        public void StepOnsiteSport(double seconds)
        {
            var s = Play;
            if (!OnsiteHere || s == null || seconds <= 0) return;
            if (s.IsFootballPenalty) { StepFootballPenalty(s, Math.Min(.1, seconds)); return; }
            if (s.Number is < 54 or > 58) return;
            double dt=Math.Min(.1,seconds);s.Time+=dt;s.DefenderX=Math.Sin(s.Time*1.7+s.Round)*2;
            if (s.Phase == "result" && s.Number <= 56 && !s.BasketballDrill && s.LastSportResult != OnsiteSportResult.Unrecorded)
            {
                double flightEnd = OnsitePlayState.BasketballFlightSeconds;
                double duration = OnsitePlayState.BasketballDropSeconds;
                if (s.Flight >= flightEnd + duration) return;
                s.Flight = Math.Min(flightEnd + duration, Math.Max(s.Flight, flightEnd) + dt);
                double drop = (s.Flight - flightEnd) / duration;
                var end = s.BasketballFlightPoint(1);
                s.BallY = end.Y + (.22 - end.Y) * drop * drop;
                double rebound = s.LastSportResult == OnsiteSportResult.Success ? 0 : .8 * drop;
                s.BallX = end.X + (end.X >= s.TargetX ? rebound : -rebound);
                s.BallZ = end.Z + .12 * drop;
            }
            else if (s.Phase == "charging")
            {
                s.Flight += dt;
                s.Power = 1 - Math.Abs(s.Flight % 2 - 1);
            }
            else if(s.Phase=="flight")
            {
                s.Flight+=dt;double duration=s.Number<=56?OnsitePlayState.BasketballFlightSeconds:2.6;double t=Math.Min(1,s.Flight/duration);
                if (s.Number <= 56) (s.BallX, s.BallY, s.BallZ) = s.BasketballFlightPoint(t);
                else
                {
                    s.BallX = s.LandingX * t;
                    s.BallY = .2 + Math.Sin(Math.PI * t) * Math.Max(3, s.Distance * .2);
                    s.BallZ = s.LandingZ * t;
                }
                if(t<1)return;
                double miss = s.SportTargetError;
                if (!s.IsBasketball && (s.Round == 0 || miss < s.BestGolfTargetError)) s.BestGolfTargetError = miss;
                bool blocked = s.BasketballShotBlocked;
                bool good=s.Number<=56?miss<OnsitePlayState.BasketballRimTolerance&&!blocked:miss<10;
                if(s.Number==55&&s.Mode<2)good=Math.Abs(s.Power-(s.Mode==0?.55:.7))<.15;
                if(s.Number==54&&s.Mode==2)good=Math.Abs(s.DefenderX)>1&&s.Power>.35&&s.Power<.8;
                // The player and target positions stay fixed from release until the ball lands.
                int points = s.IsBasketball && !s.BasketballDrill && good ? s.BasketballShotValue : 0;
                s.BasketballPoints += points;
                s.Round++;if(good)s.Success++;s.Mask|=1<<s.Mode;s.Phase="result";
                s.LastSportResult = blocked ? OnsiteSportResult.Blocked : good ? OnsiteSportResult.Success : OnsiteSportResult.Miss;
                Player.SpendEnergy(s.Number<=56?3:2);
                if(s.Number==56&&(!good||Math.Sin(s.Time*1.3)>.6))s.OpponentScore+=2;
                s.Message = s.Number >= 57 ? $"落点距目标 {miss:0.0} 米 · 实际飞行 {s.Distance:0.0} 米"
                    : s.BasketballDrill ? good ? "练习成功！" : "练习未成功，调整力度再试。"
                    : blocked ? "投篮被封盖！" : good ? $"进球！ +{points} 分" : BasketballMissMessage(s);
            }
        }

        private static string BasketballMissMessage(OnsitePlayState shot)
        {
            double sideways = shot.LandingX - shot.TargetX;
            double depth = shot.LandingZ - shot.TargetZ;
            if (Math.Abs(shot.BasketballLandingHeightOffset) > Math.Max(Math.Abs(sideways), Math.Abs(depth)))
                return shot.BasketballLandingHeightOffset < 0 ? "瞄低了。" : "瞄高了。";
            if (Math.Abs(sideways) > Math.Abs(depth)) return sideways < 0 ? "投偏左了。" : "投偏右了。";
            return depth < 0 ? "投短了。" : "投过了。";
        }

        public bool OnsiteCourtFeedback(int feedback)
        {var s=Play;if(!OnsiteHere||s?.Number!=55||s.Round<3||s.Mask!=7||feedback<0||feedback>2||s.Feedback>=0)return false;s.Feedback=feedback;s.Message="董宇记录了你对"+new[]{"抓地力","急停脚感","回弹"}[feedback]+"的实际体验。";return true;}
        public bool DecideOnsiteProcurement(int topic)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=58||s.Round<3||s.ProcurementDecided||topic<0||topic>2)return false;s.ProcurementDecided=true;
            if(topic==2){s.Message="暂不承诺供货，没有订单或收入。";return true;}
            if(!HasOpenedComputerShop){s.Message="尚未开办具备交付能力的电脑店，未接采购单。";return true;}
            double chance=EventScript.ProgressiveChance(.45,(Player.Intelligence,50,.01),(Relation("life_enterprise"),35,.008));
            if(!Random.Chance(chance)){s.Message="已讨论"+(topic==0?"用途、型号和保修":"预算、数量和交付周期")+"，暂未达成采购意向。";return true;}
            s.OrderOffered=true;s.Message=OfferCityReferralContract("onsite58:"+s.ActivityId,"云帆企业 · 沈远现场确认",1,false);return true;
        }
        public bool SetOnsitePhotoFrame(int subject,double yaw,double pitch,double distance,double offset,int exposure)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=76||subject<0||subject>=OnsitePhotoSubjects.Length||double.IsNaN(yaw)||double.IsNaN(pitch)||double.IsNaN(distance)||double.IsNaN(offset))return false;
            s.Subject=subject;s.PhotoYaw=Math.Max(-180,Math.Min(180,yaw));s.PhotoPitch=Math.Max(5,Math.Min(75,pitch));s.PhotoDistance=Math.Max(1,Math.Min(8,distance));s.PhotoOffset=Math.Max(-2,Math.Min(2,offset));s.Exposure=Math.Max(0,Math.Min(2,exposure));return true;
        }
        public int OnsitePhotoScore
        {
            get {var s=Play;if(s?.Number!=76)return 0;return Math.Max(0,100-(int)(Math.Abs(s.PhotoDistance-4)*20+Math.Abs(s.PhotoPitch-30)*.7+Math.Abs(s.PhotoOffset)*35+Math.Abs(s.Exposure-1)*30+Math.Max(0,Math.Abs(s.PhotoYaw)-90)*1.3));}
        }
        public bool RecordOnsitePhoto(string file,int width,int height)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=76||!HasSmartphone||string.IsNullOrEmpty(file)||file.Contains("..")||file.Contains("/")||file.Contains("\\")||!file.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||width<320||height<180||_onsitePhotos.Any(p=>p.File==file)||s.PhotoCount>=12)return false;
            int score=OnsitePhotoScore;_onsitePhotos.Add(new OnsitePhoto{Id="onsite-photo:"+(++_onsitePhotoSequence),ActivityId=s.ActivityId,SubjectId=OnsitePhotoSubjects[s.Subject],File=file,Width=width,Height=height,Score=score,At=Clock.Now.TotalMinutes});
            s.PhotoCount++;if(score>=60){s.GoodPhotos++;s.PhotoSubjects|=1<<s.Subject;}s.Message=$"照片已保存，构图评分{score}/100；本场{s.PhotoCount}张。";return true;
        }
        private void TickOnsitePlay(long now)
        {
            var s=Play;if(!OnsiteHere||s==null||s.Pending==null)return;
            if(s.ReadyAt==0){s.ReadyAt=now+Math.Max(1,(int)s.Distance);return;}
            if(now<s.ReadyAt)return;
            string pending=s.Pending;s.Pending=null;
            if(pending=="chat")
            {
                var a=RunningCityActivity;bool preferred=s.Mode==s.Selected%3;s.TalkMasks[s.Selected]=1<<s.Mode;AwardCityActivityRelation(a, a.Participants[s.Selected].PersonId, preferred ? 3 : 1);
                s.Message=CityPerson(a.Participants[s.Selected].PersonId)?.Name+(preferred?"对这个话题很有兴趣，分享了自己的经验。":"聊了近况，但这不是对方最感兴趣的话题。");
            }
            if(pending=="mix"){s.Phase="mixed";s.Message="曾玉：这杯好了。来，尝一口，告诉我你喝起来是什么感觉。";}
            if(pending=="taste")
            {
                s.Attempts++;s.Phase="ready";s.Round++;if(s.MixedCorrect)
                {
                    bool learned=s.Success==0;s.Success++;Player.Mood=Math.Min(100,Player.Mood+5);if(learned)Player.Intelligence=Math.Min(100,Player.Intelligence+1);
                    s.Message="入口清爽，酸甜和气泡恰到好处，味道简直完美。\n心情+5"+(learned?"，智力+1。":"。")+"\n曾玉：看你的表情，这杯你应该挺喜欢。";
                }
                else{Player.Health=Math.Max(0,Player.Health-2);Player.SpendEnergy(3);s.Message="这股味道有点奇怪，酸涩里带着说不出的苦，胃里也不太舒服。\n健康−2，精力消耗3。\n曾玉：你皱眉了？我再琢磨琢磨这杯的味道。";}
            }
        }
        private bool OnsiteCompletionReady(CityActivity a)
        {
            if (IsVenueSport(a) && a.Participants.Length > 0)
                return _onsitePlays.Any(s => s.ActivityId == a.Id && s.Round > 0);
            int n=OnsiteNumber(a.DefinitionId);if(n==0)return true;var s=_onsitePlays.FirstOrDefault(x=>x.ActivityId==a.Id);if(s==null||s.Pending!=null||s.Phase is "charging" or "flight")return false;
            return n switch{51=>s.Round==TastingDrinks.Length,52=>s.TalkMasks.Length==a.Participants.Length&&s.TalkMasks.All(x=>x!=0),53=>s.Round>=1&&s.Phase!="mixed",54=>s.Round>=3&&(s.Mask&(s.Mask-1))!=0,55=>s.Round>=3&&s.Mask==7&&s.Feedback>=0,56=>s.Round>=5,57=>s.Round>=5,58=>s.Round>=3&&s.ProcurementDecided,76=>s.PhotoCount>=3&&s.GoodPhotos>=2&&(s.PhotoSubjects&(s.PhotoSubjects-1))!=0,_=>false};
        }
        private void CompleteOnsitePlay(CityActivity a)
        {
            var s=_onsitePlays.FirstOrDefault(x=>x.ActivityId==a.Id);if(s==null)return;
            if (IsVenueSport(a))
            {
                string summary = s.IsFootballPenalty ? $"射门 {s.Round} 球 · 进球 {s.Success} 球"
                    : s.IsBasketball ? $"投篮 {s.Round} 次 · 得分 {s.BasketballPoints} 分"
                    : $"击球 {s.Round} 杆 · 命中目标 {s.Success} 次";
                a.EventSettlementNote = (a.EventSettlementNote ?? "") + "\n" + summary;
                return;
            }
            if(s.Number==52&&a.PlayerTreat)foreach(var p in a.Participants)AwardCityActivityRelation(a, p.PersonId, 2);
            if(s.Number==52&&a.HostPays)AwardCityActivityRelation(a, a.HostId, 1);
            if(s.Number>=54&&s.Number<=57&&s.Success>=2)Player.Skill=Math.Min(100,Player.Skill+1);
            if(s.Number>=54&&s.Number<=57)Player.Health=Math.Min(100,Player.Health+1);
            if(s.Number==76&&s.GoodPhotos>=3)Player.Intelligence=Math.Min(100,Player.Intelligence+1);
            if (s.Number == 51)
            {
                a.EventSettlementNote = (a.EventSettlementNote ?? "") + "\n和夏澄一起尝完了" + string.Join("、", TastingDrinks) + "。";
                return;
            }
            if (s.Number == 56)
            {
                a.EventSettlementNote = (a.EventSettlementNote ?? "") + $"\n打完五回合 · 你 {s.BasketballPoints} : {s.OpponentScore} 张绍文";
                return;
            }
            if (s.IsBasketball)
            {
                a.EventSettlementNote = (a.EventSettlementNote ?? "") + $"\n完成 {s.Round} 回合 · 投篮得分 {s.BasketballPoints} 分";
                return;
            }
            a.EventSettlementNote=(a.EventSettlementNote??"")+(s.Number==53?"\n你和曾玉在吧台尝了新饮品，也聊了各自的感受。":"\n实际回合"+s.Round+"，成功"+s.Success+"，照片"+s.PhotoCount+"。");
        }
        private void SaveOnsitePlay(SaveData d){d.onsitePlays=_onsitePlays.Select(x=>x.Copy()).ToArray();d.onsitePhotos=_onsitePhotos.Select(x=>x.Copy()).ToArray();d.onsitePhotoSequence=_onsitePhotoSequence;}
        private void RestoreOnsitePlay(SaveData d)
        {
            _onsitePlays.Clear();
            foreach (var saved in d.onsitePlays ?? Array.Empty<OnsitePlayState>())
            {
                var s = saved.Copy();
                if (string.IsNullOrEmpty(s.Pending)) s.Pending = null;
                // Earlier saves only record successes. Convert them when they all came from shots.
                bool onlyShots = s.Number switch { 54 => (s.Mask & 4) == 0, 55 => (s.Mask & 3) == 0, 56 => true, _ => false };
                if (d.cityLifeVersion < 5 && onlyShots) s.BasketballPoints = 2 * s.Success;
                _onsitePlays.Add(s);
            }
            foreach (var activity in _cityActivities.Where(a => a.Active && IsDrinkTasting(a)))
                activity.Duration = CityLifeCatalog.DrinkTastingMinutes;
            var running = RunningCityActivity;
            if (IsVenueSport(running) || IsOnsiteActivity(running)) StartOnsitePlay(running);
            if (IsVenueFootball(running) && Play is { Phase: "charging" } penalty)
            {
                penalty.Phase = "ready";
                ResetFootballBall(penalty);
                penalty.Message = "瞄准球门，按住蓄力，松手射门。";
            }
            if (IsDrinkTasting(running) && Play is { Pending: "sip" } tasting)
            {
                // A timed sip in an older save was not yet recorded as tasted.
                tasting.Pending = null;
                tasting.ReadyAt = 0;
                tasting.Message = "继续品尝吧台上剩下的饮品。";
            }
            _onsitePhotos.Clear();_onsitePhotos.AddRange((d.onsitePhotos??Array.Empty<OnsitePhoto>()).Select(x=>x.Copy()));_onsitePhotoSequence=d.onsitePhotoSequence;
        }
    }
    public sealed partial class SaveData
    {public OnsitePlayState[] onsitePlays;public OnsitePhoto[] onsitePhotos;public int onsitePhotoSequence;}
}
