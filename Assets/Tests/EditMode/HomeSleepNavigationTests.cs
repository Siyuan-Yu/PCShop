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
        private static void WithHome(Action<GameSession,GameController,HomeRoomView,GameUi> action, GameSession session = null)
        {
            using(var s=session ?? HomeTests.Make())
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
        private static bool BedWindow(GameUi ui)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.name=="SleepWakePreview");
        private static void Frame(GameSession s,GameController c,HomeRoomView room,float dt=.05f)
        {
            s.Tick(dt);room.SetActive(c.InHome);if(c.InHome)room.Update(s,dt,s.Clock.MinuteOfDay);
            if (c.InVisit) c.VisitRoom.UpdateVisit(s);
            Call(c,"UpdateHomeBedTrip",dt);Call(c,"UpdateCityTravel",dt);
        }
        [Test]
        public void DropdownReachesAnOwnedVacantHomesBedAndCanSleepThere()
        {
            var session = DistrictProgressionTests.Make();
            session.DebugAddCash(1000000);
            typeof(GameSession).GetField("_district", Private).SetValue(session, new DistrictProgress { UnlockedTier = 3, Chapter = 3 });
            var home = session.Home;
            var inhabited = session.ResidentialPlaces.Where(p => session.ResidentialTenants(p.Id).Length > 0).Take(2).ToArray();
            var vacant = inhabited[0];
            var occupied = inhabited[1];
            foreach (var tenant in session.ResidentialTenants(vacant.Id))
                session.CityPersonState(tenant.PersonId).HomePlaceId = occupied.Id;
            foreach (var residence in new[] { vacant, occupied })
            {
                GameSessionTests.Teleport(session, session.AgencyFor(residence).Id);
                Assert.True(session.BuyResidentialHome(residence.Id, false, false));
            }
            GameSessionTests.Teleport(session, home.Id);
            session.Leave();

            WithHome((s, c, room, ui) =>
            {
                var visit = new HomeRoomView(c.transform);
                typeof(GameController).GetProperty("VisitRoom").SetValue(c, visit);
                var shortcuts = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Dropdown>()).ToArray();
                var dropdown = shortcuts.Single(d => d.name == "ShortcutArrow_4");
                CollectionAssert.Contains(shortcuts.Single(d => d.name == "ShortcutArrow_1").options.Select(o => o.text).ToArray(), "电子批发市场");
                CollectionAssert.Contains(shortcuts.Single(d => d.name == "ShortcutArrow_2").options.Select(o => o.text).ToArray(), s.City.GetPlace("agency").Name);
                var button = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>())
                    .Single(b => b.name == "ShortcutGo_4");
                var caption = button.GetComponentInChildren<Text>();
                Assert.AreEqual("前往 " + s.HomeTitle, caption.text);
                Assert.AreEqual(2, dropdown.options.Count);
                Assert.AreEqual(s.HomeTitle + " · 主住所", dropdown.options[0].text);
                Assert.AreEqual(VerticalWrapMode.Truncate, dropdown.itemText.verticalOverflow);
                var tip = button.GetComponent<TipTarget>();
                StringAssert.Contains(s.City.StreetName(s.Home.StreetId), tip.Text());
                int index = dropdown.options.FindIndex(o => o.text == vacant.Name);
                Assert.Greater(index, 0);
                dropdown.value = index;
                Assert.AreEqual("前往 " + vacant.Name, caption.text);
                Assert.AreEqual(index, dropdown.value);
                Assert.IsNull(s.CityIntent);
                Assert.IsNull(s.Trip);
                Assert.False(s.CitySleepRequested);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    Assert.AreEqual(vacant.Id, loaded.CityHomeShortcut);

                s.Time.SetSpeed(8);
                button.onClick.Invoke();
                for (int i = 0; i < 3000 && !BedWindow(ui); i++) Frame(s, c, room);

                Assert.AreSame(vacant, s.CurrentPlace);
                Assert.True(BedWindow(ui));
                Assert.True(s.Nap());
                Assert.GreaterOrEqual(s.SleepHoursLast24, 2);
                Assert.AreSame(home, s.Home);
            }, session);
        }
        [TestCase(false,1)] [TestCase(false,8)] [TestCase(true,1)] [TestCase(true,8)]
        public void HomeShortcutReachesBedFromHomeOrStreet(bool street,int speed)
        {
            WithHome((s,c,room,ui)=>
            {
                GameSessionTests.Teleport(s,s.Home.Id);room.SetActive(true);room.Update(s,0,s.Clock.MinuteOfDay);
                if(street){s.Leave();s.ArriveAtNode(s.City.GetPlace("pc").NodeId);}
                s.Time.SetSpeed(speed);s.Time.SetPaused(true);c.GoHomeToSleep();
                Assert.False(s.Time.IsUserPaused);Assert.AreEqual(speed,s.Time.Speed);
                for(int i=0;i<2000&&!BedWindow(ui);i++)Frame(s,c,room);
                Assert.AreSame(s.Home,s.CurrentPlace);Assert.True(BedWindow(ui),"回家后必须继续走到床并打开睡眠选项");
                Assert.AreEqual(HomeRoomView.Bed,room.NearbyId);
            });
        }

        [Test]
        public void WalkingToBedCoversTheSameDistanceWithCoarseAndFineFrames()
        {
            WithHome((s, c, room, ui) =>
            {
                room.SetActive(true);
                room.Update(s, 0, s.Clock.MinuteOfDay);
                Vector3 start = room.OwnerWorld;
                Vector3 Advance(int frames)
                {
                    room.ResetOwner();
                    for (int i = 0; i < frames; i++)
                        Assert.AreEqual(0, room.WalkToBed(.8f / frames), $"Frame {i + 1}/{frames}, position {room.OwnerWorld}");
                    return room.OwnerWorld;
                }

                Vector3 fine = Advance(48);
                Vector3 coarse = Advance(5);
                Assert.Greater(Vector3.Distance(start, fine), 1f);
                Assert.That(Vector3.Distance(fine, coarse), Is.LessThan(.01f));
            });
        }

        [Test]
        public void WalkingToShopDoorCoversTheSameDistanceWithCoarseAndFineFrames()
        {
            var host = new GameObject("ShopDoorRegression");
            try
            {
                var shop = new ShopInteriorView(host.transform, PCShopSell.Core.Shop.ShopSize.Small);
                Vector3 Advance(int frames)
                {
                    shop.SetActive(false);
                    shop.SetActive(true);
                    shop.CancelCityDoorWalk();
                    shop.MoveOwner(Vector3.forward, .6f);
                    Assert.False(shop.NearDoor);
                    for (int i = 0; i < frames; i++) Assert.AreEqual(0, shop.WalkToDoor(.4f / frames));
                    return shop.OwnerWorld;
                }

                Vector3 fine = Advance(24);
                Vector3 coarse = Advance(4);
                Assert.That(Vector3.Distance(fine, coarse), Is.LessThan(.01f));
                int result = 0;
                for (int i = 0; i < 20 && result == 0; i++) result = shop.WalkToDoor(.15f);
                Assert.AreEqual(1, result);
                Assert.True(shop.NearDoor);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void NewDirectionResumesPauseButAHeldDirectionOrModalDoesNot()
        {
            WithHome((s, c, room, ui) =>
            {
                room.SetActive(true);
                room.Update(s, 0, s.Clock.MinuteOfDay);
                typeof(GameController).GetProperty("Cam").SetValue(c, c.gameObject.AddComponent<Camera>());
                var start = room.OwnerWorld;
                s.Time.SetPaused(true);
                Call(c, "ApplyMovement", .1f, -1f, 0f, false);
                Assert.True(s.Time.IsUserPaused);
                Assert.AreEqual(start, room.OwnerWorld);

                ui.OpenWindow("测试窗口", 400, 300, _ => { });
                Call(c, "ApplyMovement", .1f, -1f, 0f, true);
                Assert.True(s.Time.IsUserPaused);
                Assert.AreEqual(start, room.OwnerWorld);

                ui.CloseAllWindows();
                Call(c, "ApplyMovement", .1f, -1f, 0f, true);
                Assert.False(s.Time.IsUserPaused);
                Assert.AreNotEqual(start, room.OwnerWorld);
            });
        }

        [Test]
        public void DestinationFromInsideResumesAndWalksToDoor()
        {
            WithHome((s, c, room, ui) =>
            {
                room.SetActive(true);
                room.Update(s, 0, s.Clock.MinuteOfDay);
                s.Time.SetPaused(true);
                c.GoToPlace(s.City.GetPlace("pc"));
                Assert.False(s.Time.IsUserPaused);
                Assert.AreEqual("pc", s.CityIntent.TargetPlaceId);
                for (int i = 0; i < 200 && c.InHome; i++) Frame(s, c, room);
                Assert.False(c.InHome);
                Assert.NotNull(s.Trip);
            });
        }

        [TestCase(3.74f, 4.28f)]
        [TestCase(2.27f, 1.90f)]
        public void ShopShortcutCanLeaveHomeFromTheEdgeOfFurniture(float x, float z)
        {
            WithHome((s, c, room, ui) =>
            {
                GameSessionTests.Teleport(s, s.Home.Id);
                room.SetActive(true);
                room.Update(s, 0, s.Clock.MinuteOfDay);
                room.Root.Find("Owner").localPosition = new Vector3(x, 0, z);
                Assert.False(room.Furniture.Blockers.Any(rect => rect.Contains(new Vector2(x, z))));
                s.Time.SetSpeed(8);
                s.Time.SetPaused(true);
                ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>())
                    .Single(button => button.name == "ShortcutGo_0").onClick.Invoke();
                Assert.False(s.Time.IsUserPaused);
                for (int i = 0; i < 200 && c.InHome && s.CityIntent != null; i++) Frame(s, c, room);
                Assert.False(c.InHome, "家具旁的合法站位必须能找到通往门口的路线");
                Assert.NotNull(s.Trip);
            });
        }

        [Test]
        public void HomeDoorRouteCannotCrossFurnitureThatCompletelyBlocksTheWay()
        {
            WithHome((s, c, room, ui) =>
            {
                room.SetActive(true);
                room.Update(s, 0, s.Clock.MinuteOfDay);
                room.Root.Find("Owner").localPosition = new Vector3(2.2f, 0, 2.3f);
                room.Furniture.Blockers.Add(new Rect(2.84f, 0, .04f, room.Depth));
                Assert.AreEqual(-1, room.WalkToDoor(.15f));
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
