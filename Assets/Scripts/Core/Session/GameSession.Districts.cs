using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private DistrictProgress _district = new DistrictProgress();
        private IReadOnlyList<DistrictRoadGate> _districtRoadGates;
        private int _lastDistrictGate;
        private long _lastDistrictGateMinute = -100;
        public IReadOnlyList<DistrictRoadGate> DistrictRoadGates => _districtRoadGates ?? (_districtRoadGates = DistrictRoadGate.ForCity(City));
        public int DistrictAccessTier => _district.UnlockedTier;
        public DistrictProgress DistrictStoryState => _district.Copy();
        public static string DistrictName(int tier) => new[] { "城郊", "社区", "公园", "花园" }[Math.Max(0,Math.Min(3,tier))];
        public int DistrictTierOf(Place place)
        {
            string id = City.AgencyDistrictFor(place?.StreetId)?.id;
            return id == "community" ? 1 : id == "park" ? 2 : id == "garden" ? 3 : 0;
        }
        public bool DistrictRoadOpen(int tier) => !City.IsLakeCity || tier <= DistrictAccessTier;
        public string DistrictAccessBlocker(Place place) => place != null && !DistrictRoadOpen(DistrictTierOf(place))
            ? "前方" + DistrictName(DistrictTierOf(place)) + "片区暂未开放，请先完成对应的主线引荐。手机聊天中可查看进度。" : null;
        public bool TryDistrictRoadStop(MapPoint from, MapPoint to, out MapPoint stop, out int tier)
        {
            stop = to; tier = 0;
            foreach (var gate in DistrictRoadGates)
                if (!DistrictRoadOpen(gate.Tier) && gate.Crosses(from,to,out stop)) { tier = gate.Tier; return true; }
            stop = to; return false;
        }
        private void NotifyDistrictRoadStop(int tier)
        {
            if (_lastDistrictGate == tier && Clock.Now.TotalMinutes-_lastDistrictGateMinute < 10) return;
            _lastDistrictGate = tier; _lastDistrictGateMinute = Clock.Now.TotalMinutes;
            Show("前方暂不通行", "通往" + DistrictName(tier) + "的道路尚未开放。\n完成“" + DistrictName(tier-1) + " → " + DistrictName(tier) + "”主线引荐后放行。\n请到手机聊天查看主线消息；满足资金条件不会直接开路。");
        }
        private int BlockedDistrictOnTrip(Trip trip)
        {
            for(int i=1;i<trip.Points.Count;i++)
                if(TryDistrictRoadStop(trip.Points[i-1],trip.Points[i],out _,out int tier))return tier;
            return 0;
        }

        private static readonly string[] ExpoStoryChain={"expo.invitation","expo.prepare","expo.reception","expo.delivery","expo.review"};
        private static readonly string[] TenderStoryChain={"tender.invitation","tender.prepare","tender.trial","tender.decision","tender.fulfilment","tender.review"};
        private static readonly string[][] DistrictNodeChains = {
            new[] { "community.intro", "community.meeting", "community.clue", "community.search", "community.trial" },
            ExpoStoryChain,
            TenderStoryChain
        };
        public DistrictNarrativeNode CurrentDistrictNode => DistrictNarrativeCatalog.Nodes.FirstOrDefault(n => n.Id == _district.NodeId);
        public string DistrictInvitationStatus
        {
            get
            {
                if (_district.PendingChapter <= 0 || CurrentDistrictNode != null) return null;
                if (_district.InvitationWaitingForWake) return "主线邀请已到期，将在醒来2小时后联系。";
                long due = _district.InvitationDueAt;
                long remaining = Math.Max(0, due - Clock.Now.TotalMinutes);
                return "预计第" + (due / 1440 + 1) + "天 " + Hm((int)(due % 1440))
                    + "主动联系，还需" + (remaining / 60) + "小时" + (remaining % 60) + "分钟。";
            }
        }
        private string[] ActiveDistrictChain(int chapter) => DistrictNodeChains[chapter-1];
        public bool DistrictNodeCompleted(string id) => _district.Completed.Contains(id);
        public int DistrictOperatingShops(int tier) => MyShops.Count(s => DistrictTierOf(s.Place)==tier
            && (IsDedicatedVenue(s.Place) ? VenueOperation(s.Place.Id)?.Accepting==true : !s.Sim.NotFitted)
            && s.RenovationDay < Clock.Today.TotalDays && !Flags.Contains(OpeningChoicePrefix+s.Place.Id));
        public string DistrictPrerequisiteBlocker(int chapter)
        {
            if (chapter < 1 || chapter > 3) return "未知阶段";
            if (!City.IsLakeCity) return "主线验收需要当前湖城地图";
            if (DistrictAccessTier < chapter-1) return "请先完成上一段主线，开放"+DistrictName(chapter-1)+"道路";
            if (chapter==1)
            {
                if (DistrictOperatingShops(0)<5) return "城郊已布置并开业的门店 "+DistrictOperatingShops(0)+"/5";
                if (Ledger.Balance<Money.FromYuan(1000000)) return "现金尚未达到100万元";
            }
            if (chapter==2)
            {
                if (DistrictOperatingShops(1)<6) return "社区已布置并开业的门店 "+DistrictOperatingShops(1)+"/6";
                if (NetWorth()<Money.FromYuan(30000000)) return "净资产尚未达到3000万元";
                if (!_brands.Brands.Any()) return "请先创立自己的行业品牌";
            }
            if(chapter==3 && !_brands.Brands.Any(DistrictBrandRevenueQualified))
                return "需要一个品牌所属行业上个完整月营业额达到1亿元";
            return null;
        }
        public void RefreshDistrictNarrative()
        {
            if (!City.IsLakeCity || IsGameOver) return;
            if(CommunitySearchActive && _district.CommunityPieces==7)CompleteDistrictNode();
            if(_district.NodeId=="community.trial" && _district.MeetingNodeId!=_district.NodeId
                && CanDeliverCommunityPages && CityPersonState(CityLifeCatalog.TangWei) is CityPersonState receiver
                && string.IsNullOrEmpty(receiver.ActivityId) && string.IsNullOrEmpty(receiver.VisitId))
                BeginDistrictMeeting("community.trial");
            RefreshDistrictCommerce();
            if(CurrentDistrictNode!=null)return;
            int chapter = _district.Chapter+1;
            if (chapter>3) return;
            if (_district.PendingChapter!=chapter)
            {
                if (DistrictPrerequisiteBlocker(chapter)!=null) return;
                _district.CommerceBrandId=_brands.Brands.Where(b=>chapter!=3||DistrictBrandRevenueQualified(b)).OrderByDescending(b=>b.Influence).FirstOrDefault()?.Id;
                _district.PendingChapter=chapter;
                _district.InvitationDueAt=Clock.Now.TotalMinutes+3*1440;
            }
            if (Clock.Now.TotalMinutes<_district.InvitationDueAt) return;
            if (_sleeping) { _district.InvitationWaitingForWake=true; return; }
            if (_district.InvitationWaitingForWake)
            {
                _district.InvitationWaitingForWake=false;
                _district.InvitationDueAt=Clock.Now.TotalMinutes+120;
                return;
            }
            _district.PendingChapter=0;
            _district.InvitationDueAt=0;
            _district.Chapter = chapter;
            if(chapter>1)StartDistrictCommerce();
            _district.NodeId = ActiveDistrictChain(chapter)[0];
            string sender=chapter==1?"李秀兰":chapter==2?"任帆":"沈远";
            Say(sender+"主动联系了你：“"+CurrentDistrictNode.Title+"”。");
            Show(sender+"发来主线邀请",CurrentDistrictNode.Dialogue+"\n\n打开手机聊天顶部的“主线消息”回复邀请；现在不用立刻赴约。");
        }
        public string DistrictMeetingContact
        {
            get
            {
                bool booked=_district.MeetingNodeId==_district.NodeId;
                switch(_district.NodeId)
                {
                    case "community.meeting": return booked?CityLifeCatalog.TangWei:"keeper_agent";
                    case "community.trial": return CityLifeCatalog.TangWei;
                    case "expo.invitation": case "expo.review": return "life_curator";
                    case "tender.invitation": case "tender.review": return "life_enterprise";
                    default: return CurrentDistrictNode?.People.FirstOrDefault();
                }
            }
        }
        public string DistrictPersonObjective(string personId)
        {
            var node=CurrentDistrictNode;if(node==null)return null;

            if(node.Id=="community.search")
            {
                for(int i=0;i<3;i++)if(CommunityLeadPerson(i)==personId && !CommunityHasClue(i))
                    return "主线 · 打听图页";
                return null;
            }
            if(!DistrictCommerceGameplay && personId!=DistrictMeetingContact)return null;
            if(DistrictCommerceGameplay)
            {
                var tender=DistrictCommerceTender;
                if(node.Id=="tender.prepare"&&tender!=null&&
                    (personId=="life_enterprise"&&(tender.Intel&1)==0||personId=="life_tenant"&&(tender.Intel&2)==0))
                    return "主线 · 调查竞标信息";
                return null;
            }
            if(personId!=DistrictMeetingContact&&!node.People.Contains(personId)&&!_district.MeetingPeople.Contains(personId))return null;
            if(_district.MeetingNodeId!=node.Id)return "主线 · 待约见";
            if(CityPersonState(personId)?.Route!=null)return "主线 · 正在赴约";
            if(DistrictMeetingBlocker!=null)return AtCurrentDistrictMeeting?"主线 · 等待会面条件":"主线 · 前往会面点";
            return personId==DistrictMeetingContact?"主线 · 可以确认":"主线 · 找"+(CityPerson(DistrictMeetingContact)?.Name??"联系人")+"确认";
        }

        public string DistrictNodePlaceId => CurrentDistrictNode?.PlaceId;
        public bool DistrictMeetingOutdoors => City.IsLakeCity && !CommunitySearchActive
            && (_district.Chapter == 1 || StreetServicePlace(DistrictNodePlaceId));
        public string DistrictMeetingStreetNode => DistrictMeetingOutdoors && City.HasPlace(DistrictNodePlaceId)
            ? CommunityStreetNode(City.GetPlace(DistrictNodePlaceId)) : null;
        public bool AtCurrentDistrictMeeting => CurrentDistrictNode != null
            && _district.MeetingNodeId == _district.NodeId && AtDistrictMeeting();
        public string DistrictCurrentGoal
        {
            get
            {
                var node=CurrentDistrictNode;if(node==null)return "暂无进行中的区域主线。";
                string purpose=_district.Chapter==1?"这一段要做什么：找齐三页旧街商铺图，当面交给唐蔚，获得社区引荐。"
                    :_district.Chapter==2?"这一段要做什么：用自己的品牌参加季度展会，完成展会采购订单，并让同一品牌知名度达到100，获得公园引荐。"
                    :"这一段要做什么：通过企业竞标赢得供应商席位，完成同一份合同连续三期交付，获得花园引荐。";
                string step="当前步骤："+node.Objective;
                return purpose+"\n\n"+step;
            }
        }
        public string DistrictNextAction
        {
            get
            {
                if(CurrentDistrictNode==null)return DistrictInvitationStatus;
                string contact=CityPerson(DistrictMeetingContact)?.Name??"主线联系人";
                if(_district.MeetingNodeId!=_district.NodeId)return "当前："+CurrentDistrictNode.Objective+"\n联系："+contact;
                if(!AtCurrentDistrictMeeting)return "前往"+City.GetPlace(_district.MeetingPlaceId).Name+(DistrictMeetingOutdoors?"旁街边会面点":"门前")+"，找"+contact+"。";
                return DistrictMeetingBlocker??("与"+contact+"交谈，确认“"+CurrentDistrictNode.Title+"”。");
            }
        }
        public Place DistrictPersonWaitingPlace(string id)
        {
            var person=CityPersonState(id);if(person==null)return null;
            if(person.Route==null&&person.LocationPlaceId==person.HomePlaceId&&City.HasPlace(person.HomePlaceId)
                &&HomeVisitBlocker(City.GetPlace(person.HomePlaceId))!=null&&City.HasPlace(person.WorkPlaceId))
            {
                var work=City.GetPlace(person.WorkPlaceId);
                if(work.Kind!=PlaceKind.Home&&DistrictAccessBlocker(work)==null)return work;
            }
            if(person.Route?.TargetPlaceId==person.HomePlaceId&&City.HasPlace(person.HomePlaceId)
                &&DistrictAccessBlocker(City.GetPlace(person.HomePlaceId))==null)return City.GetPlace(person.HomePlaceId);
            string location=person.LocationPlaceId??person.Route?.TargetPlaceId;
            if(!City.HasPlace(location)||DistrictAccessBlocker(City.GetPlace(location))==null)return null;
            return City.HasPlace(person.HomePlaceId)&&DistrictAccessBlocker(City.GetPlace(person.HomePlaceId))==null
                ?City.GetPlace(person.HomePlaceId):null;
        }

        public string DistrictMeetingDescription
        {
            get
            {
                if(CurrentDistrictNode==null)return "暂无待处理主线消息";
                if(_district.NodeId=="community.search")return "三位保管人分别交往、打听；取得对应线索后再去藏点找图页。";
                string place=City.HasPlace(DistrictNodePlaceId)?City.GetPlace(DistrictNodePlaceId).Name:DistrictNodePlaceId;
                string site=place+(DistrictMeetingOutdoors?"附近的街边空地":"门前");
                if(_district.MeetingNodeId!=_district.NodeId)return "约见地点："+site+"。确认后人物沿实际道路赴约，玩家自行前往。";
                bool timeReady=Clock.Now.TotalMinutes>=_district.MeetingAt;
                var waiting=_district.MeetingPeople.Where(id=>CityPersonState(id)?.Route!=null
                    ||CityPersonState(id)?.LocationPlaceId!=_district.MeetingPlaceId)
                    .Select(id=>CityPerson(id)?.Name??id).ToArray();
                return "地点："+site+"\n玩家位置："+(AtCurrentDistrictMeeting?"已到达":"尚未到达，请点击前往会面点")
                    +"\n约定时间："+DistrictMeetingTime
                    +"\n现在时间："+Clock.Today+" "+Hm(Clock.MinuteOfDay)
                    +"\n时间状态："+(timeReady?"已到约定时间，可晚到，不会失约":"尚未到时间，还需"+DistrictMeetingWait)
                    +"\n参与人物："+(waiting.Length==0?"已全部到场":string.Join("、",waiting)+"仍在赴约")
                    +"\n下一步："+DistrictNextAction;
            }
        }
        public string DistrictMeetingTime => new PCShopSell.Core.Time.GameDate((int)(_district.MeetingAt/1440))
            +" "+Hm((int)(_district.MeetingAt%1440));
        private string DistrictMeetingWait
        {
            get
            {
                long minutes=Math.Max(0,_district.MeetingAt-Clock.Now.TotalMinutes);
                return (minutes>=60?minutes/60+"小时":"")+minutes%60+"分钟";
            }
        }
        public string DistrictNodeTerms
        {
            get
            {
                switch(_district.NodeId)
                {
                    case "expo.invitation": return "每季度一届、连续4天；在品牌页选择摊位，每种展品100件，邀请本品牌签约代言人。费用以报名页报价为准，展前7天截止。";
                    case "expo.review": return "认可接任务前的历史成绩：同一品牌曾达成知名度100并完成真实展会采购订单后，只需当面确认，无需再参展。";
                    case "tender.invitation": return "品牌页调查采购需求、提交方案、交付试单，评审通过才取得席位。";
                    case "tender.review": return "企业合同连续三期交付后再约见；失约失去席位，下一年度可以重新竞标。";
                    case "community.meeting": return "唐蔚筹备街区介绍会，需要找回搬家时寄存的旧街手绘商铺图。先接受寻物委托，再向李秀兰问清箱子去向。没有营业额或交钱要求。";
                    case "community.clue": return "到城郊旧居街居民住宅附近的街边空地约见李秀兰，问清三页图的保管人。之后分别交往、打听具体藏点。";
                    case "community.search": return "三位保管人分别需要好感达到30、接受委托后一起完成喜欢的活动。再向本人打听才会出现对应箱子；三个箱子可以任意顺序寻找。找齐三页后回城郊当面交给唐蔚。";
                    case "community.trial": return "把收集到的图页交给唐蔚。她会到城郊中介旁的街边会面点，无需进入中介。交付后开放社区道路。";
                    default: return "完成实际会面后推进；打开消息或目录不会完成任务。";
                }
            }
        }
        private bool AtDistrictMeeting()
        {
            if(string.IsNullOrEmpty(_district.MeetingPlaceId)||!City.HasPlace(_district.MeetingPlaceId)||Trip!=null)return false;
            var place=City.GetPlace(_district.MeetingPlaceId);
            if (DistrictMeetingStreetNode is string node)
            {
                if(CurrentPlace!=null)return false;
                var anchor=City.ViewPosition(node);
                if(DistanceCity(PlayerPosition,anchor)<=4)return true;
                // Participants stand in a small circle around the anchor; speaking beside one counts as arrival.
                return _district.MeetingPeople.Any(id=>
                {
                    var person=CityPersonState(id);
                    return person!=null&&person.Route==null&&person.LocationPlaceId==_district.MeetingPlaceId
                        &&DistanceCity(new MapPoint(person.X,person.Y),anchor)<=4&&CitySamePlace(id);
                });
            }
            return CurrentPlace==place || CurrentPlace==null && DistanceCity(PlayerPosition,StandOf(place))<=5;
        }
        public string DistrictMeetingBlocker
        {
            get
            {
                if(IsGameOver||IsWorking||CityActivityExclusive)return "请先结束当前工作或活动";
                if(CurrentDistrictNode==null)return "暂无主线";
                if(DistrictCommerceGameplay)return DistrictCommerceBlocker;
                if(_district.MeetingNodeId!=_district.NodeId)return "请先在聊天消息中确认约见";
                if(!AtDistrictMeeting())return "请自行前往"+City.GetPlace(_district.MeetingPlaceId).Name+(DistrictMeetingOutdoors?"附近的街边会面点":"门前");
                if(Clock.Now.TotalMinutes<_district.MeetingAt)return "约定 "+DistrictMeetingTime+"，还需等待"+DistrictMeetingWait+"。关闭窗口后时间才会继续。";
                if(_district.MeetingPeople.Any(id=>CityPersonState(id)?.Route!=null || CityPersonState(id)?.LocationPlaceId!=_district.MeetingPlaceId))return "同行人物仍在赴约，请关闭窗口等待";
                if (DistrictMeetingStreetNode is string streetNode)
                {
                    var meetingPoint = City.ViewPosition(streetNode);
                    if (_district.MeetingPeople.Any(id =>
                    {
                        var person = CityPersonState(id);
                        return person == null || DistanceCity(new MapPoint(person.X, person.Y), meetingPoint) > 4;
                    })) return "人物正在前往街边会面点，请关闭窗口等待";
                }
                return null;
            }
        }
        public bool BeginDistrictMeeting(string nodeId, string choice = null)
        {
            var node=CurrentDistrictNode;
            if(node==null||node.Id!=nodeId)return false;
            if(DistrictCommerceGameplay)return Fail("这一阶段直接完成品牌经营任务，无需另约会面");
            if(nodeId=="community.search")return Fail("请分别向三位保管人打听，取得线索后前往各自藏点按 F 调查。");
            if(nodeId=="community.trial" && !CanDeliverCommunityPages)return Fail("请先找回旧街手绘商铺图。");
            if(node.Choices.Length>0 && !node.Choices.Any(c=>c.Id==choice))return Fail("请选择一种处理方式。");
            if(choice=="later")return true;
            if(_district.MeetingNodeId==nodeId)return true;
            if(nodeId=="community.intro")
            {
                RememberDistrictChoice(nodeId,choice); CompleteDistrictNode();
                return true;
            }
            string placeId=DistrictNodePlaceId;
            if(!City.HasPlace(placeId)||DistrictAccessBlocker(City.GetPlace(placeId))!=null)return Fail("会面地点尚未开放。");
            var people=node.People.Where(id=>CityPersonState(id)!=null).ToArray();
            if(people.Length==0)return Fail("未找到会面人物。");
            // Reserve everyone together only after all routes and concurrent commitments validate.
            var routes=new Dictionary<string,CityRoute>();
            foreach(string id in people)
            {
                var person=CityPersonState(id);
                if(!string.IsNullOrEmpty(person.ActivityId)||!string.IsNullOrEmpty(person.VisitId)||Employees.Any(e=>PersonIdOf(e.Person)==id))
                    return Fail((CityPerson(id)?.Name??id)+"正在活动或任职，请结束安排后再约。");
                var route=PlanCityRouteToNode(person,placeId,Clock.Now.TotalMinutes,DistrictMeetingStreetNode);
                if(route==null)return Fail("人物暂时无法到达约见地点。");
                routes[id]=route;
            }
            _district.MeetingNodeId=nodeId; _district.MeetingPlaceId=placeId; _district.MeetingPeople=people;
            _district.MeetingAt=Math.Max(Clock.Now.TotalMinutes+10,routes.Values.Max(r=>r.ArriveMinute)+5);
            RememberDistrictChoice(nodeId,choice);
            foreach(var entry in routes)
            {
                var person=CityPersonState(entry.Key);
                person.Route=entry.Value;person.LocationPlaceId=null;person.Task=CityPersonTask.Travelling;person.TaskLabel="赴主线会面";
                Meet(entry.Key,0);
            }
            Say(DistrictMeetingDescription); return true;
        }
        private void RememberDistrictChoice(string id,string choice)
        {
            _district.Choices=_district.Choices.Where(x=>!x.StartsWith(id+":",StringComparison.Ordinal)).Concat(new[]{id+":"+(choice??"confirm")}).ToArray();
        }
        private bool TickDistrictMeetingPerson(CityPersonState person,long now)
        {
            if(_district.MeetingNodeId!=_district.NodeId || !_district.MeetingPeople.Contains(person.PersonId))return false;
            if(person.LocationPlaceId!=_district.MeetingPlaceId)
            {
                person.Route=PlanCityRouteToNode(person,_district.MeetingPlaceId,now,DistrictMeetingStreetNode);person.LocationPlaceId=null;
                person.Task=CityPersonTask.Travelling;
            }
            else
            {
                // Existing appointments keep their time and participants after an older save is loaded.
                if (DistrictMeetingStreetNode is string node)
                {
                    var point = City.ViewPosition(node);
                    person.X = point.X;
                    person.Y = point.Y;
                }
                person.Task = CityPersonTask.Waiting;
            }
            person.TaskLabel="主线会面："+CurrentDistrictNode?.Title;return true;
        }
        private void TickDistrictNarrative(long from,long now) => RefreshDistrictNarrative();
        public bool ActDistrictNode(string id)
        {
            if(CurrentDistrictNode==null||CurrentDistrictNode.Id!=id)return false;
            string why=DistrictMeetingBlocker;if(why!=null)return Fail(why);
            if(DistrictCommerceActive&&DistrictCommerceBlocker!=null)return Fail(DistrictCommerceBlocker);
            if(id=="community.meeting")AcceptCommunitySearch();
            if(id=="community.search")return Fail("请在藏箱旁按 F 调查，再当面交付图页。");
            if(id=="community.trial")
            {
                if(!CanDeliverCommunityPages)return Fail("还没有找到全部三页图，无法交付。");
                _district.CommunityPieces=0;_district.CommunityMapDelivered=true;
            }
            CompleteDistrictNode();RefreshDistrictCommerce();return true;
        }
        private void CompleteDistrictNode()
        {
            var node=CurrentDistrictNode;if(node==null||DistrictNodeCompleted(node.Id))return;
            _district.Completed=_district.Completed.Concat(new[]{node.Id}).ToArray();
            foreach(var id in _district.MeetingPeople)RememberSocial(id,node.Title+"："+node.Result);
            if(node.Id=="community.trial")GrantDistrictAccess(1);
            if(node.Id=="expo.review")GrantDistrictAccess(2);
            if(node.Id=="tender.review")GrantDistrictAccess(3);
            _district.MeetingPeople=Array.Empty<string>();_district.MeetingPlaceId=null;_district.MeetingNodeId=null;
            var chain=ActiveDistrictChain(_district.Chapter);int index=Array.IndexOf(chain,node.Id)+1;
            _district.NodeId=index<chain.Length?chain[index]:null;
            Say("主线完成："+node.Title+(CurrentDistrictNode==null?"":"。下一条消息："+CurrentDistrictNode.Title));
        }
        private void GrantDistrictAccess(int tier)
        {
            _district.UnlockedTier=Math.Max(tier,_district.UnlockedTier);
            Show("道路已开放", "你完成了引荐与合作，通往"+DistrictName(tier)+"的道路已经放行。房租、采购和经营费用仍按正常规则支付。");
        }

        public string DebugDistrictAcceptanceBlocker(int chapter)
        {
            if(chapter<1||chapter>3||!City.IsLakeCity)return "需要湖城地图";
            if(IsGameOver||IsWorking||_sleeping||Racing||CityActivityExclusive||PendingEvent!=null)
                return "请先结束当前活动或处理事件";
            if(DistrictAccessTier>=chapter || _district.Chapter>chapter)return "该段主线已完成";
            return null;
        }
        public bool DebugEnterDistrictAcceptance(int chapter)
        {
#if UNITY_EDITOR
            string why=DebugDistrictAcceptanceBlocker(chapter);if(why!=null)return Fail(why);
            int tier=chapter-1, count=chapter==1?5:chapter==2?6:1;
            int available=RentableUnits().Count(p=>DistrictTierOf(p)==tier
                && ShopAt(p)==null && PropertyAt(p.Id)==null && VentureAt(p.Id)==null);
            if(DistrictOperatingShops(tier)+available<count)return Fail("没有足够的可用铺位生成验收门店，未修改进度。");
            if(_district.Chapter<chapter || CurrentDistrictNode==null && DistrictAccessTier<chapter)
            {
                // Acceptance shortcuts complete earlier chapters only; the chosen chapter remains playable.
                var completed=new HashSet<string>(_district.Completed);
                for(int previous=1;previous<chapter;previous++)
                {
                    foreach(string id in DistrictNodeChains[previous-1])completed.Add(id);
                }
                _district.Completed=completed.ToArray();
                _district.UnlockedTier=Math.Max(_district.UnlockedTier,tier);
                _district.Chapter=tier;
                _district.NodeId=null;
                _district.MeetingPeople=Array.Empty<string>();
                _district.MeetingNodeId=null;_district.MeetingPlaceId=null;_district.MeetingAt=0;
                _district.PendingChapter=0;_district.InvitationDueAt=0;_district.InvitationWaitingForWake=false;
                _district.CommerceBrandId=null;
                if(chapter>1)
                {
                    _district.CommunityMapQuest=true;_district.CommunityPieces=7;
                    _district.CommunityMapDelivered=true;
                }
            }
            if(!DebugPrepareDistrictPrerequisites(chapter))return false;
            if(CurrentDistrictNode==null)
            {
                // No three-day wait in acceptance; keep the normal invitation and response flow.
                if(Brand(_district.CommerceBrandId)==null)StartDistrictCommerce();
                _district.PendingChapter=chapter;
                _district.InvitationDueAt=Clock.Now.TotalMinutes;
                _district.InvitationWaitingForWake=false;
                RefreshDistrictNarrative();
            }
            Say("已进入“"+DistrictName(tier)+" → "+DistrictName(chapter)+"”验收。此前主线与道路已开放，请到手机聊天回复当前主线邀请；重复进入保留本段进度。");
            return true;
#else
            return false;
#endif
        }
        public string DebugDistrictPrerequisitesBlocker(int chapter)
        {
            if(chapter<1||chapter>3||!City.IsLakeCity)return "需要湖城地图";
            if(DistrictAccessTier<chapter-1)return "先完成上一段主线";
            if(DistrictAccessTier>=chapter || _district.Chapter>chapter)return "该段主线已完成";
            return null;
        }
        public bool DebugPrepareDistrictPrerequisites(int chapter)
        {
#if UNITY_EDITOR
            string why=DebugDistrictPrerequisitesBlocker(chapter);if(why!=null)return Fail(why);
            // Actual shops/cash, not a counterfeit shop-count displayed only in the quest window.
            int tier=chapter-1, count=chapter==1?5:chapter==2?6:1;
            var candidates=RentableUnits().Where(p=>DistrictTierOf(p)==tier
                && ShopAt(p)==null && PropertyAt(p.Id)==null && VentureAt(p.Id)==null)
                .OrderByDescending(p=>p==MainShop.Place).ToArray();
            if(DistrictOperatingShops(tier)+candidates.Length<count)return Fail("没有足够的可用铺位生成验收门店，未修改条件。");
            var previous=_focus;
            try
            {
                foreach(var place in candidates)
                {
                    if(DistrictOperatingShops(tier)>=count)break;
                    if(!MainShop.Rented && place==MainShop.Place)MoveInto(place,ShopRentQuote(place),Money.Zero);
                    else OpenNewShop(place,ShopRentQuote(place),Money.Zero);
                    FitStarterShop();
                    PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,8);
                    PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu0"),true,8);
                    CurrentShop.RenovationDay=-1;
                    Flags.Remove(OpeningChoicePrefix+place.Id);
                    UpdateShopFit();
                }
            }
            finally{SetFocus(previous);}
            if(chapter==1 && Ledger.Balance<Money.FromYuan(1000000))DebugAddCash((100000000-Ledger.Balance.Cents+99)/100);
            if(chapter==2)
            {
                long gap=3000000000L-NetWorth().Cents;if(gap>0)DebugAddCash((gap+99)/100);
            }
            if(chapter==3)
            {
                if(Ledger.Balance<Money.FromYuan(100000))DebugAddCash((10000000-Ledger.Balance.Cents+99)/100);
            }
            if(chapter>1)
            {
                var shops=MyShops.Where(shop=>shop.RetailKind=="computer"&&!shop.Sim.NotFitted).ToArray();
                var brand=_brands.Brands.FirstOrDefault(b=>b.Kind=="computer");
                if(brand==null)
                {
                    brand=new BrandRecord{Id="acceptance-mainline",Name="主线验收科技",Kind="computer",Position="value",Founded=BrandDay};
                    _brands.Brands=_brands.Brands.Concat(new[]{brand}).ToArray();
                }
                brand.ShopIds=brand.ShopIds.Union(shops.Select(shop=>shop.Place.Id)).ToArray();
                foreach(var shop in shops)foreach(string id in new[]{"cpu0","gpu0","ram0"})
                {
                    var part=PartCatalog.Get(id);shop.Sim.Stock.ForceAdd(part,true,Math.Max(0,150-shop.Sim.Stock.Count(part,true)));
                }
                if(chapter==3)brand.Influence=Math.Max(100,brand.Influence);
                if(CurrentDistrictNode==null)
                {
                    _district.CommerceBrandId=brand.Id;
                    // Explicit editor fixture skips only the entry gate; no invented sales or fulfilled orders.
                    if(_district.PendingChapter!=chapter){_district.PendingChapter=chapter;_district.InvitationDueAt=Clock.Now.TotalMinutes+3*1440;}
                }
                RefreshBrandTraffic();
            }
            SetSmartphone(true);RefreshDistrictNarrative();
            Say("已准备第"+chapter+"段前置条件。首次达标3个游戏日后，对方会主动发来邀请；重复点击不会重置等待时间。道路仍需完成剧情后开放。");return true;
#else
            return false;
#endif
        }
        private void LoadDistrictProgress(SaveData data)
        {
            _district=data.districtProgress?.Copy()??new DistrictProgress();
            _district.UnlockedTier=Math.Max(0,Math.Min(3,_district.UnlockedTier));
            _district.Chapter=Math.Max(0,Math.Min(3,_district.Chapter));
            if(CurrentDistrictNode==null)
            {
                if(_district.NodeId!=null){_district.Chapter=_district.UnlockedTier;_district.PendingChapter=0;_district.InvitationDueAt=0;}
                _district.NodeId=null;_district.MeetingPeople=Array.Empty<string>();_district.MeetingNodeId=null;
            }
            _district.Completed=_district.Completed.Where(id=>DistrictNarrativeCatalog.Nodes.Any(n=>n.Id==id)).ToArray();
            // Legacy saves keep assets and location. Returning through a checkpoint is always allowed.
        }
    }
}
