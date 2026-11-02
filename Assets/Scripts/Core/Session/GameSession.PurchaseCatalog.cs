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
        public string Key => GameSession.PurchaseKey(Kind, Id, Source.Id);
        public PurchaseLine Line(int count) => new PurchaseLine { Kind = Kind, PartId = Id, SourceId = Source.Id, Count = count };
    }

    public sealed partial class GameSession
    {
        public static string PurchaseKey(PurchaseGoodsKind kind, string id, string source) => ((int)kind) + "|" + id + "|" + source;
        public static string PurchaseKey(PurchaseLine line) => PurchaseKey(line.Kind, line.PartId, line.SourceId);

        public IReadOnlyList<PurchaseOffer> EmployeePurchaseCatalog()
        {
            var offers = new List<PurchaseOffer>();
            foreach (var source in City.Places)
            {
                if (source.Kind == PlaceKind.Wholesaler)
                    foreach (var p in PartCatalog.All.Where(p => StallSells(source, p)))
                        offers.Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Part, Id = p.Id, Name = p.Name,
                            Category = PartCatalog.CategoryName(p.Category), Price = StallPrice(source, p),
                            UnitKg = Stock.SpaceOf(p, true, 1) * Backpack.KgPerSpace, StockSpace = Stock.SpaceOf(p, true, 1) });
                if (ShopAt(source) != null || IsVacant(source) || VentureAt(source.Id) != null) continue;
                foreach (var gift in Gifts.All.Where(g => g.SoldAt == source.Business && CityRetailGoodsAvailable(source, g)))
                    offers.Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Gift, Id = gift.Id, Name = gift.Name + (HasDualRetailWholesale(source) ? "（批发）" : ""),
                        Category = ShelfKinds.ForGift(gift.Id)?.Name ?? "其他商品", Price = CityRetailWholesalePrice(source, gift), UnitKg = gift.WeightKg, StockSpace = Stock.GoodsSpace(gift.Id, 1) });
                if (source.Business == FarmBusiness)
                {
                    offers.Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Rice, Id = "rice", Name = "大米（零售，0.5千克/袋）",
                        Category = "米与食材", Price = Pantry.RicePricePerKg.Scale(0.5), UnitKg = 0.5 });
                    foreach (var ingredient in Ingredients.All)
                        offers.Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Ingredient, Id = ingredient.Id, Name = ingredient.Name + "（零售，" + ingredient.WeightKg.ToString("0.##") + "千克/份）",
                            Category = "米与食材", Price = ingredient.Price, UnitKg = Backpack.IngredientKg(ingredient.Id) });
                }
                if (source.Business == MallBusiness)
                    foreach (var furniture in FurnitureCatalog.All.Where(f => !f.Starter))
                        offers.Add(new PurchaseOffer { Source = source, Kind = PurchaseGoodsKind.Furniture, Id = furniture.Id, Name = furniture.Name,
                            Category = "家具与货架", Price = furniture.Price, UnitKg = furniture.WeightKg });
            }
            return offers.OrderBy(o => (int)o.Kind).ToArray();
        }

        public PurchaseOffer EmployeePurchaseOffer(PurchaseLine line, string legacySource = null)
        {
            if (line == null) return null;
            string source = line.SourceId ?? legacySource;
            return EmployeePurchaseCatalog().Where(o => o.Kind == line.Kind && o.Id == line.PartId && (string.IsNullOrEmpty(source) || o.Source.Id == source))
                .OrderBy(o => o.Price.Cents).FirstOrDefault();
        }

        // Old saves had one source for the entire plan. Bind it once to each line before editing.
        public void PrepareEmployeePurchaseDraft(EmployeePurchasePlan plan)
        {
            plan.Items = (plan.Items ?? Array.Empty<PurchaseLine>()).Where(x => x != null).Select(x =>
            {
                var copy = x.Copy();
                var offer = EmployeePurchaseOffer(copy, plan.SourceId);
                if (offer != null) copy.SourceId = offer.Source.Id;
                return copy;
            }).ToArray();
            plan.SourceId = null;
        }

        public string EmployeePurchaseBrowseSource(EmployeePurchasePlan plan)
        {
            if(plan==null)return null;
            string source=plan.BrowseSourceId;
            if(!plan.BrowseSourceSelected)
            {
                source=plan.SourceId;
                if(string.IsNullOrEmpty(source))
                {
                    var sources=(plan.Items??Array.Empty<PurchaseLine>()).Where(x=>x!=null && !string.IsNullOrEmpty(x.SourceId))
                        .Select(x=>x.SourceId).Distinct().ToArray();
                    source=sources.Length==1?sources[0]:null;
                }
            }
            return !string.IsNullOrEmpty(source) && EmployeePurchaseCatalog().Any(o=>o.Source.Id==source)?source:null;
        }

        public bool RememberEmployeePurchaseSource(string employeeId,string sourceId)
        {
            var worker=FindEmployee(employeeId);
            if(worker==null || worker.Role!=EmployeeRole.Buyer)return false;
            if(!string.IsNullOrEmpty(sourceId) && !EmployeePurchaseCatalog().Any(o=>o.Source.Id==sourceId))return false;
            worker.Person.PurchasePlan.BrowseSourceId=string.IsNullOrEmpty(sourceId)?null:sourceId;
            worker.Person.PurchasePlan.BrowseSourceSelected=true;
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
            int open = source.Kind == PlaceKind.Wholesaler ? WholesalerOpenMinute : source.Business == FarmBusiness ? FarmOpenMinute : 9 * 60;
            int close = source.Kind == PlaceKind.Wholesaler ? WholesalerCloseMinute : source.Business == FarmBusiness ? FarmCloseMinute : 21 * 60;
            if ((SupplierClosesOnWeekend(source) && Clock.Today.IsWeekend) || arrivalMinute < open || arrivalMinute >= close)
                return source.Name + "到达时未营业（" + (source.Kind == PlaceKind.Wholesaler ? "工作日 " : "每天 ") + $"{open / 60}:00–{close / 60}:00）";
            return null;
        }
    }
}
