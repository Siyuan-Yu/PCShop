using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public static string CompanyResolutionName(CompanyResolutionKind kind) => kind==CompanyResolutionKind.Director?"董事提名":kind==CompanyResolutionKind.Manager?"任免总经理":kind==CompanyResolutionKind.Budget?"经营预算":kind==CompanyResolutionKind.Expansion?"经营扩张":kind==CompanyResolutionKind.Promotion?"推广投入":kind==CompanyResolutionKind.Dividend?"现金分红":"增发股份";
        public bool CompanyBoardMember(CompanyGovernance g) => g!=null&&(g.PlayerDirector&&ShareholderRatio(g,PlayerShareholderId)>=CompanyDirectorThreshold||MajorityShareholder(g)==PlayerShareholderId);
        public string CompanyMeetingBlocker(CompanyGovernance g) => CompanyGovernanceGate??(g==null?"没有这家公司":ShareholderRatio(g,PlayerShareholderId)<CompanyMeetingThreshold?"持股达到 1% 后可参加股东会议":null);
        public string CompanyBoardBlocker(CompanyGovernance g) => CompanyMeetingBlocker(g)??(!CompanyBoardMember(g)?"请先通过董事提名，或取得过半表决权":g.HandoverAt>=0?"请先完成控制权交接":null);
        public string CompanyNominationBlocker(CompanyGovernance g) => CompanyMeetingBlocker(g)??(CompanyBoardMember(g)?"已经拥有董事权限":ShareholderRatio(g,PlayerShareholderId)<CompanyDirectorThreshold?"持股达到 5% 后可申请董事席位":g.Trust<60?"公司信任须达到 60":g.Resolutions.Any(r=>!r.Closed&&r.Kind==CompanyResolutionKind.Director)?"已有待表决提名":null);
        public long GovernanceAvailableDistribution(CompanyGovernance g)
        {
            var own=GovernanceOwnedCompany(g);long reserve=own!=null?CompanyCashRequirement(own).Cents:Math.Max(0,(StockState(g.Ticker)?.YesterdayRevenueCents??0)*7);
            long earnings=own!=null?Math.Max(0,CompanyLastMonth(own).Revenue.Cents-CompanyLastMonth(own).Costs.Cents):Math.Max(0,StockState(g.Ticker)?.QuarterProfitCents??0);
            if(own!=null&&g.DistributedProfitMonth==Clock.Today.TotalDays/GameDate.DaysPerMonth)earnings=Math.Max(0,earnings-g.DistributedProfitCents);
            return Math.Max(0,Math.Min(earnings,GovernanceCash(g).Cents-reserve));
        }
        public string CompanyResolutionBlocker(string ticker,CompanyResolutionKind kind,long value,string candidate=null)
        {
            var g=GovernanceOf(ticker);string why=kind==CompanyResolutionKind.Director?CompanyNominationBlocker(g):CompanyBoardBlocker(g);if(why!=null)return why;
            if(g.Resolutions.Any(r=>!r.Closed))return "请先完成当前议案";
            if(kind==CompanyResolutionKind.Director)return null;
            if(kind==CompanyResolutionKind.Manager)return candidate==PlayerShareholderId||_companyInvestors.Any(i=>i.Id==candidate&&!i.Id.StartsWith("public:",StringComparison.Ordinal))?null:"请选择总经理";
            if(value<=0||value>100000000000L)return "请输入有效额度";
            if(kind==CompanyResolutionKind.Budget)return value<10000||value>1000000000?"每日自主开支预算须为 ¥100—¥10,000,000":null;
            if(kind==CompanyResolutionKind.Issue)
            {
                if(StockState(ticker)==null)return "公司须先挂牌";
                if(value>g.TotalShares/2)return "单次增发不超过当前股本的 50%";
                long cost=checked(CityInvestmentPrice(ticker).Cents*value);
                long cash=candidate==PlayerShareholderId?Ledger.Balance.Cents:_companyInvestors.Find(i=>i.Id==candidate)?.CashCents??-1;
                return cash<cost?"认购人资金不足":null;
            }
            if(kind==CompanyResolutionKind.Dividend)
                return value/g.TotalShares<1?"总额不足每股分红 1 分":value/g.TotalShares>CityInvestmentPrice(ticker).Cents-100?"当前股价不足以完成本次除息":value>GovernanceAvailableDistribution(g)?"分红超出可分配利润或周转资金余量":null;
            if(GovernanceCash(g).Cents<value)return "公司现金不足";
            if(value>g.DailyBudgetCents*7)return "投入超过七天经营预算，请先调整预算";
            if(kind==CompanyResolutionKind.Expansion&&GovernanceOwnedCompany(g) is CompanyRecord own)
            {string transfer=CompanyStoreTransferBlocker(own,ShopAt(candidate));if(transfer!=null)return transfer;if(CompanyStoreTransferPrice(ShopAt(candidate)).Cents!=value)return "请重新核对门店受让报价";}
            return null;
        }
        public bool ProposeCompanyResolution(string ticker,CompanyResolutionKind kind,long value,string candidate=null)
        {
            string why=CompanyResolutionBlocker(ticker,kind,value,candidate);if(why!=null)return Fail(why);
            var g=GovernanceOf(ticker);var resolution=new CompanyResolution{Id=g.NextResolutionId++,Kind=kind,Value=value,CandidateId=candidate,
                CreatedAt=Clock.Now.TotalMinutes,DueAt=Clock.Now.TotalMinutes+1440,PriceCents=CityInvestmentPrice(ticker).Cents,PlayerVote=CompanyVote.Support};
            g.Resolutions=g.Resolutions.Concat(new[]{resolution}).Reverse().Take(12).Reverse().ToArray();
            AddStockNews(ticker,Clock.Now.TotalMinutes,"股东会议案："+CompanyResolutionName(kind),"下一游戏日同一时间按实际持股表决，过半支持才通过。");
            Say("议案已提交，表决前仍可调整你的投票。");return true;
        }
        public bool VoteCompanyResolution(string ticker,int id,CompanyVote vote)
        {
            var g=GovernanceOf(ticker);string why=CompanyMeetingBlocker(g);if(why!=null)return Fail(why);
            var motion=g.Resolutions.FirstOrDefault(r=>r.Id==id&&!r.Closed);if(motion==null||Clock.Now.TotalMinutes>=motion.DueAt)return Fail("该议案已结束");
            motion.PlayerVote=vote;Say("已记录投票，结算时以实际持股计票。");return true;
        }
        private CompanyVote InvestorVote(CompanyGovernance g,CompanyResolution motion,CompanyShareholder holder)
        {
            if(holder.OwnerId==PlayerShareholderId)return motion.PlayerVote;
            if(motion.Kind==CompanyResolutionKind.Director)return g.Trust>=60?CompanyVote.Support:CompanyVote.Oppose;
            if(motion.Kind==CompanyResolutionKind.Issue)
            {
                bool useful=GovernanceCash(g).Cents<g.DailyBudgetCents*30;
                if(holder.OwnerId==motion.CandidateId)return CompanyVote.Support;
                return useful&&g.Trust>=65?CompanyVote.Support:CompanyVote.Oppose;
            }
            if(motion.Kind==CompanyResolutionKind.Dividend)return motion.Value<=GovernanceAvailableDistribution(g)?CompanyVote.Support:CompanyVote.Oppose;
            if(motion.Kind==CompanyResolutionKind.Manager&&motion.CandidateId==PlayerShareholderId)return g.Trust>=60?CompanyVote.Support:CompanyVote.Oppose;
            return g.Trust>=50?CompanyVote.Support:CompanyVote.Oppose;
        }
        public (long support,long oppose) CompanyVotePreview(CompanyGovernance g,CompanyResolution motion)
        {
            long yes=0,no=0;foreach(var holder in g.Holders){var vote=InvestorVote(g,motion,holder);if(vote==CompanyVote.Support)yes+=holder.Shares;else if(vote==CompanyVote.Oppose)no+=holder.Shares;}return(yes,no);
        }
        private void ResolveCompanyMotions(CompanyGovernance g,long now)
        {
            foreach(var motion in g.Resolutions.Where(r=>!r.Closed&&r.DueAt<=now))
            {
                var votes=CompanyVotePreview(g,motion);motion.VotingShares=g.TotalShares;motion.VotesFor=votes.support;motion.VotesAgainst=votes.oppose;motion.Closed=true;
                motion.Passed=motion.VotesFor>g.TotalShares/2;
                motion.Result=motion.Passed?ApplyCompanyResolution(g,motion):"支持票未超过总股本 50%，议案未通过";
                if(motion.Result!=null&&motion.Passed){motion.Passed=false;motion.Result="通过后未执行："+motion.Result;}
                if(motion.Passed)motion.Result="已通过并执行";
                AddStockNews(g.Ticker,now,CompanyResolutionName(motion.Kind)+(motion.Passed?"通过":"结束"),motion.Result+"；支持 "+motion.VotesFor.ToString("N0")+" 股，反对 "+motion.VotesAgainst.ToString("N0")+" 股。");
                if(ShareholderShares(g,PlayerShareholderId)>0)Say(GovernanceName(g.Ticker)+"："+CompanyResolutionName(motion.Kind)+"，"+motion.Result+"。");
            }
        }
        private string ApplyCompanyResolution(CompanyGovernance g,CompanyResolution motion)
        {
            switch(motion.Kind)
            {
                case CompanyResolutionKind.Director:
                    if(ShareholderRatio(g,PlayerShareholderId)<CompanyDirectorThreshold||g.Trust<60)return "候选人持股或信任已不满足条件";
                    g.PlayerDirector=true;break;
                case CompanyResolutionKind.Manager:g.ManagerId=motion.CandidateId;g.RetainedShopIds=Array.Empty<string>();break;
                case CompanyResolutionKind.Budget:g.DailyBudgetCents=motion.Value;break;
                case CompanyResolutionKind.Issue:return IssueCompanyShares(g,motion);
                case CompanyResolutionKind.Dividend:
                    if(motion.Value>GovernanceAvailableDistribution(g))return "当前可分配现金不足";
                    if(motion.Value/g.TotalShares>CityInvestmentPrice(g.Ticker).Cents-100)return "当前股价不足以完成除息";
                    PayCompanyDividend(g,motion.Value/g.TotalShares,Clock.Today.TotalDays,"resolution:"+motion.Id);break;
                case CompanyResolutionKind.Expansion:
                case CompanyResolutionKind.Promotion:
                    if(motion.Kind==CompanyResolutionKind.Expansion&&GovernanceOwnedCompany(g)!=null)return TransferStoreIntoCompany(g,motion.CandidateId,motion.Value);
                    if(GovernanceCash(g).Cents<motion.Value)return "公司现金已不足";
                    ChangeGovernanceCash(g,-motion.Value);g.Trust=Math.Min(100,g.Trust+2);
                    if(motion.Kind==CompanyResolutionKind.Expansion)
                    {var state=StockState(g.Ticker);state.BusinessScale=Math.Min(3,state.BusinessScale+Math.Min(.12,motion.Value/(double)Math.Max(1,state.YesterdayRevenueCents*30)));}
                    else g.PromotionUntilDay=Clock.Today.TotalDays+30;
                    RecordGovernanceExpense(g,motion.Value,CompanyResolutionName(motion.Kind));break;
            }
            return null;
        }
        private string IssueCompanyShares(CompanyGovernance g,CompanyResolution motion)
        {
            long cash=checked(motion.PriceCents*motion.Value);var investor=_companyInvestors.Find(i=>i.Id==motion.CandidateId);
            if(motion.Value<=0||motion.Value>g.TotalShares/2)return "增发股数已不符合上限";
            if(motion.CandidateId==PlayerShareholderId)
            {
                if(Ledger.Balance.Cents<cash)return "个人认购资金不足";
                if(!Ledger.TryDebit(Clock.Today,"证券投资",Money.FromCents(cash),"认购增发",LedgerAttribution.ForAsset(g.Ticker,"issue:"+g.Ticker+":"+motion.Id)))return "认购失败";
                StockState(g.Ticker).CostCents+=cash;
            }
            else{if(investor==null||investor.CashCents<cash)return "机构认购资金不足";investor.CashCents-=cash;}
            ChangeGovernanceCash(g,cash);g.TotalShares=checked(g.TotalShares+motion.Value);SetHolding(g,motion.CandidateId,checked(ShareholderShares(g,motion.CandidateId)+motion.Value));
            StockState(g.Ticker).Units=ShareholderShares(g,PlayerShareholderId);
            if(ShareholderRatio(g,PlayerShareholderId)<CompanyDirectorThreshold)g.PlayerDirector=false;
            ObserveCompanyControl(g);
            AddStockNews(g.Ticker,Clock.Now.TotalMinutes,"增发交割",motion.Value.ToString("N0")+" 股已发行，认购款全部进入公司；新股本 "+g.TotalShares.ToString("N0")+" 股。");return null;
        }
        private void RecordGovernanceExpense(CompanyGovernance g,long cents,string label)
        {
            var company=GovernanceOwnedCompany(g);
            if(company!=null)company.Expenses=company.Expenses.Concat(new[]{new CompanyExpense{Day=Clock.Today.TotalDays,Cents=cents,Label=label}}).ToArray();
            Ledger.RecordNonCash(Clock.Today,"公司议案支出",label+" · "+Money.FromCents(cents),new LedgerAttribution(LedgerBusinessKind.InternalTransfer,activityId:company?.BrandId??g.Ticker,sourceEventId:"governance:expense:"+g.Ticker+":"+g.NextResolutionId+":"+Clock.Now.TotalMinutes));
        }
        private void PayCompanyDividend(CompanyGovernance g,long perShare,int day,string eventKey=null)
        {
            eventKey??="quarter:"+day;string dividendKey="governance:dividend:"+g.Ticker+":"+eventKey;
            if(g.LastDividendKey==eventKey||_cityAssetEffects.Contains(dividendKey))return;
            var stock=StockState(g.Ticker);if(perShare<=0||stock==null)return;
            perShare=Math.Min(perShare,Math.Max(0,stock.PriceCents-100));if(perShare<=0)return;
            long cost=checked(perShare*g.TotalShares);if(GovernanceCash(g).Cents<cost)return;
            g.LastDividendKey=eventKey;_cityAssetEffects.Add(dividendKey);ChangeGovernanceCash(g,-cost);
            if(GovernanceOwnedCompany(g)!=null)
            {int month=Clock.Today.TotalDays/GameDate.DaysPerMonth;if(g.DistributedProfitMonth!=month){g.DistributedProfitMonth=month;g.DistributedProfitCents=0;}g.DistributedProfitCents+=cost;}
            foreach(var holder in g.Holders.Where(h=>h.Shares>0))
            {
                long cash=checked(holder.Shares*perShare);
                if(holder.OwnerId==PlayerShareholderId)
                {Ledger.Credit(new GameDate(day),"股票分红",Money.FromCents(cash),GovernanceName(g.Ticker),LedgerAttribution.ForAsset(g.Ticker,"governance:dividend:"+g.Ticker+":"+eventKey));stock.RealizedCents+=cash;}
                else if(holder.OwnerId==PublicShareholderId)PublicInvestor(g).CashCents+=cash;
                else Investor(holder.OwnerId).CashCents+=cash;
            }
            stock.LastDividendCents=perShare;g.LastDividendPerShare=perShare;g.LastDividendDay=day;stock.QuarterProfitCents=Math.Max(0,stock.QuarterProfitCents-cost);
            stock.PriceCents=Math.Max(100,stock.PriceCents-perShare);stock.PreviousCloseCents=stock.PriceCents;
            stock.Quotes=AppendStockQuote(stock.Quotes,Clock.Now.TotalMinutes,stock.PriceCents);stock.History=stock.Quotes.Select(q=>q.PriceCents).ToArray();
            if(stock.ShortUnits>0)ChargeStockShortCost(stock,perShare*stock.ShortUnits,"分红补偿",Clock.Now.TotalMinutes);
        }
    }
}
