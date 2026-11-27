using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public enum CompanyResolutionKind { Director, Manager, Budget, Expansion, Promotion, Dividend, Issue }
    public enum CompanyVote { Abstain, Support, Oppose }

    [Serializable] public sealed class CompanyShareholder
    {
        public string OwnerId;
        public long Shares;
        public CompanyShareholder Copy() => (CompanyShareholder)MemberwiseClone();
    }
    [Serializable] public sealed class CompanyInvestor
    {
        public string Id, Name, PersonId;
        public long CashCents;
        public CompanyInvestor Copy() => (CompanyInvestor)MemberwiseClone();
    }
    [Serializable] public sealed class CompanyResolution
    {
        public int Id;
        public CompanyResolutionKind Kind;
        public long CreatedAt, DueAt, Value, PriceCents, VotesFor, VotesAgainst, VotingShares;
        public string CandidateId, Result;
        public CompanyVote PlayerVote;
        public bool Closed, Passed;
        public CompanyResolution Copy() => (CompanyResolution)MemberwiseClone();
    }
    [Serializable] public sealed class CompanyGovernance
    {
        public string Ticker, ControllerId, ManagerId, PendingControllerId, PendingManagerId;
        public long TotalShares, CashCents, DailyBudgetCents = 1000000, HandoverAt = -1;
        public long LastDividendPerShare;
        public string LastDividendKey;
        public int DistributedProfitMonth = -1;
        public long DistributedProfitCents;
        public int Trust = 60, NextResolutionId = 1, LastNpcTradeDay = -1, LastOperatingDay = -1;
        public int LastDividendDay = -1, PromotionUntilDay = -1, LastRetentionPayDay = -1;
        public bool PlayerDirector, RetentionAccepted;
        public string RetentionOfferShopId;
        public string[] RetainedShopIds = Array.Empty<string>();
        public CompanyShareholder[] Holders = Array.Empty<CompanyShareholder>();
        public CompanyResolution[] Resolutions = Array.Empty<CompanyResolution>();
        public CompanyGovernance Copy()
        {
            var c=(CompanyGovernance)MemberwiseClone();
            c.Holders=(Holders??Array.Empty<CompanyShareholder>()).Select(x=>x.Copy()).ToArray();
            c.Resolutions=(Resolutions??Array.Empty<CompanyResolution>()).Select(x=>x.Copy()).ToArray();
            c.RetainedShopIds=(RetainedShopIds??Array.Empty<string>()).ToArray(); return c;
        }
    }
    public sealed partial class SaveData
    {
        public int companyGovernanceVersion;
        public CompanyGovernance[] companyGovernance;
        public CompanyInvestor[] companyInvestors;
    }
}
