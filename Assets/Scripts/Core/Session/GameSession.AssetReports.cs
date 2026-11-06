using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class VentureFinanceDay
    {
        public int Day;
        public long SalesCents, VariableCents, WageCents, RentCents;
        public VentureFinanceDay Copy() => (VentureFinanceDay)MemberwiseClone();
    }

    public sealed class AssetStoreReport
    {
        public string Id, Name, StreetId, Kind, State;
        public bool Managed;
        public int FirstDay;
        public long[] IncomeDays, ExpenseDays;
        public readonly Dictionary<string, long> IncomeSources = new Dictionary<string, long>();
        public readonly Dictionary<string, long> ExpenseSources = new Dictionary<string, long>();
        public long IncomeCents => IncomeDays.Sum();
        public long ExpenseCents => ExpenseDays.Sum();
        public void Add(int day, string source, long cents)
        {
            int index = day - FirstDay;
            if (index < 0 || index >= IncomeDays.Length || cents == 0) return;
            var sources = cents > 0 ? IncomeSources : ExpenseSources;
            sources.TryGetValue(source, out long previous);
            sources[source] = previous + Math.Abs(cents);
            if (cents > 0) IncomeDays[index] += cents; else ExpenseDays[index] -= cents;
        }
    }

    public sealed partial class GameSession
    {
        private void RecordVentureFinance(ManagedVenture venture, int day, long sales, long variable, long wages, long rent)
        {
            var row = new VentureFinanceDay { Day = day, SalesCents = sales, VariableCents = variable, WageCents = wages, RentCents = rent };
            venture.FinanceDays = (venture.FinanceDays ?? Array.Empty<VentureFinanceDay>()).Where(d => d.Day != day && d.Day >= day - 30)
                .Concat(new[] { row }).OrderBy(d => d.Day).ToArray();
        }

        public AssetStoreReport[] AssetStoreReports(int days = 7)
        {
            days = Math.Max(1, Math.Min(30, days));
            int first = Math.Max(0, Clock.Today.TotalDays - days + 1), count = Clock.Today.TotalDays - first + 1;
            var reports = new Dictionary<string, AssetStoreReport>();
            AssetStoreReport Create(string id, string name, string street, string kind, string state, bool managed) =>
                new AssetStoreReport { Id = id, Name = name, StreetId = street, Kind = kind, State = state, Managed = managed,
                    FirstDay = first, IncomeDays = new long[count], ExpenseDays = new long[count] };
            foreach (var shop in MyShops.Where(s => s.Rented))
                reports[shop.Place.Id] = Create(shop.Place.Id, ShopTitle(shop), shop.Place.StreetId,
                    RetailShopTypes.Name(shop.RetailKind), (shop.Sim.IsOpen ? "营业中" : "休息 / 暂停") + (shop.OwnsProperty ? " · 自有" : " · 租赁"), false);
            foreach (var v in _ventures)
            {
                var report = Create(v.PlaceId, Ventures.Get(v.Kind).Name + " · " + VentureLocationName(v), VentureLocation(v).StreetId,
                    Ventures.Get(v.Kind).Name, (v.Paused ? "暂停营业" : "营业中") + " · 店长托管", true);
                reports[v.PlaceId] = report;
                foreach (var day in v.FinanceDays ?? Array.Empty<VentureFinanceDay>())
                {
                    report.Add(day.Day, "门店营业收入", day.SalesCents);
                    report.Add(day.Day, "商品与食材成本", -day.VariableCents);
                    report.Add(day.Day, "员工工资", -day.WageCents);
                    report.Add(day.Day, "店铺租金", -day.RentCents);
                }
            }
            foreach (var entry in Ledger.Entries)
            {
                if (entry.Date.TotalDays < first || entry.Date > Clock.Today) continue;
                string id = entry.ShopId;
                if (id == null && entry.Category == "物业维护") id = entry.Attribution?.PropertyId;
                if (id == null || !reports.TryGetValue(id, out var report)) continue;
                bool ownedMaintenance = entry.Category == "物业维护" && report.Id == entry.Attribution?.PropertyId;
                if (entry.BusinessKind != LedgerBusinessKind.Operating && !ownedMaintenance) continue;
                // A headquarters top-up is already included in the managed store's full daily costs.
                if (report.Managed && (entry.Category == "企业固定费" || entry.Category == "车行固定费")) continue;
                report.Add(entry.Date.TotalDays, AssetIncomeSource(entry.Category), entry.Amount.Cents);
            }
            return reports.Values.OrderBy(r => City.StreetName(r.StreetId), StringComparer.Ordinal).ThenBy(r => r.Kind, StringComparer.Ordinal)
                .ThenBy(r => r.Name, StringComparer.Ordinal).ToArray();
        }

        private static string AssetIncomeSource(string category)
        {
            switch (category)
            {
                case "零售": return "商品销售";
                case "装机": return "装机交付款";
                case "进货": case "偿还货款": return "商品进货付款";
                case "工资": return "员工工资";
                case "房租": return "店铺租金";
                case "运费": return "采购与配送运费";
                default: return category;
            }
        }
    }
}
