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
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void TastingDialogueFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/tasting-dialogue");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,1234);_game.StartCityLifePreset("solo");_game.Ui.QuestPopups=false;
            foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            var s=_game.Session;((DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s)).UnlockedTier=3;
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.Player.Health=s.Player.Hunger=s.Player.Energy=100;
            s.RaiseEvent("act_bar_zengyu");if(s.PendingEvent==null||!s.ResolveEvent(0))throw new InvalidOperationException("Tasting invitation unavailable");_game.Ui.CloseAllWindows();
            var a=s.CityActivities.Last(x=>x.Active);s.Clock.Restore(new GameDate((int)(a.ScheduledStart/1440)),(int)(a.ScheduledStart%1440));Teleport(s,a.VenueId);s.PrepareVenueCompanions(s.CurrentPlace);s.AdvanceClock(3);
            if(a.State!=CityActivityPhase.Running)throw new InvalidOperationException("No physical meeting");_game.Ui.CloseAllWindows();_game.Ui.OpenOnsitePlay();Frame(0);Shot("53-dialogue-choice");
            if(!s.ChooseTastingDialogue(1))throw new InvalidOperationException("Dialogue option rejected");s.AdvanceClock(5);Frame(0);Shot("53-drink-offer");
            if(!s.TasteOnsiteMix())throw new InvalidOperationException("Actual tasting unavailable");s.Player.Health=80;s.AdvanceClock(2);Frame(0);Shot("53-odd-taste");
            if(s.Player.Health>=79||!s.CurrentOnsite.Message.Contains("味道有点奇怪"))throw new InvalidOperationException("Strange taste missing real effect");
            int intelligence=s.Player.Intelligence;if(!s.ChooseTastingDialogue(0))throw new InvalidOperationException("Second dialogue unavailable");s.AdvanceClock(5);s.TasteOnsiteMix();s.AdvanceClock(2);Frame(0);Shot("53-perfect-taste");
            if(s.Player.Intelligence!=intelligence+1||!s.CurrentOnsite.Message.Contains("味道简直完美"))throw new InvalidOperationException("Pleasant taste missing immediate growth");
            foreach(var text in _game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Where(t=>t.gameObject.activeInHierarchy))
                foreach(string forbidden in new[]{"已试喝","正确","错误","目标：","剩余材料","/3杯"})if(text.text.Contains(forbidden))throw new InvalidOperationException("Quiz text visible: "+forbidden);
            _game.Ui.CloseAllWindows();s.AdvanceClock((int)Math.Max(1,a.ActualStart+a.Duration-s.Clock.Now.TotalMinutes));if(a.State!=CityActivityPhase.Completed)throw new InvalidOperationException("Dialogue activity did not settle");
            if(a.EventSettlementNote.Contains("成功")||a.EventSettlementNote.Contains("实际回合"))throw new InvalidOperationException("Counter leaked into settlement");
            File.WriteAllText(Path.Combine(_outDir,"runtime-results.txt"),"Production dialogue: real invitation and meeting; choose a drink in dialogue; wait for preparation and accept the cup; strange taste applies health/energy cost; pleasant taste immediately applies mood/learning; visible UI checked for answer/count labels; full activity settles without counters.\n");
            Log("Natural tasting dialogue, physical drinking and attribute effects passed.");
        });
    }
}
