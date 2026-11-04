using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void DebugOpenCreator(string id) { var parent=OpenWindow("创作者",500,250,w=>{}); OpenCreatorRelations(id,parent); }
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
            if(S.RomanceId!=null)Line(c,"伴侣："+Creators.Get(S.RomanceId).Name+" · 私人关系不改变合同条款",16,Ui.Good);
        }
        private void OpenCreatorRelations(string id,Window parent)
        {
            OpenWindow(Creators.Get(id).Name+" · 合作与相处",880,790,w=>
            {
                var c=S.Creator(id);var d=Creators.Get(id);
                Line(w.Content,d.Style,21,Ui.Accent);
                Line(w.Content,$"商务交情 {c.Relationship}/100 · 私人好感 {c.Affection}/100 · 抱负 {c.Ambition}/100",18);
                var contract=BusinessCard(w.Content,"MCN 合作");
                if(c.McnUntilDay>S.Clock.Today.TotalDays)
                {
                    Line(contract,$"剩余 {c.McnUntilDay-S.Clock.Today.TotalDays} 天 · 本期分成收入 {Money.FromCents(c.McnIncomeCents)}",17);
                    Ui.Size(Ui.Button(contract,$"提前解约… 补偿 {S.CreatorFee(c)}",()=>ConfirmBusiness("提前解约","补偿一次当期推广费，商务交情 −5，私人关系不受合约控制。",()=>{S.EndMcnContract(id);w.Rebuild();parent.Rebuild();}),17,Ui.Warn),-1,40);
                }
                else
                {
                    string why=S.McnContractBlocker(c);if(why!=null)Para(contract,why,16,Ui.Dim);
                    BusinessAction(contract,$"签约 90 天 · {S.CreatorFee(c)*5}",()=>{bool ok=S.SignMcnCreator(id);parent.Rebuild();return ok;},w,why==null);
                }
                var life=BusinessCard(w.Content,"私下相处");
                Para(life,"先当面认识或建立商务交情，再预约家常饭。预计¥100、60游戏分钟，到场后才消费和增加私人好感。心事与关系选择需要实际见面；不要求每天打卡维系。",16,Ui.Dim);
                string outing=S.CreatorOutingBlocker(c);if(outing!=null)Line(life,outing,16,Ui.Dim);
                BusinessAction(life,"预约实际见面 · 到场后消费",()=>S.InviteCreatorPrivately(id),w,outing==null);
                if(S.CreatorStory(c)!=null)Ui.Size(Ui.Button(life,"听听对方最近的心事",()=>OpenCreatorStory(id,w),17,Ui.ButtonHot),-1,40);
                else Line(life,c.StoryStage>=3 ? "三段私人交谈已经完成。" : $"下一段心事在私人好感 {20+c.StoryStage*25} 解锁。",15,Ui.Dim);
                if(S.RomanceId==id)
                {
                    Line(life,"你们正在交往。长期疏远至私人好感低于 20 会结束关系。",16,Ui.Good);
                    Ui.Size(Ui.Button(life,"认真谈谈分开…",()=>ConfirmBusiness("结束私人关系","私人好感 −20；已经签署的商务合同继续履行。",()=>{S.EndRomance();w.Rebuild();parent.Rebuild();}),17,Ui.Warn),-1,40);
                }
                else
                {
                    string why=S.RomanceBlocker(c);if(why!=null)Para(life,why,16,Ui.Dim);
                    BusinessAction(life,"表达心意，确认关系",()=>{bool ok=S.StartRomance(id);parent.Rebuild();return ok;},w,why==null);
                }
            });
        }
        private void OpenCreatorStory(string id,Window parent)
        {
            var c=S.Creator(id);string story=S.CreatorStory(c);if(story==null)return;
            OpenPersonDialogue(id,parent:parent,firstReply:story,initialPage:3);
        }
    }
}
