using System;
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
    [Serializable] public sealed class StockMarketState
    {
        public int Version=1;
        public long LastMinute=-1, IndexPriceCents=10000, IndexPreviousCloseCents=10000, IndexAvailableUnits=40000;
        public bool IndexWatched;
        public StockQuote[] IndexQuotes=Array.Empty<StockQuote>();
        public StockNews[] News=Array.Empty<StockNews>();
        public StockMarketState Copy() => new StockMarketState {Version=Version,LastMinute=LastMinute,IndexPriceCents=IndexPriceCents,
            IndexPreviousCloseCents=IndexPreviousCloseCents,IndexAvailableUnits=IndexAvailableUnits,IndexWatched=IndexWatched,
            IndexQuotes=Array.ConvertAll(IndexQuotes??Array.Empty<StockQuote>(),q=>q.Copy()),News=Array.ConvertAll(News??Array.Empty<StockNews>(),n=>n.Copy())};
    }
}
