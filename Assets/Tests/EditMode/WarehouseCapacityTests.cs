using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class WarehouseCapacityTests
    {
        private static GameSession Make(ShopSize size=ShopSize.Small)
        {
            var data=GameSessionTests.Layout();data.places.Single(p=>p.id=="pc").size=size.ToString().ToLowerInvariant();
            data.places.Single(p=>p.id=="tea").size="small";
            var s=new GameSession(CityLayout.FromData(data),81,Money.FromYuan(1000000),GameTimestamp.From(new GameDate(3),600),false);
            s.EventsEnabled=false;s.PlayerShop.Settings.BaseCustomersPerHour=0;s.PlayerShop.Reputation=100;
            GameSessionTests.Teleport(s,"pc");return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static Employee Candidate(GameSession s,string id)
        {
            var list=(List<Employee>)typeof(GameSession).GetField("_candidates",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s);
            list.Clear();var person=new Employee{Id=id,Name=id,DailyWageYuan=100,CandidateLeavesAt=s.Clock.Now.TotalMinutes+3000};
            list.Add(person);return person;
        }
        [TestCase(ShopSize.Small,8,4)]
        [TestCase(ShopSize.Medium,12,6)]
        [TestCase(ShopSize.Large,20,10)]
        public void SizeControlsShelvesAndRealEmployeeCountInsteadOfWarehouse(ShopSize size,int shelves,int staff)
        {
            using(var s=Make(size))
            {
                Assert.AreEqual(shelves,s.ShopShelfLimit);Assert.AreEqual(staff,s.ShopStaffLimit);
                Assert.True(s.PlayerShop.Stock.CanAdd(PartCatalog.Get("cpu0"),true,100000));
                Assert.AreEqual(int.MaxValue,s.PlayerShop.Stock.MaxAddable(PartCatalog.Get("cpu0"),true));
                s.CurrentShop.Layout.Clear();
                for(int i=0;i<shelves;i++)s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf,X=0,Z=0});
                string error=s.PlacementError(true,FurnitureCatalog.Get(FurnitureCatalog.Shelf),new PlacedFurniture{Id=FurnitureCatalog.Shelf},-1);
                StringAssert.Contains("最多摆 "+shelves,error);
                Assert.False((s.PlacementError(true,FurnitureCatalog.Get(FurnitureCatalog.ShopCounter),new PlacedFurniture{Id=FurnitureCatalog.ShopCounter},-1)??"").Contains("最多摆"),"其他家具不受数量上限约束");
                Assert.False((s.PlacementError(false,FurnitureCatalog.Get(FurnitureCatalog.Shelf),new PlacedFurniture{Id=FurnitureCatalog.Shelf},-1)??"").Contains("最多摆"));
                for(int i=0;i<staff;i++)
                {Candidate(s,"worker-"+i);Assert.True(s.Hire(0),"同一收银岗位允许多人");}
                Assert.AreEqual(staff,s.Employees.Count);Assert.AreEqual(staff,s.ShopStaff(s.CurrentShop,EmployeeRole.Cashier).Count());
                Candidate(s,"overflow");Assert.False(s.Hire(0));Assert.AreEqual(staff,s.Employees.Count);
                using(var restored=Reload(s))
                {
                    Assert.AreEqual(staff,restored.Employees.Count);Assert.AreEqual(staff,restored.StaffCommitted(restored.CurrentShop));
                    Assert.AreEqual(shelves,restored.CurrentShop.Layout.Count,"迁移不丢家具");
                    Assert.True(restored.DismissEmployee(restored.CurrentShop.Employee.Id));
                    Assert.AreEqual(staff-1,restored.Employees.Count);Assert.NotNull(restored.CurrentShop.Employee,"副收银员自动承接主引用");
                    Candidate(restored,"replacement");Assert.True(restored.Hire(0));Assert.AreEqual(staff,restored.Employees.Count);
                }
            }
        }
        [Test]
        public void PendingTransfersReserveCapacityWithoutReservingAnEntireRole()
        {
            using(var s=Make(ShopSize.Large))
            {
                var source=s.CurrentShop;
                for(int i=0;i<3;i++){Candidate(s,"transfer-"+i);Assert.True(s.Hire(0));}
                var people=s.Employees.Select(e=>e.Person).ToArray();
                s.DebugAddListing("tea",0,60,Money.FromYuan(1000));GameSessionTests.Teleport(s,"agency");Assert.True(s.SignLease("tea",true));
                GameSessionTests.Teleport(s,"tea");
                for(int i=0;i<2;i++){Candidate(s,"target-"+i);Assert.True(s.Hire(0));}
                Assert.True(s.ArrangeEmployeeWork(people[0].Id,"tea",EmployeeRole.Cashier));
                Assert.True(s.ArrangeEmployeeWork(people[1].Id,"tea",EmployeeRole.Cashier));
                Assert.False(s.ArrangeEmployeeWork(people[2].Id,"tea",EmployeeRole.Cashier));
                Assert.AreEqual(4,s.StaffCommitted(s.CurrentShop));Assert.AreEqual(2,s.ShopStaff(s.CurrentShop,EmployeeRole.Cashier).Count());
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(4,loaded.StaffCommitted(loaded.CurrentShop));loaded.Clock.Advance(1440);
                    Assert.AreEqual(4,loaded.ShopStaff(loaded.ShopAt("tea"),EmployeeRole.Cashier).Count());
                    Assert.AreEqual(1,loaded.ShopStaff(loaded.ShopAt(source.Place.Id),EmployeeRole.Cashier).Count());
                }
            }
        }
        [Test]
        public void StandbyEmployeesAlsoUseTheHiringShopCapacity()
        {
            using(var s=Make())
            {
                for(int i=0;i<4;i++){Candidate(s,"standby-"+i);Assert.NotNull(s.RecruitEmployee(0));}
                Assert.AreEqual(4,s.StaffCommitted(s.CurrentShop));Candidate(s,"overflow");Assert.IsNull(s.RecruitEmployee(0));
                using(var loaded=Reload(s))Assert.AreEqual(4,loaded.StaffCommitted(loaded.CurrentShop));
            }
        }
        [Test]
        public void LegacyExcessEmployeesAndShelvesRemainButCannotGrow()
        {
            using(var s=Make())
            {
                for(int i=0;i<10;i++)s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf});
                s.CurrentShop.Employee=new Employee{Id="legacy-0",Name="legacy-0"};
                for(int i=1;i<6;i++)s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=EmployeeRole.Cashier,Person=new Employee{Id="legacy-"+i,Name="legacy-"+i}});
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(6,loaded.Employees.Count);Assert.AreEqual(s.CurrentShop.Layout.Count,loaded.CurrentShop.Layout.Count);
                    Candidate(loaded,"new");Assert.False(loaded.Hire(0));
                    StringAssert.Contains("最多摆",loaded.PlacementError(true,FurnitureCatalog.Get(FurnitureCatalog.Shelf),new PlacedFurniture{Id=FurnitureCatalog.Shelf},-1));
                }
            }
        }
        [Test]
        public void WarehouseNeedsOneBoxAndKeepsShelfCapacityAndCarryingLimits()
        {
            using(var s=Make())
            {
                Assert.True(FurnitureCatalog.Purchasable(false).Any(f=>f.Id==FurnitureCatalog.HomeBox));
                Assert.True(FurnitureCatalog.Purchasable(true).Any(f=>f.Id==FurnitureCatalog.ShopBox));
                s.CurrentShop.Layout.Clear();s.Backpack.AddFurniture(FurnitureCatalog.Shelf,1000);
                Assert.False(s.StoreInBox("furn:"+FurnitureCatalog.Shelf,1000),"没有纸箱时不能使用后仓");
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.ShopBox,X=1,Z=1});
                Assert.True(s.StoreInBox("furn:"+FurnitureCatalog.Shelf,1000));Assert.AreEqual(1000,s.ShopLocker.FurnitureCount(FurnitureCatalog.Shelf));
                Assert.False(s.TakeFromBox("furn:"+FurnitureCatalog.Shelf,1000),"后仓不限，但背包承重仍限制取出数量");
                var cpu=PartCatalog.Get("cpu0");s.PlayerShop.Stock.ForceAdd(cpu,true,10000);
                s.PlayerShop.SetShelfCapacity(new Dictionary<string,double>{{"cpu",60}});
                Assert.AreEqual(30,s.PlayerShop.ShelfFillOf(PartCategory.Cpu).Total);Assert.AreEqual(10000,s.PlayerShop.Stock.CountAll(cpu));
                GameSessionTests.Teleport(s,"flat");s.Cargo.ForceAdd(cpu,true,5000);
                Assert.True(s.UnloadBackpack());Assert.AreEqual(5000,s.HomeBoxParts.CountAll(cpu));Assert.True(s.Cargo.IsEmpty);
            }
        }
        [Test]
        public void LegacyTemporaryStorageMergesWithoutLosingFoodBatchesOrReplaying()
        {
            using(var s=Make())
            {
                s.HomeTemp.AddGift(Gifts.Cig,1000);s.HomeTemp.AddFurniture(FurnitureCatalog.Shelf,50);
                s.HomeTempParts.ForceAdd(PartCatalog.Get("cpu0"),true,2000);
                s.HomeTemp.Food.Ingredient.Add(new FoodBatch{Id="veg",Name="青菜",Count=7,ExpireDay=90,Chilled=true,ChilledDays=7});
                s.ShopTemp.AddGift(Gifts.Cig,500);s.ShopTemp.Food.RiceKg=300;
                using(var loaded=Reload(s))
                {
                    Assert.True(loaded.HomeTemp.IsEmpty);Assert.True(loaded.HomeTempParts.IsEmpty);Assert.True(loaded.ShopTemp.IsEmpty);
                    Assert.AreEqual(1000,loaded.HomeBox.GiftCount(Gifts.Cig));Assert.AreEqual(500,loaded.ShopLocker.GiftCount(Gifts.Cig));
                    Assert.AreEqual(2000,loaded.HomeBoxParts.CountAll(PartCatalog.Get("cpu0")));
                    Assert.AreEqual(50,loaded.HomeBox.FurnitureCount(FurnitureCatalog.Shelf));Assert.AreEqual(300,loaded.ShopLocker.Food.RiceKg);
                    var food=loaded.HomeBox.Food.Ingredient.Single();Assert.AreEqual(7,food.Count);Assert.AreEqual(FoodBatch.NoExpiry,food.ExpireDay);Assert.True(food.Chilled);
                    using(var again=Reload(loaded))Assert.AreEqual(1000,again.HomeBox.GiftCount(Gifts.Cig));
                }
            }
        }
        [Test]
        public void ExtraCashierCanOperateWhilePrimaryIsOffShift()
        {
            using(var s=Make())
            {
                var first=Candidate(s,"first");Assert.True(s.Hire(0));var second=Candidate(s,"second");Assert.True(s.Hire(0));
                first.ShiftStart=1200;first.ShiftEnd=1380;var p=s.CityPersonState(s.PersonIdOf(second));
                p.LocationPlaceId=s.PlayerShopPlace.Id;p.Route=null;p.ActivityId=null;
                s.Leave();Assert.True(s.PlayerShop.EmployeeOnDuty(s.Clock.Now));Assert.Greater(s.PlayerShop.EmployeeServiceRate(),0);
            }
        }
        [Test]
        public void LeaveStatusUsesCashiersActualPresenceAndOpeningHours()
        {
            using (var s = Make())
            {
                s.PlayerShop.NotFitted = false;
                var cashier = Candidate(s, "cashier");
                Assert.True(s.Hire(0));
                Assert.True(cashier.OnDuty(s.Clock.Now));
                var person = s.CityPersonState(s.PersonIdOf(cashier));
                person.Route = null;
                person.ActivityId = null;
                person.LocationPlaceId = s.Home.Id;
                Assert.True(s.ShopLeaveStatus().WillClose);

                person.LocationPlaceId = s.CurrentShop.Place.Id;
                Assert.False(s.ShopLeaveStatus().WillClose);
                StringAssert.Contains("有收银员在岗", s.ShopLeaveStatus().Message);

                s.PlayerShop.SetHours(8 * 60, 9 * 60);
                Assert.False(s.ShopLeaveStatus().WillClose);
                Assert.AreEqual("当前未营业", s.ShopLeaveStatus().Message);
            }
        }
        [Test]
        public void WarehouseUiUsesOneTilePerStackAndOpensWithBackpack()
        {
            using(var s=Make())
            {
                GameSessionTests.Teleport(s,"flat");s.HomeBoxParts.ForceAdd(PartCatalog.Get("cpu0"),true,100000);
                s.HomeBox.AddFurniture(FurnitureCatalog.Shelf,10000);
                var root=new GameObject("WarehouseUiRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new PCShopSell.Game.UI.GameUi(controller);
                try
                {
                    typeof(PCShopSell.Game.UI.GameUi).GetMethod("RefreshShopDockVisibility",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).Any(b=>b.name=="WarehouseAccess"));
                    ui.ToggleBackpack();
                    Assert.True(ui.AnyModalOpen);Assert.Less(ui.Canvases.Sum(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>().Length),300);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Text>()).Any(t=>t.text.Contains("后仓容量不限")));
                    Assert.AreEqual(100000,s.HomeBoxParts.CountAll(PartCatalog.Get("cpu0")));
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        [Test]
        public void MultipleAssemblersWorkOnDifferentOrdersWithoutDoubleCountingProgress()
        {
            using(var s=Make(ShopSize.Large))
            {
                s.Clock.Restore(new GameDate(GameSession.BuildOrdersStartDay),600);
                var a=new Employee{Id="assembly-a",Name="甲",DailyWageYuan=100};
                var b=new Employee{Id="assembly-b",Name="乙",DailyWageYuan=100};
                s.CurrentShop.Assembler=a;s.CurrentShop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=EmployeeRole.Assembler,Person=b});
                s.CurrentShop.AutoBuild=true;CityTestPeople.AtAssignedWork(s,a);CityTestPeople.AtAssignedWork(s,b);
                var orders=new[]{new BuildOrder(1,0,Money.FromYuan(3000),s.Clock.Now,s.Clock.Now.AddMinutes(600)),new BuildOrder(2,0,Money.FromYuan(3000),s.Clock.Now,s.Clock.Now.AddMinutes(600))};
                s.PlayerShop.RestoreOrders(orders,3);s.PlayerShop.Stock.Clear();
                foreach(var part in PartCatalog.BuildList(0))s.PlayerShop.Stock.ForceAdd(part,true,2);
                var cash=s.Ledger.Balance;s.AdvanceClock(1);
                Assert.True(orders.All(o=>o.PartsReserved));Assert.True(orders.All(o=>o.EmployeeMinutesLeft==89));
                s.AdvanceClock(90);Assert.Zero(s.PlayerShop.Orders.Count);
                Assert.AreEqual(1,a.BuildsCompleted);Assert.AreEqual(1,b.BuildsCompleted);
                Assert.AreEqual(cash+Money.FromYuan(6000),s.Ledger.Balance);
            }
        }
        [Test]
        public void AllAdditionalEmployeesReceivePayrollOnlyOnceAcrossReload()
        {
            using(var s=Make())
            {
                for(int i=0;i<4;i++){Candidate(s,"payroll-"+i);Assert.True(s.Hire(0));}
                s.Clock.Restore(s.Clock.Today,1439);s.AdvanceClock(2);
                var payments=s.Ledger.Entries.Where(e=>e.Category=="工资").ToArray();
                Assert.AreEqual(4,payments.Length);Assert.AreEqual(4,payments.Select(e=>e.Attribution.EmployeeId).Distinct().Count());
                using(var loaded=Reload(s)){loaded.AdvanceClock(1);Assert.AreEqual(4,loaded.Ledger.Entries.Count(e=>e.Category=="工资"));}
            }
        }
    }
}
