using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class BusinessRegressionTests
    {
        private static GameSession Make()
        {
            var s=new GameSession(CityLayout.FromData(GameSessionTests.Layout()),91,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(0),540),false);
            s.EventsEnabled=false; s.PlayerShop.Settings.BaseCustomersPerHour=0; s.PlayerShop.Reputation=40;
            s.Meet(Npcs.Zhou,80); GameSessionTests.Teleport(s,"agency"); return s;
        }
        private static GameSession Reload(GameSession s)
        {
            var json=JsonUtility.ToJson(s.CreateSave());
            var load=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(json));
            load.EventsEnabled=false;load.PlayerShop.Settings.BaseCustomersPerHour=0;return load;
        }
        private static void Midnight(GameSession s,int day)
        {s.Clock.Restore(new GameDate(day-1),1439);s.Clock.Advance(1);}
        private static void ListUnit(GameSession s) => s.DebugAddListing("tea",0,100,Money.FromYuan(1000));
        [Test] public void DirectCreditTracksGoodsDebtAndCash_AndJsonResumesOnce()
        {
            using(var s=Make())
            {
                Assert.True(s.SignDirectAgreement());var cash=s.Ledger.Balance;
                var total=s.DirectPrice(PartCatalog.Get("cpu0"))*3;
                Assert.True(s.OrderDirect("cpu0",3,true));
                Assert.AreEqual(cash-total.Scale(.3),s.Ledger.Balance);
                Assert.AreEqual(total-total.Scale(.3),s.SupplierDebtTotal);
                Assert.AreEqual(3,s.PendingOnlineCount("cpu0"));
                using(var l=Reload(s))
                {
                    Assert.AreEqual(s.NetWorth(),l.NetWorth());
                    l.Clock.Advance(25*60); Assert.AreEqual(3,l.PlayerShop.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.Zero(l.DirectShipments.Count); Assert.True(l.RepaySupplier(l.SupplierDebts[0].Id));
                    Assert.Zero(l.SupplierDebts.Count); l.Clock.Advance(60);
                    Assert.AreEqual(3,l.PlayerShop.Stock.CountAll(PartCatalog.Get("cpu0")));
                }
            }
        }
        [Test] public void DirectAndOnlineShareIncomingCapacity()
        {
            using(var s=Make())
            {
                Assert.True(s.SignDirectAgreement());s.PlayerShop.Stock.Capacity=3;
                Assert.True(s.OrderDirect("cpu0",3,false));
                Assert.False(s.OrderDirect("cpu0",3,false));
                var store=s.Online.DiscoveredIds.First();
                Assert.False(s.PlaceOnlineOrder(store,new[]{(PartCatalog.Get("cpu0"),1)}));
                Assert.AreEqual(3,s.PendingOnlineCount("cpu0"));
            }
        }
        [Test] public void SupplierDueAutoPays_WithoutDoubleChargingAfterLoad()
        {
            using(var s=Make())
            {
                s.SignDirectAgreement();s.OrderDirect("cpu0",3,true);var debt=s.SupplierDebtTotal;
                var balance=s.Ledger.Balance;Midnight(s,14);
                Assert.Zero(s.SupplierDebts.Count);Assert.AreEqual(balance-debt,s.Ledger.Balance);
                using(var l=Reload(s)){var cash=l.Ledger.Balance;l.Clock.Advance(1);Assert.AreEqual(cash,l.Ledger.Balance);}
            }
        }
        [Test] public void OverdueCreditBlocksSupply_AccruesOnce_AndCanBeRepaid()
        {
            using(var s=Make())
            {
                s.SignDirectAgreement();s.OrderDirect("cpu0",3,true);
                s.Ledger.ForceDebit(s.Clock.Today,"测试",s.Ledger.Balance);
                Midnight(s,14); Assert.True(s.HasOverdueDebt);Assert.False(s.OrderDirect("ram0",3,true));
                Midnight(s,15);long fees=s.SupplierDebts[0].Fees;Assert.Greater(fees,0);
                using(var l=Reload(s)) {l.Clock.Advance(5);Assert.AreEqual(fees,l.SupplierDebts[0].Fees);l.Ledger.Credit(l.Clock.Today,"测试",Money.FromYuan(10000));Assert.True(l.RepaySupplier(l.SupplierDebts[0].Id));Assert.False(l.HasOverdueDebt);}
            }
        }
        [Test] public void EnterpriseBatchUsesNormalAssembly_PaysOnce_RefundsBond()
        {
            using(var s=Make())
            {
                s.RefreshEnterpriseOffer();var c=s.EnterpriseContracts.Single();var before=s.Ledger.Balance;
                Assert.True(s.AcceptEnterprise(c.Id)); Assert.False(s.AcceptEnterprise(c.Id));
                foreach(var p in PartCatalog.BuildList(c.Tier)) s.PlayerShop.Stock.ForceAdd(p,true,c.Count);
                foreach(var o in s.PlayerShop.Orders.ToArray()) Assert.True(s.PlayerShop.CompleteBuild(o.Id));
                Assert.True(c.Settled);Assert.AreEqual(c.Count,c.Completed);
                Assert.AreEqual(before+c.Total+c.Total.Scale(.03),s.Ledger.Balance);
                Assert.Zero(s.ContractDeposits.Cents);
                using(var l=Reload(s)){Assert.True(l.EnterpriseContracts.Single().Settled);Assert.False(l.CancelEnterprise(c.Id));Assert.AreEqual(s.Ledger.Balance,l.Ledger.Balance);}
            }
        }
        [Test] public void CancellingReservedEnterpriseReturnsParts_AndForfeitsUnfulfilledBond()
        {
            using(var s=Make())
            {
                s.RefreshEnterpriseOffer();var c=s.EnterpriseContracts.Single();var before=s.Ledger.Balance;s.AcceptEnterprise(c.Id);
                foreach(var p in PartCatalog.BuildList(c.Tier))s.PlayerShop.Stock.ForceAdd(p,true,1);
                Assert.True(s.PlayerShop.ReserveBuild(s.PlayerShop.Orders[0].Id));
                using(var l=Reload(s))
                {
                    Assert.True(l.CancelEnterprise(c.Id));Assert.AreEqual(6,l.PlayerShop.Stock.Lines().Sum(x=>x.Count));
                    Assert.True(l.EnterpriseContracts.Single().Settled);Assert.AreEqual(before-c.Total.Scale(.05),l.Ledger.Balance);
                    Assert.False(l.CancelEnterprise(c.Id));Assert.Zero(l.PlayerShop.Orders.Count);
                }
            }
        }
        [Test] public void EnterpriseExpiredOrdersResolveContractWithoutLosingReservedParts()
        {
            using(var s=Make())
            {
                s.RefreshEnterpriseOffer();var c=s.EnterpriseContracts.Single();s.AcceptEnterprise(c.Id);
                foreach(var p in PartCatalog.BuildList(c.Tier))s.PlayerShop.Stock.ForceAdd(p,true,1);
                s.PlayerShop.ReserveBuild(s.PlayerShop.Orders[0].Id);
                s.Clock.Restore(new GameTimestamp(c.DueMinute).Date,new GameTimestamp(c.DueMinute).MinuteOfDay-1);s.Clock.Advance(1);
                Assert.True(c.Settled);Assert.AreEqual(c.Count,c.Failed);Assert.AreEqual(6,s.PlayerShop.Stock.Lines().Sum(x=>x.Count));
            }
        }
        [Test] public void ReadingOffersDoesNotRerollPricesOrIssueMoreContracts()
        {
            using(var s=Make()) {s.RefreshEnterpriseOffer();var c=s.EnterpriseContracts[0];for(int i=0;i<10;i++)s.RefreshEnterpriseOffer();Assert.AreSame(c,s.EnterpriseContracts.Single());using(var l=Reload(s)){l.RefreshEnterpriseOffer();Assert.AreEqual(c.UnitCents,l.EnterpriseContracts.Single().UnitCents);}}
        }
        [Test] public void CafeConsumesRealHardware_AndCannotBeLeasedAsMainShop()
        {
            using(var s=Make())
            {
                ListUnit(s);Assert.False(s.OpenVenture("tea","cafe"));
                foreach(var p in PartCatalog.BuildList(1))s.PlayerShop.Stock.ForceAdd(p,true,6);
                Assert.True(s.OpenVenture("tea","cafe"));Assert.True(s.PlayerShop.Stock.IsEmpty);Assert.False(s.SignLease("tea"));
                Assert.AreEqual("社区网吧",s.PlaceTitle(s.City.GetPlace("tea")));Assert.False(s.IsVacant(s.City.GetPlace("tea")));
                using(var l=Reload(s)){Assert.AreEqual(s.VentureValue,l.VentureValue);Assert.AreEqual("cafe",l.ManagedVentures.Single().Kind);}
            }
        }
        [Test] public void VentureOperationsStayInSubaccount_TransfersPreserveNetWorth()
        {
            using(var s=Make())
            {
                ListUnit(s);Assert.True(s.OpenVenture("tea","branch"));var cash=s.Ledger.Balance;var v=s.ManagedVentures.Single();
                Midnight(s,1);Assert.AreEqual(cash,s.Ledger.Balance);Assert.Greater(v.LastSalesCents,0);
                Assert.AreEqual(500000+v.LastSalesCents-v.LastCostsCents,v.ReserveCents);
                var worth=s.NetWorth();Assert.True(s.TransferVentureCash(v.PlaceId,1000,true));Assert.AreEqual(worth,s.NetWorth());
                Assert.True(s.TransferVentureCash(v.PlaceId,1000,false));Assert.AreEqual(worth,s.NetWorth());
                using(var l=Reload(s)){var reserve=l.VentureReserve;l.Clock.Advance(1);Assert.AreEqual(reserve,l.VentureReserve);}
            }
        }
        [Test] public void EmptyVenturePauses_HeadquartersPaysRetainer_ClosingIsIdempotent()
        {
            using(var s=Make())
            {
                ListUnit(s);s.OpenVenture("tea","branch");s.TransferVentureCash("tea",5000,true);var cash=s.Ledger.Balance;
                Midnight(s,1);var v=s.ManagedVentures.Single();Assert.True(v.Paused);Assert.Zero(v.ReserveCents);Assert.Less(s.Ledger.Balance.Cents,cash.Cents);
                var worth=s.NetWorth();Assert.True(s.CloseVenture("tea"));Assert.AreEqual(worth,s.NetWorth());Assert.False(s.CloseVenture("tea"));Assert.True(s.IsVacant(s.City.GetPlace("tea")));
            }
        }
        [Test] public void PropertyPurchaseOffsetsMortgage_StopsRent_AndMonthlyPrincipalReducesDebt()
        {
            using(var s=Make())
            {
                var worth=s.NetWorth();Assert.True(s.BuyProperty("pc",true));Assert.AreEqual(worth,s.NetWorth());Assert.Zero(s.CurrentShopRent.Cents);
                var p=s.OwnedProperties.Single();long debt=p.MortgageCents;var cash=s.Ledger.Balance;
                Midnight(s,30);Assert.AreEqual(debt-p.MonthlyPrincipalCents,p.MortgageCents);
                Assert.AreEqual(cash-Money.FromCents(p.MonthlyPrincipalCents)-Money.FromCents(debt).Scale(.004)-Money.FromCents(p.PriceCents).Scale(.001)-s.CurrentHomeRent-s.HomeUtilities,s.Ledger.Balance);
                using(var l=Reload(s)){Assert.Zero(l.CurrentShopRent.Cents);Assert.AreEqual(s.MortgageDebt,l.MortgageDebt);Assert.True(l.RepayMortgage("pc"));Assert.Zero(l.MortgageDebt.Cents);}
            }
        }
        [Test] public void ClosingOwnedVentureKeepsProperty_AndStartsRentalIncome()
        {
            using(var s=Make())
            {
                ListUnit(s);s.OpenVenture("tea","branch");Assert.True(s.BuyProperty("tea",false));Assert.True(s.CloseVenture("tea"));
                Assert.NotNull(s.PropertyAt("tea"));Assert.False(s.IsVacant(s.City.GetPlace("tea")));Assert.False(s.Listings.Any(x=>x.PlaceId=="tea"));
                var cash=s.Ledger.Balance;Midnight(s,1);Assert.Greater(s.Ledger.Balance.Cents,cash.Cents);Assert.True(s.SellProperty("tea"));Assert.Null(s.PropertyAt("tea"));
            }
        }
        [Test] public void HomePurchaseAndSaleRetainFurniture_AndClearMortgage()
        {
            using(var s=Make())
            {
                var furniture=s.HomeLayout.Count;double quality=s.HomeSleepQuality;
                Assert.True(s.ChangeHomeTier(1));Assert.Greater(s.HomeSleepQuality,quality);Assert.AreEqual(Money.FromYuan(1200),s.CurrentHomeRent);
                var worth=s.NetWorth();Assert.True(s.BuyHome(true));Assert.AreEqual(worth,s.NetWorth());Assert.Zero(s.CurrentHomeRent.Cents);
                using(var l=Reload(s)){Assert.AreEqual(1,l.HomeTier);Assert.AreEqual(furniture,l.HomeLayout.Count);Assert.True(l.SellHome());Assert.Zero(l.HomeMortgageCents);Assert.AreEqual(Money.FromYuan(1200),l.CurrentHomeRent);}
            }
        }
        [Test] public void FundRoundtripChargesBothFees_AndNeverSellsUnownedUnits()
        {
            using(var s=Make())
            {
                var before=s.NetWorth();var amount=s.FundUnitPrice*10;Assert.True(s.TradeFund(10,true));
                Assert.AreEqual(before-(amount.Scale(1.005)-amount),s.NetWorth());
                using(var l=Reload(s)){Assert.AreEqual(10,l.FundUnits);Assert.True(l.TradeFund(10,false));Assert.False(l.TradeFund(10,false));Assert.AreEqual(before-amount.Scale(1.005)+amount.Scale(.995),l.NetWorth());}
            }
        }
        [Test] public void CreatorCampaignHasDurationResourceCost_AndCannotStack()
        {
            using(var s=Make())
            {
                Assert.True(s.StartCreatorCampaign("momo"));Assert.Greater(s.CreatorTrafficMultiplier,1);Assert.False(s.StartCreatorCampaign("ake"));
                Assert.True(s.SupportCreator("momo"));Assert.False(s.SupportCreator("momo"));
                using(var l=Reload(s)){Assert.AreEqual(s.CreatorTrafficMultiplier,l.CreatorTrafficMultiplier);Midnight(l,7);Assert.AreEqual(1,l.CreatorTrafficMultiplier);Assert.True(l.StartCreatorCampaign("ake"));}
            }
        }
        [Test] public void CarVanAreLegal_PersistCapacity_AndCanBeSold()
        {
            using(var s=Make())
            {
                GameSessionTests.Teleport(s,"dealer");Assert.True(s.BuyVehicle(VehicleKind.Van));Assert.True(s.SetVehicle(VehicleKind.Van));
                Assert.False(s.CurrentVehicle.Illegal);Assert.AreEqual(80,s.CurrentVehicle.CargoCapacity);
                using(var l=Reload(s)){Assert.AreEqual(VehicleKind.Van,l.Vehicle);Assert.AreEqual(s.Cargo.Capacity,l.Cargo.Capacity);Assert.True(l.SellVehicle(VehicleKind.Van));Assert.False(l.Owns(VehicleKind.Van));Assert.AreEqual(VehicleKind.Walk,l.Vehicle);Assert.False(l.SellVehicle(VehicleKind.Van));}
            }
        }
        [Test] public void AutoRestockReservesDueBillsAndMortgagePayments()
        {
            using(var s=Make())
            {
                s.SignDirectAgreement(); s.OrderDirect("cpu0",3,true); var debt=s.SupplierDebtTotal;
                s.Clock.Restore(new GameDate(10),540);
                Assert.AreEqual(debt,s.UpcomingBusinessPayments(7));
                Assert.GreaterOrEqual(s.EffectiveRestockReserve.Cents,debt.Cents+50000);
                s.BuyProperty("pc",true);s.Clock.Restore(new GameDate(29),540);
                var property=s.OwnedProperties.Single();
                var expected=debt+Money.FromCents(property.MonthlyPrincipalCents)+Money.FromCents(property.MortgageCents).Scale(.004)+Money.FromCents(property.PriceCents).Scale(.001);
                Assert.AreEqual(expected,s.UpcomingBusinessPayments(1));
            }
        }
        [Test] public void PromotionRevenueDoesNotDoubleCountWhenMidnightIsCrossed()
        {
            using(var s=Make())
            {
                s.StartCreatorCampaign("momo");
                s.Ledger.Credit(s.Clock.Today,"零售",Money.FromYuan(4000));s.Clock.Advance(1);
                Assert.AreEqual(400000,s.Creator("momo").AttributedSalesCents);
                Midnight(s,1);Assert.AreEqual(400000,s.Creator("momo").AttributedSalesCents);
                Assert.Greater(s.Creator("momo").Relationship,0);
            }
        }
        [TestCase("branch")][TestCase("cafe")][TestCase("esports")][TestCase("food")]
        [TestCase("flowers")][TestCase("stay")][TestCase("cars")]
        public void EveryVentureCanOperateSaveResumeAndExitOverNinetyDays(string kind)
        {
            using(var s=Make())
            {
                ListUnit(s);var d=Ventures.Get(kind);
                if(d.HardwareCount>0)foreach(var p in PartCatalog.BuildList(d.HardwareTier))s.PlayerShop.Stock.ForceAdd(p,true,d.HardwareCount);
                Assert.True(s.OpenVenture("tea",kind));
                for(int day=1;day<=45;day++)Midnight(s,day);
                using(var l=Reload(s))
                {
                    for(int day=46;day<=90;day++)Midnight(l,day);
                    var v=l.ManagedVentures.Single();
                    Assert.GreaterOrEqual(v.ReserveCents,0);Assert.GreaterOrEqual(v.Condition,.4);Assert.IsFalse(double.IsNaN(v.LastDemand));
                    Assert.Greater(l.Ledger.Balance.Cents,0);var worth=l.NetWorth();Assert.True(l.CloseVenture("tea"));Assert.AreEqual(worth,l.NetWorth());
                }
            }
        }
        [Test] public void ParkConstructionLayoutSynergyAndExitSurviveJson()
        {
            using(var s=Make())
            {
                ListUnit(s);Assert.True(s.BuyPark("tea"));Assert.False(s.OpenParkUnit(0,"flowers"));
                s.Clock.Restore(new GameDate(7),540);
                Assert.True(s.OpenParkUnit(0,"flowers"));Assert.True(s.OpenParkUnit(1,"food"));Assert.True(s.OpenParkUnit(3,"food"));
                Assert.AreEqual(1.16,s.ParkTrafficBonus(s.ParkUnit(0)),1e-9);
                Assert.False(s.SellProperty("tea"));Assert.False(s.OpenVenture("tea","branch"));
                var worth=s.NetWorth();Assert.True(s.MoveParkUnit(0,5));Assert.AreEqual(worth-Money.FromYuan(1000),s.NetWorth());
                Assert.IsNull(s.ParkUnit(0));Assert.AreEqual(1,s.ParkTrafficBonus(s.ParkUnit(5)));
                using(var l=Reload(s))
                {
                    Assert.True(l.HasPark);Assert.AreEqual("flowers",l.ParkUnit(5).Kind);Assert.AreEqual(s.NetWorth(),l.NetWorth());
                    Midnight(l,8);Assert.Greater(l.ParkUnit(5).LastSalesCents,0);
                    foreach(var v in l.ManagedVentures.ToArray())Assert.True(l.CloseVenture(v.PlaceId));
                    Assert.False(l.Listings.Any(x=>x.PlaceId.Contains("/unit/")));
                    Assert.True(l.SellProperty("tea"));Assert.False(l.HasPark);
                }
            }
        }
        [Test] public void McnContractSettlesOnceAndStopsAfterCancellation()
        {
            using(var s=Make())
            {
                Assert.True(s.FoundMcn());var c=s.Creator("momo");c.Relationship=40;c.Resources=60;
                Assert.True(s.SignMcnCreator(c.Id));Assert.False(s.SignMcnCreator(c.Id));Assert.False(s.CloseMcn());
                Midnight(s,1);Assert.Greater(c.McnIncomeCents,0);Assert.AreEqual(90,s.McnDailyWages);
                using(var l=Reload(s))
                {
                    var cash=l.Ledger.Balance;l.Clock.Advance(1);Assert.AreEqual(cash,l.Ledger.Balance);
                    Assert.True(l.EndMcnContract(c.Id));Assert.True(l.CloseMcn());long income=l.Creator(c.Id).McnIncomeCents;
                    Midnight(l,2);Assert.AreEqual(income,l.Creator(c.Id).McnIncomeCents);Assert.Zero(l.McnDailyWages);
                }
            }
        }
        [Test] public void CreatorStoryMemoryAndExclusiveRomancePersistSeparatelyFromContract()
        {
            using(var s=Make())
            {
                var c=s.Creator("momo");c.Relationship=40;c.Affection=60;
                Assert.True(s.ResolveCreatorStory(c.Id,true));Assert.True(s.ResolveCreatorStory(c.Id,true));
                Assert.True(s.StartRomance(c.Id));Assert.False(s.StartRomance("ake"));
                Assert.True(s.FoundMcn());Assert.True(s.SignMcnCreator(c.Id));
                using(var l=Reload(s))
                {
                    Assert.AreEqual(c.Id,l.RomanceId);Assert.AreEqual(2,l.Creator(c.Id).StoryStage);
                    Assert.True(l.Flags.Contains("creator-story:momo:0:respect"));
                    Assert.True(l.EndRomance());Assert.Greater(l.Creator(c.Id).McnUntilDay,l.Clock.Today.TotalDays);
                    Assert.IsNull(l.RomanceId);
                }
            }
        }
        [Test] public void LegacySaveDoesNotInventBusinessAssetsOrLiabilities()
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.hasBusiness=false;save.business=null;
                using(var l=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)))) {Assert.Zero(l.TotalDebt.Cents);Assert.Zero(l.ManagedVentures.Count);Assert.False(l.DirectAgreement);Assert.Zero(l.HomeTier);Assert.AreEqual(3,l.CreatorPartners.Count);Assert.AreEqual(s.NetWorth(),l.NetWorth());}
            }
        }
    }
}
