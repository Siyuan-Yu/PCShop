using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum CityPersonTask { Home, Travelling, Working, Shopping, Waiting, Participating, Leisure, Returning }
    public enum CityActivityPhase { Offered, Scheduled, Gathering, Running, Completed, Cancelled, NoShow, Interrupted, Blocked }
    public enum CityParticipantPhase { NotDeparted, Travelling, Waiting, Participating, Left }

    [Serializable] public sealed class CityPersonDefinition
    {
        public string Id, Name, Role, HomeId, WorkId, Interest, Goal, Experience;
        public int Age = 28, WorkStart = 540, WorkEnd = 1080;
        public bool Recruitable;
        public string[] Topics, Stories;
        public string Circle, StoryTitle, FirstEncounter;
        public string[] LeisurePlaces;
        public bool KeyCharacter;
        public StaffDef Staff => new StaffDef(Id, Name, Role, Topics);
    }
    [Serializable] public sealed class CityRelationEdge
    {
        public string From, To, Kind, RevealTopic;
    }
    [Serializable] public sealed class CityPersonState
    {
        public string PersonId, HomePlaceId, Address, WorkPlaceId, LocationPlaceId, ActivityId, TaskLabel, VisitId;
        public int StoryChapter, LastStoryDay = -1;
        public string LeisureTargetId;
        public int LeisureStop;
        public long LeisureNextMove;
        public CityPersonTask Task;
        public double X, Y;
        public CityRoute Route;
        public string[] KnownRelations = Array.Empty<string>();
        public string[] KnownTopics = Array.Empty<string>();
        public CityPersonState Copy() => new CityPersonState { PersonId=PersonId,HomePlaceId=HomePlaceId,Address=Address,
            WorkPlaceId=WorkPlaceId,LocationPlaceId=LocationPlaceId,ActivityId=ActivityId,TaskLabel=TaskLabel,StoryChapter=StoryChapter,
            LeisureTargetId=LeisureTargetId,LeisureStop=LeisureStop,LeisureNextMove=LeisureNextMove,
            LastStoryDay=LastStoryDay,Task=Task,X=X,Y=Y,Route=Route?.Copy(),VisitId=VisitId,KnownRelations=KnownRelations?.ToArray()??Array.Empty<string>(),KnownTopics=KnownTopics?.ToArray()??Array.Empty<string>() };
    }
    [Serializable] public sealed class CityRoute
    {
        public string IntentId, TargetPlaceId;
        public string[] Nodes;
        public double[] X, Y, Meters;
        public long DepartMinute, ArriveMinute;
        public VehicleKind Transport;
        // JsonUtility may materialize an absent nested object as an empty instance. Such a value
        // is no journey and must never become a position at (0,0) or a return-home instruction.
        public bool HasPath => !string.IsNullOrEmpty(TargetPlaceId)&&X!=null&&Y!=null&&X.Length>=2&&X.Length==Y.Length&&Meters!=null&&Meters.Length>=X.Length-1;
        public CityRoute Copy() => !HasPath?null:new CityRoute { IntentId=IntentId,TargetPlaceId=TargetPlaceId,Nodes=Nodes?.ToArray(),
            X=X?.ToArray(),Y=Y?.ToArray(),Meters=Meters?.ToArray(),DepartMinute=DepartMinute,ArriveMinute=ArriveMinute,Transport=Transport };
        public MapPoint Position(long minute) => Position((double)minute);
        // Presentation samples fractional minutes; simulation decisions keep integer timestamps.
        public MapPoint Position(double minute)
        {
            if (X==null || X.Length==0) return default;
            double t=Math.Max(0,Math.Min(1,(double)(minute-DepartMinute)/Math.Max(1,ArriveMinute-DepartMinute)));
            double total=Meters?.Sum() ?? 0, distance=total*t;
            for(int i=0;i<X.Length-1;i++)
            {
                double len=Meters!=null && i<Meters.Length?Meters[i]:0;
                if(distance<=len && len>0) { double f=distance/len;return new MapPoint(X[i]+(X[i+1]-X[i])*f,Y[i]+(Y[i+1]-Y[i])*f); }
                distance-=len;
            }
            return new MapPoint(X[X.Length-1],Y[Y.Length-1]);
        }
    }
    public sealed class CityActivityDefinition
    {
        public string Id, Name, VenueId, StartText, MidText, EndText;
        public string[] Choices;
        public int Minutes, PriceYuan, Hunger, Energy, Relation;
        public int Mood, Health, Charm, Intelligence, Nutrition;
    }
    [Serializable] public sealed class CityActivityParticipant
    {
        public string PersonId, IntentId;
        public long DepartMinute;
        public CityParticipantPhase Phase;
        public bool TravelTransportChosen;
        public bool WorkForEmployer;
        public bool ResultCaptured;
        public int RelationBefore, RelationAfter, TrustBefore, TrustAfter;
        public VehicleKind TravelTransport;
        public CityActivityParticipant Copy() => (CityActivityParticipant)MemberwiseClone();
    }
    [Serializable] public sealed class CityActivity
    {
        public string Id, DefinitionId, SourceId, VenueId, HostId, EndReason;
        public string ServiceDefinitionId;
        public bool ServiceConfirmed;
        public int ServiceChoice = -1;
        public string PlayerIntentId;
        public CityRoute PlayerVenueRoute;
        public long ScheduledStart, ActualStart = -1, ActualEnd = -1;
        public long OccupiedFrom, OccupiedUntil;
        public bool EmployeeLeaveApproved;
        public int Duration, Grace = 60, Stage, Seat;
        public bool RendezvousReady;
        public string SettlementSummary;
        public long EntryCostCents;
        public bool EntryCostRecorded;
        public double[] PlayerStatsBefore, PlayerStatsAfter;
        public CityActivityPhase State;
        public bool Reminder, EntryPaid, MealApplied, MidApplied, RewardApplied;
        public string[] Choices = Array.Empty<string>();
        public CityActivityParticipant[] Participants = Array.Empty<CityActivityParticipant>();
        public bool Active => State==CityActivityPhase.Scheduled || State==CityActivityPhase.Gathering || State==CityActivityPhase.Running;
        public CityActivity Copy() => new CityActivity {Id=Id,DefinitionId=DefinitionId,SourceId=SourceId,VenueId=VenueId,HostId=HostId,
            EndReason=EndReason,ServiceDefinitionId=ServiceDefinitionId,ServiceConfirmed=ServiceConfirmed,ServiceChoice=ServiceChoice,PlayerIntentId=PlayerIntentId,PlayerVenueRoute=PlayerVenueRoute?.Copy(),OccupiedFrom=OccupiedFrom,OccupiedUntil=OccupiedUntil,EmployeeLeaveApproved=EmployeeLeaveApproved,ScheduledStart=ScheduledStart,ActualStart=ActualStart,ActualEnd=ActualEnd,
            Duration=Duration,Grace=Grace,Stage=Stage,Seat=Seat,State=State,RendezvousReady=RendezvousReady,SettlementSummary=SettlementSummary,EntryCostCents=EntryCostCents,EntryCostRecorded=EntryCostRecorded,
            PlayerStatsBefore=PlayerStatsBefore?.ToArray(),PlayerStatsAfter=PlayerStatsAfter?.ToArray(),Reminder=Reminder,EntryPaid=EntryPaid,MealApplied=MealApplied,
            MidApplied=MidApplied,RewardApplied=RewardApplied,Choices=Choices?.ToArray()??Array.Empty<string>(),
            Participants=(Participants??Array.Empty<CityActivityParticipant>()).Select(p=>p.Copy()).ToArray()};
    }
    [Serializable] public sealed class CityTravelIntent
    {
        public string Id, TargetPlaceId, ActivityId;
        public bool EnterOnArrival, Cancelled, NotifiedClosed;
    }
    [Serializable] public sealed partial class CityStoryProgress
    {
        public string Id, Branch, OrderSource, PersonId, PropertyId;
        public int Stage, Attempt;
        public long StartedMinute, CampaignEnd;
        public bool Failed, Rewarded;
    }

    /// <summary>原创静态内容。原居民和重点人物保留ID，不能按显示名合并。</summary>
    public static partial class CityLifeCatalog
    {
        public const string InvitationTestPersonId = "life_invitation_test";
        private static List<CityPersonDefinition> _people;
        public static IReadOnlyList<CityPersonDefinition> People => _people ?? (_people=BuildPeople());
        private static Dictionary<string,CityPersonDefinition> _personById;
        public static CityPersonDefinition Person(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(_personById==null)_personById=People.ToDictionary(p=>p.Id,StringComparer.Ordinal);
            return _personById.TryGetValue(id,out var person)?person:null;
        }
        public static readonly CityActivityDefinition[] Activities = ExpandVenueTiers(new CityActivityDefinition[] {
            A("dinner","一起吃家常饭","life_restaurant",60,100,55,6,6,"选餐与入座","热菜上桌，可以聊聊最近的生活。","吃完饭，记下这次相处。","清淡家常菜","招牌炖菜","聊近况","聊彼此的工作"),
            A("ktv","练歌与合唱","life_ktv",90,160,0,8,8,"选择包间活动","原创伴奏响起，大家轮流唱或坐下交流。","散场时一起整理好麦克风。","合唱原创曲","独唱练习","聊喜欢的声音","聊创作经历"),
            A("bar","吧台小聚","life_bar",60,80,10,5,5,"选择饮品","可以选无酒精饮品，认识同桌的人。","结清本次饮品后各自安排返程。","气泡果饮","热茶","聊近况","认识同桌"),
            A("court","组队打球","life_court",60,30,0,-8,6,"分组或练习","投篮和传球轮流进行，不以输赢决定交情。","赛后喝水，聊聊下一次。","传球练习","轮流投篮","赛后交流","聊运动习惯"),
            A("golf","练习场体验","life_golf",90,150,0,-5,7,"挑选练习位置","轮流挥杆，留一点时间交流。","归还器材，留下新的合作话题。","基础挥杆","轮流击球","聊生意","聊日常"),
            A("fishing","河岸钓鱼","life_park",90,40,0,5,6,"整理钓具","鱼漂起伏时，听对方讲一段往事。","收好垃圾和钓具，仍留在河岸。","安静垂钓","交流技巧","听往事","看河景"),
            A("hike","青湾郊游","life_hike",90,35,0,-8,7,"选择步道","走到观景平台，交流沿途见闻。","从步道回到入口，自己选择下一站。","轻松路线","观景平台","聊生活","分享经历"),
            A("market","市场走访","stall_cpu",60,0,0,-3,5,"约在电子市场门口","实际走访档口，交换供货信息。","结束走访，不自动购买商品。","看新品","比较供货条件","聊经营","聊行业消息"),
            A("old_market","旧物闲逛","stall_cpu",60,0,0,-3,5,"相约旧电子档口","慢慢看旧收音机和老唱片，听彼此讲旧事。","结束闲逛，各自选择下一站。","看旧收音机","聊评书","找老唱片","听旧设备故事"),
            A("snack","夜宵小聚","life_restaurant",60,60,40,3,5,"选夜宵","热汤端上来，聊聊一天的收获。","各自离席，约好下次见。","汤面","家常小菜","聊今天","聊共同朋友"),
            A("expo_electronics","电子展游览","life_expo",120,30,0,-4,7,"检票入场","供应商、新品、企业需求与行业联系人分区交流。","可提前离场，已完成展位事件保持记忆。","供应商新品","企业需求","行业联系人","交流试单条件"),
            A("expo_comic","原创潮玩展","life_expo",120,30,0,-4,7,"检票入场","原创模型、创作者工作台和兴趣社交区开放。","原创作品与联系人记录在自己的城市履历中。","原创模型商品","创作者工作台","兴趣社交","讨论推广合作")
        });
        private static CityActivityDefinition[] ExpandVenueTiers(CityActivityDefinition[] originals)
        {
            var all=originals.ToList();
            foreach(int tier in new[]{2,3,4})
            foreach(var source in originals.Where(a=>a.Id=="dinner"||a.Id=="ktv"||a.Id=="bar"))
            {
                var copy=A(source.Id+"_tier"+tier,new[]{"","","邻里","云庭","星苑"}[tier]+source.Name,
                    source.VenueId+"_tier"+tier,source.Minutes,source.PriceYuan*(tier==2?2:tier==3?5:12),source.Hunger,source.Energy,source.Relation,
                    source.StartText,source.MidText,source.EndText,source.Choices);
                copy.Mood=source.Mood;copy.Health=source.Health;copy.Charm=source.Charm;copy.Intelligence=source.Intelligence;copy.Nutrition=source.Nutrition;
                all.Add(copy);
            }
            return all.ToArray();
        }
        private static CityActivityDefinition A(string id,string name,string venue,int minutes,int price,int hunger,int energy,int relation,
            string start,string mid,string end,params string[] choices) => new CityActivityDefinition {Id=id,Name=name,VenueId=venue,Minutes=minutes,
                PriceYuan=price,Hunger=hunger,Energy=energy,Relation=relation,StartText=start,MidText=mid,EndText=end,Choices=choices,
                Mood=id=="ktv"?12:id=="fishing"||id=="hike"?10:id=="bar"||id=="court"?8:id=="market"?3:id=="snack"?5:6,
                Health=id=="court"||id=="hike"?2:id=="golf"?1:0,
                Charm=id=="ktv"||id=="bar"||id=="golf"?1:0,
                Intelligence=id=="market"||id.StartsWith("expo_")?1:0,
                Nutrition=id=="dinner"?3:id=="snack"?1:0};
        public static CityActivityDefinition Activity(string id) => Activities.FirstOrDefault(a=>a.Id==id);
        private static List<CityPersonDefinition> BuildPeople()
        {
            var result=new List<CityPersonDefinition>();
            result.Add(new CityPersonDefinition {
                Id=InvitationTestPersonId, Name="小游", Role="邀约验收伙伴", Age=28,
                HomeId="agency", WorkId=null, WorkStart=0, WorkEnd=0, Recruitable=false,
                Interest="聚餐、唱歌、运动和逛展", Goal="陪你走遍城市里的活动场所。",
                Experience="我最近时间很自由，没约出去的时候，一般就在安居房产中介门外附近散步。",
                Topics=new[]{"想去哪里玩？在聊天里邀请我，我们按日程碰面。","吃饭、唱歌、运动和逛展，我都愿意试试。","没约出去的时候，在中介附近就能找到我。","我认识中介的陈启明、同桌家常馆的杜建川、练歌房的温音，还有打球的郑禾。想打听谁，可以问我。"},
                Stories=new[]{"我们已经是很熟的朋友，也留好了联系方式。","我喜欢慢慢走遍一条街，再和朋友找个地方坐坐。"}
            });
            int i=0;
            foreach(var r in Residents.All)
            {
                var stories=r.Stories??new[]{r.Person.LineFor(0),r.Person.LineFor(1),r.Person.LineFor(2)};
                result.Add(new CityPersonDefinition {Id=r.Id,Name=r.Person.Name,Role=r.Person.Role,HomeId=Home(i++),WorkId=r.WorkPlaceId,
                    WorkStart=r.WorkStart,WorkEnd=r.WorkEnd,Interest=Interest(i),Goal=stories.LastOrDefault()??"把日子过安稳。",Experience=stories[0],
                    Topics=new[]{r.Person.LineFor(0),r.Person.LineFor(1),r.Person.LineFor(2),stories[0]},Stories=stories,Age=r.Id=="res_pupil"?14:26+i%28});
            }
            foreach(var n in Npcs.All)
            {
                if(result.Any(p=>p.Id==n.Id))continue;
                result.Add(new CityPersonDefinition {Id=n.Id,Name=n.Name,Role=n.Role,HomeId=Home(i++),WorkId=n.StallId??(n.Id==Npcs.Jie?"shop04":n.Id==Npcs.Zhang?"life_company":"life_studio"),
                    Interest=n.Likes??"dinner",Goal=n.Intro,Experience=n.FirstMeet??n.Intro,Topics=new[]{n.Intro,n.GiftReply,n.FirstMeet??"最近在忙自己店里的事。",n.Role},
                    Stories=new[]{n.Intro,"从第一份工作到现在的小生意，我花了好些年才学会量力而行。","有空一起到别的街区走走，生意之外也有想做的事。"},Age=34});
            }
            foreach(var staff in Rosters.All)
            {
                if(result.Any(p=>p.Id==staff.Id))continue;
                string work=staff==Rosters.CpuClerk?"stall_cpu":staff==Rosters.GpuClerk||staff==Rosters.GpuAssistant?"stall_gpu":staff==Rosters.MemClerk?"stall_mem":staff==Rosters.PsuClerk?"stall_psu":"agency";
                var stories=Rosters.StoriesOf(staff.Id)??new[]{staff.LineFor(0),staff.LineFor(1),staff.LineFor(2)};
                result.Add(new CityPersonDefinition{Id=staff.Id,Name=staff.Name,Role=staff.Role,HomeId=Home(i++),WorkId=work,WorkStart=540,WorkEnd=1200,
                    Interest=Interest(i),Goal=stories.LastOrDefault(),Experience=stories.FirstOrDefault(),Topics=new[]{staff.LineFor(0),staff.LineFor(1),staff.LineFor(2),stories.FirstOrDefault()},Stories=stories,Age=25+i%14});
            }
            foreach(var creator in Creators.All)
                result.Add(new CityPersonDefinition{Id=creator.Id,Name=creator.Name,Role="原创内容创作者（成年）",Age=28,HomeId=Home(i++),WorkId="life_studio",Interest=creator.Style,
                    Goal="在有供货与服务能力的店铺做长期合作，不只追求一次曝光。",Experience="从普通工作转到创作，慢慢积累自己的作品。",
                    Topics=new[]{creator.Style,"推广前会先确认商品和售后能力。","工作之外也想交朋友。","我认识纸风车工作室的陆栀。"},
                    Stories=new[]{"最开始只有几位朋友愿意看我的内容。","我想先把一件有用的作品做好。","合作和私人相处是两回事，我会分别认真对待。"}});
            var ids=new[]{"life_ming","life_ning","life_qiao","life_creator","life_enterprise","life_chef","life_singer","life_bartender","life_coach","life_golfer","life_curator","life_tenant","life_teacher","life_gardener","life_maker","life_librarian"};
            var names=new[]{"许明","顾宁","乔禾","陆栀","沈远","杜建川","温音","夏澄","郑禾","陶川","任帆","袁岚","宋青","方林","程弈","叶书"};
            var roles=new[]{"待业维修员","夜校学生兼店员","仓储协调员","原创模型创作者（27岁）","云帆企业采购联系人","同桌家常馆主厨","拾音练歌房接待","灯塔吧台店员","社区篮球教练","练习场教练","会展策划","文创街租户","社区老师","公园维护员","模型工艺师","社区图书管理员"};
            var works=new[]{(string)null,null,null,"life_studio","life_company","life_restaurant","life_ktv","life_bar","life_court","life_golf","life_expo","life_unit_s_creative_0","life_service","life_park","life_studio","life_service"};
            var interests=new[]{"原创模型","唱歌与会计","篮球和路线规划","原创模型与摄影","可靠的交付","当季家常菜","原创旋律","无酒精饮品","社区运动","户外交流","布展设计","老街手艺","邻里教育","河岸生态","机械结构","地方史"};
            var goals=new[]{"攒一套维修工具，先把基本工作做好。","用下班后的时间念完夜校，不想把生活只过成排班表。","搬来城里后想找一份稳定工作，也照顾好一起租房的朋友。","做一个属于这座城市的原创模型系列，作品不借别人的角色。","给小团队找到可靠供货方，试单合格再谈长期合作。","保留一道街坊吃得起的家常菜。","把自己写的旋律留在练歌房里。","让不喝酒的人也能自在参加聚会。","组织一支下班后能打球的队伍。","把练习场做成普通人也敢来的地方。","让展会的新人摊主也能被看见。","续租老铺前，先算清这几年的收支。","把邻里课堂恢复成每周一次。","沿河多留一段可以坐下来的地方。","用简单结构做耐玩的原创模型。","把老居民讲的故事整理成小册子。"};
            for(int j=0;j<ids.Length;j++) result.Add(new CityPersonDefinition{Id=ids[j],Name=names[j],Role=roles[j],HomeId=Home(i++),WorkId=works[j],
                WorkStart=j==6?840:j==7?1080:540,WorkEnd=j==6||j==7?120:1080,Age=j==3?27:24+j%17,Recruitable=j<3,
                Interest=interests[j],Goal=goals[j],Experience="我刚开始做"+roles[j]+"时，也经历过搬家和重新适应。",
                Topics=new[]{"我平时做"+roles[j]+"，最在意把约好的事做完。","工作之外，我喜欢"+interests[j]+"。",goals[j],"我和"+names[(j+1)%names.Length]+"常交流近况，有机会介绍你们认识。"},
                Stories=new[]{"我来到这里，是想把"+interests[j]+"变成生活中稳定的一部分。",goals[j],"现在比以前踏实些了；我希望身边的人也有重新开始的机会。"}});
            result.Add(new CityPersonDefinition{Id="life_clothier",Name="宋棉",Role="日常服饰导购",HomeId=Home(i++),WorkId="shop02",WorkStart=600,WorkEnd=1260,Age=32,Interest="实用穿搭与旧街散步",Goal="让普通预算也能买到合适的四季衣服。",Experience="我从仓库理货做起，后来学会按用途而不是价格推荐衣服。",Topics=new[]{"日常穿搭先看温度与活动。","基础冬装也可以很暖和。","我想让街坊少买不合适的衣服。","我和拾光衣舍的温岚以前一起做过陈列。"},Stories=new[]{"最初我在仓库记每件衣服的货号。","有位老人穿着我推荐的平价外套过冬后，专程来道谢。","我现在也带新人，先教他们听顾客真正需要什么。"}});
            result.Add(new CityPersonDefinition{Id="life_designer",Name="温岚",Role="原创服饰店主",HomeId=Home(i++),WorkId="clothing_boutique",WorkStart=600,WorkEnd=1260,Age=30,Interest="原创服装与展览",Goal="做有辨识度又不必追逐标志的原创服饰。",Experience="我曾在小工作室打版，攒够钱后才租下这间小店。",Topics=new[]{"我们做自己的剪裁和配色。","昂贵不等于保暖，短袖在冬天仍然薄。","我希望作品被喜欢而不是只被当作价签。","宋棉懂得把普通衣服搭得很得体。"},Stories=new[]{"我第一次做展览时，展架还是朋友帮忙搭的。","宋棉教我把试穿建议说得更简单。","我想和本地创作者一起办一场小型展示。"}});
            result.Add(new CityPersonDefinition{Id="life_clothsupplier",Name="许纬",Role="服装批发商",HomeId=Home(i++),WorkId="clothing_wholesale",WorkStart=540,WorkEnd=1260,Age=39,Interest="面料与城市骑行",Goal="按真实需求供货，让小店不用一换季就积压。",Experience="从小店送货员到独立批发商，我最重视交期。",Topics=new[]{"批发的起订量和运费要一起比较。","换季库存不会凭空消失，少量试卖更稳妥。","我计划把常用货源做得更透明。","宋棉和温岚都常来讨论选品。"},Stories=new[]{"我以前骑车给商户送样。","有一次晚到后，我重新整理了路线和到货说明。","现在我宁可说明配额，也不随便承诺不存在的货。"}});
            result.Add(new CityPersonDefinition {Id="life_model_supplier",Name="周岑",Role="模型批发行店主",HomeId=Home(i++),WorkId="model_market",WorkStart=540,WorkEnd=1260,Age=36,
                Interest="原创模型与旧街散步",Goal="把常规现货和供货条件说明白。",Experience="我从模型店理货做起，现在负责批发供货。",
                Topics=new[]{"喜欢什么系列，可以先看货再决定。","各系列常规现货都在价目表里。","进货要一起算重量和运费。","我和文创街的模型朋友们常聊新品。"},
                Stories=new[]{"最初我只负责给展柜整理价签。","现在希望小店能按自己的需求选货。"}});
            // Authored content is keyed by identity; hiring, moving and renaming never reshuffle a person's history.
            void Content(string id,string experience,string[] topics,string[] stories)
            {
                var person=result.First(p=>p.Id==id);person.Experience=experience;person.Topics=topics;person.Stories=stories;
            }
            Content("life_ming","旧维修铺结业后，我把最后一笔工资分成房租和工具钱，带着用了七年的螺丝刀留在了这里。",
                new[]{"以前修机器，我会把故障、换件和复测分开记。换工作也想保留这个习惯，别人接手时不用再猜。","陆栀画的原创模型，我喜欢先研究接缝。预算不多，买一件就会慢慢装很久。","我把旧工具放在租屋窗边，每攒够一点就添一件。最近先不买展示柜，房租和夜里的台灯更要紧。","顾宁念夜校，我有时帮她检查旧电脑；陆栀则认识我好多年，别把她只当成能带客流的联系人。"},
                new[]{"维修铺关门那天，师傅没有留住招牌，让我们各挑一件常用工具。我拿了那把手柄磨亮的螺丝刀，没拿最贵的。","找工作时我曾把会的项目写满一页。顾宁说，与其什么都答应，不如把能独立完成的事写清楚，我后来把简历重抄了一遍。","陆栀给我看过还没上色的模型样件。我帮她调顺了关节，她把我的名字写在制作记录里。第一次觉得，下班后的爱好也能认真留下痕迹。"});
            Content("life_ning","我白天做过门店工作，晚上读会计课程。第一本练习册被收银小票夹得鼓鼓的，一直没舍得扔。",
                new[]{"交班时我喜欢把未完成的事写在一张纸上。可靠不是把所有事扛在自己身上，是让下一班知道从哪里接。","练歌房对我来说不是比谁唱得好。我会选一段节奏慢的原创伴奏，把白天没理顺的呼吸重新放稳。","夜校还有一门课没结业。我想保留每周两晚看书的时间，换工作时会先说明，不想上班以后再让别人替我猜。","许明修过我的旧电脑，乔禾帮我找过离夜校近的公交换乘点。我们会交换实用消息，很少劝对方盲目辞职。"},
                new[]{"第一次去夜校，我带着当天的零钱袋，走到教室才发现没带笔。前排同学借给我一支，我后来一直多放一支在包里。","有段时间我一边背分录一边赶末班车，越急越容易记混。乔禾替我画了换乘路线，我才学会把学习和赶路留出各自的时间。","结业练习要整理一家小店的账。我没有挑数字好看的例子，而是把漏记的运费补了进去。老师说，这才是在帮一个真实的人看清日子。"});
            Content("life_qiao","做仓储协调时，我每天先看实际到货和路线，再安排下一趟。搬到城里以后，这本路线册也成了找工作的地图。",
                new[]{"采购清单里，我会分清已经有人负责的货和真正还缺的货。两个人都很勤快，也不能把同一个缺口买两次。","篮球让我习惯观察空位；看地图也一样，最短的直线不一定是推着货车能走的路。","我和朋友合租，公共支出记在冰箱边的小本子上。最近想把通勤固定下来，好轮流做晚饭。","顾宁的夜校路线我比较熟，陆栀搬展品时也会问我怎么少绕路。熟人之间先把载重和时间说清，反而更轻松。"},
                new[]{"刚搬来时，我照着地图找了一条近路，结果人能过，手推车过不了。那以后我在路线册上专门标了台阶和窄门。","合租的第一个月，我们都以为对方会买米，直到晚饭时一起打开空米袋。后来约好谁负责什么，不再靠客气猜来猜去。","一次帮陆栀运展品，我没抢着开走，而是先问清哪箱怕压。样件完整送到后，她请我在球场边喝了杯茶，那天比赶两趟空车都踏实。"});
            Content("life_creator","我做原创模型，也拍街角的门、窗和旧招牌。晨湾系列最早不是商品计划，而是回家路上随手画的几张速写。",
                new[]{"谈推广前我会先看库存和售后。作品可以帮人看见一家店，却不能替店里完成交付，更不能拿朋友信任掩盖缺货。","我喜欢拍清晨刚开门的街面，再把光线用进原创模型。没有现成角色可借，反而能慢慢找到自己的轮廓。","工作室租金和样件费用要分开留。我也想有不谈合作的晚上，散步、吃饭，聊一件没必要变成内容的小事。","许明会直说样件哪里卡手，袁岚记得老街门窗的故事，沈远则会问作品怎样服务真实使用者。三种眼光都很有用。"},
                new[]{"我的第一只样件在展桌上歪了一整天。许明收摊后帮我拆开底座，我们发现是我把承重想得太轻，不是少上一层漆就能解决。","后来拍旧街素材，我认识了袁岚。她没有让我随便拍租户的东西，而是先带我一家家说明用途，我才懂得城市不是随手取景的背景板。","有商家曾希望我只说好处。我拒绝了那次合作，回去却为下月房租发愁。沈远后来认真问起测试记录，让我觉得守住边界也能有慢一点的机会。"});
            Content("life_enterprise","我负责小团队的采购，曾经为一批延误的办公设备连续调整三次入职计划。现在更相信能验证的小承诺。",
                new[]{"试单先做两三台，配置、交期和售后写清楚。按时交付以后才有长期合作，饭局认识得再熟也不能省掉这些。","我对硬件跑分没有收藏癖，更关心新人第一天能不能顺利开工。闲下来会看展台怎样把复杂东西讲得简单。","家里的旧餐桌上还留着第一份项目清单。我现在尽量不把所有晚饭变成应酬，也给家人留完整的周末。","陆栀做作品很认真，杜建川会提醒我先吃热饭。行业里熟人介绍能打开话题，最后仍要各自把答应的事做完。"},
                new[]{"第一次独立采购，我只比较了单价。设备晚到，新同事借着旧机器开工，我才知道便宜的报价不等于便宜的结果。","后来有位供应商提前告诉我一款缺货，还列出不改变用途的替代建议。我没有直接接受，而是让使用部门一起确认；那次合作反而很稳。","我开始把大订单拆成能验证的小步骤。看到小店认真做完第一份试单，比在饭桌上听到宏大的保证更让我放心。"});
            Content("life_chef","我在饭馆后厨做了很多年，留下的手艺不是一道贵菜，而是怎样让忙了一天的人坐下吃口热的。",
                new[]{"当天有什么食材，就把适合的家常菜说明白。请客的人和被请的人都应该看得懂总价，不靠临时加项撑场面。","我试新菜会先做半锅，叫熟悉的街坊说真话。有人说淡，我会问清是口味还是身体需要。","每天收摊前我会留十分钟坐在门口，不看单子。要是从早忙到晚都没吃过自己的饭，做得再快也不算过好日子。","沈远常来吃简餐，温音偶尔下班后来打包。我会记得他们能吃什么，但不替客人把私事讲给下一桌听。"},
                new[]{"刚进后厨时，我把一道菜的调味全记在纸上，却没记开火顺序。老师傅让我看着一锅汤从冷到热，才明白步骤比背数字重要。","后来原料涨价，我试过把家常菜份量悄悄缩小。熟客没有吵，只问是不是换了碗，那次让我下决心把价格和份量都写明白。","现在菜单始终留一道普通预算能点的热菜。我不指望每桌都庆祝大事，有人只是想平平常常吃顿饭，也值得认真招待。"});
            Content("life_singer","练歌房的第一段原创提示音是我用旧键盘写的。现在我做接待，空闲时仍在慢慢整理自己的旋律。",
                new[]{"客人进来先问想唱、想聊还是想安静坐着。包间时间和费用都先讲清，不会因为一个人唱得少就催他表现。","我写旋律先哼很短的一句，再找它适合的节拍。没有现成歌名也没关系，大家能一起接上就挺好。","夜里下班，我会轻轻关门，免得吵醒合租的朋友。白天的安静时段对我很珍贵，不想把生活全挤成通宵。","杜建川有时留一份清淡的晚饭，夏澄帮我试过无酒精热饮。做夜班的人很懂得互相留一点余地。"},
                new[]{"第一份接待工作，我怕冷场，总抢着替客人选活动。后来一位客人说，他只是想和朋友坐一会儿，我才学会先问。","写第一段伴奏时，我戴着坏了一边的耳机反复听。夏澄把自己的旧耳机借来，没问什么时候能听到成品。","有次打烊前，几位街坊跟着我写的小旋律哼完了一段。没有人要我成为谁，那晚我回家把乐谱整理好了，决定继续写。"});
            Content("life_bartender","我想让吧台不只属于喝酒的人。第一张无酒精饮品单，是从给夜班朋友调一杯不影响回家休息的热饮开始的。",
                new[]{"点单时我先问是否需要无酒精、是否在意咖啡因。拒绝喝酒不需要解释，也不影响和同桌聊天。","我会记杯子的触感和香气，不只记颜色。好的饮品应该让人在自己的节奏里放松，不是靠灌得快。","收工后我常沿亮着灯的街口走一段，再回家睡觉。朋友约我白天出门，最好提前说，我会认真留时间。","温音写小曲时会来试饮，郑禾训练完更喜欢清爽果饮。他们从不同地方来，在同一张桌边也能聊上。"},
                new[]{"刚做吧台时，我学了一长串复杂名字。有位不喝酒的客人看了半天又走了，我才意识到菜单没有给他留下位置。","我试着做第一杯无酒精饮品，酸得自己都皱眉。温音没敷衍说好喝，而是陪我一小口一小口调整比例。","现在有人带着第一次来的朋友说，这里不用勉强喝酒。我喜欢这句介绍，比把客单价做高一点更像我想开的地方。"});
            Content("life_coach","我从企业业余队回来带社区训练。比起一场漂亮的比分，我更在意下班后来的人下次还愿不愿意出现。",
                new[]{"组队先看人数和体力，训练留热身与收操。谁迟下班就晚一点加入，不让普通锻炼变成另一张考勤表。","我喜欢看没有球的人怎样找位置。打得顺的时候，每个人都会知道自己能帮上什么忙。","我的球鞋补过一次底，暂时没换新。攒下的钱准备买几只公共训练球，让临时来的人不用空手站在旁边。","夏澄会给不喝酒的人留饮品选择，陶川把练习场的规则解释得很清楚。我们都想把门槛放得低一些。"},
                new[]{"以前在业余队，我总把训练强度排得一样。后来有队友上完夜班来，勉强跟完就再没出现，我才知道认真也要看人当天的状态。","第一次带社区活动只有三个人，我们没凑比赛，轮流练传球。结束时他们问下周还办不办，我比赢球时更开心。","现在我把公共球泵放在容易找到的地方。有人只是路过投几次，也算这片场地被好好使用了，不一定非要加入一支队。"});
            Content("life_golfer","我最初在练习场做清洁和捡球，靠下班后的空档一点点学动作，所以知道第一次来的人最怕什么。",
                new[]{"先讲安全线和轮流击球，再谈动作。体验课用普通球杆也能开始，不要求先买一整套装备。","我喜欢在户外留出说话的间隙。练习和商务可以放在同一天，但不是每一杆都要谈一笔生意。","休息日我会把旧球杆擦好，和家人去公园走走。好天气不用全交给工作，普通散步也算很好的户外活动。","郑禾会提醒新人先热身，任帆办活动时会画清集合区。安全和体面，都可以用很简单的方式做到。"},
                new[]{"第一次拿起球杆，我怕站错地方被笑，反复问了三遍。带我的人只把安全区又指了一次，我到现在还记得那种安心。","后来接待一场商务体验，有人一直追问谁打得最好。我把大家分成轮流练习的小组，终于有人愿意放下输赢问真正的问题。","如今我给第一次来的街坊留一段慢练时间。有人练了几杆就坐着聊天，也没有白来；知道自己喜欢什么，比装成熟练更有价值。"});
            Content("life_curator","我做过最早的一场小展览，开场前还蹲在地上贴指路纸。现在画布展图时，仍会先想一个陌生人从门口怎样找到第一张桌子。",
                new[]{"先把入口、用电和撤展时间说明白，再谈展位好不好看。新人摊主不知道的事，主办方不能默认他们已经懂了。","我会观察展桌之间留了多少停下来的空间。展览不只是让货摆得密，也要让创作者能把一件作品讲完整。","展期结束的第二天，我通常不安排新的应酬。把借来的东西归还、把费用结清，才算真的做完一届。","袁岚熟悉老铺的实际使用，陶川重视安全区域。和他们商量场地，常能发现图纸上看不出来的问题。"},
                new[]{"第一次布展，我把最好看的桌子留在入口，却挡住了搬运车。师傅没有替我强行挤过去，而是让我自己推一趟空车重新走。","有个新人摊主错过了说明会，带着不合适的展架来。我陪他借了普通桌子，又把常见问题整理成一页清单，后来每届都发。","现在每次散展，我会最后看一眼没有招牌的场地。桌面干净、设备齐全、款项清楚，下一届的人才会愿意再把作品带来。"});
            Content("life_tenant","我租着文创街的一间老铺，做过陈列、修过窗框，也亲手把每月电费和房租抄在账本里。店面不是招牌换个名字那么简单。",
                new[]{"续租前我会看剩余租期、维护责任和真实客流。房东换了也不代表店里的货和手艺就跟着换了主人。","我喜欢旧木头留下的使用痕迹，修东西会先问原来怎么用。保留旧街不是让它什么都不能变。","家和店在不同街区，晚上收工后走那一段路能让我换个心情。我想把生意做稳，也想留时间陪家里人。","陆栀拍旧街前会先征求同意，任帆布展时愿意听租户说实际困难。陈启明谈合同也会把使用权和产权分开说明。"},
                new[]{"刚接这间铺子时，我只看见大窗，没看见雨天会漏水的边框。修好它花掉了第一笔备用金，从此我看房会先看需要维护的地方。","有一次街口施工，熟客绕了远路才找到我。我和邻铺一起做了清楚的临时指示，也把那几天的收支单独记下来，没有把损失说成谁故意作对。","听说物业可能换主人时，我最担心的是老租约没人认。后来认真把合同翻完，才有余力谈下一年的合作。想做长期生意，双方都需要看得见的规则。"});
            Content("life_teacher","我在社区教过一间只有折叠桌的小课堂。孩子带来的问题常常比教材大人写好的答案更具体。",
                new[]{"课堂材料先选家里容易找到的东西。知识不该因为买不起一套工具就隔开一条线。","我喜欢把街边的树、招牌和老故事变成练习题。走出去观察，比一直坐着背答案更容易记住。","我想恢复每周一次邻里课堂，但也要先留好备课和休息时间。热心若全靠透支，往往坚持不了多久。","袁岚愿意讲老铺经历，方林认识河岸的植物。我会先问他们愿不愿意，再带学生去拜访，不突然把人围住。"},
                new[]{"第一次办邻里课堂，我准备了一长页讲义，孩子却盯着窗外问那棵树为什么少了一根枝。后来我把讲义收起来，陪他们出去看。","有位家长担心孩子没有新文具。我把下一次活动改成用旧纸盒做结构模型，也请大家讲自己家里能找到什么材料。","停课一段时间后，街坊问能不能重开。我没有立即答应每天都办，而是先定每周一次。能稳定回来，比一次说出很大的计划更好。"});
            Content("life_gardener","我维护河岸和公园，不是把每一块地方都修成一样整齐。季节到了，植物和来散步的人都有自己的需要。",
                new[]{"修枝、浇水和检查步道要看天气。不能只顾草坪好看，却让坐轮椅的街坊没地方经过。","我会记哪一段河岸先开花，也会看雨后水往哪里走。观察得久，才知道哪里适合添一张椅子。","家里窗台放着几盆不名贵的苗。下班后我有时只给它们浇水，别的事留到明天，日子也不必总赶着长。","宋青办课堂前会先来问植物能不能碰，程弈喜欢观察枝干的结构。大家看同一棵树，注意的地方很不一样。"},
                new[]{"刚做维护时，我把一片野花当杂草清掉了。常来散步的老人问起，我才学着先辨认，再决定该怎样整理。","后来想加一排观景座椅，我原本只挑最漂亮的方向。试着陪街坊推了一段轮椅，才发现还要给转身和遮阴留空间。","现在每次修完一段步道，我会坐下来看看人怎么用它。有人愿意停下歇一会儿，就是这份工作很实在的回报。"});
            Content("life_maker","我从修小机械转到做模型工艺。工作台上最常用的是一盒按尺寸分好的旧零件，不是最昂贵的机器。",
                new[]{"样件先检查连接和受力，再追求表面好看。零件明明不合适，就不能用一句手工特色带过去。","我喜欢看机械结构怎样把很少的动作变得有趣。玩具不一定要复杂，能让人反复拿起来才难。","我攒钱换的是带照明的工作台，不是展示柜。手边有个舒服的位置，慢慢做一件东西，比赶着出很多样更适合我。","方林会指给我看树枝怎样分担重量，叶书帮我找过老城工坊的资料。灵感不只在展柜里。"},
                new[]{"小时候拆过一只坏闹钟，我没修好，却把螺丝分得整整齐齐。后来做工艺，第一次被师傅夸的也是收拾工作台，不是动作快。","第一批可动样件做完，我发现连续摆动会磨损连接处。把它们拆开重做很心疼，但送到别人手里之前知道问题，总比之后找借口好。","一次去河岸散步，方林讲树枝的分叉，我回去改了模型的支撑结构。那以后我给自己留散步时间，工作不一定都在桌前发生。"});
            Content("life_librarian","我整理地方资料，也听老居民口述。有些年份记不清，但一个人怎样走到今天，不该因为没有整齐的纸张就被漏掉。",
                new[]{"口述资料会注明是谁在什么时候说的，记忆和档案对不上也先保留差异，不替别人编一个漂亮而确定的故事。","我喜欢比对旧地图和现在的路口。看见街道改了名字，再听人说当年的日常，城市才不只是几个地标。","家里有一只装着待整理纸条的木盒。我给自己定了每周一小叠，休息时就合上，不把兴趣变成永远做不完的加班。","程弈会来查老工坊结构，宋棉在旧街散步时能记住很多细节。介绍他们认识前，我会先问双方愿不愿意留联系方式。"},
                new[]{"第一次整理邻里的旧照片，我急着写齐所有名字。有位老人指着空白说，想不起来就先空着，这句话后来成了我的工作习惯。","程弈查一间老工坊，我找到的图纸只有半张。我们沿着旧地图走到原址，没找到答案，却弄清哪一部分只能说是推测。","现在我给每份口述留下回访位置。有人过很久才补上一段记忆也没关系，记录不是一次问完后就再也不见。"});
            ConfigureNarrativePeople(result);
            return result;
        }
        private static string Interest(int i) => new[]{"dinner","court","ktv","fishing","market","hike"}[i%6];
        private static string Home(int i) => "life_home_"+new[]{"s_oldhomes","s_market","s_services","s_night","s_creative","s_sports","s_expo","s_newtown"}[i%8];
        public static IEnumerable<CityRelationEdge> Relations
        {
            get
            {
                foreach(var edge in NarrativeRelations()) yield return edge;
                foreach (var tie in SocialTies.All)
                {
                    yield return new CityRelationEdge { From=tie[0], To=tie[1], Kind=tie[2], RevealTopic="friends" };
                    yield return new CityRelationEdge { From=tie[1], To=tie[0], Kind=tie[2], RevealTopic="friends" };
                }
                // Authored connections mentioned in these characters' own conversations and histories.
                foreach (var tie in new[] {
                    new[] { InvitationTestPersonId, Rosters.AgentChen.Id, "在中介附近常一起散步的街坊" },
                    new[] { InvitationTestPersonId, "life_chef", "常一起吃饭的朋友" },
                    new[] { InvitationTestPersonId, "life_singer", "一起练歌的朋友" },
                    new[] { InvitationTestPersonId, "life_coach", "一起打球的朋友" },
                    new[] { Npcs.Li, "life_enterprise", "供货往来的熟人" },
                    new[] { "life_ming", "life_creator", "相识多年的模型朋友" },
                    new[] { "life_ming", "life_ning", "帮忙修过电脑的朋友" },
                    new[] { "life_ning", "life_qiao", "互相照应通勤的朋友" },
                    new[] { "life_qiao", "life_creator", "帮忙运输展品的朋友" },
                    new[] { "life_creator", "life_tenant", "文创街的熟人" },
                    new[] { Rosters.AgentChen.Id, "life_tenant", "租铺时认识的熟人" },
                    new[] { "life_enterprise", "life_creator", "工作中认识的朋友" },
                    new[] { "life_enterprise", "life_chef", "常来吃饭的熟客" },
                    new[] { "life_chef", "life_singer", "常来吃饭的熟客" },
                    new[] { "life_singer", "life_bartender", "交流饮品与创作的朋友" },
                    new[] { "life_bartender", "life_coach", "训练后常碰面的熟人" },
                    new[] { "life_coach", "life_golfer", "社区运动圈的朋友" },
                    new[] { "life_golfer", "life_curator", "一起筹备活动的熟人" },
                    new[] { "life_curator", "life_tenant", "办展时认识的熟人" },
                    new[] { "life_tenant", "life_teacher", "邻里课堂认识的熟人" },
                    new[] { "life_teacher", "life_gardener", "邻里课堂认识的熟人" },
                    new[] { "life_gardener", "life_maker", "交流植物与模型的朋友" },
                    new[] { "life_maker", "life_librarian", "交流老城资料的朋友" },
                    new[] { "life_clothier", "life_designer", "一起做过陈列的老同事" },
                    new[] { "life_clothsupplier", "life_clothier", "服装供货往来" },
                    new[] { "life_clothsupplier", "life_designer", "服装供货往来" }
                })
                {
                    yield return new CityRelationEdge { From=tie[0], To=tie[1], Kind=tie[2], RevealTopic="friends" };
                    yield return new CityRelationEdge { From=tie[1], To=tie[0], Kind=tie[2], RevealTopic="friends" };
                }
            }
        }
    }
}
