using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    public sealed class ResidentialAgencyTests
    {
        private static GameSession Make(int access=1)
        {
            var s=DistrictProgressionTests.Make();s.DebugAddCash(1000000);
            typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=access,Chapter=access});
            At(s,s.AgencyFor(s.ResidencePlace(1)).Id);return s;
        }
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(id));

        [Test] public void CanBuyAndMoveDirectlyWithoutIntermediateRentAndKeepsFurnitureAndStorage()
        {
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;int furniture=s.HomeLayout.Count;s.HomeBox.Food.RiceKg=3;
                Assert.IsNull(s.ResidenceBlocker(1,true));Assert.True(s.BuyResidence(1,false));
                Assert.AreEqual(before-Money.FromYuan(180500).Cents,s.Ledger.Balance.Cents);
                Assert.AreEqual(1,s.HomeTier);Assert.AreEqual("home_tier1",s.Home.Id);
                Assert.AreEqual(Money.FromYuan(180000).Cents,s.HomePriceCents);Assert.Zero(s.HomeMortgageCents);Assert.Zero(s.CurrentHomeRent.Cents);
                Assert.AreEqual(furniture,s.HomeLayout.Count);Assert.AreEqual(3,s.HomeBox.Food.RiceKg);
                long cash=s.Ledger.Balance.Cents;Assert.False(s.BuyResidence(1,false));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void LegacyHomeMortgageIsRemovedOnReloadWithoutChangingCashOrOwnership()
        {
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;
                Assert.False(s.BuyResidence(1,true));Assert.AreEqual(before,s.Ledger.Balance.Cents);
                Assert.True(s.BuyResidence(1,false));
                Assert.AreEqual(before-Money.FromYuan(180500).Cents,s.Ledger.Balance.Cents);
                var save=s.CreateSave();save.business.HomeMortgageCents=12600000;save.business.HomeMonthlyPrincipalCents=350000;
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save))))
                {
                    Assert.AreEqual(1,loaded.HomeTier);Assert.Zero(loaded.HomeMortgageCents);Assert.Zero(loaded.HomeMonthlyPrincipalCents);
                    Assert.AreEqual(s.HomePriceCents,loaded.HomePriceCents);Assert.AreEqual(s.Ledger.Balance,loaded.Ledger.Balance);
                    Assert.True(loaded.SellHome());Assert.Zero(loaded.HomePriceCents);
                    Assert.AreEqual(Money.FromYuan(280),loaded.CurrentHomeRent);
                }
            }
        }
        [Test] public void RentingThenBuyingExistingHomeDoesNotChargeMovingFeeTwice()
        {
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;
                Assert.True(s.ChangeHomeTier(1));Assert.AreEqual(before-Money.FromYuan(1700).Cents,s.Ledger.Balance.Cents);
                before=s.Ledger.Balance.Cents;Assert.True(s.BuyHome(false));
                Assert.AreEqual(before-Money.FromYuan(180000).Cents,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void WrongAgencyLockedDistrictAndInsufficientCashNeverPartiallyMoveOrDebit()
        {
            using(var s=Make(0))
            {
                long cash=s.Ledger.Balance.Cents;
                Assert.False(s.BuyResidence(1,false));Assert.False(s.ChangeHomeTier(1));Assert.Zero(s.HomeTier);Assert.AreEqual(cash,s.Ledger.Balance.Cents);
            }
            using(var s=Make())
            {
                At(s,s.AgencyFor(s.ResidencePlace(0)).Id);long cash=s.Ledger.Balance.Cents;
                Assert.False(s.BuyResidence(1,true));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                At(s,s.AgencyFor(s.ResidencePlace(1)).Id);
                s.Ledger.TryDebit(s.Clock.Today,"测试",s.Ledger.Balance-Money.FromYuan(100),"不足现金");
                cash=s.Ledger.Balance.Cents;Assert.False(s.BuyResidence(1,true));Assert.Zero(s.HomeTier);Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.False(s.BuyResidence(0,false));
            }
        }
    }
}
