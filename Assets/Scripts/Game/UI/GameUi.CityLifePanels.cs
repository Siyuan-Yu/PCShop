using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _cityMapSearch = "", _cityMapStreet, _cityMapType;
        private int _cityMapPage;
        private RectTransform _cityActivityHud;
        private Text _cityActivityHudText;
        private Button _cityActivityGo, _cityActivityDetails, _cityActivityEnd;
        private string _cityHudActivityId;
        private float _cityHudRefresh;
        private GameSession _cityHudSession;
        private readonly HashSet<string> _dismissedCityActivityHud = new HashSet<string>();

        public void OpenCityLife()
        {
            OpenWindow("城市生活" + (S.IsCityLifeTest ? " · 独立测试档" : ""), 900, 700, w =>
            {
                Para(w.Content, "经营、认识街坊与参加活动都发生在同一个城市。地图负责导航；查看人物只看已知资料，线下相处需要真实到场。", 19);
                Ui.Size(Ui.Button(w.Content, "城市地图", OpenCityMap, 20, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, "通讯录", OpenContactsPanel, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "日程", OpenSchedulePanel, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "人脉经营故事", OpenCityStories, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "资产 · 门店与自有物业", OpenCityFinanceOverview, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "市场变化 / 模型目录", OpenCityRetailPanel, 20), -1, 50);
                if (S.CurrentPlace != null && CityLifeCatalog.Activities.Any(a => !a.EventOnly && a.VenueId == S.CurrentPlace.Id))
                    Ui.Size(Ui.Button(w.Content, "当前场所：" + S.CurrentPlace.Name, () => OpenCityVenue(S.CurrentPlace), 20), -1, 50);
#if UNITY_EDITOR
                Ui.Size(Ui.Button(w.Content, "城市生活验收预置（独立测试档）", OpenCityAcceptance, 18, Ui.Warn), -1, 46);
#endif
                if (S.CityIntent != null || S.Trip != null) Ui.Size(Ui.Button(w.Content, "取消当前导航", () => { S.StopTrip(); S.CancelCityTravel(); w.Rebuild(); }, 18), -1, 44);
            });
        }

        private static string CityTime(long minute) => new GameTimestamp(Math.Max(0, minute)).ToString();
        private string CityPlaceHours(Place p)
        {
            if(S.IsOwnedVenue(p)){var v=S.VenueOperation(p.Id);return v.Open==v.Close?"全天开放":$"{v.Open/60:00}:{v.Open%60:00}—{v.Close/60:00}:{v.Close%60:00}";}
            var shop = S.ShopAt(p);
            if (shop == null && GameSession.SupplierClosesOnWeekend(p)) return S.OpeningHoursText(p);
            int open = shop?.Sim.OpenMinute ?? (p.ExplicitEntrance ? p.OpenMinute : 0);
            int close = shop?.Sim.CloseMinute ?? (p.ExplicitEntrance ? p.CloseMinute : 0);
            if (open == close || open == 0 && close == 1440) return "24小时营业";
            return $"{open / 60:00}:{open % 60:00}—{(close < open ? "次日 " : "")}{close / 60:00}:{close % 60:00}";
        }
        private bool CityMapTypeMatches(Place p) => _cityMapType == null
            || _cityMapType == "my" && S.IsMyShop(p)
            || _cityMapType == "property" && S.PropertyAt(p.Id) != null
            || _cityMapType == "venue" && CityLifeCatalog.Activities.Any(a => !a.EventOnly && a.VenueId == p.Id)
            || _cityMapType == "source" && (p.Kind == PlaceKind.Wholesaler || S.HasRetailWholesale(p))
            || _cityMapType == "home" && p.Kind == PlaceKind.Home;
        private int CityMapPriority(Place p) => S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? 0 : S.CityFavorites.Contains(p.Id) ? 1 : S.CityRecent.Contains(p.Id) ? 2 : 3;
        private string CityMapPlaceName(Place place) => S.ShopAt(place) is OwnedShop shop ? S.ShopTitle(shop) : place.Name;
        private string CityMapPlaceType(Place place) => S.ShopAt(place) is OwnedShop shop
            ? RetailShopTypes.Name(shop.RetailKind) : place.Business ?? place.Kind.ToString();

        public void OpenCityMap()
        {
            _cityMapPage = 0;
            var trialNodes = GameSession.FirstChapterTrial ? TrialCityMapNodes() : null;
            OpenWindow(GameSession.FirstChapterTrial ? "城郊地图" : "十街地图", 1110, 790, w =>
            {
                Ui.Size(Ui.TextInput(w.Content, _cityMapSearch, "搜索地点名、街区、用途", value => { _cityMapSearch = value ?? ""; _cityMapPage = 0; w.RequestRebuild(); }), -1, 44);
                var types = Ui.Row(w.Content, 40, 6);
                void Type(string title, string key) => Ui.Size(Ui.Button(types, title, () => { _cityMapType = key; _cityMapPage = 0; w.Rebuild(); }, 17, Ui.SelectedButton(_cityMapType == key)), -1, 40, 1);
                Type("全部", null); Type("我的店", "my"); Type("已购物业", "property"); Type("娱乐活动", "venue"); Type("采购货源", "source"); Type("住处", "home");
                var streets = S.City.Streets.Where(street => !GameSession.FirstChapterTrial || S.City.AgencyDistrictFor(street.id)?.id == "outskirts").ToArray();
                for (int i = 0; i < streets.Length; i += 5)
                {
                    var row = Ui.Row(w.Content, 38, 6);
                    if (i == 0) Ui.Size(Ui.Button(row, "全部街区", () => { _cityMapStreet = null; _cityMapPage = 0; w.Rebuild(); }, 16, Ui.SelectedButton(_cityMapStreet == null)), -1, 38, 1);
                    foreach (var street in streets.Skip(i).Take(5))
                    {
                        string id = street.id;
                        Ui.Size(Ui.Button(row, street.name, () => { _cityMapStreet = id; _cityMapPage = 0; w.Rebuild(); }, 16, Ui.SelectedButton(_cityMapStreet == id)), -1, 38, 1);
                    }
                }
                if (S.CityDefaultDestination != null && S.City.HasPlace(S.CityDefaultDestination))
                    Line(w.Content, "底栏自定目的地：" + CityMapPlaceName(S.City.GetPlace(S.CityDefaultDestination)), 17, Ui.Accent);
                if (S.CityIntent != null || S.Trip != null) Ui.Size(Ui.Button(w.Content, "取消当前路线（预约保留）", () => { S.StopTrip(); S.CancelCityTravel(); w.Rebuild(); }, 18, Ui.Warn), -1, 42);
                BuildCityRouteMap(w.Content, trialNodes);
                var places = CityMapPlaces().Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)
                    && (_cityMapSearch.Length == 0 || (CityMapPlaceName(p) + " " + CityMapPlaceType(p) + " " + S.City.StreetName(p.StreetId)).IndexOf(_cityMapSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(CityMapPriority).ThenBy(p => p.StreetId).ThenBy(CityMapPlaceName).ToArray();
                _cityMapPage = Math.Max(0, Math.Min(_cityMapPage, Math.Max(0, (places.Length - 1) / 12)));
                Line(w.Content, "当前邀约优先，其次收藏和最近前往；地图不显示陌生人的实时位置。", 17, Ui.Dim);
                foreach (var p in places.Skip(_cityMapPage * 12).Take(12))
                {
                    var place = p;
                    string tag = S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? "[邀约] " : S.CityFavorites.Contains(p.Id) ? "★ " : S.CityRecent.Contains(p.Id) ? "[最近] " : "";
                    Ui.Size(Ui.Button(w.Content, tag + CityMapPlaceName(p) + " · " + S.City.StreetName(p.StreetId) + " · " + CityMapPlaceType(p) + "\n" + CityPlaceHours(p) + (S.IsMyShop(p) ? " · 我的店" : S.PropertyAt(p.Id) != null ? " · 已持有物业" : ""), () => OpenCityPlaceDetail(place), 18), -1, 68).name="CityMapList_"+p.Id;
                }
                if (places.Length == 0) Line(w.Content, "没有匹配地点，可清除搜索或切换筛选。", 18, Ui.Dim);
                var pages = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(pages, "上一页", () => { _cityMapPage--; w.Rebuild(); }, 18, Ui.ButtonBg, _cityMapPage > 0), -1, 40, 1);
                Ui.Size(Ui.Label(pages, $"第 {_cityMapPage + 1} 页 · {places.Length} 个地点", 17, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(pages, "下一页", () => { _cityMapPage++; w.Rebuild(); }, 18, Ui.ButtonBg, (_cityMapPage + 1) * 12 < places.Length), -1, 40, 1);
            });
        }

        private HashSet<string> TrialCityMapNodes()
        {
            string start = S.City.HasPlace("agency") ? S.City.GetPlace("agency").NodeId : S.Home.NodeId;
            var visible = new HashSet<string>(StringComparer.Ordinal) { start };
            var pending = new Queue<string>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                string node = pending.Dequeue();
                var from = S.City.ViewPosition(node);
                foreach (var edge in S.City.Map.NeighborsOf(node))
                {
                    if (visible.Contains(edge.To) || S.TryDistrictRoadStop(from, S.City.ViewPosition(edge.To), out _, out _)) continue;
                    visible.Add(edge.To);
                    pending.Enqueue(edge.To);
                }
            }
            return visible;
        }

        private void BuildCityRouteMap(Transform parent, HashSet<string> trialNodes)
        {
            var nodes = S.City.Map.Nodes.Where(node => trialNodes == null || trialNodes.Contains(node.Id)).ToArray();
            if (nodes.Length == 0) return;
            double minX = nodes.Min(n => n.X), minY = nodes.Min(n => n.Y), width = Math.Max(1, nodes.Max(n => n.X) - minX), height = Math.Max(1, nodes.Max(n => n.Y) - minY);
            Vector2 Point(double x, double y) => new Vector2(.05f + .9f * (float)((x - minX) / width), .08f + .84f * (float)((y - minY) / height));
            var background = Ui.Panel(parent, "CityRouteMap", Ui.PanelBg2).rectTransform; Ui.Size(background, -1, 240);
            var roads = Ui.Rect(background, "AuthoritativeRoads").Fill(); roads.gameObject.AddComponent<CanvasRenderer>();
            var graphic = roads.gameObject.AddComponent<CityRouteMapGraphic>(); graphic.color = new Color(.52f, .59f, .51f);
            var segments = new List<Vector2>();
            foreach (var node in nodes) foreach (var edge in S.City.Map.NeighborsOf(node.Id))
            {
                if (string.CompareOrdinal(node.Id, edge.To) >= 0 || (trialNodes != null && !trialNodes.Contains(edge.To))) continue;
                var other = S.City.Map.GetNode(edge.To); segments.Add(Point(node.X, node.Y)); segments.Add(Point(other.X, other.Y));
            }
            graphic.SetSegments(segments);
            foreach (var p in CityMapPlaces().Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)))
            {
                var place = p;var door=S.DoorOf(p);
                Vector2 at=Point(door.X*S.City.DistanceScale,door.Y*S.City.DistanceScale);
                var button = Ui.Button(background, S.IsMyShop(p) ? "店" : p.Kind == PlaceKind.Home ? "宅" : CityLifeCatalog.Activities.Any(a => !a.EventOnly && a.VenueId == p.Id) ? "娱" : "·", () => OpenCityPlaceDetail(place), 14,
                    S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? Ui.Warn : S.IsMyShop(p) || S.PropertyAt(p.Id) != null ? Ui.ButtonHot : Ui.ButtonBg);
                button.name="CityMap_"+p.Id;
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = at; rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(25, 23);
                button.Tip(CityMapPlaceName(p) + " · " + S.City.StreetName(p.StreetId));
            }
            var marker = Ui.Label(background, "我", 18, TextAnchor.MiddleCenter, Ui.Accent); var player = S.PlayerPosition;
            var playerAt = Point(player.X * S.City.DistanceScale, player.Y * S.City.DistanceScale);
            marker.raycastTarget = false; marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = playerAt; marker.rectTransform.pivot = new Vector2(.5f, .5f); marker.rectTransform.sizeDelta = new Vector2(26, 26);
        }

        private IEnumerable<Place> CityMapPlaces() => S.City.PublicPlaces.Where(place => !GameSession.FirstChapterTrial || S.DistrictAccessBlocker(place) == null);

        private void OpenCityPlaceDetail(Place place)
        {
            OpenWindow(CityMapPlaceName(place), 810, 650, w =>
            {
                Line(w.Content, S.City.StreetName(place.StreetId) + " · " + CityMapPlaceType(place), 21, Ui.Accent);
                Line(w.Content, "开放：" + CityPlaceHours(place), 19);
                int minutes = S.CityTravelMinutes(place);
                Line(w.Content, minutes >= int.MaxValue / 8 ? "当前路线不可达，请换一处目的地。" : "当前交通方式预计 " + minutes + " 游戏分钟；按真实路线出发。", 18, minutes >= int.MaxValue / 8 ? Ui.Warn : Ui.Dim);
                if (S.ClosedReason(place) != null) Para(w.Content, S.ClosedReason(place) + "。可以走到门口，抵达后会再次检查进入条件。", 17, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, "前往" + CityMapPlaceName(place), () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 20, Ui.ButtonHot, minutes < int.MaxValue / 8), -1, 48);
                bool favorite = S.CityFavorites.Contains(place.Id);
                Ui.Size(Ui.Button(w.Content, favorite ? "取消收藏" : "收藏地点（最多5个）", () => { S.SetCityFavorite(place.Id, !favorite); w.Rebuild(); }, 18), -1, 44);
                Ui.Size(Ui.Button(w.Content, "设为底栏自定目的地", () => { S.SetCityDefault(place.Id); RefreshShortcutMenus(); CloseAllWindows(); }, 18), -1, 44);
                var shortcuts = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(shortcuts, "替换中介快捷位", () => { S.SetCityShortcut(place.Id, true); Toast("快捷位已保存。"); }, 17), -1, 44, 1);
                Ui.Size(Ui.Button(shortcuts, "替换农贸快捷位", () => { S.SetCityShortcut(place.Id, false); Toast("快捷位已保存。"); }, 17), -1, 44, 1);
                foreach (var a in S.CityActivities.Where(a => a.Active && a.VenueId == place.Id))
                { string id = a.Id; Ui.Size(Ui.Button(w.Content, "查看此处邀约：" + S.CityActivityService(a)?.Name, () => OpenCityActivityDetail(id), 18), -1, 42); }
                if (CityLifeCatalog.Activities.Any(a => !a.EventOnly && a.VenueId == place.Id)) Ui.Size(Ui.Button(w.Content, "场所活动详情", () => OpenCityVenue(place), 18), -1, 44);
            });
        }

        public void OpenCityPeople() => OpenContactsPanel();

        /// <summary>名望一行字：数值 + 档位；看别人时再和玩家自己比一比。</summary>
        private string StandingText(double value, bool compareWithPlayer)
        {
            string text = "名望 " + value.ToString("0") + " · " + PersonStanding.Label(value);
            if (!compareWithPlayer || !S.HasShop) return text;
            double mine = S.PlayerShop.Reputation;
            return text + (value > mine + 2 ? " · 比你高" : value < mine - 2 ? " · 比你低" : " · 和你相当");
        }
        public void OpenCityPerson(string personId) => OpenPersonDialogue(personId);
        public void OpenCityPersonProfile(string personId, Action onClosed = null, bool fromConversation = false)
        {
            var definition = S.CityPerson(personId);
            var npc = Npcs.Get(personId);
            var staff = Rosters.Get(personId) ?? Residents.Get(personId)?.Person;
            string name = definition?.Name ?? npc?.Name ?? staff?.Name;
            if (name == null) { Toast("人物资料暂不可用。"); return; }
            int memoryPage = 0;
            OpenWindow(name + " · 人物资料", 800, 800, w =>
            {
                var state = S.CityPersonState(personId);
                string[] topics = state?.KnownTopics ?? Array.Empty<string>();
                if (fromConversation)
                {
                    w.LiveConversation = true;
                    w.LiveKey = () => ConversationViewKey(personId) + "|" + S.MemoriesOf(personId).Count() + "|" + S.LearnedPersonStories(personId).Length;
                    w.LiveStamp = w.LiveKey();
                }
                var top = Ui.Row(w.Content, 270, 16);
                PortraitCard(top, personId, name, "", 145, null);
                var info = Ui.Rect(top, "PersonDetails"); Ui.VList(info, 6, 0); Ui.Size(info, -1, -1, 1);
                Line(info, PersonNameWithKolBadge(personId, name), 25, Ui.Accent);
                if(NpcNames.Alias(personId) is string alias)Line(info,"熟人称呼："+alias,15,Ui.Dim);
                Para(info, definition != null ? S.CityPersonRole(personId) : npc?.Role ?? staff?.Role, 18);
                Line(info, "熟悉度 " + S.Relation(personId), 17, Ui.Good);
                Line(info, StandingText(S.PersonStandingOf(personId), true), 17, Ui.Accent).name = "PersonStanding";
                Ui.Size(Ui.Bar(info, S.Relation(personId) / 100f, Ui.Accent), -1, 8);
                Para(info, S.IsMet(personId) ? "已存入通讯录" : S.Flags.Contains("heard:" + personId) ? "从熟人口中听说过 · 尚未与本人交谈" : "首次交谈后加入通讯录", 16, Ui.Dim);
                BuildWorkAttributeRow(info, S.CityPersonAttributes(personId));
                if (!fromConversation)
                {
                    var actions=Ui.Row(w.Content,38,6);
                    bool nearby=S.CitySamePlace(personId);
                    Ui.Size(Ui.Button(actions,"现场交谈",()=>OpenPersonDialogue(personId),17,Ui.ButtonBg,nearby),-1,38,1).Tip(nearby?"与面前的人交谈":"需要实际到同一地点");
                    bool saved=S.SavedConversationContacts().Any(c=>c.Id==personId);
                    Ui.Size(Ui.Button(actions,"打电话",()=>OpenPersonDialogue(personId,parent:w,remote:true),17,Ui.ButtonBg,saved),-1,38,1).Tip(saved?"使用通讯录里的联系方式":"先当面聊一聊");
                    string inviteBlocker = S.CityInviteBlocker(personId);
                    Ui.Size(Ui.Button(actions, inviteBlocker ?? "邀约", () => OpenInvitePanel(personId), 16, Ui.ButtonBg, inviteBlocker == null), -1, 38, 1);
                }
                Line(w.Content, "关于这个人", 20, Ui.Accent);
                var traits = S.ConversationTraits(personId);
                for (int i = 0; i < traits.Length; i++)
                {
                    if (!traits[i].Known) continue;
                    string label = i == 0 || personId == "res_cook" ? "兴趣 · " : "";
                    Para(w.Content, label + S.ConversationTraitText(personId, i), 17);
                }
                if (S.PersonRelationshipText(personId) is string relationship)
                    Line(w.Content, "感情状态 · " + relationship, 17, S.IsPlayerPartner(personId) ? Ui.Accent : Ui.Dim);
                if (NpcPerks.Describe(personId) is string perk && !string.IsNullOrEmpty(perk))
                {
                    Para(w.Content, "交情好处 · " + perk, 16, Ui.Dim);
                    if (npc?.IsTipster == true) Para(w.Content, TrustText(npc), 16, Ui.Dim);
                }
                if (definition != null)
                    Line(w.Content, topics.Contains("life") ? "年龄：" + definition.Age + " 岁" : "年龄：尚未聊起", 17);
                bool publicWork = state != null && S.CurrentPlace != null && state.LocationPlaceId == state.WorkPlaceId && state.LocationPlaceId == S.CurrentPlace.Id;
                string workplace = topics.Contains("work") || publicWork
                    ? state?.WorkPlaceId != null && S.City.HasPlace(state.WorkPlaceId) ? S.City.GetPlace(state.WorkPlaceId).Name : "目前没有固定工作地点"
                    : "尚未聊起";
                Para(w.Content, "工作地点：" + workplace, 17);
                Para(w.Content, "生活与住处：" + (topics.Contains("life") && state != null ? state.Address : "尚未聊起"), 17, Ui.Dim);
                if (topics.Contains("life") && state != null && S.City.HasPlace(state.HomePlaceId))
                {
                    var home = S.City.GetPlace(state.HomePlaceId);
                    Ui.Size(Ui.Button(w.Content, "前往住址", () => { CloseAllWindows(); _ctrl.GoToPlace(home); },
                        17, Ui.ButtonBg, S.DistrictAccessBlocker(home) == null), -1, 38);
                }

                Line(w.Content, "生平与故事", 20, Ui.Accent);
                var stories = S.LearnedPersonStories(personId);
                if (stories.Length == 0) Para(w.Content, "还没有听对方讲过自己的经历。以后随便聊聊时，慢慢了解。", 17, Ui.Dim);
                else
                {
                    foreach (var story in stories) Para(w.Content, story, 18);
                    Para(w.Content, "这里只整理已经亲口听过的经历；尚未讲述的故事不会提前展开。", 15, Ui.Dim);
                }
                var relations = S.PersonCircle(personId).Where(relation => (state?.KnownRelations ?? Array.Empty<string>()).Contains(relation.PersonId)).ToArray();
                if (relations.Length > 0)
                {
                    Line(w.Content, "提起过的人", 20, Ui.Accent);
                    foreach (var relation in relations) Para(w.Content, relation.Name + " · " + relation.Relationship, 17, Ui.Dim);
                }
                Line(w.Content, "与你的相处记录", 20, Ui.Accent);
                var memories = S.MemoriesOf(personId).ToArray();
                const int pageSize = 8;
                int pages = Math.Max(1, (memories.Length + pageSize - 1) / pageSize);
                memoryPage = Math.Min(memoryPage, pages - 1);
                if (memories.Length == 0) Para(w.Content, "还没有实际相处记录。", 17, Ui.Dim);
                else
                {
                    foreach (var memory in memories.Skip(memoryPage * pageSize).Take(pageSize))
                    {
                        Line(w.Content, CityTime(memory.Minute), 15, Ui.Dim);
                        Para(w.Content, memory.Text, 17);
                    }
                    if (pages > 1)
                    {
                        var nav = Ui.Row(w.Content, 38);
                        Ui.Size(Ui.Button(nav, "上一页", () => { memoryPage--; w.Rebuild(); }, 16, Ui.ButtonBg, memoryPage > 0), 100);
                        Ui.Size(Ui.Label(nav, $"第 {memoryPage + 1}/{pages} 页 · 共 {memories.Length} 条", 16, TextAnchor.MiddleCenter), -1, 38, 1);
                        Ui.Size(Ui.Button(nav, "下一页", () => { memoryPage++; w.Rebuild(); }, 16, Ui.ButtonBg, memoryPage + 1 < pages), 100);
                    }
                }
                Ui.Size(Ui.Button(w.Content, fromConversation ? "返回对话" : "关闭资料", w.Close, 19), -1, 44);
            }, !fromConversation, true, onClosed);
        }

        public void OpenCityVenue(Place place, string activityId = null)
        {
            if (place == null) return;
            if (TryOpenSignHelp(place, activityId)) return;
            if (place.Id == GameSession.FishingVenueId) { OpenFishingVenue(place, activityId); return; }
            if (S.CurrentPlace == place && place.Business == GameSession.HospitalBusiness)
            {
                if (S.RaceAidAt(place) != null)
                {
                    CloseAllWindows();
                    S.Time.SetPaused(false);
                }
                else OnEnteredPlace(place);
                return;
            }
            var running = S.RunningCityActivity;
            bool here = S.CurrentPlace?.Id == place.Id;
            if (here && running?.VenueId == place.Id && GameSession.IsCookTasting(running))
            {
                OpenCookTasting(running.Id);
                return;
            }
            if (here && running?.VenueId == place.Id && S.CityActivityService(running)?.Id == "hike")
            {
                OpenCityActivityDetail(running.Id);
                return;
            }
            if (here && running?.VenueId == place.Id && GameSession.IsOutdoorMarketVisit(running))
            {
                OpenOutdoorMarketVisit(running);
                return;
            }
            if (here && running?.VenueId == place.Id && S.CurrentOnsite != null)
            {
                OpenOnsitePlay();
                return;
            }
            if (place.Business is "venue_court" or "venue_golf")
            {
                var appointment = !here ? null : running?.VenueId == place.Id ? running : S.PendingVenueActivity(place, activityId);
                if (appointment == null || GameSession.IsOnsiteSport(appointment))
                {
                    OpenSportVenue(place, activityId);
                    return;
                }
            }
            if (place.Id == GameSession.OldMarketVenueId)
            {
                if (here) S.PrepareVenueCompanions(place, activityId);
                OpenWindow(place.Name, 900, 700, w => BuildOldMarket(w, place, activityId));
                return;
            }
            if (place.Id == "life_expo") { OpenExpoReceptionForActivity(activityId); return; }
            if (place.Id == GameSession.CommunityMcnId) { OpenCommunityMcn(); return; }
            if (S.CurrentPlace != place)
            {
                OpenWindow(place.Name + " · 服务价目表", 950, 740, w => BuildCityVenue(w, place));
                return;
            }
            // Browsing a venue menu uses the same pause hold as other shop menus.
            // Closing releases only this hold and keeps the player at the venue.
            S.PrepareVenueCompanions(place, activityId);
            OpenPlaceWindow(place, (w, p) => BuildCityVenue(w, p, activityId), 1140, 900, holds: true, leaveOnClose: false);
        }

        private static readonly string[] InvitationCategories = { "全部", "餐饮", "休闲", "运动户外", "逛市场与展会" };
        private static readonly (string Label, Func<CityVenueService, int> Value)[] InvitationAttributes =
        {
            ("熟悉度上限", service => service.Relation > 0 ? GameSession.CityActivityRelationLimit(service) : 0), ("心情", service => service.Mood),
            ("精力", service => service.Energy), ("健康", service => service.Health),
            ("饱腹", service => service.Hunger), ("营养", service => service.Nutrition),
            ("见识", service => service.Intelligence), ("魅力", service => service.Charm),
        };

        private static int InvitationCategory(CityActivityDefinition activity)
        {
            if (activity == null) return 3;
            switch (activity.Id.Split('_')[0])
            {
                case "dinner": case "bar": return 1;
                case "ktv": return 2;
                case "court": case "golf": case "hike": case "fishing": return 3;
                case "market": case "old": case "oldmarket": case "expo": return 4;
                default: return 2;
            }
        }

        private static string InvitationInterest(CityActivityDefinition activity, CityPersonDefinition person, string personId)
        {
            if (activity == null) return "赛车";
            if (personId == Npcs.Zhou && activity.Id == "old_market") return "旧收音机与评书";
            string interest = person?.Interest ?? "";
            if (activity.Id == interest)
                return activity.Id == "dinner" ? "吃家常饭" : activity.Name;
            string[] keywords;
            switch (activity.Id)
            {
                case "dinner": keywords = new[] { "美食", "家常菜", "聚餐" }; break;
                case "bar": keywords = new[] { "饮品", "调酒" }; break;
                case "ktv": keywords = new[] { "唱歌", "歌唱", "合唱", "旋律" }; break;
                case "court": keywords = new[] { "篮球", "打球", "社区运动" }; break;
                case "golf": keywords = new[] { "高尔夫" }; break;
                case "hike": keywords = new[] { "徒步", "散步", "户外", "生态" }; break;
                case "fishing": keywords = new[] { "钓鱼", "垂钓" }; break;
                case "old_market": keywords = new[] { "旧收音机", "评书", "老唱片" }; break;
                case "expo_comic": keywords = new[] { "原创模型", "潮玩" }; break;
                case "expo_electronics": keywords = new[] { "布展", "展览" }; break;
                default: return null;
            }
            return keywords.FirstOrDefault(keyword => interest.Contains(keyword));
        }

        private string InvitationActivityInfo(CityActivityDefinition activity, Place venue)
        {
            if (activity == null) return "正式比赛 · 场次和费用按赛场显示";
            string price = S.VenuePriceRange(activity);
            if (Enumerable.Range(0, activity.ServiceCount).All(i => S.VenuePlayerPrice(activity, i) == Money.Zero))
                price = "免费";
            else if (activity.FixedGroupPrice)
                price += "／整场";
            return $"{venue.Name} · {activity.Minutes} 分钟 · {price}";
        }

        public void OpenCityActivityChooser(string personId = null)
        {
            if (S.CityInviteBlocker(personId) is string reason) { Toast(reason); return; }
            var person = S.CityPerson(personId);
            string name = person?.Name ?? "朋友";
            bool choosingRace = false;
            string raceTrack = RaceTracks.Get(S.CurrentPlace?.Id)?.Id;
            int category = 0, attribute = 0, sort = 0, page = 0;
            const int pageSize = 5;
            string selectedId = null;
            OpenWindow("邀请活动", 1100, 690, w =>
            {
                PersonSummary(w.Content, personId, name, $"{S.CityPersonRole(personId)} · 熟悉度 {S.Relation(personId)}", 62);
                if (choosingRace)
                {
                    Ui.Size(Ui.Button(w.Content, "‹ 返回活动列表", () =>
                    { choosingRace = false; w.ResetScrollOnRebuild = true; w.Rebuild(); }, 17), -1, 38);
                    BuildRaceInvitationChoices(w, personId, raceTrack, track => raceTrack = track);
                    return;
                }
                void ResetList() { page = 0; selectedId = null; w.RequestRebuild(); }
                var tabs = Ui.Row(w.Content, 40);
                for (int i = 0; i < InvitationCategories.Length; i++)
                {
                    int index = i;
                    Ui.Size(Ui.Button(tabs, InvitationCategories[i], () => { category = index; ResetList(); },
                        18, Ui.SelectedButton(category == i)), i == 4 ? 150 : i == 3 ? 112 : 66);
                }
                var filters = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Label(filters, "提升属性", 17), 76);
                Ui.Size(Ui.Choice(filters, new[] { "不限" }.Concat(InvitationAttributes.Select(a => a.Label)),
                    attribute, value => { attribute = value; ResetList(); }), 145, 40);
                Ui.Size(Ui.Rect(filters, "FilterSpacer"), -1, -1, 1);
                Ui.Size(Ui.Label(filters, "排序", 17), 40);
                Ui.Size(Ui.Choice(filters, new[] { "按" + name + "喜好", "费用从低到高", "时长从短到长" },
                    sort, value => { sort = value; ResetList(); }), 190, 40);

                var activities = CityLifeCatalog.Activities.Where(activity => !activity.EventOnly && activity.Id != "snack"
                    && (activity.IsSportPass || (activity.LogicalId ?? activity.Id) is not ("court" or "golf"))
                    && S.City.HasPlace(activity.VenueId)
                    && S.DistrictAccessBlocker(S.City.GetPlace(activity.VenueId)) == null).ToList();
                if (GameSession.LikesRacing(personId)) activities.Add(null);

                var results = activities
                    .Where(activity => category == 0 || InvitationCategory(activity) == category)
                    .Select(activity =>
                    {
                        var services = activity == null ? Array.Empty<CityVenueService>()
                            : Enumerable.Range(0, activity.ServiceCount).Select(activity.Service).ToArray();
                        var venue = activity == null ? null : S.PurchaseDestination(S.City.GetPlace(activity.VenueId));
                        return (Activity: activity, Id: activity?.Id ?? "racing", Title: activity?.Name ?? "一起参赛",
                            Interest: InvitationInterest(activity, person, personId), Services: services,
                            Info: InvitationActivityInfo(activity, venue));
                    })
                    .Where(item => attribute == 0 || item.Services.Any(service => InvitationAttributes[attribute - 1].Value(service) > 0));
                var ordered = sort == 1 ? results.OrderBy(item => item.Activity == null ? long.MaxValue
                        : Enumerable.Range(0, item.Activity.ServiceCount).Min(i => S.VenuePlayerPrice(item.Activity, i).Cents))
                    : sort == 2 ? results.OrderBy(item => item.Activity?.Minutes ?? int.MaxValue)
                    : results.OrderByDescending(item => personId == Npcs.Zhou && item.Id == "old_market" ? 2 : item.Interest != null ? 1 : 0);
                var filtered = ordered.ToArray();
                var items = filtered.Skip(page * pageSize).Take(pageSize).ToArray();
                var columns = Ui.Row(w.Content, 382, 12);
                var list = Ui.Rect(columns, "InviteActivityList");
                Ui.Size(list, 556);
                Ui.VList(list, 5, 0);
                if (items.Length == 0)
                {
                    Line(list, "没有符合筛选条件的活动。", 18, Ui.Dim);
                    return;
                }
                int selectedIndex = Array.FindIndex(items, item => item.Id == selectedId);
                var selected = items[Math.Max(0, selectedIndex)];
                foreach (var item in items)
                {
                    string liked = item.Interest == null ? "" : "  <color=#80551F>· 对方喜欢</color>";
                    var button = Ui.Size(Ui.Button(list, $"<b>{item.Title}</b>{liked}\n{item.Info}", () =>
                    { selectedId = item.Id; w.Rebuild(); }, 17, Ui.SelectedButton(selected.Id == item.Id)), -1, 59);
                    button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                    button.name = "CityActivityOffer_" + item.Id;
                }
                int pages = (filtered.Length + pageSize - 1) / pageSize;
                if (pages > 1)
                {
                    var pager = Ui.Row(list, 38);
                    Ui.Size(Ui.Button(pager, "上一页", () => { page--; selectedId = null; w.Rebuild(); }, 16, interactable: page > 0), 95);
                    Ui.Size(Ui.Label(pager, $"{page + 1} / {pages}", 16, TextAnchor.MiddleCenter), -1, 38, 1);
                    Ui.Size(Ui.Button(pager, "下一页", () => { page++; selectedId = null; w.Rebuild(); }, 16, interactable: page + 1 < pages), 95);
                }

                var detail = Ui.Size(Ui.Panel(columns, "InviteActivityDetails", Ui.PanelBg2), -1, -1, 1).rectTransform;
                Ui.VList(detail, 8, 14);
                Line(detail, selected.Title, 24, Ui.Accent);
                Para(detail, selected.Info, 17, Ui.Dim);
                string description = selected.Activity?.MidText ?? "邀请对方参加同一场比赛。";
                Para(detail, description, 18);
                if (selected.Interest != null) Para(detail, $"{name}喜欢 · {selected.Interest}", 16, Ui.Accent);
                var benefits = InvitationAttributes.Select(entry =>
                {
                    var values = selected.Services.Select(entry.Value).DefaultIfEmpty().ToArray();
                    int low = values.Min(), high = values.Max();
                    if (high <= 0) return null;
                    string change = low == high ? $"+{high}" : low > 0 ? $"+{low}～{high}" : $"最高 +{high}";
                    return entry.Label + " " + change;
                }).Where(value => value != null).ToArray();
                if (benefits.Length > 0) Para(detail, string.Join("　", benefits), 17, Ui.Good);
                if (selected.Services.Length > 1) Para(detail, "效果随到场选择的服务而变化。", 15, Ui.Dim);
                Ui.Size(Ui.Button(detail, selected.Activity == null ? "选赛场和场次 ›" : "选时间 ›", () =>
                {
                    if (selected.Activity != null) { OpenCityActivityOffer(selected.Id, personId); return; }
                    choosingRace = true;
                    w.ResetScrollOnRebuild = true;
                    w.Rebuild();
                }, 19, Ui.ButtonHot), -1, 44);
            });
        }

        public void OpenCityActivityOffer(string definitionId, string personId = null)
        {
            var def = CityLifeCatalog.Activity(definitionId); if (def == null) return;
            if (S.CityInviteBlocker(personId) is string reason) { Toast(reason); return; }
            long proposed = S.SuggestCityActivityStart(definitionId, personId); bool approveEmployee = false;
            OpenWindow("安排：" + def.Name, 950, 770, w =>
            {
                if (!S.City.HasPlace(def.VenueId)) { Line(w.Content, "实际场所暂不可用。", 18, Ui.Warn); return; }
                // Always choose a future quarter-hour; crossing midnight also advances the date.
                long earliestStart=(S.Clock.Now.TotalMinutes/15+1)*15;
                proposed=Math.Max((proposed+14)/15*15,earliestStart);
                var venue = S.City.GetPlace(def.VenueId);
                Line(w.Content, venue.Name + " · " + S.City.StreetName(venue.StreetId) + " · " + CityPlaceHours(venue), 20, Ui.Accent);
                Line(w.Content, $"{def.Minutes} 分钟 · 参考价 {S.VenuePriceRange(def)}", 18);
                int journey = S.CityTravelMinutes(venue);
                Line(w.Content, journey >= int.MaxValue / 8 ? "当前路线不可达" : $"路程约 {Ui.Emphasize($"{journey} 分钟")} · 到场后付费", 16, Ui.Dim);
                var date=Ui.Row(w.Content,42,8);Ui.Size(Ui.Label(date,"日期",18),65);
                int today=(int)(earliestStart/1440);
                int days=Math.Max(36,(int)(proposed/1440)-today+1);
                Ui.Size(Ui.Choice(date,Enumerable.Range(0,days).Select(offset=>S.DescribeDay(today+offset)+" · "+new GameDate(today+offset)),(int)(proposed/1440)-today,value=>{proposed=(long)(today+value)*1440+proposed%1440;w.RequestRebuild();}),-1,42,1);
                ClockSelector(w.Content,"开始",(int)(proposed%1440),value=>{proposed=proposed/1440*1440+value;w.RequestRebuild();},
                    proposed/1440==earliestStart/1440?(int)(earliestStart%1440):0);
                Ui.Size(Ui.Button(w.Content,"推荐可赴约时间",()=>{proposed=S.SuggestCityActivityStart(definitionId,personId);w.Rebuild();},17),-1,40);
                PersonSummary(w.Content, personId, S.CityPerson(personId)?.Name ?? "朋友", "约定时间前后 1 小时内到场");
                bool employee = S.Employees.Any(e => S.PersonIdOf(e.Person) == personId);
                if (employee) Ui.Size(Ui.Button(w.Content, (approveEmployee ? "✓ 已批准" : "明确批准") + "所选员工在活动出发/参加期间离岗", () => { approveEmployee = !approveEmployee; w.Rebuild(); }, 18, approveEmployee ? Ui.Warn : Ui.ButtonBg), -1, 48);
                if (employee) Para(w.Content, "受邀员工出发后将暂时离岗。", 16, Ui.Warn);
                string blocker = S.CityActivityInvitationBlocker(def.Id, personId, proposed, approveEmployee);
                if (blocker != null) Ui.Notice(w.Content, blocker);
                var actions = Ui.Row(w.Content, 48, 8);
                void ConfirmSchedule()
                {
                    long start = proposed; bool approval = approveEmployee;
                    ConfirmBusiness("确认邀约", venue.Name + " · 与" + (S.CityPerson(personId)?.Name ?? "朋友") + "见面\n"
                        + CityTime(start) + "，预留 " + def.Minutes + " 分钟。\n到场后选择菜单或服务；现在不扣消费，也不自动出发。", () =>
                    {
                        var activity = S.ScheduleCityActivityInvitation(def.Id, personId, start, approval);
                        if (activity == null) return;
                        CloseAllWindows(); Toast("已记入日程，临近出发时间会提醒你。");
                    });
                }
                Ui.Size(Ui.Button(actions, "确认邀约，加入日程", () => ConfirmSchedule(), 19, Ui.ButtonHot, blocker == null), -1, 48, 1);
            });
        }

        public void OpenCityActivitySettlement(string activityId)
        {
            var activity = S.CityActivities.FirstOrDefault(a => a.Id == activityId);
            if (string.IsNullOrEmpty(activity?.SettlementSummary)) return;
            if (activity.State == CityActivityPhase.NoShow)
            {
                OpenCityActivityNoShow(activity);
                return;
            }
            bool solo = activity.Participants.Length == 0;
            var definition = S.CityActivityService(activity);
            int choice = Math.Max(0, activity.ServiceChoice);
            string service = GameSession.VenueServiceName(definition, choice);
            EventCgFrame illustration = null;
            if (activity.State == CityActivityPhase.Completed && activity.MealApplied
                && activity.HostId != null && activity.Participants.Any(p => p.PersonId == activity.HostId)
                && (activity.SourceId?.StartsWith("friend_meal:", StringComparison.Ordinal) == true
                    || activity.SourceId?.StartsWith("classmate_help:", StringComparison.Ordinal) == true))
            {
                var dishProp = definition?.Id == "dinner" ? (choice == 0 ? EventCgProp.Meal : EventCgProp.Stew) : EventCgProp.None;
                illustration = new EventCgFrame("friend_meal", dishProp, activity.HostId, service, seated: true);
            }
            bool animateOnOpen = true;
            OpenWindow(solo ? "活动结算" : "相处结算", illustration != null ? 1180 : solo ? 780 : 960, 650, w =>
            {
                var reveal = animateOnOpen ? NewUiReveal() : null;
                animateOnOpen = false;
                var overview = Ui.Row(w.Content, illustration != null ? 84 : 98, 14);
                var heading = SettlementCard(overview, "ActivityOverview", 2);
                reveal?.Fade(heading, 0f);
                Line(heading, definition?.Name ?? "活动", 24);
                string venue = S.City.HasPlace(activity.VenueId) ? S.City.GetPlace(activity.VenueId).Name : activity.VenueId;
                Para(heading, venue + " · " + service, 16, Ui.Dim);

                if (definition?.Id != "hike" || activity.EntryCostCents > 0)
                {
                    var cost = SettlementCard(overview, "ActivityCost");
                    reveal?.Fade(cost, .04f);
                    Line(cost, activity.EntryCostRecorded ? "本次支出" : "参考费用", 15, Ui.Dim);
                    var amount = activity.EntryCostRecorded ? Money.FromCents(activity.EntryCostCents)
                        : Money.FromYuan(definition?.PriceYuan ?? 0) * (activity.PlayerTreat ? activity.Participants.Length + 1 : 1);
                    var amountLabel = Line(cost, amount.ToString(), 24, Ui.Accent);
                    reveal?.CountMoney(amountLabel, amount, value => value.ToString(), .10f);
                }

                var duration = SettlementCard(overview, "ActivityDuration");
                reveal?.Fade(duration, .08f);
                Line(duration, "体验时长", 15, Ui.Dim);
                string minutes = activity.ActualStart >= 0 && activity.ActualEnd >= activity.ActualStart
                    ? $"{activity.ActualEnd - activity.ActualStart} 分钟" : "—";
                Line(duration, minutes, 24);

                RectTransform details;
                RectTransform memoryColumn;
                if (illustration != null)
                {
                    const float imageWidth = 608;
                    var spread = Ui.Row(w.Content, -1, 12);
                    var layout = spread.GetComponent<HorizontalLayoutGroup>();
                    layout.childAlignment = TextAnchor.UpperLeft;
                    layout.childForceExpandHeight = false;

                    memoryColumn = Ui.Size(Ui.Rect(spread, "ActivityIllustrationColumn"), imageWidth, -1, 0);
                    Ui.VList(memoryColumn, 8, 0);
                    BuildEventCg(memoryColumn, illustration, imageWidth * 9 / 16);

                    details = Ui.Size(Ui.Rect(spread, "ActivityResultsColumn"), 0, -1, 1);
                    Ui.VList(details, 8, 0);
                }
                else
                {
                    details = memoryColumn = Ui.Scroll(w.Content);
                    Ui.Size((RectTransform)details.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
                    var bodyLayout = details.GetComponent<VerticalLayoutGroup>();
                    bodyLayout.spacing = 10;
                    bodyLayout.padding = new RectOffset(0, 2, 2, 2);
                }

                if (activity.PlayerStatsAfter == null || activity.PlayerStatsAfter.Length == 0)
                {
                    var summary = Para(details, activity.SettlementSummary, 17);
                    reveal?.Fade(summary.transform, .12f);
                }
                else
                {
                    var results = details;
                    if (illustration == null)
                    {
                        results = Ui.Row(details, -1, 12);
                        var layout = results.GetComponent<HorizontalLayoutGroup>();
                        layout.childAlignment = TextAnchor.UpperLeft;
                        layout.childForceExpandHeight = false;
                    }
                    if (!solo) BuildActivityRelationshipResults(results, activity, reveal, portraitWidth: illustration != null ? 76 : 104);
                    BuildActivityPersonalResults(results, activity, reveal);

                    var memory = SettlementCard(memoryColumn, "ActivityMemory");
                    reveal?.Fade(memory, .28f);
                    string[] interactions = (activity.Choices ?? Array.Empty<string>())
                        .Select(key => GameSession.CityActivityChoiceText(definition, key)).Where(choice => choice != null).ToArray();
                    if (interactions.Length > 0)
                    {
                        Line(memory, solo ? "体验内容" : "一起做过", 18, Ui.Accent);
                        Para(memory, string.Join("、", interactions), 17);
                    }
                    if ((illustration != null || GameSession.IsInteractiveFishingActivity(activity) || GameSession.IsOnsiteSport(activity))
                        && !string.IsNullOrWhiteSpace(activity.EventSettlementNote))
                        Para(memory, activity.EventSettlementNote.Trim(), 17);
                    if (!string.IsNullOrWhiteSpace(activity.EndReason)) Para(memory, activity.EndReason, 16, Ui.Dim);
                }

                var footer = Ui.Row(w.Content, 44, 10);
                bool completed = activity.State == CityActivityPhase.Completed && !activity.HasPartialSettlement;
                string status = activity.HasPartialSettlement ? "已提前结算"
                    : completed && !solo ? "已记入人脉记录" : GameSession.CityPhaseText(activity.State);
                Ui.Size(Ui.Label(footer, status, 16, TextAnchor.MiddleLeft, completed ? Ui.Good : Ui.FlatDown), 0, -1, 1);
                Ui.Size(Ui.Button(footer, solo ? "完成" : "收好这段回忆", w.Close, 18, Ui.ButtonHot), 200, 44);
                if (illustration != null) FitWindowToContent(w);
                PlayUiReveal(reveal);
            }, onClosed: () => OpenCommunityActivityClue(activityId), scrollContent: false);
        }

        private void OpenCityActivityNoShow(CityActivity activity)
        {
            OpenWindow("未赴约", 700, 545, w =>
            {
                Line(w.Content, S.CityActivityService(activity)?.Name ?? "预约活动", 23, Ui.Accent);
                string venue = S.City.HasPlace(activity.VenueId) ? S.City.GetPlace(activity.VenueId).Name : activity.VenueId;
                Line(w.Content, $"{CityTime(activity.ScheduledStart)} · {venue}", 17, Ui.Dim);
                Para(w.Content, activity.SettlementSummary, 18);
                var body = Ui.Scroll(w.Content);
                Ui.Size((RectTransform)body.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
                if (activity.Participants.Length > 0)
                    BuildActivityRelationshipResults(body, activity, portraitWidth: 164);
                Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 19, Ui.ButtonHot), -1, 44);
            }, onClosed: () => activity.NoShowNoticeRead = true, scrollContent: false);
        }

        private static RectTransform SettlementCard(Transform parent, string name, float flexWidth = 1)
        {
            var card = Ui.FlatPanel(parent, name).rectTransform;
            Ui.VList(card, 3, 10);
            var size = Ui.Size(card, 0, -1, flexWidth).GetComponent<LayoutElement>();
            size.minWidth = 0;
            return card;
        }

        private void BuildActivityRelationshipResults(Transform parent, CityActivity activity, UiReveal reveal = null,
            float portraitWidth = 104)
        {
            var card = SettlementCard(parent, "ActivityRelationships", 1.2f);
            reveal?.Fade(card, .12f);
            Line(card, "关系变化", 20, Ui.Accent);
            if (activity.HasPartialSettlement)
                Para(card, $"活动进度 {activity.PartialSettlementProgress:P0} · 基础好感按进度结算", 15, Ui.Dim);
            foreach (var participant in activity.Participants)
            {
                var person = SettlementCard(card, "ActivityCompanion");
                person.GetComponent<Image>().color = Ui.FlatBand;
                var row = Ui.Row(person, -1, 14);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                var portrait = Ui.FlatPanel(row, "CompanionPortrait");
                Ui.Size(portrait, portraitWidth, portraitWidth * 1.3f);
                FitPortrait(portrait.rectTransform, participant.PersonId, 2);
                var details = Ui.Rect(row, "CompanionDetails");
                Ui.Size(details, 0, -1, 1);
                var layout = Ui.VList(details, 6, 0);
                layout.childAlignment = TextAnchor.MiddleLeft;
                Line(details, $"<b>{S.CityPerson(participant.PersonId)?.Name ?? "同行者"}</b>", 23);
                SettlementChange(details, "好感", participant.ResultCaptured ? participant.RelationBefore : (double?)null, participant.RelationAfter, reveal, .18f);
                SettlementChange(details, "履约信任", participant.ResultCaptured ? participant.TrustBefore : (double?)null, participant.TrustAfter, reveal, .18f);
                if (activity.State == CityActivityPhase.Completed && activity.DefinitionId == "dinner" && S.IsMet(participant.PersonId))
                    Para(details, "可在手机通讯录联系", 14, Ui.Dim);
            }
        }

        private static void BuildActivityPersonalResults(Transform parent, CityActivity activity, UiReveal reveal)
        {
            var card = SettlementCard(parent, "ActivityPersonalChanges");
            float delay = activity.Participants.Length == 0 ? .12f : .19f;
            reveal?.Fade(card, delay);
            Line(card, "个人状态", 20, Ui.Accent);
            Para(card, "含活动期间的日常消耗", 14, Ui.Dim);
            var names = GameSession.CityActivityStatNames;
            bool any = false;
            for (int i = 0; i < Math.Min(names.Count, activity.PlayerStatsAfter.Length); i++)
            {
                double after = activity.PlayerStatsAfter[i];
                double? before = i < (activity.PlayerStatsBefore?.Length ?? 0) ? activity.PlayerStatsBefore[i] : (double?)null;
                if (before.HasValue && Math.Round(after - before.Value, 1) == 0) continue;
                SettlementChange(card, names[i], before, after, reveal, delay + .06f);
                any = true;
            }
            if (!any) Para(card, "本次没有数值变化", 16, Ui.Dim);
        }

        private static void SettlementChange(Transform parent, string label, double? before, double after,
            UiReveal reveal, float delay)
        {
            var row = Ui.Row(parent, 30, 6);
            Ui.Size(Ui.Label(row, label, 16), 82);
            string values = before.HasValue ? $"{before.Value:0.#} → {after:0.#}" : $"{after:0.#}";
            var valueText = Ui.Size(Ui.Label(row, values, 16, TextAnchor.MiddleLeft, Ui.Dim), 0, -1, 1);
            if (!before.HasValue) return;
            double delta = Math.Round(after - before.Value, 1);
            var color = delta > 0 ? Ui.Good : delta < 0 ? Ui.FlatDown : Ui.Dim;
            var deltaText = Ui.Size(Ui.Label(row, $"<b>{delta:+0.#;-0.#;0}</b>", 22, TextAnchor.MiddleRight, color), 68);
            if (reveal == null || delta == 0) return;
            reveal.Add(valueText.transform, delay, .34f, progress =>
            {
                double change = (after - before.Value) * progress;
                valueText.text = $"{before.Value:0.#} → {before.Value + change:0.#}";
                deltaText.text = $"<b>{Math.Round(change, 1):+0.#;-0.#;0}</b>";
            });
        }

        private void OpenCommunityActivityClue(string activityId)
        {
            int lead=S.TakeCommunityActivityClue(activityId);
            if(lead<0)return;
            string id=GameSession.CommunityLeadPerson(lead);
            string name=S.CityPerson(id)?.Name??id;
            OpenDialogue(id,name,"活动后的话","",null,
                firstReply:GameSession.CommunityLeadHint(lead)+"\n\n已记下藏箱线索，可以按主线导航前去寻找。",
                onClosed:()=>OpenCommunityActivityClue(activityId),remote:true,replyOnly:true,replyConfirm:"记下线索");
        }

        private void OpenCityActivityDetail(string activityId)
        {
            var running = S.RunningCityActivity;
            if (running?.Id == activityId && GameSession.IsCookTasting(running))
            {
                OpenCookTasting(activityId);
                return;
            }
            if (running?.Id == activityId && GameSession.IsOutdoorMarketVisit(running))
            {
                OpenOutdoorMarketVisit(running);
                return;
            }
            if (running?.Id == activityId && S.FishingActive)
            {
                OpenFishing();
                return;
            }
            if (running?.Id == activityId && S.CurrentOnsite != null)
            {
                OpenOnsitePlay();
                return;
            }
            var window = OpenWindow("活动详情", 860, 710, w =>
            {
                var a = S.CityActivities.FirstOrDefault(x => x.Id == activityId); if (a == null) { Line(w.Content, "记录暂不可用。", 18); return; }
                var def = S.CityActivityService(a);
                Line(w.Content, (def?.Name ?? a.DefinitionId) + " · " + GameSession.CityPhaseText(a.State), 22, Ui.Accent);
                Line(w.Content, "地点：" + (S.City.HasPlace(a.VenueId) ? S.City.GetPlace(a.VenueId).Name : a.VenueId) + "\n预约：" + CityTime(a.ScheduledStart), 18);
                if(a.HostId!="player"&&S.CityPerson(a.HostId)!=null)
                    Line(w.Content,"邀请人："+S.CityPerson(a.HostId).Name,18,Ui.Accent);
                foreach (var p in a.Participants) Line(w.Content, (S.CityPerson(p.PersonId)?.Name ?? "同行者") + " · " + CityParticipantText(p.Phase), 18);
                bool signHelp = a.DefinitionId == GameSession.SignHelpActivityId;
                bool parkWalk = def?.Id == "hike";
                bool freeWalk = parkWalk && a.EntryCostCents == 0;
                string payment = a.HostPays ? "邀请人请客" : a.EntryPaid ? "费用已支付" : a.PlayerTreat ? "你请客 · 到场点单后支付" : "费用尚未支付";
                if (def?.EventOnly == true && !a.EntryPaid)
                    payment = a.HostPays ? "邀请人承担，本人免费" : "到场支付 " + S.CityActivityEntryPrice(def, Math.Max(0,a.ServiceChoice), a);
                if (GameSession.OnsiteNumber(a.DefinitionId) == 56 && !a.EntryPaid) payment = "到球场后选择付款方式";
                if (!string.IsNullOrEmpty(a.EventGiftId)) payment += a.EventGiftDelivered ? " · 伴手礼已送达" : " · 请带上已购买的伴手礼";
                if (signHelp) payment = "义务帮忙 · " + a.Duration + " 分钟";
                if (freeWalk)
                {
                    if (a.State != CityActivityPhase.Running) Para(w.Content, "免费散步", 18);
                }
                else Para(w.Content, $"阶段 {a.Stage + 1}/3 · {payment} · 完成奖励 {(a.RewardApplied ? "已结算" : "尚未结算")}", 18);
                if (a.State == CityActivityPhase.Running)
                {
                    long remaining = Math.Max(0, a.ActualStart + a.Duration - S.Clock.Now.TotalMinutes);
                    bool manual = GameSession.IsManualPlayActivity(a);
                    Line(w.Content, manual
                        ? $"游戏时间暂停 · 完成后结算 {Ui.Emphasize($"{a.Duration} 分钟")}"
                        : "预计剩余 " + remaining + " 游戏分钟；世界时间按原倍速推进。", 19);
                    if (a.ServiceConfirmed)
                        Line(w.Content, (parkWalk ? "散步路线 · " : "已选项目 · ") + GameSession.VenueServiceName(def, a.ServiceChoice), 18, Ui.Good);
                    Para(w.Content, a.Stage == 0 ? def?.StartText : S.CityActivityMidText(a), 19);
                    BuildPartnerActivityChoices(w, a);
                    BuildOutdoorBatchStatus(w, a);
                    if (!parkWalk)
                        Ui.Size(Ui.Button(w.Content, signHelp ? "查看帮忙进度" : "打开场所菜单", () => GoToScheduledCityActivity(activityId), 18, Ui.ButtonBg,
                            CityActivityMenuOrTravelBlocker(activityId) == null), -1, 44);
                    if (def != null && (!parkWalk || a.Participants.Length > 0))
                    for (int i = a.ServiceConfirmed ? def.ServiceCount : 0; i < def.Choices.Length; i++)
                    {
                        int index = i; bool done = a.Choices.Contains(index.ToString()) && !S.OutdoorRepeatChoice(a, index);
                        var outdoor = S.OutdoorActivityState(a.Id);
                        bool available = outdoor?.Pending == null;
                        if (outdoor?.Number == 69 && index >= 2 && index <= 4)
                            available &= outdoor.Visited >= 3 && index - 2 < outdoor.QuoteCents.Length;
                        Ui.Size(Ui.Button(w.Content, (done ? "✓ " : "") + def.Choices[i], () =>
                        {
                            if (!manual) CloseAllWindows();
                            S.ChooseCityActivity(activityId, index);
                            if (manual) w.RequestRebuild();
                        }, 18, Ui.SelectedButton(done), available), -1, 44);
                    }
                    BuildLifeActivityInteraction(w, a);
                    BuildExhibitionActivity(w, a);
                    BuildOutdoorBatchProcurement(w, a);
                    if (manual) BuildManualActivityFinish(w, a);
                    Ui.Size(Ui.Button(w.Content, manual ? "收起活动面板" : parkWalk ? "继续散步" : "收起面板 / 继续活动", CloseAllWindows, 19, Ui.ButtonBg), -1, 46);
                }
                else if (a.Active)
                {
                    string why = CityActivityMenuOrTravelBlocker(activityId);
                    bool here = S.CurrentPlace?.Id == a.VenueId;
                    string early = !here && why == null ? S.CityActivityEarlyDepartureHint(a) : null;
                    string action = signHelp ? here ? "打开帮忙页面" : "前往手机店"
                        : !here ? "前往赴约"
                        : parkWalk && !a.ServiceConfirmed ? "选择散步路线"
                        : GameSession.IsOnsiteActivity(a) || a.ServiceConfirmed ? "打开现场预约" : "打开菜单 / 选择服务";
                    Ui.Size(Ui.Button(w.Content, action, () => GoToScheduledCityActivity(activityId), 19,
                        early == null ? Ui.ButtonHot : Ui.ButtonBg, why == null), -1, 46).Tip(early);
                    if (why != null) Para(w.Content, why, 16, Ui.Dim);
                }
                if (a.Active)
                {
                    bool runningNow = a.State == CityActivityPhase.Running;
                    string end = runningNow ? parkWalk ? "结束散步" : "提前离开" : "取消预约";
                    Ui.Size(Ui.Button(w.Content, end, () => ConfirmEndCityActivity(a, freeWalk), 18, Ui.Warn), -1, 44);
                }
                if(a.State==CityActivityPhase.Scheduled&&a.DefinitionId.StartsWith("expo_",StringComparison.Ordinal))
                    foreach(var participant in a.Participants)
                    {
                        var worker=S.Employees.FirstOrDefault(e=>S.PersonIdOf(e.Person)==participant.PersonId);if(worker?.Shop==null)continue;
                        string employeeId=worker.Person.Id;bool work=participant.WorkForEmployer;
                        Ui.Size(Ui.Button(w.Content,worker.Person.Name+(work?"：展位工作（含路程计工时）":"：私人同行，改为展位工作"),()=>{S.SetEmployeeExhibitionWork(activityId,employeeId,!work);w.Rebuild();},17),-1,42);
                    }
                if (!string.IsNullOrEmpty(a.SettlementSummary))
                {
                    string settlement = a.State == CityActivityPhase.NoShow ? "查看未赴约记录"
                        : a.Participants.Length == 0 ? "查看活动结算" : "查看相处结算";
                    Ui.Size(Ui.Button(w.Content, settlement, () => OpenCityActivitySettlement(activityId), 19, Ui.ButtonBg), -1, 44);
                }
                if (a.EndReason != null) Para(w.Content, a.EndReason, 18, Ui.Dim);
            });
            if (window != null && running?.Id == activityId && GameSession.IsManualPlayActivity(running))
            {
                window.LiveConversation = true;
                window.LiveKey = () =>
                {
                    if (window.HasFocusedInput()) return window.LiveStamp;
                    var outdoor = S.OutdoorActivityState(activityId);
                    return $"{running.State}:{outdoor?.Pending}:{outdoor?.Visited}:{S.CanFinishManualCityActivity(activityId)}";
                };
                window.LiveStamp = window.LiveKey();
            }
        }

        private void ConfirmEndCityActivity(CityActivity activity, bool freeWalk)
        {
            bool running = activity.State == CityActivityPhase.Running;
            string explanation = freeWalk
                ? running ? "保留已有的阶段效果。" : "取消这次散步预约。"
                : activity.EntryPaid ? "保留已发生费用和阶段效果。你仍留在当前场所。"
                : "尚未发生的活动费不扣除。取消预约与中途离开分别记录。";
            if (running && activity.Participants.Length > 0) explanation += "基础好感按实际完成进度结算。";
            ConfirmBusiness("结束本次安排", explanation, () => { CloseAllWindows(); S.CancelCityActivity(activity.Id, true); });
        }

        private static string CityParticipantText(CityParticipantPhase p) => p == CityParticipantPhase.NotDeparted ? "尚未出发" : p == CityParticipantPhase.Travelling ? "正在赴约" : p == CityParticipantPhase.Waiting ? "已到场等候" : p == CityParticipantPhase.Participating ? "正在参加" : "已离场";

        private void BuildCityLifeHud()
        {
            _cityActivityHud = Ui.Panel(_invitationCanvas.transform, "CityActivityHud", Ui.PanelBg).rectTransform;
            _cityActivityHud.Anchor(.28f, 0, .78f, 0, 0, 128, 0, -238);
            Ui.VList(_cityActivityHud, 4, 8);
            _cityActivityHudText = Ui.Label(_cityActivityHud, "", 18);
            Ui.Size(_cityActivityHudText, -1, 48);
            var row = Ui.Row(_cityActivityHud, 40, 7);
            _cityActivityDetails = Ui.Size(Ui.Button(row, "日程详情", () => { if (_cityHudActivityId != null) OpenCityActivityDetail(_cityHudActivityId); }, 18), -1, 40, 1);
            _cityActivityGo = Ui.Size(Ui.Button(row, "前往赴约", () =>
            {
                var active = S.RunningCityActivity;
                if (_cityHudActivityId == null) return;
                if (active?.Id == _cityHudActivityId && GameSession.IsManualPlayActivity(active)) OpenCityActivityDetail(active.Id);
                else GoToScheduledCityActivity(_cityHudActivityId);
            }, 18, Ui.ButtonHot), -1, 40, 1);
            _cityActivityEnd = Ui.Size(Ui.Button(row, "取消邀约", () =>
            {
                if (_cityHudActivityId == null) return;
                var running = S.RunningCityActivity;
                if (running?.Id == _cityHudActivityId && S.CityActivityService(running)?.Id == "hike")
                    ConfirmEndCityActivity(running, running.EntryCostCents == 0);
                else OpenCityActivityDetail(_cityHudActivityId);
            }, 18, Ui.Warn), -1, 40, 1);
            Ui.Size(Ui.Button(row, "收起", () =>
            {
                if (_cityHudActivityId != null) _dismissedCityActivityHud.Add(_cityHudActivityId);
                _cityActivityHud.gameObject.SetActive(false);
                RefreshWorldHintVisibility();
            }, 16), 72, 40).Tip("只隐藏这条提醒；导航和邀约保留，仍可从日程查看。");
            _cityActivityHud.gameObject.SetActive(false);
        }
        public void UpdateCityLifeHud(float dt)
        {
            if (_cityActivityHud == null || S == null) return;
            _cityHudRefresh -= dt; if (_cityHudRefresh > 0) return; _cityHudRefresh = .2f;
            if (_cityHudSession != S) { _cityHudSession = S; _dismissedCityActivityHud.Clear(); }
            if (S.IsGameOver || ExpoHudOpen || OpeningMealRestricted || _ctrl.PetFocusActive
                || S.FishingActive || OnsiteInputActive || GameSession.IsOnsiteSport(S.RunningCityActivity))
            {
                _cityHudActivityId = null;
                _cityActivityHud.gameObject.SetActive(false);
                RefreshWorldHintVisibility();
                return;
            }
            if (!AnyModalOpen && !S.Racing && !S.CityActivityExclusive)
            {
                var missed = S.CityActivities.FirstOrDefault(x => x.State == CityActivityPhase.NoShow
                    && !x.NoShowNoticeRead && !string.IsNullOrEmpty(x.SettlementSummary));
                if (missed != null) OpenCityActivityNoShow(missed);
            }
            string routeActivityId = S.CityIntent is { Cancelled: false } intent ? intent.ActivityId : null;
            var a = S.RunningCityActivity;
            if (a != null && _dismissedCityActivityHud.Contains(a.Id)) a = null;
            if (a == null) a = S.CityActivities.Where(x => x.Active && !_dismissedCityActivityHud.Contains(x.Id)
                && (x.Reminder || x.PlayerIntentId != null || S.CurrentPlace?.Id == x.VenueId))
                .OrderByDescending(x => x.Id == routeActivityId).ThenBy(x => x.ScheduledStart).FirstOrDefault();
            _cityActivityHud.gameObject.SetActive(a != null);
            _cityHudActivityId = a?.Id;
            RefreshWorldHintVisibility();
            if (a == null) return;
            var def = S.CityActivityService(a);
            bool parkWalk = def?.Id == "hike";
            bool running = a.State == CityActivityPhase.Running;
            bool onsite = GameSession.IsOnsiteActivity(a) || GameSession.IsVenueSport(a);
            bool raceAid = GameSession.IsRaceAidActivity(a);
            _cityActivityDetails.GetComponentInChildren<Text>().text = parkWalk && running ? "散步详情"
                : onsite && running ? "继续现场活动"
                : S.ActivityPartnerId(a) != null ? "约会详情"
                : a.State == CityActivityPhase.Running ? a.HostPays ? "活动详情" : "本次消费" : "日程详情";
            _cityActivityEnd.GetComponentInChildren<Text>().text = running ? parkWalk ? "结束散步" : "提前离开" : "取消邀约";
            bool here = S.CurrentPlace?.Id == a.VenueId;
            bool signHelp = a.DefinitionId == GameSession.SignHelpActivityId;
            bool travelling = !here && routeActivityId == a.Id;
            bool early = a.State != CityActivityPhase.Running && !travelling && S.CityActivityEarlyDepartureHint(a) != null;
            _cityActivityGo.gameObject.SetActive(!parkWalk || !running);
            _cityActivityGo.interactable = !travelling && CityActivityMenuOrTravelBlocker(a.Id) == null && !_ctrl.Placing;
            Ui.SetButtonFill(_cityActivityGo, early && !here ? Ui.ButtonBg : Ui.ButtonHot);
            _cityActivityGo.GetComponentInChildren<Text>().text = travelling ? "正在前往"
                : running && GameSession.IsCookTasting(a) ? "继续新菜试吃"
                : running && GameSession.IsManualPlayActivity(a) ? "继续活动"
                : signHelp ? here ? "打开帮忙页面" : "前往手机店"
                : raceAid ? here ? "继续陪诊" : "前往医院"
                : here ? onsite ? "进入现场" : a.ServiceConfirmed && !running ? "查看现场预约" : parkWalk ? "选择散步路线" : "打开菜单 / 服务" : "前往赴约";
            if (signHelp && a.State == CityActivityPhase.Running) _cityActivityDetails.GetComponentInChildren<Text>().text = "帮忙进度";
            string phase = travelling ? "正在赴约" : GameSession.CityPhaseText(a.State);
            bool manual = GameSession.IsManualPlayActivity(a);
            string time = a.State == CityActivityPhase.Running
                ? manual ? "时间暂停" : "剩余 " + Math.Max(0, a.ActualStart + a.Duration - S.Clock.Now.TotalMinutes) + " 分钟"
                : "预约 " + CityTime(a.ScheduledStart);
            string title = a.ServiceConfirmed && !raceAid ? GameSession.VenueServiceName(def, a.ServiceChoice) : def?.Name ?? a.DefinitionId;
            string hint;
            if (a.State == CityActivityPhase.Running) hint = manual ? "完成现场操作后结算时间。" : a.Stage == 0 ? def?.StartText : S.CityActivityMidText(a);
            else if (travelling) hint = S.Trip is Trip trip
                ? $"约 {trip.EstimateRemainingMinutes(S.Time.SecondsPerMinuteAt1x)} 分钟到达 · 按 WASD 随时接管"
                : "正在沿路线前往约定场所。";
            else if (here && raceAid)
                hint = a.Participants.All(p => p.Phase == CityParticipantPhase.Waiting || p.Phase == CityParticipantPhase.Participating)
                    ? "双方已到场，时间继续后开始陪诊。" : "已到医院，同行者到齐后自动开始陪诊。";
            else if (early) hint = here ? signHelp ? "预约时间还早，临近约定时间再开始帮忙。" : onsite ? "请在约定时间到场开始。"
                : parkWalk ? "预约时间还早，可以先看看步道。" : "预约时间还早，可以先查看场所菜单。" : "离约定时间还早，临近出发时间再前往。";
            else if (here) hint = signHelp ? "已经到店，点「打开帮忙页面」开始换招牌。" : onsite ? "已到场，点「进入现场」开始。"
                : a.ServiceConfirmed ? "已到场，从现场预约开始。" : parkWalk ? "已到公园，可以选择散步路线。"
                : "已经到场。在菜单里选择服务，与同行者一起开始活动。";
            else hint = S.Trip != null || S.CityIntent != null ? "点击「前往赴约」可切换当前路线。"
                : "赴约时间临近，点击「前往赴约」出发。";
            _cityActivityHudText.text = title + " · " + phase + " · " + time + "\n" + hint;
        }

        public void OpenCityAcceptance()
        {
            OpenWindow("城市生活验收预置 · 独立测试档", 900, 670, w =>
            {
                var content=Ui.Scroll(w.Content,480);
                Para(content, "此入口创建独立测试进度，不覆盖正式存档。预置只准备资金、日期、人物与资产；赴约、行走、活动和交易仍走正常规则。", 19, Ui.Warn);
                void Preset(string title, string key, string description)
                {
                    Para(content, description, 17);
                    Ui.Size(Ui.Button(content, title, () => { CloseAllWindows(); _ctrl.StartCityLifePreset(key); }, 19), -1, 48);
                }
                Preset("单人守店与真实赴约", "solo", "从单人店出发，检查离店暂停服务、路上接管、到场用餐。 ");
                Preset("员工、多店与共享预算", "staff", "查看实际当班、采购与后台经营；邀请唯一员工离岗再看服务。 ");
                Preset("展会、人物与故事路线", "expo", "准备展期与已认识的人，从正常活动入口体验展位及原创模型。 ");
#if UNITY_EDITOR
                Ui.Size(Ui.Button(content, "品牌展会验收：社区资格、报名、接待与交付", OpenExpoAcceptance, 19, Ui.ButtonHot), -1, 48);
#endif
                Preset("自有物业与每周房产行情", "assets", "准备4处商铺、1套自住房和1套带NPC住户住宅，覆盖自用、出租与空置；全款购入，并提供8日报价样本，可直接查看住宅和商铺的售价及周租涨跌。 ");
                Preset("夜班精力、自由排班与加班费", "night", "20:00开局，员工日薪200、12小时夜班预计280。玩家精力归零但仍能行动；测试四小时提醒、实际睡眠、跨午夜工时与读档。 ");
                Preset("服装与冷暖", "clothing", "独立冬季测试档，准备四档服装库存、真实衣架/折叠架/镜子。试穿、更衣、库存转自用和冷暖设备均走正常入口；不快进正式档。 ");
                var seasons=Ui.Row(content,40,6);
                foreach(var choice in new[]{new[]{"春季起点","clothing_spring"},new[]{"夏季起点","clothing_summer"},new[]{"秋季起点","clothing_autumn"},new[]{"冬季起点","clothing"}})
                {string key=choice[1];Ui.Size(Ui.Button(seasons,choice[0],()=>{CloseAllWindows();_ctrl.StartCityLifePreset(key);},17),-1,40,1);}

                Ui.Size(Ui.Button(content, "加载已保存的城市生活测试档", () => { CloseAllWindows(); _ctrl.LoadCityLifeTestPreset(); }, 18), -1, 44);
                if (S.IsCityLifeTest)
                {
                    Ui.Size(Ui.Button(content, "保存当前测试档（独立槽）", () => Toast(_ctrl.SaveGame() ? "测试档已保存。" : "当前状态暂不能保存测试档。"), 18), -1, 44);
                    Ui.Size(Ui.Button(content, "返回进入测试前的正式进度", () => { CloseAllWindows(); _ctrl.ReturnFromCityLifePreset(); }, 19, Ui.ButtonHot), -1, 48);
                }
            });
        }
    }
}
