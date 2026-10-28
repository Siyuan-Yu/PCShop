using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    /// <summary>
    /// 创建并启用项目的 URP 配置资产（渲染器 + 管线资产），并写入图形设置与所有质量档位。
    /// 可重复执行：资产已存在则复用。
    /// </summary>
    public static class UrpSetup
    {
        private const string Folder = "Assets/Settings";
        private const string RendererPath = Folder + "/PCShopSell_Renderer.asset";
        private const string PipelinePath = Folder + "/PCShopSell_URP.asset";
        private const string UrpPackagePath = "Packages/com.unity.render-pipelines.universal";

        [MenuItem("PCShopSell/Setup URP")]
        public static void SetupFromMenu()
        {
            Ensure();
        }

        public static UniversalRenderPipelineAsset Ensure()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UrpPackagePath);
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssets();
            }

            EnsureSsao(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            Configure(pipeline);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(original, false);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        private static void EnsureSsao(UniversalRendererData renderer)
        {
            foreach (var f in renderer.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return;

            // 该类在 URP 14 中是 internal，只能用反射创建。
            var ssaoType = typeof(UniversalRendererData).Assembly.GetType(
                "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (ssaoType == null) { Debug.LogWarning("SSAO type not found"); return; }
            var ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            ssao.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(ssao, renderer);

            var ss = new SerializedObject(ssao);
            SetFloat(ss, "m_Settings.Intensity", 1.6f);
            SetFloat(ss, "m_Settings.Radius", 0.7f);
            SetFloat(ss, "m_Settings.DirectLightingStrength", 0.25f);
            ss.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        private static void Configure(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            SetBool(so, "m_ReflectionProbeBlending", true);
            SetBool(so, "m_ReflectionProbeBoxProjection", true);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 4);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetFloat(so, "m_ShadowDistance", 160f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("URP property not found: " + name); return; }
            p.intValue = v;
        }

        private static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("URP property not found: " + name); return; }
            p.boolValue = v;
        }

        private static void SetFloat(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("URP property not found: " + name); return; }
            p.floatValue = v;
        }
    }
}
