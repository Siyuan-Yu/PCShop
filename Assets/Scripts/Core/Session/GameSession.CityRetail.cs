using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Common;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Finance;
using PCShopSell.Core.World;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<CityCompetition> _cityCompetitions = new List<CityCompetition>();
        private readonly Dictionary<string, int> _citySupplierPurchases = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _cityExpoModels = new Dictionary<string, int>();
        private readonly HashSet<(string shop, string kind, string product)> _matchedPromotions = new HashSet<(string, string, string)>();
        private int _cityCompetitionSlot = -1, _cityRetailTickDay = -1;
        public IReadOnlyList<CityCompetition> CityCompetitions => _cityCompetitions;
        private bool IsCompetingRetailShop(OwnedShop shop,bool requireOpen=true)=>shop!=null && shop.Rented
            && RetailShopTypes.All.Contains(shop.RetailKind) && !IsSpecialBusiness(shop) && !(shop.Place.Business??"").StartsWith("venue_",StringComparison.Ordinal)
            && (!requireOpen || shop.Sim.IsOpen);
        internal CityCompetition EventPromotion() => ActiveCityCompetition("promotion")
            .FirstOrDefault(e => e.RivalPlaceId != null && e.ProductId != null && e.Response == null
                && PromotionRivalStillTrading(e) && ShopAt(e.ShopId)?.Sim.IsOpen == true);

        private bool PromotionRivalStillTrading(CityCompetition promotion)
        {
            if (!City.HasPlace(promotion.RivalPlaceId)) return false;
            var place = City.GetPlace(promotion.RivalPlaceId);
            return !IsVacant(place) && ShopAt(place) == null && VentureAt(place.Id) == null && !IsPlayerPropertyOccupied(place.Id);
        }

        private Place LocalPromotionRival(OwnedShop shop) => City.Places.Where(place => place.Id != shop.Place.Id
            && place.Kind == PlaceKind.Shop && !IsVacant(place) && ShopAt(place) == null && VentureAt(place.Id) == null && !IsPlayerPropertyOccupied(place.Id)
            && DistanceCity(StandOf(shop.Place), StandOf(place)) <= ShopCompetitionRadiusWorld
            && (shop.RetailKind == "computer" && place.Business == "电脑"
                || shop.RetailKind == "clothing" && place.Business == "服装" && place.Id != "clothing_wholesale"
                || shop.RetailKind == "models" && (place.Business == "模型手办" || place.Business == "潮玩模型")
                || shop.RetailKind == PetShopCatalog.ShopKind && place.Business == "宠物用品"))
            .OrderBy(place => DistanceCity(StandOf(shop.Place), StandOf(place))).FirstOrDefault();

        private double EventPriceFactor(OwnedShop shop, string productId) =>
            _matchedPromotions.Contains((shop.Place.Id, shop.RetailKind, productId)) ? .9 : 1;
        public const int PromotionServiceWageRaise = 20;

        private EventDef BindPriceWar(EventDef template)
        {
            var promotion = EventPromotion();
            if (promotion == null) return null;
            var shop = ShopAt(promotion.ShopId);
            string product = new EventStock { Id = promotion.ProductId }.Name;
            string Respond(string response)
            {
                if (!PromotionRivalStillTrading(promotion) || !IsCompetingRetailShop(shop))
                    return "促销门店或经营状态已变化，本次没有调整售价。";
                double awarenessBefore = shop.Sim.Awareness;
                if (!RespondCityCompetition(promotion.Id, response)) return "这轮促销已处理，或门店已无法采取这个方案。";
                return response == "match" ? $"{product}临时九折，促销结束后恢复原价。"
                    : response == "position" ? $"已加强服务，员工基础日薪每人增加 ¥{PromotionServiceWageRaise}。本轮预计保留 {promotion.RetainedDemand:P0} 客流。"
                    : $"维持原价，门店人气减少 {awarenessBefore - shop.Sim.Awareness:0.##}。促销结束后解除临时客流影响。";
            }
            return EventScene(template, $"{promotion.RivalName}正在给{product}做九折促销，持续至 {new GameDate(promotion.EndDay)} 00:00。"
                + $"\n受影响门店：{ShopTitle(shop)}。",
                Choose($"只对{product}限时跟价", _ => Respond("match")),
                Choose($"加强服务 · 基础日薪每人 +¥{PromotionServiceWageRaise}", _ => Respond("position"),
                    s => s.Employees.Any(e => e.Shop == shop), "这家店目前没有员工"),
                Choose("维持原价 · 门店人气 −1", _ => Respond("maintain")));
        }
        private bool CompetitionMatchesShop(CityCompetition competition)
        {
            var shop=ShopAt(competition.ShopId);
            return IsCompetingRetailShop(shop,false) && shop.RetailKind==competition.RetailKind;
        }
        public bool CityComicExpoOpen => CurrentPlace?.Id == "life_expo" && Clock.Today.Day >= 22 && Clock.Today.Day <= 24 && IsPlaceOpen(CurrentPlace);
        public bool CityRetailGoodsAvailable(Place source, GiftDef product) => source != null && product != null && !product.LimitedEdition
            && product.SoldAt == source.Business
            && (!ClothingProducts.IsClothing(product.Id) || source.Id == "clothing_wholesale");
        public Money CityRetailWholesalePrice(Place source, GiftDef product)
        {
            if (product == null) return Money.Zero;
            bool small = ActiveCityCompetition("terms").Any(e => e.SupplierId == source?.Id && e.Response == "small");
            return product.WholesalePrice.Scale(IndustryCycles.GoodsFactor(product,Clock.Today.TotalDays)*(small?1.08:1));
        }
        private IEnumerable<CityCompetition> ActiveCityCompetition(string kind = null) => _cityCompetitions.Where(e => !e.Ended && CompetitionMatchesShop(e) && e.StartDay <= Clock.Today.TotalDays && Clock.Today.TotalDays < e.EndDay && (kind == null || e.Kind == kind));
        private string SupplierCounterKey(string sourceId, string productId) => Clock.Today.TotalDays + "|" + sourceId + "|" + productId;
        public int CitySupplierRemaining(string sourceId, string productId)
        {
            int limit = ActiveCityCompetition("quota").Any(e => e.SupplierId == sourceId && e.ProductId == productId) ? 4 : SupplierCapacity(sourceId,productId);
            _citySupplierPurchases.TryGetValue(SupplierCounterKey(sourceId, productId), out int used);
            if(productId=="produce:rice")return Math.Max(0,limit-(used+1)/2);
            return Math.Max(0, limit - used);
        }
        public int CitySupplierMinimum(string sourceId) => ActiveCityCompetition("terms").Any(e => e.SupplierId == sourceId && e.Response != "small") ? 3 : 1;
        public bool HasDualRetailWholesale(Place source) => source!=null&&source.Kind==PlaceKind.Shop
            && (source.Business==FarmBusiness||source.Business==Gifts.TobaccoBusiness||source.Business==FastFoodBusiness);
        public int CitySupplierMinimum(string sourceId,string productId)
        {
            int minimum=CitySupplierMinimum(sourceId);
            if(!City.HasPlace(sourceId)||!HasDualRetailWholesale(City.GetPlace(sourceId)))return minimum;
            // Farm retail rice is sold in .25 kg increments; wholesale bags hold .5 kg.
            int regular=productId=="produce:rice"?5:10;
            return minimum*regular;
        }
        public int PurchaseMinimum(PurchaseOffer offer)=>offer.Kind==PurchaseGoodsKind.Gift
            ?CitySupplierMinimum(offer.Source.Id,offer.Id):CitySupplierMinimum(offer.Source.Id);
        public string WholesaleMinimumText(string sourceId,string productId)
        {
            int minimum=CitySupplierMinimum(sourceId,productId);
            return productId=="produce:rice"?"起订 "+minimum+" 袋（"+(minimum*.5).ToString("0.##")+" 千克）":"起订 "+minimum+" 件";
        }
        public void RecordCitySupplierPurchase(string sourceId, string productId, int count)
        {
            if (count <= 0) return;
            string key = SupplierCounterKey(sourceId, productId);
            _citySupplierPurchases.TryGetValue(key, out int old); _citySupplierPurchases[key] = old + (productId=="produce:rice"?count*2:count);
        }
        public string CityCompetitionTitle(CityCompetition e) => e.Kind == "promotion" ? RetailShopTypes.Name(e.RetailKind)+"同行促销" : e.Kind == "quota" ? "同行业货源配额紧张" : "同行业供应商调整合作条件";
        public string CityCompetitionTerms(CityCompetition e)
        {
            if (e.Kind == "promotion") return $"{e.RivalName ?? "同商圈同行"}促销至 {new GameDate(e.EndDay)} 00:00。"
                + (e.Response == "match" ? $"已对{new EventStock { Id = e.ProductId }.Name}临时跟价，其余售价不变。"
                    : e.Response == "position" ? $"已加强服务，预计保留 {(e.RetainedDemand > 0 ? e.RetainedDemand : .95):P0} 客流。"
                    : e.Response == "maintain" ? "已维持原价，促销期间保留 85% 客流。"
                    : "暂时保留 85% 客流，可选择应对方式。");
            if (e.Kind == "quota") return $"{(City.HasPlace(e.SupplierId) ? City.GetPlace(e.SupplierId).Name : e.SupplierId)} 的 {Gifts.Get(e.ProductId)?.Name ?? (PartCatalog.Exists(e.ProductId) ? PartCatalog.Get(e.ProductId).Name : e.ProductId)} 每日最多供应4件。可选其他来源，不影响已有在途订单。";
            return (City.HasPlace(e.SupplierId)?City.GetPlace(e.SupplierId).Name:"对应供应商")+"这轮将常规起订量提高到3倍。可选择小批次合作：恢复常规起订量、批发价提高8%。兼营零售的店铺仍须满足零售数量10倍的批发门槛。仅影响本货源和事件期间的后续订单。";
        }
        public bool RespondCityCompetition(string id, string response)
        {
            var e = ActiveCityCompetition().FirstOrDefault(x => x.Id == id);
            if (e == null) return Fail("这项市场变化已经结束。 ");
            if (e.Kind == "promotion" && response != "position" && response != "maintain" && response != "match") return false;
            if (e.Kind == "terms" && response != "small" && response != "batch") return false;
            if (e.Kind == "quota" && response != "alternative") return false;
            if (e.Kind == "promotion")
            {
                var shop = ShopAt(e.ShopId);
                if (e.Response != null || !PromotionRivalStillTrading(e) || !IsCompetingRetailShop(shop))
                    return Fail("这轮促销已处理或已结束。");
                if (response == "position")
                {
                    var staff = Employees.Where(row => row.Shop == shop).Select(row => row.Person).Distinct().ToArray();
                    if (staff.Length == 0) return Fail("这家店目前没有员工可以加强服务。");
                    foreach (var employee in staff) employee.DailyWageYuan += PromotionServiceWageRaise;
                    double advice = shop.RetailKind == "computer" && Relation(Npcs.Jie) >= NpcPerks.JiePriceWarAdvice ? .025 : 0;
                    e.RetainedDemand = Math.Min(1, .90 + ShopSalesBonus(shop) * .3 + Math.Max(0, shop.Sim.ShopReputation - 50) * .001 + advice);
                }
                else if (response == "maintain") shop.Sim.AdjustShopAwareness(-1);
            }
            e.Response = response;
            ApplyCityRetailDemand();
            Say("已记录应对：" + CityCompetitionTitle(e) + "。市场变化按到期时间自然结束。"); return true;
        }
        private void ApplyCityRetailDemand()
        {
            _matchedPromotions.Clear();
            foreach (var promotion in ActiveCityCompetition("promotion"))
                if (promotion.Response == "match") _matchedPromotions.Add((promotion.ShopId, promotion.RetailKind, promotion.ProductId));
            foreach (var shop in _shops)
                shop.Sim.CityRetailDemandMultiplier = ActiveCityCompetition("promotion").Where(e => e.ShopId == shop.Place.Id)
                    .Aggregate(1.0, (f, e) => f * (e.Response == "match" ? 1 : e.Response == "position"
                        ? (e.RetainedDemand > 0 ? e.RetainedDemand : .95) : .85));
        }
        public void TickCityRetail()
        {
            int day = Clock.Today.TotalDays;
            bool newDay=_cityRetailTickDay!=day;
            _cityRetailTickDay = day;
            foreach (var e in _cityCompetitions.Where(e => !e.Ended && (day >= e.EndDay || !CompetitionMatchesShop(e)
                || e.RivalPlaceId != null && !PromotionRivalStillTrading(e))))
            { e.Ended = true; Say(CityCompetitionTitle(e) + "已结束，对应临时影响解除。"); }
            int slot = day < 14 ? -1 : (day - 14) / 7;
            if (slot >= 0 && slot > _cityCompetitionSlot && MyShopCount > 0)
            {
                int start = 14 + slot * 7;
                if(day>=start+3)_cityCompetitionSlot=slot;
                // Migrating an old save never replays already finished rounds.
                if (day < start + 3)
                {
                    var shops = MyShops.Where(shop=>IsCompetingRetailShop(shop)).OrderBy(s => s.Place.Id,StringComparer.Ordinal).ToArray();
                    if(shops.Length==0)
                    {
                        ApplyCityRetailDemand();
                        if(newDay)foreach(string key in _citySupplierPurchases.Keys.Where(k=>!k.StartsWith(day+"|")).ToArray())_citySupplierPurchases.Remove(key);
                        return;
                    }
                    var target = shops[slot % shops.Length];
                    string kind = slot % 3 == 0 ? "promotion" : slot % 3 == 1 ? "quota" : "terms";
                    if(kind=="terms" && target.RetailKind=="computer")kind="promotion";
                    var offer=EmployeePurchaseCatalog().Where(o=>target.RetailKind=="computer"?o.Kind==PurchaseGoodsKind.Part:
                            o.Kind==PurchaseGoodsKind.Gift && RetailShopTypes.Sells(target.RetailKind,o.Id))
                        .OrderByDescending(o=>o.Kind==PurchaseGoodsKind.Part?target.Sim.Stock.CountAll(PartCatalog.Get(o.Id)):target.Sim.Stock.GoodsCount(o.Id))
                        .ThenBy(o=>o.Price.Cents).ThenBy(o=>o.Source.Id,StringComparer.Ordinal).FirstOrDefault();
                    if(kind!="promotion" && offer==null)kind="promotion";
                    var rival = kind == "promotion" ? LocalPromotionRival(target) : null;
                    var promoted = rival == null ? null : EventShelfProducts(target)
                        .Where(product => target.RetailKind != "computer" || product.Part != null)
                        .OrderByDescending(product => product.Count).FirstOrDefault();
                    var e = new CityCompetition { Id = "city-market:" + slot, Kind = kind, ShopId = target.Place.Id,
                        RetailKind=target.RetailKind,RivalName=rival?.Name,RivalPlaceId=rival?.Id,
                        SupplierId=offer?.Source.Id, ProductId=kind == "promotion" ? promoted?.Id : offer?.Id,
                        StartDay = start, EndDay = start + 3, Announced = true };
                    if ((kind != "promotion" || promoted != null) && !_cityCompetitions.Any(x => x.Id == e.Id))
                    {
                        _cityCompetitions.Add(e);
                        Say(CityCompetitionTitle(e) + "：" + CityCompetitionTerms(e));
                    }
                    _cityCompetitionSlot=slot;
                }
            }
            ApplyCityRetailDemand();
            if(newDay)foreach (string key in _citySupplierPurchases.Keys.Where(k => !k.StartsWith(day + "|")).ToArray()) _citySupplierPurchases.Remove(key);
        }
        private string CityExpoCounterKey(string productId) => (Clock.Today.TotalDays / 30) + "|" + productId;
        public int CityExpoModelRemaining(string productId) => Math.Max(0, 5 - (_cityExpoModels.TryGetValue(CityExpoCounterKey(productId), out int n) ? n : 0));
        public bool BuyCityExpoModel(string productId, int count, string shopId = null, bool toBag = true)
        {
            if (IsGameOver || IsWorking || !CityComicExpoOpen) return Fail("请在每月22—24日的漫展开放时间到会展中心现场采购。 ");
            var product = Gifts.Get(productId);
            if (product?.LimitedEdition != true || count < 1 || count > CityExpoModelRemaining(productId)) return Fail("本届该限定款每人限5件，请检查剩余数量。 ");
            Money price = CityRetailWholesalePrice(CurrentPlace,product) * count;
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
                if (e != null && !string.IsNullOrEmpty(e.Id) && e.EndDay > e.StartDay && (e.Kind == "promotion" || e.Kind == "quota" || e.Kind == "terms") && !_cityCompetitions.Any(x => x.Id == e.Id))
                {
                    var copy=e.Copy();
                    if(string.IsNullOrEmpty(copy.RetailKind))copy.RetailKind=!string.IsNullOrEmpty(copy.ProductId)&&PartCatalog.Exists(copy.ProductId)?"computer":RetailShopTypes.GoodsKind(copy.ProductId);
                    if(!CompetitionMatchesShop(copy))copy.Ended=true;
                    _cityCompetitions.Add(copy);
                }
            _cityCompetitionSlot = d.cityRetailVersion > 0 ? d.cityCompetitionSlot : Math.Max(-1, (Clock.Today.TotalDays - 14) / 7 - 1);
            _citySupplierPurchases.Clear(); _cityExpoModels.Clear();
            foreach (var c in d.citySupplierPurchases ?? Array.Empty<CityRetailCounter>()) if (c != null && c.key != null && c.count > 0) _citySupplierPurchases[c.key] = c.count;
            foreach (var c in d.cityExpoModelPurchases ?? Array.Empty<CityRetailCounter>()) if (c != null && c.key != null && c.count > 0) _cityExpoModels[c.key] = c.count;
            _cityRetailTickDay = -1; ApplyCityRetailDemand();
        }
    }
}
