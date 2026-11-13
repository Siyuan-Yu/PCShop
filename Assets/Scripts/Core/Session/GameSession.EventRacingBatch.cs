using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class EventRaceBooking
    {
        public string Kind, PersonId, TrackId;
        public int Day;
        public EventRaceBooking Copy() => (EventRaceBooking)MemberwiseClone();
    }

    [Serializable] public sealed class RacingGear
    {
        public string Id, Name, Slot;
        public int Condition;
        public bool Equipped;
        public RacingGear Copy() => (RacingGear)MemberwiseClone();
    }

    public sealed partial class GameSession
    {
        public const int RiderMealPrice = 2000, CharityRaceFee = 2000, CharityDonation = 500;
        public const int StickerRacePayment = 3000, StickerContractRaces = 5, EventHelmetPrice = 2500;
        private const string Rider = "res_rider";
        private readonly List<EventRaceBooking> _eventRaceBookings = new List<EventRaceBooking>();
        private readonly List<RacingGear> _racingGear = new List<RacingGear>();
        private string[] _lastEventRaceOpponents = Array.Empty<string>();
        private string _raceAidPerson, _riderMealPayer;
        private int _raceAidDay = -1;
        private long _riderMealNextPrompt;
        private EventRaceBooking _drivingEventBooking;
        public IReadOnlyList<EventRaceBooking> EventRaceBookings => _eventRaceBookings
            .Where(b => b.Day >= Clock.Today.TotalDays).Select(b => b.Copy()).ToArray();
        public IReadOnlyList<RacingGear> OwnedRacingGear => _racingGear.Select(g => g.Copy()).ToArray();
        public double RacingInjuryReduction => Math.Min(.4, _racingGear.Where(g => g.Equipped)
            .GroupBy(g => g.Slot).Sum(slot => slot.Max(g => .2 * g.Condition / 100)));

        internal bool CanOfferRacingBatchEvent(string id)
        {
            if (id == "race_spectator") return !RacingUnlocked && !IsMet(Zhixia)
                && !_cityActivities.Any(a => a.Active && a.DefinitionId == "race_watch") && City.HasPlace(StarterRaceTrackId);
            if (RacingAccessBlocker != null) return false;
            switch (id)
            {
                case "race_rider_bet": return !_eventRaceBookings.Any(b => b.Kind == "rider" && b.Day >= Clock.Today.TotalDays)
                    && _riderMealPayer == null && FindEventRace(Rider) != null;
                case "race_battery_old": return _garage.Any(v => BatteryEventVehicle(v) && !v.EventBatteryWorn);
                case "race_rematch": return _lastEventRaceOpponents.Any(person => FindEventRace(person) != null);
                case "race_sponsor_sticker": return RacingWins >= 2 && _garage.Any(v => MotorAvailable(v)
                    && MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind) && v.SponsorRacesRemaining == 0);
                case "race_crash_help": return _raceAidPerson != null && _raceAidDay == Clock.Today.TotalDays;
                case "race_charity": return !_eventRaceBookings.Any(b => b.Kind == "charity" && b.Day >= Clock.Today.TotalDays)
                    && FindEventRace(Zhixia) != null;
                case "race_maker_gear": return !_racingGear.Any(g => g.Id == "maker-suit");
                case "race_night_ride": return !_cityActivities.Any(a => a.Active && a.DefinitionId == "race_night")
                    && HasNightRideVehicle;
                case "race_helmet_deal": return !_racingGear.Any(g => g.Id == "clearance-helmet");
                default: return false;
            }
        }

        private bool BatteryEventVehicle(GarageVehicle v) => MotorAvailable(v) && MotorCatalog.Get(v.ModelId).Kind == VehicleKind.EBike;
        private bool HasNightRideVehicle => _garage.Any(v => BatteryEventVehicle(v) && !v.EventBatteryWorn && v.Condition >= 40
            && (v.Commuter || v.GarageId == StarterRaceGarageId));
        public string NightRideModelId => _garage.FirstOrDefault(v => BatteryEventVehicle(v) && !v.EventBatteryWorn && v.Condition >= 40
            && (v.Commuter || v.GarageId == StarterRaceGarageId))?.ModelId;
        public long EventBatteryReplacementPrice(GarageVehicle vehicle) => Math.Max(800, (long)Math.Ceiling(MotorCatalog.Get(vehicle.ModelId).Price * .18));
        public string EventBatteryRepairBlocker(string vehicleId)
        {
            var vehicle = _garage.Find(v => v.Id == vehicleId);
            return vehicle == null || !vehicle.EventBatteryWorn ? "这辆车的电池无需更换" : MotorWorkshopBlocker(vehicle)
                ?? (!Ledger.CanAfford(Money.FromYuan(EventBatteryReplacementPrice(vehicle))) ? "更换电池现金不足" : null);
        }
        public bool ReplaceEventBattery(string vehicleId)
        {
            string reason = EventBatteryRepairBlocker(vehicleId);
            if (reason != null) return Fail(reason);
            var v = _garage.Find(g => g.Id == vehicleId);
            if (!Ledger.TryDebit(Clock.Today, "更换电池", Money.FromYuan(EventBatteryReplacementPrice(v)), MotorCatalog.Get(v.ModelId).Name)) return false;
            v.EventBatteryWorn = false;
            AdvanceClock(30);
            return true;
        }

        private EventDef BindRacingBatchEvent(EventDef template)
        {
            if (!CanOfferRacingBatchEvent(template.Id)) return null;
            var choices = new List<EventChoice>();
            string body;
            switch (template.Id)
            {
                case "race_rider_bet": return BindBookedRace(template, Rider, "rider");
                case "race_charity": return BindBookedRace(template, Zhixia, "charity");
                case "race_rematch":
                    foreach (string person in _lastEventRaceOpponents.Distinct())
                    {
                        var offer = FindEventRace(person);
                        if (offer == null) continue;
                        choices.Add(Choose($"与{CityPerson(person)?.Name}再战 · {EventRaceSummary(offer)}", s =>
                            s.AcceptEventRace(offer) ? "复赛已写入日程，按实际名次结算。" : "场次已变化，请重新联系。"));
                    }
                    body = "上次实际同场的车友约你再比一场。报名费到场支付。";
                    break;
                case "race_battery_old":
                    var batteryVehicle = _garage.First(v => BatteryEventVehicle(v) && !v.EventBatteryWorn);
                    batteryVehicle.EventBatteryWorn = true;
                    return EventScene(template, $"{MotorCatalog.Get(batteryVehicle.ModelId).Name}（{batteryVehicle.Id}）的电池已经老化，动力下降 25%。每次驾驶有 40% 概率途中断电，退赛并支付救援 ¥1,500。更换需要 ¥{EventBatteryReplacementPrice(batteryVehicle):N0}、30 分钟。",
                        Choose("前往顺丰改车更换电池", s => s.BeginCityTravel("motor_scooter_mod") ? "已导航到顺丰改车，到场后在车辆整备中更换电池。" : "电池问题已保留，请方便时自行前往顺丰改车。"),
                        Choose("先继续用，承担故障风险", _ => "电池老化状态保留，不会因为关闭事件恢复。需要时到顺丰改车更换。"));
                case "race_sponsor_sticker":
                    foreach (var v in _garage.Where(v => MotorAvailable(v) && MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind) && v.SponsorRacesRemaining == 0))
                        choices.Add(Choose($"签约{MotorCatalog.Get(v.ModelId).Name} · 5 场，每场 ¥3,000", s =>
                        {
                            if (!s.MotorAvailable(v) || v.SponsorRacesRemaining != 0) return "车辆状态已变化，未签约。";
                            v.SponsorRacesRemaining = StickerContractRaces;
                            s.Meet("keeper_phone");
                            return "手机店赞助已绑定这辆车，正式比赛完赛后逐场支付，共 5 场。练习、挑战、慈善赛和退赛不计。";
                        }));
                    body = "唐晓芸想在你的一辆赛车上贴手机店标识。只在使用签约车辆正式完赛后付费，总额 ¥15,000。";
                    break;
                case "race_crash_help":
                    return BindRaceAid(template);
                case "race_spectator":
                    return BindRacingActivity(template, "race_watch", Zhixia, "沈知夏邀请你到城郊练习场观赛，尚未开放赛车也可以赴约。实际到场看满一小时后增加见识并认识她。", false);
                case "race_night_ride":
                    return BindRacingActivity(template, "race_night", Zhixia, "沈知夏约你晚上带电动车到城郊赛场集合，沿场内安全路线休闲骑行 90 分钟，不竞速。19:00–21:30 可集合，气温低于 0℃或高于 35℃时取消，可重新约。", true);
                case "race_maker_gear":
                    body = "程弈送你一套旧护具，成色 60%。装备后按成色降低事故受伤程度；可花 ¥1,000 整修到全新。";
                    choices.Add(Choose("收下并装备旧护具", s =>
                    {
                        s.AddEventRacingGear("maker-suit", "程弈的旧护具", "suit", 60);
                        s.Meet("life_maker", 12); s.AddRelation("life_maker", 6);
                        return "护具已加入赛车装备并穿戴，身体护具位防伤 12%。可在赛车界面切换或整修。";
                    }));
                    break;
                case "race_helmet_deal":
                    body = "车友清仓一顶完好头盔，售价 ¥2,500。装备后头部防护降低 20% 的事故伤害；与身体护具合计最多降低 40%。";
                    choices.Add(Choose("买下并装备 · ¥2,500", s =>
                    {
                        if (s._racingGear.Any(g => g.Id == "clearance-helmet") || !s.Ledger.TryDebit(s.Clock.Today, "赛车装备", Money.FromYuan(EventHelmetPrice), "清仓头盔")) return "未购买。";
                        s.AddEventRacingGear("clearance-helmet", "车友清仓头盔", "helmet", 100);
                        return "头盔已入赛车装备并穿戴，不会直接恢复健康。";
                    }, s => s.Ledger.CanAfford(Money.FromYuan(EventHelmetPrice)), "现金不足 ¥2,500"));
                    break;
                default: return null;
            }
            choices.Add(Choose("这次先不参加", _ => "你婉拒了，交情不变。"));
            return EventScene(template, body, choices.ToArray());
        }

        private EventDef BindBookedRace(EventDef template, string person, string kind)
        {
            var offer = FindEventRace(person);
            if (offer == null) return null;
            bool charity = kind == "charity";
            string fee = charity ? "到场付 ¥2,000，完赛只颁纪念奖牌，不发奖金。" : "一对一实际跑一圈，无报名费。输家请 ¥2,000 夜宵，完赛后再约时间赴宴。";
            var choices = new List<EventChoice>
            {
                Choose("确认预约", s =>
                {
                    if (!s.AcceptEventRace(offer)) return "这场比赛已经约不上了。";
                    s._eventRaceBookings.Add(new EventRaceBooking { Kind = kind, Day = offer.Day, TrackId = offer.TrackId, PersonId = person });
                    s.Meet(person, 12);
                    return "已记入日程，到场接待处选车后使用专属比赛入口。" + fee;
                })
            };
            if (charity) choices.Add(Choose("只捐款 · ¥500", s =>
            {
                if (!s.Ledger.TryDebit(s.Clock.Today, "慈善捐款", Money.FromYuan(CharityDonation), "车友慈善赛")) return "现金不足，未捐款。";
                s.PlayerShop.Reputation += .3;
                return "已捐 ¥500，名望 +0.3；未预约比赛。";
            }, s => s.Ledger.CanAfford(Money.FromYuan(CharityDonation)), "现金不足 ¥500"));
            choices.Add(Choose("改天再约", _ => "这次不预约，之后有机会再约，交情不变。"));
            choices.Add(Choose("这次不参加", _ => "你婉拒了，交情不变。"));
            return EventScene(template, (charity ? "车友发起慈善赛。" : "吴磊约你比一圈。") + fee
                + "\n" + new GameDate(offer.Day) + " 09:00–18:00 · " + RaceTracks.Get(offer.TrackId).Name, choices.ToArray());
        }

        public string EventRaceStartBlocker(string kind, string vehicleId)
        {
            var booking = _eventRaceBookings.FirstOrDefault(b => b.Kind == kind && b.Day == Clock.Today.TotalDays && b.TrackId == CurrentPlace?.Id);
            if (booking == null) return "请在预约当天到指定赛场";
            string reason = RaceBlocker(booking.TrackId, vehicleId, true, 0);
            if (reason != null) return reason;
            if (Clock.MinuteOfDay < 540 || Clock.MinuteOfDay >= 1080) return "专属比赛 09:00–18:00 举行";
            if (kind != "wet" && !RaceAttendeeHere(booking.PersonId, booking.TrackId)) return "约好的车友尚未到场，请稍候";
            long fee = kind == "wet" ? WetPracticeFee : kind == "charity" ? CharityRaceFee : RiderMealPrice;
            return Ledger.CanAfford(Money.FromYuan(fee)) ? null : kind == "wet" ? "湿地训练报名需要 ¥1,000" : kind == "charity" ? "报名需要 ¥2,000" : "请预留输了之后的 ¥2,000 夜宵费用";
        }

        public bool CancelEventRaceBooking(string kind, string trackId, int day)
        {
            if (Racing) return false;
            foreach (var booking in _eventRaceBookings.Where(b => b.Kind == kind && b.TrackId == trackId && b.Day == day).ToArray())
                _raceInvitations.RemoveAll(i => i.PersonId == booking.PersonId && i.TrackId == trackId && i.Day == day);
            return _eventRaceBookings.RemoveAll(b => b.Kind == kind && b.TrackId == trackId && b.Day == day) > 0;
        }

        public bool StartEventRace(string kind, string vehicleId)
        {
            string reason = EventRaceStartBlocker(kind, vehicleId);
            if (reason != null) return Fail(reason);
            var booking = _eventRaceBookings.First(b => b.Kind == kind && b.Day == Clock.Today.TotalDays && b.TrackId == CurrentPlace.Id);
            var vehicle = _garage.Find(v => v.Id == vehicleId);
            if (kind == "wet")
            {
                if (!Ledger.TryDebit(Clock.Today, "湿地训练", Money.FromYuan(WetPracticeFee), "人工洒水驾驶体验")) return false;
                var motor = MotorCatalog.Performance(vehicle);
                var wetMotor = new MotorModel(motor.Id, motor.Name, motor.Kind, motor.Price, motor.Speed, motor.Acceleration,
                    motor.Grip * .6, motor.Handling, motor.Braking * .8, motor.Stability * .9);
                _drivingEventBooking = booking;
                var wetRace = new RaceSimulation(RaceTracks.Get(booking.TrackId, City), wetMotor, true) { WetSurface = true };
                StartDriving(wetRace, vehicle.Id); ApplyEventBatteryFailure(vehicle); return true;
            }
            if (kind == "charity" && !Ledger.TryDebit(Clock.Today, "慈善赛报名", Money.FromYuan(CharityRaceFee), "车友慈善赛")) return false;
            var track = RaceTracks.Get(booking.TrackId, City);
            var opponent = new RaceOpponent { PersonId = booking.PersonId, Motor = MotorCatalog.Performance(vehicle), Pace = .67 };
            var opponents = kind == "rider" ? new[] { opponent } : BuildRaceOpponents(track, MotorCatalog.Get(vehicle.ModelId).Kind);
            _drivingEventBooking = booking;
            StartDriving(new RaceSimulation(track, MotorCatalog.Performance(vehicle), false, RaceMeeting.None, opponents, kind == "rider" ? 1 : (int?)null), vehicle.Id);
            ApplyEventBatteryFailure(vehicle);
            return true;
        }

        private void ApplyEventBatteryFailure(GarageVehicle vehicle)
        {
            if (!vehicle.EventBatteryWorn || !Random.Chance(.4)) return;
            ActiveRace.EventBatteryFailureAt = 20 + Random.Next(0, 41);
        }

        private string SettleRacingBatchRace(RaceSimulation race, GarageVehicle vehicle)
        {
            RecordSocialBatchRace(race);
            var lines = new List<string>();
            var booking = _drivingEventBooking;
            _drivingEventBooking = null;
            bool finished = !race.Abandoned;
            if (race.EventBatteryFailed)
            {
                vehicle.Condition = Math.Max(0, vehicle.Condition - 5);
                Ledger.ForceDebit(Clock.Today, "电池断电救援", Money.FromYuan(1500), MotorCatalog.Get(vehicle.ModelId).Name);
                lines.Add("老化电池途中断电，本次退赛，救援支出 ¥1,500。请去顺丰改车更换电池。");
            }
            if (booking != null)
            {
                _eventRaceBookings.Remove(booking);
                _raceInvitations.RemoveAll(i => i.PersonId == booking.PersonId && i.Day == booking.Day && i.TrackId == booking.TrackId);
                if (finished && booking.Kind == "rider")
                {
                    _riderMealPayer = race.Position == 1 ? Rider : "player";
                    lines.Add(race.Position == 1 ? "你赢了，吴磊请 ¥2,000 夜宵。稍后约定饭局。" : "吴磊赢了，由你请 ¥2,000 夜宵。稍后约定饭局。");
                    AddRelation(Rider, 4);
                }
                else if (finished && booking.Kind == "charity")
                {
                    PlayerShop.Reputation += 1; Player.Charm = Math.Min(100, Player.Charm + 1);
                    Flags.Add("racing:charity-medal");
                    lines.Add("慈善赛完赛，获得纪念奖牌、名望 +1、魅力 +1，无现金奖金。");
                }
                else if (finished && booking.Kind == "wet")
                {
                    Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
                    lines.Add("完成湿地训练，见识 +1；车损和受伤按本次实际驾驶结算，不发普通赛事奖金。");
                }
                else lines.Add("本次退赛，不获得完赛奖励或夜宵输赢结算。");
            }
            if (finished && !race.Practice && race.Meeting != RaceMeeting.None)
            {
                _lastEventRaceOpponents = race.Opponents.Select(o => o.PersonId).Where(id => CityPersonState(id) != null).Distinct().ToArray();
                if (vehicle.SponsorRacesRemaining > 0)
                {
                    vehicle.SponsorRacesRemaining--;
                    Ledger.Credit(Clock.Today, "贴纸赞助", Money.FromYuan(StickerRacePayment), MotorCatalog.Get(vehicle.ModelId).Name);
                    lines.Add($"手机店贴纸赞助到账 ¥3,000，合同剩余 {vehicle.SponsorRacesRemaining} 场。");
                }
                if (race.Collisions > 0 && _lastEventRaceOpponents.Length > 0 && _raceAidPerson == null)
                { _raceAidPerson = _lastEventRaceOpponents[0]; _raceAidDay = Clock.Today.TotalDays; }
            }
            if (race.Collisions > 0) foreach (var gear in _racingGear.Where(g => g.Equipped)) gear.Condition = Math.Max(0, gear.Condition - Math.Min(20, race.Collisions * 2));
            return string.Join("\n", lines);
        }

        private EventDef BindRacingActivity(EventDef template, string activityId, string person, string body, bool night)
        {
            long earliest = night ? (long)Clock.Today.TotalDays * 1440 + 19 * 60 : 0;
            long start = SuggestCityActivityStart(activityId, person, earliest);
            bool available = CityActivityBlocker(activityId, new[] { person }, start) == null;
            return EventScene(template, body + "\n建议时间：" + new GameTimestamp(start),
                Choose("确认赴约", s =>
                {
                    var a = s.ScheduleCityActivity(activityId, new[] { person }, start, sourceId: "race-event:" + template.Id + ":" + s._nextCityActivity);
                    if (a == null) return "该时间约不上，未扣费、未增加属性。";
                    a.HostId = person; a.HostPays = true; a.ServiceConfirmed = true; a.ServiceChoice = 0;
                    return "已写入日程，请实际到场完成活动。";
                }, _ => available, "没有可赴约的时段"),
                Choose("改天再约", _ => "本次未预约，交情不变。"), Choose("不参加", _ => "你婉拒了，交情不变。"));
        }

        private EventDef BindRaceAid(EventDef template)
        {
            string person = _raceAidPerson;
            var hospital = City.PublicPlaces.Where(p => p.Business == HospitalBusiness && DistrictAccessBlocker(p) == null)
                .OrderBy(p => CityTravelMinutes(p)).FirstOrDefault();
            string activityId = hospital == null ? null : "race_aid_" + hospital.Id;
            long start = activityId == null ? 0 : SuggestCityActivityStart(activityId, person);
            return EventScene(template, $"赛后，刚才同场的{CityPerson(person)?.Name}在弯道边摔倒，工作人员正在处理。你可以陪同去医院，或交给工作人员。",
                Choose("陪同去医院 · 到院后支付照护 ¥1,500", s =>
                {
                    var a = s.ScheduleCityActivity(activityId, new[] { person }, start, sourceId: "race-aid:" + s._nextCityActivity);
                    if (a == null) return "陪诊未约成，请交给工作人员处理。";
                    a.ServiceConfirmed = true; a.ServiceChoice = 0;
                    s._raceAidPerson = null;
                    return "陪诊已记入日程，请实际前往医院；照护完成后增加交情，不扣你的健康。";
                }, _ => activityId != null && CityActivityBlocker(activityId, new[] { person }, start) == null, "暂无可陪诊的时段"),
                Choose("联系赛场工作人员", s => { s._raceAidPerson = null; return "工作人员接手救助，你没有额外支出或健康损失。"; }),
                Choose("先离开", s => { s._raceAidPerson = null; return "你确认工作人员已在处理后离开。"; }));
        }

        private void AddEventRacingGear(string id, string name, string slot, int condition)
        {
            if (_racingGear.Any(g => g.Id == id)) return;
            foreach (var gear in _racingGear.Where(g => g.Slot == slot)) gear.Equipped = false;
            _racingGear.Add(new RacingGear { Id = id, Name = name, Slot = slot, Condition = condition, Equipped = true });
        }
        public bool EquipRacingGear(string id, bool equipped)
        {
            var gear = _racingGear.Find(g => g.Id == id);
            if (gear == null || Racing) return false;
            if (equipped) foreach (var other in _racingGear.Where(g => g.Slot == gear.Slot)) other.Equipped = false;
            gear.Equipped = equipped; return true;
        }
        public bool RepairRacingGear(string id)
        {
            var gear = _racingGear.Find(g => g.Id == id);
            if (gear == null || gear.Condition >= 100 || Racing || IsWorking || _sleeping || CityActivityExclusive || IsGameOver) return false;
            if (!Ledger.TryDebit(Clock.Today, "护具整修", Money.FromYuan(1000), gear.Name)) return Fail("整修需要 ¥1,000");
            gear.Condition = 100; AdvanceClock(30); return true;
        }

        private bool TryShowRacingBatchFollowup()
        {
            if (EventRaised == null) return false;
            if (_riderMealPayer != null && Clock.Now.TotalMinutes >= _riderMealNextPrompt)
            {
                bool hosted = _riderMealPayer == Rider;
                Raise(EventScene(new EventDef { Id = "rider-bet-meal", Title = "兑现赛后夜宵" },
                    hosted ? "吴磊输了，这顿 ¥2,000 夜宵由他请客。" : "你输了，约好请吴磊吃 ¥2,000 夜宵。到场开始时支付。",
                    Choose("约夜宵", s =>
                    {
                        long start = s.SuggestCityActivityStart("race_bet_meal", Rider);
                        var a = s.ScheduleCityActivity("race_bet_meal", new[] { Rider }, start, sourceId: "rider-bet-meal:" + s._nextCityActivity);
                        if (a == null) { s._riderMealNextPrompt = s.Clock.Now.TotalMinutes + 180; return "这次未约成，三小时后提醒重新安排。"; }
                        a.HostId = hosted ? Rider : "player"; a.HostPays = hosted;
                        a.ServiceConfirmed = true; a.ServiceChoice = 0;
                        s._riderMealPayer = null;
                        return "夜宵已写入日程，实际赴约后结算，不额外转账。";
                    }),
                    Choose("稍后安排", s => { s._riderMealNextPrompt = s.Clock.Now.TotalMinutes + 180; return "约定保留，三小时后再提醒。"; })), Clock.Today);
                return true;
            }
            if (_raceAidPerson != null && _raceAidDay == Clock.Today.TotalDays)
            { RaiseEvent("race_crash_help"); return PendingEvent != null; }
            return false;
        }

        private void CompleteRacingBatchActivity(CityActivity activity)
        {
            if (activity.DefinitionId == "race_watch") Meet(Zhixia, Math.Max(14, Relation(Zhixia)));
        }

        private void StartRacingBatchActivity(CityActivity activity, long now)
        {
            if (activity.DefinitionId != "race_night") return;
            var track = RaceTracks.Get(activity.VenueId, City);
            var venue = City.GetPlace(activity.VenueId);
            CityRoute RideRoute(int seat)
            {
                var start = CityActivityAnchor(venue, seat);
                var points = new List<MapPoint> { start };
                for (int lap = 0; lap < 6; lap++)
                    points.AddRange(track.Points.Select(p => new MapPoint(track.X + p.X + seat * .6, track.Y + p.Y)));
                points.Add(start);
                return new CityRoute { IntentId = activity.Id + ":ride:" + seat, TargetPlaceId = venue.Id,
                    Transport = VehicleKind.EBike, Nodes = Array.Empty<string>(), DepartMinute = now, ArriveMinute = now + activity.Duration,
                    X = points.Select(p => p.X).ToArray(), Y = points.Select(p => p.Y).ToArray(),
                    Meters = points.Zip(points.Skip(1), (a, b) => DistanceCity(a, b)).ToArray() };
            }
            activity.PlayerVenueRoute = RideRoute(0);
            for (int i = 0; i < activity.Participants.Length; i++)
            {
                var participant = activity.Participants[i];
                var person = CityPersonState(participant.PersonId);
                person.Route = RideRoute(i + 1); participant.IntentId = person.Route.IntentId;
                person.TaskLabel = "沿赛场安全路线夜骑";
            }
        }

        private void TickRacingBatchEvents(GameTimestamp now)
        {
            if (_raceAidDay < now.Date.TotalDays) _raceAidPerson = null;
            _eventRaceBookings.RemoveAll(b => b.Day < now.Date.TotalDays);
            foreach (var a in _cityActivities.Where(a => a.Active && a.DefinitionId == "race_night").ToArray())
            {
                double temperature = CityOutdoorTemperature(Math.Max(now.TotalMinutes, a.ScheduledStart));
                if (temperature < 0 || temperature > 35 || !HasNightRideVehicle)
                    EndCityActivity(a, CityActivityPhase.Cancelled, "气温不适合夜骑或没有可用电动车，本次取消，可重新邀约，不扣交情。", now.TotalMinutes);
            }
        }
        private void AddRacingBatchSchedule(List<ScheduleEntry> entries)
        {
            foreach (var b in EventRaceBookings)
                entries.Add(new ScheduleEntry { Minute = (long)b.Day * 1440 + 540, Day = DescribeDay(b.Day), TimeText = "09:00–18:00",
                    Kind = "race_invite", DestinationId = b.TrackId,
                    Text = (b.Kind == "wet" ? "人工洒水湿地训练 · ¥1,000" : b.Kind == "rider" ? "吴磊一圈挑战 · 输家请 ¥2,000 夜宵" : "慈善赛 · 报名 ¥2,000，无奖金") + " · " + RaceTracks.Get(b.TrackId).Name });
        }
        private void SaveRacingBatchEvents(SaveData data)
        {
            data.eventRaceBookings = _eventRaceBookings.Select(b => b.Copy()).ToArray();
            data.racingGear = _racingGear.Select(g => g.Copy()).ToArray();
            data.lastEventRaceOpponents = _lastEventRaceOpponents.ToArray();
            data.raceAidPerson = _raceAidPerson; data.raceAidDay = _raceAidDay; data.riderMealPayer = _riderMealPayer;
            data.riderMealNextPrompt = _riderMealNextPrompt;
        }
        private void RestoreRacingBatchEvents(SaveData data)
        {
            _eventRaceBookings.Clear(); _racingGear.Clear();
            _eventRaceBookings.AddRange((data.eventRaceBookings ?? Array.Empty<EventRaceBooking>()).Where(b => b != null && City.HasPlace(b.TrackId)).Select(b => b.Copy()));
            _racingGear.AddRange((data.racingGear ?? Array.Empty<RacingGear>()).Where(g => g != null && (g.Slot == "helmet" || g.Slot == "suit")).Select(g => g.Copy()));
            foreach (var gear in _racingGear) gear.Condition = Math.Max(0, Math.Min(100, gear.Condition));
            _lastEventRaceOpponents = data.lastEventRaceOpponents ?? Array.Empty<string>();
            _raceAidPerson = string.IsNullOrEmpty(data.raceAidPerson) ? null : data.raceAidPerson;
            _raceAidDay = data.raceAidDay;
            _riderMealPayer = string.IsNullOrEmpty(data.riderMealPayer) ? null : data.riderMealPayer;
            _riderMealNextPrompt = data.riderMealNextPrompt;
        }

#if UNITY_EDITOR
        private void PrepareRacingBatchAcceptance(int number)
        {
            if (!_garage.Any(v => MotorCatalog.Get(v.ModelId).Kind == VehicleKind.EBike))
            { var v = NewMotor("e_range"); v.GarageId = StarterRaceGarageId; _garage.Add(v); }
            if (!HasGarage(StarterRaceGarageId)) _regionalGarages.Add(new RegionalGarageLease { PlaceId = StarterRaceGarageId, OwnedSpaces = 2 });
            Flags.Add(RacingAccessFlag); Flags.Add(RacingIntroShownFlag);
            if (number == 32) _garage.First(v => MotorCatalog.Get(v.ModelId).Kind == VehicleKind.EBike).EventBatteryWorn = false;
            if (number == 33) _lastEventRaceOpponents = new[] { Jingxing };
            if (number == 34 && RacingWins < 2) _raceRecords.Add(new RaceRecord { Key = "debug:event-wins", Wins = 2 });
            if (number == 35) { _raceAidPerson = Zhixia; _raceAidDay = Clock.Today.TotalDays; }
            if (number == 39)
            {
                var vehicle = _garage.First(v => MotorCatalog.Get(v.ModelId).Kind == VehicleKind.EBike);
                vehicle.EventBatteryWorn = false; vehicle.Condition = 100; vehicle.GarageId = StarterRaceGarageId;
            }
            if (number == 36)
            {
                Flags.Remove(RacingAccessFlag);
                // Keep the production acquaintance record; only a fresh acceptance scene can test first meeting.
            }
        }
#endif
    }

    public sealed partial class SaveData
    {
        public EventRaceBooking[] eventRaceBookings;
        public RacingGear[] racingGear;
        public string[] lastEventRaceOpponents;
        public string raceAidPerson, riderMealPayer;
        public int raceAidDay;
        public long riderMealNextPrompt;
    }
}
