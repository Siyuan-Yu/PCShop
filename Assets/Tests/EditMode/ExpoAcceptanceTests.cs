using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public sealed class ExpoAcceptanceTests
    {
        private static GameSession Make() => GameSession.CreateCityLifePreset(
            CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)), "quarter_expo");

        [Test] public void PresetOpensOnlyCommunityAndPreparesActualBrandStockAndPeople()
        {
            using (var s = Make())
            {
                Assert.True(s.IsCityLifeTest && s.IsBrandExpoAcceptance);
                Assert.AreEqual(1, s.DistrictAccessTier);
                Assert.True(s.DistrictRoadOpen(1)); Assert.False(s.DistrictRoadOpen(2));
                Assert.AreEqual(68, s.Clock.Today.TotalDays);
                Assert.AreEqual("acceptance-expo", s.BrandBusiness.Brands.Single().Id);
                Assert.AreEqual(320, s.BrandBusiness.Brands.Single().Influence);
                Assert.IsEmpty(s.BrandBusiness.Exhibits); Assert.IsEmpty(s.BrandBusiness.Orders);
                Assert.AreEqual(150, s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"), true));
                Assert.AreEqual(150, s.MainShop.Sim.Stock.Count(PartCatalog.Get("gpu0"), true));
                Assert.NotNull(s.MainShop.Employee);
                Assert.GreaterOrEqual(s.Relation("life_creator"), 20);
                Assert.IsNull(s.DistrictAccessBlocker(s.City.GetPlace("life_expo")));
                Assert.AreEqual(s.City.GetPlace("life_expo").NodeId, s.PlayerNodeId);
                Assert.IsNull(s.CurrentPlace); // Start at the actual entrance; the player still enters.
                Assert.True(s.HasSmartphone);
                Assert.AreEqual(6,s.MyShopCount);Assert.AreEqual(5,s.DistrictOperatingShops(0));Assert.AreEqual(1,s.DistrictOperatingShops(1));
                Assert.IsNull(s.DistrictPrerequisiteBlocker(1));Assert.True(s.DebugExpoBusinessBaseReady);
                Assert.AreEqual(150000000,s.Ledger.Balance.Cents);
                Assert.AreEqual("home_tier1",s.Home.Id);Assert.AreEqual(1,s.HomeTier);Assert.AreEqual(120000,s.CurrentHomeRent.Cents);
                Assert.AreEqual(6,s.BrandBusiness.Brands.Single().ShopIds.Length);
                Assert.True(s.DistrictNodeCompleted("community.trial"));Assert.True(s.DistrictStoryState.CommunityMapDelivered);
                Assert.AreEqual(6,s.MyShops.Select(shop=>shop.Employee.PersonId).Distinct().Count());
                foreach(var shop in s.MyShops)
                {
                    Assert.False(shop.Sim.NotFitted);Assert.Less(shop.RenovationDay,s.Clock.Today.TotalDays);
                    Assert.True(shop.Sim.IsOpenAt(s.Clock.Now),shop.Place.Id);
                    Assert.True(s.CityEmployeeAvailable(shop.Employee,shop),shop.Place.Id);
                    Assert.Greater(shop.Sim.Stock.Count(PartCatalog.Get("cpu0"),true),0);
                    if(shop!=s.MainShop)Assert.Greater(shop.Lease.Deposit.Cents,0);
                }
            }
        }

        [Test] public void ShortcutRequiresRegistrationAndCannotChangeAnOrdinarySave()
        {
            using (var s = DistrictProgressionTests.Make())
            {
                long time = s.Clock.Now.TotalMinutes, cash = s.Ledger.Balance.Cents;int shopsBefore=s.MyShopCount;
                Assert.False(s.DebugAdvanceToExpoOpening());
                Assert.AreEqual(time, s.Clock.Now.TotalMinutes); Assert.AreEqual(cash, s.Ledger.Balance.Cents);
                Assert.False(s.DebugPrepareExpoBusinessBase());Assert.AreEqual(cash,s.Ledger.Balance.Cents);Assert.AreEqual(shopsBefore,s.MyShopCount);
            }
            using (var s = Make())
            {
                long time = s.Clock.Now.TotalMinutes;
                Assert.False(s.DebugAdvanceToExpoOpening()); Assert.AreEqual(time, s.Clock.Now.TotalMinutes);
            }
        }

        [Test] public void OldAcceptanceSaveCanAddRealShopsAndHousingWithoutLosingBookedExpoOrDuplicatingCosts()
        {
            using(var s=GameSession.CreateCityLifePreset(CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)),"brand_expo"))
            {
                Assert.AreEqual(1,s.MyShopCount);Assert.AreEqual(0,s.HomeTier);Assert.False(s.DebugExpoBusinessBaseReady);
                Assert.True(McnSigningTest.Sign(s,"acceptance-expo","momo"));s.MainShop.Sim.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,90);
                Assert.True(s.RegisterBrandExpo("acceptance-expo",s.MainShop.Place.Id,"cpu0","buyers"));
                string exhibit=s.BrandBusiness.Exhibits.Single().Id,contract=s.KolStreaming.Contracts.Single().Id;
                int stock=s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"),true);long minute=s.Clock.Now.TotalMinutes;
                Assert.True(s.DebugPrepareExpoBusinessBase());Assert.True(s.DebugExpoBusinessBaseReady);
                Assert.AreEqual(stock,s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"),true));Assert.AreEqual(minute,s.Clock.Now.TotalMinutes);
                Assert.AreEqual(exhibit,s.BrandBusiness.Exhibits.Single().Id);Assert.AreEqual(contract,s.KolStreaming.Contracts.Single().Id);
                Assert.Zero(s.BrandBusiness.Exhibits.Single().Decision);Assert.IsEmpty(s.BrandBusiness.Orders);
                long cash=s.Ledger.Balance.Cents;int entries=s.Ledger.Entries.Count;var shops=s.MyShops.Select(shop=>shop.Place.Id).ToArray();
                Assert.True(s.DebugPrepareExpoBusinessBase());Assert.AreEqual(cash,s.Ledger.Balance.Cents);Assert.AreEqual(entries,s.Ledger.Entries.Count);
                CollectionAssert.AreEqual(shops,s.MyShops.Select(shop=>shop.Place.Id));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.True(loaded.DebugExpoBusinessBaseReady);Assert.AreEqual("home_tier1",loaded.Home.Id);
                    CollectionAssert.AreEqual(shops,loaded.MyShops.Select(shop=>shop.Place.Id));Assert.AreEqual(cash,loaded.Ledger.Balance.Cents);
                    Assert.AreEqual(exhibit,loaded.BrandBusiness.Exhibits.Single().Id);Assert.AreEqual(contract,loaded.KolStreaming.Contracts.Single().Id);
                    Assert.AreEqual(6,loaded.MyShops.Select(shop=>shop.Employee.PersonId).Distinct().Count());
                }
            }
        }

        [Test] public void RegistrationFastForwardAndSaveKeepRealCostsAndUnfinishedReception()
        {
            using (var s = Make())
            {
                Assert.True(McnSigningTest.Sign(s,"acceptance-expo","momo"));
                long cash = s.Ledger.Balance.Cents;
                Assert.True(s.RegisterBrandExpo("acceptance-expo", s.MainShop.Place.Id, "cpu0", "buyers"));
                Assert.AreEqual(cash - 500000, s.Ledger.Balance.Cents);
                Assert.AreEqual(50, s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"), true));
                var exhibit = s.BrandBusiness.Exhibits.Single();
                Assert.False(s.AssignBrandExpoPerson(exhibit.Id, "life_creator", false));
                Assert.AreEqual("momo",exhibit.KolIds.Single());
                Assert.True(s.DebugAdvanceToExpoOpening());
                Assert.False(s.DebugExpoFastForwarding);
                Assert.AreEqual(exhibit.Day * 1440L + 540, s.Clock.Now.TotalMinutes);
                Assert.False(s.IsGameOver); Assert.Greater(s.Player.Health, 90);
                Assert.AreEqual(0, s.BrandBusiness.Exhibits.Single().Stage);
                Assert.IsEmpty(s.BrandBusiness.Orders);
                Assert.False(s.DebugAdvanceToExpoOpening());
                Assert.GreaterOrEqual(s.BrandBusiness.Brands.Single().Influence,320); // Scheduled livestreams still progress during the week.
                using (var loaded = GameSession.Load(s.City, s.CreateSave()))
                {
                    Assert.True(loaded.IsBrandExpoAcceptance);
                    Assert.AreEqual(1, loaded.DistrictAccessTier);
                    Assert.AreEqual(exhibit.Id, loaded.BrandBusiness.Exhibits.Single().Id);
                    Assert.AreEqual(0, loaded.BrandBusiness.Exhibits.Single().Stage);
                }
            }
        }

        [Test] public void PlayerMustEnterAndCompleteFourDaysBeforeRealOrderAppears()
        {
            using (var s = Make())
            {
                Assert.True(McnSigningTest.Sign(s,"acceptance-expo","momo"));
                Assert.True(s.RegisterBrandExpo("acceptance-expo", s.MainShop.Place.Id, "cpu0", "buyers"));
                string id = s.BrandBusiness.Exhibits.Single().Id;
                Assert.True(s.DebugAdvanceToExpoOpening());
                Assert.False(s.ActBrandExpo(id, "buyers"));
                Assert.True(s.Enter(s.City.GetPlace("life_expo")));
                Assert.True(s.StartQuarterlyExpo(id));
                for(int i=0;i<24;i++){var m=s.CurrentExpoMoment(id);Assert.True(s.ChooseExpoAction(id,System.Array.IndexOf(m.Scores,m.Scores.Max())));}
                var order = s.BrandBusiness.Orders.Single();
                Assert.AreEqual(20, order.Count); Assert.False(order.Done); Assert.False(order.Shipped);
                s.MainShop.Sim.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,20);
                int stock = s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"), true);
                Assert.True(s.FulfilBrandOrder(order.Id));
                Assert.AreEqual(stock - 20, s.MainShop.Sim.Stock.Count(PartCatalog.Get("cpu0"), true));
                Assert.False(s.BrandBusiness.Orders.Single().Done);
                Assert.False(s.FulfilBrandOrder(order.Id));
            }
        }
    }
}
