using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void CityAssetsFromBatch() => RunSmokeBatch(SmokeCityAssets);

        private static void SmokeCityAssets()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir); ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 778);
            _game.Ui.QuestPopups = false;
            foreach (Canvas canvas in _game.Ui.Canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false;
            // Fixture setup only: funds and phone unlock. Purchases, travel and investments below use normal APIs.
            s.DebugAddCash(1500000); s.SetSmartphone(true);
            Teleport(s, "agency");
            Check(s.SignLease("player_shop"), "城市资产：通过正常中介签约开局铺面");
            _game.Ui.CloseAllWindows();
            var shop = s.MainShop; var sim = shop.Sim;
            var part = PartCatalog.Get("cpu0"); shop.Sim.Stock.ForceAdd(part, true, 2);
            Frame(0);
            Check(_game.City.Streets.Count >= 10 && _game.World.Places.Count == _game.City.Places.Count,
                "十街配置中的全部地点都建立实际场景视图");
            Check(s.CityCommercialProperties.Count() >= 20, "城市购买目录至少20处具体商业单元");
            foreach (var place in _game.World.Places.Values)
                Check(_game.World.CityBounds.Contains(place.Entrance), "世界范围包含入口：" + place.Place.Id);

            Money cash = s.Ledger.Balance; var physical = s.CurrentPlace; var time = s.Clock.Now;
            _game.Ui.OpenCityProperties(); ClickGuideButton("购买目录");
            Check(s.Ledger.Balance == cash && s.CurrentPlace == physical && s.Clock.Now == time,
                "打开全城物业目录不扣钱、不移动、不推进时间");
            Shot("city_property_catalog"); _game.Ui.CloseAllWindows();

            var quote = s.PreviewCityPropertyPurchase(shop.Place.Id, false);
            Check(s.BuyProperty(shop.Place.Id, false), "正常租转买交易成功");
            Check(ReferenceEquals(shop, s.ShopAt(shop.Place.Id)) && ReferenceEquals(sim, shop.Sim)
                && shop.Sim.Stock.CountAll(part) == 2 && s.RentOf(shop) == Money.Zero && shop.OwnsProperty,
                "租转买保留Shop实例、库存与经营，停止旧租金");
            Check(s.Ledger.Balance == cash - quote.TotalDue, "实际付款与交易费、押金和预付抵扣预览一致");
            cash = s.Ledger.Balance;
            Check(!s.BuyProperty(shop.Place.Id, false) && s.Ledger.Balance == cash, "重复购买不产生第二产权或第二付款");
            _game.Ui.OpenCityProperties(); ClickGuideButton("我的物业"); Shot("city_property_owned"); _game.Ui.CloseAllWindows();

            Check(s.TradeCityInvestment("city_chip", 10, true, true), "正常买入虚构企业10份");
            Check(s.TradeCityInvestment("city_chip", 4, false), "正常部分卖出4份");
            Check(s.CityInvestmentUnits("city_chip") == 6 && s.CityInvestmentCost("city_chip").IsPositive,
                "部分卖出保留6份及剩余成本");
            cash = s.Ledger.Balance;
            _game.Ui.OpenCityInvestments(); Shot("city_investments"); Shot("city_investments_1280", 1280, 720);
            Check(s.Ledger.Balance == cash && s.CityInvestmentUnits("city_chip") == 6, "投资面板重建不买卖或改变持仓");
            _game.Ui.CloseAllWindows();
            var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
            using (var restored = GameSession.Load(s.City, save))
            {
                Check(restored.ShopAt(shop.Place.Id)?.OwnsProperty == true && restored.CurrentShopRent == Money.Zero,
                    "产权与免租使用权读档后恢复");
                Check(restored.CityInvestmentUnits("city_chip") == 6 && restored.CityInvestmentCost("city_chip") == s.CityInvestmentCost("city_chip")
                    && restored.Ledger.Balance == s.Ledger.Balance, "读档不复制持仓、现金或产权交易");
            }

            _game.Ui.OnEnteredPlace(s.City.FirstOfKind(PCShopSell.Core.World.PlaceKind.Agency));
            Frame(0); Shot("city_ten_streets_overview"); _game.Ui.CloseAllWindows();

            // Set a safe daytime starting point, then exercise actual configured street routes without teleporting to venues.
            s.Clock.Restore(new GameDate(9), 10 * 60); s.Time.SetSpeed(8);
            CitySmokeTravelToVenue("life_restaurant", "city_restaurant_scene", "ServingDish");
            CitySmokeTravelToVenue("life_court", "city_court_scene", "Basketball");
            Check(!s.IsGameOver, "真实路线参观后会话仍可继续");
            Log("city assets and venue smoke complete");
        }

        private static void CitySmokeTravelToVenue(string id, string shot, string requiredProp)
        {
            var s = _game.Session; var venue = s.City.GetPlace(id);
            _game.Ui.CloseAllWindows(); Feed(s); s.Player.Energy = 100;
            _game.GoToPlace(venue);
            bool started = s.Trip != null || s.CityIntent != null;
            Check(started || s.CurrentPlace == venue, "实际出行已创建目的地路线：" + venue.Name);
            int frames = 0;
            while (s.CurrentPlace != venue && frames++ < 8000 && !s.IsGameOver)
            {
                if (_game.Ui.AnyModalOpen) _game.Ui.CloseModalWindows();
                Frame(.05f); if (frames % 240 == 0) Feed(s);
                if (s.Trip == null && s.CityIntent == null && s.CurrentPlace != venue) break;
            }
            Check(s.CurrentPlace == venue && s.Trip == null, "沿真实路网到场并进入：" + venue.Name);
            _game.Ui.CloseAllWindows();
            Check(_game.World.Places[id].Root.GetComponentsInChildren<Transform>(true).Any(t => t.name == requiredProp),
                "场所有专属可见设施：" + requiredProp);
            for (int i = 0; i < 15; i++) Frame(.015f);
            Shot(shot); _game.Ui.OpenCityVenue(venue); Shot(shot + "_activities"); _game.Ui.CloseAllWindows();
        }
    }
}
