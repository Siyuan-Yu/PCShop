using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private string _residentialSelectedId;
        private Action BuildResidentialAgency(Window w,Place agency)
        {
            var homes=S.ResidentialPlaces.Where(p=>S.AgencyServes(agency,p)).ToArray();
            if(!homes.Any(p=>p.Id==_residentialSelectedId))_residentialSelectedId=homes.FirstOrDefault(p=>p==S.Home)?.Id
                ??homes.FirstOrDefault(p=>S.ResidentialTenants(p.Id).Length==0)?.Id??homes.FirstOrDefault()?.Id;
            var (map, detail) = AgencyPropertyColumns(w.Content, "ResidentialMap", "ResidentialDetail");
            void RefreshProperty()
            {
                Ui.Clear(detail);
                var selected = homes.FirstOrDefault(p => p.Id == _residentialSelectedId);
                if (selected == null) Para(detail, "本片区没有住宅房源。", 19);
                else BuildResidentialDetail(detail, selected, Array.IndexOf(homes, selected) + 1, w);
            }
            BuildResidentialMap(map, homes, agency, RefreshProperty);
            RefreshProperty();
            return RefreshProperty;
        }
        private string ResidentialStatus(Place p)
        {
            if(p==S.Home)return S.HomePriceCents>0?"自有 · 默认住处":"已租 · 默认住处";
            if(S.HasResidentialLease(p))return "已租 · 可入住";
            bool occupied=S.ResidentialTenants(p.Id).Length>0;
            return S.PropertyAt(p.Id)!=null?(occupied?"我的产权 · NPC租住":"我的空置住宅"):(occupied?"已有NPC居住 · 可购产权":"空置 · 可租住或购买");
        }
        private void BuildResidentialMap(RectTransform map,Place[] homes,Place agency,Action refreshProperty)
        {
            AgencyMapText(map,S.City.AgencyCoverageName(agency)+" · 本片区住宅",23,16,6,340,34,Ui.TextColor);
            var raw=Ui.Rect(map,"ResidentialViewport");AgencyAt(raw,0,46,1120,650);raw.gameObject.AddComponent<RawImage>();raw.gameObject.AddComponent<RectMask2D>();
            var view=raw.gameObject.AddComponent<AgencyStreetMap>();view.Initialize(_ctrl,_agencyViewState,AgencyStreetMap.DistrictPlaces(S,agency),1120,650,true);
            var overlay=Ui.Rect(raw,"ResidentialMarkers").Fill();
            var zoom=Ui.Label(map,"",16,TextAnchor.MiddleCenter,Ui.Dim);AgencyAt(zoom.rectTransform,664,8,62,30);
            view.Selected = id =>
            {
                if (_residentialSelectedId == id || !view.CanSelect(id)) return;
                _residentialSelectedId = id;
                refreshProperty();
                view.Changed?.Invoke();
            };
            view.Changed=()=>
            {
                Ui.Clear(overlay);
                zoom.text=$"{view.Zoom:0.0}×";
                if(_ctrl.World.Places.TryGetValue(agency.Id,out var agentBuilding))
                {
                    var bounds=view.BoundsOf(agentBuilding);
                    AgencyMapGlow(overlay, view, "ResidentialAgencyGlow", agentBuilding, new Color32(66,158,255,255));
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    if(at.x>=0&&at.x<=view.Width&&at.y>=0&&at.y<=view.Height)
                    {
                        var label=Ui.Label(overlay,"● 当前中介",15,TextAnchor.MiddleCenter,new Color32(66,158,255,255));label.raycastTarget=false;
                        AgencyAt(label.rectTransform,Mathf.Clamp(at.x-70,2,view.Width-142),Mathf.Clamp(at.y-24,2,view.Height-30),140,28);
                    }
                }
                foreach(var p in homes)
                {
                    if(!_ctrl.World.Places.TryGetValue(p.Id,out var building))continue;
                    bool selected=p.Id==_residentialSelectedId;
                    bool owned = S.PropertyAt(p.Id) != null || p == S.Home && S.HomePriceCents > 0;
                    var bounds=view.BoundsOf(building);
                    if (owned)
                        AgencyMapGlow(overlay, view, "OwnedResidenceGlow_" + p.Id, building, AgencyOwnedPropertyColor, selected);
                    else if (selected)
                        AgencyMapGlow(overlay, view, "SelectedResidenceGlow", building, Ui.Accent, true);
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    if(at.x<0||at.x>view.Width||at.y<0||at.y>view.Height)continue;
                    bool rented = S.HasResidentialLease(p);
                    bool occupied = !owned && !rented && S.ResidentialTenants(p.Id).Length > 0;
                    Color color = selected ? Ui.Accent : owned ? (Color)AgencyOwnedPropertyColor
                        : rented ? (Color)new Color32(75,155,230,255) : occupied ? (Color)AgencyStreetMap.RentalUpcoming : Ui.Good;
                    string id=p.Id;int number=Array.IndexOf(homes,p)+1;
                    string label=number.ToString("D2")+" "+(p==S.Home?"默认住处":owned?"我的住宅":rented?"我的租房":occupied?"有住户":"空房");
                    var button=Ui.Button(overlay,label,()=>view.Selected?.Invoke(id),14,color);
                    button.name="ResidenceMap_"+id;AgencyAt((RectTransform)button.transform,Mathf.Clamp(at.x-53,2,view.Width-108),Mathf.Clamp(at.y-25,2,view.Height-32),106,30);
                    button.Tip(p.Name+"\n"+ResidentialStatus(p));
                }
            };
            AgencyMapControl(map,"定位中介","ResidentialFocusCurrent",548,108,()=>view.Focus(agency));
            AgencyMapControl(map,"−","ResidentialZoomOut",738,44,()=>view.ZoomBy(1/1.35f));
            AgencyMapControl(map,"+","ResidentialZoomIn",788,44,()=>view.ZoomBy(1.35f));
            AgencyMapControl(map,"本片区","ResidentialResetView",842,80,view.ResetView);
            AgencyMapControl(map,"放大这套","ResidentialFocusUnit",932,172,()=>{var p=homes.FirstOrDefault(h=>h.Id==_residentialSelectedId);if(p!=null)view.Focus(p);});
            AgencyMapText(map,"房源标记",14,16,703,90,24,Ui.Dim);
            AgencyMapKey(map, 120, Ui.Good, "绿色：空房");
            AgencyMapKey(map, 276, AgencyStreetMap.RentalUpcoming, "橙色：有住户、可购产权");
            AgencyMapKey(map, 502, new Color32(75,155,230,255), "蓝色：已租 / 当前中介");
            AgencyMapKey(map, 720, AgencyOwnedPropertyColor, "紫色：我的产权");
            AgencyMapKey(map, 894, Ui.Accent, "金色：已选中");
            view.Changed();
        }
        private void BuildResidentialDetail(Transform detail,Place p,int number,Window parent)
        {
            var tenants = S.ResidentialTenants(p.Id);
            var property = S.PropertyAt(p.Id);
            var room = p == S.Home ? S.ResidenceRoom : RoomSpec.ForResidence(S.ResidentialTier(p));
            var overview = AgencyPropertyCard(detail, "AgencyPropertyOverview");
            AgencyPropertyHeading(overview, $"{number:00}　{p.Name}", ResidentialStatus(p));
            var facts = AgencyPropertySummary(overview, room);
            Line(facts, $"室内面积 {room.Width * room.Depth:0.#}㎡", 18);
            Para(facts, $"{S.City.StreetName(p.StreetId)} · {tenants.Length} 名住户", 16, Ui.Dim);
            if (property != null && tenants.Length > 0)
                Line(facts, "每周应收租金 " + Money.FromCents(property.WeeklyRentCents), 16, Ui.Good);
            Ui.Size(Ui.Button(overview, "住宅详情 ›", () => OpenResidentialAgencyDetails(p), 16), -1, 32);
            BuildResidentialSettlement(AgencyPropertyCard(detail, "AgencySettlement"), p, parent, tenants.Length > 0);
        }

        private void BuildResidentialSettlement(Transform settlement, Place p, Window parent, bool occupied)
        {
            string id = p.Id;
            var property = S.PropertyAt(id);
            bool selfOwned = p == S.Home && S.HomePriceCents > 0;
            Line(settlement, selfOwned || property != null ? "出售结算" : occupied ? "全款购买结算" : "办理结算", 21);
            if (selfOwned || property != null)
            {
                Money proceeds = selfOwned ? S.HomePurchasePrice.Scale(.9) : S.PropertySaleNet(property);
                string action = selfOwned ? "出售自住房" : "出售住宅产权";
                string terms = selfOwned ? "按当前估值九折出售，继续租住，满七天起逢周日扣租。"
                    : "包含估值折价与交易成本，原住户保留。";
                AgencyPaymentSummary(settlement, -proceeds, action, () => ConfirmHousing(parent, p, action,
                    -proceeds, terms, () => selfOwned ? S.SellHome() : S.SellResidentialInvestment(id)));
                return;
            }

            if (AgencyPurchaseMode(settlement, p, () =>
            {
                Ui.Clear(settlement);
                BuildResidentialSettlement(settlement, p, parent, occupied);
            }, !occupied, true))
            {
                BuildHousingPurchaseQuote(settlement, parent, p, selfUse: !occupied);
                return;
            }
            if (S.HasResidentialLease(p))
            {
                AgencyFeeLine(settlement, "签约周租", S.SignedResidentialRent(p).ToString());
                if (S.NextResidentialRentDay(p) is int nextRentDay)
                    Line(settlement, "下次扣租 · " + new PCShopSell.Core.Time.GameDate(nextRentDay) + " 周日", 16, Ui.Dim);
                var next = S.NextResidenceAfterEndingLease(p);
                AgencyPaymentSummary(settlement, Money.Zero, "退租住宅", () => ConfirmHousing(parent, p, "确认退租", Money.Zero,
                    "本次退租后停止缴纳这套住宅的周租。" + (p == S.Home && next != null ? "\n默认住处将改为" + next.Name + "。" : ""),
                    () => S.EndResidentialLease(id)), "ResidenceEndLease", S.EndResidentialLeaseBlocker(id));
                return;
            }
            Money rent = S.ResidentialRent(p);
            AgencyFeeLine(settlement, "首周租金", rent.ToString());
            AgencyPaymentSummary(settlement, rent, "确认租赁", () =>
                OpenPropertyContract("住宅租赁合同", p.Name, "签约后可入住", rent,
                    fees => AgencyFeeLine(fees, "首周租金", rent.ToString()),
                    $"签约时付首周租金。首周结束后，<b>每周日扣租 {rent}</b>。签约后可入住，原有住处继续保留。", false, () =>
                    {
                        if (S.ResidentialRent(p) == rent) return S.SignResidentialLease(id);
                        Toast("租金有变化，请重新核对。");
                        return false;
                    }, parent.Rebuild, "ResidenceConfirm", S.ResidentialLeaseBlocker(id)),
                "ResidenceRent", S.ResidentialLeaseBlocker(id));
        }

        private void OpenResidentialAgencyDetails(Place place)
        {
            OpenWindow("住宅详情 · " + place.Name, 680, 560, w =>
            {
                var tenants = S.ResidentialTenants(place.Id);
                var property = S.PropertyAt(place.Id);
                Line(w.Content, ResidentialStatus(place), 18, Ui.Accent);
                foreach (var tenant in tenants)
                    PersonSummary(w.Content, tenant.PersonId, S.CityPerson(tenant.PersonId)?.Name ?? tenant.PersonId, "当前住户", 42);
                if (S.HasResidentialLease(place))
                    AgencyFeeLine(w.Content, "签约周租", S.SignedResidentialRent(place).ToString());
                AgencyFeeLine(w.Content, "参考周租", S.ResidentialRent(place).ToString());
                if (property != null && tenants.Length == 0)
                    Line(w.Content, "空置住宅暂无租金收入", 16, Ui.Dim);
                Para(w.Content, "空房可租可买，可同时持有多套。首周结束后，每周日按签约价扣租。退租后须保留至少一处可入住住宅。带住户买入收取 2% 交易费，原住户保留，购入七天后开始收周租。月维护费为购入价的 0.1%。", 17);
            });
        }

        private void BuildHousingPurchaseQuote(Transform settlement,Window window,Place p,bool selfUse)
        {
            bool leased=S.HasResidentialLease(p);
            string action=selfUse?(leased?"全款买下这套租房":"全款购买住宅"):"全款购买产权";
            var due=S.ResidentialPurchaseDue(p.Id,false,selfUse,keepCurrentResidence:true);
            var price = S.ResidentialPrice(p);
            string terms = !selfUse ? "保留原住户，<b>购入七天后开始收周租</b>。"
                : leased ? "买下后，<b>这套住宅停止缴纳周租</b>。" : "买下后即可入住，默认住处保留。";
            void Quote(Transform fees)
            {
                AgencyFeeLine(fees, "全款售价", price.ToString());
                if (due > price) AgencyFeeLine(fees, "交易费", (due - price).ToString());
            }
            Quote(settlement);
            if (!selfUse) Line(settlement, "带住户购买 · 购入七天后开始收租", 16, Ui.Dim);
            AgencyPaymentSummary(settlement, due, action,
                () => OpenPropertyContract("住宅买卖合同", p.Name, selfUse ? "自住住宅" : "带住户购买产权", due, Quote,
                    terms + $"\n<b>月维护费 {price.Scale(.001)}</b>。", true, () =>
                    {
                        if (S.ResidentialPrice(p) == price && S.ResidentialPurchaseDue(p.Id, false, selfUse, keepCurrentResidence: true) == due)
                            return S.BuyResidentialHome(p.Id, false, selfUse, keepCurrentResidence: true);
                        Toast("房价或费用有变化，请重新核对。");
                        return false;
                    }, window.Rebuild, "ResidenceConfirm",
                    S.ResidentialTradeBlocker(p.Id, true, false, selfUse, keepCurrentResidence: true)), selfUse ? "ResidenceBuyCash" : null,
                S.ResidentialTradeBlocker(p.Id,true,false,selfUse,keepCurrentResidence:true));
        }
        private void ConfirmHousing(Window parent,Place p,string title,Money due,string terms,Func<bool> action)
        {
            bool submitted=false;
            OpenWindow(title,760,560,confirm=>
            {
                Line(confirm.Content,p.Name,24,Ui.Accent);Para(confirm.Content,terms,19);
                string payment=due.IsNegative?"本次收回 "+due.Abs():due.IsPositive?"本次支付 "+due:"无需付费";
                Line(confirm.Content,payment+" · 办理后现金 "+(S.Ledger.Balance-due),21,Ui.Accent);
                Ui.Size(Ui.Button(confirm.Content,"确认办理",()=>{if(submitted)return;submitted=true;if(action()){confirm.Close();parent.Rebuild();}else{submitted=false;confirm.Rebuild();}},20,Ui.ButtonHot,!due.IsPositive||S.Ledger.Balance>=due),-1,48).name="ResidenceConfirm";
                Ui.Size(Ui.Button(confirm.Content,"取消",confirm.Close,19),-1,44);
            });
        }
    }
}
