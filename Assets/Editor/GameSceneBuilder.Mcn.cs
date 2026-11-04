using System.IO;
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
        public static void McnFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/community-mcn");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);s.Clock.Restore(new GameDate(40),600);s.DebugPrepareDistrictPrerequisites(1);
            typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,new DistrictProgress{UnlockedTier=1,Chapter=1});
            typeof(GameSession).GetProperty("CurrentPlace",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,s.City.GetPlace(GameSession.CommunityMcnId));
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            _game.Ui.OpenCommunityMcn();Frame(0);_game.SnapCamera();Shot("community-mcn",1600,900);
            if(!s.RequestMcnIntroduction("momo"))throw new System.Exception("MCN introduction failed");var appointment=s.McnIntroductions[0];
            _game.Ui.CloseAllWindows();_game.Ui.OpenCommunityMcn();Frame(0);Shot("community-mcn-waiting",1600,900);
            s.Clock.Restore(new GameDate((int)(appointment.Arrival/1440)),(int)(appointment.Arrival%1440));
            typeof(GameSession).GetMethod("TickCityPeople",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{s.Clock.Now.TotalMinutes});
            if(!s.ExchangeMcnContact("momo"))throw new System.Exception("MCN contact exchange failed");
            _game.Ui.CloseAllWindows();_game.Ui.OpenCommunityMcn();Frame(0);Shot("community-mcn-contact",1600,900);
            typeof(PCShopSell.Game.UI.GameUi).GetMethod("OpenBrandContact",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_game.Ui,new object[]{"momo"});
            Frame(0);Shot("community-mcn-dialogue",1600,900);
            _game.Ui.CloseAllWindows();_game.Ui.DebugOpenBusiness(4);Frame(0);Shot("community-mcn-locked",1600,900);
            if(s.FoundMcn())throw new System.Exception("Community should not allow founding MCN");
            typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,new DistrictProgress{UnlockedTier=2,Chapter=2});
            s.PlayerShop.Reputation=40;s.DebugAddCash(100000);
            _game.Ui.CloseAllWindows();_game.Ui.DebugOpenBusiness(4);Frame(0);Shot("park-mcn-found",1600,900);
            long before=s.Ledger.Balance.Cents;
            if(!s.FoundMcn()||!s.HasMcn||s.Ledger.Balance.Cents!=before-5000000)throw new System.Exception("Park MCN founding / payment failed");
            _game.Ui.CloseAllWindows();_game.Ui.DebugOpenBusiness(4);Frame(0);Shot("park-mcn-operating",1600,900);
            Log("MCN FLOW: introduction -> real route arrival -> contact -> dialogue; community founding rejected; park founding charged 50000 yuan");
            Log("MCN GRAPHICS COMPLETE");
        });
    }
}
