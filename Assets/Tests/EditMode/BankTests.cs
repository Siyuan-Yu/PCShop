using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

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
                    explicitEntrance = true, openMinute = 540, closeMinute = 1020 },
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
        public void BankLoan_ClosedOutsideHours()
        {
            using var s = Make(false);
            GoToBank(s);
            s.Clock.Restore(new GameDate(0), 20 * 60);
            Assert.False(s.IsPlaceOpen(s.CurrentPlace));
            Assert.False(s.BorrowAppLoan(1000, 1, true));
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
