using System.Linq;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenDistrictBrandGameplay()
        {
            _brandId=S.DistrictStoryState.CommerceBrandId;
            _brandTab=S.DistrictStoryState.Chapter==2?2:3;
            OpenBrandBusiness();
        }
        private void BuildDistrictCommerceProgress(Transform body,Window w)
        {
            var state=S.DistrictStoryState;
            if(state.NodeId.EndsWith(".invitation"))
            {
                Line(body,"本次引荐的品牌（接受邀请后固定）",17,Ui.Accent);
                foreach(var brand in S.BrandBusiness.Brands)
                {
                    string id=brand.Id;
                    Ui.Size(Ui.Button(body,brand.Name+(id==state.CommerceBrandId?" ✓":""),()=>{S.SelectDistrictCommerceBrand(id);w.Rebuild();},17,id==state.CommerceBrandId?Ui.ButtonHot:Ui.ButtonBg),-1,36);
                }
            }
            Para(body,S.DistrictCommerceStatus,18,Ui.Accent);
            foreach(string step in S.DistrictCommerceSteps)
            {
                var node=DistrictNarrativeCatalog.Nodes.First(n=>n.Id==step);
                Line(body,(S.DistrictNodeCompleted(step)?"✓ ":step==state.NodeId?"→ ":"○ ")+node.Title,16,step==state.NodeId?Ui.Accent:Ui.Dim);
            }
            Ui.Size(Ui.Button(body,state.Chapter==2?"打开本品牌 · 季度展会":"打开本品牌 · 企业竞标",OpenDistrictBrandGameplay,18,Ui.ButtonHot),-1,42);
        }
        private void BuildDistrictCommerceGameplay(Transform body,Window w)
        {
            Para(body,S.CurrentDistrictNode.Dialogue,18);
            BuildDistrictCommerceProgress(body,w);
            if(S.DistrictCommerceBlocker!=null)Para(body,S.DistrictCommerceBlocker,17,Ui.Warn);
            if(S.DistrictStoryState.Chapter==2)
                Ui.Size(Ui.Button(body,"前往会展中心",()=>{CloseAllWindows();S.BeginCityTravel("life_expo");},17),-1,40);
            else
                foreach(string person in new[]{"life_enterprise","life_tenant"})
                {
                    string id=person;
                    Ui.Size(Ui.Button(body,"联系"+S.CityPerson(id).Name,()=>OpenBrandContact(id),17),-1,36);
                }
            Para(body,"进度来自实际经营记录。完成后自动进入下一步；最后仍需亲自会见推荐人才能开放道路。",16,Ui.Dim);
            Ui.Size(Ui.Button(w.Content,"刷新主线进度",()=>{S.RefreshDistrictNarrative();w.Rebuild();},17),-1,38);
        }
    }
}
