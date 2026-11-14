using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void LifeBatchFromBatch()=>RunSmokeBatch(()=>
        {
            var dataPath="Assets/Resources/Data/events.json";File.WriteAllText(dataPath,EventScriptWriter.Write(EventScriptReader.Read(File.ReadAllText(dataPath))));
            _outDir=Path.GetFullPath("docs/previews/life111-120");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,1234);_game.StartCityLifePreset("solo");_game.Ui.QuestPopups=false;
            foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            var s=_game.Session;((DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).UnlockedTier=3;
            s.EventsEnabled=false;s.SetSmartphone(true);s.MainShop.Sim.Settings.BaseCustomersPerHour=0;
            void Clock(long at){s.Clock.Restore(new GameDate((int)(at/1440)),(int)(at%1440));s.Player.Health=s.Player.Hunger=s.Player.Energy=100;}
            void Here(string id){Teleport(s,id);Frame(0);_game.SnapCamera();}
            void Resolve(string id,int choice){s.RaiseEvent(id);if(s.PendingEvent==null)throw new InvalidOperationException(id+" unavailable");s.ResolveEvent(choice);_game.Ui.CloseAllWindows();}
            Clock(42*1440L+660);Here(GameSession.LifeServicePlace);Resolve("life_book_swap",0);
            // Put the librarian at her authored workplace for this isolated acceptance fixture.
            var librarian=s.CityPersonState("life_librarian");librarian.Route=null;librarian.ActivityId=null;librarian.LocationPlaceId=GameSession.LifeServicePlace;
            if(!s.BorrowLifeBook(0))throw new InvalidOperationException("Real book acquisition failed");
            Frame(0);Shot("118-visible-library");_game.Ui.OpenLifeEventPanel(2);Shot("118-book-exchange");_game.Ui.CloseAllWindows();
            Resolve("life_pet_adopt",0);Frame(0);if(GameObject.Find("LifeCompanionDog")==null)throw new InvalidOperationException("Dog model missing");Shot("114-visible-stray-dog");
            if(!s.AdoptLifeDog())throw new InvalidOperationException("Actual adoption payment failed");var adopt=s.LifeTasks.Single(t=>t.Kind=="adopt");
            if(!s.StartLifeTask(adopt.Id))throw new InvalidOperationException("Adoption registration failed");s.AdvanceClock(30);if(s.LifeDog.Stage!="adopted")throw new InvalidOperationException("Adoption did not finish");
            Here(s.Home.Id);if(!s.FeedLifeDog())throw new InvalidOperationException("Real feeding failed");Frame(0);Shot("114-dog-at-home");_game.Ui.OpenLifeEventPanel(1);Shot("114-dog-care");_game.Ui.CloseAllWindows();
            if(!s.ReadLifeBook(LifeEventGoods.Books[0].Id))throw new InvalidOperationException("Reading unavailable");var read=s.LifeTasks.Single(t=>t.Kind=="read");if(!s.StartLifeTask(read.Id))throw new InvalidOperationException("Reading start failed");s.AdvanceClock(30);
            Here(GameSession.LifeServicePlace);Resolve("life_night_class",0);if(!s.EnrollLifeCourse(0,false))throw new InvalidOperationException("Actual course payment failed");
            var lesson=s.CityActivities.Single(a=>a.Id==s.LifeCourse.Activity);Clock(lesson.ScheduledStart);Here(lesson.VenueId);s.PrepareVenueCompanions(s.CurrentPlace);s.AdvanceClock(3);
            if(lesson.State!=CityActivityPhase.Running)throw new InvalidOperationException("Teacher did not attend: "+lesson.EndReason);s.AdvanceClock(30);
            _game.Ui.CloseAllWindows();_game.Ui.OpenLifeEventPanel(3);Shot("119-actual-night-class");_game.Ui.CloseAllWindows();
            if(!s.AnswerLifeLesson(1))throw new InvalidOperationException("Actual classroom answer failed");s.AdvanceClock(31);if(s.LifeCourse.Lessons!=1)throw new InvalidOperationException("Lesson did not complete");
            Clock(47*1440L+660);Here(GameSession.LifeFairPlace);s.Backpack.AddFurniture("fan_new",3);Resolve("life_street_fair",0);
            if(!s.StartLifeFair("fan_new",true,3,90))throw new InvalidOperationException("Actual stall rental/consignment failed");Frame(0);if(GameObject.Find("PlayerCommunityStall")==null)throw new InvalidOperationException("Visible stall missing");Shot("120-visible-player-stall");
            _game.Ui.OpenLifeEventPanel(4);Shot("120-actual-consigned-stock");_game.Ui.CloseAllWindows();
            for(int n=0;n<12&&s.LifeStall.Sold==0;n++)s.AdvanceClock(15);
            if(s.LifeStall.Sold==0)throw new InvalidOperationException("No physical buyer completed a sale within this seeded stall fixture");
            _game.Ui.OpenLifeEventPanel(4);Shot("120-physical-customer-sale");_game.Ui.CloseAllWindows();s.EndLifeFair();
            File.WriteAllText(Path.Combine(_outDir,"runtime-results.txt"),"Production scene: actual book acquisition/read; visible dog, paid registration and consumed food; actual teacher/classroom answer and completion; visible player stall, reserved inventory and a physically arrived buyer sale. District access, librarian workplace and owned consignment stock are acceptance fixtures.\n");
            Log("Life 111-120 library, dog, course and actual buyer stall passed.");
        });
    }
}
