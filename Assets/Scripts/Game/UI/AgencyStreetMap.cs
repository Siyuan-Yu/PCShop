using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.World;
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
        public static readonly Color RentalAvailable = new Color(.15f, 1f, .38f);
        public static readonly Color RentalUpcoming = new Color(1f, .5f, .06f);
        private GameObject _rentalGlow;
        private Camera _camera;
        private RenderTexture _texture;
        private GameController _game;
        private RectTransform _rect;
        private ViewState _state;
        private Vector3 _overview;
        private float _overviewSize;
        private Bounds _cityBounds;
        private HashSet<string> _placeIds;
        private bool _dragged;
        private Vector2 _previousPointer;
        public Action Changed;
        public Action<string> Selected;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float Zoom => _state.Zoom;

        public void FrameArea(Vector3 center, float radius)
        {
            _overview = center;
            _overviewSize = radius * 1.15f;
            _cityBounds = new Bounds(center, new Vector3(radius * 3, 20, radius * 3));
            _state.Zoom = 1; _state.Offset = Vector2.zero;
            RenderMap();
        }

        public void Initialize(GameController game, ViewState state, Place[] units, float width, float height)
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
            _camera.allowHDR = true;
            var extra = _camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            _camera.transform.rotation = Quaternion.Euler(40, 45, 0);
            _placeIds = new HashSet<string>(units.Select(p => p.Id));
            bool hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var unit in units)
            {
                if (!game.World.Places.TryGetValue(unit.Id, out var visual)) continue;
                var buildingBounds = BuildingBounds(visual);
                if (!hasBounds) { bounds = buildingBounds; hasBounds = true; }
                else bounds.Encapsulate(buildingBounds);
                bounds.Encapsulate(game.World.NodeWorld(unit.NodeId));
            }
            if (!hasBounds) bounds = new Bounds(Vector3.zero, new Vector3(40, 20, 40));
            bounds.Expand(new Vector3(8, 4, 8));
            _cityBounds = bounds;
            _overview = bounds.center;
            Quaternion inverse = Quaternion.Inverse(_camera.transform.rotation);
            float extentX = 0, extentY = 0;
            foreach (Vector3 corner in Corners(bounds))
            {
                Vector3 v = inverse * (corner - _overview);
                extentX = Mathf.Max(extentX, Mathf.Abs(v.x));
                extentY = Mathf.Max(extentY, Mathf.Abs(v.y));
            }
            _overviewSize = Mathf.Max(extentY, extentX / _camera.aspect) * 1.06f;
            _texture = new RenderTexture(Mathf.RoundToInt(width * 1.5f), Mathf.RoundToInt(height * 1.5f), 24)
            { name = "AgencyStreetOverview", filterMode = FilterMode.Bilinear };
            _camera.targetTexture = _texture;
            GetComponent<RawImage>().texture = _texture;
            RenderMap();
        }

        public static Bounds BuildingBounds(PlaceVisual place)
        {
            return place.BuildingBounds();
        }

        public static Vector3[] Corners(Bounds b) => new[]
        {
            new Vector3(b.min.x,b.min.y,b.min.z), new Vector3(b.max.x,b.min.y,b.min.z),
            new Vector3(b.max.x,b.min.y,b.max.z), new Vector3(b.min.x,b.min.y,b.max.z),
            new Vector3(b.min.x,b.max.y,b.min.z), new Vector3(b.max.x,b.max.y,b.min.z),
            new Vector3(b.max.x,b.max.y,b.max.z), new Vector3(b.min.x,b.max.y,b.max.z)
        };

        private void ApplyCamera()
        {
            float limit = 1 - 1 / _state.Zoom;
            _state.Offset.x = Mathf.Clamp(_state.Offset.x, -_cityBounds.extents.x * limit, _cityBounds.extents.x * limit);
            _state.Offset.y = Mathf.Clamp(_state.Offset.y, -_cityBounds.extents.z * limit, _cityBounds.extents.z * limit);
            _camera.orthographicSize = _overviewSize / _state.Zoom;
            _camera.transform.position = _overview + new Vector3(_state.Offset.x, 0, _state.Offset.y) - _camera.transform.forward * 240;
        }

        public void RenderMap()
        {
            ApplyCamera();
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
                if (_rentalGlow != null) _rentalGlow.SetActive(false);
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                for (int i = 0; i < suns.Length; i++) { suns[i].intensity = intensities[i]; suns[i].color = colors[i]; suns[i].transform.rotation=rotations[i]; }
                foreach (var c in canvases) if (c != null) c.enabled = true;
                foreach (var place in outside) if (place.Root != null) place.Root.SetActive(true);
                _game.World.Root.SetActive(worldWasActive);
            }
            Changed?.Invoke();
        }

        public Vector2 Project(Vector3 world)
        {
            Vector3 v = _camera.WorldToViewportPoint(world);
            return new Vector2(v.x * Width, (1 - v.y) * Height);
        }

        public void ZoomBy(float factor) { _state.Zoom = Mathf.Clamp(_state.Zoom * factor, 1, 3.5f); RenderMap(); }
        public void ResetView() { _state.Zoom = 1; _state.Offset = Vector2.zero; RenderMap(); }
        public void Focus(Place p)
        {
            Vector3 at = _game.World.Places.TryGetValue(p.Id, out var place) ? place.Entrance : _game.World.NodeWorld(p.NodeId);
            _state.Zoom = 2.4f;
            _state.Offset = new Vector2(at.x - _overview.x, at.z - _overview.z);
            RenderMap();
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
            return marker != null && _placeIds.Contains(marker.PlaceId) ? marker.PlaceId : null;
        }

        public void OnBeginDrag(PointerEventData e) { _dragged = true; _previousPointer = e.position; }
        public void OnDrag(PointerEventData e)
        {
            Vector3 delta = GroundPoint(_previousPointer, e.pressEventCamera) - GroundPoint(e.position, e.pressEventCamera);
            _state.Offset += new Vector2(delta.x, delta.z);
            _previousPointer = e.position;
            RenderMap();
        }
        public void OnEndDrag(PointerEventData e) { }
        public void OnScroll(PointerEventData e) => ZoomBy(Mathf.Pow(1.18f, e.scrollDelta.y));
        public void OnPointerDown(PointerEventData e) => _dragged = false;

        private void OnDisable() => ReleasePreview();
        private void OnDestroy() => ReleasePreview();

        private void ReleasePreview()
        {
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
