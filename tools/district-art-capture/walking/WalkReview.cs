using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEngine;

// Installed only in the disposable review project's Editor directory.
public static class WalkReview
{
    private const double TargetTolerance = .07;
    private const double StepLength = .27;
    private static string F(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
    private static double Distance(MapPoint a, MapPoint b) =>
        Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));

    public static void Run(GameController game, string output)
    {
        var avatar = (AvatarView)typeof(GameController).GetField("_avatar",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var session = game.Session;
        using (var trace = new StreamWriter(Path.Combine(output,"park-walk-trace.tsv")))
        using (var summary = new StreamWriter(Path.Combine(output,"park-walk-summary.tsv")))
        {
            trace.WriteLine("park\tphase\tstep\ttarget_x\ttarget_z\tplayer_x\tplayer_z\tavatar_x\tavatar_y\tavatar_z\tlocal_x\tlocal_z");
            summary.WriteLine("park\tcheck\tresult\tmeasure");
            int failed=0;
            foreach (var id in new[]{"life_park","life_community_park"})
            {
                try { CheckPark(id); }
                catch (Exception ex)
                {
                    failed++;
                    summary.WriteLine(id+"\toverall\tFAIL\t"+ex.Message);
                    Debug.LogWarning("WALK_REVIEW_FAIL "+id+" "+ex.Message);
                }
                finally { session.SetSteer(0,0); trace.Flush(); summary.Flush(); }
            }
            if(failed>0) throw new InvalidOperationException("WALK_REVIEW: "+failed+" park(s) failed; see park-walk-summary.tsv");
            Debug.Log("WALK_REVIEW_OK parks=2 ringPoints=128 waterProbes=2 streetReturns=2");

            void CheckPark(string id)
            {
                var site = game.World.Places[id];
                var place = site.Place;
                var door = session.DoorOf(place);
                var road = game.City.ViewPosition(place.NodeId);
                var path = new ParkPondWalk(place.Width,place.Depth,place.Heading,door);
                const int gate = ParkPondWalk.Segments*3/4;
                var start = path.World(path.Centerline(gate));
                game.Ui.CloseAllWindows();
                session.Leave();
                session.ArriveAtNode(place.NodeId);  // Sole placement, before this park's walk starts.
                if(!session.SetVehicle(VehicleKind.Walk)) throw new InvalidOperationException("Could not select walking");
                session.Time.SetSpeed(1);
                game.Step(0);
                int steps=0, visited=0;
                float minimumHeight=float.PositiveInfinity;
                double speed=CityTravelTiming.PlayerScaledSecondsMeters(session.CurrentVehicle,
                    session.Time.Speed*TimeController.WorldRate,session.Time.SecondsPerMinuteAt1x)/game.City.TravelDistanceScale;
                if(!(speed>0)) throw new InvalidOperationException("Invalid walk speed");
                MapPoint Current() => session.PlayerPosition;
                void Record(string phase,MapPoint target)
                {
                    var at=Current(); var actor=avatar.Position; var local=site.Root.transform.InverseTransformPoint(actor);
                    trace.WriteLine(string.Join("\t",id,phase,steps.ToString(),F(target.X),F(target.Y),F(at.X),F(at.Y),
                        F(actor.x),F(actor.y),F(actor.z),F(local.x),F(local.z)));
                    if(Math.Abs(actor.x-at.X)>.035 || Math.Abs(actor.z-at.Y)>.035)
                        throw new InvalidOperationException(phase+" rendered avatar separated from player");
                }
                void Input(string phase,MapPoint target,double distance,bool onRing)
                {
                    var at=Current();
                    session.SetSteer(target.X-at.X,target.Y-at.Y);
                    game.Step((float)Math.Min(TimeController.MaxTickSeconds,Math.Min(StepLength,distance)/speed));
                    steps++;
                    Record(phase,target);
                    if(onRing)
                    {
                        minimumHeight=Math.Min(minimumHeight,avatar.Position.y);
                        if(Math.Abs(avatar.Position.y-ParkPondWalk.Top)>.035f)
                            throw new InvalidOperationException(phase+" rendered walk height="+F(avatar.Position.y));
                    }
                }
                void WalkTo(string phase,MapPoint target,bool onRing=false)
                {
                    for(int i=0;i<240;i++)
                    {
                        double remaining=Distance(Current(),target);
                        if(remaining<=TargetTolerance)return;
                        Input(phase,target,remaining,onRing);
                        if(i>6 && Math.Abs(Distance(Current(),target)-remaining)<.000001)
                            break;
                    }
                    throw new InvalidOperationException(phase+" target error="+F(Distance(Current(),target))+
                        " paused="+!session.Time.IsRunning+" tutorial="+(session.RequiredTutorialPaused || session.OpeningClockFrozen)+
                        " event="+(session.PendingEvent!=null));
                }
                void Pass(string check,string measure)=>summary.WriteLine(string.Join("\t",id,check,"PASS",measure));

                Record("street-start",road);
                if(Distance(Current(),road)>TargetTolerance) throw new InvalidOperationException("Start node did not match player");
                WalkTo("gate-in",door);
                WalkTo("ring-in",start);
                if(Math.Abs(avatar.Position.y-ParkPondWalk.Top)>.035f)throw new InvalidOperationException("Gate failed to reach ring height");
                Pass("street-to-ring","distance_from_gate="+F(Distance(door,start)));
                for(int i=1;i<=ParkPondWalk.Segments;i++)
                {
                    int sector=(gate+i)%ParkPondWalk.Segments;
                    var center=path.World(path.Centerline(sector));
                    WalkTo("ring-"+sector,center,true);
                    visited++;
                    if(sector==0)
                    {
                        // Cross the riding line squarely toward deep water; several extra input ticks
                        // must leave the actor stopped at the bank with their feet on the walk.
                        var water=path.World(new MapPoint(0,path.CenterZ));
                        var from=Current();
                        MapPoint settled=from;
                        for(int push=0;push<22;push++)
                        {
                            Input("probe-water",water,StepLength,true);
                            if(push==14)settled=Current();
                        }
                        double progress=Distance(from,Current()),slide=Distance(settled,Current());
                        double inward=Distance(settled,water)-Distance(Current(),water);
                        var local=site.Root.transform.InverseTransformPoint(avatar.Position);
                        double normalized=local.x*local.x/(path.RadiusX*path.RadiusX)+
                            (local.z-path.CenterZ)*(local.z-path.CenterZ)/(path.RadiusZ*path.RadiusZ);
                        if(progress<.40 || progress>1.35 || inward>.065 || normalized<.996)
                            throw new InvalidOperationException("Water entry was not blocked at bank: travel="+F(progress)+
                                " inward="+F(inward)+" bank_slide="+F(slide)+" ellipse_squared="+F(normalized));
                        Pass("water-block","input_m="+F(22*StepLength)+" travel_m="+F(progress)+" continued_inward_m="+F(inward)+
                            " bank_slide_m="+F(slide)+" ellipse_squared="+F(normalized));
                        WalkTo("rejoin-ring",center,true);
                    }
                    if(id=="life_park" && sector==6)
                    {
                        var shoulder=path.World(new MapPoint(12.208,19.405));
                        WalkTo("outer-edge-6",shoulder,true);
                        Pass("outer-edge-6","y="+F(avatar.Position.y));
                        WalkTo("rejoin-edge-6",center,true);
                    }
                }
                Pass("continuous-ring","points="+visited+" min_avatar_y="+F(minimumHeight));
                WalkTo("gate-out",door);
                WalkTo("street-return",road);
                session.SetSteer(0,0);
                game.Step(0);
                Record("stopped-street",road);
                if(Math.Abs(avatar.Position.y)>.065f)throw new InvalidOperationException("Unexpected street height "+F(avatar.Position.y));
                Pass("street-return","position_error="+F(Distance(Current(),road))+" elevation="+F(avatar.Position.y));
                Pass("overall","input_ticks="+steps);
                Debug.Log("WALK_REVIEW_PARK "+id+" ring="+visited+" ticks="+steps+" returnError="+F(Distance(Current(),road)));
            }
        }
    }
}
