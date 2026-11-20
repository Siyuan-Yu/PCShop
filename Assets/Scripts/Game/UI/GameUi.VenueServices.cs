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
            if (here) OpenCityVenue(S.City.GetPlace(activity.VenueId), id);
            else S.GoToCityActivity(id);
        }
        private string _openedCookTasting;
        private GameSession _cookTastingSession;

        private void CheckCookTasting()
        {
            if (!ReferenceEquals(_cookTastingSession, S))
            {
                _cookTastingSession = S;
                _openedCookTasting = null;
            }
            var activity = S.RunningCityActivity;
            if (MainMenuOpen || AnyModalOpen || PopupOpen || _ctrl.Placing || S.IsGameOver
                || !GameSession.IsCookTasting(activity) || activity.Id == _openedCookTasting
                || S.CurrentPlace?.Id != activity.VenueId) return;
            OpenCookTasting(activity.Id);
        }

        private bool TryOpenCookTasting(Place place, string activityId)
        {
            if (place == null || S.CurrentPlace != place || S.IsWorking || S.IsGameOver) return false;
            var activity = S.RunningCityActivity;
            if (!GameSession.IsCookTasting(activity) || activity.VenueId != place.Id)
                activity = S.PendingVenueActivity(place, activityId);
            if (!GameSession.IsCookTasting(activity)) return false;
            if (activity.State != CityActivityPhase.Running) S.PrepareVenueCompanions(place, activity.Id);
            OpenCookTasting(activity.Id);
            return true;
        }

        private void OpenCookTasting(string id)
        {
            OpenWindow("罗师傅的新菜试吃", 900, 590, w =>
            {
                var activity = S.CityActivities.FirstOrDefault(a => a.Id == id && a.Active);
                if (!GameSession.IsCookTasting(activity) || S.CurrentPlace?.Id != activity.VenueId)
                {
                    Para(w.Content, "这次试吃已经结束。", 19);
                    return;
                }

                if (activity.State != CityActivityPhase.Running)
                {
                    w.DialogIllustration = BuildEventCg(w.Content, new EventCgFrame("friend_meal", personId: "res_cook", seated: true), 300);
                    Para(w.Content, "罗师傅给你留了座。「今天试三道菜，我一道一道上。你尝完告诉我实话。」", 20);
                    string blocker = S.VenueAppointmentStartBlocker(activity);
                    Line(w.Content, blocker ?? $"罗师傅招待 · 约 {activity.Duration} 分钟", 17, blocker == null ? Ui.Dim : Ui.Warn);
                    Ui.Size(Ui.Button(w.Content, "入座试吃", () =>
                    {
                        if (S.StartVenueAppointment(id)) w.Rebuild();
                    }, 20, Ui.ButtonHot, blocker == null), -1, 46);
                    return;
                }

                _cookTastingSession = S;
                _openedCookTasting = id;
                int dishIndex = GameSession.CookTastingDishIndex(activity);
                var dish = GameSession.CookTastingDishes[dishIndex];
                int review = GameSession.CookTastingReviewIndex(activity, dishIndex);
                bool tasted = GameSession.HasTastedCooksDish(activity, dishIndex);
                Line(w.Content, $"第 {dishIndex + 1} / {GameSession.CookTastingDishes.Count} 道 · {dish.Name}", 22, Ui.Accent);
                w.DialogIllustration = BuildEventCg(w.Content, new EventCgFrame("friend_meal", dish.Art, "res_cook", seated: true), 300, featureDish: true);

                if (!tasted)
                {
                    Para(w.Content, dish.Served, 20);
                    Ui.Size(Ui.Button(w.Content, $"试吃{dish.Name}", () =>
                    {
                        if (S.TasteCooksDish(id, dishIndex)) w.Rebuild();
                    }, 20, Ui.ButtonHot), -1, 46).name = "CookTastingTaste";
                }
                else if (review < 0)
                {
                    Para(w.Content, dish.Tasted, 20);
                    if (!string.IsNullOrEmpty(activity.CookTastingEffects))
                        Line(w.Content, activity.CookTastingEffects, 17, Ui.Accent);
                    for (int i = 0; i < dish.Opinions.Length; i++)
                    {
                        int choice = i;
                        Ui.Size(Ui.Button(w.Content, dish.Opinions[i], () =>
                        {
                            if (S.ReviewCooksDish(id, dishIndex, choice) != null) w.Rebuild();
                        }, 19), -1, 42).name = $"CookTastingReview_{dishIndex}_{choice}";
                    }
                }
                else
                {
                    Para(w.Content, dish.Replies[review], 20);
                    if (dishIndex + 1 < GameSession.CookTastingDishes.Count)
                        Ui.Size(Ui.Button(w.Content, "上下一道菜", () =>
                        {
                            if (S.ServeNextCooksDish(id, dishIndex)) w.Rebuild();
                        }, 20, Ui.ButtonHot), -1, 46);
                    else
                        Ui.Size(Ui.Button(w.Content, "结束试吃，和罗师傅道别", () =>
                        {
                            if (S.FinishManualCityActivity(id)) CloseWindowSilently(w);
                        }, 20, Ui.ButtonHot, S.CanFinishManualCityActivity(id)), -1, 46).name = "FinishCookTasting";
                }

                Ui.Size(Ui.Button(w.Content, "提前离开", () => ConfirmEndCityActivity(activity, false), 17), -1, 38);
            }, fitContent: true);
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
            return string.Join(" · ",effects)+(completion.Count>0?"\n完成后："+string.Join(" · ",completion):"")
                + (service.Relation > 0 ? "\n本场同行好感最多 +" + GameSession.CityActivityRelationLimit(service) : "");
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

        private void BuildVenuePasses(Window w, Transform parent, CityActivityDefinition[] definitions,
            CityActivity booking, bool here, string action, string buttonPrefix, Action openPlay)
        {
            foreach (var definition in definitions)
            {
                var row = Ui.Row(parent, 56, 10);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                int minutes = booking?.Duration ?? definition.Minutes;
                int attempts = CityLifeCatalog.ManualPlayAttempts(minutes, definition);
                bool football = definition.Id == CityLifeCatalog.FootballPenaltyId;
                bool confirmed = booking?.ServiceConfirmed == true;
                var price = S.CityActivityEntryPrice(definition, confirmed ? booking.ServiceChoice : 0, booking);
                string duration = minutes % 60 == 0 ? $"{minutes / 60} 小时" : $"{minutes} 分钟";
                string blocker = !here ? "到场后可开始" : confirmed ? S.VenueAppointmentStartBlocker(booking) : S.VenueServiceBlocker(definition.Id, 0);
                string fare = booking?.HostPays == true ? "邀请人请客"
                    : booking?.EntryPaid == true ? "费用已支付" : price.IsPositive ? price.ToString() : "免费";
                string startLabel = booking == null ? "购买并开始" : price.IsPositive && !booking.EntryPaid ? "付款并开始" : "开始";
                var info = Ui.Label(row, $"<b>{(football ? "点球 " : "")}{attempts} {(football ? "球" : "次")}</b>  <size=15>全部用完结算 {duration}</size>"
                    + (blocker == null ? "" : $"\n<size=14>{blocker}</size>"), 20);
                Ui.Size(info, 0, -1, 1);
                Ui.Size(Ui.Label(row, fare, 17, TextAnchor.MiddleRight, Ui.Good), 112);
                string buttonText = football
                    ? booking == null ? "购买并踢点球" : price.IsPositive && !booking.EntryPaid ? "付款并踢点球" : "开始踢点球"
                    : startLabel + action;
                var button = Ui.Button(row, buttonText, () =>
                {
                    bool started = confirmed ? S.StartVenueAppointment(booking.Id) : S.SelectVenueService(definition.Id, 0);
                    if (started && S.RunningCityActivity?.VenueId == definition.VenueId) openPlay();
                    else w.Rebuild();
                }, 16, Ui.ButtonBg, blocker == null).Tip(blocker);
                Ui.Size(button, 156, 36).name = buttonPrefix + definition.Id;
            }
        }

        private void OpenSportVenue(Place place, string activityId)
        {
            bool here = S.CurrentPlace == place;
            if (here) S.PrepareVenueCompanions(place, activityId);
            bool court = place.Business == "venue_court";
            string action = court ? "打球" : "练习挥杆";
            OpenWindow(place.Name, 1060, 620, w =>
            {
                if (BuildOnsiteAppointment(w, place, activityId)) return;
                var booking = here ? S.PendingVenueActivity(place, activityId) : null;
                if (!GameSession.IsVenueSport(booking)) booking = null;
                var goods = BuildRosterFrame(w, place, 460, scrollGoods: true, rosterWidth: 165);
                Line(goods, booking != null ? S.CityActivityService(booking).Name
                    : place.Id == CityLifeCatalog.CommunityFootballVenueId ? "足球点球与投篮"
                    : "购买" + (court ? "投篮" : "击球") + "次数", 24, Ui.Accent);
                Para(goods, $"{CityPlaceHours(place)} · 操作时暂停游戏时钟，结束后按已用次数结算时间。", 17, Ui.Dim);
                var definitions = booking == null ? CityLifeCatalog.SportPasses(place.Id) : new[] { S.CityActivityService(booking) };
                BuildVenuePasses(w, goods, definitions, booking, here, action, "SportPass_", OpenOnsitePlay);
                if (here)
                    Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 18), -1, 46);
                else
                    Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 18, Ui.ButtonHot), -1, 46);
            }, scrollContent: false);
        }

        private void BuildCityVenue(Window w, Place place, string activityId)
        {
            if (place.Id == GameSession.OldMarketVenueId) { BuildOldMarket(w, place, activityId); return; }
            if (place.Id == "life_expo") { BuildExpoReception(w); return; }
            bool here = S.CurrentPlace == place;
            bool park = place.Id == "life_hike" || place.Id == GameSession.CommunityParkId;
            var pending = S.PendingVenueActivity(place, activityId);
            var running = S.RunningCityActivity;
            w.LiveConversation = here;
            w.LiveKey = () => string.Join("|", S.PeopleInPlace(place.Id).Select(p => p.PersonId + ":" + p.Task))
                + "|" + S.OnsiteActivityAt(place, activityId)?.Id + "|" + S.OnsiteActivityAt(place)?.Id
                + "|" + S.Ledger.Balance.Cents + "|" + S.RunningCityActivity?.Id
                + "|" + S.PendingVenueActivity(place, activityId)?.Id + "|" + S.PendingVenueActivity(place, activityId)?.ServiceConfirmed + "|" + S.PendingVenueActivity(place, activityId)?.RendezvousReady
                + "|" + string.Join("|", S.CityActivities.Where(a => a.Active && a.VenueId == place.Id).SelectMany(a => a.Participants).Select(p => p.PersonId + ":" + p.Phase));
            w.LiveStamp = w.LiveKey();
            if (BuildOnsiteAppointment(w, place, activityId))
            {
                if (running?.VenueId != place.Id)
                    Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 18), -1, 42);
                return;
            }
            string heading = place.Id == GameSession.CommunityParkId ? "湖边步道与码头" : park ? "公园步道" : S.IsOwnedVenue(place) ? "老板自用 · 免费，不计营业收入" : "现场消费";
            Line(w.Content, heading + " · " + S.City.StreetName(place.StreetId) + " · " + CityPlaceHours(place), 19, Ui.Accent);
            if (!here)
            {
                Para(w.Content, place.Id == GameSession.CommunityParkId ? "到场后可沿湖散步，也可在码头租船。"
                    : park ? "到公园后可以选择散步路线。" : "到场后可与店内人员交流，并选择菜单或服务。邀约请在和对方聊天时约定。", 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 19, Ui.ButtonHot), -1, 44);
            }
            if(here && IsCarryStore(place))
                Ui.Size(Ui.Button(w.Content,"购买带走的商品",()=>{CloseStoreWindows();OpenPlaceWindow(place,BuildCarryStore,StoreWindowWidth,960,holds:true,leaveOnClose:false);},17),-1,40);
            if (place.Id == "life_expo") Para(w.Content, S.CityExpoCalendar, 17, Ui.Dim);
            if (pending?.ServiceConfirmed == true && running?.VenueId != place.Id && S.CityActivityService(pending) is CityActivityDefinition reserved)
            {
                BuildConfirmedVenueAppointment(w, pending, reserved, here);
                if (here) Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 18), -1, 42);
                return;
            }
            if (running?.VenueId == place.Id)
            {
                var activeService = S.CityActivityService(running);
                Line(w.Content, "正在体验：" + GameSession.VenueServiceName(activeService, Math.Max(0, running.ServiceChoice)), 19, Ui.Good);
                BuildPartnerActivityChoices(w, running);
                if (place.Business == "venue_ktv")
                    Ui.Size(Ui.Button(w.Content, "打开包间 · 接唱或听歌", OpenKaraokeRoom, 18, Ui.ButtonHot), -1, 42);
                if (GameSession.IsBarDrinkService(activeService))
                    Ui.Size(Ui.Button(w.Content, "查看我点的饮品", () => OpenBarCounter(activeService,
                        running.ServiceChoice, running, ordered: true), 18), -1, 42);
                string details = activeService?.EventOnly == true ? "查看活动详情" : S.ActivityPartnerId(running) == null ? "查看本次消费" : "查看约会详情";
                Ui.Size(Ui.Button(w.Content, details,
                    () => OpenCityActivityDetail(running.Id), 18), -1, 42);
                if (activeService?.EventOnly == true) return;
            }
            else if (pending != null)
            {
                if (pending.Participants.Length > 0)
                {
                    Line(w.Content, "今天与你相约的人", 19, Ui.Accent);
                    foreach (var person in pending.Participants)
                        Para(w.Content, (S.CityPerson(person.PersonId)?.Name ?? "朋友") + " · " + CityParticipantText(person.Phase), 17);
                }
                bool walk = GameSession.IsParkWalk(S.CityActivityService(pending));
                string ready = walk ? "选择散步路线后开始。" : pending.Participants.Length > 0 ? "同行者已到场，点菜或选择服务后一起开始。" : "选好项目后开始本次消费。";
                Para(w.Content, pending.Participants.Length > 0 && !pending.RendezvousReady ? "请在约定时间前后1小时内到场集合。" : ready, 17, Ui.Good);
            }
            Line(w.Content, park ? "散步路线与活动" : place.Business == "venue_restaurant" ? "菜单" : "服务价目表", 20, Ui.Accent);
            var list = GoodsList(w.Content, 300);
            var bookedService = S.CityActivityService(pending);
            var services = bookedService != null && (bookedService.EventOnly || bookedService.Id == "dinner") ? new[] { bookedService }
                : CityLifeCatalog.Activities.Where(def => !def.EventOnly && def.VenueId == place.Id
                    && place.Business is not ("venue_court" or "venue_golf"));
            foreach (var definition in services.Select(def => def.AtVenue(place.Id)))
            {
                if (GameSession.IsBarDrinkService(definition))
                {
                    BuildBarCounterEntry(list, definition, pending, here, running?.VenueId == place.Id);
                    continue;
                }
                for (int option = 0; option < definition.ServiceCount; option++)
                    BuildServiceRow(definition, option);
            }

            void BuildServiceRow(CityActivityDefinition def, int choice)
            {
                var service = def.Service(choice);
                var row = Ui.Row(list, service.Description == null ? 90 : 114, 10);
                string description = def.Minutes + " 分钟\n" + (service.Description == null ? "" : service.Description + "\n") + VenuePlayerEffects(service);
                Ui.Size(ProductText(row, "<b>" + GameSession.VenueServiceName(def, choice) + "</b>\n<size=15>" + description + "</size>", 18), -1, -1, 1);
                bool walk = GameSession.IsParkWalk(def);
                string priceText = walk ? "免费" : pending?.HostPays == true ? "邀请人请客\n免费" : S.IsOwnedVenue(place) ? "老板自用\n免费"
                    : def.FixedGroupPrice ? "整场 " + S.VenuePlayerPrice(def, choice) + "\n你付 " + S.CityActivityEntryPrice(def, choice, pending)
                    : S.CityActivityEntryPrice(def, choice, pending) + (pending?.PlayerTreat == true ? "\n请客合计" : "/人");
                Ui.Size(Ui.Label(row, priceText, 18, TextAnchor.MiddleLeft, Ui.Good), 130);
                string why = here ? S.VenueServiceBlocker(def.Id, choice, pending?.Id) : "到场后选择";
                var action = Ui.Rect(row, "ServiceAction"); Ui.VList(action, 4, 0); Ui.Size(action, 150);
                string actionText = walk ? "选这条路线" : def.Id == GameSession.ParkRowingActivityId ? "租船上湖"
                    : place.Business == "venue_restaurant" ? "点这份餐" : "选择这项服务";
                Ui.Size(Ui.Button(action, actionText, () =>
                {
                    if (!S.SelectVenueService(def.Id, choice, pending?.Id)) { w.Rebuild(); return; }
                    CloseAllWindows();
                    if (S.ParkRowingActive) OpenParkRowing();
                    else if (place.Business == "venue_ktv") OpenKaraokeRoom();
                }, 17, Ui.ButtonBg, why == null), -1, 42);
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
        private void BuildConfirmedVenueAppointment(Window w, CityActivity booking, CityActivityDefinition definition, bool here)
        {
            var card = BrandCard(w.Content, definition.Name);
            Line(card, $"{CityTime(booking.ScheduledStart)} · {booking.Duration} 分钟", 16, Ui.Dim);
            foreach (var person in booking.Participants)
                Line(card, (S.CityPerson(person.PersonId)?.Name ?? "同行者") + " · " + CityParticipantText(person.Phase), 17);
            var price = S.CityActivityEntryPrice(definition, booking.ServiceChoice, booking);
            string payment = booking.HostPays ? "邀请人请客 · 免费" : booking.EntryPaid ? "费用已支付"
                : price.IsPositive ? $"开始时支付 {price}" : "免费参加";
            Line(card, payment, 18, Ui.Good);
            if (!here) return;

            string why = S.VenueAppointmentStartBlocker(booking);
            if (why != null) Ui.Notice(card, why);
            if (GameSession.IsBarDrinkService(definition))
            {
                Ui.Size(Ui.Button(card, "看饮品 · 选口味", () => OpenBarCounter(definition,
                    booking.ServiceChoice, booking), 19, Ui.ButtonHot, why == null), -1, 48);
                return;
            }
            string start = price.IsPositive && !booking.EntryPaid ? $"支付 {price} 并开始"
                : booking.HostPays || !booking.EntryPaid ? "开始活动 · 免费" : "开始活动";
            Ui.Size(Ui.Button(card, start, () =>
            {
                if (!S.StartVenueAppointment(booking.Id)) { w.Rebuild(); return; }
                if (GameSession.IsOutdoorMarketVisit(booking)) { OpenOutdoorMarketVisit(booking); return; }
                CloseAllWindows();
                if (GameSession.IsManualPlayActivity(booking)) OpenCityActivityDetail(booking.Id);
                else if (S.CurrentPlace?.Business == "venue_ktv") OpenKaraokeRoom();
                else S.Time.SetPaused(false);
            }, 20, Ui.ButtonHot, why == null), -1, 48).name = "StartVenueAppointment";
        }

        private bool BuildOnsiteAppointment(Window w, Place place, string activityId = null)
        {
            var activity = S.OnsiteActivityAt(place, activityId) ?? S.OnsiteActivityAt(place);
            if (activity == null) return false;
            var definition = S.CityActivityService(activity);
            var card = BrandCard(w.Content, definition.Name);
            int onsite = GameSession.OnsiteNumber(activity.DefinitionId);
            bool tasting = onsite == 53;
            bool drinkTasting = GameSession.IsDrinkTasting(activity);
            foreach (var person in activity.Participants)
            {
                string name = S.CityPerson(person.PersonId)?.Name ?? "同行者";
                if (onsite == 56) PersonSummary(card, person.PersonId, name, CityParticipantText(person.Phase), 52);
                else Line(card, name + " · " + CityParticipantText(person.Phase), 17);
            }
            if (activity.State == CityActivityPhase.Running)
            {
                Ui.Size(Ui.Button(card, drinkTasting ? "继续品尝" : tasting ? "继续试饮" : "继续现场活动", OpenOnsitePlay, 20, Ui.ButtonHot), -1, 48);
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
                    }, 18, Ui.ButtonBg, blocker == null), -1, 48, 1);
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
            if (drinkTasting) Para(card, "按喜欢的顺序品尝三杯，完成后统一结算时间。", 17);
            string why = S.OnsiteStartBlocker(activity);
            if (why != null) Ui.Notice(card, why);
            Ui.Size(Ui.Button(card, drinkTasting ? "开始品尝" : tasting ? "开始试饮" : GameSession.IsOnsiteBasketball(activity) ? "开始打球" : "开始现场活动", () =>
            {
                if (S.StartOnsiteActivity(activity.Id)) OpenOnsitePlay();
                else w.Rebuild();
            }, 20, Ui.ButtonHot, why == null), -1, 48);
            return true;
        }
    }
}
