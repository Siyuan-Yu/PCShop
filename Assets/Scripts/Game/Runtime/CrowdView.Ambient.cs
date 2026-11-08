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
            public Vector3 Target;

            public int Side;
        }
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
        private static Vector3 AmbientLane(Vector3 from, Vector3 to, bool endpoint, int side)
        {
            var point = endpoint ? to : from;
            if (Mathf.Abs(from.z) < .1f && Mathf.Abs(to.z) < .1f)
                return point + Vector3.forward * side * (float)(GameSession.FrontYAt(point.x) - GameSession.SidewalkInset);
            Vector3 normal = Vector3.Cross(Vector3.up, (to - from).normalized);
            return point + normal * side * 3.2f;
        }
        private void SyncAmbient(GameSession session, Camera camera)
        {
            if (_ambientCity != session.City)
            {
                _ambientCity = session.City; _ambientEdges.Clear();
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
            for (int i = 0; i < _ambientCount; i++)
            {
                bool added = i >= _ambient.Count;
                if (added)
                {
                    var model = Spawn(null, Vector3.zero).Go;
                    var collider = model.AddComponent<SphereCollider>(); collider.center = Vector3.up * .9f; collider.radius = .5f;
                    model.AddComponent<AmbientPersonMarker>().Status = i % 3 == 0 ? "正在逛街" : "正在路过";
                    _ambient.Add(new AmbientWalker { Go = model, Side = i % 2 == 0 ? 1 : -1 });
                }
                var person = _ambient[i];
                if (added || string.IsNullOrEmpty(person.NextNode) || !session.City.Map.HasNode(person.NextNode) || !AmbientOnScreen(camera, person.Go.transform.position, .3f))
                {
                    var entries = !added && _ambientEntryEdges.Count > 0 ? _ambientEntryEdges : _visibleAmbientEdges;
                    var edge = entries[_rng.Next(entries.Count)];
                    bool reverse = _rng.Next(2) == 0;
                    if (!added && _ambientEntryEdges.Count > 0)
                        reverse = AmbientOnScreen(camera, edge.From, -.02f);
                    Vector3 from = reverse ? edge.To : edge.From, to = reverse ? edge.From : edge.To;
                    person.PreviousNode = reverse ? edge.B : edge.A; person.NextNode = reverse ? edge.A : edge.B;
                    person.Target = AmbientLane(from, to, true, person.Side);
                    Vector3 start = AmbientLane(from, to, false, person.Side);
                    // New views can already be on their way. Recycled views enter near a street end.
                    float progress = added ? (float)_rng.NextDouble() : .02f;
                    var position = Vector3.Lerp(start, person.Target, progress);
                    position.y = _streets?.StreetHeightAt(position.x, position.z) ?? 0;
                    person.Go.transform.position = position;
                    person.Go.transform.rotation = Quaternion.LookRotation((person.Target - start).normalized);
                    person.Go.GetComponent<FigureMotion>()?.ResetMotion();
                }
            }
        }
        private void UpdateAmbient(float dt, bool visible)
        {
            for (int i = 0; i < _ambient.Count; i++)
            {
                var person = _ambient[i]; bool show = visible && i < _ambientCount;
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
                        var options = _ambientCity.Map.NeighborsOf(person.NextNode);
                        string next = null; int count = 0;
                        foreach (var edge in options)
                        {
                            if (edge.To == person.PreviousNode) continue;
                            if (_rng.Next(++count) == 0) { next = edge.To; }
                        }
                        if (next == null)
                            foreach (var edge in _ambientCity.Map.NeighborsOf(person.NextNode)) { next = edge.To; break; }
                        if (next == null) break;
                        var from = StreetWorldBuilder.ToWorld(_ambientCity.ViewPosition(person.NextNode));
                        var to = StreetWorldBuilder.ToWorld(_ambientCity.ViewPosition(next));
                        person.PreviousNode = person.NextNode; person.NextNode = next;
                        person.Target = AmbientLane(from, to, true, person.Side);
                        continue;
                    }
                    // Use the player's road calibration at the actual position. A corner's
                    // remaining visual distance must never redefine the walker's speed.
                    var at=person.Go.transform.position;
                    double scale=CityTravelTiming.LocalScale(_ambientCity,new PCShopSell.Core.World.MapPoint(at.x,at.z));
                    float move = (float)Math.Min(distance, Math.Min(.75,remaining / Math.Max(.01, scale)));
                    person.Go.transform.position += delta / distance * move;
                    person.Go.transform.rotation = Quaternion.RotateTowards(person.Go.transform.rotation, Quaternion.LookRotation(delta.normalized), 540 * dt);
                    remaining -= move * scale;
                }
                var position = person.Go.transform.position;
                position.y = _streets?.StreetHeightAt(position.x, position.z) ?? 0;
                person.Go.transform.position = position;
            }
        }
    }
}
