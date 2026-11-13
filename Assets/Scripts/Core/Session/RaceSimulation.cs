using System;
using System.Collections.Generic;
using PCShopSell.Core.World;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>Fixed-step arcade driving, independent of Unity and city simulation speed.</summary>
    public sealed class RaceSimulation
    {
        public readonly RaceTrack Track; public readonly MotorModel Model; public readonly bool Practice;
        public readonly RaceMeeting Meeting;
        public const int RivalCount=10;
        public double X,Y,Heading,Speed,Elapsed,Penalty,Damage,Countdown=3,BestLap=double.MaxValue;
        public int Next=1,Passed,Lap,Collisions,Recoveries; public bool Finished,Abandoned,Paused;
        public double EventBatteryFailureAt = -1;
        public bool EventBatteryFailed, SponsorSticker, ProtectiveHelmet, ProtectiveSuit;
        public bool WetSurface;
        public readonly double[] RivalProgress;
        public readonly double[] RivalFinishTime;
        public IReadOnlyList<RaceOpponent> Opponents { get; }
        public int CompetitorCount=>RivalProgress.Length+1;
        public int Laps { get; }
        public double Length {get;} public double Progress {get;private set;}
        public int Position
        {
            get
            {
                int position=1;
                for(int i=0;i<RivalProgress.Length;i++)
                    if(Finished&&!Abandoned?RivalFinishTime[i]<Elapsed+Penalty-1e-7:RivalProgress[i]>Progress)position++;
                return position;
            }
        }
        private double _accumulator,_collisionCooldown,_lapStart; private readonly double[] _cumulative, _rivalSpeeds;
        public double DisplayKph=>Math.Abs(Speed)*7.2;
        public RaceSimulation(RaceTrack track,MotorModel model,bool practice,RaceMeeting meeting=RaceMeeting.Small,
            IReadOnlyList<RaceOpponent> opponents = null, int? lapLimit = null)
        {
            Track=track;Model=model;Practice=practice;Meeting=practice?RaceMeeting.None:meeting;_cumulative=new double[track.Points.Length+1];
            Laps = lapLimit ?? track.Laps;
            Opponents = practice || opponents == null ? Array.Empty<RaceOpponent>() : opponents.ToArray();
            RivalProgress=new double[practice ? 0 : opponents?.Count ?? RivalCount];
            RivalFinishTime=Enumerable.Repeat(double.PositiveInfinity,RivalProgress.Length).ToArray();
            _rivalSpeeds = new double[RivalProgress.Length];
            double speedCap = (10 + Track.Tier * 1.5) * (Model.Kind == VehicleKind.EBike ? 2 : 1);
            double major = Meeting == RaceMeeting.Major ? .08 : 0;
            for (int i = 0; i < RivalProgress.Length; i++)
            {
                RivalProgress[i] = -5 * (1 + i / 2);
                double pace = i < Opponents.Count ? Opponents[i].Pace : .62 + i * .018;
                double driverCap = i < Opponents.Count ? speedCap * (.7 + .4 * pace + major) : speedCap;
                _rivalSpeeds[i] = Math.Min(driverCap, RivalMotor(i).Speed / 7.2 * (pace + .025 * Track.Tier + major));
            }
            for(int i=0;i<track.Points.Length;i++)_cumulative[i+1]=_cumulative[i]+Distance(track.Points[i],track.Points[(i+1)%track.Points.Length]);
            Length=_cumulative[track.Points.Length];ResetPosition();
        }
        public static double Distance(MapPoint a,MapPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
        public void Step(double seconds,double throttle,double steer,bool handbrake=false)
        {
            if(Finished||Paused||double.IsNaN(seconds)||seconds<=0)return;
            _accumulator+=Math.Min(.2,seconds);
            while(_accumulator>=1d/60){_accumulator-=1d/60;Tick(1d/60,Math.Max(-1,Math.Min(1,throttle)),Math.Max(-1,Math.Min(1,steer)),handbrake);}
        }
        private void Tick(double dt,double throttle,double steer,bool brake)
        {
            if(Finished)return;if(Countdown>0){Countdown=Math.Max(0,Countdown-dt);return;}
            Elapsed+=dt;_collisionCooldown=Math.Max(0,_collisionCooldown-dt);
            if (EventBatteryFailureAt >= 0 && Elapsed >= EventBatteryFailureAt)
            { EventBatteryFailed = true; Abandoned = true; Finished = true; Speed = 0; return; }
            double cap=Model.Speed/7.2,accel=Model.Acceleration*.65;
            Speed+=throttle>=0?throttle*accel*dt:throttle*(Speed>0?Model.Braking*.16:accel*.5)*dt;
            Speed*=Math.Max(0,1-(brake?2.8:throttle==0?.28:.025)*dt);Speed=Math.Max(-cap*.18,Math.Min(cap,Speed));
            double yaw=steer*(Model.Handling/80)*1.65*Math.Min(1,Math.Abs(Speed)/3)/(1+Math.Abs(Speed)/25)*(Speed<0?-1:1);
            Heading+=yaw*dt*(brake?1.4:1);
            // High speed on low-grip tyres pushes the car wide; braking restores cornering.
            double slip=Math.Max(0,Math.Abs(Speed)*Math.Abs(yaw)-Model.Grip*.23);
            // Grip controls when sliding starts; stability limits how far the body drifts.
            double drift=yaw*slip*.015*75/Math.Max(20,Model.Stability);
            X+=Math.Sin(Heading-drift)*Speed*dt;Y+=Math.Cos(Heading-drift)*Speed*dt;
            Nearest(X,Y,out double nx,out double ny,out double distance);
            if(distance>Track.HalfRoad)Speed*=Math.Max(.8,1-.9*dt);
            if(distance>Track.HalfRoad+3)
            {
                double before=Math.Abs(Speed);double scale=(Track.HalfRoad+2.8)/Math.Max(.001,distance);X=nx+(X-nx)*scale;Y=ny+(Y-ny)*scale;
                if(_collisionCooldown<=0&&before>2){Collisions++;Damage+=Math.Max(0,before-3)*.75;_collisionCooldown=1.2;}
                Speed*=.45;
            }
            var goal=Track.Points[Next];
            if(Distance(new MapPoint(X,Y),goal)<Track.HalfRoad+1)
            {
                Passed++;Next=(Next+1)%Track.Points.Length;
                if(Next==1){Lap++;BestLap=Math.Min(BestLap,Elapsed-_lapStart);_lapStart=Elapsed;if(Lap>=Laps)Finished=true;}
            }
            int prev=(Next+Track.Points.Length-1)%Track.Points.Length;var a=Track.Points[prev];var b=Track.Points[Next];
            double dx=b.X-a.X,dy=b.Y-a.Y;double along=Math.Max(0,Math.Min(1,((X-a.X)*dx+(Y-a.Y)*dy)/Math.Max(.001,dx*dx+dy*dy)));
            Progress=Lap*Length+_cumulative[prev]+Distance(a,b)*along;
            if(Finished)Progress=Laps*Length;
            if(!Practice)AdvanceRivals(dt,Elapsed+Penalty-dt);
            if(!Practice&&Elapsed>1&&_collisionCooldown<=0)
            {
                for(int i=0;i<RivalProgress.Length;i++)
                {
                    double h;var p=RivalPosition(i,out h);
                    double closing=Math.Abs(Speed-RivalSpeed(i));
                    if(Distance(new MapPoint(X,Y),p)<(MotorCatalog.ClassOf(Model.Kind)==2?1.65:1.1)&&closing>1.6)
                    {Speed*=.72;Damage+=closing*.15;Collisions++;_collisionCooldown=1;break;}
                }
            }
            if(Elapsed>=600){Abandoned=true;Finished=true;}
        }
        public MapPoint AtDistance(double distance,out double heading)
        {
            distance=(distance%Length+Length)%Length;int i=0;while(i<_cumulative.Length-2&&_cumulative[i+1]<distance)i++;
            var a=Track.Points[i];var b=Track.Points[(i+1)%Track.Points.Length];double t=(distance-_cumulative[i])/Math.Max(.001,_cumulative[i+1]-_cumulative[i]);
            heading=Math.Atan2(b.X-a.X,b.Y-a.Y);return new MapPoint(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
        }
        public void Nearest(double x,double y,out double nx,out double ny,out double distance)
        {
            nx=ny=0;distance=double.MaxValue;
            for(int i=0;i<Track.Points.Length;i++){var a=Track.Points[i];var b=Track.Points[(i+1)%Track.Points.Length];double dx=b.X-a.X,dy=b.Y-a.Y;
                double t=Math.Max(0,Math.Min(1,((x-a.X)*dx+(y-a.Y)*dy)/(dx*dx+dy*dy)));double px=a.X+t*dx,py=a.Y+t*dy,d=Math.Sqrt((x-px)*(x-px)+(y-py)*(y-py));if(d<distance){distance=d;nx=px;ny=py;}}
        }
        private void ResetPosition(){int prev=(Next+Track.Points.Length-1)%Track.Points.Length;var p=Track.Points[prev];var q=Track.Points[Next];X=p.X;Y=p.Y;Heading=Math.Atan2(q.X-p.X,q.Y-p.Y);Speed=0;}
        public MapPoint RivalPosition(int i,out double heading)
        {
            var p=AtDistance(RivalProgress[i],out heading);double lane=(i%2==0?-1:1)*Math.Min(2.4,Track.HalfRoad*.42);
            return new MapPoint(p.X+Math.Cos(heading)*lane,p.Y-Math.Sin(heading)*lane);
        }
        public int PrizeForPosition(int position)
        {
            int prize=RacingSchedule.ChampionPrize(Meeting);
            return Practice||Abandoned?0:position==1?prize:position==2?prize*6/10:position==3?prize*3/10:0;
        }
        public MotorModel RivalMotor(int i) => i < Opponents.Count ? Opponents[i].Motor : MotorCatalog.Get(Model.Id) ?? Model;
        private double RivalSpeed(int i) => _rivalSpeeds[i];
        public double RivalResultSeconds(int i) => double.IsPositiveInfinity(RivalFinishTime[i])
            ? Elapsed + Penalty + (Laps * Length - RivalProgress[i]) / RivalSpeed(i) : RivalFinishTime[i];
        private void AdvanceRivals(double seconds,double startTime)
        {
            for(int i=0;i<RivalProgress.Length;i++)
            {
                double remaining=Laps*Length-RivalProgress[i],speed=RivalSpeed(i);
                if(remaining>0&&remaining<=speed*seconds)RivalFinishTime[i]=startTime+remaining/speed;
                RivalProgress[i]=Math.Min(Laps*Length,RivalProgress[i]+seconds*speed);
            }
        }
        public void Recover(){if(Finished||Countdown>0)return;ResetPosition();if(!Practice)AdvanceRivals(5,Elapsed+Penalty);Penalty+=5;Recoveries++;}
        public void Abandon(){Abandoned=true;Finished=true;}
    }
}
