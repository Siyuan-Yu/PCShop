using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class RelationshipExperienceCapture
{
    private const string Pending = "PCShop.Qa.RelationshipExperience";
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string Output => Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
    private static GameController _game;
    private static GameSession Session => _game.Session;
    private static int _errors;

    static RelationshipExperienceCapture()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                EditorApplication.delayCall += Capture;
        };
    }

    public static void Run()
    {
        CityMapBaker.EnsureBaked(false);
        SessionState.SetBool(Pending, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void Capture()
    {
        SessionState.SetBool(Pending, false);
        Application.logMessageReceived += RecordError;
        try
        {
            Directory.CreateDirectory(Output);
            SaveStore.DirectoryOverride = Path.Combine(Output, "saves");
            _game = new GameObject("感情线实机验收").AddComponent<GameController>();
            _game.Bootstrap(false, 914);
            _game.enabled = false;
            _game.Ui.QuestPopups = false;
            Session.EventsEnabled = false;
            Session.Time.SetPaused(true);
            Session.DebugAddCash(200000000);
            Session.SetSmartphone(true);
            Session.DebugRentStarterShop(true);
            ((DistrictProgress)typeof(GameSession).GetField("_district", Instance).GetValue(Session)).UnlockedTier = 3;
            Session.Clock.Restore(Session.Clock.Today, 600);
            foreach (var shop in Session.MyShops) shop.Sim.Settings.BaseCustomersPerHour = 0;

            CheckMarketFriendAndDate();
            CheckSoloActorAtSameVenue();
            CheckSingerStory();
            Check(_errors == 0, "Play Mode 未产生异常或错误");
            File.WriteAllText(Path.Combine(Output, "result.txt"), "完成。隔离存档准备资金、熟悉度与街区，正式活动和对话操作。");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "failure.txt"), e.ToString());
            EditorApplication.Exit(1);
        }
        finally { Application.logMessageReceived -= RecordError; }
    }

    private static void CheckMarketFriendAndDate()
    {
        const string person = "keeper_phone";
        Meet(person, 80);
        _game.Ui.OpenPersonDialogue(person);
        Click("聊聊兴趣");
        Check(HasText("耳朵补过两次"), "兴趣可在正常对话直接打开，显示人物自己的话");
        Shot("01-唐晓芸聊兴趣");
        var visit = Schedule(person, "oldmarket_visit");
        StartAtVenue(visit);
        Click("旧兔子的补丁");
        Check(!TextureVisible(person, 1280), "好友版没有提前显示交往 CG");
        Shot("02-唐晓芸好友市场");
        Click("挑深蓝布");
        Check(HasText("三颗歪星星"), "蓝布分支有独立回答");
        CloseDialogue();
        Check(Session.FinishManualCityActivity(visit.Id), "好友赴约结算");
        NextMorning(person);
        double chance = GameSession.ConfessionChance(Session.Relation(person));
        Session.Random.State = Enumerable.Range(1, 1000).Select(seed => (ulong)seed).First(seed => new GameRandom(seed).Chance(chance));
        _game.Ui.OpenPersonDialogue(person);
        Click("表白");
        Check(Session.IsPlayerPartner(person), "对话里的表白进入交往状态");
        var date = Schedule(person, "oldmarket_visit");
        StartAtVenue(date);
        Click("旧兔子的补丁");
        Check(HasText("你选的蓝布上"), "交往版回应记得任意先前分支");
        Check(TextureVisible(person, 1280), "交往版显示角色专属 CG");
        Shot("03-唐晓芸交往市场CG");
        Click("教我缝最后一针");
        Check(TextureVisible(person, 512), "回应后切换到剧情表情");
        Shot("04-唐晓芸表情");
        CloseDialogue();
        Click("牵手");
        Check(HasText("把兔子的耳朵"), "伴侣牵手回应会提到这个人的东西");
        Shot("05-唐晓芸牵手");
        CloseDialogue();
        Check(!Buttons().Any(button => Caption(button) == "牵手" && button.IsInteractable()), "同一场约会只记录一次牵手");
        Click("聊聊心事");
        Check(HasText("那一针是你缝的"), "聊心事会回应本次真实选择");
        CloseDialogue();
        Check(Session.FinishManualCityActivity(date.Id), "正式交往赴约结算");
        using var saved = GameSession.Load(Session.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(Session.CreateSave())));
        Check(saved.IsPlayerPartner(person) && saved.RelationshipIllustrationTitle(person) == "旧兔子的补丁", "存读档保留伴侣及回看权限");
        _game.Ui.CloseAllWindows();
        _game.Ui.OpenCityPersonProfile(person);
        Click("回看 · 旧兔子的补丁");
        Check(TextureVisible(person, 1280), "资料页可以回看同一张 CG");
        Shot("06-唐晓芸资料回看");
        Check(Session.EndRomance(), "结束测试用关系");
    }

    private static void CheckSoloActorAtSameVenue()
    {
        const string person = "res_coder";
        NextMorning(person);
        var visit = Schedule(person, "oldmarket_visit");
        StartAtVenue(visit);
        Click("一号位给你");
        Check(HasText("一号位给你") && !HasText("挑深蓝布"), "同一个市场换人后有不同物件和回应");
        Shot("07-陈志衡同场所");
        Click("换到二号位并排打");
        Check(HasText("其实我更会配合"), "程序员的回应与人物性格对应");
        CloseDialogue();
        Check(Session.FinishManualCityActivity(visit.Id), "陈志衡普通赴约结算");
    }

    private static void CheckSingerStory()
    {
        const string person = "life_singer";
        NextMorning(person);
        _game.Ui.OpenPersonDialogue(person);
        Click("聊一聊");
        Check(Session.HasStartedRelationshipStory(person), "聊天开启温音专属故事");
        NextMorning(person);
        _game.Ui.OpenPersonDialogue(person);
        Click("聊聊见面的事");
        Shot("08-温音邀请");
        Click("约好时间");
        var invitation = Session.CityActivities.Single(a => a.Id == Session.RelationshipStoryAppointmentId(person));
        StartAtVenue(invitation);
        Click("聊聊今天");
        Check(HasText("后半段") && TextureVisible(person, 1280), "练歌房专属剧情显示曲谱 CG");
        Shot("09-温音赴约CG");
        Click("下一遍我跟你接唱");
        Check(TextureVisible(person, 512) && HasText("你听我吸气"), "继续接唱时显示温音的额外表情");
        Shot("10-温音接唱表情");
        CloseDialogue();
        Check(Session.AdvanceKaraokePlayback(invitation.Id, 1), "使用正式结算完成唱歌");
        NextMorning(person);
        _game.Ui.OpenPersonDialogue(person);
        Click("聊聊上次见面");
        Check(HasText("你接进来之前吸了好大一口气"), "隔日见面记得接唱选择");
        Shot("11-温音次日表态");
        Click("继续做朋友");
        Check(!Session.IsPlayerPartner(person) && HasText("下一首写完"), "专属剧情可以继续友情");
    }

    private static CityActivity Schedule(string person, string definition)
    {
        long now = Session.Clock.Now.TotalMinutes;
        long start = Session.SuggestCityActivityStart(definition, person);
        Check(start > now, "可预约 " + person + " " + definition);
        Check(Session.CityActivityInvitationBlocker(definition, person, start, false) == null,
            Session.CityActivityInvitationBlocker(definition, person, start, false) ?? "预约符合真实营业时间和排班");
        var result = Session.ScheduleCityActivityInvitation(definition, person, start, false);
        Check(result != null, "正式邀约进入日程 " + person);
        return result;
    }

    private static void StartAtVenue(CityActivity visit)
    {
        _game.Ui.CloseAllWindows();
        if (visit.ScheduledStart - Session.Clock.Now.TotalMinutes > 1440)
            Session.Clock.Restore(new GameDate((int)(visit.ScheduledStart / 1440)), Math.Max(0, (int)(visit.ScheduledStart % 1440) - 180));
        while (Session.Clock.Now.TotalMinutes < visit.ScheduledStart)
        {
            long before = Session.Clock.Now.TotalMinutes;
            Session.Player.Health = Session.Player.Energy = Session.Player.Hunger = 100;
            Session.AdvanceClock((int)Math.Min(30, visit.ScheduledStart - before));
            if (before >= Session.Clock.Now.TotalMinutes) throw new InvalidOperationException("赴约失败，时钟没有推进。");
        }
        Enter(visit.VenueId);
        Check(Session.SelectVenueService(visit.DefinitionId, 0, visit.Id), "开始活动 " + visit.DefinitionId);
        Check(visit.State == CityActivityPhase.Running, "实际同行者到场");
        _game.Ui.OnEnteredPlace(Session.CurrentPlace, visit.Id);
        _game.Step(0);
        _game.SnapCamera();
    }

    private static void Meet(string person, int relation)
    {
        var state = Session.CityPersonState(person);
        Enter(Session.Home.Id);
        state.Route = null;
        state.LocationPlaceId = Session.Home.Id;
        Session.Meet(person, relation);
    }

    private static void NextMorning(string person)
    {
        Session.Clock.Restore(Session.Clock.Today.AddDays(1), 600);
        Meet(person, 80);
    }

    private static void Enter(string id)
    {
        var place = Session.City.GetPlace(id);
        _game.Ui.CloseAllWindows();
        Session.StopTrip();
        Session.Leave();
        Session.ArriveAtNode(place.NodeId);
        Check(Session.Enter(place), "到达 " + place.Name);
    }

    private static Button[] Buttons() =>
        _game.Ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>()).Where(button => button.isActiveAndEnabled).ToArray();
    private static string Caption(Button button) => button.GetComponentInChildren<Text>()?.text;
    private static bool HasText(string phrase) => _game.Ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>())
        .Any(text => text.text.Contains(phrase));
    private static bool TextureVisible(string id, int width) => _game.Ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<RawImage>())
        .Any(image => image.texture != null && image.texture.name == id && image.texture.width == width);

    private static void Click(string caption)
    {
        var button = Buttons().LastOrDefault(b => Caption(b)?.TrimStart('＋', ' ') == caption && b.IsInteractable());
        Check(button != null, "按钮可操作 " + caption + (button == null ? " · " + string.Join("／", Buttons().Select(Caption)) : ""));
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        _game.Ui.Update(.5f);
        Canvas.ForceUpdateCanvases();
    }

    private static void CloseDialogue()
    {
        var end = Buttons().LastOrDefault(button => button.name == "DialogueEnd");
        Check(end != null, "场景对话能返回活动");
        Click(Caption(end));
    }

    private static void Shot(string name)
    {
        const int width = 1920, height = 1080;
        foreach (var canvas in _game.Ui.Canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _game.Cam;
            canvas.planeDistance = 1;
        }
        var camera = _game.Cam;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            _game.Ui.Update(.5f);
            Canvas.ForceUpdateCanvases();
            foreach (var fit in UnityEngine.Object.FindObjectsOfType<WindowFit>()) fit.Fit();
            Canvas.ForceUpdateCanvases();
            var updateScroll = typeof(ScrollRect).GetMethod("LateUpdate", Instance);
            foreach (var scroll in UnityEngine.Object.FindObjectsOfType<ScrollRect>()) updateScroll.Invoke(scroll, null);
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            File.WriteAllLines(Path.Combine(Output, name + ".txt"), _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Select(t => t.text));
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void Check(bool ok, string text)
    {
        if (!ok) throw new InvalidOperationException(text);
        File.AppendAllText(Path.Combine(Output, "checks.txt"), text + "\n");
    }

    private static void RecordError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        _errors++;
        File.AppendAllText(Path.Combine(Output, "errors.txt"), message + "\n" + stack + "\n");
    }
}
