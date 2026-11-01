using System.Collections.Generic;
using NUnit.Framework;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class DisplaySettingsTests
    {
        private readonly Dictionary<string,int?> _old=new Dictionary<string,int?>();
        [SetUp] public void SetUp()
        {
            GameDisplaySettings.CancelPreview();
            foreach(var key in new[]{GameDisplaySettings.WidthKey,GameDisplaySettings.HeightKey,GameDisplaySettings.ModeKey})
            {_old[key]=PlayerPrefs.HasKey(key)?(int?)PlayerPrefs.GetInt(key):null;PlayerPrefs.DeleteKey(key);}
        }
        [TearDown] public void TearDown()
        {
            GameDisplaySettings.CancelPreview();
            foreach(var pair in _old){if(pair.Value.HasValue)PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(pair.Key);}
            PlayerPrefs.Save();_old.Clear();
        }
        [Test] public void DefaultIs1080pWindowed_AndInvalidPreferencesFallBack()
        {
            Assert.AreEqual(1920,GameDisplaySettings.Saved().Width);
            Assert.AreEqual(1080,GameDisplaySettings.Saved().Height);
            Assert.AreEqual(FullScreenMode.Windowed,GameDisplaySettings.Saved().Mode);
            PlayerPrefs.SetInt(GameDisplaySettings.WidthKey,-1);
            Assert.AreEqual(1920,GameDisplaySettings.Saved().Width);
            Assert.IsFalse(GameDisplaySettings.BeginPreview(new GameDisplayConfig{Width=0},null));
            Assert.IsFalse(GameDisplaySettings.PreviewActive);
        }
        [Test] public void PreviewAndTimeoutNeverPersist_ConfirmationIsOnceOnly()
        {
            int callbacks=0;bool kept=false;
            var requested=new GameDisplayConfig{Width=1600,Height=900};
            Assert.IsTrue(GameDisplaySettings.BeginPreview(requested,v=>{callbacks++;kept=v;}));
            Assert.IsFalse(PlayerPrefs.HasKey(GameDisplaySettings.WidthKey));
            GameDisplaySettings.TickPreview(GameDisplaySettings.PreviewDeadline+1);
            Assert.AreEqual(1,callbacks);Assert.IsFalse(kept);
            Assert.IsFalse(PlayerPrefs.HasKey(GameDisplaySettings.WidthKey));
            GameDisplaySettings.BeginPreview(requested,v=>{callbacks++;kept=v;});
            GameDisplaySettings.ConfirmPreview();GameDisplaySettings.ConfirmPreview();
            Assert.AreEqual(2,callbacks);Assert.IsTrue(kept);
            Assert.AreEqual(1600,GameDisplaySettings.Saved().Width);
            GameDisplaySettings.BeginPreview(new GameDisplayConfig(),v=>callbacks++);
            GameDisplaySettings.CancelPreview();GameDisplaySettings.CancelPreview();
            Assert.AreEqual(3,callbacks);Assert.AreEqual(1600,GameDisplaySettings.Saved().Width);
        }
    }
}
