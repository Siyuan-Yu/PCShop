using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public sealed class CompetitionContextTests
    {
        private static GameSession Make(string kind="clothing",bool fullCity=false)
        {
            var data=fullCity?JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text):GameSessionTests.Layout();
            var s=new GameSession(CityLayout.FromData(data),71,Money.FromYuan(300000),GameTimestamp.From(new GameDate(0),11*60),false);
            s.EventsEnabled=false;GameSessionTests.Teleport(s,s.MainShop.Place.Id);
            Assert.IsTrue(s.SetShopMainCategory(kind));
            s.PlayerShop.SetHours(0,1440);s.PlayerShop.ForcedClosed=false;s.PlayerShop.NotFitted=false;s.PlayerShop.Markup=1.4;
            return s;
        }
        [Test]
        public void ComputerStoryBindsToTheComputerBranchWithoutTakingClothingShopStock()
        {
            using(var s=Make())
            {
                var clothing=s.CurrentShop;
                s.PlayerShop.Reputation=80;
                s.DebugAddListing("tea",0,60,Money.FromYuan(3000));GameSessionTests.Teleport(s,"agency");
                Assert.True(s.SignRetailLease("tea",true,"computer"));
                var computer=s.ShopAt("tea");computer.Sim.Stock.Clear();
                computer.Employee=new Employee{Id="branch-clerk",Name="分店收银员"};CityTestPeople.AtAssignedWork(s,computer.Employee);
                computer.Sim.SetHours(0,1440);computer.Sim.NotFitted=false;computer.Sim.ForcedClosed=false;computer.Sim.Reputation=10;
                var ram=PartCatalog.Get("ram0");computer.Sim.Stock.ForceAdd(ram,true,5);
                GameSessionTests.Teleport(s,clothing.Place.Id);var active=s.CurrentShop;
                computer.Sim.NotFitted=false;computer.Sim.ForcedClosed=false;
                computer.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"memory",60}});
                int unchanged=clothing.Sim.Stock.CountAll(ram);
                s.RaiseEvent("students");Assert.NotNull(s.PendingEvent);StringAssert.Contains(s.ShopTitle(computer),s.PendingEvent.Body);
                Assert.True(s.ResolveEvent(0));Assert.Zero(computer.Sim.Stock.CountAll(ram));
                Assert.AreEqual(unchanged,clothing.Sim.Stock.CountAll(ram));Assert.AreSame(active,s.CurrentShop);
            }
        }
        [Test]
        public void ComputerStoriesDoNotTargetClothingEvenWhenPartsRemainInTheWarehouse()
        {
            using(var s=Make())
            {
                s.PlayerShop.Reputation=10;s.PlayerShop.Stock.Clear();
                s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"),true,10);
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{"memory",60}});
                foreach(string id in new[]{"regulars","students","review"})
                {
                    Assert.False(EventCatalog.Get(id).When(s));s.RaiseEvent(id);
                    Assert.IsNull(s.PendingEvent);Assert.Zero(s.EventCountOf(id));
                }
            }
        }
        [TestCase("computer","配件")]
        [TestCase("clothing","面料")]
        [TestCase("models","正版")]
        public void ReviewUsesTheActualRetailIndustryAndCancelsIfItChanges(string kind,string word)
        {
            using(var s=Make(kind))
            {
                s.PlayerShop.Reputation=10;s.PlayerShop.Stock.Clear();
                string shelf;
                if(kind=="computer")
                {
                    s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,2);shelf="cpu";
                }
                else
                {
                    var product=Gifts.All.First(g=>RetailShopTypes.Sells(kind,g.Id));
                    s.PlayerShop.Stock.ForceAddGoods(product.Id,2);shelf=ShelfKinds.ForGift(product.Id).Id;
                }
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{shelf,60}});
                Assert.True(EventCatalog.Get("review").When(s));s.RaiseEvent("review");
                StringAssert.Contains(word,s.PendingEvent.Body);StringAssert.Contains(s.ShopTitle(s.CurrentShop),s.PendingEvent.Body);
                if(kind!="computer"){StringAssert.DoesNotContain("配件",s.PendingEvent.Body);StringAssert.DoesNotContain("翻新",s.PendingEvent.Title);}
                var cash=s.Ledger.Balance;double reputation=s.PlayerShop.Reputation;
                Assert.True(s.SetShopMainCategory(kind=="clothing"?"models":"clothing"));
                Assert.True(s.ResolveEvent(1));Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(reputation,s.PlayerShop.Reputation);
            }
        }
        [Test]
        public void ReviewRequiresOpenShopAndRelatedGoodsOnDisplayAndDoesNotMutateTheCatalog()
        {
            using(var s=Make("clothing"))
            {
                s.PlayerShop.Reputation=10;s.PlayerShop.Stock.Clear();
                var product=Gifts.All.First(g=>ClothingProducts.IsClothing(g.Id));
                s.PlayerShop.Stock.ForceAddGoods(product.Id,2);
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>());
                Assert.False(EventCatalog.Get("review").When(s));
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>{{ShelfKinds.ForGift(product.Id).Id,60}});
                s.PlayerShop.ForcedClosed=true;Assert.False(EventCatalog.Get("review").When(s));
                s.PlayerShop.ForcedClosed=false;s.RaiseEvent("review");Assert.NotNull(s.PendingEvent);
                StringAssert.Contains("配件",EventCatalog.Get("review").Body,"共享模板不能被上一次服装事件污染");
                var cash=s.Ledger.Balance;Assert.True(s.ResolveEvent(1));Assert.AreEqual(cash-Money.FromYuan(100),s.Ledger.Balance);
            }
        }
        [Test]
        public void ClosedShopCannotTriggerPriceWarOrConsumeTheCompetitionRound()
        {
            using(var s=Make("computer"))
            {
                s.PlayerShop.ForcedClosed=true;
                Assert.IsFalse(EventCatalog.Get("rival").When(s));
                s.RaiseEvent("rival");Assert.IsNull(s.PendingEvent);Assert.AreEqual(0,s.EventCountOf("rival"));
                s.Clock.Restore(new GameDate(14),11*60);s.TickCityRetail();Assert.AreEqual(0,s.CityCompetitions.Count);
                s.PlayerShop.ForcedClosed=false;s.TickCityRetail();
                Assert.AreEqual(1,s.CityCompetitions.Count,"同一天真正开门后仍可生成这一轮事件");
                s.TickCityRetail();Assert.AreEqual(1,s.CityCompetitions.Count);
            }
        }
        [Test]
        public void ClothingPriceWarUsesAClothingBrandAndIgnoresComputerFriendshipPerks()
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.ForceAddGoods("clothing:jacket",2);
                s.Meet(Npcs.Jie,100);s.AddRelation(Npcs.Jie,100);
                Assert.IsTrue(EventCatalog.Get("rival").When(s));s.RaiseEvent("rival");
                Assert.IsNotNull(s.PendingEvent);StringAssert.Contains("服装",s.PendingEvent.Title);
                StringAssert.Contains("远迹服饰",s.PendingEvent.Body);StringAssert.DoesNotContain("电脑",s.PendingEvent.Body);
                var before=s.PlayerShop.Markup;Assert.IsTrue(s.ResolveEvent(0));
                Assert.AreEqual(before-.1,s.PlayerShop.Markup,1e-9);Assert.IsFalse(s.ResolveEvent(0));
            }
        }
        [Test]
        public void ChangingTheMainCategoryInvalidatesAnAlreadyRaisedPriceWar()
        {
            using(var s=Make())
            {
                s.RaiseEvent("rival");Assert.IsNotNull(s.PendingEvent);
                Assert.IsTrue(s.SetShopMainCategory("models"));double markup=s.PlayerShop.Markup;
                Assert.IsTrue(s.ResolveEvent(0));Assert.AreEqual(markup,s.PlayerShop.Markup);
            }
        }
        [Test]
        public void PriceWarResponseStaysBoundToTheOriginalOperatingShopAfterSwitchingFocus()
        {
            using(var s=Make("computer"))
            {
                s.PlayerShop.Reputation=80;s.PlayerShop.Markup=1.1;
                s.DebugAddListing("tea",0,60,Money.FromYuan(3000));GameSessionTests.Teleport(s,"agency");
                Assert.IsTrue(s.SignLease("tea",true));GameSessionTests.Teleport(s,"tea");Assert.IsTrue(s.SetShopMainCategory("clothing"));
                var target=s.CurrentShop;target.Sim.NotFitted=false;target.Sim.ForcedClosed=false;target.Sim.SetHours(0,1440);
                target.Sim.Markup=1.4;target.Sim.EmployeeOnDuty=_=>true;
                s.RaiseEvent("rival");Assert.IsNotNull(s.PendingEvent);StringAssert.Contains("服装",s.PendingEvent.Body);
                GameSessionTests.Teleport(s,"pc");var current=s.CurrentShop;double unchanged=current.Sim.Markup;
                Assert.IsTrue(s.ResolveEvent(0));Assert.AreSame(current,s.CurrentShop);
                Assert.AreEqual(1.3,target.Sim.Markup,1e-9);Assert.AreEqual(unchanged,current.Sim.Markup);
            }
        }
        [TestCase(21,"quota")]
        [TestCase(28,"terms")]
        public void ClothingMarketEventsUseActualClothingSuppliersAndSurviveSaving(int day,string kind)
        {
            using(var s=Make(fullCity:true))
            {
                s.Clock.Restore(new GameDate(day),11*60);s.TickCityRetail();var item=s.CityCompetitions.Single();
                Assert.AreEqual(kind,item.Kind);Assert.AreEqual("clothing",item.RetailKind);
                Assert.IsTrue(ClothingProducts.IsClothing(item.ProductId));Assert.AreEqual("clothing_wholesale",item.SupplierId);
                StringAssert.DoesNotContain("CPU",s.CityCompetitionTerms(item));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var restored=loaded.CityCompetitions.Single();Assert.AreEqual(item.RetailKind,restored.RetailKind);
                    Assert.AreEqual(item.ProductId,restored.ProductId);Assert.AreEqual(item.SupplierId,restored.SupplierId);
                }
            }
        }
        [Test]
        public void SwitchingIndustryClearsPromotionAndOldComputerEventsCannotPenalizeClothing()
        {
            using(var s=Make(fullCity:true))
            {
                s.Clock.Restore(new GameDate(14),11*60);s.TickCityRetail();Assert.Less(s.PlayerShop.CityRetailDemandMultiplier,1);
                Assert.IsTrue(s.SetShopMainCategory("models"));Assert.AreEqual(1,s.PlayerShop.CityRetailDemandMultiplier);
                Assert.IsTrue(s.CityCompetitions.Single().Ended);
                Assert.IsTrue(s.SetShopMainCategory("clothing"));
                s.LoadCityRetail(new SaveData{cityRetailVersion=1,cityCompetitionSlot=0,cityCompetitions=new[]{
                    new CityCompetition{Id="old-pc",Kind="promotion",ShopId=s.MainShop.Place.Id,ProductId="cpu0",StartDay=14,EndDay=17}}});
                Assert.IsTrue(s.CityCompetitions.Single().Ended);Assert.AreEqual(1,s.PlayerShop.CityRetailDemandMultiplier);
            }
        }
        [Test]
        public void LegacyClothingInferenceAlsoClearsAComputerPromotionBeforeTheFirstTick()
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.Clear();s.PlayerShop.Stock.ForceAddGoods("clothing:cotton_short",2);
                foreach(int shelf in s.ShelfIndexes())Assert.IsTrue(s.SetShelfKind(shelf,"clothing_0"));
                s.Clock.Restore(new GameDate(14),11*60);var save=s.CreateSave();
                save.shopRetailKind="computer";save.flags=save.flags.Where(f=>!f.StartsWith("shop:main-category:")).ToArray();
                save.cityRetailVersion=1;save.cityCompetitionSlot=0;
                save.cityCompetitions=new[]{new CityCompetition{Id="legacy-inferred",Kind="promotion",ShopId=s.MainShop.Place.Id,ProductId="cpu0",StartDay=14,EndDay=17}};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual("clothing",loaded.RetailKind);Assert.IsTrue(loaded.CityCompetitions.Single().Ended);
                    Assert.AreEqual(1,loaded.PlayerShop.CityRetailDemandMultiplier);
                }
            }
        }
    }
}
