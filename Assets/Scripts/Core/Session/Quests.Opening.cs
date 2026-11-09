using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
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

        private static readonly string[] RestaurantOpeningFurniture =
        {
            FurnitureCatalog.ShopCounter, "restaurant_stove",
            "restaurant_table", FurnitureCatalog.FridgeSmall
        };

        public static string[] OpeningFurniture(string kind)=>kind=="restaurant"
            ?(string[])RestaurantOpeningFurniture.Clone()
            :kind=="secondhand"?new[]{"used_buydesk","used_display",FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter}
            :kind==PetShopCatalog.ShopKind?new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter,PetShopCatalog.SmallHouse}
            :new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter};

        private static bool MatchesOpeningFurniture(string id, FurnitureDef item) => item != null && (item.Id == id
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

        private string OpeningRewardKind(string id)=>Flags.FirstOrDefault(f=>f.StartsWith("tutorial:reward:"+id+":"))?.Substring(("tutorial:reward:"+id+":").Length);
        public string[] MissingOpeningFurniture()
        {
            if (!HasShop) return Array.Empty<string>();
            return OpeningFurniture(RetailKind).Where(id => !OpeningFurniturePlaced(_focus, id)
                && !FurnitureCatalog.All.Any(item => MatchesOpeningFurniture(id, item) && FurnitureOwnedUnplaced(item.Id) > 0)).ToArray();
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
                var food=CurrentShop.Locker.Food;food.RiceKg+=2;
                foreach(string ingredient in RecipeIngredients)
                    food.AddIngredient(ingredient, 4);
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
