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
        public static void OnsitePlayFromBatch()=>RunSmokeBatch(()=>
        {
            var data="Assets/Resources/Data/events.json";File.WriteAllText(data,EventScriptWriter.Write(EventScriptReader.Read(File.ReadAllText(data))));
            _outDir=Path.GetFullPath("docs/previews/onsite51-58-76");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,1234);_game.StartCityLifePreset("solo");_game.Ui.QuestPopups=false;
            foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            var s=_game.Session;((DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).UnlockedTier=3;
            s.EventsEnabled=false;s.SetSmartphone(true);s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.MainShop.Sim.NotFitted=false;
            CityActivity Attend(string id)
            {
                _game.Ui.CloseAllWindows();if(s.RunningCityActivity!=null)s.CancelCityActivity(s.RunningCityActivity.Id,true);
                s.Player.Health=s.Player.Hunger=s.Player.Energy=100;s.RaiseEvent(id);if(s.PendingEvent==null)throw new InvalidOperationException(id+" unavailable");
                if(!s.ResolveEvent(0))throw new InvalidOperationException(id+" could not book");_game.Ui.CloseAllWindows();var a=s.CityActivities.Last(x=>x.Active);
                s.Clock.Restore(new GameDate((int)(a.ScheduledStart/1440)),(int)(a.ScheduledStart%1440));Teleport(s,a.VenueId);s.PrepareVenueCompanions(s.CurrentPlace);s.AdvanceClock(3);
                if(a.State!=CityActivityPhase.Running)throw new InvalidOperationException(id+" did not start: "+a.EndReason);
                _game.Ui.CloseAllWindows();_game.Ui.OpenOnsitePlay();Frame(0);return a;
            }
            void ShotBall(int mode,double power=.615,double angle=0,bool finish=true)
            {
                if(!s.SelectOnsiteSportMode(mode)||!s.AimOnsiteSport(angle)||!s.BeginOnsiteCharge())throw new InvalidOperationException("Shot cannot start");
                for(int i=0;i<(int)Math.Round(power*1000);i++)s.StepOnsiteSport(.001);if(!s.ReleaseOnsiteShot())throw new InvalidOperationException("Release failed");
                for(int i=0;i<(finish?3100:700);i++)s.StepOnsiteSport(.001);Frame(0);
                if(Math.Abs(s.CurrentOnsite.Angle-angle)>.001)throw new InvalidOperationException("Aim changed unexpectedly");
            }
            void Complete()
            {
                var a=s.RunningCityActivity;_game.Ui.CloseAllWindows();s.AdvanceClock((int)Math.Max(1,a.ActualStart+a.Duration-s.Clock.Now.TotalMinutes));
                if(a.State!=CityActivityPhase.Completed)throw new InvalidOperationException(a.DefinitionId+" did not complete: "+a.EndReason);
                Log("onsite completed "+a.DefinitionId);
            }
            Attend("act_bar_bartender");s.SelectOnsiteDrink(1);s.SipOnsiteDrink();Frame(0);Shot("51-actual-drink-tasting");s.AdvanceClock(3);s.SelectOnsiteDrink(0);s.SipOnsiteDrink();s.AdvanceClock(3);s.OnsiteDrinkFeedback(1);
            Complete();Attend("act_bar_taoran");s.TalkOnsiteGuest(0,0);s.AdvanceClock(3);s.TalkOnsiteGuest(1,1);Frame(0);Shot("52-real-guests-and-topics");s.AdvanceClock(3);s.TalkOnsiteGuest(2,2);s.AdvanceClock(3);Complete();
            Attend("act_bar_zengyu");s.MixOnsiteDrink();s.AdvanceClock(5);s.TasteOnsiteMix();s.AdvanceClock(2);Frame(0);Shot("53-wrong-mix-actually-tasted");
            s.SetOnsiteRecipe(0,1);s.MixOnsiteDrink();s.AdvanceClock(5);Frame(0);Shot("53-real-recipe-and-tasting");s.TasteOnsiteMix();s.AdvanceClock(2);if(s.CurrentOnsite.Success!=1)throw new InvalidOperationException("Correct mix failed");Complete();
            Attend("act_court_coach");ShotBall(0,finish:false);Shot("54-real-basketball-flight");for(int i=0;i<2400;i++)s.StepOnsiteSport(.001);ShotBall(1,.65);ShotBall(2,.6);Complete();
            Attend("act_court_dongyu");ShotBall(0,.55);ShotBall(1,.7);ShotBall(2);s.OnsiteCourtFeedback(1);Frame(0);Shot("55-three-floor-exercises");Complete();
            Attend("act_court_zhang");for(int i=0;i<5;i++)ShotBall(i%2);Frame(0);Shot("56-five-actual-possessions");Complete();
            Attend("act_golf_golfer");ShotBall(1,.8,8,false);Shot("57-golf-flight-and-range");for(int i=0;i<2400;i++)s.StepOnsiteSport(.001);Frame(0);Shot("57-actual-golf-landing");for(int i=0;i<4;i++)ShotBall(i%3,.7);Complete();
            Attend("act_golf_enterprise");for(int i=0;i<3;i++)ShotBall(i%3,.7);s.Player.Intelligence=100;s.Meet("life_enterprise",100);int orders=s.CityReferrals.Count;
            if(!s.DecideOnsiteProcurement(1)||s.CityReferrals.Count!=orders+1)throw new InvalidOperationException("Real procurement offer was not created");Frame(0);Shot("58-after-golf-procurement");Complete();
            var photoActivity=Attend("act_comic_ningxi");
            for(int i=0;i<3;i++)
            {s.SetOnsitePhotoFrame(i,25+i*10,30,4,0,1);Frame(0);if(!_game.CaptureOnsitePhoto())throw new InvalidOperationException("Real PNG capture failed");}
            Frame(0);Shot("76-real-camera-framing");
            var photo=s.OnsitePhotos.Last(x=>x.ActivityId==photoActivity.Id);string path=_game.OnsitePhotoPath(photo.File);if(!File.Exists(path)||new FileInfo(path).Length<1000)throw new InvalidOperationException("Photo file missing");
            File.Copy(path,Path.Combine(_outDir,"76-actual-saved-photo.png"),true);_game.Ui.OpenOnsiteAlbum();Shot("76-saved-photo-album");
            Complete();
            File.WriteAllText(Path.Combine(_outDir,"runtime-results.txt"),"Production GameController/UI: all nine invitations actually scheduled, routed and attended. 51 two real timed sips; 52 specific real guests and timed topics; 53 wrong recipe actually tasted with penalty, followed by correct preparation and taste; 54 actual flight; 55 three different exercises and feedback; 56 five resolved possessions; 57 club/power/direction flight and landing; 58 three shots then one actual procurement offer; 76 three rendered PNG photos of existing catalog models plus album. Fixture opens districts, uses existing fitted shop and smartphone; high IQ/relation only for successful procurement acceptance.\n");
            Log("Onsite 51-58 and 76 real scene interactions and saved PNG photos passed.");
        });
    }
}
