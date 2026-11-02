using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class HomeSleepNavigationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static void WithHome(Action<GameSession,GameController,HomeRoomView,GameUi> action)
        {
            using(var s=HomeTests.Make())
            {
                s.EventsEnabled=false;s.Time.SetSpeed(1);
                var host=new GameObject("HomeSleepRegression");host.SetActive(false);
                var c=host.AddComponent<GameController>();var room=new HomeRoomView(host.transform);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                foreach(var property in new[]{"Session","City","HomeRoom"})
                    typeof(GameController).GetProperty(property).SetValue(c,property=="Session"?(object)s:property=="City"?s.City:(object)room);
                var ui=new GameUi(c);typeof(GameController).GetProperty("Ui").SetValue(c,ui);
                s.CityTravelRequested+=intent=>Call(c,"OnCityTravelRequested",intent);
                try{action(s,c,room,ui);}
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(host);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        private static bool BedWindow(GameUi ui)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="床"||t.text=="地铺");
        private static void Frame(GameSession s,GameController c,HomeRoomView room,float dt=.05f)
        {
            s.Tick(dt);room.SetActive(c.InHome);if(c.InHome)room.Update(s,dt,s.Clock.MinuteOfDay);
            Call(c,"UpdateHomeBedTrip",dt);Call(c,"UpdateCityTravel",dt);
        }
        [TestCase(false,1)] [TestCase(false,8)] [TestCase(true,1)] [TestCase(true,8)]
        public void HomeShortcutReachesBedFromHomeOrStreet(bool street,int speed)
        {
            WithHome((s,c,room,ui)=>
            {
                GameSessionTests.Teleport(s,s.Home.Id);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                if(street){s.Leave();s.ArriveAtNode(s.City.GetPlace("pc").NodeId);}
                s.Time.SetSpeed(speed);c.GoHomeToSleep();
                for(int i=0;i<2000&&!BedWindow(ui);i++)Frame(s,c,room);
                Assert.AreSame(s.Home,s.CurrentPlace);Assert.True(BedWindow(ui),"回家后必须继续走到床并打开睡眠选项");
                Assert.AreEqual(HomeRoomView.Bed,room.NearbyId);
            });
        }
        [Test]
        public void DoorPathCannotBeReusedAsSuccessfulBedArrivalAfterReentry()
        {
            WithHome((s,c,room,ui)=>
            {
                GameSessionTests.Teleport(s,s.Home.Id);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                int result=0;for(int i=0;i<500&&result==0;i++)result=room.WalkToDoor(.05f);
                Assert.AreEqual(1,result);room.SetActive(false);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                int bed=0;for(int i=0;i<500&&bed==0;i++){bed=room.WalkToBed(.05f);room.Update(s,.05f,s.Clock.MinuteOfDay);}
                Assert.AreEqual(1,bed);Assert.AreEqual(HomeRoomView.Bed,room.NearbyId,"门口路线不能被当成已经到床边");
            });
        }
        [Test]
        public void SwitchingFromDoorToBedReplansEvenWithoutLeavingRoom()
        {
            WithHome((s,c,room,ui)=>
            {
                GameSessionTests.Teleport(s,s.Home.Id);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                int result=0;for(int i=0;i<500&&result==0;i++)result=room.WalkToDoor(.05f);
                Assert.AreEqual(1,result);
                int bed=0;for(int i=0;i<500&&bed==0;i++){bed=room.WalkToBed(.05f);room.Update(s,.05f,s.Clock.MinuteOfDay);}
                Assert.AreEqual(1,bed);Assert.AreEqual(HomeRoomView.Bed,room.NearbyId);
            });
        }
        [Test]
        public void PauseAtArrivalKeepsSleepRequestAndResumesBedWalk()
        {
            WithHome((s,c,room,ui)=>
            {
                GameSessionTests.Teleport(s,s.Home.Id);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                c.GoHomeToSleep();s.Time.SetPaused(true);var at=room.OwnerWorld;
                for(int i=0;i<10;i++)Frame(s,c,room);
                Assert.AreEqual(at,room.OwnerWorld);Assert.True(s.CitySleepRequested);Assert.False(BedWindow(ui));
                s.Time.SetSpeed(1);for(int i=0;i<500&&!BedWindow(ui);i++)Frame(s,c,room);
                Assert.True(BedWindow(ui));Assert.AreEqual(HomeRoomView.Bed,room.NearbyId);
            });
        }
    }
}
