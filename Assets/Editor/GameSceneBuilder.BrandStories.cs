using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void BrandStoriesFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/npc-brand-stories"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true); s.DebugPrepareDistrictPrerequisites(1); s.DebugAddCash(1000000);
            typeof(GameSession).GetField("_district", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, new DistrictProgress { UnlockedTier = 3, Chapter = 3 });
            var shop = s.MyShops.First(); shop.RetailKind = "clothing"; string product = ClothingProducts.All.First().Id; shop.Sim.Stock.ForceAddGoods(product, 100);
            typeof(GameSession).GetField("_brands", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, new BrandBusinessState {
                Brands = new[] { new BrandRecord { Id = "demo", Name = "棉风衣坊", Kind = "clothing", Position = "quality", Influence = 105, ShopIds = new[] { shop.Place.Id } } }
            });
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, shop.Place);
            foreach (string person in new[] { "momo", "qiao", "life_curator", "life_designer" })
            { var p = s.CityPersonState(person); p.LocationPlaceId = shop.Place.Id; p.Route = null; }
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            _game.Ui.OpenCityPersonProfile("keeper_agent"); Frame(0); _game.SnapCamera(); Shot("formal-name-profile-720", 1280, 720);
            _game.Ui.CloseAllWindows();
            s.Meet("momo", 0);
            s.SignKolAmbassador("momo", 90, 1);
            s.AssignKolBrand(s.ActiveKolAmbassador("momo").Id, "demo");
            string promo = s.BrandStoryOffers().Single().Id; string opening = s.BeginBrandStory(promo);
            _game.Ui.OpenPersonDialogue("momo", firstReply: opening); Frame(0); Shot("creator-story-dialogue-720", 1280, 720); Shot("creator-story-dialogue-1080", 1920, 1080);
            _game.Ui.CloseAllWindows(); typeof(GameUi).GetField("_brandTab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_game.Ui, 0);
            _game.Ui.OpenBrandBusiness(); Frame(0); Shot("story-sample-preparation-720", 1280, 720); Shot("story-sample-preparation-1080", 1920, 1080);
            s.PrepareBrandStory(promo, shop.Place.Id, product, "value"); s.Clock.Restore(new GameDate(s.Clock.Today.TotalDays + 3), 600);
            string conclusion = s.FinishBrandStory(promo); _game.Ui.CloseAllWindows();
            _game.Ui.OpenPersonDialogue("momo", firstReply: conclusion); Frame(0); Shot("honest-zero-sales-followup-720", 1280, 720);
            _game.Ui.CloseAllWindows(); s.RegisterBrandExpo("demo", shop.Place.Id, product, "design");
            _game.Ui.OpenBrandBusiness(); Frame(0); Shot("exhibition-story-invitations-720", 1280, 720);
            NarrativeContentExport.Export();
            Log("NPC BRAND STORIES GRAPHICS COMPLETE");
        });
    }
}
