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
            if (here) OpenCityVenue(S.City.GetPlace(activity.VenueId), GameSession.IsOnsiteActivity(activity) ? id : null);
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

        private void BuildCityVenue(Window w, Place place) => BuildCityVenue(w, place, null);

        private void BuildCityVenue(Window w, Place place, string activityId)
        {
            if (place.Id == GameSession.OldMarketVenueId) { BuildOldMarket(w, place); return; }
            if (place.Id == "life_expo") { BuildExpoReception(w); return; }
            bool here = S.CurrentPlace == place;
            bool fishing = place.Id == GameSession.FishingVenueId;
            var pending = S.PendingVenueActivity(place);
            var running = S.RunningCityActivity;
            w.LiveConversation = here;
            w.LiveKey = () => string.Join("|", S.PeopleInPlace(place.Id).Select(p => p.PersonId + ":" + p.Task))
                + "|" + S.OnsiteActivityAt(place, activityId)?.Id + "|" + S.OnsiteActivityAt(place)?.Id
                + "|" + S.Ledger.Balance.Cents + "|" + S.RunningCityActivity?.Id
                + "|" + S.PendingVenueActivity(place)?.Id + "|" + S.PendingVenueActivity(place)?.ServiceConfirmed + "|" + S.PendingVenueActivity(place)?.RendezvousReady
                + "|" + string.Join("|", S.CityActivities.Where(a => a.Active && a.VenueId == place.Id).SelectMany(a => a.Participants).Select(p => p.PersonId + ":" + p.Phase));
            w.LiveStamp = w.LiveKey();
            if (BuildOnsiteAppointment(w, place, activityId))
            {
                if (running?.VenueId != place.Id)
                    Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 18), -1, 42);
                return;
            }
            Line(w.Content, (S.IsOwnedVenue(place)?"老板自用 · 免费，不计营业收入 · ":"现场消费 · ") + S.City.StreetName(place.StreetId) + " · " + CityPlaceHours(place), 19, Ui.Accent);
            if (!here)
            {
                Para(w.Content, "到场后可与店内人员交流，并选择菜单或服务。邀约请在和对方聊天时约定。", 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 19, Ui.ButtonHot), -1, 44);
            }
            if(here && IsCarryStore(place))
                Ui.Size(Ui.Button(w.Content,"购买带走的商品",()=>{CloseStoreWindows();OpenPlaceWindow(place,BuildCarryStore,StoreWindowWidth,960,holds:true,leaveOnClose:false);},17),-1,40);
            if (place.Id == "life_expo") Para(w.Content, S.CityExpoCalendar, 17, Ui.Dim);
            if (here && fishing)
            {
                if (S.FishingActive) Ui.Size(Ui.Button(w.Content, "开始操作钓竿 / 继续垂钓", OpenFishing, 20, Ui.ButtonHot), -1, 46);
                int count = FishingCatalog.Fish.Sum(fish => S.Backpack.Food.IngredientCount(fish.Id));
                Ui.Size(Ui.Button(w.Content, $"鱼获回收 · 背包 {count} 条 ›", () => OpenFishBuyback(w), 17), -1, 40);
            }
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
            Line(w.Content, fishing ? "垂钓与公园活动" : place.Business == "venue_restaurant" ? "菜单" : "服务价目表", 20, Ui.Accent);
            var list = fishing ? Ui.Scroll(w.Content, 330) : GoodsList(w.Content, 300);
            var services = CityLifeCatalog.Activities.Where(def => !def.EventOnly && def.VenueId == place.Id
                || def.EventOnly && pending?.DefinitionId == def.Id);
            if (fishing) services = services.OrderBy(def => def.EventOnly ? 0 : def.Id.StartsWith("fishing", StringComparison.Ordinal) ? 1 : 2);
            foreach (var definition in services.Select(def => def.AtVenue(place.Id)))
            for (int option = 0; option < definition.ServiceCount; option++)
            {
                var def = definition; int choice = option;
                var service = def.Service(choice);
                var row = Ui.Row(list, fishing ? 74 : service.Description == null ? 90 : 114, 10);
                string description = def.Minutes + " 分钟";
                if (!fishing)
                    description += "\n" + (service.Description == null ? "" : service.Description + "\n") + VenuePlayerEffects(service);
                Ui.Size(ProductText(row, "<b>" + GameSession.VenueServiceName(def, choice) + "</b>\n<size=15>" + description + "</size>", 18), -1, -1, 1);
                string priceText = pending?.HostPays == true ? "邀请人请客\n免费" : S.IsOwnedVenue(place) ? "老板自用\n免费"
                    : def.FixedGroupPrice ? "整场 " + S.VenuePlayerPrice(def, choice) + "\n你付 " + S.CityActivityEntryPrice(def, choice, pending)
                    : S.CityActivityEntryPrice(def, choice, pending) + (pending?.PlayerTreat == true ? "\n请客合计" : "/人");
                Ui.Size(Ui.Label(row, priceText, 18, TextAnchor.MiddleLeft, Ui.Good), 130);
                string why = here ? S.VenueServiceBlocker(def.Id, choice) : "到场后选择";
                var action = Ui.Rect(row, "ServiceAction"); Ui.VList(action, 4, 0); Ui.Size(action, 150);
                bool basketball = (def.LogicalId ?? def.Id) == "court";
                var select = Ui.Size(Ui.Button(action, basketball ? "开始打球" : place.Business == "venue_restaurant" ? "点这份餐" : fishing ? "选择" : "选择这项服务", () =>
                {
                    if (!S.SelectVenueService(def.Id, choice)) { w.Rebuild(); return; }
                    CloseAllWindows();
                    if (basketball) OpenOnsitePlay();
                }, 17, Ui.ButtonHot, why == null), -1, 42);
                if (fishing) select.Tip(string.Join("\n", new[] { service.Description, VenuePlayerEffects(service) }.Where(text => !string.IsNullOrEmpty(text))));
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
        private bool BuildOnsiteAppointment(Window w, Place place, string activityId = null)
        {
            var activity = S.OnsiteActivityAt(place, activityId) ?? S.OnsiteActivityAt(place);
            if (activity == null) return false;
            var definition = S.CityActivityService(activity);
            var card = BrandCard(w.Content, definition.Name);
            int onsite = GameSession.OnsiteNumber(activity.DefinitionId);
            bool tasting = onsite == 53;
            foreach (var person in activity.Participants)
            {
                string name = S.CityPerson(person.PersonId)?.Name ?? "同行者";
                if (onsite == 56) PersonSummary(card, person.PersonId, name, CityParticipantText(person.Phase), 52);
                else Line(card, name + " · " + CityParticipantText(person.Phase), 17);
            }
            if (activity.State == CityActivityPhase.Running)
            {
                Ui.Size(Ui.Button(card, tasting ? "继续试饮" : "继续现场活动", OpenOnsitePlay, 20, Ui.ButtonHot), -1, 48);
                Ui.Size(Ui.Button(card, "查看活动详情", () => OpenCityActivityDetail(activity.Id), 17), -1, 40);
                return true;
            }

            if (onsite == 56)
            {
                Line(card, $"{CityTime(activity.ScheduledStart)} · 五回合", 17, Ui.Dim);
                if (!S.OnsiteCompanionsPresent(activity))
                {
                    Ui.Notice(card, "等张绍文到场后再开始。");
                    Ui.Size(Ui.Button(card, "回到球场等候", w.Close, 18), -1, 42);
                    return true;
                }

                Para(card, "张绍文：咱们打五个回合。场地费是各付各的，还是你请？", 17);
                var payment = Ui.Row(card, 48, 8);
                void Payment(string label, bool treat)
                {
                    var cost = S.CityActivityEntryPrice(definition, 0, activity, treat);
                    string blocker = S.OnsiteStartBlocker(activity, treat);
                    var button = Ui.Size(Ui.Button(payment, $"{label} · {cost}", () =>
                    {
                        if (S.StartOnsiteActivity(activity.Id, treat)) OpenOnsitePlay();
                        else w.Rebuild();
                    }, 18, Ui.ButtonHot, blocker == null), -1, 48, 1);
                    if (blocker != null) button.Tip(blocker);
                }
                Payment("各付各的", false);
                Payment("这场我请", true);
                string whyNot = S.OnsiteStartBlocker(activity, false);
                if (whyNot != null) Ui.Notice(card, whyNot);
                Ui.Size(Ui.Button(card, "改天再约", () =>
                {
                    S.CancelCityActivity(activity.Id, early: true);
                    w.Rebuild();
                }, 16), -1, 36);
                return true;
            }

            var cost = S.CityActivityEntryPrice(definition, 0, activity);
            string fee = activity.HostPays ? "邀请人请客 · 免费" : cost.IsPositive ? $"应付 {cost}" : "免费";
            Line(card, $"{CityTime(activity.ScheduledStart)} · {activity.Duration} 分钟 · {fee}", 17, Ui.Dim);
            if (tasting) Para(card, "去吧台选一杯新饮品，等曾玉调好后亲自试饮。", 17);
            string why = S.OnsiteStartBlocker(activity);
            if (why != null) Ui.Notice(card, why);
            Ui.Size(Ui.Button(card, tasting ? "开始试饮" : GameSession.IsOnsiteBasketball(activity) ? "开始打球" : "开始现场活动", () =>
            {
                if (S.StartOnsiteActivity(activity.Id)) OpenOnsitePlay();
                else w.Rebuild();
            }, 20, Ui.ButtonHot, why == null), -1, 48);
            return true;
        }
    }
}
