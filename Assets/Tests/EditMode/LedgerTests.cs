using System;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public class LedgerTests
    {
        private static readonly GameDate D1 = GameDate.FromYmd(1, 1, 1);
        private static readonly GameDate D2 = GameDate.FromYmd(1, 1, 2);
        private static readonly GameDate D3 = GameDate.FromYmd(1, 1, 3);

        [Test]
        public void Credit_IncreasesBalance_AndRecordsEntry()
        {
            var ledger = new Ledger(Money.FromYuan(1000));
            ledger.Credit(D1, "sales", Money.FromYuan(500), "卖出一台机器");

            Assert.AreEqual(Money.FromYuan(1500), ledger.Balance);
            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.AreEqual(Money.FromYuan(500), ledger.Entries[0].Amount);
            Assert.AreEqual(Money.FromYuan(1500), ledger.Entries[0].BalanceAfter);
        }

        [Test]
        public void TryDebit_SucceedsWhenAffordable()
        {
            var ledger = new Ledger(Money.FromYuan(1000));
            Assert.IsTrue(ledger.TryDebit(D1, "purchase", Money.FromYuan(300)));
            Assert.AreEqual(Money.FromYuan(700), ledger.Balance);
            Assert.AreEqual(Money.FromYuan(-300), ledger.Entries[0].Amount);
        }

        [Test]
        public void TryDebit_FailsWhenInsufficient_AndDoesNotRecord()
        {
            var ledger = new Ledger(Money.FromYuan(100));
            Assert.IsFalse(ledger.TryDebit(D1, "purchase", Money.FromYuan(101)));
            Assert.AreEqual(Money.FromYuan(100), ledger.Balance);
            Assert.AreEqual(0, ledger.Entries.Count);
        }

        [Test]
        public void ForceDebit_CanGoNegative()
        {
            var ledger = new Ledger(Money.FromYuan(100));
            ledger.ForceDebit(D1, "rent", Money.FromYuan(300));
            Assert.AreEqual(Money.FromYuan(-200), ledger.Balance);
            Assert.IsTrue(ledger.Balance.IsNegative);
        }

        [Test]
        public void CanAfford_ChecksBalance()
        {
            var ledger = new Ledger(Money.FromYuan(100));
            Assert.IsTrue(ledger.CanAfford(Money.FromYuan(100)));
            Assert.IsFalse(ledger.CanAfford(Money.FromYuan(101)));
        }

        [Test]
        public void NetBetween_And_SumCategory_RespectDateRange()
        {
            var ledger = new Ledger(Money.Zero);
            ledger.Credit(D1, "sales", Money.FromYuan(1000));
            ledger.ForceDebit(D2, "rent", Money.FromYuan(200));
            ledger.Credit(D3, "sales", Money.FromYuan(400));

            Assert.AreEqual(Money.FromYuan(800), ledger.NetBetween(D1, D2));
            Assert.AreEqual(Money.FromYuan(1400), ledger.SumCategory("sales", D1, D3));
            Assert.AreEqual(Money.FromYuan(-200), ledger.SumCategory("rent", D1, D3));
            Assert.AreEqual(Money.Zero, ledger.SumCategory("sales", D2, D2));
        }

        [Test]
        public void InvalidAmounts_Throw()
        {
            var ledger = new Ledger(Money.FromYuan(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Credit(D1, "x", Money.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.TryDebit(D1, "x", Money.FromYuan(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.ForceDebit(D1, "x", Money.Zero));
        }

        [Test]
        public void EmptyCategory_Throws()
        {
            var ledger = new Ledger(Money.FromYuan(100));
            Assert.Throws<ArgumentException>(() => ledger.Credit(D1, "", Money.FromYuan(1)));
        }
    }
}
