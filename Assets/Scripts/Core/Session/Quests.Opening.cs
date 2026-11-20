using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const string OpeningStockMigrationFlag = "tutorial:opening-stock-purchase-checked";
        private const string SecondhandShelfMigrationFlag = "tutorial:opening-secondhand-shelf-checked";
        private const string RestaurantShelfMigrationFlag = "tutorial:opening-restaurant-shelf-checked";

        // The rent reward marker is set by the prebuilt-shop setup, which already supplies stock.
        internal Place OpeningSupplyMarket => HasShop && OpeningRewardKind("rent") == null
            && ShopSupplyMarketId(RetailKind) is string id ? PurchaseDestination(City.GetPlace(id)) : null;

        internal bool HasOpeningStockPurchase(Place market)
        {
            int signedDay = CurrentShop.Lease?.SignedDay ?? 0;
            string wholesaleMemo = market.Name + "批发 ";
            string partsMemo = market.Name + " ";
            return Ledger.Entries.Any(entry =>
            {
                if (entry.Date.TotalDays < signedDay || !entry.Amount.IsNegative) return false;
                if (RetailKind == "restaurant" && entry.Category == "买菜") return true;
                if (entry.Category != "进货") return false;
                if (RetailKind == "computer")
                    return entry.Memo?.StartsWith(partsMemo, StringComparison.Ordinal) == true;
                if (entry.Memo?.StartsWith(wholesaleMemo, StringComparison.Ordinal) != true) return false;
                string product = entry.Attribution?.ProductId;
                return RetailKind == "restaurant" ? product?.StartsWith("produce:", StringComparison.Ordinal) == true
                    : RetailShopTypes.Sells(RetailKind, product);
            });
        }

        private void MigrateOpeningStockProgress()
        {
            // Older saves completed stock from the free furniture-store reward.
            if (Flags.Contains(OpeningStockMigrationFlag) || QuestCompleted("sale")
                || !(OpeningSupplyMarket is Place market)) return;
            Flags.Add(OpeningStockMigrationFlag);
            if (QuestCompleted("stock") && !HasOpeningStockPurchase(market)) Flags.Remove("tutorial:stock");
        }

        private void MigrateSecondhandShelfProgress()
        {
            if (!HasShop || RetailKind != "secondhand" || QuestCompleted("sale") || !Flags.Add(SecondhandShelfMigrationFlag)) return;
            // Older saves marked this step complete when the shelf was merely placed.
            if (ShelvesOfKind("secondhand") == 0) Flags.Remove("tutorial:shelf");
        }

        private void MigrateRestaurantShelfProgress()
        {
            if (!HasShop || CurrentShop != MainShop || RetailKind != "restaurant" || QuestCompleted("sale")
                || !Flags.Add(RestaurantShelfMigrationFlag)) return;
            if (!OpeningFurniturePlaced(CurrentShop, FurnitureCatalog.Shelf))
            {
                Flags.Remove("tutorial:place");
                if (MissingOpeningFurniture().Contains(FurnitureCatalog.Shelf)) Flags.Remove("tutorial:mall");
            }
            if (!HasRestaurantDrinkShelf(CurrentShop)) Flags.Remove("tutorial:shelf");
        }

        private const string OpeningChoicePrefix="shop:choose-kind:";
        public string PendingShopKindPlaceId=>Flags.Where(f=>f.StartsWith(OpeningChoicePrefix))
            .Select(f=>f.Substring(OpeningChoicePrefix.Length)).FirstOrDefault(id=>ShopAt(id)!=null||PendingLease?.PlaceId==id);
        public bool SignLeaseThenChooseKind(string placeId,bool newShop)
        {
            if(PendingShopKindPlaceId!=null)return Fail("请先确认上一间铺子的经营类型。");
            if(!City.HasPlace(placeId))return false;
            bool choose=(!HasShop||newShop)&&!IsDedicatedVenue(City.GetPlace(placeId));
            if(choose)Flags.Add(OpeningChoicePrefix+placeId);
            if(SignLease(placeId,newShop))return true;
            if(choose)Flags.Remove(OpeningChoicePrefix+placeId);
            return false;
        }
        public bool ConfirmOpeningKind(string placeId,string kind)
        {
            if(PendingShopKindPlaceId!=placeId)return Fail("这间店已完成经营类型确认。");
            string blocker=RetailTypeBlocker(kind);if(blocker!=null)return Fail(blocker);
            Flags.Remove(OpeningChoicePrefix+placeId);Flags.Remove("shop:choose-first:"+placeId);
            if(PendingLease?.PlaceId==placeId)PendingRetailKind=kind;
            else
            {
                var previous=_focus;
                SetFocus(ShopAt(placeId));SetOpeningShopKind(kind);RefreshQuestProgress();
                SetFocus(previous);
            }
            Say("已确认经营"+RetailShopTypes.Name(kind)+(PendingLease?.PlaceId==placeId?"，交房后发放尚未领取的开店任务奖励。":"。"));
            return true;
        }
        private void SetOpeningShopKind(string kind)
        {
            if(IsDedicatedVenue(_focus.Place))return;
            _focus.RetailKind=RetailShopTypes.Valid(kind);_focus.Sim.RetailKind=_focus.RetailKind;
            if(!Flags.Contains(OpeningChoicePrefix+_focus.Place.Id))Flags.Add("shop:main-category:"+_focus.Place.Id);
            UpdateShopFit();
        }

        private static readonly string[] RestaurantServiceFurniture =
        {
            FurnitureCatalog.ShopCounter, "restaurant_stove",
            "restaurant_table", FurnitureCatalog.FridgeSmall
        };

        public static string[] OpeningFurniture(string kind)=>kind=="restaurant"
            ?RestaurantServiceFurniture.Append(FurnitureCatalog.Shelf).ToArray()
            :kind=="secondhand"?new[]{"used_buydesk","used_display",FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter}
            :kind==PetShopCatalog.ShopKind?new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter,PetShopCatalog.SmallHouse}
            :new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter};

        public static bool MatchesOpeningFurniture(string id, FurnitureDef item) => item != null && (item.Id == id
            || id == FurnitureCatalog.Shelf && item.Fixture == FixtureKind.Shelf
            || id == FurnitureCatalog.FridgeSmall && item.Role == FurnitureRole.Fridge
            || id == PetShopCatalog.SmallHouse && PetShopCatalog.HouseCapacity(item.Id) > 0);

        public static bool OpeningFurniturePlaced(OwnedShop shop, string id)
        {
            foreach (var placed in shop.Layout)
                if (MatchesOpeningFurniture(id, FurnitureCatalog.Get(placed.Id))) return true;
            return false;
        }

        public static string OpeningFurnitureName(string id) => id == FurnitureCatalog.FridgeSmall ? "冰箱" : FurnitureCatalog.Get(id).Name;

        internal static bool HasRestaurantDrinkShelf(OwnedShop shop) => shop.Layout.Any(item =>
            item.Item == "drink" && FurnitureCatalog.Get(item.Id)?.Fixture == FixtureKind.Shelf);
        internal const string RestaurantDrinkShelfHint =
            "走到货架旁按 F，或在「库存」选择货架。点「选择摆什么」或「换品类」，从「零售商品」里选「饮料」。堂食顾客可以加购陈列的饮料。";

        private string OpeningRewardKind(string id)=>Flags.FirstOrDefault(f=>f.StartsWith("tutorial:reward:"+id+":"))?.Substring(("tutorial:reward:"+id+":").Length);
        public string[] MissingOpeningFurniture() => MissingOpeningFurniture(CurrentShop);

        private string[] MissingOpeningFurniture(OwnedShop shop)
        {
            if (shop?.Rented != true) return Array.Empty<string>();
            return OpeningFurniture(shop.RetailKind).Where(id => !OpeningFurniturePlaced(shop, id)
                && !FurnitureCatalog.All.Any(item => MatchesOpeningFurniture(id, item) && FurnitureOwnedUnplaced(item.Id) > 0)).ToArray();
        }

        private OwnedShop ExpansionStorefront(int number) => _shops
            .Where(shop => shop.Rented && !IsDedicatedVenue(shop.Place)).Skip(number - 1).FirstOrDefault();

        internal bool ExpansionStorefrontComplete(int number)
        {
            var shop = ExpansionStorefront(number);
            return shop != null && (shop.RetailKind != "restaurant" || HasRestaurantDrinkShelf(shop)) && Ledger.Entries.Any(entry =>
                FinanceReport.IsShopSale(entry, shop.Place.Id, shop.SalesAccountId));
        }

        internal QuestDef ExpansionOpeningGuide(QuestDef source, int number)
        {
            var shop = ExpansionStorefront(number);
            if (shop == null)
            {
                if (!PendingIsNewShop || PendingLease == null || IsDedicatedVenue(City.GetPlace(PendingLease.PlaceId)))
                    return source;
                var pending = source.Copy();
                pending.Target = QuestTarget.Bed;
                pending.Short = ShopHandoverText(PendingLease.StartDay);
                pending.Hint = $"已签下{City.GetPlace(PendingLease.PlaceId).Name}。{pending.Short}，交房后可以进店装修。";
                return pending;
            }

            var guide = source.Copy();
            string kind = shop.RetailKind;
            string name = ShopTitle(shop);
            guide.Progress = _ => name;
            QuestDef Step(QuestTarget target, string next, string hint, Place destination = null)
            {
                guide.Target = target;
                guide.Short = next;
                guide.Hint = $"{name} · {RetailShopTypes.Name(kind)}\n{hint}";
                guide.GuideDestination = destination;
                return guide;
            }

            var required = OpeningFurniture(kind);
            var missing = MissingOpeningFurniture(shop);
            if (missing.Length > 0)
                return Step(QuestTarget.Mall, "买" + string.Join("、", missing.Select(OpeningFurnitureName)),
                    "还需购买\n" + string.Join("\n", missing.Select(id => FurnitureCatalog.Get(id).Name + " ×1 · " + FurnitureCatalog.Get(id).Price))
                    + (kind == "restaurant" && missing.Contains(FurnitureCatalog.Shelf) ? "\n货架任选一种规格。" : "")
                    + $"\n送货时选「{name}」。");

            var unplaced = required.Where(id => !OpeningFurniturePlaced(shop, id)).ToArray();
            if (unplaced.Length > 0)
                return Step(QuestTarget.Renovate, "到新店装修，摆好开店设施",
                    "在店铺「装修」中摆放\n" + string.Join("\n", unplaced.Select(OpeningFurnitureName))
                    + "\n把设施送到新店后再摆放。装修当天歇业。", shop.Place);

            if (kind == "restaurant" && !HasRestaurantDrinkShelf(shop))
                return Step(QuestTarget.Stock, "把货架设为「饮料」", RestaurantDrinkShelfHint, shop.Place);

            if (shop.Sim.Queue.Count > 0 || kind == "restaurant" && _specialBusinesses.TryGetValue(shop.Place.Id, out var service)
                && service.Orders.Any(order => order.Stage is DiningStage.Cooking or DiningStage.Ready or DiningStage.Eating or DiningStage.Checkout))
                return FirstSale();

            var shelves = shop.Layout.Where(item => FurnitureCatalog.Get(item.Id)?.Fixture == FixtureKind.Shelf)
                .Select(item => ShelfKinds.Get(item.Item)).Where(shelf => shelf != null)
                .Where(shelf => kind == "secondhand" ? shelf.Id == "secondhand"
                    : kind == "computer" ? shelf.IsParts : shelf.GiftIds.Any(id => RetailShopTypes.Sells(kind, id)))
                .Distinct().ToArray();
            if (kind != "restaurant" && shelves.Length == 0)
            {
                string category = kind == "secondhand" ? "二手转售品" : kind == "computer" ? "电脑配件" : RetailShopTypes.Name(kind) + "商品";
                return Step(QuestTarget.Stock, "设置新店的货架品类",
                    $"在「库存」选择货架，或走到货架前按 F，再选「换品类」，陈列{category}。", shop.Place);
            }

            if (kind == "restaurant")
            {
                bool kitchenReady = KitchenRice(shop) >= .25 && RecipeIngredients.Any(id => KitchenIngredientCount(shop, id) > 0);
                if (!kitchenReady)
                {
                    var food = new[] { shop.Fridge.Food, shop.Locker.Food, shop.Temp.Food, Backpack.Food };
                    if (food.Sum(store => store.RiceKg) >= .25
                        && RecipeIngredients.Any(id => food.Sum(store => store.IngredientCount(id)) > 0))
                        return Step(QuestTarget.Stock, "把大米和配菜放进新店冰箱",
                            "到冰箱旁按 F，把大米和至少一种配菜从背包或后仓放进冰箱。临时存放的食材可先取回。", shop.Place);
                    if (IncomingItems().Any(item => item.Kind == CourierItemKind.Rice
                        || item.Kind == CourierItemKind.Ingredient && RecipeIngredients.Contains(item.Id)))
                        return Step(QuestTarget.Stock, "等待食材送到新店",
                            "货车正在送货。到货后检查冰箱里的大米和配菜，少的再补。", shop.Place);
                    return Supply("去农贸市场买大米和配菜", "买大米和至少一种配菜，送到这家餐馆后放进冰箱。");
                }
            }
            else if (kind != "secondhand" && !shelves.Any(shelf => shop.Sim.ShelfFillOf(shelf).Total > 0))
            {
                var stockedShelf = kind == "computer"
                    ? ShelfKinds.All.FirstOrDefault(shelf => shelf.IsParts && PartCatalog.InCategory(shelf.Category).Any(part => shop.Sim.Stock.CountAll(part) > 0))
                    : ShelfKinds.GoodsKinds.FirstOrDefault(shelf => shelf.GiftIds.Any(id => RetailShopTypes.Sells(kind, id)
                        && shop.Sim.Stock.GoodsCount(id) > 0));
                if (stockedShelf != null)
                    return Step(QuestTarget.Stock, $"把货架设为「{stockedShelf.Name}」",
                        "店里已有这类商品。设置对应货架后，后仓的商品会自动陈列。", shop.Place);
                bool hasGoodsToUnload = kind == "computer"
                    ? Cargo.Lines().Any()
                    : new[] { Backpack, shop.Locker, shop.Temp }.Any(store => store.GiftLines
                        .Any(line => RetailShopTypes.Sells(kind, line.Key)));
                if (hasGoodsToUnload)
                    return Step(QuestTarget.Stock, "把首批货放进新店库存",
                        kind == "computer" ? "到新店打开背包，把配件卸到店里，再设置对应货架。"
                            : "到新店「库存」选择从背包或后仓上架。临时存放的货可先取回，再设置对应货架。", shop.Place);
                if (IncomingItems().Any(item => kind == "computer" ? item.Kind == CourierItemKind.Part
                    : (item.Kind == CourierItemKind.Gift || item.Kind == CourierItemKind.Goods)
                        && RetailShopTypes.Sells(kind, item.Id)))
                    return Step(QuestTarget.Stock, "等待首批货送到新店",
                        "货车正在送货。到货后在新店「库存」检查对应货架。", shop.Place);
                return Supply("进新店的第一批货", $"采购和货架对应的商品（{string.Join("、", shelves.Select(shelf => shelf.Name))}），自提入库或送到「{name}」。");
            }

            if (shop.RenovationDay >= Clock.Today.TotalDays)
                return Step(QuestTarget.Bed, "装修结束，等明天开业", "这家店今天歇业装修。次日营业时回店接待顾客。");

            if (kind == "secondhand" && !shelves.Any(shelf => shop.Sim.ShelfFillOf(shelf).Total > 0))
            {
                bool hasOldGoods = _specialBusinesses.TryGetValue(shop.Place.Id, out var resale)
                    && resale.Used.Any(item => item.Owned && !item.Sold);
                return hasOldGoods
                    ? Step(QuestTarget.Counter, "在新店鉴定并上架旧物",
                        "打开「收购鉴定台」，鉴定已收下的旧物，再定价上架。修复中的旧物要等修完。", shop.Place)
                    : Step(QuestTarget.Counter, "在新店收购第一批旧物",
                        "营业时打开「收购鉴定台」，接待卖家。买下旧物后鉴定、定价，再上架。", shop.Place);
            }

            return FirstSale();

            QuestDef FirstSale() => Step(QuestTarget.Counter, "在新店完成第一单",
                kind == "restaurant" ? "营业时接待顾客，按订单备料、烹饪和上菜，最后在桌边收款。"
                    : "营业时留在店里，顾客排队后到收银台结账。也可以安排收银员值班。", shop.Place);

            QuestDef Supply(string next, string hint)
            {
                if (ShopSupplyMarketId(kind) is string marketId)
                    return Step(QuestTarget.Market, next, hint, PurchaseDestination(City.GetPlace(marketId)));
                return Step(QuestTarget.Stock, next, hint, shop.Place);
            }

            IEnumerable<CourierItem> IncomingItems() => Couriers
                .Where(run => run.ToShop && (run.ShopId ?? MainShop.Place.Id) == shop.Place.Id)
                .SelectMany(run => run.Items);
        }

        public string OpeningRewardText(string id)
        {
            if(id=="rent")return null;
            string kind=OpeningRewardKind(id)??RetailKind;
            if(!HasShop&&OpeningRewardKind(id)==null)return "按签约时选择的经营类型发放";
            return kind=="restaurant"?"大米 2 千克 + 青菜、鸡蛋、五花肉各 4 份"
                :kind=="clothing"?"当季基础服装两款，各 3 件"
                :kind=="models"?"常规模型两款，各 3 件"
                :kind=="secondhand"?"两件已鉴定的二手好货"
                :kind=="computer"?"入门 CPU + 入门显卡":kind==PetShopCatalog.ShopKind?"猫粮、狗粮、猫砂各 3 袋，逗猫棒、软球各 1 件":"本店品类商品两款，各 3 件";
        }

        internal void GrantOpeningReward(string id,bool legacy=false)
        {
            if(id=="rent"||OpeningRewardKind(id)!=null)return;
            string kind=RetailKind;
            Flags.Add("tutorial:reward:"+id+":"+kind);
            // Old computer saves already received their starter stock.
            if(legacy&&kind=="computer")return;
            if(kind=="restaurant")
            {
                UnpackRestaurantIngredient(CurrentShop, new CourierItem { Kind = CourierItemKind.Rice, Kg = 2 });
                foreach (string ingredient in RecipeIngredients)
                    UnpackRestaurantIngredient(CurrentShop, new CourierItem { Kind = CourierItemKind.Ingredient, Id = ingredient, Count = 4 });
            }
            else if(kind=="secondhand")
            {
                var state=SpecialBusiness(CurrentShop.Place.Id);
                for(int product=0;product<2;product++)
                {
                    var item=new UsedItem{Id=state.NextId++,Product=product,Owned=true,Inspected=true,Condition=UsedCondition.Good,Claim=UsedCondition.Good};
                    item.Price=UsedValue(item);state.Used.Add(item);
                }
            }
            else if(kind=="computer")
            {
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,1);
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu0"),true,1);
            }
            else if(kind==PetShopCatalog.ShopKind)
            {
                AddStarterPetStock();
            }
            else
            {
                var goods=kind=="clothing"?ClothingProducts.All.OrderByDescending(g=>ClothingProducts.IsInSeason(g.Id,Clock.Now.TotalMinutes)).ThenBy(g=>g.Price.Yuan)
                    :Gifts.All.Where(g=>RetailShopTypes.Sells(kind,g.Id)&&!g.LimitedEdition).OrderBy(g=>g.Price.Yuan);
                foreach(var good in goods.Take(2))PlayerShop.Stock.ForceAddGoods(good.Id,3);
            }
            if(legacy)Say("已补齐原开店任务的"+RetailShopTypes.Name(kind)+"礼包："+OpeningRewardText(id)+"。仅补发一次。");
        }

        private void AddStarterPetStock()
        {
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.CatFood, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.DogFood, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.BasicLitter, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.Wand, 1);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.Ball, 1);
        }

        private void MigrateOpeningRewards()
        {
            if(!HasShop||IsDedicatedVenue(CurrentShop.Place))return;
            if(QuestCompleted("mall")&&OpeningRewardKind("mall")==null)GrantOpeningReward("mall",true);
        }
    }
}
