// 全体 NPC 的正式入口复拍，安装在独立工程 Assets/Editor；不改主项目场景或存档。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class NpcPopulationCapture
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    const string Pending="PCShop.NpcPopulationCapture";
    static string Output=>Environment.GetEnvironmentVariable("PCSHOP_CHARACTER_SHOTS");
    static NpcPopulationCapture(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Capture;};}
    public static void Census()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Path.Combine(Output,"人口清单.tsv"),new[]{"Id\t姓名\t身份\t年龄"}.Concat(CityLifeCatalog.People.OrderBy(p=>p.Id).Select(p=>$"{p.Id}\t{p.Name}\t{p.Role}\t{p.Age}")));
        Debug.Log("NPC_CENSUS_DONE");EditorApplication.Exit(0);
    }
    public static void Run(){ShaderUtil.allowAsyncCompilation=false;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void CameraAt(Camera c,Vector3 focus,float pitch,float size){c.orthographic=true;c.orthographicSize=size;c.aspect=16f/9;c.transform.rotation=Quaternion.Euler(pitch,0,0);c.transform.position=focus-c.transform.forward*30;}
    static void Photo(Camera cam,string name)
    {
        var rt=RenderTexture.GetTemporary(2400,1350,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);var prev=RenderTexture.active;
        cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;
        var im=new Texture2D(2400,1350,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,2400,1350),0,0);im.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),im.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(im);RenderTexture.active=prev;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
    }
    static GameObject Figure(Transform stage,string id,float x,GameController game)
    {
        string person=id.StartsWith("crowd:")?null:id;
        var fig=(GameObject)typeof(Prims).GetMethod("FigureForPerson",F).Invoke(null,new object[]{stage,id,ArtDirection.Hex("667667"),ArtDirection.Hex("E8AB82"),1f,person,id});
        CityNpcClothing.Apply(fig,game.Session,person);fig.transform.localPosition=new Vector3(x,0,0);fig.transform.localRotation=Quaternion.Euler(0,180,0);return fig;
    }
    static void Capture()
    {
        try
        {
            SessionState.SetBool(Pending,false);Directory.CreateDirectory(Output);SaveStore.DirectoryOverride=Path.Combine(Output,"临时存档");
            var game=new GameObject("全体人物复拍").AddComponent<GameController>();game.Bootstrap(false,914);game.enabled=false;game.Ui.CloseAllWindows();foreach(var c in game.Ui.Canvases)c.gameObject.SetActive(false);
            game.World.Root.SetActive(false);((Transform)typeof(GameController).GetField("_actors",F).GetValue(game)).gameObject.SetActive(false);
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.63f,.65f);RenderSettings.ambientIntensity=1;
            foreach(var l in UnityEngine.Object.FindObjectsOfType<Light>()){l.enabled=l.type==LightType.Directional&&l.name=="Sun";if(l.enabled){l.intensity=.9f;l.color=Color.white;l.transform.rotation=Quaternion.Euler(52,-22,0);}}
            Shader.SetGlobalVector("_OldQuarterFigureZone",Vector4.zero);
            var cam=game.Cam;cam.nearClipPlane=.1f;cam.farClipPlane=200;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=ArtDirection.Hex("E4E4E6");
            var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var stage=new GameObject("全员样板").transform;stage.position=new Vector3(0,300,0);
            var ids=File.ReadAllLines(Path.Combine(Output,"角色列表.txt")).Where(s=>!string.IsNullOrWhiteSpace(s)).ToArray();
            var report=new System.Collections.Generic.List<string>{"角色\t模型\t顶点\t三角面\t子网格\t材质"};
            int perPage=5;
            for(int page=0;page*perPage<ids.Length;page++)
            {
                var rows=ids.Skip(page*perPage).Take(perPage).ToArray();var figs=rows.Select((id,i)=>Figure(stage,id,(i-(rows.Length-1)*.5f)*.84f,game)).ToArray();
                for(int angle=0;angle<4;angle++)
                {
                    foreach(var f in figs)f.transform.localRotation=Quaternion.Euler(0,new[]{180,145,90,0}[angle],0);
                    CameraAt(cam,stage.position+Vector3.up*.83f,angle==1?35:8,1.32f);Photo(cam,$"{page+1:00}-"+new[]{"正面","游玩角度","侧面","背面"}[angle]);
                }
                foreach(var fig in figs)
                {
                    foreach(var r in fig.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if(r.sharedMesh==null||r.sharedMaterials.Any(m=>m==null))throw new InvalidOperationException("缺少网格或材质："+fig.name);
                        report.Add($"{fig.name}\t{r.sharedMesh.name}\t{r.sharedMesh.vertexCount}\t{r.sharedMesh.triangles.Length/3}\t{r.sharedMesh.subMeshCount}\t"+string.Join(",",r.sharedMaterials.Select(m=>m.shader.name).Distinct()));
                    }
                    fig.transform.localRotation=Quaternion.Euler(0,145,0);fig.GetComponent<FigureMotion>().ResetMotion();
                }
                // 固定可复现的整个迈步周期；每帧保留轮廓检查，随后再坐下。
                for(int frame=0;frame<24;frame++)
                {
                    foreach(var f in figs)f.transform.position+=Vector3.back*.04f;
                    FigureMotion.AdvanceAll(1f/24,true);CameraAt(cam,stage.position+Vector3.back*.04f*(frame+1)+Vector3.up*.83f,18,1.32f);
                    if(Environment.GetEnvironmentVariable("PCSHOP_NPC_ACTIONS")=="all"||frame%6==0)Photo(cam,$"{page+1:00}-走路-{frame:00}");
                }
                foreach(var f in figs){f.GetComponent<FigureMotion>().ResetMotion();f.GetComponent<FigureMotion>().Seated=true;}
                Photo(cam,$"{page+1:00}-坐姿");
                File.WriteAllLines(Path.Combine(Output,$"{page+1:00}-名单.txt"),rows);
                foreach(var f in figs){f.SetActive(false);UnityEngine.Object.DestroyImmediate(f);}
            }
            File.WriteAllLines(Path.Combine(Output,"网格记录.tsv"),report);
            File.WriteAllLines(Path.Combine(Output,"人口清单.tsv"),new[]{"Id\t姓名\t身份\t年龄"}.Concat(CityLifeCatalog.People.OrderBy(p=>p.Id).Select(p=>$"{p.Id}\t{p.Name}\t{p.Role}\t{p.Age}")));
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("Characters/PlayerPainted")))throw new InvalidOperationException("人物着色器编译失败");
            Debug.Log("NPC_POPULATION_CAPTURE_DONE");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
