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
        public static void NeighborhoodFromBatch()=>RunSmokeBatch(()=>
        {
            var data="Assets/Resources/Data/events.json";File.WriteAllText(data,EventScriptWriter.Write(EventScriptReader.Read(File.ReadAllText(data))));
            _outDir=Path.GetFullPath("docs/previews/neighborhood121-124");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,1234);_game.StartCityLifePreset("solo");_game.Ui.QuestPopups=false;
            foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            var s=_game.Session;((DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).UnlockedTier=3;
            s.EventsEnabled=false;s.SetSmartphone(true);s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.MainShop.Sim.SetHours(0,1440);
            void Clock(long at){s.Clock.Restore(new GameDate((int)(at/1440)),(int)(at%1440));s.Player.Health=s.Player.Hunger=s.Player.Energy=100;}
            void Here(string id){Teleport(s,id);Frame(0);_game.SnapCamera();}
            void Resolve(string id,int choice=0){s.RaiseEvent(id);if(s.PendingEvent==null)throw new InvalidOperationException(id+" unavailable");s.ResolveEvent(choice);_game.Ui.CloseAllWindows();}
            void Start(string kind){var task=s.LifeTasks.Single(t=>t.Kind==kind);if(!s.StartLifeTask(task.Id))throw new InvalidOperationException(kind+" cannot start");}
            Clock(42*1440L+660);Here(GameSession.LifeServicePlace);Resolve("life_blood_donation");Frame(0);Shot("121-blood-van-and-nurse");Start("blood-check");s.AdvanceClock(25);
            if(s.BloodDonation.Stage!="observing")throw new InvalidOperationException("Blood process skipped stages");_game.Ui.OpenNeighborhoodPanel(0);Shot("121-real-observation");_game.Ui.CloseAllWindows();s.AdvanceClock(15);
            if(s.Backpack.GiftCount(NeighborhoodGoods.Nutrition.Id)!=1)throw new InvalidOperationException("No actual nutrition pack");
            var p=s.CityPersonState("life_ning");p.Route=null;p.ActivityId=null;p.VisitId=null;p.LocationPlaceId="life_restaurant";p.Task=CityPersonTask.Leisure;
            Here(GameSession.LifeFairPlace);Resolve("life_lost_child");Frame(0);if(GameObject.Find("CommunityLostChild")==null)throw new InvalidOperationException("Child model missing");Shot("122-visible-child");
            if(!s.AskLostChild()||!s.EscortLostChild())throw new InvalidOperationException("Child clue/escort failed");Here(s.LostChild.CluePlace);
            if(!s.ReuniteLostChild())throw new InvalidOperationException("Actual parent reunion failed");Start("child-reunion");s.AdvanceClock(5);if(s.LostChild.Stage!="done")throw new InvalidOperationException("Reunion did not complete");
            bool rainy=false;for(int d=43;d<65&&!rainy;d++)for(int h=9;h<=15;h+=3)if(s.IsRainingAt(d*1440L+h*60)){Clock(d*1440L+h*60);rainy=true;break;}
            if(!rainy)throw new InvalidOperationException("No deterministic rain found");Here(s.MainShop.Place.Id);Resolve("life_roof_leak",2);s.AdvanceClock(30);Frame(0);Shot("123-real-rain-and-leak");
            if(!s.PlaceLeakBucket())throw new InvalidOperationException("Bucket unavailable");Start("leak-bucket");s.AdvanceClock(5);Frame(0);Shot("123-bucket-at-leak");
            if(!s.RequestLeakRepair())throw new InvalidOperationException("Paid repair unavailable");long arrival=s.RoofLeak.Worker.Route.ArriveMinute;s.AdvanceClock((int)(arrival-s.Clock.Now.TotalMinutes));Frame(0);Shot("123-worker-arrived");s.AdvanceClock(60);
            if(!s.RoofLeak.Fixed)throw new InvalidOperationException("Actual repair not completed");
            Clock(s.Clock.Today.AddDays(1).TotalDays*1440L+1080);Here(GameSession.LifeServicePlace);Resolve("life_festival_lantern");
            if(!s.BeginLanternCraft(1,1))throw new InvalidOperationException("Lantern unavailable");Start("lantern-craft");s.AdvanceClock(10);
            if(!s.HangFestivalLantern(NeighborhoodGoods.Lanterns[4].Id,1))throw new InvalidOperationException("Real lantern did not hang");Frame(0);Shot("124-player-hung-lantern");
            if(!s.BeginFestivalRiddle(0))throw new InvalidOperationException("Riddle unavailable");Start("lantern-riddle");s.AdvanceClock(5);_game.Ui.OpenNeighborhoodPanel(3);Shot("124-real-riddle-options");_game.Ui.CloseAllWindows();
            if(!s.AnswerFestivalRiddle(0))throw new InvalidOperationException("Riddle answer failed");
            File.WriteAllText(Path.Combine(_outDir,"runtime-results.txt"),"Production GameController/UI: visible blood van and nurse, ten-minute check, fifteen-minute donation, fifteen-minute observation and one real nutrition pack; visible child, actual clue, escort and physical parent reunion; deterministic actual rainy period, real bucket, routed worker arrival and sixty-minute repair; paid craft, real backpack lantern consumed at hanging slot and timed riddle answer. District opening, available parent location and existing shop hours are acceptance fixtures.\n");
            Log("Neighborhood 121-124 real scene and interaction acceptance passed.");
        });
    }
}
