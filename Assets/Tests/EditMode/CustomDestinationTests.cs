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
        private static T Control<T>(GameUi ui, string name) where T : Component =>
            ui.Canvases.SelectMany(c => c.GetComponentsInChildren<T>()).Single(control => control.name == name);

        [Test]
        public void SavingUpdatesTheShortcutWithoutTravelAndPersistsAcrossReload()
        {
            using(var s=GameSessionTests.Make())
            {
                WithUi(s,ui=>
                {
                    Assert.AreEqual("选择目的地",Caption(ui));
                    var now=s.Clock.Now;var balance=s.Ledger.Balance;var position=s.PlayerPosition;
                    Button(ui,"ShortcutGo_3").onClick.Invoke();
                    Button(ui,"DestinationRow_dealer").onClick.Invoke();
                    Assert.IsNull(s.CityDefaultDestination);
                    Button(ui,"DestinationSave").onClick.Invoke();
                    Assert.AreEqual("dealer",s.CityDefaultDestination);
                    Assert.AreEqual("前往 " + s.City.GetPlace("dealer").Name,Caption(ui));
                    Assert.False(Button(ui,"DestinationSave").interactable);
                    Assert.True(ui.AnyModalOpen);Assert.IsNull(s.CityIntent);Assert.IsNull(s.Trip);
                    Assert.AreEqual(now,s.Clock.Now);Assert.AreEqual(balance,s.Ledger.Balance);Assert.AreEqual(position,s.PlayerPosition);
                    Button(ui,"DestinationRow_tea").onClick.Invoke();
                    ui.CloseAllWindows();
                    Assert.AreEqual("dealer",s.CityDefaultDestination,"关闭窗口保留上一次明确保存的地点");
                });
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,save))WithUi(loaded,ui=>
                {
                    Assert.AreEqual("dealer",loaded.CityDefaultDestination);
                    Assert.AreEqual("前往 " + loaded.City.GetPlace("dealer").Name,Caption(ui));
                    Button(ui,"ShortcutGo_3").onClick.Invoke();
                    Assert.AreEqual("dealer",loaded.CityIntent?.TargetPlaceId);
                    Assert.NotNull(loaded.Trip);
                });
            }
        }

        [Test]
        public void GoingNowUsesTheSelectedDestinationAndLeavesTheShortcutAlone()
        {
            using var s = GameSessionTests.Make();
            s.SetCityDefault("dealer");
            Assert.True(s.SetVehicle(VehicleKind.Taxi));
            var target = s.City.GetPlace("tea");
            Assert.IsNull(s.CityDestinationTravelBlocker(target, out int minutes, out var fare));
            var balance = s.Ledger.Balance;
            WithUi(s, ui =>
            {
                Button(ui, "ShortcutArrow_3").onClick.Invoke();
                Button(ui, "DestinationRow_tea").onClick.Invoke();
                StringAssert.Contains($"约 {minutes} 分钟", Control<Text>(ui, "DestinationTravel").text);
                StringAssert.Contains(fare.ToString(), Control<Text>(ui, "DestinationTravel").text);
                Assert.AreEqual(balance, s.Ledger.Balance);
                Button(ui, "DestinationGo").onClick.Invoke();
                Assert.AreEqual("tea", s.CityIntent?.TargetPlaceId);
                Assert.NotNull(s.Trip);
                Assert.AreEqual("dealer", s.CityDefaultDestination);
                Assert.AreEqual(balance - fare, s.Ledger.Balance);
                Assert.False(ui.AnyModalOpen);
                Button(ui, "ShortcutArrow_3").onClick.Invoke();
                var list = Control<RectTransform>(ui, "DestinationResults");
                Assert.AreEqual("最近前往", list.GetChild(0).GetComponent<Text>().text);
                Assert.AreEqual("DestinationRow_tea", list.GetComponentsInChildren<Button>().First().name);
            });
        }

        [Test]
        public void SearchCombinesKeywordsTypeAndStreetAndClearsAnEmptyResult()
        {
            using var s = DistrictProgressionTests.Make();
            var bank = s.BankPlace;
            WithUi(s, ui =>
            {
                Button(ui, "ShortcutArrow_3").onClick.Invoke();
                var search = Control<InputField>(ui, "DestinationSearch");
                var type = Control<Dropdown>(ui, "DestinationType");
                var street = Control<Dropdown>(ui, "DestinationStreet");
                street.value = street.options.FindIndex(option => option.text == s.City.StreetName(bank.StreetId));
                type.value = type.options.FindIndex(option => option.text == "公共服务");
                search.text = "还款 " + bank.Name;
                var list = Control<RectTransform>(ui, "DestinationResults");
                CollectionAssert.AreEqual(new[] { "DestinationRow_" + bank.Id },
                    list.GetComponentsInChildren<Button>().Select(b => b.name));
                Assert.AreEqual(bank.Name, Control<Text>(ui, "DestinationName").text);
                search.text = "还款 找不到";
                Assert.False(Button(ui, "DestinationGo").interactable);
                Assert.False(Button(ui, "DestinationSave").interactable);
                Button(ui, "DestinationClearFilters").onClick.Invoke();
                Assert.AreEqual("", search.text);
                Assert.AreEqual(0, type.value);
                Assert.AreEqual(0, street.value);
                Assert.That(list.GetComponentsInChildren<Button>().Length, Is.GreaterThan(1));
                Assert.AreSame(search, Control<InputField>(ui, "DestinationSearch"));
                Assert.IsNull(s.CityDefaultDestination);
            });
        }
    }
}
