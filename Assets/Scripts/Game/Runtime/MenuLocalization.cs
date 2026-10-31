using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public enum GameLanguage { Chinese, Japanese, English }

    /// <summary>Menu text is data-driven; language preference is independent of game saves.</summary>
    public static class MenuLocalization
    {
        public const string PreferenceKey = "pcss_language";
        [Serializable] public sealed class Entry { public string key, zh, ja, en; }
        [Serializable] public sealed class Catalog { public Entry[] entries; }
        private static Dictionary<string, Entry> _entries;
        public static GameLanguage Current
        {
            get
            {
                string code = PlayerPrefs.GetString(PreferenceKey, "zh-CN");
                return code == "ja" ? GameLanguage.Japanese : code == "en" ? GameLanguage.English : GameLanguage.Chinese;
            }
            set
            {
                PlayerPrefs.SetString(PreferenceKey, value == GameLanguage.Japanese ? "ja" : value == GameLanguage.English ? "en" : "zh-CN");
                PlayerPrefs.Save();
            }
        }
        public static IEnumerable<Entry> Entries { get { EnsureLoaded(); return _entries.Values; } }
        private static void EnsureLoaded()
        {
            if (_entries != null) return;
            var asset = Resources.Load<TextAsset>("Localization/menu");
            if (asset == null) throw new InvalidOperationException("Missing menu localization catalog");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            var entries = new Dictionary<string, Entry>();
            foreach (var entry in catalog.entries) entries.Add(entry.key, entry);
            _entries = entries;
        }
        public static string Get(string key) => Get(key, Current);
        public static string Get(string key, GameLanguage language)
        {
            EnsureLoaded();
            if (!_entries.TryGetValue(key, out var entry)) throw new ArgumentException("Unknown menu text: " + key);
            return language == GameLanguage.Japanese ? entry.ja : language == GameLanguage.English ? entry.en : entry.zh;
        }
        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
