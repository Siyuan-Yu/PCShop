using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const string PlayerShareholderId = "player";
        public const string PublicShareholderId = "public";
        public const double CompanyMeetingThreshold = .01, CompanyDirectorThreshold = .05;
        private readonly List<CompanyGovernance> _companyGovernance = new List<CompanyGovernance>();
        private readonly List<CompanyInvestor> _companyInvestors = new List<CompanyInvestor>();
        public IReadOnlyList<CompanyGovernance> GovernanceCompanies { get { EnsureGovernanceCompanies(); return _companyGovernance; } }
        public IReadOnlyList<CompanyInvestor> CompanyInvestors => _companyInvestors;
        public bool CompanyGovernanceUnlocked => DistrictAccessTier >= 3;
        public string CompanyGovernanceGate => !CompanyGovernanceUnlocked ? "花园开放后解锁公司股权与治理" : IsGameOver ? "当前不能操作" : null;
        public string GovernanceName(string ticker) => ticker?.StartsWith("listed:",StringComparison.Ordinal)==true ? CompanyName(Company(ticker.Substring(7))) : StockDefinition(ticker)?.Name ?? "公司";
        public string ShareholderName(string owner) => owner==PlayerShareholderId?"你":owner==PublicShareholderId?"公众股东":_companyInvestors.Find(x=>x.Id==owner)?.Name??"机构股东";
        public string ShareholderPortrait(string owner) => owner==PlayerShareholderId?null:_companyInvestors.Find(x=>x.Id==owner)?.PersonId;
        public CompanyGovernance GovernanceOf(string ticker)
        {
            if(string.IsNullOrEmpty(ticker)||ticker=="hardware")return null;
            EnsureGovernanceCompanies(); return _companyGovernance.Find(x=>x.Ticker==ticker);
        }
        private CompanyRecord GovernanceOwnedCompany(CompanyGovernance g) => g?.Ticker?.StartsWith("listed:",StringComparison.Ordinal)==true?Company(g.Ticker.Substring(7)):null;
        public CompanyRecord CompanyRecordFor(string ticker) => GovernanceOwnedCompany(GovernanceOf(ticker));
        public long ShareholderShares(CompanyGovernance g,string owner) => g?.Holders.FirstOrDefault(h=>h.OwnerId==owner)?.Shares??0;
        public double ShareholderRatio(CompanyGovernance g,string owner) => g?.TotalShares>0?ShareholderShares(g,owner)/(double)g.TotalShares:0;
        public string MajorityShareholder(CompanyGovernance g) => g?.Holders.FirstOrDefault(h=>h.OwnerId!=PublicShareholderId&&h.Shares>g.TotalShares/2)?.OwnerId;
        public long CompanyShareCapital(CompanyRecord company) => GovernanceOf(CompanyTicker(company))?.TotalShares ?? (company?.ListedDay>=0?CompanyTotalShares:CompanyFounderShares);
        public long CompanyPlayerShares(CompanyRecord company) => ShareholderShares(GovernanceOf(CompanyTicker(company)),PlayerShareholderId);
        public string CompanyBusinessKind(CompanyRecord company) => Brand(company?.BrandId)?.Kind;
        public bool GovernanceNeedsAttention(CompanyGovernance g) => g!=null&&(ShareholderShares(g,PlayerShareholderId)>0||GovernanceOwnedCompany(g)!=null)
            &&(g.HandoverAt>=0||g.Resolutions.Any(r=>!r.Closed)||MajorityShareholder(g)==PlayerShareholderId&&g.ControllerId!=PlayerShareholderId);
        public bool PlayerManagesEmployee(string id) => EmployeeAuthorityBlocker(id)==null;
        public long StockPublicShares(string ticker) => Math.Max(0,ShareholderShares(GovernanceOf(ticker),PublicShareholderId)-(StockState(ticker)?.ShortUnits??0));
        public Money GovernanceCash(CompanyGovernance g) => Money.FromCents(GovernanceOwnedCompany(g)?.ReserveCents??g?.CashCents??0);
        private void ChangeGovernanceCash(CompanyGovernance g,long delta)
        {var company=GovernanceOwnedCompany(g);if(company!=null)company.ReserveCents=checked(company.ReserveCents+delta);else g.CashCents=checked(g.CashCents+delta);}
        private CompanyInvestor Investor(string id,string name=null,long cash=0,string portrait=null)
        {
            var investor=_companyInvestors.Find(x=>x.Id==id);if(investor!=null)return investor;
            investor=new CompanyInvestor{Id=id,Name=name??id,CashCents=Math.Max(0,cash),PersonId=portrait};_companyInvestors.Add(investor);return investor;
        }
        private CompanyInvestor PublicInvestor(CompanyGovernance g) => Investor("public:"+g.Ticker,"公众资金池");
        private void EnsureGovernanceCompanies()
        {
            Investor("capital_qing","晴川资本",12000000000L,"res_tang");
            Investor("capital_xing","兴华实业投资",10000000000L,"res_shen");
            Investor("capital_qiao","星桥成长基金",8000000000L,"res_yuan");
            foreach(var stock in _cityEquities.ToArray())
                if(_companyGovernance.All(g=>g.Ticker!=stock.Id))CreateGovernance(stock.Id,ListedCompanyOf(stock.Id));
            foreach(var company in _companies)
                if(_companyGovernance.All(g=>g.Ticker!=CompanyTicker(company)))CreateGovernance(CompanyTicker(company),company);
        }
        private CompanyGovernance CreateGovernance(string ticker,CompanyRecord company)
        {
            var stock=StockState(ticker);long total=company==null?StockDefinition(ticker)?.TotalShares??1000000:company.ListedDay>=0?CompanyTotalShares:CompanyFounderShares;
            // 旧挂牌公司的额外创始股份只迁移一次，之后全都进入同一可交易持仓。
            long player=Math.Max(0,stock?.Units??0)+(company?.ListedDay>=0?CompanyFounderShares:company!=null?CompanyFounderShares:0);
            total=Math.Max(total,player);long remaining=total-player;
            var g=new CompanyGovernance{Ticker=ticker,TotalShares=total,ControllerId=company==null?"founder:"+ticker:PlayerShareholderId,
                ManagerId=company==null?"founder:"+ticker:PlayerShareholderId,LastOperatingDay=Clock.Today.TotalDays-1,LastNpcTradeDay=Clock.Today.TotalDays};
            g.Holders=new[]{new CompanyShareholder{OwnerId=PlayerShareholderId,Shares=player}};
            if(company==null)
            {
                long cap=checked(total*Math.Max(100,stock?.PriceCents??100));
                Investor("founder:"+ticker,GovernanceName(ticker)+"创始人",cap/5);
                SetHolding(g,"founder:"+ticker,remaining*45/100);
                SetHolding(g,"capital_qing",remaining*15/100);SetHolding(g,"capital_qiao",remaining*10/100);
                g.CashCents=Math.Max(10000000,cap/10);
            }
            long allocated=g.Holders.Sum(h=>h.Shares);SetHolding(g,PublicShareholderId,total-allocated);
            long publicCash=Math.Max(10000000,total*Math.Max(100,stock?.PriceCents??company?.ListingPriceCents??100));
            Investor("public:"+ticker,"公众资金池",publicCash);
            if(stock!=null)stock.Units=player;
            if(player>total/2)g.ControllerId=g.ManagerId=PlayerShareholderId;
            _companyGovernance.Add(g);return g;
        }
        private static void SetHolding(CompanyGovernance g,string owner,long shares)
        {
            var h=g.Holders.FirstOrDefault(x=>x.OwnerId==owner);
            if(h==null){h=new CompanyShareholder{OwnerId=owner};g.Holders=g.Holders.Concat(new[]{h}).ToArray();}
            h.Shares=Math.Max(0,shares);
        }
        private void MoveShares(CompanyGovernance g,string from,string to,long units)
        {
            if(units<=0||units>ShareholderShares(g,from))throw new InvalidOperationException("股份来源不足");
            SetHolding(g,from,ShareholderShares(g,from)-units);SetHolding(g,to,checked(ShareholderShares(g,to)+units));
            if(StockState(g.Ticker) is CityEquityState stock)stock.Units=ShareholderShares(g,PlayerShareholderId);
            if(ShareholderRatio(g,PlayerShareholderId)<CompanyDirectorThreshold)g.PlayerDirector=false;
            ObserveCompanyControl(g);
        }
        private void SaveCompanyGovernance(SaveData data)
        {EnsureGovernanceCompanies();data.companyGovernanceVersion=1;data.companyGovernance=_companyGovernance.Select(g=>g.Copy()).ToArray();data.companyInvestors=_companyInvestors.Select(i=>i.Copy()).ToArray();}
        private void LoadCompanyGovernance(SaveData data)
        {
            _companyGovernance.Clear();_companyInvestors.Clear();
            if(data.companyGovernanceVersion==0)foreach(var company in _companies)company.CashAccountingFromDay=Clock.Today.TotalDays;
            if(data.companyGovernanceVersion>0)
            {
                foreach(var investor in data.companyInvestors??Array.Empty<CompanyInvestor>())if(investor!=null&&!string.IsNullOrEmpty(investor.Id)&&_companyInvestors.All(i=>i.Id!=investor.Id))_companyInvestors.Add(investor.Copy());
                foreach(var saved in data.companyGovernance??Array.Empty<CompanyGovernance>())
                {
                    if(saved==null||saved.TotalShares<=0||string.IsNullOrEmpty(saved.Ticker)||_companyGovernance.Any(g=>g.Ticker==saved.Ticker))continue;
                    var g=saved.Copy();g.Holders=g.Holders.Where(h=>h.Shares>=0&&!string.IsNullOrEmpty(h.OwnerId)).GroupBy(h=>h.OwnerId).Select(h=>new CompanyShareholder{OwnerId=h.Key,Shares=h.Sum(x=>x.Shares)}).ToArray();
                    long sum=g.Holders.Sum(h=>h.Shares);if(sum!=g.TotalShares)throw new InvalidOperationException("公司股本与股东名册不一致："+g.Ticker);
                    if(ShareholderRatio(g,PlayerShareholderId)<CompanyDirectorThreshold)g.PlayerDirector=false;
                    _companyGovernance.Add(g);if(StockState(g.Ticker) is CityEquityState stock)stock.Units=ShareholderShares(g,PlayerShareholderId);
                }
            }
            EnsureGovernanceCompanies();
        }
        private string GovernanceTradeGate(string ticker,bool opening)
        {return ticker!="hardware"&&opening&&!CompanyGovernanceUnlocked?CompanyGovernanceGate:null;}
        private string PublicShareTradeBlocker(string ticker,long units,bool buy)
        {
            var g=GovernanceOf(ticker);if(g==null)return null;
            if(buy&&units>StockPublicShares(ticker))return "公众可卖股份不足，可向具名股东洽谈大宗交易";
            if(!buy&&PublicInvestor(g).CashCents<CityInvestmentPrice(ticker).Cents*units)return "本次公众买盘资金不足";
            return null;
        }
        private void SettlePublicShareTrade(string ticker,long units,bool buy,long price)
        {
            var g=GovernanceOf(ticker);if(g==null)return;
            PublicInvestor(g).CashCents+=buy?checked(price*units):-checked(price*units);
            MoveShares(g,buy?PublicShareholderId:PlayerShareholderId,buy?PlayerShareholderId:PublicShareholderId,units);
        }
        public Money CompanyBlockPrice(string ticker,long units,bool buy) => CityInvestmentPrice(ticker).Scale(buy?1.10:.95)*Math.Max(0,units);
        public string CompanyBlockTradeBlocker(string ticker,string investorId,long units,bool buy)
        {
            if(CompanyGovernanceGate!=null)return CompanyGovernanceGate;
            if(SecuritiesAccessBlocker!=null)return SecuritiesAccessBlocker;
            if(!StockMarketOpen)return "周末休市";
            var g=GovernanceOf(ticker);var investor=_companyInvestors.Find(i=>i.Id==investorId);
            if(g==null||StockState(ticker)==null||investor==null||investorId.StartsWith("public:",StringComparison.Ordinal))return "请选择具名股东或机构";
            if(units<1||units>g.TotalShares)return "股数超出总股本";
            if(StockState(ticker).ShortUnits>0)return "请先平掉该公司的做空持仓";
            if(ShareholderShares(g,buy?investorId:PlayerShareholderId)<units)return "卖方持股不足";
            var gross=CompanyBlockPrice(ticker,units,buy);var fee=CityInvestments.Fee(gross);
            if(buy&&Ledger.Balance<gross+fee)return "个人现金不足";
            if(!buy&&investor.CashCents<gross.Cents)return "该机构可用资金不足";
            return null;
        }
        public bool TradeCompanyBlock(string ticker,string investorId,long units,bool buy)
        {
            string why=CompanyBlockTradeBlocker(ticker,investorId,units,buy);if(why!=null)return Fail(why);
            var g=GovernanceOf(ticker);var stock=StockState(ticker);var investor=_companyInvestors.Find(i=>i.Id==investorId);
            var gross=CompanyBlockPrice(ticker,units,buy);var fee=CityInvestments.Fee(gross);
            string key="block:"+ticker+":"+Clock.Now.TotalMinutes+":"+_cityAssetEffects.Count;
            if(buy)
            {
                if(!Ledger.TryDebit(Clock.Today,"证券投资",gross+fee,"大宗受让 · "+investor.Name,LedgerAttribution.ForAsset(ticker,key)))return false;
                stock.CostCents+=(gross+fee).Cents;investor.CashCents+=gross.Cents;
            }
            else
            {
                long basis=stock.Units==units?stock.CostCents:(long)((decimal)stock.CostCents*units/stock.Units);
                Ledger.Credit(Clock.Today,"证券赎回",gross-fee,"大宗转让 · "+investor.Name,LedgerAttribution.ForAsset(ticker,key));
                stock.CostCents-=basis;stock.RealizedCents+=(gross-fee).Cents-basis;investor.CashCents-=gross.Cents;
            }
            MoveShares(g,buy?investorId:PlayerShareholderId,buy?PlayerShareholderId:investorId,units);
            _cityAssetEffects.Add(key);RecordStockExecution(ticker,buy?StockExecutionKind.Buy:StockExecutionKind.Sell,Clock.Now.TotalMinutes,gross.Cents/units,units);
            AddStockNews(ticker,Clock.Now.TotalMinutes,"大宗股份交割",ShareholderName(investorId)+(buy?"转让":"受让")+units.ToString("N0")+"股；玩家持股 "+ShareholderRatio(g,PlayerShareholderId).ToString("P2")+"。");
            Say("大宗交易已交割，股份与资金已同步更新。");return true;
        }
        private void CompleteGovernanceIPO(CompanyRecord company,CompanyGovernance g,long price)
        {
            long remaining=CompanyPublicShares;
            foreach(var investor in _companyInvestors.Where(i=>i.Id.StartsWith("capital_",StringComparison.Ordinal)).OrderByDescending(i=>i.CashCents))
            {
                long units=Math.Min(remaining,investor.CashCents/price);if(units<=0)continue;
                long cash=checked(price*units);investor.CashCents-=cash;company.ReserveCents+=cash;
                SetHolding(g,investor.Id,ShareholderShares(g,investor.Id)+units);remaining-=units;if(remaining==0)break;
            }
            if(remaining!=0)throw new InvalidOperationException("发行前认购资金检查失效");
            g.TotalShares+=CompanyPublicShares;StockState(g.Ticker).Units=ShareholderShares(g,PlayerShareholderId);
        }
    }
}
