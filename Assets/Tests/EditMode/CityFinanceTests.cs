using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class CityFinanceTests
    {
        [Test]
        public void PeriodCashSummaryClosesBeforeTheNewWeekAndReconcilesTransfers()
        {
            var ledger = new Ledger(Money.FromYuan(1000));
            var from = new GameDate(7);
            var to = new GameDate(13);
            ledger.Credit(new GameDate(6), "零售", Money.FromYuan(200));
            ledger.Credit(from, "打工", Money.FromYuan(150));
            ledger.ForceDebit(to, "工资", Money.FromYuan(80));
            ledger.ForceDebit(to, "企业周转金", Money.FromYuan(20));
            ledger.ForceDebit(new GameDate(14), "房租", Money.FromYuan(100));

            var report = FinanceReport.CashSummary(ledger, from, to);
            Assert.AreEqual(Money.FromYuan(1200), report.OpeningBalance);
            Assert.AreEqual(Money.FromYuan(150), report.Income);
            Assert.AreEqual(Money.FromYuan(80), report.Expense);
            Assert.AreEqual(Money.FromYuan(-20), report.InternalNet);
            Assert.AreEqual(Money.FromYuan(1250), report.ClosingBalance);
        }

        [Test]
        public void EconomicReportDefaultsToWeeklyAndKeepsRecordingWhenPopupIsDisabled()
        {
            using (var session = GameSessionTests.Make(200000))
            {
                session.EventsEnabled = false;
                session.Clock.Restore(new GameDate(6), 23 * 60 + 59);
                session.Clock.Advance(1);
                Assert.True(session.EconomicReportPending);

                session.SetEconomicReportCadence(null);
                session.Clock.Restore(new GameDate(13), 23 * 60 + 59);
                session.Clock.Advance(1);
                Assert.False(session.EconomicReportPending);
                Assert.AreEqual(14, session.PriceDays.Last().Day);
                using (var loaded = GameSession.Load(session.City, session.CreateSave()))
                {
                    Assert.False(loaded.EconomicReportAutomatic);
                    loaded.SetEconomicReportCadence(EconomicReportCadence.Biweekly);
                    loaded.Clock.Restore(new GameDate(20), 23 * 60 + 59);
                    loaded.Clock.Advance(1);
                    Assert.False(loaded.EconomicReportPending);
                    loaded.Clock.Restore(new GameDate(27), 23 * 60 + 59);
                    loaded.Clock.Advance(1);
                    Assert.True(loaded.EconomicReportPending);
                }
            }
        }

        [Test]
        public void OperatingCashExcludesLoanAndInternalTransfers()
        {
            var day = new GameDate(0); var ledger = new Ledger(Money.FromYuan(1000));
            ledger.Credit(day, "零售", Money.FromYuan(1000), attribution: LedgerAttribution.ForShop("shop-a"));
            ledger.TryDebit(day, "进货", Money.FromYuan(600), attribution: LedgerAttribution.ForShop("shop-a"));
            ledger.TryDebit(day, "工资", Money.FromYuan(100), attribution: LedgerAttribution.ForShop("shop-a"));
            ledger.Credit(day, "贷款到账", Money.FromYuan(5000));
            ledger.Credit(day, "企业资金转回", Money.FromYuan(2000));
            var combined = FinanceReport.CitySummary(ledger, day, day);
            var shop = FinanceReport.CitySummary(ledger, day, day, "shop-a");
            Assert.AreEqual(Money.FromYuan(300), combined.OperatingNet);
            Assert.AreEqual(Money.FromYuan(300), shop.OperatingNet);
            Assert.AreEqual(Money.FromYuan(5000), combined.FinancingNet);
            Assert.AreEqual(Money.FromYuan(2000), combined.InternalNet);
            Assert.AreEqual(5, ledger.Entries.Count, "Reading reports does not add transactions.");
        }

        [Test]
        public void ASettledSourceRemainsIdempotentAfterLedgerRestore()
        {
            var day = new GameDate(0); var ledger = new Ledger(Money.FromYuan(100));
            var source = LedgerAttribution.ForShop("shop-a", "wage:employee:0", "工资");
            Assert.IsTrue(ledger.TryDebit(day, "工资", Money.FromYuan(100), attribution: source));
            var saved = ledger.Entries.ToArray(); var balance = ledger.Balance;
            ledger.Restore(balance, saved);
            Assert.IsTrue(ledger.TryDebit(day, "工资", Money.FromYuan(100), attribution: source));
            Assert.AreEqual(balance, ledger.Balance);
            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.Throws<InvalidOperationException>(() => ledger.ForceDebit(day, "工资", Money.FromYuan(101), attribution: source));
        }

        [Test]
        public void FailedPaymentDoesNotClaimItsSource()
        {
            var ledger = new Ledger(); var day = new GameDate(0);
            var source = LedgerAttribution.ForShop("shop-a", "purchase:1");
            Assert.IsFalse(ledger.TryDebit(day, "进货", Money.FromYuan(10), attribution: source));
            Assert.IsFalse(ledger.HasSource("purchase:1"));
            ledger.Credit(day, "贷款到账", Money.FromYuan(10));
            Assert.IsTrue(ledger.TryDebit(day, "进货", Money.FromYuan(10), attribution: source));
        }

        [Test]
        public void LegacyMemoNeverInventsShopOwnership()
        {
            var ledger = new Ledger(); var day = new GameDate(0);
            ledger.Restore(Money.FromYuan(100), new[] { new LedgerEntry(day, "零售", Money.FromYuan(100), "我的电脑店 shop-a", Money.FromYuan(100)) });
            Assert.AreEqual(Money.FromYuan(100), FinanceReport.CitySummary(ledger, day, day).OperatingNet);
            Assert.AreEqual(Money.Zero, FinanceReport.CitySummary(ledger, day, day, "shop-a").OperatingNet);
            Assert.IsNull(ledger.Entries[0].ShopId);
        }
    }
}
