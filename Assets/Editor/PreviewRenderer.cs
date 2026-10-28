using System;
using System.IO;
using PCShopSell.Game.Procedural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    /// <summary>
    /// 在编辑器/批处理模式下搭建程序生成的街区，并用 URP 渲染出预览图。
    /// 用于在没有任何美术素材的情况下，估计"代码生成 + 打光"能到什么效果。
    /// </summary>
    public static class PreviewRenderer
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("PCShopSell/Render Preview Images")]
        public static void RenderFromMenu()
        {
            Run();
        }

        public static void RenderFromBatch()
        {
            int code = 0;
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        private static void Run()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(outDir);

            // 批处理里着色器默认异步编译，第一帧会渲染成洋红色，所以先关掉异步。
            ShaderUtil.allowAsyncCompilation = false;
            var urp = UrpSetup.Ensure();

            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                Debug.Log("DIAG pipeline=" + GraphicsSettings.currentRenderPipeline +
                          " lit=" + (litShader != null ? litShader.name + " supported=" + litShader.isSupported : "NULL") +
                          " api=" + SystemInfo.graphicsDeviceType);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGo = new GameObject("PreviewCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 500f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.allowHDR = true;
                cam.allowMSAA = true;
                var extra = cam.GetUniversalAdditionalCameraData();
                extra.renderPostProcessing = true;

                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.95f;

                var volGo = new GameObject("PostVolume");
                var volume = volGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1;

                var looks = new[] { CityLookProfile.Day(), CityLookProfile.Dusk(), CityLookProfile.Night() };
                foreach (var look in looks)
                {
                    var builder = new ProceduralCityBuilder(look, 20261028);
                    var city = builder.Build();

                    ApplyLook(look, cam, sun, volume);
                    string probePath = BakeProbe(city, look);

                    // 全景
                    PlaceCamera(cam, new Vector3(0f, 2f, 0f), 34f);
                    Save(cam, Path.Combine(outDir, $"city_{look.Name}_wide.png"));

                    // 近景：电脑城门口
                    Vector3 hero = builder.HeroFrontCenter;
                    PlaceCamera(cam, new Vector3(hero.x + 1f, 4f, hero.z - 2f), 13f);
                    Save(cam, Path.Combine(outDir, $"city_{look.Name}_closeup.png"));

                    UnityEngine.Object.DestroyImmediate(city);
                    if (probePath != null) AssetDatabase.DeleteAsset(probePath);
                }
            }
            Debug.Log("Preview images written to " + outDir);
        }

        /// <summary>烘焙一个覆盖整座城市的反射探针，让湿润路面和玻璃反射出霓虹。</summary>
        private static string BakeProbe(GameObject city, CityLookProfile look)
        {
            foreach (var t in city.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ReflectionProbeStatic);

            var go = new GameObject("CityProbe");
            go.transform.SetParent(city.transform, false);
            go.transform.position = new Vector3(0f, 6f, 0f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = new Vector3(190f, 60f, 190f);
            probe.center = Vector3.zero;
            probe.intensity = 1f;
            probe.importance = 1;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = look.SkyColor;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 400f;

            string path = "Assets/Settings/ProbeTemp_" + look.Name + ".exr";
            bool ok = Lightmapping.BakeReflectionProbe(probe, path);
            Debug.Log("DIAG probe bake " + look.Name + " ok=" + ok);
            if (!ok) return null;

            AssetDatabase.ImportAsset(path);
            probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            return path;
        }

        private static void ApplyLook(CityLookProfile look, Camera cam, Light sun, Volume volume)
        {
            cam.backgroundColor = look.SkyColor;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = look.AmbientColor;
            RenderSettings.skybox = null;
            RenderSettings.fog = look.Fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = look.FogColor;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;

            sun.color = look.SunColor;
            sun.intensity = look.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(look.SunEuler);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.threshold.Override(look.BloomThreshold);
            bloom.scatter.Override(0.7f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(look.Exposure);
            grade.saturation.Override(look.Saturation);
            grade.contrast.Override(look.Contrast);

            if (look.ChromaticAberration > 0f)
            {
                var ca = profile.Add<ChromaticAberration>(true);
                ca.intensity.Override(look.ChromaticAberration);
            }

            if (look.FilmGrain > 0f)
            {
                var grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(look.FilmGrain);
            }

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(look.ShadowTint);
            smh.highlights.Override(look.HighlightTint);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(look.Vignette);
            vig.smoothness.Override(0.5f);

            volume.sharedProfile = profile;
        }

        private static void PlaceCamera(Camera cam, Vector3 target, float orthoSize)
        {
            // 固定斜俯视：俯角 35°，偏航 45°
            var rot = Quaternion.Euler(35f, 45f, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = target - rot * Vector3.forward * 150f;
            cam.orthographicSize = orthoSize;
        }

        private static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            cam.targetTexture = rt;
            cam.Render(); // 预热：触发着色器变体编译
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("Saved " + path);
        }
    }
}
