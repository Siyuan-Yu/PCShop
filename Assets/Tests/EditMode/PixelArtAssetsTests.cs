using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class PixelArtAssetsTests
    {
        [TestCase("PixelTown")]
        [TestCase("PixelFoliage")]
        [TestCase("PixelPortrait")]
        public void PresentationShadersArePackagedAndCompile(string name)
        {
            var shader=Resources.Load<Shader>("Shaders/"+name);
            Assert.NotNull(shader,"Runtime Resources lookup must work in the player too.");
            Assert.False(ShaderUtil.ShaderHasError(shader));
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.True(shader.isSupported);
        }
        [Test]
        public void ProductionRendererIncludesExactlyOnePixelFeature()
        {
            var assets=AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PCShopSell_Renderer.asset");
            var features=assets.Where(a=>a!=null&&a.GetType().Name=="PixelTownRendererFeature").ToArray();
            Assert.AreEqual(1,features.Length);
            var serialized=new SerializedObject(features[0]);
            Assert.True(serialized.FindProperty("m_Active").boolValue);
        }
        [Test]
        public void PortraitArtKeepsStencilSupportForExistingScrollViews()
        {
            var material=Ui.PixelPortraitMaterial;
            Assert.NotNull(material);Assert.True(material.HasProperty("_MainTex"));
            Assert.True(material.HasProperty("_StencilComp"));Assert.True(material.HasProperty("_StencilReadMask"));
        }
    }
}
