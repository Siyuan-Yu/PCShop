using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum RestaurantJob { None, Cook, Plate, Serve, Clear }

    [Serializable]
    public sealed class RestaurantWorker
    {
        public string ActorId, LayoutKey, Station;
        public int OrderId, Phase, Stove=-1;
        public RestaurantJob Job;
        public double X,Z, Depart,Arrive,OperateUntil=-1;
        public double[] PathX=Array.Empty<double>(),PathZ=Array.Empty<double>();
        public bool Busy=>Job!=RestaurantJob.None;
        public bool Carrying=>Job==RestaurantJob.Serve&&Phase==1;
        public RestaurantWorker Copy()
        {
            var copy=(RestaurantWorker)MemberwiseClone();copy.PathX=(double[])(PathX??Array.Empty<double>()).Clone();copy.PathZ=(double[])(PathZ??Array.Empty<double>()).Clone();return copy;
        }
        public (double x,double z) Position(double minute)
        {
            if(!Busy||PathX==null||PathX.Length<2||Arrive<=Depart)return (X,Z);
            double length=0;for(int i=1;i<PathX.Length;i++)length+=Distance(PathX[i-1],PathZ[i-1],PathX[i],PathZ[i]);
            double remaining=length*Math.Max(0,Math.Min(1,(minute-Depart)/(Arrive-Depart)));
            for(int i=1;i<PathX.Length;i++)
            {
                double segment=Distance(PathX[i-1],PathZ[i-1],PathX[i],PathZ[i]);
                if(remaining<=segment)return (PathX[i-1]+(PathX[i]-PathX[i-1])*remaining/Math.Max(segment,1e-9),PathZ[i-1]+(PathZ[i]-PathZ[i-1])*remaining/Math.Max(segment,1e-9));
                remaining-=segment;
            }
            return (PathX.Last(),PathZ.Last());
        }
        internal static double Distance(double x,double z,double xx,double zz)=>Math.Sqrt((xx-x)*(xx-x)+(zz-z)*(zz-z));
    }

    public sealed partial class SpecialBusinessState
    {
        public List<RestaurantWorker> Workers=new List<RestaurantWorker>();
    }

    public sealed partial class GameSession
    {
        public const string RestaurantPlayer="$player";
        public const int RestaurantOperationMinutes=2;
        public static string RestaurantJobName(RestaurantJob job)=>job==RestaurantJob.Cook?"备料投锅":job==RestaurantJob.Plate?"出锅":job==RestaurantJob.Serve?"出锅上菜":job==RestaurantJob.Clear?"收桌／清锅":"待命";
        public RestaurantWorker RestaurantActor(string shopId,string actor)=>_specialBusinesses.TryGetValue(shopId,out var state)?state.Workers.FirstOrDefault(w=>w.ActorId==actor):null;
        public void CancelPlayerRestaurantWork()
        {
            foreach(var state in _specialBusinesses.Values)state.Workers.RemoveAll(w=>w.ActorId==RestaurantPlayer);
        }
        private static string RestaurantLayoutKey(OwnedShop shop)=>shop.Sim.Size+":"+string.Join(";",shop.Layout.Select(p=>p.Id+":"+p.X+":"+p.Z+":"+p.Rot));
        private static bool CanTakeOffHeat(DiningOrder order, long now) =>
            order?.Stage == DiningStage.Cooking && now >= order.ReadyAt && now < order.BurnAt;

        private static bool JobMatches(RestaurantJob job, DiningOrder order, long now) => order != null && (job switch
        {
            RestaurantJob.Cook => order.Stage == DiningStage.Waiting,
            RestaurantJob.Plate => CanTakeOffHeat(order, now),
            RestaurantJob.Serve => order.Stage == DiningStage.Ready || CanTakeOffHeat(order, now),
            RestaurantJob.Clear => order.NeedsClearing,
            _ => false
        });

        // A grid route to the reachable edge of the actual furniture footprint, shared by on/off-screen staff.
        private static List<(double x,double z)> RestaurantPath(OwnedShop shop,double x,double z,PlacedFurniture target)
        {
            var room=RoomSpec.ForShop(shop.Sim.Size);const double cell=.25;
            var blockers=shop.Layout.Select(p=>(p,d:FurnitureCatalog.Get(p.Id))).Where(v=>v.d?.Slot==FurnitureSlot.Floor)
                .Select(v=>RoomPlanner.Footprint(room,v.d,v.p)).ToArray();
            var area=RoomPlanner.Footprint(room,FurnitureCatalog.Get(target.Id),target);
            bool Blocked(double xx,double zz)=>blockers.Any(b=>xx>=b.MinX&&xx<=b.MaxX&&zz>=b.MinZ&&zz<=b.MaxZ);
            double Near(double xx,double zz)=>Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(area.MinX-xx,xx-area.MaxX)),2)+Math.Pow(Math.Max(0,Math.Max(area.MinZ-zz,zz-area.MaxZ)),2));
            if(!Blocked(x,z)&&Near(x,z)<=.55)return new List<(double,double)>{(x,z)};
            int nx=(int)Math.Ceiling(room.Width/cell)+1,nz=(int)Math.Ceiling(room.Depth/cell)+1;
            int start=Math.Max(1,Math.Min(nx-2,(int)Math.Round(x/cell)))+nx*Math.Max(2,Math.Min(nz-2,(int)Math.Round(z/cell)));
            var previous=Enumerable.Repeat(-1,nx*nz).ToArray();var queue=new Queue<int>();previous[start]=start;queue.Enqueue(start);int goal=-1;
            while(queue.Count>0)
            {
                int current=queue.Dequeue();double xx=current%nx*cell,zz=current/nx*cell;
                if(!Blocked(xx,zz)&&Near(xx,zz)<=.55){goal=current;break;}
                for(int dir=0;dir<4;dir++)
                {
                    int ix=current%nx+(dir==0?1:dir==1?-1:0),iz=current/nx+(dir==2?1:dir==3?-1:0);
                    if(ix<1||iz<2||ix>=nx-1||iz>=nz-1)continue;
                    int next=ix+iz*nx;if(previous[next]>=0||Blocked(ix*cell,iz*cell))continue;
                    previous[next]=current;queue.Enqueue(next);
                }
            }
            if(goal<0)return null;
            var path=new List<(double,double)>();for(int node=goal;node!=start;node=previous[node])path.Add((node%nx*cell,node/nx*cell));
            path.Add((start%nx*cell,start/nx*cell));path.Reverse();path.Insert(0,(x,z));return path;
        }
        private PlacedFurniture RestaurantStation(OwnedShop shop,DiningOrder order,RestaurantWorker worker)
        {
            string id=worker.Job==RestaurantJob.Cook||worker.Job==RestaurantJob.Plate?"restaurant_stove":
                worker.Job==RestaurantJob.Serve?(worker.Phase==0?"restaurant_stove":"restaurant_table"):
                order.Stage==DiningStage.Burnt?"restaurant_stove":"restaurant_table";
            worker.Station=id;int index=id=="restaurant_table"?order.Table:id=="restaurant_stove"?Math.Max(0,worker.Stove):0;
            var stations = shop.Layout.Where(p => p.Id == id);
            var target = stations.Skip(index).FirstOrDefault();
            return target ?? (id == "restaurant_stove" && order.Stage == DiningStage.Ready ? stations.FirstOrDefault() : null);
        }
        private int RestaurantCheckoutWalkMinutes(OwnedShop shop,DiningOrder order)
        {
            var table=shop.Layout.Where(p=>p.Id=="restaurant_table").Skip(order.Table).FirstOrDefault();
            var counter=shop.Layout.FirstOrDefault(p=>p.Id==FurnitureCatalog.ShopCounter);
            if(table==null||counter==null)return 1;
            var path=RestaurantPath(shop,table.X,table.Z-.7,counter);
            double distance=0;
            if(path!=null)
                for(int i=1;i<path.Count;i++)distance+=RestaurantWorker.Distance(path[i-1].x,path[i-1].z,path[i].x,path[i].z);
            else distance=RestaurantWorker.Distance(table.X,table.Z-.7,counter.X,counter.Z);
            double speed=4.8*Time.SecondsPerMinuteAt1x*CityTravelTiming.NpcWalkSpeedFactor;
            return Math.Max(2,(int)Math.Ceiling((distance+1.5)/Math.Max(.01,speed)));
        }
        private bool PlanRestaurantLeg(OwnedShop shop,DiningOrder order,RestaurantWorker worker,double minute)
        {
            var target=RestaurantStation(shop,order,worker);if(target==null)return false;
            var path=RestaurantPath(shop,worker.X,worker.Z,target);if(path==null)return false;
            worker.PathX=path.Select(p=>p.x).ToArray();worker.PathZ=path.Select(p=>p.z).ToArray();
            double length=0;for(int i=1;i<path.Count;i++)length+=RestaurantWorker.Distance(path[i-1].x,path[i-1].z,path[i].x,path[i].z);
            // Matches the indoor 4.8 units/real-second player pace; staff walk at 85% of that pace.
            double speed=4.8*Time.SecondsPerMinuteAt1x*(worker.ActorId==RestaurantPlayer?CityTravelTiming.PlayerWalkSpeedFactor:CityTravelTiming.NpcWalkSpeedFactor);
            worker.Depart=minute;worker.Arrive=minute+length/Math.Max(.01,speed);worker.OperateUntil=-1;return true;
        }
        private bool AssignRestaurantWork(OwnedShop shop,SpecialBusinessState state,RestaurantWorker worker,DiningOrder order,RestaurantJob job,double minute)
        {
            if(!JobMatches(job,order,(long)minute)||state.Workers.Any(w=>w!=worker&&w.Busy&&w.OrderId==order.Id))return false;
            int stove=order.Stove;
            if(job==RestaurantJob.Cook)
            {
                if(Furnished(shop,"restaurant_stove")==0)return false;
                if(KitchenRice(shop)<.25-1e-9||KitchenIngredientCount(shop,RecipeIngredients[order.Recipe])<1)return false;
                var occupied=state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt).Select(o=>o.Stove)
                    .Concat(state.Workers.Where(w=>w.Busy&&w.Job==RestaurantJob.Cook).Select(w=>w.Stove)).ToArray();
                stove=Enumerable.Range(0,Furnished(shop,"restaurant_stove")).FirstOrDefault(i=>!occupied.Contains(i));
                if(occupied.Contains(stove))return false;
            }
            worker.Job=job;worker.OrderId=order.Id;worker.Phase=0;worker.Stove=stove;worker.LayoutKey=RestaurantLayoutKey(shop);
            if(PlanRestaurantLeg(shop,order,worker,minute))return true;
            worker.Job=RestaurantJob.None;return false;
        }
        public bool RequestRestaurantWork(int orderId,RestaurantJob job,double playerX,double playerZ)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant")return Fail("请在自己的餐馆内操作，装修期间请先完成摆放。");
            if(job==RestaurantJob.Cook&&!SpecialOpen(CurrentShop,Clock.Now))return Fail("餐馆尚未营业，请检查营业时间和厨房设施。");
            var state=SpecialBusiness(CurrentShop.Place.Id);var order=state.Orders.FirstOrDefault(o=>o.Id==orderId);
            if(!JobMatches(job,order,Clock.Now.TotalMinutes))return Fail("这桌的状态已经变化，请重新选择操作。");
            var existing=state.Workers.FirstOrDefault(w=>w.ActorId==RestaurantPlayer);
            if(state.Workers.Any(w=>w.ActorId!=RestaurantPlayer&&w.Busy&&w.OrderId==orderId))return Fail("已有员工正在处理这桌。");
            if(existing?.Busy==true)return Fail("正在执行餐厅任务，按WASD接管后可重新安排。");
            var worker=new RestaurantWorker{ActorId=RestaurantPlayer,X=playerX,Z=playerZ};
            if(!AssignRestaurantWork(CurrentShop,state,worker,order,job,Clock.Now.TotalMinutes+Time.FractionalMinute))return Fail("设备正在使用或路线被家具挡住，请留出通道。");
            state.Workers.RemoveAll(w=>w.ActorId==RestaurantPlayer);state.Workers.Add(worker);return true;
        }
        private bool FinishRestaurantWork(OwnedShop shop,SpecialBusinessState state,RestaurantWorker worker,DiningOrder order,long minute)
        {
            if(worker.Job==RestaurantJob.Cook)return StartRestaurantOrder(shop,state,order,minute,true,worker.Stove);
            if(worker.Job==RestaurantJob.Plate){order.Stage=DiningStage.Ready;return true;}
            if(worker.Job==RestaurantJob.Serve){order.Stage=DiningStage.Eating;order.EatUntil=minute+15;return true;}
            return ClearRestaurantOrder(state,order,minute);
        }
        private void TickRestaurantWorkers(OwnedShop shop,SpecialBusinessState state,GameTimestamp now,bool open)
        {
            long minute=now.TotalMinutes;var chefs=SpecialWorkers(shop,EmployeeRole.Chef,now);
            var staff=chefs.Concat(SpecialWorkers(shop,EmployeeRole.Waiter,now)).Distinct().ToArray();
            state.Workers.RemoveAll(w=>w.ActorId==RestaurantPlayer?!SpecialPlayer(shop):!staff.Any(e=>e.Id==w.ActorId));
            string layout=RestaurantLayoutKey(shop);
            foreach(var worker in state.Workers.ToArray())
            {
                if(!worker.Busy)continue;
                var pos=worker.Position(minute);worker.X=pos.x;worker.Z=pos.z;
                var order=state.Orders.FirstOrDefault(o=>o.Id==worker.OrderId);
                bool stoppedForClosing=!open&&(worker.ActorId!=RestaurantPlayer||worker.Job==RestaurantJob.Cook);
                if(!JobMatches(worker.Job,order,minute)||worker.LayoutKey!=layout||stoppedForClosing){worker.Job=RestaurantJob.None;continue;}
                if(minute<worker.Arrive)continue;
                if(worker.OperateUntil<0)
                {
                    var employee=staff.FirstOrDefault(e=>e.Id==worker.ActorId);
                    var role=chefs.Contains(employee)?EmployeeRole.Chef:EmployeeRole.Waiter;
                    worker.OperateUntil=minute+(employee==null?RestaurantOperationMinutes:RestaurantEmployeeOperationMinutes(employee,role));
                    continue;
                }
                if(minute<worker.OperateUntil)continue;
                if (worker.Job == RestaurantJob.Serve && worker.Phase == 0)
                    order.Stage = DiningStage.Ready;
                int phases = worker.Job == RestaurantJob.Serve ? 2 : 1;
                if(++worker.Phase<phases)
                {
                    if(!PlanRestaurantLeg(shop,order,worker,minute))
                    {worker.Job=RestaurantJob.None;if(worker.ActorId==RestaurantPlayer)Say("通往操作位置的路线被家具挡住了，请先留出通道。");}
                    continue;
                }
                if(!FinishRestaurantWork(shop,state,worker,order,minute)&&worker.ActorId==RestaurantPlayer)Say("这次操作未完成，请检查食材、锅位及订单状态。");
                worker.Job=RestaurantJob.None;
            }
            if(!open)return;
            foreach(var employee in staff)
            {
                var worker=state.Workers.FirstOrDefault(w=>w.ActorId==employee.Id);
                if(worker==null){var room=RoomSpec.ForShop(shop.Sim.Size);worker=new RestaurantWorker{ActorId=employee.Id,X=room.Width-3,Z=.9};state.Workers.Add(worker);}
                if(worker.Busy)continue;
                bool cook=chefs.Contains(employee);
                foreach(var job in cook?new[]{RestaurantJob.Plate,RestaurantJob.Clear,RestaurantJob.Cook}:new[]{RestaurantJob.Serve,RestaurantJob.Clear})
                {
                    var candidates=state.Orders.Where(o=>JobMatches(job,o,minute)
                        && (job!=RestaurantJob.Serve || o.Stage==DiningStage.Ready)
                        && (job!=RestaurantJob.Clear || (cook?o.Stage==DiningStage.Burnt:o.Stage!=DiningStage.Burnt))).OrderBy(o=>o.Arrived);
                    bool assigned=false;foreach(var order in candidates)if(AssignRestaurantWork(shop,state,worker,order,job,minute)){assigned=true;break;}
                    if(assigned)break;
                }
            }
        }
    }
}
