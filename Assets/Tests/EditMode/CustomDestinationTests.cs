using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class CustomDestinationTests
    {
        private static void WithUi(GameSession session,System.Action<GameUi> action)
        {
            var root=new GameObject("CustomDestinationRegression");root.SetActive(false);
            var controller=root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller,session);
            typeof(GameController).GetProperty("City").SetValue(controller,session.City);
            var existing=UnityEngine.EventSystems.EventSystem.current;
            var ui=new GameUi(controller);
            typeof(GameController).GetProperty("Ui").SetValue(controller,ui);
            try{action(ui);}
            finally
            {
                ui.CloseAllWindows();
                foreach(var canvas in ui.Canvases)Object.DestroyImmediate(canvas.gameObject);
                Object.DestroyImmediate(root);
                if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)
                    Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
            }
        }
        private static Button Button(GameUi ui,string name)=>ui.Canvases
            .SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name==name);
        private static string Caption(GameUi ui)=>Button(ui,"ShortcutGo_3").GetComponentInChildren<Text>().text;

        [Test]
        public void MapSelectionPinsWithoutTravelAndMainButtonTravelsAfterJsonReload()
        {
            using(var s=GameSessionTests.Make())
            {
                WithUi(s,ui=>
                {
                    Assert.AreEqual("自定目的地",Caption(ui));
                    var now=s.Clock.Now;var balance=s.Ledger.Balance;var position=s.PlayerPosition;
                    Button(ui,"ShortcutGo_3").onClick.Invoke();
                    Assert.AreEqual(s.City.Places.Count(),ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Count(b=>b.name.StartsWith("CityMap_")));
                    Button(ui,"CityMap_dealer").onClick.Invoke();
                    Assert.AreEqual("dealer",s.CityDefaultDestination);
                    Assert.AreEqual(s.City.GetPlace("dealer").Name,Caption(ui));
                    Assert.False(ui.AnyModalOpen);Assert.IsNull(s.CityIntent);Assert.IsNull(s.Trip);
                    Assert.AreEqual(now,s.Clock.Now);Assert.AreEqual(balance,s.Ledger.Balance);Assert.AreEqual(position,s.PlayerPosition);
                    Button(ui,"ShortcutArrow_3").onClick.Invoke();
                    Button(ui,"CityMapList_tea").onClick.Invoke();
                    Assert.AreEqual("tea",s.CityDefaultDestination);Assert.False(ui.AnyModalOpen);Assert.IsNull(s.CityIntent);
                    Button(ui,"ShortcutArrow_3").onClick.Invoke();ui.CloseAllWindows();
                    Assert.AreEqual("tea",s.CityDefaultDestination,"关闭选择窗口不应清除快捷目的地");
                });
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,save))WithUi(loaded,ui=>
                {
                    Assert.AreEqual("tea",loaded.CityDefaultDestination);
                    Assert.AreEqual(loaded.City.GetPlace("tea").Name,Caption(ui));
                    Button(ui,"ShortcutGo_3").onClick.Invoke();
                    Assert.AreEqual("tea",loaded.CityIntent?.TargetPlaceId);
                    Assert.NotNull(loaded.Trip);
                });
            }
        }
    }
}
