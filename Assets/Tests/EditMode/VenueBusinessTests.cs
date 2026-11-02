using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class VenueBusinessTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,891,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(5),600),false,true,false);
            s.EventsEnabled=false;s.PlayerShop.Reputation=100;
            var court=city.GetPlace("life_court");
            s.DebugAddListing(court.Id,0,60,Money.FromYuan(1000));
            GameSessionTests.Teleport(s,s.AgencyFor(court).Id);
            Assert.True(s.SignRetailLease(court.Id,false,"clothing"));
            GameSessionTests.Teleport(s,court.Id);return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        [Test]
        public void LeasePreservesPurposeAndCannotBeChangedToRetailOrOrdinaryInterior()
        {
            using(var s=Make())
            {
                Assert.AreEqual("我的篮球场",s.ShopTitle(s.CurrentShop));
                Assert.False(s.SetShopMainCategory("clothing"));
                Assert.False(s.PlayerShop.TradingEnabled);Assert.False(s.PlayerShop.IsOpen);
                Assert.False(s.VenueOpen(s.CurrentPlace));Assert.IsNull(s.CurrentShop.Employee,"原教练不自动受雇");
                var root=new GameObject("VenueOwnerRegression");root.SetActive(false);
                var c=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(c,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(c,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                var ui=new PCShopSell.Game.UI.GameUi(c);
                try
                {
                    Assert.False(c.InInterior,"保留真实球场，不进入普通店铺室内");
                    ui.OnEnteredPlace(s.CurrentPlace);
                    Assert.False(ui.DockOpen,"不得出现普通零售柜台");
                    Assert.True(ui.Canvases.SelectMany(canvas=>canvas.GetComponentsInChildren<UnityEngine.UI.Text>()).Any(t=>t.text.Contains("场馆管理")));
                    Assert.True(ui.Canvases.SelectMany(canvas=>canvas.GetComponentsInChildren<UnityEngine.UI.Button>()).Any(b=>b.name=="VenueOwnerUse"));
                    ui.DebugRefreshQuests();StringAssert.Contains("场馆经营",ui.QuestTrackerText);
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)Object.DestroyImmediate(canvas.gameObject);
                    Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        [Test]
        public void LegacyLeaseMigrationKeepsPaidRentDepositInventoryFurnitureAndDoesNotDebitAgain()
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.ForceAddGoods("flower_rose",2);
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});
                s.PlayerShop.RestoreDisplay(new[]{0});
                var rent=s.Lease.Rent;var deposit=s.Lease.Deposit;var cash=s.Ledger.Balance;
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));save.venueBusinesses=null;
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(cash,loaded.Ledger.Balance);Assert.AreEqual(rent,loaded.Lease.Rent);Assert.AreEqual(deposit,loaded.Lease.Deposit);
                    Assert.AreEqual(2,loaded.PlayerShop.Stock.GoodsCount("flower_rose"));
                    Assert.AreEqual(s.CurrentShop.Layout.Count,loaded.CurrentShop.Layout.Count);Assert.False(loaded.PlayerShop.TradingEnabled);
                    CollectionAssert.AreEqual(s.PlayerShop.DisplayPcs,loaded.PlayerShop.DisplayPcs,"原展示整机继续保留，不能因场馆迁移而自动拆除");
                    Assert.NotNull(loaded.VenueOperation("life_court"));
                }
            }
        }
        [Test]
        public void OwnerUsesRealActivityForFreeWithTimeAndEnergyCost()
        {
            using(var s=Make())
            {
                Assert.True(s.SelectVenueService("court",0));
                var activity=s.RunningCityActivity;Assert.NotNull(activity);
                var cash=s.Ledger.Balance;var energy=s.Player.Energy;var time=s.Clock.Now;
                s.Time.SetSpeed(8);
                for(int i=0;i<30000&&activity.Active;i++)s.Tick(.05);
                Assert.AreEqual(CityActivityPhase.Completed,activity.State,activity.EndReason);
                Assert.AreEqual(0,activity.EntryCostCents);Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.Less(s.Player.Energy,energy);Assert.Greater(s.Clock.Now.TotalMinutes,time.TotalMinutes);
                Assert.AreEqual(0,s.VenueOperation("life_court").TotalRevenueCents);
                StringAssert.Contains("支出 ¥0",activity.SettlementSummary);
            }
        }
        [Test]
        public void ArrivedCustomerPaysExactlyOnceAcrossJsonReloadAndCapacityBlocksOwner()
        {
            using(var s=Make())
            {
                var place=s.CurrentPlace;Assert.True(s.ConfigureVenue(place.Id,true,0,0,4500));
                var state=s.VenueOperation(place.Id);state.LastPulse=s.Clock.Now.TotalMinutes/10;
                var people=s.CityPeople.Where(p=>p.PersonId.StartsWith("res_")).Take(s.VenueCapacity(place)).ToArray();
                Assert.AreEqual(s.VenueCapacity(place),people.Length);
                state.CustomersHere=people.Select((p,i)=>
                {
                    p.Route=null;p.LocationPlaceId=place.Id;p.ActivityId="venue-test:"+i;
                    return new VenueCustomer{PersonId=p.PersonId,ReservationId=p.ActivityId,PriceCents=4500};
                }).ToArray();
                var cash=s.Ledger.Balance;s.Clock.Advance(1);
                Assert.AreEqual(cash+Money.FromCents(4500*people.Length),s.Ledger.Balance);
                Assert.AreEqual(people.Length,state.Customers);Assert.AreEqual(4500*people.Length,state.TotalRevenueCents);
                Assert.NotNull(s.VenueServiceBlocker("court",0));
                using(var loaded=Reload(s))
                {
                    loaded.Clock.Advance(1);var restored=loaded.VenueOperation(place.Id);
                    Assert.AreEqual(state.TotalRevenueCents,restored.TotalRevenueCents);
                    Assert.AreEqual(s.Ledger.Balance,loaded.Ledger.Balance);
                    Assert.AreEqual(people.Length,restored.CustomersHere.Length);
                    Assert.True(loaded.ConfigureVenue(place.Id,false,0,0,4500));
                    loaded.Clock.Advance(70);Assert.Zero(restored.CustomersHere.Length);
                    Assert.NotNull(loaded.CityPersonState(people[0].PersonId));
                    Assert.IsNull(loaded.CityPersonState(people[0].PersonId).ActivityId);
                }
            }
        }
        [Test]
        public void NoAttendantMeansNoAdmissionsAndDailyMaintenanceDoesNotRepeatAfterReload()
        {
            using(var s=Make())
            {
                var place=s.CurrentPlace;Assert.True(s.ConfigureVenue(place.Id,true,0,0,3000));
                Assert.True(s.VenueOpen(place));s.Leave();Assert.False(s.VenueOpen(place));
                var cash=s.Ledger.Balance;s.Clock.Advance(1440);
                Assert.Zero(s.VenueOperation(place.Id).TotalRevenueCents);
                Assert.AreEqual(cash-Money.FromYuan(s.VenueMaintenanceYuan(place)),s.Ledger.Balance);
                using(var loaded=Reload(s))
                {var before=loaded.Ledger.Balance;loaded.Clock.Advance(1);Assert.AreEqual(before,loaded.Ledger.Balance);}
            }
        }
        [Test]
        public void OpenVenueReceivesRealResidentsByRouteAndNeverExceedsCapacity()
        {
            using(var s=Make())
            {
                var place=s.CurrentPlace;Assert.True(s.ConfigureVenue(place.Id,true,0,0,3000));
                var state=s.VenueOperation(place.Id);bool seenEnRoute=false;
                for(int minute=0;minute<600;minute++)
                {
                    s.Player.Hunger=100;s.Player.Energy=100;s.Clock.Advance(1);
                    Assert.LessOrEqual(state.CustomersHere.Length,s.VenueCapacity(place));
                    foreach(var c in state.CustomersHere)
                    {
                        Assert.NotNull(s.CityPerson(c.PersonId));
                        if(c.Started<0&&s.CityPersonState(c.PersonId).Route!=null)seenEnRoute=true;
                    }
                }
                Assert.True(seenEnRoute,"顾客需要走实际路线到场");
                Assert.Greater(state.Customers,0);Assert.Greater(state.TotalRevenueCents,0);
                Assert.AreEqual(state.TotalRevenueCents,s.Ledger.Entries.Where(e=>e.Category=="场馆服务").Sum(e=>e.Amount.Cents));
            }
        }
        [Test]
        public void HiredAttendantUsesActualPresenceAndShiftInsteadOfOriginalCoach()
        {
            using(var s=Make())
            {
                var place=s.CurrentPlace;var shop=s.CurrentShop;
                Assert.True(s.ConfigureVenue(place.Id,true,0,0,3000));s.Leave();Assert.False(s.VenueOpen(place));
                var guard=new Employee{Id="venue-guard",Name="值守测试员",DailyWageYuan=120};shop.Employee=guard;
                Assert.NotNull(s.EmployeeAssignmentBlocker(guard.Id,place.Id,EmployeeRole.Assembler),"场馆不能安排零售装机岗位");
                var person=s.CityPersonState(s.PersonIdOf(guard));person.WorkPlaceId=place.Id;
                Assert.False(s.VenueOpen(place),"已雇佣但尚未实际到岗不接客");
                person.LocationPlaceId=place.Id;person.Route=null;person.ActivityId=null;
                Assert.True(s.VenueOpen(place));
                guard.Schedule.NextNotBefore=s.Clock.Now.TotalMinutes+1440;Assert.False(s.VenueOpen(place));
            }
        }
        [Test]
        public void PausedVenueTerminatesLeaseWithDepositForfeitedAndNewLeaseStartsClean()
        {
            using(var s=Make())
            {
                var place=s.CurrentPlace;var cash=s.Ledger.Balance;
                // Starter boxes are assets and must be moved out explicitly before surrender.
                s.CurrentShop.Locker.ClearFurniture();
                Assert.IsNull(s.CloseShopBlocker(s.CurrentShop));Assert.True(s.CloseShop(place.Id));
                Assert.AreEqual(cash,s.Ledger.Balance);Assert.IsNull(s.VenueOperation(place.Id));
                GameSessionTests.Teleport(s,s.AgencyFor(place).Id);Assert.True(s.SignRetailLease(place.Id,false,"computer"));
                var state=s.VenueOperation(place.Id);Assert.False(state.Accepting);Assert.Zero(state.TotalRevenueCents);
            }
        }
    }
}
