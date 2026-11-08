using System;
using System.IO;
using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    // Isolated edit-mode art renders. Uses the production builders and lighting, never a player save.
    public static partial class CityArtReview
    {
        private static Camera camera;
        private static DayNightLighting lighting;
        private static int frames;
        private static bool sideViews;
        private static CityLayout reviewLayout;
        public static void Run()
        {
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var data=JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text);
                var layout=CityLayout.FromData(data);
                reviewLayout=layout;
                if(data.lakeCity&&data.layoutVersion>=13)
                {
                    var full=Pathfinder.FindPath(layout.Map,data.lakeSouthNode,data.lakeNorthNode);
                    int expectedMinutes=(int)Math.Ceiling(1440*layout.ExpansionScale-1e-7);
                    if(!full.Found||CityTravelTiming.Minutes(full.Distance,Vehicles.Walk)!=expectedMinutes)
                        throw new InvalidOperationException("Full lake route must match authored expansion: "+expectedMinutes+" game minutes.");
                    if(layout.FirstOfKind(PlaceKind.Home).Id!="home")throw new InvalidOperationException("Starter home changed.");
                    foreach(var pair in new[]{new[]{"home","player_shop"},new[]{"player_shop","stall_cpu"},new[]{"home","stall_cpu"}})
                    {
                        var path=Pathfinder.FindPath(layout.Map,layout.GetPlace(pair[0]).NodeId,layout.GetPlace(pair[1]).NodeId);
                        if(!path.Found)throw new InvalidOperationException("Disconnected daily route");
                        Debug.Log("ART_ROUTE "+pair[0]+" -> "+pair[1]+" walk="+CityTravelTiming.Minutes(path.Distance,Vehicles.Walk)+" min; ebike="+CityTravelTiming.Minutes(path.Distance,Vehicles.EBike)+" min (road nodes)");
                    }
                    Debug.Log("ART_LAYOUT_VALIDATED full walk="+expectedMinutes+" min; start home=home");
                }
                var world=new StreetWorldBuilder(layout,12345);world.Build();
                camera=new GameObject("ArtReviewCamera").AddComponent<Camera>();
                camera.orthographic=true;camera.allowHDR=true;camera.allowMSAA=true;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.farClipPlane=2000;camera.nearClipPlane=.1f;
                var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;
                extra.antialiasing=AntialiasingMode.None;
                var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;
                var volume=new GameObject("PostVolume").AddComponent<Volume>();volume.priority=1;
                lighting=new DayNightLighting(camera,sun,volume,world.NightLights);lighting.Apply(720,false);
                frames=0;EditorApplication.update+=Render;
                // Overview shadows must reach the city at the distant review camera position.
                if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)pipeline.shadowDistance=1800;
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
                if(Array.IndexOf(args,"-roadViews")>=0)
                {
                    camera.farClipPlane=5000;
                    Shot(output,"city",new Vector3(0,0,155),0,72,690);
                    Shot(output,"outskirts",new Vector3(-490,0,-415),0,78,125);
                    Shot(output,"community",new Vector3(340,0,-335),0,78,130);
                    Shot(output,"park",new Vector3(650,0,150),0,78,150);
                    Shot(output,"garden",new Vector3(15,0,705),0,78,140);
                    Debug.Log("ROAD_REVIEW_COMPLETE "+Path.GetFullPath(output));EditorApplication.Exit(0);return;
                }
                if(Array.IndexOf(args,"-racingViews")>=0)
                {
                    RacingOverview(output);
                    Debug.Log("RACING_MAP_REVIEW_COMPLETE "+Path.GetFullPath(output));EditorApplication.Exit(0);return;
                }
                if(Array.IndexOf(args,"-sideViews")>=0)
                {
                    sideViews=true;camera.farClipPlane=5000;
                    if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)pipeline.shadowDistance=3500;
                    Shot(output,"city-side",new Vector3(-20,0,160),0,35,490);
                    Shot(output,"lakeside-side",new Vector3(650,0,375),-40,25,135);
                    Shot(output,"garden-side",new Vector3(-110,0,735),20,25,135);
                    Shot(output,"outskirts",new Vector3(-525,0,-420),0,52,145);
                    Shot(output,"community",new Vector3(380,0,-310),0,52,145);
                    Shot(output,"reference-angle",new Vector3(-20,0,160),0,30,570,true);
                    Debug.Log("ART_REVIEW_COMPLETE "+Path.GetFullPath(output));EditorApplication.Exit(0);return;
                }
                Shot(output,"city",new Vector3(0,0,100),0,35,365);
                Shot(output,"lake-bridges",new Vector3(30,0,60),0,45,250);
                Shot(output,"villa",new Vector3(23,0,450),30,38,37);
                Shot(output,"villa-estate",new Vector3(4.35f,0,521),30,35,18);
                Shot(output,"apartments",new Vector3(397,0,55),-65,40,57);
                Shot(output,"old-street",new Vector3(-388,0,-297),30,38,34);
                Shot(output,"commercial",new Vector3(438,0,185),-65,38,45);
                Shot(output,"wholesale",new Vector3(-68,0,-282),25,40,42);
                lighting.Apply(1260,false);
                Shot(output,"night",new Vector3(30,0,60),0,45,250);
                Debug.Log("ART_REVIEW_COMPLETE "+Path.GetFullPath(output));EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Shot(string folder,string name,Vector3 focus,float yaw,float pitch,float size,bool perspective=false)
        {
            float scale=(float)(reviewLayout?.ExpansionScale??1);
            focus=new Vector3(focus.x*scale,focus.y,focus.z*scale);size*=scale;
            camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            // Keep close shots within the production shadow distance.
            float distance=Mathf.Max(75,size*2.2f);
            if(sideViews)distance=Mathf.Max(1600,size*3.5f);
            camera.orthographic=!perspective;
            if(perspective){camera.fieldOfView=38;distance=size/Mathf.Tan(19*Mathf.Deg2Rad);}
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
