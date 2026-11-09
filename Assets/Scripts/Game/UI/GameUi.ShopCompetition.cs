using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _competitionPreviewKind = RetailShopTypes.Default;
        private string CompetitionPreviewKind(Place place) => S.ShopAt(place)?.RetailKind
            ?? (S.IsPendingShop(place) ? S.PendingRetailKind : _competitionPreviewKind);

        private void ChooseCompetitionKind()
        {
            OpenWindow("商圈试算店型", 620, 600, w =>
            {
                foreach (string id in RetailShopTypes.All)
                {
                    string kind = id;
                    Ui.Size(Ui.Button(w.Content, RetailShopTypes.Name(kind), () => { _competitionPreviewKind = kind; w.Close(); RebuildWindows(); }, 18), -1, 42);
                }
            });
        }

        private string CompetitionSummary(Place place)
        {
            string kind = CompetitionPreviewKind(place);
            int count = S.ShopCompetitionCount(place, kind);
            return $"{GameSession.ShopCompetitionRadiusMeters:0}米内同类自营店 {count} 家（含本店） · 客流保留 1/{count}"
                + $"\n分流后预计每日入店约 {S.ExpectedDailyShopEntrants(place, kind):N0} 人";
        }

        private void BuildCompetitionEntry(Transform parent)
        {
            if (S.IsDedicatedVenue(S.PlayerShopPlace)) return;
            var place = S.PlayerShopPlace;
            Ui.Size(Ui.Button(parent, "查看商圈 · " + S.ShopCompetitionCount(place) + " 家同类店分流", () => OpenShopCompetition(place), 16), -1, 34).name = "ShopCompetitionEntry";
        }

        private void AddCompetitionRing(RectTransform overlay, AgencyStreetMap view, Place place)
        {
            if (place == null || place.Kind != PlaceKind.Shop || S.IsDedicatedVenue(place)) return;
            var door = S.DoorOf(place); float radius = (float)S.ShopCompetitionRadiusWorld;
            var points = new Vector2[96];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                points[i] = view.Project(new Vector3((float)door.X + Mathf.Cos(angle) * radius, .2f, (float)door.Y + Mathf.Sin(angle) * radius));
            }
            var ring = Ui.Rect(overlay, "ShopCompetitionRadius").Fill();
            ring.gameObject.AddComponent<CompetitionRangeGraphic>().Initialize(points);
        }

        public void OpenShopCompetition(Place place) => OpenShopCompetition(place, agencyDetails: false);

        private void OpenShopCompetition(Place place, bool agencyDetails)
        {
            OpenWindow("店铺商圈 · " + S.PlaceTitle(place), 1180, 880, w =>
            {
                Para(w.Content, CompetitionSummary(place), 19, Ui.Accent).name = "ShopCompetitionSummary";
                if (agencyDetails) BuildAgencyShopNotes(w.Content, place);
                if (!S.IsMyShop(place) && !S.IsPendingShop(place)) Ui.Size(Ui.Button(w.Content, "试算店型：" + RetailShopTypes.Name(CompetitionPreviewKind(place)) + " · 点击更换", ChooseCompetitionKind, 17), -1, 36);
                HelpLink(w.Content,"商圈规则","橙圈半径40米，每店统计自己圈内的同类自营店，含本店共N家时客流保留1/N。不同店型、空铺、待交付店不计入；临时关门仍计入，退租或转型后重算。整日预估会随营业时间、知名度、品牌口碑、街道人流和同类店数量变化。");
                var others = S.NearbyCompetingShops(place, CompetitionPreviewKind(place));
                Para(w.Content, others.Count == 0 ? "圈内暂无其他同类自营店。" : "圈内同类店：" + string.Join("、", others.Select(S.ShopTitle)), 16);
                if (_ctrl.World != null)
                {
                    var holder = Ui.Rect(w.Content, "CompetitionStreetFrame"); Ui.Size(holder, -1, 560);
                    var raw = Ui.Rect(holder, "CompetitionStreetViewport");
                    raw.anchorMin = raw.anchorMax = raw.pivot = new Vector2(.5f, 1);
                    raw.sizeDelta = new Vector2(1120, 560);
                    raw.gameObject.AddComponent<RawImage>(); raw.gameObject.AddComponent<RectMask2D>();
                    var view = raw.gameObject.AddComponent<AgencyStreetMap>();
                    var state = new AgencyStreetMap.ViewState();
                    var center = S.DoorOf(place); double range = S.ShopCompetitionRadiusWorld * 1.6;
                    var units = S.City.Places.Where(p => { var d = S.DoorOf(p); return System.Math.Abs(d.X-center.X) <= range && System.Math.Abs(d.Y-center.Y) <= range; }).ToArray();
                    view.Initialize(_ctrl, state, units, 1120, 560);
                    view.FrameArea(new Vector3((float)center.X, .2f, (float)center.Y), (float)S.ShopCompetitionRadiusWorld);
                    var overlay = Ui.Rect(raw, "CompetitionAnnotations").Fill();
                    view.Changed = () => { Ui.Clear(overlay); AddCompetitionRing(overlay, view, place); AddCompetitionMarkers(overlay, view, place); };
                    view.Changed();
                }
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 18), -1, 40);
            });
        }

        private void AddCompetitionMarkers(RectTransform overlay, AgencyStreetMap view, Place place)
        {
            var occupied = new List<Rect>();
            var competitors = new HashSet<string>(S.NearbyCompetingShops(place, CompetitionPreviewKind(place)).Select(s => s.Place.Id));
            void Mark(Place target, string name, string kind, bool selected)
            {
                if (!_ctrl.World.Places.TryGetValue(target.Id, out var building)) return;
                var bounds = AgencyStreetMap.BuildingBounds(building);
                var point = view.Project(new Vector3(bounds.center.x, bounds.max.y + 1, bounds.center.z));
                if (point.x < 0 || point.y < 0 || point.x > view.Width || point.y > view.Height) return;
                bool competing = competitors.Contains(target.Id);
                var color = selected ? Ui.Accent : competing ? Ui.Warn : Ui.Dim;
                if (selected || competing)
                {
                    var glow = Ui.Rect(overlay, "CompetitionShop_" + target.Id); AgencyAt(glow, 0, 0, view.Width, view.Height);
                    glow.SetAsFirstSibling();
                    glow.gameObject.AddComponent<CanvasRenderer>();
                    glow.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(AgencyStreetMap.Corners(bounds).Select(view.Project).ToArray(), color, selected);
                }
                Rect box = AgencyNameBox(point, 210, occupied, view.Width, view.Height, 44); occupied.Add(box);
                var panel = AgencyBlock(overlay, "CompetitionName_" + target.Id, selected ? Ui.Accent : new Color(.08f,.16f,.14f,.95f), box.x, box.y, box.width, box.height);
                var label = Ui.Label(panel.transform, name + "\n" + RetailShopTypes.Name(kind) + (competing ? " · 参与分流" : ""), 14, TextAnchor.MiddleCenter, selected ? AgencyInk : Color.white);
                label.rectTransform.Fill(2); label.raycastTarget = false;
            }
            Mark(place, S.IsMyShop(place) ? "本店" : "拟开店铺", CompetitionPreviewKind(place), true);
            foreach (var shop in S.MyShops) if (shop.Place != place) Mark(shop.Place, S.ShopTitle(shop), shop.RetailKind, false);
        }
    }
}
