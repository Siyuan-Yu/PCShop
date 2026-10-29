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
    }

    /// <summary>
    /// 程序合成的小音效（没有任何音频资源）：点按钮、收钱、门铃、出错、完成。
    /// 音量与静音存在 PlayerPrefs，菜单里可调。批处理（自动测试）下不出声。
    /// </summary>
    public static class SoundFx
    {
        private const string MuteKey = "pcss_sfx_mute";
        private const string VolumeKey = "pcss_sfx_volume";
        private const int Rate = 44100;

        private static AudioSource _source;
        private static readonly AudioClip[] Clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
        private static float _lastClick;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set => PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
        }

        /// <summary>0 ~ 1，默认 0.6。</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.6f));
            set => PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        }

        public static void Play(Sfx sfx)
        {
            if (Muted || Application.isBatchMode || Volume <= 0.001f) return;
            if (sfx == Sfx.Click)
            {
                if (Time.unscaledTime - _lastClick < 0.04f) return;
                _lastClick = Time.unscaledTime;
            }
            if (_source == null)
            {
                var go = new GameObject("SoundFx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            AudioClip clip = Clips[(int)sfx];
            if (clip == null) clip = Clips[(int)sfx] = Make(sfx);
            _source.PlayOneShot(clip, Volume * (sfx == Sfx.Click ? 0.5f : 1f));
        }

        private static AudioClip Make(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click: return Build("click", 0.05f, t => Tone(1500, t, 60) * 0.5f + Tone(900, t, 70) * 0.5f);
                case Sfx.Coin:
                    return Build("coin", 0.42f, t => t < 0.09f ? Tone(1319, t, 10) : Tone(1760, t - 0.09f, 7) + 0.3f * Tone(3520, t - 0.09f, 12));
                case Sfx.Bell:
                    return Build("bell", 0.9f, t => (Tone(880, t, 4) + 0.5f * Tone(1760, t, 6) + 0.25f * Tone(2640, t, 9)) * 0.7f);
                case Sfx.Error:
                    return Build("error", 0.22f, t => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 170 * t)) * Mathf.Exp(-9 * t) * 0.35f);
                default:
                    return Build("success", 0.7f, t =>
                    {
                        float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f };
                        float v = 0;
                        for (int i = 0; i < f.Length; i++) if (t >= i * 0.1f) v += Tone(f[i], t - i * 0.1f, 5) * 0.5f;
                        return v;
                    });
            }
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
