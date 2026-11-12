using System.IO;
using System.Linq;
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
        public static void CityOverviewFromBatch() => RunSmokeBatch(SmokeCityOverview);
        private static void SmokeCityOverview()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir); ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); _game.StartCityLifePreset("staff"); _game.Ui.QuestPopups = false;
            foreach (Canvas c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false;
            Frame(0);
            var balance = s.Ledger.Balance; var place = s.CurrentPlace; var position = s.PlayerPosition;
            int entries = s.Ledger.Entries.Count;
            _game.Ui.OpenCityFinanceOverview(); Shot("citylife_01_overview", 1280, 720);
            Check(s.CurrentPlace == place && s.PlayerPosition.X == position.X && s.PlayerPosition.Y == position.Y, "经营总览不改变玩家实际位置");
            Check(s.Ledger.Balance == balance && s.Ledger.Entries.Count == entries, "经营总览不产生现金或新账");
            _game.Ui.CloseAllWindows(); _game.Ui.OpenCityProcurement(); Shot("citylife_02_procurement", 1280, 720);
            Check(s.Ledger.Balance == balance && s.Ledger.Entries.Count == entries, "查看统一采购不自动下单");
            _game.Ui.CloseAllWindows(); _game.Ui.OpenCityMap(); Shot("citylife_03_map", 1280, 720);
            Check(s.Trip == null && s.CurrentPlace == place, "查看十街地图不自动开始旅行");
            var pins=UnityEngine.Object.FindObjectsOfType<RectTransform>();
            var dinnerPin=pins.FirstOrDefault(p=>p.name=="CityMap_life_restaurant");
            var clothingPin=pins.FirstOrDefault(p=>p.name=="CityMap_clothing_boutique");
            Check(dinnerPin!=null&&clothingPin!=null&&Vector2.Distance(dinnerPin.anchorMin,clothingPin.anchorMin)>.05f,
                "不同街区显式入口按节点加门口偏移投影，不堆叠在原点");
            _game.Ui.CloseAllWindows(); s.Meet("life_ming", 15);
            _game.Ui.OpenCityPeople(); Shot("citylife_04_people", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenCityPerson("life_ming"); Shot("citylife_05_person", 1280, 720);
            Check(s.CurrentPlace == place && s.CityPersonState("life_ming") != null, "查看人物保留稳定身份且不把玩家移到对方身边");
            _game.Ui.CloseAllWindows();
            var cash = s.Ledger.Balance; int relation = s.Relation("life_creator");
            long start = s.SuggestCityActivityStart("dinner", "life_creator");
            var activity = s.ScheduleCityActivity("dinner", new[] { "life_creator" }, start);
            Check(activity != null, "通过正常预约服务创建真实饭局日程");
            Check(s.Ledger.Balance == cash && s.Relation("life_creator") == relation, "接受未来饭局不提前扣费或加关系");
            _game.Ui.OpenSchedulePanel(); Shot("citylife_06_schedule", 1280, 720);
            _game.Ui.CloseAllWindows(); _game.Ui.OpenCityModelCatalog(); Shot("citylife_07_models", 1280, 720);
            Check(s.Ledger.Balance == cash, "查看原创模型目录不产生商品和收益");
            _game.Ui.CloseAllWindows();
            Log("city overview UI smoke completed; 1280x720 captures generated");
        }
    }
}
