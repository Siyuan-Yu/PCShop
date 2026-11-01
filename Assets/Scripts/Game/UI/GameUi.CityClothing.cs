using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _cityClothingPreview;
        private bool _cityClothingDetails;
        private int _cityClothingPage;
        public void OpenCityWardrobe()
        {
            _cityClothingPreview=S.EquippedOutfit;_cityClothingPage=0;
            OpenWindow("个人衣柜",1000,760,BuildCityWardrobe);
        }
        private void CityClothingMirror(Window w)
        {
            var o=Outfits.Get(_cityClothingPreview)??S.Outfit;
            _avatarPreview=_avatarPreview??new AvatarPreview();_avatarPreview.Render(o.Id);
            var row=Ui.Row(w.Content,240,12);
            var img=Ui.Rect(row,"ClothingPreview").gameObject.AddComponent<RawImage>();img.texture=_avatarPreview.Texture;img.raycastTarget=false;Ui.Size(img,184,234);
            Ui.Size(Ui.Label(row,$"<b>{o.Name}</b> · {o.FictionalBrandId} / {o.TierName}\n适温 {o.ComfortMinC:0}—{o.ComfortMaxC:0}℃\n{o.Description}\n\n当前装备：{S.Outfit.Name}\n预览仅试穿；不会购买、换装或增加所有权。\n衣着魅力 +{o.CharmBonus}（总魅力只计算已装备一套）",17,TextAnchor.MiddleLeft),-1,234,1);
            var actions=Ui.Row(w.Content,40,8);
            Ui.Size(Ui.Button(actions,"取消试穿 / 还原",()=>{_cityClothingPreview=S.EquippedOutfit;w.Rebuild();},17),-1,40,1);
            Ui.Size(Ui.Button(actions,"换上已拥有这套",()=>{S.Equip(o.Id);w.Rebuild();},17,Ui.ButtonHot,S.CanChangeCityClothing&&S.OwnedOutfits.Contains(o.Id)&&o.Id!=S.EquippedOutfit),-1,40,1);
            if(!S.CanChangeCityClothing)Line(w.Content,"查看和试穿不受位置限制；实际更衣请到家中衣柜或营业店试衣镜旁。",16,Ui.Warn);
        }
        private void BuildCityWardrobe(Window w)
        {
            double temp=S.CityOutdoorTemperature(S.Clock.Now.TotalMinutes);
            Line(w.Content,$"室外 {temp:0.0}℃ · 当前穿着：{S.Outfit.Name}",18,Ui.Accent);
            var weatherOutfit=S.RecommendCityClothing(temp);
            Ui.Size(Ui.Button(w.Content,"换上适合当前天气的衣服 · "+weatherOutfit.Name,()=>
            {
                if(S.EquipForCurrentWeather())_cityClothingPreview=S.EquippedOutfit;
                w.Rebuild();
            },17,Ui.ButtonHot,S.CanChangeCityClothing),-1,42);
            CityClothingMirror(w);
            Ui.Size(Ui.Button(w.Content,_cityClothingDetails?"收起详情":"展开服装详情",()=>{_cityClothingDetails=!_cityClothingDetails;w.Rebuild();},16),-1,34);
            var outfits=Outfits.All.Where(o=>S.OwnedOutfits.Contains(o.Id)).ToArray();
            CityClothingRows(w,outfits,false,null);
            foreach(var shop in S.MyShops.Where(s=>s.Place==S.CurrentPlace))
            {
                var own=shop;
                foreach(var line in own.Sim.Stock.GoodsLines().Where(l=>ClothingProducts.IsClothing(l.Key)&&l.Value>0))
                {
                    var o=ClothingProducts.Outfit(line.Key);if(S.OwnedOutfits.Contains(o.Id))continue;
                    Ui.Size(Ui.Button(w.Content,$"库存转自用：{o.Name}（未预留 {line.Value} 件）",()=>ConfirmBusiness("从库存领用个人衣物",o.Name+"：消耗1件现货，不产生销售收入、不发生自买自卖。已拥有同款不会重复消耗。",()=>{S.UseCityShopClothing(own.Place.Id,o.Id);w.Rebuild();}),16),-1,38);
                }
            }
        }
        private void CityClothingRows(Window w,OutfitDef[] outfits,bool buy,Place store)
        {
            const int size=6;int pages=Math.Max(1,(outfits.Length+size-1)/size);_cityClothingPage=Math.Max(0,Math.Min(_cityClothingPage,pages-1));
            foreach(var item in outfits.Skip(_cityClothingPage*size).Take(size))
            {
                var o=item;var row=Ui.Row(w.Content,42,6);bool owned=S.OwnedOutfits.Contains(o.Id);
                Ui.Size(Ui.Label(row,o.Name+" · "+o.TierName+" · "+o.ComfortMinC+"—"+o.ComfortMaxC+"℃"+(buy?" · "+o.Price:owned?" · 已拥有":""),16),-1,42,1);
                Ui.Size(Ui.Button(row,"试穿",()=>{_cityClothingPreview=o.Id;w.Rebuild();},16),78,40);
                if(buy)Ui.Size(Ui.Button(row,owned?"已拥有":"柜台购买",()=>{S.BuyCityClothing(store,o.Id);w.Rebuild();},16,Ui.ButtonHot,!owned&&S.CurrentPlace==store&&S.IsPlaceOpen(store)&&S.Ledger.Balance>=o.Price),110,40);
                if(_cityClothingDetails)Para(w.Content,o.FictionalBrandId+" · "+o.WeightKg+"千克 · "+o.Description,15,Ui.Dim);
            }
            if(outfits.Length==0)Line(w.Content,"当前筛选下没有已拥有服装。",16,Ui.Dim);
            var nav=Ui.Row(w.Content,36,8);Ui.Size(Ui.Button(nav,"上一页",()=>{_cityClothingPage--;w.Rebuild();},16,interactable:_cityClothingPage>0),120,36);
            Ui.Size(Ui.Label(nav,(_cityClothingPage+1)+" / "+pages,16),-1,36,1);
            Ui.Size(Ui.Button(nav,"下一页",()=>{_cityClothingPage++;w.Rebuild();},16,interactable:_cityClothingPage+1<pages),120,36);
        }
        public void OpenCityClothingShop(Place place)
        {
            OpenPlaceWindow(place,BuildCarryStore,1120,960);
        }
    }
}
