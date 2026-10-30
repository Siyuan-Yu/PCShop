using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Catalog;
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
            data.places = data.places.Concat(new[]
            {
                new LayoutPlaceData { id = "phone", name = "手机店", kind = "shop", node = "dl", size = "small", business = GameSession.PhoneBusiness },
            }).ToArray();
            return new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 8 * 60), true, true, startWithShop);
        }

        [Test]
        public void Quests_AreInOrder_AndHaveText()
        {
            CollectionAssert.AreEqual(new[] { "rent", "mall", "place", "shelf", "stock", "sale", "hire", "phone", "qr" }, Quests.All.Take(9).Select(q => q.Id).ToArray());
            Assert.AreEqual(23, Quests.All.Count);
            Assert.AreEqual(Quests.All.Count, Quests.All.Select(q => q.Id).Distinct().Count());
            foreach (QuestDef q in Quests.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(q.Title) || string.IsNullOrEmpty(q.Short) || string.IsNullOrEmpty(q.Hint), q.Id);
                Assert.That(q.Chapter, Is.InRange(0, Quests.Chapters.Length - 1));
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
        public void RentReward_ThenMallQuest_ThenPlaceFixtures()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("pc"));
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.Shelf), "租铺任务奖励一个货架");
            Assert.AreEqual(1, s.FurnitureInStock(FurnitureCatalog.ShopCounter), "和一个收银台");
            Assert.AreEqual("mall", s.CurrentQuest().Id);
            s.Flags.Add(GameSession.MallBoughtFlag);
            Assert.AreEqual("place", s.CurrentQuest().Id);
            Assert.AreEqual(1, s.PlayerShop.Stock.CountAll(PartCatalog.Get("gpu0")), "家具城任务奖励显卡");
            StringAssert.Contains("货架 0/1", s.CurrentQuest().Progress(s));
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
        public void CompletedTutorial_DoesNotRegressAfterRearrangingOrSaving()
        {
            GameSession s = Make(true);
            Assert.AreEqual("sale", s.CurrentQuest().Id);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(s.SetShelfKind(s.ShelfIndexes()[0], null));
            s.PlayerShop.Stock.Clear();
            Assert.AreEqual("sale", s.CurrentQuest().Id);
            var loaded = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual("sale", loaded.CurrentQuest().Id);
        }

        [Test]
        public void EmptyBackroom_NeedsAFirstBatchOfStock()
        {
            GameSession s = Make(true);
            foreach (var p in PCShopSell.Core.Catalog.PartCatalog.All)
                s.PlayerShop.Stock.TryRemove(p, true, s.PlayerShop.Stock.CountAll(p));
            Assert.AreEqual("stock", s.CurrentQuest().Id);
            s.PlayerShop.Stock.ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get("ram0"), true, 10);
            Assert.AreEqual("sale", s.CurrentQuest().Id, "先经营一个品类即可开业");
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
        public void PhoneAndQr_ContinueIntoGrowthGuide()
        {
            GameSession s = Make(true);
            s.Ledger.Credit(s.Clock.Today, ShopSimulation.SalesCategory, Money.FromYuan(100), "测试");
            HireSomeone(s);
            Assert.AreEqual("qr", s.CurrentQuest().Id, "雇了人、有手机，只差收款码");
            s.SetSmartphone(false);
            Assert.AreEqual("qr", s.CurrentQuest().Id, "已完成手机教学不会倒退");
            s.SetSmartphone(true);
            Assert.AreEqual("qr", s.CurrentQuest().Id);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Qr), "店里不能直接买收款码");
            GameSessionTests.Teleport(s, "phone");
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.Qr));
            Assert.AreEqual("pricing", s.CurrentQuest().Id);
            Assert.AreEqual(9, s.QuestIndex());
        }

        private static GameSession Reload(GameSession s) => GameSession.Load(s.City,
            UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave())));

        // A returning player's completed teaching history; live business conditions remain unchanged.
        private static void FinishBefore(GameSession s, string id)
        {
            foreach (var q in Quests.All.TakeWhile(q => q.Id != id)) s.Flags.Add("tutorial:" + q.Id);
        }

        [Test]
        public void LessonNeedsDestinationAndExplicitAcknowledgement_AndCostsNothing()
        {
            using (var s = Make(true))
            {
                FinishBefore(s, "pricing");
                var cash = s.Ledger.Balance;
                Assert.False(s.CompleteQuestLesson("pricing"));
                s.VisitQuestTarget(QuestTarget.Pricing);
                Assert.False(s.QuestCompleted("pricing"), "打开页面不等于理解教学");
                Assert.True(s.CompleteQuestLesson("pricing"));
                Assert.True(s.QuestCompleted("pricing"));
                Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.False(s.CompleteQuestLesson("enterprise"), "不能用阅读确认冒充实际履约");
                using (var loaded = Reload(s)) Assert.True(loaded.QuestCompleted("pricing"));
            }
        }

        [Test]
        public void LegacyManualTrackingCannotSkipOpeningPrerequisites()
        {
            using (var s = Make(false))
            {
                s.Flags.Add("tutorial:track:shelf");
                s.Flags.Add("tutorial:defer:rent");
                Assert.AreEqual("rent", s.CurrentQuest().Id, "没有铺子时不能因为旧版手动选择去设置货架");
                Assert.False(s.Flags.Contains("tutorial:track:shelf"));
                Assert.False(s.Flags.Contains("tutorial:defer:rent"));
                using (var loaded = Reload(s))
                {
                    Assert.AreEqual("rent", loaded.CurrentQuest().Id);
                    Assert.False(loaded.Flags.Contains("tutorial:issued:park"));
                }
            }
        }

        [Test]
        public void LaterAchievementsAreRememberedWhileEarlierTasksRemain()
        {
            using (var s = Make(true))
            {
                s.SetRestockTarget("cpu0", 2);
                s.RestockDailyBudgetYuan = 2000;
                Assert.AreEqual("sale", s.CurrentQuest().Id);
                Assert.True(s.QuestCompleted("restock"));
                s.SetRestockTarget("cpu0", 0);
                s.RestockDailyBudgetYuan = 0;
                using (var loaded = Reload(s)) Assert.True(loaded.QuestCompleted("restock"));
            }
        }

        [Test]
        public void RestockGuideRequiresWorkingAppBudgetAndOperatingReserve()
        {
            using (var s = Make(true))
            {
                var q = Quests.Get("restock");
                s.SetRestockTarget("cpu0", 2);
                Assert.False(q.Done(s));
                s.RestockDailyBudgetYuan = 2000;
                s.RestockReserveDays = 0;
                Assert.False(q.Done(s));
                s.RestockReserveDays = 7;
                Assert.True(q.Done(s));
                s.SetSmartphone(false);
                Assert.False(q.Done(s));
            }
        }

        [Test]
        public void FirstBuildRequiresDelivery_NotJustStockOrAnAcceptedOrder()
        {
            using (var s = Make(true))
            {
                s.EventsEnabled = false;
                var order = s.PlayerShop.AddContractOrder(null, 0, Money.FromYuan(3000), s.Clock.Now.AddMinutes(2880));
                foreach (var p in PartCatalog.BuildList(0)) s.PlayerShop.Stock.ForceAdd(p, true, 1);
                s.RefreshQuestProgress();
                Assert.False(s.QuestCompleted("build"));
                Assert.True(s.PlayerShop.ReserveBuild(order.Id));
                Assert.True(s.PlayerShop.CompleteBuild(order.Id));
                Assert.True(s.QuestCompleted("build"), "成功交付时即时记录，包括不在跟踪中的步骤");
                using (var loaded = Reload(s)) Assert.True(loaded.QuestCompleted("build"));
            }
        }

        [Test]
        public void EnterpriseGuideNeedsFullDelivery_AndCancelledContractDoesNotPass()
        {
            using (var s = Make(true, 1000000))
            {
                s.PlayerShop.Reputation = 10;
                s.RefreshEnterpriseOffer();
                var contract = s.EnterpriseContracts.First();
                Assert.True(s.AcceptEnterprise(contract.Id));
                s.RefreshQuestProgress();
                Assert.False(s.QuestCompleted("enterprise"));
                Assert.True(s.CancelEnterprise(contract.Id));
                s.RefreshQuestProgress();
                Assert.False(s.QuestCompleted("enterprise"));
            }
        }

        [Test]
        public void DirectSupplyTutorialAcceptsFullPayment_WithoutForcingDebt()
        {
            using (var s = Make(true, 1000000))
            {
                s.PlayerShop.Reputation = 12; s.Meet(Npcs.Zhou, 50);
                Assert.True(s.SignDirectAgreement());
                s.RefreshQuestProgress(); Assert.False(s.QuestCompleted("direct"));
                Assert.True(s.OrderDirect("ram0", 3, false));
                Assert.True(s.QuestCompleted("direct"));
                Assert.Zero(s.SupplierDebts.Count);
                using (var loaded = Reload(s)) Assert.True(loaded.QuestCompleted("direct"));
            }
        }

        [Test]
        public void BranchReviewWaitsForActualSettlement_ThenExplicitReview()
        {
            using (var s = Make(true, 1000000))
            {
                FinishBefore(s, "branch_review");
                s.PlayerShop.Reputation = 20;
                GameSessionTests.Teleport(s, "agency");
                s.DebugAddListing("tea", 0, 100, Money.FromYuan(1000));
                Assert.True(s.OpenVenture("tea", "branch"));
                Assert.True(s.QuestCompleted("branch"));
                s.VisitQuestTarget(QuestTarget.Ventures);
                Assert.False(s.CompleteQuestLesson("branch_review"));
                s.Clock.Restore(new GameDate(0), 1439); s.Clock.Advance(1);
                s.RefreshQuestProgress();
                Assert.False(s.QuestCompleted("branch_review"));
                Assert.AreEqual("branch_review", s.CurrentQuest().Id);
                s.VisitQuestTarget(QuestTarget.Ventures);
                Assert.True(s.CompleteQuestLesson("branch_review"));
                Assert.True(s.CloseVenture("tea"));
                using (var loaded = Reload(s))
                {
                    Assert.True(loaded.QuestCompleted("branch"));
                    Assert.True(loaded.QuestCompleted("branch_review"));
                }
            }
        }

        [Test]
        public void EveryGuidePageCanDescribeNewAndDevelopedSavesWithoutMutatingMoney()
        {
            foreach (bool fitted in new[] { false, true }) using (var s = Make(fitted))
            {
                var cash = s.Ledger.Balance;
                foreach (var q in Quests.All) Assert.DoesNotThrow(() => q.Progress?.Invoke(s), q.Id);
                s.RefreshQuestProgress();
                Assert.AreEqual(cash, s.Ledger.Balance);
            }
        }

        [Test]
        public void BetweenStages_NoTaskUntilReputationReachesActualBusinessThreshold()
        {
            using (var s = Make(true, 1000))
            {
                FinishBefore(s, "enterprise");
                s.PlayerShop.Reputation = 7;
                Assert.IsNull(s.CurrentQuest());
                Assert.AreEqual(Quests.All.Count, s.QuestIndex());
                Assert.False(s.HasPark); Assert.Zero(s.SupplierDebts.Count);
                s.PlayerShop.Reputation = 8;
                Assert.AreEqual("enterprise", s.CurrentQuest().Id);
                using (var loaded = Reload(s)) Assert.AreEqual("enterprise", loaded.CurrentQuest().Id);
            }
        }

        [Test]
        public void FreshGameCannotAcknowledgeFutureLessonsByOpeningTheirPages()
        {
            using (var s = Make(false))
            {
                foreach (var q in Quests.All.Where(x => x.Lesson))
                {
                    s.VisitQuestTarget(q.Target);
                    Assert.False(s.CompleteQuestLesson(q.Id), q.Id);
                    Assert.False(s.Flags.Contains("tutorial:visited:" + q.Id), q.Id);
                }
                Assert.AreEqual("rent", s.CurrentQuest().Id);
            }
        }

        [Test]
        public void FirstBuildDeliveryUnlocksRestocking_WithoutRequiringFutureBusinesses()
        {
            using (var s = Make(true))
            {
                FinishBefore(s, "build");
                Assert.AreEqual("build", s.CurrentQuest().Id);
                var order = s.PlayerShop.AddContractOrder(null, 0, Money.FromYuan(3000), s.Clock.Now.AddMinutes(2880));
                foreach (var p in PartCatalog.BuildList(0)) s.PlayerShop.Stock.ForceAdd(p, true, 1);
                Assert.True(s.PlayerShop.CompleteBuild(order.Id));
                Assert.AreEqual("restock", s.CurrentQuest().Id);
                Assert.False(s.Flags.Contains("tutorial:issued:enterprise"));
                Assert.False(s.Flags.Contains("tutorial:issued:park_plan"));
            }
        }

        [Test]
        public void DirectGuideWaitsForRealRelationshipThreshold()
        {
            using (var s = Make(true, 0))
            {
                FinishBefore(s, "direct");
                s.PlayerShop.Reputation = 10;
                s.Meet(Npcs.Zhou, 39);
                Assert.IsNull(s.CurrentQuest());
                s.AddRelation(Npcs.Zhou, 1);
                Assert.AreEqual("direct", s.CurrentQuest().Id);
                s.AddRelation(Npcs.Zhou, -1);
                using (var loaded = Reload(s)) Assert.AreEqual("direct", loaded.CurrentQuest().Id, "任务已发出后短期波动不会让任务闪现消失");
            }
        }

        [Test]
        public void AllFinished_NoTaskAndNoReplayAcknowledgement()
        {
            using (var s = Make(true))
            {
                foreach (var q in Quests.All) s.Flags.Add("tutorial:" + q.Id);
                Assert.IsNull(s.CurrentQuest());
                s.VisitQuestTarget(QuestTarget.Pricing);
                Assert.False(s.CompleteQuestLesson("pricing"));
                using (var loaded = Reload(s)) Assert.IsNull(loaded.CurrentQuest());
            }
        }

        [Test]
        public void ParkGuideWaitsForRealLandAndConstructionBudget()
        {
            using (var s = Make(true, 349999))
            {
                FinishBefore(s, "park_plan");
                s.Flags.Add("tutorial:creator"); s.Flags.Add("tutorial:property_lesson");
                s.PlayerShop.Reputation = 35;
                s.DebugAddListing("tea", 0, 100, Money.FromYuan(1000));
                Assert.IsNull(s.CurrentQuest(), "没到买地改造预算，不提前布置园区任务");
                s.Ledger.Credit(s.Clock.Today, "测试", Money.FromYuan(1));
                Assert.AreEqual("park_plan", s.CurrentQuest().Id);
                Assert.False(s.HasPark, "派任务本身不买地");
            }
        }

        [Test]
        public void BranchGuideWaitsUntilAnyRealVentureCanBeOpened()
        {
            using (var s = Make(true, 0))
            {
                FinishBefore(s, "branch");
                s.PlayerShop.Reputation = 8;
                s.DebugAddListing("tea", 0, 100, Money.FromYuan(1000));
                var price = s.VentureOpeningCost(s.Listings.Single(l => l.PlaceId == "tea"), Ventures.Get("flowers"));
                s.Ledger.Credit(s.Clock.Today, "测试", price - Money.FromYuan(1));
                Assert.AreNotEqual("branch", s.CurrentQuest()?.Id);
                s.Ledger.Credit(s.Clock.Today, "测试", Money.FromYuan(1));
                Assert.AreEqual("branch", s.CurrentQuest().Id);
            }
        }
    }
}
