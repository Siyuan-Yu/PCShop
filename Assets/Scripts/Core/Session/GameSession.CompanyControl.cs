using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private int _companySystemDepth;
        private void OnTimeAdvanced(GameTimestamp from,GameTimestamp to)
        {
            _companySystemDepth++;
            try{OnTimeAdvancedCore(from,to);TickCompanyGovernance(to.TotalMinutes);}
            finally{_companySystemDepth--;}
        }
        private void OnDayAdvanced(GameDate previous,GameDate today)
        {
            _companySystemDepth++;
            try{OnDayAdvancedCore(previous,today);}
            finally{_companySystemDepth--;}
        }
        public bool PlayerManagesCompany(CompanyRecord company)
        {
            if(company==null)return true;var g=GovernanceOf(CompanyTicker(company));
            return g==null||g.ManagerId==PlayerShareholderId;
        }
        public bool PlayerManagesShop(OwnedShop shop)
        {
            var company=CompanyOfShop(shop);if(company==null)return true;
            var g=GovernanceOf(CompanyTicker(company));return g==null||g.ManagerId==PlayerShareholderId||g.RetainedShopIds.Contains(shop.Place.Id);
        }
        public string CompanyOfficeBlocker(CompanyRecord company) => _companySystemDepth>0||PlayerManagesCompany(company)?null:"公司由新管理层经营；你可在股东会议行使持股权利";
        public string ShopAuthorityBlocker(OwnedShop shop) => _companySystemDepth>0||PlayerManagesShop(shop)?null:"这家店已交给公司管理层；个人门店和获准留任的店仍可经营";
        private string EmployeeAuthorityBlocker(string id)
        {
            if(_companySystemDepth>0)return null;var row=FindEmployee(id);var company=CompanyOfEmployee(id);
            return row?.Shop!=null?ShopAuthorityBlocker(row.Shop):CompanyOfficeBlocker(company);
        }
        public string CompanyControlStatus(CompanyGovernance g)
        {
            if(g==null)return "暂无股权记录";
            if(g.HandoverAt>=0)return ShareholderName(g.PendingControllerId)+"控股 · 待交接";
            return ShareholderName(g.ControllerId)+"管理 · 总经理 "+ShareholderName(g.ManagerId);
        }
        private void ObserveCompanyControl(CompanyGovernance g)
        {
            string majority=MajorityShareholder(g);
            if(g.HandoverAt>=0&&majority!=g.PendingControllerId)
            {g.HandoverAt=-1;g.PendingControllerId=g.PendingManagerId=null;g.RetentionAccepted=false;g.RetentionOfferShopId=null;Say(GovernanceName(g.Ticker)+"的控股比例已变化，原交接计划已撤回。");}
            if(majority==null||majority==g.ControllerId||majority==PlayerShareholderId||g.HandoverAt>=0)return;
            PrepareCompanyHandover(g,majority,majority);
        }
        private void PrepareCompanyHandover(CompanyGovernance g,string controller,string manager)
        {
            g.PendingControllerId=controller;g.PendingManagerId=manager;g.HandoverAt=(Clock.Now.TotalMinutes/1440+1)*1440;
            g.RetentionAccepted=false;g.RetentionOfferShopId=controller!=PlayerShareholderId?CompanyShops(GovernanceOwnedCompany(g)).OrderBy(s=>s.Lease?.SignedDay??0).FirstOrDefault()?.Place.Id:null;
            AddStockNews(g.Ticker,Clock.Now.TotalMinutes,"控制权变更预告",ShareholderName(controller)+"持股超过 50%，将在 "+new GameTimestamp(g.HandoverAt)+"完成管理权交接。个人门店不受影响，公司员工、订单、库存和在途任务保留。");
            Say(GovernanceName(g.Ticker)+"已产生新控股人，请到公司治理查看交接及留任安排。");
        }
        public string CompanyTakeoverBlocker(CompanyGovernance g) => CompanyGovernanceGate??(g==null?"没有公司":MajorityShareholder(g)!=PlayerShareholderId?"须持有超过 50% 表决权，50% 不构成控股":g.HandoverAt>=0?"已有待完成交接":g.ControllerId==PlayerShareholderId?"你已控制这家公司":null);
        public bool ReorganizeControlledCompany(string ticker,string managerId,long dailyBudgetCents)
        {
            var g=GovernanceOf(ticker);string why=CompanyTakeoverBlocker(g);if(why!=null)return Fail(why);
            if(managerId!=PlayerShareholderId&&!_companyInvestors.Any(i=>i.Id==managerId&&!i.Id.StartsWith("public:",StringComparison.Ordinal)))return Fail("请选择总经理");
            if(dailyBudgetCents<10000||dailyBudgetCents>1000000000)return Fail("每日预算须为 ¥100—¥10,000,000");
            g.DailyBudgetCents=dailyBudgetCents;PrepareCompanyHandover(g,PlayerShareholderId,managerId);return true;
        }
        public bool SetCompanyRetention(string ticker,bool accept)
        {
            var g=GovernanceOf(ticker);if(g==null||g.HandoverAt<0||g.RetentionOfferShopId==null||g.PendingControllerId==PlayerShareholderId)return Fail("没有待处理的留任邀请");
            g.RetentionAccepted=accept;Say(accept?"已接受指定门店店长留任，交接后按授权经营并领取日薪 ¥200。":"已选择交接公司门店，保留股东身份与个人门店。");return true;
        }
        public bool LeaveCompanyManagement(string ticker)
        {
            var g=GovernanceOf(ticker);if(g==null||g.RetainedShopIds.Length==0)return false;
            g.RetainedShopIds=Array.Empty<string>();Say("已结束门店留任，原店继续由公司经营，持股保留。");return true;
        }
        private void TickCompanyGovernance(long now)
        {
            EnsureGovernanceCompanies();
            foreach(var g in _companyGovernance.ToArray())
            {
                ResolveCompanyMotions(g,now);
                if(g.HandoverAt>=0&&now>=g.HandoverAt)
                {
                    if(MajorityShareholder(g)!=g.PendingControllerId){g.HandoverAt=-1;g.PendingControllerId=g.PendingManagerId=null;continue;}
                    g.ControllerId=g.PendingControllerId;g.ManagerId=g.PendingManagerId;
                    g.RetainedShopIds=g.RetentionAccepted&&g.RetentionOfferShopId!=null?new[]{g.RetentionOfferShopId}:Array.Empty<string>();
                    g.PendingControllerId=g.PendingManagerId=null;g.HandoverAt=-1;g.PlayerDirector=g.ControllerId==PlayerShareholderId||g.PlayerDirector;
                    var company=GovernanceOwnedCompany(g);
                    foreach(var shop in CompanyShops(company))
                    {
                        if(!PlayerManagesShop(shop))shop.Sim.PlayerPresent=false;
                        shop.AutoBuild=true;
                    }
                    if(company!=null&&g.ManagerId!=PlayerShareholderId)EnsureNpcCompanyCoverage(company);
                    AddStockNews(g.Ticker,now,"管理权交接完成",CompanyControlStatus(g)+"。公司门店的员工、满意度、库存、订单、合同和采购任务继续执行。");
                    Say(GovernanceName(g.Ticker)+"管理权交接完成。");
                }
            }
        }
        private void EnsureNpcCompanyCoverage(CompanyRecord company)
        {
            foreach(var shop in CompanyShops(company))
            {
                var role=shop.RetailKind=="restaurant"?EmployeeRole.Waiter:EmployeeRole.Cashier;
                if(ShopStaff(shop,role).Any()||StaffCapacityBlocker(shop)!=null)continue;
                var person=new Employee{Id="company_manager:"+company.BrandId+":"+shop.Place.Id,Name="公司值班店员",DailyWageYuan=150,ShiftStart=540,ShiftEnd=1140,
                    CheckoutMinutes=3,Intelligence=55,Dexterity=60,Stamina=60,Charm=50};
                BindCityCandidate(person);EnsureEmployeeWellbeing(person,Clock.Now.TotalMinutes);AddShopEmployee(shop,role,person);_employeeHireShops[person.Id]=shop.Place.Id;
            }
        }
        private void SettleGovernanceOperatingDay(int day)
        {
            EnsureGovernanceCompanies();
            foreach(var g in _companyGovernance.ToArray())
            {
                if(g.LastOperatingDay>=day-1)continue;g.LastOperatingDay=day-1;
                var stock=StockState(g.Ticker);var company=GovernanceOwnedCompany(g);
                if(company==null&&stock!=null)
                {
                    ChangeGovernanceCash(g,stock.YesterdayProfitCents);
                    if(g.PromotionUntilDay>day)stock.BusinessScale=Math.Min(3,stock.BusinessScale+.001);
                }
                if(company!=null&&g.RetainedShopIds.Length>0&&g.LastRetentionPayDay<day&&GovernanceCash(g).Cents>=20000)
                {
                    g.LastRetentionPayDay=day;ChangeGovernanceCash(g,-20000);
                    company.Expenses=company.Expenses.Concat(new[]{new CompanyExpense{Day=day-1,Cents=20000,Label="留任店长工资"}}).ToArray();
                    Ledger.Credit(new GameDate(day),"店长工资",Money.FromYuan(200),GovernanceName(g.Ticker),LedgerAttribution.ForAsset(g.Ticker,"retention:pay:"+g.Ticker+":"+day));
                }
                if(company!=null&&g.ManagerId!=PlayerShareholderId)
                {
                    EnsureNpcCompanyCoverage(company);
                    foreach(var row in CompanyRoster(company).ToArray())
                    {
                        var request=row.Person.Wellbeing?.Request;if(request?.Outstanding!=true||request.Response==EmployeeRequestResponse.Promised)continue;
                        if(request.Kind==EmployeeRequestKind.Transfer&&row.Shop!=null)
                            ArrangeEmployeeWork(row.Person.Id,row.Shop.Place.Id,request.PreferredRole);
                        else if(GovernanceCash(g).Cents>=Math.Max(row.Person.DailyWageYuan,request.TargetWage)*700)
                            RespondEmployeeRequest(row.Person.Id,EmployeeRequestResponse.Promised);
                    }
                }
                long earnings=stock?.YesterdayProfitCents??0;
                int targetTrust=earnings>=0?70:45;
                long required=company!=null?CompanyCashRequirement(company).Cents:Math.Max(0,(stock?.YesterdayRevenueCents??0)*7);
                if(GovernanceCash(g).Cents<required)targetTrust-=15;
                if(company!=null&&CompanyRoster(company).Any(r=>r.Person.Wellbeing?.Satisfaction<40))targetTrust-=10;
                g.Trust=Math.Max(0,Math.Min(100,g.Trust+Math.Sign(targetTrust-g.Trust)));
                if(!CompanyGovernanceUnlocked||new GameDate(day).IsWeekend||g.LastNpcTradeDay>=day||stock==null)continue;
                g.LastNpcTradeDay=day;
                var investor=_companyInvestors.Where(i=>i.Id.StartsWith("capital_",StringComparison.Ordinal)).OrderBy(i=>i.Id).ElementAt((int)(((ulong)(uint)ConversationSeed(g.Ticker)+(uint)day)%3UL));
                if(ShareholderShares(g,PublicShareholderId)<g.TotalShares/20&&ShareholderShares(g,investor.Id)>0)
                {
                    long offered=Math.Min(ShareholderShares(g,investor.Id),Math.Max(1,g.TotalShares/200));
                    offered=Math.Min(offered,Math.Min(Math.Max(0,stock.AvailableUnits),PublicInvestor(g).CashCents/Math.Max(1,stock.PriceCents)));
                    if(offered>0){long gross=offered*stock.PriceCents;PublicInvestor(g).CashCents-=gross;investor.CashCents+=gross-CityInvestments.Fee(Money.FromCents(gross)).Cents;MoveShares(g,investor.Id,PublicShareholderId,offered);ConsumeStockLiquidity(g.Ticker,offered);}continue;
                }
                long amount=Math.Min(Math.Max(0,stock.AvailableUnits),Math.Min(Math.Max(1,g.TotalShares/100),StockPublicShares(g.Ticker)));
                amount=Math.Min(amount,investor.CashCents/Math.Max(1,stock.PriceCents));if(amount<=0)continue;
                long before=ShareholderShares(g,investor.Id);long cash=checked(stock.PriceCents*amount);long fee=CityInvestments.Fee(Money.FromCents(cash)).Cents;
                if(investor.CashCents<cash+fee)continue;
                investor.CashCents-=cash+fee;PublicInvestor(g).CashCents+=cash;MoveShares(g,PublicShareholderId,investor.Id,amount);
                ConsumeStockLiquidity(g.Ticker,amount);
                if(before*20/g.TotalShares!=ShareholderShares(g,investor.Id)*20/g.TotalShares)
                    AddStockNews(g.Ticker,Clock.Now.TotalMinutes,"大股东持股披露",investor.Name+"持股达到 "+ShareholderRatio(g,investor.Id).ToString("P2")+"，本次从公众股东受让 "+amount.ToString("N0")+" 股。");
            }
        }
        private void ConfigureCompanyCashAccounts()
        {
            Ledger.CashAccountOf=(attribution,category)=>
            {
                if(attribution==null||attribution.BusinessKind!=LedgerBusinessKind.Operating&&category!="房租")return null;
                var company=attribution.ActivityId!=null?Company(attribution.ActivityId):null;
                company??=attribution.ShopId!=null?CompanyOfShop(ShopAt(attribution.ShopId)):null;
                return company?.BrandId;
            };
            Ledger.CashAccountBalance=id=>Money.FromCents(Company(id)?.ReserveCents??0);
            Ledger.ChangeCashAccount=(id,delta)=>{var company=Company(id);if(company==null)throw new InvalidOperationException("公司账户不存在");company.ReserveCents=checked(company.ReserveCents+delta.Cents);};
        }
        public Money ShopAvailableCash(OwnedShop shop) => CompanyOfShop(shop) is CompanyRecord company?Money.FromCents(company.ReserveCents):Ledger.Balance;
        private bool ListedCompanyAsset(string shopId) => CompanyOfShop(ShopAt(shopId))?.ListedDay>=0;
        public Money CompanyStoreTransferPrice(OwnedShop shop) => shop==null?Money.Zero:CityShopInventoryCapital(shop,out _)+(shop.Lease?.Deposit??Money.Zero);
        public string CompanyStoreTransferBlocker(CompanyRecord company,OwnedShop shop)
        {
            if(company==null||shop==null||!shop.Rented||CompanyOfShop(shop)!=null)return "请选择未划入公司的个人门店";
            if(Brand(company.BrandId)?.Kind!=shop.RetailKind)return "只能划入同一行业的门店";
            if(ShopAuthorityBlocker(shop)!=null)return ShopAuthorityBlocker(shop);
            if(CompanyStoreTransferPrice(shop).Cents>company.ReserveCents)return "公司现金不足以按库存成本和押金受让";
            return null;
        }
        private string TransferStoreIntoCompany(CompanyGovernance g,string placeId,long agreedPrice)
        {
            var company=GovernanceOwnedCompany(g);var shop=ShopAt(placeId);string why=CompanyStoreTransferBlocker(company,shop);if(why!=null)return why;
            if(CompanyStoreTransferPrice(shop).Cents!=agreedPrice)return "门店库存或押金已变化，请重新确认受让报价";
            foreach(var employee in EmployeesForShop(shop))SettleEmployeeExitPay(employee.Person);
            company.ReserveCents-=agreedPrice;
            if(agreedPrice>0)Ledger.Credit(Clock.Today,"门店业务转让",Money.FromCents(agreedPrice),ShopTitle(shop)+"划入公司（库存成本及押金）",LedgerAttribution.ForAsset(g.Ticker,"store:transfer:"+g.Ticker+":"+placeId+":"+Clock.Now.TotalMinutes));
            company.ShopIds=company.ShopIds.Append(placeId).ToArray();company.StoreHistory=company.StoreHistory.Append(CompanyStoreSnapshot(placeId)).ToArray();
            Say("门店业务已划入公司，原员工、满意度、库存、订单及在途业务保留；房屋产权不随之转让。");return null;
        }
        public bool ContributePersonalCompanyStore(string id,string placeId)
        {
            var company=Company(id);if(IsGameOver||company==null||company.ListedDay>=0)return Fail("挂牌公司请走股东会议案");
            if(CompanyOfficeBlocker(company) is string why)return Fail(why);
            string result=TransferStoreIntoCompany(GovernanceOf(CompanyTicker(company)),placeId,CompanyStoreTransferPrice(ShopAt(placeId)).Cents);
            return result==null||Fail(result);
        }
    }
}
