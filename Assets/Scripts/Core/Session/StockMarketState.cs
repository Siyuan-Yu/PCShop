using System;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class StockQuote
    {
        public long Minute, PriceCents;
        public StockQuote Copy() => (StockQuote)MemberwiseClone();
    }
    [Serializable] public sealed class StockNews
    {
        public string StockId, Headline, Detail;
        public long Minute;
        public StockNews Copy() => (StockNews)MemberwiseClone();
    }
    public enum StockExecutionKind { Buy, Sell, Short, Cover, ForcedCover }

    [Serializable] public struct StockExecution
    {
        public string StockId;
        public StockExecutionKind Kind;
        public long Minute, PriceCents, Units;
    }

    [Serializable] public sealed class StockMarketState
    {
        public const int ExecutionHistoryVersion = 2;
        public int Version = ExecutionHistoryVersion;
        public int CompanyEventSeed;
        public long LastMinute=-1, IndexPriceCents=10000, IndexPreviousCloseCents=10000, IndexAvailableUnits=40000;
        public bool IndexWatched;
        public StockQuote[] IndexQuotes=Array.Empty<StockQuote>();
        public StockNews[] News=Array.Empty<StockNews>();
        public List<StockExecution> Executions = new List<StockExecution>();
        public StockMarketState Copy() => new StockMarketState {Version=Version,LastMinute=LastMinute,IndexPriceCents=IndexPriceCents,
            IndexPreviousCloseCents=IndexPreviousCloseCents,IndexAvailableUnits=IndexAvailableUnits,IndexWatched=IndexWatched,CompanyEventSeed=CompanyEventSeed,
            IndexQuotes=Array.ConvertAll(IndexQuotes??Array.Empty<StockQuote>(),q=>q.Copy()),News=Array.ConvertAll(News??Array.Empty<StockNews>(),n=>n.Copy()),
            Executions = Executions == null ? new List<StockExecution>() : new List<StockExecution>(Executions) };
    }
}
