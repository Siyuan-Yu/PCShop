using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public sealed class ExpoFinanceReport
    {
        public Money BoothPaid { get; internal set; }
        public Money PosterPaid { get; internal set; }
        public bool FeesRecorded { get; internal set; }
        public Money Revenue { get; internal set; }
        public Money WagesPaid { get; internal set; }
        public Money CashNet => Revenue - BoothPaid - PosterPaid - WagesPaid;
        private readonly Dictionary<string, Money> _productSales = new Dictionary<string, Money>();
        public IReadOnlyDictionary<string, Money> ProductSales => _productSales;
        internal void AddProductSale(string id, Money amount)
        {
            if (id == null) return;
            _productSales.TryGetValue(id, out var previous);
            _productSales[id] = previous + amount;
        }
    }

    public sealed partial class GameSession
    {
        /// <summary>Historical settlement uses amounts paid, never the current price catalogue.</summary>
        public ExpoFinanceReport GetExpoFinance(string exhibitId)
        {
            var exhibit = Array.Find(_brands.Exhibits, e => e.Id == exhibitId);
            if (exhibit == null) return null;
            var result = new ExpoFinanceReport {
                Revenue = Money.FromCents(exhibit.RevenueCents),
                WagesPaid = Money.FromCents(exhibit.ExpoWagesCents)
            };
            bool boothRecorded = false, posterRecorded = exhibit.DesignerId == null;
            string salesPrefix = exhibitId + ":sale:";
            foreach (var entry in Ledger.Entries)
            {
                if (entry.SourceEventId == exhibitId + ":booth")
                { result.BoothPaid += -entry.Amount; boothRecorded = true; }
                else if (entry.SourceEventId == exhibitId + ":poster")
                { result.PosterPaid += -entry.Amount; posterRecorded = true; }
                else if (entry.SourceEventId?.StartsWith(salesPrefix, StringComparison.Ordinal) == true)
                    result.AddProductSale(entry.Attribution?.ProductId, entry.Amount);
            }
            result.FeesRecorded = boothRecorded && posterRecorded;
            return result;
        }
    }
}
