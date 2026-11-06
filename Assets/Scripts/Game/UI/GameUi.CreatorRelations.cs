using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildMcnHeader(Window w)
        {
            var c=BusinessCard(w.Content,"创作工作室 · 地位 "+S.BusinessStanding+"/100");
            if(!S.HasMcn)
            {
                Para(c,"公园阶段开放自办MCN，需门店与知名度25。设立¥50,000；基础人员¥60/天，每位签约创作者另加¥30/天。合约90天，广告收入分成20%。",15,Ui.Dim);
                string why=S.McnFoundBlocker;if(why!=null)Para(c,why,16,Ui.Dim);
                BusinessAction(c,"成立MCN公司 · ¥50,000",S.FoundMcn,w,why==null);
            }
            else
            {
                Line(c,$"工作室运营中 · 当前人员日薪 {Money.FromYuan(S.McnDailyWages)}",17);
                Para(c,"广告收入受粉丝、资源与商务关系影响，分成进入总部账本；每日消耗资源 1。分成不是利润，还要扣人员、签约与赞助支出。",15,Ui.Dim);
                Ui.Size(Ui.Button(c,"关闭工作室…",()=>ConfirmBusiness("关闭工作室","先结束所有创作者合约；设立费用不退。关闭后停止人员费用。",()=>{S.CloseMcn();w.Rebuild();}),16,Ui.Warn),-1,36);
            }
        }
        private void OpenCreatorRelations(string id,Window parent)
        {
            OpenWindow(Creators.Get(id).Name+" · MCN合作",760,450,w=>
            {
                var c=S.Creator(id);var d=Creators.Get(id);
                Line(w.Content,d.Style,21,Ui.Accent);
                Line(w.Content,$"商务交情 {c.Relationship}/100 · 抱负 {c.Ambition}/100",18);
                var contract=BusinessCard(w.Content,"MCN 合作");
                if(c.McnUntilDay>S.Clock.Today.TotalDays)
                {
                    Line(contract,$"剩余 {c.McnUntilDay-S.Clock.Today.TotalDays} 天 · 本期分成收入 {Money.FromCents(c.McnIncomeCents)}",17);
                    Ui.Size(Ui.Button(contract,$"提前解约… 补偿 {S.CreatorFee(c)}",()=>ConfirmBusiness("提前解约","支付所示解约补偿，商务交情 −5。",()=>{S.EndMcnContract(id);w.Rebuild();parent.Rebuild();}),17,Ui.Warn),-1,40);
                }
                else
                {
                    string why=S.McnContractBlocker(c);if(why!=null)Para(contract,why,16,Ui.Dim);
                    BusinessAction(contract,$"签约 90 天 · {S.CreatorFee(c)*5}",()=>{bool ok=S.SignMcnCreator(id);parent.Rebuild();return ok;},w,why==null);
                }
                Ui.Size(Ui.Button(w.Content, "返回交谈", w.Close, 17), -1, 40);
            });
        }
    }
}
