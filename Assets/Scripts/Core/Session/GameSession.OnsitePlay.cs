using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class OnsitePlayState
    {
        public string ActivityId,Phase="ready",Pending,Message;
        public int Number,Mode,Selected,Round,Success,OpponentScore,Mask,Feedback=-1,Attempts,Base,Flavor,Sweet,Subject,PhotoCount,GoodPhotos,PhotoSubjects;
        public int[] TalkMasks=Array.Empty<int>();
        public bool MixedCorrect,ProcurementDecided,OrderOffered;
        public long ReadyAt;
        public double Time,Power,Angle,Flight,Distance,LandingX,LandingZ,BallX,BallY=1.7,BallZ,Wind,TargetX,TargetZ,DefenderX,ShotOriginX;
        public double PhotoYaw=25,PhotoPitch=25,PhotoDistance=4,PhotoOffset,Exposure=1;
        public OnsitePlayState Copy(){var c=(OnsitePlayState)MemberwiseClone();c.TalkMasks=TalkMasks?.ToArray()??Array.Empty<int>();return c;}
    }
    [Serializable] public sealed class OnsitePhoto
    {
        public string Id,ActivityId,SubjectId,File; public int Score,Width,Height; public long At;
        public OnsitePhoto Copy()=>(OnsitePhoto)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        private static readonly string[] OnsiteIds={"act_bar_bartender","act_bar_taoran","act_bar_zengyu","act_court_coach","act_court_dongyu","act_court_zhang","act_golf_golfer","act_golf_enterprise"};
        private static readonly string[] OnsitePeople={"life_bartender","resident_taoran","resident_zengyu","life_coach","resident_dongyu","zhang","life_golfer","life_enterprise"};
        private readonly List<OnsitePlayState> _onsitePlays=new List<OnsitePlayState>();
        private readonly List<OnsitePhoto> _onsitePhotos=new List<OnsitePhoto>();
        private int _onsitePhotoSequence;
        public static int OnsiteNumber(string id)
        {var bits=(id??"").Split('_');return bits.Length>=2&&bits[0]=="social"&&int.TryParse(bits[1],out int n)&&(n>=51&&n<=58||n==76)?n:0;}
        public static bool IsOnsiteActivity(CityActivity a)=>OnsiteNumber(a?.DefinitionId)!=0;
        private static bool OnsiteWorkingMeeting(string definition,string person)=>OnsiteNumber(definition)==58&&person=="life_enterprise"||OnsiteNumber(definition)==76&&person=="resident_ningxi";
        public OnsitePlayState OnsiteState(string id)=>_onsitePlays.FirstOrDefault(s=>s.ActivityId==id)?.Copy();
        public OnsitePlayState CurrentOnsite=>OnsiteState(RunningCityActivity?.Id);
        public OnsitePhoto[] OnsitePhotos=>_onsitePhotos.Select(x=>x.Copy()).ToArray();
        public static readonly string[] OnsitePhotoSubjects={"model:garden:greenhouse","model:rainbot:repairer","model:starlight:rover"};
        private OnsitePlayState Play=>_onsitePlays.FirstOrDefault(s=>s.ActivityId==RunningCityActivity?.Id);
        private bool OnsiteHere=>RunningCityActivity is CityActivity a&&CurrentPlace?.Id==a.VenueId&&!_sleeping&&!IsWorking&&!IsGameOver&&a.Participants.All(p=>CitySamePlace(p.PersonId));
        internal bool CanOfferOnsite(int n)
        {
            string place=n<=53?"life_bar":n<=56?"life_court":n<=58?"life_golf":"life_expo";
            return (n>=51&&n<=58||n==76)&&City.HasPlace(place)&&DistrictAccessBlocker(City.GetPlace(place))==null
                &&(n!=76||HasSmartphone)&&!_cityActivities.Any(a=>a.Active&&OnsiteNumber(a.DefinitionId)==n);
        }
        private EventDef BindOnsiteEvent(EventDef template,int n)
        {
            if(!CanOfferOnsite(n))return null;
            string place=n<=53?"life_bar":n<=56?"life_court":n<=58?"life_golf":"life_expo";
            string person=n==76?"resident_ningxi":OnsitePeople[n-51];
            string[] guests=n==52?new[]{person,"life_ning","resident_luoya"}:new[]{person};
            string definition="social_"+n;
            long Start(string def)
            {
                long first=SuggestCityActivityStartAt(def,person,0,place);
                for(int d=0;d<40*96;d++)
                {long at=first+d*15L;if(n==76&&!CityExpoDay(at,true))continue;if(CityActivityBlockerAt(def,guests,at,false,place)==null)return at;}
                return -1;
            }
            var choices=new List<EventChoice>();
            void Offer(string label,string def,bool treat=false,bool host=false)
            {
                long at=Start(def);choices.Add(Choose(label,s=>
                {
                    var a=ScheduleCityActivityAt(def,guests,at,false,"onsite:"+n+":"+_nextCityActivity,place);if(a==null)return "暂无共同空闲时段，未扣款。";
                    a.HostId=host?person:treat?"player":person;a.HostPays=host;a.PlayerTreat=treat;a.ServiceConfirmed=true;a.ServiceChoice=0;
                    return "已预约"+new PCShopSell.Core.Time.GameTimestamp(at)+"，到场付款并实际操作，未完成现场任务不结算完成奖励。";
                },s=>at>=0,"暂无共同空闲时段"));
            }
            if(n is 51 or 53 or 76)Offer("确认赴约 · 邀请人招待",definition,host:true);
            else
            {
                Offer("各付自己的费用",definition);Offer("这场我请",definition,treat:true);
                if(n==52){Offer("陶然请客 · 接受邀请",definition,host:true);Offer("臻享聚会 · 各付¥1,800","social_52_premium");}
                if(n==58)Offer("私人指导 · 各付¥2,500","social_58_lesson");
            }
            choices.Add(Choose("改天再约",s=>"没有预约或扣款，也不损失交情。"));
            string body=n switch{
                51=>"夏澄请你实际品尝两款饮品，再选口味评价；店方招待，没有瞬间试喝奖励。",
                52=>"陶然、顾宁和罗雅的聚会。普通饮品餐食¥600/人，臻享¥1,800/人；可AA、你请四人或接受陶然招待。选择具体的人和话题，按各自兴趣产生不同反馈。",
                53=>"曾玉想听听你对几种新饮品的感受。到吧台聊一聊，选一杯，等她调好后亲自尝尝。",
                54=>"郑禾的篮球训练¥1,200/人，选择定点、上篮或防守训练，按住蓄力、松开出手，实际完成三轮。",
                55=>"董宇的新地板体验¥600/人，实际体验运球、急停和跳投，再根据脚感选择反馈。",
                56=>"与张绍文打五个回合，¥1,200/人。看防守位置，选择投篮或突破，自己控制方向和力度。输赢都按实际回合结算。",
                57=>"陶川的高尔夫练习¥2,500/人。选择铁杆、木杆或挖起杆，调整方向，按住蓄力松开击球，观察真实飞行和落点，完成五杆。",
                58=>"商务球叙¥6,800/人，也可选¥2,500/人指导。至少打三杆后选择采购话题；可明确不承诺，谈成才形成真实订单，不在答应邀约时发订单。",
                _=>"与宁汐到每月22–24日原创模型展摄影。选择展品、角度、远近和曝光，保存真实照片。至少三张照片、两种展品且两张构图合格才完成。"};
            return EventScene(template,body,choices.ToArray());
        }
        private void StartOnsitePlay(CityActivity a)
        {
            int n=OnsiteNumber(a.DefinitionId);if(n==0||_onsitePlays.Any(s=>s.ActivityId==a.Id))return;
            var state=new OnsitePlayState{ActivityId=a.Id,Number=n,TalkMasks=new int[a.Participants.Length],Message="打开现场操作界面，按自己的选择完成活动。"};
            if(n==53)state.Message="曾玉朝你招招手，把几只杯子摆到了吧台上。";
            _onsitePlays.Add(state);SetNextOnsiteTarget(state);
        }
        private void SetNextOnsiteTarget(OnsitePlayState s)
        {s.TargetZ=s.Number<=56?(s.Mode==1?5.2:8):new[]{60.0,85,110}[s.Round%3];s.TargetX=s.Number<=56?0:new[]{0.0,-12,12}[s.Round%3];s.Wind=s.Number>=57?new[]{-2.0,1.5,3}[s.Round%3]:0;}
        public bool SelectOnsiteDrink(int drink)
        {var s=Play;if(!OnsiteHere||s?.Number!=51||s.Pending!=null||drink<0||drink>2||s.Round>=2)return false;s.Selected=drink;return true;}
        public bool SipOnsiteDrink()
        {var s=Play;if(!OnsiteHere||s?.Number!=51||s.Pending!=null||s.Round>=2||(s.Mask&(1<<s.Selected))!=0)return false;QueueOnsite(s,"sip",3);StartOnsiteTimer(s);return true;}
        public bool OnsiteDrinkFeedback(int opinion)
        {var s=Play;if(!OnsiteHere||s?.Number!=51||s.Round<2||s.Feedback>=0||opinion<0||opinion>2)return false;s.Feedback=opinion;s.Message=new[]{"你指出偏甜的部分，夏澄记下了配方调整。","你喜欢香气，夏澄讲解了风味来源。","你建议减弱气泡，夏澄重新核对了比例。"}[opinion];return true;}
        private static void QueueOnsite(OnsitePlayState s,string kind,int minutes){s.Pending=kind;s.ReadyAt=0;s.Distance=minutes;s.Message="正在"+(kind=="sip"?"品尝":kind=="chat"?"交流":kind=="mix"?"调配":"试饮")+"，现场时间继续推进。";}
        private void StartOnsiteTimer(OnsitePlayState s){s.ReadyAt=Clock.Now.TotalMinutes+(int)s.Distance;}
        public bool TalkOnsiteGuest(int person,int topic)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=52||s.Pending!=null||person<0||person>=s.TalkMasks.Length||topic<0||topic>2||s.TalkMasks[person]!=0)return false;
            s.Selected=person;s.Mode=topic;QueueOnsite(s,"chat",3);StartOnsiteTimer(s);return true;
        }
        public bool SetOnsiteRecipe(int slot,int option)
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3||slot<0||slot>2||option<0||option>2)return false;if(slot==0)s.Base=option;else if(slot==1)s.Flavor=option;else s.Sweet=option;return true;}
        public bool MixOnsiteDrink()
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3)return false;s.MixedCorrect=s.Base==1&&s.Flavor==0&&s.Sweet==0;QueueOnsite(s,"mix",5);StartOnsiteTimer(s);return true;}
        public bool ChooseTastingDialogue(int choice)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase=="mixed"||s.Attempts>=3||choice<0||choice>2)return false;
            s.Selected=choice;
            s.Base=new[]{1,2,0}[choice];s.Flavor=new[]{0,1,2}[choice];s.Sweet=new[]{0,1,2}[choice];
            if(!MixOnsiteDrink())return false;
            s.Message="曾玉：好，稍等，我来调这杯。";return true;
        }
        public bool TasteOnsiteMix()
        {var s=Play;if(!OnsiteHere||s?.Number!=53||s.Pending!=null||s.Phase!="mixed")return false;QueueOnsite(s,"taste",2);StartOnsiteTimer(s);return true;}
        public bool SelectOnsiteSportMode(int mode)
        {var s=Play;if(!OnsiteHere||s==null||s.Number<54||s.Number>58||s.Phase is "flight" or "charging"||mode<0||mode>2)return false;s.Mode=mode;s.Phase="ready";s.Power=0;s.BallX=s.BallZ=0;s.BallY=1.7;SetNextOnsiteTarget(s);return true;}
        public bool AimOnsiteSport(double degrees)
        {var s=Play;if(!OnsiteHere||s==null||s.Number<54||s.Number>58||s.Phase is "flight" or "charging")return false;s.Angle=Math.Max(-50,Math.Min(50,degrees));return true;}
        public bool BeginOnsiteCharge()
        {
            var s=Play;if(!OnsiteHere||s==null||s.Number<54||s.Number>58||s.Phase is "flight" or "charging"||s.Round>=8)return false;
            SetNextOnsiteTarget(s);s.Phase="charging";s.Power=0;s.Flight=0;s.BallX=s.BallZ=0;s.BallY=1.7;return true;
        }
        public bool ReleaseOnsiteShot()
        {
            var s=Play;if(!OnsiteHere||s?.Phase!="charging")return false;
            s.Phase="flight";s.Flight=0;double rad=s.Angle*Math.PI/180;
            double range=s.Number<=56?(s.Mode==1?8:13)*s.Power:new[]{100.0,180,55}[s.Mode]*s.Power*s.Power;
            s.Distance=range;s.LandingX=Math.Sin(rad)*range+s.Wind;s.LandingZ=Math.Cos(rad)*range;s.ShotOriginX=0;
            s.Message="球已出手，观察飞行和落点。";return true;
        }
        public void StepOnsiteSport(double seconds)
        {
            var s=Play;if(!OnsiteHere||s==null||s.Number<54||s.Number>58||seconds<=0)return;
            double dt=Math.Min(.1,seconds);s.Time+=dt;s.DefenderX=Math.Sin(s.Time*1.7+s.Round)*2;
            if(s.Phase=="charging"){s.Flight+=dt;s.Power=1-Math.Abs((s.Flight%2)-1);}
            else if(s.Phase=="flight")
            {
                s.Flight+=dt;double duration=s.Number<=56?1.4:2.6;double t=Math.Min(1,s.Flight/duration);
                s.BallX=s.LandingX*t;s.BallZ=s.LandingZ*t;
                s.BallY=s.Number<=56?1.7*(1-t)+3.05*t+2.5*Math.Sin(Math.PI*t):.2+Math.Sin(Math.PI*t)*Math.Max(3,s.Distance*.2);
                if(s.Number==55&&s.Mode==0){s.BallX=s.BallZ=0;s.BallY=.2+Math.Abs(Math.Sin(t*Math.PI*3))*1.2;}
                if(t<1)return;
                double miss=Math.Sqrt(Math.Pow(s.LandingX-s.TargetX,2)+Math.Pow(s.LandingZ-s.TargetZ,2));
                bool blocked=s.Number==56&&(s.Mode==0&&Math.Abs(s.DefenderX)<.55||s.Mode==1&&s.DefenderX>1.3);
                bool good=s.Number<=56?miss<.65&&!blocked:miss<10;
                if(s.Number==55&&s.Mode<2)good=Math.Abs(s.Power-(s.Mode==0?.55:.7))<.15;
                if(s.Number==54&&s.Mode==2)good=Math.Abs(s.DefenderX)>1&&s.Power>.35&&s.Power<.8;
                s.Round++;if(good)s.Success++;s.Mask|=1<<s.Mode;s.Phase="result";
                Player.SpendEnergy(s.Number<=56?3:2);
                if(s.Number==56&&(!good||Math.Sin(s.Time*1.3)>.6))s.OpponentScore+=2;
                s.Message=s.Number<=56?(blocked?"张老师封住了出手路线。":good?"命中！":"没有命中，看看方向和力度再试。"):$"落点距目标 {miss:0.0}米 · 实际飞行{s.Distance:0.0}米";
            }
        }
        public bool OnsiteCourtFeedback(int feedback)
        {var s=Play;if(!OnsiteHere||s?.Number!=55||s.Round<3||s.Mask!=7||feedback<0||feedback>2||s.Feedback>=0)return false;s.Feedback=feedback;s.Message="董宇记录了你对"+new[]{"抓地力","急停脚感","回弹"}[feedback]+"的实际体验。";return true;}
        public bool DecideOnsiteProcurement(int topic)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=58||s.Round<3||s.ProcurementDecided||topic<0||topic>2)return false;s.ProcurementDecided=true;
            if(topic==2){s.Message="暂不承诺供货，没有订单或收入。";return true;}
            if(!HasOpenedComputerShop){s.Message="尚未开办具备交付能力的电脑店，未接采购单。";return true;}
            double chance=EventScript.ProgressiveChance(.45,(Player.Intelligence,50,.01),(Relation("life_enterprise"),35,.008));
            if(!Random.Chance(chance)){s.Message="已讨论"+(topic==0?"用途、型号和保修":"预算、数量和交付周期")+"，暂未达成采购意向。";return true;}
            s.OrderOffered=true;s.Message=OfferCityReferralContract("onsite58:"+s.ActivityId,"云帆企业 · 沈远现场确认",1,false);return true;
        }
        public bool SetOnsitePhotoFrame(int subject,double yaw,double pitch,double distance,double offset,int exposure)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=76||subject<0||subject>=OnsitePhotoSubjects.Length||double.IsNaN(yaw)||double.IsNaN(pitch)||double.IsNaN(distance)||double.IsNaN(offset))return false;
            s.Subject=subject;s.PhotoYaw=Math.Max(-180,Math.Min(180,yaw));s.PhotoPitch=Math.Max(5,Math.Min(75,pitch));s.PhotoDistance=Math.Max(1,Math.Min(8,distance));s.PhotoOffset=Math.Max(-2,Math.Min(2,offset));s.Exposure=Math.Max(0,Math.Min(2,exposure));return true;
        }
        public int OnsitePhotoScore
        {
            get {var s=Play;if(s?.Number!=76)return 0;return Math.Max(0,100-(int)(Math.Abs(s.PhotoDistance-4)*20+Math.Abs(s.PhotoPitch-30)*.7+Math.Abs(s.PhotoOffset)*35+Math.Abs(s.Exposure-1)*30+Math.Max(0,Math.Abs(s.PhotoYaw)-90)*1.3));}
        }
        public bool RecordOnsitePhoto(string file,int width,int height)
        {
            var s=Play;if(!OnsiteHere||s?.Number!=76||!HasSmartphone||string.IsNullOrEmpty(file)||file.Contains("..")||file.Contains("/")||file.Contains("\\")||!file.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||width<320||height<180||_onsitePhotos.Any(p=>p.File==file)||s.PhotoCount>=12)return false;
            int score=OnsitePhotoScore;_onsitePhotos.Add(new OnsitePhoto{Id="onsite-photo:"+(++_onsitePhotoSequence),ActivityId=s.ActivityId,SubjectId=OnsitePhotoSubjects[s.Subject],File=file,Width=width,Height=height,Score=score,At=Clock.Now.TotalMinutes});
            s.PhotoCount++;if(score>=60){s.GoodPhotos++;s.PhotoSubjects|=1<<s.Subject;}s.Message=$"照片已保存，构图评分{score}/100；本场{s.PhotoCount}张。";return true;
        }
        private void TickOnsitePlay(long now)
        {
            var s=Play;if(!OnsiteHere||s==null||s.Pending==null)return;
            if(s.ReadyAt==0){StartOnsiteTimer(s);return;}
            if(now<s.ReadyAt)return;
            string pending=s.Pending;s.Pending=null;
            if(pending=="sip"){s.Round++;s.Mask|=1<<s.Selected;Player.Mood=Math.Min(100,Player.Mood+2);s.Message="已实际品尝"+new[]{"莓果气泡","桂花乌龙","姜汁柠檬"}[s.Selected]+"，可继续品尝并给出评价。";}
            if(pending=="chat")
            {
                var a=RunningCityActivity;bool preferred=s.Mode==s.Selected%3;s.TalkMasks[s.Selected]=1<<s.Mode;AddRelation(a.Participants[s.Selected].PersonId,preferred?3:1);
                s.Message=CityPerson(a.Participants[s.Selected].PersonId)?.Name+(preferred?"对这个话题很有兴趣，分享了自己的经验。":"聊了近况，但这不是对方最感兴趣的话题。");
            }
            if(pending=="mix"){s.Phase="mixed";s.Message="曾玉：这杯好了。来，尝一口，告诉我你喝起来是什么感觉。";}
            if(pending=="taste")
            {
                s.Attempts++;s.Phase="ready";s.Round++;if(s.MixedCorrect)
                {
                    bool learned=s.Success==0;s.Success++;Player.Mood=Math.Min(100,Player.Mood+5);if(learned)Player.Intelligence=Math.Min(100,Player.Intelligence+1);
                    s.Message="入口清爽，酸甜和气泡恰到好处，味道简直完美。\n心情+5"+(learned?"，智力+1。":"。")+"\n曾玉：看你的表情，这杯你应该挺喜欢。";
                }
                else{Player.Health=Math.Max(0,Player.Health-2);Player.SpendEnergy(3);s.Message="这股味道有点奇怪，酸涩里带着说不出的苦，胃里也不太舒服。\n健康−2，精力消耗3。\n曾玉：你皱眉了？我再琢磨琢磨这杯的味道。";}
            }
        }
        private bool OnsiteCompletionReady(CityActivity a)
        {
            int n=OnsiteNumber(a.DefinitionId);if(n==0)return true;var s=_onsitePlays.FirstOrDefault(x=>x.ActivityId==a.Id);if(s==null||s.Pending!=null||s.Phase is "charging" or "flight")return false;
            return n switch{51=>s.Round>=2&&s.Feedback>=0,52=>s.TalkMasks.Length==a.Participants.Length&&s.TalkMasks.All(x=>x!=0),53=>s.Round>=1&&s.Phase!="mixed",54=>s.Round>=3&&(s.Mask&(s.Mask-1))!=0,55=>s.Round>=3&&s.Mask==7&&s.Feedback>=0,56=>s.Round>=5,57=>s.Round>=5,58=>s.Round>=3&&s.ProcurementDecided,76=>s.PhotoCount>=3&&s.GoodPhotos>=2&&(s.PhotoSubjects&(s.PhotoSubjects-1))!=0,_=>false};
        }
        private void CompleteOnsitePlay(CityActivity a)
        {
            var s=_onsitePlays.FirstOrDefault(x=>x.ActivityId==a.Id);if(s==null)return;
            if(s.Number==52&&a.PlayerTreat)foreach(var p in a.Participants)AddRelation(p.PersonId,2);
            if(s.Number==52&&a.HostPays)AddRelation(a.HostId,1);
            if(s.Number>=54&&s.Number<=57&&s.Success>=2)Player.Skill=Math.Min(100,Player.Skill+1);
            if(s.Number>=54&&s.Number<=57)Player.Health=Math.Min(100,Player.Health+1);
            if(s.Number==76&&s.GoodPhotos>=3)Player.Intelligence=Math.Min(100,Player.Intelligence+1);
            a.EventSettlementNote=(a.EventSettlementNote??"")+(s.Number==53?"\n你和曾玉在吧台尝了新饮品，也聊了各自的感受。":"\n实际回合"+s.Round+"，成功"+s.Success+"，照片"+s.PhotoCount+"。");
        }
        private void SaveOnsitePlay(SaveData d){d.onsitePlays=_onsitePlays.Select(x=>x.Copy()).ToArray();d.onsitePhotos=_onsitePhotos.Select(x=>x.Copy()).ToArray();d.onsitePhotoSequence=_onsitePhotoSequence;}
        private void RestoreOnsitePlay(SaveData d)
        {
            _onsitePlays.Clear();foreach(var x in d.onsitePlays??Array.Empty<OnsitePlayState>()){var s=x.Copy();if(string.IsNullOrEmpty(s.Pending))s.Pending=null;_onsitePlays.Add(s);}
            _onsitePhotos.Clear();_onsitePhotos.AddRange((d.onsitePhotos??Array.Empty<OnsitePhoto>()).Select(x=>x.Copy()));_onsitePhotoSequence=d.onsitePhotoSequence;
        }
    }
    public sealed partial class SaveData
    {public OnsitePlayState[] onsitePlays;public OnsitePhoto[] onsitePhotos;public int onsitePhotoSequence;}
}
