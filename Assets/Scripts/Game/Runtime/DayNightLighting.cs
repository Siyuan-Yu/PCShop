using System.Collections.Generic;
using PCShopSell.Game.Procedural;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game.Runtime
{
    /// <summary>
    /// 按游戏时间在白天 / 黄昏 / 夜晚三套外观之间插值：太阳、环境光、雾、后处理、夜间发光材质和路灯。
    /// </summary>
    public sealed class DayNightLighting
    {
        private readonly CityLookProfile _day = CityLookProfile.Day();
        private readonly CityLookProfile _dusk = CityLookProfile.Dusk();
        private readonly CityLookProfile _night = CityLookProfile.Night();

        private readonly Camera _cam;
        private readonly Light _sun;
        private readonly List<Light> _lamps;
        private readonly Bloom _bloom;
        private readonly ColorAdjustments _grade;
        private readonly Vignette _vignette;
        private readonly FilmGrain _grain;
        private float _lastMinute = -1;

        public DayNightLighting(Camera cam, Light sun, Volume volume, List<Light> lamps)
        {
            _cam = cam;
            _sun = sun;
            _lamps = lamps;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>(true);
            _bloom.scatter.Override(0.7f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            _grade = profile.Add<ColorAdjustments>(true);
            _vignette = profile.Add<Vignette>(true);
            _vignette.smoothness.Override(0.5f);
            _grain = profile.Add<FilmGrain>(true);
            _grain.type.Override(FilmGrainLookup.Thin1);
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.skybox = null;
            RenderSettings.fogMode = FogMode.Linear;
        }

        /// <summary>0 = 白天，1 = 黄昏，2 = 夜晚；返回 (a, b, t)。</summary>
        private (CityLookProfile a, CityLookProfile b, float t) Blend(float minute)
        {
            float h = minute / 60f;
            if (h < 5f) return (_night, _night, 0);
            if (h < 6.5f) return (_night, _dusk, Mathf.InverseLerp(5f, 6.5f, h));
            if (h < 8f) return (_dusk, _day, Mathf.InverseLerp(6.5f, 8f, h));
            if (h < 17f) return (_day, _day, 0);
            if (h < 18.5f) return (_day, _dusk, Mathf.InverseLerp(17f, 18.5f, h));
            if (h < 20f) return (_dusk, _night, Mathf.InverseLerp(18.5f, 20f, h));
            return (_night, _night, 0);
        }

        /// <summary>夜间系数 0~1（路灯、招牌亮度）。</summary>
        public static float NightFactor(float minute)
        {
            float h = minute / 60f;
            if (h < 5.5f || h >= 19.5f) return 1;
            if (h < 7.5f) return 1 - Mathf.InverseLerp(5.5f, 7.5f, h);
            if (h >= 17.5f) return Mathf.InverseLerp(17.5f, 19.5f, h);
            return 0;
        }

        private LightShadows? _sunShadows;

        /// <summary>编辑模式（俯视图）：关掉太阳阴影，墙的长影子不会盖住房间；退出时恢复。</summary>
        public void SetEditView(bool on)
        {
            if (on)
            {
                if (_sunShadows == null) _sunShadows = _sun.shadows;
                _sun.shadows = LightShadows.None;
            }
            else if (_sunShadows != null)
            {
                _sun.shadows = _sunShadows.Value;
                _sunShadows = null;
            }
        }

        /// <param name="interior">在店里时用暖色室内光。</param>
        public void Apply(float minuteOfDay, bool interior)
        {
            if (Mathf.Abs(minuteOfDay - _lastMinute) < 0.5f && !interior) return;
            _lastMinute = minuteOfDay;

            var (a, b, t) = Blend(minuteOfDay);
            float night = NightFactor(minuteOfDay);

            _cam.backgroundColor = interior ? new Color(0.08f, 0.08f, 0.1f) : Color.Lerp(a.SkyColor, b.SkyColor, t);
            RenderSettings.ambientLight = interior ? new Color(0.55f, 0.52f, 0.48f) : Color.Lerp(a.AmbientColor, b.AmbientColor, t);
            RenderSettings.fog = !interior && (a.Fog || b.Fog);
            RenderSettings.fogColor = Color.Lerp(a.FogColor, b.FogColor, t);
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 420f;

            _sun.color = interior ? new Color(1f, 0.93f, 0.82f) : Color.Lerp(a.SunColor, b.SunColor, t);
            _sun.intensity = interior ? 1.0f : Mathf.Lerp(a.SunIntensity, b.SunIntensity, t);
            _sun.transform.rotation = Quaternion.Euler(Vector3.Lerp(a.SunEuler, b.SunEuler, t));

            _bloom.intensity.Override(Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, t));
            _bloom.threshold.Override(Mathf.Lerp(a.BloomThreshold, b.BloomThreshold, t));
            _grade.postExposure.Override(Mathf.Lerp(a.Exposure, b.Exposure, t));
            _grade.saturation.Override(Mathf.Lerp(a.Saturation, b.Saturation, t));
            _grade.contrast.Override(Mathf.Lerp(a.Contrast, b.Contrast, t));
            _vignette.intensity.Override(Mathf.Lerp(a.Vignette, b.Vignette, t));
            _grain.intensity.Override(Mathf.Lerp(a.FilmGrain, b.FilmGrain, t));

            float glow = Mathf.Lerp(0.08f, 1f, night);
            foreach (var (mat, baseEmission) in Mats.NightGlow)
            {
                if (mat != null) mat.SetColor("_EmissionColor", baseEmission * glow);
            }
            foreach (Light l in _lamps)
            {
                if (l != null) l.intensity = night * 3f;
            }
        }
    }
}
