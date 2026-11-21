using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class VenueWalkInOrder
        {
            public Window Window;
            public CityActivityDefinition Definition;
            public int Choice;
            public bool Honey, PlayerTreat;
            public bool LockedGuests;
            public Action OnStarted;
            public readonly List<string> Guests = new List<string>();
        }

        private VenueWalkInOrder _venueWalkInOrder;
        private Window _venueActivityCgWindow;
        private string _finishingActivityCgId;

        private static bool HasVenueActivityCg(CityActivity activity) => GameSession.IsRestaurantMeal(activity)
            || GameSession.IsParkWalkActivity(activity);

        private bool TryOpenVenueActivityCg(CityActivity activity)
        {
            if (!HasVenueActivityCg(activity)) return false;
            bool dining = GameSession.IsRestaurantMeal(activity);
            var session = S;
            var place = session.CurrentPlace;
            var definition = session.CityActivityService(activity);
            if (_venueActivityCgWindow is { Closed: false } || place?.Id != activity.VenueId
                || definition == null || !session.CanFinishManualCityActivity(activity.Id)) return true;

            string serviceName = GameSession.VenueServiceName(definition, Math.Max(0, activity.ServiceChoice));
            bool withCompanions = activity.Participants.Length > 0;
            int topicCount = (definition.Choices?.Length ?? 0) - definition.ServiceCount;
            bool hasConversation = withCompanions && topicCount > 0;
            bool walkConversation = !dining && hasConversation;
            string[] companionNames = activity.Participants.Select(p => session.CityPerson(p.PersonId)?.Name ?? "同行者").ToArray();
            var topics = Enumerable.Range(definition.ServiceCount, topicCount);
            bool HasUnusedWalkTopic() => topics.Any(topic => !activity.Choices.Contains(topic.ToString()));
            bool choosingWalkTopic = walkConversation && HasUnusedWalkTopic();
            string startText = dining ? "上菜中…" : "走上步道…";
            string activityText = dining ? (withCompanions ? "和同行者一起用餐…" : "用餐中…")
                : (withCompanions ? $"和{string.Join("、", companionNames)}沿步道散步…" : "沿步道散步…");
            float playback = 0f;
            float duration = hasConversation ? 7f : 4.5f;
            float walkStop = duration * .5f;
            _cityActivityHud?.gameObject.SetActive(false);
            _venueActivityCgWindow = OpenWindow(serviceName, 0, 0, w =>
            {
                var header = Ui.Panel(w.Content, "ActivityCgHeader", Ui.Chrome).rectTransform.Anchor(0, .92f, 1, 1);
                Ui.Label(header, place.Name, 20).rectTransform.Anchor(.03f, 0, .27f, 1);
                Ui.Label(header, serviceName, 28, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Anchor(.27f, 0, .73f, 1);
                Ui.Label(header, $"{(dining ? "用餐" : "散步")} · {activity.Duration} 分钟", 18, TextAnchor.MiddleRight)
                    .rectTransform.Anchor(.73f, 0, .97f, 1);

                var scene = Ui.Rect(w.Content, dining ? "RestaurantMealCg" : "ParkWalkCg").Anchor(.035f, .12f, .965f, .90f);
                Action<float> animateScene = dining ? BuildRestaurantMealCg(scene, activity, RestaurantDishArt(definition, activity.ServiceChoice))
                    : BuildParkWalkCg(scene, activity, duration, session.CityIsRaining, session.Clock.Now.Hour is >= 18 or < 6);

                var footer = Ui.Panel(w.Content, "ActivityCgFooter", Ui.Chrome).rectTransform.Anchor(.035f, .026f, .965f, .105f);
                var status = Ui.Label(footer, startText, 19, hasConversation ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                status.rectTransform.Anchor(.026f, 0, hasConversation ? .44f : .974f, 1);
                RectTransform actions = null;
                if (hasConversation)
                {
                    actions = Ui.Row(footer, -1, 8).Anchor(.44f, .15f, .98f, .85f);
                    actions.name = "ActivityCgConversation";
                    foreach (int choice in topics)
                    {
                        bool used = activity.Choices.Contains(choice.ToString());
                        Button button = null;
                        button = Ui.Button(actions, definition.Choices[choice], () =>
                        {
                            if (walkConversation)
                            {
                                if (!choosingWalkTopic || !OpenParkWalkConversation(activity, definition, choice, () =>
                                    { if (!HasUnusedWalkTopic()) choosingWalkTopic = false; })) return;
                                button.interactable = false;
                                return;
                            }
                            if (session.ChooseCityActivity(activity.Id, choice)) button.interactable = false;
                        }, 17, Ui.SelectedButton(used), !used);
                        Ui.Size(button, 0, -1, 1);
                    }
                    if (walkConversation)
                    {
                        Ui.Size(Ui.Button(actions, "继续散步", () => choosingWalkTopic = false, 17, Ui.ButtonHot), 0, -1, 1);
                        actions.gameObject.SetActive(choosingWalkTopic);
                    }
                }

                w.RefreshContent = () =>
                {
                    if (S != session || session.RunningCityActivity?.Id != activity.Id || session.CurrentPlace != place)
                    {
                        w.Close();
                        return;
                    }
                    if (_windows.LastOrDefault() != w || !Application.isFocused) return;
                    playback += Mathf.Min(Time.unscaledDeltaTime, .1f);
                    if (choosingWalkTopic) playback = Mathf.Min(playback, walkStop);
                    if (walkConversation) actions.gameObject.SetActive(choosingWalkTopic);
                    animateScene(playback);
                    status.text = playback < .7f ? startText : choosingWalkTopic && playback >= walkStop ? "在湖边歇一会儿…" : activityText;
                    if (playback < duration) return;

                    CompleteVenueActivityCg(w, activity);
                };
            }, closable: false, scrollContent: false, showTitleBar: false, fullScreen: true);
            return true;
        }

        private bool OpenParkWalkConversation(CityActivity activity, CityActivityDefinition definition, int choice, Action onClosed)
        {
            var session = S;
            var companions = activity.Participants.Where(p => p.Phase == CityParticipantPhase.Participating && session.CitySamePlace(p.PersonId))
                .Select(p => session.CityPerson(p.PersonId)).Where(person => person != null).ToArray();
            if (companions.Length == 0 || activity.Choices.Contains(choice.ToString())
                || !session.ChooseCityActivity(activity.Id, choice)) return false;

            bool sharingExperience = definition.Choices[choice] == "分享经历";
            var first = companions[0];
            OpenDialogue(first.Id, first.Name, session.CityPersonRole(first.Id), "", null, onClosed: onClosed,
                start: ctx => ShowTopic(ctx, 0, false));
            return true;

            void ShowTopic(TalkCtx ctx, int index, bool followUp)
            {
                var person = companions[index];
                ctx.SpeakerId = person.Id;
                string line = sharingExperience && !followUp ? session.SharePersonalExperience(person.Id)
                    : session.CityTalk(person.Id, followUp && !sharingExperience ? "interest" : "life");
                var replies = new List<TalkOpt>();
                if (!followUp)
                    replies.Add(new TalkOpt { Label = sharingExperience ? "那你最近过得怎么样？" : "平时喜欢做些什么？",
                        Act = () => ShowTopic(ctx, index, true) });
                if (index + 1 < companions.Length)
                    replies.Add(new TalkOpt { Label = $"听{companions[index + 1].Name}说说", Act = () => ShowTopic(ctx, index + 1, false) });
                replies.Add(new TalkOpt { Label = "回到步道", Hot = true, Act = () => ctx.W.Close() });
                ctx.Show(string.IsNullOrWhiteSpace(line) ? "难得有空出来走走。我们在这儿多待一会儿吧。" : line, replies.ToArray());
            }
        }

        private void CompleteVenueActivityCg(Window window, CityActivity activity)
        {
            var session = S;
            var place = session.CurrentPlace;
            bool finished;
            _finishingActivityCgId = activity.Id;
            try { finished = session.FinishManualCityActivity(activity.Id); }
            finally { _finishingActivityCgId = null; }
            if (!finished) return;

            window.Close();
            if (S != session || session.CurrentPlace != place || session.IsGameOver) return;
            void ReturnToVenue()
            {
                if (S == session && session.CurrentPlace == place && !session.IsGameOver && !AnyModalOpen)
                    OpenCityVenue(place);
            }

            if (activity.State == CityActivityPhase.Completed && activity.Participants.Length == 0 && GameSession.IsRestaurantMeal(activity))
            {
                OpenCityActivitySettlement(activity.Id, ReturnToVenue);
                return;
            }
            ReturnToVenue();
            OpenActivityFarewell(activity);
        }

        private static string RestaurantDishArt(CityActivityDefinition definition, int choice)
        {
            if (definition.Id == "hotpot") return "prop_hotpot";
            if (definition.Id == "snack") return choice == 0 ? "prop_noodles" : "prop_tastingtofu";
            if (definition.Id is "dinner" or "dinner_tier2" or "dinner_tier3" or "dinner_tier4")
                return choice == 0 ? "prop_meal" : "prop_stew";
            return "prop_stew";
        }

        private static RectTransform BuildActivityCgFrame(Transform parent, string name, Texture2D background)
        {
            var scene = Ui.Rect(parent, name).Fill();
            var fit = scene.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = background != null ? (float)background.width / background.height : 16f / 9;
            scene.gameObject.AddComponent<RectMask2D>();
            return scene;
        }

        private static Action<float> BuildRestaurantMealCg(Transform parent, CityActivity activity, string dishArt)
        {
            var background = Resources.Load<Texture2D>("EventCg/friend_meal");
            var scene = BuildActivityCgFrame(parent, "DiningTable", background);
            if (background != null) EventCgImage(scene, "RestaurantInterior", background, true);

            const float tableEdge = .28f;
            var guests = activity.Participants;
            float portraitWidth = Mathf.Min(.28f, .86f / Mathf.Max(1, guests.Length));
            for (int i = 0; i < guests.Length; i++)
            {
                var portrait = ScenePersonTexture(guests[i].PersonId, true, out bool seated);
                if (portrait == null) continue;
                float center = guests.Length == 1 ? .73f : Mathf.Lerp(.43f, .86f, i / (float)(guests.Length - 1));
                var seat = Ui.Rect(scene, "DiningCompanion").Anchor(center - portraitWidth * .5f, tableEdge,
                    center + portraitWidth * .5f, .95f);
                var image = EventCgImage(seat, guests[i].PersonId, portrait);
                image.rectTransform.pivot = new Vector2(.5f, 0f);
                if (!seated)
                {
                    // Frame standing portrait art at the waist, behind the tabletop.
                    const float visibleHeight = .69f;
                    image.uvRect = new Rect(0, 1 - visibleHeight, 1, visibleHeight);
                    image.GetComponent<AspectRatioFitter>().aspectRatio /= visibleHeight;
                }
            }

            var tabletop = Ui.Rect(scene, "MealDishes").Fill();
            var dishes = tabletop.gameObject.AddComponent<CanvasGroup>();
            dishes.alpha = 0;
            dishes.blocksRaycasts = false;
            if (guests.Length > 0 || dishArt == "prop_hotpot")
            {
                Dish("prop_tastingrice", .27f, .125f, .44f, .278f);
                Dish("prop_tastingtofu", .69f, .101f, .88f, .265f);
            }
            Dish(dishArt, .35f, .035f, .75f, .322f);
            return elapsed =>
            {
                float served = Mathf.SmoothStep(0, 1, elapsed / .7f);
                dishes.alpha = served;
                tabletop.anchoredPosition = new Vector2(0, 18f * (1 - served));
            };

            void Dish(string resource, float left, float bottom, float right, float top)
            {
                var texture = Resources.Load<Texture2D>("EventCg/" + resource);
                if (texture == null) return;
                var holder = Ui.Rect(tabletop, resource).Anchor(left, bottom, right, top);
                EventCgImage(holder, "Dish", texture);
            }
        }

        private static Action<float> BuildParkWalkCg(Transform parent, CityActivity activity, float duration, bool raining, bool evening)
        {
            var backdrop = Resources.Load<Texture2D>("EventCg/event_park_trail");
            var scene = BuildActivityCgFrame(parent, "ParkTrail", backdrop);
            var scenery = Ui.Rect(scene, "Scenery").Anchor(-.045f, -.045f, 1.045f, 1.045f);
            var light = evening ? new Color(.68f, .72f, .82f) : raining ? new Color(.79f, .85f, .91f) : Color.white;
            if (backdrop != null) EventCgImage(scenery, "Trail", backdrop, true).color = light;

            float direction = activity.ServiceChoice == 1 ? -1 : 1;
            return elapsed =>
            {
                float pan = Mathf.SmoothStep(-.025f, .025f, elapsed / duration) * direction;
                scenery.anchoredPosition = Vector2.right * (scene.rect.width * pan);
            };
        }

        private void SelectVenueWalkIn(Window w, CityActivityDefinition definition, int choice, Action onStarted, bool honey = false,
            IReadOnlyList<string> lockedGuests = null)
        {
            var order = new VenueWalkInOrder
            {
                Window = w, Definition = definition, Choice = choice, Honey = honey, OnStarted = onStarted, LockedGuests = lockedGuests != null
            };
            if (lockedGuests != null) order.Guests.AddRange(lockedGuests);
            _venueWalkInOrder = order;
            w.OnClosed += () => { if (_venueWalkInOrder == order) _venueWalkInOrder = null; };
            w.RequestRebuild();
        }

        private bool BuildVenueWalkIn(Window w, Transform parent)
        {
            var order = _venueWalkInOrder;
            if (order?.Window != w) return false;

            var def = order.Definition;
            bool bar = S.CurrentPlace?.Business == "venue_bar";
            int limit = def.WalkInCapacity(order.Choice);
            int count = order.Guests.Count + 1;
            var unitPrice = S.VenuePlayerPrice(def, order.Choice);
            if (bar) Line(parent, "和谁一起喝", 19, Ui.Accent);
            var title = Ui.Row(parent, 38, 8);
            Ui.Size(Ui.Label(title, GameSession.VenueServiceName(def, order.Choice), 23, color: Ui.Accent), 0, 38, 1);
            Ui.Size(Ui.Label(title, $"{count} / {limit} 人（含你）", 17, TextAnchor.MiddleRight, Ui.Accent), 134, 38);

            string priceUnit = def.Id == GameSession.ParkRowingActivityId ? "／条船" : def.FixedGroupPrice ? "／整场" : "／人";
            Line(parent, $"{def.Minutes} 分钟 · " + (unitPrice.IsPositive ? unitPrice + priceUnit : "免费"), 16, Ui.Dim);
            var people = order.LockedGuests ? order.Guests.ToArray() : S.PeopleInPlace(def.VenueId)
                .Where(p => S.WalkInVenueCompanionBlocker(p.PersonId, def) == null)
                .Select(p => p.PersonId).Concat(order.Guests).Distinct().ToArray();
            if (people.Length == 0)
                Para(parent, "现在没有可以一起参加的人。", 16, Ui.Dim);
            else
            {
                const int columns = 2;
                var list = people.Length > columns * 4 && parent.GetComponentInParent<ScrollRect>() == null
                    ? Ui.Scroll(parent, 219) : parent;
                for (int i = 0; i < people.Length; i += columns)
                {
                    var row = Ui.Row(list, 52, 8);
                    foreach (string id in people.Skip(i).Take(columns))
                    {
                        bool selected = order.Guests.Contains(id);
                        string unavailable = S.WalkInVenueCompanionBlocker(id, def);
                        var cell = Ui.Size(Ui.Rect(row, "VenueGuest_" + id), 0, 52, 1);
                        Ui.VList(cell, 0, 0);
                        var guest = PersonSummary(cell, id, S.CityPerson(id)?.Name ?? id,
                            unavailable ?? (selected ? "已选为同行" : "在场"), 52);
                        bool full = count >= limit;
                        Ui.Size(Ui.Button(guest, order.LockedGuests ? "本次同行" : selected ? "✓ 已选" : full ? "已满" : "选为同行", () =>
                        {
                            if (!order.Guests.Remove(id) && order.Guests.Count + 1 < limit)
                                order.Guests.Add(id);
                            w.RequestRebuild();
                        }, 15, Ui.SelectedButton(selected), !order.LockedGuests && (selected || !full && unavailable == null)), 86, 36);
                    }
                    if (columns == 2 && i + 1 == people.Length)
                        Ui.Size(Ui.Rect(row, "EmptyColumn"), 0, 52, 1);
                }
            }

            var total = S.VenueServiceTotal(def, order.Choice, count);
            var due = S.VenueServicePayment(def, order.Choice, count, order.PlayerTreat);
            if (total.IsPositive)
            {
                var payment = Ui.Row(parent, 38, 6);
                if (count > 1)
                {
                    Ui.Size(Ui.Button(payment, "AA", () => { order.PlayerTreat = false; w.RequestRebuild(); },
                        16, Ui.SelectedButton(!order.PlayerTreat)), 82, 36);
                    Ui.Size(Ui.Button(payment, "我请客", () => { order.PlayerTreat = true; w.RequestRebuild(); },
                        16, Ui.SelectedButton(order.PlayerTreat)), 100, 36);
                }
                Ui.Size(Ui.Label(payment, $"合计 {total}　实付 {due}", 18, TextAnchor.MiddleRight, Ui.Accent), 0, 38, 1);
            }

            string reason = S.VenueServiceBlocker(def.Id, order.Choice, walkInCompanions: order.Guests, playerTreat: order.PlayerTreat);
            if (reason != null) Para(parent, reason, 15, Ui.Warn);
            var actions = Ui.Row(parent, 44, 8);
            Ui.Size(Ui.Button(actions, "返回", () => { _venueWalkInOrder = null; w.RequestRebuild(); }, 17), 100, 42);
            string start = bar ? (count == 1 ? "自己喝" : "一起喝") : count == 1 ? "自己开始" : "一起开始";
            Ui.Size(Ui.Button(actions, start, () =>
            {
                if (_venueWalkInOrder != order) return;
                if (!S.SelectVenueService(def.Id, order.Choice, barHoneyAdded: order.Honey,
                    walkInCompanions: order.Guests, playerTreat: order.PlayerTreat))
                {
                    w.RequestRebuild();
                    return;
                }
                _venueWalkInOrder = null;
                order.OnStarted();
            }, 18, Ui.ButtonHot, reason == null), 0, 42, 1).name = "VenueWalkInStart";
            return true;
        }
    }
}
