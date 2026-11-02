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
            bool first=!HasShop,choose=(first||newShop)&&!IsDedicatedVenue(City.GetPlace(placeId));
            if(choose)
            {
                Flags.Add(OpeningChoicePrefix+placeId);
                if(first)Flags.Add("shop:choose-first:"+placeId);
            }
            if(SignLease(placeId,newShop))return true;
            if(choose){Flags.Remove(OpeningChoicePrefix+placeId);Flags.Remove("shop:choose-first:"+placeId);}
            return false;
        }
        public string OpeningKindBlocker(string placeId,string kind)=>!RetailShopTypes.All.Contains(kind)?"请选择经营类型"
            :Flags.Contains("shop:choose-first:"+placeId)?null:RetailTypeBlocker(kind);
        public bool ConfirmOpeningKind(string placeId,string kind)
        {
            if(PendingShopKindPlaceId!=placeId)return Fail("这间店已完成经营类型确认。");
            string blocker=OpeningKindBlocker(placeId,kind);if(blocker!=null)return Fail(blocker);
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

        public static string[] OpeningFurniture(string kind)=>kind=="restaurant"
            ?new[]{"restaurant_prep","restaurant_stove","restaurant_pass","restaurant_table",FurnitureCatalog.Fridge}
            :kind=="secondhand"?new[]{"used_buydesk","used_display",FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter}
            :new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter};

        private string OpeningRewardKind(string id)=>Flags.FirstOrDefault(f=>f.StartsWith("tutorial:reward:"+id+":"))?.Substring(("tutorial:reward:"+id+":").Length);
        public string OpeningRewardText(string id)
        {
            string kind=OpeningRewardKind(id)??RetailKind;
            if(!HasShop&&OpeningRewardKind(id)==null)return "按签约时选择的经营类型发放";
            if(id=="rent")return string.Join(" + ",OpeningFurniture(kind).Select(f=>FurnitureCatalog.Get(f).Name+" ×1"));
            return kind=="restaurant"?"大米 2 千克 + 青菜、鸡蛋、五花肉各 4 份"
                :kind=="clothing"?"当季基础服装两款，各 3 件"
                :kind=="models"?"常规模型两款，各 3 件"
                :kind=="secondhand"?"两件已鉴定的二手好货"
                :kind=="computer"?"入门 CPU + 入门显卡":"本店品类商品两款，各 3 件";
        }

        internal void GrantOpeningReward(string id,bool legacy=false)
        {
            if(OpeningRewardKind(id)!=null)return;
            string kind=RetailKind;
            Flags.Add("tutorial:reward:"+id+":"+kind);
            // Old computer rewards already exist. Other old businesses receive their missing opening equipment once.
            if(legacy&&kind=="computer")return;
            if(id=="rent")
            {
                foreach(string furniture in OpeningFurniture(kind))
                    if(!legacy||FurnitureOwnedUnplaced(furniture)+MyShops.Sum(s=>s.Layout.Count(p=>p.Id==furniture))==0)
                        AddFurniture(furniture,1);
            }
            else if(kind=="restaurant")
            {
                var food=CurrentShop.Locker.Food;food.RiceKg+=2;
                foreach(string ingredient in RecipeIngredients)
                {
                    var def=Ingredients.Get(ingredient);
                    food.Ingredient.Add(new FoodBatch{Id=ingredient,Name=def.Name,Count=4,ExpireDay=Clock.Today.TotalDays+def.ShelfDays});
                }
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
            else
            {
                var goods=kind=="clothing"?ClothingProducts.All.OrderByDescending(g=>ClothingProducts.IsInSeason(g.Id,Clock.Now.TotalMinutes)).ThenBy(g=>g.Price.Yuan)
                    :Gifts.All.Where(g=>RetailShopTypes.Sells(kind,g.Id)&&!g.LimitedEdition).OrderBy(g=>g.Price.Yuan);
                foreach(var good in goods.Take(2))PlayerShop.Stock.ForceAddGoods(good.Id,3);
            }
            if(legacy)Say("已补齐原开店任务的"+RetailShopTypes.Name(kind)+"礼包："+OpeningRewardText(id)+"。仅补发一次。");
        }
        private void MigrateOpeningRewards()
        {
            if(!HasShop||IsDedicatedVenue(CurrentShop.Place))return;
            foreach(string id in new[]{"rent","mall"})
                if(QuestCompleted(id)&&OpeningRewardKind(id)==null)GrantOpeningReward(id,true);
        }
    }
}
