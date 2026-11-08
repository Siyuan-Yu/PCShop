using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private readonly Dictionary<string, (Vector2 from, Vector2 to, float height, float width, double strength, string street)[]> _agencyFlows =
            new Dictionary<string, (Vector2, Vector2, float, float, double, string)[]>();
        private bool _showAgencyFlow = true;

        private void BuildAgencyMap(RectTransform map, Place[] units, Window w, Place agency)
        {
            AgencyMapText(map, S.City.AgencyCoverageName(agency) + " · 商铺集中区", 23, 16, 6, 280, 34, Ui.TextColor);
            AgencyMapControl(map,_showAgencyFlow?"✓ 街道人流":"街道人流","AgencyTrafficToggle",300,118,()=>
            {
                _showAgencyFlow = !_showAgencyFlow;
                w.Rebuild();
            });
            if (_showAgencyFlow)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = 425 + i * 40;
                    AgencyBlock(map,"StreetFlowKey",AgencyStreetMap.PedestrianBandColor(i),x,18,17,10);
                    AgencyMapText(map,i==0?"少":i==1?"中":"多",14,x+21,8,18,30,Ui.TextColor);
                }
            }
            var raw = Ui.Rect(map, "StreetViewport");
            AgencyAt(raw, 0, 46, 1120, 650);
            raw.gameObject.AddComponent<RawImage>();
            raw.gameObject.AddComponent<RectMask2D>();
            var view = raw.gameObject.AddComponent<AgencyStreetMap>();
            view.Initialize(_ctrl, _agencyViewState, units, 1120, 650,
                pedestrianBands: _showAgencyFlow ? AgencyPedestrianBands(units,agency) : null);
            var overlay = Ui.Rect(raw, "MapAnnotations").Fill();
            var zoom = Ui.Label(map, "", 16, TextAnchor.MiddleCenter, Ui.Dim);
            AgencyAt(zoom.rectTransform, 664, 8, 62, 30);
            view.Selected = id =>
            {
                if (!view.CanSelect(id)) return;
                _agencySelectedId = id;
                w.Rebuild();
            };
            view.Changed = () =>
            {
                zoom.text = $"{view.Zoom:0.0}×";
                BuildAgencyMapAnnotations(overlay, view, units, w, agency);
            };
            AgencyMapControl(map,"定位中介","AgencyFocusCurrent",548,108,()=>view.Focus(agency));
            AgencyMapControl(map, "−", "AgencyZoomOut", 738, 44, () => view.ZoomBy(1 / 1.35f));
            AgencyMapControl(map, "+", "AgencyZoomIn", 788, 44, () => view.ZoomBy(1.35f));
            AgencyMapControl(map, "本片区", "AgencyResetView", 842, 80, view.ResetView);
            AgencyMapControl(map, "放大这间", "AgencyFocusUnit", 932, 172,
                () => { Place p = units.FirstOrDefault(u => u.Id == _agencySelectedId); if (p != null) view.Focus(p); });
            view.Changed();
            AgencyMapText(map, "房源标记", 14, 16, 703, 90, 24, Ui.Dim);
            AgencyMapKey(map, 120, AgencyStreetMap.RentalAvailable, "绿光：现在可租");
            AgencyMapKey(map, 340, AgencyStreetMap.RentalUpcoming, "橙光：之后可租");
            AgencyMapKey(map, 590, Ui.Accent, "金色：已选中房源");
            AgencyMapKey(map, 814, new Color32(66,158,255,255), "蓝色：当前中介 / 你在这里");
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

        private void BuildAgencyMapAnnotations(RectTransform overlay, AgencyStreetMap view, Place[] units, Window w, Place agency)
        {
            Ui.Clear(overlay);
            if (_agencySelectedId != null && S.City.HasPlace(_agencySelectedId))
                AddCompetitionRing(overlay, view, S.City.GetPlace(_agencySelectedId));
            void Glow(string id,PlaceVisual building,Color color,bool emphasized=false)
            {
                var rect=Ui.Rect(overlay,id);AgencyAt(rect,0,0,view.Width,view.Height);
                rect.gameObject.AddComponent<CanvasRenderer>();
                rect.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(
                    AgencyStreetMap.Corners(AgencyStreetMap.BuildingBounds(building)).Select(view.Project).ToArray(),color,emphasized);
            }
            foreach(var place in units)
            {
                var listing=AgencyListing(place);
                if(listing==null||S.IsMyShop(place)||S.IsPendingShop(place)||S.VentureAt(place.Id)!=null||S.PropertyAt(place.Id)!=null)continue;
                if(!_ctrl.World.Places.TryGetValue(place.Id,out var building))continue;
                Glow("RentalGlow_"+place.Id,building,listing.OpenDay>S.Clock.Today.TotalDays?AgencyStreetMap.RentalUpcoming:AgencyStreetMap.RentalAvailable);
            }
            var blue=new Color32(66,158,255,255);
            if(_ctrl.World.Places.TryGetValue(agency.Id,out var agentBuilding))Glow("CurrentAgencyGlow",agentBuilding,blue);
            if(_agencySelectedId!=null&&_ctrl.World.Places.TryGetValue(_agencySelectedId,out var selected))
                Glow("SelectedBuildingGlow",selected,Ui.Accent,true);
            var occupied=new List<Rect>();
            void Badge(PlaceVisual building,string name,string text,Color color,Color ink,float width)
            {
                var bounds=AgencyStreetMap.BuildingBounds(building);
                var roof=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                bool outside=roof.x<0||roof.x>view.Width||roof.y<0||roof.y>view.Height;
                var box=AgencyNameBox(roof,width,occupied,view.Width,view.Height,50);occupied.Add(box);
                var anchor=view.Project(building.Entrance+Vector3.up*.25f);
                AgencyMapLine(overlay,new Vector2(box.center.x,box.yMax),anchor,color);
                var pin=AgencyBlock(overlay,name+"Pin",color,anchor.x-7,anchor.y-7,14,14);pin.sprite=CircleSprite();pin.raycastTarget=false;
                var panel=AgencyBlock(overlay,name,color,box.x,box.y,box.width,box.height);panel.raycastTarget=false;
                var label=Ui.Label(panel.transform,text+(outside?"（画面外）":""),15,TextAnchor.MiddleCenter,ink);
                label.rectTransform.Fill(4);label.raycastTarget=false;
            }
            if(agentBuilding!=null)
                Badge(agentBuilding,"CurrentAgencyMarker","<b>● 当前中介 · 你在这里</b>\n"+agency.Name,blue,Color.white,286);
            if(_agencySelectedId!=null&&_ctrl.World.Places.TryGetValue(_agencySelectedId,out var chosen)&&chosen.Place.Kind!=PlaceKind.Agency)
                Badge(chosen,"SelectedPropertyMarker","<b>✓ 已选中房源</b>\n"+(S.IsMyShop(chosen.Place)?S.PlaceTitle(chosen.Place):chosen.Place.Name),Ui.Accent,AgencyInk,300);
            foreach(var place in units.OrderBy(p=>S.City.ViewPosition(p.NodeId).X))
            {
                if (!view.CanSelect(place.Id) || place.Id == agency.Id || place.Id == _agencySelectedId) continue;
                if(view.Zoom<1.6f&&!S.IsMyShop(place)&&AgencyListing(place)==null)continue;
                if(!_ctrl.World.Places.TryGetValue(place.Id,out var building))continue;
                var bounds=AgencyStreetMap.BuildingBounds(building);
                Vector2 roof=view.Project(new Vector3(bounds.center.x,bounds.max.y+.6f,bounds.center.z));
                if(roof.x<0||roof.x>view.Width||roof.y<0||roof.y>view.Height)continue;
                string name=S.IsMyShop(place)?S.PlaceTitle(place):place.Name;
                float width=Mathf.Clamp(name.Length*13+18,70,170);
                Rect box=AgencyNameBox(roof,width,occupied,view.Width,view.Height);occupied.Add(box);
                var button=Ui.Button(overlay,name,()=>{_agencySelectedId=place.Id;w.Rebuild();},13,new Color(.1f,.18f,.14f,.94f));
                button.name="AgencyName_"+place.Id;
                AgencyAt((RectTransform)button.transform,box.x,box.y,box.width,box.height);
                button.GetComponentInChildren<Text>().rectTransform.Fill(2);
            }
        }

        private AgencyStreetMap.PedestrianBand[] AgencyTrafficBands((Vector2 from, Vector2 to, float height, float width, double strength, string street)[] bands)
        {
            var result = new AgencyStreetMap.PedestrianBand[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                double strength = band.strength * System.Math.Sqrt(S.StreetTrafficMultiplier(band.street));
                int level = strength >= .67 ? 2 : strength >= .34 ? 1 : 0;
                result[i] = new AgencyStreetMap.PedestrianBand(band.from, band.to, band.height, band.width, level);
            }
            return result;
        }

        private AgencyStreetMap.PedestrianBand[] AgencyPedestrianBands(Place[] units, Place agency)
        {
            if (_agencyFlows.TryGetValue(agency.Id,out var cached)) return AgencyTrafficBands(cached);
            var city = S.City;
            var map = city.Map;
            var paths = new Dictionary<(string from,string to),double>();
            var homes = city.OfKind(PlaceKind.Home).GroupBy(p=>p.NodeId)
                .Select(g=>(node:g.Key,weight:System.Math.Min(3,g.Count()))).ToArray();
            var stations = city.OfKind(PlaceKind.Station).Select(p=>p.NodeId).Distinct().ToArray();
            // Estimate walking demand from trips between homes, stations and local destinations.
            var destinations = units.Where(p=>p.Kind==PlaceKind.Shop || p.Kind==PlaceKind.Wholesaler || p.Kind==PlaceKind.Agency)
                .GroupBy(p=>p.StreetId)
                .SelectMany(group=>
                {
                    var nodes = group.Select(p=>p.NodeId).Distinct().OrderBy(id=>city.ViewPosition(id).X)
                        .ThenBy(id=>city.ViewPosition(id).Y).ToArray();
                    int stride = Mathf.Max(1,Mathf.CeilToInt(nodes.Length/6f));
                    return nodes.Where((id,index)=>index%stride==0);
                }).Distinct();
            foreach (string destination in destinations)
            {
                foreach (var home in homes.Where(h=>h.node!=destination).OrderBy(h=>map.StraightDistance(h.node,destination)).Take(3))
                    AddRoute(home.node,destination,home.weight);
                foreach (string station in stations.Where(id=>id!=destination).OrderBy(id=>map.StraightDistance(id,destination)).Take(1))
                    AddRoute(station,destination,3);
            }
            void AddRoute(string origin,string destination,int weight)
            {
                if (map.StraightDistance(origin,destination)>700) return;
                var path = Pathfinder.FindPath(map,origin,destination);
                if (!path.Found || path.Distance>700) return;
                double contribution = weight/(1+path.Distance/100);
                for (int i=1; i<path.NodeIds.Count; i++)
                {
                    string a = path.NodeIds[i-1], b = path.NodeIds[i];
                    var segment = string.CompareOrdinal(a,b)<0 ? (a,b) : (b,a);
                    paths.TryGetValue(segment,out double previous);
                    paths[segment] = previous+contribution;
                }
            }

            var local = units.Select(p=>p.NodeId).Distinct().Select(Point).ToArray();
            var localStreets = new HashSet<string>(units.Select(p => p.StreetId));
            double peak = paths.Values.DefaultIfEmpty(1).Max();
            var bands = new List<(Vector2 from, Vector2 to, float height, float width, double strength, string street)>();
            foreach (var node in map.Nodes)
            foreach (var edge in map.NeighborsOf(node.Id))
            {
                string from = node.Id, to = edge.To;
                if (string.CompareOrdinal(from,to)>=0) continue;
                paths.TryGetValue((from,to),out double flow);
                Vector2 a = Point(from), b = Point(to), delta = b-a;
                if (delta.sqrMagnitude < 1) continue;
                string street = S.TrafficStreetOfSegment(from,to);
                if (flow == 0 && !localStreets.Contains(street) && !local.Any(p=>NearSegment(p,a,delta))) continue;
                double strength = System.Math.Sqrt(flow/peak);
                bool raised = city.IsLakeCity && (StreetWorldBuilder.IsLakeAccessNode(from) || StreetWorldBuilder.IsLakeAccessNode(to));
                float height = raised ? StreetWorldBuilder.LakeAccessHeight(city) + .008f : city.IsLakeCity ? .032f : .055f;
                bands.Add((a,b,height,RoadWidth(a,b),strength,street));
            }
            cached = bands.ToArray();
            _agencyFlows.Add(agency.Id,cached);
            return AgencyTrafficBands(cached);

            Vector2 Point(string id)
            {
                MapPoint p = city.ViewPosition(id);
                return new Vector2((float)p.X,(float)p.Y);
            }
            float RoadWidth(Vector2 a,Vector2 b)
            {
                var from = new MapPoint(a.x,a.y);
                var to = new MapPoint(b.x,b.y);
                return city.LayoutVersion>=7 ? (float)GameSession.CityRoadWidth(from,to)
                    : GameSession.IsShoppingStreet(from,to) ? 14 : GameSession.IsMainRoad(from,to) ? 7 : 5;
            }
        }

        private static bool NearSegment(Vector2 p,Vector2 a,Vector2 delta)
        {
            float t = Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            return (p-a-delta*t).sqrMagnitude<40*40;
        }

        private static Rect AgencyNameBox(Vector2 roof, float width, List<Rect> occupied, float mapWidth, float mapHeight, float height=22)
        {
            Rect best = default; float bestScore = float.MaxValue;
            for (int row = 0; row < 6; row++)
            foreach (int offset in new[] { 0, -28, 28 })
            {
                var box = new Rect(Mathf.Clamp(roof.x - width / 2 + offset, 3, mapWidth - width - 3),
                    Mathf.Clamp(roof.y - height - 4 - row * (height+3), 3, mapHeight - height - 3), width, height);
                float score = occupied.Count(other => new Rect(other.x - 3, other.y - 2, other.width + 6, other.height + 4).Overlaps(box)) * 100000
                    + (box.center - roof).sqrMagnitude;
                if (score < bestScore) { best = box; bestScore = score; }
            }
            return best;
        }

        private static void BuildAgencyFloorPlan(Transform parent, RoomSpec room)
        {
            var paper = Ui.Panel(parent, "AgencyFloorPlan", AgencyPaper).rectTransform;
            Ui.Size(paper, -1, 178);
            // Fixed drawing space is centered inside the responsive detail column.
            var drawing = Ui.Rect(paper, "PlanDrawing");
            drawing.anchorMin = drawing.anchorMax = new Vector2(.5f, 1);
            drawing.pivot = new Vector2(.5f, 1);
            drawing.sizeDelta = new Vector2(430, 178);
            AgencyMapText(drawing, $"室内平面  {room.Width:0.#} × {room.Depth:0.#} 米", 14, 12, 0, 405, 24, AgencyInk);
            const float scale = 10;
            float width = (float)room.Width * scale, height = (float)room.Depth * scale;
            float x = (430 - width) / 2, y = 28 + (120 - height) / 2;
            AgencyBlock(drawing, "Walls", AgencyInk, x - 3, y - 3, width + 6, height + 6);
            AgencyBlock(drawing, "Floor", new Color32(247, 242, 226, 255), x, y, width, height);
            for (int i = 1; i < room.Width; i++) AgencyBlock(drawing, "Grid", new Color(0, .15f, .1f, .08f), x + i * scale, y, 1, height);
            for (int i = 1; i < room.Depth; i++) AgencyBlock(drawing, "Grid", new Color(0, .15f, .1f, .08f), x, y + i * scale, width, 1);
            float doorX = x + (float)room.DoorClear.MinX * scale;
            float doorWidth = (float)(room.DoorClear.MaxX - room.DoorClear.MinX) * scale;
            AgencyBlock(drawing, "DoorClearance", Ui.Accent, doorX, y + height - (float)room.DoorClear.MaxZ * scale,
                doorWidth, (float)(room.DoorClear.MaxZ-room.DoorClear.MinZ) * scale);
            if (room.IsShop)
                AgencyBlock(drawing, "Door", AgencyFuture, doorX, y + height, doorWidth, 4);
            else
            {
                AgencyBlock(drawing,"Door",AgencyFuture,x+width,y+height-(float)room.DoorMaxZ*scale,4,(float)(room.DoorMaxZ-room.DoorMinZ)*scale);
                AgencyBlock(drawing,"Window",new Color32(83,147,170,255),
                    x+(float)room.WindowMinX*scale,y-3,(float)(room.WindowMaxX-room.WindowMinX)*scale,4);
            }
            AgencyMapText(drawing, $"{room.Width*room.Depth:0.#}㎡", 17, x, y, width, height, AgencyInk, TextAnchor.MiddleCenter);
            string caption = room.IsShop ? "临街入口 · 黄色区域留通道" : "右侧入口 · 黄色区域留通道 · 蓝色为窗";
            AgencyMapText(drawing, caption, 13, 12, 153, 405, 20, AgencyInk, TextAnchor.MiddleCenter);
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
