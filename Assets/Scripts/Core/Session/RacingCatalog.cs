using System;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed class MotorModel
    {
        public readonly string Id,Name; public readonly VehicleKind Kind; public readonly long Price;
        public readonly double Speed,Acceleration,Grip,Handling;
        public MotorModel(string id,string name,VehicleKind kind,long price,double speed,double acceleration,double grip,double handling)
        {Id=id;Name=name;Kind=kind;Price=price;Speed=speed;Acceleration=acceleration;Grip=grip;Handling=handling;}
    }
    [Serializable] public sealed class GarageVehicle
    {
        public string Id,ModelId,GarageId; public int Motor,Battery,Tyres,Steering; public double Condition=100;
        public bool Commuter,Impounded;
        public GarageVehicle Copy()=>(GarageVehicle)MemberwiseClone();
    }
    public static class MotorCatalog
    {
        public static readonly MotorModel[] All={
            new MotorModel("e_city","顺风·轻行",VehicleKind.EBike,1800,80,7,72,88),
            new MotorModel("e_range","顺风·远行",VehicleKind.EBike,3200,96,7.6,80,75),
            new MotorModel("e_turn","顺风·灵燕",VehicleKind.EBike,5600,110,9.6,90,96),
            new MotorModel("e_dash","顺风·疾电",VehicleKind.EBike,9000,140,13,76,80),
            new MotorModel("e_race","顺风·逐风R",VehicleKind.EBike,16000,170,15,94,90),
            new MotorModel("t_cargo","丰运·载星",VehicleKind.Tricycle,2500,38,3,85,55),
            new MotorModel("t_sport","丰运·雷豹",VehicleKind.Tricycle,8800,65,5,90,68),
            new MotorModel("v_basic","五菱宏途",VehicleKind.Van,28000,110,3.8,62,57),
            new MotorModel("v_cargo","丰运·小货王",VehicleKind.Van,46000,120,4.2,68,60),
            new MotorModel("c_city","青禾·家悦",VehicleKind.Car,68000,150,5.2,75,76),
            new MotorModel("c_su7","大米苏七",VehicleKind.Car,218000,230,9,83,82),
            new MotorModel("c_718","宝石泰718",VehicleKind.Car,580000,260,10,94,96),
            new MotorModel("c_921","宝石泰921",VehicleKind.Car,1280000,300,12,96,91),
            new MotorModel("c_lambo","蓝宝石基尼·星矢",VehicleKind.Car,2680000,330,14,91,86),
            new MotorModel("c_fa","法梭利·赤焰",VehicleKind.Car,3800000,350,15,98,94)
        };
        public static MotorModel Get(string id)=>All.FirstOrDefault(m=>m.Id==id);
        public static MotorModel Legacy(VehicleKind k)=>Get(k==VehicleKind.EBike?"e_city":k==VehicleKind.Tricycle?"t_cargo":k==VehicleKind.Van?"v_cargo":"c_city");
        public static int ClassOf(VehicleKind k)=>k==VehicleKind.EBike?0:k==VehicleKind.Tricycle?1:2;
        public static string ClassName(int category)=>category==0?"小电驴":category==1?"三轮车":"轿车";
        public static bool CanRace(VehicleKind kind)=>kind==VehicleKind.EBike||kind==VehicleKind.Tricycle||kind==VehicleKind.Car;
        public static int DealerClass(VehicleKind kind)=>kind==VehicleKind.EBike?0:kind==VehicleKind.Tricycle?1:kind==VehicleKind.Van?2:3;
        public static string DealerClassName(int category)=>new[]{"小电驴","三轮车","货车","轿车"}[category];
        public static MotorModel Performance(GarageVehicle v)
        {
            var m=Get(v.ModelId);double wear=.75+.25*Math.Max(0,Math.Min(100,v.Condition))/100;
            return new MotorModel(m.Id,m.Name,m.Kind,m.Price,m.Speed*(1+.07*v.Battery)*wear,m.Acceleration*(1+.13*v.Motor)*wear,
                Math.Min(125,m.Grip+8*v.Tyres),Math.Min(125,m.Handling+8*v.Steering));
        }
        public static string PartName(GarageVehicle v,int part)=>new[]{ClassOf(Get(v.ModelId).Kind)==0?"电机":"动力总成",ClassOf(Get(v.ModelId).Kind)==0?"电池与电控":"传动系统","轮毂与轮胎","转向与悬挂"}[part];
        public static int Level(GarageVehicle v,int part)=>part==0?v.Motor:part==1?v.Battery:part==2?v.Tyres:v.Steering;
        public static long UpgradePrice(GarageVehicle v,int part)=>(long)(Get(v.ModelId).Price*.12*(Level(v,part)+1))+100;
    }
    public sealed class RaceTrack
    {
        public string Id,Name,Street,Npc; public int Tier; public double X,Y,Width,Depth; public MapPoint[] Points;
        public int Laps=>2+Tier/2; public int Fee=>100; public int RequiredWins=>new[]{0,1,3,6}[Tier];
        public double HalfRoad=>Tier==0?5:7;
        public int OpenMinute=>Tier==0?0:540;
        public int CloseMinute=>Tier==0?1440:1080;
        public string OpeningHours=>Tier==0?"24小时全天开放":"9:00—18:00";
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
    }
}
