using System.Reflection;
using System.Linq;
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
        [Test]
        public void JsonReloadKeepsOnDutyStaffWorkingWithoutInventingAVisit()
        {
            using var s = Make();
            var venue = s.City.GetPlace(GameSession.FishingVenueId);
            var staff = s.CityPeople.First(p => s.IsOnDutyVenueNpc(venue,p));
            Assert.IsNull(staff.VisitId);
            using var loaded = Reload(s);
            var restored = loaded.CityPersonState(staff.PersonId);
            Assert.IsNull(restored.VisitId);
            Assert.IsNull(restored.ActivityId);
            Assert.AreEqual(staff.LocationPlaceId,restored.LocationPlaceId);
            Assert.AreEqual(staff.Task,restored.Task);
            Assert.True(loaded.IsPlaceOpen(venue),"读档不能让原本在岗的钓场店员变成返程状态");
        }
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [Test]
        public void EmptyMarketFindStaysEmptyAndRealUnclaimedFindSurvivesJson()
        {
            using var s = Make();
            Assert.IsNull(s.PendingMarketFind);
            using (var empty = Reload(s)) Assert.IsNull(empty.PendingMarketFind);
            var data=s.CreateSave();
            data.oldMarketHunt.Pending=(UsedItem)typeof(OldMarketHuntCatalog)
                .GetMethod("Draw",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{OldMarketHuntCatalog.Spot("watch-box"),0,500,0,1});
            using var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)));
            Assert.NotNull(loaded.PendingMarketFind);
            Assert.AreEqual(data.oldMarketHunt.Pending.Id,loaded.PendingMarketFind.Id);
            Assert.AreEqual(data.oldMarketHunt.Pending.MarketModelName,loaded.PendingMarketFind.MarketModelName);
        }
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
