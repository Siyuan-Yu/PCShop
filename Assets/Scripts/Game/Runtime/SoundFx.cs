using System;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public enum Sfx
    {
        Click,
        Coin,
        Bell,
        Error,
        Success,
        Purchase,
        Arrival,
        Notification,
        ShopGuestLeaves,
        Cooking,
        Dining,
        CatMeow,
        DogBark,
        PhoneRing,
    }

    /// <summary>
    /// 程序合成并缓存短音效。
    /// 音量与静音存在 PlayerPrefs，菜单里可调。批处理（自动测试）下不出声。
    /// </summary>
    public static class SoundFx
    {
        private const string MuteKey = "pcss_sfx_mute";
        private const string VolumeKey = "pcss_sfx_volume";
        private const int Rate = 44100;

        private static AudioSource _source;
        private static readonly AudioClip[] Clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
        private static readonly float[] NextPlayTime = new float[Clips.Length];

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                if (value && _source != null) _source.Stop();
            }
        }

        /// <summary>0 ~ 1，默认 0.6。</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.6f));
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume);
                if (_source != null) _source.volume = volume;
            }
        }

        public static void Play(Sfx sfx)
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            int index = (int)sfx;
            float now = Time.unscaledTime;
            if (now < NextPlayTime[index] || Muted || Volume <= 0.001f) return;
            var (gain, cooldown) = sfx switch
            {
                Sfx.Click => (0.32f, 0.065f),
                Sfx.Coin => (0.78f, 0.28f),
                Sfx.Bell => (0.56f, 0.8f),
                Sfx.Error => (0.68f, 0.25f),
                Sfx.Arrival => (0.62f, 0.35f),
                Sfx.Notification => (0.60f, 0.30f),
                Sfx.PhoneRing => (0.62f, 0.85f),
                Sfx.ShopGuestLeaves => (0.55f, 0.85f),
                Sfx.Cooking => (0.45f, 1.7f),
                Sfx.Dining => (0.38f, 3.2f),
                Sfx.CatMeow => (0.46f, 4f),
                Sfx.DogBark => (0.40f, 4f),
                _ => (0.80f, 0.20f),
            };
            NextPlayTime[index] = now + cooldown;

            if (_source == null)
            {
                var go = new GameObject("SoundFx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                _source.volume = Volume;
            }
            AudioClip clip = Clips[index];
            if (clip == null) clip = Clips[index] = Make(sfx);
            _source.PlayOneShot(clip, gain);
        }

        private static AudioClip Make(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click:
                    return Build("click", 0.045f, t => 0.35f * Tone(1040, t, 70) + 0.65f * Tone(560, t, 85));
                case Sfx.Coin:
                    return Build("coin", 0.25f, t => 0.75f * Tone(1319, t, 23) + Chime(1760, t, 0.065f, 22));
                case Sfx.Bell:
                    return Build("bell", 0.50f, t => 0.75f * Chime(880, t, 0, 10) + 0.55f * Chime(659, t, 0.11f, 12));
                case Sfx.Error:
                    return Build("error", 0.17f, t => 0.6f * Tone(311, t, 22) + 0.6f * Chime(220, t, 0.055f, 26));
                case Sfx.Purchase:
                    return Build("purchase", 0.24f, t => 0.75f * Chime(523, t, 0, 22) + 0.75f * Chime(698, t, 0.068f, 19));
                case Sfx.Arrival:
                    return Build("arrival", 0.28f, t => 0.65f * Chime(784, t, 0, 17) + 0.55f * Chime(659, t, 0.085f, 16));
                case Sfx.Notification:
                    return Build("notification", 0.19f, t => 0.7f * Chime(880, t, 0, 20) + 0.6f * Chime(1047, t, 0.065f, 22));
                case Sfx.PhoneRing:
                    return Build("phone-ring", .72f, t => .58f * Chime(740, t, 0, 11)
                        + .48f * Chime(988, t, .115f, 12) + .48f * Chime(740, t, .34f, 14)
                        + .40f * Chime(988, t, .455f, 15));
                case Sfx.ShopGuestLeaves:
                    return Build("shop-door-out", 0.33f, t => 0.62f * Chime(587, t, 0, 15)
                        + 0.30f * Chime(440, t, 0.09f, 17) + 0.52f * Chime(110, t, 0.12f, 35));
                case Sfx.Cooking:
                    return Build("pan-sizzle", 0.85f, t =>
                    {
                        float sizzle = Noise(t, Rate) * (0.34f + 0.14f * Mathf.Sin(t * 38f)) + 0.10f * Noise(t, 8300);
                        float envelope = Mathf.Min(1f, t * 16f) * Mathf.Clamp01((.85f - t) * 5f);
                        return sizzle * envelope + 0.28f * Chime(1020, t, .14f, 47) + 0.20f * Chime(760, t, .58f, 42);
                    });
                case Sfx.Dining:
                    return Build("dining", 0.65f, t => 0.40f * Chime(1680, t, 0, 54) + 0.25f * Chime(2180, t, 0.09f, 58)
                        + 0.35f * Bite(t, .19f, .14f) + 0.29f * Bite(t, .39f, .15f));
                case Sfx.CatMeow:
                    return Build("cat-meow", .78f, t =>
                    {
                        const float length = .78f;
                        float u = t / length;
                        // 连续滑音与谐波形成短促的“喵呜”，首尾包络避免爆音。
                        float phase = 2 * Mathf.PI * (420 * t + 180 * length / Mathf.PI * (1 - Mathf.Cos(Mathf.PI * u)) - 60 * t * t / length);
                        float vowel = Mathf.Sin(phase) * .52f + Mathf.Sin(phase * 2) * (.36f - .16f * u)
                            + Mathf.Sin(phase * 3) * .16f + Mathf.Sin(phase * 5) * .06f;
                        return Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * u)), .8f) * vowel;
                    });
                case Sfx.DogBark:
                    return Build("dog-bark", .52f, t => Bark(t, 0) + .72f * Bark(t, .27f));
                default:
                    return Build("success", 0.39f, t => 0.7f * Chime(523, t, 0, 15) + 0.7f * Chime(659, t, 0.065f, 15)
                        + 0.7f * Chime(784, t, 0.13f, 15) + 0.65f * Chime(1047, t, 0.196f, 13));
            }
        }

        private static float Bark(float t, float start)
        {
            float elapsed = t - start;
            if (elapsed < 0 || elapsed >= .22f) return 0;
            float phase = 2 * Mathf.PI * (190 * elapsed - 160 * elapsed * elapsed);
            float voice = Mathf.Sin(phase) * .38f + Mathf.Sin(phase * 2) * .22f
                + Mathf.Sin(phase * 3) * .18f + Noise(t, 3800) * .25f;
            return Mathf.Min(1, elapsed * 100) * Mathf.Exp(-elapsed * 13) * voice;
        }

        private static float Bite(float t, float start, float length)
        {
            float elapsed = t - start;
            if (elapsed < 0 || elapsed >= length) return 0;
            return Mathf.Sin(Mathf.PI * elapsed / length) * (0.58f * Noise(t, 4500) + 0.42f * Noise(t, 1800));
        }

        private static float Noise(float t, int samplesPerSecond)
        {
            uint bits = (uint)(t * samplesPerSecond);
            bits = unchecked((bits ^ (bits >> 16)) * 0x7FEB352DU);
            bits = unchecked((bits ^ (bits >> 15)) * 0x846CA68BU);
            return ((bits ^ (bits >> 16)) & 0xFFFFU) / 32767.5f - 1f;
        }

        private static float Chime(float freq, float t, float start, float decay)
        {
            float elapsed = t - start;
            return elapsed < 0 ? 0 : Tone(freq, elapsed, decay) + 0.20f * Tone(2 * freq, elapsed, decay * 1.5f);
        }

        /// <summary>带指数衰减的正弦。</summary>
        private static float Tone(float freq, float t, float decay) => Mathf.Sin(2 * Mathf.PI * freq * t) * Mathf.Exp(-decay * t);

        private static AudioClip Build(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float fadeIn = Mathf.Min(1f, i / 80f);                    // 开头淡入，避免爆音
                float fadeOut = Mathf.Min(1f, (n - i) / 300f);            // 结尾淡出
                data[i] = Mathf.Clamp(wave(t) * 0.5f * fadeIn * fadeOut, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
