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
            if(!BrandPeople.Contains(person))return "没有这位合作创作者";
            return City.IsLakeCity&&!IsMet(person)?"先去社区澄光传媒认识对方、交换业务名片":null;
        }
        private string McnVisitBlocker()
        {
            if(IsGameOver||IsWorking||_sleeping||CityActivityExclusive)return "请先结束当前工作或活动";
            if(DistrictAccessTier<1)return "先进入社区阶段";
            if(CurrentPlace?.Id!=CommunityMcnId)return "请到社区澄光传媒现场办理介绍";
            if(!IsPlaceOpen(CurrentPlace))return "MCN接待时间09:00—18:00";
            return null;
        }
        public string McnIntroductionBlocker(string person)
        {
            string why=McnVisitBlocker();if(why!=null)return why;
            if(!BrandPeople.Contains(person)||CityPersonState(person)==null)return "没有这位合作创作者";
            if(IsMet(person))return "已有联系方式，可直接通过手机联系";
            if(_mcnIntroductions.Any(x=>x.PersonId==person&&!x.Finished&&x.Until>=Clock.Now.TotalMinutes))return "介绍已安排，请等待对方到场";
            var p=CityPersonState(person);
            if(!string.IsNullOrEmpty(p.ActivityId)||!string.IsNullOrEmpty(p.VisitId)||Employees.Any(e=>PersonIdOf(e.Person)==person)
                ||BrandExpoPersonReserved(person)||_district.MeetingPeople.Contains(person))return "对方已有活动或工作安排，请稍后再约";
            return null;
        }
        public bool RequestMcnIntroduction(string person)
        {
            string why=McnIntroductionBlocker(person);if(why!=null)return Fail(why);
            var p=CityPersonState(person);long now=Clock.Now.TotalMinutes;
            var route=p.LocationPlaceId==CommunityMcnId?null:PlanCityRoute(p,CommunityMcnId,now);
            if(route==null&&p.LocationPlaceId!=CommunityMcnId)return Fail("对方暂时无法到达MCN");
            long arrival=route?.ArriveMinute??now;
            long close=BrandDay*1440L+1080;
            if(arrival+15>close)return Fail("今天来不及会面，明天营业后再安排");
            _mcnIntroductions=_mcnIntroductions.Where(x=>x.PersonId!=person).Concat(new[]{new McnIntroduction{PersonId=person,Arrival=arrival,Until=Math.Min(close,arrival+120)}}).ToArray();
            if(route!=null){p.Route=route;p.LocationPlaceId=null;p.Task=CityPersonTask.Travelling;}
            p.TaskLabel="MCN商务介绍";Say("公司已约好"+CityPerson(person).Name+"，预计"+Math.Max(0,arrival-now)+"分钟后到场。会面后才能交换名片。");return true;
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
            if(!IsMet(person))
            {
                Meet(person,0);var c=Creator(person);if(c!=null)c.Relationship=Math.Max(c.Relationship,5);
                RememberSocial(person,"在社区澄光传媒经公司介绍认识，交换了业务名片。私人交情还需逐步建立。");
            }
            Say("已保存"+CityPerson(person).Name+"的联系方式，可从手机聊天或品牌推广页联系。");return true;
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
            _mcnIntroductions=(d.mcnIntroductions??Array.Empty<McnIntroduction>()).Where(x=>x!=null&&BrandPeople.Contains(x.PersonId)).Select(x=>x.Copy()).ToArray();
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
