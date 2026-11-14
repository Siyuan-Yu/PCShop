using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public static class LifeEventGoods
    {
        public static readonly GiftDef[] Books = {
            new GiftDef("life:book_accounts", "小店记账入门", "把货款、借款和收入分开记录。",120,.5,"社区图书角"),
            new GiftDef("life:book_repairs", "旧设备维修手记", "故障、修理与复测实例。",180,.6,"社区图书角"),
            new GiftDef("life:book_streets", "湖城街巷故事", "居民整理的地方口述。",150,.5,"社区图书角") };
        public static readonly GiftDef DogFood = new GiftDef("life:dog_food", "犬用主粮（一份）", "供家庭宠物食用。",40,.3,"社区服务站");
        public static GiftDef Get(string id) => Books.FirstOrDefault(b => b.Id == id) ?? (id == DogFood.Id ? DogFood : null);
    }
    [Serializable] public sealed class LifeEventTask
    {
        public string Id, Kind, Place, Person, Item, Activity, Message;
        public int Remaining, Fee, Mode, Count; public long LastMinute, At;
        public bool Started, Done; public double X,Y,Distance;
        public LifeEventTask Copy() => (LifeEventTask)MemberwiseClone();
    }
    [Serializable] public sealed class LifeDogState
    {
        public string Stage, Place; public int Hunger=80, Bond, FeedDay=-1, WalkDay=-1, LastDay;
        public LifeDogState Copy() => (LifeDogState)MemberwiseClone();
    }
    [Serializable] public sealed class LifeCourseState
    {
        public int Course, Lessons, Score, Granted; public string Activity; public bool WithNing, Paid;
        public LifeCourseState Copy() => (LifeCourseState)MemberwiseClone();
    }
    [Serializable] public sealed class LifeStallState
    {
        public string Item, Name, Buyer, Message; public bool Furniture, Done, Ended; public int Remaining, Sold;
        public long Price, Ends, NextBuyer; public LifeStallState Copy() => (LifeStallState)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        public const string LifeServicePlace="life_service", LifeFitnessPlace="life_fitness", LifeFairPlace="life_market";
        private readonly List<LifeEventTask> _lifeTasks=new List<LifeEventTask>();
        private readonly Dictionary<string,long> _lifeContact=new Dictionary<string,long>();
        private readonly HashSet<string> _lifeRead=new HashSet<string>();
        private int[] _lifeBookStock={2,2,2}; private int _lifeBudget,_lifeSequence,_fluDay=-1,_fluExposure,_gymRounds;
        private long _fluSeasonUntil,_fluProtectionUntil,_fluIllUntil,_noiseAt;
        private string _noisePlace,_noisePerson; private bool _noiseHandled=true,_gymReady;
        private LifeDogState _lifeDog; private LifeCourseState _lifeCourse; private LifeStallState _lifeStall;
        public LifeEventTask[] LifeTasks=>_lifeTasks.Where(t=>!t.Done).Select(t=>t.Copy()).ToArray();
        public LifeDogState LifeDog=>_lifeDog?.Copy(); public LifeCourseState LifeCourse=>_lifeCourse?.Copy(); public LifeStallState LifeStall=>_lifeStall?.Copy();
        public int[] LifeBookStock=>(int[])_lifeBookStock.Clone();
        public long FluIllUntil=>_fluIllUntil; public long FluProtectionUntil=>_fluProtectionUntil;
        public int LifeDonationTotal=>_lifeTasks.Where(t=>t.Kind=="donation"&&t.Done).Sum(t=>t.Mode);
        public int LifeLibraryUnspent=>_lifeBudget;
        public bool LifeGymReady=>_gymReady; public int LifeGymRounds=>_gymRounds;
        public bool LifeDogFollowing=>_lifeDog?.Stage=="adopting" || _lifeTasks.Any(t=>t.Kind=="dog-walk"&&t.Started&&!t.Done);
        private bool LifeHere(string place)=>CurrentPlace?.Id==place;
        private bool LifeOpen(string place)=>City.HasPlace(place)&&DistrictAccessBlocker(City.GetPlace(place))==null
            &&(ShopAt(place)?.Rented==true||City.GetPlace(place).Kind==PlaceKind.Home&&CanSleepAt(City.GetPlace(place))||IsPlaceOpen(City.GetPlace(place)));
        private bool LifeAccessible(string place)=>City.HasPlace(place)&&DistrictAccessBlocker(City.GetPlace(place))==null;
        private LifeEventTask NewLifeTask(string kind,string place,int minutes,int fee=0)
        { var t=new LifeEventTask{Id="life-event:"+(++_lifeSequence),Kind=kind,Place=place,Remaining=minutes,Fee=fee,Mode=kind=="donation"?fee:0,LastMinute=Clock.Now.TotalMinutes};_lifeTasks.Add(t);return t; }
        private bool HasLifeTask(string kind)=>_lifeTasks.Any(t=>!t.Done&&t.Kind==kind);
        private string LifeOldFriend()=>_cityPeople.Values.Where(p=>IsMet(p.PersonId)&&p.PersonId!=CityLifeCatalog.InvitationTestPersonId
            &&(!_lifeContact.TryGetValue(p.PersonId,out var last)||Clock.Now.TotalMinutes-last>=7*1440L)).OrderByDescending(p=>Relation(p.PersonId)).Select(p=>p.PersonId).FirstOrDefault();
        internal bool CanOfferLifeBatch(int n)
        {
            InitializeCityPeople();
            return n switch {
                111=>LifeAccessible(LifeServicePlace)&&!HasLifeTask("donation"),
                112=>ShopBatchShop()!=null&&Clock.Now.TotalMinutes>=_fluSeasonUntil,
                113=>!HasLifeTask("neighbor")&&!_noiseHandled&&Clock.Now.TotalMinutes-_noiseAt<2*1440&&CityPersonState(_noisePerson)!=null,
                114=>LifeAccessible(LifeServicePlace)&&_lifeDog==null&&!Flags.Contains("life:dog-offered")&&CurrentPlace!=null,
                115=>LifeAccessible(LifeServicePlace)&&CityPersonState("life_singer")!=null&&!HasLifeTask("music"),
                116=>HasSmartphone&&LifeOldFriend()!=null&&!HasLifeTask("chat"),
                117=>LifeAccessible(LifeFitnessPlace)&&!_gymReady,
                118=>LifeAccessible(LifeServicePlace)&&CityPersonState("life_librarian")!=null&&!Flags.Contains("life:library-invited"),
                119=>LifeAccessible(LifeServicePlace)&&CityPersonState("life_teacher")!=null&&(_lifeCourse==null||_lifeCourse.Lessons>=4),
                120=>Clock.Today.IsWeekend&&LifeAccessible(LifeFairPlace)&&EventIdleItems.Length>0&&(_lifeStall==null||_lifeStall.Done),_=>false };
        }
        private EventDef BindLifeBatch(EventDef template,int n)
        {
            if(!CanOfferLifeBatch(n))return null;
            if(n==111)return EventScene(template,"社区服务站正在为图书角募书。到场登记，捐款用于实际补充可借阅的图书。",
                new[]{500,2000,5000}.Select(f=>Choose("捐¥"+f+" · 到场登记10分钟",_=>{NewLifeTask("donation",LifeServicePlace,10,f);return "已记入社区生活待办，到场登记时才付款。";})).Concat(new[]{Choose("这次不捐",_=>"没有扣款。")}).ToArray());
            if(n==112)
            {
                _fluSeasonUntil=Clock.Now.TotalMinutes+14*1440L;var shop=ShopBatchShop();
                return EventScene(template,"最近有流感传播。防护能降低风险，不能保证免疫。",
                    Choose("到店消毒 · ¥300 / 30分钟",_=>{NewLifeTask("disinfect",shop.Place.Id,30,300);return "到店从社区生活待办开始，完成后防护持续7天。";}),
                    Choose("注意休息",_=>{NewLifeTask("rest",Home.Id,60);return "可到家休息，生病时也能看诊或吃药。";}),Choose("暂不处理",_=>"没有扣款或立即判定生病。"));
            }
            if(n==113)
            {
                string person=_noisePerson,place=_noisePlace;
                EventChoice Reply(string text,bool quiet)=>Choose(text,_=>{var result=QueueLifeVisit("neighbor","social_113",person,place,quiet?1:0);if(HasLifeTask("neighbor"))_noiseHandled=true;return result;});
                return EventScene(template,(CityPerson(person)?.Name??person)+"提到你在"+new GameTimestamp(_noiseAt)+"的夜间搬动或卸货影响休息。",
                    Reply("道歉说明情况",false),Reply("协商以后白天处理",true),Choose("不理会",_=>{_noiseHandled=true;AddRelation(person,-2);return "你没有回应，对方交情−2。";}));
            }
            if(n==114)
            {
                Flags.Add("life:dog-offered");_lifeDog=new LifeDogState{Stage="stray",Place=LifeServicePlace,LastDay=Clock.Today.TotalDays};
                return EventScene(template,"社区服务站门口有一只流浪狗。可以走到它旁边喂食，或选择收养、联系救助。",
                    Choose("过去看看",_=>"狗的位置已记入社区生活待办，收养后需要实际喂食和遛狗。"),
                    Choose("联系救助",_=>{QueueDogRescue();return "已联系救助者，按日程在现场交接，完成后狗才离开。";}));
            }
            if(n==115)
            {
                string place=LifeServicePlace;
                return EventScene(template,"歌手准备在街边唱一首歌，你可以约好到场听约10分钟。",
                    Choose("听一首",_=>QueueLifeVisit("music","social_115","life_singer",place)),Choose("赶时间",_=>"没有预约或属性奖励。"));
            }
            if(n==116)
            {
                string friend=LifeOldFriend();return EventScene(template,(CityPerson(friend)?.Name??friend)+"发来消息，问你最近过得怎样。",
                    Choose("聊近况 · 15分钟",_=>{var t=NewLifeTask("chat",null,15);t.Person=friend;return "手机聊天已记入待办，实际聊完才增加这位朋友的交情。";}),
                    Choose("约见面",_=>QueueLifeVisit("friend","social_116",friend,LifeServicePlace)),Choose("稍后回复",_=>"没有强制推进时间，也不加魅力。"));
            }
            if(n==117)return EventScene(template,"公园健身场欢迎体验，按实际完成的训练量结算。",
                Choose("去体验",_=>{_gymReady=true;_gymRounds=0;return "到公园健身场选择快走、器械或拉伸，完成三轮约30分钟。";}),Choose("暂时不去",_=>"没有即时健康奖励。"));
            if(n==118)return EventScene(template,"叶书邀请你到社区图书角看书，第一次可免费领一本，以后带书交换。",
                Choose("去看看",_=>{Flags.Add("life:library-invited");return "到社区服务站与叶书见面，选书后实物进入背包，阅读需要实际时间。";}),Choose("改天再去",_=>"没有凭空拿到书或增加智力。"));
            if(n==119)return EventScene(template,"社区夜校四节课，每节60分钟，整期¥1,000。可选记账、采购或沟通，实际到课并完成课堂题。",
                Choose("查看课程",_=>{Flags.Add("life:school-invited");return "到社区服务站选择课程报名，可邀请认识的顾宁同行。";}),Choose("暂不报名",_=>"没有扣学费。"));
            return EventScene(template,"禾田集市周末开放临时摊位，¥200租半天。带实际闲置物品，真实顾客到场购买后才收款。",
                Choose("准备摆摊",_=>{Flags.Add("life:fair-invited");return "到禾田集市选择实物、数量与售价，未售物品会退回。";}),Choose("只去逛逛",_=>{Flags.Add("life:fair-invited");return "可去现有集市浏览实际商品，没有自动消费或收入。";}));
        }
        private string QueueLifeVisit(string kind,string definition,string person,string place,int mode=0)
        {
            long at=SuggestCityActivityStartAt(definition,person,0,place);
            var a=ScheduleCityActivityAt(definition,new[]{person},at,false,"life-event:"+(++_lifeSequence),place);
            if(a==null)return "目前没有共同空闲时段，未预约。";
            a.ServiceConfirmed=true;a.ServiceChoice=0;var t=NewLifeTask(kind,place,0);t.Person=person;t.Activity=a.Id;t.Mode=mode;return "已加入真实日程，实际到场后继续。";
        }
        public bool StartLifeTask(string id)
        {
            var t=_lifeTasks.FirstOrDefault(t=>t.Id==id&&!t.Done&&!t.Started&&t.Activity==null);if(t==null||IsWorking||CityActivityExclusive||_sleeping||IsGameOver)return false;
            if(t.Kind=="chat"&&!HasSmartphone)return false;
            if(_lifeTasks.Any(x=>x.Started&&!x.Done&&x.Remaining>0))return Fail("先暂停当前生活事项。");
            if(t.Place!=null&&(!LifeHere(t.Place)||!LifeOpen(t.Place)))return Fail("请到对应地点的开放时段开始。");
            if(t.Kind=="read"&&Backpack.GiftCount(t.Item)<1)return false;
            if(t.Fee>0&&!Ledger.TryDebit(Clock.Today,t.Kind=="donation"?"社区募书捐款":t.Kind=="disinfect"?"防护用品":"收养基础安置",Money.FromYuan(t.Fee),t.Kind))return Fail("现金不足。");
            t.Fee=0;t.Started=true;t.LastMinute=Clock.Now.TotalMinutes;t.X=PlayerPosition.X;t.Y=PlayerPosition.Y;return true;
        }
        public void PauseLifeTask(string id){var t=_lifeTasks.FirstOrDefault(t=>t.Id==id);if(t!=null)t.Started=false;}
        public bool BorrowLifeBook(int book,string exchange=null)
        {
            if(!Flags.Contains("life:library-invited")||!LifeHere(LifeServicePlace)||!CitySamePlace("life_librarian")||book<0||book>=3||_lifeBookStock[book]<1)return false;
            if(!CanCarry(LifeEventGoods.Books[book].WeightKg))return false;
            bool first=!Flags.Contains("life:first-book");
            if(!first)
            {int old=Array.FindIndex(LifeEventGoods.Books,b=>b.Id==exchange);if(old<0||!Backpack.TakeGift(exchange))return Fail("请带一本图书来交换。");_lifeBookStock[old]++;}
            _lifeBookStock[book]--;Backpack.AddGift(LifeEventGoods.Books[book].Id,1);Flags.Add("life:first-book");Meet("life_librarian",12);ApplyCargoLimit();return true;
        }
        public bool ReadLifeBook(string id)
        {
            if(Backpack.GiftCount(id)<1||!LifeEventGoods.Books.Any(b=>b.Id==id)||HasLifeTask("read"))return false;
            var t=NewLifeTask("read",Home.Id,30);t.Item=id;return true;
        }
        public bool BuyLifeDogFood(int count)
        {
            if(count<1||count>20||!LifeHere(LifeServicePlace)||!LifeOpen(LifeServicePlace)||!CanCarry(.3*count))return false;
            if(!Ledger.TryDebit(Clock.Today,"犬用主粮",LifeEventGoods.DogFood.Price*count,"购买实际主粮"))return false;
            Backpack.AddGift(LifeEventGoods.DogFood.Id,count);ApplyCargoLimit();return true;
        }
        public bool LifeDogHere=>_lifeDog!=null&&(_lifeDog.Stage=="stray"&&LifeHere(_lifeDog.Place)||_lifeDog.Stage=="adopted"&&LifeHere(Home.Id)||LifeDogFollowing);
        public bool TakeLifeFluMedicine()
        {
            string key="life:flu-medicine:"+Clock.Today.TotalDays;
            if(_fluIllUntil<=Clock.Now.TotalMinutes||Flags.Contains(key)||!Ledger.TryDebit(Clock.Today,"医疗",Money.FromYuan(20),"流感对症药物"))return false;
            Flags.Add(key);_fluIllUntil=Math.Min(_fluIllUntil,Clock.Now.TotalMinutes+1440);Player.SpendEnergy(5);return true;
        }
        public bool FeedLifeDog()
        {
            if(!LifeDogHere||_lifeDog.Stage=="rescued"||_lifeDog.FeedDay==Clock.Today.TotalDays||!Backpack.TakeGift(LifeEventGoods.DogFood.Id))return false;
            _lifeDog.Hunger=Math.Min(100,_lifeDog.Hunger+50);_lifeDog.FeedDay=Clock.Today.TotalDays;_lifeDog.Bond=Math.Min(100,_lifeDog.Bond+2);ApplyCargoLimit();return true;
        }
        public bool AdoptLifeDog()
        {
            if(_lifeDog?.Stage!="stray"||!LifeDogHere||HasLifeTask("dog-rescue")||HasLifeTask("adopt"))return false;
            if(!Ledger.TryDebit(Clock.Today,"收养基础安置",Money.FromYuan(800),"基础用品与收养登记"))return false;
            _lifeDog.Stage="adopting";NewLifeTask("adopt",LifeServicePlace,30);return true;
        }
        private void QueueDogRescue()
        {if(_lifeDog?.Stage!="stray"||HasLifeTask("dog-rescue"))return;QueueLifeVisit("dog-rescue","social_114","life_gardener",_lifeDog.Place);}
        public bool RescueLifeDog(){if(_lifeDog?.Stage!="stray")return false;QueueDogRescue();return HasLifeTask("dog-rescue");}
        public bool WalkLifeDog()
        {
            if(_lifeDog?.Stage!="adopted"||!LifeDogHere||_lifeDog.WalkDay==Clock.Today.TotalDays||HasLifeTask("dog-walk"))return false;
            NewLifeTask("dog-walk",null,10);return true;
        }
        public bool LifeGymRound(int mode)
        {
            if(!_gymReady||mode<0||mode>3||HasLifeTask("gym")||!LifeHere(LifeFitnessPlace))return false;
            var t=NewLifeTask("gym",LifeFitnessPlace,mode==3?5:10);t.Mode=mode;return true;
        }
        public bool TipLifeMusic(int yuan)
        {
            var t=_lifeTasks.FirstOrDefault(t=>t.Kind=="music"&&!t.Done);var a=_cityActivities.FirstOrDefault(a=>a.Id==t?.Activity);
            if(t==null||t.Mode!=0||a?.State!=CityActivityPhase.Running||!LifeHere(t.Place)||!CitySamePlace(t.Person)||yuan is not (200 or 1000))return false;
            if(!Ledger.TryDebit(Clock.Today,"街头音乐打赏",Money.FromYuan(yuan),CityPerson(t.Person)?.Name))return false;t.Mode=1;return true;
        }
        public bool SingLifeMusic()
        {
            var t=_lifeTasks.FirstOrDefault(t=>t.Kind=="music"&&!t.Done);var a=_cityActivities.FirstOrDefault(a=>a.Id==t?.Activity);
            if(t==null||t.Count!=0||a?.State!=CityActivityPhase.Running||!LifeHere(t.Place)||Clock.Now.TotalMinutes-a.ActualStart<5)return false;
            t.Count=Random.Chance(EventScript.ProgressiveChance(.4,(EffectiveCharm,45,.01)))?2:1;t.Message=t.Count==2?"合唱跟上了节奏。":"没有跟上节奏，仍可继续听。";Say(t.Message);return true;
        }
        public bool EnrollLifeCourse(int course,bool withNing)
        {
            if(!Flags.Contains("life:school-invited")||course<0||course>2||Flags.Contains("life:course-completed:"+course)||!LifeHere(LifeServicePlace)||!LifeOpen(LifeServicePlace)||_lifeCourse!=null&&_lifeCourse.Lessons<4)return false;
            if(withNing&&!IsMet("life_ning"))return Fail("先认识顾宁才能邀请同行。");
            var people=withNing?new[]{"life_teacher","life_ning"}:new[]{"life_teacher"};
            if(!Enumerable.Range(0,14).Any(d=>{long at=Clock.Today.AddDays(d).TotalDays*1440L+1140;return at>=Clock.Now.TotalMinutes+30&&CityActivityBlockerAt("social_119",people,at,false,LifeServicePlace)==null;}))return Fail("暂无共同晚课时段，未扣学费。");
            if(!Ledger.TryDebit(Clock.Today,"夜校学费",Money.FromYuan(1000),new[]{"经营记账","采购实务","沟通课程"}[course]))return false;
            _lifeCourse=new LifeCourseState{Course=course,WithNing=withNing,Paid=true};return BookLifeLesson();
        }
        public bool BookLifeLesson()
        {
            if(_lifeCourse==null||_lifeCourse.Lessons>=4||_cityActivities.Any(a=>a.Id==_lifeCourse.Activity&&a.Active))return false;
            string[] people=_lifeCourse.WithNing?new[]{"life_teacher","life_ning"}:new[]{"life_teacher"};
            for(int d=0;d<14;d++)
            {
                long start=Clock.Today.AddDays(d).TotalDays*1440L+19*60;if(start<Clock.Now.TotalMinutes+30)continue;
                if(CityActivityBlockerAt("social_119",people,start,false,LifeServicePlace)!=null)continue;
                var a=ScheduleCityActivityAt("social_119",people,start,false,"night-course:"+_lifeSequence+":"+_lifeCourse.Lessons,LifeServicePlace);
                if(a==null)continue;a.ServiceConfirmed=true;a.ServiceChoice=0;_lifeCourse.Activity=a.Id;
                var t=NewLifeTask("lesson",LifeServicePlace,0);t.Activity=a.Id;t.Mode=-1;return true;
            }
            Say("暂无共同空闲晚课，学费已记录，可在生活面板重新约课。");return true;
        }
        private int LifeLessonIndex => _lifeCourse==null?0:_lifeCourse.Course*4+Math.Min(3,_lifeCourse.Lessons);
        private static readonly string[] LifeQuestions={"借款到账应记为？","营业额和利润相同吗？","顾客退款怎样记账？","月末应核对什么？","采购应优先核对？","在途货是现货吗？","过低的报价怎样处理？","备货数量根据什么？","顾客提出异议先做什么？","交货会迟怎么办？","不清楚需求时？","应该怎样承诺？"};
        private static readonly string[][] LifeAnswers={
            new[]{"销售收入","借款及债务","利润"},new[]{"不同，利润要扣成本","完全相同","都是现金"},new[]{"忽略","当作新收入","按原支付调整销售"},new[]{"只看收入","账实、应收应付","只看口头说明"},
            new[]{"型号、数量、到货条件","包装颜色","只看卖家承诺"},new[]{"立即当现货卖","不用管","到店入库才是现货"},new[]{"不检查直接买","核查真伪和保修","加倍买"},new[]{"需求、库存与现金","仅看宣传","越多越好"},
            new[]{"先反驳","立刻降价","了解具体诉求"},new[]{"隐瞒","及时说明并协商","不回复"},new[]{"确认用途和预算","猜一个","直接发最贵的"},new[]{"什么都答应","先收钱再说","只承诺能履约的条件"}};
        private static readonly int[] LifeCorrect={1,0,2,1,0,2,1,0,2,1,0,2};
        public string LifeLessonQuestion=>_lifeCourse==null?"":LifeQuestions[LifeLessonIndex];
        public string[] LifeLessonAnswers=>_lifeCourse==null?Array.Empty<string>():LifeAnswers[LifeLessonIndex].ToArray();
        public bool AnswerLifeLesson(int choice)
        {
            var t=_lifeTasks.FirstOrDefault(t=>t.Kind=="lesson"&&t.Activity==_lifeCourse?.Activity&&!t.Done);var a=_cityActivities.FirstOrDefault(a=>a.Id==t?.Activity);
            if(t==null||t.Mode>=0||choice<0||choice>2||a?.State!=CityActivityPhase.Running||!LifeHere(LifeServicePlace)||Clock.Now.TotalMinutes-a.ActualStart<30)return false;
            t.Mode=choice;return true;
        }
        private bool LifeActivityReady(CityActivity a)=>a.DefinitionId!="social_119"||_lifeTasks.Any(t=>t.Activity==a.Id&&t.Kind=="lesson"&&t.Mode>=0);
        private void CompleteLifeActivity(CityActivity a)
        {
            var t=_lifeTasks.FirstOrDefault(t=>t.Activity==a.Id&&!t.Done);if(t==null)return;t.Done=true;
            if(t.Kind=="dog-rescue"&&_lifeDog?.Stage=="stray")_lifeDog.Stage="rescued";
            if(t.Kind=="music"){Player.Mood=Math.Min(100,Player.Mood+5);if(t.Count==2)AddRelation(t.Person,2);}
            if(t.Kind=="neighbor"&&t.Mode==1)Flags.Add("life:quiet:"+t.Place);
            if(t.Kind=="friend")_lifeContact[t.Person]=Clock.Now.TotalMinutes;
            if(t.Kind=="lesson"&&_lifeCourse?.Activity==a.Id)
            {
                int correct=LifeCorrect[LifeLessonIndex];_lifeCourse.Lessons++;if(t.Mode==correct)_lifeCourse.Score++;
                int reward=_lifeCourse.Score/2;if(reward>_lifeCourse.Granted){Player.Intelligence=Math.Min(100,Player.Intelligence+reward-_lifeCourse.Granted);_lifeCourse.Granted=reward;}
                if(_lifeCourse.Lessons>=4)Flags.Add("life:course-completed:"+_lifeCourse.Course);
                _lifeCourse.Activity=null;Say("本节课完成，已上"+_lifeCourse.Lessons+"/4节。请约下一节。");
            }
        }
        internal void RecordLifeNoise(string place)
        {
            if(Clock.MinuteOfDay>=420&&Clock.MinuteOfDay<1320||!City.HasPlace(place))return;
            var site=City.GetPlace(place);InitializeCityPeople();
            var neighbor=_cityPeople.Values.Where(p=>p.PersonId!=Npcs.Landlady&&p.PersonId!=CityLifeCatalog.InvitationTestPersonId&&City.HasPlace(p.HomePlaceId)&&City.GetPlace(p.HomePlaceId).StreetId==site.StreetId)
                .OrderBy(p=>DistanceCity(StandOf(City.GetPlace(p.HomePlaceId)),StandOf(site))).FirstOrDefault();
            if(neighbor==null)return;_noisePlace=place;_noisePerson=neighbor.PersonId;_noiseAt=Clock.Now.TotalMinutes;_noiseHandled=false;
        }
        private bool LifeQuietBlock(string place)=>Flags.Contains("life:quiet:"+place)&&(Clock.MinuteOfDay<420||Clock.MinuteOfDay>=1320);
        private void TickLifeBatch(long now)
        {
            foreach(var t in _lifeTasks.Where(t=>!t.Done).ToArray())
            {
                if(t.Activity!=null){var a=_cityActivities.FirstOrDefault(a=>a.Id==t.Activity);if(a==null||!a.Active){t.Done=true;if(t.Kind=="lesson"&&_lifeCourse?.Activity==t.Activity)_lifeCourse.Activity=null;}continue;}
                if(t.Kind=="book-feedback"&&now>=t.At)
                {
                    _lifeBudget+=t.Count;int bought=0;bool changed=true;while(changed){changed=false;for(int b=0;b<3;b++)if(_lifeBudget>=LifeEventGoods.Books[b].Price.Yuan){_lifeBudget-=(int)LifeEventGoods.Books[b].Price.Yuan;_lifeBookStock[b]++;bought++;changed=true;}}
                    t.Done=true;Say("社区已用募书款补充"+bought+"本真实可领阅图书，余款留作下次购书。");continue;
                }
                int delta=(int)Math.Max(0,Math.Min(1,now-t.LastMinute));t.LastMinute=now;
                if(!t.Started||IsWorking||CityActivityExclusive||_sleeping||t.Kind=="chat"&&!HasSmartphone||t.Place!=null&&(!LifeHere(t.Place)||!LifeOpen(t.Place)))continue;
                if(t.Kind=="donation"&&!CitySamePlace("life_teacher"))continue;
                if(t.Kind=="read"&&Backpack.GiftCount(t.Item)<1)continue;
                if(t.Kind=="neighbor"&&!CitySamePlace(t.Person))
                {t.Message=QueueLifeVisit("neighbor","social_113",t.Person,t.Place);t.Done=true;continue;}
                if(t.Kind=="dog-walk")
                {if(CurrentPlace!=null||CurrentVehicle.Kind!=VehicleKind.Walk){t.X=PlayerPosition.X;t.Y=PlayerPosition.Y;continue;}t.Distance+=DistanceCity(new MapPoint(t.X,t.Y),PlayerPosition);t.X=PlayerPosition.X;t.Y=PlayerPosition.Y;}
                t.Remaining=Math.Max(0,t.Remaining-delta);if(t.Remaining>0||t.Kind=="dog-walk"&&t.Distance<20)continue;t.Done=true;
                switch(t.Kind)
                {
                    case "donation":var receipt=NewLifeTask("book-feedback",LifeServicePlace,0);receipt.Count=t.Mode;receipt.At=now+3*1440L;Say("捐款登记完成，三天后可查看购书反馈。");break;
                    case "disinfect":_fluProtectionUntil=now+7*1440L;Say("到店消毒完成，防护7天，仍有患病风险。");break;
                    case "rest":Player.Energy=Math.Min(Player.MaxEnergy,Player.Energy+5);Player.Health=Math.Min(100,Player.Health+2);_fluIllUntil=Math.Min(_fluIllUntil,now+1440);break;
                    case "read":if(_lifeRead.Add(t.Item))Player.Intelligence=Math.Min(100,Player.Intelligence+1);else Player.Mood=Math.Min(100,Player.Mood+3);Say("读完了这本书，本书首次学习收益只结算一次。");break;
                    case "chat":AddRelation(t.Person,3);_lifeContact[t.Person]=now;break;
                    case "adopt":_lifeDog.Stage="adopted";_lifeDog.Place=Home.Id;_lifeDog.LastDay=Clock.Today.TotalDays;Backpack.AddGift(LifeEventGoods.DogFood.Id,3);ApplyCargoLimit();break;
                    case "dog-walk":_lifeDog.WalkDay=Clock.Today.TotalDays;_lifeDog.Bond=Math.Min(100,_lifeDog.Bond+3);break;
                    case "gym":if(t.Mode==3){Player.Energy=Math.Min(Player.MaxEnergy,Player.Energy+3);break;} _gymRounds++;Player.SpendEnergy(t.Mode==1?6:3);Player.Health=Math.Min(100,Player.Health+(t.Mode==1?2:1));if(_gymRounds>=3)_gymReady=false;break;
                    case "neighbor":if(t.Mode==1)Flags.Add("life:quiet:"+t.Place);AddRelation(t.Person,1);break;
                }
            }
            int day=Clock.Today.TotalDays;
            if(_lifeDog!=null&&_lifeDog.Stage=="adopted"&&day>_lifeDog.LastDay){_lifeDog.Hunger=Math.Max(0,_lifeDog.Hunger-30*(day-_lifeDog.LastDay));_lifeDog.LastDay=day;if(_lifeDog.Hunger<20)_lifeDog.Bond=Math.Max(0,_lifeDog.Bond-1);}
            if(day!=_fluDay){_fluDay=day;_fluExposure=0;}
            if(now<_fluSeasonUntil&&now>=_fluIllUntil&&_fluExposure<30&&ShopAt(CurrentPlace)?.Sim.ActiveCustomers.Count>0)
            {if(++_fluExposure==30&&Random.Chance(now < _fluProtectionUntil ? .05 : .25)){_fluIllUntil=now+3*1440L;Player.Health=Math.Max(0,Player.Health-8);Player.SpendEnergy(5);Say("你出现流感症状，可实际看诊、吃药或休息。");}}
            TickLifeFair(now);
        }
        private void SaveLifeBatch(SaveData d)
        {
            d.lifeEventTasks=_lifeTasks.Select(t=>t.Copy()).ToArray();d.lifeEventDog=_lifeDog?.Copy();d.lifeEventCourse=_lifeCourse?.Copy();d.lifeEventStall=_lifeStall?.Copy();
            d.lifeEventStock=LifeBookStock;d.lifeEventRead=_lifeRead.ToArray();d.lifeContactIds=_lifeContact.Keys.ToArray();d.lifeContactTimes=d.lifeContactIds.Select(id=>_lifeContact[id]).ToArray();
            d.lifeEventBudget=_lifeBudget;d.lifeEventSequence=_lifeSequence;d.lifeFluDay=_fluDay;d.lifeFluExposure=_fluExposure;d.lifeFluSeason=_fluSeasonUntil;d.lifeFluProtection=_fluProtectionUntil;d.lifeFluIll=_fluIllUntil;
            d.lifeNoisePlace=_noisePlace;d.lifeNoisePerson=_noisePerson;d.lifeNoiseAt=_noiseAt;d.lifeNoiseHandled=_noiseHandled;d.lifeGymReady=_gymReady;d.lifeGymRounds=_gymRounds;
        }
        private void RestoreLifeBatch(SaveData d)
        {
            _lifeTasks.Clear();_lifeTasks.AddRange((d.lifeEventTasks??Array.Empty<LifeEventTask>()).Select(t=>t.Copy()));
            foreach(var t in _lifeTasks){if(string.IsNullOrEmpty(t.Activity))t.Activity=null;if(string.IsNullOrEmpty(t.Place))t.Place=null;}
            _lifeDog=string.IsNullOrEmpty(d.lifeEventDog?.Stage)?null:d.lifeEventDog.Copy();_lifeCourse=d.lifeEventCourse?.Paid==true?d.lifeEventCourse.Copy():null;
            if(_lifeCourse!=null&&string.IsNullOrEmpty(_lifeCourse.Activity))_lifeCourse.Activity=null;
            _lifeStall=string.IsNullOrEmpty(d.lifeEventStall?.Item)?null:d.lifeEventStall.Copy();
            _lifeBookStock=d.lifeEventStock?.Length==3?(int[])d.lifeEventStock.Clone():new[]{2,2,2};_lifeRead.Clear();foreach(var id in d.lifeEventRead??Array.Empty<string>())_lifeRead.Add(id);
            _lifeContact.Clear();for(int i=0;i<Math.Min(d.lifeContactIds?.Length??0,d.lifeContactTimes?.Length??0);i++)_lifeContact[d.lifeContactIds[i]]=d.lifeContactTimes[i];
            _lifeBudget=d.lifeEventBudget;_lifeSequence=d.lifeEventSequence;_fluDay=d.lifeFluDay;_fluExposure=d.lifeFluExposure;_fluSeasonUntil=d.lifeFluSeason;_fluProtectionUntil=d.lifeFluProtection;_fluIllUntil=d.lifeFluIll;
            _noisePlace=d.lifeNoisePlace;_noisePerson=d.lifeNoisePerson;_noiseAt=d.lifeNoiseAt;_noiseHandled=d.lifeNoisePlace==null||d.lifeNoiseHandled;_gymReady=d.lifeGymReady;_gymRounds=d.lifeGymRounds;
            RestoreLifeFairBuyer();
        }
    }
    public sealed partial class SaveData
    {
        public LifeEventTask[] lifeEventTasks;public LifeDogState lifeEventDog;public LifeCourseState lifeEventCourse;public LifeStallState lifeEventStall;
        public int[] lifeEventStock;public string[] lifeEventRead,lifeContactIds;public long[] lifeContactTimes;
        public int lifeEventBudget,lifeEventSequence,lifeFluDay,lifeFluExposure,lifeGymRounds;public long lifeFluSeason,lifeFluProtection,lifeFluIll,lifeNoiseAt;
        public string lifeNoisePlace,lifeNoisePerson;public bool lifeNoiseHandled,lifeGymReady;
    }
}
