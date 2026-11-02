using System.IO;
using System.Linq;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void PerformanceReviewFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/performance");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.DebugAddCash(500000);Check(s.DebugRentStarterShop(),"测试门店已租用");
            s.Clock.Restore(new GameDate(0),600);s.Time.SetPaused(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            s.StopTrip();s.Leave();s.ArriveAtNode(s.MainShop.Place.NodeId);Frame(0);Shot("street");
            var streetColor=_game.Cam.backgroundColor;
            var trees=_game.World.Root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("ForestCanopies_")||f.name.StartsWith("ForestTrunks")).ToArray();
            Check(trees.Length>100,"树林已按空间分块");
            Check(trees.All(f=>f.sharedMesh.bounds.size.x<250&&f.sharedMesh.bounds.size.z<250),"每个树林网格都有局部包围盒");
            Log("FOREST mesh-count="+trees.Length+" triangles="+trees.Sum(f=>(long)f.sharedMesh.GetIndexCount(0)/3));
            Teleport(s,s.Home.Id);Frame(0);Shot("home");
            s.Leave();s.ArriveAtNode(s.MainShop.Place.NodeId);Frame(0);
            Check(_game.Cam.backgroundColor==streetColor,"同一分钟出门立即恢复室外灯光");
            Teleport(s,s.MainShop.Place.Id);Frame(0);Shot("shop");
            var button=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).First(b=>b.name=="ShopMainCategory");
            for(int i=0;i<120;i++)Frame(1f/60);
            Check(button!=null,"暂停两秒后柜台保留原控件");
            Log("PERFORMANCE GRAPHICS COMPLETE");
        });
    }
}
