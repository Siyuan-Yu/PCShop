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

        public static readonly Color RentalAvailable = new Color32(22, 110, 94, 255);
        public static readonly Color RentalUpcoming = new Color32(156, 77, 30, 255);
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
        private const float DefaultCameraDistance = 240f;
        private const float DefaultFarClip = 600f;
        private const float FullDistrictPanZoom = 2f;
        private float _cameraDistance = DefaultCameraDistance;
        private Bounds _cityBounds;
        private Vector2[] _districtOutline;
        private bool _assetOverview;
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
            _districtOutline = null;
            _cameraDistance = DefaultCameraDistance;
            _camera.farClipPlane = DefaultFarClip;
            _state.Zoom = 1; _state.Offset = Vector2.zero;
            RequestMapRender();
        }

        public void Initialize(GameController game, ViewState state, Place[] units, float width, float height,
            bool residential = false,IReadOnlyList<PedestrianBand> pedestrianBands = null, bool assetOverview = false)
        {
            _game = game; _state = state; _rect = (RectTransform)transform; _assetOverview = assetOverview;
            Width = width; Height = height;
            _camera = new GameObject("AgencyStreetCamera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.nearClipPlane = .3f; _camera.farClipPlane = DefaultFarClip;
            _camera.aspect = width / height;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(159, 179, 156, 255);
            _camera.allowHDR = false;
            // Property markers are drawn by the map UI. Keep the street's translucent effects out of the render texture.
            _camera.cullingMask &= ~(1 << ShopBeacon.StreetPropertyEffectLayer);
            var extra = _camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            _camera.transform.rotation = assetOverview ? Quaternion.Euler(68, 5, 0) : Quaternion.Euler(60, 45, 0);
            _placeIds = new HashSet<string>(units.Select(p => p.Id));
            _selectableIds = new HashSet<string>(units.Where(p => assetOverview || (residential ? p.Kind == PlaceKind.Home
                : CanSelectCommercial(p))).Select(p => p.Id));
            if (assetOverview) FrameWholeCity();
            else FrameDistrict(units);
            _texture = new RenderTexture(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 24)
            { name = "AgencyStreetOverview", filterMode = FilterMode.Bilinear };
            _camera.targetTexture = _texture;
            GetComponent<RawImage>().texture = _texture;
            if (pedestrianBands?.Count>0 && SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                BuildPedestrianOverlay(pedestrianBands);
            RequestMapRender(notify: false);
        }

        private static bool CanSelectCommercial(Place place) => place.Kind != PlaceKind.Home && !place.IsElectronicsCounter;

        private bool IsCommercialFramePlace(Place place) => IsDistrictOverviewPlace(place)
            || (place.Kind == PlaceKind.Shop && (_game.Session.IsMyShop(place)
                || _game.Session.VentureAt(place.Id) != null || _game.Session.PropertyAt(place.Id) != null));

        private void FrameWholeCity()
        {
            _districtOutline = null;
            _cityBounds = _game.World.CityBounds;
            var footprint = new Bounds(new Vector3(_cityBounds.center.x, 0, _cityBounds.center.z),
                new Vector3(_cityBounds.size.x, 0, _cityBounds.size.z));
            var projected = ProjectedBuildingBounds(new[] { footprint }, _camera.transform.rotation);
            _overview = footprint.center;
            _overviewSize = Mathf.Max(projected.extents.y, projected.extents.x / _camera.aspect) * 1.08f;
            _cameraDistance = Mathf.Max(DefaultCameraDistance, _overviewSize * 2.2f);
            _camera.farClipPlane = _cameraDistance + Mathf.Max(DefaultFarClip, footprint.size.magnitude);
        }

        public void FrameAssetRegion(LayoutAgencyDistrictData district, bool reset = true)
        {
            if (district == null) FrameWholeCity();
            else
            {
                FrameDistrict(_game.Session.City.PublicPlaces.Where(place => district.streets.Contains(place.StreetId)).ToArray());
                _districtOutline = null;
                _cityBounds = _game.World.CityBounds;
            }
            if (reset) ResetView();
            else RequestMapRender();
        }

        private void FrameDistrict(Place[] units)
        {
            var buildings = new List<Bounds>();
            foreach (var unit in units)
            {
                // 房源和日常街面界定片区。独立会展设施、赛道等专用场地不拉大片区。
                bool isDistrictFrontage = unit.Kind == PlaceKind.Home || unit.Kind == PlaceKind.Agency
                    || unit.Kind == PlaceKind.Wholesaler || unit.Kind == PlaceKind.Station
                    || (unit.Kind == PlaceKind.Shop && unit.Business != "venue_expo");
                if (!isDistrictFrontage && !IsCommercialFramePlace(unit)) continue;
                if (!_game.World.Places.TryGetValue(unit.Id, out var visual)) continue;
                buildings.Add(BoundsOf(visual));
                buildings.Add(new Bounds(visual.Entrance, Vector3.zero));
            }
            if (buildings.Count == 0) buildings.Add(new Bounds(Vector3.zero, new Vector3(40, 20, 40)));

            var footprint = new List<Vector2>();
            foreach (var building in buildings)
            {
                var min = building.min;
                var max = building.max;
                footprint.Add(new Vector2(min.x, min.z));
                footprint.Add(new Vector2(max.x, min.z));
                footprint.Add(new Vector2(max.x, max.z));
                footprint.Add(new Vector2(min.x, max.z));
            }
            _districtOutline = ConvexOutline(footprint);

            var rotation = _camera.transform.rotation;
            var projected = ProjectedBuildingBounds(buildings, rotation);
            var center = rotation * projected.center;
            var forward = _camera.transform.forward;
            var groundCenter = center - forward * (center.y / forward.y);
            var overviewCenter = ClampToOutline(new Vector2(groundCenter.x, groundCenter.z), _districtOutline);
            _overview = new Vector3(overviewCenter.x, 0, overviewCenter.y);
            var projectedCenter = Quaternion.Inverse(rotation) * _overview;
            float halfWidth = projected.extents.x + Mathf.Abs(projected.center.x - projectedCenter.x);
            float halfHeight = projected.extents.y + Mathf.Abs(projected.center.y - projectedCenter.y);
            _overviewSize = Mathf.Max(12, Mathf.Max(halfHeight, halfWidth / _camera.aspect) * 1.12f + 2);

            float minDepth = projected.min.z;
            float maxDepth = projected.max.z;
            foreach (var point in _districtOutline)
            {
                float depth = forward.x * point.x + forward.z * point.y;
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
            }
            float depthSpan = maxDepth - minDepth;
            _cameraDistance = Mathf.Max(DefaultCameraDistance, depthSpan + 16);
            _camera.farClipPlane = Mathf.Max(DefaultFarClip, _cameraDistance + depthSpan + 16);
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
            var center = new Vector2(_overview.x, _overview.z);
            var target = center + _state.Offset;
            if (_districtOutline != null)
            {
                // 让全景留在片区中央，放大后逐步开放到完整的片区边界。
                float pan = Mathf.InverseLerp(1f, FullDistrictPanZoom, _state.Zoom);
                target = pan > 0 ? ClampToOutline(target, _districtOutline, center, pan) : center;
            }
            else
            {
                target.x = Mathf.Clamp(target.x, _cityBounds.min.x, _cityBounds.max.x);
                target.y = Mathf.Clamp(target.y, _cityBounds.min.z, _cityBounds.max.z);
            }
            _state.Offset = target - center;
            _camera.orthographicSize = halfHeight;
            cameraTransform.position = new Vector3(target.x, _overview.y, target.y) - cameraTransform.forward * _cameraDistance;
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

        private static Vector2 ClampToOutline(Vector2 target, Vector2[] outline, Vector2 center = default, float scale = 1f)
        {
            bool inside = outline.Length >= 3;
            var closest = Vector2.LerpUnclamped(center, outline[0], scale);
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = Vector2.LerpUnclamped(center, outline[i], scale);
                var b = Vector2.LerpUnclamped(center, outline[(i + 1) % outline.Length], scale);
                var edge = b - a;
                if (Cross(edge, target - a) < -.001f) inside = false;
                float t = edge.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(target - a, edge) / edge.sqrMagnitude) : 0;
                var candidate = a + edge * t;
                float distance = (candidate - target).sqrMagnitude;
                if (distance < bestDistance) { closest = candidate; bestDistance = distance; }
            }
            return inside ? target : closest;
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
            bool worldWasActive = _game.World.Root.activeSelf;
            try
            {
                _game.World.Root.SetActive(true);
                if (_pedestrianOverlay != null) _pedestrianOverlay.SetActive(true);
                foreach (var c in canvases) c.enabled = false;
                if (_rentalGlow != null) _rentalGlow.SetActive(false);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = _assetOverview ? new Color(.39f, .45f, .49f) : new Color(.48f, .56f, .65f);
                foreach (var sun in suns)
                {
                    sun.intensity = _assetOverview ? 1.12f : 1.35f;
                    sun.color = new Color(1, .97f, .90f);
                    sun.transform.rotation = Quaternion.Euler(52, -32, 0);
                }
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
                _game.World.Root.SetActive(worldWasActive);
            }
        }

        internal Bounds BoundsOf(PlaceVisual building)
        {
            if (!_buildingBounds.TryGetValue(building, out var bounds))
            {
                bounds = BuildingBounds(building);
                _buildingBounds.Add(building, bounds);
            }
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
            float minZoom = _districtOutline != null ? FullDistrictPanZoom : 1f;
            _state.Zoom = Mathf.Clamp(_overviewSize / size, minZoom, 8);
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
            if (!Physics.Raycast(ray, out RaycastHit hit, _camera.farClipPlane)) return null;
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
