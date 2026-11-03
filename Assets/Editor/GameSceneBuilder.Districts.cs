using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Catalog;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void DistrictAcceptanceFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/district-progression");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            typeof(GameUi).GetMethod("OpenDistrictAcceptance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_game.Ui,null);
            Frame(0);_game.SnapCamera();Shot("three-prerequisite-buttons",1280,720);
            Check(s.DebugPrepareDistrictPrerequisites(1),"一键准备真实五家店与100万元");
            Check(s.DistrictOperatingShops(0)==5&&s.DistrictAccessTier==0,"只准备条件，不提前开路");
            for(int i=0;s.CurrentDistrictNode==null&&i<100;i++)s.AdvanceClock((int)(s.DistrictStoryState.InvitationDueAt-s.Clock.Now.TotalMinutes));
            _game.Ui.CloseAllWindows();_game.Ui.OpenChatPanel();Frame(0);_game.SnapCamera();Shot("chat-main-story",1280,720);
            _game.Ui.OpenDistrictStory();Frame(0);_game.SnapCamera();Shot("first-invitation",1280,720);
            Check(s.BeginDistrictMeeting("community.intro","accept"),"接受引荐后安排真实人物赴约");
            _game.Ui.CloseAllWindows();
            var gate=s.DistrictRoadGates.First();
            s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,null);
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,gate.FromNode);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)new MapPoint(gate.Center.X-gate.Direction.X*3,gate.Center.Y-gate.Direction.Y*3));
            Frame(0);var view=Object.FindObjectOfType<DistrictGateView>();view.Refresh();
            Check(GameObject.Find("DistrictCheckpoint_1")!=null,"主线完成前路障显示");_game.SnapCamera();Shot("road-closed",1280,720);
            s.Time.SetPaused(false);s.Time.SetSpeed(8);s.SetSteer(gate.Direction.X,gate.Direction.Y);
            for(int i=0;i<10;i++)Frame(.1f);
            _game.SnapCamera();Shot("road-stopped",1280,720);
            Check(s.DistrictAccessTier==0,"走到路障处提示，仍未获得资格");
            _game.Ui.CloseAllWindows();s.Time.SetPaused(true);
            long at=s.DistrictStoryState.MeetingAt;
            for(int i=0;s.Clock.Now.TotalMinutes<at+1&&i<50;i++)s.AdvanceClock((int)(at+1-s.Clock.Now.TotalMinutes));
            Teleport(s,"agency");_game.Ui.CloseAllWindows();
            Check(s.ActDistrictNode("community.meeting"),"当面领取寻物委托");
            Check(s.DistrictAccessTier==0,"领取委托不解锁道路");
            Check(s.BeginDistrictMeeting("community.clue"),"约见李秀兰询问寄存线索");
            at=s.DistrictStoryState.MeetingAt;
            for(int i=0;s.Clock.Now.TotalMinutes<at+1&&i<50;i++)s.AdvanceClock((int)(at+1-s.Clock.Now.TotalMinutes));
            s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,null);
            var cluePlace=s.City.GetPlace("life_home_s_oldhomes");
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,cluePlace.NodeId);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)s.StandOf(cluePlace));
            Feed(s);_game.Ui.CloseAllWindows();Frame(0);_game.SnapCamera();Frame(.2f);
            Check(GameObject.Find("DistrictPersonMarker_landlady")!=null,"李秀兰会面头顶显示主线标识");
            Shot("community-meeting-marker",1280,720);
            var marker=GameObject.Find("DistrictPersonMarker_landlady");
            Log("QUEST MARKER objective="+s.DistrictPersonObjective("landlady")+" pos="+marker?.GetComponent<RectTransform>().anchoredPosition+" canvas="+marker?.GetComponentInParent<Canvas>().name);
            Check(s.ActDistrictNode("community.clue"),"在住宅门前实际会面取得线索");
            _game.Ui.OpenDistrictStory();Frame(0);_game.SnapCamera();Shot("community-search-clue",1280,720);_game.Ui.CloseAllWindows();
            var boxes=Object.FindObjectOfType<CommunitySearchView>();boxes.Refresh();
            Check(GameObject.Find("CommunityBox_0")==null&&GameObject.Find("CommunityBox_1")==null&&GameObject.Find("CommunityBox_2")==null,"未取得线索前不生成箱子");
            // Graphics fixture supplies completed-outing evidence. EditMode progression tests
            // independently exercise actual scheduling, service choice and timed settlement.
            var search=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
            foreach(int i in new[]{0,2,1})
            {
                string person=GameSession.CommunityLeadPerson(i);s.Meet(person,40);s.AddRelation(person,40);
                search.CommunityOutings|=1<<i;s.AskCommunityLead(i,true);Check(s.CommunityHasClue(i),"取得独立线索 "+i);
                s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,null);
                typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.City.GetPlace(GameSession.CommunityBoxPlace(i)).NodeId);
                typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)s.CommunityBoxPosition(i));
                Feed(s);Frame(.2f);boxes.Refresh();_game.SnapCamera();Frame(.2f);
                Check(GameObject.Find("CommunityBox_"+i)!=null,"取得线索后出现对应箱子 "+i);
                Check(_game.TryGetInteract(out _,out string hint)&&hint.Contains(GameSession.CommunityBoxName(i)),"对应箱子的F提示 "+i);
                Shot("community-fragment-box-"+i,1280,720);
                Check(s.SearchCommunityBox(i),"现场取得一份图页 "+i);_game.Ui.CloseAllWindows();boxes.Refresh();
                Check(GameObject.Find("CommunityBox_"+i)==null,"取物后该箱隐藏 "+i);
                Check(!s.DistrictStoryState.HasCommunityMap&&s.DistrictAccessTier==0,"单份图页不自动拼图或开路");
            }
            _game.Ui.OpenDistrictStory();Frame(0);Shot("community-three-pieces",1280,720);_game.Ui.CloseAllWindows();
            Check(s.AssembleCommunityMap(),"三份图页手动拼合");
            Frame(.2f);_game.SnapCamera();Shot("community-map-found",1280,720);_game.Ui.CloseAllWindows();
            Check(s.BeginDistrictMeeting("community.trial"),"预约交还地图");at=s.DistrictStoryState.MeetingAt;
            for(int i=0;s.Clock.Now.TotalMinutes<at+1&&i<50;i++)s.AdvanceClock((int)(at+1-s.Clock.Now.TotalMinutes));
            Teleport(s,"agency");_game.Ui.CloseAllWindows();Check(s.ActDistrictNode("community.trial"),"当面交付后引荐");
            Check(s.DistrictAccessTier==1&&!s.DistrictRoadOpen(2),"只解锁第一条路");
            _game.Ui.CloseAllWindows();s.Leave();
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,gate.FromNode);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)gate.Center);
            Frame(0);view.Refresh();Check(GameObject.Find("DistrictCheckpoint_1")==null,"完成主线后路障移除");_game.SnapCamera();Shot("road-open",1280,720);
            Log("DISTRICT ACCEPTANCE GRAPHICS COMPLETE");
        });
    }
}
