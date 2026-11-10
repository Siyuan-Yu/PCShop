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
                Ui.Size(Ui.Button(w.Content, "城市地图 / 自定目的地", OpenCityMap, 20, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, "街坊与已认识的人", OpenCityPeople, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "活动日程 / 展会日历", OpenCityActivities, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "人脉经营故事", OpenCityStories, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, "资产 · 门店与自有物业", OpenCityFinanceOverview, 20), -1, 50);
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
            || _cityMapType == "venue" && CityLifeCatalog.Activities.Any(a => a.VenueId == p.Id)
            || _cityMapType == "source" && (p.Kind == PlaceKind.Wholesaler || S.HasRetailWholesale(p))
            || _cityMapType == "home" && p.Kind == PlaceKind.Home;
        private int CityMapPriority(Place p) => S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? 0 : S.CityFavorites.Contains(p.Id) ? 1 : S.CityRecent.Contains(p.Id) ? 2 : 3;
        private string CityMapPlaceName(Place place) => S.ShopAt(place) is OwnedShop shop ? S.ShopTitle(shop) : place.Name;
        private string CityMapPlaceType(Place place) => S.ShopAt(place) is OwnedShop shop
            ? RetailShopTypes.Name(shop.RetailKind) : place.Business ?? place.Kind.ToString();

        public void OpenCityMap()=>OpenCityMap(null);
        private void OpenCityMap(Action<Place> onSelected)
        {
            _cityMapPage = 0;
            OpenWindow(onSelected==null?"十街地图 / 自定义目的地":"选择自定目的地", 1110, 790, w =>
            {
                void Pick(Place place)
                {
                    if(onSelected==null){OpenCityPlaceDetail(place);return;}
                    onSelected(place);w.Close();
                }
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
                    Line(w.Content, "底栏自定目的地：" + CityMapPlaceName(S.City.GetPlace(S.CityDefaultDestination)), 17, Ui.Accent);
                if (S.CityIntent != null || S.Trip != null) Ui.Size(Ui.Button(w.Content, "取消当前路线（预约保留）", () => { S.StopTrip(); S.CancelCityTravel(); w.Rebuild(); }, 18, Ui.Warn), -1, 42);
                BuildCityRouteMap(w.Content,Pick);
                var places = S.City.PublicPlaces.Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)
                    && (_cityMapSearch.Length == 0 || (CityMapPlaceName(p) + " " + CityMapPlaceType(p) + " " + S.City.StreetName(p.StreetId)).IndexOf(_cityMapSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(CityMapPriority).ThenBy(p => p.StreetId).ThenBy(CityMapPlaceName).ToArray();
                _cityMapPage = Math.Max(0, Math.Min(_cityMapPage, Math.Max(0, (places.Length - 1) / 12)));
                Line(w.Content, onSelected==null?"当前邀约优先，其次收藏和最近前往；地图不显示陌生人的实时位置。":"选择地点后保留在底栏，点击底栏地点主按钮才出发。", 17, Ui.Dim);
                foreach (var p in places.Skip(_cityMapPage * 12).Take(12))
                {
                    var place = p;
                    string tag = S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? "[邀约] " : S.CityFavorites.Contains(p.Id) ? "★ " : S.CityRecent.Contains(p.Id) ? "[最近] " : "";
                    Ui.Size(Ui.Button(w.Content, tag + CityMapPlaceName(p) + " · " + S.City.StreetName(p.StreetId) + " · " + CityMapPlaceType(p) + "\n" + CityPlaceHours(p) + (S.IsMyShop(p) ? " · 我的店" : S.PropertyAt(p.Id) != null ? " · 已持有物业" : ""), () => Pick(place), 18), -1, 68).name="CityMapList_"+p.Id;
                }
                if (places.Length == 0) Line(w.Content, "没有匹配地点，可清除搜索或切换筛选。", 18, Ui.Dim);
                var pages = Ui.Row(w.Content, 40, 8);
                Ui.Size(Ui.Button(pages, "上一页", () => { _cityMapPage--; w.Rebuild(); }, 18, Ui.ButtonBg, _cityMapPage > 0), -1, 40, 1);
                Ui.Size(Ui.Label(pages, $"第 {_cityMapPage + 1} 页 · {places.Length} 个地点", 17, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(pages, "下一页", () => { _cityMapPage++; w.Rebuild(); }, 18, Ui.ButtonBg, (_cityMapPage + 1) * 12 < places.Length), -1, 40, 1);
            });
        }

        private void BuildCityRouteMap(Transform parent,Action<Place> onSelected=null)
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
            foreach (var p in S.City.PublicPlaces.Where(p => (_cityMapStreet == null || p.StreetId == _cityMapStreet) && CityMapTypeMatches(p)))
            {
                var place = p;var door=S.DoorOf(p);
                Vector2 at=Point(door.X*S.City.DistanceScale,door.Y*S.City.DistanceScale);
                var button = Ui.Button(background, S.IsMyShop(p) ? "店" : p.Kind == PlaceKind.Home ? "宅" : CityLifeCatalog.Activities.Any(a => a.VenueId == p.Id) ? "娱" : "·", () => {if(onSelected!=null)onSelected(place);else OpenCityPlaceDetail(place);}, 14,
                    S.CityActivities.Any(a => a.Active && a.VenueId == p.Id) ? Ui.Warn : S.IsMyShop(p) || S.PropertyAt(p.Id) != null ? Ui.ButtonHot : Ui.ButtonBg);
                button.name="CityMap_"+p.Id;
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = at; rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(25, 23);
                button.Tip(CityMapPlaceName(p) + " · " + S.City.StreetName(p.StreetId));
            }
            var marker = Ui.Label(background, "我", 18, TextAnchor.MiddleCenter, Ui.Accent); var player = S.PlayerPosition;
            var playerAt = Point(player.X * S.City.DistanceScale, player.Y * S.City.DistanceScale);
            marker.raycastTarget = false; marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = playerAt; marker.rectTransform.pivot = new Vector2(.5f, .5f); marker.rectTransform.sizeDelta = new Vector2(26, 26);
        }

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
                if (CityLifeCatalog.Activities.Any(a => a.VenueId == place.Id)) Ui.Size(Ui.Button(w.Content, "场所活动详情", () => OpenCityVenue(place), 18), -1, 44);
            });
        }

        private string _cityPeopleSearch = "";
        public void OpenCityPeople()
        {
            OpenWorkspace("people", "people", "街坊与人际关系", 960, 750, w =>
            {
                BuildPeopleNavigation(w,"people");
                Ui.Size(Ui.TextInput(w.Content, _cityPeopleSearch, "搜索已知名字、职业或兴趣", s => { _cityPeopleSearch = s ?? ""; w.RequestRebuild(); }), -1, 42);
                Para(w.Content, "此处只列已认识的人和当前真实在附近的人。查看通讯录不会把对方召到身边；不显示远处人物的精确位置。", 17, Ui.Dim);
                var people = S.CityPeople.Where(p => S.IsMet(p.PersonId) || S.CitySamePlace(p.PersonId))
                    .Where(p => { var d = S.CityPerson(p.PersonId); return d != null && (_cityPeopleSearch.Length == 0 || (d.Name + NpcNames.Alias(p.PersonId) + S.CityPersonRole(p.PersonId) + (p.KnownTopics.Contains("interest") ? d.Interest : "")).IndexOf(_cityPeopleSearch, StringComparison.OrdinalIgnoreCase) >= 0); })
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
                bool known = S.HasGreeted(personId);
                if (fromConversation)
                {
                    w.LiveConversation = true;
                    w.LiveKey = () => ConversationViewKey(personId) + "|" + S.MemoriesOf(personId).Count() + "|" + S.LearnedPersonStories(personId).Length;
                    w.LiveStamp = w.LiveKey();
                }
                var top = Ui.Row(w.Content, 232, 16);
                PortraitCard(top, personId, name, "", 145, null);
                var info = Ui.Rect(top, "PersonDetails"); Ui.VList(info, 6, 0); Ui.Size(info, -1, -1, 1);
                Line(info, name, 25, Ui.Accent);
                if(NpcNames.Alias(personId) is string alias)Line(info,"熟人称呼："+alias,15,Ui.Dim);
                Para(info, definition != null ? S.CityPersonRole(personId) : npc?.Role ?? staff?.Role, 18);
                Line(info, "熟悉度 " + S.Relation(personId), 17, Ui.Good);
                Line(info, StandingText(S.PersonStandingOf(personId), true), 17, Ui.Accent).name = "PersonStanding";
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
                if (!fromConversation)
                {
                    var actions=Ui.Row(w.Content,38,6);
                    bool nearby=S.CitySamePlace(personId);
                    Ui.Size(Ui.Button(actions,"现场交谈",()=>OpenPersonDialogue(personId),17,Ui.ButtonHot,nearby),-1,38,1).Tip(nearby?"与面前的人交谈":"需要实际到同一地点");
                    bool saved=S.SavedConversationContacts().Any(c=>c.Id==personId);
                    Ui.Size(Ui.Button(actions,"电话 / 消息",()=>OpenPersonDialogue(personId,parent:w,remote:true),17,Ui.ButtonBg,saved),-1,38,1).Tip(saved?"使用已交换的联系方式":"先当面交换联系方式");
                    Ui.Size(Ui.Button(actions,"邀约",()=>OpenInvitePanel(personId),17,Ui.ButtonBg,saved),-1,38,1);
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
            if (place.Id == "life_expo") { OpenExpoReception(); return; }
            if (place.Id == GameSession.CommunityMcnId) { OpenCommunityMcn(); return; }
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
            bool choosingRace = false;
            string raceTrack = RaceTracks.Get(S.CurrentPlace?.Id)?.Id;
            OpenWindow("约" + (S.CityPerson(personId)?.Name ?? "朋友") + "一起去", 900, 750, w =>
            {
                if (choosingRace)
                {
                    Ui.Size(Ui.Button(w.Content, "‹ 返回活动列表", () =>
                    { choosingRace = false; w.ResetScrollOnRebuild = true; w.Rebuild(); }, 17), -1, 38);
                    BuildRaceInvitationChoices(w, personId, raceTrack, track => raceTrack = track);
                    return;
                }
                Para(w.Content, "先选活动，再看日期、真实路程、费用和店铺值守影响。预约不会立即消费或增加关系。", 18, Ui.Accent);
                if (GameSession.LikesRacing(personId))
                    Ui.Size(Ui.Button(w.Content, "一起参赛 · 选择赛场和比赛场次", () =>
                    { choosingRace = true; w.ResetScrollOnRebuild = true; w.Rebuild(); }, 18), -1, 54).name = "CityActivityOffer_racing";
                foreach (var definition in CityLifeCatalog.Activities.Where(activity => activity.Id != "snack"))
                {
                    var def = definition; string location = S.City.HasPlace(def.VenueId) ? S.City.GetPlace(def.VenueId).Name : "场所暂不可用";
                    Ui.Size(Ui.Button(w.Content, $"{def.Name} · {location} · {def.Minutes}分钟\n到场后选择菜单或服务", () => OpenCityActivityOffer(def.Id, personId), 18), -1, 66).name="CityActivityOffer_"+def.Id;
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
                Line(w.Content, $"{def.Minutes} 分钟 · 参考价 {S.VenuePriceRange(def)}", 18);
                int journey = S.CityTravelMinutes(venue);
                Line(w.Content, journey >= int.MaxValue / 8 ? "当前路线不可达" : $"路程约 {journey} 分钟 · 到场后付费", 16, Ui.Dim);
                var date=Ui.Row(w.Content,42,8);Ui.Size(Ui.Label(date,"日期",18),65);
                int today=(int)(earliestStart/1440);
                int days=Math.Max(36,(int)(proposed/1440)-today+1);
                Ui.Size(Ui.Choice(date,Enumerable.Range(0,days).Select(offset=>S.DescribeDay(today+offset)+" · "+new GameDate(today+offset)),(int)(proposed/1440)-today,value=>{proposed=(long)(today+value)*1440+proposed%1440;w.RequestRebuild();}),-1,42,1);
                ClockSelector(w.Content,"开始",(int)(proposed%1440),value=>{proposed=proposed/1440*1440+value;w.RequestRebuild();},
                    proposed/1440==earliestStart/1440?(int)(earliestStart%1440):0);
                Ui.Size(Ui.Button(w.Content,"推荐可赴约时间",()=>{proposed=S.SuggestCityActivityStart(definitionId,selected.FirstOrDefault());w.Rebuild();},17),-1,40);
                PersonSummary(w.Content, personId, S.CityPerson(personId)?.Name ?? "朋友", "约定时间前后 1 小时内到场");
                bool employee = selected.Any(id => S.Employees.Any(e => S.PersonIdOf(e.Person) == id));
                if (employee) Ui.Size(Ui.Button(w.Content, (approveEmployee ? "✓ 已批准" : "明确批准") + "所选员工在活动出发/参加期间离岗", () => { approveEmployee = !approveEmployee; w.Rebuild(); }, 18, approveEmployee ? Ui.Warn : Ui.ButtonBg), -1, 48);
                if (employee) Para(w.Content, "受邀员工出发后将暂时离岗。", 16, Ui.Warn);
                string blocker = S.CityActivityBlocker(def.Id, selected, proposed, approveEmployee);
                if (blocker != null) Ui.Notice(w.Content, blocker);
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

        public void OpenCityActivities() => OpenCalendar(1);
        private void BuildActivitySchedule(Window w)
        {
                Para(w.Content, S.CityExpoCalendar, 19, Ui.Accent);
                HelpLink(w.Content,"邀约与活动说明","从人物资料发出邀约，到约定地点后选择服务。个人维修、快递和用餐等安排在“我的安排”中查看。");
                foreach (var activity in S.CityActivities.OrderByDescending(a => a.Active).ThenBy(a => a.Active ? a.ScheduledStart : -a.ScheduledStart).Take(30))
                {
                    var a = activity; var def = S.CityActivityService(a);
                    string venue = S.City.HasPlace(a.VenueId) ? S.City.GetPlace(a.VenueId).Name : "场所失效";
                    Ui.Size(Ui.Button(w.Content, GameSession.CityPhaseText(a.State) + " · " + (def?.Name ?? a.DefinitionId) + " · " + venue + "\n" + CityTime(a.ScheduledStart) + (a.EntryPaid ? " · 已产生实际消费" : " · 尚未支付活动费"), () => OpenCityActivityDetail(a.Id), 18), -1, 72);
                }
                if (S.CityActivities.Count == 0) Para(w.Content, "还没有活动记录。在和朋友聊天时发出邀约；到场后再点菜或选择服务。", 18, Ui.Dim);
        }

        public void OpenCityActivitySettlement(string activityId)
        {
            var activity = S.CityActivities.FirstOrDefault(a => a.Id == activityId);
            if (string.IsNullOrEmpty(activity?.SettlementSummary)) return;
            bool solo = activity.Participants.Length == 0;
            OpenWindow(solo ? "活动结算" : "相处结算", solo ? 780 : 960, 650, w =>
            {
                var definition = S.CityActivityService(activity);
                var overview = Ui.Row(w.Content, 98, 14);
                var heading = SettlementCard(overview, "ActivityOverview", 2);
                Line(heading, definition?.Name ?? "活动", 24);
                string venue = S.City.HasPlace(activity.VenueId) ? S.City.GetPlace(activity.VenueId).Name : activity.VenueId;
                Para(heading, venue + " · " + GameSession.VenueServiceName(definition, Math.Max(0, activity.ServiceChoice)), 16, Ui.Dim);

                var cost = SettlementCard(overview, "ActivityCost");
                Line(cost, activity.EntryCostRecorded ? "本次支出" : "参考费用", 15, Ui.Dim);
                var amount = activity.EntryCostRecorded ? Money.FromCents(activity.EntryCostCents) : Money.FromYuan(definition?.PriceYuan ?? 0);
                Line(cost, amount.ToString(), 24, Ui.Accent);

                var duration = SettlementCard(overview, "ActivityDuration");
                Line(duration, "体验时长", 15, Ui.Dim);
                string minutes = activity.ActualStart >= 0 && activity.ActualEnd >= activity.ActualStart
                    ? $"{activity.ActualEnd - activity.ActualStart} 分钟" : "—";
                Line(duration, minutes, 24);

                var body = Ui.Scroll(w.Content);
                Ui.Size((RectTransform)body.parent.parent, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
                var bodyLayout = body.GetComponent<VerticalLayoutGroup>();
                bodyLayout.spacing = 10;
                bodyLayout.padding = new RectOffset(0, 2, 2, 2);
                if (activity.PlayerStatsAfter == null || activity.PlayerStatsAfter.Length == 0)
                    Para(body, activity.SettlementSummary, 17);
                else
                {
                    var results = Ui.Row(body, -1, 12);
                    var layout = results.GetComponent<HorizontalLayoutGroup>();
                    layout.childAlignment = TextAnchor.UpperLeft;
                    layout.childForceExpandHeight = false;
                    if (!solo) BuildActivityRelationshipResults(results, activity);
                    BuildActivityPersonalResults(results, activity);

                    var memory = SettlementCard(body, "ActivityMemory");
                    string[] interactions = (activity.Choices ?? Array.Empty<string>())
                        .Select(key => int.TryParse(key, out int index) && index >= 0 && index < (definition?.Choices.Length ?? 0)
                            ? definition.Choices[index] : null).Where(choice => choice != null).ToArray();
                    if (interactions.Length > 0)
                    {
                        Line(memory, solo ? "体验内容" : "一起做过", 18, Ui.Accent);
                        Para(memory, string.Join("、", interactions), 17);
                    }
                    if (!string.IsNullOrWhiteSpace(activity.EndReason)) Para(memory, activity.EndReason, 16, Ui.Dim);
                }

                var footer = Ui.Row(w.Content, 44, 10);
                bool completed = activity.State == CityActivityPhase.Completed;
                string status = completed && !solo ? "已记入人脉记录" : GameSession.CityPhaseText(activity.State);
                Ui.Size(Ui.Label(footer, status, 16, TextAnchor.MiddleLeft, completed ? Ui.Good : Ui.FlatDown), 0, -1, 1);
                Ui.Size(Ui.Button(footer, solo ? "完成" : "收好这段回忆", w.Close, 18, Ui.ButtonHot), 200, 44);
            }, onClosed: () => OpenCommunityActivityClue(activityId), scrollContent: false);
        }

        private static RectTransform SettlementCard(Transform parent, string name, float flexWidth = 1)
        {
            var card = Ui.FlatPanel(parent, name).rectTransform;
            Ui.VList(card, 3, 10);
            var size = Ui.Size(card, 0, -1, flexWidth).GetComponent<LayoutElement>();
            size.minWidth = 0;
            return card;
        }

        private void BuildActivityRelationshipResults(Transform parent, CityActivity activity)
        {
            var card = SettlementCard(parent, "ActivityRelationships", 1.2f);
            Line(card, "关系变化", 20, Ui.Accent);
            foreach (var participant in activity.Participants)
            {
                var person = SettlementCard(card, "ActivityCompanion");
                person.GetComponent<Image>().color = Ui.FlatBand;
                var identity = Ui.Row(person, 46, 9);
                PortraitThumb(identity, participant.PersonId, 44, null);
                Ui.Size(Ui.Label(identity, "<b>" + (S.CityPerson(participant.PersonId)?.Name ?? "同行者") + "</b>", 19), 0, -1, 1);
                SettlementChange(person, "好感", participant.ResultCaptured ? participant.RelationBefore : (double?)null, participant.RelationAfter);
                SettlementChange(person, "履约信任", participant.ResultCaptured ? participant.TrustBefore : (double?)null, participant.TrustAfter);
                if (activity.State == CityActivityPhase.Completed && activity.DefinitionId == "dinner" && S.IsMet(participant.PersonId))
                    Para(person, "可在手机通讯录联系", 14, Ui.Dim);
            }
        }

        private static void BuildActivityPersonalResults(Transform parent, CityActivity activity)
        {
            var card = SettlementCard(parent, "ActivityPersonalChanges");
            Line(card, "个人状态", 20, Ui.Accent);
            Para(card, "含活动期间的日常消耗", 14, Ui.Dim);
            var names = GameSession.CityActivityStatNames;
            bool any = false;
            for (int i = 0; i < Math.Min(names.Count, activity.PlayerStatsAfter.Length); i++)
            {
                double after = activity.PlayerStatsAfter[i];
                double? before = i < (activity.PlayerStatsBefore?.Length ?? 0) ? activity.PlayerStatsBefore[i] : (double?)null;
                if (before.HasValue && Math.Round(after - before.Value, 1) == 0) continue;
                SettlementChange(card, names[i], before, after);
                any = true;
            }
            if (!any) Para(card, "本次没有数值变化", 16, Ui.Dim);
        }

        private static void SettlementChange(Transform parent, string label, double? before, double after)
        {
            var row = Ui.Row(parent, 26, 6);
            Ui.Size(Ui.Label(row, label, 16), 82);
            string values = before.HasValue ? $"{before.Value:0.#} → {after:0.#}" : $"{after:0.#}";
            Ui.Size(Ui.Label(row, values, 16, TextAnchor.MiddleLeft, Ui.Dim), 0, -1, 1);
            if (!before.HasValue) return;
            double delta = Math.Round(after - before.Value, 1);
            var color = delta > 0 ? Ui.Good : delta < 0 ? Ui.FlatDown : Ui.Dim;
            Ui.Size(Ui.Label(row, $"<b>{delta:+0.#;-0.#;0}</b>", 20, TextAnchor.MiddleRight, color), 68);
        }

        private void OpenCommunityActivityClue(string activityId)
        {
            int lead=S.TakeCommunityActivityClue(activityId);
            if(lead<0)return;
            string id=GameSession.CommunityLeadPerson(lead);
            string name=S.CityPerson(id)?.Name??id;
            OpenDialogue(id,name,"活动后的话",null,"",6,ctx=>new System.Collections.Generic.List<TalkOpt>(),
                firstReply:GameSession.CommunityLeadHint(lead)+"\n\n已记下藏箱线索，可以按主线导航前去寻找。",
                onClosed:()=>OpenCommunityActivityClue(activityId),remote:true,replyOnly:true,replyConfirm:"记下线索");
        }

        private void OpenCityActivityDetail(string activityId)
        {
            OpenWindow("活动详情（关闭后继续世界时间）", 860, 710, w =>
            {
                var a = S.CityActivities.FirstOrDefault(x => x.Id == activityId); if (a == null) { Line(w.Content, "记录暂不可用。", 18); return; }
                var def = S.CityActivityService(a);
                Line(w.Content, (def?.Name ?? a.DefinitionId) + " · " + GameSession.CityPhaseText(a.State), 22, Ui.Accent);
                Line(w.Content, "地点：" + (S.City.HasPlace(a.VenueId) ? S.City.GetPlace(a.VenueId).Name : a.VenueId) + "\n预约：" + CityTime(a.ScheduledStart), 18);
                if(a.HostId!="player"&&S.CityPerson(a.HostId)!=null)
                    Line(w.Content,"邀请人："+S.CityPerson(a.HostId).Name,18,Ui.Accent);
                foreach (var p in a.Participants) Line(w.Content, (S.CityPerson(p.PersonId)?.Name ?? "同行者") + " · " + CityParticipantText(p.Phase), 18);
                Para(w.Content, $"阶段 {a.Stage + 1}/3 · 活动费用 {(a.EntryPaid ? "已支付" : "尚未支付")} · 完成奖励 {(a.RewardApplied ? "已结算" : "尚未结算")}", 18);
                if (a.State == CityActivityPhase.Running)
                {
                    long remaining = Math.Max(0, a.ActualStart + a.Duration - S.Clock.Now.TotalMinutes);
                    Line(w.Content, "预计剩余 " + remaining + " 游戏分钟；世界时间按原倍速推进。", 19);
                    if (a.ServiceConfirmed) Line(w.Content, "已选项目 · " + GameSession.VenueServiceName(def, a.ServiceChoice), 18, Ui.Good);
                    Para(w.Content, a.Stage == 0 ? def?.StartText : S.CityActivityMidText(a), 19);
                    Ui.Size(Ui.Button(w.Content, "打开场所菜单", () => GoToScheduledCityActivity(activityId), 18, Ui.ButtonHot,
                        CityActivityMenuOrTravelBlocker(activityId) == null), -1, 44);
                    if (def != null) for (int i = a.ServiceConfirmed ? def.ServiceCount : 0; i < def.Choices.Length; i++)
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
            string title = a.ServiceConfirmed ? GameSession.VenueServiceName(def, a.ServiceChoice) : def?.Name ?? a.DefinitionId;
            _cityActivityHudText.text = title + " · " + phase + " · " + time + "\n" + (a.State == CityActivityPhase.Running ? a.Stage == 0 ? def?.StartText : S.CityActivityMidText(a) : here ? "已经到场。在菜单里选择服务，与同行者一起开始活动。" : travelling ? "正在沿路线前往约定场所；可收起提醒，导航会继续。" : "赴约时间临近；空闲时点击“前往赴约”出发。");
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
