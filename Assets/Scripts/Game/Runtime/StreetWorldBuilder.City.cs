using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class StreetWorldBuilder
    {
        public static Bounds BoundsOf(CityLayout city)
        {
            var points = city.Map.Nodes.Select(n => ToWorld(city.ViewPosition(n.Id))).ToArray();
            if (points.Length == 0) return new Bounds(Vector3.zero, new Vector3(100, 25, 100));
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            foreach (var p in city.Places.Where(p => p.ExplicitEntrance))
            {
                var node = ToWorld(city.ViewPosition(p.NodeId));
                bounds.Encapsulate(node + new Vector3((float)p.EntranceX, 15, (float)p.EntranceY));
            }
            bounds.Expand(new Vector3(70, 28, 70)); return bounds;
        }
        private void BuildCitySidewalks()
        {
            var walks=new GameObject("CitySidewalks").transform;walks.SetParent(_root,false);
            var seen = new HashSet<string>();
            foreach (var n in _city.Map.Nodes)
            foreach (var edge in _city.Map.NeighborsOf(n.Id))
            {
                string key = string.CompareOrdinal(n.Id, edge.To) < 0 ? n.Id + "|" + edge.To : edge.To + "|" + n.Id;
                if (!seen.Add(key)) continue;
                Vector3 a = NodeWorld(n.Id), b = NodeWorld(edge.To), delta = b - a;
                if (delta.sqrMagnitude < .01f || (_city.LayoutVersion<7 && Mathf.Abs(a.z) < .1f && Mathf.Abs(b.z) < .1f)) continue;
                float width = _city.LayoutVersion>=7?PlannedRoadWidth(a,b):GameSession.IsMainRoad(_city.ViewPosition(n.Id), _city.ViewPosition(edge.To)) ? 7 : 5;
                Vector3 right = Vector3.Cross(Vector3.up, delta.normalized);
                foreach (int side in new[] { -1, 1 })
                {
                    var walk = Prims.Box(walks, "CitySidewalk_" + key, (a + b) * .5f + right * side * (width / 2 + .7f) + Vector3.up * .06f,
                        new Vector3(1.4f, .12f, delta.magnitude + 1), Mats.Lit(Sidewalk));
                    walk.transform.localRotation = Quaternion.LookRotation(delta.normalized);
                }
            }
            if(_city.IsLakeCity)StaticBatchingUtility.Combine(walks.gameObject);
        }
        private void BuildCityPlace(Place p)
        {
            Vector3 node = NodeWorld(p.NodeId), entrance = node + new Vector3((float)p.EntranceX, 0, (float)p.EntranceY);
            var root = new GameObject("Place_" + p.Id).transform; root.SetParent(_root, false); root.position = entrance;
            float yaw = (float)p.Heading;
            if (Mathf.Abs(yaw) < .01f && (Math.Abs(p.EntranceX) > .1 || Math.Abs(p.EntranceY) > .1))
                yaw = Mathf.Atan2((float)p.EntranceX, (float)p.EntranceY) * Mathf.Rad2Deg;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var visual = new PlaceVisual { Place = p, Entrance = entrance, Root = root.gameObject };
            Places[p.Id] = visual; _cur = visual;
            float width = Mathf.Max(6, (float)p.Width), depth = Mathf.Max(6, (float)p.Depth);
            bool venue = (p.Business ?? "").StartsWith("venue_", StringComparison.Ordinal);
            if (venue || p.Business=="服装") CityVenueView.Build(root, p, width, depth);
            else
            {
                float height = p.Kind == PlaceKind.Home ? 7 : 4.2f;
                var wall = Facades[Math.Abs(StableCityHash(p.Id)) % Facades.Length];
                var body = Prims.Box(root, "Body", new Vector3(0, height / 2, depth / 2), new Vector3(width, height, depth), Mats.Lit(wall), true);
                body.AddComponent<PlaceMarker>().PlaceId = p.Id;
                Prims.Box(root, "EntranceDoor", new Vector3(0, 1.1f, -.07f), new Vector3(1.7f, 2.2f, .12f), Mats.Lit(ArtDirection.Ink));
                Prims.Box(root, "DoorFrame", new Vector3(0, 2.3f, -.1f), new Vector3(2, .15f, .2f), Mats.Lit(ArtDirection.Brass));
                for (int side = -1; side <= 1; side += 2)
                    Prims.Box(root, "DisplayWindow", new Vector3(side * width * .3f, 1.6f, -.12f), new Vector3(width * .25f, 1.7f, .1f), Mats.Lit(new Color(.32f, .45f, .47f), .7f));
                Prims.Box(root, "Fascia", new Vector3(0, 3.3f, -.17f), new Vector3(width - .4f, .9f, .16f), Mats.Lit(ArtDirection.Sage));
                Prims.SignText(root, p.Name, new Vector3(0, 3.3f, -.27f), width - .8f, .65f, ArtDirection.Cream);
                Prims.Box(root, "Roof", new Vector3(0, height + .08f, depth / 2), new Vector3(width + .3f, .2f, depth + .3f), Mats.Lit(ArtDirection.Ink));
                var open = Prims.Box(root, "OpenLamp", new Vector3(width * .37f, 2.8f, -.24f), new Vector3(.4f, .25f, .08f), Mats.Unlit(UiGreen));
                var closed = Prims.Box(root, "ClosedLamp", new Vector3(width * .37f, 2.8f, -.24f), new Vector3(.4f, .25f, .08f), Mats.Unlit(ArtDirection.Clay));
                visual.OpenOnly.Add(open); visual.ClosedOnly.Add(closed);
            }
            Vector3 path = entrance - node;
            if (path.sqrMagnitude > .1f)
            {
                var approach = Prims.Box(_root, "Approach_" + p.Id, (entrance + node) / 2 + Vector3.up * .08f,
                    new Vector3(1.8f, .14f, path.magnitude + .4f), Mats.Lit(Sidewalk));
                approach.transform.localRotation = Quaternion.LookRotation(path.normalized);
            }
            visual.LabelPos = root.TransformPoint(new Vector3(0, venue ? 4.8f : p.Kind == PlaceKind.Home ? 8 : 5.2f, depth / 2));
            visual.Sign = root.GetComponentInChildren<UnityEngine.UI.Text>();
        }
        private static readonly Color UiGreen = new Color(.35f, .75f, .46f);
        private static int StableCityHash(string id) { int h = 17; foreach (char c in id) h = unchecked(h * 31 + c); return h & 0x7fffffff; }
    }
}
