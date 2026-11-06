using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class RaceRecord
    {public string Key;public double BestSeconds;public int Wins,Starts;public RaceRecord Copy()=>(RaceRecord)MemberwiseClone();}
    [Serializable] public sealed class RacingSave
    {public GarageVehicle[] Garage;public RegionalGarageLease[] RegionalGarages;public RaceRecord[] Records;public UsedMotor[] Stock;public string[] BoughtLeads;public int Serial;}
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
        public IReadOnlyList<GarageVehicle> Garage {get{SyncGarage();return _garage.Select(v=>v.Copy()).ToArray();}}
        public IReadOnlyList<RaceRecord> RacingRecords=>_raceRecords.Select(r=>r.Copy()).ToArray();
        public int RacingWins=>_raceRecords.Sum(r=>r.Wins);
        public RaceMeeting CurrentRaceMeeting=>RacingSchedule.On(Clock.Today);
        public string RaceMeetingSummary()
        {
            var meeting=CurrentRaceMeeting;var next=RacingSchedule.Next(Clock.Today);
            return (meeting==RaceMeeting.None?"今天无比赛，可免费练习":RacingSchedule.Name(meeting)+" · 11人同场 · 参赛 ¥"+RacingSchedule.Fee(meeting)+" · 冠军 ¥"+RacingSchedule.ChampionPrize(meeting))
                +"\n下一场："+next+" · "+RacingSchedule.Name(RacingSchedule.On(next));
        }
        public bool RacingUnlocked=>_owned.Any(MotorCatalog.CanRace);
        private void SyncGarage()
        {
            foreach(var k in new[]{VehicleKind.EBike,VehicleKind.Tricycle,VehicleKind.Car,VehicleKind.Van})
            {
                int count=VehicleCount(k);
                while(_garage.Count(g=>MotorCatalog.Get(g.ModelId)?.Kind==k)<count)
                {
                    var v=NewMotor(MotorCatalog.Legacy(k).Id);
                    v.Commuter=k!=VehicleKind.Van&&!_garage.Any(g=>g.Commuter&&MotorCatalog.Get(g.ModelId).Kind==k);
                    if(!v.Commuter&&k!=VehicleKind.Van)v.GarageId=_regionalGarages.FirstOrDefault(g=>GarageFreeSpaces(g.PlaceId)>0)?.PlaceId;
                    _garage.Add(v);
                }
                while(_garage.Count(g=>MotorCatalog.Get(g.ModelId)?.Kind==k)>count)_garage.Remove(_garage.Last(g=>MotorCatalog.Get(g.ModelId)?.Kind==k));
            }
            foreach(var v in _garage.Where(v=>MotorCatalog.Get(v.ModelId).Kind==VehicleKind.Van)){v.Commuter=false;v.GarageId=null;}
            if(Vehicle==VehicleKind.Van){Vehicle=VehicleKind.Walk;Trip?.ChangeVehicle(Vehicles.Walk);ApplyCargoLimit();}
        }
        private GarageVehicle NewMotor(string model)=>new GarageVehicle{Id="motor_"+(++_motorSerial),ModelId=model};
        public bool DealerSells(string modelId)
        {
            var m=MotorCatalog.Get(modelId);if(m==null||CurrentPlace?.Kind!=PlaceKind.Dealer)return false;
            return CurrentPlace.Id=="dealer"?MotorCatalog.ClassOf(m.Kind)<2:CurrentPlace.Id=="motor_cars"&&MotorCatalog.ClassOf(m.Kind)==2;
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
            SyncGarage();var v=_garage.Find(g=>g.Id==id);if(part<0||part>3)return false;string why=MotorWorkshopBlocker(v);if(why!=null)return Fail(why);
            if(MotorCatalog.Level(v,part)>=3)return false;
            if(!Ledger.TryDebit(Clock.Today,"车辆改装",Money.FromYuan(MotorCatalog.UpgradePrice(v,part)),MotorCatalog.PartName(v,part)))return Fail("改装资金不足。");
            if(part==0)v.Motor++;else if(part==1)v.Battery++;else if(part==2)v.Tyres++;else v.Steering++;
            AdvanceClock(30);return true;
        }
        public long MotorRepairPrice(GarageVehicle v)=>(long)Math.Ceiling((100-v.Condition)*MotorCatalog.Get(v.ModelId).Price*.002);
        public bool RepairMotor(string id)
        {
            SyncGarage();var v=_garage.Find(g=>g.Id==id);string why=MotorWorkshopBlocker(v);if(why!=null)return Fail(why);
            if(v.Condition>=100)return false;if(!Ledger.TryDebit(Clock.Today,"车辆整备",Money.FromYuan(MotorRepairPrice(v)),MotorCatalog.Get(v.ModelId).Name))return Fail("维修资金不足。");
            v.Condition=100;AdvanceClock(30);return true;
        }
        public bool SellMotor(string id)
        {
            SyncGarage();var v=_garage.Find(g=>g.Id==id);if(v==null||Racing||IsGameOver||CurrentPlace?.Kind!=PlaceKind.Dealer||!IsDealerOpen||!MotorAvailable(v))return false;
            var m=MotorCatalog.Get(v.ModelId);if(v.Commuter&&Vehicle==m.Kind&&!SetVehicle(VehicleKind.Walk))return false;
            _owned.Remove(m.Kind);_garage.Remove(v);Ledger.Credit(Clock.Today,"载具出售",Money.FromYuan((long)(m.Price*.6*v.Condition/100)),m.Name);return true;
        }
        public string RaceBlocker(string trackId,string motorId,bool practice)
        {
            SyncGarage();var t=RaceTracks.Get(trackId);var v=_garage.Find(g=>g.Id==motorId);
            if(Racing||IsGameOver||IsWorking||_sleeping||CityActivityExclusive||PendingEvent!=null)return "请先结束当前活动";
            if(t==null||CurrentPlace?.Id!=trackId)return "请实际前往赛场";
            if(DistrictAccessTier<t.Tier)return "先解锁"+DistrictName(t.Tier)+"片区";
            if(!t.IsOpenAt(Clock.MinuteOfDay))return "赛场开放时间 "+t.OpeningHours;
            if(!practice&&CurrentRaceMeeting==RaceMeeting.None)return "今天没有比赛；下一场 "+RacingSchedule.Next(Clock.Today)+"，现在可免费练习";
            if(v==null||!MotorAvailable(v))return "请准备一辆空闲且未被扣押的自有车辆";
            if(!MotorCatalog.CanRace(MotorCatalog.Get(v.ModelId).Kind))return "货车仅供员工进货，不可比赛或练习";
            if(v.Commuter)return "通勤车不能比赛或练习，请先在当地车库改为入库车辆";
            string garageId=RegionalGarages.ForTrack(trackId).Id;
            if(!HasGarage(garageId)||v.GarageId!=garageId)return "只能使用本区车库的入库车辆，可付费从其他车库调入";
            if(v.Condition<40)return "车况低于40%，先去车间修复";
            if(!practice&&RacingWins<t.RequiredWins)return "需要累计赢得 "+t.RequiredWins+" 场低级别比赛";
            if(!practice&&Ledger.Balance<Money.FromYuan(RacingSchedule.Fee(CurrentRaceMeeting)))return "参赛费不足";
            return null;
        }
        public bool StartRace(string trackId,string motorId,bool practice)
        {
            string why=RaceBlocker(trackId,motorId,practice);if(why!=null)return Fail(why);
            var t=RaceTracks.Get(trackId);var v=_garage.Find(g=>g.Id==motorId);
            var meeting=CurrentRaceMeeting;
            if(!practice&&!Ledger.TryDebit(Clock.Today,"赛车报名",Money.FromYuan(RacingSchedule.Fee(meeting)),t.Name+" · "+RacingSchedule.Name(meeting)))return false;
            _raceCitySpeed=Time.Speed;_raceCityPaused=Time.IsUserPaused;Time.SetSpeed(1);
            _raceVehicle=v.Id;ActiveRace=new RaceSimulation(t,MotorCatalog.Performance(v),practice,meeting);LastRaceReport=null;return true;
        }
        public string FinishRace()
        {
            var r=ActiveRace;if(r==null||!r.Finished)return null;
            var v=_garage.Find(g=>g.Id==_raceVehicle);ActiveRace=null;_raceVehicle=null;
            Time.SetSpeed(_raceCitySpeed);Time.SetPaused(_raceCityPaused);
            v.Condition=Math.Max(0,v.Condition-2-r.Damage);double injury=0;
            if(r.Damage>5&&Random.Chance(Math.Min(.8,r.Damage/60))){injury=Math.Min(30,5+r.Damage*.4);Player.Health=Math.Max(1,Player.Health-injury);}
            Player.SpendEnergy(8);string key=r.Track.Id+":"+MotorCatalog.ClassOf(r.Model.Kind)+(r.Practice?":practice":":race");
            var record=_raceRecords.Find(x=>x.Key==key);if(record==null){record=new RaceRecord{Key=key};_raceRecords.Add(record);}record.Starts++;
            long prize=0;if(!r.Abandoned){double score=r.Elapsed+r.Penalty;if(record.BestSeconds<=0||score<record.BestSeconds)record.BestSeconds=score;
                if(!r.Practice){if(r.Position==1)record.Wins++;prize=r.PrizeForPosition(r.Position);if(prize>0)Ledger.Credit(Clock.Today,"赛事奖金",Money.FromYuan(prize),r.Track.Name+" · "+RacingSchedule.Name(r.Meeting));Meet(r.Track.Npc);AddRelation(r.Track.Npc,r.Position==1?3:1);}}
            LastRaceReport=(r.Abandoned?"已退赛":r.Practice?"练习完成":RacingSchedule.Name(r.Meeting)+" · 第 "+r.Position+" / "+r.CompetitorCount+" 名")+" · 用时 "+(r.Elapsed+r.Penalty).ToString("0.00")+"秒（罚时 "+r.Penalty.ToString("0")+"秒）\n奖金 ¥"+prize.ToString("N0")+" · 车况 "+v.Condition.ToString("0")+"% · "+(injury>0?"碰撞受伤，健康 -"+injury.ToString("0"):"未受伤");
            AdvanceClock(30);return LastRaceReport;
        }
        public bool MeetRacer(string trackId)
        {
            var t=RaceTracks.Get(trackId);if(t==null||Racing||CurrentPlace?.Id!=trackId||Clock.MinuteOfDay<540||Clock.MinuteOfDay>=1080)return false;
            Meet(t.Npc);string flag="race_chat:"+t.Id+":"+Clock.Today.TotalDays;if(Flags.Add(flag))AddRelation(t.Npc,2);return true;
        }
        private void SaveRacing(SaveData d)
        {SyncGarage();d.racing=new RacingSave{Garage=_garage.Select(v=>v.Copy()).ToArray(),RegionalGarages=_regionalGarages.Select(g=>g.Copy()).ToArray(),Records=_raceRecords.Select(r=>r.Copy()).ToArray(),Serial=_motorSerial,Stock=_usedMotors.Select(s=>s.Copy()).ToArray(),BoughtLeads=_boughtMotorLeads.ToArray()};}
        private void LoadRacing(SaveData d)
        {
            _garage.Clear();_regionalGarages.Clear();_raceRecords.Clear();_usedMotors.Clear();_boughtMotorLeads.Clear();_motorSerial=d.racing?.Serial??0;
            _regionalGarages.AddRange((d.racing?.RegionalGarages??Array.Empty<RegionalGarageLease>()).Where(g=>RegionalGarages.Get(g.PlaceId)!=null).Select(g=>g.Copy()));
            foreach(var v in d.racing?.Garage??Array.Empty<GarageVehicle>())if(MotorCatalog.Get(v.ModelId)!=null)_garage.Add(v.Copy());
            _raceRecords.AddRange((d.racing?.Records??Array.Empty<RaceRecord>()).Select(r=>r.Copy()));
            _usedMotors.AddRange((d.racing?.Stock??Array.Empty<UsedMotor>()).Where(s=>MotorCatalog.Get(s.Vehicle?.ModelId)!=null).Select(s=>s.Copy()));
            foreach(var l in d.racing?.BoughtLeads??Array.Empty<string>())_boughtMotorLeads.Add(l);SyncGarage();
        }
    }
}
