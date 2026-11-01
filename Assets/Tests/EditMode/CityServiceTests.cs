using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityServiceTests
    {
        private static GameSession Make(string preset)
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=GameSession.CreateCityLifePreset(city,preset);foreach(var store in s.MyShops)store.Sim.Settings.BaseCustomersPerHour=0;return s;
        }
        [Test] public void ActualCashierKeepsServiceAfterOwnerLeavesButRemoteFocusDoesNotStaffAnEmptyStore()
        {
            using(var solo=Make("solo"))
            {
                Assert.True(solo.CityPlayerCanWork(solo.MainShop));solo.Leave();
                Assert.False(solo.CityPlayerCanWork(solo.MainShop));Assert.False(solo.MainShop.Sim.IsOpen);
                var before=solo.PlayerPosition;solo.GetCityCashReserve();
                Assert.AreEqual(before.X,solo.PlayerPosition.X);Assert.False(solo.MainShop.Sim.IsOpen);
            }
            using(var staffed=Make("staff"))
            {
                Assert.GreaterOrEqual(staffed.MyShopCount,2,"The staff/multi-store preset contains actual separate shops");
                staffed.MainShop.Assembler=null;var employee=staffed.MainShop.Employee;
                Assert.True(staffed.CityEmployeeAvailable(employee,staffed.MainShop));staffed.Leave();
                Assert.True(staffed.MainShop.Sim.IsOpen);
                var p=staffed.CityPersonState(staffed.PersonIdOf(employee));p.LocationPlaceId=p.HomePlaceId;
                Assert.False(staffed.MainShop.Sim.IsOpen,"A shift flag without physical presence cannot keep the shop open");
            }
        }
        [Test] public void RealOnDutyAssemblerRetainsProgressThroughOwnerDepartureAndJsonRestore()
        {
            using(var s=Make("staff"))
            {
                var shop=s.MainShop;shop.AutoBuild=true;
                StringAssert.Contains("装机员",s.CityPersonRole("life_ning"));
                var order=new BuildOrder(700,0,Money.FromYuan(3000),s.Clock.Now,s.Clock.Now.AddMinutes(600));
                shop.Sim.RestoreOrders(new[]{order},701);
                foreach(var part in PartCatalog.BuildList(0))shop.Sim.Stock.ForceAdd(part,true,1);
                var cash=s.Ledger.Balance;s.AdvanceClock(20);
                Assert.True(order.PartsReserved);Assert.Greater(order.EmployeeMinutesLeft,0);
                int left=order.EmployeeMinutesLeft;s.Leave();s.AdvanceClock(5);
                Assert.Less(order.EmployeeMinutesLeft,left,"Actual employees continue while the owner is away");
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                Assert.AreEqual(shop.Place.Id,s.CityPersonState("life_ning").LocationPlaceId,"Original assembler remained physically at work before save");
                Assert.AreEqual(shop.Place.Id,save.cityPeople.Single(p=>p.PersonId=="life_ning").LocationPlaceId,"JSON must preserve the worker position");
                using(var loaded=GameSession.Load(s.City,save))
                {
                    loaded.EventsEnabled=false;foreach(var store in loaded.MyShops)store.Sim.Settings.BaseCustomersPerHour=0;
                    var restored=loaded.MainShop.Sim.Orders.Single(o=>o.Id==700);
                    Assert.AreEqual(order.EmployeeMinutesLeft,restored.EmployeeMinutesLeft);
                    Assert.True(loaded.CityEmployeeAvailable(loaded.MainShop.Assembler,loaded.MainShop),$"Saved assembler: id={loaded.PersonIdOf(loaded.MainShop.Assembler)}, onDuty={loaded.MainShop.Assembler.OnDuty(loaded.Clock.Now)}, place={loaded.CityPersonState(loaded.PersonIdOf(loaded.MainShop.Assembler))?.LocationPlaceId}, route={loaded.CityPersonState(loaded.PersonIdOf(loaded.MainShop.Assembler))?.Route?.TargetPlaceId}, activity={loaded.CityPersonState(loaded.PersonIdOf(loaded.MainShop.Assembler))?.ActivityId}");
                    int remaining=restored.EmployeeMinutesLeft;
                    for(int minute=0;minute<remaining+2&&loaded.MainShop.Sim.Orders.Any(o=>o.Id==700);minute++)
                    {
                        int previous=restored.EmployeeMinutesLeft;loaded.AdvanceClock(1);
                        Assert.Less(restored.EmployeeMinutesLeft,previous,$"No assembly progress at minute {minute}: open={loaded.MainShop.Sim.IsOpen}, worker={loaded.CityEmployeeAvailable(loaded.MainShop.Assembler,loaded.MainShop)}, auto={loaded.MainShop.AutoBuild}, queue={loaded.MainShop.Sim.Queue.Count}");
                    }
                    Assert.False(loaded.MainShop.Sim.Orders.Any(o=>o.Id==700),$"left={restored.EmployeeMinutesLeft}; open={loaded.MainShop.Sim.IsOpen}; fit={loaded.MainShop.Sim.NotFitted}; auto={loaded.MainShop.AutoBuild}; staff={loaded.CityEmployeeAvailable(loaded.MainShop.Assembler,loaded.MainShop)}; place={loaded.CityPersonState(loaded.PersonIdOf(loaded.MainShop.Assembler))?.LocationPlaceId}");Assert.AreEqual(cash+order.Price,loaded.Ledger.Balance);
                    var paid=loaded.Ledger.Balance;loaded.AdvanceClock(3);Assert.AreEqual(paid,loaded.Ledger.Balance);
                }
            }
        }
    }
}
