using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Game;
using PCShopSell.Core.Session;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class DistrictUiTests
    {
        [TestCase("community.meeting",1)] [TestCase("community.clue",1)] [TestCase("community.trial",1)]
        [TestCase("park.proposal",2)] [TestCase("park.change",2)] [TestCase("park.delivery",2)]
        [TestCase("model.dinner",2)] [TestCase("model.clues",2)] [TestCase("model.repair",2)] [TestCase("model.delivery",2)]
        [TestCase("park.recommendation",2)] [TestCase("garden.invitation",3)] [TestCase("garden.proposal",3)]
        [TestCase("garden.terms",3)] [TestCase("garden.fulfilment",3)] [TestCase("garden.echo",3)]
        public void QuestTrackerCannotPerformInPersonActions(string node,int chapter)
        {
            using(var s=DistrictProgressionTests.Make())
            {
                typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,
                    new DistrictProgress{Chapter=chapter,UnlockedTier=chapter-1,NodeId=node});
                var root=new GameObject("QuestGuideAudit");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var previous=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                try
                {
                    var before=JsonUtility.ToJson(s.DistrictStoryState);
                    ui.OpenDistrictStory();
                    Assert.AreEqual(before,JsonUtility.ToJson(s.DistrictStoryState));
                    foreach(var button in ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()))
                    {
                        string label=button.GetComponentInChildren<Text>()?.text??"";
                        Assert.False(button.name=="DistrictAcceptMeeting"||button.name=="DistrictCompleteNode"||button.name.StartsWith("DistrictChoice_"),node);
                        Assert.False(label.StartsWith("联系 ")||label=="现场核对并记录"||label.StartsWith("已选 · "),label);
                    }
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }

        [Test] public void ResidenceDoorShowsAllResidentsBeforeCheckingEntry()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                var resident = s.CityPersonState("landlady");
                var home = s.City.GetPlace(resident.HomePlaceId);
                resident.Route = null;
                resident.LocationPlaceId = home.Id;
                resident.Task = CityPersonTask.Home;
                s.Leave();
                s.ArriveAtNode(home.NodeId);
                typeof(GameSession).GetField("_free", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(s, (PCShopSell.Core.World.MapPoint?)s.StandOf(home));
                s.AddRelation(resident.PersonId, 59 - s.Relation(resident.PersonId));

                var root = new GameObject("ResidenceUiTest");
                root.SetActive(false);
                var controller = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller, s);
                typeof(GameController).GetProperty("City").SetValue(controller, s.City);
                var previous = UnityEngine.EventSystems.EventSystem.current;
                var ui = new GameUi(controller);
                typeof(GameController).GetProperty("Ui").SetValue(controller, ui);
                var interact = typeof(GameController).GetMethod("TryEnterNearby", BindingFlags.Instance | BindingFlags.NonPublic);
                Button EnterButton() => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>())
                    .Single(b => b.name == "ResidenceEnter");
                bool HasStatus(string value) => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>())
                    .Any(t => t.text.Contains(s.CityPerson(resident.PersonId).Name) && t.text.Contains(value));
                try
                {
                    interact.Invoke(controller, null);
                    Assert.IsNull(s.CurrentPlace);
                    Assert.True(HasStatus("在家"));
                    Assert.False(EnterButton().interactable);
                    var portrait = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>())
                        .First(b => b.targetGraphic is RawImage);
                    portrait.onClick.Invoke();
                    Assert.True(ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>())
                        .Any(t => t.text.Contains("人物资料")));
                    Assert.IsNull(s.CurrentPlace);


                    s.AddRelation(resident.PersonId, 1);
                    resident.LocationPlaceId = null;
                    ui.CloseAllWindows();
                    interact.Invoke(controller, null);
                    Assert.True(HasStatus("不在家"));
                    Assert.False(EnterButton().interactable);

                    resident.LocationPlaceId = home.Id;
                    ui.CloseAllWindows();
                    interact.Invoke(controller, null);
                    Assert.True(EnterButton().interactable);
                    EnterButton().onClick.Invoke();
                    Assert.AreEqual(home, s.CurrentPlace);
                    Assert.True(s.CitySamePlace(resident.PersonId));
                    Assert.True(controller.InVisit);
                    Assert.False(ui.AnyModalOpen, "Entering must close the doorstep roster");
                    s.Leave();
                    Assert.IsNull(s.CurrentPlace);
                }
                finally
                {
                    ui.CloseAllWindows();
                    foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                        UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }

        [Test] public void AllRelationsCheatIncludesNamedResidentsExactlyOnce()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                var before = s.CityPeople.ToDictionary(person => person.PersonId, person => s.Relation(person.PersonId));
                s.DebugRaiseAllRelations(20);
                foreach (var person in s.CityPeople)
                {
                    string id = person.PersonId;
                    Assert.True(s.IsMet(id), id);
                    Assert.AreEqual(Math.Min(100, before[id] + 20), s.Relation(id), id);
                }
            }
        }

        [Test] public void ThreeCheatButtonsLeadToChatAndCannotCompleteMeetingRemotely()
        {
            using(var s=DistrictProgressionTests.Make())
            {
                var root=new GameObject("DistrictUiTest");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var previous=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(controller);
                Button Find(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name==name);
                try
                {
                    typeof(GameUi).GetMethod("OpenDistrictAcceptance",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    Assert.True(Find("DistrictPrerequisites_1").interactable);
                    Assert.False(Find("CheatCommunityConditions").interactable);
                    Assert.False(s.DebugPrepareCommunityConditions());
                    Assert.False(Find("DistrictPrerequisites_2").interactable);Assert.False(Find("DistrictPrerequisites_3").interactable);
                    Find("DistrictPrerequisites_1").onClick.Invoke();Assert.Zero(s.DistrictAccessTier);
                    ui.CloseAllWindows();ui.DebugRefreshQuests();
                    StringAssert.Contains("主线 · 等待联系",ui.QuestTrackerText);
                    StringAssert.Contains(s.DistrictInvitationStatus,ui.QuestTrackerText);
                    ui.OpenQuests();Find("DistrictStoryMessage").onClick.Invoke();
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("72小时")));
                    ui.CloseAllWindows();
                    DistrictProgressionTests.AwaitInvitation(s);ui.CloseAllWindows();ui.OpenChatPanel();Find("DistrictStoryMessage").onClick.Invoke();
                    ui.DebugRefreshQuests();StringAssert.Contains(s.CurrentDistrictNode.Title,ui.QuestTrackerText);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictGoToMeeting"));
                    StringAssert.Contains("回复李秀兰",Find("DistrictChoice_accept").GetComponentInChildren<Text>().text);
                    var beforeReply=s.PlayerPosition;
                    Find("DistrictChoice_later").onClick.Invoke();
                    Assert.AreEqual("community.intro",s.CurrentDistrictNode.Id);
                    Assert.IsNull(s.DistrictStoryState.MeetingNodeId);
                    ui.OpenDistrictStory();
                    Find("DistrictChoice_accept").onClick.Invoke();
                    Assert.AreEqual("community.meeting",s.CurrentDistrictNode.Id);
                    Assert.AreEqual(beforeReply,s.PlayerPosition,"Replying arranges the meeting; it does not move the player");
                    Assert.IsNull(s.DistrictStoryState.MeetingNodeId,"Replying must not book Tang Wei automatically");
                    Assert.AreEqual("keeper_agent",s.DistrictMeetingContact);
                    Assert.IsNull(s.DistrictPersonObjective(CityLifeCatalog.TangWei));
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictGoToMeeting"));
                    var agent=s.CityPersonState("keeper_agent");
                    agent.Route=null;agent.LocationPlaceId="agency";
                    var anchor=s.City.ViewPosition(s.DistrictMeetingStreetNode);agent.X=anchor.X;agent.Y=anchor.Y;
                    s.Leave();s.ArriveAtNode(s.DistrictMeetingStreetNode);
                    typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,(PCShopSell.Core.World.MapPoint?)anchor);
                    ui.CloseAllWindows();ui.OpenPersonDialogue("keeper_agent");
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>(b.GetComponentInChildren<Text>()?.text??"").StartsWith("主线 · ")));
                    var arrange=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>(b.GetComponentInChildren<Text>()?.text??"")=="请帮我约唐蔚出来见面");
                    Assert.True(arrange.interactable);arrange.onClick.Invoke();
                    Assert.AreEqual(CityLifeCatalog.TangWei,s.DistrictMeetingContact);
                    Assert.IsNull(s.DistrictPersonObjective("keeper_agent"));
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictCompleteNode"));
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictGoToMeeting"),"Already beside the intermediary, do not repeat navigation");
                    Assert.NotNull(s.DistrictPersonObjective(CityLifeCatalog.TangWei));
                    Assert.Zero(s.DistrictAccessTier);
                    var meetingNode=s.DistrictMeetingStreetNode;
                    s.Leave();s.ArriveAtNode(meetingNode);
                    typeof(GameSession).GetField("_free",BindingFlags.Instance|BindingFlags.NonPublic)
                        .SetValue(s,(PCShopSell.Core.World.MapPoint?)s.City.ViewPosition(meetingNode));
                    s.Player.Health=100;s.Player.Hunger=100;s.Player.Energy=100;
                    s.AdvanceClock((int)(s.DistrictStoryState.MeetingAt-s.Clock.Now.TotalMinutes)+1);
                    Assert.True(s.AtCurrentDistrictMeeting);
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictGoToMeeting"));
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictCompleteNode"));
                    ui.CloseAllWindows();ui.OpenPersonDialogue("keeper_agent");
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>(b.GetComponentInChildren<Text>()?.text??"").Contains("接受寻物委托")));
                    ui.CloseAllWindows();ui.OpenPersonDialogue(CityLifeCatalog.TangWei);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>(b.GetComponentInChildren<Text>()?.text??"").StartsWith("主线 · ")));
                    var accept=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>(b.GetComponentInChildren<Text>()?.text??"")=="答应帮忙找回三页图，接受寻物委托");
                    Assert.True(accept.interactable);accept.onClick.Invoke();
                    Assert.AreEqual("community.clue",s.CurrentDistrictNode.Id);
                    Assert.False(s.AtCurrentDistrictMeeting);
                    Assert.False(s.ActDistrictNode("community.meeting"),"The accepted commission cannot replay");
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictAcceptMeeting"||b.name=="DistrictCompleteNode"||b.name=="DistrictGoToMeeting"));
                    if(!s.CitySamePlace("landlady"))
                    {
                        var beforeNavigation=JsonUtility.ToJson(s.DistrictStoryState);
                        Assert.True(Find("DistrictFindPerson_landlady").interactable);
                        Find("DistrictFindPerson_landlady").onClick.Invoke();
                        Assert.AreEqual(beforeNavigation,JsonUtility.ToJson(s.DistrictStoryState),"Navigation must not book or finish a conversation");
                    }
                    DistrictProgressionTests.StandWithLead(s,0);
                    ui.CloseAllWindows();ui.OpenPersonDialogue("landlady");
                    var book=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>(b.GetComponentInChildren<Text>()?.text??"").StartsWith("商量主线会面："));
                    Assert.True(book.interactable);book.onClick.Invoke();
                    Assert.AreEqual("community.clue",s.DistrictStoryState.MeetingNodeId);
                    // Seed an in-progress search to verify the actual cheat button and saved evidence.
                    var state=(DistrictProgress)typeof(GameSession).GetField("_district",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
                    state.NodeId="community.search";state.CommunityMapQuest=true;state.CommunitySearchVersion=2;
                    state.CommunityClues=1;state.CommunityPieces=1;
                    s.Meet("lin",65);var now=s.Clock.Now;var money=s.Ledger.Balance;int activities=s.CityActivities.Count();
                    ui.CloseAllWindows();typeof(GameUi).GetMethod("OpenCheatPanel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    Assert.AreEqual("主线 · 打听图页",s.DistrictPersonObjective("lin"));
                    Assert.IsNull(s.DistrictPersonObjective("landlady"),"Already obtained clue removes marker");
                    Assert.IsNull(s.DistrictPersonObjective("irrelevant"));
                    Assert.True(Find("CheatCommunityConditions").interactable);Find("CheatCommunityConditions").onClick.Invoke();
                    Find("CheatCommunityConditions").onClick.Invoke();
                    Assert.AreEqual(65,s.Relation("lin"));Assert.AreEqual(now,s.Clock.Now);Assert.AreEqual(money,s.Ledger.Balance);
                    Assert.AreEqual(activities,s.CityActivities.Count(),"Cheat evidence must not fabricate real activity history");
                    Assert.AreEqual(1,state.CommunityClues);Assert.AreEqual(1,state.CommunityPieces);Assert.False(state.HasCommunityMap);Assert.Zero(s.DistrictAccessTier);
                    Assert.AreEqual("community.search",s.CurrentDistrictNode.Id);
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    string StoryText()=>string.Join("\n",ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Select(t=>t.text));
                    StringAssert.DoesNotContain(GameSession.CommunityPieceName(0),StoryText());
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictFindPerson_landlady"));
                    StringAssert.DoesNotContain("邀请：",StoryText());
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>(b.GetComponentInChildren<Text>()?.text??"").Contains("找他聊聊")||(b.GetComponentInChildren<Text>()?.text??"").StartsWith("邀请：")));
                    StringAssert.Contains("向本人打听图页线索",StoryText());
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="CommunityAssembleMap"));
                    using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    {
                        for(int i=0;i<3;i++){Assert.GreaterOrEqual(loaded.Relation(GameSession.CommunityLeadPerson(i)),30);Assert.True(loaded.CommunityHasOuting(i));Assert.IsNull(loaded.RemoteConversationBlocker(GameSession.CommunityLeadPerson(i)));}
                        Assert.False(loaded.CommunityHasClue(1));loaded.AskCommunityLead(1,true);Assert.False(loaded.CommunityHasClue(1),"Remote chat cannot unlock a clue even with all conditions met");DistrictProgressionTests.AskLeadLocally(loaded,1);Assert.True(loaded.CommunityHasClue(1));Assert.IsNull(loaded.DistrictPersonObjective("lin"));
                        Assert.True(loaded.CommunityBoxAvailable(1));Assert.False(loaded.CommunityHasPiece(1));Assert.Zero(loaded.DistrictAccessTier);
                    }
                    state.CommunityClues=3;
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictFindPerson_lin"));
                    StringAssert.Contains("前往藏箱地点",StoryText());
                    state.CommunityOutings&=~4;
                    DistrictProgressionTests.StandWithLead(s,2);
                    ui.CloseAllWindows();ui.OpenCityActivityChooser("zhou");
                    StringAssert.Contains("市场走访",Find("CityActivityOffer_market").GetComponentInChildren<Text>().text);
                    StringAssert.Contains("旧物闲逛",Find("CityActivityOffer_old_market").GetComponentInChildren<Text>().text);
                    var booking=s.ScheduleCityActivity("market",new[]{"zhou"},s.SuggestCityActivityStart("market","zhou"));
                    Assert.NotNull(booking);
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.True(Find("DistrictGoToActivity_zhou").interactable,"A same-venue appointment still needs a route; preferred service is chosen on arrival");
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="DistrictFindPerson_zhou"));
                    state.CommunityClues=7;state.CommunityPieces=7;
                    ui.CloseAllWindows();ui.OpenDistrictStory();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="CommunityAssembleMap"));
                    ui.CloseAllWindows();ui.OpenBackpackPanel();
                    for(int i=0;i<3;i++)Assert.True(Find("BagItem_quest-page:"+i).interactable);
                    Find("BagItem_quest-page:2").onClick.Invoke();
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains(GameSession.CommunityPieceName(2))));
                    Assert.AreEqual("community.trial",s.CurrentDistrictNode.Id);
                    StringAssert.DoesNotContain("找他聊聊",StoryText());
                    StringAssert.DoesNotContain("前往藏箱地点",StoryText());

                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)
                        UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
