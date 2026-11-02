using System;
using System.IO;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    // Isolated edit-mode art renders. Uses the production builders and lighting, never a player save.
    public static class CityArtReview
    {
        private static Camera camera;
        private static DayNightLighting lighting;
        private static int frames;
        public static void Run()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var data=JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text);
                var world=new StreetWorldBuilder(CityLayout.FromData(data),12345);world.Build();
                camera=new GameObject("ArtReviewCamera").AddComponent<Camera>();
                camera.orthographic=true;camera.allowHDR=true;camera.allowMSAA=true;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.farClipPlane=2000;camera.nearClipPlane=.1f;
                var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;
                extra.antialiasing=AntialiasingMode.None;
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;
                var volume=new GameObject("PostVolume").AddComponent<Volume>();volume.priority=1;
                lighting=new DayNightLighting(camera,sun,volume,world.NightLights);lighting.Apply(720,false);
                frames=0;EditorApplication.update+=Render;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Render()
        {
            if(++frames<12)return;EditorApplication.update-=Render;
            try
            {
                string output="work/art-review";var args=Environment.GetCommandLineArgs();
                for(int i=0;i<args.Length-1;i++)if(args[i]=="-artReviewOutput")output=args[i+1];
                Directory.CreateDirectory(output);
                Shot(output,"city",new Vector3(28,0,264),0,40,205);
                Shot(output,"villa",new Vector3(53,0,445),30,38,37);
                Shot(output,"villa-estate",new Vector3(34.35f,0,516),30,35,18);
                Shot(output,"apartments",new Vector3(-75,0,333),25,40,57);
                Shot(output,"old-street",new Vector3(-148,0,23),30,38,34);
                Shot(output,"commercial",new Vector3(55,0,292),25,38,45);
                Shot(output,"wholesale",new Vector3(172,0,38),25,40,42);
                lighting.Apply(1260,false);
                Shot(output,"night",new Vector3(53,0,445),30,38,37);
                Debug.Log("ART_REVIEW_COMPLETE "+Path.GetFullPath(output));EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Shot(string folder,string name,Vector3 focus,float yaw,float pitch,float size)
        {
            camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            // Keep close shots within the production shadow distance.
            float distance=Mathf.Max(75,size*2.2f);
            camera.transform.position=focus-camera.transform.forward*distance;
            camera.orthographicSize=size;camera.aspect=16f/9;
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};
            camera.targetTexture=rt;
            camera.Render();camera.Render();
            var before=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            RenderTexture.active=before;camera.targetTexture=null;rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
