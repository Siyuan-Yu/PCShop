using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    // Event workers are temporary scene participants, not additional permanent residents.
    [Serializable] public sealed class NeighborhoodWorker
    {
        public string Name, Place; public double X,Y; public CityRoute Route;
        public NeighborhoodWorker Copy()=>new NeighborhoodWorker{Name=Name,Place=Place,X=X,Y=Y,Route=Route?.Copy()};
    }
    [Serializable] public sealed class BloodDonationState
    {
        public string Stage,Message; public int Day; public long CompletedAt;
        public BloodDonationState Copy()=>(BloodDonationState)MemberwiseClone();
    }
    [Serializable] public sealed class LostChildState
    {
        public string Stage,Origin,Place,Parent,CluePlace,Message; public bool Asked;
        public NeighborhoodWorker Helper;
        public LostChildState Copy(){var x=(LostChildState)MemberwiseClone();x.Helper=Helper?.Copy();return x;}
    }
    [Serializable] public sealed class RoofLeakState
    {
        public string Shop,Kind,Message; public int SignedDay,Water; public bool Bucket,Fixed;
        public long LastWet; public NeighborhoodWorker Worker;
        public RoofLeakState Copy(){var x=(RoofLeakState)MemberwiseClone();x.Worker=Worker?.Copy();return x;}
    }
    [Serializable] public sealed class LanternFestivalState
    {
        public bool Active; public int Version,FirstDay,Actions,RiddleMask,PendingRiddle=-1;
        public int[] Hung={-1,-1,-1}; public string Message;
        public LanternFestivalState Copy(){var x=(LanternFestivalState)MemberwiseClone();x.Hung=(int[])Hung.Clone();return x;}
    }
    public static class NeighborhoodGoods
    {
        public static readonly GiftDef Nutrition=new GiftDef("community:nutrition","献血营养包","实际完成献血和留观后领取，可自行食用。",80,.5,"献血车");
        public static readonly GiftDef[] Lanterns=Enumerable.Range(0,6).Select(i=>new GiftDef("community:lantern:"+i,
            (i>=3?"精制":"纸艺")+new[]{"圆月灯","莲花灯","方宫灯"}[i%3],"亲手制作的挂灯，可在花灯会现场悬挂。",i>=3?1000:300,.3,"社区花灯会")).ToArray();
        public static GiftDef Get(string id)=>id==Nutrition.Id?Nutrition:Lanterns.FirstOrDefault(x=>x.Id==id);
    }
    public sealed partial class GameSession
    {
        private BloodDonationState _blood;
        private LostChildState _child;
        private RoofLeakState _leak;
        private LanternFestivalState _festival;
        public BloodDonationState BloodDonation=>_blood?.Copy();
        public LostChildState LostChild=>_child?.Copy();
        public RoofLeakState RoofLeak=>_leak?.Copy();
        public LanternFestivalState LanternFestival=>_festival?.Copy();
        public bool IsRainingAt(long minute)=>CityWeather.IsRaining(_weatherSeed,minute);
        public bool CityIsRaining=>IsRainingAt(Clock.Now.TotalMinutes);
        public bool FestivalOpen=>_festival?.Active==true&&Clock.Today.TotalDays>=_festival.FirstDay&&Clock.Today.TotalDays<_festival.FirstDay+3&&Clock.MinuteOfDay>=1080&&Clock.MinuteOfDay<1320;
        private bool NeighborhoodVenueFits(Place place,long at,int minutes)=>place?.Id==LifeServicePlace&&_festival?.Active==true
            &&at/1440>=_festival.FirstDay&&at/1440<_festival.FirstDay+3&&at%1440>=1080&&at%1440+Math.Max(1,minutes)<=1320;
        private bool NeighborhoodIdle=>!IsWorking&&!_sleeping&&!IsGameOver&&!CityActivityExclusive;
        private bool FestivalTask(LifeEventTask t)=>t.Kind.StartsWith("lantern-")&&RunningCityActivity?.DefinitionId=="social_124";
        private bool CurrentLeakLease=>_leak!=null&&ShopAt(_leak.Shop)?.Rented==true&&ShopAt(_leak.Shop).Lease.SignedDay==_leak.SignedDay&&ShopAt(_leak.Shop).RetailKind==_leak.Kind;
        private CityPersonState ChildParentCandidate()=>_cityPeople.Values.Where(p=>p.PersonId!=CityLifeCatalog.InvitationTestPersonId&&p.Route==null
            &&string.IsNullOrEmpty(p.ActivityId)&&string.IsNullOrEmpty(p.VisitId)&&p.Task!=CityPersonTask.Working&&p.Task!=CityPersonTask.Waiting
            &&City.HasPlace(p.LocationPlaceId)&&City.GetPlace(p.LocationPlaceId).Kind!=PlaceKind.Home&&LifeAccessible(p.LocationPlaceId)
            &&p.LocationPlaceId!=LifeFairPlace&&!Employees.Any(e=>e.Person.PersonId==p.PersonId)&&!_candidates.Any(e=>e.PersonId==p.PersonId))
            .OrderBy(p=>DistanceCity(new MapPoint(p.X,p.Y),StandOf(City.GetPlace(LifeFairPlace)))).FirstOrDefault();
        internal bool CanOfferNeighborhood(int n)
        {
            InitializeCityPeople();
            return n switch {
                121=>LifeAccessible(LifeServicePlace)&&(_blood==null||_blood.Stage=="done"&&Clock.Now.TotalMinutes-_blood.CompletedAt>=90*1440L),
                122=>LifeAccessible(LifeFairPlace)&&LifeAccessible(LifeServicePlace)&&(_child==null||_child.Stage=="done")&&ChildParentCandidate()!=null,
                123=>CityIsRaining&&ShopBatchShop()!=null&&(_leak==null||_leak.Fixed||!CurrentLeakLease),
                124=>LifeAccessible(LifeServicePlace)&&(_festival==null||Clock.Today.TotalDays>=_festival.FirstDay+3),_=>false};
        }
        private EventDef BindNeighborhood(EventDef template,int n)
        {
            if(!CanOfferNeighborhood(n))return null;
            if(n==121)
            {
                _blood=new BloodDonationState{Stage="invited",Day=Clock.Today.TotalDays,Message="献血车停在社区服务站，09:00–17:00登记。"};
                return EventScene(template,"服务站门前的献血车开放登记。先检查，再献血和留观，全程约40分钟。",
                    Choose("登记检查",_=>{BeginBloodCheck();return "到服务站实际检查10分钟；身体不合格可改天再来。";}),Choose("暂不参加",_=>"没有扣属性，可在社区生活里查看献血车。"));
            }
            if(n==122)
            {
                var parent=ChildParentCandidate();_child=new LostChildState{Stage="waiting",Origin=LifeFairPlace,Place=LifeFairPlace,Parent=parent.PersonId,CluePlace=parent.LocationPlaceId,Message="集市门口有个孩子找不到家人。"};
                // Hold a specific, already-present adult at their actual public location, never teleport them.
                parent.VisitId="community-child";
                return EventScene(template,"集市门口有个孩子在等家人。可以到场询问，陪去服务站，或联系工作人员。",
                    Choose("过去询问",_=>"位置已记入社区生活，到孩子身边再询问。"),Choose("请工作人员协助",_=>{ContactChildHelper();return "工作人员将沿真实路线赶到集市；你也可以到现场陪同。";}));
            }
            if(n==123)
            {
                var shop=ShopBatchShop();_leak=new RoofLeakState{Shop=shop.Place.Id,SignedDay=shop.Lease.SignedDay,Kind=shop.RetailKind,LastWet=Clock.Now.TotalMinutes,Message="雨水从门店一角滴落，尚未修补。"};
                return EventScene(template,shop.Place.Name+"的一角开始漏水。可以到店放桶，或请师傅上门修补。",
                    Choose("请师傅 · ¥2,000",_=>RequestLeakRepair()?"已付维修费，师傅正赶来；到店后维修60分钟。":"暂时无法叫修，未扣款。"),
                    Choose("到店放桶",_=>"到漏水的门店，从社区生活选择放桶，实际操作5分钟。"),Choose("稍后处理",_=>"漏点保留，雨天会积水；不会随机销毁库存。"));
            }
            _festival=new LanternFestivalState{Version=1,Active=true,FirstDay=Clock.Today.TotalDays,Message="连续三晚18:00–22:00，社区服务站前举办花灯会。"};
            return EventScene(template,"连续三晚18:00–22:00，社区服务站前有猜灯谜、做挂灯和小摊。可独自来，也可约熟人同行。",
                Choose("去看看",_=>"花灯会已记入社区生活，现场选择玩法。"),Choose("约朋友",_=>"在花灯会页选择具体熟人和可用时段，不会默认增加房东关系。"));
        }
        public bool BeginBloodCheck()
        {
            if(_blood==null||_blood.Stage is "donating" or "observing" or "done"||HasLifeTask("blood-check"))return false;
            if(_blood.Stage=="retry"&&_blood.Day==Clock.Today.TotalDays)return Fail("今天检查未通过，请养好身体，改天再来。");
            _blood.Day=Clock.Today.TotalDays;
            NewLifeTask("blood-check",LifeServicePlace,10);return true;
        }
        public bool EatNeighborhoodNutrition()
        {
            if(!Backpack.TakeGift(NeighborhoodGoods.Nutrition.Id))return false;
            Player.Hunger=Math.Min(100,Player.Hunger+20);Player.Energy=Math.Min(Player.MaxEnergy,Player.Energy+8);ApplyCargoLimit();return true;
        }
        private bool ChildHere=>_child!=null&&_child.Stage!="done"&&(_child.Stage=="following"?true:LifeHere(_child.Place));
        public bool AskLostChild()
        {
            if(!ChildHere||!NeighborhoodIdle)return false;_child.Asked=true;
            _child.Message="孩子记得家人在"+City.GetPlace(_child.CluePlace).Name+"附近；"+CityPerson(_child.Parent)?.Name+"正在那里等候。";return true;
        }
        public bool EscortLostChild()
        {
            if(!ChildHere||!NeighborhoodIdle||_child.Helper!=null)return false;
            _child.Stage="following";_child.Message="孩子跟在身边，请陪同到社区服务站交接。";return true;
        }
        private NeighborhoodWorker NeighborhoodDispatch(string name,string target)
        {
            var from=NearbyHospital()??City.GetPlace(LifeServicePlace);var pos=StandOf(from);
            var route=PlanCityRoute(new CityPersonState{X=pos.X,Y=pos.Y,LocationPlaceId=from.Id},target,Clock.Now.TotalMinutes);
            if(route==null)return null;return new NeighborhoodWorker{Name=name,X=pos.X,Y=pos.Y,Place=from.Id,Route=route};
        }
        public bool ContactChildHelper()
        {
            if(_child==null||_child.Stage=="done"||_child.Helper!=null)return false;
            if(_child.Stage=="following"&&!LifeHere(LifeServicePlace))return Fail("请先把孩子陪同到服务站。");
            string site=_child.Stage=="following"?LifeServicePlace:_child.Place;
            var helper=NeighborhoodDispatch("社区值守员",site);if(helper==null)return false;
            _child.Place=site;_child.Stage="handoff";_child.Helper=helper;var t=NewLifeTask("child-handoff",site,15);t.Message="等待工作人员到场后实际交接15分钟。";return true;
        }
        public bool ReuniteLostChild()
        {
            if(!ChildHere||!_child.Asked||!NeighborhoodIdle||CurrentPlace==null||!CitySamePlace(_child.Parent)||HasLifeTask("child-reunion")||_child.Helper!=null)return false;
            _child.Stage="following";var t=NewLifeTask("child-reunion",CurrentPlace.Id,5);t.Person=_child.Parent;return true;
        }
        private void FinishLostChild(bool parent)
        {
            _child.Stage="done";_child.Message=parent?"家长现场确认，孩子已与家人团聚。":"工作人员已现场接手并联系家属。";
            var p=CityPersonState(_child.Parent);if(p?.VisitId=="community-child"){p.VisitId=null;p.Task=CityPersonTask.Returning;}
            if(parent)AddRelation(_child.Parent,3);if(HasShop)PlayerShop.Reputation+=parent?1:.3;Say(_child.Message);
        }
        public bool RequestLeakRepair()
        {
            if(!CurrentLeakLease||_leak.Fixed||_leak.Worker!=null)return false;
            var worker=NeighborhoodDispatch("屋面维修师傅",_leak.Shop);if(worker==null)return Fail("目前无法到达该门店，未收费。");
            if(!Ledger.TryDebit(Clock.Today,"屋顶修补",Money.FromYuan(2000),_leak.Shop))return false;
            _leak.Worker=worker;var t=NewLifeTask("leak-repair",_leak.Shop,60);t.Started=true;_leak.Message="师傅正赶来，到店后实际维修60分钟。";return true;
        }
        public bool PlaceLeakBucket()
        {
            if(!CurrentLeakLease||_leak.Fixed||_leak.Bucket||!LifeHere(_leak.Shop)||HasLifeTask("leak-bucket"))return false;
            NewLifeTask("leak-bucket",_leak.Shop,5);return true;
        }
        public double NeighborhoodShopTraffic(string shop)=>CurrentLeakLease&&_leak.Shop==shop&&!_leak.Fixed&&_leak.Water>=3?.8:1;
        public bool BeginLanternCraft(int tier,int style)
        {
            if(!FestivalOpen||!LifeHere(LifeServicePlace)||tier<0||tier>1||style<0||style>2||HasLifeTask("lantern-craft"))return false;
            var t=NewLifeTask("lantern-craft",LifeServicePlace,10,tier==0?300:1000);t.Mode=tier*3+style;return true;
        }
        public bool HangFestivalLantern(string id,int slot)
        {
            int index=Array.FindIndex(NeighborhoodGoods.Lanterns,x=>x.Id==id);
            if(!FestivalOpen||!LifeHere(LifeServicePlace)||IsWorking||_sleeping||slot<0||slot>2||_festival.Hung[slot]>=0||index<0||!Backpack.TakeGift(id))return false;
            _festival.Hung[slot]=index;_festival.Actions++;RecordFestivalParticipation();Player.Mood=Math.Min(100,Player.Mood+3);ApplyCargoLimit();_festival.Message="已将"+NeighborhoodGoods.Lanterns[index].Name+"挂到"+(slot+1)+"号位。";return true;
        }
        public static readonly string[] FestivalRiddles={"千条线，万条线，落在水里看不见。","小小船，白布篷，头也红来桨也红。","一面镜子亮晶晶，走遍天下照古今。"};
        public static readonly string[][] FestivalAnswers={new[]{"雨","柳条","头发"},new[]{"鸭子","白鹅","纸船"},new[]{"玻璃","池塘","月亮"}};
        public bool BeginFestivalRiddle(int index)
        {
            if(!FestivalOpen||!LifeHere(LifeServicePlace)||index<0||index>2||(_festival.RiddleMask&(1<<index))!=0||_festival.PendingRiddle>=0||HasLifeTask("lantern-riddle"))return false;
            var t=NewLifeTask("lantern-riddle",LifeServicePlace,5);t.Mode=index;return true;
        }
        public bool AnswerFestivalRiddle(int answer)
        {
            if(!FestivalOpen||!LifeHere(LifeServicePlace)||_festival.PendingRiddle<0||answer<0||answer>2)return false;
            int i=_festival.PendingRiddle;_festival.RiddleMask|=1<<i;_festival.PendingRiddle=-1;_festival.Actions++;
            RecordFestivalParticipation();bool correct=answer==new[]{0,1,2}[i];Player.Mood=Math.Max(0,Math.Min(100,Player.Mood+(correct?3:-1)));_festival.Message=correct?"答对了！继续看看别处的花灯吧。":"这次猜错了，谜底是"+FestivalAnswers[i][new[]{0,1,2}[i]]+"。";return true;
        }
        public bool BrowseFestivalStall()
        {
            string key="community:browse:"+(RunningCityActivity?.DefinitionId=="social_124"?RunningCityActivity.Id:_festival?.FirstDay.ToString());
            if(!FestivalOpen||!LifeHere(LifeServicePlace)||HasLifeTask("lantern-browse")||Flags.Contains(key))return false;
            var t=NewLifeTask("lantern-browse",LifeServicePlace,10);t.Item=key;return true;
        }
        public bool InviteFestivalFriend(string person)
        {
            if(_festival==null||!IsMet(person)||HasLifeTask("festival-visit")||Flags.Contains("community:festival:"+_festival.FirstDay+":"+person))return false;
            for(int d=Math.Max(Clock.Today.TotalDays,_festival.FirstDay);d<_festival.FirstDay+3;d++)
            {
                long at=Math.Max(d*1440L+1080,Clock.Now.TotalMinutes+30);if(at>d*1440L+1260)continue;
                if(CityActivityBlockerAt("social_124",new[]{person},at,false,LifeServicePlace)!=null)continue;
                var a=ScheduleCityActivityAt("social_124",new[]{person},at,false,"festival:"+_festival.FirstDay,LifeServicePlace);if(a==null)continue;
                a.ServiceConfirmed=true;a.ServiceChoice=0;var t=NewLifeTask("festival-visit",LifeServicePlace,0);t.Person=person;t.Activity=a.Id;t.Mode=_festival.Actions;Flags.Add("community:festival:"+_festival.FirstDay+":"+person);return true;
            }
            return Fail("这三晚暂无共同空闲时段，没有强制拉人到场。");
        }
        private bool NeighborhoodActivityReady(CityActivity a)=>a.DefinitionId!="social_124"||FestivalOpen&&_lifeTasks.Any(t=>!t.Done&&t.Activity==a.Id&&t.Count>0);
        private void RecordFestivalParticipation()
        {foreach(var t in _lifeTasks.Where(t=>!t.Done&&t.Kind=="festival-visit"))if(_cityActivities.Any(a=>a.Id==t.Activity&&a.State==CityActivityPhase.Running)&&CitySamePlace(t.Person)&&LifeHere(LifeServicePlace))t.Count++;}
        private bool NeighborhoodTaskReady(LifeEventTask t)
        {
            if(t.Kind.StartsWith("blood-"))return _blood!=null&&LifeHere(LifeServicePlace)&&(t.Kind!="blood-check"||Clock.MinuteOfDay>=540&&Clock.MinuteOfDay<1020);
            if(t.Kind=="child-handoff")return _child?.Stage=="handoff"&&_child.Helper?.Route==null&&_child.Helper?.Place==t.Place;
            if(t.Kind=="child-reunion")return _child?.Stage=="following"&&CitySamePlace(t.Person);
            if(t.Kind=="leak-repair")return CurrentLeakLease&&_leak.Worker?.Route==null&&_leak.Worker?.Place==t.Place;
            if(t.Kind=="leak-bucket")return CurrentLeakLease&&!_leak.Fixed;
            if(t.Kind=="lantern-craft")return t.Fee==0||FestivalOpen; // Paid work can finish at the service station after the festival.
            if(t.Kind is "lantern-riddle" or "lantern-browse")return FestivalOpen;
            return true;
        }
        private void FinishNeighborhoodTask(LifeEventTask t,long now)
        {
            switch(t.Kind)
            {
                case "blood-check":
                    if(Player.Health<70||Player.Energy<50||_fluIllUntil>now){_blood.Stage="retry";_blood.Message="检查未通过，先休息养好身体，改天可重查；没有献血扣属性。";break;}
                    _blood.Stage="donating";var donate=NewLifeTask("blood-donate",LifeServicePlace,15);donate.Started=true;_blood.Message="检查通过，正在献血15分钟。";break;
                case "blood-donate":_blood.Stage="observing";Player.Health=Math.Max(0,Player.Health-3);Player.SpendEnergy(5);var observe=NewLifeTask("blood-observe",LifeServicePlace,15);observe.Started=true;_blood.Message="献血结束，请在车旁留观15分钟后领取营养包。";break;
                case "blood-observe":_blood.Stage="done";_blood.CompletedAt=now;Backpack.AddGift(NeighborhoodGoods.Nutrition.Id,1);ApplyCargoLimit();if(HasShop)PlayerShop.Reputation+=.5;_blood.Message="留观完成，已领取一份实际营养包。";break;
                case "child-reunion":FinishLostChild(true);break;
                case "child-handoff":FinishLostChild(false);break;
                case "leak-bucket":_leak.Bucket=true;_leak.Water=0;_leak.Message="已放好接水桶，临时保持地面干燥，漏点仍需修补。";break;
                case "leak-repair":_leak.Fixed=true;_leak.Water=0;_leak.Message="师傅已完成60分钟修补，漏点与积水已清理。";break;
                case "lantern-craft":Backpack.AddGift(NeighborhoodGoods.Lanterns[t.Mode].Id,1);ApplyCargoLimit();_festival.Message="挂灯已做好并放进背包，请选择空位悬挂。";break;
                case "lantern-riddle":_festival.PendingRiddle=t.Mode;break;
                case "lantern-browse":Flags.Add(t.Item);_festival.Actions++;RecordFestivalParticipation();Player.Mood=Math.Min(100,Player.Mood+3);_festival.Message="沿小摊逛了一圈。喜欢挂灯可以自己选样式制作。";break;
            }
        }
        private static void TickNeighborhoodWorker(NeighborhoodWorker worker,long now)
        {
            if(worker?.Route==null)return;var pos=worker.Route.Position(now);worker.X=pos.X;worker.Y=pos.Y;
            if(now>=worker.Route.ArriveMinute){worker.Place=worker.Route.TargetPlaceId;worker.Route=null;}
        }
        private void TickNeighborhood(long now)
        {
            bool repairTravelling=_leak?.Worker?.Route!=null;
            TickNeighborhoodWorker(_child?.Helper,now);TickNeighborhoodWorker(_leak?.Worker,now);
            if(repairTravelling&&_leak.Worker.Route==null)
                foreach(var t in _lifeTasks.Where(t=>!t.Done&&t.Kind=="leak-repair"))t.LastMinute=now;
            if(_child?.Stage=="following")_child.Place=CurrentPlace?.Id;
            if(_leak!=null&&!_leak.Fixed)
            {
                if(!CurrentLeakLease){_leak.Fixed=true;_leak.Message="原租约或业态已变化，原维修事项结束。";foreach(var t in _lifeTasks.Where(t=>!t.Done&&(t.Kind=="leak-repair"||t.Kind=="leak-bucket")))t.Done=true;}
                else if(now-_leak.LastWet>=10){_leak.LastWet=now;_leak.Water=_leak.Bucket||!CityIsRaining?Math.Max(0,_leak.Water-1):Math.Min(6,_leak.Water+1);}
            }
            if(_festival?.Active==true&&Clock.Today.TotalDays>=_festival.FirstDay+3)
            {_festival.Active=false;_festival.Message="本次花灯会结束，未挂的成品仍在背包。";foreach(var t in _lifeTasks.Where(t=>!t.Done&&(t.Kind=="lantern-riddle"||t.Kind=="lantern-browse")))t.Done=true;}
            if(_child!=null&&_child.Stage!="done")
            {var p=CityPersonState(_child.Parent);if(p!=null&&string.IsNullOrEmpty(p.VisitId))p.VisitId="community-child";}
        }
        private void SaveNeighborhood(SaveData d){d.communityBlood=_blood?.Copy();d.communityChild=_child?.Copy();d.communityLeak=_leak?.Copy();d.communityFestival=_festival?.Copy();}
        private void RestoreNeighborhood(SaveData d)
        {
            _blood=string.IsNullOrEmpty(d.communityBlood?.Stage)?null:d.communityBlood.Copy();_child=string.IsNullOrEmpty(d.communityChild?.Stage)?null:d.communityChild.Copy();
            _leak=string.IsNullOrEmpty(d.communityLeak?.Shop)?null:d.communityLeak.Copy();
            _festival=d.communityFestival?.Version==1&&d.communityFestival.Hung?.Length==3?d.communityFestival.Copy():null;
            if(_child?.Helper!=null&&string.IsNullOrEmpty(_child.Helper.Name))_child.Helper=null;
            if(_leak?.Worker!=null&&string.IsNullOrEmpty(_leak.Worker.Name))_leak.Worker=null;
            if(_leak!=null&&string.IsNullOrEmpty(_leak.Kind))_leak.Kind=null;
            if(_child!=null&&_child.Stage!="done"){var parent=CityPersonState(_child.Parent);if(parent!=null)parent.VisitId="community-child";}
        }
    }
    public sealed partial class SaveData
    {public BloodDonationState communityBlood;public LostChildState communityChild;public RoofLeakState communityLeak;public LanternFestivalState communityFestival;}
}
