using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>新手任务：新游戏先去租铺子，再买设施、摆设施、设置货架、进货、卖出第一单……每一步看实际状态判断。</summary>
    public class QuestTests
    {
        private static GameSession Make(bool startWithShop, long cash = 30000)
        {
            CityLayoutData data = GameSessionTests.Layout();
            return new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, startWithShop);
        }

        [Test]
        public void Quests_AreInOrder_AndHaveText()
        {
            CollectionAssert.AreEqual(new[] { "rent", "buy", "place", "shelf", "stock", "sale", "hire", "phone", "qr" }, Quests.All.Select(q => q.Id).ToArray());
            foreach (QuestDef q in Quests.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(q.Title) || string.IsNullOrEmpty(q.Short) || string.IsNullOrEmpty(q.Hint), q.Id);
            }
        }

        [Test]
        public void NewGame_FirstQuestIsToRentAShop()
        {
            GameSession s = Make(false);
            Assert.AreEqual("rent", s.CurrentQuest().Id);
            Assert.AreEqual(0, s.QuestIndex());
        }

        [Test]
        public void AfterRenting_NextIsBuyingFixtures_WithProgress()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("pc"));
            QuestDef q = s.CurrentQuest();
            Assert.AreEqual("buy", q.Id);
            StringAssert.Contains("货架 1/6", q.Progress(s));
            StringAssert.Contains("收银台 1/1", q.Progress(s));

            s.DebugGiveFurniture(FurnitureCatalog.Shelf, 4);
            StringAssert.Contains("货架 5/6", s.CurrentQuest().Progress(s));
            Assert.AreEqual("buy", s.CurrentQuest().Id);

            // 买齐：6 个货架（哪档都算，送的 1 个也算）、收银台（送的）、展示桌、操作台
            s.DebugGiveFurniture(FurnitureCatalog.RackStrong, 1);
            s.DebugGiveFurniture(FurnitureCatalog.ShopBench, 1);
            s.DebugGiveFurniture(FurnitureCatalog.ShopDisplay, 2);
            Assert.AreEqual("place", s.CurrentQuest().Id, "买齐了就该摆了");
            StringAssert.Contains("货架 0/6", s.CurrentQuest().Progress(s));
        }

        [Test]
        public void FittedShop_SkipsToShelvesStockAndFirstSale()
        {
            GameSession s = Make(true);
            Assert.AreEqual("sale", s.CurrentQuest().Id, "开局送的店设施齐全、货架设置好、有货，下一步是卖出第一单");
            s.Ledger.Credit(s.Clock.Today, ShopSimulation.SalesCategory, Money.FromYuan(100), "测试");
            Assert.AreEqual("hire", s.CurrentQuest().Id);
        }

        [Test]
        public void UnsetShelf_BringsTheShelfQuestBack()
        {
            GameSession s = Make(true);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.SetShelfKind(s.ShelfIndexes()[0], null));
            Assert.AreEqual("shelf", s.CurrentQuest().Id);
            StringAssert.Contains("5/6", s.CurrentQuest().Progress(s));
        }

        [Test]
        public void EmptyBackroom_NeedsAFirstBatchOfStock()
        {
            GameSession s = Make(true);
            foreach (var p in PCShopSell.Core.Catalog.PartCatalog.All)
                s.PlayerShop.Stock.TryRemove(p, true, s.PlayerShop.Stock.CountAll(p));
            Assert.AreEqual("stock", s.CurrentQuest().Id);
            s.PlayerShop.Stock.ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get("ram0"), true, 10);
            Assert.AreEqual("stock", s.CurrentQuest().Id, "只有一类配件不够");
            s.PlayerShop.Stock.ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get("ssd0"), true, 1);
            s.PlayerShop.Stock.ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get("cpu0"), true, 1);
            Assert.AreEqual("sale", s.CurrentQuest().Id);
        }

        private static void HireSomeone(GameSession s)
        {
            GameSessionTests.Teleport(s, "pc");
            s.PostJobAd();
            for (int i = 0; i < 12 * 24 && s.Candidates.Count == 0; i++) s.AdvanceClock(60);
            Assert.Greater(s.Candidates.Count, 0, "贴了启事要来人");
            Assert.IsTrue(s.Hire(0));
        }

        [Test]
        public void PhoneAndQr_AreTheLastSteps_ThenNoQuestIsLeft()
        {
            GameSession s = Make(true);
            s.Ledger.Credit(s.Clock.Today, ShopSimulation.SalesCategory, Money.FromYuan(100), "测试");
            HireSomeone(s);
            Assert.AreEqual("qr", s.CurrentQuest().Id, "雇了人、有手机，只差收款码");
            s.SetSmartphone(false);
            Assert.AreEqual("phone", s.CurrentQuest().Id);
            s.SetSmartphone(true);
            Assert.AreEqual("qr", s.CurrentQuest().Id);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.Qr));
            Assert.IsNull(s.CurrentQuest());
            Assert.AreEqual(Quests.All.Count, s.QuestIndex());
        }
    }
}
