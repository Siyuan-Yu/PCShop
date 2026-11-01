using System.IO;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void CityActivitiesFromBatch()=>RunSmokeBatch(SmokeCityActivities);
        private static void SmokeCityActivities()
        {
            _outDir=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/previews"));
            Directory.CreateDirectory(_outDir);ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cam=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,1234);
            _game.StartCityLifePreset("solo");_game.Ui.QuestPopups=false;
            foreach(var c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;}
            var s=_game.Session;s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;s.Time.SetSpeed(8);
            Frame(0);_game.Ui.CloseAllWindows();
            long start=s.SuggestCityActivityStart("dinner","life_creator");
            var a=s.ScheduleCityActivity("dinner",new[]{"life_creator"},start);
            Check(a!=null,"饭局通过正常预约服务创建");
            Check(s.GoToCityActivity(a.Id),"从实际店内启动赴约路线");
            for(int i=0;i<35;i++)Frame(.05f);
            Shot("city_activity_01_departure",1280,720);
            Check(!s.CityPlayerCanWork(s.MainShop),"老板真实离店后不能继续现场服务");
            int frames=0;
            while(a.Active&&a.State!=CityActivityPhase.Running&&frames++<16000)
            {
                if(_game.Ui.AnyModalOpen)_game.Ui.CloseModalWindows();
                s.Player.Hunger=100;s.Player.Energy=100;Frame(.05f);
            }
            Check(a.State==CityActivityPhase.Running,"双方沿实际路线到达餐馆后开始活动："+a.EndReason);
            Check(s.CurrentPlace?.Id=="life_restaurant","玩家实际在餐馆");
            Check(s.CityPersonState("life_creator").LocationPlaceId=="life_restaurant","同行人物实际到场");
            _game.Ui.CloseAllWindows();for(int f=0;f<8;f++)Frame(.05f);Shot("city_activity_02_at_restaurant",1280,720);
            Check(s.ChooseCityActivity(a.Id,0),"实际在场选择餐馆互动");
            _game.Ui.OpenCityActivities();Shot("city_activity_03_running",1280,720);_game.Ui.CloseAllWindows();
            while(a.Active&&frames++<23000){s.Player.Hunger=100;s.Player.Energy=100;Frame(.05f);}
            Check(a.State==CityActivityPhase.Completed,"饭局随世界时钟真实结束");
            Check(s.CurrentPlace?.Id=="life_restaurant","活动结束不把老板传回柜台");
            _game.Ui.OpenCityActivities();Shot("city_activity_04_completed",1280,720);
            Log("city activity actual departure, travel, gathering and timed meal smoke completed");
        }
    }
}
