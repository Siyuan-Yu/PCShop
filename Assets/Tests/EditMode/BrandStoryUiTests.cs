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
        [Test]
        public void KolBadgeIdentifiesTheCreatorInContactsDialogueAndProfile()
        {
            using var s = DistrictProgressionTests.Make();
            s.Meet("life_singer", 21);
            s.Meet("life_bartender", 21);
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var root = new GameObject("KolIdentityTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller, s);
            typeof(GameController).GetProperty("City").SetValue(controller, s.City);
            var ui = new GameUi(controller);
            Text[] Labels() => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).ToArray();
            try
            {
                ui.OpenContactsPanel();
                var singer = Labels().Single(t => t.text.Contains("<b>温音"));
                Assert.That(singer.text, Does.Contain(">KOL<").And.Contain(s.CityPersonRole("life_singer")));
                Assert.That(Labels().Single(t => t.text.Contains("<b>夏澄")).text, Does.Not.Contain("KOL"));
                singer.transform.parent.GetComponentsInChildren<Button>()
                    .Single(b => b.GetComponentInChildren<Text>()?.text == "发消息").onClick.Invoke();
                var identity = Labels().Where(t => t.transform.parent.name == "DialogueIdentity").Select(t => t.text).ToArray();
                Assert.That(identity.Any(text => text.Contains("温音") && text.Contains(">KOL<")), Is.True);
                Assert.That(identity.Any(text => text.Contains(s.CityPersonRole("life_singer")) && text.Contains("电话")), Is.True);

                ui.OpenCityPersonProfile("life_singer");
                Assert.That(Labels().Any(t => t.transform.parent.name == "PersonDetails"
                    && t.text.Contains("温音") && t.text.Contains(">KOL<")), Is.True);
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
                    float readingHeight = Area("DialogueReadingPanel").rect.height;
                    void AssertFixedDialogueLayout()
                    {
                        Canvas.ForceUpdateCanvases();
                        var end = (RectTransform)Find("结束对话").transform;
                        var panel = (RectTransform)end.GetComponentInParent<WindowFit>().transform;
                        var bottomRight = panel.InverseTransformPoint(end.TransformPoint(new Vector3(end.rect.xMax, end.rect.yMin)));
                        Assert.That(panel.rect.width, Is.EqualTo(930f).Within(.1f));
                        Assert.That(panel.rect.height, Is.EqualTo(520f).Within(.1f));
                        Assert.That(Area("DialogueReadingPanel").rect.height, Is.EqualTo(readingHeight).Within(.1f));
                        Assert.That(panel.rect.xMax - bottomRight.x, Is.EqualTo(16f).Within(.1f));
                        Assert.That(bottomRight.y - panel.rect.yMin, Is.EqualTo(16f).Within(.1f));
                        Assert.That(end.rect.width, Is.EqualTo(146f).Within(.1f));
                        Assert.That(end.rect.height, Is.EqualTo(40f).Within(.1f));
                        Assert.AreEqual(Ui.WarningSurface, end.GetComponent<Image>().color);
                    }
                    AssertFixedDialogueLayout();
                    var knownBefore = p.KnownTopics.ToArray();
                    Find("聊聊").onClick.Invoke();
                    Assert.IsTrue(s.HasGreeted("momo"));
                    Assert.IsTrue(s.IsMet("momo"));
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
                    Assert.IsFalse(Buttons().Single(b => ButtonText(b).StartsWith("邀请活动")).interactable);
                    s.AddRelation("momo", 1);
                    ui.RebuildWindows();
                    Assert.IsFalse(Buttons().Any(b => ButtonText(b).Contains("联系方式")));
                    Assert.IsTrue(Find("邀请活动").interactable);
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
                    AssertFixedDialogueLayout();
                    Find("谈谈分手").onClick.Invoke();
                    AssertFixedDialogueLayout();
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
                    Find("继续交谈").onClick.Invoke();
                    var farewellWindow = Find("继续").GetComponentInParent<WindowFit>();
                    Assert.NotNull(farewellWindow);
                    AssertFixedDialogueLayout();
                    Find("继续").onClick.Invoke();
                    Assert.NotNull(Find("记下线索"));
                    Assert.AreSame(farewellWindow,Find("记下线索").GetComponentInParent<WindowFit>());
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
