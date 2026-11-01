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
            var row = Ui.Row(parent, 38);
            Ui.Size(Ui.Button(row, "进货渠道", OpenRetailSources, 17, Ui.ButtonHot), -1, -1, 1);
            Ui.Size(Ui.Button(row, "背包/纸箱商品上架", OpenRetailListing, 17), -1, -1, 1);
            Ui.Size(Ui.Button(row, "主售品类", OpenRetailTypeChooser, 17), -1, -1, 1);
        }
        private void OpenRetailListing()
        {
            OpenWindow("商品上架", 820, 700, w =>
            {
                if (!S.InPlayerShop) { w.Close(); return; }
                Para(w.Content, "将背包或本店纸箱里的商品放入后仓；摆好并设置相应品类的货架后，顾客才能买到。", 18);
                var items = Gifts.All.Where(g => S.GoodsAvailableToList(g.Id) > 0 && RetailShopTypes.Sells(S.RetailKind, g.Id)).ToArray();
                var list = Ui.Scroll(w.Content, 510);
                if (items.Length == 0) Para(list, "手边没有可上架的商品。可先去批发进货，或让采购员直接送入后仓。", 18, Ui.Dim);
                foreach (var product in items)
                {
                    string id = product.Id;
                    int have = S.GoodsAvailableToList(id);
                    Line(list, product.Name + $" · 手边 {have} 件 · 每件 {product.WeightKg:0.##} 千克", 18, Ui.Accent);
                    var row = Ui.Row(list, 38);
                    Ui.Size(Ui.Button(row, "上架 1 件", () => { S.ListGoods(id, 1); w.Rebuild(); RebuildDock(); }, 17), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "全部上架", () => { S.ListGoods(id, have); w.Rebuild(); RebuildDock(); }, 17, Ui.ButtonHot), -1, -1, 1);
                }
            });
        }
        private void OpenRetailTypeChooser()
        {
            OpenWindow("本店主售品类",760,720,w=>
            {
                Para(w.Content,"选择本店主要销售什么。之后进店的顾客按这个品类挑选商品；已有库存和已接订单保留，尚未结账的顾客会先离店。",18,Ui.Dim);
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
        private void OpenRetailSources()
        {
            OpenWindow("进货渠道", 820, 720, w =>
            {
                Para(w.Content, "去对应店铺打开“批发进货”，可放背包带回上架，也可叫货车送入指定店铺后仓。员工管理的采购清单同样包含这些货源。", 18);
                var list = Ui.Scroll(w.Content, 520);
                foreach (var p in S.City.Places.Where(p => S.HasRetailWholesale(p)))
                {
                    var source = p;
                    Line(list, source.Name + " · " + S.OpeningHoursText(source), 19, Ui.Accent);
                    Para(list, string.Join("、", Gifts.All.Where(g => S.CityRetailGoodsAvailable(source, g)).Select(g => g.Name)), 16);
                    Ui.Size(Ui.Button(list, "前往进货", () => { CloseAllWindows(); _ctrl.GoToPlace(source); }, 17), -1, 36);
                }
                Ui.Size(Ui.Button(w.Content, "安排采购员", OpenEmployeesPanel, 18, Ui.ButtonHot), -1, 42);
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
            public string Key,Name,Group,Detail;
            public double Kg;
            public Money Price;
            public Func<bool> Add;
            public Func<double> Remaining;
            public Action Preview;
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
            void Offer(string key,string name,string group,double kg,Money price,string detail,Func<bool> add,Func<double> remaining=null,Action preview=null)
                =>offers.Add(new CarryOffer {Key=key,Name=name,Group=group,Kg=kg,Price=price,Detail=detail,Add=add,Remaining=remaining,Preview=preview});
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
                        "零售参考价 "+item.Price+" · 起订 "+S.CitySupplierMinimum(source.Id)+" 件"+(item.ShelfDays>0?" · 保鲜 "+item.ShelfDays+" 天":""),
                        ()=>S.CartAddRetailWholesale(source,item.Id),()=>Math.Min(100,S.CitySupplierRemaining(source.Id,item.Id))-S.CartAmount(key));
                }
            }
            else if(source.Kind==PlaceKind.Wholesaler)
            {
                foreach(var part in PartCatalog.All.Where(item=>S.StallSells(source,item)))
                {
                    var item=part;
                    Offer("part:"+item.Id,item.Name,PartCatalog.CategoryName(item.Category),PartUnitKg(item),S.StallPrice(source,item),
                        "市价 "+S.Market.PriceOf(item)+" · 盒装正品",()=>S.CartAddStall(source,item,1),()=>S.StallRemaining(source,item)-S.CartAmount("part:"+item.Id));
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
                    Offer("furn:"+product.Id,product.Name,product.IsFixture?"店面设施":product.ForShop?"店里装饰":"家里用",product.WeightKg,product.Price,product.Description,()=>S.CartAddFurniture(product.Id));
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
                Offer("phone","智能手机","手机",0,GameSession.PhonePrice,"随身设备，不计背包负重；可安装日历、1788等应用。",S.CartAddSmartphone,
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
                    Offer("upgrade:"+item.Id,item.Name,"店铺设备",0,item.Price,item.Description+" · 购后安装到门店，不占货运空间",()=>S.CartAddUpgrade(item.Id),
                        ()=>S.HasUpgrade(item.Id)?0:Math.Max(0,1-S.CartAmount("upgrade:"+item.Id)));
                }
            Line(w.Content,"商品购买 · "+(S.IsPlaceOpen(source)?"营业中 · ":"已打烊 · ")+S.OpeningHoursText(source),19,Ui.Accent);
            Para(w.Content,"选择商品数量，加入购物车后统一结账。可携带商品优先放背包，也可叫车；设备和车辆直接登记为已购资产。",16,Ui.Dim);
            var links=Ui.Row(w.Content,36,6);
            if(wholesale && (source.Business==GameSession.FarmBusiness || source.Business==Gifts.TobaccoBusiness))
                Ui.Size(Ui.Button(links,"切回零售",()=>{CloseStoreWindows();OpenPlaceWindow(source,BuildCarryStore,1120,960);},16),120,36);
            if(S.IsCityClothingRetail(source))Ui.Size(Ui.Button(links,"我的衣柜",OpenCityWardrobe,16),120,36);
            if(source.Kind==PlaceKind.Dealer)Ui.Size(Ui.Button(links,"已购车辆 / 赎回",OpenCarryVehicleManagement,16),180,36);
            if(source.Business==GameSession.MallBusiness && S.MallStashCount>0)
                Ui.Size(Ui.Button(links,"取寄存的货",()=>OpenWindow("家具城寄存",1000,760,detail=>BuildMallStash(detail,detail.Content)),16),150,36);
            if(links.childCount==0)links.gameObject.SetActive(false);
            var tabs=offers.Select(o=>o.Group).Distinct().ToList();string tab=tabs.Count>1?ShopTabs(w,source,tabs):tabs.FirstOrDefault();
            var list=GoodsList(w.Content);
            if(offers.Count==0)Para(list,"这里暂时没有可购买的商品。",18,Ui.Dim);
            foreach(var product in offers.Where(o=>o.Group==tab))
            {
                var item=product;double remaining=item.Remaining?.Invoke()??double.PositiveInfinity;
                var row=Ui.Row(list,84,10);
                string label=item.Kg>0?ProductLabel(item.Name,item.Kg,item.Detail):item.Name+"\n<size=14>登记为已购资产，不占背包或货车空间</size>\n<size=14>"+item.Detail+"</size>";
                Ui.Size(ProductText(row,label,17),-1,-1,1);
                Ui.Size(Ui.Label(row,item.Price.ToString(),19,TextAnchor.MiddleLeft,Ui.Good),100);
                if(item.Preview!=null)Ui.Size(Ui.Button(row,"试穿",item.Preview,16),68,38);
                CartAddButton(w,row,item.Key,item.Add,"+1",58,S.IsPlaceOpen(source)&&remaining>0,maxAmount:
                    double.IsPositiveInfinity(remaining)?1000:Math.Max(S.CartAmount(item.Key),S.CartAmount(item.Key)+remaining));
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
