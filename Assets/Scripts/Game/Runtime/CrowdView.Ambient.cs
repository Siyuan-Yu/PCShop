using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed class AmbientPersonMarker : MonoBehaviour { public string Status = "正在路过"; }

    public sealed partial class CrowdView
    {
        private sealed class AmbientEdge { public string A, B; public Vector3 From, To; }
        private sealed class AmbientWalker
        {
            public GameObject Go;
            public string PreviousNode, NextNode;
            public Vector3 LaneStart, LaneEnd;
            public bool JoiningLane;
            public Vector3 Target => JoiningLane ? LaneStart : LaneEnd;
        }
        private const float AmbientSpacing = .85f;
        private readonly List<AmbientWalker> _ambient = new List<AmbientWalker>();
        private readonly List<AmbientEdge> _ambientEdges = new List<AmbientEdge>(), _visibleAmbientEdges = new List<AmbientEdge>(), _ambientEntryEdges = new List<AmbientEdge>();
        private CityLayout _ambientCity;
        private int _ambientCount;
        private static bool AmbientOnScreen(Camera camera, Vector3 point, float margin)
        {
            if (camera == null) return false;
            var p = camera.WorldToViewportPoint(point + Vector3.up);
            return p.z > 0 && p.x >= -margin && p.x <= 1 + margin && p.y >= -margin && p.y <= 1 + margin;
        }
        private Vector3 AmbientLaneOffset(Vector3 from, Vector3 to)
        {
            var a = new MapPoint(from.x, from.z);
            var b = new MapPoint(to.x, to.z);
            double width = _ambientCity.LayoutVersion >= 7 ? GameSession.CityRoadWidth(a, b)
                : GameSession.IsShoppingStreet(a, b) ? 14 : GameSession.IsMainRoad(a, b) ? 7 : 5;
            // Opposing directions use opposite sidewalks.
            return Vector3.Cross(Vector3.up, (to - from).normalized)
                * (float)((width + GameSession.CityPavingWidth) * .5);
        }

        private void SetAmbientLane(AmbientWalker person, string fromNode, string toNode, Vector3 from, Vector3 to)
        {
            var offset = AmbientLaneOffset(from, to);
            person.PreviousNode = fromNode;
            person.NextNode = toNode;
            person.LaneStart = from + offset;
            person.LaneEnd = to + offset;
            person.JoiningLane = true;
        }

        private void SyncAmbient(GameSession session, Camera camera)
        {
            if (_ambientCity != session.City)
            {
                _ambientCity = session.City; _ambientEdges.Clear();
                foreach (var person in _ambient) person.NextNode = null;
                foreach (var node in session.City.Map.Nodes)
                foreach (var edge in session.City.Map.NeighborsOf(node.Id))
                {
                    if (string.CompareOrdinal(node.Id, edge.To) >= 0) continue;
                    Vector3 a = StreetWorldBuilder.ToWorld(session.City.ViewPosition(node.Id));
                    Vector3 b = StreetWorldBuilder.ToWorld(session.City.ViewPosition(edge.To));
                    float length = Vector3.Distance(a, b);
                    if (length < 3) continue;
                    _ambientEdges.Add(new AmbientEdge { A = node.Id, B = edge.To, From = a, To = b });
                }
            }
            _visibleAmbientEdges.Clear();
            foreach (var edge in _ambientEdges)
                if (AmbientOnScreen(camera, edge.From, .1f) || AmbientOnScreen(camera, edge.To, .1f)
                    || AmbientOnScreen(camera, (edge.From + edge.To) * .5f, .1f)) _visibleAmbientEdges.Add(edge);
            _ambientEntryEdges.Clear();
            foreach (var edge in _visibleAmbientEdges)
                if (!AmbientOnScreen(camera, edge.From, -.02f) || !AmbientOnScreen(camera, edge.To, -.02f)) _ambientEntryEdges.Add(edge);
            int minute = session.Clock.MinuteOfDay;
            int daytime = camera == null ? 12 : Mathf.Clamp(Mathf.CeilToInt(camera.orthographicSize * camera.aspect / 7), 8, 28);
            _ambientCount = _visibleAmbientEdges.Count == 0 ? 0 : minute >= 420 && minute < 1200 ? daytime : minute >= 360 && minute < 1380 ? Math.Max(4, daytime / 2) : 2;
            if (_ambientCount > 0)
            {
                double growth = 0;
                foreach (var edge in _visibleAmbientEdges)
                    growth += session.StreetTrafficMultiplier(session.TrafficStreetOfSegment(edge.A, edge.B)) - 1;
                _ambientCount += Mathf.RoundToInt((float)(_ambientCount * growth / _visibleAmbientEdges.Count));
            }
            for (int i = _ambientCount; i < _ambient.Count; i++)
                _ambient[i].NextNode = null;
            for (int i = 0; i < _ambientCount; i++)
            {
                bool added = i >= _ambient.Count;
                if (added)
                {
                    var model = Spawn(null, Vector3.zero).Go;
                    CityNpcClothing.Apply(model, session, "ambient:" + i);
                    var collider = model.AddComponent<SphereCollider>(); collider.center = Vector3.up * .9f; collider.radius = .5f;
                    model.AddComponent<AmbientPersonMarker>().Status = i % 3 == 0 ? "正在逛街" : "正在路过";
                    _ambient.Add(new AmbientWalker { Go = model });
                }
                var person = _ambient[i];
                if (added || string.IsNullOrEmpty(person.NextNode) || !session.City.Map.HasNode(person.NextNode) || !AmbientOnScreen(camera, person.Go.transform.position, .3f))
                {
                    PlaceAmbient(person, camera, added);
                }
            }
        }

        private void PlaceAmbient(AmbientWalker person, Camera camera, bool added)
        {
            person.NextNode = null;
            bool enterFromStreetEnd = !added && _ambientEntryEdges.Count > 0;
            var entries = enterFromStreetEnd ? _ambientEntryEdges : _visibleAmbientEdges;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var edge = entries[_rng.Next(entries.Count)];
                bool canStartAtA = !enterFromStreetEnd || !AmbientOnScreen(camera, edge.From, -.02f);
                bool canStartAtB = !enterFromStreetEnd || !AmbientOnScreen(camera, edge.To, -.02f);
                bool reverse = canStartAtB && (!canStartAtA || _rng.Next(2) == 0);
                Vector3 from = reverse ? edge.To : edge.From, to = reverse ? edge.From : edge.To;
                float progress = enterFromStreetEnd ? .02f : (float)_rng.NextDouble();
                var position = Vector3.Lerp(from, to, progress) + AmbientLaneOffset(from, to);
                if (!AmbientPositionFree(position)) continue;
                SetAmbientLane(person, reverse ? edge.B : edge.A, reverse ? edge.A : edge.B, from, to);
                person.JoiningLane = false;
                position.y = _streets?.StreetHeightAt(position.x, position.z) ?? 0;
                person.Go.transform.position = position;
                person.Go.transform.rotation = Quaternion.LookRotation(to - from);
                person.Go.GetComponent<FigureMotion>()?.ResetMotion();
                return;
            }
            // Retry on the next sync if the entry is still occupied.
        }

        private bool AmbientPositionFree(Vector3 position)
        {
            for (int i = 0; i < Mathf.Min(_ambient.Count, _ambientCount); i++)
            {
                var other = _ambient[i];
                if (other.NextNode == null) continue;
                var delta = other.Go.transform.position - position;
                delta.y = 0;
                if (delta.sqrMagnitude < AmbientSpacing * AmbientSpacing) return false;
            }
            return true;
        }

        private float LimitAmbientStride(AmbientWalker person, Vector3 at, Vector3 direction, float distance)
        {
            for (int i = 0; i < _ambientCount; i++)
            {
                var other = _ambient[i];
                if (other == person || other.NextNode == null) continue;
                var offset = other.Go.transform.position - at;
                offset.y = 0;
                float ahead = Vector3.Dot(offset, direction);
                if (ahead <= 0 || ahead >= distance + AmbientSpacing) continue;
                float across = (offset - direction * ahead).sqrMagnitude;
                if (across >= .35f * .35f) continue;
                if (Vector3.Dot(other.Target - other.Go.transform.position, direction) < 0) continue;
                distance = Mathf.Min(distance, Mathf.Max(0, ahead - AmbientSpacing));
            }
            return distance;
        }

        private void UpdateAmbient(float dt, bool visible)
        {
            for (int i = 0; i < _ambient.Count; i++)
            {
                var person = _ambient[i]; bool show = visible && i < _ambientCount && person.NextNode != null;
                if (person.Go.activeSelf != show) person.Go.SetActive(show);
                if (!show || dt <= 0 || _citySession == null) continue;
                double remaining = CityTravelTiming.ScaledSecondsMeters(Vehicles.Walk, dt, _citySession.Time.SecondsPerMinuteAt1x) * CityTravelTiming.NpcWalkSpeedFactor;
                for (int step = 0; remaining > .00001 && step < 128; step++)
                {
                    Vector3 delta = person.Target - person.Go.transform.position;
                    delta.y = 0;
                    float distance = delta.magnitude;
                    if (distance < .02f)
                    {
                        if (person.JoiningLane)
                        {
                            person.JoiningLane = false;
                            continue;
                        }
                        var options = _ambientCity.Map.NeighborsOf(person.NextNode);
                        string next = null; int count = 0;
                        foreach (var edge in options)
                        {
                            if (edge.To == person.PreviousNode || edge.To == person.NextNode) continue;
                            if (_rng.Next(++count) == 0) { next = edge.To; }
                        }
                        if (next == null)
                            next = person.PreviousNode;
                        if (next == null) break;
                        var from = StreetWorldBuilder.ToWorld(_ambientCity.ViewPosition(person.NextNode));
                        var to = StreetWorldBuilder.ToWorld(_ambientCity.ViewPosition(next));
                        SetAmbientLane(person, person.NextNode, next, from, to);
                        continue;
                    }
                    // Use the player's road calibration at the actual position. A corner's
                    // remaining visual distance must never redefine the walker's speed.
                    var at=person.Go.transform.position;
                    double scale=CityTravelTiming.LocalScale(_ambientCity,new PCShopSell.Core.World.MapPoint(at.x,at.z));
                    Vector3 direction = delta / distance;
                    float stride = (float)Math.Min(distance, Math.Min(.75, remaining / Math.Max(.01, scale)));
                    float move = LimitAmbientStride(person, at, direction, stride);
                    if (move <= .00001f) break;
                    person.Go.transform.position += direction * move;
                    person.Go.transform.rotation = Quaternion.RotateTowards(person.Go.transform.rotation, Quaternion.LookRotation(direction), 540 * dt);
                    remaining -= move * scale;
                }
                var position = person.Go.transform.position;
                position.y = _streets?.StreetHeightAt(position.x, position.z) ?? 0;
                person.Go.transform.position = position;
            }
        }
    }
}
