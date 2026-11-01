using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using PCShopSell.Core.Finance;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void CityClothingFromBatch()=>RunSmokeBatch(SmokeCityClothing);
        private static void SmokeCityClothing()
        {
            _outDir=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/previews"));Directory.CreateDirectory(_outDir);
            string previous=SaveStore.DirectoryOverride;
            SaveStore.DirectoryOverride=Path.Combine(_outDir,"city-clothing-test-save");Directory.CreateDirectory(SaveStore.DirectoryOverride);
            try
            {
                ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                Camera cam=BuildRig();_game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
                _game.StartCityLifePreset("clothing");var s=_game.Session;s.EventsEnabled=false;s.Clock.Restore(s.Clock.Today,10*60);Frame(0);
                void Bind(){foreach(Canvas c in _game.Ui.Canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;}}
                void Capture(string name){Bind();Frame(0);Shot(name,1280,720);}
                void EnterPoint(string id){_game.Ui.CloseAllWindows();Teleport(s,id);_game.Ui.OnEnteredPlace(s.City.GetPlace(id));Frame(0);}
                bool Click(string label,string row=null)
                {
                    var b=_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>(true)).FirstOrDefault(x=>x.gameObject.activeInHierarchy&&x.interactable&&x.GetComponentInChildren<Text>()?.text==label&&(row==null||x.transform.parent.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(row))));
                    if(b==null)return false;b.onClick.Invoke();Frame(0);return true;
                }
                Check(s.IsCityLifeTest&&ClothingProducts.All.Count==24,"独立服装测试档与24款真实商品");
                Check(s.InPlayerShop&&s.RetailKind=="clothing","截图使用当前正式店内服装经营实例");
                foreach(var fixture in s.ShopLayout)
                    Check(RoomPlanner.Check(s.ShopRoom,s.ShopLayout,FurnitureCatalog.Get(fixture.Id),fixture,s.ShopLayout.ToList().IndexOf(fixture),null)==null,"预置真实设施在房间内且不挡门/重叠："+fixture.Id);
                foreach(int tier in new[]{0,1,2,3})Check(Outfits.All.Count(o=>o.Id!="tee"&&o.Tier==tier)>=4,"四档各有至少4款原创服装 "+tier);
                string shopId=s.MainShop.Place.Id;var stock=s.MainShop.Sim.Stock;int before=stock.GoodsCount("clothing:jacket");var cash=s.Ledger.Balance;
                Check(s.UseCityShopClothing(shopId,"jacket"),"正常库存转自用入口消耗可用服装");
                Check(stock.GoodsCount("clothing:jacket")==before-1&&s.OwnedOutfits.Contains("jacket")&&s.Ledger.Balance==cash,"只消耗1件现货，不自买自卖或凭空增收");
                Check(!s.UseCityShopClothing(shopId,"jacket")&&stock.GoodsCount("clothing:jacket")==before-1,"已拥有同款不重复消耗库存");
                Check(s.Ledger.Entries.Last().BusinessKind==LedgerBusinessKind.InternalTransfer,"领用记录归内部划转");
                _game.Ui.OpenCityWardrobe();Capture("cityclothing_01_wardrobe");
                var equipped=s.EquippedOutfit;int owned=s.OwnedOutfits.Count;cash=s.Ledger.Balance;
                Check(Click("试穿","暖棉厚装"),"从真实衣柜行点击厚冬装试穿");Capture("cityclothing_02_winter_preview");
                Check(s.EquippedOutfit==equipped&&s.OwnedOutfits.Count==owned&&s.Ledger.Balance==cash,"试穿不改变装备所有权与现金");
                Check(Click("换上已拥有这套"),"实际镜子旁换上已拥有冬装");
                Check(s.EquippedOutfit=="winter_basic"&&s.OutfitComfortMin<=0,"实际穿搭与舒适温区同步");
                _game.Ui.CloseAllWindows();Frame(0);
                _game.Interior.StandAtCounter();_game.DebugShopOverview(true);Frame(0);
                Check(_game.Interior.Furniture.Entries.Count(e=>e.Def.Id==FurnitureCatalog.ClothingRack)==2,"店内视图真实生成两组服装挂架");
                Check(_game.Interior.Root.GetComponentsInChildren<Transform>().Any(t=>t.gameObject.activeInHierarchy&&t.name=="HangingGarment"),"有实际库存的衣架显示悬挂服装");
                Check(_game.Interior.Root.GetComponentsInChildren<Transform>().Any(t=>t.gameObject.activeInHierarchy&&t.name=="FoldedTShirt"),"折叠架显示可辨认的折叠衣服");
                Capture("cityclothing_03_shop_racks_worn");_game.DebugShopOverview(false);
                EnterPoint("shop02");_game.Ui.OpenCityClothingShop(s.City.GetPlace("shop02"));Capture("cityclothing_04_regular_shop");
                cash=s.Ledger.Balance;Check(s.BuyCityClothing(s.City.GetPlace("shop02"),"knit_basic"),"普通服装店柜台真实购买");
                Check(s.OwnedOutfits.Contains("knit_basic")&&s.Ledger.Balance==cash-Outfits.Get("knit_basic").Price,"付款一次取得一套个人服装");
                EnterPoint("clothing_boutique");_game.Ui.OpenCityClothingShop(s.City.GetPlace("clothing_boutique"));Capture("cityclothing_05_boutique");
                EnterPoint(shopId);_game.Ui.CloseAllWindows();s.MainShop.Sim.Settings.BaseCustomersPerHour=0;
                double cold=s.CityAirTemperature;s.Time.SetSpeed(8);for(int i=0;i<20;i++)Frame(.05f);
                Check(s.CityAirTemperature>cold,"实际营业有人在场时，自动空调沿世界时钟逐渐制热");
                Check(s.CityClimateZones.Single(z=>z.PlaceId==shopId).Devices.Any(d=>d.Running&&!CityWeather.IsFan(d.Kind)),"冷天实际安装的空调运行，不是仅拥有标记");
                _game.Ui.OpenCityClimate();Capture("cityclothing_06_climate");
                _game.Ui.CloseAllWindows();Check(SaveStore.TrySave(s),"测试进度保存到独立目录与独立槽");
                using(var restored=SaveStore.LoadCityLifeTest(s.City))
                {
                    Check(restored.IsCityLifeTest&&restored.EquippedOutfit==s.EquippedOutfit&&restored.OwnedOutfits.Contains("knit_basic"),"测试档恢复真实穿搭和已拥有服装");
                    Check(restored.MainShop.Sim.Stock.GoodsCount("clothing:jacket")==before-1,"读档不归还已领用库存");
                    Check(restored.Ledger.Balance==s.Ledger.Balance,"读档不重复柜台消费");
                }
                Log("city clothing normal-API UI smoke completed; six 1280x720 screenshots in isolated test progress");
            }
            finally {SaveStore.DirectoryOverride=previous;}
        }
    }
}
