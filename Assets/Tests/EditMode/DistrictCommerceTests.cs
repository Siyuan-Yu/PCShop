using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class DistrictCommerceTests
    {
        private static void Set(GameSession s,string field,object value)=>typeof(GameSession).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,value);
        private static void Invoke(GameSession s,string name,params object[] args)=>typeof(GameSession).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,args);
        private static string Product=>ClothingProducts.All.First().Id;
        private static GameSession Make(int chapter)
        {
            var s=DistrictProgressionTests.Make();s.DebugPrepareDistrictPrerequisites(1);s.DebugAddCash(1000000);
            var shop=s.MyShops.First();shop.RetailKind="clothing";shop.Sim.Stock.ForceAddGoods(Product,200);
            Set(s,"_brands",new BrandBusinessState{Brands=new[]{new BrandRecord{Id="b",Name="主线衣坊",Kind="clothing",Position="quality",Influence=chapter==3?100:0,ShopIds=new[]{shop.Place.Id}}}});
            Set(s,"_district",new DistrictProgress{UnlockedTier=chapter-1,Chapter=chapter,CommerceBrandId="b",NodeId=chapter==2?"expo.invitation":"tender.invitation"});return s;
        }
        private static void At(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.City.GetPlace(id));
        private static void Time(GameSession s,long minute)=>s.Clock.Restore(new GameDate((int)(minute/1440)),(int)(minute%1440));
        private static void Meet(GameSession s)
        {
            string id=s.CurrentDistrictNode.Id;Assert.True(s.BeginDistrictMeeting(id));var state=s.DistrictStoryState;
            Time(s,state.MeetingAt);At(s,state.MeetingPlaceId);
            foreach(string person in state.MeetingPeople){var p=s.CityPersonState(person);p.Route=null;p.LocationPlaceId=state.MeetingPlaceId;}
            Assert.True(s.ActDistrictNode(id));
        }
        private static void Deliver(GameSession s,BrandOrder order)
        {
            Assert.True(s.FulfilBrandOrder(order.Id));s.RefreshDistrictNarrative();
            var sent=s.BrandBusiness.Orders.Single(o=>o.Id==order.Id);Time(s,sent.Arrival);Invoke(s,"TickBrandDeliveries",sent.Arrival);s.RefreshDistrictNarrative();
        }
        [Test] public void ExpoMainlineUsesRealRegistrationReceptionDeliveryAndFinalMeeting()
        {
            using(var s=Make(2))
            {
                Meet(s);Assert.AreEqual("expo.prepare",s.CurrentDistrictNode.Id);Assert.False(s.BeginDistrictMeeting("expo.prepare"));
                s.Meet("momo",0);Assert.True(McnSigningTest.Sign(s,"b","momo"));Assert.True(s.RegisterBrandExpo("b",s.MyShops.First().Place.Id,Product,"design"));s.RefreshDistrictNarrative();Assert.AreEqual("expo.reception",s.CurrentDistrictNode.Id);
                var e=s.BrandBusiness.Exhibits.Single();Time(s,e.Day*1440L+540);At(s,"life_expo");
                var host=s.CityPersonState("momo");host.Route=null;host.LocationPlaceId="life_expo";host.ActivityId=null;host.VisitId=null;
                s.Player.Energy=s.Player.MaxEnergy;Assert.True(s.StartQuarterlyExpo(e.Id));
                for(int i=0;i<24;i++){var m=s.CurrentExpoMoment(e.Id);Assert.True(s.ChooseExpoAction(e.Id,Array.IndexOf(m.Scores,m.Scores.Max())));}
                s.RefreshDistrictNarrative();Assert.AreEqual("expo.delivery",s.CurrentDistrictNode.Id);Assert.AreEqual(1,s.DistrictAccessTier);
                Deliver(s,s.BrandBusiness.Orders.Single(o=>o.Source=="expo"));Assert.AreEqual("expo.review",s.CurrentDistrictNode.Id);Assert.AreEqual(1,s.DistrictAccessTier);
                Assert.False(s.ActDistrictNode("expo.review"));Meet(s);Assert.AreEqual(2,s.DistrictAccessTier);Assert.IsNull(s.CurrentDistrictNode);
                Assert.GreaterOrEqual(s.Relation("life_creator"), 20);
                Assert.AreEqual(1, s.CityStory("romance").Stage);
                Assert.IsNull(s.RomanceId);
            }
        }
        [Test] public void TenderMainlineRequiresTrialWinThreeRealPeriodsThenRecommendation()
        {
            using(var s=Make(3))
            {
                Meet(s);Assert.AreEqual("tender.prepare",s.CurrentDistrictNode.Id);
                Assert.True(s.EnterBrandTender("b",s.MyShops.First().Place.Id,Product));s.RefreshDistrictNarrative();Assert.AreEqual("tender.prepare",s.CurrentDistrictNode.Id);
                var t=s.BrandBusiness.Tenders.Single();DistrictProgressionTests.StandWithPerson(s,"life_enterprise");Assert.True(s.ResearchBrandTender(t.Id,1));s.RefreshDistrictNarrative();Assert.AreEqual("tender.prepare",s.CurrentDistrictNode.Id);
                DistrictProgressionTests.StandWithPerson(s,"life_tenant");Assert.True(s.ResearchBrandTender(t.Id,2));s.RefreshDistrictNarrative();Assert.AreEqual("tender.trial",s.CurrentDistrictNode.Id);Assert.True(s.SubmitBrandTender(t.Id,"service",true));
                Deliver(s,s.BrandBusiness.Orders.Single(o=>o.Source=="trial"));Assert.AreEqual("tender.decision",s.CurrentDistrictNode.Id);
                Assert.True(s.EvaluateBrandTender(t.Id));s.RefreshDistrictNarrative();Assert.AreEqual("tender.fulfilment",s.CurrentDistrictNode.Id);
                for(int round=0;round<3;round++)
                {
                    var order=s.BrandBusiness.Orders.Single(o=>o.Id==t.Id+":delivery:"+round);Deliver(s,order);Assert.AreEqual(2,s.DistrictAccessTier);
                    Time(s,order.Due*1440L+600);Invoke(s,"TickBrandBusiness",new GameDate(order.Due-1),new GameDate(order.Due));s.RefreshDistrictNarrative();
                }
                Assert.AreEqual("tender.review",s.CurrentDistrictNode.Id);Assert.AreEqual(2,s.DistrictAccessTier);Meet(s);Assert.AreEqual(3,s.DistrictAccessTier);
            }
        }
        [Test] public void OtherBrandReceiptsCannotCompleteSelectedBrandAndSelectionLocks()
        {
            using(var s=Make(2))
            {
                Meet(s);Assert.False(s.SelectDistrictCommerceBrand("other"));var b=s.BrandBusiness;
                b.Brands=b.Brands.Concat(new[]{new BrandRecord{Id="other",Name="其他品牌",Kind="models",Influence=100}}).ToArray();
                b.Exhibits=new[]{new BrandExhibit{Id="other:expo:75",BrandId="other",Finished=true}};
                b.Orders=new[]{new BrandOrder{Id="other:expo:75:lead",BrandId="other",Source="expo",Done=true}};Set(s,"_brands",b);
                s.RefreshDistrictNarrative();Assert.AreEqual("expo.prepare",s.CurrentDistrictNode.Id);Assert.AreEqual(1,s.DistrictAccessTier);
            }
        }
        [Test] public void MilestonesAndSelectedBrandSurviveSaveWithoutFreeProgress()
        {
            using(var s=Make(2))
            {
                Meet(s);s.Meet("momo",0);Assert.True(McnSigningTest.Sign(s,"b","momo"));Assert.True(s.RegisterBrandExpo("b",s.MyShops.First().Place.Id,Product,"buyers"));s.RefreshDistrictNarrative();
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {Assert.AreEqual("b",loaded.DistrictStoryState.CommerceBrandId);Assert.AreEqual("expo.reception",loaded.CurrentDistrictNode.Id);loaded.RefreshDistrictNarrative();Assert.AreEqual(1,loaded.DistrictAccessTier);Assert.NotNull(loaded.DistrictCommerceBlocker);}
            }
        }

        [Test] public void HistoricalQualifiedExpoCountsBeforeAcceptanceAndSurvivesLaterWorseResults()
        {
            using(var s=Make(2))
            {
                var b=s.BrandBusiness;b.Brands[0].Influence=20;
                b.Exhibits=new[]{
                    new BrandExhibit{Id="past",BrandId="b",Finished=true,Score=240,Day=10,InfluenceAfter=120},
                    new BrandExhibit{Id="new",BrandId="b",Finished=true,Score=300,Day=100},
                    new BrandExhibit{Id="future",BrandId="b",Day=190}};
                b.Orders=new[]{new BrandOrder{Id="past:lead",BrandId="b",Source="expo",Done=true}};Set(s,"_brands",b);
                // Discovery runs while still on the invitation, without advancing acceptance itself.
                s.RefreshDistrictNarrative();Assert.AreEqual("expo.invitation",s.CurrentDistrictNode.Id);
                Assert.True(s.BrandBusiness.Exhibits.Single(e=>e.Id=="past").MainlineQualified);
                var d=s.DistrictStoryState;d.NodeId="expo.prepare";Set(s,"_district",d);
                s.RefreshDistrictNarrative();Assert.AreEqual("expo.review",s.CurrentDistrictNode.Id);
                Assert.AreEqual(1,s.DistrictAccessTier);StringAssert.Contains("历史最高评分 300",s.DistrictCommerceStatus);
                StringAssert.Contains("历史成绩已达标",s.DistrictCommerceStatus);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {Assert.AreEqual("expo.review",loaded.CurrentDistrictNode.Id);Assert.IsNull(loaded.DistrictCommerceBlocker);}
            }
        }
        [Test] public void HighScoreAloneDoesNotInventHistoricalDelivery()
        {
            using(var s=Make(2))
            {
                var b=s.BrandBusiness;b.Brands[0].Influence=120;
                b.Exhibits=new[]{new BrandExhibit{Id="past",BrandId="b",Finished=true,Score=240,Day=10,InfluenceAfter=120}};
                b.Orders=new[]{new BrandOrder{Id="past:lead",BrandId="b",Source="expo",Shipped=true}};Set(s,"_brands",b);
                var d=s.DistrictStoryState;d.NodeId="expo.prepare";Set(s,"_district",d);s.RefreshDistrictNarrative();
                Assert.AreEqual("expo.delivery",s.CurrentDistrictNode.Id);Assert.False(s.BrandBusiness.Exhibits[0].MainlineQualified);
                Assert.NotNull(s.DistrictCommerceBlocker);Assert.AreEqual(1,s.DistrictAccessTier);
            }
        }
        [Test] public void FailedHistoryDoesNotHideNextRegisteredExpo()
        {
            using(var s=Make(2))
            {
                var b=s.BrandBusiness;b.Exhibits=new[]{
                    new BrandExhibit{Id="past",BrandId="b",Finished=true,Score=50,Day=10},
                    new BrandExhibit{Id="next",BrandId="b",Day=100}};Set(s,"_brands",b);
                var d=s.DistrictStoryState;d.NodeId="expo.prepare";Set(s,"_district",d);s.RefreshDistrictNarrative();
                Assert.AreEqual("expo.reception",s.CurrentDistrictNode.Id);
                StringAssert.Contains("历史最高评分 50",s.DistrictCommerceStatus);
                Assert.NotNull(s.DistrictCommerceBlocker);
            }
        }
        [Test] public void FailedTenderDoesNotUnlockRoadAndExplainsRetry()
        {
            using(var s=Make(3))
            {
                var b=s.BrandBusiness;b.Tenders=new[]{new BrandTender{Id="b:tender:0",BrandId="b",Stage=4}};Set(s,"_brands",b);
                var d=s.DistrictStoryState;d.NodeId="tender.fulfilment";Set(s,"_district",d);s.RefreshDistrictNarrative();
                Assert.AreEqual("tender.fulfilment",s.CurrentDistrictNode.Id);StringAssert.Contains("下年度",s.DistrictCommerceBlocker);Assert.AreEqual(2,s.DistrictAccessTier);
            }
        }

    }
}
