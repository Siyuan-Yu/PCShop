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
            AgencyMapKey(map, 120, AgencyStreetMap.RentalAvailable, "绿光：现在可租");
            AgencyMapKey(map, 340, AgencyStreetMap.RentalUpcoming, "橙光：之后可租");
            AgencyMapKey(map, 590, Ui.Accent, "金线框：当前选中");
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
            // Draw availability on the UI layer so the glow remains prominent at every map zoom.
            foreach (var place in units)
            {
                var listing = AgencyListing(place);
                if (listing == null || S.IsMyShop(place) || S.IsPendingShop(place)
                    || S.VentureAt(place.Id) != null || S.PropertyAt(place.Id) != null) continue;
                if (!_ctrl.World.Places.TryGetValue(place.Id, out var building)) continue;
                var body = building.Root.transform.Find("Body")?.GetComponent<Renderer>();
                if (body == null) continue;
                var rect = Ui.Rect(overlay, "RentalGlow_" + place.Id); AgencyAt(rect, 0, 0, view.Width, view.Height);
                rect.gameObject.AddComponent<CanvasRenderer>();
                rect.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(AgencyStreetMap.Corners(body.bounds).Select(view.Project).ToArray(),
                    listing.OpenDay > S.Clock.Today.TotalDays ? AgencyStreetMap.RentalUpcoming : AgencyStreetMap.RentalAvailable);
            }
            if (_agencySelectedId != null && _ctrl.World.Places.TryGetValue(_agencySelectedId, out var selected))
            {
                var body = selected.Root.transform.Find("Body")?.GetComponent<Renderer>();
                if (body != null)
                {
                    var corners = AgencyStreetMap.Corners(body.bounds).Select(view.Project).ToArray();
                    for (int i = 0; i < 4; i++)
                    {
                        AgencyMapLine(overlay, corners[i], corners[(i + 1) % 4], Ui.Accent);
                        AgencyMapLine(overlay, corners[i + 4], corners[(i + 1) % 4 + 4], Ui.Accent);
                        AgencyMapLine(overlay, corners[i], corners[i + 4], Ui.Accent);
                    }
                }
            }
            var occupied = new List<Rect>();
            foreach (var place in units.OrderBy(p => S.City.ViewPosition(p.NodeId).X))
            {
                if (!_ctrl.World.Places.TryGetValue(place.Id, out var building)) continue;
                var body = building.Root.transform.Find("Body")?.GetComponent<Renderer>();
                if (body == null) continue;
                Vector2 roof = view.Project(new Vector3(body.bounds.center.x, body.bounds.max.y + .6f, body.bounds.center.z));
                if (roof.x < 0 || roof.x > view.Width || roof.y < 0 || roof.y > view.Height) continue;
                string name = S.IsMyShop(place) ? S.PlaceTitle(place) : place.Name;
                float width = Mathf.Clamp(name.Length * 13 + 18, 70, 170);
                Rect box = AgencyNameBox(roof, width, occupied, view.Width, view.Height); occupied.Add(box);
                var button = Ui.Button(overlay, name, () => { _agencySelectedId = place.Id; w.Rebuild(); }, 13, new Color(.1f, .18f, .14f, .94f));
                button.name = "AgencyName_" + place.Id;
                AgencyAt((RectTransform)button.transform, box.x, box.y, box.width, box.height);
                button.GetComponentInChildren<Text>().rectTransform.Fill(2);
            }
        }

        private static Rect AgencyNameBox(Vector2 roof, float width, List<Rect> occupied, float mapWidth, float mapHeight)
        {
            Rect best = default; float bestScore = float.MaxValue;
            for (int row = 0; row < 6; row++)
            foreach (int offset in new[] { 0, -28, 28 })
            {
                var box = new Rect(Mathf.Clamp(roof.x - width / 2 + offset, 3, mapWidth - width - 3),
                    Mathf.Clamp(roof.y - 26 - row * 25, 3, mapHeight - 25), width, 22);
                float score = occupied.Count(other => new Rect(other.x - 3, other.y - 2, other.width + 6, other.height + 4).Overlaps(box)) * 100000
                    + (box.center - roof).sqrMagnitude;
                if (score < bestScore) { best = box; bestScore = score; }
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
