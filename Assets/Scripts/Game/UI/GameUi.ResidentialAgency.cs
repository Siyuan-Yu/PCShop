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
            Line(w.Content,S.City.AgencyCoverageName(agency)+" · 住宅租售　现金 "+S.Ledger.Balance,23,Ui.Accent);
            Line(w.Content,"当前住处："+S.HomeTitle+"　片区住宅 "+homes.Length+" 处 · 无NPC住户 "+homes.Count(p=>S.ResidentialTenants(p.Id).Length==0)+" 处",17);
            var body=Ui.Row(w.Content,740,14);
            var map=Ui.Panel(body,"ResidentialMap",Ui.PanelBg2).rectTransform;Ui.Size(map,1120,740,0);
            BuildResidentialMap(map,homes,agency,w);
            var panel=Ui.Panel(body,"ResidentialDetail",Ui.PanelBg2).rectTransform;Ui.Size(panel,-1,740,1);Ui.VList(panel,6,12);
            var detail=Ui.Scroll(panel,690);var selected=homes.FirstOrDefault(p=>p.Id==_residentialSelectedId);
            if(selected==null){Para(detail,"本片区没有住宅房源。",19);return;}
            BuildResidentialDetail(detail,selected,w);
        }
        private string ResidentialStatus(Place p)
        {
            if(p==S.Home)return S.HomePriceCents>0?"当前自有住宅":"当前租住住宅";
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
                    var color=selected?Ui.Accent:p==S.Home||S.PropertyAt(p.Id)!=null?new Color32(75,155,230,255):S.ResidentialTenants(p.Id).Length>0?AgencyFuture:Ui.Good;
                    var bounds=AgencyStreetMap.BuildingBounds(building);
                    if(selected)
                    {
                        var glow=Ui.Rect(overlay,"SelectedResidenceGlow");AgencyAt(glow,0,0,view.Width,view.Height);glow.gameObject.AddComponent<CanvasRenderer>();
                        glow.gameObject.AddComponent<AgencyBuildingGlow>().Initialize(AgencyStreetMap.Corners(bounds).Select(view.Project).ToArray(),color,true);
                    }
                    var at=view.Project(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z));
                    if(at.x<0||at.x>view.Width||at.y<0||at.y>view.Height)continue;
                    string id=p.Id;int number=Array.IndexOf(homes,p)+1;
                    string label=number.ToString("D2")+" "+(p==S.Home?"当前住处":S.PropertyAt(id)!=null?"我的住宅":S.ResidentialTenants(id).Length>0?"有住户":"空房");
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
            AgencyMapText(map,"绿：空房　橙：有NPC住户、可购产权　蓝：当前住处 / 我的产权　金：已选中",16,18,702,1080,32,Ui.Dim);
            view.Changed();
        }
        private void HousingButton(Transform parent,string label,Action action,string blocker=null,string name=null)
        {
            var b=Ui.Size(Ui.Button(parent,label,action,18,Ui.ButtonHot,blocker==null),-1,42);
            if(name!=null)b.name=name;if(blocker!=null)Line(parent,blocker,15,Ui.Dim);
        }
        private void BuildResidentialDetail(Transform detail,Place p,Window parent)
        {
            string id=p.Id;var tenants=S.ResidentialTenants(id);var property=S.PropertyAt(id);bool selfOwned=p==S.Home&&S.HomePriceCents>0;
            Line(detail,p.Name,22,Ui.Accent);Line(detail,ResidentialStatus(p),17,Ui.Good);
            Line(detail,"新租月租 "+S.ResidentialRent(p)+" · 房价 "+S.ResidentialPrice(p),20);
            if(p==S.Home&&!selfOwned)Line(detail,"当前租约月租 "+S.CurrentHomeRent+"（保留签约价）",17,Ui.Dim);
            Para(detail,"地址："+S.City.StreetName(p.StreetId)+"\n按住所登记的住户 "+tenants.Length+" 人",16,Ui.Dim);
            BuildAgencyFloorPlan(detail,p==S.Home?S.ResidenceRoom:RoomSpec.ForResidence(S.ResidentialTier(p)));
            if(tenants.Length>0)Para(detail,"住户："+string.Join("、",tenants.Select(t=>S.CityPerson(t.PersonId)?.Name??t.PersonId))+"\n保留住户，不可直接搬入；购买产权后，从下一月账期收租。",17);
            if(selfOwned)
            {
                HousingButton(detail,"出售自住房…",()=>ConfirmHousing(parent,p,"出售自住房",-S.HomePurchasePrice.Scale(.9),"按当前估值九折出售，之后按当周月租继续租住。",S.SellHome));
            }
            else if(property!=null)
            {
                Line(detail,tenants.Length>0?"下个账期应收月租 "+Money.FromCents(property.MonthlyRentCents):"暂无实际租户，不产生租金",17,Ui.Accent);
                if(tenants.Length==0)HousingButton(detail,"搬入自有空房…",()=>ConfirmHousing(parent,p,"搬入自有住宅",Money.FromYuan(500),"不付首月租金，产权转为自住房；家具与储物保留。",()=>S.RentResidentialHome(id)),S.ResidentialTradeBlocker(id,false));
                HousingButton(detail,"出售住宅产权…",()=>ConfirmHousing(parent,p,"出售出租住宅",-S.PropertySaleNet(property),"包含估值折价与交易成本，原NPC住户保留。",()=>S.SellResidentialInvestment(id)));
            }
            else
            {
                if(tenants.Length==0)
                {
                    HousingButton(detail,"租住这套住宅…",()=>ConfirmHousing(parent,p,"确认租住",S.ResidentialRent(p)+Money.FromYuan(500),"首月租金 "+S.ResidentialRent(p)+" + 搬家费¥500，家具与储物保留。",()=>S.RentResidentialHome(id)),S.ResidentialTradeBlocker(id,false),"ResidenceRent");
                    HousingPurchaseButtons(detail,parent,p,true);
                }
                if(p!=S.Home)HousingPurchaseButtons(detail,parent,p,false);
            }
            HelpLink(detail,"住宅产权与月租","住宅只支持全款购买。售价与新租月租每周分别浮动，已签租约保留原价。空房可租住或买来自住；有NPC登记住址的住宅不能搬入，可带住户购买产权。出租房购买收2%交易费，原住户不搬家。按套计租，从下一月账期收款，空房无租金。月维护费为购入价0.1%。");
        }
        private void HousingPurchaseButtons(Transform parent,Window window,Place p,bool selfUse)
        {
            Line(parent,selfUse?"买来自住":"购买产权（保留住户 / 空置持有）",18,Ui.Accent);
            HousingButton(parent,"全款 · "+S.ResidentialPurchaseDue(p.Id,false,selfUse)+"…",()=>ConfirmHousing(window,p,selfUse?"购买并搬入":"购买出租住宅产权",S.ResidentialPurchaseDue(p.Id,false,selfUse),
                "房价 "+S.ResidentialPrice(p)+(selfUse?"\n异地自住含¥500搬家费，不收首月租金。":"\n包含2%交易费，原住户及地址保留。")
                +"\n全款购买。\n月维护 "+S.ResidentialPrice(p).Scale(.001),
                ()=>S.BuyResidentialHome(p.Id,false,selfUse)),S.ResidentialTradeBlocker(p.Id,true,false,selfUse),selfUse?"ResidenceBuyCash":null);
        }
        private void ConfirmHousing(Window parent,Place p,string title,Money due,string terms,Func<bool> action)
        {
            bool submitted=false;
            OpenWindow(title,760,560,confirm=>
            {
                Line(confirm.Content,p.Name,24,Ui.Accent);Para(confirm.Content,terms,19);
                Line(confirm.Content,(due.IsNegative?"本次收回 "+due.Abs():"本次支付 "+due)+" · 办理后现金 "+(S.Ledger.Balance-due),21,Ui.Accent);
                Ui.Size(Ui.Button(confirm.Content,"确认办理",()=>{if(submitted)return;submitted=true;if(action()){confirm.Close();parent.Rebuild();}else{submitted=false;confirm.Rebuild();}},20,Ui.ButtonHot,S.Ledger.Balance>=due),-1,48).name="ResidenceConfirm";
                Ui.Size(Ui.Button(confirm.Content,"取消",confirm.Close,19),-1,44);
            });
        }
    }
}
