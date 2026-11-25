using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
[InitializeOnLoad]public static class TownMatchCamera{
 static TownMatchCamera(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TownMatchCamera",false))EditorApplication.delayCall+=Capture;};}
 public static void Run(){ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("TownMatchCamera",true);EditorApplication.EnterPlaymode();}
 static void Capture(){try{SessionState.SetBool("TownMatchCamera",false);SaveStore.DirectoryOverride=Path.GetFullPath("quarter-review-save");
 var game=new GameObject("Review").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;var s=game.Session;s.EventsEnabled=false;game.Ui.QuestPopups=false;game.Ui.CloseAllWindows();s.DebugRentStarterShop(computerScenario:true);s.Leave();s.Clock.Restore(s.Clock.Today,630);game.Step(0);foreach(var c in game.Ui.Canvases)c.gameObject.SetActive(false);game.World.Root.SetActive(true);
 var starter=game.World.Places["life_unit_s_oldhomes_2"];s.ArriveAtNode(starter.Place.NodeId);game.Step(.03f);game.SnapCamera();Debug.Log("Outdoor capture: active="+game.World.Root.activeSelf+" current="+s.CurrentPlace?.Id+" exit="+s.LeaveBlocker);game.World.Root.SetActive(true);
 var cam=game.Cam;cam.farClipPlane=2500;cam.nearClipPlane=.08f;
 var light=(DayNightLighting)typeof(GameController).GetField("_lighting",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);light.Apply(630,false,false,true);
 Directory.CreateDirectory("TownMatchShots");
 void Shot(string name,Vector3 point,float yaw,float pitch,float size){
 cam.orthographic=true;cam.orthographicSize=size;cam.aspect=16/9f;cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);cam.transform.position=point-cam.transform.forward*Mathf.Max(30,size*2.6f);
 var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var prev=RenderTexture.active;cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("TownMatchShots/"+name+".png",image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
 var locations=new[]{("01","life_unit_s_oldhomes_2",14f),("02","expansion_shop_s_market_03",14.5f),("03","life_park",18.5f),("04","home_tier3",15f)};
 foreach(var loc in locations){
 var v=game.World.Places[loc.Item2];var pivot=v.Root.transform.TransformPoint(new Vector3(0,2.2f,(float)v.Place.Depth*.25f));
 Shot(loc.Item1+"A",pivot,(float)v.Place.Heading+34,36,loc.Item3);
 Shot(loc.Item1+"B",pivot,(float)v.Place.Heading-37,31,loc.Item3*.91f);
 }
 var regionIds=new[]{"outskirts","community","park","garden"};
 for(int i=0;i<regionIds.Length;i++){
   string region=regionIds[i];
   var places=game.World.Places.Values.Where(v=>game.City.AgencyDistrictFor(v.Place.StreetId)?.id==region).ToArray();
   if(places.Length==0)throw new Exception("No places in "+region);
   var corners=places.SelectMany(v=>new[]{v.Root.transform.TransformPoint(new Vector3(-(float)v.Place.Width*.5f,0,0)),v.Root.transform.TransformPoint(new Vector3((float)v.Place.Width*.5f,0,(float)v.Place.Depth))}).ToArray();
   var lo=corners.Aggregate(Vector3.Min);var hi=corners.Aggregate(Vector3.Max);
   var center=(lo+hi)*.5f;
   for(int j=0;j<2;j++){
     float yaw=(i==2?105f:181f)+(j==0?-21f:25f);float pitch=j==0?48:45;
     var inverse=Quaternion.Inverse(Quaternion.Euler(pitch,yaw,0));
     var extents=corners.Select(pos=>inverse*(pos-center)).ToArray();
     float height=extents.Max(pos=>Mathf.Max(Mathf.Abs(pos.x)*9/16,Mathf.Abs(pos.y)));
     Shot("wide"+(i+1).ToString("00")+(j==0?"A":"B"),center,yaw,pitch,height*1.04f+6);
   }
 }
 Debug.Log("CONCEPT_CAMERA_CAPTURED");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}