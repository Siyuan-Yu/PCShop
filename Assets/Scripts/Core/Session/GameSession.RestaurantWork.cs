using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Core.Session
{
    public enum RestaurantJob { None, Cook, Plate, Serve, Clear, Deliver }

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
        public const double RestaurantStandSpacing = .85;
        public static string RestaurantJobName(RestaurantJob job)=>job==RestaurantJob.Cook?"备料投锅":job==RestaurantJob.Plate?"出锅":job==RestaurantJob.Serve?"上菜":job==RestaurantJob.Clear?"收桌／清锅":job==RestaurantJob.Deliver?"交付鱼餐／收款":"待命";
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
            RestaurantJob.Serve => !order.IsFishOrder && (order.Stage == DiningStage.Ready || CanTakeOffHeat(order, now)),
            RestaurantJob.Clear => order.NeedsClearing && (!order.IsFishOrder || order.Stage == DiningStage.Burnt),
            RestaurantJob.Deliver => order.IsFishOrder && order.Stage == DiningStage.Ready && now < order.DueAt,
            _ => false
        });

        private static RectD[] RestaurantObstacles(OwnedShop shop, RoomSpec room) =>
            shop.Layout.Select(p => (Placement: p, Definition: FurnitureCatalog.Get(p.Id)))
                .Where(v => v.Definition?.Slot == FurnitureSlot.Floor)
                .Select(v => RoomPlanner.Footprint(room, v.Definition, v.Placement)).ToArray();

        private static bool RestaurantOccupied(double x, double z, IReadOnlyList<(double x, double z)> stands) =>
            stands.Any(p => RestaurantWorker.Distance(x, z, p.x, p.z) < RestaurantStandSpacing);

        private static bool RestaurantBlocked(RectD[] obstacles, double x, double z, double clearance) =>
            obstacles.Any(b => x >= b.MinX - clearance && x <= b.MaxX + clearance
                && z >= b.MinZ - clearance && z <= b.MaxZ + clearance);

        private static (double x, double z) RestaurantDestination(RestaurantWorker worker) =>
            worker.PathX?.Length > 0 ? (worker.PathX.Last(), worker.PathZ.Last()) : (worker.X, worker.Z);

        private static (double x, double z) FindRestaurantStand(OwnedShop shop, double x, double z,
            IReadOnlyList<(double x, double z)> occupied)
        {
            var room = RoomSpec.ForShop(shop.Sim.Size);
            var obstacles = RestaurantObstacles(shop, room);
            var best = (x, z);
            double bestDistance = double.PositiveInfinity;
            for (double xx = .5; xx <= room.Width - .5; xx += RoomPlanner.Grid)
                for (double zz = .5; zz <= room.Depth - .5; zz += RoomPlanner.Grid)
                {
                    double distance = (xx - x) * (xx - x) + (zz - z) * (zz - z);
                    if (distance >= bestDistance || RestaurantOccupied(xx, zz, occupied)
                        || RestaurantBlocked(obstacles, xx, zz, .2)) continue;
                    best = (xx, zz);
                    bestDistance = distance;
                }
            return best;
        }

        // Reserve the standing point as well as the work surface so two reachable routes cannot end on each other.
        private static List<(double x,double z)> RestaurantPath(OwnedShop shop,double x,double z,PlacedFurniture target,
            IReadOnlyList<(double x, double z)> occupied)
        {
            var room=RoomSpec.ForShop(shop.Sim.Size);const double cell=RoomPlanner.Grid;
            var blockers=RestaurantObstacles(shop,room);
            var area=RoomPlanner.Footprint(room,FurnitureCatalog.Get(target.Id),target);
            double Near(double xx,double zz)=>Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(area.MinX-xx,xx-area.MaxX)),2)+Math.Pow(Math.Max(0,Math.Max(area.MinZ-zz,zz-area.MaxZ)),2));
            bool IsStand(double xx, double zz) => xx >= .43 && xx <= room.Width - .43 && zz >= .43 && zz <= room.Depth - .43
                && Near(xx, zz) <= .55 && !RestaurantBlocked(blockers, xx, zz, 0) && !RestaurantOccupied(xx, zz, occupied);
            bool canStay = IsStand(x, z);
            if (canStay && !RestaurantBlocked(blockers, x, z, .2)) return new List<(double, double)> { (x, z) };
            int nx=(int)Math.Ceiling(room.Width/cell)+1,nz=(int)Math.Ceiling(room.Depth/cell)+1;
            int start=Math.Max(1,Math.Min(nx-2,(int)Math.Round(x/cell)))+nx*Math.Max(2,Math.Min(nz-2,(int)Math.Round(z/cell)));
            var previous=Enumerable.Repeat(-1,nx*nz).ToArray();var queue=new Queue<int>();previous[start]=start;queue.Enqueue(start);
            int goal = -1, fallback = -1;
            while(queue.Count>0)
            {
                int current=queue.Dequeue();double xx=current%nx*cell,zz=current/nx*cell;
                if (IsStand(xx, zz))
                {
                    if (!RestaurantBlocked(blockers, xx, zz, .2)) { goal = current; break; }
                    if (fallback < 0) fallback = current;
                }
                for(int dir=0;dir<4;dir++)
                {
                    int ix=current%nx+(dir==0?1:dir==1?-1:0),iz=current/nx+(dir==2?1:dir==3?-1:0);
                    if(ix<1||iz<2||ix>=nx-1||iz>=nz-1)continue;
                    int next=ix+iz*nx;if(previous[next]>=0||RestaurantBlocked(blockers,ix*cell,iz*cell,0))continue;
                    previous[next]=current;queue.Enqueue(next);
                }
            }
            if (goal < 0 && canStay) return new List<(double, double)> { (x, z) };
            if (goal < 0) goal = fallback;
            if (goal < 0) return null;
            var path=new List<(double,double)>();for(int node=goal;node!=start;node=previous[node])path.Add((node%nx*cell,node/nx*cell));
            path.Add((start%nx*cell,start/nx*cell));path.Reverse();path.Insert(0,(x,z));return path;
        }
        private static PlacedFurniture RestaurantStation(OwnedShop shop,DiningOrder order,RestaurantWorker worker,out string id)
        {
            id=worker.Job==RestaurantJob.Deliver?FurnitureCatalog.ShopCounter:
                worker.Job==RestaurantJob.Cook||worker.Job==RestaurantJob.Plate?"restaurant_stove":
                worker.Job==RestaurantJob.Serve?(worker.Phase==0?"restaurant_stove":"restaurant_table"):
                order.Stage==DiningStage.Burnt?"restaurant_stove":"restaurant_table";
            string stationId = id;
            int index = worker.Job == RestaurantJob.Deliver ? 0 : id == "restaurant_table" ? order.Table : Math.Max(0, worker.Stove);
            var stations = shop.Layout.Where(p => p.Id == stationId);
            var target = stations.Skip(index).FirstOrDefault();
            return target ?? (id == "restaurant_stove" && order.Stage == DiningStage.Ready ? stations.FirstOrDefault() : null);
        }
        public PlacedFurniture RestaurantWorkStation(OwnedShop shop, RestaurantWorker worker) =>
            worker.Busy && _specialBusinesses.TryGetValue(shop.Place.Id, out var state)
                ? RestaurantWorkStation(shop, state, worker) : null;

        private static PlacedFurniture RestaurantWorkStation(OwnedShop shop, SpecialBusinessState state, RestaurantWorker worker)
        {
            var order = state.Orders.Find(o => o.Id == worker.OrderId);
            return order == null ? null : RestaurantStation(shop, order, worker, out _);
        }

        private bool PlanRestaurantLeg(OwnedShop shop,SpecialBusinessState state,DiningOrder order,RestaurantWorker worker,double minute)
        {
            var target=RestaurantStation(shop,order,worker,out var station);
            if(target==null)return false;
            var otherWorkers = state.Workers.Where(w => w != worker && w.Busy).ToArray();
            if (otherWorkers.Any(w => RestaurantWorkStation(shop, state, w) == target)) return false;
            var path=RestaurantPath(shop,worker.X,worker.Z,target,otherWorkers.Select(RestaurantDestination).ToArray());
            if(path==null)return false;
            worker.Station=station;
            worker.PathX=path.Select(p=>p.x).ToArray();worker.PathZ=path.Select(p=>p.z).ToArray();
            double length=0;for(int i=1;i<path.Count;i++)length+=RestaurantWorker.Distance(path[i-1].x,path[i-1].z,path[i].x,path[i].z);
            // Matches the indoor 4.8 units/real-second player pace; staff walk at 85% of that pace.
            double speed=4.8*Time.SecondsPerMinuteAt1x*(worker.ActorId==RestaurantPlayer?CityTravelTiming.PlayerWalkSpeedFactor:CityTravelTiming.NpcWalkSpeedFactor);
            worker.Depart=minute;worker.Arrive=minute+length/Math.Max(.01,speed);worker.OperateUntil=-1;return true;
        }
        private bool AssignRestaurantWork(OwnedShop shop,SpecialBusinessState state,RestaurantWorker worker,DiningOrder order,RestaurantJob job,double minute)
        {
            if(!JobMatches(job,order,(long)minute)||state.Workers.Any(w=>w!=worker&&w.Busy&&w.OrderId==order.Id))return false;
            if (job == RestaurantJob.Clear && order.Stage == DiningStage.Checkout && Furnished(shop, FurnitureCatalog.ShopCounter) == 0) return false;
            IEnumerable<int> stoves = new[] { order.Stove };
            if(job==RestaurantJob.Cook)
            {
                if (RestaurantOrderIngredients(shop, state, order, (long)minute).Blocker != null) return false;
                var occupied=state.Orders.Where(o=>o.Stage==DiningStage.Cooking||o.Stage==DiningStage.Burnt).Select(o=>o.Stove);
                stoves = Enumerable.Range(0,Furnished(shop,"restaurant_stove")).Except(occupied);
            }
            worker.Job=job;worker.OrderId=order.Id;worker.Phase=0;worker.LayoutKey=RestaurantLayoutKey(shop);
            foreach (int stove in stoves)
            {
                worker.Stove = stove;
                if (PlanRestaurantLeg(shop,state,order,worker,minute)) return true;
            }
            worker.Job=RestaurantJob.None;return false;
        }
        public bool RequestRestaurantWork(int orderId,RestaurantJob job,double playerX,double playerZ)
        {
            if(!SpecialPlayer(CurrentShop)||CurrentShop.RetailKind!="restaurant")return Fail("请在自己的餐馆内操作，装修期间请先完成摆放。");
            if (job != RestaurantJob.Clear && !CanServeSpecialShop(CurrentShop, Clock.Now, true))
                return Fail("餐馆暂时无法继续服务，请检查厨房设施和供电。");
            var state=SpecialBusiness(CurrentShop.Place.Id);var order=state.Orders.FirstOrDefault(o=>o.Id==orderId);
            if(!JobMatches(job,order,Clock.Now.TotalMinutes))return Fail("这桌的状态已经变化，请重新选择操作。");
            if (job == RestaurantJob.Cook && RestaurantIngredientBlocker(CurrentShop, order) is string missing)
                return Fail(missing);
            if (job == RestaurantJob.Clear && order.Stage == DiningStage.Checkout && Furnished(CurrentShop, FurnitureCatalog.ShopCounter) == 0)
                return Fail("请先摆放收银台，才能在桌边收款。");
            var existing=state.Workers.FirstOrDefault(w=>w.ActorId==RestaurantPlayer);
            if(state.Workers.Any(w=>w.ActorId!=RestaurantPlayer&&w.Busy&&w.OrderId==orderId))return Fail("已有员工正在处理这桌。");
            if(existing?.Busy==true)return Fail("正在执行餐厅任务，按WASD接管后可重新安排。");
            var worker=new RestaurantWorker{ActorId=RestaurantPlayer,X=playerX,Z=playerZ};
            if(!AssignRestaurantWork(CurrentShop,state,worker,order,job,Clock.Now.TotalMinutes+Time.FractionalMinute))return Fail("设备正在使用或路线被家具挡住，请留出通道。");
            state.Workers.RemoveAll(w=>w.ActorId==RestaurantPlayer);state.Workers.Add(worker);return true;
        }
        private bool FinishRestaurantWork(OwnedShop shop,SpecialBusinessState state,RestaurantWorker worker,DiningOrder order,long minute)
        {
            if (worker.Job == RestaurantJob.Serve || worker.Job == RestaurantJob.Clear && order.Stage == DiningStage.Checkout)
            {
                var waiter = ShopStaff(shop, EmployeeRole.Waiter).FirstOrDefault(person => person.Id == worker.ActorId);
                if (waiter != null && (!order.HasWaiterService || worker.Job == RestaurantJob.Serve))
                {
                    order.HasWaiterService = true;
                    order.ServiceCharm = EmployeeCharm(waiter);
                }
            }
            if(worker.Job==RestaurantJob.Cook)return StartRestaurantOrder(shop,state,order,minute,true,worker.Stove);
            if(worker.Job==RestaurantJob.Plate){order.Stage=DiningStage.Ready;return true;}
            if(worker.Job==RestaurantJob.Serve){order.Stage=DiningStage.Eating;order.EatUntil=minute+15;return true;}
            if (worker.Job == RestaurantJob.Deliver)
            {
                SettleRestaurantTable(shop, state, order, new GameTimestamp(minute));
                state.Orders.Remove(order);
                return true;
            }
            return ClearRestaurantOrder(shop, state, order, new GameTimestamp(minute));
        }

        private Money RestaurantServiceTip(OwnedShop shop, SpecialBusinessState state, DiningOrder order, GameTimestamp now)
        {
            if (!order.HasWaiterService || order.IsFishOrder || !Random.Chance(WaiterTipChance(order.ServiceCharm)))
                return Money.Zero;
            long maximum = (long)Math.Floor(order.Price * Math.Min(WaiterTipMaximumRate, WaiterTipUpperRate(order.ServiceCharm)));
            if (maximum < 1) return Money.Zero;
            long yuan = Random.Next(1, (int)Math.Min(int.MaxValue - 1L, maximum) + 1);
            var tip = Money.FromYuan(yuan);
            Ledger.Credit(now.Date, "餐饮小费", tip, order.DishName + " · 顾客感谢服务",
                LedgerAttribution.ForShop(shop.Place.Id, RestaurantSaleId(shop, order) + ":tip", "销售"));
            state.Revenue += yuan;
            Say($"{ShopTitle(shop)}的顾客留下 {tip} 小费。");
            return tip;
        }
        private static bool RestaurantRoleHandles(EmployeeRole role, RestaurantJob job, DiningOrder order) => role switch
        {
            EmployeeRole.Chef => job == RestaurantJob.Cook || job == RestaurantJob.Plate || job == RestaurantJob.Deliver
                || job == RestaurantJob.Clear && order.Stage == DiningStage.Burnt,
            EmployeeRole.Waiter => job == RestaurantJob.Deliver || !order.IsFishOrder && (job == RestaurantJob.Serve && order.Stage == DiningStage.Ready
                || job == RestaurantJob.Clear && order.Stage != DiningStage.Burnt),
            _ => false
        };

        private void TickRestaurantWorkers(OwnedShop shop,SpecialBusinessState state,GameTimestamp now,bool canServe)
        {
            long minute=now.TotalMinutes;var chefs=SpecialWorkers(shop,EmployeeRole.Chef,now);
            var staff=chefs.Select(person => (Person: person, Role: EmployeeRole.Chef))
                .Concat(SpecialWorkers(shop, EmployeeRole.Waiter, now).Select(person => (Person: person, Role: EmployeeRole.Waiter))).ToArray();
            state.Workers.RemoveAll(w=>w.ActorId==RestaurantPlayer?!SpecialPlayer(shop):!staff.Any(e=>e.Person.Id==w.ActorId));
            string layout=RestaurantLayoutKey(shop);
            foreach(var worker in state.Workers.ToArray())
            {
                if(!worker.Busy)continue;
                var pos=worker.Position(minute);worker.X=pos.x;worker.Z=pos.z;
                var order=state.Orders.FirstOrDefault(o=>o.Id==worker.OrderId);
                var employee = staff.FirstOrDefault(e => e.Person.Id == worker.ActorId);
                if(!JobMatches(worker.Job,order,minute)||worker.LayoutKey!=layout||!canServe && worker.Job != RestaurantJob.Clear
                    || employee.Person != null && !RestaurantRoleHandles(employee.Role, worker.Job, order))
                {worker.Job=RestaurantJob.None;continue;}
                if(minute<worker.Arrive)continue;
                if(worker.OperateUntil<0)
                {
                    worker.OperateUntil=minute+(employee.Person==null?RestaurantOperationMinutes:RestaurantEmployeeOperationMinutes(employee.Person,employee.Role));
                    continue;
                }
                if(minute<worker.OperateUntil)continue;
                if (worker.Job == RestaurantJob.Serve && worker.Phase == 0)
                    order.Stage = DiningStage.Ready;
                int phases = worker.Job == RestaurantJob.Serve ? 2 : 1;
                if(++worker.Phase<phases)
                {
                    if(!PlanRestaurantLeg(shop,state,order,worker,minute))
                    {worker.Job=RestaurantJob.None;if(worker.ActorId==RestaurantPlayer)Say("通往操作位置的路线被家具挡住了，请先留出通道。");}
                    continue;
                }
                if(!FinishRestaurantWork(shop,state,worker,order,minute)&&worker.ActorId==RestaurantPlayer)Say("这次操作未完成，请检查食材、锅位及订单状态。");
                worker.Job=RestaurantJob.None;
            }
            foreach(var (employee, role) in staff)
            {
                var worker=state.Workers.FirstOrDefault(w=>w.ActorId==employee.Id);
                if(worker==null)
                {
                    var room=RoomSpec.ForShop(shop.Sim.Size);
                    var occupied = state.Workers.Select(w => (w.X, w.Z))
                        .Concat(state.Workers.Where(w => w.Busy).Select(RestaurantDestination)).ToArray();
                    var stand = FindRestaurantStand(shop, room.Width - 3, .9, occupied);
                    worker = new RestaurantWorker { ActorId = employee.Id, X = stand.x, Z = stand.z };
                    state.Workers.Add(worker);
                }
                if(worker.Busy)continue;
                var jobs = role == EmployeeRole.Chef
                    ? new[] { RestaurantJob.Plate, RestaurantJob.Deliver, RestaurantJob.Clear, RestaurantJob.Cook }
                    : new[] { RestaurantJob.Deliver, RestaurantJob.Clear, RestaurantJob.Serve };
                // 先及时出锅避免焦糊，其次按交期处理鱼餐，最后处理普通堂食。
                var candidates = jobs.Where(job => canServe || job == RestaurantJob.Clear)
                    .SelectMany((job, index) => state.Orders
                        .Where(order => JobMatches(job, order, minute) && RestaurantRoleHandles(role, job, order))
                        .Select(order => (Job: job, Order: order, Index: index)))
                    .OrderBy(task => task.Job == RestaurantJob.Plate ? 0 : task.Order.IsFishOrder ? 1 : 2)
                    .ThenBy(task => task.Order.IsFishOrder ? task.Order.DueAt : 0)
                    .ThenBy(task => task.Index).ThenBy(task => task.Order.Arrived).ThenBy(task => task.Order.Id);
                foreach (var task in candidates)
                {
                    if (AssignRestaurantWork(shop, state, worker, task.Order, task.Job, minute)) break;
                }
            }
            // Free up new destinations and restore distinct idle positions when loading older saves.
            var reserved = state.Workers.Where(w => w.Busy).Select(RestaurantDestination).ToList();
            foreach (var worker in state.Workers.Where(w => !w.Busy && w.ActorId != RestaurantPlayer))
            {
                if (RestaurantOccupied(worker.X, worker.Z, reserved))
                    (worker.X, worker.Z) = FindRestaurantStand(shop, worker.X, worker.Z, reserved);
                reserved.Add((worker.X, worker.Z));
            }
        }
    }
}
