using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class BrandStoryUiTests
    {
        [Test] public void FullNameProfileKeepsAliasAndLongDialogueCanScrollToItsEnding()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                var previous = UnityEngine.EventSystems.EventSystem.current;
                var root = new GameObject("BrandStoryUiTest"); root.SetActive(false);
                var controller = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller, s);
                typeof(GameController).GetProperty("City").SetValue(controller, s.City);
                var ui = new GameUi(controller);
                try
                {
                    ui.OpenCityPersonProfile("keeper_agent");
                    var profile = string.Join("\n", ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Select(t => t.text));
                    StringAssert.Contains("陈启明", profile); StringAssert.Contains("熟人称呼：小陈", profile);
                    ui.CloseAllWindows();
                    var p = s.CityPersonState("momo"); p.Route = null; p.LocationPlaceId = "life_studio";
                    typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace("life_studio"));
                    string text = string.Concat(Enumerable.Repeat("这次合作留下了真实记录。", 50)) + "结尾仍然可以读到。";
                    ui.OpenPersonDialogue("momo", firstReply: text);
                    Canvas.ForceUpdateCanvases();
                    var scroll = ui.Canvases.SelectMany(c => c.GetComponentsInChildren<ScrollRect>()).Single(x => x.name == "DialogueTextScroll");
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
                    Assert.AreEqual(text, scroll.content.GetComponentInChildren<Text>().text);
                    Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                    scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01));
                    Assert.IsEmpty(s.BrandStories, "Reading dialogue must not accept or finish a story");
                    Button Find(string label) => ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>())
                        .Single(b=>b.GetComponentInChildren<Text>()?.text==label);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="随便聊聊"),
                        "A reply replaces the topic list instead of hiding in its greeting bubble");
                    Assert.Greater(((RectTransform)scroll.transform).rect.height,300);
                    Find("返回上一级").onClick.Invoke();
                    Assert.NotNull(Find("随便聊聊"));
                    Find("随便聊聊").onClick.Invoke();
                    Assert.NotNull(Find("返回上一级"));
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="随便聊聊"));
                    var reply=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<ScrollRect>()).Single(x=>x.name=="DialogueTextScroll").content.GetComponentInChildren<Text>().text;
                    ui.RebuildWindows();
                    Assert.AreEqual(reply,ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<ScrollRect>()).Single(x=>x.name=="DialogueTextScroll").content.GetComponentInChildren<Text>().text,
                        "Refreshing the window must keep the reply open until explicitly returned");
                    Find("返回上一级").onClick.Invoke();
                    Assert.NotNull(Find("随便聊聊"));
                    ui.CloseAllWindows();
                    typeof(GameSession).GetField("_district",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(s,
                        new DistrictProgress{Chapter=1,NodeId="community.search",CommunityMapQuest=true,CommunitySearchVersion=2});
                    DistrictProgressionTests.CompleteLeadOuting(s,0);
                    var outing=s.CityActivities.Last(a=>a.State==CityActivityPhase.Completed);
                    Assert.True(s.CommunityHasClue(0));
                    ui.OpenCityActivitySettlement(outing.Id);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="记下线索"));
                    Find("收好这段回忆").onClick.Invoke();
                    Assert.NotNull(Find("记下线索"));
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains(GameSession.CommunityLeadHint(0))));
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text=="返回上一级"));
                    Find("记下线索").onClick.Invoke();
                    ui.OpenCityActivitySettlement(outing.Id);Find("收好这段回忆").onClick.Invoke();
                    Assert.False(ui.AnyModalOpen,"Reopening settlement must not repeat the clue dialogue");
                }
                finally
                {
                    ui.CloseAllWindows(); foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if (previous == null && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
