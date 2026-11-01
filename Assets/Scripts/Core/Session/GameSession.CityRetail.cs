using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Common;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Finance;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<CityCompetition> _cityCompetitions = new List<CityCompetition>();
        private readonly Dictionary<string, int> _citySupplierPurchases = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _cityExpoModels = new Dictionary<string, int>();
        private int _cityCompetitionSlot = -1, _cityRetailTickDay = -1;
        public IReadOnlyList<CityCompetition> CityCompetitions => _cityCompetitions;
        public bool CityComicExpoOpen => CurrentPlace?.Id == "life_expo" && Clock.Today.Day >= 22 && Clock.Today.Day <= 24 && IsPlaceOpen(CurrentPlace);
        public bool CityRetailGoodsAvailable(Place source, GiftDef product) => source != null && product != null && product.SoldAt == source.Business && !product.LimitedEdition && (!ClothingProducts.IsClothing(product.Id) || source.Id == "clothing_wholesale");
        public Money CityRetailWholesalePrice(Place source, GiftDef product)
        {
            if (product == null) return Money.Zero;
            bool small = ActiveCityCompetition("terms").Any(e => e.SupplierId == source?.Id && e.Response == "small");
            return small ? product.WholesalePrice.Scale(1.08) : product.WholesalePrice;
        }
        private IEnumerable<CityCompetition> ActiveCityCompetition(string kind = null) => _cityCompetitions.Where(e => !e.Ended && e.StartDay <= Clock.Today.TotalDays && Clock.Today.TotalDays < e.EndDay && (kind == null || e.Kind == kind));
        private string SupplierCounterKey(string sourceId, string productId) => Clock.Today.TotalDays + "|" + sourceId + "|" + productId;
        public int CitySupplierRemaining(string sourceId, string productId)
        {
            int limit = ActiveCityCompetition("quota").Any(e => e.SupplierId == sourceId && e.ProductId == productId) ? 4 : int.MaxValue;
            if (limit == int.MaxValue) return limit;
            _citySupplierPurchases.TryGetValue(SupplierCounterKey(sourceId, productId), out int used);
            return Math.Max(0, limit - used);
        }
        public int CitySupplierMinimum(string sourceId) => ActiveCityCompetition("terms").Any(e => e.SupplierId == sourceId && e.Response != "small") ? 3 : 1;
        public void RecordCitySupplierPurchase(string sourceId, string productId, int count)
        {
            if (count <= 0) return;
            string key = SupplierCounterKey(sourceId, productId);
            _citySupplierPurchases.TryGetValue(key, out int old); _citySupplierPurchases[key] = old + count;
        }
        public string CityCompetitionTitle(CityCompetition e) => e.Kind == "promotion" ? "同街同行周末促销" : e.Kind == "quota" ? "批发货源配额紧张" : "原创工坊调整合作条件";
        public string CityCompetitionTerms(CityCompetition e)
        {
            if (e.Kind == "promotion") return "附近同行清理旧货，目标门店潜在询价客流暂为85%。可维持定位、调整陈列价格，或突出特色系列（潜在客流恢复到95%）；收入仍必须经过实际库存、服务与成交。";
            if (e.Kind == "quota") return $"{(City.HasPlace(e.SupplierId) ? City.GetPlace(e.SupplierId).Name : e.SupplierId)} 的 {Gifts.Get(e.ProductId)?.Name ?? (PartCatalog.Exists(e.ProductId) ? PartCatalog.Get(e.ProductId).Name : e.ProductId)} 每日最多供应4件。可选其他来源，不影响已有在途订单。";
            return "原创工坊这轮排产要求每款至少3件。可接受正常起订量，或选择小批次合作：恢复1件起订、批发价提高8%。仅影响本货源和事件期间的后续订单。";
        }
        public bool RespondCityCompetition(string id, string response)
        {
            var e = ActiveCityCompetition().FirstOrDefault(x => x.Id == id);
            if (e == null) return Fail("这项市场变化已经结束。 ");
            if (e.Kind == "promotion" && response != "position" && response != "maintain") return false;
            if (e.Kind == "terms" && response != "small" && response != "batch") return false;
            if (e.Kind == "quota" && response != "alternative") return false;
            e.Response = response; ApplyCityRetailDemand();
            Say("已记录应对：" + CityCompetitionTitle(e) + "。市场变化按到期时间自然结束。"); return true;
        }
        private void ApplyCityRetailDemand()
        {
            foreach (var shop in _shops)
                shop.Sim.CityRetailDemandMultiplier = ActiveCityCompetition("promotion").Where(e => e.ShopId == shop.Place.Id)
                    .Aggregate(1.0, (f, e) => f * (e.Response == "position" ? .95 : .85));
        }
        public void TickCityRetail()
        {
            int day = Clock.Today.TotalDays;
            if (_cityRetailTickDay == day) return;
            _cityRetailTickDay = day;
            foreach (var e in _cityCompetitions.Where(e => !e.Ended && day >= e.EndDay))
            { e.Ended = true; Say(CityCompetitionTitle(e) + "已结束，对应临时影响解除。"); }
            int slot = day < 14 ? -1 : (day - 14) / 7;
            if (slot >= 0 && slot > _cityCompetitionSlot && MyShopCount > 0)
            {
                _cityCompetitionSlot = slot;
                int start = 14 + slot * 7;
                // Migrating an old save never replays already finished rounds.
                if (day < start + 3)
                {
                    var shops = MyShops.OrderBy(s => s.Place.Id).ToArray(); var target = shops[slot % shops.Length];
                    string kind = slot % 3 == 0 ? "promotion" : slot % 3 == 1 ? "quota" : "terms";
                    var source = kind == "terms" || target.RetailKind == "models" ? City.Places.FirstOrDefault(p => p.Id == "model_workshop")
                        : City.Places.FirstOrDefault(p => p.Kind == PlaceKind.Wholesaler && p.Sells.Contains(PartCategory.Cpu));
                    var e = new CityCompetition { Id = "city-market:" + slot, Kind = kind, ShopId = target.Place.Id,
                        SupplierId = source?.Id ?? "model_workshop", ProductId = target.RetailKind == "models" ? ModelProducts.All[0].Id : "cpu0", StartDay = start, EndDay = start + 3, Announced = true };
                    if (!_cityCompetitions.Any(x => x.Id == e.Id)) { _cityCompetitions.Add(e); Say(CityCompetitionTitle(e) + "：" + CityCompetitionTerms(e)); }
                }
            }
            ApplyCityRetailDemand();
            foreach (string key in _citySupplierPurchases.Keys.Where(k => !k.StartsWith(day + "|")).ToArray()) _citySupplierPurchases.Remove(key);
        }
        private string CityExpoCounterKey(string productId) => (Clock.Today.TotalDays / 30) + "|" + productId;
        public int CityExpoModelRemaining(string productId) => Math.Max(0, 5 - (_cityExpoModels.TryGetValue(CityExpoCounterKey(productId), out int n) ? n : 0));
        public bool BuyCityExpoModel(string productId, int count, string shopId = null, bool toBag = true)
        {
            if (IsGameOver || IsWorking || !CityComicExpoOpen) return Fail("请在每月22—24日的漫展开放时间到会展中心现场采购。 ");
            var product = Gifts.Get(productId);
            if (product?.LimitedEdition != true || count < 1 || count > CityExpoModelRemaining(productId)) return Fail("本届该限定款每人限5件，请检查剩余数量。 ");
            Money price = product.WholesalePrice * count;
            if (toBag)
            {
                if (!CanCarry(product.WeightKg * count)) return false;
                if (!Ledger.TryDebit(Clock.Today, "进货", price, "漫展限定现货 " + product.Name + "×" + count,
                    LedgerAttribution.ForShop(null, transactionKind: "采购").Product(productId, count, product.WholesalePrice.Cents))) return Fail("现金不足。 ");
                Backpack.AddGift(productId, count); ApplyCargoLimit();
            }
            else
            {
                var dest = ShopAt(shopId);
                if (dest == null && !_retailCartBatch) return Fail("请选择有效收货店铺。 ");
                double free = double.PositiveInfinity; if(dest!=null)WithShop(dest, () => free = PlayerShop.Stock.FreeSpace - IncomingShopSpace);
                if (Stock.GoodsSpace(productId, count) > free + 1e-9) return Fail("收货店的仓储空间不足。 ");
                if (!Ship(dest, "进货", price, "漫展限定现货 " + product.Name + "×" + count,
                    new CourierItem { Kind = CourierItemKind.Goods, Id = productId, Count = count, ExpireDay = -1 }, "采购了")) return false;
            }
            string key = CityExpoCounterKey(productId); _cityExpoModels.TryGetValue(key, out int old); _cityExpoModels[key] = old + count;
            Say("购入" + product.Name + "×" + count + "，本届剩余可采购 " + CityExpoModelRemaining(productId) + " 件。"); return true;
        }
        public void SaveCityRetail(SaveData d)
        {
            d.cityRetailVersion = 1; d.cityCompetitionSlot = _cityCompetitionSlot;
            d.cityCompetitions = _cityCompetitions.Select(x => x.Copy()).ToArray();
            d.citySupplierPurchases = _citySupplierPurchases.Select(x => new CityRetailCounter { key = x.Key, count = x.Value }).ToArray();
            d.cityExpoModelPurchases = _cityExpoModels.Select(x => new CityRetailCounter { key = x.Key, count = x.Value }).ToArray();
        }
        public void LoadCityRetail(SaveData d)
        {
            _cityCompetitions.Clear();
            foreach (var e in d.cityCompetitions ?? Array.Empty<CityCompetition>())
                if (e != null && !string.IsNullOrEmpty(e.Id) && e.EndDay > e.StartDay && (e.Kind == "promotion" || e.Kind == "quota" || e.Kind == "terms") && !_cityCompetitions.Any(x => x.Id == e.Id)) _cityCompetitions.Add(e.Copy());
            _cityCompetitionSlot = d.cityRetailVersion > 0 ? d.cityCompetitionSlot : Math.Max(-1, (Clock.Today.TotalDays - 14) / 7 - 1);
            _citySupplierPurchases.Clear(); _cityExpoModels.Clear();
            foreach (var c in d.citySupplierPurchases ?? Array.Empty<CityRetailCounter>()) if (c != null && c.key != null && c.count > 0) _citySupplierPurchases[c.key] = c.count;
            foreach (var c in d.cityExpoModelPurchases ?? Array.Empty<CityRetailCounter>()) if (c != null && c.key != null && c.count > 0) _cityExpoModels[c.key] = c.count;
            _cityRetailTickDay = -1; ApplyCityRetailDemand();
        }
    }
}
