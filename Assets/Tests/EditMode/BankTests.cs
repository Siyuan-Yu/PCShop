using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    /// <summary>城郊银行：存款月息 0.3%，柜台借款和手机借款同一套接口。</summary>
    public class BankTests
    {
        private static GameSession Make(bool phone)
        {
            CityLayoutData data = GameSessionTests.Layout();
            data.places = data.places.Concat(new[]
            {
                new LayoutPlaceData { id = GameSession.BankPlaceId, name = "城郊银行", kind = "shop", node = "dl", size = "small", business = GameSession.BankBusiness,
                    explicitEntrance = true, openMinute = 0, closeMinute = 1440 },
            }).ToArray();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 10 * 60), true, true, false, false);
            s.SetSmartphone(phone);
            return s;
        }
        /// <summary>AdvanceClock 遇到昏倒等打断会提前返回，所以循环推到目标时刻。</summary>
        private static void Days(GameSession s, int days)
        {
            long target = s.Clock.Now.TotalMinutes + days * 1440L;
            for (int i = 0; i < 100000 && s.Clock.Now.TotalMinutes < target; i++) s.AdvanceClock((int)System.Math.Min(1440, target - s.Clock.Now.TotalMinutes));
        }
        private static void GoToBank(GameSession s)
        {
            s.Leave(); var bank = s.City.GetPlace(GameSession.BankPlaceId);
            s.ArriveAtNode(bank.NodeId); Assert.True(s.Enter(bank));
        }

        [Test]
        public void Deposit_RequiresBankCounter_AndEarnsPointThreePercentPerMonth()
        {
            using var s = Make(true);
            Assert.False(s.BankDepositMoney(1000), "不在银行不能存");
            GoToBank(s);
            Money cash = s.Ledger.Balance, netStart = s.NetWorth();
            Assert.True(s.BankDepositMoney(10000));
            Assert.AreEqual(cash - Money.FromYuan(10000), s.Ledger.Balance);
            Assert.AreEqual(Money.FromYuan(10000), s.BankDeposit);
            Assert.AreEqual(netStart, s.NetWorth(), "存进银行只是换个地方放钱，净资产不变");
            Days(s, 30);
            Assert.AreEqual(1003000, s.BankDeposit.Cents, 5, "30 天后正好 +0.3%");
            Assert.AreEqual(s.BankDeposit - Money.FromYuan(10000), s.BankInterestEarned);
        }

        [Test]
        public void Withdraw_ReturnsCash_AndRejectsOverdraw()
        {
            using var s = Make(true);
            GoToBank(s);
            Assert.True(s.BankDepositMoney(500));
            Assert.False(s.BankWithdrawMoney(501));
            Assert.True(s.BankWithdrawMoney(200));
            Assert.AreEqual(Money.FromYuan(300), s.BankDeposit);
            Assert.True(s.BankWithdrawAll());
            Assert.AreEqual(Money.Zero, s.BankDeposit);
            Assert.False(s.BankWithdrawAll());
        }

        [Test]
        public void Deposit_SurvivesSaveAndLoad()
        {
            using var s = Make(true);
            GoToBank(s);
            Assert.True(s.BankDepositMoney(2000));
            Days(s, 3);
            Money deposit = s.BankDeposit, interest = s.BankInterestEarned;
            using var again = GameSession.Load(s.City, UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(deposit, again.BankDeposit);
            Assert.AreEqual(interest, again.BankInterestEarned);
        }

        [Test]
        public void BankLoan_WorksWithoutSmartphone_AndSharesTheSameTerms()
        {
            using var s = Make(false);
            Assert.False(s.BorrowAppLoan(1000, 3), "没手机、不在银行借不了");
            GoToBank(s);
            Assert.False(s.BorrowAppLoan(1000, 3), "没手机不能走手机渠道");
            Money cash = s.Ledger.Balance;
            Assert.True(s.BorrowAppLoan(1000, 3, true));
            var loan = s.AppLoans.Single();
            Assert.IsTrue(loan.AtBank);
            Assert.AreEqual(cash + Money.FromYuan(1000), s.Ledger.Balance);
            Assert.AreEqual(Money.FromYuan(1000) + Money.FromYuan(200) * 3, loan.TotalDue, "月息 20%，和手机借款一样");
            Assert.True(s.RepayAppLoan(loan.Id));
            Assert.AreEqual(0, s.AppLoans.Count(l => !l.Repaid));
        }

        [Test]
        public void ReputationSetsASharedCreditLineAndRepaymentFreesPrincipal()
        {
            using var s = Make(true);
            GoToBank(s);
            s.PlayerShop.Reputation = 0;
            Assert.AreEqual(50000, s.AppLoanAvailableYuan);
            Assert.False(s.BorrowAppLoan(50100, 3, true));
            Assert.True(s.BorrowAppLoan(40000, 3, true));
            Assert.False(s.BorrowAppLoan(10100, 3));
            Assert.True(s.BorrowAppLoan(10000, 3));
            Assert.AreEqual(0, s.AppLoanAvailableYuan);
            s.PlayerShop.Reputation = 2.5;
            Assert.AreEqual(60000, s.AppLoanCreditLimitYuan);
            Assert.AreEqual(10000, s.AppLoanAvailableYuan);
            Assert.True(s.RepayAppLoan(s.AppLoans.First().Id));
            Assert.AreEqual(50000, s.AppLoanAvailableYuan);
            s.PlayerShop.Reputation = 1000;
            Assert.AreEqual(500000, s.AppLoanCreditLimitYuan);
        }

        [TestCase(0, 1)]
        [TestCase(30, 1)]
        [TestCase(31, 2)]
        [TestCase(60, 2)]
        [TestCase(61, 3)]
        public void EarlyRepaymentDebitsOnlyTheMonthsUsed(int elapsedDays, int billedMonths)
        {
            using var s = Make(false);
            GoToBank(s);
            Assert.True(s.BorrowAppLoan(50000, 3, true));
            var loan = s.AppLoans.Single();
            s.Clock.Restore(s.Clock.Today.AddDays(elapsedDays), s.Clock.MinuteOfDay);
            var cash = s.Ledger.Balance;
            var interest = Money.FromYuan(10000 * billedMonths);
            Assert.AreEqual(Money.FromYuan(80000), loan.TotalDue);
            Assert.AreEqual(loan.Principal + interest, s.LoanPayoff(loan));
            Assert.True(s.RepayAppLoan(loan.Id));
            Assert.AreEqual(interest.Cents, loan.PaidInterestCents);
            Assert.AreEqual(loan.Principal + interest, cash - s.Ledger.Balance);
        }

        [Test]
        public void ExistingLoanAboveNewCeilingLoadsAndConsumesCredit()
        {
            using var s = Make(false);
            var save = s.CreateSave();
            save.appLoans = new[] { new AppLoan { Id = 1, PrincipalCents = 60000000, Months = 3 } };
            using var loaded = GameSession.Load(s.City, save);
            Assert.AreEqual(Money.FromYuan(600000), loaded.AppLoans.Single().Principal);
            Assert.AreEqual(0, loaded.AppLoanAvailableYuan);
        }

        [Test]
        public void ReminderWithoutPhoneRoutesToBankAndShowsTodaysPayoffOnArrival()
        {
            using var s = Make(false);
            s.EventsEnabled = false;
            GoToBank(s);
            Assert.True(s.BorrowAppLoan(50000, 3, true));
            s.Leave();
            GameSessionTests.Teleport(s, s.Home.Id);
            var root = new GameObject("BankReminderTest");
            root.SetActive(false);
            var controller = root.AddComponent<GameController>();
            typeof(GameController).GetProperty("Session").SetValue(controller, s);
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var ui = new GameUi(controller);
            typeof(GameController).GetProperty("Ui").SetValue(controller, ui);
            s.CityPlaceEntered += ui.OnEnteredPlace;
            try
            {
                var reminder = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>(true))
                    .Single(button => button.name == "LoanReminderHud");
                reminder.onClick.Invoke();
                Assert.AreEqual(GameSession.BankPlaceId, s.CityIntent?.TargetPlaceId);
                GameSessionTests.RunTrip(s);
                Assert.IsNull(s.Trip);
                s.AdvanceClock(1); // Enter-on-arrival is processed on the city clock tick.
                Assert.True(s.AtBank);
                var texts = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>()).Select(text => text.text);
                Assert.True(texts.Any(text => text.Contains("今日结清 ¥60,000")));
                Assert.True(texts.Any(text => text.Contains("到期应还 ¥80,000")));
            }
            finally
            {
                s.CityPlaceEntered -= ui.OnEnteredPlace;
                ui.CloseAllWindows();
                foreach (var canvas in ui.Canvases) Object.DestroyImmediate(canvas.gameObject);
                Object.DestroyImmediate(root);
                if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                    Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
            }
        }

        [Test]
        public void ExistingResidentWorksAtBankInNewAndOlderSaves()
        {
            using var s = Make(false);
            GoToBank(s);
            string id = CityLifeCatalog.BankTellerId;
            Assert.AreEqual("张淑琴", s.CityPerson(id).Name);
            Assert.AreEqual(GameSession.BankPlaceId, s.CityPersonState(id).WorkPlaceId);
            Assert.True(s.CitySamePlace(id));
            s.Meet(id, 22);

            var save = s.CreateSave();
            var oldState = save.cityPeople.Single(p => p.PersonId == id);
            oldState.WorkPlaceId = null;
            oldState.LocationPlaceId = oldState.HomePlaceId;
            oldState.Task = CityPersonTask.Home;
            using var loaded = GameSession.Load(s.City, save);
            Assert.True(loaded.CitySamePlace(id));
            Assert.AreEqual("银行柜员", loaded.CityPersonRole(id));
            Assert.AreEqual(22, loaded.Relation(id));
        }

        [Test]
        public void BankLoan_AvailableAtNightOnWeekends()
        {
            using var s = Make(false);
            s.Clock.Restore(new GameDate(6), 23 * 60);
            GoToBank(s);
            Assert.True(s.IsPlaceOpen(s.CurrentPlace));
            Assert.True(s.BorrowAppLoan(1000, 1, true));
        }

        [Test]
        public void DueLoan_PullsFromDepositWhenCashShort()
        {
            using var s = Make(true);
            GoToBank(s);
            Assert.True(s.BorrowAppLoan(1000, 1, true));
            Assert.True(s.BankDepositMoney(s.Ledger.Balance.Cents / 100 - 100));
            Money deposit = s.BankDeposit;
            Assert.IsTrue(s.Ledger.Balance < Money.FromYuan(1200));
            Days(s, 31);
            Assert.AreEqual(0, s.AppLoans.Count(l => !l.Repaid));
            Assert.IsTrue(s.BankDeposit < deposit);
        }
    }
}
