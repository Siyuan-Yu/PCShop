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
        private const double StartingGridOffset = 7;
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
        private double _accumulator,_collisionCooldown,_lapStart;
        private double _driftAngle;
        private readonly double[] _cumulative, _rivalSpeeds, _rivalSpeedLimits, _rivalAccelerations;
        public double DisplayKph=>Math.Abs(Speed)*7.2;
        public int StartLights => Countdown > 0 ? Math.Max(1, (int)Math.Ceiling(Countdown - 1e-8)) : 0;
        public bool ShowGo => StartLights == 0 && Elapsed < .8 && !Finished;
        public double ThrottleInput { get; private set; }
        public double BrakeInput { get; private set; }
        public bool Reversing => Speed < 0;
        public bool HandbrakeHeld { get; private set; }
        public bool Drifting => Math.Abs(_driftAngle) > .10 && Speed > 1.5;
        public RaceSimulation(RaceTrack track,MotorModel model,bool practice,RaceMeeting meeting=RaceMeeting.Small,
            IReadOnlyList<RaceOpponent> opponents = null, int? lapLimit = null)
        {
            Track=track;Model=model;Practice=practice;Meeting=practice?RaceMeeting.None:meeting;_cumulative=new double[track.Points.Length+1];
            Laps = lapLimit ?? track.Laps;
            Opponents = practice || opponents == null ? Array.Empty<RaceOpponent>() : opponents.ToArray();
            RivalProgress=new double[practice ? 0 : opponents?.Count ?? RivalCount];
            RivalFinishTime=Enumerable.Repeat(double.PositiveInfinity,RivalProgress.Length).ToArray();
            _rivalSpeeds = new double[RivalProgress.Length];
            _rivalSpeedLimits = new double[RivalProgress.Length];
            _rivalAccelerations = new double[RivalProgress.Length];
            double major = Meeting == RaceMeeting.Major ? .06 : 0;
            for (int i = 0; i < RivalProgress.Length; i++)
            {
                RivalProgress[i] = -StartingGridOffset - 5 * (1 + i / 2);
                var motor = RivalMotor(i);
                double pace = i < Opponents.Count ? Opponents[i].Pace : .79 + i * .011;
                double skill = Math.Min(1, pace + .015 * Track.Tier + major);
                _rivalSpeedLimits[i] = motor.Speed / 7.2 * skill;
                _rivalAccelerations[i] = motor.Acceleration * .65 * (.82 + .18 * skill);
            }
            for(int i=0;i<track.Points.Length;i++)_cumulative[i+1]=_cumulative[i]+Distance(track.Points[i],track.Points[(i+1)%track.Points.Length]);
            Length = _cumulative[track.Points.Length];
            var start = AtDistance(-StartingGridOffset, out Heading);
            X = start.X;
            Y = start.Y;
            Progress = -StartingGridOffset;
        }
        public static double Distance(MapPoint a,MapPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
        public void Step(double seconds,double throttle,double steer,bool handbrake=false)
        {
            if(Finished||Paused||double.IsNaN(seconds)||seconds<=0)return;
            throttle = Math.Max(-1, Math.Min(1, throttle));
            steer = Math.Max(-1, Math.Min(1, steer));
            ThrottleInput = Math.Max(0, throttle);
            BrakeInput = Math.Max(0, -throttle);
            HandbrakeHeld = handbrake;
            _accumulator += Math.Min(.2, seconds);
            const double tickSeconds = 1d / 60;
            while (_accumulator >= tickSeconds && !Finished)
            {
                _accumulator -= tickSeconds;
                Tick(tickSeconds, throttle, steer, handbrake);
            }
        }
        private void Tick(double dt,double throttle,double steer,bool handbrake)
        {
            if (Countdown > 0)
            {
                Countdown = Countdown > dt + 1e-8 ? Countdown - dt : 0;
                return;
            }
            Elapsed+=dt;_collisionCooldown=Math.Max(0,_collisionCooldown-dt);
            if (EventBatteryFailureAt >= 0 && Elapsed >= EventBatteryFailureAt)
            { EventBatteryFailed = true; Abandoned = true; Finished = true; Speed = 0; return; }
            Drive(dt, throttle, steer, handbrake);
            Nearest(X,Y,out double nx,out double ny,out double distance);
            if(distance>Track.HalfRoad)Speed*=Math.Max(.8,1-.9*dt);
            if(distance>Track.HalfRoad+3)
            {
                double before=Math.Abs(Speed);double scale=(Track.HalfRoad+2.8)/Math.Max(.001,distance);X=nx+(X-nx)*scale;Y=ny+(Y-ny)*scale;
                if(_collisionCooldown<=0&&before>2){Collisions++;Damage+=Math.Max(0,before-3)*.75;_collisionCooldown=1.2;}
                Speed*=.45;
                _driftAngle*=.4;
            }
            var goal=Track.Points[Next];
            if(Distance(new MapPoint(X,Y),goal)<Track.HalfRoad+1)
            {
                Passed++;Next=(Next+1)%Track.Points.Length;
                if(Next==1){Lap++;BestLap=Math.Min(BestLap,Elapsed-_lapStart);_lapStart=Elapsed;if(Lap>=Laps)Finished=true;}
            }
            int prev=(Next+Track.Points.Length-1)%Track.Points.Length;var a=Track.Points[prev];var b=Track.Points[Next];
            double dx=b.X-a.X,dy=b.Y-a.Y;
            double segmentLength = _cumulative[prev + 1] - _cumulative[prev];
            double along = ((X-a.X)*dx+(Y-a.Y)*dy)/Math.Max(.001,segmentLength*segmentLength);
            double firstGridProgress = Passed == 0 ? -StartingGridOffset : 0;
            Progress = Lap * Length + _cumulative[prev] + Math.Max(firstGridProgress, Math.Min(segmentLength, segmentLength * along));
            if(Finished)Progress=Laps*Length;
            if(!Practice)AdvanceRivals(dt,Elapsed+Penalty-dt);
            if(!Practice&&Elapsed>1&&_collisionCooldown<=0)
            {
                for(int i=0;i<RivalProgress.Length;i++)
                {
                    if (!double.IsPositiveInfinity(RivalFinishTime[i])) continue;
                    double h;var p=RivalPosition(i,out h);
                    double closing=Math.Abs(Speed-RivalSpeed(i));
                    if(Distance(new MapPoint(X,Y),p)<(MotorCatalog.ClassOf(Model.Kind)==2?1.65:1.1)&&closing>1.6)
                    {Speed*=.72;Damage+=closing*.15;Collisions++;_collisionCooldown=1;break;}
                }
            }
            if(Elapsed>=600){Abandoned=true;Finished=true;}
        }
        private void Drive(double dt, double throttle, double steer, bool handbrake)
        {
            double cap = Model.Speed / 7.2;
            double acceleration = Model.Acceleration * .65;
            if (throttle < 0 && Speed > 0)
                Speed = Math.Max(0, Speed + throttle * Model.Braking * .16 * dt);
            else
                Speed += throttle * acceleration * (throttle < 0 ? .5 : 1) * dt;

            double drag = handbrake ? 1.25 : throttle == 0 ? .28 : .025;
            Speed = Math.Max(-cap * .18, Math.Min(cap, Speed * (1 - drag * dt)));
            double motionSpeed = Math.Abs(Speed);
            double yaw = steer * (Model.Handling / 80) * 1.65 * Math.Min(1, motionSpeed / 3)
                / (1 + motionSpeed / 25) * (Reversing ? -1 : 1);

            // A brief handbrake pull while turning breaks rear grip. The heading of the
            // body reacts first and the direction of travel catches up after release.
            double grip = Model.Grip * (WetSurface ? .78 : 1);
            double driftSpeed = Math.Max(2.3, Math.Min(5, cap * .35));
            double slide = handbrake && !Reversing
                ? Math.Min(1, Math.Max(0, (Speed - driftSpeed) / 2)) * Math.Abs(steer) : 0;
            Heading += yaw * (1 + .65 * slide) * dt;
            double targetDrift = steer * slide * Math.Max(.26, Math.Min(.62, .57 - (grip - 72) * .004));
            double response = slide > 0 ? 11 : Math.Max(2.4, Model.Stability / 25);
            _driftAngle += (targetDrift - _driftAngle) * (1 - Math.Exp(-response * dt));

            double naturalSlip = Math.Max(0, motionSpeed * Math.Abs(yaw) - grip * .23);
            double understeer = yaw * naturalSlip * .015 * 75 / Math.Max(20, Model.Stability);
            double direction = Heading - _driftAngle - understeer;
            X += Math.Sin(direction) * Speed * dt;
            Y += Math.Cos(direction) * Speed * dt;
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
        private void ResetPosition(){int prev=(Next+Track.Points.Length-1)%Track.Points.Length;var p=Track.Points[prev];var q=Track.Points[Next];X=p.X;Y=p.Y;Heading=Math.Atan2(q.X-p.X,q.Y-p.Y);Speed=0;_driftAngle=0;}
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
            ? Elapsed + Penalty + RivalTimeToCover(i, Laps * Length - RivalProgress[i]) : RivalFinishTime[i];

        private double RivalTimeToCover(int i, double distance)
        {
            double speed = _rivalSpeeds[i], limit = _rivalSpeedLimits[i], acceleration = _rivalAccelerations[i];
            double timeToLimit = (limit - speed) / acceleration;
            double distanceToLimit = (limit + speed) * .5 * timeToLimit;
            if (distance >= distanceToLimit) return timeToLimit + (distance - distanceToLimit) / limit;
            return (Math.Sqrt(speed * speed + 2 * acceleration * distance) - speed) / acceleration;
        }

        private void AdvanceRivals(double seconds,double startTime)
        {
            double finish = Laps * Length;
            for (int i = 0; i < RivalProgress.Length; i++)
            {
                if (!double.IsPositiveInfinity(RivalFinishTime[i])) continue;
                double speed = _rivalSpeeds[i], limit = _rivalSpeedLimits[i], acceleration = _rivalAccelerations[i];
                double accelerating = Math.Min(seconds, (limit - speed) / acceleration);
                double nextSpeed = Math.Min(limit, speed + acceleration * accelerating);
                double distance = (speed + nextSpeed) * .5 * accelerating + limit * (seconds - accelerating);
                double remaining = finish - RivalProgress[i];
                if (distance >= remaining)
                {
                    RivalFinishTime[i] = startTime + RivalTimeToCover(i, remaining);
                    RivalProgress[i] = finish;
                }
                else RivalProgress[i] += distance;
                _rivalSpeeds[i] = nextSpeed;
            }
        }
        public void Recover(){if(Finished||Countdown>0)return;ResetPosition();if(!Practice)AdvanceRivals(5,Elapsed+Penalty);Penalty+=5;Recoveries++;}
        public void Abandon(){Abandoned=true;Finished=true;}
    }
}
