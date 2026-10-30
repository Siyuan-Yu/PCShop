using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class AgencyMapLabel
        {
            public Place Place;
            public ShopListing Listing;
            public Vector2 Anchor;
            public Rect Box;
            public string Text;
            public int Number;
        }

        private void BuildAgencyMap(RectTransform map, Place[] units, Window w)
        {
            AgencyMapText(map, "商业街 · 街区鸟瞰", 23, 16, 6, 280, 34, Ui.TextColor);
            AgencyMapText(map, "滚轮缩放 · 拖动查看 · 点建筑选址", 15, 300, 9, 350, 30, Ui.Dim);
            var raw = Ui.Rect(map, "StreetViewport");
            AgencyAt(raw, 0, 46, 1120, 650);
            raw.gameObject.AddComponent<RawImage>();
            raw.gameObject.AddComponent<RectMask2D>();
            var view = raw.gameObject.AddComponent<AgencyStreetMap>();
            view.Initialize(_ctrl, _agencyViewState, units, 1120, 650);
            var overlay = Ui.Rect(raw, "MapAnnotations").Fill();
            var zoom = Ui.Label(map, "", 16, TextAnchor.MiddleCenter, Ui.Dim);
            AgencyAt(zoom.rectTransform, 664, 8, 62, 30);
            view.Selected = id =>
            {
                if (!units.Any(p => p.Id == id)) return;
                _agencySelectedId = id;
                w.Rebuild();
            };
            view.Changed = () =>
            {
                zoom.text = $"{view.Zoom:0.0}×";
                BuildAgencyMapAnnotations(overlay, view, units, w);
            };
            AgencyMapControl(map, "−", "AgencyZoomOut", 738, 44, () => view.ZoomBy(1 / 1.35f));
            AgencyMapControl(map, "+", "AgencyZoomIn", 788, 44, () => view.ZoomBy(1.35f));
            AgencyMapControl(map, "全街", "AgencyResetView", 842, 80, view.ResetView);
            AgencyMapControl(map, "放大这间", "AgencyFocusUnit", 932, 172,
                () => { Place p = units.FirstOrDefault(u => u.Id == _agencySelectedId); if (p != null) view.Focus(p); });
            view.Changed();
            AgencyMapText(map, "房源标记", 14, 16, 703, 90, 24, Ui.Dim);
            AgencyMapKey(map, 120, Ui.ButtonHot, "正在出租");
            AgencyMapKey(map, 290, AgencyFuture, "到期后可租");
            AgencyMapKey(map, 480, Ui.ButtonBg, "未挂牌");
            AgencyMapKey(map, 630, new Color32(81,111,126,255), "我的店 / 中介");
            AgencyMapKey(map, 850, Ui.Accent, "金线框为选中建筑");
        }

        private static void AgencyMapControl(Transform parent, string label, string name, float x, float width, System.Action action)
        {
            var button = Ui.Button(parent, label, action, 17);
            button.name = name;
            AgencyAt((RectTransform)button.transform, x, 7, width, 32);
        }

        private static void AgencyMapKey(Transform parent, float x, Color color, string text)
        {
            AgencyBlock(parent, "Legend", color, x, 709, 12, 12);
            AgencyMapText(parent, text, 14, x + 19, 702, 210, 26, Ui.TextColor);
        }

        private void BuildAgencyMapAnnotations(RectTransform overlay, AgencyStreetMap view, Place[] units, Window w)
        {
            Ui.Clear(overlay);
            var labels = new List<AgencyMapLabel>();
            var occupied = new List<Rect>();
            foreach (Place p in units)
            {
                if (!_ctrl.World.Places.TryGetValue(p.Id, out var visual)) continue;
                var body = visual.Root.transform.Find("Body")?.GetComponent<Renderer>();
                if (body == null) continue;
                Vector2[] corners = AgencyStreetMap.Corners(body.bounds).Select(view.Project).ToArray();
                occupied.Add(Rect.MinMaxRect(corners.Min(v => v.x) - 3, corners.Min(v => v.y) - 3,
                    corners.Max(v => v.x) + 3, corners.Max(v => v.y) + 3));
            }
            // Quotes get space first; other shop names are attached to their actual roofs.
            foreach (Place p in units.OrderBy(p => AgencyListing(p) != null ? 0 : p.Kind == PlaceKind.Agency ? 1 : 2)
                .ThenBy(p => S.City.ViewPosition(p.NodeId).X))
            {
                if (!_ctrl.World.Places.TryGetValue(p.Id, out var visual)) continue;
                var body = visual.Root.transform.Find("Body")?.GetComponent<Renderer>();
                Vector3 roof = body != null ? new Vector3(body.bounds.center.x, body.bounds.max.y + .5f, body.bounds.center.z) : visual.LabelPos;
                Vector2 anchor = view.Project(roof);
                if (anchor.x < 4 || anchor.x > view.Width - 4 || anchor.y < 4 || anchor.y > view.Height - 4) continue;
                ShopListing listing = AgencyListing(p);
                string name = p.Kind == PlaceKind.Agency ? "安居中介 · 你在这里" : S.IsMyShop(p) ? (S.ShopAt(p) == S.MainShop ? "我的主店" : S.PlaceTitle(p)) : p.Name;
                Vector2 size = listing != null ? new Vector2(144, 82) : new Vector2(Mathf.Clamp(name.Length * 14 + 20, 64, 174), 26);
                var label = new AgencyMapLabel { Place = p, Listing = listing, Anchor = anchor, Text = name, Number = System.Array.IndexOf(units, p) + 1 };
                label.Box = AgencyLabelBox(anchor, size, occupied, view.Width, view.Height);
                occupied.Add(label.Box);
                labels.Add(label);
                if (p.Id == _agencySelectedId && body != null)
                {
                    Vector2[] corners = AgencyStreetMap.Corners(body.bounds).Select(view.Project).ToArray();
                    for (int i = 0; i < 4; i++)
                    {
                        AgencyMapLine(overlay, corners[i], corners[(i + 1) % 4], Ui.Accent);
                        AgencyMapLine(overlay, corners[i + 4], corners[(i + 1) % 4 + 4], Ui.Accent);
                        AgencyMapLine(overlay, corners[i], corners[i + 4], Ui.Accent);
                    }
                }
            }
            Place metro = S.City.FirstOfKind(PlaceKind.Station);
            if (metro != null && _ctrl.World.Places.TryGetValue(metro.Id, out var station))
                AddAgencyLandmark(labels, occupied, view, view.Project(station.LabelPos), "M  商业街地铁站");
            Place dealer = S.City.FirstOfKind(PlaceKind.Dealer);
            if (dealer != null && _ctrl.World.Places.TryGetValue(dealer.Id, out var dealerView))
            {
                var roof = dealerView.Root.transform.Find("Body")?.GetComponent<Renderer>();
                AddAgencyLandmark(labels, occupied, view, view.Project(roof != null ? new Vector3(roof.bounds.center.x, roof.bounds.max.y + .5f, roof.bounds.center.z) : dealerView.LabelPos), dealer.Name);
            }
            AddAgencyLandmark(labels, occupied, view, view.Project(new Vector3(-25, 20, 32)), "沿街住宅");
            AddAgencyLandmark(labels, occupied, view, view.Project(new Vector3(105, 1, 0)), "城中村方向 →");

            foreach (var label in labels)
            {
                Rect box = label.Box;
                Vector2 attach = new Vector2(Mathf.Clamp(label.Anchor.x, box.xMin, box.xMax), Mathf.Clamp(label.Anchor.y, box.yMin, box.yMax));
                AgencyMapLine(overlay, label.Anchor, attach, label.Place?.Id == _agencySelectedId ? Ui.Accent : new Color(.95f,.93f,.82f,.7f));
            }
            foreach (var label in labels)
            {
                Rect box = label.Box;
                Place p = label.Place;
                if (p == null)
                {
                    AgencyBlock(overlay, "MapLandmark", new Color(.15f,.23f,.26f,.92f), box.x, box.y, box.width, box.height);
                    AgencyMapText(overlay, label.Text, 15, box.x + 5, box.y, box.width - 10, box.height, Ui.TextColor, TextAnchor.MiddleCenter);
                    continue;
                }
                ShopListing l = label.Listing;
                bool selected = p.Id == _agencySelectedId;
                if (selected) AgencyBlock(overlay, "SelectedQuote", Ui.Accent, box.x - 2, box.y - 2, box.width + 4, box.height + 4);
                Color color = l != null || S.IsMyShop(p) || p.Kind == PlaceKind.Agency ? AgencyColor(p, l) : new Color(.15f,.23f,.20f,.9f);
                var button = Ui.Button(overlay, l != null ? "" : label.Text, () => { _agencySelectedId = p.Id; w.Rebuild(); }, 14, color);
                button.name = (l != null ? "AgencyPin_" : "AgencyLot_") + p.Id;
                AgencyAt((RectTransform)button.transform, box.x, box.y, box.width, box.height);
                button.GetComponentInChildren<Text>().rectTransform.Fill(2);
                if (l != null)
                {
                    AgencyMapText(button.transform, $"{label.Number:00}  {label.Text}", 14, 4, 2, box.width - 8, 18, Ui.TextColor, TextAnchor.MiddleCenter);
                    AgencyMapText(button.transform, $"<b>{l.Rent}</b><size=13> /月</size>", 20, 4, 20, box.width - 8, 24, Ui.TextColor, TextAnchor.MiddleCenter);
                    AgencyMapText(button.transform, $"{RealEstate.SizeArea(p.Size)} · 客流 ×{S.LocationFactorOf(p):0.00}", 13, 4, 44, box.width - 8, 18, Ui.TextColor, TextAnchor.MiddleCenter);
                    AgencyMapText(button.transform, AgencyStatus(p, l), 13, 4, 62, box.width - 8, 18, Ui.TextColor, TextAnchor.MiddleCenter);
                    var dot = Ui.Button(overlay, "●", () => { _agencySelectedId = p.Id; w.Rebuild(); }, 19, color);
                    dot.name = "AgencyLot_" + p.Id;
                    AgencyAt((RectTransform)dot.transform, label.Anchor.x - 10, label.Anchor.y - 10, 20, 20);
                    dot.GetComponentInChildren<Text>().rectTransform.Fill();
                }
                else AgencyBlock(overlay, "RoofAnchor", selected ? Ui.Accent : Ui.TextColor, label.Anchor.x - 2, label.Anchor.y - 2, 4, 4);
            }
        }

        private static void AddAgencyLandmark(List<AgencyMapLabel> labels, List<Rect> occupied, AgencyStreetMap view, Vector2 point, string title)
        {
            if (point.x < 0 || point.x > view.Width || point.y < 0 || point.y > view.Height) return;
            Rect box = AgencyLabelBox(point, new Vector2(title.Length * 15 + 18, 28), occupied, view.Width, view.Height);
            labels.Add(new AgencyMapLabel { Anchor = point, Box = box, Text = title });
            occupied.Add(box);
        }

        private static Rect AgencyLabelBox(Vector2 anchor, Vector2 size, List<Rect> occupied, float width, float height)
        {
            Rect best = new Rect(); float bestScore = float.MaxValue;
            // Find a nearby free label location, not a list slot; leaders retain the geographic connection.
            for (int ring = 0; ring < 7; ring++)
            for (int side = 0; side < 8; side++)
            {
                float angle = side * Mathf.PI / 4;
                Vector2 center = anchor + new Vector2(Mathf.Cos(angle) * (size.x / 2 + 28 + ring * 32),
                    Mathf.Sin(angle) * (size.y / 2 + 28 + ring * 32));
                var rect = new Rect(Mathf.Clamp(center.x - size.x / 2, 6, width - size.x - 6),
                    Mathf.Clamp(center.y - size.y / 2, 6, height - size.y - 6), size.x, size.y);
                var padded = new Rect(rect.x - 5, rect.y - 5, rect.width + 10, rect.height + 10);
                float overlap = occupied.Count(r => padded.Overlaps(r));
                float score = overlap * 100000 + (rect.center - anchor).sqrMagnitude;
                if (score < bestScore) { best = rect; bestScore = score; }
            }
            return best;
        }

        private static void BuildAgencyFloorPlan(RectTransform parent, Place p)
        {
            RoomSpec room = RoomSpec.ForShop(p.Size);
            var paper = Ui.Panel(parent, "AgencyFloorPlan", AgencyPaper).rectTransform;
            Ui.Size(paper, -1, 150);
            // Fixed drawing space is centered inside the responsive detail column.
            var drawing = Ui.Rect(paper, "PlanDrawing");
            drawing.anchorMin = drawing.anchorMax = new Vector2(.5f, 1);
            drawing.pivot = new Vector2(.5f, 1);
            drawing.sizeDelta = new Vector2(430, 150);
            AgencyMapText(drawing, $"室内平面  {room.Width:0} × {room.Depth:0} 米", 14, 12, 0, 405, 24, AgencyInk);
            float scale = Mathf.Min(270 / (float)room.Width, 100 / (float)room.Depth);
            float width = (float)room.Width * scale, height = (float)room.Depth * scale;
            float x = (430 - width) / 2, y = 26;
            AgencyBlock(drawing, "Walls", AgencyInk, x - 3, y - 3, width + 6, height + 6);
            AgencyBlock(drawing, "Floor", new Color32(247, 242, 226, 255), x, y, width, height);
            for (int i = 1; i < room.Width; i++) AgencyBlock(drawing, "Grid", new Color(0, .15f, .1f, .08f), x + i * scale, y, 1, height);
            for (int i = 1; i < room.Depth; i++) AgencyBlock(drawing, "Grid", new Color(0, .15f, .1f, .08f), x, y + i * scale, width, 1);
            float doorX = x + (float)room.DoorClear.MinX * scale;
            float doorWidth = (float)(room.DoorClear.MaxX - room.DoorClear.MinX) * scale;
            AgencyBlock(drawing, "DoorClearance", Ui.Accent, doorX, y + height - (float)room.DoorClear.MaxZ * scale, doorWidth, (float)room.DoorClear.MaxZ * scale);
            AgencyBlock(drawing, "Door", AgencyFuture, doorX, y + height, doorWidth, 4);
            AgencyMapText(drawing, RealEstate.SizeArea(p.Size), 24, x, y + 12, width, height - 24, AgencyInk, TextAnchor.MiddleCenter);
            AgencyMapText(drawing, "临街入口 · 黄色区域留通道", 13, 12, 129, 405, 20, AgencyInk, TextAnchor.MiddleCenter);
        }

        private static void AgencyAt(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static Image AgencyBlock(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            Image img = Ui.Panel(parent, name, color);
            img.raycastTarget = false;
            AgencyAt(img.rectTransform, x, y, width, height);
            return img;
        }

        private static void AgencyMapText(Transform parent, string text, int size, float x, float y, float width, float height,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(parent, text, size, anchor, color);
            AgencyAt(label.rectTransform, x, y, width, height);
        }

        private static void AgencyMapLine(Transform parent, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            Image line = AgencyBlock(parent, "Leader", color, from.x, from.y, delta.magnitude, 1.5f);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
