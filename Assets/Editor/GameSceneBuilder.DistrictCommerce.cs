using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void DistrictCommerceFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/district-commerce");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);s.DebugPrepareDistrictPrerequisites(1);s.DebugAddCash(1000000);
            void Set(string field,object value)=>typeof(GameSession).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,value);
            void Capture(string name){_game.Ui.CloseAllWindows();_game.Ui.OpenDistrictStory();Frame(0);_game.SnapCamera();Shot(name,1600,900);}
            var shop=s.MyShops.First();shop.RetailKind="clothing";string product=ClothingProducts.All.First().Id;shop.Sim.Stock.ForceAddGoods(product,100);
            Set("_brands",new BrandBusinessState{Brands=new[]{new BrandRecord{Id="demo",Name="棉风衣坊",Kind="clothing",Position="quality",Influence=100,ShopIds=new[]{shop.Place.Id}}}});
            Set("_district",new DistrictProgress{UnlockedTier=1,Chapter=2,BrandCommerceRoute=true,CommerceVersion=1,CommerceBrandId="demo",NodeId="expo.prepare",Completed=new[]{"expo.invitation"}});
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            if(!s.RegisterBrandExpo("demo",shop.Place.Id,product,"design"))throw new System.Exception("Expo registration failed");
            Capture("expo-progress");
            Set("_district",new DistrictProgress{UnlockedTier=2,Chapter=3,BrandCommerceRoute=true,CommerceVersion=1,CommerceBrandId="demo",NodeId="tender.prepare",Completed=new[]{"tender.invitation"}});
            if(!s.EnterBrandTender("demo",shop.Place.Id,product))throw new System.Exception("Tender registration failed");
            string tender=s.BrandBusiness.Tenders.Single().Id;s.ResearchBrandTender(tender,1);Capture("tender-investigation");
            s.ResearchBrandTender(tender,2);s.SubmitBrandTender(tender,"service",true);s.RefreshDistrictNarrative();
            var order=s.BrandBusiness.Orders.Single(o=>o.Source=="trial");if(!s.FulfilBrandOrder(order.Id))throw new System.Exception("Trial shipment failed");
            long arrival=s.BrandBusiness.Orders.Single(o=>o.Id==order.Id).Arrival;s.Clock.Restore(new GameDate((int)(arrival/1440)),(int)(arrival%1440));
            typeof(GameSession).GetMethod("TickBrandDeliveries",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{arrival});
            if(!s.EvaluateBrandTender(tender))throw new System.Exception("Tender evaluation failed");Capture("tender-contract");
            Log("DISTRICT COMMERCE GRAPHICS COMPLETE");
        });
    }
}
