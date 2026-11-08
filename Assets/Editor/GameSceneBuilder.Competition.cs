using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void CompetitionFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/competition"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.DebugAddCash(500000); s.DebugRentStarterShop(computerScenario: true);
            s.Clock.Restore(new GameDate(15), 600); s.PlayerShop.Reputation = 100; s.Time.SetPaused(true);
            var place = s.MainShop.Place; var center = s.DoorOf(place);
            var nearby = s.City.OfKind(PlaceKind.Shop).Where(p => p != place && !s.IsDedicatedVenue(p)).Select(p => new { Place = p, Point = s.DoorOf(p) })
                .Where(p => System.Math.Pow(p.Point.X-center.X,2)+System.Math.Pow(p.Point.Y-center.Y,2)<=s.ShopCompetitionRadiusWorld*s.ShopCompetitionRadiusWorld)
                .OrderBy(p => System.Math.Pow(p.Point.X-center.X,2)+System.Math.Pow(p.Point.Y-center.Y,2)).Take(2).ToArray();
            Check(nearby.Length == 2, "开局商圈能覆盖附近至少两间铺面");
            foreach (var item in nearby)
            {
                s.DebugAddListing(item.Place.Id, 0, 60, Money.FromYuan(1000)); Teleport(s, s.AgencyFor(item.Place).Id);
                Check(s.SignRetailLease(item.Place.Id, true, "computer"), "签约同类邻店 " + item.Place.Id);
            }
            Teleport(s, place.Id); Frame(0);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=_game.Cam; canvas.planeDistance=1; }
            Check(!_game.World.Root.activeSelf, "店内正常隐藏城市模型");
            _game.Ui.OpenShopCompetition(place); Frame(0);
            Check(!_game.World.Root.activeSelf, "地图拍摄后恢复城市隐藏状态");
            var summary = _game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Single(t=>t.name=="ShopCompetitionSummary");
            Check(summary.text.Contains("1/3"), "三店实际分流与窗口文字一致");
            Check(_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<CompetitionRangeGraphic>()).Count()==1, "商圈窗口有真实投影圆圈");
            var viewport = _game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<AgencyStreetMap>()).Single();
            Check(Mathf.Abs(((RectTransform)viewport.transform).rect.width-viewport.Width)<.1f, "地图与圆圈使用相同的投影宽度");
            Shot("shop_competition_three"); Shot("shop_competition_three_720",1280,720);
            _game.Ui.CloseAllWindows(); Teleport(s,s.AgencyFor(place).Id); _game.Ui.OnEnteredPlace(s.CurrentPlace);
            typeof(GameUi).GetField("_agencySelectedId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_game.Ui,place.Id);
            _game.Ui.RebuildWindows(); Frame(0);
            Check(_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<CompetitionRangeGraphic>()).Count()==1,"中介选中房源同步显示半径圈");
            Shot("agency_competition"); Shot("agency_competition_720",1280,720);
            Log("COMPETITION GRAPHICS COMPLETE");
        });
    }
}
