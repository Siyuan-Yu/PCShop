using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
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
        {Assert.True(McnSigningTest.Sign(s,"acceptance-expo",person));return s.KolStreaming.Contracts.Last();}
        private static void Ready(GameSession s,string person)
        {
            var p=s.CityPersonState(person);p.Route=null;p.LocationPlaceId=GameSession.CommunityMcnId;p.ActivityId=null;p.VisitId=null;
        }
        private static void At(GameSession s,int minute)=>s.Clock.Restore(s.Clock.Today,minute);
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        [Test] public void LiveFeeIsPaidAtOpeningOnlyOnceAcrossMidnightAndReload()
        {
            using (var s = Make())
            {
                var contract = Sign(s);
                long fee = s.KolLiveSessionFee(contract);
                Assert.True(s.SetKolSchedule(contract.Id, 1425, 60, false));
                At(s, 1420);
                s.AdvanceClock(10);
                Assert.False(s.Ledger.Entries.Any(e => e.Category == "KOL直播场次费"));

                Assert.True(s.SetKolSchedule(contract.Id, 1425, 60, true));
                Ready(s, contract.PersonId);
                s.AdvanceClock(5);
                var debit = s.Ledger.Entries.Single(e => e.Category == "KOL直播场次费");
                Assert.AreEqual(-fee, debit.Amount.Cents);
                Assert.AreEqual(contract.BrandId, debit.Attribution.PropertyId);
                using (var loaded = Reload(s))
                {
                    Ready(loaded, contract.PersonId);
                    loaded.AdvanceClock(20);
                    Assert.True(loaded.SetKolSchedule(contract.Id, 1425, 75, true));
                    loaded.AdvanceClock(20);
                    Assert.AreEqual(1, loaded.Ledger.Entries.Count(e => e.Category == "KOL直播场次费"));
                    Assert.False(loaded.Ledger.Entries.Any(e => e.Category == "KOL日薪"));
                    Assert.AreEqual(fee, loaded.KolBrandHistory(contract.BrandId).Single().FeeCents);
                }
            }
        }

        [Test] public void UnfundedLiveStreamWaitsForPaymentWithoutChargingAnIdleContract()
        {
            using (var s = Make())
            {
                var contract = Sign(s);
                At(s, 1080);
                Ready(s, contract.PersonId);
                s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance);
                StringAssert.Contains("资金不足", s.GetKolRoom(contract.PersonId, false).Blocker);
                s.AdvanceClock(1);
                Assert.False(s.KolStreaming.Sessions.Any(session => session.ContractId == contract.Id));
                Assert.False(s.Ledger.Entries.Any(e => e.Category == "KOL直播场次费"));

                s.Ledger.Credit(s.Clock.Today, "测试", Money.FromYuan(10000));
                Ready(s, contract.PersonId);
                s.AdvanceClock(1);
                Assert.AreEqual(-s.KolLiveSessionFee(contract),
                    s.Ledger.Entries.Single(e => e.Category == "KOL直播场次费").Amount.Cents);
            }
        }

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
                s.AdvanceClock(7);Assert.IsNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(2,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
            }
        }

        [Test] public void MarketHasEnoughDistinctAdultNpcsWithRealPortraitsAndDifferentAptitudes()
        {
            Assert.GreaterOrEqual(KolMarket.All.Length,15);Assert.AreEqual(KolMarket.All.Length,KolMarket.All.Select(d=>d.Id).Distinct().Count());
            foreach(var d in KolMarket.All){Assert.GreaterOrEqual(CityLifeCatalog.Person(d.Id).Age,18);Assert.NotNull(Resources.Load<Texture2D>("PortraitsRetro/"+d.Id));}
            Assert.Greater(KolMarket.Get("ake").Charm,KolMarket.Get("momo").Charm);
            Assert.Greater(KolMarket.Get("momo").Intelligence,KolMarket.Get("ake").Intelligence);



        }
        [Test] public void BrandLevelsEnforceThreeSevenTwelveSlotsAndUpgradesCostActualMoney()
        {
            using(var s=Make())
            {
                foreach(var d in KolMarket.All.Take(3))Sign(s,d.Id);
                AssertFull(s, KolMarket.All[3].Id);Assert.AreEqual(3,s.BrandKolCapacity("acceptance-expo"));
                Assert.False(s.UpgradeKolBrand("acceptance-expo"));Brand(s).Influence=300;
                long before=s.Ledger.Balance.Cents;Assert.True(s.UpgradeKolBrand("acceptance-expo"));Assert.AreEqual(before-2000000,s.Ledger.Balance.Cents);
                foreach(var d in KolMarket.All.Skip(3).Take(4)) AssignAndSchedule(s, d.Id);
                Assert.AreEqual(7,s.BrandKolCount("acceptance-expo"));AssertFull(s, KolMarket.All[7].Id);
                Assert.True(s.UpgradeKolBrand("acceptance-expo"));foreach(var d in KolMarket.All.Skip(7).Take(5)) AssignAndSchedule(s, d.Id);
                Assert.AreEqual(12,s.BrandKolCount("acceptance-expo"));AssertFull(s, KolMarket.All[12].Id);
                Assert.False(s.UpgradeKolBrand("acceptance-expo"));
            }
        }
        private static void AssignAndSchedule(GameSession s, string person)
        {
            var contract = s.ActiveKolAmbassador(person);
            if (contract == null) { Sign(s, person); return; }
            Assert.True(s.AssignKolBrand(contract.Id, "acceptance-expo"));
            Assert.True(s.SetKolSchedule(contract.Id, contract.StartMinute, contract.EndMinute, true));
        }

        private static void AssertFull(GameSession s, string person)
        {
            if (s.ActiveKolAmbassador(person) == null)
                Assert.True(s.SignKolAmbassador(person, 90, 1, remote: true, phoneCall: true));
            string id = s.ActiveKolAmbassador(person).Id;
            long before = s.Ledger.Balance.Cents;
            Assert.False(s.AssignKolBrand(id, "acceptance-expo"));
            Assert.AreEqual(before, s.Ledger.Balance.Cents);
        }

        [Test] public void ContactDuplicateAndInvalidScheduleRejectWithoutChargingOrChangingContract()
        {
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;Assert.False(s.SignKolAmbassador("momo",31,1,remote:true,phoneCall:true));Assert.AreEqual(before,s.Ledger.Balance.Cents);
                var c=Sign(s);before=s.Ledger.Balance.Cents;Assert.False(s.SignKolAmbassador(c.PersonId,90,1,remote:true,phoneCall:true));Assert.AreEqual(before,s.Ledger.Balance.Cents);
                Assert.False(s.SetKolSchedule(c.Id,1080,1080,true));Assert.False(s.SetKolSchedule(c.Id,0,900,true));Assert.False(s.SetKolSchedule(c.Id,1081,120,true));
                Assert.AreEqual(1080,s.KolStreaming.Contracts.Single().StartMinute);
            }
            using(var s=DistrictProgressionTests.Make())
            {
                typeof(GameSession).GetField("_brands",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new BrandBusinessState{Brands=new[]{new BrandRecord{Id="b",Kind="computer"}}});
                long before=s.Ledger.Balance.Cents;Assert.False(s.SignKolAmbassador("qiao",90,1,remote:true,phoneCall:true));Assert.AreEqual(before,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void OvernightScheduleUsesOneSessionAcrossMidnightAndNoOfflineBackfill()
        {
            using(var s=Make())
            {
                var c=Sign(s);Ready(s,"momo");At(s,1079);Assert.NotNull(s.KolLiveBlocker(c.Id));s.AdvanceClock(2);
                Assert.IsNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(1,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
                At(s,1439);Ready(s,"momo");s.AdvanceClock(2);Assert.AreEqual(1,s.KolStreaming.Sessions.Count(session => session.ContractId == c.Id));Assert.AreEqual(3,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
                At(s,119);Ready(s,"momo");s.AdvanceClock(1);Assert.NotNull(s.KolLiveBlocker(c.Id));Assert.AreEqual(4,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
                s.AdvanceClock(1);Assert.AreEqual(4,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
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
        [Test] public void BrandRewardsSettleOnceAtEndAndApplyThroughRegularBrandTraffic()
        {
            using (var s = Make())
            {
                var c = Sign(s); At(s,1080); Ready(s,c.PersonId);
                Assert.True(s.SetKolSchedule(c.Id, 1080, 1170, true));
                Brand(s).ShopIds = Array.Empty<string>();
                double traffic = s.BrandTraffic(s.MainShop);
                int fame = Brand(s).Influence, reputation = Brand(s).Reputation;
                s.AdvanceClock(89);
                Assert.AreEqual(fame, Brand(s).Influence);
                Assert.AreEqual(reputation, Brand(s).Reputation);
                Assert.AreEqual(traffic, s.BrandTraffic(s.MainShop));
                using (var loaded = Reload(s))
                {
                    Ready(loaded, c.PersonId);
                    loaded.AdvanceClock(1);
                    var result = loaded.KolBrandHistory(c.BrandId).Single();
                    Assert.Greater(result.InfluenceAfter, fame);
                    Assert.Greater(result.ReputationAfter, reputation);
                    Assert.Greater(loaded.BrandTraffic(loaded.MainShop), traffic);
                    Assert.AreEqual(1, loaded.KolBrandRecentSpecials(c.BrandId));
                    using (var again = Reload(loaded))
                    {
                        again.AdvanceClock(10);
                        Assert.AreEqual(result.InfluenceAfter, Brand(again).Influence);
                        Assert.AreEqual(result.ReputationAfter, Brand(again).Reputation);
                    }
                }
            }
        }

        [TestCase(2, 2)] [TestCase(7, 7)]
        public void SpecialFrequencyPersistsAndLeavesOtherDaysForPersonalStreams(int frequency, int nextOffset)
        {
            using (var s = Make())
            {
                var c = Sign(s);
                int first = s.Clock.Today.TotalDays + 1;
                Assert.True(s.SetKolSchedule(c.Id, 1080, 120, true, frequency, first));
                using (var loaded = Reload(s))
                {
                    var saved = loaded.KolStreaming.Contracts.Single();
                    Assert.AreEqual(frequency, saved.FrequencyDays);
                    Assert.AreEqual((first + nextOffset) * 1440L + 1080, loaded.KolNextSpecialStart(saved, first * 1440L + 1081));
                    loaded.Clock.Restore(new GameDate(first + 1), 1140);
                    Ready(loaded, c.PersonId);
                    Assert.IsNull(loaded.GetKolRoom(c.PersonId).BrandId);
                    Assert.True(loaded.GetKolRoom(c.PersonId).IsLive);
                }
            }
        }

        [Test] public void RecentBrandSpecialsReduceTheNextSettlement()
        {
            using (var s = Make())
            {
                var contract = Sign(s);
                var first = s.Clock.Today;
                Assert.True(s.SetKolSchedule(contract.Id, 1080, 1230, true, 1, first.TotalDays));
                for (int day = 0; day < 4; day++)
                {
                    s.Clock.Restore(first.AddDays(day), 1080);
                    Ready(s, contract.PersonId);
                    s.AdvanceClock(150);
                }
                var reports = s.KolBrandHistory(contract.BrandId);
                Assert.AreEqual(4, reports.Length);
                Assert.AreEqual(4, reports[0].RecentSpecials);
                Assert.AreEqual(reports[3].LiveMinutes, reports[0].LiveMinutes);
                Assert.Less(reports[0].InfluenceAfter - reports[0].InfluenceBefore,
                    reports[3].InfluenceAfter - reports[3].InfluenceBefore);
            }
        }

        [Test] public void PersonalAndRivalRoomsSupportGiftsWithoutAnOwnedContract()
        {
            using (var s = Make())
            {
                int friday = s.Clock.Today.TotalDays / 7 * 7 + 4;
                s.Clock.Restore(new GameDate(friday), KolMarket.Get("qiao").PublicStartMinute);
                Ready(s,"qiao");
                var rival = s.GetKolRoom("qiao");
                Assert.True(rival.IsLive);
                Assert.IsNull(rival.BrandId);
                Assert.AreEqual(s.BrandRivalName("computer"), rival.Sponsor);
                int ownedFame = Brand(s).Influence;
                s.AdvanceClock(1);
                Assert.True(s.TipKolRoom("qiao", 100));
                Assert.False(s.TipKolRoom("qiao", 100));
                s.AdvanceClock(89);
                Assert.Greater(s.KolStreaming.Rivals.Single(r => r.Kind == "computer").Influence, 0);
                Assert.AreEqual(ownedFame, Brand(s).Influence);

                s.Clock.Restore(new GameDate(friday + 1), KolMarket.Get("momo").PublicStartMinute);
                Ready(s,"momo"); s.AdvanceClock(1);
                var room = s.GetKolRoom("momo");
                Assert.True(room.IsLive); Assert.IsNull(room.Sponsor);
                long cash = s.Ledger.Balance.Cents;
                Assert.True(s.TipKolRoom("momo", 10, room.Session.Id));
                Assert.AreEqual(cash - 1000, s.Ledger.Balance.Cents);
            }
        }


        [Test] public void WorkAndActivitiesDelayTheBroadcastWithoutEndingTheContract()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);Assert.IsNull(s.KolLiveBlocker(c.Id));
                s.CityPersonState(c.PersonId).ActivityId="busy";
                Assert.NotNull(s.KolLiveBlocker(c.Id));
                s.CityPersonState(c.PersonId).ActivityId=null;Assert.IsNull(s.KolLiveBlocker(c.Id));
                Assert.AreEqual(1,s.BrandKolCount(c.BrandId));
            }
        }
        [Test] public void WatchingSnapshotsDoesNotRunSimulationAndHeadlessRoomDoesNotHoldTime()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);s.Time.SetPaused(false);
                Assert.True(s.SetKolSchedule(c.Id, 1080, 1170, true));
                ulong rng=s.Random.State;long cash=s.Ledger.Balance.Cents;
                var previous=UnityEngine.EventSystems.EventSystem.current;var root=new GameObject("KolUiTest");root.SetActive(false);
                var ctrl=root.AddComponent<GameController>();typeof(GameController).GetProperty("Session").SetValue(ctrl,s);typeof(GameController).GetProperty("City").SetValue(ctrl,s.City);
                var ui=new GameUi(ctrl);
                try
                {
                    ui.OpenKolLiveRoom(c.Id);Assert.False(s.Time.IsHeld);Assert.True(s.Time.IsRunning);
                    Assert.True(ui.Canvases.SelectMany(v=>v.GetComponentsInChildren<RawImage>()).Any(p=>p.name=="KolPortrait_momo"&&p.texture!=null));
                    Assert.True(ui.Canvases.SelectMany(v=>v.GetComponentsInChildren<Button>()).Any(b=>b.name=="KolOpenGifts"));
                    Assert.False(s.KolStreaming.Sessions.Any(session => session.ContractId == c.Id));Assert.AreEqual(rng,s.Random.State);Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                    var snapshot=s.KolStreaming;snapshot.Contracts[0].StartMinute=0;Assert.AreEqual(1080,s.KolStreaming.Contracts[0].StartMinute);
                    var room=((System.Collections.Generic.List<Window>)typeof(GameUi).GetField("_windows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui)).Single();
                    Assert.True(room.LiveConversation);string previousStamp=room.LiveKey();
                    s.AdvanceClock(2);Assert.AreNotEqual(previousStamp,room.LiveKey());
                    Assert.AreEqual(2,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);Assert.False(s.Time.IsHeld);
                    s.AdvanceClock(88);
                    room.Rebuild();
                    var result = s.KolBrandHistory(c.BrandId).Single();
                    var labels = room.Content.GetComponentsInChildren<Text>().Select(label => label.text).ToArray();
                    CollectionAssert.Contains(labels, "本场品牌结算 · " + Brand(s).Name);
                    CollectionAssert.Contains(labels, "品牌知名度");
                    CollectionAssert.Contains(labels, "品牌口碑");
                    CollectionAssert.Contains(labels, $"{result.InfluenceBefore} → {result.InfluenceAfter}   (+{result.InfluenceAfter - result.InfluenceBefore})");
                    CollectionAssert.Contains(labels, $"{result.ReputationBefore} → {result.ReputationAfter}   (+{result.ReputationAfter - result.ReputationBefore})");
                    room.Rebuild();
                    Assert.AreEqual(result.InfluenceAfter, Brand(s).Influence);
                    Assert.AreEqual(result.ReputationAfter, Brand(s).Reputation);
                    ui.CloseAllWindows();
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }

        [Test] public void PhoneAppShowsOfflineRoomsAndCanSendAConfirmedGiftToAnUnsignedHost()
        {
            using (var s = Make())
            {
                s.SetSmartphone(true);
                Assert.True(s.UninstallApp(PhoneApps.Live));
                At(s, 1140);
                Ready(s, "momo");
                s.AdvanceClock(1);
                var previous = UnityEngine.EventSystems.EventSystem.current;
                var root = new GameObject("KolPhoneTest"); root.SetActive(false);
                var ctrl = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(ctrl, s);
                typeof(GameController).GetProperty("City").SetValue(ctrl, s.City);
                var ui = new GameUi(ctrl);
                Button Find(string name) => ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Button>()).Single(b => b.name == name);
                try
                {
                    ui.OpenPhoneStore();
                    Find("PhoneDownload_live").onClick.Invoke();
                    ui.ClosePhone();
                    ui.OpenPhone();
                    Find("PhoneApp_live").onClick.Invoke();
                    Assert.False(s.Time.IsHeld);
                    Assert.False(Find("KolRoom_qiao").interactable);
                    Assert.True(Find("KolRoom_momo").interactable);
                    Find("KolRoom_momo").onClick.Invoke();
                    Assert.False(s.Time.IsHeld);
                    Assert.True(ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<RawImage>())
                        .Any(raw => raw.name == "KolStudioArt" && raw.texture != null));
                    long before = s.Ledger.Balance.Cents;
                    Find("KolOpenGifts").onClick.Invoke();
                    Assert.True(s.Time.IsHeld);
                    Assert.AreEqual(before, s.Ledger.Balance.Cents);
                    Find("KolConfirmGift").onClick.Invoke();
                    Assert.False(s.Time.IsHeld);
                    Assert.AreEqual(before - 10000, s.Ledger.Balance.Cents);
                    Assert.False(Find("KolOpenGifts").interactable);

                    ui.CloseAllWindows();
                    int today = s.Clock.Today.TotalDays;
                    int friday = today + (4 - today % 7 + 7) % 7;
                    s.Clock.Restore(new GameDate(friday), KolMarket.Get("qiao").PublicStartMinute);
                    Ready(s, "qiao");
                    ui.OpenKolLobby();
                    Find("KolRoom_qiao").onClick.Invoke();
                    int ownInfluence = Brand(s).Influence;
                    s.AdvanceClock(90);
                    ui.RebuildWindows();
                    var labels = ui.Canvases.SelectMany(canvas => canvas.GetComponentsInChildren<Text>()).Select(label => label.text).ToArray();
                    CollectionAssert.Contains(labels, "本场品牌结算 · " + s.BrandRivalName("computer"));
                    CollectionAssert.Contains(labels, "品牌知名度");
                    CollectionAssert.Contains(labels, "品牌口碑");
                    Assert.AreEqual(ownInfluence, Brand(s).Influence);
                }
                finally
                {
                    ui.CloseAllWindows();
                    foreach (var canvas in ui.Canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                        UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
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
                    Assert.AreEqual(1,loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).TipCount);Assert.False(loaded.TipKolLive(c.Id,100));
                    long minutes=loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes;loaded.AdvanceClock(5);
                    Assert.AreEqual(minutes+5,loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);
                    Assert.AreEqual(10000,loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).TipsCents);
                }
            }
        }
        [Test] public void ExpiredGiftBoostDoesNotReturnWithANewGift()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);s.AdvanceClock(1);
                Assert.True(s.TipKolLive(c.Id,1000));s.AdvanceClock(61);Assert.True(s.TipKolLive(c.Id,10));
                Assert.AreEqual(25,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).TipBoost);
                Brand(s).Influence=300;Assert.True(s.UpgradeKolBrand(c.BrandId));Assert.True(s.UpgradeKolBrand(c.BrandId));
                foreach(var d in KolMarket.All.Skip(1).Take(11)){Sign(s,d.Id);Ready(s,d.Id);}
                Assert.AreEqual(12, s.BrandKolCount(c.BrandId));
                var save=s.CreateSave();save.kolStreaming=null;
                using(var old=GameSession.Load(s.City,save))
                {Assert.IsEmpty(old.KolStreaming.Contracts);Assert.IsEmpty(old.KolStreaming.Sessions);}
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
                    Assert.True(loaded.IsMet("resident_ningxi"));
                    Assert.AreEqual(1, loaded.CityPeople.Count(p => p.PersonId == "resident_ningxi"));
                    Assert.False(loaded.RequestMcnIntroduction("resident_ningxi"));
                    Assert.AreEqual(s.Ledger.Balance.Cents, loaded.Ledger.Balance.Cents);
                }
            }
        }
        [Test] public void EndingAndExpiryReleaseSlotsAndStopTheBroadcast()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);Assert.True(s.EndKolLive(c.Id));
                Assert.Zero(s.KolRoomViewers(c.Id));Assert.Zero(s.BrandKolCount(c.BrandId));
                c=Sign(s);s.Clock.Restore(new GameDate((int)(c.EndAt/1440)),(int)(c.EndAt%1440));
                Assert.NotNull(s.KolLiveBlocker(c.Id));Assert.Zero(s.BrandKolCount(c.BrandId));Assert.False(s.TipKolLive(c.Id,100));
            }
        }
        [Test] public void LongLiveSessionProgressesWithoutPlayerWatchingAndCrossMidnightStateSurvivesReload()
        {
            using(var s=Make())
            {
                var c=Sign(s);At(s,1080);Ready(s,c.PersonId);int influence=Brand(s).Influence;s.AdvanceClock(61);
                Assert.AreEqual(61,s.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);Assert.AreEqual(influence,Brand(s).Influence);Assert.Greater(s.KolPopularity(c.PersonId),0);
                At(s,1439);Ready(s,c.PersonId);s.AdvanceClock(2);
                using(var loaded=Reload(s))
                {
                    long minutes=loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes;loaded.AdvanceClock(1);
                    Assert.AreEqual(minutes+1,loaded.KolStreaming.Sessions.Single(session => session.ContractId == c.Id).LiveMinutes);Assert.AreEqual(1,loaded.KolStreaming.Sessions.Count(session => session.ContractId == c.Id));
                }
            }
        }
    }
}
