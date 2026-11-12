using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
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
                    Button[] Buttons() => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).ToArray();
                    string ButtonText(Button button) => button.GetComponentInChildren<Text>()?.text ?? "";
                    Button Find(string label) => Buttons().Single(b => ButtonText(b) == label);
                    bool Has(string label) => Buttons().Any(b => ButtonText(b) == label);
                    Text Reply() => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Single(t => t.name == "DialogueReplyText");
                    Assert.IsTrue(Has("聊聊"));
                    Assert.IsFalse(Has("打个招呼"));
                    Assert.IsFalse(Has("返回上一级"));
                    Assert.Greater(((RectTransform)scroll.transform).rect.height,300);
                    RectTransform Area(string name) => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<RectTransform>()).Single(r => r.name == name);
                    Assert.Greater(Area("DialogueSpeech").rect.width, 3 * Area("DialogueIdentity").rect.width);
                    var chat = Find("聊聊").GetComponent<RectTransform>();
                    var end = Find("结束对话").GetComponent<RectTransform>();
                    Assert.AreSame(chat.parent, end.parent);
                    Assert.That(chat.rect.width, Is.EqualTo(end.rect.width).Within(.1f));
                    Assert.That(chat.rect.height, Is.EqualTo(end.rect.height).Within(.1f));
                    var knownBefore = p.KnownTopics.ToArray();
                    Find("聊聊").onClick.Invoke();
                    Assert.IsTrue(s.HasGreeted("momo"));
                    Assert.IsFalse(s.IsMet("momo"));
                    CollectionAssert.AreEqual(knownBefore, p.KnownTopics);
                    string introduction = Reply().text;
                    StringAssert.Contains("你好，我是", introduction);
                    Find("聊聊").onClick.Invoke();
                    var reply = Reply().text;
                    Assert.AreNotEqual(introduction, reply);
                    ui.RebuildWindows();
                    Assert.AreEqual(reply, Reply().text, "The latest reply survives a live refresh");
                    Assert.IsTrue(Has("聊聊"));
                    Assert.IsFalse(Has("返回上一级"));
                    s.AddRelation("momo", 9 - s.Relation("momo"));
                    ui.RebuildWindows();
                    Assert.IsFalse(Buttons().Single(b => ButtonText(b).StartsWith("要个联系方式")).interactable);
                    s.AddRelation("momo", 1);
                    ui.RebuildWindows();
                    Find("要个联系方式").onClick.Invoke();
                    Assert.IsTrue(s.IsMet("momo"));
                    Assert.IsFalse(Buttons().Any(b => ButtonText(b).Contains("联系方式")));
                    Assert.IsTrue(Has("邀请活动"));
                    StringAssert.Contains("通讯录", Reply().text);
                    s.AddRelation("momo", 60);
                    ui.RebuildWindows();
                    Assert.IsFalse(Has("表白"));
                    s.AddRelation("momo", 1);
                    ui.RebuildWindows();
                    Assert.IsTrue(Find("表白").interactable);
                    ui.CloseAllWindows();
                    ui.OpenPersonDialogue("momo");
                    Assert.IsFalse(Buttons().Any(b => ButtonText(b).Contains("联系方式")));
                    s.Random.State = Enumerable.Range(1, 100).Select(seed => (ulong)seed)
                        .First(seed => new GameRandom(seed).Chance(GameSession.ConfessionChance(71)));
                    Find("表白").onClick.Invoke();
                    Assert.IsTrue(s.IsPlayerPartner("momo"));
                    StringAssert.Contains("交往", Reply().text);
                    Assert.IsTrue(Has("聊聊心事"));
                    Canvas.ForceUpdateCanvases();
                    var optionRow = Find("聊聊").transform.parent;
                    var optionWidths = optionRow.GetComponentsInChildren<Button>().Select(b => b.GetComponent<RectTransform>().rect.width).ToArray();
                    Assert.That(optionWidths.Max() - optionWidths.Min(), Is.LessThan(.1f));
                    Find("谈谈分手").onClick.Invoke();
                    Find("确认分手").onClick.Invoke();
                    Assert.IsFalse(s.IsPlayerPartner("momo"));
                    Assert.IsFalse(Has("聊聊心事"));
                    Assert.IsTrue(Has("聊聊"));
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
