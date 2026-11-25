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
[InitializeOnLoad]public static class TownOccupiedStreets{
 static TownOccupiedStreets(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TownOccupiedStreets",false))EditorApplication.delayCall+=Capture;};}
 public static void Run(){ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("TownOccupiedStreets",true);EditorApplication.EnterPlaymode();}
 static void Capture(){try{SessionState.SetBool("TownOccupiedStreets",false);SaveStore.DirectoryOverride=Path.GetFullPath("quarter-review-save");
 var game=new GameObject("Review").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;var s=game.Session;s.EventsEnabled=false;game.Ui.QuestPopups=false;game.Ui.CloseAllWindows();s.DebugRentStarterShop(computerScenario:true);s.Leave();s.Clock.Restore(s.Clock.Today,630);game.Step(0);foreach(var c in game.Ui.Canvases)c.gameObject.SetActive(false);game.World.Root.SetActive(true);
 var starter=game.World.Places["life_unit_s_oldhomes_2"];s.ArriveAtNode(starter.Place.NodeId);game.Step(.03f);game.SnapCamera();Debug.Log("Outdoor capture: active="+game.World.Root.activeSelf+" current="+s.CurrentPlace?.Id+" exit="+s.LeaveBlocker);game.World.Root.SetActive(true);
 var cam=game.Cam;cam.farClipPlane=2500;cam.nearClipPlane=.08f;
 var light=(DayNightLighting)typeof(GameController).GetField("_lighting",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);light.Apply(630,false,false,true);
 Directory.CreateDirectory("TownOccupiedShots");
 void Shot(string name,Vector3 point,float yaw,float pitch,float size){
 cam.orthographic=true;cam.orthographicSize=size;cam.aspect=16/9f;cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);cam.transform.position=point-cam.transform.forward*Mathf.Max(30,size*2.6f);
 var crowd=(CrowdView)typeof(GameController).GetField("_crowd",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
 crowd.SyncCity(s,game.World.StreetPosition(s.PlayerPosition),cam,game.World);crowd.Update(.033f,true);
 var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var prev=RenderTexture.active;cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("TownOccupiedShots/"+name+".png",image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
 var locations=new[]{("01","life_unit_s_oldhomes_2",14f),("02","expansion_shop_s_market_03",14.5f),("03","life_park",18.5f),("04","home_tier3",15f)};
 foreach(var loc in locations){
 var v=game.World.Places[loc.Item2];var pivot=v.Root.transform.TransformPoint(new Vector3(0,2.2f,(float)v.Place.Depth*.25f));
 Shot(loc.Item1+"A",pivot,(float)v.Place.Heading+34,36,loc.Item3);
 Shot(loc.Item1+"B",pivot,(float)v.Place.Heading-37,31,loc.Item3*.91f);
 }
 Debug.Log("CONCEPT_CAMERA_CAPTURED");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}