using System;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>Fixed-step arcade driving, independent of Unity and city simulation speed.</summary>
    public sealed class RaceSimulation
    {
        public readonly RaceTrack Track; public readonly MotorModel Model; public readonly bool Practice;
        public double X,Y,Heading,Speed,Elapsed,Penalty,Damage,Countdown=3,BestLap=double.MaxValue;
        public int Next=1,Passed,Lap,Collisions,Recoveries; public bool Finished,Abandoned,Paused;
        public readonly double[] RivalProgress=new double[2];
        public readonly double[] RivalFinishTime={double.PositiveInfinity,double.PositiveInfinity};
        public double Length {get;} public double Progress {get;private set;}
        public int Position=>Finished&&!Abandoned
            ?1+(RivalFinishTime[0]<Elapsed+Penalty-1e-7?1:0)+(RivalFinishTime[1]<Elapsed+Penalty-1e-7?1:0)
            :1+(RivalProgress[0]>Progress?1:0)+(RivalProgress[1]>Progress?1:0);
        private double _accumulator,_collisionCooldown,_lapStart; private readonly double[] _cumulative;
        public double DisplayKph=>Math.Abs(Speed)*7.2;
        public RaceSimulation(RaceTrack track,MotorModel model,bool practice)
        {
            Track=track;Model=model;Practice=practice;_cumulative=new double[track.Points.Length+1];
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
            double cap=Model.Speed/7.2,accel=Model.Acceleration*.65;
            Speed+=throttle>=0?throttle*accel*dt:throttle*(Speed>0?accel*2.5:accel*.5)*dt;
            Speed*=Math.Max(0,1-(brake?2.8:throttle==0?.28:.025)*dt);Speed=Math.Max(-cap*.18,Math.Min(cap,Speed));
            double yaw=steer*(Model.Handling/80)*1.65*Math.Min(1,Math.Abs(Speed)/3)/(1+Math.Abs(Speed)/25)*(Speed<0?-1:1);
            Heading+=yaw*dt*(brake?1.4:1);
            // High speed on low-grip tyres pushes the car wide; braking restores cornering.
            double slip=Math.Max(0,Math.Abs(Speed)*Math.Abs(yaw)-Model.Grip*.23);
            X+=Math.Sin(Heading-yaw*slip*.015)*Speed*dt;Y+=Math.Cos(Heading-yaw*slip*.015)*Speed*dt;
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
                if(Next==1){Lap++;BestLap=Math.Min(BestLap,Elapsed-_lapStart);_lapStart=Elapsed;if(Lap>=Track.Laps)Finished=true;}
            }
            int prev=(Next+Track.Points.Length-1)%Track.Points.Length;var a=Track.Points[prev];var b=Track.Points[Next];
            double dx=b.X-a.X,dy=b.Y-a.Y;double along=Math.Max(0,Math.Min(1,((X-a.X)*dx+(Y-a.Y)*dy)/Math.Max(.001,dx*dx+dy*dy)));
            Progress=Lap*Length+_cumulative[prev]+Distance(a,b)*along;
            if(Finished)Progress=Track.Laps*Length;
            if(!Practice)AdvanceRivals(dt,Elapsed+Penalty-dt);
            if(!Practice&&Elapsed>1&&_collisionCooldown<=0)
            {
                for(int i=0;i<2;i++)
                {
                    double h;var rival=AtDistance(RivalProgress[i],out h);double lane=i==0?-2:2;
                    var p=new MapPoint(rival.X+Math.Cos(h)*lane,rival.Y-Math.Sin(h)*lane);
                    double closing=Math.Abs(Speed-RivalSpeed(i));
                    if(Distance(new MapPoint(X,Y),p)<(MotorCatalog.ClassOf(Model.Kind)==2?1.65:1.1)&&closing>1.6)
                    {Speed*=.72;Damage+=closing*.15;Collisions++;_collisionCooldown=1;break;}
                }
            }
            if(Elapsed>=600){Abandoned=true;Finished=true;}
        }
        public MapPoint AtDistance(double distance,out double heading)
        {
            distance=Math.Max(0,distance)%Length;int i=0;while(i<_cumulative.Length-2&&_cumulative[i+1]<distance)i++;
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
        private double RivalSpeed(int i)=>Math.Min(8+Track.Tier*1.2,MotorCatalog.Get(Model.Id).Speed/7.2*(.38+.025*Track.Tier+i*.055));
        private void AdvanceRivals(double seconds,double startTime)
        {
            for(int i=0;i<2;i++)
            {
                double remaining=Track.Laps*Length-RivalProgress[i],speed=RivalSpeed(i);
                if(remaining>0&&remaining<=speed*seconds)RivalFinishTime[i]=startTime+remaining/speed;
                RivalProgress[i]=Math.Min(Track.Laps*Length,RivalProgress[i]+seconds*speed);
            }
        }
        public void Recover(){if(Finished||Countdown>0)return;ResetPosition();if(!Practice)AdvanceRivals(5,Elapsed+Penalty);Penalty+=5;Recoveries++;}
        public void Abandon(){Abandoned=true;Finished=true;}
    }
}
