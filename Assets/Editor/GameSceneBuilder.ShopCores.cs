using System.IO;
using System.Linq;
using PCShopSell.Core.Home;
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
        public static void ShopCoresFromBatch()=>RunSmokeBatch(SmokeShopCores);
        private static void SmokeShopCores()
        {
            _outDir=Path.GetFullPath("docs/previews/shop-cores");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.DebugAddCash(500000);Check(s.DebugRentStarterShop(),"测试夹具实际租下起始店");s.Clock.Restore(new GameDate(15),600);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            Teleport(s,s.PlayerShopPlace.Id);_game.Ui.CloseAllWindows();
            Check(s.SetShopMainCategory("restaurant"),"可开餐馆");
            var shop=s.CurrentShop;shop.Layout.Clear();
            string[] kitchen={"restaurant_prep","restaurant_stove","restaurant_pass",FurnitureCatalog.Fridge};
            for(int i=0;i<kitchen.Length;i++)shop.Layout.Add(new PlacedFurniture{Id=kitchen[i],X=1+i*1.25,Z=4.8});
            shop.Layout.Add(new PlacedFurniture{Id="restaurant_table",X=2,Z=2});shop.Layout.Add(new PlacedFurniture{Id="restaurant_table",X=4,Z=2});
            shop.Fridge.Food.RiceKg=1;shop.Fridge.Food.AddIngredient("veg",3,15);shop.Sim.ForcedClosed=false;
            var state=s.SpecialBusiness(shop.Place.Id);state.NextVisit=long.MaxValue;
            state.Orders.Add(new DiningOrder{Id=state.NextId++,Recipe=0,Table=0,Stage=DiningStage.Waiting,Arrived=s.Clock.Now.TotalMinutes,Price=18});
            _game.Ui.OnEnteredPlace(s.CurrentPlace);_game.Ui.OpenDock();Frame(0);
            Check(_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("厨房大米")),"餐馆使用独立厨房操作界面");
            Shot("restaurant_waiting");Shot("restaurant_waiting_720",1280,720);
            Check(s.StartRestaurantCooking(1),"实际食材投入炒锅");s.AdvanceClock(8);_game.Ui.RebuildDock();Frame(0);Shot("restaurant_ready_to_plate");
            Check(s.PlateRestaurantOrder(1)&&s.ServeRestaurantOrder(1),"实际出锅并上菜");s.AdvanceClock(15);
            Check(state.Paid==1&&state.Revenue==18,"吃完实际收款且无重复收费");s.ClearRestaurantOrder(1);
            Check(s.SetShopMainCategory("secondhand"),"清理餐馆订单后可转型二手店");
            shop.Layout.Clear();shop.Layout.Add(new PlacedFurniture{Id="used_buydesk",X=2,Z=4.2});shop.Layout.Add(new PlacedFurniture{Id="used_display",X=4,Z=3});
            var seller=new UsedItem{Id=state.NextId++,Product=1,Personality=1,Statement=2,Claim=UsedCondition.Good,Condition=UsedCondition.Replica,Asking=130,Minimum=70,ExpiresAt=s.Clock.Now.TotalMinutes+90};state.Used.Add(seller);
            _game.Ui.RebuildDock();Frame(0);Shot("used_seller");Shot("used_seller_720",1280,720);
            Check(!s.InspectUsed(seller.Id),"付款前不能鉴定");Check(s.OfferUsedPrice(seller.Id,70)&&s.InspectUsed(seller.Id),"收购后鉴定揭晓仿造品");
            Check(s.ListUsedItem(seller.Id,GameSession.UsedValue(seller)),"按真实状态定价上架");_game.Ui.RebuildDock();Frame(0);Shot("used_inspected");
            Check(state.Used.Single(i=>i.Id==seller.Id).Condition==UsedCondition.Replica,"鉴定结果不重新抽取");
            Log("SHOP CORES GRAPHICS COMPLETE");
        }
    }
}
