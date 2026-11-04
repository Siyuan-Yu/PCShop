using System.IO;
using System.Reflection;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ShoppingFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/simple-shopping");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            _game.Session.EventsEnabled=false;_game.Session.Time.SetPaused(true);_game.Session.SetSmartphone(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            _game.Ui.OpenPhone();Frame(0);_game.SnapCamera();Shot("phone",1600,900);
            _game.Ui.CloseAllWindows();_game.Ui.OpenPhoneStore();Frame(0);Shot("app-store",1600,900);
            _game.Ui.CloseAllWindows();_game.Ui.OpenOnlinePanel();Frame(0);Shot("shopping",1600,900);
            _game.Ui.CloseAllWindows();typeof(GameUi).GetField("_onlineTab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_game.Ui,5);
            _game.Ui.OpenOnlinePanel();Frame(0);Shot("shopping-parts",1600,900);
            Log("SHOPPING GRAPHICS COMPLETE");
        });
    }
}
