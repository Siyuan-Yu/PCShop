using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum PurchaseGoodsKind { Part, Gift, Rice, Ingredient, Furniture }

    public sealed class PurchaseOffer
    {
        public Place Source;
        public PurchaseGoodsKind Kind;
        public string Id, Name, Category;
        public Money Price;
        public double UnitKg, StockSpace;

        /// <summary>同一件商品可以同时属于多个取值，用这个字符连接（例如一件家具适用多种店型）。</summary>
        public const char FacetSeparator = '|';

        /// <summary>各筛选维度的取值。键见 <see cref="PurchaseFacets"/>，界面按维度自动生成多级筛选。</summary>
        public readonly Dictionary<string, string> Facets = new Dictionary<string, string>();

        public string SupplierName => Source.Name;
        public string Key => GameSession.PurchaseKey(Kind, Id, Source.Id);
        public PurchaseLine Line(int count) => new PurchaseLine { Kind = Kind, PartId = Id, SourceId = Source.Id, Count = count };

        /// <summary>取某个维度的值；这件商品没有这个维度时返回 null。</summary>
        public string Facet(string key) => key != null && Facets.TryGetValue(key, out string value) ? value : null;

        /// <summary>该维度是否包含指定取值。</summary>
        public bool HasFacet(string key, string value) =>
            value != null && Facet(key)?.Split(FacetSeparator).Contains(value) == true;

        /// <summary>按一组筛选条件判断是否命中；取值为 null、空或「全部」表示该维度不限制。</summary>
        public bool Matches(IReadOnlyDictionary<string, string> filters)
        {
            if (filters == null) return true;
            foreach (var pair in filters)
            {
                if (string.IsNullOrEmpty(pair.Value) || pair.Value == PurchaseFacets.All) continue;
                if (!HasFacet(pair.Key, pair.Value)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 进货员采购目录的筛选维度。为商品填充该维度的取值后，采购界面会按登记顺序生成筛选。
    /// </summary>
    public static class PurchaseFacets
    {
        /// <summary>表示该维度不限制。</summary>
        public const string All = "全部";
        public const string Category = "category";
        public const string Shop = "shop";
        public const string Season = "season";

        /// <summary>维度顺序：越靠前越先筛，界面也按这个顺序排列。</summary>
        public static readonly string[] Order = { Category, Shop, Season };

        public static string Label(string key) =>
            key == Shop ? "适用店铺" : key == Season ? "季节" : "分类";
    }

    public sealed partial class GameSession
    {
        public static string PurchaseKey(PurchaseGoodsKind kind, string id, string source) => ((int)kind) + "|" + id + "|" + source;
        public static string PurchaseKey(PurchaseLine line) => PurchaseKey(line.Kind, line.PartId, line.SourceId);

        public Place PurchaseDestination(Place source) => source?.IsElectronicsCounter == true
            ? WholesaleStallsAt(source).FirstOrDefault(stall => !stall.IsElectronicsCounter) ?? source
            : source;

        private string PurchaseDestinationId(string sourceId) => City.HasPlace(sourceId)
            ? PurchaseDestination(City.GetPlace(sourceId)).Id : null;

        public IReadOnlyList<PurchaseOffer> EmployeePurchaseCatalog()
        {
            var offers = new List<PurchaseOffer>();
            // 统一入口：分类是每个商品必有的维度，季节与适用店铺按需附加。
            void Add(PurchaseOffer offer, string category, string season = null, string[] shops = null)
            {
                offer.Category = offer.Facets[PurchaseFacets.Category] = category;
                if (season != null) offer.Facets[PurchaseFacets.Season] = season;
                if (shops != null && shops.Length > 0)
                    offer.Facets[PurchaseFacets.Shop] = string.Join(PurchaseOffer.FacetSeparator.ToString(), shops);
                offers.Add(offer);
            }
            foreach (var source in City.Places)
            {
                if (source.Kind == PlaceKind.Wholesaler && PurchaseDestination(source) == source)
                    foreach (var p in PartCatalog.All.Where(p => StallSells(source, p)))
                        Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Part, Id = p.Id, Name = p.Name,
                            Price = StallPrice(source, p),
                            UnitKg = Stock.SpaceOf(p, true, 1) * Backpack.KgPerSpace, StockSpace = Stock.SpaceOf(p, true, 1) },
                            PartCatalog.CategoryName(p.Category));
                if (ShopAt(source) != null || IsVacant(source) || VentureAt(source.Id) != null) continue;
                foreach (var gift in Gifts.All.Where(g => g.SoldAt == source.Business && CityRetailGoodsAvailable(source, g)))
                    Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Gift, Id = gift.Id, Name = gift.Name + (UsesWholesalePackMinimum(source) ? "（批发）" : ""),
                        Price = CityRetailWholesalePrice(source, gift), UnitKg = gift.WeightKg, StockSpace = Stock.GoodsSpace(gift.Id, 1) },
                        ShelfKinds.ForGift(gift.Id)?.Name ?? "其他商品", ClothingProducts.SeasonGroup(gift.Id));
                if (source.Business == MallBusiness)
                    foreach (var furniture in FurnitureCatalog.All.Where(f => !f.Starter))
                        Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Furniture, Id = furniture.Id, Name = furniture.Name,
                            Price = furniture.Price, UnitKg = furniture.WeightKg }, "家具与货架", null, FurnitureShops.For(furniture));
            }
            return offers.OrderBy(o => (int)o.Kind).ToArray();
        }

        public PurchaseOffer EmployeePurchaseOffer(PurchaseLine line, string legacySource = null, IReadOnlyList<PurchaseOffer> catalog = null)
        {
            if (line == null) return null;
            string source = line.SourceId ?? legacySource;
            if (line.Kind == PurchaseGoodsKind.Part)
                source = PurchaseDestinationId(source) ?? source;
            return (catalog ?? EmployeePurchaseCatalog()).Where(o => o.Kind == line.Kind && o.Id == line.PartId && (string.IsNullOrEmpty(source) || o.Source.Id == source))
                .OrderBy(o => o.Price.Cents).FirstOrDefault();
        }

        private bool IsFormerFarmRetailLine(PurchaseLine line, string sourceId) =>
            (line.Kind == PurchaseGoodsKind.Rice || line.Kind == PurchaseGoodsKind.Ingredient)
            && (string.IsNullOrEmpty(sourceId) || City.HasPlace(sourceId) && City.GetPlace(sourceId).Business == FarmBusiness);

        // Bind old sources and combine entries that now refer to the same offer.
        public void PrepareEmployeePurchaseDraft(EmployeePurchasePlan plan, IReadOnlyList<PurchaseOffer> catalog = null)
        {
            catalog ??= EmployeePurchaseCatalog();
            var items = new List<PurchaseLine>();
            var combinedOffers = new Dictionary<string, PurchaseLine>();
            foreach (var line in plan.Items ?? Array.Empty<PurchaseLine>())
            {
                if (line == null) continue;
                var copy = line.Copy();
                if (IsFormerFarmRetailLine(copy, copy.SourceId ?? plan.SourceId))
                {
                    copy.PartId = "produce:" + copy.PartId;
                    copy.Kind = PurchaseGoodsKind.Gift;
                    copy.ReservedByWeekday = null;
                }
                var offer = EmployeePurchaseOffer(copy, plan.SourceId, catalog);
                if (offer != null) copy.SourceId = offer.Source.Id;
                bool canCombine = offer != null && (offer.Kind == PurchaseGoodsKind.Part || offer.Source.Business == FarmBusiness);
                if (canCombine && combinedOffers.TryGetValue(offer.Key, out var combined))
                {
                    combined.Count += copy.Count;
                    combined.BuyMaximum |= copy.BuyMaximum;
                    combined.ReservedByWeekday = null;
                    continue;
                }
                if (canCombine) combinedOffers.Add(offer.Key, copy);
                items.Add(copy);
            }
            plan.Items = items.ToArray();
            plan.SourceId = null;
        }

        public string EmployeePurchaseBrowseSource(EmployeePurchasePlan plan, IReadOnlyList<PurchaseOffer> catalog = null)
        {
            if(plan==null)return null;
            string source=plan.BrowseSourceId;
            if(!plan.BrowseSourceSelected)
            {
                source=plan.SourceId;
                if(string.IsNullOrEmpty(source))
                {
                    var sources=(plan.Items??Array.Empty<PurchaseLine>()).Where(x=>x!=null && !string.IsNullOrEmpty(x.SourceId))
                        .Select(x=>PurchaseDestinationId(x.SourceId) ?? x.SourceId).Distinct().ToArray();
                    source=sources.Length==1?sources[0]:null;
                }
            }
            source = PurchaseDestinationId(source);
            return source != null && (catalog ?? EmployeePurchaseCatalog()).Any(o => PurchaseDestination(o.Source).Id == source) ? source : null;
        }

        public bool RememberEmployeePurchaseSource(string employeeId, string sourceId)
        {
            var worker=FindEmployee(employeeId);
            if(worker==null || worker.Role!=EmployeeRole.Buyer)return false;
            string destinationId = PurchaseDestinationId(sourceId);
            if (!string.IsNullOrEmpty(sourceId) && (destinationId == null || !EmployeePurchaseCatalog().Any(o => PurchaseDestination(o.Source).Id == destinationId))) return false;
            worker.Person.PurchasePlan.BrowseSourceId = destinationId;
            worker.Person.PurchasePlan.BrowseSourceSelected = true;
            return true;
        }

        public static string PurchaseItemName(PurchaseLine line)
        {
            switch (line.Kind)
            {
                case PurchaseGoodsKind.Gift: return Gifts.Get(line.PartId)?.Name ?? line.PartId;
                case PurchaseGoodsKind.Rice: return "大米（0.5千克/袋）";
                case PurchaseGoodsKind.Ingredient: return Ingredients.Get(line.PartId)?.Name ?? line.PartId;
                case PurchaseGoodsKind.Furniture: return FurnitureCatalog.Get(line.PartId)?.Name ?? line.PartId;
                default: return PartCatalog.Exists(line.PartId) ? PartCatalog.Get(line.PartId).Name : line.PartId;
            }
        }

        public static double PurchaseStockSpace(PurchaseLine line) =>
            line.Kind == PurchaseGoodsKind.Part && PartCatalog.Exists(line.PartId) ? Stock.SpaceOf(PartCatalog.Get(line.PartId), true, line.Count)
            : line.Kind == PurchaseGoodsKind.Gift ? Stock.GoodsSpace(line.PartId, line.Count) : 0;

        public Money PurchaseInventoryValue(PurchaseLine line) =>
            line.Kind == PurchaseGoodsKind.Part && PartCatalog.Exists(line.PartId) ? Market.PriceOf(PartCatalog.Get(line.PartId)) * line.Count
            : line.Kind == PurchaseGoodsKind.Gift && Gifts.Get(line.PartId) != null ? Gifts.Get(line.PartId).Price * line.Count : Money.Zero;

        private CourierItem PurchaseCourierItem(PurchaseLine line) => new CourierItem
        {
            Kind = line.Kind == PurchaseGoodsKind.Part ? CourierItemKind.Part : line.Kind == PurchaseGoodsKind.Gift ? CourierItemKind.Goods
                : line.Kind == PurchaseGoodsKind.Rice ? CourierItemKind.Rice : line.Kind == PurchaseGoodsKind.Ingredient ? CourierItemKind.Ingredient : CourierItemKind.Furniture,
            Id = line.PartId, Count = line.Count, Kg = line.Count * 0.5, Boxed = true, ExpireDay = line.ExpireDay
        };

        private string PurchaseOpeningBlocker(Place source, int arrivalMinute)
        {
            if (SupplierClosesOnWeekend(source))
            {
                CityVenueHours(source, out int supplierOpen, out int supplierClose);
                if (Clock.Today.IsWeekend || arrivalMinute < supplierOpen || arrivalMinute >= supplierClose)
                    return $"{source.Name}到达时未营业（{OpeningHoursText(source)}）";
                return null;
            }
            int open = StreetShopOpenMinute, close = StreetShopCloseMinute;
            if (source.Business == FarmBusiness) CityVenueHours(source, out open, out close);
            if (arrivalMinute < open || arrivalMinute >= close)
                return $"{source.Name}到达时未营业（每天 {Hm(open)}–{Hm(close)}）";
            return null;
        }
    }
}
