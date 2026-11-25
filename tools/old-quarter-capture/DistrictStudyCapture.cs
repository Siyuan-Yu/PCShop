using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Core.World;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.EditorTools
{
    [InitializeOnLoad]
    static class DistrictStudyCapture
    {
        const string Key="OldQuarter.DistrictStudy";
        static string folder;
        static GameController game;
        static double deadline;
        static int phase,index;
        static PlaceVisual[] plots;
        static List<string> cameras=new List<string>();
        static List<string> locations=new List<string>();
        static List<Action> shots=new List<Action>();
        static string S(float v)=>v.ToString("F5",CultureInfo.InvariantCulture);

        static DistrictStudyCapture()
        {
            if(!SessionState.GetBool(Key,false))return;
            folder=Environment.GetEnvironmentVariable("PCSHOP_DISTRICT_DEST");
            Directory.CreateDirectory(folder);
            SaveStore.DirectoryOverride=Path.GetFullPath("work/district-concept-disposable");
            deadline=EditorApplication.timeSinceStartup+430;
            EditorApplication.update+=Update;
        }
        public static void Run()
        {
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }
        static void Update()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("District concept stage "+phase);
                if(phase==0)
                {
                    game=UnityEngine.Object.FindObjectOfType<GameController>();
                    if(!game || game.Ui==null)return;
                    game.NewGame();phase++;return;
                }
                if(phase==1)
                {
                    if(game.World==null || game.Session==null || game.Cam==null || !game.World.Places.ContainsKey("home"))return;
                    var session=game.Session;
                    game.Ui.CloseAllWindows();
                    session.Flags.Remove("tutorial:opening-meal-pending");session.Flags.Remove("tutorial:opening-agency-pending");session.Leave();
                    for(int d=session.Clock.Today.TotalDays;d<80;d++)
                    {
                        session.Clock.Restore(new PCShopSell.Core.Time.GameDate(d),9*60+14);
                        if(!session.CityIsRaining)break;
                    }
                    session.Time.SetPaused(true);game.enabled=false;
                    var sync=typeof(GameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic);
                    for(int i=0;i<34;i++)sync.Invoke(game,new object[]{1f/30,false});
                    Time.timeScale=0;
                    Plan();phase++;return;
                }
                if(index<shots.Count){shots[index++]();return;}
                File.WriteAllLines(Path.Combine(folder,"cameras.tsv"),cameras);
                File.WriteAllLines(Path.Combine(folder,"projections.tsv"),locations);
                Finish(0);
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        static void Plan()
        {
            plots=game.World.Places.Values.Where(p=>game.City.AgencyDistrictFor(p.Place.StreetId)?.id=="outskirts").OrderBy(p=>p.Place.Id).ToArray();
            var rows=new List<string>{"id\tname\tstreet\tkind\ttier\tsize\tx\tz\tyaw\twidth\tdepth\tshell"};
            foreach(var plot in plots)
            {
                var p=plot.Place;
                float w=(float)p.Width,d=(float)p.Depth;
                if(GameSession.IsCommercialPremises(p)){var room=RoomSpec.ForShop(p.Size);w=(float)room.Width;d=(float)room.Depth;}
                string[] options={"Walkup","YardWalkup","LaneHouse","Shopfront","RouteGrayHouse","RouteSageHouse","RouteCreamHouse","RouteStation","RouteBlueShop","RouteGrocery"};
                string shell=options.FirstOrDefault(k=>plot.Root.transform.Find(k)!=null)??p.Model;
                rows.Add(string.Join("\t",p.Id,p.Name,p.StreetId,p.Kind.ToString(),p.Tier.ToString(),p.Size.ToString(),S(plot.Entrance.x),S(plot.Entrance.z),S(plot.Root.transform.eulerAngles.y),S(w),S(d),shell));
            }
            File.WriteAllLines(Path.Combine(folder,"lots.tsv"),rows);
            cameras.Add("name\tfocusX\tfocusY\tfocusZ\tyaw\tpitch\tsize\twidth\theight");
            locations.Add("view\tid\tx\ty");
            var houses=plots.Where(p=>p.Place.Kind==PlaceKind.Home).ToArray();
            var wholesale=plots.Where(p=>p.Place.StreetId=="s_electronics").ToArray();
            var retail=plots.Where(p=>p.Place.StreetId=="s_start" && p.Entrance.z< -460).ToArray();
            var home=game.World.Places["home"];
            var housing=game.World.Places["residence_s_oldhomes_04"];
            var start=home.Root.transform;
            Vector3 center=new Vector3(-482,4,-523);
            shots.Add(()=>Fit("housing-a",houses,193,45,2880));shots.Add(()=>Fit("housing-b",houses,13,45,2880));
            shots.Add(()=>Shot("near-a",center,start.eulerAngles.y+52.6f,35,31.5f,2400));
            shots.Add(()=>Shot("near-b",center,start.eulerAngles.y+142.6f,35,31.5f,2400));
            shots.Add(()=>Shot("near-c",center,start.eulerAngles.y+232.6f,35,31.5f,2400));
            shots.Add(()=>Shot("rental-close",home.Entrance+Vector3.up*2.5f,start.eulerAngles.y+52.6f,34,16,2400));
            var homeSix=game.World.Places["residence_s_oldhomes_06"];
            shots.Add(()=>Shot("large-close",homeSix.Entrance+Vector3.up*3.5f+homeSix.Root.transform.forward*2,homeSix.Root.transform.eulerAngles.y+48,34,12,2000));
            shots.Add(()=>Shot("small-close",home.Entrance+Vector3.up*1.8f+start.forward*2,start.eulerAngles.y+52.6f,32,9,2000));
            Vector3 yard(float x,float z)=>home.Entrance+Quaternion.Euler(0,-21,0)*new Vector3(x,1.6f,z);
            shots.Add(()=>Shot("yard-laundry",yard(-54,34),190,40,20,2400));
            shots.Add(()=>Shot("yard-bikes",yard(-26,21),25,39,17,2400));
            shots.Add(()=>Shot("yard-stop",yard(-17,-11),start.eulerAngles.y+47,36,19,2400));
            shots.Add(()=>Shot("yard-home",yard(-11,-9),34,36,17,2400));
            shots.Add(()=>Shot("gameplay",home.Entrance+Vector3.up*2.5f,start.eulerAngles.y+52.6f,34,17,1920));
            int daylightMinute=game.Session.Clock.MinuteOfDay;
            foreach(var moment in new[]{(1090,"dusk"),(1290,"night"),(daylightMinute,"day-restored")})
            {
                shots.Add(()=>{
                    var session=game.Session;
                    session.Clock.Restore(session.Clock.Today,moment.Item1);
                    var sync=typeof(GameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic);
                    sync.Invoke(game,new object[]{1f/30,false});
                    Shot(moment.Item2,home.Entrance+Vector3.up*2.5f,start.eulerAngles.y+52.6f,34,16,1920);
                });
            }
            shots.Add(VisitHouses);
            foreach (string id in new[]{"home","residence_s_oldhomes_06"})
            {
                var chosen=game.City.GetPlace(id);
                shots.Add(()=>game.Ui.GetType().GetMethod("GoToPropertyAgency",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(game.Ui,new object[]{chosen}));
                shots.Add(()=>Shot("agency-"+chosen.Id,home.Entrance,start.eulerAngles.y+52.6f,34,17,1920));
            }
        }
        static void VisitHouses()
        {
            var session = game.Session;
            var sync = typeof(GameController).GetMethod("SyncViews",BindingFlags.Instance|BindingFlags.NonPublic);
            var rows = new List<string>();
            foreach (var id in new[]{"home","residence_s_oldhomes_06","residence_s_oldhomes_07"})
            {
                var v=game.World.Places[id];
                session.Leave();
                session.ArriveAtNode(v.Place.NodeId);
                bool entered = session.Enter(v.Place);
                sync.Invoke(game,new object[]{1f/30,false});
                var room = (game.InHome ? game.HomeRoom : game.VisitRoom)?.Room;
                var marker = v.Root.GetComponent<PlaceMarker>();
                rows.Add($"{id} | {v.Place.HousingForm} | entered={entered} | room={room?.Width}x{room?.Depth} | marker={marker?.PlaceId} | click={v.Root.GetComponent<BoxCollider>()!=null}");
                session.Leave();
            }
            var agency = game.City.GetPlace("agency");
            session.ArriveAtNode(agency.NodeId);
            session.Enter(agency);
            var flags = BindingFlags.NonPublic|BindingFlags.Instance;
            var ui = game.Ui;
            var detail = ui.GetType().GetMethod("BuildResidentialDetail",flags);
            foreach (string id in new[]{"home","residence_s_oldhomes_06"})
            {
                var panel=new GameObject("Agency preview",typeof(RectTransform));
                detail.Invoke(ui,new object[]{panel.transform,game.City.GetPlace(id),1,null});
                var labels=panel.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t=>t.text);
                rows.Add("agency "+id+" | "+string.Join(" / ",labels.Where(t=>t.Contains("㎡"))));
                UnityEngine.Object.DestroyImmediate(panel);
            }
            rows.Add("baked instance = "+(game.World.Root.GetComponent<BakedCityMap>()!=null));
            foreach (var form in new[]{ResidenceForm.Walkup,ResidenceForm.LaneHouse})
            {
                var available = game.City.OfKind(PlaceKind.Home).FirstOrDefault(p=>p.HousingForm==form && session.ResidentialTradeBlocker(p.Id,false)==null);
                if (available==null) { rows.Add("no vacant "+form+" | "+string.Join(" / ",game.City.OfKind(PlaceKind.Home).Where(p=>p.HousingForm==form).Select(p=>session.ResidentialTradeBlocker(p.Id,false)).Distinct()));continue; }
                bool signed=session.RentResidentialHome(available.Id);
                session.Leave();session.ArriveAtNode(available.NodeId);
                bool entered=session.Enter(available);
                sync.Invoke(game,new object[]{1f/30,false});
                rows.Add($"lease {available.Id} | signed={signed} | entered={entered} | interior={game.HomeRoom.Room.Width}x{game.HomeRoom.Room.Depth}");
                session.Leave();session.ArriveAtNode(agency.NodeId);session.Enter(agency);
            }
            File.WriteAllLines(Path.Combine(folder,"visit.txt"),rows);
        }
        static void Fit(string name,PlaceVisual[] group,float yaw,float pitch,int width)
        {
            var q=Quaternion.Euler(pitch,yaw,0);var inverse=Quaternion.Inverse(q);
            var b=new Bounds();bool first=true;
            foreach(var p in group)
            {
                float w=(float)p.Place.Width,d=(float)p.Place.Depth;
                foreach(float x in new[]{-w/2,w/2})foreach(float z in new[]{-1,d})foreach(float y in new[]{0f,12f})
                {
                    var v=inverse*p.Root.transform.TransformPoint(new Vector3(x,y,z));
                    if(first){b=new Bounds(v,Vector3.zero);first=false;}else b.Encapsulate(v);
                }
            }
            Shot(name,q*b.center,yaw,pitch,Mathf.Max(b.extents.y,b.extents.x*9/16)*1.09f+3,width);
        }
        static void Shot(string name,Vector3 focus,float yaw,float pitch,float size,int width)
        {
            var cam=game.Cam;int height=width*9/16;

            cam.orthographic=true;cam.orthographicSize=size;cam.aspect=16f/9f;
            cam.nearClipPlane=.2f;cam.farClipPlane=2400;
            cam.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            cam.transform.position=focus-cam.transform.forward*(size*1.12f+27);

            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
            var old=RenderTexture.active;cam.targetTexture=rt;
            bool showHud=name=="gameplay" || name.StartsWith("agency-");
            var hud=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>showHud && c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var camerasBefore=hud.Select(c=>c.worldCamera).ToArray();
            var distances=hud.Select(c=>c.planeDistance).ToArray();
            foreach(var canvas in hud){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=2f;}
            if(hud.Length>0){game.Ui.Update(0);Canvas.ForceUpdateCanvases();}
            cam.Render();cam.Render();RenderTexture.active=rt;
            if(name=="rental-close")
            {
                var urp=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                var sun=UnityEngine.Object.FindObjectsOfType<Light>().Where(l=>l.type==LightType.Directional && l.shadows!=LightShadows.None).First();
                File.WriteAllText(Path.Combine(folder,"lighting.txt"), $"{urp.name} / {SystemInfo.graphicsDeviceType} / {QualitySettings.activeColorSpace} / {sun.shadows} / strength {sun.shadowStrength} / {urp.mainLightShadowmapResolution} / cascades {urp.shadowCascadeCount} / post {cam.GetUniversalAdditionalCameraData().renderPostProcessing} / grass {Resources.Load<Material>("OldQuarter/RouteGrassPaint").mainTexture.name}");
            }
            for(int i=0;i<hud.Length;i++){hud[i].worldCamera=camerasBefore[i];hud[i].planeDistance=distances[i];hud[i].renderMode=RenderMode.ScreenSpaceOverlay;}
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
            cameras.Add(string.Join("\t",name,S(focus.x),S(focus.y),S(focus.z),S(yaw%360),S(pitch),S(size),width.ToString(),height.ToString()));
            foreach(var p in plots)
            {
                var v=cam.WorldToViewportPoint(p.Entrance);
                locations.Add(string.Join("\t",name,p.Place.Id,S(v.x),S(v.y)));
            }
            Debug.Log("DISTRICT_VIEW "+name);
        }
        static void Finish(int code)
        {
            SessionState.EraseBool(Key);EditorApplication.update-=Update;EditorApplication.Exit(code);
        }
    }
}
