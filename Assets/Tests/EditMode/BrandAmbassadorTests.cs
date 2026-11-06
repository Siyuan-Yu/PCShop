using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Time;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Tests
{
    public sealed class BrandAmbassadorTests
    {
        const string Brand = "acceptance-expo";
        static GameSession Make() => GameSession.CreateCityLifePreset(CityLayout.FromData(
            JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)), "quarter_expo");
        static SaveData Copy(GameSession s) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));

        [Test] public void OneSigningPaysOnceAndGrantsStreamingAndExpoEligibility()
        {
            using(var s=Make())
            {
                long cash=s.Ledger.Balance.Cents, fee=s.KolSigningFee("momo")*100;
                Assert.True(McnSigningTest.Endorse(s,Brand,"momo","endorse",false));
                Assert.AreEqual(cash-fee,s.Ledger.Balance.Cents);
                Assert.AreEqual(1,s.BrandKolCount(Brand));
                var c=s.KolStreaming.Contracts.Single();var d=s.BrandBusiness.Deals.Single();
                Assert.AreEqual(c.Id,d.KolContractId);
                Assert.Contains("momo",s.ExpoAvailableKols(Brand,s.NextBrandExpoDay));
                Assert.False(s.SignKolLive(Brand,"momo"));
                Assert.False(s.SignBrandDeal(Brand,"momo","endorse",true));
                Assert.AreEqual(cash-fee,s.Ledger.Balance.Cents);
                Assert.Zero(s.BrandDealTraffic(d),"No second always-on endorsement bonus");
                Assert.True(s.EndKolLive(c.Id));
                Assert.True(s.BrandBusiness.Deals.Single().Ended);
                Assert.IsEmpty(s.ExpoAvailableKols(Brand,s.NextBrandExpoDay));
            }
        }

        [Test] public void LegacyEndorsementMigratesWithoutPaymentOrBackdatedWagesAndKeepsStoryId()
        {
            using(var s=Make())
            {
                var save=Copy(s);int day=s.Clock.Today.TotalDays;
                save.brandBusiness.Deals=new[]{new BrandDeal{Id="old-paid",BrandId=Brand,PersonId="momo",Mode="endorse",Start=day-10,End=day+90,Fee=999999,Response="pause"}};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(s.Ledger.Balance.Cents,loaded.Ledger.Balance.Cents);
                    var c=loaded.KolStreaming.Contracts.Single();
                    Assert.AreEqual((day+90)*1440L,c.EndAt);Assert.False(c.Enabled);
                    Assert.AreEqual(loaded.Clock.Now.TotalMinutes,c.LastPaidThrough);
                    Assert.Zero(c.WagesPaidCents);Assert.AreEqual("old-paid",loaded.BrandBusiness.Deals.Single().Id);
                    using(var again=GameSession.Load(s.City,Copy(loaded)))
                    {Assert.AreEqual(1,again.KolStreaming.Contracts.Length);Assert.AreEqual(loaded.Ledger.Balance.Cents,again.Ledger.Balance.Cents);}
                }
            }
        }

        [Test] public void DualOldContractsMergePerBrandWithoutResettingScheduleOrCharging()
        {
            using(var s=Make())
            {
                Assert.True(McnSigningTest.Sign(s,Brand,"momo"));var c=s.KolStreaming.Contracts.Single();
                Assert.True(s.SetKolSchedule(c.Id,1200,60,false,7,s.Clock.Today.TotalDays));
                var save=Copy(s);
                save.brandBusiness.Deals=new[]{new BrandDeal{Id="old",BrandId=Brand,PersonId="momo",Mode="endorse",Start=0,End=(int)(c.EndAt/1440)+5,Fee=100000}};
                using(var loaded=GameSession.Load(s.City,save))
                {
                    var merged=loaded.KolStreaming.Contracts.Single();Assert.AreEqual(c.Id,merged.Id);
                    Assert.AreEqual(1200,merged.StartMinute);Assert.AreEqual(7,merged.FrequencyDays);Assert.False(merged.Enabled);
                    Assert.AreEqual((c.EndAt/1440+5)*1440L,merged.EndAt);Assert.AreEqual(s.Ledger.Balance.Cents,loaded.Ledger.Balance.Cents);
                    Assert.AreEqual(1,loaded.BrandKolCount(Brand));
                }
            }
        }

        [Test] public void OnlyInvitedAmbassadorsAttendAndShortCampaignDoesNotQualify()
        {
            using(var s=Make())
            {
                Assert.True(McnSigningTest.Sign(s,Brand,"momo"));Assert.True(McnSigningTest.Sign(s,Brand,"ake"));
                Assert.True(s.SignBrandDeal(Brand,"qiao","campaign",false));
                CollectionAssert.DoesNotContain(s.ExpoAvailableKols(Brand,s.NextBrandExpoDay),"qiao");
                Assert.True(s.BookQuarterlyExpo(Brand,s.MainShop.Place.Id,0,new[]{PartCatalog.All.First().Id},new[]{"momo"}));
                CollectionAssert.AreEqual(new[]{"momo"},s.ExpoEndorsers(s.BrandBusiness.Exhibits.Single()));
            }
        }

        [Test] public void ExpiredOldEndorsementIsNotReactivated()
        {
            using(var s=Make())
            {
                var save=Copy(s);save.brandBusiness.Deals=new[]{new BrandDeal{Id="expired",BrandId=Brand,PersonId="momo",Mode="endorse",End=s.Clock.Today.TotalDays}};
                using(var loaded=GameSession.Load(s.City,save))Assert.IsEmpty(loaded.KolStreaming.Contracts);
            }
        }

        [Test] public void BrandSalesDoNotChargeASecondEndorsementCommission()
        {
            using(var s=Make())
            {
                Assert.True(McnSigningTest.Endorse(s,Brand,"momo","endorse",true));
                int day=s.Clock.Today.TotalDays;
                s.Ledger.Credit(new GameDate(day),"零售",Money.FromYuan(100000),"sale",LedgerAttribution.ForShop(s.MainShop.Place.Id));
                long balance=s.Ledger.Balance.Cents;
                s.Clock.Restore(new GameDate(day+1),0);
                typeof(GameSession).GetMethod("TickBrandBusiness",BindingFlags.NonPublic|BindingFlags.Instance)
                    .Invoke(s,new object[]{new GameDate(day),s.Clock.Today});
                Assert.AreEqual(balance,s.Ledger.Balance.Cents);
                Assert.Zero(s.BrandBusiness.Deals.Single().SharePaid);
                typeof(GameSession).GetMethod("SettleKolDailyWages",BindingFlags.NonPublic|BindingFlags.Instance)
                    .Invoke(s,new object[]{s.Clock.Now.TotalMinutes});
                Assert.Less(s.Ledger.Balance.Cents,balance);
                long paid=s.Ledger.Balance.Cents;
                typeof(GameSession).GetMethod("SettleKolDailyWages",BindingFlags.NonPublic|BindingFlags.Instance)
                    .Invoke(s,new object[]{s.Clock.Now.TotalMinutes});
                Assert.AreEqual(paid,s.Ledger.Balance.Cents);
            }
        }
    }
}
