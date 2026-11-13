using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class ShopUpgradeTests
    {
        private static GameSession InShop(long cash = 20000)
        {
            CityLayoutData data = GameSessionTests.Layout();
            data.places = data.places.Concat(new[]
            {
                new LayoutPlaceData { id = "phone", name = "手机店", kind = "shop", node = "dl", size = "small", business = GameSession.PhoneBusiness },
                new LayoutPlaceData { id = "mall", name = "家具城", kind = "shop", node = "dl", size = "large", business = GameSession.MallBusiness },
            }).ToArray();
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 8 * 60));
            PackTests.EmptyBag(s);
            GameSessionTests.Teleport(s, "pc");
            return s;
        }

        /// <summary>升级要去卖它的店里买：收款码、自助结账、监控在手机店，灯箱、空调在家具城。</summary>
        private static bool Buy(GameSession s, string id)
        {
            GameSessionTests.Teleport(s, ShopUpgrades.Get(id).SoldAt == ShopUpgrades.FurnitureMall ? "mall" : "phone");
            bool ok = s.BuyUpgrade(id);
            GameSessionTests.Teleport(s, "pc");
            return ok;
        }

        [Test]
        public void Qr_NeedsTheRealSmartphone()
        {
            GameSession s = InShop();
            s.SetSmartphone(false);
            Assert.IsFalse(Buy(s, ShopUpgrades.Qr), "没手机不能开通收款码");
            s.SetSmartphone(true);
            Assert.IsTrue(Buy(s, ShopUpgrades.Qr));
            Assert.AreEqual(PaymentMethods.Cash | PaymentMethods.QrCode, s.PlayerShop.Accepted);
        }

        [Test]
        public void SelfCheckout_CanBePurchasedAfterReputationUnlock()
        {
            GameSession s = InShop();
            Buy(s, ShopUpgrades.Qr);
            Assert.IsFalse(Buy(s, ShopUpgrades.SelfCheckout), "知名度不足时不能购买");
            s.PlayerShop.Reputation = 10;
            Assert.IsTrue(Buy(s, ShopUpgrades.SelfCheckout));
            Assert.IsTrue(s.GrantUpgrade(ShopUpgrades.SelfCheckout));
            Assert.IsTrue((s.PlayerShop.Accepted & PaymentMethods.SelfCheckout) != 0);
        }

        [Test]
        public void Buying_DebitsLedger_AndNeedsMoney()
        {
            GameSession s = InShop(700);
            Assert.IsFalse(Buy(s, ShopUpgrades.Shelf1), "货架扩容不卖了（去家具城买更好的货架）");
            Assert.IsFalse(Buy(s, ShopUpgrades.Phone), "手机去手机店买");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(Buy(s, ShopUpgrades.Cctv));
            Assert.AreEqual((before - ShopUpgrades.Get(ShopUpgrades.Cctv).Price).Cents, s.Ledger.Balance.Cents);
        }

        [Test]
        public void MustBuyAtTheSeller_NotInOwnShop()
        {
            GameSession s = InShop();
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Sign), "自己店里的升级页只能看");
            GameSessionTests.Teleport(s, "phone");
            Assert.IsFalse(s.BuyUpgrade(ShopUpgrades.Sign), "灯箱在家具城卖，手机店买不到");
            Assert.IsTrue(Buy(s, ShopUpgrades.Sign));
        }

        [Test]
        public void LegacyShelfUpgrades_FromOldSaves_StillRaiseCapacity()
        {
            GameSession s = InShop();
            double basic = s.PlayerShop.Stock.Capacity;
            s.GrantUpgrade(ShopUpgrades.Shelf1);
            Assert.AreEqual(basic + 20, s.PlayerShop.Stock.Capacity, 1e-9);
            s.GrantUpgrade(ShopUpgrades.Shelf2);
            s.GrantUpgrade(ShopUpgrades.Shelf3);
            Assert.AreEqual(basic + 70, s.PlayerShop.Stock.Capacity, 1e-9);
        }

        [Test]
        public void SignAndAirCon_ChangeShopParameters()
        {
            GameSession s = InShop();
            int patience = s.PlayerShop.Settings.PatienceMinutes;
            Buy(s, ShopUpgrades.Sign);
            Buy(s, ShopUpgrades.AirCon);
            Assert.AreEqual(ShopUpgrades.SignTrafficBonus, s.PlayerShop.TrafficMultiplier, 1e-9);
            Assert.True(s.HasSeasonalAircon(s.PlayerShopPlace.Id));
            s.Clock.Restore(new PCShopSell.Core.Time.GameDate(150),900);
            long now=s.Clock.Now.TotalMinutes;s.TickCityClimate(now,now+1);
            Assert.AreEqual(0,s.CityClimateZones.Count,"a facility does not create a room simulation");
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
            Buy(s, ShopUpgrades.Qr);
            s.GrantUpgrade(ShopUpgrades.SelfCheckout);

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
            Buy(s, ShopUpgrades.Qr);
            s.GrantUpgrade(ShopUpgrades.Shelf1);
            s.Flags.Add("x_flag");
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.OrderOnline(ParcelKind.Outfit, "shirt"));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "bed_spring"));
            while (s.Clock.Today.TotalDays < 3) s.AdvanceClock(60);
            Assert.IsTrue(s.Equip("shirt"));

            SaveData d = s.CreateSave();
            GameSession loaded = GameSession.Load(s.City, d);
            Assert.IsTrue(loaded.HasUpgrade(ShopUpgrades.Qr));
            Assert.AreEqual(s.PlayerShop.Stock.Capacity, loaded.PlayerShop.Stock.Capacity, 1e-9);
            Assert.AreEqual(s.PlayerShop.Accepted, loaded.PlayerShop.Accepted);
            Assert.AreEqual("shirt", loaded.EquippedOutfit);
            Assert.AreEqual(1, loaded.FurnitureStored("bed_spring"));
            Assert.AreEqual(s.HomeLayout.Count, loaded.HomeLayout.Count);
            Assert.IsTrue(loaded.Flags.Contains("x_flag"));
        }
    }

    public class LifeGrowthTests
    {
        [Test]
        public void Outfit_ArrivesByParcel_AndAddsCharm()
        {
            GameSession s = GameSessionTests.Make(5000);
            int charm = s.Player.Charm;
            Assert.AreEqual(charm, s.EffectiveCharm);
            Assert.IsTrue(s.OrderOnline(ParcelKind.Outfit, "suit"));
            Assert.IsFalse(s.Equip("suit"), "快递还没到");
            while (s.Clock.Today.TotalDays < 2) s.AdvanceClock(60);
            s.Leave();
            Assert.IsFalse(s.Equip("suit"), "到货不代表能在街上远程更衣");
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.Equip("suit"));
            Assert.AreEqual(charm + 7, s.EffectiveCharm);
            Assert.IsTrue(s.Equip(Outfits.Default));
            Assert.AreEqual(charm, s.EffectiveCharm);
            Assert.IsTrue(s.Equip("suit"));
        }

        [Test]
        public void Outfit_CannotBeBoughtWithoutMoney()
        {
            GameSession s = GameSessionTests.Make(100);
            Assert.IsFalse(s.OrderOnline(ParcelKind.Outfit, "suit"));
            Assert.AreEqual(Outfits.Default, s.EquippedOutfit);
        }

        [Test]
        public void BetterHome_RecoversMoreEnergy_AndCostsUtilities()
        {
            GameSession a = GameSessionTests.Make(20000, 22 * 60);
            GameSession b = GameSessionTests.Make(20000, 22 * 60);
            HomeTests.FurnishBetter(b);
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
        public void ScheduledCredit_ShowsDueDateAndPaysExactlyOnceAfterLoad()
        {
            using var s = GameSessionTests.Make(5000);
            var amount = Money.FromYuan(800);
            var due = s.ScheduleCredit(3, amount, "表哥还钱");
            using var loaded = GameSession.Load(s.City, s.CreateSave());
            loaded.EventsEnabled = false;
            Assert.AreEqual(due.TotalDays * (long)GameTimestamp.MinutesPerDay,
                loaded.Schedule().Single(e => e.Kind == "npc_repayment").Minute);
            Assert.IsFalse(loaded.Ledger.Entries.Any(e => e.Memo == "表哥还钱"));

            loaded.Clock.Restore(due.AddDays(-1), GameTimestamp.MinutesPerDay - 1);
            var before = loaded.Ledger.Balance;
            loaded.AdvanceClock(1);
            Assert.AreEqual(before + amount, loaded.Ledger.Balance);
            Assert.IsFalse(loaded.Schedule().Any(e => e.Kind == "npc_repayment"));

            using var settled = GameSession.Load(s.City, loaded.CreateSave());
            settled.AdvanceClock(GameTimestamp.MinutesPerDay);
            var receipt = settled.Ledger.Entries.Single(e => e.Category == GameSession.NpcRepaymentTitle);
            Assert.AreEqual(amount, receipt.Amount);
            Assert.AreEqual(due, receipt.Date);
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
            Assert.IsFalse(s.ResolveEvent(0), "钱不够不能花 ¥200");
            Assert.IsNotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(1));
        }

        [Test]
        public void Flyer_ChargesPromotionWithoutImmediateReputation()
        {
            GameSession s = Make();
            double rep = s.PlayerShop.Reputation;
            Money cash = s.Ledger.Balance;
            s.RaiseEvent("flyer");
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(rep, s.PlayerShop.Reputation, 1e-9);
            Assert.AreEqual(cash - Money.FromYuan(200), s.Ledger.Balance);
        }

        [Test]
        public void Blackout_ChargesPerUnprotectedShopAndCanRecurAfterFailure()
        {
            using (var s = Make(200000))
            {
                s.PlayerShop.Reputation = 80;
                s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(s, "agency");
                Assert.IsTrue(s.SignRetailLease("tea", true, "computer"));

                Money cash = s.Ledger.Balance;
                s.RaiseEvent("blackout");
                Assert.IsTrue(s.ResolveEvent(0));
                Assert.AreEqual(cash - Money.FromYuan(300), s.Ledger.Balance);
                Assert.IsFalse(EventCatalog.Get("blackout").When(s));
                using (var loaded = GameSession.Load(s.City, s.CreateSave()))
                {
                    var main = loaded.MainShop;
                    var branch = loaded.ShopAt("tea");
                    Assert.AreEqual(s.MainShop.EmergencyLighting.FailureDay, main.EmergencyLighting.FailureDay);
                    Assert.AreEqual(s.ShopAt("tea").EmergencyLighting.FailureDay, branch.EmergencyLighting.FailureDay);
                    Assert.IsTrue(branch.EmergencyLighting.PowerOutAt(loaded.Clock.Now.TotalMinutes));
                    int day = main.EmergencyLighting.FailureDay;
                    branch.EmergencyLighting.FailureDay = day + 1;
                    loaded.Clock.Restore(new GameDate(day), 15 * 60);
                    Assert.IsTrue(EventCatalog.Get("blackout").When(loaded));
                    cash = loaded.Ledger.Balance;
                    loaded.RaiseEvent("blackout");
                    Assert.IsTrue(loaded.ResolveEvent(0));
                    Assert.AreEqual(cash - Money.FromYuan(150), loaded.Ledger.Balance);
                    Assert.AreEqual(day + 1, branch.EmergencyLighting.FailureDay);
                }
            }
        }

        [Test]
        public void StockDiscrepancyEnablesVideoReviewWhenCctvIsInstalled()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            s.PlayerShop.SetHours(0,1440);s.PlayerShop.ForcedClosed=false;s.PlayerShop.NotFitted=false;
            s.SetShopMainCategory("computer");
            s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"memory",60}});
            s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 5);
            Assert.IsTrue(EventCatalog.Get("thief").When(s));
            s.RaiseEvent("thief");
            Assert.IsFalse(s.IsChoiceEnabled(s.PendingEvent.Choices[1]));
            var start = s.Clock.Now.TotalMinutes;
            int before = s.PlayerShop.Stock.CountAll(PartCatalog.Get("ram0"));
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.AreEqual(start, s.Clock.Now.TotalMinutes);
            Assert.That(before - s.PlayerShop.Stock.CountAll(PartCatalog.Get("ram0")), Is.InRange(0, 3));
            s.RaiseEvent("thief");
            before = s.PlayerShop.Stock.CountAll(PartCatalog.Get("ram0"));
            Assert.IsTrue(s.ResolveEvent(2));
            Assert.That(before - s.PlayerShop.Stock.CountAll(PartCatalog.Get("ram0")), Is.InRange(1, 3));
            s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 5);
            s.GrantUpgrade(ShopUpgrades.Cctv);
            s.RaiseEvent("thief");
            Assert.IsTrue(s.IsChoiceEnabled(s.PendingEvent.Choices[1]));
        }

        [Test]
        public void Students_SellFiveRam()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s,"pc");
            s.SetShopMainCategory("computer");
            s.PlayerShop.SetHours(0,1440);s.PlayerShop.ForcedClosed=false;s.PlayerShop.NotFitted=false;
            s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"memory",60}});
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
            var scriptIds = EventLoader.ReadFile().events.Where(entry => !entry.code).Select(entry => entry.id).ToHashSet();
            foreach (EventDef def in EventCatalog.All.Where(def => scriptIds.Contains(def.Id)))
            {
                Assert.IsNotEmpty(def.Title);
                Assert.IsNotEmpty(def.Body);
                Assert.GreaterOrEqual(def.Choices.Count, 2, def.Id);
                Assert.IsTrue(def.Choices.Any(c => c.Enabled == null), def.Id + " 至少有一个总能选的选项，免得玩家卡死");
            }
        }
    }
}
