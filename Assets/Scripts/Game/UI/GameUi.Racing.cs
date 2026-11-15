using System;
using System.Collections.Generic;
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
        private RectTransform _drivingHud;
        private Text _drivingLap, _drivingStats, _drivingNearby, _drivingSpeed, _drivingCountdown, _drivingHint, _drivingHandbrake, _drivingBrakeLabel;
        private Image _drivingThrottle, _drivingBrake;
        private string _raceSelection;
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
                    if (v.EventBatteryWorn)
                    {
                        string batteryBlocker = S.EventBatteryRepairBlocker(v.Id);
                        Ui.Size(Ui.Button(card, "更换老化电池 · ¥" + S.EventBatteryReplacementPrice(v).ToString("N0") + " · 30 分钟", () => { S.ReplaceEventBattery(v.Id); w.Rebuild(); }, 16, Ui.ButtonHot, batteryBlocker == null), -1, 38);
                        if (batteryBlocker != null) Line(card, batteryBlocker, 15, Ui.Dim);
                    }
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
                if (S.RacingAccessBlocker is string blocker)
                {
                    Para(w.Content, blocker, 20, Ui.Warn);
                    return;
                }
                BuildRacingIntroduction(w);
                Line(w.Content,"比赛胜场 "+S.RacingWins+" · 参赛和练习使用对应片区的入库车辆",20,Ui.Accent);
                Para(w.Content, $"周一大赛冠军 ¥{RacingSchedule.ChampionPrize(RaceMeeting.Major):N0} · 三日小赛冠军 ¥{RacingSchedule.ChampionPrize(RaceMeeting.Small):N0}。同日优先举行大赛，最多10名车友同场。", 17);
                Para(w.Content,S.RaceMeetingSummary(),17,Ui.Accent);
                foreach(var track in RaceTracks.All.Where(track => !GameSession.FirstChapterTrial || track.Tier <= S.DistrictAccessTier))
                {
                    var t=track;var card=BusinessCard(w.Content,t.Name);Line(card,t.OpeningHours,16,Ui.Dim);Line(card,$"{GameSession.DistrictName(t.Tier)}进度 · 正赛需 {t.RequiredWins} 胜 · {t.Laps} 圈 · 练习 ¥{RacingSchedule.PracticeFee}/次",17);
                    Ui.Size(Ui.Button(card,"前往赛场",()=>{CloseWindowSilently(w);S.BeginCityTravel(t.Id);},18,Ui.ButtonHot,S.DistrictAccessTier>=t.Tier),-1,38);
                }
            });
        }

        private void BuildRacingIntroduction(Window w, bool compact = false)
        {
            if (!S.RacingUnlocked) return;
            string destination, instruction;
            switch (S.RacingIntroduction)
            {
                case RacingIntroStep.GetVehicle:
                    destination = "dealer";
                    instruction = "先到顺丰车行买一辆小电驴或三轮车。";
                    break;
                case RacingIntroStep.GetParking:
                    destination = GameSession.StarterRaceGarageId;
                    instruction = "到城郊赛场车库全款购买一个车位。";
                    break;
                case RacingIntroStep.ParkVehicle:
                    destination = GameSession.StarterRaceGarageId;
                    instruction = "把小电驴或三轮车停入城郊车库。通勤车入库时会取消通勤用途，也可从其他车库调入。";
                    break;
                case RacingIntroStep.Drive:
                    destination = GameSession.StarterRaceTrackId;
                    instruction = $"在城郊练习场选入库车辆，完成一次计时练习（¥{RacingSchedule.PracticeFee}）或正式比赛。";
                    break;
                default: return;
            }
            bool canTravel = S.CurrentPlace?.Id != destination && S.City.HasPlace(destination);
            if (compact)
            {
                var row = Ui.Row(w.Content, 52, 12);
                Ui.Size(Ui.Label(row, "赛车入门", 17, TextAnchor.MiddleLeft, Ui.Accent), 88);
                Ui.Size(Ui.Label(row, instruction, 16, TextAnchor.MiddleLeft, Ui.Dim), 0, -1, 1);
                if (canTravel)
                    Ui.Size(Ui.Button(row, "前往" + S.City.GetPlace(destination).Name, Travel, 17), 180, 36);
                return;
            }
            var card = BusinessCard(w.Content, "许星澜 · 赛车入门");
            Para(card, instruction, 17);
            if (canTravel)
                Ui.Size(Ui.Button(card, "前往" + S.City.GetPlace(destination).Name, Travel, 18, Ui.ButtonHot), -1, 38);

            void Travel()
            {
                if (S.BeginCityTravel(destination)) CloseWindowSilently(w);
            }
        }

        private enum RaceReceptionTab { Registration, Practice, Special }

        private sealed class RaceReceptionView
        {
            public RaceReceptionTab Tab;
            public int? SelectedDay;
            public bool ChoosingVehicle;
        }

        private void OpenRaceReception(Place place)
        {
            var view = new RaceReceptionView();
            OpenPlaceWindow(place, (w, p) => BuildRaceReception(w, RaceTracks.Get(p.Id), view), 1430, 810);
        }

        private void BuildRaceReception(Window w, RaceTrack track, RaceReceptionView view)
        {
            if (S.RacingAccessBlocker is string blocker) { Ui.Notice(w.Content, blocker); return; }
            var today = S.Clock.Today;
            var bookings = S.EventRaceBookings.Where(b => b.Day == today.TotalDays && b.TrackId == track.Id).ToArray();
            bool collectorTrial = track.Id == "race_3" && S.HasCollectorTrial;
            if (bookings.Length == 0 && !collectorTrial && view.Tab == RaceReceptionTab.Special) view.Tab = RaceReceptionTab.Registration;
            var tabs = Ui.Row(w.Content, 42, 8);
            Tab("报名参赛", RaceReceptionTab.Registration, 154);
            Tab("计时练习", RaceReceptionTab.Practice, 132);
            if (bookings.Length > 0 || collectorTrial) Tab("专属活动", RaceReceptionTab.Special, 132);
            Ui.Size(Ui.Label(tabs, $"{RaceDateText(today)}  {Ui.Hm(S.Clock.MinuteOfDay)}", 16, TextAnchor.MiddleRight, Ui.Dim), 0, 42, 1);

            if (track.Id == GameSession.StarterRaceTrackId) BuildRacingIntroduction(w, compact: true);
            var columns = Ui.Row(w.Content, -1, 12);
            columns.GetComponent<LayoutElement>().flexibleHeight = 1;
            var reception = RaceReceptionColumn(columns, "赛场接待", 172);
            BuildRaceDesk(reception, track, w);

            if (view.Tab != RaceReceptionTab.Registration)
            {
                var vehicles = RaceReceptionColumn(columns, view.Tab == RaceReceptionTab.Practice ? "计时练习" : "专属活动", 0, 1);
                var options = RaceReceptionBody(vehicles);
                BuildRaceVehicleSelection(options, track, view, bookings, collectorTrial, w);
                return;
            }

            var days = Enumerable.Range(0, 7).Select(today.AddDays).Where(d => RacingSchedule.On(d) != RaceMeeting.None).ToArray();
            if (!days.Any(d => d.TotalDays == view.SelectedDay))
                view.SelectedDay = days.First(d => d != today || S.Clock.MinuteOfDay < RacingSchedule.MeetCloseMinute).TotalDays;
            var date = new GameDate(view.SelectedDay.Value);
            var schedule = RaceReceptionColumn(columns, "未来 7 天赛程", 258);
            var scheduleBody = RaceReceptionBody(schedule);
            foreach (var day in days)
            {
                var entry = S.RaceRegistrationAt(track.Id, day);
                bool ended = day == today && S.Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute;
                string status = entry?.Started == true ? "已参赛" : ended ? "已结束" : entry != null ? "已报名" : "可报名";
                string label = (day == today ? "今天 · " : "") + RaceDateText(day) + "\n"
                    + RacingSchedule.Name(RacingSchedule.On(day)) + "  ·  " + status;
                var button = Ui.Size(Ui.Button(scheduleBody, label, () =>
                {
                    view.SelectedDay = day.TotalDays;
                    view.ChoosingVehicle = false;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 17, day == date ? Ui.ButtonHot : Ui.ButtonBg), -1, 68);
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }

            var detail = RaceReceptionColumn(columns, "比赛信息", 0, 1);
            var body = RaceReceptionBody(detail);
            var registration = S.RaceRegistrationAt(track.Id, date);
            bool canStart = registration != null && !registration.Started && date == today
                && S.Clock.MinuteOfDay >= RacingSchedule.MeetOpenMinute && S.Clock.MinuteOfDay < RacingSchedule.MeetCloseMinute;
            if (view.ChoosingVehicle && canStart)
                BuildRaceVehicleSelection(body, track, view, bookings, collectorTrial, w);
            else
                BuildRaceMeetingDetail(body, track, date, registration, canStart, view, w);

            var next = days.Where(d => d > today || S.Clock.MinuteOfDay < RacingSchedule.MeetCloseMinute)
                .Select(d => S.RaceRegistrationAt(track.Id, d)).FirstOrDefault(r => r != null && !r.Started);
            if (next != null)
            {
                var bookedDay = new GameDate(next.Day);
                var footer = Ui.Row(w.Content, 39, 10);
                Ui.Size(Ui.Label(footer, "已加入日程", 17, TextAnchor.MiddleLeft, Ui.Good), 94);
                Ui.Size(Ui.Label(footer, $"{RaceDateText(bookedDay)} · {RacingSchedule.MeetHours} · {RacingSchedule.Name(RacingSchedule.On(bookedDay))}", 16, TextAnchor.MiddleLeft), 0, 39, 1);
                Ui.Size(Ui.Button(footer, "查看日程", () => OpenCalendar(next.Day), 16), 106, 34);
            }

            void Tab(string title, RaceReceptionTab tab, int width)
            {
                Ui.Size(Ui.Button(tabs, title, () =>
                {
                    view.Tab = tab;
                    view.ChoosingVehicle = false;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 18,
                    view.Tab == tab ? Ui.ButtonHot : Ui.ButtonBg), width, 40);
            }
        }

        private static string RaceDateText(GameDate day) => $"{day.Month}月{day.Day}日 {Ui.WeekdayName(day.Weekday)}";

        private static RectTransform RaceReceptionColumn(Transform parent, string title, int width, int flex = 0)
        {
            var column = Ui.Rect(parent, title);
            Ui.Size(column, width, -1, flex);
            Ui.VList(column, 5, 0);
            Line(column, title, 20);
            return column;
        }

        private static RectTransform RaceReceptionBody(Transform parent)
        {
            var body = Ui.Panel(parent, "RaceReceptionBody", Ui.FlatWell).rectTransform;
            Ui.VList(body, 8, 10);
            Ui.Size(body).GetComponent<LayoutElement>().flexibleHeight = 1;
            return body;
        }

        private void BuildRaceDesk(Transform parent, RaceTrack track, Window w)
        {
            var npc = S.CityPerson(track.Npc);
            if (npc == null) return;
            var card = RaceReceptionBody(parent);
            var portrait = Ui.Panel(card, "RaceHostPortrait", Ui.PanelBg2).rectTransform;
            Ui.Size(portrait, -1, 154);
            FitPortrait(portrait, track.Npc, 3);
            bool here = S.Clock.MinuteOfDay >= RacingSchedule.MeetOpenMinute && S.Clock.MinuteOfDay < RacingSchedule.MeetCloseMinute
                && S.CitySamePlace(track.Npc);
            Ui.Label(card, npc.Name, 21, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            Ui.Label(card, npc.Role, 15, TextAnchor.MiddleCenter, Ui.Dim);
            Ui.Label(card, here ? "在场" : "暂不在场", 15, TextAnchor.MiddleCenter, here ? Ui.Good : Ui.Dim);
            Ui.Size(Ui.Button(card, "交谈", () =>
            {
                if (S.MeetRacer(track.Id)) OpenPersonDialogue(track.Npc, parent: w);
                else Toast("接待车友暂不在场。");
            }, 17, interactable: here), -1, 38);
            Ui.Label(card, "接待 " + RacingSchedule.MeetHours, 14, TextAnchor.MiddleCenter, Ui.Dim);
            if (S.IsMet(track.Npc)) Ui.Label(card, $"熟悉度 {S.Relation(track.Npc)}", 15, TextAnchor.MiddleCenter, Ui.Good);
        }

        private void BuildRaceMeetingDetail(Transform parent, RaceTrack track, GameDate date, RaceRegistration registration,
            bool canStart, RaceReceptionView view, Window w)
        {
            var meeting = RacingSchedule.On(date);
            bool today = date == S.Clock.Today;
            bool ended = today && S.Clock.MinuteOfDay >= RacingSchedule.MeetCloseMinute;
            var header = Ui.Row(parent, 59, 12);
            Metric(0, RacingSchedule.Name(meeting), $"{RaceDateText(date)} · {RacingSchedule.MeetHours} · {track.Laps} 圈", Ui.TextColor);
            Metric(126, "参赛费", $"¥{RacingSchedule.Fee(meeting):N0}", Ui.TextColor);
            Metric(138, "冠军奖金", $"¥{RacingSchedule.ChampionPrize(meeting):N0}", Ui.Good);

            var invited = S.RaceInvitations.Where(i => i.TrackId == track.Id && i.Day == date.TotalDays).Select(i => i.PersonId).ToArray();
            var arrived = today && !ended ? S.RaceParticipantIds(track.Id) : Array.Empty<string>();
            var people = arrived.Concat(invited).Distinct().ToArray();
            var heading = Ui.Row(parent, 35, 8);
            Ui.Size(Ui.Label(heading, today ? "参赛车友" : "已约同行", 21, TextAnchor.MiddleLeft), 0, 35, 1).fontStyle = FontStyle.Bold;
            string rosterStatus = today && !ended ? $"{arrived.Count} 人已到场" : !today ? $"{invited.Length} 人已确认" : "场次已结束";
            Ui.Size(Ui.Label(heading, rosterStatus, 16, TextAnchor.MiddleRight, Ui.Good), 150, 35);
            var list = Ui.Scroll(parent);
            Ui.Size((RectTransform)list.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
            if (people.Length == 0) Line(list, !today ? "比赛日显示到场车友，可从人物交谈里邀请同行。"
                : ended ? "本场已经结束。" : "车友尚未到场。", 17, Ui.Dim);
            int perRow = people.Length > 8 ? 5 : 4;
            for (int first = 0; first < people.Length; first += perRow)
            {
                var row = Ui.Row(list, 110, 7);
                foreach (string person in people.Skip(first).Take(perRow))
                    BuildRaceRosterCard(row, person, invited.Contains(person), arrived.Contains(person), today, track, date, w);
                for (int i = Math.Min(people.Length - first, perRow); i < perRow; i++)
                    Ui.Size(Ui.Rect(row, "EmptyRacerCell"), 0, -1, 1);
            }

            if (registration?.Started == true) { Line(parent, "你已参加本场比赛", 17, Ui.Good); return; }
            if (ended) { Line(parent, "本场比赛已结束", 17, Ui.Dim); return; }
            var actions = Ui.Row(parent, 43, 8);
            if (registration == null)
            {
                string reason = S.RaceRegistrationBlocker(track.Id, date);
                Ui.Size(Ui.Label(actions, reason ?? "报名后加入日程，参赛费在开赛时扣除。", 16, TextAnchor.MiddleLeft, reason == null ? Ui.Dim : Ui.Warn), 0, 43, 1);
                Ui.Size(Ui.Button(actions, "确认报名", () =>
                {
                    if (S.RegisterForRace(track.Id, date)) _upcomingScheduleDirty = true;
                    w.Rebuild();
                }, 19, Ui.ButtonHot, reason == null), 190, 42);
                return;
            }
            Ui.Size(Ui.Label(actions, canStart ? "开赛前选择本区车库的车辆" : $"已报名 · 比赛当天 {RacingSchedule.MeetHours} 到场", 16, TextAnchor.MiddleLeft, Ui.Dim), 0, 43, 1);
            Ui.Size(Ui.Button(actions, "取消报名", () =>
            {
                if (S.CancelRaceRegistration(track.Id, date)) _upcomingScheduleDirty = true;
                w.Rebuild();
            }, 16), 100, 36);
            if (canStart)
                Ui.Size(Ui.Button(actions, "选车并开赛", () =>
                {
                    view.ChoosingVehicle = true;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 19, Ui.ButtonHot), 168, 42);

            void Metric(int width, string label, string value, Color color)
            {
                var col = Ui.Rect(header, label);
                Ui.Size(col, width, 59, width == 0 ? 1 : 0);
                Ui.VList(col, 0, 0);
                Ui.Size(Ui.Label(col, label, width == 0 ? 22 : 15, TextAnchor.MiddleLeft, width == 0 ? Ui.Accent : Ui.Dim),
                    -1, width == 0 ? 31 : 24);
                Ui.Size(Ui.Label(col, value, width == 0 ? 15 : 21, TextAnchor.MiddleLeft, color),
                    -1, width == 0 ? 26 : 33);
            }
        }

        private void BuildRaceRosterCard(Transform row, string id, bool invited, bool arrived, bool today, RaceTrack track, GameDate date, Window w)
        {
            var npc = S.CityPerson(id);
            if (npc == null) return;
            var result = S.LastRaceOpponentResult(id);
            var button = Ui.Size(Ui.Button(row, "", () => OpenRaceRosterPerson(id, invited, arrived, result, track, date, w),
                bg: invited ? Ui.Accent : Ui.Edge), 0, 110, 1);
            var card = Ui.Panel(button.transform, "RacerCard", Ui.PanelBg2).rectTransform.Fill(1);
            var content = Ui.VList(card, 0, 6);
            content.childForceExpandHeight = true;
            var inner = Ui.Row(card, 96, 6);
            var portrait = Ui.Rect(inner, "RacerPortrait");
            Ui.Size(portrait, 57, 94);
            FitPortrait(portrait, id);
            var info = Ui.Rect(inner, "RacerDetails");
            Ui.Size(info, 0, 94, 1);
            Ui.VList(info, 2, 0).childAlignment = TextAnchor.MiddleLeft;
            Ui.Label(info, npc.Name, 18, TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
            Ui.Label(info, result == null ? "暂无成绩" : $"上次第 {result.Place} 名", 14, TextAnchor.MiddleLeft, Ui.Dim);
            if (result != null) Ui.Label(info, $"{result.Seconds:0.00} 秒", 14, TextAnchor.MiddleLeft);
            if (invited) Ui.Label(info, today && !arrived ? "受邀 · 未到" : "受你邀请", 14, TextAnchor.MiddleLeft, Ui.Accent);
        }

        private void OpenRaceRosterPerson(string id, bool invited, bool arrived, RaceOpponentResult result, RaceTrack track, GameDate date, Window parent)
        {
            var npc = S.CityPerson(id);
            OpenWindow(npc.Name, 525, 350, w =>
            {
                PersonSummary(w.Content, id, npc.Name, npc.Role, 70, () => OpenCityPersonProfile(id));
                Line(w.Content, "上次比赛成绩", 20, Ui.Accent);
                if (result == null) Line(w.Content, "暂无比赛成绩", 16, Ui.Dim);
                else
                {
                    Line(w.Content, $"第 {result.Place} / {result.Competitors} 名 · {result.Seconds:0.00} 秒", 18);
                    Line(w.Content, $"{new GameDate(result.Day)} · {RaceTracks.Get(result.TrackId)?.Name ?? result.TrackId} · {result.Vehicle}", 15, Ui.Dim);
                }
                var actions = Ui.Row(w.Content, 40, 8);
                if (arrived)
                    Ui.Size(Ui.Button(actions, "交谈", () => OpenPersonDialogue(id, parent: w, onClosed: parent.Rebuild), 17), 108, 38);
                if (invited && date.TotalDays * 1440L + RacingSchedule.MeetCloseMinute > S.Clock.Now.TotalMinutes)
                    Ui.Size(Ui.Button(actions, "取消邀请", () =>
                    {
                        if (S.CancelRaceInvitation(id, track.Id, date.TotalDays)) { w.Close(); parent.Rebuild(); }
                    }, 17), 108, 38);
            });
        }

        private void BuildRaceVehicleSelection(Transform parent, RaceTrack track, RaceReceptionView view,
            IReadOnlyList<EventRaceBooking> bookings, bool collectorTrial, Window w)
        {
            bool practice = view.Tab == RaceReceptionTab.Practice;
            bool special = view.Tab == RaceReceptionTab.Special;
            var garage = RegionalGarages.ForTrack(track.Id);
            var heading = Ui.Row(parent, 38, 8);
            if (view.ChoosingVehicle)
                Ui.Size(Ui.Button(heading, "‹ 返回赛程", () => { view.ChoosingVehicle = false; w.Rebuild(); }, 16), 117, 36);
            Ui.Size(Ui.Label(heading, garage.Name, 19, TextAnchor.MiddleLeft, Ui.Accent), 0, 38, 1);
            Ui.Size(Ui.Button(heading, "从其他车库调入", () => OpenWindow("调入" + MotorCatalog.ClassName(_raceMotorClass), 680, 575, transfer =>
                BuildMotorTransferPanel(transfer, garage, _raceMotorClass, "race:" + track.Id, () => { transfer.Close(); w.Rebuild(); })), 16), 144, 36);
            if (S.OwnedRacingGear.Count > 0 || S.Flags.Contains("racing:charity-medal"))
                Ui.Size(Ui.Button(heading, "护具", () => OpenWindow("赛车护具", 650, 530, BuildRacingEventEquipment), 16), 62, 36);
            BuildMotorClassTabs(parent, _raceMotorClass, category =>
            { _raceMotorClass = category; _raceSelection = null; w.ResetScrollOnRebuild = true; w.Rebuild(); });

            var list = Ui.Scroll(parent);
            Ui.Size((RectTransform)list.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
            var motors = S.Garage.Where(v => !v.Commuter && v.GarageId == garage.Id
                && MotorCatalog.RaceClass(MotorCatalog.Get(v.ModelId).Kind) == _raceMotorClass).ToArray();
            if (motors.All(v => v.Id != _raceSelection)) _raceSelection = motors.FirstOrDefault()?.Id;
            foreach (var car in motors)
                MotorChoice(list, car.ModelId, $"{MotorCatalog.Get(car.ModelId).Name} · 车况 {car.Condition:0}%",
                    () => { _raceSelection = car.Id; w.Rebuild(); }, selected: car.Id == _raceSelection);

            var selected = motors.FirstOrDefault(v => v.Id == _raceSelection);
            if (selected != null)
            {
                BuildMotorPerformance(list, MotorCatalog.Performance(selected), selected.Condition);
                if (selected.EventBatteryWorn) Para(list, "电池老化 · 动力下降 25% · 有概率断电退赛并支付 ¥1,500 救援费。", 16, Ui.Warn);
                if (selected.SponsorRacesRemaining > 0) Line(list, $"手机店贴纸赞助 · 正赛完赛 ¥3,000/场 · 剩余 {selected.SponsorRacesRemaining} 场", 16, Ui.Accent);
            }
            else if (!special || bookings.Count > 0)
                Line(list, S.HasGarage(garage.Id) ? "本区没有这一类入库车辆，可从其他车库调入。"
                    : "请到本区车库购买车位并停入车辆。", 16, Ui.Warn);

            if (special)
            {
                if (collectorTrial)
                {
                    string reason = S.CollectorTrialBlocker;
                    Ui.Size(Ui.Button(list, "试驾顾若宁的收藏车 · 1 圈", () =>
                    { if (S.StartCollectorTrial()) CloseWindowSilently(w); }, 18, Ui.ButtonHot, reason == null), -1, 44);
                    if (reason != null) Line(list, reason, 15, Ui.Dim);
                }
                foreach (var booking in bookings)
                {
                    string kind = booking.Kind;
                    string reason = S.EventRaceStartBlocker(kind, _raceSelection);
                    string title = kind == "wet" ? "湿地训练 · ¥1,000 · 抓地 60%"
                        : kind == "rider" ? "吴磊一圈挑战 · 输家请 ¥2,000 夜宵" : "慈善赛 · ¥2,000 · 无奖金";
                    Ui.Size(Ui.Button(list, title, () =>
                    { if (S.StartEventRace(kind, _raceSelection)) CloseWindowSilently(w); }, 18, Ui.ButtonHot, reason == null), -1, 44);
                    if (reason != null) Line(list, reason, 15, Ui.Dim);
                    Ui.Size(Ui.Button(list, "取消这项专属预约", () => { S.CancelEventRaceBooking(kind, booking.TrackId, booking.Day); w.Rebuild(); }, 16), -1, 34);
                }
                return;
            }

            string recordKey = $"{track.Id}:{_raceMotorClass}:" + (practice ? "practice" : "race");
            var record = S.RacingRecords.FirstOrDefault(r => r.Key == recordKey);
            if (record?.BestSeconds > 0) Line(list, $"个人最佳 · {record.BestSeconds:0.00} 秒", 16, Ui.Good);
            string blocker = S.RaceBlocker(track.Id, _raceSelection, practice);
            if (blocker != null) Para(parent, blocker, 16, Ui.Warn);
            int fee = practice ? RacingSchedule.PracticeFee : RacingSchedule.Fee(S.CurrentRaceMeeting);
            string start = practice ? $"开始练习 · {track.Laps} 圈 · ¥{fee:N0}" : $"开始比赛 · ¥{fee:N0}";
            var action = Ui.Size(Ui.Button(parent, start, () =>
            { if (S.StartRace(track.Id, _raceSelection, practice)) CloseWindowSilently(w); }, 19, Ui.ButtonHot, blocker == null), -1, 44);
            action.Tip("固定 1× · W/S 加速或减速 · A/D 转向 · Shift 手刹 · 空格暂停 · R 复位罚 5 秒。");
        }

        private void BuildRacingEventEquipment(Window w)
        {
            if (S.Flags.Contains("racing:charity-medal")) Line(w.Content, "已获慈善赛纪念奖牌", 16, Ui.Good);
            if (S.OwnedRacingGear.Count == 0) return;
            Line(w.Content, "赛车护具 · 当前防伤 " + (S.RacingInjuryReduction * 100).ToString("0.#") + "%", 18, Ui.Accent);
            foreach (var item in S.OwnedRacingGear)
            {
                var gear = item;
                Line(w.Content, gear.Name + " · 成色 " + gear.Condition + "% · " + (gear.Equipped ? "已装备" : "未装备"), 16);
                var row = Ui.Row(w.Content, 36, 6);
                Ui.Size(Ui.Button(row, gear.Equipped ? "卸下" : "装备", () => { S.EquipRacingGear(gear.Id, !gear.Equipped); w.Rebuild(); }, 16), -1, 36, 1);
                Ui.Size(Ui.Button(row, "整修 · ¥1,000 / 30 分钟", () => { S.RepairRacingGear(gear.Id); w.Rebuild(); }, 16, Ui.ButtonBg, gear.Condition < 100), -1, 36, 1);
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
            foreach (var w in _windows.ToArray()) CloseWindowSilently(w);
            CloseDock();
            _hudCanvas.enabled = false;
            _labelCanvas.enabled = false;
            _dockCanvas.enabled = false;
            _drivingHud = Ui.Rect(_modalCanvas.transform, "DrivingHud").Fill();
            Color surface = new Color(.06f, .085f, .12f, .88f);
            var race = S.ActiveRace;

            float timingBottom = race.Opponents.Count > 0 ? 162 : 140;
            var timing = RaceHudSurface(_drivingHud, "RaceTiming", surface).Anchor(0, 1, 0, 1, 24, -timingBottom, -338, 24);
            string mode = race.Practice ? "计时练习" : RacingSchedule.Name(race.Meeting);
            Ui.Label(timing, race.Track.Name + " · " + mode + " · 1×", 18, color: Ui.Dim)
                .rectTransform.Anchor(0, 1, 1, 1, 14, -36, 10, 8);
            _drivingLap = Ui.Label(timing, "", 36);
            _drivingLap.fontStyle = FontStyle.Bold;
            _drivingLap.rectTransform.Anchor(0, 1, 1, 1, 14, -78, 10, 34);
            _drivingStats = Ui.Label(timing, "", 17);
            _drivingStats.rectTransform.Anchor(0, 1, 1, 1, 14, -108, 10, 79);
            _drivingNearby = Ui.Label(timing, "", 16, color: Ui.Dim);
            _drivingNearby.rectTransform.Anchor(0, 1, 1, 1, 14, -132, 10, 105);

            _drivingHint = Ui.Label(_drivingHud, "", 21, color: new Color(1, .86f, .59f));
            _drivingHint.fontStyle = FontStyle.Bold;
            _drivingHint.rectTransform.Anchor(0, 1, 0, 1, 24, -timingBottom - 52, -360, timingBottom + 8);
            var hintOutline = _drivingHint.gameObject.AddComponent<Outline>();
            hintOutline.effectColor = new Color(.09f, .06f, .03f, .9f);
            hintOutline.effectDistance = new Vector2(2, -2);

            var actions = Ui.Rect(_drivingHud, "RaceActions").Anchor(1, 1, 1, 1, -426, -64, 24, 24);
            var row = Ui.Row(actions, 40, 6).Fill();
            Ui.Size(Ui.Button(row, "暂停 / 继续", () => race.Paused = !race.Paused, 16), 124, 40);
            Ui.Size(Ui.Button(row, "复位 +5秒", () => { if (!race.Paused) race.Recover(); }, 16), 106, 40);
            Ui.Size(Ui.Button(row, race.Practice ? "结束练习" : "退赛", race.Abandon, 16, Ui.Warn), 0, 40, 1);

            var controls = RaceHudSurface(_drivingHud, "RaceControls", surface).Anchor(0, 0, 0, 0, 24, 24, -602, -86);
            Ui.Label(controls, "W 油门　S 刹车 / 倒车　A/D 转向　Shift 手刹", 17)
                .rectTransform.Anchor(0, 1, 1, 1, 12, -31, 10, 4);
            Ui.Label(controls, "空格 / Esc 暂停　R 复位　右键视角　V 回正　Tab 全景", 15, color: Ui.Dim)
                .rectTransform.Anchor(0, 0, 1, 0, 12, 4, 10, -29);

            var instruments = RaceHudSurface(_drivingHud, "RaceInstruments", surface).Anchor(1, 0, 1, 0, -358, 24, 24, -168);
            _drivingSpeed = Ui.Label(instruments, "", 46);
            _drivingSpeed.fontStyle = FontStyle.Bold;
            _drivingSpeed.rectTransform.Anchor(0, 1, 1, 1, 14, -64, 118, 5);
            var badge = RaceHudSurface(instruments, "HandbrakeBadge", new Color(.26f, .17f, .07f, .94f));
            badge.Anchor(1, 1, 1, 1, -112, -50, 12, 16);
            _drivingHandbrake = Ui.Label(badge, "SHIFT 手刹", 16, TextAnchor.MiddleCenter, Ui.Accent);
            _drivingHandbrake.fontStyle = FontStyle.Bold;
            _drivingHandbrake.rectTransform.Fill();
            _drivingThrottle = BuildRacePedal(instruments, "油门", 72, new Color(.36f, .84f, .42f), out _);
            _drivingBrake = BuildRacePedal(instruments, "刹车", 106, new Color(.97f, .35f, .23f), out _drivingBrakeLabel);

            _drivingCountdown = Ui.Label(_drivingHud, "", 142, TextAnchor.MiddleCenter, new Color(1, .87f, .57f));
            _drivingCountdown.fontStyle = FontStyle.Bold;
            _drivingCountdown.rectTransform.Anchor(.5f, .59f, .5f, .59f, -210, -106, -210, -106);
            var outline = _drivingCountdown.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.18f, .085f, .02f, .94f);
            outline.effectDistance = new Vector2(4, -4);
            UpdateDrivingHud();
        }

        private static RectTransform RaceHudSurface(Transform parent, string name, Color color)
        {
            var panel = Ui.Panel(parent, name, color);
            panel.raycastTarget = false;
            return panel.rectTransform;
        }

        private static Image BuildRacePedal(RectTransform parent, string title, int top, Color color, out Text label)
        {
            label = Ui.Label(parent, title, 20);
            label.rectTransform.Anchor(0, 1, 0, 1, 14, -top - 24, -62, top);
            var rail = RaceHudSurface(parent, title + "Bar", new Color(.27f, .32f, .37f));
            rail.Anchor(0, 1, 1, 1, 68, -top - 22, 14, top + 2);
            var track = RaceHudSurface(rail, "Track", new Color(.09f, .125f, .17f)).Fill(2);
            var fill = Ui.Panel(track, "PedalFill", color);
            fill.raycastTarget = false;
            fill.rectTransform.Anchor(0, 0, 0, 1);
            return fill;
        }

        public void UpdateDrivingHud()
        {
            var race = S.ActiveRace;
            if (race == null || _drivingHud == null) return;
            _drivingLap.text = $"第 {Math.Min(race.Laps, race.Lap + 1)} / {race.Laps} 圈";
            _drivingStats.text = race.Paused ? "已暂停"
                : (race.Practice ? "" : $"第 {race.Position}/{race.CompetitorCount} 名　")
                    + $"{race.Elapsed + race.Penalty:0.0} 秒　碰撞 {race.Collisions}";
            if (race.Opponents.Count > 0)
            {
                int closest = 0;
                for (int i = 1; i < race.Opponents.Count; i++)
                    if (Math.Abs(race.RivalProgress[i] - race.Progress) < Math.Abs(race.RivalProgress[closest] - race.Progress))
                        closest = i;
                var person = S.CityPerson(race.Opponents[closest].PersonId);
                _drivingNearby.text = person == null ? "" : "最近车友 " + person.Name;
            }
            _drivingSpeed.text = $"{race.DisplayKph:0} <size=19>km/h</size>";
            _drivingThrottle.rectTransform.anchorMax = new Vector2((float)race.ThrottleInput, 1);
            _drivingBrake.rectTransform.anchorMax = new Vector2((float)race.BrakeInput, 1);
            _drivingBrakeLabel.text = race.Reversing ? "倒车" : "刹车";
            _drivingHandbrake.transform.parent.gameObject.SetActive(race.HandbrakeHeld);
            _drivingHint.text = race.Paused || !race.Drifting ? ""
                : race.HandbrakeHeld ? "松开手刹 · 修正车头" : "修正车头 · 恢复抓地";

            int lights = race.StartLights;
            _drivingCountdown.gameObject.SetActive(!race.Paused && (lights > 0 || race.ShowGo));
            if (lights > 0 || race.ShowGo)
            {
                _drivingCountdown.text = lights > 0 ? lights.ToString() : "GO!";
                _drivingCountdown.color = lights > 0 ? new Color(1, .87f, .57f) : new Color(.6f, 1f, .55f);
                float age = lights > 0 ? lights - (float)race.Countdown : (float)race.Elapsed;
                _drivingCountdown.rectTransform.localScale = Vector3.one * (1 + .22f * (1 - Mathf.Clamp01(age / .23f)));
            }
        }

        public void EndDrivingHud(string report)
        {
            if(_drivingHud!=null)Kill.It(_drivingHud.gameObject);_drivingHud=null;
            _hudCanvas.enabled=true;_labelCanvas.enabled=true;_dockCanvas.enabled=true;
            var result = S.LastRaceSettlement;
            OpenWindow(result?.Practice == true ? "练习结算" : "比赛结算", 980, 400, w =>
            {
                if (result == null) Para(w.Content, report, 20);
                else BuildRaceSettlement(w.Content, result);
                var footer = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Rect(footer, "FooterSpace"), 0, -1, 1);
                Ui.Size(Ui.Button(footer, "回到赛场接待处", () =>
                { CloseWindowSilently(w); if (S.CurrentPlace != null) OnEnteredPlace(S.CurrentPlace); }, 17, Ui.ButtonHot), 190, 40);

                // Fit the window to the full result, including any wrapped event notes.
                LayoutRebuilder.ForceRebuildLayoutImmediate(w.Content);
                var panel = (RectTransform)w.Content.parent;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 50 + LayoutUtility.GetPreferredHeight(w.Content));
                w.Fitter.Fit();
            }, scrollContent: false);
        }

        private void BuildRaceSettlement(Transform content, RaceSettlement result)
        {
            string mode = result.EventTitle ?? (result.Practice ? "计时练习" : RacingSchedule.Name(result.Meeting));
            Line(content, result.Track + " · " + mode + " · " + result.Vehicle, 21, Ui.Accent);
            var summary = Ui.Row(content, 50, 8);
            string place = result.Abandoned ? (result.Practice ? "练习已结束" : "已退赛")
                : result.Practice ? "练习完成" : $"第 {result.Position} / {result.Competitors} 名";
            Ui.Size(Ui.Label(summary, $"<b>{place}</b>", 28, TextAnchor.MiddleCenter, Ui.Accent), 0, 50, 1);
            if (!result.Practice)
                Ui.Size(Ui.Label(summary, $"<b>奖金 ¥{result.Prize:N0}</b>", 24, TextAnchor.MiddleCenter, Ui.Good), 0, 50, 1);
            Ui.Size(Ui.Label(summary, $"用时 {result.Seconds:0.00} 秒", 21, TextAnchor.MiddleCenter), 0, 50, 1);
            Line(content, $"罚时 {result.Penalty:0} 秒 · 碰撞 {result.Collisions} 次 · 车况 {result.Condition:0}%"
                + (result.Injury > 0 ? $" · 健康 -{result.Injury:0}" : " · 未受伤")
                + (result.PersonalBest ? " · 个人最佳" : ""), 16, Ui.Dim);
            if (result.Standings.Length > 0) BuildRaceStandings(content, result);
            if (!string.IsNullOrEmpty(result.Extra)) Para(content, result.Extra, 16, Ui.Accent);
        }

        private void BuildRaceStandings(Transform parent, RaceSettlement result)
        {
            var table = Ui.Rect(parent, "RaceStandings");
            Ui.VList(table, 2, 0);
            var changes = result.Relationships.ToDictionary(change => change.PersonId);
            var header = Ui.Row(table, 28, 8);
            AddRank(header, "名次", Ui.Dim);
            Ui.Size(Ui.Rect(header, "PortraitSpace"), 28);
            AddCells(header, "车手", "车辆", "用时", "交情变化", Ui.Dim, Ui.Dim);
            foreach (var entry in result.Standings)
            {
                var row = Ui.Row(table, 34, 8);
                if (entry.Player || entry.Place % 2 == 0)
                {
                    var fill = row.gameObject.AddComponent<Image>();
                    fill.color = entry.Player ? Ui.PanelBg2 : Ui.FlatBand;
                    fill.raycastTarget = false;
                }
                Color color = entry.Player ? Ui.Accent : Ui.TextColor;
                AddRank(row, entry.Place.ToString(), color);
                if (entry.Player) Ui.Size(Ui.Rect(row, "PortraitSpace"), 28);
                else PortraitThumb(row, entry.PersonId, 28, null);
                int delta = !entry.Player && changes.TryGetValue(entry.PersonId, out var change) ? change.After - change.Before : 0;
                AddCells(row, entry.Name + (entry.Invited ? " · 受邀" : ""), entry.Vehicle,
                    $"{entry.Seconds:0.00} 秒", delta == 0 ? "—" : $"{delta:+0;-0}", color,
                    delta > 0 ? Ui.Good : delta < 0 ? Ui.Warn : Ui.Dim);
            }

            void AddRank(Transform row, string label, Color color) =>
                Ui.Size(Ui.Label(row, label, 16, TextAnchor.MiddleCenter, color), 56);

            void AddCells(Transform row, string name, string vehicle, string time, string relation, Color color, Color relationColor)
            {
                Ui.Size(Ui.Label(row, name, 17, color: color), 0, -1, 1);
                Ui.Size(Ui.Label(row, vehicle, 16, color: Ui.Dim), 0, -1, 1);
                Ui.Size(Ui.Label(row, time, 16, TextAnchor.MiddleRight, color), 104);
                Ui.Size(Ui.Label(row, relation, 16, TextAnchor.MiddleCenter, relationColor), 100);
            }
        }
    }
}
