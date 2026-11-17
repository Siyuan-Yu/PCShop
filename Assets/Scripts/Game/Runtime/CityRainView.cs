using UnityEngine;
using UnityEngine.Rendering;

namespace PCShopSell.Game.Runtime
{
    public sealed class CityRainView : MonoBehaviour
    {
        private const float FallHeight = 18f;
        private const float FallSpeed = 18f;
        private const float Lifetime = FallHeight / FallSpeed;
        private Camera _camera;
        private ParticleSystem _rain;
        private ParticleSystemRenderer _renderer;
        private Material _material;
        private Texture2D _streak;
        private bool _visible;

        public void Initialize(Camera camera)
        {
            _camera = camera;
            _rain = gameObject.AddComponent<ParticleSystem>();
            _rain.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Lifetime;
            main.startSpeed = FallSpeed;
            main.maxParticles = 1300;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var wind = _rain.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = 2.4f;
            wind.y = 0;
            wind.z = 1.4f;

            _renderer = _rain.GetComponent<ParticleSystemRenderer>();
            _renderer.renderMode = ParticleSystemRenderMode.Stretch;
            _renderer.lengthScale = 2f;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _streak = CreateStreakTexture();
            _material = new Material(Mats.Unlit(new Color(.73f, .83f, .98f, .62f))) { name = "City rain streak" };
            _material.SetTexture("_BaseMap", _streak);
            _material.mainTexture = _streak;
            _renderer.sharedMaterial = _material;
        }

        public void Sync(bool visible)
        {
            if (!visible)
            {
                if (_visible) _rain.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                _visible = false;
                return;
            }

            // Cover the ground visible to the camera, including the upstream side where high drops project into view.
            Vector3 forward = _camera.transform.forward;
            float slope = Mathf.Max(.2f, -forward.y);
            float groundDistance = Mathf.Clamp(_camera.transform.position.y / slope, 5f, _camera.farClipPlane);
            Vector3 center = _camera.transform.position + forward * groundDistance;
            Vector3 groundForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            float halfHeight = _camera.orthographic ? _camera.orthographicSize
                : groundDistance * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float heightOffset = FallHeight * Vector3.Dot(_camera.transform.up, Vector3.up) / slope;
            center -= groundForward * (heightOffset * .5f);
            center.y = FallHeight;
            bool movedFar = (center - transform.position).sqrMagnitude > 40f * 40f;
            transform.SetPositionAndRotation(center, Quaternion.LookRotation(Vector3.down, groundForward));

            float width = halfHeight * 2f * _camera.aspect + 8f;
            float depth = halfHeight * 2f / slope + heightOffset + 8f;
            var shape = _rain.shape;
            shape.scale = new Vector3(width, depth, .1f);
            var emission = _rain.emission;
            emission.rateOverTime = Mathf.Clamp(width * depth * .16f, 230f, 1150f);
            var main = _rain.main;
            main.startSize = Mathf.Max(.022f, halfHeight * .0042f);
            _renderer.velocityScale = Mathf.Clamp(halfHeight * .0032f, .017f, .12f);

            if (!_visible || movedFar)
            {
                // Start with drops throughout the view even when the player just stepped out or moved the camera.
                _rain.Simulate(Lifetime, false, true, true);
                _rain.Play(false);
            }
            _visible = true;
        }

        private static Texture2D CreateStreakTexture()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Rain streak falloff", wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x * 2f / (size - 1) - 1);
                    float dy = Mathf.Abs(y * 2f / (size - 1) - 1);
                    float fade = Mathf.Clamp01(2f * (1 - dx)) * Mathf.Clamp01(2f * (1 - dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(fade * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            Kill.It(_material);
            Kill.It(_streak);
        }
    }
}
