using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public bool DistrictCommerceActive=>_district.Chapter>=2&&CurrentDistrictNode!=null;
        public bool DistrictCommerceGameplay=>DistrictCommerceActive&&_district.NodeId!="expo.invitation"&&_district.NodeId!="expo.review"&&_district.NodeId!="tender.invitation"&&_district.NodeId!="tender.review";
        public BrandRecord DistrictCommerceBrand=>Brand(_district.CommerceBrandId)?.Copy();
        public string[] DistrictCommerceSteps=>_district.Chapter==2?ExpoStoryChain.ToArray():TenderStoryChain.ToArray();
        private BrandOrder ExpoMainlineReceipt(BrandExhibit e) => e==null?null:_brands.Orders.FirstOrDefault(o=>o.Id==e.Id+":lead"&&o.BrandId==e.BrandId&&o.Source=="expo");
        private BrandExhibit DistrictBestExpo => _brands.Exhibits.Where(e=>e.BrandId==_district.CommerceBrandId&&e.Finished&&!e.Missed)
            .OrderByDescending(e=>e.Score).ThenByDescending(e=>e.Day).FirstOrDefault();
        private BrandExhibit DistrictCommerceExpo
        {
            get
            {
                var exhibits=_brands.Exhibits.Where(e=>e.BrandId==_district.CommerceBrandId&&!e.Missed).ToArray();
                // An earned qualification takes priority over a newer or higher-scoring unfinished attempt.
                return exhibits.Where(e=>e.MainlineQualified).OrderByDescending(e=>e.Score).ThenByDescending(e=>e.Day).FirstOrDefault()
                    ?? exhibits.Where(e=>e.Finished&&ExpoMainlineReceipt(e)!=null&&!ExpoMainlineReceipt(e).Failed)
                        .OrderByDescending(e=>ExpoMainlineReceipt(e).Done).ThenByDescending(e=>e.Score).ThenByDescending(e=>e.Day).FirstOrDefault()
                    ?? exhibits.Where(e=>!e.Finished).OrderByDescending(e=>e.Day).FirstOrDefault()
                    ?? DistrictBestExpo;
            }
        }
        private void RecordExpoMainlineQualifications()
        {
            // Record achievements even before this story is accepted, and never revoke an earned result.
            foreach(var e in _brands.Exhibits.Where(e=>e.Finished&&!e.Missed&&!e.MainlineQualified))
                if(ExpoMainlineReceipt(e)?.Done==true && Math.Max(e.InfluenceAfter,Brand(e.BrandId)?.Influence??0)>=100)
                    e.MainlineQualified=true;
        }
        private BrandTender DistrictCommerceTender=>_brands.Tenders.Where(t=>t.BrandId==_district.CommerceBrandId).OrderByDescending(t=>t.Stage==3).ThenByDescending(t=>t.Deadline).FirstOrDefault();
        private BrandOrder DistrictExpoReceipt=>ExpoMainlineReceipt(DistrictCommerceExpo);
        private bool DistrictBrandRevenueQualified(BrandRecord b)=>BrandRevenue(MyShops.Where(s=>s.RetailKind==b.Kind),(Clock.Today.TotalDays/30-1)*30,Clock.Today.TotalDays/30*30)>=10000000000L;
        private void StartDistrictCommerce()
        {
            if(Brand(_district.CommerceBrandId)==null)_district.CommerceBrandId=_brands.Brands.Where(b=>_district.Chapter!=3||DistrictBrandRevenueQualified(b)).OrderByDescending(b=>b.Influence).FirstOrDefault()?.Id;
        }
        public bool SelectDistrictCommerceBrand(string id)
        {
            if(!DistrictCommerceActive||!_district.NodeId.EndsWith(".invitation"))return false;
            var b=Brand(id);if(b==null||(_district.Chapter==3&&b.Id!=_district.CommerceBrandId&&!DistrictBrandRevenueQualified(b)))return Fail("请选择满足本段引荐条件的品牌");
            _district.CommerceBrandId=id;return true;
        }
        public string DistrictCommerceBlocker
        {
            get
            {
                if(!DistrictCommerceActive)return null;
                var b=Brand(_district.CommerceBrandId);if(b==null)return "请先选择本次引荐的品牌";
                var e=DistrictCommerceExpo;var t=DistrictCommerceTender;
                if(_district.Chapter==3&&t?.Stage==4&&!_district.NodeId.EndsWith(".invitation"))return "本轮未能保住席位；主线保留，下年度重新报名、试单和履约";
                switch(_district.NodeId)
                {
                    case "expo.prepare": return e!=null&&!e.Missed?null:"到品牌展会页准备摊位、每种100件展品与已签约KOL；错过本届可报名下季度";
                    case "expo.reception": return e?.Finished==true?null:e?.Missed==true?"本届未完成，保留主线，下一季度重新参展":"开幕当天进入会展中心，连续4天参与事件与舞台互动";
                    case "expo.delivery": case "expo.review":
                        if(e?.Finished!=true||DistrictExpoReceipt?.Done!=true)return DistrictExpoReceipt==null&&e?.Finished==true?"本届没有获得采购订单，下季度改善现场表现后重新参展":DistrictExpoReceipt?.Failed==true?"展会订单已逾期，下季度参展取得新订单后继续":"请实际交付本品牌的展会线索订单；发货不等于到货";
                        return e.MainlineQualified||Math.Max(b.Influence,e.InfluenceAfter)>=100?null:"本品牌尚未达成知名度100，请继续经营；已完成的展会和订单不需重做";
                    case "tender.prepare": return t==null?"到企业竞标页报名，联系沈远和袁岚调查需求与对手，再准备报价和试单":t.Intel==3||t.Stage==2||t.Stage==3?null:"报名已完成，请联系沈远核实需求、向袁岚调查对手";
                    case "tender.trial": return _brands.Orders.Any(o=>o.Id==t?.Id+":trial"&&o.Done)?null:"提交本品牌的竞标方案，实际交付10件试单并等待到货";
                    case "tender.decision": return t?.Stage==2||t?.Stage==3?null:t?.Stage==4?"本轮竞标失败，主线保留；下一年度重新报名、试单并评审":"试单到货后提交评审，评分须超过原供应商才能取得席位";
                    case "tender.fulfilment": case "tender.review": return t?.Stage==3?null:t?.Stage==4?"本轮失去席位；下年度重新竞标，完成新合约的三期交付":"完成同一份企业合约的连续三期实际交付";
                }
                return null;
            }
        }
        public string DistrictCommerceStatus
        {
            get
            {
                var b=Brand(_district.CommerceBrandId);if(b==null)return "尚未选定品牌";
                if(_district.Chapter==2)
                {
                    var e=DistrictCommerceExpo;var o=DistrictExpoReceipt;
                    string exhibit=e==null?"尚未报名":e.Missed?"本届已错过":e.Finished?"展会已完成":e.Format==2?"4天展会 · 现场进度 "+e.Decision+"/24":"旧版展会 · 接待 "+e.Stage+"/3";
                    string delivery=o==null?"尚未取得展会订单":o.Done?"展会订单已到货结算":o.Failed?"订单已逾期":o.Shipped?"订单运输中，尚未到货":"待发货 "+o.Count+" 件 · 截止第"+(o.Due+1)+"天";
                    var best=DistrictBestExpo;
                    string history=best==null?"历史：尚未完成展会":"历史最高评分 "+best.Score+" · "+new PCShopSell.Core.Time.GameDate(best.Day);
                    string credit=e?.MainlineQualified==true?"历史成绩已达标，无需重新参展；接任务前的记录同样有效。": "接任务前的展会与交付记录同样计入；未达标可继续下一届。";
                    string selected=e==null?"":"\n当前采用："+new PCShopSell.Core.Time.GameDate(e.Day)+" · 评分 "+e.Score;
                    return b.Name+" · 当前知名度 "+b.Influence+"\n"+history+selected+"\n"+exhibit+"\n"+delivery+"\n"+credit;
                }
                var t=DistrictCommerceTender;if(t==null)return b.Name+" · 尚未报名企业竞标";
                var trial=_brands.Orders.FirstOrDefault(o=>o.Id==t.Id+":trial");
                int delivered=_brands.Orders.Count(o=>o.Id.StartsWith(t.Id+":delivery:")&&o.Done);
                return b.Name+" · 需求调查 "+((t.Intel&1)!=0?"已完成":"未完成")+" · 对手调查 "+((t.Intel&2)!=0?"已完成":"未完成")
                    +"\n试单："+(trial?.Done==true?"已到货":trial?.Shipped==true?"运输中":"待交付")+" · 我方评分 "+BrandTenderScore(t)+" / 对手 "+t.RivalScore
                    +"\n正式合约交付 "+delivered+"/3"+(string.IsNullOrEmpty(t.Result)?"":" · "+t.Result);
            }
        }
        private void RefreshDistrictCommerce()
        {
            RecordExpoMainlineQualifications();
            // Read actual business receipts; never manufacture sales, stock or influence here.
            for(int step=0;step<6&&DistrictCommerceGameplay&&DistrictCommerceBlocker==null;step++)CompleteDistrictNode();
        }
    }
}
