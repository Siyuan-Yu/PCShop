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
        private RectTransform _communityRevealOverlay;
        private CanvasGroup _communityRevealTitle;
        private bool[] _communityRevealCanvasStates;

        public void BeginCommunityReveal()
        {
            var canvases = new[] { _hudCanvas, _dockCanvas, _labelCanvas, _invitationCanvas, _toastCanvas };
            _communityRevealCanvasStates = canvases.Select(c => c.enabled).ToArray();
            foreach (var canvas in canvases) canvas.enabled = false;
            _communityRevealOverlay = Ui.Panel(_modalCanvas.transform, "CommunityUnlock", new Color(0, 0, 0, .06f)).rectTransform.Fill();
            var title = Ui.Panel(_communityRevealOverlay, "Title", new Color(.07f, .11f, .10f, .86f)).rectTransform;
            title.Anchor(.19f, .12f, .81f, .32f, 0, 0, 0, 0);
            _communityRevealTitle = title.gameObject.AddComponent<CanvasGroup>();
            _communityRevealTitle.alpha = 0;
            var text = Ui.Label(title, "<size=28>第二章</size>\n社区已解锁", 56, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.Fill(16);
            var hint = Ui.Label(_communityRevealOverlay, "Esc 跳过", 16, TextAnchor.MiddleRight, Color.white);
            hint.rectTransform.Anchor(.78f, 0, 1, 0, 0, 16, 24, -50);
        }

        public void SetCommunityRevealTitle(float alpha)
        {
            _communityRevealTitle.alpha = alpha;
            if (_communityRevealOverlay.GetSiblingIndex() < _modalCanvas.transform.childCount - 1)
                _communityRevealOverlay.SetAsLastSibling();
        }

        public void EndCommunityReveal()
        {
            if (_communityRevealOverlay == null) return;
            Kill.It(_communityRevealOverlay.gameObject);
            _communityRevealOverlay = null;
            _communityRevealTitle = null;
            var canvases = new[] { _hudCanvas, _dockCanvas, _labelCanvas, _invitationCanvas, _toastCanvas };
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = _communityRevealCanvasStates[i];
            _communityRevealCanvasStates = null;
        }

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
                var point = S.DistrictPersonPosition(state);
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
                if (S.FirstChapterTrialCompleted)
                {
                    Para(content, "试玩主线已完成 · 可以留在城郊继续经营。", 18, Ui.Good);
                    return;
                }
                if (S.DistrictInvitationStatus == null)
                {
                    if (GameSession.FirstChapterTrial && S.CurrentQuest()?.Target != QuestTarget.CommunityGoal)
                    {
                        Line(content, "城郊试玩主线", 19, Ui.Accent);
                        ProgressLine(content, Quests.Get("community_goal"), 16);
                    }
                    return;
                }
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
            _districtStoryWindow=OpenWindow(GameSession.FirstChapterTrial ? "城郊经营目标" : "主线 · 街区引荐",820,700,w=>
            {
                var node=S.CurrentDistrictNode;var state=S.DistrictStoryState;
                Line(w.Content,"道路已开放至："+GameSession.DistrictName(S.DistrictAccessTier),20,Ui.Good);
                if(node==null)
                {
                    if (S.FirstChapterTrialCompleted)
                    {
                        Para(w.Content, "试玩主线已完成", 24, Ui.Good);
                        Para(w.Content, "已达成：城郊至少 5 家开业门店、自有现金 100 万元、无未偿还贷款。", 18);
                        Para(w.Content, "你可以继续在城郊经营。后续片区在完整版中开放。", 18);
                        Ui.Size(Ui.Button(w.Content, "继续经营", w.Close, 18, Ui.ButtonHot), -1, 42);
                        return;
                    }
                    if(state.PendingChapter>0)
                    {
                        Para(w.Content,S.DistrictInvitationStatus+$"\n从首次达标起等待满 {Ui.Emphasize($"{DistrictNarrativeCatalog.InvitationDelayDays * 24} 小时")}；到期时若正在睡觉，会改为醒来 2 小时后主动联系。资金随后减少不会取消邀请。关闭窗口后时间才会继续。",18);
                        return;
                    }
                    Para(w.Content,S.DistrictAccessTier==3?"三段引荐已完成，可以继续经营和人物探索。":S.DistrictPrerequisiteBlocker(state.Chapter+1)??"继续经营，留意新的主线邀请。",18);
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
                    Para(body,$"回复后，亲自找到{Ui.Emphasize("陈启明")}，请他帮忙约{Ui.Emphasize("唐蔚")}。约好后才会显示会面时间和地点。",17);
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
                        : node.Id=="community.trial" ? "唐蔚会到城郊中介旁收取图页。若她还在忙，请关闭窗口等待约见安排。" : node.Id=="community.meeting" ? "联系陈启明，请他帮忙约唐蔚出来。" : "联系对方约好时间，再前往会面点。",19);
                    if(state.MeetingNodeId==node.Id)Para(body,S.DistrictMeetingDescription,17,Ui.Good);
                    if(state.MeetingNodeId==node.Id)Para(body,S.DistrictNodeTerms,17);
                }
                if(node.Id!="community.search"&&state.MeetingNodeId!=node.Id)
                {
                    var contact=node.Id=="community.trial"?null:S.DistrictMeetingContact ?? DistrictNavigationContact(node.People);
                    if(contact!=null)BuildDistrictPersonNavigation(body,contact, allowPhone: true);
                    if (node.Id == "community.trial" && S.City.HasPlace(node.PlaceId))
                        Ui.Size(Ui.Button(body, "前往城郊中介等候", () => NavigateToDistrictMeeting(node.PlaceId),
                            18, Ui.ButtonHot), -1, 44);
                }
                if(node.Id!="community.search"&&state.MeetingNodeId==node.Id&&!S.AtCurrentDistrictMeeting)
                {
                    string placeId=state.MeetingPlaceId??node.PlaceId;
                    if(S.City.HasPlace(placeId))
                        Ui.Size(Ui.Button(body,(S.DistrictMeetingOutdoors?"前往街边会面点 · ":"前往会面地点门前 · ")+S.City.GetPlace(placeId).Name,
                            ()=>NavigateToDistrictMeeting(placeId),18,Ui.ButtonHot),-1,44).name="DistrictGoToMeeting";
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
                        if(completedOuting!=null)
                        {
                            Ui.Size(Ui.Button(body,"查看活动结算 · 听取藏箱线索",()=>OpenCityActivitySettlement(completedOuting.Id),16),-1,38);
                            continue;
                        }
                        var outing=S.CommunityLeadAppointment(lead);
                        if(!S.CommunityHasOuting(lead)&&outing!=null&&S.City.HasPlace(outing.VenueId))
                        {
                            string venue=outing.VenueId;
                            Para(body,"已约好："+S.City.GetPlace(venue).Name+" · "+CityTime(outing.ScheduledStart),16,Ui.Good);
                            if(S.CurrentPlace?.Id!=venue)
                                Ui.Size(Ui.Button(body,"前往活动地点 · "+S.City.GetPlace(venue).Name,()=>GoToScheduledCityActivity(outing.Id),16),-1,38).name="DistrictGoToActivity_"+id;
                        }
                        else BuildDistrictPersonNavigation(body,id, allowPhone: true);
                    }
                }
                else if(node.Id!="community.trial" || state.MeetingNodeId==node.Id)
                {
                    string who=state.MeetingNodeId!=node.Id?(S.CityPerson(person)?.Name??person):(S.CityPerson(S.DistrictMeetingContact)?.Name??S.DistrictMeetingContact);
                    Para(body,state.MeetingNodeId!=node.Id
                        ? "联系"+who+"商量本次会面。"
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

        private string DistrictContactHint(string id, string name) => S.IsMet(id)
            ? $"可在手机通讯录给{name}发消息，也可以当面聊。"
            : $"先当面找{name}。认识后也可用手机通讯录联系。";

        private void BuildDistrictPersonNavigation(Transform body,string id,bool allowPhone = false)
        {
            var person=S.CityPersonState(id);if(person==null)return;
            string name=S.CityPerson(id)?.Name??id;
            if (allowPhone)
                Para(body, DistrictContactHint(id, name), 17);
            if(S.CitySamePlace(id))
            {
                Para(body,name+"就在附近。关闭任务窗口，走到本人身边按 F 交谈。",16,Ui.Good);return;
            }
            var place = S.DistrictPersonCurrentPlace(person);
            string blocker = S.DistrictAccessBlocker(place);
            if (place != null)
            {
                Para(body, $"{name}目前在{place.Name}。", 17, Ui.Good);
                if (blocker != null) Para(body, GameSession.DistrictName(S.DistrictTierOf(place)) + "片区尚未开放。", 16, Ui.Warn);
                else if (place.Kind == PCShopSell.Core.World.PlaceKind.Home && place != S.Home)
                    Para(body,S.HomeVisitBlocker(place)??"到门口按 F 查看住户，再进屋拜访。",15,Ui.Dim);
            }
            else
            {
                var pos = S.DistrictPersonPosition(person);
                var landmark=S.City.Places.OrderBy(p=>
                {
                    var point=S.StandOf(p);return (point.X-pos.X)*(point.X-pos.X)+(point.Y-pos.Y)*(point.Y-pos.Y);
                }).FirstOrDefault();
                Para(body,name+"目前在："+(landmark?.Name??"街道")+"附近"+(person.Route!=null?"，正在途中。":"。"),17,Ui.Good);
            }
            Ui.Size(Ui.Button(body,"前往寻找 · "+name,()=>NavigateToDistrictPerson(id),17,Ui.ButtonHot,blocker == null)
                .Tip(blocker),-1,40).name="DistrictFindPerson_"+id;
            if (person.Route != null)
                Para(body, "对方仍在移动，导航使用出发时的位置。", 15, Ui.Dim);
        }

        private void NavigateToDistrictMeeting(string placeId)
        {
            CloseAllWindows();
            if (placeId == S.DistrictNodePlaceId && S.DistrictMeetingStreetNode is string streetNode) S.GoTo(streetNode);
            else S.BeginCityTravel(placeId, enter: false);
        }

        private void NavigateToDistrictPerson(string id)
        {
            if (S.GoToDistrictPerson(id)) CloseAllWindows();
        }

        // Main-quest actions belong to the existing character conversation, never the tracker.
        private void BuildQuestItems(Window w)
        {
            var state=S.DistrictStoryState;
        }

        private string DistrictDialogueContext(string personId, bool remote)
        {
            var node = S.CurrentDistrictNode;
            if (node == null || node.Id == "community.intro" || S.CommunitySearchActive || S.DistrictCommerceGameplay)
                return null;

            var state = S.DistrictStoryState;
            bool isBroker = node.Id == "community.meeting" && personId == "keeper_agent";
            if (!isBroker && personId != S.DistrictMeetingContact && !state.MeetingPeople.Contains(personId))
                return null;

            if (state.MeetingNodeId != node.Id)
            {
                return node.Id switch
                {
                    "community.meeting" => "李姐跟我提过你。唐蔚最近在找几页旧街的图，我帮你约她，让她当面跟你说吧。",
                    "community.clue" => "你是来问唐蔚那张图的吧？那天搬家我在场。咱们找个时间，我慢慢跟你说。",
                    "community.trial" => "图都找齐了？我一会儿过去，咱们在中介旁边碰面。",
                    _ => "这事当面说方便些。咱们约个时间吧。"
                };
            }

            string time = DistrictSpeechTime(state.MeetingAt);
            if (!S.AtCurrentDistrictMeeting)
                return isBroker ? DistrictBookedSpeech(node.Id)
                    : $"咱们约的是{time}，在{DistrictSpeechMeetingPlace(state)}碰面。到那儿再细说。";
            if (remote)
                return "这事等见面再细聊吧。";

            var waiting = state.MeetingPeople.Where(id => id != personId)
                .Where(id => S.CityPersonState(id)?.Route != null
                    || S.CityPersonState(id)?.LocationPlaceId != state.MeetingPlaceId)
                .Select(id => S.CityPerson(id)?.Name ?? "另一位").ToArray();
            string waitingFor = string.Join("、", waiting);
            if (S.Clock.Now.TotalMinutes < state.MeetingAt)
                return waiting.Length > 0
                    ? $"你来得早。我跟{waitingFor}约的是{time}，人还在路上，先在这儿等会儿吧。"
                    : $"你来得早。咱们约的是{time}，先在这儿等会儿吧。";
            if (waiting.Length > 0)
                return $"{waitingFor}还在路上。咱们再等一会儿，人到齐了再聊。";
            if (S.DistrictMeetingBlocker != null)
                return "先在这儿等会儿吧，人到齐了再说。";

            if (isBroker) return "唐蔚到了。你过去跟她聊吧，我就不打扰了。";
            if (personId != S.DistrictMeetingContact) return "人齐了，咱们说正事吧。";
            return node.Id switch
            {
                "community.meeting" => DistrictCharacterLine(node, personId)
                    ?? "你好，我是唐蔚。陈启明跟我说过你，有件事正想请你帮忙。",
                "community.clue" => "你来了。唐蔚把找图的事跟你说了吧？那天搬家的事，我还记得。",
                "community.trial" => "你把图带来了？拿给我看看，三页都在吗？",
                _ => DistrictCharacterLine(node, personId) ?? "你来了，咱们说正事吧。"
            };
        }

        private string DistrictSpeechTime(long minute)
        {
            var time = new PCShopSell.Core.Time.GameTimestamp(minute);
            int days = time.Date.TotalDays - S.Clock.Today.TotalDays;
            string day = days == 0 ? "今天" : days == 1 ? "明天" : days == 2 ? "后天" : days == -1 ? "昨天" : time.Date.ToString();
            string period = time.Hour < 6 ? "凌晨" : time.Hour < 12 ? "上午" : time.Hour < 13 ? "中午" : time.Hour < 19 ? "下午" : "晚上";
            int hour = (time.Hour + 11) % 12 + 1;
            return day + period + hour + "点" + (time.Minute == 0 ? "" : time.Minute + "分");
        }

        private string DistrictSpeechMeetingPlace(DistrictProgress state)
        {
            if (!S.City.HasPlace(state.MeetingPlaceId)) return "约好的地方";
            string place = S.City.GetPlace(state.MeetingPlaceId).Name;
            return place + (S.DistrictMeetingOutdoors ? "旁边的空地" : "门口");
        }

        private string DistrictBookedSpeech(string nodeId)
        {
            var state = S.DistrictStoryState;
            if (state.MeetingNodeId != nodeId) return "行，那就改天再说。";
            string time = DistrictSpeechTime(state.MeetingAt);
            string place = DistrictSpeechMeetingPlace(state);
            return nodeId == "community.meeting"
                ? $"我跟唐蔚说好了，{time}在{place}见。她会过去，你到时候找她就行。"
                : $"那就说定了，{time}在{place}见。";
        }

        private string DistrictCharacterLine(DistrictNarrativeNode node, string personId)
        {
            string name = S.CityPerson(personId)?.Name;
            if (string.IsNullOrEmpty(name)) return null;
            string speaker = name + "：";
            int start = node.Dialogue.IndexOf(speaker, System.StringComparison.Ordinal);
            if (start < 0) return null;
            start += speaker.Length;
            int end = node.Dialogue.Length;
            foreach (string otherId in node.People.Where(id => id != personId))
            {
                string otherName = S.CityPerson(otherId)?.Name;
                if (string.IsNullOrEmpty(otherName)) continue;
                int next = node.Dialogue.IndexOf(otherName + "：", start, System.StringComparison.Ordinal);
                if (next >= 0 && next < end) end = next;
            }
            return node.Dialogue.Substring(start, end - start).Trim();
        }

        private string DistrictConversationResult(DistrictNarrativeNode node, string personId) => node.Id switch
        {
            "community.meeting" => "那就拜托你了。先去问问李秀兰吧，那几个箱子托给了谁，她最清楚。",
            "community.clue" => DistrictCharacterLine(node, personId)
                ?? "三页没丢，我、林远航和周维成各收着一页。我把他俩的号码发你了，你可以跟他们约时间。",
            "community.trial" => DistrictCharacterLine(node, personId) ?? "三页都齐了。谢谢你，我这就把那些小店补进名册。",
            _ => "好，那就这么定了。"
        };

        private void AddDistrictDialogue(List<TalkOpt> options,string personId,bool remote,bool near,System.Action<string> reply)
        {
            var node=S.CurrentDistrictNode;var state=S.DistrictStoryState;
            if(node==null||node.Id=="community.intro"||S.CommunitySearchActive||S.DistrictCommerceGameplay)return;
            if(personId!=S.DistrictMeetingContact)return;
            string local=remote||!near?"请走到本人身边，当面商谈。":null;
            if(S.DistrictCommerceActive&&node.Id.EndsWith(".invitation"))
                foreach(var brand in S.BrandBusiness.Brands)
                {
                    string id=brand.Id;
                    options.Add(new TalkOpt{Label="商定参与品牌："+brand.Name,Mainline=true,Blocker=local,
                        Act=()=>reply(S.SelectDistrictCommerceBrand(id)?"已选定本次参与的品牌。":"该品牌尚不满足条件。")});
                }
            if(state.MeetingNodeId!=node.Id)
            {
                string bookingBlocker = remote ? S.RemoteConversationBlocker(personId) : near ? null : "对方已离开。";
                if(node.Choices.Length==0)
                    options.Add(new TalkOpt{Label=node.Id=="community.meeting"?"请陈启明约唐蔚":"约见面",Mainline=true,Invitation=true,Blocker=bookingBlocker,
                        Act=()=>reply(S.BeginDistrictMeeting(node.Id) ? DistrictBookedSpeech(node.Id) : "这会儿约不上，咱们晚点再试吧。")});
                foreach(var option in node.Choices)
                {
                    var choice=option;
                    string label=choice.Label;
                    options.Add(new TalkOpt{Label=label,Mainline=true,Hot=true,Blocker=bookingBlocker,
                        Act=()=>reply(S.BeginDistrictMeeting(node.Id, choice.Id) ? DistrictBookedSpeech(node.Id) : "这会儿约不上，咱们晚点再试吧。")});
                }
                return;
            }
            if(!S.AtCurrentDistrictMeeting && S.City.HasPlace(state.MeetingPlaceId))
            {
                string placeId=state.MeetingPlaceId;
                options.Add(new TalkOpt{Label="前往约定会面点 · "+S.City.GetPlace(placeId).Name,Mainline=true,Hot=true,Act=()=>
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
            string meetingBlocker = S.DistrictMeetingBlocker;
            string unavailable = meetingBlocker == null ? S.DistrictCommerceBlocker
                : S.Clock.Now.TotalMinutes < state.MeetingAt ? "会面时间还没到" : "会面条件还没满足";
            options.Add(new TalkOpt{Label=action,Mainline=true,Hot=true,SeparateBlocker=true,Blocker=local??unavailable,
                Act=()=>
                {
                    if (!S.ActDistrictNode(node.Id))
                    {
                        reply("这事还没办妥，咱们一会儿再说。");
                        return;
                    }
                    reply(DistrictConversationResult(node, personId));
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
                string[] labels={"① 城郊 → 社区：5家店＋自有现金100万","② 社区 → 公园：品牌展会与订单交付","③ 公园 → 花园：企业竞标与三期交付"};
                for(int stage=1;stage<=3;stage++)
                {
                    int chapter=stage;string why=S.DebugDistrictAcceptanceBlocker(stage);
                    Ui.Size(Ui.Button(body,labels[stage-1],()=>{S.DebugEnterDistrictAcceptance(chapter);w.Rebuild();},17,Ui.ButtonHot,why==null),-1,58).name="DistrictPrerequisites_"+stage;
                    if(why!=null)Line(body,why,15,Ui.Dim);
                }
                Para(body,"第一段准备5家城郊店和100万元自有现金；第二段准备6家社区店、3000万元净资产和真实品牌；第三段准备公园供货店、知名度100的品牌和10万元周转金。重复点击只补缺口，不重复创建门店，也不重置当前任务进度。",16);
                Para(body,"后两段验收会准备3种商品各至少150件。第三段仅跳过行业月营收的入场门槛，不生成虚构营业收入；参展、签约代言人、竞标和交付仍由玩家完成。",16,Ui.Warn);
                BuildCommunityConditionsCheat(body,w.Rebuild);
                Line(body,"当前通行："+GameSession.DistrictName(S.DistrictAccessTier),18,Ui.Good);
                Ui.Size(Ui.Button(body,"查看主线邀请",()=>{w.Close();OpenDistrictStory();},18),-1,44);
                Para(body,"在当前档准备验收场景：保留已有资产与关系，跳过此前任务；不会替你完成选中的这一段。",15,Ui.Dim);
            });
        }
#endif
    }
}
