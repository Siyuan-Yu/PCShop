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
            if (!fullCity)
                data.places = data.places.Append(new LayoutPlaceData { id = "rival_shop", name = "邻街同行",
                    kind = "shop", node = "s1", size = "small", explicitEntrance = true, entranceY = 8,
                    business = kind == "computer" ? "电脑" : "服装" }).ToArray();
            var s=new GameSession(CityLayout.FromData(data),71,Money.FromYuan(300000),GameTimestamp.From(new GameDate(0),11*60),false);
            s.EventsEnabled=false;GameSessionTests.Teleport(s,s.MainShop.Place.Id);
            Assert.IsTrue(s.SetShopMainCategory(kind));
            s.PlayerShop.SetHours(0,1440);s.PlayerShop.ForcedClosed=false;s.PlayerShop.NotFitted=false;s.PlayerShop.Markup=1.4;
            return s;
        }

        private static void PutClothingOnShelf(GameSession s)
        {
            var product = Gifts.Get("clothing:jacket");
            s.PlayerShop.Stock.ForceAddGoods(product.Id, 2);
            s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double>
                { { ShelfKinds.ForGift(product.Id).Id, 60 } });
        }

        private static void BeginPromotionRound(GameSession s)
        {
            s.Clock.Restore(new GameDate(14), 11 * 60);
            s.TickCityRetail();
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
        [TestCase("clothing", "clothing:jacket")]
        [TestCase("restaurant", "produce:veg")]
        [TestCase("secondhand", null)]
        public void StudentDiscountOrderUsesTheStoreInventoryAndAddsAwareness(string kind, string productId)
        {
            using var s = Make(kind);
            var shop = s.CurrentShop;
            shop.Sim.Reputation = 10;
            shop.Sim.Stock.Clear();
            var special = s.SpecialBusiness(shop.Place.Id);
            if (s.IsSpecialBusiness(shop))
            {
                shop.Layout.Clear();
                foreach (string id in GameSession.OpeningFurniture(kind))
                    shop.Layout.Add(new PlacedFurniture { Id = id });
            }
            if (kind == "secondhand")
            {
                shop.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double> { { "secondhand", 60 } });
                for (int i = 1; i <= 5; i++)
                    special.Used.Add(new UsedItem { Id = i, Owned = true, Inspected = true, Listed = true, Cost = 30, Price = 80 });
            }
            else
            {
                shop.Sim.Stock.ForceAddGoods(productId, 5);
                if (kind == "restaurant") shop.Sim.Stock.ForceAddGoods("produce:rice", 3);
                else shop.Sim.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double>
                    { { ShelfKinds.ForGift(productId).Id, 60 } });
            }
            double awareness = shop.Sim.Awareness;
            var cash = s.Ledger.Balance;
            s.RaiseEvent("students");
            Assert.IsNotNull(s.PendingEvent);
            Assert.IsTrue(s.ResolveEvent(0));
            Assert.IsTrue(s.Ledger.Balance > cash);
            Assert.AreEqual(awareness + 2, shop.Sim.Awareness, 1e-9);
            if (kind == "secondhand") Assert.IsTrue(special.Used.All(item => item.Sold && !item.Listed));
            else Assert.AreEqual(0, shop.Sim.Stock.GoodsCount(productId));
            if (kind == "restaurant") Assert.AreEqual(.25, s.KitchenRice(shop), 1e-9);
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
                var cash=s.Ledger.Balance;var reputation=s.PlayerShop.ShopReputation;
                Assert.True(s.ResolveEvent(1));Assert.AreEqual(cash-Money.FromYuan(1000),s.Ledger.Balance);
                Assert.AreEqual(reputation,s.PlayerShop.ShopReputation);
            }
        }

        [TestCase("zhang_family", Npcs.Zhang, "clothing", "clothing:jacket", 1, .70)]
        [TestCase("landlady_help", Npcs.Landlady, "clothing", "clothing:jacket", 3, .95)]
        [TestCase("landlady_help", Npcs.Landlady, "models", "model:harbor:skiff", 3, .95)]
        [TestCase("zhang_family", Npcs.Zhang, PetShopCatalog.ShopKind, PetShopCatalog.CatFood, 1, .70)]
        public void IntroducedOrdersUseRealRetailInventory(string eventId, string personId, string kind, string product, int count, double discount)
        {
            using var s = Make(kind);
            var shop = s.CurrentShop;
            shop.Sim.Stock.ForceAddGoods(product, count);
            s.Meet(personId, 55);
            s.RaiseEvent(eventId);
            Assert.IsTrue(s.ResolveEvent(0));
            var offer = s.CityReferrals.Single();
            Assert.AreEqual((shop.Sim.GoodsPriceOf(product) * count).Scale(discount), offer.Total);
            Assert.AreEqual(shop.Place.Id, offer.ShopId);
            var cash = s.Ledger.Balance;
            Assert.IsTrue(s.AcceptCityReferral(offer.Id));
            Assert.AreEqual(cash - offer.Deposit, s.Ledger.Balance);
            Assert.IsTrue(s.DeliverIndustryReferral(offer.Id));
            Assert.AreEqual(0, shop.Sim.Stock.GoodsCount(product));
            Assert.AreEqual(cash + offer.Total + (count == 1 ? Money.Zero : offer.Total.Scale(.03)), s.Ledger.Balance);
            Assert.AreEqual(57, s.Relation(personId));
            Assert.IsFalse(s.DeliverIndustryReferral(offer.Id));
        }

        [TestCase("restaurant")]
        [TestCase("secondhand")]
        public void SpecialShopReferralKeepsItsQuotedInventoryAndConsumesItOnDelivery(string kind)
        {
            using var s = Make(kind);
            var shop = s.CurrentShop;
            shop.Layout.Clear();
            foreach (string id in GameSession.OpeningFurniture(kind))
                shop.Layout.Add(new PlacedFurniture { Id = id });
            var state = s.SpecialBusiness(shop.Place.Id);
            if (kind == "restaurant")
            {
                shop.Sim.Stock.ForceAddGoods("produce:rice", 2);
                shop.Sim.Stock.ForceAddGoods("produce:veg", 3);
            }
            else
            {
                int shelf = shop.Layout.FindIndex(item => item.Id == FurnitureCatalog.Shelf);
                Assert.IsTrue(s.SetShelfKind(shelf, "secondhand"));
                for (int id = 1; id <= 3; id++) state.Used.Add(new UsedItem
                    { Id = id, Owned = true, Inspected = true, Listed = true, Cost = 30, Price = 80 });
            }
            s.Meet(Npcs.Landlady, 55);
            s.RaiseEvent("landlady_help");
            Assert.IsTrue(s.ResolveEvent(0));
            var offer = s.CityReferrals.Single();
            Assert.IsTrue(s.AcceptCityReferral(offer.Id));
            Assert.IsTrue(s.DeliverIndustryReferral(offer.Id));
            Assert.IsTrue(offer.Fulfilled);
            if (kind == "restaurant") Assert.AreEqual(0, shop.Sim.Stock.GoodsCount("produce:veg"));
            else Assert.IsTrue(state.Used.All(item => item.Sold && !item.Listed));
        }

        [Test]
        public void PersonalComputerReferralIsASevenTenthsBuildOrderWithoutADeposit()
        {
            using var s = Make("computer");
            var shop = s.CurrentShop;
            s.RaiseEvent("zhang_family");
            Assert.IsTrue(s.ResolveEvent(0));
            var offer = s.CityReferrals.Single();
            Assert.AreEqual(shop.Sim.QuoteBuild(0).Scale(.70), offer.Total);
            var balance = s.Ledger.Balance;
            Assert.IsTrue(s.AcceptCityReferral(offer.Id));
            Assert.AreEqual(balance, s.Ledger.Balance);
            Assert.IsTrue(s.EnterpriseContracts.Single().PersonalReferral);
            var order = shop.Sim.Orders.Single(o => o.ContractId == offer.ContractId);
            foreach (var part in PartCatalog.BuildList(order.Tier)) shop.Sim.Stock.ForceAdd(part, true, 1);
            Assert.IsTrue(shop.Sim.CompleteBuild(order.Id));
            Assert.AreEqual(balance + offer.Total, s.Ledger.Balance);
            Assert.IsTrue(offer.Fulfilled);
        }
        [Test]
        public void ClosedShopCannotTriggerPriceWarOrConsumeTheCompetitionRound()
        {
            using(var s=Make("computer"))
            {
                s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram0"), true, 5);
                s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string, double> { { "memory", 60 } });
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
        public void ClothingPriceWarNamesTheActualRivalAndTemporarilyDiscountsOnlyThePromotedProduct()
        {
            using(var s=Make())
            {
                PutClothingOnShelf(s);
                BeginPromotionRound(s);
                Assert.IsTrue(EventCatalog.Get("rival").When(s));s.RaiseEvent("rival");
                Assert.IsNotNull(s.PendingEvent);
                StringAssert.Contains("邻街同行",s.PendingEvent.Body);
                var price=s.PlayerShop.GoodsPriceOf("clothing:jacket");
                var markup=s.PlayerShop.Markup;
                Assert.IsTrue(s.ResolveEvent(0));
                Assert.AreEqual(price.Scale(.9), s.PlayerShop.GoodsPriceOf("clothing:jacket"));
                Assert.AreEqual(markup,s.PlayerShop.Markup,1e-9);
                s.Clock.Restore(new GameDate(17),11*60);
                s.TickCityRetail();
                Assert.AreEqual(price,s.PlayerShop.GoodsPriceOf("clothing:jacket"));
            }
        }

        [TestCase("maintain", 4, 100)]
        [TestCase("position", 5, 120)]
        public void PriceWarResponseChangesAwarenessOrWagesOnlyOnce(string response, double awareness, int wage)
        {
            using var s = Make();
            s.PlayerShop.Reputation = 5;
            s.CurrentShop.Employee = new Employee { Id = "clerk", DailyWageYuan = 100 };
            PutClothingOnShelf(s);
            BeginPromotionRound(s);
            string id = s.CityCompetitions.Single().Id;
            Assert.IsTrue(s.RespondCityCompetition(id, response));
            Assert.IsFalse(s.RespondCityCompetition(id, response));
            Assert.AreEqual(awareness, s.PlayerShop.Awareness, 1e-9);
            Assert.AreEqual(wage, s.CurrentShop.Employee.DailyWageYuan);
        }
        [Test]
        public void ChangingTheMainCategoryInvalidatesAnAlreadyRaisedPriceWar()
        {
            using(var s=Make())
            {
                PutClothingOnShelf(s);
                BeginPromotionRound(s);
                s.RaiseEvent("rival");Assert.IsNotNull(s.PendingEvent);
                Assert.IsTrue(s.SetShopMainCategory("models"));double markup=s.PlayerShop.Markup;
                Assert.IsTrue(s.ResolveEvent(0));Assert.AreEqual(markup,s.PlayerShop.Markup);
            }
        }
        [Test]
        public void PriceWarResponseStaysBoundToTheOriginalOperatingShopAfterSwitchingFocus()
        {
            using(var s=Make("clothing"))
            {
                s.PlayerShop.Reputation=80;s.PlayerShop.Markup=1.1;
                s.PlayerShop.ForcedClosed = true;
                s.DebugAddListing("tea",0,60,Money.FromYuan(3000));GameSessionTests.Teleport(s,"agency");
                Assert.IsTrue(s.SignLease("tea",true));GameSessionTests.Teleport(s,"tea");Assert.IsTrue(s.SetShopMainCategory("clothing"));
                var target=s.CurrentShop;target.Sim.NotFitted=false;target.Sim.ForcedClosed=false;target.Sim.SetHours(0,1440);
                target.Sim.Markup=1.4;target.Sim.EmployeeOnDuty=_=>true;
                PutClothingOnShelf(s);
                BeginPromotionRound(s);
                var price=target.Sim.GoodsPriceOf("clothing:jacket");
                s.RaiseEvent("rival");Assert.IsNotNull(s.PendingEvent);
                GameSessionTests.Teleport(s,"pc");var current=s.CurrentShop;double unchanged=current.Sim.Markup;
                Assert.IsTrue(s.ResolveEvent(0));Assert.AreSame(current,s.CurrentShop);
                Assert.AreEqual(price.Scale(.9),target.Sim.GoodsPriceOf("clothing:jacket"));Assert.AreEqual(unchanged,current.Sim.Markup);
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
            using(var s=Make())
            {
                PutClothingOnShelf(s);
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
