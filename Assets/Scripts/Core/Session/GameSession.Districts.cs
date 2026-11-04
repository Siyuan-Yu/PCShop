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

        private static readonly string[][] DistrictNodeChains = {
            new[] { "community.intro", "community.meeting", "community.clue", "community.search", "community.trial" },
            new[] { "park.proposal", "park.change", "park.delivery", "model.dinner", "model.clues", "model.repair", "model.delivery", "park.recommendation" },
            new[] { "garden.invitation", "garden.proposal", "garden.terms", "garden.fulfilment", "garden.echo" }
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
        private string[] ActiveDistrictChain(int chapter) => _district.BrandCommerceRoute&&chapter==2?(_district.CommerceVersion>0?ExpoStoryChain:new[]{"expo.invitation","expo.review"})
            :_district.BrandCommerceRoute&&chapter==3?(_district.CommerceVersion>0?TenderStoryChain:new[]{"tender.invitation","tender.review"}):DistrictNodeChains[chapter-1];
        public bool DistrictNodeCompleted(string id) => _district.Completed.Contains(id);
        public int DistrictOperatingShops(int tier) => MyShops.Count(s => DistrictTierOf(s.Place)==tier
            && (IsDedicatedVenue(s.Place) ? VenueOperation(s.Place.Id)?.Accepting==true : !s.Sim.NotFitted)
            && s.RenovationDay < Clock.Today.TotalDays && !Flags.Contains(OpeningChoicePrefix+s.Place.Id));
        private bool DistrictTestListing
        {
            get {
#if UNITY_EDITOR
                return _district.DebugListedIssuer;
#else
                return false;
#endif
            }
        }
        private bool DistrictTestQualifiedMonth
        {
            get {
#if UNITY_EDITOR
                return _district.DebugQualifiedMonth && _district.DebugListedIssuer
                    && !string.IsNullOrEmpty(_district.DebugIssuerId) && !string.IsNullOrEmpty(_district.DebugSectorId)
                    && _district.DebugSectorRevenueCents>=10000000000L && _district.DebugSharePriceCents>=10000;
#else
                return false;
#endif
            }
        }
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
                if (!_brands.Brands.Any()&&!DistrictTestListing) return "请先创立自己的行业品牌";
            }
            if(chapter==3&&_brands.Brands.Any())
            {
                int month=Clock.Today.TotalDays/30;
                if(!_brands.Brands.Any(b=>BrandRevenue(MyShops.Where(s=>s.RetailKind==b.Kind),(month-1)*30,month*30)>=10000000000L))return "需要一个品牌所属行业上个完整月营业额达到1亿元";
                return null;
            }
            if (chapter==3 && (!DistrictTestQualifiedMonth || Clock.Today.TotalDays/30<=_district.QualifiedMonth))
                return "需同一行业完整月收入1亿元、对应自有股价100元，并进入次月（编辑器可准备验收凭据）";
            return null;
        }
        public void RefreshDistrictNarrative()
        {
            if (!City.IsLakeCity || IsGameOver) return;
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
            if(chapter>1&&_brands.Brands.Any()){_district.BrandCommerceRoute=true;StartDistrictCommerce();}
            _district.NodeId = ActiveDistrictChain(chapter)[0];
            string sender=chapter==1?"李秀兰":_district.BrandCommerceRoute?(chapter==2?"任帆":"沈远"):chapter==2?"唐蔚":"任帆";
            Say(sender+"主动联系了你：“"+CurrentDistrictNode.Title+"”。");
            Show(sender+"发来主线邀请",CurrentDistrictNode.Dialogue+"\n\n打开手机聊天顶部的“主线消息”回复邀请；现在不用立刻赴约。");
        }
        public event Action<string,string> DistrictDialogueReady;
        public string DistrictMeetingContact
        {
            get
            {
                bool booked=_district.MeetingNodeId==_district.NodeId;
                switch(_district.NodeId)
                {
                    case "community.meeting": return booked?CityLifeCatalog.TangWei:"keeper_agent";
                    case "community.trial": return CityLifeCatalog.TangWei;
                    case "park.proposal": return CityLifeCatalog.TangWei;
                    case "park.change": case "park.delivery": case "park.recommendation":
                    case "expo.invitation": case "expo.review": return "life_curator";
                    case "model.dinner": return booked?"life_enterprise":"life_curator";
                    case "model.clues": return DistrictChoice("model.clues")=="supplier"?"life_model_supplier":"life_tenant";
                    case "model.repair": return "life_maker";
                    case "model.delivery": case "tender.invitation": case "tender.review": return "life_enterprise";
                    case "garden.invitation": return booked?CityLifeCatalog.LiangJing:"life_curator";
                    case "garden.proposal": case "garden.terms": case "garden.fulfilment": return CityLifeCatalog.LiangJing;
                    case "garden.echo": return CityLifeCatalog.TangWei;
                    default: return CurrentDistrictNode?.People.FirstOrDefault();
                }
            }
        }
        public IEnumerable<DistrictTask> ReadyDistrictTasks => _district.MeetingNodeId==_district.NodeId
            ? DistrictTasks.Where(t=>!HasDistrictTask(t.Id)&&t.Requires.All(HasDistrictTask)) : Enumerable.Empty<DistrictTask>();

        public string DistrictPersonObjective(string personId)
        {
            var node=CurrentDistrictNode;if(node==null)return null;
            if((node.Id=="park.delivery"||node.Id=="model.dinner")&&_district.WorkStartedAt>0)return null;

            if(node.Id=="community.search")
            {
                for(int i=0;i<3;i++)if(CommunityLeadPerson(i)==personId && !CommunityHasClue(i))
                    return "主线 · 打听图页";
                return null;
            }
            if(!DistrictCommerceGameplay)
            {
                var tasks=ReadyDistrictTasks.ToArray();
                if(tasks.Any(t=>t.PersonId==personId))return "主线 · "+tasks.First(t=>t.PersonId==personId).Title;
                if(DistrictTasksBlocker!=null&&_district.MeetingNodeId==node.Id)return null;
                if(personId!=DistrictMeetingContact && !(node.Id=="model.clues"&&_district.MeetingNodeId!=node.Id&&personId=="life_model_supplier"))return null;
            }
            if(DistrictCommerceGameplay)
            {
                var tender=DistrictCommerceTender;
                if(node.Id=="tender.prepare"&&tender!=null&&
                    (personId=="life_enterprise"&&(tender.Intel&1)==0||personId=="life_tenant"&&(tender.Intel&2)==0))
                    return "主线 · 调查竞标信息";
                return null;
            }
            if(personId!=DistrictMeetingContact&&!node.People.Contains(personId)&&!_district.MeetingPeople.Contains(personId)&&!DistrictTasks.Any(t=>t.PersonId==personId))return null;
            if(_district.MeetingNodeId!=node.Id)return "主线 · 待约见";
            if(CityPersonState(personId)?.Route!=null)return "主线 · 正在赴约";
            return Clock.Now.TotalMinutes<_district.MeetingAt?"主线 · 等待会面":"主线 · 可以交谈";
        }

        public string DistrictNodePlaceId => CurrentDistrictNode?.PlaceId;
        public bool DistrictMeetingOutdoors => City.IsLakeCity && _district.Chapter == 1 && !CommunitySearchActive;
        public string DistrictMeetingStreetNode => DistrictMeetingOutdoors && City.HasPlace(DistrictNodePlaceId)
            ? CommunityStreetNode(City.GetPlace(DistrictNodePlaceId)) : null;
        public bool AtCurrentDistrictMeeting => CurrentDistrictNode != null
            && _district.MeetingNodeId == _district.NodeId && AtDistrictMeeting();
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
                string schedule="约定时间："+DistrictMeetingTime+"\n会面地点："+site+"。\n";
                if(AtCurrentDistrictMeeting)return schedule+(DistrictMeetingBlocker==null
                    ? "你已到达，对方也已到场，可以当面交谈。"
                    : "你已到达。"+DistrictMeetingBlocker);
                long remaining=_district.MeetingAt-Clock.Now.TotalMinutes;
                return schedule+(remaining>0?"距离约定时间还有"+DistrictMeetingWait+"。":"约定时间已到。")+"可晚些到，机会不会消失。";
            }
        }
        private string DistrictMeetingTime => new PCShopSell.Core.Time.GameDate((int)(_district.MeetingAt/1440))
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
                    case "expo.invitation": return "每90天一届；品牌页报名，需¥2,000及3件展示样品。报名截止展前7天。";
                    case "expo.review": return "同一品牌影响力100，并完成至少一笔真实展会采购订单。";
                    case "tender.invitation": return "品牌页调查采购需求、提交方案、交付试单，评审通过才取得席位。";
                    case "tender.review": return "企业合同连续三期交付后再约见；失约失去席位，下一年度可以重新竞标。";
                    case "community.meeting": return "唐蔚筹备街区介绍会，需要找回搬家时寄存的旧街手绘商铺图。先接受寻物委托，再向李秀兰问清箱子去向。没有营业额或交钱要求。";
                    case "community.clue": return "到城郊旧居街居民住宅附近的街边空地约见李秀兰，问清三页图的保管人。之后分别交往、打听具体藏点。";
                    case "community.search": return "三位保管人分别需要好感达到30、接受委托后一起完成喜欢的活动。再向本人打听才会出现对应箱子；三个箱子可以任意顺序寻找。找齐后手动拼图，再回中介交付。";
                    case "community.trial": return "任务物品：旧街手绘商铺图 ×"+(_district.HasCommunityMap?"1":"0")+"。带到城郊中介旁的街边会面点交给唐蔚，无需进入中介；确认交付后消耗任务物品并开放社区道路。";
                    case "park.proposal": return "共同分摊预算¥2,000；缩小规模预算¥1,000。会面确认后实际支付。";
                    case "park.delivery": return "在社区服务站实际参与30游戏分钟；离开不累计，管理窗口暂停时不累计。";
                    case "model.dinner": return "饭局费用¥160；到场后确认开席，与任帆、沈远交谈30游戏分钟。";
                    case "model.repair": return _district.RepairPaid ? "已支付转让及修复¥1,800；修复剩余 "+Math.Max(0,_district.RepairReadyAt-Clock.Now.TotalMinutes)+" 游戏分钟。" : "当面确认转让¥1,500、修复¥300；工期120游戏分钟。沈远在交付时按约报销¥1,800。";
                    case "model.delivery": return "委托物品：修复后的初版城市展样 ×"+(_district.HasModel?"1":"0")+"。交付后移除，报销仅一次。";
                    case "garden.proposal": return "封路期间先在已有公园门店完成合作试点；不要求提前进入花园。请选一家已布置的公园自营店作为试点。";
                    case "garden.terms": return "分阶段试营支付¥5,000；有限支持支付¥50,000；另找合作者支付¥10,000。均需签约后满1天且试点门店有真实成交。";
                    case "garden.fulfilment": return "试点："+(_district.ProjectShopId??"尚未选择")+"。签约满1天、约定门店实际营业并产生销售后，回公园中介验收。";
                    default: return "完成实际会面后推进；打开消息或目录不会完成任务。";
                }
            }
        }
        private bool AtDistrictMeeting()
        {
            if(string.IsNullOrEmpty(_district.MeetingPlaceId)||!City.HasPlace(_district.MeetingPlaceId)||Trip!=null)return false;
            var place=City.GetPlace(_district.MeetingPlaceId);
            if (DistrictMeetingStreetNode is string node)
                return CurrentPlace == null && DistanceCity(PlayerPosition, City.ViewPosition(node)) <= 4;
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
        public bool SelectDistrictProjectShop(string placeId)
        {
            if(_district.NodeId!="garden.proposal" || _district.ProjectStartedAt>0)return false;
            var shop=ShopAt(placeId);
            if(shop==null||DistrictTierOf(shop.Place)!=2||shop.Sim.NotFitted)return Fail("请选择已布置开业的公园门店。");
            if(_district.ProjectShopId!=shop.Place.Id)_district.TaskEvidence=_district.TaskEvidence.Where(e=>e!="garden.site").ToArray();
            _district.ProjectShopId=shop.Place.Id; return true;
        }
        public bool BeginDistrictMeeting(string nodeId, string choice = null)
        {
            var node=CurrentDistrictNode;
            if(node==null||node.Id!=nodeId)return false;
            if(DistrictCommerceGameplay)return Fail("这一阶段直接完成品牌经营任务，无需另约会面");
            if(nodeId=="community.search")return Fail("请分别向三位保管人打听，取得线索后前往各自藏点按 F 调查。");
            if(nodeId=="community.trial" && !_district.HasCommunityMap)return Fail("请先找回旧街手绘商铺图。");
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
            if(nodeId=="garden.terms" && choice=="respect")people=people.Concat(new[]{"life_clothier"}).Distinct().ToArray();
            if(nodeId=="garden.fulfilment" && _district.ProjectPartnerId=="life_clothier")
                people=people.Where(id=>id!="life_tenant").Concat(new[]{"life_clothier"}).Distinct().ToArray();
            if(nodeId=="model.clues")people=choice=="supplier"?new[]{"life_model_supplier","life_curator"}:new[]{"life_tenant","life_creator"};
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
            _district.WorkMinutes=0; _district.WorkStartedAt=0;
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
        private string DistrictChoice(string id) => _district.Choices.FirstOrDefault(x=>x.StartsWith(id+":",StringComparison.Ordinal))?.Substring(id.Length+1);
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
        private void TickDistrictNarrative(long from,long now)
        {
            RefreshDistrictNarrative();
            if(_district.WorkStartedAt>0 && (_district.NodeId=="park.delivery"||_district.NodeId=="model.dinner") && DistrictMeetingBlocker==null)
            {
                _district.WorkMinutes+=Math.Max(0,now-Math.Max(from,_district.WorkStartedAt));
                if(_district.WorkMinutes>=30)
                {
                    var node=CurrentDistrictNode;string speaker=DistrictMeetingContact;
                    if(ActDistrictNode(node.Id))DistrictDialogueReady?.Invoke(speaker,node.Dialogue+"\n\n下一步："+(CurrentDistrictNode?.Title??"本段主线已完成。"));
                }
            }
        }
        public bool ActDistrictNode(string expectedNodeId)
        {
            if(CurrentDistrictNode?.Id!=expectedNodeId)return false;
            string why=DistrictMeetingBlocker;if(why!=null)return Fail(why);
            string id=_district.NodeId;
            if(DistrictCommerceActive&&DistrictCommerceBlocker!=null)return Fail(DistrictCommerceBlocker);
            if(id=="expo.review"&&!_brands.Brands.Any(b=>b.Influence>=100&&_brands.Orders.Any(o=>o.BrandId==b.Id&&o.Source=="expo"&&o.Done)))return Fail("需要同一品牌达到100影响力并交付展会订单");
            if(id=="tender.review"&&!_brands.Tenders.Any(t=>t.Stage==3))return Fail("需要赢得席位并完成连续三期履约");
            string taskBlocker=DistrictTasksBlocker;
            if(taskBlocker!=null)return Fail(taskBlocker);
            if(id=="community.meeting")AcceptCommunitySearch();
            if(id=="community.search")return Fail("请在纸箱旁按 F 调查，远程对话不能取物。");
            if(id=="community.trial")
            {
                if(!_district.HasCommunityMap)return Fail("还没有找到手绘商铺图，无法交付。");
                _district.HasCommunityMap=false;_district.CommunityMapDelivered=true;
            }
            if(id=="park.proposal")
            {
                long cost=DistrictChoice(id)=="scaled"?1000:2000;
                if(!Ledger.TryDebit(Clock.Today,"主线合作",Money.FromYuan(cost),"社区活动预算"))return Fail("活动预算不足。");
            }
            if(id=="park.delivery"||id=="model.dinner")
            {
                if(_district.WorkStartedAt==0)
                {
                    if(id=="model.dinner"&&!Ledger.TryDebit(Clock.Today,"主线饭局",Money.FromYuan(160),"与任帆、沈远的委托饭局"))return Fail("饭局费用不足。");
                    _district.WorkStartedAt=Clock.Now.TotalMinutes;
                    Say("已开始，请关闭管理窗口，在现场参与30游戏分钟。");return true;
                }
                if(_district.WorkMinutes<30)return Fail("现场参与 "+_district.WorkMinutes+"/30 分钟，请关闭窗口继续参与。");
            }
            if(id=="model.repair")
            {
                if(!_district.RepairPaid)
                {
                    if(!Ledger.TryDebit(Clock.Today,"主线委托",Money.FromYuan(1800),"初版展样转让及修复（沈远确认报销）"))return Fail("暂垫资金不足。");
                    _district.RepairPaid=true;_district.RepairReadyAt=Clock.Now.TotalMinutes+120;
                    Say("程弈已收款并开始修复，120游戏分钟后在这里领取。");return true;
                }
                if(Clock.Now.TotalMinutes<_district.RepairReadyAt)return Fail("修复尚未完成，请稍后领取。");
                _district.HasModel=true;
            }
            if(id=="model.delivery")
            {
                if(!_district.HasModel||!_district.RepairPaid||_district.ModelDelivered)return Fail("没有可交付的修复展样。");
                _district.HasModel=false;_district.ModelDelivered=true;
                Ledger.Credit(Clock.Today,"委托报销",Money.FromYuan(1800),"沈远报销展样转让与修复费用");
            }
            if(id=="garden.proposal" && ShopAt(_district.ProjectShopId)==null)return Fail("请先与合作方当面商定公园试点门店。");
            if(id=="garden.terms")
            {
                var shop=ShopAt(_district.ProjectShopId);
                if(shop==null||shop.Sim.NotFitted)return Fail("约定的试点门店尚未准备好。");
                string choice=DistrictChoice(id);long cost=choice=="limited"?50000:choice=="respect"?10000:5000;
                if(!Ledger.TryDebit(Clock.Today,"主线合作",Money.FromYuan(cost),"花园合作：公园门店试点 / "+choice))return Fail("签约资金不足。");
                _district.ProjectStartedAt=Clock.Now.TotalMinutes;
                _district.ProjectPartnerId=choice=="respect"?"life_clothier":"life_tenant";
                Flags.Add("district:project:sales-start:"+Ledger.Entries.Count);
            }
            if(id=="garden.fulfilment")
            {
                if(Clock.Now.TotalMinutes-_district.ProjectStartedAt<1440)return Fail("签约后尚未满1天。");
                var shop=ShopAt(_district.ProjectShopId);
                if(shop==null||shop.Sim.NotFitted)return Fail("约定试点门店已关闭或尚未布置。");
                int start=0;int.TryParse(Flags.FirstOrDefault(f=>f.StartsWith("district:project:sales-start:"))?.Substring("district:project:sales-start:".Length),out start);
                if(!Ledger.Entries.Skip(start).Any(e=>e.ShopId==shop.Place.Id&&e.Amount.IsPositive&&(e.Category==ShopSimulation.SalesCategory||e.Category==ShopSimulation.BuildCategory||e.Category=="餐饮")))
                    return Fail("试点门店尚无签约后的真实营业成交。");
            }
            CompleteDistrictNode();return true;
        }
        private void CompleteDistrictNode()
        {
            var node=CurrentDistrictNode;if(node==null||DistrictNodeCompleted(node.Id))return;
            _district.Completed=_district.Completed.Concat(new[]{node.Id}).ToArray();
            foreach(var id in _district.MeetingPeople)RememberSocial(id,node.Title+"："+node.Result);
            if(node.Id=="community.trial")GrantDistrictAccess(1);
            if(node.Id=="park.recommendation")GrantDistrictAccess(2);
            if(node.Id=="garden.fulfilment")GrantDistrictAccess(3);
            if(node.Id=="expo.review")GrantDistrictAccess(2);
            if(node.Id=="tender.review")GrantDistrictAccess(3);
            _district.MeetingPeople=Array.Empty<string>();_district.MeetingPlaceId=null;_district.MeetingNodeId=null;
            _district.WorkStartedAt=0;_district.WorkMinutes=0;
            var chain=ActiveDistrictChain(_district.Chapter);int index=Array.IndexOf(chain,node.Id)+1;
            _district.NodeId=index<chain.Length?chain[index]:null;
            Say("主线完成："+node.Title+(CurrentDistrictNode==null?"":"。下一条消息："+CurrentDistrictNode.Title));
        }
        private void GrantDistrictAccess(int tier)
        {
            _district.UnlockedTier=Math.Max(tier,_district.UnlockedTier);
            Show("道路已开放", "你完成了引荐与合作，通往"+DistrictName(tier)+"的道路已经放行。房租、采购和经营费用仍按正常规则支付。");
        }

        public string DebugDistrictPrerequisitesBlocker(int chapter)
        {
            if(chapter<1||chapter>3||!City.IsLakeCity)return "需要湖城地图";
            if(DistrictAccessTier<chapter-1)return "先完成上一段主线";
            if(_district.Chapter>chapter || _district.Chapter==chapter && CurrentDistrictNode==null)return "该段主线已完成";
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
                .OrderBy(p=>p==MainShop.Place?0:IsDedicatedVenue(p)?2:1).ToArray();
            if(DistrictOperatingShops(tier)+candidates.Length<count)return Fail("没有足够的可用铺位生成验收门店，未修改条件。");
            var previous=_focus;
            try
            {
                foreach(var place in candidates)
                {
                    if(DistrictOperatingShops(tier)>=count)break;
                    if(!MainShop.Rented && place==MainShop.Place)MoveInto(place,ShopRentQuote(place),Money.Zero);
                    else OpenNewShop(place,ShopRentQuote(place),Money.Zero);
                    if(IsDedicatedVenue(place))VenueOperation(place.Id).Accepting=true;
                    else
                    {
                        FitStarterShop();
                        PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,8);
                        PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu0"),true,8);
                    }
                    CurrentShop.RenovationDay=-1;
                    UpdateShopFit();
                }
            }
            finally{SetFocus(previous);}
            if(chapter==1 && Ledger.Balance<Money.FromYuan(1000000))DebugAddCash((100000000-Ledger.Balance.Cents+99)/100);
            if(chapter==2)
            {
                long gap=3000000000L-NetWorth().Cents;if(gap>0)DebugAddCash((gap+99)/100);
                _district.DebugListedIssuer=true;
                _district.DebugIssuerId="acceptance-own-brand";_district.DebugSectorId="computer";
            }
            if(chapter==3)
            {
                _district.DebugQualifiedMonth=true;_district.QualifiedMonth=Clock.Today.TotalDays/30-1;
                _district.DebugListedIssuer=true;_district.DebugIssuerId="acceptance-own-brand";_district.DebugSectorId="computer";
                _district.DebugSectorRevenueCents=10000000000L;_district.DebugSharePriceCents=10000;
                if(Ledger.Balance<Money.FromYuan(100000))DebugAddCash((10000000-Ledger.Balance.Cents+99)/100);
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
            // Saves with the superseded trading assignment resume at the clue, retaining unlocked districts.
            if(_district.UnlockedTier==0 && _district.NodeId=="community.trial" && !_district.CommunityMapQuest)
            {
                _district.CommunityMapQuest=true;_district.NodeId="community.clue";
                _district.MeetingPeople=Array.Empty<string>();_district.MeetingNodeId=null;_district.MeetingPlaceId=null;
            }
            if(_district.CommunityMapQuest && _district.CommunitySearchVersion<2 && !_district.HasCommunityMap && _district.UnlockedTier==0)
            {
                _district.CommunitySearchVersion=2;_district.CommunityAcceptedAt=Clock.Now.TotalMinutes;
                _district.CommunityClues=0;_district.CommunityPieces=0;_district.CommunityOutings=0;
            }
            _district.UnlockedTier=Math.Max(0,Math.Min(3,_district.UnlockedTier));
            _district.Chapter=Math.Max(0,Math.Min(3,_district.Chapter));
            if(CurrentDistrictNode==null){_district.NodeId=null;_district.MeetingPeople=Array.Empty<string>();_district.MeetingNodeId=null;}
            MigrateDistrictCommerce();
            // Legacy saves keep assets and location. Returning through a checkpoint is always allowed.
        }
    }
}
