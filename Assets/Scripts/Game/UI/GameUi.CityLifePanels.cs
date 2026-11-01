using System;
using System.Linq;
using System.Collections.Generic;
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
                Ui.Size(Ui.Button(w.Content, "城市地图 / 自定目的地", OpenCityMap, 20, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, "街坊与已认识的人", OpenCityPeople, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "活动日程 / 展会日历", OpenCityActivities, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "人脉经营故事", OpenCityStories, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "经营总览", OpenCityFinanceOverview, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "市场变化 / 模型目录", OpenCityRetailPanel, 20), -1, 50);
                if (S.CurrentPlace != null && CityLifeCatalog.Activities.Any(a => a.VenueId == S.CurrentPlace.Id))
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
            int open = p.ExplicitEntrance ? p.OpenMinute : p.Kind == PlaceKind.Wholesaler ? 540 : 0;
            int close = p.ExplicitEntrance ? p.CloseMinute : p.Kind == PlaceKind.Wholesaler ? 1200 : 0;
            if (open == close) return "全天开放";
            return $"{open / 60:00}:{open % 60:00}—{(close < open ? "次日 " : "")}{close / 60:00}:{close % 60:00}" + (p.Kind == PlaceKind.Wholesaler ? "（工作日）" : "");
        }
        private bool CityMapTypeMatches(Place p) => _cityMapType == null
            || _cityMapType == "my" && S.IsMyShop(p)
            || _cityMapType == "property" && S.PropertyAt(p.Id) != null
            || _cityMapType == "venue" && CityLifeCatalog.Activities.Any(a => a.VenueId == p.Id)
            || _cityMapType == "source" && (p.Kind == PlaceKind.Wholesaler || S.HasRetailWholesale(p))
            || _cityMapType == "home" && p.Kind == PlaceKind.Home;
        private int CityMapPriority(Place p) => S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? 0 : S.CityFavorites.Contains(p.Id) ? 1 : S.CityRecent.Contains(p.Id) ? 2 : 3;

        public void OpenCityMap()
        {
            _cityMapPage = 0;
            OpenWindow("十街地图 / 自定义目的地", 1110, 790, w =>
            {
                Ui.Size(Ui.TextInput(w.Content, _cityMapSearch, "搜索地点名、街区、用途", value => { _cityMapSearch = value ?? ""; _cityMapPage = 0; w.RequestRebuild(); }), -1, 44);
                var types = Ui.Row(w.Content, 40, 6);
                void Type(string title, string key) => Ui.Size(Ui.Button(types, title, () => { _cityMapType = key; _cityMapPage = 0; w.Rebuild(); }, 17, _cityMapType == key ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
                Type("全部", null); Type("我的店", "my"); Type("已购物业", "property"); Type("娱乐活动", "venue"); Type("采购货源", "source"); Type("住处", "home");
                var streets = S.City.Streets.ToArray();
                for (int i = 0; i < streets.Length; i += 5)
                {
                    var row = Ui.Row(w.Content, 38, 6);
                    if (i == 0) Ui.Size(Ui.Button(row, "全部街区", () => { _cityMapStreet = null; _cityMapPage = 0; w.Rebuild(); }, 16, _cityMapStreet == null ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
                    foreach (var street in streets.Skip(i).Take(5))
                    {
                        string id = street.id;
                        Ui.Size(Ui.Button(row, street.name, () => { _cityMapStreet = id; _cityMapPage = 0; w.Rebuild(); }, 16, _cityMapStreet == id ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
                    }
                }
                if (S.CityDefaultDestination != null && S.City.HasPlace(S.CityDefaultDestination))
                    Line(w.Content, "自定默认：" + S.City.GetPlace(S.CityDefaultDestination).Name + " · 收藏 " + S.CityFavorites.Count + "/5（仅在此面板显示）", 17, Ui.Accent);
                if (S.CityIntent != null || S.Trip != null) Ui.Size(Ui.Button(w.Content, "取消当前路线（预约保留）", () => { S.StopTrip(); S.CancelCityTravel(); w.Rebuild(); }, 18, Ui.Warn), -1, 42);
                BuildCityRouteMap(w.Content);
                var places = S.City.Places.Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)
                    && (_cityMapSearch.Length == 0 || (p.Name + " " + p.Business + " " + S.City.StreetName(p.StreetId)).IndexOf(_cityMapSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(CityMapPriority).ThenBy(p => p.StreetId).ThenBy(p => p.Name).ToArray();
                _cityMapPage = Math.Max(0, Math.Min(_cityMapPage, Math.Max(0, (places.Length - 1) / 12)));
                Line(w.Content, "当前邀约优先，其次收藏和最近前往；地图不显示陌生人的实时位置。", 17, Ui.Dim);
                foreach (var p in places.Skip(_cityMapPage * 12).Take(12))
                {
                    var place = p;
                    string tag = S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? "[邀约] " : S.CityFavorites.Contains(p.Id) ? "★ " : S.CityRecent.Contains(p.Id) ? "[最近] " : "";
                    Ui.Size(Ui.Button(w.Content, tag + p.Name + " · " + S.City.StreetName(p.StreetId) + " · " + (p.Business ?? p.Kind.ToString()) + "\n" + CityPlaceHours(p) + (S.IsMyShop(p) ? " · 我的店" : S.PropertyAt(p.Id) != null ? " · 已持有物业" : ""), () => OpenCityPlaceDetail(place), 18), -1, 68);
                }
                if (places.Length == 0) Line(w.Content, "没有匹配地点，可清除搜索或切换筛选。", 18, Ui.Dim);
                var pages = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(pages, "上一页", () => { _cityMapPage--; w.Rebuild(); }, 18, Ui.ButtonBg, _cityMapPage > 0), -1, 40, 1);
                Ui.Size(Ui.Label(pages, $"第 {_cityMapPage + 1} 页 · {places.Length} 个地点", 17, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(pages, "下一页", () => { _cityMapPage++; w.Rebuild(); }, 18, Ui.ButtonBg, (_cityMapPage + 1) * 12 < places.Length), -1, 40, 1);
            });
        }

        private void BuildCityRouteMap(Transform parent)
        {
            var nodes = S.City.Map.Nodes.ToArray(); if (nodes.Length == 0) return;
            double minX = nodes.Min(n => n.X), minY = nodes.Min(n => n.Y), width = Math.Max(1, nodes.Max(n => n.X) - minX), height = Math.Max(1, nodes.Max(n => n.Y) - minY);
            Vector2 Point(double x, double y) => new Vector2(.05f + .9f * (float)((x - minX) / width), .08f + .84f * (float)((y - minY) / height));
            var background = Ui.Panel(parent, "CityRouteMap", Ui.PanelBg2).rectTransform; Ui.Size(background, -1, 240);
            var roads = Ui.Rect(background, "AuthoritativeRoads").Fill(); roads.gameObject.AddComponent<CanvasRenderer>();
            var graphic = roads.gameObject.AddComponent<CityRouteMapGraphic>(); graphic.color = new Color(.52f, .59f, .51f);
            var segments = new List<Vector2>();
            foreach (var node in nodes) foreach (var edge in S.City.Map.NeighborsOf(node.Id))
            {
                if (string.CompareOrdinal(node.Id, edge.To) >= 0) continue;
                var other = S.City.Map.GetNode(edge.To); segments.Add(Point(node.X, node.Y)); segments.Add(Point(other.X, other.Y));
            }
            graphic.SetSegments(segments);
            foreach (var p in S.City.Places.Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)))
            {
                var place = p;var door=S.DoorOf(p);
                Vector2 at=Point(door.X*S.City.DistanceScale,door.Y*S.City.DistanceScale);
                var button = Ui.Button(background, S.IsMyShop(p) ? "店" : p.Kind == PlaceKind.Home ? "宅" : CityLifeCatalog.Activities.Any(a => a.VenueId == p.Id) ? "娱" : "·", () => OpenCityPlaceDetail(place), 14,
                    S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? Ui.Warn : S.IsMyShop(p) || S.PropertyAt(p.Id) != null ? Ui.ButtonHot : Ui.ButtonBg);
                button.name="CityMap_"+p.Id;
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = at; rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(25, 23);
                button.Tip(p.Name + " · " + S.City.StreetName(p.StreetId));
            }
            var marker = Ui.Label(background, "我", 18, TextAnchor.MiddleCenter, Ui.Accent); var player = S.PlayerPosition;
            var playerAt = Point(player.X * S.City.DistanceScale, player.Y * S.City.DistanceScale);
            marker.raycastTarget = false; marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = playerAt; marker.rectTransform.pivot = new Vector2(.5f, .5f); marker.rectTransform.sizeDelta = new Vector2(26, 26);
        }

        private void OpenCityPlaceDetail(Place place)
        {
            OpenWindow(place.Name, 810, 650, w =>
            {
                Line(w.Content, S.City.StreetName(place.StreetId) + " · " + (place.Business ?? place.Kind.ToString()), 21, Ui.Accent);
                Line(w.Content, "开放：" + CityPlaceHours(place), 19);
                int minutes = S.CityTravelMinutes(place);
                Line(w.Content, minutes >= int.MaxValue / 8 ? "当前路线不可达，请换一处目的地。" : "当前交通方式预计 " + minutes + " 游戏分钟；按真实路线出发。", 18, minutes >= int.MaxValue / 8 ? Ui.Warn : Ui.Dim);
                if (S.ClosedReason(place) != null) Para(w.Content, S.ClosedReason(place) + "。可以走到门口，抵达后会再次检查进入条件。", 17, Ui.Warn);
                Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 20, Ui.ButtonHot, minutes < int.MaxValue / 8), -1, 48);
                bool favorite = S.CityFavorites.Contains(place.Id);
                Ui.Size(Ui.Button(w.Content, favorite ? "取消收藏" : "收藏地点（最多5个）", () => { S.SetCityFavorite(place.Id, !favorite); w.Rebuild(); }, 18), -1, 44);
                Ui.Size(Ui.Button(w.Content, "设为自定默认目的地", () => { S.SetCityDefault(place.Id); Toast("自定目的地已保存。"); w.Rebuild(); }, 18), -1, 44);
                var shortcuts = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(shortcuts, "替换中介快捷位", () => { S.SetCityShortcut(place.Id, true); Toast("快捷位已保存。"); }, 17), -1, 44, 1);
                Ui.Size(Ui.Button(shortcuts, "替换农贸快捷位", () => { S.SetCityShortcut(place.Id, false); Toast("快捷位已保存。"); }, 17), -1, 44, 1);
                foreach (var a in S.CityActivities.Where(a => a.Active && a.VenueId == place.Id))
                { string id = a.Id; Ui.Size(Ui.Button(w.Content, "查看此处邀约：" + S.CityActivityService(a)?.Name, () => OpenCityActivityDetail(id), 18), -1, 42); }
                if (CityLifeCatalog.Activities.Any(a => a.VenueId == place.Id)) Ui.Size(Ui.Button(w.Content, "场所活动详情", () => OpenCityVenue(place), 18), -1, 44);
            });
        }

        public void OpenCityPeople()
        {
            string search = "";
            OpenWindow("街坊与人际关系", 960, 750, w =>
            {
                Ui.Size(Ui.TextInput(w.Content, search, "搜索已知名字、职业或兴趣", s => { search = s ?? ""; w.RequestRebuild(); }), -1, 42);
                Para(w.Content, "此处只列已认识的人和当前真实在附近的人。查看通讯录不会把对方召到身边；不显示远处人物的精确位置。", 17, Ui.Dim);
                var people = S.CityPeople.Where(p => S.IsMet(p.PersonId) || S.CitySamePlace(p.PersonId))
                    .Where(p => { var d = S.CityPerson(p.PersonId); return d != null && (search.Length == 0 || (d.Name + S.CityPersonRole(p.PersonId) + (p.KnownTopics.Contains("interest") ? d.Interest : "")).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0); })
                    .OrderByDescending(p => S.CitySamePlace(p.PersonId)).ThenBy(p => S.CityPerson(p.PersonId)?.Name).ToArray();
                foreach (var p in people)
                {
                    string id = p.PersonId; var d = S.CityPerson(id); bool met = S.IsMet(id);
                    Ui.Size(Ui.Button(w.Content, d.Name + " · " + S.CityPersonRole(id) + (S.CitySamePlace(id) ? " · 当前在附近" : " · 联系人") + (met ? $"\n熟悉 {S.Relation(id)} · 履约信任 {S.CityTrust(id)} · {(p.KnownTopics.Contains("interest") ? d.Interest : "兴趣尚未聊到")}" : "\n尚未正式认识，可当面打招呼"), () => OpenCityPersonProfile(id), 18), -1, 72);
                }
                if (people.Length == 0) Para(w.Content, "暂时没有可列的人。先到店铺、公共场所或街上靠近居民，再当面打招呼。", 18);
                Ui.Size(Ui.Button(w.Content, "人脉经营故事", OpenCityStories, 18), -1, 44);
            });
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
                bool known = S.HasGreeted(personId);
                if (fromConversation)
                {
                    w.LiveConversation = true;
                    w.LiveKey = () => ConversationViewKey(personId) + "|" + S.MemoriesOf(personId).Count() + "|" + S.LearnedPersonStories(personId).Length;
                    w.LiveStamp = w.LiveKey();
                }
                var top = Ui.Row(w.Content, 200, 16);
                PortraitCard(top, personId, name, "", 145, null);
                var info = Ui.Rect(top, "PersonDetails"); Ui.VList(info, 6, 0); Ui.Size(info, -1, -1, 1);
                Line(info, name, 25, Ui.Accent);
                Para(info, definition != null ? S.CityPersonRole(personId) : npc?.Role ?? staff?.Role, 18);
                Line(info, "熟悉度 " + S.Relation(personId), 17, Ui.Good);
                Ui.Size(Ui.Bar(info, S.Relation(personId) / 100f, Ui.Accent), -1, 8);
                Para(info, S.IsMet(personId) ? "已交换联系方式" : known ? "已经认识 · 尚未交换联系方式" : S.Flags.Contains("heard:" + personId) ? "从熟人口中听说过 · 尚未与本人见面" : "尚未打过招呼", 16, Ui.Dim);
                var tags = Ui.Row(info, 32, 6);
                var traits = S.ConversationTraits(personId);
                for (int i = 0; i < traits.Length; i++)
                {
                    int index = i; var trait = traits[i];
                    Ui.Size(Ui.Button(tags, trait.Known ? trait.Name : "???", () =>
                        OpenWindow(trait.Known ? trait.Name : "尚未了解", 440, 230, detail =>
                            Para(detail.Content, S.ConversationTraitText(personId, index), 18), !fromConversation), 14), -1, 32, 1);
                }
                Line(w.Content, "关于这个人", 20, Ui.Accent);
                if (definition != null)
                    Line(w.Content, topics.Contains("life") ? "年龄：" + definition.Age + " 岁" : "年龄：尚未聊起", 17);
                bool publicWork = state != null && S.CurrentPlace != null && state.LocationPlaceId == state.WorkPlaceId && state.LocationPlaceId == S.CurrentPlace.Id;
                string workplace = topics.Contains("work") || publicWork
                    ? state?.WorkPlaceId != null && S.City.HasPlace(state.WorkPlaceId) ? S.City.GetPlace(state.WorkPlaceId).Name : "目前没有固定工作地点"
                    : "尚未聊起";
                Para(w.Content, "工作地点：" + workplace, 17);
                Para(w.Content, "生活与住处：" + (topics.Contains("life") && state != null ? state.Address : "尚未聊起"), 17, Ui.Dim);

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

        public void OpenCityVenue(Place place)
        {
            if (place == null) return;
            if (S.CurrentPlace != place)
            {
                OpenWindow(place.Name + " · 服务价目表", 950, 740, w => BuildCityVenue(w, place));
                return;
            }
            // Browsing a venue menu uses the same pause hold as other shop menus.
            // Closing releases only this hold and keeps the player at the venue.
            S.PrepareVenueCompanions(place);
            OpenPlaceWindow(place, BuildCityVenue, 1140, 900, holds: true, leaveOnClose: false);
        }

        public void OpenCityActivityChooser(string personId = null)
        {
            if (string.IsNullOrEmpty(personId) || !S.IsMet(personId)) { Toast("请先交换联系方式，再和对方约定活动。一个人消费可直接到场选择服务。"); return; }
            OpenWindow("约" + (S.CityPerson(personId)?.Name ?? "朋友") + "一起去", 900, 750, w =>
            {
                Para(w.Content, "先选活动，再看日期、真实路程、费用和店铺值守影响。预约不会立即消费或增加关系。", 18, Ui.Accent);
                foreach (var definition in CityLifeCatalog.Activities.Where(activity => activity.Id != "snack"))
                {
                    var def = definition; string location = S.City.HasPlace(def.VenueId) ? S.City.GetPlace(def.VenueId).Name : "场所暂不可用";
                    Ui.Size(Ui.Button(w.Content, $"{location} · {def.Minutes}分钟\n到场后选择菜单或服务", () => OpenCityActivityOffer(def.Id, personId), 18), -1, 66);
                }
            });
        }

        public void OpenCityActivityOffer(string definitionId, string personId = null)
        {
            var def = CityLifeCatalog.Activity(definitionId); if (def == null) return;
            if (string.IsNullOrEmpty(personId) || !S.IsMet(personId)) { Toast("邀约需要先和对方交换联系方式。单人消费直接到场办理。"); return; }
            long proposed = S.SuggestCityActivityStart(definitionId, personId); bool approveEmployee = false;
            var selected = new List<string>(); if (personId != null) selected.Add(personId);
            OpenWindow("安排：" + def.Name, 950, 770, w =>
            {
                if (!S.City.HasPlace(def.VenueId)) { Line(w.Content, "实际场所暂不可用。", 18, Ui.Warn); return; }
                // Always choose a future quarter-hour; crossing midnight also advances the date.
                long earliestStart=(S.Clock.Now.TotalMinutes/15+1)*15;
                proposed=Math.Max((proposed+14)/15*15,earliestStart);
                var venue = S.City.GetPlace(def.VenueId);
                Line(w.Content, venue.Name + " · " + S.City.StreetName(venue.StreetId) + " · " + CityPlaceHours(venue), 20, Ui.Accent);
                Line(w.Content, $"预约开始 {CityTime(proposed)}\n活动 {def.Minutes}分钟 · 约定时间前后1小时内到场即可集合 · 服务参考价 ¥{def.PriceYuan}（到场后选定）", 20);
                int journey = S.CityTravelMinutes(venue);
                Para(w.Content, "当前交通路程预计 " + (journey >= int.MaxValue / 8 ? "不可达" : journey + "分钟") + "。费用在实际到场并开始时支付；接受只登记日程与时段占用。", 17, Ui.Dim);
                var date=Ui.Row(w.Content,42,8);Ui.Size(Ui.Label(date,"日期",18),65);
                int today=(int)(earliestStart/1440);
                int days=Math.Max(36,(int)(proposed/1440)-today+1);
                Ui.Size(Ui.Choice(date,Enumerable.Range(0,days).Select(offset=>S.DescribeDay(today+offset)+" · "+new GameDate(today+offset)),(int)(proposed/1440)-today,value=>{proposed=(long)(today+value)*1440+proposed%1440;w.RequestRebuild();}),-1,42,1);
                ClockSelector(w.Content,"开始",(int)(proposed%1440),value=>{proposed=proposed/1440*1440+value;w.RequestRebuild();},
                    proposed/1440==earliestStart/1440?(int)(earliestStart%1440):0);
                Ui.Size(Ui.Button(w.Content,"推荐可赴约时间",()=>{proposed=S.SuggestCityActivityStart(definitionId,selected.FirstOrDefault());w.Rebuild();},17),-1,40);
                Line(w.Content, "同行：" + (S.CityPerson(personId)?.Name ?? "朋友"), 18, Ui.Accent);
                Para(w.Content, "这次只约定见面地点和时间。约定时间前后1小时内到场，同伴直接在场，再一起选择菜单或服务。", 17, Ui.Dim);
                bool employee = selected.Any(id => S.Employees.Any(e => S.PersonIdOf(e.Person) == id));
                if (employee) Ui.Size(Ui.Button(w.Content, (approveEmployee ? "✓ 已批准" : "明确批准") + "所选员工在活动出发/参加期间离岗", () => { approveEmployee = !approveEmployee; w.Rebuild(); }, 18, approveEmployee ? Ui.Warn : Ui.ButtonBg), -1, 48);
                Para(w.Content, "值守提示：只有你守店时，真正离店后暂停现场服务；被邀请的员工真正出发后不再提供店内能力，其他实际当班员工仍可工作。查看远程经营总览不会恢复值守。", 17, Ui.Warn);
                foreach (var shop in S.MyShops) Line(w.Content, S.ShopTitle(shop) + "：" + S.CityServiceReason(shop), 17);
                string blocker = S.CityActivityBlocker(def.Id, selected, proposed, approveEmployee);
                if (blocker != null) Para(w.Content, blocker, 18, Ui.Warn);
                var actions = Ui.Row(w.Content, 48, 8);
                void ConfirmSchedule()
                {
                    string[] participants = selected.ToArray(); long start = proposed; bool approval = approveEmployee;
                    ConfirmBusiness("确认邀约", venue.Name + " · 与" + (S.CityPerson(personId)?.Name ?? "朋友") + "见面\n"
                        + CityTime(start) + "，预留 " + def.Minutes + " 分钟。\n到场后选择菜单或服务；现在不扣消费，也不自动出发。", () =>
                    {
                        var activity = S.ScheduleCityActivity(def.Id, participants, start, approval);
                        if (activity == null) return;
                        CloseAllWindows(); Toast("已记入日程，临近出发时间会提醒你。");
                    });
                }
                Ui.Size(Ui.Button(actions, "确认邀约，加入日程", () => ConfirmSchedule(), 19, Ui.ButtonHot, blocker == null), -1, 48, 1);
            });
        }

        public void OpenCityActivities()
        {
            OpenWindow("城市活动日程", 980, 750, w =>
            {
                Para(w.Content, S.CityExpoCalendar, 19, Ui.Accent);
                Para(w.Content, "与人物聊天时约定见面，邀约会记录在这里。一个人用餐或游玩，直接到店选择服务即可。", 18, Ui.Dim);
                foreach (var activity in S.CityActivities.OrderByDescending(a => a.Active).ThenBy(a => a.Active ? a.ScheduledStart : -a.ScheduledStart).Take(30))
                {
                    var a = activity; var def = S.CityActivityService(a);
                    string venue = S.City.HasPlace(a.VenueId) ? S.City.GetPlace(a.VenueId).Name : "场所失效";
                    Ui.Size(Ui.Button(w.Content, GameSession.CityPhaseText(a.State) + " · " + (def?.Name ?? a.DefinitionId) + " · " + venue + "\n" + CityTime(a.ScheduledStart) + (a.EntryPaid ? " · 已产生实际消费" : " · 尚未支付活动费"), () => OpenCityActivityDetail(a.Id), 18), -1, 72);
                }
                if (S.CityActivities.Count == 0) Para(w.Content, "还没有活动记录。在和朋友聊天时发出邀约；到场后再点菜或选择服务。", 18, Ui.Dim);
            });
        }

        public void OpenCityActivitySettlement(string activityId)
        {
            var activity=S.CityActivities.FirstOrDefault(a=>a.Id==activityId);
            if(string.IsNullOrEmpty(activity?.SettlementSummary))return;
            bool solo=activity.Participants.Length==0;
            OpenWindow(solo?"活动结算":"相处结算",800,740,w=>
            {
                Para(w.Content,activity.SettlementSummary,19);
                Ui.Size(Ui.Button(w.Content,"收好这段回忆",w.Close,20,Ui.ButtonHot),-1,46);
            });
        }

        private void OpenCityActivityDetail(string activityId)
        {
            OpenWindow("活动详情（关闭后继续世界时间）", 860, 710, w =>
            {
                var a = S.CityActivities.FirstOrDefault(x => x.Id == activityId); if (a == null) { Line(w.Content, "记录暂不可用。", 18); return; }
                var def = S.CityActivityService(a);
                Line(w.Content, (def?.Name ?? a.DefinitionId) + " · " + GameSession.CityPhaseText(a.State), 22, Ui.Accent);
                Line(w.Content, "地点：" + (S.City.HasPlace(a.VenueId) ? S.City.GetPlace(a.VenueId).Name : a.VenueId) + "\n预约：" + CityTime(a.ScheduledStart), 18);
                foreach (var p in a.Participants) Line(w.Content, (S.CityPerson(p.PersonId)?.Name ?? "同行者") + " · " + CityParticipantText(p.Phase), 18);
                Para(w.Content, $"阶段 {a.Stage + 1}/3 · 活动费用 {(a.EntryPaid ? "已支付" : "尚未支付")} · 完成奖励 {(a.RewardApplied ? "已结算" : "尚未结算")}", 18);
                if (a.State == CityActivityPhase.Running)
                {
                    long remaining = Math.Max(0, a.ActualStart + a.Duration - S.Clock.Now.TotalMinutes);
                    Line(w.Content, "预计剩余 " + remaining + " 游戏分钟；世界时间按原倍速推进。", 19);
                    Para(w.Content, a.Stage == 0 ? def?.StartText : def?.MidText, 19);
                    Ui.Size(Ui.Button(w.Content, "打开场所菜单", () => GoToScheduledCityActivity(activityId), 18, Ui.ButtonHot,
                        CityActivityMenuOrTravelBlocker(activityId) == null), -1, 44);
                    if (def != null) for (int i = 0; i < def.Choices.Length; i++)
                    {
                        int index = i; bool done = a.Choices.Contains(index.ToString());
                        Ui.Size(Ui.Button(w.Content, (done ? "✓ " : "") + def.Choices[i], () => { CloseAllWindows(); S.ChooseCityActivity(activityId, index); }, 18, done ? Ui.ButtonBg : Ui.ButtonHot), -1, 44);
                    }
                    Ui.Size(Ui.Button(w.Content, "收起面板 / 继续活动", CloseAllWindows, 19, Ui.ButtonHot), -1, 46);
                }
                else if (a.Active)
                {
                    string why = CityActivityMenuOrTravelBlocker(activityId);
                    Ui.Size(Ui.Button(w.Content, S.CurrentPlace?.Id == a.VenueId ? "打开菜单 / 选择服务" : "前往赴约", () => GoToScheduledCityActivity(activityId), 19, Ui.ButtonHot, why == null), -1, 46);
                    if (why != null) Para(w.Content, why, 16, Ui.Dim);
                }
                if (a.Active) Ui.Size(Ui.Button(w.Content, a.State == CityActivityPhase.Running ? "提前离开" : "取消预约", () => ConfirmBusiness("结束本次安排", a.EntryPaid ? "保留已发生费用和阶段效果，未完成阶段不给奖励。你仍留在实际场所，自行选择下一站。" : "尚未发生的活动费不扣除。取消预约与中途离开分别记录。", () => { CloseAllWindows(); S.CancelCityActivity(activityId, true); }), 18, Ui.Warn), -1, 44);
                if(a.State==CityActivityPhase.Scheduled&&a.DefinitionId.StartsWith("expo_",StringComparison.Ordinal))
                    foreach(var participant in a.Participants)
                    {
                        var worker=S.Employees.FirstOrDefault(e=>S.PersonIdOf(e.Person)==participant.PersonId);if(worker?.Shop==null)continue;
                        string employeeId=worker.Person.Id;bool work=participant.WorkForEmployer;
                        Ui.Size(Ui.Button(w.Content,worker.Person.Name+(work?"：展位工作（含路程计工时）":"：私人同行，改为展位工作"),()=>{S.SetEmployeeExhibitionWork(activityId,employeeId,!work);w.Rebuild();},17),-1,42);
                    }
                if (!string.IsNullOrEmpty(a.SettlementSummary))
                    Ui.Size(Ui.Button(w.Content,a.Participants.Length==0?"查看活动结算":"查看相处结算",()=>OpenCityActivitySettlement(activityId),19,Ui.ButtonHot),-1,44);
                if (a.EndReason != null) Para(w.Content, a.EndReason, 18, Ui.Dim);
            });
        }
        private static string CityParticipantText(CityParticipantPhase p) => p == CityParticipantPhase.NotDeparted ? "尚未出发" : p == CityParticipantPhase.Travelling ? "正在赴约" : p == CityParticipantPhase.Waiting ? "已到场等候" : p == CityParticipantPhase.Participating ? "正在参加" : "已离场";

        public void BuildCityLifeHud(Transform parent)
        {
            if (_cityActivityHud != null) return;
            _cityActivityHud = Ui.Panel(parent, "CityActivityHud", Ui.PanelBg).rectTransform;
            _cityActivityHud.Anchor(.28f, 0, .78f, 0, 0, 108, 0, -218);
            var list = Ui.VList(_cityActivityHud, 4, 8);
            _cityActivityHudText = Ui.Label(_cityActivityHud, "", 18);
            Ui.Size(_cityActivityHudText, -1, 48);
            var row = Ui.Row(_cityActivityHud, 40, 7);
            _cityActivityDetails = Ui.Size(Ui.Button(row, "日程详情", () => { if (_cityHudActivityId != null) OpenCityActivityDetail(_cityHudActivityId); }, 18), -1, 40, 1);
            _cityActivityGo = Ui.Size(Ui.Button(row, "前往赴约", () => { if (_cityHudActivityId != null) GoToScheduledCityActivity(_cityHudActivityId); }, 18, Ui.ButtonHot), -1, 40, 1);
            _cityActivityEnd = Ui.Size(Ui.Button(row, "取消邀约", () => { if (_cityHudActivityId != null) OpenCityActivityDetail(_cityHudActivityId); }, 18, Ui.Warn), -1, 40, 1);
            Ui.Size(Ui.Button(row, "收起", () =>
            {
                if (_cityHudActivityId != null) _dismissedCityActivityHud.Add(_cityHudActivityId);
                _cityActivityHud.gameObject.SetActive(false);
            }, 16), 72, 40).Tip("只隐藏这条提醒；导航和邀约保留，仍可从日程查看。");
            _cityActivityHud.gameObject.SetActive(false);
        }
        public void UpdateCityLifeHud(float dt)
        {
            if (_cityActivityHud == null || S == null) return;
            _cityHudRefresh -= dt; if (_cityHudRefresh > 0) return; _cityHudRefresh = .2f;
            if (_cityHudSession != S) { _cityHudSession = S; _dismissedCityActivityHud.Clear(); }
            var a = S.RunningCityActivity;
            if (a != null && _dismissedCityActivityHud.Contains(a.Id)) a = null;
            if (a == null) a = S.CityActivities.Where(x => x.Active && !_dismissedCityActivityHud.Contains(x.Id)
                && (x.Reminder || x.PlayerIntentId != null || S.CurrentPlace?.Id == x.VenueId)).OrderBy(x => x.ScheduledStart).FirstOrDefault();
            _cityActivityHud.gameObject.SetActive(a != null);
            _cityHudActivityId = a?.Id;
            if (a == null) return;
            _cityActivityDetails.GetComponentInChildren<Text>().text = a.State == CityActivityPhase.Running ? "本次消费" : "日程详情";
            _cityActivityEnd.GetComponentInChildren<Text>().text = a.State == CityActivityPhase.Running ? "提前离开" : "取消邀约";
            bool here = S.CurrentPlace?.Id == a.VenueId;
            bool travelling = !here && S.CityIntent?.ActivityId == a.Id && !S.CityIntent.Cancelled;
            _cityActivityGo.interactable = !travelling && CityActivityMenuOrTravelBlocker(a.Id) == null && !_ctrl.Placing;
            _cityActivityGo.GetComponentInChildren<Text>().text = here ? "打开菜单 / 服务" : travelling ? "正在前往" : "前往赴约";
            var def = S.CityActivityService(a);
            string phase = travelling ? "正在赴约" : GameSession.CityPhaseText(a.State);
            string time = a.State == CityActivityPhase.Running ? "剩余 " + Math.Max(0, a.ActualStart + a.Duration - S.Clock.Now.TotalMinutes) + " 分钟" : "预约 " + CityTime(a.ScheduledStart);
            _cityActivityHudText.text = (def?.Name ?? a.DefinitionId) + " · " + phase + " · " + time + "\n" + (a.State == CityActivityPhase.Running ? a.Stage == 0 ? def?.StartText : def?.MidText : here ? "已经到场。在菜单里选择服务，与同行者一起开始活动。" : travelling ? "正在沿路线前往约定场所；可收起提醒，导航会继续。" : "赴约时间临近；空闲时点击“前往赴约”出发。");
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
                Preset("具体物业与投资", "assets", "从正常中介购买、租转买、持仓交易入口验收。 ");
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
