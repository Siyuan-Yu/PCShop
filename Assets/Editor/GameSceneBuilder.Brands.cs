using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void BrandsFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/brand-business");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);s.DebugPrepareDistrictPrerequisites(1);s.DebugAddCash(1000000);
            typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,new DistrictProgress{UnlockedTier=3,Chapter=3});
            var shop=s.MyShops.First();shop.RetailKind="clothing";shop.Sim.Stock.ForceAddGoods(ClothingProducts.All.First().Id,100);
            typeof(GameSession).GetField("_brands",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,new BrandBusinessState{Brands=new[]{new BrandRecord{Id="demo",Name="棉风衣坊",Kind="clothing",Position="quality",Influence=105,ShopIds=new[]{shop.Place.Id}}}});
            s.Meet("life_designer",0);s.SignBrandDeal("demo","life_designer","endorse",false);s.RegisterBrandExpo("demo",shop.Place.Id,ClothingProducts.All.First().Id,"design");
            s.EnterBrandTender("demo",shop.Place.Id,ClothingProducts.All.First().Id);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            for(int tab=0;tab<5;tab++)
            {
                _game.Ui.CloseAllWindows();typeof(GameUi).GetField("_brandTab",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_game.Ui,tab);
                _game.Ui.OpenBrandBusiness();Frame(0);_game.SnapCamera();Shot("brand-"+tab,1600,900);
            }
            Log("BRAND BUSINESS GRAPHICS COMPLETE");
        });
    }
}
