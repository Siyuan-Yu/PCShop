using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _assetPropertyListTab;

        private void BuildAssetProperties(Window w)
        {
            GuideHint(w.Content,QuestTarget.Property,w.Rebuild);
            var owned=S.OwnedProperties.Where(p=>S.City.HasPlace(p.PlaceId)).Select(p=>S.City.GetPlace(p.PlaceId))
                .Concat(S.HomePriceCents>0?new[]{S.Home}:Array.Empty<Place>()).GroupBy(p=>p.Id).Select(g=>g.First())
                .OrderBy(p=>S.City.StreetName(p.StreetId),StringComparer.Ordinal).ThenBy(p=>p.Name,StringComparer.Ordinal).ToArray();
            var ownedIds=new HashSet<string>(owned.Select(p=>p.Id));
            var rented=S.MyShops.Where(s=>s.Rented&&!s.OwnsProperty&&s.Lease!=null).Select(s=>s.Place)
                .Concat(S.ManagedVentures.Where(v=>S.City.HasPlace(v.PlaceId)&&S.PropertyAt(v.PlaceId)==null).Select(v=>S.City.GetPlace(v.PlaceId)))
                .Concat(S.HomePriceCents<=0?new[]{S.Home}:Array.Empty<Place>())
                .Concat(S.PendingLease!=null&&S.City.HasPlace(S.PendingLease.PlaceId)?new[]{S.City.GetPlace(S.PendingLease.PlaceId)}:Array.Empty<Place>())
                .Where(p=>!ownedIds.Contains(p.Id)).GroupBy(p=>p.Id).Select(g=>g.First())
                .OrderBy(p=>S.City.StreetName(p.StreetId),StringComparer.Ordinal).ThenBy(p=>p.Name,StringComparer.Ordinal).ToArray();
            var rentedIds=new HashSet<string>(rented.Select(p=>p.Id));
            if(owned.Length==0&&rented.Length>0)_assetPropertyListTab=1;
            if(!owned.Concat(rented).Any(p=>p.Id==_assetPropertyId))
                _assetPropertyId=(_assetPropertyListTab==0?owned:rented).FirstOrDefault()?.Id;

            var summary=Ui.Row(w.Content,32);
            Ui.Size(Ui.Label(summary,"我的物业位置 · 已购 "+owned.Length+"处 · 租用 "+rented.Length+"处",22,TextAnchor.MiddleLeft,Ui.Accent),-1,32,1);
            var navigation=Ui.Row(w.Content,36,6);
            var stage=Ui.Rect(w.Content,"PropertyCity");Ui.Size(stage,-1,602);
            var mapArea=Ui.Rect(stage,"CityMap");mapArea.Anchor(0,0,1,1,0,24,290,0);mapArea.gameObject.AddComponent<RectMask2D>();
            var drawing=Ui.Rect(mapArea,"PropertyMapDrawing").Fill();
            var view=drawing.gameObject.AddComponent<AssetPropertyMap>();
            var detail=Ui.Rect(stage,"PropertyList");detail.Anchor(1,0,1,1,-272,0,0,0);Ui.VList(detail,5,0);
            view.Initialize(S,_assetMapView,owned,rented,_assetPropertyId);

            var regionButtons=new List<(string id,Button button)>();
            regionButtons.Add(("all",Ui.Size(Ui.Button(navigation,"全城",view.FocusAll,17),86,36)));
            foreach(var district in S.City.AgencyDistricts)
            {
                var target=district;
                regionButtons.Add((target.id,Ui.Size(Ui.Button(navigation,target.name,()=>view.FocusDistrict(target),17),145,36)));
            }
            var mapCaption=Ui.Label(mapArea,"",14,TextAnchor.UpperLeft,Ui.TextColor);mapCaption.rectTransform.Anchor(0,1,1,1,12,-34,80,0);
            var north=Ui.Label(mapArea,"↑ 北",14,TextAnchor.UpperRight,Ui.Dim);north.rectTransform.Anchor(1,1,1,1,-62,-35,12,0);
            var controls=Ui.Panel(mapArea,"AssetMapZoom",Ui.PanelBg).rectTransform;controls.Anchor(0,0,0,0,12,12,-184,-51);
            var bar=Ui.Row(controls,39,3);bar.Fill();
            Ui.Size(Ui.Button(bar,"−",()=>view.ZoomBy(1/1.3f),20),43,37);
            var zoom=Ui.Size(Ui.Label(bar,"",15,TextAnchor.MiddleCenter),58,37);
            Ui.Size(Ui.Button(bar,"+",()=>view.ZoomBy(1.3f),20),43,37);

            var popup=Ui.Panel(mapArea,"Panel",Ui.PanelBg).rectTransform;popup.name="AssetBuildingInfo";
            popup.anchorMin=popup.anchorMax=new Vector2(.5f,.5f);popup.pivot=new Vector2(0,1);popup.sizeDelta=new Vector2(330,252);
            var popupContent=Ui.Rect(popup,"BuildingInfoContent").Fill(10);Ui.VList(popupContent,3,0);
            popup.gameObject.SetActive(false);
            AssetPropertyMap.Building popupBuilding=null;bool popupWanted=false;
            var listButtons=new Dictionary<string,Button>();

            void UpdateSelection()
            {
                foreach(var pair in listButtons)pair.Value.image.color=pair.Key==_assetPropertyId?Ui.ButtonHot:Ui.ButtonBg;
            }
            void PlacePopup()
            {
                if(!popupWanted||popupBuilding==null){popup.gameObject.SetActive(false);return;}
                var point=view.ProjectBuilding(popupBuilding);var bounds=mapArea.rect;
                bool visible=bounds.Contains(point);popup.gameObject.SetActive(visible);if(!visible)return;
                popup.anchoredPosition=new Vector2(Mathf.Clamp(point.x+12,bounds.xMin+8,bounds.xMax-popup.sizeDelta.x-8),
                    Mathf.Clamp(point.y-12,bounds.yMin+popup.sizeDelta.y+8,bounds.yMax-8));
            }
            void ShowInfo(AssetPropertyMap.Building building)
            {
                popupBuilding=building;popupWanted=building!=null;
                if(building==null){PlacePopup();return;}
                _assetPropertyId=building.Id;view.Select(building.Id);UpdateSelection();Ui.Clear(popupContent);
                var head=Ui.Row(popupContent,43,4);
                var title=Ui.Size(Ui.Label(head,building.Place==null?building.Name:AssetPropertyName(building.Place),18,TextAnchor.UpperLeft,Ui.Accent),-1,43,1);
                title.resizeTextForBestFit=true;title.resizeTextMinSize=15;title.resizeTextMaxSize=18;
                Ui.Size(Ui.Button(head,"×",()=>{popupWanted=false;PlacePopup();},19),29,29);
                Ui.Size(Line(popupContent,S.City.StreetName(building.StreetId),14,Ui.Dim),-1,22);
                var place=building.Place;
                bool mine=ownedIds.Contains(building.Id),leased=rentedIds.Contains(building.Id);
                Ui.Size(Line(popupContent,AssetPropertyStatus(place,mine,leased),16,mine?AssetPropertyMap.OwnedColor:leased?AssetPropertyMap.RentedColor:Ui.Dim),-1,25);
                foreach(string line in AssetPropertyInfo(place,mine,leased).Take(3))
                {
                    var text=Ui.Size(Line(popupContent,line,15),-1,25);text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=15;
                }
                if(place!=null&&(place.Kind==PlaceKind.Home||GameSession.IsCommercialPremises(place)))
                    Ui.Size(Ui.Button(popupContent,"前往所属中介办理",()=>GoToPropertyAgency(place),15),-1,31);
                PlacePopup();
            }
            void RenderSide()
            {
                Ui.Clear(detail);listButtons.Clear();
                Line(detail,"我的物业",21,Ui.Accent);
                var tabs=Ui.Row(detail,38,5);
                Ui.Size(Ui.Button(tabs,"我拥有的 · "+owned.Length,()=>{_assetPropertyListTab=0;RenderSide();},15,_assetPropertyListTab==0?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);
                Ui.Size(Ui.Button(tabs,"我租的 · "+rented.Length,()=>{_assetPropertyListTab=1;RenderSide();},15,_assetPropertyListTab==1?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);
                var rows=_assetPropertyListTab==0?owned:rented;
                long monthly=_assetPropertyListTab==0?rows.Sum(AssetPropertyRent):rows.Sum(AssetLeasedRent);
                var total=Line(detail,(_assetPropertyListTab==0?"每月应收 ":"每月应付 ")+Money.FromCents(monthly),19,
                    _assetPropertyListTab==0?AssetPropertyMap.OwnedColor:AssetPropertyMap.RentedColor);
                total.resizeTextForBestFit=true;total.resizeTextMinSize=14;total.resizeTextMaxSize=19;
                Line(detail,"单击查看信息 · 双击定位聚焦",14,Ui.Dim);
                var list=Ui.Scroll(detail,440);
                if(rows.Length==0)Line(list,_assetPropertyListTab==0?"暂无已购物业":"暂无租用物业",17,Ui.Dim);
                foreach(var p in rows)
                {
                    var target=p;bool mine=_assetPropertyListTab==0;
                    long amount=mine?AssetPropertyRent(target):AssetLeasedRent(target);
                    string sub=AssetPropertyStatus(target,mine,!mine)+(amount>0?" · "+Money.FromCents(amount)+"/月":"");
                    var button=Ui.Size(Ui.Button(list,AssetPropertyName(target)+"\n<size=13>"+sub+"</size>",()=>ShowInfo(view.BuildingAt(target.Id)),15,
                        target.Id==_assetPropertyId?Ui.ButtonHot:Ui.ButtonBg),-1,69);
                    var text=button.GetComponentInChildren<Text>();text.alignment=TextAnchor.MiddleLeft;text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=15;
                    button.name="AssetPropertyRow_"+target.Id;button.Tip("单击查看建筑信息，双击在地图上定位。\n"+S.City.StreetName(target.StreetId));
                    listButtons[target.Id]=button;
                    button.gameObject.AddComponent<AssetPropertyDoubleClick>().Focus=()=>view.FocusPlace(target);
                }
            }
            view.BuildingSelected=ShowInfo;
            view.Changed=()=>
            {
                zoom.text=(view.Zoom*100).ToString("0")+"%";
                mapCaption.text=view.Region=="all"?"全城":S.City.AgencyDistricts.FirstOrDefault(d=>d.id==view.Region)?.name??"自由查看";
                foreach(var pair in regionButtons)pair.button.image.color=pair.id==view.Region?Ui.ButtonHot:Ui.ButtonBg;
                PlacePopup();
            };
            RenderSide();view.Changed();
            if(_assetFocusProperty)
            {
                var focused=owned.Concat(rented).FirstOrDefault(p=>p.Id==_assetPropertyId);
                if(focused!=null){ShowInfo(view.BuildingAt(focused.Id));view.FocusPlace(focused);}
                _assetFocusProperty=false;
            }
            var legend=Ui.Label(stage,"<color=#E8BE62>■ 已购买</color>　<color=#67B5D8>■ 我租用的</color>　<color=#697C6C>■ 其他建筑</color>　· 点建筑查看",14,TextAnchor.MiddleLeft,Ui.Dim);
            legend.rectTransform.Anchor(0,0,1,0,0,0,290,-23);
            Line(w.Content,"点击建筑查看基本信息；右侧单击选中、双击聚焦。滚轮缩放、拖动查看。",15,Ui.Dim);
        }

        private string AssetPropertyName(Place place)
        {
            var shop=S.ShopAt(place.Id);return shop==null?place.Name:S.ShopTitle(shop);
        }
        private long AssetPropertyRent(Place place)
        {
            if(place==S.Home&&S.HomePriceCents>0)return 0;
            var p=S.PropertyAt(place.Id);if(p==null||S.ShopAt(place.Id)!=null||S.VentureAt(place.Id)!=null||place.Id==S.ParkPlaceId)return 0;
            if(place.Kind==PlaceKind.Home&&S.ResidentialTenants(place.Id).Length==0)return 0;
            return p.UseMode=="managed"?Money.FromCents(p.MonthlyRentCents).Scale(.85).Cents:p.UseMode=="npc"?p.MonthlyRentCents:0;
        }
        private long AssetLeasedRent(Place place)
        {
            if(place==S.Home&&S.HomePriceCents<=0)return S.CurrentHomeRent.Cents;
            var shop=S.ShopAt(place.Id);if(shop!=null&&shop.Rented)return S.RentOf(shop).Cents;
            var venture=S.VentureAt(place.Id);if(venture!=null)return venture.RentCents;
            return S.PendingLease?.PlaceId==place.Id?S.PendingLease.Rent.Cents:0;
        }
        private string AssetPropertyStatus(Place place,bool owned,bool rented)
        {
            if(place==null)return "其他建筑";
            if(owned)
                return place==S.Home&&S.HomePriceCents>0?"已购 · 自住":S.ShopAt(place.Id)!=null||S.VentureAt(place.Id)!=null||place.Id==S.ParkPlaceId?"已购 · 自营":AssetPropertyRent(place)>0?"已购 · 出租":"已购 · 空置";
            if(rented)return S.PendingLease?.PlaceId==place.Id?"租用 · 待交房":place==S.Home?"租住 · 当前住所":"租用 · 经营";
            if(place.Kind==PlaceKind.Home)return S.ResidentialTenants(place.Id).Length>0?"其他业主 · 有住户":"其他业主 · 空房";
            return GameSession.IsCommercialPremises(place)?"其他业主 · 商铺":"公共 / 专用场所";
        }
        private string[] AssetPropertyInfo(Place place,bool owned,bool rented)
        {
            if(place==null)return new[]{"城市背景建筑，无可办理的房源。"};
            if(owned)
            {
                var property=S.PropertyAt(place.Id);
                Money value=place==S.Home&&S.HomePriceCents>0?S.HomePurchasePrice:S.PropertyMarketValue(property);
                return new[]{"当前估值 "+value,"每月应收租金 "+Money.FromCents(AssetPropertyRent(place)),
                    AssetPropertyRent(place)>0?"租户："+property.TenantName:"产权属于你，无需向房东交租。"};
            }
            if(!rented&&place.Kind!=PlaceKind.Home&&!GameSession.IsCommercialPremises(place))return new[]{"当前用途："+(string.IsNullOrEmpty(place.Business)?place.Name:place.Business),"该地点不开放普通房产租购。"};
            Money asking=place.Kind==PlaceKind.Home?S.ResidentialRent(place):S.ShopRentQuote(place);
            if(rented)return new[]{"签约月租 "+Money.FromCents(AssetLeasedRent(place))+"（保留签约价）","当前新租参考 "+asking,
                S.PendingLease?.PlaceId==place.Id?"交房日期 "+new GameDate(S.PendingLease.StartDay):"你持有租用权，产权属于房东。"};
            Money price=place.Kind==PlaceKind.Home?S.ResidentialPrice(place):S.CityPropertyPrice(place.Id);
            var listing=S.Listings.FirstOrDefault(l=>l.PlaceId==place.Id);
            return new[]{"参考售价 "+price,(listing==null?"新租参考月租 ":"挂牌月租 ")+(listing?.Rent??asking),
                place.Kind==PlaceKind.Home?"登记住户 "+S.ResidentialTenants(place.Id).Length+"人":listing==null?"当前没有租赁挂牌，可到中介了解产权。":"租赁与购买需在所属中介办理。"};
        }
    }
}
