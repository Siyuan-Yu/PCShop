using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
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
        public static void RetroPortraitsFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/retro-npc-portraits"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 7);
            _game.Ui.QuestPopups = false; _game.Session.EventsEnabled = false; _game.Session.Time.SetPaused(true);
            foreach (var canvas in _game.Ui.Canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            var people = CityLifeCatalog.People.Where(p => p.Id != CityLifeCatalog.InvitationTestPersonId).ToArray();
            Check(people.Length > 0, $"正式 NPC 名单 {people.Length} 人");
            int pages = (people.Length + 19) / 20;
            long portraitBytes = 0;
            foreach (var person in people)
            {
                var texture = Resources.Load<Texture2D>("PortraitsRetro/" + person.Id);
                Check(texture != null, "独立头像 " + person.Id);
                if (texture != null) portraitBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            }
            Log("RETRO PORTRAIT_TEXTURE_BYTES=" + portraitBytes);
            Check(GameUi.PortraitOf("employee_portrait_review_unknown") == Resources.Load<Texture2D>("PortraitsRetro/default"), "动态人物使用复古备用头像");

            for (int page = 0; page < pages; page++)
            {
                var go = new GameObject("RetroPortraitCatalogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _game.Cam; canvas.planeDistance = .5f; canvas.sortingOrder = 10000;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 1760); scaler.matchWidthOrHeight = 0;
                var paper = Ui.Rect(go.transform, "Paper").Fill();
                paper.gameObject.AddComponent<Image>().color = new Color(.94f, .90f, .82f);
                var title = Ui.Label(paper, $"复古生活漫画 · NPC头像核对 {page + 1}/{pages}", 26, TextAnchor.MiddleLeft, new Color(.22f, .16f, .11f));
                Position(title.rectTransform, 32, 8, 1536, 52);
                for (int cell = 0; cell < 20 && page * 20 + cell < people.Length; cell++)
                {
                    var person = people[page * 20 + cell]; int col = cell % 5, row = cell / 5;
                    var image = Ui.Rect(paper, person.Id); Position(image, 32 + col * 310, 76 + row * 414, 264, 352);
                    image.gameObject.AddComponent<RawImage>().texture = GameUi.PortraitOf(person.Id);
                    var label = Ui.Label(paper, person.Name + " · " + person.Age + "岁\n" + person.Id, 18, TextAnchor.MiddleCenter, new Color(.22f, .16f, .11f));
                    Position(label.rectTransform, 32 + col * 310, 432 + row * 414, 264, 46);
                }
                Shot("catalogue-" + (page + 1).ToString("D2"), 1600, 1760);
                Object.DestroyImmediate(go);
            }
            foreach (string id in new[] { "keeper_agent", "momo", "life_designer", "landlady", "life_creator", "resident_hanzheng", "story_shenxiao" })
            {
                _game.Ui.CloseAllWindows(); _game.Ui.OpenCityPersonProfile(id); Frame(0);
                Shot("profile-" + id, 1280, 720);
            }
            _game.Ui.CloseAllWindows();
            typeof(GameSession).GetProperty("CurrentPlace").SetValue(_game.Session, _game.Session.Home);
            var samePlace = _game.Session.CityPersonState("momo"); samePlace.Route = null;
            samePlace.LocationPlaceId = _game.Session.CurrentPlace.Id;
            _game.Ui.OpenPersonDialogue("momo", firstReply: "这次合作先把适用范围和准备工作讲清楚。等下一次见面，我们再一起看实际反馈。");
            Frame(0); Shot("dialogue-momo", 1280, 720);
            Log("RETRO NPC PORTRAITS GRAPHICS COMPLETE");
        });

        private static void Position(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
