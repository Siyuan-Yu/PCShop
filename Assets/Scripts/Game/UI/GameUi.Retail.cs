using System.Linq;
using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using UnityEngine.UI;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildRetailLinks(Transform parent)
        {
            Line(parent, "经营类型：" + RetailShopTypes.Name(S.RetailKind), 18, Ui.Accent);
            if(S.RetailKind=="restaurant")Line(parent,"堂食可加购已上架饮料 · 购买意愿+20个百分点",15,Ui.Dim);
            var row = Ui.Row(parent, 38);
            Ui.Size(Ui.Button(row, "进货员 · " + S.Employees.Count(x=>x.Shop==S.CurrentShop&&x.Role==EmployeeRole.Buyer) + " 人", OpenShopBuyers, 17, Ui.ButtonHot), -1, -1, 1).name="ShopBuyersEntry";
            Ui.Size(Ui.Button(row, "商品入库", OpenRetailListing, 17), -1, -1, 1);
        }
        private void OpenRetailListing()
        {
            OpenWorkspace("retail-storage","retail-storage","商品入库", 820, 700, w =>
            {
                if (!S.InPlayerShop) { w.Close(); return; }
                Para(w.Content, "将手边商品转入经营库存；货架按所选品类和容量自动陈列，余货留在后仓。", 18);
                var items = Gifts.All.Where(g => S.GoodsAvailableToList(g.Id) > 0).ToArray();
                var list = Ui.Scroll(w.Content, 510);
                if (items.Length == 0) Para(list, "手边暂无可入库商品。可先采购，或直接送货到本店后仓。", 18, Ui.Dim);
                foreach (var product in items)
                {
                    string id = product.Id;
                    int have = S.GoodsAvailableToList(id);
                    Line(list, product.Name + $" · 手边 {have} 件 · 每件 {product.WeightKg:0.##} 千克", 18, Ui.Accent);
                    var row = Ui.Row(list, 38);
                    Ui.Size(Ui.Button(row, "入库 1 件", () => { StoreRetailGoods(id,1); w.Rebuild(); RebuildDock(); }, 17), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "全部入库", () => { StoreRetailGoods(id,have); w.Rebuild(); RebuildDock(); }, 17, Ui.ButtonHot), -1, -1, 1);
                }
            });
        }
        private void StoreRetailGoods(string id,int count)
        {
            int before=S.PlayerShop.Stock.GoodsCount(id);
            if(!S.ListGoods(id,count))return;
            int stock=S.PlayerShop.Stock.GoodsCount(id),shown=S.PlayerShop.OnShelfGoods(id);
            Toast($"已入库 {stock-before} 件 · 本品陈列 {shown} 件 · 后仓 {Math.Max(0,stock-shown)} 件");
        }
        private void OpenRetailTypeChooser()
        {
            OpenWindow("本店主售品类",760,720,w=>
            {
                Para(w.Content,"主售影响客流与选购偏好，不限制陈列：顾客更倾向买主售商品，也会购买其他已陈列商品。",18,Ui.Dim);
                foreach(string id in RetailShopTypes.All)
                {
                    string kind=id;bool current=kind==S.RetailKind;
                    Line(w.Content,RetailShopTypes.Name(kind)+(current?" · 当前主售":""),21,current?Ui.Good:Ui.TextColor);
                    Para(w.Content,RetailShopTypes.Description(kind),16,Ui.Dim);
                    Ui.Size(Ui.Button(w.Content,current?"已选定":"主售"+RetailShopTypes.Name(kind),()=>
                    {if(S.SetShopMainCategory(kind)){w.Close();RebuildDock();RebuildWindows();}},18,current?Ui.ButtonBg:Ui.ButtonHot,!current),-1,40);
                }
            });
        }
        private void OpenRetailWholesale(Place source)
        {
            CloseStoreWindows();
            OpenPlaceWindow(source,BuildRetailWholesale,1120,960);
        }
        private void BuildRetailWholesale(Window w,Place source) => BuildCarryCatalog(w,source,"wholesale");
        private sealed class CarryOffer
        {
            public string Key,Name,Group,Detail,Season;
            public double Kg;
            public Money Price;
            public Func<bool> Add;
            public Func<double> Remaining;
            public Action Preview;
        }
        private static string FurniturePurchaseGroup(FurnitureDef item)
        {
            if(item.Role==FurnitureRole.Fridge || item.Role==FurnitureRole.Climate || item.Role==FurnitureRole.Cooker
                || item.Id=="shop_dispenser" || item.Id=="shop_led" || item.Id==GameSession.HomeWorkbenchId)return "电器";
            switch(item.Id)
            {
                case FurnitureCatalog.Curtain:
                case "rug": case "plant": case "poster": case "shop_plant": case "shop_poster":
                    return "装饰";
                default:return "非电器";
            }
        }
        private bool IsCarryStore(Place source) => source!=null && !S.IsMyShop(source) && !S.IsVacant(source)
            && S.VentureAt(source.Id)==null && !S.IsPlayerPropertyOccupied(source.Id)
            && (source.Kind==PlaceKind.Wholesaler || source.Kind==PlaceKind.Dealer || S.HasRetailWholesale(source)
                || S.IsCityClothingRetail(source) || source.Business==GameSession.MallBusiness || source.Business==GameSession.PhoneBusiness
                || source.Business==GameSession.FastFoodBusiness || source.Business==GameSession.FarmBusiness || source.Business==Gifts.TobaccoBusiness);
        private void CloseStoreWindows()
        {
            foreach(var window in _windows.ToArray())CloseWindowSilently(window);
        }
        private void BuildCarryStore(Window w,Place source)
        {
            bool wholesale=S.HasRetailWholesale(source) && !S.IsCityClothingRetail(source)
                && source.Business!=GameSession.FarmBusiness && source.Business!=Gifts.TobaccoBusiness
                && source.Business!=GameSession.FastFoodBusiness && source.Business!=GameSession.MallBusiness && source.Business!=GameSession.PhoneBusiness;
            BuildCarryCatalog(w,source,wholesale?"wholesale":"retail");
        }
        private void BuildCarryCatalog(Window w,Place source,string mode="retail")
        {
            bool wholesale=mode=="wholesale",expo=mode=="expo";
            var offers=new List<CarryOffer>();
            void Offer(string key,string name,string group,double kg,Money price,string detail,Func<bool> add,Func<double> remaining=null,Action preview=null,string season=null)
                =>offers.Add(new CarryOffer {Key=key,Name=name,Group=group,Kg=kg,Price=price,Detail=detail,Add=add,Remaining=remaining,Preview=preview,Season=season});
            if(expo)
            {
                foreach(var item in ModelProducts.Limited)
                {
                    var product=item;
                    Offer("expo:"+product.Id,product.Name,ModelProducts.SeriesName(product.ModelSeries),product.WeightKg,product.WholesalePrice,
                        product.Description+" · 本届每款限5件",()=>S.CartAddExpoModel(product.Id),()=>S.CityExpoModelRemaining(product.Id)-S.CartAmount("expo:"+product.Id));
                }
            }
            else if(wholesale)
            {
                foreach(var product in Gifts.All.Where(g=>S.CityRetailGoodsAvailable(source,g)))
                {
                    var item=product;string key="wholesale:"+item.Id;
                    Offer(key,item.Name,ShelfKinds.ForGift(item.Id)?.Name??"商品",item.WeightKg,S.CityRetailWholesalePrice(source,item),
                        "零售参考价 "+item.Price.Scale(IndustryCycles.GoodsFactor(item,S.Clock.Today.TotalDays))+" · "+S.WholesaleMinimumText(source.Id,item.Id)+" · 今日可购 "+S.CitySupplierRemaining(source.Id,item.Id)+(item.ShelfDays>0?" · 保鲜 "+item.ShelfDays+" 天":""),
                        ()=>S.CartAddRetailWholesale(source,item.Id,S.CartAmount(key)==0?S.CitySupplierMinimum(source.Id,item.Id):1),()=>Math.Min(100,S.CitySupplierRemaining(source.Id,item.Id))-S.CartAmount(key),
                        season:ClothingProducts.SeasonGroup(item.Id));
                }
            }
            else if(source.Kind==PlaceKind.Wholesaler)
            {
                var stalls = S.WholesaleStallsAt(source).ToArray();
                foreach (var part in PartCatalog.All)
                foreach (var stall in stalls.Where(s => S.StallSells(s, part)))
                {
                    string key = S.StallCartKey(stall, part);
                    string supplier = GameSession.StallSupplierName(stall);
                    Offer(key, part.Name, PartCatalog.CategoryName(part.Category), PartUnitKg(part), S.StallPrice(stall, part),
                        $"{supplier} · 今日可购 {S.StallRemaining(stall, part)}/{S.SupplierCapacity(stall.Id, part.Id)} · 市价 {S.Market.PriceOf(part)}",
                        () => S.CartAddStall(stall, part, 1), () => S.StallRemaining(stall, part) - S.CartAmount(key));
                }
            }
            else if(S.IsCityClothingRetail(source))
            {
                foreach(var outfit in S.CityClothingSelection(source))
                {
                    var item=outfit;
                    Offer("outfit:"+item.Id,item.Name,item.TierName,item.WeightKg,item.Price,"适温 "+item.ComfortMinC+"—"+item.ComfortMaxC+"℃ · 购后加入个人衣柜",
                        ()=>S.CartAddOutfit(source,item.Id),()=>S.OwnedOutfits.Contains(item.Id)?0:Math.Max(0,1-S.CartAmount("outfit:"+item.Id)),
                        ()=>{_cityClothingPreview=item.Id;OpenWindow("试穿 · "+item.Name,900,500,CityClothingMirror);});
                }
            }
            else if(source.Kind==PlaceKind.Dealer)
            {
                foreach(var vehicle in Vehicles.All.Where(v=>v.Purchasable))
                {
                    var item=vehicle;
                    Offer("vehicle:"+item.Kind,item.Name,"车辆",0,item.Price,"载具，不装背包 · "+S.VehicleUsage(item.Kind)
                        +" · 载货 "+(item.CargoCapacity*Backpack.KgPerSpace)+" 千克"+(item.Illegal?" · 城里禁止上路，交警会追":""),()=>S.CartAddVehicle(item.Kind),()=>Math.Max(0,20-S.CartAmount("vehicle:"+item.Kind)));
                }
            }
            else if(source.Business==GameSession.MallBusiness)
            {
                foreach(var item in FurnitureCatalog.Purchasable(false).Concat(FurnitureCatalog.Purchasable(true)).GroupBy(f=>f.Id).Select(g=>g.First()))
                {
                    var product=item;
                    Offer("furn:"+product.Id,product.Name,FurniturePurchaseGroup(product),product.WeightKg,product.Price,product.Description,()=>S.CartAddFurniture(product.Id));
                }
            }
            else if(source.Business==GameSession.FastFoodBusiness)
            {
                foreach(var food in GameSession.FastFoodMenu)
                {
                    var item=food;
                    Offer("fast:"+item.Id,item.Name,item.Drink?"饮料":"食物",item.WeightKg,item.Price,item.Effects+" · 保鲜 "+item.ShelfDays+" 天 · 买后带走，稍后吃喝",
                        ()=>S.CartAddFastFood(item.Id),()=>100-S.CartAmount("fast:"+item.Id));
                }
            }
            else if(source.Business==GameSession.PhoneBusiness)
                Offer("phone","智能手机","手机",0,GameSession.PhonePrice,"随身设备，不计背包负重；可安装日历、网购等应用。",S.CartAddSmartphone,
                    ()=>S.HasSmartphone?0:Math.Max(0,1-S.CartAmount("phone")));
            else
            {
                foreach(var product in Gifts.All.Where(g=>g.SoldAt==source.Business && !g.LimitedEdition && (source.Business!=GameSession.FarmBusiness || !RetailProducts.IsFood(g))))
                {
                    var item=product;
                    Offer("gift:"+item.Id,item.Name,ShelfKinds.ForGift(item.Id)?.Name??"商品",item.WeightKg,item.Price,item.Description,()=>S.CartAddGift(item.Id));
                }
            }
            if(!wholesale && !expo && source.Business==GameSession.FarmBusiness)
            {
                Offer("rice","大米","米",.25,Pantry.RicePricePerKg.Scale(.25),"每次加购0.25千克 · "+Pantry.RicePricePerKg+"/千克",()=>S.CartAddRice(.25));
                foreach(var ingredient in Ingredients.All)
                {
                    var item=ingredient;
                    Offer("ing:"+item.Id,item.Name,"菜蛋肉",item.WeightKg,item.PricePerKg.Scale(item.WeightKg),"可焖「"+item.DishName+"」 · 保鲜 "+item.ShelfDays+" 天",()=>S.CartAddIngredient(item.Id,1));
                }
            }
            if(!wholesale && !expo)
                foreach(var upgrade in ShopUpgrades.SoldBy(source.Business))
                {
                    var item=upgrade;
                    Offer("upgrade:"+item.Id,item.Name,source.Business==GameSession.MallBusiness?"电器":"店铺设备",0,item.Price,item.Description+" · 购后安装到门店，不占货运空间",()=>S.CartAddUpgrade(item.Id),
                        ()=>S.HasUpgrade(item.Id)?0:Math.Max(0,1-S.CartAmount("upgrade:"+item.Id)));
                }
            Line(w.Content,"商品购买 · "+(S.IsPlaceOpen(source)?"营业中 · ":"已打烊 · ")+S.OpeningHoursText(source),19,Ui.Accent);
            if (source.Kind == PlaceKind.Wholesaler)
                Para(w.Content, "按配件分类选货，不同档口的商品可以一起结账。每个档口单独计算每日可售数量。", 16, Ui.Dim);
            var missing=S.HasShop && source.Business==GameSession.MallBusiness && !S.QuestCompleted("mall")
                ? S.MissingOpeningFurniture() : Array.Empty<string>();
            if (missing.Length>0)
            {
                Line(w.Content,"开店待买 · "+string.Join("、",missing.Select(id=>FurnitureCatalog.Get(id).Name+" ×1")),16,Ui.Accent);
            }
            Para(w.Content,"选择商品数量，加入购物车后统一结账。可携带商品优先放背包，也可叫车；设备和车辆直接登记为已购资产。",16,Ui.Dim);
            var links=Ui.Row(w.Content,36,6);
            if(wholesale && (source.Business==GameSession.FarmBusiness || source.Business==Gifts.TobaccoBusiness))
                Ui.Size(Ui.Button(links,"切回零售",()=>{CloseStoreWindows();OpenPlaceWindow(source,BuildCarryStore,1120,960);},16),120,36);
            if(S.IsCityClothingRetail(source))Ui.Size(Ui.Button(links,"我的衣柜",OpenCityWardrobe,16),120,36);
            if(source.Kind==PlaceKind.Dealer)Ui.Size(Ui.Button(links,"已购车辆 / 赎回",OpenCarryVehicleManagement,16),180,36);
            if(source.Business==GameSession.MallBusiness && S.MallStashCount>0)
                Ui.Size(Ui.Button(links,"取寄存的货",()=>OpenWindow("家具城寄存",1000,760,detail=>BuildMallStash(detail,detail.Content)),16),150,36);
            if(links.childCount==0)links.gameObject.SetActive(false);
            var tabs=source.Business==GameSession.MallBusiness
                ?new List<string>{"全部","电器","非电器","装饰"}
                :new[]{"全部"}.Concat(offers.Select(o=>o.Group)).Distinct().ToList();
            if(missing.Length>0)tabs.Insert(0,"开店清单");
            bool clothingWholesale=wholesale && offers.Any(o=>o.Season!=null);
            string tab=ShopTabs(w,source,tabs,label:clothingWholesale?"品质":null);
            string season=clothingWholesale?ShopTabs(w,source,new[]{"全部","春秋装","夏装","冬装"},filterKey:"season",label:"季节"):"全部";
            if(clothingWholesale)Line(w.Content,"当前"+CityWeather.Season(S.Clock.Now.TotalMinutes)+"季 · 应季服装需求较多，反季服装仍有需求。",16,Ui.Dim);
            var list=GoodsList(w.Content);
            var visible=offers.Where(o=>(tab=="全部" || o.Group==tab
                || tab=="开店清单" && missing.Any(id=>o.Key=="furn:"+id)) && (season=="全部" || o.Season==season)).ToList();
            if(visible.Count==0)Para(list,offers.Count==0?"这里暂时没有可购买的商品。":"当前筛选下没有商品，可切换品质或季节。",18,Ui.Dim);
            foreach(var product in visible)
            {
                var item=product;double remaining=item.Remaining?.Invoke()??double.PositiveInfinity;
                var row=Ui.Row(list,84,10);
                string detail=item.Season==null?item.Detail:item.Season+" · "+item.Detail;
                string label=item.Kg>0?ProductLabel(item.Name,item.Kg,detail):item.Name+"\n<size=14>登记为已购资产，不占背包或货车空间</size>\n<size=14>"+detail+"</size>";
                Ui.Size(ProductText(row,label,17),-1,-1,1);
                Ui.Size(Ui.Label(row,item.Price.ToString(),19,TextAnchor.MiddleLeft,Ui.Good),100);
                if(item.Preview!=null)Ui.Size(Ui.Button(row,"试穿",item.Preview,16),68,38);
                CartAddButton(w,row,item.Key,item.Add,"+1",58,S.IsPlaceOpen(source)&&remaining>0,maxAmount:
                    double.IsPositiveInfinity(remaining)?1000:Math.Max(S.CartAmount(item.Key),S.CartAmount(item.Key)+remaining));
                if((wholesale||expo||source.Kind==PlaceKind.Wholesaler)&&remaining>0&&!double.IsInfinity(remaining))
                    Ui.Size(Ui.Button(row,"最大",()=>{
                        for(int n=0;n<1000;n++){double before=S.CartAmount(item.Key);if((item.Remaining?.Invoke()??0)<=0)break;item.Add();if(S.CartAmount(item.Key)<=before)break;}
                        w.RequestRebuild();
                    },15),58);
                if(remaining<=0)Ui.Size(Ui.Label(row,"已选满 / 已拥有",14,TextAnchor.MiddleLeft,Ui.Dim),106);
            }
            CartPanel(w,w.Content,wholesale||expo||source.Kind==PlaceKind.Wholesaler);
        }
        private void OpenCarryVehicleManagement()
        {
            OpenWindow("已购车辆与赎回",760,650,w=>
            {
                foreach(var vehicle in Vehicles.All.Where(v=>v.Purchasable && S.Owns(v.Kind)))
                {
                    var item=vehicle;
                    Line(w.Content,item.Name+" · "+S.VehicleUsage(item.Kind),19);
                    Ui.Size(Ui.Button(w.Content,"出售一台（购价六折）",()=>ConfirmBusiness("出售载具",item.Name+"按购价六折回收。",()=>{S.SellVehicle(item.Kind);w.Rebuild();}),17,Ui.Warn),-1,40);
                }
                foreach(var kind in new[]{VehicleKind.EBike,VehicleKind.Tricycle})
                {
                    var selected=kind;if(S.ImpoundedVehicleCount(kind)<=0)continue;
                    Ui.Size(Ui.Button(w.Content,"交 "+PoliceSystem.RedeemFee+" 赎回"+Vehicles.Get(kind).Name,()=>{S.RedeemVehicle(selected);w.Rebuild();},17),-1,40);
                }
            });
        }
    }
}
