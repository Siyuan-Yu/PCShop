using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class BuyerHistoryTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7,Money.FromYuan(500000),GameTimestamp.From(new GameDate(0),9*60));
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;
            s.MainShop.Buyer=new Employee{Name="顾宁",DailyWageYuan=150};
            CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
            var offers=s.EmployeePurchaseCatalog();
            Assert.True(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id,new EmployeePurchasePlan
            {
                Transport=VehicleKind.Taxi,Items=new[]{offers.First(o=>o.Id=="cpu0"&&o.Kind==PurchaseGoodsKind.Part).Line(2),
                    offers.First(o=>o.Id=="ram0"&&o.Kind==PurchaseGoodsKind.Part).Line(1)}
            }));
            return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        [Test]
        public void ActualReceiptSurvivesActiveAndCompletedSavesWithoutChargingOrDeliveringTwice()
        {
            using(var s=Make())
            {
                string id=s.MainShop.Buyer.Id,shop=s.MainShop.Place.Id;
                var quote=s.QuoteEmployeePurchase(id);Assert.IsNull(quote.Error,quote.Error);
                var cash=s.Ledger.Balance;long depart=s.Clock.Now.TotalMinutes;
                int stock=s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0"));
                Assert.True(s.DispatchEmployeePurchase(id));
                var receipt=s.EmployeePurchaseRecords(shop,id).Single();
                Assert.True(receipt.CostsRecorded);Assert.AreEqual(depart,receipt.DepartAt);
                Assert.AreEqual(quote.Total.Cents,receipt.GoodsCents+receipt.TravelCents+receipt.FreightCents);
                Assert.AreEqual(cash-quote.Total,s.Ledger.Balance);
                Assert.AreEqual(2,receipt.ReceiptLines.Length);
                Assert.True(receipt.ReceiptLines.All(l=>!string.IsNullOrEmpty(l.SourceName)&&!string.IsNullOrEmpty(l.ProductName)));
                Assert.AreEqual(receipt.GoodsCents,receipt.ReceiptLines.Sum(l=>l.UnitCents*l.Count));
                foreach(var line in receipt.ReceiptLines)
                    Assert.True(receipt.TravelLegs.Any(r=>r.TargetPlaceId==line.SourceId));
                receipt.ReceiptLines[0].Count=999;
                Assert.AreEqual(2,s.EmployeePurchaseRecords(shop,id).Single().ReceiptLines[0].Count,"查询返回副本");
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(quote.Total.Cents,loaded.EmployeePurchaseRecords(shop,id).Single().GoodsCents+receipt.TravelCents+receipt.FreightCents);
                    var trip=loaded.EmployeePurchaseTrips.Single();
                    loaded.AdvanceClock((int)(Math.Max(trip.ReturnAt,trip.DeliveryAt)-loaded.Clock.Now.TotalMinutes+1));
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count);
                    var done=loaded.EmployeePurchaseRecords(shop,id).Single();
                    Assert.True(done.Returned&&done.Delivered);
                    Assert.AreEqual(stock+2,loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.True(loaded.DismissEmployee(id));
                    using(var again=Reload(loaded))
                    {
                        var archived=again.EmployeePurchaseRecords(shop,id).Single();
                        Assert.AreEqual(done.GoodsCents,archived.GoodsCents);
                        Assert.AreEqual(done.ReceiptLines[0].SourceName,archived.ReceiptLines[0].SourceName);
                        var paid=again.Ledger.Balance;
                        again.AdvanceClock(5);
                        Assert.AreEqual(paid,again.Ledger.Balance);
                        Assert.AreEqual(stock+2,again.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                        Assert.AreEqual(1,again.EmployeePurchaseRecords(shop).Length,"辞退后本店历史仍保留");
                        Assert.IsEmpty(again.EmployeePurchaseRecords("other-shop"));
                    }
                }
            }
        }

        [Test]
        public void EditingFuturePlanCannotRewriteDispatchedItemsOrPaidPrices()
        {
            using(var s=Make())
            {
                string id=s.MainShop.Buyer.Id,shop=s.MainShop.Place.Id;
                Assert.True(s.DispatchEmployeePurchase(id));
                var before=s.EmployeePurchaseRecords(shop,id).Single();
                Assert.True(s.CompleteEmployeePurchase(id,new EmployeePurchasePlan()));
                var after=s.EmployeePurchaseRecords(shop,id).Single();
                CollectionAssert.AreEqual(before.ReceiptLines.Select(x=>x.Count),after.ReceiptLines.Select(x=>x.Count));
                Assert.AreEqual(before.GoodsCents,after.GoodsCents);
                Assert.False(after.Delivered);
                Assert.AreEqual("前往货源途中",s.EmployeePurchaseTripStatus(after));
            }
        }

        [Test]
        public void PartialPurchaseReceiptContainsOnlyActualQuantitiesAndExplainsSkippedGoods()
        {
            using(var s=Make())
            {
                string id=s.MainShop.Buyer.Id;
                var offer=s.EmployeePurchaseCatalog().First(o=>o.Id=="cpu0"&&o.Kind==PurchaseGoodsKind.Part);
                Assert.True(s.ConfigureEmployeePurchase(id,new EmployeePurchasePlan{Transport=VehicleKind.Taxi,Items=new[]{offer.Line(5)}}));
                var full=s.QuoteEmployeePurchase(id);
                var available=offer.Price*2+full.Travel+full.Freight+Money.FromYuan(1);
                s.Ledger.ForceDebit(s.Clock.Today,"测试",s.Ledger.Balance-available,"测试可用现金");
                Assert.True(s.DispatchEmployeePurchase(id));
                var record=s.EmployeePurchaseRecords(s.MainShop.Place.Id,id).Single();
                Assert.AreEqual(2,record.Items.Single().Count);
                Assert.AreEqual(2,record.ReceiptLines.Single().Count);
                StringAssert.Contains("计划 5 件，购入 2 件",record.SkippedNote);
                Assert.AreEqual(available.Cents-s.Ledger.Balance.Cents,record.GoodsCents+record.TravelCents+record.FreightCents);
            }
        }

        [Test]
        public void OldSaveWithoutHistoryOrReceiptRemainsReadableAndDoesNotInventCosts()
        {
            using(var s=Make())
            {
                Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                var save=s.CreateSave();save.employeePurchaseHistory=null;
                foreach(var trip in save.employeeTrips){trip.CostsRecorded=false;trip.ReceiptLines=null;}
                using(var loaded=GameSession.Load(s.City,save))
                {
                    var record=loaded.EmployeePurchaseRecords(s.MainShop.Place.Id).Single();
                    Assert.False(record.CostsRecorded);Assert.IsNull(record.ReceiptLines);
                    Assert.AreEqual(2,record.Items.Length);
                }
            }
        }

        [Test]
        public void StoreBuyersShowOnlyThisStoreAndReturnToOverviewAfterEditing()
        {
            using(var s=Make())
            {
                string other=s.City.Places.First(p=>p.Kind==PlaceKind.Shop&&!s.IsMyShop(p)&&!s.IsDedicatedVenue(p)).Id;
                s.PlayerShop.Reputation=80;
                s.DebugAddListing(other,0,60,Money.FromYuan(3000));
                GameSessionTests.Teleport(s,s.AgencyFor(s.City.GetPlace(other)).Id);Assert.True(s.SignLease(other,true));
                s.CurrentShop.Buyer=new Employee{Name="别店采购员"};
                GameSessionTests.Teleport(s,s.MainShop.Place.Id);
                var root=new GameObject("BuyerHistoryUiRegression");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(controller);
                Button Button(string text)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.GetComponentInChildren<Text>().text==text);
                string Texts()=>string.Join("\n",ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Select(t=>t.text));
                try
                {
                    ui.OpenDock();ui.DebugSelectTab(1);
                    Assert.NotNull(Button("进货员 · 1 人"));
                    Button("进货员 · 1 人").onClick.Invoke();
                    StringAssert.Contains("顾宁",Texts());StringAssert.DoesNotContain("别店采购员",Texts());
                    StringAssert.DoesNotContain("自己去进货",Texts());StringAssert.DoesNotContain("进货渠道",Texts());
                    var overview=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name=="ShopBuyersList").parent;
                    Assert.IsNull(overview.GetComponent<ContentSizeFitter>(),"外层不滚动，只有进货员列表滚动");
                    Button("进货记录").onClick.Invoke();StringAssert.Contains("暂无进货记录",Texts());
                    Button("返回进货员").onClick.Invoke();
                    CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
                    Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                    ui.RebuildWindows();
                    Button("进货记录").onClick.Invoke();
                    StringAssert.Contains("本趟已付",Texts());StringAssert.Contains("预计采购完成",Texts());
                    StringAssert.Contains(s.EmployeePurchaseRecords(s.MainShop.Place.Id).Single().ReceiptLines[0].SourceName,Texts());
                    Button("返回进货员").onClick.Invoke();
                    Button("调整进货").onClick.Invoke();Button("完成").onClick.Invoke();
                    Assert.NotNull(Button("本店进货记录"),"保存后返回总览，不关闭整个管理流程");
                    Assert.True(s.Time.IsHeld);
                    ui.CloseAllWindows();Assert.False(s.Time.IsHeld);
                    s.MainShop.Buyer=null;
                    ui.OpenShopBuyers();StringAssert.Contains("本店还没有进货员",Texts());
                    Assert.NotNull(Button("安排现有员工"));
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
