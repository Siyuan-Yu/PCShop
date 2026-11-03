using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.Finance;

namespace PCShopSell.Tests
{
    public sealed class BrandBusinessTests
    {
        private static GameSession Make(int tier=3)
        {
            var s=DistrictProgressionTests.Make();s.DebugPrepareDistrictPrerequisites(1);s.DebugAddCash(1000000);
            typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=tier,Chapter=1});
            var shop=s.MyShops.First();shop.RetailKind="clothing";
            var b=new BrandRecord{Id="b",Name="试验服装",Kind="clothing",Position="quality",ShopIds=new[]{shop.Place.Id}};
            Set(s,new BrandBusinessState{Brands=new[]{b}});return s;
        }
        private static void Set(GameSession s,BrandBusinessState b)=>typeof(GameSession).GetField("_brands",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,b);
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,s.City.GetPlace(id));
        private static string Product=>ClothingProducts.All.First().Id;
        private static void Day(GameSession s,int day,int minute=600){s.Clock.Restore(new GameDate(day),minute);typeof(GameSession).GetMethod("TickBrandBusiness",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{new GameDate(day-1),new GameDate(day)});}
        private static void Delivery(GameSession s){typeof(GameSession).GetMethod("TickBrandDeliveries",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{s.Clock.Now.TotalMinutes});}
        [Test] public void BrandRequiresCommunityAndSustainedQualifications()
        {
            using(var s=Make(0)){Assert.NotNull(s.BrandFoundBlocker("models"));Assert.False(s.FoundBrand("models","我的品牌","quality"));}
            using(var s=Make()){Assert.NotNull(s.BrandFoundBlocker("models"));Assert.False(s.FoundBrand("models","我的品牌","quality"));Assert.AreEqual(1,s.BrandBusiness.Brands.Length);}
        }
        [Test] public void PromotionActuallyChangesTrafficAndExclusiveBlocksOtherBrand()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;var b=state.Brands[0];var shop=s.ShopAt(b.ShopIds[0]);
                state.Brands=state.Brands.Concat(new[]{new BrandRecord{Id="other",Kind="models",Name="模型"}}).ToArray();Set(s,state);
                double before=s.CityCampaignTraffic(shop);long balance=s.Ledger.Balance.Cents;
                Assert.True(s.SignBrandDeal("b","momo","endorse",true));Assert.Less(s.Ledger.Balance.Cents,balance);Assert.Greater(s.CityCampaignTraffic(shop),before);
                Assert.False(s.SignBrandDeal("other","momo","campaign",false));
                string id=s.BrandBusiness.Deals[0].Id;Assert.True(s.RespondBrandDeal(id,"pause"));Assert.AreEqual(before,s.CityCampaignTraffic(shop),1e-6);
                Assert.True(s.RespondBrandDeal(id,"end"));Assert.False(s.RespondBrandDeal(id,"end"));
            }
        }
        [Test] public void RegistrationConsumesSamplesOnlyOnceAndRemoteExpoCannotProgress()
        {
            using(var s=Make())
            {
                var shop=s.ShopAt(s.BrandBusiness.Brands[0].ShopIds[0]);shop.Sim.Stock.ForceAddGoods(Product,30);
                Assert.True(s.RegisterBrandExpo("b",shop.Place.Id,Product,"design"));Assert.AreEqual(27,shop.Sim.Stock.GoodsCount(Product));
                long balance=s.Ledger.Balance.Cents;Assert.False(s.RegisterBrandExpo("b",shop.Place.Id,Product,"design"));Assert.AreEqual(balance,s.Ledger.Balance.Cents);
                var e=s.BrandBusiness.Exhibits[0];Day(s,e.Day);Assert.False(s.ActBrandExpo(e.Id,"design"));
                At(s,"life_expo");Assert.True(s.ActBrandExpo(e.Id,"design"));Assert.False(s.ActBrandExpo(e.Id,"design"));
                Day(s,e.Day,630);Assert.True(s.ActBrandExpo(e.Id,"design"));Day(s,e.Day,660);Assert.True(s.ActBrandExpo(e.Id,"design"));
                Assert.True(s.BrandBusiness.Exhibits[0].Finished);Assert.AreEqual(1,s.BrandBusiness.Orders.Length);Assert.False(s.ActBrandExpo(e.Id,"design"));
                Assert.True(s.FulfilBrandOrder(s.BrandBusiness.Orders[0].Id));Assert.AreEqual(7,shop.Sim.Stock.GoodsCount(Product));
            }
        }
        [Test] public void OrderPaysOnArrivalOnceAndSurvivesSave()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;string shop=state.Brands[0].ShopIds[0];s.ShopAt(shop).Sim.Stock.ForceAddGoods(Product,25);
                state.Orders=new[]{new BrandOrder{Id="o",BrandId="b",ShopId=shop,ProductId=Product,Source="expo",Count=20,Due=80,UnitCents=10000,Influence=70}};Set(s,state);
                long money=s.Ledger.Balance.Cents;Assert.True(s.FulfilBrandOrder("o"));Assert.AreEqual(money-3000,s.Ledger.Balance.Cents);Assert.False(s.FulfilBrandOrder("o"));
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using(var loaded=GameSession.Load(s.City,save))
                {
                    var order=loaded.BrandBusiness.Orders[0];loaded.Clock.Restore(new GameDate((int)(order.Arrival/1440)),(int)(order.Arrival%1440));Delivery(loaded);
                    Assert.True(loaded.BrandBusiness.Orders[0].Done);Assert.AreEqual(money-3000+200000,loaded.Ledger.Balance.Cents);Delivery(loaded);Assert.AreEqual(money-3000+200000,loaded.Ledger.Balance.Cents);
                }
            }
        }
        [Test] public void InsufficientStockAndInvalidCommandsHaveNoMoneyEffect()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;state.Orders=new[]{new BrandOrder{Id="o",BrandId="b",ShopId=state.Brands[0].ShopIds[0],ProductId=Product,Count=20,Due=80}};Set(s,state);
                long money=s.Ledger.Balance.Cents;Assert.False(s.FulfilBrandOrder("o"));Assert.False(s.SignBrandDeal("b","fake","endorse",false));Assert.False(s.SignBrandResource("b","media","channel"));Assert.AreEqual(money,s.Ledger.Balance.Cents);
            }
        }
        [Test] public void TenderNeedsTrialAndFailureDoesNotAwardSeat()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;state.Brands[0].Influence=100;Set(s,state);string shop=state.Brands[0].ShopIds[0];s.ShopAt(shop).Sim.Stock.ForceAddGoods(Product,100);
                Assert.True(s.EnterBrandTender("b",shop,Product));var t=s.BrandBusiness.Tenders[0];Assert.True(s.SubmitBrandTender(t.Id,"cheap",false));Assert.False(s.EvaluateBrandTender(t.Id));
                Assert.True(s.FulfilBrandOrder(t.Id+":trial"));var o=s.BrandBusiness.Orders[0];s.Clock.Restore(new GameDate((int)(o.Arrival/1440)),(int)(o.Arrival%1440));Delivery(s);
                Assert.True(s.EvaluateBrandTender(t.Id));Assert.AreEqual(4,s.BrandBusiness.Tenders[0].Stage);Assert.AreEqual(1,s.BrandBusiness.Orders.Length);
            }
        }
        [Test] public void TenderWinRequiresThreeDistinctTimedDeliveries()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;state.Brands[0].Influence=100;Set(s,state);string shop=state.Brands[0].ShopIds[0];s.ShopAt(shop).Sim.Stock.ForceAddGoods(Product,150);
                Assert.True(s.EnterBrandTender("b",shop,Product));string id=s.BrandBusiness.Tenders[0].Id;
                Assert.True(s.ResearchBrandTender(id,1));Assert.True(s.ResearchBrandTender(id,2));Assert.False(s.ResearchBrandTender(id,2));Assert.True(s.SubmitBrandTender(id,"service",true));
                void Send(string orderId)
                {Assert.True(s.FulfilBrandOrder(orderId));var o=s.BrandBusiness.Orders.First(x=>x.Id==orderId);s.Clock.Restore(new GameDate((int)(o.Arrival/1440)),(int)(o.Arrival%1440));Delivery(s);}
                Send(id+":trial");Assert.True(s.EvaluateBrandTender(id));Assert.AreEqual(2,s.BrandBusiness.Tenders[0].Stage);
                for(int round=0;round<3;round++){string order=id+":delivery:"+round;Send(order);Day(s,s.BrandBusiness.Orders.First(o=>o.Id==order).Due);}
                Assert.AreEqual(3,s.BrandBusiness.Tenders[0].Stage);Assert.AreEqual(50,s.ShopAt(shop).Sim.Stock.GoodsCount(Product));Assert.AreEqual(4,s.BrandBusiness.Orders.Count(o=>o.Done));
            }
        }
        [Test] public void OverdueContractLosesSeatAndCannotBeRevived()
        {
            using(var s=Make())
            {
                var state=s.BrandBusiness;state.Tenders=new[]{new BrandTender{Id="t",BrandId="b",Stage=2,LastRound=0}};
                state.Orders=new[]{new BrandOrder{Id="t:delivery:0",BrandId="b",ProductId=Product,Due=41}};Set(s,state);Day(s,42);
                Assert.True(s.BrandBusiness.Orders[0].Failed);Assert.AreEqual(4,s.BrandBusiness.Tenders[0].Stage);Assert.False(s.FulfilBrandOrder("t:delivery:0"));
            }
        }
        [Test] public void GardenResourceIsLockedCostlyLimitedAndExpires()
        {
            using(var s=Make(2)){Assert.False(s.SignBrandResource("b","channel","channel"));}
            using(var s=Make())
            {
                long before=s.Ledger.Balance.Cents;Assert.True(s.SignBrandResource("b","channel","channel"));Assert.AreEqual(before-2000000,s.Ledger.Balance.Cents);Assert.False(s.SignBrandResource("b","channel","channel"));
                Day(s,131);Assert.True(s.SignBrandResource("b","channel","channel"));
            }
        }
        [Test] public void CopiesCannotMutateLiveStateAndOldSavesRemainValid()
        {
            using(var s=Make()){var copy=s.BrandBusiness;copy.Brands[0].ShopIds[0]="broken";Assert.AreNotEqual("broken",s.BrandBusiness.Brands[0].ShopIds[0]);var save=s.CreateSave();save.brandBusiness=null;using(var restored=GameSession.Load(s.City,save))Assert.Zero(restored.BrandBusiness.Brands.Length);}
        }
        [Test] public void ExpoIsAccessibleFromCommunityBeforeParkGate()
        {
            using(var s=Make(1)){var expo=s.City.GetPlace("life_expo");Assert.AreEqual(1,s.DistrictTierOf(expo));Assert.IsNull(s.DistrictAccessBlocker(expo));var gate=s.DistrictRoadGates.First(g=>g.Tier==2);var point=s.StandOf(expo);Assert.Less((point.X-gate.Center.X)*gate.Direction.X+(point.Y-gate.Center.Y)*gate.Direction.Y,0);}
        }
        [Test] public void QualificationAccumulatesRealRevenueAndFoundingIsUnique()
        {
            using(var s=Make(1))
            {
                Assert.True(s.DebugPrepareDistrictPrerequisites(2));Set(s,new BrandBusinessState());foreach(var shop in s.MyShops)shop.RetailKind="clothing";
                var suburb=s.MyShops.First(x=>s.DistrictTierOf(x.Place)==0);var community=s.MyShops.First(x=>s.DistrictTierOf(x.Place)==1);
                for(int day=41;day<=75;day++)
                {
                    s.Ledger.Credit(new GameDate(day-1),"零售",Money.FromYuan(20000),"fixture actual attributed sales",LedgerAttribution.ForShop(suburb.Place.Id));
                    s.Ledger.Credit(new GameDate(day-1),"零售",Money.FromYuan(2000),"fixture community sales",LedgerAttribution.ForShop(community.Place.Id));
                    Day(s,day);
                }
                Assert.IsNull(s.BrandFoundBlocker("clothing"),s.BrandQualificationText("clothing"));Assert.True(s.FoundBrand("clothing","棉风衣坊","quality"));Assert.False(s.FoundBrand("clothing","另一品牌","value"));
                Assert.AreEqual(s.MyShops.Count,s.BrandBusiness.Brands[0].ShopIds.Length);
            }
        }
        private static void Meeting(GameSession s,string node)
        {
            Assert.True(s.BeginDistrictMeeting(node));var m=s.DistrictStoryState;
            s.Clock.Restore(new GameDate((int)(m.MeetingAt/1440)),(int)(m.MeetingAt%1440));At(s,m.MeetingPlaceId);
            foreach(string id in m.MeetingPeople){var p=s.CityPersonState(id);p.Route=null;p.LocationPlaceId=m.MeetingPlaceId;}
            Assert.IsNull(s.DistrictMeetingBlocker);
        }
        [Test] public void NewMainlineRequiresActualOrderEvidenceThenUnlocksRoad()
        {
            using(var s=Make(1))
            {
                typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=1,Chapter=2,BrandCommerceRoute=true,NodeId="expo.review"});
                Meeting(s,"expo.review");Assert.False(s.ActDistrictNode("expo.review"));Assert.AreEqual(1,s.DistrictAccessTier);
                var state=s.BrandBusiness;state.Brands[0].Influence=100;Set(s,state);Assert.False(s.ActDistrictNode("expo.review"));
                state=s.BrandBusiness;state.Orders=new[]{new BrandOrder{Id="receipt",BrandId="b",Source="expo",Done=true}};Set(s,state);
                Assert.True(s.ActDistrictNode("expo.review"));Assert.AreEqual(2,s.DistrictAccessTier);
            }
        }
        [Test] public void GardenInvitationUsesOnlyPreviousCompletedIndustryMonth()
        {
            using(var s=Make(2))
            {
                string shop=s.BrandBusiness.Brands[0].ShopIds[0];Day(s,65);
                s.Ledger.Credit(new GameDate(65),"零售",Money.FromYuan(100000000),"current month not yet closed",LedgerAttribution.ForShop(shop));
                Assert.NotNull(s.DistrictPrerequisiteBlocker(3));Day(s,90);Assert.IsNull(s.DistrictPrerequisiteBlocker(3));
            }
        }
        [Test] public void AssignedExhibitionEmployeeTravelsAndCannotAlsoWorkInShop()
        {
            using(var s=Make())
            {
                var shop=s.ShopAt(s.BrandBusiness.Brands[0].ShopIds[0]);shop.Sim.Stock.ForceAddGoods(Product,5);
                var employee=new Employee{Name="展务测试员",ShiftStart=480,ShiftEnd=1320};shop.Salesperson=employee;
                string person=s.PersonIdOf(employee);Assert.True(s.RegisterBrandExpo("b",shop.Place.Id,Product,"buyers"));var e=s.BrandBusiness.Exhibits[0];
                Assert.True(s.AssignBrandExpoPerson(e.Id,person,true));long balance=s.Ledger.Balance.Cents;Assert.False(s.AssignBrandExpoPerson(e.Id,person,true));Assert.AreEqual(balance,s.Ledger.Balance.Cents);
                Day(s,e.Day,540);var p=s.CityPersonState(person);var point=s.StandOf(shop.Place);p.X=point.X;p.Y=point.Y;p.LocationPlaceId=shop.Place.Id;p.Route=null;
                Assert.False(s.CityEmployeeAvailable(employee,shop));
                Assert.True((bool)typeof(GameSession).GetMethod("TickBrandExpoPerson",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{p,s.Clock.Now.TotalMinutes}));
                Assert.NotNull(p.Route);Assert.AreEqual("life_expo",p.Route.TargetPlaceId);Assert.Greater(p.Route.ArriveMinute,s.Clock.Now.TotalMinutes);Assert.IsNull(p.LocationPlaceId);
            }
        }
        [Test] public void NewControversyCanOnlyBeNegotiatedOnce()
        {
            using(var s=Make())
            {
                Assert.True(s.SignBrandDeal("b","momo","endorse",false));var state=s.BrandBusiness;state.Deals[0].Severity=1;state.Deals[0].NewsDay=40;Set(s,state);string id=state.Deals[0].Id;
                Assert.True(s.RespondBrandDeal(id,"negotiate"));Assert.True(s.RespondBrandDeal(id,"continue"));long balance=s.Ledger.Balance.Cents;Assert.False(s.RespondBrandDeal(id,"negotiate"));Assert.AreEqual(balance,s.Ledger.Balance.Cents);
            }
        }
    }
}
