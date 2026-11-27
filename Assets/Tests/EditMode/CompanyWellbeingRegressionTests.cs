using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    // 只用反射准备阶段/公司初态；交易、投票、调薪、休假、时间推进和读档均走正式接口。
    public sealed class CompanyWellbeingRegressionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static GameSession Ready(int tier=3)
        {
            var layout=GameSessionTests.Layout();
            layout.places=layout.places.Concat(new[]{new PCShopSell.Core.World.LayoutPlaceData{id=GameSession.EnterpriseCenterId,name="企业服务中心",kind="shop",node="st",explicitEntrance=true,openMinute=0,closeMinute=1440}}).ToArray();
            var s=new GameSession(PCShopSell.Core.World.CityLayout.FromData(layout),7,Money.FromYuan(200000000),GameTimestamp.From(new GameDate(0),540));
            s.EventsEnabled=false; s.PlayerShop.Settings.BaseCustomersPerHour=0;
            Tier(s,tier); GameSessionTests.Teleport(s,"pc"); return s;
        }
        internal static void Tier(GameSession s,int tier) => ((DistrictProgress)typeof(GameSession).GetField("_district",Private).GetValue(s)).UnlockedTier=tier;
        internal static GameSession Reload(GameSession s)
        {
            var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            loaded.EventsEnabled=false;return loaded;
        }
        internal static void Advance(GameSession s,int minutes)
        {
            long target=s.Clock.Now.TotalMinutes+minutes;
            while(s.Clock.Now.TotalMinutes<target)
            {
                s.Player.Health=s.Player.Hunger=s.Player.Energy=100;
                long before=s.Clock.Now.TotalMinutes;
                s.AdvanceClock((int)Math.Min(30,target-before));
                Assert.Greater(s.Clock.Now.TotalMinutes,before,"时间推进停滞，必须记录阻塞而非死循环");
            }
        }
        static CompanyRecord Own(GameSession s)
        {
            typeof(GameSession).GetMethod("CreateBrandAndCompany",Private).Invoke(s,new object[]{"computer","测试公司","value",new[]{s.MainShop.Place.Id}});
            var c=s.Companies.Single();c.ReserveCents=10000000;return c;
        }
        static Employee Worker(GameSession s)
        {
            var e=new Employee{Id="qa_worker",Name="测试员工",DailyWageYuan=120,ShiftStart=540,ShiftEnd=1020,CheckoutMinutes=3,Dexterity=65,Intelligence=65,Stamina=65,Charm=60};
            s.MainShop.Employee=e;s.EmployeeWellbeingOf(e.Id);return e;
        }
        static CompanyRecord ListingReady(GameSession s)
        {
            var c=Own(s);
            // 准备完整盈利月；挂牌及认购随后使用公开接口。
            s.Ledger.Credit(new GameDate(30),"零售",Money.FromYuan(100000),"上月营业收入",LedgerAttribution.ForShop("pc","qa:last-month"));
            s.Clock.Restore(new GameDate(60),600);
            GameSessionTests.Teleport(s,GameSession.EnterpriseCenterId);return c;
        }
        [Test] public void IpoTransfersInstitutionCashToCompanyExactlyOnceAndPersists()
        {
            using var s=Ready();var c=ListingReady(s);var cash=s.Ledger.Balance;
            var g=s.GovernanceOf(s.CompanyTicker(c));long reserve=c.ReserveCents;
            long wallets=s.CompanyInvestors.Where(i=>i.Id.StartsWith("capital_")).Sum(i=>i.CashCents);
            long raised=s.CompanyListingPrice(c).Cents*GameSession.CompanyPublicShares;
            Assert.IsNull(s.CompanyListingBlocker(c));Assert.True(s.ListCompany(c.BrandId));
            Assert.AreEqual(cash,s.Ledger.Balance);Assert.AreEqual(reserve+raised,c.ReserveCents);
            Assert.AreEqual(wallets-raised,s.CompanyInvestors.Where(i=>i.Id.StartsWith("capital_")).Sum(i=>i.CashCents));
            Assert.AreEqual(1250000,g.TotalShares);Assert.AreEqual(1000000,s.StockState(g.Ticker).Units);
            Assert.False(s.ListCompany(c.BrandId));using var loaded=Reload(s);
            Assert.AreEqual(1000000,loaded.StockState(g.Ticker).Units);Assert.AreEqual(c.ReserveCents,loaded.Companies.Single().ReserveCents);Invariants(loaded);
        }
        [TestCase(true)] [TestCase(false)]
        public void NpcTakeoverKeepsPersonalShopAndHonoursRetentionAfterReload(bool retain)
        {
            using var s=Ready();var c=ListingReady(s);Assert.True(s.ListCompany(c.BrandId),s.CompanyListingBlocker(c));
            var g=s.GovernanceOf(s.CompanyTicker(c));
            s.PlayerShop.Reputation=80;s.DebugAddListing("tea",0,60,Money.FromYuan(3000));GameSessionTests.Teleport(s,"agency");
            Assert.True(s.SignLease("tea",true,"computer"));
            Assert.True(s.TradeCompanyBlock(g.Ticker,"capital_qing",380001,false));Assert.Greater(g.HandoverAt,0);
            Assert.True(s.SetCompanyRetention(g.Ticker,retain));using var loaded=Reload(s);
            var lg=loaded.GovernanceOf(g.Ticker);CompanyWellbeingRegressionTests.Advance(loaded,(int)(lg.HandoverAt-loaded.Clock.Now.TotalMinutes));
            Assert.AreEqual("capital_qing",lg.ControllerId);Assert.AreEqual(retain,loaded.PlayerManagesShop(loaded.ShopAt("pc")));
            Assert.True(loaded.PlayerManagesShop(loaded.ShopAt("tea")));Assert.AreEqual(2,loaded.MyShopCount);
            Assert.AreEqual(619999,loaded.StockState(g.Ticker).Units);Invariants(loaded);
        }
        static void Holdings(GameSession s,CompanyGovernance g,long player,string other="founder:rival_pc")
        {
            g.Holders=new[]{new CompanyShareholder{OwnerId="player",Shares=player},new CompanyShareholder{OwnerId=other,Shares=g.TotalShares-player}};
            if(s.StockState(g.Ticker)!=null)s.StockState(g.Ticker).Units=player;
        }
        internal static void Invariants(GameSession s)
        {
            foreach(var g in s.GovernanceCompanies)
            {
                Assert.AreEqual(g.TotalShares,g.Holders.Sum(h=>h.Shares),g.Ticker+" 股本守恒");
                Assert.True(g.Holders.All(h=>h.Shares>=0),g.Ticker);
                Assert.AreEqual(g.Holders.Length,g.Holders.Select(h=>h.OwnerId).Distinct().Count());
                if(s.StockState(g.Ticker)!=null)Assert.AreEqual(s.ShareholderShares(g,"player"),s.StockState(g.Ticker).Units);
            }
            Assert.True(s.CompanyInvestors.All(i=>i.CashCents>=0),"机构不能凭空借款买股");
            Assert.AreEqual(s.Employees.Count,s.Employees.Select(e=>e.Person.Id).Distinct().Count(),"员工不能重复占位");
            foreach(var e in s.Employees)
            {
                var w=s.EmployeeWellbeingOf(e.Person.Id);
                Assert.That(w.Satisfaction,Is.InRange(0,100));Assert.That(w.Energy,Is.InRange(0,100));
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void NewEquityOperationsAreLockedBeforeGarden(int tier)
        {
            using var s=Ready(tier);var g=s.GovernanceOf("rival_pc");var cash=s.Ledger.Balance;
            Assert.False(s.TradeCompanyBlock(g.Ticker,"founder:rival_pc",10,true));
            Assert.False(s.TradeCityInvestment(g.Ticker,1,true,true));
            Assert.False(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Director,0));
            Assert.AreEqual(cash,s.Ledger.Balance);Invariants(s);
        }
        [Test] public void BlockTradeTransfersRealSharesAndCashAndRejectsOversell()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");string seller="founder:rival_pc";
            long shares=s.ShareholderShares(g,seller),wallet=s.CompanyInvestors.Single(i=>i.Id==seller).CashCents;
            var cash=s.Ledger.Balance;var gross=s.CompanyBlockPrice(g.Ticker,100,true);
            Assert.True(s.TradeCompanyBlock(g.Ticker,seller,100,true),s.CompanyBlockTradeBlocker(g.Ticker,seller,100,true));
            Assert.AreEqual(cash-gross-CityInvestments.Fee(gross),s.Ledger.Balance);
            Assert.AreEqual(wallet+gross.Cents,s.CompanyInvestors.Single(i=>i.Id==seller).CashCents);
            Assert.AreEqual(shares-100,s.ShareholderShares(g,seller));Assert.AreEqual(100,s.StockState(g.Ticker).Units);
            var snapshot=s.Ledger.Balance;Assert.False(s.TradeCompanyBlock(g.Ticker,seller,101,false));Assert.AreEqual(snapshot,s.Ledger.Balance);
            Assert.True(s.TradeCompanyBlock(g.Ticker,seller,100,false));Invariants(s);
        }
        [Test] public void ExistingSharesCanExitBeforeGarden()
        {
            using var s=Ready();Assert.True(s.TradeCityInvestment("rival_pc",10,true,true));
            Tier(s,0);Assert.True(s.TradeCityInvestment("rival_pc",10,false,true));Invariants(s);
        }
        [Test] public void ExactlyHalfAndAggregatedPublicDoNotControlCompany()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares/2);
            Assert.IsNull(s.MajorityShareholder(g));Assert.IsNotNull(s.CompanyTakeoverBlocker(g));
            Holdings(s,g,0,"public");Assert.IsNull(s.MajorityShareholder(g));
        }
        [Test] public void TakeoverSurvivesSaveAndExecutesOnlyOnce()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares/2+1);
            Assert.True(s.ReorganizeControlledCompany(g.Ticker,"player",1000000));Assert.False(s.ReorganizeControlledCompany(g.Ticker,"player",1000000));
            using var loaded=Reload(s);var lg=loaded.GovernanceOf(g.Ticker);long at=lg.HandoverAt;
            Advance(loaded,(int)(at-loaded.Clock.Now.TotalMinutes));
            Assert.AreEqual("player",lg.ControllerId);Assert.AreEqual("player",lg.ManagerId);Assert.AreEqual(-1,lg.HandoverAt);
            Advance(loaded,60);Assert.False(loaded.ReorganizeControlledCompany(g.Ticker,"player",1000000));Invariants(loaded);
        }
        [Test] public void LosingMajorityBeforeHandoverCancelsIt()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares/2+1);
            Assert.True(s.ReorganizeControlledCompany(g.Ticker,"player",1000000));
            Assert.True(s.TradeCompanyBlock(g.Ticker,"capital_qing",2,false));Assert.AreEqual(-1,g.HandoverAt);
            Assert.AreNotEqual("player",g.ControllerId);Invariants(s);
        }
        [Test] public void IssueWaitsForVoteAndDoesNotRepeatAfterReload()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares*6/10);
            long total=g.TotalShares,before=s.ShareholderShares(g,"player"),companyCash=g.CashCents;
            Assert.True(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Issue,1000,"player"));
            var motion=g.Resolutions.Single();long subscription=motion.PriceCents*1000;
            Assert.False(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Budget,1000000));
            Assert.AreEqual(total,g.TotalShares);Advance(s,1440);
            Assert.True(motion.Passed,motion.Result);Assert.AreEqual(total,motion.VotingShares);Assert.AreEqual(total+1000,g.TotalShares);
            Assert.AreEqual(before+1000,s.ShareholderShares(g,"player"));Assert.GreaterOrEqual(g.CashCents,companyCash+subscription-10000000);
            using var loaded=Reload(s);Advance(loaded,1);Assert.AreEqual(total+1000,loaded.GovernanceOf(g.Ticker).TotalShares);Invariants(loaded);
        }
        [Test] public void IssueRechecksSubscriberFundsAtSettlement()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares*6/10);
            Assert.True(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Issue,1000,"player"));
            s.Ledger.ForceDebit(s.Clock.Today,"测试",s.Ledger.Balance,"认购前资金已用尽");long total=g.TotalShares;
            Advance(s,1440);Assert.False(g.Resolutions.Single().Passed);Assert.AreEqual(total,g.TotalShares);Invariants(s);
        }
        [Test] public void DilutionBelowDirectorThresholdRemovesBoardPrivilege()
        {
            using var s=Ready();var g=s.GovernanceOf("rival_pc");Holdings(s,g,g.TotalShares*6/100);
            g.PlayerDirector=true;g.Trust=90;g.CashCents=0;
            Assert.True(s.ProposeCompanyResolution(g.Ticker,CompanyResolutionKind.Issue,g.TotalShares/2,"capital_qing"));
            Advance(s,1440);Assert.True(g.Resolutions.Single().Passed,g.Resolutions.Single().Result);
            Assert.Less(s.ShareholderRatio(g,"player"),GameSession.CompanyDirectorThreshold);
            Assert.False(s.CompanyBoardMember(g),"稀释后不再满足席位持股条件");Invariants(s);
        }
        [Test] public void ReturnedBuyerWithUndeliveredGoodsCannotStartLeave()
        {
            using var s=Ready();var e=Worker(s);
            var trips=(List<EmployeePurchaseTrip>)typeof(GameSession).GetField("_employeeTrips",Private).GetValue(s);
            trips.Add(new EmployeePurchaseTrip{EmployeeId=e.Id,Returned=true,Delivered=false});
            Assert.True(s.EmployeeHandoverPending(e.Id));Assert.IsNull(s.EmployeeTrip(e.Id));
            Assert.False(s.ApproveEmployeeLeave(e.Id),"人已回来但货没交接，仍不能休假");
            trips.Clear();Assert.True(s.ApproveEmployeeLeave(e.Id));
        }
        [Test] public void CompanyOperatingCashIsSeparateAndPersists()
        {
            using var s=Ready();var company=Own(s);var personal=s.Ledger.Balance;long reserve=company.ReserveCents;
            Assert.True(s.Ledger.TryDebit(s.Clock.Today,"进货",Money.FromYuan(500),"测试",LedgerAttribution.ForShop("pc","qa:purchase")));
            Assert.AreEqual(personal,s.Ledger.Balance);Assert.AreEqual(reserve-50000,company.ReserveCents);
            Assert.False(s.Ledger.TryDebit(s.Clock.Today,"进货",Money.FromCents(reserve),"不足",LedgerAttribution.ForShop("pc")));
            using var loaded=Reload(s);Assert.AreEqual(personal,loaded.Ledger.Balance);Assert.AreEqual(company.ReserveCents,loaded.Companies.Single().ReserveCents);
            Assert.AreEqual(company.BrandId,loaded.Ledger.Entries.Single(e=>e.SourceEventId=="qa:purchase").CashAccountId);
        }
        [Test] public void NpcManagedCompanyDeniesEmployeeAndShopChanges()
        {
            using var s=Ready();var worker=Worker(s);var company=Own(s);var g=s.GovernanceOf(s.CompanyTicker(company));g.ManagerId="capital_qing";
            Assert.False(s.PlayerManagesShop(s.MainShop));Assert.False(s.SetEmployeeDailyWage(worker.Id,10000));Assert.False(s.ApproveEmployeeLeave(worker.Id));
            Assert.AreEqual(120,worker.DailyWageYuan);
            g.RetainedShopIds=new[]{"pc"};Assert.True(s.PlayerManagesShop(s.MainShop));Assert.True(s.SetEmployeeDailyWage(worker.Id,150));
        }
        [Test] public void GovernanceRefreshAndReloadNeverMintInvestorCash()
        {
            using var s=Ready();Invariants(s);var wallet=s.CompanyInvestors.ToDictionary(i=>i.Id,i=>i.CashCents);
            for(int i=0;i<50;i++){s.GovernanceOf("rival_pc");s.CreateSave();}
            using var loaded=Reload(s);foreach(var pair in wallet)Assert.AreEqual(pair.Value,loaded.CompanyInvestors.Single(i=>i.Id==pair.Key).CashCents);
            Invariants(loaded);
        }
        [Test] public void SalaryChangeDuringShiftPreservesCurrentPayrollThroughReload()
        {
            using var s=Ready();var e=Worker(s);Advance(s,30);Assert.True(s.SetEmployeeDailyWage(e.Id,180));
            Assert.AreEqual(120,e.DailyWageYuan);Assert.AreEqual(180,e.Wellbeing.PendingWage);
            using var loaded=Reload(s);var le=loaded.FindEmployee(e.Id).Person;Advance(loaded,450);
            Assert.AreEqual(180,le.DailyWageYuan);Assert.AreEqual(12000,loaded.EmployeePayroll.First(p=>p.EmployeeId==e.Id).DailyCents);
            Invariants(loaded);
        }
        [Test] public void CompletedShiftChangesMoraleOnceAndTransferPreservesIt()
        {
            using var s=Ready();var e=Worker(s);double initial=e.Wellbeing.Satisfaction;
            Advance(s,480);Assert.AreEqual(1,e.Wellbeing.CompletedShifts);Assert.LessOrEqual(Math.Abs(e.Wellbeing.Satisfaction-initial),3);
            double after=e.Wellbeing.Satisfaction;Advance(s,60);Assert.AreEqual(after,e.Wellbeing.Satisfaction);
            Assert.True(s.ArrangeEmployeeWork(e.Id,"pc",EmployeeRole.Assembler));Assert.AreEqual(after,s.FindEmployee(e.Id).Person.Wellbeing.Satisfaction);
            using var loaded=Reload(s);Assert.AreEqual(after,loaded.EmployeeWellbeingOf(e.Id).Satisfaction);Assert.AreEqual(1,loaded.EmployeeWellbeingOf(e.Id).CompletedShifts);
        }
        [Test] public void RequestsMayDeferOnceAndAgreementDoesNotInstantlyRaiseMorale()
        {
            using var s=Ready();var e=Worker(s);var w=e.Wellbeing;
            w.Request=new EmployeeWorkRequest{Kind=EmployeeRequestKind.Pay,TargetWage=180,DueAfterShift=3};
            Assert.True(s.RespondEmployeeRequest(e.Id,EmployeeRequestResponse.Deferred));Assert.AreEqual(5,w.Request.DueAfterShift);
            Assert.False(s.RespondEmployeeRequest(e.Id,EmployeeRequestResponse.Deferred));
            Assert.True(s.RespondEmployeeRequest(e.Id,EmployeeRequestResponse.Promised));Assert.AreEqual(65,w.Satisfaction);Assert.AreNotEqual(EmployeeRequestResponse.Fulfilled,w.Request.Response);
        }
        [Test] public void LeaveIsNextShiftPaidAndCannotBeRepeatedOrCreditedTwice()
        {
            using var s=Ready();var e=Worker(s);Assert.True(s.ApproveEmployeeLeave(e.Id));Assert.False(s.ApproveEmployeeLeave(e.Id));
            var w=e.Wellbeing;Assert.AreEqual(1980,w.LeaveFrom);Assert.AreEqual(1440,w.LeaveUntil-w.LeaveFrom);
            Advance(s,(int)(w.LeaveFrom-s.Clock.Now.TotalMinutes));int shifts=w.CompletedShifts;
            Advance(s,1440);Assert.AreEqual(shifts,w.CompletedShifts);Assert.True(w.LeaveCredited);
            Assert.True(s.EmployeePayroll.Any(p=>p.EmployeeId==e.Id&&p.Start==w.LeaveFrom&&p.BasePaid),"休假仍须发基本工资");
            double morale=w.Satisfaction;using var loaded=Reload(s);Advance(loaded,1);Assert.AreEqual(morale,loaded.EmployeeWellbeingOf(e.Id).Satisfaction);
        }
        [TestCase(7)] [TestCase(41)] [TestCase(914)]
        public void RandomShareOperationsAreReplayableAndConserveOwnership(int seed)
        {
            var random=new System.Random(seed);GameSession s=Ready();
            try
            {
                for(int step=0;step<80;step++)
                {
                    int operation=random.Next(4),units=random.Next(1,100);
                    TestContext.Progress.WriteLine($"seed={seed} step={step} operation={operation} units={units}");
                    if(operation<2)s.TradeCompanyBlock("rival_pc","founder:rival_pc",units,operation==0);
                    else if(operation==2)s.TradeCityInvestment("rival_pc",units,random.Next(2)==0,true);
                    else {var next=Reload(s);s.Dispose();s=next;}
                    Invariants(s);
                }
            }
            finally{s.Dispose();}
        }
    }
}
