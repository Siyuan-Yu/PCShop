using UnityEngine;

namespace PCShopSell.Game.Procedural
{
    /// <summary>
    /// 一天中不同时段�?外观参数"：同一座程序生成的城市，靠这些参数变出白天/黄昏/夜晚�?
    /// </summary>
    public sealed class CityLookProfile
    {
        public string Name = "day";

        // 窗户和招�?
        public float WindowLitChance = 0f;
        public float EmissiveIntensity = 1f;
        public float NeonBoost = 1f;
        public Color UnlitGlassColor = new Color(0.30f, 0.42f, 0.55f);

        // 路面（湿润程度：越高反光越强�?
        public float RoadSmoothness = 0.35f;

        // 路灯
        public bool LampsOn;
        public float LampIntensity = 0f;
        public float NeonLightIntensity = 0f;

        // 天空和环�?
        public Color SkyColor = new Color(0.55f, 0.75f, 0.95f);
        public Color AmbientColor = new Color(0.55f, 0.62f, 0.72f);
        public bool Fog;
        public Color FogColor = new Color(0.5f, 0.6f, 0.7f);
        public float FogStart = 140f;
        public float FogEnd = 320f;

        // 主光（太�?月亮�?
        public Color SunColor = Color.white;
        public float SunIntensity = 1.2f;
        public Vector3 SunEuler = new Vector3(35f, 110f, 0f);

        // 后处�?
        public float BloomIntensity = 0.2f;
        public float BloomThreshold = 1f;
        public float Exposure = 0f;
        public float Saturation = -8f;
        public float Contrast = 4f;
        public float Vignette = 0.10f;
        public float ChromaticAberration = 0f;
        public float FilmGrain = 0f;
        public Vector4 ShadowTint = new Vector4(1f, 1f, 1f, 0f);
        public Vector4 HighlightTint = new Vector4(1f, 1f, 1f, 0f);

        public static CityLookProfile Day()
        {
            return new CityLookProfile
            {
                Name = "day",
                WindowLitChance = 0.05f,
                EmissiveIntensity = 1.2f,
                UnlitGlassColor = new Color(0.35f, 0.5f, 0.65f),
                RoadSmoothness = 0.35f,
                LampsOn = false,
                SkyColor = new Color(0.77f, 0.83f, 0.79f),
                AmbientColor = new Color(0.72f, 0.76f, 0.73f),
                SunColor = new Color(1f, 0.96f, 0.88f),
                SunIntensity = 1.1f,
                SunEuler = new Vector3(42f, 115f, 0f),
                BloomIntensity = 0.15f,
                BloomThreshold = 1.1f,
                Exposure = 0f,
            };
        }

        public static CityLookProfile Dusk()
        {
            return new CityLookProfile
            {
                Name = "dusk",
                WindowLitChance = 0.45f,
                EmissiveIntensity = 1.6f,
                NeonBoost = 1.6f,
                UnlitGlassColor = new Color(0.20f, 0.26f, 0.38f),
                RoadSmoothness = 0.7f,
                LampsOn = true,
                LampIntensity = 2.5f,
                NeonLightIntensity = 4f,
                SkyColor = new Color(0.95f, 0.55f, 0.38f),
                AmbientColor = new Color(0.56f, 0.53f, 0.49f),
                Fog = true,
                FogColor = new Color(0.55f, 0.35f, 0.45f),
                FogStart = 150f,
                FogEnd = 340f,
                SunColor = new Color(1f, 0.58f, 0.32f),
                SunIntensity = 1.1f,
                SunEuler = new Vector3(9f, 105f, 0f),
                BloomIntensity = 0.22f,
                BloomThreshold = 0.9f,
                Exposure = 0f,
                Vignette = 0.12f,
                ChromaticAberration = 0f,
                FilmGrain = 0.02f,
                ShadowTint = new Vector4(0.95f, 0.85f, 1.15f, 0f),
                HighlightTint = new Vector4(1.1f, 1.0f, 0.95f, 0f),
            };
        }

        public static CityLookProfile Night()
        {
            return new CityLookProfile
            {
                Name = "night",
                WindowLitChance = 0.65f,
                EmissiveIntensity = 2.1f,
                NeonBoost = 1.5f,
                UnlitGlassColor = new Color(0.05f, 0.07f, 0.11f),
                RoadSmoothness = 0.92f,
                LampsOn = true,
                LampIntensity = 4.5f,
                NeonLightIntensity = 9f,
                SkyColor = new Color(0.02f, 0.02f, 0.07f),
                AmbientColor = new Color(0.30f, 0.38f, 0.42f),
                Fog = true,
                FogColor = new Color(0.17f, 0.25f, 0.28f),
                FogStart = 150f,
                FogEnd = 330f,
                SunColor = new Color(0.67f, 0.78f, 0.85f),
                SunIntensity = 0.5f,
                SunEuler = new Vector3(38f, 125f, 0f),
                BloomIntensity = 0.28f,
                BloomThreshold = 0.9f,
                Exposure = 0.1f,
                Saturation = -10f,
                Contrast = 5f,
                Vignette = 0.16f,
                ChromaticAberration = 0f,
                FilmGrain = 0.025f,
                ShadowTint = new Vector4(0.80f, 0.78f, 1.25f, 0f),
                HighlightTint = new Vector4(1.0f, 0.90f, 1.15f, 0f),
            };
        }
    }
}
