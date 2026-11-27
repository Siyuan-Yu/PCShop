// 仅复制到隔离工程 Assets/Editor；在真实 Game View 中采样，不生成发布包。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

[InitializeOnLoad]
public static class QaPerformanceCapture
{
    const string Key="PCShop.QaPerformanceCapture";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static string Output=>Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
    static GameController game;
    static EditorWindow gameView;
    static ProfilerRecorder draws, triangles,simulationRecorder,viewsRecorder,uiRecorder;
    static readonly bool PlayerLoopDrive=Environment.GetCommandLineArgs().Contains("-pcshop-perf-playerloop");
    static readonly bool Hotspots=Environment.GetCommandLineArgs().Contains("-pcshop-perf-hotspots");
    static readonly List<double> simulationSamples=new List<double>(),viewSamples=new List<double>(),uiSamples=new List<double>();
    static readonly string[] detailNames={"UpdateLabels","UpdateHud","UpdateDistrictPersonMarkers","UpdateInteractMarker","UpdateTip","UpdateQuestTracker","RefreshShortcutMenus","UpdateUpcomingScheduleHud","RefreshWorkbenchAccess","UpdateCityLifeHud"};
    static ProfilerRecorder[] detailRecorders;
    static readonly Dictionary<string,ProfilerRecorder> engineRecorders=new Dictionary<string,ProfilerRecorder>();
    static readonly Dictionary<string,List<double>> engineSamples=new Dictionary<string,List<double>>();
    [Serializable] class Spike { public double frameMs,simulationMs,viewsMs,uiMs; public long minute; }
    static readonly List<Spike> spikes=new List<Spike>();
    static readonly Dictionary<string,List<double>> detailSamples=new Dictionary<string,List<double>>(),coreSamples=new Dictionary<string,List<double>>();
    [Serializable] class Timing { public string name; public int count; public double meanMs,p99Ms,maxMs; }
    static readonly List<double> frames=new List<double>();
    static readonly List<double> steps=new List<double>();
    static readonly List<Result> results=new List<Result>();
    static readonly string RequestedCase=Argument("-pcshop-perf-case");
    static string Argument(string key){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;}
    static readonly string[] cases=RequestedCase!=null?new[]{RequestedCase}:Hotspots?new[]{"park-1x","garden-1x","garden-4x"}:new[]{"suburb-1x","community-1x","park-1x","garden-1x","race-1x","shop-1x","home-1x","suburb-reload","garden-4x","home-sleep"};
    static bool RestCase=>index>=0&&index<cases.Length&&cases[index]=="home-sleep";
    static int index=-1,lastFrame=-1,errors;
    static double since,last,simulation,drawSum,triangleSum,transitionMs;
    static double loadSimulationMs,attachSessionMs;
    static int initialPendingTextures;
    static long startMinute;
    static bool sampling,ready;
    static string initialSave;
    static int pausedFrames,recoveryFrames;
    [Serializable] class Result
    {
        public string scene,resolution,place;
        public int frames,activeRenderers,visibleRenderers,missingMaterials,errorShaders,pendingTextures,notAtTargetMip,initialPendingTextures,pausedFrames,recoveryFrames;
        public long simulatedMinutes;
        public double fps,frameMs,p95Ms,p99Ms,maxMs,stepMs,drawCalls,triangles,allocatedMB,textureMB,graphicsDriverMB,transitionMs;
        public double stepP99Ms,stepMaxMs,sampleSeconds;
        public double loadSimulationMs,attachSessionMs;
        public bool isRecovery,recoveryCompleted;
        public bool cpuMarkersValid;
        public double simulationP99Ms,viewsP99Ms,uiP99Ms,simulationMaxMs,viewsMaxMs,uiMaxMs;
        public double over120BudgetPercent,slowestOnePercentFps;
        public int internalSpeed;
        public string displayedSpeed;
        public bool meets120P99;
        public string[] materialWarnings;
        public Timing[] uiBreakdown,coreBreakdown,engineBreakdown;
        public Spike[] worstFrames;
    }
    [Serializable] class Report
    {
        public string unity,gpu,mode="Unity Editor Game View；包含编辑器开销，不是发行包 FPS";
        public string driver=PlayerLoopDrive?"Unity PlayerLoop.Update":"EditorApplication.update";
        public int graphicsMemoryMB,errors;
        public bool rendered,backgroundThrottleSuspected,validForFrameRate;
        public bool hotspotDiagnostic=Hotspots;
        public Result[] scenarios;
    }
    static QaPerformanceCapture(){EditorApplication.playModeStateChanged+=state=>{
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
        {
            Application.logMessageReceived+=Log;
            if(PlayerLoopDrive)
            {
                var loop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                for(int i=0;i<loop.subSystemList.Length;i++)
                {
                    if(loop.subSystemList[i].type!=typeof(UnityEngine.PlayerLoop.Update))continue;
                    var update=loop.subSystemList[i];
                    var systems=update.subSystemList.ToList();
                    systems.Add(new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(QaPerformanceCapture),updateDelegate=Tick});
                    update.subSystemList=systems.ToArray();loop.subSystemList[i]=update;
                    UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);break;
                }
                EditorApplication.update+=Pump;
            }
            else EditorApplication.update+=Tick;
        }
    };}
    public static void Run()
    {
        Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        foreach(var scene in Resources.FindObjectsOfTypeAll<SceneView>())scene.Close();
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView=view;
        view.titleContent=new GUIContent("PCShop 性能测试");view.Show();view.Focus();
        try
        {
            var assembly=typeof(EditorWindow).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,F,null,new object[]{Enum.Parse(kindType,"FixedResolution"),1920,1080,"QA 1080p"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            view.GetType().GetProperty("selectedSizeIndex",F).SetValue(view,count-1);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,"resolution-note.txt"),"固定分辨率设置未成功，以各场景实际 Screen 分辨率为准。\n"+e);}
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Path.Combine(Output,"errors.txt"),message+"\n"+stack+"\n");}
    }
    static void Pump()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(gameView!=null)gameView.Repaint();
    }
    static void Tick()
    {
        try
        {
            // 独立编辑器可能位于后台；主动请求 PlayerLoop，避免把后台 10 Hz 节流当成 GPU 性能。
            EditorApplication.QueuePlayerLoopUpdate();
            if(gameView==null)gameView=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.Repaint();
            if(game==null)
            {
                SaveStore.DirectoryOverride=Path.Combine(Output,"saves");
                game=new GameObject("性能巡检").AddComponent<GameController>();game.Bootstrap(true,7);game.enabled=false;
                Action enter=()=>{
                    var session=new GameSession(game.City,7,Money.FromYuan(15000),GameTimestamp.From(new GameDate(0),480),true,false,false,startWithCookingTutorial:true);
                    typeof(GameController).GetMethod("StartSession",F).Invoke(game,new object[]{session,0,null});
                };
                typeof(GameController).GetMethod("EnterGame",F).Invoke(game,new object[]{enter});return;
            }
            if(game.Session==null)return;
            if(!ready)
            {
                ready=true;
                game.Ui.QuestPopups=false;game.Session.EventsEnabled=false;game.Session.DebugAddCash(200000000);game.Session.DebugRentStarterShop(true);
                ((DistrictProgress)typeof(GameSession).GetField("_district",F).GetValue(game.Session)).UnlockedTier=3;
                game.Session.Clock.Restore(new GameDate(15),600);
                initialSave=JsonUtility.ToJson(game.Session.CreateSave());
                Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
                draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);
                triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);
                simulationRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"PCShop.Simulation",1);
                viewsRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"PCShop.Views",1);
                uiRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"PCShop.UI",1);
                detailRecorders=detailNames.Select(n=>ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"PCShop.UI."+n,1)).ToArray();
                foreach(string n in detailNames)detailSamples[n]=new List<double>();
                if (Hotspots)
                {
                    var wanted=new HashSet<string>{"Main Thread","Render Thread","PlayerLoop","EditorLoop","CPU Main Thread Frame Time","CPU Render Thread Frame Time","GPU Frame Time","Gfx.WaitForPresentOnGfxThread","WaitForTargetFPS","Canvas.BuildBatch","Canvas.SendWillRenderCanvases","GC.Collect"};
                    var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
                    foreach(var handle in handles)
                    {
                        var description=ProfilerRecorderHandle.GetDescription(handle);
                        if(!wanted.Contains(description.Name)||engineRecorders.ContainsKey(description.Name))continue;
                        engineRecorders[description.Name]=ProfilerRecorder.StartNew(description.Category,description.Name,1);
                        engineSamples[description.Name]=new List<double>();
                    }
                    File.WriteAllLines(Path.Combine(Output,"引擎计时器.txt"),engineRecorders.Keys);
                }
                if (Hotspots) RuntimePerformanceProbe.Listener=(n,ms)=>{if(!coreSamples.TryGetValue(n,out var list))coreSamples[n]=list=new List<double>();list.Add(ms);};
                Next();return;
            }
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            double now=EditorApplication.timeSinceStartup;
            if(sampling)
            {
                frames.Add((now-last)*1000);drawSum+=draws.LastValue;triangleSum+=triangles.LastValue;
                if(!game.Session.Time.IsRunning)pausedFrames++;
                if(game.Session.IsTimeAdvancing)recoveryFrames++;
                simulationSamples.Add(simulationRecorder.Valid?simulationRecorder.LastValue/1000000.0:0);
                viewSamples.Add(viewsRecorder.Valid?viewsRecorder.LastValue/1000000.0:0);
                uiSamples.Add(uiRecorder.Valid?uiRecorder.LastValue/1000000.0:0);
                foreach(var entry in engineRecorders)if(entry.Value.Valid)engineSamples[entry.Key].Add(entry.Value.LastValue/1000000.0);
                if(Hotspots&&(now-last)*1000>1000.0/120)spikes.Add(new Spike{frameMs=(now-last)*1000,simulationMs=simulationSamples.Last(),viewsMs=viewSamples.Last(),uiMs=uiSamples.Last(),minute=game.Session.Clock.Now.TotalMinutes});
                for(int i=0;i<detailNames.Length;i++)if(detailRecorders[i].Valid)detailSamples[detailNames[i]].Add(detailRecorders[i].LastValue/1000000.0);
            }
            float dt=(float)Math.Min(.25,Math.Max(0,now-last));last=now;
            long begin=Stopwatch.GetTimestamp();game.Step(dt);
            if(sampling)
            {
                double step=(Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency;
                simulation+=step;steps.Add(step);
            }
            if(!sampling&&now-since>=4)
            {
                if(RestCase && !game.Session.SleepUntil(480))throw new Exception("实际会话未接受睡眠请求");
                sampling=true;since=now;startMinute=game.Session.Clock.Now.TotalMinutes;
            }
            else if(sampling&&(RestCase ? !game.Session.IsTimeAdvancing : now-since>=(Hotspots&&!cases[index].EndsWith("4x")?30:60)&&frames.Count>=600))
            {FinishCase();Next();}
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static void Next()
    {
        var transition=Stopwatch.StartNew();
        index++;
        if(index>=cases.Length)
        {
            bool rendered=results.All(r=>r.drawCalls>0);
            bool throttled=results.Count(r=>r.frameMs>90&&r.frameMs<110&&r.stepMs<20)>=results.Count-1;
            var report=new Report{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,graphicsMemoryMB=SystemInfo.graphicsMemorySize,errors=errors,rendered=rendered,backgroundThrottleSuspected=throttled,validForFrameRate=rendered&&!throttled,scenarios=results.ToArray()};
            File.WriteAllText(Path.Combine(Output,"performance.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(Output,"result.txt"),!rendered?"无有效绘制，不能作为 FPS。":throttled?"出现固定后台节流，不能作为游戏 FPS。":"Game View 已提交绘制；请按编辑器测试解释帧率。" );
            draws.Dispose();triangles.Dispose();simulationRecorder.Dispose();viewsRecorder.Dispose();uiRecorder.Dispose();foreach(var recorder in detailRecorders)recorder.Dispose();foreach(var recorder in engineRecorders.Values)recorder.Dispose();RuntimePerformanceProbe.Listener=null;SessionState.SetBool(Key,false);EditorApplication.Exit(rendered&&!throttled&&errors==0?0:2);return;
        }
        coreSamples.Clear();
        var s=game.Session;
        // Each case starts from the same saved day. Rewinding only the clock after
        // a 60-second sample leaves appointments, NPC routes and schedules in the future.
        {
            var loadWatch=Stopwatch.StartNew();
            var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(initialSave));
            loadSimulationMs=loadWatch.Elapsed.TotalMilliseconds;loadWatch.Restart();
            typeof(GameController).GetMethod("StartSession",F).Invoke(game,new object[]{loaded,0,null});s=game.Session;
            attachSessionMs=loadWatch.Elapsed.TotalMilliseconds;
            s.EventsEnabled=false;
            if(cases[index]!="suburb-reload")transition.Restart();
        }
        s.StopTrip();s.Leave();s.Player.Health=100;s.Player.Energy=100;s.Player.Hunger=100;
        s.Clock.Restore(new GameDate(15),600);
        int tier=cases[index].StartsWith("community")?1:cases[index].StartsWith("park")?2:cases[index].StartsWith("garden")?3:0;
        Place place=cases[index].StartsWith("home")?s.Home:cases[index].StartsWith("race")?s.City.GetPlace("race_0"):
            tier==0?s.MainShop.Place:s.City.Places.First(p=>p.Kind==PlaceKind.Agency&&s.DistrictTierOf(p)==tier);
        s.ArriveAtNode(place.NodeId);
        if(cases[index].StartsWith("home")||cases[index].StartsWith("shop"))s.Enter(place);
        s.Time.SetPaused(false);s.Time.SetSpeed(cases[index].EndsWith("4x")?8:TimeController.DefaultSpeed);
        game.Step(0);game.Ui.CloseAllWindows();game.Ui.CloseDock();game.SnapCamera();
        transitionMs=transition.Elapsed.TotalMilliseconds;initialPendingTextures=(int)Texture.streamingTexturePendingLoadCount;
        File.AppendAllText(Path.Combine(Output,"progress.txt"),cases[index]+" "+place.Id+"\n");
        frames.Clear();steps.Clear();pausedFrames=recoveryFrames=0;simulation=drawSum=triangleSum=0;sampling=false;since=last=EditorApplication.timeSinceStartup;
        simulationSamples.Clear();viewSamples.Clear();uiSamples.Clear();
        foreach(var list in detailSamples.Values)list.Clear();
        foreach(var list in engineSamples.Values)list.Clear();spikes.Clear();
    }
    static void FinishCase()
    {
        var sorted=frames.OrderBy(x=>x).ToArray();var sortedSteps=steps.OrderBy(x=>x).ToArray();var plane=GeometryUtility.CalculateFrustumPlanes(game.Cam);
        var renderers=UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
        var visible=renderers.Where(r=>GeometryUtility.TestPlanesAABB(plane,r.bounds)).ToArray();
        var warnings=new List<string>();int missing=0,bad=0;
        foreach(var r in visible)foreach(var m in r.sharedMaterials)
        {
            if(m==null){missing++;warnings.Add(r.name+": 材质为空");continue;}
            if(m.shader==null||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader"){bad++;warnings.Add(r.name+": "+m.name+" 着色器异常");}
        }
        var textures=visible.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).OfType<Texture2D>().Distinct().ToArray();
        var result=new Result{scene=cases[index],resolution=Screen.width+"x"+Screen.height,place=game.Session.CurrentPlace?.Id??"outdoors",frames=frames.Count,
            uiBreakdown=Timings(detailSamples),coreBreakdown=Timings(coreSamples),engineBreakdown=Timings(engineSamples),worstFrames=spikes.OrderByDescending(s=>s.frameMs).Take(32).ToArray(),
            cpuMarkersValid=simulationRecorder.Valid&&viewsRecorder.Valid&&uiRecorder.Valid,
            simulationP99Ms=Percentile99(simulationSamples),viewsP99Ms=Percentile99(viewSamples),uiP99Ms=Percentile99(uiSamples),
            simulationMaxMs=simulationSamples.Max(),viewsMaxMs=viewSamples.Max(),uiMaxMs=uiSamples.Max(),
            fps=1000/frames.Average(),frameMs=frames.Average(),p95Ms=sorted[(int)(sorted.Length*.95)],p99Ms=sorted[(int)(sorted.Length*.99)],maxMs=sorted.Last(),
            over120BudgetPercent=100.0*frames.Count(ms=>ms>1000.0/120)/frames.Count,
            slowestOnePercentFps=1000.0/sorted.Skip((int)(sorted.Length*.99)).Average(),
            internalSpeed=game.Session.Time.Speed,displayedSpeed=TimeController.SpeedLabel(game.Session.Time.Speed),
            pausedFrames=pausedFrames,recoveryFrames=recoveryFrames,
            meets120P99=pausedFrames==0&&recoveryFrames==0&&1000/frames.Average()>=120&&sorted[(int)(sorted.Length*.99)]<=1000.0/120,
            stepMs=simulation/frames.Count,drawCalls=drawSum/frames.Count,triangles=triangleSum/frames.Count,simulatedMinutes=game.Session.Clock.Now.TotalMinutes-startMinute,
            stepP99Ms=sortedSteps[(int)(sortedSteps.Length*.99)],stepMaxMs=sortedSteps.Last(),sampleSeconds=frames.Sum()/1000,
            loadSimulationMs=loadSimulationMs,attachSessionMs=attachSessionMs,
            isRecovery=RestCase,recoveryCompleted=RestCase&&!game.Session.IsTimeAdvancing&&game.Session.TimeAdvanceError==null&&game.Session.Clock.MinuteOfDay==480,
            activeRenderers=renderers.Length,visibleRenderers=visible.Length,missingMaterials=missing,errorShaders=bad,pendingTextures=(int)Texture.streamingTexturePendingLoadCount,
            notAtTargetMip=textures.Count(t=>t.streamingMipmaps&&t.loadedMipmapLevel>t.desiredMipmapLevel),allocatedMB=Profiler.GetTotalAllocatedMemoryLong()/1048576.0,
            textureMB=Texture.currentTextureMemory/1048576.0,graphicsDriverMB=Profiler.GetAllocatedMemoryForGraphicsDriver()/1048576.0,materialWarnings=warnings.Take(40).ToArray(),
            transitionMs=transitionMs,initialPendingTextures=initialPendingTextures};
        results.Add(result);File.WriteAllText(Path.Combine(Output,cases[index]+".json"),JsonUtility.ToJson(result,true));
        var rt=RenderTexture.GetTemporary(1920,1080,24);var old=RenderTexture.active;var target=game.Cam.targetTexture;
        game.Cam.targetTexture=rt;game.Cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Output,cases[index]+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        game.Cam.targetTexture=target;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
    }
    static double Percentile99(List<double> values){var sorted=values.OrderBy(x=>x).ToArray();return sorted[(int)(sorted.Length*.99)];}
    static Timing[] Timings(Dictionary<string,List<double>> samples)=>samples.Where(p=>p.Value.Count>0).Select(p=>new Timing{name=p.Key,count=p.Value.Count,meanMs=p.Value.Average(),p99Ms=Percentile99(p.Value),maxMs=p.Value.Max()}).ToArray();
}
