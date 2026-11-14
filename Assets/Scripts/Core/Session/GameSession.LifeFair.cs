using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public bool StartLifeFair(string id,bool furniture,int count,int percent)
        {
            var item=EventIdleItems.FirstOrDefault(i=>i.Id==id&&i.Furniture==furniture);
            if(!Flags.Contains("life:fair-invited")||!Clock.Today.IsWeekend||!LifeHere(LifeFairPlace)||!LifeOpen(LifeFairPlace)||Clock.MinuteOfDay>17*60
                ||_lifeStall!=null&&!_lifeStall.Done||item==null||count<1||count>item.Count||percent is not (100 or 90 or 70)||IsWorking||IsGameOver)return false;
            if(!Ledger.TryDebit(Clock.Today,"社区集市摊位",Money.FromYuan(200),"四小时摊位"))return Fail("摊位费不足。");
            if(furniture)Backpack.TakeFurniture(id,count);else Backpack.TakeGift(id,count);ApplyCargoLimit();
            var original=furniture?FurnitureCatalog.Get(id).Price:Gifts.Get(id).Price;
            _lifeStall=new LifeStallState{Item=id,Name=item.Name,Furniture=furniture,Remaining=count,Price=original.Scale(percent/100.0).Cents,
                Ends=Clock.Now.TotalMinutes+240,NextBuyer=Clock.Now.TotalMinutes+15,Message="等待实际顾客到场，离开时停止销售。"};return true;
        }
        private string LifeBuyerVisitId=>"life-fair:"+_lifeStall?.Ends;
        private void ReleaseLifeFairBuyer()
        {
            var p=CityPersonState(_lifeStall?.Buyer);if(p?.VisitId==LifeBuyerVisitId){p.VisitId=null;p.Task=CityPersonTask.Returning;}
            if(_lifeStall!=null)_lifeStall.Buyer=null;
        }
        public bool EndLifeFair()
        {
            var s=_lifeStall;if(s==null||s.Done)return false;ReleaseLifeFairBuyer();s.Ended=true;
            if(!LifeHere(LifeFairPlace)){s.Message="已结束营业，请到集市领取未售物品。";return false;}
            if(s.Furniture)Backpack.AddFurniture(s.Item,s.Remaining);else Backpack.AddGift(s.Item,s.Remaining);
            s.Remaining=0;s.Done=true;ApplyCargoLimit();Say("集市收摊，未售物品已退回背包。");return true;
        }
        private void TickLifeFair(long now)
        {
            var s=_lifeStall;if(s==null||s.Done)return;
            if(now>=s.Ends||s.Remaining==0||s.Ended){EndLifeFair();return;}
            if(!LifeHere(LifeFairPlace)||!LifeOpen(LifeFairPlace)||IsWorking||_sleeping){ReleaseLifeFairBuyer();return;}
            if(s.Buyer!=null)
            {
                var buyer=CityPersonState(s.Buyer);if(buyer==null||buyer.VisitId!=LifeBuyerVisitId){ReleaseLifeFairBuyer();return;}
                if(buyer.Route!=null||buyer.LocationPlaceId!=LifeFairPlace)return;
                double original=(s.Furniture?FurnitureCatalog.Get(s.Item).Price:Gifts.Get(s.Item).Price).Cents;
                double chance=s.Price/original<.75?.95:s.Price/original<.95?.70:.45;
                if(CityRetailBudgetFor(buyer.PersonId)>=s.Price&&Random.Chance(chance))
                {s.Remaining--;s.Sold++;Ledger.Credit(Clock.Today,"社区集市销售",Money.FromCents(s.Price),s.Name+" · "+CityPerson(buyer.PersonId)?.Name,
                    new LedgerAttribution(LedgerBusinessKind.Personal,sourceEventId:"life-fair-sale:"+s.Ends+":"+s.Sold));s.Message=(CityPerson(buyer.PersonId)?.Name??"顾客")+"买走了一件"+s.Name+"。";}
                else s.Message="这位顾客看过实物和报价，没有购买。";
                ReleaseLifeFairBuyer();s.NextBuyer=now+15;return;
            }
            if(now<s.NextBuyer)return;s.NextBuyer=now+15;InitializeCityPeople();
            foreach(var p in _cityPeople.Values.Where(p=>p.PersonId!=CityLifeCatalog.InvitationTestPersonId&&p.VisitId==null&&p.Route==null&&p.ActivityId==null
                &&p.Task!=CityPersonTask.Working&&p.Task!=CityPersonTask.Waiting&&!Employees.Any(e=>e.Person.PersonId==p.PersonId)&&!_candidates.Any(e=>e.PersonId==p.PersonId)
                &&!_cityActivities.Any(a=>a.Active&&a.Participants.Any(g=>g.PersonId==p.PersonId)))
                .OrderBy(p=>DistanceCity(new MapPoint(p.X,p.Y),StandOf(City.GetPlace(LifeFairPlace)))))
            {
                var route=PlanCityRoute(p,LifeFairPlace,now);if(route==null||route.ArriveMinute>=s.Ends)continue;
                p.VisitId=LifeBuyerVisitId;p.Task=CityPersonTask.Shopping;p.Route=route;p.LocationPlaceId=null;s.Buyer=p.PersonId;s.Message="顾客正在沿实际路线来摊位。";break;
            }
        }
        private void RestoreLifeFairBuyer()
        {
            if(_lifeStall==null||_lifeStall.Done||_lifeStall.Buyer==null)return;
            var p=CityPersonState(_lifeStall.Buyer);if(p!=null&&p.VisitId==null){p.VisitId=LifeBuyerVisitId;p.Task=CityPersonTask.Shopping;}
            else _lifeStall.Buyer=null;
        }
    }
}
