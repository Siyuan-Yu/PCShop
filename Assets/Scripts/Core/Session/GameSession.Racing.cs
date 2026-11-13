using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum RacingIntroStep { GetVehicle, GetParking, ParkVehicle, Drive, Complete }

    [Serializable] public sealed class RaceRecord
    {public string Key;public double BestSeconds;public int Wins,Starts;public RaceRecord Copy()=>(RaceRecord)MemberwiseClone();}
    [Serializable] public sealed class RacingSave
    {public GarageVehicle[] Garage;public RegionalGarageLease[] RegionalGarages;public RaceRecord[] Records;public RaceInvitation[] Invitations;public UsedMotor[] Stock;public string[] BoughtLeads;public int Serial;}
    public sealed partial class SaveData {public RacingSave racing;}
    public sealed partial class GameSession
    {
        private readonly List<GarageVehicle> _garage=new List<GarageVehicle>();
        private readonly List<RaceRecord> _raceRecords=new List<RaceRecord>();
        private int _motorSerial;private string _raceVehicle;
        private int _raceCitySpeed=1;private bool _raceCityPaused;
        public RaceSimulation ActiveRace {get;private set;}
        public string LastRaceReport {get;private set;}
        public bool Racing=>ActiveRace!=null;
        public IReadOnlyList<GarageVehicle> Garage {get{return _garage.Select(v=>v.Copy()).ToArray();}}
        public IReadOnlyList<RaceRecord> RacingRecords=>_raceRecords.Select(r=>r.Copy()).ToArray();
        public int RacingWins=>_raceRecords.Sum(r=>r.Wins);
        public RaceMeeting CurrentRaceMeeting=>RacingSchedule.On(Clock.Today);
        public string RaceMeetingSummary()
        {
            var meeting=CurrentRaceMeeting;var next=RacingSchedule.Next(Clock.Today);
            return (meeting==RaceMeeting.None?"今天无比赛，可练习 · ¥"+RacingSchedule.PracticeFee+"/次":RacingSchedule.Name(meeting)+" · 09:00—18:00 · 参赛 ¥"+RacingSchedule.Fee(meeting)+" · 冠军 ¥"+RacingSchedule.ChampionPrize(meeting))
                +"\n下一场："+next+" · "+RacingSchedule.Name(RacingSchedule.On(next));
        }
        public const string StarterRaceTrackId = "race_0";
        public const string StarterRaceGarageId = "garage_0";
        public const string RacingIntroTitle = "许星澜邀请你去城郊练习场";
        private const string RacingAccessFlag = "racing:access";
        private const string RacingIntroShownFlag = "racing:intro-shown";
        public bool RacingUnlocked => Flags.Contains(RacingAccessFlag);
        public string RacingAccessBlocker => FirstChapterTrial ? "赛车场暂未开放。"
            : RacingUnlocked ? null : "赛车场暂未开放。现金超过 ¥100,000 且结清所有欠款后，会有车友邀请你。";

        public RacingIntroStep RacingIntroduction
        {
            get
            {
                if (_raceRecords.Any(record => record.Key.StartsWith(StarterRaceTrackId + ":", StringComparison.Ordinal)
                    && record.BestSeconds > 0)) return RacingIntroStep.Complete;
                bool IsStarterVehicle(GarageVehicle vehicle) =>
                    MotorCatalog.Get(vehicle.ModelId)?.Kind is VehicleKind.EBike or VehicleKind.Tricycle;
                if (!_garage.Any(IsStarterVehicle)) return RacingIntroStep.GetVehicle;
                if (!HasGarage(StarterRaceGarageId)) return RacingIntroStep.GetParking;
                return _garage.Any(vehicle => IsStarterVehicle(vehicle) && !vehicle.Commuter && vehicle.GarageId == StarterRaceGarageId)
                    ? RacingIntroStep.Drive : RacingIntroStep.ParkVehicle;
            }
        }

        private void CheckRacingAccess()
        {
            if (!RacingUnlocked && !FirstChapterTrial && !IsGameOver && Ledger.Balance > Money.FromYuan(100000)
                && TotalDebt == Money.Zero && City.HasPlace(StarterRaceTrackId))
                Flags.Add(RacingAccessFlag);
        }

        private bool TryShowRacingIntroduction()
        {
            if (!RacingUnlocked || Popup == null || !Flags.Add(RacingIntroShownFlag)) return false;
            Popup.Invoke(RacingIntroTitle,
                "「等你有空，来城郊跑两圈吧。小电驴、三轮车都行。先去旁边的车库租或买个车位，把车停进去，再到练习场选车。我白天常在那儿。」");
            return true;
        }
        private GarageVehicle NewMotor(string model)=>new GarageVehicle{Id="motor_"+(++_motorSerial),ModelId=model};
        public bool DealerSells(string modelId)
        {
            var m=MotorCatalog.Get(modelId);if(m==null||CurrentPlace?.Kind!=PlaceKind.Dealer)return false;
            return MotorCatalog.SoldBy(CurrentPlace.Id,m.Kind);
        }
        public string MotorPurchaseBlocker(string modelId,bool forCommute=true,string garageId=null)
        {
            if(!DealerSells(modelId)||!IsDealerOpen)return "请在对应车行营业时间购买";
            string why=MotorStorageBlocker(modelId,forCommute,garageId);if(why!=null)return why;
            return Ledger.CanAfford(Money.FromYuan(MotorCatalog.Get(modelId).Price))?null:"购车现金不足";
        }
        public bool BuyMotor(string modelId,bool forCommute=true,string garageId=null)
        {
            string why=MotorPurchaseBlocker(modelId,forCommute,garageId);if(why!=null)return Fail(why);
            var m=MotorCatalog.Get(modelId);
            if(!Ledger.TryDebit(Clock.Today,"购车",Money.FromYuan(m.Price),m.Name))return Fail("购车现金不足。");
            PlaceAcquiredMotor(NewMotor(modelId),forCommute,garageId);
            Say(m.Name+(m.Kind==VehicleKind.Van?"已加入员工进货车队，不占车位，不可通勤或参赛。":forCommute?"已设为通勤车，不占车库车位；参赛前需在车库改为入库车辆。":"已停入"+RegionalGarages.Get(garageId).Name+"。"));return true;
        }
        public bool MotorAvailable(GarageVehicle v)
        {
            return v!=null&&MotorCatalog.Get(v.ModelId)!=null&&MotorUsable(v);
        }
        public string MotorWorkshopBlocker(GarageVehicle v)
        {
            if(v==null||IsGameOver||Racing)return "当前不能改装";
            if(!MotorAvailable(v))return "车辆正在运输或被扣押";
            if(!v.Commuter&&MotorCatalog.Get(v.ModelId).Kind!=VehicleKind.Van&&v.GarageId!="garage_0")return "请先将车辆调入城郊车库，再到车间整备";
            bool scooter=MotorCatalog.Get(v.ModelId).Kind==VehicleKind.EBike;
            if(CurrentPlace?.Id!=(scooter?"motor_scooter_mod":"motor_cars"))return scooter?"请到顺丰改车办理":"请到湖岸汽车城整备车间办理";
            if(Clock.MinuteOfDay<540||Clock.MinuteOfDay>=1140)return "车间 9:00–19:00 营业";
            return null;
        }
        public bool UpgradeMotor(string id,int part)
        {
            var v=_garage.Find(g=>g.Id==id);if(part<0||part>3)return false;string why=MotorWorkshopBlocker(v);if(why!=null)return Fail(why);
            if(MotorCatalog.Level(v,part)>=3)return false;
            if(!Ledger.TryDebit(Clock.Today,"车辆改装",Money.FromYuan(MotorCatalog.UpgradePrice(v,part)),MotorCatalog.PartName(v,part)))return Fail("改装资金不足。");
            if(part==0)v.Motor++;else if(part==1)v.Battery++;else if(part==2)v.Tyres++;else v.Steering++;
            AdvanceClock(30);return true;
        }
        public long MotorRepairPrice(GarageVehicle v)=>(long)Math.Ceiling((100-v.Condition)*MotorCatalog.Get(v.ModelId).Price*.002);
        public bool RepairMotor(string id)
        {
            var v=_garage.Find(g=>g.Id==id);string why=MotorWorkshopBlocker(v);if(why!=null)return Fail(why);
            if(v.Condition>=100)return false;if(!Ledger.TryDebit(Clock.Today,"车辆整备",Money.FromYuan(MotorRepairPrice(v)),MotorCatalog.Get(v.ModelId).Name))return Fail("维修资金不足。");
            v.Condition=100;AdvanceClock(30);return true;
        }
        public bool SellMotor(string id)
        {
            var v=_garage.Find(g=>g.Id==id);if(v==null||Racing||IsGameOver||CurrentPlace?.Kind!=PlaceKind.Dealer||!IsDealerOpen||!MotorAvailable(v))return false;
            var m=MotorCatalog.Get(v.ModelId);if(v.Commuter&&Vehicle==m.Kind&&!SetVehicle(VehicleKind.Walk))return false;
            var price=Money.FromYuan(MotorCatalog.ResaleValue(v));
            _garage.Remove(v);
            if(price.IsPositive)Ledger.Credit(Clock.Today,"载具出售",price,m.Name);
            return true;
        }
        public string RaceBlocker(string trackId,string motorId,bool practice)
            => RaceBlocker(trackId, motorId, practice, null);
        private string RaceBlocker(string trackId,string motorId,bool practice,int? entryFee)
        {
            var t=RaceTracks.Get(trackId,City);var v=_garage.Find(g=>g.Id==motorId);
            if(RacingAccessBlocker is string accessBlocker)return accessBlocker;
            if(Racing||IsGameOver||IsWorking||_sleeping||CityActivityExclusive||PendingEvent!=null)return "请先结束当前活动";
            if(t==null||CurrentPlace?.Id!=trackId)return "请实际前往赛场";
            if(DistrictAccessTier<t.Tier)return "先解锁"+DistrictName(t.Tier)+"片区";
            if(!t.IsOpenAt(Clock.MinuteOfDay))return "赛场开放时间 "+t.OpeningHours;
            if(!practice&&CurrentRaceMeeting==RaceMeeting.None)return "今天没有比赛；下一场 "+RacingSchedule.Next(Clock.Today)+"，现在可练习 · ¥"+RacingSchedule.PracticeFee+"/次";
            if (!practice && (Clock.MinuteOfDay < RacingSchedule.MeetOpenMinute || Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute))
                return "正式比赛在比赛日 09:00—18:00 举行";
            if(v==null||!MotorAvailable(v))return "请准备一辆空闲且未被扣押的自有车辆";
            if(!MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind))return "货车仅供员工进货，不可比赛或练习";
            if(v.Commuter)return "通勤车不能比赛或练习，请先在当地车库改为入库车辆";
            string garageId=RegionalGarages.ForTrack(trackId).Id;
            if(!HasGarage(garageId)||v.GarageId!=garageId)return "只能使用本区车库的入库车辆，可付费从其他车库调入";
            if(v.Condition<40)return "车况低于40%，先去车间修复";
            if(!practice&&RacingWins<t.RequiredWins)return "需要累计赢得 "+t.RequiredWins+" 场低级别比赛";
            if(Ledger.Balance<Money.FromYuan(entryFee ?? (practice?RacingSchedule.PracticeFee:RacingSchedule.Fee(CurrentRaceMeeting))))return practice?"练习费不足，需要 ¥"+RacingSchedule.PracticeFee:"参赛费不足";
            if (!practice)
            {
                var absent = _raceInvitations.FirstOrDefault(i => i.Day == Clock.Today.TotalDays && i.TrackId == trackId
                    && !RaceAttendeeHere(i.PersonId, trackId));
                if (absent != null) return "受邀车友" + CityPerson(absent.PersonId)?.Name + "尚未到场。关闭接待窗口等待，或取消邀请后参赛。";
                if (RaceParticipantIds(trackId).Count == 0) return "还没有车友到场，关闭接待窗口稍候再来";
            }
            return null;
        }
        public bool StartRace(string trackId,string motorId,bool practice)
        {
            string why=RaceBlocker(trackId,motorId,practice);if(why!=null)return Fail(why);
            var t=RaceTracks.Get(trackId,City);var v=_garage.Find(g=>g.Id==motorId);
            var meeting=CurrentRaceMeeting;
            int fee=practice?RacingSchedule.PracticeFee:RacingSchedule.Fee(meeting);
            if(!Ledger.TryDebit(Clock.Today,practice?"赛车练习":"赛车报名",Money.FromYuan(fee),t.Name+" · "+(practice?"计时练习":RacingSchedule.Name(meeting))))return false;
            var opponents = practice ? Array.Empty<RaceOpponent>() : BuildRaceOpponents(t, MotorCatalog.Get(v.ModelId).Kind);
            StartDriving(new RaceSimulation(t,MotorCatalog.Performance(v),practice,meeting,opponents), v.Id);
            ApplyEventBatteryFailure(v);
            return true;
        }
        private void StartDriving(RaceSimulation race, string vehicleId)
        {
            _raceCitySpeed=Time.Speed;_raceCityPaused=Time.IsUserPaused;Time.SetSpeed(1);
            _raceVehicle=vehicleId;ActiveRace=race;
            race.SponsorSticker = _garage.Any(v => v.Id == vehicleId && v.SponsorRacesRemaining > 0);
            race.ProtectiveHelmet = _racingGear.Any(g => g.Equipped && g.Slot == "helmet" && g.Condition > 0);
            race.ProtectiveSuit = _racingGear.Any(g => g.Equipped && g.Slot == "suit" && g.Condition > 0);
            LastRaceReport = null;
            LastRaceSettlement = null;
        }
        public string FinishRace()
        {
            var r=ActiveRace;if(r==null||!r.Finished)return null;
            var v=_garage.Find(g=>g.Id==_raceVehicle);ActiveRace=null;_raceVehicle=null;
            Time.SetSpeed(_raceCitySpeed);Time.SetPaused(_raceCityPaused);
            if (_collectorTrialDriving) return FinishCollectorTrial(r);
            v.Condition=Math.Max(0,v.Condition-2-r.Damage);double injury=0;
            if(r.Damage>5&&Random.Chance(Math.Min(.8,r.Damage/60))){injury=Math.Min(30,5+r.Damage*.4)*(1-RacingInjuryReduction);Player.Health=Math.Max(1,Player.Health-injury);}
            Player.SpendEnergy(8);string key=r.Track.Id+":"+MotorCatalog.RaceClass(r.Model.Kind)+(_drivingEventBooking != null ? ":event:"+_drivingEventBooking.Kind : r.Practice?":practice":r.Meeting==RaceMeeting.None?":event":":race");
            var record=_raceRecords.Find(x=>x.Key==key);if(record==null){record=new RaceRecord{Key=key};_raceRecords.Add(record);}record.Starts++;
            double score = r.Elapsed + r.Penalty;
            bool best = !r.Abandoned && (record.BestSeconds <= 0 || score < record.BestSeconds);
            if (best) record.BestSeconds = score;
            int position = r.Practice || r.Abandoned ? 0 : r.Position;
            long prize = r.Meeting == RaceMeeting.None ? 0 : r.PrizeForPosition(position);
            if (position == 1 && r.Meeting != RaceMeeting.None) record.Wins++;
            if (prize > 0) Ledger.Credit(Clock.Today, "赛事奖金", Money.FromYuan(prize), r.Track.Name + " · " + RacingSchedule.Name(r.Meeting));
            if (position > 0 && !PersonHasWork(Ming) && r.Opponents.Any(o => o.PersonId == Ming)
                && RaceInviteFor(Ming, Clock.Today.TotalDays) is RaceInvitation invitation && invitation.TrackId == r.Track.Id
                && Flags.Add("ming-race-meal:" + Clock.Today.TotalDays)) _mingAfterRace = true;
            LastRaceSettlement = new RaceSettlement
            {
                Track = r.Track.Name, Vehicle = r.Model.Name, Meeting = r.Meeting, Practice = r.Practice, Abandoned = r.Abandoned,
                PersonalBest = best, Position = position, Competitors = r.CompetitorCount, Prize = prize, Seconds = score,
                Penalty = r.Penalty, Condition = v.Condition, Injury = injury, Collisions = r.Collisions,
                Standings = position > 0 ? RaceStandings(r) : Array.Empty<RaceStanding>(),
                Relationships = position > 0 ? SettleRaceRelationships(r) : Array.Empty<RaceRelationshipChange>()
            };
            string raceTitle = _drivingEventBooking?.Kind == "wet" ? "人工洒水湿地训练" : _drivingEventBooking?.Kind == "rider" ? "吴磊单圈挑战" : _drivingEventBooking?.Kind == "charity" ? "慈善赛" : null;
            LastRaceSettlement.EventTitle = raceTitle;
            LastRaceReport=(r.Abandoned?"已退赛":r.Practice?"练习完成":(raceTitle ?? RacingSchedule.Name(r.Meeting))+" · 第 "+r.Position+" / "+r.CompetitorCount+" 名")+" · 用时 "+(r.Elapsed+r.Penalty).ToString("0.00")+"秒（罚时 "+r.Penalty.ToString("0")+"秒）\n奖金 ¥"+prize.ToString("N0")+" · 车况 "+v.Condition.ToString("0")+"% · "+(injury>0?"碰撞受伤，健康 -"+injury.ToString("0"):"未受伤");
            LastRaceSettlement.Extra = SettleRacingBatchRace(r, v);
            if (!string.IsNullOrEmpty(LastRaceSettlement.Extra)) LastRaceReport += "\n" + LastRaceSettlement.Extra;
            AdvanceClock(30);return LastRaceReport;
        }
        public bool MeetRacer(string trackId)
        {
            var t=RaceTracks.Get(trackId,City);if(t==null||!RacingUnlocked||Racing||CurrentPlace?.Id!=trackId||Clock.MinuteOfDay<540||Clock.MinuteOfDay>=1080||!CitySamePlace(t.Npc))return false;
            Meet(t.Npc);string flag="race_chat:"+t.Id+":"+Clock.Today.TotalDays;if(Flags.Add(flag))AddRelation(t.Npc,2);return true;
        }
        private void SaveRacing(SaveData d)
        {d.racing=new RacingSave{Garage=_garage.Select(v=>v.Copy()).ToArray(),RegionalGarages=_regionalGarages.Select(g=>g.Copy()).ToArray(),Records=_raceRecords.Select(r=>r.Copy()).ToArray(),Invitations=RaceInvitations.ToArray(),Serial=_motorSerial,Stock=_usedMotors.Select(s=>s.Copy()).ToArray(),BoughtLeads=_boughtMotorLeads.ToArray()};}
        private void LoadRacing(SaveData d)
        {
            _garage.Clear();_regionalGarages.Clear();_raceRecords.Clear();_usedMotors.Clear();_boughtMotorLeads.Clear();_motorSerial=d.racing?.Serial??0;
            _raceInvitations.Clear();
            _raceInvitations.AddRange((d.racing?.Invitations ?? Array.Empty<RaceInvitation>())
                .Where(i => i != null && i.Day >= Clock.Today.TotalDays && LikesRacing(i.PersonId) && RaceTracks.Get(i.TrackId) != null && City.HasPlace(i.TrackId))
                .GroupBy(i => (i.PersonId, i.Day)).Select(group => group.First().Copy()));
            _regionalGarages.AddRange((d.racing?.RegionalGarages??Array.Empty<RegionalGarageLease>()).Where(g=>RegionalGarages.Get(g.PlaceId)!=null).Select(g=>g.Copy()));
            foreach(var v in d.racing?.Garage??Array.Empty<GarageVehicle>())if(MotorCatalog.Get(v.ModelId)!=null)_garage.Add(v.Copy());
            _raceRecords.AddRange((d.racing?.Records??Array.Empty<RaceRecord>()).Select(r=>r.Copy()));
            _usedMotors.AddRange((d.racing?.Stock??Array.Empty<UsedMotor>()).Where(s=>MotorCatalog.Get(s.Vehicle?.ModelId)!=null).Select(s=>s.Copy()));
            foreach(var l in d.racing?.BoughtLeads??Array.Empty<string>())_boughtMotorLeads.Add(l);
            // One-time import at the persistence boundary, never in a getter or purchase command.
            if (d.racing?.Garage == null)
            {
                foreach (int value in d.ownedVehicles ?? Array.Empty<int>())
                {
                    var kind = (VehicleKind)value;
                    var model = MotorCatalog.Legacy(kind);
                    if (model == null) continue;
                    var vehicle = NewMotor(model.Id);
                    vehicle.Commuter = MotorCatalog.CanRace(kind) && !_garage.Any(v => v.Commuter && MotorCatalog.Get(v.ModelId).Kind == kind);
                    _garage.Add(vehicle);
                }
                foreach (var kind in new[] { VehicleKind.EBike, VehicleKind.Tricycle })
                {
                    int count = kind == VehicleKind.EBike ? d.impoundedEBikes : Math.Max(d.impoundedTricycles, d.tricycleImpounded ? 1 : 0);
                    foreach (var vehicle in _garage.Where(v => MotorCatalog.Get(v.ModelId).Kind == kind).Take(Math.Max(0, count))) vehicle.Impounded = true;
                }
            }
            foreach (var vehicle in _garage.Where(v => MotorCatalog.Get(v.ModelId).Kind == VehicleKind.Van))
            { vehicle.Commuter = false; vehicle.GarageId = null; }
        }
    }
}
