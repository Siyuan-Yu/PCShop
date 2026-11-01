using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;
namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly Dictionary<string,string> _cityClothingPresets=new Dictionary<string,string>();
        public bool CityClothingAuto { get; private set; }
        public string CityClothingOccasion { get; private set; }="daily";
        public OutfitDef CurrentClothing => Outfit;
        public double OutfitComfortMin => CurrentClothing.ComfortMinC;
        public double OutfitComfortMax => CurrentClothing.ComfortMaxC;
        public bool CanChangeCityClothing => Trip==null && !IsWorking && !CityActivityExclusive &&
            (CurrentPlace==Home&&HomeHas(FurnitureRole.Wardrobe) || CurrentPlace!=null &&
             (IsCityClothingRetail(CurrentPlace)&&IsPlaceOpen(CurrentPlace) || MyShops.Any(s=>s.Place==CurrentPlace&&s.Layout.Any(f=>f.Id=="fitting_mirror"))));
        public bool IsCityClothingRetail(Place p)=>p!=null&&(p.Id=="shop02"||p.Id=="clothing_boutique")&&!IsMyShop(p)&&!IsVacant(p)&&VentureAt(p.Id)==null;
        public IReadOnlyList<OutfitDef> CityClothingSelection(Place p)=>Outfits.All.Where(o=>o.Id!=Outfits.Default &&
            (p?.Id=="clothing_boutique"?o.Tier>=2:p?.Id=="shop02"?o.Tier<=1:false)).ToArray();
        public bool BuyCityClothing(Place shop,string outfitId)
        {
            var o=Outfits.Get(outfitId);
            if(IsGameOver||IsWorking||CityActivityExclusive||CurrentPlace!=shop||!IsCityClothingRetail(shop)||!IsPlaceOpen(shop)||!CityClothingSelection(shop).Contains(o))return Fail("请到营业中的服装店柜台购买。");
            if(_ownedOutfits.Contains(outfitId))return Fail("衣柜已拥有同款，无需再次购买。");
            if(_parcels.Any(p=>p.Kind==ParcelKind.Outfit&&p.Id==outfitId))return Fail("同款网购衣物已经在途，无需重复购买。");
            if(!Ledger.TryDebit(Clock.Today,"个人衣物",o.Price,o.Name,new LedgerAttribution(LedgerBusinessKind.Personal,transactionKind:"服装零售")))return Fail("现金不足，仍可免费试穿。");
            _ownedOutfits.Add(outfitId);Say("买下了"+o.Name+"，已加入个人衣柜。可在试衣镜旁换上。");return true;
        }
        public bool UseCityShopClothing(string shopId,string outfitId)
        {
            var shop=ShopAt(shopId);var o=Outfits.Get(outfitId);
            if(shop==null||o==null||CurrentPlace!=shop.Place||IsWorking||CityActivityExclusive)return Fail("请到自己的店内办理库存转自用。");
            if(_ownedOutfits.Contains(outfitId))return Fail("已拥有同款，没有消耗库存。");
            if(!shop.Sim.Stock.TryRemoveGoods(o.ProductId,1))return Fail("没有未预留现货；顾客手上和在途的衣服不可转自用。");
            var costs=Ledger.Entries.Where(e=>e.ShopId==shopId&&e.Amount.IsNegative&&(e.Attribution?.ProductId==o.ProductId||e.Attribution?.ProductId=="g:"+o.ProductId)&&e.Attribution.Quantity>0&&e.Attribution.UnitCostCents>0).ToArray();
            long qty=costs.Sum(e=>(long)e.Attribution.Quantity);
            long unitCost=qty>0?costs.Sum(e=>e.Attribution.UnitCostCents*e.Attribution.Quantity)/qty:0;
            _ownedOutfits.Add(outfitId);
            Ledger.RecordNonCash(Clock.Today,"库存转自用",o.Name+"×1；"+(unitCost>0?"按已记录采购成本归属":"旧库存成本未知，未伪造金额"),
                new LedgerAttribution(LedgerBusinessKind.InternalTransfer,shopId:shopId,transactionKind:"个人衣物领用").Product(o.ProductId,1,unitCost));
            Say("已从经营库存取出1套"+o.Name+"归个人使用；无现金交易，不计销售。");return true;
        }
        public string CityClothingPresetFor(string occasion)=>_cityClothingPresets.TryGetValue(occasion,out var id)?id:null;
        public bool SetCityClothingPreset(string occasion,string outfitId)
        {
            if(!new[]{"daily","business","sport"}.Contains(occasion)||!_ownedOutfits.Contains(outfitId))return false;
            _cityClothingPresets[occasion]=outfitId;return true;
        }
        public void ClearCityClothingPresets()=>_cityClothingPresets.Clear();
        public void SetCityClothingAuto(bool enabled,string occasion="daily")
        { CityClothingAuto=false;CityClothingOccasion="daily"; }
        public OutfitDef RecommendCityClothing(double temperature,string occasion="daily")=>Outfits.All.Where(o=>_ownedOutfits.Contains(o.Id))
            .OrderBy(o=>Math.Max(0,Math.Max(o.ComfortMinC-temperature,temperature-o.ComfortMaxC)))
            .ThenBy(o=>Math.Abs((o.ComfortMinC+o.ComfortMaxC)/2-temperature))
            .ThenBy(o=>o.Id==EquippedOutfit?0:1).ThenBy(o=>o.Price.Cents).First();
        public bool EquipForCurrentWeather()
        {
            var temperature=CityOutdoorTemperature(Clock.Now.TotalMinutes);
            var outfit=RecommendCityClothing(temperature);
            if(!Equip(outfit.Id))return false;
            bool comfortable=temperature>=outfit.ComfortMinC&&temperature<=outfit.ComfortMaxC;
            Say("已换上"+outfit.Name+"。室外 "+temperature.ToString("0.0")+"℃"+
                (comfortable?"，适合当前天气。":"；这是已拥有衣服中最接近当前天气的一套。"));
            return true;
        }
        // Kept for older callers and saves. Dressing is now always a manual wardrobe choice.
        public void PrepareCityClothingDeparture(double temperature) { }
        public double SeasonDemandAt(string productId,double temperature)=>ClothingProducts.SeasonDemandAt(productId,temperature);
        public string CityClothingReaction(string personId)
        {
            if(!CitySamePlace(personId))return null;var p=CityPerson(personId);if(p==null)return null;
            if((p.Interest??"").Contains("运动")||(p.Interest??"").Contains("户外"))
                return CurrentClothing.IsOccasion("sport")?"这身活动起来方便，待会儿练球也合适。":"一起活动穿得舒服就好，不用专门买贵衣服。";
            if((p.Interest??"").Contains("创作")||(p.Interest??"").Contains("模型"))
                return "你这套"+CurrentClothing.FictionalBrandId+"的轮廓挺有意思。喜欢的风格比价格更重要。";
            if(CurrentClothing.IsOccasion("business")&&(p.Role??"").Contains("经理"))
                return "今天穿得挺利落。合作还是要看交付、资金与履约。";
            return null;
        }
        public void SaveCityClothing(SaveData d)
        { d.cityClothingVersion=1;d.cityClothingAuto=false;d.cityClothingOccasion=CityClothingOccasion;d.cityClothingPresets=Array.Empty<CityClothingPreset>(); }
        public void LoadCityClothing(SaveData d)
        {
            _cityClothingPresets.Clear();
            SetCityClothingAuto(d.cityClothingVersion>0&&d.cityClothingAuto,d.cityClothingOccasion);
        }
    }
}
