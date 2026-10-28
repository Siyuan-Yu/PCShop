using UnityEngine;

namespace PCShopSell.Game.Procedural
{
    /// <summary>
    /// 一天中不同时段的"外观参数"：同一座程序生成的城市，靠这些参数变出白天/黄昏/夜晚。
    /// </summary>
    public sealed class CityLookProfile
    {
        public string Name = "day";

        // 窗户和招牌
        public float WindowLitChance = 0f;
        public float EmissiveIntensity = 1f;
        public Color UnlitGlassColor = new Color(0.30f, 0.42f, 0.55f);

        // 路灯
        public bool LampsOn;
        public float LampIntensity = 0f;

        // 天空和环境
        public Color SkyColor = new Color(0.55f, 0.75f, 0.95f);
        public Color AmbientColor = new Color(0.55f, 0.62f, 0.72f);

        // 主光（太阳/月亮）
        public Color SunColor = Color.white;
        public float SunIntensity = 1.2f;
        public Vector3 SunEuler = new Vector3(35f, 110f, 0f);

        // 后处理
        public float BloomIntensity = 0.2f;
        public float BloomThreshold = 1f;
        public float Exposure = 0f;
        public float Saturation = 10f;
        public float Contrast = 10f;
        public float Vignette = 0.2f;

        public static CityLookProfile Day()
        {
            return new CityLookProfile
            {
                Name = "day",
                WindowLitChance = 0.05f,
                EmissiveIntensity = 1.2f,
                UnlitGlassColor = new Color(0.35f, 0.5f, 0.65f),
                LampsOn = false,
                SkyColor = new Color(0.62f, 0.80f, 0.96f),
                AmbientColor = new Color(0.58f, 0.64f, 0.74f),
                SunColor = new Color(1f, 0.96f, 0.88f),
                SunIntensity = 1.4f,
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
                UnlitGlassColor = new Color(0.20f, 0.26f, 0.38f),
                LampsOn = true,
                LampIntensity = 2.5f,
                SkyColor = new Color(0.95f, 0.55f, 0.38f),
                AmbientColor = new Color(0.36f, 0.30f, 0.42f),
                SunColor = new Color(1f, 0.58f, 0.32f),
                SunIntensity = 1.1f,
                SunEuler = new Vector3(9f, 105f, 0f),
                BloomIntensity = 0.6f,
                BloomThreshold = 0.9f,
                Exposure = 0f,
                Vignette = 0.28f,
            };
        }

        public static CityLookProfile Night()
        {
            return new CityLookProfile
            {
                Name = "night",
                WindowLitChance = 0.65f,
                EmissiveIntensity = 2.1f,
                UnlitGlassColor = new Color(0.05f, 0.07f, 0.11f),
                LampsOn = true,
                LampIntensity = 4.5f,
                SkyColor = new Color(0.03f, 0.04f, 0.10f),
                AmbientColor = new Color(0.07f, 0.09f, 0.18f),
                SunColor = new Color(0.45f, 0.55f, 1f),
                SunIntensity = 0.35f,
                SunEuler = new Vector3(38f, 125f, 0f),
                BloomIntensity = 1.4f,
                BloomThreshold = 0.8f,
                Exposure = 0.2f,
                Saturation = 20f,
                Contrast = 15f,
                Vignette = 0.35f,
            };
        }
    }
}
