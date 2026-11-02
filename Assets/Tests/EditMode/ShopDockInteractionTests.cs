using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class ShopDockInteractionTests
    {
        private static void WithUi(Action<GameSession,GameUi> action)
        {
            using(var s=GameSessionTests.Make())
            {
                GameSessionTests.Teleport(s,"pc");
                var root=new GameObject("ShopDockInteractionRegression");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(controller);
                try{action(s,ui);}
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        private static void Refresh(GameUi ui)=>typeof(GameUi).GetMethod("RefreshShopDockVisibility",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
        private static Button[] Buttons(GameUi ui)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).ToArray();
        [Test]
        public void DockRemainsRecoverableAcrossPauseDialogsAndExplicitCollapse()
        {
            WithUi((s,ui)=>
            {
                ui.OpenDock();Refresh(ui);
                var toggle=Buttons(ui).Single(b=>b.name=="ShopDockToggle");
                Assert.True(toggle.gameObject.activeSelf);Assert.True(ui.DockOpen);
                ui.DebugSelectTab(1);s.Time.SetPaused(true);Refresh(ui);
                Assert.True(ui.DockOpen);Assert.True(s.Time.IsUserPaused);
                ui.OpenWindow("临时操作",400,300,w=>{});Assert.True(s.Time.IsHeld);
                ui.CloseAllWindows();Refresh(ui);Assert.True(ui.DockOpen,"关闭其他窗口后店铺面板应恢复");
                Assert.True(s.Time.IsUserPaused,"恢复侧栏不能改变玩家暂停状态");
                toggle.onClick.Invoke();Assert.False(ui.DockOpen);Refresh(ui);Assert.False(ui.DockOpen);
                s.Time.SetPaused(false);ui.OpenWindow("其他操作",400,300,w=>{});ui.CloseAllWindows();Refresh(ui);
                Assert.False(ui.DockOpen,"主动收起后不能被定时刷新或弹窗强行展开");
                toggle.onClick.Invoke();Assert.True(ui.DockOpen);Assert.False(s.Time.IsHeld);Assert.False(s.Time.IsUserPaused);
                Assert.False(Buttons(ui).Any(b=>b.name=="ShopMainCategory"),"重新展开应保留库存页");
                s.Leave();Refresh(ui);Assert.False(ui.DockOpen);Assert.False(toggle.gameObject.activeSelf);
                GameSessionTests.Teleport(s,"pc");Refresh(ui);Assert.True(ui.DockOpen);Assert.True(toggle.gameObject.activeSelf);
            });
        }
        [Test]
        public void OnlyCounterSelectsMainCategoryAndUpgradeTabIsRemoved()
        {
            WithUi((s,ui)=>
            {
                ui.OpenDock();Refresh(ui);Assert.AreEqual(6,ui.DebugTabCount);
                var dock=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopDock");
                var labels=dock.Find("Tabs").GetComponentsInChildren<Text>().Select(t=>t.text.Replace("› ","")).ToArray();
                CollectionAssert.AreEqual(new[]{"柜台","库存","定价","订单","装修","招人"},labels);
                for(int i=0;i<ui.DebugTabCount;i++)
                {
                    ui.DebugSelectTab(i);
                    Assert.AreEqual(i==0?1:0,Buttons(ui).Count(b=>b.name=="ShopMainCategory"));
                    Assert.False(Buttons(ui).Any(b=>b.GetComponentInChildren<Text>()?.text=="主售品类"));
                }
                ui.DebugSelectTab(0);
                Buttons(ui).Single(b=>b.name=="ShopMainCategory").onClick.Invoke();
                Assert.True(ui.AnyModalOpen);Assert.True(s.Time.IsHeld);
                ui.CloseAllWindows();Refresh(ui);Assert.True(ui.DockOpen);Assert.False(s.Time.IsHeld);
            });
        }
    }
}
