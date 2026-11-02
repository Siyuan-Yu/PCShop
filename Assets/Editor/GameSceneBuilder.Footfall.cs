using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void FootfallFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/footfall");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Clock.Restore(new GameDate(0),600);s.Time.SetPaused(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            var place=s.PlayerShopPlace;Teleport(s,s.AgencyFor(place).Id);
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            typeof(GameUi).GetField("_agencySelectedId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_game.Ui,place.Id);
            _game.Ui.RebuildWindows();Frame(0);
            var text=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Single(t=>t.name=="AgencyDailyPassers");
            Check(text.text.Contains(s.FootfallOf(place).DailyPassers.ToString("N0")),"中介展示真实估算的每日经过人数");
            Check(!_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("地段客流 ×")||t.text.Contains("条越长")),"中介不再展示倍率和模糊客流条");
            Check(s.Listings.All(l=>l.Rent==s.ShopRentQuote(s.ListingPlace(l))),"挂牌月租与预计人数统一报价");
            Shot("agency_daily_passers");Shot("agency_daily_passers_720",1280,720);
            Log("FOOTFALL GRAPHICS COMPLETE");
        });
    }
}
