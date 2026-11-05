using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class QuarterlyExpoSceneTests
    {
        private static GameSession Make()=>GameSession.CreateCityLifePreset(CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)),"quarter_expo");
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,id==null?null:s.City.GetPlace(id));
        private static void Day(GameSession s,int day,int minute=540)=>s.Clock.Restore(new GameDate(day),minute);
        private static BrandExhibit Book(GameSession s,int size=0)
        {
            if(size>0)Assert.True(s.UpgradeKolBrand("acceptance-expo"));if(size>1)Assert.True(s.UpgradeKolBrand("acceptance-expo"));
            var kols=KolMarket.All.Take(GameSession.ExpoMinKols(size)).Select(k=>k.Id).ToArray();foreach(string id in kols)Assert.True(s.SignKolLive("acceptance-expo",id));
            Assert.True(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,size,PartCatalog.All.Take(GameSession.ExpoMinProducts(size)).Select(p=>p.Id).ToArray(),kols));
            return s.BrandBusiness.Exhibits.Single();
        }
        private static void Arrive(GameSession s,BrandExhibit e)
        {
            Day(s,e.Day);At(s,"life_expo");s.Player.Energy=s.Player.MaxEnergy;s.Player.Health=100;
            foreach(string id in e.KolIds){var p=s.CityPersonState(id);p.LocationPlaceId="life_expo";p.Route=null;p.ActivityId=null;p.VisitId=null;}
        }
        private static QuarterlyExpoView View(GameSession s,out GameObject root)
        {
            root=new GameObject("ExpoSceneTest");root.transform.position=new Vector3(111,0,222);root.transform.rotation=Quaternion.Euler(0,98.5f,0);
            var p=s.City.GetPlace("life_expo");CityVenueView.Build(root.transform,p,(float)p.Width,(float)p.Depth);return root.GetComponent<QuarterlyExpoView>();
        }
        [Test] public void SignedEndorsersTravelToOpeningAndAppearOnceOnTheBrandStage()
        {
            using (var s = Make())
            {
                var e = Book(s);
                foreach (string id in new[] { "momo", "ake" })
                {
                    Assert.True(s.SignBrandDeal(e.BrandId, id, "endorse", false));
                    var person = s.CityPersonState(id);
                    var start = s.City.GetPlace(GameSession.CommunityMcnId);
                    var position = s.StandOf(start);
                    person.LocationPlaceId = start.Id;
                    person.X = position.X; person.Y = position.Y;
                    person.Route = null; person.ActivityId = null; person.VisitId = null;
                }
                CollectionAssert.AreEquivalent(new[] { "momo", "ake" }, s.ExpoEndorsers(e));
                Day(s, e.Day, 359);
                s.AdvanceClock(1);
                Assert.AreEqual("life_expo", s.CityPersonState("ake").Route?.TargetPlaceId);
                s.AdvanceClock(180);
                Assert.True(s.ExpoPersonArrived("ake"));
                Assert.True(s.ExpoPersonArrived("momo"));
                At(s, "life_expo");
                s.Player.Energy = s.Player.MaxEnergy;
                s.Player.Health = 100;
                Assert.True(s.StartQuarterlyExpo(e.Id));
                var view = View(s, out var root);
                try
                {
                    view.Sync(s, 0, true);
                    var hosts = view.LiveRoot.GetComponentsInChildren<ExpoKolMarker>();
                    CollectionAssert.AreEquivalent(new[] { "momo", "ake" }, hosts.Select(host => host.PersonId));
                    var endorser = hosts.Single(host => host.PersonId == "ake");
                    float booth = endorser.transform.localPosition.z;
                    Assert.True(view.FocusPerson("ake"));
                    Assert.AreEqual("person:ake", view.FocusZone);
                    for (int i = 0; i < GameSession.ExpoEndorserStageDecision; i++) Assert.True(s.ChooseExpoAction(e.Id, 0));
                    view.Sync(s, 0, true);
                    Assert.AreEqual("stage", view.FocusZone);
                    Assert.Greater(endorser.transform.localPosition.z, booth);
                    StringAssert.Contains(s.CityPerson("ake").Name, s.CurrentExpoMoment(e.Id).Prompt);
                    int trust = s.ActiveBrandExpo.Trust;
                    Assert.True(s.ChooseExpoAction(e.Id, 1));
                    Assert.Greater(s.ActiveBrandExpo.Trust, trust);
                    StringAssert.Contains("品牌舞台介绍", s.ActiveBrandExpo.Journal.Last());
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        [Test] public void ActualVenueAndExhibitionDateBothGateThePopulatedScene()
        {
            using(var s=Make())
            {
                var view=View(s,out var root);try
                {
                    At(s,"life_expo");view.Sync(s,0,true);Assert.False(view.Open);Assert.False(view.LiveRoot.activeSelf);Assert.Zero(view.BuildVersion);
                    Day(s,75);At(s,null);view.Sync(s,0,true);Assert.False(view.Open);
                    At(s,"life_expo");view.Sync(s,0,true);Assert.True(view.Open);Assert.True(view.LiveRoot.activeSelf);Assert.NotNull(view.LiveRoot.transform.Find("BrandStage"));Assert.Greater(view.VisibleVisitors,0);
                    Day(s,79);view.Sync(s,0,true);Assert.False(view.Open);Assert.True(root.transform.Find("ExpoClosedHall").gameObject.activeSelf);
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [TestCase(0,1,1)] [TestCase(1,4,5)] [TestCase(2,6,8)]
        public void SceneUsesTheBookedBoothReservedProductsAndActualSignedPeople(int size,int count,int hosts)
        {
            using(var s=Make())
            {
                var e=Book(s,size);if(size>0)Assert.True(s.CommissionExpoPoster(e.Id,"life_designer"));Arrive(s,e);
                var view=View(s,out var root);try
                {
                    view.Sync(s,0,true);var booth=view.LiveRoot.transform.Find("PlayerBooth");
                    var products=booth.GetComponentsInChildren<ExpoProductMarker>().Where(p=>p.transform.parent.name.StartsWith("ExhibitPod_")).ToArray();
                    Assert.AreEqual(count,products.Length);CollectionAssert.AreEquivalent(e.Products.Select(p=>p.ProductId),products.Select(p=>p.ProductId));
                    Assert.AreEqual(10+size*4,booth.Find("BoothCarpet").localScale.x,.001f);Assert.AreEqual(hosts,view.VisibleKols);
                    CollectionAssert.AreEquivalent(e.KolIds,view.LiveRoot.GetComponentsInChildren<ExpoKolMarker>().Select(k=>k.PersonId));
                    Assert.AreEqual(size>0,booth.Find("CommissionedPoster")!=null);
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test] public void HostsAreShownOnlyAfterRealArrivalAndViewDoesNotAdvanceTheSimulation()
        {
            using(var s=Make())
            {
                var e=Book(s);Arrive(s,e);var p=s.CityPersonState(e.KolIds[0]);p.LocationPlaceId=GameSession.CommunityMcnId;
                var view=View(s,out var root);try
                {
                    string before=JsonUtility.ToJson(s.CreateSave());view.Sync(s,0,true);for(int n=0;n<20;n++)view.Sync(s,.05f);
                    Assert.Zero(view.VisibleKols);Assert.AreEqual(before,JsonUtility.ToJson(s.CreateSave()));
                    p.LocationPlaceId="life_expo";view.Sync(s,0,true);Assert.AreEqual(1,view.VisibleKols);
                    p.ActivityId="elsewhere";view.Sync(s,0,true);Assert.Zero(view.VisibleKols);
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test] public void ChoicesChangeTheEventCrowdAndStockWithoutRebuildingTheHall()
        {
            using(var s=Make())
            {
                var e=Book(s);Arrive(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));var view=View(s,out var root);
                try
                {
                    view.Sync(s,0,true);int build=view.BuildVersion;Assert.AreEqual("queue",view.EventEffect);Assert.AreEqual("aisle",view.FocusZone);
                    Assert.True(s.ChooseExpoAction(e.Id,1));view.Sync(s,0,true);
                    Assert.AreEqual(build,view.BuildVersion);Assert.AreEqual("performance",view.EventEffect);Assert.AreEqual("stage",view.FocusZone);
                    Assert.True(view.LiveRoot.transform.Find("CurrentExpoEvent/Event_performance").gameObject.activeSelf);
                    Assert.False(view.LiveRoot.transform.Find("CurrentExpoEvent/Event_queue").gameObject.activeSelf);
                    var actual=s.ActiveBrandExpo.Products.Single();var pod=view.LiveRoot.transform.Find("PlayerBooth/ExhibitPod_"+actual.ProductId);
                    Assert.True(pod.GetComponentsInChildren<Text>().Any(t=>t.text=="余 "+(100-actual.Sold)+" / 100"));
                    for(int n=0;n<6;n++){Assert.True(s.ChooseExpoAction(e.Id,0));view.Sync(s,0,true);}
                    actual=s.ActiveBrandExpo.Products.Single();Assert.AreEqual((int)Math.Ceiling((100-actual.Sold)/25d),pod.Cast<Transform>().Count(t=>t.name=="ReservedCarton"&&t.gameObject.activeSelf));
                    Assert.LessOrEqual(view.VisibleVisitors,26);Assert.AreEqual(build,view.BuildVersion);
                    for(int n=7;n<24;n++)Assert.True(s.ChooseExpoAction(e.Id,0));view.Sync(s,0,true);
                    Assert.True(s.BrandBusiness.Exhibits.Single().StockReturned);
                    Assert.Zero(pod.Cast<Transform>().Count(t=>t.name=="ReservedCarton"&&t.gameObject.activeSelf));
                    Assert.IsNull(view.EventEffect);Assert.AreEqual(build,view.BuildVersion);
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test] public void ReloadReconstructsTheCurrentStageAndCannotAdvanceFromElsewhere()
        {
            using(var s=Make())
            {
                var e=Book(s);Arrive(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));Assert.True(s.ChooseExpoAction(e.Id,0));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var view=View(loaded,out var root);try
                    {
                        view.Sync(loaded,0,true);Assert.AreEqual("performance",view.EventEffect);Assert.AreEqual("stage",view.FocusZone);
                        long cash=loaded.Ledger.Balance.Cents,time=loaded.Clock.Now.TotalMinutes;
                        At(loaded,null);Assert.False(loaded.ChooseExpoAction(e.Id,0));Assert.AreEqual(1,loaded.ActiveBrandExpo.Decision);Assert.AreEqual(cash,loaded.Ledger.Balance.Cents);Assert.AreEqual(time,loaded.Clock.Now.TotalMinutes);
                        view.Sync(loaded,0,true);Assert.False(view.Open);
                        At(loaded,"life_expo");Day(loaded,79);Assert.False(loaded.ChooseExpoAction(e.Id,0));Assert.AreEqual(1,loaded.ActiveBrandExpo.Decision);
                    }finally{UnityEngine.Object.DestroyImmediate(root);}
                }
            }
        }
        [Test] public void ReusingTheSceneAfterChangingSavesBuildsOnlyOneHallAndLeavesOldCrowdsInactive()
        {
            using(var s=Make())using(var second=Make())
            {
                var e=Book(s);Arrive(s,e);var view=View(s,out var root);
                try
                {
                    view.Sync(s,0,true);int build=view.BuildVersion;
                    var other=Book(second);At(second,null);view.Sync(second,0,true);Arrive(second,other);view.Sync(second,0,true);
                    Assert.AreEqual(build+1,view.BuildVersion);Assert.AreEqual(1,view.LiveRoot.transform.Cast<Transform>().Count(t=>t.name=="PlayerBooth"));
                    Assert.AreEqual(26,view.LiveRoot.transform.Cast<Transform>().Count(t=>t.name.StartsWith("ExpoVisitor_")));
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
        }
        [Test] public void ScenePickingAndFocusRespectTheRotatedWorldVenue()
        {
            using(var s=Make())
            {
                Day(s,75);At(s,"life_expo");var view=View(s,out var root);
                var camRoot=new GameObject("ExpoCameraTest");var camera=camRoot.AddComponent<Camera>();camera.enabled=false;
                try
                {
                    view.Sync(s,0,true);Physics.SyncTransforms();Vector3 point=view.WorldPoint("stage");
                    Assert.AreEqual("stage",view.Pick(new Ray(point+Vector3.up*15,Vector3.down)));
                    foreach(string zone in new[]{"overview","stage","booth","reception"})
                    {
                        view.Focus(zone);float size=view.CameraSize(16f/9);var rotation=Quaternion.Euler(52,root.transform.eulerAngles.y,0);
                        var target=view.WorldPoint(zone)+Vector3.up;var focus=target+rotation*Vector3.right*(size*(16f/9)*.32f);
                        camera.orthographic=true;camera.aspect=16f/9;camera.orthographicSize=size;camera.transform.SetPositionAndRotation(focus-rotation*Vector3.forward*120,rotation);
                        var screen=camera.WorldToViewportPoint(target);Assert.That(screen.x,Is.InRange(.1f,.6f));Assert.That(screen.y,Is.InRange(.1f,.9f));
                    }
                }finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camRoot);}
            }
        }
        [Test] public void OnsiteHudLeavesTheSceneUncoveredAndRemoteBrandAccessCannotOpenIt()
        {
            using(var s=Make())
            {
                var e=Book(s);var original=UnityEngine.EventSystems.EventSystem.current;var root=new GameObject("ExpoSideHudTest");root.SetActive(false);var ctrl=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(ctrl,s);typeof(GameController).GetProperty("City").SetValue(ctrl,s.City);var ui=new GameUi(ctrl);
                try
                {
                    ui.OpenQuarterlyExpo(e.Id);Assert.False(ui.ExpoHudOpen);Assert.False(s.Time.IsHeld);
                    Arrive(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));ui.OnEnteredPlace(s.CurrentPlace);Assert.True(ui.ExpoHudOpen);Assert.True(s.Time.IsHeld);
                    var windows=(List<Window>)typeof(GameUi).GetField("_windows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);var w=windows.Single();
                    Assert.Zero(w.Root.GetComponent<Image>().color.a);Assert.False(w.Root.GetComponent<Image>().raycastTarget);
                    var panel=(RectTransform)w.Root.Find("Panel");Assert.GreaterOrEqual(panel.anchorMin.x,.68f);Assert.False(panel.GetComponent<WindowFit>().enabled);
                    Assert.AreEqual(3,w.Root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ExpoChoice_")));Assert.False(w.Closable);
                    Canvas.ForceUpdateCanvases();Assert.Greater(panel.rect.width,400);Assert.Greater(panel.rect.height,500);
                    Assert.True(ui.CloseTopWindow()==false);Assert.True(ui.ExpoHudOpen);
                }finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);
                    if(original==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        [Test] public void ControllerFramesTheRealVenueMovesTheExistingPlayerAndRestoresTheStreetView()
        {
            using(var s=Make())
            {
                var e=Book(s);Arrive(s,e);var view=View(s,out var hall);var root=new GameObject("ExpoControllerTest");root.SetActive(false);
                var actors=new GameObject("ExpoTestActors");var camRoot=new GameObject("ExpoTestCamera");var camera=camRoot.AddComponent<Camera>();camera.enabled=false;camera.aspect=16f/9;
                float oldYaw=CameraRig.Yaw,oldPitch=CameraRig.Pitch;
                try
                {
                    var ctrl=root.AddComponent<GameController>();var world=new StreetWorldBuilder(s.City,1);
                    world.Places["life_expo"]=new PlaceVisual{Place=s.City.GetPlace("life_expo"),Root=hall,VenueInterior=hall};
                    var rig=new CameraRig(camera);var avatar=new AvatarView(actors.transform);
                    void Prop(string name,object value)=>typeof(GameController).GetProperty(name).SetValue(ctrl,value);
                    void Field(string name,object value)=>typeof(GameController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ctrl,value);
                    Prop("Session",s);Prop("City",s.City);Prop("World",world);Prop("Cam",camera);Field("_rig",rig);Field("_avatar",avatar);
                    var sync=typeof(GameController).GetMethod("SyncExpoView",BindingFlags.Instance|BindingFlags.NonPublic);var realPosition=s.PlayerPosition;
                    avatar.Update(s,.1f);sync.Invoke(ctrl,new object[]{.1f});ctrl.SnapCamera();
                    Assert.Less(Vector3.Distance(view.PlayerPoint("overview"),avatar.Position),.01f);
                    Assert.Greater(camera.orthographicSize,20);Assert.That(camera.WorldToViewportPoint(view.WorldPoint("overview")).x,Is.InRange(.1f,.6f));
                    ctrl.FocusExpo("stage");for(int n=0;n<120;n++){avatar.Update(s,.1f);sync.Invoke(ctrl,new object[]{.1f});}
                    Assert.Less(Vector3.Distance(view.PlayerPoint("stage"),avatar.Position),.01f);Assert.AreEqual(realPosition.X,s.PlayerPosition.X);Assert.AreEqual(realPosition.Y,s.PlayerPosition.Y);
                    Assert.AreEqual(1,actors.GetComponentsInChildren<FigureMotion>().Count(m=>m.PresentationAnimation));
                    At(s,null);avatar.Update(s,.1f);sync.Invoke(ctrl,new object[]{.1f});ctrl.SnapCamera();
                    Assert.False(view.Open);Assert.AreEqual(16,camera.orthographicSize);Assert.AreEqual(StreetWorldBuilder.ToWorld(s.PlayerPosition),avatar.Position);
                    Assert.False(actors.GetComponentInChildren<FigureMotion>().PresentationAnimation);
                }finally
                {
                    UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(hall);UnityEngine.Object.DestroyImmediate(actors);UnityEngine.Object.DestroyImmediate(camRoot);
                    CameraRig.Yaw=oldYaw;CameraRig.Pitch=oldPitch;
                }
            }
        }
    }
}
