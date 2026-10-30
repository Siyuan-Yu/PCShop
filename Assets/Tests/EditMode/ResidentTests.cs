using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    /// <summary>街坊的作息、人脉、顾客点名要的型号。</summary>
    public class ResidentTests
    {
        [Test]
        public void Residents_AllHaveUniqueIdsAndStories()
        {
            Assert.AreEqual(Residents.All.Length, Residents.All.Select(r => r.Id).Distinct().Count());
            foreach (ResidentDef r in Residents.All)
            {
                Assert.IsNotNull(Rosters.StoriesOf(r.Id), r.Id);
                Assert.AreEqual(0, r.Outings.Length % 2, r.Id);
            }
        }

        [Test]
        public void Resident_CommutesBeforeAndAfterWork()
        {
            ResidentDef guide = Residents.Get("keeper_mall");
            Assert.AreEqual(ResidentTrip.ToWork, Residents.TripAt(guide, guide.WorkStart - 10));
            Assert.IsNull(Residents.TripAt(guide, guide.WorkStart + 60));
            Assert.IsTrue(Residents.AtWork(guide, guide.WorkStart + 60));
            Assert.AreEqual(ResidentTrip.ToHome, Residents.TripAt(guide, guide.WorkEnd + 5));
            Assert.IsNull(Residents.TripAt(guide, 3 * 60), "半夜在家");
        }

        [Test]
        public void Street_IsBusierByDayThanAtNight()
        {
            using (GameSession s = GameSessionTests.Make(5000))
            {
                s.Clock.Restore(s.Clock.Today, 10 * 60);
                int day = s.ResidentsOutside().Count;
                s.Clock.Restore(s.Clock.Today, 3 * 60);
                int night = s.ResidentsOutside().Count;
                Assert.Greater(day, 2);
                Assert.AreEqual(0, night);
            }
        }

        [Test]
        public void Contacts_ListPeopleYouTalkedTo()
        {
            using (GameSession s = GameSessionTests.Make(5000))
            {
                Assert.IsFalse(s.Contacts().Any(c => c.Id == "res_wang"));
                Assert.IsNotNull(s.StaffChat(Residents.Get("res_wang").Person));
                ContactInfo c = s.Contacts().Single(x => x.Id == "res_wang");
                Assert.AreEqual(ContactGroup.Neighbors, c.Group, "测试地图里没有修鞋铺，算街坊");
                Assert.Greater(c.Level, 0);
            }
        }

        [Test]
        public void SocialTies_ReferToRealPeople()
        {
            foreach (string[] t in SocialTies.All)
                foreach (string id in new[] { t[0], t[1] })
                    Assert.IsTrue(Npcs.Get(id) != null || Rosters.Get(id) != null || Residents.Get(id) != null, id);
        }
    }
}
