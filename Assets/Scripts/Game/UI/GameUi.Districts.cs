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
            foreach(string id in node.People.Concat(new[]{S.DistrictMeetingContact}.Where(id=>id!=null)).Concat(S.DistrictStoryState.MeetingPeople).Distinct())
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
            if(node==null)
            {
                if(S.DistrictInvitationStatus==null)return;
                Para(content,"主线 · 等待联系\n"+S.DistrictInvitationStatus,18,Ui.Accent);
                Ui.Size(Ui.Button(content,"查看主线等待详情",OpenDistrictStory,18),-1,40).name="DistrictStoryMessage";
                return;
            }
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
                    if (S.FirstChapterTrialCompleted)
                    {
                        Para(w.Content, "试玩主线已完成 · 已向唐蔚交付三页商铺图。你可以继续在城郊经营。", 18, Ui.Good);
                        return;
                    }
                    if(state.PendingChapter>0)
                    {
                        Para(w.Content,S.DistrictInvitationStatus+"\n从首次达标起等待满72小时；到期时若正在睡觉，会改为醒来2小时后主动联系。资金随后减少不会取消邀请。关闭窗口后时间才会继续。",18);
                        return;
                    }
                    Para(w.Content,S.DistrictAccessTier==3?"三段引荐已完成，可以继续经营和人物探索。":S.DistrictPrerequisiteBlocker(state.Chapter+1)??"继续经营，留意新的聊天消息。",18);
                    return;
                }
                Line(w.Content,node.Title,25,Ui.Accent);
                var body=Ui.Scroll(w.Content,430);
                if(S.DistrictCommerceGameplay){BuildDistrictCommerceGameplay(body,w);return;}
                if(S.DistrictCommerceActive)BuildDistrictCommerceProgress(body,w);
                var person=S.DistrictMeetingContact ?? (state.MeetingNodeId==node.Id?node.People.FirstOrDefault():DistrictNavigationContact(node.People));
                if(node.Id!="community.search"&&person!=null)PortraitCard(body,person,S.CityPerson(person)?.Name??person,"",84,()=>{},tagText:null);
                if(node.Id=="community.intro")
                {
                    Line(body,"李秀兰发来的消息 · 等待你的回复",18,Ui.Good);
                    Para(body,node.Dialogue,19);
                    Para(body,"回复后，亲自找到陈启明，请他帮忙约唐蔚。约好后才会显示会面时间和地点。",17);
                    Ui.Size(Ui.Button(body,"回复李秀兰：好，麻烦你帮我联系陈启明",()=>
                    {
                        S.BeginDistrictMeeting(node.Id,"accept");w.Rebuild();
                    },18,Ui.ButtonHot),-1,46).name="DistrictChoice_accept";
                    Ui.Size(Ui.Button(body,"暂不回复，稍后再看",w.Close,17),-1,42).name="DistrictChoice_later";
                    Para(w.Content,"这是主线消息回复。尚未约好见面，不用现在去中介；邀请会保留。",16,Ui.Dim);
                    return;
                }
                if(node.Id!="community.search")
                {
                    if(S.AtCurrentDistrictMeeting && S.DistrictMeetingBlocker==null)
                        Para(body,node.Dialogue,19);
                    else Para(body,state.MeetingNodeId==node.Id
                        ? (S.AtCurrentDistrictMeeting?"你已到达会面点，请留意下方约定时间。":"已约好见面。请按约定前往，双方到场后再交谈。")
                        : node.Id=="community.trial" ? "唐蔚会到城郊中介旁收取图页。若她还在忙，请关闭窗口等待约见安排。" : node.Id=="community.meeting" ? "李秀兰已介绍陈启明给你。先亲自找到陈启明，请他帮忙约唐蔚出来。" : "先找到对方当面商量；确认约见后再前往会面点。",19);
                    if(state.MeetingNodeId==node.Id)Para(body,S.DistrictMeetingDescription,17,Ui.Good);
                    if(state.MeetingNodeId==node.Id)Para(body,S.DistrictNodeTerms,17);
                }
                if(node.Id!="community.search"&&state.MeetingNodeId!=node.Id)
                {
                    var contact=node.Id=="community.trial"?null:S.DistrictMeetingContact ?? DistrictNavigationContact(node.People);
                    if(contact!=null)BuildDistrictPersonNavigation(body,contact);
                }
                if(node.Id!="community.search"&&state.MeetingNodeId==node.Id&&!S.AtCurrentDistrictMeeting)
                {
                    string placeId=state.MeetingPlaceId??node.PlaceId;
                    if(S.City.HasPlace(placeId))
                        Ui.Size(Ui.Button(body,(S.DistrictMeetingOutdoors?"前往街边会面点 · ":"前往会面地点门前 · ")+S.City.GetPlace(placeId).Name,()=>
                        {
                            CloseAllWindows();
                            if (S.DistrictMeetingStreetNode is string streetNode) S.GoTo(streetNode);
                            else S.BeginCityTravel(placeId,enter:false);
                        },18,Ui.ButtonHot),-1,44).name="DistrictGoToMeeting";
                }
                if(node.Id=="community.search")
                {
                    for(int i=0;i<3;i++)
                    {
                        if(S.CommunityHasPiece(i))continue;
                        int lead=i;string id=GameSession.CommunityLeadPerson(i);
                        Line(body,(S.CityPerson(id)?.Name??id)+" · "+GameSession.CommunityPieceName(i),19,Ui.Accent);
                        Para(body,S.CommunityLeadStatus(i),17);
                        if (S.CommunityBoxAvailable(lead))
                            Ui.Size(Ui.Button(body, "前往藏箱地点", () =>
                            {
                                CloseAllWindows();
                                S.GoTo(S.CommunityBoxNode(lead));
                            }, 16), -1, 38);
                        if(S.CommunityHasClue(lead))continue;
                        var completedOuting=S.CommunityLeadCompletedActivity(lead);
                        if(completedOuting!=null&&S.Relation(id)>=GameSession.CommunityLeadRelation)
                        {
                            Ui.Size(Ui.Button(body,"查看活动结算 · 听取藏箱线索",()=>OpenCityActivitySettlement(completedOuting.Id),16),-1,38);
                            continue;
                        }
                        var outing=S.CityActivities.FirstOrDefault(a=>a.Active&&a.VenueId==GameSession.CommunityLeadVenue(lead)
                            &&a.Participants.Any(p=>p.PersonId==id));
                        if(!S.CommunityHasOuting(lead)&&outing!=null&&S.City.HasPlace(outing.VenueId))
                        {
                            string venue=outing.VenueId;
                            Para(body,"已约好活动："+S.City.GetPlace(venue).Name+"，"+CityTime(outing.ScheduledStart)+"。到场后选择“"+CityLifeCatalog.Activity(GameSession.CommunityLeadActivity(lead)).Name+"”并完成活动。",16,Ui.Good);
                            if(S.CurrentPlace?.Id!=venue)
                                Ui.Size(Ui.Button(body,"前往活动地点 · "+S.City.GetPlace(venue).Name,()=>{CloseAllWindows();S.BeginCityTravel(venue,enter:false);},16),-1,38).name="DistrictGoToActivity_"+id;
                        }
                        else BuildDistrictPersonNavigation(body,id);
                    }
                }
                else if(node.Id!="community.trial" || state.MeetingNodeId==node.Id)
                {
                    string who=state.MeetingNodeId!=node.Id?(S.CityPerson(person)?.Name??person):(S.CityPerson(S.DistrictMeetingContact)?.Name??S.DistrictMeetingContact);
                    Para(body,state.MeetingNodeId!=node.Id
                        ? "找到"+who+"，在人物对话中商量本次会面。"
                        : "到场后与"+who+"交谈，完成当前事项。任务页不会代替对话或交付。",17,Ui.Good);
                    if(S.AtCurrentDistrictMeeting)
                    {
                        string why=S.DistrictMeetingBlocker??S.DistrictCommerceBlocker;
                        if(why!=null)Para(body,why,17,Ui.Warn);
                    }
                }
                Ui.Size(Ui.Button(w.Content,"刷新进度",()=>{S.RefreshDistrictNarrative();w.Rebuild();},16),-1,36);
                Para(w.Content,"会面需自行前往。此窗口打开时暂停时间；等人时请先关闭窗口。",15,Ui.Dim);
            },onClosed:()=>_districtStoryWindow=null);
        }


        private string DistrictNavigationContact(IEnumerable<string> people) => people.Where(id=>S.CityPersonState(id)!=null).OrderBy(id=>
        {
            var p=S.CityPersonState(id);string location=p.LocationPlaceId??p.Route?.TargetPlaceId;
            return S.City.HasPlace(location)&&S.DistrictAccessBlocker(S.City.GetPlace(location))!=null?1:0;
        }).FirstOrDefault();

        private void BuildDistrictPersonNavigation(Transform body,string id)
        {
            var person=S.CityPersonState(id);if(person==null)return;
            string name=S.CityPerson(id)?.Name??id;
            if(S.CitySamePlace(id))
            {
                Para(body,name+"就在附近。关闭任务窗口，走到本人身边交谈。",16,Ui.Good);return;
            }
            var waitingHome=S.DistrictPersonWaitingPlace(id);
            if(waitingHome!=null)
            {
                string location=person.LocationPlaceId??person.Route?.TargetPlaceId;
                int end=S.CityPerson(id)?.WorkEnd??1080;
                bool waitingAtWork=waitingHome.Id==person.WorkPlaceId&&location==person.HomePlaceId;
                if(waitingAtWork)
                {
                    int start=S.CityPerson(id)?.WorkStart??540;
                    Para(body,name+"现在在家，当前关系还不能入户。可以在上班时间到"+waitingHome.Name+"找他，不必为交付另外提高好感。",17,Ui.Good);
                    Para(body,"通常工作时间 "+(start/60)+":"+(start%60).ToString("00")+"–"+(end/60)+":"+(end%60).ToString("00")+"；到达后可刷新位置。",16);
                    if(S.NearbyPlace?.Id!=waitingHome.Id&&S.CurrentPlace?.Id!=waitingHome.Id)
                        Ui.Size(Ui.Button(body,"前往工作地点等候 · "+name,()=>
                        {
                            CloseAllWindows();S.BeginCityTravel(waitingHome.Id,enter:false);
                        },17,Ui.ButtonHot),-1,40).name="DistrictFindPerson_"+id;
                    return;
                }
                bool returning=person.Route?.TargetPlaceId==waitingHome.Id;
                Para(body,returning?name+"正在返回"+waitingHome.Name+"。":name+"目前在"+S.City.GetPlace(location).Name+"，该片区尚未开放。",17,returning?Ui.Good:Ui.Warn);
                Para(body,(returning?"": "通常 "+(end/60)+":"+(end%60).ToString("00")+" 下班，之后还需返程。")+"可到"+waitingHome.Name+"附近等候；刷新任务页查看他是否回来。",16);
                if(S.NearbyPlace?.Id!=waitingHome.Id&&S.CurrentPlace?.Id!=waitingHome.Id)
                    Ui.Size(Ui.Button(body,"前往住处附近等候 · "+name,()=>
                    {
                        CloseAllWindows();S.BeginCityTravel(waitingHome.Id,enter:false);
                    },17,Ui.ButtonHot),-1,40).name="DistrictFindPerson_"+id;
                return;
            }
            bool inside=S.CityPersonInside(person);
            string placeId=inside?person.LocationPlaceId:null;
            if(placeId!=null&&S.City.HasPlace(placeId))
            {
                var place=S.City.GetPlace(placeId);
                Para(body,name+"目前在："+place.Name+"内。",17,Ui.Good);
                if(place.Kind==PCShopSell.Core.World.PlaceKind.Home&&place!=S.Home)
                    Para(body,S.HomeVisitBlocker(place)??"到门口按 F 查看住户，再进屋拜访。",15,Ui.Dim);
            }
            else
            {
                var pos=person.Route?.Position(S.Clock.Now.TotalMinutes+S.Time.FractionalMinute)
                    ??new PCShopSell.Core.World.MapPoint(person.X,person.Y);
                var landmark=S.City.Places.OrderBy(p=>
                {
                    var point=S.StandOf(p);return (point.X-pos.X)*(point.X-pos.X)+(point.Y-pos.Y)*(point.Y-pos.Y);
                }).FirstOrDefault();
                Para(body,name+"目前在："+(landmark?.Name??"街道")+"附近"+(person.Route!=null?"，正在途中。":"。"),17,Ui.Good);
            }
            Ui.Size(Ui.Button(body,"前往寻找 · "+name,()=>
            {
                // Re-read when clicked; this is walking/navigation, never a dialogue or teleport.
                var current=S.CityPersonState(id);if(current==null)return;
                CloseAllWindows();
                if(S.CityPersonInside(current)&&S.City.HasPlace(current.LocationPlaceId))
                    S.BeginCityTravel(current.LocationPlaceId,enter:false);
                else
                {
                    var pos=current.Route?.Position(S.Clock.Now.TotalMinutes+S.Time.FractionalMinute)
                        ??new PCShopSell.Core.World.MapPoint(current.X,current.Y);
                    S.GoTo(S.City.NearestNode(pos.X,pos.Y));
                }
            },17,Ui.ButtonHot),-1,40).name="DistrictFindPerson_"+id;
            if(person.Route!=null)Para(body,"人物仍会移动；到达后可刷新位置继续寻找。",15,Ui.Dim);
        }

        // Main-quest actions belong to the existing character conversation, never the tracker.
        private void BuildQuestItems(Window w)
        {
            var state=S.DistrictStoryState;
        }

        private string DistrictDialogueContext(string personId)
        {
            var node=S.CurrentDistrictNode;
            if(node==null||(!node.People.Contains(personId)&&personId!=S.DistrictMeetingContact)||node.Id=="community.intro"
                ||S.CommunitySearchActive||S.DistrictCommerceGameplay)return null;
            var state=S.DistrictStoryState;
            if(state.MeetingNodeId!=node.Id)
                return "主线 · "+node.Title+"\n"+node.Dialogue+"\n\n"+S.DistrictNodeTerms;
            return "主线 · "+node.Title+"\n"+S.DistrictCurrentGoal+"\n\n本次确认联系人："+(S.CityPerson(S.DistrictMeetingContact)?.Name??"联系人")
;
        }

        private void AddDistrictDialogue(List<TalkOpt> options,string personId,bool remote,bool near,System.Action<string> reply)
        {
            var node=S.CurrentDistrictNode;var state=S.DistrictStoryState;
            if(node==null||node.Id=="community.intro"||S.CommunitySearchActive||S.DistrictCommerceGameplay)return;
            if(node.People.Contains(personId)||personId==S.DistrictMeetingContact)
            {
                options.Add(new TalkOpt{Label="主线 · 这次需要我做什么？",Hot=true,
                    Act=()=>reply(S.DistrictCurrentGoal+"\n\n下一步："+S.DistrictNextAction)});
                if(personId!=S.DistrictMeetingContact)
                {
                    string contact=S.DistrictMeetingContact;
                    options.Add(new TalkOpt{Label="主线 · 找"+(S.CityPerson(contact)?.Name??"联系人")+"继续",Hot=true,Act=()=>
                    {
                        if(!remote&&S.CitySamePlace(contact)){CloseAllWindows();OpenPersonDialogue(contact);}
                        else reply("本次由"+(S.CityPerson(contact)?.Name??"联系人")+"确认，请走到对方身边交谈。\n"+S.DistrictMeetingDescription);
                    }});
                }
            }
            if(personId!=S.DistrictMeetingContact)return;
            string local=remote||!near?"请走到本人身边，当面商谈。":null;
            if(S.DistrictCommerceActive&&node.Id.EndsWith(".invitation"))
                foreach(var brand in S.BrandBusiness.Brands)
                {
                    string id=brand.Id;
                    options.Add(new TalkOpt{Label="商定参与品牌："+brand.Name,Blocker=local,
                        Act=()=>reply(S.SelectDistrictCommerceBrand(id)?"已选定本次参与的品牌。":"该品牌尚不满足条件。")});
                }
            if(state.MeetingNodeId!=node.Id)
            {
                if(node.Choices.Length==0)
                    options.Add(new TalkOpt{Label=node.Id=="community.meeting"?"请帮我约唐蔚出来见面":"商量主线会面："+node.Title,Hot=true,Blocker=local,
                        Act=()=>reply(S.BeginDistrictMeeting(node.Id)?S.DistrictMeetingDescription:"暂时无法安排，请先结束冲突的活动。")});
                foreach(var option in node.Choices)
                {
                    var choice=option;
                    string label=choice.Label;
                    options.Add(new TalkOpt{Label=label,Hot=true,Blocker=local,
                        Act=()=>reply(S.BeginDistrictMeeting(node.Id,choice.Id)?S.DistrictMeetingDescription:"暂时无法安排本次会面。")});
                }
                return;
            }
            if(!S.AtCurrentDistrictMeeting && S.City.HasPlace(state.MeetingPlaceId))
            {
                string placeId=state.MeetingPlaceId;
                options.Add(new TalkOpt{Label="前往约定会面点 · "+S.City.GetPlace(placeId).Name,Hot=true,Act=()=>
                {
                    CloseAllWindows();
                    if(S.DistrictMeetingStreetNode is string streetNode)S.GoTo(streetNode);
                    else S.BeginCityTravel(placeId,enter:false);
                }});
                return;
            }
            string action=node.Id=="community.meeting"?"接受寻找三页商铺图的委托"
                :node.Id=="community.clue"?"询问三页图分别由谁保管"
                :node.Id=="community.trial"?"交付三份图页，完成引荐"
                :node.Id.EndsWith(".invitation")?"接受邀请，开始准备"
                :"确认履约记录，完成引荐";
            options.Add(new TalkOpt{Label=action,Hot=true,SeparateBlocker=true,Blocker=local??S.DistrictMeetingBlocker??S.DistrictCommerceBlocker,
                Act=()=>
                {
                    if(!S.ActDistrictNode(node.Id)){reply("暂时无法完成，请核对现场条件。" );return;}
                    reply(S.CurrentDistrictNode?.Id==node.Id?S.DistrictNodeTerms
                        :node.Dialogue+"\n\n"+(S.CurrentDistrictNode==null?"本段主线已完成。":"下一步："+S.CurrentDistrictNode.Title));
                }});
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
            OpenWindow("区域主线 · 直接进入验收",780,660,w=>
            {
                var body=Ui.Scroll(w.Content,560);
                Para(body,"可直接选择任意一段：自动完成此前主线、开放此前区域、补齐本段门店与资金，并立即收到邀请。回复消息后亲自验收本段任务，完成后才开放下一区域。",18);
                string[] labels={"① 城郊 → 社区：5家店＋现金100万","② 社区 → 公园：品牌展会与订单交付","③ 公园 → 花园：企业竞标与三期交付"};
                for(int stage=1;stage<=3;stage++)
                {
                    int chapter=stage;string why=S.DebugDistrictAcceptanceBlocker(stage);
                    Ui.Size(Ui.Button(body,labels[stage-1],()=>{S.DebugEnterDistrictAcceptance(chapter);w.Rebuild();},17,Ui.ButtonHot,why==null),-1,58).name="DistrictPrerequisites_"+stage;
                    if(why!=null)Line(body,why,15,Ui.Dim);
                }
                Para(body,"第一段准备5家城郊店和100万元；第二段准备6家社区店、3000万元净资产和真实品牌；第三段准备公园供货店、知名度100的品牌和10万元周转金。重复点击只补缺口，不重复创建门店，也不重置当前任务进度。",16);
                Para(body,"后两段验收会准备3种商品各至少150件。第三段仅跳过行业月营收的入场门槛，不生成虚构营业收入；参展、签约代言人、竞标和交付仍由玩家完成。",16,Ui.Warn);
                BuildCommunityConditionsCheat(body,w.Rebuild);
                Line(body,"当前通行："+GameSession.DistrictName(S.DistrictAccessTier),18,Ui.Good);
                Ui.Size(Ui.Button(body,"打开聊天，查看主线消息",()=>{w.Close();OpenChatPanel();},18),-1,44);
                Para(body,"在当前档准备验收场景：保留已有资产与关系，跳过此前任务；不会替你完成选中的这一段。",15,Ui.Dim);
            });
        }
#endif
    }
}
