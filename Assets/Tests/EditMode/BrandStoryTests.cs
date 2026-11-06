using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.Finance;

namespace PCShopSell.Tests
{
    public sealed class BrandStoryTests
    {
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static string Product => ClothingProducts.All.First().Id;
        private static GameSession Make()
        {
            var s = DistrictProgressionTests.Make(); s.EventsEnabled = false;
            s.DebugPrepareDistrictPrerequisites(1); s.DebugAddCash(1000000);
            // These story cases start after the business introduction; unmet-contact
            // rejection is covered by McnIntroductionTests.
            foreach (string person in GameSession.BrandPeople) s.Meet(person, 0);
            typeof(GameSession).GetField("_district", Private).SetValue(s, new DistrictProgress { UnlockedTier = 3, Chapter = 1 });
            var shop = s.MyShops.First(); shop.RetailKind = "clothing"; shop.Sim.Stock.ForceAddGoods(Product, 100);
            typeof(GameSession).GetField("_brands", Private).SetValue(s, new BrandBusinessState {
                Brands = new[] { new BrandRecord { Id = "b", Name = "棉风", Kind = "clothing", Position = "quality", ShopIds = new[] { shop.Place.Id } } }
            });
            At(s, shop.Place.Id); return s;
        }
        private static string Shop(GameSession s) => s.BrandBusiness.Brands[0].ShopIds[0];
        private static void At(GameSession s, string place) => typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace(place));
        private static void MeetHere(GameSession s, string person)
        {
            At(s, Shop(s)); var p = s.CityPersonState(person); p.LocationPlaceId = Shop(s); p.Route = null;
        }
        private static void Day(GameSession s, int day, int minute = 600) => s.Clock.Restore(new GameDate(day), minute);
        private static string BeginDeal(GameSession s, string person)
        {
            Assert.True(McnSigningTest.Endorse(s,"b", person, "endorse", false)); MeetHere(s, person);
            string id = s.BrandStoryOffers().Single(x => x.PersonId == person).Id; s.BeginBrandStory(id);
            Assert.AreEqual(1, s.BrandStories.Single(x => x.Id == id).Stage); return id;
        }
        private static string Register(GameSession s)
        {
            // An explicitly seeded pre-upgrade save tests continued story compatibility.
            var state=s.BrandBusiness;var e=new BrandExhibit{Id="b:legacy:75",BrandId="b",ShopId=Shop(s),ProductId=Product,Focus="buyers",Day=s.NextBrandExpoDay};
            state.Exhibits=new[]{e};typeof(GameSession).GetField("_brands",Private).SetValue(s,state);return e.Id;
        }
        private static void Expo(GameSession s)
        {
            var e = s.BrandBusiness.Exhibits.Single(); Day(s, e.Day); At(s, "life_expo");
            for (int n = 0; n < 3; n++) { Day(s, e.Day, 600 + n * 30); Assert.True(s.ActBrandExpo(e.Id, "design")); }
        }
        private static void Deliver(GameSession s)
        {
            var order = s.BrandBusiness.Orders.Single(); Assert.True(s.FulfilBrandOrder(order.Id));
            order = s.BrandBusiness.Orders.Single(); Day(s, (int)(order.Arrival / 1440), (int)(order.Arrival % 1440));
            typeof(GameSession).GetMethod("TickBrandDeliveries", Private).Invoke(s, new object[] { s.Clock.Now.TotalMinutes });
            Assert.True(s.BrandBusiness.Orders.Single().Done);
        }
        [Test] public void ReadingOffersAndProfilesDoesNotStartStoryOrRevealConclusion()
        {
            using (var s = Make())
            {
                s.SignBrandDeal("b", "momo", "campaign", false); var first = s.BrandStoryOffers().ToArray();
                Assert.AreEqual(1, first.Length); first[0].Stage = 3;
                Assert.AreEqual(0, s.BrandStories.Count); Assert.AreEqual(0, s.BrandStoryOffers().Single().Stage);
                Assert.False(s.LearnedPersonStories("momo").Any(t => t.Contains("视频带来的")));
                s.Flags.Remove(GameSession.MetFlag("momo"));
                s.BeginBrandStory(first[0].Id, true); Assert.AreEqual(0, s.BrandStories.Count, "No contact bypass");
                MeetHere(s, "momo"); s.BeginBrandStory(first[0].Id);
                Assert.True(s.HasGreeted("momo")); Assert.False(s.IsMet("momo"), "Talking does not exchange contact details");
            }
        }
        [Test] public void PreparationIsLocalConsumesSelectedStockOnceAndDoesNotPayProfit()
        {
            using (var s = Make())
            {
                string id = BeginDeal(s, "momo"); int stock = s.ShopAt(Shop(s)).Sim.Stock.GoodsCount(Product); long cash = s.Ledger.Balance.Cents;
                At(s, "agency"); Assert.False(s.PrepareBrandStory(id, Shop(s), Product, "value"));
                At(s, Shop(s)); Assert.False(s.PrepareBrandStory(id, Shop(s), Product, "fake"));
                Assert.True(s.PrepareBrandStory(id, Shop(s), Product, "value")); Assert.False(s.PrepareBrandStory(id, Shop(s), Product, "value"));
                Assert.AreEqual(stock - 1, s.ShopAt(Shop(s)).Sim.Stock.GoodsCount(Product)); Assert.AreEqual(cash, s.Ledger.Balance.Cents);
                Assert.NotNull(s.BrandStoryFinishBlocker(id));
            }
        }
        [Test] public void PromoFollowupUsesOnlySelectedShopRevenueAndCompletesOnceAfterSave()
        {
            using (var s = Make())
            {
                string id = BeginDeal(s, "momo"); Assert.True(s.PrepareBrandStory(id, Shop(s), Product, "service"));
                s.Ledger.Credit(s.Clock.Today, "零售", Money.FromYuan(123), "fixture", LedgerAttribution.ForShop(Shop(s)));
                s.Ledger.Credit(s.Clock.Today, "借款", Money.FromYuan(9999), "not sales", LedgerAttribution.ForShop(Shop(s)));
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Day(loaded, loaded.BrandStories.Single().PreparedDay + 3); MeetHere(loaded, "momo");
                    long money = loaded.Ledger.Balance.Cents; string text = loaded.FinishBrandStory(id);
                    StringAssert.Contains(Money.FromYuan(123).ToString(), text); StringAssert.Contains("不能都算成视频带来的", text);
                    int relation = loaded.Relation("momo"); loaded.FinishBrandStory(id); Assert.AreEqual(relation, loaded.Relation("momo"));
                    Assert.AreEqual(money, loaded.Ledger.Balance.Cents); Assert.True(loaded.BrandStories.Single().Fulfilled);
                    Assert.Contains(text, loaded.LearnedPersonStories("momo")); Assert.IsEmpty(loaded.BrandStoryOffers());
                }
            }
        }
        [Test] public void ZeroSalesIsAnHonestCompleteOutcome()
        {
            using (var s = Make())
            {
                string id = BeginDeal(s, "momo"); s.PrepareBrandStory(id, Shop(s), Product, "value");
                Day(s, s.Clock.Today.TotalDays + 3); MeetHere(s, "momo");
                StringAssert.Contains("暂时没有成交", s.FinishBrandStory(id)); Assert.True(s.BrandStories.Single().Fulfilled);
            }
        }
        [Test] public void RetestRequiresTwoSamplesAndARealDay()
        {
            using (var s = Make())
            {
                string id = BeginDeal(s, "qiao"); int count = s.ShopAt(Shop(s)).Sim.Stock.GoodsCount(Product);
                s.ShopAt(Shop(s)).Sim.Stock.TryRemoveGoods(Product, count - 1);
                Assert.False(s.PrepareBrandStory(id, Shop(s), Product, "retest")); Assert.AreEqual(1, s.BrandStories.Single().Stage);
                s.ShopAt(Shop(s)).Sim.Stock.ForceAddGoods(Product, 1); Assert.True(s.PrepareBrandStory(id, Shop(s), Product, "retest"));
                Assert.NotNull(s.BrandStoryFinishBlocker(id)); Day(s, s.Clock.Today.TotalDays + 1); MeetHere(s, "qiao");
                StringAssert.Contains("补测", s.FinishBrandStory(id)); Assert.True(s.BrandStories.Single().Fulfilled);
            }
        }
        [Test] public void EndingContractDoesNotFakeFinishedRetestOrAwardRelationship()
        {
            using (var s = Make())
            {
                string id = BeginDeal(s, "qiao"); s.PrepareBrandStory(id, Shop(s), Product, "retest");
                Assert.True(s.RespondBrandDeal(s.BrandStories.Single().SourceId, "end")); int relation = s.Relation("qiao");
                s.FinishBrandStory(id); Assert.AreEqual(3, s.BrandStories.Single().Stage);
                Assert.False(s.BrandStories.Single().Fulfilled); Assert.AreEqual(relation, s.Relation("qiao"));
            }
        }
        [Test] public void ExhibitStoryUsesRegisteredGoodsAndWaitsForActualDelivery()
        {
            using (var s = Make())
            {
                Register(s); MeetHere(s, "life_designer"); string id = s.BrandStoryOffers().Single(x => x.Kind == "design").Id;
                s.BeginBrandStory(id); int count = s.ShopAt(Shop(s)).Sim.Stock.GoodsCount(Product);
                Assert.True(s.PrepareBrandStory(id, Shop(s), Product, "design")); Assert.AreEqual(count, s.ShopAt(Shop(s)).Sim.Stock.GoodsCount(Product));
                Assert.AreEqual("design", s.BrandBusiness.Exhibits.Single().Focus); Expo(s); Assert.NotNull(s.BrandStoryFinishBlocker(id));
                Deliver(s); MeetHere(s, "life_designer"); StringAssert.Contains("实际送达", s.FinishBrandStory(id)); Assert.True(s.BrandStories.Single().Fulfilled);
            }
        }
        [Test] public void CuratorStoryNeedsRecordedFulfilmentAndRealConversationWithDesigner()
        {
            using (var s = Make())
            {
                Register(s); MeetHere(s, "life_curator"); string id = s.BrandStoryOffers().Single(x => x.Kind == "curator").Id;
                s.BeginBrandStory(id); Assert.NotNull(s.ShareBrandExpoBlocker(id)); Expo(s); Deliver(s);
                At(s, "agency"); Assert.NotNull(s.ShareBrandExpoBlocker(id), "Not at the same place as 白明");
                MeetHere(s, "resident_baiming"); StringAssert.Contains("白明", s.ShareBrandExpoStory(id, "share"));
                MeetHere(s, "life_curator"); s.FinishBrandStory(id); Assert.True(s.BrandStories.Single().Fulfilled);
            }
        }
        [Test] public void MissedExhibitCanCloseWithoutRewardOrMutatingOrders()
        {
            using (var s = Make())
            {
                Register(s); MeetHere(s, "life_curator"); string id = s.BrandStoryOffers().Single(x => x.Kind == "curator").Id; s.BeginBrandStory(id);
                Day(s, s.BrandBusiness.Exhibits.Single().Day + 1); MeetHere(s, "life_curator"); int relation = s.Relation("life_curator");
                s.FinishBrandStory(id); Assert.AreEqual(3, s.BrandStories.Single().Stage); Assert.False(s.BrandStories.Single().Fulfilled);
                Assert.AreEqual(relation, s.Relation("life_curator")); Assert.IsEmpty(s.BrandBusiness.Orders);
            }
        }
        [Test] public void StoryCopiesDoNotMutateLiveProgressAndOldSaveGetsEmptyRecords()
        {
            using (var s = Make())
            {
                BeginDeal(s, "momo"); s.BrandStories.Single().Stage = 3; Assert.AreEqual(1, s.BrandStories.Single().Stage);
                var save = s.CreateSave(); save.brandStories = null;
                using (var loaded = GameSession.Load(s.City, save)) Assert.IsEmpty(loaded.BrandStories);
            }
        }
        [Test] public void RoutineCareerUpdatesDoNotInventRecurringMisconduct()
        {
            using (var s = Make())
            {
                for (int day = 42; day <= 147; day++)
                {
                    Day(s, day); typeof(GameSession).GetMethod("TickBrandBusiness", Private).Invoke(s, new object[] { new GameDate(day - 1), new GameDate(day) });
                    foreach (var c in s.BrandBusiness.Careers) { Assert.Zero(c.Severity); StringAssert.DoesNotContain("争议已有证据", c.News); }
                }
                Assert.Greater(s.BrandBusiness.Careers.Select(c => c.News).Distinct().Count(), 3);
            }
        }
        [Test] public void FormalNamesAreConsistentAcrossCataloguesAndPreserveAllStableIds()
        {
            Assert.AreEqual(40, NpcNames.Familiar.Count);
            foreach (var pair in NpcNames.Familiar)
            {
                var p = CityLifeCatalog.Person(pair.Key); Assert.NotNull(p, pair.Key); Assert.AreNotEqual(pair.Value, p.Name);
                Assert.That(p.Name.Length, Is.InRange(2, 4)); Assert.AreEqual(pair.Value, NpcNames.Alias(pair.Key));
                var staff = Rosters.Get(pair.Key) ?? Residents.Get(pair.Key)?.Person;
                if (staff != null) Assert.AreEqual(p.Name, staff.Name);
                var npc = Npcs.All.FirstOrDefault(n => n.Id == pair.Key); if (npc != null) Assert.AreEqual(p.Name, npc.Name);
                var creator = Creators.Get(pair.Key); if (creator != null) Assert.AreEqual(p.Name, creator.Name);
            }
            Assert.AreEqual("陈启明", CityLifeCatalog.Person("keeper_agent").Name);
            Assert.AreEqual("莫文宁", Creators.Get("momo").Name);
            Assert.AreEqual(124, CityLifeCatalog.People.Count(p => p.Id != CityLifeCatalog.InvitationTestPersonId));
        }
        [Test] public void OldNamedPersonRetainsContactRelationshipAndBiographyAfterLoad()
        {
            using (var s = Make())
            {
                s.Meet("keeper_agent", 37); s.CityPersonState("keeper_agent").StoryChapter = 2;
                string address = s.CityPersonState("keeper_agent").HomePlaceId;
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())); save.brandStories = null;
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Assert.AreEqual("陈启明", loaded.CityPerson("keeper_agent").Name);
                    Assert.AreEqual(s.Relation("keeper_agent"), loaded.Relation("keeper_agent")); Assert.True(loaded.IsMet("keeper_agent"));
                    Assert.AreEqual(2, loaded.CityPersonState("keeper_agent").StoryChapter); Assert.AreEqual(address, loaded.CityPersonState("keeper_agent").HomePlaceId);
                }
            }
        }
        [Test] public void EmployeeNamesMigrateWithoutChangingPersonIdentityOrInventingNewPerson()
        {
            using (var s = Make())
            {
                var fixedPerson = new Employee { PersonId = "keeper_agent", Name = "小陈" };
                Assert.AreEqual("keeper_agent", s.PersonIdOf(fixedPerson)); Assert.AreEqual("陈启明", fixedPerson.Name);
                var generated = new Employee { PersonId = "employee_legacy", Name = "王阿强" };
                Assert.AreEqual("employee_legacy", s.PersonIdOf(generated)); Assert.AreEqual("王志强", generated.Name);
                Assert.AreEqual("王志强", s.CityPerson("employee_legacy").Name); s.PersonIdOf(generated); Assert.AreEqual("王志强", generated.Name);
                var custom = new Employee { PersonId = "employee_custom", Name = "徐乐" }; s.PersonIdOf(custom); Assert.AreEqual("徐乐", custom.Name);
            }
        }
        [Test] public void OtherBrandOrOtherExhibitCannotCompleteBoundStory()
        {
            using (var s = Make())
            {
                string exhibit = Register(s); MeetHere(s, "life_designer"); var offer = s.BrandStoryOffers().Single(x => x.Kind == "design"); s.BeginBrandStory(offer.Id);
                s.PrepareBrandStory(offer.Id, Shop(s), Product, "buyers");
                var state = s.BrandBusiness;
                state.Orders = new[] { new BrandOrder { Id = exhibit + ":lead", BrandId = "another", Done = true }, new BrandOrder { Id = "other-expo:lead", BrandId = "b", Done = true } };
                typeof(GameSession).GetField("_brands", Private).SetValue(s, state);
                Assert.NotNull(s.BrandStoryFinishBlocker(offer.Id)); Assert.AreEqual(2, s.BrandStories.Single().Stage);
            }
        }
        [Test] public void LateDesignPreparationCannotChangeAnExhibitAlreadyRunning()
        {
            using (var s = Make())
            {
                string exhibit = Register(s); MeetHere(s, "life_designer"); var offer = s.BrandStoryOffers().Single(x => x.Kind == "design"); s.BeginBrandStory(offer.Id);
                Day(s, s.BrandBusiness.Exhibits.Single().Day); At(s, "life_expo"); Assert.True(s.ActBrandExpo(exhibit, "buyers"));
                At(s, Shop(s)); Assert.False(s.PrepareBrandStory(offer.Id, Shop(s), Product, "design")); Assert.AreEqual("buyers", s.BrandBusiness.Exhibits.Single().Focus);
            }
        }
    }
}
