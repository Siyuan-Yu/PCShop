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
