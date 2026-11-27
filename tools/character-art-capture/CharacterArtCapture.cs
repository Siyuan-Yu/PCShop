// 安装到复拍工程 Assets/Editor。用正式人物、服装、骨架和动作入口拍摄。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class CharacterArtCapture
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    const string Pending="PCShop.CharacterArtCapture";
    static string Output => Environment.GetEnvironmentVariable("PCSHOP_CHARACTER_SHOTS");
    static CharacterArtCapture()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
                EditorApplication.delayCall += Capture;
        };
    }
    public static void Run()
    {
        if(string.IsNullOrEmpty(Output)) throw new ArgumentException("缺少 PCSHOP_CHARACTER_SHOTS。");
        ShaderUtil.allowAsyncCompilation=false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static GameObject Figure(Transform parent,string id,float x,GameController game)
    {
        GameObject fig;
        if(id=="")
        {
            var at=typeof(Prims).Assembly.GetType("PCShopSell.Game.Runtime.FigureAppearance");
            var method=typeof(Prims).GetMethod("Figure",F,null,new[]{typeof(Transform),typeof(string),typeof(Color),typeof(Color),typeof(float),at},null);
            fig=(GameObject)method.Invoke(null,new object[]{parent,"主角",Color.gray,Color.white,1f,Enum.ToObject(at,1)});
        }
        else
        {
            fig=(GameObject)typeof(Prims).GetMethod("FigureForPerson",F).Invoke(null,new object[]{parent,id,Color.gray,Color.white,1f,id,null});
            CityNpcClothing.Apply(fig,game.Session,id);
        }
        fig.transform.localPosition=new Vector3(x,0,0);
        fig.transform.localRotation=Quaternion.Euler(0,180,0);
        return fig;
    }
    static void Photo(Camera cam,string name)
    {
        var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        var prev=RenderTexture.active; cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=prev;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
        Debug.Log("人物复拍 "+name);
    }
    static void CameraAt(Camera cam,Vector3 focus,float pitch,float yaw,float size)
    {
        cam.orthographic=true;cam.orthographicSize=size;cam.aspect=16f/9;
        cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);cam.transform.position=focus-cam.transform.forward*30;
    }
    static void Capture()
    {
        try
        {
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);
            SaveStore.DirectoryOverride=Path.Combine(Output,"临时存档");
            var game=new GameObject("人物复拍").AddComponent<GameController>();game.Bootstrap(false,914);
            game.enabled=false;game.Ui.CloseAllWindows();foreach(var cv in game.Ui.Canvases)cv.gameObject.SetActive(false);
            game.World.Root.SetActive(false);
            var actors=(Transform)typeof(GameController).GetField("_actors",F).GetValue(game);actors.gameObject.SetActive(false);
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.62f,.63f,.65f);RenderSettings.ambientIntensity=1;
            var lamps=UnityEngine.Object.FindObjectsOfType<Light>().Select(l=>(light:l,on:l.enabled)).ToArray();
            foreach(var item in lamps)
            {
                var light=item.light;
                if(light.type==LightType.Directional && light.name=="Sun")
                {light.enabled=true;light.intensity=.9f;light.color=Color.white;light.transform.rotation=Quaternion.Euler(52,-22,0);}
                else light.enabled=false;
            }
            Shader.SetGlobalVector("_OldQuarterFigureZone",Vector4.zero);
            var cam=game.Cam;cam.nearClipPlane=.1f;cam.farClipPlane=200;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=ArtDirection.Hex("E4E4E6");
            var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var stage=new GameObject("三人模型").transform;stage.position=new Vector3(0,300,0);
            var ids=new[]{"","res_baker","life_singer"};
            var figs=ids.Select((id,i)=>Figure(stage,id,(i-1)*1.05f,game)).ToArray();
            CameraAt(cam,stage.position+Vector3.up*.86f,8,0,1.05f);
            for(int angle=0;angle<4;angle++)
            {
                foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,new[]{180,145,90,0}[angle],0);
                Photo(cam,new[]{"01-front","02-three-quarter","03-side","04-back"}[angle]);
            }
            foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,180,0);
            CameraAt(cam,stage.position+Vector3.up*.80f,35,0,1.12f);Photo(cam,"05-game-angle-close");
            CameraAt(cam,stage.position+Vector3.up*.80f,35,0,5.5f);Photo(cam,"06-game-distance");
            if(Environment.GetEnvironmentVariable("PCSHOP_CHARACTER_STAGE")!="animation")
            {
                game.World.Root.SetActive(true);
                foreach(var item in lamps)if(item.light!=null)item.light.enabled=item.on;
                var staticPlace=game.World.Places[game.City.PlayerShop().Id];
                stage.position=staticPlace.Entrance+Vector3.up*.02f-staticPlace.Root.transform.forward*3f;
                stage.rotation=staticPlace.Root.transform.rotation;
                var staticLighting=(DayNightLighting)typeof(GameController).GetField("_lighting",F).GetValue(game);
                staticLighting.Apply(720,false);data.renderPostProcessing=true;cam.farClipPlane=2000;
                CameraAt(cam,stage.position+Vector3.up*.65f,40,staticPlace.Root.transform.eulerAngles.y+25,5.5f);
                Photo(cam,"06b-street-static");
                CameraAt(cam,stage.position+Vector3.up*.65f,CameraRig.DefaultPitch,CameraRig.DefaultYaw,16f);
                Photo(cam,"06c-street-default");
                var staticShader=Resources.Load<Shader>("Characters/PlayerPainted");
                if(ShaderUtil.ShaderHasError(staticShader))throw new InvalidOperationException("人物着色器编译失败。");
                File.WriteAllText(Path.Combine(Output,"静态复拍记录.txt"),
                    "来源：独立 Unity 工程 Play Mode，正式人物生成入口。未补画截图，未运行自动测试或发布构建。\n"+
                    "标准正面/侧面/背面：1920×1080，正交尺寸 1.05，俯角 8°。游戏角度：俯角 35°。\n"+
                    "06 / 06b 为拉近至 5.5 的查看距离；06c 采用 SnapCamera 的街道默认尺寸 16、默认俯角 35° / 方位 45°。\n"+
                    "下列网格数量含同一子网格内的描边壳，三名人物仍各 3 个 SkinnedMeshRenderer。\n"+
                    string.Join("\n",figs.Select(f=>f.name+"\n"+string.Join("\n",f.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Select(r=>$"  {r.name}：顶点 {r.sharedMesh.vertexCount}，三角面 {r.sharedMesh.triangles.Length/3}，子网格 {r.sharedMesh.subMeshCount}")))));
                Debug.Log("CHARACTER_STATIC_CAPTURE_DONE");EditorApplication.Exit(0);return;
            }
            CameraAt(cam,stage.position+Vector3.up*.84f,12,0,1.05f);
            foreach(var fig in figs)fig.GetComponent<FigureMotion>().ResetMotion();
            for(int frame=0;frame<48;frame++)
            {
                foreach(var fig in figs)fig.transform.position+=Vector3.back*.03f;
                FigureMotion.AdvanceAll(1f/24,true);
                CameraAt(cam,stage.position+Vector3.back*.03f*(frame+1)+Vector3.up*.84f,12,0,1.05f);
                Photo(cam,"walk-"+frame.ToString("00"));
                foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,100,0);
                Photo(cam,"walk-side-"+frame.ToString("00"));
                foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,180,0);
            }
            for(int i=0;i<3;i++)
            {
                figs[i].transform.localPosition=new Vector3((i-1)*1.05f,0,0);
                figs[i].GetComponent<FigureMotion>().ResetMotion();
            }
            CameraAt(cam,stage.position+Vector3.up*.84f,12,0,1.05f);
            figs[0].GetComponent<FigureMotion>().WorkAction=FigureWorkAction.Assemble;
            figs[1].GetComponent<FigureMotion>().WorkAction=FigureWorkAction.Cook;
            figs[2].GetComponent<FigureMotion>().ActivityKind="talk";
            for(int frame=0;frame<24;frame++)
            {
                FigureMotion.AdvanceAll(1f/24,true);Photo(cam,"work-"+frame.ToString("00"));
            }
            foreach(var fig in figs)fig.GetComponent<FigureMotion>().Seated=true;
            Photo(cam,"07-seated");
            foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,100,0);
            Photo(cam,"07-seated-side");
            foreach(var fig in figs)fig.GetComponent<FigureMotion>().Seated=false;
            foreach(var fig in figs)typeof(FigureMotion).GetMethod("SetFurnitureSeatPose",F).Invoke(fig.GetComponent<FigureMotion>(),new object[]{.42f,1f});
            Photo(cam,"07b-furniture-seat-side");
            foreach(var fig in figs)typeof(FigureMotion).GetMethod("SetFurnitureSeatPose",F).Invoke(fig.GetComponent<FigureMotion>(),new object[]{.42f,.5f});
            Photo(cam,"07c-seat-transition");
            foreach(var fig in figs)typeof(FigureMotion).GetMethod("SetFurnitureSeatPose",F).Invoke(fig.GetComponent<FigureMotion>(),new object[]{0f,0f});
            foreach(var fig in figs)fig.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var fig in figs)fig.GetComponent<FigureMotion>().Seated=false;
            var report=figs.Select(f=>new {name=f.name,renderers=f.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>new {name=r.name,vertices=r.sharedMesh.vertexCount,triangles=r.sharedMesh.triangles.Length/3,submeshes=r.sharedMesh.subMeshCount}).ToArray()}).ToArray();
            File.WriteAllText(Path.Combine(Output,"mesh-counts.txt"),string.Join("\n",report.Select(r=>r.name+"\n"+string.Join("\n",r.renderers.Select(m=>$"  {m.name} 顶点 {m.vertices} 三角面 {m.triangles} 子网格 {m.submeshes}")))));
            // 正式地图里的同一批网格，以及实际日夜灯光。
            foreach(var fig in figs)
            {
                var motion=fig.GetComponent<FigureMotion>();motion.WorkAction=FigureWorkAction.None;motion.ActivityKind=null;motion.ResetMotion();
            }
            game.World.Root.SetActive(true);
            foreach(var item in lamps)if(item.light!=null)item.light.enabled=item.on;
            var place=game.World.Places[game.City.PlayerShop().Id];
            stage.position=place.Entrance+Vector3.up*.02f-place.Root.transform.forward*3f;
            stage.rotation=place.Root.transform.rotation;
            var lighting=(DayNightLighting)typeof(GameController).GetField("_lighting",F).GetValue(game);
            data.renderPostProcessing=true;cam.farClipPlane=2000;
            foreach(var state in new[]{("08-street-day",720,false),("09-street-rain",720,true),("10-street-night",1320,false)})
            {
                lighting.Apply(state.Item2,false,state.Item3);
                CameraAt(cam,stage.position+Vector3.up*.65f,40,place.Root.transform.eulerAngles.y+25,5.5f);Photo(cam,state.Item1);
            }
            lighting.Apply(720,false);
            var mirror=new AvatarPreview();mirror.Render("tee");
            SaveTexture(mirror.Texture,"11-wardrobe");mirror.Destroy();
            var portrait=new AvatarPreview(portrait:true);portrait.Render("tee");SaveTexture(portrait.Texture,"12-portrait");portrait.Destroy();
            var wardrobe=new AvatarPreview();
            foreach(string outfit in new[]{"cotton_short","suit","coat_quality"})
            {wardrobe.Render(outfit);SaveTexture(wardrobe.Texture,"outfit-"+outfit);}
            wardrobe.Destroy();
            var verification=new AvatarPreview(verification:true);verification.PrepareVerification("tee");
            foreach(var gesture in new[]{PCShopSell.Core.Session.LoanGesture.Nod,PCShopSell.Core.Session.LoanGesture.ShakeHead,PCShopSell.Core.Session.LoanGesture.Wave})
            {
                verification.RenderGesture(gesture,.375f);SaveTexture(verification.Texture,"gesture-"+gesture);
            }
            verification.Destroy();
            var shader=Resources.Load<Shader>("Characters/PlayerPainted");
            if(ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("人物着色器编译失败，检查前面的 Shader error。");
            Debug.Log("CHARACTER_CAPTURE_DONE");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void SaveTexture(RenderTexture rt,string name)
    {
        var prev=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=prev;
    }
}
