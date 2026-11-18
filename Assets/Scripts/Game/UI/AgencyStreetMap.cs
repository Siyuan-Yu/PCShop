using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>A separate, inspectable camera on the real street. It never controls the player camera or travel.</summary>
    [ExecuteAlways]
    public sealed class AgencyStreetMap : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public sealed class ViewState { public float Zoom = 1; public Vector2 Offset; }
        public readonly struct PedestrianBand
        {
            public readonly Vector2 From, To;
            public readonly float Height, Width;
            public readonly int Level;
            public PedestrianBand(Vector2 from,Vector2 to,float height,float width,int level)
            { From=from; To=to; Height=height; Width=width; Level=level; }
        }

        public static readonly Color RentalAvailable = new Color(.15f, 1f, .38f);
        public static readonly Color RentalUpcoming = new Color(1f, .5f, .06f);
        private static readonly Color[] PedestrianBandColors =
        {
            new Color32(174,234,244,255), new Color32(80,179,201,255), new Color32(16,106,132,255)
        };
        public static Color PedestrianBandColor(int level) => PedestrianBandColors[level];

        private GameObject _rentalGlow;
        private GameObject _pedestrianOverlay;
        private readonly List<Mesh> _pedestrianMeshes = new List<Mesh>();
        private Camera _camera;
        private RenderTexture _texture;
        private GameController _game;
        private RectTransform _rect;
        private readonly Dictionary<PlaceVisual, Bounds> _buildingBounds = new Dictionary<PlaceVisual, Bounds>();
        private int _renderFrame = -1;
        private bool _notifyAfterRender;
        private ViewState _state;
        private Vector3 _overview;
        private float _overviewSize;
        private Bounds _cityBounds;
        private Bounds? _districtFrame;
        private Vector2[] _districtOutline;
        private HashSet<string> _placeIds;
        private HashSet<string> _selectableIds;
        private bool _dragged;
        private Vector2 _previousPointer;
        public Action Changed;
        public Action<string> Selected;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float Zoom => _state.Zoom;
        public bool CanSelect(string id)=>id!=null&&_selectableIds!=null&&_selectableIds.Contains(id);
        internal bool ShowsPlace(string id) => id != null && _placeIds != null && _placeIds.Contains(id);
        public static Place[] DistrictPlaces(GameSession session,Place agency)=>session.City.Places
            .Where(p=>session.AgencyServes(agency,p)).OrderBy(p=>session.City.ViewPosition(p.NodeId).X).ToArray();

        public void FrameArea(Vector3 center, float radius)
        {
            _overview = center;
            _overviewSize = radius * 1.15f;
            _cityBounds = new Bounds(center, new Vector3(radius * 3, 20, radius * 3));
            _districtFrame = null;
            _districtOutline = null;
            _state.Zoom = 1; _state.Offset = Vector2.zero;
            RequestMapRender();
        }

        public void Initialize(GameController game, ViewState state, Place[] units, float width, float height,
            bool residential = false,IReadOnlyList<PedestrianBand> pedestrianBands = null)
        {
            _game = game; _state = state; _rect = (RectTransform)transform;
            Width = width; Height = height;
            _camera = new GameObject("AgencyStreetCamera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.nearClipPlane = .3f; _camera.farClipPlane = 600;
            _camera.aspect = width / height;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(159, 179, 156, 255);
            _camera.allowHDR = false;
            var extra = _camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            _camera.transform.rotation = Quaternion.Euler(52, 45, 0);
            _placeIds = new HashSet<string>(units.Select(p => p.Id));
            _selectableIds = new HashSet<string>(units.Where(p => residential ? p.Kind == PlaceKind.Home
                : IsDistrictOverviewPlace(p) || (p.Kind == PlaceKind.Shop && (game.Session.IsMyShop(p)
                    || game.Session.VentureAt(p.Id) != null || game.Session.PropertyAt(p.Id) != null))).Select(p => p.Id));
            bool hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            var shopBounds = new List<Bounds>();
            var localBounds = new List<Bounds>();
            foreach (var unit in units)
            {
                if (!game.World.Places.TryGetValue(unit.Id, out var visual)) continue;
                var buildingBounds = BoundsOf(visual);
                localBounds.Add(buildingBounds);
                localBounds.Add(new Bounds(visual.Entrance, Vector3.zero));
                if (!hasBounds) { bounds = buildingBounds; hasBounds = true; }
                else bounds.Encapsulate(buildingBounds);
                bounds.Encapsulate(game.World.NodeWorld(unit.NodeId));
                // Both tabs share the same district framing and navigation boundary.
                if (IsDistrictOverviewPlace(unit)) shopBounds.Add(buildingBounds);
            }
            if (!hasBounds) bounds = new Bounds(Vector3.zero, new Vector3(40, 20, 40));
            bounds.Expand(new Vector3(8, 4, 8));
            _cityBounds = bounds;
            // A remote venue can belong to this agency administratively without
            // belonging in the shop-selection overview. Keep it reachable by pan,
            // but frame actual shop exteriors, not the empty corners of a world AABB.
            if (shopBounds.Count == 0) shopBounds.Add(bounds);
            var projected = ProjectedBuildingBounds(shopBounds, _camera.transform.rotation);
            _overview = _camera.transform.rotation * projected.center;
            _overviewSize = Mathf.Max(12, Mathf.Max(projected.extents.y, projected.extents.x / _camera.aspect) * 1.12f + 2);
            var districtFrame = ProjectedBuildingBounds(localBounds.Count == 0 ? shopBounds : localBounds, _camera.transform.rotation);
            // Leave enough street frontage to frame properties along the district boundary.
            districtFrame.Expand(new Vector3(32f * _camera.aspect, 32f, 0));
            districtFrame.Encapsulate(new Bounds(projected.center, new Vector3(
                2 * _overviewSize * _camera.aspect, 2 * _overviewSize, 0)));
            _districtFrame = districtFrame;
            // A screen-aligned AABB includes large empty corners beside diagonal streets.
            // Keep the actual footprint, with frontage padding and room for the initial overview.
            var outlinePoints = new List<Vector2>();
            var inverseRotation = Quaternion.Inverse(_camera.transform.rotation);
            foreach (var building in localBounds)
            foreach (var corner in Corners(building))
            {
                var point = inverseRotation * corner;
                AddRectangle(new Vector2(point.x, point.y), new Vector2(16 * _camera.aspect, 16));
            }
            AddRectangle(new Vector2(projected.center.x, projected.center.y),
                new Vector2(_overviewSize * _camera.aspect, _overviewSize));
            _districtOutline = ConvexOutline(outlinePoints);

            void AddRectangle(Vector2 center, Vector2 extent)
            {
                outlinePoints.Add(center + new Vector2(-extent.x, -extent.y));
                outlinePoints.Add(center + new Vector2(extent.x, -extent.y));
                outlinePoints.Add(center + extent);
                outlinePoints.Add(center + new Vector2(-extent.x, extent.y));
            }
            _texture = new RenderTexture(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 24)
            { name = "AgencyStreetOverview", filterMode = FilterMode.Bilinear };
            _camera.targetTexture = _texture;
            GetComponent<RawImage>().texture = _texture;
            if (pedestrianBands?.Count>0 && SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                BuildPedestrianOverlay(pedestrianBands);
            RequestMapRender(notify: false);
        }

        private void BuildPedestrianOverlay(IReadOnlyList<PedestrianBand> bands)
        {
            _pedestrianOverlay = new GameObject("AgencyPedestrianOverlay");
            _pedestrianOverlay.SetActive(false);
            var roadIndex = new RoadSurfaceGeometry.Index();
            var vertices = new[] { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
            // Use the same ownership order as the road mesh so intersections have one fill.
            foreach (var band in bands.OrderByDescending(b => b.Height).ThenByDescending(b => (b.To - b.From).sqrMagnitude))
            {
                var road = RoadSurfaceGeometry.Road(band.From, band.To, band.Width);
                var parts = roadIndex.Cut(new List<Vector2[]> { road }, road);
                roadIndex.Add(road);
                var points = vertices[band.Level];
                var triangles = indices[band.Level];
                foreach (var part in parts)
                {
                    int first = points.Count;
                    foreach (var point in part) points.Add(new Vector3(point.x, band.Height, point.y));
                    for (int i = 1; i < part.Length - 1; i++)
                    {
                        triangles.Add(first);
                        triangles.Add(first + i + 1);
                        triangles.Add(first + i);
                    }
                }
            }
            BuildBandMesh("StreetFlowLow", 0);
            BuildBandMesh("StreetFlowMedium", 1);
            BuildBandMesh("StreetFlowHigh", 2);

            void BuildBandMesh(string name,int level)
            {
                if (vertices[level].Count==0) return;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices[level]);
                mesh.SetTriangles(indices[level],0);
                _pedestrianMeshes.Add(mesh);
                var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(_pedestrianOverlay.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = Mats.Unlit(PedestrianBandColor(level));
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public static Bounds BuildingBounds(PlaceVisual place)
        {
            return place.BuildingBounds();
        }

        public static Bounds ProjectedBuildingBounds(IEnumerable<Bounds> buildings, Quaternion cameraRotation)
        {
            var inverse = Quaternion.Inverse(cameraRotation);
            bool found = false; var result = new Bounds();
            foreach (var building in buildings)
            foreach (var corner in Corners(building))
            {
                var point = inverse * corner;
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
            return result;
        }

        public static bool IsDistrictOverviewPlace(Place place) => place.Kind == PlaceKind.Agency
            || GameSession.IsCommercialPremises(place);

        public static Vector3[] Corners(Bounds b) => new[]
        {
            new Vector3(b.min.x,b.min.y,b.min.z), new Vector3(b.max.x,b.min.y,b.min.z),
            new Vector3(b.max.x,b.min.y,b.max.z), new Vector3(b.min.x,b.min.y,b.max.z),
            new Vector3(b.min.x,b.max.y,b.min.z), new Vector3(b.max.x,b.max.y,b.min.z),
            new Vector3(b.max.x,b.max.y,b.max.z), new Vector3(b.min.x,b.max.y,b.max.z)
        };

        private void ApplyCamera()
        {
            float halfHeight = _overviewSize / _state.Zoom;
            var cameraTransform = _camera.transform;
            Vector3 target = _overview + new Vector3(_state.Offset.x, 0, _state.Offset.y);
            if (_districtFrame is Bounds frame)
            {
                Vector3 right = cameraTransform.right;
                Vector3 up = cameraTransform.up;
                Vector3 groundUp = Vector3.ProjectOnPlane(up, Vector3.up);
                float x = Vector3.Dot(target, right);
                float y = Vector3.Dot(target, up);
                float panX = Mathf.Max(0, frame.extents.x - halfHeight * _camera.aspect);
                float panY = Mathf.Max(0, frame.extents.y - halfHeight);
                float clampedX = Mathf.Clamp(x, frame.center.x - panX, frame.center.x + panX);
                float clampedY = Mathf.Clamp(y, frame.center.y - panY, frame.center.y + panY);
                var bounded = ConstrainDistrictView(new Vector2(clampedX, clampedY),
                    new Vector2(halfHeight * _camera.aspect, halfHeight));
                clampedX = bounded.x;
                clampedY = bounded.y;
                target += right * (clampedX - x) + groundUp * ((clampedY - y) / groundUp.sqrMagnitude);
            }
            else
            {
                target.x = Mathf.Clamp(target.x, _cityBounds.min.x, _cityBounds.max.x);
                target.z = Mathf.Clamp(target.z, _cityBounds.min.z, _cityBounds.max.z);
            }
            _state.Offset = new Vector2(target.x - _overview.x, target.z - _overview.z);
            _camera.orthographicSize = halfHeight;
            cameraTransform.position = target - cameraTransform.forward * 240;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2[] ConvexOutline(List<Vector2> points)
        {
            var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            if (sorted.Length < 3) return sorted;
            var hull = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lowerCount = hull.Count;
            for (int i = sorted.Length - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count > lowerCount && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull.ToArray();
        }

        private Vector2 ConstrainDistrictView(Vector2 target, Vector2 viewportExtent)
        {
            if (_districtOutline == null || _districtOutline.Length < 3) return target;
            // Inset every edge by the viewport's support distance. This constrains
            // all four visible corners, rather than just the camera's centre.
            var allowed = new List<Vector2>(_districtOutline);
            for (int i = 0; i < _districtOutline.Length && allowed.Count > 0; i++)
            {
                var a = _districtOutline[i];
                var edge = _districtOutline[(i + 1) % _districtOutline.Length] - a;
                var normal = new Vector2(-edge.y, edge.x).normalized;
                float limit = Vector2.Dot(normal, a) + Mathf.Abs(normal.x) * viewportExtent.x
                    + Mathf.Abs(normal.y) * viewportExtent.y;
                var clipped = new List<Vector2>();
                var previous = allowed[allowed.Count - 1];
                float previousDistance = Vector2.Dot(normal, previous) - limit;
                foreach (var current in allowed)
                {
                    float distance = Vector2.Dot(normal, current) - limit;
                    if ((distance >= 0) != (previousDistance >= 0))
                        clipped.Add(Vector2.LerpUnclamped(previous, current,
                            previousDistance / (previousDistance - distance)));
                    if (distance >= 0) clipped.Add(current);
                    previous = current;
                    previousDistance = distance;
                }
                allowed = clipped;
            }
            var overview = Quaternion.Inverse(_camera.transform.rotation) * _overview;
            // At minimum zoom the valid area may collapse to a point.
            if (allowed.Count == 0) return new Vector2(overview.x, overview.y);
            bool inside = true;
            var closest = allowed[0];
            float bestDistance = float.PositiveInfinity;
            float area = 0;
            for (int i = 0; i < allowed.Count; i++)
            {
                var a = allowed[i];
                var b = allowed[(i + 1) % allowed.Count];
                var edge = b - a;
                area += Cross(a - allowed[0], b - allowed[0]);
                if (Cross(edge, target - a) < -.001f) inside = false;
                float t = edge.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(target - a, edge) / edge.sqrMagnitude) : 0;
                var candidate = a + edge * t;
                float distance = (candidate - target).sqrMagnitude;
                if (distance < bestDistance) { closest = candidate; bestDistance = distance; }
            }
            return inside && area > .001f ? target : closest;
        }

        private void RequestMapRender(bool notify = true)
        {
            ApplyCamera();
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CaptureMap();
                if (notify) Changed?.Invoke();
                return;
            }
            if (_renderFrame < 0) _renderFrame = Time.frameCount + 1;
            _notifyAfterRender |= notify;
        }

        private void LateUpdate()
        {
            if (_renderFrame >= 0 && Time.frameCount >= _renderFrame) RenderMap();
        }

        public void RenderMap()
        {
            _renderFrame = -1;
            bool notify = _notifyAfterRender;
            _notifyAfterRender = false;
            ApplyCamera();
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CaptureMap();
            if (notify || !Application.isPlaying) Changed?.Invoke();
        }

        private void CaptureMap()
        {
            // Keep the map legible at night, without modifying the scene's lighting after the capture.
            bool fog = RenderSettings.fog;
            AmbientMode ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            var canvases = _game.Ui.Canvases.Where(c => c != null && c.enabled).ToArray();
            var suns = FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional).ToArray();
            var intensities = suns.Select(l => l.intensity).ToArray();
            var colors = suns.Select(l => l.color).ToArray();
            var rotations = suns.Select(l => l.transform.rotation).ToArray();
            // Hide other streets only during this camera capture; restore the live world below.
            var outside = _game.World.Places.Values.Where(p => !_placeIds.Contains(p.Place.Id) && p.Root.activeSelf).ToArray();
            bool worldWasActive = _game.World.Root.activeSelf;
            try
            {
                _game.World.Root.SetActive(true);
                if (_pedestrianOverlay != null) _pedestrianOverlay.SetActive(true);
                foreach (var c in canvases) c.enabled = false;
                foreach (var place in outside) place.Root.SetActive(false);
                if (_rentalGlow != null) _rentalGlow.SetActive(false);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.48f, .56f, .65f);
                foreach (var sun in suns) { sun.intensity = 1.35f; sun.color = new Color(1, .97f, .90f); sun.transform.rotation=Quaternion.Euler(52,-32,0); }
                _camera.Render();
            }
            finally
            {
                if (_pedestrianOverlay != null) _pedestrianOverlay.SetActive(false);
                if (_rentalGlow != null) _rentalGlow.SetActive(false);
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                for (int i = 0; i < suns.Length; i++) { suns[i].intensity = intensities[i]; suns[i].color = colors[i]; suns[i].transform.rotation=rotations[i]; }
                foreach (var c in canvases) if (c != null) c.enabled = true;
                foreach (var place in outside) if (place.Root != null) place.Root.SetActive(true);
                _game.World.Root.SetActive(worldWasActive);
            }
        }

        internal Bounds BoundsOf(PlaceVisual building)
        {
            if (!_buildingBounds.TryGetValue(building, out var bounds))
                _buildingBounds.Add(building, bounds = BuildingBounds(building));
            return bounds;
        }

        public Vector2 Project(Vector3 world)
        {
            Vector3 v = _camera.WorldToViewportPoint(world);
            return new Vector2(v.x * Width, (1 - v.y) * Height);
        }

        public void ZoomBy(float factor)
        {
            float zoom = Mathf.Clamp(_state.Zoom * factor, 1, 8f);
            if (Mathf.Approximately(_state.Zoom, zoom)) return;
            _state.Zoom = zoom;
            RequestMapRender();
        }
        public void ResetView() { _state.Zoom = 1; _state.Offset = Vector2.zero; RequestMapRender(); }
        public void Focus(Place p)
        {
            if (!ShowsPlace(p?.Id)) return;
            Vector3 at = _game.World.Places.TryGetValue(p.Id, out var place) ? place.Entrance : _game.World.NodeWorld(p.NodeId);
            float size = _game.World.Places.TryGetValue(p.Id, out var visual)
                ? Mathf.Max(14, Mathf.Max(BoundsOf(visual).size.x, BoundsOf(visual).size.z) * 1.3f) : 18;
            _state.Zoom = Mathf.Clamp(_overviewSize / size, 1, 8);
            _state.Offset = new Vector2(at.x - _overview.x, at.z - _overview.z);
            RequestMapRender();
        }

        private Vector2 ViewportPoint(Vector2 screen, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, eventCamera, out Vector2 local);
            return new Vector2((local.x - _rect.rect.xMin) / _rect.rect.width, (local.y - _rect.rect.yMin) / _rect.rect.height);
        }

        private Vector3 GroundPoint(Vector2 screen, Camera eventCamera)
        {
            Ray ray = _camera.ViewportPointToRay(ViewportPoint(screen, eventCamera));
            var plane = new Plane(Vector3.up, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : _overview;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_dragged || e.button != PointerEventData.InputButton.Left) return;
            string place = PlaceAt(ViewportPoint(e.position, e.pressEventCamera));
            if (place != null) Selected?.Invoke(place);
        }

        public string PlaceAt(Vector2 viewport)
        {
            Physics.SyncTransforms();
            Ray ray = _camera.ViewportPointToRay(viewport);
            if (!Physics.Raycast(ray, out RaycastHit hit, 600)) return null;
            PlaceMarker marker = hit.collider.GetComponentInParent<PlaceMarker>();
            return marker != null && CanSelect(marker.PlaceId) ? marker.PlaceId : null;
        }

        public void OnBeginDrag(PointerEventData e) { _dragged = true; _previousPointer = e.position; }
        public void OnDrag(PointerEventData e)
        {
            Vector3 delta = GroundPoint(_previousPointer, e.pressEventCamera) - GroundPoint(e.position, e.pressEventCamera);
            _state.Offset += new Vector2(delta.x, delta.z);
            _previousPointer = e.position;
            RequestMapRender();
        }
        public void OnEndDrag(PointerEventData e) { }
        public void OnScroll(PointerEventData e) => ZoomBy(Mathf.Pow(1.18f, e.scrollDelta.y));
        public void OnPointerDown(PointerEventData e) => _dragged = false;

        private void OnDisable() => ReleasePreview();
        private void OnDestroy() => ReleasePreview();

        private void ReleasePreview()
        {
            _renderFrame = -1;
            _notifyAfterRender = false;
            _buildingBounds.Clear();
            if (_pedestrianOverlay != null)
            {
                _pedestrianOverlay.SetActive(false);
                Kill.It(_pedestrianOverlay);
                _pedestrianOverlay = null;
            }
            foreach (var mesh in _pedestrianMeshes) Kill.It(mesh);
            _pedestrianMeshes.Clear();
            if (_rentalGlow != null) { _rentalGlow.SetActive(false); Kill.It(_rentalGlow); _rentalGlow = null; }
            if (_camera != null)
            {
                _camera.targetTexture = null;
                _camera.gameObject.SetActive(false);
                Kill.It(_camera.gameObject);
                _camera = null;
            }
            if (_texture != null) { _texture.Release(); Kill.It(_texture); _texture = null; }
            Changed = null; Selected = null;
        }
    }
}
