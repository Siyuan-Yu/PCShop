using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private KolStreamingState _kol=new KolStreamingState();
        private readonly System.Collections.Generic.Dictionary<string,KolLiveSession> _kolSessionIndex=new System.Collections.Generic.Dictionary<string,KolLiveSession>();
        public KolStreamingState KolStreaming=>_kol.Copy();
        public bool BrandManagementUnlocked=>DistrictAccessTier>=1||_brands.Brands.Length>0;
        public static int KolCapacity(int level)=>level>=2?12:level==1?7:3;
        public string BrandLevelName(string id)=>Brand(id)?.Level>=2?"高级品牌":Brand(id)?.Level==1?"中级品牌":"初级品牌";
        public int BrandKolCapacity(string id)=>KolCapacity(Brand(id)?.Level??0);
        private bool KolContractCurrent(KolLiveContract c,long at)=>!c.Ended&&c.SignedAt<=at&&at<c.EndAt;
        public int BrandKolCount(string id)=>_kol.Contracts.Count(c=>c.BrandId==id&&KolContractCurrent(c,Clock.Now.TotalMinutes));
        public long KolSigningFee(string person)=>KolMarket.Get(person)==null?0:1000L+KolMarket.Get(person).Audience/8;
        public string KolUpgradeBlocker(string brandId)
        {
            var b=Brand(brandId);if(IsGameOver||b==null)return "没有可升级的品牌";
            if(b.Level>=2)return "已达到高级品牌";
            int influence=b.Level==0?100:300;long price=b.Level==0?20000:100000;
            if(b.Influence<influence)return "品牌影响力需要达到"+influence;
            return Ledger.Balance<Money.FromYuan(price)?"升级需要¥"+price:null;
        }
        public bool UpgradeKolBrand(string brandId)
        {
            string why=KolUpgradeBlocker(brandId);if(why!=null)return Fail(why);
            var b=Brand(brandId);if(!BrandPay((b.Level==0?20000L:100000L)*100,"品牌升级 · "+b.Name))return false;
            b.Level++;return true;
        }
        public string KolSigningBlocker(string brandId,string person)
        {
            if(IsGameOver||Brand(brandId)==null||KolMarket.Get(person)==null)return "无法签约该直播合作";
            if(City.IsLakeCity&&!IsMet(person))return "先去社区MCN实际认识、交换名片，再签约";
            if(_kol.Contracts.Any(c=>c.PersonId==person&&KolContractCurrent(c,Clock.Now.TotalMinutes)))return "此KOL已有独家直播合约";
            if(_brands.Deals.Any(c=>c.PersonId==person&&c.BrandId!=brandId&&c.Exclusive&&!c.Ended&&c.End>BrandDay))return "此KOL已有其他品牌独家代言";
            if(BrandKolCount(brandId)>=BrandKolCapacity(brandId))return "直播签约名额已满，请升级品牌或解除合约";
            return Ledger.Balance<Money.FromYuan(KolSigningFee(person))?"签约资金不足":null;
        }
        public bool SignKolLive(string brandId,string person)
        {
            string why=KolSigningBlocker(brandId,person);if(why!=null)return Fail(why);
            long fee=KolSigningFee(person)*100;
            if(!BrandPay(fee,"90天直播签约 · "+(CityPerson(person)?.Name??person)))return false;
            long now=Clock.Now.TotalMinutes;
            _kol.Contracts=AppendBrand(_kol.Contracts,new KolLiveContract{Id="kol:"+_kol.NextId++,BrandId=brandId,PersonId=person,
                SignedAt=now,EndAt=now+90*1440L,LastProcessedAt=now,LastPaidThrough=now,ScheduleEffectiveAt=now,FeeCents=fee});
            EnsureKolCareer(person);InvalidateKolEffects();return true;
        }
        public bool EndKolLive(string id)
        {
            var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);if(IsGameOver||c==null||!KolContractCurrent(c,Clock.Now.TotalMinutes))return false;
            if(ActiveBrandExpo?.KolIds.Contains(c.PersonId)==true)return Fail("正在为展会站台，闭展后才能解除合约");
            AccrueKolWages(c,Clock.Now.TotalMinutes);
            c.Ended=true;InvalidateKolEffects();return true;
        }
        public bool SetKolSchedule(string id,int start,int end,bool enabled)
        {
            var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);
            if(IsGameOver||c==null||!KolContractCurrent(c,Clock.Now.TotalMinutes))return false;
            int duration=(end-start+1440)%1440;
            if(start<0||start>=1440||end<0||end>=1440||start%15!=0||end%15!=0||duration<15||duration>720)return Fail("直播时间按15分钟选择，每日15分钟至12小时，可跨午夜");
            if(c.StartMinute==start&&c.EndMinute==end&&c.Enabled==enabled)return true;
            c.StartMinute=start;c.EndMinute=end;c.Enabled=enabled;c.ScheduleVersion++;c.ScheduleEffectiveAt=Clock.Now.TotalMinutes;
            InvalidateKolEffects();return true;
        }
        private long KolScheduledStart(KolLiveContract c,long at)
        {
            if(!c.Enabled||!KolContractCurrent(c,at))return -1;
            long day=at/1440;int minute=(int)(at%1440);
            if(c.StartMinute<c.EndMinute)return minute>=c.StartMinute&&minute<c.EndMinute?day*1440+c.StartMinute:-1;
            return minute>=c.StartMinute?day*1440+c.StartMinute:minute<c.EndMinute?(day-1)*1440+c.StartMinute:-1;
        }
        private bool KolPersonAvailable(string id,long at)
        {
            var p=CityPersonState(id);if(p==null||p.Route!=null||p.LocationPlaceId==null||!string.IsNullOrEmpty(p.ActivityId)||!string.IsNullOrEmpty(p.VisitId))return false;
            if(BrandExpoPersonReserved(id)||_district.MeetingPeople.Contains(id))return false;
            if(Employees.Any(e=>PersonIdOf(e.Person)==id&&e.Person.OnDuty(Clock.Now)))return false;
            if(AuthoredCityWorkAt(p,at)&&!(Creators.Get(id)!=null&&p.LocationPlaceId==CommunityMcnId))return false;
            return true;
        }
        public string KolLiveBlocker(string id)
        {
            var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);long now=Clock.Now.TotalMinutes;
            if(c==null||!KolContractCurrent(c,now))return "直播合约已结束";
            if(!c.Enabled)return "直播排班已暂停";
            if(KolScheduledStart(c,now)<0)return "未到直播时段";
            return KolPersonAvailable(c.PersonId,now)?null:"主播正在工作、赶路或参加其他活动，直播暂缓";
        }
        private bool HoldKolLivePerson(CityPersonState p,long now)
        {
            if(_kol.Contracts.Length==0)return false;
            var c=_kol.Contracts.FirstOrDefault(x=>x.PersonId==p.PersonId&&KolScheduledStart(x,now)>=0);
            if(c==null||!KolPersonAvailable(p.PersonId,now))return false;
            p.Task=CityPersonTask.Working;p.TaskLabel="品牌直播 · "+Brand(c.BrandId)?.Name;return true;
        }
        public int KolFollowers(string person)=>Creator(person)?.Followers??_kol.Careers.FirstOrDefault(x=>x.PersonId==person)?.Followers??KolMarket.Get(person)?.Audience??0;
        public int KolPopularity(string person)=>_kol.Careers.FirstOrDefault(x=>x.PersonId==person)?.Popularity??0;
        private KolCareerState EnsureKolCareer(string person)
        {
            var state=_kol.Careers.FirstOrDefault(x=>x.PersonId==person);if(state!=null)return state;
            state=new KolCareerState{PersonId=person,Followers=KolFollowers(person)};_kol.Careers=AppendBrand(_kol.Careers,state);return state;
        }
        private string KolSessionId(KolLiveContract c,long start)=>c.Id+":"+start+":"+c.ScheduleVersion;
        private KolLiveSession CurrentKolSession(KolLiveContract c,long at)
        {long start=KolScheduledStart(c,at);return start>=0&&_kolSessionIndex.TryGetValue(KolSessionId(c,start),out var session)?session:null;}
        private int KolViewersAt(KolLiveContract c,long at)
        {
            var def=KolMarket.Get(c.PersonId);if(def==null)return 0;
            double wave=.9+((at/5+def.Audience+def.Charm)%21)*.01;
            var session=CurrentKolSession(c,at);double boost=session==null?0:session.TipBoost*Math.Max(0,1-(at-session.LastTipAt)/60.0);
            return Math.Max(1,(int)(KolFollowers(c.PersonId)*.035*(.4+def.Charm/100.0)*(1+Math.Min(1000,KolPopularity(c.PersonId))/500.0)*wave+boost));
        }
        public int KolRoomViewers(string id)
        {var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);return c==null||KolLiveBlocker(id)!=null?0:KolViewersAt(c,Clock.Now.TotalMinutes);}
        public double KolRoomTraffic(string id)
        {var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);if(c==null)return 0;var d=KolMarket.Get(c.PersonId);return KolTrafficContribution(d.Charm,KolRoomViewers(id))*(d.Kind==Brand(c.BrandId)?.Kind?1:.65);}
        public double KolRoomIntent(string id)
        {var c=_kol.Contracts.FirstOrDefault(x=>x.Id==id);if(c==null||KolRoomViewers(id)==0)return 0;var d=KolMarket.Get(c.PersonId);return KolIntentContribution(d.Intelligence,KolRoomViewers(id))*(d.Kind==Brand(c.BrandId)?.Kind?1:.65);}
        public double KolBrandTrafficBonus(string id)=>KolBrandEffects(id).Item1;
        public double KolBrandIntentBonus(string id)=>KolBrandEffects(id).Item2;
        public static double KolTrafficContribution(int charm,int viewers)=>Math.Min(.25,Math.Max(0,viewers)/1000.0*(.3+Math.Max(0,Math.Min(100,charm))/100.0));
        public static double KolIntentContribution(int intelligence,int viewers)=>viewers<=0?0:Math.Max(0,Math.Min(100,intelligence))*.0005*(.5+Math.Min(1,Math.Max(0,viewers)/500.0));
        private long _kolEffectMinute=-1;
        private readonly System.Collections.Generic.Dictionary<string,Tuple<double,double>> _kolEffects=new System.Collections.Generic.Dictionary<string,Tuple<double,double>>();
        private readonly System.Collections.Generic.Dictionary<string,int> _kolAvailability=new System.Collections.Generic.Dictionary<string,int>();
        private void InvalidateKolEffects(){_kolEffectMinute=-1;_kolEffects.Clear();_kolAvailability.Clear();}
        private Tuple<double,double> KolBrandEffects(string brandId)
        {
            long now=Clock.Now.TotalMinutes;if(_kolEffectMinute!=now){_kolEffectMinute=now;_kolEffects.Clear();}
            var active=_kol.Contracts.Where(x=>x.BrandId==brandId&&KolScheduledStart(x,now)>=0).ToArray();
            int signature=17;foreach(var c in active)signature=unchecked(signature*31+(KolPersonAvailable(c.PersonId,now)?1:0));
            if(_kolEffects.TryGetValue(brandId,out var value)&&_kolAvailability.TryGetValue(brandId,out int old)&&signature==old)return value;
            double traffic=0,intent=0;
            foreach(var c in active)
            {
                if(!KolPersonAvailable(c.PersonId,now))continue;var d=KolMarket.Get(c.PersonId);int viewers=KolViewersAt(c,now);
                double fit=d.Kind==Brand(brandId)?.Kind?1:.65;
                traffic+=KolTrafficContribution(d.Charm,viewers)*fit;intent+=KolIntentContribution(d.Intelligence,viewers)*fit;
            }
            value=Tuple.Create(Math.Min(.9,traffic),Math.Min(.20,intent));_kolEffects[brandId]=value;_kolAvailability[brandId]=signature;return value;
        }
        private BrandRecord KolShopBrand(OwnedShop shop)=>_brands.Brands.FirstOrDefault(b=>b.Kind==shop.RetailKind&&b.ShopIds.Contains(shop.Place.Id));
        public double KolShopTraffic(OwnedShop shop){var b=KolShopBrand(shop);return b==null?0:KolBrandEffects(b.Id).Item1;}
        public double KolShopIntent(OwnedShop shop){var b=KolShopBrand(shop);return b==null?0:KolBrandEffects(b.Id).Item2;}
        private void TickKolStreams(long from,long to)
        {
            if(_kol.Contracts.Length==0)return;
            bool influenceChanged=false;
            foreach(var c in _kol.Contracts)
            {
                for(long at=Math.Max(from,c.LastProcessedAt);at<to;at++)
                {
                    long start=KolScheduledStart(c,at);if(start<0||!KolPersonAvailable(c.PersonId,at))continue;
                    var id=KolSessionId(c,start);
                    if(!_kolSessionIndex.TryGetValue(id,out var session))
                    {
                        session=new KolLiveSession{Id=id,ContractId=c.Id,StartedAt=Math.Max(start,Math.Max(c.SignedAt,c.ScheduleEffectiveAt)),
                            EndsAt=Math.Min(c.EndAt,start+(c.EndMinute-c.StartMinute+1440)%1440)};
                        _kol.Sessions=AppendBrand(_kol.Sessions,session);
                        _kolSessionIndex[id]=session;
                    }
                    int viewers=KolViewersAt(c,at);session.LiveMinutes++;session.ViewerMinutes+=viewers;session.PeakViewers=Math.Max(session.PeakViewers,viewers);
                    var career=EnsureKolCareer(c.PersonId);career.LiveMinutes++;
                    if(session.LiveMinutes%30==0)
                    {
                        career.Popularity=Math.Min(1000,career.Popularity+1);career.Followers=Math.Min(2000000,KolFollowers(c.PersonId)+Math.Max(1,viewers/100));
                        var creator=Creator(c.PersonId);if(creator!=null)creator.Followers=career.Followers;
                    }
                    if(session.LiveMinutes%60==0){var b=Brand(c.BrandId);if(b!=null){b.Influence++;influenceChanged=true;}}
                }
                c.LastProcessedAt=Math.Max(c.LastProcessedAt,to);
            }
            // Keep recent sessions for results; lifetime career totals are retained separately.
            if(_kol.Sessions.Length>720)
            {
                _kol.Sessions=_kol.Sessions.OrderByDescending(s=>s.StartedAt).Take(720).ToArray();
                _kolSessionIndex.Clear();foreach(var s in _kol.Sessions)_kolSessionIndex[s.Id]=s;
            }
            InvalidateKolEffects();
            if(influenceChanged)RefreshBrandTraffic();
        }
        public bool TipKolLive(string id,int yuan)
        {
            if(IsGameOver||!new[]{10,100,1000}.Contains(yuan))return false;
            string why=KolLiveBlocker(id);if(why!=null)return Fail(why);
            var c=_kol.Contracts.First(x=>x.Id==id);var session=CurrentKolSession(c,Clock.Now.TotalMinutes);
            if(session==null)return Fail("直播刚刚开始，请先让游戏时间推进一分钟");
            if(Clock.Now.TotalMinutes-session.LastTipAt<15)return Fail("每个直播间每15游戏分钟可打赏一次");
            if(!Ledger.TryDebit(Clock.Today,"直播打赏",Money.FromYuan(yuan),CityPerson(c.PersonId).Name+" · "+Brand(c.BrandId).Name,
                LedgerAttribution.ForAsset(c.BrandId,"kol:gift:"+id+":"+Clock.Now.TotalMinutes)))return Fail("打赏现金不足");
            int remainingBoost=(int)(session.TipBoost*Math.Max(0,1-(Clock.Now.TotalMinutes-session.LastTipAt)/60.0));
            session.LastTipAt=Clock.Now.TotalMinutes;session.TipsCents+=yuan*100L;session.TipCount++;session.TipBoost=Math.Min(300,remainingBoost+(yuan==10?25:yuan==100?60:120));
            var career=EnsureKolCareer(c.PersonId);career.TipsReceivedCents+=yuan*100L;career.Popularity=Math.Min(1000,career.Popularity+(yuan==10?1:yuan==100?3:8));
            career.Followers=Math.Min(2000000,KolFollowers(c.PersonId)+(yuan==10?5:yuan==100?20:80));
            var creator=Creator(c.PersonId);if(creator!=null)creator.Followers=career.Followers;
            if(Random.Chance(yuan==10?.08:yuan==100?.18:.35)){Brand(c.BrandId).Influence++;session.BrandGiftWins++;}
            InvalidateKolEffects();RefreshBrandTraffic();return true;
        }
        private void RestoreKolStreaming(SaveData data)
        {
            _kol=data.kolStreaming?.Copy()??new KolStreamingState();
            _kol.Contracts=(_kol.Contracts??Array.Empty<KolLiveContract>()).Where(c=>KolMarket.Get(c.PersonId)!=null&&Brand(c.BrandId)!=null)
                .GroupBy(c=>c.Id).Select(g=>g.First()).ToArray();
            _kol.Careers=_kol.Careers??Array.Empty<KolCareerState>();_kol.Sessions=_kol.Sessions??Array.Empty<KolLiveSession>();
            _kolSessionIndex.Clear();foreach(var s in _kol.Sessions)_kolSessionIndex[s.Id]=s;
            foreach(var c in _kol.Contracts){c.LastProcessedAt=Math.Min(Clock.Now.TotalMinutes,Math.Max(0,c.LastProcessedAt));if(c.LastPaidThrough==0)c.LastPaidThrough=Clock.Now.TotalMinutes;}
            _kol.NextId=Math.Max(_kol.NextId,_kol.Contracts.Length+1);InvalidateKolEffects();
        }
    }
}
