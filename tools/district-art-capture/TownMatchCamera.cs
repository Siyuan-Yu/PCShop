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
public static class TownMatchCamera
{
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
                    EditorApplication.delayCall += () => Capture(game,true);
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

    private static void Capture(GameController game, bool baked)
    {
        try
        {
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
                Save(cam,"play"+tag);
                var center = site.Root.transform.TransformPoint(new Vector3(0,2.2f,(float)site.Place.Depth*.25f));
                Shot(cam,tag+"A",center,(float)site.Place.Heading+34,36,Sites[i].size);
                Shot(cam,tag+"B",center,(float)site.Place.Heading-37,31,Sites[i].size*.91f);
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
                }
                if(i==0) continue;
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
                    Shot(cam,track.Id+"Front",race.transform.TransformPoint(stand+new Vector3(6,2.3f,2)),race.transform.eulerAngles.y-26,25,14.8f);
                Shot(cam,track.Id+"B",race.transform.position,210,48,(float)Math.Max(track.Width*.38,track.Depth*.65));
                Shot(cam,track.Id+"C",entrance.Entrance+Vector3.up*2,32,34,16);
                var bend=track.Points[track.Points.Length/3];
                Shot(cam,track.Id+"D",race.transform.TransformPoint(new Vector3((float)bend.X,1,(float)bend.Y)),205,38,22);
            }
            Debug.Log($"CONCEPT_CAMERA_CAPTURED baked={baked} sites={manifest.Count}");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Shot(Camera cam, string name, Vector3 point, float yaw, float pitch, float size, int width=1600)
    {
        cam.orthographic=true;
        cam.orthographicSize=size;
        cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);
        cam.transform.position=point-cam.transform.forward*Mathf.Max(30,size*2.6f);
        Save(cam,name,width);
    }

    private static void Save(Camera cam, string name, int width=1600)
    {
        int height=width*9/16;
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
        var previous=RenderTexture.active;
        var previousTarget=cam.targetTexture;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            cam.targetTexture=target;
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
