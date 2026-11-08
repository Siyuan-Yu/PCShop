using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    // Renders the real production world/HUD in an isolated unsaved editor scene.
    public static class IndustrialArtReview
    {
        private static GameController game;private static string folder;private static int frames;
        public static void Run()
        {
            try
            {
                var args=Environment.GetCommandLineArgs();folder="work/art-review/industrial/pass1";
                for(int i=0;i<args.Length-1;i++)if(args[i]=="-artReviewOutput")folder=args[i+1];
                Directory.CreateDirectory(folder);SaveStore.DirectoryOverride=Path.GetFullPath("work/art-review-saves");
                ShaderUtil.allowAsyncCompilation=false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                game=new GameObject("Art review — isolated session").AddComponent<GameController>();
                game.Bootstrap(false,12345);game.enabled=false;game.Session.EventsEnabled=false;game.Ui.QuestPopups=false;
                game.Ui.CloseAllWindows();game.Session.Time.SetPaused(true);game.Session.Leave();game.Step(0);
                frames=0;EditorApplication.update+=Render;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Render()
        {
            if(++frames<12)return;EditorApplication.update-=Render;
            try
            {
                var cam=game.Cam;cam.farClipPlane=5000;
                var shop=game.World.Places[game.City.PlayerShop().Id];
                Vector3 center=shop.BuildingBounds().center;
                foreach(var canvas in game.Ui.Canvases)canvas.gameObject.SetActive(false);
                Time(720);Shot("street-day",center,shop.Place.Heading+25,36,23);
                Time(1100);Shot("street-dusk",center,shop.Place.Heading+25,32,23);
                Time(1260);Shot("street-night",center,shop.Place.Heading+25,36,23);
                Time(720);
                foreach(string model in new[]{"warehouse","oldhome","villa","luxury_apartment"})
                {
                    var place=game.World.Places.Values.FirstOrDefault(p=>p.Place.Model==model);
                    if(place!=null)Shot(model,place.BuildingBounds().center,place.Place.Heading+28,35,model=="villa"?25:30);
                }
                Shot("city-layout",new Vector3(-20,0,160),0,55,570);
                foreach(var canvas in game.Ui.Canvases)canvas.gameObject.SetActive(true);
                Time(1100);game.Step(0);Shot("street-hud",center,shop.Place.Heading+25,36,23,true);
                var s=game.Session;s.DebugRentStarterShop(computerScenario: true);game.Ui.CloseAllWindows();s.Leave();s.ArriveAtNode(s.PlayerShopPlace.NodeId);s.Clock.Restore(s.Clock.Today,600);s.Enter(s.PlayerShopPlace);
                game.Step(0);game.Ui.OnEnteredPlace(s.CurrentPlace);game.Interior.StandAtCounter();game.Step(0);game.Ui.OpenDock();
                Vector3 inside=ShopInteriorView.Origin+new Vector3(game.Interior.Width*.5f,1,game.Interior.Depth*.5f);
                Shot("shop-interior-ui",inside,35,38,7,true);
                var guest=s.CityPersonState("zhou");guest.LocationPlaceId=s.CurrentPlace.Id;guest.Route=null;
                game.Ui.OpenPersonDialogue("zhou");Shot("dialogue-ui",inside,35,38,7,true);
                File.WriteAllText(Path.Combine(folder,"verification.txt"),"Production builders and real UI; unchanged layout data. Places="+game.City.Places.Count()+"\n"+DateTime.UtcNow.ToString("O"));
                Debug.Log("INDUSTRIAL_ART_REVIEW_COMPLETE "+Path.GetFullPath(folder));EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Time(int minute)
        {
            game.Session.Clock.Restore(new GameDate(0),minute);
            var lighting=(DayNightLighting)typeof(GameController).GetField("_lighting",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            lighting.Apply(minute,false);
        }
        private static void Shot(string name,Vector3 focus,double yaw,float pitch,float size,bool hud=false)
        {
            var cam=game.Cam;cam.orthographic=true;cam.orthographicSize=size;cam.aspect=16f/9;
            cam.transform.rotation=Quaternion.Euler(pitch,(float)yaw,0);cam.transform.position=focus-cam.transform.forward*Mathf.Max(85,size*2.4f);
            if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)pipeline.shadowDistance=Mathf.Max(260,size*3.4f);
            cam.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.None;
            if(hud)
            {
                game.Ui.Update(0);
                foreach(var c in game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;}
            }
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=4};cam.targetTexture=rt;
            Canvas.ForceUpdateCanvases();cam.Render();cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());RenderTexture.active=previous;cam.targetTexture=null;rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
