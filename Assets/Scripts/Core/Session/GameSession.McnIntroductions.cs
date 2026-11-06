using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class McnIntroduction
    {
        public string PersonId; public long Arrival, Until; public bool Finished;
        public McnIntroduction Copy()=>(McnIntroduction)MemberwiseClone();
    }
    public sealed partial class SaveData {public McnIntroduction[] mcnIntroductions;}
    public sealed partial class GameSession
    {
        public const string CommunityMcnId="life_mcn";
        public long McnIntroductionFee(string person) =>
            20000L + KolFollowers(person) / 100 * 100L + KolPopularity(person) * 100L;
        private McnIntroduction[] _mcnIntroductions=Array.Empty<McnIntroduction>();
        public McnIntroduction[] McnIntroductions=>_mcnIntroductions.Select(x=>x.Copy()).ToArray();
        public string McnFoundBlocker
        {
            get
            {
                if(IsGameOver)return "当前不能创办公司";
                if(HasMcn)return "已经成立MCN公司";
                if(City.IsLakeCity&&DistrictAccessTier<2)return "进入公园阶段后才能开办自己的MCN公司";
                if(!HasShop||PlayerShop.Reputation<25)return "需要自己的门店和知名度25";
                if(Ledger.Balance<PCShopSell.Core.Common.Money.FromYuan(50000))return "成立公司需要¥50,000";
                return null;
            }
        }
        public string KolContactBlocker(string person)
        {
            if(!BrandPeople.Contains(person)&&KolMarket.Get(person)==null)return "没有这位合作创作者";
            return City.IsLakeCity&&!IsMet(person)?"先去社区澄光传媒认识对方、交换业务名片":null;
        }
        private string McnVisitBlocker()
        {
            if(IsGameOver||IsWorking||_sleeping||CityActivityExclusive)return "请先结束当前工作或活动";
            if(DistrictAccessTier<1)return "先进入社区阶段";
            if(CurrentPlace?.Id!=CommunityMcnId)return "请到社区MCN联合接待处找公司人事办理";
            if(!IsPlaceOpen(CurrentPlace))return "MCN接待时间09:00—18:00";
            return null;
        }
        public string McnIntroductionBlocker(string person)
        {
            string why=McnVisitBlocker();if(why!=null)return why;
            if((!BrandPeople.Contains(person)&&KolMarket.Get(person)==null)||CityPersonState(person)==null)return "没有这位合作创作者";
            if(IsMet(person))return "已有联系方式，可直接通过手机联系";
            if(_mcnIntroductions.Any(x=>x.PersonId==person&&!x.Finished&&x.Until>=Clock.Now.TotalMinutes))return "介绍已安排，请等待对方到场";
            return Ledger.Balance < PCShopSell.Core.Common.Money.FromYuan(McnIntroductionFee(person)) ? "介绍费不足" : null;
        }
        public bool RequestMcnIntroduction(string person)
        {
            string why = McnIntroductionBlocker(person);
            if (why != null) return Fail(why);
            string name = CityPerson(person).Name;
            if (!Ledger.TryDebit(Clock.Today, "MCN介绍费", PCShopSell.Core.Common.Money.FromYuan(McnIntroductionFee(person)),
                BrandAgency.ForPerson(person).Name + " · " + name + "业务名片")) return Fail("介绍费不足");
            SaveMcnContact(person);
            return true;
        }

        public string McnExchangeBlocker(string person)
        {
            string why=McnVisitBlocker();if(why!=null)return why;
            var a=_mcnIntroductions.FirstOrDefault(x=>x.PersonId==person&&!x.Finished);
            if(a==null||a.Until<Clock.Now.TotalMinutes)return "本次介绍已结束，可重新预约";
            var p=CityPersonState(person);
            if(Clock.Now.TotalMinutes<a.Arrival||p?.Route!=null||p?.LocationPlaceId!=CommunityMcnId)return "对方还在路上，实际到场后交换名片";
            return null;
        }
        public bool ExchangeMcnContact(string person)
        {
            string why=McnExchangeBlocker(person);if(why!=null)return Fail(why);
            var a=_mcnIntroductions.First(x=>x.PersonId==person&&!x.Finished);a.Finished=true;
            SaveMcnContact(person);
            return true;
        }

        private void SaveMcnContact(string person)
        {
            if (!IsMet(person))
            {
                Meet(person, 0);
                var creator = Creator(person);
                if (creator != null) creator.Relationship = Math.Max(creator.Relationship, 5);
                RememberSocial(person, "经" + BrandAgency.ForPerson(person).Name + "介绍，收到了业务名片。");
            }
            Say("已保存" + CityPerson(person).Name + "的业务名片，可从手机通讯录打电话或发消息。");
        }
        private bool TickMcnIntroduction(CityPersonState p,long now)
        {
            var a=_mcnIntroductions.FirstOrDefault(x=>x.PersonId==p.PersonId&&x.Until>=now);
            if(a==null||!string.IsNullOrEmpty(p.ActivityId))return false;
            if(p.LocationPlaceId!=CommunityMcnId)
            {p.Route=PlanCityRoute(p,CommunityMcnId,now);if(p.Route==null)return false;p.LocationPlaceId=null;p.Task=CityPersonTask.Travelling;}
            else p.Task=CityPersonTask.Waiting;
            p.TaskLabel="MCN商务介绍";return true;
        }
        private void RestoreMcnIntroductions(SaveData d)
        {
            _mcnIntroductions=(d.mcnIntroductions??Array.Empty<McnIntroduction>()).Where(x=>x!=null&&(BrandPeople.Contains(x.PersonId)||KolMarket.Get(x.PersonId)!=null)).Select(x=>x.Copy()).ToArray();
            // Previously paid business relationships retain contact access; never charge to reacquire them.
            foreach(string person in BrandPeople)
            {
                var c=Creator(person);
                if(_brands.Deals.Any(x=>x.PersonId==person)||_cityPromotions.Any(x=>x.CreatorId==person)
                    ||c!=null&&(c.McnUntilDay>0||c.CampaignUntilDay>0||c.Relationship>0))
                    if(!IsMet(person))Meet(person,0);
            }
        }
    }
}
