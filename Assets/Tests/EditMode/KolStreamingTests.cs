using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class KolStreamingTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=GameSession.CreateCityLifePreset(city,"brand_expo");s.DebugAddCash(1000000);
            foreach(var d in KolMarket.All)s.Meet(d.Id,25);
            return s;
        }
        private static BrandRecord Brand(GameSession s)=>((BrandBusinessState)typeof(GameSession).GetField("_brands",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s)).Brands.Single();
        private static KolLiveContract Sign(GameSession s,string person="momo")
        {Assert.True(s.SignKolLive("acceptance-expo",person));return s.KolStreaming.Contracts.Last();}
        private static void Ready(GameSession s,string person)
        {
            var p=s.CityPersonState(person);p.Route=null;p.LocationPlaceId=GameSession.CommunityMcnId;p.ActivityId=null;p.VisitId=null;
        }
        private static void At(GameSession s,int minute)=>s.Clock.Restore(s.Clock.Today,minute);
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        [Test] public void AcceptancePresetPreparesContactsButRequiresRealSigningAndPayment()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using(var s=GameSession.CreateCityLifePreset(city,"kol_live"))
            {
                Assert.True(s.IsCityLifeTest);Assert.True(s.BrandManagementUnlocked);Assert.False(s.IsBrandExpoAcceptance);
                Assert.AreEqual(17*60+55,s.Clock.Now.TotalMinutes%1440);Assert.IsEmpty(s.KolStreaming.Contracts);
                Assert.AreEqual(3,s.BrandKolCapacity("acceptance-expo"));
                Assert.True(KolMarket.All.All(d=>s.IsMet(d.Id)));
                long cash=s.Ledger.Balance.Cents;var c=Sign(s);Assert.Less(s.Ledger.Balance.Cents,cash);
                s.AdvanceClock(7);Assert.IsNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(2,s.KolStreaming.Sessions.Single().LiveMinutes);
            }
        }

        [Test] public void MarketHasEnoughDistinctAdultNpcsWithRealPortraitsAndDifferentAptitudes()
        {
            Assert.GreaterOrEqual(KolMarket.All.Length,15);Assert.AreEqual(KolMarket.All.Length,KolMarket.All.Select(d=>d.Id).Distinct().Count());
            foreach(var d in KolMarket.All){Assert.GreaterOrEqual(CityLifeCatalog.Person(d.Id).Age,18);Assert.NotNull(Resources.Load<Texture2D>("PortraitsRetro/"+d.Id));}
            Assert.Greater(KolMarket.Get("ake").Charm,KolMarket.Get("momo").Charm);
            Assert.Greater(KolMarket.Get("momo").Intelligence,KolMarket.Get("ake").Intelligence);
            Assert.Greater(GameSession.KolTrafficContribution(95,150),GameSession.KolTrafficContribution(40,150));
            Assert.Greater(GameSession.KolIntentContribution(95,500),GameSession.KolIntentContribution(40,500));
            Assert.Zero(GameSession.KolIntentContribution(95,0));
        }
        [Test] public void BrandLevelsEnforceThreeSevenTwelveSlotsAndUpgradesCostActualMoney()
        {
            using(var s=Make())
            {
                foreach(var d in KolMarket.All.Take(3))Sign(s,d.Id);
                Assert.False(s.SignKolLive("acceptance-expo",KolMarket.All[3].Id));Assert.AreEqual(3,s.BrandKolCapacity("acceptance-expo"));
                Assert.False(s.UpgradeKolBrand("acceptance-expo"));Brand(s).Influence=300;
                long before=s.Ledger.Balance.Cents;Assert.True(s.UpgradeKolBrand("acceptance-expo"));Assert.AreEqual(before-2000000,s.Ledger.Balance.Cents);
                foreach(var d in KolMarket.All.Skip(3).Take(4))Sign(s,d.Id);
                Assert.AreEqual(7,s.BrandKolCount("acceptance-expo"));Assert.False(s.SignKolLive("acceptance-expo",KolMarket.All[7].Id));
                Assert.True(s.UpgradeKolBrand("acceptance-expo"));foreach(var d in KolMarket.All.Skip(7).Take(5))Sign(s,d.Id);
                Assert.AreEqual(12,s.BrandKolCount("acceptance-expo"));Assert.False(s.SignKolLive("acceptance-expo",KolMarket.All[12].Id));
                Assert.False(s.UpgradeKolBrand("acceptance-expo"));
            }
        }
        [Test] public void ContactDuplicateAndInvalidScheduleRejectWithoutChargingOrChangingContract()
        {
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;Assert.False(s.SignKolLive("bad","momo"));Assert.AreEqual(before,s.Ledger.Balance.Cents);
                var c=Sign(s);before=s.Ledger.Balance.Cents;Assert.False(s.SignKolLive(c.BrandId,c.PersonId));Assert.AreEqual(before,s.Ledger.Balance.Cents);
                Assert.False(s.SetKolSchedule(c.Id,1080,1080,true));Assert.False(s.SetKolSchedule(c.Id,0,900,true));Assert.False(s.SetKolSchedule(c.Id,1081,120,true));
                Assert.AreEqual(1080,s.KolStreaming.Contracts.Single().StartMinute);
            }
            using(var s=DistrictProgressionTests.Make())
            {
                typeof(GameSession).GetField("_brands",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new BrandBusinessState{Brands=new[]{new BrandRecord{Id="b",Kind="computer"}}});
                long before=s.Ledger.Balance.Cents;Assert.False(s.SignKolLive("b","qiao"));Assert.AreEqual(before,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void OvernightScheduleUsesOneSessionAcrossMidnightAndNoOfflineBackfill()
        {
            using(var s=Make())
            {
                var c=Sign(s);Ready(s,"momo");At(s,1079);Assert.NotNull(s.KolLiveBlocker(c.Id));s.AdvanceClock(2);
                Assert.IsNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(1,s.KolStreaming.Sessions.Single().LiveMinutes);
                At(s,1439);Ready(s,"momo");s.AdvanceClock(2);Assert.AreEqual(1,s.KolStreaming.Sessions.Length);Assert.AreEqual(3,s.KolStreaming.Sessions.Single().LiveMinutes);
                At(s,119);Ready(s,"momo");s.AdvanceClock(1);Assert.NotNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(4,s.KolStreaming.Sessions.Single().LiveMinutes);
                s.AdvanceClock(1);Assert.AreEqual(4,s.KolStreaming.Sessions.Single().LiveMinutes);
            }
        }
        [Test] public void EachKolKeepsOwnScheduleAndPausedContractStillOccupiesSlot()
        {
            using(var s=Make())
            {
                var a=Sign(s);var b=Sign(s,"qiao");Ready(s,a.PersonId);Ready(s,b.PersonId);
                Assert.True(s.SetKolSchedule(b.Id,1200,1320,true));At(s,1080);
                Assert.IsNull(s.KolLiveBlocker(a.Id));Assert.NotNull(s.KolLiveBlocker(b.Id));
                Assert.True(s.SetKolSchedule(a.Id,1080,120,false));At(s,1200);
                Assert.NotNull(s.KolLiveBlocker(a.Id));Assert.IsNull(s.KolLiveBlocker(b.Id));Assert.AreEqual(2,s.BrandKolCount(a.BrandId));
            }
        }
        [Test] public void LiveTrafficAndIntentReachActualShopMathAndStopOutsideTheWindow()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);
                double boosted=s.PlayerShop.ArrivalRatePerMinute(s.Clock.Now),intent=s.PlayerShop.MarkupPurchaseIntent(3);
                Assert.Greater(s.KolShopTraffic(s.MainShop),0);Assert.Greater(intent,0);
                Assert.True(s.SetKolSchedule(c.Id,1080,120,false));
                double baseline=s.PlayerShop.ArrivalRatePerMinute(s.Clock.Now);Assert.Greater(boosted,baseline);Assert.Zero(s.PlayerShop.MarkupPurchaseIntent(3));
                Assert.True(s.SetKolSchedule(c.Id,1080,120,true));
                Brand(s).ShopIds=Array.Empty<string>();Assert.Zero(s.KolShopTraffic(s.MainShop));Assert.Zero(s.KolShopIntent(s.MainShop));
            }
        }
        [Test] public void WorkAndActivitiesSuspendLiveBenefitsImmediatelyWithoutEndingContract()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);Assert.Greater(s.KolShopIntent(s.MainShop),0);
                s.CityPersonState(c.PersonId).ActivityId="busy";
                Assert.NotNull(s.KolLiveBlocker(c.Id));Assert.Zero(s.KolShopIntent(s.MainShop));
                s.CityPersonState(c.PersonId).ActivityId=null;Assert.IsNull(s.KolLiveBlocker(c.Id));
                Assert.AreEqual(1,s.BrandKolCount(c.BrandId));
            }
        }
        [Test] public void WatchingSnapshotsDoesNotRunSimulationAndHeadlessRoomDoesNotHoldTime()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);s.Time.SetPaused(false);
                ulong rng=s.Random.State;long cash=s.Ledger.Balance.Cents;
                var previous=UnityEngine.EventSystems.EventSystem.current;var root=new GameObject("KolUiTest");root.SetActive(false);
                var ctrl=root.AddComponent<GameController>();typeof(GameController).GetProperty("Session").SetValue(ctrl,s);typeof(GameController).GetProperty("City").SetValue(ctrl,s.City);
                var ui=new GameUi(ctrl);
                try
                {
                    ui.OpenKolLiveRoom(c.Id);Assert.False(s.Time.IsHeld);Assert.True(s.Time.IsRunning);
                    Assert.True(ui.Canvases.SelectMany(v=>v.GetComponentsInChildren<RawImage>()).Any(p=>p.name=="KolPortrait_momo"&&p.texture!=null));
                    Assert.True(ui.Canvases.SelectMany(v=>v.GetComponentsInChildren<Button>()).Any(b=>b.name=="KolGift_100"));
                    Assert.IsEmpty(s.KolStreaming.Sessions);Assert.AreEqual(rng,s.Random.State);Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                    var snapshot=s.KolStreaming;snapshot.Contracts[0].StartMinute=0;Assert.AreEqual(1080,s.KolStreaming.Contracts[0].StartMinute);
                    var room=((System.Collections.Generic.List<Window>)typeof(GameUi).GetField("_windows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui)).Single();
                    Assert.True(room.LiveConversation);string previousStamp=room.LiveKey();
                    s.AdvanceClock(2);Assert.AreNotEqual(previousStamp,room.LiveKey());room.Rebuild();
                    Assert.AreEqual(2,s.KolStreaming.Sessions.Single().LiveMinutes);Assert.False(s.Time.IsHeld);
                    ui.CloseAllWindows();
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        [Test] public void GiftsDebitOnceBoostViewershipAndSaveCooldownOutcomeAndRng()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);s.AdvanceClock(1);
                int viewers=s.KolRoomViewers(c.Id),fans=s.KolFollowers(c.PersonId);long cash=s.Ledger.Balance.Cents;
                Assert.True(s.TipKolLive(c.Id,100));Assert.AreEqual(cash-10000,s.Ledger.Balance.Cents);
                Assert.Greater(s.KolRoomViewers(c.Id),viewers);Assert.Greater(s.KolFollowers(c.PersonId),fans);
                cash=s.Ledger.Balance.Cents;ulong rng=s.Random.State;
                Assert.False(s.TipKolLive(c.Id,100));Assert.False(s.TipKolLive(c.Id,0));Assert.AreEqual(cash,s.Ledger.Balance.Cents);Assert.AreEqual(rng,s.Random.State);
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(1,loaded.KolStreaming.Sessions.Single().TipCount);Assert.False(loaded.TipKolLive(c.Id,100));
                    long minutes=loaded.KolStreaming.Sessions.Single().LiveMinutes;loaded.AdvanceClock(5);
                    Assert.AreEqual(minutes+5,loaded.KolStreaming.Sessions.Single().LiveMinutes);
                    Assert.AreEqual(10000,loaded.KolStreaming.Sessions.Single().TipsCents);
                }
            }
        }
        [Test] public void ExpiredGiftBoostDoesNotReturnWithANewGiftAndBrandEffectsHaveCaps()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);s.AdvanceClock(1);
                Assert.True(s.TipKolLive(c.Id,1000));s.AdvanceClock(61);Assert.True(s.TipKolLive(c.Id,10));
                Assert.AreEqual(25,s.KolStreaming.Sessions.Single().TipBoost);
                Brand(s).Influence=300;Assert.True(s.UpgradeKolBrand(c.BrandId));Assert.True(s.UpgradeKolBrand(c.BrandId));
                foreach(var d in KolMarket.All.Skip(1).Take(11)){Sign(s,d.Id);Ready(s,d.Id);}
                Assert.LessOrEqual(s.KolBrandTrafficBonus(c.BrandId),.9);Assert.LessOrEqual(s.KolBrandIntentBonus(c.BrandId),.20);
                var save=s.CreateSave();save.kolStreaming=null;
                using(var old=GameSession.Load(s.City,save))
                {Assert.IsEmpty(old.KolStreaming.Contracts);Assert.Zero(old.KolShopTraffic(old.MainShop));}
            }
        }
        [Test] public void NewMarketTalentCanBeIntroducedAndSavedWithoutInventingASecondNpc()
        {
            using(var s=DistrictProgressionTests.Make())
            {
                s.DebugPrepareDistrictPrerequisites(1);
                typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=1,Chapter=1});
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(GameSession.CommunityMcnId));
                s.Clock.Restore(s.Clock.Today,600);Ready(s,"resident_ningxi");
                Assert.False(s.IsMet("resident_ningxi"));Assert.True(s.RequestMcnIntroduction("resident_ningxi"));
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual("resident_ningxi",loaded.McnIntroductions.Single().PersonId);
                    Assert.True(loaded.ExchangeMcnContact("resident_ningxi"));Assert.True(loaded.IsMet("resident_ningxi"));
                    Assert.False(loaded.ExchangeMcnContact("resident_ningxi"));
                }
            }
        }
        [Test] public void EndingAndExpiryReleaseSlotsAndDoNotLeaveTrafficOrPurchasingBoosts()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);Assert.True(s.EndKolLive(c.Id));
                Assert.Zero(s.KolRoomViewers(c.Id));Assert.Zero(s.KolShopIntent(s.MainShop));Assert.Zero(s.BrandKolCount(c.BrandId));
                c=Sign(s);s.Clock.Restore(new GameDate((int)(c.EndAt/1440)),(int)(c.EndAt%1440));
                Assert.NotNull(s.KolLiveBlocker(c.Id));Assert.Zero(s.BrandKolCount(c.BrandId));Assert.False(s.TipKolLive(c.Id,100));
            }
        }
        [Test] public void LongLiveSessionProgressesWithoutPlayerWatchingAndCrossMidnightStateSurvivesReload()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);int influence=Brand(s).Influence;s.AdvanceClock(61);
                Assert.AreEqual(61,s.KolStreaming.Sessions.Single().LiveMinutes);Assert.Greater(Brand(s).Influence,influence);Assert.Greater(s.KolPopularity(c.PersonId),0);
                At(s,1439);Ready(s,c.PersonId);s.AdvanceClock(2);
                using(var loaded=Reload(s))
                {
                    long minutes=loaded.KolStreaming.Sessions.Single().LiveMinutes;loaded.AdvanceClock(1);
                    Assert.AreEqual(minutes+1,loaded.KolStreaming.Sessions.Single().LiveMinutes);Assert.AreEqual(1,loaded.KolStreaming.Sessions.Length);
                }
            }
        }
    }
}
