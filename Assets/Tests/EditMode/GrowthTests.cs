using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public class ShopUpgradeTests
    {
        private static GameSession InShop(long cash = 20000)
        {
            GameSession s = GameSessionTests.Make(cash);
            GameSessionTests.Teleport(s, "pc");
            return s;
        }

        [Test]
        public void Upgrades_NeedPrerequisites()
        {
            GameSession s = InShop();
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Qr), "没手机不能开通收款码");
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.Phone));
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.Qr));
            Assert.AreEqual(PaymentMethods.Cash | PaymentMethods.QrCode, s.PlayerShop.Accepted);
        }

        [Test]
        public void SelfCheckout_NeedsReputation()
        {
            GameSession s = InShop();
            s.BuyUpgrade(ShopUpgrades.Phone);
            s.BuyUpgrade(ShopUpgrades.Qr);
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.SelfCheckout));
            s.PlayerShop.Reputation = 10;
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.SelfCheckout));
            Assert.IsTrue((s.PlayerShop.Accepted & PaymentMethods.SelfCheckout) != 0);
        }

        [Test]
        public void Buying_DebitsLedger_AndNeedsMoney()
        {
            GameSession s = InShop(700);
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Shelf1), "只有 ¥700 买不起 ¥1000 的扩容");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.BuyUpgrade(ShopUpgrades.Cctv));
            Assert.AreEqual((before - ShopUpgrades.Get(ShopUpgrades.Cctv).Price).Cents, s.Ledger.Balance.Cents);
        }

        [Test]
        public void MustBeInShop()
        {
            GameSession s = GameSessionTests.Make(20000);
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Sign));
        }

        [Test]
        public void ShelfUpgrades_RaiseCapacity()
        {
            GameSession s = InShop();
            double basic = s.PlayerShop.Stock.Capacity;
            s.BuyUpgrade(ShopUpgrades.Shelf1);
            Assert.AreEqual(basic + 20, s.PlayerShop.Stock.Capacity, 1e-9);
            s.BuyUpgrade(ShopUpgrades.Shelf2);
            s.BuyUpgrade(ShopUpgrades.Shelf3);
            Assert.AreEqual(basic + 70, s.PlayerShop.Stock.Capacity, 1e-9);
        }

        [Test]
        public void SignAndAirCon_ChangeShopParameters()
        {
            GameSession s = InShop();
            int patience = s.PlayerShop.Settings.PatienceMinutes;
            s.BuyUpgrade(ShopUpgrades.Sign);
            s.BuyUpgrade(ShopUpgrades.AirCon);
            Assert.AreEqual(ShopUpgrades.SignTrafficBonus, s.PlayerShop.TrafficMultiplier, 1e-9);
            Assert.AreEqual(patience + ShopUpgrades.AirConPatienceBonus, s.PlayerShop.Settings.PatienceMinutes);
        }

        [Test]
        public void SelfCheckout_AutoPaysQueue_WithoutClicking()
        {
            GameSession s = InShop();
            PartDef ram = PartCatalog.Get("ram0");
            s.PlayerShop.Stock.ForceAdd(ram, true, 50);
            s.PlayerShop.Markup = 0.9;
            s.PlayerShop.Reputation = 10;
            s.BuyUpgrade(ShopUpgrades.Phone);
            s.BuyUpgrade(ShopUpgrades.Qr);
            s.BuyUpgrade(ShopUpgrades.SelfCheckout);

            int maxQueue = 0;
            for (int i = 0; i < 12 * 60; i++)
            {
                s.AdvanceClock(1);
                maxQueue = System.Math.Max(maxQueue, s.PlayerShop.Queue.Count);
            }
            Assert.LessOrEqual(maxQueue, 3, "自助结账下排队很短");
            Assert.IsTrue(s.Ledger.Entries.Any(e => e.Category == ShopSimulation.SalesCategory), "不点结账也有零售收入");
        }

        [Test]
        public void SaveLoad_KeepsUpgradesOutfitsHomeAndFlags()
        {
            GameSession s = InShop();
            s.BuyUpgrade(ShopUpgrades.Phone);
            s.BuyUpgrade(ShopUpgrades.Qr);
            s.BuyUpgrade(ShopUpgrades.Shelf1);
            s.Flags.Add("x_flag");
            GameSessionTests.Teleport(s, "flat");
            s.BuyOutfit("shirt");
            s.UpgradeHome();

            SaveData d = s.CreateSave();
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.IsTrue(loaded.HasUpgrade(ShopUpgrades.Qr));
            Assert.AreEqual(s.PlayerShop.Stock.Capacity, loaded.PlayerShop.Stock.Capacity, 1e-9);
            Assert.AreEqual(s.PlayerShop.Accepted, loaded.PlayerShop.Accepted);
            Assert.AreEqual("shirt", loaded.EquippedOutfit);
            Assert.AreEqual(1, loaded.HomeComfort);
            Assert.IsTrue(loaded.Flags.Contains("x_flag"));
        }
    }

    public class LifeGrowthTests
    {
        [Test]
        public void Outfit_AddsCharm_AndNeedsHome()
        {
            GameSession s = GameSessionTests.Make(5000);
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.BuyOutfit("suit"), "不在家不能换衣服");
            GameSessionTests.Teleport(s, "flat");
            int charm = s.Player.Charm;
            Assert.AreEqual(charm, s.EffectiveCharm);
            Assert.IsTrue(s.BuyOutfit("suit"));
            Assert.AreEqual(charm + 7, s.EffectiveCharm);
            Assert.IsTrue(s.Equip(Outfits.Default));
            Assert.AreEqual(charm, s.EffectiveCharm);
            Assert.IsTrue(s.Equip("suit"));
        }

        [Test]
        public void Outfit_CannotBeBoughtWithoutMoney()
        {
            GameSession s = GameSessionTests.Make(100);
            Assert.IsFalse(s.BuyOutfit("suit"));
            Assert.AreEqual(Outfits.Default, s.EquippedOutfit);
        }

        [Test]
        public void BetterHome_RecoversMoreEnergy_AndCostsUtilities()
        {
            GameSession a = GameSessionTests.Make(20000, 22 * 60);
            GameSession b = GameSessionTests.Make(20000, 22 * 60);
            Assert.IsTrue(b.UpgradeHome());
            Assert.IsTrue(b.UpgradeHome());
            Assert.IsTrue(b.UpgradeHome());
            Assert.IsFalse(b.UpgradeHome(), "最高三档");
            a.Player.Energy = 20;
            b.Player.Energy = 20;
            a.Nap();
            b.Nap();
            Assert.Greater(b.Player.Energy, a.Player.Energy);

            // 水电费：下一个月初扣
            Money before = b.Ledger.Balance;
            while (b.Clock.Today.TotalDays < GameDate.DaysPerMonth) b.AdvanceClock(60);
            Assert.IsTrue(b.Ledger.Entries.Any(e => e.Category == "水电" && e.Amount.IsNegative));
            Assert.IsTrue(b.Ledger.Balance < before);
        }

        [Test]
        public void ScheduledCredit_PaysOutLater()
        {
            GameSession s = GameSessionTests.Make(5000);
            s.ScheduleCredit(3, Money.FromYuan(800), "还钱");
            while (s.Clock.Today.TotalDays < 2) s.AdvanceClock(60);
            Assert.IsFalse(s.Ledger.Entries.Any(e => e.Memo == "还钱"));
            while (s.Clock.Today.TotalDays < 4) s.AdvanceClock(60);
            Assert.IsTrue(s.Ledger.Entries.Any(e => e.Memo == "还钱" && e.Amount == Money.FromYuan(800)));
        }
    }

    public class EventSystemTests
    {
        private static GameSession Make(long cash = 8000)
        {
            GameSession s = GameSessionTests.Make(cash);
            s.EventsEnabled = true;
            return s;
        }

        [Test]
        public void EventsAreOff_ByDefault()
        {
            GameSession s = GameSessionTests.Make();
            while (s.Clock.Today.TotalDays < 20) s.AdvanceClock(60);
            Assert.IsNull(s.PendingEvent);
        }

        [Test]
        public void Events_FireOverTime_AndEveryChoiceResolves()
        {
            int resolved = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var city = PCShopSell.Core.World.CityLayout.FromData(GameSessionTests.Layout());
                var s = new GameSession(city, seed, Money.FromYuan(30000), GameTimestamp.From(new GameDate(0), 8 * 60));
                s.EventsEnabled = true;
                s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 10);
                s.PlayerShop.Reputation = 10;
                for (int day = 0; day < 60 && !s.IsGameOver; day++)
                {
                    for (int h = 0; h < 24; h++)
                    {
                        s.AdvanceClock(60);
                        if (s.PendingEvent != null)
                        {
                            int idx = (day + h) % s.PendingEvent.Choices.Count;
                            if (!s.ResolveEvent(idx)) Assert.IsTrue(s.ResolveEvent(s.PendingEvent.Choices.Count - 1));
                            resolved++;
                        }
                    }
                    s.Ledger.Credit(s.Clock.Today, "测试", Money.FromYuan(500), "补钱");
                }
            }
            Assert.Greater(resolved, 10, "60 天里应该遇到不少事件");
        }

        [Test]
        public void Pending_BlocksSave_UntilResolved()
        {
            GameSession s = Make();
            s.RaiseEvent("flyer");
            Assert.IsNotNull(s.PendingEvent);
            Assert.IsFalse(s.CanSave);
            Assert.IsTrue(s.ResolveEvent(1));
            Assert.IsNull(s.PendingEvent);
            Assert.IsTrue(s.CanSave);
        }

        [Test]
        public void DisabledChoice_CannotBePicked()
        {
            GameSession s = Make(50);
            s.RaiseEvent("flyer");
            Assert.IsFalse(s.ResolveEvent(0), "钱不够不能花 ¥100");
            Assert.IsNotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(1));
        }

        [Test]
        public void Flyer_BuysReputation()
        {
            GameSession s = Make();
            double rep = s.PlayerShop.Reputation;
            Money cash = s.Ledger.Balance;
            s.RaiseEvent("flyer");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(rep + 2, s.PlayerShop.Reputation, 1e-9);
            Assert.AreEqual((cash - Money.FromYuan(100)).Cents, s.Ledger.Balance.Cents);
        }

        [Test]
        public void Blackout_EmergencyLight_PreventsRepeat()
        {
            GameSession s = Make();
            s.RaiseEvent("blackout");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.IsTrue(s.Flags.Contains(EventCatalog.EmergencyLightFlag));
            Assert.IsFalse(EventCatalog.Get("blackout").When(s));
        }

        [Test]
        public void Thief_DoesNotHappenWithCctv()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            Assert.IsTrue(EventCatalog.Get("thief").When(s));
            s.BuyUpgrade(ShopUpgrades.Cctv);
            Assert.IsFalse(EventCatalog.Get("thief").When(s));
        }

        [Test]
        public void Students_SellFiveRam()
        {
            GameSession s = Make();
            PartDef ram = PartCatalog.Get("ram0");
            s.PlayerShop.Stock.ForceAdd(ram, true, 5);
            s.PlayerShop.Reputation = 3;
            int have = s.PlayerShop.Stock.CountAll(ram);
            Assert.IsTrue(EventCatalog.Get("students").When(s));
            Money cash = s.Ledger.Balance;
            s.RaiseEvent("students");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(have - 5, s.PlayerShop.Stock.CountAll(ram));
            Assert.IsTrue(s.Ledger.Balance > cash);
        }

        [Test]
        public void Lend_MayBeRepaidLater()
        {
            bool repaid = false;
            for (ulong seed = 1; seed < 40 && !repaid; seed++)
            {
                var city = PCShopSell.Core.World.CityLayout.FromData(GameSessionTests.Layout());
                var s = new GameSession(city, seed, Money.FromYuan(8000), GameTimestamp.From(new GameDate(0), 8 * 60));
                s.RaiseEvent("lend");
                Money start = s.Ledger.Balance;
                s.ResolveEvent(0);
                Assert.AreEqual((start - Money.FromYuan(800)).Cents, s.Ledger.Balance.Cents);
                while (s.Clock.Today.TotalDays < 5) s.AdvanceClock(60);
                if (s.Ledger.Entries.Any(e => e.Memo == "表哥还钱")) repaid = true;
            }
            Assert.IsTrue(repaid, "借钱有一定概率会还");
        }

        [Test]
        public void EventHistory_SurvivesSaveLoad_SoCooldownHolds()
        {
            GameSession s = Make();
            s.RaiseEvent("flyer");
            s.ResolveEvent(1);
            SaveData d = s.CreateSave();
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.AreEqual(1, loaded.EventCountOf("flyer"));
            Assert.IsNull(loaded.PendingEvent);
        }

        [Test]
        public void Catalog_IsWellFormed()
        {
            var ids = EventCatalog.All.Select(e => e.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "事件 id 不能重复");
            foreach (EventDef def in EventCatalog.All)
            {
                Assert.IsNotEmpty(def.Title);
                Assert.IsNotEmpty(def.Body);
                Assert.GreaterOrEqual(def.Choices.Count, 2, def.Id);
                Assert.IsTrue(def.Choices.Any(c => c.Enabled == null), def.Id + " 至少有一个总能选的选项，免得玩家卡死");
            }
        }
    }
}
