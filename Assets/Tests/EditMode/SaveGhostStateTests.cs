using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    /// <summary>
    /// Unity JsonUtility writes a null [Serializable] class field as a default object, so on load
    /// "没有这条状态"必须还是没有，不能变成凭空出现的空对象。
    /// </summary>
    public sealed class SaveGhostStateTests
    {
        private static GameSession Make()
        {
            using var f = DistrictProgressionTests.Make();
            var s = new GameSession(f.City, 1121, Money.FromYuan(500000), GameTimestamp.From(new GameDate(42), 660), false, true, true);
            s.EventsEnabled = false; s.Tick(0); s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; return s;
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static bool HasLinLoan(GameSession s) => (bool)typeof(GameSession).GetProperty("HasLinLoan", Any).GetValue(s);

        [Test]
        public void NoScratchTicket_StaysNoTicketAfterReload_AndTicketCanBeBought()
        {
            using var s = Make();
            Assert.IsNull(s.ScratchTicket);
            using var loaded = Reload(s);
            Assert.IsNull(loaded.ScratchTicket, "读档不能凭空多出一张空刮刮乐");
            Assert.IsTrue(loaded.BuyEventScratchTicket(), "没有票时读档后应能正常购买");
        }

        [Test]
        public void NoLinLoan_StaysNoLoanAfterReload()
        {
            using var s = Make();
            Assert.IsFalse(HasLinLoan(s));
            using var loaded = Reload(s);
            Assert.IsFalse(HasLinLoan(loaded), "读档不能凭空多出林远航借款");
        }

        [Test]
        public void RealLinLoan_SurvivesReload()
        {
            using var s = Make();
            var state = new GameSession.LinLoanState { DueDay = s.Clock.Today.TotalDays + 25, Chance = 0.5 };
            typeof(GameSession).GetField("_linLoan", Any).SetValue(s, state);
            using var loaded = Reload(s);
            Assert.IsTrue(HasLinLoan(loaded));
        }

        [Test]
        public void RealScratchTicket_SurvivesReload()
        {
            using var s = Make();
            Assert.IsTrue(s.BuyEventScratchTicket());
            int id = s.ScratchTicket.Id;
            using var loaded = Reload(s);
            Assert.IsNotNull(loaded.ScratchTicket);
            Assert.AreEqual(id, loaded.ScratchTicket.Id);
        }
    }
}
