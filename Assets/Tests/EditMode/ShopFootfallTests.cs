using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class ShopFootfallTests
    {
        private static CityLayout City(double station=100,double housing=500,string size="small",bool connected=true,bool block=false)
        {
            return CityLayout.FromData(new CityLayoutData
            {
                nodes=new[]{new LayoutNodeData{id="shop",x=0,y=0},new LayoutNodeData{id="station",x=10,y=0},new LayoutNodeData{id="home",x=0,y=10}},
                edges=connected?new[]{new LayoutEdgeData{a="shop",b="station",length=station},new LayoutEdgeData{a="shop",b="home",length=housing}}:Array.Empty<LayoutEdgeData>(),
                places=new[]{new LayoutPlaceData{id="pc",name="铺面",kind="shop",node="shop",size=size,player=true},
                    new LayoutPlaceData{id="tea",name="待租铺面",kind="shop",node="home",size="small"},
                    new LayoutPlaceData{id="spare",name="空置铺面",kind="shop",node="station",size="small"},
                    new LayoutPlaceData{id="metro",name="地铁站",kind="station",node="station"},
                    new LayoutPlaceData{id="flat",name="住宅",kind="home",node="home"},new LayoutPlaceData{id="agency",name="中介",kind="agency",node="shop"}},
                blocks=block?new[]{new LayoutBlockData{model="apartment",x=0,y=10}}:Array.Empty<LayoutBlockData>()
            });
        }
        private static GameSession Session(CityLayout city,bool rented=true)=>new GameSession(city,7,Money.FromYuan(500000),GameTimestamp.From(new GameDate(0),600),startWithShop:rented){EventsEnabled=false};
        private static ShopFootfallEstimate Estimate(CityLayout city)=>new ShopFootfall(city).For(city.PlayerShop());

        [Test] public void CloserMetroAndHousingEachIncreaseDailyPeopleAndRent()
        {
            var far=Estimate(City(1000,1000));var metro=Estimate(City(50,1000));var home=Estimate(City(1000,50));
            Assert.Greater(metro.DailyPassers,far.DailyPassers);Assert.Greater(home.DailyPassers,far.DailyPassers);
            Assert.Greater(metro.MonthlyRent.Cents,far.MonthlyRent.Cents);Assert.Greater(home.MonthlyRent.Cents,far.MonthlyRent.Cents);
            Assert.AreEqual(50,metro.StationMeters);Assert.AreEqual(50,home.HousingMeters);
            Assert.Greater(Estimate(City(1000,50,block:true)).DailyPassers,home.DailyPassers,"填充住宅也提供周边住户来源");
        }
        [Test] public void LargerPremisesHaveMorePassingPeopleAndStrictlyHigherRent()
        {
            var estimates=new[]{"small","medium","large"}.Select(size=>Estimate(City(size:size))).ToArray();
            for(int i=0;i<estimates.Length;i++)
            {
                Assert.AreEqual(estimates[i].DailyPassers*5,estimates[i].MonthlyRent.Yuan);
                if(i>0){Assert.Greater(estimates[i].DailyPassers,estimates[i-1].DailyPassers);Assert.Greater(estimates[i].MonthlyRent.Cents,estimates[i-1].MonthlyRent.Cents);}
            }
        }
        [Test] public void DistancesFollowRoadsAndUnreachableSourcesDoNotCountAsNearby()
        {
            var road=Estimate(City(2000,3000));Assert.AreEqual(2000,road.StationMeters);Assert.AreEqual(3000,road.HousingMeters);
            var disconnected=Estimate(City(2000,3000,connected:false));
            Assert.True(double.IsPositiveInfinity(disconnected.StationMeters));Assert.True(double.IsPositiveInfinity(disconnected.HousingMeters));
            Assert.Less(disconnected.DailyPassers,road.DailyPassers);
        }
        [Test] public void RetailArrivalRateUsesTheEstimateWithoutDoubleCountingShopSize()
        {
            using(var small=Session(City(1000,1000)))using(var large=Session(City(50,50,"large")))
            {
                double a=small.PlayerShop.ArrivalRatePerMinute(small.Clock.Now),b=large.PlayerShop.ArrivalRatePerMinute(large.Clock.Now);
                double ratio=(double)large.FootfallOf(large.PlayerShopPlace).DailyPassers/small.FootfallOf(small.PlayerShopPlace).DailyPassers;
                Assert.AreEqual(ratio,b/a,1e-9);Assert.Greater(b,a);
            }
        }
        [TestCase("computer")]
        [TestCase(PetShopCatalog.ShopKind)]
        public void NewShopEstimateMatchesAFullDayOfArrivalsAndGrowsWithReputation(string kind)
        {
            var city = City();
            using (var agency = Session(city, rented:false))
            using (var live = Session(city))
            {
                double estimate = agency.ExpectedDailyShopEntrants(agency.PlayerShopPlace, "computer");
                Assert.That(agency.ExpectedDailyShopEntrants(agency.PlayerShopPlace, kind), Is.EqualTo(estimate).Within(1e-9));
                GameSessionTests.Teleport(live, live.PlayerShopPlace.Id);
                Assert.True(live.SetShopMainCategory(kind));
                var shop = live.PlayerShop;
                shop.ForcedClosed = false;
                shop.NotFitted = false;
                shop.PlayerPresent = true;
                live.Clock.Restore(live.Clock.Today, ShopSimulation.DefaultOpenMinute);
                live.Clock.Advance(ShopSimulation.DefaultCloseMinute - ShopSimulation.DefaultOpenMinute);
                Assert.That(shop.TodayCustomers, Is.EqualTo(estimate).Within(2));

                shop.Reputation = 60;
                Assert.Greater(live.StreetTrafficMultiplier(live.PlayerShopPlace.StreetId), 1);
                Assert.Greater(live.ExpectedDailyShopEntrants(live.PlayerShopPlace), estimate);
            }
        }
        [Test] public void ActualLakeMapHasOrderedQuotesAffordableStartAndCachedEstimates()
        {
            var data=JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text);
            using(var s=Session(CityLayout.FromData(data),false))
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var estimates=s.City.OfKind(PlaceKind.Shop).Select(s.FootfallOf).OrderBy(e=>e.DailyPassers).ToArray();
                for(int i=1;i<estimates.Length;i++)Assert.AreEqual(estimates[i].DailyPassers*5,estimates[i].MonthlyRent.Yuan);
                long initialMs=watch.ElapsedMilliseconds;var first=s.FootfallOf(s.PlayerShopPlace);
                for(int i=0;i<10000;i++)Assert.AreSame(first,s.FootfallOf(s.PlayerShopPlace));
                var starterRent = s.ShopRentQuote(s.PlayerShopPlace);
                var busierShop = s.City.GetPlace("expansion_shop_s_start_04");
                double rentRatio = (double)s.ShopRentQuote(busierShop).Cents / starterRent.Cents;
                double trafficRatio = s.ExpectedDailyShopEntrants(busierShop) / s.ExpectedDailyShopEntrants(s.PlayerShopPlace);
                Assert.Less(RealEstate.SignCost(starterRent).Yuan,10000,"万元开局能租起首间店");
                Assert.Greater(rentRatio, 1.35, "同面积客流接近翻倍，租金需明显拉开");
                Assert.Less(rentRatio, trafficRatio, "租金涨幅应低于客流涨幅");
                Assert.True(s.Listings.All(l=>l.Rent==s.ShopRentQuote(s.ListingPlace(l))));
                TestContext.WriteLine($"Lake quote count={estimates.Length}; first-pass={initialMs}ms; starter={first.DailyPassers} people/day, rent={starterRent.Yuan}/week; range={estimates.First().DailyPassers}-{estimates.Last().DailyPassers}.");
            }
        }
        [TestCase("restaurant")] [TestCase("secondhand")]
        public void SpecializedShopsUseFootfallForVisitFrequency(string kind)
        {
            long Interval(string size)
            {
                using(var s=Session(City(size:size)))
                {
                    GameSessionTests.Teleport(s,"pc");Assert.True(s.SetShopMainCategory(kind));s.CurrentShop.Layout.Clear();
                    var ids = GameSession.OpeningFurniture(kind);
                    for(int i=0;i<ids.Length;i++)s.CurrentShop.Layout.Add(new PCShopSell.Core.Home.PlacedFurniture{Id=ids[i],X=1+i,Z=2});
                    s.PlayerShop.ForcedClosed=false;var state=s.SpecialBusiness("pc");state.NextVisit=s.Clock.Now.TotalMinutes;
                    s.AdvanceClock(1);return state.NextVisit-s.Clock.Now.TotalMinutes;
                }
            }
            long small=Interval("small"),large=Interval("large");Assert.Greater(small,0);Assert.Greater(large,0);Assert.Less(large,small);
        }
        [Test] public void ListingQuoteSigningAndJsonReloadAgree()
        {
            using(var s=Session(City(),false))
            {
                var quote=s.Listings.Single(l=>l.PlaceId=="pc").Rent;
                Assert.AreEqual(s.ShopRentQuote(s.PlayerShopPlace),quote);
                GameSessionTests.Teleport(s,"agency");long cash=s.Ledger.Balance.Cents;
                Assert.True(s.SignLease("pc"));Assert.AreEqual(quote,s.Lease.Rent);
                Assert.AreEqual(RealEstate.SignCost(quote).Cents,cash-s.Ledger.Balance.Cents);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {Assert.AreEqual(quote,loaded.Lease.Rent);Assert.AreEqual(s.FootfallOf(s.PlayerShopPlace).DailyPassers,loaded.FootfallOf(loaded.PlayerShopPlace).DailyPassers);}
            }
        }
        [TestCase(0)] [TestCase(2)]
        public void OldSaveRequotesListingsAndPreservesWeeklyContracts(int version)
        {
            using(var s=Session(City()))
            {
                s.DebugAddListing("spare",0,60,Money.FromYuan(9999));var data=s.CreateSave();
                data.shopFootfallVersion=version;data.leaseRentCents=123400;data.leaseDepositCents=123400;
                data.pendingPlace="tea";data.pendingRentCents=234500;data.pendingDepositCents=234500;data.pendingStartDay=40;
                using(var loaded=GameSession.Load(s.City,data))
                {
                    Assert.AreEqual(version == 2 ? Money.FromCents(123400) : loaded.ShopRentQuote(loaded.PlayerShopPlace), loaded.Lease.Rent);
                    Assert.AreEqual(version == 2 ? Money.FromCents(234500) : loaded.ShopRentQuote(loaded.City.GetPlace("tea")), loaded.PendingLease.Rent);
                    Assert.AreEqual(1234,loaded.Lease.Deposit.Yuan);Assert.AreEqual(2345,loaded.PendingLease.Deposit.Yuan);
                    Assert.AreEqual(loaded.ShopRentQuote(loaded.City.GetPlace("spare")),loaded.Listings.Single(l=>l.PlaceId=="spare").Rent);
                    Assert.AreEqual(s.Ledger.Balance,loaded.Ledger.Balance);Assert.AreEqual(3,loaded.CreateSave().shopFootfallVersion);
                    using(var twice=GameSession.Load(s.City,loaded.CreateSave()))Assert.AreEqual(loaded.Lease.Rent,twice.Lease.Rent);
                }
            }
        }
        [Test] public void AgencyDetailDisplaysPeoplePerDayAndDistancesWithoutTrafficMultiplier()
        {
            using(var s=Session(City(),false))
            {
                GameSessionTests.Teleport(s,"agency");var root=new GameObject("FootfallUiTest");root.SetActive(false);
                var controller=root.AddComponent<GameController>();typeof(GameController).GetProperty("Session").SetValue(controller,s);typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                var detail=new GameObject("AgencyDetail",typeof(RectTransform));
                try
                {
                    typeof(GameUi).GetMethod("BuildAgencyDetail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{detail.GetComponent<RectTransform>(),s.PlayerShopPlace,1,null});
                    var texts=detail.GetComponentsInChildren<Text>();
                    StringAssert.Contains($"{s.ExpectedDailyShopEntrants(s.PlayerShopPlace):N0}",texts.Single(t=>t.name=="AgencyDailyEntrants").text);
                    Assert.True(texts.Any(t=>t.text.Contains("距最近地铁 100 米")));Assert.True(texts.Any(t=>t.text.Contains("距最近住宅 500 米")));
                    Assert.False(texts.Any(t=>t.text.Contains("地段客流 ×")||t.text.Contains("条越长")));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(detail);ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
