using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenCityStories()
        {
            string selected="supply", shopId=S.MyShops.FirstOrDefault()?.Place.Id;
            OpenWindow("城市履历 · 人脉、经营与生活",1080,790,w=>
            {
                var tabs=Ui.Row(w.Content,42);
                foreach(string key in new[]{"supply","creator","property","romance"})
                {
                    string id=key;var progress=S.CityStory(id);
                    string label=id=="supply"?"供应与履约":id=="creator"?"员工与创作":id=="property"?"商铺与街坊":"个人相处（可选）";
                    Ui.Size(Ui.Button(tabs,label,()=>{selected=id;w.Rebuild();},17,selected==id?Ui.ButtonHot:Ui.ButtonBg),-1,42,1);
                }
                var story=S.CityStory(selected);if(story==null)return;
                Line(w.Content,GameSession.CityStoryTitle(selected),25,Ui.Accent);
                Line(w.Content,"当前："+GameSession.CityStoryStageName(selected,story.Stage),21,Ui.Good);
                Para(w.Content,S.CityStoryObjective(selected),18);
                Para(w.Content,"每条路线都可稍后再做。普通经营、采购和日常生活不依赖完成故事；接受邀约不会提前发放费用、好感或经营收益。",15,Ui.Dim);
                if(story.Failed)Para(w.Content,"这次尝试未完成，可以按实际条件补救后再试；已发生费用与真实履历保留。",16,Ui.Warn);
                var body=Ui.Scroll(w.Content,430);
                if(selected=="supply")
                {
                    if(story.Stage==0)CityStoryPersonActions(body,story.PersonId,w,"电子市场供应商");
                    else if(story.Stage==1)BuildStoryActivityActions(body,story,w,"家常饭局引荐");
                    else if(story.Stage==2||(story.Stage==3&&story.Failed)||story.Stage>=5)
                    {
                        CityStoryPersonActions(body,"life_enterprise",w,"企业联系人沈远");
                        string block=S.CitySupplyTrialBlocker();if(block!=null)Para(body,block,16,Ui.Dim);
                        Ui.Size(Ui.Button(body,story.Stage>=5?"确认新的两台复订试单…":"确认两台试单…",()=>ConfirmAction("企业试单", "2台入门电脑，按目标电脑店正常报价，先付5%保证金。需要真实采购、装机和交付，未履约部分按原合同处理。", "确认签约",()=>{S.AcceptCitySupplyTrial();w.Rebuild();}),18,Ui.ButtonHot,block==null),-1,46);
                    }
                    else if(story.Stage==3)
                    {
                        var order=S.EnterpriseContracts.FirstOrDefault(c=>c.Id==story.OrderSource);
                        if(order!=null)Para(body,$"{order.Client}：已交付 {order.Completed}/{order.Count} · 未履约 {order.Failed} · 交期 {new GameTimestamp(order.DueMinute)}",18,Ui.Accent);
                        Ui.Size(Ui.Button(body,"查看采购与补货",OpenCityProcurement,18),-1,44);
                        if(S.City.HasPlace(story.ShopId))Ui.Size(Ui.Button(body,"返回承担交付的店",()=>{CloseModalWindows();S.BeginCityTravel(story.ShopId);},18),-1,44);
                    }
                    else if(story.Stage==4)BuildStoryActivityActions(body,story,w,"电子展合作");
                }
                else if(selected=="creator")
                {
                    if(story.Stage<=2)
                    {
                        Para(body,"许明想攒工具、顾宁在读夜校、乔禾希望搬家后有稳定工作。故事在应聘前后都能继续，不以入职或花钱买关系代替了解。",17);
                        CityStoryPersonActions(body,story.PersonId,w,"员工或可招聘朋友");
                        Ui.Size(Ui.Button(body,"员工管理 / 正常招聘入口",OpenEmployeesPanel,17),-1,42);
                    }
                    else if(story.Stage==3)BuildStoryActivityActions(body,story,w,"原创漫展同行");
                    else
                    {
                        CityStoryPersonActions(body,"life_creator",w,"陆栀 · 27岁原创创作者");
                        foreach(var shop in S.MyShops)
                        {
                            var target=shop;
                            Ui.Size(Ui.Button(body,(shopId==shop.Place.Id?"✓ ":"")+S.ShopTitle(shop),()=>{shopId=target.Place.Id;w.Rebuild();},17),-1,38);
                        }
                        string block=shopId==null?"先有一家正常经营门店":S.CityPromotionBlocker("life_creator",shopId);
                        if(block!=null)Para(body,block,16,Ui.Dim);
                        if(story.Stage==4||(story.Stage==5&&story.Failed)||story.Stage>=6)
                            Ui.Size(Ui.Button(body,"签5天整店线上推广 · ¥300…",()=>ConfirmAction("原创创作者推广", "只为选定门店增加最多25%实际询价客流，不直接给钱。成交仍需要现货、定价、货架和真实值守；24笔期间成交后停止额外流量。此费用不增加私人好感。", "确认合作",()=>{S.StartCityCreatorPromotion(shopId);w.Rebuild();}),18,Ui.ButtonHot,block==null),-1,46);
                        var promotion=S.CityPromotions.FirstOrDefault(c=>c.Id==story.CampaignId);
                        if(promotion!=null)BuildCityPromotionStatus(body,promotion);
                    }
                }
                else if(selected=="property")
                {
                    if(story.Stage==0||story.Stage==2)CityStoryPersonActions(body,Rosters.AgentChen.Id,w,"中介陈姐");
                    else if(story.Stage==1)CityStoryPersonActions(body,"life_tenant",w,"文创街租户袁岚");
                    else if(story.Stage==3)
                    {
                        if(S.City.HasPlace(story.PropertyId))Para(body,"已了解的物业："+S.CityPropertyAddress(S.City.GetPlace(story.PropertyId))+"。带租约购买保留原租户。",18,Ui.Accent);
                        Ui.Size(Ui.Button(body,"查看正常物业交易",OpenCityProperties,18,Ui.ButtonHot),-1,46);
                        CityStoryPersonActions(body,Rosters.AgentChen.Id,w,"到中介核对并办理");
                    }
                    else if(story.Stage==4)BuildStoryActivityActions(body,story,w,"租户与创作者饭局");
                    else {CityStoryPersonActions(body,"life_tenant",w,"租户生活继续");Ui.Size(Ui.Button(body,"查看已持有物业",OpenCityProperties,18),-1,44);}
                }
                else
                {
                    Para(body,"陆栀是原创成年角色。你可以只做朋友和合作伙伴，感情路线不影响普通经营与其他人物机会。",17,Ui.Dim);
                    CityStoryPersonActions(body,"life_creator",w,"陆栀 · 私人相处");
                    if(story.Stage==1)Ui.Size(Ui.Button(body,"当面听一段个人经历",()=>OpenPersonDialogue("life_creator",parent:w,firstReply:S.HearCityPersonalStory("life_creator")),18,Ui.ButtonHot,S.CitySamePlace("life_creator")),-1,44);
                    if(story.Stage==2)BuildStoryActivityActions(body,story,w,"河岸相处");
                    if(story.Stage==3)
                    {
                        Ui.Size(Ui.Button(body,"当面表达：继续朋友与合作",()=>{S.ChooseCityRomance(false);w.Rebuild();},18,Ui.ButtonHot,S.CitySamePlace("life_creator")),-1,44);
                        Ui.Size(Ui.Button(body,"当面表达：认真发展感情…",()=>ConfirmAction("个人关系选择", "双方都是成年人。你希望与陆栀确认恋爱关系，商业合同与费用仍独立。", "确认表达",()=>{S.ChooseCityRomance(true);w.Rebuild();}),18,Ui.ButtonHot,S.CitySamePlace("life_creator")),-1,44);
                    }
                    if(story.Stage>=4)Para(body,S.CityPersonalFeedback("life_creator"),18,Ui.Good);
                }
                foreach(var referral in S.CityReferrals.Where(o=>o.ContractId==null))
                {
                    var offer=referral;
                    Para(body,"转介绍："+offer.Client+" · "+offer.Count+"台 · "+(offer.Discount?"正常报价95%":"正常报价"),17,Ui.Accent);
                    Ui.Size(Ui.Button(body,"核对并确认真实订单…",()=>ConfirmAction("转介绍试单", "按电脑店现行整机报价确认，支付5%保证金；仍需库存、装机和真实交付，不直接领取所谓净收益。", "确认合同",()=>{S.AcceptCityReferral(offer.Id);w.Rebuild();}),17,Ui.ButtonHot),-1,42);
                }
                if (!GameSession.FirstChapterTrial) Para(w.Content,S.CityExpoCalendar,15,Ui.Dim);
            });
        }
        private void CityStoryPersonActions(Transform content,string personId,Window parent,string role)
        {
            var person=S.CityPerson(personId);if(person==null){Para(content,"人物资料暂不可用，既有记录保留。",17);return;}
            Line(content,person.Name+" · "+role+" · 熟悉 "+S.Relation(personId)+" / 履约信任 "+S.CityTrust(personId),18,Ui.Accent);
            var row=Ui.Row(content,42);
            Ui.Size(Ui.Button(row,"当面聊天",()=>OpenCityStoryConversation(personId,parent),18,Ui.ButtonHot,S.CitySamePlace(personId)),-1,42,1);
            string destination=S.CityStoryMeetingPlace(personId);
            Ui.Size(Ui.Button(row,"前往已知见面地点",()=>{CloseModalWindows();S.BeginCityTravel(destination);},17,Ui.ButtonBg,destination!=null),-1,42,1);
            if(!S.CitySamePlace(personId))Para(content,"交谈需要实际遇见对方。工作地点按营业与班次见面；未了解的私人地址不会作为追踪目的地。",15,Ui.Dim);
        }
        private void OpenCityStoryConversation(string personId,Window parent) => OpenPersonDialogue(personId,parent:parent);
        private void BuildStoryActivityActions(Transform content,CityStoryProgress story,Window parent,string title)
        {
            var activity=S.CityActivities.FirstOrDefault(a=>a.Id==story.ActivityId);
            if(activity!=null)
            {
                Para(content,title+" · "+GameSession.CityPhaseText(activity.State)+" · "+new GameTimestamp(activity.ScheduledStart),18,Ui.Accent);
                if(activity.EndReason!=null)Para(content,activity.EndReason,16,Ui.Dim);
                if(activity.Active)
                {
                    Ui.Size(Ui.Button(content,"实际前往赴约",()=>{CloseModalWindows();S.GoToCityActivity(activity.Id);},18,Ui.ButtonHot),-1,46);
                    Ui.Size(Ui.Button(content,"取消/改约…",()=>ConfirmAction("取消日程", "尚未发生的费用不会扣除，已经发生的消费保留。下次仍可重新安排这一步。", "确认取消",()=>{S.CancelCityActivity(activity.Id);parent.Rebuild();}),17),-1,42);return;
                }
            }
            Ui.Size(Ui.Button(content,"预约"+title+"…",()=>ConfirmAction("预约实际活动", "双方按真实路线前往。预约只记入日程；不会提前消费或增加关系。若同行朋友是当班员工，本次确认同时批准其活动时段离岗；唯一当班员工离开后将暂停相应店内服务。活动中世界经营继续，展会按固定月历安排。", "确认预约并批准同行离岗",()=>{S.ScheduleCityStoryActivity(story.Id,false,true);parent.Rebuild();}),18,Ui.ButtonHot),-1,46);
        }
        private void BuildCityPromotionStatus(Transform content,CityPromotion promotion)
        {
            Para(content,"推广 "+promotion.Id+" · "+promotion.State+" · 到 "+new GameTimestamp(promotion.End),17,Ui.Accent);
            Para(content,"目标门店 "+(S.ShopAt(promotion.ShopId) is OwnedShop shop?S.ShopTitle(shop):promotion.ShopId)+" · 整店现货 · "+promotion.Audience
                +"\n期间实际成交 "+promotion.SalesCount+" 笔 / "+Money.FromCents(promotion.SalesCents)+"（含自然客流，不当作净增收益）",16);
        }
    }
}
