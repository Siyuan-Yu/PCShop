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
        public static void CommunityWalkthroughFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/community-walkthrough");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetSpeed(8);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            void Must(bool ok,string message){if(!ok)throw new System.Exception("WALKTHROUGH: "+message);Log("WALKTHROUGH "+message);}
            Button[] Buttons()=>_game.Ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            string Label(Button b)=>b.GetComponentInChildren<Text>()?.text??"";
            void Click(string key,bool byName=false)
            {
                var b=Buttons().FirstOrDefault(x=>(byName?x.name==key:Label(x).StartsWith(key))&&x.interactable);
                Must(b!=null,"可点击 "+key+" ["+string.Join("|",Buttons().Select(Label))+"]");b.onClick.Invoke();Frame(0);
            }
            int shot=0;
            void Capture(string name){Frame(.41f);_game.SnapCamera();Shot((++shot).ToString("D2")+"-"+name,1920,1080);}
            void Guide(string name){_game.Ui.CloseAllWindows();_game.Ui.OpenDistrictStory();Capture(name);}
            void Healthy(){Feed(s);s.Player.Energy=s.Player.MaxEnergy;}
            void Walk()
            {
                _game.Ui.CloseAllWindows();s.Time.SetPaused(false);
                int i=0;
                while((s.Trip!=null||s.CityIntent!=null)&&i++<40000)
                {
                    Healthy();if(_game.Ui.AnyModalOpen)_game.Ui.CloseAllWindows();Frame(.25f);
                }
                Must(i<40000,"路线正常到达 "+s.PlayerNodeId+" place="+s.CurrentPlace?.Id);
                _game.Ui.CloseAllWindows();Frame(0);
            }
            void WaitUntil(long minute)
            {
                _game.Ui.CloseAllWindows();
                while(s.Clock.Now.TotalMinutes<minute){Healthy();s.AdvanceClock((int)System.Math.Min(15,minute-s.Clock.Now.TotalMinutes));}
                _game.Ui.CloseAllWindows();Frame(0);
            }
            void NearPerson(string id)
            {
                for(int attempt=0;attempt<30;attempt++)
                {
                    Guide("find-"+id);
                    var nav=Buttons().FirstOrDefault(b=>b.name=="DistrictFindPerson_"+id);
                    if(nav!=null){nav.onClick.Invoke();Walk();}
                    else _game.Ui.CloseAllWindows();
                    var person=s.CityPersonState(id);
                    if(s.DistrictPersonWaitingPlace(id)!=null)
                    {
                        Log("WALKTHROUGH 等待人物按真实日程抵达可接触地点 "+id);
                        WaitUntil(s.Clock.Now.TotalMinutes+60);continue;
                    }
                    if(s.CityPersonInside(person)&&person.LocationPlaceId!=s.CurrentPlace?.Id)
                    {
                        var place=s.City.GetPlace(person.LocationPlaceId);
                        if(s.NearbyPlace?.Id!=place.Id){Log("WALKTHROUGH 人物已换地点，刷新导航 "+id);continue;}
                        Must(true,"找到人物所在建筑门口 "+id);
                        typeof(GameController).GetMethod("TryEnterNearby",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_game,null);Frame(0);
                        if(place.Kind==PlaceKind.Home)
                        {
                            Log("WALKTHROUGH HOME relation="+s.Relation(id)+" home="+person.HomePlaceId+" location="+person.LocationPlaceId+" blocker="+s.ClosedReason(place));
                            Capture("home-entry");Click("ResidenceEnter",true);
                        }
                        _game.Ui.CloseAllWindows();Frame(0);
                    }
                    if(_game.InVisit)
                    {
                        var figures=(System.Collections.Generic.Dictionary<string,GameObject>)typeof(HomeRoomView)
                            .GetField("_visitResidents",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_game.VisitRoom);
                        for(int step=0;step<400&&!_game.VisitRoom.CanReachResident(id)&&figures.ContainsKey(id);step++)
                        {
                            var delta=figures[id].transform.position-_game.VisitRoom.OwnerWorld;delta.y=0;
                            _game.VisitRoom.MoveOwner(delta.normalized,.05f);Frame(0);
                        }
                        Must(_game.VisitRoom.CanReachResident(id),"实际走到屋内住户身边 "+id);
                    }
                    if(s.CitySamePlace(id))return;
                    Log("WALKTHROUGH 人物移动，刷新路线 "+id);
                }
                throw new System.Exception("WALKTHROUGH 找人导航未到本人身边 "+id);
            }
            void Talk(string id){_game.Ui.CloseAllWindows();_game.Ui.OpenPersonDialogue(id);Frame(0);Must(_game.Ui.AnyModalOpen,"打开当面对话 "+id);}
            void AttendMeeting(string action)
            {
                Guide("meeting-route");
                if(!s.AtCurrentDistrictMeeting){Click("DistrictGoToMeeting",true);Walk();}
                else Must(!Buttons().Any(b=>b.name=="DistrictGoToMeeting"),"本来就在会面点，不重复导航");
                WaitUntil(s.DistrictStoryState.MeetingAt+1);
                Must(s.AtCurrentDistrictMeeting&&s.DistrictMeetingBlocker==null,"双方实际到达会面点");
                Guide("meeting-arrived");Must(!Buttons().Any(b=>b.name=="DistrictGoToMeeting"),"到场不再提示前往");
                var id=s.DistrictStoryState.MeetingPeople.First();Talk(id);Capture("meeting-talk");Click(action);Capture("meeting-reply");
            }
            // Test setup only. No quest evidence, actor positions or activity results are injected.
            Must(s.DebugPrepareDistrictPrerequisites(1),"准备第一段资产门槛");
            for(int i=0;i<3;i++)
            {
                string id=GameSession.CommunityLeadPerson(i);s.Meet(id,65);s.AddRelation(id,65-s.Relation(id));
            }
            WaitUntil(s.DistrictStoryState.InvitationDueAt);
            Guide("invitation");Must(!Buttons().Any(b=>b.name=="DistrictGoToMeeting"),"邀请未回复前无会面路线");
            Click("DistrictChoice_accept",true);
            Must(s.DistrictStoryState.MeetingNodeId==null,"回复只获得陈启明的线索，不自动预约");
            NearPerson("keeper_agent");Talk("keeper_agent");Click("请帮我约唐蔚出来见面");
            AttendMeeting("答应帮忙找回三页图");
            Must(s.CurrentDistrictNode.Id=="community.clue","接受委托后进入找李秀兰阶段");
            NearPerson("landlady");Talk("landlady");Click("商量主线会面：");
            AttendMeeting("询问三页图分别由谁保管");
            Must(s.CommunitySearchActive,"开始三人寻物");
            for(int i=0;i<3;i++)
            {
                string id=GameSession.CommunityLeadPerson(i),defId=GameSession.CommunityLeadActivity(i);
                NearPerson(id);Talk(id);Click("邀请活动");
                var venue=s.City.GetPlace(GameSession.CommunityLeadVenue(i));
                Click("CityActivityOffer_"+defId,true);Capture("outing-offer-"+i);Click("确认邀约，加入日程");Click("确定办理");
                var activity=s.CityActivities.Last(a=>a.Active&&a.Participants.Any(p=>p.PersonId==id));
                Guide("outing-route-"+i);
                Must(!Buttons().Any(b=>b.name=="DistrictFindPerson_"+id),"预约后只提示赴约，不再提示找人 "+i);
                if(s.CurrentPlace?.Id!=venue.Id){Click("DistrictGoToActivity_"+id,true);Walk();}
                else Must(!Buttons().Any(b=>b.name=="DistrictGoToActivity_"+id),"已在活动场所不重复显示导航 "+i);
                WaitUntil(System.Math.Max(s.Clock.Now.TotalMinutes,activity.ScheduledStart));
                Log("WALKTHROUGH ENTRY now="+s.Clock.Now+" scheduled="+activity.ScheduledStart+" venue="+venue.Id+" near="+s.NearbyPlace?.Id+" pos="+s.PlayerPosition+" door="+s.DoorOf(venue)+" closed="+s.ClosedReason(venue)+" current="+s.CurrentPlace?.Id+" activity="+activity.State);
                if(s.CurrentPlace?.Id!=venue.Id)Must(s.EnterNearby(),"进入活动地点 "+venue.Name);Frame(0);
                _game.Ui.OnEnteredPlace(venue);Frame(0);
                int wait=0;
                while(!activity.RendezvousReady&&wait++<65){WaitUntil(s.Clock.Now.TotalMinutes+1);}
                Must(activity.RendezvousReady,"同行人按实际路线抵达 "+i);
                _game.Ui.CloseAllWindows();_game.Ui.OnEnteredPlace(venue);Frame(0);Capture("outing-service-"+i);
                var service=Buttons().FirstOrDefault(b=>(Label(b)=="选择这项服务"||Label(b)=="点这份餐")&&b.interactable
                    &&b.transform.parent.parent.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(GameSession.VenueServiceName(CityLifeCatalog.Activity(defId),0))));
                Must(service!=null,"场所菜单中存在对应活动服务 "+defId);service.onClick.Invoke();Frame(0);
                Must(activity.State==CityActivityPhase.Running,"实际选择服务后活动开始 "+i);
                WaitUntil(activity.ActualStart+activity.Duration+1);
                Must(activity.State==CityActivityPhase.Completed&&s.CommunityHasOuting(i),"实际参加并结算活动 "+i);
                Must(s.CommunityHasClue(i),"活动结算直接取得线索，无需再次找人 "+i);
                _game.Ui.CloseAllWindows();_game.Ui.OpenCityActivitySettlement(activity.Id);Frame(0);Capture("outing-settlement-"+i);
                Click("收好这段回忆");Capture("outing-clue-"+i);
                // An automatically displayed settlement may already have delivered this one-time dialogue.
                if(Buttons().Any(b=>Label(b)=="记下线索"))Click("记下线索");
                Guide("box-route-"+i);Must(!Buttons().Any(b=>b.name=="DistrictFindPerson_"+id),"有线索后只提示箱子 "+i);
                Click("前往藏箱地点");Walk();Capture("box-arrived-"+i);
                Must(s.NearbyCommunityBox==i,"实际走到对应箱子 "+i);
                Must(_game.TryGetInteract(out _,out var hint)&&hint.Contains(GameSession.CommunityBoxName(i)),"现场显示箱子F提示 "+i);
                Must(s.SearchCommunityBox(i),"通过现场交互取得图页 "+i);
                Guide("piece-complete-"+i);Must(!Buttons().Any(b=>b.name=="DistrictFindPerson_"+id),"取物后隐藏该人物指引 "+i);
            }
            _game.Ui.CloseAllWindows();_game.Ui.OpenBackpackPanel();Capture("assemble-in-backpack");Click("CommunityAssembleMap",true);
            Must(s.DistrictStoryState.HasCommunityMap,"背包拼图成功");
            Guide("delivery-find-person");
            var delivery=Buttons().First(b=>b.name.StartsWith("DistrictFindPerson_"));string receiver=delivery.name.Substring("DistrictFindPerson_".Length);
            NearPerson(receiver);Talk(receiver);Click("商量主线会面：");AttendMeeting("交还手绘商铺图");
            Must(s.DistrictAccessTier==1&&s.CurrentDistrictNode==null&&!s.DistrictStoryState.HasCommunityMap,"交付后移除任务物品、结束第一段、只开放社区");
            Guide("completed");Must(!Buttons().Any(b=>b.name.StartsWith("DistrictFindPerson_")||b.name=="DistrictGoToMeeting"),"完成后不残留主线导航");
            Log("COMMUNITY WALKTHROUGH COMPLETE - production navigation, conversations, outings and delivery");
        });

        public static void DistrictAcceptanceFromBatch()=>RunSmokeBatch(()=>
        {
            _outDir=Path.GetFullPath("docs/previews/district-progression");Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation=false;UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);BuildRig();
            _game=new GameObject("Game").AddComponent<GameController>();_game.Bootstrap(false,7);_game.Ui.QuestPopups=false;
            var s=_game.Session;s.EventsEnabled=false;s.Time.SetPaused(true);
            foreach(var canvas in _game.Ui.Canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_game.Cam;canvas.planeDistance=1;}
            var residence=s.City.Blocks.Where(block=>block.tier==0)
                .Select(block=>s.City.GetPlace(block.homeId))
                .First(place=>s.CityPeople.Count(person=>person.HomePlaceId==place.Id)>=2);
            s.Leave();s.ArriveAtNode(residence.NodeId);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(s,(MapPoint?)s.StandOf(residence));
            var residents=s.CityPeople.Where(person=>person.HomePlaceId==residence.Id).ToArray();
            residents[0].Route=null;residents[0].Task=CityPersonTask.Home;residents[0].LocationPlaceId=residence.Id;
            residents[1].LocationPlaceId=null;
            typeof(GameController).GetMethod("TryEnterNearby",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_game,null);
            Check(_game.Ui.Canvases.SelectMany(canvas=>canvas.GetComponentsInChildren<Text>())
                .Any(label=>label.text.Contains("不在家")),"场景住宅门口可查看外出住户");
            Check(s.CurrentPlace==null,"先查看住户面板，再决定是否进屋");
            Frame(0);_game.SnapCamera();Shot("residence-roster",1280,720);
            _game.Ui.CloseAllWindows();
            s.Meet(residents[0].PersonId,GameSession.HomeVisitMinRelation);
            _game.Ui.OpenResidence(residence);
            _game.Ui.Canvases.SelectMany(canvas=>canvas.GetComponentsInChildren<Button>())
                .Single(button=>button.name=="ResidenceEnter").onClick.Invoke();
            Frame(0);_game.SnapCamera();
            Check(_game.InVisit && _game.VisitRoom.Root.gameObject.activeInHierarchy && !_game.World.Root.activeSelf,
                "进屋拜访切换到可走动的住宅室内");
            Check(!_game.VisitRoom.CanReachResident(residents[0].PersonId),"门边够不到屋里的住户");
            _game.Ui.OpenCityPerson(residents[0].PersonId);
            Check(!_game.Ui.AnyModalOpen,"离住户太远不能打开面对面对话");
            for(int i=0;i<21;i++)_game.VisitRoom.MoveOwner(Vector3.left,.05f);
            for(int i=0;i<10;i++)_game.VisitRoom.MoveOwner(Vector3.forward,.05f);
            Frame(0);
            Check(_game.VisitRoom.CanReachResident(residents[0].PersonId),"走到住户身边可交谈");
            Shot("residence-interior",1280,720);
            var visitInteract=typeof(GameController).GetMethod("InteractVisitRoom",BindingFlags.Instance|BindingFlags.NonPublic);
            visitInteract.Invoke(_game,null);
            Check(_game.Ui.AnyModalOpen,"在屋里按F打开具名住户对话");
            _game.Ui.CloseAllWindows();
            for(int i=0;i<150 && _game.VisitRoom.WalkToDoor(.05f)==0;i++){}
            Frame(0);visitInteract.Invoke(_game,null);Frame(0);
            Check(!_game.InVisit && _game.World.Root.activeSelf,"走回门边按F返回街道");

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
            void AtStreetMeeting()
            {
                s.StopTrip();
                s.SetSteer(0, 0);
                s.ArriveAtNode(s.DistrictMeetingStreetNode);
                Feed(s);
                _game.Ui.CloseAllWindows();
                Check(s.NearbyPlace == null, "街边会面点不与建筑入口重叠");
            }
            long at=s.DistrictStoryState.MeetingAt;
            for(int i=0;s.Clock.Now.TotalMinutes<at+1&&i<50;i++)s.AdvanceClock((int)(at+1-s.Clock.Now.TotalMinutes));
            AtStreetMeeting();
            Check(s.ActDistrictNode("community.meeting"),"当面领取寻物委托");
            Check(s.DistrictAccessTier==0,"领取委托不解锁道路");
            Check(s.BeginDistrictMeeting("community.clue"),"约见李秀兰询问寄存线索");
            at=s.DistrictStoryState.MeetingAt;
            for(int i=0;s.Clock.Now.TotalMinutes<at+1&&i<50;i++)s.AdvanceClock((int)(at+1-s.Clock.Now.TotalMinutes));
            AtStreetMeeting();
            Frame(0);_game.SnapCamera();Frame(.2f);
            Check(GameObject.Find("DistrictPersonMarker_landlady")!=null,"李秀兰会面头顶显示主线标识");
            Shot("community-meeting-marker",1280,720);
            var marker=GameObject.Find("DistrictPersonMarker_landlady");
            Log("QUEST MARKER objective="+s.DistrictPersonObjective("landlady")+" pos="+marker?.GetComponent<RectTransform>().anchoredPosition+" canvas="+marker?.GetComponentInParent<Canvas>().name);
            Check(s.ActDistrictNode("community.clue"),"在街边实际会面取得线索");
            _game.Ui.OpenDistrictStory();Frame(0);_game.SnapCamera();Shot("community-search-clue",1280,720);_game.Ui.CloseAllWindows();
            var boxes=Object.FindObjectOfType<CommunitySearchView>();boxes.Refresh();
            Check(GameObject.Find("CommunityBox_0")==null&&GameObject.Find("CommunityBox_1")==null&&GameObject.Find("CommunityBox_2")==null,"未取得线索前不生成箱子");
            // Graphics fixture supplies completed-outing evidence. EditMode progression tests
            // independently exercise actual scheduling, service choice and timed settlement.
            var search=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
            foreach(int i in new[]{0,2,1})
            {
                string person=GameSession.CommunityLeadPerson(i);s.Meet(person,40);s.AddRelation(person,40);
                var lead=s.CityPersonState(person);lead.Route=null;lead.LocationPlaceId=null;lead.Task=CityPersonTask.Waiting;
                s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,null);
                typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.City.NearestNode(lead.X,lead.Y));
                typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)new MapPoint(lead.X,lead.Y));
                search.CommunityOutings|=1<<i;s.AskCommunityLead(i);Check(s.CommunityHasClue(i),"当面取得独立线索 "+i);
                s.StopTrip();typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,null);
                typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,s.CommunityBoxNode(i));
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
            AtStreetMeeting();Check(s.ActDistrictNode("community.trial"),"当面交付后引荐");
            Check(s.DistrictAccessTier==1&&!s.DistrictRoadOpen(2),"只解锁第一条路");
            _game.Ui.CloseAllWindows();s.Leave();
            typeof(GameSession).GetProperty("PlayerNodeId").SetValue(s,gate.FromNode);
            typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(MapPoint?)gate.Center);
            Frame(0);view.Refresh();Check(GameObject.Find("DistrictCheckpoint_1")==null,"完成主线后路障移除");_game.SnapCamera();Shot("road-open",1280,720);
            Log("DISTRICT ACCEPTANCE GRAPHICS COMPLETE");
        });
    }
}
