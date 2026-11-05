using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform _drivingHud;private Text _drivingStats;private string _raceSelection;
        private string PerformanceText(GarageVehicle v)
        {var m=MotorCatalog.Performance(v);return $"极速 {m.Speed:0} km/h　加速 {m.Acceleration:0.0}　抓地 {m.Grip:0}　操控 {m.Handling:0}　车况 {v.Condition:0}%";}
        private void BuildMotorDealer(Window w,Place p)
        {
            bool workshop=p.Id=="motor_scooter_mod";
            Line(w.Content,workshop?"只改装小电驴 · 9:00—19:00":p.Id=="dealer"?"电驴与三轮车 · 9:00—19:00":"四轮车展厅 / 整备车间 · 9:00—19:00",20,Ui.Accent);
            var links=Ui.Row(w.Content,40,8);
            Ui.Size(Ui.Button(links,"我的车库 / 改装",OpenMotorGarage,18),-1,40,1);
            Ui.Size(Ui.Button(links,"四大赛车场",OpenRacingDestinations,18),-1,40,1);
            var destinations=Ui.Row(w.Content,36,8);
            foreach(string id in new[]{"dealer","motor_scooter_mod","motor_cars"}.Where(id=>id!=p.Id))
            {string target=id;Ui.Size(Ui.Button(destinations,"前往"+S.City.GetPlace(target).Name,()=>{CloseWindowSilently(w);S.BeginCityTravel(target);},17),-1,36,1);}
            if(workshop){Para(w.Content,"电机提高加速，电池与电控提高极速，轮毂轮胎提高抓地，转向悬挂提高操控。每项最多3级，每次施工30分钟。",18);return;}
            foreach(var model in MotorCatalog.All.Where(m=>S.DealerSells(m.Id)))
            {
                var m=model;var card=BusinessCard(w.Content,m.Name+" · ¥"+m.Price.ToString("N0"));
                Line(card,$"极速 {m.Speed:0} km/h　加速 {m.Acceleration:0.0}　抓地 {m.Grip:0}　操控 {m.Handling:0}",17);
                Ui.Size(Ui.Button(card,"购买并加入车库",()=>{S.BuyMotor(m.Id);w.Rebuild();},17,Ui.ButtonHot,S.IsDealerOpen),-1,36);
            }
        }
        public void OpenMotorGarage()
        {
            OpenWindow("我的车库",1100,850,w=>
            {
                Para(w.Content,"出行与员工运输仍使用底部车辆按钮；赛车在赛场选择具体车型。施工和出售需要车辆空闲。",17,Ui.Dim);
                foreach(var item in S.Garage)
                {
                    var v=item;var model=MotorCatalog.Get(v.ModelId);var card=BusinessCard(w.Content,model.Name+" · "+v.Id);
                    Line(card,PerformanceText(v),17);string blocker=S.MotorWorkshopBlocker(v);if(blocker!=null)Line(card,blocker,15,Ui.Dim);
                    var row=Ui.Row(card,38,6);
                    for(int part=0;part<4;part++)
                    {
                        int index=part;int level=MotorCatalog.Level(v,part);
                        Ui.Size(Ui.Button(row,MotorCatalog.PartName(v,part)+" "+level+"/3"+(level<3?" · ¥"+MotorCatalog.UpgradePrice(v,part).ToString("N0"):""),()=>{S.UpgradeMotor(v.Id,index);w.Rebuild();},15,Ui.ButtonBg,blocker==null&&level<3),-1,38,1);
                    }
                    var actions=Ui.Row(card,36,6);
                    Ui.Size(Ui.Button(actions,"修复车况 · ¥"+S.MotorRepairPrice(v).ToString("N0"),()=>{S.RepairMotor(v.Id);w.Rebuild();},16,Ui.ButtonBg,blocker==null&&v.Condition<100),-1,36,1);
                    long sale=(long)(model.Price*.6*v.Condition/100);
                    Ui.Size(Ui.Button(actions,"回收出售 · ¥"+sale.ToString("N0"),()=>ConfirmBusiness("出售车辆",model.Name+"将从车库移除，收到 ¥"+sale.ToString("N0"),()=>{S.SellMotor(v.Id);w.Rebuild();}),16,Ui.Warn,S.CurrentPlace?.Kind==PlaceKind.Dealer&&S.MotorAvailable(v)),-1,36,1);
                }
                if(S.Garage.Count==0)Para(w.Content,"还没有车辆，先到顺丰车行购买第一辆小电驴。",19);
                foreach(var k in new[]{VehicleKind.EBike,VehicleKind.Tricycle})if(S.ImpoundedVehicleCount(k)>0){var kind=k;Ui.Size(Ui.Button(w.Content,"赎回"+Vehicles.Get(k).Name+" · "+PoliceSystem.RedeemFee,()=>{S.RedeemVehicle(kind);w.Rebuild();},17),-1,38);}
            });
        }
        public void OpenRacingDestinations()
        {
            OpenWindow("湖区赛车",840,670,w=>
            {
                Line(w.Content,"比赛胜场 "+S.RacingWins+" · 买到第一辆电驴即可开始",20,Ui.Accent);
                foreach(var track in RaceTracks.All)
                {
                    var t=track;var card=BusinessCard(w.Content,t.Name);Line(card,$"{GameSession.DistrictName(t.Tier)}进度 · 正赛需 {t.RequiredWins} 胜 · 报名 ¥{t.Fee:N0} · {t.Laps} 圈",17);
                    Ui.Size(Ui.Button(card,"前往赛场",()=>{CloseWindowSilently(w);S.BeginCityTravel(t.Id);},18,Ui.ButtonHot,S.DistrictAccessTier>=t.Tier),-1,38);
                }
                Ui.Size(Ui.Button(w.Content,"我的车库",OpenMotorGarage,18),-1,38);
            });
        }
        private void BuildRaceReception(Window w,Place place)
        {
            var t=RaceTracks.Get(place.Id);Line(w.Content,t.Name+" · 9:00—18:00",23,Ui.Accent);
            Para(w.Content,"W加速，S刹车/倒车，A/D转向，空格手刹，R复位（罚5秒），Tab切换全景，Esc暂停。按绿色检查点依次跑完；高速撞护栏可能受伤。",17,Ui.Dim);
            var npc=S.CityPerson(t.Npc);var contact=Ui.Row(w.Content,92,12);PortraitThumb(contact,t.Npc,88,()=>OpenCityPersonProfile(t.Npc));
            Ui.Size(Ui.Button(contact,"与 "+npc.Name+" 聊车 · "+npc.Role,()=>{S.MeetRacer(t.Id);w.Rebuild();},18),-1,60,1);
            if(S.IsMet(t.Npc))Line(w.Content,"已认识 · 交情 "+S.Relation(t.Npc)+" · 每周提供一条二手车源",16,Ui.Good);
            if(S.Garage.All(v=>v.Id!=_raceSelection))_raceSelection=S.Garage.FirstOrDefault()?.Id;
            foreach(var v in S.Garage)
            {
                var car=v;Ui.Size(Ui.Button(w.Content,(_raceSelection==car.Id?"● ":"○ ")+MotorCatalog.Get(car.ModelId).Name+" · "+PerformanceText(car),()=>{_raceSelection=car.Id;w.Rebuild();},16,_raceSelection==car.Id?Ui.ButtonHot:Ui.ButtonBg),-1,42);
            }
            if(!S.RacingUnlocked)Line(w.Content,"先去顺丰车行买一辆电驴。",18,Ui.Warn);
            foreach(bool mode in new[]{true,false})
            {
                bool practice=mode;string why=S.RaceBlocker(t.Id,_raceSelection,practice);
                Ui.Size(Ui.Button(w.Content,practice?"免费练习计时 · "+t.Laps+"圈":"三车竞速 · 报名 ¥"+t.Fee+" · 冠军奖金 ¥"+(long)(t.Fee*2.5),()=>{if(S.StartRace(t.Id,_raceSelection,practice))CloseWindowSilently(w);},18,Ui.ButtonHot,why==null),-1,44);
                if(why!=null)Line(w.Content,why,15,Ui.Dim);
            }
            foreach(var record in S.RacingRecords.Where(r=>r.Key.StartsWith(t.Id+":")))
                Line(w.Content,(record.Key.Contains(":0:")?"电驴":record.Key.Contains(":1:")?"三轮":"四轮")+(record.Key.EndsWith("practice")?"练习":"竞速")+" · 最佳 "+(record.BestSeconds>0?record.BestSeconds.ToString("0.00")+"秒":"尚未完赛")+" · "+record.Wins+"胜",16);
        }
        public void BeginDrivingHud()
        {
            foreach(var w in _windows.ToArray())CloseWindowSilently(w);CloseDock();
            _hudCanvas.enabled=false;_labelCanvas.enabled=false;_dockCanvas.enabled=false;
            _drivingHud=Ui.Panel(_modalCanvas.transform,"DrivingHud",new Color(.07f,.1f,.13f,.94f)).rectTransform;
            _drivingHud.Anchor(0,1,1,1,20,-148,20,18);Ui.VList(_drivingHud,6,10);
            _drivingStats=Ui.Label(_drivingHud,"",24);Ui.Size(_drivingStats,-1,40);
            var row=Ui.Row(_drivingHud,38,8);
            Ui.Size(Ui.Button(row,"暂停 / 继续（Esc）",()=>S.ActiveRace.Paused=!S.ActiveRace.Paused,17),230,38);
            Ui.Size(Ui.Button(row,"复位（R）· +5秒",()=>{if(!S.ActiveRace.Paused)S.ActiveRace.Recover();},17),230,38);
            Ui.Size(Ui.Button(row,"退赛（不退报名费）",()=>S.ActiveRace.Abandon(),17,Ui.Warn),260,38);
            Ui.Size(Ui.Label(row,"W/S 加减速　A/D 转向　空格手刹　Tab 全景",17,color:Ui.Dim),-1,38,1);
        }
        public void UpdateDrivingHud()
        {
            var r=S.ActiveRace;if(r==null||_drivingStats==null)return;
            _drivingStats.text=(r.Paused?"已暂停 · ":r.Countdown>0?"起跑倒计时 "+Math.Ceiling(r.Countdown)+" · ":"")+r.Track.Name+"　"+(r.Practice?"计时练习":"第"+r.Position+"/3名")+$"　{Math.Min(r.Track.Laps,r.Lap+1)}/{r.Track.Laps}圈　{r.DisplayKph:0} km/h　{r.Elapsed+r.Penalty:0.0}秒　碰撞 {r.Collisions}";
        }
        public void EndDrivingHud(string report)
        {
            if(_drivingHud!=null)Kill.It(_drivingHud.gameObject);_drivingHud=null;
            _hudCanvas.enabled=true;_labelCanvas.enabled=true;_dockCanvas.enabled=true;
            OpenWindow("比赛结果",760,360,w=>{Para(w.Content,report,22);Ui.Size(Ui.Button(w.Content,"回到赛场接待处",()=>{CloseWindowSilently(w);if(S.CurrentPlace!=null)OnEnteredPlace(S.CurrentPlace);},19,Ui.ButtonHot),-1,44);});
        }
    }
}
