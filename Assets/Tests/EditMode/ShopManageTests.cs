using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    /// <summary>按配件定价、自动补货、经营报表。</summary>
    public class ShopManageTests
    {
        private static GameSession Make(bool phone = true, long cash = 20000)
        {
            return new GameSession(PCShopSell.Core.World.CityLayout.FromData(GameSessionTests.Layout()), 7,
                Money.FromYuan(cash), GameTimestamp.From(new GameDate(3), 8 * 60), false, phone);
        }

        [Test]
        public void PartMarkup_OverridesGlobal_AndSaves()
        {
            GameSession s = Make();
            PartDef gpu = PartCatalog.Get("gpu1"), ram = PartCatalog.Get("ram0");
            s.SetMarkup(1.2);
            Money ramGlobal = s.PlayerShop.PriceOf(ram, true);
            s.PlayerShop.SetPartMarkup(gpu, 1.5);
            Assert.AreEqual(1.5, s.PlayerShop.MarkupOf(gpu), 1e-9);
            Assert.AreEqual(1.2, s.PlayerShop.MarkupOf(ram), 1e-9, "没单独定的跟全局");
            Assert.AreEqual(ramGlobal, s.PlayerShop.PriceOf(ram, true));
            Assert.Greater(s.PlayerShop.PriceOf(gpu, true).Cents, (s.Market.PriceOf(gpu) * 1).Cents * 1.5, "单独 1.5 倍，盒装再贵 12%");

            s.SetMarkup(1.0);
            Assert.AreEqual(1.5, s.PlayerShop.MarkupOf(gpu), 1e-9, "全局变了，单独定价不变");

            s.PlayerShop.SetPartMarkup(gpu, 9.0);
            Assert.AreEqual(1.6, s.PlayerShop.MarkupOf(gpu), 1e-9, "上限 1.6");

            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(1.6, l.PlayerShop.MarkupOf(PartCatalog.Get("gpu1")), 1e-9);
            Assert.IsFalse(l.PlayerShop.HasPartMarkup(PartCatalog.Get("ram0")));

            s.PlayerShop.SetPartMarkup(gpu, null);
            Assert.AreEqual(1.0, s.PlayerShop.MarkupOf(gpu), 1e-9, "恢复跟随全局");
        }

        [Test]
        public void FinanceReport_SumsRevenueAndExpensePerDay()
        {
            var ledger = new Ledger(Money.FromYuan(1000));
            var d5 = new GameDate(5);
            ledger.Credit(d5, FinanceReport.RetailCategory, Money.FromYuan(300));
            ledger.Credit(d5, FinanceReport.BuildCategory, Money.FromYuan(200));
            ledger.Credit(d5, "其他", Money.FromYuan(99));
            ledger.ForceDebit(d5, "进货", Money.FromYuan(120));
            ledger.Credit(new GameDate(7), FinanceReport.RetailCategory, Money.FromYuan(50));
            ledger.Credit(new GameDate(1), FinanceReport.RetailCategory, Money.FromYuan(999));

            var days = FinanceReport.Daily(ledger, new GameDate(7), 3);
            Assert.AreEqual(3, days.Count);
            Assert.AreEqual(5, days[0].Date.TotalDays);
            Assert.AreEqual(Money.FromYuan(500), days[0].Revenue, "只统计零售和装机");
            Assert.AreEqual(Money.FromYuan(120), days[0].Expense);
            Assert.AreEqual(Money.Zero, days[1].Revenue);
            Assert.AreEqual(Money.FromYuan(50), days[2].Revenue);

            Assert.AreEqual(2, FinanceReport.Daily(ledger, new GameDate(1), 5).Count, "不画游戏开始以前的日子");
        }

        [Test]
        public void AutoRestock_OrdersWhenLow_AndRespectsPhoneAndCash()
        {
            GameSession s = Make();
            PartDef ram = PartCatalog.Get("ram1");
            Assert.IsTrue(s.UninstallApp(PhoneApps.Wholesale));
            s.SetRestockTarget(ram.Id, 10);
            Assert.AreEqual(10, s.RestockTarget(ram.Id));

            s.RunAutoRestock();
            Assert.AreEqual(0, s.Online.Pending.Count, "没装 1788 不能自动下单");
            Assert.IsNotNull(s.LastRestockNote);

            Assert.IsTrue(s.InstallApp(PhoneApps.Wholesale));
            Money before = s.Ledger.Balance;
            s.RunAutoRestock();
            Assert.AreEqual(1, s.Online.Pending.Count, "库存 0 < 40%，应该下单");
            Assert.AreEqual(10, s.PendingOnlineCount(ram.Id), "补到目标");
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);

            s.RunAutoRestock();
            Assert.AreEqual(1, s.Online.Pending.Count, "在途也算库存，不重复下单");

            s.SetRestockTarget(ram.Id, 0);
            Assert.AreEqual(0, s.RestockTarget(ram.Id));
        }

        [Test]
        public void AutoRestock_SkipsWhenCashWouldRunLow_AndDailyTriggerAt9()
        {
            GameSession s = Make(true, 300);
            s.SetRestockTarget("cpu1", 10);
            s.RunAutoRestock();
            Assert.AreEqual(0, s.Online.Pending.Count, "钱不够留 500 的底");

            GameSession r = Make();
            r.SetRestockTarget("ssd1", 6);
            r.AdvanceClock(60 + 5);
            Assert.AreEqual(1, r.Online.Pending.Count, "8:00 出发，推进过 9:00 自动补货");

            GameSession l = GameSession.Load(r.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(r.CreateSave())));
            Assert.AreEqual(6, l.RestockTarget("ssd1"));
        }
    }
}
