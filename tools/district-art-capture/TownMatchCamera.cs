using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Install in Assets/Editor in the review copy only.
[InitializeOnLoad]
public static partial class TownMatchCamera
{
    static partial void CaptureWalking(GameController game,string folder);
    static partial void CaptureInteriors(GameController game, ref bool handled);
    private const string Pending = "PCShop.DistrictArtCapture";
    private static readonly string[] Regions = { "outskirts", "community", "park", "garden" };
    private static readonly (string id, float size)[] Sites =
    {
        ("life_unit_s_oldhomes_2",14f), ("expansion_shop_s_market_03",14.5f),
        ("life_park",18.5f), ("home_tier3",15f)
    };
    private static string Output => Environment.GetEnvironmentVariable("PCSHOP_TOWN_SHOTS");

    static TownMatchCamera()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
                EditorApplication.delayCall += Start;
        };
    }

    public static void Run()
    {
        if (string.IsNullOrWhiteSpace(Output)) throw new ArgumentException("请指定 PCSHOP_TOWN_SHOTS 输出目录。");
        ShaderUtil.allowAsyncCompilation = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true);
        EditorApplication.EnterPlaymode();
    }

    private static void Start()
    {
        try
        {
            SessionState.SetBool(Pending,false);
            Directory.CreateDirectory(Output);
            SaveStore.DirectoryOverride = Path.Combine(Output,"临时存档");
            var game = new GameObject("场景复核").AddComponent<GameController>();
            bool baked = Environment.GetEnvironmentVariable("PCSHOP_BAKED_REVIEW") == "1";
            game.Bootstrap(baked,914);
            if (!baked) { Capture(game,false); return; }

            // Use the same asynchronous scene loader as New Game. Seed the session and bypass
            // the cinematic so both ways of loading the map are photographed under the same sky.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var controller = typeof(GameController);
            Action ready = () =>
            {
                try
                {
                    var session = controller.GetMethod("NewSession",flags).Invoke(game,new object[] { (ulong?)914 });
                    controller.GetMethod("StartSession",flags).Invoke(game,new object[] { session,0,null });
                    game.StartCoroutine(CaptureAfterLoadingScreen(game));
                }
                catch (Exception ex) { Fail(ex); }
            };
            controller.GetMethod("EnterGame",flags).Invoke(game,new object[] { ready });
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Fail(Exception ex)
    {
        Debug.LogException(ex);
        EditorApplication.Exit(1);
    }

    private static System.Collections.IEnumerator CaptureAfterLoadingScreen(GameController game)
    {
        // EnterFirstSession closes its loading window one player frame after the ready callback.
        // Wait for its finally block, then let Unity release the dismissed window and lay out the HUD.
        var entering = typeof(GameController).GetField("_enteringWorld", BindingFlags.Instance | BindingFlags.NonPublic);
        while ((bool)entering.GetValue(game)) yield return null;
        yield return null;
        Capture(game,true);
    }

    private static void Capture(GameController game, bool baked)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("PCSHOP_INTERIORS") == "1")
            {
                bool handled = false;
                CaptureInteriors(game, ref handled);
                if (!handled) throw new InvalidOperationException("室内复拍需要同时安装 InteriorReview.cs。");
                return;
            }
            if (baked && !game.World.Root.TryGetComponent<BakedCityMap>(out _))
                throw new InvalidOperationException("城市加载未采用预生成地图。");
            game.enabled = false;
            var session = game.Session;
            session.EventsEnabled = false;
            game.Ui.QuestPopups = false;
            game.Ui.CloseAllWindows();
            session.DebugRentStarterShop(computerScenario:true);
            session.Leave();
            session.Clock.Restore(session.Clock.Today,630);
            game.Step(0);
            var playHud=game.Ui.Canvases.Where(c=>c.gameObject.activeInHierarchy && c.isRootCanvas &&
                c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var canvas in game.Ui.Canvases) canvas.gameObject.SetActive(false);
            game.World.Root.SetActive(true);
            var cam = game.Cam;
            cam.farClipPlane = 2500;
            cam.nearClipPlane = .08f;
            cam.aspect = 16f/9;
            var manifest = new List<string>();
            for (int i=0; i<Regions.Length; i++)
            {
                var site = game.World.Places[Sites[i].id];
                string tag = (i+1).ToString("00");
                session.ArriveAtNode(site.Place.NodeId);
                game.Step(.03f);
                game.SnapCamera();
                // Prime the first offscreen render before recording either version of the entrance.
                SaveWithHud(game,cam,"hud"+tag,playHud,warmup:i==0);
                Save(cam,"play"+tag);
                var center = site.Root.transform.TransformPoint(new Vector3(0,2.2f,(float)site.Place.Depth*.25f));
                Shot(cam,tag+"A",center,(float)site.Place.Heading+34,36,Sites[i].size);
                Shot(cam,tag+"B",center,(float)site.Place.Heading-37,31,Sites[i].size*.91f);
                if(i>0)
                {
                    // Supplemental closer views keep the subject large enough to inspect painted material.
                    // The fixed street and survey views above/below remain the baseline for layout checks.
                    Shot(cam,tag+"DetailA",center,(float)site.Place.Heading+34,36,Sites[i].size*.81f);
                    Shot(cam,tag+"DetailB",center,(float)site.Place.Heading-37,31,Sites[i].size*.76f);
                    var nearSize=Sites[i].size;
                    DistrictMatch(cam,i-1,0,center,(float)site.Place.Heading+34,36,nearSize*(i==1?.81f:1));
                    DistrictMatch(cam,i-1,1,center,(float)site.Place.Heading-37,31,nearSize*(i==1?.76f:.91f));
                    if(i==1)
                    {
                        var yard=site.Root.transform.TransformPoint(new Vector3(-(float)site.Place.Width*.5f-1.15f,1.18f,(float)site.Place.Depth*.70f));
                        Shot(cam,"02Cycle",yard,(float)site.Place.Heading+101,15,2.25f,standOff:3.7f);
                    }
                }
                var sites = game.World.Places.Values.Where(v=>game.City.AgencyDistrictFor(v.Place.StreetId)?.id==Regions[i]).ToArray();
                var corners = sites.SelectMany(v=>new[] {
                    v.Root.transform.TransformPoint(new Vector3(-(float)v.Place.Width*.5f,0,0)),
                    v.Root.transform.TransformPoint(new Vector3((float)v.Place.Width*.5f,0,(float)v.Place.Depth))
                }).ToArray();
                var origin = (corners.Aggregate(Vector3.Min)+corners.Aggregate(Vector3.Max))*.5f;
                for (int j=0; j<2; j++)
                {
                    float yaw=(i==2?105f:181f)+(j==0?-21f:25f), pitch=j==0?48:45;
                    var inverse = Quaternion.Inverse(Quaternion.Euler(pitch,yaw,0));
                    float size = corners.Select(p=>inverse*(p-origin)).Max(p=>Mathf.Max(Mathf.Abs(p.x)*9/16,Mathf.Abs(p.y)));
                    Shot(cam,"wide"+tag+(j==0?"A":"B"),origin,yaw,pitch,size*1.04f+6);
                    if(i>0)DistrictMatch(cam,i-1,j+2,origin,yaw,pitch,size*1.04f+6);
                }
                if(i==0)
                {
                    Shot(cam,"road-west",new Vector3(-577.39f,.10f,-544.19f),193,50,11);
                    Shot(cam,"road-east",new Vector3(-422.34f,.10f,-482.10f),193,50,11);
                    session.Clock.Restore(session.Clock.Today,554);
                    game.Step(.03f);
                    Shot(cam,"oldtown-home",new Vector3(-438.23760f,2.50000f,-527.19120f),210.72770f,34,16);
                    Shot(cam,"oldtown-yard",new Vector3(-445.28160f,1.60000f,-539.53550f),34,36,17);
                    Shot(cam,"oldtown-wide",new Vector3(-493.78710f,6.00005f,-517.48000f),193,45,69.94250f);
                    // Small registration corrections from the common corners of the approved paintings.
                    // Keep the three original survey shots above as the cross-revision comparison.
                    void Registered(string name,Vector3 point,float yaw,float pitch,float size,float shiftX,float shiftY)
                    {
                        var frame=Quaternion.Euler(pitch,yaw,0);
                        var offset=frame*new Vector3(-shiftX,shiftY,0)*(2*size/675f);
                        Shot(cam,"matched-"+name,point+offset,yaw,pitch,size);
                    }
                    Registered("home",new Vector3(-438.23760f,2.50000f,-527.19120f),210.72770f,34.40f,16,1.9f,4.95f);
                    Registered("yard",new Vector3(-445.28160f,1.60000f,-539.53550f),34,36.50f,17,.89f,8.04f);
                    Registered("wide",new Vector3(-493.78710f,6.00005f,-517.48000f),194.10f,44.0f,68.90f,-5.15f,-6.51f);
                    if(Environment.GetEnvironmentVariable("PCSHOP_TOWN_ORBIT")=="1")
                    {
                        var pivot=new Vector3(-438.23760f,2.50000f,-527.19120f);
                        for(int frame=0;frame<64;frame++)
                        {
                            float turn=Mathf.Sin(frame*2*Mathf.PI/64);
                            Shot(cam,"orbit/home-"+frame.ToString("D2"),pivot,210.72770f+turn*18,34.4f+turn*1.1f,16,1024);
                        }
                    }
                    var fishing=game.World.Places["life_fishing"];
                    var fishingCenter=fishing.Root.transform.TransformPoint(new Vector3(0,1,(float)fishing.Place.Depth*.48f));
                    Shot(cam,"fishing-a",fishingCenter,(float)fishing.Place.Heading+34,36,16);
                    Shot(cam,"fishing-b",fishingCenter,(float)fishing.Place.Heading-37,33,16);
                    session.Clock.Restore(session.Clock.Today,630);
                    game.Step(.03f);
                    continue;
                }
                if(i==1 || i==3)
                {
                    string id=i==1?"life_community_ferris":"life_garden_amusement";
                    var venue=game.World.Places[id].Root.transform;
                    var wheel=venue.Find("FerrisWheel");
                    Shot(cam,tag+"Wheel",wheel.position,venue.eulerAngles.y+20,29,i==1?54:96);
                    Shot(cam,tag+"WheelGate",venue.TransformPoint(new Vector3(0,2,5)),venue.eulerAngles.y+24,32,17);
                }
                foreach(var hour in new[]{(minute:1090,label:"Dusk"),(minute:1290,label:"Night")})
                {
                    session.Clock.Restore(session.Clock.Today,hour.minute);
                    game.Step(.03f);
                    Shot(cam,tag+hour.label,center,(float)site.Place.Heading+34,36,Sites[i].size);
                }
                session.Clock.Restore(session.Clock.Today,630);
                game.Step(.03f);
                foreach (var v in sites.OrderBy(v=>v.Place.Id,StringComparer.Ordinal))
                {
                    var p=v.Place;
                    var key="sites/"+tag+"-"+p.Id;
                    var pivot=v.Root.transform.TransformPoint(new Vector3(0,2.3f,(float)p.Depth*.34f));
                    Shot(cam,key,pivot,(float)p.Heading+32,36,Mathf.Clamp((float)Math.Max(p.Width,p.Depth)*.62f,9,60),512);
                    manifest.Add(string.Join("\t",Regions[i],p.Id,p.Name,p.Model,key+".png"));
                }
            }
            File.WriteAllLines(Path.Combine(Output,"地点索引.tsv"),manifest);
            foreach (var race in game.World.Root.GetComponentsInChildren<RaceTrackView>(true))
            {
                var track=race.Track;
                var entrance=game.World.Places[track.Id];
                session.ArriveAtNode(entrance.Place.NodeId);
                game.Step(.03f);
                var stand=track.Tier==0?new Vector3(-35,1,-19):new Vector3(-10,1,0);
                Shot(cam,track.Id+"A",race.transform.TransformPoint(stand),154,36,26);
                if(track.Tier>0)
                {
                    // Same lens and anchor for each revision of its concept panel. A and Front are kept for continuity.
                    float halfHeight=track.Tier==1?19.7f:track.Tier==2?18f:15.2f;
                    Shot(cam,track.Id+"Stand",race.transform.TransformPoint(stand+new Vector3(5,1,2.6f)),154,24,halfHeight);
                }
                if(track.Tier>0)
                    Shot(cam,track.Id+"Front",race.transform.TransformPoint(stand+new Vector3(6,2.3f,2)),race.transform.eulerAngles.y-26,25,14.8f);
                Shot(cam,track.Id+"B",race.transform.position,210,48,(float)Math.Max(track.Width*.38,track.Depth*.65));
                Shot(cam,track.Id+"C",entrance.Entrance+Vector3.up*2,32,34,16);
                var bend=track.Points[track.Points.Length/3];
                Shot(cam,track.Id+"D",race.transform.TransformPoint(new Vector3((float)bend.X,1,(float)bend.Y)),205,38,22);
            }
            if(baked && game.World.Root.TryGetComponent<BakedCityMap>(out var map))
                Debug.Log($"CAPTURE_MAP version={map.Version} trial={map.Trial} source={map.SourceHash}");
            CaptureWalking(game,Output);
            Debug.Log($"CONCEPT_CAMERA_CAPTURED baked={baked} sites={manifest.Count}");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    // Calibrated from fixed roof corners, pond edges, fountain centres, road junctions
    // and wheel hubs on the approved boards. Translation is in a 512 x 288 view.
    private static readonly (float yaw,float pitch,float size,float x,float y)[,] DistrictPoses =
    {
        { (0.234f,-3.365f,0.967f,0.801f,5.547f), (-1.900f,-2.129f,1.017f,-4.937f,4.462f), (-0.012f,2.093f,1.041f,-11.126f,-4.741f), (-1.679f,0.405f,0.958f,-14.593f,-8.101f) },
        { (0.468f,-1.797f,0.906f,9.116f,-1.167f), (2.449f,3.535f,1.049f,2.167f,2.788f), (0.510f,0.240f,1.039f,-3.944f,-3.146f), (-1.137f,-2.170f,0.970f,-4.208f,0.138f) },
        { (0.047f,-2.065f,0.863f,-3.365f,12.707f), (-3.547f,-0.477f,0.820f,-10.444f,-13.371f), (-0.734f,-0.801f,0.977f,-10.357f,-6.278f), (1.679f,-1.948f,1.033f,-7.496f,-7.421f) }
    };
    private static void DistrictMatch(Camera cam,int district,int view,Vector3 pivot,float yaw,float pitch,float size)
    {
        var adjust=DistrictPoses[district,view];
        yaw+=adjust.yaw;pitch+=adjust.pitch;size*=adjust.size;
        var shift=Quaternion.Euler(pitch,yaw,0)*new Vector3(-adjust.x,adjust.y,0)*(size/144f);
        string label=view<2?"street":"wide";
        Shot(cam,"match-"+(district+2).ToString("00")+"-"+label+(view%2==0?"A":"B"),pivot+shift,yaw,pitch,size);
    }

    private static void Shot(Camera cam, string name, Vector3 point, float yaw, float pitch, float size, int width=1600,float? standOff=null)
    {
        cam.orthographic=true;
        cam.orthographicSize=size;
        cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);
        cam.transform.position=point-cam.transform.forward*(standOff??Mathf.Max(30,size*2.6f));
        Save(cam,name,width);
    }

    private static void SaveWithHud(GameController game, Camera cam,string name,Canvas[] layers, bool warmup)
    {
        var prior=layers.Select(c=>(camera:c.worldCamera,distance:c.planeDistance)).ToArray();
        try
        {
            foreach(var layer in layers)
            {
                layer.gameObject.SetActive(true);
                layer.renderMode=RenderMode.ScreenSpaceCamera;
                layer.worldCamera=cam;
                layer.planeDistance=2f;
            }
            game.Ui.Update(0);
            Canvas.ForceUpdateCanvases();
            Save(cam,name,warmup:warmup);
        }
        finally
        {
            for(int i=0;i<layers.Length;i++)
            {
                layers[i].worldCamera=prior[i].camera;
                layers[i].planeDistance=prior[i].distance;
                layers[i].renderMode=RenderMode.ScreenSpaceOverlay;
                layers[i].gameObject.SetActive(false);
            }
        }
    }

    private static void Save(Camera cam, string name, int width=1600, bool warmup=false)
    {
        int height=width*9/16;
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        var previous=RenderTexture.active;
        var previousTarget=cam.targetTexture;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            cam.targetTexture=target;
            if (warmup) cam.Render();
            cam.Render();
            RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);
            image.Apply();
            string path=Path.Combine(Output,name+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            cam.targetTexture=previousTarget;
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
