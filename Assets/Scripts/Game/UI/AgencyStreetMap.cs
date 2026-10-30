using System;
using System.Linq;
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
        private Camera _camera;
        private RenderTexture _texture;
        private GameController _game;
        private RectTransform _rect;
        private ViewState _state;
        private Vector3 _overview;
        private float _overviewSize;
        private bool _dragged;
        private Vector2 _previousPointer;
        public Action Changed;
        public Action<string> Selected;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public float Zoom => _state.Zoom;

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
            float minX = (float)units.Min(p => game.City.ViewPosition(p.NodeId).X) - 22;
            float maxX = (float)units.Max(p => game.City.ViewPosition(p.NodeId).X) + 12;
            var bounds = new Bounds(new Vector3((minX + maxX) / 2, 10, 10), new Vector3(maxX - minX, 22, 48));
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
            _state.Offset.x = Mathf.Clamp(_state.Offset.x, -115 * limit, 115 * limit);
            _state.Offset.y = Mathf.Clamp(_state.Offset.y, -65 * limit, 65 * limit);
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
            try
            {
                foreach (var c in canvases) c.enabled = false;
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .70f, .63f);
                foreach (var sun in suns) { sun.intensity = 1.1f; sun.color = new Color(1, .96f, .86f); }
                _camera.Render();
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                for (int i = 0; i < suns.Length; i++) { suns[i].intensity = intensities[i]; suns[i].color = colors[i]; }
                foreach (var c in canvases) if (c != null) c.enabled = true;
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
            Vector3 at = _game.World.NodeWorld(p.NodeId) + new Vector3(0, 0, 10);
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
            return marker?.PlaceId;
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
