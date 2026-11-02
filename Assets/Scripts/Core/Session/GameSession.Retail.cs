using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private static KeyValuePair<string, int>[] SaveRetailCounts(PCShopSell.Core.Shop.ShopSimulation shop) =>
            shop.Stock.GoodsLines().ToArray();
        private static GoodsLot[] SaveRetailLots(PCShopSell.Core.Shop.ShopSimulation shop) =>
            shop.Stock.GoodsLots.Select(l => l.Copy()).ToArray();
        public bool OtherRetailShopsUnlocked => HasShop && Clock.Today.TotalDays >= 14;
        public string PendingRetailKind { get; private set; } = "computer";
        public string RetailKind => _focus.RetailKind;
        public string RetailTypeBlocker(string kind)
        {
            if (!RetailShopTypes.All.Contains(kind)) return "请选择店铺类型";
            if (kind != "computer" && !OtherRetailShopsUnlocked) return "先开电脑店，第 15 天起可开其他类型的分店";
            return null;
        }
        public bool SignRetailLease(string placeId, bool newShop, string kind)
        {
            if(City.HasPlace(placeId)&&IsDedicatedVenue(City.GetPlace(placeId)))return SignLease(placeId,newShop);
            string why = RetailTypeBlocker(kind);
            if (why != null) return Fail(why);
            if (!newShop && kind != RetailKind) return Fail("搬店会保留原来的经营类型。");
            if (!SignLease(placeId, newShop)) return false;
            if (PendingLease != null) PendingRetailKind = kind;
            else if (newShop) { _focus.RetailKind = kind; _focus.Sim.RetailKind = kind; }
            return true;
        }
        public bool SetRetailKind(string kind) => SetShopMainCategory(kind);
        public bool SetShopMainCategory(string kind)
        {
            if(IsDedicatedVenue(PlayerShopPlace))return Fail("专用场馆只能延续原有服务，不能改为零售店。");
            if(!InPlayerShop || !RetailShopTypes.All.Contains(kind))return Fail("请在自己的店里选择主售品类。");
            Flags.Add("shop:main-category:"+_focus.Place.Id);
            if(IsSpecialBusiness(_focus)&&SpecialBusinessBusy(_focus))return Fail("请先完成或清理餐馆订单、二手库存及卖家，再转型。");
            if((kind=="restaurant"||kind=="secondhand")&&(PlayerShop.Orders.Count>0||PlayerShop.Queue.Count>0))return Fail("请先完成装机订单和排队结账，再转型。");
            _focus.RetailKind=kind;PlayerShop.RetailKind=kind;
            PlayerShop.TradingEnabled=!IsSpecialBusiness(_focus);
            if(IsSpecialBusiness(_focus))PlayerShop.ClearCustomers();
            foreach(var competition in _cityCompetitions.Where(e=>e.ShopId==_focus.Place.Id && e.RetailKind!=kind))competition.Ended=true;
            ApplyCityRetailDemand();
            UpdateShopFit();
            Say("本店主售已改为"+RetailShopTypes.Name(kind)+"，顾客将按此品类进店选购。");return true;
        }
        private void MigrateStoreMainCategories()
        {
            foreach(var shop in _shops.Where(s=>s.Rented))
            {
                if(IsDedicatedVenue(shop.Place))continue;
                if(Flags.Contains("shop:main-category:"+shop.Place.Id)) {shop.Sim.RetailKind=shop.RetailKind;continue;}
                if(shop.RetailKind!="computer"){shop.Sim.RetailKind=shop.RetailKind;continue;}
                var kinds=shop.Layout.Where(p=>FurnitureCatalog.Get(p.Id)?.Fixture==FixtureKind.Shelf)
                    .Select(p=>ShelfKinds.Get(p.Item)).Where(k=>k!=null).ToArray();
                if(kinds.Length==0 || kinds.Any(k=>k.IsParts))continue;
                var candidates=RetailShopTypes.All.Where(type=>type!="computer" && kinds.All(k=>k.GiftIds.All(id=>RetailShopTypes.Sells(type,id)))).ToArray();
                if(candidates.Length!=1)continue;
                string inferred=candidates[0];
                if(shop.Sim.Stock.GoodsLines().Where(line=>RetailShopTypes.Sells(inferred,line.Key)).Sum(line=>line.Value)<=0)continue;
                shop.Sim.ClearCustomers();shop.RetailKind=inferred;shop.Sim.RetailKind=inferred;
                Flags.Add("shop:main-category:"+shop.Place.Id);
            }
            foreach(var competition in _cityCompetitions.Where(e=>!CompetitionMatchesShop(e)))competition.Ended=true;
            ApplyCityRetailDemand();
        }
        private int RetailCount(Backpack bag, GiftDef def) => def == null ? 0 : def.IngredientId != null ? bag.Food.Ingredient.Where(b => b.Id == def.IngredientId).Sum(b => b.Count)
            : def.MealId != null ? bag.Food.Packed.Where(b => b.Id == def.Id).Sum(b => b.Count)
            : def.Id == "produce:rice" ? (int)Math.Floor(bag.Food.RiceKg / 0.5 + 1e-9) : bag.GiftCount(def.Id);
        private void AddRetailToBag(Backpack bag, GiftDef def, int count, int expiry, bool chilled = false)
        {
            if (def.Id == "produce:rice") { bag.Food.RiceKg += count * 0.5; return; }
            if (!RetailProducts.IsFood(def)) { bag.AddGift(def.Id, count); return; }
            var meal = FastFoodCatalog.Packed(def.Id);
            var batch = new FoodBatch { Id = def.IngredientId ?? def.Id, Name = def.Name, Count = count,
                ExpireDay = expiry >= 0 ? expiry : Clock.Today.TotalDays + def.ShelfDays,
                Hunger = meal?.Hunger ?? 0, Chilled = chilled, ChilledDays = chilled ? 7 : 0 };
            (def.IngredientId != null ? bag.Food.Ingredient : bag.Food.Packed).Add(batch);
        }
        private FoodBatch TakeRetailFromBag(Backpack bag, GiftDef def)
        {
            if (def.IngredientId != null) return Pantry.TakeBowl(bag.Food.Ingredient, def.IngredientId);
            if (def.MealId != null) return Pantry.TakeBowl(bag.Food.Packed, def.Id);
            if (def.Id == "produce:rice") { if (bag.Food.RiceKg < 0.5) return null; bag.Food.RiceKg -= 0.5; }
            else if (!bag.TakeGift(def.Id)) return null;
            return new FoodBatch { Id = def.Id, Count = 1, ExpireDay = -1 };
        }
        public bool HasRetailWholesale(Place source) => source != null && source.Kind == PlaceKind.Shop && !IsMyShop(source)
            && !IsVacant(source) && VentureAt(source.Id) == null && Gifts.All.Any(g => CityRetailGoodsAvailable(source, g));
        public bool BuyRetailWholesale(string sourceId, string id, int count, bool toBag, string shopId)
        {
            if (IsGameOver || IsWorking || !City.HasPlace(sourceId)) return false;
            var source = City.GetPlace(sourceId); var def = Gifts.Get(id);
            if (CurrentPlace != source || !HasRetailWholesale(source) || !IsPlaceOpen(source) || !CityRetailGoodsAvailable(source, def) || count < 1 || count > 100)
                return Fail("请到对应货源营业时采购，单项数量为 1–100。");
            if (!_retailCartBatch && count < CitySupplierMinimum(source.Id,id)) return Fail(def.Name+" · "+WholesaleMinimumText(source.Id,id)+"。");
            if (count > CitySupplierRemaining(source.Id, id)) return Fail("该商品本日供货配额只剩" + CitySupplierRemaining(source.Id, id) + "件，可换其他来源。 ");
            Money cost = CityRetailWholesalePrice(source, def) * count;
            int expiry = def.ShelfDays > 0 ? Clock.Today.TotalDays + def.ShelfDays : -1;
            if (toBag)
            {
                if (!CanCarry(def.WeightKg * count)) return false;
                if (!Ledger.TryDebit(Clock.Today, "进货", cost, source.Name + "批发 " + def.Name + "×" + count,
                    LedgerAttribution.ForShop(null, transactionKind: "采购").Product(id, count, CityRetailWholesalePrice(source, def).Cents))) return Fail("现金不足。");
                AddRetailToBag(Backpack, def, count, expiry); ApplyCargoLimit();
                RecordCitySupplierPurchase(source.Id, id, count);
                Say(def.Name + "×" + count + "已放入背包，可带到自己的店里上架。"); return true;
            }
            var dest = ShopAt(shopId);
            if (dest == null && !_retailCartBatch) return Fail("请选择收货店铺。");
            double free = double.PositiveInfinity;
            if(dest!=null)WithShop(dest, () => free = Math.Max(0, PlayerShop.Stock.FreeSpace - IncomingShopSpace));
            if (Stock.GoodsSpace(id, count) > free + 1e-9) return Fail("收货店的仓储空间不足，请减少数量。");
            bool sent = Ship(dest, "进货", cost, source.Name + "批发 " + def.Name + "×" + count,
                new CourierItem { Kind = CourierItemKind.Goods, Id = id, Count = count, ExpireDay = expiry }, "买了");
            if (sent) RecordCitySupplierPurchase(source.Id, id, count);
            return sent;
        }
        public string GoodsExpiryText(string id)
        {
            int expiry = PlayerShop.Stock.GoodsExpiry(id);
            if (expiry < 0) return "无期限";
            long minutes = Math.Max(0, FoodBatch.ExpiryMinute(expiry) - Clock.Now.TotalMinutes);
            return new GameDate(expiry) + $"（还剩 {minutes / 1440} 天 {minutes % 1440 / 60} 小时）";
        }
        private void SpoilRetailGoods(int today)
        {
            foreach (var shop in _shops)
            {
                var lost = shop.Sim.Stock.SpoilGoods(today);
                if (lost.Count > 0) Say(ShopTitle(shop) + "清理过期商品：" + string.Join("、", lost));
            }
        }
    }
}
