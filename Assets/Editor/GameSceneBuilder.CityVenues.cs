using System.IO;
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
        public static void CityVenuesFromBatch()=>RunSmokeBatch(SmokeAllCityVenues);
        private static void SmokeAllCityVenues()
        {
            _outDir=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/previews"));Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cam=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,813);
            foreach(var definition in CityLifeCatalog.Activities)
            {
                // Each scenario starts from an isolated fixture date/time; all subsequent travel,
                // venue entry, interaction, elapsed activity and payments use the ordinary game.
                _game.StartCityLifePreset("solo");_game.Ui.CloseAllWindows();_game.Ui.QuestPopups=false;
                foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;}
                var s=_game.Session;s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;
                int hour=definition.Id=="bar"?18:definition.Id=="ktv"?14:10;
                s.Clock.Restore(new GameDate(definition.Id=="expo_electronics"?9:21),hour*60);s.Time.SetSpeed(8);Frame(0);
                var venue=s.City.GetPlace(definition.VenueId);
                var activity=s.ScheduleCityActivity(definition.Id,new string[0],s.Clock.Now.TotalMinutes+s.CityTravelMinutes(venue)+8);
                Check(activity!=null,"正常安排："+definition.Name);if(activity==null)continue;
                Check(s.GoToCityActivity(activity.Id),"正常出发："+definition.Name);
                int frames=0;
                while(activity.Active&&activity.State!=CityActivityPhase.Running&&frames++<16000)
                {if(_game.Ui.AnyModalOpen)_game.Ui.CloseModalWindows();s.Player.Hunger=100;s.Player.Energy=100;Frame(.05f);}
                Check(activity.State==CityActivityPhase.Running,definition.Name+"真实到场开始："+activity.EndReason);
                if(activity.State!=CityActivityPhase.Running)continue;
                Check(s.CurrentPlace==venue,definition.Name+"人物在真实地点");
                for(int i=0;i<definition.Choices.Length&&i<2;i++)Check(s.ChooseCityActivity(activity.Id,i),definition.Name+"场所专属互动"+i);
                _game.Ui.CloseAllWindows();for(int i=0;i<6;i++)Frame(.05f);Shot("cityvenue_"+definition.Id,1280,720);
                while(activity.Active&&frames++<22000){if(_game.Ui.AnyModalOpen)_game.Ui.CloseModalWindows();s.Player.Hunger=100;s.Player.Energy=100;Frame(.05f);}
                Check(activity.State==CityActivityPhase.Completed&&s.CurrentPlace==venue,definition.Name+"按时间结束且留在场所");
            }
            Log("all configured city venue activities exercised through ordinary travel and timed interaction");
        }
    }
}
