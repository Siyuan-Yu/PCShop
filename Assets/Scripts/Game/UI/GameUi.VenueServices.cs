using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string CityActivityMenuOrTravelBlocker(string id)
        {
            var activity = S.CityActivities.FirstOrDefault(a => a.Id == id && a.Active);
            if (activity != null && S.CurrentPlace?.Id == activity.VenueId && !S.IsWorking && !S.IsGameOver)
                return null; // Browsing the current venue is not leaving or starting another activity.
            // Keep early departures clickable so the player can see when to leave.
            return S.CityActivityTravelBlocker(id, checkDepartureTime: false);
        }

        private void GoToScheduledCityActivity(string id)
        {
            var activity = S.CityActivities.FirstOrDefault(a => a.Id == id && a.Active);
            if (activity == null) { Toast("日程已经结束。"); return; }
            if (_ctrl.Placing) { Toast("请先结束摆放家具。"); return; }
            string why = CityActivityMenuOrTravelBlocker(id);
            if (why != null) { Toast(why); return; }
            bool here = S.CurrentPlace?.Id == activity.VenueId;
            if (!here && S.CityActivityEarlyDepartureHint(activity) is string early)
            {
                Popup("赴约提醒", early);
                return;
            }
            CloseAllWindows();
            if (here) OpenCityVenue(S.City.GetPlace(activity.VenueId));
            else S.GoToCityActivity(id);
        }
        private static string VenuePlayerEffects(CityVenueService service)
        {
            var effects=new List<string>();
            void Add(string name,double value){if(value!=0)effects.Add(name+" "+(value>0?"+":"")+value.ToString("0.#"));}
            Add("饱腹",service.Hunger); Add("精力",service.Energy<0?service.Energy*PlayerStats.EnergyConsumptionMultiplier:service.Energy); Add("心情",service.Mood); Add("营养",service.Nutrition);
            var completion=new List<string>();
            if(service.Health!=0)completion.Add("健康 +"+service.Health);
            if(service.Charm!=0)completion.Add("魅力 +"+service.Charm);
            if(service.Intelligence!=0)completion.Add("见识 +"+service.Intelligence);
            if(service.Relation>0)completion.Add("同行好感 +"+service.Relation);
            return string.Join(" · ",effects)+(completion.Count>0?"\n完成后："+string.Join(" · ",completion):"");
        }

        private void BuildPartnerActivityChoices(Window w, CityActivity activity)
        {
            string line = S.PartnerActivityLine(activity);
            if (line == null) return;
            Para(w.Content, line, 17, Ui.Accent);
            if (!S.CanTalkToActivityPartner(activity)) return;
            var row = Ui.Row(w.Content, 40, 6);
            void Interact(bool holdHands)
            {
                string name = S.CityPerson(S.ActivityPartnerId(activity))?.Name;
                string reply = S.InteractWithActivityPartner(activity.Id, holdHands);
                if (reply != null) Toast(name + "：" + reply);
            }
            Ui.Size(Ui.Button(row, "牵手", () => Interact(true), 16), -1, 40, 1);
            Ui.Size(Ui.Button(row, "聊聊心事", () => Interact(false), 16), -1, 40, 1);
        }

        private void BuildCityVenue(Window w, Place place)
        {
            if (place.Id == "life_expo") { BuildExpoReception(w); return; }
            bool here = S.CurrentPlace == place;
            var pending = S.PendingVenueActivity(place);
            var running = S.RunningCityActivity;
            w.LiveConversation = here;
            w.LiveKey = () => string.Join("|", S.PeopleInPlace(place.Id).Select(p => p.PersonId + ":" + p.Task))
                + "|" + S.Ledger.Balance.Cents + "|" + S.RunningCityActivity?.Id
                + "|" + S.PendingVenueActivity(place)?.Id + "|" + S.PendingVenueActivity(place)?.ServiceConfirmed
                + "|" + string.Join("|", S.CityActivities.Where(a => a.Active && a.VenueId == place.Id).SelectMany(a => a.Participants).Select(p => p.PersonId + ":" + p.Phase));
            w.LiveStamp = w.LiveKey();
            Line(w.Content, (S.IsOwnedVenue(place)?"老板自用 · 免费，不计营业收入 · ":"现场消费 · ") + S.City.StreetName(place.StreetId) + " · " + CityPlaceHours(place), 19, Ui.Accent);
            if (!here)
            {
                Para(w.Content, "到场后可与店内人员交流，并选择菜单或服务。邀约请在和对方聊天时约定。", 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 19, Ui.ButtonHot), -1, 44);
            }
            if(here && IsCarryStore(place))
                Ui.Size(Ui.Button(w.Content,"购买带走的商品",()=>{CloseStoreWindows();OpenPlaceWindow(place,BuildCarryStore,StoreWindowWidth,960,holds:true,leaveOnClose:false);},17),-1,40);
            if (place.Id == "life_expo") Para(w.Content, S.CityExpoCalendar, 17, Ui.Dim);
            if (running?.VenueId == place.Id)
            {
                Line(w.Content, "正在体验：" + GameSession.VenueServiceName(S.CityActivityService(running), Math.Max(0, running.ServiceChoice)), 19, Ui.Good);
                BuildPartnerActivityChoices(w, running);
                Ui.Size(Ui.Button(w.Content, S.ActivityPartnerId(running) == null ? "查看本次消费" : "查看约会详情",
                    () => OpenCityActivityDetail(running.Id), 18), -1, 42);
            }
            else if (pending != null)
            {
                if (pending.Participants.Length > 0)
                {
                    Line(w.Content, "今天与你相约的人", 19, Ui.Accent);
                    foreach (var person in pending.Participants)
                        Para(w.Content, (S.CityPerson(person.PersonId)?.Name ?? "朋友") + " · " + CityParticipantText(person.Phase), 17);
                }
                Para(w.Content, pending.ServiceConfirmed ? "已选好服务，与同行者一起开始。"
                    : pending.Participants.Length > 0 ? pending.RendezvousReady ? "同行者已到场，点菜或选择服务后一起开始。" : "请在约定时间前后1小时内到场集合。" : "选好项目后开始本次消费。", 17, Ui.Good);
            }
            Line(w.Content, place.Business == "venue_restaurant" ? "菜单" : "服务价目表", 20, Ui.Accent);
            var list = GoodsList(w.Content, 300);
            foreach (var definition in CityLifeCatalog.Activities.Where(def => def.VenueId == place.Id))
            for (int option = 0; option < definition.ServiceCount; option++)
            {
                var def = definition; int choice = option;
                var service = def.Service(choice);
                var row = Ui.Row(list, service.Description == null ? 90 : 114, 10);
                string description = def.Minutes + " 分钟\n"
                    + (service.Description == null ? "" : service.Description + "\n") + VenuePlayerEffects(service);
                Ui.Size(ProductText(row, "<b>" + GameSession.VenueServiceName(def, choice) + "</b>\n<size=15>" + description + "</size>", 18), -1, -1, 1);
                Ui.Size(Ui.Label(row, S.IsOwnedVenue(place)?"老板自用\n免费":S.VenuePlayerPrice(def, choice)+"/次", 20, TextAnchor.MiddleLeft, Ui.Good), 100);
                string why = here ? S.VenueServiceBlocker(def.Id, choice) : "到场后选择";
                var action = Ui.Rect(row, "ServiceAction"); Ui.VList(action, 4, 0); Ui.Size(action, 150);
                Ui.Size(Ui.Button(action, place.Business == "venue_restaurant" ? "点这份餐" : "选择这项服务", () =>
                {
                    if (!S.SelectVenueService(def.Id, choice)) { w.Rebuild(); return; }
                    CloseAllWindows();
                }, 17, Ui.ButtonHot, why == null), -1, 42);
                if (why != null) Para(action, why, 13, Ui.Dim);
            }
            if (here && place.Id == "life_expo" && S.CityExpoOpen(true))
                Ui.Size(Ui.Button(w.Content, "原创模型现货展位", OpenCityModelExpo, 18), -1, 42);
            if (here) Ui.Size(Ui.Button(w.Content, "返回街上", () =>
            {
                if (S.RunningCityActivity?.VenueId == place.Id)
                {
                    OpenCityActivityDetail(S.RunningCityActivity.Id);
                    return;
                }
                w.Close(); _ctrl.LeaveCurrentPlace();
            }, 18), -1, 42);
        }
    }
}
