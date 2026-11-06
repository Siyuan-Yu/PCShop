using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Common;
using PCShopSell.Game;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class QuarterlyExpoTests
    {
        private static GameSession Make()=>GameSession.CreateCityLifePreset(CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)),"quarter_expo");
        private static BrandRecord Brand(GameSession s)=>((BrandBusinessState)typeof(GameSession).GetField("_brands",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s)).Brands.Single();
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(id));
        private static void Day(GameSession s,int day,int minute=540)=>s.Clock.Restore(new GameDate(day),minute);

        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static string[] Kols(GameSession s,int n)
        {
            if(n>3)Assert.True(s.UpgradeKolBrand("acceptance-expo"));if(n>7)Assert.True(s.UpgradeKolBrand("acceptance-expo"));
            var ids=KolMarket.All.Take(n).Select(k=>k.Id).ToArray();foreach(string id in ids)Assert.True(McnSigningTest.Sign(s,"acceptance-expo",id));return ids;
        }
        private static BrandExhibit Book(GameSession s,int size=0)
        {
            string[] ids=Kols(s,GameSession.ExpoMinKols(size));var products=PartCatalog.All.Take(GameSession.ExpoMinProducts(size)).Select(p=>p.Id).ToArray();
            Assert.True(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,size,products,ids));return s.BrandBusiness.Exhibits.Single();
        }
        private static void PrepareStart(GameSession s,BrandExhibit e)
        {
            Day(s,e.Day);At(s,"life_expo");s.Player.Energy=s.Player.MaxEnergy;s.Player.Health=100;
            foreach(string person in e.KolIds)
            {
                var p=s.CityPersonState(person);p.LocationPlaceId="life_expo";p.Route=null;p.ActivityId=null;p.VisitId=null;
            }
        }
        [Test] public void VenueCannotBeLeasedBoughtOrOpenedAsABusinessEvenFromAnInjectedListing()
        {
            using(var s=Make())
            {
                Assert.False(s.City.GetPlace("life_expo").IsTradable);Assert.False(s.CityCommercialProperties.Any(p=>p.Id=="life_expo"));
                var l=s.DebugAddListing("life_expo",68,90,Money.FromYuan(100));Assert.NotNull(s.LeaseBlocker(l,true));
                Assert.NotNull(s.CityPropertyPurchaseBlocker("life_expo",false));Assert.NotNull(s.VentureBlocker("life_expo","stay"));
                using(var loaded=Reload(s))Assert.False(loaded.Listings.Any(x=>x.PlaceId=="life_expo"));
            }
        }
        [Test] public void QuarterlyScheduleSpansFourDaysAndAdvancesByNinetyDays()
        {
            using(var s=Make())
            {
                Assert.AreEqual(75,s.NextBrandExpoDay);Day(s,75);Assert.True(s.CityExpoOpen(false));
                Day(s,78);Assert.True(s.CityExpoOpen(false));Day(s,79);Assert.False(s.CityExpoOpen(false));Assert.AreEqual(165,s.NextBrandExpoDay);
                Day(s,165);Assert.True(s.CityExpoOpen(false));
            }
        }
        [Test] public void BookingUsesOneHundredOfEachProductAndOnlySignedKolsAndIsAtomic()
        {
            using(var s=Make())
            {
                string p=PartCatalog.All.First().Id;long cash=s.Ledger.Balance.Cents;int stock=s.MainShop.Sim.Stock.Count(PartCatalog.Get(p),true);
                Assert.False(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,0,new[]{p},new[]{"momo"}));
                Assert.AreEqual(cash,s.Ledger.Balance.Cents);Assert.AreEqual(stock,s.MainShop.Sim.Stock.Count(PartCatalog.Get(p),true));
                var ids=Kols(s,1);cash=s.Ledger.Balance.Cents;
                Assert.False(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,0,new[]{p,p},ids));
                Assert.True(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,0,new[]{p},ids));
                Assert.AreEqual(cash-500000,s.Ledger.Balance.Cents);Assert.AreEqual(stock-100,s.MainShop.Sim.Stock.Count(PartCatalog.Get(p),true));
                cash=s.Ledger.Balance.Cents;Assert.False(s.BookQuarterlyExpo("acceptance-expo",s.MainShop.Place.Id,0,new[]{p},ids));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                var snapshot=s.BrandBusiness;snapshot.Exhibits[0].Products[0].Sold=99;snapshot.Exhibits[0].KolIds[0]="bad";
                Assert.Zero(s.BrandBusiness.Exhibits[0].Products[0].Sold);Assert.AreEqual("momo",s.BrandBusiness.Exhibits[0].KolIds[0]);
            }
        }
        [TestCase(1,4,5)] [TestCase(2,6,8)]
        public void LargerBoothsRequireMoreProductsSignedTalentAndAnActualPosterCommission(int size,int products,int kols)
        {
            using(var s=Make())
            {
                Assert.AreEqual(products,GameSession.ExpoMinProducts(size));Assert.AreEqual(kols,GameSession.ExpoMinKols(size));
                var e=Book(s,size);Assert.False(s.CommissionExpoPoster(e.Id,"life_ming"));
                long cash=s.Ledger.Balance.Cents;Assert.True(s.CommissionExpoPoster(e.Id,"life_designer"));
                Assert.AreEqual(cash-GameSession.ExpoPosterFee(size)*100,s.Ledger.Balance.Cents);Assert.False(s.CommissionExpoPoster(e.Id,"life_designer"));
                e=s.BrandBusiness.Exhibits.Single();Assert.AreEqual(s.Clock.Now.TotalMinutes+1440,e.PosterReadyAt);
                PrepareStart(s,e);Assert.IsNull(s.ExpoStartBlocker(e.Id));Assert.True(s.StartQuarterlyExpo(e.Id));
            }
        }
        [Test] public void ContactOrFriendshipCannotSubstituteForAContractAndExpiryMustCoverClosing()
        {
            using(var s=Make())
            {
                var ids=Kols(s,1);var c=s.KolStreaming.Contracts.Single();Assert.True(s.EndKolLive(c.Id));
                Assert.False(s.ExpoAvailableKols("acceptance-expo",75).Contains(ids[0]));
                Day(s,150);Assert.True(McnSigningTest.Sign(s,"acceptance-expo","momo"));
                Assert.False(s.ExpoAvailableKols("acceptance-expo",250).Contains("momo"));
            }
        }
        [Test] public void PosterContactAndDeadlineAndProductCapacityAreValidatedBeforePayment()
        {
            using(var s=Make())
            {
                var e=Book(s,1);Day(s,e.Day-1,600);long cash=s.Ledger.Balance.Cents;
                Assert.False(s.CommissionExpoPoster(e.Id,"life_designer"));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                PrepareStart(s,e);Assert.NotNull(s.ExpoStartBlocker(e.Id));
            }
        }
        [Test] public void InvalidatedContractRequiresReplacementAndActiveExhibitLocksTermination()
        {
            using(var s=Make())
            {
                var e=Book(s);string old=e.KolIds.Single();Assert.True(s.EndKolLive(s.KolStreaming.Contracts.Single().Id));
                Assert.True(McnSigningTest.Sign(s,e.BrandId,"ake"));Assert.True(s.ReplaceExpoKol(e.Id,old,"ake"));
                e=s.BrandBusiness.Exhibits.Single();PrepareStart(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));
                Assert.False(s.EndKolLive(s.KolStreaming.Contracts.Last().Id));Assert.False(s.ReplaceExpoKol(e.Id,"ake","qiao"));
                Assert.False(s.BeginCityTravel(s.Home.Id));Assert.False(s.Enter(s.Home));s.Leave();Assert.AreEqual("life_expo",s.CurrentPlace.Id);
                long time=s.Clock.Now.TotalMinutes;s.AdvanceClock(180);Assert.AreEqual(time,s.Clock.Now.TotalMinutes);
                Assert.False(s.StartPlayerWork());Assert.False(s.ActBrandExpo(e.Id,"buyers"));
            }
        }
        [Test] public void FullFourDaysNeedTwentyFourChoicesPayFourAttendanceDaysAndReturnActualUnsoldStock()
        {
            using(var s=Make())
            {
                // An unfitted source shop isolates reserved goods from ordinary store sales.
                s.MainShop.Layout.Clear();typeof(GameSession).GetMethod("ApplyUpgrades",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,null);
                var e=Book(s);PrepareStart(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));Assert.False(s.StartQuarterlyExpo(e.Id));
                for(int i=0;i<24;i++)
                {
                    var m=s.CurrentExpoMoment(e.Id);int best=Array.IndexOf(m.Scores,m.Scores.Max());Assert.True(s.ChooseExpoAction(e.Id,best));
                    Assert.AreEqual(i+1,s.BrandBusiness.Exhibits.Single().Decision);
                }
                e=s.BrandBusiness.Exhibits.Single();Assert.True(e.Finished&&e.StockReturned);Assert.False(s.BrandExpoExclusive);
                Assert.AreEqual(78,s.Clock.Today.TotalDays);Assert.AreEqual(1020,s.Clock.MinuteOfDay);Assert.AreEqual(4,e.AttendanceDays);
                Assert.AreEqual(24,e.Journal.Length);Assert.Greater(e.RevenueCents,0);Assert.Greater(Brand(s).Influence,320);
                Assert.AreEqual(e.KolDayQuotes.Sum() * 4 * 100, e.ExpoWagesCents);
                Assert.AreEqual(4, s.Ledger.Entries.Count(x => x.Category == "展会KOL站台"));
                Assert.AreEqual(150-e.Products.Single().Sold,s.MainShop.Sim.Stock.Count(PartCatalog.Get(e.ProductId),true));
                Assert.AreEqual(e.RevenueCents,s.Ledger.Entries.Where(x=>x.Category=="展会零售").Sum(x=>x.Amount.Cents));
                Assert.AreEqual(1,s.BrandBusiness.Orders.Length);long cash=s.Ledger.Balance.Cents;Assert.False(s.ChooseExpoAction(e.Id,0));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.GreaterOrEqual(s.Player.Health,30);Assert.GreaterOrEqual(s.Player.Energy,1);
            }
        }
        [Test] public void SaveDuringTheShowKeepsCurrentDecisionMoneyStockWagesAndDoesNotReplay()
        {
            using(var s=Make())
            {
                var e=Book(s);PrepareStart(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));for(int i=0;i<7;i++)Assert.True(s.ChooseExpoAction(e.Id,0));
                e=s.BrandBusiness.Exhibits.Single();long cash=s.Ledger.Balance.Cents;
                using(var loaded=Reload(s))
                {
                    Assert.True(loaded.BrandExpoExclusive);Assert.AreEqual(7,loaded.ActiveBrandExpo.Decision);Assert.AreEqual(cash,loaded.Ledger.Balance.Cents);
                    Assert.AreEqual(e.ExpoWagesCents,loaded.ActiveBrandExpo.ExpoWagesCents);Assert.AreEqual(e.RevenueCents,loaded.ActiveBrandExpo.RevenueCents);
                    Assert.True(loaded.ChooseExpoAction(e.Id,1));Assert.AreEqual(8,loaded.ActiveBrandExpo.Decision);
                }
            }
        }
        [Test] public void PoorDecisionsProduceLessTrustAndNoGuaranteedProcurementOrder()
        {
            using(var s=Make())
            {
                var e=Book(s);PrepareStart(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));
                for(int i=0;i<24;i++){var m=s.CurrentExpoMoment(e.Id);Assert.True(s.ChooseExpoAction(e.Id,Array.IndexOf(m.Scores,m.Scores.Min())));}
                e=s.BrandBusiness.Exhibits.Single();Assert.True(e.Finished);Assert.Less(e.Trust,30);Assert.IsEmpty(s.BrandBusiness.Orders);Assert.LessOrEqual(e.Products.Single().Sold,100);
            }
        }
        [Test] public void MissingTheOpeningReturnsStockOnceButDoesNotRefundTheBooth()
        {
            using(var s=Make())
            {
                var e=Book(s);long cash=s.Ledger.Balance.Cents;Day(s,e.Day+1);
                var tick=typeof(GameSession).GetMethod("SettleQuarterlyExpoAbsence",BindingFlags.NonPublic|BindingFlags.Instance);
                tick.Invoke(s,new object[]{e.Day+1});tick.Invoke(s,new object[]{e.Day+1});
                Assert.True(s.BrandBusiness.Exhibits.Single().Missed);Assert.AreEqual(150,s.MainShop.Sim.Stock.Count(PartCatalog.Get(e.ProductId),true));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void HeadlessShowHasThreeChoicesKeepsTimeHeldAndCanResumeFromASavedDecision()
        {
            using(var s=Make())
            {
                var e=Book(s);PrepareStart(s,e);Assert.True(s.StartQuarterlyExpo(e.Id));
                var previous=UnityEngine.EventSystems.EventSystem.current;var root=new GameObject("QuarterExpoUiTest");root.SetActive(false);var ctrl=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(ctrl,s);typeof(GameController).GetProperty("City").SetValue(ctrl,s.City);var ui=new GameUi(ctrl);
                try
                {
                    ui.OpenQuarterlyExpo(e.Id);Assert.True(s.Time.IsHeld);
                    var buttons=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Where(b=>b.name.StartsWith("ExpoChoice_")).ToArray();Assert.AreEqual(3,buttons.Length);
                    buttons[0].onClick.Invoke();Assert.AreEqual(1,s.ActiveBrandExpo.Decision);Assert.True(s.Time.IsHeld);
                    var windows=(System.Collections.Generic.List<Window>)typeof(GameUi).GetField("_windows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);Assert.False(windows.Single().Closable);
                }
                finally{ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(root);if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);}
            }
        }
    }
}
