using System;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed class MotorModel
    {
        public readonly string Id,Name; public readonly VehicleKind Kind; public readonly long Price;
        public readonly double Speed,Acceleration,Grip,Handling,Braking,Stability;
        public MotorModel(string id,string name,VehicleKind kind,long price,double speed,double acceleration,double grip,double handling,double braking,double stability)
        {Id=id;Name=name;Kind=kind;Price=price;Speed=speed;Acceleration=acceleration;Grip=grip;Handling=handling;Braking=braking;Stability=stability;}
    }
    [Serializable] public sealed class GarageVehicle
    {
        public string Id,ModelId,GarageId; public int Motor,Battery,Tyres,Steering; public double Condition=100;
        public bool Commuter,Impounded;
        public bool EventBatteryWorn;
        public int SponsorRacesRemaining;
        public GarageVehicle Copy()=>(GarageVehicle)MemberwiseClone();
    }
    public static class MotorCatalog
    {
        public static readonly MotorModel[] All={
            new MotorModel("e_city","顺风·轻行",VehicleKind.EBike,1800,80,7,72,88,68,70),
            new MotorModel("e_range","顺风·远行",VehicleKind.EBike,3200,96,7.6,80,75,76,84),
            new MotorModel("e_turn","顺风·灵燕",VehicleKind.EBike,5600,110,9.6,90,96,85,88),
            new MotorModel("e_dash","顺风·疾电",VehicleKind.EBike,9000,140,13,76,80,78,72),
            new MotorModel("e_race","顺风·逐风R",VehicleKind.EBike,16000,170,15,94,90,96,94),
            new MotorModel("t_cargo","丰运·载星",VehicleKind.Tricycle,2500,38,3,85,55,62,58),
            new MotorModel("t_sport","丰运·雷豹",VehicleKind.Tricycle,8800,65,5,90,68,80,72),
            new MotorModel("v_basic","五菱宏途",VehicleKind.Van,28000,110,3.8,62,57,65,66),
            new MotorModel("v_cargo","丰运·小货王",VehicleKind.Van,46000,120,4.2,68,60,74,75),
            new MotorModel("c_city","青禾·家悦",VehicleKind.Car,68000,150,5.2,75,76,78,82),
            new MotorModel("c_su7","大米苏七",VehicleKind.Car,218000,230,9,83,82,91,89),
            new MotorModel("c_718","宝石泰718",VehicleKind.Car,580000,260,10,94,96,98,94),
            new MotorModel("c_921","宝石泰921",VehicleKind.Car,1280000,300,12,96,91,102,99),
            new MotorModel("c_lambo","蓝宝石基尼·星矢",VehicleKind.Car,2680000,330,14,91,86,96,88),
            new MotorModel("c_fa","法梭利·赤焰",VehicleKind.Car,3800000,350,15,98,94,105,101)
        };
        public static MotorModel Get(string id)=>All.FirstOrDefault(m=>m.Id==id);
        // Physical shape, racing category and dealer category are different concepts.
        // Adding a transport type must not silently classify it as a racing car.
        private sealed class Category
        {
            public readonly VehicleKind Kind;
            public readonly string Name, DefaultModel, Dealer;
            public readonly int Shape, Race, Sale;
            public Category(VehicleKind kind, string name, string model, string dealer, int shape, int race, int sale)
            { Kind=kind; Name=name; DefaultModel=model; Dealer=dealer; Shape=shape; Race=race; Sale=sale; }
        }
        private static readonly Category[] Categories = {
            new Category(VehicleKind.EBike,"小电驴","e_city","dealer",0,0,0),
            new Category(VehicleKind.Tricycle,"三轮车","t_cargo","dealer",1,1,1),
            new Category(VehicleKind.Van,"货车","v_cargo","motor_cars",2,-1,2),
            new Category(VehicleKind.Car,"轿车","c_city","motor_cars",2,2,3)
        };
        public static MotorModel Legacy(VehicleKind kind)=>Get(Categories.FirstOrDefault(c=>c.Kind==kind)?.DefaultModel);
        public static int ClassOf(VehicleKind kind)=>Categories.FirstOrDefault(c=>c.Kind==kind)?.Shape??-1;
        public static int RaceClass(VehicleKind kind)=>Categories.FirstOrDefault(c=>c.Kind==kind)?.Race??-1;
        public static string ClassName(int category)=>Categories.FirstOrDefault(c=>c.Race>=0&&c.Race==category)?.Name??"未知车型";
        public static bool CanRace(VehicleKind kind)=>RaceClass(kind)>=0;
        public static int DealerClass(VehicleKind kind)=>Categories.FirstOrDefault(c=>c.Kind==kind)?.Sale??-1;
        public static string DealerClassName(int category)=>Categories.FirstOrDefault(c=>c.Sale==category)?.Name??"未知车型";
        public static bool SoldBy(string dealer,VehicleKind kind)=>dealer!=null&&Categories.Any(c=>c.Kind==kind&&c.Dealer==dealer);
        public static long ResaleValue(GarageVehicle vehicle)=>(long)(Get(vehicle.ModelId).Price*.6*Math.Max(0,Math.Min(100,vehicle.Condition))/100);
        public static MotorModel Performance(GarageVehicle v)
        {
            var m=Get(v.ModelId);double wear=(.75+.25*Math.Max(0,Math.Min(100,v.Condition))/100)*(v.EventBatteryWorn?.75:1);
            return new MotorModel(m.Id,m.Name,m.Kind,m.Price,m.Speed*(1+.07*v.Battery)*wear,m.Acceleration*(1+.13*v.Motor)*wear,
                Math.Min(125,m.Grip+8*v.Tyres),Math.Min(125,m.Handling+8*v.Steering),
                Math.Min(125,m.Braking+6*v.Tyres)*wear,Math.Min(125,m.Stability+6*v.Steering)*wear);
        }
        // Fixed shared scale: buying/upgrading another model never changes existing ratings.
        // All six values use 0..100; raw speed and acceleration remain visible beside the chart.
        public static double[] PerformanceRatings(MotorModel model)
        {
            return new[]{model.Speed/450,model.Acceleration/22,model.Grip/125,
                model.Handling/125,model.Braking/125,model.Stability/125}
                .Select(v=>Math.Max(0,Math.Min(100,v*100))).ToArray();
        }
        public static string PartName(GarageVehicle v,int part)=>new[]{ClassOf(Get(v.ModelId).Kind)==0?"电机":"动力总成",ClassOf(Get(v.ModelId).Kind)==0?"电池与电控":"传动系统","轮毂与轮胎","转向与悬挂"}[part];
        public static int Level(GarageVehicle v,int part)=>part==0?v.Motor:part==1?v.Battery:part==2?v.Tyres:v.Steering;
        public static long UpgradePrice(GarageVehicle v,int part)=>(long)(Get(v.ModelId).Price*.12*(Level(v,part)+1))+100;
    }
    /// <summary>One tier definition shared by racing, parking and second-hand sourcing.</summary>
    public sealed class RacingTierRules
    {
        public readonly int Laps, RequiredWins, OpenMinute, CloseMinute;
        public readonly double HalfRoad;
        public readonly long ParkingRent, ParkingPrice, UsedVehicleCeiling;
        private RacingTierRules(int laps,int wins,double halfRoad,int open,int close,long rent,long price,long ceiling)
        { Laps=laps; RequiredWins=wins; HalfRoad=halfRoad; OpenMinute=open; CloseMinute=close;
            ParkingRent=rent; ParkingPrice=price; UsedVehicleCeiling=ceiling; }
        private static readonly RacingTierRules[] All = {
            new RacingTierRules(2,0,5,0,1440,500,10000,100000),
            new RacingTierRules(2,1,7,540,1080,500,10000,600000),
            new RacingTierRules(3,3,7,540,1080,500,10000,1500000),
            new RacingTierRules(3,6,7,540,1080,500,10000,4000000)
        };
        public static RacingTierRules For(int tier)
        {
            if (tier < 0 || tier >= All.Length) throw new ArgumentOutOfRangeException(nameof(tier));
            return All[tier];
        }
    }
    public sealed class RaceTrack
    {
        public string Id,Name,Street,Npc; public int Tier; public double X,Y,Width,Depth; public MapPoint[] Points;
        private RacingTierRules Rules=>RacingTierRules.For(Tier);
        public int Laps=>Rules.Laps; public int Fee=>RacingSchedule.Fee(RaceMeeting.Small); public int RequiredWins=>Rules.RequiredWins;
        public double HalfRoad=>Rules.HalfRoad;
        public int OpenMinute=>Rules.OpenMinute;
        public int CloseMinute=>Rules.CloseMinute;
        public string OpeningHours=>OpenMinute==0&&CloseMinute==1440?"24小时全天开放":$"{OpenMinute/60}:{OpenMinute%60:00}—{CloseMinute/60}:{CloseMinute%60:00}";
        public bool IsOpenAt(int minute)=>minute>=OpenMinute&&minute<CloseMinute;
    }
    public static class RaceTracks
    {
        public static readonly RaceTrack[] All={
            Make(0,"城郊练习场","s_start",-470,-120,130,90,new[]{-50d,-20, -25,-32, 32,-32, 52,-12, 52,22, 25,34, -35,30, -52,10}),
            Make(1,"社区俱乐部","s_market",120,-150,170,115,new[]{-68d,-35, -15,-40, 12,-10, 60,-35, 68,10, 36,42, -10,20, -60,40}),
            Make(2,"公园竞速场","s_sports",390,90,220,145,new[]{-88d,-45, -10,-52, 75,-40, 88,0, 60,50, 10,48, -15,10, -80,48}),
            Make(3,"花园大奖赛","s_night",70,450,270,165,new[]{-110d,-50, 50,-55, 110,-30, 110,28, 58,55, 10,25, -45,58, -110,25})
        };
        private static RaceTrack Make(int tier,string name,string street,double x,double y,double width,double depth,double[] points)
        {
            var p=Enumerable.Range(0,points.Length/2).Select(i=>new MapPoint(points[i*2],points[i*2+1])).ToArray();
            // Rounded corners: identical geometry for physics, gates, opponents and the rendered road.
            for(int pass=0;pass<2;pass++)p=Enumerable.Range(0,p.Length).SelectMany(i=>new[]{new MapPoint(p[i].X*.75+p[(i+1)%p.Length].X*.25,p[i].Y*.75+p[(i+1)%p.Length].Y*.25),new MapPoint(p[i].X*.25+p[(i+1)%p.Length].X*.75,p[i].Y*.25+p[(i+1)%p.Length].Y*.75)}).ToArray();
            return new RaceTrack{Id="race_"+tier,Name=name,Street=street,Npc="racer_"+tier,Tier=tier,X=x,Y=y,Width=width,Depth=depth,Points=p};
        }
        public static RaceTrack Get(string id)=>All.FirstOrDefault(t=>t.Id==id);
        public static RaceTrack Get(string id,CityLayout city)
        {
            var track=Get(id);double scale=city?.ExpansionScale??1;
            if(track==null||Math.Abs(scale-1)<1e-9)return track;
            // Driving dimensions, steering and lap times remain those of the existing circuit.
            return new RaceTrack {Id=track.Id,Name=track.Name,Street=track.Street,Npc=track.Npc,Tier=track.Tier,
                X=track.X*scale,Y=track.Y*scale,Width=track.Width,Depth=track.Depth,Points=track.Points};
        }
    }
}
