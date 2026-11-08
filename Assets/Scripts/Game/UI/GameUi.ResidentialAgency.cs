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
        private void BuildResidentialAgency(Window w,Place agency)
        {
            var homes=S.ResidentialPlaces.Where(p=>S.AgencyServes(agency,p)).ToArray();
            if(!homes.Any(p=>p.Id==_residentialSelectedId))_residentialSelectedId=homes.FirstOrDefault(p=>p==S.Home)?.Id
                ??homes.FirstOrDefault(p=>S.ResidentialTenants(p.Id).Length==0)?.Id??homes.FirstOrDefault()?.Id;
            var (map, detail) = AgencyPropertyColumns(w.Content, "ResidentialMap", "ResidentialDetail");
            BuildResidentialMap(map,homes,agency,w);
            var selected=homes.FirstOrDefault(p=>p.Id==_residentialSelectedId);
            if(selected==null){Para(detail,"本片区没有住宅房源。",19);return;}
            BuildResidentialDetail(detail,selected,Array.IndexOf(homes,selected)+1,w);
        }
        private string ResidentialStatus(Place p)
        {
            if(p==S.Home)return S.HomePriceCents>0?"自有 · 默认住处":"已租 · 默认住处";
            if(S.HasResidentialLease(p))return "已租 · 可入住";
            bool occupied=S.ResidentialTenants(p.Id).Length>0;
            return S.PropertyAt(p.Id)!=null?(occupied?"我的产权 · NPC租住":"我的空置住宅"):(occupied?"已有NPC居住 · 可购产权":"空置 · 可租住或购买");
        }
        private void BuildResidentialMap(RectTransform map,Place[] homes,Place agency,Window w)
        {
            AgencyMapText(map,S.City.AgencyCoverageName(agency)+" · 本片区住宅",23,16,6,340,34,Ui.TextColor);
            var raw=Ui.Rect(map,"ResidentialViewport");AgencyAt(raw,0,46,1120,650);raw.gameObject.AddComponent<RawImage>();raw.gameObject.AddComponent<RectMask2D>();
            var view=raw.gameObject.AddComponent<AgencyStreetMap>();view.Initialize(_ctrl,_agencyViewState,AgencyStreetMap.DistrictPlaces(S,agency),1120,650,true);
            var overlay=Ui.Rect(raw,"ResidentialMarkers").Fill();
            var zoom=Ui.Label(map,"",16,TextAnchor.MiddleCenter,Ui.Dim);AgencyAt(zoom.rectTransform,664,8,62,30);
            view.Selected=id=>{if(homes.Any(p=>p.Id==id)){_residentialSelectedId=id;w.Rebuild();}};
            view.Changed=()=>
            {
                Ui.Clear(overlay);
                zoom.text=$"{view.Zoom:0.0}×";
                if(_ctrl.World.Places.TryGetValue(agency.Id,out var agentBuilding))
                {
                    var bounds=AgencyStreetMap.BuildingBounds(agentBuilding);
                    var glow=Ui.Rect(overlay,"ResidentialAgencyGlow");AgencyAt(glow,0,0,view.Width,view.Height);glow.gameObject.AddComponent<CanvasRenderer>();
                    glow.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(AgencyStreetMap.Corners(bounds).Select(view.Project).ToArray(),new Color32(66,158,255,255),false);
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
                    var color=selected?Ui.Accent:p==S.Home||S.HasResidentialLease(p)||S.PropertyAt(p.Id)!=null?new Color32(75,155,230,255):S.ResidentialTenants(p.Id).Length>0?AgencyFuture:Ui.Good;
                    var bounds=AgencyStreetMap.BuildingBounds(building);
                    if(selected)
                    {
                        var glow=Ui.Rect(overlay,"SelectedResidenceGlow");AgencyAt(glow,0,0,view.Width,view.Height);glow.gameObject.AddComponent<CanvasRenderer>();
                        glow.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(AgencyStreetMap.Corners(bounds).Select(view.Project).ToArray(),color,true);
                    }
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    if(at.x<0||at.x>view.Width||at.y<0||at.y>view.Height)continue;
                    string id=p.Id;int number=Array.IndexOf(homes,p)+1;
                    string label=number.ToString("D2")+" "+(p==S.Home?"默认住处":S.HasResidentialLease(p)?"我的租房":S.PropertyAt(id)!=null?"我的住宅":S.ResidentialTenants(id).Length>0?"有住户":"空房");
                    var button=Ui.Button(overlay,label,()=>{_residentialSelectedId=id;w.Rebuild();},14,color);
                    button.name="ResidenceMap_"+id;AgencyAt((RectTransform)button.transform,Mathf.Clamp(at.x-53,2,view.Width-108),Mathf.Clamp(at.y-25,2,view.Height-32),106,30);
                    button.Tip(p.Name+"\n"+ResidentialStatus(p));
                }
            };
            AgencyMapControl(map,"定位中介","ResidentialFocusCurrent",548,108,()=>view.Focus(agency));
            AgencyMapControl(map,"−","ResidentialZoomOut",738,44,()=>view.ZoomBy(1/1.35f));
            AgencyMapControl(map,"+","ResidentialZoomIn",788,44,()=>view.ZoomBy(1.35f));
            AgencyMapControl(map,"本片区","ResidentialResetView",842,80,view.ResetView);
            AgencyMapControl(map,"放大这套","ResidentialFocusUnit",932,172,()=>{var p=homes.FirstOrDefault(h=>h.Id==_residentialSelectedId);if(p!=null)view.Focus(p);});
            AgencyMapText(map,"绿：空房　橙：有住户、可购产权　蓝：已租或已购　金：已选中",16,18,702,1080,32,Ui.Dim);
            view.Changed();
        }
        private void BuildResidentialDetail(Transform detail,Place p,int number,Window parent)
        {
            string id=p.Id;var tenants=S.ResidentialTenants(id);var property=S.PropertyAt(id);bool selfOwned=p==S.Home&&S.HomePriceCents>0;
            var room = p == S.Home ? S.ResidenceRoom : RoomSpec.ForResidence(S.ResidentialTier(p));
            Money rent = S.ResidentialRent(p);
            bool leased = S.HasResidentialLease(p);
            Money signedRent = leased ? S.SignedResidentialRent(p) : Money.Zero;
            AgencyPropertyHeading(detail, $"{number:00}　{p.Name}", ResidentialStatus(p));
            AgencyPropertyMetrics(detail, leased ? "签约周租" : "新租周租", (leased ? signedRent : rent).ToString(),
                S.ResidentialPrice(p).ToString(), "室内面积", $"{room.Width * room.Depth:0.#}㎡");
            if(selfOwned)
            {
                AgencyPropertyAction(detail,"出售自住房",()=>ConfirmHousing(parent,p,"出售自住房",-S.HomePurchasePrice.Scale(.9),"按当前估值九折出售，继续租住，七天后开始缴周租。",S.SellHome));
            }
            else if(property!=null)
            {
                AgencyPropertyAction(detail,"出售住宅产权",()=>ConfirmHousing(parent,p,"出售出租住宅",-S.PropertySaleNet(property),"包含估值折价与交易成本，原NPC住户保留。",()=>S.SellResidentialInvestment(id)),primary:tenants.Length>0);
            }
            else
            {
                if(tenants.Length==0)
                {
                    if(leased)
                    {
                        var next=S.NextResidenceAfterEndingLease(p);
                        AgencyPropertyAction(detail,"退租住宅",()=>ConfirmHousing(parent,p,"确认退租",Money.Zero,
                            "本次退租后停止缴纳这套住宅的周租。"+(p==S.Home&&next!=null?"\n默认住处将改为"+next.Name+"。":""),
                            ()=>S.EndResidentialLease(id)),"ResidenceEndLease",primary:false,blocker:S.EndResidentialLeaseBlocker(id));
                    }
                    else
                    {
                        AgencyPropertyAction(detail,"租赁住宅",
                            ()=>ConfirmHousing(parent,p,"确认租赁",rent,"签约时付首周租金，此后每七天按签约价扣款。租下后可入住，原有住处继续保留。",()=>S.SignResidentialLease(id)),
                            "ResidenceRent",blocker:S.ResidentialLeaseBlocker(id));
                    }
                }
                HousingPurchaseButton(detail,parent,p,tenants.Length==0,leased||tenants.Length>0);
            }
            BuildAgencyFloorPlan(detail,room);
            Line(detail,S.City.StreetName(p.StreetId)+" · "+tenants.Length+" 名住户",16,Ui.Dim);
            if(leased&&rent!=signedRent)
                Line(detail,"当前租约保留签约价 · 新租报价 "+rent,17,Ui.Dim);
            if(property!=null)
                Line(detail,tenants.Length>0?"每周应收租金 "+Money.FromCents(property.WeeklyRentCents):"暂无实际租户，不产生租金",17,Ui.Accent);
            if(tenants.Length>0)
            {
                foreach (var tenant in tenants)
                    PersonSummary(detail,tenant.PersonId,S.CityPerson(tenant.PersonId)?.Name??tenant.PersonId,"当前住户",42);
                if(property==null)Para(detail,"购入后保留住户，下月开始收租。",16,Ui.Dim);
            }
            if (S.NextResidentialRentDay(p) is int nextRentDay)
                Line(detail, "下次扣租 · " + new PCShopSell.Core.Time.GameDate(nextRentDay), 16, Ui.Dim);
            HelpLink(detail,"住宅产权与周租","空房可租可买，可同时持有多套。租金按各套住宅的签约价每七天扣款。退租后必须保留至少一处可入住的住宅。有住户的住宅可全款购买产权，收2%交易费，购入七天后开始收周租。空房无租金，月维护费为购入价0.1%。");
        }
        private void HousingPurchaseButton(Transform parent,Window window,Place p,bool selfUse,bool primary)
        {
            bool leased=S.HasResidentialLease(p);
            string action=selfUse?(leased?"全款买下这套租房":"全款购买住宅"):"全款购买产权";
            var due=S.ResidentialPurchaseDue(p.Id,false,selfUse,keepCurrentResidence:true);
            string terms=!selfUse?"含2%交易费，原住户从下月起交租。"
                :leased?"买下后，这套住宅停止缴纳周租。":"买下后即可入住，默认住处保留。";
            AgencyPropertyAction(parent,action,()=>ConfirmHousing(window,p,action,due,terms+"\n月维护 "+S.ResidentialPrice(p).Scale(.001),
                ()=>S.BuyResidentialHome(p.Id,false,selfUse,keepCurrentResidence:true)),selfUse?"ResidenceBuyCash":null,primary,
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
