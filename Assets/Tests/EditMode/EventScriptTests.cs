using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    /// <summary>Resources/Data/events.json 的格式和加载。</summary>
    public class EventScriptTests
    {
        [Test]
        public void EventsJson_IsValid_AndEveryCodeEventIsListed()
        {
            var file = EventLoader.ReadFile();
            Assert.NotNull(file, "Resources/Data/events.json 读不出来");
            var codeIds = EventCatalog.CodeEvents.Select(e => e.Id).ToList();
            var errors = EventScript.Validate(file, codeIds);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            foreach (string id in codeIds) Assert.IsTrue(file.events.Any(e => e.id == id && e.code), "C# 事件没在 json 里排位：" + id);
        }

        [Test]
        public void Catalog_FollowsJsonOrder_AndOverridesCodeEventNumbers()
        {
            var file = EventLoader.ReadFile();
            CollectionAssert.AreEqual(file.events.Select(e => e.id).ToArray(), EventCatalog.All.Select(e => e.Id).ToArray());
            Assert.AreEqual(8, EventCatalog.Get("inspection").Weight);
            Assert.AreEqual(5, EventCatalog.Get("inspection").MinDay);
            Assert.GreaterOrEqual(EventCatalog.All.Count, 22);
        }

        [Test]
        public void DebugEventPresetCanRaiseTheFirstTenInOrder()
        {
            using var fixture = DistrictProgressionTests.Make();
            using var session = GameSession.CreateCityLifePreset(fixture.City, "events");
            var expected = new[] { "inspection", "regulars", "blackout", "thief", "students",
                "rival", "wallet", "friend_meal", "lend", "classmate_help" };
            for (int number = 1; number <= expected.Length; number++)
            {
                Assert.IsTrue(session.DebugRaiseNumberedEvent(number), $"#{number:00} {expected[number - 1]}");
                Assert.AreEqual(expected[number - 1], session.PendingEvent.Id);
                int choice = Enumerable.Range(0, session.PendingEvent.Choices.Count)
                    .Last(index => session.IsChoiceEnabled(session.PendingEvent.Choices[index]));
                Assert.IsTrue(session.ResolveEvent(choice));
            }
            Assert.IsNull(GameSession.DebugNumberedEvent(102));
            Assert.AreEqual("money_win_lottery", GameSession.DebugNumberedEvent(103).Id);
            Assert.AreEqual("life_festival_lantern", GameSession.DebugNumberedEvent(124).Id);
        }

        [Test]
        public void Reader_ReportsTypos_AndBadJson()
        {
            var typo = EventScriptReader.Read("{\"events\":[{\"id\":\"x\",\"titel\":\"a\",\"choices\":[{\"text\":\"t\",\"effects\":[{\"type\":\"pay\",\"yaun\":5}]}]}]}");
            var errors = EventScript.Validate(typo, new string[0]);
            Assert.IsTrue(errors.Any(e => e.Contains("titel")), string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("yaun")), string.Join("\n", errors));
            Assert.IsTrue(EventScriptReader.Read("{\"events\":[").readErrors.Count > 0);
            Assert.IsEmpty(EventCatalog.LoadErrors, string.Join("\n", EventCatalog.LoadErrors));
        }

        [Test]
        public void InspectionRequiresMinimumCharmAndShowsTheActualChance()
        {
            using (var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(PCShopSell.Tests.GameSessionTests.Layout()), 3, Money.FromYuan(100), PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(5), 600), true))
            {
                s.DebugRentStarterShop(true);
                s.PlayerShop.NotFitted = true;
                s.Player.Charm += 44 - s.EffectiveCharm;
                s.RaiseEvent("inspection");
                Assert.That(s.PendingEvent.Choices[2].Text, Does.Contain("0"));
                Assert.IsFalse(s.IsChoiceEnabled(s.PendingEvent.Choices[2]));
                Assert.IsTrue(s.ResolveEvent(1));
                s.Player.Charm += 45 - s.EffectiveCharm;
                s.RaiseEvent("inspection");
                Assert.That(s.PendingEvent.Choices[2].Text, Does.Contain("35"));
                Assert.IsTrue(s.IsChoiceEnabled(s.PendingEvent.Choices[2]));
            }
        }

        [Test]
        public void Writer_RoundTrips_AndMatchesTheFileOnDisk()
        {
            string text = EventCatalog.JsonSource().Replace("\r\n", "\n");
            string written = EventScriptWriter.Write(EventScriptReader.Read(text));
            Assert.AreEqual(text, written, "events.json 不是编辑器的标准格式——用外部事件编辑器打开并保存，或运行 EventEditor.exe --reformat");
            Assert.AreEqual(written, EventScriptWriter.Write(EventScriptReader.Read(written)));
        }

        [Test]
        public void Compile_CarriesMaxDay_AndTriggerChance()
        {
            var plain = EventScript.Compile(new EventEntry { id = "a", title = "t", body = "b" });
            Assert.AreEqual(-1, plain.MaxDay);
            Assert.AreEqual(1.0, plain.TriggerChance);
            var limited = EventScript.Compile(new EventEntry { id = "b", title = "t", body = "b", maxDay = 30, triggerChance = 0.25 });
            Assert.AreEqual(30, limited.MaxDay);
            Assert.AreEqual(0.25, limited.TriggerChance, 1e-9);
            var bad = EventScript.Validate(EventScriptReader.Read("{\"events\":[{\"id\":\"x\",\"title\":\"a\",\"body\":\"b\",\"minDay\":9,\"maxDay\":3,\"triggerChance\":1.5,\"choices\":[{\"text\":\"t\"}]}]}"), new string[0]);
            Assert.IsTrue(bad.Any(e => e.Contains("maxDay")) && bad.Any(e => e.Contains("triggerChance")), string.Join("\n", bad));
        }

        [Test]
        public void ClassmateHelp_AfterThreeNegativeDays_LendsFiveThousandWithoutInterest()
        {
            var def = EventCatalog.Get("classmate_help");
            Assert.AreEqual(30, def.MaxDay);
            using (var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(PCShopSell.Tests.GameSessionTests.Layout()), 3, Money.FromYuan(1000), PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(0), 600), true))
            {
                Assert.IsFalse(def.When(s), "账户是正的，不该触发");
                s.Ledger.ForceDebit(s.Clock.Today, "测试", Money.FromYuan(3000), "透支");
                Assert.IsFalse(def.When(s), "刚透支，还没连续 3 天");
                long target = s.Clock.Now.TotalMinutes + 3 * 1440L;
                for (int i = 0; i < 100000 && s.Clock.Now.TotalMinutes < target; i++) s.AdvanceClock((int)System.Math.Min(1440, target - s.Clock.Now.TotalMinutes));
                Assert.GreaterOrEqual(s.NegativeCashDays, 3);
                Assert.IsTrue(s.Ledger.Balance < Money.Zero);
                Assert.IsTrue(def.When(s));

                Money before = s.Ledger.Balance;
                s.RaiseEvent("classmate_help");
                Assert.IsTrue(s.ResolveEvent(0));
                Assert.AreEqual(before + Money.FromYuan(5000), s.Ledger.Balance);
                var loan = s.AppLoans.Single(l => l.NoInterest);
                Assert.AreEqual("res_coder", loan.Lender);
                Assert.AreEqual(Money.FromYuan(5000), loan.TotalDue, "无息：到期只还本金");
            }
        }

        [Test]
        public void WeekendWorkPaysOnlyAfterElapsedShiftEvenAfterReload()
        {
            var city = CityLayout.FromData(GameSessionTests.Layout());
            var start = GameTimestamp.From(new GameDate(5), 11 * 60);
            using (var session = new GameSession(city, 3, Money.FromYuan(30000), start, true))
            {
                session.RaiseEvent("money_parttime_offer");
                Assert.IsTrue(session.ResolveEvent(0));
                Assert.AreEqual(start, session.Clock.Now);
                Assert.IsTrue(session.IsWeekendWorking);
                Assert.IsFalse(session.Ledger.Entries.Any(e => e.Memo == "周末搬货兼职"));
                using (var restored = GameSession.Load(city, session.CreateSave()))
                {
                    Assert.IsTrue(restored.IsWeekendWorking);
                    Assert.AreEqual(GameSession.WeekendWorkHours * 60, restored.WorkMinutesLeft);
                    restored.AdvanceClock(restored.WorkMinutesLeft - 1);
                    Assert.IsFalse(restored.Ledger.Entries.Any(e => e.Memo == "周末搬货兼职"));
                    restored.AdvanceClock(1);
                    Assert.IsFalse(restored.IsWeekendWorking);
                    restored.AdvanceClock(1);
                    Assert.AreEqual(1, restored.Ledger.Entries.Count(e => e.Memo == "周末搬货兼职"));
                }
            }
        }

        [Test]
        public void EveryJsonEvent_EveryChoice_RunsWithoutErrors()
        {
            var failures = new System.Collections.Generic.List<string>();
            foreach (var def in EventCatalog.All.Where(e => e.Group != null && e.Group != "npc" || e.Id.StartsWith("classmate")).ToList())
            {
                using (var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(PCShopSell.Tests.GameSessionTests.Layout()), 3, Money.FromYuan(30000), PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(12), 600), true))
                {
                    for (int i = 0; i < def.Choices.Count; i++)
                    {
                        try
                        {
                            s.RaiseEvent(def.Id);
                            s.ResolveEvent(i);
                        }
                        catch (System.Exception e) { failures.Add(def.Id + " 选项" + (i + 1) + "：" + e.GetType().Name + " " + e.Message); break; }
                    }
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EventCatalog_Has_AtLeastOneHundredJsonEvents()
        {
            Assert.GreaterOrEqual(EventCatalog.All.Count(e => !e.Id.StartsWith("zzz")), 120);
            CollectionAssert.AllItemsAreUnique(EventCatalog.All.Select(e => e.Id).ToList());
        }

        [Test]
        public void RandomEvents_RespectDayAndWeekCaps_AndStartSlowly()
        {
            using (var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(PCShopSell.Tests.GameSessionTests.Layout()), 21, Money.FromYuan(500000), PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(0), 600), true))
            {
                s.DebugRentStarterShop(true);
                s.EventsEnabled = true;
                var days = new System.Collections.Generic.List<int>();
                s.EventRaised += def => days.Add(s.Clock.Today.TotalDays);
                long target = s.Clock.Now.TotalMinutes + 140 * 1440L;
                for (int i = 0; i < 500000 && s.Clock.Now.TotalMinutes < target; i++)
                {
                    if (s.PendingEvent != null) s.ResolveEvent(s.PendingEvent.Choices.Count - 1);
                    s.AdvanceClock((int)System.Math.Min(60, target - s.Clock.Now.TotalMinutes));
                }
                string log = string.Join(",", days);
                foreach (int d in days.Distinct())
                    Assert.LessOrEqual(days.Count(x => x == d), GameSession.EventDayCap, "一天超过上限：" + log);
                foreach (int d in days)
                    Assert.LessOrEqual(days.Count(x => x >= d && x < d + 7), GameSession.EventWeekCap, "7 天内超过上限：" + log);
                Assert.GreaterOrEqual(days.Count, 40, "140 天里随机事件太少：" + log);
            }
        }
        [Test]
        public void RandomEvents_FirstFourteenDays_AreRoughlyHalf()
        {
            int early = 0, later = 0;
            for (ulong seed = 31; seed < 41; seed++)
            {
                using (var s = new GameSession(PCShopSell.Core.World.CityLayout.FromData(PCShopSell.Tests.GameSessionTests.Layout()), seed, Money.FromYuan(500000), PCShopSell.Core.Time.GameTimestamp.From(new PCShopSell.Core.Time.GameDate(0), 600), true))
                {
                    s.DebugRentStarterShop(true);
                    s.EventsEnabled = true;
                    s.EventRaised += def => { if (s.Clock.Today.TotalDays < GameSession.EventEarlyDays) early++; else later++; };
                    long target = s.Clock.Now.TotalMinutes + 28 * 1440L;
                    for (int i = 0; i < 200000 && s.Clock.Now.TotalMinutes < target; i++)
                    {
                        if (s.PendingEvent != null) s.ResolveEvent(s.PendingEvent.Choices.Count - 1);
                        s.AdvanceClock((int)System.Math.Min(60, target - s.Clock.Now.TotalMinutes));
                    }
                }
            }
            Assert.Less(early, later, "前 14 天应该比后 14 天少：前 " + early + " 个，后 " + later + " 个");
            Assert.Greater(later, 0);
        }
    }
}
