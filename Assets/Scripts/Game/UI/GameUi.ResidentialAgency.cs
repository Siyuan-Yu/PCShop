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
            if(p==S.Home)return S.HomePriceCents>0?"已购 · 主住所":"已租 · 主住所";
            if(S.HasResidentialLease(p))return "已租 · 可入住";
            bool occupied=S.ResidentialTenants(p.Id).Length>0;
            return S.PropertyAt(p.Id)!=null ? "已购 · " + OwnedPropertyStatus(p) : (occupied?"已有住户 · 不可租，可购产权":"空置 · 可租住或购买");
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
                    AgencyMapGlow(overlay, view, "ResidentialAgencyGlow", agentBuilding, AgencyLocationColor);
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    if(at.x>=0&&at.x<=view.Width&&at.y>=0&&at.y<=view.Height)
                    {
                        var badge = AgencyBlock(overlay, "ResidentialAgencyMarker", AgencyLocationColor,
                            Mathf.Clamp(at.x - 70, 2, view.Width - 142), Mathf.Clamp(at.y - 24, 2, view.Height - 30), 140, 28);
                        Ui.Label(badge.transform, "● 当前中介", 15, TextAnchor.MiddleCenter, Color.white).rectTransform.Fill(2);
                    }
                }
                foreach(var p in homes)
                {
                    if(!_ctrl.World.Places.TryGetValue(p.Id,out var building))continue;
                    bool selected=p.Id==_residentialSelectedId;
                    bool owned = OwnsPropertyAt(p);
                    bool rented = LeasesPropertyAt(p);
                    var bounds=view.BoundsOf(building);
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    bool inView = at.x >= 0 && at.x <= view.Width && at.y >= 0 && at.y <= view.Height;
                    if (!inView && !owned && !rented && !selected) continue;
                    bool occupied = !owned && !rented && S.ResidentialTenants(p.Id).Length > 0;
                    Color color = owned ? PropertyOwnedColor : rented ? PropertyRentedColor
                        : occupied ? AgencyStreetMap.RentalUpcoming : AgencyStreetMap.RentalAvailable;
                    if (owned || rented || selected)
                        AgencyMapGlow(overlay, view, "ResidenceOutline_" + p.Id, building, color, selected);
                    if (!inView) continue;
                    string id=p.Id;int number=Array.IndexOf(homes,p)+1;
                    string label=(selected ? "✓ " : "")+number.ToString("D2")+" "+(owned?"已购":rented?"已租":occupied?"有住户":"空房");
                    var button=Ui.Button(overlay,label,()=>view.Selected?.Invoke(id),14,color);
                    button.name="ResidenceMap_"+id;AgencyAt((RectTransform)button.transform,Mathf.Clamp(at.x-53,2,view.Width-108),Mathf.Clamp(at.y-25,2,view.Height-32),106,30);
                    button.Tip(p.Name+"\n"+ResidentialRoomCaption(p,S.RoomForResidence(p))+" · "+ResidentialStatus(p));
                }
            };
            AgencyMapControl(map,"定位中介","ResidentialFocusCurrent",548,108,()=>view.Focus(agency));
            AgencyMapControl(map,"−","ResidentialZoomOut",738,44,()=>view.ZoomBy(1/1.35f));
            AgencyMapControl(map,"+","ResidentialZoomIn",788,44,()=>view.ZoomBy(1.35f));
            AgencyMapControl(map,"本片区","ResidentialResetView",842,80,view.ResetView);
            AgencyMapControl(map,"放大这套","ResidentialFocusUnit",932,172,()=>{var p=homes.FirstOrDefault(h=>h.Id==_residentialSelectedId);if(p!=null)view.Focus(p);});
            BuildAgencyMapLegend(map, residential: true);
            view.Changed();
        }
        private static string ResidentialRoomCaption(Place place, RoomSpec room)
        {
            string label = place.HousingForm switch
            {
                ResidenceForm.LaneHouse => "小平房 · ",
                ResidenceForm.Walkup => "多层楼房 · ",
                _ => "室内面积 ",
            };
            return $"{label}{room.Width * room.Depth:0.#}㎡";
        }

        private void BuildResidentialDetail(Transform detail,Place p,int number,Window parent)
        {
            var tenants = S.ResidentialTenants(p.Id);
            var property = S.PropertyAt(p.Id);
            var room = p == S.Home ? S.ResidenceRoom : S.RoomForResidence(p);
            var overview = AgencyPropertyCard(detail, "AgencyPropertyOverview");
            AgencyPropertyHeading(overview, $"{number:00}　{p.Name}", ResidentialStatus(p));
            var facts = AgencyPropertySummary(overview, room);
            Line(facts, ResidentialRoomCaption(p,room), 18);
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
            if (selfOwned || property != null)
            {
                BuildOwnedResidenceActions(settlement, p, parent);
                return;
            }

            Line(settlement, occupied ? "全款购买结算" : "办理结算", 21);
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
                BuildResidencePropertyActions(settlement, p, parent, showTravel: true);
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

        private void BuildOwnedResidenceActions(Transform parent, Place place, Window owner)
        {
            var row = Ui.Row(parent, 33, 6);
            Ui.Size(Ui.Label(row, "住宅管理", 20, TextAnchor.MiddleLeft, Ui.Accent), 0, 33, 1);
            bool selfOwned = place == S.Home && S.HomePriceCents > 0;
            var property = S.PropertyAt(place.Id);
            Money proceeds = selfOwned ? S.HomePurchasePrice.Scale(.9) : S.PropertySaleNet(property);
            string title = selfOwned ? "出售自住房" : "出售住宅产权";
            string terms = selfOwned ? "按当前估值九折出售，出售后继续租住，满七天起逢周日扣租。" : "原住户保留。";
            Ui.Size(Ui.Button(row, "出售产权…", () => ConfirmHousing(owner, place, title, -proceeds, terms,
                () => selfOwned ? S.SellHome() : S.SellResidentialInvestment(place.Id)), 15, Ui.Warn), 111, 33)
                .Tip($"出售到手 {proceeds}");
            BuildResidencePropertyActions(parent, place, owner, showTravel: false);
            if (property != null) BuildPropertyRentalControls(parent, place, owner);
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
