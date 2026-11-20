using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

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
        public void EventDialogFitsTheEntirePageBeforeEnablingScroll()
        {
            var screen = new GameObject("Screen", typeof(RectTransform));
            try
            {
                var viewport = (RectTransform)screen.transform;
                viewport.sizeDelta = new Vector2(1100, 740);
                var panel = Ui.Rect(viewport, "Dialog");
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
                panel.sizeDelta = new Vector2(900, 440);
                var fitter = panel.gameObject.AddComponent<WindowFit>();
                var content = Ui.Scroll(panel);
                var body = ((RectTransform)content.parent.parent).Anchor(0, 0, 1, 1, 0, 0, 0, 50);
                var scroll = body.GetComponent<ScrollRect>();
                var illustration = Ui.Size(Ui.Rect(content, "Illustration"), -1, 300);
                Ui.Size(Ui.Rect(content, "Text"), -1, 180);
                Ui.Size(Ui.Rect(content, "Actions"), -1, 44);

                fitter.FitContent(content, body, illustration);
                Assert.Greater(panel.rect.height, 440);
                Assert.IsFalse(scroll.vertical);
                Assert.IsFalse(scroll.verticalScrollbar.gameObject.activeSelf);

                viewport.sizeDelta = new Vector2(1100, 500);
                fitter.Fit();
                Assert.AreEqual(1, panel.localScale.x, .001);
                Assert.Less(LayoutUtility.GetPreferredHeight(illustration), 300);
                Assert.IsFalse(scroll.vertical);

                viewport.sizeDelta = new Vector2(1100, 380);
                fitter.Fit();
                Assert.IsTrue(scroll.vertical);
                Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);

                viewport.sizeDelta = new Vector2(1100, 740);
                fitter.Fit();
                Assert.AreEqual(300, LayoutUtility.GetPreferredHeight(illustration));
                Assert.IsFalse(scroll.vertical);
            }
            finally { UnityEngine.Object.DestroyImmediate(screen); }
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
                using (var session = GameSessionTests.Make())
                {
                    Assert.True(SaveStore.TrySave(session, 3, campaignId: "manual"));
                    string manual = File.ReadAllText(Path.Combine(folder, "save3.json"));
                    Assert.True(SaveStore.TrySave(session, 3, automatic: true, campaignId: "manual"));
                    Assert.True(SaveStore.TrySave(session, 8, automatic: true, campaignId: "manual"));
                    Assert.AreEqual(manual, File.ReadAllText(Path.Combine(folder, "save3.json")));
                    Assert.IsEmpty(Directory.GetFiles(folder, "*.auto.json"));
                    Assert.False(SaveStore.IsOccupied(8));
                    Assert.True(SaveStore.IsOccupied(3));
                    Assert.AreEqual(1, SaveStore.FirstEmptySlot);
                }
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
