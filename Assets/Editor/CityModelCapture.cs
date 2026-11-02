using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PCShopSell.EditorTools
{
    [InitializeOnLoad]
    public static class CityModelCapture
    {
        const string Key="PCShop.ModelCapture";
        static double ready; static int errors;
        static CityModelCapture(){if(SessionState.GetBool(Key,false)){SaveStore.DirectoryOverride=Path.GetFullPath("work/model-capture-saves");EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;};}}
        public static void RunFromBatch(){SessionState.SetBool(Key,true);SaveStore.DirectoryOverride=Path.GetFullPath("work/model-capture-saves");EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");EditorApplication.EnterPlaymode();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;if(ready==0)ready=EditorApplication.timeSinceStartup+8;if(EditorApplication.timeSinceStartup<ready)return;
            try{
                ShaderUtil.allowAsyncCompilation=false;
                var g=UnityEngine.Object.FindObjectOfType<GameController>();if(g?.World==null)throw new Exception("Main world missing");
                g.enabled=false;g.Ui.CloseAllWindows();foreach(var c in g.Ui.Canvases)c.gameObject.SetActive(false);
                var actors=(Transform)typeof(GameController).GetField("_actors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);actors.gameObject.SetActive(false);
                g.World.Root.SetActive(true);g.World.SetVenueFocus(null);
                RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.75f,.76f,.75f);RenderSettings.ambientIntensity=1.1f;
                foreach(var l in UnityEngine.Object.FindObjectsOfType<Light>())if(l.type==LightType.Directional){l.intensity=1.2f;l.color=new Color(1,.96f,.88f);l.transform.rotation=Quaternion.Euler(50,-35,0);}
                var cam=g.Cam;cam.orthographic=true;cam.farClipPlane=2000;cam.nearClipPlane=.1f;
                Shot(cam,"models_01_city",new Vector3(25,0,260),new Vector3(-240,450,-360),335);
                Shot(cam,"models_02_old_neighborhood",new Vector3(-110,0,68),new Vector3(-85,100,-110),90);
                Shot(cam,"models_03_apartments",new Vector3(-100,0,330),new Vector3(-100,115,-130),95);
                Shot(cam,"models_04_villas",new Vector3(10,0,465),new Vector3(-120,130,-150),105);
                foreach(var kind in new[]{"ktv","restaurant","bar"}){var p=g.World.Places.Values.FirstOrDefault(v=>v.Place.Business=="venue_"+kind);if(p!=null)Shot(cam,"models_05_"+kind,p.Root.transform.position+new Vector3(0,3,7),new Vector3(-24,24,-35),24);}
                Debug.Log("CITY MODEL CAPTURE RESULT errors="+errors+" scene="+g.gameObject.scene.path+" places="+g.World.Places.Count);
                SessionState.SetBool(Key,false);EditorApplication.Exit(errors==0?0:1);
            }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        static void Shot(Camera cam,string name,Vector3 target,Vector3 offset,float size){cam.transform.position=target+offset*3f;cam.transform.LookAt(target);cam.orthographicSize=size;var rt=new RenderTexture(1920,1080,24);cam.targetTexture=rt;cam.aspect=1920f/1080;cam.Render();cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();Directory.CreateDirectory("docs/previews/current-models");File.WriteAllBytes("docs/previews/current-models/"+name+".png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(tex);}
    }
}
