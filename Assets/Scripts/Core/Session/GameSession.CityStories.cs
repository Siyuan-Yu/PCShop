using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class CityPromotion
    {
        public string Id, CreatorId, ShopId, ProductId, Mode, Audience, ActivityId, StoryId, State;
        public long Start, End, FeeCents, SalesCents;
        public int StartLedgerIndex, SalesCount, MaxConversions = 24;
        public int LastLedgerCount = -1;
        public double MaxTrafficBoost = .25;
        public bool Paid;
        public CityPromotion Copy() => (CityPromotion)MemberwiseClone();
    }
    [Serializable]
    public sealed class CityReferralOffer
    {
        public string Id, Client, ContractId;
        public int Count;
        public bool Discount;
        public CityReferralOffer Copy() => (CityReferralOffer)MemberwiseClone();
    }
    public sealed partial class CityStoryProgress
    {
        public string ActivityId, ShopId, CampaignId, NextHint;
    }
    public sealed partial class SaveData
    {
        public CityPromotion[] cityPromotions;
        public string[] cityStoryEffects;
        public int cityNextPromotion;
        public CityReferralOffer[] cityReferrals;
    }
    public sealed partial class GameSession
    {
        private readonly List<CityStoryProgress> _cityStories = new List<CityStoryProgress>
        {
            new CityStoryProgress { Id="supply", PersonId=Npcs.Li },
            new CityStoryProgress { Id="creator", PersonId="life_ming" },
            new CityStoryProgress { Id="property", PersonId=Rosters.AgentChen.Id },
            new CityStoryProgress { Id="romance", PersonId="life_creator" }
        };
        private readonly List<CityPromotion> _cityPromotions = new List<CityPromotion>();
        private readonly HashSet<string> _cityStoryEffects = new HashSet<string>();
        private readonly List<CityReferralOffer> _cityReferrals = new List<CityReferralOffer>();
        private int _nextCityPromotion = 1;
        public IReadOnlyList<CityStoryProgress> CityStories => _cityStories;
        public IReadOnlyList<CityPromotion> CityPromotions => _cityPromotions;
        public IReadOnlyList<CityReferralOffer> CityReferrals => _cityReferrals;
        public CityStoryProgress CityStory(string id) => _cityStories.FirstOrDefault(s => s.Id == id);
        public static string CityStoryTitle(string id) => id=="supply"?"一张靠谱的交货单":id=="creator"?"工具箱与原创作品":id=="property"?"老铺子的下一位房东":"陆栀：作品以外的生活";
        public static string CityStoryStageName(string id,int stage)
        {
            string[] names=id=="supply"?new[]{"认识供应商","真实饭局引荐","确认小规模试单","实际交付两台电脑","下一届电子展合作","长期供货机会"}
                :id=="creator"?new[]{"了解员工的兴趣","听一段生活经历","由朋友引荐创作者","一起到原创漫展","签订真实推广","用库存与服务兑现推广","后续口碑合作"}
                :id=="property"?new[]{"认识中介","走访文创街租户","了解续租与生活","通过正常交易购买物业","与租户和创作者再见面","邻里合作"}
                :new[]{"当面认识成年创作者","了解工作之外的生活","实际一起赴约","明确朋友或感情选择","关系中的日常"};
            return names[Math.Max(0,Math.Min(names.Length-1,stage))];
        }

        public string CityStoryObjective(string id)
        {
            var p=CityStory(id);if(p==null)return "故事记录不存在";
            if(id=="supply")return p.Stage==0?"到电子市场与李老板聊工作/共同朋友，熟悉到8；普通采购不受这条故事限制。"
                :p.Stage==1?"约李老板和企业联系人沈远到同桌家常馆，实际完成饭局后才形成引荐。"
                :p.Stage==2?"与沈远确认2台入门电脑试单；按现有成本报价、5%保证金和真实装机交付执行。"
                :p.Stage==3?(p.Failed?"试单未全数履约，可补救后再试；普通店铺与其他客户不受锁定。":"采购、预留材料并完成试单；等待交付结果，不能点按钮领收益。")
                :p.Stage==4?"与沈远参加本月或下一届电子展，实际到场交流后解锁复订合作。":"已建立履约记录，可接受新的小批量订单；仍需正常采购和交付。";
            if(id=="creator")return p.Stage==0?"与许明、顾宁或乔禾当面聊兴趣。可在应聘前认识，也可入职后继续。"
                :p.Stage==1?"听对方讲攒维修工具、夜校或搬家后的生活，再聊共同朋友。"
                :p.Stage==2?"由这位朋友引荐陆栀。她是27岁的原创模型创作者，与文创街袁岚及沈远也相识。"
                :p.Stage==3?"约陆栀与这位朋友参加原创漫展；完成实际游览与交流，不在接受邀约时结算。"
                :p.Stage==4?"选择一家有货、能服务顾客的门店，签5天整店线上推广；不增加私人好感。"
                :p.Stage==5?(p.Failed?"本期实际成交不足，可准备好货架、库存和当班服务后再投放。":"推广仅提高目标店真实询价客流；需要3笔正常成交建立后续口碑，不直接发钱。")
                :"创作者与员工的朋友关系保留，可继续合作；不要求发展恋爱。";
            if(id=="property")return p.Stage==0?"到中介当面聊工作/街坊，先了解具体商铺的租买区别。"
                :p.Stage==1?"实际走访文创街的袁岚，听她讲老铺与续租生活。"
                :p.Stage==2?"回中介核实这处具体物业的产权、租约和资金条件；不免费获得房产。"
                :p.Stage==3?"通过中介正常购买已了解的物业。原租户、货物与人物关系保留，钱不够可以以后再来。"
                :p.Stage==4?"约袁岚与陆栀实际吃顿饭，谈租户合作；产权本身不代替邻里信任。":"物业与租户仍按正常规则运作，邻里口碑会带来有限的真实询价。";
            return p.Stage==0?"当面认识陆栀，聊她的兴趣；商业费用不会增加这一条个人进展。"
                :p.Stage==1?"当面听她讲一段个人经历，尊重她把作品与私人生活分开的想法。"
                :p.Stage==2?"约她去河岸钓鱼/散步，真实到场并共同度过活动时间。"
                :p.Stage==3?"下次当面明确选择：继续朋友合作，或互相确认恋爱关系。"
                :p.Branch=="romance"?"你们已确认关系。日常交流会提到相处，商业合作仍按独立条款。":"你们选择继续朋友与合作，普通经营和人物故事继续推进。";
        }

        private void CityStoryRemember(string person,string text) { if(CityPerson(person)!=null) RememberSocial(person,text); }
        private void IntroduceCityPerson(string from,string target)
        {
            Flags.Add("heard:" + target);
            if (Relation(target) < 3) AddRelation(target, 3 - Relation(target));
            var p=CityPersonState(from);if(p!=null)p.KnownRelations=(p.KnownRelations??Array.Empty<string>()).Union(new[]{target}).ToArray();
            CityStoryRemember(target,"经"+(CityPerson(from)?.Name??"朋友")+"引荐留下联系；尚未代替实际见面与赴约。");
        }
        private void OnCityStoryTalk(string id,string topic)
        {
            if(!CitySamePlace(id))return;
            var supply=CityStory("supply");
            if(supply.Stage==0&&(id==Npcs.Li||id==Npcs.Zhou)&&(topic=="work"||topic=="friends")&&Relation(id)>=8)
            {supply.PersonId=id;supply.Stage=1;supply.StartedMinute=Clock.Now.TotalMinutes;CityStoryRemember(id,"聊起小团队也需要靠谱的电脑供货方；答应在真实饭局上引荐。");}
            var creator=CityStory("creator");
            bool employeeFriend=id=="life_ming"||id=="life_ning"||id=="life_qiao";
            if(creator.Stage==0&&employeeFriend&&topic=="interest")
            {creator.PersonId=id;creator.Stage=1;creator.StartedMinute=Clock.Now.TotalMinutes;CityStoryRemember(id,id=="life_ming"?"许明说想先攒齐一套维修工具，空闲时给原创模型修小机关。":id=="life_ning"?"顾宁讲起下班后读夜校，不愿让每周只剩采购和排班。":"乔禾搬家后慢慢认识邻居，也想把熟悉路线的本领用在稳定工作上。");}
            else if(creator.Stage==1&&id==creator.PersonId&&(topic=="life"||topic=="work"))
            {creator.Stage=2;creator.Branch="heard-personal-life";CityStoryRemember(id,"你认真听完对方的生活目标，没有把这次交谈只当成索取资源。");}
            else if(creator.Stage==2&&id==creator.PersonId&&topic=="friends")
            {IntroduceCityPerson(id,"life_creator");creator.Stage=3;}
            var property=CityStory("property");
            if(property.Stage==0&&id==Rosters.AgentChen.Id&&(topic=="work"||topic=="friends"))
            {property.Stage=1;property.StartedMinute=Clock.Now.TotalMinutes;IntroduceCityPerson(id,"life_tenant");}
            else if(property.Stage==1&&id=="life_tenant"&&(topic=="life"||topic=="work"))
            {
                string place=CityPersonState(id)?.WorkPlaceId;
                if(place!=null&&City.HasPlace(place)){property.PropertyId=place;property.Stage=2;CityStoryRemember(id,"袁岚讲起老铺子的续租计划，希望下一位房东保留这家生意和街坊来往。");}
            }
            else if(property.Stage==2&&id==Rosters.AgentChen.Id&&(topic=="work"||topic=="friends"))
            {property.Stage=3;CityStoryRemember(id,"核实了袁岚店面的产权与带租约交易条件；购买仍按中介报价实际支付。");}
            var romance=CityStory("romance");
            if(id=="life_creator"&&romance.Stage==0&&(topic=="interest"||topic=="life"))
            {romance.Stage=1;romance.StartedMinute=Clock.Now.TotalMinutes;CityStoryRemember(id,"陆栀希望作品可以公开讨论，私人相处则由彼此决定，不拿商业费用衡量。");}
            if(id=="life_creator"&&romance.Stage>=4)Say(CityPersonalFeedback(id));
        }

        public string CityPersonalFeedback(string personId)
        {
            var p=CityStory("romance");if(personId!="life_creator"||p.Stage<4)return null;
            return p.Branch=="romance"?"陆栀：今天也想留一点时间给彼此。合作的事照合同来，散步时可以只聊生活。":"陆栀：做朋友也很好，作品和生意之外，我们仍然可以聊日常。";
        }
        public string HearCityPersonalStory(string personId)
        {
            string text=CityHearStory(personId);
            var romance=CityStory("romance");
            if(personId=="life_creator"&&romance.Stage==1&&CitySamePlace(personId)&&CityPersonState(personId)?.StoryChapter>0)romance.Stage=2;
            return text;
        }
        public string CityStoryMeetingPlace(string personId)
        {
            var person=CityPerson(personId);var state=CityPersonState(personId);if(person==null||state==null)return null;
            var employment=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==personId);
            string work=employment?.Shop?.Place.Id??state.WorkPlaceId;
            if(work!=null&&City.HasPlace(work))return work;
            if((state.KnownTopics??Array.Empty<string>()).Contains("life")&&City.HasPlace(state.HomePlaceId))return state.HomePlaceId;
            // Recruitment/public leisure are findable; unknown private real-time coordinates are not exposed.
            return person.Recruitable&&HasShop?MainShop.Place.Id:City.HasPlace("life_park")?"life_park":null;
        }

        public long NextCityStoryExpo(bool comic,string personId)
        {
            long candidate=SuggestCityActivityStart(comic?"expo_comic":"expo_electronics",personId);
            for(int days=0;days<65;days++)
            {
                int date=(int)(candidate/1440)+days, dom=date%30+1;
                if(comic?(dom<22||dom>24):(dom<10||dom>12))continue;
                long start=(long)date*1440+13*60;
                if(start<Clock.Now.TotalMinutes+CityTravelMinutes(City.GetPlace("life_expo"))+15)continue;
                return start;
            }
            return candidate;
        }
        public CityActivity ScheduleCityStoryActivity(string storyId,bool goNow=false,bool approveEmployee=false)
        {
            var story=CityStory(storyId);if(story==null)return null;
            string def;string[] people;
            if(storyId=="supply"&&story.Stage==1){def="dinner";people=new[]{story.PersonId,"life_enterprise"};}
            else if(storyId=="supply"&&story.Stage==4){def="expo_electronics";people=new[]{"life_enterprise"};}
            else if(storyId=="creator"&&story.Stage==3){def="expo_comic";people=new[]{story.PersonId,"life_creator"};}
            else if(storyId=="property"&&story.Stage==4){def="dinner";people=new[]{"life_tenant","life_creator"};}
            else if(storyId=="romance"&&story.Stage==2){def="fishing";people=new[]{"life_creator"};}
            else {Fail("这一步需要先完成当前实际目标。");return null;}
            var active=CityActivities.FirstOrDefault(a=>a.Id==story.ActivityId&&a.Active);if(active!=null){if(goNow)GoToCityActivity(active.Id);return active;}
            long start=def.StartsWith("expo_")?NextCityStoryExpo(def=="expo_comic",people.Last()):people.Max(p=>SuggestCityActivityStart(def,p));
            string source="city-story:"+story.Id+":"+story.Stage+":"+story.Attempt;
            var old=CityActivities.FirstOrDefault(a=>a.SourceId==source);
            if(old!=null&&!old.Active&&old.State!=CityActivityPhase.Completed){story.Attempt++;source="city-story:"+story.Id+":"+story.Stage+":"+story.Attempt;}
            // Find a compatible slot without deleting any existing appointment. Business-expo staff
            // attendance is validated by the same activity service as ordinary invitations.
            string blocker=CityActivityBlocker(def,people,start,approveEmployee);
            if(blocker!=null)
            {
                long first=Math.Max(Clock.Now.TotalMinutes+5,start);
                for(int shift=0;shift<=35*1440;shift+=5)
                {
                    long candidate=first+shift;
                    if(CityActivityBlocker(def,people,candidate,approveEmployee)==null){start=candidate;break;}
                }
            }

            var a=ScheduleCityActivity(def,people,start,approveEmployee,source);
            if(a==null)return null;story.ActivityId=a.Id;if(goNow)GoToCityActivity(a.Id);return a;
        }

        public string CitySupplyTrialBlocker()
        {
            var story=CityStory("supply");
            if(story.Stage!=2&&!(story.Stage==3&&story.Failed)&&story.Stage<5)return "先完成供应商引荐饭局";
            if(!CitySamePlace("life_enterprise"))return "需要与沈远实际见面确认试单";
            if(!HasShop||!HasSmartphone)return "需要正常经营门店和智能手机";
            if(PlayerShop.Reputation<8)return "店铺知名度达到8，可先继续普通经营";
            if(_contracts.Any(c=>c.Accepted&&!c.Settled))return "先完成或处理已有企业订单";
            var shop=MyShops.FirstOrDefault(s=>s.RetailKind=="computer");if(shop==null)return "需要一家电脑店承担装机交付";
            Money total=shop.Sim.QuoteBuild(0)*2;
            return Ledger.Balance<total.Scale(.05)?"需支付订单金额5%的履约保证金":null;
        }
        public bool AcceptCitySupplyTrial()
        {
            string reason=CitySupplyTrialBlocker();if(reason!=null)return Fail(reason);
            var story=CityStory("supply");var shop=MyShops.First(s=>s.RetailKind=="computer");
            string id="city-enterprise:"+(story.Attempt++);
            var contract=new EnterpriseContract {Id=id,Client="沈远 · 云帆两台试单",Tier=0,Count=2,UnitCents=shop.Sim.QuoteBuild(0).Cents,OfferUntilDay=Clock.Today.TotalDays+2};
            _contracts.Add(contract);bool accepted=false;WithShop(shop,()=>accepted=AcceptEnterprise(id));
            if(!accepted){_contracts.Remove(contract);return false;}
            bool repeat=story.Rewarded&&story.Stage>=5;
            story.OrderSource=id;story.ShopId=shop.Place.Id;story.Failed=false;story.Stage=repeat?5:3;
            CityStoryRemember("life_enterprise","确认2台入门电脑试单，材料、装机、服务与实际交付都按正常经营规则。");return true;
        }

        private void OnCityActivityChoice(CityActivity activity,int choice)
        {
            if(activity==null||activity.State!=CityActivityPhase.Running)return;
            if(activity.DefinitionId.StartsWith("expo_")&&choice==2)
            {
                string key="expo-contact:"+activity.DefinitionId+":"+(int)(activity.ScheduledStart/(30L*1440));
                if(_cityStoryEffects.Add(key))
                {
                    string person=activity.DefinitionId=="expo_comic"?"life_creator":"life_enterprise";
                    IntroduceCityPerson("life_curator",person);
                    Say("在本届展会实际交流并留下联系；复入展馆不会重复授予同一认识事件。");
                }
            }
        }
        private void OnCityActivityCompleted(CityActivity activity)
        {
            if(activity==null||activity.State!=CityActivityPhase.Completed||!activity.RewardApplied)return;
            if(!_cityStoryEffects.Add("completed:"+activity.Id))return;
            foreach(var story in _cityStories.Where(s=>s.ActivityId==activity.Id))
            {
                if(story.Id=="supply"&&story.Stage==1){IntroduceCityPerson(story.PersonId,"life_enterprise");story.Stage=2;}
                else if(story.Id=="supply"&&story.Stage==4){story.Stage=5;story.Rewarded=true;Flags.Add("city:trusted-supply");Online.Discover("s_zhou");CityStoryRemember("life_enterprise","完成电子展合作交流，愿意继续以实际订单小批量复订。");}
                else if(story.Id=="creator"&&story.Stage==3){story.Stage=4;IntroduceCityPerson("life_creator","life_tenant");CityStoryRemember("life_creator","在原创漫展认真交流了作品与推广边界，后续商务报价独立确认。");}
                else if(story.Id=="property"&&story.Stage==4)
                {
                    story.Stage=5;story.Rewarded=true;Flags.Add("city:tenant-cooperation");
                    var shop=MyShops.FirstOrDefault();if(shop!=null)AddCityPromotion("life_tenant",shop.Place.Id,"neighborhood",Money.Zero,3*1440, .10, null,"property");
                    CityStoryRemember("life_tenant","一次真实饭局后，愿意向街坊介绍这位遵守带租约约定的新房东；原店与邻里生活继续。");
                }
                else if(story.Id=="romance"&&story.Stage==2){story.Stage=3;CityStoryRemember("life_creator","在河岸一起度过了真实的一段时间；可以选择继续朋友，也可以当面明确个人关系。");}
            }
            if(activity.SourceId!=null&&activity.SourceId.StartsWith("legacy-creator-private:"))
            {
                var person=activity.Participants.FirstOrDefault(p=>Creator(p.PersonId)!=null);var creator=person==null?null:Creator(person.PersonId);
                if(creator!=null){creator.LastOutingDay=Clock.Today.TotalDays;creator.Affection=Math.Min(100,creator.Affection+8+Math.Min(4,EffectiveCharm/10));creator.Relationship=Math.Min(100,creator.Relationship+2);}
            }
            if(activity.SourceId!=null&&activity.SourceId.StartsWith("legacy-friend-meal:"))
            {Player.Charm=Math.Min(100,Player.Charm+1);Player.Intelligence=Math.Min(100,Player.Intelligence+1);}
            if(activity.SourceId!=null&&activity.SourceId.StartsWith("legacy-regulars-meal:"))PlayerShop.Reputation+=3;
            if(activity.SourceId!=null&&activity.SourceId.StartsWith("legacy-lin-meal:"))Player.Health=Math.Min(100,Player.Health+2);
        }

        public bool ChooseCityRomance(bool romantic)
        {
            var progress=CityStory("romance");
            if(progress.Stage!=3)return Fail("先当面了解经历并完成实际相处，再决定个人关系。");
            if(!CitySamePlace("life_creator"))return Fail("需要当面表达这个选择。");
            if(romantic&&RomanceId!=null&&RomanceId!="life_creator")return Fail("已有个人关系，请先认真处理原关系。");
            progress.Stage=4;progress.Branch=romantic?"romance":"friends";
            if(romantic)RomanceId="life_creator";
            CityStoryRemember("life_creator",romantic?"彼此明确确认成年人的恋爱关系，商业合作仍独立履约。":"明确选择继续朋友与合作，普通经营与今后的相处都不受惩罚。");
            Say(CityPersonalFeedback("life_creator"));return true;
        }

        public double CityCampaignTraffic(OwnedShop shop)
        {
            if(shop==null)return 1;
            long now=Clock.Now.TotalMinutes;
            return 1+Math.Min(.50,_cityPromotions.Where(c=>c.ShopId==shop.Place.Id&&c.State=="Running"&&c.Start<=now&&now<c.End&&c.SalesCount<c.MaxConversions).Sum(c=>c.MaxTrafficBoost));
        }
        private CityPromotion AddCityPromotion(string creator,string shop,string mode,Money fee,int duration,double boost,string activity,string story)
        {
            string id="campaign:"+_nextCityPromotion++;
            var campaign=new CityPromotion {Id=id,CreatorId=creator,ShopId=shop,ProductId=null,Mode=mode,Audience=ShopAt(shop)?.RetailKind=="models"?"原创模型/本地兴趣":"本地生活/门店现货",
                FeeCents=fee.Cents,Start=Clock.Now.TotalMinutes,End=Clock.Now.TotalMinutes+duration,MaxTrafficBoost=boost,ActivityId=activity,StoryId=story,
                State=activity==null?"Running":"Scheduled",StartLedgerIndex=Ledger.Entries.Count};
            if(activity==null)
            {
                if(fee.IsPositive&&!Ledger.TryDebit(Clock.Today,"推广",fee,(CityPerson(creator)?.Name??creator)+"整店线上投放",LedgerAttribution.ForShop(shop,id+":fee","推广")))return null;
                campaign.Paid=true;
            }
            _cityPromotions.Add(campaign);var target=ShopAt(shop);if(target!=null)WithShop(target,ApplyUpgrades);return campaign;
        }
        public string CityPromotionBlocker(string creator,string shop)
        {
            var target=ShopAt(shop);if(target==null)return "请选择经营中的门店";
            if(!HasSmartphone)return "线上推广需要智能手机";
            if(!IsMet(creator))return "先通过实际见面或朋友引荐认识创作者";
            if(CityTrust(creator)<1)return "先完成一次实际相处/展会履约，建立基本信任";
            if(_cityPromotions.Any(c=>c.ShopId==shop&&(c.State=="Running"||c.State=="Scheduled")))return "目标店已有推广安排，不重复叠加";
            if(target.Sim.Stock.Lines().Sum(l=>l.Count)+target.Sim.Stock.GoodsTotal==0)return "目标店还没有可卖现货，请先采购上架再投放";
            return Ledger.Balance<Money.FromYuan(300)?"本次线上推广需要¥300":null;
        }
        public bool StartCityCreatorPromotion(string shopId)
        {
            var story=CityStory("creator");if(story.Stage!=4&&!(story.Stage==5&&story.Failed)&&story.Stage<6)return Fail("先完成员工引荐和实际漫展交流。");
            string blocker=CityPromotionBlocker("life_creator",shopId);if(blocker!=null)return Fail(blocker);
            var campaign=AddCityPromotion("life_creator",shopId,"online",Money.FromYuan(300),5*1440,.25,null,"creator");if(campaign==null)return false;
            story.ShopId=shopId;story.CampaignId=campaign.Id;story.CampaignEnd=campaign.End;story.Stage=5;story.Failed=false;
            Say("5天线上推广开始：仅目标店客流最多增加25%，期间最多24笔成交后停止额外流量。现货、定价、货架和实际值守仍决定能否成交；不直接发放收益。");return true;
        }

        private void TickCityStories(long now)
        {
            var romance=CityStory("romance");if(romance.Stage==1&&CityPersonState("life_creator")?.StoryChapter>0)romance.Stage=2;
            var supply=CityStory("supply");
            if(supply.Stage==3||(supply.Stage>=5&&supply.OrderSource!=null&&!_cityStoryEffects.Contains("trial:"+supply.OrderSource)))
            {
                var c=_contracts.FirstOrDefault(x=>x.Id==supply.OrderSource);
                if(c!=null&&c.Settled)
                {
                    if(c.Failed==0&&c.Completed==c.Count)
                    {supply.Stage=supply.Rewarded?5:4;supply.Failed=false;if(_cityStoryEffects.Add("trial:"+c.Id)){_cityTrust["life_enterprise"]=Math.Min(100,CityTrust("life_enterprise")+5);CityStoryRemember("life_enterprise","按真实订单收到全部电脑，履约信任提升；愿意谈下一届电子展合作。");}}
                    else supply.Failed=true;
                }
            }
            var property=CityStory("property");if(property.Stage==3&&property.PropertyId!=null&&PropertyAt(property.PropertyId)!=null)property.Stage=4;
            var refresh=new HashSet<string>();
            foreach(var promotion in _cityPromotions)
            {
                if(promotion.State=="Scheduled")
                {
                    var a=CityActivities.FirstOrDefault(x=>x.Id==promotion.ActivityId);
                    if(a==null||(!a.Active&&a.State!=CityActivityPhase.Completed)){promotion.State="Cancelled";continue;}
                    if(a.State==CityActivityPhase.Running&&a.Participants.Any(p=>p.PersonId==promotion.CreatorId&&p.Phase==CityParticipantPhase.Participating))
                    {
                        if(promotion.FeeCents>0&&!Ledger.TryDebit(Clock.Today,"推广",Money.FromCents(promotion.FeeCents),"实际到场推广",LedgerAttribution.ForShop(promotion.ShopId,promotion.Id+":fee","推广")))
                        {promotion.State="Failed";continue;}
                        promotion.Paid=true;promotion.State="Running";promotion.Start=now;promotion.End=now+1440;promotion.StartLedgerIndex=Ledger.Entries.Count;refresh.Add(promotion.ShopId);
                    }
                }
                if(promotion.State!="Running")continue;
                var legacy=Creator(promotion.CreatorId);
                if(legacy!=null&&legacy.CampaignUntilDay<=Clock.Today.TotalDays)
                {promotion.State="Completed";refresh.Add(promotion.ShopId);continue;}
                if(promotion.LastLedgerCount!=Ledger.Entries.Count)
                {
                    var sales=Ledger.Entries.Skip(Math.Max(0,promotion.StartLedgerIndex)).Where(e=>e.ShopId==promotion.ShopId&&e.Amount.IsPositive&&(e.Category=="零售"||e.Category=="装机")).ToArray();
                    int old=promotion.SalesCount;promotion.SalesCount=sales.Length;promotion.SalesCents=sales.Sum(e=>e.Amount.Cents);promotion.LastLedgerCount=Ledger.Entries.Count;
                    if(old<promotion.MaxConversions&&promotion.SalesCount>=promotion.MaxConversions)refresh.Add(promotion.ShopId);
                }
                if(now>=promotion.End){promotion.State="Completed";refresh.Add(promotion.ShopId);}
            }
            foreach(string id in refresh){var shop=ShopAt(id);if(shop!=null)WithShop(shop,ApplyUpgrades);}
            var creator=CityStory("creator");if(creator.Stage==5)
            {
                var c=_cityPromotions.FirstOrDefault(p=>p.Id==creator.CampaignId);
                if(c!=null&&c.SalesCount>=3){creator.Stage=6;creator.Rewarded=true;if(_cityStoryEffects.Add("creator-success:"+c.Id)){_cityTrust["life_creator"]=Math.Min(100,CityTrust("life_creator")+3);CityStoryRemember("life_creator","本期产生真实成交，供货与服务得到落实；愿意继续合作，私人关系保持独立。");}}
                else if(c!=null&&(c.State=="Completed"||c.State=="Failed"||c.State=="Cancelled"))creator.Failed=true;
            }
        }

        internal string ScheduleLegacyCityMeeting(string personId,string definitionId,string source)
        {
            long start=SuggestCityActivityStart(definitionId,personId);
            var previous=CityActivities.FirstOrDefault(a=>a.SourceId==source);
            if(previous!=null&&!previous.Active&&previous.State!=CityActivityPhase.Completed)source+=":retry:"+_nextCityActivity;
            var a=ScheduleCityActivity(definitionId,new[]{personId},start,false,source);
            return a==null?"预约未成立，请在日程选择可用时段；尚未消费或增加关系。":"已约在"+new GameTimestamp(start)+"到"+City.GetPlace(a.VenueId).Name+"见面。请从城市日程实际前往，到场后按活动阶段结算。";
        }
        internal string OfferCityReferralContract(string source,string client,int count,bool discount)
        {
            string id=source+":"+Clock.Today.TotalDays;
            if(!_cityReferrals.Any(o=>o.Id==id))_cityReferrals.Add(new CityReferralOffer{Id=id,Client=client,Count=Math.Max(1,Math.Min(3,count)),Discount=discount});
            return "转介绍已记入城市履历："+client+"，"+count+"台。请在城市履历核对报价和5%保证金后确认；正常采购、装机、交付后才产生收入。";
        }
        public bool AcceptCityReferral(string id)
        {
            var offer=_cityReferrals.FirstOrDefault(o=>o.Id==id);
            if(offer==null||offer.ContractId!=null)return Fail("转介绍已处理或不存在。");
            var shop=MyShops.FirstOrDefault(s=>s.RetailKind=="computer");if(shop==null)return Fail("需要一家电脑店承担订单。");
            if(_contracts.Any(c=>c.Accepted&&!c.Settled))return Fail("先处理已有企业订单，转介绍信息会保留。");
            var contract=new EnterpriseContract{Id="referral:"+offer.Id,Client=offer.Client,Count=offer.Count,Tier=0,
                UnitCents=shop.Sim.QuoteBuild(0).Scale(offer.Discount ? .95 : 1.0).Cents,OfferUntilDay=Clock.Today.TotalDays+2};
            _contracts.Add(contract);bool success=false;WithShop(shop,()=>success=AcceptEnterprise(contract.Id));
            if(!success){_contracts.Remove(contract);return false;}offer.ContractId=contract.Id;return true;
        }
        private void SaveCityStories(SaveData d)
        {
            d.cityPromotions=_cityPromotions.Select(c=>c.Copy()).ToArray();d.cityStoryEffects=_cityStoryEffects.ToArray();d.cityNextPromotion=_nextCityPromotion;
            d.cityReferrals=_cityReferrals.Select(o=>o.Copy()).ToArray();
        }
        private void LoadCityStories(SaveData d)
        {
            var defaults=_cityStories.Select(s=>s.Copy()).ToArray();_cityStories.Clear();
            foreach(var old in d.cityStories??Array.Empty<CityStoryProgress>())if(old!=null&&!string.IsNullOrEmpty(old.Id)&&!_cityStories.Any(s=>s.Id==old.Id))_cityStories.Add(old.Copy());
            foreach(var expected in defaults)if(!_cityStories.Any(s=>s.Id==expected.Id))_cityStories.Add(new CityStoryProgress{Id=expected.Id,PersonId=expected.PersonId});
            _cityPromotions.Clear();foreach(var c in d.cityPromotions??Array.Empty<CityPromotion>())if(c!=null&&!_cityPromotions.Any(p=>p.Id==c.Id))_cityPromotions.Add(c.Copy());
            _cityReferrals.Clear();foreach(var o in d.cityReferrals??Array.Empty<CityReferralOffer>())if(o!=null&&!_cityReferrals.Any(p=>p.Id==o.Id))_cityReferrals.Add(o.Copy());
            _cityStoryEffects.Clear();foreach(string key in d.cityStoryEffects??Array.Empty<string>())if(key!=null)_cityStoryEffects.Add(key);
            _nextCityPromotion=Math.Max(1,d.cityNextPromotion);
            if(d.cityPromotions==null)
                foreach(var creator in _creators.Where(c=>c.CampaignUntilDay>Clock.Today.TotalDays))
                {
                    creator.CampaignShopId=creator.CampaignShopId??MainShop.Place.Id;
                    _cityPromotions.Add(new CityPromotion {Id="legacy-campaign:"+creator.Id+":"+creator.CampaignUntilDay,CreatorId=creator.Id,
                        ShopId=creator.CampaignShopId,Mode="online-legacy",Audience="原推广受众",Start=Clock.Now.TotalMinutes,End=(long)creator.CampaignUntilDay*1440,
                        State="Running",Paid=true,StartLedgerIndex=Ledger.Entries.Count,MaxTrafficBoost=Math.Min(.35,.08+creator.Followers/200000.0)});
                }
            // Restore flags and progress only. No money, relationship, trust or elapsed-time replay here.
        }
    }
}
