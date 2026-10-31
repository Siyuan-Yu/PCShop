using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class MenuLocalizationTests
    {
        [Test]
        public void MenuWindowFit_RespondsToSmallerAndLargerViewport()
        {
            var parent = new GameObject("Viewport", typeof(RectTransform));
            try
            {
                var viewport = (RectTransform)parent.transform;
                viewport.sizeDelta = new Vector2(1920, 1080);
                var panel = new GameObject("Menu", typeof(RectTransform));
                panel.transform.SetParent(viewport, false);
                ((RectTransform)panel.transform).sizeDelta = new Vector2(1320, 850);
                var fitter = panel.AddComponent<PCShopSell.Game.UI.WindowFit>();
                fitter.Fit();
                Assert.AreEqual(1, panel.transform.localScale.x, .001);
                viewport.sizeDelta = new Vector2(800, 600);
                fitter.Fit();
                Assert.Less(panel.transform.localScale.x, 1);
                Assert.LessOrEqual(1320 * panel.transform.localScale.x, 768.01);
                viewport.sizeDelta = new Vector2(1920, 1080);
                fitter.Fit();
                Assert.AreEqual(1, panel.transform.localScale.x, .001);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        [Test]
        public void MenuCatalog_HasCompleteTranslationsAndMatchingFormatArguments()
        {
            var entries = MenuLocalization.Entries.ToArray();
            Assert.Greater(entries.Length, 50);
            foreach (var entry in entries)
            {
                foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                {
                    string text = MenuLocalization.Get(entry.key, language);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), entry.key + " / " + language);
                    Assert.DoesNotThrow(() => string.Format(text, "123"), entry.key + " / " + language);
                    Assert.AreEqual(entry.zh.Contains("{0}"), text.Contains("{0}"), entry.key);
                }
            }
        }

        [Test]
        public void LanguagePreference_UsesStableCodesAndFallsBackForInvalidValue()
        {
            bool existed = PlayerPrefs.HasKey(MenuLocalization.PreferenceKey);
            string previous = PlayerPrefs.GetString(MenuLocalization.PreferenceKey);
            try
            {
                foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                {
                    MenuLocalization.Current = language;
                    Assert.AreEqual(language, MenuLocalization.Current);
                }
                PlayerPrefs.SetString(MenuLocalization.PreferenceKey, "unknown");
                Assert.AreEqual(GameLanguage.Chinese, MenuLocalization.Current);
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(MenuLocalization.PreferenceKey, previous);
                else PlayerPrefs.DeleteKey(MenuLocalization.PreferenceKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void LoadSlotInspection_DistinguishesSlotsAndBackupWithoutLoadingGame()
        {
            string previous = SaveStore.DirectoryOverride;
            string folder = Path.Combine(Path.GetTempPath(), "pcshop-menu-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            SaveStore.DirectoryOverride = folder;
            try
            {
                Assert.False(SaveStore.InspectSlot(false).Exists);
                using (var session = GameSessionTests.Make()) SaveStore.Save(session, true);
                Assert.False(SaveStore.InspectSlot(false).Exists);
                Assert.True(SaveStore.InspectSlot(true).Exists);
                Assert.IsNotEmpty(SaveStore.InspectSlot(true).SavedAt);
                File.Move(SaveStore.AutoPath, SaveStore.AutoPath + ".bak");
                Assert.True(SaveStore.InspectSlot(true).Exists);
                Assert.IsNotEmpty(SaveStore.InspectSlot(true).SavedAt);
            }
            finally
            {
                SaveStore.Delete();
                SaveStore.DirectoryOverride = previous;
                Directory.Delete(folder);
            }
        }
    }
}
