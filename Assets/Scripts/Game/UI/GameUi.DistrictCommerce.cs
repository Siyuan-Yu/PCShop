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
            _brandTab=S.DistrictStoryState.Chapter==2?BrandExpoTab:BrandTenderTab;
            OpenBrandBusiness();
        }
        private void BuildDistrictCommerceProgress(Transform body,Window w)
        {
            var state=S.DistrictStoryState;
            Para(body,S.DistrictCommerceStatus,18,Ui.Accent);
            foreach(string step in S.DistrictCommerceSteps.Where(step=>step==state.NodeId))
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
            if(S.CurrentDistrictNode.Id=="expo.reception"&&S.CurrentPlace?.Id!="life_expo")
                Ui.Size(Ui.Button(body,"前往会展中心",()=>{CloseAllWindows();S.BeginCityTravel("life_expo");},17),-1,40);
            if(S.CurrentDistrictNode.Id=="tender.prepare")
            {
                var tender=S.BrandBusiness.Tenders.Where(t=>t.BrandId==S.DistrictStoryState.CommerceBrandId)
                    .OrderByDescending(t=>t.Deadline).FirstOrDefault();
                if(tender!=null)
                {
                    if((tender.Intel&1)==0)BuildDistrictPersonNavigation(body,"life_enterprise");
                    if((tender.Intel&2)==0)BuildDistrictPersonNavigation(body,"life_tenant");
                }
            }
            Para(body,"进度来自实际经营记录。完成后自动进入下一步；最后仍需亲自会见推荐人才能开放道路。",16,Ui.Dim);
            Ui.Size(Ui.Button(w.Content,"刷新主线进度",()=>{S.RefreshDistrictNarrative();w.Rebuild();},17),-1,38);
        }
    }
}
