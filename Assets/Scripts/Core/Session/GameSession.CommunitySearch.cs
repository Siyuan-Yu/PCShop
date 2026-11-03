using System;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const int CommunityLeadRelation = 30;
        public bool CommunitySearchActive => _district.NodeId=="community.search";
        public static string CommunityLeadPerson(int index) => new[]{"landlady","lin","zhou"}[index];
        public static string CommunityLeadActivity(int index) => new[]{"hike","dinner","old_market"}[index];
        public static string CommunityLeadVenue(int index) => new[]{"life_hike","life_restaurant","stall_cpu"}[index];
        public static string CommunityBoxPlace(int index) => new[]{"life_hike","life_court","metro_b"}[index];
        public static string CommunityBoxName(int index) => new[]{"绿绳旧居资料箱","蓝绳街铺资料箱","红绳市场资料箱"}[index];
        public static string CommunityPieceName(int index) => new[]{"旧居街图页","商业街图页","批发市场图页"}[index];
        public static string CommunityLeadHint(int index) => new[]{
            "我把旧居街那一页放进绿绳箱了，就在城郊平地散步公园入口。循着旧居街去找，别进居民家里。",
            "商业街那一页放在蓝绳箱里，我已托人放到青湾球场入口。还记得小时候一起打球吗？图上画着球场附近的老铺子。",
            "市场那一页在红绳箱里，我已经送到批发市场站入口，靠近入口找。纸上有那座旧水塔。"}[index];
        public bool CommunityHasClue(int index) => index>=0&&index<3&&(_district.CommunityClues&(1<<index))!=0;
        public bool CommunityHasPiece(int index) => index>=0&&index<3&&(_district.CommunityPieces&(1<<index))!=0;
        public bool CommunityHasOuting(int index) => index>=0&&index<3&&(_district.CommunityOutings&(1<<index))!=0;
        public bool CommunityBoxAvailable(int index) => CommunitySearchActive&&CommunityHasClue(index)&&!CommunityHasPiece(index);
#if UNITY_EDITOR
        public string DebugCommunityConditionsBlocker => !CommunitySearchActive
            ? "请先接受陈启明的寻物委托，并完成李秀兰对三位保管人的介绍。" : null;
        public bool DebugPrepareCommunityConditions()
        {
            string why=DebugCommunityConditionsBlocker;if(why!=null)return Fail(why);
            for(int i=0;i<3;i++)Meet(CommunityLeadPerson(i),CommunityLeadRelation);
            // Explicit acceptance fixture: satisfy quest evidence without inventing activity history.
            _district.CommunityOutings|=7;
            SetSmartphone(true);
            Say("已补齐李秀兰、林远航、周维成的好感与娱乐条件，并加入通讯录。请在主线消息中分别找他们打听，再去找箱子、拼图和交付。");
            return true;
        }
#endif
        private void AcceptCommunitySearch()
        {
            _district.CommunityMapQuest=true;_district.CommunitySearchVersion=2;
            _district.CommunityAcceptedAt=Clock.Now.TotalMinutes;
        }
        private void RecordCommunityOuting(CityActivity activity)
        {
            if(!_district.CommunityMapQuest||_district.CommunitySearchVersion!=2||_district.HasCommunityMap||_district.CommunityMapDelivered
                ||activity.State!=CityActivityPhase.Completed||!activity.RewardApplied||!activity.EntryPaid
                ||activity.ActualStart<_district.CommunityAcceptedAt)return;
            for(int i=0;i<3;i++)
                if(CityActivityService(activity)?.Id==CommunityLeadActivity(i)&&activity.VenueId==CommunityLeadVenue(i)
                    &&activity.Participants.Any(p=>p.PersonId==CommunityLeadPerson(i)))
                    _district.CommunityOutings|=1<<i;
        }
        public string CommunityLeadStatus(int index)
        {
            if(CommunityHasPiece(index))return "已找到："+CommunityPieceName(index);
            if(CommunityHasClue(index))return "线索："+CommunityLeadHint(index);
            return "好感 "+Relation(CommunityLeadPerson(index))+"/"+CommunityLeadRelation+"；接受委托后一起完成："
                +CityLifeCatalog.Activities.First(a=>a.Id==CommunityLeadActivity(index)).Name
                +"（"+(CommunityHasOuting(index)?"已完成":"未完成")+"）。两项达成后再向本人打听。";
        }
        public string AskCommunityLead(int index,bool remote=false)
        {
            if(index<0||index>2||!CommunitySearchActive)return "请先接受委托，向李秀兰问清三位保管人。";
            string id=CommunityLeadPerson(index);
            string blocker=remote?RemoteConversationBlocker(id):CitySamePlace(id)?null:"请先找到本人，在附近交谈。";
            if(blocker!=null)return blocker;
            if(CommunityHasClue(index))return CommunityLeadHint(index);
            if(Relation(id)<CommunityLeadRelation||!CommunityHasOuting(index))
                return "我确实保管着其中一页。等我们熟悉些，一起去喜欢的地方坐坐、走走，再聊这段旧街往事吧。\n"+CommunityLeadStatus(index);
            _district.CommunityClues|=1<<index;
            RememberSocial(id,"交代了旧街图页的藏箱线索。");
            return CommunityLeadHint(index)+"\n已记录线索，对应资料箱现在可以现场调查。";
        }
        public MapPoint CommunityBoxPosition(int index)
        {
            var place=City.GetPlace(CommunityBoxPlace(index));var stand=StandOf(place);var node=City.ViewPosition(place.NodeId);
            // Near the street end of the entrance corridor, clear of the facade.
            double dx=stand.X-node.X,dy=stand.Y-node.Y,length=Math.Sqrt(dx*dx+dy*dy);
            double ratio=length<.001?0:Math.Min(2,length)/length;
            return new MapPoint(node.X+dx*ratio,node.Y+dy*ratio);
        }
        public int NearbyCommunityBox
        {
            get
            {
                if(!CommunitySearchActive||CurrentPlace!=null||Trip!=null||IsWorking||CityActivityExclusive||IsGameOver)return -1;
                int best=-1;double distance=2.2;
                for(int i=0;i<3;i++)
                {
                    if(!CommunityBoxAvailable(i))continue;
                    double d=DistanceCity(PlayerPosition,CommunityBoxPosition(i));
                    if(d<distance){distance=d;best=i;}
                }
                return best;
            }
        }
        public bool SearchCommunityBox(int index)
        {
            if(index<0||index>2||NearbyCommunityBox!=index)return Fail("请先向对应保管人取得线索，再到藏点走近纸箱调查。");
            _district.CommunityBoxesInspected|=1<<index;_district.CommunityPieces|=1<<index;
            Show("找到图页", "已获得任务物品："+CommunityPieceName(index)+"。\n"+(_district.CommunityPieces==7?
                "三页已经找齐，请打开主线消息，按图上的街道接缝拼成完整商铺图。":"另外两页有各自的保管人与藏点，可以按任意顺序寻找。"));
            return true;
        }
        public bool AssembleCommunityMap()
        {
            if(!CommunitySearchActive||_district.CommunityPieces!=7||_district.HasCommunityMap)return Fail("需要先找齐三份不同的图页。");
            if(Trip!=null||IsWorking||CityActivityExclusive||IsGameOver)return Fail("请先结束当前行程或活动，再整理图页。");
            _district.HasCommunityMap=true;CompleteDistrictNode();
            Show("拼好了旧街手绘商铺图","你对齐三页的街道接缝，补齐了从旧居街到批发市场的旧街全图。\n请预约回城郊中介，当面交给陈启明与唐蔚。交付后才开放社区道路。");
            return true;
        }
    }
}
