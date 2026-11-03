using System.IO;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void DistrictDetailsFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/district-details");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            // Rendering fixture only: the tests separately exercise actual progression and action checks.
            void Scenario(string node,int chapter,string choice,string filename)
            {
                _game.Ui.CloseAllWindows();
                typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,new DistrictProgress{Chapter=chapter,UnlockedTier=chapter-1,NodeId=node});
                Check(s.BeginDistrictMeeting(node,choice),"schedule "+node);
                _game.Ui.CloseAllWindows();_game.Ui.OpenDistrictStory();Frame(0);_game.SnapCamera();Shot(filename,1600,900);
            }
            Scenario("park.change",2,"consent","community-coordination");
            Scenario("model.clues",2,"supplier","model-investigation");
            Scenario("garden.terms",3,"respect","garden-consent");
            Log("DISTRICT DETAILS GRAPHICS COMPLETE");
        });
    }
}
