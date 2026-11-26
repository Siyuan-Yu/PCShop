using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEngine;

public static class ForecourtReview
{
    public static void RunTogether(GameController game,string folder)
    {
        Exception park=null,shops=null;
        try { WalkReview.Run(game,folder); } catch(Exception ex) { park=ex; }
        try { Run(game,folder); } catch(Exception ex) { shops=ex; }
        if(park!=null || shops!=null)throw new Exception("Movement review: "+park?.Message+" "+shops?.Message);
    }

    private static string F(double n)=>n.ToString("F4",CultureInfo.InvariantCulture);
    private static double Dist(MapPoint a,MapPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));

    private static void Run(GameController game,string folder)
    {
        string[] ids={"expansion_shop_s_market_03","expansion_shop_s_newtown_03"};
        var s=game.Session;
        var actor=(AvatarView)typeof(GameController).GetField("_avatar",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
        using(var log=new StreamWriter(Path.Combine(folder,"shop-forecourt-trace.tsv")))
        using(var summary=new StreamWriter(Path.Combine(folder,"shop-forecourt-summary.tsv")))
        {
            log.WriteLine("shop\tvehicle\tstage\ttick\ttarget_local_x\ttarget_local_z\tactual_local_x\tactual_local_z\tworld_x\tworld_y\tworld_z");
            summary.WriteLine("shop\tvehicle\tcheck\tresult\tmeasure");
            int failures=0;
            foreach(var kind in new[]{VehicleKind.Walk,VehicleKind.Car})
            {
                if(kind==VehicleKind.Car && !s.Owns(kind))
                {
                    // The review save buys a normal commuter car from its in-game dealer.
                    game.Ui.CloseAllWindows();s.Leave();s.Clock.Restore(s.Clock.Today,660);
                    var dealer=game.City.PublicPlaces.First(p=>p.Kind==PlaceKind.Dealer && MotorCatalog.SoldBy(p.Id,kind));
                    s.ArriveAtNode(dealer.NodeId);
                    s.DebugAddCash(MotorCatalog.Legacy(kind).Price+1L);
                    bool purchased=s.Enter(dealer) && s.BuyVehicle(kind);
                    s.Leave();
                    if(!purchased)throw new InvalidOperationException("Could not purchase the review commuter car");
                }
                foreach(var id in ids)
                {
                    try { CheckShop(id,kind); }
                    catch(Exception ex)
                    {
                        failures++;summary.WriteLine(string.Join("\t",id,kind.ToString(),"overall","FAIL",ex.Message));
                        Debug.LogWarning("FORECOURT_REVIEW_FAIL "+id+" "+kind+" "+ex.Message);
                    }
                    finally { s.SetSteer(0,0);log.Flush();summary.Flush(); }
                }
            }
            if(failures>0)throw new InvalidOperationException("FORECOURT_REVIEW failures="+failures);
            Debug.Log("FORECOURT_REVIEW_OK shops=2 doorProbes=6 sideProbes=4 drivingChecks=2");

            void CheckShop(string id,VehicleKind kind)
            {
                var site=game.World.Places[id];var place=site.Place;
                game.Ui.CloseAllWindows();s.Leave();
                s.ArriveAtNode(place.NodeId);
                if(!s.SetVehicle(kind) || s.Vehicle!=kind)throw new InvalidOperationException("Could not select "+kind);
                s.Time.SetSpeed(1);game.Step(0);
                var road=game.City.ViewPosition(place.NodeId);
                var root=site.Root.transform;
                int ticks=0;
                var speed=CityTravelTiming.PlayerScaledSecondsMeters(s.CurrentVehicle,s.Time.Speed*TimeController.WorldRate,
                    s.Time.SecondsPerMinuteAt1x)/game.City.TravelDistanceScale;
                MapPoint World(float x,float z){var p=root.TransformPoint(new Vector3(x,0,z));return new MapPoint(p.x,p.z);}
                Vector3 Local(MapPoint p)=>root.InverseTransformPoint(new Vector3((float)p.X,0,(float)p.Y));
                void Pass(string check,string metric)=>summary.WriteLine(string.Join("\t",id,kind.ToString(),check,"PASS",metric));
                void Tick(string stage,MapPoint target)
                {
                    var p=s.PlayerPosition;double dist=Dist(p,target);
                    s.SetSteer(target.X-p.X,target.Y-p.Y);
                    game.Step((float)Math.Min(TimeController.MaxTickSeconds,Math.Min(.27,dist)/speed));
                    ticks++;var actual=Local(s.PlayerPosition);var to=Local(target);var a=actor.Position;
                    log.WriteLine(string.Join("\t",id,kind.ToString(),stage,ticks.ToString(),F(to.x),F(to.z),F(actual.x),F(actual.z),F(a.x),F(a.y),F(a.z)));
                    if(Math.Abs(a.x-s.PlayerPosition.X)>.035 || Math.Abs(a.z-s.PlayerPosition.Y)>.035)
                        throw new InvalidOperationException(stage+" avatar differs from simulated position");
                }
                void Reach(string stage,MapPoint target)
                {
                    for(int n=0;n<260;n++)
                    {
                        double before=Dist(s.PlayerPosition,target);
                        if(before<=.07)return;
                        Tick(stage,target);
                        if(n>6 && Math.Abs(Dist(s.PlayerPosition,target)-before)<.000001)break;
                    }
                    throw new InvalidOperationException(stage+" unreachable, error_m="+F(Dist(s.PlayerPosition,target)));
                }
                if(kind==VehicleKind.Walk)
                {
                    Reach("enter-apron",World(0,-1.1f));
                    foreach(float x in new[]{-3.4f,0,3.4f})
                    {
                        var lane=World(x,-1.1f);Reach("visit-"+F(x),lane);
                        Pass("visit-"+F(x),"local_z="+F(Local(s.PlayerPosition).z)+" error_m="+F(Dist(lane,s.PlayerPosition)));
                        float maxZ=float.NegativeInfinity;
                        for(int n=0;n<16;n++)
                        {
                            Tick("press-facade-"+F(x),World(x,2.4f));
                            maxZ=Math.Max(maxZ,Local(s.PlayerPosition).z);
                        }
                        if(maxZ<-.15f || maxZ>.10f || Math.Abs(Local(s.PlayerPosition).x-x)>.14f)
                            throw new InvalidOperationException("Facade contact at x="+F(x)+" maximum_z="+F(maxZ));
                        Pass("facade-stop-"+F(x),"maximum_local_z="+F(maxZ));
                        Reach("back-from-facade",lane);
                        if(x==0)continue;
                        float sign=Math.Sign(x),half=(float)(place.Width+1.1)*.5f,furthest=0;
                        for(int n=0;n<17;n++)
                        {
                            Tick("press-outer-edge-"+F(x),World(sign*(half+3),-1.1f));
                            furthest=Math.Max(furthest,sign*Local(s.PlayerPosition).x);
                        }
                        if(Math.Abs(furthest-half)>.15f)throw new InvalidOperationException("Apron edge at x="+F(sign*furthest)+" expected "+F(sign*half));
                        Pass("apron-bound-"+F(x),"max_abs_local_x="+F(furthest)+" edge="+F(half));
                        Reach("back-from-side",lane);
                    }
                    Reach("walk-back-street",road);
                }
                else
                {
                    var next=game.City.Map.NeighborsOf(place.NodeId).Select(e=>game.City.ViewPosition(e.To))
                        .OrderByDescending(point=>Dist(point,road)).First();
                    double length=Dist(next,road),along=Math.Min(6,length*.60);
                    var onRoad=new MapPoint(road.X+(next.X-road.X)*along/length,road.Y+(next.Y-road.Y)*along/length);
                    Reach("drive-street",onRoad);Reach("drive-return",road);
                    Pass("drive-street","out_and_back_m="+F(2*along));
                    // Face the wing from its own side of the street, outside the registered centre door approach.
                    var streetLocal=Local(road);var start=World(3.4f,streetLocal.z);
                    Reach("drive-to-roadside",start);
                    var pedestrianWing=World(3.4f,-1.1f);
                    for(int n=0;n<48;n++)Tick("press-pedestrian-wing",pedestrianWing);
                    var stopped=Local(s.PlayerPosition);
                    double gap=Dist(s.PlayerPosition,pedestrianWing);
                    if(gap<.22)throw new InvalidOperationException("Vehicle entered expanded pedestrian wing; gap="+F(gap));
                    Pass("pedestrian-only-wing","gap_m="+F(gap)+" local_x="+F(stopped.x)+" local_z="+F(stopped.z));
                    Reach("drive-back-street",road);
                }
                s.SetSteer(0,0);game.Step(0);
                Pass("overall","ticks="+ticks+" return_error_m="+F(Dist(s.PlayerPosition,road)));
            }
        }
    }
}

public static partial class TownMatchCamera
{
    static partial void CaptureWalking(GameController game,string folder)
    {
        if(Environment.GetEnvironmentVariable("PCSHOP_WALK_CHECK")=="1")
            ForecourtReview.RunTogether(game,folder);
    }
}
