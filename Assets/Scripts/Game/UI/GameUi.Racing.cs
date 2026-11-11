using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform _drivingHud;private Text _drivingStats;private string _raceSelection;
        private int _dealerMotorClass;private string _dealerMotorPlace;
        private void BuildMotorPerformance(Transform parent,MotorModel model,double? condition=null,bool showSummary=true)
        {
            if(showSummary)Line(parent,$"极速 {model.Speed:0} km/h · 加速 {model.Acceleration:0.0}"+
                (condition.HasValue?$" · 车况 {condition.Value:0}%":""),17);
            RectTransform detail=null;
            Button toggle=null;
            toggle=Ui.Button(parent,"展开性能 ▾",()=>
            {
                bool expanded=!detail.gameObject.activeSelf;
                detail.gameObject.SetActive(expanded);
                toggle.GetComponentInChildren<Text>().text=expanded?"收起性能 ▴":"展开性能 ▾";
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)parent);
            },16);
            Ui.Size(toggle,-1,32);
            detail=BuildMotorRadar(parent,model,condition);
            detail.gameObject.SetActive(false);
        }
        private RectTransform BuildMotorRadar(Transform parent,MotorModel model,double? condition)
        {
            var row=Ui.Row(parent,210,16);
            var graph=Ui.Rect(row,"MotorPerformanceRadar");
            Ui.Size(graph,270,210).GetComponent<LayoutElement>().minWidth=270;
            graph.gameObject.AddComponent<MotorRadarGraphic>().Initialize(model);
            var details=Ui.Rect(row,"PerformanceDetails");Ui.Size(details,-1,210,1);
            var layout=Ui.VList(details,6,4);layout.childAlignment=TextAnchor.MiddleLeft;
            Line(details,$"极速 {model.Speed:0} km/h",22,Ui.Accent);
            Line(details,$"动力加速 {model.Acceleration:0.0}",17);
            if(condition.HasValue)Line(details,$"车况 {condition.Value:0}% · 含当前改装",16,Ui.Dim);
            Line(details,"六项统一评分 0—100",15,Ui.Dim);
            Line(details,"越靠外，性能越强",15,Ui.Dim);
            if(model.Kind==VehicleKind.Van)Line(details,"货车仅供员工采购，不可参赛",15,Ui.Dim);
            return row;
        }
        private RectTransform BuildDealerVehicleCard(Transform parent,MotorModel model)
        {
            var card=Ui.Panel(parent,"VehicleCard",Ui.PanelBg2).rectTransform;
            Ui.VList(card,8,12);
            var header=Ui.Row(card,104,14);
            MotorThumbnail(header,model.Id,104);
            var details=Ui.Rect(header,"VehicleInfo");
            Ui.Size(details,0,-1,1).GetComponent<LayoutElement>().minWidth=0;
            Ui.VList(details,4,0);
            Line(details,model.Name,20,Ui.Accent);
            Line(details,"¥"+model.Price.ToString("N0"),18,Ui.Accent);
            Line(details,$"极速 {model.Speed:0} km/h · 加速 {model.Acceleration:0.0}",17);
            return card;
        }
        private void BuildMotorDealer(Window w,Place p)
        {
            bool workshop=p.Id=="motor_scooter_mod";
            Line(w.Content,workshop?"只改装小电驴 · 9:00—19:00":p.Id=="dealer"?"电驴与三轮车 · 9:00—19:00":"四轮车展厅 / 整备车间 · 9:00—19:00",20,Ui.Accent);
            Ui.Size(Ui.Button(w.Content,"改装 / 维修 / 回收",OpenMotorWorkshop,18),-1,40);
            if(workshop){Para(w.Content,"电机提高加速，电池与电控提高极速，轮毂轮胎提高抓地和制动，转向悬挂提高转向和稳定。每项最多3级，每次施工30分钟。",18);return;}
            if(_dealerMotorPlace!=p.Id){_dealerMotorPlace=p.Id;_dealerMotorClass=MotorCatalog.All.Where(m=>S.DealerSells(m.Id)).Select(m=>MotorCatalog.DealerClass(m.Kind)).DefaultIfEmpty(0).First();}
            BuildDealerMotorTabs(w.Content,_dealerMotorClass,c=>{_dealerMotorClass=c;w.Rebuild();});
            var models=MotorCatalog.All.Where(m=>S.DealerSells(m.Id)&&MotorCatalog.DealerClass(m.Kind)==_dealerMotorClass).ToArray();
            foreach(var model in models)
            {
                var m=model;var card=BuildDealerVehicleCard(w.Content,m);
                BuildMotorPerformance(card,m,showSummary:false);
                Ui.Size(Ui.Button(card,"购买车辆 · ¥"+m.Price.ToString("N0"),()=>OpenMotorAcquisition(w,m.Id,false),17,Ui.ButtonHot,S.IsDealerOpen),-1,36);
            }
            if(models.Length==0)Para(w.Content,"本车行不出售这一类车辆。顺丰出售小电驴和三轮车，湖岸汽车城出售货车和轿车。",17,Ui.Dim);
        }
        private void OpenMotorWorkshop()
        {
            if(S.CurrentPlace?.Kind!=PlaceKind.Dealer&&S.CurrentPlace?.Id!="motor_scooter_mod"){Toast("请到对应车行或顺丰改车办理整备。");return;}
            OpenWindow("车辆整备",1100,850,w=>
            {
                BuildDealerMotorTabs(w.Content,_workshopMotorClass,c=>{_workshopMotorClass=c;w.Rebuild();});
                foreach(var item in S.Garage.Where(v=>MotorCatalog.DealerClass(MotorCatalog.Get(v.ModelId).Kind)==_workshopMotorClass))
                {
                    var v=item;var model=MotorCatalog.Get(v.ModelId);var card=MotorCard(w.Content,model,model.Name+" · "+v.Id);
                    Line(card,S.MotorLocationText(v),16,Ui.Dim);
                    BuildMotorPerformance(card,MotorCatalog.Performance(v),v.Condition);string blocker=S.MotorWorkshopBlocker(v);if(blocker!=null)Line(card,blocker,15,Ui.Dim);
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
                Line(w.Content,"比赛胜场 "+S.RacingWins+" · 参赛和练习使用对应片区的入库车辆",20,Ui.Accent);
                Para(w.Content, "周一大赛冠军 ¥2,000，三日小赛冠军 ¥1,000。同日优先举行大赛，最多10名车友同场。", 17);
                Para(w.Content,S.RaceMeetingSummary(),17,Ui.Accent);
                foreach(var track in RaceTracks.All.Where(track => !GameSession.FirstChapterTrial || track.Tier <= S.DistrictAccessTier))
                {
                    var t=track;var card=BusinessCard(w.Content,t.Name);Line(card,t.OpeningHours,16,Ui.Dim);Line(card,$"{GameSession.DistrictName(t.Tier)}进度 · 正赛需 {t.RequiredWins} 胜 · {t.Laps} 圈 · 练习 ¥{RacingSchedule.PracticeFee}/次",17);
                    Ui.Size(Ui.Button(card,"前往赛场",()=>{CloseWindowSilently(w);S.BeginCityTravel(t.Id);},18,Ui.ButtonHot,S.DistrictAccessTier>=t.Tier),-1,38);
                }
            });
        }
        private void BuildRaceReception(Window w,Place place)
        {
            var t=RaceTracks.Get(place.Id);Line(w.Content,t.Name+" · "+t.OpeningHours,23,Ui.Accent);
            Para(w.Content,S.RaceMeetingSummary(),18,Ui.Accent);
            Para(w.Content,"比赛与练习固定1倍速。W沿车头方向加速，S刹车/倒车，A/D只控制车辆转向；鼠标右键拖动调整视角。空格或Esc暂停/恢复，Shift手刹，R复位（罚5秒），Tab切换全景。默认后上方跟随，右键调整角度，滚轮缩放，V回正。",17,Ui.Dim);
            var npc=S.CityPerson(t.Npc);var contact=Ui.Row(w.Content,92,12);PortraitThumb(contact,t.Npc,88,()=>OpenCityPersonProfile(t.Npc));
            bool racerHere=S.Clock.MinuteOfDay>=540&&S.Clock.MinuteOfDay<1080&&S.CitySamePlace(t.Npc);
            Ui.Size(Ui.Button(contact,racerHere?"与 "+npc.Name+" 聊车 · "+npc.Role:"车友此刻不在接待处 · 通常9:00—18:00在场",()=>{if(S.MeetRacer(t.Id))OpenPersonDialogue(t.Npc,parent:w);else Toast("车友此刻不在接待处，请稍后再来。");},18,Ui.ButtonBg,racerHere),-1,60,1);
            if(S.IsMet(t.Npc))Line(w.Content,"已认识 · 交情 "+S.Relation(t.Npc)+" · 每周提供一条二手车源",16,Ui.Good);
            BuildRaceReceptionRoster(w, t);
            BuildMotorClassTabs(w.Content,_raceMotorClass,c=>{_raceMotorClass=c;_raceSelection=null;w.Rebuild();});
            var garage=RegionalGarages.ForTrack(t.Id);
            var motors=S.Garage.Where(v=>!v.Commuter&&v.GarageId==garage.Id&&MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind)==_raceMotorClass).ToArray();
            if(motors.All(v=>v.Id!=_raceSelection))_raceSelection=motors.FirstOrDefault()?.Id;
            Line(w.Content,garage.Name+" · "+MotorCatalog.ClassName(_raceMotorClass)+" "+motors.Length+"辆",19,Ui.Accent);
            foreach(var v in motors)
            {
                var car=v;MotorChoice(w.Content,car.ModelId,(_raceSelection==car.Id?"● ":"○ ")+MotorCatalog.Get(car.ModelId).Name+$" · 车况 {car.Condition:0}%",()=>{_raceSelection=car.Id;w.Rebuild();},selected:_raceSelection==car.Id);
            }
            var selectedMotor=motors.FirstOrDefault(v=>v.Id==_raceSelection);
            if(selectedMotor!=null)BuildMotorPerformance(w.Content,MotorCatalog.Performance(selectedMotor),selectedMotor.Condition);
            if(motors.Length==0)Para(w.Content,S.HasGarage(garage.Id)?"本区车库没有这一类车辆，可以从其他车库付费调入。":"请先到本区车库建筑旁按 F，租用或购买车位，再将通勤车入库或购入赛车。",17,Ui.Warn);
            BuildMotorTransfer(w,garage,_raceMotorClass,"race:"+t.Id);
            string previousWhy=null;
            foreach(bool mode in new[]{true,false})
            {
                bool practice=mode;string why=S.RaceBlocker(t.Id,_raceSelection,practice);
                var meeting=S.CurrentRaceMeeting;
                string title=practice?"开始练习 · ¥"+RacingSchedule.PracticeFee+" · "+t.Laps+"圈":meeting==RaceMeeting.None?"今天没有竞速比赛":"开始"+RacingSchedule.Name(meeting)+" · ¥"+RacingSchedule.Fee(meeting)+" · 冠军 ¥"+RacingSchedule.ChampionPrize(meeting);
                Ui.Size(Ui.Button(w.Content,title,()=>{if(S.StartRace(t.Id,_raceSelection,practice))CloseWindowSilently(w);},18,Ui.ButtonHot,why==null),-1,44);
                if(why!=null&&why!=previousWhy)Line(w.Content,why,15,Ui.Dim);previousWhy=why;
            }
            foreach(var record in S.RacingRecords.Where(r=>r.Key.StartsWith(t.Id+":"+_raceMotorClass+":")))
                Line(w.Content,MotorCatalog.ClassName(_raceMotorClass)+(record.Key.EndsWith("practice")?"练习":"竞速")+" · 最佳 "+(record.BestSeconds>0?record.BestSeconds.ToString("0.00")+"秒":"尚未完赛")+" · "+record.Wins+"胜",16);
        }

        private void BuildRaceReceptionRoster(Window w, RaceTrack track)
        {
            var today = S.Clock.Today;
            var roster = S.RaceParticipantIds(track.Id);
            var bookings = S.RaceInvitations.Where(i => i.TrackId == track.Id).ToArray();
            var invited = bookings.Where(i => i.Day == today.TotalDays).Select(i => i.PersonId).ToHashSet();
            if (S.CurrentRaceMeeting != RaceMeeting.None && S.Clock.MinuteOfDay < RacingSchedule.MeetCloseMinute)
            {
                Line(w.Content, "正赛车友 · 已到场 " + roster.Count + " 人", 19, Ui.Accent);
                for (int first = 0; first < roster.Count; first += 5)
                {
                    var row = Ui.Row(w.Content, 64, 8);
                    foreach (string id in roster.Skip(first).Take(5))
                    {
                        string person = id;
                        string label = S.CityPerson(id)?.Name ?? id;
                        var cell = Ui.Size(Ui.Row(row, 64, 4), 0, 64, 1);
                        PortraitThumb(cell, id, 40, () => OpenPersonDialogue(person, parent: w));
                        if (invited.Contains(id)) label += "\n<size=13>受邀</size>";
                        Ui.Size(Ui.Button(cell, label, () => OpenPersonDialogue(person, parent: w), 15), 0, 44, 1);
                    }
                }
                if (roster.Count == 0) Para(w.Content, "车友尚未到场。离开接待窗口后，游戏时间继续推进。", 16, Ui.Dim);
            }
            foreach (var invite in bookings)
            {
                string name = S.CityPerson(invite.PersonId)?.Name ?? invite.PersonId;
                string state = invite.Day != today.TotalDays ? "已确认"
                    : S.Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute ? "场次已结束"
                    : S.RaceAttendeeHere(invite.PersonId, track.Id) ? "已到场" : "尚未到场";
                var row = PersonSummary(w.Content, invite.PersonId, name, new GameDate(invite.Day) + " · " + state, 46);
                if (state != "场次已结束")
                    Ui.Size(Ui.Button(row, "取消邀请", () =>
                    {
                        if (S.CancelRaceInvitation(invite.PersonId, track.Id, invite.Day)) w.Rebuild();
                    }, 15), 100, 34);
            }
        }

        private void BuildRaceInvitationChoices(Window w, string personId, string selectedTrack, Action<string> selectTrack)
        {
            var person = S.CityPerson(personId);
            var tracks = RaceTracks.All.Where(t => S.City.HasPlace(t.Id) && S.DistrictAccessTier >= t.Tier).ToArray();
            if (person == null || tracks.Length == 0)
            {
                Para(w.Content, "当前没有可邀约的赛场。", 18, Ui.Dim);
                return;
            }
            int trackIndex = Math.Max(0, Array.FindIndex(tracks, t => t.Id == selectedTrack));
            var track = tracks[trackIndex];
            PersonSummary(w.Content, personId, person.Name, "邀请同场参赛");
            Ui.Size(Ui.Choice(w.Content, tracks.Select(t => t.Name), trackIndex, index =>
            { selectTrack(tracks[index].Id); w.Rebuild(); }), -1, 40);
            var day = S.Clock.Today;
            if (S.CurrentRaceMeeting == RaceMeeting.None || S.Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute)
                day = RacingSchedule.Next(day);
            var bookings = S.RaceInvitations;
            for (int i = 0; i < 3; i++, day = RacingSchedule.Next(day))
            {
                GameDate date = day;
                var card = BusinessCard(w.Content, date + " · " + RacingSchedule.Name(RacingSchedule.On(date)));
                Line(card, "09:00—18:00 · " + track.Name, 16, Ui.Dim);
                bool booked = bookings.Any(invite => invite.PersonId == person.Id && invite.TrackId == track.Id && invite.Day == date.TotalDays);
                if (booked)
                {
                    Ui.Size(Ui.Button(card, "已接受 · 取消邀请", () =>
                    { S.CancelRaceInvitation(person.Id, track.Id, date.TotalDays); w.Rebuild(); }, 16), -1, 36);
                }
                else
                {
                    string why = S.RaceInviteBlocker(person.Id, track.Id, date);
                    if (why != null) Para(card, why, 15, Ui.Dim);
                    Ui.Size(Ui.Button(card, "邀请 " + person.Name, () =>
                    { if (S.InviteRaceOpponent(person.Id, track.Id, date)) w.Rebuild(); },
                        17, Ui.ButtonHot, why == null), -1, 38);
                }
            }
        }
        public void BeginDrivingHud()
        {
            foreach(var w in _windows.ToArray())CloseWindowSilently(w);CloseDock();
            _hudCanvas.enabled=false;_labelCanvas.enabled=false;_dockCanvas.enabled=false;
            _drivingHud=Ui.Panel(_modalCanvas.transform,"DrivingHud",new Color(.07f,.1f,.13f,.94f)).rectTransform;
            _drivingHud.Anchor(0,1,1,1,20,-148,20,18);Ui.VList(_drivingHud,6,10);
            _drivingStats=Ui.Label(_drivingHud,"",24);Ui.Size(_drivingStats,-1,40);
            _drivingStats.resizeTextForBestFit=true;_drivingStats.resizeTextMinSize=18;_drivingStats.resizeTextMaxSize=24;
            var row=Ui.Row(_drivingHud,38,8);
            Ui.Size(Ui.Button(row,"暂停 / 继续（空格 / Esc）",()=>S.ActiveRace.Paused=!S.ActiveRace.Paused,17),230,38);
            Ui.Size(Ui.Button(row,"复位（R）· +5秒",()=>{if(!S.ActiveRace.Paused)S.ActiveRace.Recover();},17),230,38);
            Ui.Size(Ui.Button(row,S.ActiveRace.Practice?"结束练习":"退赛（不退报名费）",()=>S.ActiveRace.Abandon(),17,Ui.Warn),260,38);
            Ui.Size(Ui.Label(row,"固定1×　W/S油门刹车　A/D转向　空格暂停　Shift手刹　右键视角　V回正　Tab全景",16,color:Ui.Dim),-1,38,1);
        }
        public void UpdateDrivingHud()
        {
            var r=S.ActiveRace;if(r==null||_drivingStats==null)return;
            _drivingStats.text=(r.Paused?"已暂停 · ":r.Countdown>0?"起跑倒计时 "+Math.Ceiling(r.Countdown)+" · ":"")+r.Track.Name+" · 固定1×　"+(r.Practice?"计时练习":RacingSchedule.Name(r.Meeting)+" · 第"+r.Position+"/"+r.CompetitorCount+"名")+$"　{Math.Min(r.Track.Laps,r.Lap+1)}/{r.Track.Laps}圈　{r.DisplayKph:0} km/h　{r.Elapsed+r.Penalty:0.0}秒　碰撞 {r.Collisions}";
            if (r.Opponents.Count == 0) return;
            int closest = 0;
            for (int i = 1; i < r.Opponents.Count; i++)
                if (Math.Abs(r.RivalProgress[i] - r.Progress) < Math.Abs(r.RivalProgress[closest] - r.Progress)) closest = i;
            _drivingStats.text += "　最近车友 " + S.CityPerson(r.Opponents[closest].PersonId)?.Name;
        }
        public void EndDrivingHud(string report)
        {
            if(_drivingHud!=null)Kill.It(_drivingHud.gameObject);_drivingHud=null;
            _hudCanvas.enabled=true;_labelCanvas.enabled=true;_dockCanvas.enabled=true;
            var result = S.LastRaceSettlement;
            OpenWindow(result?.Practice == true ? "练习结算" : "比赛结算", 940, 790, w =>
            {
                if (result == null) Para(w.Content, report, 20);
                else BuildRaceSettlement(w.Content, result);
                Ui.Size(Ui.Button(w.Content, "回到赛场接待处", () =>
                { CloseWindowSilently(w); if (S.CurrentPlace != null) OnEnteredPlace(S.CurrentPlace); }, 19, Ui.ButtonHot), -1, 44);
            });
        }

        private void BuildRaceSettlement(Transform content, RaceSettlement result)
        {
            string mode = result.Practice ? "计时练习" : RacingSchedule.Name(result.Meeting);
            Line(content, result.Track + " · " + mode + " · " + result.Vehicle, 22, Ui.Accent);
            var summary = Ui.Row(content, 68, 8);
            string place = result.Abandoned ? (result.Practice ? "练习已结束" : "已退赛")
                : result.Practice ? "练习完成" : $"第 {result.Position} / {result.Competitors} 名";
            Ui.Size(Ui.Label(summary, $"<b>{place}</b>", 28, TextAnchor.MiddleCenter, Ui.Accent), 0, 68, 1);
            Ui.Size(Ui.Label(summary, $"<b>奖金 ¥{result.Prize:N0}</b>", 24, TextAnchor.MiddleCenter, Ui.Good), 0, 68, 1);
            Ui.Size(Ui.Label(summary, $"用时 {result.Seconds:0.00} 秒", 21, TextAnchor.MiddleCenter), 0, 68, 1);
            Line(content, $"罚时 {result.Penalty:0} 秒 · 碰撞 {result.Collisions} 次 · 车况 {result.Condition:0}%"
                + (result.Injury > 0 ? $" · 健康 -{result.Injury:0}" : " · 未受伤")
                + (result.PersonalBest ? " · 个人最佳" : ""), 16, Ui.Dim);
            if (!result.Practice && !result.Abandoned)
            {
                Line(content, "同场交情", 19, Ui.Accent);
                if (result.Relationships.Length == 0) Line(content, "本场交情无变化", 16, Ui.Dim);
                var changes = result.Relationships.OrderByDescending(c => c.Invited).ToArray();
                for (int first = 0; first < changes.Length; first += 3)
                {
                    var row = Ui.Row(content, 44, 8);
                    foreach (var change in changes.Skip(first).Take(3))
                    {
                        var cell = Ui.Size(Ui.Row(row, 44, 6), 0, 44, 1);
                        PortraitThumb(cell, change.PersonId, 36, null);
                        Ui.Size(Ui.Label(cell, $"{change.Name}{(change.Invited ? "（受邀）" : "")} +{change.After - change.Before}",
                            16, color: Ui.Good), 0, 44, 1);
                    }
                }
                Line(content, "完赛排名", 19, Ui.Accent);
                foreach (var entry in result.Standings)
                {
                    var row = Ui.Row(content, 44, 8);
                    Color color = entry.Player ? Ui.Accent : Ui.TextColor;
                    Ui.Size(Ui.Label(row, entry.Place.ToString(), 17, TextAnchor.MiddleCenter, color), 46, 34);
                    if (entry.Player) Ui.Size(Ui.Rect(row, "PlayerPortraitSpace"), 36);
                    else PortraitThumb(row, entry.PersonId, 36, null);
                    Ui.Size(Ui.Label(row, entry.Name + (entry.Invited ? " · 受邀" : ""), 17, color: color), 0, 34, 1);
                    Ui.Size(Ui.Label(row, entry.Vehicle, 15, color: Ui.Dim), 0, 34, 1);
                    Ui.Size(Ui.Label(row, $"{entry.Seconds:0.00} 秒", 16, TextAnchor.MiddleRight, color), 110, 34);
                }
            }
        }
    }
}
