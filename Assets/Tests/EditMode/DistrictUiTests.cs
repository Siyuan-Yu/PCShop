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
                    DistrictProgressionTests.AwaitInvitation(s);ui.CloseAllWindows();ui.OpenChatPanel();Find("DistrictStoryMessage").onClick.Invoke();
                    Find("DistrictChoice_accept").onClick.Invoke();
                    Assert.AreEqual("community.meeting",s.CurrentDistrictNode.Id);
                    Assert.False(Find("DistrictCompleteNode").interactable);
                    Assert.True(Find("DistrictGoToMeeting").interactable);
                    Assert.NotNull(s.DistrictPersonObjective("keeper_agent"));
                    Assert.Zero(s.DistrictAccessTier);
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
                    using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    {
                        for(int i=0;i<3;i++){Assert.GreaterOrEqual(loaded.Relation(GameSession.CommunityLeadPerson(i)),30);Assert.True(loaded.CommunityHasOuting(i));Assert.IsNull(loaded.RemoteConversationBlocker(GameSession.CommunityLeadPerson(i)));}
                        Assert.False(loaded.CommunityHasClue(1));loaded.AskCommunityLead(1,true);Assert.True(loaded.CommunityHasClue(1));Assert.IsNull(loaded.DistrictPersonObjective("lin"));
                        Assert.True(loaded.CommunityBoxAvailable(1));Assert.False(loaded.CommunityHasPiece(1));Assert.Zero(loaded.DistrictAccessTier);
                    }

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
