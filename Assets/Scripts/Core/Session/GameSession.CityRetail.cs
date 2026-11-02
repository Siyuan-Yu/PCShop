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
        private bool IsCompetingRetailShop(OwnedShop shop,bool requireOpen=true)=>shop!=null && shop.Rented
            && RetailShopTypes.All.Contains(shop.RetailKind) && !IsSpecialBusiness(shop) && !(shop.Place.Business??"").StartsWith("venue_",StringComparison.Ordinal)
            && (!requireOpen || shop.Sim.IsOpen);
        internal OwnedShop PriceWarTarget()=>MyShops.Where(shop=>IsCompetingRetailShop(shop) && shop.Sim.Markup>1.2
            && (shop.RetailKind!="computer" || Relation(Npcs.Jie)<NpcPerks.JieNoPriceWar))
            .OrderBy(shop=>shop==CurrentShop?0:1).ThenBy(shop=>shop.Place.Id,StringComparer.Ordinal).FirstOrDefault();
        private string RetailRivalName(OwnedShop shop)
        {
            if(shop.RetailKind=="clothing")
            {
                int tier=shop.Sim.Stock.GoodsLines().Where(l=>ClothingProducts.IsClothing(l.Key))
                    .GroupBy(l=>ClothingProducts.Outfit(l.Key).Tier).OrderByDescending(g=>g.Sum(l=>l.Value)).Select(g=>g.Key).FirstOrDefault();
                return Outfits.All.First(o=>o.Id!=Outfits.Default && o.Tier==tier).FictionalBrandId+"服饰";
            }
            return "同商圈"+RetailShopTypes.Name(shop.RetailKind);
        }
        private EventDef BindPriceWar(EventDef template)
        {
            var target=PriceWarTarget();if(target==null)return null;
            string kind=target.RetailKind,title=ShopTitle(target),rival=RetailRivalName(target);
            var bound=new EventDef{Id=template.Id,Title=RetailShopTypes.Name(kind)+"同行价格战",MinDay=template.MinDay,
                CooldownDays=template.CooldownDays,Weight=template.Weight,
                Body=$"{title}正在营业。{rival}推出了同类商品促销，宣传比同行便宜一成，顾客来询问你的价格是否也会调整。"};
            foreach(var item in template.Choices)
            {
                var choice=item;
                bound.Choices.Add(new EventChoice{Text=choice.Text,DisabledReason=choice.DisabledReason,
                    Apply=session=>
                    {
                        if(session.ShopAt(target.Place.Id)!=target || target.RetailKind!=kind || !session.IsCompetingRetailShop(target))
                            return "目标店铺已停业、退租或改变主售，本次竞争不再结算，其他店铺未受影响。";
                        string result=null;session.WithShop(target,()=>result=choice.Apply(session));return result;
                    }});
            }
            return bound;
        }
        private bool CompetitionMatchesShop(CityCompetition competition)
        {
            var shop=ShopAt(competition.ShopId);
            return IsCompetingRetailShop(shop,false) && shop.RetailKind==competition.RetailKind;
        }
        public bool CityComicExpoOpen => CurrentPlace?.Id == "life_expo" && Clock.Today.Day >= 22 && Clock.Today.Day <= 24 && IsPlaceOpen(CurrentPlace);
        public bool CityRetailGoodsAvailable(Place source, GiftDef product) => source != null && product != null && product.SoldAt == source.Business && !product.LimitedEdition && (!ClothingProducts.IsClothing(product.Id) || source.Id == "clothing_wholesale");
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
            if (e.Kind == "promotion") return (e.RivalName??"同商圈同行")+"正在促销，目标门店潜在询价客流暂为85%。可维持定位、调整陈列价格，或突出特色系列（潜在客流恢复到95%）；只影响对应店铺，收入仍需实际成交。";
            if (e.Kind == "quota") return $"{(City.HasPlace(e.SupplierId) ? City.GetPlace(e.SupplierId).Name : e.SupplierId)} 的 {Gifts.Get(e.ProductId)?.Name ?? (PartCatalog.Exists(e.ProductId) ? PartCatalog.Get(e.ProductId).Name : e.ProductId)} 每日最多供应4件。可选其他来源，不影响已有在途订单。";
            return (City.HasPlace(e.SupplierId)?City.GetPlace(e.SupplierId).Name:"对应供应商")+"这轮将常规起订量提高到3倍。可选择小批次合作：恢复常规起订量、批发价提高8%。兼营零售的店铺仍须满足零售数量10倍的批发门槛。仅影响本货源和事件期间的后续订单。";
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
            bool newDay=_cityRetailTickDay!=day;
            _cityRetailTickDay = day;
            foreach (var e in _cityCompetitions.Where(e => !e.Ended && (day >= e.EndDay || !CompetitionMatchesShop(e))))
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
                    var e = new CityCompetition { Id = "city-market:" + slot, Kind = kind, ShopId = target.Place.Id,
                        RetailKind=target.RetailKind,RivalName=RetailRivalName(target),SupplierId=offer?.Source.Id, ProductId=offer?.Id,
                        StartDay = start, EndDay = start + 3, Announced = true };
                    if (!_cityCompetitions.Any(x => x.Id == e.Id)) { _cityCompetitions.Add(e); Say(CityCompetitionTitle(e) + "：" + CityCompetitionTerms(e)); }
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
