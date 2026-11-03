using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using PCShopSell.Game.Runtime;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _districtStoryWindow;
        private readonly Dictionary<string,Button> _districtPersonMarks=new Dictionary<string,Button>();
        private void UpdateDistrictPersonMarkers()
        {
            var node=S.CurrentDistrictNode;var cam=_ctrl.Cam;
            foreach(var mark in _districtPersonMarks.Values)mark.gameObject.SetActive(false);
            if(node==null||cam==null||_ctrl.InRoom)return;
            foreach(string id in node.People.Concat(S.DistrictStoryState.MeetingPeople).Distinct())
            {
                string objective=S.DistrictPersonObjective(id);var state=S.CityPersonState(id);
                if(objective==null||state==null)continue;
                bool inside=S.CityPersonInside(state);
                var point=state.Route!=null?state.Route.Position(S.Clock.Now.TotalMinutes+S.Time.FractionalMinute)
                    :new PCShopSell.Core.World.MapPoint(state.X,state.Y);
                if(inside && state.LocationPlaceId!=null && S.City.HasPlace(state.LocationPlaceId))point=S.StandOf(S.City.GetPlace(state.LocationPlaceId));
                Vector3 sp=cam.WorldToViewportPoint(StreetWorldBuilder.ToWorld(point,3.3f));
                if(sp.z<=0||sp.x<0||sp.x>1||sp.y<0||sp.y>1)continue;
                if(!_districtPersonMarks.TryGetValue(id,out var button))
                {
                    string person=id;
                    button=Ui.Button(_hudCanvas.transform,"",()=>
                    {
                        if(S.CitySamePlace(person))OpenPersonDialogue(person);
                        else OpenDistrictStory();
                    },18,new Color(.96f,.71f,.12f,.98f));
                    button.name="DistrictPersonMarker_"+id;
                    var rt=(RectTransform)button.transform;rt.anchorMin=rt.anchorMax=Vector2.zero;rt.pivot=new Vector2(.5f,0);
                    rt.sizeDelta=new Vector2(210,62);_districtPersonMarks[id]=button;
                    button.GetComponentInChildren<Text>().color=new Color(.12f,.08f,.01f);
                }
                button.GetComponentInChildren<Text>().text="!  "+(S.CityPerson(id)?.Name??id)+"\n"+(inside?"主线 · 在此建筑内":objective);
                // Viewport anchors remain stable across zoom, camera canvases and capture resolutions.
                var markerRect=(RectTransform)button.transform;
                markerRect.anchorMin=markerRect.anchorMax=new Vector2(sp.x,sp.y);
                markerRect.anchoredPosition=Vector2.zero;
                button.gameObject.SetActive(true);button.transform.SetAsLastSibling();
            }
        }

        private void BuildDistrictMessage(Transform content)
        {
            var node=S.CurrentDistrictNode;
            if(node==null)return;
            Ui.Size(Ui.Button(content,"主线消息 · "+node.Title,OpenDistrictStory,18,Ui.ButtonHot),-1,46).name="DistrictStoryMessage";
        }
        public void OpenDistrictStory()
        {
            S.RefreshDistrictNarrative();
            if(_districtStoryWindow!=null){_districtStoryWindow.Rebuild();return;}
            _districtStoryWindow=OpenWindow("主线 · 街区引荐",820,700,w=>
            {
                var node=S.CurrentDistrictNode;var state=S.DistrictStoryState;
                Line(w.Content,"道路已开放至："+GameSession.DistrictName(S.DistrictAccessTier),20,Ui.Good);
                if(node==null)
                {
                    if(state.PendingChapter>0)
                    {
                        Para(w.Content,"已达到引荐条件，对方会在3个游戏日后主动联系你。剩余约 "+System.Math.Max(0,state.InvitationDueAt-S.Clock.Now.TotalMinutes)+" 游戏分钟；可以继续正常经营。",18);
                        return;
                    }
                    Para(w.Content,S.DistrictAccessTier==3?"三段引荐已完成，可以继续经营和人物探索。":S.DistrictPrerequisiteBlocker(state.Chapter+1)??"继续经营，留意新的聊天消息。",18);
                    return;
                }
                Line(w.Content,node.Title,25,Ui.Accent);
                var body=Ui.Scroll(w.Content,430);
                var person=node.People.FirstOrDefault();
                if(person!=null)PortraitCard(body,person,S.CityPerson(person)?.Name??person,"",84,()=>{},tagText:null);
                Para(body,node.Dialogue,19);
                Para(body,S.DistrictMeetingDescription,17,Ui.Good);
                Para(body,S.DistrictNodeTerms,17);
                if(node.Id!="community.search")
                {
                    string placeId=state.MeetingPlaceId??node.PlaceId;
                    if(S.City.HasPlace(placeId))
                        Ui.Size(Ui.Button(body,"前往会面地点门前 · "+S.City.GetPlace(placeId).Name,()=>
                        {
                            CloseAllWindows();S.BeginCityTravel(placeId,enter:false);
                        },18,Ui.ButtonHot),-1,44).name="DistrictGoToMeeting";
                }

                if(state.ProjectPartnerId=="life_clothier")Para(body,"袁岚保留原店，宋棉自愿参加本次试点；后续履约按宋棉的合作安排核验。",16,Ui.Good);
                if(node.Id=="garden.proposal")
                {
                    Line(body,"选择合作试点门店",18,Ui.Accent);
                    var shops=S.MyShops.Where(shop=>S.DistrictTierOf(shop.Place)==2&&!shop.Sim.NotFitted).ToArray();
                    if(shops.Length==0)Para(body,"先在公园中介租铺并布置开业，再回来选择。",16,Ui.Warn);
                    foreach(var shop in shops)
                    {
                        string shopId=shop.Place.Id;
                        Ui.Size(Ui.Button(body,(state.ProjectShopId==shopId?"已选 · ":"")+S.ShopTitle(shop)+" · "+shop.Place.Name,()=>{S.SelectDistrictProjectShop(shopId);w.Rebuild();},16),-1,40);
                    }
                }
                if(node.Id=="community.search")
                {
                    for(int i=0;i<3;i++)
                    {
                        int lead=i;string id=GameSession.CommunityLeadPerson(i);
                        Line(body,(S.CityPerson(id)?.Name??id)+" · "+GameSession.CommunityPieceName(i),19,Ui.Accent);
                        Para(body,S.CommunityLeadStatus(i),17);
                        bool remote=!S.CitySamePlace(id);string why=remote?S.RemoteConversationBlocker(id):null;
                        if(why!=null)Para(body,"先当面结识此人；可在人物列表查看位置。"+why,15,Ui.Dim);
                        Ui.Size(Ui.Button(body,"找他聊聊 / 打听图页",()=>OpenPersonDialogue(id,parent:w,remote:remote),16,Ui.ButtonHot,why==null),-1,38);
                        Ui.Size(Ui.Button(body,"邀请："+CityLifeCatalog.Activity(GameSession.CommunityLeadActivity(lead)).Name,()=>OpenCityActivityOffer(GameSession.CommunityLeadActivity(lead),id),16,Ui.ButtonHot,why==null),-1,38);
                    }
                    Para(body,"取得线索后，对应箱子才会出现。关闭窗口，亲自到藏点靠近箱子按 F。三页可按任意顺序寻找。",17,Ui.Good);
                    Ui.Size(Ui.Button(body,"把三份图页拼成完整商铺图",()=>{S.AssembleCommunityMap();w.Rebuild();},18,Ui.ButtonHot,state.CommunityPieces==7),-1,44).name="CommunityAssembleMap";
                }
                else if(state.MeetingNodeId!=node.Id)
                {
                    if(node.Choices.Length==0)
                    {
                        string blocker=node.Id=="community.trial"?(state.HasCommunityMap?null:"请先找回手绘商铺图"):null;
                        if(blocker!=null)Para(body,blocker,16,Ui.Warn);
                        Ui.Size(Ui.Button(body,node.Id=="community.trial"?"预约交还商铺图":"确认约见",()=>{S.BeginDistrictMeeting(node.Id);w.Rebuild();},18,Ui.ButtonHot,blocker==null),-1,44).name="DistrictAcceptMeeting";
                    }
                    foreach(var option in node.Choices)
                    {
                        var choice=option;
                        Ui.Size(Ui.Button(body,choice.Label,()=>{S.BeginDistrictMeeting(node.Id,choice.Id);w.Rebuild();},17,Ui.ButtonHot),-1,44).name="DistrictChoice_"+choice.Id;
                    }
                }
                else
                {
                    string why=S.DistrictMeetingBlocker;
                    string action=node.Id=="community.meeting"?"接受寻物委托"
                        :node.Id=="community.trial"?"交还手绘商铺图，完成引荐"
                        :node.Id=="model.repair"?(state.RepairPaid?"领取修复展样":"确认交易并委托修复 ¥1,800")
                        :node.Id=="model.delivery"?"交付展样并结清报销"
                        :node.Id=="park.delivery"||node.Id=="model.dinner"?(state.WorkStartedAt==0?"开始现场参与":"确认完成（已参与 "+state.WorkMinutes+"/30 分钟）")
                        :node.Id=="garden.terms"?"确认条款并支付约定费用"
                        :node.Id=="garden.fulfilment"?"核验试点履约并完成引荐":"当面确认并继续";
                    if(why!=null)Para(body,why,17,Ui.Warn);
                    Ui.Size(Ui.Button(body,action,()=>{S.ActDistrictNode(node.Id);w.Rebuild();},18,Ui.ButtonHot,why==null),-1,46).name="DistrictCompleteNode";
                }
                Ui.Size(Ui.Button(w.Content,"刷新进度",w.Rebuild,16),-1,36);
                Para(w.Content,"会面需自行前往。此窗口打开时暂停时间；等人、参与活动或等待修复时请先关闭窗口。",15,Ui.Dim);
            },onClosed:()=>_districtStoryWindow=null);
        }

#if UNITY_EDITOR
        private void BuildCommunityConditionsCheat(Transform content,System.Action rebuild)
        {
            string why=S.DebugCommunityConditionsBlocker;
            Ui.Size(Ui.Button(content,"三人寻物：一键满足好感和娱乐条件",()=>{S.DebugPrepareCommunityConditions();rebuild();},17,Ui.ButtonHot,why==null),-1,48)
                .name="CheatCommunityConditions";
            Para(content,why??"仅补齐三人的好感与娱乐条件、通讯录；需自己打听线索、找箱子、拼图和交付。",15,Ui.Dim);
        }

        private void OpenDistrictAcceptance()
        {
            OpenWindow("三个关键主线 · 前置条件验收",780,660,w=>
            {
                var body=Ui.Scroll(w.Content,560);
                Para(body,"一键准备条件 → 3个游戏日后人物主动联系并弹出邀请 → 回复消息、完成会面与合作 → 对应道路放行。重复点击不重置计时。",18);
                string[] labels={"① 城郊 → 社区：5家店＋现金100万","② 社区 → 公园：6家店＋净资产3000万＋上市凭据","③ 公园 → 花园：单行业月收入1亿＋股价100＋次月"};
                for(int stage=1;stage<=3;stage++)
                {
                    int chapter=stage;string why=S.DebugDistrictPrerequisitesBlocker(stage);
                    Ui.Size(Ui.Button(body,labels[stage-1],()=>{S.DebugPrepareDistrictPrerequisites(chapter);w.Rebuild();},17,Ui.ButtonHot,why==null),-1,58).name="DistrictPrerequisites_"+stage;
                    if(why!=null)Line(body,why,15,Ui.Dim);
                }
                Para(body,"前两段补足真实门店与资金；第三段另准备1家公园试点店和最低10万元周转金。重复点击只补缺口，不重复创建门店。",16);
                Para(body,"上市、行业月账与股价尚无完整正式机制，本工具写入明确的编辑器验收凭据，不伪造真实月营收。正常发行版不接受这些凭据作为晋升前提。",16,Ui.Warn);
                BuildCommunityConditionsCheat(body,w.Rebuild);
                Line(body,"当前通行："+GameSession.DistrictName(S.DistrictAccessTier),18,Ui.Good);
                Ui.Size(Ui.Button(body,"打开聊天，查看主线消息",()=>{w.Close();OpenChatPanel();},18),-1,44);
                Para(body,"会改变当前存档的资产与进度，建议先保存一份验收前的手动档。不会删除已有资产或关系。",15,Ui.Dim);
            });
        }
#endif
    }
}
